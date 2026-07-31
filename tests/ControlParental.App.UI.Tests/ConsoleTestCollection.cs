// <copyright file="ConsoleTestCollection.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using Xunit;

public static class ConsoleTestCollection
{
    public const string Name = "Console IO";
}

[CollectionDefinition("Console IO", DisableParallelization = true)]
public sealed class ConsoleTestCollectionDefinition
{
}
