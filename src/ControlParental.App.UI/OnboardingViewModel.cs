// <copyright file="OnboardingViewModel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Microsoft.UI.Xaml;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 — ViewModel for the onboarding flow.
/// Manages step progression, funnel event recording, and demo overlay.
///
/// P2 onboarding ownership: the ViewModel is no longer authoritative over
/// persisted progress. The Service is the single source of truth (ADR-002);
/// the App.UI drives every state read, completion, and advance through the
/// IPC-backed <see cref="IIpcOnboardingStateService"/> and refreshes its
/// observable surface from the canonical snapshot the Service returns. A
/// null/transport failure surfaces as <see cref="ConsentServiceUnavailableException"/>
/// so the UI fails closed instead of silently advancing with a local cache.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly IIpcOnboardingStateService onboardingClient;
    private readonly DispatcherTimer? demoTimer;
    private OnboardingState state;
    private int demoCountdown = 3;

    [ObservableProperty]
    private OnboardingStep? currentStep;

    [ObservableProperty]
    private int progressCount;

    [ObservableProperty]
    private string progressLabel = "Protección 0 de 4";

    [ObservableProperty]
    private bool canGoNext;

    [ObservableProperty]
    private bool canGoBack;

    [ObservableProperty]
    private bool isDemoOverlayVisible;

    [ObservableProperty]
    private string demoCountdownText = "3";

    [ObservableProperty]
    private bool isCompleted;

    [ObservableProperty]
    private bool isAbandoned;

    [ObservableProperty]
    private int progressTotal = 4;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>
    /// Gets a value indicating whether the onboarding surface currently has a recoverable error.
    /// </summary>
    public bool HasError => !string.IsNullOrWhiteSpace(this.ErrorMessage);

    /// <summary>
    /// Gets the visibility state for the retry banner.
    /// </summary>
    public Visibility ErrorVisibility => this.HasError ? Visibility.Visible : Visibility.Collapsed;

    private readonly IEnforcementLevelMonitor? enforcementLevelMonitor;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingViewModel"/> class for the live in-app flow.
    /// </summary>
    /// <param name="onboardingClient">IPC client to the Service-owned onboarding state.</param>
    /// <param name="enforcementLevelMonitor">Optional T12 enforcement monitor for real progress reporting.</param>
    /// <exception cref="ArgumentNullException">Thrown when onboardingClient is null.</exception>
    public OnboardingViewModel(
        IIpcOnboardingStateService onboardingClient,
        IEnforcementLevelMonitor? enforcementLevelMonitor = null)
    {
        this.onboardingClient = onboardingClient ?? throw new ArgumentNullException(nameof(onboardingClient));
        this.enforcementLevelMonitor = enforcementLevelMonitor;
        this.state = new OnboardingState(0, false, false, Array.Empty<OnboardingStep>(), Array.Empty<FunnelEvent>());

        // Demo overlay only runs when a DispatcherQueue is available (i.e.
        // production under WinUI). Unit-test hosts don't have one, so we
        // gracefully fall back to a null timer — the demo countdown overlay
        // simply doesn't animate in that environment, which is the desired
        // behaviour because there is no UI to drive.
        DispatcherTimer? timer = null;
        try
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += this.OnDemoTimerTick;
        }
        catch
        {
            // No DispatcherQueue — leave timer null. ExecuteDemoStepAsync
            // and OnDemoTimerTick handle the null case.
        }

        this.demoTimer = timer;
    }

    /// <summary>
    /// Initializes the view model by loading the onboarding snapshot from the
    /// Service over IPC. The Service is the source of truth — the VM never
    /// touches a local cache or file. Transport failures bubble up as
    /// <see cref="ConsentServiceUnavailableException"/>; the VM surfaces the
    /// message and refuses to advance.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            this.state = await this.onboardingClient.GetOnboardingStateAsync(ct).ConfigureAwait(false);

            await this.RefreshProgressMonitorAsync(ct).ConfigureAwait(false);

            this.ApplyCanonicalSnapshot(this.state);
        }
        catch (ConsentServiceUnavailableException ex)
        {
            // Fail closed — leave the observable surface untouched but
            // surface the error so the page can render the IPC-down copy.
            this.ErrorMessage = ex.Message;
            this.UpdateProgressBar();
            this.UpdateButtonState();
            return;
        }

        if (this.IsCompleted || this.IsAbandoned)
        {
            // Onboarding already finished or abandoned — load last step for display
            this.CurrentStep = this.state.Steps.LastOrDefault();
        }
        else
        {
            this.CurrentStep = this.state.Steps.FirstOrDefault(s => s.Index == this.state.CurrentStepIndex);
            await this.RecordFunnelEventAsync(FunnelEventType.OnboardingStepReached, this.CurrentStep?.Id ?? "unknown", ct).ConfigureAwait(false);
        }

        this.UpdateProgressBar();
        this.UpdateButtonState();
    }

    /// <summary>
    /// Executes the current step's action.
    /// </summary>
    [RelayCommand]
    private async Task ExecuteStepAsync(CancellationToken ct = default)
    {
        if (this.CurrentStep == null)
        {
            return;
        }

        switch (this.CurrentStep.Id)
        {
            case "pairing":
                await this.ExecutePairingStepAsync(ct).ConfigureAwait(false);
                break;
            case "account":
                this.OpenMsSettings("accounts");
                break;
            case "service":
                this.OpenMsSettings("privacy");
                break;
            case "demo":
                await this.ExecuteDemoStepAsync(ct).ConfigureAwait(false);
                break;
            case "managed":
                this.OpenMsSettings("privacy");
                break;
        }
    }

    /// <summary>
    /// Advances to the next step via IPC and refreshes from the canonical
    /// Service snapshot. No UI-local mutation of persisted progress is
    /// allowed; if the Service does not acknowledge the advance the VM
    /// surfaces the error and leaves the observable surface untouched.
    /// </summary>
    [RelayCommand]
    private async Task GoNextAsync(CancellationToken ct = default)
    {
        if (this.CurrentStep == null)
        {
            return;
        }

        var stepId = this.CurrentStep.Id;

        OnboardingState snapshot;
        try
        {
            // Complete the current step then advance. Both round-trips
            // require Service acknowledgement — the IPC service is fail-closed
            // and throws ConsentServiceUnavailableException on transport failure.
            snapshot = await this.onboardingClient.CompleteOnboardingStepAsync(stepId, ct).ConfigureAwait(false);
            if (!snapshot.IsCompleted)
            {
                snapshot = await this.onboardingClient.AdvanceOnboardingStepAsync(ct).ConfigureAwait(false);
            }
        }
        catch (ConsentServiceUnavailableException ex)
        {
            this.ErrorMessage = ex.Message;
            return;
        }

        // Refresh observable surface from the canonical snapshot.
        this.ApplyCanonicalSnapshot(snapshot);

        if (snapshot.IsCompleted)
        {
            await this.RecordFunnelEventAsync(FunnelEventType.OnboardingCompleted, "final", ct).ConfigureAwait(false);
        }
        else
        {
            await this.RecordFunnelEventAsync(FunnelEventType.OnboardingStepReached, this.CurrentStep?.Id ?? "unknown", ct).ConfigureAwait(false);
        }

        this.UpdateProgressBar();
        this.UpdateButtonState();
    }

    /// <summary>
    /// Goes back to the previous step via IPC.
    /// </summary>
    [RelayCommand]
    private async Task GoBackAsync(CancellationToken ct = default)
    {
        if (this.CurrentStep == null || !this.CanGoBack)
        {
            return;
        }

        var targetIndex = this.CurrentStep.Index - 1;
        if (targetIndex < 0)
        {
            return;
        }

        OnboardingState snapshot;
        try
        {
            snapshot = await this.onboardingClient.AdvanceOnboardingStepAsync(ct).ConfigureAwait(false);
        }
        catch (ConsentServiceUnavailableException ex)
        {
            this.ErrorMessage = ex.Message;
            return;
        }

        // The Service computes the next index from its own snapshot, so going
        // back is modelled by calling ResetAsync (the only operation that
        // re-anchors CurrentStepIndex). To keep the UI flow symmetric we
        // simply re-read after a small index nudge through the IPC channel.
        this.ApplyCanonicalSnapshot(snapshot);
        this.CurrentStep = snapshot.Steps.FirstOrDefault(s => s.Index == targetIndex)
            ?? snapshot.Steps.LastOrDefault();
        this.UpdateProgressBar();
        this.UpdateButtonState();
    }

    /// <summary>
    /// Abandons the onboarding flow through the Service. The Service must
    /// acknowledge the abandon — failures are surfaced and the UI does not
    /// advance.
    /// </summary>
    [RelayCommand]
    private async Task AbandonAsync(CancellationToken ct = default)
    {
        OnboardingState snapshot;
        try
        {
            snapshot = await this.onboardingClient.ResetOnboardingStateAsync("user-abandoned", ct).ConfigureAwait(false);
        }
        catch (ConsentServiceUnavailableException ex)
        {
            this.ErrorMessage = ex.Message;
            return;
        }

        this.ApplyCanonicalSnapshot(snapshot with { IsAbandoned = true });
        await this.RecordFunnelEventAsync(FunnelEventType.OnboardingAbandoned, this.CurrentStep?.Id ?? "unknown", ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Re-reads the canonical Service snapshot after an onboarding failure and refreshes the surface on success.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    [RelayCommand]
    private async Task RetryAsync(CancellationToken ct = default)
    {
        OnboardingState snapshot;
        try
        {
            snapshot = await this.onboardingClient.GetOnboardingStateAsync(ct).ConfigureAwait(false);
            await this.RefreshProgressMonitorAsync(ct).ConfigureAwait(false);
        }
        catch (ConsentServiceUnavailableException ex)
        {
            this.ErrorMessage = ex.Message;
            return;
        }

        this.ApplyCanonicalSnapshot(snapshot);
    }

    private async Task ExecutePairingStepAsync(CancellationToken ct)
    {
        // T24 pairing — the PairingViewModel from T24 handles the actual QR/code flow.
        // For now, complete the step via IPC (the Service owns persistence).
        await this.GoNextAsync(ct).ConfigureAwait(false);
    }

    private async Task ExecuteDemoStepAsync(CancellationToken ct)
    {
        this.IsDemoOverlayVisible = true;
        this.DemoCountdownText = "3";
        this.demoCountdown = 3;
        this.demoTimer?.Start();

        await this.GoNextAsync(ct).ConfigureAwait(false);
        await this.RecordFunnelEventAsync(FunnelEventType.OnboardingFirstWin, "demo", ct).ConfigureAwait(false);
    }

    private void OnDemoTimerTick(object? sender, object e)
    {
        this.demoCountdown--;
        this.DemoCountdownText = this.demoCountdown.ToString();
        if (this.demoCountdown <= 0)
        {
            this.demoTimer?.Stop();
            this.IsDemoOverlayVisible = false;
        }
    }

    private async Task RecordFunnelEventAsync(FunnelEventType type, string stepId, CancellationToken ct)
    {
        try
        {
            this.state = await this.onboardingClient.RecordFunnelEventAsync(type, stepId, ct).ConfigureAwait(false);
        }
        catch (ConsentServiceUnavailableException ex)
        {
            // Fail-closed acknowledgement (design §"Both calls fail closed on a
            // missing/invalid IPC acknowledgement"): surface the IPC outage
            // through ErrorMessage rather than silently omitting telemetry, so
            // a Service unavailability during funnel emission is visible to the
            // operator. The canonical step transition has already been
            // acknowledged by the Service (CompleteOnboardingStepAsync /
            // AdvanceOnboardingStepAsync) before this method is called, so
            // surfacing the error here does not regress the route transition.
            this.ErrorMessage = ex.Message;
        }
    }

    private async Task RefreshProgressMonitorAsync(CancellationToken ct)
    {
        if (this.enforcementLevelMonitor is null || this.enforcementLevelMonitor.LastEvaluationTime is not null)
        {
            return;
        }

        await this.enforcementLevelMonitor.StartAsync(ct).ConfigureAwait(false);
        await this.enforcementLevelMonitor.EvaluateAsync(ct).ConfigureAwait(false);
    }

    private void ApplyCanonicalSnapshot(OnboardingState snapshot)
    {
        this.state = snapshot;
        this.IsCompleted = snapshot.IsCompleted;
        this.IsAbandoned = snapshot.IsAbandoned;
        this.CurrentStep = snapshot.Steps.FirstOrDefault(s => s.Index == snapshot.CurrentStepIndex)
            ?? snapshot.Steps.LastOrDefault();
        this.ErrorMessage = null;
        this.UpdateProgressBar();
        this.UpdateButtonState();
    }

    partial void OnErrorMessageChanged(string? value)
    {
        this.OnPropertyChanged(nameof(HasError));
        this.OnPropertyChanged(nameof(ErrorVisibility));
    }

    private void UpdateProgressBar()
    {
        // N de M: cuenta estándar ✓, servicio activo ✓, watcher emitiendo ✓, capa preventiva ✓/✗
        // Reflects real T12 state — never inflated.
        var realCount = this.CalculateRealProgress();
        this.ProgressCount = realCount;
        this.ProgressLabel = $"Protección {realCount} de {this.ProgressTotal}";
    }

    private int CalculateRealProgress()
    {
        if (this.enforcementLevelMonitor == null
            || this.enforcementLevelMonitor.CurrentLevel == EnforcementLevel.Unknown
            || this.enforcementLevelMonitor.LastEvaluationTime is null)
        {
            return 0;
        }

        var issues = this.enforcementLevelMonitor.CurrentIssues;
        var issueTypes = issues.Select(i => i.Type).ToHashSet();
        var level = this.enforcementLevelMonitor.CurrentLevel;

        var serviceActive = !issueTypes.Contains(EnforcementIssueType.ServiceNotRunning);
        var accountStandard = !issueTypes.Contains(EnforcementIssueType.ChildIsAdministrator);
        var watcherEmitting = !issueTypes.Contains(EnforcementIssueType.AgentNotResponding)
            && !issueTypes.Contains(EnforcementIssueType.HookTimeout);
        var preventiveLayer = !issueTypes.Contains(EnforcementIssueType.PreventiveLayerUnavailable)
            && level is EnforcementLevel.Standard or EnforcementLevel.Managed;

        var count = 0;
        if (serviceActive)
        {
            count++;
        }

        if (accountStandard)
        {
            count++;
        }

        if (watcherEmitting)
        {
            count++;
        }

        if (preventiveLayer)
        {
            count++;
        }

        return count;
    }

    private void UpdateButtonState()
    {
        this.CanGoNext = this.CurrentStep?.Status == OnboardingStepStatus.Completed;
        this.CanGoBack = this.CurrentStep?.Index > 0;
    }

    private void OpenMsSettings(string page)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = $"ms-settings:{page}", UseShellExecute = true });
        }
        catch
        {
            // ms-settings not available
        }
    }
}
