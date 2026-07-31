// <copyright file="App.xaml.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
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
            Log($"MainWindow FAILED: {ex.GetType().Name}: {ex.Message}");

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

        // T26: WinUI 3 unpackaged needs a Win32 message pump on the UI thread so
        // OnLaunched returning doesn't terminate the process. PeekMessage doesn't
        // block, allowing the XAML DispatcherQueue to also process.
        RunMessageLoop();
    }

    [DllImport("user32.dll")]
    private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    private static void RunMessageLoop()
    {
        Log("Starting message loop");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mainWindow);
        var msg = new MSG();

        // GetMessage blocks — this is the correct pattern for WinUI 3 unpackaged.
        // The blocking loop keeps the UI thread alive so the XAML DispatcherQueue
        // can process render and input messages.
        while (GetMessage(out msg, hwnd, 0, 0) != 0)
        {
            if (msg.message == 0x0012) // WM_QUIT
            {
                break;
            }

            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        Log("Message loop exited");
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
    }
}
