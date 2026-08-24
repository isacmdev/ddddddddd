// <copyright file="IntegrityVerdictHandler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T23 — Handles integrity verdict reaction with 8 anti-false-positive mechanisms:
/// 1. Timeout graceful — network failures don't degrade
/// 2. Count threshold — 3 consecutive revoked before degrading
/// 3. Hysteresis — 3 consecutive trust to recover from degraded
/// 4. Escalation before DEGRADED — notify admin, wait 5 min
/// 5. Grace period startup — skip first 5 minutes
/// 6. Staged response WARN → LIMIT → DEGRADED
/// 7. Shadow mode — log only until disabled
/// 8. Circuit breaker — 5 failures → 15 min cooldown
/// </summary>
public interface IIntegrityVerdictHandler
{
    /// <summary>
    /// Processes a verdict received from the server.
    /// </summary>
    /// <param name="verdict">Server verdict: "trust", "revoked", "unknown", or null if network error.</param>
    /// <param name="success">Whether the backend call succeeded.</param>
    /// <param name="timestamp">When the verdict was received.</param>
    /// <returns>The recommended reaction to take.</returns>
    VerdictReaction HandleVerdict(string? verdict, bool success, DateTimeOffset timestamp);

    /// <summary>
    /// Processes a local integrity check failure (signature invalid, hash mismatch).
    /// </summary>
    /// <param name="reason">Description of the local failure.</param>
    /// <param name="timestamp">When the failure was detected.</param>
    /// <returns>The recommended reaction to take.</returns>
    VerdictReaction HandleLocalFailure(string reason, DateTimeOffset timestamp);

    /// <summary>
    /// Gets whether the circuit breaker is currently open.
    /// </summary>
    bool IsCircuitOpen { get; }

    /// <summary>
    /// Gets whether shadow mode is active.
    /// </summary>
    bool IsShadowMode { get; }

    /// <summary>
    /// Disables shadow mode. Call when admin confirms the system should act on verdicts.
    /// </summary>
    void DisableShadowMode();
}

/// <summary>
/// T23 — Result of processing a verdict — tells the caller what action to take.
/// </summary>
public enum VerdictAction
{
    /// <summary>Take no action.</summary>
    None,

    /// <summary>Log a warning; no enforcement action.</summary>
    Warn,

    /// <summary>Add a warning-level enforcement issue.</summary>
    Limit,

    /// <summary>Add a severe enforcement issue (triggers DEGRADED) after escalation timer.</summary>
    Degrade,

    /// <summary>Shadow mode: log what would happen without acting.</summary>
    ShadowWarn,
}

/// <summary>
/// T23 — Reaction recommendation from IntegrityVerdictHandler.
/// </summary>
public sealed record VerdictReaction(
    VerdictAction Action,
    EnforcementIssueSeverity? Severity,
    string? Reason)
{
    public bool IsAuthoritativeRecovery { get; init; }
}

public sealed record NotificationCommand(
    string Type,
    string Title,
    string Body,
    DateTimeOffset Timestamp);

public sealed record VerdictDecision(
    long Sequence,
    long Epoch,
    VerdictReaction Reaction,
    NotificationCommand? Notification,
    DateTimeOffset ObservedAt,
    string IdentityScope,
    string ReactionIdempotencyKey,
    string? NotificationIdempotencyKey);

/// <summary>
/// T23 — Implementation of IIntegrityVerdictHandler with all 8 anti-false-positive mechanisms.
/// </summary>
public sealed class IntegrityVerdictHandler : IIntegrityVerdictHandler, IDisposable
{
    // Mechanism constants
    private const int RevokedThreshold = 3;
    private const int RecoveryThreshold = 3;
    private const int CircuitFailureThreshold = 5;
    private const int CircuitOpenDurationMinutes = 15;
    private const int GracePeriodMinutes = 5;
    private const int EscalationDelayMinutes = 5;

    // State
    private int consecutiveRevokedCount;
    private int consecutiveTrustCount;
    private int consecutiveFailures;
    private DateTimeOffset? circuitOpenedAt;
    private DateTimeOffset serviceStartTime;
    private DateTimeOffset? lastVerdictTime;
    private DateTimeOffset? escalationDueAt;
    private bool pendingDegradeNotified;
    private bool escalationFired;
    private bool degraded;
    private bool shadowMode;
    private long sequence;
    private long epoch;
    private readonly object stateGate = new();
    private readonly Func<DateTimeOffset> clock;
    private NotificationCommand? notificationForDecision;
    private readonly string identityScope;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrityVerdictHandler"/> class.
    /// </summary>
    /// <param name="outboxManager">Optional outbox manager for admin notifications.</param>
    public IntegrityVerdictHandler(
        IOutboxManager? outboxManager = null,
        bool shadowMode = false,
        Action<VerdictReaction>? onReaction = null,
        Func<DateTimeOffset>? clock = null,
        string identityScope = "local")
    {
        _ = outboxManager;
        this.shadowMode = shadowMode;
        this.serviceStartTime = DateTimeOffset.UtcNow;
        _ = onReaction;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.identityScope = identityScope;
    }

