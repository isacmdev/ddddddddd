// <copyright file="OnboardingViewModelFunnelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Moq;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 (verify remediation) — fail-closed acknowledgement tests for the
/// funnel telemetry path in <see cref="OnboardingViewModel"/>.
///
/// The T26 design mandates that both <c>RecordFunnelEventAsync</c> and
/// <c>RequestOrVerifyServiceSetupAsync</c> fail closed on a missing or
/// invalid IPC acknowledgement. Earlier revisions silently swallowed
/// <see cref="ConsentServiceUnavailableException"/> in
/// <c>RecordFunnelEventAsync</c>, which violated the design's fail-closed
/// wording and let Service outages disappear without operator visibility.
/// These tests pin the new contract: every funnel call that fails over IPC
/// surfaces the error through <see cref="OnboardingViewModel.ErrorMessage"/>
/// while leaving the canonical step transition intact (the transition itself
/// was acknowledged by the Service before the funnel call).
/// </summary>
public sealed class OnboardingViewModelFunnelTests
{
    [Fact]
    public async Task RecordFunnelEventAsyncWhenIpcFailsSurfacesErrorMessageWithoutRegressingStepTransition()
    {
        // Arrange — Moq-based client that succeeds for every IPC call except
        // RecordFunnelEventAsync, which throws ConsentServiceUnavailableException
        // to simulate a Service outage on the telemetry pipeline.
        var seed = BuildInitialState();
        var mock = BuildMockClient(
            seed: seed,
            funnelBehavior: SetupFunnelFailure("Funnel pipeline unavailable."));
        var viewModel = new OnboardingViewModel(mock.Object);

        // Act — InitializeAsync must call GetOnboardingStateAsync (succeeds)
        // and then RecordFunnelEventAsync (fails). The canonical step
        // transition is unchanged; the funnel failure surfaces as ErrorMessage.
        await viewModel.InitializeAsync().ConfigureAwait(false);

        // Assert — the live step is still pairing (GetOnboardingStateAsync was
        // acknowledged) and the funnel IPC failure is visible to the operator.
        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal("pairing", viewModel.CurrentStep!.Id);
        Assert.Equal("Funnel pipeline unavailable.", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RecordFunnelEventAsyncOnGoNextSurfacesFunnelFailureAfterAcknowledgedAdvance()
    {
        // Arrange — InitializeAsync succeeds, GoNextAsync advances through
        // acknowledged CompleteOnboardingStepAsync + AdvanceOnboardingStepAsync,
        // and the post-advance RecordFunnelEventAsync fails. The canonical
        // advance must complete; the funnel failure surfaces.
        var seed = BuildInitialState();
        var mock = BuildMockClient(
            seed: seed,
            funnelBehavior: SetupFunnelFailure("Funnel pipeline unavailable."));
        var viewModel = new OnboardingViewModel(mock.Object);

        await viewModel.InitializeAsync().ConfigureAwait(false);
        viewModel.ErrorMessage = null; // Clear the funnel failure from init so we can prove GoNext surfaces its own.

        // Act
        await viewModel.GoNextCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        // Assert — the canonical advance ran (CurrentStep is now consent)
        // AND the funnel IPC failure is surfaced.
        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal("consent", viewModel.CurrentStep!.Id);
        Assert.Equal("Funnel pipeline unavailable.", viewModel.ErrorMessage);
    }

    private static OnboardingState BuildInitialState()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Pairing", string.Empty, string.Empty, OnboardingStepStatus.InProgress),
            new(1, "consent", "Consent", string.Empty, string.Empty, OnboardingStepStatus.Locked),
            new(2, "account", "Account", string.Empty, string.Empty, OnboardingStepStatus.Locked),
            new(3, "service", "Service", string.Empty, string.Empty, OnboardingStepStatus.Locked),
            new(4, "demo", "Demo", string.Empty, string.Empty, OnboardingStepStatus.Locked, IsFirstWin: true),
            new(5, "managed", "Managed", string.Empty, string.Empty, OnboardingStepStatus.Locked),
        };
        return new OnboardingState(0, false, false, steps, Array.Empty<FunnelEvent>());
    }

    private static Mock<IIpcOnboardingStateService> BuildMockClient(
        OnboardingState seed,
        Action<Mock<IIpcOnboardingStateService>> funnelBehavior)
    {
        var mock = new Mock<IIpcOnboardingStateService>();
        var workingState = seed;
        var currentStepIndex = seed.CurrentStepIndex;

        mock.Setup(m => m.GetOnboardingStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => workingState);

        mock.Setup(m => m.CompleteOnboardingStepAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string stepId, CancellationToken _) =>
            {
                var steps = workingState.Steps.ToList();
                for (int i = 0; i < steps.Count; i++)
                {
                    if (string.Equals(steps[i].Id, stepId, StringComparison.Ordinal)
                        && steps[i].Status != OnboardingStepStatus.Completed)
                    {
                        steps[i] = steps[i] with { Status = OnboardingStepStatus.Completed };
                    }
                }

                workingState = workingState with { Steps = steps };
                return workingState;
            });

        mock.Setup(m => m.AdvanceOnboardingStepAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var nextIndex = currentStepIndex + 1;
                if (nextIndex >= workingState.Steps.Count)
                {
                    workingState = workingState with { IsCompleted = true };
                    return workingState;
                }

                var steps = workingState.Steps.ToList();
                for (int i = 0; i < steps.Count; i++)
                {
                    if (steps[i].Index < nextIndex)
                    {
                        steps[i] = steps[i] with { Status = OnboardingStepStatus.Completed };
                    }
                    else if (steps[i].Index == nextIndex)
                    {
                        steps[i] = steps[i] with { Status = OnboardingStepStatus.InProgress };
                    }
                }

                currentStepIndex = nextIndex;
                workingState = workingState with { CurrentStepIndex = nextIndex, Steps = steps };
                return workingState;
            });

        mock.Setup(m => m.ResetOnboardingStateAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                workingState = workingState with { IsAbandoned = true };
                return workingState;
            });

        funnelBehavior(mock);

        return mock;
    }

    private static Action<Mock<IIpcOnboardingStateService>> SetupFunnelFailure(string message)
    {
        return mock => mock
            .Setup(m => m.RecordFunnelEventAsync(It.IsAny<FunnelEventType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConsentServiceUnavailableException(message));
    }
}
