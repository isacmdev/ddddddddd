// <copyright file="ForegroundWatcher.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent;

using System.Diagnostics;
using ControlParental.Domain;
using ControlParental.SessionAgent.Interop;

/// <summary>
/// T05 — Real foreground watcher using Win32 SetWinEventHook.
/// Runs a dedicated message pump thread to receive Windows event callbacks.
/// Emits ForegroundChanged(appId) when the foreground app changes.
/// Filters noise (shell, task switcher, overlay windows).
/// </summary>
public sealed class ForegroundWatcher : IForegroundWatcher, IDisposable
{
    // Process names that are noise and should be filtered
    private static readonly HashSet<string> NoiseProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Shell
        "explorer",
        "explorer.exe",

        // Task switcher / Start menu
        "dwm",
        "ctfmon",
        "sapi",
        "magnify",
        "narrator",
        "osk",
        "magnification",

        // System
        "SystemSettings",
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "TextInputHost",
        "SearchHost",
        "TaskHostW",
        "RuntimeBroker",
        "ApplicationFrameHost",

        // ControlParental's own overlay
        "ControlParental.SessionAgent",
        "ControlParental.SessionAgent.exe",

        // Notification center / Action center
        "SearchUI",
        "Notifications",
    };

    private readonly object lockObj = new();
    private readonly SynchronizationContext? syncContext;

    private IntPtr hookHandle = IntPtr.Zero;
    private Thread? messagePumpThread;
    private CancellationTokenSource? cts;
    private string? currentAppId;
    private ObservedProcessTarget? currentTarget;
    private bool isRunning;
    private bool isDisposed;

    public ForegroundWatcher()
    {
        // Capture the sync context so we can Post() back to the .NET thread
        this.syncContext = SynchronizationContext.Current;
    }

    /// <inheritdoc />
    public string? CurrentAppId
    {
        get
        {
            lock (this.lockObj)
            {
                return this.currentAppId;
            }
        }
    }

    /// <inheritdoc />
    public event Action<string>? ForegroundChanged;

    public ObservedProcessTarget? CurrentTarget
    {
        get
        {
            lock (this.lockObj)
            {
                return this.currentTarget;
            }
        }
    }

    public ObservedProcessTarget? GetCurrentTarget(string appId)
    {
        lock (this.lockObj)
        {
            return string.Equals(this.currentAppId, appId, StringComparison.Ordinal)
                ? this.currentTarget
                : null;
        }
    }

    internal static ForegroundChanged CreateObservation(
        string appId,
        int processId,
        int sessionId,
        DateTimeOffset startedAt) =>
        new(appId, new ObservedProcessTarget(processId, sessionId, startedAt));

    internal static bool ShouldPublishObservation(
        string? currentAppId,
        ObservedProcessTarget? currentTarget,
        ForegroundChanged observation) =>
        !string.Equals(currentAppId, observation.AppId, StringComparison.Ordinal) ||
        currentTarget != observation.Target;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (this.lockObj)
        {
            if (this.isRunning)
            {
                return Task.CompletedTask;
            }

            this.cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.isRunning = true;

            // Start the message pump thread
            this.messagePumpThread = new Thread(this.MessagePumpThreadProc)
            {
                Name = "ForegroundWatcher-MessagePump",
                IsBackground = true,
                Priority = ThreadPriority.Normal,
            };
            this.messagePumpThread.Start();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (this.lockObj)
        {
            if (!this.isRunning)
            {
                return;
            }

            this.isRunning = false;
            this.cts?.Cancel();

            // Unhook the event
            if (this.hookHandle != IntPtr.Zero)
            {
                Win32Api.UnhookWinEvent(this.hookHandle);
                this.hookHandle = IntPtr.Zero;
            }

            // Post WM_QUIT to the message pump thread to stop it
            if (this.messagePumpThread != null && this.messagePumpThread.IsAlive)
            {
                try
                {
                    Win32Api.PostThreadMessage(
                        (uint)this.messagePumpThread.ManagedThreadId,
                        Win32Api.WM_QUIT,
                        IntPtr.Zero,
                        IntPtr.Zero);
                }
                catch
                {
                    // Best effort
                }
            }
        }
    }

    private void MessagePumpThreadProc()
    {
        // Set up the WinEvent hook on this dedicated thread
        // The callback will be called on this thread, so we need to pump messages
        // to keep it alive and dispatch Windows events

        // Set up Windows message pump
        WinEventDelegate? callback = null;
        callback = new WinEventDelegate(this.OnWinEvent);

        // Hook EVENT_SYSTEM_FOREGROUND for all processes/threads (0, 0)
        this.hookHandle = Win32Api.SetWinEventHook(
            Win32Api.EVENT_SYSTEM_FOREGROUND,
            Win32Api.EVENT_SYSTEM_FOREGROUND,
            hmodWinEventProc: IntPtr.Zero,
            lpfnWinEventProc: callback,
            idProcess: 0,    // All processes
            idThread: 0,     // All threads
            dwFlags: Win32Api.WINEVENT_OUTOFCONTEXT);

        if (this.hookHandle == IntPtr.Zero)
        {
            // Hook failed — fall back to polling on this thread
            Debug.WriteLine("[ForegroundWatcher] SetWinEventHook failed, using fallback polling.");
            this.RunPollingFallback();
            return;
        }

        Debug.WriteLine("[ForegroundWatcher] Hook installed, running message pump.");

        // Message pump
        MSG msg;
        var cts = this.cts;
        while (cts != null && !cts.IsCancellationRequested)
        {
            var result = Win32Api.GetMessageW(out msg, hWnd: IntPtr.Zero, 0, 0);

            if (result == -1)
            {
                // Error
                break;
            }

            if (result == 0)
            {
                // WM_QUIT received
                break;
            }

            Win32Api.TranslateMessage(ref msg);
            Win32Api.DispatchMessageW(ref msg);
        }

        Debug.WriteLine("[ForegroundWatcher] Message pump exited.");
    }

    private void OnWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hWnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        // Filter out non-window events (only care about windows)
        if (idObject != 0 || idChild != 0)
        {
            return;
        }

        // Filter out our own process's windows (overlay, etc.)
        if (Environment.CurrentManagedThreadId == (int)dwEventThread)
        {
            // Same thread — check if it's our window
            var currentProcessId = (uint)Environment.ProcessId;
            _ = Win32Api.GetWindowThreadProcessId(hWnd, out var windowProcessId);
            if (windowProcessId == currentProcessId)
            {
                return; // Our own window
            }
        }

        this.ProcessForegroundWindow(hWnd);
    }

    private void ProcessForegroundWindow(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            return;
        }

        // Get the process that owns this window
        var threadId = Win32Api.GetWindowThreadProcessId(hWnd, out var processId);
        if (processId == 0 || threadId == 0)
        {
            return;
        }

        // Get the process path
        var processPath = AppIdentityResolver.GetProcessPathById(processId);
        if (string.IsNullOrEmpty(processPath))
        {
            return;
        }

        // Extract process name for filtering
        var processName = Path.GetFileName(processPath);

        // Filter noise
        if (IsNoiseProcess(processName))
        {
            return;
        }

        // Resolve to AppId
        var appId = AppIdentityResolver.Resolve(processPath);
        ObservedProcessTarget? target = null;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            target = CreateObservation(
                appId,
                process.Id,
                process.SessionId,
                process.StartTime.ToUniversalTime()).Target;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[ForegroundWatcher] Exact process identity unavailable: {ex.Message}");
        }

        var observation = new ForegroundChanged(appId, target);

        // Check if the canonical identity or exact process instance actually changed.
        string? previousAppId;
        lock (this.lockObj)
        {
            previousAppId = this.currentAppId;
            if (!ShouldPublishObservation(this.currentAppId, this.currentTarget, observation))
            {
                return;
            }

            this.currentAppId = observation.AppId;
            this.currentTarget = observation.Target;
        }

        Debug.WriteLine($"[ForegroundWatcher] Foreground changed: {previousAppId} → {appId}");

        // Raise the event on the .NET sync context
        this.RaiseForegroundChanged(appId);
    }

    private void RunPollingFallback()
    {
        var cts = this.cts;
        if (cts == null)
        {
            return;
        }

        string? lastAppId = null;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                var appId = AppIdentityResolver.GetCurrentForegroundAppId();

                if (!string.Equals(appId, lastAppId, StringComparison.Ordinal) &&
                    !IsNoiseProcess(Path.GetFileName(appId)))
                {
                    lock (this.lockObj)
                    {
                        lastAppId = appId;
                        this.currentAppId = appId;
                        this.currentTarget = null;
                    }

                    this.RaiseForegroundChanged(appId);
                }

                Thread.Sleep(500); // Poll every 500ms
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ForegroundWatcher] Polling error: {ex.Message}");
                Thread.Sleep(1000);
            }
        }
    }

    private void RaiseForegroundChanged(string appId)
    {
        var handler = this.ForegroundChanged;
        if (handler == null)
        {
            return;
        }

        if (this.syncContext != null)
        {
            // Post back to the .NET sync context (main thread)
            this.syncContext.Post(_ => handler(appId), null);
        }
        else
        {
            // No sync context — invoke directly (may cause issues)
            try
            {
                handler(appId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ForegroundWatcher] Event handler error: {ex.Message}");
            }
        }
    }

    private static bool IsNoiseProcess(string processName)
    {
        if (string.IsNullOrEmpty(processName))
        {
            return true;
        }

        // Remove .exe suffix for comparison
        var name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName[..^4]
            : processName;

        return NoiseProcessNames.Contains(name) ||
               NoiseProcessNames.Contains(processName);
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        this.Stop();
        this.cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
