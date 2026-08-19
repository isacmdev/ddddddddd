// <copyright file="PolicyRepositoryTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using Microsoft.Data.Sqlite;
using System.Text.Json;
using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
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
        Dictionary<string, string>? categoryAssignments = null)
    {
        return new Policy
        {
            DeviceId = deviceId,
            Version = version,
            DeviceState = DeviceState.Active,
            DailyScreenTimeMinutes = 120,
            Schedules = schedules ?? [],
            CategoryLimits = categoryLimits ?? [],
            AppPolicies = appPolicies ?? [],
            CategoryAssignments = categoryAssignments ?? new Dictionary<string, string>(),
            Grants = grants ?? [],
        };
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
