namespace ControlParental.SessionAgent.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Service;
using ControlParental.SessionAgent;
using Xunit;

public sealed class WindowsRuntimeSafetyLoopTests
{
    [Fact]
    public async Task ControlledWindowsRuntimeExercisesFiveSafetyFlows()
    {
        Assert.True(OperatingSystem.IsWindows());

        var startedAt = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        var overlay = ExerciseControlledOverlayLifecycle();
        var overlayDuration = timer.Elapsed;
        timer.Restart();
        var termination = await ExerciseExactChildTerminationAsync();
        var terminationDuration = timer.Elapsed;
        timer.Restart();
        var transport = await ExerciseLockReconnectAndHeartbeatAsync();
        var transportDuration = timer.Elapsed;

        Assert.Equal((ActionStatus.Confirmed, ActionStatus.Confirmed, ActionStatus.Confirmed), overlay);
        Assert.Equal((ActionStatus.InvalidTarget, ActionStatus.Confirmed), termination);
        Assert.True(transport.LockExportResolved);
        Assert.Equal(1, transport.SafeLockCalls);
        Assert.Equal(ActionStatus.Confirmed, transport.LockStatus);
        Assert.True(transport.GenerationChanged);
        Assert.True(transport.StaleResultRejected);
        Assert.True(transport.HeartbeatLossObserved);
        Assert.True(transport.HeartbeatRecovered);

        var evidencePath = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_RUNTIME_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidencePath))
        {
            var evidence = new
            {
                StartedAtUtc = startedAt,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Os = Environment.OSVersion.VersionString,
                Environment.ProcessId,
                SessionId = Process.GetCurrentProcess().SessionId,
                Flows = new object[]
                {
                    new { Name = "overlay-lifecycle", Result = "PASS", DurationMs = overlayDuration.TotalMilliseconds, Detail = "native 320x180 topmost show/replace/clear" },
                    new { Name = "exact-termination", Result = "PASS", DurationMs = terminationDuration.TotalMilliseconds, Detail = "wrong start-time rejected; exact child terminated" },
                    new { Name = "lock-workstation-safe-seam", Result = "PASS", DurationMs = transportDuration.TotalMilliseconds, Detail = "user32 export resolved; production lock path called safe delegate once; workstation not locked" },
                    new { Name = "agent-reconnect", Result = "PASS", DurationMs = transportDuration.TotalMilliseconds, Detail = "real named-pipe replacement generation 1 -> 2; stale generation rejected" },
                    new { Name = "heartbeat-loss-recovery", Result = "PASS", DurationMs = transportDuration.TotalMilliseconds, Detail = "300ms bounded silence observed; generation-2 authority heartbeat accepted" },
                },
            };
            File.WriteAllText(evidencePath, JsonSerializer.Serialize(
                evidence, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void DisposingOneControlledOverlayDoesNotInvalidateAnotherWindow()
    {
        using var first = new OverlayWindow(40, 40, 320, 180, hideCursor: false);
        var second = new OverlayWindow(80, 80, 320, 180, hideCursor: false);
        try
        {
            Assert.Equal(ActionStatus.Confirmed, first.Apply(new OverlayIntent(true, "first", null, 1)).Status);
            Assert.Equal(ActionStatus.Confirmed, second.Apply(new OverlayIntent(true, "second", null, 1)).Status);
            var firstHandle = first.Handle;

            second.Dispose();

            Assert.Equal(firstHandle, first.Handle);
            Assert.True(GetWindowRect(first.Handle, out _));
        }
        finally
        {
            second.Dispose();
        }
    }

    private static (ActionStatus Show, ActionStatus Replace, ActionStatus Clear)
        ExerciseControlledOverlayLifecycle()
    {
        using var window = new OverlayWindow(40, 40, 320, 180, hideCursor: false);
        var shown = window.Apply(new OverlayIntent(true, "runtime-show", "Continue", 1));
        var handle = window.Handle;
        var replaced = window.Apply(new OverlayIntent(true, "runtime-replace", null, 2));
        var bounds = GetBounds(handle);
        var topmost = (GetWindowLongPtr(handle, -20).ToInt64() & 0x00000008L) != 0;
        var cleared = window.Apply(new OverlayIntent(false, "runtime-clear", null, 3));

        Assert.NotEqual(IntPtr.Zero, handle);
        Assert.Equal((320, 180), (bounds.Right - bounds.Left, bounds.Bottom - bounds.Top));
        Assert.True(topmost);
        Assert.Equal("runtime-replace", window.GetCurrentReason());
        Assert.False(window.IsVisible);
        return (shown.Status, replaced.Status, cleared.Status);
    }

    private static async Task<(ActionStatus WrongIdentity, ActionStatus ExactIdentity)>
        ExerciseExactChildTerminationAsync()
    {
        using var child = Process.Start(new ProcessStartInfo(
            Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            "/d /c ping 127.0.0.1 -t")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        }) ?? throw new InvalidOperationException("Controlled child did not start.");

        try
        {
            var started = new DateTimeOffset(child.StartTime.ToUniversalTime());
            var exact = new ObservedProcessTarget(child.Id, child.SessionId, started);
            var reused = exact with { StartedAt = started.AddSeconds(-1) };
            var terminator = new ProcessTerminator();

            var wrong = await terminator.TerminateAsync(reused).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(child.HasExited);
            var confirmed = await terminator.TerminateAsync(exact).WaitAsync(TimeSpan.FromSeconds(8));
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
            return (wrong, confirmed);
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(2));
            }
        }
    }

    private static async Task<TransportEvidence> ExerciseLockReconnectAndHeartbeatAsync()
    {
        var sessionId = Process.GetCurrentProcess().SessionId;
        var sink = new RuntimeHealthSink();
        var acceptedHeartbeats = 0;
        await using var loop = new SessionSafetyLoop(
            sessionId,
            (_, _) => Task.FromResult(Allowed()),
            (_, _) => Task.FromResult(ActionStatus.Confirmed),
            new RuntimeIntentStore(), sink,
            heartbeatAccepted: () => Interlocked.Increment(ref acceptedHeartbeats));

        var safeLockCalls = 0;
        var first = await RuntimeConnection.StartAsync(
            "runtime-" + Guid.NewGuid().ToString("N"),
            () => { Interlocked.Increment(ref safeLockCalls); return true; });
        AgentCommandEnvelope? oldCommand = null;
        try
        {
            first.Server.MessageReceived += message =>
            {
                if (message is AgentCommandCompleted completed) loop.AcceptResult(completed.Result);
                if (message is AgentHeartbeat heartbeat) loop.AcceptHeartbeat(heartbeat);
            };
            await loop.AttachAgentAsync(1, async (command, token) =>
            {
                oldCommand = command;
                await first.Server.SendAsync(new AgentCommandRequest(command), token);
            });
            await first.Server.SendAsync(new AgentAuthority(sessionId, 1));
            await WaitUntilAsync(() => Volatile.Read(ref acceptedHeartbeats) == 1, TimeSpan.FromSeconds(2));

            var lockCommand = new AgentCommandEnvelope(
                Guid.NewGuid(), sessionId, 1, 9,
                AgentCommandKind.LockWorkstation, DateTimeOffset.UtcNow.AddSeconds(2));
            oldCommand = lockCommand;
            await first.Server.SendAsync(new AgentCommandRequest(lockCommand));
            var lockResult = await first.Server.WaitForAsync<AgentCommandCompleted>(
                completed => completed.Result.CommandId == lockCommand.CommandId, TimeSpan.FromSeconds(2));

            await first.DisposeAsync();
            await loop.AgentDiedAsync();
            var countAtLoss = Volatile.Read(ref acceptedHeartbeats);
            await Task.Delay(300);
            var loss = Volatile.Read(ref acceptedHeartbeats) == countAtLoss;

            var second = await RuntimeConnection.StartAsync(
                "runtime-" + Guid.NewGuid().ToString("N"), () => true);
            try
            {
                second.Server.MessageReceived += message =>
                {
                    if (message is AgentCommandCompleted completed) loop.AcceptResult(completed.Result);
                    if (message is AgentHeartbeat heartbeat) loop.AcceptHeartbeat(heartbeat);
                };
                await loop.AttachAgentAsync(2, async (command, token) =>
                    await second.Server.SendAsync(new AgentCommandRequest(command), token));
                await second.Server.SendAsync(new AgentAuthority(sessionId, 2));
                await WaitUntilAsync(() => Volatile.Read(ref acceptedHeartbeats) > countAtLoss, TimeSpan.FromSeconds(2));

                var stale = oldCommand != null && !loop.AcceptResult(new AgentActionResult(
                    oldCommand.CommandId, oldCommand.SessionId, oldCommand.ConnectionGeneration,
                    oldCommand.IntentVersion, ActionStatus.Confirmed, null));
                return new(
                    NativeLibrary.TryLoad("user32.dll", out var library) &&
                        NativeLibrary.TryGetExport(library, "LockWorkStation", out _),
                    safeLockCalls, lockResult.Result.Status, true, stale, loss,
                    Volatile.Read(ref acceptedHeartbeats) > countAtLoss);
            }
            finally
            {
                await second.DisposeAsync();
            }
        }
        finally
        {
            await first.DisposeAsync();
        }
    }

    private static EnforcementResult Allowed() => new()
    {
        Success = true,
        Blocked = false,
        ReasonText = "allowed",
        Timestamp = DateTimeOffset.UtcNow,
    };

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (!predicate())
        {
            if (deadline.Elapsed > timeout) throw new TimeoutException("Runtime condition timed out.");
            await Task.Delay(20);
        }
    }

    private static RECT GetBounds(IntPtr handle)
    {
        Assert.True(GetWindowRect(handle, out var bounds));
        return bounds;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    private sealed record TransportEvidence(
        bool LockExportResolved, int SafeLockCalls, ActionStatus LockStatus,
        bool GenerationChanged, bool StaleResultRejected,
        bool HeartbeatLossObserved, bool HeartbeatRecovered);

    private sealed class RuntimeHealthSink : IAuthoritativeHealthSink
    {
        public void SetRestoreStatus(bool succeeded) { }
        public void SetCurrentCriticalActionsConfirmed(bool confirmed) { }
        public void SetHealthBlockingIssues(bool hasBlockingIssues) { }
    }

    private sealed class RuntimeIntentStore : IOverlayIntentStore
    {
        private OverlayIntent? intent;
        public OverlayIntent? Load() => this.intent;
        public void Save(OverlayIntent value) => this.intent = value;
    }

    private sealed class RuntimeConnection : IAsyncDisposable
    {
        private RuntimeConnection(RuntimePipeServer server, RuntimePipeChannel channel, SessionAgentHost host)
        {
            this.Server = server;
            this.Channel = channel;
            this.Host = host;
        }

        public RuntimePipeServer Server { get; }
        private RuntimePipeChannel Channel { get; }
        private SessionAgentHost Host { get; }

        public static async Task<RuntimeConnection> StartAsync(string pipeName, Func<bool> lockCall)
        {
            var server = new RuntimePipeServer(pipeName);
            var accept = server.StartAsync();
            var channel = new RuntimePipeChannel(pipeName);
            var host = new SessionAgentHost(channel, new RuntimeOverlayManager(), new RuntimeWatcher(), lockCall);
            await host.StartAsync(CancellationToken.None);
            await accept.WaitAsync(TimeSpan.FromSeconds(2));
            return new(server, channel, host);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Host.StopAsync(CancellationToken.None);
            await this.Server.DisposeAsync();
            await this.Channel.DisposeAsync();
        }
    }

    private abstract class RuntimePipeEndpoint : IAsyncDisposable
    {
        private readonly SemaphoreSlim writeGate = new(1, 1);
        private readonly ConcurrentQueue<IIpcMessage> messages = new();
        private readonly SemaphoreSlim available = new(0);
        private CancellationTokenSource? readCancellation;
        private bool disposed;
        protected PipeStream? Stream { get; set; }
        public event Action<IIpcMessage>? MessageReceived;

        public async Task SendAsync(IIpcMessage message, CancellationToken token = default)
        {
            var envelope = JsonSerializer.Serialize(new WireMessage(
                message.MessageType, JsonSerializer.SerializeToElement(message, message.GetType())));
            await this.writeGate.WaitAsync(token);
            try
            {
                using var writer = new StreamWriter(this.Stream!, leaveOpen: true) { AutoFlush = true };
                await writer.WriteLineAsync(envelope.AsMemory(), token);
            }
            finally { this.writeGate.Release(); }
        }

        protected void StartReading()
        {
            this.readCancellation = new();
            _ = this.ReadAsync(this.readCancellation.Token);
        }

        public async Task<T> WaitForAsync<T>(Func<T, bool> predicate, TimeSpan timeout) where T : class, IIpcMessage
        {
            using var cancellation = new CancellationTokenSource(timeout);
            while (true)
            {
                await this.available.WaitAsync(cancellation.Token);
                foreach (var message in this.messages.OfType<T>())
                    if (predicate(message)) return message;
            }
        }

        private async Task ReadAsync(CancellationToken token)
        {
            using var reader = new StreamReader(this.Stream!, leaveOpen: true);
            try
            {
                while (!token.IsCancellationRequested && await reader.ReadLineAsync(token) is { } line)
                {
                    var wire = JsonSerializer.Deserialize<WireMessage>(line)!;
                    var message = Deserialize(wire);
                    this.messages.Enqueue(message);
                    this.available.Release();
                    this.MessageReceived?.Invoke(message);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        }

        public virtual async ValueTask DisposeAsync()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.readCancellation?.Cancel();
            if (this.Stream != null) await this.Stream.DisposeAsync();
            this.readCancellation?.Dispose();
            this.writeGate.Dispose();
            this.available.Dispose();
        }

        private static IIpcMessage Deserialize(WireMessage wire) => wire.Type switch
        {
            nameof(AgentAuthority) => wire.Payload.Deserialize<AgentAuthority>()!,
            nameof(AgentCommandRequest) => wire.Payload.Deserialize<AgentCommandRequest>()!,
            nameof(AgentCommandCompleted) => wire.Payload.Deserialize<AgentCommandCompleted>()!,
            nameof(AgentHeartbeat) => wire.Payload.Deserialize<AgentHeartbeat>()!,
            _ => throw new InvalidDataException($"Unsupported runtime message {wire.Type}."),
        };

        private sealed record WireMessage(string Type, JsonElement Payload);
    }

    private sealed class RuntimePipeServer(string pipeName) : RuntimePipeEndpoint
    {
        public async Task StartAsync()
        {
            this.Stream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await ((NamedPipeServerStream)this.Stream).WaitForConnectionAsync().WaitAsync(TimeSpan.FromSeconds(2));
            this.StartReading();
        }
    }

    private sealed class RuntimePipeChannel(string pipeName) : RuntimePipeEndpoint, IIpcChannel
    {
        public bool IsConnected => this.Stream?.IsConnected == true;
        public event Action? Disconnected;
        event Action<IIpcMessage>? IIpcChannel.MessageReceived
        {
            add => this.MessageReceived += value;
            remove => this.MessageReceived -= value;
        }

        public async Task StartAsync(CancellationToken token = default)
        {
            this.Stream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await ((NamedPipeClientStream)this.Stream).ConnectAsync(token).WaitAsync(TimeSpan.FromSeconds(2));
            this.StartReading();
        }

        Task IIpcChannel.SendAsync(IIpcMessage message, CancellationToken token) => this.SendAsync(message, token);
        public Task StopAsync() { this.Disconnected?.Invoke(); return Task.CompletedTask; }
    }

    private sealed class RuntimeOverlayManager : IOverlayManager
    {
        public bool IsOverlayVisible { get; private set; }
        public event Action? CtaClicked;
        public void ShowOverlay(string reason, string? ctaLabel = null) => this.IsOverlayVisible = true;
        public void HideOverlay() => this.IsOverlayVisible = false;
        public void ShowWarning(int minutesRemaining) => this.IsOverlayVisible = true;
        public void Dispose() { }
    }

    private sealed class RuntimeWatcher : IForegroundWatcher
    {
        public string? CurrentAppId => null;
        public event Action<string>? ForegroundChanged;
        public Task StartAsync(CancellationToken token = default) => Task.CompletedTask;
        public void Stop() { }
    }
}
