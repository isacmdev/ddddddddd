// <copyright file="OnboardingE2EStateMachineTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Moq;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 (Fase 5) — End-to-end behavior test for the onboarding state machine.
///
/// Earlier revisions drove the flow through <c>OnStepCompletedAsync(stepId)</c>
/// and an <c>IUIChannel</c> / <c>IConsentService</c> triple. The current
/// <see cref="OnboardingViewModel"/> API has no per-step completion command —
/// the VM advances the state machine through <c>GoNextCommand</c> and persists
/// through <see cref="IOnboardingStateStore"/>. This test pins down the
/// observable contract against the current API:
///
/// <list type="bullet">
///   <item>Five successive advances move through the 5 steps in order
///         (pairing → consent → account → demo → managed).</item>
///   <item>The fifth advance marks the state <c>IsCompleted = true</c>.</item>
///   <item>When the supplied monitor reports <see cref="EnforcementLevel.Managed"/>
///         with no issues, all four T12 checks pass and the progress label
///         reads <c>"Protección 4 de 4"</c>.</item>
/// </list>
///
/// We use <c>GoNextCommand</c> rather than <c>ExecuteStepCommand</c> so the
/// test stays free of UI-thread side effects (e.g. <c>DispatcherTimer</c> in
/// the demo step) — the E2E intent is "the state machine advances and reports
/// completion", not "every per-step side-effect runs".
/// </summary>
public sealed class OnboardingE2EStateMachineTests
{
    [Fact]
    public async Task CompleteOnboardingAdvancesStepsInOrderAndReportsCompletion()
    {
        var steps = BuildSteps();
        var client = new FakeIpcOnboardingStateService(new OnboardingState(0, false, false, steps, Array.Empty<FunnelEvent>()));
        var monitor = CreateMonitor(level: EnforcementLevel.Managed, issues: Array.Empty<EnforcementIssue>());
        var viewModel = new OnboardingViewModel(client, monitor.Object);

        await viewModel.InitializeAsync().ConfigureAwait(false);

        var visitedStepIds = new List<string>();
        for (int i = 0; i < steps.Count; i++)
        {
            Assert.NotNull(viewModel.CurrentStep);
            visitedStepIds.Add(viewModel.CurrentStep!.Id);
            await viewModel.GoNextCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }

        // The completion signal lives on the canonical Service snapshot; the
        // VM refreshes from it on every round-trip and exposes IsCompleted on
        // the observable surface.
        Assert.True(viewModel.IsCompleted);

        // Progress bar still reports 4/4 because the monitor snapshot drives
        // the count, not the step status.
        Assert.Equal(4, viewModel.ProgressCount);
        Assert.Equal("Protección 4 de 4", viewModel.ProgressLabel);
        Assert.Equal(
            new[] { "pairing", "consent", "account", "service", "demo", "managed" },
            visitedStepIds);
    }

    private static List<OnboardingStep> BuildSteps()
    {
        return new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", string.Empty, string.Empty, OnboardingStepStatus.InProgress),
            new(1, "consent", "Consent", string.Empty, string.Empty, OnboardingStepStatus.Locked),
            new(2, "account", "Account", string.Empty, string.Empty, OnboardingStepStatus.Locked),
            new(3, "service", "Service", string.Empty, string.Empty, OnboardingStepStatus.Locked),
            new(4, "demo", "Demo", string.Empty, string.Empty, OnboardingStepStatus.Locked, true),
            new(5, "managed", "Managed", string.Empty, string.Empty, OnboardingStepStatus.Locked),
        };
    }

    private static Mock<IEnforcementLevelMonitor> CreateMonitor(
        EnforcementLevel level,
        IReadOnlyList<EnforcementIssue> issues)
    {
        var mock = new Mock<IEnforcementLevelMonitor>();
        mock.SetupGet(m => m.CurrentLevel).Returns(level);
        mock.SetupGet(m => m.CurrentIssues).Returns(issues);
        mock.SetupGet(m => m.LastEvaluationTime).Returns(DateTimeOffset.UtcNow);
        mock.Setup(m => m.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        mock.Setup(m => m.EvaluateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return mock;
    }

    /// <summary>
    /// T26 PR (P2 onboarding ownership) — the legacy <c>TestStore</c>
    /// (an in-memory <see cref="IOnboardingStateStore"/>) is replaced by
    /// <see cref="FakeIpcOnboardingStateService"/>; the VM now drives every
    /// state transition through the IPC client.
    /// </summary>
}
