// <copyright file="IConsentService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T25 — Consent service interface for managing data collection consent.
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Gets the current consent status for the local device.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The consent record for the local device.</returns>
    Task<ConsentRecord> GetConsentStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Grants consent for data collection.
    /// </summary>
    /// <param name="grantedByDeviceId">Optional device ID that granted consent (for linked devices).</param>
    /// <param name="ct">Cancellation token.</param>
    Task GrantConsentAsync(string? grantedByDeviceId, CancellationToken ct = default);

    /// <summary>
    /// Gets a value indicating whether consent has been granted.
    /// </summary>
    bool IsConsentGranted { get; }
}
