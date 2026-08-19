// <copyright file="Extensions.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Interop;

using System.Security.Principal;

/// <summary>
/// Extension methods for Windows-specific types.
/// </summary>
public static class WindowsExtensions
{
    /// <summary>
    /// Gets the impersonation SID of the connected client.
    /// This is used to validate that the client is the expected user.
    /// </summary>
    /// <param name="pipeServer">The named pipe server stream.</param>
    /// <returns>The SID of the impersonated user, or null if not available.</returns>
    public static SecurityIdentifier? GetImpersonationUserSid(
        this System.IO.Pipes.NamedPipeServerStream pipeServer)
    {
        ArgumentNullException.ThrowIfNull(pipeServer);

        try
        {
            using var identity = WindowsIdentity.GetCurrent(ifImpersonating: true);
            return identity?.User;
        }
        catch
        {
            return null;
        }
    }
}
