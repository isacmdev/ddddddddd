// <copyright file="BackendIdentityLifecycleClient.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Bounded production adapter for the v1 pre-pair, pairing, and definitive-refresh contract.
/// Route availability remains an external gate; transport uncertainty always fails closed.
/// </summary>
internal sealed class BackendIdentityLifecycleClient : IBackendIdentityLifecyclePortV1
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;
    private readonly string publishableKey;
    private string? prePairAccessToken;
    private string? prePairRefreshToken;
    private DateTimeOffset prePairExpiresAt;

    public BackendIdentityLifecycleClient(HttpClient httpClient, string baseUrl, string publishableKey)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.baseUrl = !string.IsNullOrWhiteSpace(baseUrl)
            ? baseUrl.TrimEnd('/')
            : throw new ArgumentException("A backend URL is required.", nameof(baseUrl));
        this.publishableKey = !string.IsNullOrWhiteSpace(publishableKey)
            ? publishableKey
            : throw new ArgumentException("A publishable key is required.", nameof(publishableKey));
    }

    public async Task<BackendSessionStepV1> RecoverOrCreatePrePairSessionAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(this.prePairAccessToken) &&
            !string.IsNullOrWhiteSpace(this.prePairRefreshToken) &&
            this.prePairExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return BackendSessionStepV1.PrePair(
                this.prePairAccessToken,
                this.prePairRefreshToken,
                this.prePairExpiresAt);
        }

        using var request = this.CreatePublishableRequest(
            HttpMethod.Post,
            "/auth/v1/anonymous",
            JsonContent.Create(new { data = new { } }));
        var response = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.Response is null)
        {
            return BackendSessionStepV1.Failed(response.Error);
        }

        using (response.Response)
        {
            if (!response.Response.IsSuccessStatusCode)
            {
                return BackendSessionStepV1.Failed(MapStatus(response.Response.StatusCode));
            }

            var session = await ReadBoundedAsync<SessionResponse>(response.Response, cancellationToken).ConfigureAwait(false);
            if (!TryGetSession(session, out var accessToken, out var refreshToken, out var expiresAt))
            {
                return BackendSessionStepV1.Failed(BackendIdentityErrorV1.InvalidPayload);
            }

            this.prePairAccessToken = accessToken;
            this.prePairRefreshToken = refreshToken;
            this.prePairExpiresAt = expiresAt;
            return BackendSessionStepV1.PrePair(accessToken, refreshToken, expiresAt);
        }
    }

    public async Task<BackendPairingStepV1> PairOnceAsync(PairingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(this.prePairAccessToken))
        {
            return BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden);
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{this.baseUrl}/functions/v1/pairing")
        {
            Content = JsonContent.Create(new
            {
                code = command.Code,
                age_band = command.AgeBand.ToString().ToLowerInvariant(),
                operation_id = command.OperationId,
                device_name = command.DeviceName,
                os_version = command.OsVersion,
                app_version = command.AppVersion,
            }),
        };
        request.Headers.Authorization = new("Bearer", this.prePairAccessToken);
        request.Headers.Add("apikey", this.publishableKey);

        var sent = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (sent.Response is null)
        {
            return BackendPairingStepV1.Failed(sent.Error, uncertain: sent.Error == BackendIdentityErrorV1.Timeout);
        }

        using (sent.Response)
        {
            if (!sent.Response.IsSuccessStatusCode)
            {
                return BackendPairingStepV1.Failed(
                    MapStatus(sent.Response.StatusCode),
                    sent.Response.Headers.RetryAfter?.Delta);
            }

            var result = await ReadBoundedAsync<PairingResponse>(sent.Response, cancellationToken).ConfigureAwait(false);
            return result is not null &&
                   !string.IsNullOrWhiteSpace(result.DeviceId) &&
                   !string.IsNullOrWhiteSpace(result.ParentId)
                ? BackendPairingStepV1.Accepted(result.DeviceId, result.ParentId, result.PolicyVersion)
                : BackendPairingStepV1.Failed(BackendIdentityErrorV1.InvalidPayload);
        }
    }

    public Task<BackendPairingStepV1> ReconcilePairingAsync(string operationId, CancellationToken cancellationToken)
        => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Timeout));

    public async Task<BackendSessionStepV1> RefreshDefinitiveSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BackendSessionStepV1.Failed(BackendIdentityErrorV1.Forbidden);
        }

        using var request = this.CreatePublishableRequest(
            HttpMethod.Post,
            "/auth/v1/token?grant_type=refresh_token",
            JsonContent.Create(new { refresh_token = refreshToken }));
        var sent = await this.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (sent.Response is null)
        {
            return BackendSessionStepV1.Failed(sent.Error);
        }

        using (sent.Response)
        {
            if (!sent.Response.IsSuccessStatusCode)
            {
                return BackendSessionStepV1.Failed(MapStatus(sent.Response.StatusCode));
            }

            var session = await ReadBoundedAsync<SessionResponse>(sent.Response, cancellationToken).ConfigureAwait(false);
            if (!TryGetSession(session, out var accessToken, out var rotatedRefreshToken, out var expiresAt) ||
                !DeviceAuthenticator.TryReadExactDeviceClaim(accessToken, out var deviceId))
            {
                return BackendSessionStepV1.Failed(BackendIdentityErrorV1.ClaimMismatch);
            }

            return BackendSessionStepV1.Definitive(
                accessToken,
                rotatedRefreshToken,
                expiresAt,
                new BackendIdentityClaimV1(deviceId!));
        }
    }

    private HttpRequestMessage CreatePublishableRequest(HttpMethod method, string path, HttpContent content)
    {
        var request = new HttpRequestMessage(method, $"{this.baseUrl}{path}") { Content = content };
        request.Headers.Add("apikey", this.publishableKey);
        return request;
    }

    private async Task<SendResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return new(await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false), BackendIdentityErrorV1.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new(null, BackendIdentityErrorV1.Timeout);
        }
        catch (HttpRequestException)
        {
            return new(null, BackendIdentityErrorV1.Timeout);
        }
    }

    private static async Task<T?> ReadBoundedAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await response.Content.LoadIntoBufferAsync(BackendIdentityContractV1.MaximumPayloadBytes, cancellationToken)
                .ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or HttpRequestException)
        {
            return default;
        }
    }

    private static bool TryGetSession(
        SessionResponse? session,
        out string accessToken,
        out string refreshToken,
        out DateTimeOffset expiresAt)
    {
        accessToken = session?.AccessToken ?? string.Empty;
        refreshToken = session?.RefreshToken ?? string.Empty;
        expiresAt = session?.ExpiresAt is > 0
            ? DateTimeOffset.FromUnixTimeSeconds(session.ExpiresAt.Value)
            : default;
        return !string.IsNullOrWhiteSpace(accessToken) &&
               !string.IsNullOrWhiteSpace(refreshToken) &&
               expiresAt != default;
    }

    private static BackendIdentityErrorV1 MapStatus(HttpStatusCode status)
        => status switch
        {
            HttpStatusCode.NotFound => BackendIdentityErrorV1.NotFound,
            HttpStatusCode.Gone => BackendIdentityErrorV1.Gone,
            HttpStatusCode.TooManyRequests => BackendIdentityErrorV1.RateLimited,
            HttpStatusCode.Unauthorized => BackendIdentityErrorV1.Revoked,
            HttpStatusCode.Forbidden => BackendIdentityErrorV1.Forbidden,
            _ => BackendIdentityErrorV1.InvalidPayload,
        };

    private sealed record SendResult(HttpResponseMessage? Response, BackendIdentityErrorV1 Error);

    private sealed class SessionResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_at")]
        public long? ExpiresAt { get; init; }
    }

    private sealed class PairingResponse
    {
        [JsonPropertyName("device_id")]
        public string? DeviceId { get; init; }

        [JsonPropertyName("parent_id")]
        public string? ParentId { get; init; }

        [JsonPropertyName("policy_version")]
        public int PolicyVersion { get; init; }
    }
}
