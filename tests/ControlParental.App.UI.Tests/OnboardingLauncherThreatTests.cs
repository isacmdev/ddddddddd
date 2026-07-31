// <copyright file="OnboardingLauncherThreatTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Moq;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T25/T26 (Unit 3) — RED tests for the launcher-threat scenario described in
/// <c>sdd/t26-live-flow-closure/design</c> (threat matrix: "Windows
/// URI/elevation process activation"). Pending steps launch only fixed,
/// internally selected targets; launch failure leaves the step pending and
/// requires Service-side re-verification on return. These tests pin the
/// observable contract against the live IPC-backed onboarding state machine.
/// </summary>
public sealed class OnboardingLauncherThreatTests
{
    [Fact]
    public async Task LauncherThreatOnAccountStepDoesNotAdvanceIpcCompletion()
    {
        // Arrange — start on the "account" step, which the VM dispatches
        // through OpenMsSettings (not IPC). The threat contract is that the
        // launcher never auto-records completion of the canonical step;
        // completion only flows through the guarded MainWindow route callback.
        var client = new FakeIpcOnboardingStateService(BuildAccountInProgressState());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        // Act — even if the launcher somehow throws, no completion must be
        // recorded on the IPC service and the observable step index must
        // remain at the account index.
        await vm.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — observable surface is unchanged and no outbound completion
        // was recorded against the IPC client.
        Assert.Empty(client.CompletedStepIds);
        Assert.Equal("account", vm.CurrentStep?.Id);
    }

    [Fact]
    public async Task LauncherThreatOnServiceStepDoesNotAdvanceIpcCompletion()
    {
        var client = new FakeIpcOnboardingStateService(BuildServiceStepInProgressState());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        await vm.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.Empty(client.CompletedStepIds);
        Assert.Equal("service", vm.CurrentStep?.Id);
    }

    [Fact]
    public async Task LauncherThreatOnManagedStepDoesNotAdvanceIpcCompletion()
    {
        var client = new FakeIpcOnboardingStateService(BuildManagedStepInProgressState());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        await vm.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.Empty(client.CompletedStepIds);
        Assert.Equal("managed", vm.CurrentStep?.Id);
    }

    [Fact]
    public async Task LauncherThreatAfterSettingsReturnRequiresIpcCompletionToAdvance()
    {
        // Arrange — start on the "service" step with no demo/service
        // completion recorded. The threat contract is that returning from
        // the settings deep-link must NOT advance the step until the user
        // explicitly completes setup verification (which the live shell
        // achieves through ServiceSetupPage's RequestOrVerifyServiceSetupAsync
        // round-trip + the guarded coordinator completion).
        var client = new FakeIpcOnboardingStateService(BuildServiceStepInProgressState());
        var vm = new OnboardingViewModel(client);
        await vm.InitializeAsync().ConfigureAwait(false);

        // Act — simulate the launcher open + return cycle. The VM should
        // remain at the "service" step because no IPC completion has been
        // recorded.
        await vm.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — still at "service", never auto-advanced.
        Assert.Equal("service", vm.CurrentStep?.Id);
        Assert.Empty(client.CompletedStepIds);
    }

    private static OnboardingState BuildAccountInProgressState()
    {
        return new OnboardingState(
            CurrentStepIndex: 2,
            IsCompleted: false,
            IsAbandoned: false,
            Steps: new List<OnboardingStep>
            {
                new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
                new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.Completed),
                new(2, "account", "Account", "x", "Account", OnboardingStepStatus.InProgress),
                new(3, "service", "Service", "x", "Verify", OnboardingStepStatus.Locked),
                new(4, "demo", "Demo", "x", "Demo", OnboardingStepStatus.Locked, IsFirstWin: true),
                new(5, "managed", "Managed", "x", "Managed", OnboardingStepStatus.Locked),
            },
            Events: Array.Empty<FunnelEvent>());
    }

    private static OnboardingState BuildServiceStepInProgressState()
    {
        return new OnboardingState(
            CurrentStepIndex: 3,
            IsCompleted: false,
            IsAbandoned: false,
            Steps: new List<OnboardingStep>
            {
                new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
                new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.Completed),
                new(2, "account", "Account", "x", "Account", OnboardingStepStatus.Completed),
                new(3, "service", "Service", "x", "Verify", OnboardingStepStatus.InProgress),
                new(4, "demo", "Demo", "x", "Demo", OnboardingStepStatus.Locked, IsFirstWin: true),
                new(5, "managed", "Managed", "x", "Managed", OnboardingStepStatus.Locked),
            },
            Events: Array.Empty<FunnelEvent>());
    }

    private static OnboardingState BuildManagedStepInProgressState()
    {
        return new OnboardingState(
            CurrentStepIndex: 5,
            IsCompleted: false,
            IsAbandoned: false,
            Steps: new List<OnboardingStep>
            {
                new(0, "pairing", "Pairing", "x", "Pair", OnboardingStepStatus.Completed),
                new(1, "consent", "Consent", "x", "Consent", OnboardingStepStatus.Completed),
                new(2, "account", "Account", "x", "Account", OnboardingStepStatus.Completed),
                new(3, "service", "Service", "x", "Verify", OnboardingStepStatus.Completed),
                new(4, "demo", "Demo", "x", "Demo", OnboardingStepStatus.Completed, IsFirstWin: true),
                new(5, "managed", "Managed", "x", "Managed", OnboardingStepStatus.InProgress),
            },
            Events: Array.Empty<FunnelEvent>());
    }
}
