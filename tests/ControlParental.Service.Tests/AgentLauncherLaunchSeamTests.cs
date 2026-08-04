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
            ipcChannelFactory: () => new FakeIpcChannel(),
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
            ipcChannelFactory: () => new FakeIpcChannel(),
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

    private sealed class FakeIpcChannel : IIpcChannel, IDisposable
    {
        public bool IsConnected { get; private set; }

        public event Action? Disconnected;

        public event Action<IIpcMessage>? MessageReceived;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            this.IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            this.IsConnected = false;
            return Task.CompletedTask;
        }

        public Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public void Dispose()
        {
        }
    }

    private sealed class FakeProcessLaunchApi : AgentLauncher.IProcessLaunchApi
    {
        public bool DuplicateTokenResult { get; init; }

        public bool CreateProcessResult { get; init; }

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
            return true;
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
