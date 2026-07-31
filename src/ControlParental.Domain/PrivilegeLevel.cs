// <copyright file="PrivilegeLevel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Represents the privilege level of a user account.
/// Used to determine the enforcement level (T12).
/// </summary>
public enum PrivilegeLevel
{
    /// <summary>
    /// User is a standard (non-admin) account.
    /// This is the secure configuration for a child account.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// User is a local administrator.
    /// This is a DEGRADED state — the child can manipulate the system.
    /// </summary>
    Administrator = 1,

    /// <summary>
    /// Unable to determine the privilege level.
    /// </summary>
    Unknown = 2,
}
