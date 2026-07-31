// <copyright file="OnboardingStep.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T26 — Represents the status of an onboarding step.
/// </summary>
public enum OnboardingStepStatus
{
    Locked,
    Pending,
    InProgress,
    Completed,
    Failed,
}

/// <summary>
/// T26 — Represents a single step in the onboarding flow.
/// </summary>
/// <param name="Index">Zero-based step index.</param>
/// <param name="Id">Unique step identifier.</param>
/// <param name="Title">Human-readable step title.</param>
/// <param name="Description">Step description shown to the user.</param>
/// <param name="ButtonLabel">Label for the primary action button.</param>
/// <param name="Status">Current step status.</param>
/// <param name="IsFirstWin">Whether this step is the first win moment.</param>
public sealed record OnboardingStep(
    int Index,
    string Id,
    string Title,
    string Description,
    string? ButtonLabel,
    OnboardingStepStatus Status,
    bool IsFirstWin = false);
