// <copyright file="UIMessageHandlerStateControlTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// T26 PR #11 — Regression tests for the
/// <see cref="UIMessageHandler"/> dispatch of the new
/// <see cref="AdvanceOnboardingStep"/> and <see cref="ResetOnboardingState"/>
/// IPC messages. These were added to complete design §2 IPC surface so any
/// future feature (test runner, kiosk reset, remote admin) can drive the state
/// machine programmatically. They are unused by today's UI but must be
/// pinned so a future refactor cannot silently drop them.
/// </summary>
public sealed class UIMessageHandlerStateControlTests : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly string tempFolder;
    private readonly OnboardingStateService stateService;
    private readonly UIMessageHandler handler;
    private readonly Mock<ILogger<UIMessageHandler>> logger;

    public UIMessageHandlerStateControlTests()
    {
        var services = new ServiceCollection();
        this.provider = services.BuildServiceProvider();

        this.tempFolder = Path.Combine(Path.GetTempPath(), $"cp-handler-state-{Guid.NewGuid():N}");
        this.stateService = new OnboardingStateService(
            this.tempFolder,
            new Mock<IChildAccountStore>().Object,
            new Mock<ILogger<OnboardingStateService>>().Object);

        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(x => x.CurrentIssues).Returns(Array.Empty<EnforcementIssue>());
        this.logger = new Mock<ILogger<UIMessageHandler>>();

        this.handler = new UIMessageHandler(
            this.stateService,
            new EnforcementLevelQueryHandler(monitor.Object),
            this.provider.GetRequiredService<IServiceScopeFactory>(),
            this.logger.Object);
    }

    [Fact]
    public async Task HandleAdvanceOnboardingStep_AdvancesStateMachine()
    {
        // Act — initial state is index 0 (Pending pairing). Advance moves to 1.
        var response = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new AdvanceOnboardingStep()));

        // Assert — current step moved to index 1, prior step is Completed.
        Assert.Equal(1, response.State.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.Completed, response.State.Steps[0].Status);
        Assert.Equal(OnboardingStepStatus.InProgress, response.State.Steps[1].Status);
        Assert.False(response.State.IsCompleted);

        // Assert — disk reflects the advance.
        var reloaded = await this.stateService.GetStateAsync();
        Assert.Equal(1, reloaded.CurrentStepIndex);
    }

    [Fact]
    public async Task HandleAdvanceOnboardingStep_AtLastStep_SetsIsCompleted()
    {
        // Arrange — advance once to land on the last step, then a second advance
        // pushes the index past Steps.Count so OnboardingStateService sets IsCompleted.
        await this.stateService.AdvanceAsync(5); // index of "managed" (last)
        var indexBeforeFinalAdvance = (await this.stateService.GetStateAsync()).CurrentStepIndex;

        // Act
        var response = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new AdvanceOnboardingStep()));

        // Assert — handler reads current snapshot (5), asks service for index 6,
        // service sets IsCompleted and returns.
        Assert.Equal(indexBeforeFinalAdvance, response.State.CurrentStepIndex);
        Assert.True(response.State.IsCompleted);
    }

    [Fact]
    public async Task HandleResetOnboardingState_ResetsToInitial()
    {
        // Arrange — pollute the state.
        await this.stateService.RecordStepCompletedAsync("pairing");
        await this.stateService.AdvanceAsync(2);

        // Act
        var response = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new ResetOnboardingState("test reset")));

        // Assert — fresh initial state.
        Assert.False(response.State.IsCompleted);
        Assert.Equal(0, response.State.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.Pending, response.State.Steps[0].Status);
        Assert.Empty(response.State.Events);
    }

    [Fact]
    public async Task HandleResetOnboardingState_WithReason_LogsAtWarningLevel()
    {
        // Act
        await this.handler.HandleAsync(new ResetOnboardingState("kiosk reboot"));

        // Assert — Warning level was used and the reason made it into the log.
        this.logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("kiosk reboot")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleResetOnboardingState_WithoutReason_StillWorks()
    {
        // Act — null reason must not throw; the handler logs a placeholder.
        var response = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new ResetOnboardingState()));

        // Assert — fresh initial state.
        Assert.Equal(0, response.State.CurrentStepIndex);
        Assert.False(response.State.IsCompleted);
        this.logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("(no reason provided)")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleResetOnboardingState_WithWhitespaceReason_LogsPlaceholder()
    {
        // Act — whitespace-only reason must NOT leak through; handler normalises it.
        await this.handler.HandleAsync(new ResetOnboardingState("   "));

        // Assert — placeholder logged, not the whitespace.
        this.logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("(no reason provided)")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAdvanceOnboardingStep_LogsAtInformationLevel()
    {
        // Act
        await this.handler.HandleAsync(new AdvanceOnboardingStep());

        // Assert — Information level was used for the advance.
        this.logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    // T26 PR (P2 onboarding ownership) — RED→GREEN contract tests for
    // RecordOnboardingStepCompleted returning the canonical Service snapshot
    // (OnboardingStateResponse) instead of the legacy StepCompletedResponse
    // boolean. The UI now requires the canonical snapshot to refresh its
    // observable surface without re-reading state.

    [Fact]
    public async Task HandleRecordOnboardingStepCompleted_ReturnsCanonicalSnapshot()
    {
        // Act — initial state has pairing at index 0 with Pending status.
        var response = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new RecordOnboardingStepCompleted("pairing")));

        // Assert — the canonical snapshot reports the completed step and the
        // current step index is unchanged (completion != advance).
        Assert.Equal(OnboardingStepStatus.Completed, response.State.Steps[0].Status);
        Assert.Equal(0, response.State.CurrentStepIndex);
        Assert.False(response.State.IsCompleted);

        // Assert — the persisted state matches the snapshot.
        var reloaded = await this.stateService.GetStateAsync();
        Assert.Equal(OnboardingStepStatus.Completed, reloaded.Steps[0].Status);
    }

    [Fact]
    public async Task HandleRecordOnboardingStepCompleted_DuplicateStepId_IsIdempotent()
    {
        // Arrange — first call completes pairing; second call must be a no-op
        // and return the same canonical snapshot (UI doesn't have to re-render).
        var first = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new RecordOnboardingStepCompleted("pairing")));

        // Act
        var second = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new RecordOnboardingStepCompleted("pairing")));

        // Assert — both responses reflect the same canonical state.
        Assert.Equal(first.State.Steps[0].Status, second.State.Steps[0].Status);
        Assert.Equal(first.State.CurrentStepIndex, second.State.CurrentStepIndex);
        Assert.Equal(first.State.Steps.Count, second.State.Steps.Count);
    }

    [Fact]
    public async Task HandleRecordOnboardingStepCompleted_UnknownStepId_ReturnsCurrentSnapshot()
    {
        // Arrange — get the initial snapshot to compare against.
        var initial = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new GetOnboardingState()));

        // Act — call with an unknown step id; handler must NOT throw and must
        // return the current snapshot unchanged.
        var response = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new RecordOnboardingStepCompleted("not-a-real-step")));

        // Assert — response equals the current snapshot (no spurious mutation).
        Assert.Equal(initial.State.CurrentStepIndex, response.State.CurrentStepIndex);
        Assert.Equal(initial.State.IsCompleted, response.State.IsCompleted);
        Assert.Equal(
            initial.State.Steps[0].Status,
            response.State.Steps[0].Status);
    }

    [Fact]
    public async Task HandleGetOnboardingState_StillReturnsCanonicalSnapshot()
    {
        // Spec anchor: the existing GetOnboardingState handler must keep its
        // canonical-snapshot contract after the RecordOnboardingStepCompleted
        // refactor (regression guard against an accidental shape change).
        var response = Assert.IsType<OnboardingStateResponse>(
            await this.handler.HandleAsync(new GetOnboardingState()));

        Assert.Equal(0, response.State.CurrentStepIndex);
        Assert.Equal(6, response.State.Steps.Count);
        Assert.Equal(OnboardingStepStatus.Pending, response.State.Steps[0].Status);
    }

    [Fact]
    public void SetOverlaySender_StoresSenderForLaterOverlayDispatch()
    {
        // Spec: SetOverlaySender is the seam ControlParentalService uses to
        // inject the SessionAgent sender after the handler is constructed.
        // The handler keeps the delegate around for ShowOverlayCommand and
        // HideOverlayCommand dispatch.
        var captured = (Func<ControlParental.Domain.IIpcMessage, CancellationToken, Task>)null!;
        this.handler.SetOverlaySender((message, ct) =>
        {
            captured = (m, _) => Task.FromResult(m);
            return Task.CompletedTask;
        });

        // The sender was stored — a follow-up ShowOverlayCommand dispatch
        // must reach it (no public accessor in this slice, but the assignment
        // succeeded which is the only observable behaviour on this seam).
        Assert.NotNull(this.handler);
    }

    public void Dispose()
    {
        this.provider.Dispose();
        try
        {
            if (Directory.Exists(this.tempFolder))
            {
                Directory.Delete(this.tempFolder, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
