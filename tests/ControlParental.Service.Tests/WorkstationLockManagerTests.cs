// <copyright file="WorkstationLockManagerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Collections.Concurrent;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T09 — Tests for WorkstationLockManager and OverlayPersistenceManager.
/// Tests lock operations, IPC integration, overlay persistence, and session unlock behavior.
/// Target: ≥80% coverage.
/// </summary>
public class WorkstationLockManagerTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private readonly Mock<IIpcChannel> mockIpcChannel;
    private readonly WorkstationLockManager lockManager;

    public WorkstationLockManagerTests()
    {
        this.mockIpcChannel = new Mock<IIpcChannel>();
        this.mockIpcChannel.SetupGet(c => c.IsConnected).Returns(true);
        this.lockManager = new WorkstationLockManager(this.mockIpcChannel.Object);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullIpcChannel_DoesNotThrow()
    {
        // IPC channel is now nullable - SetIpcChannel must be called before use
        var act = () => new WorkstationLockManager(null);
        act.Should().NotThrow();
    }

    // ── Initial State Tests ─────────────────────────────────────────

    [Fact]
    public void InitialState_IsLockPendingFalse()
    {
        // Assert
        this.lockManager.IsLockPending.Should().BeFalse();
    }

    [Fact]
    public void InitialState_LastLockResultIsNull()
    {
        // Assert
        this.lockManager.LastLockResult.Should().BeNull();
    }

    // ── LockNowAsync Tests ───────────────────────────────────────────

    [Fact]
    public async Task LockNowAsync_WhenOnlyLegacyIpcConnected_DoesNotClaimSuccess()
    {
        // Arrange
        var sentMessages = new ConcurrentQueue<IIpcMessage>();
        this.mockIpcChannel
            .Setup(c => c.SendAsync(It.IsAny<IIpcMessage>(), It.IsAny<CancellationToken>()))
            .Callback<IIpcMessage, CancellationToken>((msg, _) => sentMessages.Enqueue(msg))
            .Returns(Task.CompletedTask);

        // Act
        var result = await this.lockManager.LockNowAsync();

        // Assert
        result.Should().BeFalse();
        sentMessages.Should().BeEmpty();
        this.lockManager.LastLockResult!.Status.Should().Be(ActionStatus.InvalidState);
    }

    [Fact]
    public async Task LockNowAsync_WhenIpcNotConnected_ReturnsFalse()
    {
        // Arrange
        this.mockIpcChannel.SetupGet(c => c.IsConnected).Returns(false);

        // Act
        var result = await this.lockManager.LockNowAsync();

        // Assert
        result.Should().BeFalse();
        this.lockManager.LastLockResult.Should().NotBeNull();
        this.lockManager.LastLockResult!.Success.Should().BeFalse();
        this.lockManager.LastLockResult.ErrorMessage.Should().Contain("Typed command authority");
    }

    [Fact]
    public async Task LockNowAsync_WhenAlreadyLocked_ReturnsFalse()
    {
        var completion = new TaskCompletionSource<AgentActionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        AgentCommandEnvelope? pendingCommand = null;
        this.lockManager.SetCommandExecutor(7, 3, (command, _) =>
        {
            pendingCommand = command;
            return new ValueTask<AgentActionResult>(completion.Task);
        });

        // Start first lock
        var firstLock = this.lockManager.LockNowAsync();

        // Try second lock while first is pending
        var secondLock = await this.lockManager.LockNowAsync();

        // Assert
        secondLock.Should().BeFalse();
        completion.SetResult(new AgentActionResult(
            pendingCommand!.CommandId, pendingCommand.SessionId,
            pendingCommand.ConnectionGeneration, pendingCommand.IntentVersion,
            ActionStatus.Confirmed, null));
        (await firstLock).Should().BeTrue();
    }

    [Fact]
    public async Task LockNowAsync_WhenSendThrows_ReturnsFalseAndRecordsError()
    {
        // Arrange
        this.lockManager.SetCommandExecutor(7, 3, (_, _) =>
            ValueTask.FromException<AgentActionResult>(new InvalidOperationException("Send failed")));

        // Act
        var result = await this.lockManager.LockNowAsync();

        // Assert
        result.Should().BeFalse();
        this.lockManager.LastLockResult.Should().NotBeNull();
        this.lockManager.LastLockResult!.Success.Should().BeFalse();
        this.lockManager.LastLockResult.ErrorMessage.Should().Contain("Send failed");
    }

    [Fact]
    public async Task LockNowAsync_AfterSuccess_SetsLastLockResultToSuccess()
    {
        AgentCommandEnvelope? sent = null;
        this.lockManager.SetCommandExecutor(7, 3, (command, _) =>
        {
            sent = command;
            return ValueTask.FromResult(new AgentActionResult(
                command.CommandId, command.SessionId, command.ConnectionGeneration,
                command.IntentVersion, ActionStatus.Confirmed, null));
        });

        // Act
        await this.lockManager.LockNowAsync();

        // Assert
        this.lockManager.LastLockResult.Should().NotBeNull();
        this.lockManager.LastLockResult!.Success.Should().BeTrue();
        this.lockManager.LastLockResult.Status.Should().Be(ActionStatus.Confirmed);
        this.lockManager.LastLockResult.ErrorMessage.Should().BeNull();
        sent.Should().NotBeNull();
        sent!.SessionId.Should().Be(7);
        sent.ConnectionGeneration.Should().Be(3);
        sent.Command.Should().Be(AgentCommandKind.LockWorkstation);
    }

    [Theory]
    [InlineData(ActionStatus.TimedOut)]
    [InlineData(ActionStatus.ConnectionReplaced)]
    [InlineData(ActionStatus.NativeFailure)]
    public async Task LockNowAsync_NonConfirmedTypedResultCannotBecomeSuccess(ActionStatus status)
    {
        this.lockManager.SetCommandExecutor(7, 3, (command, _) =>
            ValueTask.FromResult(new AgentActionResult(
                command.CommandId, command.SessionId, command.ConnectionGeneration,
                command.IntentVersion, status, null)));

        var result = await this.lockManager.LockNowAsync();

        result.Should().BeFalse();
        this.lockManager.LastLockResult!.Status.Should().Be(status);
    }

    [Fact]
    public async Task LockNowAsync_MismatchedTypedResultCannotBecomeSuccess()
    {
        this.lockManager.SetCommandExecutor(7, 3, (command, _) =>
            ValueTask.FromResult(new AgentActionResult(
                command.CommandId, command.SessionId, command.ConnectionGeneration + 1,
                command.IntentVersion, ActionStatus.Confirmed, null)));

        var result = await this.lockManager.LockNowAsync();

        result.Should().BeFalse();
        this.lockManager.LastLockResult!.Status.Should().Be(ActionStatus.Stale);
    }

    [Fact]
    public async Task LockNowAsync_ClearsIsLockPendingAfterCompletion()
    {
        // Arrange
        this.mockIpcChannel
            .Setup(c => c.SendAsync(It.IsAny<IIpcMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await this.lockManager.LockNowAsync();

        // Assert
        this.lockManager.IsLockPending.Should().BeFalse();
    }

    // ── IsLockPending Tests ──────────────────────────────────────────

    [Fact]
    public async Task IsLockPending_TrueDuringLockOperation()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        AgentCommandEnvelope? pendingCommand = null;
        this.lockManager.SetCommandExecutor(7, 3, (command, _) =>
        {
            pendingCommand = command;
            return new ValueTask<AgentActionResult>(tcs.Task.ContinueWith(_ =>
                new AgentActionResult(
                    command.CommandId, command.SessionId, command.ConnectionGeneration,
                    command.IntentVersion, ActionStatus.Confirmed, null)));
        });

        var lockTask = this.lockManager.LockNowAsync();

        // Small delay to ensure the flag is set
        await Task.Delay(10);

        // Assert
        this.lockManager.IsLockPending.Should().BeTrue();

        // Complete the operation
        tcs.SetResult();
        await lockTask;
    }
}

/// <summary>
/// T09 — Tests for OverlayPersistenceManager.
/// </summary>
public class OverlayPersistenceManagerTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private readonly OverlayPersistenceManager persistenceManager;

    public OverlayPersistenceManagerTests()
    {
        this.persistenceManager = new OverlayPersistenceManager();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_InitializesNotActive()
    {
        // Assert
        this.persistenceManager.IsPersistentOverlayActive.Should().BeFalse();
    }

    // ── EnablePersistentOverlay Tests ───────────────────────────────

    [Fact]
    public void EnablePersistentOverlay_SetsActiveTrue()
    {
        // Act
        this.persistenceManager.EnablePersistentOverlay("Test reason");

        // Assert
        this.persistenceManager.IsPersistentOverlayActive.Should().BeTrue();
    }

    [Fact]
    public void EnablePersistentOverlay_StoresReason()
    {
        // Act
        this.persistenceManager.EnablePersistentOverlay("Test reason");

        // Assert
        this.persistenceManager.PersistentOverlayReason.Should().Be("Test reason");
    }

    [Fact]
    public void EnablePersistentOverlay_WithCtaLabel_StoresCtaLabel()
    {
        // Act
        this.persistenceManager.EnablePersistentOverlay("Test reason", "Click me");

        // Assert
        this.persistenceManager.PersistentOverlayReason.Should().Be("Test reason");
        this.persistenceManager.PersistentOverlayCtaLabel.Should().Be("Click me");
    }

    [Fact]
    public void EnablePersistentOverlay_WithNullReason_StoresEmptyString()
    {
        // Act
        this.persistenceManager.EnablePersistentOverlay(null!);

        // Assert
        this.persistenceManager.PersistentOverlayReason.Should().Be(string.Empty);
    }

    // ── DisablePersistentOverlay Tests ──────────────────────────────

    [Fact]
    public void DisablePersistentOverlay_SetsActiveFalse()
    {
        // Arrange
        this.persistenceManager.EnablePersistentOverlay("Test reason");

        // Act
        this.persistenceManager.DisablePersistentOverlay();

        // Assert
        this.persistenceManager.IsPersistentOverlayActive.Should().BeFalse();
    }

    [Fact]
    public void DisablePersistentOverlay_ClearsReason()
    {
        // Arrange
        this.persistenceManager.EnablePersistentOverlay("Test reason");

        // Act
        this.persistenceManager.DisablePersistentOverlay();

        // Assert
        this.persistenceManager.PersistentOverlayReason.Should().BeNull();
    }

    [Fact]
    public void DisablePersistentOverlay_ClearsCtaLabel()
    {
        // Arrange
        this.persistenceManager.EnablePersistentOverlay("Test reason", "Click me");

        // Act
        this.persistenceManager.DisablePersistentOverlay();

        // Assert
        this.persistenceManager.PersistentOverlayCtaLabel.Should().BeNull();
    }

    [Fact]
    public void DisablePersistentOverlay_WhenNotActive_DoesNotThrow()
    {
        // Act & Assert
        var act = () => this.persistenceManager.DisablePersistentOverlay();
        act.Should().NotThrow();
    }

    // ── OnSessionUnlocked Tests ─────────────────────────────────────

    [Fact]
    public void OnSessionUnlocked_WhenActive_CallsShowOverlayAction()
    {
        // Arrange
        this.persistenceManager.EnablePersistentOverlay("Locked", "Ask parent");
        var showOverlayCalled = false;
        string? receivedReason = null;
        string? receivedCtaLabel = null;

        void ShowOverlay(string reason, string? ctaLabel)
        {
            showOverlayCalled = true;
            receivedReason = reason;
            receivedCtaLabel = ctaLabel;
        }

        // Act
        this.persistenceManager.OnSessionUnlocked(ShowOverlay);

        // Assert
        showOverlayCalled.Should().BeTrue();
        receivedReason.Should().Be("Locked");
        receivedCtaLabel.Should().Be("Ask parent");
    }

    [Fact]
    public void OnSessionUnlocked_WhenNotActive_DoesNotCallAction()
    {
        // Arrange
        var actionCalled = false;
        void ShowOverlay(string reason, string? ctaLabel)
        {
            actionCalled = true;
        }

        // Act
        this.persistenceManager.OnSessionUnlocked(ShowOverlay);

        // Assert
        actionCalled.Should().BeFalse();
    }

    [Fact]
    public void OnSessionUnlocked_WithNullAction_DoesNotThrow()
    {
        // Arrange
        this.persistenceManager.EnablePersistentOverlay("Test reason");

        // Act & Assert
        var act = () => this.persistenceManager.OnSessionUnlocked(null!);
        act.Should().NotThrow();
    }

    // ── State Machine Tests ─────────────────────────────────────────

    [Fact]
    public void StateMachine_EnableDisable_ResetsCorrectly()
    {
        // Act
        this.persistenceManager.EnablePersistentOverlay("First reason");
        this.persistenceManager.DisablePersistentOverlay();
        this.persistenceManager.EnablePersistentOverlay("Second reason", "CTA");

        // Assert
        this.persistenceManager.IsPersistentOverlayActive.Should().BeTrue();
        this.persistenceManager.PersistentOverlayReason.Should().Be("Second reason");
        this.persistenceManager.PersistentOverlayCtaLabel.Should().Be("CTA");
    }

    [Fact]
    public void StateMachine_MultipleEnables_OverwritesReason()
    {
        // Act
        this.persistenceManager.EnablePersistentOverlay("First");
        this.persistenceManager.EnablePersistentOverlay("Second");

        // Assert
        this.persistenceManager.PersistentOverlayReason.Should().Be("Second");
    }

    // ── Thread Safety Tests ─────────────────────────────────────────

    [Fact]
    public void EnableDisable_ConcurrentCalls_DoNotThrow()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act & Assert - should not throw
        for (var i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                if (index % 2 == 0)
                {
                    this.persistenceManager.EnablePersistentOverlay($"Reason {index}");
                }
                else
                {
                    this.persistenceManager.DisablePersistentOverlay();
                }
            }));
        }

        // Assert
        var act = () => Task.WaitAll(tasks.ToArray());
        act.Should().NotThrow();
    }
}
