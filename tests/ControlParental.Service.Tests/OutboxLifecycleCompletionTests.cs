namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class OutboxLifecycleCompletionTests : OutboxManagerTestFixture
{
    [Fact]
    public async Task ClaimAsync_StaleCompletionAndFailureCannotMutateReclaimedRow()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "race");
        var first = Assert.Single(await this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1)));
        this.timeProvider.SetWallClockNow(FixedNow.AddMinutes(2));
        await this.manager.RecoverExpiredClaimsAsync();
        var second = Assert.Single(await this.secondManager.ClaimAsync(1, TimeSpan.FromMinutes(1)));

        Assert.False(await this.manager.CompleteAsync(first));
        Assert.False(await this.manager.FailAsync(first, "stale"));
        Assert.True(await this.secondManager.CompleteAsync(second));
    }

    [Fact]
    public async Task FailAsync_ExhaustionRetainsDurableDeadLetterAndAudit()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "exhaustion");
        var first = Assert.Single(await this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1)));
        await this.manager.FailAsync(first, "temporary", FixedNow);
        var second = Assert.Single(await this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1)));
        Assert.True(await this.manager.FailAsync(second, "temporary", maxAttempts: 2));

        var dead = Assert.Single(await this.db.Outbox.Where(row => row.DedupKey == "exhaustion").ToListAsync());
        Assert.Equal(OutboxEntryStatus.DeadLetter, dead.Status);
        Assert.Equal("temporary", dead.SafeFailureCode);
        Assert.NotNull(dead.DeadLetteredAt);
    }

    [Fact]
    public async Task FailAsync_IsPerEntryAndDeadLettersWithRedactedDiagnostics()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "operation-1");
        await this.manager.EnqueueAsync("events", new { Value = 2 }, "operation-2");
        var claims = await this.manager.ClaimAsync(2, TimeSpan.FromMinutes(5));

        Assert.True(await this.manager.FailAsync(
            claims[0], "authorization=secret-token body={\"password\":\"secret\"}", permanent: true));
        Assert.True(await this.manager.FailAsync(claims[1], "temporary-network-error", FixedNow.AddMinutes(10)));

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var rows = await verifyDb.Outbox.OrderBy(row => row.DedupKey).ToListAsync();
        Assert.Equal(OutboxEntryStatus.DeadLetter, rows[0].Status);
        Assert.Equal(OutboxEntryStatus.Pending, rows[1].Status);
        Assert.DoesNotContain("secret-token", rows[0].SafeFailureCode, StringComparison.Ordinal);
        Assert.DoesNotContain("password", rows[0].SafeFailureCode, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Lifecycle_MixedSuccessTransientAndPermanentOutcomesRemainIsolated()
    {
        await this.SeedEntriesAsync(3);
        var claims = await this.manager.ClaimAsync(3, TimeSpan.FromMinutes(5));

        Assert.True(await this.manager.CompleteAsync(claims[0]));
        Assert.True(await this.manager.FailAsync(claims[1], "temporary", FixedNow));
        Assert.True(await this.manager.FailAsync(claims[2], "permanent", permanent: true));

        await using var verifyDb = this.dbContextFactory.CreateDbContext();
        var rows = await verifyDb.Outbox.OrderBy(row => row.Id).ToListAsync();
        Assert.Equal(OutboxEntryStatus.Acknowledged, rows[0].Status);
        Assert.Equal(OutboxEntryStatus.Pending, rows[1].Status);
        Assert.Equal(OutboxEntryStatus.DeadLetter, rows[2].Status);
        var pending = await this.manager.GetPendingEntriesAsync();
        Assert.Single(pending);
        Assert.Equal(rows[1].Id, pending[0].Id);
    }
}
