// <copyright file="AclHardener.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;


/// <summary>
/// Windows implementation of <see cref="IAclHardener"/>.
/// Sets ACLs to deny write/delete to the Users group on folders,
/// registry keys, and the service binary.
/// </summary>
public sealed class AclHardener : IAclHardener
{
    private static readonly SecurityIdentifier UsersGroup =
        new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

    /// <inheritdoc />
    public Task<bool> HardenAgentFolderAsync(
        string agentFolder,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => this.SetDenyWriteDeleteAcl(agentFolder), cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HardenDataFolderAsync(
        string dataFolder,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() => this.SetDenyWriteDeleteAcl(dataFolder), cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HardenRegistryKeyAsync(
        string serviceRegistryKey,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        serviceRegistryKey,
                        writable: true);

                    if (key == null)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AclHardener] Registry key not found: {serviceRegistryKey}");
                        return false;
                    }

                    var rs = key.GetAccessControl();
                    var rule = new RegistryAccessRule(
                        UsersGroup,
                        RegistryRights.WriteKey | RegistryRights.Delete,
                        InheritanceFlags.None,
                        PropagationFlags.None,
                        AccessControlType.Deny);

                    rs.AddAccessRule(rule);
                    key.SetAccessControl(rs);

                    return true;
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Elevation required: the service (LocalSystem) should be able to set ACLs
                    System.Diagnostics.Debug.WriteLine(
                        $"[AclHardener] Cannot set registry ACL (requires elevation): {ex.Message}");
                    return false;
                }
                catch (IOException ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AclHardener] Registry I/O error: {ex.Message}");
                    return false;
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HardenServiceBinaryAsync(
        string serviceExePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                if (!File.Exists(serviceExePath))
                {
                    return false;
                }

                try
                {
                    var fileInfo = new FileInfo(serviceExePath);
                    var fs = fileInfo.GetAccessControl();

                    // Deny delete and change permissions to Users
                    var deleteRule = new FileSystemAccessRule(
                        UsersGroup,
                        FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles,
                        AccessControlType.Deny);

                    var changeRule = new FileSystemAccessRule(
                        UsersGroup,
                        FileSystemRights.ChangePermissions,
                        AccessControlType.Deny);

                    fs.AddAccessRule(deleteRule);
                    fs.AddAccessRule(changeRule);

                    fileInfo.SetAccessControl(fs);
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HardenAllAsync(
        string agentFolder,
        string dataFolder,
        string serviceRegistryKey,
        string serviceExePath,
        CancellationToken cancellationToken = default)
    {
        var agentResult = await this.HardenAgentFolderAsync(agentFolder, cancellationToken);
        var dataResult = await this.HardenDataFolderAsync(dataFolder, cancellationToken);
        var registryResult = await this.HardenRegistryKeyAsync(serviceRegistryKey, cancellationToken);
        var binaryResult = await this.HardenServiceBinaryAsync(serviceExePath, cancellationToken);

        // All operations must succeed for a fully hardened installation
        return agentResult && dataResult && registryResult && binaryResult;
    }

    private bool SetDenyWriteDeleteAcl(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return false;
        }

        try
        {
            if (Directory.Exists(path))
            {
                var dirInfo = new DirectoryInfo(path);
                var ds = dirInfo.GetAccessControl();

                // Deny delete (but NOT write/create) to the Users group on the folder itself.
                // Write is needed so the service (running as admin or LocalSystem) can create
                // the database file here. Individual files inside Secrets/ are hardened
                // separately with Write deny via the file-level rule below.
                var deleteRule = new FileSystemAccessRule(
                    UsersGroup,
                    FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles,
                    AccessControlType.Deny);

                var changeRule = new FileSystemAccessRule(
                    UsersGroup,
                    FileSystemRights.ChangePermissions,
                    AccessControlType.Deny);

                ds.AddAccessRule(deleteRule);
                ds.AddAccessRule(changeRule);

                dirInfo.SetAccessControl(ds);
                return true;
            }
            else
            {
                var fileInfo = new FileInfo(path);
                var fs = fileInfo.GetAccessControl();

                var writeRule = new FileSystemAccessRule(
                    UsersGroup,
                    FileSystemRights.Write,
                    AccessControlType.Deny);

                var deleteRule = new FileSystemAccessRule(
                    UsersGroup,
                    FileSystemRights.Delete,
                    AccessControlType.Deny);

                fs.AddAccessRule(writeRule);
                fs.AddAccessRule(deleteRule);

                fileInfo.SetAccessControl(fs);
                return true;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }
}

