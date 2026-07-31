// <copyright file="IIpcChannel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Interface for the IPC channel between Service and Session Agent.
/// Implemented by NamedPipeServer (Service) and NamedPipeClient (SessionAgent).
/// </summary>
public interface IIpcChannel
{
    /// <summary>
    /// Gets a value indicating whether the channel is connected.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Event raised when the channel is disconnected.
    /// </summary>
    event Action? Disconnected;

    /// <summary>
    /// Event raised when a message is received.
    /// </summary>
    event Action<IIpcMessage>? MessageReceived;

    /// <summary>
    /// Starts the channel (server listens or client connects).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the channel and disposes resources.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Sends a message over the channel.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default);
}
