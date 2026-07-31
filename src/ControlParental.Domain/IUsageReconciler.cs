// <copyright file="IUsageReconciler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T07 — Usage reconciler interface.
/// Backfills usage from WMI/ETW process events when T06 was not running.
/// Idempotent: re-running does not double-count.
/// Degrades gracefully if WMI/ETW is unavailable (no crash, logs warning).
/// </summary>
public interface IUsageReconciler
{
    /// <summary>
    /// Gets whether the reconciler is in degraded mode (WMI/ETW unavailable).
    /// </summary>
    bool IsDegraded { get; }

    /// <summary>
    /// Gets whether the reconciler is actively listening for process events.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the WMI/ETW event watcher.
    /// Registers for process start/stop events and begins reconciliation.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the event watcher and releases resources.
    /// </summary>
    void Stop();

    /// <summary>
    /// Performs a one-time reconciliation: compares WMI events with recorded usage
    /// and backfills gaps. Idempotent — calling multiple times has no extra effect.
    /// </summary>
    /// <returns>A reconciliation result with backfill summary and discrepancies.</returns>
    Task<ReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Raised when a discrepancy is detected between WMI events and recorded usage.
    /// The reconciler has already corrected it; this event is for observability/T12.
    /// </summary>
    event Action<ReconciliationDiscrepancy>? DiscrepancyFound;

    /// <summary>
    /// Raised when the reconciler enters or exits degraded mode.
    /// </summary>
    event Action<bool>? DegradedModeChanged;

    /// <summary>
    /// Sets the IPC channel after construction.
    /// The channel is created by SessionManager at runtime and is not available at DI registration time.
    /// </summary>
    void SetIpcChannel(IIpcChannel channel);
}

/// <summary>
/// Result of a reconciliation run.
/// </summary>
public sealed record ReconciliationResult(
    bool Success,
    int AppsReconciled,
    int AppsBackfilled,
    int DiscrepanciesFound,
    TimeSpan Elapsed,
    string? ErrorMessage)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ReconciliationResult Ok(int reconciled, int backfilled, int discrepancies, TimeSpan elapsed)
        => new(true, reconciled, backfilled, discrepancies, elapsed, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ReconciliationResult Fail(string error, TimeSpan elapsed)
        => new(false, 0, 0, 0, elapsed, error);
}

/// <summary>
/// A detected discrepancy between WMI events and recorded usage.
/// </summary>
public sealed record ReconciliationDiscrepancy(
    string AppId,
    int WmiMinutes,
    int RecordedMinutes,
    int BackfilledDelta,
    DateOnly ServerDate,
    DiscrepancyReason Reason);

/// <summary>
/// Why a discrepancy was detected.
/// </summary>
public enum DiscrepancyReason
{
    /// <summary>WMI shows usage that was not recorded by T06.</summary>
    BackfillNeeded,

    /// <summary>WMI shows less usage than recorded (possible tampering or clock jump).</summary>
    UnderRecorded,

    /// <summary>The app was running but T06 was paused (session locked).</summary>
    PausedPeriod,

    /// <summary>Reconciler caught up after service restart.</summary>
    ServiceRestartCatchup,
}
