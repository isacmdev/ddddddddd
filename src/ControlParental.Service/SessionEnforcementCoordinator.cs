namespace ControlParental.Service;

using System.Threading.Channels;

public enum SessionInputKind { Foreground, Tick }

public sealed class SessionEnforcementCoordinator : IAsyncDisposable
{
    private readonly Channel<Func<CancellationToken, ValueTask>> queue;
    private readonly SemaphoreSlim available = new(0);
    private readonly CancellationTokenSource shutdown = new();
    private readonly Task consumer;
    private readonly object coalescingGate = new();
    private Func<CancellationToken, ValueTask>? latestForeground;
    private Func<CancellationToken, ValueTask>? latestTick;
    private int foregroundPending;
    private int tickPending;
    private long foregroundSequence;
    private long tickSequence;
    private long nextSequence;
    private int queued;
    private TaskCompletionSource idle = CompletedSignal();
    private int disposed;

    public SessionEnforcementCoordinator(int sessionId, int capacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sessionId);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        this.SessionId = sessionId;
        this.queue = Channel.CreateBounded<Func<CancellationToken, ValueTask>>(
            new BoundedChannelOptions(capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        this.consumer = this.ConsumeAsync();
    }

    public int SessionId { get; }

    public async ValueTask PostAsync(Func<CancellationToken, ValueTask> work, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();
        this.MarkQueued();
        try
        {
            await this.queue.Writer.WriteAsync(work, cancellationToken);
            this.available.Release();
        }
        catch { this.MarkCompleted(); throw; }
    }

    public bool TryPostCoalescible(SessionInputKind kind, Func<CancellationToken, ValueTask> work)
    {
        this.ThrowIfDisposed();
        lock (this.coalescingGate)
        {
            if (kind == SessionInputKind.Foreground)
            {
                this.latestForeground = work;
                if (this.foregroundPending != 0)
                {
                    return false;
                }

                this.foregroundPending = 1;
                this.foregroundSequence = ++this.nextSequence;
            }
            else
            {
                this.latestTick = work;
                if (this.tickPending != 0)
                {
                    return false;
                }

                this.tickPending = 1;
                this.tickSequence = ++this.nextSequence;
            }
        }

        this.MarkQueued();
        this.available.Release();
        return true;
    }

    public Task WhenIdleAsync() => Volatile.Read(ref this.idle).Task;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) != 0)
        {
            return;
        }

        this.queue.Writer.TryComplete();
        this.shutdown.Cancel();
        await this.consumer;
        this.available.Dispose();
        this.shutdown.Dispose();
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (true)
            {
                await this.available.WaitAsync(this.shutdown.Token);
                var work = this.queue.Reader.TryRead(out var critical)
                    ? critical
                    : this.TakeNextPending();
                if (work is null)
                {
                    continue;
                }

                try { await work(this.shutdown.Token); }
                catch (OperationCanceledException) when (this.shutdown.IsCancellationRequested) { }
                finally { this.MarkCompleted(); }
            }
        }
        catch (OperationCanceledException) when (this.shutdown.IsCancellationRequested) { }
    }

    private void MarkQueued()
    {
        if (Interlocked.Increment(ref this.queued) == 1)
        {
            Volatile.Write(ref this.idle, new(TaskCreationOptions.RunContinuationsAsynchronously));
        }
    }

    private Func<CancellationToken, ValueTask>? TakeNextPending()
    {
        lock (this.coalescingGate)
        {
            if (this.foregroundPending != 0 &&
                (this.tickPending == 0 || this.foregroundSequence < this.tickSequence))
            {
                this.foregroundPending = 0;
                var foreground = this.latestForeground;
                this.latestForeground = null;
                return foreground;
            }

            if (this.tickPending == 0)
            {
                return null;
            }

            this.tickPending = 0;
            var tick = this.latestTick;
            this.latestTick = null;
            return tick;
        }
    }

    private void MarkCompleted()
    {
        if (Interlocked.Decrement(ref this.queued) == 0)
        {
            Volatile.Read(ref this.idle).TrySetResult();
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref this.disposed) != 0)
        {
            throw new InvalidOperationException("The session coordinator is stopped.");
        }
    }

    private static TaskCompletionSource CompletedSignal()
    {
        var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.SetResult();
        return signal;
    }
}
