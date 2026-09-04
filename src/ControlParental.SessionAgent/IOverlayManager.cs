// <copyright file="IOverlayManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent;

using System.Diagnostics;

/// <summary>
/// T08 — Manages the blocking overlay displayed to the child when access is denied.
/// </summary>
public interface IOverlayManager
{
    /// <summary>
    /// Gets a value indicating whether the overlay is currently visible.
    /// </summary>
    bool IsOverlayVisible { get; }

    /// <summary>
    /// Shows the blocking overlay with the specified reason.
    /// </summary>
    /// <param name="reason">The reason for the block (from the rules engine).</param>
    /// <param name="ctaLabel">Optional CTA button label.</param>
    void ShowOverlay(string reason, string? ctaLabel = null);

    /// <summary>
    /// Hides the blocking overlay.
    /// </summary>
    void HideOverlay();

    /// <summary>
    /// Shows a time warning notification.
    /// </summary>
    /// <param name="minutesRemaining">The number of minutes remaining.</param>
    void ShowWarning(int minutesRemaining);
}

/// <summary>
/// T08 — Implementation of <see cref="IOverlayManager"/> using two independent
/// Win32 surfaces: a blocking enforcement overlay (<see cref="OverlayWindow"/>)
/// and a non-blocking warning toast
/// (<see cref="OverlayWindow.WarningToastWindow"/>). Each surface owns its own
/// STA message-pump thread, so a warning can never degrade the enforcement
/// overlay's bounds, reason, CTA or visibility, and closing/hiding the UI never
/// deactivates policy (policy lives in the Service, not here).
/// </summary>
public sealed class OverlayManager : IOverlayManager, IDisposable
{
    // ── Dependencies / state ──────────────────────────────────────────

    private readonly OverlayWindow blockWindow;
    private readonly OverlayWindow.WarningToastWindow warningWindow;
    private readonly TimeSpan warningDuration;
    private readonly object stateLock = new();

    private bool isOverlayVisible;
    private bool isWarningVisible;
    private string? currentWarningMessage;
    private long warningGeneration;
    private bool disposed;

    // ── Public Events ──────────────────────────────────────────────────

    /// <summary>
    /// Event raised when the user activates the CTA button on the blocking overlay.
    /// This is the local seam only: it is intentionally not wired to any request
    /// or backend semantics.
    /// </summary>
    public event Action? CtaClicked;

    // ── Constructor ───────────────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayManager"/> class.
    /// </summary>
    public OverlayManager()
        : this(TimeSpan.FromSeconds(3))
    {
    }

    /// <summary>
    /// Initializes a new instance with a configurable warning auto-close duration.
    /// </summary>
    /// <param name="warningDuration">How long a warning toast stays visible.</param>
    /// <param name="showCursor">Optional seam over ShowCursor for tests (defaults to the real API).</param>
    /// <param name="blockBounds">Optional bounded geometry for the block surface (tests).</param>
    /// <param name="topmostReassertInterval">Optional topmost re-assert cadence (tests).</param>
    internal OverlayManager(
        TimeSpan warningDuration,
        Func<bool, bool>? showCursor = null,
        (int X, int Y, int Width, int Height)? blockBounds = null,
        TimeSpan? topmostReassertInterval = null)
    {
        if (warningDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(warningDuration));
        }

