// <copyright file="IWorkstationLockManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T09 — Manages workstation locking.
/// Coordinates with the session agent to lock the workstation on demand.
/// </summary>
public interface IWorkstationLockManager
{
    /// <summary>
    /// Gets whether a lock request is currently in progress.
    /// </summary>
    bool IsLockPending { get; }

    /// <summary>
    /// Locks the workstation immediately by sending LockWorkstation to the agent.
    /// The agent executes LockWorkStation() from the interactive session.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if lock was requested successfully.</returns>
    Task<bool> LockNowAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last lock result.
    /// </summary>
    LockResult? LastLockResult { get; }

    /// <summary>
    /// Sets the IPC channel after construction.
    /// The channel is created by SessionManager at runtime and is not available at DI registration time.
    /// </summary>
    void SetIpcChannel(IIpcChannel channel);
}

/// <summary>
/// Result of a workstation lock operation.
/// </summary>
public sealed record LockResult(
    bool Success,
    DateTimeOffset Timestamp,
    string? ErrorMessage,
    ActionStatus Status)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static LockResult Succeeded(DateTimeOffset timestamp)
        => new(true, timestamp, null, ActionStatus.Confirmed);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static LockResult Failed(DateTimeOffset timestamp, string error, ActionStatus status)
        => new(false, timestamp, error, status);
}
