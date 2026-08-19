// <copyright file="OnboardingViewModelIpcOwnershipTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 PR (P2 onboarding ownership) — Coverage-focused tests for the
/// <see cref="OnboardingViewModel"/> IPC paths that were not previously
/// exercised by the legacy store-backed VM:
///
/// <list type="bullet">
///   <item><c>GoBackAsync</c> — Service-backed back-navigation through the IPC client.</item>
///   <item><c>AbandonAsync</c> — fail-closed when the Service does not acknowledge.</item>
///   <item><c>ExecuteStepAsync</c> per-step routing through the IPC client.</item>
///   <item><c>InitializeAsync</c> when the Service reports a non-completed,
///         non-abandoned state with a current step already in InProgress.</item>
/// </list>
/// </summary>
[Collection(ConsoleTestCollection.Name)]
public sealed class OnboardingViewModelIpcOwnershipTests
{
    [Fact]
    public async Task GoBackAsyncDrivesIpcCallAndRefreshesSurface()
    {
        // Arrange — start on step index 1 (consent InProgress) so CanGoBack is
        // enabled; the VM must issue a Service round-trip and refresh from the
        // canonical snapshot the fake returns.
        var client = new FakeIpcOnboardingStateService(BuildStateWithConsentInProgress());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        // Act
        await vm.GoBackCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — the VM refreshed from the canonical snapshot the fake
        // returned for AdvanceOnboardingStep.
        Assert.Equal(0, vm.CurrentStep?.Index);
    }

    [Fact]
    public async Task GoBackAsyncWhenIpcServiceUnavailableKeepsStateUntouched()
    {
        // Arrange
        var client = new FakeIpcOnboardingStateService(BuildStateWithConsentInProgress());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);
        var initialStepIndex = vm.CurrentStep?.Index;

        client.FailNextWithUnavailable();

