// <copyright file="OnboardingViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System.Threading.Tasks;
using ControlParental.Domain;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 — Smoke tests for the IPC-backed <see cref="OnboardingViewModel"/>.
/// The richer behavioural coverage lives in
/// <c>OnboardingViewModelStateRouteTests</c>, <c>OnboardingViewModelIpcOwnershipTests</c>,
/// <c>OnboardingViewModelProgressTests</c>, and <c>OnboardingViewModelFunnelTests</c>.
/// This file preserves the smoke surface that the original onboarding unit
/// suite exposed (without the legacy <c>ConsentDialog</c> + local-store
/// constructor variant that t26 Unit 3 retired).
/// </summary>
public sealed class OnboardingViewModelTests
{
    [Fact]
    public async Task InitializeAsync_WhenNoState_ReadsInitialStateFromIpcClient()
    {
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);

        await viewModel.InitializeAsync().ConfigureAwait(false);

        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal("pairing", viewModel.CurrentStep!.Id);
        Assert.Equal(0, viewModel.CurrentStep.Index);
    }

    [Fact]
    public async Task InitializeAsync_ReadsSnapshotPersistedByClient()
    {
        // The IPC client is the single source of truth: completion and route
        // advancement are separate Service-owned transitions.
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);

        var completed = await client.CompleteOnboardingStepAsync("pairing").ConfigureAwait(false);
        Assert.Equal(0, completed.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.Completed, completed.Steps[0].Status);

        await client.AdvanceOnboardingStepAsync().ConfigureAwait(false);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal("consent", viewModel.CurrentStep!.Id);
    }

    [Fact]
    public async Task GoNextCommand_AdvancesCurrentStepIndex()
    {
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        var initialIndex = viewModel.CurrentStep!.Index;
        await viewModel.GoNextCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.NotEqual(initialIndex, viewModel.CurrentStep?.Index);
        Assert.Equal(1, viewModel.CurrentStep?.Index);
    }

    [Fact]
    public async Task RecordFunnelEvent_AddsEventToStateSnapshot()
    {
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        var state = await client.GetOnboardingStateAsync().ConfigureAwait(false);

        Assert.NotEmpty(state.Events);
        Assert.Contains(
            state.Events,
            e => e.Type == FunnelEventType.OnboardingStepReached && e.StepId == "pairing");
    }

    [Fact]
    public async Task ProgressLabel_BeforeAnyMonitor_IsZeroOfFour()
    {
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client, enforcementLevelMonitor: null);

        await viewModel.InitializeAsync().ConfigureAwait(false);

        Assert.Equal("Protección 0 de 4", viewModel.ProgressLabel);
        Assert.Equal(0, viewModel.ProgressCount);
    }

    [Fact]
    public async Task CurrentStep_AfterInit_IsPairingStep()
    {
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal("pairing", viewModel.CurrentStep.Id);
    }

    [Fact]
    public async Task CanGoNext_IsFalseInitially_WhenStepNotCompleted()
    {
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        Assert.False(viewModel.CanGoNext);
    }

    [Fact]
    public async Task CanGoBack_IsFalse_WhenOnFirstStep()
    {
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        Assert.False(viewModel.CanGoBack);
    }

    [Fact]
    public async Task Abandon_MarksIsAbandonedTrue()
    {
        var client = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        await viewModel.AbandonCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.True(viewModel.IsAbandoned);
    }
}
