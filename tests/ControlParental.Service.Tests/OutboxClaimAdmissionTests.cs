namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class OutboxClaimAdmissionTests : OutboxManagerTestFixture
{
    [Fact]
    public async Task GetPendingEntriesAsync_FiltersEligibilityBeforeLimit()
    {
        await this.db.Outbox.AddRangeAsync(
            new OutboxDbEntity
            {
                EventType = "blocked",
                PayloadJson = "{}",
                DedupKey = "blocked",
                OperationId = "blocked",
                CreatedAt = FixedNow.AddMinutes(-2),
                NextEligibleAt = FixedNow.AddMinutes(10),
            },
            new OutboxDbEntity
            {
                EventType = "eligible",
                PayloadJson = "{}",
                DedupKey = "eligible",
                OperationId = "eligible",
                CreatedAt = FixedNow.AddMinutes(-1),
            });
        await this.db.SaveChangesAsync();

        var entries = await this.manager.GetPendingEntriesAsync(1);

        var entry = Assert.Single(entries);
        Assert.Equal("eligible", entry.DedupKey);
    }

    [Fact]
    public async Task ClaimAsync_ConcurrentClaimersReceiveDisjointRows()
    {
        await this.SeedEntriesAsync(2);

        var results = await Task.WhenAll(
            this.manager.ClaimAsync(2, TimeSpan.FromMinutes(5)),
            this.secondManager.ClaimAsync(2, TimeSpan.FromMinutes(5)));

        var claimedIds = results.SelectMany(rows => rows).Select(row => row.Id).ToArray();
        Assert.Equal(2, claimedIds.Distinct().Count());
        Assert.Contains(results, rows => rows.Count == 2);
        Assert.Contains(results, rows => rows.Count == 0);
    }

    [Fact]
    public async Task ClaimAsync_InvalidBoundsReturnsEmptyWithoutMutation()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "invalid-bounds");

        Assert.Empty(await this.manager.ClaimAsync(0, TimeSpan.FromMinutes(1)));
        Assert.Empty(await this.manager.ClaimAsync(1, TimeSpan.Zero));

        var row = Assert.Single(await this.db.Outbox.Where(item => item.DedupKey == "invalid-bounds").ToListAsync());
        Assert.Equal(OutboxEntryStatus.Pending, row.Status);
        Assert.Equal(0, row.Attempts);
    }

    [Fact]
    public async Task ClaimAsync_IsBoundedAndClaimsOnlyEligibleEntries()
    {
        await this.SeedEntriesAsync(3);

        var claims = await this.manager.ClaimAsync(2, TimeSpan.FromMinutes(5));

        Assert.Equal(2, claims.Count);
        Assert.All(claims, claim =>
        {
            Assert.Equal(OutboxEntryStatus.Claimed, claim.Status);
            Assert.NotEqual(string.Empty, claim.OperationId);
            Assert.True(claim.ClaimVersion > 0);
            Assert.Equal(FixedNow.AddMinutes(5), claim.ClaimedUntil);
        });
        Assert.Equal(1, (await this.manager.GetPendingEntriesAsync()).Count);
    }

    [Fact]
    public void Constructor_RejectsMissingDurabilityDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new OutboxManager(null!, this.timeProvider));
        Assert.Throws<ArgumentNullException>(() => new OutboxManager(this.dbContextFactory, null!));
    }
}
