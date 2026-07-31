// <copyright file="IUIChannel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Interop;

/// <summary>
/// T26 — Abstraction over the IPC channel between App.UI and the Service.
/// Enables unit tests to substitute a fake/mock channel without opening
/// a real named pipe. The production implementation lives in
/// <see cref="NamedPipeUIChannel"/>.
/// </summary>
public interface IUIChannel
{
    /// <summary>
    /// Queries the Service with a request and waits for a typed response.
    /// </summary>
    /// <typeparam name="TQuery">The query message type (implements <see cref="IUIMessage"/>).</typeparam>
    /// <typeparam name="TResponse">The expected response message type.</typeparam>
    /// <param name="query">The query message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response, or <c>null</c> when the Service is unavailable or the request timed out.</returns>
    Task<TResponse?> QueryAsync<TQuery, TResponse>(
        TQuery query,
        CancellationToken ct = default)
        where TQuery : IUIMessage
        where TResponse : class, IUIMessage;

    /// <summary>
    /// Sends a message to the Service without waiting for a response. Transport
    /// failures are propagated so callers can fail closed when delivery matters.
    /// </summary>
    /// <typeparam name="T">The message type (implements <see cref="IUIMessage"/>).</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task SendAsync<T>(T message, CancellationToken ct = default)
        where T : IUIMessage;
}
