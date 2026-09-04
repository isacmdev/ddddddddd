// <copyright file="BackendRealtimeIdentity.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// The current identity lease issued by the Service-owned identity authority.
/// The access token is held in process memory only and is not a UI wire contract.
/// </summary>
public sealed record RealtimeIdentitySnapshot(
    string AccessToken,
    string DeviceId,
    long Generation)
{
    /// <summary>Gets the backend-issued expiry of this lease.</summary>
    public DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>
/// Supplies the current Service-owned identity lease to a realtime transport.
/// Implementations must obtain the snapshot from the Service-owned identity
/// lifecycle and must not source it from user-editable UI state.
/// </summary>
public interface IRealtimeIdentityAuthority
{
    /// <summary>
    /// Gets the current identity lease, or <see langword="null"/> when the
    /// device is not paired, revoked, or the protected credential is unavailable.
    /// </summary>
    RealtimeIdentitySnapshot? Current { get; }

    /// <summary>
    /// Raised when the Service refreshes, rotates, or clears the lease.
    /// </summary>
    event EventHandler? Changed;
}
