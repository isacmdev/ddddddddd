// <copyright file="UsageAccumulator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Diagnostics;
using ControlParental.Domain;

/// <summary>
/// T06 — Live usage counter.
/// Accumulates foreground time every tick, emits warnings to the agent,
/// pauses when the session is locked/suspended.
/// Lives in the service so it survives logout of the child's session.
/// </summary>
public class UsageAccumulator : IUsageAccumulator, IDisposable
{
    // ── Constants ─────────────────────────────────────────────────────

    /// <summary>
    /// Interval between usage accumulation ticks (seconds).
    /// </summary>
    public const int TickIntervalSeconds = 8;

    /// <summary>
    /// Warning thresholds in minutes remaining. Warning sent when crossing each.
    /// </summary>
    public static readonly int[] WarningThresholds = [10, 5];

    // ── Dependencies ────────────────────────────────────────────────────

    private readonly PolicyRepository repository;
    private readonly ITimeProvider timeProvider;
    private readonly IUsageReconciler? usageReconciler;
    private IIpcChannel? ipcChannel;

    /// <summary>
    /// Gets the IPC channel for sending messages to the agent.
    /// May be null before the channel is set via <see cref="SetIpcChannel"/>.
    /// </summary>
    public IIpcChannel? AgentChannel => this.ipcChannel;

    // ── State ──────────────────────────────────────────────────────────

    private readonly object lockObj = new();
    private readonly Timer tickTimer;
    private readonly HashSet<int> triggeredThresholds = new();

    private string? currentAppId;
    private DateTimeOffset? foregroundStartTime;
    private bool isPaused;
    private bool isRunning;
    private bool isDisposed;
    private DateTimeOffset? lastTickTime;

    // Last computed remaining minutes (for warning detection)
    private int? lastWarnedRemaining;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsageAccumulator"/> class.
    /// </summary>
    /// <param name="ipcChannel">Optional IPC channel for sending messages to the agent.</param>
    /// <param name="repository">Policy repository for reading policy and usage data.</param>
    /// <param name="timeProvider">Time provider for wall clock and server date.</param>
    /// <param name="usageReconciler">Optional usage reconciler for backfill requests (T07).</param>
    public UsageAccumulator(
        IIpcChannel? ipcChannel,
        PolicyRepository repository,
        ITimeProvider timeProvider,
        IUsageReconciler? usageReconciler = null)
    {
        this.ipcChannel = ipcChannel;
        this.repository = repository;
        this.timeProvider = timeProvider;
        this.usageReconciler = usageReconciler;

        // Timer with no due time, repeating every TickIntervalSeconds
        this.tickTimer = new Timer(
            callback: _ =>
            {
                // Guard: only fire if still running (StopTimer doesn't change isRunning)
                lock (this.lockObj)
                {
                    if (!this.isRunning || this.isPaused)
                    {
                        return;
                    }
                }

                this.SimulateTickAsync().Wait();
            },
            state: null,
            dueTime: Timeout.Infinite, // Start manually
            period: TickIntervalSeconds * 1000);
    }

    /// <inheritdoc />
    public string? CurrentAppId
    {
        get
        {
            lock (this.lockObj)
            {
                return this.currentAppId;
            }
        }
    }

    /// <inheritdoc />
    public bool IsPaused
    {
        get
        {
            lock (this.lockObj)
            {
                return this.isPaused;
            }
        }
    }

    /// <inheritdoc />
    public event Action<int>? WarningThresholdCrossed;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (this.lockObj)
        {
            if (this.isRunning)
            {
                return Task.CompletedTask;
            }

            this.isRunning = true;
            this.isPaused = false;

            // Subscribe to foreground changes from the agent
            // IPC channel may be null if SetIpcChannel hasn't been called yet (agent not connected)
            if (this.ipcChannel != null)
            {
                this.ipcChannel.MessageReceived += this.OnIpcMessage;
            }

            // Start the tick timer
            this.lastTickTime = this.timeProvider.WallClockNow;
            this.tickTimer.Change(0, TickIntervalSeconds * 1000);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (this.lockObj)
        {
            if (!this.isRunning)
            {
                return;
            }

            this.isRunning = false;
            this.tickTimer.Change(Timeout.Infinite, Timeout.Infinite);
            if (this.ipcChannel != null)
            {
                this.ipcChannel.MessageReceived -= this.OnIpcMessage;
            }
        }
    }

