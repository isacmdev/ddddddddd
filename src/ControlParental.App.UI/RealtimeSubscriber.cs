// <copyright file="RealtimeSubscriber.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Diagnostics;
using ControlParental.Domain;

public sealed class RealtimeSubscriber : IRealtimeSubscriber
{
    // ── Dependencies ────────────────────────────────────────────────────────

    private readonly IRealtimeChannel policyChannel;
    private readonly IRealtimeChannel grantsChannel;
    private readonly IWindowLifecycleObserver lifecycleObserver;
    private readonly string deviceId;

    // ── State ─────────────────────────────────────────────────────────────

    private readonly object lockObj = new();
    private bool disposed;
    private bool isConnected;

    // ── Events ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public event EventHandler<PolicyChangedEventArgs>? PolicyChanged;

    /// <inheritdoc />
    public event EventHandler<GrantsChangedEventArgs>? GrantsChanged;

    // ── Construction ──────────────────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeSubscriber"/> class.
    /// </summary>
    /// <param name="policyChannel">Realtime channel for policy changes.</param>
    /// <param name="grantsChannel">Realtime channel for grants changes.</param>
    /// <param name="lifecycleObserver">Window lifecycle observer for foreground/background events.</param>
    /// <param name="deviceId">Device ID for channel identification.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public RealtimeSubscriber(
        IRealtimeChannel policyChannel,
        IRealtimeChannel grantsChannel,
        IWindowLifecycleObserver lifecycleObserver,
        string deviceId)
    {
        this.policyChannel = policyChannel ?? throw new ArgumentNullException(nameof(policyChannel));
        this.grantsChannel = grantsChannel ?? throw new ArgumentNullException(nameof(grantsChannel));
        this.lifecycleObserver = lifecycleObserver ?? throw new ArgumentNullException(nameof(lifecycleObserver));
        this.deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));

        this.policyChannel.BroadcastReceived += this.HandlePolicyBroadcast;
        this.grantsChannel.BroadcastReceived += this.HandleGrantBroadcast;

        this.lifecycleObserver.EnteredForeground += this.OnEnteredForeground;
        this.lifecycleObserver.EnteredBackground += this.OnEnteredBackground;
    }

    // ── IRealtimeSubscriber ─────────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsConnected
    {
        get
        {
            lock (this.lockObj)
            {
                return this.isConnected;
            }
        }
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken ct = default)
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(RealtimeSubscriber));
        }

        return this.ConnectInternalAsync(ct);
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        return this.DisconnectInternalAsync(ct);
    }

    // ── Private Methods ────────────────────────────────────────────────────

    private async Task ConnectInternalAsync(CancellationToken ct = default)
    {
        lock (this.lockObj)
        {
            if (this.isConnected || this.disposed)
            {
                return;
            }

            this.isConnected = true;
        }

        try
        {
            await this.policyChannel.SubscribeAsync().ConfigureAwait(false);
            await this.grantsChannel.SubscribeAsync().ConfigureAwait(false);

            Trace.WriteLine($"[RealtimeSubscriber] Connected for device {this.deviceId}");
        }
        catch
        {
            lock (this.lockObj)
            {
                this.isConnected = false;
            }

            throw;
        }
    }

    private async Task DisconnectInternalAsync(CancellationToken ct = default)
    {
        lock (this.lockObj)
        {
            if (!this.isConnected)
            {
                return;
            }

            this.isConnected = false;
        }

        this.policyChannel.Unsubscribe();
        this.grantsChannel.Unsubscribe();

        // Allow unsubscribe operations to complete
        await Task.Yield();

        Trace.WriteLine($"[RealtimeSubscriber] Disconnected for device {this.deviceId}");
    }

    private void HandlePolicyBroadcast(object? sender, Broadcast broadcast)
    {
        try
        {
            // Payload contains { "version": <int> }
            if (broadcast.Payload.TryGetValue("version", out var versionObj) &&
                int.TryParse(versionObj?.ToString(), out var version))
            {
                this.PolicyChanged?.Invoke(
                    this,
                    new PolicyChangedEventArgs { NewVersion = version });
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RealtimeSubscriber] Error parsing policy broadcast: {ex.Message}");
        }
    }

    private void HandleGrantBroadcast(object? sender, Broadcast broadcast)
    {
        try
        {
            // Payload contains { "grant_id": <string>, "is_approved": <bool> }
            if (broadcast.Payload.TryGetValue("grant_id", out var grantIdObj) &&
                broadcast.Payload.TryGetValue("is_approved", out var isApprovedObj))
            {
                var grantId = grantIdObj?.ToString();
                var isApproved = isApprovedObj is bool b && b;

                if (!string.IsNullOrEmpty(grantId))
                {
                    this.GrantsChanged?.Invoke(
                        this,
                        new GrantsChangedEventArgs { GrantId = grantId, IsApproved = isApproved });
                }
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RealtimeSubscriber] Error parsing grant broadcast: {ex.Message}");
        }
    }

    private void OnEnteredForeground(object? sender, EventArgs e)
    {
        _ = this.ConnectAsync();
    }

    private void OnEnteredBackground(object? sender, EventArgs e)
    {
        _ = this.DisconnectAsync();
    }

    // ── IDisposable ────────────────────────────────────────────────────────

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;

        this.policyChannel.BroadcastReceived -= this.HandlePolicyBroadcast;
        this.grantsChannel.BroadcastReceived -= this.HandleGrantBroadcast;

        this.lifecycleObserver.EnteredForeground -= this.OnEnteredForeground;
        this.lifecycleObserver.EnteredBackground -= this.OnEnteredBackground;

        // Synchronously disconnect (fire and forget is acceptable on dispose)
        _ = this.DisconnectAsync();
    }
}
