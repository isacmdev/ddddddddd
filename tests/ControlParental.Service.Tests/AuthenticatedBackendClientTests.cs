// <copyright file="AuthenticatedBackendClientTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using ControlParental.Domain;
using ControlParental.Service;
using Xunit;

public sealed class AuthenticatedBackendClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task FetchPolicyAsync_DefinitiveSession_InjectsBearer()
    {
        var authority = Authority.Definitive("access-one", 7);
        HttpRequestMessage? observed = null;
        var handler = new StubHandler((request, _) => { observed = request;
            return Task.FromResult(Response(HttpStatusCode.OK, "{\"version\":2,\"policyJson\":\"{}\"}")); });
        var sut = Client(handler, authority);

        var result = await sut.FetchPolicyAsync("device-a", 1);

        Assert.True(result.Success);
        Assert.Equal("Bearer", observed!.Headers.Authorization?.Scheme);
        Assert.Equal("access-one", observed.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task FetchPolicyAsync_WithoutDefinitiveSession_DeniesWithoutHttp()
    {
        var authority = Authority.Denied();
        var sends = 0;
        var sut = Client(new StubHandler((_, _) => { sends++;
            return Task.FromResult(Response(HttpStatusCode.OK)); }), authority);

        var result = await sut.FetchPolicyAsync("device-a", 1);

        Assert.False(result.Success); Assert.Equal(0, sends);
    }

    [Fact]
    public async Task FetchPolicyAsync_DefinitiveIdentityTimeout_ReturnsRequestTimeoutWithoutSending()
    {
        var sends = 0;
        var authority = Authority.TimedOut();
        var sut = Client(new StubHandler((_, _) =>
        {
            sends++;
            return Task.FromResult(Response(HttpStatusCode.OK));
        }), authority, Options([]) with { RequestTimeout = TimeSpan.FromMilliseconds(20) });

        var operation = sut.FetchPolicyAsync("device-a", 1);
        var completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(operation, completed);
        var result = await operation;
        Assert.False(result.Success);
        Assert.Equal("Request timeout", result.ErrorMessage);
        Assert.Equal(0, sends);
    }

    [Fact]
    public async Task FetchPolicyAsync_AuthenticatedSendTimeout_ReturnsRequestTimeoutWithoutCallerCancellation()
    {
        var sends = 0;
        var sut = Client(new StubHandler(async (_, token) =>
        {
            sends++;
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Response(HttpStatusCode.OK);
        }), Authority.Definitive("access-one", 7), Options([]) with { RequestTimeout = TimeSpan.FromMilliseconds(20) });

        var operation = sut.FetchPolicyAsync("device-a", 1);
        var completed = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(1)));

        Assert.Same(operation, completed);
        var result = await operation;
        Assert.False(result.Success);
        Assert.Equal("Request timeout", result.ErrorMessage);
        Assert.Equal(1, sends);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task FetchPolicyAsync_IdentityRejection_InvalidatesWithoutReplay(HttpStatusCode status)
    {
        var authority = Authority.Definitive("access-one", 7);
        var sends = 0;
        var sut = Client(new StubHandler((_, _) => { sends++;
            return Task.FromResult(Response(status)); }), authority);

        var result = await sut.FetchPolicyAsync("device-a", 1);

        Assert.False(result.Success);
        Assert.Equal(1, sends);
        Assert.Equal(7, authority.InvalidatedGeneration);
    }

    [Fact]
    public async Task PushUsageLogsAsync_RetryAfter_ReusesIdempotencyKeyAndRetriesOnce()
    {
        var authority = Authority.Definitive("access-one", 7);
        var keys = new List<string?>();
        var delays = new List<TimeSpan>();
        var sends = 0;
        var handler = new StubHandler((request, _) =>
        {
            sends++;
            keys.Add(request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null);
            if (sends == 1)
            {
                var throttled = Response(HttpStatusCode.TooManyRequests);
                throttled.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(4));
                return Task.FromResult(throttled);
            }

            return Task.FromResult(Response(HttpStatusCode.Created));
        });
        var sut = Client(handler, authority, options: Options(delays));

        var result = await sut.PushUsageLogsAsync([Log("dedup-a")]);

        Assert.True(result.Success);
        Assert.Equal(2, sends);
        Assert.Equal(keys[0], keys[1]);
        Assert.False(string.IsNullOrWhiteSpace(keys[0]));
        Assert.Equal([TimeSpan.FromSeconds(4)], delays);
    }

    [Fact]
    public async Task PushUsageLogsAsync_Reconnect_UsesBoundedJitteredRetry()
    {
        var delays = new List<TimeSpan>();
        var sends = 0;
        var sut = Client(new StubHandler((_, _) => { sends++; return sends == 1
                ? Task.FromException<HttpResponseMessage>(new HttpRequestException("offline"))
                : Task.FromResult(Response(HttpStatusCode.Created)); }), Authority.Definitive("access-one", 7), options: Options(delays));

        var result = await sut.PushUsageLogsAsync([Log("dedup-a")]);

        Assert.True(result.Success);
        Assert.Equal(2, sends);
        Assert.Equal([TimeSpan.FromMilliseconds(125)], delays);
    }

    [Fact]
    public async Task PushUsageLogsAsync_PersistentReconnectFailure_StopsAtBoundWithoutDisclosure()
    {
        var sends = 0;
        var sut = Client(new StubHandler((_, _) => { sends++;
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("sensitive-url")); }), Authority.Definitive("access-one", 7), options: Options([]));

        var result = await sut.PushUsageLogsAsync([Log("dedup-a")]);

        Assert.False(result.Success);
        Assert.DoesNotContain("sensitive-url", result.ErrorMessage);
        Assert.Equal(2, sends);
    }

    [Fact]
    public async Task PushUsageLogsAsync_Cancellation_DoesNotRetry()
    {
        var sends = 0;
        using var cancellation = new CancellationTokenSource();
        var sut = Client(new StubHandler((_, token) => { sends++; cancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(token); }), Authority.Definitive("access-one", 7), options: Options([]));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.PushUsageLogsAsync([Log("dedup-a")], cancellation.Token));
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task PushUsageLogsAsync_RequestTimeout_FailsWithoutOverlappingRetry()
    {
        var sends = 0;
        var options = Options([]) with { RequestTimeout = TimeSpan.FromMilliseconds(20) };
        var sut = Client(new StubHandler(async (_, token) => { sends++;
            await Task.Delay(Timeout.InfiniteTimeSpan, token); return Response(HttpStatusCode.Created); }), Authority.Definitive("access-one", 7), options);

        var result = await sut.PushUsageLogsAsync([Log("dedup-a")]);

        Assert.False(result.Success);
        Assert.Equal(1, sends);
    }

    private static BackendClient Client(HttpMessageHandler handler, IBackendIdentityCoordinator authority, BackendReliabilityOptions? options = null)
        => new(new HttpClient(handler), "https://example.supabase.co", authority, options);

    private static BackendReliabilityOptions Options(List<TimeSpan> delays)
        => new(TimeSpan.FromSeconds(2), 2, (_, retryAfter) => retryAfter ?? TimeSpan.FromMilliseconds(125),
            (delay, _) => { delays.Add(delay); return Task.CompletedTask; });

    private static UsageLogEntry Log(string key)
        => new() { AppId = "app-a", Minutes = 1, ServerDate = Now, DedupKey = key };

    private static HttpResponseMessage Response(HttpStatusCode status, string content = "") => new(status) { Content = new StringContent(content) };

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }

    private sealed class Authority : IBackendIdentityCoordinator
    {
        private BackendDefinitiveSessionResult result = BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Forbidden);

        public BackendIdentityState CurrentState { get; private set; } = BackendIdentityState.Unpaired();

        public long? InvalidatedGeneration { get; private set; }

        public static Authority Definitive(string token, long generation)
        {
            var authority = new Authority { CurrentState = BackendIdentityState.Restore(
                BackendIdentityPhase.DefinitiveSession, generation, "device-a") };
            authority.result = BackendDefinitiveSessionResult.Authorized(new(generation, "device-a", token, Now.AddHours(1)));
            return authority;
        }

        public static Authority Denied() => new();

        public static Authority TimedOut() => new()
        {
            result = BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Timeout),
        };

        public Task<BackendDefinitiveSessionResult> GetDefinitiveSessionAsync(CancellationToken cancellationToken = default) => Task.FromResult(this.result);

        public Task InvalidateAsync(long generation, BackendIdentityErrorV1 reason, CancellationToken cancellationToken = default) {
            this.InvalidatedGeneration = generation;
            this.CurrentState = this.CurrentState.Invalidate();
            this.result = BackendDefinitiveSessionResult.Failed(reason);
            return Task.CompletedTask;
        }

        public Task<BackendPairingLifecycleResult> PairAsync(PairingCommand command, CancellationToken cancellationToken = default) => Task.FromResult(new BackendPairingLifecycleResult(BackendIdentityErrorV1.Forbidden));
    }
}