        // Act
        await vm.GoBackCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — fail-closed: observable surface untouched, error surfaced.
        Assert.Equal(initialStepIndex, vm.CurrentStep?.Index);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task AbandonAsyncWhenIpcServiceUnavailableKeepsStateUntouched()
    {
        // Arrange
        var client = new FakeIpcOnboardingStateService();
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        client.FailNextWithUnavailable();

        // Act
        await vm.AbandonCommand.ExecuteAsync(null).ConfigureAwait(false);

        // Assert — fail-closed: not abandoned, error surfaced.
        Assert.False(vm.IsAbandoned);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteStepAsyncAccountStepRoutesThroughIpc()
    {
        // Arrange — start on the "account" step so the switch dispatches there.
        var client = new FakeIpcOnboardingStateService(BuildStateAtAccountStep());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        // Act — ExecuteStepAsync for "account" calls OpenMsSettings (no IPC
        // completion), but the VM stays ready for a follow-up GoNextAsync.
        await vm.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — no completion was sent to the IPC service (OpenMsSettings
        // is a non-mutating launcher), and the VM did not advance.
        Assert.Empty(client.CompletedStepIds);
        Assert.Equal("account", vm.CurrentStep?.Id);
    }

    [Fact]
    public async Task ExecuteStepAsyncServiceStepDoesNotInvokeIpc()
    {
        // Spec anchor: the legacy "service" step is a Windows-settings launcher
        // (OpenMsSettings) — no IPC completion is expected.
        var client = new FakeIpcOnboardingStateService(BuildStateAtServiceStep());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        await vm.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.Empty(client.CompletedStepIds);
        Assert.Equal("service", vm.CurrentStep?.Id);
    }

    [Fact]
    public async Task ExecuteStepAsyncManagedStepDoesNotInvokeIpc()
    {
        // Spec anchor: the legacy "managed" step is a Windows-settings launcher
        // (OpenMsSettings) — no IPC completion is expected.
        var client = new FakeIpcOnboardingStateService(BuildStateAtManagedStep());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        await vm.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.Empty(client.CompletedStepIds);
        Assert.Equal("managed", vm.CurrentStep?.Id);
    }

    [Fact]
    public async Task ExecuteStepAsyncConsentStepIsNoLongerHandledByVm()
    {
        // Spec anchor: Unit 3 deleted the VM-side consent dispatch because the
        // live flow presents consent through the in-app ConsentPage. The VM
        // must NOT auto-advance the consent step; the page wires its own
        // completion callback through MainWindow.OnStepCompletedAsync.
        var client = new FakeIpcOnboardingStateService(BuildStateAtConsentStep());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        await vm.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — no IPC completion and observable surface unchanged. The
        // canonical advance goes through the guarded callback in production.
        Assert.Empty(client.CompletedStepIds);
        Assert.Equal("consent", vm.CurrentStep?.Id);
    }

    [Fact]
    public async Task GoNextAsyncWhenServiceUnavailableDoesNotMutate()
    {
        // Arrange — start on pairing InProgress so CanGoNext is true.
        var client = new FakeIpcOnboardingStateService();
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        // Fail the next two IPC calls (CompleteOnboardingStepAsync +
        // AdvanceOnboardingStepAsync) so the VM must surface the error and
        // refuse to advance.
        client.FailNextWithUnavailable(count: 2);

        // Act
        await vm.GoNextCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — fail-closed: current step unchanged, error surfaced.
        Assert.Equal("pairing", vm.CurrentStep?.Id);
        Assert.NotNull(vm.ErrorMessage);
    }

    [Fact]
    public async Task RetryAsyncRefreshesCanonicalSnapshotAfterFailedMutation()
    {
        // Arrange — the first mutate round-trip fails on the advance boundary,
        // leaving the visible surface on the last acknowledged snapshot.
        var client = new FakeIpcOnboardingStateService();
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        client.FailNextWithUnavailable(count: 1);

        await vm.GoNextCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        Assert.Equal("pairing", vm.CurrentStep?.Id);
        Assert.NotNull(vm.ErrorMessage);

        // The Service later acknowledges the canonical state; retry must read it.
        await client.AdvanceOnboardingStepAsync().ConfigureAwait(false);

        // Act
        await vm.RetryCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — surface refreshed only after the acknowledged read succeeds.
        Assert.Null(vm.ErrorMessage);
        Assert.NotNull(vm.CurrentStep);
        Assert.Equal("consent", vm.CurrentStep!.Id);
    }

    [Fact]
    public async Task InitializeAsyncWhenStateIsAbandonedLoadsLastStep()
    {
        // Arrange — Service reports an abandoned state with all steps still
        // present; the VM must surface IsAbandoned=true and the last step.
        var client = new FakeIpcOnboardingStateService(BuildAbandonedState());
        var vm = new OnboardingViewModel(client);

        await vm.InitializeAsync().ConfigureAwait(false);

        Assert.True(vm.IsAbandoned);
        Assert.NotNull(vm.CurrentStep);
        Assert.Equal("managed", vm.CurrentStep!.Id);
    }

    private static OnboardingState BuildStateWithConsentInProgress()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
            new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.InProgress),
            new(2, "account", "Account", "x", "Account", OnboardingStepStatus.Locked),
            new(3, "demo", "Demo", "x", "Demo", OnboardingStepStatus.Locked, IsFirstWin: true),
            new(4, "managed", "Managed", "x", "Managed", OnboardingStepStatus.Locked),
        };
        return new OnboardingState(1, false, false, steps, Array.Empty<FunnelEvent>());
    }

    private static OnboardingState BuildStateAtAccountStep()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
            new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.Completed),
            new(2, "account", "Account", "x", "Account", OnboardingStepStatus.InProgress),
            new(3, "demo", "Demo", "x", "Demo", OnboardingStepStatus.Locked, IsFirstWin: true),
            new(4, "managed", "Managed", "x", "Managed", OnboardingStepStatus.Locked),
        };
        return new OnboardingState(2, false, false, steps, Array.Empty<FunnelEvent>());
    }

    private static OnboardingState BuildStateAtServiceStep()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
            new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.Completed),
            new(2, "account", "Account", "x", "Account", OnboardingStepStatus.Completed),
            new(3, "service", "Service", "x", "Service", OnboardingStepStatus.InProgress),
            new(4, "managed", "Managed", "x", "Managed", OnboardingStepStatus.Locked),
        };
        return new OnboardingState(3, false, false, steps, Array.Empty<FunnelEvent>());
    }

    private static OnboardingState BuildStateAtManagedStep()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
            new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.Completed),
            new(2, "account", "Account", "x", "Account", OnboardingStepStatus.Completed),
            new(3, "service", "Service", "x", "Service", OnboardingStepStatus.Completed),
            new(4, "managed", "Managed", "x", "Managed", OnboardingStepStatus.InProgress),
        };
        return new OnboardingState(4, false, false, steps, Array.Empty<FunnelEvent>());
    }

    private static OnboardingState BuildStateAtConsentStep()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
            new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.InProgress),
            new(2, "account", "Account", "x", "Account", OnboardingStepStatus.Locked),
        };
        return new OnboardingState(1, false, false, steps, Array.Empty<FunnelEvent>());
    }

    private static OnboardingState BuildAbandonedState()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
            new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.Completed),
            new(2, "account", "Account", "x", "Account", OnboardingStepStatus.Completed),
            new(3, "demo", "Demo", "x", "Demo", OnboardingStepStatus.Completed, IsFirstWin: true),
            new(4, "managed", "Managed", "x", "Managed", OnboardingStepStatus.Pending),
        };
        return new OnboardingState(4, false, true, steps, Array.Empty<FunnelEvent>());
    }
}
