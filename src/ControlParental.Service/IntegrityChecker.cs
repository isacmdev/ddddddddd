// <copyright file="IntegrityChecker.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ControlParental.Service.Interop;

/// <summary>
/// T23 — Resultado de verificación de integridad local.
/// </summary>
public sealed record IntegrityCheckResult(
    bool IsSignatureValid,
    string BinaryHash,
    string ExecutablePath);

/// <summary>
/// T23 — Interfaz para verificación de firma Authenticode.
/// </summary>
public interface IWinTrustVerifier
{
    /// <summary>
    /// Verifica si un archivo tiene firma Authenticode válida.
    /// </summary>
    /// <param name="filePath">Ruta del archivo.</param>
    /// <returns>True si la firma es válida.</returns>
    bool IsSigned(string filePath);
}

/// <summary>
/// T23 — Interfaz para verificación de integridad binaria.
/// </summary>
public interface IIntegrityChecker
{
    /// <summary>
    /// Verifica la integridad del binario actual: firma Authenticode y hash SHA256.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Resultado con estado de firma, hash y ruta del ejecutable.</returns>
    Task<IntegrityCheckResult> CheckLocalIntegrityAsync(CancellationToken ct = default);
}

/// <summary>
/// T23 — Implementación de verificación de firma Authenticode usando WinVerifyTrust.
/// </summary>
public sealed class WinTrustVerifier : IWinTrustVerifier
{
    /// <inheritdoc />
    public bool IsSigned(string filePath)
    {
        try
        {
            using var trustInfo = new WinTrustFileInfo(filePath, WinTrust.GenericVerifyV2ActionId);
            return trustInfo.IsSigned;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// T23 — Implementación de verificación de integridad binaria.
/// Utiliza WinVerifyTrust para firma Authenticode y SHA256 para hash del binario.
/// </summary>
public sealed class IntegrityChecker : IIntegrityChecker
{
    private readonly IWinTrustVerifier winTrustVerifier;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrityChecker"/> class.
    /// </summary>
    /// <param name="winTrustVerifier">Verifier for Authenticode signatures.</param>
    public IntegrityChecker(IWinTrustVerifier winTrustVerifier)
    {
        this.winTrustVerifier = winTrustVerifier ?? throw new ArgumentNullException(nameof(winTrustVerifier));
    }

    /// <inheritdoc />
    public async Task<IntegrityCheckResult> CheckLocalIntegrityAsync(CancellationToken ct = default)
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine process executable path.");

        // R9: Anti-debug stub — log warning if debugger attached
        if (System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debug.WriteLine(
                "[IntegrityChecker] WARNING: Debugger is attached. This is not a tamper event.");
        }

        // R1: Verify Authenticode signature using WinVerifyTrust
        var isSignatureValid = await Task.Run(() => this.winTrustVerifier.IsSigned(executablePath), ct);

        // R2: Compute SHA256 hash of the binary
        var binaryHash = await Task.Run(() => ComputeSha256(executablePath), ct);

        return new IntegrityCheckResult(
            IsSignatureValid: isSignatureValid,
            BinaryHash: binaryHash,
            ExecutablePath: executablePath);
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
