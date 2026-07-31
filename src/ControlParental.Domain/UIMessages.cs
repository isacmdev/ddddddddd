// <copyright file="UIMessages.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Canonical App.UI to Service IPC envelope records.
/// App.UI keeps wire-compatible mirror records because it owns its client assembly;
/// the Service and Domain source-generated catalogue use these Domain-owned types.
/// </summary>
public sealed record GetEnforcementLevel() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(GetEnforcementLevel);
}

/// <summary>Service response containing the current enforcement level and checks.</summary>
public sealed record EnforcementLevelResponse(
    EnforcementLevel Level,
    IReadOnlyList<EnforcementLevelCheck> Checks) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(EnforcementLevelResponse);
}

/// <summary>One enforcement check result.</summary>
public sealed record EnforcementLevelCheck(
    string CheckName,
    bool IsPassing,
    string Details);

/// <summary>Request for the current onboarding state.</summary>
public sealed record GetOnboardingState() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(GetOnboardingState);
}

/// <summary>Service response containing the current onboarding state.</summary>
public sealed record OnboardingStateResponse(OnboardingState State) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(OnboardingStateResponse);
}

/// <summary>Records that an onboarding step was completed.</summary>
public sealed record RecordOnboardingStepCompleted(string StepId) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(RecordOnboardingStepCompleted);
}

/// <summary>Response for onboarding-step recording.</summary>
public sealed record StepCompletedResponse(bool Success) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(StepCompletedResponse);
}

/// <summary>Records a causal funnel event.</summary>
public sealed record RecordFunnelEvent(string EventName, string StepId = "unknown") : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(RecordFunnelEvent);
}

/// <summary>Requests that the Session Agent show an overlay.</summary>
public sealed record ShowOverlayCommand(string Reason, string? CtaLabel = null) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ShowOverlayCommand);
}

/// <summary>Requests that the Session Agent hide an overlay.</summary>
public sealed record HideOverlayCommand() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(HideOverlayCommand);
}

/// <summary>Requests pairing with a parent account.</summary>
public sealed record PairDevice(string Code, string AgeBand) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(PairDevice);
}

/// <summary>Response to a pairing request.</summary>
public sealed record PairDeviceResponse(
    bool Success,
    string? DeviceId,
    string? ParentId,
    int PolicyVersion,
    PairingStatus Status,
    string? ErrorMessage) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(PairDeviceResponse);
}

/// <summary>Requests the list of Windows user accounts.</summary>
public sealed record ListAccounts() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ListAccounts);
}

/// <summary>Information about a Windows user account.</summary>
public sealed record AccountInfo(string Username, string Type, bool IsStandard);

/// <summary>Response containing Windows user accounts.</summary>
public sealed record AccountList(IReadOnlyList<AccountInfo> Accounts) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(AccountList);
}

/// <summary>Requests creation of a standard Windows account.</summary>
public sealed record CreateAccount(string Username, string Password) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(CreateAccount);
}

/// <summary>Response to an account-creation request.</summary>
public sealed record CreateAccountResponse(
    bool Success,
    string? ErrorMessage,
    bool RequiresElevation = false) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(CreateAccountResponse);
}

/// <summary>Requests conversion of an administrator account to standard.</summary>
public sealed record ConvertAccount(string Username) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ConvertAccount);
}

/// <summary>Response to an account-conversion request.</summary>
public sealed record ConvertAccountResponse(bool Success, string? ErrorMessage) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ConvertAccountResponse);
}

/// <summary>Requests the Service installation status.</summary>
public sealed record GetServiceStatus() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(GetServiceStatus);
}

/// <summary>Service installation status response.</summary>
public sealed record ServiceStatusResponse(
    bool IsInstalled,
    bool IsRunning,
    string? StatusDescription) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ServiceStatusResponse);
}

/// <summary>Requests the current consent status.</summary>
public sealed record GetConsentStatus() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(GetConsentStatus);
}

/// <summary>Consent status response.</summary>
public sealed record ConsentStatusSnapshot(
    bool IsGranted,
    DateTimeOffset GrantedAt,
    string? GrantedByDeviceId) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ConsentStatusSnapshot);
}

/// <summary>Grants consent for data collection.</summary>
public sealed record GrantConsent(string? GrantedByDeviceId) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(GrantConsent);
}

/// <summary>Advances the onboarding state machine.</summary>
public sealed record AdvanceOnboardingStep() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(AdvanceOnboardingStep);
}

/// <summary>Resets the onboarding state machine.</summary>
public sealed record ResetOnboardingState(string? Reason = null) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ResetOnboardingState);
}

/// <summary>T26 PR #14 — App.UI → Service: triggers an immediate policy/sync refresh.</summary>
public sealed record TriggerSync() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(TriggerSync);
}
