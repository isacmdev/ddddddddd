// <copyright file="PrivilegeLevelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain.Tests;

using Xunit;

/// <summary>
/// Tests for PrivilegeLevel enum and IPrivilegeInspector contract.
/// </summary>
public class PrivilegeLevelTests
{
    [Fact]
    public void PrivilegeLevel_Standard_ShouldBeZero()
    {
        // Standard is 0 (secure)
        Assert.Equal(0, (int)PrivilegeLevel.Standard);
    }

    [Fact]
    public void PrivilegeLevel_Administrator_ShouldBeOne()
    {
        // Administrator is 1 (DEGRADED)
        Assert.Equal(1, (int)PrivilegeLevel.Administrator);
    }

    [Fact]
    public void PrivilegeLevel_Unknown_ShouldBeTwo()
    {
        // Unknown is 2
        Assert.Equal(2, (int)PrivilegeLevel.Unknown);
    }

    [Fact]
    public void AccountCreationResult_Succeeded_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var result = AccountCreationResult.Succeeded("child_user");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("child_user", result.Username);
        Assert.False(result.RequiresElevation);
        Assert.Null(result.ErrorMessage);
        Assert.False(result.NeedsElevation);
    }

    [Fact]
    public void AccountCreationResult_NeedsAdminElevation_ShouldRequireElevation()
    {
        // Arrange & Act
        var result = AccountCreationResult.NeedsAdminElevation("Admin action required");

        // Assert
        Assert.False(result.Success);
        Assert.True(result.RequiresElevation);
        Assert.True(result.NeedsElevation);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Admin action required", result.ErrorMessage);
    }

    [Fact]
    public void AccountCreationResult_Failed_ShouldNotRequireElevation()
    {
        // Arrange & Act
        var result = AccountCreationResult.Failed("Password too short");

        // Assert
        Assert.False(result.Success);
        Assert.False(result.RequiresElevation);
        Assert.False(result.NeedsElevation);
        Assert.Equal("Password too short", result.ErrorMessage);
    }
}
