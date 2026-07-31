// <copyright file="IRealtimeSubscriber.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T21 — Event args for policy change notifications.
/// </summary>
public class PolicyChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the new policy version.
    /// </summary>
    public required int NewVersion { get; init; }
}

/// <summary>
/// T21 — Event args for grant change notifications.
/// </summary>
public class GrantsChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the grant ID that changed.
    /// </summary>
    public required string GrantId { get; init; }

    /// <summary>
    /// Gets whether the grant is approved.
    /// </summary>
    public required bool IsApproved { get; init; }
}

/// <summary>
/// T21 — Interface for realtime subscription that is ONLY active when UI is in foreground.
/// The service does NOT open Realtime; only UI. Never as a control channel.
/// </summary>
public interface IRealtimeSubscriber : IDisposable
{
    /// <summary>
    /// Gets whether the subscriber is currently connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to the realtime channel.
    /// Should only be called when UI is in foreground.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnects from the realtime channel.
    /// Called when UI goes to background or closes.
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Event fired when policy changes are received.
    /// </summary>
    event EventHandler<PolicyChangedEventArgs>? PolicyChanged;

    /// <summary>
    /// Event fired when grants change.
    /// </summary>
    event EventHandler<GrantsChangedEventArgs>? GrantsChanged;
}
