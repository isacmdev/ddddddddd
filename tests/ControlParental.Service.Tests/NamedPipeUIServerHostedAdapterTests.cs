// <copyright file="NamedPipeUIServerHostedAdapterTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using ControlParental.Domain;
using ControlParental.Service.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// P0 — Lifecycle tests for <see cref="NamedPipeUIServerHostedAdapter"/>.
/// Covers the contract the generic host depends on:
///
/// <list type="bullet">
///   <item><see cref="IHostedService.StartAsync"/> must kick the listener
///         off in the background and return promptly, never blocking on
///         <c>WaitForConnectionAsync</c>.</item>
///   <item><see cref="IHostedService.StopAsync"/> must drain the listener
///         gracefully and remain safe to call more than once.</item>
///   <item><see cref="IDisposable.Dispose"/> must release the inner
///         server and the internal CTS without leaking, even if
///         <see cref="IHostedService.StopAsync"/> was never called.</item>
///   <item>An already-cancelled host stopping token MUST NOT propagate
///         out of <see cref="IHostedService.StopAsync"/>.</item>
///   <item><see cref="IHostedService.StopAsync"/> MUST observe the
///         listener run task to actual completion — even when the host
///         stopping token is already cancelled on entry — so the host
///         never returns before the listener has drained.</item>
/// </list>
///
/// <para>
/// Tests use a real <see cref="NamedPipeUIServer"/> so the production
/// StartAsync/StopAsync surface is exercised. The listener only blocks
/// on WaitForConnectionAsync, and every test cancels the listener
/// before exiting, so no pipe is left bound on the shared
/// "ControlParental.UI" name. Tests within this fixture run serially
/// (xUnit's default for a single class) to avoid racing on the name.
/// </para>
/// </summary>
public sealed class NamedPipeUIServerHostedAdapterTests : IDisposable
{
    private readonly string tempDir;
    private readonly ServiceProvider services;
    private readonly UIMessageHandler messageHandler;
    private readonly Mock<ILogger<NamedPipeUIServerHostedAdapter>> mockLogger;

    public NamedPipeUIServerHostedAdapterTests()
    {
        this.tempDir = Path.Combine(
            Path.GetTempPath(),
            $"cp-ui-pipe-adapter-{Guid.NewGuid():N}");

        var collection = new ServiceCollection();
        this.services = collection.BuildServiceProvider();

        var stateService = new OnboardingStateService(
            this.tempDir,
            new Mock<IChildAccountStore>().Object,
            new Mock<ILogger<OnboardingStateService>>().Object);

        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(x => x.CurrentIssues).Returns(Array.Empty<EnforcementIssue>());

        this.messageHandler = new UIMessageHandler(
            stateService,
            new EnforcementLevelQueryHandler(monitor.Object),
            this.services.GetRequiredService<IServiceScopeFactory>(),
            new Mock<ILogger<UIMessageHandler>>().Object);

        this.mockLogger = new Mock<ILogger<NamedPipeUIServerHostedAdapter>>();
    }

    [Fact]
    public async Task StartAsync_DelegatesToPipeServer_AndReturnsPromptly()
    {
        // Arrange — fresh adapter, listener has not yet been started.
        var adapter = this.CreateAdapter();

        // Act — StartAsync must kick the listener off in a background task
        // and return immediately. A naive delegation that awaited the inner
        // StartAsync would block indefinitely here (the listener loop sits
        // on WaitForConnectionAsync).
        var stopwatch = Stopwatch.StartNew();
        await adapter.StartAsync(CancellationToken.None);
        stopwatch.Stop();

        // Assert — return path is fast; we are not waiting on a connection.
        Assert.True(
            stopwatch.ElapsedMilliseconds < 2000,
            $"StartAsync took {stopwatch.ElapsedMilliseconds}ms; the adapter must run the listener in the background.");

        // Cleanup — cancel the listener so the kernel pipe handle is released.
        await adapter.StopAsync(CancellationToken.None);

        adapter.Dispose();
    }

    [Fact]
    public async Task StopAsync_IsIdempotent()
    {
        // Arrange
        var adapter = this.CreateAdapter();
        await adapter.StartAsync(CancellationToken.None);

        // Act + Assert — second call MUST NOT throw. The first call cancels
        // the internal loop and waits for the run task; the second call is a
        // no-op.
        await adapter.StopAsync(CancellationToken.None);
        await adapter.StopAsync(CancellationToken.None);

        adapter.Dispose();
    }

