// <copyright file="OverlayPersistenceManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Diagnostics;
using ControlParental.Domain;

/// <summary>
/// T09 — Implementation of overlay persistence manager.
/// Manages the persistent overlay that reappears after session unlock
/// when device_state==locked.
/// </summary>
public sealed class OverlayPersistenceManager : IOverlayPersistenceManager
{
    // ── State ──────────────────────────────────────────────────────────

    private bool isPersistentOverlayActive;
    private string? persistentReason;
    private string? persistentCtaLabel;

    /// <summary>
    /// Lock object for thread safety.
    /// </summary>
    private readonly object lockObj = new();

    // ── IOverlayPersistenceManager ───────────────────────────────────

    /// <inheritdoc />
    public bool IsPersistentOverlayActive
    {
        get
        {
            lock (this.lockObj)
            {
                return this.isPersistentOverlayActive;
            }
        }
    }

    /// <inheritdoc />
    public void EnablePersistentOverlay(string reason, string? ctaLabel = null)
    {
        lock (this.lockObj)
        {
            this.isPersistentOverlayActive = true;
            this.persistentReason = reason ?? string.Empty;
            this.persistentCtaLabel = ctaLabel;

            Debug.WriteLine(
                $"[OverlayPersistenceManager] Persistent overlay enabled: {reason}");
        }
    }

    /// <inheritdoc />
    public void DisablePersistentOverlay()
    {
        lock (this.lockObj)
        {
            this.isPersistentOverlayActive = false;
            this.persistentReason = null;
            this.persistentCtaLabel = null;

            Debug.WriteLine("[OverlayPersistenceManager] Persistent overlay disabled.");
        }
    }

    /// <inheritdoc />
    public void OnSessionUnlocked(Action<string, string?> showOverlayAction)
    {
        lock (this.lockObj)
        {
            if (!this.isPersistentOverlayActive)
            {
                Debug.WriteLine(
                    "[OverlayPersistenceManager] Session unlocked, but persistent overlay not active.");
                return;
            }

            if (showOverlayAction == null)
            {
                Debug.WriteLine(
                    "[OverlayPersistenceManager] Cannot restore overlay: action is null.");
                return;
            }

            // Re-show the overlay with the persistent reason
            var reason = this.persistentReason ?? string.Empty;
            var ctaLabel = this.persistentCtaLabel;

            Debug.WriteLine(
                $"[OverlayPersistenceManager] Restoring persistent overlay: {reason}");

            showOverlayAction(reason, ctaLabel);
        }
    }

    /// <inheritdoc />
    public string? PersistentOverlayReason
    {
        get
        {
            lock (this.lockObj)
            {
                return this.persistentReason;
            }
        }
    }

    /// <inheritdoc />
    public string? PersistentOverlayCtaLabel
    {
        get
        {
            lock (this.lockObj)
            {
                return this.persistentCtaLabel;
            }
        }
    }
}