    /// <inheritdoc />
    public void Pause()
    {
        lock (this.lockObj)
        {
            if (this.isPaused)
            {
                return;
            }

            this.isPaused = true;
            this.foregroundStartTime = null; // Reset foreground start on pause

            // Stop the timer while paused
            this.tickTimer.Change(Timeout.Infinite, Timeout.Infinite);
            Debug.WriteLine("[UsageAccumulator] Paused.");
        }
    }

    /// <inheritdoc />
    public void Resume()
    {
        lock (this.lockObj)
        {
            if (!this.isPaused)
            {
                return;
            }

            this.isPaused = false;
            this.lastTickTime = this.timeProvider.WallClockNow;
            this.tickTimer.Change(0, TickIntervalSeconds * 1000);
            Debug.WriteLine("[UsageAccumulator] Resumed.");
        }
    }

    /// <inheritdoc />
    public void OnForegroundChanged(string appId)
    {
        lock (this.lockObj)
        {
            if (appId == this.currentAppId)
            {
                return; // No change
            }

            // Reset foreground start time for the new app
            this.currentAppId = appId;
            this.foregroundStartTime = this.timeProvider.WallClockNow;
            this.triggeredThresholds.Clear(); // Reset warnings on app change
            this.lastWarnedRemaining = null;

            Debug.WriteLine($"[UsageAccumulator] Tracking foreground: {appId}");
        }
    }

    /// <inheritdoc />
    public async Task RequestBackfillAsync(CancellationToken cancellationToken = default)
    {
        // T07: Request backfill from the reconciler.
        // The reconciler is injected and handles idempotency.
        if (this.usageReconciler != null)
        {
            Debug.WriteLine("[UsageAccumulator] Requesting backfill from T07 UsageReconciler.");
            var result = await this.usageReconciler.ReconcileAsync(cancellationToken);
            Debug.WriteLine(
                $"[UsageAccumulator] Backfill result: success={result.Success}, " +
                $"reconciled={result.AppsReconciled}, backfilled={result.AppsBackfilled}, " +
                $"discrepancies={result.DiscrepanciesFound}");
        }
        else
        {
            Debug.WriteLine("[UsageAccumulator] No UsageReconciler available, skipping backfill.");
        }

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<int?> GetMinutesRemainingAsync(
        string appId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var policy = await this.GetPolicyAsyncCore(cancellationToken);
        if (policy == null)
        {
            return null; // No policy, no limit
        }

        var snapshot = await this.GetUsageSnapshotAsyncCore(cancellationToken);
        var activeGrants = await this.GetActiveGrantsAsyncCore(now, cancellationToken);

        // Get grants covering this scope
        var relevantGrants = activeGrants
            .Where(g => g.Scope == "device" || g.Scope == appId ||
                        (g.Scope == "category" && policy.CategoryAssignments.GetValueOrDefault(appId) != null))
            .ToList();

        // Calculate remaining minutes
        var limitMinutes = policy.DailyScreenTimeMinutes;

        // Global usage (already computed in snapshot)
        var globalUsed = snapshot.GlobalMinutes;

        // Additional minutes from active grants
        var grantMinutes = relevantGrants.Sum(g => g.Minutes);

        // Total available = limit + grants - used
        var totalAvailable = limitMinutes + grantMinutes;
        var remaining = totalAvailable - globalUsed;

        return remaining > 0 ? remaining : 0;
    }

    /// <inheritdoc />
    public async Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return await this.GetUsageSnapshotAsyncCore(cancellationToken);
    }

    // ── Private ────────────────────────────────────────────────────────

    private void OnIpcMessage(IIpcMessage message)
    {
        if (message is ForegroundChanged fg)
        {
            this.OnForegroundChanged(fg.AppId);
        }
    }

    /// <summary>
    /// Override point for testing: accumulates usage for an app.
    /// </summary>
    protected virtual Task AccumulateUsageAsyncCoreAsync(
        string appId,
        int minutes,
        CancellationToken ct = default)
        => this.repository.AccumulateUsageAsync(appId, minutes, ct);

