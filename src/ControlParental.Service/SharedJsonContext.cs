// <copyright file="SharedJsonContext.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Text.Json;
using System.Text.Json.Serialization;
using ControlParental.Domain;

/// <summary>
/// Shared source-generated JSON context for all service-owned serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(UsageLogEntry))]
[JsonSerializable(typeof(DeviceAlertEntry))]
[JsonSerializable(typeof(BehavioralEventEntry))]
[JsonSerializable(typeof(TimeRequestEntry))]
[JsonSerializable(typeof(HeartbeatData))]
[JsonSerializable(typeof(IntegrityReport))]
[JsonSerializable(typeof(PairingRequest))]
[JsonSerializable(typeof(PairingSuccessResponse))]
[JsonSerializable(typeof(PolicyFetchResponse))]
[JsonSerializable(typeof(HeartbeatResponse))]
[JsonSerializable(typeof(AccessTokenResponse))]
[JsonSerializable(typeof(OutboxEntry))]
[JsonSerializable(typeof(OutboxEntryStatus))]
[JsonSerializable(typeof(HttpOutcome))]
[JsonSerializable(typeof(DeviceAuthState))]
[JsonSerializable(typeof(EnforcementLevel))]
[JsonSerializable(typeof(TamperEventType))]
[JsonSerializable(typeof(TamperSeverity))]
[JsonSerializable(typeof(VerdictAction))]
[JsonSerializable(typeof(VerdictReaction))]
[JsonSerializable(typeof(EnforcementIssueType))]
[JsonSerializable(typeof(EnforcementIssueSeverity))]
[JsonSerializable(typeof(IntegrityEscalationStateEnvelope))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<UsageLogEntry>))]
[JsonSerializable(typeof(List<DeviceAlertEntry>))]
[JsonSerializable(typeof(List<BehavioralEventEntry>))]
[JsonSerializable(typeof(List<TimeRequestEntry>))]
[JsonSerializable(typeof(TimeRequestPostPayload))]
public sealed partial class SharedJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Typed request sent to the time_requests endpoint. Keeping this DTO explicit
/// prevents anonymous payloads from bypassing the wire contract at this boundary.
/// </summary>
public sealed record TimeRequestPostPayload
{
    [JsonPropertyName("request_id")] public required string RequestId { get; init; }
    [JsonPropertyName("minutes")] public required int Minutes { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
    [JsonPropertyName("created_at")] public required string CreatedAt { get; init; }
    [JsonPropertyName("scope")] public string? Scope { get; init; }
    [JsonPropertyName("origin")] public string? Origin { get; init; }
    [JsonPropertyName("policy_version")] public ulong? PolicyVersion { get; init; }
    [JsonPropertyName("device_id")] public string? DeviceId { get; init; }
}

/// <summary>
/// Public DTO used when refreshing the WNS OAuth token.
/// </summary>
public sealed class AccessTokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

/// <summary>
/// Public DTO used when parsing a successful pairing response.
/// </summary>
public sealed class PairingSuccessResponse
{
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = string.Empty;

    [JsonPropertyName("parent_id")]
    public string ParentId { get; set; } = string.Empty;

    [JsonPropertyName("policy_version")]
    public int PolicyVersion { get; set; }
}

/// <summary>
/// Public DTO used when parsing the policy fetch RPC response.
/// </summary>
public sealed class PolicyFetchResponse
{
    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("policy_json")]
    public string? PolicyJson { get; set; }
}

/// <summary>
/// Public DTO used when parsing the heartbeat RPC response.
/// </summary>
public sealed class HeartbeatResponse
{
    [JsonPropertyName("server_time_offset_ms")]
    public long? ServerTimeOffsetMs { get; set; }

    [JsonPropertyName("new_policy_available")]
    public bool NewPolicyAvailable { get; set; }
}
