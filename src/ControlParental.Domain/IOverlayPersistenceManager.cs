// <copyright file="IOverlayPersistenceManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T09 — Manages overlay persistence across session lock/unlock cycles.
/// When device_state==locked, the overlay should reappear after session unlock.
/// </summary>
public interface IOverlayPersistenceManager
{
    /// <summary>
    /// Gets whether persistent overlay mode is active.
    /// </summary>
    bool IsPersistentOverlayActive { get; }

    /// <summary>
    /// Enables persistent overlay mode.
    /// The overlay will reappear when the session is unlocked.
    /// </summary>
    /// <param name="reason">The reason for the persistent overlay.</param>
    /// <param name="ctaLabel">Optional CTA label.</param>
    void EnablePersistentOverlay(string reason, string? ctaLabel = null);

    /// <summary>
    /// Disables persistent overlay mode.
    /// </summary>
    void DisablePersistentOverlay();

    /// <summary>
    /// Called when the session is unlocked.
    /// If persistent overlay is active, re-shows the overlay.
    /// </summary>
    /// <param name="showOverlayAction">Action to call to show the overlay.</param>
    void OnSessionUnlocked(Action<string, string?> showOverlayAction);

    /// <summary>
    /// Gets the current persistent overlay reason.
    /// </summary>
    string? PersistentOverlayReason { get; }

    /// <summary>
    /// Gets the current persistent overlay CTA label.
    /// </summary>
    string? PersistentOverlayCtaLabel { get; }
}
