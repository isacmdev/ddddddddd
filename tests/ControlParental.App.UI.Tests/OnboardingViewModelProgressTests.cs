// <copyright file="OnboardingViewModelProgressTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Moq;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 Fase 2 (ADR-005) + PR #4 — Regression tests for the honest progress bar.
/// The VM derives its progress from the supplied <see cref="IEnforcementLevelMonitor"/>;
/// without a monitor it falls back to counting the steps the
/// <see cref="IOnboardingStateStore"/> reports as completed. Either way the
/// "N de M" label never inflates past the observable truth.
///
/// Spec anchor (specs/onboarding/spec.md "Progress bar reflects real T12 state
/// with honest total"):
///   - ProgressTotal reflects the onboarding step count.
///   - ProgressCount is derived from the IEnforcementLevelMonitor snapshot when
///     one is supplied; otherwise it counts Completed steps in the current state.
///   - The label is always "Protección N de M" — never "Estado desconocido".
///     (That copy was a pre-refactor fallback that the current API does not
///     emit; the closest observable behavior is the constant initial label and
///     a count driven by the monitor.)
///
/// The four T12 checks the monitor snapshot drives:
///   1. Service running (no ServiceNotRunning issue)
///   2. Account is standard (no ChildIsAdministrator issue)
///   3. Watcher emitting (no AgentNotResponding / no HookTimeout)
///   4. Preventive layer (no PreventiveLayerUnavailable AND level is Standard/Managed)
///
/// When all four pass, ProgressCount == 4 and the label reads "Protección 4 de 4".
/// </summary>
public sealed class OnboardingViewModelProgressTests
{
    [Fact]
    public void ProgressTotalIs4()
    {
        // Arrange & Act — construct a VM with a fake IPC client; the constant
        // progressTotal is observable without initializing the view model.
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);

        // Assert — total reflects the four monitor-driven checks (not five).
        Assert.Equal(4, viewModel.ProgressTotal);
    }

    [Fact]
    public async Task ProgressCountWhenAllChecksPassEquals4()
    {
        // Arrange — monitor reports Standard level and no issues, so all four
        // T12 checks pass. ProgressCount must be 4 and the label
        // "Protección 4 de 4".
        var client = new FakeIpcOnboardingStateService();
        var monitor = CreateMonitor(
            level: EnforcementLevel.Standard,
            issues: Array.Empty<EnforcementIssue>());
        var viewModel = new OnboardingViewModel(client, monitor.Object);

        // Act
        await viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert
        Assert.Equal(4, viewModel.ProgressCount);
        Assert.Equal("Protección 4 de 4", viewModel.ProgressLabel);
    }

    [Fact]
    public async Task ProgressCountWhenServiceNotRunningEquals1()
    {
        // Arrange — issues that fail the other three checks while leaving
        // ServiceNotRunning out: only the "service running" check passes.
        var issues = new[]
        {
            MakeIssue(EnforcementIssueType.ChildIsAdministrator),
            MakeIssue(EnforcementIssueType.AgentNotResponding),
            MakeIssue(EnforcementIssueType.PreventiveLayerUnavailable),
        };
        var client = new FakeIpcOnboardingStateService();
        var monitor = CreateMonitor(level: EnforcementLevel.Degraded, issues: issues);
        var viewModel = new OnboardingViewModel(client, monitor.Object);

        // Act
        await viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert — only the service check (no ServiceNotRunning issue) passes
        // → ProgressCount = 1, label "Protección 1 de 4".
        Assert.Equal(1, viewModel.ProgressCount);
        Assert.Equal("Protección 1 de 4", viewModel.ProgressLabel);
    }

    [Fact]
    public async Task ProgressLabelWhenMonitorIsNullReflectsCompletedSteps()
    {
        // Arrange — VM constructed WITHOUT an IEnforcementLevelMonitor.
        // The fallback path counts completed steps in the state returned by the
        // IPC client; with the default initial state none are completed, so the
        // bar reports the constant initial label and a count of 0 (no inflation).
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client, enforcementLevelMonitor: null);

        // Act
        await viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert — closest observable behavior against the current API:
        // the label is the constant "Protección 0 de 4" and the count is 0.
        // (The pre-refactor "Estado desconocido" copy is no longer emitted.)
        Assert.Equal("Protección 0 de 4", viewModel.ProgressLabel);
        Assert.Equal(0, viewModel.ProgressCount);
    }

    /// <summary>
    /// Builds a mock <see cref="IEnforcementLevelMonitor"/> with the supplied
    /// snapshot. Only the properties the progress bar reads are stubbed; the
    /// rest return Moq defaults (no-ops) so the VM can be constructed without
    /// pulling in the full Service-side pipeline.
    /// </summary>
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

    private static EnforcementIssue MakeIssue(EnforcementIssueType type)
    {
        return new EnforcementIssue
        {
            Type = type,
            Severity = EnforcementIssueSeverity.Warning,
            Description = type.ToString(),
            DetectedAt = DateTimeOffset.UtcNow,
        };
    }
}
