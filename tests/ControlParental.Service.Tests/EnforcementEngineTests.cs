// <copyright file="EnforcementEngineTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Collections.Concurrent;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T11 — Tests for EnforcementEngine and ProcessTerminator.
/// </summary>
public class EnforcementEngineTests : IDisposable
{
    // ── Fixtures ──────────────────────────────────────────────────────

    private readonly Mock<IPolicyRepository> mockPolicyRepository;
    private readonly Mock<IUsageAccumulator> mockUsageAccumulator;
    private readonly Mock<IProcessTerminator> mockProcessTerminator;
    private readonly Mock<IWorkstationLockManager> mockWorkstationLockManager;
    private readonly Mock<ITimeProvider> mockTimeProvider;
    private readonly Mock<IIpcChannel> mockIpcChannel;
    private readonly ConcurrentQueue<IIpcMessage> sentMessages;
    private readonly EnforcementEngine engine;

    public EnforcementEngineTests()
    {
        this.mockPolicyRepository = new Mock<IPolicyRepository>();
        this.mockUsageAccumulator = new Mock<IUsageAccumulator>();
        this.mockProcessTerminator = new Mock<IProcessTerminator>();
        this.mockWorkstationLockManager = new Mock<IWorkstationLockManager>();
        this.mockTimeProvider = new Mock<ITimeProvider>();
        this.mockIpcChannel = new Mock<IIpcChannel>();
        this.sentMessages = new ConcurrentQueue<IIpcMessage>();

        this.mockIpcChannel.SetupGet(c => c.IsConnected).Returns(true);
        this.mockIpcChannel
            .Setup(c => c.SendAsync(It.IsAny<IIpcMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(DateTimeOffset.UtcNow);
        this.mockTimeProvider.SetupGet(t => t.CurrentZone).Returns(TimeZoneInfo.Local);
        this.mockTimeProvider.SetupGet(t => t.MonotonicNow).Returns(0);

        this.engine = new EnforcementEngine(
            policyRepository: this.mockPolicyRepository.Object,
            usageAccumulator: this.mockUsageAccumulator.Object,
            processTerminator: this.mockProcessTerminator.Object,
            workstationLockManager: this.mockWorkstationLockManager.Object,
            timeProvider: this.mockTimeProvider.Object,
            ipcChannel: this.mockIpcChannel.Object);
    }

    public void Dispose()
    {
        this.engine.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullPolicyRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new EnforcementEngine(
            policyRepository: null!,
            usageAccumulator: this.mockUsageAccumulator.Object,
            processTerminator: this.mockProcessTerminator.Object,
            workstationLockManager: this.mockWorkstationLockManager.Object,
            timeProvider: this.mockTimeProvider.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("policyRepository");
    }

    [Fact]
    public void Constructor_WithNullUsageAccumulator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new EnforcementEngine(
            policyRepository: this.mockPolicyRepository.Object,
            usageAccumulator: null!,
            processTerminator: this.mockProcessTerminator.Object,
            workstationLockManager: this.mockWorkstationLockManager.Object,
            timeProvider: this.mockTimeProvider.Object);

        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("usageAccumulator");
    }

    // ── EnforceForegroundChangeAsync Tests ───────────────────────────

    [Fact]
    public async Task EnforceForegroundChangeAsync_WhenNoPolicy_ReturnsAllowed()
    {
        // Arrange
        this.mockPolicyRepository
            .Setup(r => r.GetPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Policy?)null);

        // Act
        var result = await this.engine.EnforceForegroundChangeAsync("com.example.app", CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Blocked.Should().BeFalse();
        result.ReasonText.Should().Be("Sin política activa");
    }

    [Fact]
    public async Task EnforceForegroundChangeAsync_WhenDeviceLocked_BlocksAllApps()
    {
        // Arrange
        var policy = CreateTestPolicy(deviceState: DeviceState.Locked);
        this.mockPolicyRepository
            .Setup(r => r.GetPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var usage = new UsageSnapshot(
            AppMinutes: new Dictionary<string, int>(),
            CategoryMinutes: new Dictionary<string, int>(),
            GlobalMinutes: 0,
            ExemptAppIds: new HashSet<string>());

        this.mockUsageAccumulator
            .Setup(u => u.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);

        this.mockProcessTerminator
            .Setup(p => p.CanTerminate(It.IsAny<string>()))
            .Returns(true);

        this.mockWorkstationLockManager
            .Setup(w => w.LockNowAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await this.engine.EnforceForegroundChangeAsync("com.example.app", CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Blocked.Should().BeTrue();
        result.ReasonCode.Should().Be(2); // Device locked
        this.mockWorkstationLockManager.Verify(w => w.LockNowAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EnforceForegroundChangeAsync_WhenAppAllowed_DoesNotTerminate()
    {
        // Arrange
        var policy = CreateTestPolicy(deviceState: DeviceState.Active);
        this.mockPolicyRepository
            .Setup(r => r.GetPolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var usage = new UsageSnapshot(
            AppMinutes: new Dictionary<string, int>(),
            CategoryMinutes: new Dictionary<string, int>(),
            GlobalMinutes: 0,
            ExemptAppIds: new HashSet<string>());

        this.mockUsageAccumulator
            .Setup(u => u.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(usage);

        // Act
        var result = await this.engine.EnforceForegroundChangeAsync("explorer.exe", CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Blocked.Should().BeFalse();
        this.mockProcessTerminator.Verify(
            p => p.TerminateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── LockDeviceAsync Tests ─────────────────────────────────────────

    [Fact]
    public async Task LockDeviceAsync_SetsDeviceLockedAndSendsOverlay()
    {
        // Arrange
        this.mockWorkstationLockManager
            .Setup(w => w.LockNowAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.engine.LockDeviceAsync("Test reason", CancellationToken.None);

        // Assert
        var status = this.engine.GetStatus();
        status.IsDeviceLocked.Should().BeTrue();
        this.mockIpcChannel.Verify(
            c => c.SendAsync(It.Is<IIpcMessage>(m => m is ShowOverlay), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task LockDeviceAsync_CallsWorkstationLockManager()
    {
        // Arrange
        this.mockWorkstationLockManager
            .Setup(w => w.LockNowAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await this.engine.LockDeviceAsync("Test reason", CancellationToken.None);

        // Assert
        this.mockWorkstationLockManager.Verify(w => w.LockNowAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── UnlockDeviceAsync Tests ──────────────────────────────────────

    [Fact]
    public async Task UnlockDeviceAsync_ClearsDeviceLockedAndHidesOverlay()
    {
        // Arrange
        this.mockWorkstationLockManager
            .Setup(w => w.LockNowAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await this.engine.LockDeviceAsync("Test reason", CancellationToken.None);

        // Act
        await this.engine.UnlockDeviceAsync(CancellationToken.None);

        // Assert
        var status = this.engine.GetStatus();
        status.IsDeviceLocked.Should().BeFalse();
        status.IsOverlayActive.Should().BeFalse();
    }

    // ── GetStatus Tests ──────────────────────────────────────────────

    [Fact]
    public void GetStatus_ReturnsCorrectStatus()
    {
        // Act
        var status = this.engine.GetStatus();

        // Assert
        status.Should().NotBeNull();
        status.IsOverlayActive.Should().BeFalse();
        status.IsDeviceLocked.Should().BeFalse();
    }

    // ── Helper Methods ───────────────────────────────────────────────

    private static Policy CreateTestPolicy(DeviceState deviceState = DeviceState.Active)
    {
        return new Policy
        {
            DeviceId = "test-device-001",
            Version = 1,
            DeviceState = deviceState,
            DailyScreenTimeMinutes = 120,
            Schedules = Array.Empty<Schedule>(),
            CategoryLimits = Array.Empty<CategoryLimit>(),
            CategoryAssignments = new Dictionary<string, string>(),
            Grants = Array.Empty<Grant>()
        };
    }
}

/// <summary>
/// T11 — Tests for ProcessTerminator.
/// </summary>
public class ProcessTerminatorTests
{
    // ── CanTerminate Tests ───────────────────────────────────────────

    [Theory]
    [InlineData("explorer", false)]
    [InlineData("winlogon", false)]
    [InlineData("csrss", false)]
    [InlineData("services", false)]
    [InlineData("ControlParental.SessionAgent", false)]
    [InlineData("com.example.app", true)]
    [InlineData("notepad.exe", true)]
    public void CanTerminate_ReturnsExpectedResult(string appId, bool expected)
    {
        // Arrange
        var terminator = new ProcessTerminator();

        // Act
        var result = terminator.CanTerminate(appId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CanTerminate_WithExeSuffix_NormalizesCorrectly()
    {
        // Arrange
        var terminator = new ProcessTerminator();

        // Act
        var result = terminator.CanTerminate("notepad.exe");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanTerminate_BlocksSystemProcessWithoutExe()
    {
        // Arrange
        var terminator = new ProcessTerminator();

        // Act
        var result = terminator.CanTerminate("explorer");

        // Assert
        result.Should().BeFalse();
    }

    // ── GetProcessId Tests ───────────────────────────────────────────

    [Fact]
    public void GetProcessId_WithNonExistentProcess_ReturnsNull()
    {
        // Arrange
        var terminator = new ProcessTerminator();

        // Act
        var result = terminator.GetProcessId("NonExistentProcess123456789");

        // Assert
        result.Should().BeNull();
    }

    // ── TerminateAsync Tests ─────────────────────────────────────────

    [Fact]
    public async Task TerminateAsync_WithProtectedProcess_ReturnsFalse()
    {
        // Arrange
        var terminator = new ProcessTerminator();

        // Act
        var result = await terminator.TerminateAsync("explorer", "Blocked", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TerminateAsync_WithNonExistentProcess_ReturnsFalse()
    {
        // Arrange
        var terminator = new ProcessTerminator();

        // Act
        var result = await terminator.TerminateAsync("NonExistentProcess123456789", "Blocked", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}
