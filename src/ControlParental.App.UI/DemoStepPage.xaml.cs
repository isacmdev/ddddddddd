// <copyright file="DemoStepPage.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.App.UI.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

/// <summary>
/// T26 — WinUI3 page for the demo overlay step.
/// </summary>
public sealed partial class DemoStepPage : Page
{
    private readonly DemoStepViewModel viewModel;

    /// <summary>
    /// Gets the typed resource accessor exposed to XAML so the markup can
    /// resolve localized copy without literals (T26 Fase 8, ADR-007).
    /// </summary>
    public StringsAdapter Strings => StringsAdapter.Instance;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoStepPage"/> class.
    /// WinUI navigation uses this constructor, so the live IPC channel is
    /// resolved from the application DI container.
    /// </summary>
    public DemoStepPage()
        : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoStepPage"/> class with explicit live-shell dependencies.
    /// </summary>
    /// <param name="uiChannel">IPC channel for sending demo commands.</param>
    /// <param name="onDemoCompleted">Callback owned by the route coordinator.</param>
    public DemoStepPage(IUIChannel? uiChannel, Action? onDemoCompleted)
    {
        var resolvedChannel = uiChannel ?? TryResolveChannelFromDi();
        this.viewModel = new DemoStepViewModel(resolvedChannel, onDemoCompleted);
        this.InitializeComponent();
        this.DataContext = this.viewModel;
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
