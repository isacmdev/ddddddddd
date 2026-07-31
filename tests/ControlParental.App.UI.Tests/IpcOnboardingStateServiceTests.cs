// <copyright file="IpcOnboardingStateServiceTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Moq;
using Xunit;

/// <summary>
/// T26 PR (P2 onboarding ownership) — Regression tests for the App.UI IPC proxy
/// <see cref="IpcOnboardingStateService"/>. The proxy is now the single
/// surface the <see cref="OnboardingViewModel"/> drives (read, complete,
/// advance, reset). The tests pin:
///
/// <list type="bullet">
///   <item>The wire payload (request message types, step id, optional reason).</item>
///   <item>The fail-closed contract: a null transport response surfaces
///         <see cref="ConsentServiceUnavailableException"/> instead of
///         silently returning a default snapshot.</item>
///   <item>Duplicate completion idempotency: a stable step id sent twice
///         still resolves to the same canonical snapshot.</item>
/// </list>
/// </summary>
public sealed class IpcOnboardingStateServiceTests
{
    [Fact]
    public async Task AdvanceOnboardingStepAsyncSendsCorrectMessageType()
    {
        // Arrange — channel records the outbound AdvanceOnboardingStep envelope.
        var channel = new MockNamedPipeUIChannel();
        var advanced = BuildState(currentIndex: 1, isCompleted: false);
        channel.OnAdvanceOnboardingStep(() => advanced);
        var service = new IpcOnboardingStateService(channel);

        // Act
        await service.AdvanceOnboardingStepAsync().ConfigureAwait(false);

        // Assert — the mock captured the App.UI AdvanceOnboardingStep request.
        Assert.NotNull(channel.LastAdvanceRequest);
        Assert.IsType<ControlParental.App.UI.AdvanceOnboardingStep>(channel.LastAdvanceRequest);
    }

    [Fact]
    public async Task AdvanceOnboardingStepAsyncReturnsNewState()
    {
        // Arrange — channel returns the post-advance snapshot.
        var channel = new MockNamedPipeUIChannel();
        var advanced = BuildState(currentIndex: 2, isCompleted: false);
        channel.OnAdvanceOnboardingStep(() => advanced);
        var service = new IpcOnboardingStateService(channel);

        // Act
        var returned = await service.AdvanceOnboardingStepAsync().ConfigureAwait(false);

        // Assert — proxy returns the snapshot the Service would have persisted.
        Assert.Equal(2, returned.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.InProgress, returned.Steps[2].Status);
    }

    [Fact]
    public async Task AdvanceOnboardingStepAsyncWhenServiceUnavailableThrows()
    {
        // Arrange — channel returns null on QueryAsync, simulating a Service
        // that did not acknowledge the request (pipe timeout, deserialization
        // failure, agent crashed).
        var channel = new Mock<IUIChannel>();
        channel
            .Setup(c => c.QueryAsync<
                ControlParental.App.UI.AdvanceOnboardingStep,
                ControlParental.App.UI.OnboardingStateResponse>(
                It.IsAny<ControlParental.App.UI.AdvanceOnboardingStep>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ControlParental.App.UI.OnboardingStateResponse?)null);
        var service = new IpcOnboardingStateService(channel.Object);

        // Act & Assert — proxy must surface ConsentServiceUnavailableException,
        // never silently succeed. The caller can then fail closed.
        await Assert.ThrowsAsync<ConsentServiceUnavailableException>(
            () => service.AdvanceOnboardingStepAsync()).ConfigureAwait(false);
    }

    [Fact]
    public async Task ResetOnboardingStateAsyncWithReasonSendsReason()
    {
        // Arrange — channel records the outbound ResetOnboardingState envelope
        // and asserts the reason made it onto the wire.
        var channel = new MockNamedPipeUIChannel();
        var fresh = BuildState(currentIndex: 0, isCompleted: false);
        channel.OnResetOnboardingState(req =>
        {
            Assert.Equal("audit-2026-001", req.Reason);
            return fresh;
        });
        var service = new IpcOnboardingStateService(channel);

        // Act
        await service.ResetOnboardingStateAsync("audit-2026-001").ConfigureAwait(false);

        // Assert — the mock captured the ResetOnboardingState request with the reason.
        Assert.NotNull(channel.LastResetRequest);
        Assert.Equal("audit-2026-001", channel.LastResetRequest!.Reason);
    }

