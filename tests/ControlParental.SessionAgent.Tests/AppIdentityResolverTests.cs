// <copyright file="AppIdentityResolverTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent.Tests;

using ControlParental.SessionAgent.Interop;
using Xunit;

/// <summary>
/// T05 — Tests for AppIdentityResolver (pure logic, no Win32 API required).
/// Tests the resolution logic and filtering.
/// </summary>
public class AppIdentityResolverTests
{
    // ── Resolve: null/empty input ───────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_NullOrEmptyPath_ReturnsUnknown(string? path)
    {
        // Act
        var appId = AppIdentityResolver.Resolve(path!);

        // Assert
        Assert.Equal("unknown", appId);
    }

    // ── Resolve: basic Win32 exe ─────────────────────────────────────

    [Theory]
    [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", "chrome.exe")]
    [InlineData(@"C:\Program Files\Mozilla Firefox\firefox.exe", "firefox.exe")]
    [InlineData(@"C:\Windows\notepad.exe", "notepad.exe")]
    [InlineData(@"C:\Users\Child\AppData\Local\Programs\Microsoft VS Code\Code.exe", "Code.exe")]
    public void Resolve_BasicExe_ReturnsExeNamePlusPathHash(string path, string expectedExeName)
    {
        // Act
        var appId = AppIdentityResolver.Resolve(path);

        // Assert
        Assert.StartsWith(expectedExeName, appId);
        Assert.Contains("|", appId); // Has separator (hash or publisher)
    }

    // ── Resolve: MSIX path format ───────────────────────────────────

    [Fact]
    public void Resolve_NonWindowsAppsPath_ReturnsPathHashFallback()
    {
        // Arrange — path without WindowsApps → not MSIX, uses hash fallback
        var path = @"C:\Program Files\CustomApp\app.exe";

        // Act
        var appId = AppIdentityResolver.Resolve(path);

        // Assert — no WindowsApps, so fallback to exe + hash
        Assert.StartsWith("app.exe", appId);
        Assert.Contains("|", appId);
    }

    // ── GetProcessPathById: invalid PID ──────────────────────────────

    [Fact]
    public void GetProcessPathById_InvalidPid_ReturnsNull()
    {
        // Act — use a very high PID that doesn't exist
        var path = AppIdentityResolver.GetProcessPathById(uint.MaxValue);

        // Assert — should return null (process not found)
        Assert.Null(path);
    }

    // ── Interface contract ────────────────────────────────────────────

    [Fact]
    public void AppIdentityResolver_IsPublicAndStatic()
    {
        // Arrange
        var type = typeof(AppIdentityResolver);

        // Assert
        Assert.True(type.IsPublic);
        Assert.True(type.IsAbstract);
        Assert.True(type.IsSealed);
        Assert.Contains("Resolve", type.GetMethods().Select(m => m.Name));
    }

    [Fact]
    public void Resolve_CaseInsensitivePath_ProducesStableResult()
    {
        // Arrange — same path with different casing
        var path1 = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        var path2 = @"c:\program files\google\chrome\application\chrome.exe";

        // Act
        var appId1 = AppIdentityResolver.Resolve(path1);
        var appId2 = AppIdentityResolver.Resolve(path2);

        // Assert — should be the same (canonical path)
        Assert.Equal(appId1, appId2);
    }

    [Fact]
    public void Resolve_DifferentPaths_ProduceDifferentAppIds()
    {
        // Arrange
        var path1 = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
        var path2 = @"C:\Program Files\Mozilla Firefox\firefox.exe";

        // Act
        var appId1 = AppIdentityResolver.Resolve(path1);
        var appId2 = AppIdentityResolver.Resolve(path2);

        // Assert
        Assert.NotEqual(appId1, appId2);
    }
}
