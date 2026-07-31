// <copyright file="Program.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlParental.Domain;
using ControlParental.SessionAgent.Interop;

/// <summary>
/// Entry point for the Session Agent.
/// Runs in the interactive session of the child.
/// </summary>
public static class Program
{
    private const string DefaultPipeName = "SessionAgent";

    public static async Task Main(string[] args)
    {
        // Parse command line arguments
        var pipeName = DefaultPipeName;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--pipe=", StringComparison.OrdinalIgnoreCase))
            {
                pipeName = arg.Substring("--pipe=".Length);
            }
        }

        var builder = Host.CreateApplicationBuilder(args);

        // T38: Register IPC channel
        builder.Services.AddSingleton<IIpcChannel>(_ => new NamedPipeClient(pipeName));

        // T08: Register overlay manager
        builder.Services.AddSingleton<IOverlayManager, OverlayManager>();

        // T05: Register foreground watcher (real implementation with SetWinEventHook)
        builder.Services.AddSingleton<IForegroundWatcher, ForegroundWatcher>();

        builder.Services.AddHostedService<SessionAgentHost>();

        var host = builder.Build();
        await host.RunAsync();
    }
}

/// <summary>
/// Hosted service for the Session Agent.
/// Connects to the Service via IPC and manages the overlay and foreground watcher.
/// </summary>
public sealed class SessionAgentHost : BackgroundService
{
    private readonly IIpcChannel ipcChannel;
    private readonly IOverlayManager overlayManager;
    private readonly IForegroundWatcher foregroundWatcher;
    private readonly Stopwatch upTimeStopwatch;

    public SessionAgentHost(
        IIpcChannel ipcChannel,
        IOverlayManager overlayManager,
        IForegroundWatcher foregroundWatcher)
    {
        this.ipcChannel = ipcChannel;
        this.overlayManager = overlayManager;
        this.foregroundWatcher = foregroundWatcher;
        this.upTimeStopwatch = Stopwatch.StartNew();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Connect to the service
        await this.ipcChannel.StartAsync(stoppingToken);

        // Subscribe to IPC messages
        this.ipcChannel.MessageReceived += this.OnMessageReceived;
        this.ipcChannel.Disconnected += this.OnDisconnected;

        // Subscribe to foreground changes
        this.foregroundWatcher.ForegroundChanged += this.OnForegroundChanged;

        // Start watching foreground
        await this.foregroundWatcher.StartAsync(stoppingToken);

        // Send initial heartbeat
        await this.SendHeartbeatAsync(stoppingToken);

        // Keep the agent alive
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            // Periodic heartbeat
            await this.SendHeartbeatAsync(stoppingToken);
        }
    }

    private async void OnForegroundChanged(string appId)
    {
        // Send foreground change to the service
        var message = new ForegroundChanged(appId);
        await this.ipcChannel.SendAsync(message, CancellationToken.None);
    }

    private void OnMessageReceived(IIpcMessage message)
    {
        switch (message)
        {
            case ShowOverlay overlay:
                this.overlayManager.ShowOverlay(overlay.Reason, overlay.CtaLabel);
                break;

            case HideOverlay:
                this.overlayManager.HideOverlay();
                break;

            case ShowWarning warning:
                this.overlayManager.ShowWarning(warning.MinutesRemaining);
                break;

            case LockWorkstation:
                this.DoLockWorkstation();
                break;

            case RequestStateSnapshot:
                _ = this.SendStateSnapshotAsync();
                break;

            case Ping:
                _ = this.ipcChannel.SendAsync(new Pong(), CancellationToken.None);
                break;

            default:
                Debug.WriteLine($"[SessionAgentHost] Unknown message: {message.MessageType}");
                break;
        }
    }

    private void OnDisconnected()
    {
        Debug.WriteLine("[SessionAgentHost] Disconnected from service.");
        // The host will stop when the cancellation token is triggered
    }

    private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        var heartbeat = new AgentHeartbeat(
            AgentId: Environment.MachineName,
            UpTimeMs: this.upTimeStopwatch.ElapsedMilliseconds,
            IsOverlayVisible: this.overlayManager.IsOverlayVisible);

        await this.ipcChannel.SendAsync(heartbeat, cancellationToken);
    }

    private async Task SendStateSnapshotAsync()
    {
        var snapshot = new StateSnapshot(
            AppId: this.foregroundWatcher.CurrentAppId ?? "unknown",
            IsOverlayVisible: this.overlayManager.IsOverlayVisible,
            UpTimeMs: this.upTimeStopwatch.ElapsedMilliseconds);

        await this.ipcChannel.SendAsync(snapshot, CancellationToken.None);
    }

    private void DoLockWorkstation()
    {
        // Lock the workstation using the Windows API
        // This is called when the service decides to lock the device
        if (LockWorkStationApi())
        {
            Debug.WriteLine("[SessionAgentHost] Workstation locked.");
        }
        else
        {
            Debug.WriteLine("[SessionAgentHost] Failed to lock workstation.");
        }
    }

    [DllImport("user32.dll")]
    private static extern bool LockWorkStationApi();

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.foregroundWatcher.Stop();
        this.ipcChannel.MessageReceived -= this.OnMessageReceived;
        this.ipcChannel.Disconnected -= this.OnDisconnected;
        await this.ipcChannel.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}
