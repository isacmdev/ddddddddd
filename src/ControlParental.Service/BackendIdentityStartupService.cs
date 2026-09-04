// <copyright file="BackendIdentityStartupService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;
using Microsoft.Extensions.Hosting;

internal sealed class BackendIdentityStartupService : IHostedService
{
    private readonly BackendIdentityCoordinator coordinator;
    private readonly BackendRealtimeIdentityAuthority? realtimeIdentityAuthority;
    private readonly System.TimeProvider timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly object wakeSync = new();
    private TaskCompletionSource<bool> lifecycleWake = NewWakeSource();
    private Task? startupTask;
    private Task? refreshLoop;

    public BackendIdentityStartupService(BackendIdentityCoordinator coordinator)
        : this(coordinator, realtimeIdentityAuthority: null)
    {
    }

    public BackendIdentityStartupService(
        BackendIdentityCoordinator coordinator,
        BackendRealtimeIdentityAuthority? realtimeIdentityAuthority,
        System.TimeProvider? timeProvider = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.realtimeIdentityAuthority = realtimeIdentityAuthority;
        this.timeProvider = timeProvider ?? System.TimeProvider.System;
        this.delay = delay ?? ((interval, token) => Task.Delay(interval, this.timeProvider, token));
        this.coordinator.LifecycleChanged += this.OnLifecycleChanged;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.startupTask = this.coordinator.RunOwnedOperationAsync(
            BackendIdentityOperationKind.HostedStart,
            -1,
            this.StartCoreAsync,
            cancellationToken);
        _ = this.startupTask.ContinueWith(
            _ => this.LaunchRefreshLoop(cancellationToken),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
        return this.startupTask;
    }

    private void LaunchRefreshLoop(CancellationToken cancellationToken)
    {
        if (this.realtimeIdentityAuthority is not null &&
            !cancellationToken.IsCancellationRequested &&
            this.coordinator.LifecycleSnapshot.Admission == BackendIdentityAdmissionState.Open)
        {
            this.refreshLoop = this.coordinator.RunOwnedOperationAsync(
                BackendIdentityOperationKind.HostedRefreshLoop,
                -1,
                this.RunRefreshLoopAsync,
                cancellationToken);
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        var result = await this.coordinator.InitializeAsync(cancellationToken).ConfigureAwait(false);
        if (result != BackendIdentityErrorV1.None)
        {
            return;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (this.realtimeIdentityAuthority is null)
        {
            return;
        }

        await this.realtimeIdentityAuthority.RefreshAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await this.coordinator.CloseRefreshAdmissionAndDrainAsync(cancellationToken).ConfigureAwait(false);
        this.realtimeIdentityAuthority?.Clear();
        this.startupTask = null;
        this.refreshLoop = null;
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var authority = this.realtimeIdentityAuthority;
            if (authority is null)
            {
                return;
            }
            while (!cancellationToken.IsCancellationRequested)
            {
                var wake = this.CaptureWakeTask();
                var identity = authority.Current;
                if (identity is null)
                {
                    await authority.RefreshAsync(cancellationToken).ConfigureAwait(false);
                    await this.DelayOrWakeAsync(TimeSpan.FromMinutes(1), wake, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var untilRefresh = identity.ExpiresAt - this.timeProvider.GetUtcNow() - TimeSpan.FromMinutes(2);
                if (untilRefresh <= TimeSpan.Zero)
                {
                    await authority.RefreshAsync(cancellationToken).ConfigureAwait(false);
                    await this.DelayOrWakeAsync(TimeSpan.FromSeconds(1), wake, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                await this.DelayOrWakeAsync(untilRefresh, wake, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private Task DelayAsync(TimeSpan interval, CancellationToken cancellationToken)
        => this.delay(interval > TimeSpan.Zero ? interval : TimeSpan.Zero, cancellationToken);

    private Task CaptureWakeTask()
    {
        lock (this.wakeSync)
        {
            return this.lifecycleWake.Task;
        }
    }

    private async Task DelayOrWakeAsync(TimeSpan interval, Task wake, CancellationToken cancellationToken)
    {
        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delayed = this.DelayAsync(interval, delayCancellation.Token);
        var completed = await Task.WhenAny(delayed, wake).ConfigureAwait(false);
        delayCancellation.Cancel();
        try
        {
            await delayed.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (delayCancellation.IsCancellationRequested)
        {
            // The losing delay is always drained before returning to the loop.
        }
        if (completed == wake)
        {
            lock (this.wakeSync)
            {
                if (this.lifecycleWake.Task.IsCompleted)
                {
                    this.lifecycleWake = NewWakeSource();
                }
            }
        }

    }

    private void OnLifecycleChanged(object? sender, BackendIdentityLifecycleEventArgs args)
    {
        lock (this.wakeSync)
        {
            this.lifecycleWake.TrySetResult(true);
        }
    }

    private static TaskCompletionSource<bool> NewWakeSource()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
