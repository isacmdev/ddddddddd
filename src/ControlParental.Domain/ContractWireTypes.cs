namespace ControlParental.Domain;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Versioned wire envelope shared by local and backend contracts.</summary>
public sealed record ContractEnvelope
{
    [JsonPropertyName("contract")] public string Contract { get; init; } = string.Empty;
    [JsonPropertyName("version")] public int Version { get; init; }
    [JsonPropertyName("message_type")] public string MessageType { get; init; } = string.Empty;
    [JsonPropertyName("correlation_id")] public Guid CorrelationId { get; init; }
    [JsonPropertyName("payload")] public JsonElement Payload { get; init; }
    [JsonPropertyName("extensions")] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

public sealed record SetProtectedAccountRequest(Guid OperationId, string Username, string Sid, DateTimeOffset RequestedAt);
public sealed record SetProtectedAccountResponse(Guid OperationId, string Status, string ActivationState, Guid CorrelationId, string ReasonCode);
public sealed record RuntimeActivationState(string ActivationState, string ReasonCode, DateTimeOffset ObservedAt, ulong StateVersion);
public sealed record CreateTimeRequest(Guid RequestId, string Scope, int Minutes, string Origin, ulong PolicyVersion, Guid DeviceId, DateTimeOffset CreatedAt, string? Reason = null);
public sealed record TimeRequestOutbox(Guid RequestId, Guid DeviceGeneration, CreateTimeRequest WireRequest, string OutboxState, uint AttemptCount, DateTimeOffset? NextAttemptAt);
public sealed record IntegrityEvidence(Guid EvidenceId, Guid DeviceGeneration, string AgentVersion, string BinarySha256, string SignatureResult, string SignerSummary, DateTimeOffset CollectedAt, uint EvidenceSchemaVersion);
public sealed record IntegrityVerdict(string Verdict, Guid EvidenceId, DateTimeOffset EvaluatedAt, ulong VerdictVersion, string ReasonCode);

/// <summary>Signal-only WNS/Realtime hint. It carries no authority.</summary>
public sealed record ContractHint
{
    [JsonPropertyName("hint_type")] public string? HintType { get; init; }
    [JsonPropertyName("correlation_hint")] public Guid? CorrelationHint { get; init; }
}