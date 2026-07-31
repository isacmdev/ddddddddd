// <copyright file="IIpcOnboardingStateService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Interop;

using ControlParental.Domain;

/// <summary>
/// T26 PR (P2 onboarding ownership) — Abstraction over the App.UI IPC client
/// for the Service-owned onboarding state machine. The App.UI never touches
/// a local cache or file (ADR-002); every state read, completion, advance,
/// and reset flows through this surface and refreshes its observable
/// surface from the canonical <see cref="OnboardingState"/> snapshot the
/// Service returns.
///
/// Implementations MUST translate transport failures (null response, pipe
/// timeout, deserialization failure) into <see cref="ConsentServiceUnavailableException"/>
/// so the caller fails closed instead of silently advancing with stale local
/// state.
/// </summary>
public interface IIpcOnboardingStateService
{
    /// <summary>
    /// Reads the canonical onboarding snapshot from the Service.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The canonical onboarding snapshot from the Service.</returns>
    /// <exception cref="ConsentServiceUnavailableException">
    /// Thrown when the Service does not acknowledge the read.
    /// </exception>
    Task<OnboardingState> GetOnboardingStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Records the completion of a stable <paramref name="stepId"/> on the
    /// Service and returns the canonical post-completion snapshot. The Service
    /// is idempotent — duplicate completions return the same snapshot.
    /// </summary>
    /// <param name="stepId">The stable step identifier (e.g. "pairing").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The canonical post-completion onboarding snapshot.</returns>
    /// <exception cref="ConsentServiceUnavailableException">
    /// Thrown when the Service does not acknowledge the completion.
    /// </exception>
    Task<OnboardingState> CompleteOnboardingStepAsync(string stepId, CancellationToken ct = default);

    /// <summary>
    /// Asks the Service to advance the onboarding state machine by one step
    /// and returns the resulting snapshot.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The post-advance onboarding state.</returns>
    /// <exception cref="ConsentServiceUnavailableException">
    /// Thrown when the Service does not acknowledge the request.
    /// </exception>
    Task<OnboardingState> AdvanceOnboardingStepAsync(CancellationToken ct = default);

    /// <summary>
    /// Asks the Service to reset the onboarding state machine to its initial
    /// state and returns the fresh snapshot.
    /// </summary>
    /// <param name="reason">Optional audit-trail reason for the reset.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The fresh initial onboarding state.</returns>
    /// <exception cref="ConsentServiceUnavailableException">
    /// Thrown when the Service does not acknowledge the request.
    /// </exception>
    Task<OnboardingState> ResetOnboardingStateAsync(string? reason = null, CancellationToken ct = default);

    /// <summary>Records a causal funnel event and returns the canonical snapshot.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<OnboardingState> RecordFunnelEventAsync(FunnelEventType type, string stepId, CancellationToken ct = default);

    /// <summary>Requests fresh Service-owned setup verification.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task<ControlParental.App.UI.ServiceStatusResponse> RequestOrVerifyServiceSetupAsync(CancellationToken ct = default);
}
