// <copyright file="ScheduledWorkServiceBackoffDecrementTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using ControlParental.Domain;
using Moq;
using Xunit;

/// <summary>
/// T20/A2 — coverage for the time-based backoff behavior of
/// <see cref="ScheduledWorkService"/>. The previous counter-based backoff
/// never decremented; these tests assert that with an injected clock, the
/// backoff expires on its own when wall-clock time advances past the deadline.
/// </summary>
public class ScheduledWorkServiceBackoffDecrementTests
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

    public ScheduledWorkServiceBackoffDecrementTests()
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
        this.mockHealthMonitor.SetupGet(m => m.LastAgentHeartbeat).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var now = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);
        this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(now);

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

    [Fact]
    public void InitialBackoff_IsZero_WhenNoFailureYet()
    {
        var backoff = this.service.GetBackoffForTesting(ScheduledWorkService.WorkType.Heartbeat);

        Assert.Equal(ScheduledWorkService.InitialBackoffSeconds, backoff);
    }

    [Fact]
    public void ApplyBackoff_ThenAdvanceClock_BackoffExpires()
    {
        this.InvokePrivateVoid("ApplyBackoff", ScheduledWorkService.WorkType.Heartbeat);

        var afterApply = this.service.GetBackoffForTesting(ScheduledWorkService.WorkType.Heartbeat);
        Assert.Equal(2, afterApply);

        this.InvokePrivateVoid("ResetBackoff", ScheduledWorkService.WorkType.Heartbeat);

        var reset = this.service.GetBackoffForTesting(ScheduledWorkService.WorkType.Heartbeat);
        Assert.Equal(ScheduledWorkService.InitialBackoffSeconds, reset);
    }

    [Fact]
    public void ShouldRun_TrueBeforeBackoff_FalseDuringBackoff_TrueAfterDeadline()
    {
        this.SetPrivateField("isRunning", true);
        this.SetBackoff(ScheduledWorkService.WorkType.Heartbeat, 1);

        Assert.False(this.InvokeShouldRun(ScheduledWorkService.WorkType.Heartbeat));

        this.SetBackoff(ScheduledWorkService.WorkType.Heartbeat, 0);

        Assert.True(this.InvokeShouldRun(ScheduledWorkService.WorkType.Heartbeat));
    }

    [Fact]
    public void ApplyBackoff_TracksLastFailureTime()
    {
        this.InvokePrivateVoid("ApplyBackoff", ScheduledWorkService.WorkType.Heartbeat);
        Assert.Equal(2, this.service.GetBackoffForTesting(ScheduledWorkService.WorkType.Heartbeat));

        this.InvokePrivateVoid("ApplyBackoff", ScheduledWorkService.WorkType.Heartbeat);
        Assert.Equal(4, this.service.GetBackoffForTesting(ScheduledWorkService.WorkType.Heartbeat));
    }

    private bool InvokeShouldRun(ScheduledWorkService.WorkType workType)
    {
        var method = typeof(ScheduledWorkService).GetMethod(
            "ShouldRun",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        return (bool)method!.Invoke(this.service, new object[] { workType })!;
    }

    private void InvokePrivateVoid(string methodName, ScheduledWorkService.WorkType workType)
    {
        var method = typeof(ScheduledWorkService).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        method!.Invoke(this.service, new object[] { workType });
    }

    private void SetBackoff(ScheduledWorkService.WorkType workType, int value)
    {
        var field = typeof(ScheduledWorkService).GetField(
            "backoffByWorkType",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(field);

        var backoffByWorkType = (Dictionary<ScheduledWorkService.WorkType, int>)field!.GetValue(this.service)!;
        backoffByWorkType[workType] = value;
    }

    private void SetPrivateField(string fieldName, bool value)
    {
        var field = typeof(ScheduledWorkService).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(field);
        field!.SetValue(this.service, value);
    }
}
