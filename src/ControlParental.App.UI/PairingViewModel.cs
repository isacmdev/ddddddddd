// <copyright file="PairingViewModel.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 PR #5 (Fase 3: real pairing) — ViewModel for the pairing step.
/// The child enters the 8-character code provided by the parent and selects
/// their age band. Pairing is handled exclusively through
/// <see cref="IUIChannel"/> IPC — there is no local simulation path
/// (T26 audit finding #5, ADR-002).
/// </summary>
public sealed partial class PairingViewModel : ObservableObject
{
    /// <summary>
    /// Canonical age-band wire values. ADR-004 — the IPC payload MUST carry
    /// these exact strings, NOT the localized "7-12 años" copy the user sees
    /// in the ComboBox. The XAML stores the wire value in
    /// <c>ComboBoxItem.Tag</c> so the value sent on the wire is independent
    /// of the display copy.
    /// </summary>
    public const string AgeBandWire712 = "7-12";

    /// <summary>
    /// ADR-004 — see <see cref="AgeBandWire712"/>.
    /// </summary>
    public const string AgeBandWire1316 = "13-16";

    /// <summary>
    /// ADR-004 — see <see cref="AgeBandWire712"/>.
    /// </summary>
    public const string AgeBandWire1718 = "17-18";

    /// <summary>
    /// T26 PR #5 wire-format error catalogue (T26 design §6, Fase 3).
    /// The VM renders these strings directly so the wire-format
    /// <see cref="PairingStatus"/> maps deterministically to UI copy and the
    /// child never sees a backend-supplied error string.
    /// </summary>
    public const string PairingErrorInvalidCode = "Ese código no es válido. Pedile uno nuevo a tu tutor";

    /// <summary>
    /// T26 PR #5 — error copy for an expired pairing code (HTTP 410 / Gone).
    /// </summary>
    public const string PairingErrorExpiredCode = "Ese código ya expiró. Pedile uno nuevo";

    /// <summary>
    /// T26 PR #5 — error copy for the soft rate-limit path (HTTP 429).
    /// </summary>
    public const string PairingErrorTooManyRequests = "Probá de nuevo en un ratito";

    /// <summary>
    /// T26 PR #5 — fallback error copy when the backend reports a status we
    /// don't explicitly handle (e.g. <see cref="PairingStatus.Error"/>) and
    /// does not provide a usable <c>ErrorMessage</c>.
    /// </summary>
    public const string PairingErrorFallback = "No pudimos emparejar. Pedile ayuda a tu tutor.";

    /// <summary>
    /// T26 PR #5 — error copy when the IPC channel is missing or the Service
    /// returned no response (timeout, deserialization failure, broken pipe).
    /// </summary>
    public const string PairingErrorIpcUnavailable = "No pudimos conectar con la protección. Pedile a tu tutor que revise la instalación.";

    /// <summary>
    /// T26 PR #5 — copy surfaced when the user taps Emparejar before choosing
    /// an age band. The button should already be disabled in this state, but
    /// the VM keeps a defence-in-depth check so a programmatic invocation
    /// cannot reach the IPC.
    /// </summary>
    public const string PairingErrorAgeBandRequired = "Seleccioná tu edad antes de emparejar.";

    private const int CodeLength = 8;

