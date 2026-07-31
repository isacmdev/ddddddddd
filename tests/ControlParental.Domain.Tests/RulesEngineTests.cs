// <copyright file="RulesEngineTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain.Tests;

using Xunit;

/// <summary>
/// T02 Rules Engine tests — exhaustive edge cases per step.
/// Step numbering matches the spec: 1=whitelist, 2=device_locked, 3=blocked_app,
/// 4=allow_only, 5=allowed_windows, 6=grant, 7=downtime, 8=lock_schedule,
/// 9=app_limit, 10=category_limit, 11=global_limit, 12=default_allow.
/// </summary>
public class RulesEngineTests
{
    private static readonly TimeZoneInfo Tokyo = TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time");

    // ── Helper factories ───────────────────────────────────────────────────

    private static Policy MakePolicy(
        DeviceState deviceState = DeviceState.Active,
        int dailyScreenTime = 120,
        Schedule[]? schedules = null,
        AppPolicy[]? appPolicies = null,
        CategoryLimit[]? categoryLimits = null,
        Grant[]? grants = null,
        Dictionary<string, string>? categoryAssignments = null)
    {
        return new Policy
        {
            DeviceId = "test-device",
            Version = 1,
            DeviceState = deviceState,
            DailyScreenTimeMinutes = dailyScreenTime,
            Schedules = schedules ?? [],
            CategoryLimits = categoryLimits ?? [],
            AppPolicies = appPolicies ?? [],
            CategoryAssignments = categoryAssignments ?? new Dictionary<string, string>(),
            Grants = grants ?? [],
        };
    }

    private static UsageSnapshot MakeUsage(
        Dictionary<string, int>? appMinutes = null,
        Dictionary<string, int>? categoryMinutes = null,
        int globalMinutes = 0,
        HashSet<string>? exemptApps = null)
    {
        return new UsageSnapshot(
            AppMinutes: appMinutes ?? new Dictionary<string, int>(),
            CategoryMinutes: categoryMinutes ?? new Dictionary<string, int>(),
            GlobalMinutes: globalMinutes,
            ExemptAppIds: exemptApps ?? new HashSet<string>());
    }

    private static DateTimeOffset OnDay(DayOfWeek day, int hour, int minute)
    {
        // Build a date that falls on the requested day (Monday = day 1)
        var baseDate = new DateTimeOffset(2026, 6, 1, hour, minute, 0, TimeSpan.Zero); // Monday
        var targetDow = day switch
        {
            DayOfWeek.MON => System.DayOfWeek.Monday,
            DayOfWeek.TUE => System.DayOfWeek.Tuesday,
            DayOfWeek.WED => System.DayOfWeek.Wednesday,
            DayOfWeek.THU => System.DayOfWeek.Thursday,
            DayOfWeek.FRI => System.DayOfWeek.Friday,
            DayOfWeek.SAT => System.DayOfWeek.Saturday,
            DayOfWeek.SUN => System.DayOfWeek.Sunday,
            _ => System.DayOfWeek.Monday,
        };
        var daysToAdd = ((int)targetDow - (int)baseDate.DayOfWeek + 7) % 7;
        return baseDate.AddDays(daysToAdd);
    }

    private static DateTimeOffset OnMonday(int hour, int minute)
        => OnDay(DayOfWeek.MON, hour, minute);

    // ── Step 1: Hard whitelist ─────────────────────────────────────────────

    [Theory]
    [InlineData("winlogon")]
    [InlineData("winlogon.exe")]
    [InlineData("explorer")]
    [InlineData("explorer.exe")]
    [InlineData("ControlParental.SessionAgent")]
    [InlineData("csrss")]
    [InlineData("logonui")]
    [InlineData("dwm")]
    [InlineData("svchost")]
    public void Evaluar_Step1_ExemptProcess_ShouldAllow(string appId)
    {
        // Arrange
        var policy = MakePolicy(deviceState: DeviceState.Locked); // Would block at step 2
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, appId, usage, now, TimeZoneInfo.Utc);

