// <copyright file="RulesEngine.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T02 — Rules engine: deterministic evaluation of policy against app and context.
/// Pure function, no hidden state, no global clock.
/// </summary>
public static class RulesEngine
{
    // Hard-coded whitelist: critical OS processes that must never be blocked.
    // These are process names (case-insensitive) that are essential for session
    // management, security, and accessibility. They are not policy-controlled.
    private static readonly HashSet<string> ExemptProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Session agent
        "ControlParental.SessionAgent",
        "ControlParental.SessionAgent.exe",

        // Critical Windows session processes
        "winlogon",
        "logonui",
        "csrss",
        "smss",
        "services",
        "lsass",
        "svchost",

        // UAC / consent dialog
        "consent",
        "consent.exe",
        "dwm",

        // Windows accessibility (built-in)
        "sapi",
        "ctfmon",
        "magnify",
        "narrator",
        "osk",
        "magnification",

        // Shell / explorer
        "explorer",
        "explorer.exe",

        // System processes that should never be blocked
        "system",
        "registry",
        "Memory Compression",
        "Registry",
    };

    // Human-readable reason texts (T25 copy catalog origin).
    // Format: reason code -> reason text.
    private static readonly Dictionary<int, string> ReasonTexts = new()
    {
        { 2, "dispositivo bloqueado" },
        { 3, "esta app está bloqueada" },
        { 4, "solo ciertas apps durante este horario" },
        { 5, "fuera del horario permitido" },
        // Reason 6 is PERMIT (no reason text)
        { 7, "hora de dormir" },
        { 8, "hora de dormir" },
        { 9, "se acabó el tiempo de esta app" },
        { 10, "se acabó el tiempo de {0}" }, // category name interpolated
        { 11, "se acabó el tiempo de hoy" },
    };

    /// <summary>
    /// Evaluates the policy for the given app at the given time.
    /// </summary>
    /// <param name="policy">The active policy.</param>
    /// <param name="appId">The package name of the app in foreground (e.g. "com.whatsapp").</param>
    /// <param name="usage">Today's usage snapshot (from T03).</param>
    /// <param name="now">Current time (wall-clock, from T04's ITimeProvider).</param>
    /// <param name="zonaHoraria">The time zone to evaluate windows in.</param>
    /// <returns>A <see cref="Decision"/> indicating allow or block with reason.</returns>
    public static Decision Evaluar(
        Policy policy,
        string appId,
        UsageSnapshot usage,
        DateTimeOffset now,
        TimeZoneInfo zonaHoraria)
    {
        // ── Step 1: Hard whitelist ──────────────────────────────────────────
        if (IsExemptProcess(appId))
        {
            return Decision.Allow;
        }

        // ── Step 2: Device locked ─────────────────────────────────────────
        if (policy.DeviceState == DeviceState.Locked)
        {
            return Block(2);
        }

        // ── Step 3: App explicitly blocked ────────────────────────────────
        var appPolicy = policy.GetAppPolicy(appId);
        if (appPolicy?.State == AppPolicyState.Blocked)
        {
            return Block(3);
        }

        // ── Step 4: Allow-only schedule active and app not in allow list ───
        var activeAllowOnly = GetActiveAllowOnlySchedule(policy, now, zonaHoraria);
        if (activeAllowOnly != null)
        {
            // Check if app is in the allow list
            if (appPolicy?.State == AppPolicyState.AlwaysAllowed)
            {
                // always_allowed exempts from allow_only restriction (per spec interpretation)
            }
            else if (!IsInAllowList(activeAllowOnly, appId))
            {
                return Block(4);
            }
        }

        // ── Step 5: App has allowed_windows and currently outside all ─────
        if (appPolicy?.AllowedWindows != null && appPolicy.AllowedWindows.Length > 0)
        {
            if (!IsWithinAnyWindow(now, zonaHoraria, appPolicy.AllowedWindows))
            {
                return Block(5);
            }
        }

        // ── Step 6: Active grant covering scope ───────────────────────────
        // Check if any active grant covers this app.
        // Grants lift steps 7-11 only; do NOT lift steps 2-5.
        // We check this here and carry the flag forward.
        var hasActiveGrant = policy.GetActiveGrants(now).Any(g =>
            g.Scope == "device" ||
            g.Scope == appId ||
            (g.Scope == "category" && GetAppCategory(policy, appId) != null));

        // ── Step 7: Device in downtime and app not always_allowed ───────────
        // always_allowed apps are exempt from this step
        // grants lift steps 7-11
        if (!hasActiveGrant && policy.DeviceState == DeviceState.Downtime)
        {
            if (appPolicy?.State != AppPolicyState.AlwaysAllowed)
            {
                return Block(7);
            }
        }

        // ── Step 8: Active lock schedule and not always_allowed ────────────
        // always_allowed apps are exempt from lock schedules
        // grants lift steps 7-11
        if (!hasActiveGrant)
        {
            var activeLockSchedule = GetActiveLockSchedule(policy, now, zonaHoraria);
            if (activeLockSchedule != null)
            {
                if (appPolicy?.State != AppPolicyState.AlwaysAllowed)
                {
                    return Block(8);
                }
            }
        }

        // ── Step 9: App exceeded its daily limit and not always_allowed ───
        // always_allowed apps are exempt from this step
        // grants lift steps 7-11, so skip if we have an active grant
        if (!hasActiveGrant && appPolicy?.State == AppPolicyState.Limited)
        {
            if (appPolicy.State != AppPolicyState.AlwaysAllowed && // always_allowed is already excluded
                appPolicy.DailyLimitMinutes.HasValue)
            {
                var appMinutes = usage.AppMinutes.GetValueOrDefault(appId, 0);
                if (appMinutes >= appPolicy.DailyLimitMinutes.Value)
                {
                    return Block(9);
                }
            }
        }

        // ── Step 10: Category exceeded its limit and not always_allowed ───
        // always_allowed apps are exempt from category limits
        // grants lift steps 7-11
        if (!hasActiveGrant)
        {
            var appCategory = GetAppCategory(policy, appId);
            if (appCategory != null)
            {
                var categoryLimit = policy.CategoryLimits
                    .FirstOrDefault(c => c.Category == appCategory);
                if (categoryLimit != null)
                {
                    var categoryMinutes = usage.CategoryMinutes.GetValueOrDefault(appCategory, 0);
                    if (categoryMinutes >= categoryLimit.Minutes)
                    {
                        return Block(10, categoryName: appCategory);
                    }
                }
            }

            // ── Step 11: Global screen time exceeded (non-exempt app) ──────────
            if (!usage.ExemptAppIds.Contains(appId))
            {
                if (usage.GlobalMinutes >= policy.DailyScreenTimeMinutes)
                {
                    return Block(11);
                }
            }
        }

        // ── Step 12: Default — allow ───────────────────────────────────────
        return Decision.Allow;
    }

    private static bool IsExemptProcess(string appId)
    {
        // appId can be a package name (e.g. "com.microsoft.windows") or
        // a process name (e.g. "explorer"). Check both forms.
        if (ExemptProcessNames.Contains(appId))
        {
            return true;
        }

        // Also check without .exe suffix
        var withoutExe = appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? appId[..^4]
            : appId;
        return ExemptProcessNames.Contains(withoutExe);
    }

    private static bool IsInAllowList(Schedule schedule, string appId)
    {
        if (schedule.AllowList == null || schedule.AllowList.Length == 0)
        {
            return false;
        }

        return schedule.AllowList.Contains(appId, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsWithinAnyWindow(
        DateTimeOffset now,
        TimeZoneInfo zonaHoraria,
        Window[] windows)
    {
        foreach (var window in windows)
        {
            if (IsWithinWindow(now, zonaHoraria, window))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithinWindow(
        DateTimeOffset now,
        TimeZoneInfo zonaHoraria,
        Window window)
    {
        // Check if today matches one of the allowed days
        var localNow = TimeZoneInfo.ConvertTime(now, zonaHoraria);
        var todayDow = ConvertToDayOfWeek(localNow.DayOfWeek);
        if (!window.Days.Contains(todayDow))
        {
            return false;
        }

        // Parse window times
        if (!TryParseTime(window.From, out var fromHour, out var fromMin) ||
            !TryParseTime(window.To, out var toHour, out var toMin))
        {
            return false;
        }

        var fromTime = new TimeOnly(fromHour, fromMin);
        var toTime = new TimeOnly(toHour, toMin);
        var nowTime = TimeOnly.FromDateTime(localNow.DateTime);

        if (window.CrossesMidnight)
        {
            // Window like 22:00 → 07:00
            return nowTime >= fromTime || nowTime < toTime;
        }
        else
        {
            return nowTime >= fromTime && nowTime < toTime;
        }
    }

    private static Schedule? GetActiveLockSchedule(
        Policy policy,
        DateTimeOffset now,
        TimeZoneInfo zonaHoraria)
    {
        foreach (var schedule in policy.Schedules)
        {
            if (schedule.Action == ScheduleAction.Lock && IsScheduleActive(schedule, now, zonaHoraria))
            {
                return schedule;
            }
        }

        return null;
    }

    private static Schedule? GetActiveAllowOnlySchedule(
        Policy policy,
        DateTimeOffset now,
        TimeZoneInfo zonaHoraria)
    {
        foreach (var schedule in policy.Schedules)
        {
            if (schedule.Action == ScheduleAction.AllowOnly && IsScheduleActive(schedule, now, zonaHoraria))
            {
                return schedule;
            }
        }

        return null;
    }

    private static bool IsScheduleActive(
        Schedule schedule,
        DateTimeOffset now,
        TimeZoneInfo zonaHoraria)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, zonaHoraria);
        var todayDow = ConvertToDayOfWeek(localNow.DayOfWeek);
        if (!schedule.Days.Contains(todayDow))
        {
            return false;
        }

        if (!TryParseTime(schedule.From, out var fromHour, out var fromMin) ||
            !TryParseTime(schedule.To, out var toHour, out var toMin))
        {
            return false;
        }

        var fromTime = new TimeOnly(fromHour, fromMin);
        var toTime = new TimeOnly(toHour, toMin);
        var nowTime = TimeOnly.FromDateTime(localNow.DateTime);

        if (schedule.CrossesMidnight)
        {
            return nowTime >= fromTime || nowTime < toTime;
        }
        else
        {
            return nowTime >= fromTime && nowTime < toTime;
        }
    }

    private static string? GetAppCategory(Policy policy, string appId)
    {
        return policy.CategoryAssignments.GetValueOrDefault(appId);
    }

    private static Domain.DayOfWeek ConvertToDayOfWeek(global::System.DayOfWeek dow)
    {
        // Map System.DayOfWeek to our Domain.DayOfWeek
        return dow switch
        {
            global::System.DayOfWeek.Monday => Domain.DayOfWeek.MON,
            global::System.DayOfWeek.Tuesday => Domain.DayOfWeek.TUE,
            global::System.DayOfWeek.Wednesday => Domain.DayOfWeek.WED,
            global::System.DayOfWeek.Thursday => Domain.DayOfWeek.THU,
            global::System.DayOfWeek.Friday => Domain.DayOfWeek.FRI,
            global::System.DayOfWeek.Saturday => Domain.DayOfWeek.SAT,
            global::System.DayOfWeek.Sunday => Domain.DayOfWeek.SUN,
            _ => Domain.DayOfWeek.MON,
        };
    }

    private static bool TryParseTime(string time, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;

        var parts = time.Split(':');
        if (parts.Length != 2)
        {
            return false;
        }

        return int.TryParse(parts[0], out hour) &&
               int.TryParse(parts[1], out minute) &&
               hour >= 0 && hour < 24 &&
               minute >= 0 && minute < 60;
    }

    private static Decision Block(int reasonCode, string? categoryName = null)
    {
        var text = ReasonTexts.GetValueOrDefault(reasonCode) ?? $"razón {reasonCode}";
        if (categoryName != null && text.Contains("{0}"))
        {
            text = string.Format(text, categoryName);
        }

        return Decision.Block(reasonCode, text);
    }
}
