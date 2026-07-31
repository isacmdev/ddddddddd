// <copyright file="IProtectedProcessReporter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Reports on the Protected Process Light (PPL) status of the service.
/// Best-effort: PPL requires the service binary to be signed with
/// a Microsoft-approved code signing certificate.
///
/// Used by T12 to document the enforcement posture.
/// </summary>
public interface IProtectedProcessReporter
{
    /// <summary>
    /// Gets a value indicating whether the service can run as a PPL.
    /// This is determined by the signing certificate, not by runtime code.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// True if the service binary is signed with a PPL-eligible certificate.
    /// </returns>
    Task<bool> IsPplCapableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a human-readable description of the PPL status and what
    /// it protects against.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A description of the PPL status.</returns>
    Task<string> GetStatusDescriptionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets documentation on how to enable PPL for the service.
    /// </summary>
    /// <returns>Documentation text with steps to enable PPL.</returns>
    string GetPplDocumentation();
}
