// <copyright file="ChildAccountStore.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// Persists the child account name to a protected file in ProgramData.
/// The file is protected by ACLs set by <see cref="AclHardener"/>.
/// </summary>
public sealed class ChildAccountStore : IChildAccountStore
{
    private const string AccountFileName = "child_account.txt";
    private readonly string accountFilePath;
    private readonly IAclHardener aclHardener;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChildAccountStore"/> class.
    /// </summary>
    /// <param name="dataFolder">Path to the ProgramData folder.</param>
    /// <param name="aclHardener">The ACL hardener used to protect the store.</param>
    public ChildAccountStore(string dataFolder, IAclHardener aclHardener)
    {
        this.aclHardener = aclHardener;
        this.accountFilePath = Path.Combine(
            dataFolder,
            AccountFileName);
    }

    /// <inheritdoc />
    public string? GetChildAccountName()
    {
        try
        {
            if (!File.Exists(this.accountFilePath))
            {
                return null;
            }

            var content = File.ReadAllText(this.accountFilePath);
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SetChildAccountName(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException(
                "Username cannot be null or empty.",
                nameof(username));
        }

        var directory = Path.GetDirectoryName(this.accountFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(this.accountFilePath, username.Trim());

        // Protect the file with ACLs (deny write to Users group) — best-effort
        try
        {
            this.aclHardener.HardenDataFolderAsync(directory!, CancellationToken.None)
                .Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // Best-effort: hardening failure should not block account setup
        }
    }

    /// <inheritdoc />
    public void ClearChildAccountName()
    {
        try
        {
            if (File.Exists(this.accountFilePath))
            {
                File.Delete(this.accountFilePath);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Cannot delete: file already protected
        }
        catch (IOException)
        {
            // Cannot delete: file locked or protected
        }
    }
}
