// <copyright file="IScmController.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Controls the Windows Service Control Manager (SCM).
/// Used by T10 (persistence) and T12 (health monitoring).
/// </summary>
public interface IScmController
{
    /// <summary>
    /// Gets a value indicating whether the service is running.
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is running; false otherwise.</returns>
    Task<bool> IsServiceRunningAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the service if it is not running.
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service was started or is already running.</returns>
    Task<bool> StartServiceAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the service.
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service was stopped or is not running.</returns>
    Task<bool> StopServiceAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Configures the service failure actions (auto-restart on crash).
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if configuration succeeded.</returns>
    Task<bool> ConfigureFailureActionsAsync(
        string serviceName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the service startup type (auto, delayed-auto, manual, disabled).
    /// </summary>
    /// <param name="serviceName">Name of the service.</param>
    /// <param name="startupType">The startup type to set.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the startup type was set.</returns>
    Task<bool> SetStartupTypeAsync(
        string serviceName,
        string startupType,
        CancellationToken cancellationToken = default);
}
