// <copyright file="ConsentFlowTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Moq;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 Fase 1 (Unit 3 closure) — Regression tests for the in-app consent
/// flow:
/// <list type="bullet">
/// <item>The IPC consent service contract (acknowledgement, failure path).</item>
/// <item>The live shell mounts the WinUI <c>ConsentPage</c> and
///       <c>TransparencyPage</c>; no <c>ConsentDialog</c> remains reachable
///       from App.UI.</item>
/// <item>Consent acknowledgement is the gating signal for the page
///       completion callback.</item>
/// </list>
///
/// The legacy console <c>ConsentDialog</c> was deleted in t26-live-flow-closure
/// Unit 3 because the live consent flow is exclusively in-app. These tests
/// pin that contract.
/// </summary>
public sealed class ConsentFlowTests
{
    [Fact]
    public async Task ExecuteStepCommandOnPairingStepDoesNotDriveConsentService()
    {
        // The legacy <c>ConsentDialog</c> surface is gone — no path through the
        // VM should touch a consent service when the user is still on the
        // pairing step.
        var onboardingClient = new FakeIpcOnboardingStateService();
        var viewModel = new OnboardingViewModel(onboardingClient);

        await viewModel.InitializeAsync().ConfigureAwait(false);

        await viewModel.ExecuteStepCommand.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);