    private readonly Action? onPairingCompleted;
    private readonly IUIChannel? uiChannel;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private bool isPairing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private string codeDigit1 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private string codeDigit2 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private string codeDigit3 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private string codeDigit4 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private string codeDigit5 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private string codeDigit6 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private string codeDigit7 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair))]
    private string codeDigit8 = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanPair), nameof(SelectedAgeBandWire))]
    private int selectedAgeBandIndex = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PairingViewModel"/> class.
    /// </summary>
    /// <param name="onPairingCompleted">Callback invoked when pairing succeeds.</param>
    /// <param name="uiChannel">
    /// IPC channel to the Service. When <c>null</c> the VM surfaces the
    /// fail-closed <see cref="PairingErrorIpcUnavailable"/> copy rather than
    /// falling back to a <c>Task.Delay</c> simulation — there is no
    /// development mode pairing path (T26 PR #5, ADR-002, audit finding #5).
    /// </param>
    public PairingViewModel(Action? onPairingCompleted = null, IUIChannel? uiChannel = null)
    {
        this.onPairingCompleted = onPairingCompleted;
        this.uiChannel = uiChannel;
    }

    /// <summary>
    /// Gets wire-format age band derived from the ComboBox selection index.
    /// ADR-004 — returns the canonical "7-12" / "13-16" / "17-18" string
    /// without the "años" suffix the XAML displays. Returns <c>null</c> when
    /// the user has not yet picked a band so the VM raises an explicit
    /// "Seleccioná tu edad" error rather than silently defaulting (the
    /// "may be inflated" lie the design forbids).
    /// </summary>
    public string? SelectedAgeBandWire => this.SelectedAgeBandIndex switch
    {
        0 => AgeBandWire712,
        1 => AgeBandWire1316,
        2 => AgeBandWire1718,
        _ => null,
    };

    /// <summary>
    /// Gets a value indicating whether true only when the user has filled all 8 digits, selected an age band
    /// AND no pairing is currently in flight. Bound to the Emparejar button's
    /// <c>IsEnabled</c> so the UI prevents submitting an incomplete payload
    /// (T26 PR #5 spec scenarios "Age band not selected").
    /// </summary>
    public bool CanPair =>
        !this.IsPairing
        && this.SelectedAgeBandIndex >= 0
        && !string.IsNullOrEmpty(this.CodeDigit1)
        && !string.IsNullOrEmpty(this.CodeDigit2)
        && !string.IsNullOrEmpty(this.CodeDigit3)
        && !string.IsNullOrEmpty(this.CodeDigit4)
        && !string.IsNullOrEmpty(this.CodeDigit5)
        && !string.IsNullOrEmpty(this.CodeDigit6)
        && !string.IsNullOrEmpty(this.CodeDigit7)
        && !string.IsNullOrEmpty(this.CodeDigit8);

    /// <summary>
    /// Attempts to pair using the entered 8-character code. Routes through
    /// the IPC channel exclusively — no <c>Task.Delay</c> fallback
    /// (T26 audit finding #5).
    /// </summary>
    [RelayCommand]
    private async Task PairWithCodeAsync(CancellationToken ct = default)
    {
        this.HasError = false;
        this.ErrorMessage = string.Empty;
        this.IsPairing = true;

        try
        {
            // Defence-in-depth — the Emparejar button is gated by CanPair,
            // but a programmatic invocation must NOT silently send IPC
            // with no age band. Surface the user-facing copy and bail out.
            if (this.SelectedAgeBandWire is null)
            {
                this.HasError = true;
                this.ErrorMessage = PairingErrorAgeBandRequired;
                return;
            }

            var fullCode =
                this.CodeDigit1
                + this.CodeDigit2
                + this.CodeDigit3
                + this.CodeDigit4
                + this.CodeDigit5
                + this.CodeDigit6
                + this.CodeDigit7
                + this.CodeDigit8;

            if (fullCode.Length != CodeLength)
            {
                this.HasError = true;
                this.ErrorMessage = "Ingresá los 8 caracteres del código.";
                return;
            }

            if (!fullCode.All(c => char.IsLetterOrDigit(c)))
            {
                this.HasError = true;
                this.ErrorMessage = "El código debe ser alfanumérico.";
                return;
            }

            if (this.uiChannel is null)
            {
                // T26 PR #5 — fail-closed. The PR #4 architectural flip
                // established that every state mutation MUST travel through
                // the Service. A null channel in production means the
                // Service is unregistered — surface the failure to the user
                // rather than fall through to a Task.Delay fake success.
                this.HasError = true;
                this.ErrorMessage = PairingErrorIpcUnavailable;
                return;
            }

            var response = await this.uiChannel
                .QueryAsync<PairDevice, PairDeviceResponse>(
                    new PairDevice(fullCode, this.SelectedAgeBandWire),
                    ct)
                .ConfigureAwait(false);

            if (response is null)
            {
                this.HasError = true;
                this.ErrorMessage = PairingErrorIpcUnavailable;
                return;
            }

            if (response.Success && response.Status == PairingStatus.Success)
            {
                this.onPairingCompleted?.Invoke();
                return;
            }

            // Status mapping per T26 §6 (Fase 3) catalogue. The backend
            // status drives the copy; the backend's free-form ErrorMessage
            // is only used as a last-resort fallback so we keep the wire
            // vocabulary honest without exposing implementation details.
            this.HasError = true;
            this.ErrorMessage = response.Status switch
            {
                PairingStatus.InvalidCode => PairingErrorInvalidCode,
                PairingStatus.ExpiredCode => PairingErrorExpiredCode,
                PairingStatus.TooManyRequests => PairingErrorTooManyRequests,
                _ => string.IsNullOrWhiteSpace(response.ErrorMessage)
                    ? PairingErrorFallback
                    : response.ErrorMessage!,
            };
        }
        catch (OperationCanceledException)
        {
            // Cancellation — leave state untouched so the user can retry.
        }
        catch (Exception ex)
        {
            this.HasError = true;
            this.ErrorMessage = $"Error: {ex.Message}";
        }
        finally
        {
            this.IsPairing = false;
        }
    }
}
