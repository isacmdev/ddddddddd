// <copyright file="Policy.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json.Serialization;

/// <summary>
/// The complete policy for a device.
/// This is the root type deserialized from the backend JSON.
/// Uses JsonStringEnumConverter so that enum values are (de)serialized as their
/// PascalCase string names (Active, Locked, Downtime, etc.).
/// The backend sends lowercase snake_case ("active", "locked") which requires
/// a custom converter or case-insensitive options — handled by using
/// the source-gen context with proper enum handling.
/// </summary>
[JsonSerializable(typeof(Policy), GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Schedule))]
[JsonSerializable(typeof(Window))]
[JsonSerializable(typeof(CategoryLimit))]
[JsonSerializable(typeof(AppPolicy))]
[JsonSerializable(typeof(Grant))]
[JsonSerializable(typeof(DayOfWeek))]
[JsonSerializable(typeof(AppPolicyState))]
[JsonSerializable(typeof(ScheduleAction))]
[JsonSerializable(typeof(GrantSource))]
[JsonSerializable(typeof(GrantScope))]
[JsonSerializable(typeof(DeviceState))]
[JsonSerializable(typeof(PrivilegeLevel))]
public sealed partial class PolicyJsonContext : JsonSerializerContext
{
}

/// <summary>
/// The root policy document containing all configuration for a device.
/// </summary>
public sealed record Policy
{
    /// <summary>
    /// Gets the unique device identifier.
    /// </summary>
    [JsonPropertyName("device_id")]
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the policy version. Higher versions override lower ones.
    /// The agent applies only if version > local_version.
    /// </summary>
    [JsonPropertyName("version")]
    public int Version { get; init; }

    /// <summary>Gets the server-authenticated content hash of this snapshot.</summary>
    [JsonPropertyName("snapshot_hash")]
    public string SnapshotHash { get; init; } = string.Empty;

    /// <summary>
    /// Gets the current device state: active, locked, downtime.
    /// </summary>
    [JsonPropertyName("device_state")]
    public DeviceState DeviceState { get; init; }

    /// <summary>
    /// Gets the daily screen time limit in minutes (global).
    /// </summary>
    [JsonPropertyName("daily_screen_time_minutes")]
    public int DailyScreenTimeMinutes { get; init; }

    /// <summary>
    /// Gets the list of time schedules.
    /// </summary>
    [JsonPropertyName("schedules")]
    public Schedule[] Schedules { get; init; } = [];

    /// <summary>
    /// Gets the list of category time limits.
    /// </summary>
    [JsonPropertyName("category_limits")]
    public CategoryLimit[] CategoryLimits { get; init; } = [];

    /// <summary>
    /// Gets the list of app-specific policies.
    /// </summary>
    [JsonPropertyName("app_policies")]
    public AppPolicy[] AppPolicies { get; init; } = [];

    /// <summary>
    /// Gets the map of package_name to category assignment.
    /// Used to compute category totals.
    /// </summary>
    [JsonPropertyName("category_assignments")]
    public Dictionary<string, string> CategoryAssignments { get; init; } = [];

    /// <summary>
    /// Gets the list of active grants.
    /// </summary>
    [JsonPropertyName("grants")]
    public Grant[] Grants { get; init; } = [];

    /// <summary>
    /// Validates the entire policy.
    /// Throws ArgumentException with details if any invariant is violated.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the policy is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.DeviceId))
        {
            throw new ArgumentException("Policy must have a non-empty device_id.", nameof(this.DeviceId));
        }

        if (this.Version <= 0)
        {
            throw new ArgumentException(
                $"Policy version must be positive. Got {this.Version}.",
                nameof(this.Version));
        }

        if (this.DailyScreenTimeMinutes < 0)
        {
            throw new ArgumentException(
                $"daily_screen_time_minutes cannot be negative. Got {this.DailyScreenTimeMinutes}.",
                nameof(this.DailyScreenTimeMinutes));
        }

        foreach (var schedule in this.Schedules)
        {
            schedule.Validate();
        }

        foreach (var limit in this.CategoryLimits)
        {
            limit.Validate();
        }

        foreach (var appPolicy in this.AppPolicies)
        {
            appPolicy.Validate();
        }

        foreach (var grant in this.Grants)
        {
            grant.Validate();
        }
    }

    /// <summary>
    /// Gets the app policy for a specific AppId.
    /// </summary>
    /// <param name="appId">The AppId to look up.</param>
    /// <returns>The AppPolicy or null if not found.</returns>
    public AppPolicy? GetAppPolicy(string appId)
    {
        return this.AppPolicies.FirstOrDefault(p =>
            p.PackageName.Equals(appId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the category limit for a specific category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>The CategoryLimit or null if not found.</returns>
    public CategoryLimit? GetCategoryLimit(string category)
    {
        return this.CategoryLimits.FirstOrDefault(l =>
            l.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets active grants at a specific time.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns>Array of active grants.</returns>
    public Grant[] GetActiveGrants(DateTimeOffset now)
    {
        return this.Grants.Where(g => g.IsActive(now)).ToArray();
    }

    /// <summary>
    /// Gets the schedule by ID.
    /// </summary>
    /// <param name="scheduleId">The schedule ID.</param>
    /// <returns>The Schedule or null if not found.</returns>
    public Schedule? GetSchedule(string scheduleId)
    {
        return this.Schedules.FirstOrDefault(s =>
            s.Id.Equals(scheduleId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the category for an AppId from the category_assignments map.
    /// </summary>
    /// <param name="appId">The AppId.</param>
    /// <returns>The category or null if not assigned.</returns>
    public string? GetCategory(string appId)
    {
        return this.CategoryAssignments.TryGetValue(appId, out var category)
            ? category
            : null;
    }
}
