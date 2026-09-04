// <copyright file="NamedPipeServer.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Interop;

using System.IO.Pipes;
using System.Security.AccessControl;
using ControlParental.Domain;
using System.Security.Principal;
using System.Text.Json;

/// <summary>
/// Named pipe server for IPC with the Session Agent.
/// Runs in the Service (LocalSystem, Session 0).
/// Only accepts connections from the Session Agent process owned by the target child user.
/// </summary>
public sealed class NamedPipeServer : IIpcChannel, IDisposable
{
    private const string PipeNamePrefix = "ControlParental";
    internal const int BufferSize = 8192;
    internal const int MaxNativePipeBufferSize = 65535;
    internal const int PipeInstances = NamedPipeServerStream.MaxAllowedServerInstances;
    private readonly string pipeName;
    private readonly SecurityIdentifier childSid;
    private readonly Func<string, bool> validateClientSid;
    private readonly int sessionId;
    private readonly CancellationTokenSource internalCts;
    private readonly Func<CancellationToken, Task<IServicePipeConnection>> pipeFactory;
    private readonly Func<IServicePipeConnection, Task<IReadOnlyList<string>?>>? authenticateClient;
    private readonly Func<string?> signerProvider;
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
        Func<string, bool> validateClientSid,
        int sessionId = -1)
        : this(pipeName, childSid, validateClientSid, sessionId, null, null, null)
    {
    }

    internal NamedPipeServer(
        string pipeName,
        SecurityIdentifier childSid,
        Func<string, bool> validateClientSid,
        int sessionId,
        Func<CancellationToken, Task<IServicePipeConnection>>? pipeFactory,
        Func<IServicePipeConnection, Task<IReadOnlyList<string>?>>? authenticateClient,
        Func<string?>? signerProvider)
    {
        this.pipeName = $"{PipeNamePrefix}.{pipeName}";
        this.childSid = childSid ?? throw new ArgumentNullException(nameof(childSid));
        this.validateClientSid = validateClientSid ?? throw new ArgumentNullException(nameof(validateClientSid));
        this.sessionId = sessionId;
        this.internalCts = new CancellationTokenSource();
        this.pipeFactory = pipeFactory ?? this.CreatePipeAsync;
        this.authenticateClient = authenticateClient;
        this.signerProvider = signerProvider ?? (() => AuthenticodeSigner.GetSigner(Environment.ProcessPath));
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
            this.sessionId,
            this.OnMessage,
            this.OnDisconnected,
            linkedCts.Token,
            this.pipeFactory,
            this.authenticateClient,
            this.signerProvider);

        await this.listenerTask.StartAsync();
    }

    private Task<IServicePipeConnection> CreatePipeAsync(CancellationToken cancellationToken)
    {
        var pipeSecurity = CreatePipeSecurity(this.childSid);
        IServicePipeConnection pipe = new ServicePipeConnection(NamedPipeServerStreamAcl.Create(
            this.pipeName, PipeDirection.InOut, PipeInstances, PipeTransmissionMode.Message,
            PipeOptions.Asynchronous, BufferSize, BufferSize, pipeSecurity, HandleInheritability.None));
        return Task.FromResult(pipe);
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
    internal sealed class PipeServerListener : IDisposable
    {
        private readonly string pipeName;
        private readonly SecurityIdentifier childSid;
        private readonly Func<string, bool> validateClientSid;
        private readonly int sessionId;
        private readonly Action<IIpcMessage> onMessage;
        private readonly Action onDisconnected;
        private readonly CancellationToken cancellationToken;
        private readonly Func<CancellationToken, Task<IServicePipeConnection>> pipeFactory;
        private readonly Func<IServicePipeConnection, Task<IReadOnlyList<string>?>>? authenticateClient;
        private readonly Func<string?> signerProvider;
        private IServicePipeConnection? pipeServer;
        private ProcessIdentity? clientIdentity;
        private readonly IpcPhaseTrace trace = new();
        private Task? listenerTask;
        private bool disposed;

        public PipeServerListener(
            string pipeName,
            SecurityIdentifier childSid,
            Func<string, bool> validateClientSid,
            int sessionId,
            Action<IIpcMessage> onMessage,
            Action onDisconnected,
            CancellationToken cancellationToken,
            Func<CancellationToken, Task<IServicePipeConnection>> pipeFactory,
            Func<IServicePipeConnection, Task<IReadOnlyList<string>?>>? authenticateClient,
            Func<string?> signerProvider)
        {
            this.pipeName = pipeName;
            this.childSid = childSid;
            this.validateClientSid = validateClientSid;
            this.sessionId = sessionId;
            this.onMessage = onMessage;
            this.onDisconnected = onDisconnected;
            this.cancellationToken = cancellationToken;
            this.pipeFactory = pipeFactory;
            this.authenticateClient = authenticateClient;
            this.signerProvider = signerProvider;
        }

        public bool IsConnected => this.pipeServer?.IsConnected ?? false;

        public async Task StartAsync()
        {
            while (!this.cancellationToken.IsCancellationRequested)
            {
                try
                {
                    this.pipeServer = await this.pipeFactory(this.cancellationToken);
                    await this.pipeServer.WaitForConnectionAsync(this.cancellationToken);
                    this.trace.Record(IpcPhase.Connected);

                    var initialFrames = await (this.authenticateClient?.Invoke(this.pipeServer)
                        ?? this.AuthenticateClientAsync());
                    if (initialFrames is null)
                    {
                        this.ClosePipe();
                        this.pipeServer = null;
                        continue;
                    }

                    await this.SendHandshakeAsync();
                    await this.ReadMessagesAsync(initialFrames);
                    this.ClosePipe();
                    this.pipeServer = null;
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

            var typeInfo = UIMessagesJsonContext.Default.GetTypeInfo(message.GetType())
                ?? throw new InvalidOperationException("Unsupported IPC message type.");
            var frame = IpcFrameCodec.Encode(JsonSerializer.Serialize(message, typeInfo));
            await this.writeGate.WaitAsync(cancellationToken);
            try { await this.pipeServer.WriteAsync(frame, cancellationToken); }
            finally { this.writeGate.Release(); }
        }

        private async Task ReadMessagesAsync(IReadOnlyList<string> initialFrames)
        {
            var buffer = new byte[BufferSize];
            var decoder = new IpcFrameCodec.Decoder();

            try
            {
                this.Dispatch(initialFrames);
                while (this.pipeServer?.IsConnected ?? false)
                {
                    var bytesRead = await this.pipeServer.ReadAsync(buffer, this.cancellationToken);

                    if (bytesRead == 0)
                    {
                        break;
                    }

                    foreach (var json in decoder.Append(buffer.AsSpan(0, bytesRead)))
                    {
                        var message = this.DeserializeMessage(json);
                        if (message != null) this.onMessage(message);
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

        private async Task<IReadOnlyList<string>?> AuthenticateClientAsync()
        {
            if (this.pipeServer == null)
            {
                return null;
            }

            try
            {
                if (!GetNamedPipeClientProcessId(this.pipeServer.SafePipeHandle, out var clientPid) || clientPid == 0)
                {
                    return null;
                }
                this.trace.Record(IpcPhase.ServerPid);
                this.clientIdentity?.Dispose();
                this.clientIdentity = AuthenticodeSigner.Open((int)clientPid);
                this.trace.Record(IpcPhase.ProcessHandle);
                this.trace.Record(IpcPhase.Path);
                SecurityIdentifier? clientSid = null;
                this.pipeServer.RunAsClient(() => clientSid = this.pipeServer!.GetImpersonationUserSid());
                if (!ValidateClientSid(clientSid, this.validateClientSid))
                {
                    return null;
                }

                if (this.sessionId >= 0 && this.clientIdentity.SessionId != this.sessionId)
                {
                    return null;
                }

                var expectedSigner = AuthenticodeSigner.GetSigner(Environment.ProcessPath);
                var clientSigner = AuthenticodeSigner.GetSigner(this.clientIdentity, out _);
                this.trace.Record(IpcPhase.AuthResult);
                if (expectedSigner is null || !string.Equals(expectedSigner, clientSigner, StringComparison.OrdinalIgnoreCase)) return null;

                var decoder = new IpcFrameCodec.Decoder();
                while (true)
                {
                    var bytesRead = await this.pipeServer.ReadAsync(this.readBuffer, this.cancellationToken);
                    if (bytesRead == 0) return null;
                    var frames = decoder.Append(this.readBuffer.AsSpan(0, bytesRead));
                    if (frames.Count == 0) continue;
                    this.trace.Record(IpcPhase.ClientHelloRead);
                    if (!IpcHandshake.TryParse(frames[0], out var handshake) ||
                        !IpcHandshake.IsAuthorized(handshake, this.sessionId >= 0 ? this.sessionId : this.clientIdentity.SessionId, (int)clientPid, clientSigner)) return null;
                    return frames.Skip(1).ToArray();
                }
            }
            catch
            {
                return null;
            }
        }

        private void Dispatch(IEnumerable<string> frames)
        {
            foreach (var json in frames)
            {
                var message = this.DeserializeMessage(json);
                if (message != null) this.onMessage(message);
            }
        }

        private async Task SendHandshakeAsync()
        {
            var signer = this.signerProvider()
                ?? throw new UnauthorizedAccessException("The Service image is not Authenticode trusted.");
            var frame = IpcFrameCodec.Encode(IpcHandshake.Create(this.sessionId, Environment.ProcessId, signer));
            await this.writeGate.WaitAsync(this.cancellationToken);
            try { await this.pipeServer!.WriteAsync(frame, this.cancellationToken); }
            finally { this.writeGate.Release(); }
            this.trace.Record(IpcPhase.ServerHelloWrite);
        }

        private void ClosePipe() => this.pipeServer?.Dispose();

        private IIpcMessage? DeserializeMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("MessageType", out var typeElement)
                    || typeElement.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                var messageType = typeElement.GetString();

                // Route to the correct record type based on MessageType
                return messageType switch
                {
                    nameof(ForegroundChanged) => root.Deserialize(UIMessagesJsonContext.Default.ForegroundChanged),
                    nameof(AgentHeartbeat) => root.Deserialize(UIMessagesJsonContext.Default.AgentHeartbeat),
                    nameof(AgentCommandCompleted) => root.Deserialize(UIMessagesJsonContext.Default.AgentCommandCompleted),
                    nameof(StateSnapshot) => root.Deserialize(UIMessagesJsonContext.Default.StateSnapshot),
                    nameof(Pong) => root.Deserialize(UIMessagesJsonContext.Default.Pong),
                    nameof(GetUsageState) => root.Deserialize(UIMessagesJsonContext.Default.GetUsageState),
                    nameof(UsageStateResponse) => root.Deserialize(UIMessagesJsonContext.Default.UsageStateResponse),
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
                this.clientIdentity?.Dispose();
                this.writeGate.Dispose();
                this.disposed = true;
            }
        }

        private readonly byte[] readBuffer = new byte[BufferSize];
        private readonly SemaphoreSlim writeGate = new(1, 1);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientProcessId(
            Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint processId);
    }

    internal interface IServicePipeConnection : IDisposable
    {
        bool IsConnected { get; }
        Microsoft.Win32.SafeHandles.SafePipeHandle SafePipeHandle { get; }
        Task WaitForConnectionAsync(CancellationToken cancellationToken);
        Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken);
        Task WriteAsync(byte[] buffer, CancellationToken cancellationToken);
        void RunAsClient(Action action);
        SecurityIdentifier GetImpersonationUserSid();
    }

    private sealed class ServicePipeConnection : IServicePipeConnection
    {
        private readonly NamedPipeServerStream pipe;
        public ServicePipeConnection(NamedPipeServerStream pipe) => this.pipe = pipe;
        public bool IsConnected => this.pipe.IsConnected;
        public Microsoft.Win32.SafeHandles.SafePipeHandle SafePipeHandle => this.pipe.SafePipeHandle;
        public Task WaitForConnectionAsync(CancellationToken token) => this.pipe.WaitForConnectionAsync(token);
        public Task<int> ReadAsync(byte[] buffer, CancellationToken token) => this.pipe.ReadAsync(buffer, token).AsTask();
        public Task WriteAsync(byte[] buffer, CancellationToken token) => this.pipe.WriteAsync(buffer, token).AsTask();
        public void RunAsClient(Action action) => this.pipe.RunAsClient(() => action());
        public SecurityIdentifier GetImpersonationUserSid() => this.pipe.GetImpersonationUserSid();
        public void Dispose() => this.pipe.Dispose();
    }
}
