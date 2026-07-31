// <copyright file="PairingPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.App.UI.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

/// <summary>
/// T26 — WinUI3 page for device pairing.
/// The child enters the 8-character code provided by the parent and selects their age band.
/// </summary>
public sealed partial class PairingPage : Page
{
    private readonly PairingViewModel viewModel;

    /// <summary>
    /// Gets the typed resource accessor exposed to XAML so the markup can
    /// resolve localized copy without literals (T26 Fase 8, ADR-007).
    /// </summary>
    public StringsAdapter Strings => StringsAdapter.Instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="PairingPage"/> class.
    /// WinUI3's <c>Frame.Navigate(Type)</c> only invokes the parameterless
    /// constructor, so this entry point resolves the IPC channel from the
    /// application's service provider (T26 PR #5, ADR-002). When the DI
    /// container is not initialised (e.g. in unit-test scenarios that
    /// exercise the view-model directly) the channel may be <c>null</c> and
    /// the VM will surface the fail-closed
    /// <see cref="PairingViewModel.PairingErrorIpcUnavailable"/> copy rather
    /// than silently simulate success.
    /// </summary>
    public PairingPage()
        : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PairingPage"/> class with a callback and IPC channel.
    /// </summary>
    /// <param name="onPairingCompleted">Callback when pairing succeeds.</param>
    /// <param name="uiChannel">Optional IPC channel to the Service.</param>
    public PairingPage(Action? onPairingCompleted, IUIChannel? uiChannel)
    {
        IUIChannel? resolvedChannel = uiChannel;

        if (resolvedChannel is null)
        {
            // T26 PR #5: PairingViewModel used to receive a hard-coded `null`
            // channel here. After the Fase 3 architectural flip the page MUST
            // resolve the channel from DI so the live flow never runs with
            // a simulated 500 ms delay. Catch the case where the app's
            // service provider is not initialised (very early startup) by
            // passing through the null and letting the VM fail-closed.
            try
            {
                resolvedChannel = App.Services.GetService<IUIChannel>();
            }
            catch
            {
                resolvedChannel = null;
            }
        }

        this.viewModel = new PairingViewModel(onPairingCompleted, resolvedChannel);
        this.InitializeComponent();
        this.DataContext = this.viewModel;
    }

    private void OnDigitTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Text.Length == 1)
        {
            // Auto-advance to next field
            switch (textBox.Name)
            {
                case "Digit1":
                    this.Digit2.Focus(FocusState.Programmatic);
                    break;
                case "Digit2":
                    this.Digit3.Focus(FocusState.Programmatic);
                    break;
                case "Digit3":
                    this.Digit4.Focus(FocusState.Programmatic);
                    break;
                case "Digit4":
                    this.Digit5.Focus(FocusState.Programmatic);
                    break;
                case "Digit5":
                    this.Digit6.Focus(FocusState.Programmatic);
                    break;
                case "Digit6":
                    this.Digit7.Focus(FocusState.Programmatic);
                    break;
                case "Digit7":
                    this.Digit8.Focus(FocusState.Programmatic);
                    break;
            }
        }

        // Update ViewModel
        this.viewModel.CodeDigit1 = this.Digit1.Text;
        this.viewModel.CodeDigit2 = this.Digit2.Text;
        this.viewModel.CodeDigit3 = this.Digit3.Text;
        this.viewModel.CodeDigit4 = this.Digit4.Text;
        this.viewModel.CodeDigit5 = this.Digit5.Text;
        this.viewModel.CodeDigit6 = this.Digit6.Text;
        this.viewModel.CodeDigit7 = this.Digit7.Text;
        this.viewModel.CodeDigit8 = this.Digit8.Text;
    }

    private async void OnPairClick(object sender, RoutedEventArgs e)
    {
        await this.viewModel.PairWithCodeCommand.ExecuteAsync(null).ConfigureAwait(false);
    }
}
