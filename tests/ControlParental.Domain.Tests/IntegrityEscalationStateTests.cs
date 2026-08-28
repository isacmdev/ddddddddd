namespace ControlParental.Domain.Tests;
using ControlParental.Domain;
using Xunit;

public sealed class IntegrityEscalationStateTests
{
    [Fact]
    public void EnvelopeWithUnsupportedDocumentVersion_IsRejectedWithTypedError()
        => AssertEnvelopeError(new(2, 1, Create()), IntegrityEscalationStateError.UnsupportedDocumentVersion);

    [Fact]
    public void EnvelopeWithUnsupportedSchemaVersion_IsRejectedWithTypedError()
        => AssertEnvelopeError(new(1, 2, Create(schema: 2)), IntegrityEscalationStateError.UnsupportedSchemaVersion);

    [Fact]
    public void EnvelopeWithStateSchemaMismatch_IsRejectedWithTypedError()
        => AssertEnvelopeError(new(1, 1, Create(schema: 2)), IntegrityEscalationStateError.UnsupportedSchemaVersion);

    [Fact]
    public void EnvelopeWithNullState_IsRejectedAsCorrupt()
        => AssertEnvelopeError(new(1, 1, null!), IntegrityEscalationStateError.CorruptDocument);

    [Fact]
    public void EnvelopeWithInvalidState_DelegatesStateValidation()
        => AssertEnvelopeError(new(1, 1, Create(policy: 0)), IntegrityEscalationStateError.InvalidState);

    [Fact]
    public void DualPositiveStreaks_AreRejected()
        => AssertInvalid(Create(revoked: 1, trust: 1, phase: EscalationPhase.Normal,
            origin: null, due: null, pendingReaction: null, pendingNotification: null));

