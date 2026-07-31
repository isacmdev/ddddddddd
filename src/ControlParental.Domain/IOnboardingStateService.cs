// <copyright file="IOnboardingStateService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T26 — Interface for the Service-side onboarding state management.
/// App.UI calls this via IPC to persist and query onboarding state at the Service layer.
/// </summary>
public interface IOnboardingStateService
{
    /// <summary>
    /// Gets the current onboarding state.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current onboarding state.</returns>
    Task<OnboardingState> GetStateAsync(CancellationToken ct = default);

    /// <summary>
    /// T26 PR-Fase (P2 onboarding ownership) — Records that a step was completed
    /// and returns the canonical post-completion <see cref="OnboardingState"/>
    /// snapshot so the caller never needs to re-read state to learn what the
    /// Service persisted. The Service is the single source of truth for
    /// onboarding progress (ADR-002); App.UI must drive every completion
    /// through this method (via IPC) and refresh its observable surface from
    /// the returned snapshot.
    ///
    /// Idempotency: a repeated call with the same stable <paramref name="stepId"/>
    /// is a no-op on the persisted state (the step is already Completed) and
    /// returns the same canonical snapshot without re-emitting a persistence
    /// event. Unknown step IDs do not corrupt state — the Service returns the
    /// current snapshot untouched.
    /// </summary>
    /// <param name="stepId">The stable step identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The canonical post-completion <see cref="OnboardingState"/>.</returns>
    Task<OnboardingState> RecordStepCompletedAsync(string stepId, CancellationToken ct = default);

    /// <summary>
    /// Records a causal funnel event and returns the canonical snapshot. Duplicate
    /// event-type and step-id pairs are idempotent.
    /// </summary>
    /// <param name="eventName">The event name to record.</param>
    /// <param name="stepId">The stable step identifier that caused the event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The canonical onboarding snapshot.</returns>
    Task<OnboardingState> RecordFunnelEventAsync(string eventName, string stepId, CancellationToken ct = default);

    /// <summary>
    /// T26 Fase 5 — Advances the active step to <paramref name="newIndex"/>, marking
    /// all steps before the new index as <see cref="OnboardingStepStatus.Completed"/>
    /// and the new index as <see cref="OnboardingStepStatus.InProgress"/>. Persists
    /// the resulting state atomically (`.tmp` + `File.Move(overwrite: true)`).
    /// </summary>
    /// <param name="newIndex">The new current step index (0-based).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The post-advance state.</returns>
    Task<OnboardingState> AdvanceAsync(int newIndex, CancellationToken ct = default);

    /// <summary>
    /// T26 Fase 10 (test helper) — Wipes any persisted state and returns the
    /// fresh initial state. Used by tests that need to assert a clean baseline.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fresh initial state.</returns>
    Task<OnboardingState> ResetAsync(CancellationToken ct = default);
}
