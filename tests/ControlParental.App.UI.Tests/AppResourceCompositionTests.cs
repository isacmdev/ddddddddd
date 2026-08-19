// <copyright file="AppResourceCompositionTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

public sealed class AppResourceCompositionTests
{
    [Fact]
    public void AppResources_MergeExactlyOneWinUiControlsResourcesBeforeApplicationResources()
    {
        var app = XDocument.Load(Path.Combine(LocateRepoRoot(), "src", "ControlParental.App.UI", "App.xaml"));
        var resourceDictionary = app.Root!
            .Descendants()
            .Single(element => element.Name.LocalName == "ResourceDictionary");

        var mergedDictionaries = resourceDictionary.Elements()
            .SingleOrDefault(element => element.Name.LocalName.EndsWith(".MergedDictionaries", StringComparison.Ordinal));

        var frameworkResources = resourceDictionary.Descendants()
            .Where(element => element.Name.LocalName == "XamlControlsResources"
                && element.Name.NamespaceName == "using:Microsoft.UI.Xaml.Controls")
            .ToArray();
        Assert.Single(frameworkResources);

        Assert.NotNull(mergedDictionaries);
        var firstResource = resourceDictionary.Elements().First();
        Assert.Equal("ResourceDictionary.MergedDictionaries", firstResource.Name.LocalName);
    }

    private static string LocateRepoRoot()
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(typeof(AppResourceCompositionTests).Assembly.Location)!);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, ".git")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
