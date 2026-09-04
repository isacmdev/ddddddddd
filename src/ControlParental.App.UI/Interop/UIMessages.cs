// <copyright file="UIMessages.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.Domain;

/// <summary>
/// T26 PR #4 — IPC message records for App.UI to Service communication.
///
/// The data records that travel inside the payloads (OnboardingState,
/// OnboardingStep, OnboardingStepStatus, FunnelEvent) live in the Domain
/// assembly as the canonical single source of truth (ADR-002, ADR-003);
/// App.UI re-uses them directly over JSON. Only the wire envelopes —
/// the request/response records that implement <see cref="IUIMessage"/> —
/// are duplicated here because App.UI cannot reference the Service
/// assembly but still needs a stable type to constrain the channel.
/// </summary>

/// <summary>
/// Base interface for UI → Service messages.
/// </summary>
public interface IUIMessage : ControlParental.Domain.IUIMessage
{
}

/// <summary>
/// T26 — UI → Service: request for the current enforcement level.
/// </summary>
public sealed record GetEnforcementLevel() : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(GetEnforcementLevel);
}

/// <summary>
/// T26 — Service → UI: response with the current enforcement level and checks.
/// </summary>
/// <param name="Level">The current enforcement level.</param>
/// <param name="Checks">List of enforcement checks with pass/fail status.</param>
public sealed record EnforcementLevelResponse(
    string Level,
    IReadOnlyList<EnforcementLevelCheck> Checks) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(EnforcementLevelResponse);
}

/// <summary>
/// T26 — A single enforcement check result.
/// </summary>
/// <param name="CheckName">Identifier for the check.</param>
/// <param name="IsPassing">Whether the check is passing.</param>
/// <param name="Details">Human-readable details about the check.</param>
public sealed record EnforcementLevelCheck(
    string CheckName,
    bool IsPassing,
    string Details);

/// <summary>
/// T26 — UI → Service: request for the current onboarding state.
/// </summary>
public sealed record GetOnboardingState() : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(GetOnboardingState);
}

/// <summary>
/// T26 — Service → UI: response with the current onboarding state.
/// The payload is the canonical Domain type so we don't maintain two
/// parallel type trees (ADR-003). Wire-level JSON is identical regardless
/// of which side serializes / deserializes it.
/// </summary>
/// <param name="State">The current onboarding state from the Service.</param>
public sealed record OnboardingStateResponse(Domain.OnboardingState State) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(OnboardingStateResponse);
}

/// <summary>
/// T26 — UI → Service: record that a step was completed.
/// </summary>
/// <param name="StepId">The step identifier.</param>
public sealed record RecordOnboardingStepCompleted(string StepId) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(RecordOnboardingStepCompleted);
}

/// <summary>
/// T26 — UI → Service: response for step completion recording.
/// </summary>
public sealed record StepCompletedResponse(bool Success) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(StepCompletedResponse);
}

/// <summary>
/// T26 — UI → Service: record a causal funnel event.
/// </summary>
/// <param name="EventName">Name of the funnel event.</param>
/// <param name="StepId">Stable step identifier that caused the event.</param>
public sealed record RecordFunnelEvent(string EventName, string StepId = "unknown") : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(RecordFunnelEvent);
}

/// <summary>
/// T26 — UI → Service: command to show overlay on SessionAgent.
/// </summary>
/// <param name="Reason">The reason/message to display.</param>
/// <param name="CtaLabel">Optional CTA button label.</param>
public sealed record ShowOverlayCommand(string Reason, string? CtaLabel = null) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(ShowOverlayCommand);
}

/// <summary>
/// T26 — UI → Service: command to hide overlay on SessionAgent.
/// </summary>
public sealed record HideOverlayCommand() : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(HideOverlayCommand);
}

/// <summary>
/// T26 — UI → Service: request to pair the device with a parent account.
/// </summary>
public sealed record PairDevice(string Code, string AgeBand) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(PairDevice);
}

