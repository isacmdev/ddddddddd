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

    [Fact]
    public async Task ConnectAsync_SetsIsConnectedTrue()
    {
        // Arrange
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
    public async Task PolicyBroadcast_FiresPolicyChangedWithCorrectVersion()
    {
        // Arrange
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
        receivedArgs.Should().NotBeNull();
        receivedArgs!.NewVersion.Should().Be(5);
    }

    [Fact]
    public async Task GrantBroadcast_FiresGrantsChangedWithCorrectData()
    {
        // Arrange
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
        receivedArgs.Should().NotBeNull();
        receivedArgs!.GrantId.Should().Be("g1");
        receivedArgs.IsApproved.Should().BeTrue();
    }

    [Fact]
    public async Task ConnectAsync_Idempotent()
    {
        // Arrange
        var freshPolicy = new CountingFakeRealtimeChannel();
        var freshGrants = new CountingFakeRealtimeChannel();
        var freshSubscriber = new RealtimeSubscriber(freshPolicy, freshGrants, this.lifecycle, this.deviceId);

        // Act
        await freshSubscriber.ConnectAsync();
        await freshSubscriber.ConnectAsync();

        // Assert
        freshPolicy.SubscribeCallCount.Should().Be(1);
        freshGrants.SubscribeCallCount.Should().Be(1);

        freshSubscriber.Dispose();
    }

    [Fact]
    public async Task DisconnectAsync_Idempotent()
    {
        // Arrange
        var subscriber = this.CreateSubscriber();
        await subscriber.ConnectAsync();

        // Act & Assert - Should not throw
        await subscriber.DisconnectAsync();
        await subscriber.DisconnectAsync();
    }

    [Fact]
    public async Task BroadcastWithMissingFields_DoesNotThrow()
    {
        // Arrange
        var subscriber = this.CreateSubscriber();
        await subscriber.ConnectAsync();

        // Act & Assert - Should not throw with null/missing fields
        var act = () => this.policyChannel.FireBroadcast(new Dictionary<string, object?>());
        act.Should().NotThrow();

        act = () => this.grantsChannel.FireBroadcast(new Dictionary<string, object?>());
        act.Should().NotThrow();
    }

    private class CountingFakeRealtimeChannel : Domain.IRealtimeChannel
    {
        private bool subscribed;

        public int SubscribeCallCount { get; private set; }

        public bool IsSubscribed => this.subscribed;

        public event EventHandler<Broadcast>? BroadcastReceived;

        public Task SubscribeAsync()
        {
            this.SubscribeCallCount++;
            this.subscribed = true;
            return Task.CompletedTask;
        }

        public void Unsubscribe()
        {
            this.subscribed = false;
        }

        public void Dispose()
        {
            this.subscribed = false;
            this.BroadcastReceived = null;
        }
    }
}
