// <copyright file="ControlParentalServiceReenforcementTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using ControlParental.Domain;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T11 — Tests that ControlParentalService re-enforces the current foreground app
/// when the usage accumulator reports that a usage limit has been crossed.
/// These tests bypass the Windows-specific service startup path by invoking the
/// private reaction handler directly through reflection.
/// </summary>
public class ControlParentalServiceReenforcementTests : IDisposable
{
    private readonly ControlParentalService service;
    private readonly Mock<IUsageAccumulator> mockUsageAccumulator;
    private readonly Mock<IEnforcementEngine> mockEnforcementEngine;
    private readonly Mock<IProtectedProcessReporter> mockProtectedProcessReporter;
    private readonly string tempDataPath;

    public ControlParentalServiceReenforcementTests()
    {
        this.mockUsageAccumulator = new Mock<IUsageAccumulator>();
        this.mockEnforcementEngine = new Mock<IEnforcementEngine>();
        this.mockProtectedProcessReporter = new Mock<IProtectedProcessReporter>();
        this.tempDataPath = Path.Combine(Path.GetTempPath(), $"cp_reenforce_{Guid.NewGuid():N}");

        this.mockProtectedProcessReporter
            .Setup(r => r.GetStatusDescriptionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Service binary is signed and PPL-capable.");

        this.service = new ControlParentalService(
            scmController: new Mock<IScmController>().Object,
            privilegeInspector: new Mock<IPrivilegeInspector>().Object,
            accountManager: new Mock<IAccountManager>().Object,
            usageAccumulator: this.mockUsageAccumulator.Object,
            usageReconciler: new Mock<IUsageReconciler>().Object,
            workstationLockManager: new Mock<IWorkstationLockManager>().Object,
            overlayPersistenceManager: new Mock<IOverlayPersistenceManager>().Object,
            healthMonitor: new Mock<IServiceHealthMonitor>().Object,
            recoveryManager: new Mock<IServiceRecoveryManager>().Object,
            timeProvider: new Mock<ITimeProvider>().Object,
            policyRepository: new Mock<IPolicyRepository>().Object,
            processTerminator: new Mock<IProcessTerminator>().Object,
            protectedProcessReporter: this.mockProtectedProcessReporter.Object,
            enforcementLevelMonitor: new Mock<IEnforcementLevelMonitor>().Object);

        this.SetEnforcementEngine(this.mockEnforcementEngine.Object);
    }

    public void Dispose()
    {
        this.service.Dispose();

        try
        {
            if (Directory.Exists(this.tempDataPath))
            {
                Directory.Delete(this.tempDataPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void OnForegroundChanged_WithoutSessionAuthority_DoesNotBypassSafetyLoop()
    {
        // Arrange
        const string AppId = "com.example.app";

        this.mockEnforcementEngine
            .Setup(e => e.EnforceForegroundChangeAsync(AppId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EnforcementResult
            {
                Success = true,
                Blocked = true,
                Timestamp = DateTimeOffset.UtcNow,
                ReasonText = "Límite alcanzado",
            });

        // Act
        this.InvokeOnForegroundChanged(AppId);

        // Assert
        this.mockUsageAccumulator.Verify(u => u.OnForegroundChanged(AppId), Times.Once);
        this.mockEnforcementEngine.Verify(
            e => e.EnforceForegroundChangeAsync(AppId, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OnForegroundChanged_WhenEngineIsNull_OnlyUpdatesUsageAccumulator()
    {
        const string AppId = "com.example.app";
        this.ClearEnforcementEngine();

        // Act
        this.InvokeOnForegroundChanged(AppId);

        // Assert
        this.mockUsageAccumulator.Verify(u => u.OnForegroundChanged(AppId), Times.Once);
        this.mockEnforcementEngine.Verify(
            e => e.EnforceForegroundChangeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReportProtectedProcessStatusAsync_WhenReporterExists_QueriesStatusOnce()
    {
        var method = typeof(ControlParentalService).GetMethod(
            "ReportProtectedProcessStatusAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);

        method.Should().NotBeNull("the startup PPL status helper must exist for testability");

        var invocation = method!.Invoke(this.service, new object[] { CancellationToken.None });
        invocation.Should().BeAssignableTo<Task>();

        await ((Task)invocation!).WaitAsync(TimeSpan.FromSeconds(2));

        this.mockProtectedProcessReporter.Verify(
            r => r.GetStatusDescriptionAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private void SetEnforcementEngine(IEnforcementEngine engine)
    {
        var field = typeof(ControlParentalService).GetField(
            "enforcementEngine",
            BindingFlags.NonPublic | BindingFlags.Instance);

        field.Should().NotBeNull("the private enforcementEngine field must exist for testability");
        field!.SetValue(this.service, engine);
    }

    private void ClearEnforcementEngine()
    {
        this.SetEnforcementEngine(null!);
    }

    private void InvokeOnForegroundChanged(string appId)
    {
        var method = typeof(ControlParentalService).GetMethod(
            "OnForegroundChanged",
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(ForegroundChanged) },
            null);

        method.Should().NotBeNull("the private OnForegroundChanged handler must exist for testability");
        method!.Invoke(this.service, new object[] { new ForegroundChanged(appId) });
    }
}
