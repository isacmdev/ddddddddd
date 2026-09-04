// <copyright file="SupabaseRealtimeComposition.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using ControlParental.Domain;
using Newtonsoft.Json;
using Supabase.Realtime;
using Supabase.Realtime.Channel;
using Supabase.Realtime.Models;
using Supabase.Realtime.Socket;

/// <summary>
/// Immutable public configuration for the device-scoped realtime channels.
/// </summary>
public sealed record SupabaseRealtimeConfiguration(
    string Url,
    string PublishableKey,
    string JwtIssuer = "",
    string JwtAudience = "",
    string JwtSigningKeyPem = "",
    string CertificatePins = "");

/// <summary>
/// Default UI registration while the Service-owned identity bridge is unavailable.
/// </summary>
public sealed class UnavailableRealtimeIdentityAuthority : IRealtimeIdentityAuthority
{
    /// <inheritdoc />
    public RealtimeIdentitySnapshot? Current => null;

    /// <inheritdoc />
    public event EventHandler? Changed
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Owns the two realtime channels used by the UI and disposes them together.
/// </summary>
public sealed class SupabaseRealtimeChannels : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SupabaseRealtimeChannels"/> class.
    /// </summary>
    public SupabaseRealtimeChannels(
        IRealtimeChannel policy,
        IRealtimeChannel grants,
        Action? disposeTransport = null)
        : this(policy, grants, deviceId: null, disposeTransport: disposeTransport)
    {
    }

    internal SupabaseRealtimeChannels(
        IRealtimeChannel policy,
        IRealtimeChannel grants,
        string? deviceId,
        Action? disposeTransport)
    {
        this.Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.Grants = grants ?? throw new ArgumentNullException(nameof(grants));
        this.disposeTransport = disposeTransport;
        this.DeviceId = deviceId;
    }

    private readonly Action? disposeTransport;
    private int disposed;

    /// <summary>
    /// Gets the device-scoped policy channel.
    /// </summary>
    public IRealtimeChannel Policy { get; }

    /// <summary>
    /// Gets the device-scoped grants channel.
    /// </summary>
    public IRealtimeChannel Grants { get; }

    /// <summary>
    /// Gets the device identity extracted from the validated backend-issued
    /// access token. It is null for fail-closed channels and test-only channel sets.
    /// </summary>
    public string? DeviceId { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        this.Policy.Dispose();
        this.Grants.Dispose();
        this.disposeTransport?.Invoke();
    }
}

