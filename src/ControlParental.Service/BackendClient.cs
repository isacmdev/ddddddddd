// <copyright file="BackendClient.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlParental.Domain;

/// <summary>
/// T14 — Implementación del cliente del backend de Supabase.
/// </summary>
public sealed class BackendClient : IBackendClient
{
    private readonly HttpClient httpClient;
    private readonly JsonSerializerOptions jsonOptions;
    private readonly string baseUrl;
    private readonly IDeviceAuthenticator deviceAuthenticator;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests.</param>
    /// <param name="baseUrl">Supabase base URL.</param>
    /// <param name="deviceAuthenticator">Authenticator for session token (T17).</param>
    public BackendClient(
        HttpClient httpClient,
        string baseUrl,
        IDeviceAuthenticator deviceAuthenticator)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
        this.deviceAuthenticator = deviceAuthenticator ?? throw new ArgumentNullException(nameof(deviceAuthenticator));
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
    }

    /// <summary>
    /// Creates a request message with the current auth token from DeviceAuthenticator.
    /// </summary>
    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        var token = this.deviceAuthenticator.CurrentAccessToken;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

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
            var content = JsonContent.Create(new { p_device_id = deviceId });
            var request = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            request.Content = content;

            var response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return PolicyFetchResult.Failed($"HTTP {response.StatusCode}: {error}");
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
        catch (HttpRequestException ex)
        {
            return PolicyFetchResult.Failed($"Network error: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            return PolicyFetchResult.Failed("Request timeout");
        }
        catch (Exception ex)
        {
            return PolicyFetchResult.Failed($"Unexpected error: {ex.Message}");
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

            var content = JsonContent.Create(payload);
            var request = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            request.Content = content;

            // Add idempotency headers
            request.Headers.Add("Prefer", "resolution=merge-duplicates");

            var response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return DataPushResult.Failed($"HTTP {response.StatusCode}: {error}");
            }

            return DataPushResult.Succeeded(logsList.Count);
        }
        catch (HttpRequestException ex)
        {
            return DataPushResult.Failed($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return DataPushResult.Failed($"Unexpected error: {ex.Message}");
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

            var content = JsonContent.Create(payload);
            var request = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            request.Content = content;

            request.Headers.Add("Prefer", "resolution=merge-duplicates");

            var response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return DataPushResult.Failed($"HTTP {response.StatusCode}: {error}");
            }

            return DataPushResult.Succeeded(alertsList.Count);
        }
        catch (HttpRequestException ex)
        {
            return DataPushResult.Failed($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return DataPushResult.Failed($"Unexpected error: {ex.Message}");
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

            var content = JsonContent.Create(payload);
            var request = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            request.Content = content;

            request.Headers.Add("Prefer", "resolution=merge-duplicates");

            var response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return DataPushResult.Failed($"HTTP {response.StatusCode}: {error}");
            }

            return DataPushResult.Succeeded(eventsList.Count);
        }
        catch (HttpRequestException ex)
        {
            return DataPushResult.Failed($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return DataPushResult.Failed($"Unexpected error: {ex.Message}");
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

            var content = JsonContent.Create(payload);
            var request = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            request.Content = content;

            var response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return HeartbeatResult.Failed($"HTTP {response.StatusCode}: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(
                this.jsonOptions,
                cancellationToken);

            return HeartbeatResult.Succeeded(
                result?.ServerTimeOffsetMs,
                result?.NewPolicyAvailable ?? false);
        }
        catch (HttpRequestException ex)
        {
            return HeartbeatResult.Failed($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return HeartbeatResult.Failed($"Unexpected error: {ex.Message}");
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

            var content = JsonContent.Create(payload);
            var request = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            request.Content = content;

            request.Headers.Add("Prefer", "resolution=merge-duplicates");

            var response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return PushTokenRegistrationResult.Failed($"HTTP {response.StatusCode}: {error}");
            }

            return PushTokenRegistrationResult.Succeeded(expiresAt);
        }
        catch (HttpRequestException ex)
        {
            return PushTokenRegistrationResult.Failed($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return PushTokenRegistrationResult.Failed($"Unexpected error: {ex.Message}");
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

            var content = JsonContent.Create(payload);
            var requestMsg = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            requestMsg.Content = content;

            var response = await this.httpClient.SendAsync(requestMsg, cancellationToken);

            return response.IsSuccessStatusCode;
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
                timestamp = report.Timestamp.ToString("O"),
                agent_version = report.AgentVersion,
                platform = report.Platform,
            };

            var content = JsonContent.Create(payload);
            var requestMsg = this.CreateAuthenticatedRequest(HttpMethod.Post, url);
            requestMsg.Content = content;

            var response = await this.httpClient.SendAsync(requestMsg, cancellationToken);

            if (!response.IsSuccessStatusCode)
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
            catch (Exception)
            {
                // Verdict field absent or malformed — no action per backlog
            }

            return new IntegrityReportResult(Success: true, Verdict: verdict);
        }
        catch (Exception)
        {
            return new IntegrityReportResult(Success: false, Verdict: null);
        }
    }

    /// <inheritdoc />
    public async Task<PairingHttpResult> PairAsync(PairingRequest request, CancellationToken cancellationToken = default)
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
