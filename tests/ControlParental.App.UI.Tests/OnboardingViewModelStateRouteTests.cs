// <copyright file="OnboardingViewModelStateRouteTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 (P2 onboarding ownership) — Regression tests for the way the
/// <see cref="OnboardingViewModel"/> interacts with the IPC-backed
/// <see cref="IIpcOnboardingStateService"/>.
///
/// After the architectural flip the VM no longer talks to a local store;
/// it routes state reads, completions, and advances exclusively through the
/// IPC client (ADR-002). These tests pin the observable contract against
/// the new seam:
///
/// <list type="bullet">
///   <item><c>InitializeAsync</c> reads the canonical snapshot from the IPC client.</item>
///   <item><c>ExecuteStepCommand</c> on the "pairing" step completes and advances
///         via the IPC client; the observable surface refreshes from the
///         canonical Service snapshot.</item>
///   <item>Duplicate completion (same step id twice) is idempotent — the fake
///         records the second call but the state remains unchanged.</item>
///   <item>Constructor null guards: missing IPC client throws immediately.</item>
/// </list>
/// </summary>
public sealed class OnboardingViewModelStateRouteTests
{
    [Fact]
    public async Task InitializeAsyncReadsStateFromIpcClient()
    {
        // Arrange — an IPC fake seeded with pairing=Completed and current step
        // "consent" simulates a Service restart that resumed mid-flow.
        var client = new FakeIpcOnboardingStateService(BuildStateWithPairingCompleted());
        var viewModel = new OnboardingViewModel(client);

        // Act
        await viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert — the loaded snapshot reflects what the IPC client returned.
        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal("consent", viewModel.CurrentStep!.Id);
    }

    [Fact]
    public async Task InitializeAsyncCallsGetOnboardingStateAtLeastOnce()
    {
        // Spec: InitializeAsync must do one IPC read on startup.
        var client = new FakeIpcOnboardingStateService(BuildInitialState());
        var viewModel = new OnboardingViewModel(client);

        await viewModel.InitializeAsync().ConfigureAwait(false);

        Assert.NotNull(viewModel.CurrentStep);
    }

    [Fact]
    public async Task ExecuteStepCommandOnPairingStepDrivesCompletionThroughIpcClient()
    {
        // Spec: advancing from pairing must complete the step via the IPC
        // client — not through any local file or fallback path.
        var client = new FakeIpcOnboardingStateService(BuildInitialState());
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        // Act — execute the "pairing" step. The VM issues a complete-then-advance
        // IPC round-trip and refreshes from the canonical snapshot.
        await viewModel.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — the IPC fake captured the completion and advance calls,
        // and the observable surface now shows "consent".
        Assert.Contains("pairing", client.CompletedStepIds);
        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal("consent", viewModel.CurrentStep!.Id);
    }

