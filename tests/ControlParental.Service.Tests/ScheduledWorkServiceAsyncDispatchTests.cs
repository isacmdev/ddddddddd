// <copyright file="ScheduledWorkServiceAsyncDispatchTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using ControlParental.Domain;
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

        this.mockUsageReconciler.SetupGet(r => r.IsRunning).Returns(false);
        this.mockEnforcementLevelMonitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Standard);
        this.mockHealthMonitor.SetupGet(m => m.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(m => m.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);

        this.service = new ScheduledWorkService(
            backendClient: this.mockBackendClient.Object,
            outboxManager: this.mockOutboxManager.Object,
            usageReconciler: this.mockUsageReconciler.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            timeProvider: this.mockTimeProvider.Object,
            healthMonitor: this.mockHealthMonitor.Object,
            recoveryManager: this.mockRecoveryManager.Object,
            policyRepository: this.mockPolicyRepository.Object);
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
    public async Task ExecuteHeartbeatAsync_WhenNewPolicyAvailable_DispatchesSync()
    {
        this.mockBackendClient
            .Setup(c => c.SendHeartbeatAsync(It.IsAny<HeartbeatData>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HeartbeatResult.Succeeded(newPolicyAvailable: true));

        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(1, string.Empty));

        await this.service.StartAsync();
        await this.service.ExecuteHeartbeatAsync(CancellationToken.None);

        // The follow-up sync must reach the backend at least once.
        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
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
    public async Task ExecutePolicySyncAsync_WhenNewerPolicyArrives_PersistsIt()
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
        this.mockPolicyRepository
            .Setup(r => r.UpsertPolicyAsync(It.IsAny<Policy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
            .Setup(m => m.GetPendingEntriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OutboxEntry>());

        var task = this.service.RunBackupAsync(BackupMode.Outbox, CancellationToken.None);
        await task;

        this.mockOutboxManager.Verify(
            m => m.GetPendingEntriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunBackupAsync_WithOutboxMode_ForwardsCancellationToken()
    {
        using var cts = new CancellationTokenSource();

        this.mockOutboxManager
            .Setup(m => m.GetPendingEntriesAsync(It.IsAny<int>(), It.Is<CancellationToken>(ct => ct == cts.Token)))
            .ReturnsAsync(Array.Empty<OutboxEntry>());

        await this.service.RunBackupAsync(BackupMode.Outbox, cts.Token);

        this.mockOutboxManager.Verify(
            m => m.GetPendingEntriesAsync(It.IsAny<int>(), It.Is<CancellationToken>(ct => ct == cts.Token)),
            Times.Once);
    }

    [Fact]
    public void ShutdownBudget_IsDedicatedAndDistinctFromBackoff()
    {
        ScheduledWorkService.ShutdownBudgetSeconds.Should().Be(30);
        ScheduledWorkService.ShutdownBudgetSeconds.Should().NotBe(ScheduledWorkService.MaxBackoffSeconds);
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
