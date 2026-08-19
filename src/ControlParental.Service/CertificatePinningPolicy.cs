// <copyright file="CertificatePinningPolicy.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControlParental.Domain;

/// <summary>
/// Unified TLS/pinning policy for all service-owned HTTPS traffic.
/// </summary>
public sealed class CertificatePinningPolicy : ITlsPolicy
{
    private const int Sha256Bytes = 32;
    private const int MaximumRotatingPins = 2;
    private readonly HashSet<string> certPins;
    private readonly Func<DateTimeOffset> utcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificatePinningPolicy"/> class.
    /// </summary>
    /// <param name="certPins">Optional semicolon-delimited current and next SPKI pins.</param>
    /// <param name="utcNow">Clock used for explicit certificate-validity checks.</param>
    public CertificatePinningPolicy(string? certPins = null, Func<DateTimeOffset>? utcNow = null)
    {
        this.certPins = ParsePins(certPins);
        this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public HttpClientHandler ConfigureHandler()
    {
        var handler = new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
            CheckCertificateRevocationList = true,
            ServerCertificateCustomValidationCallback = this.RemoteCertificateValidationCallback,
        };

        return handler;
    }

    private bool RemoteCertificateValidationCallback(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        if (request.RequestUri is null || request.RequestUri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (certificate is null || errors != SslPolicyErrors.None)
        {
            return false;
        }

        var now = this.utcNow().UtcDateTime;
        if (now < certificate.NotBefore.ToUniversalTime() || now > certificate.NotAfter.ToUniversalTime())
        {
            return false;
        }

        if (this.certPins.Count == 0)
        {
            return true;
        }

        try
        {
            var computedPin = CertificatePinningValidator.CalculateSpkiPin(certificate);
            return this.certPins.Contains(computedPin);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static HashSet<string> ParsePins(string? configuredPins)
    {
        var pins = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(configuredPins))
        {
            return pins;
        }

        var candidates = configuredPins.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (candidates.Length is < 1 or > MaximumRotatingPins)
        {
            throw new ArgumentException("TLS pin configuration must contain one or two pins.", nameof(configuredPins));
        }

        foreach (var candidate in candidates)
        {
            try
            {
                if (Convert.FromBase64String(candidate).Length != Sha256Bytes)
                {
                    throw new ArgumentException("TLS pin configuration is invalid.", nameof(configuredPins));
                }
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("TLS pin configuration is invalid.", nameof(configuredPins), exception);
            }

            _ = pins.Add(candidate);
        }

        return pins;
    }
}
