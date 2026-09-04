// <copyright file="IntegrityEnvelopeZeroStateTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using ControlParental.Domain;
using ControlParental.Domain.WireContracts;
using ControlParental.Domain.WireContracts.Models;
using ControlParental.Service;
using Moq;
using Moq.Protected;
using Xunit;

public sealed class IntegrityEnvelopeZeroStateTests
{
    [Fact]
    public async Task ReportIntegrityAsync_InvalidLocalEvidence_IsInvalidEnvelopeAndDoesNotSend()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var client = new BackendClient(
            new HttpClient(handler.Object),
            "https://example.test",
            new Mock<IDeviceAuthenticator>().Object);

        var result = await client.ReportIntegrityAsync(new IntegrityReport
        {
            ReportHash = "report",
            BinaryHash = "not-a-sha256",
            SignatureValid = true,
            Timestamp = DateTimeOffset.UtcNow,
            AgentVersion = "test",
            Platform = "windows",
        });

        Assert.False(result.Success);
        Assert.Null(result.Verdict);
        Assert.True(result.IsInvalidEnvelope);
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData("bare")]
    [InlineData("malformed")]
    [InlineData("invalid-json")]
    [InlineData("binding")]
    [Trait("ProductiveSeam", "AntiTamperMonitor")]
    public async Task AntiTamperMonitor_InvalidIntegrityEnvelope_IsZeroState(string responseKind)
    {
        var observedAt = DateTimeOffset.UtcNow;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(CreateResponse(responseKind)),
            });

        var timeProvider = new Mock<ITimeProvider>(MockBehavior.Strict);
        timeProvider.SetupGet(value => value.WallClockNow).Returns(observedAt);
        timeProvider.SetupGet(value => value.MonotonicNow).Returns(1_000L);

        var privilegeInspector = new Mock<IPrivilegeInspector>(MockBehavior.Strict);
        privilegeInspector
            .Setup(value => value.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var integrityChecker = new Mock<IIntegrityChecker>(MockBehavior.Strict);
        integrityChecker
            .Setup(value => value.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IntegrityCheckResult(
                IsSignatureValid: false,
                BinaryHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                ExecutablePath: "agent.exe"));
        var enforcement = new Mock<IEnforcementLevelMonitor>(MockBehavior.Strict);
        var outbox = new Mock<IOutboxManager>(MockBehavior.Strict);
        var verdictHandler = new IntegrityVerdictHandler(clock: () => observedAt);
        verdictHandler.SetServiceStartTime(observedAt.AddMinutes(-10));
        var backendClient = new BackendClient(
            new HttpClient(handler.Object),
            "https://example.test",
            new Mock<IDeviceAuthenticator>().Object);

        using var monitor = new AntiTamperMonitor(
            timeProvider.Object,
            outbox.Object,
            privilegeInspector.Object,
            enforcement.Object,
            integrityChecker.Object,
            backendClient,
            verdictHandler,
            tickSource: _ => new ValueTask<bool>(false));

        var before = verdictHandler.Snapshot();
        await monitor.StartAsync();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await monitor.TriggerIntegrityCheckAsync();
        }

        Assert.Equal(before, verdictHandler.Snapshot());
        Assert.False(verdictHandler.IsCircuitOpen);
        Assert.Empty(monitor.DetectedEvents);
        enforcement.VerifyNoOtherCalls();
        outbox.VerifyNoOtherCalls();
        handler.Protected().Verify(
            "SendAsync",
            Times.Exactly(5),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
        await monitor.StopAsync();
    }

    private static string CreateResponse(string responseKind)
    {
        return responseKind switch
        {
            "bare" => "{\"verdict\":\"revoked\"}",
            "malformed" => "[]",
            "invalid-json" => "{\"verdict\":\"revoked\"",
            "binding" => CreateBindingMismatchResponse(),
            _ => throw new ArgumentOutOfRangeException(nameof(responseKind)),
        };
    }

    private static string CreateBindingMismatchResponse()
    {
        var correlationId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var envelope = WireContractCodec.EncodeEnvelope(
            new IntegrityVerdictWire
            {
                Verdict = "revoked",
                EvidenceId = correlationId,
                EvaluatedAt = "2032-01-02T03:04:05Z",
                VerdictVersion = 1,
                ReasonCode = "mismatch",
            },
            correlationId,
            WireContractCatalog.IntegrityVerdict);
        return System.Text.Encoding.UTF8.GetString(envelope);
    }
}
