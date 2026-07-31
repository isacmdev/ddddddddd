// <copyright file="AgentLauncherCommandLineTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Service;
using Xunit;

/// <summary>
/// T38 — Regression tests for the Session Agent launch command line.
/// </summary>
public sealed class AgentLauncherCommandLineTests
{
    [Fact]
    public void BuildCommandLine_QuotesExecutableAndAppendsPipeArgument()
    {
        // Arrange
        var exe = @"C:\Program Files\ControlParental\Agent\ControlParental.SessionAgent.exe";
        var pipeName = "SessionAgent";

        // Act
        var commandLine = AgentLauncher.BuildCommandLine(exe, pipeName);

        // Assert
        Assert.StartsWith("\"", commandLine);
        Assert.Contains(exe, commandLine);
        Assert.Contains("--pipe=SessionAgent", commandLine);
    }

    [Fact]
    public void BuildCommandLine_PreservesPipeNameWithoutPrefix()
    {
        // Act
        var commandLine = AgentLauncher.BuildCommandLine("agent.exe", "MyPipe");

        // Assert
        Assert.Equal("\"agent.exe\" --pipe=MyPipe", commandLine);
    }

    [Fact]
    public void BuildCommandLine_HandlesPathsWithSpaces()
    {
        // Act
        var commandLine = AgentLauncher.BuildCommandLine("C:\\Program Files\\Agent\\agent.exe", "Pipe");

        // Assert
        Assert.StartsWith("\"C:\\Program Files\\Agent\\agent.exe\"", commandLine);
        Assert.EndsWith(" --pipe=Pipe", commandLine);
    }
}
