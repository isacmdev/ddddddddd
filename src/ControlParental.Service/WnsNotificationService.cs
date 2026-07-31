// <copyright file="WnsNotificationService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ControlParental.Domain;

/// <summary>
/// T14/T19 — Implementación del servicio de notificaciones WNS.
/// </summary>
public sealed class WnsNotificationService : IPushNotificationService
{
    private readonly HttpClient httpClient;
    private readonly string packageSid;
    private readonly string clientSecret;
    private readonly ITimeProvider timeProvider;

    private string? currentToken;
    private DateTimeOffset? tokenExpiresAt;
    private string? accessToken;
    private DateTimeOffset? accessTokenExpiresAt;

    private readonly SemaphoreSlim syncLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="WnsNotificationService"/> class.
    /// </summary>
    public WnsNotificationService(
        HttpClient httpClient,
        string packageSid,
        string clientSecret,
        ITimeProvider timeProvider)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.packageSid = packageSid ?? throw new ArgumentNullException(nameof(packageSid));
        this.clientSecret = clientSecret ?? throw new ArgumentNullException(nameof(clientSecret));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<WnsTokenResult> GetOrRenewTokenAsync(CancellationToken cancellationToken = default)
    {
        await this.syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Check if current token is still valid
            if (!string.IsNullOrEmpty(this.currentToken) &&
                this.tokenExpiresAt.HasValue &&
                this.tokenExpiresAt.Value > this.timeProvider.WallClockNow.AddMinutes(5))
            {
                return WnsTokenResult.Succeeded(this.currentToken, this.tokenExpiresAt);
            }

            // Need to renew - first get access token
            var authResult = await this.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (!authResult.Success)
            {
                return WnsTokenResult.Failed(authResult.ErrorMessage ?? "Failed to get access token");
            }

            this.accessToken = authResult.AccessToken;
            this.accessTokenExpiresAt = authResult.ExpiresAt;

            // Request new WNS channel
            var channelResult = await this.RequestChannelAsync(cancellationToken).ConfigureAwait(false);
            if (!channelResult.Success)
            {
                return WnsTokenResult.Failed(channelResult.ErrorMessage ?? "Failed to request channel");
            }

            this.currentToken = channelResult.ChannelUri;
            this.tokenExpiresAt = channelResult.ExpiresAt;

            return WnsTokenResult.Succeeded(this.currentToken, this.tokenExpiresAt);
        }
        finally
        {
            this.syncLock.Release();
        }
    }

    /// <inheritdoc />
    public string? GetCurrentToken() => this.currentToken;

    /// <inheritdoc />
    public DateTimeOffset? GetTokenExpiresAt() => this.tokenExpiresAt;

    /// <inheritdoc />
    public bool NeedsRenewal()
    {
        if (string.IsNullOrEmpty(this.currentToken))
        {
            return true;
        }

        if (!this.tokenExpiresAt.HasValue)
        {
            return true;
        }

        // Renew if expiring within 5 days
        return this.tokenExpiresAt.Value <= this.timeProvider.WallClockNow.AddDays(5);
    }

    /// <summary>
    /// Obtiene el token de acceso OAuth para WNS.
    /// </summary>
    private async Task<AccessTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = this.packageSid,
                ["client_secret"] = this.clientSecret,
                ["scope"] = "notify.windows.com",
            });

            var response = await this.httpClient.PostAsync(
                "https://login.live.com/oauth20_token.srf",
                content,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return AccessTokenResult.Failed($"HTTP {response.StatusCode}: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (result == null || string.IsNullOrEmpty(result.AccessToken))
            {
                return AccessTokenResult.Failed("Invalid response from OAuth endpoint");
            }

            return AccessTokenResult.Succeeded(
                result.AccessToken,
                this.timeProvider.WallClockNow.AddSeconds(result.ExpiresIn - 60));
        }
        catch (HttpRequestException ex)
        {
            return AccessTokenResult.Failed($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return AccessTokenResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Solicita un nuevo canal WNS.
    /// </summary>
    private async Task<ChannelRequestResult> RequestChannelAsync(CancellationToken cancellationToken)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://cn1.notify.windows.com/secondary/");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.accessToken);
            request.Headers.Add("X-WindowsPhone-Target", "wns");
            request.Headers.Add("X-WNS-RequestForStatusFor", "true");

            var body = new StringContent(
                "<lambda publicationId=\"00000000-0000-0000-0000-000000000000\" />",
                System.Text.Encoding.UTF8,
                "text/xml");

            request.Content = body;

            var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return ChannelRequestResult.Failed($"HTTP {response.StatusCode}: {error}");
            }

            var channelUri = response.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(channelUri))
            {
                return ChannelRequestResult.Failed("No Channel URI in response");
            }

            // WNS channels typically expire in 30 days
            var expiresAt = this.timeProvider.WallClockNow.AddDays(30);

            return ChannelRequestResult.Succeeded(channelUri, expiresAt);
        }
        catch (HttpRequestException ex)
        {
            return ChannelRequestResult.Failed($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ChannelRequestResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Resultado de acceso OAuth.
    /// </summary>
    private class AccessTokenResponse
    {
        public string? AccessToken { get; set; }
        public int ExpiresIn { get; set; }
    }

    /// <summary>
    /// Resultado de acceso token interno.
    /// </summary>
    private class AccessTokenResult
    {
        public bool Success { get; init; }
        public string? AccessToken { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
        public string? ErrorMessage { get; init; }

        public static AccessTokenResult Succeeded(string token, DateTimeOffset expiresAt)
            => new() { Success = true, AccessToken = token, ExpiresAt = expiresAt };

        public static AccessTokenResult Failed(string error)
            => new() { Success = false, ErrorMessage = error };
    }

    /// <summary>
    /// Resultado de request de canal interno.
    /// </summary>
    private class ChannelRequestResult
    {
        public bool Success { get; init; }
        public string? ChannelUri { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
        public string? ErrorMessage { get; init; }

        public static ChannelRequestResult Succeeded(string channelUri, DateTimeOffset expiresAt)
            => new() { Success = true, ChannelUri = channelUri, ExpiresAt = expiresAt };

        public static ChannelRequestResult Failed(string error)
            => new() { Success = false, ErrorMessage = error };
    }
}
