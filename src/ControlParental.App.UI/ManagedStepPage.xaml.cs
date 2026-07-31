// <copyright file="ManagedStepPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 PR #6 (Fase 4: managed opt-in) — WinUI 3 page for the final
/// onboarding step. The page surfaces the current
/// <see cref="EnforcementLevel"/> and an opt-in card for the MANAGED
/// enforcement mode. The actual WDAC/AppLocker provisioning is out of
/// scope here (T31); this PR only adds the surface area.
/// </summary>
public sealed partial class ManagedStepPage : Page
{
    private readonly ManagedStepViewModel viewModel;

    /// <summary>
    /// Gets the typed resource accessor exposed to XAML so the markup can
    /// resolve localized copy without literals (T26 Fase 8, ADR-007).
    /// </summary>
    public StringsAdapter Strings => StringsAdapter.Instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedStepPage"/> class.
    /// WinUI 3's <c>Frame.Navigate(Type)</c> only invokes the parameterless
    /// constructor, so this entry point resolves the enforcement monitor
    /// from the application's service provider (same pattern as
    /// <see cref="PairingPage"/> and <see cref="ConsentPage"/>).
    /// </summary>
    public ManagedStepPage()
        : this(
            monitor: null,
            onCompleted: null,
            currentEditionSupportsManaged: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedStepPage"/> class
    /// with explicit dependencies. The parameterized constructor is the
    /// primary surface for unit tests; in production <see cref="MainWindow"/>
    /// resolves the monitor from DI and forwards the onCompleted callback so
    /// the canonical state advances through IPC.
    /// </summary>
    /// <param name="monitor">
    /// Optional enforcement monitor. When <c>null</c> the page falls back to
    /// <see cref="App.Services"/> (production) or to a stub that returns
    /// <see cref="EnforcementLevel.Unknown"/> (when DI is unavailable, e.g.
    /// very early startup or test hosts).
    /// </param>
    /// <param name="onCompleted">
    /// Optional completion callback. <c>MainWindow</c> wires this to
    /// <c>OnStepCompletedAsync("managed")</c> so the canonical state machine
    /// advances through IPC, never locally.
    /// </param>
    /// <param name="currentEditionSupportsManaged">
    /// Whether the running Windows edition permits MANAGED enforcement
    /// (Pro+/Enterprise/Education). Defaults to <c>true</c> so the upgrade
    /// card is offered on every edition; the Service can reject the actual
    /// activation on a future IPC round-trip.
    /// </param>
    public ManagedStepPage(
        IEnforcementLevelMonitor? monitor,
        Action? onCompleted,
        bool currentEditionSupportsManaged = true)
    {
        IEnforcementLevelMonitor resolvedMonitor = monitor
            ?? TryResolveMonitorFromDi()
            ?? new StubEnforcementLevelMonitor();

        Action resolvedCallback = onCompleted ?? (static () => { });

        this.viewModel = new ManagedStepViewModel(
            resolvedMonitor,
            resolvedCallback,
            currentEditionSupportsManaged);

        this.InitializeComponent();
        this.DataContext = this.viewModel;
        this.viewModel.LoadStateAsync();
    }

    private void OnActivateManagedClick(object sender, RoutedEventArgs e)
    {
        // Delegate to the ViewModel's RelayCommand so the click handler
        // and any keyboard accelerators route through one method.
        this.viewModel.FinishCommand.Execute(null);
    }

    private void OnFinishClick(object sender, RoutedEventArgs e)
    {
        this.viewModel.FinishCommand.Execute(null);
    }

    private static IEnforcementLevelMonitor? TryResolveMonitorFromDi()
    {
        try
        {
            return App.Services.GetService<IEnforcementLevelMonitor>();
        }
        catch
        {
            // App.Services may not be initialised in some test hosts.
            return null;
        }
    }

    /// <summary>
    /// Last-resort fallback used when DI is not available (e.g. very early
    /// startup). Reports <see cref="EnforcementLevel.Unknown"/> so the page
    /// surfaces the neutral "Estado desconocido" copy. The real
    /// <see cref="IEnforcementLevelMonitor"/> resolves the level from the
    /// Service over IPC.
    /// </summary>
    private sealed class StubEnforcementLevelMonitor : IEnforcementLevelMonitor
    {
        public EnforcementLevel CurrentLevel => EnforcementLevel.Unknown;

        public bool IsCritical => false;

        public IReadOnlyList<EnforcementIssue> CurrentIssues => Array.Empty<EnforcementIssue>();

        public DateTimeOffset? LastEvaluationTime => null;

        public event EventHandler<EnforcementLevelChangedEventArgs>? LevelChanged;

        public event EventHandler<EnforcementIssueDetectedEventArgs>? IssueDetected;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public Task EvaluateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void RecordAgentAlive()
        {
        }

        public void RecordAgentHeartbeat()
        {
        }

        public void RecordForegroundChange()
        {
        }

        public void AddIssue(EnforcementIssueType type, EnforcementIssueSeverity severity, string description)
        {
        }
    }
}
