// <copyright file="BackendIdentityAdaptersTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Text;
using ControlParental.Domain;
using ControlParental.Service;
using Xunit;

public sealed class BackendIdentityAdaptersTests
{
    [Theory]
    [InlineData(BackendIdentityErrorV1.NotFound, PairingStatus.InvalidCode)]
    [InlineData(BackendIdentityErrorV1.Gone, PairingStatus.ExpiredCode)]
    [InlineData(BackendIdentityErrorV1.RateLimited, PairingStatus.TooManyRequests)]
    [InlineData(BackendIdentityErrorV1.Replay, PairingStatus.Error)]
    public async Task PairingService_MapsLifecycleOutcomeWithoutOwningCredentials(
        BackendIdentityErrorV1 error,
        PairingStatus expected)
    {
        var coordinator = new CoordinatorStub
        {
            Result = new BackendPairingLifecycleResult(error, RetryAfter: TimeSpan.FromSeconds(17)),
        };
        var sut = new PairingService(coordinator);

        var result = await sut.PairAsync("abc123", AgeBand.Child);

        Assert.Equal(expected, result.Status);
        Assert.Equal(error == BackendIdentityErrorV1.RateLimited ? TimeSpan.FromSeconds(17) : null, result.RetryAfter);
        Assert.Equal("ABC123", coordinator.Command?.Code);
    }

    [Fact]
    public async Task PairingService_ReportsPairedOnlyFromDefinitiveCoordinatorState()
    {
        var coordinator = new CoordinatorStub
        {
            State = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 7, "device-a"),
            Result = new BackendPairingLifecycleResult(BackendIdentityErrorV1.None, "device-a", "parent-a", 4),
        };
        var sut = new PairingService(coordinator);

        var result = await sut.PairAsync("ABC123", AgeBand.Preteen);

        Assert.True(result.Success);
        Assert.True(sut.IsPaired);
        Assert.Equal("device-a", sut.GetCurrentDeviceId());
        Assert.Equal(4, result.PolicyVersion);
    }

    [Fact]
    public void DeviceAuthenticator_ReadsOnlyExactDeviceIdClaim_NotSubjectFallback()
    {
        var exact = Jwt("{\"device_id\":\"device-a\",\"sub\":\"auth-user\"}");
        var subjectOnly = Jwt("{\"sub\":\"auth-user\"}");

        var accepted = DeviceAuthenticator.TryReadExactDeviceClaim(exact, out var deviceId);
        var rejected = DeviceAuthenticator.TryReadExactDeviceClaim(subjectOnly, out var fallback);

        Assert.True(accepted);
        Assert.Equal("device-a", deviceId);
        Assert.False(rejected);
        Assert.Null(fallback);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("e30.e30.signature")]
    public void DeviceAuthenticator_MissingOrMalformedDeviceClaim_FailsClosed(string token)
    {
        Assert.False(DeviceAuthenticator.TryReadExactDeviceClaim(token, out var deviceId));
        Assert.Null(deviceId);
    }

    private static string Jwt(string payload)
        => $"e30.{Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.signature";

    private sealed class CoordinatorStub : IBackendIdentityCoordinator
    {
        public BackendIdentityState State { get; set; } = BackendIdentityState.Unpaired();

        public BackendPairingLifecycleResult Result { get; set; } = new(BackendIdentityErrorV1.InvalidPayload);

        public PairingCommand? Command { get; private set; }

        public BackendIdentityState CurrentState => this.State;

        public Task<BackendPairingLifecycleResult> PairAsync(PairingCommand command, CancellationToken cancellationToken = default)
        {
            this.Command = command;
            return Task.FromResult(this.Result);
        }
    }
}
