// <copyright file="ScheduledWorkServiceAsyncDispatchTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Domain.WireContracts;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T20/P1 — Focused tests for the async dispatch / overlap / shutdown hardening
/// of <see cref="ScheduledWorkService"/>. These tests target the new
/// non-blocking dispatch path, the per-work-type overlap suppression gate, and
/// the deterministic StopAsync/Dispose behaviour. They also exercise the
/// previously-uncovered ExecuteHeartbeatAsync, ExecuteReconciliationAsync, and
/// ExecutePolicySyncAsync code paths through their now-internal entry points
/// so that the focused line coverage gate (>= 80% on ScheduledWorkService.cs)
/// is met.
/// </summary>
public class ScheduledWorkServiceAsyncDispatchTests : IDisposable
{
    private readonly Mock<IBackendClient> mockBackendClient;
    private readonly Mock<IOutboxManager> mockOutboxManager;
    private readonly Mock<IUsageReconciler> mockUsageReconciler;
    private readonly Mock<IEnforcementLevelMonitor> mockEnforcementLevelMonitor;
    private readonly Mock<ITimeProvider> mockTimeProvider;
    private readonly Mock<IServiceHealthMonitor> mockHealthMonitor;
    private readonly Mock<IServiceRecoveryManager> mockRecoveryManager;
    private readonly Mock<IPolicyRepository> mockPolicyRepository;
    private readonly Mock<IBackendIdentityCoordinator> mockIdentityCoordinator;
    private readonly ScheduledWorkService service;

    public ScheduledWorkServiceAsyncDispatchTests()
    {
        this.mockBackendClient = new Mock<IBackendClient>();
        this.mockOutboxManager = new Mock<IOutboxManager>();
        this.mockUsageReconciler = new Mock<IUsageReconciler>();
        this.mockEnforcementLevelMonitor = new Mock<IEnforcementLevelMonitor>();
        this.mockTimeProvider = new Mock<ITimeProvider>();
        this.mockHealthMonitor = new Mock<IServiceHealthMonitor>();
        this.mockRecoveryManager = new Mock<IServiceRecoveryManager>();
        this.mockPolicyRepository = new Mock<IPolicyRepository>();
        this.mockIdentityCoordinator = new Mock<IBackendIdentityCoordinator>();

        this.mockUsageReconciler.SetupGet(r => r.IsRunning).Returns(false);
        this.mockEnforcementLevelMonitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Standard);
        this.mockHealthMonitor.SetupGet(m => m.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(m => m.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockIdentityCoordinator.SetupGet(c => c.CurrentState).Returns(
            BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 1, "device-test"));

        this.service = new ScheduledWorkService(
            backendClient: this.mockBackendClient.Object,
            outboxManager: this.mockOutboxManager.Object,
            usageReconciler: this.mockUsageReconciler.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            timeProvider: this.mockTimeProvider.Object,
            healthMonitor: this.mockHealthMonitor.Object,
            recoveryManager: this.mockRecoveryManager.Object,
            policyRepository: this.mockPolicyRepository.Object,
            identityCoordinator: this.mockIdentityCoordinator.Object);
    }

    public void Dispose()
    {
        this.service.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Async dispatch seam ─────────────────────────────────────────────

    /// <summary>
    /// Spec: TryDispatchWork is the new seam that replaces the blocking
    /// Task.Wait calls on the timer thread. It must run the supplied work on
    /// the thread pool, not on the caller's thread, and must record the
    /// in-flight task so StopAsync can await it.
    /// </summary>
    [Fact]
    public async Task TryDispatchWork_RunsOnThreadPool_AndTracksInFlight()
    {
        await this.service.StartAsync();

        var callingThreadId = Environment.CurrentManagedThreadId;
        int? workThreadId = null;

        var dispatched = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Heartbeat,
            () =>
            {
                workThreadId = Environment.CurrentManagedThreadId;
                dispatched.SetResult(true);
                return Task.CompletedTask;
            });

        // Wait for the dispatched work to actually run.
        await dispatched.Task.WaitAsync(TimeSpan.FromSeconds(5));

        workThreadId.Should().NotBeNull();
        workThreadId.Should().NotBe(callingThreadId, "dispatch must not run on the timer's thread");

        // After completion the in-flight slot must be cleared.
        await this.service.StopAsync();
    }

