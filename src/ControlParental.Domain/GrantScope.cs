// <copyright file="GrantScope.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Scope of a grant (time permission).
/// - device: applies to all apps globally
/// - package_name: applies only to that specific app
/// - category: applies to all apps in that category
/// </summary>
public enum GrantScope
{
    /// <summary>
    /// Grant applies to the entire device (all apps).
    /// </summary>
    Device,

    /// <summary>
    /// Grant applies to a specific package name (AppId).
    /// </summary>
    Package,

    /// <summary>
    /// Grant applies to all apps in a category.
    /// </summary>
    Category
}