    [Fact]
    public void UndefinedPhase_IsRejected()
        => AssertInvalid(Create(phase: (EscalationPhase)99, origin: null, due: null, revoked: 0));

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(999)]
    public void AnyPositivePolicyVersion_IsAccepted(int version)
        => Create(policy: version).Validate();

    [Fact]
    public void ExactAcceptedBoundaries_AreValid()
    {
        Create(identity: new string('i', 256), policy: int.MaxValue, epoch: 0, sequence: 0,
            revoked: 0, trust: 3, phase: EscalationPhase.Normal, origin: null, due: null,
            pendingReaction: null, pendingNotification: null).Validate();
        Create(identity: new string('i', 256), epoch: IntegrityEscalationState.MaximumCounter,
            sequence: IntegrityEscalationState.MaximumCounter, revoked: 3, trust: 0,
            pendingReaction: new string('r', 256), pendingNotification: new string('n', 256)).Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositivePolicyVersion_IsRejected(int version)
        => AssertInvalid(Create(policy: version));

    [Fact]
    public void PendingTimingMustBeValid()
        => AssertInvalid(Create(timingValid: false));

    [Theory]
    [InlineData(" ")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void IdentityMustBeBoundedAndNonWhitespace(string identity)
        => AssertInvalid(Create(identity: identity));

    [Theory]
    [InlineData(-1L, 7L, 3, 0)]
    [InlineData(1_000_000_001L, 7L, 3, 0)]
    [InlineData(2L, -1L, 3, 0)]
    [InlineData(2L, 1_000_000_001L, 3, 0)]
    [InlineData(2L, 7L, -1, 0)]
    [InlineData(2L, 7L, 4, 0)]
    [InlineData(2L, 7L, 3, -1)]
    [InlineData(2L, 7L, 3, 4)]
    public void CountersAndStreaksAreBounded(long epoch, long sequence, int revoked, int trust)
        => AssertInvalid(Create(epoch, sequence, revoked, trust));

    [Fact]
    public void ClocksMustBeUtcAndNonDefault()
    {
        AssertInvalid(Create(maxWallClock: DateTimeOffset.MinValue));
        AssertInvalid(Create(maxWallClock: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1))));
    }

    [Theory]
    [InlineData("missing-origin")]
    [InlineData("missing-due")]
    [InlineData("non-utc-origin")]
    [InlineData("wrong-duration")]
    [InlineData("normal-fired")]
    [InlineData("normal-deadline")]
    [InlineData("degraded-not-fired")]
    [InlineData("degraded-deadline")]
    [InlineData("pending-fired")]
    [InlineData("non-utc-due")]
    [InlineData("invalid-schema")]
    public void PhaseTimingShapesAreExhaustive(string invalidShape)
    {
        var origin = DateTimeOffset.UnixEpoch;
        var due = origin.AddMinutes(5);
        var state = invalidShape switch
        {
            "missing-origin" => Create(origin: null, due: due, useDefaultTiming: false),
            "missing-due" => Create(origin: origin, due: null, useDefaultTiming: false),
            "non-utc-origin" => Create(origin: origin.ToOffset(TimeSpan.FromHours(1))),
            "non-utc-due" => Create(due: due.ToOffset(TimeSpan.FromHours(1))),
            "wrong-duration" => Create(due: origin.AddMinutes(6)),
            "normal-fired" => Create(phase: EscalationPhase.Normal, origin: null, due: null, revoked: 0, fired: true),
            "normal-deadline" => Create(phase: EscalationPhase.Normal, origin: origin, due: due),
            "degraded-not-fired" => Create(phase: EscalationPhase.Degraded, origin: null, due: null, revoked: 0),
            "degraded-deadline" => Create(phase: EscalationPhase.Degraded, origin: origin, due: due, fired: true),
            "pending-fired" => Create(fired: true),
            "invalid-schema" => Create(schema: 2),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidShape)),
        };
        AssertInvalid(state);
    }

    [Fact]
    public void NormalPendingAndDegradedValidShapesAreAccepted()
    {
        Create(phase: EscalationPhase.Normal, origin: null, due: null, revoked: 0).Validate();
        Create().Validate();
        Create(phase: EscalationPhase.Degraded, origin: null, due: null, revoked: 0, fired: true).Validate();
        new IntegrityEscalationStateEnvelope(1, 1, Create()).Validate();
    }

    [Theory]
    [InlineData("whitespace-pending-reaction")]
    [InlineData("empty-pending-reaction")]
    [InlineData("oversize-pending-reaction")]
    [InlineData("notification-without-reaction")]
    [InlineData("completed-without-pending-reaction")]
    [InlineData("mismatched-completed-reaction")]
    [InlineData("notification-without-completed-reaction")]
    [InlineData("mismatched-completed-notification")]
    public void EffectProgressMustBeOrderedAndMatching(string invalidShape)
    {
        var state = invalidShape switch
        {
            "whitespace-pending-reaction" => Create(pendingReaction: " "),
            "empty-pending-reaction" => Create(pendingReaction: string.Empty),
            "oversize-pending-reaction" => Create(pendingReaction: new string('x', 257)),
            "notification-without-reaction" => Create(pendingReaction: null),
            "completed-without-pending-reaction" => Create(pendingReaction: null, completedReaction: "reaction-key", pendingNotification: null),
            "mismatched-completed-reaction" => Create(completedReaction: "other"),
            "notification-without-completed-reaction" => Create(completedNotification: "notification-key"),
            "mismatched-completed-notification" => Create(completedNotification: "other"),
            _ => throw new ArgumentOutOfRangeException(nameof(invalidShape)),
        };
        AssertInvalid(state);
    }

    [Fact]
    public void MatchingReactionAndNotificationProgressIsAccepted()
        => Create(completedReaction: "reaction-key", completedNotification: "notification-key").Validate();

    [Fact]
    public void LegacyPendingEffectWithoutDescriptor_IsRejectedWithTypedError()
    {
        var error = Assert.Throws<IntegrityEscalationStateException>(() => Create(includeDescriptor: false).Validate());

        Assert.Equal(IntegrityEscalationStateError.MissingEffectDescriptor, error.Error);
    }

    [Fact]
    public void MalformedPendingEffectDescriptor_IsRejectedWithTypedError()
    {
        var malformed = Create() with
        {
            PendingEffectDescriptor = CreateDescriptor() with { Reason = string.Empty },
        };

        var error = Assert.Throws<IntegrityEscalationStateException>(() => malformed.Validate());

        Assert.Equal(IntegrityEscalationStateError.InvalidEffectDescriptor, error.Error);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(999, false)]
    [InlineData((int)EnforcementIssueSeverity.Info, false)]
    [InlineData((int)EnforcementIssueSeverity.Warning, true)]
    [InlineData((int)EnforcementIssueSeverity.Severe, true)]
    public void AddIssueSeverityMustBeDefinedAndAtLeastWarning(int severity, bool valid)
    {
        var descriptor = CreateDescriptor() with { Severity = (EnforcementIssueSeverity)severity };
        var act = () => descriptor.Validate();

        if (valid)
        {
            act();
            return;
        }

        var error = Assert.Throws<IntegrityEscalationStateException>(act);
        Assert.Equal(IntegrityEscalationStateError.InvalidEffectDescriptor, error.Error);
    }

    [Fact]
    public void ResolveIssueStillRequiresNullSeverity()
    {
        var descriptor = CreateDescriptor() with
        {
            ReactionKind = IntegrityEscalationReactionKind.ResolveIssue,
            Severity = EnforcementIssueSeverity.Warning,
        };

        var error = Assert.Throws<IntegrityEscalationStateException>(() => descriptor.Validate());

        Assert.Equal(IntegrityEscalationStateError.InvalidEffectDescriptor, error.Error);
    }

    [Fact]
    public void ReactionOnlyProgressShapesAreAccepted()
    {
        Create(pendingNotification: null).Validate();
        Create(pendingNotification: null, completedReaction: "reaction-key").Validate();
        Create(phase: EscalationPhase.Normal, origin: null, due: null, revoked: 0, pendingReaction: null, pendingNotification: null).Validate();
    }

    [Fact]
    public void PendingNotificationIdMustBeBoundedAndNonWhitespace()
    {
        AssertInvalid(Create(pendingNotification: string.Empty));
        AssertInvalid(Create(pendingNotification: " "));
        AssertInvalid(Create(pendingNotification: new string('x', 257)));
    }

    private static IntegrityEscalationState Create(
        long epoch = 2, long sequence = 7, int revoked = 3, int trust = 0,
        string identity = "identity", int policy = 1, int schema = 1, EscalationPhase phase = EscalationPhase.Pending,
        DateTimeOffset? origin = null, DateTimeOffset? due = null, DateTimeOffset? maxWallClock = null,
        bool timingValid = true, bool fired = false, string? pendingReaction = "reaction-key",
        string? completedReaction = null, string? pendingNotification = "notification-key",
        string? completedNotification = null, bool useDefaultTiming = true, bool includeDescriptor = true)
    {
        if (phase == EscalationPhase.Pending && useDefaultTiming)
        {
            origin ??= DateTimeOffset.UnixEpoch;
            due ??= DateTimeOffset.UnixEpoch.AddMinutes(5);
        }
        var descriptor = includeDescriptor && pendingReaction is not null
            ? CreateDescriptor(pendingNotification is not null)
            : null;
        return new(identity, policy, schema, epoch, sequence, revoked, trust, phase, origin, due,
            maxWallClock ?? DateTimeOffset.UnixEpoch, timingValid, fired, false, pendingReaction,
            completedReaction, pendingNotification, completedNotification)
        {
            PendingEffectDescriptor = descriptor,
        };
    }

    private static IntegrityEscalationEffectDescriptor CreateDescriptor(bool withNotification = true)
        => new(
            IntegrityEscalationEffectDescriptor.CurrentVersion,
            IntegrityEscalationReactionKind.AddIssue,
            EnforcementIssueSeverity.Warning,
            "test",
            withNotification ? "integrity" : null,
            withNotification ? "title" : null,
            withNotification ? "body" : null,
            withNotification ? DateTimeOffset.UnixEpoch : null);

    private static void AssertInvalid(IntegrityEscalationState state)
        => Assert.Throws<IntegrityEscalationStateException>(() => state.Validate());

    private static void AssertEnvelopeError(IntegrityEscalationStateEnvelope envelope, IntegrityEscalationStateError error)
        => Assert.Equal(error, Assert.Throws<IntegrityEscalationStateException>(() => envelope.Validate()).Error);
}
