// <copyright file="UIMessagesJsonContext.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json.Serialization;

/// <summary>
/// T26 PR #14 — Source-generated JSON metadata for the App.UI ↔ Service IPC
/// envelope types declared in <c>IpcMessage.cs</c> (Domain), plus the payload
/// records they carry (<see cref="OnboardingState"/>, <see cref="OnboardingStep"/>,
/// <see cref="FunnelEvent"/>, <see cref="GrantInfo"/>, <see cref="ActiveIssue"/>,
/// <see cref="AccountInfo"/>) and the enums those records reference.
///
/// This is the canonical catalogue that closes S4 (verify report) and
/// satisfies ADR-006: AOT-safe JSON for IPC. The Service project uses this
/// context through <c>UIMessagesJsonContext.Default.&lt;TypeName&gt;</c>
/// instead of reflection-based <c>JsonSerializer.Serialize/Deserialize</c>
/// calls in <c>NamedPipeUIServer</c> and <c>NamedPipeServer</c>.
///
/// Enum values are serialized as their PascalCase string names via
/// <see cref="JsonSourceGenerationOptions.UseStringEnumConverter"/>; the
/// App.UI duplicate records (<c>App.UI.EnforcementLevelResponse.Level</c>
/// etc.) carry the field as <c>string</c> on the wire, so the round-trip is
/// symmetric on both sides of the pipe.
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
[JsonSerializable(typeof(GetServiceStatus))]
[JsonSerializable(typeof(ServiceStatusResponse))]
[JsonSerializable(typeof(GetConsentStatus))]
[JsonSerializable(typeof(ConsentStatusSnapshot))]
[JsonSerializable(typeof(GrantConsent))]
[JsonSerializable(typeof(AdvanceOnboardingStep))]
[JsonSerializable(typeof(ResetOnboardingState))]
[JsonSerializable(typeof(RegisterWnsChannel))]
[JsonSerializable(typeof(WnsRegistrationResult))]
[JsonSerializable(typeof(WnsRegistrationStatus))]

// Agent ↔ Service envelopes (same wire contract, different pipe).
[JsonSerializable(typeof(ForegroundChanged))]
[JsonSerializable(typeof(AgentHeartbeat))]
[JsonSerializable(typeof(AgentAuthority))]
[JsonSerializable(typeof(AgentCommandRequest))]
[JsonSerializable(typeof(AgentCommandCompleted))]
[JsonSerializable(typeof(StateSnapshot))]
[JsonSerializable(typeof(Pong))]
[JsonSerializable(typeof(ShowOverlay))]
[JsonSerializable(typeof(HideOverlay))]
[JsonSerializable(typeof(ShowWarning))]
[JsonSerializable(typeof(LockWorkstation))]
[JsonSerializable(typeof(RequestStateSnapshot))]
[JsonSerializable(typeof(Ping))]

// Enums referenced transitively from the records above. Source-gen needs
// them declared because the source generator does not infer enums from
// property types the way the reflection serializer does.
[JsonSerializable(typeof(EnforcementLevel))]
[JsonSerializable(typeof(EnforcementIssueType))]
[JsonSerializable(typeof(EnforcementIssueSeverity))]
[JsonSerializable(typeof(OnboardingStepStatus))]
[JsonSerializable(typeof(FunnelEventType))]
[JsonSerializable(typeof(GrantSource))]
[JsonSerializable(typeof(PairingStatus))]
[JsonSerializable(typeof(ContractEnvelope))]
[JsonSerializable(typeof(ContractHint))]
[JsonSerializable(typeof(SetProtectedAccountRequest))]
[JsonSerializable(typeof(SetProtectedAccountResponse))]
[JsonSerializable(typeof(RuntimeActivationState))]
[JsonSerializable(typeof(CreateTimeRequest))]
[JsonSerializable(typeof(TimeRequestOutbox))]
[JsonSerializable(typeof(IntegrityEvidence))]
[JsonSerializable(typeof(IntegrityVerdict))]
public sealed partial class UIMessagesJsonContext : JsonSerializerContext
{
}
