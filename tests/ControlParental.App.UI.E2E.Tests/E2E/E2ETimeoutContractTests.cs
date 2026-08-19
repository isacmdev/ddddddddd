// <copyright file="E2ETimeoutContractTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.E2E.Tests;

using Xunit;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

public sealed class E2ETimeoutContractTests
{
    [Fact]
    public void SessionStartupHasItsOwnBoundedBudgetAndDoesNotChangeCommandBudget()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), WnsRegistrationE2ETests.SessionStartTimeout);
    }

    [Fact]
    public void AutomationDriverDefaultsToWinAppDriverAndAllowsOnlyDocumentedNovaContingency()
    {
        var previous = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME");
        try
        {
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME", null);
            Assert.Equal("Windows", WnsRegistrationE2ETests.AutomationName());
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME", "NovaWindows2");
            Assert.Equal("NovaWindows2", WnsRegistrationE2ETests.AutomationName());
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME", "XPath");
            Assert.Equal("Windows", WnsRegistrationE2ETests.AutomationName());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_AUTOMATION_NAME", previous);
        }
    }

    [Fact]
    public void RunbookPropagatesExplicitAppiumHomeToExternalTestProcess()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var runbook = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tests",
            "ControlParental.App.UI.E2E.Tests",
            "run-windows-e2e.ps1"));

        Assert.Contains("[string]$AppiumHome", runbook, StringComparison.Ordinal);
        Assert.Contains("$env:APPIUM_HOME = $AppiumHome", runbook, StringComparison.Ordinal);
        Assert.Contains("$($AppiumUrl.TrimEnd('/'))/status", runbook, StringComparison.Ordinal);
    }

    [Fact]
    public void RunbookAggregateBudgetAccommodatesFiniteE2EEnvelope()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var runbook = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tests",
            "ControlParental.App.UI.E2E.Tests",
            "run-windows-e2e.ps1"));

        Assert.Contains("AddSeconds(180)", runbook, StringComparison.Ordinal);
        Assert.Contains("WaitForExit(150000)", runbook, StringComparison.Ordinal);
    }

    [Fact]
    public void RunbookUsesNoBuildNoRestoreForDedicatedE2EProjectAndPreservesTimeoutWrapper()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var runbook = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tests",
            "ControlParental.App.UI.E2E.Tests",
            "run-windows-e2e.ps1"));
        var activeSource = StripPowerShellComments(runbook);
        var command = Regex.Match(
            activeSource,
            @"Start-Process\s+dotnet\s+-ArgumentList\s+@\((?<arguments>.*?)\)",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        Assert.True(command.Success, "The active runbook must contain its dotnet test command.");
        var tokens = Regex.Matches(command.Groups["arguments"].Value, "'((?:''|[^'])*)'|\\\"((?:\\\"\\\"|[^\\\"])*)\\\"")
            .Select(match => match.Groups[1].Success ? match.Groups[1].Value.Replace("''", "'", StringComparison.Ordinal) : match.Groups[2].Value.Replace("\"\"", "\"", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains("tests/ControlParental.App.UI.E2E.Tests/ControlParental.App.UI.E2E.Tests.csproj", tokens);
        Assert.Contains("--no-restore", tokens);
        Assert.Contains("--no-build", tokens);
        Assert.Contains("-p:Platform=x64", tokens);
        Assert.Contains("Require-Step 'App.UI E2E contract'", activeSource, StringComparison.Ordinal);
        Assert.Contains("WaitForExit(150000)", activeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveRunbookUsesDeterministicPowerShellChildExitHandling()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var runbook = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "tests",
            "ControlParental.App.UI.E2E.Tests",
            "run-windows-e2e.ps1"));
        var activeSource = StripPowerShellComments(runbook);

        Assert.Contains("$completed = $test.WaitForExit(150000)", activeSource, StringComparison.Ordinal);

        var timeout = Regex.Match(
            activeSource,
            @"if\s*\(\s*-not\s+\$completed\s*\)\s*\{(?<body>.*?)\}",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        Assert.True(timeout.Success, "The active runbook must explicitly branch on the bounded wait result.");
        Assert.Contains("taskkill", timeout.Groups["body"].Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/PID $test.Id /T /F", timeout.Groups["body"].Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("throw", timeout.Groups["body"].Value, StringComparison.OrdinalIgnoreCase);

        var completedIndex = activeSource.IndexOf("$completed = $test.WaitForExit(150000)", StringComparison.Ordinal);
        var settledWaitIndex = activeSource.IndexOf("$test.WaitForExit()", completedIndex, StringComparison.Ordinal);
        var refreshIndex = activeSource.IndexOf("$test.Refresh()", settledWaitIndex, StringComparison.Ordinal);
        var exitCodeIndex = activeSource.IndexOf("$exitCode = [int]$test.ExitCode", refreshIndex, StringComparison.Ordinal);
        Assert.True(completedIndex >= 0 && settledWaitIndex > completedIndex && refreshIndex > settledWaitIndex && exitCodeIndex > refreshIndex,
            "The process must settle and refresh before reading its typed exit code.");
        Assert.Contains("if ($exitCode -ne 0)", activeSource, StringComparison.Ordinal);
        Assert.Contains("exit code $exitCode", activeSource, StringComparison.OrdinalIgnoreCase);

        var finallyIndex = activeSource.LastIndexOf("finally", StringComparison.OrdinalIgnoreCase);
        var successExitIndex = activeSource.LastIndexOf("exit 0", StringComparison.OrdinalIgnoreCase);
        Assert.True(finallyIndex >= 0 && successExitIndex > finallyIndex,
            "The script must explicitly exit successfully only after try/finally; thrown errors must remain nonzero.");
    }

    [Fact]
    public void TopLevelWindowCapabilityReplacesAppOnlyWhenConfigured()
    {
        var previous = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW");
        try
        {
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW", null);
            var launch = WnsRegistrationE2ETests.SessionCapabilities("C:\\owned.exe");
            Assert.Equal("C:\\owned.exe", launch["appium:app"]);
            Assert.False(launch.ContainsKey("appium:appTopLevelWindow"));

            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW", "0x12345");
            var attach = WnsRegistrationE2ETests.SessionCapabilities("C:\\owned.exe");
            Assert.Equal("0x12345", attach["appium:appTopLevelWindow"]);
            Assert.False(attach.ContainsKey("appium:app"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW", previous);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-handle")]
    public void TopLevelWindowCapabilityRejectsInvalidHandle(string value)
    {
        var previous = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW");
        try
        {
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW", value);
            Assert.Throws<ArgumentException>(() => WnsRegistrationE2ETests.SessionCapabilities("C:\\owned.exe"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW", previous);
        }
    }

    [Fact]
    public async Task InitialReadinessWaitRetriesUntilAutomationIdAppearsWithoutWallClockSleep()
    {
        var attempts = 0;
        var elapsed = TimeSpan.Zero;

        var element = await WnsRegistrationE2ETests.WaitForElementAsync(
            () =>
            {
                attempts++;
                return attempts < 3
                    ? Task.FromException<string>(new InvalidOperationException("not ready"))
                    : Task.FromResult("element-1");
            },
            "WnsRegistrationNavigationButton",
            TimeSpan.FromSeconds(1),
            delay: delay =>
            {
                elapsed += delay;
                return Task.CompletedTask;
            },
            elapsed: _ => elapsed);

        Assert.Equal("element-1", element);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task InitialReadinessWaitTimesOutWithAutomationIdAndDeadline()
    {
        var elapsed = TimeSpan.Zero;
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            WnsRegistrationE2ETests.WaitForElementAsync(
                () => Task.FromException<string>(new InvalidOperationException("not ready")),
                "MissingAutomationId",
                TimeSpan.FromSeconds(1),
                delay: delay =>
                {
                    elapsed += delay;
                    return Task.CompletedTask;
                },
                elapsed: _ => elapsed));

        Assert.Contains("MissingAutomationId", exception.Message, StringComparison.Ordinal);
        Assert.Contains("1s", exception.Message, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ControlParental.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private static string StripPowerShellComments(string source)
    {
        var result = new StringBuilder(source.Length);
        var inSingleQuotedString = false;
        var inDoubleQuotedString = false;
        var inComment = false;

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inComment)
            {
                if (character is '\r' or '\n')
                {
                    inComment = false;
                    result.Append(character);
                }

                continue;
            }

            if (!inSingleQuotedString && !inDoubleQuotedString && character == '#')
            {
                inComment = true;
                continue;
            }

            result.Append(character);
            if (inSingleQuotedString && character == '\'' && next == '\'')
            {
                result.Append(next);
                index++;
            }
            else if (inDoubleQuotedString && character == '`' && next != '\0')
            {
                result.Append(next);
                index++;
            }
            else if (character == '\'' && !inDoubleQuotedString)
            {
                inSingleQuotedString = !inSingleQuotedString;
            }
            else if (character == '"' && !inSingleQuotedString)
            {
                inDoubleQuotedString = !inDoubleQuotedString;
            }
        }

        return result.ToString();
    }
}
