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
[JsonSourceGenerationOptions(
    UseStringEnumConverter = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(ControlParental.App.UI.GetEnforcementLevel))]
[JsonSerializable(typeof(ControlParental.App.UI.EnforcementLevelResponse))]
[JsonSerializable(typeof(ControlParental.App.UI.EnforcementLevelCheck))]
[JsonSerializable(typeof(ControlParental.App.UI.GetOnboardingState))]
[JsonSerializable(typeof(ControlParental.App.UI.OnboardingStateResponse))]
[JsonSerializable(typeof(OnboardingState))]
[JsonSerializable(typeof(OnboardingStep))]
[JsonSerializable(typeof(FunnelEvent))]
[JsonSerializable(typeof(ControlParental.App.UI.RecordOnboardingStepCompleted))]
[JsonSerializable(typeof(ControlParental.App.UI.StepCompletedResponse))]
[JsonSerializable(typeof(ControlParental.App.UI.RecordFunnelEvent))]
[JsonSerializable(typeof(ControlParental.App.UI.ShowOverlayCommand))]
[JsonSerializable(typeof(ControlParental.App.UI.HideOverlayCommand))]
[JsonSerializable(typeof(ControlParental.Domain.GetUsageState))]
[JsonSerializable(typeof(ControlParental.Domain.UsageStateResponse))]
[JsonSerializable(typeof(GrantInfo))]
[JsonSerializable(typeof(ActiveIssue))]
[JsonSerializable(typeof(TriggerSync))]
[JsonSerializable(typeof(ControlParental.Domain.GetRealtimeIdentity))]
[JsonSerializable(typeof(ControlParental.Domain.RealtimeIdentityResponse))]
[JsonSerializable(typeof(ControlParental.App.UI.PairDevice))]
[JsonSerializable(typeof(ControlParental.App.UI.PairDeviceResponse))]
[JsonSerializable(typeof(ControlParental.App.UI.ListAccounts))]
[JsonSerializable(typeof(ControlParental.App.UI.AccountList))]
[JsonSerializable(typeof(ControlParental.App.UI.AccountInfo))]
[JsonSerializable(typeof(ControlParental.App.UI.CreateAccount))]
[JsonSerializable(typeof(ControlParental.App.UI.CreateAccountResponse))]
[JsonSerializable(typeof(ControlParental.App.UI.ConvertAccount))]
[JsonSerializable(typeof(ControlParental.App.UI.ConvertAccountResponse))]
[JsonSerializable(typeof(ControlParental.App.UI.GetServiceStatus))]
[JsonSerializable(typeof(ControlParental.App.UI.ServiceStatusResponse))]
[JsonSerializable(typeof(ControlParental.App.UI.GetConsentStatus))]
[JsonSerializable(typeof(ControlParental.App.UI.ConsentStatusSnapshot))]
[JsonSerializable(typeof(ControlParental.App.UI.GrantConsent))]
[JsonSerializable(typeof(ControlParental.App.UI.AdvanceOnboardingStep))]
[JsonSerializable(typeof(ControlParental.App.UI.ResetOnboardingState))]
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
