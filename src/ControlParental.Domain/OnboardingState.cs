// <copyright file="OnboardingState.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T26 — Types for onboarding state persistence and funnel event tracking.
/// </summary>

/// <summary>
/// T26 — Funnel event types for tracking onboarding progress.
/// </summary>
public enum FunnelEventType
{
    OnboardingStepReached,
    OnboardingFirstWin,
    OnboardingCompleted,
    OnboardingAbandoned,
}

/// <summary>
/// T26 — A funnel event recorded during onboarding.
/// </summary>
/// <param name="Type">The type of funnel event.</param>
/// <param name="StepId">The step ID where the event occurred.</param>
/// <param name="OccurredAt">When the event occurred (UTC).</param>
public sealed record FunnelEvent(FunnelEventType Type, string StepId, DateTimeOffset OccurredAt);

/// <summary>
/// T26 — The complete onboarding state.
/// </summary>
/// <param name="CurrentStepIndex">Index of the current step.</param>
/// <param name="IsCompleted">Whether onboarding has completed.</param>
/// <param name="IsAbandoned">Whether the user abandoned onboarding.</param>
/// <param name="Steps">All onboarding steps.</param>
/// <param name="Events">Funnel events recorded so far.</param>
public sealed record OnboardingState(
    int CurrentStepIndex,
    bool IsCompleted,
    bool IsAbandoned,
    IReadOnlyList<OnboardingStep> Steps,
    IReadOnlyList<FunnelEvent> Events);
