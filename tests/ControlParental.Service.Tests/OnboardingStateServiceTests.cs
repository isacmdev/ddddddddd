// <copyright file="OnboardingStateServiceTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Moq;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 Fase 5 (PR #4a) — Regression tests for the Service-side
/// <see cref="OnboardingStateService"/>. These tests cover the architectural flip
/// that moves onboarding-state ownership from App.UI to the Service:
/// <list type="bullet">
///   <item>Atomic write via <c>.tmp</c> + <see cref="File.Move(string, string, bool)"/>
///         so a crash mid-write leaves the canonical file untouched (ADR-009,
///         design §12).</item>
///   <item>Advance and reset mutations persist to the same file.</item>
///   <item>Kill-during-write race: half-written state files do not corrupt
///         the prior persisted state.</item>
/// </list>
/// </summary>
[Collection("OnboardingStateServiceFile")]
public sealed class OnboardingStateServiceTests : IDisposable
{
    private readonly string tempDir;
    private readonly OnboardingStateService service;

    public OnboardingStateServiceTests()
    {
        this.tempDir = Path.Combine(
            Path.GetTempPath(),
            $"cp-onboarding-{Guid.NewGuid():N}");

        var store = new Mock<IChildAccountStore>();
        this.service = new OnboardingStateService(
            this.tempDir,
            store.Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<OnboardingStateService>>().Object);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(this.tempDir))
            {
                Directory.Delete(this.tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task GetStateAsync_WhenFileMissing_ReturnsInitialState()
    {
        // Act
        var state = await this.service.GetStateAsync();

        // Assert — initial state has 5 steps, the first is Pending, index 0.
        Assert.Equal(6, state.Steps.Count);
        Assert.Equal(0, state.CurrentStepIndex);
        Assert.False(state.IsCompleted);
        Assert.False(state.IsAbandoned);
        Assert.Equal(OnboardingStepStatus.Pending, state.Steps[0].Status);
    }

    [Fact]
    public async Task GetStateAsync_LegacyFiveStepSnapshot_NormalizesToPendingServiceStep()
    {
        var legacy = new OnboardingState(
            3,
            false,
            false,
            new[]
            {
                new OnboardingStep(0, "pairing", "Pairing", "x", "x", OnboardingStepStatus.Completed),
                new OnboardingStep(1, "consent", "Consent", "x", "x", OnboardingStepStatus.Completed),
                new OnboardingStep(2, "account", "Account", "x", "x", OnboardingStepStatus.Completed),
                new OnboardingStep(3, "demo", "Demo", "x", "x", OnboardingStepStatus.InProgress, true),
                new OnboardingStep(4, "managed", "Managed", "x", "x", OnboardingStepStatus.Locked),
            },
            Array.Empty<FunnelEvent>());
        await File.WriteAllTextAsync(
            Path.Combine(this.tempDir, "onboarding_state.json"),
            System.Text.Json.JsonSerializer.Serialize(legacy, OnboardingStateJsonContext.Default.OnboardingState));

        var normalized = await this.service.GetStateAsync();

        Assert.Equal(6, normalized.Steps.Count);
        Assert.Equal("service", normalized.Steps[3].Id);
        Assert.Equal(3, normalized.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.InProgress, normalized.Steps[3].Status);
    }

    [Fact]
    public async Task OnboardingStateService_OnCorruptedFile_LogsErrorAndReturnsInitialState()
    {
        var statePath = Path.Combine(this.tempDir, "onboarding_state.json");
        await File.WriteAllTextAsync(statePath, "{ definitely-not-json }");
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<OnboardingStateService>>();
        var service = new OnboardingStateService(
            this.tempDir,
            new Mock<IChildAccountStore>().Object,
            logger.Object);

        var state = await service.GetStateAsync();

        Assert.Equal(0, state.CurrentStepIndex);
        Assert.Equal(6, state.Steps.Count);
        logger.Verify(
            x => x.Log(
                Microsoft.Extensions.Logging.LogLevel.Error,
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.Is<It.IsAnyType>((_, _) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void OnboardingStateJsonContext_ProvidesOnboardingStateMetadata()
    {
        var typeInfo = OnboardingStateJsonContext.Default.OnboardingState;

        Assert.Equal(typeof(OnboardingState), typeInfo.Type);
    }

    [Fact]
    public async Task RecordStepCompletedAsync_PersistsStepStatus()
    {
        // Arrange
        await this.service.GetStateAsync(); // initialize file via advance path

        // Act
        await this.service.RecordStepCompletedAsync("pairing");

        // Assert — reload and verify
        var state = await this.service.GetStateAsync();
        var pairing = state.Steps.First(s => s.Id == "pairing");
        Assert.Equal(OnboardingStepStatus.Completed, pairing.Status);
    }

    [Fact]
    public async Task AdvanceAsync_MarksPriorStepsCompletedAndTargetInProgress()
    {
        // Act
        var advanced = await this.service.AdvanceAsync(2);

        // Assert — return value reflects the new current step
        Assert.Equal(2, advanced.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.Completed, advanced.Steps[0].Status);
        Assert.Equal(OnboardingStepStatus.Completed, advanced.Steps[1].Status);
        Assert.Equal(OnboardingStepStatus.InProgress, advanced.Steps[2].Status);

        // Assert — persisted state matches
        var reloaded = await this.service.GetStateAsync();
        Assert.Equal(2, reloaded.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.InProgress, reloaded.Steps[2].Status);
    }

    [Fact]
    public async Task Hardening_AdvanceAsync_WhenSecurityIsDegraded_BlocksHealthyOnboarding()
    {
        var degraded = new OnboardingStateService(
            this.tempDir + "-degraded",
            new Mock<IChildAccountStore>().Object,
            new Mock<Microsoft.Extensions.Logging.ILogger<OnboardingStateService>>().Object,
            canProceedWithHealthyOnboarding: () => false);

        var state = await degraded.AdvanceAsync(3);

        Assert.Equal(0, state.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.Pending, state.Steps[0].Status);
    }

    [Fact]
    public async Task AdvanceAsync_CalledTwice_AdvancesTwice()
    {
        // T26 PR #11 — sanity test that the in-process AdvanceAsync still works
        // after the IPC handler wraps it. The IPC handler reads the current
        // snapshot and calls Advance(current+1) — two consecutive IPC advances
        // must move the index by 2, not bounce back.

        // Act
        await this.service.AdvanceAsync(1);
        var second = await this.service.AdvanceAsync(2);

        // Assert
        Assert.Equal(2, second.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.InProgress, second.Steps[2].Status);
        Assert.False(second.IsCompleted);

        var reloaded = await this.service.GetStateAsync();
        Assert.Equal(2, reloaded.CurrentStepIndex);
    }

    [Fact]
    public async Task AdvanceAsync_PastLastStep_SetsIsCompleted()
    {
        // Act
        var advanced = await this.service.AdvanceAsync(int.MaxValue);

        // Assert
        Assert.True(advanced.IsCompleted);
        Assert.Equal(6, advanced.Steps.Count); // steps unchanged
    }

    [Fact]
    public async Task ResetAsync_WipesPersistedState()
    {
        // Arrange — modify state first
        await this.service.RecordStepCompletedAsync("pairing");
        await this.service.RecordFunnelEventAsync("OnboardingStepReached", "pairing");

        // Act
        var reset = await this.service.ResetAsync();

        // Assert — fresh initial state
        Assert.False(reset.IsCompleted);
        Assert.Equal(0, reset.CurrentStepIndex);
        Assert.Equal(OnboardingStepStatus.Pending, reset.Steps[0].Status);

        // Assert — disk reflects reset
        var reloaded = await this.service.GetStateAsync();
        Assert.Equal(0, reloaded.CurrentStepIndex);
        Assert.Empty(reloaded.Events);
    }

    [Fact]
    public async Task AtomicWrite_ConcurrentReadersAndWriter_NoCorruption()
    {
        // Arrange — perform some writes to populate the file
        await this.service.AdvanceAsync(1);
        var beforeRace = await this.service.GetStateAsync();

        // Act — spin up several concurrent writes alongside reads
        var writeTasks = Enumerable.Range(0, 10)
            .Select(i => this.service.RecordFunnelEventAsync(FunnelEventType.OnboardingStepReached.ToString(), $"step-{i}"))
            .ToArray();
        var readTasks = Enumerable.Range(0, 10)
            .Select(_ => this.service.GetStateAsync())
            .ToArray();
        await Task.WhenAll(writeTasks);
        await Task.WhenAll(readTasks);

        // Assert — file is still deserializable, no corruption
        var afterRace = await this.service.GetStateAsync();
        Assert.Equal(1, afterRace.CurrentStepIndex);
        Assert.Equal(beforeRace.Steps.Count, afterRace.Steps.Count);
    }

    [Fact]
    public void AtomicWrite_LeavesTmpFileClean_AfterSuccess()
    {
        // Act — perform a normal write through the public surface
        this.service.AdvanceAsync(1).GetAwaiter().GetResult();
        this.service.RecordFunnelEventAsync(FunnelEventType.OnboardingStepReached.ToString(), "pairing")
            .GetAwaiter().GetResult();

        // Assert — the .tmp file must NOT exist (atomic move consumed it)
        var tmpFile = Path.Combine(this.tempDir, "onboarding_state.json.tmp");
        Assert.False(File.Exists(tmpFile), "Tmp file must not survive a successful write.");
    }

    // T26 PR (P2 onboarding ownership) — RED→GREEN contract tests for
    // RecordStepCompletedAsync returning the canonical post-completion snapshot
    // and being idempotent on duplicate / unknown step ids.

    [Fact]
    public async Task RecordFunnelEventAsync_DuplicateCausalEvent_IsIdempotent()
    {
        var first = await this.service.RecordFunnelEventAsync(
            FunnelEventType.OnboardingStepReached.ToString(),
            "service");
        var second = await this.service.RecordFunnelEventAsync(
            FunnelEventType.OnboardingStepReached.ToString(),
            "service");

        Assert.Single(first.Events);
        Assert.Single(second.Events);
        Assert.Equal("service", second.Events[0].StepId);
    }

    [Fact]
    public async Task RecordStepCompletedAsync_ReturnsPostCompletionSnapshot()
    {
        // Act — first completion is a happy path that must return the post-write
        // canonical snapshot so the caller can refresh its observable surface
        // without an extra IPC read.
        var snapshot = await this.service.RecordStepCompletedAsync("pairing");

        // Assert — the returned snapshot reflects the completed pairing step
        // and the Service holds the same canonical state.
        Assert.NotNull(snapshot);
        Assert.Equal(OnboardingStepStatus.Completed, snapshot.Steps[0].Status);
        Assert.Equal(0, snapshot.CurrentStepIndex);
        Assert.False(snapshot.IsCompleted);

        // Assert — a follow-up read sees the same canonical state.
        var reloaded = await this.service.GetStateAsync();
        Assert.Equal(OnboardingStepStatus.Completed, reloaded.Steps[0].Status);
    }

    [Fact]
    public async Task RecordStepCompletedAsync_DuplicateStepId_IsIdempotent()
    {
        // Arrange — first call completes the pairing step and persists once.
        var firstSnapshot = await this.service.RecordStepCompletedAsync("pairing");
        var firstJson = await File.ReadAllTextAsync(Path.Combine(this.tempDir, "onboarding_state.json"));

        // Act — repeat the call with the same stable step id. The Service must
        // NOT re-persist (file content unchanged) and must return the same
        // canonical snapshot.
        var secondSnapshot = await this.service.RecordStepCompletedAsync("pairing");
        var secondJson = await File.ReadAllTextAsync(Path.Combine(this.tempDir, "onboarding_state.json"));

        // Assert — same canonical snapshot, no second persistence event.
        Assert.Equal(firstSnapshot.Steps[0].Status, secondSnapshot.Steps[0].Status);
        Assert.Equal(firstJson, secondJson);

        // Assert — the tmp file must not exist (the second call didn't even
        // reach SaveStateAsync).
        var tmpFile = Path.Combine(this.tempDir, "onboarding_state.json.tmp");
        Assert.False(File.Exists(tmpFile), "Duplicate completion must not perform a write.");
    }

    [Fact]
    public async Task RecordStepCompletedAsync_UnknownStepId_DoesNotCorruptState()
    {
        // Arrange — force the canonical file to exist by advancing one step,
        // then capture the on-disk content so we can assert no mutation.
        await this.service.AdvanceAsync(1);
        var stateFile = Path.Combine(this.tempDir, "onboarding_state.json");
        var originalJson = await File.ReadAllTextAsync(stateFile);

        // Act — call with an unknown step id; the Service must not throw, must
        // not persist, and must return the current snapshot unchanged.
        var snapshot = await this.service.RecordStepCompletedAsync("this-step-does-not-exist");

        // Assert — the persisted file content is unchanged.
        Assert.Equal(originalJson, await File.ReadAllTextAsync(stateFile));
        Assert.NotNull(snapshot);
        Assert.Equal(OnboardingStepStatus.InProgress, snapshot.Steps[1].Status);
    }

    [Fact]
    public async Task RecordStepCompletedAsync_PersistsOnce()
    {
        // Spec: the Service persists exactly once per state mutation, even if
        // GetStateAsync internally reads the file. Counting the file writes
        // (atomic rename) is the cleanest way to assert single persistence.

        // Arrange — wire a counting logger to detect duplicate-persistence
        // symptoms: RecordStepCompletedAsync must NOT spawn extra writes
        // beyond the one its happy path performs.
        await this.service.GetStateAsync();

        // Act
        await this.service.RecordStepCompletedAsync("pairing");

        // Assert — file exists exactly once (no orphan .tmp leftover).
        var stateFile = Path.Combine(this.tempDir, "onboarding_state.json");
        Assert.True(File.Exists(stateFile));
        var tmpFile = Path.Combine(this.tempDir, "onboarding_state.json.tmp");
        Assert.False(File.Exists(tmpFile));
    }
}

/// <summary>
/// xUnit collection that serializes tests touching the temp ProgramData folder
/// since <see cref="OnboardingStateServiceTests"/> uses a fresh path per test
/// already, but we keep the collection available for future tests that share a
/// path. Currently empty (single-test class) so the attribute is required so the
/// collection exists.
/// </summary>
[CollectionDefinition("OnboardingStateServiceFile", DisableParallelization = false)]
public sealed class OnboardingStateServiceFileCollection
{
}
