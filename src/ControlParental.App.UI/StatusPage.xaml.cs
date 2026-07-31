// <copyright file="StatusPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

/// <summary>
/// T27 — Page showing the child's daily status: time remaining, current app,
/// active grants, and enforcement issues.
/// </summary>
public sealed partial class StatusPage : Page
{
    private readonly StatusViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusPage"/> class.
    /// </summary>
    public StatusPage()
    {
        this.viewModel = App.Services.GetRequiredService<StatusViewModel>();
        this.InitializeComponent();
        this.DataContext = this.viewModel;
        _ = this.viewModel.InitializeAsync();
    }
}
