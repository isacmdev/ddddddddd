// <copyright file="RealtimeSubscriber.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Diagnostics;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Domain.WireContracts;
using ControlParental.Domain.WireContracts.Models;

public sealed class RealtimeSubscriber : IRealtimeSubscriber
{
    private static readonly TimeSpan HintLifetime = TimeSpan.FromMinutes(15);
    private readonly IRealtimeChannel policyChannel;
    private readonly IRealtimeChannel grantsChannel;
    private readonly IWindowLifecycleObserver lifecycleObserver;
    private readonly IRealtimeIdentityAuthority? identityAuthority;
    private readonly string deviceId;
    private readonly Func<CancellationToken, Task> syncPull;
    private readonly Func<DateTimeOffset> clock;
    private readonly object lockObj = new();
    private readonly SemaphoreSlim lifecycleGate = new(1, 1);
    private Task? syncPullTask;
    private CancellationTokenSource? syncPullCancellation;
    private bool syncPullPending;
    private DateTimeOffset? syncPullExpiresAt;
    private bool disposed;
    private bool isConnected;
    private long generation; private long lifecycleEpoch; private long? readyGeneration; private CancellationTokenSource? generationCancellation; private EventHandler<Broadcast>? policyHandler; private EventHandler<Broadcast>? grantsHandler; private Task lifecycleTask = Task.CompletedTask;

    public event EventHandler<PolicyChangedEventArgs>? PolicyChanged;

    public event EventHandler<GrantsChangedEventArgs>? GrantsChanged;

    public RealtimeSubscriber(
        IRealtimeChannel policyChannel,
        IRealtimeChannel grantsChannel,
        IWindowLifecycleObserver lifecycleObserver,
        string deviceId,
        Func<CancellationToken, Task>? syncPull = null,
        Func<DateTimeOffset>? clock = null,
        IRealtimeIdentityAuthority? identityAuthority = null)
    {
        this.policyChannel = policyChannel ?? throw new ArgumentNullException(nameof(policyChannel));
        this.grantsChannel = grantsChannel ?? throw new ArgumentNullException(nameof(grantsChannel));
        this.lifecycleObserver = lifecycleObserver ?? throw new ArgumentNullException(nameof(lifecycleObserver));
        this.deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        this.syncPull = syncPull ?? (_ => Task.CompletedTask);
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.identityAuthority = identityAuthority;
        this.lifecycleObserver.EnteredForeground += this.OnEnteredForeground;
        this.lifecycleObserver.EnteredBackground += this.OnEnteredBackground;
        if (this.identityAuthority is not null)
        {
            this.identityAuthority.Changed += this.OnIdentityChanged;
        }
    }

    /// <summary>
    /// Creates the production subscriber whose hint reaction is an authenticated
    /// Service IPC TriggerSync request; realtime payloads are never applied locally.
    /// </summary>
    public RealtimeSubscriber(
        IRealtimeChannel policyChannel,
        IRealtimeChannel grantsChannel,
        IWindowLifecycleObserver lifecycleObserver,
        string deviceId,
        Interop.IUIChannel uiChannel)
        : this(policyChannel, grantsChannel, lifecycleObserver, deviceId,
            ct => uiChannel.SendAsync(new TriggerSync(), ct))
    {
        ArgumentNullException.ThrowIfNull(uiChannel);
    }

    /// <summary>
    /// Creates the production subscriber with the Service-owned identity lease.
    /// Identity changes fence the current socket before a serialized reconnect.
    /// </summary>
    public RealtimeSubscriber(
        IRealtimeChannel policyChannel,
        IRealtimeChannel grantsChannel,
        IWindowLifecycleObserver lifecycleObserver,
        string deviceId,
        Interop.IUIChannel uiChannel,
        IRealtimeIdentityAuthority identityAuthority)
        : this(
            policyChannel,
            grantsChannel,
            lifecycleObserver,
            deviceId,
            ct => uiChannel.SendAsync(new TriggerSync(), ct),
            identityAuthority: identityAuthority)
    {
        ArgumentNullException.ThrowIfNull(uiChannel);
        ArgumentNullException.ThrowIfNull(identityAuthority);
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
        if (this.identityAuthority is not null && !this.HasCurrentAuthorityIdentity()) return;
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
            if (!activeChannel.IsSubscribed)
            {
                throw new InvalidOperationException("The policy realtime channel did not subscribe.");
            }

            if (!this.IsCurrentGeneration(currentGeneration, requestedEpoch)) { activeChannel.Unsubscribe(); return; }

            if (!this.CanSubscribe(currentGeneration, requestedEpoch)) { this.InvalidateCurrentGeneration(); return; }
            activeChannel = this.grantsChannel;
            subscription = activeChannel.SubscribeAsync();
            await subscription.WaitAsync(cancellation.Token).ConfigureAwait(false);
            if (!activeChannel.IsSubscribed)
            {
                throw new InvalidOperationException("The grants realtime channel did not subscribe.");
            }

            if (!this.IsCurrentGeneration(currentGeneration, requestedEpoch)) { activeChannel.Unsubscribe(); return; }

            var isForeground = this.lifecycleObserver.IsInForeground;
            lock (this.lockObj)
            {
                if (isForeground && this.IsCurrentGenerationLocked(currentGeneration, requestedEpoch))
                {
                    this.readyGeneration = currentGeneration;
                }
            }

            Trace.WriteLine("[RealtimeSubscriber] Connected for the current identity generation.");
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
        CancellationTokenSource? oldSyncPullCancellation;
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
            oldSyncPullCancellation = this.syncPullCancellation;
            this.syncPullCancellation = null;
            this.syncPullPending = false;
            this.syncPullExpiresAt = null;
        }

