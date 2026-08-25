namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

public sealed class EnforcementLevelMonitorSafetyBranchTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SevereSemanticIssueAndResolutionImmediatelyUpdateAuthoritativeHealth()
    {
        var health = CreateHealth(out var sink);
        using var monitor = CreateMonitor(health.Object);
        var key = new IssueKey(4, EnforcementIssueType.AgentNotResponding, "runtime-channel");

        monitor.AddIssue(key, EnforcementIssueSeverity.Severe, "channel lost");
        monitor.ResolveIssue(key);

        sink.Verify(value => value.SetHealthBlockingIssues(true), Times.Once);
        sink.Verify(value => value.SetHealthBlockingIssues(false), Times.Once);
        Assert.Empty(monitor.CurrentIssues);
    }

    [Fact]
    public async Task DurableSemanticIssueAndRecoveryUseStoreAndUpdateAuthoritativeHealth()
    {
        var health = CreateHealth(out var sink);
        var store = new Mock<IIssueStore>();
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "timezone");
        store.Setup(value => value.UpsertActiveAsync(
                key, EnforcementIssueSeverity.Critical, "changed", Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DurableIssue(
                key, EnforcementIssueSeverity.Critical, "changed", Now, Now, 2,
                true, null, null, 2));
        using var monitor = CreateMonitor(health.Object, store.Object);

        await monitor.AddIssueAsync(key, EnforcementIssueSeverity.Critical, "changed");
        await monitor.ResolveIssueAsync(key, "authoritative recovery");

        store.Verify(value => value.ResolveAsync(
            key, "authoritative recovery", Now, It.IsAny<CancellationToken>()), Times.Once);
        sink.Verify(value => value.SetHealthBlockingIssues(true), Times.Once);
        sink.Verify(value => value.SetHealthBlockingIssues(false), Times.Once);
        Assert.Empty(monitor.CurrentIssues);
    }

    [Fact]
    public async Task KeyedFirstApplyProjectsHealthAndEventButReplayDoesNothing()
    {
        var health = CreateHealth(out var sink);
        var store = new Mock<IIssueStore>();
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "keyed-monitor");
        var issue = new DurableIssue(
            key, EnforcementIssueSeverity.Critical, "changed", Now, Now, 1, true, null, null, 1, "key-1");
        var eventCount = 0;
        var calls = 0;
        store.Setup(value => value.UpsertActiveAsync(
                key, EnforcementIssueSeverity.Critical, "changed", It.IsAny<DateTimeOffset>(), "key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new IssueUpsertResult(issue, calls++ > 0));
        using var monitor = CreateMonitor(health.Object, store.Object);
        monitor.IssueDetected += (_, _) => eventCount++;

        await monitor.AddIssueAsync(key, EnforcementIssueSeverity.Critical, "changed", "key-1");
        var firstIssues = monitor.CurrentIssues;
        var firstLevel = monitor.CurrentLevel;
        var firstHealthCalls = sink.Invocations.Count(invocation => invocation.Method.Name == nameof(IAuthoritativeHealthSink.SetHealthBlockingIssues));
        var firstIssue = Assert.Single(firstIssues);
        firstIssue.Key.Should().Be(key);
        firstIssue.Severity.Should().Be(EnforcementIssueSeverity.Critical);
        firstIssue.Description.Should().Be("changed");
        firstIssue.OccurrenceCount.Should().Be(1);
        monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        sink.Verify(value => value.SetHealthBlockingIssues(true), Times.Once);
        eventCount.Should().Be(1);

        await monitor.AddIssueAsync(key, EnforcementIssueSeverity.Critical, "changed", "key-1");

        store.Verify(value => value.UpsertActiveAsync(
            key, EnforcementIssueSeverity.Critical, "changed", It.IsAny<DateTimeOffset>(), "key-1", It.IsAny<CancellationToken>()), Times.Exactly(2));
        monitor.CurrentIssues.Should().BeEquivalentTo(firstIssues);
        monitor.CurrentLevel.Should().Be(firstLevel);
        sink.Invocations.Count(invocation => invocation.Method.Name == nameof(IAuthoritativeHealthSink.SetHealthBlockingIssues))
            .Should().Be(firstHealthCalls);
        eventCount.Should().Be(1);
    }

    [Fact]
    public async Task KeyedDefaultInterfaceAdmissionFailsClosed()
    {
        var monitor = new Mock<IEnforcementLevelMonitor> { CallBase = true };
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "unsupported-keyed");

        var act = () => monitor.Object.AddIssueAsync(
            key, EnforcementIssueSeverity.Warning, "evidence", "key-1");

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("Keyed issue admission is not supported.");
    }

    [Fact]
    public async Task ForegroundHeartbeatSilenceProducesScopedHookTimeout()
    {
        var current = Now;
        var health = CreateHealth(out _);
        var time = new Mock<ITimeProvider>();
        time.SetupGet(value => value.WallClockNow).Returns(() => current);
        using var monitor = CreateMonitor(health.Object, timeProvider: time.Object);
        monitor.RecordForegroundChange();
        current = Now.AddSeconds(121);

        await monitor.EvaluateAsync();

        var issue = Assert.Single(monitor.CurrentIssues,
            value => value.Type == EnforcementIssueType.HookTimeout);
        Assert.Equal(new IssueKey(-1, EnforcementIssueType.HookTimeout, "health/foreground-observation"), issue.Key);
        Assert.Contains("121s", issue.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DurableRestoreFailureStaysDegradedAndBlocksHealth()
    {
        var health = CreateHealth(out var sink);
        var store = new Mock<IIssueStore>();
        store.Setup(value => value.LoadAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("corrupt runtime state"));
        using var monitor = CreateMonitor(health.Object, store.Object);

        await monitor.StartAsync();

        Assert.Equal(EnforcementLevel.Degraded, monitor.CurrentLevel);
        var issue = Assert.Single(monitor.CurrentIssues,
            value => value.Type == EnforcementIssueType.RestoreFailure);
        Assert.Contains("corrupt runtime state", issue.Description, StringComparison.Ordinal);
        sink.Verify(value => value.SetRestoreStatus(false), Times.Once);
        sink.Verify(value => value.SetHealthBlockingIssues(true), Times.AtLeastOnce);
    }

    private static EnforcementLevelMonitor CreateMonitor(
        IServiceHealthMonitor health,
        IIssueStore? store = null,
        ITimeProvider? timeProvider = null)
    {
        var privilege = new Mock<IPrivilegeInspector>();
        privilege.Setup(value => value.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var scm = new Mock<IScmController>();
        scm.Setup(value => value.IsServiceRunningAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        timeProvider ??= Mock.Of<ITimeProvider>(value => value.WallClockNow == Now);
        return new EnforcementLevelMonitor(
            privilege.Object, scm.Object, health, timeProvider, issueStore: store);
    }

    private static Mock<IServiceHealthMonitor> CreateHealth(
        out Mock<IAuthoritativeHealthSink> sink)
    {
        var health = new Mock<IServiceHealthMonitor>();
        health.SetupGet(value => value.IsAgentHealthy).Returns(true);
        health.SetupGet(value => value.LastAgentHeartbeat).Returns(Now);
        sink = health.As<IAuthoritativeHealthSink>();
        return health;
    }
}

public sealed class ExactTerminationFailureBranchTests
{
    [Theory]
    [InlineData("access", ActionStatus.AccessDenied)]
    [InlineData("missing", ActionStatus.HarmlessAbsence)]
    [InlineData("native", ActionStatus.NativeFailure)]
    public async Task NativeOpenFailureMapsToExactTypedOutcome(string failure, ActionStatus expected)
    {
        using var handle = new ThrowingProcessHandle(failure);
        var target = new ObservedProcessTarget(42, 4, DateTimeOffset.UtcNow);
        var terminator = new ProcessTerminator(handle);

        var status = await terminator.TerminateAsync(target);

        Assert.Equal(expected, status);
        Assert.False(handle.KillCalled);
    }

    private sealed class ThrowingProcessHandle(string failure) : IExactProcessHandle
    {
        public bool KillCalled { get; private set; }
        public bool HasExited => false;
        public ProcessIdentity ReadIdentity() => failure switch
        {
            "access" => throw new UnauthorizedAccessException(),
            "missing" => throw new InvalidOperationException(),
            _ => throw new IOException("native failure"),
        };
        public bool CloseMainWindow() => false;
        public void Kill() => this.KillCalled = true;
        public Task WaitForExitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Dispose() { }
    }
}
