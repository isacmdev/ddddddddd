// <copyright file="FakeWindowLifecycleObserver.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

/// <summary>
/// T21 — Fake implementation of IWindowLifecycleObserver for testing.
/// Allows simulating foreground/background transitions in unit tests.
/// </summary>
public sealed class FakeWindowLifecycleObserver : Domain.IWindowLifecycleObserver
{
    /// <inheritdoc />
    public bool IsInForeground { get; private set; }

    /// <inheritdoc />
    public event EventHandler? EnteredForeground;

    /// <inheritdoc />
    public event EventHandler? EnteredBackground;

    /// <summary>
    /// Simulates the window entering the foreground.
    /// </summary>
    public void SimulateEnterForeground()
    {
        this.IsInForeground = true;
        this.EnteredForeground?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Simulates the window entering the background.
    /// </summary>
    public void SimulateEnterBackground()
    {
        this.IsInForeground = false;
        this.EnteredBackground?.Invoke(this, EventArgs.Empty);
    }
}
