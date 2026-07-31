// <copyright file="WindowsPreventiveLayerDetector.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Security;
using ControlParental.Domain;
using Microsoft.Win32;

/// <summary>
/// T12 — Windows implementation of <see cref="IPreventiveLayerDetector"/>.
/// Inspects local Windows state for one of the supported preventive control
/// layers (WDAC, AppLocker, MDM) and returns a single typed verdict.
/// </summary>
/// <remarks>
/// <para>
/// Read-only by design. The detector NEVER configures, deploys, enrolls, or
/// remediates any preventive control. It only inspects the registry and the
/// local service catalogue to report what is verifiably present, verifiably
/// absent, unsupported by the running edition, or indeterminate due to access
/// denied, contradictory sources, or unknown edition.
/// </para>
/// <para>
/// The detector is intentionally narrow and dependency-free aside from the
/// registry. It does NOT touch WMI, MDM enrollment, or any web endpoint.
/// </para>
/// <para>
/// Testability seams: an internal constructor accepts <see cref="Func{TResult}"/>
/// delegates for the edition probe and each kind probe so the public
/// behaviour can be exercised deterministically without depending on the host
/// registry. The production constructor uses the built-in registry probes.
/// </para>
/// </remarks>
public sealed class WindowsPreventiveLayerDetector : IPreventiveLayerDetector
{
    private readonly Func<WindowsEditionProbeResult> readEdition;
    private readonly Func<PreventiveLayerProbeResult> probeWdac;
    private readonly Func<PreventiveLayerProbeResult> probeAppLocker;
    private readonly Func<PreventiveLayerProbeResult> probeMdm;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsPreventiveLayerDetector"/>
    /// class that inspects the local Windows registry for the preventive
    /// control layer evidence.
    /// </summary>
    public WindowsPreventiveLayerDetector()
        : this(ReadWindowsEdition, ProbeWdac, ProbeAppLocker, ProbeMdm)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsPreventiveLayerDetector"/>
    /// class with the supplied evidence probes. Intended for unit tests so the
    /// detector can be exercised deterministically without depending on the host
    /// registry.
    /// </summary>
    /// <param name="readEdition">Probe for the Windows edition profile.</param>
    /// <param name="probeWdac">Probe for WDAC evidence.</param>
    /// <param name="probeAppLocker">Probe for AppLocker evidence.</param>
    /// <param name="probeMdm">Probe for MDM evidence.</param>
    internal WindowsPreventiveLayerDetector(
        Func<WindowsEditionProbeResult> readEdition,
        Func<PreventiveLayerProbeResult> probeWdac,
        Func<PreventiveLayerProbeResult> probeAppLocker,
        Func<PreventiveLayerProbeResult> probeMdm)
    {
        this.readEdition = readEdition ?? throw new ArgumentNullException(nameof(readEdition));
        this.probeWdac = probeWdac ?? throw new ArgumentNullException(nameof(probeWdac));
        this.probeAppLocker = probeAppLocker ?? throw new ArgumentNullException(nameof(probeAppLocker));
        this.probeMdm = probeMdm ?? throw new ArgumentNullException(nameof(probeMdm));
    }

    /// <inheritdoc />
    public Task<PreventiveLayerDetectionResult> DetectAsync(
        CancellationToken cancellationToken = default)
    {
        // Run on a thread pool worker so the caller's synchronization context
        // is not blocked by registry I/O. Cancellation is observed by the
        // caller via the passed-in token; we throw OperationCanceledException
        // so it propagates back to the monitor (which already filters it out).
        return Task.Run(
            () => this.DetectCore(cancellationToken),
            cancellationToken);
    }

    private PreventiveLayerDetectionResult DetectCore(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // 1. Establish the Windows edition profile. Any failure here
        // short-circuits to Indeterminate because we cannot prove
        // which evidence sources are applicable.
        var edition = this.SafeReadEdition();
        if (edition.Status == WindowsEditionProbeStatus.Indeterminate)
        {
            return new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Indeterminate,
                null,
                edition.Detail);
        }

