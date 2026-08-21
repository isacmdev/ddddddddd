// <copyright file="AntiTamperMonitorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Collections.Concurrent;
using System.Threading.Channels;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
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
    public async Task ConcurrentStart_SharesOutcomeAndCleansOneGeneration(string outcome) { using var cts = new CancellationTokenSource(); var entered = NewSignal(); var release = NewSignal(); var failure = new InvalidOperationException("shared initialization failure"); this.ConfigureHealthyBackend(); this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>(async (_, token) => { entered.SetResult(); if (outcome == "cancel") { await Task.Delay(Timeout.Infinite, token); } await release.Task; if (outcome == "failure") throw failure; return new(false, ""); }); var before = (int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!; var a = this.monitor.StartAsync(outcome == "cancel" ? cts.Token : default); await entered.Task; var b = this.monitor.StartAsync(); var generation = this.GetGeneration(); if (outcome == "cancel") cts.Cancel(); release.SetResult(); if (outcome == "success") await Task.WhenAll(a, b); else { var ea = await Assert.ThrowsAnyAsync<Exception>(() => a); var eb = await Assert.ThrowsAnyAsync<Exception>(() => b); ea.GetType().Should().Be(eb.GetType()); if (outcome == "failure") { ea.Should().BeSameAs(failure); ea.Message.Should().Be(failure.Message); } } this.mockBackendClient.Verify(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()), Times.Once); ((int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!).Should().Be(before + 1); await this.monitor.StopAsync(); this.AssertResourcesDisposed(generation); this.GetGeneration().Should().BeNull(); }

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
        newGeneration.Should().NotBeSameAs(oldGeneration); oldResources.Should().OnlyHaveUniqueItems(); names.Select(name => this.GetResource(newGeneration, name)).Should().NotContain(oldResources); this.AssertResourcesDisposed(oldGeneration); this.InvokeGeneration("AdmitPeriodicCheck", oldGeneration); this.InvokeGeneration("AdmitTimezoneCheck", oldGeneration); newGeneration.Should().BeSameAs(this.GetGeneration()); var newStop = this.monitor.StopAsync(); var newDrain = this.GetResource(newGeneration, "Drain"); await newStop; new[] { oldDrain, newDrain }.Should().OnlyHaveUniqueItems(); this.AssertResourcesDisposed(newGeneration); await this.monitor.StopAsync(); this.monitor.Dispose(); this.AssertResourcesDisposed(oldGeneration); this.AssertResourcesDisposed(newGeneration);
    }

    [Fact]
    public async Task ConcurrentStopAndDisposeShareLifecycleOwnership() { this.ConfigureHealthyBackend(); var e = NewSignal(); var r = NewSignal(); var calls = 0; this.mockBackendClient.Setup(c => c.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>())).Returns<IntegrityReport, CancellationToken>((_, _) => Interlocked.Increment(ref calls) == 1 ? Task.FromResult(new IntegrityReportResult(true, "trust")) : WaitForReleaseAsync(e, r)); await this.monitor.StartAsync(); var generation = this.GetGeneration(); var work = this.monitor.TriggerIntegrityCheckAsync(); await e.Task; var allocations = (int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!; var a = Task.Factory.StartNew(this.monitor.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default); var b = Task.Factory.StartNew(this.monitor.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default); a.IsCompleted.Should().BeFalse(); b.IsCompleted.Should().BeFalse(); r.SetResult(); await Task.WhenAll(work, a, b); this.AssertResourcesDisposed(generation); this.GetGeneration().Should().BeNull(); this.monitor.Dispose(); await Assert.ThrowsAsync<ObjectDisposedException>(() => this.monitor.StartAsync()); ((int)typeof(AntiTamperMonitor).GetField("generationAllocations", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor)!).Should().Be(allocations); }

    [Fact]
    public async Task TamperCallback_CanRequestStopWithoutSelfDeadlock() { this.ConfigureHealthyBackend(); await this.monitor.StartAsync(); var generation = this.GetGeneration()!; generation.GetType().GetField("Timezone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(generation, "other"); var callback = NewSignal(); Task? requested = null; this.monitor.TamperDetected += (_, _) => { requested = this.monitor.StopAsync(); this.monitor.Dispose(); callback.SetResult(); }; typeof(AntiTamperMonitor).GetMethod("AdmitTimezoneCheck", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(this.monitor, new[] { generation }); await callback.Task; requested.Should().NotBeNull(); await this.monitor.StopAsync(); this.AssertResourcesDisposed(generation); }

    [Fact] public async Task SynchronousAdmission_IsRemovedBeforeDrain() { this.ConfigureHealthyBackend(); await this.monitor.StartAsync(); var generation = this.GetGeneration()!; await this.monitor.TriggerIntegrityCheckAsync(); ((ICollection<Task>)generation.GetType().GetField("OwnedTasks", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(generation)!).Should().BeEmpty(); await this.monitor.StopAsync(); }

    private void ConfigureHealthyBackend() { this.mockPrivilegeInspector.Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true); this.mockIntegrityChecker.Setup(c => c.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new IntegrityCheckResult(true, "hash", "agent.exe")); this.mockVerdictHandler.Setup(v => v.HandleVerdict(It.Is<string?>(_ => true), It.IsAny<bool>(), It.IsAny<DateTimeOffset>())).Returns((VerdictReaction)new(VerdictAction.None, null, null)); }

    private object? GetGeneration() => typeof(AntiTamperMonitor).GetField("generation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(this.monitor);

    private static object? GetCallbackMarker(AntiTamperMonitor monitor) { var marker = typeof(AntiTamperMonitor).GetField("callbackGeneration", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(monitor)!; return marker.GetType().GetProperty("Value")!.GetValue(marker); }

    private object GetResource(object? generation, string name) => generation!.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(generation)!;

    private void AssertResourcesDisposed(object? generation) { this.GetResourceCount(generation, "CancellationDisposals").Should().Be(1); this.GetResourceCount(generation, "GateDisposals").Should().Be(1); this.GetResourceCount(generation, "TickTimerDisposals").Should().Be(1); this.GetResourceCount(generation, "TimezoneTimerDisposals").Should().Be(1); ((Task)this.GetResource(generation, "Loop")).IsCompleted.Should().BeTrue(); ((Task)this.GetResource(generation, "InitialOperation")).IsCompleted.Should().BeTrue(); ((TaskCompletionSource)this.GetResource(generation, "InitialCheck")).Task.IsCompleted.Should().BeTrue(); ((ICollection<Task>)this.GetResource(generation, "OwnedTasks")).Should().BeEmpty(); ((Task)this.GetResource(generation, "Drain")).IsCompleted.Should().BeTrue(); }

    private int GetResourceCount(object? generation, string name) { generation.Should().NotBeNull(); return (int)generation!.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetValue(generation)!; }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void InvokeGeneration(string method, object generation) => typeof(AntiTamperMonitor).GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(this.monitor, new[] { generation });

    private AntiTamperMonitor CreateMonitor(Func<CancellationToken, ValueTask<bool>> tickSource)
        => new(this.mockTimeProvider.Object, this.mockOutboxManager.Object, this.mockPrivilegeInspector.Object, this.mockEnforcementLevelMonitor.Object, this.mockIntegrityChecker.Object, this.mockBackendClient.Object, this.mockVerdictHandler.Object, tickSource: tickSource);

    private static async Task<IntegrityReportResult> WaitForReleaseAsync(TaskCompletionSource entered, TaskCompletionSource release) { entered.TrySetResult(); await release.Task; return new IntegrityReportResult(true, "trust"); }

    private static async ValueTask<bool> ReadTickAsync(ChannelReader<int> reader, TaskCompletionSource consumed1, TaskCompletionSource consumed2, CancellationToken token) { var tick = await reader.ReadAsync(token); (tick == 1 ? consumed1 : consumed2).TrySetResult(); return true; }

}
