// <copyright file="WindowLifecycleObserver.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.Domain;

/// <summary>
/// Product lifecycle port used by the realtime subscriber. The window host calls
/// the transition methods when its activation state changes.
/// </summary>
public sealed class WindowLifecycleObserver : IWindowLifecycleObserver
{
    private int foreground;

    public bool IsInForeground => Volatile.Read(ref this.foreground) != 0;

    public event EventHandler? EnteredForeground;

    public event EventHandler? EnteredBackground;

    public void EnterForeground()
    {
        if (Interlocked.Exchange(ref this.foreground, 1) == 0)
        {
            this.EnteredForeground?.Invoke(this, EventArgs.Empty);
        }
    }

    public void EnterBackground()
    {
        if (Interlocked.Exchange(ref this.foreground, 0) != 0)
        {
            this.EnteredBackground?.Invoke(this, EventArgs.Empty);
        }
    }
}