    [Fact]
    public async Task Dispose_IsIdempotent_AndReleasesListener()
    {
        // Arrange — start the adapter so the listener is bound, then dispose
        // directly without going through StopAsync. The host uses StopAsync
        // first, but tests / future callers MUST be able to dispose the
        // adapter and walk away cleanly.
        var adapter = this.CreateAdapter();
        await adapter.StartAsync(CancellationToken.None);

        // Act + Assert — disposing twice does not throw and does not
        // propagate from Dispose.
        adapter.Dispose();
        adapter.Dispose();
    }

    [Fact]
    public async Task StopAsync_WithAlreadyCancelledToken_CompletesCleanly()
    {
        // Arrange — start the adapter and pre-cancel the token the host
        // will pass to StopAsync. The cancellation path through WaitFor*
        // must drain without throwing; the run task's WaitAsync catch on
        // OperationCanceledException must swallow the signal.
        var adapter = this.CreateAdapter();
        await adapter.StartAsync(CancellationToken.None);

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();

        // Act + Assert — StopAsync must not throw on a cancelled token.
        await adapter.StopAsync(alreadyCancelled.Token);

        adapter.Dispose();
    }

    [Fact]
    public async Task StopAsync_WithAlreadyCancelledToken_ObservesListenerCompletion()
    {
        // Arrange — start the adapter and pre-cancel the token the host
        // will pass to StopAsync. The previous best-effort path returned
        // immediately on the cancelled token (Task.WaitAsync(token) threw
        // OperationCanceledException on entry), so the listener was left
        // still draining. The fix replaces the cancellationToken argument
        // with a bounded drain CTS so the listener completion is always
        // observed before StopAsync returns.
        var adapter = this.CreateAdapter();
        await adapter.StartAsync(CancellationToken.None);

        using var alreadyCancelled = new CancellationTokenSource();
        alreadyCancelled.Cancel();

        // Act — StopAsync must not throw on a cancelled token AND must wait
        // for the listener to actually drain within the bounded drain
        // budget before returning.
        await adapter.StopAsync(alreadyCancelled.Token);

        // Assert — the run task must be observed as completed. If StopAsync
        // returned early on the cancelled token, the run task would still
        // be pending (the listener is blocked on WaitForConnectionAsync
        // until internalCts is cancelled and the loop unwinds).
        Assert.True(
            adapter.IsRunTaskCompleted,
            "After StopAsync with a cancelled token returns, the listener run task must be observed as completed.");

        adapter.Dispose();
    }

    [Fact]
    public async Task StartStopLifecycle_HappyPath_CompletesWithoutThrowing()
    {
        // Arrange — full lifecycle: start, stop, dispose. Mirrors what the
        // generic host does when bringing the service online and back down.
        var adapter = this.CreateAdapter();

        // Act
        await adapter.StartAsync(CancellationToken.None);
        await adapter.StopAsync(CancellationToken.None);

        // Assert — both calls returned; no exception escaped.
        adapter.Dispose();
    }

    [Fact]
    public async Task Listener_PipeCreationFailuresUseBoundedBackoffInsteadOfSpinning()
    {
        // RED seam: expose the private listener with an injectable pipe factory
        // and delay delegate. The delay delegate is deliberately deterministic;
        // this test must not sleep in real time.
        using var cancellation = new CancellationTokenSource();
        var createAttempts = 0;
        var delays = new List<TimeSpan>();
        var listener = this.CreateListener(
            cancellation.Token,
            _ =>
            {
                createAttempts++;
                throw new IOException("sensitive pipe path");
            },
            (delay, _) =>
            {
                delays.Add(delay);
                if (delays.Count >= 3)
                {
                    cancellation.Cancel();
                }

                return Task.CompletedTask;
            });

        await listener.StartAsync();

        Assert.Equal(3, createAttempts);
        Assert.Equal(3, delays.Count);
        Assert.All(delays, delay => Assert.InRange(delay, TimeSpan.Zero, TimeSpan.FromSeconds(1)));
        Assert.True(listener.IsReady == false);
    }

    [Fact]
    public async Task Listener_PipeCreationFailureLogsSanitizedTypeAndContext()
    {
        using var cancellation = new CancellationTokenSource();
        var listener = this.CreateListener(
            cancellation.Token,
            _ => throw new UnauthorizedAccessException("secret path or token"),
            (_, _) =>
            {
                cancellation.Cancel();
                return Task.CompletedTask;
            });

        await listener.StartAsync();

        var log = Assert.Single(
            this.mockLogger.Invocations,
            invocation => invocation.Method.Name == nameof(ILogger.Log));
        Assert.Equal(LogLevel.Error, (LogLevel)log.Arguments[0]!);
        var state = log.Arguments[2]?.ToString() ?? string.Empty;
        Assert.Contains("pipe", state, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(UnauthorizedAccessException), state);
        Assert.DoesNotContain("secret path or token", state);
    }

