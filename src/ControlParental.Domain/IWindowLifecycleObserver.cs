// <copyright file="IWindowLifecycleObserver.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T21 — Abstracts window lifecycle so we can test without WinUI.
/// Allows the realtime subscriber to connect only when the UI window is in foreground.
/// </summary>
public interface IWindowLifecycleObserver
{
    /// <summary>
    /// Gets whether the window is currently in foreground.
    /// </summary>
    bool IsInForeground { get; }

    /// <summary>
    /// Event fired when window goes to foreground.
    /// </summary>
    event EventHandler? EnteredForeground;

    /// <summary>
    /// Event fired when window goes to background.
    /// </summary>
    event EventHandler? EnteredBackground;
}