    [Fact]
    public async Task ResetOnboardingStateAsyncWithoutReasonStillWorks()
    {
        // Arrange — channel accepts the reason-less request and returns fresh state.
        var channel = new MockNamedPipeUIChannel();
        var fresh = BuildState(currentIndex: 0, isCompleted: false);
        channel.OnResetOnboardingState(req =>
        {
            Assert.Null(req.Reason);
            return fresh;
        });
        var service = new IpcOnboardingStateService(channel);

        // Act
        var returned = await service.ResetOnboardingStateAsync().ConfigureAwait(false);

        // Assert
        Assert.Equal(0, returned.CurrentStepIndex);
        Assert.False(returned.IsCompleted);
        Assert.Null(channel.LastResetRequest!.Reason);
    }

    [Fact]
    public async Task ResetOnboardingStateAsyncWhenServiceUnavailableThrows()
    {
        // Arrange
        var channel = new Mock<IUIChannel>();
        channel
            .Setup(c => c.QueryAsync<
                ControlParental.App.UI.ResetOnboardingState,
                ControlParental.App.UI.OnboardingStateResponse>(
                It.IsAny<ControlParental.App.UI.ResetOnboardingState>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ControlParental.App.UI.OnboardingStateResponse?)null);
        var service = new IpcOnboardingStateService(channel.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ConsentServiceUnavailableException>(
            () => service.ResetOnboardingStateAsync("any reason")).ConfigureAwait(false);
    }

    [Fact]
    public void ConstructorNullChannelThrows()
    {
        // Defensive guard — NRE on a missing channel would surface deep inside
        // the proxy. Fail fast at construction time instead.
        Assert.Throws<ArgumentNullException>(() => GC.KeepAlive(new IpcOnboardingStateService(null!)));
    }

    // T26 PR (P2 onboarding ownership) — RED→GREEN contract tests for the new
    // GetOnboardingStateAsync / CompleteOnboardingStepAsync surface.
    [Fact]
    public async Task GetOnboardingStateAsyncReturnsCanonicalSnapshot()
    {
        // Arrange — channel returns the default snapshot (pairing Pending at
        // index 0). The proxy must surface the snapshot directly, not a null
        // sentinel.
        var channel = new MockNamedPipeUIChannel();
        var service = new IpcOnboardingStateService(channel);

        // Act
        var snapshot = await service.GetOnboardingStateAsync().ConfigureAwait(false);

        // Assert — the snapshot reflects what the Service would have persisted.
        Assert.Equal(0, snapshot.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.Pending, snapshot.Steps[0].Status);
    }

    [Fact]
    public async Task GetOnboardingStateAsyncWhenServiceUnavailableThrows()
    {
        // Arrange — channel returns null on QueryAsync, simulating a Service
        // that did not acknowledge the request (pipe timeout, deserialization
        // failure, agent crashed).
        var channel = new Mock<IUIChannel>();
        channel
            .Setup(c => c.QueryAsync<
                ControlParental.App.UI.GetOnboardingState,
                ControlParental.App.UI.OnboardingStateResponse>(
                It.IsAny<ControlParental.App.UI.GetOnboardingState>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ControlParental.App.UI.OnboardingStateResponse?)null);
        var service = new IpcOnboardingStateService(channel.Object);

        // Act & Assert — proxy must surface ConsentServiceUnavailableException,
        // never silently succeed. The caller can then fail closed.
        await Assert.ThrowsAsync<ConsentServiceUnavailableException>(
            () => service.GetOnboardingStateAsync()).ConfigureAwait(false);
    }

    [Fact]
    public async Task CompleteOnboardingStepAsyncSendsCorrectStepId()
    {
        // Arrange — channel captures the outbound RecordOnboardingStepCompleted
        // envelope so we can assert the stable step id was sent on the wire.
        var channel = new MockNamedPipeUIChannel();
        var snapshot = BuildState(currentIndex: 1, isCompleted: false);
        channel.OnRecordOnboardingStepCompleted(req =>
        {
            Assert.Equal("pairing", req.StepId);
            return snapshot;
        });
        var service = new IpcOnboardingStateService(channel);

        // Act
        var returned = await service.CompleteOnboardingStepAsync("pairing").ConfigureAwait(false);

        // Assert
        Assert.NotNull(channel.LastRecordOnboardingStepRequest);
        Assert.Equal("pairing", channel.LastRecordOnboardingStepRequest!.StepId);
        Assert.Equal(1, returned.CurrentStepIndex);
    }

    [Fact]
    public async Task CompleteOnboardingStepAsyncWhenServiceUnavailableThrows()
    {
        // Arrange — channel returns null on QueryAsync to simulate a Service
        // that did not acknowledge the completion.
        var channel = new Mock<IUIChannel>();
        channel
            .Setup(c => c.QueryAsync<
                ControlParental.App.UI.RecordOnboardingStepCompleted,
                ControlParental.App.UI.OnboardingStateResponse>(
                It.IsAny<ControlParental.App.UI.RecordOnboardingStepCompleted>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ControlParental.App.UI.OnboardingStateResponse?)null);
        var service = new IpcOnboardingStateService(channel.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ConsentServiceUnavailableException>(
            () => service.CompleteOnboardingStepAsync("pairing")).ConfigureAwait(false);
    }

    [Fact]
    public async Task CompleteOnboardingStepAsyncDuplicateCallIsIdempotentOnWire()
    {
        // Spec: duplicate completions on the same stable step id are a no-op
        // on the wire (the Service detects the already-Completed step and
        // does not re-persist). The proxy forwards both envelopes (so the
        // audit trail captures the second tap) and returns the same canonical
        // snapshot both times.
        var channel = new MockNamedPipeUIChannel();
        var snapshot = BuildState(currentIndex: 1, isCompleted: false);
        channel.OnRecordOnboardingStepCompleted(_ => snapshot);
        var service = new IpcOnboardingStateService(channel);

        // Act — invoke the same completion twice.
        var first = await service.CompleteOnboardingStepAsync("pairing").ConfigureAwait(false);
        var second = await service.CompleteOnboardingStepAsync("pairing").ConfigureAwait(false);

        // Assert — both responses reflect the canonical snapshot the fake
        // Service returned for the duplicate wire envelope.
        Assert.Equal(first.CurrentStepIndex, second.CurrentStepIndex);
        Assert.Equal(first.Steps[0].Status, second.Steps[0].Status);
    }

    [Fact]
    public async Task CompleteOnboardingStepAsyncEmptyStepIdThrows()
    {
        // Spec: the proxy must reject empty / whitespace step ids locally so
        // they never reach the wire.
        var channel = new MockNamedPipeUIChannel();
        var service = new IpcOnboardingStateService(channel);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.CompleteOnboardingStepAsync(string.Empty)).ConfigureAwait(false);
    }

    private static ControlParental.Domain.OnboardingState BuildState(int currentIndex, bool isCompleted)
    {
        var steps = new List<Domain.OnboardingStep>
        {
            new(0, "pairing", "Emparejar", "x", "x", currentIndex > 0 ? OnboardingStepStatus.Completed : OnboardingStepStatus.Pending),
            new(1, "consent", "Consentimiento", "x", "x", currentIndex > 1 ? OnboardingStepStatus.Completed : (currentIndex == 1 ? OnboardingStepStatus.InProgress : OnboardingStepStatus.Locked)),
            new(2, "account", "Cuenta", "x", "x", currentIndex > 2 ? OnboardingStepStatus.Completed : (currentIndex == 2 ? OnboardingStepStatus.InProgress : OnboardingStepStatus.Locked)),
            new(3, "demo", "Demo", "x", "x", currentIndex > 3 ? OnboardingStepStatus.Completed : (currentIndex == 3 ? OnboardingStepStatus.InProgress : OnboardingStepStatus.Locked), IsFirstWin: true),
            new(4, "managed", "Managed", "x", "x", currentIndex > 4 ? OnboardingStepStatus.Completed : (currentIndex == 4 ? OnboardingStepStatus.InProgress : OnboardingStepStatus.Locked)),
        };
        return new ControlParental.Domain.OnboardingState(currentIndex, isCompleted, false, steps, Array.Empty<Domain.FunnelEvent>());
    }
}
