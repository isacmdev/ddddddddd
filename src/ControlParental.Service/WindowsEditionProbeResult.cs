// <copyright file="WindowsEditionProbeResult.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// Status of the Windows edition probe.
/// </summary>
internal enum WindowsEditionProbeStatus
{
    /// <summary>The edition can host at least one supported preventive layer.</summary>
    Supported,

    /// <summary>The edition cannot host any supported preventive layer.</summary>
    Unsupported,

    /// <summary>The edition could not be determined.</summary>
    Indeterminate,
}

/// <summary>
/// Result of probing the Windows edition profile.
/// </summary>
internal sealed record WindowsEditionProbeResult(
    WindowsEditionProbeStatus Status,
    string Detail)
{
    public static WindowsEditionProbeResult Supported(string detail) =>
        new(WindowsEditionProbeStatus.Supported, detail);

    public static WindowsEditionProbeResult Unsupported(string detail) =>
        new(WindowsEditionProbeStatus.Unsupported, detail);

    public static WindowsEditionProbeResult Indeterminate(string detail) =>
        new(WindowsEditionProbeStatus.Indeterminate, detail);
}
