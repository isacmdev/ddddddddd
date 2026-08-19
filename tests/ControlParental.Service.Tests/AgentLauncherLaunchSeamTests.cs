// <copyright file="AgentLauncherLaunchSeamTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using ControlParental.Domain;
using ControlParental.Service;
using Xunit;

/// <summary>
/// Regression tests for the injectable AgentLauncher seams.
/// </summary>
public sealed class AgentLauncherLaunchSeamTests
{
    [Fact]
    public void CreateProcessAsUserCore_UsesInjectedHooksAndSelectsUnicodeEnvironment()
    {
        // Arrange
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
        };

        var launcher = new AgentLauncher(
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            _ => true,
            _ => { },
            () => { },
            sessionUserTokenProvider: _ => new IntPtr(0x1111),
            ipcChannelFactory: null,
            processLaunchApi: api);

        // Act
        var success = launcher.CreateProcessAsUserCore(new IntPtr(0x1111), 7);

        // Assert
        Assert.True(success);
        Assert.True(api.DuplicateTokenCalled);
        Assert.True(api.CreateEnvironmentBlockCalled);
        Assert.True(api.CreateProcessCalled);
        Assert.True(api.DestroyEnvironmentBlockCalled);
        Assert.Contains(api.DuplicatedToken, api.ClosedHandles);
        Assert.Contains(api.ProcessHandle, api.ClosedHandles);
        Assert.Contains(api.ThreadHandle, api.ClosedHandles);
        Assert.Equal(api.EnvironmentBlock, api.CapturedEnvironment);
        Assert.NotNull(api.CapturedFlags);
        Assert.True((api.CapturedFlags!.Value & 0x00000400) != 0);
    }

    [Fact]
    public void CreateProcessAsUserCore_ReturnsFalseWhenTokenDuplicationFails()
    {
        var api = new FakeProcessLaunchApi();
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111));

        Assert.False(launcher.CreateProcessAsUserCore(new IntPtr(0x1111), 7));
        Assert.True(api.DuplicateTokenCalled);
        Assert.False(api.CreateEnvironmentBlockCalled);
        Assert.Contains(api.DuplicatedToken, api.ClosedHandles);
    }

    [Fact]
    public void CreateProcessAsUserCore_ContinuesWithoutEnvironmentBlock()
    {
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            CreateEnvironmentBlockResult = false,
            CreateProcessResult = true,
        };
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111));

        Assert.True(launcher.CreateProcessAsUserCore(new IntPtr(0x1111), 7));
        Assert.True(api.CreateEnvironmentBlockCalled);
        Assert.Equal(IntPtr.Zero, api.CapturedEnvironment);
        Assert.False(api.DestroyEnvironmentBlockCalled);
    }

    [Fact]
    public void CreateProcessAsUserCore_ReturnsFalseWhenProcessApiThrows()
    {
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            OnCreateProcess = () => throw new InvalidOperationException("create failed"),
        };
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111));

        Assert.False(launcher.CreateProcessAsUserCore(new IntPtr(0x1111), 7));
        Assert.True(api.DestroyEnvironmentBlockCalled);
        Assert.Contains(api.DuplicatedToken, api.ClosedHandles);
    }

    [Fact]
    public async Task LaunchAgentAsync_PassesTargetSessionIdToPipeServerFactory()
    {
        var requestedSessionId = -1;
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
        };
        using var launcher = new AgentLauncher(
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            _ => true,
            _ => { },
            () => { },
            sessionUserTokenProvider: _ => new IntPtr(0x1111),
            ipcChannelFactory: sessionId =>
            {
                requestedSessionId = sessionId;
                return new FakeIpcChannel();
            },
            processLaunchApi: api);

        Assert.True(await launcher.LaunchAgentAsync(42));
        Assert.Equal(42, requestedSessionId);
    }

    public async Task SessionManager_OnSessionStarted_UsesInjectedLauncherWithoutHanging()
    {
        // Arrange
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
        };

        var launcher = new AgentLauncher(
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            _ => true,
            _ => { },
            () => { },
            sessionUserTokenProvider: _ => new IntPtr(0x1111),
            ipcChannelFactory: _ => new FakeIpcChannel(),
            processLaunchApi: api);

        var sessionManager = new SessionManager(
            string.Empty,
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            _ => { },
            _ => { },
            _ => { },
            launcher);

        // Act
        var task = sessionManager.OnSessionStarted(7);
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));

        // Assert
        Assert.Same(task, completed);
        await task;
        Assert.True(api.CreateProcessCalled);
        Assert.Contains(new IntPtr(0x1111), api.ClosedHandles);
    }

    [Fact]
    public async Task SessionManager_RecoverAgentAsync_UsesCurrentSessionAndRelaunchesAgent()
    {
        // Arrange
        var requestedSessionIds = new List<int>();
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
        };

        var launcher = new AgentLauncher(
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            _ => true,
            _ => { },
            () => { },
            sessionUserTokenProvider: sessionId =>
            {
                requestedSessionIds.Add(sessionId);
                return new IntPtr(0x1111);
            },
            ipcChannelFactory: _ => new FakeIpcChannel(),
            processLaunchApi: api);

        var sessionManager = new SessionManager(
            string.Empty,
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            _ => { },
            _ => { },
            _ => { },
            launcher);
        await sessionManager.OnSessionStarted(7);

        // Act
        var success = await sessionManager.RecoverAgentAsync();

        // Assert
        Assert.True(success);
        Assert.True(api.CreateProcessCalled);
        Assert.Equal(new[] { 7, 7 }, requestedSessionIds);
    }

    [Fact]
    public async Task SessionManager_SuppressesDuplicateStartAndStopsOwnedRecord()
    {
        var tokenRequests = 0;
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
        };
        var launcher = CreateLauncher(api, _ =>
        {
            tokenRequests++;
            return new IntPtr(0x1111);
        });
        var manager = CreateManager(launcher);

        await manager.OnSessionStarted(7);
        await manager.OnSessionStarted(7);

        Assert.NotNull(manager.AgentChannel);
        Assert.Equal(1, tokenRequests);
        await manager.SendToAgentAsync(new ShowOverlay("lifecycle", null));
        await manager.OnSessionEnded(7);
        await manager.StopAsync();
        manager.Dispose();
    }

    [Fact]
    public async Task AgentLauncher_FailedProcessLaunchStopsTheOwnedChannel()
    {
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = false,
        };
        var channel = new FakeIpcChannel();
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111), _ => channel);

        var success = await launcher.LaunchAgentAsync(7);

        Assert.False(success);
        Assert.False(channel.IsConnected);
        Assert.Contains(new IntPtr(0x1111), api.ClosedHandles);
        Assert.True(channel.StopCalled);
        Assert.True(channel.DisposeCalled);
        launcher.Dispose();
    }

    [Fact]
    public async Task AgentLauncher_ContainsListenerStopAndDisposeFailures()
    {
        var channel = new FakeIpcChannel(
            stopException: new TimeoutException(),
            disposeException: new InvalidOperationException("dispose failed"));
        var launcher = CreateLauncher(
            new FakeProcessLaunchApi { DuplicateTokenResult = true },
            _ => new IntPtr(0x1111),
            _ => channel);

        Assert.False(await launcher.LaunchAgentAsync(7));
        Assert.True(channel.StopCalled);
        Assert.True(channel.DisposeCalled);
        launcher.Dispose();
    }

    [Fact]
    public void AgentLauncher_DisposeIsIdempotent()
    {
        var launcher = CreateLauncher(new FakeProcessLaunchApi(), _ => null);

        launcher.Dispose();
        launcher.Dispose();
    }

    [Fact]
    public async Task AgentLauncher_RejectsLaunchAfterDispose()
    {
        var launcher = CreateLauncher(new FakeProcessLaunchApi(), _ => null);

        launcher.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => launcher.LaunchAgentAsync(7));
    }

    [Fact]
    public async Task AgentLauncher_CreatesProcessWhileListenerTaskIsActive()
    {
        var listenerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new FakeIpcChannel(listenerStarted.Task);
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
            OnCreateProcess = () => Assert.False(listenerStarted.Task.IsCompleted),
        };
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111), _ => channel);

        var success = await launcher.LaunchAgentAsync(7);

        Assert.True(success);
        Assert.True(api.CreateProcessCalled);
        Assert.False(listenerStarted.Task.IsCompleted);
        listenerStarted.SetResult(true);
        launcher.Dispose();
        await channel.CleanupCompleted.Task;
        Assert.True(channel.DisposeCalled);
        Assert.False(channel.IsConnected);
        Assert.True(channel.StartTask.IsCompleted);
    }

    [Fact]
    public async Task AgentLauncher_LateListenerFailureAfterProcessCreationIsObservedAndCleansUp()
    {
        var listener = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new FakeIpcChannel(listener.Task);
        var failure = new InvalidOperationException("listener failed after process creation");
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
            OnCreateProcess = () => listener.TrySetException(failure),
        };
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111), _ => channel);

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => launcher.LaunchAgentAsync(7));

        Assert.Same(failure, observed);
        Assert.True(api.CreateProcessCalled);
        Assert.True(channel.StopCalled);
        Assert.True(channel.DisposeCalled);
        Assert.False(channel.IsConnected);
        Assert.True(channel.StartTask.IsCompleted);
        launcher.Dispose();
    }

    [Fact]
    public async Task AgentLauncher_PostReturnListenerFailureStopsAndDisposesOwnedChannel()
    {
        var listener = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new FakeIpcChannel(listener.Task);
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
        };
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111), _ => channel);

        var success = await launcher.LaunchAgentAsync(7);

        Assert.True(success);
        Assert.True(channel.IsConnected);
        var failure = new InvalidOperationException("listener failed after launch returned");
        listener.SetException(failure);

        await channel.CleanupCompleted.Task;

        Assert.True(channel.StopCalled);
        Assert.True(channel.DisposeCalled);
        Assert.False(channel.IsConnected);
        Assert.True(channel.StartTask.IsCompleted);
        Assert.Null(launcher.AgentChannel);
        launcher.Dispose();
    }

    [Fact]
    public async Task AgentLauncher_CancellationStopsOwnedListenerBeforeProcessCreation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var listener = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new FakeIpcChannel(listener.Task, () => listener.TrySetCanceled());
        var api = new FakeProcessLaunchApi();
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111), _ => channel);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => launcher.LaunchAgentAsync(7, cancellation.Token));

        Assert.True(channel.StopCalled);
        Assert.True(channel.DisposeCalled);
        Assert.False(api.CreateProcessCalled);
        launcher.Dispose();
    }

    [Fact]
    public async Task AgentLauncher_SynchronousStartFailureCleansProvisionalOwnership()
    {
        var channel = new FakeIpcChannel(startException: new InvalidOperationException("start failed"));
        var api = new FakeProcessLaunchApi();
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111), _ => channel);

        await Assert.ThrowsAsync<InvalidOperationException>(() => launcher.LaunchAgentAsync(7));

        Assert.False(api.CreateProcessCalled);
        Assert.True(channel.CancellationRequested);
        Assert.Equal(1, channel.StopCount);
        Assert.Equal(1, channel.DisposeCount);
        Assert.Null(launcher.AgentChannel);
        launcher.Dispose();
    }

    [Fact]
    public async Task AgentLauncher_DisposeDuringStartRejectsPublicationAndCleansCandidate()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new FakeIpcChannel(startEntered: entered, startGate: gate.Task);
        var api = new FakeProcessLaunchApi { CreateProcessResult = true };
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111), _ => channel);

        var launch = Task.Run(() => launcher.LaunchAgentAsync(7));
        await entered.Task;
        launcher.Dispose();
        gate.SetResult(true);

        Assert.False(await launch);
        Assert.False(api.CreateProcessCalled);
        Assert.True(channel.CancellationRequested);
        Assert.Equal(1, channel.StopCount);
        Assert.Equal(1, channel.DisposeCount);
        Assert.Null(launcher.AgentChannel);
    }

    [Fact]
    public async Task AgentLauncher_DisposeDuringProcessCreationCannotReturnSuccess()
    {
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new FakeIpcChannel();
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true, EnvironmentBlock = new IntPtr(0x2222), CreateProcessResult = true,
            OnCreateProcess = () => { entered.TrySetResult(true); gate.Task.GetAwaiter().GetResult(); },
        };
        var launcher = CreateLauncher(api, _ => new IntPtr(0x1111), _ => channel);

        var launch = Task.Run(() => launcher.LaunchAgentAsync(7));
        await entered.Task;
        launcher.Dispose();
        gate.SetResult(true);

        Assert.False(await launch);
        Assert.Equal(1, channel.StopCount);
        Assert.Equal(1, channel.DisposeCount);
        Assert.Null(launcher.AgentChannel);
    }

    [Fact]
    public async Task AgentLauncher_FaultAndDisposeRaceHasOneCleanupClaimant()
    {
        var listener = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var channel = new FakeIpcChannel(listener.Task);
        var launcher = CreateLauncher(new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true, EnvironmentBlock = new IntPtr(0x2222), CreateProcessResult = true,
        }, _ => new IntPtr(0x1111), _ => channel);
        Assert.True(await launcher.LaunchAgentAsync(7));

        await Task.WhenAll(
            Task.Run(launcher.Dispose),
            Task.Run(() => listener.TrySetException(new InvalidOperationException("fault"))));
        await channel.CleanupCompleted.Task;

        Assert.Equal(1, channel.StopCount);
        Assert.Equal(1, channel.DisposeCount);
        Assert.Null(launcher.AgentChannel);
    }

    [Fact]
    public async Task AgentLauncher_MissingSessionTokenReturnsWithoutStartingIpc()
    {
        var api = new FakeProcessLaunchApi();
        var starts = 0;
        var launcher = CreateLauncher(api, _ => null, _ =>
        {
            starts++;
            return new FakeIpcChannel();
        });

        var success = await launcher.LaunchAgentAsync(7);

        Assert.False(success);
        Assert.Equal(0, starts);
        launcher.Dispose();
    }

    [Fact]
    public async Task SessionManager_RecoveryFailureRemainsBoundedAndObservable()
    {
        var requestedSessionIds = new List<int>();
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = false,
        };
        var manager = CreateManager(CreateLauncher(api, sessionId =>
        {
            requestedSessionIds.Add(sessionId);
            return new IntPtr(0x1111);
        }));

        await manager.OnSessionStarted(7);
        var recovered = await manager.RecoverAgentAsync();

        Assert.False(recovered);
        Assert.Equal(4, requestedSessionIds.Count);
        Assert.All(requestedSessionIds, sessionId => Assert.Equal(7, sessionId));
        await manager.StopAsync();
        manager.Dispose();
    }

    [Fact]
    public async Task SessionManager_RecoveryCancellationStopsBeforeNextRetry()
    {
        var requestedSessionIds = new List<int>();
        var recoveryAttemptObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var createProcessCount = 0;
        using var cancellation = new CancellationTokenSource();
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = false,
            OnCreateProcess = () =>
            {
                if (Interlocked.Increment(ref createProcessCount) == 2)
                {
                    recoveryAttemptObserved.TrySetResult(true);
                }
            },
        };
        var manager = CreateManager(CreateLauncher(api, sessionId =>
        {
            requestedSessionIds.Add(sessionId);
            return new IntPtr(0x1111);
        }));

        await manager.OnSessionStarted(7);
        var recovery = manager.RecoverAgentAsync(cancellation.Token);
        await recoveryAttemptObserved.Task;
        cancellation.Cancel();

        Assert.False(await recovery);
        Assert.Equal(2, requestedSessionIds.Count);
        Assert.All(requestedSessionIds, sessionId => Assert.Equal(7, sessionId));
        await manager.StopAsync();
        manager.Dispose();
    }

    [Fact]
    public async Task SessionManager_ReportsChannelChangesOnRecovery()
    {
        var requestedSessionIds = new List<int>();
        var emittedChannels = new List<IIpcChannel?>();
        var launchCount = 0;
        var api = new FakeProcessLaunchApi
        {
            DuplicateTokenResult = true,
            EnvironmentBlock = new IntPtr(0x2222),
            CreateProcessResult = true,
        };

        var channel1 = new FakeIpcChannel();
        var channel2 = new FakeIpcChannel();
        var launcher = CreateLauncher(
            api,
            sessionId =>
            {
                requestedSessionIds.Add(sessionId);
                return new IntPtr(0x1111);
            },
            _ => launchCount++ == 0 ? channel1 : channel2);

        var manager = new SessionManager(
            string.Empty,
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            _ => { },
            _ => { },
            _ => { },
            launcher,
            channel => emittedChannels.Add(channel));

        await manager.OnSessionStarted(7);
        var recovered = await manager.RecoverAgentAsync();

        Assert.True(recovered);
        Assert.Same(channel1, emittedChannels[0]);
        Assert.Same(channel2, emittedChannels[1]);
        Assert.Equal(new[] { 7, 7 }, requestedSessionIds);

        await manager.StopAsync();
        manager.Dispose();
    }

    private static SessionManager CreateManager(AgentLauncher launcher)
        => new(
            string.Empty,
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            _ => { },
            _ => { },
            _ => { },
            launcher);

    private static AgentLauncher CreateLauncher(
        FakeProcessLaunchApi api,
        Func<int, IntPtr?> tokenProvider,
        Func<int, IIpcChannel>? channelFactory = null)
        => new(
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            _ => true,
            _ => { },
            () => { },
            tokenProvider,
            channelFactory ?? (_ => new FakeIpcChannel()),
            api);

    private sealed class FakeIpcChannel : IIpcChannel, IDisposable
    {
        private readonly Task startTask;
        private readonly Action? onStop;
        private readonly TaskCompletionSource<bool>? startEntered;
        private readonly Task? startGate;
        private readonly Exception? startException;
        private readonly Exception? stopException;
        private readonly Exception? disposeException;

        public FakeIpcChannel(Task? startTask = null, Action? onStop = null, TaskCompletionSource<bool>? startEntered = null, Task? startGate = null, Exception? startException = null, Exception? stopException = null, Exception? disposeException = null)
        {
            this.startTask = startTask ?? Task.CompletedTask;
            this.onStop = onStop;
            this.startEntered = startEntered;
            this.startGate = startGate;
            this.startException = startException;
            this.stopException = stopException;
            this.disposeException = disposeException;
        }

        public bool IsConnected { get; private set; }

        public bool StopCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public int StopCount { get; private set; }

        public int DisposeCount { get; private set; }

        public bool CancellationRequested { get; private set; }

        public TaskCompletionSource<bool> CleanupCompleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task StartTask => this.startTask;

        public event Action? Disconnected;

        public event Action<IIpcMessage>? MessageReceived;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            this.IsConnected = true;
            _ = cancellationToken.Register(() => this.CancellationRequested = true);
            this.startEntered?.TrySetResult(true);
            this.startGate?.GetAwaiter().GetResult();
            if (this.startException != null)
            {
                throw this.startException;
            }

            return this.startTask;
        }

        public Task StopAsync()
        {
            this.StopCalled = true;
            this.StopCount++;
            this.onStop?.Invoke();
            if (this.stopException != null)
            {
                throw this.stopException;
            }
            this.IsConnected = false;
            return Task.CompletedTask;
        }

        public Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
            this.DisposeCalled = true;
            this.DisposeCount++;
            if (this.disposeException != null)
            {
                throw this.disposeException;
            }
            this.IsConnected = false;
            this.CleanupCompleted.TrySetResult(true);
        }
    }

    private sealed class FakeProcessLaunchApi : AgentLauncher.IProcessLaunchApi
    {
        public bool DuplicateTokenResult { get; init; }

        public bool CreateProcessResult { get; init; }

        public bool CreateEnvironmentBlockResult { get; init; } = true;

        public Action? OnCreateProcess { get; init; }

        public IntPtr EnvironmentBlock { get; init; }

        public IntPtr DuplicatedToken { get; } = new(0x3333);

        public IntPtr ProcessHandle { get; } = new(0x4444);

        public IntPtr ThreadHandle { get; } = new(0x5555);

        public bool DuplicateTokenCalled { get; private set; }

        public bool CreateEnvironmentBlockCalled { get; private set; }

        public bool CreateProcessCalled { get; private set; }

        public bool DestroyEnvironmentBlockCalled { get; private set; }

        public int? CapturedFlags { get; private set; }

        public IntPtr CapturedEnvironment { get; private set; }

        public List<IntPtr> ClosedHandles { get; } = new();

        public bool DuplicateTokenEx(
            IntPtr existingToken,
            int desiredAccess,
            IntPtr tokenAttributes,
            int impersonationLevel,
            int tokenType,
            out IntPtr newToken)
        {
            this.DuplicateTokenCalled = true;
            newToken = this.DuplicatedToken;
            return this.DuplicateTokenResult;
        }

        public bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit)
        {
            this.CreateEnvironmentBlockCalled = true;
            environment = this.EnvironmentBlock;
            return this.CreateEnvironmentBlockResult;
        }

        public bool CreateProcessAsUser(
            IntPtr token,
            string? applicationName,
            string commandLine,
            IntPtr processAttributes,
            IntPtr threadAttributes,
            bool inheritHandles,
            int flags,
            IntPtr environment,
            string? currentDirectory,
            ref AgentLauncher.STARTUPINFO startupInfo,
            out AgentLauncher.PROCESS_INFORMATION processInformation)
        {
            this.CreateProcessCalled = true;
            this.OnCreateProcess?.Invoke();
            this.CapturedFlags = flags;
            this.CapturedEnvironment = environment;
            processInformation = new AgentLauncher.PROCESS_INFORMATION
            {
                hProcess = this.ProcessHandle,
                hThread = this.ThreadHandle,
                dwProcessId = 4242,
                dwThreadId = 99,
            };

            return this.CreateProcessResult;
        }

        public bool DestroyEnvironmentBlock(IntPtr environment)
        {
            this.DestroyEnvironmentBlockCalled = true;
            return true;
        }

        public Process GetProcessById(int processId)
            => throw new ArgumentException("Process exited immediately.");

        public bool CloseHandle(IntPtr handle)
        {
            this.ClosedHandles.Add(handle);
            return true;
        }
    }
}
