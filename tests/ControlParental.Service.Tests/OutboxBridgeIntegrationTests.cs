namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Reflection;
using Xunit;

public sealed class OutboxBridgeIntegrationTests : OutboxManagerTestFixture
{
    [Fact]
    public async Task MarkSentAsync_LegacyBridgeDoesNotDeleteDurableEntry()
    {
        var entry = new OutboxDbEntity
        {
            EventType = "test",
            PayloadJson = "{}",
            DedupKey = Guid.NewGuid().ToString(),
            CreatedAt = FixedNow,
        };
        await this.db.Outbox.AddAsync(entry);
        await this.db.SaveChangesAsync();

        await this.manager.MarkSentAsync(entry.Id);

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var persisted = await verifyDb.Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(OutboxEntryStatus.Acknowledged, persisted.Status);
    }

    [Fact]
    public async Task MarkSentAsync_NonExistentIdDoesNotThrow()
    {
        await this.manager.MarkSentAsync(99999);
    }

    [Fact]
    public async Task MarkFailedAsync_LegacyBridgeRedactsAndAdvancesAttempt()
    {
        var entry = new OutboxDbEntity
        {
            EventType = "test",
            PayloadJson = "{}",
            DedupKey = Guid.NewGuid().ToString(),
            CreatedAt = FixedNow,
        };
        await this.db.Outbox.AddAsync(entry);
        await this.db.SaveChangesAsync();

        await this.manager.MarkFailedAsync(entry.Id, "Network error");

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var updated = await verifyDb.Outbox.FindAsync(entry.Id);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.Attempts);
        Assert.Equal(OutboxEntryStatus.Pending, updated.Status);
        Assert.Equal("redacted-failure", updated.SafeFailureCode);
        Assert.DoesNotContain("Network error", updated.LastError, StringComparison.Ordinal);
        Assert.NotNull(updated.LastAttemptAt);
    }

    [Fact]
    public async Task MarkFailedAsync_LegacyBridgeRedactsLongRawError()
    {
        var entry = new OutboxDbEntity
        {
            EventType = "test",
            PayloadJson = "{}",
            DedupKey = Guid.NewGuid().ToString(),
            CreatedAt = FixedNow,
        };
        await this.db.Outbox.AddAsync(entry);
        await this.db.SaveChangesAsync();
        var longError = new string('x', 600);

        await this.manager.MarkFailedAsync(entry.Id, longError);

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var updated = await verifyDb.Outbox.FindAsync(entry.Id);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.Attempts);
        Assert.Equal("redacted-failure", updated.SafeFailureCode);
        Assert.DoesNotContain(longError, updated.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MarkFailedAsync_NonExistentIdDoesNotThrow()
    {
        await this.manager.MarkFailedAsync(99999, "error");
    }

    [Fact]
    public async Task MarkSentAsync_AfterPriorAttemptsAcknowledgesDurableEntry()
    {
        var entry = new OutboxDbEntity
        {
            EventType = "test",
            PayloadJson = "{}",
            DedupKey = Guid.NewGuid().ToString(),
            CreatedAt = FixedNow,
            Attempts = 2,
        };
        await this.db.Outbox.AddAsync(entry);
        await this.db.SaveChangesAsync();

        await this.manager.MarkSentAsync(entry.Id);

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var persisted = await verifyDb.Outbox.AsNoTracking().SingleAsync();
        Assert.Equal(OutboxEntryStatus.Acknowledged, persisted.Status);
    }

    [Theory]
    [InlineData(OutboxEntryStatus.Claimed)]
    [InlineData(OutboxEntryStatus.Acknowledged)]
    [InlineData(OutboxEntryStatus.DeadLetter)]
    public async Task MarkSentAsync_RejectsNonPendingSourceStates(OutboxEntryStatus status)
    {
        var entry = new OutboxDbEntity
        {
            EventType = "events",
            PayloadJson = "{}",
            DedupKey = $"invalid-source-{status}",
            OperationId = $"invalid-source-{status}",
            Status = status,
            ClaimedUntil = status == OutboxEntryStatus.Claimed ? FixedNow.AddMinutes(5) : null,
            CreatedAt = FixedNow,
        };
        await this.db.Outbox.AddAsync(entry);
        await this.db.SaveChangesAsync();

        await this.manager.MarkSentAsync(entry.Id);

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var persisted = await verifyDb.Outbox.AsNoTracking().SingleAsync(row => row.Id == entry.Id);
        Assert.Equal(status, persisted.Status);
    }

    [Fact]
    public async Task RequeueDeadLetterAsync_UnauthorizedRequestDoesNotMutateOrAudit()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "unauthorized-requeue");
        var claim = Assert.Single(await this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1)));
        await this.manager.FailAsync(claim, "permanent", permanent: true);

        Assert.False(await this.manager.RequeueDeadLetterAsync(claim.Id, "wrong-authorization"));

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var row = await verifyDb.Outbox.AsNoTracking().SingleAsync(item => item.Id == claim.Id);
        Assert.Equal(OutboxEntryStatus.DeadLetter, row.Status);
        Assert.Null(row.AuditReference);
    }

    [Fact]
    public async Task RequeueDeadLetterAsync_RepeatedAuthorizedRequestIsIdempotent()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "repeated-requeue");
        var claim = Assert.Single(await this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1)));
        await this.manager.FailAsync(claim, "permanent", permanent: true);

        Assert.True(await this.manager.RequeueDeadLetterAsync(claim.Id, "outbox.requeue"));
        Assert.False(await this.manager.RequeueDeadLetterAsync(claim.Id, "outbox.requeue"));

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var row = await verifyDb.Outbox.AsNoTracking().SingleAsync(item => item.Id == claim.Id);
        Assert.Equal(OutboxEntryStatus.Pending, row.Status);
        Assert.Single(row.AuditReference!.Split(';', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task RealSchedulerManagerPath_PersistsFailureAttemptEligibilityAndRedaction()
    {
        await this.manager.EnqueueAsync(
            "usage_logs",
            new { AppId = "app", Minutes = 1, ServerDate = FixedNow, DedupKey = "scheduler-failure" },
            "scheduler-failure");

        var backend = new Mock<IBackendClient>();
        backend.Setup(client => client.PushUsageLogsAsync(
                It.IsAny<IEnumerable<UsageLogEntry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DataPushResult.Failed("authorization=secret-token body=secret"));
        var reconciler = new Mock<IUsageReconciler>();
        reconciler.SetupGet(value => value.IsRunning).Returns(false);
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(value => value.CurrentLevel).Returns(EnforcementLevel.Standard);
        var health = new Mock<IServiceHealthMonitor>();
        health.SetupGet(value => value.IsAgentHealthy).Returns(true);
        health.SetupGet(value => value.LastAgentHeartbeat).Returns(FixedNow);
        var scheduler = new ScheduledWorkService(
            backend.Object,
            this.manager,
            reconciler.Object,
            monitor.Object,
            this.timeProvider,
            health.Object,
            new Mock<IServiceRecoveryManager>().Object,
            new Mock<IPolicyRepository>().Object,
            identityCoordinator: CreateDefinitiveIdentityCoordinator());
        try
        {
            var method = typeof(ScheduledWorkService).GetMethod(
                "ExecuteOutboxPushAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            await (Task)method.Invoke(scheduler, new object?[] { CancellationToken.None })!;

            await using var verifyDb = this.dbContextFactory.CreateDbContext();
            var row = await verifyDb.Outbox.AsNoTracking().SingleAsync(item => item.DedupKey == "scheduler-failure");
            Assert.Equal(OutboxEntryStatus.Pending, row.Status);
            Assert.Equal(1, row.Attempts);
            Assert.Equal("network", row.SafeFailureCode);
            Assert.DoesNotContain("secret-token", row.LastError, StringComparison.Ordinal);
            Assert.NotNull(row.NextEligibleAt);
        }
        finally
        {
            scheduler.Dispose();
        }
    }

    private static IBackendIdentityCoordinator CreateDefinitiveIdentityCoordinator()
    {
        var identity = new Mock<IBackendIdentityCoordinator>();
        identity.SetupGet(value => value.CurrentState)
            .Returns(BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 1, "device-test"));
        return identity.Object;
    }
}
