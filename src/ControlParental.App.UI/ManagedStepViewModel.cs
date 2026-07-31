// <copyright file="ManagedStepViewModel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 PR #6 (Fase 4: managed opt-in) — ViewModel for the final onboarding
/// step where the parent chooses whether to upgrade the system to MANAGED
/// enforcement (WDAC/AppLocker) or finish with STANDARD.
///
/// T31 (full WDAC/AppLocker provisioning) is explicitly out of scope for this
/// PR — the page presents the opt-in affordance and a "Finalizar onboarding"
/// completion path; the actual provisioning change belongs to T31. The
/// "Activar modo reforzado" button only advances the onboarding step and lets
/// the Service validate the activation on a future IPC round-trip
/// (design §8 ADR-008 — Service-driven elevation; App.UI must NOT call
/// <c>Process.Start</c> from a standard user session).
/// </summary>
public sealed partial class ManagedStepViewModel : ObservableObject
{
    /// <summary>
    /// Copy surfaced when the Windows edition does not allow MANAGED
    /// enforcement (Home edition). The button is "Finalizar onboarding" so the
    /// user can close out the flow without blocking on the unavailable upgrade.
    /// </summary>
    public const string NotAvailableOnThisEditionCopy =
        "Modo reforzado no disponible en esta edición";

    /// <summary>
    /// Standard edition copy when MANAGED is offered but not yet active.
    /// </summary>
    public const string DescriptionStandardCopy =
        "En esta edición de Windows podés sumar la capa preventiva (WDAC).";

    /// <summary>
    /// Copy when the Service has not yet answered <c>GetEnforcementLevel</c>.
    /// </summary>
    public const string UnknownStateCopy = "Estado desconocido";

    private readonly IEnforcementLevelMonitor monitor;
    private readonly Action onCompleted;

    [ObservableProperty]
    private EnforcementLevel currentEnforcementLevel = EnforcementLevel.Unknown;

    [ObservableProperty]
    private string currentEnforcementLevelDisplay = UnknownStateCopy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManagedAlready))]
    private bool isOptedInToManaged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedStepViewModel"/> class.
    /// </summary>
    /// <param name="monitor">
    /// Real T12 enforcement monitor used to read the current
    /// <see cref="EnforcementLevel"/>. In production this is the
    /// IPC-backed implementation from the Service. In tests, a fake.
    /// </param>
    /// <param name="onCompleted">
    /// Callback invoked when the user finishes the step. <c>MainWindow</c>
    /// wires this to <c>OnStepCompletedAsync("managed")</c> so the canonical
    /// state advances through IPC, never locally.
    /// </param>
    /// <param name="currentEditionSupportsManaged">
    /// Whether the running Windows edition permits MANAGED enforcement
    /// (Pro+/Enterprise/Education). The production wiring resolves the
    /// edition probe at startup; if no probe is provided the VM defaults to
    /// <c>true</c> (offer the upgrade) and lets the Service reject the
    /// activation on a future IPC round-trip — per the orchestrator's
    /// fail-open guidance on edition detection.
    /// </param>
    public ManagedStepViewModel(
        IEnforcementLevelMonitor monitor,
        Action onCompleted,
        bool currentEditionSupportsManaged = true)
    {
        this.monitor = monitor ?? throw new System.ArgumentNullException(nameof(monitor));
        this.onCompleted = onCompleted ?? throw new System.ArgumentNullException(nameof(onCompleted));
        this.CurrentEditionSupportsManaged = currentEditionSupportsManaged;
    }

    /// <summary>
    /// Gets a value indicating whether true when the running Windows edition allows MANAGED. False on Home.
    /// Exposed as a property so the XAML can bind the upgrade-card visibility
    /// without embedding business logic in markup.
    /// </summary>
    public bool CurrentEditionSupportsManaged { get; }

    /// <summary>
    /// Gets a value indicating whether true when the user has already activated MANAGED. Hides the upgrade
    /// button and shows the "Capa preventiva activa" success copy.
    /// </summary>
    public bool IsManagedAlready =>
        this.CurrentEnforcementLevel == EnforcementLevel.Managed || this.IsOptedInToManaged;

    /// <summary>
    /// Reads the current enforcement level from the injected monitor and
    /// refreshes the derived display string. The Service polls asynchronously
    /// so callers should re-invoke this on every <c>LevelChanged</c> event; in
    /// practice the page calls it once on load.
    /// </summary>
    public void LoadStateAsync()
    {
        this.CurrentEnforcementLevel = this.monitor.CurrentLevel;
        this.CurrentEnforcementLevelDisplay = this.CurrentEnforcementLevel switch
        {
            EnforcementLevel.Managed => "MANAGED — capa preventiva activa",
            EnforcementLevel.Standard => "STANDARD — capa preventiva opcional",
            EnforcementLevel.Degraded => "DEGRADED — reparar primero",
            _ => UnknownStateCopy,
        };
    }

    /// <summary>
    /// Completes the step. Wired to both the "Activar modo reforzado" and
    /// "Finalizar onboarding" buttons. The callback
    /// (<c>MainWindow.OnStepCompletedAsync("managed")</c>) is the only path
    /// that advances the canonical onboarding state — the VM never writes
    /// state itself (design §3 ADR-002).
    /// </summary>
    [RelayCommand]
    private void Finish()
    {
        // Mark the local opt-in flag so the UI can immediately flip the
        // upgrade card into the "MANAGED — activa" success state without
        // waiting for the next IPC poll. The authoritative state lives in
        // the Service; this is a render-only affordance for the moment
        // between user click and the next LevelChanged event.
        if (this.CurrentEditionSupportsManaged
            && this.CurrentEnforcementLevel != EnforcementLevel.Managed)
        {
            this.IsOptedInToManaged = true;
        }

        this.onCompleted.Invoke();
    }
}
