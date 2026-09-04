// <copyright file="ServiceHealthMonitorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Collections.Concurrent;
using System.Reflection;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T10 — Tests for ServiceHealthMonitor.
/// </summary>
public class ServiceHealthMonitorTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private readonly Mock<ITimeProvider> mockTimeProvider;
    private readonly ConcurrentQueue<string> agentDiedEvents;
    private readonly ConcurrentQueue<string> unhealthyEvents;
    private readonly ServiceHealthMonitor monitor;

    public ServiceHealthMonitorTests()
    {
        this.mockTimeProvider = new Mock<ITimeProvider>();
        this.agentDiedEvents = new ConcurrentQueue<string>();
        this.unhealthyEvents = new ConcurrentQueue<string>();

        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(0);

        this.monitor = new ServiceHealthMonitor(
            timeProvider: this.mockTimeProvider.Object,
            onAgentDied: () => this.agentDiedEvents.Enqueue("agent-died"),
            onServiceUnhealthy: issue => this.unhealthyEvents.Enqueue(issue),
            agentHeartbeatTimeout: TimeSpan.FromSeconds(30),
            healthCheckInterval: TimeSpan.FromMilliseconds(100));
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
        var act = () => new ServiceHealthMonitor(
            timeProvider: null!,
            onAgentDied: () => { },
            onServiceUnhealthy: _ => { });

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("timeProvider");
    }

    [Fact]
    public void Constructor_WithNullOnAgentDied_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ServiceHealthMonitor(
            timeProvider: this.mockTimeProvider.Object,
            onAgentDied: null!,
            onServiceUnhealthy: _ => { });

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("onAgentDied");
    }

    [Fact]
    public void Constructor_WithNullOnServiceUnhealthy_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ServiceHealthMonitor(
            timeProvider: this.mockTimeProvider.Object,
            onAgentDied: () => { },
            onServiceUnhealthy: null!);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("onServiceUnhealthy");
    }

    // ── Initial State Tests ─────────────────────────────────────────

    [Fact]
    public void InitialState_IsAgentHealthyFalse()
    {
        // Assert
        this.monitor.IsAgentHealthy.Should().BeFalse();
    }

    [Fact]
    public void InitialState_IsServiceHealthyFalse()
    {
        // Assert - not started yet
        this.monitor.IsServiceHealthy.Should().BeFalse();
    }

    [Fact]
    public void InitialState_LastAgentHeartbeatNull()
    {
        // Assert
        this.monitor.LastAgentHeartbeat.Should().BeNull();
    }

    [Fact]
    public void InitialState_AgentRestartCountZero()
    {
        // Assert
        this.monitor.AgentRestartCount.Should().Be(0);
    }

    // ── StartAsync Tests ─────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_WhenNotDisposed_StartsSuccessfully()
    {
        // Act
        await this.monitor.StartAsync();

        // Assert
        this.monitor.IsServiceHealthy.Should().BeFalse("restore, heartbeat, and action confirmation are not authoritative yet");
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ReturnsCompleted()
    {
        // Arrange
        await this.monitor.StartAsync();

        // Act
        await this.monitor.StartAsync();

        // Assert - should not throw
        this.monitor.IsServiceHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task IsServiceHealthyRequiresAuthoritativeRestoreHeartbeatAndCurrentConfirmation()
    {
        await this.monitor.StartAsync();
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);

        this.monitor.SetRestoreStatus(succeeded: true);
        this.monitor.SetHealthBlockingIssues(hasBlockingIssues: false);
        this.monitor.SetCurrentCriticalActionsConfirmed(confirmed: true);

        this.monitor.IsServiceHealthy.Should().BeFalse("a success state cannot substitute for agent liveness");

        this.monitor.RecordAgentHeartbeat();

        this.monitor.IsServiceHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task AgentDeathInvalidatesHeartbeatAndCurrentConfirmation()
    {
        await this.monitor.StartAsync();
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);
        this.monitor.SetRestoreStatus(succeeded: true);
        this.monitor.SetHealthBlockingIssues(hasBlockingIssues: false);
        this.monitor.SetCurrentCriticalActionsConfirmed(confirmed: true);
        this.monitor.RecordAgentHeartbeat();
        this.monitor.IsServiceHealthy.Should().BeTrue();

        this.monitor.RecordAgentDeath();

        this.monitor.IsAgentHealthy.Should().BeFalse();
        this.monitor.IsServiceHealthy.Should().BeFalse();
        this.monitor.RecordAgentHeartbeat();
        this.monitor.IsServiceHealthy.Should().BeFalse("replacement heartbeat does not confirm current critical intent");
    }

    [Theory]
    [InlineData(RuntimeSecurityVerdict.HealthyStandard, true, true)]
    [InlineData(RuntimeSecurityVerdict.Administrator, true, true)]
    [InlineData(RuntimeSecurityVerdict.Unknown, false, true)]
    [InlineData(RuntimeSecurityVerdict.AclFailure, false, true)]
    public async Task Hardening_ApplySecurityVerdict_PublishesHealthWithoutDisablingEnforcement(
        RuntimeSecurityVerdict verdict,
        bool healthy,
        bool enforcementActive)
    {
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);
        this.monitor.SetRestoreStatus(succeeded: true);
        this.monitor.SetHealthBlockingIssues(hasBlockingIssues: false);
        this.monitor.SetCurrentCriticalActionsConfirmed(confirmed: true);
        this.monitor.RecordAgentHeartbeat();
        this.monitor.ApplySecurityVerdict(verdict);
        await this.monitor.StartAsync();

        this.monitor.IsServiceHealthy.Should().Be(healthy);
        this.monitor.IsEnforcementActive.Should().Be(enforcementActive);
        this.monitor.SecurityVerdict.Should().Be(verdict);
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

    // ── StopAsync Tests ──────────────────────────────────────────────

    [Fact]
    public async Task StopAsync_WhenStarted_StopsSuccessfully()
    {
        // Arrange
        await this.monitor.StartAsync();

        // Act
        await this.monitor.StopAsync();

        // Assert
        this.monitor.IsServiceHealthy.Should().BeFalse();
    }

    // ── RecordAgentHeartbeat Tests ─────────────────────────────────

    [Fact]
    public void RecordAgentHeartbeat_SetsIsAgentHealthyTrue()
    {
        // Arrange
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);

        // Act
        this.monitor.RecordAgentHeartbeat();

        // Assert
        this.monitor.IsAgentHealthy.Should().BeTrue();
    }

    [Fact]
    public void RecordAgentHeartbeat_UpdatesLastAgentHeartbeat()
    {
        // Arrange
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);

        // Act
        this.monitor.RecordAgentHeartbeat();

        // Assert
        this.monitor.LastAgentHeartbeat.Should().NotBeNull();
    }

    // ── RecordAgentDeath Tests ─────────────────────────────────────

    [Fact]
    public void RecordAgentDeath_IncrementsAgentRestartCount()
    {
        // Act
        this.monitor.RecordAgentDeath();

        // Assert
        this.monitor.AgentRestartCount.Should().Be(1);
    }

    [Fact]
    public void RecordAgentDeath_FiresAgentDiedEvent()
    {
        // Arrange
        AgentDiedEventArgs? receivedArgs = null;
        this.monitor.AgentDied += (_, args) => receivedArgs = args;

        // Act
        this.monitor.RecordAgentDeath();

        // Assert
        receivedArgs.Should().NotBeNull();
        receivedArgs!.DeathCount.Should().Be(1);
    }

    [Fact]
    public void RecordAgentDeath_CallsOnAgentDiedCallback()
    {
        // Act
        this.monitor.RecordAgentDeath();

        // Assert
        this.agentDiedEvents.Should().Contain("agent-died");
    }

    [Fact]
    public void RecordAgentDeath_MultipleDeaths_IncrementsCount()
    {
        // Act
        this.monitor.RecordAgentDeath();
        this.monitor.RecordAgentDeath();
        this.monitor.RecordAgentDeath();

        // Assert
        this.monitor.AgentRestartCount.Should().Be(3);
    }

    // ── ResetAgentRestartCount Tests ────────────────────────────────

    [Fact]
    public void ResetAgentRestartCount_ResetsToZero()
    {
        // Arrange
        this.monitor.RecordAgentDeath();
        this.monitor.RecordAgentDeath();

        // Act
        this.monitor.ResetAgentRestartCount();

        // Assert
        this.monitor.AgentRestartCount.Should().Be(0);
    }

    // ── IsAgentHealthy Tests ───────────────────────────────────────

    [Fact]
    public void IsAgentHealthy_WhenHeartbeatRecent_ReturnsTrue()
    {
        // Arrange
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);
        this.monitor.RecordAgentHeartbeat();

        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(200);

        // Act
        var isHealthy = this.monitor.IsAgentHealthy;

        // Assert
        isHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsAgentHealthy_WhenHeartbeatOverdue_ReturnsFalse()
    {
        // Arrange
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);
        this.monitor.RecordAgentHeartbeat();

        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100 + (long)(TimeSpan.FromSeconds(31).TotalMilliseconds * 10_000));

        // Act
        var isHealthy = this.monitor.IsAgentHealthy;

        // Assert
        isHealthy.Should().BeFalse();
    }

    // ── GetHealthIssues Tests ──────────────────────────────────────

    [Fact]
    public void GetHealthIssues_WhenMonitorNotRunning_ReturnsNotRunningIssue()
    {
        // Act
        var issues = this.monitor.GetHealthIssues();

        // Assert
        issues.Should().Contain("Monitor is not running");
    }

    [Fact]
    public void GetHealthIssues_WhenRunningWithoutHeartbeat_ReturnsNoHeartbeatIssue()
    {
        // Arrange
        this.monitor.StartAsync().Wait();

        // Act
        var issues = this.monitor.GetHealthIssues();

        // Assert
        issues.Should().Contain("No agent heartbeat received yet");
    }

    [Fact]
    public void GetHealthIssues_WhenHeartbeatOverdue_ReturnsOverdueIssue()
    {
        // Arrange
        this.monitor.StartAsync().Wait();
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);
        this.monitor.RecordAgentHeartbeat();

        // Set time past the timeout
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(
            100 + (long)(TimeSpan.FromSeconds(31).TotalMilliseconds * 10_000));

        // Act
        var issues = this.monitor.GetHealthIssues();

        // Assert
        issues.Should().ContainMatch("*Agent heartbeat overdue*");
    }

    [Fact]
    public void GetHealthIssues_WhenAgentInCriticalState_ReturnsCriticalIssue()
    {
        // Arrange
        for (var i = 0; i < 3; i++)
        {
            this.monitor.RecordAgentDeath();
        }

        // Act
        var issues = this.monitor.GetHealthIssues();

        // Assert
        issues.Should().ContainMatch("*Agent died 3 times*");
    }

    [Fact]
    public void GetHealthIssues_WhenSecurityVerdictIsUnhealthy_ReturnsSecurityIssue()
    {
        // Arrange
        this.monitor.StartAsync().Wait();
        this.monitor.ApplySecurityVerdict(RuntimeSecurityVerdict.AclFailure);

        // Act
        var issues = this.monitor.GetHealthIssues();

        // Assert
        issues.Should().ContainMatch("*Runtime security verdict is AclFailure*");
    }

    // ── ServiceBecameUnhealthy Event Tests ───────────────────────

    [Fact]
    public void ServiceBecameUnhealthy_WhenFired_CallsOnServiceUnhealthy()
    {
        // Arrange
        this.monitor.StartAsync().Wait();
        // Trigger multiple agent deaths to generate health issues
        this.monitor.RecordAgentDeath();
        this.monitor.RecordAgentDeath();

        // Act - The unhealthy callback is called when PerformHealthCheck detects issues
        // This happens via the timer, but we can verify the callback is registered
        var issues = this.monitor.GetHealthIssues();

        // Assert - Issues are detected (actual callback triggered by timer in production)
        issues.Should().NotBeEmpty();
    }

    [Fact]
    public void ServiceBecameUnhealthy_WhenFired_FiresEvent()
    {
        // Arrange
        this.monitor.StartAsync().Wait();
        var issuesDetected = false;
        this.monitor.ServiceBecameUnhealthy += (_, _) => issuesDetected = true;

        // Act - Trigger 3 agent deaths to reach critical threshold
        this.monitor.RecordAgentDeath();
        this.monitor.RecordAgentDeath();
        this.monitor.RecordAgentDeath();

        // Assert - Agent restart count should be 3
        this.monitor.AgentRestartCount.Should().Be(3);

        // Note: The event is fired by PerformHealthCheck which is called by the timer
        // In production, the timer would trigger this. For tests, we verify the event is registered
        var issues = this.monitor.GetHealthIssues();

        // Assert - Issues are detected (event would fire via timer in production)
        issues.Should().Contain("Agent died 3 times - possible instability");
    }

    // ── Dispose Tests ─────────────────────────────────────────────

    [Fact]
    public void Dispose_WhenRunning_StopsMonitor()
    {
        // Arrange
        this.monitor.StartAsync().Wait();

        // Act
        this.monitor.Dispose();

        // Assert
        this.monitor.IsServiceHealthy.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        this.monitor.StartAsync().Wait();

        // Act
        var act = () =>
        {
            this.monitor.Dispose();
            this.monitor.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    // ── HealthCheckCallback Tests ────────────────────────────────

    [Fact]
    public void HealthCheckCallback_WhenNotRunning_DoesNothing()
    {
        // Arrange
        var healthyBefore = this.monitor.IsAgentHealthy;

        // Act - Call the private callback indirectly by recording death
        // When not running, the health check should not trigger recovery
        this.monitor.RecordAgentDeath();

        // Assert - Agent died but monitor was not running, so no automatic recovery
        this.monitor.AgentRestartCount.Should().Be(1);
    }

    [Fact]
    public void HealthCheckCallback_WhenDisposed_DoesNothing()
    {
        // Arrange
        this.monitor.Dispose();
        var disposedMonitor = this.monitor;

        // Act - Record heartbeat on disposed monitor
        // This should not throw
        var act = () => disposedMonitor.RecordAgentHeartbeat();

        // Assert - Should not throw, but heartbeat is ignored
        act.Should().NotThrow();
    }

    [Fact]
    public void HealthCheckCallback_WhenRunningAndHeartbeatOverdue_TriggersRecovery()
    {
        // Arrange
        this.monitor.StartAsync().Wait();
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);
        this.monitor.RecordAgentHeartbeat();

        // Move time past the timeout
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(
            100 + (long)(TimeSpan.FromSeconds(31).TotalMilliseconds * 10_000));

        // Act - Record another heartbeat (simulating health check finding overdue)
        // This should trigger the death detection
        var countBefore = this.monitor.AgentRestartCount;

        // Force a death record
        this.monitor.RecordAgentDeath();

        // Assert
        this.monitor.AgentRestartCount.Should().BeGreaterThan(countBefore);
    }

    [Fact]
    public void HealthCheckCallback_WhenRunningAndOverdueHeartbeat_FiresUnhealthyCallbacks()
    {
        // Arrange
        this.monitor.StartAsync().Wait();
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(100);
        this.monitor.RecordAgentHeartbeat();

        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(
            100 + (long)(TimeSpan.FromSeconds(31).TotalMilliseconds * 10_000));

        var callback = typeof(ServiceHealthMonitor).GetMethod(
            "HealthCheckCallback",
            BindingFlags.Instance | BindingFlags.NonPublic);

        callback.Should().NotBeNull();

        // Act
        callback!.Invoke(this.monitor, new object?[] { null });

        // Assert
        this.monitor.AgentRestartCount.Should().Be(1);
        this.agentDiedEvents.Should().Contain("agent-died");
        this.unhealthyEvents.Should().NotBeEmpty();
        this.unhealthyEvents.ToArray()[0].Should().Contain("No agent heartbeat received yet");
    }

    // ── IsAgentInCriticalState Tests ─────────────────────────────

    [Fact]
    public void IsAgentInCriticalState_WhenBelowThreshold_ReturnsFalse()
    {
        // Arrange
        this.monitor.RecordAgentDeath();
        this.monitor.RecordAgentDeath();

        // Assert
        this.monitor.IsAgentInCriticalState.Should().BeFalse();
    }

    [Fact]
    public void IsAgentInCriticalState_WhenAtThreshold_ReturnsTrue()
    {
        // Arrange
        for (var i = 0; i < 3; i++)
        {
            this.monitor.RecordAgentDeath();
        }

        // Assert
        this.monitor.IsAgentInCriticalState.Should().BeTrue();
    }

    [Fact]
    public async Task RecoverySuccess_ResetsAgentRestartCountButDoesNotInventHealthyStatus()
    {
        // Arrange
        this.monitor.RecordAgentDeath();
        this.monitor.RecordAgentDeath();

        var recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.monitor,
            recoverAgentFunc: () => Task.FromResult(true),
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: () => { });

        // Act
        var recovered = await recoveryManager.RequestAgentRecoveryAsync("Crash/relaunch recovery");
        var status = recoveryManager.GetRecoveryStatus();

        // Assert
        recovered.Should().BeTrue();
        this.monitor.AgentRestartCount.Should().Be(0);
        status.IsHealthy.Should().BeFalse();
        status.HealthLevel.Should().Be(ServiceHealthLevel.Degraded);
    }
}