/// <summary>
/// T26 — Service → UI: response to a PairDevice request.
/// </summary>
public sealed record PairDeviceResponse(
    bool Success,
    string? DeviceId,
    string? ParentId,
    int PolicyVersion,
    Domain.PairingStatus Status,
    string? ErrorMessage) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(PairDeviceResponse);
}

/// <summary>
/// T26 — UI → Service: request for the list of Windows accounts.
/// </summary>
public sealed record ListAccounts() : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(ListAccounts);
}

/// <summary>
/// T26 — Information about a Windows user account.
/// </summary>
public sealed record AccountInfo(string Username, string Type, bool IsStandard);

/// <summary>
/// T26 — Service → UI: response with the list of Windows accounts.
/// </summary>
public sealed record AccountList(IReadOnlyList<AccountInfo> Accounts) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(AccountList);
}

/// <summary>
/// T26 — UI → Service: request to create a new standard account.
/// </summary>
public sealed record CreateAccount(string Username, string Password) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(CreateAccount);
}

/// <summary>
/// T26 — Service → UI: response to CreateAccount.
/// </summary>
public sealed record CreateAccountResponse(
    bool Success,
    string? ErrorMessage,
    bool RequiresElevation = false) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(CreateAccountResponse);
}

/// <summary>
/// T26 — UI → Service: request to convert an admin account to standard.
/// </summary>
public sealed record ConvertAccount(string Username) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(ConvertAccount);
}

/// <summary>
/// T26 — Service → UI: response to ConvertAccount.
/// </summary>
public sealed record ConvertAccountResponse(bool Success, string? ErrorMessage) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(ConvertAccountResponse);
}

/// <summary>
/// T26 — UI → Service: request for the service install status.
/// </summary>
public sealed record GetServiceStatus() : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(GetServiceStatus);
}

/// <summary>
/// T26 — Service → UI: response with service install status.
/// </summary>
public sealed record ServiceStatusResponse(bool IsInstalled, bool IsRunning, string? StatusDescription) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(ServiceStatusResponse);
}

/// <summary>
/// T25/T26 — UI → Service: request for the current consent status.
/// </summary>
public sealed record GetConsentStatus() : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(GetConsentStatus);
}

/// <summary>
/// T25/T26 — Service → UI: snapshot of consent state after a grant or query.
/// </summary>
/// <param name="IsGranted">Whether consent has been granted.</param>
/// <param name="GrantedAt">When consent was granted (UTC).</param>
/// <param name="GrantedByDeviceId">The device ID that granted consent (null if local).</param>
public sealed record ConsentStatusSnapshot(
    bool IsGranted,
    DateTimeOffset GrantedAt,
    string? GrantedByDeviceId) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(ConsentStatusSnapshot);
}

/// <summary>
/// T25/T26 — UI → Service: request to grant consent for data collection.
/// </summary>
/// <param name="GrantedByDeviceId">Optional device ID that is granting consent (null for local).</param>
public sealed record GrantConsent(string? GrantedByDeviceId) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(GrantConsent);
}

/// <summary>
/// T26 PR #11 — UI → Service: request to advance the onboarding state machine to
/// the next step. The Service resolves the target index from its current snapshot
/// (current step index + 1) and persists the result atomically. Returns the new
/// <see cref="OnboardingStateResponse"/> so the UI gets the post-advance snapshot
/// in a single round-trip.
/// </summary>
public sealed record AdvanceOnboardingStep() : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(AdvanceOnboardingStep);
}

/// <summary>
/// T26 PR #11 — UI → Service: request to reset the onboarding state machine to
/// its initial state. The Service wipes the persisted file and returns the fresh
/// initial <see cref="OnboardingStateResponse"/>.
/// </summary>
/// <param name="Reason">
/// Optional audit-trail reason. The Service logs the reason at Warning level when
/// supplied so destructive resets are visible in the Service log. May be null.
/// </param>
public sealed record ResetOnboardingState(string? Reason = null) : IUIMessage
{
    /// <inheritdoc/>
    public string MessageType => nameof(ResetOnboardingState);
}
