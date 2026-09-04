// <copyright file="RepositoryRootLocator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;
using System;
using System.IO;
internal static class RepositoryRootLocator
{
    public static string Locate(Type anchorType)
    {
        var current = new DirectoryInfo(Path.GetDirectoryName(anchorType.Assembly.Location)!);
        while (current is not null)
        {
            var gitPath = Path.Combine(current.FullName, ".git");
            if (Directory.Exists(gitPath) || File.Exists(gitPath))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not locate repository root (no .git directory or file ancestor).");
    }
}
