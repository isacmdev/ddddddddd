// <copyright file="RealtimeIdentityBridge.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;

/// <summary>
/// UI-side proxy for the Service-owned realtime lease. Credentials are retained
/// only in process memory and are cleared on every failed refresh.
/// </summary>
public sealed class RealtimeIdentityBridge : IRealtimeIdentityAuthority, IDisposable
{
    private const int MaximumTokenLength = 16384;
    private readonly IUIChannel channel;
    private readonly Func<DateTimeOffset> clock;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly object sync = new();
    private readonly CancellationTokenSource lifetime = new();
    private long refreshSequence;
    private RealtimeIdentitySnapshot? current;
    private Task? refreshLoop;
    private bool disposed;

    public RealtimeIdentityBridge(
        IUIChannel channel,
        Func<DateTimeOffset>? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.delay = delay ?? ((interval, token) => Task.Delay(interval, token));
    }

    public RealtimeIdentitySnapshot? Current
    {
        get { lock (this.sync) return this.current; }
    }

    public event EventHandler? Changed;

    /// <summary>Gets the currently hosted refresh loop.</summary>
    public Task LifecycleTask
    {
        get { lock (this.sync) return this.refreshLoop ?? Task.CompletedTask; }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
        lock (this.sync)
        {
            if (!this.disposed && this.refreshLoop is null)
            {
                this.refreshLoop = Task.Run(() => this.RefreshLoopAsync(this.lifetime.Token));
            }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var sequence = Interlocked.Increment(ref this.refreshSequence);
        RealtimeIdentityResponse? response;
        try
        {
            response = await this.channel
                .QueryAsync<GetRealtimeIdentity, RealtimeIdentityResponse>(new GetRealtimeIdentity(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Publish(sequence, null);
            throw;
        }
        catch
        {
            Publish(sequence, null);
            return;
        }

        RealtimeIdentitySnapshot? snapshot = response is not null && IsValid(response, this.clock())
            ? new RealtimeIdentitySnapshot(response.AccessToken!, response.DeviceId!, response.Generation)
            {
                ExpiresAt = response.ExpiresAt,
            }
            : null;
        Publish(sequence, snapshot);
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var snapshot = this.Current;
                var interval = snapshot is null
                    ? TimeSpan.FromSeconds(5)
                    : snapshot.ExpiresAt - this.clock() - TimeSpan.FromMinutes(1);
                if (interval < TimeSpan.FromMilliseconds(100)) interval = TimeSpan.FromMilliseconds(100);
                await this.delay(interval, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) break;
                await this.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch
        {
            Publish(Interlocked.Increment(ref this.refreshSequence), null);
        }
    }

    private void Publish(long sequence, RealtimeIdentitySnapshot? snapshot)
    {
        lock (this.sync)
        {
            if (this.disposed || sequence != Volatile.Read(ref this.refreshSequence)) return;
            this.current = snapshot;
        }
        this.Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cancels and drains the bounded refresh loop before clearing credentials.</summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task loop;
        lock (this.sync)
        {
            loop = this.refreshLoop ?? Task.CompletedTask;
        }

        this.lifetime.Cancel();
        await loop.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        lock (this.sync)
        {
            this.current = null;
            Interlocked.Increment(ref this.refreshSequence);
        }
        this.Changed?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsValid(RealtimeIdentityResponse response, DateTimeOffset now)
    {
        if (!response.Success || !string.Equals(response.ErrorCode, "none", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(response.AccessToken) || response.AccessToken.Length > MaximumTokenLength
            || response.Generation <= 0 || string.IsNullOrWhiteSpace(response.DeviceId)
            || response.ExpiresAt <= now || response.DeviceId.Length > 128)
        {
            return false;
        }
        var parts = response.AccessToken.Split('.', StringSplitOptions.None);
        return parts.Length == 3 && parts.All(static p => p.Length > 0);
    }

    public void Dispose()
    {
        lock (this.sync)
        {
            if (this.disposed) return;
            this.disposed = true;
            this.current = null;
            Interlocked.Increment(ref this.refreshSequence);
        }
        this.lifetime.Cancel();
        this.lifetime.Dispose();
        this.Changed?.Invoke(this, EventArgs.Empty);
    }
}
