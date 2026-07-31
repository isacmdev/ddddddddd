// <copyright file="ConsentStatus.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T25 — Consent status for data collection.
/// </summary>
public enum ConsentStatus
{
    NotStarted = 0,
    Pending = 1,
    Granted = 2,
}

/// <summary>
/// T25 — Consent record containing status and metadata.
/// </summary>
/// <param name="Status">The current consent status.</param>
/// <param name="GrantedAt">When consent was granted (UTC).</param>
/// <param name="GrantedByDeviceId">The device ID that granted consent (null if local).</param>
public sealed record ConsentRecord(
    ConsentStatus Status,
    DateTimeOffset GrantedAt,
    string? GrantedByDeviceId);
