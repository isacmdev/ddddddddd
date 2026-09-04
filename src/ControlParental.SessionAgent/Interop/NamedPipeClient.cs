// <copyright file="NamedPipeClient.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent.Interop;

using System.IO.Pipes;
using System.Text.Json;
using ControlParental.Domain;

/// <summary>
/// Named pipe client for IPC with the Service.
/// Runs in the Session Agent (interactive session of the child).
/// </summary>
public sealed class NamedPipeClient : IIpcChannel, IDisposable
{
    private const string PipeNamePrefix = "ControlParental";
    private const int BufferSize = 8192;
    private readonly string pipeName;
    private readonly CancellationTokenSource internalCts;
    private readonly Func<CancellationToken, Task<IClientPipeConnection>> connect;
    private readonly Func<IClientPipeConnection, bool> authenticateServer;
    private readonly Func<string?> signerProvider;
    private IClientPipeConnection? pipeClient;
    private ProcessIdentity? serverIdentity;
    private readonly IpcPhaseTrace trace = new();
    private uint serverProcessId;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NamedPipeClient"/> class.
    /// </summary>
    /// <param name="pipeName">The pipe name (without prefix).</param>
    public NamedPipeClient(string pipeName)
        : this(pipeName, null, null, null, 0)
    {
    }

    internal NamedPipeClient(
        string pipeName,
        Func<CancellationToken, Task<IClientPipeConnection>>? connect,
        Func<IClientPipeConnection, bool>? authenticateServer,
        Func<string?>? signerProvider,
        uint serverProcessId = 0)
    {
        this.pipeName = $"{PipeNamePrefix}.{pipeName}";
        this.internalCts = new CancellationTokenSource();
        this.connect = connect ?? this.ConnectAsync;
        this.authenticateServer = authenticateServer ?? (_ => this.AuthenticateServer());
        this.signerProvider = signerProvider ?? (() => AuthenticodeSigner.GetSigner(Environment.ProcessPath));
        this.serverProcessId = serverProcessId;
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
                this.pipeClient = await this.connect(linkedCts.Token);
                await this.pipeClient.ConnectAsync(linkedCts.Token);
                this.trace.Record(IpcPhase.Connected);

                if (this.pipeClient.IsConnected && this.authenticateServer(this.pipeClient))
                {
                    await this.SendHandshakeAsync(linkedCts.Token);
                    // Start reading messages
                    _ = this.ReadMessagesAsync(linkedCts.Token);
                    return;
                }
            }
            catch (IOException)
            {
            }
            catch (TimeoutException)
            {
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

            var typeInfo = UIMessagesJsonContext.Default.GetTypeInfo(message.GetType())
                ?? throw new InvalidOperationException("Unsupported IPC message type.");
            var json = JsonSerializer.Serialize(message, typeInfo);
            await this.WriteFrameAsync(json, cancellationToken);
    }

    private async Task ReadMessagesAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        var decoder = new IpcFrameCodec.Decoder();
        var serverAuthenticated = false;

        try
        {
            while (this.pipeClient?.IsConnected ?? false)
            {
                var bytesRead = await this.pipeClient.ReadAsync(buffer, cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                foreach (var json in decoder.Append(buffer.AsSpan(0, bytesRead)))
                {
                    if (!serverAuthenticated)
                    {
                        if (!IpcHandshake.TryParse(json, out var handshake) ||
                            handshake.ProcessId != this.serverProcessId)
                        {
                            throw new UnauthorizedAccessException("The Service handshake was rejected.");
                        }

                        var signer = this.serverIdentity is null ? null : AuthenticodeSigner.GetSigner(this.serverIdentity, out _);
                        if (signer is null || !IpcHandshake.IsAuthorized(
                            handshake, System.Diagnostics.Process.GetCurrentProcess().SessionId, (int)this.serverProcessId, signer))
                        {
                            throw new UnauthorizedAccessException("The Service signer was rejected.");
                        }

                        serverAuthenticated = true;
                        this.trace.Record(IpcPhase.ServerHelloRead);
                        this.trace.Record(IpcPhase.Authenticated);
                        continue;
                    }

                    var message = this.DeserializeMessage(json);
                    if (message != null) this.MessageReceived?.Invoke(message);
                }
            }
        }
        catch (IOException)
        {
            // Connection closed
        }
        catch (OperationCanceledException)
        {
            this.trace.CancelledAt(IpcPhase.ServerHelloRead);
        }

        this.Disconnected?.Invoke();
    }