/// <summary>
/// Composes authenticated Supabase Realtime channels. Identity is accepted only
/// from a Service-owned authority and only after JWT and transport validation.
/// Missing or malformed inputs intentionally return fail-closed channels.
/// </summary>
public static class SupabaseRealtimeComposition
{
    private const string RequiredJwtAlgorithm = "RS256";
    private const string RequiredPublishableKeyPrefix = "sb_publishable_";
    private const long JwtClockSkewSeconds = 300;
    private static readonly TimeSpan CertificateProbeTimeout = TimeSpan.FromSeconds(10);
    private static readonly System.Text.RegularExpressions.Regex DeviceIdPattern =
        new("^[A-Za-z0-9._-]{1,128}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// Legacy overload retained as a fail-closed compatibility boundary. A
    /// caller must provide an explicit Service-owned identity authority; no JWT
    /// supplied as configuration can become a realtime credential.
    /// </summary>
    public static SupabaseRealtimeChannels Create(SupabaseRealtimeConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return FailClosed();
    }

    /// <summary>
    /// Creates device-scoped policy and grants channels from a Service-owned
    /// identity lease. The certificate pin is required before any websocket
    /// connection is attempted.
    /// </summary>
    public static SupabaseRealtimeChannels Create(
        SupabaseRealtimeConfiguration configuration,
        IRealtimeIdentityAuthority identityAuthority)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(identityAuthority);

        try
        {
            if (!TryValidateConfiguration(configuration, out var url, out var signingKey))
            {
                return FailClosed();
            }

            var identity = identityAuthority.Current;
            if (identity is null
                || !TryValidateIdentity(configuration, identity, signingKey, out var deviceId))
            {
                signingKey?.Dispose();
                return FailClosed();
            }

            var realtimeUri = new UriBuilder(url)
            {
                Scheme = "wss",
                Path = "/realtime/v1/websocket",
                Query = string.Empty,
            }.Uri;
            PinnedRealtimeTransportSecurity? transportSecurity = null;
            RealtimeSocket? socket = null;
            EventHandler? identityChanged = null;
            try
            {
                transportSecurity = new PinnedRealtimeTransportSecurity(configuration.CertificatePins);
                var socketOptions = new Supabase.Realtime.ClientOptions
                {
                    Parameters = new SocketOptionsParameters
                    {
                        ApiKey = configuration.PublishableKey,
                        Token = identity.AccessToken,
                    },
                };
                socket = new RealtimeSocket(realtimeUri.ToString(), socketOptions);
                var identityInvalidated = 0;
                long activeGeneration = identity.Generation;
                identityChanged = (_, _) =>
                {
                    var currentIdentity = identityAuthority.Current;
                    if (currentIdentity is null
                        || !string.Equals(currentIdentity.DeviceId, deviceId, StringComparison.Ordinal)
                        || currentIdentity.Generation < Volatile.Read(ref activeGeneration))
                    {
                        Volatile.Write(ref identityInvalidated, 1);
                    }
                    else
                    {
                        Volatile.Write(ref identityInvalidated, 0);
                    }

                    if (socket.IsConnected)
                    {
                        socket.Disconnect(
                            System.Net.WebSockets.WebSocketCloseStatus.PolicyViolation,
                            "Service identity changed");
                    }
                };
                identityAuthority.Changed += identityChanged;
                socket.AddErrorHandler((sender, _) =>
                {
                    // The SDK reconnects automatically with the original URL,
                    // which could contain an expired token. Reconnection is
                    // therefore stopped; the next lifecycle connects through
                    // this validation gate with the current Service lease.
                    if (sender is RealtimeSocket failedSocket)
                    {
                        failedSocket.Disconnect(
                            System.Net.WebSockets.WebSocketCloseStatus.EndpointUnavailable,
                            "Realtime transport validation failed");
                    }
                });

                var policyChannel = new RealtimeChannel(
                    socket,
                    $"device:{deviceId}:policy",
                    new ChannelOptions(socketOptions, () => socketOptions.Parameters.Token, new JsonSerializerSettings()));
                var grantsChannel = new RealtimeChannel(
                    socket,
                    $"device:{deviceId}:grants",
                    new ChannelOptions(socketOptions, () => socketOptions.Parameters.Token, new JsonSerializerSettings()));
                var policyBroadcast = policyChannel.Register<BaseBroadcast>(false, false);
                var grantsBroadcast = grantsChannel.Register<BaseBroadcast>(false, false);
                var ensureConnected = async () =>
                {
                    if (!socket.IsConnected)
                    {
                        if (Volatile.Read(ref identityInvalidated) != 0)
                        {
                            throw new InvalidOperationException("The Service-owned realtime identity was revoked.");
                        }

                        await transportSecurity!.ValidateAsync(realtimeUri).ConfigureAwait(false);
                        var currentIdentity = identityAuthority.Current;
                        if (currentIdentity is null
                            || !TryValidateIdentity(configuration, currentIdentity, signingKey, out var currentDeviceId)
                            || !string.Equals(currentDeviceId, deviceId, StringComparison.Ordinal)
                            || currentIdentity.Generation < Volatile.Read(ref activeGeneration))
                        {
                            throw new InvalidOperationException("The Service-owned realtime identity changed.");
                        }

                        Interlocked.Exchange(ref activeGeneration, currentIdentity.Generation);
                        socketOptions.Parameters.Token = currentIdentity.AccessToken;
                        await socket.Connect().ConfigureAwait(false);
                    }
                };

                return new SupabaseRealtimeChannels(
                    new RealtimeChannelAdapter(policyChannel, policyBroadcast, ensureConnected),
                    new RealtimeChannelAdapter(grantsChannel, grantsBroadcast, ensureConnected),
                    deviceId,
                    () =>
                    {
                        identityAuthority.Changed -= identityChanged;
                        DisposeSocket(socket!);
                        transportSecurity!.Dispose();
                        signingKey?.Dispose();
                    });
            }
            catch
            {
                if (socket is not null)
                {
                    DisposeSocket(socket);
                }

                if (identityChanged is not null)
                {
                    identityAuthority.Changed -= identityChanged;
                }

                transportSecurity?.Dispose();
                signingKey?.Dispose();
                throw;
            }
        }
        catch
        {
            return FailClosed();
        }
    }

