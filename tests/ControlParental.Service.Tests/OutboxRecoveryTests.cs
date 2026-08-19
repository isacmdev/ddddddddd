namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Diagnostics;
using Xunit;

public sealed class OutboxRecoveryTests : OutboxManagerTestFixture
{
    [Fact]
    public async Task CrashBeforeAcknowledgement_ReplaysSameOperationIdentity()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "crash-window");
        var first = Assert.Single(await this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1)));
        this.timeProvider.SetWallClockNow(FixedNow.AddMinutes(2));
        await this.secondManager.RecoverExpiredClaimsAsync();
        var replay = Assert.Single(await this.secondManager.ClaimAsync(1, TimeSpan.FromMinutes(1)));

        Assert.Equal(first.OperationId, replay.OperationId);
        Assert.True(await this.secondManager.CompleteAsync(
            new OutboxClaim(replay.Id, replay.OperationId, replay.ClaimVersion, replay.ClaimedUntil!.Value)));
        Assert.False(await this.manager.CompleteAsync(first));
    }

    [Fact]
    public async Task RecoverExpiredClaims_RollsBackAfterBeginAndCanRetry()
    {
        var databaseName = $"rollback-{Guid.NewGuid():N}";
        await using var connection = new SqliteConnection($"Data Source=file:{databaseName}?mode=memory&cache=shared;Default Timeout=1");
        await connection.OpenAsync();
        var interceptor = new ThrowAfterBeginInterceptor();
        var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        await using (var database = new ControlParentalDbContext(options))
        {
            await database.Database.EnsureCreatedAsync();
            await database.Outbox.AddAsync(new OutboxDbEntity
            {
                EventType = "events",
                PayloadJson = "{}",
                DedupKey = "rollback",
                OperationId = "rollback",
                Status = OutboxEntryStatus.Claimed,
                ClaimVersion = 1,
                ClaimedUntil = FixedNow.AddMinutes(-1),
                CreatedAt = FixedNow,
            });
            await database.SaveChangesAsync();
        }

        var manager = new OutboxManager(new TrackingDbContextFactory(options), this.timeProvider);
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.RecoverExpiredClaimsAsync());
        await using (var unchanged = new ControlParentalDbContext(options))
        {
            Assert.Equal(OutboxEntryStatus.Claimed, (await unchanged.Outbox.SingleAsync()).Status);
        }

        interceptor.ThrowOnMutation = false;
        Assert.Equal(1, await manager.RecoverExpiredClaimsAsync());
        await using var recovered = new ControlParentalDbContext(options);
        Assert.Equal(OutboxEntryStatus.Pending, (await recovered.Outbox.SingleAsync()).Status);
    }

    [Fact]
    public async Task ClaimAsync_CancellationDoesNotMutateRows()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "cancelled");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1), cancellation.Token));

        var row = Assert.Single(await this.db.Outbox.Where(item => item.DedupKey == "cancelled").ToListAsync());
        Assert.Equal(OutboxEntryStatus.Pending, row.Status);
        Assert.Equal(0, row.Attempts);
    }

    [Fact]
    public async Task CompletionAndFailureCancellationDoesNotMutateClaim()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "cancel-transition");
        var claim = Assert.Single(await this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => this.manager.CompleteAsync(claim, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => this.manager.FailAsync(claim, "temporary", cancellationToken: cancellation.Token));

        var row = Assert.Single(await this.db.Outbox.Where(item => item.Id == claim.Id).ToListAsync());
        Assert.Equal(OutboxEntryStatus.Claimed, row.Status);
        Assert.Equal(claim.ClaimVersion, row.ClaimVersion);
    }

    [Fact]
    public async Task RecoverExpiredClaims_IsBoundedBeforeUpdatingRows()
    {
        for (var i = 0; i < 1005; i++)
        {
            await this.db.Outbox.AddAsync(new OutboxDbEntity
            {
                EventType = "events",
                PayloadJson = "{}",
                DedupKey = $"expired-{i:D4}",
                OperationId = $"expired-{i:D4}",
                Status = OutboxEntryStatus.Claimed,
                ClaimVersion = 1,
                ClaimedUntil = FixedNow.AddMinutes(-i - 1),
                CreatedAt = FixedNow.AddMinutes(-i),
            });
        }
        await this.db.SaveChangesAsync();

        Assert.Equal(1000, await this.manager.RecoverExpiredClaimsAsync());
        Assert.Equal(5, await this.db.Outbox.CountAsync(row => row.Status == OutboxEntryStatus.Claimed));
    }

    [Fact]
    public async Task ClaimAsync_WhenDatabaseIsLocked_FailsWithinBoundAndDoesNotMutate()
    {
        await this.manager.EnqueueAsync("events", new { Value = 1 }, "locked");
        await using var lockCommand = this.secondConnection.CreateCommand();
        lockCommand.CommandText = "BEGIN IMMEDIATE";
        await lockCommand.ExecuteNonQueryAsync();
        try
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var started = Stopwatch.StartNew();
            await Assert.ThrowsAnyAsync<Exception>(() => this.manager.ClaimAsync(1, TimeSpan.FromMinutes(1), cancellation.Token));
            started.Stop();

            Assert.True(started.Elapsed < TimeSpan.FromSeconds(3));
            var row = await this.db.Outbox.SingleAsync(item => item.DedupKey == "locked");
            Assert.Equal(OutboxEntryStatus.Pending, row.Status);
            Assert.Equal(0, row.Attempts);
        }
        finally
        {
            await using var rollback = this.secondConnection.CreateCommand();
            rollback.CommandText = "ROLLBACK";
            await rollback.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task FileBackedRestart_ReclaimsLeaseAndPreservesOperationId()
    {
        var path = Path.Combine(Path.GetTempPath(), $"outbox-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path};Default Timeout=1";
        try
        {
            OutboxEntry original;
            using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                var options = new DbContextOptionsBuilder<ControlParentalDbContext>().UseSqlite(connection).Options;
                await using var database = new ControlParentalDbContext(options);
                await database.Database.EnsureCreatedAsync();
                var manager = new OutboxManager(new TrackingDbContextFactory(options), this.timeProvider);
                await manager.EnqueueAsync("events", new { Value = 1 }, "restart");
                original = Assert.Single(await manager.ClaimAsync(1, TimeSpan.FromMinutes(1)));
            }

            this.timeProvider.SetWallClockNow(FixedNow.AddMinutes(2));
            using (var reopened = new SqliteConnection(connectionString))
            {
                await reopened.OpenAsync();
                var reopenedOptions = new DbContextOptionsBuilder<ControlParentalDbContext>().UseSqlite(reopened).Options;
                var reopenedManager = new OutboxManager(new TrackingDbContextFactory(reopenedOptions), this.timeProvider);
                Assert.Equal(1, await reopenedManager.RecoverExpiredClaimsAsync());
                var replay = Assert.Single(await reopenedManager.ClaimAsync(1, TimeSpan.FromMinutes(1)));

                Assert.Equal(original.OperationId, replay.OperationId);
                Assert.True(await reopenedManager.CompleteAsync(replay));
                Assert.False(await reopenedManager.CompleteAsync(original));
            }
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // SQLite may release the final file handle just after disposal.
            }
        }
    }
}

internal sealed class ThrowAfterBeginInterceptor : DbCommandInterceptor
{
    private bool began;

    public bool ThrowOnMutation { get; set; } = true;

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        this.ThrowIfMutation(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        this.ThrowIfMutation(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void ThrowIfMutation(DbCommand command)
    {
        if (command.CommandText.Contains("BEGIN IMMEDIATE", StringComparison.OrdinalIgnoreCase))
        {
            this.began = true;
        }
        else if (this.began && this.ThrowOnMutation && command.CommandText.Contains("UPDATE outbox", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("forced-after-begin-failure");
        }
    }
}
