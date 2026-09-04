namespace ControlParental.Domain;
public enum EscalationPhase
{
    Normal,
    Pending,
    Degraded,
}
public enum IntegrityEscalationStateError
{
    InvalidState,
    MissingEffectDescriptor,
    InvalidEffectDescriptor,
    UnsupportedDocumentVersion,
    UnsupportedSchemaVersion,
    WrongIdentity,
    CorruptDocument,
    StoreUnavailable,
    StaleState,
}
public sealed class IntegrityEscalationStateException : Exception
{
    public IntegrityEscalationStateException(IntegrityEscalationStateError error, string message, Exception? inner = null)
        : base(message, inner) => this.Error = error;

    public IntegrityEscalationStateError Error { get; }
}

public enum IntegrityEscalationReactionKind
{
    AddIssue,
    ResolveIssue,
}

public sealed record IntegrityEscalationEffectDescriptor(
    int Version,
    IntegrityEscalationReactionKind ReactionKind,
    EnforcementIssueSeverity? Severity,
    string Reason,
    string? NotificationType,
    string? NotificationTitle,
    string? NotificationBody,
    DateTimeOffset? NotificationTimestamp)
{
    public const int CurrentVersion = 1;
    public const int MaximumReasonLength = 1024;
    public const int MaximumNotificationFieldLength = 4096;

    public void Validate()
    {
        if (this.Version != CurrentVersion || !Enum.IsDefined(this.ReactionKind) ||
            string.IsNullOrWhiteSpace(this.Reason) || this.Reason.Length > MaximumReasonLength ||
            this.ReactionKind == IntegrityEscalationReactionKind.AddIssue &&
            (this.Severity is null || !Enum.IsDefined(this.Severity.Value) || this.Severity.Value < EnforcementIssueSeverity.Warning) ||
            this.ReactionKind == IntegrityEscalationReactionKind.ResolveIssue && this.Severity is not null)
        {
            throw InvalidDescriptor();
        }

        var notificationFields = new[] { this.NotificationType, this.NotificationTitle, this.NotificationBody };
        var hasNotification = notificationFields.Any(value => value is not null) || this.NotificationTimestamp is not null;
        if (hasNotification &&
            (notificationFields.Any(string.IsNullOrWhiteSpace) ||
             notificationFields.Any(value => value!.Length > MaximumNotificationFieldLength) ||
             this.NotificationTimestamp is null || !IsUtc(this.NotificationTimestamp.Value)))
        {
            throw InvalidDescriptor();
        }
    }

    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero && value != default;
    private static IntegrityEscalationStateException InvalidDescriptor() =>
        new(IntegrityEscalationStateError.InvalidEffectDescriptor, "The durable escalation effect descriptor is invalid.");
}

