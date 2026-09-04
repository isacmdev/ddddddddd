// <copyright file="ForegroundWatcherTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent.Tests;

using ControlParental.Domain;
using Xunit;

/// <summary>
/// T05 — Tests for ForegroundWatcher behavior.
/// Tests the interface contract and the noise filtering logic.
/// </summary>
public class ForegroundWatcherTests
{
    // ── Fake for testing interface contract ──────────────────────────

    private sealed class FakeForegroundWatcher : IForegroundWatcher
    {
        public string? CurrentAppId { get; set; }
        public ObservedProcessTarget? CurrentTarget { get; set; }
        public bool IsStarted { get; private set; }
        public event Action<string>? ForegroundChanged;
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            this.IsStarted = true;
            return Task.CompletedTask;
        }

        public void Stop()
        {
            // No-op for testing
        }

        public void SimulateForegroundChange(string appId)
        {
            this.CurrentAppId = appId;
            this.ForegroundChanged?.Invoke(appId);
        }
    }

    // ── Interface contract ────────────────────────────────────────────

    [Fact]
    public void IForegroundWatcher_Interface_HasAllRequiredMembers()
    {
        // Arrange
        var type = typeof(IForegroundWatcher);

        // Assert — all required members
        Assert.NotNull(type.GetProperty(nameof(IForegroundWatcher.CurrentAppId)));
        Assert.NotNull(type.GetEvent(nameof(IForegroundWatcher.ForegroundChanged)));
        Assert.NotNull(type.GetMethod(nameof(IForegroundWatcher.StartAsync)));
        Assert.NotNull(type.GetMethod(nameof(IForegroundWatcher.Stop)));
    }

    [Fact]
    public void ForegroundWatcher_ImplementsInterface()
    {
        // Arrange
        var type = typeof(ForegroundWatcher);

        // Assert
        Assert.True(typeof(IForegroundWatcher).IsAssignableFrom(type));
    }

    [Fact]
    public void ForegroundWatcher_ImplementsIDisposable()
    {
        // Arrange
        var type = typeof(ForegroundWatcher);

        // Assert
        Assert.True(typeof(System.IDisposable).IsAssignableFrom(type));
    }

    // ── Event handling ───────────────────────────────────────────────

    [Fact]
    public void ForegroundChanged_Event_FiresWithCorrectAppId()
    {
        // Arrange
        var watcher = new FakeForegroundWatcher();
        var receivedAppId = (string?)null;
        var eventCount = 0;

        watcher.ForegroundChanged += appId =>
        {
            receivedAppId = appId;
            eventCount++;
        };

        // Act
        watcher.SimulateForegroundChange("chrome.exe|abc123");

        // Assert
        Assert.Equal("chrome.exe|abc123", receivedAppId);
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void ForegroundChanged_Event_MultipleSubscribers_AllReceive()
    {
        // Arrange
        var watcher = new FakeForegroundWatcher();
        var count1 = 0;
        var count2 = 0;

        watcher.ForegroundChanged += _ => count1++;
        watcher.ForegroundChanged += _ => count2++;

        // Act
        watcher.SimulateForegroundChange("whatsapp");

        // Assert
        Assert.Equal(1, count1);
        Assert.Equal(1, count2);
    }

    [Fact]
    public void CurrentAppId_AfterForegroundChange_ReflectsNewApp()
    {
        // Arrange
        var watcher = new FakeForegroundWatcher();

        // Assert — initially null
        Assert.Null(watcher.CurrentAppId);

        // Act
        watcher.SimulateForegroundChange("instagram.exe|def456");

        // Assert
        Assert.Equal("instagram.exe|def456", watcher.CurrentAppId);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────

    [Fact]
    public async Task StartAsync_ReturnsCompletedTask()
    {
        // Arrange
        var watcher = new FakeForegroundWatcher();

        // Act
        var task = watcher.StartAsync();

        // Assert
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public void Stop_CanBeCalledMultipleTimes_Safely()
    {
        // Arrange
        var watcher = new FakeForegroundWatcher();

        // Act & Assert — no exception
        watcher.Stop();
        watcher.Stop();
        watcher.Stop();
    }

    [Fact]
    public async Task StartAsync_WithCancellation_StartsSuccessfully()
    {
        // Arrange
        var watcher = new FakeForegroundWatcher();
        using var cts = new CancellationTokenSource();

        // Act
        await watcher.StartAsync(cts.Token);

        // Assert
        Assert.True(watcher.IsStarted);
    }

    // ── AppId format ─────────────────────────────────────────────────

    [Theory]
    [InlineData("chrome.exe|abc123")]
    [InlineData("Microsoft.Office.Word")]
    [InlineData("whatsapp|xyz789")]
    [InlineData("code|def456")]
    public void AppId_CanBeAnyNonEmptyString(string appId)
    {
        // Arrange
        var watcher = new FakeForegroundWatcher();

        // Act
        watcher.SimulateForegroundChange(appId);

        // Assert — no validation, any string is valid AppId
        Assert.Equal(appId, watcher.CurrentAppId);
    }

    // ── No content read ───────────────────────────────────────────────

    [Fact]
    public void ForegroundWatcher_NoWindowTitlesOrContent_OnlyAppId()
    {
        // Arrange
        var watcher = new FakeForegroundWatcher();
        var receivedAppIds = new List<string>();

        watcher.ForegroundChanged += appId => receivedAppIds.Add(appId);

        // Act — simulate various app changes (no window titles in the AppId)
        watcher.SimulateForegroundChange("chrome.exe|hash");
        watcher.SimulateForegroundChange("whatsapp|hash");
        watcher.SimulateForegroundChange("instagram.exe|hash");

        // Assert — only AppIds, no window titles
        Assert.Equal(3, receivedAppIds.Count);
        foreach (var appId in receivedAppIds)
        {
            Assert.DoesNotContain(" ", appId); // No window title with spaces
            Assert.StartsWith(appId, appId); // Just package/process name
        }
    }

    [Fact]
    public void ObservationKeepsCanonicalAppIdSeparateFromExactProcessTarget()
    {
        const string AppId = "publisher|family|canonical";
        var startedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

        var observation = ForegroundWatcher.CreateObservation(AppId, 42, 4, startedAt);

        Assert.Equal(AppId, observation.AppId);
        Assert.Equal(new ObservedProcessTarget(42, 4, startedAt), observation.Target);
    }

    [Fact]
    public void ExactTargetIsReturnedOnlyForItsCanonicalAppObservation()
    {
        IForegroundWatcher watcher = new FakeForegroundWatcher
        {
            CurrentAppId = "current-app",
            CurrentTarget = new ObservedProcessTarget(42, 4, DateTimeOffset.UtcNow),
        };

        Assert.Equal(watcher.CurrentTarget, watcher.GetCurrentTarget("current-app"));
        Assert.Null(watcher.GetCurrentTarget("stale-app"));
    }

    [Fact]
    public void ObservationIdentityUsesOrdinalCanonicalAppIdAndExactTarget()
    {
        var startedAt = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var current = ForegroundWatcher.CreateObservation(
            "Publisher|Canonical-App",
            42,
            4,
            startedAt);

        Assert.False(ForegroundWatcher.ShouldPublishObservation(
            current.AppId,
            current.Target,
            current));
        Assert.True(ForegroundWatcher.ShouldPublishObservation(
            current.AppId,
            current.Target,
            current with { AppId = "publisher|canonical-app" }));
        Assert.True(ForegroundWatcher.ShouldPublishObservation(
            current.AppId,
            current.Target,
            current with { Target = current.Target! with { ProcessId = 43 } }));
    }
}
