// <copyright file="IPrivilegeInspector.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Inspects the privilege level of the child user account.
/// Exposes the result to T12 (Health Watcher).
/// </summary>
public interface IPrivilegeInspector
{
    /// <summary>
    /// Gets the privilege level of the current user or a specified user.
    /// </summary>
    /// <param name="username">Optional username. If null, uses the current process token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The privilege level of the account.</returns>
    Task<PrivilegeLevel> GetPrivilegeLevelAsync(
        string? username = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a value indicating whether the child account is a standard user.
    /// Convenience method: returns true only when <see cref="PrivilegeLevel.Standard"/>.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the child is a standard user (secure); false otherwise.</returns>
    Task<bool> IsChildStandardAsync(CancellationToken cancellationToken = default);
}