    /// <summary>
    /// Reads non-secret transport and JWT verification configuration from the
    /// environment and obtains credentials only from the supplied authority.
    /// The legacy realtime-JWT environment variable is deliberately ignored.
    /// </summary>
    public static SupabaseRealtimeChannels CreateFromEnvironment(
        IRealtimeIdentityAuthority identityAuthority)
    {
        ArgumentNullException.ThrowIfNull(identityAuthority);

        return Create(
            new SupabaseRealtimeConfiguration(
                Environment.GetEnvironmentVariable("SUPABASE_URL") ?? string.Empty,
                Environment.GetEnvironmentVariable("SUPABASE_PUBLISHABLE_KEY") ?? string.Empty,
                Environment.GetEnvironmentVariable("CONTROL_PARENTAL_JWT_ISSUER") ?? string.Empty,
                Environment.GetEnvironmentVariable("CONTROL_PARENTAL_JWT_AUDIENCE") ?? string.Empty,
                Environment.GetEnvironmentVariable("CONTROL_PARENTAL_JWT_SIGNING_KEY_PEM") ?? string.Empty,
                Environment.GetEnvironmentVariable("SUPABASE_REALTIME_CERT_PINS") ?? string.Empty),
            identityAuthority);
    }

    /// <summary>
    /// Compatibility overload that cannot obtain a Service-owned identity and
    /// therefore always remains fail closed.
    /// </summary>
    public static SupabaseRealtimeChannels CreateFromEnvironment() => FailClosed();

