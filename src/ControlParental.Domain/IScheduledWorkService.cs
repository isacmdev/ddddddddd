// <copyright file="IScheduledWorkService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T20 — Scheduled work service interface.
/// Manages periodic heartbeat, outbox push, and usage reconciliation
/// with exponential backoff and connectivity checks.
/// </summary>
public interface IScheduledWorkService
{
    /// <summary>
    /// Runs one backup operation through this service's existing single-flight coordinator.
    /// </summary>
    /// <param name="mode">The backup operation to run.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RunBackupAsync(BackupMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the service is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the scheduled work service.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the scheduled work service.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
