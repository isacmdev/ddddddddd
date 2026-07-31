// <copyright file="UsageAccumulatorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Collections.Concurrent;
using ControlParental.Domain;
using ControlParental.Service;
using Xunit;

/// <summary>
/// T06 — Tests for UsageAccumulator behavior.
/// Tests tick accumulation, warning thresholds, pause/resume, and foreground change.
/// Uses a TestableUsageAccumulator that overrides the repository methods for isolation.
/// </summary>
public class UsageAccumulatorTests
{
    // ── TestableUsageAccumulator ──────────────────────────────────────

    /// <summary>
    /// A testable subclass of UsageAccumulator that overrides repository methods
    /// so tests don't need a real DB or PolicyRepository instance.
    /// </summary>
    private sealed class TestableUsageAccumulator : UsageAccumulator
    {
        private Func<string, int, Task>? accumulateCallback;
        private Func<Task<UsageSnapshot>>? getSnapshotCallback;
        private Func<DateTimeOffset, Task<Grant[]>>? getGrantsCallback;
        private Func<CancellationToken, Task<Policy?>>? getPolicyCallback;

        public ConcurrentQueue<(string AppId, int Minutes)> AccumulatedCalls { get; } = new();

        public TestableUsageAccumulator(IIpcChannel? ipc, ITimeProvider timeProvider)
            : base(ipc, repository: null!, timeProvider)
        {
        }

        public void SetCallbacks(
            Func<string, int, Task>? accumulate = null,
            Func<Task<UsageSnapshot>>? getSnapshot = null,
            Func<DateTimeOffset, Task<Grant[]>>? getGrants = null,
            Func<CancellationToken, Task<Policy?>>? getPolicy = null)
        {
            this.accumulateCallback = accumulate;
            this.getSnapshotCallback = getSnapshot;
            this.getGrantsCallback = getGrants;
            this.getPolicyCallback = getPolicy;
        }

        protected override Task AccumulateUsageAsyncCoreAsync(
            string appId,
            int minutes,
            CancellationToken ct = default)
        {
            this.AccumulatedCalls.Enqueue((appId, minutes));
            return this.accumulateCallback?.Invoke(appId, minutes) ?? Task.CompletedTask;
        }

        protected override Task<Policy?> GetPolicyAsyncCore(CancellationToken ct = default)
            => this.getPolicyCallback?.Invoke(ct) ?? Task.FromResult<Policy?>(null);

        protected override Task<UsageSnapshot> GetUsageSnapshotAsyncCore(CancellationToken ct = default)
            => this.getSnapshotCallback?.Invoke() ?? Task.FromResult(new UsageSnapshot());

        protected override Task<Grant[]> GetActiveGrantsAsyncCore(
            DateTimeOffset now,
            CancellationToken ct = default)
            => this.getGrantsCallback?.Invoke(now) ?? Task.FromResult(Array.Empty<Grant>());
    }

    // ── Fakes ─────────────────────────────────────────────────────────

    private sealed class FakeIpcChannel : IIpcChannel
    {
        public bool IsConnected { get; set; } = true;
        public event Action? Disconnected;
        public event Action<IIpcMessage>? MessageReceived;

        public ConcurrentQueue<IIpcMessage> SentMessages { get; } = new();

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            this.IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StopAsync() => Task.CompletedTask;

        public Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
        {
            this.SentMessages.Enqueue(message);
            return Task.CompletedTask;
        }

        public void SimulateMessage(IIpcMessage message)
        {
            this.MessageReceived?.Invoke(message);
        }
    }

    private sealed class FakeTimeProvider : ITimeProvider
    {
        public long MonotonicNow { get; set; } = 0;
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset WallClockNow => this.Now;
        public TimeZoneInfo CurrentZone { get; set; } = TimeZoneInfo.Local;
        public DateOnly? ServerDate { get; private set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        public bool IsServerDateUncertain => false;
        public event EventHandler<TimeChangedEventArgs>? TimeChanged;

        public void SetServerDate(long offsetMs) { }
        public void SetServerDate(DateOnly serverDate) => this.ServerDate = serverDate;
        public bool DetectClockJump() => false;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static TestableUsageAccumulator CreateAccumulator(
        FakeIpcChannel ipc,
        FakeTimeProvider? timeProvider = null)
    {
        timeProvider ??= new FakeTimeProvider();
        return new TestableUsageAccumulator(ipc, timeProvider);
    }

    // ── Tests ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsFields()
    {
        var ipc = new FakeIpcChannel();
        var tp = new FakeTimeProvider();
        var accumulator = new TestableUsageAccumulator(ipc, tp);

        Assert.NotNull(accumulator);
        Assert.False(accumulator.IsPaused);
        Assert.Null(accumulator.CurrentAppId);
    }

    [Fact]
    public async Task StartAsync_SetsRunningState()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        await accumulator.StartAsync();

        // StartAsync should subscribe to IPC messages without throwing
        // The internal isRunning flag is set to true
        Assert.False(accumulator.IsPaused);
    }

