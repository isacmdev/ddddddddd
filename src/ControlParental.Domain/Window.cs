// <copyright file="Window.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json.Serialization;

/// <summary>
/// A time window defining when an app is allowed or a schedule is active.
/// Reused by both schedules (schedule windows) and app-specific allowed_windows.
/// Hours are in HH:mm format. If From > To, the window crosses midnight.
/// </summary>
public sealed record Window
{
    /// <summary>
    /// Gets the days when this window is active.
    /// </summary>
    [JsonPropertyName("days")]
    public DayOfWeek[] Days { get; init; } = [];

    /// <summary>
    /// Gets the start time in HH:mm format (e.g., "15:00").
    /// </summary>
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    /// <summary>
    /// Gets the end time in HH:mm format (e.g., "20:00").
    /// If greater than From, the window is on the same day.
    /// If less than From, the window crosses midnight.
    /// </summary>
    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    /// <summary>
    /// Validates that the window has valid data.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the window is invalid.</exception>
    public void Validate()
    {
        if (this.Days == null || this.Days.Length == 0)
        {
            throw new ArgumentException("Window must have at least one day.", nameof(this.Days));
        }

        if (!IsValidTimeFormat(this.From))
        {
            throw new ArgumentException(
                $"Invalid start time format '{this.From}'. Expected HH:mm (e.g., '15:00').",
                nameof(this.From));
        }

        if (!IsValidTimeFormat(this.To))
        {
            throw new ArgumentException(
                $"Invalid end time format '{this.To}'. Expected HH:mm (e.g., '20:00').",
                nameof(this.To));
        }
    }

    /// <summary>
    /// Checks if a time string is in valid HH:mm format.
    /// </summary>
    private static bool IsValidTimeFormat(string time)
    {
        if (string.IsNullOrEmpty(time) || time.Length != 5)
        {
            return false;
        }

        if (time[2] != ':')
        {
            return false;
        }

        return int.TryParse(time.AsSpan(0, 2), out var hours) &&
               int.TryParse(time.AsSpan(3, 2), out var minutes) &&
               hours >= 0 && hours <= 23 &&
               minutes >= 0 && minutes <= 59;
    }

    /// <summary>
    /// Gets the start time as minutes since midnight.
    /// </summary>
    public int FromMinutes => ParseTimeToMinutes(this.From);

    /// <summary>
    /// Gets the end time as minutes since midnight.
    /// </summary>
    public int ToMinutes => ParseTimeToMinutes(this.To);

    /// <summary>
    /// Gets a value indicating whether this window crosses midnight.
    /// </summary>
    public bool CrossesMidnight => this.ToMinutes < this.FromMinutes;

    private static int ParseTimeToMinutes(string time)
    {
        if (!IsValidTimeFormat(time))
        {
            return 0;
        }

        var hours = int.Parse(time.AsSpan(0, 2));
        var minutes = int.Parse(time.AsSpan(3, 2));
        return hours * 60 + minutes;
    }
}
