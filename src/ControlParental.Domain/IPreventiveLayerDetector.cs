// <copyright file="IPreventiveLayerDetector.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T12 — Read-only detector for the preventive control layer
/// (WDAC, AppLocker, MDM) on the local machine.
/// </summary>
/// <remarks>
/// The detector is intentionally side-effect free. It MUST NOT configure,
/// deploy, enroll, or remediate any preventive control. It only inspects
/// local Windows state to report what is verifiably present, verifiably
/// absent, unsupported by the running edition, or indeterminate due to
/// access denial, errors, or unknown capabilities.
/// </remarks>
public interface IPreventiveLayerDetector
{
    /// <summary>
    /// Inspects the local Windows state and reports a single preventive-layer
    /// verdict.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PreventiveLayerDetectionResult"/> whose
    /// <see cref="PreventiveLayerDetectionResult.Status"/> is one of
    /// <see cref="PreventiveLayerDetectionStatus.Present"/>,
    /// <see cref="PreventiveLayerDetectionStatus.Absent"/>,
    /// <see cref="PreventiveLayerDetectionStatus.Unsupported"/>, or
    /// <see cref="PreventiveLayerDetectionStatus.Indeterminate"/>.
    /// </returns>
    Task<PreventiveLayerDetectionResult> DetectAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable result returned by <see cref="IPreventiveLayerDetector"/>.
/// </summary>
/// <param name="Status">
/// Verdict: <see cref="PreventiveLayerDetectionStatus.Present"/> when a
/// supported preventive layer is verifiably active; <see cref="Absent"/>
/// when a supported layer is verifiably not active; <see cref="Unsupported"/>
/// when the running edition cannot host any of the supported layers; or
/// <see cref="Indeterminate"/> when the detector could not read the
/// required evidence (access denied, exception, contradictory sources,
/// or unknown edition).
/// </param>
/// <param name="DetectedLayer">
/// Specific layer that produced positive evidence (only set when
/// <paramref name="Status"/> is <see cref="PreventiveLayerDetectionStatus.Present"/>).
/// </param>
/// <param name="Detail">
/// Human-readable, non-PII detail suitable for surfacing in logs and
/// alerts. MUST NOT include credentials or registry values.
/// </param>
public sealed record PreventiveLayerDetectionResult(
    PreventiveLayerDetectionStatus Status,
    PreventiveLayerKind? DetectedLayer,
    string Detail);

/// <summary>
/// Status of the preventive-layer detection outcome.
/// </summary>
public enum PreventiveLayerDetectionStatus
{
    /// <summary>
    /// A supported preventive layer is verifiably active.
    /// </summary>
    Present,

    /// <summary>
    /// A supported preventive layer is verifiably not active on this host.
    /// </summary>
    Absent,

    /// <summary>
    /// The running Windows edition cannot host any supported preventive layer.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The detector could not read required evidence (access denied, exception,
    /// unknown edition, contradictory sources). MUST map to <c>DEGRADED</c>.
    /// </summary>
    Indeterminate,
}

/// <summary>
/// Specific preventive control that produced positive evidence.
/// </summary>
public enum PreventiveLayerKind
{
    /// <summary>
    /// Windows Defender Application Control.
    /// </summary>
    Wdac,

    /// <summary>
    /// AppLocker policy.
    /// </summary>
    AppLocker,

    /// <summary>
    /// Mobile Device Management enrollment/policy.
    /// </summary>
    Mdm,
}
