// <copyright file="ScheduledWorkServiceBackoffDecrementTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using ControlParental.Domain;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T20/A2 — clock-based eligibility coverage for <see cref="ScheduledWorkService"/>.
/// </summary>
public class ScheduledWorkServiceBackoffDecrementTests : IDisposable
{
    private readonly MutableTimeProvider timeProvider;
    private readonly Mock<IBackendClient> mockBackendClient;
    private readonly Mock<IOutboxManager> mockOutboxManager;
    private readonly Mock<IUsageReconciler> mockUsageReconciler;
    private readonly Mock<IEnforcementLevelMonitor> mockEnforcementLevelMonitor;
    private readonly Mock<IServiceHealthMonitor> mockHealthMonitor;
    private readonly Mock<IServiceRecoveryManager> mockRecoveryManager;
    private readonly Mock<IPolicyRepository> mockPolicyRepository;
    private readonly ScheduledWorkService service;

    public ScheduledWorkServiceBackoffDecrementTests()
    {
        this.timeProvider = new MutableTimeProvider(new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero));
        this.mockBackendClient = new Mock<IBackendClient>();
        this.mockOutboxManager = new Mock<IOutboxManager>();
        this.mockUsageReconciler = new Mock<IUsageReconciler>();
        this.mockEnforcementLevelMonitor = new Mock<IEnforcementLevelMonitor>();
        this.mockHealthMonitor = new Mock<IServiceHealthMonitor>();
        this.mockRecoveryManager = new Mock<IServiceRecoveryManager>();
        this.mockPolicyRepository = new Mock<IPolicyRepository>();

        this.mockUsageReconciler.SetupGet(r => r.IsRunning).Returns(false);
        this.mockEnforcementLevelMonitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Standard);
        this.mockHealthMonitor.SetupGet(m => m.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(m => m.LastAgentHeartbeat).Returns(this.timeProvider.Now);

        this.service = new ScheduledWorkService(
            backendClient: this.mockBackendClient.Object,
            outboxManager: this.mockOutboxManager.Object,
            usageReconciler: this.mockUsageReconciler.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            timeProvider: this.timeProvider,
            healthMonitor: this.mockHealthMonitor.Object,
            recoveryManager: this.mockRecoveryManager.Object,
            policyRepository: this.mockPolicyRepository.Object);

        this.SetPrivateField("isRunning", true);
    }

    public void Dispose()
    {
        this.service.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ShouldRun_WhenStarted_AndNoDeadline_ReturnsTrue()
    {
        this.GetNextEligibleAt(ScheduledWorkService.WorkType.Heartbeat).Should().Be(DateTimeOffset.MinValue);
        this.InvokeShouldRun(ScheduledWorkService.WorkType.Heartbeat).Should().BeTrue();
    }

    [Fact]
    public void ApplyBackoff_SetsDeadline_AndBlocksUntilClockPasses()
    {
        this.InvokePrivateVoid("ApplyBackoff", ScheduledWorkService.WorkType.Heartbeat);

        this.GetBackoff(ScheduledWorkService.WorkType.Heartbeat).Should().Be(2);
        this.GetNextEligibleAt(ScheduledWorkService.WorkType.Heartbeat)
            .Should().Be(this.timeProvider.Now.AddSeconds(ScheduledWorkService.InitialBackoffSeconds));
        this.InvokeShouldRun(ScheduledWorkService.WorkType.Heartbeat).Should().BeFalse();

        this.timeProvider.Now = this.timeProvider.Now.AddMilliseconds(999);
        this.InvokeShouldRun(ScheduledWorkService.WorkType.Heartbeat).Should().BeFalse();

        this.timeProvider.Now = this.timeProvider.Now.AddMilliseconds(1);
        this.InvokeShouldRun(ScheduledWorkService.WorkType.Heartbeat).Should().BeTrue();
    }

    [Fact]
    public void ResetBackoff_ClearsDeadline_AfterSuccess()
    {
        this.InvokePrivateVoid("ApplyBackoff", ScheduledWorkService.WorkType.Heartbeat);
        this.InvokePrivateVoid("ResetBackoff", ScheduledWorkService.WorkType.Heartbeat);

        this.GetBackoff(ScheduledWorkService.WorkType.Heartbeat).Should().Be(ScheduledWorkService.InitialBackoffSeconds);
        this.GetNextEligibleAt(ScheduledWorkService.WorkType.Heartbeat).Should().Be(DateTimeOffset.MinValue);
        this.InvokeShouldRun(ScheduledWorkService.WorkType.Heartbeat).Should().BeTrue();
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

    private int GetBackoff(ScheduledWorkService.WorkType workType)
    {
        var method = typeof(ScheduledWorkService).GetMethod(
            "GetBackoffForTesting",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        return (int)method!.Invoke(this.service, new object[] { workType })!;
    }

    private DateTimeOffset GetNextEligibleAt(ScheduledWorkService.WorkType workType)
    {
        var method = typeof(ScheduledWorkService).GetMethod(
            "GetNextEligibleAtForTesting",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(method);

        return (DateTimeOffset)method!.Invoke(this.service, new object[] { workType })!;
    }

    private void SetPrivateField(string fieldName, bool value)
    {
        var field = typeof(ScheduledWorkService).GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(field);
        field!.SetValue(this.service, value);
    }

    private sealed class MutableTimeProvider : ITimeProvider
    {
        public MutableTimeProvider(DateTimeOffset now)
        {
            this.Now = now;
        }

        public DateTimeOffset Now { get; set; }

        public long MonotonicNow => 0;

        public DateTimeOffset WallClockNow => this.Now;

        public TimeZoneInfo CurrentZone => TimeZoneInfo.Utc;

        public DateOnly? ServerDate => null;

        public bool IsServerDateUncertain => false;

        public event EventHandler<TimeChangedEventArgs>? TimeChanged;

        public void SetServerDate(long offsetMs)
        {
        }

        public bool DetectClockJump() => false;
    }
}
