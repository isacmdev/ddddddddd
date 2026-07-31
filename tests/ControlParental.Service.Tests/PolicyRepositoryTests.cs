// <copyright file="PolicyRepositoryTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Text.Json;
using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// T03 — Tests for PolicyRepository using in-memory SQLite.
/// </summary>
public class PolicyRepositoryTests : IDisposable
{
    private readonly ControlParentalDbContext db;
    private readonly FakeTimeProvider timeProvider;
    private readonly PolicyRepository repository;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower) },
    };

    public PolicyRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        this.db = new ControlParentalDbContext(options);
        this.timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        this.repository = new PolicyRepository(this.db, this.timeProvider);
    }

    public void Dispose()
    {
        this.db.Dispose();
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

    // ── Usage tracking ─────────────────────────────────────────────────

    [Fact]
    public async Task AccumulateUsage_NewApp_ShouldCreateRecord()
    {
        // Arrange
        this.timeProvider.SetServerDate(new DateOnly(2026, 6, 11));

        // Act
        await this.repository.AccumulateUsageAsync("chrome", 5);

        // Assert
        var usage = await this.repository.GetAppUsageAsync("chrome");
        Assert.Equal(5, usage);
    }

    [Fact]
    public async Task AccumulateUsage_ExistingApp_ShouldIncrement()
    {
        // Arrange
        this.timeProvider.SetServerDate(new DateOnly(2026, 6, 11));
        await this.repository.AccumulateUsageAsync("chrome", 5);

        // Act
        await this.repository.AccumulateUsageAsync("chrome", 10);

        // Assert
        var usage = await this.repository.GetAppUsageAsync("chrome");
        Assert.Equal(15, usage);
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
        await this.repository.AccumulateUsageAsync("chrome", 30);
        await this.repository.AccumulateUsageAsync("whatsapp", 20);
        await this.repository.AccumulateUsageAsync("notepad", 10);

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
    }

    // ── Grants ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveGrants_Expired_ShouldNotReturn()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
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
        var now = DateTimeOffset.UtcNow;
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

        public void SetServerDate(DateOnly date, bool uncertain = false)
        {
            this.serverDate = date;
            this.serverDateUncertain = uncertain;
        }

        public void SetServerDate(DateOnly serverDate)
        {
            this.serverDate = serverDate;
            this.serverDateUncertain = false;
        }

        public bool DetectClockJump() => false;
    }
}
