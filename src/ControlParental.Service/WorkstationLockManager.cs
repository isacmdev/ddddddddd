// <copyright file="WorkstationLockManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Diagnostics;
using ControlParental.Domain;

/// <summary>
/// T09 — Implementation of workstation lock manager.
/// Sends LockWorkstation command to the session agent via IPC.
/// The agent executes LockWorkStation() from the interactive session.
/// </summary>
public sealed class WorkstationLockManager : IWorkstationLockManager
{
    // ── Dependencies ────────────────────────────────────────────────────

    private IIpcChannel? ipcChannel;
    private Func<AgentCommandEnvelope, CancellationToken, ValueTask<AgentActionResult>>? commandExecutor;
    private int sessionId;
    private long generation;
    private long intentVersion;

    // ── State ──────────────────────────────────────────────────────────

    private bool isLockPending;
    private LockResult? lastLockResult;

    /// <summary>
    /// Lock object for thread safety.
    /// </summary>
    private readonly object lockObj = new();

    /// <summary>
    /// Timeout for lock operation (seconds).
    /// </summary>
    private const int LockTimeoutSeconds = 5;

    // ── Constructor ───────────────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkstationLockManager"/> class.
    /// </summary>
    /// <param name="ipcChannel">The IPC channel to communicate with the agent. May be null if set later via SetIpcChannel.</param>
    public WorkstationLockManager(IIpcChannel? ipcChannel)
    {
        this.ipcChannel = ipcChannel;
        this.isLockPending = false;
    }

    // ── IWorkstationLockManager ──────────────────────────────────────

    /// <inheritdoc />
    public bool IsLockPending
    {
        get
        {
            lock (this.lockObj)
            {
                return this.isLockPending;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> LockNowAsync(CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;

        lock (this.lockObj)
        {
            if (this.isLockPending)
            {
                Debug.WriteLine("[WorkstationLockManager] Lock already pending.");
                return false;
            }

            this.isLockPending = true;
        }

        try
        {
            Func<AgentCommandEnvelope, CancellationToken, ValueTask<AgentActionResult>>? executor;
            int currentSession;
            long currentGeneration;
            lock (this.lockObj)
            {
                executor = this.commandExecutor;
                currentSession = this.sessionId;
                currentGeneration = this.generation;
            }

            if (executor is null)
            {
                this.RecordResult(LockResult.Failed(
                    timestamp, "Typed command authority not connected", ActionStatus.InvalidState));
                return false;
            }

            var command = new AgentCommandEnvelope(
                Guid.NewGuid(), currentSession, currentGeneration,
                Interlocked.Increment(ref this.intentVersion), AgentCommandKind.LockWorkstation,
                timestamp.AddSeconds(LockTimeoutSeconds));
            var result = await executor(command, cancellationToken);
            var correlated = result.CommandId == command.CommandId &&
                result.SessionId == command.SessionId &&
                result.ConnectionGeneration == command.ConnectionGeneration &&
                result.IntentVersion == command.IntentVersion;
            if (correlated && result.Status == ActionStatus.Confirmed)
            {
                this.RecordResult(LockResult.Succeeded(timestamp));
                return true;
            }

            var status = correlated ? result.Status : ActionStatus.Stale;
            this.RecordResult(LockResult.Failed(
                timestamp, $"Workstation lock was not confirmed: {status}", status));
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorkstationLockManager] Failed to send lock command: {ex.Message}");
            this.RecordResult(LockResult.Failed(timestamp, ex.Message, ActionStatus.NativeFailure));
            return false;
        }
        finally
        {
            lock (this.lockObj)
            {
                this.isLockPending = false;
            }
        }
    }

    /// <inheritdoc />
    public LockResult? LastLockResult
    {
        get
        {
            lock (this.lockObj)
            {
                return this.lastLockResult;
            }
        }
    }

    /// <inheritdoc />
    public void SetIpcChannel(IIpcChannel channel)
    {
        this.ipcChannel = channel;
    }

    public void SetCommandExecutor(
        int sessionId,
        long generation,
        Func<AgentCommandEnvelope, CancellationToken, ValueTask<AgentActionResult>> executor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sessionId);
        ArgumentNullException.ThrowIfNull(executor);
        lock (this.lockObj)
        {
            this.sessionId = sessionId;
            this.generation = generation;
            this.commandExecutor = executor;
        }
    }

    // ── Private Methods ───────────────────────────────────────────────

    private void RecordResult(LockResult result)
    {
        lock (this.lockObj)
        {
            this.lastLockResult = result;
        }
    }
}
