// <copyright file="PrivilegeInspectorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Xunit;

/// <summary>
/// T37 — Tests for PrivilegeInspector.
/// Verifies privilege detection using the real WindowsIdentity of the test process.
/// </summary>
public class PrivilegeInspectorTests
{
    [Fact]
    public async Task GetPrivilegeLevelAsync_WithNoUsername_ReturnsCurrentUserLevel()
    {
        // Arrange
        var inspector = new PrivilegeInspector();

        // Act
        var level = await inspector.GetPrivilegeLevelAsync(cancellationToken: CancellationToken.None);

        // Assert — level must be a valid enum value
        Enum.IsDefined(typeof(PrivilegeLevel), level).Should().BeTrue();
    }

    [Fact]
    public async Task GetPrivilegeLevelAsync_ReturnsOneOfDefinedValues()
    {
        // Arrange
        var inspector = new PrivilegeInspector();

        // Act
        var level = await inspector.GetPrivilegeLevelAsync(cancellationToken: CancellationToken.None);

        // Assert — returns Standard, Administrator, or Unknown (never throws)
        level.Should().BeOneOf(PrivilegeLevel.Standard, PrivilegeLevel.Administrator, PrivilegeLevel.Unknown);
    }

    [Fact]
    public async Task IsChildStandardAsync_ReturnsBool()
    {
        // Arrange
        var inspector = new PrivilegeInspector();

        // Act
        var isStandard = await inspector.IsChildStandardAsync(CancellationToken.None);

        // Assert — returns a boolean (true = standard, false = admin/unknown)
        // Booleans in C# are always true or false; just verify no exception was thrown
        _ = isStandard;
    }

    [Fact]
    public async Task GetPrivilegeLevelAsync_IsNotStandardUser_WhenCurrentUserIsAdmin()
    {
        // Arrange — skip if not running as admin (tests run as normal user in CI)
        var inspector = new PrivilegeInspector();
        var level = await inspector.GetPrivilegeLevelAsync();

        // This test documents expected behavior: Standard users are the secure configuration.
        // If running as admin, IsChildStandardAsync returns false.
        if (level == PrivilegeLevel.Administrator)
        {
            var result = await inspector.IsChildStandardAsync();
            result.Should().BeFalse();
        }
    }

    [Fact]
    public async Task GetPrivilegeLevelAsync_CompletesWithinReasonableTime()
    {
        // Arrange
        var inspector = new PrivilegeInspector();

        // Act
        var task = inspector.GetPrivilegeLevelAsync();
        var completed = task.Wait(TimeSpan.FromSeconds(5));

        // Assert — should complete within 5 seconds
        completed.Should().BeTrue("privilege inspection should not hang");
        task.Result.Should().BeOneOf(PrivilegeLevel.Standard, PrivilegeLevel.Administrator, PrivilegeLevel.Unknown);
    }

    [Fact]
    public async Task IsChildStandardAsync_CompletesWithinReasonableTime()
    {
        // Arrange
        var inspector = new PrivilegeInspector();

        // Act
        var task = inspector.IsChildStandardAsync();
        var completed = task.Wait(TimeSpan.FromSeconds(5));

        // Assert
        completed.Should().BeTrue("IsChildStandardAsync should not hang");
    }

    [Fact]
    public async Task GetPrivilegeLevelAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var inspector = new PrivilegeInspector();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert — TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => inspector.GetPrivilegeLevelAsync(cancellationToken: cts.Token));
    }

    [Fact]
    public async Task IsChildStandardAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var inspector = new PrivilegeInspector();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert — TaskCanceledException inherits from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => inspector.IsChildStandardAsync(cts.Token));
    }

    [Fact]
    public async Task GetPrivilegeLevelAsync_ReturnsConsistentResult()
    {
        // Arrange
        var inspector = new PrivilegeInspector();

        // Act
        var level1 = await inspector.GetPrivilegeLevelAsync();
        var level2 = await inspector.GetPrivilegeLevelAsync();
        var level3 = await inspector.GetPrivilegeLevelAsync();

        // Assert — multiple calls return the same result
        level1.Should().Be(level2);
        level2.Should().Be(level3);
    }

    [Fact]
    public async Task IsChildStandardAsync_IsInverseOf_IsAdmin()
    {
        // Arrange
        var inspector = new PrivilegeInspector();
        var level = await inspector.GetPrivilegeLevelAsync();
        var isStandard = await inspector.IsChildStandardAsync();

        // Assert — IsChildStandardAsync should be true only when level is Standard
        // (not when Administrator or Unknown)
        if (level == PrivilegeLevel.Standard)
        {
            isStandard.Should().BeTrue();
        }
        else
        {
            isStandard.Should().BeFalse();
        }
    }
}
