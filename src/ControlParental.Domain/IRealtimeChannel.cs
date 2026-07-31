// <copyright file="IRealtimeChannel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T21 — Represents a broadcast message received from a realtime channel.
/// This is a Domain concept; the actual Supabase broadcast is adapted to this type.
/// </summary>
/// <param name="Payload">Dictionary of broadcast payload values.</param>
public sealed record Broadcast(IReadOnlyDictionary<string, object?> Payload);

/// <summary>
/// T21 — Abstracts the realtime channel to enable testing without Supabase Realtime.
/// This keeps realtime ONLY in App.UI — Service project has zero realtime dependencies.
/// </summary>
public interface IRealtimeChannel : IDisposable
{
    /// <summary>
    /// Gets whether the channel is currently subscribed.
    /// </summary>
    bool IsSubscribed { get; }

    /// <summary>
    /// Subscribes to the realtime channel.
    /// </summary>
    Task SubscribeAsync();

    /// <summary>
    /// Unsubscribes from the realtime channel.
    /// </summary>
    void Unsubscribe();

    /// <summary>
    /// Event fired when a broadcast message is received.
    /// </summary>
    event EventHandler<Broadcast>? BroadcastReceived;
}