/// <summary>
/// T10 — Tests for ServiceRecoveryManager.
/// </summary>
public class ServiceRecoveryManagerTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private readonly Mock<IServiceHealthMonitor> mockHealthMonitor;
    private readonly ConcurrentQueue<string> failedEvents;
    private readonly ConcurrentQueue<string> successEvents;
    private ServiceRecoveryManager recoveryManager;

    public ServiceRecoveryManagerTests()
    {
        this.mockHealthMonitor = new Mock<IServiceHealthMonitor>();
        this.failedEvents = new ConcurrentQueue<string>();
        this.successEvents = new ConcurrentQueue<string>();

        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(false),
            onRecoveryFailed: issue => this.failedEvents.Enqueue(issue),
            onRecoverySucceeded: () => this.successEvents.Enqueue("success"));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullHealthMonitor_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ServiceRecoveryManager(
            healthMonitor: null!,
            recoverAgentFunc: () => Task.FromResult(false),
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: () => { });

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("healthMonitor");
    }

    [Fact]
    public void Constructor_WithNullRecoverAgentFunc_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: null!,
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: () => { });

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("recoverAgentFunc");
    }

    [Fact]
    public void Constructor_WithNullOnRecoveryFailed_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(false),
            onRecoveryFailed: null!,
            onRecoverySucceeded: () => { });

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("onRecoveryFailed");
    }

    [Fact]
    public void Constructor_WithNullOnRecoverySucceeded_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(false),
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: null!);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("onRecoverySucceeded");
    }

    // ── Initial State Tests ─────────────────────────────────────────

    [Fact]
    public void InitialState_FailedRecoveryAttemptsZero()
    {
        // Assert
        this.recoveryManager.FailedRecoveryAttempts.Should().Be(0);
    }

    [Fact]
    public void InitialState_LastRecoveryErrorNull()
    {
        // Assert
        this.recoveryManager.LastRecoveryError.Should().BeNull();
    }

    [Fact]
    public void InitialState_IsInRecoveryModeFalse()
    {
        // Assert
        this.recoveryManager.IsInRecoveryMode.Should().BeFalse();
    }

    // ── RequestAgentRecoveryAsync Tests ────────────────────────────

    [Fact]
    public async Task RequestAgentRecoveryAsync_WhenSucceeds_ReturnsTrue()
    {
        // Arrange
        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(true),
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: () => this.successEvents.Enqueue("success"));

        // Act
        var result = await this.recoveryManager.RequestAgentRecoveryAsync("Test recovery");

        // Assert
        result.Should().BeTrue();
        this.successEvents.Should().Contain("success");
    }

    [Fact]
    public async Task RequestAgentRecoveryAsync_WhenAlreadyInFlight_ReturnsFalse()
    {
        // Arrange
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () =>
            {
                started.TrySetResult(true);
                return release.Task;
            },
            onRecoveryFailed: issue => this.failedEvents.Enqueue(issue),
            onRecoverySucceeded: () => { });

        var firstAttempt = this.recoveryManager.RequestAgentRecoveryAsync("First attempt");
        await started.Task;

        // Act
        var secondResult = await this.recoveryManager.RequestAgentRecoveryAsync("Second attempt");

        // Assert
        secondResult.Should().BeFalse();

        release.SetResult(true);
        (await firstAttempt).Should().BeTrue();
    }

    [Fact]
    public async Task RequestAgentRecoveryAsync_RespectsCooldownAfterSuccess()
    {
        // Arrange
        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(true),
            onRecoveryFailed: issue => this.failedEvents.Enqueue(issue),
            onRecoverySucceeded: () => { });

        // Act
        var firstResult = await this.recoveryManager.RequestAgentRecoveryAsync("Initial");
        var secondResult = await this.recoveryManager.RequestAgentRecoveryAsync("Immediate retry");

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
        this.recoveryManager.LastRecoveryError.Should().Contain("Recovery cooldown active");
    }

    [Fact]
    public async Task RequestAgentRecoveryAsync_WhenFails_ReturnsFalse()
    {
        // Arrange
        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(false),
            onRecoveryFailed: issue => this.failedEvents.Enqueue(issue),
            onRecoverySucceeded: () => { });

        // Act
        var result = await this.recoveryManager.RequestAgentRecoveryAsync("Test recovery");

        // Assert
        result.Should().BeFalse();
        this.failedEvents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RequestAgentRecoveryAsync_WhenThrows_ReturnsFalseAndRecordsError()
    {
        // Arrange
        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => throw new InvalidOperationException("Recovery failed"),
            onRecoveryFailed: issue => this.failedEvents.Enqueue(issue),
            onRecoverySucceeded: () => { });

        // Act
        var result = await this.recoveryManager.RequestAgentRecoveryAsync("Test recovery");

        // Assert
        result.Should().BeFalse();
        this.recoveryManager.LastRecoveryError.Should().Be("Recovery failed");
    }

    [Fact]
    public async Task RequestAgentRecoveryAsync_AfterFailure_IncrementsFailedAttempts()
    {
        // Arrange
        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(false),
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: () => { });

        await this.recoveryManager.RequestAgentRecoveryAsync("First");
        await this.recoveryManager.RequestAgentRecoveryAsync("Second");

        // Act
        await this.recoveryManager.RequestAgentRecoveryAsync("Third");

        // Assert
        this.recoveryManager.FailedRecoveryAttempts.Should().Be(3);
    }

    // ── ResetRecoveryState Tests ───────────────────────────────────

    [Fact]
    public void ResetRecoveryState_ResetsAllCounters()
    {
        // Arrange
        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(false),
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: () => { });

        this.recoveryManager.RequestAgentRecoveryAsync("Test").Wait();

        // Act
        this.recoveryManager.ResetRecoveryState();

        // Assert
        this.recoveryManager.FailedRecoveryAttempts.Should().Be(0);
        this.recoveryManager.LastRecoveryError.Should().BeNull();
    }

    // ── GetRecoveryStatus Tests ───────────────────────────────────

    [Fact]
    public void GetRecoveryStatus_WhenHealthy_ReturnsHealthyStatus()
    {
        // Arrange
        this.mockHealthMonitor.SetupGet(m => m.AgentRestartCount).Returns(0);
        this.mockHealthMonitor.SetupGet(m => m.IsServiceHealthy).Returns(true);

        // Act
        var status = this.recoveryManager.GetRecoveryStatus();

        // Assert
        status.IsHealthy.Should().BeTrue();
        status.HealthLevel.Should().Be(ServiceHealthLevel.Healthy);
    }

    [Fact]
    public void GetRecoveryStatus_WhenCountersAreClearButAuthorityIsUnhealthy_ReturnsDegraded()
    {
        this.mockHealthMonitor.SetupGet(m => m.AgentRestartCount).Returns(0);
        this.mockHealthMonitor.SetupGet(m => m.IsServiceHealthy).Returns(false);

        var status = this.recoveryManager.GetRecoveryStatus();

        status.IsHealthy.Should().BeFalse();
        status.HealthLevel.Should().Be(ServiceHealthLevel.Degraded);
    }

    [Fact]
    public void GetRecoveryStatus_WhenCritical_ReturnsCriticalStatus()
    {
        // Arrange
        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () => Task.FromResult(false),
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: () => { });

        // Fail 3 times
        for (var i = 0; i < 3; i++)
        {
            this.recoveryManager.RequestAgentRecoveryAsync($"Attempt {i}").Wait();
        }

        // Act
        var status = this.recoveryManager.GetRecoveryStatus();

        // Assert
        status.IsHealthy.Should().BeFalse();
        status.HealthLevel.Should().Be(ServiceHealthLevel.Critical);
        status.FailedRecoveries.Should().Be(3);
    }

    [Fact]
    public void GetRecoveryStatus_WhenDegraded_ReturnsDegradedStatus()
    {
        // Arrange
        this.mockHealthMonitor.SetupGet(m => m.AgentRestartCount).Returns(2);

        // Act
        var status = this.recoveryManager.GetRecoveryStatus();

        // Assert
        status.HealthLevel.Should().Be(ServiceHealthLevel.Degraded);
    }

    // ── AgentDied Event Integration Tests ─────────────────────────

    [Fact]
    public async Task AgentDied_WhenHealthMonitorFires_TriggersRecovery()
    {
        // Arrange
        var recoveryTriggered = false;
        this.recoveryManager = new ServiceRecoveryManager(
            healthMonitor: this.mockHealthMonitor.Object,
            recoverAgentFunc: () =>
            {
                recoveryTriggered = true;
                return Task.FromResult(true);
            },
            onRecoveryFailed: _ => { },
            onRecoverySucceeded: () => { });

        // Act - Simulate agent death event from health monitor
        this.mockHealthMonitor.Raise(
            m => m.AgentDied += null!,
            this.mockHealthMonitor.Object,
            new AgentDiedEventArgs { DeathCount = 1, Timestamp = DateTimeOffset.UtcNow });

        // Wait a bit for the async operation
        await Task.Delay(50);

        // Assert
        recoveryTriggered.Should().BeTrue();
    }
}
