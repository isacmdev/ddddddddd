// <copyright file="PreventiveLayerProbeResult.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// Result of probing a single preventive-layer source.
/// </summary>
internal sealed record PreventiveLayerProbeResult(
    PreventiveLayerDetectionStatus Status,
    string Detail)
{
    public static PreventiveLayerProbeResult Present(string detail) =>
        new(PreventiveLayerDetectionStatus.Present, detail);

    public static PreventiveLayerProbeResult Absent(string detail) =>
        new(PreventiveLayerDetectionStatus.Absent, detail);

    public static PreventiveLayerProbeResult Indeterminate(string detail) =>
        new(PreventiveLayerDetectionStatus.Indeterminate, detail);
}
