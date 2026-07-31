// <copyright file="TransparencyPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using Microsoft.UI.Xaml.Controls;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T25 — WinUI3 page showing what is monitored (transparency).
/// Always accessible to the child and never mutates onboarding state.
/// </summary>
public sealed partial class TransparencyPage : Page
{
    private readonly Action? onBackRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransparencyPage"/> class for standalone navigation.
    /// </summary>
    public TransparencyPage()
        : this(null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransparencyPage"/> class with a shell-owned return action.
    /// </summary>
    /// <param name="onBackRequested">Callback that restores the prior shell route.</param>
    public TransparencyPage(Action? onBackRequested)
    {
        this.onBackRequested = onBackRequested;
        this.InitializeComponent();
    }

    private void OnBackClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (this.onBackRequested is not null)
        {
            this.onBackRequested.Invoke();
        }
        else if (this.Frame?.CanGoBack == true)
        {
            this.Frame.GoBack();
        }
    }
}
