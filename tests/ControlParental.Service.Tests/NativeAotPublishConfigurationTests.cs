// <copyright file="NativeAotPublishConfigurationTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.IO;
using Xunit;

public sealed class NativeAotPublishConfigurationTests
{
    [Fact]
    public void ReleaseNativeAotConfiguration_EnablesTrimming()
    {
        var root = LocateRepoRoot();
        var serviceProject = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ControlParental.Service",
            "ControlParental.Service.csproj"));
        var sharedProps = File.ReadAllText(Path.Combine(root, "Directory.Build.props"));

        Assert.Contains("<PublishAot>true</PublishAot>", serviceProject, StringComparison.Ordinal);
        Assert.Contains("<PublishTrimmed>true</PublishTrimmed>", serviceProject, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<PublishTrimmed Condition=\"'$(Configuration)' == 'Release'\">false</PublishTrimmed>",
            sharedProps,
            StringComparison.Ordinal);
    }

    private static string LocateRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null &&
               !Directory.Exists(Path.Combine(current.FullName, ".git")) &&
               !File.Exists(Path.Combine(current.FullName, ".git")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
