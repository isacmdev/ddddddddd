// <copyright file="BackendIdentityContractV1Tests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Text;
using ControlParental.Service;
using Xunit;

public sealed class BackendIdentityContractV1Tests
{
    [Theory]
    [InlineData("device-a", "device-a", BackendIdentityErrorV1.None)]
    [InlineData("device-a", "device-b", BackendIdentityErrorV1.ClaimMismatch)]
    public void ValidateDeviceClaim_RequiresExactBackendIdentity(
        string expectedDeviceId,
        string claimedDeviceId,
        BackendIdentityErrorV1 expected)
    {
        var claim = new BackendIdentityClaimV1(claimedDeviceId);

        var result = BackendIdentityContractV1.ValidateDeviceClaim(expectedDeviceId, claim);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_AcceptedV1Response_ReturnsTypedContractAndDeterministicReceipt()
    {
        var payload = Encoding.UTF8.GetBytes(
            """{"schema":"backend-identity","version":1,"operation_id":"op-a","status":"accepted","device_id":"device-a","server_time":"2026-08-11T12:00:00Z"}""");

        var first = BackendIdentityContractV1.Parse(payload);
        var second = BackendIdentityContractV1.Parse(payload);

        Assert.True(first.IsSuccess);
        Assert.Equal("device-a", first.DeviceId);
        Assert.Equal(first, second);
        Assert.False(first.Receipt!.ExternalVerified);
        Assert.Equal("v1:op-a:accepted:2026-08-11T12:00:00.0000000+00:00", first.Receipt.Id);
    }

    [Theory]
    [InlineData("{\"schema\":\"backend-identity-v2\",\"version\":1,\"operation_id\":\"op-a\",\"status\":\"accepted\",\"device_id\":\"device-a\",\"server_time\":\"2026-08-11T12:00:00Z\"}", BackendIdentityErrorV1.UnsupportedSchema)]
    [InlineData("{\"schema\":\"backend-identity\",\"version\":2,\"operation_id\":\"op-a\",\"status\":\"accepted\",\"device_id\":\"device-a\",\"server_time\":\"2026-08-11T12:00:00Z\"}", BackendIdentityErrorV1.UnsupportedVersion)]
    [InlineData("{\"schema\":", BackendIdentityErrorV1.InvalidPayload)]
    [InlineData("{\"schema\":\"backend-identity\",\"version\":1,\"operation_id\":\"op-a\",\"status\":\"accepted\",\"server_time\":\"2026-08-11T12:00:00Z\"}", BackendIdentityErrorV1.InvalidPayload)]
    [InlineData("[]", BackendIdentityErrorV1.InvalidPayload)]
    [InlineData("{\"schema\":\"backend-identity\"}", BackendIdentityErrorV1.InvalidPayload)]
    [InlineData("{\"schema\":\"backend-identity\",\"version\":\"1\"}", BackendIdentityErrorV1.InvalidPayload)]
    [InlineData("{\"schema\":\"backend-identity\",\"version\":1,\"operation_id\":\"op-a\",\"status\":\"unknown\",\"server_time\":\"2026-08-11T12:00:00Z\"}", BackendIdentityErrorV1.InvalidPayload)]
    [InlineData("{\"schema\":\"backend-identity\",\"version\":1,\"operation_id\":\"op-a\",\"status\":\"rate_limited\",\"server_time\":\"2026-08-11T12:00:00Z\"}", BackendIdentityErrorV1.InvalidPayload)]
    public void Parse_UnknownOrInvalidPayload_FailsClosed(string json, BackendIdentityErrorV1 expected)
    {
        var result = BackendIdentityContractV1.Parse(Encoding.UTF8.GetBytes(json));

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error);
        Assert.Null(result.DeviceId);
    }

