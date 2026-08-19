// <copyright file="AppStartupDiagnosticsTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

public sealed class AppStartupDiagnosticsTests
{
    [Fact]
    public void MainWindowFailureDiagnosticsIncludeNestedTypeHResultAndXamlLocationContract()
    {
        var source = ReadAppUiFile("App.xaml.cs");

        Assert.Contains("FormatStartupException", source, StringComparison.Ordinal);
        Assert.Contains("InnerException", source, StringComparison.Ordinal);
        Assert.Contains("HResult", source, StringComparison.Ordinal);
        Assert.Contains("LineNumber", source, StringComparison.Ordinal);
        Assert.Contains("LinePosition", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ex.ToString()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainWindowFailureDiagnosticsIncludeActionableXamlContextWithoutSensitiveDetails()
    {
        var formatter = typeof(App).GetMethod(
            "FormatMainWindowStartupException",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(formatter is not null, "App must expose the MainWindow startup diagnostic composition seam.");

        var inner = new XamlLikeException("nested loader failure", unchecked((int)0x802B000A), 42, 7);
        var outer = new InvalidOperationException("XAML parsing failed", inner);
        var diagnostic = (string)formatter!.Invoke(
            obj: null,
            parameters: [outer]);

        Assert.Contains("XamlFile=MainWindow.xaml", diagnostic, StringComparison.Ordinal);
        Assert.Contains("TargetType=MainWindow", diagnostic, StringComparison.Ordinal);
        Assert.Contains("ResourceKey=PageHost", diagnostic, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", diagnostic, StringComparison.Ordinal);
        Assert.Contains("HResult=0x802B000A", diagnostic, StringComparison.Ordinal);
        Assert.Contains("LineNumber=42", diagnostic, StringComparison.Ordinal);
        Assert.Contains("LinePosition=7", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("StackTrace", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccessToken", diagnostic, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void AppStartupDoesNotDefineOrInvokeCustomMessageLoop()
    {
        var source = StripCommentsAndStringLiterals(ReadAppUiFile("App.xaml.cs"));

        Assert.DoesNotMatch(@"\bRunMessageLoop\s*\(", source);
        Assert.DoesNotMatch(@"\b(?:GetMessage|TranslateMessage|DispatchMessage)\s*\(", source);
    }

    private static string StripCommentsAndStringLiterals(string source)
    {
        var result = new StringBuilder(source.Length);
        var inLineComment = false;
        var inBlockComment = false;
        var inString = false;
        var inChar = false;
        var verbatimString = false;

        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            var next = index + 1 < source.Length ? source[index + 1] : '\0';

            if (inLineComment)
            {
                if (current is '\r' or '\n')
                {
                    inLineComment = false;
                    result.Append(current);
                }

                continue;
            }

            if (inBlockComment)
            {
                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    index++;
                }

                continue;
            }

            if (inString)
            {
                if (verbatimString && current == '"' && next == '"')
                {
                    index++;
                }
                else if ((!verbatimString && current == '\\') || current == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (inChar)
            {
                if (current == '\\')
                {
                    index++;
                }
                else if (current == '\'')
                {
                    inChar = false;
                }

                continue;
            }

            if (current == '/' && next == '/')
            {
                inLineComment = true;
                index++;
            }
            else if (current == '/' && next == '*')
            {
                inBlockComment = true;
                index++;
            }
            else if (current == '"')
            {
                inString = true;
                verbatimString = index > 0 && source[index - 1] == '@';
            }
            else if (current == '\'')
            {
                inChar = true;
            }
            else
            {
                result.Append(current);
            }
        }

        return result.ToString();
    }

    private sealed class XamlLikeException : Exception
    {
        public XamlLikeException(string message, int hResult, int lineNumber, int linePosition)
            : base(message)
        {
            this.HResult = hResult;
            this.LineNumber = lineNumber;
            this.LinePosition = linePosition;
        }

        public int LineNumber { get; }

        public int LinePosition { get; }
    }

    private static string ReadAppUiFile(string fileName)
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(typeof(AppStartupDiagnosticsTests).Assembly.Location)!);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, ".git")))
        {
            current = current.Parent;
        }

        return File.ReadAllText(Path.Combine(current!.FullName, "src", "ControlParental.App.UI", fileName));
    }
}
