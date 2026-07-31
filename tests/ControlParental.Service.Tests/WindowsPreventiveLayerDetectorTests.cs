// <copyright file="WindowsPreventiveLayerDetectorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Security;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Xunit;

/// <summary>
/// T12 — Focused tests for the read-only Windows prevention-layer detector.
/// These tests pin the public contract and the fail-safe behaviour that
/// <see cref="EnforcementLevelMonitor"/> relies on. The detector itself is
/// tested through its internal seams (<see cref="Func{TResult}"/> delegates)
/// so the behaviour is deterministic regardless of the host registry.
/// </summary>
public sealed class WindowsPreventiveLayerDetectorTests
{
    // ── Construction / contract ────────────────────────────────────────

    [Fact]
    public void Constructor_WithNullReadEdition_ThrowsArgumentNullException()
    {
        // Arrange
        Func<PreventiveLayerProbeResult> probe =
            () => throw new InvalidOperationException("unused");

        // Act
        var act = () => new WindowsPreventiveLayerDetector(
            readEdition: null!,
            probeWdac: probe,
            probeAppLocker: probe,
            probeMdm: probe);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("readEdition");
    }

    [Fact]
    public void Constructor_WithNullProbeWdac_ThrowsArgumentNullException()
    {
        // Arrange
        Func<WindowsEditionProbeResult> editionProbe =
            () => WindowsEditionProbeResult.Supported("Windows 10 Pro");
        Func<PreventiveLayerProbeResult> layerProbe =
            () => PreventiveLayerProbeResult.Absent("unused");

        // Act
        var act = () => new WindowsPreventiveLayerDetector(
            readEdition: editionProbe,
            probeWdac: null!,
            probeAppLocker: layerProbe,
            probeMdm: layerProbe);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("probeWdac");
    }

    [Fact]
    public void Constructor_WithNullProbeAppLocker_ThrowsArgumentNullException()
    {
        // Arrange
        Func<WindowsEditionProbeResult> editionProbe =
            () => WindowsEditionProbeResult.Supported("Windows 10 Pro");
        Func<PreventiveLayerProbeResult> layerProbe =
            () => PreventiveLayerProbeResult.Absent("unused");

        // Act
        var act = () => new WindowsPreventiveLayerDetector(
            readEdition: editionProbe,
            probeWdac: layerProbe,
            probeAppLocker: null!,
            probeMdm: layerProbe);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("probeAppLocker");
    }

    [Fact]
    public void Constructor_WithNullProbeMdm_ThrowsArgumentNullException()
    {
        // Arrange
        Func<WindowsEditionProbeResult> editionProbe =
            () => WindowsEditionProbeResult.Supported("Windows 10 Pro");
        Func<PreventiveLayerProbeResult> layerProbe =
            () => PreventiveLayerProbeResult.Absent("unused");

        // Act
        var act = () => new WindowsPreventiveLayerDetector(
            readEdition: editionProbe,
            probeWdac: layerProbe,
            probeAppLocker: layerProbe,
            probeMdm: null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("probeMdm");
    }

    // ── Positive evidence (Task 3.1) ───────────────────────────────────

    [Fact]
    public async Task DetectAsync_WhenWdacIsPresent_ReturnsPresentWithWdacKind()
    {
        // Arrange — WDAC is present; AppLocker and MDM are absent.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Present("WDAC policy"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("no AppLocker"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("no MDM"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Present);
        result.DetectedLayer.Should().Be(PreventiveLayerKind.Wdac);
        result.Detail.Should().Contain("WDAC");
    }

    [Fact]
    public async Task DetectAsync_WhenAppLockerIsPresent_ReturnsPresentWithAppLockerKind()
    {
        // Arrange — AppLocker is present; WDAC and MDM are absent.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Enterprise"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("no WDAC"),
            probeAppLocker: () => PreventiveLayerProbeResult.Present("AppLocker policy"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("no MDM"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Present);
        result.DetectedLayer.Should().Be(PreventiveLayerKind.AppLocker);
        result.Detail.Should().Contain("AppLocker");
    }

    [Fact]
    public async Task DetectAsync_WhenMdmIsPresent_ReturnsPresentWithMdmKind()
    {
        // Arrange — MDM is present; WDAC and AppLocker are absent.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 11 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("no WDAC"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("no AppLocker"),
            probeMdm: () => PreventiveLayerProbeResult.Present("MDM enrolled"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Present);
        result.DetectedLayer.Should().Be(PreventiveLayerKind.Mdm);
        result.Detail.Should().Contain("MDM");
    }

    [Fact]
    public async Task DetectAsync_WhenMultipleLayersPresent_ReturnsFirstPositiveAndStops()
    {
        // Arrange — WDAC and AppLocker are both present; the detector must
        // return the first positive evidence (WDAC) and MUST NOT short-circuit
        // to keep probing. The probe invoked twice proves that the first
        // positive short-circuits the remaining probes.
        var appLockerInvocations = 0;
        var mdmInvocations = 0;

        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Present("WDAC policy"),
            probeAppLocker: () =>
            {
                appLockerInvocations++;
                return PreventiveLayerProbeResult.Present("AppLocker policy");
            },
            probeMdm: () =>
            {
                mdmInvocations++;
                return PreventiveLayerProbeResult.Present("MDM enrolled");
            });

        // Act
        var result = await detector.DetectAsync();

        // Assert — WDAC is the first positive; AppLocker and MDM are NEVER
        // invoked because the detector short-circuits on the first hit.
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Present);
        result.DetectedLayer.Should().Be(PreventiveLayerKind.Wdac);
        appLockerInvocations.Should().Be(0);
        mdmInvocations.Should().Be(0);
    }

