// <copyright file="PrivilegeInspector.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// Windows implementation of <see cref="IPrivilegeInspector"/>.
/// Detects if the child user is a standard or administrator account
/// using WindowsIdentity and group membership.
/// Feeds T12 (Health Watcher) with DEGRADED state.
/// </summary>
public sealed class PrivilegeInspector : IPrivilegeInspector
{
    /// <inheritdoc />
    public Task<PrivilegeLevel> GetPrivilegeLevelAsync(
        string? username = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    System.Security.Principal.WindowsIdentity identity;

                    if (string.IsNullOrEmpty(username))
                    {
                        identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                    }
                    else
                    {
                        identity = new System.Security.Principal.WindowsIdentity(username);
                    }

                    var principal = new System.Security.Principal.WindowsPrincipal(identity);

                    // Check if the user is a member of the Administrators group
                    if (principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                    {
                        return PrivilegeLevel.Administrator;
                    }

                    return PrivilegeLevel.Standard;
                }
                catch (System.Security.Principal.IdentityNotMappedException)
                {
                    return PrivilegeLevel.Unknown;
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    // Cannot resolve username
                    System.Diagnostics.Debug.WriteLine(
                        $"[PrivilegeInspector] Cannot resolve user '{username}': {ex.Message}");
                    return PrivilegeLevel.Unknown;
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> IsChildStandardAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var level = this.GetPrivilegeLevelAsync(cancellationToken: cancellationToken).Result;
                return level == PrivilegeLevel.Standard;
            },
            cancellationToken);
    }
}
