namespace ControlParental.Service.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ControlParental.Service;
using Xunit;

public sealed class SessionManagerLifecycleTests
{
    [Fact]
    public async Task WatcherReportsIndependentSessionsAndStopsPromptly()
    {
        var started = new List<int>();
        var ended = new List<int>();
        var snapshots = new[]
        {
            new SessionWatcher.SessionSnapshot(7, false),
            new SessionWatcher.SessionSnapshot(8, false),
        };
        using var watcher = new SessionWatcher(
            "child", started.Add, ended.Add, _ => { }, _ => { },
            () => snapshots, TimeSpan.FromMilliseconds(1));

        await watcher.StartAsync();
        await watcher.StartAsync();
        await Task.Delay(20);
        await watcher.StopAsync();

        Assert.Contains(7, started);
        Assert.Contains(8, started);
        Assert.Empty(ended);
    }

    [Fact]
    public async Task WatcherEndsOnlyTheSessionRemovedDuringFastSwitch()
    {
        var ended = new List<int>();
        var firstPoll = true;
        var firstSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new SessionWatcher(
            "child", _ => { }, ended.Add, _ => { }, _ => { },
            () =>
            {
                if (firstPoll)
                {
                    firstPoll = false;
                    firstSeen.TrySetResult(true);
                    return new[]
                    {
                        new SessionWatcher.SessionSnapshot(7, false),
                        new SessionWatcher.SessionSnapshot(8, false),
                    };
                }

                secondSeen.TrySetResult(true);
                return new[] { new SessionWatcher.SessionSnapshot(8, false) };
            },
            TimeSpan.FromMilliseconds(1));

        await watcher.StartAsync();
        await firstSeen.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await secondSeen.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await watcher.StopAsync();

        Assert.Equal(new[] { 7 }, ended);
    }

    [Fact]
    public async Task WatcherCancellationBoundsProviderFailureShutdown()
    {
        var calls = 0;
        var providerCalled = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var watcher = new SessionWatcher(
            "child", _ => { }, _ => { }, _ => { }, _ => { },
            () =>
            {
                calls++;
                providerCalled.TrySetResult(null);
                throw new InvalidOperationException("synthetic provider failure");
            },
            TimeSpan.FromMilliseconds(1));

        await watcher.StartAsync();
        await providerCalled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var started = DateTime.UtcNow;
        try
        {
            await watcher.StopAsync();
        }
        catch (TaskCanceledException)
        {
            // The provider's bounded retry delay observes the same cancellation.
        }

        Assert.True(calls > 0);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void WatcherRejectsMissingLifecycleCallbacks()
    {
        Assert.Throws<ArgumentNullException>(() => new SessionWatcher(
            "child", null!, _ => { }, _ => { }, _ => { }));
    }

}
