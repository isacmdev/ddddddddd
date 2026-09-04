namespace ControlParental.Domain;
public enum AgentCommandKind { ShowOverlay, ReplaceOverlay, ClearOverlay, TerminateProcess, LockWorkstation }
public enum ActionStatus
{
    Confirmed, HarmlessAbsence, TimedOut, ConnectionReplaced, Stale,
    InvalidTarget, InvalidState, NativeFailure, AccessDenied,
}
public sealed record AgentCommandEnvelope(
    Guid CommandId, int SessionId, long ConnectionGeneration, long IntentVersion,
    AgentCommandKind Command, DateTimeOffset Deadline, OverlayIntent? Overlay = null,
    ObservedProcessTarget? Target = null);
public sealed record AgentActionResult(
    Guid CommandId, int SessionId, long ConnectionGeneration, long IntentVersion,
    ActionStatus Status, int? NativeError);
public sealed record ObservedProcessTarget(int ProcessId, int SessionId, DateTimeOffset StartedAt);
public sealed record OverlayIntent(bool Desired, string Reason, string? CtaLabel, long Version);
public sealed record NativeActionOutcome(ActionStatus Status, int? NativeError);

public interface IOverlayIntentStore
{
    OverlayIntent? Load();
    void Save(OverlayIntent intent);
}

public interface IAgentCommandPort
{
    long Generation { get; }

    ValueTask<AgentActionResult> ExecuteAsync(
        AgentCommandEnvelope command, CancellationToken cancellationToken = default);
}
