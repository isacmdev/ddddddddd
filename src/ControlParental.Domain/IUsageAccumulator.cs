// <copyright file="IUsageAccumulator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T06 — Live usage counter that accumulates foreground time in SQLite.
/// Lives in the service (survives logout), emits warnings to the agent.
/// </summary>
public interface IUsageAccumulator
{
    /// <summary>
    /// Gets the AppId currently being tracked (may be null if no foreground).
    /// </summary>
    string? CurrentAppId { get; }

    /// <summary>
    /// Gets whether the counter is currently paused (session locked/suspended).
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Event raised when the usage counter detects that a warning threshold
    /// has been crossed (e.g. 10 min remaining, 5 min remaining).
    /// The argument is the number of minutes remaining.
    /// </summary>
    event Action<int>? WarningThresholdCrossed;

    /// <summary>
    /// Starts the usage counter. Should be called once at service startup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the usage counter.
    /// </summary>
    void Stop();

    /// <summary>
    /// Pauses the counter (called when the child's session is locked or
    /// fast-user-switching away).
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes the counter (called when the child's session is unlocked).
    /// </summary>
    void Resume();

    /// <summary>
    /// Called when the foreground app changes. Updates the tracked app.
    /// </summary>
    /// <param name="appId">The canonical AppId of the new foreground app.</param>
    void OnForegroundChanged(string appId);

    /// <summary>
    /// Requests backfill from T07 (usage reconciliation). Should be called
    /// at startup and periodically.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RequestBackfillAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the minutes remaining for a given scope before the limit is hit.
    /// Uses the current policy and active grants.
    /// </summary>
    /// <param name="appId">The app to check (or "device" for global).</param>
    /// <param name="now">Current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Minutes remaining, or null if no limit.</returns>
    Task<int?> GetMinutesRemainingAsync(
        string appId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a snapshot of the current usage for the rules engine.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current usage snapshot.</returns>
    Task<UsageSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
