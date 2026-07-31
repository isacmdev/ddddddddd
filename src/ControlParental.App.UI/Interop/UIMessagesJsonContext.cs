// <copyright file="UIMessagesJsonContext.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Interop;

using System.Text.Json.Serialization;
using ControlParental.Domain;

/// <summary>
/// T26 PR #14 — App.UI-side source-generated JSON metadata for the App.UI
/// duplicate IPC envelope records declared in <c>UIMessages.cs</c>.
///
/// The Domain assembly owns the canonical envelope types
/// (<see cref="Domain.UIMessagesJsonContext"/>). The App.UI copies exist
/// because App.UI cannot reference the Service assembly but still needs
/// stable types to constrain the <see cref="IUIChannel"/> generics. This
/// context mirrors the Domain catalogue for App.UI's own copy so the
/// client-side serialization stays AOT-friendly and the wire format
/// matches the Domain side exactly (same fields, same enum-as-string
/// shape).
///
/// The payload types (<see cref="OnboardingState"/>, <see cref="OnboardingStep"/>,
/// <see cref="FunnelEvent"/>, <see cref="GrantInfo"/>, <see cref="ActiveIssue"/>,
/// <see cref="AccountInfo"/>) are reused from the Domain assembly (ADR-003),
/// so we reference the Domain context's metadata via this attribute list
/// rather than redefining the records here.
/// </summary>
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(GetEnforcementLevel))]
[JsonSerializable(typeof(EnforcementLevelResponse))]
[JsonSerializable(typeof(EnforcementLevelCheck))]
[JsonSerializable(typeof(GetOnboardingState))]
[JsonSerializable(typeof(OnboardingStateResponse))]
[JsonSerializable(typeof(OnboardingState))]
[JsonSerializable(typeof(OnboardingStep))]
[JsonSerializable(typeof(FunnelEvent))]
[JsonSerializable(typeof(RecordOnboardingStepCompleted))]
[JsonSerializable(typeof(StepCompletedResponse))]
[JsonSerializable(typeof(RecordFunnelEvent))]
[JsonSerializable(typeof(ShowOverlayCommand))]
[JsonSerializable(typeof(HideOverlayCommand))]
[JsonSerializable(typeof(GetUsageState))]
[JsonSerializable(typeof(UsageStateResponse))]
[JsonSerializable(typeof(GrantInfo))]
[JsonSerializable(typeof(ActiveIssue))]
[JsonSerializable(typeof(TriggerSync))]
[JsonSerializable(typeof(PairDevice))]
[JsonSerializable(typeof(PairDeviceResponse))]
[JsonSerializable(typeof(ListAccounts))]
[JsonSerializable(typeof(AccountList))]
[JsonSerializable(typeof(AccountInfo))]
[JsonSerializable(typeof(CreateAccount))]
[JsonSerializable(typeof(CreateAccountResponse))]
[JsonSerializable(typeof(ConvertAccount))]
[JsonSerializable(typeof(ConvertAccountResponse))]
[JsonSerializable(typeof(ServiceStatusResponse))]
[JsonSerializable(typeof(ConsentStatusSnapshot))]
[JsonSerializable(typeof(GrantConsent))]
[JsonSerializable(typeof(AdvanceOnboardingStep))]
[JsonSerializable(typeof(ResetOnboardingState))]
[JsonSerializable(typeof(EnforcementLevel))]
[JsonSerializable(typeof(EnforcementIssueType))]
[JsonSerializable(typeof(EnforcementIssueSeverity))]
[JsonSerializable(typeof(OnboardingStepStatus))]
[JsonSerializable(typeof(FunnelEventType))]
[JsonSerializable(typeof(GrantSource))]
[JsonSerializable(typeof(PairingStatus))]
public sealed partial class UIMessagesJsonContext : JsonSerializerContext
{
}