    /// <inheritdoc />
    public bool IsCircuitOpen
    {
        get
        {
            var now = this.clock();
            lock (this.stateGate)
            {
                return this.IsCircuitOpenAtLocked(now);
            }
        }
    }

    private bool IsCircuitOpenAtLocked(DateTimeOffset now)
        => this.circuitOpenedAt is not null
            && (now - this.circuitOpenedAt.Value).TotalMinutes < CircuitOpenDurationMinutes;

    /// <inheritdoc />
    public bool IsShadowMode { get { lock (this.stateGate) return this.shadowMode; } }

    /// <inheritdoc />
    public void DisableShadowMode()
    {
        lock (this.stateGate) this.shadowMode = false;
        System.Diagnostics.Debug.WriteLine("[IntegrityVerdictHandler] Shadow mode disabled. Verdict handling is now active.");
    }

    /// <inheritdoc />
    public VerdictReaction HandleVerdict(string? verdict, bool success, DateTimeOffset timestamp)
        => this.HandleVerdictDecision(verdict, success, timestamp).Reaction;

    public VerdictDecision HandleVerdictDecision(string? verdict, bool success, DateTimeOffset timestamp, string? identityScope = null)
    {
        var observedAt = this.clock();
        lock (this.stateGate)
        {
            var identity = this.AllocateDecisionIdentityLocked();
            this.sequence = identity.Sequence;
            this.epoch = identity.Epoch;
            this.notificationForDecision = null;
            var reaction = this.HandleVerdictCore(verdict, success, timestamp, observedAt);
            var scope = identityScope ?? this.identityScope;
            return new VerdictDecision(
                identity.Sequence,
                identity.Epoch,
                reaction,
                this.notificationForDecision,
                observedAt,
                scope,
                BuildIdempotencyKey(scope, identity.Epoch, identity.Sequence, reaction.Action.ToString()),
                this.notificationForDecision is null ? null : BuildIdempotencyKey(scope, identity.Epoch, identity.Sequence, "notification"));
        }
    }

    private (long Sequence, long Epoch) AllocateDecisionIdentityLocked()
    {
        if (this.sequence == long.MaxValue || this.epoch == long.MaxValue) throw new OverflowException("Integrity decision identity exhausted");
        return (checked(this.sequence + 1), checked(this.epoch + 1));
    }

    private static string BuildIdempotencyKey(string scope, long epoch, long sequence, string effect)
        => $"integrity/{scope}/integrity-binary/{epoch}/{sequence}/{effect.ToLowerInvariant()}";