    /// <summary>
    /// Override point for testing: gets the current policy.
    /// </summary>
    protected virtual Task<Policy?> GetPolicyAsyncCore(CancellationToken ct = default)
        => this.repository.GetPolicyAsync(ct);

    /// <summary>
    /// Override point for testing: gets the usage snapshot.
    /// </summary>
    protected virtual Task<UsageSnapshot> GetUsageSnapshotAsyncCore(CancellationToken ct = default)
        => this.repository.GetUsageSnapshotAsync(ct);

    /// <summary>
    /// Override point for testing: gets active grants.
    /// </summary>
    protected virtual Task<Grant[]> GetActiveGrantsAsyncCore(
        DateTimeOffset now,
        CancellationToken ct = default)
        => this.repository.GetActiveGrantsAsync(now, ct);

    // ── Internal (for testing) ─────────────────────────────────────────

    /// <summary>
    /// Simulates a tick: accumulates usage and checks warnings.
    /// Exposed for testing only. Bypasses the internal timer.
    /// </summary>
    public async Task SimulateTickAsync(CancellationToken ct = default)
    {
        string? appId;
        DateTimeOffset? startTime;
        bool paused;

        lock (this.lockObj)
        {
            if (this.isPaused || !this.isRunning)
            {
                return;
            }

            appId = this.currentAppId;
            startTime = this.foregroundStartTime;
            paused = this.isPaused;
        }

        if (paused || string.IsNullOrEmpty(appId) || startTime == null)
        {
            return;
        }

        try
        {
            // Calculate elapsed seconds
            var now = this.timeProvider.WallClockNow;
            var elapsed = now - startTime.Value;
            var elapsedSeconds = (int)elapsed.TotalSeconds;

            if (elapsedSeconds < 1)
            {
                return; // Less than 1 second, skip
            }

            // Accumulate usage
            await this.AccumulateUsageAsyncCoreAsync(appId, elapsedSeconds / 60, ct);

            // Update foreground start time for next tick
            lock (this.lockObj)
            {
                this.foregroundStartTime = now;
            }

            Debug.WriteLine(
                $"[UsageAccumulator] Accumulated {elapsedSeconds / 60} min for {appId}");

            // Check warning thresholds
            await this.CheckWarningThresholdsAsync(appId, now, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UsageAccumulator] Tick error: {ex.Message}");
        }
    }

    private async Task CheckWarningThresholdsAsync(
        string appId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var remaining = await this.GetMinutesRemainingAsync(appId, now, cancellationToken);
        if (remaining == null)
        {
            return;
        }

        // Check each threshold
        foreach (var threshold in WarningThresholds)
        {
            // Check if we just crossed this threshold (was above, now at or below)
            var wasAbove = this.lastWarnedRemaining == null || this.lastWarnedRemaining > threshold;
            var isNowAtOrBelow = remaining.Value <= threshold;

            if (wasAbove && isNowAtOrBelow && !this.triggeredThresholds.Contains(threshold))
            {
                this.triggeredThresholds.Add(threshold);
                this.lastWarnedRemaining = remaining.Value;

                Debug.WriteLine(
                    $"[UsageAccumulator] Warning threshold: {threshold} min remaining");

                // Send ShowWarning to the agent
                var warning = new ShowWarning(remaining.Value);
                await this.ipcChannel.SendAsync(warning, cancellationToken);

                // Raise the event
                this.WarningThresholdCrossed?.Invoke(threshold);
            }
        }

        this.lastWarnedRemaining = remaining.Value;
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        this.Stop();
        this.tickTimer.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sets the IPC channel for sending messages to the agent.
    /// Called by ControlParentalService after SessionManager creates its channel.
    /// </summary>
    /// <param name="channel">The IPC channel to use.</param>
    public void SetIpcChannel(IIpcChannel channel)
    {
        lock (this.lockObj)
        {
            this.ipcChannel = channel;
        }
    }

    /// <summary>
    /// Stops the internal timer (for testing only).
    /// Use this in tests to prevent the timer from firing during test execution.
    /// </summary>
    internal void StopTimer() => this.tickTimer.Change(Timeout.Infinite, Timeout.Infinite);
}
