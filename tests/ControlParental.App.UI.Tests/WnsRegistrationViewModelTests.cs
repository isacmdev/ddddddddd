// <copyright file="WnsRegistrationViewModelTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI;
using ControlParental.Domain;
using System.Threading;
using Xunit;

/// <summary>
/// Strict-TDD RED coverage for the application-level WNS registration UI seam.
/// These tests intentionally precede the view-model and port implementation.
/// </summary>
public sealed class WnsRegistrationViewModelTests
{
    [Fact]
    public async Task RegisterAsync_ObtainsChannelOnceAndForwardsItsIntentExactlyOnce()
    {
        var intent = new WnsChannelIntent(
            "channel-uri-secret",
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var provider = new FakeWnsChannelProvider { Intent = intent };
        var port = new FakeWnsRegistrationPort
        {
            Result = new WnsRegistrationResult("operation-id", WnsRegistrationStatus.Accepted, "correlation-id"),
        };
        var viewModel = new WnsRegistrationViewModel(port, provider);

        await viewModel.RegisterAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(1, provider.InvocationCount);
        Assert.Equal(1, port.InvocationCount);
        Assert.Equal(intent.Uri, port.ChannelUri);
        Assert.Equal(intent.ExpiresAt, port.ExpiresAt);
        Assert.Equal(WnsRegistrationDisplayStatus.Accepted, viewModel.Status);
    }

    [Fact]
    public async Task RegisterAsync_WhenChannelUnavailable_DoesNotInvokePortAndClearsBusy()
    {
        var provider = new FakeWnsChannelProvider { Intent = null };
        var port = new FakeWnsRegistrationPort();
        var viewModel = new WnsRegistrationViewModel(port, provider);

        await viewModel.RegisterAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(1, provider.InvocationCount);
        Assert.Equal(0, port.InvocationCount);
        Assert.Equal(WnsRegistrationDisplayStatus.Unavailable, viewModel.Status);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RegisterAsync_ConcurrentCallsShareOneInFlightOperation()
    {
        var provider = new FakeWnsChannelProvider
        {
            Intent = new WnsChannelIntent("channel-uri", DateTimeOffset.UtcNow),
            Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var port = new FakeWnsRegistrationPort
        {
            Result = new WnsRegistrationResult("operation-id", WnsRegistrationStatus.Accepted, "correlation-id"),
            Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var viewModel = new WnsRegistrationViewModel(port, provider);

        var first = viewModel.RegisterAsync(CancellationToken.None);
        await provider.Entered.Task.ConfigureAwait(false);
        var second = viewModel.RegisterAsync(CancellationToken.None);

        provider.Gate.SetResult(true);
        await port.Entered.Task.ConfigureAwait(false);
        port.Gate.SetResult(true);
        await Task.WhenAll(first, second).ConfigureAwait(false);

        Assert.Equal(1, provider.InvocationCount);
        Assert.Equal(1, port.InvocationCount);
    }

    [Fact]
    public async Task RegisterAsync_ForwardsCancellationToProviderAndPortAndClearsBusy()
    {
        using var cts = new CancellationTokenSource();
        var provider = new FakeWnsChannelProvider
        {
            Intent = new WnsChannelIntent("channel-uri", DateTimeOffset.UtcNow),
        };
        var port = new FakeWnsRegistrationPort { WaitForCancellation = true };
        var viewModel = new WnsRegistrationViewModel(port, provider);
        var registration = viewModel.RegisterAsync(cts.Token);

        await port.Entered.Task.ConfigureAwait(false);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registration).ConfigureAwait(false);

        Assert.Equal(cts.Token, provider.ObservedToken);
        Assert.Equal(cts.Token, port.ObservedToken);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RegisterAsync_PendingOffline_ExposesTypedSafeStatusAndClearsBusy()
    {
        var port = new FakeWnsRegistrationPort
        {
            Result = new WnsRegistrationResult("operation-secret", WnsRegistrationStatus.PendingOffline, "correlation-secret"),
        };
        var viewModel = new WnsRegistrationViewModel(port);

        await viewModel.RegisterAsync("https://notify.windows.com/channel-secret", CancellationToken.None)
            .ConfigureAwait(false);

        Assert.Equal(WnsRegistrationDisplayStatus.PendingOffline, viewModel.Status);
        Assert.False(viewModel.IsBusy);
        Assert.DoesNotContain("operation-secret", viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("correlation-secret", viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("channel-secret", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterAsync_NullResult_MapsToUnavailableAndNeverSuccess()
    {
        var viewModel = new WnsRegistrationViewModel(new FakeWnsRegistrationPort { Result = null });

        await viewModel.RegisterAsync("https://notify.windows.com/channel", CancellationToken.None)
            .ConfigureAwait(false);

        Assert.Equal(WnsRegistrationDisplayStatus.Unavailable, viewModel.Status);
        Assert.NotEqual(WnsRegistrationDisplayStatus.Accepted, viewModel.Status);
        Assert.False(viewModel.IsBusy);
    }

    [Theory]
    [InlineData(WnsRegistrationStatus.Accepted, WnsRegistrationDisplayStatus.Accepted)]
    [InlineData(WnsRegistrationStatus.Denied, WnsRegistrationDisplayStatus.Denied)]
    [InlineData(WnsRegistrationStatus.Retryable, WnsRegistrationDisplayStatus.Retryable)]
    [InlineData(WnsRegistrationStatus.PendingOffline, WnsRegistrationDisplayStatus.PendingOffline)]
    public async Task RegisterAsync_TypedOutcomesRemainDistinctAndCredentialFree(
        WnsRegistrationStatus resultStatus,
        WnsRegistrationDisplayStatus expectedStatus)
    {
        var port = new FakeWnsRegistrationPort
        {
            Result = new WnsRegistrationResult("operation-id", resultStatus, "correlation-id"),
        };
        var viewModel = new WnsRegistrationViewModel(port);

        await viewModel.RegisterAsync("https://notify.windows.com/channel-uri", CancellationToken.None)
            .ConfigureAwait(false);

        Assert.Equal(expectedStatus, viewModel.Status);
        Assert.DoesNotContain("operation-id", viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("correlation-id", viewModel.StatusText, StringComparison.Ordinal);
        Assert.DoesNotContain("channel-uri", viewModel.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterAsync_PropagatesCancellation_ClearsBusyAndInvokesPortOnce()
    {
        using var cts = new CancellationTokenSource();
        var port = new FakeWnsRegistrationPort { WaitForCancellation = true };
        var viewModel = new WnsRegistrationViewModel(port);
        var registration = viewModel.RegisterAsync("https://notify.windows.com/channel", cts.Token);

        Assert.True(viewModel.IsBusy);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => registration).ConfigureAwait(false);

        Assert.False(viewModel.IsBusy);
        Assert.Equal(1, port.InvocationCount);
    }

    private sealed class FakeWnsRegistrationPort : IWnsRegistrationPort
    {
        public WnsRegistrationResult? Result { get; init; }

        public bool WaitForCancellation { get; init; }

        public TaskCompletionSource<bool>? Gate { get; init; }

        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string? ChannelUri { get; private set; }

        public DateTimeOffset ExpiresAt { get; private set; }

        public CancellationToken ObservedToken { get; private set; }

        public int InvocationCount { get; private set; }

        public async Task<WnsRegistrationResult?> RegisterChannelAsync(
            string channelUri,
            DateTimeOffset expiresAt,
            CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            this.ChannelUri = channelUri;
            this.ExpiresAt = expiresAt;
            this.ObservedToken = cancellationToken;
            this.Entered.TrySetResult(true);
            if (this.Gate is not null)
            {
                await this.Gate.Task.ConfigureAwait(false);
            }

            if (this.WaitForCancellation)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
            }

            return this.Result;
        }
    }

    private sealed class FakeWnsChannelProvider : IWnsChannelProvider
    {
        public WnsChannelIntent? Intent { get; init; }

        public TaskCompletionSource<bool>? Gate { get; init; }

        public TaskCompletionSource<bool> Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int InvocationCount { get; private set; }

        public CancellationToken ObservedToken { get; private set; }

        public async Task<WnsChannelIntent?> CreateChannelAsync(CancellationToken cancellationToken = default)
        {
            this.InvocationCount++;
            this.ObservedToken = cancellationToken;
            this.Entered.TrySetResult(true);
            if (this.Gate is not null)
            {
                await this.Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return this.Intent;
        }
    }
}
