// <copyright file="RealtimeSubscriber.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Diagnostics;
using ControlParental.Domain;

public sealed class RealtimeSubscriber : IRealtimeSubscriber
{
    private readonly IRealtimeChannel policyChannel;
    private readonly IRealtimeChannel grantsChannel;
    private readonly IWindowLifecycleObserver lifecycleObserver;
    private readonly string deviceId;
    private readonly object lockObj = new();
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private bool disposed;
    private bool isConnected;
    private long generation; private long lifecycleEpoch; private long? readyGeneration; private CancellationTokenSource? generationCancellation; private EventHandler<Broadcast>? policyHandler; private EventHandler<Broadcast>? grantsHandler; private Task lifecycleTask = Task.CompletedTask;

    public event EventHandler<PolicyChangedEventArgs>? PolicyChanged;

    public event EventHandler<GrantsChangedEventArgs>? GrantsChanged;

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
        this.lifecycleObserver.EnteredForeground += this.OnEnteredForeground;
        this.lifecycleObserver.EnteredBackground += this.OnEnteredBackground;
    }

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

    public Task LifecycleTask
    {
        get
        {
            lock (this.lockObj)
            {
                return this.lifecycleTask;
            }
        }
    }

    public Task ConnectAsync(CancellationToken ct = default)
    {
        long epoch; lock (this.lockObj) { epoch = this.lifecycleEpoch; }
        return this.RunLifecycleCoreAsync(() => this.ConnectCoreAsync(ct, epoch), ct);
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        this.InvalidateCurrentGeneration();
        return this.RunLifecycleCoreAsync(() => Task.CompletedTask, ct);
    }

    private Task ConnectAsync(long epoch) => this.RunLifecycleCoreAsync(() => this.ConnectCoreAsync(CancellationToken.None, epoch), CancellationToken.None);

    private async Task ConnectCoreAsync(CancellationToken ct, long? requestedEpoch = null)
    {
        long currentGeneration;
        if (!this.lifecycleObserver.IsInForeground) return;
        lock (this.lockObj)
        {
            if (this.disposed)
            { throw new ObjectDisposedException(nameof(RealtimeSubscriber)); }

            if (requestedEpoch is not null && this.lifecycleEpoch != requestedEpoch.Value) return;

            if (this.isConnected)
            { return; }

            this.isConnected = true;
            currentGeneration = ++this.generation;
            this.generationCancellation = new CancellationTokenSource();
            this.AttachHandlers(currentGeneration);
        }

        Task? subscription = null; IRealtimeChannel? activeChannel = null;
        try
        {
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, this.GetGenerationCancellation(currentGeneration));
            if (!this.CanSubscribe(currentGeneration, requestedEpoch)) { this.InvalidateCurrentGeneration(); return; }
            activeChannel = this.policyChannel;
            subscription = activeChannel.SubscribeAsync();
            await subscription.WaitAsync(cancellation.Token).ConfigureAwait(false);
            if (!this.IsCurrentGeneration(currentGeneration, requestedEpoch)) { activeChannel.Unsubscribe(); return; }

            if (!this.CanSubscribe(currentGeneration, requestedEpoch)) { this.InvalidateCurrentGeneration(); return; }
            activeChannel = this.grantsChannel;
            subscription = activeChannel.SubscribeAsync();
            await subscription.WaitAsync(cancellation.Token).ConfigureAwait(false);
            if (!this.IsCurrentGeneration(currentGeneration, requestedEpoch)) { activeChannel.Unsubscribe(); return; }

            var isForeground = this.lifecycleObserver.IsInForeground;
            lock (this.lockObj)
            {
                if (isForeground && this.IsCurrentGenerationLocked(currentGeneration, requestedEpoch))
                {
                    this.readyGeneration = currentGeneration;
                }
            }

            Trace.WriteLine($"[RealtimeSubscriber] Connected for device {this.deviceId}");
        }
        catch
        {
            var wasCurrent = this.IsCurrentGeneration(currentGeneration);
            if (subscription is not null)
            {
                try { await subscription.ConfigureAwait(false); } catch { }
            }
            if (!wasCurrent)
            {
                activeChannel?.Unsubscribe();
            }
            this.InvalidateCurrentGeneration();
            if (!wasCurrent)
            { return; }

            throw;
        }
    }

    private void InvalidateCurrentGeneration()
    {
        EventHandler<Broadcast>? oldPolicyHandler;
        EventHandler<Broadcast>? oldGrantsHandler;
        CancellationTokenSource? oldCancellation;
        lock (this.lockObj)
        {
            if (!this.isConnected && this.readyGeneration is null)
            {
                return;
            }

            this.isConnected = false;
            this.readyGeneration = null;
            this.generation++;
            oldPolicyHandler = this.policyHandler;
            oldGrantsHandler = this.grantsHandler;
            this.policyHandler = null;
            this.grantsHandler = null;
            oldCancellation = this.generationCancellation;
            this.generationCancellation = null;
        }

        oldCancellation?.Cancel();
        oldCancellation?.Dispose();

        if (oldPolicyHandler is not null) this.policyChannel.BroadcastReceived -= oldPolicyHandler;
        if (oldGrantsHandler is not null) this.grantsChannel.BroadcastReceived -= oldGrantsHandler;

        this.policyChannel.Unsubscribe();
        this.grantsChannel.Unsubscribe();
        Trace.WriteLine($"[RealtimeSubscriber] Disconnected for device {this.deviceId}");
    }

    private CancellationToken GetGenerationCancellation(long currentGeneration)
    {
        lock (this.lockObj)
        {
            return this.generation == currentGeneration && this.generationCancellation is not null
                ? this.generationCancellation.Token
                : new CancellationToken(canceled: true);
        }
    }

    private async Task RunLifecycleCoreAsync(Func<Task> operation, CancellationToken ct)
    {
        await this.lifecycleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            this.lifecycleGate.Release();
        }
    }

    private void QueueLifecycleOperation(Func<Task> operation)
    {
        lock (this.lockObj)
        {
            this.lifecycleTask = this.lifecycleTask.ContinueWith(
                async _ =>
                {
                    try
                    {
                        await operation().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"[RealtimeSubscriber] Lifecycle operation failed: {ex.Message}");
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default).Unwrap();
        }
    }

    private void AttachHandlers(long currentGeneration)
    {
        this.policyHandler = (_, broadcast) => this.HandlePolicyBroadcast(currentGeneration, broadcast);
        this.grantsHandler = (_, broadcast) => this.HandleGrantBroadcast(currentGeneration, broadcast);
        this.policyChannel.BroadcastReceived += this.policyHandler;
        this.grantsChannel.BroadcastReceived += this.grantsHandler;
    }

    private bool IsCurrentGeneration(long currentGeneration, long? requestedEpoch = null)
    {
        lock (this.lockObj)
        {
            return this.IsCurrentGenerationLocked(currentGeneration, requestedEpoch);
        }
    }

    private bool CanSubscribe(long currentGeneration, long? requestedEpoch) => this.lifecycleObserver.IsInForeground && this.IsCurrentGeneration(currentGeneration, requestedEpoch);

    private bool IsCurrentGenerationLocked(long currentGeneration, long? requestedEpoch = null) =>
        !this.disposed && this.isConnected && this.generation == currentGeneration &&
        (requestedEpoch is null || this.lifecycleEpoch == requestedEpoch.Value);

    private void HandlePolicyBroadcast(long currentGeneration, Broadcast broadcast)
    {
        try
        {
            lock (this.lockObj)
            {
                if (this.readyGeneration != currentGeneration)
                {
                    return;
                }
            }

            if (broadcast.Payload.TryGetValue("version", out var versionObj) &&
                int.TryParse(versionObj?.ToString(), out var version))
            {
                this.PolicyChanged?.Invoke(this, new PolicyChangedEventArgs { NewVersion = version });
            }
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RealtimeSubscriber] Error parsing policy broadcast: {ex.Message}");
        }
    }

    private void HandleGrantBroadcast(long currentGeneration, Broadcast broadcast)
    {
        try
        {
            lock (this.lockObj)
            {
                if (this.readyGeneration != currentGeneration)
                {
                    return;
                }
            }

            if (broadcast.Payload.TryGetValue("grant_id", out var grantIdObj) &&
                broadcast.Payload.TryGetValue("is_approved", out var isApprovedObj))
            {
                var grantId = grantIdObj?.ToString();
                var isApproved = isApprovedObj is bool b && b;
                if (!string.IsNullOrEmpty(grantId))
                {
                    this.GrantsChanged?.Invoke(this, new GrantsChangedEventArgs { GrantId = grantId, IsApproved = isApproved });
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
        long epoch; lock (this.lockObj) { epoch = ++this.lifecycleEpoch; }

        this.QueueLifecycleOperation(() => this.ConnectAsync(epoch));
    }

    private void OnEnteredBackground(object? sender, EventArgs e)
    {
        lock (this.lockObj) { ++this.lifecycleEpoch; }

        this.InvalidateCurrentGeneration();
        this.QueueLifecycleOperation(() => this.DisconnectAsync());
    }

    public void Dispose()
    {
        lock (this.lockObj)
        {
            if (this.disposed)
            { return; }

            this.disposed = true;
            ++this.lifecycleEpoch;
        }

        this.lifecycleObserver.EnteredForeground -= this.OnEnteredForeground;
        this.lifecycleObserver.EnteredBackground -= this.OnEnteredBackground;
        this.InvalidateCurrentGeneration();
        this.QueueLifecycleOperation(() => this.DisconnectAsync());
    }
}