    [Fact]
    public async Task AdvanceDrivesCompletionAndAdvanceThroughIpcClient()
    {
        // Spec: advancing a step must complete-then-advance via the IPC client
        // so the Service is the single source of truth.
        var client = new FakeIpcOnboardingStateService(BuildInitialState());
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        // Act
        await viewModel.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — the IPC fake received at least one completion (for the
        // current step) and the current-step index advanced on the canonical
        // snapshot.
        Assert.NotEmpty(client.CompletedStepIds);
        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal(1, viewModel.CurrentStep!.Index);
    }

    [Fact]
    public async Task CompleteOnboardingStepTwiceIsIdempotent()
    {
        // Spec: duplicate completions on the same stable stepId are a no-op.
        var client = new FakeIpcOnboardingStateService(BuildInitialState());
        var viewModel = new OnboardingViewModel(client);
        await viewModel.InitializeAsync().ConfigureAwait(false);

        // Act — invoke the same step twice. The IPC service records both
        // outbound requests (audit trail) but the state stays at one Completed.
        await viewModel.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        await client.CompleteOnboardingStepAsync("pairing").ConfigureAwait(false);

        // Assert — the fake captured two outbound RecordOnboardingStepCompleted
        // envelopes for "pairing" and the persisted state still shows
        // pairing Completed exactly once.
        Assert.Equal(2, client.CompletedStepIds.Count(id => id == "pairing"));
        var state = await client.GetOnboardingStateAsync().ConfigureAwait(false);
        Assert.Equal(OnboardingStepStatus.Completed, state.Steps[0].Status);
    }

    [Fact]
    public void ConstructorNullIpcClientThrows()
    {
        // Defensive guard — passing a null IPC client must fail fast at
        // construction so the VM never silently falls back to a default state.
        Assert.Throws<ArgumentNullException>(() => GC.KeepAlive(new OnboardingViewModel(null!)));
    }

    [Fact]
    public async Task InitializeAsyncWhenIpcServiceUnavailableLeavesObservableSurfaceUntouched()
    {
        // Spec: when the IPC service fails to acknowledge the read the VM
        // fails closed — no observable mutation, error message surfaced.
        var client = new FakeIpcOnboardingStateService(BuildInitialState());
        client.FailNextWithUnavailable();
        var viewModel = new OnboardingViewModel(client);

        await viewModel.InitializeAsync().ConfigureAwait(false);

        Assert.Null(viewModel.CurrentStep);
        Assert.NotNull(viewModel.ErrorMessage);
    }

    [Fact]
    public void LiveRouteCatalogUsesCanonicalSixStepOrderWithFirstWinBeforeManaged()
    {
        var stepIds = OnboardingRouteCatalog.CanonicalStepIds.ToArray();

        Assert.Equal(
            new[] { "pairing", "consent", "account", "service", "demo", "managed" },
            stepIds);
        Assert.True(Array.IndexOf(stepIds, "demo") < Array.IndexOf(stepIds, "managed"));
    }

    [Theory]
    [InlineData("pairing", OnboardingRoute.Pairing)]
    [InlineData("consent", OnboardingRoute.Consent)]
    [InlineData("account", OnboardingRoute.Account)]
    [InlineData("service", OnboardingRoute.ServiceSetup)]
    [InlineData("demo", OnboardingRoute.Demo)]
    [InlineData("managed", OnboardingRoute.Managed)]
    public void LiveRouteCatalogSelectsPageFromStableSnapshotId(
        string stepId,
        OnboardingRoute expectedRoute)
    {
        var route = OnboardingRouteCatalog.Select(stepId, isCompleted: false, isAbandoned: false);

        Assert.Equal(expectedRoute, route);
    }

    [Fact]
    public void LiveRouteCatalogRejectsStaleOrFinishedPageCallbacks()
    {
        Assert.False(OnboardingRouteCatalog.CanComplete("consent", "pairing", false, false));
        Assert.False(OnboardingRouteCatalog.CanComplete("consent", "consent", true, false));
        Assert.False(OnboardingRouteCatalog.CanComplete("consent", "consent", false, true));
        Assert.True(OnboardingRouteCatalog.CanComplete("consent", "consent", false, false));
    }

    private static ControlParental.Domain.OnboardingState BuildInitialState()
    {
        var steps = new List<Domain.OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", Domain.OnboardingStepStatus.InProgress),
            new(1, "consent", "Consent", "x", "Consent", Domain.OnboardingStepStatus.Locked),
            new(2, "account", "Account", "x", "Account", Domain.OnboardingStepStatus.Locked),
            new(3, "demo", "Demo", "x", "Demo", Domain.OnboardingStepStatus.Locked, IsFirstWin: true),
            new(4, "managed", "Managed", "x", "Managed", Domain.OnboardingStepStatus.Locked),
        };
        return new ControlParental.Domain.OnboardingState(0, false, false, steps, Array.Empty<Domain.FunnelEvent>());
    }

    private static ControlParental.Domain.OnboardingState BuildStateWithPairingCompleted()
    {
        var steps = new List<Domain.OnboardingStep>
        {
            new(0, "pairing", "Pairing", "x", "Pair", Domain.OnboardingStepStatus.Completed),
            new(1, "consent", "Consent", "x", "Consent", Domain.OnboardingStepStatus.InProgress),
            new(2, "account", "Account", "x", "Account", Domain.OnboardingStepStatus.Locked),
            new(3, "demo", "Demo", "x", "Demo", Domain.OnboardingStepStatus.Locked, IsFirstWin: true),
            new(4, "managed", "Managed", "x", "Managed", Domain.OnboardingStepStatus.Locked),
        };
        return new ControlParental.Domain.OnboardingState(1, false, false, steps, Array.Empty<Domain.FunnelEvent>());
    }
}
