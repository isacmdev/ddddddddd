namespace ControlParental.SessionAgent.Tests;

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.SessionAgent.Interop;
using Xunit;

public sealed class NamedPipeClientSeamTests
{
    [Fact]
    public void DefaultPipeFactoryRequestsImpersonationTokenLevel()
    {
        using var pipe = NamedPipeClient.CreateClientPipeForTest("impersonation");

        Assert.Equal(
            System.Security.Principal.TokenImpersonationLevel.Impersonation,
            (System.Security.Principal.TokenImpersonationLevel)typeof(System.IO.Pipes.NamedPipeClientStream).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Single(field => field.FieldType == typeof(System.Security.Principal.TokenImpersonationLevel)).GetValue(pipe)!);
    }

    [Fact]
    public async Task Client_SendWithoutConnectionDoesNotUseRawFallback()
    {
        using var client = new NamedPipeClient("not-connected", null, _ => true, () => "trusted", 1);

        await client.SendAsync(new Ping());

        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task Client_RetryCancellationStopsConnectLoop()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new NamedPipeClient(
            "retry-cancelled",
            _ =>
            {
                cancellation.Cancel();
                return Task.FromException<NamedPipeClient.IClientPipeConnection>(new IOException("closed"));
            },
            _ => true,
            () => "trusted",
            1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.StartAsync(cancellation.Token));
    }

    [Fact]
    public async Task Client_RetryCancellationStopsTimeoutLoop()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new NamedPipeClient(
            "timeout-cancelled",
            _ =>
            {
                cancellation.Cancel();
                return Task.FromException<NamedPipeClient.IClientPipeConnection>(new TimeoutException("late"));
            },
            _ => true,
            () => "trusted",
            1);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.StartAsync(cancellation.Token));
    }

    [Fact]
    public async Task Client_SendsHelloBeforeDispatchingAuthorizedServerMessages()
    {
        var serverPid = (uint)Process.GetCurrentProcess().Id;
        var serverHello = IpcFrameCodec.Encode(
            IpcHandshake.Create(Process.GetCurrentProcess().SessionId, (int)serverPid, "trusted"));
        var serverMessage = IpcFrameCodec.Encode(System.Text.Json.JsonSerializer.Serialize(new Ping()));
        var pipe = new FakeClientPipe(serverHello.Concat(serverMessage).ToArray());
        using var client = new NamedPipeClient(
            "test",
            _ => Task.FromResult<NamedPipeClient.IClientPipeConnection>(pipe),
            _ => true,
            () => "trusted",
            serverPid);
        await client.StartAsync();
        await Task.Delay(50);

        Assert.Single(pipe.Writes);
        var helloJson = new IpcFrameCodec.Decoder().Append(pipe.Writes[0]).Single();
        Assert.Contains(nameof(IpcHandshake), helloJson);
    }

    [Fact]
    public async Task Client_SerializesHandshakeAndPayloadWrites()
    {
        var serverPid = (uint)Process.GetCurrentProcess().Id;
        var serverHello = IpcFrameCodec.Encode(
            IpcHandshake.Create(Process.GetCurrentProcess().SessionId, (int)serverPid, "trusted"));
        var pipe = new FakeClientPipe(serverHello, holdRead: true);
        using var client = new NamedPipeClient(
            "serialized",
            _ => Task.FromResult<NamedPipeClient.IClientPipeConnection>(pipe),
            _ => true,
            () => "trusted",
            serverPid);

        await client.StartAsync();
        await client.SendAsync(new Ping());

        Assert.Equal(2, pipe.Writes.Count);
        Assert.Equal(nameof(IpcHandshake),
            JsonDocument.Parse(new IpcFrameCodec.Decoder().Append(pipe.Writes[0]).Single())
                .RootElement.GetProperty("MessageType").GetString());
        Assert.Equal(nameof(Ping),
            JsonDocument.Parse(new IpcFrameCodec.Decoder().Append(pipe.Writes[1]).Single())
                .RootElement.GetProperty("MessageType").GetString());
        await client.StopAsync();
    }

    [Fact]
    public async Task Client_CancelledPayloadWriteDoesNotBypassWriteGate()
    {
        var serverPid = (uint)Process.GetCurrentProcess().Id;
        var serverHello = IpcFrameCodec.Encode(
            IpcHandshake.Create(Process.GetCurrentProcess().SessionId, (int)serverPid, "trusted"));
        var pipe = new FakeClientPipe(serverHello, holdRead: true);
        using var client = new NamedPipeClient(
            "cancelled",
            _ => Task.FromResult<NamedPipeClient.IClientPipeConnection>(pipe),
            _ => true,
            () => "trusted",
            serverPid);

        await client.StartAsync();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SendAsync(new Ping(), cancelled.Token));
        Assert.Single(pipe.Writes);
    }

    [Fact]
    public async Task Client_RejectsUntrustedLocalSignerBeforeHandshake()
    {
        var pipe = new FakeClientPipe(Array.Empty<byte>());
        using var client = new NamedPipeClient(
            "untrusted-signer",
            _ => Task.FromResult<NamedPipeClient.IClientPipeConnection>(pipe),
            _ => true,
            () => null,
            (uint)Process.GetCurrentProcess().Id);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => client.StartAsync());
        Assert.Empty(pipe.Writes);
    }

    [Fact]
    public async Task Client_RejectsServerHandshakeWithWrongProcessIdentity()
    {
        var serverHello = IpcFrameCodec.Encode(
            IpcHandshake.Create(Process.GetCurrentProcess().SessionId, Process.GetCurrentProcess().Id + 1, "trusted"));
        var pipe = new FakeClientPipe(serverHello);
        using var client = new NamedPipeClient(
            "wrong-server",
            _ => Task.FromResult<NamedPipeClient.IClientPipeConnection>(pipe),
            _ => true,
            () => "trusted",
            (uint)Process.GetCurrentProcess().Id);

        await client.StartAsync();
        await Task.Delay(25);

        Assert.Single(pipe.Writes);
    }

    private sealed class FakeClientPipe : NamedPipeClient.IClientPipeConnection
    {
        private readonly byte[] serverBytes;
        private readonly bool holdRead;
        private int offset;
        public FakeClientPipe(byte[] serverBytes, bool holdRead = false)
        {
            this.serverBytes = serverBytes;
            this.holdRead = holdRead;
        }
        public List<byte[]> Writes { get; } = new();
        public bool IsConnected { get; private set; } = true;
        public Microsoft.Win32.SafeHandles.SafePipeHandle SafePipeHandle => new(IntPtr.Zero, false);
        public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            if (this.offset >= this.serverBytes.Length)
            {
                if (this.holdRead)
                {
                    return WaitForCancellationAsync(cancellationToken);
                }

                IsConnected = false;
                return Task.FromResult(0);
            }
            var count = Math.Min(buffer.Length, this.serverBytes.Length - this.offset);
            Buffer.BlockCopy(this.serverBytes, this.offset, buffer, 0, count);
            this.offset += count;
            return Task.FromResult(count);
        }
        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            Writes.Add(buffer);
            return Task.CompletedTask;
        }
        public void Dispose() => IsConnected = false;

        private static async Task<int> WaitForCancellationAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
}
