// <copyright file="RealtimeSubscriberTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.Domain;
using FluentAssertions;
using Xunit;

public class RealtimeSubscriberTests : IDisposable
{
    private readonly FakeRealtimeChannel policyChannel;
    private readonly FakeRealtimeChannel grantsChannel;
    private readonly FakeWindowLifecycleObserver lifecycle;
    private readonly string deviceId;
    private RealtimeSubscriber? subscriber;

    public RealtimeSubscriberTests()
    {
        this.policyChannel = new FakeRealtimeChannel();
        this.grantsChannel = new FakeRealtimeChannel();
        this.lifecycle = new FakeWindowLifecycleObserver();
        this.deviceId = "device-123";
    }

    public void Dispose()
    {
        this.subscriber?.Dispose();
    }

    private RealtimeSubscriber CreateSubscriber()
    {
        this.subscriber = new RealtimeSubscriber(
            this.policyChannel,
            this.grantsChannel,
            this.lifecycle,
            this.deviceId);
        return this.subscriber;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConnectAsync_SetsIsConnectedTrue(bool foreground)
    {
        if (!foreground)
        {
            var policy = new CountingFakeRealtimeChannel(); var grants = new CountingFakeRealtimeChannel();
            var backgroundSubscriber = new RealtimeSubscriber(policy, grants, this.lifecycle, this.deviceId);
            await backgroundSubscriber.ConnectAsync();
            (backgroundSubscriber.IsConnected, policy.SubscribeCallCount, grants.SubscribeCallCount).Should().Be((false, 0, 0));
            return;
        }

        // Arrange
        this.lifecycle.SimulateEnterForeground();
        var subscriber = this.CreateSubscriber();

        // Act
        await subscriber.ConnectAsync();

        // Assert
        subscriber.IsConnected.Should().BeTrue();
        this.policyChannel.IsSubscribed.Should().BeTrue();
        this.grantsChannel.IsSubscribed.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectAsync_SetsIsConnectedFalse()
    {
        // Arrange
        this.lifecycle.SimulateEnterForeground();
        var subscriber = this.CreateSubscriber();
        await subscriber.ConnectAsync();

        // Act
        await subscriber.DisconnectAsync();

        // Assert
        subscriber.IsConnected.Should().BeFalse();
        this.policyChannel.IsSubscribed.Should().BeFalse();
        this.grantsChannel.IsSubscribed.Should().BeFalse();
    }

    [Fact]
    public async Task EnteredBackground_TriggersUnsubscribe()
    {
        // Arrange
        var subscriber = this.CreateSubscriber();
        this.lifecycle.SimulateEnterForeground();
        await subscriber.ConnectAsync();

        // Act
        this.lifecycle.SimulateEnterBackground();

        // Allow the async disconnect to complete
        await Task.Delay(50);

        // Assert
        subscriber.IsConnected.Should().BeFalse();
        this.policyChannel.IsSubscribed.Should().BeFalse();
        this.grantsChannel.IsSubscribed.Should().BeFalse();
    }

    [Fact]
    public async Task LegacyPolicyBroadcast_IsIgnored()
    {
        // Arrange
        this.lifecycle.SimulateEnterForeground();
        var subscriber = this.CreateSubscriber();
        await subscriber.ConnectAsync();

        PolicyChangedEventArgs? receivedArgs = null;
        subscriber.PolicyChanged += (s, e) => receivedArgs = e;

        // Act
        this.policyChannel.FireBroadcast(new Dictionary<string, object?>
        {
            { "version", 5 },
        });

        // Assert
        receivedArgs.Should().BeNull();
    }

    [Fact]
    public async Task LegacyGrantBroadcast_IsIgnored()
    {
        // Arrange
        this.lifecycle.SimulateEnterForeground();
        var subscriber = this.CreateSubscriber();
        await subscriber.ConnectAsync();

        GrantsChangedEventArgs? receivedArgs = null;
        subscriber.GrantsChanged += (s, e) => receivedArgs = e;

        // Act
        this.grantsChannel.FireBroadcast(new Dictionary<string, object?>
        {
            { "grant_id", "g1" },
            { "is_approved", true },
        });

        // Assert
        receivedArgs.Should().BeNull();
    }

    [Fact]
    public async Task MalformedHintBroadcast_DoesNotApplyState()
    {
        this.lifecycle.SimulateEnterForeground();
        var subscriber = this.CreateSubscriber();
        await subscriber.ConnectAsync();
        var policyChanged = 0;
        subscriber.PolicyChanged += (_, _) => policyChanged++;

        this.policyChannel.FireBroadcast(new Dictionary<string, object?>
        {
            ["contract"] = "control-parental.windows",
            ["version"] = 1,
            ["message_type"] = "realtime.hint",
            ["correlation_id"] = "00000000-0000-4000-8000-000000000030",
            ["payload"] = new Dictionary<string, object?> { ["unknown"] = true },
        });

        policyChanged.Should().Be(0);
    }

    [Fact]
    public async Task ConnectAsync_Idempotent()
    {
        var freshPolicy = new CountingFakeRealtimeChannel();
        var freshGrants = new CountingFakeRealtimeChannel();
        var freshSubscriber = new RealtimeSubscriber(freshPolicy, freshGrants, this.lifecycle, this.deviceId);
        this.lifecycle.SimulateEnterForeground();
        await freshSubscriber.ConnectAsync();
        await freshSubscriber.ConnectAsync();
        freshPolicy.SubscribeCallCount.Should().Be(1);
        freshGrants.SubscribeCallCount.Should().Be(1);
        freshSubscriber.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_Idempotent()
    {
        var subscriber = this.CreateSubscriber();
        this.lifecycle.SimulateEnterForeground();
        await subscriber.ConnectAsync();
        await subscriber.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact]
    public async Task BroadcastWithMissingFields_DoesNotThrow()
    {
        this.lifecycle.SimulateEnterForeground();
        var subscriber = this.CreateSubscriber();
        await subscriber.ConnectAsync();
        var act = () => this.policyChannel.FireBroadcast(new Dictionary<string, object?>());
        act.Should().NotThrow();
        act = () => this.grantsChannel.FireBroadcast(new Dictionary<string, object?>());
        act.Should().NotThrow();
    }

    [Fact]
    public async Task ConcurrentHints_CoalesceIntoOnePull()
    {
        this.lifecycle.SimulateEnterForeground();
        var pullStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releasePull = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pullCount = 0;
        this.subscriber = new RealtimeSubscriber(
            this.policyChannel,
            this.grantsChannel,
            this.lifecycle,
            this.deviceId,
            _ =>
            {
                Interlocked.Increment(ref pullCount);
                pullStarted.SetResult(true);
                return releasePull.Task;
            });
        await this.subscriber.ConnectAsync();
        var hint = new Dictionary<string, object?>
        {
            ["contract"] = "control-parental.windows", ["version"] = 1,
            ["message_type"] = "realtime.hint",
            ["correlation_id"] = "00000000-0000-4000-8000-000000000030",
            ["payload"] = new Dictionary<string, object?> { ["hint_type"] = "sync" },
        };

        this.policyChannel.FireBroadcast(hint);
        await pullStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        this.grantsChannel.FireBroadcast(hint);
        this.policyChannel.FireBroadcast(hint);
        releasePull.SetResult(true);
        await Task.Delay(50);

        Assert.Equal(2, pullCount);
    }

    [Fact]
    public async Task PendingHint_IsRetriedAfterTheInFlightPullFails()
    {
        this.lifecycle.SimulateEnterForeground();
        var pullStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failPull = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pullCount = 0;
        this.subscriber = new RealtimeSubscriber(
            this.policyChannel,
            this.grantsChannel,
            this.lifecycle,
            this.deviceId,
            _ =>
            {
                if (Interlocked.Increment(ref pullCount) == 1)
                {
                    pullStarted.SetResult(true);
                    return failPull.Task;
                }

                return Task.CompletedTask;
            });
        await this.subscriber.ConnectAsync();
        var hint = new Dictionary<string, object?>
        {
            ["contract"] = "control-parental.windows", ["version"] = 1,
            ["message_type"] = "realtime.hint",
            ["correlation_id"] = "00000000-0000-4000-8000-000000000030",
            ["payload"] = new Dictionary<string, object?> { ["hint_type"] = "sync" },
        };

        this.policyChannel.FireBroadcast(hint);
        await pullStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        this.grantsChannel.FireBroadcast(hint);
        failPull.SetException(new InvalidOperationException("offline"));

        for (var attempt = 0; attempt < 100 && Volatile.Read(ref pullCount) < 2; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.Equal(2, pullCount);
    }

    [Fact]
    public async Task PendingHint_ExpiresAfterFifteenMinutesWithoutRetry()
    {
        this.lifecycle.SimulateEnterForeground();
        var now = DateTimeOffset.Parse("2026-09-03T12:00:00Z");
        var pullStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failPull = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pullCount = 0;
        this.subscriber = new RealtimeSubscriber(
            this.policyChannel,
            this.grantsChannel,
            this.lifecycle,
            this.deviceId,
            _ =>
            {
                if (Interlocked.Increment(ref pullCount) == 1)
                {
                    pullStarted.SetResult(true);
                    return failPull.Task;
                }

                return Task.CompletedTask;
            },
            () => now);
        await this.subscriber.ConnectAsync();
        var hint = new Dictionary<string, object?>
        {
            ["contract"] = "control-parental.windows", ["version"] = 1,
            ["message_type"] = "realtime.hint",
            ["correlation_id"] = "00000000-0000-4000-8000-000000000030",
            ["payload"] = new Dictionary<string, object?> { ["hint_type"] = "sync" },
        };

        this.policyChannel.FireBroadcast(hint);
        await pullStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        this.grantsChannel.FireBroadcast(hint);
        now = now.AddMinutes(15);
        failPull.SetException(new InvalidOperationException("offline"));
        await Task.Delay(100);

        Assert.Equal(1, pullCount);
    }

    [Fact]
    public async Task IdentityRotation_ReconnectsWithTheCurrentAuthorityGeneration()
    {
        var authority = new MutableRealtimeIdentityAuthority(
            new RealtimeIdentitySnapshot("token-one", this.deviceId, 1));
        var policy = new CountingFakeRealtimeChannel();
        var grants = new CountingFakeRealtimeChannel();
        this.lifecycle.SimulateEnterForeground();
        using var subscriber = new RealtimeSubscriber(
            policy,
            grants,
            this.lifecycle,
            this.deviceId,
            syncPull: null,
            clock: null,
            identityAuthority: authority);

        await subscriber.ConnectAsync();
        authority.Current = new RealtimeIdentitySnapshot("token-two", this.deviceId, 2);
        authority.RaiseChanged();
        await subscriber.LifecycleTask;

        subscriber.IsConnected.Should().BeTrue();
        (policy.SubscribeCallCount, grants.SubscribeCallCount).Should().Be((2, 2));
    }

    [Fact]
    public async Task IdentityRevocation_DoesNotReconnectUntilAuthorityReturns()
    {
        var authority = new MutableRealtimeIdentityAuthority(
            new RealtimeIdentitySnapshot("token-one", this.deviceId, 1));
        var policy = new CountingFakeRealtimeChannel();
        var grants = new CountingFakeRealtimeChannel();
        this.lifecycle.SimulateEnterForeground();
        using var subscriber = new RealtimeSubscriber(
            policy,
            grants,
            this.lifecycle,
            this.deviceId,
            syncPull: null,
            clock: null,
            identityAuthority: authority);

        await subscriber.ConnectAsync();
        authority.Current = null;
        authority.RaiseChanged();
        await subscriber.LifecycleTask;

        subscriber.IsConnected.Should().BeFalse();
        (policy.SubscribeCallCount, grants.SubscribeCallCount).Should().Be((1, 1));
    }

    [Fact]
    public async Task QueuedForegroundConnectBeforeBackground_DoesNotStartAfterBackground()
    {
        var policy = new BarrierRealtimeChannel { BlockSubscribe = true }; var grants = new BarrierRealtimeChannel(); var subscriber = new RealtimeSubscriber(policy, grants, this.lifecycle, this.deviceId);
        this.lifecycle.SimulateEnterForeground();
        await policy.SubscribeStarted.Task;
        this.lifecycle.SimulateEnterForeground(); this.lifecycle.SimulateEnterBackground(); policy.ReleaseSubscribe();
        await subscriber.LifecycleTask;
        (subscriber.IsConnected, policy.IsSubscribed, policy.SubscribeCallCount, grants.SubscribeCallCount).Should().Be((false, false, 1, 0)); subscriber.Dispose();
        await subscriber.DisconnectAsync(); await subscriber.DisconnectAsync();
    }

    [Fact]
    public async Task FailedConnectAndQueuedFailure_CleanUpAndReconnect()
    {
        var policy = new CountingFakeRealtimeChannel(); var grants = new CountingFakeRealtimeChannel();
        this.lifecycle.SimulateEnterForeground();
        var subscriber = new RealtimeSubscriber(policy, grants, this.lifecycle, this.deviceId); policy.SubscribeException = new InvalidOperationException("first");
        await Assert.ThrowsAsync<InvalidOperationException>(() => subscriber.ConnectAsync());
        (policy.IsSubscribed, grants.IsSubscribed).Should().Be((false, false)); (policy.HandlerCount, grants.HandlerCount).Should().Be((0, 0));
        policy.SubscribeException = new InvalidOperationException("queued"); this.lifecycle.SimulateEnterForeground(); await subscriber.LifecycleTask;
        subscriber.IsConnected.Should().BeFalse();
        (policy.IsSubscribed, grants.IsSubscribed).Should().Be((false, false)); (policy.HandlerCount, grants.HandlerCount).Should().Be((0, 0));
        grants.SubscribeException = new InvalidOperationException("grant"); await Assert.ThrowsAsync<InvalidOperationException>(() => subscriber.ConnectAsync());
        (policy.IsSubscribed, grants.IsSubscribed).Should().Be((false, false)); (policy.HandlerCount, grants.HandlerCount).Should().Be((0, 0));
        await subscriber.ConnectAsync();
        (policy.SubscribeCallCount, grants.SubscribeCallCount, policy.HandlerCount, grants.HandlerCount).Should().Be((4, 2, 1, 1));
    }

    [Fact]
    public async Task OldPolicyAndGrantCallbacks_AreIgnoredAfterRestart()
    {
        var policy = new CountingFakeRealtimeChannel(); var grants = new CountingFakeRealtimeChannel();
        this.lifecycle.SimulateEnterForeground();
        var subscriber = new RealtimeSubscriber(policy, grants, this.lifecycle, this.deviceId); var policies = 0; var grantsSeen = 0;
        subscriber.PolicyChanged += (_, _) => policies++; subscriber.GrantsChanged += (_, _) => grantsSeen++;
        await subscriber.ConnectAsync(); await subscriber.DisconnectAsync(); await subscriber.ConnectAsync(); await subscriber.ConnectAsync();
        policy.FireLateBroadcast(new() { { "version", 1 } }); grants.FireLateBroadcast(new() { { "grant_id", "old" }, { "is_approved", true } });
        policy.FireBroadcast(new() { { "version", 2 } }); grants.FireBroadcast(new() { { "grant_id", "new" }, { "is_approved", true } });
        policy.FireBroadcast(new()); grants.FireBroadcast(new());
        (policies, grantsSeen).Should().Be((0, 0));
        (policy.SubscribeCallCount, grants.SubscribeCallCount).Should().Be((2, 2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StartedLateCompletion_AfterConcurrentBackgroundDispose_CleansBothChannels(bool policyBlocks)
    {
        var policy = new BarrierRealtimeChannel { BlockSubscribe = policyBlocks };
        var grants = new BarrierRealtimeChannel { BlockSubscribe = !policyBlocks };
        var subscriber = new RealtimeSubscriber(policy, grants, this.lifecycle, this.deviceId);
        this.lifecycle.SimulateEnterForeground(); var connect = subscriber.ConnectAsync();
        await (policyBlocks ? policy : grants).SubscribeStarted.Task;
        var background = Task.Run(() => this.lifecycle.SimulateEnterBackground()); var dispose = Task.Run(subscriber.Dispose); subscriber.Dispose();
        (policyBlocks ? policy : grants).ReleaseSubscribe();
        await Task.WhenAll(background, dispose, connect, subscriber.LifecycleTask);
        (subscriber.IsConnected, policy.IsSubscribed, grants.IsSubscribed, policy.HandlerCount, grants.HandlerCount).Should().Be((false, false, false, 0, 0));
    }

    private class CountingFakeRealtimeChannel : Domain.IRealtimeChannel
    {
        private bool subscribed;
        private EventHandler<Broadcast>? handlers;
        private EventHandler<Broadcast>? firstHandler;

        public int SubscribeCallCount { get; private set; } public int HandlerCount => this.handlers?.GetInvocationList().Length ?? 0;
        public Exception? SubscribeException { get; set; }
        public Action? OnSubscribed { get; set; }

        public bool IsSubscribed => this.subscribed;

        public event EventHandler<Broadcast>? BroadcastReceived { add { this.firstHandler ??= value; this.handlers += value; } remove { this.handlers -= value; } }

        public Task SubscribeAsync()
        {
            this.SubscribeCallCount++;
            var exception = this.SubscribeException; this.SubscribeException = null;
            if (exception is not null) return Task.FromException(exception);

            this.subscribed = true;
            this.OnSubscribed?.Invoke();
            return Task.CompletedTask;
        }

        public void FireBroadcast(Dictionary<string, object?> payload) => this.handlers?.Invoke(this, new Broadcast(payload));

        public void FireLateBroadcast(Dictionary<string, object?> payload) => this.firstHandler?.Invoke(this, new Broadcast(payload));

        public void Unsubscribe()
        {
            this.subscribed = false;
        }

        public void Dispose() => (this.subscribed, this.handlers) = (false, null);
    }

    private sealed class BarrierRealtimeChannel : Domain.IRealtimeChannel
    {
        private bool subscribed;
        private EventHandler<Broadcast>? handlers;
        private TaskCompletionSource<bool> release = NewSignal();

        public bool BlockSubscribe { get; set; }

        public TaskCompletionSource<bool> SubscribeStarted { get; } = NewSignal();

        public int SubscribeCallCount { get; private set; }
        public int HandlerCount => this.handlers?.GetInvocationList().Length ?? 0;

        public bool IsSubscribed => this.subscribed;

        public event EventHandler<Broadcast>? BroadcastReceived { add => this.handlers += value; remove => this.handlers -= value; }

        public async Task SubscribeAsync() { this.SubscribeCallCount++; this.SubscribeStarted.TrySetResult(true); if (this.BlockSubscribe) await this.release.Task; this.subscribed = true; }

        public void ReleaseSubscribe() => this.release.TrySetResult(true);

        public void Unsubscribe() => this.subscribed = false;

        public void Dispose()
        {
            this.Unsubscribe();
            this.handlers = null;
        }

        private static TaskCompletionSource<bool> NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class MutableRealtimeIdentityAuthority : IRealtimeIdentityAuthority
    {
        public MutableRealtimeIdentityAuthority(RealtimeIdentitySnapshot current)
        {
            this.Current = current;
        }

        public RealtimeIdentitySnapshot? Current { get; set; }

        public event EventHandler? Changed;

        public void RaiseChanged() => this.Changed?.Invoke(this, EventArgs.Empty);
    }
}
