// <copyright file="AccountManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

using System.Diagnostics;
using System.Security.Principal;

/// <summary>
/// Windows implementation of <see cref="IAccountManager"/>.
/// Manages child account creation, conversion to standard, and verification.
/// Requires elevation (LocalSystem) for account creation and conversion.
/// Consumed by the onboarding (T26).
/// </summary>
public sealed class AccountManager : IAccountManager
{
    private readonly IPrivilegeInspector privilegeInspector;
    private readonly IChildAccountStore accountStore;
    private readonly string programFilesPath;
    private readonly string dataFolderPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountManager"/> class.
    /// </summary>
    /// <param name="privilegeInspector">The privilege inspector.</param>
    /// <param name="accountStore">The child account store.</param>
    /// <param name="programFilesPath">Path to Program Files.</param>
    /// <param name="dataFolderPath">Path to ProgramData.</param>
    public AccountManager(
        IPrivilegeInspector privilegeInspector,
        IChildAccountStore accountStore,
        string programFilesPath,
        string dataFolderPath)
    {
        this.privilegeInspector = privilegeInspector ?? throw new ArgumentNullException(nameof(privilegeInspector));
        this.accountStore = accountStore ?? throw new ArgumentNullException(nameof(accountStore));
        this.programFilesPath = programFilesPath;
        this.dataFolderPath = dataFolderPath;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var accounts = new List<string>();

                try
                {
                    // Use WMI to enumerate local user accounts
                    var searcher = new System.Management.ManagementObjectSearcher(
                        "SELECT * FROM Win32_UserAccount WHERE LocalAccount = true");

                    foreach (System.Management.ManagementObject mo in searcher.Get())
                    {
                        var name = mo["Name"] as string;
                        if (!string.IsNullOrEmpty(name))
                        {
                            accounts.Add(name);
                        }
                    }
                }
                catch (System.Management.ManagementException ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AccountManager] WMI query failed: {ex.Message}");
                }

                return (IReadOnlyList<string>)accounts;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public string? GetChildAccountName()
    {
        return this.accountStore.GetChildAccountName();
    }

    /// <inheritdoc />
    public void SetChildAccountName(string username)
    {
        this.accountStore.SetChildAccountName(username);
    }

    /// <inheritdoc />
    public Task<bool> IsAccountStandardAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    var identity = new WindowsIdentity(username);
                    var principal = new WindowsPrincipal(identity);

                    // If the user is in Administrators group, they are NOT standard
                    return !principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch (IdentityNotMappedException)
                {
                    return false;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    return false;
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<AccountCreationResult> ConvertToStandardAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    return AccountCreationResult.Failed("Username cannot be empty.");
                }

                // Convert to standard using net.exe (LocalGroup)
                // Remove from Administrators group
                var removeResult = this.RunNetCommand(
                    $"localgroup Administrators \"{username}\" /delete");

                if (!removeResult.Success)
                {
                    // Check if already standard (not in group)
                    var isStandard = this.IsAccountStandardAsync(username, cancellationToken).Result;
                    if (isStandard)
                    {
                        return AccountCreationResult.Succeeded(username);
                    }

                    return AccountCreationResult.NeedsAdminElevation(
                        $"Cannot convert '{username}' to standard. " +
                        $"Run with administrator privileges: " +
                        $"net localgroup Administrators \"{username}\" /delete");
                }

                return AccountCreationResult.Succeeded(username);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<AccountCreationResult> CreateStandardAccountAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    return AccountCreationResult.Failed("Username cannot be empty.");
                }

                if (string.IsNullOrEmpty(password) || password.Length < 4)
                {
                    return AccountCreationResult.Failed(
                        "Password must be at least 4 characters.");
                }

                // Create user with net.exe
                var createResult = this.RunNetCommand(
                    $"user \"{username}\" \"{password}\" /add");

                if (!createResult.Success)
                {
                    // Check if user already exists
                    var existingAccounts = this.GetAccountsAsync(cancellationToken).Result;
                    if (existingAccounts.Contains(username, StringComparer.OrdinalIgnoreCase))
                    {
                        return AccountCreationResult.Succeeded(username);
                    }

                    return AccountCreationResult.NeedsAdminElevation(
                        $"Cannot create account '{username}'. " +
                        $"Run with administrator privileges: " +
                        $"net user \"{username}\" \"{password}\" /add");
                }

                return AccountCreationResult.Succeeded(username);
            },
            cancellationToken);
    }

    private (bool Success, string Output) RunNetCommand(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "net.exe",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, "Failed to start net.exe");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(TimeSpan.FromSeconds(30));

            var success = process.ExitCode == 0;
            var message = success ? output : $"{output}\n{error}";

            return (success, message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
