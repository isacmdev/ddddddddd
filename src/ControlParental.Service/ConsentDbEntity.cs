// <copyright file="ConsentDbEntity.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T25 — Stored consent entity in SQLite.
/// </summary>
public sealed class ConsentDbEntity
{
    /// <summary>
    /// Device ID (primary key).
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Consent status.
    /// </summary>
    public ConsentStatus Status { get; set; }

    /// <summary>
    /// When consent was granted (UTC).
    /// </summary>
    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>
    /// The device ID that granted consent (null if local).
    /// </summary>
    public string? GrantedByDeviceId { get; set; }
}
