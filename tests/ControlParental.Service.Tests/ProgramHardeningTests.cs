// <copyright file="ProgramHardeningTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using ControlParental.Domain;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

/// <summary>
/// T10 — Tests for Program hardening wiring.
/// </summary>
public class ProgramHardeningTests
{
    [Fact]
    public async Task ApplyHardeningAsync_InvokesAclAndSCMHardeningBoundaries()
    {
        var aclHardener = new Mock<IAclHardener>(MockBehavior.Strict);
        aclHardener.Setup(h => h.HardenAllAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var privilegeInspector = new Mock<IPrivilegeInspector>(MockBehavior.Strict);
        privilegeInspector.Setup(p => p.GetPrivilegeLevelAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrivilegeLevel.Standard)
            .Verifiable();

        var scm = new Mock<IScmController>(MockBehavior.Strict);
        scm.Setup(s => s.ConfigureFailureActionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        scm.Setup(s => s.SetStartupTypeAsync(It.IsAny<string>(), "auto", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var timeProvider = new Mock<ITimeProvider>(MockBehavior.Strict);
        timeProvider.SetupGet(t => t.MonotonicNow).Returns(0);

        var healthMonitor = new ServiceHealthMonitor(
            timeProvider.Object,
            onAgentDied: () => { },
            onServiceUnhealthy: _ => { });

        var services = new ServiceCollection();
        services.AddSingleton(aclHardener.Object);
        services.AddSingleton(privilegeInspector.Object);
        services.AddSingleton(scm.Object);
        services.AddSingleton(healthMonitor);

        var provider = services.BuildServiceProvider();
        var applyHardening = typeof(Program).GetMethod(
            "ApplyHardeningAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var task = (Task)applyHardening.Invoke(null, new object[] { provider })!;
        await task;

        aclHardener.Verify();
        privilegeInspector.Verify();
        scm.Verify();
        healthMonitor.SecurityVerdict.Should().Be(RuntimeSecurityVerdict.HealthyStandard);
    }
}