    [Fact]
    public void Parse_PayloadAboveBound_FailsBeforeDeserialization()
    {
        var payload = new byte[BackendIdentityContractV1.MaximumPayloadBytes + 1];

        var result = BackendIdentityContractV1.Parse(payload);

        Assert.Equal(BackendIdentityErrorV1.CapacityExceeded, result.Error);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Parse_RateLimitEnvelope_PreservesTypedRetryAfter()
    {
        var payload = Encoding.UTF8.GetBytes(
            """{"schema":"backend-identity","version":1,"operation_id":"op-rate","status":"rate_limited","server_time":"2026-08-11T12:00:00Z","retry_after_seconds":30}""");

        var result = BackendIdentityContractV1.Parse(payload);

        Assert.Equal(BackendIdentityErrorV1.RateLimited, result.Error);
        Assert.Equal(TimeSpan.FromSeconds(30), result.RetryAfter);
        Assert.Equal("op-rate", result.Receipt!.OperationId);
    }

    [Theory]
    [InlineData(404, null, BackendIdentityErrorV1.NotFound)]
    [InlineData(410, null, BackendIdentityErrorV1.Gone)]
    [InlineData(429, 30, BackendIdentityErrorV1.RateLimited)]
    public void FromHttpFailure_StableStatusesAreTyped(int status, int? retrySeconds, BackendIdentityErrorV1 expected)
    {
        var result = BackendIdentityContractV1.FromHttpFailure(status, retrySeconds is null ? null : TimeSpan.FromSeconds(retrySeconds.Value));

        Assert.Equal(expected, result.Error);
        Assert.Equal(retrySeconds, (int?)result.RetryAfter?.TotalSeconds);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Harness_TimeoutAndCancellation_AreTerminalAndDeterministic()
    {
        var harness = new BackendIdentityContractV1Harness();

        var timeout = harness.Pair(PairingCase.ValidDeviceA, "timeout-op", timesOut: true);
        var cancelled = harness.Pair(PairingCase.ValidDeviceA, "cancel-op", cancelled: true);

        Assert.Equal(BackendIdentityErrorV1.Timeout, timeout.Error);
        Assert.Equal(BackendIdentityErrorV1.Cancelled, cancelled.Error);
        Assert.Equal(new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero), timeout.ObservedAt);
    }

    [Fact]
    public void Harness_StablePairingFailures_PreserveRetryAfterOnlyForRateLimit()
    {
        var harness = new BackendIdentityContractV1Harness();

        var missing = harness.Pair(PairingCase.Missing, "missing-op");
        var expired = harness.Pair(PairingCase.Expired, "expired-op");
        var throttled = harness.Pair(PairingCase.Throttled, "throttled-op");

        Assert.Equal(BackendIdentityErrorV1.NotFound, missing.Error);
        Assert.Equal(BackendIdentityErrorV1.Gone, expired.Error);
        Assert.Null(missing.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(30), throttled.RetryAfter);
    }

    [Fact]
    public void Harness_DuplicateOperationIsIdempotent_ButNewOperationIsReplay()
    {
        var harness = new BackendIdentityContractV1Harness();

        var accepted = harness.Pair(PairingCase.ValidDeviceA, "op-1");
        var duplicate = harness.Pair(PairingCase.ValidDeviceA, "op-1");
        var replay = harness.Pair(PairingCase.ValidDeviceA, "op-2");

        Assert.Equal(BackendIdentityErrorV1.None, accepted.Error);
        Assert.Same(accepted, duplicate);
        Assert.Equal(BackendIdentityErrorV1.Replay, replay.Error);
    }

    [Fact]
    public void TwoDeviceProbe_IsolatesRowsAndRejectsRevokedDevice()
    {
        var harness = new BackendIdentityContractV1Harness();

        var ownRows = harness.ProbeRows("device-a", "device-a");
        var otherRows = harness.ProbeRows("device-a", "device-b");
        harness.Revoke("device-a");
        var revoked = harness.ProbeRows("device-a", "device-a");

        Assert.Equal(BackendIdentityErrorV1.None, ownRows.Error);
        Assert.Equal(BackendIdentityErrorV1.Forbidden, otherRows.Error);
        Assert.Equal(BackendIdentityErrorV1.Revoked, revoked.Error);
        Assert.All(new[] { ownRows, otherRows, revoked }, receipt => Assert.False(receipt.ExternalVerified));
    }

    [Fact]
    public void TwoDeviceProbe_UnknownOrUnboundedFixture_FailsClosed()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var unknown = BackendIdentityContractV1.ProbeTwoDeviceAccess("device-c", "device-a", new HashSet<string>(), now);
        var unbounded = BackendIdentityContractV1.ProbeTwoDeviceAccess(
            "device-a",
            "device-a",
            new HashSet<string> { "device-a", "device-b", "device-c" },
            now);

        Assert.Equal(BackendIdentityErrorV1.Forbidden, unknown.Error);
        Assert.Equal(BackendIdentityErrorV1.CapacityExceeded, unbounded.Error);
    }
}
