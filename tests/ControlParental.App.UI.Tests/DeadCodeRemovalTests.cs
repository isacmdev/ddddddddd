// <copyright file="DeadCodeRemovalTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System;
using System.IO;
using System.Reflection;
using FluentAssertions;
using Xunit;

/// <summary>
/// T25/T26 (Unit 3) — regression tests pinning the removal of symbols that
/// are no longer part of the current product surface.
///
/// <para>
/// The current product still intentionally uses the following —
/// they are NOT dead and must NOT be removed:
/// </para>
/// <list type="bullet">
/// <item><c>ConsentStrings</c> (Domain-shipped literal copy — retained for
///       engine-side reasons beyond the WinUI flow).</item>
/// <item><c>OnboardingViewModel.ExecutePairingStepAsync</c> / <c>ExecuteDemoStepAsync</c> / <c>OnDemoTimerTick</c> / <c>OpenMsSettings</c> / <c>IsDemoOverlayVisible</c>.</item>
/// </list>
///
/// <para>
/// T25/T26 closure (Unit 3) deletes the legacy console disclosure path —
/// <c>ConsentDialog</c> — because the live onboarding flow uses the in-app
/// WinUI <c>ConsentPage</c>. The legacy constructor that accepted a
/// <c>ConsentDialog</c> argument on the <c>OnboardingViewModel</c> is also
/// gone because no production site needs it.
/// </para>
/// </summary>
public sealed class DeadCodeRemovalTests
{
    private const string AppUIAssemblyName = "ControlParental.App.UI";

    [Fact]
    public void ServiceInstallStepPageDoesNotExist()
    {
        // PR #8b removed the in-app service install page because the service
        // is delivered via MSIX. Reintroducing it would re-add the obsolete
        // copy and the dead MainWindow.NavigateToStep routing.
        var pageType = ResolveTypeAcrossAssemblies("ServiceInstallStepPage");
        pageType.Should().BeNull(
            "ServiceInstallStepPage was deleted in PR #8b — service install is delivered via MSIX, not in-app.");
    }

    [Fact]
    public void ServiceInstallStepViewModelDoesNotExist()
    {
        // PR #8b removed the dead view model for the obsolete install step.
        var vmType = ResolveTypeAcrossAssemblies("ServiceInstallStepViewModel");
        vmType.Should().BeNull(
            "ServiceInstallStepViewModel was deleted in PR #8b — its only consumer was ServiceInstallStepPage.");
    }

    [Fact]
    public void ServiceInstallStepPageFilesNotPresentOnDisk()
    {
        // Defense in depth: even if reflection somehow resolves a stale type,
        // the files must be gone from the working tree so future diffs cannot
        // regress.
        var deleted = new[]
        {
            @"src/ControlParental.App.UI/ServiceInstallStepPage.xaml",
            @"src/ControlParental.App.UI/ServiceInstallStepPage.xaml.cs",
            @"src/ControlParental.App.UI/ServiceInstallStepViewModel.cs",
        };

        var repoRoot = RepositoryRootLocator.Locate(typeof(DeadCodeRemovalTests));
        foreach (var relativePath in deleted)
        {
            var fullPath = Path.Combine(repoRoot, relativePath);
            File.Exists(fullPath).Should().BeFalse(
                $"{relativePath} must be absent from the working tree after PR #8b.");
        }
    }

    [Fact]
    public void ConsentDialogDoesNotExist()
    {
        // Unit 3 — the live WinUI onboarding flow presents disclosure and
        // consent through ConsentPage. The legacy console ConsentDialog was
        // only exercised from non-production test seams; reintroducing it
        // would resurrect an out-of-app fall-back that T25 explicitly forbids.
        var dialogType = ResolveTypeAcrossAssemblies("ConsentDialog");
        dialogType.Should().BeNull(
            "ConsentDialog was deleted in t26-live-flow-closure Unit 3 — the live consent flow is the in-app ConsentPage.");
    }

    [Fact]
    public void ConsentDialogFileNotPresentOnDisk()
    {
        // Defense in depth: the source file must stay absent from the working
        // tree so future diffs cannot silently re-introduce the legacy path.
        var fullPath = Path.Combine(
            RepositoryRootLocator.Locate(typeof(DeadCodeRemovalTests)),
            "src",
            "ControlParental.App.UI",
            "ConsentDialog.cs");

        File.Exists(fullPath).Should().BeFalse(
            "src/ControlParental.App.UI/ConsentDialog.cs must be absent from the working tree after t26-live-flow-closure Unit 3.");
    }

    private static Type? ResolveTypeAcrossAssemblies(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType($"{AppUIAssemblyName}.{typeName}", throwOnError: false);
            if (type != null)
            {
                return type;
            }

            type = assembly.GetType($"ControlParental.Domain.{typeName}", throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

}
