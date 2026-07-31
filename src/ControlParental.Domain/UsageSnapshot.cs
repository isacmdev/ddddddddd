// <copyright file="UsageSnapshot.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Immutable snapshot of today's usage for the rules engine.
/// Derived by T03 from SQLite; the engine does not recalculate categories.
/// </summary>
/// <param name="AppMinutes">Minutes used today per app package name.</param>
/// <param name="CategoryMinutes">Minutes used today summed per category name.</param>
/// <param name="GlobalMinutes">Total minutes used today (excluding always_allowed apps).</param>
/// <param name="ExemptAppIds">Apps that are exempt from global screen time limits.</param>
public readonly record struct UsageSnapshot(
    IReadOnlyDictionary<string, int> AppMinutes,
    IReadOnlyDictionary<string, int> CategoryMinutes,
    int GlobalMinutes,
    IReadOnlySet<string> ExemptAppIds)
{
    /// <summary>
    /// Creates an empty usage snapshot with no usage recorded.
    /// </summary>
    public static UsageSnapshot Empty { get; } = new(
        AppMinutes: new Dictionary<string, int>(),
        CategoryMinutes: new Dictionary<string, int>(),
        GlobalMinutes: 0,
        ExemptAppIds: new HashSet<string>());
}
