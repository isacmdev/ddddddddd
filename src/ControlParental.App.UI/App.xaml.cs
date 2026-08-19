// <copyright file="App.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using Microsoft.Windows.ApplicationModel.WindowsAppRuntime;
using Microsoft.UI.Xaml;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 — WinUI 3 application entry point.
/// Composes the live, in-app onboarding flow against the IPC-backed
/// Service-owned state.
/// </summary>
public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ControlParental",
        "app_startup.log");

    private static IServiceProvider? serviceProvider;
    private static OnboardingViewModel? viewModel;
    private static Microsoft.UI.Xaml.Window? mainWindow;

    /// <summary>
    /// Gets the service provider for DI.
    /// </summary>
    public static IServiceProvider Services => serviceProvider!;

    private static void Log(string msg)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
            System.Diagnostics.Debug.WriteLine($"[APP] {msg}");
        }
        catch
        {
        }
    }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Log("OnLaunched started");

        try
        {
            Log("Calling DeploymentManager.Initialize()...");
            var initResult = DeploymentManager.Initialize();
            Log($"DeploymentManager result: {initResult}");
        }
        catch (Exception ex)
        {
            Log($"DeploymentManager EXCEPTION: {ex.GetType().Name}: {ex.Message}");
        }

        try
        {
            Log("Configuring DI...");
            var services = new ServiceCollection();
            ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();
            Log("DI OK");
        }
        catch (Exception ex)
        {
            Log($"DI EXCEPTION: {ex.Message}");
        }

        try
        {
            Log("Building OnboardingViewModel...");
            var onboardingClient = serviceProvider!.GetRequiredService<IIpcOnboardingStateService>();
            var monitor = serviceProvider!.GetService<IEnforcementLevelMonitor>();
            viewModel = new OnboardingViewModel(onboardingClient, monitor);
            Log("ViewModel OK");
        }
        catch (Exception ex)
        {
            Log($"ViewModel EXCEPTION: {ex.Message}");
        }

        try
        {
            Log("Creating MainWindow...");
            mainWindow = new MainWindow(viewModel!);
            Log("MainWindow created, activating...");
            mainWindow.Activate();
            Log("MainWindow activated OK");
        }
        catch (Exception ex)
        {
            Log($"MainWindow FAILED: {FormatMainWindowStartupException(ex)}");

            // T26 fallback: show a simple window so user sees something
            try
            {
                var fallback = new Microsoft.UI.Xaml.Window();
                fallback.Content = new Microsoft.UI.Xaml.Controls.TextBlock
                {
                    Text = $"Error cargando UI:\n{ex.Message}",
                    FontSize = 14,
                    Margin = new Microsoft.UI.Xaml.Thickness(20),
                };
                fallback.Activate();
                mainWindow = fallback;
                Log("Fallback window shown");
            }
            catch (Exception ex2)
            {
                Log($"Fallback FAILED: {ex2.Message}");
            }
        }

    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // T26 PR #4 / Unit 2 — IPC channel is the only path the App.UI uses to
        // talk to the Service. Every state read, completion, advance, and funnel
        // event flows through this surface so the Service is the single source
        // of truth (ADR-002).
        services.AddSingleton<IUIChannel, NamedPipeUIChannel>();
        services.AddSingleton<IIpcOnboardingStateService, IpcOnboardingStateService>();

        // T26 PR #14 / Unit 2 — IPC-backed consent service. The App.UI never
        // writes a local consent cache; the Service persists the canonical
        // snapshot and fail-closes when the IPC acknowledgement is missing.
        services.AddSingleton<IConsentService, IpcConsentService>();

        // T26 PR #10 — IPC-backed enforcement monitor. The App.UI no longer
        // owns a local stub; the canonical snapshot comes from the Service over
        // the same UI pipe.
        services.AddSingleton<IEnforcementLevelMonitor, ServiceEnforcementLevelMonitor>();
        services.AddSingleton<IWnsRegistrationPort, WnsPushNotificationHandler>();
        services.AddSingleton<WnsPushNotificationHandler>(serviceProvider =>
            (WnsPushNotificationHandler)serviceProvider.GetRequiredService<IWnsRegistrationPort>());
        services.AddSingleton<IWnsChannelProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<WnsPushNotificationHandler>());
        services.AddSingleton<WnsRegistrationViewModel>();
        services.AddTransient<WnsRegistrationPage>();
    }

    private static string FormatMainWindowStartupException(Exception exception)
        => FormatStartupException(exception, "XamlFile=MainWindow.xaml TargetType=MainWindow ResourceKey=PageHost");

    private static string FormatStartupException(Exception exception, string? xamlContext = null)
    {
        var details = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(xamlContext))
        {
            details.Append(xamlContext);
            details.Append(' ');
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (details.Length > 0)
            {
                details.Append(" | Inner: ");
            }

            details.Append(current.GetType().Name);
            details.Append(": ");
            details.Append(current.Message);
            details.Append(" HResult=0x");
            details.Append(current.HResult.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));

            AppendXamlLocation(details, current);
        }

        return details.ToString();
    }

    private static void AppendXamlLocation(System.Text.StringBuilder details, Exception exception)
    {
        var type = exception.GetType();
        foreach (var propertyName in new[] { "LineNumber", "LinePosition" })
        {
            var property = type.GetProperty(propertyName);
            if (property?.GetValue(exception) is int value && value >= 0)
            {
                details.Append(' ');
                details.Append(propertyName);
                details.Append('=');
                details.Append(value);
            }
        }
    }
}
