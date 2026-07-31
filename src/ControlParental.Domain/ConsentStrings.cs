// <copyright file="ConsentStrings.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T25 — Localized consent UI strings.
/// </summary>
public static class ConsentStrings
{
    /// <summary>
    /// Title for the data disclosure dialog.
    /// </summary>
    public static string DisclosureTitle => Strings.GetString("DisclosureTitle");

    /// <summary>
    /// Body text for the data disclosure dialog.
    /// </summary>
    public static string DisclosureBody => Strings.GetString("DisclosureBody");

    /// <summary>
    /// Title for the transparency detail dialog.
    /// </summary>
    public static string TransparencyTitle => Strings.GetString("TransparencyTitle");

    /// <summary>
    /// Body text explaining what is monitored.
    /// </summary>
    public static string TransparencyBody => Strings.GetString("TransparencyBody");

    /// <summary>
    /// Label for the accept button.
    /// </summary>
    public static string AcceptButton => Strings.GetString("AcceptButton");

    /// <summary>
    /// Label for the view transparency details button.
    /// </summary>
    public static string ViewTransparencyButton => Strings.GetString("ViewTransparencyButton");

    /// <summary>
    /// Reason text when an app is blocked by parent.
    /// </summary>
    public static string ReasonBlocked => Strings.GetString("ReasonBlocked");

    /// <summary>
    /// Reason text when an app is blocked due to downtime.
    /// </summary>
    public static string ReasonDowntime => Strings.GetString("ReasonDowntime");

    /// <summary>
    /// Reason text when an app is blocked due to time limit.
    /// </summary>
    public static string ReasonLimitReached => Strings.GetString("ReasonLimitReached");

    /// <summary>
    /// Reason text when account lacks admin privileges.
    /// </summary>
    public static string ReasonAdminDetected => Strings.GetString("ReasonAdminDetected");
}
