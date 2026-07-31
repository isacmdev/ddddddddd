// <copyright file="IpcOnboardingStateService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Interop;

using ControlParental.Domain;

/// <summary>
/// T26 PR #11 — IPC-backed client for the Service-side
/// <see cref="IOnboardingStateService.AdvanceAsync"/> and
/// <see cref="IOnboardingStateService.ResetAsync"/> operations.
///
/// Mirrors the <see cref="IpcConsentService"/> pattern: a thin proxy over
/// <see cref="IUIChannel"/> that translates transport failures into the
/// fail-closed <see cref="ConsentServiceUnavailableException"/> so the UI never
/// silently advances without Service acknowledgement.
///
/// The current <see cref="OnboardingViewModel"/> drives state transitions
/// through <c>RecordOnboardingStepCompleted</c> + <c>GetOnboardingState</c>
/// round-trips, so this proxy is unused by today's UI; it exists to complete
/// design §2 IPC surface so any future feature (test runner, kiosk reset,
/// remote admin) can drive the state machine programmatically.
/// </summary>
public sealed class IpcOnboardingStateService : IIpcOnboardingStateService
{
    private readonly IUIChannel channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="IpcOnboardingStateService"/> class.
    /// </summary>
    /// <param name="channel">The IPC channel used to talk to the Service.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="channel"/> is null.</exception>
    public IpcOnboardingStateService(IUIChannel channel)
    {
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <summary>
    /// T26 PR (P2 onboarding ownership) — Reads the canonical onboarding
    /// snapshot from the Service. The Service is the single source of truth
    /// (ADR-002); this method is the only path the App.UI may use to observe
    /// onboarding progress.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The canonical onboarding snapshot from the Service.</returns>
    /// <exception cref="ConsentServiceUnavailableException">
    /// Thrown when the Service does not acknowledge the read (pipe timeout,
    /// deserialization failure, agent crashed) or the response is null.
    /// The caller MUST surface this — the App.UI must never silently fall
    /// back to a local cache.
    /// </exception>
    public async Task<OnboardingState> GetOnboardingStateAsync(CancellationToken ct = default)
    {
        var response = await this.channel.QueryAsync<
            ControlParental.App.UI.GetOnboardingState,
            ControlParental.App.UI.OnboardingStateResponse>(
            new ControlParental.App.UI.GetOnboardingState(),
            ct).ConfigureAwait(false);

        if (response is null)
        {
            throw new ConsentServiceUnavailableException(
                "Onboarding service did not acknowledge the state read. The Service may be unavailable.");
        }

        return response.State;
    }

    /// <summary>
    /// T26 PR (P2 onboarding ownership) — Asks the Service to record the
    /// completion of a stable <paramref name="stepId"/> and returns the
    /// canonical post-completion snapshot. The Service persists atomically
    /// and the completion is idempotent: a duplicate call with the same
    /// stable <paramref name="stepId"/> is a no-op that returns the same
    /// snapshot.
    /// </summary>
    /// <param name="stepId">The stable step identifier (e.g. "pairing").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The canonical post-completion onboarding snapshot.</returns>
    /// <exception cref="ConsentServiceUnavailableException">
    /// Thrown when the Service does not acknowledge the completion or the
    /// response is null. The caller MUST surface this and refuse to advance
    /// the UI — see ADR-001 in t26 design.
    /// </exception>
    public async Task<OnboardingState> CompleteOnboardingStepAsync(string stepId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new ArgumentException(
                "Step id must be a non-empty stable identifier.",
                nameof(stepId));
        }

        var response = await this.channel.QueryAsync<
            ControlParental.App.UI.RecordOnboardingStepCompleted,
            ControlParental.App.UI.OnboardingStateResponse>(
            new ControlParental.App.UI.RecordOnboardingStepCompleted(stepId),
            ct).ConfigureAwait(false);

        if (response is null)
        {
            throw new ConsentServiceUnavailableException(
                "Onboarding service did not acknowledge the step completion. The Service may be unavailable.");
        }

        return response.State;
    }

    /// <summary>
    /// Asks the Service to advance the onboarding state machine by one step and
    /// returns the resulting snapshot.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The post-advance onboarding state.</returns>
    /// <exception cref="ConsentServiceUnavailableException">
    /// Thrown when the Service does not acknowledge the request.
    /// </exception>
    public async Task<OnboardingState> AdvanceOnboardingStepAsync(CancellationToken ct = default)
    {
        var response = await this.channel.QueryAsync<
            ControlParental.App.UI.AdvanceOnboardingStep,
            ControlParental.App.UI.OnboardingStateResponse>(
            new ControlParental.App.UI.AdvanceOnboardingStep(),
            ct).ConfigureAwait(false);

        if (response is null)
        {
            throw new ConsentServiceUnavailableException(
                "Onboarding service did not acknowledge the advance. The Service may be unavailable.");
        }

        return response.State;
    }

    /// <inheritdoc/>
    public async Task<OnboardingState> RecordFunnelEventAsync(FunnelEventType type, string stepId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(stepId))
        {
            throw new ArgumentException("Step id must be a non-empty stable identifier.", nameof(stepId));
        }

        var response = await this.channel.QueryAsync<
            ControlParental.App.UI.RecordFunnelEvent,
            ControlParental.App.UI.OnboardingStateResponse>(
            new ControlParental.App.UI.RecordFunnelEvent(type.ToString(), stepId),
            ct).ConfigureAwait(false);
        return response?.State ?? throw new ConsentServiceUnavailableException(
            "Onboarding service did not acknowledge the funnel event.");
    }

    /// <inheritdoc/>
    public async Task<ControlParental.App.UI.ServiceStatusResponse> RequestOrVerifyServiceSetupAsync(CancellationToken ct = default)
    {
        var response = await this.channel.QueryAsync<
            ControlParental.App.UI.GetServiceStatus,
            ControlParental.App.UI.ServiceStatusResponse>(
            new ControlParental.App.UI.GetServiceStatus(),
            ct).ConfigureAwait(false);
        return response ?? throw new ConsentServiceUnavailableException(
            "Onboarding service did not acknowledge setup verification.");
    }

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
    public async Task<OnboardingState> ResetOnboardingStateAsync(string? reason = null, CancellationToken ct = default)
    {
        var response = await this.channel.QueryAsync<
            ControlParental.App.UI.ResetOnboardingState,
            ControlParental.App.UI.OnboardingStateResponse>(
            new ControlParental.App.UI.ResetOnboardingState(reason),
            ct).ConfigureAwait(false);

        if (response is null)
        {
            throw new ConsentServiceUnavailableException(
                "Onboarding service did not acknowledge the reset. The Service may be unavailable.");
        }

        return response.State;
    }
}
