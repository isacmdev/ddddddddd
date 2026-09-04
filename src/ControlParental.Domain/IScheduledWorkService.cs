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
    /// Admits a bounded, untrusted sync hint to the service-owned policy sync.
    /// </summary>
    /// <param name="source">The bounded origin of the hint.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The admission outcome.</returns>
    Task<SyncAdmissionResult> AdmitSyncAsync(
        SyncTriggerSource source,
        CancellationToken cancellationToken = default);

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

/// <summary>Allowed origins for a policy sync hint.</summary>
public enum SyncTriggerSource
{
    Startup,
    Timer,
    Polling,
    Wns,
    Ui,
}

/// <summary>Outcome of admitting a policy sync hint.</summary>
public enum SyncAdmissionResult
{
    Rejected,
    Accepted,
    Coalesced,
    Cancelled,
}
