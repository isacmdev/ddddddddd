// <copyright file="BackendClientSingleRequestTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using System.Text.Json;
using ControlParental.Domain;
using Moq;
using Moq.Protected;
using Xunit;

/// <summary>
/// T14/B3: the centralized HTTP boundary must issue exactly ONE request per
/// high-level call. These tests count invocations on the underlying handler
/// and fail loudly if a code path regresses to a "classify + body" double
/// request, which was the previous bug.
/// </summary>
public class BackendClientSingleRequestTests
{
    private readonly Mock<IDeviceAuthenticator> _deviceAuthenticatorMock = new();

    public BackendClientSingleRequestTests()
    {
        this._deviceAuthenticatorMock
            .Setup(d => d.CurrentAccessToken)
            .Returns("test-jwt-token-12345");
    }

    [Fact]
    public async Task FetchPolicyAsync_IssuesExactlyOneHttpRequest()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var requestCount = 0;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => requestCount++)
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"version\":5,\"policy_json\":\"{}\"}"),
                };
                return response;
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var result = await sut.FetchPolicyAsync("device-123", 4, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task FetchPolicyAsync_OnNonSuccess_DoesNotRetryWithASecondRequest()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var requestCount = 0;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => requestCount++)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var result = await sut.FetchPolicyAsync("device-123", 0, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task SendHeartbeatAsync_IssuesExactlyOneHttpRequest()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var requestCount = 0;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => requestCount++)
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"server_time_offset_ms\":0,\"new_policy_available\":false}"),
                };
                return response;
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var result = await sut.SendHeartbeatAsync(
            new HeartbeatData { Enforcement = EnforcementLevel.Standard, ClockOffsetMs = 0 },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task SendHeartbeatAsync_OnNonSuccess_DoesNotRetryWithASecondRequest()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var requestCount = 0;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => requestCount++)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var result = await sut.SendHeartbeatAsync(
            new HeartbeatData { Enforcement = EnforcementLevel.Standard },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(1, requestCount);
    }

    [Fact]
    public async Task ReportIntegrityAsync_IssuesExactlyOneHttpRequest()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var requestCount = 0;
        string? capturedBody = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                requestCount++;
                if (req.Content != null)
                {
                    capturedBody = req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            })
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"verdict\":\"trust\"}"),
                };
                return response;
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var result = await sut.ReportIntegrityAsync(
            new IntegrityReport
            {
                ReportHash = "HASH",
                BinaryHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                SignatureValid = true,
                Timestamp = DateTimeOffset.UtcNow,
                AgentVersion = "1.0.0",
                Platform = "windows",
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Null(result.Verdict);
        Assert.Equal(1, requestCount);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"evidence_id\"", capturedBody!);
        Assert.Contains("\"binary_sha256\":\"0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"", capturedBody!);
        Assert.Contains("\"signature_result\":\"valid\"", capturedBody!);

        using var doc = JsonDocument.Parse(capturedBody!);
        var payload = doc.RootElement.GetProperty("payload");
        Assert.Equal("1.0.0", payload.GetProperty("agent_version").GetString());
        Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", payload.GetProperty("binary_sha256").GetString());
        Assert.Equal("valid", payload.GetProperty("signature_result").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("collected_at").GetString()));
    }

    [Fact]
    public async Task ReportIntegrityAsync_OnNonSuccess_DoesNotRetryWithASecondRequest()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var requestCount = 0;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback(() => requestCount++)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest));

        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var result = await sut.ReportIntegrityAsync(
            new IntegrityReport
            {
                ReportHash = "HASH",
                Timestamp = DateTimeOffset.UtcNow,
                AgentVersion = "1.0.0",
                Platform = "windows",
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task ReportIntegrityAsync_BareVerdictIsRejectedWithoutTrustDecision()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"verdict\":\"trust\"}"),
            });

        var sut = new BackendClient(
            new HttpClient(handlerMock.Object),
            "https://example.supabase.co",
            this._deviceAuthenticatorMock.Object);

        var result = await sut.ReportIntegrityAsync(new IntegrityReport
        {
            ReportHash = "HASH",
            BinaryHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SignatureValid = true,
            Timestamp = DateTimeOffset.UtcNow,
            AgentVersion = "1.0.0",
            Platform = "windows",
        });

        Assert.False(result.Success);
        Assert.Null(result.Verdict);
        Assert.True(result.IsInvalidEnvelope);
    }

    [Fact]
    public async Task ReportIntegrityAsync_EmptySuccessfulResponseIsNonDefinitiveNotTransportFailure()
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(string.Empty) });

        var sut = new BackendClient(
            new HttpClient(handlerMock.Object),
            "https://example.supabase.co",
            this._deviceAuthenticatorMock.Object);

        var result = await sut.ReportIntegrityAsync(new IntegrityReport
        {
            ReportHash = "HASH",
            BinaryHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            SignatureValid = true,
            Timestamp = DateTimeOffset.UtcNow,
            AgentVersion = "1.0.0",
            Platform = "windows",
        });

        Assert.False(result.Success);
        Assert.Null(result.Verdict);
        Assert.True(result.IsInvalidEnvelope);
    }
}
