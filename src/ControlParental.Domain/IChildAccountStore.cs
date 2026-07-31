// <copyright file="IChildAccountStore.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Persists the protected child account name across service restarts.
/// This is the only piece of user-specific configuration that lives
/// outside of the SQLite policy DB.
/// </summary>
public interface IChildAccountStore
{
    /// <summary>
    /// Gets the name of the protected child account.
    /// </summary>
    /// <returns>The username, or null if not configured.</returns>
    string? GetChildAccountName();

    /// <summary>
    /// Sets the name of the protected child account.
    /// </summary>
    /// <param name="username">The username to protect.</param>
    void SetChildAccountName(string username);

    /// <summary>
    /// Clears the child account name.
    /// </summary>
    void ClearChildAccountName();
}
