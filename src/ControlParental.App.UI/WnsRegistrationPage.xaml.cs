// <copyright file="WnsRegistrationPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

/// <summary>Dedicated, secret-free WNS registration surface.</summary>
public sealed partial class WnsRegistrationPage : Page
{
    private readonly WnsRegistrationViewModel viewModel;
    private readonly CancellationTokenSource lifetimeCancellation = new();

    /// <summary>Initializes the page from the application composition root.</summary>
    public WnsRegistrationPage()
    {
        this.viewModel = App.Services.GetRequiredService<WnsRegistrationViewModel>();
        this.DataContext = this.viewModel;
        this.InitializeComponent();
        this.Unloaded += this.OnUnloaded;
    }

    private async void OnRegisterClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await this.viewModel.RegisterAsync(this.lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // Navigation or application shutdown cancelled the in-flight request.
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        this.lifetimeCancellation.Cancel();
        this.Unloaded -= this.OnUnloaded;
        this.lifetimeCancellation.Dispose();
    }
}
