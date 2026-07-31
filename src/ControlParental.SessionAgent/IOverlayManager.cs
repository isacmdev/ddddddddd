// <copyright file="IOverlayManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent;

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
/// T08 — Implementation of IOverlayManager using Win32 overlay window.
/// </summary>
public sealed class OverlayManager : IOverlayManager, IDisposable
{
    // ── Dependencies ────────────────────────────────────────────────────

    private readonly OverlayWindow overlayWindow;

    // ── State ──────────────────────────────────────────────────────────

    private bool isOverlayVisible;
    private bool disposed;

    // ── Public Events ──────────────────────────────────────────────────

    /// <summary>
    /// Event raised when the user clicks the CTA button on the overlay.
    /// </summary>
    public event Action? CtaClicked;

    // ── Constructor ───────────────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayManager"/> class.
    /// </summary>
    public OverlayManager()
    {
        this.overlayWindow = new OverlayWindow();
        this.isOverlayVisible = false;
    }

    // ── IOverlayManager ───────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsOverlayVisible
    {
        get
        {
            lock (this.overlayWindow)
            {
                return this.isOverlayVisible;
            }
        }
    }

    /// <inheritdoc />
    public void ShowOverlay(string reason, string? ctaLabel = null)
    {
        if (this.disposed)
        {
            return;
        }

        lock (this.overlayWindow)
        {
            this.isOverlayVisible = true;
        }

        // Set up CTA callback
        Action? ctaCallback = null;
        if (!string.IsNullOrEmpty(ctaLabel))
        {
            ctaCallback = () => this.CtaClicked?.Invoke();
        }

        // Show the overlay window
        this.overlayWindow.Show(reason ?? string.Empty, ctaLabel, ctaCallback);

        System.Diagnostics.Debug.WriteLine(
            $"[OverlayManager] Showing overlay: {reason} (CTA: {ctaLabel})");
    }

    /// <inheritdoc />
    public void HideOverlay()
    {
        if (this.disposed)
        {
            return;
        }

        this.overlayWindow.Hide();
        lock (this.overlayWindow)
        {
            this.isOverlayVisible = false;
        }

        System.Diagnostics.Debug.WriteLine("[OverlayManager] Hiding overlay.");
    }

    /// <inheritdoc />
    public void ShowWarning(int minutesRemaining)
    {
        if (this.disposed)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[OverlayManager] Showing warning: {minutesRemaining} min remaining.");

        // Mark as visible when showing warning overlay
        lock (this.overlayWindow)
        {
            this.isOverlayVisible = true;
        }

        // For now, show a brief overlay as the warning
        // TODO: Consider showing a toast/notification instead of full overlay
        var warningMessage = minutesRemaining <= 5
            ? $"Se acabaron los minutos! Solicita mas tiempo."
            : $"Te quedan {minutesRemaining} minutos.";

        // Show a brief overlay for 3 seconds
        this.overlayWindow.Show(warningMessage, "Solicitar mas tiempo", () => this.CtaClicked?.Invoke());

        // Note: In production, this should be a toast notification
        // The full overlay should only appear when access is denied
    }

    // ── IDisposable ─────────────────────────────────────────────────────

    /// <summary>
    /// Disposes of the overlay manager and its resources.
    /// </summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.overlayWindow.Dispose();
        lock (this.overlayWindow)
        {
            this.isOverlayVisible = false;
        }

        GC.SuppressFinalize(this);
    }
}
