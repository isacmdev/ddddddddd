// <copyright file="AntiTamperMonitorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Collections.Concurrent;
using System.Threading.Channels;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

/// <summary>
/// T13 — Tests for AntiTamperMonitor.
/// </summary>
public class AntiTamperMonitorTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private readonly Mock<ITimeProvider> mockTimeProvider;
    private readonly Mock<IOutboxManager> mockOutboxManager;
    private readonly Mock<IPrivilegeInspector> mockPrivilegeInspector;
    private readonly Mock<IEnforcementLevelMonitor> mockEnforcementLevelMonitor;
    private readonly Mock<IIntegrityChecker> mockIntegrityChecker;
    private readonly Mock<IBackendClient> mockBackendClient;
    private readonly Mock<IIntegrityVerdictHandler> mockVerdictHandler;
    private readonly ConcurrentQueue<TamperEvent> detectedEvents;
    private readonly AntiTamperMonitor monitor;

    public AntiTamperMonitorTests()
    {
        this.mockTimeProvider = new Mock<ITimeProvider>();
        this.mockOutboxManager = new Mock<IOutboxManager>();
        this.mockPrivilegeInspector = new Mock<IPrivilegeInspector>();
        this.mockEnforcementLevelMonitor = new Mock<IEnforcementLevelMonitor>();
        this.mockIntegrityChecker = new Mock<IIntegrityChecker>();
        this.mockBackendClient = new Mock<IBackendClient>();
        this.mockVerdictHandler = new Mock<IIntegrityVerdictHandler>();
        this.detectedEvents = new ConcurrentQueue<TamperEvent>();

        this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(DateTimeOffset.UtcNow);
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(1000L);

        this.monitor = new AntiTamperMonitor(
            timeProvider: this.mockTimeProvider.Object,
            outboxManager: this.mockOutboxManager.Object,
            privilegeInspector: this.mockPrivilegeInspector.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            integrityChecker: this.mockIntegrityChecker.Object,
            backendClient: this.mockBackendClient.Object,
            verdictHandler: this.mockVerdictHandler.Object,
            onTamperDetected: e => this.detectedEvents.Enqueue(e));
    }

    public void Dispose()
    {
        this.monitor.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AntiTamperMonitor(
            timeProvider: null!,
            outboxManager: this.mockOutboxManager.Object,
            privilegeInspector: this.mockPrivilegeInspector.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            integrityChecker: this.mockIntegrityChecker.Object,
            backendClient: this.mockBackendClient.Object,
            verdictHandler: this.mockVerdictHandler.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("timeProvider");
    }

    [Fact]
    public void Constructor_WithNullOutboxManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AntiTamperMonitor(
            timeProvider: this.mockTimeProvider.Object,
            outboxManager: null!,
            privilegeInspector: this.mockPrivilegeInspector.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            integrityChecker: this.mockIntegrityChecker.Object,
            backendClient: this.mockBackendClient.Object,
            verdictHandler: this.mockVerdictHandler.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("outboxManager");
    }

    [Fact]
    public void Constructor_WithNullPrivilegeInspector_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AntiTamperMonitor(
            timeProvider: this.mockTimeProvider.Object,
            outboxManager: this.mockOutboxManager.Object,
            privilegeInspector: null!,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            integrityChecker: this.mockIntegrityChecker.Object,
            backendClient: this.mockBackendClient.Object,
            verdictHandler: this.mockVerdictHandler.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("privilegeInspector");
    }

    [Fact]
    public void Constructor_WithNullEnforcementLevelMonitor_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AntiTamperMonitor(
            timeProvider: this.mockTimeProvider.Object,
            outboxManager: this.mockOutboxManager.Object,
            privilegeInspector: this.mockPrivilegeInspector.Object,
            enforcementLevelMonitor: null!,
            integrityChecker: this.mockIntegrityChecker.Object,
            backendClient: this.mockBackendClient.Object,
            verdictHandler: this.mockVerdictHandler.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("enforcementLevelMonitor");
    }

    [Fact]
    public void Constructor_WithNullIntegrityChecker_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AntiTamperMonitor(
            timeProvider: this.mockTimeProvider.Object,
            outboxManager: this.mockOutboxManager.Object,
            privilegeInspector: this.mockPrivilegeInspector.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            integrityChecker: null!,
            backendClient: this.mockBackendClient.Object,
            verdictHandler: this.mockVerdictHandler.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("integrityChecker");
    }

    [Fact]
    public void Constructor_WithNullBackendClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AntiTamperMonitor(
            timeProvider: this.mockTimeProvider.Object,
            outboxManager: this.mockOutboxManager.Object,
            privilegeInspector: this.mockPrivilegeInspector.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            integrityChecker: this.mockIntegrityChecker.Object,
            backendClient: null!,
            verdictHandler: this.mockVerdictHandler.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("backendClient");
    }

    [Fact]
    public void Constructor_WithNullVerdictHandler_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new AntiTamperMonitor(
            timeProvider: this.mockTimeProvider.Object,
            outboxManager: this.mockOutboxManager.Object,
            privilegeInspector: this.mockPrivilegeInspector.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            integrityChecker: this.mockIntegrityChecker.Object,
            backendClient: this.mockBackendClient.Object,
            verdictHandler: null!);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("verdictHandler");
    }

    // ── Initial State Tests ─────────────────────────────────────────

    [Fact]
    public void InitialState_DetectedEventsIsEmpty()
    {
        // Assert
        this.monitor.DetectedEvents.Should().BeEmpty();
    }

    [Fact]
    public void InitialState_ClockJumpDetectedIsFalse()
    {
        // Assert
        this.monitor.ClockJumpDetected.Should().BeFalse();
    }

    [Fact]
    public void InitialState_TimezoneChangedDetectedIsFalse()
    {
        // Assert
        this.monitor.TimezoneChangedDetected.Should().BeFalse();
    }

    // ── RecordServiceStopAttempt Tests ───────────────────────────────

    [Fact]
    public void RecordServiceStopAttempt_AddsEventToDetectedEvents()
    {
        // Arrange
        TamperEvent? capturedEvent = null;
        this.monitor.TamperDetected += (_, args) => capturedEvent = args.Event;

        // Act
        this.monitor.RecordServiceStopAttempt("Test reason");

        // Assert
        this.monitor.DetectedEvents.Should().HaveCount(1);
        this.monitor.DetectedEvents[0].Type.Should().Be(TamperEventType.ServiceStopAttempt);
        this.monitor.DetectedEvents[0].Description.Should().Be("Test reason");
        this.monitor.DetectedEvents[0].Severity.Should().Be(TamperSeverity.Critical);

        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(TamperEventType.ServiceStopAttempt);
    }

    [Fact]
    public void RecordServiceStopAttempt_WithoutReason_UsesDefaultDescription()
    {
        // Act
        this.monitor.RecordServiceStopAttempt();

        // Assert
        this.monitor.DetectedEvents.Should().HaveCount(1);
        this.monitor.DetectedEvents[0].Description.Should().Be("Service stop attempt detected");
    }

    [Fact]
    public async Task RecordServiceStopAttempt_EnqueuesToOutbox()
    {
        // Act
        this.monitor.RecordServiceStopAttempt();

        // Allow async operation to complete
        await Task.Delay(100);

        // Assert
        this.mockOutboxManager.Verify(
            o => o.EnqueueAsync(
                "device_alerts",
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── RecordAgentDeath Tests ─────────────────────────────────────

    [Fact]
    public void RecordAgentDeath_AddsEventToDetectedEvents()
    {
        // Arrange
        TamperEvent? capturedEvent = null;
        this.monitor.TamperDetected += (_, args) => capturedEvent = args.Event;

        // Act
        this.monitor.RecordAgentDeath(42);

        // Assert
        this.monitor.DetectedEvents.Should().HaveCount(1);
        this.monitor.DetectedEvents[0].Type.Should().Be(TamperEventType.AgentKillDetected);
        this.monitor.DetectedEvents[0].Description.Should().Contain("exit code 42");
        this.monitor.DetectedEvents[0].Severity.Should().Be(TamperSeverity.Severe);

        capturedEvent.Should().NotBeNull();
    }

    [Fact]
    public void RecordAgentDeath_WithoutExitCode_UsesDefaultDescription()
    {
        // Act
        this.monitor.RecordAgentDeath();

        // Assert
        this.monitor.DetectedEvents.Should().HaveCount(1);
        this.monitor.DetectedEvents[0].Description.Should().Be("Agent death detected");
    }

    // ── RecordUninstallAttempt Tests ────────────────────────────────

    [Fact]
    public void RecordUninstallAttempt_AddsEventToDetectedEvents()
    {
        // Arrange
        TamperEvent? capturedEvent = null;
        this.monitor.TamperDetected += (_, args) => capturedEvent = args.Event;

        // Act
        this.monitor.RecordUninstallAttempt("TestPackage");

        // Assert
        this.monitor.DetectedEvents.Should().HaveCount(1);
        this.monitor.DetectedEvents[0].Type.Should().Be(TamperEventType.UninstallAttempt);
        this.monitor.DetectedEvents[0].Description.Should().Contain("TestPackage");
        this.monitor.DetectedEvents[0].Severity.Should().Be(TamperSeverity.Severe);

        capturedEvent.Should().NotBeNull();
    }

    [Fact]
    public void RecordUninstallAttempt_WithoutPackageName_UsesDefaultDescription()
    {
        // Act
        this.monitor.RecordUninstallAttempt();

        // Assert
        this.monitor.DetectedEvents.Should().HaveCount(1);
        this.monitor.DetectedEvents[0].Description.Should().Be("Uninstall attempt detected");
    }

    // ── Lifecycle ownership tests ────────────────────────────────────

    [Fact]
    public async Task StartAsync_WhenNotDisposed_StartsSuccessfully()
    {
        // Arrange
        this.ConfigureHealthyBackend();
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.monitor.StartAsync();

        // Assert - no exception
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotThrow()
    {
        // Arrange
        this.ConfigureHealthyBackend();
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await this.monitor.StartAsync();
        // Act & Assert - should not throw
        var act = () => this.monitor.StartAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_WhenDisposed_ThrowsObjectDisposedException()
    {
        // Arrange
        var allocations = (int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!;
        this.monitor.Dispose();
        var disposedMonitor = this.monitor;

        // Act & Assert
        var act = () => disposedMonitor.StartAsync();
        await act.Should().ThrowExactlyAsync<ObjectDisposedException>();
        this.GetGeneration().Should().BeNull(); ((int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!).Should().Be(allocations);
    }

    [Theory, InlineData("success"), InlineData("failure"), InlineData("cancel")]
    public async Task ConcurrentStart_SharesOutcomeAndCleansOneGeneration(string outcome) { using var cts = new CancellationTokenSource(); var entered = NewSignal(); var release = NewSignal(); var failure = new InvalidOperationException("shared initialization failure"); this.ConfigureHealthyBackend(); this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, token) => { entered.SetResult(); if (outcome == "cancel") { await Task.Delay(Timeout.Infinite, token); } await release.Task; if (outcome == "failure") throw failure; return new(false, ""); }); var before = (int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!; var a = this.monitor.StartAsync(outcome == "cancel" ? cts.Token : default); await entered.Task; var b = this.monitor.StartAsync(); var generation = this.GetGeneration(); if (outcome == "cancel") cts.Cancel(); release.SetResult(); if (outcome == "success") await Task.WhenAll(a, b); else { var ea = await Assert.ThrowsAnyAsync<Exception>(() => a); var eb = await Assert.ThrowsAnyAsync<Exception>(() => b); ea.GetType().Should().Be(eb.GetType()); if (outcome == "failure") { ea.Should().BeSameAs(failure); ea.Message.Should().Be(failure.Message); } } this.mockBackendClient.Verify(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()), Times.Once); ((int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!).Should().Be(before + 1); if (outcome == "failure") await Assert.ThrowsAsync<InvalidOperationException>(() => this.monitor.StopAsync()); else await this.monitor.StopAsync(); this.AssertResourcesDisposed(generation); this.GetGeneration().Should().BeNull(); }

    [Fact]
    public async Task StartAndDispose_CancelsOwnedInitializationAndCleansOnce() { var entered = NewSignal(); var cancelled = NewSignal(); var release = NewSignal(); this.ConfigureHealthyBackend(); this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, token) => { entered.SetResult(); using var registration = token.Register(cancelled.SetResult); await release.Task; return new(true, "trust"); }); var start = this.monitor.StartAsync(); await entered.Task; var generation = this.GetGeneration(); var dispose = Task.Factory.StartNew(this.monitor.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default); await cancelled.Task; release.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start); await dispose; this.AssertResourcesDisposed(generation); var allocations = (int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!; await Assert.ThrowsAsync<ObjectDisposedException>(() => this.monitor.StartAsync()); ((int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!).Should().Be(allocations); }

    // ── StopAsync Tests ─────────────────────────────────────────────

    [Fact]
    public async Task StopAsync_WhenRunning_StopsSuccessfully()
    {
        this.ConfigureHealthyBackend();
        // Arrange
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await this.monitor.StartAsync();

        // Act
        await this.monitor.StopAsync();

        // Assert - no exception
    }

    [Fact]
    public async Task StopBeforeStart_AllowsLaterStart() { await this.monitor.StopAsync(); this.ConfigureHealthyBackend(); await this.monitor.StartAsync(); await this.monitor.StopAsync(); }

    [Fact]
    public async Task ConcurrentStop_SharesDrainUntilAdmittedWorkReleases() { this.ConfigureHealthyBackend(); var e = NewSignal(); var r = NewSignal(); var calls = 0; this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { if (Interlocked.Increment(ref calls) == 1) return new(true, "trust"); e.SetResult(); await r.Task; return new(true, "trust"); }); await this.monitor.StartAsync(); var work = this.monitor.TriggerIntegrityCheckAsync(); await e.Task; var a = this.monitor.StopAsync(); var b = this.monitor.StopAsync(); a.IsCompleted.Should().BeFalse(); b.IsCompleted.Should().BeFalse(); r.SetResult(); await Task.WhenAll(work, a, b); }

    [Theory]
    [InlineData("revoked")]
    [InlineData("trust")]
    [InlineData("unknown")]
    public async Task LateNonCancellableNormalCompletion_IsIgnoredAfterStop(string verdict)
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); var calls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { if (Interlocked.Increment(ref calls) == 1) return new(true, "trust"); entered.SetResult(); await release.Task; return new(true, verdict); });
        await this.monitor.StartAsync(); this.mockVerdictHandler.Invocations.Clear(); var work = this.monitor.TriggerIntegrityCheckAsync(); await entered.Task; var stop = this.monitor.StopAsync(); stop.IsCompleted.Should().BeFalse(); this.mockVerdictHandler.Invocations.Should().BeEmpty(); release.SetResult(); await work; await stop; this.mockVerdictHandler.Invocations.Should().BeEmpty(); this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); this.mockOutboxManager.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task LateNonCancellableFailure_IsObservableAndHasNoEffects()
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); var failure = new InvalidOperationException("late integrity failure"); var calls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { if (Interlocked.Increment(ref calls) == 1) return new(true, "trust"); entered.SetResult(); await release.Task; throw failure; });
        await this.monitor.StartAsync(); this.mockVerdictHandler.Invocations.Clear(); var work = this.monitor.TriggerIntegrityCheckAsync(); await entered.Task; var stop = this.monitor.StopAsync(); stop.IsCompleted.Should().BeFalse(); release.SetResult(); var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => work); observed.Should().BeSameAs(failure); await Assert.ThrowsAsync<InvalidOperationException>(() => stop); this.mockVerdictHandler.Invocations.Should().BeEmpty(); this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); this.mockOutboxManager.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task LateNonCancellableFailure_PostRemovalGapRetainsFaultForStop()
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); var gapReached = NewSignal(); var releaseGap = NewSignal(); var failure = new InvalidOperationException("deterministic late integrity failure"); var calls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { if (Interlocked.Increment(ref calls) == 1) return new(true, "trust"); entered.SetResult(); await release.Task; throw failure; });
        await this.monitor.StartAsync(); this.monitor.AdmissionCompletionRemovalGapForTesting = async () => { gapReached.SetResult(); await releaseGap.Task; }; this.mockVerdictHandler.Invocations.Clear(); var work = this.monitor.TriggerIntegrityCheckAsync(); await entered.Task; release.SetResult(); await gapReached.Task;
        try
        {
            var stop = this.monitor.StopAsync(); var stopObserved = await Assert.ThrowsAsync<InvalidOperationException>(() => stop); stopObserved.Should().BeSameAs(failure); stopObserved.Message.Should().Be(failure.Message);
        }
        finally
        {
            releaseGap.SetResult();
        }

        var workObserved = await Assert.ThrowsAsync<InvalidOperationException>(() => work); workObserved.Should().BeSameAs(failure); workObserved.Message.Should().Be(failure.Message); this.mockVerdictHandler.Invocations.Should().BeEmpty(); this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); this.mockOutboxManager.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task OwnerCancellation_BypassesAdmissionFaultAndRemovalGap()
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); var gapReached = NewSignal(); var calls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { if (Interlocked.Increment(ref calls) == 1) return new(true, "trust"); entered.SetResult(); await release.Task; throw new OperationCanceledException(); });
        await this.monitor.StartAsync(); this.monitor.AdmissionCompletionRemovalGapForTesting = () => { gapReached.SetResult(); return Task.CompletedTask; }; this.mockVerdictHandler.Invocations.Clear(); var work = this.monitor.TriggerIntegrityCheckAsync(true); await entered.Task; var stop = this.monitor.StopAsync(); stop.IsCompleted.Should().BeFalse(); release.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work); await stop; gapReached.Task.IsCompleted.Should().BeFalse(); this.mockVerdictHandler.Invocations.Should().BeEmpty(); this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); this.mockOutboxManager.Invocations.Should().BeEmpty();
    }

    [Fact]
    public async Task PostInitialLoopFault_IsObservableAndRestartUsesFreshGeneration()
    {
        this.ConfigureHealthyBackend(); var fault = NewSignal(); var entered = NewSignal(); var failure = new InvalidOperationException("tick fault"); var firstRun = true; using var monitor = this.CreateMonitor(_ => firstRun ? FaultingTick(entered, fault, failure) : new ValueTask<bool>(false));
        await monitor.StartAsync(); await entered.Task; var first = typeof(AntiTamperMonitor).GetField("generation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(monitor); fault.SetResult(); var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.LifecycleTask); observed.Should().BeSameAs(failure); await monitor.StopAsync(); firstRun = false; await monitor.StartAsync(); var second = typeof(AntiTamperMonitor).GetField("generation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(monitor); second.Should().NotBeSameAs(first); await monitor.StopAsync();
    }

    [Fact]
    public async Task ExternalCancellation_IsReturnedToCallerAndOwnerCanDrain()
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); var calls = 0; using var caller = new CancellationTokenSource();
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { if (Interlocked.Increment(ref calls) == 1) return new(true, "trust"); entered.SetResult(); await release.Task; return new(true, "trust"); });
        await this.monitor.StartAsync(); var generation = this.GetGeneration(); var work = this.monitor.TriggerIntegrityCheckAsync(caller.Token); await entered.Task; caller.Cancel(); work.IsCompleted.Should().BeTrue(); var stop = this.monitor.StopAsync(); stop.IsCompleted.Should().BeFalse(); release.SetResult(); var observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work); work.Status.Should().Be(TaskStatus.Canceled); work.IsCanceled.Should().BeTrue(); observed.CancellationToken.CanBeCanceled.Should().BeTrue(); observed.CancellationToken.Should().Be(caller.Token); await stop; stop.IsCompletedSuccessfully.Should().BeTrue(); this.AssertResourcesDisposed(generation); this.GetGeneration().Should().BeNull();
    }

    [Fact]
    public async Task OwnerCancellation_ForwardsOperationTokenAndDrains()
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); CancellationToken operationToken = default; var calls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, token) => { operationToken = token; if (Interlocked.Increment(ref calls) == 1) return new(true, "trust"); entered.SetResult(); await release.Task; throw new OperationCanceledException(); });
        await this.monitor.StartAsync(); var generation = this.GetGeneration(); var work = this.monitor.TriggerIntegrityCheckAsync(true); await entered.Task; var stop = this.monitor.StopAsync(); stop.IsCompleted.Should().BeFalse(); release.SetResult(); var observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work); observed.CancellationToken.CanBeCanceled.Should().BeTrue(); observed.CancellationToken.Should().Be(operationToken); await stop; stop.IsCompletedSuccessfully.Should().BeTrue(); this.AssertResourcesDisposed(generation); this.GetGeneration().Should().BeNull();
    }

    [Fact]
    public async Task QueuedCallerCancellation_DoesNotReleaseUnacquiredGate()
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); var calls = 0; var startup = true; using var caller = new CancellationTokenSource();
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { if (startup) { startup = false; return new(true, "trust"); } Interlocked.Increment(ref calls); entered.SetResult(); await release.Task; return new(true, "trust"); });
        await this.monitor.StartAsync(); var generation = this.GetGeneration(); var first = this.monitor.TriggerIntegrityCheckAsync(); await entered.Task; var before = this.CollaboratorSnapshot(); var queued = this.monitor.TriggerIntegrityCheckAsync(caller.Token); caller.Cancel(); calls.Should().Be(1); var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued); queued.IsCanceled.Should().BeTrue(); canceled.CancellationToken.Should().Be(caller.Token); release.SetResult(); await first; await this.monitor.StopAsync(); this.AssertResourcesDisposed(generation); before[3]++; this.CollaboratorSnapshot().Should().Equal(before); this.monitor.Dispose(); this.AssertResourcesDisposed(generation);
    }

    [Fact]
    public async Task LatePrivilegeCompletion_AfterStopHasNoTamperOrOutboxEffects()
    {
        var entered = NewSignal(); var release = NewSignal(); var calls = 0;
        this.ConfigureHealthyBackend();
        this.mockPrivilegeInspector.Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async _ =>
            {
                if (Interlocked.Increment(ref calls) == 1) return true;
                entered.SetResult(); await release.Task; return false;
            });
        await this.monitor.StartAsync();
        this.detectedEvents.Clear(); this.mockOutboxManager.Invocations.Clear();
        var generation = this.GetGeneration(); var work = this.monitor.TriggerIntegrityCheckAsync(); await entered.Task;
        var before = this.CollaboratorSnapshot();
        var stop = this.monitor.StopAsync(); stop.IsCompleted.Should().BeFalse();
        release.SetResult(); await work; await stop;
        this.detectedEvents.Should().BeEmpty();
        this.mockOutboxManager.Invocations.Should().BeEmpty();
        this.CollaboratorSnapshot().Should().Equal(before); this.AssertResourcesDisposed(generation);
    }

    [Fact]
    public async Task FaultedGeneration_RejectsStaleManualAndTimezoneCallbacksAfterRestart()
    {
        this.ConfigureHealthyBackend(); var fault = NewSignal(); var entered = NewSignal();
        var failure = new InvalidOperationException("stale generation fault"); var firstRun = true;
        var localEvents = new ConcurrentQueue<TamperEvent>(); using var monitor = this.CreateMonitor(_ => firstRun ? FaultingTick(entered, fault, failure) : new ValueTask<bool>(false), localEvents);
        await monitor.StartAsync(); await entered.Task;
        var oldGeneration = this.GetGeneration(monitor)!;
        fault.SetResult(); var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.LifecycleTask); observed.Should().BeSameAs(failure); await monitor.StopAsync();
        firstRun = false; await monitor.StartAsync(); var newGeneration = this.GetGeneration(monitor)!;
        var before = this.CollaboratorSnapshot(localEvents); this.InvokeGeneration(monitor, "AdmitPeriodicCheck", oldGeneration); this.InvokeGeneration(monitor, "AdmitTimezoneCheck", oldGeneration);
        newGeneration.Should().BeSameAs(this.GetGeneration(monitor)); this.CollaboratorSnapshot(localEvents).Should().Equal(before); localEvents.Should().BeEmpty(); await monitor.StopAsync();
        this.AssertResourcesDisposed(newGeneration);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LatePolicyStage_AfterStopDoesNotStartDurableMutation(bool recovery)
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); var calls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()))
            .Returns<IntegrityReport, CancellationToken>(async (_, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1) return new(true, "trust");
                entered.SetResult(); await release.Task; return new(true, recovery ? "trust" : "revoked");
            });
        this.mockVerdictHandler.Setup(v => v.HandleVerdict(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>()))
            .Returns((string? verdict, bool _, DateTimeOffset _) => recovery
                ? new VerdictReaction(VerdictAction.None, null, null)
                : new VerdictReaction(VerdictAction.Degrade, EnforcementIssueSeverity.Severe, "late policy"));
        await this.monitor.StartAsync(); var generation = this.GetGeneration(); this.mockEnforcementLevelMonitor.Invocations.Clear();
        var work = this.monitor.TriggerIntegrityCheckAsync(); await entered.Task; var stop = this.monitor.StopAsync();
        release.SetResult(); await work; await stop;
        this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); this.AssertResourcesDisposed(generation);
    }

    [Fact]
    public async Task ActiveGeneration_UsesCanonicalResolveStageExactlyOnce()
    {
        this.ConfigureHealthyBackend(); var identity = new Mock<IBackendIdentityCoordinator>(); var state = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 7, "device-7"); identity.SetupGet(i => i.CurrentState).Returns(state); var verdictCalls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(true, "trust"));
        this.mockVerdictHandler.Setup(v => v.HandleVerdict(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>())).Returns(() => Interlocked.Increment(ref verdictCalls) == 1 ? new(VerdictAction.None, null, null) : new(VerdictAction.None, null, null) { IsAuthoritativeRecovery = true });
        using var monitor = new AntiTamperMonitor(this.mockTimeProvider.Object, this.mockOutboxManager.Object, this.mockPrivilegeInspector.Object, this.mockEnforcementLevelMonitor.Object, this.mockIntegrityChecker.Object, this.mockBackendClient.Object, this.mockVerdictHandler.Object, identityCoordinator: identity.Object);
        await monitor.StartAsync(); this.mockEnforcementLevelMonitor.Invocations.Clear(); var before = this.CollaboratorSnapshot();
        await monitor.TriggerIntegrityCheckAsync();
        this.mockEnforcementLevelMonitor.Invocations.Count(i => i.Method.Name == nameof(IEnforcementLevelMonitor.ResolveIssueAsync)).Should().Be(1);
        var invocation = this.mockEnforcementLevelMonitor.Invocations.Single(i => i.Method.Name == nameof(IEnforcementLevelMonitor.ResolveIssueAsync)); invocation.Arguments[0].Should().Be(new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", "device-7")); invocation.Arguments[1].Should().Be("authoritative backend trust verdict");
        this.CollaboratorSnapshot()[4].Should().Be(before[4] + 1); await monitor.StopAsync();
    }

    [Fact]
    public async Task StopBeforeResolveAdmission_SuppressesResolveAndPreservesSnapshot()
    {
        this.ConfigureHealthyBackend(); var identity = new Mock<IBackendIdentityCoordinator>(); identity.SetupGet(i => i.CurrentState).Returns(BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 7, "device-7")); Task? stop = null; var verdictCalls = 0; int[]? atStop = null;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(true, "trust"));
        using var monitor = new AntiTamperMonitor(this.mockTimeProvider.Object, this.mockOutboxManager.Object, this.mockPrivilegeInspector.Object, this.mockEnforcementLevelMonitor.Object, this.mockIntegrityChecker.Object, this.mockBackendClient.Object, this.mockVerdictHandler.Object, identityCoordinator: identity.Object);
        this.mockVerdictHandler.Setup(v => v.HandleVerdict(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>())).Returns(() => { if (Interlocked.Increment(ref verdictCalls) == 2) { atStop = this.CollaboratorSnapshot(); stop = monitor.StopAsync(); } return new(VerdictAction.None, null, null) { IsAuthoritativeRecovery = true }; });
        await monitor.StartAsync(); this.mockEnforcementLevelMonitor.Invocations.Clear(); await monitor.TriggerIntegrityCheckAsync(); await stop!;
        this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); this.CollaboratorSnapshot().Should().Equal(atStop!); this.GetGeneration().Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmittedMutationTask_IsOwnedAndDrained(bool recovery)
    {
        this.ConfigureHealthyBackend(); var entered = NewSignal(); var release = NewSignal(); var identity = new Mock<IBackendIdentityCoordinator>(); identity.SetupGet(i => i.CurrentState).Returns(BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 7, "device-7")); var verdictCalls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(true, "trust"));
        this.mockVerdictHandler.Setup(v => v.HandleVerdict(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>())).Returns(() => Interlocked.Increment(ref verdictCalls) == 1 ? new(VerdictAction.None, null, null) : recovery ? new(VerdictAction.None, null, null) { IsAuthoritativeRecovery = true } : new(VerdictAction.Limit, EnforcementIssueSeverity.Warning, "active mutation"));
        if (recovery) this.mockEnforcementLevelMonitor.Setup(m => m.ResolveIssueAsync(It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, string, CancellationToken>(async (_, _, _) => { entered.SetResult(); await release.Task; });
        else this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, _) => { entered.SetResult(); await release.Task; });
         using var monitor = new AntiTamperMonitor(this.mockTimeProvider.Object, this.mockOutboxManager.Object, this.mockPrivilegeInspector.Object, this.mockEnforcementLevelMonitor.Object, this.mockIntegrityChecker.Object, this.mockBackendClient.Object, this.mockVerdictHandler.Object, identityCoordinator: identity.Object); await monitor.StartAsync(); var generation = this.GetGeneration(monitor); var work = monitor.TriggerIntegrityCheckAsync(); await entered.Task; var atEntry = this.CollaboratorSnapshot(); var stop = monitor.StopAsync(); var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var publicPendingAtStop = false; stop.ContinueWith(_ => { publicPendingAtStop = !work.IsCompleted; observed.SetResult(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default); stop.IsCompleted.Should().BeFalse(); release.SetResult(); await work; await observed.Task; await stop; publicPendingAtStop.Should().BeFalse(); this.CollaboratorSnapshot().Should().Equal(atEntry); this.mockEnforcementLevelMonitor.Invocations.Count(i => i.Method.Name == (recovery ? nameof(IEnforcementLevelMonitor.ResolveIssueAsync) : nameof(IEnforcementLevelMonitor.AddIssueAsync))).Should().Be(1); this.AssertResourcesDisposed(generation); await monitor.StopAsync(); monitor.Dispose(); this.AssertResourcesDisposed(generation);
    }

    [Fact]
    public async Task SynchronousHandler_ReentrantStopReturnsCompletedBeforeConsumption()
    {
        this.ConfigureHealthyBackend(); var verdictCalls = 0; AntiTamperMonitor? monitor = null; Task? stop = null;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(true, "trust"));
        var owned = this.CreateMonitor(_ => new ValueTask<bool>(false)); monitor = owned;
        this.mockVerdictHandler.Setup(v => v.HandleVerdict(It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<DateTimeOffset>())).Returns(() => { if (Interlocked.Increment(ref verdictCalls) == 2) { GetCallbackMarker(monitor!).Should().BeSameAs(this.GetGeneration(monitor)); stop = monitor.StopAsync(); stop.IsCompletedSuccessfully.Should().BeTrue(); stop.GetAwaiter().GetResult(); } return new(VerdictAction.None, null, null); });
         await monitor.StartAsync(); var generation = this.GetGeneration(monitor); await monitor.TriggerIntegrityCheckAsync(); await stop!; monitor.Dispose(); this.AssertResourcesDisposed(generation); this.GetGeneration(monitor).Should().BeNull();
    }

    // ── VerifyClockAgainstServerTimeAsync Tests ─────────────────────

    [Fact]
    public async Task VerifyClockAgainstServerTimeAsync_WhenDriftWithinThreshold_DoesNotRecordEvent()
    {
        // Arrange
        var localTime = DateTimeOffset.UtcNow;
        var serverTime = localTime.AddSeconds(-60); // 60 seconds drift (within 300s threshold)

        this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(localTime);

        // Act
        await this.monitor.VerifyClockAgainstServerTimeAsync(serverTime);

        // Assert
        this.monitor.DetectedEvents.Should().BeEmpty();
        this.monitor.ClockJumpDetected.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyClockAgainstServerTimeAsync_WhenDriftExceedsThreshold_RecordsEvent()
    {
        // Arrange
        var localTime = DateTimeOffset.UtcNow;
        var serverTime = localTime.AddMinutes(10); // 10 minutes drift (exceeds 5min threshold)

        this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(localTime);

        // Act
        await this.monitor.VerifyClockAgainstServerTimeAsync(serverTime);

        // Assert
        this.monitor.DetectedEvents.Should().HaveCount(1);
        this.monitor.DetectedEvents[0].Type.Should().Be(TamperEventType.ClockTamperSuspected);
    }

    [Fact]
    public async Task VerifyClockAgainstServerTimeAsync_WhenDisposed_DoesNotThrow()
    {
        this.monitor.Dispose();
        await this.monitor.VerifyClockAgainstServerTimeAsync(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock() { var wall = DateTimeOffset.UtcNow; var monotonic = 1000L; this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(() => wall); this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(() => monotonic); this.ConfigureHealthyBackend(); await this.monitor.StartAsync(); var firstGeneration = this.GetGeneration(); wall = wall.AddSeconds(120); monotonic++; GetCallbackMarker(this.monitor).Should().BeNull(); var callbacks = 0; Task? stop = null; this.monitor.OnClockJumpDetected += (_, args) => { GetCallbackMarker(this.monitor).Should().BeSameAs(firstGeneration); callbacks++; args.Direction.Should().Be(1); stop = this.monitor.StopAsync(); this.monitor.Dispose(); }; await this.monitor.TriggerIntegrityCheckAsync(); await stop!; GetCallbackMarker(this.monitor).Should().BeNull(); await this.monitor.StopAsync(); this.AssertResourcesDisposed(firstGeneration); this.GetGeneration().Should().BeNull(); callbacks.Should().Be(1); this.monitor.ClockJumpDetected.Should().BeTrue(); this.monitor.DetectedEvents.Should().BeEmpty(); using var other = this.CreateMonitor(_ => new ValueTask<bool>(false)); await other.StartAsync(); var otherGeneration = typeof(AntiTamperMonitor).GetField("generation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(other)!; GetCallbackMarker(other).Should().BeNull(); var otherCallbacks = 0; other.OnClockJumpDetected += (_, _) => { GetCallbackMarker(other).Should().BeSameAs(otherGeneration); GetCallbackMarker(other).Should().NotBeSameAs(firstGeneration); otherCallbacks++; }; wall = wall.AddSeconds(120); monotonic++; await other.TriggerIntegrityCheckAsync(); GetCallbackMarker(other).Should().BeNull(); otherCallbacks.Should().Be(1); await other.StopAsync(); }

    // ── TamperDetected Event Tests ───────────────────────────────

    [Fact]
    public void TamperDetected_WhenEventRecorded_FiresEvent()
    {
        // Arrange
        TamperEvent? capturedEvent = null;
        this.monitor.TamperDetected += (_, args) => capturedEvent = args.Event;

        // Act
        this.monitor.RecordServiceStopAttempt();

        // Assert
        capturedEvent.Should().NotBeNull();
        capturedEvent!.Type.Should().Be(TamperEventType.ServiceStopAttempt);
    }

    // ── CurrentTimezone Tests ─────────────────────────────────────

    [Fact]
    public void CurrentTimezone_ReturnsCurrentTimezone()
    {
        // Assert
        this.monitor.CurrentTimezone.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks()
    {
        var ticks = Channel.CreateUnbounded<int>(); var consumed1 = NewSignal(); var consumed2 = NewSignal(); var entered = NewSignal(); var entered3 = NewSignal(); var release = NewSignal(); var calls = 0;
        using var monitor = this.CreateMonitor(token => ReadTickAsync(ticks.Reader, consumed1, consumed2, token)); var lifecycleLock = typeof(AntiTamperMonitor).GetField("lockObject", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(monitor)!;
        this.ConfigureHealthyBackend();
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { var acquired = Monitor.TryEnter(lifecycleLock); acquired.Should().BeTrue(); if (acquired) Monitor.Exit(lifecycleLock); monitor.CurrentTimezone.Should().NotBeNullOrEmpty(); var call = Interlocked.Increment(ref calls); if (call == 1) return new(true, "trust"); if (call == 2) { entered.SetResult(); await release.Task; } else entered3.SetResult(); return new(true, "trust"); });
        await monitor.StartAsync(); ticks.Writer.TryWrite(1).Should().BeTrue(); await consumed1.Task; await entered.Task; ticks.Writer.TryWrite(2).Should().BeTrue(); consumed2.Task.IsCompleted.Should().BeFalse(); calls.Should().Be(2); release.SetResult(); await consumed2.Task; await entered3.Task; calls.Should().Be(3); await monitor.StopAsync();
    }

    [Fact]
    public async Task Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer()
    {
        var entered = NewSignal(); var release = NewSignal(); var calls = 0; var names = new[] { "Cancellation", "Gate", "TickTimer", "TimezoneTimer", "InitialCheck", "InitialOperation", "Loop", "OwnedTasks" }; this.ConfigureHealthyBackend(); this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>((_, _) => Interlocked.Increment(ref calls) == 1 ? Task.FromResult(new IntegrityReportResult(true, "trust")) : WaitForReleaseAsync(entered, release)); await this.monitor.StartAsync(); var oldGeneration = this.GetGeneration(); var oldResources = names.Select(name => this.GetResource(oldGeneration, name)).ToList(); var trigger = this.monitor.TriggerIntegrityCheckAsync(); await entered.Task; var stop = this.monitor.StopAsync(); var oldDrain = this.GetResource(oldGeneration, "Drain"); var restart = this.monitor.StartAsync(); release.SetResult(); await Task.WhenAll(trigger, stop, restart); var newGeneration = this.GetGeneration();
        newGeneration.Should().NotBeSameAs(oldGeneration); oldResources.Should().OnlyHaveUniqueItems(); names.Select(name => this.GetResource(newGeneration, name)).Should().NotContain(oldResources); this.AssertResourcesDisposed(oldGeneration); this.InvokeGeneration(this.monitor, "AdmitPeriodicCheck", oldGeneration); this.InvokeGeneration(this.monitor, "AdmitTimezoneCheck", oldGeneration); newGeneration.Should().BeSameAs(this.GetGeneration()); var newStop = this.monitor.StopAsync(); var newDrain = this.GetResource(newGeneration, "Drain"); await newStop; new[] { oldDrain, newDrain }.Should().OnlyHaveUniqueItems(); this.AssertResourcesDisposed(newGeneration); await this.monitor.StopAsync(); this.monitor.Dispose(); this.AssertResourcesDisposed(oldGeneration); this.AssertResourcesDisposed(newGeneration);
    }

    [Fact]
    public async Task ConcurrentStopAndDisposeShareLifecycleOwnership() { this.ConfigureHealthyBackend(); var e = NewSignal(); var r = NewSignal(); var calls = 0; this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>((_, _) => Interlocked.Increment(ref calls) == 1 ? Task.FromResult(new IntegrityReportResult(true, "trust")) : WaitForReleaseAsync(e, r)); await this.monitor.StartAsync(); var generation = this.GetGeneration(); var work = this.monitor.TriggerIntegrityCheckAsync(); await e.Task; var allocations = (int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!; var a = Task.Factory.StartNew(this.monitor.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default); var b = Task.Factory.StartNew(this.monitor.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default); a.IsCompleted.Should().BeFalse(); b.IsCompleted.Should().BeFalse(); r.SetResult(); await Task.WhenAll(work, a, b); this.AssertResourcesDisposed(generation); this.GetGeneration().Should().BeNull(); this.monitor.Dispose(); await Assert.ThrowsAsync<ObjectDisposedException>(() => this.monitor.StartAsync()); ((int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!).Should().Be(allocations); }

    [Fact]
    public async Task TamperCallback_CanRequestStopWithoutSelfDeadlock() { this.ConfigureHealthyBackend(); await this.monitor.StartAsync(); var generation = this.GetGeneration()!; generation.GetType().GetField("Timezone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(generation, "other"); var callback = NewSignal(); Task? requested = null; this.monitor.TamperDetected += (_, _) => { requested = this.monitor.StopAsync(); this.monitor.Dispose(); callback.SetResult(); }; typeof(AntiTamperMonitor).GetMethod("AdmitTimezoneCheck", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(this.monitor, new[] { generation }); await callback.Task; requested.Should().NotBeNull(); await this.monitor.StopAsync(); this.AssertResourcesDisposed(generation); }

    [Fact] public async Task SynchronousAdmission_IsRemovedBeforeDrain() { this.ConfigureHealthyBackend(); await this.monitor.StartAsync(); var generation = this.GetGeneration()!; await this.monitor.TriggerIntegrityCheckAsync(); ((ICollection<Task>)generation.GetType().GetField("OwnedTasks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(generation)!).Should().BeEmpty(); await this.monitor.StopAsync(); }

    [Fact]
    public async Task PureDecision_ExecutesReactionBeforeItsNotification()
    {
        var order = new ConcurrentQueue<string>();
        this.ConfigurePureBackendAfterStartup("revoked");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => { order.Enqueue("reaction"); return Task.CompletedTask; });
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => { order.Enqueue("notification"); return Task.CompletedTask; });
        using var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        await monitor.TriggerIntegrityCheckAsync(); await monitor.TriggerIntegrityCheckAsync(); await monitor.TriggerIntegrityCheckAsync();
        order.Should().Equal("reaction", "reaction", "notification");
        await monitor.StopAsync();
    }

    [Fact]
    public async Task StaleIdentityBeforeStage_ProducesNoDecisionEffects()
    {
        var entered = NewSignal(); var release = NewSignal(); var current = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 1, "device-a");
        var identity = new Mock<IBackendIdentityCoordinator>(); identity.SetupGet(i => i.CurrentState).Returns(() => current);
        this.ConfigurePureBackend("trust");
        var calls = 0; this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, _) => { if (Interlocked.Increment(ref calls) == 1) return new(true, "trust"); entered.SetResult(); await release.Task; return new(true, "revoked"); });
        using var monitor = this.CreatePureMonitor(identity.Object);
        await monitor.StartAsync();
        var work = monitor.TriggerIntegrityCheckAsync(); await entered.Task;
        current = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 2, "device-b"); release.SetResult();
        await work; this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); this.mockOutboxManager.Invocations.Should().BeEmpty(); await monitor.StopAsync();
    }

    [Fact]
    public async Task AdmittedNotificationChain_DrainsAfterStopAndIdentityRotation()
    {
        var entered = NewSignal(); var release = NewSignal(); var current = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 1, "device-a");
        var identity = new Mock<IBackendIdentityCoordinator>(); identity.SetupGet(i => i.CurrentState).Returns(() => current);
        this.ConfigurePureBackend("revoked");
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var reactions = 0; this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, _) => { if (Interlocked.Increment(ref reactions) == 2) { entered.SetResult(); await release.Task; } });
        var monitor = this.CreatePureMonitor(identity.Object); await monitor.StartAsync().WaitAsync(TimeSpan.FromSeconds(2)); await monitor.TriggerIntegrityCheckAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var work = monitor.TriggerIntegrityCheckAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var stop = monitor.StopAsync(); stop.IsCompleted.Should().BeFalse(); current = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 2, "device-b"); release.SetResult();
        await work.WaitAsync(TimeSpan.FromSeconds(2)); await stop.WaitAsync(TimeSpan.FromSeconds(2)); this.mockOutboxManager.Verify(m => m.EnqueueIntegrityNotificationAsync("integrity_degrade_pending", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once); monitor.Dispose();
    }

    [Fact]
    public async Task ReactionFault_IsObservedByWorkAndStopWithoutNotification()
    {
        var failure = new InvalidOperationException("reaction fault"); this.ConfigurePureBackendAfterStartup("revoked");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var monitor = this.CreatePureMonitor(); await monitor.StartAsync(); await monitor.TriggerIntegrityCheckAsync();
        var work = monitor.TriggerIntegrityCheckAsync(); var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => work); observed.Should().BeSameAs(failure);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync()); this.mockOutboxManager.Invocations.Should().BeEmpty(); monitor.Dispose();
    }

    [Fact]
    public async Task NotificationFault_IsObservedAfterExactlyOneReaction()
    {
        var failure = new InvalidOperationException("notification fault"); var reactions = 0; this.ConfigurePureBackendAfterStartup("revoked");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => { Interlocked.Increment(ref reactions); return Task.CompletedTask; });
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var monitor = this.CreatePureMonitor(); await monitor.StartAsync(); await monitor.TriggerIntegrityCheckAsync(); await monitor.TriggerIntegrityCheckAsync();
        var work = monitor.TriggerIntegrityCheckAsync(); var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => work); observed.Should().BeSameAs(failure); reactions.Should().Be(2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync()); monitor.Dispose();
    }

    [Fact]
    public async Task ConcurrentAdmittedDecisions_AreSerializedByGenerationGate()
    {
        var entered = NewSignal(); var secondEntered = NewSignal(); var release = NewSignal(); var active = 0; var maximum = 0; var calls = 0; this.ConfigurePureBackendAfterStartup("revoked");
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, _) => { var now = Interlocked.Increment(ref active); while (true) { var previous = Volatile.Read(ref maximum); if (previous >= now || Interlocked.CompareExchange(ref maximum, now, previous) == previous) break; } if (Interlocked.Increment(ref calls) == 1) entered.SetResult(); else secondEntered.SetResult(); await release.Task; Interlocked.Decrement(ref active); });
        var monitor = this.CreatePureMonitor(); await monitor.StartAsync().WaitAsync(TimeSpan.FromSeconds(2)); await monitor.TriggerIntegrityCheckAsync().WaitAsync(TimeSpan.FromSeconds(2)); var first = monitor.TriggerIntegrityCheckAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); var second = monitor.TriggerIntegrityCheckAsync(); secondEntered.Task.IsCompleted.Should().BeFalse(); maximum.Should().Be(1); release.SetResult(); await Task.WhenAll(first.WaitAsync(TimeSpan.FromSeconds(2)), second.WaitAsync(TimeSpan.FromSeconds(2))); await monitor.StopAsync().WaitAsync(TimeSpan.FromSeconds(2)); monitor.Dispose();
    }

    [Fact]
    public async Task AcceptedCallerCancellation_DrainsBlockedDecisionChain()
    {
        var entered = NewSignal(); var release = NewSignal(); this.ConfigurePureBackendAfterStartup("revoked");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, _) => { entered.SetResult(); await release.Task; });
         using var monitor = this.CreatePureMonitor(); await monitor.StartAsync(); await monitor.TriggerIntegrityCheckAsync(); using var caller = new CancellationTokenSource(); var work = monitor.TriggerIntegrityCheckAsync(caller.Token); await entered.Task; caller.Cancel(); work.IsCompleted.Should().BeTrue(); release.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work); await monitor.StopAsync();
    }

    [Fact]
    public async Task CallerCancellationWhileWaitingGenerationGateProducesNoEffects()
    {
        var entered = NewSignal(); var release = NewSignal(); var reactions = 0; var notifications = 0;
        var backendCalls = 0;
        this.ConfigurePureBackend("revoked");
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Interlocked.Increment(ref backendCalls) switch { 1 => new(true, "trust"), 2 or 3 => new(true, "revoked"), _ => new(true, "trust") });
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, _) => { Interlocked.Increment(ref reactions); entered.SetResult(); await release.Task; });
        this.mockEnforcementLevelMonitor.Setup(m => m.ResolveIssueAsync(It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => { Interlocked.Increment(ref reactions); return Task.CompletedTask; });
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => { Interlocked.Increment(ref notifications); return Task.CompletedTask; });
        using var monitor = this.CreatePureMonitor(); await monitor.StartAsync().WaitAsync(TimeSpan.FromSeconds(2));
        await monitor.TriggerIntegrityCheckAsync().WaitAsync(TimeSpan.FromSeconds(2)); var first = monitor.TriggerIntegrityCheckAsync(); await entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); using var caller = new CancellationTokenSource(); var second = monitor.TriggerIntegrityCheckAsync(caller.Token); caller.Cancel(); release.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(2)); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second).WaitAsync(TimeSpan.FromSeconds(2)); await monitor.StopAsync().WaitAsync(TimeSpan.FromSeconds(2)); backendCalls.Should().Be(3); reactions.Should().Be(1); notifications.Should().Be(0);
    }

    [Fact]
    public async Task CallerCancellationThenReactionFaultIsObservedByStopExactlyOnce()
    {
        var entered = NewSignal(); var release = NewSignal(); var failure = new InvalidOperationException("reaction fault after caller cancellation");
        this.ConfigurePureBackendAfterStartup("revoked");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, _) => { entered.SetResult(); await release.Task; throw failure; });
        var monitor = this.CreatePureMonitor(); await monitor.StartAsync(); await monitor.TriggerIntegrityCheckAsync(); using var caller = new CancellationTokenSource(); var work = monitor.TriggerIntegrityCheckAsync(caller.Token); await entered.Task; caller.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work); release.SetResult(); var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync()); observed.Should().BeSameAs(failure); monitor.Dispose();
    }

    [Fact]
    public async Task Dispose_DrainsBlockedAdmittedDecisionChain()
    {
        var entered = NewSignal(); var release = NewSignal(); this.ConfigurePureBackendAfterStartup("revoked");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, _) => { entered.SetResult(); await release.Task; });
        var monitor = this.CreatePureMonitor(); await monitor.StartAsync(); await monitor.TriggerIntegrityCheckAsync(); var work = monitor.TriggerIntegrityCheckAsync(); await entered.Task; var dispose = Task.Run(monitor.Dispose); dispose.IsCompleted.Should().BeFalse(); release.SetResult(); await work; await dispose;
    }

    [Fact]
    public async Task DecisionWithoutNotification_ExecutesReactionOnly()
    {
        this.ConfigurePureBackendAfterStartup("revoked"); var reactions = 0; this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => { reactions++; return Task.CompletedTask; });
        using var monitor = this.CreatePureMonitor(); await monitor.StartAsync(); await monitor.TriggerIntegrityCheckAsync(); await monitor.TriggerIntegrityCheckAsync(); reactions.Should().Be(1); this.mockOutboxManager.Verify(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never); await monitor.StopAsync();
    }

    [Fact]
    public async Task StaleAdmissionDoesNotStageHandlerStateBeforeNextAcceptedDecision()
    {
        var firstIdentity = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 1, "device-a");
        var secondIdentity = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 2, "device-b");
        var currentIdentity = firstIdentity;
        var transitionBeforeDecision = false;
        var identity = new Mock<IBackendIdentityCoordinator>();
        identity.SetupGet(coordinator => coordinator.CurrentState).Returns(() => currentIdentity);
        this.ConfigurePureBackendAfterStartup("revoked");
        var reports = 0;
        this.mockBackendClient
            .Setup(client => client.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (Interlocked.Increment(ref reports) == 2)
                {
                    transitionBeforeDecision = true;
                }

                return new IntegrityReportResult(true, reports == 1 ? "trust" : "revoked");
            });
        var originalWallClock = DateTimeOffset.UtcNow;
        this.mockTimeProvider
            .SetupGet(provider => provider.WallClockNow)
            .Returns(() =>
            {
                if (transitionBeforeDecision)
                {
                    transitionBeforeDecision = false;
                    currentIdentity = secondIdentity;
                }

                return originalWallClock;
            });

        using var monitor = this.CreatePureMonitor(identity.Object);
        await monitor.StartAsync();
        await monitor.TriggerIntegrityCheckAsync();
        this.mockEnforcementLevelMonitor.Verify(
            monitorMock => monitorMock.AddIssueAsync(
                It.IsAny<IssueKey>(),
                It.IsAny<EnforcementIssueSeverity>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        await monitor.TriggerIntegrityCheckAsync();
        this.mockEnforcementLevelMonitor.Verify(
            monitorMock => monitorMock.AddIssueAsync(
                It.IsAny<IssueKey>(),
                It.IsAny<EnforcementIssueSeverity>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        await monitor.TriggerIntegrityCheckAsync();
        this.mockEnforcementLevelMonitor.Verify(
            monitorMock => monitorMock.AddIssueAsync(
                It.IsAny<IssueKey>(),
                It.IsAny<EnforcementIssueSeverity>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task AcceptedCallerCancellationCancelsObservationWhileOwnedChainDrains()
    {
        var entered = NewSignal();
        var release = NewSignal();
        this.ConfigurePureBackendAfterStartup("revoked");
        this.mockEnforcementLevelMonitor
            .Setup(manager => manager.AddIssueAsync(
                It.IsAny<IssueKey>(),
                It.IsAny<EnforcementIssueSeverity>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, cancellationToken) =>
            {
                cancellationToken.Should().Be(CancellationToken.None);
                entered.SetResult();
                await release.Task;
            });
        using var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        await monitor.TriggerIntegrityCheckAsync();
        using var caller = new CancellationTokenSource();
        var work = monitor.TriggerIntegrityCheckAsync(caller.Token);
        await entered.Task;
        caller.Cancel();
        var stop = monitor.StopAsync();
        stop.IsCompleted.Should().BeFalse();
        release.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        await stop;
    }

    [Fact]
    public async Task SameDecision_IsConsumedOncePerEffectDomainAfterGateRecheck()
    {
        var decision = Decision(4, 4, "reaction-key", "notification-key");
        var reactions = 0;
        var notifications = 0;
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => { Interlocked.Increment(ref reactions); return Task.CompletedTask; });
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => { Interlocked.Increment(ref notifications); return Task.CompletedTask; });
        using var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => monitor.ExecuteDecisionAsync(decision)));
        reactions.Should().Be(1);
        notifications.Should().Be(1);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task ReactionFailure_LeavesBothDomainsUncommittedAndRetrySucceeds()
    {
        var decision = Decision(5, 5, "reaction-retry", "notification-retry");
        var failure = new InvalidOperationException("reaction retry");
        var attempts = 0;
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => Interlocked.Increment(ref attempts) == 1 ? Task.FromException(failure) : Task.CompletedTask);
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.ExecuteDecisionAsync(decision));
        first.Should().BeSameAs(failure);
        await monitor.ExecuteDecisionAsync(decision);
        attempts.Should().Be(2);
        this.mockOutboxManager.Verify(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), "notification-retry", It.IsAny<CancellationToken>()), Times.Once);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());
    }

    [Fact]
    public async Task NotificationFailure_CommitsReactionAndRetriesOnlyNotificationWithSameKey()
    {
        var decision = Decision(6, 6, "reaction-notification", "notification-exact");
        var notificationAttempts = 0;
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(() => Interlocked.Increment(ref notificationAttempts) == 1 ? Task.FromException(new InvalidOperationException("notification retry")) : Task.CompletedTask);
        using var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.ExecuteDecisionAsync(decision));
        await monitor.ExecuteDecisionAsync(decision);
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        this.mockOutboxManager.Verify(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), "notification-exact", It.IsAny<CancellationToken>()), Times.Exactly(2));
        notificationAttempts.Should().Be(2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());
    }

    [Fact]
    public async Task NewerDomainsAndReactionOnlyDecisionsAreNotSuppressed()
    {
        var newer = Decision(8, 8, "new-reaction", "new-notification");
        var reactionOnly = Decision(9, 9, "reaction-only", null);
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        await monitor.ExecuteDecisionAsync(new VerdictDecision(7, 7, new(VerdictAction.Limit, EnforcementIssueSeverity.Warning, "old"), null, DateTimeOffset.UtcNow, "scope", "old-reaction", null));
        await monitor.ExecuteDecisionAsync(newer);
        await monitor.ExecuteDecisionAsync(reactionOnly);
        this.mockEnforcementLevelMonitor.Invocations.Count(i => i.Method.Name == nameof(IEnforcementLevelMonitor.AddIssueAsync)).Should().Be(3);
        this.mockOutboxManager.Verify(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), "new-notification", It.IsAny<CancellationToken>()), Times.Once);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task FailedOlderDecision_BlocksQueuedNewerDecisionUntilExactRetry()
    {
        var decision = Decision(10, 10, "generation-reaction", null);
        var entered = NewSignal();
        var release = NewSignal();
        var attempts = 0;
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, CancellationToken>(async (_, _, _, _) => { if (Interlocked.Increment(ref attempts) == 1) { entered.SetResult(); await release.Task; throw new InvalidOperationException("old chain"); } });
        var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        var old = monitor.ExecuteDecisionAsync(decision);
        await entered.Task;
        var newer = monitor.ExecuteDecisionAsync(Decision(11, 11, "new-generation-reaction", null));
        newer.IsCompleted.Should().BeFalse();
        release.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => old);
        await Assert.ThrowsAsync<InvalidOperationException>(() => newer);
        attempts.Should().Be(1);
        await monitor.ExecuteDecisionAsync(decision);
        attempts.Should().Be(2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());
        await monitor.StartAsync();
        await monitor.ExecuteDecisionAsync(Decision(11, 11, "new-generation-reaction", null));
        attempts.Should().Be(3);
        await monitor.StopAsync();
        monitor.Dispose();
    }

    [Fact]
    public async Task EqualPosition_NullNotificationThenNotificationIsRejectedBeforeEffects()
    {
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        await monitor.ExecuteDecisionAsync(Decision(12, 12, "same-reaction", null));
        var conflicting = Decision(12, 12, "same-reaction", "new-notification");

        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.ExecuteDecisionAsync(conflicting));
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        this.mockOutboxManager.Verify(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());
    }

    [Fact]
    public async Task EqualPosition_ScopeMismatchIsRejectedBeforeEffects()
    {
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var monitor = this.CreatePureMonitor();
        await monitor.StartAsync();
        await monitor.ExecuteDecisionAsync(Decision(13, 13, "scope-a", null));
        var conflicting = new VerdictDecision(13, 13, new(VerdictAction.Limit, EnforcementIssueSeverity.Warning, "test"), null, DateTimeOffset.UnixEpoch, "scope-b", "scope-b-key", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.ExecuteDecisionAsync(conflicting));
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());
    }

    [Fact]
    public async Task PersistThenThrow_RetriesExactOutboxKeyOnceAndUnblocksNewerDecision()
    {
        var databaseName = $"slice-b-{Guid.NewGuid():N}";
        await using var connection = new SqliteConnection($"Data Source=file:{databaseName}?mode=memory&cache=shared");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ControlParentalDbContext>().UseSqlite(connection).Options;
        await using var db = new ControlParentalDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var manager = new OutboxManager(new TrackingDbContextFactory(options), this.mockTimeProvider.Object);
        var outbox = new Mock<IOutboxManager>();
        var persisted = NewSignal(); var release = NewSignal(); var attempts = 0;
        var failure = new InvalidOperationException("persisted then throw");
        outbox.Setup(value => value.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, string, DateTimeOffset, string, CancellationToken>(async (type, title, body, timestamp, key, token) =>
            {
                await manager.EnqueueIntegrityNotificationAsync(type, title, body, timestamp, key, token);
                if (Interlocked.Increment(ref attempts) == 1) { persisted.SetResult(); await release.Task; throw failure; }
            });
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var monitor = this.CreatePureMonitor(outbox: outbox.Object);
        await monitor.StartAsync();
        var first = Decision(14, 14, "reaction-persisted", "notification-persisted");
        var newer = Decision(15, 15, "newer-reaction", null);
        var firstWork = monitor.ExecuteDecisionAsync(first);
        await persisted.Task;
        var newerWork = monitor.ExecuteDecisionAsync(newer);
        release.SetResult();
        (await Assert.ThrowsAsync<InvalidOperationException>(() => firstWork)).Should().BeSameAs(failure);
        await Assert.ThrowsAsync<InvalidOperationException>(() => newerWork);
        this.mockEnforcementLevelMonitor.Verify(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        var row = Assert.Single(await manager.GetPendingEntriesAsync());
        row.DedupKey.Should().Be("notification-persisted");
        await monitor.ExecuteDecisionAsync(first);
        (await manager.GetPendingEntriesAsync()).Should().ContainSingle(value => value.DedupKey == "notification-persisted");
        await monitor.ExecuteDecisionAsync(newer);
        this.mockEnforcementLevelMonitor.Verify(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        outbox.Verify(value => value.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), "notification-persisted", It.IsAny<CancellationToken>()), Times.Exactly(2));
        attempts.Should().Be(2);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());
    }

    [Fact]
    public async Task DurableOwner_RehydratesBeforeFirstRemote_AndMissingStateIsValid()
    {
        var store = new RecordingEscalationStore();
        this.ConfigurePureBackend("trust");
        store.OnLoad = () => store.Loads.Should().Be(1);
        using var monitor = this.CreatePureMonitor(store: store);

        await monitor.StartAsync();
        store.Loads.Should().Be(1);
        this.mockBackendClient.Verify(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()), Times.Once);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task DurableOwner_SavesAcceptedStateBeforeEffects_WithExactKeys()
    {
        var store = new RecordingEscalationStore();
        var reactionKey = string.Empty;
        var notificationKey = string.Empty;
        this.ConfigurePureBackendAfterStartup("revoked");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<IssueKey, EnforcementIssueSeverity, string, string?, CancellationToken>((_, _, _, key, _) => { key.Should().Be(store.Last!.State.PendingReactionId); reactionKey = key!; return Task.CompletedTask; });
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, string, DateTimeOffset, string, CancellationToken>((_, _, _, _, key, _) => { notificationKey = key; store.Last!.State.PendingNotificationId.Should().Be(key); return Task.CompletedTask; });
        using var monitor = this.CreatePureMonitor(store: store);

        await monitor.StartAsync();
        await monitor.TriggerIntegrityCheckAsync();
        await monitor.TriggerIntegrityCheckAsync();
        await monitor.TriggerIntegrityCheckAsync();
        reactionKey.Should().NotBeNullOrWhiteSpace();
        notificationKey.Should().NotBeNullOrWhiteSpace();
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), reactionKey, It.IsAny<CancellationToken>()), Times.Once);
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Last!.Validate();
        await monitor.StopAsync();
    }

    [Fact]
    public async Task DurableOwner_RestartReconcilesReactionAndNotificationIndependently()
    {
        var pendingReaction = ValidState("reaction-restart", null);
        var pendingNotification = ValidState("reaction-complete", "notification-restart") with { CompletedNotificationId = null };
        var store = new RecordingEscalationStore(pendingReaction);
        this.ConfigureHealthyBackend();
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using (var monitor = this.CreatePureMonitor(store: store)) await monitor.StartAsync();
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        this.mockOutboxManager.Invocations.Clear();
        store.State = pendingNotification;
        using var restarted = this.CreatePureMonitor(store: store);
        await restarted.StartAsync();
        this.mockOutboxManager.Verify(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), "notification-restart", It.IsAny<CancellationToken>()), Times.Once);
        await restarted.StopAsync();
    }

    [Fact]
    public async Task DurableOwner_RestartWithInvalidPendingSeverity_FailsBeforeAnyEffect()
    {
        var due = DateTimeOffset.UtcNow.AddMinutes(5);
        var pending = PendingState(due) with
        {
            CompletedReactionId = null,
            PendingNotificationId = "notification-invalid-severity",
            PendingEffectDescriptor = PendingState(due).PendingEffectDescriptor! with
            {
                Severity = (EnforcementIssueSeverity)999,
                NotificationType = "integrity_degrade_pending",
                NotificationTitle = "title",
                NotificationBody = "body",
                NotificationTimestamp = DateTimeOffset.UnixEpoch,
            },
        };
        var store = new RecordingEscalationStore(pending);
        this.ConfigureHealthyBackend();
        var monitor = this.CreatePureMonitor(store: store);

        var error = await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => monitor.StartAsync());

        error.Error.Should().Be(IntegrityEscalationStateError.InvalidEffectDescriptor);
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(
            It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        this.mockEnforcementLevelMonitor.Verify(m => m.ResolveIssueAsync(
            It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        this.mockOutboxManager.Invocations.Should().BeEmpty();

        await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => monitor.StopAsync());
        monitor.Dispose();
    }

    [Fact]
    public async Task DurableReplay_ThirdRevoked_ReusesExactReactionAndNotificationSemantics()
    {
        var observedAt = DateTimeOffset.UtcNow;
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(observedAt.AddSeconds(1));
        this.mockPrivilegeInspector.Setup(value => value.IsChildStandardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        this.mockIntegrityChecker.Setup(value => value.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityCheckResult(true, "hash", "agent.exe"));
        this.mockBackendClient.Setup(value => value.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(false, null));
        var handler = new IntegrityVerdictHandler(clock: () => observedAt);
        handler.SetServiceStartTime(observedAt.AddMinutes(-10));
        var store = new RecordingEscalationStore();
        var firstAttempt = true;
        var reactions = new List<(EnforcementIssueSeverity Severity, string Reason)>();
        var notifications = new List<(string Type, string Title, string Body, DateTimeOffset Timestamp)>();

        this.mockEnforcementLevelMonitor
            .Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<IssueKey, EnforcementIssueSeverity, string, string?, CancellationToken>((_, severity, reason, _, _) =>
            {
                reactions.Add((severity, reason));
                return firstAttempt
                    ? Task.FromException(new InvalidOperationException("crash after durable prepare"))
                    : Task.CompletedTask;
            });
        this.mockOutboxManager
            .Setup(value => value.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, string, DateTimeOffset, string, CancellationToken>((type, title, body, timestamp, _, _) =>
            {
                notifications.Add((type, title, body, timestamp));
                return Task.CompletedTask;
            });

        var monitor = this.CreatePureMonitor(handler: handler, store: store);
        await monitor.StartAsync();
        _ = handler.HandleVerdictDecision("revoked", true, observedAt);
        _ = handler.HandleVerdictDecision("revoked", true, observedAt);
        var thirdRevoked = handler.HandleVerdictDecision("revoked", true, observedAt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.ExecuteDecisionAsync(thirdRevoked));
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());

        firstAttempt = false;
        this.mockEnforcementLevelMonitor.Invocations.Clear();
        this.mockOutboxManager.Invocations.Clear();
        using var restarted = this.CreatePureMonitor(store: store);
        await restarted.StartAsync();

        reactions.Should().HaveCount(2);
        reactions[1].Should().Be((thirdRevoked.Reaction.Severity!.Value, thirdRevoked.Reaction.Reason!));
        notifications.Should().ContainSingle().Which.Should().Be((
            thirdRevoked.Notification!.Type,
            thirdRevoked.Notification.Title,
            thirdRevoked.Notification.Body,
            thirdRevoked.Notification.Timestamp));
        await restarted.StopAsync();
    }

    [Fact]
    public async Task DurableOwner_CompletedReplayIsNoOp_ButDistinctLaterDecisionExecutes()
    {
        var store = new RecordingEscalationStore(ValidState("completed-old", "completed-old") with { CompletedReactionId = "completed-old" });
        this.ConfigurePureBackend("revoked");
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        this.mockEnforcementLevelMonitor.Invocations.Clear();
        await monitor.ExecuteDecisionAsync(Decision(1, 1, "completed-old", null));
        this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty();
        await monitor.TriggerIntegrityCheckAsync();
        this.mockEnforcementLevelMonitor.Invocations.Should().Contain(i => i.Method.Name == nameof(IEnforcementLevelMonitor.AddIssueAsync));
        store.Last!.State.PendingReactionId.Should().NotBe("completed-old");
        await monitor.StopAsync();
    }

    [Fact]
    public async Task DurableOwner_DegradedRecoveryUsesResolveAndPersistsCompletion()
    {
        var store = new RecordingEscalationStore(ValidState("recovery", "recovery", EscalationPhase.Degraded, recoveryLatch: true, trustStreak: 2));
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(m => m.ResolveIssueAsync(It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        await monitor.TriggerIntegrityCheckAsync();
        this.mockEnforcementLevelMonitor.Verify(m => m.ResolveIssueAsync(It.IsAny<IssueKey>(), "Authoritative trust recovery", It.IsAny<CancellationToken>()), Times.Once);
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Last!.Validate();
        await monitor.StopAsync();

        store.State = ValidState("non-degraded-latch", null, recoveryLatch: true);
        this.mockEnforcementLevelMonitor.Invocations.Clear();
        using var restarted = this.CreatePureMonitor(store: store);
        await restarted.StartAsync();
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), "non-degraded-latch", It.IsAny<CancellationToken>()), Times.Once);
        this.mockEnforcementLevelMonitor.Invocations.Should().NotContain(i => i.Method.Name == nameof(IEnforcementLevelMonitor.ResolveIssueAsync));
        await restarted.StopAsync();
    }

    [Fact]
    public async Task DurableOwner_InvalidOrUnavailableLoadFailsClosed_AndCancellationUsesOwnerToken()
    {
        foreach (var failure in new (IntegrityEscalationState? State, Exception? Error)[]
        {
            (ValidState("bad", null) with { IdentityScope = string.Empty }, null),
            (null, new IntegrityEscalationStateException(IntegrityEscalationStateError.StoreUnavailable, "unavailable")),
        })
        {
            var store = new RecordingEscalationStore(failure.State) { LoadException = failure.Error };
            this.ConfigurePureBackend("trust");
            var monitor = this.CreatePureMonitor(store: store);
            await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => monitor.StartAsync());
            await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => monitor.StopAsync());
            this.mockBackendClient.Verify(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()), Times.Never);
            this.mockEnforcementLevelMonitor.VerifyNoOtherCalls();
            monitor.Dispose();
        }

        using var cancellation = new CancellationTokenSource();
        var cancelled = new RecordingEscalationStore { OnLoad = cancellation.Cancel, LoadBehavior = token => { token.ThrowIfCancellationRequested(); return Task.FromResult<IntegrityEscalationState?>(null); } };
        this.ConfigurePureBackend("trust");
        using var owner = this.CreatePureMonitor(store: cancelled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.StartAsync(cancellation.Token));
        cancelled.ObservedToken.CanBeCanceled.Should().BeTrue();
        this.mockBackendClient.Verify(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DurableOwner_ActualRevokedAndTrustObservationsPersistBoundariesAndReset()
    {
        var store = new RecordingEscalationStore();
        this.ConfigurePureBackend("trust");
        var verdict = "revoked";
        var calls = 0;
        this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Interlocked.Increment(ref calls) == 1 ? new IntegrityReportResult(true, "trust") : new IntegrityReportResult(true, verdict));
        using (var monitor = this.CreatePureMonitor(store: store))
        {
            await monitor.StartAsync();
            var boundaries = new List<(long Sequence, int Streak, EscalationPhase Phase, DateTimeOffset? Due, string? Key)>();
            for (var i = 0; i < 4; i++)
            {
                await monitor.TriggerIntegrityCheckAsync();
                var state = store.State;
                state!.Validate();
                boundaries.Add((state.Sequence, state.RevokedStreak, state.Phase, state.DeadlineDueUtc, state.PendingReactionId));
            }
            boundaries.Select(value => (value.Sequence, value.Streak, value.Phase)).Should().Equal(
                (2, 1, EscalationPhase.Normal), (3, 2, EscalationPhase.Normal), (4, 3, EscalationPhase.Pending), (5, 3, EscalationPhase.Pending));
            boundaries.Select(value => (value.Streak, value.Phase)).Should().Equal(
                (1, EscalationPhase.Normal), (2, EscalationPhase.Normal), (3, EscalationPhase.Pending), (3, EscalationPhase.Pending));
            boundaries.Select(value => value.Key).Should().Equal(null, boundaries[1].Key, boundaries[2].Key, boundaries[3].Key);
            store.State!.Sequence.Should().Be(5);
            boundaries[2].Due.Should().NotBeNull();
            boundaries[3].Due.Should().Be(boundaries[2].Due);
            boundaries[2].Key.Should().NotBeNullOrWhiteSpace();
            boundaries[3].Key.Should().NotBeNullOrWhiteSpace();
            await monitor.StopAsync();
        }
        store.State = store.State! with { Phase = EscalationPhase.Degraded, FiredLatch = true, RecoveryLatch = false, RevokedStreak = 0, TrustStreak = 0, DeadlineOriginUtc = null, DeadlineDueUtc = null, PendingReactionId = null, CompletedReactionId = null, PendingNotificationId = null, CompletedNotificationId = null, PendingEffectDescriptor = null };
        verdict = "unknown";
        this.mockEnforcementLevelMonitor.Setup(m => m.ResolveIssueAsync(It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var recovery = this.CreatePureMonitor(store: store);
        await recovery.StartAsync();
        verdict = "trust";
        await recovery.TriggerIntegrityCheckAsync();
        store.Last!.Validate();
        store.State!.TrustStreak.Should().Be(1);
        await recovery.TriggerIntegrityCheckAsync();
        store.Last!.Validate();
        store.State!.TrustStreak.Should().Be(2);
        await recovery.TriggerIntegrityCheckAsync();
        store.Last!.Validate();
        store.State!.TrustStreak.Should().Be(0);
        store.State.Phase.Should().Be(EscalationPhase.Normal);
        verdict = "revoked";
        await recovery.TriggerIntegrityCheckAsync();
        store.Last!.Validate();
        store.State!.RevokedStreak.Should().Be(1);
        store.State.Phase.Should().Be(EscalationPhase.Normal);
        await recovery.StopAsync();
    }

    [Fact]
    public async Task DeadlineOwner_UsesPersistedDueForBeforeDueAndOneShotDegradation()
    {
        var now = DateTimeOffset.UtcNow;
        var state = PendingState(now.AddMinutes(5));
        var store = new RecordingEscalationStore(state);
        var degraded = NewSignal();
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now);
        this.ConfigurePureBackend("unknown");
        this.mockEnforcementLevelMonitor
            .Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() => { degraded.TrySetResult(); return Task.CompletedTask; });

        using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        var generation = this.GetGeneration(monitor)!;
        this.GetResource(generation, "DeadlineTimer").Should().NotBeNull();

        this.InvokeGeneration(monitor, "AdmitDeadlineCheck", generation);
        await Task.Delay(50);
        store.State!.Phase.Should().Be(EscalationPhase.Pending);
        this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty();

        now = state.DeadlineDueUtc!.Value;
        this.InvokeGeneration(monitor, "AdmitDeadlineCheck", generation);
        await degraded.Task.WaitAsync(TimeSpan.FromSeconds(2));
        this.InvokeGeneration(monitor, "AdmitDeadlineCheck", generation);
        await Task.Delay(50);
        store.State!.Phase.Should().Be(EscalationPhase.Degraded);
        this.mockEnforcementLevelMonitor.Invocations.Count(value => value.Method.Name == nameof(IEnforcementLevelMonitor.AddIssueAsync)).Should().Be(1);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task DeadlineOwner_RollbackFailsClosedWithoutReplacementDelay()
    {
        var now = DateTimeOffset.UtcNow;
        var state = PendingState(now.AddMinutes(5)) with { MaxWallClockSeenUtc = now };
        var store = new RecordingEscalationStore(state);
        var reads = 0;
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => Interlocked.Increment(ref reads) == 1 ? now : now.AddMinutes(-1));
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        this.GetResource(this.GetGeneration(monitor), "DeadlineTimer").Should().BeNull();
        store.State!.Phase.Should().Be(EscalationPhase.Degraded);
        this.mockEnforcementLevelMonitor.Invocations.Count(value => value.Method.Name == nameof(IEnforcementLevelMonitor.AddIssueAsync)).Should().Be(1);
        await monitor.StopAsync();
    }

    [Fact]
    public async Task DeadlineOwner_SyntheticEarlyCallbackDoesNotRearmOrResubtract()
    {
        var now = DateTimeOffset.UtcNow;
        var state = PendingState(now.AddMinutes(5));
        var store = new RecordingEscalationStore(state);
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now);
        this.ConfigurePureBackend("unknown");
        using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        var generation = this.GetGeneration(monitor)!;
        var timer = this.GetResource(generation, "DeadlineTimer");
        var version = (long)generation.GetType().GetField("DeadlineVersion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(generation)!;
        var saves = store.SaveCalls;

        this.InvokeGeneration(monitor, "AdmitDeadlineCheck", generation);
        await Task.Delay(50);

        this.GetResource(generation, "DeadlineTimer").Should().BeSameAs(timer);
        generation.GetType().GetField("DeadlineVersion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(generation).Should().Be(version);
        store.State!.Phase.Should().Be(EscalationPhase.Pending);
        store.SaveCalls.Should().Be(saves);
        this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty();
        await monitor.StopAsync();
    }

    [Fact]
    public async Task DeadlineOwner_ForwardJumpAdmitsExpiryThroughIntegrityPath()
    {
        var now = DateTimeOffset.UtcNow;
        var monotonic = 1000L;
        var state = PendingState(now.AddMinutes(5));
        var store = new RecordingEscalationStore(state);
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now);
        this.mockTimeProvider.SetupGet(value => value.MonotonicNow).Returns(() => monotonic);
        this.ConfigurePureBackend("unknown");
        var degraded = NewSignal();
        this.mockEnforcementLevelMonitor.Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(() => { degraded.TrySetResult(); return Task.CompletedTask; });
        using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        now = state.DeadlineDueUtc!.Value.AddMinutes(1);
        monotonic++;
        await monitor.TriggerIntegrityCheckAsync();
        await degraded.Task.WaitAsync(TimeSpan.FromSeconds(2));
        store.State!.Phase.Should().Be(EscalationPhase.Degraded);
        await monitor.StopAsync();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task DeadlineOwner_ActualOneShotTimerDegradesAtOrAfterDue(int minutesFromDue)
    {
        var now = DateTimeOffset.UtcNow;
        var due = now.AddMinutes(minutesFromDue);
        var state = PendingState(due);
        var store = new RecordingEscalationStore(state);
        var degraded = NewSignal();
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now);
        this.mockTimeProvider.SetupGet(value => value.MonotonicNow).Returns(1000L);
        this.mockPrivilegeInspector.Setup(value => value.IsChildStandardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        this.mockIntegrityChecker.Setup(value => value.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityCheckResult(true, "hash", "agent.exe"));
        this.mockBackendClient.Setup(value => value.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(false, null));
        this.mockEnforcementLevelMonitor.Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(() => { degraded.TrySetResult(); return Task.CompletedTask; });

        using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        var generation = this.GetGeneration(monitor)!;
        await degraded.Task.WaitAsync(TimeSpan.FromSeconds(2));
        store.State!.Phase.Should().Be(EscalationPhase.Degraded);
        store.Last!.State.PendingReactionId.Should().NotBeNullOrWhiteSpace();
        this.mockEnforcementLevelMonitor.Invocations.Count(value => value.Method.Name == nameof(IEnforcementLevelMonitor.AddIssueAsync)).Should().Be(1);
        await monitor.StopAsync();
        this.GetResource(generation, "DeadlineTimer").Should().BeNull();
        this.GetResourceCount(generation, "DeadlineTimerDisposals").Should().Be(1);

        this.mockEnforcementLevelMonitor.Invocations.Clear();
        this.mockBackendClient.Setup(value => value.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(true, "trust"));
        this.mockEnforcementLevelMonitor.Setup(value => value.ResolveIssueAsync(It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var recovery = this.CreatePureMonitor(store: store);
        await recovery.StartAsync();
        var recoveryGeneration = this.GetGeneration(recovery);
        await recovery.TriggerIntegrityCheckAsync(); await recovery.TriggerIntegrityCheckAsync(); await recovery.TriggerIntegrityCheckAsync();
        this.mockEnforcementLevelMonitor.Verify(value => value.ResolveIssueAsync(It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        await recovery.TriggerIntegrityCheckAsync();
        this.mockEnforcementLevelMonitor.Verify(value => value.ResolveIssueAsync(It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        await recovery.StopAsync();
    }

    [Fact]
    public async Task DeadlineOwner_ElapsedCallbackSurvivesQueuedSyntheticAdmission()
    {
        var now = DateTimeOffset.UtcNow; var store = new RecordingEscalationStore(PendingState(now.AddMinutes(5)));
        var degraded = NewSignal(); this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now); this.ConfigurePureBackend("unknown");
        this.mockEnforcementLevelMonitor.Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(() => { degraded.TrySetResult(); return Task.CompletedTask; });
        using var monitor = this.CreatePureMonitor(store: store); await monitor.StartAsync(); var generation = this.GetGeneration(monitor)!;
        var gate = (SemaphoreSlim)this.GetResource(generation, "Gate"); await gate.WaitAsync().WaitAsync(TimeSpan.FromSeconds(2));
        this.InvokeGeneration(monitor, "AdmitDeadlineCheck", generation);
        ((Timer)this.GetResource(generation, "DeadlineTimer")).Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        try
        {
            SpinWait.SpinUntil(() => ((ICollection<Task>)this.GetResource(generation, "OwnedTasks")).Count >= 2, TimeSpan.FromSeconds(2)).Should().BeTrue();
        }
        finally { gate.Release(); }
        await degraded.Task.WaitAsync(TimeSpan.FromSeconds(2));
        store.State!.Phase.Should().Be(EscalationPhase.Degraded); this.mockEnforcementLevelMonitor.Invocations.Count(value => value.Method.Name == nameof(IEnforcementLevelMonitor.AddIssueAsync)).Should().Be(1);
        await monitor.StopAsync();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeadlineOwner_TimingDefectsReplayOldEffectsBeforeNewFailClosedDeadline(bool rollback)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        var oldNotificationTimestamp = now.AddSeconds(-30);
        var oldReactionKey = "integrity/local/integrity-binary/4/4/limit";
        var oldNotificationKey = "integrity/local/integrity-binary/4/4/notification";
        var pending = PendingState(now.AddMinutes(5)) with
        {
            PendingReactionId = oldReactionKey,
            CompletedReactionId = null,
            PendingNotificationId = oldNotificationKey,
            CompletedNotificationId = null,
            MaxWallClockSeenUtc = rollback ? now.AddMinutes(1) : now,
            TimingValid = rollback,
            PendingEffectDescriptor = new(
                IntegrityEscalationEffectDescriptor.CurrentVersion,
                IntegrityEscalationReactionKind.AddIssue,
                EnforcementIssueSeverity.Warning,
                "old warning reason",
                "old_notification_type",
                "Old warning",
                "Old warning body",
                oldNotificationTimestamp),
        };
        var store = new RecordingEscalationStore(pending);
        var calls = new List<string>();
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now);
        this.ConfigurePureBackend("trust");
        this.mockEnforcementLevelMonitor.Setup(value => value.AddIssueAsync(
                It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<IssueKey, EnforcementIssueSeverity, string, string?, CancellationToken>((_, severity, reason, key, _) => calls.Add($"reaction:{key}:{severity}:{reason}"))
            .Returns(Task.CompletedTask);
        this.mockOutboxManager.Setup(value => value.EnqueueIntegrityNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, DateTimeOffset, string, CancellationToken>((type, title, body, timestamp, key, _) =>
            {
                calls.Add($"notification:{key}");
                (type, title, body, timestamp).Should().Be(("old_notification_type", "Old warning", "Old warning body", oldNotificationTimestamp));
            })
            .Returns(Task.CompletedTask);

        using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();

        calls.Should().HaveCount(3);
        calls[0].Should().Be($"reaction:{oldReactionKey}:Warning:old warning reason");
        calls[1].Should().Be($"notification:{oldNotificationKey}");
        calls[2].Should().Match("reaction:*:Severe:Integrity deadline reached");
        var newReactionKey = store.State!.PendingReactionId;
        newReactionKey.Should().NotBe(oldReactionKey);
        store.State.CompletedReactionId.Should().Be(newReactionKey);
        store.State.PendingNotificationId.Should().BeNull();
        store.State.CompletedNotificationId.Should().BeNull();
        store.State.Phase.Should().Be(EscalationPhase.Degraded);
        store.State.PendingEffectDescriptor!.Severity.Should().Be(EnforcementIssueSeverity.Severe);
        store.State.PendingEffectDescriptor.Reason.Should().Be("Integrity deadline reached");
        store.State.Validate();
        await monitor.StopAsync();
    }

    [Fact]
    public async Task DeadlineOwner_UnknownPendingCorruptionStillFailsClosed()
    {
        var store = new RecordingEscalationStore(PendingState(DateTimeOffset.UtcNow.AddMinutes(5)) with { IdentityScope = string.Empty }); this.ConfigurePureBackend("unknown");
        using var monitor = this.CreatePureMonitor(store: store); await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => monitor.StartAsync());
        this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => monitor.StopAsync());
    }

    [Fact]
    public async Task DeadlineOwner_StopRestartDisposesOldTimerAndRejectsStaleGeneration()
    {
        var now = DateTimeOffset.UtcNow; var due = now.AddMinutes(5); var store = new RecordingEscalationStore(PendingState(due));
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now); this.ConfigurePureBackend("unknown"); using var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync(); var first = this.GetGeneration(monitor)!; this.GetResource(first, "DeadlineTimer").Should().NotBeNull();
        await monitor.StopAsync(); this.GetResource(first, "DeadlineTimer").Should().BeNull(); this.GetResourceCount(first, "DeadlineTimerDisposals").Should().Be(1);
        await monitor.StartAsync(); var second = this.GetGeneration(monitor)!; second.Should().NotBeSameAs(first); this.GetResource(second, "DeadlineTimer").Should().NotBeNull();
        var snapshot = store.State; this.InvokeGeneration(monitor, "AdmitDeadlineCheck", first); await Task.Yield(); store.State.Should().Be(snapshot); this.GetResource(second, "DeadlineTimer").Should().NotBeNull();
        await monitor.StopAsync(); this.GetResourceCount(second, "DeadlineTimerDisposals").Should().Be(1);
    }

    [Fact]
    public async Task DeadlineOwner_TrustFreshIdentityAndDirectDisposeRejectStaleCallbacks()
    {
        var now = DateTimeOffset.UtcNow; var store = new RecordingEscalationStore(PendingState(now.AddMinutes(5)));
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now); this.ConfigurePureBackend("unknown");
        using var monitor = this.CreatePureMonitor(store: store); await monitor.StartAsync();
        var first = this.GetGeneration(monitor)!; var firstTimer = this.GetResource(first, "DeadlineTimer"); var firstVersion = this.GetDeadlineVersion(first);
        var due = store.State!.DeadlineDueUtc; var origin = store.State.DeadlineOriginUtc; var disposals = this.GetResourceCount(first, "DeadlineTimerDisposals");
        this.ConfigurePureBackend("trust"); await monitor.TriggerIntegrityCheckAsync();
        store.State!.Phase.Should().Be(EscalationPhase.Normal); store.State.DeadlineDueUtc.Should().BeNull(); store.State.DeadlineOriginUtc.Should().BeNull();
        this.GetResource(first, "DeadlineTimer").Should().BeNull(); this.GetResourceCount(first, "DeadlineTimerDisposals").Should().Be(disposals + 1);
        var before = this.CollaboratorSnapshot(); this.InvokeDeadline(monitor, first, firstVersion, true); await Task.Yield(); this.CollaboratorSnapshot().Should().Equal(before);
        this.ConfigurePureBackend("revoked"); now = now.AddSeconds(1); await monitor.TriggerIntegrityCheckAsync(); await monitor.TriggerIntegrityCheckAsync(); await monitor.TriggerIntegrityCheckAsync();
        store.State!.Phase.Should().Be(EscalationPhase.Pending); store.State.DeadlineDueUtc.Should().NotBe(due); store.State.DeadlineOriginUtc.Should().NotBe(origin);
        var second = this.GetGeneration(monitor)!; var secondTimer = this.GetResource(second, "DeadlineTimer"); var secondVersion = this.GetDeadlineVersion(second);
        second.Should().BeSameAs(this.GetGeneration(monitor)); secondVersion.Should().BeGreaterThan(firstVersion); secondTimer.Should().NotBeNull(); secondTimer.Should().NotBeSameAs(firstTimer);
        this.InvokeDeadline(monitor, first, firstVersion, true); await Task.Yield(); this.GetResource(second, "DeadlineTimer").Should().BeSameAs(secondTimer); this.GetDeadlineVersion(second).Should().Be(secondVersion);
        monitor.Dispose(); this.GetResource(second, "DeadlineTimer").Should().BeNull(); this.GetResourceCount(second, "DeadlineTimerDisposals").Should().Be(disposals + 2); monitor.Dispose();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeadlineOwner_RealTimerFaultRestartConvergesByExactKey(bool saveFault)
    {
        var now = DateTimeOffset.UtcNow; var store = new RecordingEscalationStore(PendingState(now.AddMinutes(5)));
        var failure = new InvalidOperationException(saveFault ? "deadline save fault" : "deadline effect fault"); var entered = NewSignal();
        this.mockTimeProvider.SetupGet(value => value.WallClockNow).Returns(() => now); this.ConfigurePureBackend("unknown");
        var failedSave = 0;
        if (!saveFault) this.mockEnforcementLevelMonitor.Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns<IssueKey, EnforcementIssueSeverity, string, string?, CancellationToken>((_, _, _, _, _) => { entered.TrySetResult(); return Task.FromException(failure); });
        using (var monitor = this.CreatePureMonitor(store: store))
        {
            await monitor.StartAsync().WaitAsync(TimeSpan.FromSeconds(2)); now = store.State!.DeadlineDueUtc!.Value;
            if (saveFault) { failedSave = store.SaveCalls + 1; store.SaveFailureAt = failedSave; }
            ((Timer)this.GetResource(this.GetGeneration(monitor), "DeadlineTimer")).Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
            if (saveFault) SpinWait.SpinUntil(() => store.SaveCalls >= failedSave, TimeSpan.FromSeconds(2)).Should().BeTrue(); else await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync().WaitAsync(TimeSpan.FromSeconds(2)));
        }
        store.State!.Validate(); var key = store.LastAttempted?.State.PendingReactionId ?? store.State.PendingReactionId; key.Should().NotBeNullOrWhiteSpace();
        if (saveFault) store.State.Phase.Should().Be(EscalationPhase.Pending); else { store.State.Phase.Should().Be(EscalationPhase.Degraded); store.State.CompletedReactionId.Should().BeNull(); }
        this.mockEnforcementLevelMonitor.Setup(value => value.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        store.SaveException = null; store.SaveGate = null; store.SaveFailureAt = null; this.mockBackendClient.Setup(value => value.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(false, null));
        using var restarted = this.CreatePureMonitor(store: store); await restarted.StartAsync().WaitAsync(TimeSpan.FromSeconds(2)); SpinWait.SpinUntil(() => store.State!.CompletedReactionId == key, TimeSpan.FromSeconds(2)).Should().BeTrue();
        store.State!.Validate(); store.State.CompletedReactionId.Should().Be(key); await restarted.StopAsync();
    }

    [Fact]
    public async Task DurableOwner_RecordAgentDeathRestartReconcilesPersistedAuthorityWithoutDuplicateKey()
    {
        var state = ValidState("reaction-agent", "notification-agent") with { CompletedReactionId = null, CompletedNotificationId = null };
        var store = new RecordingEscalationStore(state);
        this.ConfigureHealthyBackend();
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using (var monitor = this.CreatePureMonitor(store: store))
        {
            monitor.RecordAgentDeath(17);
            await monitor.StartAsync();
            await monitor.StopAsync();
        }
        var adds = this.mockEnforcementLevelMonitor.Invocations.Count(value => value.Method.Name == nameof(IEnforcementLevelMonitor.AddIssueAsync));
        var notifications = this.mockOutboxManager.Invocations.Count(value => value.Method.Name == nameof(IOutboxManager.EnqueueIntegrityNotificationAsync));
        using var restarted = this.CreatePureMonitor(store: store);
        restarted.RecordAgentDeath(17);
        await restarted.StartAsync();
        await restarted.StopAsync();
        this.mockEnforcementLevelMonitor.Invocations.Count(value => value.Method.Name == nameof(IEnforcementLevelMonitor.AddIssueAsync)).Should().Be(adds);
        this.mockOutboxManager.Invocations.Count(value => value.Method.Name == nameof(IOutboxManager.EnqueueIntegrityNotificationAsync)).Should().Be(notifications);
        store.State!.CompletedReactionId.Should().Be("reaction-agent");
        store.State.CompletedNotificationId.Should().Be("notification-agent");
    }

    [Fact]
    public async Task DurableOwner_SaveFailureBeforeEffect_RetryUsesTheSameKey()
    {
        var store = new RecordingEscalationStore();
        var decision = Decision(20, 20, "save-before-effect", null);
        this.ConfigureHealthyBackend();
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        store.SaveFailureAt = store.SaveCalls + 1;

        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.ExecuteDecisionAsync(decision));
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), "save-before-effect", It.IsAny<CancellationToken>()), Times.Never);
        await monitor.ExecuteDecisionAsync(decision);
        this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), "save-before-effect", It.IsAny<CancellationToken>()), Times.Once);
        store.State!.PendingReactionId.Should().Be("save-before-effect"); store.State.CompletedReactionId.Should().Be("save-before-effect");
        store.State.PendingNotificationId.Should().BeNull(); store.State.CompletedNotificationId.Should().BeNull();
        this.mockOutboxManager.Invocations.Should().BeEmpty();
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());
    }

    [Fact]
    public async Task DurableOwner_ProgressSaveFailure_RetryRepeatsExactEffectAndConverges()
    {
        var store = new RecordingEscalationStore();
        var decision = Decision(21, 21, "progress-retry", "progress-notification");
        this.ConfigureHealthyBackend();
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var monitor = this.CreatePureMonitor(store: store);
        await monitor.StartAsync();
        store.SaveFailureAt = store.SaveCalls + 2;

        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.ExecuteDecisionAsync(decision));
        store.State!.PendingReactionId.Should().Be("progress-retry"); store.State.CompletedReactionId.Should().BeNull();
        store.State.PendingNotificationId.Should().Be("progress-notification"); store.State.CompletedNotificationId.Should().BeNull();
        await Assert.ThrowsAsync<InvalidOperationException>(() => monitor.StopAsync());

        this.mockEnforcementLevelMonitor.Invocations.Clear(); this.mockOutboxManager.Invocations.Clear();
        using (var restarted = this.CreatePureMonitor(store: store))
        {
            await restarted.StartAsync();
            this.mockEnforcementLevelMonitor.Verify(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), "progress-retry", It.IsAny<CancellationToken>()), Times.Once);
            this.mockOutboxManager.Verify(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), "progress-notification", It.IsAny<CancellationToken>()), Times.Once);
            store.State.PendingReactionId.Should().Be("progress-retry"); store.State.CompletedReactionId.Should().Be("progress-retry");
            store.State.PendingNotificationId.Should().Be("progress-notification"); store.State.CompletedNotificationId.Should().Be("progress-notification");
            await restarted.StopAsync();
        }

        this.mockEnforcementLevelMonitor.Invocations.Clear(); this.mockOutboxManager.Invocations.Clear();
        using var finalRestart = this.CreatePureMonitor(store: store);
        await finalRestart.StartAsync();
        this.mockEnforcementLevelMonitor.Invocations.Should().BeEmpty(); this.mockOutboxManager.Invocations.Should().BeEmpty();
        store.State.PendingReactionId.Should().Be("progress-retry"); store.State.CompletedReactionId.Should().Be("progress-retry");
        store.State.PendingNotificationId.Should().Be("progress-notification"); store.State.CompletedNotificationId.Should().Be("progress-notification");
        await finalRestart.StopAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupLoadAfterStop_IsolatedByOutcome(bool fault)
    {
        var state = ValidState("startup-load-reaction", "startup-load-notification") with { CompletedReactionId = null, CompletedNotificationId = null };
        var entered = NewSignal(); var release = NewSignal();
        var store = new RecordingEscalationStore(state)
        {
            LoadBehavior = async _ => { entered.SetResult(); await release.Task; if (fault) throw new InvalidOperationException("stale startup load"); return state; },
        };
        this.ConfigureHealthyBackend();
        using var monitor = this.CreatePureMonitor(store: store);
        var start = Task.Run(() => monitor.StartAsync()); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = monitor.StopAsync(); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start); await stop;
        store.SaveCalls.Should().Be(0); store.State!.PendingReactionId.Should().Be("startup-load-reaction"); store.State.CompletedReactionId.Should().BeNull();
        store.State.PendingNotificationId.Should().Be("startup-load-notification"); store.State.CompletedNotificationId.Should().BeNull();
        this.mockBackendClient.VerifyNoOtherCalls();
        this.mockEnforcementLevelMonitor.VerifyNoOtherCalls(); this.mockOutboxManager.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupReactionAfterStop_DoesNotContinueOrSurfaceStaleFault(bool fault)
    {
        var state = ValidState("startup-reaction", "startup-notification") with { CompletedReactionId = null, CompletedNotificationId = null };
        var store = new RecordingEscalationStore(state);
        var entered = NewSignal(); var release = NewSignal();
        this.ConfigureHealthyBackend();
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns<IssueKey, EnforcementIssueSeverity, string, string?, CancellationToken>(async (_, _, _, _, _) => { entered.SetResult(); await release.Task; if (fault) throw new InvalidOperationException("stale startup reaction"); });
        using var monitor = this.CreatePureMonitor(store: store);
        var start = Task.Run(() => monitor.StartAsync()); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = monitor.StopAsync(); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start); await stop;
        store.SaveCalls.Should().Be(0); store.State!.PendingReactionId.Should().Be("startup-reaction");
        this.mockOutboxManager.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupReactionProgressSaveAfterStop_IsolatedByOutcome(bool fault)
    {
        var state = ValidState("startup-save", null); var store = new RecordingEscalationStore(state);
        this.ConfigureHealthyBackend();
        this.mockEnforcementLevelMonitor.Setup(m => m.AddIssueAsync(It.IsAny<IssueKey>(), It.IsAny<EnforcementIssueSeverity>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        store.SaveGate = NewSignal(); store.SaveException = fault ? new InvalidOperationException("stale startup save") : null;
        using var monitor = this.CreatePureMonitor(store: store);
        var start = Task.Run(() => monitor.StartAsync()); await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = monitor.StopAsync(); await Task.Delay(20);
        store.SaveGate.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start); await stop;
        store.State!.PendingReactionId.Should().Be("startup-save");
        store.State.CompletedReactionId.Should().Be(fault ? null : "startup-save");
        this.mockOutboxManager.Invocations.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupNotificationCompletionAfterStop_DoesNotStartProgressSave(bool fault)
    {
        var state = ValidState("startup-reaction-complete", "startup-notification") with { CompletedReactionId = "startup-reaction-complete", CompletedNotificationId = null };
        var store = new RecordingEscalationStore(state); var entered = NewSignal(); var release = NewSignal();
        this.ConfigureHealthyBackend();
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, string, DateTimeOffset, string, CancellationToken>(async (_, _, _, _, _, _) => { entered.SetResult(); await release.Task; if (fault) throw new InvalidOperationException("stale startup notification"); });
        using var monitor = this.CreatePureMonitor(store: store);
        var start = Task.Run(() => monitor.StartAsync()); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = monitor.StopAsync(); release.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start); await stop;
        store.SaveCalls.Should().Be(0); store.State!.CompletedNotificationId.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StartupNotificationProgressSaveAfterStop_IsolatedByOutcome(bool fault)
    {
        var state = ValidState("startup-reaction-complete", "startup-notification") with { CompletedReactionId = "startup-reaction-complete", CompletedNotificationId = null };
        var store = new RecordingEscalationStore(state) { SaveGate = NewSignal(), SaveException = fault ? new InvalidOperationException("stale notification progress save") : null };
        this.ConfigureHealthyBackend();
        this.mockOutboxManager.Setup(m => m.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        using var monitor = this.CreatePureMonitor(store: store);
        var start = Task.Run(() => monitor.StartAsync()); await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = monitor.StopAsync(); await Task.Delay(20); store.SaveGate.SetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => start); await stop;
        store.State!.PendingNotificationId.Should().Be("startup-notification");
        store.State.CompletedNotificationId.Should().Be(fault ? null : "startup-notification");
    }

    private static IntegrityEscalationState ValidState(string reaction, string? notification, EscalationPhase phase = EscalationPhase.Normal, bool recoveryLatch = false, int trustStreak = 0)
    {
        var descriptor = new IntegrityEscalationEffectDescriptor(
            IntegrityEscalationEffectDescriptor.CurrentVersion,
            IntegrityEscalationReactionKind.AddIssue,
            EnforcementIssueSeverity.Severe,
            "Integrity reaction",
            notification is null ? null : "integrity_degrade_pending",
            notification is null ? null : "title",
            notification is null ? null : "body",
            notification is null ? null : DateTimeOffset.UnixEpoch);
        return new("local", 1, IntegrityEscalationState.CurrentSchemaVersion, 1, 1, 0, trustStreak, phase, null, null, DateTimeOffset.UtcNow, true, phase == EscalationPhase.Degraded, recoveryLatch, reaction, notification is null ? null : reaction, notification, notification)
        {
            PendingEffectDescriptor = descriptor,
        };
    }

    private static IntegrityEscalationState PendingState(DateTimeOffset due)
        => new("local", 1, IntegrityEscalationState.CurrentSchemaVersion, 1, 4, 3, 0, EscalationPhase.Pending, due.AddMinutes(-5), due, due.AddMinutes(-5), true, false, false, "deadline-reaction", "deadline-reaction", null, null)
        {
            PendingEffectDescriptor = new(
                IntegrityEscalationEffectDescriptor.CurrentVersion,
                IntegrityEscalationReactionKind.AddIssue,
                EnforcementIssueSeverity.Severe,
                "Integrity deadline reached",
                null,
                null,
                null,
                null),
        };

    private sealed class RecordingEscalationStore : IIntegrityEscalationStateStore
    {
        public RecordingEscalationStore(IntegrityEscalationState? state = null) => this.State = state;
        public IntegrityEscalationState? State { get; set; }
        public IntegrityEscalationStateEnvelope? Last { get; private set; }
        public IntegrityEscalationStateEnvelope? LastAttempted { get; private set; }
        public int Loads { get; private set; }
        public Action? OnLoad { get; set; }
        public Exception? LoadException { get; set; }
        public Func<CancellationToken, Task<IntegrityEscalationState?>>? LoadBehavior { get; set; }
        public int SaveCalls { get; private set; }
        public int? SaveFailureAt { get; set; }
        public CancellationToken ObservedToken { get; private set; }
        public TaskCompletionSource? SaveGate { get; set; }
        public TaskCompletionSource SaveEntered { get; private set; } = NewSignal();
        public Exception? SaveException { get; set; }
        public async Task<IntegrityEscalationState?> LoadAsync(string identity, CancellationToken cancellationToken = default) { this.Loads++; this.ObservedToken = cancellationToken; this.OnLoad?.Invoke(); if (this.LoadBehavior is not null) return await this.LoadBehavior(cancellationToken); if (this.LoadException is not null) throw this.LoadException; return this.State; }
        public async Task SaveAsync(IntegrityEscalationStateEnvelope value, CancellationToken cancellationToken = default) { this.SaveCalls++; this.LastAttempted = value; if (this.SaveGate is not null) { this.SaveEntered.TrySetResult(); await this.SaveGate.Task; if (this.SaveException is not null) throw this.SaveException; } if (this.SaveCalls == this.SaveFailureAt) throw new InvalidOperationException("save retry"); value.Validate(); this.Last = value; this.State = value.State; }
    }

    private static VerdictDecision Decision(long epoch, long sequence, string reactionKey, string? notificationKey)
        => new(sequence, epoch, new(VerdictAction.Limit, EnforcementIssueSeverity.Warning, "test"), notificationKey is null ? null : new("integrity_degrade_pending", "title", "body", DateTimeOffset.UnixEpoch), DateTimeOffset.UnixEpoch, "scope", reactionKey, notificationKey);

    private void ConfigureHealthyBackend() { this.mockPrivilegeInspector.Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true); this.mockIntegrityChecker.Setup(c => c.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityCheckResult(true, "hash", "agent.exe")); this.mockVerdictHandler.Setup(v => v.HandleVerdict(It.Is<string?>(_ => true), It.IsAny<bool>(), It.IsAny<DateTimeOffset>())).Returns((VerdictReaction)new(VerdictAction.None, null, null)); }

    private void ConfigurePureBackend(string verdict) { this.mockPrivilegeInspector.Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true); this.mockIntegrityChecker.Setup(c => c.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityCheckResult(true, "hash", "agent.exe")); this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityReportResult(true, verdict)); }

    private void ConfigurePureBackendAfterStartup(string verdict) { this.ConfigurePureBackend(verdict); var calls = 0; this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => Interlocked.Increment(ref calls) == 1 ? new IntegrityReportResult(true, "trust") : new IntegrityReportResult(true, verdict)); }

    private AntiTamperMonitor CreatePureMonitor(IBackendIdentityCoordinator? identity = null, IOutboxManager? outbox = null, IIntegrityEscalationStateStore? store = null, IntegrityVerdictHandler? handler = null)
    {
        handler ??= new IntegrityVerdictHandler(clock: () => DateTimeOffset.UtcNow);
        handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        return new(this.mockTimeProvider.Object, outbox ?? this.mockOutboxManager.Object, this.mockPrivilegeInspector.Object, this.mockEnforcementLevelMonitor.Object, this.mockIntegrityChecker.Object, this.mockBackendClient.Object, handler, identityCoordinator: identity, tickSource: _ => new ValueTask<bool>(false), stateStore: store);
    }

    private object? GetGeneration() => this.GetGeneration(this.monitor);

    private object? GetGeneration(AntiTamperMonitor monitor) => typeof(AntiTamperMonitor).GetField("generation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(monitor);

    private static object? GetCallbackMarker(AntiTamperMonitor monitor) { var marker = typeof(AntiTamperMonitor).GetField("callbackGeneration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(monitor)!; return marker.GetType().GetProperty("Value")!.GetValue(marker); }

    private object GetResource(object? generation, string name) => generation!.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(generation)!;

    private void AssertResourcesDisposed(object? generation) { this.GetResourceCount(generation, "CancellationDisposals").Should().Be(1); this.GetResourceCount(generation, "GateDisposals").Should().Be(1); if (this.GetResource(generation, "TickTimer") is not null) this.GetResourceCount(generation, "TickTimerDisposals").Should().Be(1); this.GetResourceCount(generation, "TimezoneTimerDisposals").Should().Be(1); ((Task)this.GetResource(generation, "Loop")).IsCompleted.Should().BeTrue(); ((Task)this.GetResource(generation, "InitialOperation")).IsCompleted.Should().BeTrue(); ((TaskCompletionSource)this.GetResource(generation, "InitialCheck")).Task.IsCompleted.Should().BeTrue(); ((ICollection<Task>)this.GetResource(generation, "OwnedTasks")).Should().BeEmpty(); ((Task)this.GetResource(generation, "Drain")).IsCompleted.Should().BeTrue(); }

    private int GetResourceCount(object? generation, string name) { generation.Should().NotBeNull(); return (int)generation!.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetValue(generation)!; }

    private int[] CollaboratorSnapshot() => this.CollaboratorSnapshot(this.detectedEvents);

    private int[] CollaboratorSnapshot(ConcurrentQueue<TamperEvent> events) => new[] { this.mockBackendClient.Invocations.Count, this.mockPrivilegeInspector.Invocations.Count, this.mockIntegrityChecker.Invocations.Count, this.mockVerdictHandler.Invocations.Count, this.mockEnforcementLevelMonitor.Invocations.Count, this.mockOutboxManager.Invocations.Count, events.Count };

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async ValueTask<bool> ThrowAfterSignal(TaskCompletionSource signal, Exception failure) { await signal.Task; throw failure; }

    private static async ValueTask<bool> FaultingTick(TaskCompletionSource entered, TaskCompletionSource fault, Exception failure) { entered.SetResult(); await fault.Task; throw failure; }

    private void InvokeGeneration(AntiTamperMonitor monitor, string method, object generation)
        => typeof(AntiTamperMonitor).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(value => value.Name == method && value.GetParameters().Length == 1)
            .Invoke(monitor, new[] { generation });

    private void InvokeDeadline(AntiTamperMonitor monitor, object generation, long version, bool elapsed)
        => typeof(AntiTamperMonitor).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(value => value.Name == "AdmitDeadlineCheck" && value.GetParameters().Length == 3)
            .Invoke(monitor, new[] { generation, (object)version, elapsed });

    private long GetDeadlineVersion(object generation)
        => (long)generation.GetType().GetField("DeadlineVersion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(generation)!;

    private AntiTamperMonitor CreateMonitor(Func<CancellationToken, ValueTask<bool>> tickSource, ConcurrentQueue<TamperEvent>? events = null)
        => new(this.mockTimeProvider.Object, this.mockOutboxManager.Object, this.mockPrivilegeInspector.Object, this.mockEnforcementLevelMonitor.Object, this.mockIntegrityChecker.Object, this.mockBackendClient.Object, this.mockVerdictHandler.Object, onTamperDetected: events is null ? null : events.Enqueue, tickSource: tickSource);

    private static async Task<IntegrityReportResult> WaitForReleaseAsync(TaskCompletionSource entered, TaskCompletionSource release) { entered.TrySetResult(); await release.Task; return new IntegrityReportResult(true, "trust"); }

    private static async ValueTask<bool> ReadTickAsync(ChannelReader<int> reader, TaskCompletionSource consumed1, TaskCompletionSource consumed2, CancellationToken token) { var tick = await reader.ReadAsync(token); (tick == 1 ? consumed1 : consumed2).TrySetResult(); return true; }

}