        // Assert — step 1 bypasses everything including device locked
        Assert.False(decision.IsBlocked);
        Assert.Null(decision.ReasonCode);
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("notepad")]
    [InlineData("com.whatsapp")]
    public void Evaluar_Step1_NonExempt_ShouldNotBypass(string appId)
    {
        // Arrange — locked device would block at step 2
        var policy = MakePolicy(deviceState: DeviceState.Locked);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, appId, usage, now, TimeZoneInfo.Utc);

        // Assert — blocked by step 2, not allowed by step 1
        Assert.True(decision.IsBlocked);
        Assert.Equal(2, decision.ReasonCode);
    }

    // ── Step 2: Device locked ─────────────────────────────────────────────

    [Fact]
    public void Evaluar_Step2_DeviceLocked_ShouldBlock()
    {
        // Arrange
        var policy = MakePolicy(deviceState: DeviceState.Locked);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(2, decision.ReasonCode);
        Assert.Contains("bloqueado", decision.ReasonText);
    }

    [Fact]
    public void Evaluar_Step2_DeviceLocked_EvenWithAlwaysAllowed_ShouldBlock()
    {
        // Arrange — always_allowed normally bypasses most restrictions
        var policy = MakePolicy(
            deviceState: DeviceState.Locked,
            appPolicies: [new AppPolicy { PackageName = "whatsapp", State = AppPolicyState.AlwaysAllowed }]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "whatsapp", usage, now, TimeZoneInfo.Utc);

        // Assert — step 2 blocks even always_allowed
        Assert.True(decision.IsBlocked);
        Assert.Equal(2, decision.ReasonCode);
    }

    // ── Step 3: App blocked ───────────────────────────────────────────────

    [Fact]
    public void Evaluar_Step3_BlockedApp_ShouldBlock()
    {
        // Arrange
        var policy = MakePolicy(
            appPolicies: [new AppPolicy { PackageName = "chrome", State = AppPolicyState.Blocked }]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(3, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_Step3_BlockedApp_EvenWithActiveGrant_ShouldBlock()
    {
        // Arrange — grant would lift steps 7-11 but NOT step 3
        var policy = MakePolicy(
            appPolicies: [new AppPolicy { PackageName = "chrome", State = AppPolicyState.Blocked }],
            grants: [MakeGrant("device", now: OnMonday(14, 0))]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — step 3 blocks even with active grant
        Assert.True(decision.IsBlocked);
        Assert.Equal(3, decision.ReasonCode);
    }

    // ── Step 4: Allow-only schedule ─────────────────────────────────────

    [Fact]
    public void Evaluar_Step4_AllowOnlyActive_AppNotInList_ShouldBlock()
    {
        // Arrange — allow_only schedule Mon 13:00-15:00, app not in list
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "hw", Days = [DayOfWeek.MON], From = "13:00", To = "15:00", Action = ScheduleAction.AllowOnly, AllowList = ["msedge"] }]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0); // During allow_only window

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(4, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_Step4_AllowOnlyActive_AppInList_ShouldContinue()
    {
        // Arrange — allow_only with app in list
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "hw", Days = [DayOfWeek.MON], From = "13:00", To = "15:00", Action = ScheduleAction.AllowOnly, AllowList = ["chrome", "msedge"] }]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — not blocked by step 4, continues to step 5
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step4_AllowOnly_OutsideWindow_ShouldNotBlock()
    {
        // Arrange — allow_only 13:00-15:00, testing 12:00 (before)
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "hw", Days = [DayOfWeek.MON], From = "13:00", To = "15:00", Action = ScheduleAction.AllowOnly, AllowList = ["msedge"] }]);
        var usage = MakeUsage();
        var now = OnMonday(12, 0); // Before allow_only window

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — no block, step 4 not active
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step4_AllowOnly_CrossMidnight_ShouldWork()
    {
        // Arrange — allow_only 22:00-07:00 (crosses midnight)
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "night", Days = [DayOfWeek.MON], From = "22:00", To = "07:00", Action = ScheduleAction.AllowOnly, AllowList = ["msedge"] }]);
        var usage = MakeUsage();

        // Act — at 23:00 (still Monday, within window)
        var decision23 = RulesEngine.Evaluar(policy, "chrome", usage, OnMonday(23, 0), TimeZoneInfo.Utc);
        // Act — at 03:00 (still Monday, within window)
        var decision03 = RulesEngine.Evaluar(policy, "chrome", usage, OnMonday(3, 0), TimeZoneInfo.Utc);

        Assert.True(decision23.IsBlocked); // Step 4 blocks chrome (not in list)
        Assert.True(decision03.IsBlocked); // Step 4 blocks chrome (not in list)
        Assert.Equal(4, decision23.ReasonCode);
        Assert.Equal(4, decision03.ReasonCode);
    }

    // ── Step 5: App has allowed_windows ──────────────────────────────────

    [Fact]
    public void Evaluar_Step5_OutsideAllowedWindow_ShouldBlock()
    {
        // Arrange — app has allowed_windows Mon 14:00-16:00, testing 17:00
        var policy = MakePolicy(
            appPolicies: [new AppPolicy
            {
                PackageName = "instagram",
                State = AppPolicyState.Limited,
                DailyLimitMinutes = 60,
                AllowedWindows = [new Window { Days = [DayOfWeek.MON], From = "14:00", To = "16:00" }],
            }]);
        var usage = MakeUsage();
        var now = OnMonday(17, 0); // Outside window

        // Act
        var decision = RulesEngine.Evaluar(policy, "instagram", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(5, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_Step5_WithinAllowedWindow_ShouldContinue()
    {
        // Arrange
        var policy = MakePolicy(
            appPolicies: [new AppPolicy
            {
                PackageName = "instagram",
                State = AppPolicyState.Limited,
                DailyLimitMinutes = 60,
                AllowedWindows = [new Window { Days = [DayOfWeek.MON], From = "14:00", To = "16:00" }],
            }]);
        var usage = MakeUsage(appMinutes: new Dictionary<string, int> { { "instagram", 30 } });
        var now = OnMonday(14, 30); // Inside window

        // Act
        var decision = RulesEngine.Evaluar(policy, "instagram", usage, now, TimeZoneInfo.Utc);

        // Assert — not blocked by step 5
        Assert.False(decision.IsBlocked);
    }

    // ── Step 6: Grant ─────────────────────────────────────────────────────

    [Fact]
    public void Evaluar_Step6_GrantDevice_Active_ShouldAllow()
    {
        // Arrange — grant for device scope active now
        var policy = MakePolicy(
            grants: [MakeGrant("device", now: OnMonday(14, 0))]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — step 6 allows, engine continues but all remaining steps pass
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step6_GrantDevice_DoesNotLiftBlockedOrAllowOnly()
    {
        // Arrange — grant does NOT lift steps 2-5
        var policy = MakePolicy(
            appPolicies: [new AppPolicy { PackageName = "chrome", State = AppPolicyState.Blocked }],
            grants: [MakeGrant("device", now: OnMonday(14, 0))]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — blocked by step 3, grant doesn't lift it
        Assert.True(decision.IsBlocked);
        Assert.Equal(3, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_Step6_GrantDevice_DoesNotLiftAllowOnly()
    {
        // Arrange
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "hw", Days = [DayOfWeek.MON], From = "13:00", To = "15:00", Action = ScheduleAction.AllowOnly, AllowList = ["msedge"] }],
            grants: [MakeGrant("device", now: OnMonday(14, 0))]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — step 4 blocks (grant only lifts 7-11)
        Assert.True(decision.IsBlocked);
        Assert.Equal(4, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_Step6_GrantPackage_Active_ShouldAllow()
    {
        // Arrange — grant for specific package
        var policy = MakePolicy(
            grants: [MakeGrant("chrome", now: OnMonday(14, 0))]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step6_GrantCategory_Active_ShouldAllow()
    {
        // Arrange — grant for category, app in that category
        var policy = MakePolicy(
            grants: [MakeGrant("category", now: OnMonday(14, 0))],
            categoryAssignments: new Dictionary<string, string> { { "chrome", "games" } });
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step6_GrantExpired_ShouldNotAllow()
    {
        // Arrange — grant expired before now
        var policy = MakePolicy(
            grants: [MakeGrant("device", now: OnMonday(14, 0), expiresAt: OnMonday(13, 0))]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — grant not active, step 6 skipped
        Assert.False(decision.IsBlocked); // Would proceed to step 12
    }

    // ── Step 7: Downtime ─────────────────────────────────────────────────

    [Fact]
    public void Evaluar_Step7_Downtime_NonAlwaysAllowed_ShouldBlock()
    {
        // Arrange
        var policy = MakePolicy(deviceState: DeviceState.Downtime);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(7, decision.ReasonCode);
        Assert.Contains("dormir", decision.ReasonText);
    }

    [Fact]
    public void Evaluar_Step7_Downtime_AlwaysAllowed_ShouldContinue()
    {
        // Arrange
        var policy = MakePolicy(
            deviceState: DeviceState.Downtime,
            appPolicies: [new AppPolicy { PackageName = "whatsapp", State = AppPolicyState.AlwaysAllowed }]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "whatsapp", usage, now, TimeZoneInfo.Utc);

        // Assert — not blocked by step 7
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step7_Downtime_GrantLifts_ShouldAllow()
    {
        // Arrange — grant lifts steps 7-11
        var policy = MakePolicy(
            deviceState: DeviceState.Downtime,
            grants: [MakeGrant("device", now: OnMonday(14, 0))]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — grant lifts step 7
        Assert.False(decision.IsBlocked);
    }

    // ── Step 8: Lock schedule ────────────────────────────────────────────

    [Fact]
    public void Evaluar_Step8_LockScheduleActive_NonAlwaysAllowed_ShouldBlock()
    {
        // Arrange — lock schedule Mon 22:00-07:00, testing 23:00
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "bedtime", Days = [DayOfWeek.MON], From = "22:00", To = "07:00", Action = ScheduleAction.Lock }]);
        var usage = MakeUsage();
        var now = OnMonday(23, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(8, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_Step8_LockSchedule_AlwaysAllowed_ShouldContinue()
    {
        // Arrange
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "bedtime", Days = [DayOfWeek.MON], From = "22:00", To = "07:00", Action = ScheduleAction.Lock }],
            appPolicies: [new AppPolicy { PackageName = "whatsapp", State = AppPolicyState.AlwaysAllowed }]);
        var usage = MakeUsage();
        var now = OnMonday(23, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "whatsapp", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step8_LockSchedule_OutsideWindow_ShouldNotBlock()
    {
        // Arrange
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "bedtime", Days = [DayOfWeek.MON], From = "22:00", To = "07:00", Action = ScheduleAction.Lock }]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0); // Outside lock window

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step8_LockSchedule_GrantLifts_ShouldAllow()
    {
        // Arrange
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "bedtime", Days = [DayOfWeek.MON], From = "22:00", To = "07:00", Action = ScheduleAction.Lock }],
            grants: [MakeGrant("device", now: OnMonday(23, 0))]);
        var usage = MakeUsage();
        var now = OnMonday(23, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    // ── Step 9: App daily limit ─────────────────────────────────────────

    [Fact]
    public void Evaluar_Step9_AppLimitExceeded_ShouldBlock()
    {
        // Arrange — instagram limited to 30 min, already used 30 min
        var policy = MakePolicy(
            appPolicies: [new AppPolicy { PackageName = "instagram", State = AppPolicyState.Limited, DailyLimitMinutes = 30 }]);
        var usage = MakeUsage(appMinutes: new Dictionary<string, int> { { "instagram", 30 } });
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "instagram", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(9, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_Step9_AppLimitNotExceeded_ShouldContinue()
    {
        // Arrange — instagram used 20 min, limit 30 min
        var policy = MakePolicy(
            appPolicies: [new AppPolicy { PackageName = "instagram", State = AppPolicyState.Limited, DailyLimitMinutes = 30 }]);
        var usage = MakeUsage(appMinutes: new Dictionary<string, int> { { "instagram", 20 } });
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "instagram", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step9_AlwaysAllowed_NotBlockedByAppLimit()
    {
        // Arrange
        var policy = MakePolicy(
            appPolicies: [new AppPolicy { PackageName = "whatsapp", State = AppPolicyState.AlwaysAllowed }]);
        var usage = MakeUsage();
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "whatsapp", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step9_GrantLiftsAppLimit_ShouldAllow()
    {
        // Arrange — grant lifts steps 7-11
        var policy = MakePolicy(
            appPolicies: [new AppPolicy { PackageName = "instagram", State = AppPolicyState.Limited, DailyLimitMinutes = 30 }],
            grants: [MakeGrant("device", now: OnMonday(16, 0))]);
        var usage = MakeUsage(appMinutes: new Dictionary<string, int> { { "instagram", 30 } });
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "instagram", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    // ── Step 10: Category limit ────────────────────────────────────────

    [Fact]
    public void Evaluar_Step10_CategoryLimitExceeded_ShouldBlock()
    {
        // Arrange — games category limited to 60 min, used 60 min
        var policy = MakePolicy(
            categoryLimits: [new CategoryLimit { Category = "games", Minutes = 60 }],
            categoryAssignments: new Dictionary<string, string> { { "clashroyale", "games" }, { "minecraft", "games" } });
        var usage = MakeUsage(categoryMinutes: new Dictionary<string, int> { { "games", 60 } });
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "clashroyale", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(10, decision.ReasonCode);
        Assert.Contains("games", decision.ReasonText);
    }

    [Fact]
    public void Evaluar_Step10_CategoryLimitNotExceeded_ShouldContinue()
    {
        // Arrange
        var policy = MakePolicy(
            categoryLimits: [new CategoryLimit { Category = "games", Minutes = 60 }],
            categoryAssignments: new Dictionary<string, string> { { "clashroyale", "games" } });
        var usage = MakeUsage(categoryMinutes: new Dictionary<string, int> { { "games", 45 } });
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "clashroyale", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step10_AlwaysAllowed_NotBlockedByCategoryLimit()
    {
        // Arrange
        var policy = MakePolicy(
            categoryLimits: [new CategoryLimit { Category = "games", Minutes = 60 }],
            appPolicies: [new AppPolicy { PackageName = "whatsapp", State = AppPolicyState.AlwaysAllowed }],
            categoryAssignments: new Dictionary<string, string> { { "whatsapp", "social" } });
        var usage = MakeUsage();
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "whatsapp", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step10_NoCategoryAssignment_ShouldNotBlock()
    {
        // Arrange — chrome has no category assignment
        var policy = MakePolicy(
            categoryLimits: [new CategoryLimit { Category = "games", Minutes = 60 }]);
        var usage = MakeUsage();
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — no category, step 10 not applicable
        Assert.False(decision.IsBlocked);
    }

    // ── Step 11: Global screen time ─────────────────────────────────────

    [Fact]
    public void Evaluar_Step11_GlobalExceeded_ShouldBlock()
    {
        // Arrange — global limit 120 min, used 120 min
        var policy = MakePolicy(dailyScreenTime: 120);
        var usage = MakeUsage(globalMinutes: 120);
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.True(decision.IsBlocked);
        Assert.Equal(11, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_Step11_GlobalNotExceeded_ShouldContinue()
    {
        // Arrange
        var policy = MakePolicy(dailyScreenTime: 120);
        var usage = MakeUsage(globalMinutes: 90);
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step11_ExemptApp_ShouldBypassGlobal()
    {
        // Arrange — whatsapp is exempt
        var policy = MakePolicy(dailyScreenTime: 120);
        var usage = MakeUsage(globalMinutes: 120, exemptApps: new HashSet<string> { "whatsapp" });
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "whatsapp", usage, now, TimeZoneInfo.Utc);

        // Assert — exempt app bypasses global limit
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_Step11_GrantLiftsGlobal_ShouldAllow()
    {
        // Arrange
        var policy = MakePolicy(
            dailyScreenTime: 120,
            grants: [MakeGrant("device", now: OnMonday(16, 0))]);
        var usage = MakeUsage(globalMinutes: 120);
        var now = OnMonday(16, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    // ── Step 12: Default allow ──────────────────────────────────────────

    [Fact]
    public void Evaluar_Step12_NormalApp_ShouldAllow()
    {
        // Arrange — no restrictions apply
        var policy = MakePolicy();
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
        Assert.Null(decision.ReasonCode);
    }

    // ── Cross-cutting combinations ───────────────────────────────────────

    [Fact]
    public void Evaluar_CrossMidnight_ScheduleCrossesMidnight_InsideWindow_ShouldBlock()
    {
        // Arrange — lock schedule 22:00-07:00, testing 01:00 Monday night
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "bedtime", Days = [DayOfWeek.MON], From = "22:00", To = "07:00", Action = ScheduleAction.Lock }]);
        var usage = MakeUsage();
        var now = OnMonday(1, 0); // 01:00 Monday

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — inside midnight-crossing window
        Assert.True(decision.IsBlocked);
        Assert.Equal(8, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_CrossMidnight_ScheduleCrossesMidnight_OutsideWindow_ShouldAllow()
    {
        // Arrange
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "bedtime", Days = [DayOfWeek.MON], From = "22:00", To = "07:00", Action = ScheduleAction.Lock }]);
        var usage = MakeUsage();
        var now = OnMonday(10, 0); // 10:00 — outside midnight-crossing window

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_GrantEdgeCase_ExpiresExactlyNow_ShouldNotBlock()
    {
        // Arrange — grant expires AT 14:30 (exclusive per spec: now < ExpiresAt)
        // Use a date far from epoch to avoid edge cases
        var grantedAt = new DateTimeOffset(2026, 6, 1, 13, 30, 0, TimeSpan.Zero);
        var expiresAt = new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 6, 1, 14, 30, 0, TimeSpan.Zero); // Exactly at expiry

        var policy = new Policy
        {
            DeviceId = "test",
            Version = 1,
            DailyScreenTimeMinutes = 120, // Must set — default is 0 which blocks at step 11
            Grants = [new Grant
            {
                Id = "g1",
                RequestId = "r1",
                Scope = "device",
                Minutes = 30,
                GrantedAt = grantedAt,
                ExpiresAt = expiresAt,
                Source = GrantSource.ExtraTime,
            }],
        };
        var usage = MakeUsage();

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — at expiry (exclusive), grant not active → step 12 allows
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_GrantEdgeCase_GrantedExactlyNow_ShouldAllow()
    {
        // Arrange — grant starts AT 14:00 (inclusive per spec: GrantedAt <= now)
        var grantedAt = new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero);
        var expiresAt = new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero);

        var policy = new Policy
        {
            DeviceId = "test",
            Version = 1,
            Grants = [new Grant
            {
                Id = "g1",
                RequestId = "r1",
                Scope = "device",
                Minutes = 30,
                GrantedAt = grantedAt,
                ExpiresAt = expiresAt,
                Source = GrantSource.ExtraTime,
            }],
        };
        var usage = MakeUsage();

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert — at grant start (inclusive), grant active
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_TimeZone_TokyoZone_ShouldEvaluateCorrectly()
    {
        // Arrange — lock schedule Mon 22:00-07:00 local Tokyo.
        // In Tokyo (UTC+9), UTC 13:00 = Tokyo 22:00 (Monday).
        // This is inside the 22:00-07:00 window.
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "bedtime", Days = [DayOfWeek.MON], From = "22:00", To = "07:00", Action = ScheduleAction.Lock }]);
        var usage = MakeUsage();

        // UTC 13:00 = Tokyo 22:00 Monday — inside window
        var utc1300Monday = new DateTimeOffset(2026, 6, 1, 13, 0, 0, TimeSpan.Zero); // Monday UTC

        // Act — at UTC 13:00 (Tokyo 22:00, Monday)
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, utc1300Monday, Tokyo);

        // Assert — inside lock window
        Assert.True(decision.IsBlocked);
        Assert.Equal(8, decision.ReasonCode);
    }

    [Fact]
    public void Evaluar_NoAppPolicy_ShouldDefaultAllow()
    {
        // Arrange — chrome has no specific policy
        var policy = MakePolicy();
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "chrome", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_AllowedApp_InAllowOnly_ShouldAllow()
    {
        // Arrange
        var policy = MakePolicy(
            schedules: [new Schedule { Id = "hw", Days = [DayOfWeek.MON], From = "13:00", To = "15:00", Action = ScheduleAction.AllowOnly, AllowList = ["msedge"] }]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "msedge", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_ComplexScenario_DowntimeWithAlwaysAllowed_ShouldAllow()
    {
        // Arrange — downtime + always_allowed app
        var policy = MakePolicy(
            deviceState: DeviceState.Downtime,
            appPolicies: [new AppPolicy { PackageName = "whatsapp", State = AppPolicyState.AlwaysAllowed }]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act
        var decision = RulesEngine.Evaluar(policy, "whatsapp", usage, now, TimeZoneInfo.Utc);

        // Assert
        Assert.False(decision.IsBlocked);
    }

    [Fact]
    public void Evaluar_MultipleSchedules_MatchingDay_ShouldEvaluateAll()
    {
        // Arrange — two schedules active at same time, lock takes precedence
        var policy = MakePolicy(
            schedules:
            [
                new Schedule { Id = "lock", Days = [DayOfWeek.MON], From = "13:00", To = "15:00", Action = ScheduleAction.Lock },
                new Schedule { Id = "allow", Days = [DayOfWeek.MON], From = "13:00", To = "15:00", Action = ScheduleAction.AllowOnly, AllowList = ["chrome"] },
            ]);
        var usage = MakeUsage();
        var now = OnMonday(14, 0);

        // Act — step 4 checks allow_only first (order in spec: allow_only before lock)
        var decision = RulesEngine.Evaluar(policy, "msedge", usage, now, TimeZoneInfo.Utc);

        // Assert — msedge not in allow list → blocked by step 4
        Assert.True(decision.IsBlocked);
        Assert.Equal(4, decision.ReasonCode);
    }

    // ── Helper ──────────────────────────────────────────────────────────

    private static Grant MakeGrant(string scope, DateTimeOffset now, DateTimeOffset? expiresAt = null)
    {
        return new Grant
        {
            Id = "g1",
            RequestId = "r1",
            Scope = scope,
            Minutes = 30,
            GrantedAt = now.AddMinutes(-30),
            ExpiresAt = expiresAt ?? now.AddMinutes(30),
            Source = GrantSource.ExtraTime,
        };
    }
}
