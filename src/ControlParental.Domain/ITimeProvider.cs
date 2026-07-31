// <copyright file="ITimeProvider.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T04 — Injected time source for the rules engine and usage tracking.
/// Combines wall-clock time, monotonic time (for jump detection), and
/// server-provided date. All time-dependent operations in Domain receive
/// this through DI — never call DateTime.UtcNow directly.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Monotonic tick count for detecting clock jumps (64-bit, never resets).
    /// Use for comparing wall-clock vs monotonic consistency.
    /// </summary>
    long MonotonicNow { get; }

    /// <summary>
    /// Current wall-clock time in UTC.
    /// </summary>
    DateTimeOffset WallClockNow { get; }

    /// <summary>
    /// Current time zone (may change at runtime if user adjusts clock).
    /// </summary>
    TimeZoneInfo CurrentZone { get; }

    /// <summary>
    /// Server date (UTC date of the server/backend). Used to determine "today"
    /// for usage tracking and schedule evaluation. Injected by T18; falls back
    /// to local UTC date if not set (with uncertainty flag).
    /// </summary>
    /// <returns>The server date or null if unknown (fallback mode).</returns>
    DateOnly? ServerDate { get; }

    /// <summary>
    /// Whether the server date is a fallback (true = using local UTC date,
    /// not confirmed by server; treat as uncertain).
    /// </summary>
    bool IsServerDateUncertain { get; }

    /// <summary>
    /// Raised when a clock jump is detected (wall-clock jumped relative to
    /// monotonic time) or when the time zone changes.
    /// Implementations in Service wire this to SystemEvents.
    /// </summary>
    event EventHandler<TimeChangedEventArgs>? TimeChanged;

    /// <summary>
    /// Sets the server time offset in milliseconds (called by T18 after sync).
    /// </summary>
    void SetServerDate(long offsetMs);

    /// <summary>
    /// Gets the local time in the current zone.
    /// </summary>
    DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(this.WallClockNow, this.CurrentZone);

    /// <summary>
    /// Gets the day-of-week in the current zone for schedule evaluation.
    /// </summary>
    DayOfWeek LocalDayOfWeek => ConvertToDayOfWeek(this.LocalNow.DayOfWeek);

    /// <summary>
    /// Detects if a clock jump occurred since the last check.
    /// </summary>
    /// <returns>True if a jump was detected.</returns>
    bool DetectClockJump();

    private static Domain.DayOfWeek ConvertToDayOfWeek(global::System.DayOfWeek dow)
    {
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
}

/// <summary>
/// Event args for clock jump or time zone change detection.
/// </summary>
public sealed class TimeChangedEventArgs : EventArgs
{
    /// <summary>
    /// The reason for the change.
    /// </summary>
    public TimeChangeReason Reason { get; init; }

    /// <summary>
    /// The new time zone (null if reason is not ZoneChange).
    /// </summary>
    public TimeZoneInfo? NewZone { get; init; }

    /// <summary>
    /// Approximate delta of the clock jump (null if not a jump).
    /// </summary>
    public TimeSpan? JumpDelta { get; init; }
}

/// <summary>
/// Reason for a time change event.
/// </summary>
public enum TimeChangeReason
{
    /// <summary>Wall clock jumped relative to monotonic time.</summary>
    ClockJump,

    /// <summary>Time zone was changed by the user.</summary>
    ZoneChange,

    /// <summary>System time was changed (DateTime changed event).</summary>
    SystemTimeChanged,
}
