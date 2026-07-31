// <copyright file="NamedPipeClient.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent.Interop;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ControlParental.Domain;

/// <summary>
/// Named pipe client for IPC with the Service.
/// Runs in the Session Agent (interactive session of the child).
/// </summary>
public sealed class NamedPipeClient : IIpcChannel, IDisposable
{
    private const string PipeNamePrefix = "ControlParental";
    private const int BufferSize = 65536;
    private readonly string pipeName;
    private readonly CancellationTokenSource internalCts;
    private NamedPipeClientStream? pipeClient;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeClient"/> class.
    /// </summary>
    /// <param name="pipeName">The pipe name (without prefix).</param>
    public NamedPipeClient(string pipeName)
    {
        this.pipeName = $"{PipeNamePrefix}.{pipeName}";
        this.internalCts = new CancellationTokenSource();
    }

    /// <inheritdoc />
    public bool IsConnected => this.pipeClient?.IsConnected ?? false;

    /// <inheritdoc />
    public event Action? Disconnected;

    /// <inheritdoc />
    public event Action<IIpcMessage>? MessageReceived;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this.internalCts.Token);

        // Connect to the server (with retry)
        var maxRetries = 10;
        var retryDelay = TimeSpan.FromSeconds(1);

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                this.pipeClient = new NamedPipeClientStream(
                    ".",
                    this.pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                await this.pipeClient.ConnectAsync((int)retryDelay.TotalMilliseconds);

                if (this.pipeClient.IsConnected)
                {
                    // Start reading messages
                    _ = this.ReadMessagesAsync(linkedCts.Token);
                    return;
                }
            }
            catch (IOException)
            {
                // Retry
            }
            catch (TimeoutException)
            {
                // Retry
            }

            if (i < maxRetries - 1)
            {
                await Task.Delay(retryDelay, linkedCts.Token);
            }
        }

        throw new InvalidOperationException(
            $"Failed to connect to IPC pipe '{this.pipeName}' after {maxRetries} retries.");
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        this.internalCts.Cancel();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
    {
        if (this.pipeClient == null || !this.pipeClient.IsConnected)
        {
            return;
        }

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        await this.pipeClient.WriteAsync(bytes, cancellationToken);
    }

    private async Task ReadMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];

        try
        {
            while (this.pipeClient?.IsConnected ?? false)
            {
                var bytesRead = await this.pipeClient.ReadAsync(buffer, cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                var message = this.DeserializeMessage(json);

                if (message != null)
                {
                    this.MessageReceived?.Invoke(message);
                }
            }
        }
        catch (IOException)
        {
            // Connection closed
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested
        }

        this.Disconnected?.Invoke();
    }

    private IIpcMessage? DeserializeMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("MessageType", out var typeElement))
            {
                return null;
            }

            var messageType = typeElement.GetString();

            return messageType switch
            {
                nameof(ShowOverlay) => JsonSerializer.Deserialize<ShowOverlay>(json),
                nameof(HideOverlay) => JsonSerializer.Deserialize<HideOverlay>(json),
                nameof(ShowWarning) => JsonSerializer.Deserialize<ShowWarning>(json),
                nameof(LockWorkstation) => JsonSerializer.Deserialize<LockWorkstation>(json),
                nameof(RequestStateSnapshot) => JsonSerializer.Deserialize<RequestStateSnapshot>(json),
                nameof(Ping) => JsonSerializer.Deserialize<Ping>(json),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.internalCts.Cancel();
            this.pipeClient?.Dispose();
            this.internalCts.Dispose();
            this.disposed = true;
        }
    }
}
