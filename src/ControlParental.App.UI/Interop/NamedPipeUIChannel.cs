// <copyright file="NamedPipeUIChannel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

#pragma warning disable SA1633, SA1512

// <copyright file="NamedPipeUIChannel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Interop;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;

/// <summary>
/// T26 — Named pipe client for IPC between App.UI and Service.
/// Connects to the ControlParental.UI pipe (separate from SessionAgent pipe).
/// Provides query/response pattern with timeout.
/// </summary>
public sealed class NamedPipeUIChannel : IUIChannel, IDisposable
{
    private const string PipeName = "ControlParental.UI";
    private const int TimeoutMs = 5000;
    private const int BufferSize = 65536;

    /// <summary>
    /// Queries the Service with a request and waits for a response.
    /// </summary>
    /// <typeparam name="TQuery">The query type (implements IUIMessage).</typeparam>
    /// <typeparam name="TResponse">The expected response type.</typeparam>
    /// <param name="query">The query message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The response message, or null on timeout/failure.</returns>
    public async Task<TResponse?> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken ct = default)
        where TQuery : IUIMessage
        where TResponse : class, IUIMessage
    {
        try
        {
            CancellationTokenSource? connectCts = null;
            try
            {
                connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                connectCts.CancelAfter(TimeoutMs);

                using var pipe = new NamedPipeClientStream(
                    ".",
                    PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(TimeoutMs, connectCts.Token).ConfigureAwait(false);

                // Send query — T26 PR #14 source-gen dispatch by runtime type
                // (same approach as the Service-side NamedPipeUIServer).
                var queryTypeInfo = UIMessagesJsonContext.Default.GetTypeInfo(query.GetType())!;
                var json = JsonSerializer.Serialize(query, queryTypeInfo);
                var bytes = Encoding.UTF8.GetBytes(json);
                await pipe.WriteAsync(bytes, connectCts.Token).ConfigureAwait(false);

                // Read response
                var buffer = new byte[BufferSize];
                var bytesRead = await pipe.ReadAsync(buffer, connectCts.Token).ConfigureAwait(false);
                if (bytesRead == 0)
                {
                    return null;
                }

                var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var responseTypeInfo = UIMessagesJsonContext.Default.GetTypeInfo(typeof(TResponse))!;
                var response = JsonSerializer.Deserialize(responseJson, responseTypeInfo) as TResponse;
                return response;
            }
            finally
            {
                connectCts?.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation
            return null;
        }
        catch (IOException)
        {
            // Pipe connection failed
            return null;
        }
        catch (JsonException)
        {
            // Deserialization failed
            return null;
        }
    }

    /// <summary>
    /// Sends a command to the Service without waiting for a response.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="message">The message to send.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been written.</returns>
    public async Task SendAsync<T>(T message, CancellationToken ct = default)
        where T : IUIMessage
    {
        using var pipe = new NamedPipeClientStream(
            ".",
            PipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);

        await pipe.ConnectAsync(TimeoutMs, ct).ConfigureAwait(false);

        // T26 PR #14 — source-gen dispatch by runtime type via UIMessagesJsonContext.
        var typeInfo = UIMessagesJsonContext.Default.GetTypeInfo(message.GetType())!;
        var json = JsonSerializer.Serialize(message, typeInfo);
        var bytes = Encoding.UTF8.GetBytes(json);
        await pipe.WriteAsync(bytes, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases the channel state.
    /// </summary>
    public void Dispose()
    {
    }
}
