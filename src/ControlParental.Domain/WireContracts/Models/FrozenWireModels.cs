namespace ControlParental.Domain.WireContracts.Models;

using System.Text.Json;
using System.Text.Json.Serialization;
using ControlParental.Domain.WireContracts;

public sealed record SetProtectedAccountRequestWire
{
    [JsonPropertyName("operation_id")] public required Guid OperationId { get; init; }
    [JsonPropertyName("username")] public required string Username { get; init; }
    [JsonPropertyName("sid")] public required string Sid { get; init; }
    [JsonPropertyName("requested_at")] public required string RequestedAt { get; init; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}
public sealed record SetProtectedAccountResponseWire
{
    [JsonPropertyName("operation_id")] public required Guid OperationId { get; init; }
    [JsonPropertyName("status")] public required string Status { get; init; }
    [JsonPropertyName("activation_state")] public required string ActivationState { get; init; }
    [JsonPropertyName("correlation_id")] public required Guid CorrelationId { get; init; }
    [JsonPropertyName("reason_code")] public required string ReasonCode { get; init; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}
public sealed record RuntimeActivationStateWire
{
    [JsonPropertyName("activation_state")] public required string ActivationState { get; init; }
    [JsonPropertyName("reason_code")] public required string ReasonCode { get; init; }
    [JsonPropertyName("observed_at")] public required string ObservedAt { get; init; }
    [JsonPropertyName("state_version")] public required ulong StateVersion { get; init; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}
public sealed record TimeRequestOutboxWire
{
    [JsonPropertyName("request_id")] public required Guid RequestId { get; init; }
    [JsonPropertyName("device_generation")] public required Guid DeviceGeneration { get; init; }
    [JsonPropertyName("wire_request")] public required CreateTimeRequestWire WireRequest { get; init; }
    [JsonPropertyName("outbox_state")] public required string OutboxState { get; init; }
    [JsonPropertyName("attempt_count")] public required uint AttemptCount { get; init; }
    [JsonPropertyName("next_attempt_at")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? NextAttemptAt { get; init; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}
public sealed record IntegrityEvidenceWire
{
    [JsonPropertyName("evidence_id")] public required Guid EvidenceId { get; init; }
    [JsonPropertyName("device_generation")] public required Guid DeviceGeneration { get; init; }
    [JsonPropertyName("agent_version")] public required string AgentVersion { get; init; }
    [JsonPropertyName("binary_sha256")] public required string BinarySha256 { get; init; }
    [JsonPropertyName("signature_result")] public required string SignatureResult { get; init; }
    [JsonPropertyName("signer_summary")] public required string SignerSummary { get; init; }
    [JsonPropertyName("collected_at")] public required string CollectedAt { get; init; }
    [JsonPropertyName("evidence_schema_version")] public required uint EvidenceSchemaVersion { get; init; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}
public sealed record IntegrityVerdictWire
{
    [JsonPropertyName("verdict")] public required string Verdict { get; init; }
    [JsonPropertyName("evidence_id")] public required Guid EvidenceId { get; init; }
    [JsonPropertyName("evaluated_at")] public required string EvaluatedAt { get; init; }
    [JsonPropertyName("verdict_version")] public required ulong VerdictVersion { get; init; }
    [JsonPropertyName("reason_code")] public required string ReasonCode { get; init; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}
public sealed record HintWire
{
    [JsonPropertyName("hint_type")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? HintType { get; init; }
    [JsonPropertyName("correlation_hint")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? CorrelationHint { get; init; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}
public sealed record FrozenScheduleWire
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("days")] public required IReadOnlyList<string> Days { get; init; }
    [JsonPropertyName("from")] public required string From { get; init; }
    [JsonPropertyName("to")] public required string To { get; init; }
    [JsonPropertyName("action")] public required string Action { get; init; }
    [JsonPropertyName("allow_list")] public IReadOnlyList<string> AllowList { get; init; } = [];
}
public sealed record FrozenCategoryLimitWire
{
    [JsonPropertyName("category")] public required string Category { get; init; }
    [JsonPropertyName("minutes")] public required int Minutes { get; init; }
}
public sealed record FrozenWindowWire
{
    [JsonPropertyName("days")] public required IReadOnlyList<string> Days { get; init; }
    [JsonPropertyName("from")] public required string From { get; init; }
    [JsonPropertyName("to")] public required string To { get; init; }
}
public sealed record FrozenAppPolicyWire
{
    [JsonPropertyName("package_name")] public required string PackageName { get; init; }
    [JsonPropertyName("state")] public required string State { get; init; }
    [JsonPropertyName("daily_limit_minutes")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? DailyLimitMinutes { get; init; }
    [JsonPropertyName("category")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Category { get; init; }
    [JsonPropertyName("allowed_windows")] public IReadOnlyList<FrozenWindowWire> AllowedWindows { get; init; } = [];
}
public sealed record FrozenGrantWire
{
    [JsonPropertyName("id")] public required string Id { get; init; }
    [JsonPropertyName("request_id")] public required string RequestId { get; init; }
    [JsonPropertyName("scope")] public required string Scope { get; init; }
    [JsonPropertyName("minutes")] public required int Minutes { get; init; }
    [JsonPropertyName("granted_at")] public required string GrantedAt { get; init; }
    [JsonPropertyName("expires_at")] public required string ExpiresAt { get; init; }
    [JsonPropertyName("source")] public required string Source { get; init; }
}

public sealed record PolicySnapshotWire
{
    [JsonPropertyName("device_id")] public required Guid DeviceId { get; init; }
    [JsonPropertyName("version")] public required ulong Version { get; init; }
    [JsonPropertyName("device_state")] public required string DeviceState { get; init; }
    [JsonPropertyName("daily_screen_time_minutes")] public required int DailyScreenTimeMinutes { get; init; }
    [JsonPropertyName("schedules")] public required IReadOnlyList<FrozenScheduleWire> Schedules { get; init; }
    [JsonPropertyName("category_limits")] public required IReadOnlyList<FrozenCategoryLimitWire> CategoryLimits { get; init; }
    [JsonPropertyName("app_policies")] public required IReadOnlyList<FrozenAppPolicyWire> AppPolicies { get; init; }
    [JsonPropertyName("category_assignments")] public required IReadOnlyDictionary<string, string> CategoryAssignments { get; init; }
    [JsonPropertyName("grants")] public required IReadOnlyList<FrozenGrantWire> Grants { get; init; }
    [JsonPropertyName("snapshot_hash")] public required string SnapshotHash { get; init; }
    [JsonPropertyName("extensions")][JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public IReadOnlyDictionary<string, JsonElement>? Extensions { get; init; }
}

public sealed record RuntimeActivationResultWire
{
    [JsonPropertyName("activation_state")] public required string ActivationState { get; init; }
    [JsonPropertyName("reason_code")] public required string ReasonCode { get; init; }
    [JsonPropertyName("observed_at")] public required string ObservedAt { get; init; }
    [JsonPropertyName("state_version")] public required ulong StateVersion { get; init; }
}
