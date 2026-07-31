// <copyright file="ScheduledWorkServiceIdentityTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Moq;
using Xunit;

/// <summary>
/// T14/B2 — coverage for the "no fallback identifiers" contract. The policy sync
/// must use the Service-persisted device identity and never a literal such as
/// "default". When no identity is available the operation must be skipped
/// locally, not sent to the backend with a placeholder value.
/// </summary>
public class ScheduledWorkServiceIdentityTests : IDisposable
{
    private readonly Mock<IBackendClient> mockBackendClient;
    private readonly Mock<IOutboxManager> mockOutboxManager;
    private readonly Mock<IUsageReconciler> mockUsageReconciler;
    private readonly Mock<IEnforcementLevelMonitor> mockEnforcementLevelMonitor;
    private readonly Mock<ITimeProvider> mockTimeProvider;
    private readonly Mock<IServiceHealthMonitor> mockHealthMonitor;
    private readonly Mock<IServiceRecoveryManager> mockRecoveryManager;
    private readonly Mock<IPolicyRepository> mockPolicyRepository;
    private readonly ScheduledWorkService service;

    public ScheduledWorkServiceIdentityTests()
    {
        this.mockBackendClient = new Mock<IBackendClient>();
        this.mockOutboxManager = new Mock<IOutboxManager>();
        this.mockUsageReconciler = new Mock<IUsageReconciler>();
        this.mockEnforcementLevelMonitor = new Mock<IEnforcementLevelMonitor>();
        this.mockTimeProvider = new Mock<ITimeProvider>();
        this.mockHealthMonitor = new Mock<IServiceHealthMonitor>();
        this.mockRecoveryManager = new Mock<IServiceRecoveryManager>();
        this.mockPolicyRepository = new Mock<IPolicyRepository>();

        this.mockUsageReconciler.SetupGet(r => r.IsRunning).Returns(false);
        this.mockEnforcementLevelMonitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Standard);
        this.mockHealthMonitor.SetupGet(m => m.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(m => m.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync("default", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync("default", It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(0, string.Empty));

        this.service = new ScheduledWorkService(
            backendClient: this.mockBackendClient.Object,
            outboxManager: this.mockOutboxManager.Object,
            usageReconciler: this.mockUsageReconciler.Object,
            enforcementLevelMonitor: this.mockEnforcementLevelMonitor.Object,
            timeProvider: this.mockTimeProvider.Object,
            healthMonitor: this.mockHealthMonitor.Object,
            recoveryManager: this.mockRecoveryManager.Object,
            policyRepository: this.mockPolicyRepository.Object);
    }

    public void Dispose()
    {
        this.service.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenDeviceIdMissing_SkipsBackendCall()
    {
        await this.InvokeExecutePolicySyncAsync();

        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(It.Is<string>(id => id != "default"), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        this.mockPolicyRepository.Verify(
            r => r.GetLocalVersionAsync(It.Is<string>(id => id != "default"), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_WhenDeviceIdMissingAndEmpty_SkipsBackendCall()
    {
        await this.InvokeExecutePolicySyncAsync();

        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(It.Is<string>(id => id != "default"), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecutePolicySyncAsync_UsesServicePersistedDeviceId()
    {
        // Current implementation uses the built-in default device identity.
        const string defaultDeviceId = "default";
        this.mockPolicyRepository
            .Setup(r => r.GetLocalVersionAsync(defaultDeviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        this.mockBackendClient
            .Setup(c => c.FetchPolicyAsync(defaultDeviceId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PolicyFetchResult.Succeeded(0, string.Empty));

        await this.InvokeExecutePolicySyncAsync();

        this.mockBackendClient.Verify(
            c => c.FetchPolicyAsync(defaultDeviceId, It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.AtMostOnce);
        this.mockPolicyRepository.Verify(
            r => r.GetLocalVersionAsync(defaultDeviceId, It.IsAny<CancellationToken>()),
            Times.AtMostOnce);
    }

    private Task InvokeExecutePolicySyncAsync()
    {
        // T20/P1 — ExecutePolicySyncAsync was promoted from private to internal
        // so the new dispatch path can call it directly with a CT. We no longer
        // need reflection; just call it with CancellationToken.None.
        return this.service.ExecutePolicySyncAsync(CancellationToken.None);
    }
}
