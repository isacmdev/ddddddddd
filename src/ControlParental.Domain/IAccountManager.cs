// <copyright file="IAccountManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Manages child account creation, conversion to standard, and verification.
/// Consumed by the onboarding (T26).
/// </summary>
public interface IAccountManager
{
    /// <summary>
    /// Gets the list of existing user accounts on the machine.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of account names.</returns>
    Task<IReadOnlyList<string>> GetAccountsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of the child account that this service protects.
    /// </summary>
    /// <returns>The child account username, or null if not set.</returns>
    string? GetChildAccountName();

    /// <summary>
    /// Sets the name of the child account that this service protects.
    /// Stored persistently.
    /// </summary>
    /// <param name="username">The child account username.</param>
    void SetChildAccountName(string username);

    /// <summary>
    /// Verifies that the child account is a standard (non-admin) account.
    /// </summary>
    /// <param name="username">The account to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the account is standard; false if admin; throws on error.</returns>
    Task<bool> IsAccountStandardAsync(
        string username,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to convert an admin account to a standard account.
    /// This operation requires elevation (must be performed by the parent).
    /// </summary>
    /// <param name="username">The account to convert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A result indicating success, the username, or an error message.
    /// If <see cref="AccountCreationResult.RequiresElevation"/> is true,
    /// the parent must perform this action with elevation.
    /// </returns>
    Task<AccountCreationResult> ConvertToStandardAsync(
        string username,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new standard user account for the child.
    /// This operation requires elevation (must be performed by the parent).
    /// </summary>
    /// <param name="username">Desired username.</param>
    /// <param name="password">Password for the new account.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or an error requiring elevation.</returns>
    Task<AccountCreationResult> CreateStandardAccountAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
