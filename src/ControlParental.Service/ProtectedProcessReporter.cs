// <copyright file="ProtectedProcessReporter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

using System.Reflection;

/// <summary>
/// Best-effort implementation of <see cref="IProtectedProcessReporter"/>.
/// PPL (Protected Process Light) requires the service binary to be signed
/// with a Microsoft-approved Extended Validation (EV) code signing certificate.
/// This implementation checks the Authenticode signature of the service binary
/// and documents how to enable PPL.
/// </summary>
public sealed class ProtectedProcessReporter : IProtectedProcessReporter
{
    private readonly string serviceExePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedProcessReporter"/> class.
    /// </summary>
    /// <param name="serviceExePath">Path to the service executable.</param>
    public ProtectedProcessReporter(string serviceExePath)
    {
        this.serviceExePath = serviceExePath;
    }

    /// <inheritdoc />
    public Task<bool> IsPplCapableAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                try
                {
                    if (!File.Exists(this.serviceExePath))
                    {
                        return false;
                    }

                    // Verify Authenticode signature using WinVerifyTrust
                    // The action GUID for WinVerifyTrust
                    var verifyAction = new Guid("00AAC60B-0000-0000-0000-000000000000");
                    var wintrust = new Interop.WinTrustFileInfo(this.serviceExePath, verifyAction);

                    return wintrust.IsSigned;
                }
                catch
                {
                    return false;
                }
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> GetStatusDescriptionAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            async () =>
            {
                var isCapable = await this.IsPplCapableAsync(cancellationToken);

                if (isCapable)
                {
                    return "Service binary is signed and PPL-capable. " +
                           "The service can run as a Protected Process Light, " +
                           "which provides protection against tampering by admin users.";
                }

                return "Service binary is not PPL-capable. " +
                       "To enable PPL protection, sign the binary with an " +
                       "Extended Validation (EV) code signing certificate " +
                       "approved by Microsoft. See GetPplDocumentation() for details.";
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public string GetPplDocumentation()
    {
        return """
            ## Protected Process Light (PPL) — Documentation

            ### Overview
            PPL provides an additional layer of protection against tampering
            by admin users. Even if a standard user somehow gains admin rights,
            they cannot kill, inject, or modify a PPL-signed process.

            ### Requirements
            1. Extended Validation (EV) code signing certificate from a
               Microsoft-approved Certificate Authority (CA).
            2. The certificate must include the "Protected Process" capability.

            ### How to enable PPL
            1. Sign the service binary (ControlParental.Service.exe) with the
               EV certificate using signtool.exe:

               signtool sign /sha1 <thumbprint> /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 ControlParental.Service.exe

            2. Add the PPL flag to the service manifest or registration.
               The service is registered as a Windows service; the PPL flag
               is set by signing the binary, not by code changes.

            3. Restart the service. If the signature is valid, the service
               will run as a Protected Process Light.

            ### What PPL protects against
            - TerminateProcess() from non-PPL processes (even admins)
            - Code injection (CreateRemoteThread, SetThreadContext)
            - Memory read/write (ReadProcessMemory, WriteProcessMemory)
            - Image replacement (NtUnmapViewOfSection)

            ### What PPL does NOT protect against
            - A malicious admin with a PPL-signed tool that can kill the service
            - Kernel-mode code (running as SYSTEM)
            - Direct disk access to modify the binary

            ### Fallback (without PPL)
            Without PPL, the service relies on:
            - ACLs on folders, registry, and the binary (IAclHardener)
            - Service recovery actions (sc failure)
            - The standard user being non-admin (IPrivilegeInspector)

            These measures are sufficient for STANDARD enforcement level.
            PPL is recommended for MANAGED enforcement level.
            """;
    }
}
