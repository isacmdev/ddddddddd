// <copyright file="Decision.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Result of evaluating a policy against an app and context.
/// Used by T02 Rules Engine.
/// </summary>
/// <param name="IsBlocked">True if the app should be blocked, false if allowed.</param>
/// <param name="ReasonCode">Machine-readable reason code (1-11 from T02 spec, null if allowed).</param>
/// <param name="ReasonText">Human-readable reason text (from T25 copy catalog, null if allowed).</param>
public readonly record struct Decision(
    bool IsBlocked,
    int? ReasonCode,
    string? ReasonText)
{
    /// <summary>
    /// Allows the app unconditionally.
    /// </summary>
    public static Decision Allow { get; } = new(false, null, null);

    /// <summary>
    /// Blocks the app with the given reason code and text.
    /// </summary>
    public static Decision Block(int reasonCode, string reasonText)
        => new(true, reasonCode, reasonText);
}
