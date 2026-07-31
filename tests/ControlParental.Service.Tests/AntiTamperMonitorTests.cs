// <copyright file="AntiTamperMonitorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Collections.Concurrent;
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

    // ── StartAsync Tests ─────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_WhenNotDisposed_StartsSuccessfully()
    {
        // Arrange
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
        this.monitor.Dispose();
        var disposedMonitor = this.monitor;

        // Act & Assert
        var act = () => disposedMonitor.StartAsync();
        await act.Should().ThrowExactlyAsync<ObjectDisposedException>();
    }

    // ── StopAsync Tests ─────────────────────────────────────────────

    [Fact]
    public async Task StopAsync_WhenRunning_StopsSuccessfully()
    {
        // Arrange
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await this.monitor.StartAsync();

        // Act
        await this.monitor.StopAsync();

        // Assert - no exception
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
    public async Task VerifyClockAgainstServerTimeAsync_WhenDriftExceedsJumpThreshold_FiresClockJump()
    {
        // Arrange
        var localTime = DateTimeOffset.UtcNow;
        var serverTime = localTime.AddMinutes(-10); // Server is 10 minutes behind

        this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(localTime);

        ClockJumpEventArgs? capturedArgs = null;
        this.monitor.OnClockJumpDetected += (_, args) => capturedArgs = args;

        // Act
        await this.monitor.VerifyClockAgainstServerTimeAsync(serverTime);

        // Assert
        capturedArgs.Should().NotBeNull();
        capturedArgs!.Direction.Should().Be(1); // Forward (local is ahead of server)
    }

    [Fact]
    public async Task VerifyClockAgainstServerTimeAsync_WhenDisposed_DoesNotThrow()
    {
        // Arrange
        this.monitor.Dispose();
        var disposedMonitor = this.monitor;

        // Act & Assert
        var act = () => disposedMonitor.VerifyClockAgainstServerTimeAsync(DateTimeOffset.UtcNow);
        await act.Should().NotThrowAsync();
    }

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
}
