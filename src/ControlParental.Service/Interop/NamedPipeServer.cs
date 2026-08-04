// <copyright file="NamedPipeServer.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Interop;

using System.IO.Pipes;
using System.Security.AccessControl;
using ControlParental.Domain;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

/// <summary>
/// Named pipe server for IPC with the Session Agent.
/// Runs in the Service (LocalSystem, Session 0).
/// Only accepts connections from the Session Agent process owned by the target child user.
/// </summary>
public sealed class NamedPipeServer : IIpcChannel, IDisposable
{
    private const string PipeNamePrefix = "ControlParental";
    private const int BufferSize = 65536;
    private readonly string pipeName;
    private readonly SecurityIdentifier childSid;
    private readonly Func<string, bool> validateClientSid;
    private readonly CancellationTokenSource internalCts;
    private PipeServerListener? listenerTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeServer"/> class.
    /// </summary>
    /// <param name="pipeName">The pipe name (without prefix).</param>
    /// <param name="childSid">SID allowed to connect to the pipe.</param>
    /// <param name="validateClientSid">Function to validate the client SID.</param>
    public NamedPipeServer(
        string pipeName,
        SecurityIdentifier childSid,
        Func<string, bool> validateClientSid)
    {
        this.pipeName = $"{PipeNamePrefix}.{pipeName}";
        this.childSid = childSid ?? throw new ArgumentNullException(nameof(childSid));
        this.validateClientSid = validateClientSid ?? throw new ArgumentNullException(nameof(validateClientSid));
        this.internalCts = new CancellationTokenSource();
    }

    /// <inheritdoc />
    public bool IsConnected => this.listenerTask?.IsConnected ?? false;

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

        this.listenerTask = new PipeServerListener(
            this.pipeName,
            this.childSid,
            this.validateClientSid,
            this.OnMessage,
            this.OnDisconnected,
            linkedCts.Token);

        await this.listenerTask.StartAsync();
    }

    internal static PipeSecurity CreatePipeSecurity(SecurityIdentifier childSid)
    {
        ArgumentNullException.ThrowIfNull(childSid);

        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            childSid,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        return security;
    }

    internal static bool ValidateClientSid(
        SecurityIdentifier? clientSid,
        Func<string, bool> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);

        if (clientSid == null)
        {
            return false;
        }

        try
        {
            return validator(clientSid.Value);
        }
        catch
        {
            return false;
        }
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
        if (this.listenerTask == null)
        {
            throw new InvalidOperationException("Server not started.");
        }

        await this.listenerTask.SendAsync(message, cancellationToken);
    }

    private void OnMessage(IIpcMessage message)
    {
        this.MessageReceived?.Invoke(message);
    }

    private void OnDisconnected()
    {
        this.Disconnected?.Invoke();
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.internalCts.Cancel();
            this.listenerTask?.Dispose();
            this.internalCts.Dispose();
            this.disposed = true;
        }
    }

    /// <summary>
    /// Internal listener that accepts connections and handles message dispatch.
    /// </summary>
    private sealed class PipeServerListener : IDisposable
    {
        private readonly string pipeName;
        private readonly SecurityIdentifier childSid;
        private readonly Func<string, bool> validateClientSid;
        private readonly Action<IIpcMessage> onMessage;
        private readonly Action onDisconnected;
        private readonly CancellationToken cancellationToken;
        private NamedPipeServerStream? pipeServer;
        private Task? listenerTask;
        private bool disposed;

        public PipeServerListener(
            string pipeName,
            SecurityIdentifier childSid,
            Func<string, bool> validateClientSid,
            Action<IIpcMessage> onMessage,
            Action onDisconnected,
            CancellationToken cancellationToken)
        {
            this.pipeName = pipeName;
            this.childSid = childSid;
            this.validateClientSid = validateClientSid;
            this.onMessage = onMessage;
            this.onDisconnected = onDisconnected;
            this.cancellationToken = cancellationToken;
        }

        public bool IsConnected => this.pipeServer?.IsConnected ?? false;

        public async Task StartAsync()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var pipeSecurity = CreatePipeSecurity(this.childSid);
                    this.pipeServer = NamedPipeServerStreamAcl.Create(
                        this.pipeName,
                        PipeDirection.InOut,
                        255,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous,
                        inBufferSize: BufferSize,
                        outBufferSize: BufferSize,
                        pipeSecurity: pipeSecurity,
                        inheritability: HandleInheritability.None);

                    await this.pipeServer.WaitForConnectionAsync(this.cancellationToken);

                    // Validate the client SID
                    if (!this.ValidateClient())
                    {
                        this.pipeServer.Close();
                        this.pipeServer.Dispose();
                        this.pipeServer = null;
                        continue;
                    }

                    // Start reading messages in a background task
                    _ = this.ReadMessagesAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NamedPipeServer] IO error: {ex.Message}");
                    this.pipeServer?.Dispose();
                    this.pipeServer = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NamedPipeServer] Unexpected error: {ex.Message}");
                    this.pipeServer?.Dispose();
                    this.pipeServer = null;
                }
            }
        }

        public async Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
        {
            if (this.pipeServer == null || !this.pipeServer.IsConnected)
            {
                return;
            }

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await this.pipeServer.WriteAsync(bytes, cancellationToken);
        }

        private async Task ReadMessagesAsync()
        {
            var buffer = new byte[BufferSize];

            try
            {
                while (this.pipeServer?.IsConnected ?? false)
                {
                    var bytesRead = await this.pipeServer.ReadAsync(buffer, this.cancellationToken);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var message = this.DeserializeMessage(json);

                    if (message != null)
                    {
                        this.onMessage(message);
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

            this.onDisconnected();
        }

        private bool ValidateClient()
        {
            if (this.pipeServer == null)
            {
                return false;
            }

            try
            {
                var clientSid = this.pipeServer.GetImpersonationUserSid();
                if (clientSid == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[NamedPipeServer] Could not get client SID.");
                    return false;
                }

                var sidString = clientSid.Value;
                var isValid = ValidateClientSid(clientSid, this.validateClientSid);

                if (!isValid)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[NamedPipeServer] Client SID '{sidString}' is not the target child user.");
                }

                return isValid;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NamedPipeServer] Failed to validate client: {ex.Message}");
                return false;
            }
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

                // Route to the correct record type based on MessageType
                return messageType switch
                {
                    nameof(ForegroundChanged) => JsonSerializer.Deserialize<ForegroundChanged>(json),
                    nameof(AgentHeartbeat) => JsonSerializer.Deserialize<AgentHeartbeat>(json),
                    nameof(StateSnapshot) => JsonSerializer.Deserialize<StateSnapshot>(json),
                    nameof(Pong) => JsonSerializer.Deserialize<Pong>(json),
                    nameof(GetUsageState) => JsonSerializer.Deserialize<GetUsageState>(json),
                    nameof(UsageStateResponse) => JsonSerializer.Deserialize<UsageStateResponse>(json),
                    _ => null,
                };
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[NamedPipeServer] Failed to deserialize message: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.pipeServer?.Dispose();
                this.disposed = true;
            }
        }
    }
}
