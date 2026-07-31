namespace ControlParental.Domain;

/// <summary>
/// Represents the enforcement level of the system.
/// </summary>
public enum EnforcementLevel
{
    /// <summary>
    /// Unknown - not yet evaluated.
    /// </summary>
    Unknown = -1,

    /// <summary>
    /// Standard enforcement with user-mode blocking.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Managed enforcement with preventive controls (WDAC/AppLocker/MDM).
    /// </summary>
    Managed = 1,

    /// <summary>
    /// Degraded - missing critical foundation.
    /// </summary>
    Degraded = 2
}

/// <summary>
/// Possible states for a device.
/// </summary>
public enum DeviceState
{
    Active,
    Locked,
    Downtime
}

/// <summary>
/// Possible states for an app policy.
/// </summary>
public enum AppPolicyState
{
    Allowed,
    Blocked,
    Limited,
    AlwaysAllowed
}

/// <summary>
/// Possible actions for a schedule.
/// </summary>
public enum ScheduleAction
{
    Lock,
    AllowOnly
}

/// <summary>
/// Possible sources for grants.
/// </summary>
public enum GrantSource
{
    ExtraTime,
    Reward,
    Manual
}