    [Fact]
    public async Task Stop_StopsTimer()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        await accumulator.StartAsync();
        accumulator.Stop();

        // Stop should not throw
        // A subsequent tick should not fire because isRunning is false
    }

    [Fact]
    public async Task OnForegroundChanged_SetsCurrentAppId()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        await accumulator.StartAsync();
        accumulator.OnForegroundChanged("com.example.app");

        Assert.Equal("com.example.app", accumulator.CurrentAppId);
    }

    [Fact]
    public async Task OnForegroundChanged_SameAppId_DoesNotReset()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        await accumulator.StartAsync();
        accumulator.OnForegroundChanged("com.example.app");
        var first = accumulator.CurrentAppId;

        accumulator.OnForegroundChanged("com.example.app"); // Same app
        var second = accumulator.CurrentAppId;

        Assert.Same(first, second); // Same reference means no change
    }

    [Fact]
    public void Pause_SetsPausedFlag()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        accumulator.Pause();

        Assert.True(accumulator.IsPaused);
    }

    [Fact]
    public void Pause_Twice_IsIdempotent()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        accumulator.Pause();
        accumulator.Pause(); // Should not throw

        Assert.True(accumulator.IsPaused);
    }

    [Fact]
    public void Resume_ClearsPausedFlag()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        accumulator.Pause();
        accumulator.Resume();

        Assert.False(accumulator.IsPaused);
    }

    [Fact]
    public void Resume_WhenNotPaused_IsIdempotent()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        accumulator.Resume(); // Not paused, should not throw

        Assert.False(accumulator.IsPaused);
    }

    [Fact]
    public async Task GetMinutesRemainingAsync_NoPolicy_ReturnsNull()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);
        accumulator.SetCallbacks(getPolicy: _ => Task.FromResult<Policy?>(null));

        var remaining = await accumulator.GetMinutesRemainingAsync(
            "com.example.app",
            DateTimeOffset.UtcNow);

        Assert.Null(remaining);
    }

    [Fact]
    public async Task GetMinutesRemainingAsync_WithPolicy_ReturnsRemainingMinutes()
    {
        var policy = new Policy { DailyScreenTimeMinutes = 60 };

        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);
        accumulator.SetCallbacks(
            getSnapshot: () => Task.FromResult(new UsageSnapshot { GlobalMinutes = 30 }),
            getPolicy: _ => Task.FromResult<Policy?>(policy));

        var remaining = await accumulator.GetMinutesRemainingAsync(
            "com.example.app",
            DateTimeOffset.UtcNow);

        Assert.Equal(30, remaining); // 60 limit - 30 used = 30 remaining
    }

    [Fact]
    public async Task GetMinutesRemainingAsync_ZeroUsage_ReturnsFullLimit()
    {
        var policy = new Policy { DailyScreenTimeMinutes = 120 };

        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);
        accumulator.SetCallbacks(
            getSnapshot: () => Task.FromResult(new UsageSnapshot { GlobalMinutes = 0 }),
            getPolicy: _ => Task.FromResult<Policy?>(policy));

        var remaining = await accumulator.GetMinutesRemainingAsync(
            "com.example.app",
            DateTimeOffset.UtcNow);

        Assert.Equal(120, remaining);
    }

    [Fact]
    public async Task GetMinutesRemainingAsync_ExceedsLimit_ReturnsZero()
    {
        var policy = new Policy { DailyScreenTimeMinutes = 60 };

        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);
        accumulator.SetCallbacks(
            getSnapshot: () => Task.FromResult(new UsageSnapshot { GlobalMinutes = 100 }),
            getPolicy: _ => Task.FromResult<Policy?>(policy));

        var remaining = await accumulator.GetMinutesRemainingAsync(
            "com.example.app",
            DateTimeOffset.UtcNow);

        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task GetMinutesRemainingAsync_WithActiveGrant_IncludesGrantMinutes()
    {
        var policy = new Policy { DailyScreenTimeMinutes = 60 };
        Grant[] grants =
        [
            new Grant
            {
                Scope = "device",
                Minutes = 30,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            },
        ];

        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);
        accumulator.SetCallbacks(
            getSnapshot: () => Task.FromResult(new UsageSnapshot { GlobalMinutes = 30 }),
            getGrants: _ => Task.FromResult(grants),
            getPolicy: _ => Task.FromResult<Policy?>(policy));

        var remaining = await accumulator.GetMinutesRemainingAsync(
            "com.example.app",
            DateTimeOffset.UtcNow);

        // 60 limit + 30 grant - 30 used = 60 remaining
        Assert.Equal(60, remaining);
    }

    [Fact]
    public async Task SimulateTick_AccumulatesUsage()
    {
        var accumulated = new List<(string, int)>();
        var ipc = new FakeIpcChannel();
        var tp = new FakeTimeProvider { Now = DateTimeOffset.UtcNow };
        var accumulator = CreateAccumulator(ipc, tp);
        accumulator.SetCallbacks(
            accumulate: (appId, mins) =>
            {
                accumulated.Add((appId, mins));
                return Task.CompletedTask;
            });

        await accumulator.StartAsync();
        ((UsageAccumulator)(accumulator)).StopTimer(); // Disable timer without affecting isRunning
        accumulator.OnForegroundChanged("com.example.app");

        // Advance time so the tick accumulates
        tp.Now = tp.Now.AddSeconds(10);

        // Simulate tick (bypassing timer)
        await accumulator.SimulateTickAsync();

        // At least one accumulation should have been recorded (timer may have fired too)
        Assert.NotEmpty(accumulated);
        Assert.Equal("com.example.app", accumulated[0].Item1);
    }

    [Fact]
    public async Task SimulateTick_DoesNotAccumulate_WhenPaused()
    {
        var accumulated = new List<(string, int)>();
        var ipc = new FakeIpcChannel();
        var tp = new FakeTimeProvider { Now = DateTimeOffset.UtcNow };
        var accumulator = CreateAccumulator(ipc, tp);
        accumulator.SetCallbacks(
            accumulate: (appId, mins) =>
            {
                accumulated.Add((appId, mins));
                return Task.CompletedTask;
            });

        await accumulator.StartAsync();
        ((UsageAccumulator)(accumulator)).StopTimer(); // Disable timer without affecting isRunning
        accumulator.OnForegroundChanged("com.example.app");
        accumulator.Pause();

        tp.Now = tp.Now.AddSeconds(10);
        await accumulator.SimulateTickAsync();

        Assert.Empty(accumulated); // No accumulation while paused
    }

    [Fact]
    public async Task SimulateTick_SendsWarningAt10MinThreshold()
    {
        var policy = new Policy { DailyScreenTimeMinutes = 15 };
        var ipc = new FakeIpcChannel();
        var tp = new FakeTimeProvider { Now = DateTimeOffset.UtcNow };
        var accumulator = CreateAccumulator(ipc, tp);
        var emptyDict = new Dictionary<string, int>();
        var emptySet = new HashSet<string>();
        accumulator.SetCallbacks(
            getSnapshot: () => Task.FromResult(new UsageSnapshot(emptyDict, emptyDict, 5, emptySet)),
            getPolicy: _ => Task.FromResult<Policy?>(policy));

        await accumulator.StartAsync();
        ((UsageAccumulator)(accumulator)).StopTimer(); // Disable timer without affecting isRunning
        accumulator.OnForegroundChanged("com.example.app");

        tp.Now = tp.Now.AddSeconds(10);
        await accumulator.SimulateTickAsync();

        // 15 limit - 5 used = 10 remaining → 10-min warning should be sent
        var warning = ipc.SentMessages.OfType<ShowWarning>().FirstOrDefault();
        Assert.NotNull(warning);
        Assert.Equal(10, warning.MinutesRemaining);
    }

    [Fact]
    public async Task SimulateTick_SendsWarningAt5MinThreshold()
    {
        var policy = new Policy { DailyScreenTimeMinutes = 10 };
        var ipc = new FakeIpcChannel();
        var tp = new FakeTimeProvider { Now = DateTimeOffset.UtcNow };
        var accumulator = CreateAccumulator(ipc, tp);
        var emptyDict = new Dictionary<string, int>();
        var emptySet = new HashSet<string>();
        accumulator.SetCallbacks(
            getSnapshot: () => Task.FromResult(new UsageSnapshot(emptyDict, emptyDict, 5, emptySet)),
            getPolicy: _ => Task.FromResult<Policy?>(policy));

        await accumulator.StartAsync();
        ((UsageAccumulator)(accumulator)).StopTimer(); // Disable timer without affecting isRunning
        accumulator.OnForegroundChanged("com.example.app");

        tp.Now = tp.Now.AddSeconds(10);
        await accumulator.SimulateTickAsync();

        // 10 limit - 5 used = 5 remaining → 5-min warning should be sent
        var warning = ipc.SentMessages.OfType<ShowWarning>().FirstOrDefault();
        Assert.NotNull(warning);
        Assert.Equal(5, warning.MinutesRemaining);
    }

    [Fact]
    public async Task SimulateTick_DoesNotDuplicateWarning_AfterThresholdCrossed()
    {
        var policy = new Policy { DailyScreenTimeMinutes = 15 };
        var ipc = new FakeIpcChannel();
        var tp = new FakeTimeProvider { Now = DateTimeOffset.UtcNow };
        var accumulator = CreateAccumulator(ipc, tp);
        var emptyDict = new Dictionary<string, int>();
        var emptySet = new HashSet<string>();
        accumulator.SetCallbacks(
            getSnapshot: () => Task.FromResult(new UsageSnapshot(emptyDict, emptyDict, 5, emptySet)),
            getPolicy: _ => Task.FromResult<Policy?>(policy));

        await accumulator.StartAsync();
        ((UsageAccumulator)(accumulator)).StopTimer(); // Disable timer without affecting isRunning
        accumulator.OnForegroundChanged("com.example.app");

        // First tick: crosses 10-min threshold
        tp.Now = tp.Now.AddSeconds(10);
        await accumulator.SimulateTickAsync();

        // Second tick: still at 10 min remaining (no change in threshold crossing)
        tp.Now = tp.Now.AddSeconds(10);
        await accumulator.SimulateTickAsync();

        var warnings = ipc.SentMessages.OfType<ShowWarning>().ToList();
        Assert.Single(warnings); // Only one warning for the 10-min threshold
    }

    [Fact]
    public void SetIpcChannel_UpdatesChannel()
    {
        var ipc1 = new FakeIpcChannel();
        var ipc2 = new FakeIpcChannel();
        var tp = new FakeTimeProvider();

        var accumulator = new TestableUsageAccumulator(ipc1, tp);
        Assert.Same(ipc1, accumulator.AgentChannel);

        accumulator.SetIpcChannel(ipc2);
        Assert.Same(ipc2, accumulator.AgentChannel);
    }

    [Fact]
    public async Task Dispose_StopsAndDisposesTimer()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        await accumulator.StartAsync();
        accumulator.Dispose();
        accumulator.Dispose(); // Idempotent - should not throw
    }

    [Fact]
    public async Task RequestBackfillAsync_DoesNotThrow_WhenT07NotImplemented()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        await accumulator.RequestBackfillAsync(); // Should not throw
    }

    [Fact]
    public async Task StartAsync_SubscribesToIpcMessages()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        await accumulator.StartAsync();

        // Simulate a ForegroundChanged message from the IPC channel
        var fg = new ForegroundChanged("com.example.app");
        ipc.SimulateMessage(fg);

        Assert.Equal("com.example.app", accumulator.CurrentAppId);
    }

    [Fact]
    public async Task Stop_UnsubscribesFromIpcMessages()
    {
        var ipc = new FakeIpcChannel();
        var accumulator = CreateAccumulator(ipc);

        await accumulator.StartAsync();
        accumulator.Stop();

        // After stop, messages should no longer update CurrentAppId
        var fg = new ForegroundChanged("com.example.app");
        ipc.SimulateMessage(fg);

        Assert.Null(accumulator.CurrentAppId);
    }
}
