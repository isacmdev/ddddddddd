// <copyright file="ITaskSchedulerBackup.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T20 — Registers backup tasks with Windows Task Scheduler as a safety net
/// for when service timers fail. Tasks run on service start, user logon,
/// and on a periodic interval.
/// </summary>
public interface ITaskSchedulerBackup
{
    /// <summary>
    /// Registers backup tasks with Windows Task Scheduler.
    /// Tasks run on: service start, user logon, and periodic interval.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if registration succeeded.</returns>
    Task<bool> RegisterBackupTasksAsync(CancellationToken ct = default);

    /// <summary>
    /// Unregisters all backup tasks.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if unregistration succeeded.</returns>
    Task<bool> UnregisterBackupTasksAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if backup tasks are registered.
    /// </summary>
    bool AreBackupTasksRegistered { get; }
}
