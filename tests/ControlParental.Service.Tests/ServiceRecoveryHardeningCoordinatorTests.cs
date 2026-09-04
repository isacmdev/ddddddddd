// <copyright file="ServiceRecoveryHardeningCoordinatorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T10 — Tests for isolated SCM recovery hardening.
/// </summary>
public class ServiceRecoveryHardeningCoordinatorTests
{
    [Fact]
    public async Task ApplyAsync_WhenFailureActionsSucceed_ConfiguresStartupType()
    {
        var scm = new Mock<IScmController>(MockBehavior.Strict);
        scm.Setup(c => c.ConfigureFailureActionsAsync("ControlParental.Service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        scm.Setup(c => c.SetStartupTypeAsync("ControlParental.Service", "auto", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var coordinator = new ServiceRecoveryHardeningCoordinator(scm.Object);

        var result = await coordinator.ApplyAsync("ControlParental.Service");

        result.Should().BeTrue();
        scm.Verify();
    }

    [Fact]
    public async Task ApplyAsync_WhenFailureActionsFail_DoesNotConfigureStartupType()
    {
        var scm = new Mock<IScmController>(MockBehavior.Strict);
        scm.Setup(c => c.ConfigureFailureActionsAsync("ControlParental.Service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable();

        var coordinator = new ServiceRecoveryHardeningCoordinator(scm.Object);

        var result = await coordinator.ApplyAsync("ControlParental.Service");

        result.Should().BeFalse();
        scm.Verify(c => c.SetStartupTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        scm.Verify();
    }

    [Fact]
    public async Task ApplyAsync_WhenStartupTypeFailsOnFirstAttempt_RetriesPartialStateOnNextCall()
    {
        var configureCalls = 0;
        var startupCalls = 0;
        var scm = new Mock<IScmController>(MockBehavior.Strict);

        scm.Setup(c => c.ConfigureFailureActionsAsync("ControlParental.Service", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                configureCalls++;
                return true;
            });

        scm.SetupSequence(c => c.SetStartupTypeAsync("ControlParental.Service", "auto", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                startupCalls++;
                return false;
            })
            .ReturnsAsync(() =>
            {
                startupCalls++;
                return true;
            });

        var coordinator = new ServiceRecoveryHardeningCoordinator(scm.Object);

        var first = await coordinator.ApplyAsync("ControlParental.Service");
        var second = await coordinator.ApplyAsync("ControlParental.Service");

        first.Should().BeFalse();
        second.Should().BeTrue();
        configureCalls.Should().Be(2);
        startupCalls.Should().Be(2);
        scm.Verify();
    }
}
