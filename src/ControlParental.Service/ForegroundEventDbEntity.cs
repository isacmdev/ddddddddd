// <copyright file="ForegroundEventDbEntity.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

/// <summary>
/// T05/T07 — Records a foreground change event from T05 (agent).
/// Used by T07 for reconciliation to detect gaps in T06 accumulation.
/// </summary>
public sealed class ForegroundEventDbEntity
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The AppId that gained foreground.
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Wall-clock time when the app gained foreground.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Wall-clock time when the app lost foreground (null if still active).
    /// </summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Server date for the event (for grouping by day).
    /// </summary>
    public DateOnly ServerDate { get; set; }

    /// <summary>
    /// Source of the event: "T05" (WMI) or "T07" (backfill).
    /// </summary>
    public string Source { get; set; } = "T05";
}

/// <summary>
/// T07 — Records reconciliation run history for idempotency.
/// Only one reconciliation per day is recorded (unique index on ServerDate).
/// </summary>
public sealed class ReconciliationHistoryDbEntity
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The server date for which reconciliation was performed.
    /// </summary>
    public DateOnly ServerDate { get; set; }

    /// <summary>
    /// Number of apps that were checked during reconciliation.
    /// </summary>
    public int AppsReconciled { get; set; }

    /// <summary>
    /// Number of apps that received backfill (gaps found).
    /// </summary>
    public int AppsBackfilled { get; set; }

    /// <summary>
    /// Number of discrepancies found (for T12 observability).
    /// </summary>
    public int DiscrepanciesFound { get; set; }

    /// <summary>
    /// When the reconciliation started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// When the reconciliation completed (null if failed).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Error message if the reconciliation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