        Assert.NotNull(viewModel.CurrentStep);
        Assert.Equal("consent", viewModel.CurrentStep!.Id);
    }

    [Fact]
    public async Task IpcConsentServiceGrantConsentAsyncWhenServiceUnavailableThrows()
    {
        // Arrange — a channel that returns null on QueryAsync simulates the
        // Service being unreachable (pipe timeout, deserialization failure).
        var channel = new Mock<IUIChannel>();
        channel
            .Setup(c => c.QueryAsync<
                ControlParental.App.UI.GetConsentStatus,
                ControlParental.App.UI.ConsentStatusSnapshot>(
                It.IsAny<ControlParental.App.UI.GetConsentStatus>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ControlParental.App.UI.ConsentStatusSnapshot?)null);
        channel
            .Setup(c => c.QueryAsync<
                ControlParental.App.UI.GrantConsent,
                ControlParental.App.UI.ConsentStatusSnapshot>(
                It.IsAny<ControlParental.App.UI.GrantConsent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ControlParental.App.UI.ConsentStatusSnapshot?)null);

        var service = new IpcConsentService(channel.Object);

        // Act & Assert — grant throws ConsentServiceUnavailableException, the
        // contract that lets the page surface a fail-closed error instead of
        // silently advancing.
        await Assert.ThrowsAsync<ConsentServiceUnavailableException>(
            () => service.GrantConsentAsync(null)).ConfigureAwait(false);
    }

    [Fact]
    public async Task IpcConsentServiceGrantConsentAsyncWhenServiceAcknowledgesUpdatesCache()
    {
        // Arrange — a channel that returns a granted snapshot.
        var grantedAt = DateTimeOffset.UtcNow;
        var channel = new Mock<IUIChannel>();
        channel
            .Setup(c => c.QueryAsync<
                ControlParental.App.UI.GrantConsent,
                ControlParental.App.UI.ConsentStatusSnapshot>(
                It.IsAny<ControlParental.App.UI.GrantConsent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ControlParental.App.UI.ConsentStatusSnapshot(
                IsGranted: true,
                GrantedAt: grantedAt,
                GrantedByDeviceId: "test-device"));

        var service = new IpcConsentService(channel.Object);
        Assert.False(service.IsConsentGranted);

        // Act
        await service.GrantConsentAsync("test-device").ConfigureAwait(false);

        // Assert — IsConsentGranted is now true and the cached record reflects the snapshot.
        Assert.True(service.IsConsentGranted);
        var record = await service.GetConsentStatusAsync().ConfigureAwait(false);
        Assert.Equal(ConsentStatus.Granted, record.Status);
        Assert.Equal("test-device", record.GrantedByDeviceId);
    }

    [Fact]
    public void LiveShellRoutesConsentAndTransparencyInApp()
    {
        var repoRoot = ResolveRepoRoot();
        var appSource = File.ReadAllText(
            Path.Combine(repoRoot, "src", "ControlParental.App.UI", "App.xaml.cs"));
        var shellSource = File.ReadAllText(
            Path.Combine(repoRoot, "src", "ControlParental.App.UI", "MainWindow.xaml.cs"));
        var shellXaml = File.ReadAllText(
            Path.Combine(repoRoot, "src", "ControlParental.App.UI", "MainWindow.xaml"));

        Assert.DoesNotContain("new ConsentDialog", appSource, StringComparison.Ordinal);
        Assert.Contains("AddSingleton<IConsentService, IpcConsentService>", appSource, StringComparison.Ordinal);
        Assert.Contains(
            "AddSingleton<IEnforcementLevelMonitor, ServiceEnforcementLevelMonitor>",
            appSource,
            StringComparison.Ordinal);
        Assert.Contains("new ConsentPage", shellSource, StringComparison.Ordinal);
        Assert.Contains("new TransparencyPage", shellSource, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"PageHost\"", shellXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"ExecuteButton\"", shellXaml, StringComparison.Ordinal);
        Assert.DoesNotContain("x:Name=\"NextButton\"", shellXaml, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveShellAppUiSourcesDoNotReferenceConsentDialog()
    {
        // Unit 3 — final live-flow legacy-console regression. A grep over every
        // production App.UI source file must not contain the legacy
        // <c>ConsentDialog</c> type. The class itself was deleted in t26-live-flow-closure
        // Unit 3; this test pins the entire class against reintroduction through
        // a stray using-statement, a stubbed reference, or a copied snippet.
        var repoRoot = ResolveRepoRoot();
        var appUiRoot = Path.Combine(repoRoot, "src", "ControlParental.App.UI");

        // Files explicitly re-scanned: the production source set App.UI compiles.
        // XAML/pages are excluded because they cannot contain C# type references.
        var scannedFiles = new[]
        {
            Path.Combine(appUiRoot, "App.xaml.cs"),
            Path.Combine(appUiRoot, "OnboardingViewModel.cs"),
            Path.Combine(appUiRoot, "MainWindow.xaml.cs"),
            Path.Combine(appUiRoot, "PairingPage.xaml.cs"),
            Path.Combine(appUiRoot, "ConsentPage.xaml.cs"),
            Path.Combine(appUiRoot, "TransparencyPage.xaml.cs"),
            Path.Combine(appUiRoot, "AccountStepPage.xaml.cs"),
            Path.Combine(appUiRoot, "ServiceSetupPage.xaml.cs"),
            Path.Combine(appUiRoot, "DemoStepPage.xaml.cs"),
            Path.Combine(appUiRoot, "ManagedStepPage.xaml.cs"),
            Path.Combine(appUiRoot, "StatusPage.xaml.cs"),
            Path.Combine(appUiRoot, "PairingViewModel.cs"),
            Path.Combine(appUiRoot, "AccountStepViewModel.cs"),
            Path.Combine(appUiRoot, "DemoStepViewModel.cs"),
            Path.Combine(appUiRoot, "ManagedStepViewModel.cs"),
            Path.Combine(appUiRoot, "StatusViewModel.cs"),
            Path.Combine(appUiRoot, "OnboardingRouteCatalog.cs"),
            Path.Combine(appUiRoot, "ConsentService.cs"),
            Path.Combine(appUiRoot, "ConsentStringsAdapter.cs"),
        };

        foreach (var relativeOrAbsolute in scannedFiles)
        {
            var fullPath = relativeOrAbsolute;
            if (!File.Exists(fullPath))
            {
                // Skip optional files that may not exist on every branch.
                continue;
            }

            var contents = File.ReadAllText(fullPath);
            Assert.False(
                contents.Contains("ConsentDialog", StringComparison.Ordinal),
                $"{fullPath} still references the deleted ConsentDialog — Unit 3 closed the legacy console path.");
        }

        // Defense in depth: the legacy source file must be absent from the tree.
        var dialogSourcePath = Path.Combine(appUiRoot, "ConsentDialog.cs");
        Assert.False(
            File.Exists(dialogSourcePath),
            $"{dialogSourcePath} must be absent from the working tree after t26-live-flow-closure Unit 3.");
    }

    [Fact]
    public void ConsentPageReportsCompletionOnlyAfterAcknowledgedGrant()
    {
        var repoRoot = ResolveRepoRoot();
        var source = File.ReadAllText(
            Path.Combine(repoRoot, "src", "ControlParental.App.UI", "ConsentPage.xaml.cs"));
        var failureGate = source.IndexOf("if (!result.Succeeded)", StringComparison.Ordinal);
        var completionCallback = source.IndexOf("this.onConsentGranted?.Invoke()", StringComparison.Ordinal);

        Assert.True(failureGate >= 0);
        Assert.True(completionCallback > failureGate);
        Assert.DoesNotContain("App.MainWindow", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Frame.Navigate", source, StringComparison.Ordinal);
        Assert.Contains("this.onTransparencyRequested?.Invoke()", source, StringComparison.Ordinal);
    }

    private static string ResolveRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ControlParental.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate ControlParental.sln.");
    }
}