    // ── Absent / Unsupported evidence ──────────────────────────────────

    [Fact]
    public async Task DetectAsync_WhenAllProbesAbsent_AndEditionSupported_ReturnsAbsent()
    {
        // Arrange — edition supports prevention; no layer is verifiably active.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("no WDAC"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("no AppLocker"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("no MDM"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Absent);
        result.DetectedLayer.Should().BeNull();
        result.Detail.Should().Contain("No preventive layer");
    }

    [Fact]
    public async Task DetectAsync_WhenEditionIsUnsupported_ReturnsUnsupported_EvenWithPresentProbes()
    {
        // Arrange — Home edition cannot host any preventive layer in
        // enforcement mode. Even if a probe reports Present, the
        // detector must return Unsupported (edition-relative).
        var probeInvocations = 0;

        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Unsupported("Windows 10 Home"),
            probeWdac: () =>
            {
                probeInvocations++;
                return PreventiveLayerProbeResult.Present("ignored");
            },
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("ignored"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("ignored"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Unsupported);
        result.DetectedLayer.Should().BeNull();
        result.Detail.Should().Contain("Home");
        probeInvocations.Should().Be(0,
            "edition probe runs first; probes MUST NOT run on an unsupported edition");
    }

    // ── Indeterminate evidence (Task 3.1) ──────────────────────────────

    [Fact]
    public async Task DetectAsync_WhenEditionIsIndeterminate_ReturnsIndeterminate()
    {
        // Arrange — edition cannot be read (corrupt registry, missing key, etc.).
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Indeterminate("registry key missing"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("unused"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("unused"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("unused"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Indeterminate);
        result.DetectedLayer.Should().BeNull();
        result.Detail.Should().Contain("registry key missing");
    }

    [Fact]
    public async Task DetectAsync_WhenEditionProbeThrowsUnauthorizedAccess_ReturnsIndeterminate()
    {
        // Arrange — the edition probe raises UnauthorizedAccessException; the
        // detector MUST normalize to Indeterminate (security failure ≠ empty).
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => throw new UnauthorizedAccessException("HLKM denied"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("unused"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("unused"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("unused"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Indeterminate);
        result.Detail.Should().Contain("Access denied");
    }

    [Fact]
    public async Task DetectAsync_WhenAnyProbeThrowsUnauthorizedAccess_ReturnsIndeterminate()
    {
        // Arrange — edition is supported; AppLocker probe raises
        // UnauthorizedAccessException. The detector must surface that as
        // Indeterminate, NOT as Absent. Verified absence requires
        // affirmative read.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("no WDAC"),
            probeAppLocker: () => throw new UnauthorizedAccessException("registry denied"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("no MDM"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Indeterminate);
        result.DetectedLayer.Should().BeNull();
        result.Detail.Should().Contain("Access denied");
    }

    [Fact]
    public async Task DetectAsync_WhenAnyProbeThrowsSecurityException_ReturnsIndeterminate()
    {
        // Arrange — SecurityException is treated the same as UnauthorizedAccessException.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => throw new SecurityException("denied"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("ignored"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("ignored"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Indeterminate);
        result.Detail.Should().Contain("Security error");
    }

    [Fact]
    public async Task DetectAsync_WhenAnyProbeThrowsIOException_ReturnsIndeterminate()
    {
        // Arrange — I/O failures (corrupt registry hive, transport issues) are
        // also Indeterminate, not Absent.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("ignored"),
            probeAppLocker: () => throw new IOException("hive corrupt"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("ignored"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Indeterminate);
        result.Detail.Should().Contain("I/O error");
    }

    [Fact]
    public async Task DetectAsync_WhenProbeReturnsIndeterminate_AndOthersAbsent_ReturnsIndeterminate()
    {
        // Arrange — AppLocker probe returns Indeterminate explicitly (the
        // probe COULD read the registry but its data was contradictory).
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("no WDAC"),
            probeAppLocker: () => PreventiveLayerProbeResult.Indeterminate("contradictory sources"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("no MDM"));

        // Act
        var result = await detector.DetectAsync();

        // Assert — the specific probe detail is preserved end-to-end so the
        // surfaced alert reflects the actual root cause.
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Indeterminate);
        result.Detail.Should().Contain("contradictory sources");
    }

    // ── Cancellation ───────────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WhenCancellationRequestedBeforeStart_PropagatesOperationCanceled()
    {
        // Arrange — version of the test that checks the public surface:
        // if the probe throws OperationCanceledException, the detector must
        // propagate it (the monitor's catch filter will let it through).
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => throw new OperationCanceledException("cancelled"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("unused"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("unused"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("unused"));

        // Act + Assert — cancellation propagates (no degraded snapshot).
        Func<Task> act = () => detector.DetectAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DetectAsync_WhenProbeThrowsOperationCanceled_PropagatesThroughProbe()
    {
        // Arrange — the probe itself can throw OperationCanceledException
        // (e.g., the read chokes on a cancelled IO). The detector must
        // NOT convert it to Indeterminate.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => throw new OperationCanceledException("cancelled by probe"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("unused"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("unused"));

        // Act + Assert
        Func<Task> act = () => detector.DetectAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DetectAsync_WhenCancellationTokenIsCancelled_PropagatesOperationCanceled()
    {
        // Arrange — a pre-cancelled token; the detector must surface
        // OperationCanceledException to the caller.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("unused"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("unused"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("unused"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act + Assert
        Func<Task> act = () => detector.DetectAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── Read-only probe observations ───────────────────────────────────

    [Fact]
    public async Task DetectAsync_DoesNotMutateDetectedLayer_WhenAbsent()
    {
        // Arrange — verified absence MUST leave DetectedLayer null (the
        // contract is "DetectedLayer is only set when Status is Present").
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("no WDAC"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("no AppLocker"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("no MDM"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Absent);
        result.DetectedLayer.Should().BeNull();
    }

    [Fact]
    public async Task DetectAsync_DoesNotMutateDetectedLayer_WhenUnsupported()
    {
        // Arrange — unsupported edition MUST leave DetectedLayer null.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Unsupported("Windows 10 Home"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("unused"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("unused"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("unused"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Unsupported);
        result.DetectedLayer.Should().BeNull();
    }

    [Fact]
    public async Task DetectAsync_DoesNotMutateDetectedLayer_WhenIndeterminate()
    {
        // Arrange — indeterminate (from any source) MUST leave DetectedLayer null.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Indeterminate("contradictory"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("unused"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("unused"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Indeterminate);
        result.DetectedLayer.Should().BeNull();
    }

    // ── Probe invocation order ─────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_ProbesInExpectedOrder_WdacThenAppLockerThenMdm()
    {
        // Arrange — record the order of probe invocations.
        var invocations = new List<string>();

        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () =>
            {
                invocations.Add("Wdac");
                return PreventiveLayerProbeResult.Absent("ignored");
            },
            probeAppLocker: () =>
            {
                invocations.Add("AppLocker");
                return PreventiveLayerProbeResult.Absent("ignored");
            },
            probeMdm: () =>
            {
                invocations.Add("Mdm");
                return PreventiveLayerProbeResult.Absent("ignored");
            });

        // Act
        var result = await detector.DetectAsync();

        // Assert — probes are invoked in WDAC, AppLocker, MDM order.
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Absent);
        invocations.Should().Equal("Wdac", "AppLocker", "Mdm");
    }

    [Fact]
    public async Task DetectAsync_WhenWdacIndeterminate_StillProbesOthers()
    {
        // Arrange — WDAC is Indeterminate; the detector MUST continue probing
        // AppLocker and MDM (the kind probes are independent). If AppLocker
        // is Present, the final result is Present (AppLocker).
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Indeterminate("contradictory"),
            probeAppLocker: () => PreventiveLayerProbeResult.Present("AppLocker enforced"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("ignored"));

        // Act
        var result = await detector.DetectAsync();

        // Assert — AppLocker evidence takes precedence over the prior
        // indeterminate; the layer is observed through WDAC then AppLocker.
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Present);
        result.DetectedLayer.Should().Be(PreventiveLayerKind.AppLocker);
    }

    // ── Detail forwarding ───────────────────────────────────────────────

    [Fact]
    public async Task DetectAsync_WhenWdacPresent_ForwardsDetectedLayerDetail()
    {
        // Arrange — confirm the human-readable detail from the probe is
        // preserved end-to-end so the surfaced Info issue is non-generic.
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Present("specific WDAC evidence"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("ignored"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("ignored"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Present);
        result.DetectedLayer.Should().Be(PreventiveLayerKind.Wdac);
        result.Detail.Should().Be("specific WDAC evidence");
    }

    [Fact]
    public async Task DetectAsync_WhenAllProbesAbsent_DetailIncludesEditionName()
    {
        // Arrange — verified absence must include the edition context so the
        // Info issue is informative ("no preventive layer on Windows 10 Pro").
        var detector = new WindowsPreventiveLayerDetector(
            readEdition: () => WindowsEditionProbeResult.Supported("Windows 10 Pro"),
            probeWdac: () => PreventiveLayerProbeResult.Absent("ignored"),
            probeAppLocker: () => PreventiveLayerProbeResult.Absent("ignored"),
            probeMdm: () => PreventiveLayerProbeResult.Absent("ignored"));

        // Act
        var result = await detector.DetectAsync();

        // Assert
        result.Status.Should().Be(PreventiveLayerDetectionStatus.Absent);
        result.Detail.Should().Contain("Windows 10 Pro");
    }
}
