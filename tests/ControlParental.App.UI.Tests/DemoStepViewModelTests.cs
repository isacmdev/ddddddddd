// <copyright file="DemoStepViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.Domain;
using Xunit;
using AppUI = ControlParental.App.UI;

public sealed class DemoStepViewModelTests
{
    [Fact]
    public async Task RunDemoAsyncSendsShowOverlayThenHideOverlayAndFirstWinEvent()
    {
        var channel = new RecordingUIChannel();
        var viewModel = new AppUI.DemoStepViewModel(channel, overlayDuration: TimeSpan.Zero);

        await viewModel.RunDemoCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.Collection(
            channel.SentMessages,
            message => Assert.Equal("demo", Assert.IsType<AppUI.ShowOverlayCommand>(message).Reason),
            message => Assert.IsType<AppUI.HideOverlayCommand>(message),
            message => Assert.Equal(
                FunnelEventType.OnboardingFirstWin.ToString(),
                Assert.IsType<AppUI.RecordFunnelEvent>(message).EventName));
    }

    [Fact]
    public async Task RunDemoAsyncOnIpcFailureDoesNotInvokeOnCompleted()
    {
        var channel = new RecordingUIChannel { SendException = new IOException("pipe unavailable") };
        var completed = false;
        var viewModel = new AppUI.DemoStepViewModel(
            channel,
            () => completed = true,
            TimeSpan.Zero);

        await viewModel.RunDemoCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.False(completed);
        Assert.False(viewModel.IsCompleted);
        Assert.True(viewModel.HasError);
    }

    [Fact]
    public async Task RunDemoAsyncOnSuccessInvokesOnCompletedAndEmitsFirstWin()
    {
        var channel = new RecordingUIChannel();
        var completed = false;
        var viewModel = new AppUI.DemoStepViewModel(
            channel,
            () => completed = true,
            TimeSpan.Zero);

        await viewModel.RunDemoCommand.ExecuteAsync(null).ConfigureAwait(false);

        Assert.True(completed);
        Assert.True(viewModel.IsCompleted);
        Assert.Contains(channel.SentMessages, message => message is AppUI.RecordFunnelEvent);
    }
}
