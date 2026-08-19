// <copyright file="BackendClient.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlParental.Domain;

public sealed record BackendReliabilityOptions(TimeSpan RequestTimeout, int MaximumAttempts,
    Func<int, TimeSpan?, TimeSpan> DelayFactory, Func<TimeSpan, CancellationToken, Task> DelayAsync)
{
    public static BackendReliabilityOptions Default { get; } = new(
        TimeSpan.FromSeconds(10),
        2,
        static (attempt, retryAfter) => retryAfter ?? TimeSpan.FromMilliseconds(100 * (1 << attempt) + Random.Shared.Next(0, 51)),
        static (delay, cancellationToken) => Task.Delay(delay, cancellationToken));
}

/// <summary>
/// T14 — Implementación del cliente del backend de Supabase.
/// </summary>
public sealed class BackendClient : IBackendClient
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly string baseUrl;
    private readonly IDeviceAuthenticator? deviceAuthenticator;
    private readonly IBackendIdentityCoordinator? identityCoordinator;
    private readonly BackendReliabilityOptions reliability;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests.</param>
    /// <param name="baseUrl">Supabase base URL.</param>
    /// <param name="deviceAuthenticator">Authenticator for session token (T17).</param>
    [Obsolete("Test compatibility only. Production composition requires IBackendIdentityCoordinator.")]
    internal BackendClient(
        HttpClient httpClient,
        string baseUrl,
        IDeviceAuthenticator deviceAuthenticator)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        this.deviceAuthenticator = deviceAuthenticator ?? throw new ArgumentNullException(nameof(deviceAuthenticator));
        this.reliability = BackendReliabilityOptions.Default;
        this.jsonOptions = CreateJsonOptions();
    }

    public BackendClient(
        HttpClient httpClient,
        string baseUrl,
        IBackendIdentityCoordinator identityCoordinator,
        BackendReliabilityOptions? reliability = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        this.identityCoordinator = identityCoordinator ?? throw new ArgumentNullException(nameof(identityCoordinator));
        this.reliability = reliability ?? BackendReliabilityOptions.Default;
        if (this.reliability.MaximumAttempts is < 1 or > 3 || this.reliability.RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(reliability));
        }

        this.jsonOptions = CreateJsonOptions();
    }

    private static JsonSerializerOptions CreateJsonOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

    /// <summary>
    /// Creates a request message with the current auth token from DeviceAuthenticator.
    /// </summary>
    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var token = this.deviceAuthenticator?.CurrentAccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task<HttpResponseMessage?> SendAuthenticatedAsync(
        Func<HttpRequestMessage> requestFactory,
        bool retryable,
        CancellationToken cancellationToken,
        bool classifyTimeout = false)
    {
        if (this.identityCoordinator is null)
        {
            using var request = requestFactory();
            var legacyToken = this.deviceAuthenticator?.CurrentAccessToken;
            if (!string.IsNullOrWhiteSpace(legacyToken))
            {
                request.Headers.Authorization = new("Bearer", legacyToken);
            }

            return await this.httpClient.SendAsync(request, cancellationToken);
        }

        var idempotencyKey = retryable ? Guid.NewGuid().ToString("N") : null;
        for (var attempt = 0; attempt < this.reliability.MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(this.reliability.RequestTimeout);
            var authorization = await this.identityCoordinator.GetDefinitiveSessionAsync(
                classifyTimeout ? timeout.Token : cancellationToken);

            if (!authorization.IsSuccess)
            {
                if (classifyTimeout && authorization.Error == BackendIdentityErrorV1.Timeout)
                {
                    throw new TaskCanceledException("Backend request timed out", null, timeout.Token);
                }

                return null;
            }

            using var request = requestFactory();
            request.Headers.Authorization = new("Bearer", authorization.Session!.AccessToken);
            if (idempotencyKey is not null)
            {
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            }

            HttpResponseMessage response;
            try
            {
                response = await this.httpClient.SendAsync(request, timeout.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException) when (classifyTimeout)
            {
                throw new TaskCanceledException("Backend request timed out", null, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (HttpRequestException)
            {
                if (retryable && attempt + 1 < this.reliability.MaximumAttempts)
                {
                    await this.DelayForRetryAsync(attempt, null, cancellationToken);
                    continue;
                }

                return null;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                await this.identityCoordinator.InvalidateAsync(
                    authorization.Session.Generation,
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? BackendIdentityErrorV1.Revoked
                        : BackendIdentityErrorV1.Forbidden,
                    cancellationToken);
                return response;
            }

            if (retryable && IsTransient(response.StatusCode) && attempt + 1 < this.reliability.MaximumAttempts)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta;
                response.Dispose();
                await this.DelayForRetryAsync(attempt, retryAfter, cancellationToken);
                continue;
            }

            try
            {
                await response.Content.LoadIntoBufferAsync(64 * 1024, cancellationToken);
                return response;
            }
            catch (HttpRequestException)
            {
                response.Dispose();
                return null;
            }
        }

        return null;
    }

    private Task DelayForRetryAsync(int attempt, TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        var delay = this.reliability.DelayFactory(attempt, retryAfter);
        if (delay < TimeSpan.Zero || delay > TimeSpan.FromSeconds(30))
        {
            delay = TimeSpan.FromSeconds(30);
        }

        return this.reliability.DelayAsync(delay, cancellationToken);
    }

    private static bool IsTransient(HttpStatusCode status)
        => status is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)status >= 500;

    private static HttpRequestMessage CreateUpsertRequest<T>(string url, T payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Prefer", "resolution=merge-duplicates");
        return request;
    }

    /// <inheritdoc />
    public async Task<PolicyFetchResult> FetchPolicyAsync(
        string deviceId,
        int currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{this.baseUrl}/rest/v1/rpc/get_device_policy";
            using var response = await this.SendAuthenticatedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(new { p_device_id = deviceId }),
                },
                retryable: true,
                cancellationToken,
                classifyTimeout: true);

            if (response is null || !response.IsSuccessStatusCode)
            {
                return PolicyFetchResult.Failed(response is null ? "Remote access denied" : $"HTTP {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<PolicyFetchResponse>(
                this.jsonOptions,
                cancellationToken);

            if (result == null || result.Version <= currentVersion)
            {
                return PolicyFetchResult.Succeeded(currentVersion, string.Empty);
            }

            return PolicyFetchResult.Succeeded(result.Version, result.PolicyJson ?? string.Empty);
        }
        catch (HttpRequestException)
        {
            return PolicyFetchResult.Failed("Network error");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            return PolicyFetchResult.Failed("Request timeout");
        }
        catch (Exception)
        {
            return PolicyFetchResult.Failed("Unexpected error");
        }
    }

    /// <inheritdoc />
    public async Task<DataPushResult> PushUsageLogsAsync(
        IEnumerable<UsageLogEntry> usageLogs,
        CancellationToken cancellationToken = default)
    {
        var logsList = usageLogs.ToList();
        if (logsList.Count == 0)
        {
            return DataPushResult.Succeeded(0);
        }

        try
        {
            var url = $"{this.baseUrl}/rest/v1/usage_logs";
            var payload = logsList.Select(l => new
            {
                app_id = l.AppId,
                minutes = l.Minutes,
                server_date = l.ServerDate.ToString("yyyy-MM-dd"),
                dedup_key = l.DedupKey,
            });

            using var response = await this.SendAuthenticatedAsync(
                () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = JsonContent.Create(payload),
                    };
                    request.Headers.Add("Prefer", "resolution=merge-duplicates");
                    return request;
                },
                retryable: true,
                cancellationToken);

            if (response is null || !response.IsSuccessStatusCode)
            {
                return DataPushResult.Failed(response is null ? "Remote access denied" : $"HTTP {response.StatusCode}");
            }

            return DataPushResult.Succeeded(logsList.Count);
        }
        catch (HttpRequestException)
        {
            return DataPushResult.Failed("Network error");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return DataPushResult.Failed("Unexpected error");
        }
    }

    /// <inheritdoc />
    public async Task<DataPushResult> PushDeviceAlertsAsync(
        IEnumerable<DeviceAlertEntry> alerts,
        CancellationToken cancellationToken = default)
    {
        var alertsList = alerts.ToList();
        if (alertsList.Count == 0)
        {
            return DataPushResult.Succeeded(0);
        }

        try
        {
            var url = $"{this.baseUrl}/rest/v1/device_alerts";
            var payload = alertsList.Select(a => new
            {
                event_type = a.EventType,
                description = a.Description,
                severity = a.Severity,
                detected_at = a.DetectedAt.ToString("O"),
                dedup_key = a.DedupKey,
            });

            using var response = await this.SendAuthenticatedAsync(
                () => CreateUpsertRequest(url, payload),
                retryable: true,
                cancellationToken);

            if (response is null || !response.IsSuccessStatusCode)
            {
                return DataPushResult.Failed(response is null ? "Remote access denied" : $"HTTP {response.StatusCode}");
            }

            return DataPushResult.Succeeded(alertsList.Count);
        }
        catch (HttpRequestException)
        {
            return DataPushResult.Failed("Network error");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return DataPushResult.Failed("Unexpected error");
        }
    }

    /// <inheritdoc />
    public async Task<DataPushResult> PushBehavioralEventsAsync(
        IEnumerable<BehavioralEventEntry> events,
        CancellationToken cancellationToken = default)
    {
        var eventsList = events.ToList();
        if (eventsList.Count == 0)
        {
            return DataPushResult.Succeeded(0);
        }

        try
        {
            var url = $"{this.baseUrl}/rest/v1/behavioral_events";
            var payload = eventsList.Select(e => new
            {
                event_type = e.EventType,
                app_id = e.AppId,
                timestamp = e.Timestamp.ToString("O"),
                metadata = e.Metadata,
                dedup_key = e.DedupKey,
            });

            using var response = await this.SendAuthenticatedAsync(
                () => CreateUpsertRequest(url, payload),
                retryable: true,
                cancellationToken);

            if (response is null || !response.IsSuccessStatusCode)
            {
                return DataPushResult.Failed(response is null ? "Remote access denied" : $"HTTP {response.StatusCode}");
            }

            return DataPushResult.Succeeded(eventsList.Count);
        }
        catch (HttpRequestException)
        {
            return DataPushResult.Failed("Network error");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return DataPushResult.Failed("Unexpected error");
        }
    }

    /// <inheritdoc />
    public async Task<HeartbeatResult> SendHeartbeatAsync(
        HeartbeatData heartbeat,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{this.baseUrl}/rest/v1/rpc/heartbeat";
            var payload = new
            {
                enforcement = heartbeat.Enforcement.ToString(),
                battery_pct = heartbeat.BatteryPct,
                clock_offset_ms = heartbeat.ClockOffsetMs,
                agent_uptime_ms = heartbeat.AgentUptimeMs,
            };

            using var response = await this.SendAuthenticatedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) },
                retryable: true,
                cancellationToken);

            if (response is null || !response.IsSuccessStatusCode)
            {
                return HeartbeatResult.Failed(response is null ? "Remote access denied" : $"HTTP {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(
                this.jsonOptions,
                cancellationToken);

            return HeartbeatResult.Succeeded(
                result?.ServerTimeOffsetMs,
                result?.NewPolicyAvailable ?? false);
        }
        catch (HttpRequestException)
        {
            return HeartbeatResult.Failed("Network error");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HeartbeatResult.Failed("Unexpected error");
        }
    }

    /// <inheritdoc />
    public async Task<PushTokenRegistrationResult> RegisterPushTokenAsync(
        string pushToken,
        string channel,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{this.baseUrl}/rest/v1/device_push_tokens";
            var payload = new
            {
                channel = channel,
                push_handle = pushToken,
                expires_at = expiresAt?.ToString("O"),
            };

            using var response = await this.SendAuthenticatedAsync(
                () => CreateUpsertRequest(url, payload),
                retryable: true,
                cancellationToken);

            if (response is null || !response.IsSuccessStatusCode)
            {
                var kind = response?.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => PushTokenRegistrationFailureKind.Revoked,
                    HttpStatusCode.Forbidden => PushTokenRegistrationFailureKind.Forbidden,
                    HttpStatusCode.TooManyRequests => PushTokenRegistrationFailureKind.RateLimited,
                    _ => PushTokenRegistrationFailureKind.RemoteUnavailable,
                };
                return PushTokenRegistrationResult.Failed("WnsRegistrationFailed", kind);
            }

            return PushTokenRegistrationResult.Succeeded(expiresAt);
        }
        catch (HttpRequestException ex)
        {
            return PushTokenRegistrationResult.Failed("WnsRegistrationFailed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return PushTokenRegistrationResult.Failed("WnsRegistrationFailed");
        }
    }

    /// <inheritdoc />
    public async Task<bool> CreateTimeRequestAsync(
        TimeRequestEntry request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{this.baseUrl}/rest/v1/time_requests";
            var payload = new
            {
                request_id = request.RequestId,
                minutes = request.Minutes,
                reason = request.Reason,
                created_at = request.CreatedAt.ToString("O"),
            };

            using var response = await this.SendAuthenticatedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) },
                retryable: true,
                cancellationToken);

            return response?.IsSuccessStatusCode == true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IntegrityReportResult> ReportIntegrityAsync(
        IntegrityReport report,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{this.baseUrl}/rest/v1/integrity_reports";
            var payload = new
            {
                report_hash = report.ReportHash,
                binary_hash = report.BinaryHash,
                signature_valid = report.SignatureValid,
                timestamp = report.Timestamp.ToString("O"),
                agent_version = report.AgentVersion,
                platform = report.Platform,
            };

            using var response = await this.SendAuthenticatedAsync(
                () => new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) },
                retryable: true,
                cancellationToken);

            if (response is null || !response.IsSuccessStatusCode)
            {
                return new IntegrityReportResult(Success: false, Verdict: null);
            }

            // Extract verdict from response body
            string? verdict = null;
            try
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("verdict", out var verdictElement))
                {
                    verdict = verdictElement.GetString();
                }
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Verdict field absent or malformed — no action per backlog
            }

            return new IntegrityReportResult(Success: true, Verdict: verdict);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return new IntegrityReportResult(Success: false, Verdict: null);
        }
    }

    /// <inheritdoc />
    [Obsolete("Pairing is owned by IBackendIdentityCoordinator through IPairingService.")]
    internal async Task<PairingHttpResult> PairAsync(PairingRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{this.baseUrl}/functions/v1/pairing";
            var payload = new
            {
                code = request.Code,
                device_name = request.DeviceName,
                device_model = request.DeviceModel,
                os_version = request.OsVersion,
                app_version = request.AppVersion,
                age_band = request.AgeBand,
            };

            var content = JsonContent.Create(payload);
            var requestMsg = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            requestMsg.Content = content;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await this.httpClient.SendAsync(requestMsg, cts.Token);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<PairingSuccessResponse>(
                    this.jsonOptions,
                    cts.Token);
                return json != null
                    ? PairingHttpResult.SuccessResult(json.DeviceId, json.ParentId, json.PolicyVersion)
                    : PairingHttpResult.ServerError("Empty response body");
            }

            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => PairingHttpResult.NotFound(),
                HttpStatusCode.Gone => PairingHttpResult.Gone(),
                HttpStatusCode.TooManyRequests => PairingHttpResult.TooManyRequests(),
                _ => PairingHttpResult.ServerError($"HTTP {(int)response.StatusCode}"),
            };
        }
        catch (TaskCanceledException)
        {
            return PairingHttpResult.NetworkError("Request timeout");
        }
        catch (HttpRequestException ex)
        {
            return PairingHttpResult.NetworkError(ex.Message);
        }
        catch (Exception ex)
        {
            return PairingHttpResult.ServerError(ex.Message);
        }
    }

    /// <summary>
    /// Response from the pairing endpoint.
    /// </summary>
    private class PairingSuccessResponse
    {
        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("parent_id")]
        public string ParentId { get; set; } = string.Empty;

        [JsonPropertyName("policy_version")]
        public int PolicyVersion { get; set; }
    }

    /// <summary>
    /// Response from policy fetch RPC.
    /// </summary>
    private class PolicyFetchResponse
    {
        public int Version { get; set; }
        public string? PolicyJson { get; set; }
    }

    /// <summary>
    /// Response from heartbeat RPC.
    /// </summary>
    private class HeartbeatResponse
    {
        public long? ServerTimeOffsetMs { get; set; }
        public bool NewPolicyAvailable { get; set; }
    }
}