public sealed record IntegrityEscalationState(
    string IdentityScope,
    int PolicyVersion,
    int SchemaVersion,
    long Epoch,
    long Sequence,
    int RevokedStreak,
    int TrustStreak,
    EscalationPhase Phase,
    DateTimeOffset? DeadlineOriginUtc,
    DateTimeOffset? DeadlineDueUtc,
    DateTimeOffset MaxWallClockSeenUtc,
    bool TimingValid,
    bool FiredLatch,
    bool RecoveryLatch,
    string? PendingReactionId,
    string? CompletedReactionId,
    string? PendingNotificationId,
    string? CompletedNotificationId)
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumIdentityLength = 256;
    public const int MaximumEffectKeyLength = 256;
    public const long MaximumCounter = 1_000_000_000;
    public const int DefinitiveRevokedThreshold = 3;
    public const int DefinitiveTrustRecoveryThreshold = 3;
    public static readonly TimeSpan EscalationDeadlineDelay = TimeSpan.FromMinutes(5);
    public IntegrityEscalationEffectDescriptor? PendingEffectDescriptor { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.IdentityScope) || this.IdentityScope.Length > MaximumIdentityLength ||
            this.PolicyVersion <= 0 || this.SchemaVersion != CurrentSchemaVersion || this.Epoch < 0 ||
            this.Sequence < 0 || this.Epoch > MaximumCounter || this.Sequence > MaximumCounter ||
            this.RevokedStreak is < 0 or > DefinitiveRevokedThreshold ||
            this.TrustStreak is < 0 or > DefinitiveTrustRecoveryThreshold ||
            (this.RevokedStreak > 0 && this.TrustStreak > 0) ||
            !Enum.IsDefined(this.Phase) ||
            !IsUtc(this.MaxWallClockSeenUtc) || !ValidKey(this.PendingReactionId) || !ValidKey(this.CompletedReactionId) ||
            !ValidKey(this.PendingNotificationId) || !ValidKey(this.CompletedNotificationId))
        {
            throw InvalidState();
        }
        if (this.Phase == EscalationPhase.Pending)
        {
            if (this.DeadlineOriginUtc is null || this.DeadlineDueUtc is null ||
                this.DeadlineDueUtc.Value - this.DeadlineOriginUtc.Value != EscalationDeadlineDelay ||
                this.RevokedStreak != DefinitiveRevokedThreshold || !this.TimingValid || this.FiredLatch || !IsUtc(this.DeadlineOriginUtc.Value) ||
                !IsUtc(this.DeadlineDueUtc.Value))
            {
                throw InvalidState();
            }
        }
        else if (this.DeadlineOriginUtc is not null || this.DeadlineDueUtc is not null)
        {
            throw InvalidState();
        }
        if ((this.Phase == EscalationPhase.Normal && this.FiredLatch) ||
            (this.Phase == EscalationPhase.Degraded && !this.FiredLatch) ||
            (this.CompletedReactionId is not null && this.CompletedReactionId != this.PendingReactionId) ||
            (this.PendingNotificationId is not null && this.PendingReactionId is null) ||
            (this.CompletedNotificationId is not null &&
            (this.CompletedNotificationId != this.PendingNotificationId || this.CompletedReactionId is null))
           )
        {
            throw InvalidState();
        }

        var reactionPending = this.PendingReactionId is not null && this.PendingReactionId != this.CompletedReactionId;
        var notificationPending = this.PendingNotificationId is not null && this.PendingNotificationId != this.CompletedNotificationId;
        if ((reactionPending || notificationPending) && this.PendingEffectDescriptor is null)
        {
            throw new IntegrityEscalationStateException(
                IntegrityEscalationStateError.MissingEffectDescriptor,
                "A pending escalation effect is missing its durable descriptor.");
        }
        if (this.PendingEffectDescriptor is not null)
        {
            this.PendingEffectDescriptor.Validate();
            if (this.PendingReactionId is null ||
                notificationPending && this.PendingEffectDescriptor.NotificationType is null ||
                !notificationPending && this.PendingEffectDescriptor.NotificationType is not null && this.PendingNotificationId is null)
            {
                throw InvalidState();
            }
        }
    }

    private static bool ValidKey(string? value) => value is null ||
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumEffectKeyLength;
    private static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero && value != default;
    private static IntegrityEscalationStateException InvalidState() =>
        new(IntegrityEscalationStateError.InvalidState, "The escalation state is invalid.");
}
public sealed record IntegrityEscalationStateEnvelope(
    int DocumentVersion,
    int SchemaVersion,
    IntegrityEscalationState State)
{
    public const int CurrentDocumentVersion = 1;

    public void Validate()
    {
        if (this.DocumentVersion != CurrentDocumentVersion)
        {
            throw new IntegrityEscalationStateException(IntegrityEscalationStateError.UnsupportedDocumentVersion,
                "The escalation document version is unsupported.");
        }
        if (this.SchemaVersion != IntegrityEscalationState.CurrentSchemaVersion ||
            this.State is not null && this.State.SchemaVersion != this.SchemaVersion)
        {
            throw new IntegrityEscalationStateException(IntegrityEscalationStateError.UnsupportedSchemaVersion,
                "The escalation schema version is unsupported or mismatched.");
        }
        if (this.State is null)
        {
            throw new IntegrityEscalationStateException(IntegrityEscalationStateError.CorruptDocument,
                "The escalation document state is missing.");
        }
        this.State.Validate();
    }
}
public interface IIntegrityEscalationStateStore
{
    Task<IntegrityEscalationState?> LoadAsync(string identity, CancellationToken cancellationToken = default);

    Task SaveAsync(IntegrityEscalationStateEnvelope value, CancellationToken cancellationToken = default);
}