    private static bool TryValidateConfiguration(
        SupabaseRealtimeConfiguration configuration,
        out Uri url,
        out RSA signingKey)
    {
        url = null!;
        signingKey = null!;
        if (!Uri.TryCreate(configuration.Url, UriKind.Absolute, out Uri? parsedUrl)
            || parsedUrl is null
            || parsedUrl.Scheme != Uri.UriSchemeHttps
            || !IsPublishableKey(configuration.PublishableKey)
            || string.IsNullOrWhiteSpace(configuration.JwtIssuer)
            || string.IsNullOrWhiteSpace(configuration.JwtAudience)
            || string.IsNullOrWhiteSpace(configuration.JwtSigningKeyPem)
            || configuration.JwtSigningKeyPem.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(configuration.CertificatePins))
        {
            return false;
        }

        url = parsedUrl;

        try
        {
            signingKey = RSA.Create();
            signingKey.ImportFromPem(configuration.JwtSigningKeyPem);
            if (signingKey.KeySize < 2048)
            {
                signingKey?.Dispose();
                signingKey = null!;
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or CryptographicException)
        {
            signingKey?.Dispose();
            signingKey = null!;
            return false;
        }
    }

    private static bool TryValidateIdentity(
        SupabaseRealtimeConfiguration configuration,
        RealtimeIdentitySnapshot identity,
        RSA signingKey,
        out string? deviceId)
    {
        deviceId = null;
        if (identity.Generation <= 0
            || string.IsNullOrWhiteSpace(identity.DeviceId)
            || !DeviceIdPattern.IsMatch(identity.DeviceId)
            || string.IsNullOrWhiteSpace(identity.AccessToken))
        {
            return false;
        }

        var parts = identity.AccessToken.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
        {
            return false;
        }

        try
        {
            var signedBytes = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
            var signature = DecodeBase64Url(parts[2]);
            if (signature.Length == 0
                || !signingKey.VerifyData(
                    signedBytes,
                    signature,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1))
            {
                return false;
            }

            using var headerDocument = JsonDocument.Parse(DecodeBase64Url(parts[0]));
            var header = headerDocument.RootElement;
            if (header.ValueKind != JsonValueKind.Object
                || !header.TryGetProperty("alg", out var algorithm)
                || algorithm.ValueKind != JsonValueKind.String
                || !string.Equals(algorithm.GetString(), RequiredJwtAlgorithm, StringComparison.Ordinal))
            {
                return false;
            }

            using var payloadDocument = JsonDocument.Parse(DecodeBase64Url(parts[1]));
            var payload = payloadDocument.RootElement;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (payload.ValueKind != JsonValueKind.Object
                || !HasExactString(payload, "iss", configuration.JwtIssuer)
                || !HasAudience(payload, configuration.JwtAudience)
                || !TryGetUnixTime(payload, "exp", out var expiresAt)
                || expiresAt <= now - JwtClockSkewSeconds
                || !TryGetUnixTime(payload, "nbf", out var notBefore)
                || notBefore > now + JwtClockSkewSeconds
                || !HasExactString(payload, "device_id", identity.DeviceId)
                || !TryGetPositiveInt64(payload, "generation", out var generation)
                || generation != identity.Generation)
            {
                return false;
            }

            deviceId = identity.DeviceId;
            return true;
        }
        catch (Exception exception) when (
            exception is FormatException
                or System.Text.Json.JsonException
                or CryptographicException
                or InvalidOperationException
                or OverflowException)
        {
            return false;
        }
    }

    private static bool HasExactString(JsonElement payload, string name, string expected)
        => payload.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
            && string.Equals(value.GetString(), expected, StringComparison.Ordinal);

    private static bool IsPublishableKey(string value)
        => !string.IsNullOrWhiteSpace(value)
            && value.StartsWith(RequiredPublishableKeyPrefix, StringComparison.Ordinal)
            && value.Length > RequiredPublishableKeyPrefix.Length
            && value.All(character => !char.IsWhiteSpace(character));

    private static bool HasAudience(JsonElement payload, string expected)
    {
        if (!payload.TryGetProperty("aud", out var audience))
        {
            return false;
        }

        if (audience.ValueKind == JsonValueKind.String)
        {
            return string.Equals(audience.GetString(), expected, StringComparison.Ordinal);
        }

        if (audience.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return audience.EnumerateArray().Any(value =>
            value.ValueKind == JsonValueKind.String
            && string.Equals(value.GetString(), expected, StringComparison.Ordinal));
    }

    private static bool TryGetUnixTime(JsonElement payload, string name, out long value)
    {
        value = 0;
        return payload.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.Number
            && element.TryGetInt64(out value);
    }

    private static bool TryGetPositiveInt64(JsonElement payload, string name, out long value)
        => TryGetUnixTime(payload, name, out value) && value > 0;

    private static byte[] DecodeBase64Url(string value)
    {
        if (value.Any(character => !char.IsLetterOrDigit(character) && character is not ('-' or '_'))
            || value.Length % 4 == 1)
        {
            throw new FormatException("Invalid base64url value.");
        }

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty,
        };
        return Convert.FromBase64String(padded);
    }

    private static SupabaseRealtimeChannels FailClosed() =>
        new(new FailClosedRealtimeChannel(), new FailClosedRealtimeChannel());

    private static void DisposeSocket(RealtimeSocket socket)
    {
        if (socket.IsConnected)
        {
            socket.Disconnect(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, "UI closed");
        }
    }
}

/// <summary>
/// Certificate-pinned TLS gate for the Supabase Realtime websocket endpoint.
/// The SDK does not expose a websocket certificate callback, so the same-host
/// TLS probe is required immediately before the SDK opens its connection.
/// </summary>
public sealed class PinnedRealtimeTransportSecurity : IDisposable
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);
    private readonly HttpClient client;
    private readonly HashSet<string> certificatePins;
    private int disposed;

    /// <summary>
    /// Initializes the policy with one or two Base64 SHA-256 SPKI pins.
    /// </summary>
    public PinnedRealtimeTransportSecurity(string configuredPins)
    {
        this.certificatePins = ParsePins(configuredPins);
        var handler = new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CheckCertificateRevocationList = true,
            ServerCertificateCustomValidationCallback = this.ValidateCertificate,
        };
        this.client = new HttpClient(handler)
        {
            Timeout = ProbeTimeout,
        };
    }

    /// <summary>
    /// Performs a same-host HTTPS handshake whose certificate must match the
    /// configured SPKI pins. HTTP status is intentionally not authoritative.
    /// </summary>
    public async Task ValidateAsync(Uri websocketEndpoint, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(this.disposed != 0, this);
        ArgumentNullException.ThrowIfNull(websocketEndpoint);
        if (websocketEndpoint.Scheme != "wss")
        {
            throw new InvalidOperationException("Realtime transport must use wss.");
        }

        var probe = new UriBuilder(websocketEndpoint)
        {
            Scheme = Uri.UriSchemeHttps,
            Path = "/",
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
        using var request = new HttpRequestMessage(HttpMethod.Head, probe);
        using var response = await this.client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);
        _ = response.StatusCode;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            this.client.Dispose();
        }
    }

    private bool ValidateCertificate(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        _ = chain;
        if (request.RequestUri is null
            || request.RequestUri.Scheme != Uri.UriSchemeHttps
            || certificate is null
            || errors != SslPolicyErrors.None
            || DateTimeOffset.UtcNow < certificate.NotBefore.ToUniversalTime()
            || DateTimeOffset.UtcNow > certificate.NotAfter.ToUniversalTime())
        {
            return false;
        }

        try
        {
            var spki = GetSubjectPublicKeyInfo(certificate);
            var computed = Convert.ToBase64String(SHA256.HashData(spki));
            return this.certificatePins.Contains(computed);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static HashSet<string> ParsePins(string configuredPins)
    {
        if (string.IsNullOrWhiteSpace(configuredPins))
        {
            throw new ArgumentException("At least one realtime certificate pin is required.", nameof(configuredPins));
        }

        var pins = configuredPins.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pins.Length is < 1 or > 2)
        {
            throw new ArgumentException("Realtime certificate pinning accepts one or two pins.", nameof(configuredPins));
        }

        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pin in pins)
        {
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(pin);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("Realtime certificate pin is not valid Base64.", nameof(configuredPins), exception);
            }

            if (decoded.Length != 32)
            {
                throw new ArgumentException("Realtime certificate pins must be SHA-256 values.", nameof(configuredPins));
            }

            _ = result.Add(Convert.ToBase64String(decoded));
        }

        return result;
    }

    private static byte[] GetSubjectPublicKeyInfo(X509Certificate2 certificate)
    {
        using var rsa = certificate.GetRSAPublicKey();
        if (rsa is not null)
        {
            return rsa.ExportSubjectPublicKeyInfo();
        }

        using var ecdsa = certificate.GetECDsaPublicKey();
        if (ecdsa is not null)
        {
            return ecdsa.ExportSubjectPublicKeyInfo();
        }

        throw new CryptographicException("The TLS certificate key type is unsupported.");
    }
}
