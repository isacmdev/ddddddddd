// <copyright file="EnforcementLevelMonitorTests.cs" company="ControlParental">
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
/// T12 — Tests for EnforcementLevelMonitor.
/// </summary>
public class EnforcementLevelMonitorTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private readonly Mock<IPrivilegeInspector> mockPrivilegeInspector;
    private readonly Mock<IScmController> mockScmController;
    private readonly Mock<IServiceHealthMonitor> mockHealthMonitor;
    private readonly Mock<ITimeProvider> mockTimeProvider;
    private readonly ConcurrentQueue<EnforcementIssue> detectedIssues;
    private readonly EnforcementLevelMonitor monitor;

    public EnforcementLevelMonitorTests()
    {
        this.mockPrivilegeInspector = new Mock<IPrivilegeInspector>();
        this.mockScmController = new Mock<IScmController>();
        this.mockHealthMonitor = new Mock<IServiceHealthMonitor>();
        this.mockTimeProvider = new Mock<ITimeProvider>();
        this.detectedIssues = new ConcurrentQueue<EnforcementIssue>();

        this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(DateTimeOffset.UtcNow);

        this.monitor = new EnforcementLevelMonitor(
            privilegeInspector: this.mockPrivilegeInspector.Object,
            scmController: this.mockScmController.Object,
            healthMonitor: this.mockHealthMonitor.Object,
            timeProvider: this.mockTimeProvider.Object,
            onIssueDetected: issue => this.detectedIssues.Enqueue(issue));
    }

    public void Dispose()
    {
        this.monitor.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullPrivilegeInspector_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new EnforcementLevelMonitor(
            privilegeInspector: null!,
            scmController: this.mockScmController.Object,
            healthMonitor: this.mockHealthMonitor.Object,
            timeProvider: this.mockTimeProvider.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("privilegeInspector");
    }

    [Fact]
    public void Constructor_WithNullScmController_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new EnforcementLevelMonitor(
            privilegeInspector: this.mockPrivilegeInspector.Object,
            scmController: null!,
            healthMonitor: this.mockHealthMonitor.Object,
            timeProvider: this.mockTimeProvider.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("scmController");
    }

    // ── Initial State Tests ─────────────────────────────────────────

    [Fact]
    public void InitialState_CurrentLevelIsUnknown()
    {
        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Unknown);
    }

    [Fact]
    public void InitialState_IsCriticalIsFalse()
    {
        // Assert
        this.monitor.IsCritical.Should().BeFalse();
    }

    [Fact]
    public void InitialState_CurrentIssuesIsEmpty()
    {
        // Assert
        this.monitor.CurrentIssues.Should().BeEmpty();
    }

    [Fact]
    public void InitialState_LastEvaluationTimeIsNull()
    {
        // Assert
        this.monitor.LastEvaluationTime.Should().BeNull();
    }

    // ── StartAsync Tests ─────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_WhenNotDisposed_StartsSuccessfully()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.monitor.StartAsync();

        // Assert
        this.monitor.CurrentLevel.Should().NotBe(EnforcementLevel.Unknown);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_DoesNotThrow()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
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

    // ── RecordAgentHeartbeat Tests ─────────────────────────────────

    [Fact]
    public void RecordAgentHeartbeat_DoesNotThrow()
    {
        // Act & Assert
        var act = () => this.monitor.RecordAgentHeartbeat();
        act.Should().NotThrow();
    }

    // ── RecordForegroundChange Tests ───────────────────────────────

    [Fact]
    public void RecordForegroundChange_DoesNotThrow()
    {
        // Act & Assert
        var act = () => this.monitor.RecordForegroundChange();
        act.Should().NotThrow();
    }

    // ── LevelChanged Event Tests ───────────────────────────────

    [Fact]
    public async Task LevelChanged_WhenLevelChanges_FiresEvent()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        EnforcementLevelChangedEventArgs? receivedArgs = null;
        this.monitor.LevelChanged += (_, args) => receivedArgs = args;

        await this.monitor.StartAsync();

        // Assert
        receivedArgs.Should().NotBeNull();
    }

    // ── IssueDetected Event Tests ───────────────────────────────

    [Fact]
    public async Task IssueDetected_WhenIssueDetected_FiresEvent()
    {
        // Arrange
        // Service not running
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(false);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns((DateTimeOffset?)null);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        EnforcementIssue? receivedIssue = null;
        this.monitor.IssueDetected += (_, args) => receivedIssue = args.Issue;

        await this.monitor.StartAsync();

        // Assert
        receivedIssue.Should().NotBeNull();
        receivedIssue!.Type.Should().BeOneOf(
            EnforcementIssueType.ServiceNotRunning,
            EnforcementIssueType.AgentNotResponding,
            EnforcementIssueType.PreventiveLayerUnavailable);
    }

    // ── EvaluateAsync Tests ─────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_WhenServiceRunning_ReturnsStandardOrManaged()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.monitor.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().BeOneOf(EnforcementLevel.Standard, EnforcementLevel.Managed);
    }

    [Fact]
    public async Task EvaluateAsync_WhenServiceNotRunning_ReturnsDegraded()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(false);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns((DateTimeOffset?)null);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.monitor.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentIssues.Should().Contain(i => i.Type == EnforcementIssueType.ServiceNotRunning);
    }

    [Fact]
    public async Task EvaluateAsync_WhenChildIsAdmin_ReturnsDegraded()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false); // Child is admin

        // Act
        await this.monitor.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentIssues.Should().Contain(i => i.Type == EnforcementIssueType.ChildIsAdministrator);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAgentUnhealthy_ReturnsDegraded()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(false);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns((DateTimeOffset?)null);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.monitor.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentIssues.Should().Contain(i => i.Type == EnforcementIssueType.AgentNotResponding);
    }

    // ── CurrentIssues Tests ─────────────────────────────────────

    [Fact]
    public async Task CurrentIssues_ReturnsCorrectIssues()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(false);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns((DateTimeOffset?)null);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await this.monitor.EvaluateAsync();

        // Assert
        var issues = this.monitor.CurrentIssues;
        issues.Should().HaveCountGreaterOrEqualTo(2);
        issues.Should().Contain(i => i.Type == EnforcementIssueType.ServiceNotRunning);
        issues.Should().Contain(i => i.Type == EnforcementIssueType.ChildIsAdministrator);
    }

    // ── IsCritical Tests ─────────────────────────────────────

    [Fact]
    public async Task IsCritical_WhenNoSevereIssues_ReturnsFalse()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.monitor.EvaluateAsync();

        // Assert
        this.monitor.IsCritical.Should().BeFalse();
    }

    [Fact]
    public async Task IsCritical_WhenServiceNotRunning_ReturnsTrue()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(false);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns((DateTimeOffset?)null);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.monitor.EvaluateAsync();

        // Assert
        this.monitor.IsCritical.Should().BeTrue();
    }

    // ── StopAsync Tests ─────────────────────────────────────

    [Fact]
    public async Task StopAsync_WhenRunning_StopsSuccessfully()
    {
        // Arrange
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await this.monitor.StartAsync();

        // Act
        await this.monitor.StopAsync();

        // Assert - no exception
    }

    // ── RecordAgentAlive Tests ─────────────────────────────────

    [Fact]
    public void RecordAgentAlive_DoesNotThrow()
    {
        // Act & Assert
        var act = () => this.monitor.RecordAgentAlive();
        act.Should().NotThrow();
    }
}
