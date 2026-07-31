// <copyright file="PolicyValidationTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain.Tests;

using System.Text.Json;
using Xunit;

/// <summary>
/// Tests for Policy validation (invariants).
/// Verifies that invalid policies are rejected with clear error messages.
/// </summary>
public class PolicyValidationTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower) },
    };

    [Fact]
    public void Validate_LimitedWithoutDailyLimit_ShouldThrow()
    {
        // Arrange
        var json = """
            {
              "device_id": "dev-1",
              "version": 1,
              "device_state": "active",
              "daily_screen_time_minutes": 60,
              "schedules": [],
              "category_limits": [],
              "app_policies": [
                {"package_name": "test.app", "state": "limited", "daily_limit_minutes": null}
              ],
              "category_assignments": {},
              "grants": []
            }
            """;

        var policy = JsonSerializer.Deserialize<Policy>(json, JsonOpts);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => policy!.Validate());
        Assert.Contains("daily_limit_minutes", ex.Message);
    }

    [Fact]
    public void Validate_AllowOnlyWithoutAllowList_ShouldThrow()
    {
        // Arrange
        var json = """
            {
              "device_id": "dev-1",
              "version": 1,
              "device_state": "active",
              "daily_screen_time_minutes": 60,
              "schedules": [
                {"id": "s1", "days": ["MON"], "from": "10:00", "to": "12:00", "action": "allow_only", "allow_list": []}
              ],
              "category_limits": [],
              "app_policies": [],
              "category_assignments": {},
              "grants": []
            }
            """;

        var policy = JsonSerializer.Deserialize<Policy>(json, JsonOpts);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => policy!.Validate());
        Assert.Contains("allow_only", ex.Message);
    }

    [Fact]
    public void Validate_GrantExpiresBeforeGranted_ShouldThrow()
    {
        // Arrange
        var json = """
            {
              "device_id": "dev-1",
              "version": 1,
              "device_state": "active",
              "daily_screen_time_minutes": 60,
              "schedules": [],
              "category_limits": [],
              "app_policies": [],
              "category_assignments": {},
              "grants": [
                {"id": "g1", "request_id": "r1", "scope": "device", "minutes": 30, "granted_at": "2026-05-25T21:00:00Z", "expires_at": "2026-05-25T20:00:00Z", "source": "extra_time"}
              ]
            }
            """;

        var policy = JsonSerializer.Deserialize<Policy>(json, JsonOpts);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => policy!.Validate());
        Assert.Contains("expires_at", ex.Message);
    }

    [Fact]
    public void Validate_InvalidTimeFormat_ShouldThrow()
    {
        // Arrange
        var json = """
            {
              "device_id": "dev-1",
              "version": 1,
              "device_state": "active",
              "daily_screen_time_minutes": 60,
              "schedules": [
                {"id": "s1", "days": ["MON"], "from": "25:00", "to": "12:00", "action": "lock"}
              ],
              "category_limits": [],
              "app_policies": [],
              "category_assignments": {},
              "grants": []
            }
            """;

        var policy = JsonSerializer.Deserialize<Policy>(json, JsonOpts);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => policy!.Validate());
        Assert.Contains("HH:mm", ex.Message);
    }

    [Fact]
    public void Validate_EmptyDeviceId_ShouldThrow()
    {
        // Arrange
        var json = """
            {
              "device_id": "",
              "version": 1,
              "device_state": "active",
              "daily_screen_time_minutes": 60,
              "schedules": [],
              "category_limits": [],
              "app_policies": [],
              "category_assignments": {},
              "grants": []
            }
            """;

        var policy = JsonSerializer.Deserialize<Policy>(json, JsonOpts);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => policy!.Validate());
        Assert.Contains("device_id", ex.Message);
    }

    [Fact]
    public void Validate_ZeroVersion_ShouldThrow()
    {
        // Arrange
        var json = """
            {
              "device_id": "dev-1",
              "version": 0,
              "device_state": "active",
              "daily_screen_time_minutes": 60,
              "schedules": [],
              "category_limits": [],
              "app_policies": [],
              "category_assignments": {},
              "grants": []
            }
            """;

        var policy = JsonSerializer.Deserialize<Policy>(json, JsonOpts);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => policy!.Validate());
        Assert.Contains("version", ex.Message);
    }

    [Fact]
    public void Grant_IsActive_ShouldReturnCorrectResult()
    {
        // Arrange
        var grant = new Grant
        {
            Id = "g1",
            Scope = "device",
            Minutes = 30,
            GrantedAt = new DateTimeOffset(2026, 5, 25, 20, 0, 0, TimeSpan.Zero),
            ExpiresAt = new DateTimeOffset(2026, 5, 25, 20, 30, 0, TimeSpan.Zero),
            Source = GrantSource.ExtraTime,
        };

        // Assert - before grant
        Assert.False(grant.IsActive(new DateTimeOffset(2026, 5, 25, 19, 0, 0, TimeSpan.Zero)));

        // Assert - at grant start (inclusive)
        Assert.True(grant.IsActive(new DateTimeOffset(2026, 5, 25, 20, 0, 0, TimeSpan.Zero)));

        // Assert - middle
        Assert.True(grant.IsActive(new DateTimeOffset(2026, 5, 25, 20, 15, 0, TimeSpan.Zero)));

        // Assert - at expiry (exclusive)
        Assert.False(grant.IsActive(new DateTimeOffset(2026, 5, 25, 20, 30, 0, TimeSpan.Zero)));

        // Assert - after expiry
        Assert.False(grant.IsActive(new DateTimeOffset(2026, 5, 25, 21, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Window_CrossesMidnight_ShouldReturnCorrectValue()
    {
        // Arrange
        var normalWindow = new Window { Days = [DayOfWeek.MON], From = "15:00", To = "20:00" };
        var midnightWindow = new Window { Days = [DayOfWeek.MON], From = "22:00", To = "07:00" };

        // Assert
        Assert.False(normalWindow.CrossesMidnight);
        Assert.True(midnightWindow.CrossesMidnight);
    }

    [Fact]
    public void Policy_GetAppPolicy_ShouldReturnCorrectPolicy()
    {
        // Arrange
        var policy = new Policy
        {
            DeviceId = "dev-1",
            Version = 1,
            AppPolicies =
            [
                new AppPolicy { PackageName = "whatsapp", State = AppPolicyState.AlwaysAllowed },
                new AppPolicy { PackageName = "instagram", State = AppPolicyState.Limited, DailyLimitMinutes = 30 },
            ],
        };

        // Act & Assert
        var whatsapp = policy.GetAppPolicy("whatsapp");
        Assert.NotNull(whatsapp);
        Assert.Equal(AppPolicyState.AlwaysAllowed, whatsapp.State);

        var instagram = policy.GetAppPolicy("instagram");
        Assert.NotNull(instagram);
        Assert.Equal(30, instagram.DailyLimitMinutes);

        var nonexistent = policy.GetAppPolicy("nonexistent");
        Assert.Null(nonexistent);
    }

    [Fact]
    public void Policy_GetActiveGrants_ShouldReturnOnlyActiveGrants()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 5, 25, 12, 0, 0, TimeSpan.Zero);

        var policy = new Policy
        {
            DeviceId = "dev-1",
            Version = 1,
            Grants =
            [
                new Grant
                {
                    Id = "g1",
                    Scope = "device",
                    Minutes = 30,
                    GrantedAt = new DateTimeOffset(2026, 5, 25, 10, 0, 0, TimeSpan.Zero),
                    ExpiresAt = new DateTimeOffset(2026, 5, 25, 11, 0, 0, TimeSpan.Zero),
                    Source = GrantSource.ExtraTime,
                },
                new Grant
                {
                    Id = "g2",
                    Scope = "device",
                    Minutes = 30,
                    GrantedAt = new DateTimeOffset(2026, 5, 25, 11, 0, 0, TimeSpan.Zero),
                    ExpiresAt = new DateTimeOffset(2026, 5, 25, 13, 0, 0, TimeSpan.Zero),
                    Source = GrantSource.ExtraTime,
                },
                new Grant
                {
                    Id = "g3",
                    Scope = "device",
                    Minutes = 30,
                    GrantedAt = new DateTimeOffset(2026, 5, 25, 14, 0, 0, TimeSpan.Zero),
                    ExpiresAt = new DateTimeOffset(2026, 5, 25, 15, 0, 0, TimeSpan.Zero),
                    Source = GrantSource.ExtraTime,
                },
            ],
        };

        // Act
        var active = policy.GetActiveGrants(now);

        // Assert
        Assert.Single(active);
        Assert.Equal("g2", active[0].Id);
    }
}