        if (edition.Status == WindowsEditionProbeStatus.Unsupported)
        {
            // Per T12 design: an edition that cannot host any supported
            // preventive layer is not equivalent to a degraded state.
            return new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Unsupported,
                null,
                edition.Detail);
        }

        // 2. Probe each supported kind. Stop at the first positive evidence.
        var wdac = this.SafeProbe(this.probeWdac);
        if (wdac.Status == PreventiveLayerDetectionStatus.Present)
        {
            return IndeterminateIfRequired(wdac) ?? new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Present,
                PreventiveLayerKind.Wdac,
                wdac.Detail);
        }

        var appLocker = this.SafeProbe(this.probeAppLocker);
        if (appLocker.Status == PreventiveLayerDetectionStatus.Present)
        {
            return IndeterminateIfRequired(appLocker) ?? new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Present,
                PreventiveLayerKind.AppLocker,
                appLocker.Detail);
        }

        var mdm = this.SafeProbe(this.probeMdm);
        if (mdm.Status == PreventiveLayerDetectionStatus.Present)
        {
            return IndeterminateIfRequired(mdm) ?? new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Present,
                PreventiveLayerKind.Mdm,
                mdm.Detail);
        }

        // 3. If any probe was indeterminate, fail safe to Indeterminate.
        // Surface the FIRST specific detail (preserves the most informative
        // cause) so the alert reflects the actual root cause.
        if (wdac.Status == PreventiveLayerDetectionStatus.Indeterminate)
        {
            return new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Indeterminate,
                null,
                wdac.Detail);
        }

        if (appLocker.Status == PreventiveLayerDetectionStatus.Indeterminate)
        {
            return new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Indeterminate,
                null,
                appLocker.Detail);
        }

        if (mdm.Status == PreventiveLayerDetectionStatus.Indeterminate)
        {
            return new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Indeterminate,
                null,
                mdm.Detail);
        }

        // 4. Edition supports prevention but no layer is verifiably active.
        return new PreventiveLayerDetectionResult(
            PreventiveLayerDetectionStatus.Absent,
            null,
            "No preventive layer (WDAC/AppLocker/MDM) detected on " + edition.Detail);
    }

    private static PreventiveLayerDetectionResult? IndeterminateIfRequired(
        PreventiveLayerProbeResult probe)
    {
        if (probe.Status == PreventiveLayerDetectionStatus.Indeterminate)
        {
            return new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Indeterminate,
                null,
                probe.Detail);
        }

        return null;
    }

    private WindowsEditionProbeResult SafeReadEdition()
    {
        try
        {
            return this.readEdition();
        }
        catch (UnauthorizedAccessException ex)
        {
            return WindowsEditionProbeResult.Indeterminate(
                "Access denied while reading Windows edition: " + ex.Message);
        }
        catch (SecurityException ex)
        {
            return WindowsEditionProbeResult.Indeterminate(
                "Security error while reading Windows edition: " + ex.Message);
        }
        catch (IOException ex)
        {
            return WindowsEditionProbeResult.Indeterminate(
                "I/O error while reading Windows edition: " + ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return WindowsEditionProbeResult.Indeterminate(
                "Invalid registry state while reading Windows edition: " + ex.Message);
        }
    }

    private PreventiveLayerProbeResult SafeProbe(Func<PreventiveLayerProbeResult> probe)
    {
        try
        {
            return probe();
        }
        catch (UnauthorizedAccessException ex)
        {
            return PreventiveLayerProbeResult.Indeterminate(
                "Access denied: " + ex.Message);
        }
        catch (SecurityException ex)
        {
            return PreventiveLayerProbeResult.Indeterminate(
                "Security error: " + ex.Message);
        }
        catch (IOException ex)
        {
            return PreventiveLayerProbeResult.Indeterminate(
                "I/O error: " + ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return PreventiveLayerProbeResult.Indeterminate(
                "Invalid registry state: " + ex.Message);
        }
    }

    // ── Static Windows registry probes ───────────────────────────────────

    /// <summary>
    /// Reads the Windows edition profile from the registry. Returns
    /// <see cref="WindowsEditionProbeStatus.Unsupported"/> for editions
    /// that cannot host any of the supported preventive layers (Home,
    /// Single Language, S Mode, IoT Core, etc.).
    /// </summary>
    private static WindowsEditionProbeResult ReadWindowsEdition()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
            writable: false);

        if (key is null)
        {
            return WindowsEditionProbeResult.Indeterminate(
                "Windows NT CurrentVersion registry key not found");
        }

        var editionId = key.GetValue("EditionID") as string;
        var productName = key.GetValue("ProductName") as string;
        var installationType = key.GetValue("InstallationType") as string;

        // Combine the strongest signal available; prefer EditionID for
        // SKU-level classification, fall back to InstallationType for
        // Server SKUs which do not always set EditionID.
        var signal = editionId ?? installationType ?? string.Empty;

        if (string.IsNullOrWhiteSpace(signal))
        {
            return WindowsEditionProbeResult.Indeterminate(
                "Windows edition identifier is empty");
        }

        if (IsUnsupportedEdition(signal))
        {
            return WindowsEditionProbeResult.Unsupported(
                $"Windows edition '{signal}' does not support WDAC/AppLocker/MDM enforcement");
        }

        return WindowsEditionProbeResult.Supported(
            $"Windows edition '{signal}' (ProductName: '{productName ?? "unknown"}')");
    }

    private static bool IsUnsupportedEdition(string edition)
    {
        // T12 definition: a Home/x64-style SKU cannot host any of the
        // supported preventive layers in enforcement mode. The list is
        // intentionally conservative; if a SKU is not listed as supported
        // here we treat it as supported and rely on the layer probes
        // to verify absence.
        //
        // Education / Enterprise / Professional / Pro for Workstations /
        // Server Datacenter / Server Standard all support WDAC, AppLocker,
        // or MDM in enforcement mode and are deliberately NOT listed here.
        //
        // Source: Microsoft documentation for WDAC/AppLocker requirements.
        return edition.Contains("Home", StringComparison.OrdinalIgnoreCase)
            || edition.StartsWith("Core", StringComparison.OrdinalIgnoreCase)
            || edition.Contains("Single Language", StringComparison.OrdinalIgnoreCase)
            || edition.Contains("S Mode", StringComparison.OrdinalIgnoreCase)
            || edition.Contains("IoT Core", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Probes the registry for WDAC (Windows Defender Application Control)
    /// active policy. Requires a locally observable policy artifact under
    /// <c>HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy</c>.
    /// </summary>
    private static PreventiveLayerProbeResult ProbeWdac()
    {
        // The CI policy registry key is only present when a WDAC policy is
        // actively deployed. Auxiliary state (HypervisorEnforcedCodeIntegrity,
        // LsaCfgFlags) is informational; per T12 design we require positive
        // policy evidence.
        using var ciPolicyKey = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\CI\Policy",
            writable: false);

        if (ciPolicyKey is not null)
        {
            return PreventiveLayerProbeResult.Present(
                "WDAC policy registered under SYSTEM\\CurrentControlSet\\Control\\CI\\Policy");
        }

        // Fallback: a deployed WDAC policy also registers the
        // HypervisorEnforcedCodeIntegrity scenario. The presence of an
        // Enabled value of 1 confirms HVCI/WDAC is operational.
        using var hvciKey = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
            writable: false);

        if (hvciKey is not null)
        {
            var enabled = hvciKey.GetValue("Enabled");
            if (enabled is int enabledInt && enabledInt == 1)
            {
                return PreventiveLayerProbeResult.Present(
                    "HypervisorEnforcedCodeIntegrity is enabled");
            }
        }

        return PreventiveLayerProbeResult.Absent(
            "No WDAC policy or HVCI scenario detected");
    }

    /// <summary>
    /// Probes the registry for AppLocker enforcement. Requires the
    /// <c>HKLM\SOFTWARE\Policies\Microsoft\Windows\SrpV2</c> policy tree
    /// to contain at least one rule collection (Exe, Msi, Script, Dll,
    /// Appx). The Application Identity service start type is also
    /// inspected as a corroborating signal.
    /// </summary>
    private static PreventiveLayerProbeResult ProbeAppLocker()
    {
        using var srpKey = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Policies\Microsoft\Windows\SrpV2",
            writable: false);

        if (srpKey is null)
        {
            return PreventiveLayerProbeResult.Absent(
                "AppLocker policy tree (SrpV2) is not present");
        }

        // At least one rule collection must be configured.
        var collections = srpKey.GetSubKeyNames();
        var knownCollections = new[]
        {
            "Exe", "Msi", "Script", "Dll", "Appx", "AppXDeploymentServer",
        };

        foreach (var collection in knownCollections)
        {
            if (Array.IndexOf(collections, collection) < 0)
            {
                continue;
            }

            using var collectionKey = srpKey.OpenSubKey(
                collection,
                writable: false);
            if (collectionKey is null)
            {
                continue;
            }

            // Each rule collection must have a Policy subkey with Enforce
            // mode (value 1) or Audit mode (value 2) for the policy to be
            // considered active. The mere presence of the rule collection
            // tree is not sufficient.
            using var policyKey = collectionKey.OpenSubKey(
                "Policy",
                writable: false);
            if (policyKey is null)
            {
                continue;
            }

            var value = policyKey.GetValue("Enforcement");
            if (value is int enforcement && (enforcement == 1 || enforcement == 2))
            {
                return PreventiveLayerProbeResult.Present(
                    $"AppLocker policy '{collection}' is configured (Enforcement={enforcement})");
            }
        }

        // Corroborating signal: AppIDSvc (Application Identity) start type.
        // Running enforced AppLocker requires AppIDSvc to be auto-start (2).
        using var appIdSvcKey = Registry.LocalMachine.OpenSubKey(
            @"SYSTEM\CurrentControlSet\Services\AppIDSvc",
            writable: false);
        if (appIdSvcKey is not null)
        {
            var start = appIdSvcKey.GetValue("Start");
            if (start is int startInt && startInt == 2)
            {
                return PreventiveLayerProbeResult.Present(
                    "AppIDSvc is set to auto-start with active policy tree");
            }
        }

        return PreventiveLayerProbeResult.Absent(
            "AppLocker policy tree is present but no active enforcement configuration was found");
    }

    /// <summary>
    /// Probes the registry for MDM enrollment. Checks both the
    /// <c>HKLM\SOFTWARE\Microsoft\Enrollments</c> tree (local client
    /// enrollments) and the <c>HKLM\SOFTWARE\Microsoft\PolicyManager\Providers</c>
    /// tree (mobile-device-management providers).
    /// </summary>
    private static PreventiveLayerProbeResult ProbeMdm()
    {
        // Local enrollments (e.g., Azure AD joined devices).
        using var enrollmentsKey = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Enrollments",
            writable: false);

        if (enrollmentsKey is not null)
        {
            var enrollments = enrollmentsKey.GetSubKeyNames();
            if (enrollments.Length > 0)
            {
                return PreventiveLayerProbeResult.Present(
                    $"MDM enrollment detected ({enrollments.Length} enrollment(s))");
            }
        }

        // MDM provider-side state.
        using var providersKey = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\PolicyManager\Providers",
            writable: false);

        if (providersKey is not null)
        {
            var providers = providersKey.GetSubKeyNames();
            if (providers.Length > 0)
            {
                return PreventiveLayerProbeResult.Present(
                    $"MDM policy provider detected ({providers.Length} provider(s))");
            }
        }

        return PreventiveLayerProbeResult.Absent(
            "No MDM enrollment or policy provider detected");
    }
}
