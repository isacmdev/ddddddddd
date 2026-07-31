// <copyright file="IAclHardener.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Hardens ACLs on folders, registry keys, and the service to prevent
/// tampering by a standard user.
/// Consumed by the onboarding (T26) and startup (T10).
/// </summary>
public interface IAclHardener
{
    /// <summary>
    /// Hardens the agent installation folder under Program Files.
    /// Denies write/delete to the Users group.
    /// </summary>
    /// <param name="agentFolder">Path to the agent folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if hardening succeeded; false otherwise.</returns>
    Task<bool> HardenAgentFolderAsync(
        string agentFolder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hardens the ProgramData folder used by the service (SQLite, secrets store).
    /// Denies write/delete to the Users group.
    /// </summary>
    /// <param name="dataFolder">Path to the ProgramData folder.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if hardening succeeded; false otherwise.</returns>
    Task<bool> HardenDataFolderAsync(
        string dataFolder,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hardens the registry keys used by the service.
    /// Denies write/delete to the Users group.
    /// </summary>
    /// <param name="serviceRegistryKey">Registry key path for the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if hardening succeeded; false otherwise.</returns>
    Task<bool> HardenRegistryKeyAsync(
        string serviceRegistryKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hardens the service binary to prevent replacement.
    /// Ensures the service executable has restricted ACLs.
    /// </summary>
    /// <param name="serviceExePath">Path to the service executable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if hardening succeeded; false otherwise.</returns>
    Task<bool> HardenServiceBinaryAsync(
        string serviceExePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies all hardening measures for the service installation.
    /// Called during onboarding (T26) and at every service start (T10).
    /// </summary>
    /// <param name="agentFolder">Path to the agent folder.</param>
    /// <param name="dataFolder">Path to the ProgramData folder.</param>
    /// <param name="serviceRegistryKey">Registry key for the service.</param>
    /// <param name="serviceExePath">Path to the service executable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if all hardening succeeded; partial success returns false.</returns>
    Task<bool> HardenAllAsync(
        string agentFolder,
        string dataFolder,
        string serviceRegistryKey,
        string serviceExePath,
        CancellationToken cancellationToken = default);
}
