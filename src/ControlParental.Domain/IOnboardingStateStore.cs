// <copyright file="IOnboardingStateStore.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T26 — Interface for persisting onboarding state.
/// </summary>
public interface IOnboardingStateStore
{
    /// <summary>
    /// Loads the onboarding state from persistent storage.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current onboarding state.</returns>
    Task<OnboardingState> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the onboarding state to persistent storage.
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SaveAsync(OnboardingState state, CancellationToken ct = default);

    /// <summary>
    /// Records a funnel event.
    /// </summary>
    /// <param name="evt">The event to record.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordFunnelEventAsync(FunnelEvent evt, CancellationToken ct = default);
}