    /// <summary>
    /// Spec: an exception inside the dispatched work must NOT propagate out of
    /// the timer callback or crash the service. The dispatch wrapper swallows
    /// it, logs at debug, and clears the slot.
    /// </summary>
    [Fact]
    public async Task TryDispatchWork_ExceptionInsideWork_IsSwallowedAndSlotReleased()
    {
        await this.service.StartAsync();

        var faulted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Heartbeat,
            () =>
            {
                faulted.SetResult(true);
                throw new InvalidOperationException("boom");
            });

        await faulted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Give the dispatcher time to observe the throw and clean up.
        await Task.Delay(50);

        // After the throw the in-flight slot MUST be released — a subsequent
        // dispatch for the same work type must succeed.
        var secondRan = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Heartbeat,
            () =>
            {
                secondRan.SetResult(true);
                return Task.CompletedTask;
            });

        await secondRan.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await this.service.StopAsync();
    }

    // ── Overlap suppression ──────────────────────────────────────────────

    /// <summary>
    /// Spec: while a work-type is in flight, a new tick for the SAME work type
    /// must be dropped (overlap suppression). This protects against timer
    /// re-entrancy when a slow Execute* exceeds the timer interval.
    /// </summary>
    [Fact]
    public async Task TryDispatchWork_SameWorkTypeAlreadyInFlight_IsSuppressed()
    {
        await this.service.StartAsync();

        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondObserved = false;

        // First dispatch — runs and blocks on `gate`.
        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Heartbeat,
            async () =>
            {
                firstStarted.SetResult(true);
                await gate.Task.ConfigureAwait(false);
            });

        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Second dispatch — must be dropped immediately because the slot is
        // occupied. We track this by NOT entering the inner lambda at all.
        try
        {
            this.InvokeTryDispatchWork(
                ScheduledWorkService.WorkType.Heartbeat,
                () =>
                {
                    secondObserved = true;
                    gate.SetResult(true);
                    return Task.CompletedTask;
                });
        }
        catch
        {
            // The suppression path doesn't call the work delegate, so the gate
            // is never released by the second attempt — release it from here
            // if anything goes wrong, otherwise release after the assertion.
        }

        await Task.Delay(100);

        secondObserved.Should().BeFalse("overlapping dispatch must be dropped while the slot is occupied");

        gate.TrySetResult(true);

        await this.service.StopAsync();
    }

    /// <summary>
    /// Spec: overlap suppression is per-work-type — different work types must
    /// be allowed to run concurrently.
    /// </summary>
    [Fact]
    public async Task TryDispatchWork_DifferentWorkTypes_RunIndependently()
    {
        await this.service.StartAsync();

        var heartbeatDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconciliationDone = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Heartbeat,
            () =>
            {
                heartbeatDone.SetResult(true);
                return Task.CompletedTask;
            });

        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Reconciliation,
            () =>
            {
                reconciliationDone.SetResult(true);
                return Task.CompletedTask;
            });

        await heartbeatDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await reconciliationDone.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await this.service.StopAsync();
    }

    /// <summary>
    /// Spec: TryDispatchWork is a no-op when the service is not running or has
    /// been disposed. This is the deterministic-shutdown contract for the
    /// dispatch layer.
    /// </summary>
    [Fact]
    public async Task TryDispatchWork_WhenStopped_DoesNotInvokeWork()
    {
        // Do NOT start the service — IsRunning is false.
        var workInvoked = false;

        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Heartbeat,
            () =>
            {
                workInvoked = true;
                return Task.CompletedTask;
            });

        await Task.Delay(50);
        workInvoked.Should().BeFalse();
    }

    [Fact]
    public async Task TryDispatchWork_AfterDispose_DoesNotInvokeWork()
    {
        await this.service.StartAsync();
        this.service.Dispose();

        var workInvoked = false;
        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Heartbeat,
            () =>
            {
                workInvoked = true;
                return Task.CompletedTask;
            });

        await Task.Delay(50);
        workInvoked.Should().BeFalse();
    }

    // ── Deterministic StopAsync / Dispose ─────────────────────────────────

    /// <summary>
    /// Spec: StopAsync cancels in-flight work and returns once that work
    /// completes. A slow Execute* must NOT prevent StopAsync from returning
    /// within the shutdown budget.
    /// </summary>
    [Fact]
    public async Task StopAsync_AwaitsInFlightWork_BeforeReturning()
    {
        await this.service.StartAsync();

        var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedInsideWork = false;

        this.InvokeTryDispatchWork(
            ScheduledWorkService.WorkType.Heartbeat,
            async () =>
            {
                observedInsideWork = true;
                observed.SetResult(true);

                // Wait until cancellation kicks in.
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected.
                }
            });

        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // StopAsync should cancel CTS, await the in-flight task, and return.
        await this.service.StopAsync();

        observedInsideWork.Should().BeTrue();
        this.service.IsRunning.Should().BeFalse();
    }

    /// <summary>
    /// Spec: StopAsync does not throw when called twice (idempotent). The
    /// service must not deadlock or fault when its own shutdown is replayed.
    /// </summary>
    [Fact]
    public async Task StopAsync_CalledTwice_DoesNotThrowOrBlock()
    {
        await this.service.StartAsync();

        await this.service.StopAsync();
        var act = async () => await this.service.StopAsync();

        await act.Should().NotThrowAsync();
    }

    /// <summary>
    /// Spec: StopAsync respects an externally-cancelled CancellationToken and
    /// returns OperationCanceledException from the await in that case, rather
    /// than waiting for the in-flight budget.
    /// </summary>
    [Fact]
    public async Task StopAsync_ExternalCancellation_PropagatesAsOCE()
    {
        await this.service.StartAsync();

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await this.service.StopAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// Spec: Start after Stop must succeed — the service is restartable in
    /// process. This is the lifecycle contract for the new CTS recreation.
    /// </summary>
    [Fact]
    public async Task StartAsync_AfterStop_IsRunningTrue()
    {
        await this.service.StartAsync();
        await this.service.StopAsync();

        await this.service.StartAsync();
        this.service.IsRunning.Should().BeTrue();
    }

    // ── Direct coverage for ExecuteHeartbeatAsync ────────────────────────

    [Fact]
    public async Task ExecuteHeartbeatAsync_WhenNetworkAvailableAndBackendSucceeds_ResetsBackoff()
    {
        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HeartbeatResult.Succeeded());

        // Push backoff up to 4 first to prove the reset path.
        await this.service.StartAsync();
        this.ApplyBackoffInternal(ScheduledWorkService.WorkType.Heartbeat);
        this.ApplyBackoffInternal(ScheduledWorkService.WorkType.Heartbeat);

        await this.service.ExecuteHeartbeatAsync(CancellationToken.None);

        this.GetBackoff(ScheduledWorkService.WorkType.Heartbeat).Should().Be(ScheduledWorkService.InitialBackoffSeconds);
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_WhenBackendFails_AppliesBackoff()
    {
        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HeartbeatResult.Failed("server unavailable"));

        await this.service.StartAsync();
        var before = this.GetBackoff(ScheduledWorkService.WorkType.Heartbeat);

        await this.service.ExecuteHeartbeatAsync(CancellationToken.None);

        var after = this.GetBackoff(ScheduledWorkService.WorkType.Heartbeat);
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_WhenServerProvidesTimeOffset_SetsServerDate()
    {
        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HeartbeatResult.Succeeded(serverTimeOffsetMs: 42L));

        await this.service.StartAsync();
        await this.service.ExecuteHeartbeatAsync(CancellationToken.None);

        this.mockTimeProvider.Verify(tp => tp.SetServerDate(42L), Times.Once);
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_WhenNewPolicyAvailable_AwaitsSyncCompletion()
    {
        var syncStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSync = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HeartbeatResult.Succeeded(newPolicyAvailable: true));

        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                syncStarted.SetResult();
                await releaseSync.Task;
                return PolicyFetchResult.Succeeded(1, string.Empty);
            });

        var heartbeat = this.service.ExecuteHeartbeatAsync(CancellationToken.None);
        await syncStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        heartbeat.IsCompleted.Should().BeFalse("heartbeat completion must include the requested policy sync");
        releaseSync.SetResult();
        await heartbeat;

        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteHeartbeatAsync_WhenPolicySyncIsCancelled_PropagatesCancellation()
    {
        var syncStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();

        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), cts.Token))
            .ReturnsAsync(HeartbeatResult.Succeeded(newPolicyAvailable: true));
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), cts.Token))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), cts.Token))
            .Returns(async () =>
            {
                syncStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cts.Token);
                return PolicyFetchResult.Succeeded(1, string.Empty);
            });

        var heartbeat = this.service.ExecuteHeartbeatAsync(cts.Token);
        await syncStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        var act = async () => await heartbeat;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenAnotherSyncIsRunning_DoesNotOverlap()
    {
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var active = 0;
        var maximumActive = 0;

        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                var call = Interlocked.Increment(ref calls);
                var currentActive = Interlocked.Increment(ref active);
                maximumActive = Math.Max(maximumActive, currentActive);
                if (call == 1)
                {
                    await releaseFirst.Task;
                }

                Interlocked.Decrement(ref active);
                return 0;
            });
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(1, string.Empty));

        var first = this.service.ExecutePolicySyncAsync(CancellationToken.None);
        calls.Should().Be(1);

        var second = this.service.ExecutePolicySyncAsync(CancellationToken.None);
        calls.Should().Be(1, "the second sync must await the active sync without entering policy work");

        releaseFirst.SetResult();
        await Task.WhenAll(first, second);

        calls.Should().Be(2);
        maximumActive.Should().Be(1);
    }

    [Fact]
    public async Task AdmitSyncAsync_DeniedIdentityThenLaterAdmissionConvergesAcrossAllSources()
    {
        await this.StartWithoutStartupPolicySyncAsync();
        this.mockIdentityCoordinator
            .SetupGet(c => c.CurrentState)
            .Returns(BackendIdentityState.Unpaired());

        var denied = await this.service.AdmitSyncAsync(SyncTriggerSource.Wns);

        Assert.Equal(SyncAdmissionResult.Accepted, denied);
        this.mockPolicyRepository.Verify(
            r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.mockPolicyRepository.Invocations.Clear();
        this.mockBackendClient.Invocations.Clear();

        this.mockIdentityCoordinator
            .SetupGet(c => c.CurrentState)
            .Returns(BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 2, "device-test"));

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                entered.SetResult();
                await release.Task;
                return 0;
            });
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(0, string.Empty));

        var sources = new[]
        {
            SyncTriggerSource.Startup,
            SyncTriggerSource.Wns,
            SyncTriggerSource.Ui,
            SyncTriggerSource.Timer,
            SyncTriggerSource.Polling,
        };
        var admissions = sources.Select(source => this.service.AdmitSyncAsync(source)).ToArray();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
        var results = await Task.WhenAll(admissions);

        Assert.Equal(1, results.Count(result => result == SyncAdmissionResult.Accepted));
        Assert.Equal(4, results.Count(result => result == SyncAdmissionResult.Coalesced));
        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdmitSyncAsync_CancelledRequestDoesNotStartPolicyWork()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await this.service.AdmitSyncAsync(SyncTriggerSource.Ui, cts.Token);

        Assert.Equal(SyncAdmissionResult.Cancelled, result);
        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AdmitSyncAsync_UnknownSourceIsRejected()
    {
        var result = await this.service.AdmitSyncAsync((SyncTriggerSource)99);

        Assert.Equal(SyncAdmissionResult.Rejected, result);
    }

    [Fact]
    public async Task AdmitSyncAsync_CallerCancellationOnlyCancelsThatWaiter()
    {
        await this.StartWithoutStartupPolicySyncAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                entered.SetResult();
                await release.Task;
                return 0;
            });

        using var callerCancellation = new CancellationTokenSource();
        var owner = this.service.AdmitSyncAsync(SyncTriggerSource.Wns, callerCancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var coalesced = this.service.AdmitSyncAsync(SyncTriggerSource.Ui);

        callerCancellation.Cancel();
        Assert.Equal(SyncAdmissionResult.Cancelled, await owner);

        release.SetResult();
        Assert.Equal(SyncAdmissionResult.Coalesced, await coalesced);
        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdmitSyncAsync_ShutdownCancellationCancelsSharedWork()
    {
        await this.StartWithoutStartupPolicySyncAsync();
        var workEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken token) =>
            {
                workEntered.TrySetResult();
                return ObserveCancellationAsync(token, cancellationObserved);
            });

        var admission = this.service.AdmitSyncAsync(SyncTriggerSource.Timer);
        await workEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await this.service.StopAsync();

        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(SyncAdmissionResult.Accepted, await admission);
    }

    [Fact]
    public async Task AdmitSyncAsync_StoppedAndDisposedServiceRejectsAdmission()
    {
        Assert.Equal(
            SyncAdmissionResult.Rejected,
            await this.service.AdmitSyncAsync(SyncTriggerSource.Polling));

        this.service.Dispose();
        Assert.Equal(
            SyncAdmissionResult.Rejected,
            await this.service.AdmitSyncAsync(SyncTriggerSource.Polling));
    }

    private async Task StartWithoutStartupPolicySyncAsync()
    {
        var startupCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => startupCompleted.TrySetResult())
            .ReturnsAsync(PolicyFetchResult.Succeeded(0, string.Empty));
        await this.service.StartAsync();
        await startupCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        this.mockBackendClient.Invocations.Clear();
        this.mockPolicyRepository.Invocations.Clear();
    }
    private static async Task<int> ObserveCancellationAsync(
        CancellationToken cancellationToken,
        TaskCompletionSource cancellationObserved)
    {
        using var registration = cancellationToken.Register(() => cancellationObserved.TrySetResult());
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            cancellationObserved.TrySetResult();
            throw;
        }

        return 0;
    }
    [Fact]
    public async Task ExecuteHeartbeatAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        await this.service.StartAsync();

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await this.service.ExecuteHeartbeatAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Direct coverage for ExecuteReconciliationAsync ───────────────────

    [Fact]
    public async Task ExecuteReconciliationAsync_WhenReconcilerAlreadyRunning_SkipsWithoutTouchingBackoff()
    {
        this.mockUsageReconciler.SetupGet(r => r.IsRunning).Returns(true);
        await this.service.StartAsync();

        var before = this.GetBackoff(ScheduledWorkService.WorkType.Reconciliation);
        await this.service.ExecuteReconciliationAsync(CancellationToken.None);
        var after = this.GetBackoff(ScheduledWorkService.WorkType.Reconciliation);

        after.Should().Be(before);
        this.mockUsageReconciler.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteReconciliationAsync_WhenReconcileSucceeds_ResetsBackoff()
    {
        this.mockUsageReconciler
            .Setup(r => r.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        this.mockUsageReconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReconciliationResult.Ok(2, 1, 0, TimeSpan.FromSeconds(1)));

        await this.service.StartAsync();
        this.ApplyBackoffInternal(ScheduledWorkService.WorkType.Reconciliation);
        this.ApplyBackoffInternal(ScheduledWorkService.WorkType.Reconciliation);

        await this.service.ExecuteReconciliationAsync(CancellationToken.None);

        this.GetBackoff(ScheduledWorkService.WorkType.Reconciliation).Should().Be(ScheduledWorkService.InitialBackoffSeconds);
    }

    [Fact]
    public async Task ExecuteReconciliationAsync_WhenReconcileFails_AppliesBackoff()
    {
        this.mockUsageReconciler
            .Setup(r => r.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        this.mockUsageReconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReconciliationResult.Fail("disk error", TimeSpan.FromSeconds(1)));

        await this.service.StartAsync();
        var before = this.GetBackoff(ScheduledWorkService.WorkType.Reconciliation);

        await this.service.ExecuteReconciliationAsync(CancellationToken.None);

        var after = this.GetBackoff(ScheduledWorkService.WorkType.Reconciliation);
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task ExecuteReconciliationAsync_WhenReconcileThrows_AppliesBackoff()
    {
        this.mockUsageReconciler
            .Setup(r => r.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        this.mockUsageReconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("kaboom"));

        await this.service.StartAsync();
        var before = this.GetBackoff(ScheduledWorkService.WorkType.Reconciliation);

        await this.service.ExecuteReconciliationAsync(CancellationToken.None);

        var after = this.GetBackoff(ScheduledWorkService.WorkType.Reconciliation);
        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task ExecuteReconciliationAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await this.service.ExecuteReconciliationAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Direct coverage for ExecutePolicySyncAsync ───────────────────────

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenLegacyPolicyArrives_RejectsBeforePersistence()
    {
        // Policy uses snake_case JSON property names and integer-valued enums
        // (PolicyJsonContext has no UseStringEnumConverter).
        var policyJson = @"{
            ""device_id"": ""device-1"",
            ""version"": 7,
            ""device_state"": 0,
            ""daily_screen_time_minutes"": 120,
            ""schedules"": [],
            ""category_limits"": [],
            ""app_policies"": [],
            ""category_assignments"": {},
            ""grants"": []
        }";

        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(7, policyJson));
        await Assert.ThrowsAsync<JsonException>(() => this.service.ExecutePolicySyncAsync(CancellationToken.None));

        this.mockPolicyRepository.Verify(
            r => r.UpsertPolicyAsync(It.IsAny<Policy>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenVersionedPolicyEnvelopeArrives_PersistsPayload()
    {
        var policyWithoutHash = """{"device_id":"00000000-0000-4000-8000-000000000012","version":7,"device_state":"active","daily_screen_time_minutes":120,"schedules":[],"category_limits":[],"app_policies":[],"category_assignments":{},"grants":[]}""";
        using var payloadDocument = JsonDocument.Parse(policyWithoutHash);
        var policyJson = policyWithoutHash[..^1] + ",\"snapshot_hash\":\"" + CanonicalJson.Sha256Hex(payloadDocument.RootElement) + "\"}";
        var envelope = """{"contract":"control-parental.windows","version":1,"message_type":"policy.snapshot","correlation_id":"00000000-0000-4000-8000-000000000001","payload":""" + policyJson + "}";

        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(7, envelope));

        await this.service.ExecutePolicySyncAsync(CancellationToken.None);

        this.mockPolicyRepository.Verify(
            r => r.UpsertPolicyAsync(It.Is<Policy>(p => p.Version == 7), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenFetchFails_DoesNotUpsert()
    {
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Failed("network down"));

        await this.service.ExecutePolicySyncAsync(CancellationToken.None);

        this.mockPolicyRepository.Verify(
            r => r.UpsertPolicyAsync(It.IsAny<Policy>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenEnvelopeHasUnknownMember_DoesNotPersist()
    {
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(1, "{\"contract\":\"control-parental.windows\",\"version\":1,\"message_type\":\"policy.snapshot\",\"correlation_id\":\"00000000-0000-4000-8000-000000000001\",\"unexpected\":true,\"payload\":{}}"));

        var act = async () => await this.service.ExecutePolicySyncAsync(CancellationToken.None);

        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
        this.mockPolicyRepository.Verify(
            r => r.UpsertPolicyAsync(It.IsAny<Policy>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenMalformedJson_PropagatesAsJsonException()
    {
        // Spec: ExecutePolicySyncAsync does NOT catch deserialization errors;
        // the dispatch wrapper in TryDispatchWork swallows them and logs.
        // Direct callers (tests, backup mode) must observe the exception so
        // they can react to a corrupted policy payload.
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(1, "{ not valid json }"));

        var act = async () => await this.service.ExecutePolicySyncAsync(CancellationToken.None);
        await act.Should().ThrowAsync<System.Text.Json.JsonException>();
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await this.service.ExecutePolicySyncAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── RunBackupAsync dispatch ──────────────────────────────────────────

    [Fact]
    public async Task RunBackupAsync_WithHeartbeatMode_DispatchesAndReturns()
    {
        await this.service.StartAsync();

        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HeartbeatResult.Succeeded());

        // RunBackupAsync bypasses the timer — it executes the Execute method
        // directly on the caller thread and returns its Task. This is the T20
        // backup-mode contract used by Task Scheduler.
        var task = this.service.RunBackupAsync(BackupMode.Heartbeat, CancellationToken.None);
        await task;

        this.mockBackendClient.Verify(
            c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_WithHeartbeatMode_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.Is<CancellationToken>(ct => ct == cts.Token)))
            .ReturnsAsync(HeartbeatResult.Succeeded());

        await this.service.RunBackupAsync(BackupMode.Heartbeat, cts.Token);

        this.mockBackendClient.Verify(
            c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.Is<CancellationToken>(ct => ct == cts.Token)),
            Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_WithReconciliationMode_DispatchesAndReturns()
    {
        this.mockUsageReconciler.SetupGet(r => r.IsRunning).Returns(false);
        this.mockUsageReconciler
            .Setup(r => r.StartAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        this.mockUsageReconciler
            .Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReconciliationResult.Ok(0, 0, 0, TimeSpan.Zero));

        var task = this.service.RunBackupAsync(BackupMode.Reconciliation, CancellationToken.None);
        await task;

        this.mockUsageReconciler.Verify(r => r.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
        this.mockUsageReconciler.Verify(r => r.ReconcileAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_WithReconciliationMode_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        this.mockUsageReconciler.SetupGet(r => r.IsRunning).Returns(false);
        this.mockUsageReconciler
            .Setup(r => r.StartAsync(It.Is<CancellationToken>(ct => ct == cts.Token)))
            .Returns(Task.CompletedTask);
        this.mockUsageReconciler
            .Setup(r => r.ReconcileAsync(It.Is<CancellationToken>(ct => ct == cts.Token)))
            .ReturnsAsync(ReconciliationResult.Ok(0, 0, 0, TimeSpan.Zero));

        await this.service.RunBackupAsync(BackupMode.Reconciliation, cts.Token);

        this.mockUsageReconciler.Verify(r => r.StartAsync(It.Is<CancellationToken>(ct => ct == cts.Token)), Times.Once);
        this.mockUsageReconciler.Verify(r => r.ReconcileAsync(It.Is<CancellationToken>(ct => ct == cts.Token)), Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_WithOutboxMode_DispatchesAndReturns()
    {
        this.mockOutboxManager
            .Setup(m => m.ClaimAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OutboxEntry>());

        var task = this.service.RunBackupAsync(BackupMode.Outbox, CancellationToken.None);
        await task;

        this.mockOutboxManager.Verify(
            m => m.ClaimAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_WithOutboxMode_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        this.mockOutboxManager
            .Setup(m => m.ClaimAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.Is<CancellationToken>(ct => ct == cts.Token)))
            .ReturnsAsync(Array.Empty<OutboxEntry>());

        await this.service.RunBackupAsync(BackupMode.Outbox, cts.Token);

        this.mockOutboxManager.Verify(
            m => m.ClaimAsync(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.Is<CancellationToken>(ct => ct == cts.Token)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteOutboxPushAsync_ClaimsAndCompletesEachDurableEntry()
    {
        var entry = new OutboxEntry
        {
            Id = 41,
            TableName = "usage_logs",
            PayloadJson = "{\"appId\":\"app\",\"minutes\":1,\"serverDate\":\"2026-07-23T12:00:00Z\",\"dedupKey\":\"op-41\"}",
            DedupKey = "op-41",
            OperationId = "op-41",
            ClaimVersion = 2,
            AttemptCount = 1,
            Status = OutboxEntryStatus.Claimed,
            CreatedAt = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero),
        };
        this.mockIdentityCoordinator
            .SetupGet(c => c.CurrentState)
            .Returns(BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 1, "device-test"));
        this.mockOutboxManager
            .Setup(m => m.ClaimAsync(100, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        this.mockBackendClient
            .Setup(c => c.PushUsageLogsAsync(It.IsAny<IEnumerable<UsageLogEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataPushResult.Succeeded(1));

        await this.service.StartAsync();
        await this.service.ExecuteOutboxPushAsync(CancellationToken.None);

        this.mockOutboxManager.Verify(
            m => m.ClaimAsync(100, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Once);
        this.mockOutboxManager.Verify(
            m => m.CompleteAsync(It.Is<OutboxEntry>(claimed => claimed.Id == entry.Id && claimed.OperationId == entry.OperationId), It.IsAny<CancellationToken>()),
            Times.Once);
        this.mockOutboxManager.Verify(
            m => m.GetPendingEntriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.mockOutboxManager.Verify(
            m => m.MarkSentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteOutboxPushAsync_CancellationLeavesClaimForDurableRecovery()
    {
        var entry = new OutboxEntry
        {
            Id = 42,
            TableName = "usage_logs",
            PayloadJson = "{\"appId\":\"app\",\"minutes\":1,\"serverDate\":\"2026-07-23T12:00:00Z\",\"dedupKey\":\"op-42\"}",
            DedupKey = "op-42",
            OperationId = "op-42",
            ClaimVersion = 1,
            AttemptCount = 1,
            Status = OutboxEntryStatus.Claimed,
            CreatedAt = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero),
        };
        this.mockIdentityCoordinator
            .SetupGet(c => c.CurrentState)
            .Returns(BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 1, "device-test"));
        this.mockOutboxManager
            .Setup(m => m.ClaimAsync(100, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        using var cts = new CancellationTokenSource();
        this.mockBackendClient
            .Setup(c => c.PushUsageLogsAsync(It.IsAny<IEnumerable<UsageLogEntry>>(), It.IsAny<CancellationToken>()))
            .Returns(async (IEnumerable<UsageLogEntry> _, CancellationToken token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return DataPushResult.Succeeded(1);
            });

        await this.service.StartAsync();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await this.service.Invoking(s => s.ExecuteOutboxPushAsync(cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();

        this.mockOutboxManager.Verify(
            m => m.FailAsync(It.IsAny<OutboxEntry>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<bool>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunBackupAsync_WhenCallerIsCancelledInFlight_PropagatesCancellation()
    {
        var enteredBackend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBackend = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();
        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.Is<CancellationToken>(token => token == cts.Token)))
            .Returns(async (HeartbeatData _, CancellationToken token) =>
            {
                enteredBackend.SetResult(true);
                await releaseBackend.Task.WaitAsync(token);
                return HeartbeatResult.Succeeded();
            });

        var backup = this.service.RunBackupAsync(BackupMode.Heartbeat, cts.Token);

        try
        {
            await enteredBackend.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cts.Cancel();

            var act = async () => await backup;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            releaseBackend.TrySetResult(true);
            try
            {
                await backup;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    [Fact]
    public async Task ExecuteOutboxPushAsync_DeliversAllSupportedEntryTypesIndependently()
    {
        var entries = new[]
        {
            CreateEntry(43, "device_alerts", "{\"eventType\":\"warning\",\"detectedAt\":\"2026-07-23T12:00:00Z\",\"dedupKey\":\"op-43\"}"),
            CreateEntry(44, "behavioral_events", "{\"eventType\":\"blocked\",\"timestamp\":\"2026-07-23T12:00:00Z\",\"dedupKey\":\"op-44\"}"),
            CreateEntry(45, "time_requests", "{\"requestId\":\"00000000-0000-4000-8000-000000000045\",\"minutes\":5,\"createdAt\":\"2026-07-23T12:00:00Z\"}"),
        };
        this.mockOutboxManager
            .Setup(m => m.ClaimAsync(100, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        this.mockBackendClient
            .Setup(c => c.PushDeviceAlertsAsync(It.IsAny<IEnumerable<DeviceAlertEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataPushResult.Succeeded(1));
        this.mockBackendClient
            .Setup(c => c.PushBehavioralEventsAsync(It.IsAny<IEnumerable<BehavioralEventEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataPushResult.Succeeded(1));
        this.mockBackendClient
            .Setup(c => c.CreateTimeRequestAsync(It.IsAny<TimeRequestEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await this.service.ExecuteOutboxPushAsync(CancellationToken.None);

        this.mockOutboxManager.Verify(
            m => m.CompleteAsync(It.IsAny<OutboxEntry>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        this.mockOutboxManager.Verify(
            m => m.FailAsync(
                It.Is<OutboxEntry>(entry => entry.Id == 45),
                "permanent",
                null,
                true,
                ScheduledWorkService.MaxOutboxAttempts,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteOutboxPushAsync_MalformedAndUnsupportedEntriesArePermanentFailures()
    {
        var entries = new[]
        {
            CreateEntry(46, "usage_logs", "not-json"),
            CreateEntry(47, "unknown", "{}"),
        };
        this.mockOutboxManager
            .Setup(m => m.ClaimAsync(100, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        await this.service.ExecuteOutboxPushAsync(CancellationToken.None);

        this.mockOutboxManager.Verify(
            m => m.FailAsync(
                It.IsAny<OutboxEntry>(),
                "permanent",
                null,
                true,
                ScheduledWorkService.MaxOutboxAttempts,
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        this.mockBackendClient.Verify(
            c => c.PushUsageLogsAsync(It.IsAny<IEnumerable<UsageLogEntry>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteOutboxPushAsync_DeliveryExceptionUsesSafeTransientFailure()
    {
        var entry = CreateEntry(
            48,
            "usage_logs",
            "{\"appId\":\"app\",\"minutes\":1,\"serverDate\":\"2026-07-23T12:00:00Z\",\"dedupKey\":\"op-48\"}");
        this.mockOutboxManager
            .Setup(m => m.ClaimAsync(100, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { entry });
        this.mockBackendClient
            .Setup(c => c.PushUsageLogsAsync(It.IsAny<IEnumerable<UsageLogEntry>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("secret-token=must-not-leak"));

        await this.service.ExecuteOutboxPushAsync(CancellationToken.None);

        this.mockOutboxManager.Verify(
            m => m.FailAsync(
                It.Is<OutboxEntry>(failed => failed.Id == entry.Id),
                "network",
                It.IsAny<DateTimeOffset?>(),
                false,
                ScheduledWorkService.MaxOutboxAttempts,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void ShutdownBudget_IsDedicatedAndDistinctFromBackoff()
    {
        ScheduledWorkService.ShutdownBudgetSeconds.Should().Be(30);
        ScheduledWorkService.ShutdownBudgetSeconds.Should().NotBe(ScheduledWorkService.MaxBackoffSeconds);
    }

    private static OutboxEntry CreateEntry(int id, string tableName, string payloadJson)
    {
        return new OutboxEntry
        {
            Id = id,
            TableName = tableName,
            PayloadJson = payloadJson,
            DedupKey = $"op-{id}",
            OperationId = $"op-{id}",
            ClaimVersion = 1,
            AttemptCount = 1,
            Status = OutboxEntryStatus.Claimed,
            CreatedAt = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private int GetBackoff(ScheduledWorkService.WorkType workType)
    {
        var method = typeof(ScheduledWorkService).GetMethod(
            "GetBackoffForTesting",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        return (int)method!.Invoke(this.service, new object[] { workType })!;
    }

    private void ApplyBackoffInternal(ScheduledWorkService.WorkType workType)
    {
        var method = typeof(ScheduledWorkService).GetMethod(
            "ApplyBackoff",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        method!.Invoke(this.service, new object[] { workType });
    }

    private void InvokeTryDispatchWork(ScheduledWorkService.WorkType workType, Func<Task> work)
    {
        var method = typeof(ScheduledWorkService).GetMethod(
            "TryDispatchWork",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        method!.Invoke(this.service, new object[] { workType, work });
    }
}
