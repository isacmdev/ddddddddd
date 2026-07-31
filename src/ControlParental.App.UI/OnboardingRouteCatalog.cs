// <copyright file="OnboardingRouteCatalog.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

/// <summary>
/// Stable live-shell destinations derived from the Service-owned onboarding snapshot.
/// </summary>
public enum OnboardingRoute
{
    /// <summary>Waiting for the canonical snapshot.</summary>
    Loading,

    /// <summary>Device pairing.</summary>
    Pairing,

    /// <summary>In-app disclosure and affirmative consent.</summary>
    Consent,

    /// <summary>Standard child-account confirmation.</summary>
    Account,

    /// <summary>Service setup and verification.</summary>
    ServiceSetup,

    /// <summary>First-win protection demonstration.</summary>
    Demo,

    /// <summary>Optional managed-level offer.</summary>
    Managed,

    /// <summary>Completed onboarding.</summary>
    Completed,

    /// <summary>Abandoned onboarding.</summary>
    Abandoned,

    /// <summary>Unrecognized stable identifier.</summary>
    Unknown,
}

/// <summary>
/// Maps stable onboarding step identifiers to live in-app routes.
/// </summary>
public static class OnboardingRouteCatalog
{
    private static readonly IReadOnlyList<string> StepIds = Array.AsReadOnly(
        new[] { "pairing", "consent", "account", "service", "demo", "managed" });

    /// <summary>
    /// Gets the canonical T26 route order. The first win (<c>demo</c>) precedes the managed offer.
    /// </summary>
    public static IReadOnlyList<string> CanonicalStepIds => StepIds;

    /// <summary>
    /// Selects one shell destination from the current canonical snapshot surface.
    /// </summary>
    /// <param name="currentStepId">Stable current-step identifier from the Service snapshot.</param>
    /// <param name="isCompleted">Whether the canonical flow is complete.</param>
    /// <param name="isAbandoned">Whether the canonical flow was abandoned.</param>
    /// <returns>The corresponding live-shell destination.</returns>
    public static OnboardingRoute Select(string? currentStepId, bool isCompleted, bool isAbandoned)
    {
        if (isAbandoned)
        {
            return OnboardingRoute.Abandoned;
        }

        if (isCompleted)
        {
            return OnboardingRoute.Completed;
        }

        return currentStepId switch
        {
            null or "" => OnboardingRoute.Loading,
            "pairing" => OnboardingRoute.Pairing,
            "consent" => OnboardingRoute.Consent,
            "account" => OnboardingRoute.Account,
            "service" => OnboardingRoute.ServiceSetup,
            "demo" => OnboardingRoute.Demo,
            "managed" => OnboardingRoute.Managed,
            _ => OnboardingRoute.Unknown,
        };
    }

    /// <summary>
    /// Returns whether a page completion still matches the current, unfinished snapshot.
    /// </summary>
    /// <param name="currentStepId">Stable current-step identifier from the Service snapshot.</param>
    /// <param name="reportedStepId">Stable identifier reported by the routed page.</param>
    /// <param name="isCompleted">Whether the canonical flow is complete.</param>
    /// <param name="isAbandoned">Whether the canonical flow was abandoned.</param>
    /// <returns><see langword="true"/> only for a matching unfinished step.</returns>
    public static bool CanComplete(
        string? currentStepId,
        string reportedStepId,
        bool isCompleted,
        bool isAbandoned)
        => !isCompleted
            && !isAbandoned
            && string.Equals(currentStepId, reportedStepId, StringComparison.Ordinal);
}
