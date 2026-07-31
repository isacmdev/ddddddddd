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
            // Check if IPC is connected
            if (this.ipcChannel == null || !this.ipcChannel.IsConnected)
            {
                Debug.WriteLine("[WorkstationLockManager] IPC not connected, cannot lock.");
                this.RecordResult(LockResult.Failed(timestamp, "IPC not connected"));
                return false;
            }

            // Send LockWorkstation command to the agent
            var lockCommand = new LockWorkstation();
            await this.ipcChannel.SendAsync(lockCommand, cancellationToken);

            Debug.WriteLine("[WorkstationLockManager] LockWorkstation command sent to agent.");

            // Note: We don't wait for confirmation from the agent
            // The agent executes LockWorkStation() and the OS handles it
            // Success means the command was sent, not that the station is locked
            this.RecordResult(LockResult.Succeeded(timestamp));
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WorkstationLockManager] Failed to send lock command: {ex.Message}");
            this.RecordResult(LockResult.Failed(timestamp, ex.Message));
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

    // ── Private Methods ───────────────────────────────────────────────

    private void RecordResult(LockResult result)
    {
        lock (this.lockObj)
        {
            this.lastLockResult = result;
        }
    }
}