        oldCancellation?.Cancel();
        oldCancellation?.Dispose();
        oldSyncPullCancellation?.Cancel();
        oldSyncPullCancellation?.Dispose();

        if (oldPolicyHandler is not null) this.policyChannel.BroadcastReceived -= oldPolicyHandler;
        if (oldGrantsHandler is not null) this.grantsChannel.BroadcastReceived -= oldGrantsHandler;

        this.policyChannel.Unsubscribe();
        this.grantsChannel.Unsubscribe();
        Trace.WriteLine("[RealtimeSubscriber] Disconnected from the current identity generation.");
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

    private bool CanSubscribe(long currentGeneration, long? requestedEpoch) =>
        this.lifecycleObserver.IsInForeground
        && (this.identityAuthority is null || this.HasCurrentAuthorityIdentity())
        && this.IsCurrentGeneration(currentGeneration, requestedEpoch);

    private bool HasCurrentAuthorityIdentity()
    {
        var identity = this.identityAuthority?.Current;
        return identity is not null
            && identity.Generation > 0
            && !string.IsNullOrWhiteSpace(identity.AccessToken)
            && (string.IsNullOrEmpty(this.deviceId)
                || string.Equals(identity.DeviceId, this.deviceId, StringComparison.Ordinal));
    }

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

            if (!this.IsAcceptedHint(broadcast.Payload, WireContractCatalog.RealtimeHint))
            {
                return;
            }

            // A realtime hint is only an accelerator for one subsequent
            // authenticated policy fetch. It must never apply state directly.
            this.RequestSyncPull();
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

            if (!this.IsAcceptedHint(broadcast.Payload, WireContractCatalog.RealtimeHint))
            {
                return;
            }

            // Grant broadcasts are likewise synchronization hints, not grant
            // decisions. The service obtains the authoritative snapshot.
            this.RequestSyncPull();
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[RealtimeSubscriber] Error parsing grant broadcast: {ex.Message}");
        }
    }

    /// <summary>
    /// Starts at most one authoritative HTTPS pull for a burst of realtime
    /// hints. Broadcasts are accelerators only; they never apply payload state.
    /// </summary>
    private void RequestSyncPull()
    {
        lock (this.lockObj)
        {
            if (this.disposed)
            {
                return;
            }

            if (this.syncPullTask is not null && !this.syncPullTask.IsCompleted)
            {
                this.syncPullPending = true;
                var expiresAt = this.clock().Add(HintLifetime);
                if (this.syncPullExpiresAt is null || expiresAt > this.syncPullExpiresAt.Value)
                {
                    this.syncPullExpiresAt = expiresAt;
                }

                return;
            }

            this.syncPullPending = false;
            this.syncPullExpiresAt = this.clock().Add(HintLifetime);
            this.syncPullCancellation = new CancellationTokenSource();
            var cancellationToken = this.syncPullCancellation.Token;
            this.syncPullTask = Task.Run(() => this.RunSyncPullAsync(cancellationToken));
        }
    }

    private async Task RunSyncPullAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (this.lockObj)
                {
                    if (this.syncPullExpiresAt is null || this.clock() >= this.syncPullExpiresAt.Value)
                    {
                        this.syncPullPending = false;
                        break;
                    }

                    this.syncPullPending = false;
                }

                try
                {
                    await this.syncPull(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[RealtimeSubscriber] Authoritative pull failed: {ex.Message}");
                }

                lock (this.lockObj)
                {
                    if (!this.syncPullPending
                        || this.disposed
                        || this.syncPullExpiresAt is null
                        || this.clock() >= this.syncPullExpiresAt.Value)
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            lock (this.lockObj)
            {
                this.syncPullTask = null;
                this.syncPullExpiresAt = null;
                this.syncPullCancellation?.Dispose();
                this.syncPullCancellation = null;
            }
        }
    }

    private bool IsAcceptedHint(
        IReadOnlyDictionary<string, object?> payload,
        ContractDescriptor<HintWire> descriptor)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            using var document = JsonDocument.Parse(bytes);
            if (!document.RootElement.TryGetProperty("payload", out var envelopePayload)
                || envelopePayload.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return WireContractCodec.DecodeAndValidate(bytes, descriptor).IsValid;
        }
        catch (JsonException)
        {
            return false;
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

    private void OnIdentityChanged(object? sender, EventArgs e)
    {
        long epoch;
        bool disposeTransport;
        lock (this.lockObj)
        {
            if (this.disposed)
            {
                return;
            }

            var identity = this.identityAuthority?.Current;
            disposeTransport = !string.IsNullOrEmpty(this.deviceId)
                && (identity is null
                    || !string.Equals(identity.DeviceId, this.deviceId, StringComparison.Ordinal));
            epoch = ++this.lifecycleEpoch;
        }

        // A changed lease is a hard generation fence. The existing channels
        // may only reconnect after the composition validates the current lease.
        this.InvalidateCurrentGeneration();
        if (disposeTransport)
        {
            this.policyChannel.Dispose();
            this.grantsChannel.Dispose();
            return;
        }

        if (this.lifecycleObserver.IsInForeground)
        {
            this.QueueLifecycleOperation(() => this.ConnectAsync(epoch));
        }
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
        if (this.identityAuthority is not null)
        {
            this.identityAuthority.Changed -= this.OnIdentityChanged;
        }
        Task? pullTask;
        lock (this.lockObj)
        {
            this.syncPullPending = false;
            this.syncPullCancellation?.Cancel();
            pullTask = this.syncPullTask;
        }
        this.InvalidateCurrentGeneration();
        this.QueueLifecycleOperation(async () =>
        {
            await this.DisconnectAsync().ConfigureAwait(false);
            if (pullTask is not null)
            {
                await pullTask.ConfigureAwait(false);
            }
        });
    }
}