        this.warningDuration = warningDuration;
        this.blockWindow = blockBounds is { } bounds
            ? new OverlayWindow(bounds.X, bounds.Y, bounds.Width, bounds.Height, hideCursor: true, showCursor, topmostReassertInterval)
            : new OverlayWindow(hideCursor: true, showCursor: showCursor, topmostReassertInterval: topmostReassertInterval);
        this.warningWindow = new OverlayWindow.WarningToastWindow();
        this.isOverlayVisible = false;
    }

    // ── IOverlayManager ───────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsOverlayVisible
    {
        get
        {
            lock (this.stateLock)
            {
                return this.isOverlayVisible;
            }
        }
    }

    /// <summary>
    /// Gets whether a non-blocking warning is currently displayed.
    /// </summary>
    internal bool IsWarningVisible
    {
        get
        {
            lock (this.stateLock)
            {
                return this.isWarningVisible;
            }
        }
    }

    /// <summary>
    /// Gets the current warning text, if any.
    /// </summary>
    internal string? CurrentWarningMessage
    {
        get
        {
            lock (this.stateLock)
            {
                return this.currentWarningMessage;
            }
        }
    }

    /// <summary>
    /// Gets the native handle of the blocking enforcement surface.
    /// </summary>
    internal IntPtr BlockWindowHandle => this.blockWindow.Handle;

    /// <summary>
    /// Gets the native handle of the warning toast surface.
    /// </summary>
    internal IntPtr WarningWindowHandle => this.warningWindow.Handle;

    /// <summary>
    /// Gets the native handle of the CTA child button (Zero when no CTA label).
    /// </summary>
    internal IntPtr CtaButtonHandle => this.blockWindow.CtaHandle;

    /// <summary>
    /// Gets how many WM_PAINT operations reached the blocking surface.
    /// </summary>
    internal int BlockPaintCount => this.blockWindow.PaintCount;

    /// <summary>
    /// Gets how many WM_PAINT operations reached the warning toast.
    /// </summary>
    internal int WarningPaintCount => this.warningWindow.PaintCount;

    /// <summary>
    /// Gets how many times the block surface re-asserted HWND_TOPMOST.
    /// </summary>
    internal int TopmostReassertCount => this.blockWindow.TopmostReassertCount;

    /// <summary>
    /// Gets the current cursor-hide reference count owned by the block surface.
    /// </summary>
    internal int CursorHideRefs => this.blockWindow.CursorHideRefs;

    /// <summary>
    /// Gets whether keyboard focus currently sits on the CTA button.
    /// </summary>
    internal bool CtaFocused => this.blockWindow.IsCtaFocused;

    /// <summary>
    /// Gets the deterministic render plan of the blocking surface.
    /// </summary>
    internal OverlayWindow.OverlayRenderPlan GetBlockRenderPlan() => this.blockWindow.GetRenderPlan();

    /// <summary>
    /// Gets the current warning toast geometry.
    /// </summary>
    internal (int Width, int Height, uint Dpi) WarningGeometry => this.warningWindow.GetGeometry();

    /// <inheritdoc />
    public void ShowOverlay(string reason, string? ctaLabel = null)
    {
        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.isOverlayVisible = true;
        }

        Action? ctaCallback = null;
        if (!string.IsNullOrEmpty(ctaLabel))
        {
            ctaCallback = () => this.CtaClicked?.Invoke();
        }

        try
        {
            // The window marshals the physical show onto its owner message pump thread.
            this.blockWindow.Show(reason ?? string.Empty, ctaLabel, ctaCallback);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OverlayManager] ShowOverlay failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void HideOverlay()
    {
        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }
        }

        try
        {
            this.blockWindow.Hide();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OverlayManager] HideOverlay failed: {ex.Message}");
        }

        lock (this.stateLock)
        {
            this.isOverlayVisible = false;
        }
    }

    /// <inheritdoc />
    public void ShowWarning(int minutesRemaining)
    {
        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }
        }

        var message = BuildWarningMessage(Math.Max(0, minutesRemaining));
        long generation;
        lock (this.stateLock)
        {
            generation = ++this.warningGeneration;
            this.currentWarningMessage = message;
            this.isWarningVisible = true;
        }

        try
        {
            // The toast owns its auto-close; only the latest generation may clear state.
            this.warningWindow.Show(message, this.warningDuration, () => this.OnWarningAutoClosed(generation));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OverlayManager] ShowWarning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the localized warning text for a minutes-remaining value.
    /// </summary>
    internal static string BuildWarningMessage(int minutesRemaining)
    {
        return minutesRemaining switch
        {
            0 => "Se acabó el tiempo.",
            5 => "Te quedan 5 minutos.",
            10 => "Te quedan 10 minutos.",
            _ => $"Te quedan {minutesRemaining} minutos.",
        };
    }

    private void OnWarningAutoClosed(long generation)
    {
        lock (this.stateLock)
        {
            if (this.disposed || generation != this.warningGeneration)
            {
                return;
            }

            this.isWarningVisible = false;
            this.currentWarningMessage = null;
        }
    }

    // ── IDisposable ─────────────────────────────────────────────────────

    /// <summary>
    /// Disposes of the overlay manager and both native surfaces.
    /// </summary>
    public void Dispose()
    {
        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.warningGeneration++;
            this.isWarningVisible = false;
            this.currentWarningMessage = null;
        }

        try
        {
            this.warningWindow.Dispose();
            this.blockWindow.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OverlayManager] Dispose failed: {ex.Message}");
        }

        lock (this.stateLock)
        {
            this.isOverlayVisible = false;
        }

        GC.SuppressFinalize(this);
    }
}
