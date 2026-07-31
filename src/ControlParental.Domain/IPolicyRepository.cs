// <copyright file="IPolicyRepository.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T03/T11 — Repository for policy access.
/// </summary>
public interface IPolicyRepository
{
    /// <summary>
    /// Gets the current active policy.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active policy or null if none exists.</returns>
    Task<Policy?> GetPolicyAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the locally stored policy version, or 0 if not found.
    /// </summary>
    Task<int> GetLocalVersionAsync(string deviceId, CancellationToken ct = default);

    /// <summary>
    /// Upserts the policy atomically: applies only if <paramref name="newVersion"/>
    /// is greater than the locally stored version. Downgrades are discarded.
    /// </summary>
    Task<bool> UpsertPolicyAsync(Policy policy, CancellationToken ct = default);
}
