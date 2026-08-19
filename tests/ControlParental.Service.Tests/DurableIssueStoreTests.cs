namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

public sealed class DurableIssueStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cp-issues-{Guid.NewGuid():N}");

    [Fact]
    public async Task RepeatedSemanticEvidenceAndResolutionSurviveRestart()
    {
        var path = Path.Combine(this.directory, "issues.json");
        var store = new FileIssueStore(path);
        var key = new IssueKey(4, EnforcementIssueType.AgentNotResponding, "stale-heartbeat");
        var otherSession = key with { SessionId = 7 };
        var first = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

        await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "first", first);
        await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Severe, "second", first.AddMinutes(1));
        await store.UpsertActiveAsync(otherSession, EnforcementIssueSeverity.Warning, "other", first);
        await store.ResolveAsync(key, "matching heartbeat", first.AddMinutes(2));

        var restored = await new FileIssueStore(path).LoadAsync();

        restored.Should().HaveCount(2);
        restored.Single(issue => issue.Key == key).Should().Match<DurableIssue>(issue =>
            !issue.IsActive &&
            issue.OccurrenceCount == 2 &&
            issue.ResolutionEvidence == "matching heartbeat" &&
            issue.Revision == 3);
        restored.Single(issue => issue.Key == otherSession).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentEvidenceForOneSemanticKeyProducesOneBoundedRecord()
    {
        var store = new FileIssueStore(Path.Combine(this.directory, "issues.json"));
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "timezone-change");
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

        await Task.WhenAll(Enumerable.Range(0, 32).Select(index =>
            store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, $"evidence-{index}", now)));

        var restored = await store.LoadAsync();
        restored.Should().ContainSingle();
        restored[0].OccurrenceCount.Should().Be(32);
    }

    [Fact]
    public async Task CorruptDocumentFailsRestoreInsteadOfReturningHealthyEmptyState()
    {
        Directory.CreateDirectory(this.directory);
        var path = Path.Combine(this.directory, "issues.json");
        await File.WriteAllTextAsync(path, "{not-json");

        var act = () => new FileIssueStore(path).LoadAsync();

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task EnforcementMonitorRestoresOnlyActiveSemanticIssues()
    {
        var path = Path.Combine(this.directory, "issues.json");
        var key = new IssueKey(4, EnforcementIssueType.AgentNotResponding, "agent-death");
        var resolved = new IssueKey(4, EnforcementIssueType.ClockTampering, "timezone-change");
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var store = new FileIssueStore(path);
        await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Severe, "agent died", now);
        await store.UpsertActiveAsync(resolved, EnforcementIssueSeverity.Warning, "changed", now);
        await store.ResolveAsync(resolved, "reevaluated", now.AddMinutes(1));

        using var monitor = CreateMonitor(new FileIssueStore(path), now);
        await monitor.StartAsync();

        monitor.CurrentIssues.Should().ContainSingle(issue => issue.Key == key);
        monitor.CurrentIssues.Should().NotContain(issue => issue.Key == resolved);
        monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
    }

    [Fact]
    public async Task AuthoritativeProbeRecoveryPersistsResolutionEvidence()
    {
        var path = Path.Combine(this.directory, "issues.json");
        var store = new FileIssueStore(path);
        var privilege = new Mock<IPrivilegeInspector>();
        privilege.Setup(instance => instance.IsChildStandardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var scm = new Mock<IScmController>();
        scm.Setup(instance => instance.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var health = new Mock<IServiceHealthMonitor>();
        health.SetupGet(instance => instance.IsAgentHealthy).Returns(false);
        health.SetupGet(instance => instance.LastAgentHeartbeat).Returns((DateTimeOffset?)null);
        var time = new Mock<ITimeProvider>();
        time.SetupGet(instance => instance.WallClockNow)
            .Returns(new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero));
        using var monitor = new EnforcementLevelMonitor(
            privilege.Object, scm.Object, health.Object, time.Object, issueStore: store);

        await monitor.EvaluateAsync();
        var active = await store.LoadAsync();
        active.Should().ContainSingle(issue =>
            issue.Key.Type == EnforcementIssueType.AgentNotResponding && issue.IsActive);

        health.SetupGet(instance => instance.IsAgentHealthy).Returns(true);
        await monitor.EvaluateAsync();
        var recovered = await new FileIssueStore(path).LoadAsync();

        recovered.Should().ContainSingle(issue =>
            issue.Key.Type == EnforcementIssueType.AgentNotResponding &&
            !issue.IsActive &&
            issue.ResolutionEvidence == "authoritative health probe recovered");
    }

    public void Dispose()
    {
        if (Directory.Exists(this.directory))
        {
            Directory.Delete(this.directory, recursive: true);
        }
    }

    private static EnforcementLevelMonitor CreateMonitor(IIssueStore store, DateTimeOffset now)
    {
        var privilege = new Mock<IPrivilegeInspector>();
        privilege.Setup(instance => instance.IsChildStandardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var scm = new Mock<IScmController>();
        scm.Setup(instance => instance.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var health = new Mock<IServiceHealthMonitor>();
        health.SetupGet(instance => instance.IsAgentHealthy).Returns(true);
        var time = new Mock<ITimeProvider>();
        time.SetupGet(instance => instance.WallClockNow).Returns(now);
        return new EnforcementLevelMonitor(privilege.Object, scm.Object, health.Object, time.Object, issueStore: store);
    }
}
