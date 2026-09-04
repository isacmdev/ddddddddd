// <copyright file="BackendIdentityLifecycleClientTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using System.Text;
using Xunit;

public sealed class BackendIdentityLifecycleClientTests
{
    [Fact]
    public async Task Lifecycle_HappyPath_UsesPrePairBearerThenReturnsExactDefinitiveClaim()
    {
        var handler = new QueueHandler(
            Json(HttpStatusCode.OK, "{\"access_token\":\"pre-access\",\"refresh_token\":\"pre-refresh\",\"expires_at\":1893456000}"),
            Json(HttpStatusCode.OK, "{\"device_id\":\"device-a\",\"parent_id\":\"parent-a\",\"policy_version\":3}"),
            Json(HttpStatusCode.OK, $"{{\"access_token\":\"{Jwt("device-a")}\",\"refresh_token\":\"rotated-refresh\",\"expires_at\":1893456000}}"));
        var sut = new BackendIdentityLifecycleClient(
            new HttpClient(handler), "https://example.supabase.co", "publishable-key");

        var prePair = await sut.RecoverOrCreatePrePairSessionAsync(CancellationToken.None);
        var pairing = await sut.PairOnceAsync(
            new PairingCommand("ABC123", Domain.AgeBand.Child, "operation-a", "device", "windows", "1.0"),
            CancellationToken.None);
        var definitive = await sut.RefreshDefinitiveSessionAsync(prePair.RefreshToken!, CancellationToken.None);

        Assert.True(prePair.IsSuccess);
        Assert.Equal("device-a", pairing.DeviceId);
        Assert.Equal("device-a", definitive.Claim?.DeviceId);
        Assert.Equal("Bearer pre-access", handler.Requests[1].Authorization);
        Assert.DoesNotContain("device_id", handler.Requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("operation-a", handler.Requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PairWithoutPrePairSession_FailsClosedWithoutNetworkCall()
    {
        var handler = new QueueHandler();
        var sut = new BackendIdentityLifecycleClient(
            new HttpClient(handler), "https://example.supabase.co", "publishable-key");

        var result = await sut.PairOnceAsync(
            new PairingCommand("ABC123", Domain.AgeBand.Child, "operation-b", "device", "windows", "1.0"),
            CancellationToken.None);

        Assert.Equal(BackendIdentityErrorV1.Forbidden, result.Error);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, BackendIdentityErrorV1.Revoked)]
    [InlineData(HttpStatusCode.Forbidden, BackendIdentityErrorV1.Forbidden)]
    public async Task RefreshRejected_FailsClosedWithTypedError(HttpStatusCode status, BackendIdentityErrorV1 expected)
    {
        var handler = new QueueHandler(Json(status, "{}"));
        var sut = new BackendIdentityLifecycleClient(
            new HttpClient(handler), "https://example.supabase.co", "publishable-key");

        var result = await sut.RefreshDefinitiveSessionAsync("refresh-token", CancellationToken.None);

        Assert.Equal(expected, result.Error);
        Assert.Null(result.AccessToken);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string Jwt(string deviceId)
    {
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\"}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"device_id\":\"{deviceId}\"}}"))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{header}.{payload}.signature";
    }

    private sealed class QueueHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses;

        public QueueHandler(params HttpResponseMessage[] responses)
        {
            this.responses = new Queue<HttpResponseMessage>(responses);
        }

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Requests.Add(new CapturedRequest(
                request.Headers.Authorization?.ToString(),
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return this.responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(string? Authorization, string Body);
}
