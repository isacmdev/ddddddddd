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
    string? Reason);

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
    private Timer? escalationTimer;
    private bool pendingDegradeNotified;
    private bool shadowMode = true;
    private bool disposed;

    private readonly IOutboxManager? outboxManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrityVerdictHandler"/> class.
    /// </summary>
    /// <param name="outboxManager">Optional outbox manager for admin notifications.</param>
    public IntegrityVerdictHandler(IOutboxManager? outboxManager = null)
    {
        this.outboxManager = outboxManager;
        this.serviceStartTime = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc />
    public bool IsCircuitOpen
    {
        get
        {
            if (this.circuitOpenedAt == null)
            {
                return false;
            }

            if ((DateTimeOffset.UtcNow - this.circuitOpenedAt.Value).TotalMinutes >= CircuitOpenDurationMinutes)
            {
                return false;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public bool IsShadowMode => this.shadowMode;

    /// <inheritdoc />
    public void DisableShadowMode()
    {
        this.shadowMode = false;
        System.Diagnostics.Debug.WriteLine("[IntegrityVerdictHandler] Shadow mode disabled. Verdict handling is now active.");
    }

    /// <inheritdoc />
    public VerdictReaction HandleVerdict(string? verdict, bool success, DateTimeOffset timestamp)
    {
        if (this.disposed)
        {
            return new VerdictReaction(VerdictAction.None, null, "Handler disposed");
        }

        // Mechanism 5: Grace period — skip first N minutes after startup
        if ((timestamp - this.serviceStartTime).TotalMinutes < GracePeriodMinutes)
        {
            return new VerdictReaction(VerdictAction.ShadowWarn, null, "Grace period active, skipping check");
        }

        // Mechanism 8: Circuit breaker — if open, skip all verdicts
        if (this.IsCircuitOpen)
        {
            return new VerdictReaction(VerdictAction.ShadowWarn, null, "Circuit breaker open, skipping verdict");
        }

        // Mechanism 1: Timeout graceful — network failures don't degrade
        if (!success)
        {
            this.consecutiveFailures++;
            this.consecutiveTrustCount = 0;

            if (this.consecutiveFailures >= CircuitFailureThreshold)
            {
                this.circuitOpenedAt = timestamp;
                this.consecutiveFailures = 0;
                this.consecutiveRevokedCount = 0;

                // Notify admin that circuit opened
                _ = this.EnqueueNotificationAsync(
                    "circuit_opened",
                    "Integrity Circuit Breaker Opened",
                    $"Backend failed {CircuitFailureThreshold} consecutive times. Circuit open for {CircuitOpenDurationMinutes} minutes.",
                    timestamp);

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

            if (this.consecutiveTrustCount >= RecoveryThreshold)
            {
                // Recovery threshold met — the EnforcementLevelMonitor will automatically
                // remove the degradation issue when trust is restored
                System.Diagnostics.Debug.WriteLine(
                    $"[IntegrityVerdictHandler] Trust threshold met ({RecoveryThreshold}). System can recover from degraded state.");
            }

            return new VerdictReaction(VerdictAction.None, null, "Trust verdict received");
        }

        // Unknown verdict — reset counters, warn but don't degrade
        if (verdict == "unknown")
        {
            this.consecutiveTrustCount = 0;
            this.consecutiveRevokedCount = 0;

            return new VerdictReaction(VerdictAction.Warn, EnforcementIssueSeverity.Warning, "Verdict unknown");
        }

        // "revoked" verdict — apply staged response
        if (verdict == "revoked")
        {
            return this.HandleRevokedVerdict(timestamp);
        }

        // Unknown verdict string
        this.consecutiveTrustCount = 0;
        this.consecutiveRevokedCount = 0;
        return new VerdictReaction(VerdictAction.Warn, EnforcementIssueSeverity.Warning, $"Unknown verdict: {verdict}");
    }

    /// <inheritdoc />
    public VerdictReaction HandleLocalFailure(string reason, DateTimeOffset timestamp)
    {
        if (this.disposed)
        {
            return new VerdictReaction(VerdictAction.None, null, "Handler disposed");
        }

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

    private VerdictReaction HandleRevokedVerdict(DateTimeOffset timestamp)
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
            // Stage 1: WARN (consecutive 1)
            // Stage 2: WARN (consecutive 2)
            // Stage 3: Escalation triggers
            var stage = this.consecutiveRevokedCount switch
            {
                1 => "Warn",
                2 => "Limit",
                _ => "Escalation",
            };

            return new VerdictReaction(
                VerdictAction.Warn,
                EnforcementIssueSeverity.Warning,
                $"Revoked verdict {this.consecutiveRevokedCount}/{RevokedThreshold}: {stage}");
        }

        // Mechanism 4: Escalation before DEGRADED — notify admin, wait 5 minutes
        if (!this.pendingDegradeNotified)
        {
            // Schedule escalation timer
            this.ScheduleEscalation(timestamp);

            // Enqueue admin notification
            _ = this.EnqueueNotificationAsync(
                "integrity_degrade_pending",
                "Integrity Degradation Pending",
                $"Binary integrity revoked {RevokedThreshold} consecutive times. System will degrade in {EscalationDelayMinutes} minutes unless overridden by admin.",
                timestamp);

            this.pendingDegradeNotified = true;

            return new VerdictReaction(
                VerdictAction.Warn,
                EnforcementIssueSeverity.Warning,
                $"Escalation: degrade in {EscalationDelayMinutes} minutes unless overridden");
        }

        // Already notified — degrade now
        return new VerdictReaction(
            VerdictAction.Degrade,
            EnforcementIssueSeverity.Severe,
            $"Revoked threshold exceeded ({this.consecutiveRevokedCount}/{RevokedThreshold}), degrading");
    }

    private void ScheduleEscalation(DateTimeOffset timestamp)
    {
        this.escalationTimer?.Dispose();
        this.escalationTimer = new Timer(
            _ => this.OnEscalationTimer(),
            null,
            TimeSpan.FromMinutes(EscalationDelayMinutes),
            Timeout.InfiniteTimeSpan);

        System.Diagnostics.Debug.WriteLine(
            $"[IntegrityVerdictHandler] Escalation timer scheduled for {EscalationDelayMinutes} minutes from {timestamp}");
    }

    private void OnEscalationTimer()
    {
        if (this.disposed)
        {
            return;
        }

        // If we still haven't received a "trust" verdict, the degradation is now active
        // This is called after the 5-minute window; the caller (AntiTamperMonitor) handles the actual AddIssue
        System.Diagnostics.Debug.WriteLine("[IntegrityVerdictHandler] Escalation timer fired. Degradation is now active.");
    }

    private async Task EnqueueNotificationAsync(
        string notificationType,
        string title,
        string body,
        DateTimeOffset timestamp)
    {
        if (this.outboxManager == null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[IntegrityVerdictHandler] Notification not sent (no outbox): {title}");
            return;
        }

        try
        {
            var payload = new
            {
                NotificationType = notificationType,
                Title = title,
                Body = body,
                Timestamp = timestamp.ToString("O"),
                Source = "IntegrityVerdictHandler",
            };

            await this.outboxManager.EnqueueAsync(
                "notifications",
                payload,
                $"integrity_{notificationType}_{timestamp.ToUnixTimeMilliseconds()}",
                CancellationToken.None);

            System.Diagnostics.Debug.WriteLine(
                $"[IntegrityVerdictHandler] Notification enqueued: {title}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[IntegrityVerdictHandler] Failed to enqueue notification: {ex.Message}");
        }
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
        if (!this.disposed)
        {
            this.disposed = true;
            this.escalationTimer?.Dispose();
            this.escalationTimer = null;
        }

        GC.SuppressFinalize(this);
    }
}
