// <copyright file="FakeRealtimeChannel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

/// <summary>
/// T21 — Fake implementation of IRealtimeChannel for testing.
/// Allows simulating subscribe/unsubscribe and broadcasting events in unit tests.
/// </summary>
public sealed class FakeRealtimeChannel : Domain.IRealtimeChannel
{
    private bool subscribed;
    private bool disposed;

    /// <inheritdoc />
    public bool IsSubscribed => this.subscribed && !this.disposed;

    /// <inheritdoc />
    public event EventHandler<Domain.Broadcast>? BroadcastReceived;

    /// <summary>
    /// Simulates subscribing to the channel.
    /// </summary>
    public Task SubscribeAsync()
    {
        this.subscribed = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates unsubscribing from the channel.
    /// </summary>
    public void Unsubscribe()
    {
        this.subscribed = false;
    }

    /// <summary>
    /// Fires a broadcast event to all registered handlers.
    /// </summary>
    /// <param name="payload">The broadcast payload to fire.</param>
    public void FireBroadcast(Dictionary<string, object?> payload)
    {
        this.BroadcastReceived?.Invoke(this, new Domain.Broadcast(payload));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        this.disposed = true;
        this.subscribed = false;
        this.BroadcastReceived = null;
    }
}
