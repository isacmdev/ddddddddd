// <copyright file="BackupMode.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T20 — Identifies which one-shot work the Windows Task Scheduler backup
/// tasks should run. Mirrors the <c>--backup-*</c> command-line arguments
/// consumed by <c>Program.Main</c>.
/// </summary>
public enum BackupMode
{
    /// <summary>
    /// Run a single heartbeat against the backend.
    /// </summary>
    Heartbeat,

    /// <summary>
    /// Drain the outbox once.
    /// </summary>
    Outbox,

    /// <summary>
    /// Run one usage reconciliation pass.
    /// </summary>
    Reconciliation,
}
