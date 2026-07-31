// <copyright file="Strings.Designer.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Globalization;
using System.Resources;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T25 — Strongly-typed resource accessor for localized strings.
/// </summary>
internal sealed class Strings
{
    private static readonly ResourceManager ResourceManager = new("ControlParental.Domain.Strings", typeof(Strings).Assembly);

    /// <summary>
    /// Gets a localized string value.
    /// </summary>
    /// <param name="name">The resource key.</param>
    /// <returns>The localized string.</returns>
    public static string GetString(string name)
    {
        return ResourceManager.GetString(name, CultureInfo.CurrentUICulture) ?? name;
    }

    /// <summary>
    /// Gets the <see cref="ResourceManager"/> for this class.
    /// </summary>
    internal static ResourceManager ResourceManagerProperty => ResourceManager;
}
