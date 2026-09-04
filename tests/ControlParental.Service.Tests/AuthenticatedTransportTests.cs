namespace ControlParental.Service.Tests;

using System.IO.Pipes;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Service.Interop;
using Xunit;

public sealed class AuthenticatedTransportTests
{
    [Fact]
    public async Task WindowsExtensions_ReturnsConnectedClientSidInsideImpersonation()
    {
        var pipeName = $"control-parental-sid-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
        var expectedSid = WindowsIdentity.GetCurrent().User;
        var connect = client.ConnectAsync(timeout.Token);
        await server.WaitForConnectionAsync(timeout.Token);
        await connect.WaitAsync(timeout.Token);

        SecurityIdentifier? actualSid = null;
        server.RunAsClient(() => actualSid = server.GetImpersonationUserSid());

        Assert.Equal(expectedSid, actualSid);
    }

    [Fact]
    public async Task WindowsExtensions_ReturnsNullOutsideImpersonation()
    {
        var pipeName = $"control-parental-sid-{Guid.NewGuid():N}";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, TokenImpersonationLevel.Impersonation);
        var connect = client.ConnectAsync(timeout.Token);
        await server.WaitForConnectionAsync(timeout.Token);
        await connect.WaitAsync(timeout.Token);

        Assert.Null(server.GetImpersonationUserSid());
    }

    [Fact]
    public async Task NamedPipeServer_EmitsConfiguredSessionIdInServerHello()
    {
        const int expectedSessionId = 42;
        var pipeName = $"server-hello-session-{Guid.NewGuid():N}";
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        NamedPipeServerStream? serverStream = null;
        using var server = new NamedPipeServer(
            pipeName,
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            _ => true,
            expectedSessionId,
            _ =>
            {
                serverStream = new NamedPipeServerStream(
                    $"ControlParental.{pipeName}",
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                return Task.FromResult<NamedPipeServer.IServicePipeConnection>(
                    new TestServicePipeConnection(serverStream));
            },
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "test-signer");
        using var client = new NamedPipeClientStream(".", $"ControlParental.{pipeName}", PipeDirection.InOut, PipeOptions.Asynchronous);

        var serverTask = server.StartAsync(stop.Token);
        await client.ConnectAsync(stop.Token);
        var bytes = new byte[NamedPipeServer.BufferSize];
        var bytesRead = await client.ReadAsync(bytes, stop.Token);
        var frames = new IpcFrameCodec.Decoder().Append(bytes.AsSpan(0, bytesRead));

        Assert.True(IpcHandshake.TryParse(frames.Single(), out var hello));
        Assert.Equal(expectedSessionId, hello.SessionId);

        stop.Cancel();
        serverStream?.Dispose();
        await serverTask;
    }

    [Fact]
    public async Task Server_AuthenticatesBeforeHelloAndDispatchesOnlyAuthorizedFrames()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new FakeServicePipe(stop, IpcFrameCodec.Encode(JsonSerializer.Serialize(new ForegroundChanged("child"))));
        var phases = new List<string>();
        var received = new List<IIpcMessage>();
        using var listener = new NamedPipeServer.PipeServerListener(
            "test",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            received.Add,
            () => phases.Add("disconnected"),
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => { phases.Add("authenticated"); return Task.FromResult<IReadOnlyList<string>?>(
                new[] { JsonSerializer.Serialize(new ForegroundChanged("initial")) }); },
            () => "trusted");

        await listener.StartAsync();

        Assert.Equal(new[] { "authenticated", "disconnected" }, phases);
        Assert.Equal(new[] { "initial", "child" }, received.Cast<ForegroundChanged>().Select(x => x.AppId));
        Assert.Single(pipe.Writes); // ServerHello follows authentication and dispatch setup.
    }

    private sealed class TestServicePipeConnection : NamedPipeServer.IServicePipeConnection
    {
        private readonly NamedPipeServerStream pipe;

        public TestServicePipeConnection(NamedPipeServerStream pipe) => this.pipe = pipe;

        public bool IsConnected => this.pipe.IsConnected;
        public Microsoft.Win32.SafeHandles.SafePipeHandle SafePipeHandle => this.pipe.SafePipeHandle;
        public Task WaitForConnectionAsync(CancellationToken cancellationToken) => this.pipe.WaitForConnectionAsync(cancellationToken);
        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken) => this.pipe.ReadAsync(buffer, cancellationToken).AsTask();
        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken) => this.pipe.WriteAsync(buffer, cancellationToken).AsTask();
        public void RunAsClient(Action action) => this.pipe.RunAsClient(() => action());
        public SecurityIdentifier GetImpersonationUserSid() => WindowsIdentity.GetCurrent().User!;
        public void Dispose() => this.pipe.Dispose();
    }

    private class FakeServicePipe : NamedPipeServer.IServicePipeConnection
    {
        private readonly CancellationTokenSource stop;
        private readonly byte[] readFrame;
        public FakeServicePipe(CancellationTokenSource stop, byte[] readFrame) { this.stop = stop; this.readFrame = readFrame; }
        public List<byte[]> Writes { get; } = new();
        protected CancellationTokenSource StopSource => this.stop;
        public bool IsConnected { get; private set; } = true;
        public Microsoft.Win32.SafeHandles.SafePipeHandle SafePipeHandle => new(IntPtr.Zero, false);
        public virtual Task WaitForConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            if (this.readFrame.Length == 0) return Task.FromResult(0);
            Buffer.BlockCopy(this.readFrame, 0, buffer, 0, this.readFrame.Length);
            IsConnected = false;
            stop.Cancel();
            return Task.FromResult(this.readFrame.Length);
        }
        public virtual Task WriteAsync(byte[] buffer, CancellationToken cancellationToken) { Writes.Add(buffer); return Task.CompletedTask; }
        public void RunAsClient(Action action) => action();
        public SecurityIdentifier GetImpersonationUserSid() => new(WellKnownSidType.BuiltinUsersSid, null);
        public void Dispose() => IsConnected = false;
    }
    [Fact]
    public void NativePipeBuffer_IsWithinWindowsMessagePipeRange()
    {
        Assert.InRange(
            NamedPipeServer.BufferSize,
            1,
            NamedPipeServer.MaxNativePipeBufferSize);
    }

    [Fact]
    public void NativePipeInstances_UsesTheFrameworkUnlimitedValue()
    {
        Assert.Equal(
            NamedPipeServerStream.MaxAllowedServerInstances,
            NamedPipeServer.PipeInstances);
    }

    [Fact]
    public void ProcessIdentity_ResolvesTheFinalImageFromAnOpenHandle()
    {
        using var identity = AuthenticodeSigner.Open(Process.GetCurrentProcess().Id);

        Assert.Equal(
            Path.GetFullPath(Environment.ProcessPath!),
            Path.GetFullPath(identity.ImagePath));
        Assert.False(identity.Handle.IsInvalid);
        Assert.Equal(Process.GetCurrentProcess().SessionId, identity.SessionId);
        Assert.True(identity.StartTimeUtc <= DateTime.UtcNow);
    }

    [Fact]
    public void ServiceSigner_UsesTheRawTrustResultForAnOpenIdentity()
    {
        using var identity = AuthenticodeSigner.Open(Process.GetCurrentProcess().Id);

        var signer = AuthenticodeSigner.GetSigner(identity, out var rawResult);

        Assert.Equal(signer, AuthenticodeSigner.GetSigner(Process.GetCurrentProcess().Id));
        Assert.NotEqual(int.MinValue, rawResult);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ServiceSigner_RejectsUnavailableProcess(int processId)
    {
        Assert.Null(AuthenticodeSigner.GetSigner(processId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-signed-file")]
    public void ServiceSigner_RejectsMissingOrUnsignedPath(string? path)
    {
        Assert.Null(AuthenticodeSigner.GetSigner(path));
    }

    [Fact]
    public void WinTrust_ExposesTheRawLongAndUsesZeroAsSuccess()
    {
        using var trust = new WinTrustFileInfo(Environment.ProcessPath!, Guid.NewGuid());

        Assert.Equal(trust.RawResult == 0, trust.IsSigned);
    }

    [Fact]
    public void WinTrust_UsesTheOfficialGenericVerifyV2Action()
    {
        Assert.Equal(
            new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE"),
            WinTrust.GenericVerifyV2ActionId);
    }

    [Fact]
    public void WinTrustData_MatchesTheNativePointerSizedLayout()
    {
        Assert.Equal(IntPtr.Size == 8 ? 88 : 52, Marshal.SizeOf<WinTrust.WINTRUST_DATA>());

        Assert.Equal(0, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.cbStruct)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 8 : 4, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.pPolicyCallbackData)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 16 : 8, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.pSIPClientData)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 24 : 12, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.dwUIChoice)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 40 : 24, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.pFile)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 48 : 28, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.dwStateAction)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 56 : 32, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.hWVTStateData)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 64 : 36, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.pwszURLReference)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 72 : 40, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.dwProvFlags)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 76 : 44, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.dwUIContext)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 80 : 48, Marshal.OffsetOf<WinTrust.WINTRUST_DATA>(nameof(WinTrust.WINTRUST_DATA.pSignatureSettings)).ToInt32());
    }

    [Fact]
    public void WinTrustData_ProviderFlagsDefaultToZero()
    {
        var data = new WinTrust.WINTRUST_DATA();

        Assert.Equal(0u, data.dwProvFlags);
        Assert.Equal(0u, data.dwStateAction);
        Assert.Equal(IntPtr.Zero, data.hWVTStateData);
        Assert.Equal(IntPtr.Zero, data.pwszURLReference);
    }

    [Fact]
    public void PhaseTrace_RecordsCancellationAtTheExactPhase()
    {
        var trace = new IpcPhaseTrace();

        trace.Record(IpcPhase.Connected);
        trace.CancelledAt(IpcPhase.ClientHelloWrite);

        Assert.Equal(new[] { IpcPhase.Connected, IpcPhase.ClientHelloWrite }, trace.Phases);
        Assert.Equal(IpcPhase.ClientHelloWrite, trace.CancelledPhase);
    }

    [Fact]
    public void ValidateClientSid_ReturnsFalseWhenValidatorThrows()
    {
        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        Assert.False(NamedPipeServer.ValidateClientSid(sid, _ => throw new InvalidOperationException("rejected")));
    }

    [Fact]
    public void ValidateClientSid_RejectsMissingOrUnexpectedSid()
    {
        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        Assert.False(NamedPipeServer.ValidateClientSid(null, _ => true));
        Assert.False(NamedPipeServer.ValidateClientSid(sid, _ => false));
        Assert.True(NamedPipeServer.ValidateClientSid(sid, value => value == sid.Value));
    }

    [Fact]
    public void SecurityHelpers_RejectNullInputs()
    {
        var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        Assert.Throws<ArgumentNullException>(() => NamedPipeServer.ValidateClientSid(sid, null!));
        Assert.Throws<ArgumentNullException>(() => NamedPipeServer.CreatePipeSecurity(null!));
    }

    [Fact]
    public async Task ServerSendAsync_RejectsUseBeforeStart()
    {
        using var server = new NamedPipeServer(
            "unstarted",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            sessionId: 7);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            server.SendAsync(new Ping()));
    }

    [Fact]
    public async Task Server_RejectsAuthenticationBeforeDispatchAndStopsAfterCancellation()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new FakeServicePipe(stop, Array.Empty<byte>());
        var listener = new NamedPipeServer.PipeServerListener(
            "reject",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => throw new Xunit.Sdk.XunitException("unauthorized frame was dispatched"),
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ =>
            {
                stop.Cancel();
                return Task.FromResult<IReadOnlyList<string>?>(null);
            },
            () => "trusted");

        await listener.StartAsync();

        Assert.Empty(pipe.Writes);
        listener.Dispose();
    }

    [Fact]
    public async Task Server_DefaultAuthenticationFailsClosedForUnidentifiablePipe()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new FakeServicePipe(stop, Array.Empty<byte>());
        using var listener = new NamedPipeServer.PipeServerListener(
            "default-auth-reject",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => throw new Xunit.Sdk.XunitException("unauthorized frame was dispatched"),
            () => { },
            stop.Token,
            _ =>
            {
                stop.Cancel();
                return Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe);
            },
            null,
            () => "trusted");

        await listener.StartAsync();

        Assert.False(pipe.IsConnected);
    }

    [Fact]
    public async Task Server_RejectsConnectionWhenSignerIsUnavailable()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new FakeServicePipe(stop, Array.Empty<byte>());
        using var listener = new NamedPipeServer.PipeServerListener(
            "missing-signer",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => throw new Xunit.Sdk.XunitException("unauthorized frame was dispatched"),
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ =>
            {
                stop.Cancel();
                return Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>());
            },
            () => null);

        await listener.StartAsync();

        Assert.Empty(pipe.Writes);
    }

    [Fact]
    public async Task Server_StopsAfterPipeFactoryIoFailure()
    {
        using var stop = new CancellationTokenSource();
        using var listener = new NamedPipeServer.PipeServerListener(
            "factory-io",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => { },
            () => { },
            stop.Token,
            _ =>
            {
                stop.Cancel();
                return Task.FromException<NamedPipeServer.IServicePipeConnection>(new IOException("closed"));
            },
            null,
            () => "trusted");

        await listener.StartAsync();
        Assert.False(listener.IsConnected);
    }

    [Fact]
    public async Task Server_StopsAfterPipeFactoryUnexpectedFailure()
    {
        using var stop = new CancellationTokenSource();
        using var listener = new NamedPipeServer.PipeServerListener(
            "factory-unexpected",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => { },
            () => { },
            stop.Token,
            _ =>
            {
                stop.Cancel();
                return Task.FromException<NamedPipeServer.IServicePipeConnection>(new InvalidOperationException("failed"));
            },
            null,
            () => "trusted");

        await listener.StartAsync();
        Assert.False(listener.IsConnected);
    }

    [Fact]
    public async Task Server_SendBeforeConnectionDoesNotWrite()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new FakeServicePipe(stop, Array.Empty<byte>());
        using var listener = new NamedPipeServer.PipeServerListener(
            "send-before-connection",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => { },
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            null,
            () => "trusted");

        await listener.SendAsync(new Ping());

        Assert.Empty(pipe.Writes);
    }

    [Fact]
    public async Task Server_IgnoresMalformedAndUnknownFramesWithoutDispatch()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new FakeServicePipe(
            stop,
            IpcFrameCodec.Encode("{\"MessageType\":\"Unknown\"}")
                .Concat(IpcFrameCodec.Encode("not-json"))
                .ToArray());
        var received = new List<IIpcMessage>();
        using var listener = new NamedPipeServer.PipeServerListener(
            "malformed",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            received.Add,
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "trusted");

        await listener.StartAsync();

        Assert.Empty(received);
    }

    [Fact]
    public async Task Server_IgnoresObjectWithoutMessageType()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new FakeServicePipe(stop, IpcFrameCodec.Encode("{}"));
        using var listener = new NamedPipeServer.PipeServerListener(
            "missing-message-type",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => throw new Xunit.Sdk.XunitException("frame was dispatched"),
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "trusted");

        await listener.StartAsync();
        Assert.Single(pipe.Writes);
    }

    [Fact]
    public async Task Server_DispatchesEachKnownManagedMessageShape()
    {
        using var stop = new CancellationTokenSource();
        var messages = new IIpcMessage[]
        {
            new AgentHeartbeat("agent", 1, false),
            new StateSnapshot("app", false, 1),
            new GetUsageState(),
            new Pong(),
        };
        var frames = messages
            .Select(message => IpcFrameCodec.Encode(JsonSerializer.Serialize(message)))
            .SelectMany(bytes => bytes)
            .ToArray();
        var pipe = new FakeServicePipe(stop, frames);
        var received = new List<IIpcMessage>();
        using var listener = new NamedPipeServer.PipeServerListener(
            "known-messages",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            received.Add,
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "trusted");

        await listener.StartAsync();

        Assert.Equal(messages.Select(message => message.MessageType), received.Select(message => message.MessageType));
    }

    [Fact]
    public async Task Server_DispatchesUsageStateResponseFromAnAuthenticatedFrame()
    {
        using var stop = new CancellationTokenSource();
        var response = new UsageStateResponse(null, null, false, Array.Empty<GrantInfo>(), EnforcementLevel.Standard, Array.Empty<ActiveIssue>());
        var pipe = new FakeServicePipe(stop, IpcFrameCodec.Encode(JsonSerializer.Serialize(response)));
        var received = new List<IIpcMessage>();
        using var listener = new NamedPipeServer.PipeServerListener(
            "usage-response",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            received.Add,
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "trusted");

        await listener.StartAsync();

        Assert.IsType<UsageStateResponse>(Assert.Single(received));
    }

    [Fact]
    public async Task Server_ClosesConnectionOnReadIoFailure()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new ThrowingReadServicePipe(stop, new IOException("closed"));
        using var listener = CreateListener("io-failure", pipe, stop);

        await listener.StartAsync();

        Assert.False(pipe.IsConnected);
    }

    [Fact]
    public async Task Server_ClosesConnectionOnReadCancellation()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new ThrowingReadServicePipe(stop, new OperationCanceledException());
        using var listener = CreateListener("cancel-failure", pipe, stop);

        await listener.StartAsync();

        Assert.False(pipe.IsConnected);
    }

    [Fact]
    public async Task ServerFacade_ForwardsMessagesAndDisconnectedEvents()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new FakeServicePipe(
            stop,
            IpcFrameCodec.Encode(JsonSerializer.Serialize(new ForegroundChanged("facade"))));
        var received = new List<IIpcMessage>();
        var disconnected = 0;
        using var server = new NamedPipeServer(
            "facade",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "trusted");
        server.MessageReceived += received.Add;
        server.Disconnected += () => disconnected++;

        await server.StartAsync(stop.Token);
        await server.StopAsync();

        Assert.Equal("facade", Assert.IsType<ForegroundChanged>(Assert.Single(received)).AppId);
        Assert.Equal(1, disconnected);
        Assert.False(server.IsConnected);
    }

    [Fact]
    public async Task ServerFacade_SerializesSendThroughStartedListener()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new BlockingWriteServicePipe(stop);
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pipe.Connected = connected;
        using var server = new NamedPipeServer(
            "facade-send",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "trusted");

        var start = server.StartAsync(stop.Token);
        await connected.Task;
        pipe.ReleaseWrites();
        await server.SendAsync(new Ping());
        stop.Cancel();
        await start;

        Assert.Equal(2, pipe.Writes.Count);
    }

    [Fact]
    public void ServerFacade_CreatesRestrictedPipeSecurity()
    {
        var security = NamedPipeServer.CreatePipeSecurity(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));

        Assert.NotNull(security);
    }

    private static NamedPipeServer.PipeServerListener CreateListener(
        string name,
        FakeServicePipe pipe,
        CancellationTokenSource stop)
        => new(
            name,
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => { },
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "trusted");

    [Fact]
    public async Task Server_SerializesConcurrentWritesPerConnection()
    {
        using var stop = new CancellationTokenSource();
        var pipe = new BlockingWriteServicePipe(stop);
        var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pipe.Connected = connected;
        using var listener = new NamedPipeServer.PipeServerListener(
            "serialized",
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            _ => true,
            7,
            _ => { },
            () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeServer.IServicePipeConnection>(pipe),
            _ => Task.FromResult<IReadOnlyList<string>?>(Array.Empty<string>()),
            () => "trusted");

        var start = listener.StartAsync();
        await connected.Task;
        var sends = Enumerable.Range(0, 4)
            .Select(index => listener.SendAsync(new Ping()))
            .ToArray();
        pipe.ReleaseWrites();
        await Task.WhenAll(sends);
        stop.Cancel();
        await start;

        Assert.Equal(5, pipe.Writes.Count);
        Assert.Equal(1, pipe.MaximumConcurrentWrites);
    }

    private sealed class BlockingWriteServicePipe : FakeServicePipe
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int activeWrites;

        public BlockingWriteServicePipe(CancellationTokenSource stop)
            : base(stop, Array.Empty<byte>())
        {
        }

        public TaskCompletionSource? Connected { get; set; }

        public int MaximumConcurrentWrites { get; private set; }

        public void ReleaseWrites() => this.release.TrySetResult();

        public override async Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref this.activeWrites);
            this.MaximumConcurrentWrites = Math.Max(this.MaximumConcurrentWrites, active);
            this.Writes.Add(buffer);
            try
            {
                await this.release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref this.activeWrites);
            }
        }

        public override Task WaitForConnectionAsync(CancellationToken cancellationToken)
        {
            this.Connected?.TrySetResult();
            return Task.CompletedTask;
        }

        public override async Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

    private sealed class ThrowingReadServicePipe : FakeServicePipe
    {
        private readonly Exception exception;

        public ThrowingReadServicePipe(CancellationTokenSource stop, Exception exception)
            : base(stop, Array.Empty<byte>())
        {
            this.exception = exception;
        }

        public override Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            this.StopSource.Cancel();
            return Task.FromException<int>(this.exception);
        }
    }

    }
