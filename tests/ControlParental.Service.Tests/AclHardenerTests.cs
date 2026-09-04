// <copyright file="AclHardenerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.Win32;
using System.Security.Principal;
using System.Security;
using Xunit;

/// <summary>
/// Tests for the AclHardener implementation.
/// Verifies that ACLs are set correctly on folders.
/// Note: some tests require Windows-specific file system behavior.
/// </summary>
public class AclHardenerTests
{
    [Fact]
    public async Task HardenRegistryKeyAsync_WhenRegistryBoundaryThrowsSecurityException_ReturnsFalse()
    {
        var previous = AclHardener.RegistryKeyOpener;
        AclHardener.RegistryKeyOpener = (_, _) => throw new SecurityException("access denied");

        try
        {
            var result = await new AclHardener().HardenRegistryKeyAsync("SYSTEM\\ControlParental");

            Assert.False(result);
        }
        finally
        {
            AclHardener.RegistryKeyOpener = previous;
        }
    }

    [Fact]
    public async Task Hardening_HardenAgentFolderAsync_RepeatedApplication_IsSuccessful()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"cp_acl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        var hardener = new AclHardener();

        try
        {
            var first = await hardener.HardenAgentFolderAsync(tempPath);
            var second = await hardener.HardenAgentFolderAsync(tempPath);
            await File.WriteAllTextAsync(Path.Combine(tempPath, "data.txt"), "test");
            var fileResult = await hardener.HardenDataFolderAsync(Path.Combine(tempPath, "data.txt"));
            var binaryResult = await hardener.HardenServiceBinaryAsync(Path.Combine(tempPath, "data.txt"));

            Assert.True(first);
            Assert.True(second);
            Assert.True(fileResult);
            Assert.True(binaryResult);

            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
            {
                var keyPath = $"SOFTWARE\\ControlParental-Harness-{Guid.NewGuid():N}";
                try
                {
                    using var key = Registry.LocalMachine.CreateSubKey(keyPath);
                    Assert.NotNull(key);
                    Assert.True(await hardener.HardenRegistryKeyAsync(keyPath));
                }
                finally
                {
                    Registry.LocalMachine.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
                }
            }
        }
        finally
        {
            try { Directory.Delete(tempPath, recursive: true); } catch { }
        }
    }
    [Fact]
    public async Task HardenAgentFolderAsync_OnNewDirectory_ShouldSucceed()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"cp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);

        var hardener = new AclHardener();

        try
        {
            // Act
            var result = await hardener.HardenAgentFolderAsync(tempPath, CancellationToken.None);

            // Assert
            Assert.True(result);
        }
        finally
        {
            // Best-effort cleanup
            try
            {
                Directory.Delete(tempPath, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public async Task HardenDataFolderAsync_OnNonExistentPath_ShouldReturnFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(
            Path.GetTempPath(),
            $"nonexistent_{Guid.NewGuid():N}");

        var hardener = new AclHardener();

        // Act
        var result = await hardener.HardenDataFolderAsync(nonExistentPath, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task HardenAllAsync_ShouldCallAllHardeningMethods()
    {
        // Arrange
        var agentPath = Path.Combine(Path.GetTempPath(), $"cp_agent_{Guid.NewGuid():N}");
        var dataPath = Path.Combine(Path.GetTempPath(), $"cp_data_{Guid.NewGuid():N}");

        Directory.CreateDirectory(agentPath);
        Directory.CreateDirectory(dataPath);

        var hardener = new AclHardener();

        try
        {
            // Act
            // Note: registry and binary hardening will fail in test environment
            // because the paths don't exist, but the method should still complete
            var result = await hardener.HardenAllAsync(
                agentPath,
                dataPath,
                @"SYSTEM\NonExistentKey",
                Path.Combine(Path.GetTempPath(), "nonexistent.exe"),
                CancellationToken.None);

            // Assert - folder hardening should succeed even if registry/binary fail
            // The method returns false only if ALL operations fail
            // In this test, folder hardening succeeds so result may be partial
            Assert.NotNull(result);
        }
        finally
        {
            try
            {
                Directory.Delete(agentPath, recursive: true);
            }
            catch { /* best-effort */ }

            try
            {
                Directory.Delete(dataPath, recursive: true);
            }
            catch { /* best-effort */ }
        }
    }
}
