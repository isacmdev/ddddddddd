// <copyright file="ScmControllerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Collections.Concurrent;
using ControlParental.Service;
using FluentAssertions;
using Xunit;

/// <summary>
/// T10 — Tests for ScmController idempotency and safe SCM command orchestration.
/// </summary>
public class ScmControllerTests
{
    [Fact]
    public async Task StartServiceAsync_WhenAlreadyRunning_DoesNotIssueStartCommand()
    {
        var commands = new ConcurrentQueue<string>();
        var running = true;

        var controller = new ScmController(command =>
        {
            commands.Enqueue(command);

            if (command.StartsWith("query", StringComparison.OrdinalIgnoreCase))
            {
                return (true, running ? "STATE              : 4  RUNNING" : "STATE              : 1  STOPPED");
            }

            if (command.StartsWith("start", StringComparison.OrdinalIgnoreCase))
            {
                running = true;
                return (true, "SERVICE_START_PENDING");
            }

            return (true, string.Empty);
        });

        var result = await controller.StartServiceAsync("ControlParental.Service");

        result.Should().BeTrue();
        commands.Should().ContainSingle(c => c.StartsWith("query", StringComparison.OrdinalIgnoreCase));
        commands.Should().NotContain(c => c.StartsWith("start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StartServiceAsync_WhenStopped_IssuesStartCommand()
    {
        var commands = new ConcurrentQueue<string>();
        var controller = new ScmController(command =>
        {
            commands.Enqueue(command);
            return command.StartsWith("query", StringComparison.OrdinalIgnoreCase)
                ? (true, "STATE              : 1  STOPPED")
                : (true, "SERVICE_START_PENDING");
        });

        var result = await controller.StartServiceAsync("ControlParental.Service");

        result.Should().BeTrue();
        commands.Should().Contain(c => c.StartsWith("start", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task IsServiceRunningAsync_WhenQueryFails_ReturnsFalse()
    {
        var controller = new ScmController(_ => (false, "access denied"));

        var result = await controller.IsServiceRunningAsync("ControlParental.Service");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task StopServiceAsync_WhenAlreadyStopped_DoesNotIssueStopCommand()
    {
        var commands = new ConcurrentQueue<string>();
        var running = false;

        var controller = new ScmController(command =>
        {
            commands.Enqueue(command);

            if (command.StartsWith("query", StringComparison.OrdinalIgnoreCase))
            {
                return (true, running ? "STATE              : 4  RUNNING" : "STATE              : 1  STOPPED");
            }

            if (command.StartsWith("stop", StringComparison.OrdinalIgnoreCase))
            {
                running = false;
                return (true, "SERVICE_STOPPED");
            }

            return (true, string.Empty);
        });

        var result = await controller.StopServiceAsync("ControlParental.Service");

        result.Should().BeTrue();
        commands.Should().ContainSingle(c => c.StartsWith("query", StringComparison.OrdinalIgnoreCase));
        commands.Should().NotContain(c => c.StartsWith("stop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StopServiceAsync_WhenRunning_IssuesStopCommand()
    {
        var commands = new ConcurrentQueue<string>();
        var controller = new ScmController(command =>
        {
            commands.Enqueue(command);
            return command.StartsWith("query", StringComparison.OrdinalIgnoreCase)
                ? (true, "STATE              : 4  RUNNING")
                : (true, "SERVICE_STOPPED");
        });

        var result = await controller.StopServiceAsync("ControlParental.Service");

        result.Should().BeTrue();
        commands.Should().Contain(c => c.StartsWith("stop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConfigureFailureActionsAsync_IsIdempotent_ForSameService()
    {
        var commands = new ConcurrentQueue<string>();
        var controller = new ScmController(command =>
        {
            commands.Enqueue(command);
            return (true, string.Empty);
        });

        var first = await controller.ConfigureFailureActionsAsync("ControlParental.Service");
        var second = await controller.ConfigureFailureActionsAsync("ControlParental.Service");

        first.Should().BeTrue();
        second.Should().BeTrue();
        commands.Should().ContainSingle(c => c.StartsWith("failure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConfigureFailureActionsAsync_WhenCommandFails_ReturnsFalse()
    {
        var controller = new ScmController(_ => (false, "failure"));

        var result = await controller.ConfigureFailureActionsAsync("ControlParental.Service");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetStartupTypeAsync_IsIdempotent_ForSameServiceAndType()
    {
        var commands = new ConcurrentQueue<string>();
        var controller = new ScmController(command =>
        {
            commands.Enqueue(command);
            return (true, string.Empty);
        });

        var first = await controller.SetStartupTypeAsync("ControlParental.Service", "auto");
        var second = await controller.SetStartupTypeAsync("ControlParental.Service", "automatic");

        first.Should().BeTrue();
        second.Should().BeTrue();
        commands.Should().ContainSingle(c => c.StartsWith("config", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetStartupTypeAsync_IsIdempotent_PerServiceName()
    {
        var commands = new ConcurrentQueue<string>();
        var controller = new ScmController(command =>
        {
            commands.Enqueue(command);
            return (true, string.Empty);
        });

        var firstServiceFirstCall = await controller.SetStartupTypeAsync("ControlParental.Service.A", "auto");
        var firstServiceSecondCall = await controller.SetStartupTypeAsync("ControlParental.Service.A", "automatic");
        var secondServiceFirstCall = await controller.SetStartupTypeAsync("ControlParental.Service.B", "auto");
        var secondServiceSecondCall = await controller.SetStartupTypeAsync("ControlParental.Service.B", "automatic");

        firstServiceFirstCall.Should().BeTrue();
        firstServiceSecondCall.Should().BeTrue();
        secondServiceFirstCall.Should().BeTrue();
        secondServiceSecondCall.Should().BeTrue();

        commands.Should().HaveCount(2);
        commands.Should().Contain(command => command.Contains("\"ControlParental.Service.A\"", StringComparison.OrdinalIgnoreCase));
        commands.Should().Contain(command => command.Contains("\"ControlParental.Service.B\"", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SetStartupTypeAsync_WhenCommandFails_ReturnsFalse()
    {
        var controller = new ScmController(_ => (false, "config failed"));

        var result = await controller.SetStartupTypeAsync("ControlParental.Service", "auto");

        result.Should().BeFalse();
    }
}