    private bool AuthenticateServer()
    {
        if (this.pipeClient is null || !GetNamedPipeServerProcessId(this.pipeClient.SafePipeHandle, out var serverPid))
        {
            return false;
        }

        this.serverProcessId = serverPid;
        this.trace.Record(IpcPhase.ServerPid);
        this.serverIdentity?.Dispose();
        this.serverIdentity = AuthenticodeSigner.Open((int)serverPid);
        this.trace.Record(IpcPhase.ProcessHandle);
        this.trace.Record(IpcPhase.Path);
        var expectedSigner = AuthenticodeSigner.GetSigner(Environment.ProcessPath);
        var actualSigner = AuthenticodeSigner.GetSigner(this.serverIdentity, out _);
        this.trace.Record(IpcPhase.AuthResult);
        return serverPid > 0 && expectedSigner is not null &&
            string.Equals(expectedSigner, actualSigner, StringComparison.OrdinalIgnoreCase);
    }

    private Task<IClientPipeConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        IClientPipeConnection pipe = new ClientPipeConnection(CreateClientPipe(this.pipeName));
        return Task.FromResult(pipe);
    }

    internal static NamedPipeClientStream CreateClientPipeForTest(string pipeName)
        => CreateClientPipe(pipeName);

    private static NamedPipeClientStream CreateClientPipe(string pipeName)
        => new(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous,
            System.Security.Principal.TokenImpersonationLevel.Impersonation);

    private async Task SendHandshakeAsync(CancellationToken cancellationToken)
    {
            var signer = this.signerProvider()
            ?? throw new UnauthorizedAccessException("The SessionAgent image is not Authenticode trusted.");
        await this.WriteFrameAsync(
            IpcHandshake.Create(System.Diagnostics.Process.GetCurrentProcess().SessionId, Environment.ProcessId, signer),
            cancellationToken);
        this.trace.Record(IpcPhase.ClientHelloWrite);
    }

    private async Task WriteFrameAsync(string json, CancellationToken cancellationToken)
    {
        if (this.pipeClient is null || !this.pipeClient.IsConnected) return;
        var frame = IpcFrameCodec.Encode(json);
        await this.writeGate.WaitAsync(cancellationToken);
        try { await this.pipeClient.WriteAsync(frame, cancellationToken); }
        finally { this.writeGate.Release(); }
    }

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

            return messageType switch
            {
                nameof(ShowOverlay) => root.Deserialize(UIMessagesJsonContext.Default.ShowOverlay),
                nameof(HideOverlay) => root.Deserialize(UIMessagesJsonContext.Default.HideOverlay),
                nameof(ShowWarning) => root.Deserialize(UIMessagesJsonContext.Default.ShowWarning),
                nameof(LockWorkstation) => root.Deserialize(UIMessagesJsonContext.Default.LockWorkstation),
                nameof(AgentAuthority) => root.Deserialize(UIMessagesJsonContext.Default.AgentAuthority),
                nameof(AgentCommandRequest) => root.Deserialize(UIMessagesJsonContext.Default.AgentCommandRequest),
                nameof(RequestStateSnapshot) => root.Deserialize(UIMessagesJsonContext.Default.RequestStateSnapshot),
                nameof(Ping) => root.Deserialize(UIMessagesJsonContext.Default.Ping),
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
            this.serverIdentity?.Dispose();
            this.internalCts.Dispose();
            this.writeGate.Dispose();
            this.disposed = true;
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(
        Microsoft.Win32.SafeHandles.SafePipeHandle pipe, out uint processId);
    internal interface IClientPipeConnection : IDisposable
    {
        bool IsConnected { get; }
        Microsoft.Win32.SafeHandles.SafePipeHandle SafePipeHandle { get; }
        Task ConnectAsync(CancellationToken cancellationToken);
        Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken);
        Task WriteAsync(byte[] buffer, CancellationToken cancellationToken);
    }

    private sealed class ClientPipeConnection : IClientPipeConnection
    {
        private readonly NamedPipeClientStream pipe;
        public ClientPipeConnection(NamedPipeClientStream pipe) => this.pipe = pipe;
        public bool IsConnected => this.pipe.IsConnected;
        public Microsoft.Win32.SafeHandles.SafePipeHandle SafePipeHandle => this.pipe.SafePipeHandle;
        public Task ConnectAsync(CancellationToken token) => this.pipe.ConnectAsync(token);
        public Task<int> ReadAsync(byte[] buffer, CancellationToken token) => this.pipe.ReadAsync(buffer, token).AsTask();
        public Task WriteAsync(byte[] buffer, CancellationToken token) => this.pipe.WriteAsync(buffer, token).AsTask();
        public void Dispose() => this.pipe.Dispose();
    }
}