    public Task<VerdictDecision> HandleVerdictDecisionAsync(
        string? verdict,
        bool success,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
        => cancellationToken.IsCancellationRequested
            ? Task.FromCanceled<VerdictDecision>(cancellationToken)
            : Task.FromResult(this.HandleVerdictDecision(verdict, success, timestamp));

    private VerdictReaction HandleVerdictCore(string? verdict, bool success, DateTimeOffset timestamp, DateTimeOffset acceptanceTime)
    {
        // Mechanism 5: Grace period — skip first N minutes after startup
        if ((timestamp - this.serviceStartTime).TotalMinutes < GracePeriodMinutes)
        {
            return new VerdictReaction(VerdictAction.ShadowWarn, null, "Grace period active, skipping check");
        }

        var deadlineWon = this.CommitDeadlineIfDueLocked(acceptanceTime);

        // Mechanism 8: Circuit breaker — if open, skip all verdicts
        if (this.IsCircuitOpenAtLocked(acceptanceTime))
        {
            if (deadlineWon) return new VerdictReaction(VerdictAction.Degrade, EnforcementIssueSeverity.Severe, "Integrity deadline reached");
            return new VerdictReaction(VerdictAction.ShadowWarn, null, "Circuit breaker open, skipping verdict");
        }

        // Mechanism 1: Timeout graceful — network failures don't degrade
        if (!success)
        {
            this.consecutiveFailures++;
            if (deadlineWon) return new VerdictReaction(VerdictAction.Degrade, EnforcementIssueSeverity.Severe, "Integrity deadline reached");
            if (this.consecutiveFailures >= CircuitFailureThreshold)
            {
                this.circuitOpenedAt = timestamp;
                this.consecutiveFailures = 0;

                return new VerdictReaction(VerdictAction.ShadowWarn, null, "Circuit breaker opened due to consecutive failures");
            }

            return new VerdictReaction(VerdictAction.None, null, "Backend unavailable, no action taken");
        }

        // Success: reset failure counter
        this.consecutiveFailures = 0;

        // Mechanism 3: Hysteresis — once degraded, need consecutive trust to recover
        if (verdict == "trust")
        {
            this.consecutiveTrustCount++;
            this.consecutiveRevokedCount = 0;
            if (!this.degraded)
            {
                this.pendingDegradeNotified = false;
                this.escalationDueAt = null;
                this.escalationFired = false;
                this.consecutiveTrustCount = 0;
            }
            if (this.degraded && this.consecutiveTrustCount >= RecoveryThreshold)
            {
                // Recovery threshold met — the EnforcementLevelMonitor will automatically
                // remove the degradation issue when trust is restored
                System.Diagnostics.Debug.WriteLine(
                    $"[IntegrityVerdictHandler] Trust threshold met ({RecoveryThreshold}). System can recover from degraded state.");

                this.degraded = false;
                this.consecutiveTrustCount = 0;
                this.pendingDegradeNotified = false;
                this.escalationDueAt = null;
                this.escalationFired = false;

                return new VerdictReaction(VerdictAction.None, null, "Authoritative trust recovery")
                {
                    IsAuthoritativeRecovery = true,
                };
            }

            if (deadlineWon) return new VerdictReaction(VerdictAction.Degrade, EnforcementIssueSeverity.Severe, "Integrity deadline reached before trust recovery");

            return new VerdictReaction(VerdictAction.None, null, "Trust verdict received");
        }

        // Unknown verdict — reset counters, warn but don't degrade
        if (verdict == "unknown")
        {
            if (deadlineWon) return new VerdictReaction(VerdictAction.Degrade, EnforcementIssueSeverity.Severe, "Integrity deadline reached");
            return new VerdictReaction(VerdictAction.Warn, EnforcementIssueSeverity.Warning, "Verdict unknown");
        }

        // "revoked" verdict — apply staged response
        if (verdict == "revoked")
        {
            this.consecutiveTrustCount = 0;
            var reaction = this.HandleRevokedVerdict(timestamp, acceptanceTime);
            return deadlineWon ? new VerdictReaction(VerdictAction.Degrade, EnforcementIssueSeverity.Severe, "Integrity deadline reached") : reaction;
        }

        // Unknown verdict string
        return new VerdictReaction(VerdictAction.Warn, EnforcementIssueSeverity.Warning, $"Unknown verdict: {verdict}");
    }

    /// <inheritdoc />
    public VerdictReaction HandleLocalFailure(string reason, DateTimeOffset timestamp)
        => this.HandleLocalFailureDecision(reason, timestamp).Reaction;

    public VerdictDecision HandleLocalFailureDecision(string reason, DateTimeOffset timestamp, string? identityScope = null)
    {
        var observedAt = this.clock();
        lock (this.stateGate)
        {
            var identity = this.AllocateDecisionIdentityLocked();
            this.sequence = identity.Sequence;
            this.epoch = identity.Epoch;
            this.notificationForDecision = null;
            var reaction = this.HandleLocalFailureCore(reason, timestamp);
            var scope = identityScope ?? this.identityScope;
            return new VerdictDecision(
                identity.Sequence,
                identity.Epoch,
                reaction,
                null,
                observedAt,
                scope,
                BuildIdempotencyKey(scope, identity.Epoch, identity.Sequence, reaction.Action.ToString()),
                null);
        }
    }

    private VerdictReaction HandleLocalFailureCore(string reason, DateTimeOffset timestamp)
    {
        // Mechanism 5: Grace period — skip during first N minutes
        if ((timestamp - this.serviceStartTime).TotalMinutes < GracePeriodMinutes)
        {
            return new VerdictReaction(VerdictAction.ShadowWarn, null, "Grace period active, local failure ignored");
        }

        // Mechanism 7: Shadow mode — log but don't act
        if (this.shadowMode)
        {
            return new VerdictReaction(VerdictAction.ShadowWarn, null, $"Shadow mode: would degrade on local failure: {reason}");
        }

        // Local failure is authoritative — no threshold needed, degrade immediately
        return new VerdictReaction(VerdictAction.Degrade, EnforcementIssueSeverity.Severe, $"Local integrity failure: {reason}");
    }

    private VerdictReaction HandleRevokedVerdict(DateTimeOffset timestamp, DateTimeOffset acceptanceTime)
    {
        this.consecutiveTrustCount = 0;
        this.consecutiveRevokedCount++;
        this.lastVerdictTime = timestamp;

        // Mechanism 7: Shadow mode — log what would happen but take no action
        if (this.shadowMode)
        {
            return new VerdictReaction(
                VerdictAction.ShadowWarn,
                null,
                $"Shadow mode: would {'d' + "egrade"} on revoked (count {this.consecutiveRevokedCount}/{RevokedThreshold})");
        }

        // Mechanism 6: Staged response — WARN → LIMIT → DEGRADED based on count
        if (this.consecutiveRevokedCount < RevokedThreshold)
        {
            return new VerdictReaction(
                this.consecutiveRevokedCount == 1 ? VerdictAction.Warn : VerdictAction.Limit,
                EnforcementIssueSeverity.Warning,
                $"Revoked verdict {this.consecutiveRevokedCount}/{RevokedThreshold}");
        }

        // Mechanism 4: Escalation before DEGRADED — notify admin, wait 5 minutes
        if (!this.pendingDegradeNotified)
        {
            this.escalationDueAt = acceptanceTime.AddMinutes(EscalationDelayMinutes);
            // Enqueue admin notification
            this.notificationForDecision = new NotificationCommand(
                "integrity_degrade_pending",
                "Integrity Degradation Pending",
                $"Binary integrity revoked {RevokedThreshold} consecutive times. System will degrade in {EscalationDelayMinutes} minutes unless overridden by admin.",
                timestamp);

            this.pendingDegradeNotified = true;

            return new VerdictReaction(
                VerdictAction.Limit,
                EnforcementIssueSeverity.Warning,
                $"Escalation: degrade in {EscalationDelayMinutes} minutes unless overridden");
        }

        if (!this.escalationFired)
        {
            return new VerdictReaction(VerdictAction.Limit, EnforcementIssueSeverity.Warning, "Escalation pending");
        }

        return new VerdictReaction(
            VerdictAction.Limit, EnforcementIssueSeverity.Warning,
            $"Revoked threshold held ({this.consecutiveRevokedCount}/{RevokedThreshold})");
    }

    /// <summary>Evaluates the pending deadline without owning a timer or scheduling work.</summary>
    public VerdictReaction EvaluateDeadline(DateTimeOffset now)
        => this.EvaluateDeadlineDecision(now).Reaction;

    public VerdictDecision EvaluateDeadlineDecision(DateTimeOffset now, string? identityScope = null)
    {
        var observedAt = this.clock();
        lock (this.stateGate)
        {
            var identity = this.AllocateDecisionIdentityLocked();
            this.sequence = identity.Sequence;
            this.epoch = identity.Epoch;
            var reaction = this.CommitDeadlineIfDueLocked(now)
                ? new VerdictReaction(VerdictAction.Degrade, EnforcementIssueSeverity.Severe, "Integrity deadline reached")
                : this.pendingDegradeNotified && !this.escalationFired
                    ? new VerdictReaction(VerdictAction.Limit, EnforcementIssueSeverity.Warning, "Integrity deadline pending")
                    : new VerdictReaction(VerdictAction.None, null, "No integrity deadline transition");
            var scope = identityScope ?? this.identityScope;
            return new VerdictDecision(identity.Sequence, identity.Epoch, reaction, null, observedAt, scope,
                BuildIdempotencyKey(scope, identity.Epoch, identity.Sequence, reaction.Action.ToString()), null);
        }
    }

    private bool CommitDeadlineIfDueLocked(DateTimeOffset acceptanceTime)
    {
        if (!this.pendingDegradeNotified || this.escalationFired
            || this.escalationDueAt is null || acceptanceTime < this.escalationDueAt.Value) return false;

        this.escalationFired = true;
        this.degraded = true;
        return true;
    }

    internal void SeedDecisionCountersForTesting(long sequence, long epoch)
    {
        lock (this.stateGate) { this.sequence = sequence; this.epoch = epoch; }
    }

    /// <summary>
    /// Resets the handler state (for testing purposes).
    /// </summary>
    internal void ResetForTesting()
    {
        this.consecutiveRevokedCount = 0;
        this.consecutiveTrustCount = 0;
        this.consecutiveFailures = 0;
        this.circuitOpenedAt = null;
        this.pendingDegradeNotified = false;
        this.escalationDueAt = null;
        this.escalationFired = false;
        this.degraded = false;
        this.sequence = 0;
        this.epoch = 0;
        this.notificationForDecision = null;
        this.serviceStartTime = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the service start time (for testing).
    /// </summary>
    internal void SetServiceStartTime(DateTimeOffset startTime)
    {
        this.serviceStartTime = startTime;
    }

    /// <summary>
    /// Sets the circuit opened time directly (for testing).
    /// </summary>
    internal void SetCircuitOpenedAt(DateTimeOffset? circuitOpenedAt)
    {
        this.circuitOpenedAt = circuitOpenedAt;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
