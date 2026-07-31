// <copyright file="MainWindow.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 — Live shell hosting the canonical six-step onboarding flow.
/// Routes the <see cref="OnboardingRouteCatalog"/> through the
/// <see cref="OnboardingViewModel"/> and mounts the matching in-app pages
/// into the <c>PageHost</c> region. Page completion callbacks advance the
/// canonical Service-owned state through the IPC-backed VM — the shell
/// never mutates persisted state itself (ADR-002).
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly OnboardingViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The IPC-backed onboarding view model. Cannot be null.</param>
    public MainWindow(OnboardingViewModel viewModel)
    {
        this.viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        this.viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
        this.InitializeComponent();
        _ = this.InitializeOnboardingAsync();
    }

    /// <summary>
    /// Gets the typed resource accessor exposed to XAML. The header binds
    /// <c>Strings.ProtectionActiveOverlay</c> through this instance so the
    /// localized copy stays in the canonical WinUI catalog
    /// (design.md Â§10, ADR-007).
    /// </summary>
    public StringsAdapter Strings => StringsAdapter.Instance;

    /// <summary>
    /// Gets the onboarding view model so XAML <c>{x:Bind ViewModel.ProgressCount}</c>
    /// can render the live progress bar from the routed page host.
    /// </summary>
    public OnboardingViewModel ViewModel => this.viewModel;

    private async Task InitializeOnboardingAsync()
    {
        try
        {
            await this.viewModel.InitializeAsync();
        }
        catch
        {
            // Onboarding init failed — the VM surfaces the IPC error and the
            // page host remains on the current route. The shell never advances
            // without Service acknowledgement (ADR-001).
        }

        this.NavigateToCurrentRoute();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnboardingViewModel.CurrentStep)
            || e.PropertyName == nameof(OnboardingViewModel.IsCompleted)
            || e.PropertyName == nameof(OnboardingViewModel.IsAbandoned))
        {
            this.NavigateToCurrentRoute();
        }
    }

    private void NavigateToCurrentRoute()
    {
        var stepId = this.viewModel.CurrentStep?.Id;
        var route = OnboardingRouteCatalog.Select(
            stepId,
            this.viewModel.IsCompleted,
            this.viewModel.IsAbandoned);

        this.PageHost.Content = route switch
        {
            OnboardingRoute.Pairing => new PairingPage(
                onPairingCompleted: () => this.OnStepCompletedAsync("pairing"),
                uiChannel: null),
            OnboardingRoute.Consent => new ConsentPage(
                onConsentGranted: () => this.OnStepCompletedAsync("consent"),
                onTransparencyRequested: () => this.NavigateToTransparencyFromConsent()),
            OnboardingRoute.Transparency => new TransparencyPage(
                onBackRequested: () => this.NavigateBackToConsent()),
            OnboardingRoute.Account => new AccountStepPage(
                onAccountCompleted: () => this.OnStepCompletedAsync("account")),
            OnboardingRoute.ServiceSetup => new ServiceSetupPage(
                onSetupVerified: () => this.OnStepCompletedAsync("service")),
            OnboardingRoute.Demo => new DemoStepPage(
                uiChannel: null,
                onDemoCompleted: () => this.OnStepCompletedAsync("demo")),
            OnboardingRoute.Managed => new ManagedStepPage(
                monitor: null,
                onCompleted: () => this.OnStepCompletedAsync("managed")),
            _ => null,
        };
    }

    private async Task OnStepCompletedAsync(string stepId)
    {
        // Guarded coordinator path — the only place that drives the canonical
        // advance, routed through the IPC-backed VM. A page NEVER auto-records
        // completion (T26 design Â§3, ADR-002): the page exposes completion,
        // the shell translates it to the VM command, and the VM surfaces the
        // Service-acknowledged snapshot.
        _ = stepId;
        try
        {
            await this.viewModel.GoNextCommand.ExecuteAsync(null);
        }
        catch
        {
            // The VM already surfaces IPC failures through ErrorMessage — the
            // shell intentionally swallows here so a transient page error
            // does not crash the live shell.
        }
    }

    private void NavigateToTransparencyFromConsent()
    {
        // Transparency never mutates onboarding state (T26 design Â§6). The
        // shell mounts it directly so the consent page can be restored
        // without a Service round-trip.
        this.PageHost.Content = new TransparencyPage(
            onBackRequested: () => this.NavigateBackToConsent());
    }

    private void NavigateBackToConsent()
    {
        this.NavigateToCurrentRoute();
    }
}
