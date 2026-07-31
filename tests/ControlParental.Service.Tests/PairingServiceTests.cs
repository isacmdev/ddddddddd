// <copyright file="PairingServiceTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Moq;
using Xunit;

public class PairingServiceTests
{
    private readonly Mock<IDeviceAuthenticator> deviceAuthenticatorMock = new();
    private readonly Mock<IBackendClient> backendClientMock = new();
    private readonly Mock<ISecretStore> secretStoreMock = new();
    private readonly Mock<IPolicyRepository> policyRepositoryMock = new();
    private readonly Mock<ITimeProvider> timeProviderMock = new();

    public PairingServiceTests()
    {
        this.timeProviderMock.Setup(t => t.WallClockNow).Returns(DateTimeOffset.UtcNow);
    }

    private PairingService CreateSut()
        => new PairingService(
            this.deviceAuthenticatorMock.Object,
            this.backendClientMock.Object,
            this.secretStoreMock.Object,
            this.policyRepositoryMock.Object,
            this.timeProviderMock.Object);

    [Fact]
    public async Task PairAsync_ValidCode_ReturnsSuccess_AndPersistsDeviceId()
    {
        // Arrange
        var sut = this.CreateSut();
        this.deviceAuthenticatorMock
            .Setup(d => d.CreateAnonymousSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceAuthResult.Succeeded("token", "device-123"));

        this.backendClientMock
            .Setup(b => b.PairAsync(It.IsAny<PairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PairingHttpResult.SuccessResult("new-device-id", "parent-id", 1));

        this.secretStoreMock
            .Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SecretWriteResult.NewSecret());

        this.backendClientMock
            .Setup(b => b.FetchPolicyAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(1, "{}"));

        // Act
        var result = await sut.PairAsync("ABC123", AgeBand.Child, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(PairingStatus.Success, result.Status);
        Assert.Equal("new-device-id", result.DeviceId);
        Assert.Equal("parent-id", result.ParentId);
        Assert.Equal(1, result.PolicyVersion);

        // Verify SecretStore was called twice (device_id and parent_id)
        this.secretStoreMock.Verify(
            s => s.WriteAsync("device_id", "new-device-id", It.IsAny<CancellationToken>()),
            Times.Once);
        this.secretStoreMock.Verify(
            s => s.WriteAsync("parent_id", "parent-id", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PairAsync_InvalidCode_ReturnsInvalidCode()
    {
        // Arrange
        var sut = this.CreateSut();
        this.deviceAuthenticatorMock
            .Setup(d => d.CreateAnonymousSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceAuthResult.Succeeded("token", "device-123"));

        this.backendClientMock
            .Setup(b => b.PairAsync(It.IsAny<PairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PairingHttpResult.NotFound());

        // Act - use 6-char code; backend returns 404 NotFound
        var result = await sut.PairAsync("INVALI", AgeBand.Child, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingStatus.InvalidCode, result.Status);
        this.secretStoreMock.Verify(
            s => s.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PairAsync_ExpiredCode_ReturnsExpiredCode()
    {
        // Arrange
        var sut = this.CreateSut();
        this.deviceAuthenticatorMock
            .Setup(d => d.CreateAnonymousSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceAuthResult.Succeeded("token", "device-123"));

        this.backendClientMock
            .Setup(b => b.PairAsync(It.IsAny<PairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PairingHttpResult.Gone());

        // Act
        var result = await sut.PairAsync("ABC123", AgeBand.Preteen, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingStatus.ExpiredCode, result.Status);
        this.secretStoreMock.Verify(
            s => s.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PairAsync_NetworkError_Retries3Times_ThenReturnsError()
    {
        // Arrange
        var sut = this.CreateSut();
        this.deviceAuthenticatorMock
            .Setup(d => d.CreateAnonymousSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceAuthResult.Succeeded("token", "device-123"));

        var callCount = 0;
        this.backendClientMock
            .Setup(b => b.PairAsync(It.IsAny<PairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return PairingHttpResult.NetworkError("Network failure");
            });

        // Act
        var result = await sut.PairAsync("ABC123", AgeBand.Teen, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingStatus.Error, result.Status);
        Assert.Equal(3, callCount); // 3 retries
    }

    [Fact]
    public async Task PairAsync_ServerError_Retries3Times_ThenReturnsError()
    {
        // Arrange
        var sut = this.CreateSut();
        this.deviceAuthenticatorMock
            .Setup(d => d.CreateAnonymousSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceAuthResult.Succeeded("token", "device-123"));

        var callCount = 0;
        this.backendClientMock
            .Setup(b => b.PairAsync(It.IsAny<PairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return PairingHttpResult.ServerError("Internal error");
            });

        // Act
        var result = await sut.PairAsync("ABC123", AgeBand.Child, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingStatus.Error, result.Status);
        Assert.Equal(3, callCount); // 3 retries
    }

    [Fact]
    public async Task PairAsync_SecretStoreWriteFails_ReturnsError()
    {
        // Arrange
        var sut = this.CreateSut();
        this.deviceAuthenticatorMock
            .Setup(d => d.CreateAnonymousSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceAuthResult.Succeeded("token", "device-123"));

        this.backendClientMock
            .Setup(b => b.PairAsync(It.IsAny<PairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PairingHttpResult.SuccessResult("new-device-id", "parent-id", 1));

        this.secretStoreMock
            .Setup(s => s.WriteAsync("device_id", "new-device-id", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage error"));

        // Act
        var result = await sut.PairAsync("ABC123", AgeBand.Child, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingStatus.Error, result.Status);
    }

    [Fact]
    public async Task PairAsync_CodeNot6Chars_ReturnsError()
    {
        // Arrange
        var sut = this.CreateSut();

        // Act
        var result = await sut.PairAsync("ABC12", AgeBand.Child, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingStatus.Error, result.Status);
    }

    [Fact]
    public async Task PairAsync_SessionFails_ReturnsError()
    {
        // Arrange
        var sut = this.CreateSut();
        this.deviceAuthenticatorMock
            .Setup(d => d.CreateAnonymousSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceAuthResult.Failed("Session failed", DeviceAuthState.Unauthenticated));

        // Act
        var result = await sut.PairAsync("ABC123", AgeBand.Child, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(PairingStatus.Error, result.Status);
    }

    [Fact]
    public async Task PairAsync_PolicyVersionFetched_OnSuccess()
    {
        // Arrange
        var sut = this.CreateSut();
        this.deviceAuthenticatorMock
            .Setup(d => d.CreateAnonymousSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeviceAuthResult.Succeeded("token", "device-123"));

        this.backendClientMock
            .Setup(b => b.PairAsync(It.IsAny<PairingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PairingHttpResult.SuccessResult("new-device-id", "parent-id", 1));

        this.secretStoreMock
            .Setup(s => s.WriteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SecretWriteResult.NewSecret());

        this.backendClientMock
            .Setup(b => b.FetchPolicyAsync("new-device-id", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(1, "{\"rules\":[]}"));

        // Act
        var result = await sut.PairAsync("ABC123", AgeBand.Child, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        this.backendClientMock.Verify(
            b => b.FetchPolicyAsync("new-device-id", 0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void IsPaired_WhenDeviceIdExists_ReturnsTrue()
    {
        // Arrange
        var sut = this.CreateSut();
        this.secretStoreMock
            .Setup(s => s.ReadAsync("device_id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SecretReadResult.Succeeded("existing-device-id"));

        // Act
        var isPaired = sut.IsPaired;

        // Assert
        Assert.True(isPaired);
    }

    [Fact]
    public void IsPaired_WhenDeviceIdMissing_ReturnsFalse()
    {
        // Arrange
        var sut = this.CreateSut();
        this.secretStoreMock
            .Setup(s => s.ReadAsync("device_id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SecretReadResult.NotFoundResult());

        // Act
        var isPaired = sut.IsPaired;

        // Assert
        Assert.False(isPaired);
    }

    [Fact]
    public void GetCurrentDeviceId_ReturnsStoredValue()
    {
        // Arrange
        var sut = this.CreateSut();
        this.secretStoreMock
            .Setup(s => s.ReadAsync("device_id", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SecretReadResult.Succeeded("my-device-id"));

        // Act
        var deviceId = sut.GetCurrentDeviceId();

        // Assert
        Assert.Equal("my-device-id", deviceId);
    }
}
