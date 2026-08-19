// <copyright file="Program.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;
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
    private readonly Func<bool> lockWorkstation;
    private readonly Stopwatch upTimeStopwatch;
    private readonly Channel<Func<CancellationToken, ValueTask>> work =
        Channel.CreateUnbounded<Func<CancellationToken, ValueTask>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private int sessionId = -1;
    private long generation = -1;

    public SessionAgentHost(
        IIpcChannel ipcChannel,
        IOverlayManager overlayManager,
        IForegroundWatcher foregroundWatcher)
        : this(ipcChannel, overlayManager, foregroundWatcher, LockWorkStationApi)
    {
    }

    internal SessionAgentHost(
        IIpcChannel ipcChannel,
        IOverlayManager overlayManager,
        IForegroundWatcher foregroundWatcher,
        Func<bool> lockWorkstation)
    {
        this.ipcChannel = ipcChannel;
        this.overlayManager = overlayManager;
        this.foregroundWatcher = foregroundWatcher;
        this.lockWorkstation = lockWorkstation ?? throw new ArgumentNullException(nameof(lockWorkstation));
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

        while (!stoppingToken.IsCancellationRequested)
        {
            var ready = this.work.Reader.WaitToReadAsync(stoppingToken).AsTask();
            var heartbeatDue = Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            if (await Task.WhenAny(ready, heartbeatDue) == heartbeatDue)
            {
                await this.SendHeartbeatAsync(stoppingToken);
                continue;
            }

            while (this.work.Reader.TryRead(out var next))
            {
                await next(stoppingToken);
            }
        }
    }

    private void OnForegroundChanged(string appId)
    {
        var target = this.foregroundWatcher.GetCurrentTarget(appId);
        this.work.Writer.TryWrite(token => this.ForwardForegroundChangedAsync(appId, target, token));
    }

    private async ValueTask ForwardForegroundChangedAsync(
        string appId,
        ObservedProcessTarget? target,
        CancellationToken cancellationToken)
    {
        try
        {
            // Send foreground change to the service
            await this.ipcChannel.SendAsync(new ForegroundChanged(appId, target), cancellationToken);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionAgentHost] Failed to forward foreground change: {ex.Message}");
        }
    }

    private void OnMessageReceived(IIpcMessage message)
    {
        this.work.Writer.TryWrite(token => this.HandleMessageAsync(message, token));
    }

    private async ValueTask HandleMessageAsync(IIpcMessage message, CancellationToken cancellationToken)
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

            case AgentAuthority authority:
                this.sessionId = authority.SessionId;
                this.generation = authority.ConnectionGeneration;
                await this.SendHeartbeatAsync(cancellationToken);
                break;

            case AgentCommandRequest request:
                await this.ExecuteCommandAsync(request.Envelope, cancellationToken);
                break;

            case RequestStateSnapshot:
                await this.SendStateSnapshotAsync(cancellationToken);
                break;

            case Ping:
                await this.ipcChannel.SendAsync(new Pong(), cancellationToken);
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
            IsOverlayVisible: this.overlayManager.IsOverlayVisible,
            SessionId: this.sessionId,
            ConnectionGeneration: this.generation);

        await this.ipcChannel.SendAsync(heartbeat, cancellationToken);
    }

    private async Task SendStateSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = new StateSnapshot(
            AppId: this.foregroundWatcher.CurrentAppId ?? "unknown",
            IsOverlayVisible: this.overlayManager.IsOverlayVisible,
            UpTimeMs: this.upTimeStopwatch.ElapsedMilliseconds);

        await this.ipcChannel.SendAsync(snapshot, cancellationToken);
    }

    private NativeActionOutcome DoLockWorkstation()
    {
        // Lock the workstation using the Windows API
        // This is called when the service decides to lock the device
        var succeeded = this.lockWorkstation();
        var outcome = MapLockResult(succeeded, succeeded ? 0 : Marshal.GetLastWin32Error());
        if (outcome.Status == ActionStatus.Confirmed)
        {
            Debug.WriteLine("[SessionAgentHost] Workstation locked.");
        }
        else
        {
            Debug.WriteLine("[SessionAgentHost] Failed to lock workstation.");
        }

        return outcome;
    }

    private async ValueTask ExecuteCommandAsync(
        AgentCommandEnvelope command,
        CancellationToken cancellationToken)
    {
        var status = ActionStatus.InvalidState;
        if (command.SessionId == this.sessionId && command.ConnectionGeneration == this.generation)
        {
            status = command.Command switch
            {
                AgentCommandKind.ShowOverlay or AgentCommandKind.ReplaceOverlay when command.Overlay is { } overlay =>
                    this.ApplyOverlay(overlay),
                AgentCommandKind.ClearOverlay => this.ClearOverlay(),
                AgentCommandKind.LockWorkstation => this.DoLockWorkstation().Status,
                _ => ActionStatus.InvalidState,
            };
        }

        var result = new AgentActionResult(
            command.CommandId, command.SessionId, command.ConnectionGeneration,
            command.IntentVersion, status, null);
        await this.ipcChannel.SendAsync(new AgentCommandCompleted(result), cancellationToken);
    }

    private ActionStatus ApplyOverlay(OverlayIntent overlay)
    {
        this.overlayManager.ShowOverlay(overlay.Reason, overlay.CtaLabel);
        return ActionStatus.Confirmed;
    }

    private ActionStatus ClearOverlay()
    {
        this.overlayManager.HideOverlay();
        return ActionStatus.Confirmed;
    }

    internal static NativeActionOutcome MapLockResult(bool succeeded, int nativeError) =>
        succeeded
            ? new(ActionStatus.Confirmed, null)
            : new(ActionStatus.NativeFailure, nativeError);

    [DllImport("user32.dll")]
    private static extern bool LockWorkStationApi();

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.foregroundWatcher.Stop();
        this.foregroundWatcher.ForegroundChanged -= this.OnForegroundChanged;
        this.ipcChannel.MessageReceived -= this.OnMessageReceived;
        this.ipcChannel.Disconnected -= this.OnDisconnected;
        await this.ipcChannel.StopAsync();
        await base.StopAsync(cancellationToken);
    }
}
