// <copyright file="AccountStepPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.App.UI.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

/// <summary>
/// T26 — WinUI3 page for child account creation and conversion.
/// </summary>
public sealed partial class AccountStepPage : Page
{
    private readonly AccountStepViewModel viewModel;

    /// <summary>
    /// Gets the typed resource accessor exposed to XAML so the markup can
    /// resolve localized copy without literals (T26 Fase 8, ADR-007).
    /// </summary>
    public StringsAdapter Strings => StringsAdapter.Instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountStepPage"/> class.
    /// The parameterless WinUI constructor resolves the Service channel from DI.
    /// </summary>
    public AccountStepPage()
        : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountStepPage"/> class with a callback.
    /// </summary>
    /// <param name="onAccountCompleted">Callback when account step completes.</param>
    public AccountStepPage(Action onAccountCompleted)
        : this(onAccountCompleted, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountStepPage"/> class.
    /// Initializes a new instance with explicit onboarding callback and IPC channel.
    /// </summary>
    /// <param name="onAccountCompleted">Callback when account step completes.</param>
    /// <param name="uiChannel">Optional IPC channel to the Service.</param>
    public AccountStepPage(Action? onAccountCompleted, IUIChannel? uiChannel)
    {
        var resolvedChannel = uiChannel ?? TryResolveChannelFromDi();
        this.viewModel = new AccountStepViewModel(onAccountCompleted, resolvedChannel);
        this.InitializeComponent();
        this.DataContext = this.viewModel;
    }

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        // Get password from PasswordBox.
        this.viewModel.NewAccountPassword = this.NewPasswordBox.Password;
        await this.viewModel.CreateAccountCommand.ExecuteAsync(null).ConfigureAwait(false);
    }

    private void OnUseAccountClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is object account)
        {
            this.viewModel.UseAccountCommand.Execute(account);
        }
    }

    private void OnUseAccountRadioClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is object account)
        {
            this.viewModel.UseAccountCommand.Execute(account);
        }
    }

    private static IUIChannel? TryResolveChannelFromDi()
    {
        try
        {
            return App.Services.GetService<IUIChannel>();
        }
        catch
        {
            // The VM reports the unavailable-service state when DI is not ready.
            return null;
        }
    }
}
