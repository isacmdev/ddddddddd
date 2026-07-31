// <copyright file="BackendClientTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using System.Text.Json;
using ControlParental.Domain;
using Moq;
using Moq.Protected;
using Xunit;

public class BackendClientTests
{
    // T17: Shared mock for IDeviceAuthenticator with a known test token
    private readonly Mock<IDeviceAuthenticator> _deviceAuthenticatorMock = new Mock<IDeviceAuthenticator>();

    public BackendClientTests()
    {
        // Configure the mock to return a known test token
        this._deviceAuthenticatorMock.Setup(d => d.CurrentAccessToken).Returns("test-jwt-token-12345");
    }

    [Fact]
    public async Task FetchPolicyAsync_WhenPolicyExists_ReturnsPolicy()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        // The response should have policyJson as a string
        var policyJson = "{\"version\":5,\"rules\":[]}";
        var responseJson = JsonSerializer.Serialize(new { version = 5, policyJson = policyJson });

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(responseJson);
                return response;
            });

        // Act
        var result = await sut.FetchPolicyAsync("device-123", 4, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Expected success but got: {result.ErrorMessage}");
        Assert.Equal(5, result.Version);
        Assert.Equal(policyJson, result.PolicyJson);
    }

    [Fact]
    public async Task FetchPolicyAsync_WhenNoNewVersion_ReturnsCurrentVersion()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var responseJson = JsonSerializer.Serialize(new { version = 4, policyJson = "{}" });

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(responseJson);
                return response;
            });

        // Act
        var result = await sut.FetchPolicyAsync("device-123", 4, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(4, result.Version);
        Assert.Empty(result.PolicyJson ?? string.Empty);
    }

    [Fact]
    public async Task FetchPolicyAsync_WhenNetworkError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unavailable"));

        // Act
        var result = await sut.FetchPolicyAsync("device-123", 0, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Network error", result.ErrorMessage);
    }

    [Fact]
    public async Task FetchPolicyAsync_WhenServerError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                response.Content = new StringContent("{\"message\":\"Server error\"}");
                return response;
            });

        // Act
        var result = await sut.FetchPolicyAsync("device-123", 0, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task PushUsageLogsAsync_WhenEmpty_ReturnsZero()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        // Act
        var result = await sut.PushUsageLogsAsync(
            Array.Empty<UsageLogEntry>(),
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ItemsSent);
    }

    [Fact]
    public async Task PushUsageLogsAsync_WhenSuccess_ReturnsItemsSent()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var logs = new[]
        {
            new UsageLogEntry
            {
                AppId = "app1",
                Minutes = 30,
                ServerDate = DateTimeOffset.UtcNow,
                DedupKey = "key1",
            },
        };

        // Act
        var result = await sut.PushUsageLogsAsync(logs, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsSent);
    }

    [Fact]
    public async Task PushUsageLogsAsync_WhenNetworkError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unavailable"));

        var logs = new[]
        {
            new UsageLogEntry
            {
                AppId = "app1",
                Minutes = 30,
                ServerDate = DateTimeOffset.UtcNow,
                DedupKey = "key1",
            },
        };

        // Act
        var result = await sut.PushUsageLogsAsync(logs, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Network error", result.ErrorMessage);
    }

    [Fact]
    public async Task PushDeviceAlertsAsync_WhenSuccess_ReturnsItemsSent()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var alerts = new[]
        {
            new DeviceAlertEntry
            {
                EventType = "clock_tamper_suspected",
                Description = "Clock jump detected",
                Severity = "Warning",
                DetectedAt = DateTimeOffset.UtcNow,
                DedupKey = "alert1",
            },
        };

        // Act
        var result = await sut.PushDeviceAlertsAsync(alerts, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsSent);
    }

    [Fact]
    public async Task PushBehavioralEventsAsync_WhenSuccess_ReturnsItemsSent()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var events = new[]
        {
            new BehavioralEventEntry
            {
                EventType = "app_launched",
                AppId = "app1",
                Timestamp = DateTimeOffset.UtcNow,
                DedupKey = "event1",
            },
        };

        // Act
        var result = await sut.PushBehavioralEventsAsync(events, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ItemsSent);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenSuccess_ReturnsOffset()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var responseJson = JsonSerializer.Serialize(new { serverTimeOffsetMs = 150L, newPolicyAvailable = true });

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(responseJson);
                return response;
            });

        var heartbeat = new HeartbeatData
        {
            Enforcement = EnforcementLevel.Standard,
            BatteryPct = 80,
            ClockOffsetMs = 100,
            AgentUptimeMs = 3600000,
        };

        // Act
        var result = await sut.SendHeartbeatAsync(heartbeat, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(150, result.ServerTimeOffsetMs);
        Assert.True(result.NewPolicyAvailable);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenNetworkError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unavailable"));

        var heartbeat = new HeartbeatData
        {
            Enforcement = EnforcementLevel.Standard,
            ClockOffsetMs = 100,
        };

        // Act
        var result = await sut.SendHeartbeatAsync(heartbeat, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Network error", result.ErrorMessage);
    }

    [Fact]
    public async Task RegisterPushTokenAsync_WhenSuccess_ReturnsExpiresAt()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        // Act
        var result = await sut.RegisterPushTokenAsync(
            "https://db3p.notify.windows.com/?token=xxx",
            "wns",
            expiresAt,
            CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task RegisterPushTokenAsync_WhenServerError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.BadRequest);
                response.Content = new StringContent("{\"message\":\"Invalid token\"}");
                return response;
            });

        // Act
        var result = await sut.RegisterPushTokenAsync(
            "invalid-token",
            "wns",
            null,
            CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CreateTimeRequestAsync_WhenSuccess_ReturnsTrue()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var request = new TimeRequestEntry
        {
            RequestId = "req-123",
            Minutes = 30,
            Reason = "Homework",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Act
        var result = await sut.CreateTimeRequestAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task CreateTimeRequestAsync_WhenServerError_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var request = new TimeRequestEntry
        {
            RequestId = "req-123",
            Minutes = 30,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Act
        var result = await sut.CreateTimeRequestAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReportIntegrityAsync_WhenSuccess_ReturnsTrue()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var report = new IntegrityReport
        {
            ReportHash = "abc123",
            Timestamp = DateTimeOffset.UtcNow,
            AgentVersion = "1.0.0",
            Platform = "windows",
        };

        // Act
        var result = await sut.ReportIntegrityAsync(report, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ReportIntegrityAsync_WhenNetworkError_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unavailable"));

        var report = new IntegrityReport
        {
            ReportHash = "abc123",
            Timestamp = DateTimeOffset.UtcNow,
            AgentVersion = "1.0.0",
            Platform = "windows",
        };

        // Act
        var result = await sut.ReportIntegrityAsync(report, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
    }

    // ── T18: Idempotency header verification ─────────────────────────────────

    [Fact]
    public async Task PushUsageLogsAsync_SendsIdempotencyHeader()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var logs = new[]
        {
            new UsageLogEntry
            {
                AppId = "app1",
                Minutes = 30,
                ServerDate = DateTimeOffset.UtcNow,
                DedupKey = "key1",
            },
        };

        // Act
        await sut.PushUsageLogsAsync(logs, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("Prefer"));
        Assert.Equal("resolution=merge-duplicates", capturedRequest.Headers.GetValues("Prefer").First());
    }

    [Fact]
    public async Task PushDeviceAlertsAsync_SendsIdempotencyHeader()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var alerts = new[]
        {
            new DeviceAlertEntry
            {
                EventType = "clock_tamper_suspected",
                Description = "Clock jump detected",
                Severity = "Warning",
                DetectedAt = DateTimeOffset.UtcNow,
                DedupKey = "alert1",
            },
        };

        // Act
        await sut.PushDeviceAlertsAsync(alerts, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("Prefer"));
        Assert.Equal("resolution=merge-duplicates", capturedRequest.Headers.GetValues("Prefer").First());
    }

    [Fact]
    public async Task PushBehavioralEventsAsync_SendsIdempotencyHeader()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var events = new[]
        {
            new BehavioralEventEntry
            {
                EventType = "app_launched",
                AppId = "app1",
                Timestamp = DateTimeOffset.UtcNow,
                DedupKey = "event1",
            },
        };

        // Act
        await sut.PushBehavioralEventsAsync(events, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("Prefer"));
        Assert.Equal("resolution=merge-duplicates", capturedRequest.Headers.GetValues("Prefer").First());
    }

    [Fact]
    public async Task RegisterPushTokenAsync_SendsIdempotencyHeader()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        // Act
        await sut.RegisterPushTokenAsync(
            "https://db3p.notify.windows.com/?token=xxx",
            "wns",
            DateTimeOffset.UtcNow.AddDays(30),
            CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Contains("Prefer"));
        Assert.Equal("resolution=merge-duplicates", capturedRequest.Headers.GetValues("Prefer").First());
    }

    // ── T18: Malformed JSON response handling ─────────────────────────────────

    [Fact]
    public async Task FetchPolicyAsync_WhenMalformedJson_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("not valid json {{{");
                return response;
            });

        // Act
        var result = await sut.FetchPolicyAsync("device-123", 0, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenMalformedJson_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("not valid json {{{");
                return response;
            });

        var heartbeat = new HeartbeatData
        {
            Enforcement = EnforcementLevel.Standard,
            ClockOffsetMs = 100,
        };

        // Act
        var result = await sut.SendHeartbeatAsync(heartbeat, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task FetchPolicyAsync_WhenNullResponseBody_ReturnsCurrentVersion()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("null");
                return response;
            });

        // Act
        var result = await sut.FetchPolicyAsync("device-123", 5, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(5, result.Version);
        Assert.Empty(result.PolicyJson ?? string.Empty);
    }

    // ── T18: TaskCanceledException handling ──────────────────────────────────

    [Fact]
    public async Task SendHeartbeatAsync_WhenTimeout_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var heartbeat = new HeartbeatData
        {
            Enforcement = EnforcementLevel.Standard,
            ClockOffsetMs = 100,
        };

        // Act
        var result = await sut.SendHeartbeatAsync(heartbeat, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task PushUsageLogsAsync_WhenTimeout_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var logs = new[]
        {
            new UsageLogEntry
            {
                AppId = "app1",
                Minutes = 30,
                ServerDate = DateTimeOffset.UtcNow,
                DedupKey = "key1",
            },
        };

        // Act
        var result = await sut.PushUsageLogsAsync(logs, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CreateTimeRequestAsync_WhenTimeout_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var request = new TimeRequestEntry
        {
            RequestId = "req-123",
            Minutes = 30,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        // Act
        var result = await sut.CreateTimeRequestAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ReportIntegrityAsync_WhenTimeout_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var report = new IntegrityReport
        {
            ReportHash = "abc123",
            Timestamp = DateTimeOffset.UtcNow,
            AgentVersion = "1.0.0",
            Platform = "windows",
        };

        // Act
        var result = await sut.ReportIntegrityAsync(report, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
    }

    // ── T18: 500 error handling for all methods ───────────────────────────────

    [Fact]
    public async Task PushUsageLogsAsync_WhenServerError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var logs = new[]
        {
            new UsageLogEntry
            {
                AppId = "app1",
                Minutes = 30,
                ServerDate = DateTimeOffset.UtcNow,
                DedupKey = "key1",
            },
        };

        // Act
        var result = await sut.PushUsageLogsAsync(logs, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task PushDeviceAlertsAsync_WhenServerError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var alerts = new[]
        {
            new DeviceAlertEntry
            {
                EventType = "clock_tamper_suspected",
                Description = "Clock jump detected",
                Severity = "Warning",
                DetectedAt = DateTimeOffset.UtcNow,
                DedupKey = "alert1",
            },
        };

        // Act
        var result = await sut.PushDeviceAlertsAsync(alerts, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task PushBehavioralEventsAsync_WhenServerError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var events = new[]
        {
            new BehavioralEventEntry
            {
                EventType = "app_launched",
                AppId = "app1",
                Timestamp = DateTimeOffset.UtcNow,
                DedupKey = "event1",
            },
        };

        // Act
        var result = await sut.PushBehavioralEventsAsync(events, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task SendHeartbeatAsync_WhenServerError_ReturnsFailed()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object);
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var heartbeat = new HeartbeatData
        {
            Enforcement = EnforcementLevel.Standard,
            ClockOffsetMs = 100,
        };

        // Act
        var result = await sut.SendHeartbeatAsync(heartbeat, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    // ── T18: Correct RPC endpoint verification ───────────────────────────────

    [Fact]
    public async Task FetchPolicyAsync_CallsCorrectRpcEndpoint()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("{\"version\":1,\"policyJson\":\"{}\"}");
                return response;
            });

        // Act
        await sut.FetchPolicyAsync("device-abc", 0, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Contains("/rest/v1/rpc/get_device_policy", capturedRequest.RequestUri?.PathAndQuery);
    }

    [Fact]
    public async Task SendHeartbeatAsync_CallsCorrectRpcEndpoint()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("{}");
                return response;
            });

        var heartbeat = new HeartbeatData
        {
            Enforcement = EnforcementLevel.Standard,
            ClockOffsetMs = 100,
        };

        // Act
        await sut.SendHeartbeatAsync(heartbeat, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Contains("/rest/v1/rpc/heartbeat", capturedRequest.RequestUri?.PathAndQuery);
    }

    // =====================================================================
    // T17: Authorization header tests — validate BackendClient sends Bearer token
    // =====================================================================

    [Fact]
    public async Task FetchPolicyAsync_SendsAuthorizationBearerHeader()
    {
        // Arrange
        var expectedToken = "test-jwt-token-12345";
        var deviceAuthMock = new Mock<IDeviceAuthenticator>();
        deviceAuthMock.Setup(d => d.CurrentAccessToken).Returns(expectedToken);

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", deviceAuthMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent("{\"version\":5,\"policyJson\":\"{}\"}");
                return response;
            });

        // Act
        await sut.FetchPolicyAsync("device-123", 4, CancellationToken.None);

        // Assert — T17: Authorization header must be present with Bearer scheme
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Authorization != null, "Authorization header should be set");
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal(expectedToken, capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task PushUsageLogsAsync_SendsAuthorizationBearerHeader()
    {
        // Arrange
        var expectedToken = "another-test-token-67890";
        var deviceAuthMock = new Mock<IDeviceAuthenticator>();
        deviceAuthMock.Setup(d => d.CurrentAccessToken).Returns(expectedToken);

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", deviceAuthMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Created));

        var logs = new[] { new UsageLogEntry { AppId = "app.exe", Minutes = 30, ServerDate = DateTime.Today, DedupKey = "key-1" } };

        // Act
        await sut.PushUsageLogsAsync(logs, CancellationToken.None);

        // Assert — T17: PushUsageLogsAsync must include Authorization header
        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.Authorization != null, "Authorization header should be set");
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization.Scheme);
        Assert.Equal(expectedToken, capturedRequest.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task CreateAuthenticatedRequest_WhenTokenIsNullOrEmpty_DoesNotSetAuthorizationHeader()
    {
        // Arrange
        var deviceAuthMock = new Mock<IDeviceAuthenticator>();
        deviceAuthMock.Setup(d => d.CurrentAccessToken).Returns((string?)null);

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", deviceAuthMock.Object);

        HttpRequestMessage? capturedRequest = null;
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"version\":5,\"policyJson\":\"{}\"}"),
            });

        // Act
        await sut.FetchPolicyAsync("device-123", 4, CancellationToken.None);

        // Assert — when no token, header must NOT be set
        Assert.NotNull(capturedRequest);
        Assert.True(
            capturedRequest.Headers.Authorization == null,
            "Authorization header should NOT be set when CurrentAccessToken is null");
    }

    // =====================================================================
    // T24: PairAsync tests — JSON parsing and status code mapping
    // =====================================================================

    [Fact]
    public async Task PairAsync_WhenSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        var responseJson = JsonSerializer.Serialize(new
        {
            success = true,
            device_id = "device-uuid-123",
            parent_id = "parent-uuid-456",
            policy_version = 2,
        });

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new StringContent(responseJson);
                return response;
            });

        var request = new PairingRequest(
            Code: "ABC123",
            DeviceName: "TEST-PC",
            DeviceModel: "Dell XPS",
            OsVersion: "Windows 11",
            AppVersion: "1.0.0",
            AgeBand: "7-12");

        // Act
        var result = await sut.PairAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(PairingHttpStatus.Success, result.Status);
        Assert.Equal("device-uuid-123", result.DeviceId);
        Assert.Equal("parent-uuid-456", result.ParentId);
        Assert.Equal(2, result.PolicyVersion);
    }

    [Fact]
    public async Task PairAsync_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        var request = new PairingRequest(
            Code: "INVALID",
            DeviceName: "TEST-PC",
            DeviceModel: "Dell XPS",
            OsVersion: "Windows 11",
            AppVersion: "1.0.0",
            AgeBand: "7-12");

        // Act
        var result = await sut.PairAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingHttpStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task PairAsync_WhenGone_ReturnsGone()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.Gone));

        var request = new PairingRequest(
            Code: "EXPIRED",
            DeviceName: "TEST-PC",
            DeviceModel: "Dell XPS",
            OsVersion: "Windows 11",
            AppVersion: "1.0.0",
            AgeBand: "13-16");

        // Act
        var result = await sut.PairAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingHttpStatus.Gone, result.Status);
    }

    [Fact]
    public async Task PairAsync_WhenTooManyRequests_ReturnsTooManyRequests()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var request = new PairingRequest(
            Code: "ABC123",
            DeviceName: "TEST-PC",
            DeviceModel: "Dell XPS",
            OsVersion: "Windows 11",
            AppVersion: "1.0.0",
            AgeBand: "17-18");

        // Act
        var result = await sut.PairAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingHttpStatus.TooManyRequests, result.Status);
    }

    [Fact]
    public async Task PairAsync_WhenServerError_ReturnsServerError()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var request = new PairingRequest(
            Code: "ABC123",
            DeviceName: "TEST-PC",
            DeviceModel: "Dell XPS",
            OsVersion: "Windows 11",
            AppVersion: "1.0.0",
            AgeBand: "7-12");

        // Act
        var result = await sut.PairAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingHttpStatus.ServerError, result.Status);
    }

    [Fact]
    public async Task PairAsync_WhenNetworkError_ReturnsNetworkError()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unavailable"));

        var request = new PairingRequest(
            Code: "ABC123",
            DeviceName: "TEST-PC",
            DeviceModel: "Dell XPS",
            OsVersion: "Windows 11",
            AppVersion: "1.0.0",
            AgeBand: "7-12");

        // Act
        var result = await sut.PairAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingHttpStatus.NetworkError, result.Status);
    }

    [Fact]
    public async Task PairAsync_WhenTimeout_ReturnsNetworkError()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("https://example.supabase.co"),
        };
        var sut = new BackendClient(httpClient, "https://example.supabase.co", this._deviceAuthenticatorMock.Object);

        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var request = new PairingRequest(
            Code: "ABC123",
            DeviceName: "TEST-PC",
            DeviceModel: "Dell XPS",
            OsVersion: "Windows 11",
            AppVersion: "1.0.0",
            AgeBand: "7-12");

        // Act
        var result = await sut.PairAsync(request, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingHttpStatus.NetworkError, result.Status);
    }
}
