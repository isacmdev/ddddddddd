// <copyright file="TaskSchedulerBackupServiceTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using FluentAssertions;
using Xunit;

/// <summary>
/// T20 — Tests for TaskSchedulerBackupService.
/// </summary>
public class TaskSchedulerBackupServiceTests : IDisposable
{
    private readonly TaskSchedulerBackupService service;

    public TaskSchedulerBackupServiceTests()
    {
        this.service = new TaskSchedulerBackupService();
    }

    public void Dispose()
    {
        this.service.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsAreBackupTasksRegisteredToFalse()
    {
        this.service.AreBackupTasksRegistered.Should().BeFalse();
    }

    // ── AreBackupTasksRegistered Tests ─────────────────────────────────

    [Fact]
    public void AreBackupTasksRegistered_WhenNotRegistered_ReturnsFalse()
    {
        this.service.AreBackupTasksRegistered.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Act
        var act = () =>
        {
            this.service.Dispose();
            this.service.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task TriggerBackupAsync_UsesTheSharedAdmissionCallback()
    {
        BackupMode? observedMode = null;
        CancellationToken observedToken = default;
        var callback = new Func<BackupMode, CancellationToken, Task>((mode, token) =>
        {
            observedMode = mode;
            observedToken = token;
            return Task.CompletedTask;
        });
        using var service = new TaskSchedulerBackupService(callback);
        using var cts = new CancellationTokenSource();

        await service.TriggerBackupAsync(BackupMode.Outbox, cts.Token);

        observedMode.Should().Be(BackupMode.Outbox);
        observedToken.CanBeCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task TriggerBackupAsync_WhenCancelled_DoesNotInvokeAdmission()
    {
        var invoked = false;
        using var service = new TaskSchedulerBackupService((_, _) =>
        {
            invoked = true;
            return Task.CompletedTask;
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.TriggerBackupAsync(BackupMode.Heartbeat, cts.Token));

        invoked.Should().BeFalse();
    }

    [Fact]
    public async Task TriggerBackupAsync_WhenDisposed_RejectsTheTrigger()
    {
        this.service.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            this.service.TriggerBackupAsync(BackupMode.Outbox));
    }

    [Fact]
    public async Task TriggerBackupAsync_WhenAdmissionIsNotConfigured_RejectsTheTrigger()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            this.service.TriggerBackupAsync(BackupMode.Outbox));
    }

    [Fact]
    public async Task Dispose_CancelsAnInFlightAdmission()
    {
        var admissionStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var admissionCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var service = new TaskSchedulerBackupService((_, token) =>
        {
            admissionStarted.SetResult();
            token.Register(admissionCancelled.SetResult);
            return admissionCancelled.Task;
        });

        var trigger = service.TriggerBackupAsync(BackupMode.Outbox);
        await admissionStarted.Task;
        service.Dispose();

        (await Task.WhenAny(trigger, Task.Delay(TimeSpan.FromMilliseconds(250))))
            .Should().Be(trigger);
    }
}
/// <summary>
/// T20 — Tests for TaskSchedulerBackupService.RegisterBackupTasksAsync behavior.
/// </summary>
public class TaskSchedulerBackupServiceRegisterTests : IDisposable
{
    private readonly TaskSchedulerBackupService service;

    public TaskSchedulerBackupServiceRegisterTests()
    {
        this.service = new TaskSchedulerBackupService();
    }

    public void Dispose()
    {
        this.service.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RegisterBackupTasksAsync_WhenNotAdmin_DoesNotThrow()
    {
        // Act - on a normal user context without admin rights, registration should fail gracefully
        // The key behavior is that it doesn't throw, regardless of return value
        var act = async () => await this.service.RegisterBackupTasksAsync();

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterBackupTasksAsync_WhenDisposed_ReturnsFalse()
    {
        // Arrange
        this.service.Dispose();

        // Act
        var result = await this.service.RegisterBackupTasksAsync();

        // Assert
        result.Should().BeFalse();
    }

}

/// <summary>
/// T20 — Tests for TaskSchedulerBackupService.UnregisterBackupTasksAsync behavior.
/// </summary>
public class TaskSchedulerBackupServiceUnregisterTests : IDisposable
{
    private readonly TaskSchedulerBackupService service;

    public TaskSchedulerBackupServiceUnregisterTests()
    {
        this.service = new TaskSchedulerBackupService();
    }

    public void Dispose()
    {
        this.service.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UnregisterBackupTasksAsync_WhenNoTasks_ReturnsTrue()
    {
        // Act - unregister when nothing is registered should still return true
        var result = await this.service.UnregisterBackupTasksAsync();

        // Assert - should return true as no error occurred
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterBackupTasksAsync_WhenDisposed_ReturnsFalse()
    {
        // Arrange
        this.service.Dispose();

        // Act
        var result = await this.service.UnregisterBackupTasksAsync();

        // Assert
        result.Should().BeFalse();
    }

}
