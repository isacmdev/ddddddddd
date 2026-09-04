// <copyright file="PolicyRepositoryTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using Microsoft.Data.Sqlite;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Service.CompiledModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

/// <summary>
/// T03 — Tests for PolicyRepository using in-memory SQLite.
/// </summary>
public class PolicyRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2032, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly ControlParentalDbContext db;
    private readonly SqliteConnection connection;
    private readonly FakeTimeProvider timeProvider;
    private readonly TrackingDbContextFactory dbContextFactory;
    private readonly PolicyRepository repository;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower) },
    };

    public PolicyRepositoryTests()
    {
        this.connection = new SqliteConnection("Data Source=:memory:");
        this.connection.Open();

        var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(this.connection)
            .Options;

        this.db = new ControlParentalDbContext(options);
        this.db.Database.EnsureCreated();
        this.timeProvider = new FakeTimeProvider(FixedNow);
        this.dbContextFactory = new TrackingDbContextFactory(options);
        this.repository = new PolicyRepository(this.dbContextFactory, this.timeProvider);
    }

    public void Dispose()
    {
        this.db.Dispose();
        this.connection.Dispose();
    }

    // ── Version guard ──────────────────────────────────────────────────

    [Fact]
    public async Task UpsertPolicy_NewPolicy_ShouldApply()
    {
        // Arrange
        var policy = MakePolicy(deviceId: "dev-1", version: 5);

        // Act
        var applied = await this.repository.UpsertPolicyAsync(policy);

        // Assert
        Assert.True(applied);
        var stored = await this.repository.GetPolicyAsync();
        Assert.NotNull(stored);
        Assert.Equal("dev-1", stored.DeviceId);
        Assert.Equal(5, stored.Version);

        var storedEntity = await this.db.Policies.SingleAsync();
        Assert.Equal(FixedNow, storedEntity.LastUpdated);
    }

    [Fact]
    public async Task UpsertPolicy_DowngradeVersion_ShouldDiscard()
    {
        // Arrange — first: version 10
        var policyV10 = MakePolicy(deviceId: "dev-1", version: 10);
        await this.repository.UpsertPolicyAsync(policyV10);

        // Act — try to apply version 5 (downgrade)
        var policyV5 = MakePolicy(deviceId: "dev-1", version: 5);
        var applied = await this.repository.UpsertPolicyAsync(policyV5);

        // Assert
        Assert.False(applied);
        var stored = await this.repository.GetPolicyAsync();
        Assert.NotNull(stored);
        Assert.Equal(10, stored.Version); // Still 10
    }

    [Fact]
    public async Task UpsertPolicy_SameVersion_ShouldDiscard()
    {
        // Arrange
        var policy = MakePolicy(deviceId: "dev-1", version: 5);
        await this.repository.UpsertPolicyAsync(policy);

        // Act — re-apply same version
        var applied = await this.repository.UpsertPolicyAsync(policy);

        // Assert
        Assert.False(applied);
    }

    [Fact]
    public async Task UpsertPolicy_SameVersionDifferentHash_QuarantinesWithoutApplying()
    {
        await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-a"));

        var applied = await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-b"));

        Assert.False(applied);
        var stored = await this.db.Policies.SingleAsync();
        Assert.Equal("hash-a", stored.SnapshotHash);
        Assert.True(stored.IsQuarantined);
        Assert.Equal("same_version_snapshot_hash_mismatch", stored.QuarantineReason);
    }

    [Fact(DisplayName = "CT-09 productive policy seam retains the current version and quarantines a conflicting hash")]
    [Trait("ContractTest", "CT-09")]
    public async Task CT09_ProductivePolicySeamRetainsVersionAndQuarantinesConflict()
    {
        Assert.True(await this.repository.UpsertPolicyAsync(MakePolicy(version: 9, snapshotHash: "hash-a")));
        Assert.False(await this.repository.UpsertPolicyAsync(MakePolicy(version: 8, snapshotHash: "hash-b")));
        var retained = await this.repository.GetPolicyAsync();
        Assert.NotNull(retained);
        Assert.Equal(9, retained!.Version);
        Assert.Equal("hash-a", retained.SnapshotHash);

        Assert.False(await this.repository.UpsertPolicyAsync(MakePolicy(version: 9, snapshotHash: "hash-b")));
        Assert.Null(await this.repository.GetPolicyAsync());
        Assert.True(this.repository.IsPolicyQuarantined);
        Assert.Equal("same_version_snapshot_hash_mismatch", this.repository.PolicyQuarantineReason);

        var persisted = await this.db.Policies.SingleAsync();
        Assert.Equal(9, persisted.Version);
        Assert.Equal("hash-a", persisted.SnapshotHash);
        Assert.True(persisted.IsQuarantined);
    }

    [Fact]
    public async Task UpsertPolicy_SameVersionSameHash_IsIdempotent()
    {
        await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-a"));

        Assert.False(await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-a")));
        Assert.False((await this.db.Policies.SingleAsync()).IsQuarantined);
    }

    [Fact]
    public async Task UpsertPolicy_SameVersionDifferentHash_HidesQuarantinedPolicyWithoutManualInvalidation()
    {
        await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-a"));
        await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-b"));

        Assert.Null(await this.repository.GetPolicyAsync());
    }

    [Fact]
    public async Task GetPolicyAsync_AfterRepositoryRestart_PreservesQuarantineAndHidesPolicy()
    {
        await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-a"));
        await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-b"));

        using var restartedRepository = new PolicyRepository(this.dbContextFactory, this.timeProvider);

        Assert.Null(await restartedRepository.GetPolicyAsync());
        Assert.True(restartedRepository.IsPolicyQuarantined);
        Assert.Equal("same_version_snapshot_hash_mismatch", restartedRepository.PolicyQuarantineReason);
    }

    [Fact]
    public async Task GetPolicyAsync_AfterFreshContextRestart_WithCompiledModel_PreservesSnapshotHashAndQuarantine()
    {
        await using var restartConnection = new SqliteConnection("Data Source=:memory:");
        await restartConnection.OpenAsync();
        var restartOptions = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(restartConnection)
            .UseModel(ControlParentalDbContextModel.Instance)
            .Options;

        await using (var bootstrap = new ControlParentalDbContext(restartOptions))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        var firstFactory = new TrackingDbContextFactory(restartOptions);
        using (var firstRepository = new PolicyRepository(firstFactory, this.timeProvider))
        {
            Assert.True(await firstRepository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-a")));
            Assert.False(await firstRepository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-b")));
        }

        var restartedFactory = new TrackingDbContextFactory(restartOptions);
        using var restartedRepository = new PolicyRepository(restartedFactory, this.timeProvider);

        Assert.Null(await restartedRepository.GetPolicyAsync());
        Assert.True(restartedRepository.IsPolicyQuarantined);
        Assert.Equal("same_version_snapshot_hash_mismatch", restartedRepository.PolicyQuarantineReason);
        await using var verificationContext = new ControlParentalDbContext(restartOptions);
        var persisted = await verificationContext.Policies.SingleAsync();
        Assert.Equal("hash-a", persisted.SnapshotHash);
        Assert.True(persisted.IsQuarantined);
    }

    [Fact]
    public async Task GetPolicyAsync_AfterRestart_WithMissingQuarantineReason_RemainsObservable()
    {
        await this.repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-a"));
        var persisted = await this.db.Policies.SingleAsync();
        persisted.IsQuarantined = true;
        persisted.QuarantineReason = null;
        await this.db.SaveChangesAsync();

        using var restartedRepository = new PolicyRepository(this.dbContextFactory, this.timeProvider);

        Assert.Null(await restartedRepository.GetPolicyAsync());
        Assert.True(restartedRepository.IsPolicyQuarantined);
        Assert.Equal("same_version_snapshot_hash_mismatch", restartedRepository.PolicyQuarantineReason);
    }

    [Fact]
    public async Task GetPolicyAsync_DuringConflictingUpsert_DoesNotReturnCachedPolicy()
    {
        await using var concurrencyConnection = new SqliteConnection("Data Source=:memory:");
        await concurrencyConnection.OpenAsync();
        var normalOptions = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(concurrencyConnection)
            .Options;
        await using (var bootstrap = new ControlParentalDbContext(normalOptions))
        {
            await bootstrap.Database.EnsureCreatedAsync();
        }

        var saveBlocker = new QuarantineSaveBlocker();
        var blockedOptions = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(concurrencyConnection)
            .AddInterceptors(saveBlocker)
            .Options;
        var factory = new SwitchingDbContextFactory(normalOptions);
        using var repository = new PolicyRepository(factory, this.timeProvider);
        Assert.True(await repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-a")));
        Assert.NotNull(await repository.GetPolicyAsync());

        factory.Options = blockedOptions;
        var conflictingUpsert = repository.UpsertPolicyAsync(MakePolicy(version: 5, snapshotHash: "hash-b"));
        await saveBlocker.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var concurrentRead = repository.GetPolicyAsync();
        Assert.False(concurrentRead.IsCompleted);

        saveBlocker.Release.TrySetResult(true);
        Assert.False(await conflictingUpsert);
        Assert.Null(await concurrentRead);
        Assert.True(repository.IsPolicyQuarantined);
    }

    [Fact]
    public async Task UpsertPolicy_InvalidPolicyFailsClosedWithoutWriting()
    {
        var invalid = MakePolicy(deviceId: "dev-1", version: 5) with { DailyScreenTimeMinutes = -1 };
        var contextsBefore = this.dbContextFactory.CreateCount;

        var applied = await this.repository.UpsertPolicyAsync(invalid);

        Assert.False(applied);
        Assert.Empty(this.db.Policies);
        Assert.Equal(contextsBefore, this.dbContextFactory.CreateCount);
    }

    [Fact]
    public async Task UpsertPolicy_UpgradeVersion_ShouldApply()
    {
        // Arrange
        await this.repository.UpsertPolicyAsync(MakePolicy(deviceId: "dev-1", version: 5));

        // Act
        var applied = await this.repository.UpsertPolicyAsync(MakePolicy(deviceId: "dev-1", version: 6));

        // Assert
        Assert.True(applied);
        var stored = await this.repository.GetPolicyAsync();
        Assert.Equal(6, stored!.Version);
    }

    [Fact]
    public async Task RepeatedPolicyReadsUseVersionedSnapshotWithoutMoreDatabaseContexts()
    {
        await this.repository.UpsertPolicyAsync(MakePolicy(deviceId: "dev-1", version: 5));
        var contextsAfterCommit = this.dbContextFactory.CreateCount;

        var first = await this.repository.GetPolicyAsync();
        var second = await this.repository.GetPolicyAsync();

        Assert.Same(first, second);
        Assert.Equal(5, second!.Version);
        Assert.Equal(contextsAfterCommit, this.dbContextFactory.CreateCount);
    }

    [Fact]
    public async Task ExplicitSnapshotInvalidationReloadsAuthoritativePolicyOnce()
    {
        await this.repository.UpsertPolicyAsync(MakePolicy(deviceId: "dev-1", version: 5));
        var contextsAfterCommit = this.dbContextFactory.CreateCount;

        this.repository.InvalidatePolicySnapshot();
        var reloaded = await this.repository.GetPolicyAsync();
        var cached = await this.repository.GetPolicyAsync();

        Assert.Equal(5, reloaded!.Version);
        Assert.Same(reloaded, cached);
        Assert.Equal(contextsAfterCommit + 1, this.dbContextFactory.CreateCount);
    }

    // ── Usage tracking ─────────────────────────────────────────────────

    [Fact]
    public async Task AccumulateUsage_NewApp_ShouldCreateRecord()
    {
        // Arrange
        this.timeProvider.SetServerDate(new DateOnly(2026, 6, 11));

        // Act
        await this.repository.AccumulateUsageAsync("chrome", 20);

        // Assert
        var usage = await this.repository.GetAppUsageAsync("chrome");
        Assert.Equal(20, usage);

        var stored = await this.db.UsageToday.SingleAsync();
        Assert.Equal(20L, stored.ElapsedSeconds);
        Assert.Equal(0, stored.Minutes);
        Assert.Equal(FixedNow, stored.LastUpdated);
        Assert.Equal(new DateOnly(2026, 6, 11), stored.ServerDate);
    }

    [Fact]
    public async Task AccumulateUsage_WhenServerDateMissing_UsesWallClockDate()
    {
        // Arrange
        this.timeProvider.SetServerDate(null);

        // Act
        await this.repository.AccumulateUsageAsync("chrome", 5);

        // Assert
        var stored = await this.db.UsageToday.SingleAsync();
        Assert.Equal(DateOnly.FromDateTime(FixedNow.UtcDateTime), stored.ServerDate);
        Assert.Equal(FixedNow, stored.LastUpdated);
        Assert.Equal(5L, stored.ElapsedSeconds);
    }

    [Fact]
    public async Task AccumulateUsage_ExistingApp_ShouldIncrement()
    {
        // Arrange
        this.timeProvider.SetServerDate(new DateOnly(2026, 6, 11));
        await this.repository.AccumulateUsageAsync("chrome", 20);

        // Act
        await this.repository.AccumulateUsageAsync("chrome", 25);
        await this.repository.AccumulateUsageAsync("chrome", 20);

        // Assert
        var usage = await this.repository.GetAppUsageAsync("chrome");
        Assert.Equal(65, usage);

        var stored = await this.db.UsageToday.SingleAsync();
        Assert.Equal(65L, stored.ElapsedSeconds);
        Assert.Equal(1, stored.Minutes);
    }

    [Fact]
    public async Task GetUsageSnapshot_ShouldComputeAggregates()
    {
        // Arrange
        this.timeProvider.SetServerDate(new DateOnly(2026, 6, 11));

        // Policy with category assignments
        var policy = MakePolicy(
            deviceId: "dev-1",
            version: 1,
            categoryAssignments: new Dictionary<string, string> { { "chrome", "games" }, { "whatsapp", "social" } },
            appPolicies: new[]
            {
                new AppPolicy { PackageName = "whatsapp", State = AppPolicyState.AlwaysAllowed },
            });
        await this.repository.UpsertPolicyAsync(policy);

        // Add usage
        await this.repository.AccumulateUsageAsync("chrome", 30 * 60);
        await this.repository.AccumulateUsageAsync("whatsapp", 20 * 60);
        await this.repository.AccumulateUsageAsync("notepad", 10 * 60);

        // Act
        var snapshot = await this.repository.GetUsageSnapshotAsync();

        // Assert
        Assert.Equal(3, snapshot.AppMinutes.Count);
        Assert.Equal(30, snapshot.AppMinutes["chrome"]);
        Assert.Equal(20, snapshot.AppMinutes["whatsapp"]);

        // whatsapp is always_allowed → exempt from global count and category aggregate
        Assert.Equal(40, snapshot.GlobalMinutes); // 30 (chrome) + 10 (notepad); whatsapp excluded

        // Category aggregates: only non-exempt apps contribute
        Assert.Equal(30, snapshot.CategoryMinutes["games"]); // chrome is games, not exempt
        // whatsapp is AlwaysAllowed → exempt, so social category has 0 usage recorded
        Assert.Equal(0, snapshot.CategoryMinutes.GetValueOrDefault("social"));

        // always_allowed apps are exempt
        Assert.True(snapshot.ExemptAppIds.Contains("whatsapp"));
    }

    // ── Outbox ─────────────────────────────────────────────────────────

    [Fact]
    public async Task EnqueueOutboxEvent_New_ShouldInsert()
    {
        // Act
        await this.repository.EnqueueOutboxEventAsync("usage_log", """{"app": "chrome"}""", "dedup-1");

        // Assert
        var pending = await this.repository.GetPendingOutboxEventsAsync();
        Assert.Single(pending);
        Assert.Equal("usage_log", pending[0].EventType);
        Assert.Equal("dedup-1", pending[0].DedupKey);
        Assert.Equal(FixedNow, pending[0].CreatedAt);
    }

    [Fact]
    public async Task EnqueueOutboxEvent_DuplicateDedupKey_ShouldSkip()
    {
        // Arrange
        await this.repository.EnqueueOutboxEventAsync("usage_log", """{"app": "chrome"}""", "dedup-1");

        // Act — re-enqueue same dedup key
        await this.repository.EnqueueOutboxEventAsync("usage_log", """{"app": "chrome2"}""", "dedup-1");

        // Assert
        var pending = await this.repository.GetPendingOutboxEventsAsync();
        Assert.Single(pending); // Still 1, not 2
    }

    [Fact]
    public async Task MarkOutboxSent_ShouldRemove()
    {
        // Arrange
        await this.repository.EnqueueOutboxEventAsync("usage_log", """{}""", "dedup-1");
        var pending = await this.repository.GetPendingOutboxEventsAsync();
        var id = pending[0].Id;

        // Act
        await this.repository.MarkOutboxSentAsync(id);

        // Assert
        var remaining = await this.repository.GetPendingOutboxEventsAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task MarkOutboxFailed_ShouldIncrementAttempts()
    {
        // Arrange
        await this.repository.EnqueueOutboxEventAsync("usage_log", """{}""", "dedup-1");
        var pending = await this.repository.GetPendingOutboxEventsAsync();
        var id = pending[0].Id;

        // Act
        await this.repository.MarkOutboxFailedAsync(id, "Network error");

        // Assert
        var updated = await this.db.Outbox.FindAsync(id);
        Assert.NotNull(updated);
        Assert.Equal(1, updated.Attempts);
        Assert.Equal("Network error", updated.LastError);
        Assert.NotNull(updated.LastAttemptAt);
        Assert.Equal(FixedNow, updated.LastAttemptAt);
    }

    // ── Grants ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveGrants_Expired_ShouldNotReturn()
    {
        // Arrange
        var now = FixedNow;
        var policy = MakePolicy(
            deviceId: "dev-1",
            version: 1,
            grants: new[]
            {
                new Grant
                {
                    Id = "g1",
                    RequestId = "r1",
                    Scope = "device",
                    Minutes = 30,
                    GrantedAt = now.AddMinutes(-60),
                    ExpiresAt = now.AddMinutes(-30), // Expired 30 min ago
                    Source = GrantSource.ExtraTime,
                },
            });
        await this.repository.UpsertPolicyAsync(policy);

        // Act
        var active = await this.repository.GetActiveGrantsAsync(now);

        // Assert
        Assert.Empty(active);
    }

    [Fact]
    public async Task GetActiveGrants_Active_ShouldReturn()
    {
        // Arrange
        var now = FixedNow;
        var policy = MakePolicy(
            deviceId: "dev-1",
            version: 1,
            grants: new[]
            {
                new Grant
                {
                    Id = "g1",
                    RequestId = "r1",
                    Scope = "device",
                    Minutes = 30,
                    GrantedAt = now.AddMinutes(-30),
                    ExpiresAt = now.AddMinutes(30),
                    Source = GrantSource.ExtraTime,
                },
            });
        await this.repository.UpsertPolicyAsync(policy);

        // Act
        var active = await this.repository.GetActiveGrantsAsync(now);

        // Assert
        Assert.Single(active);
        Assert.Equal("g1", active[0].Id);
        Assert.Equal("device", active[0].Scope);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static Policy MakePolicy(
        string deviceId = "dev-1",
        int version = 1,
        Schedule[]? schedules = null,
        AppPolicy[]? appPolicies = null,
        CategoryLimit[]? categoryLimits = null,
        Grant[]? grants = null,
        Dictionary<string, string>? categoryAssignments = null,
        string snapshotHash = "hash-a")
    {
        return new Policy
        {
            DeviceId = deviceId,
            Version = version,
            SnapshotHash = snapshotHash,
            DeviceState = DeviceState.Active,
            DailyScreenTimeMinutes = 120,
            Schedules = schedules ?? [],
            CategoryLimits = categoryLimits ?? [],
            AppPolicies = appPolicies ?? [],
            CategoryAssignments = categoryAssignments ?? new Dictionary<string, string>(),
            Grants = grants ?? [],
        };
    }

    private sealed class SwitchingDbContextFactory : IDbContextFactory<ControlParentalDbContext>
    {
        public SwitchingDbContextFactory(DbContextOptions<ControlParentalDbContext> options) => this.Options = options;

        public DbContextOptions<ControlParentalDbContext> Options { get; set; }

        public ControlParentalDbContext CreateDbContext() => new(this.Options);
    }

    private sealed class QuarantineSaveBlocker : SaveChangesInterceptor
    {
        public TaskCompletionSource<bool> SaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var isQuarantineWrite = eventData.Context?.ChangeTracker
                .Entries<PolicyDbEntity>()
                .Any(entry => entry.State == EntityState.Modified && entry.Entity.IsQuarantined) == true;
            if (isQuarantineWrite)
            {
                this.SaveEntered.TrySetResult(true);
                await this.Release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    // Fake ITimeProvider for testing
    private sealed class FakeTimeProvider : ITimeProvider
    {
        private DateTimeOffset wallClock;
        private DateOnly? serverDate;
        private bool serverDateUncertain;

        public FakeTimeProvider(DateTimeOffset now) => this.wallClock = now;

        public long MonotonicNow => 0;
        public DateTimeOffset WallClockNow => this.wallClock;
        public TimeZoneInfo CurrentZone => TimeZoneInfo.Utc;
        public DateOnly? ServerDate => this.serverDate;
        public bool IsServerDateUncertain => this.serverDateUncertain;
        public DateTimeOffset LocalNow => this.wallClock;

        public event EventHandler<TimeChangedEventArgs>? TimeChanged;

        public void SetServerDate(long offsetMs) { }

        public void SetWallClockNow(DateTimeOffset now) => this.wallClock = now;

        public void SetServerDate(DateOnly? serverDate, bool uncertain = false)
        {
            this.serverDate = serverDate;
            this.serverDateUncertain = serverDate.HasValue && uncertain;
        }

        public bool DetectClockJump() => false;
    }
}
