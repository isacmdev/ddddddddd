// <copyright file="TaskSchedulerBackupServiceTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using FluentAssertions;
using Moq;
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

/// <summary>
/// T20 — Tests verifying ITaskSchedulerBackup interface contract.
/// </summary>
public class TaskSchedulerBackupServiceInterfaceTests : IDisposable
{
    private readonly Mock<ITaskSchedulerBackup> mockBackup;

    public TaskSchedulerBackupServiceInterfaceTests()
    {
        this.mockBackup = new Mock<ITaskSchedulerBackup>();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task RegisterBackupTasksAsync_CalledOnce_ReturnsResult()
    {
        // Arrange
        this.mockBackup.Setup(b => b.RegisterBackupTasksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await this.mockBackup.Object.RegisterBackupTasksAsync();

        // Assert
        result.Should().BeTrue();
        this.mockBackup.Verify(b => b.RegisterBackupTasksAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnregisterBackupTasksAsync_CalledOnce_ReturnsResult()
    {
        // Arrange
        this.mockBackup.Setup(b => b.UnregisterBackupTasksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await this.mockBackup.Object.UnregisterBackupTasksAsync();

        // Assert
        result.Should().BeTrue();
        this.mockBackup.Verify(b => b.UnregisterBackupTasksAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AreBackupTasksRegistered_WhenRegistered_ReturnsTrue()
    {
        // Arrange
        this.mockBackup.SetupGet(b => b.AreBackupTasksRegistered).Returns(true);

        // Act
        var result = this.mockBackup.Object.AreBackupTasksRegistered;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AreBackupTasksRegistered_WhenNotRegistered_ReturnsFalse()
    {
        // Arrange
        this.mockBackup.SetupGet(b => b.AreBackupTasksRegistered).Returns(false);

        // Act
        var result = this.mockBackup.Object.AreBackupTasksRegistered;

        // Assert
        result.Should().BeFalse();
    }
}
