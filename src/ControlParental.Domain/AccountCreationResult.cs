// <copyright file="AccountCreationResult.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Result of a child account creation or conversion operation.
/// </summary>
public sealed class AccountCreationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccountCreationResult"/> class.
    /// </summary>
    /// <param name="success">Whether the operation succeeded.</param>
    /// <param name="username">The username of the account, if created or converted.</param>
    /// <param name="errorMessage">Error message if the operation failed.</param>
    /// <param name="requiresElevation">Whether the operation requires elevation (admin).</param>
    public AccountCreationResult(
        bool success,
        string? username = null,
        string? errorMessage = null,
        bool requiresElevation = false)
    {
        this.Success = success;
        this.Username = username;
        this.ErrorMessage = errorMessage;
        this.RequiresElevation = requiresElevation;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets the username of the account, if created or converted.
    /// </summary>
    public string? Username { get; }

    /// <summary>
    /// Gets the error message if the operation failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets a value indicating whether the operation requires elevation.
    /// </summary>
    /// <summary>
    /// Gets a value indicating whether the operation requires elevation.
    /// </summary>
    public bool RequiresElevation { get; }

    /// <summary>
    /// Gets a value indicating whether the operation requires elevation.
    /// Alias for <see cref="RequiresElevation"/>. Used in factory methods.
    /// </summary>
    public bool NeedsElevation => this.RequiresElevation;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static AccountCreationResult Succeeded(string username)
        => new(success: true, username: username);

    /// <summary>
    /// Creates a failed result that requires elevation (parent must act).
    /// </summary>
    public static AccountCreationResult NeedsAdminElevation(string message)
        => new(success: false, errorMessage: message, requiresElevation: true);

    /// <summary>
    /// Creates a failed result (without requiring elevation).
    /// </summary>
    public static AccountCreationResult Failed(string message)
        => new(success: false, errorMessage: message);
}
