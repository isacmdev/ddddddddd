namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using System.Text.Json;
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
    public async Task EmptyIdempotencyKeyPreservesLegacyOccurrenceBehavior()
    {
        var store = new FileIssueStore(Path.Combine(this.directory, "issues.json"));
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "empty-key");

        var first = await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "one", DateTimeOffset.UtcNow, "key-1");
        var second = await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "two", DateTimeOffset.UtcNow, "");

        first.IsReplay.Should().BeFalse();
        second.IsReplay.Should().BeFalse();
        second.Issue.OccurrenceCount.Should().Be(2);
        second.Issue.LastIdempotencyKey.Should().BeNull();
    }

    [Fact]
    public async Task FirstKeyedApplyPersistsKeyAndExactReplayIgnoresObservedAt()
    {
        var store = new FileIssueStore(Path.Combine(this.directory, "issues.json"));
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "keyed");
        var observedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var idempotencyKey = new string('k', 256);

        var first = await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", observedAt, idempotencyKey);
        var replay = await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", observedAt.AddHours(1), idempotencyKey);

        first.IsReplay.Should().BeFalse();
        first.Issue.LastIdempotencyKey.Should().Be(idempotencyKey);
        replay.IsReplay.Should().BeTrue();
        replay.Issue.Should().Be(first.Issue);
    }

    [Fact]
    public async Task OversizedPersistedIssueDocumentFailsBeforeLoadingRecords()
    {
        Directory.CreateDirectory(this.directory);
        var path = Path.Combine(this.directory, "issues.json");
        var issues = Enumerable.Range(0, 1025).Select(index => new DurableIssue(
            new IssueKey(index, EnforcementIssueType.ClockTampering, $"cause-{index}"),
            EnforcementIssueSeverity.Warning,
            "evidence",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            true,
            null,
            null,
            1)).ToArray();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { Version = 1, Issues = issues }));

        var act = () => new FileIssueStore(path).LoadAsync();

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task SameKeyWithDifferentScopeSeverityOrEvidenceConflictsWithoutMutation()
    {
        var path = Path.Combine(this.directory, "issues.json");
        var store = new FileIssueStore(path);
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "conflict", "scope-a");
        var applied = await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", DateTimeOffset.UtcNow, "key-1");
        var before = await File.ReadAllTextAsync(path);

        var scopeConflict = () => store.UpsertActiveAsync(key with { IdentityScope = "scope-b" }, EnforcementIssueSeverity.Warning, "evidence", DateTimeOffset.UtcNow, "key-1");
        var severityConflict = () => store.UpsertActiveAsync(key, EnforcementIssueSeverity.Severe, "evidence", DateTimeOffset.UtcNow, "key-1");
        var evidenceConflict = () => store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "other", DateTimeOffset.UtcNow, "key-1");

        await scopeConflict.Should().ThrowAsync<InvalidOperationException>();
        await severityConflict.Should().ThrowAsync<InvalidOperationException>();
        await evidenceConflict.Should().ThrowAsync<InvalidOperationException>();
        (await File.ReadAllTextAsync(path)).Should().Be(before);
        applied.Issue.OccurrenceCount.Should().Be(1);
    }

    [Fact]
    public async Task KeyedReplaySurvivesRestartAndLegacyJsonLoadsMissingKeyAsNull()
    {
        var path = Path.Combine(this.directory, "issues.json");
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "restart");
        var observedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var store = new FileIssueStore(path);
        await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", observedAt, "key-1");

        var replay = await new FileIssueStore(path).UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", observedAt.AddDays(1), "key-1");
        replay.IsReplay.Should().BeTrue();

        await File.WriteAllTextAsync(path, "{\"Version\":1,\"Issues\":[{\"Key\":{\"SessionId\":4,\"Type\":1,\"Cause\":\"legacy\"},\"Severity\":1,\"LastEvidence\":\"old\",\"FirstObservedAt\":\"2026-08-10T12:00:00+00:00\",\"LastObservedAt\":\"2026-08-10T12:00:00+00:00\",\"OccurrenceCount\":1,\"IsActive\":true,\"Revision\":1}]}");
        var legacy = await new FileIssueStore(path).LoadAsync();

        legacy.Single().LastIdempotencyKey.Should().BeNull();
    }

    [Fact]
    public async Task ResolvedExactReplayReturnsResolvedRecordUnchanged()
    {
        var store = new FileIssueStore(Path.Combine(this.directory, "issues.json"));
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "resolved");
        var applied = await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", DateTimeOffset.UtcNow, "key-1");
        await store.ResolveAsync(key, "recovered", DateTimeOffset.UtcNow.AddMinutes(1));
        var resolved = (await store.LoadAsync()).Single();

        var replay = await store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", DateTimeOffset.UtcNow.AddHours(1), "key-1");

        replay.IsReplay.Should().BeTrue();
        replay.Issue.Should().Be(resolved);
        replay.Issue.Should().NotBe(applied.Issue);
    }

    [Fact]
    public async Task ConcurrentSameKeyCallsApplyOnceAndReplayTheRest()
    {
        var store = new FileIssueStore(Path.Combine(this.directory, "issues.json"));
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "concurrent-key");

        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(index =>
            store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "same", DateTimeOffset.UtcNow.AddSeconds(index), "key-1")));

        results.Count(result => !result.IsReplay).Should().Be(1);
        results.Count(result => result.IsReplay).Should().Be(15);
        var issue = (await store.LoadAsync()).Single();
        issue.OccurrenceCount.Should().Be(1);
        issue.Revision.Should().Be(1);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\tkey")]
    public async Task InvalidWhitespaceKeyIsRejectedBeforeMutation(string invalidKey)
    {
        var path = Path.Combine(this.directory, "issues.json");
        var store = new FileIssueStore(path);
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "invalid");

        var act = () => store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", DateTimeOffset.UtcNow, invalidKey);

        await act.Should().ThrowAsync<ArgumentException>();
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task OversizedKeyIsRejectedBeforeMutation()
    {
        var path = Path.Combine(this.directory, "issues.json");
        var store = new FileIssueStore(path);
        var key = new IssueKey(4, EnforcementIssueType.ClockTampering, "oversized");

        var act = () => store.UpsertActiveAsync(key, EnforcementIssueSeverity.Warning, "evidence", DateTimeOffset.UtcNow, new string('x', 257));

        await act.Should().ThrowAsync<ArgumentException>();
        File.Exists(path).Should().BeFalse();
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
