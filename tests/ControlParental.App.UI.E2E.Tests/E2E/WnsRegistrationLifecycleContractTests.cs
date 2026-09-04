// <copyright file="WnsRegistrationLifecycleContractTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.E2E.Tests;

using System.Text;
using System.Text.RegularExpressions;
using Xunit;

public sealed class WnsRegistrationLifecycleContractTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public void Registration_surface_exposes_only_safe_typed_state_UsesButtonEnabledLifecycle()
    {
        var source = File.ReadAllText(FindRegistrationTestSource());
        var activeSource = StripCSharpComments(source);

        Assert.DoesNotContain(
            "AttributeAsync(http, session, \"WnsRegistrationProgress\", \"IsActive\")",
            activeSource,
            StringComparison.Ordinal);

        var buttonEnabledReads = Regex.Matches(
                activeSource,
                """AttributeAsync\s*\(\s*http\s*,\s*session\s*,\s*"WnsRegistrationButton"\s*,\s*"IsEnabled"\s*\)""")
            .Count;

        Assert.True(buttonEnabledReads >= 3, "Initial, polling, and final lifecycle reads must use the button IsEnabled attribute.");
        Assert.Contains("bool.Parse", activeSource, StringComparison.Ordinal);
        Assert.Contains("Assert.True(initialButtonEnabled)", activeSource, StringComparison.Ordinal);
        Assert.Contains("Assert.True(finalButtonEnabled)", activeSource, StringComparison.Ordinal);
        Assert.Contains("observedBusy |= !buttonEnabled", activeSource, StringComparison.Ordinal);
        Assert.Contains("WaitForElementAsync(http, session, \"WnsRegistrationProgress\")", activeSource, StringComparison.Ordinal);
    }

    private static string FindRegistrationTestSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ControlParental.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found."),
            "tests",
            "ControlParental.App.UI.E2E.Tests",
            "E2E",
            "WnsRegistrationE2ETests.cs");
    }

    private static string StripCSharpComments(string source)
    {
        var result = new StringBuilder(source.Length);
        var inLineComment = false;
        var inBlockComment = false;
        var inString = false;
        var inChar = false;
        var escaped = false;

        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inLineComment)
            {
                if (character is '\r' or '\n')
                {
                    inLineComment = false;
                    result.Append(character);
                }

                continue;
            }

            if (inBlockComment)
            {
                if (character == '*' && next == '/')
                {
                    inBlockComment = false;
                    index++;
                }

                continue;
            }

            if (!inString && !inChar && character == '/' && next == '/')
            {
                inLineComment = true;
                index++;
                continue;
            }

            if (!inString && !inChar && character == '/' && next == '*')
            {
                inBlockComment = true;
                index++;
                continue;
            }

            result.Append(character);
            if (escaped)
            {
                escaped = false;
            }
            else if ((inString || inChar) && character == '\\')
            {
                escaped = true;
            }
            else if (character == '"' && !inChar)
            {
                inString = !inString;
            }
            else if (character == '\'' && !inString)
            {
                inChar = !inChar;
            }
        }

        return result.ToString();
    }
}
