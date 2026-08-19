// <copyright file="WnsLifecycleTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System.Text;
using System.Text.Json;
using ControlParental.App.UI;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// T19 coverage for WNS lifecycle and sync-only receive behavior. The actual
/// <c>PushNotificationManager</c> path requires a packaged WinUI identity, so
/// lifecycle decisions remain pure while the raw notification seam is exercised
/// through a local named-pipe harness. WNS payloads are intentionally opaque;
/// every receipt emits only the typed <see cref="TriggerSync"/> signal.
/// </summary>
public class WnsLifecycleTests
{
    [Fact]
    public void ShouldRenewWhenExpirationIsNullReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

        Assert.True(WnsChannelPlanner.ShouldRenew(now, expirationUtc: null));
    }

    [Fact]
    public void ShouldRenewWhenMoreThanWindowAwayReturnsFalse()
    {
        var now = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);
        var expiration = now.AddDays(WnsChannelPlanner.RenewalWindowDays + 5);

        Assert.False(WnsChannelPlanner.ShouldRenew(now, expiration));
    }

    [Fact]
    public void ShouldRenewWhenWithinWindowReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);
        var expiration = now.AddDays(WnsChannelPlanner.RenewalWindowDays - 1);

        Assert.True(WnsChannelPlanner.ShouldRenew(now, expiration));
    }

    [Fact]
    public void ShouldRenewWhenAlreadyExpiredReturnsTrue()
    {
        var now = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);
        var expiration = now.AddDays(-1);

        Assert.True(WnsChannelPlanner.ShouldRenew(now, expiration));
    }

    [Fact]
    public void ResolveExpirationTrustsChannelExpirationWhenProvided()
    {
        var now = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);
        var channelExpiration = now.AddDays(20);

        var resolved = WnsChannelPlanner.ResolveExpiration(now, channelExpiration);

        Assert.Equal(channelExpiration, resolved);
    }

    [Fact]
    public void ResolveExpirationFallsBackToCanonicalWindowWhenMissing()
    {
        var now = new DateTimeOffset(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

        var resolved = WnsChannelPlanner.ResolveExpiration(now, channelExpiration: null);

        // 30-day canonical window minus the renewal safety margin.
        Assert.Equal(now.AddDays(30 - WnsChannelPlanner.RenewalWindowDays), resolved);
    }

    [Fact]
    public void RenewalCheckIntervalIsAtLeastOneHour()
    {
        // Cheap invariant: at least one hour, at most one day.
        Assert.True(TimeSpan.FromHours(1) <= WnsChannelPlanner.RenewalCheckInterval);
        Assert.True(TimeSpan.FromDays(1) >= WnsChannelPlanner.RenewalCheckInterval);
    }

    [Fact]
    public void WnsChannelRegistrationRoundTripsThroughSourceGenContext()
    {
        var registration = new WnsChannelRegistration
        {
            Channel = "wns",
            PushHandle = "https://db3p.notify.windows.com/?token=abc",
            ExpiresAt = "2026-08-22T00:00:00.0000000+00:00",
        };

        var json = JsonSerializer.Serialize(
            registration, WnsJsonContext.Default.WnsChannelRegistration);

        // Snake_case wire contract is preserved by the source-gen catalogue.
        Assert.Contains("\"channel\"", json);
        Assert.Contains("\"push_handle\"", json);
        Assert.Contains("\"expires_at\"", json);

        var roundTripped = JsonSerializer.Deserialize(
            json, WnsJsonContext.Default.WnsChannelRegistration);

        Assert.NotNull(roundTripped);
        Assert.Equal("wns", roundTripped!.Channel);
        Assert.Equal(registration.PushHandle, roundTripped.PushHandle);
        Assert.Equal(registration.ExpiresAt, roundTripped.ExpiresAt);
    }

    [Fact]
    public async Task StartAsyncWhenWnsConfigurationIsAbsentCompletesWithoutBlocking()
    {
        var previousAppId = Environment.GetEnvironmentVariable("WNS_AAD_APP_ID");
        Environment.SetEnvironmentVariable("WNS_AAD_APP_ID", null);

        try
        {
            using var handler = new WnsPushNotificationHandler(
                "https://example.supabase.co",
                "test-key");

            var startTask = handler.StartAsync();
            var completedTask = await Task.WhenAny(startTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);

            Assert.Same(startTask, completedTask);
            await startTask.ConfigureAwait(false);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WNS_AAD_APP_ID", previousAppId);
        }
    }

    [Fact]
    public async Task RegisterChannelAsync_SendsRegisterWnsChannelOverTypedServiceIpc()
    {
        var channel = new RecordingUIChannel();
        var sut = new WnsPushNotificationHandler(channel);
        var expiresAt = new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);
        var expected = new WnsRegistrationResult("operation", WnsRegistrationStatus.Accepted, "correlation");
        channel.QueryResult = expected;

        var result = await sut.RegisterChannelAsync(
            "https://db3p.notify.windows.com/?token=abc",
            expiresAt,
            CancellationToken.None);

        Assert.Same(expected, result);
        var message = Assert.IsType<RegisterWnsChannel>(channel.Messages.Single());
        Assert.Equal("https://db3p.notify.windows.com/?token=abc", message.ChannelUri);
        Assert.Equal("wns", message.Channel);
        Assert.Equal(expiresAt, message.ExpiresAt);
        Assert.False(string.IsNullOrWhiteSpace(message.OperationId));
    }

    [Fact]
    public async Task RegisterChannelAsync_ServiceUnavailable_FailsClosedWithoutBackendFallback()
    {
        var channel = new RecordingUIChannel
        {
            SendException = new InvalidOperationException("Service unavailable."),
        };
        var sut = new WnsPushNotificationHandler(channel);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RegisterChannelAsync(
            "https://db3p.notify.windows.com/?token=abc",
            DateTimeOffset.UtcNow.AddDays(1),
            CancellationToken.None));

        Assert.Single(channel.Messages);
        Assert.Empty(channel.SentMessages);
    }

    [Theory]
    [InlineData(WnsRegistrationStatus.Denied)]
    [InlineData(WnsRegistrationStatus.Retryable)]
    public async Task RegisterChannelAsync_PreservesTypedNonAcceptedResultWithoutRetry(WnsRegistrationStatus status)
    {
        var channel = new RecordingUIChannel
        {
            QueryResult = new WnsRegistrationResult("operation", status, "correlation"),
        };
        var sut = new WnsPushNotificationHandler(channel);

        var result = await sut.RegisterChannelAsync(
            "https://db3p.notify.windows.com/?token=abc",
            DateTimeOffset.UtcNow.AddDays(1),
            CancellationToken.None);

        Assert.Equal(channel.QueryResult, result);
        Assert.Equal(status, result!.Status);
        Assert.Single(channel.Messages);
        Assert.Empty(channel.SentMessages);
    }

    [Fact]
    public async Task RegisterChannelAsync_NullResultFailsClosedWithoutFallback()
    {
        var channel = new RecordingUIChannel();
        var sut = new WnsPushNotificationHandler(channel);

        var result = await sut.RegisterChannelAsync(
            "https://db3p.notify.windows.com/?token=abc",
            DateTimeOffset.UtcNow.AddDays(1),
            CancellationToken.None);

        Assert.Null(result);
        Assert.Single(channel.Messages);
        Assert.Empty(channel.SentMessages);
    }

    [Fact]
    public async Task RegisterChannelAsync_SeparateInvocationsUseDistinctOperationIds()
    {
        var channel = new RecordingUIChannel();
        var sut = new WnsPushNotificationHandler(channel);

        await sut.RegisterChannelAsync(
            "https://db3p.notify.windows.com/?token=abc",
            DateTimeOffset.UtcNow.AddDays(1),
            CancellationToken.None);
        await sut.RegisterChannelAsync(
            "https://db3p.notify.windows.com/?token=abc",
            DateTimeOffset.UtcNow.AddDays(1),
            CancellationToken.None);

        var requests = channel.Messages.Cast<RegisterWnsChannel>().ToArray();
        Assert.Equal(2, requests.Length);
        Assert.All(requests, request => Assert.False(string.IsNullOrWhiteSpace(request.OperationId)));
        Assert.NotEqual(requests[0].OperationId, requests[1].OperationId);
    }

    [Fact]
    public async Task RegisterChannelAsync_PropagatesCancellationToTypedServiceIpc()
    {
        using var cts = new CancellationTokenSource();
        var channel = new RecordingUIChannel();
        var sut = new WnsPushNotificationHandler(channel);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.RegisterChannelAsync(
            "https://db3p.notify.windows.com/?token=abc",
            DateTimeOffset.UtcNow.AddDays(1),
            cts.Token));

            Assert.Equal(cts.Token, channel.CancellationToken);
    }

    [Fact]
    public async Task HandleRawNotificationAsyncWithMalformedPayloadEmitsTypedTriggerSync()
    {
        var malformedPayload = Encoding.UTF8.GetBytes("{not-json");

        var json = await CaptureTriggerSyncJsonAsync(malformedPayload).ConfigureAwait(false);

        AssertTriggerSync(json);
    }

    [Fact]
    public async Task HandleRawNotificationAsyncWithValidPayloadEmitsTypedTriggerSync()
    {
        var validPayload = Encoding.UTF8.GetBytes("{\"event\":\"sync\",\"version\":42}");

        var json = await CaptureTriggerSyncJsonAsync(validPayload).ConfigureAwait(false);

        AssertTriggerSync(json);
    }

    private static async Task<string> CaptureTriggerSyncJsonAsync(ReadOnlyMemory<byte> rawPayload)
    {
        var capturedJson = string.Empty;
        var signalTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var handler = new WnsPushNotificationHandler(
            "https://example.supabase.co",
            "test-key",
            (json, _) =>
            {
                capturedJson = json;
                signalTcs.TrySetResult(json);
                return Task.CompletedTask;
            });

        await handler.HandleRawNotificationAsync(rawPayload, CancellationToken.None).ConfigureAwait(false);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(signalTcs.Task, Task.Delay(Timeout.InfiniteTimeSpan, timeoutCts.Token)).ConfigureAwait(false);
        if (completedTask != signalTcs.Task)
        {
            throw new TimeoutException("Timed out waiting for TriggerSync serialization capture.");
        }

        return capturedJson;
    }

    [Fact]
    public void TriggerSyncRoundTripsThroughSourceGeneratedContext()
    {
        var original = new TriggerSync();

        var json = JsonSerializer.Serialize(
            original,
            ControlParental.App.UI.Interop.UIMessagesJsonContext.Default.TriggerSync);
        var roundTripped = JsonSerializer.Deserialize(
            json,
            ControlParental.App.UI.Interop.UIMessagesJsonContext.Default.TriggerSync);

        Assert.NotNull(roundTripped);
        Assert.Equal(nameof(TriggerSync), roundTripped!.MessageType);
    }

    private static void AssertTriggerSync(string json)
    {
        var message = JsonSerializer.Deserialize(
            json,
            ControlParental.App.UI.Interop.UIMessagesJsonContext.Default.TriggerSync);

        Assert.NotNull(message);
        Assert.Equal(nameof(TriggerSync), message!.MessageType);
    }

    private sealed class RecordingUIChannel : IUIChannel
    {
        public List<object> Messages { get; } = new();

        public List<object> SentMessages { get; } = new();

        public Exception? SendException { get; init; }

        public WnsRegistrationResult? QueryResult { get; set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<TResponse?> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken ct = default)
            where TQuery : ControlParental.Domain.IUIMessage
            where TResponse : class, ControlParental.Domain.IUIMessage
        {
            this.CancellationToken = ct;
            this.Messages.Add(query);
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<TResponse?>(ct);
            }

            return this.SendException is null
                ? Task.FromResult(this.QueryResult as TResponse)
                : Task.FromException<TResponse?>(this.SendException);
        }

        public Task SendAsync<T>(T message, CancellationToken ct = default)
            where T : ControlParental.Domain.IUIMessage
        {
            this.CancellationToken = ct;
            this.Messages.Add(message);
            this.SentMessages.Add(message);
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled(ct);
            }

            return this.SendException is null ? Task.CompletedTask : Task.FromException(this.SendException);
        }
    }
}