    [Fact]
    public async Task Listener_CancellationDuringBackoffExitsPromptly()
    {
        using var cancellation = new CancellationTokenSource();
        var backoffEntered = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = this.CreateListener(
            cancellation.Token,
            _ => throw new IOException("pipe unavailable"),
            (_, token) =>
            {
                backoffEntered.SetResult(null);
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            });

        var startTask = listener.StartAsync();
        await backoffEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await startTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(listener.IsReady);
    }

    [Fact]
    public async Task Listener_DoesNotReportReadyUntilPipeCreationSucceeds()
    {
        using var cancellation = new CancellationTokenSource();
        var createAttempts = 0;
        var backoffEntered = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBackoff = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var pipe = new FakeUiPipeServer();
        var listener = this.CreateListener(
            cancellation.Token,
            _ =>
            {
                createAttempts++;
                if (createAttempts == 1)
                {
                    throw new IOException("pipe unavailable");
                }

                return Task.FromResult<NamedPipeUIServer.IUiPipeServer>(pipe);
            },
            (_, _) =>
            {
                backoffEntered.TrySetResult(null);
                return releaseBackoff.Task;
            });

        var startTask = listener.StartAsync();
        try
        {
            await backoffEntered.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.False(listener.IsReady);

            releaseBackoff.TrySetResult(null);
            await pipe.ConnectionWaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(
                SpinWait.SpinUntil(() => listener.IsReady, TimeSpan.FromSeconds(1)),
                "Listener did not report ready within one second after the pipe began waiting for a connection.");
        }
        finally
        {
            releaseBackoff.TrySetResult(null);
            cancellation.Cancel();
            await startTask.WaitAsync(TimeSpan.FromSeconds(1));
        }
    }

    [Fact]
    public async Task StartAsync_ExposesUiPipeBeforeClientConnects()
    {
        var adapter = new NamedPipeUIServerHostedAdapter(
            new NamedPipeUIServer(
                null,
                null,
                new Mock<ILogger<NamedPipeUIServer>>().Object),
            this.messageHandler,
            this.mockLogger.Object);

        await adapter.StartAsync(CancellationToken.None);

        using var client = new NamedPipeClientStream(
            ".",
            "ControlParental.UI",
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);

        Assert.True(client.IsConnected);
        await adapter.StopAsync(CancellationToken.None);
        adapter.Dispose();
    }

    [Fact]
    public void UiPipe_UsesFrameworkMaximumServerInstanceValue()
    {
        var source = File.ReadAllText(Path.Combine(
            LocateRepoRoot(),
            "src",
            "ControlParental.Service",
            "Interop",
            "NamedPipeUIServer.cs"));

        Assert.Contains("NamedPipeServerStream.MaxAllowedServerInstances", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PipeDirection.InOut,\n                255,", source, StringComparison.Ordinal);
    }

    private NamedPipeUIServer.PipeServerListener CreateListener(
        CancellationToken cancellationToken,
        Func<CancellationToken, Task<NamedPipeUIServer.IUiPipeServer>> pipeFactory,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        return new NamedPipeUIServer.PipeServerListener(
            "ControlParental.UI",
            null!,
            null!,
            null!,
            () => { },
            cancellationToken,
            pipeFactory,
            delay,
            this.mockLogger.Object);
    }

    private sealed class FakeUiPipeServer : NamedPipeUIServer.IUiPipeServer
    {
        public TaskCompletionSource<object?> ConnectionWaitStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsConnected => false;

        public Task WaitForConnectionAsync(CancellationToken cancellationToken)
        {
            this.ConnectionWaitStarted.TrySetResult(null);
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
            => cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<int>(cancellationToken)
                : Task.FromResult(0);

        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
            => cancellationToken.IsCancellationRequested
                ? Task.FromCanceled(cancellationToken)
                : Task.CompletedTask;

        public System.Security.Principal.SecurityIdentifier GetImpersonationUserSid()
            => new(
                System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid,
                null);

        public void Dispose()
        {
        }
    }

    private NamedPipeUIServerHostedAdapter CreateAdapter()
    {
        return new NamedPipeUIServerHostedAdapter(
            new NamedPipeUIServer(),
            this.messageHandler,
            this.mockLogger.Object);
    }

    private static string LocateRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null &&
               !Directory.Exists(Path.Combine(current.FullName, ".git")) &&
               !File.Exists(Path.Combine(current.FullName, ".git")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    public void Dispose()
    {
        this.services.Dispose();

        // Best-effort cleanup — the StateService may have created the folder
        // to host onboarding_state.json (the adapter never persists in this
        // fixture, but the handler holds a reference).
        try
        {
            if (Directory.Exists(this.tempDir))
            {
                Directory.Delete(this.tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}
