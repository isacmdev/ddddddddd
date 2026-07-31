// <copyright file="PolicyRoundTripTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain.Tests;

using System.Text.Json;
using Xunit;

/// <summary>
/// Tests for Policy serialization/deserialization (T01).
/// Verifies round-trip of the exact JSON from the backlog.
/// </summary>
public class PolicyRoundTripTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower) },
    };

    private const string SamplePolicyJson = """
        {
          "device_id": "550e8400-e29b-41d4-a716-446655440000",
          "version": 42,
          "device_state": "active",
          "daily_screen_time_minutes": 240,
          "schedules": [
            {"id": "bedtime", "days": ["MON", "TUE", "WED", "THU", "SUN"], "from": "22:00", "to": "07:00", "action": "lock"},
            {"id": "homework", "days": ["MON", "TUE", "WED", "THU", "FRI"], "from": "16:00", "to": "18:00", "action": "allow_only", "allow_list": ["msedge"]}
          ],
          "category_limits": [{"category": "games", "minutes": 60}],
          "app_policies": [
            {"package_name": "whatsapp", "state": "always_allowed"},
            {"package_name": "instagram", "state": "limited", "daily_limit_minutes": 30, "category": "social", "allowed_windows": [{"days": ["MON", "TUE", "WED", "THU", "FRI"], "from": "15:00", "to": "20:00"}]},
            {"package_name": "clashroyale", "state": "blocked", "category": "games"}
          ],
          "category_assignments": {"instagram": "social", "clashroyale": "games"},
          "grants": [
            {"id": "g1", "request_id": "tr_77", "scope": "device", "minutes": 30, "granted_at": "2026-05-25T20:30:00Z", "expires_at": "2026-05-25T21:00:00Z", "source": "extra_time"}
          ]
        }
        """;

    [Fact]
    public void Deserialize_SamplePolicy_ShouldSucceed()
    {
        // Act
        var policy = JsonSerializer.Deserialize<Policy>(SamplePolicyJson, JsonOpts);

        // Assert
        Assert.NotNull(policy);
        Assert.Equal("550e8400-e29b-41d4-a716-446655440000", policy.DeviceId);
        Assert.Equal(42, policy.Version);
        Assert.Equal(DeviceState.Active, policy.DeviceState);
        Assert.Equal(240, policy.DailyScreenTimeMinutes);
    }

    [Fact]
    public void Deserialize_Schedules_ShouldHaveCorrectValues()
    {
        // Act
        var policy = JsonSerializer.Deserialize<Policy>(SamplePolicyJson, JsonOpts);

        // Assert
        Assert.NotNull(policy);
        Assert.Equal(2, policy.Schedules.Length);

        var bedtime = policy.Schedules[0];
        Assert.Equal("bedtime", bedtime.Id);
        Assert.Equal(DayOfWeek.MON, bedtime.Days[0]);
        Assert.Equal("22:00", bedtime.From);
        Assert.Equal("07:00", bedtime.To);
        Assert.Equal(ScheduleAction.Lock, bedtime.Action);

        var homework = policy.Schedules[1];
        Assert.Equal("homework", homework.Id);
        Assert.Equal(ScheduleAction.AllowOnly, homework.Action);
        Assert.NotNull(homework.AllowList);
        Assert.Single(homework.AllowList);
        Assert.Equal("msedge", homework.AllowList[0]);
    }

    [Fact]
    public void Deserialize_AppPolicies_ShouldHaveCorrectStates()
    {
        // Act
        var policy = JsonSerializer.Deserialize<Policy>(SamplePolicyJson, JsonOpts);

        // Assert
        Assert.NotNull(policy);
        Assert.Equal(3, policy.AppPolicies.Length);

        var whatsapp = policy.AppPolicies[0];
        Assert.Equal("whatsapp", whatsapp.PackageName);
        Assert.Equal(AppPolicyState.AlwaysAllowed, whatsapp.State);

        var instagram = policy.AppPolicies[1];
        Assert.Equal("instagram", instagram.PackageName);
        Assert.Equal(AppPolicyState.Limited, instagram.State);
        Assert.Equal(30, instagram.DailyLimitMinutes);
        Assert.Equal("social", instagram.Category);
        Assert.NotNull(instagram.AllowedWindows);
        Assert.Single(instagram.AllowedWindows);

        var clashroyale = policy.AppPolicies[2];
        Assert.Equal("clashroyale", clashroyale.PackageName);
        Assert.Equal(AppPolicyState.Blocked, clashroyale.State);
    }

    [Fact]
    public void Deserialize_Grants_ShouldHaveCorrectTimestamps()
    {
        // Act
        var policy = JsonSerializer.Deserialize<Policy>(SamplePolicyJson, JsonOpts);

        // Assert
        Assert.NotNull(policy);
        Assert.Single(policy.Grants);

        var grant = policy.Grants[0];
        Assert.Equal("g1", grant.Id);
        Assert.Equal("tr_77", grant.RequestId);
        Assert.Equal("device", grant.Scope);
        Assert.Equal(30, grant.Minutes);
        Assert.Equal(GrantSource.ExtraTime, grant.Source);
        Assert.Equal(new DateTimeOffset(2026, 5, 25, 20, 30, 0, TimeSpan.Zero), grant.GrantedAt);
        Assert.Equal(new DateTimeOffset(2026, 5, 25, 21, 0, 0, TimeSpan.Zero), grant.ExpiresAt);
    }

    [Fact]
    public void Deserialize_CategoryAssignments_ShouldMapCorrectly()
    {
        // Act
        var policy = JsonSerializer.Deserialize<Policy>(SamplePolicyJson, JsonOpts);

        // Assert
        Assert.NotNull(policy);
        Assert.Equal(2, policy.CategoryAssignments.Count);
        Assert.Equal("social", policy.CategoryAssignments["instagram"]);
        Assert.Equal("games", policy.CategoryAssignments["clashroyale"]);
    }

    [Fact]
    public void RoundTrip_SerializeDeserialize_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new Policy
        {
            DeviceId = "device-123",
            Version = 5,
            DeviceState = DeviceState.Active,
            DailyScreenTimeMinutes = 120,
            Schedules =
            [
                new Schedule
                {
                    Id = "test-schedule",
                    Days = [DayOfWeek.MON, DayOfWeek.WED],
                    From = "09:00",
                    To = "17:00",
                    Action = ScheduleAction.Lock,
                },
            ],
            CategoryLimits =
            [
                new CategoryLimit { Category = "games", Minutes = 60 },
            ],
            AppPolicies =
            [
                new AppPolicy
                {
                    PackageName = "test.app",
                    State = AppPolicyState.Limited,
                    DailyLimitMinutes = 45,
                    Category = "games",
                    AllowedWindows =
                    [
                        new Window { Days = [DayOfWeek.MON], From = "10:00", To = "15:00" },
                    ],
                },
            ],
            CategoryAssignments = new Dictionary<string, string> { { "test.app", "games" } },
            Grants =
            [
                new Grant
                {
                    Id = "grant-1",
                    RequestId = "req-1",
                    Scope = "device",
                    Minutes = 15,
                    GrantedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
                    Source = GrantSource.ExtraTime,
                },
            ],
        };

        // Act
        var json = JsonSerializer.Serialize(original, JsonOpts);
        var restored = JsonSerializer.Deserialize<Policy>(json, JsonOpts);

        // Assert
        Assert.NotNull(restored);
        Assert.Equal(original.DeviceId, restored.DeviceId);
        Assert.Equal(original.Version, restored.Version);
        Assert.Equal(original.DeviceState, restored.DeviceState);
        Assert.Equal(original.DailyScreenTimeMinutes, restored.DailyScreenTimeMinutes);
        Assert.Equal(original.Schedules.Length, restored.Schedules.Length);
        Assert.Equal(original.CategoryLimits.Length, restored.CategoryLimits.Length);
        Assert.Equal(original.AppPolicies.Length, restored.AppPolicies.Length);
        Assert.Equal(original.Grants.Length, restored.Grants.Length);
        Assert.Equal(original.CategoryAssignments.Count, restored.CategoryAssignments.Count);
    }
}
