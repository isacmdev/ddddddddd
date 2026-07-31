// <copyright file="IForegroundWatcher.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent;

/// <summary>
/// T05 — Watches for foreground app changes in the child's session.
/// Emits ForegroundChanged(appId) when the foreground app changes.
/// </summary>
public interface IForegroundWatcher
{
    /// <summary>
    /// Gets the AppId of the current foreground app.
    /// </summary>
    string? CurrentAppId { get; }

    /// <summary>
    /// Event raised when the foreground app changes.
    /// The string is the canonical AppId (package name for MSIX, exe+publisher for Win32).
    /// </summary>
    event Action<string>? ForegroundChanged;

    /// <summary>
    /// Starts watching for foreground changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops watching for foreground changes.
    /// </summary>
    void Stop();
}
