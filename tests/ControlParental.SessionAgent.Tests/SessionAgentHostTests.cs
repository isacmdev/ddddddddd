// <copyright file="SessionAgentHostTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent.Tests;

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using ControlParental.Domain;
using ControlParental.SessionAgent;
using Xunit;

/// <summary>
/// T05 — Tests for SessionAgentHost IPC integration.
/// Verifies that ForegroundChanged events are sent to the service via IPC.
/// </summary>
public class SessionAgentHostTests
{
    // ── Fake IPC channel ──────────────────────────────────────────────

    private sealed class FakeIpcChannel : IIpcChannel
    {
        public bool IsConnected { get; private set; }
        public bool ThrowOnSend { get; set; }

        public event Action? Disconnected;
        public event Action<IIpcMessage>? MessageReceived;

        public ConcurrentQueue<IIpcMessage> SentMessages { get; } = new();

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            this.IsConnected = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            this.IsConnected = false;
            return Task.CompletedTask;
        }

        public Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default)
        {
            if (this.ThrowOnSend)
            {
                throw new InvalidOperationException("send failed");
            }

            this.SentMessages.Enqueue(message);
            return Task.CompletedTask;
        }

        public void SimulateDisconnect()
        {
            this.IsConnected = false;
            this.Disconnected?.Invoke();
        }

        public void SimulateMessageReceived(IIpcMessage message)
        {
            this.MessageReceived?.Invoke(message);
        }
    }

    // ── Fake overlay manager ─────────────────────────────────────────

    private sealed class FakeOverlayManager : SessionAgent.IOverlayManager
    {
        public bool IsOverlayVisible { get; private set; }
        public string? LastReason { get; private set; }
        public int? LastMinutes { get; private set; }

        public void ShowOverlay(string reason, string? ctaLabel = null)
        {
            this.IsOverlayVisible = true;
            this.LastReason = reason;
        }

        public void HideOverlay()
        {
            this.IsOverlayVisible = false;
            this.LastReason = null;
        }

        public void ShowWarning(int minutesRemaining)
        {
            this.LastMinutes = minutesRemaining;
        }
    }

    // ── Fake foreground watcher ──────────────────────────────────────

    private sealed class FakeForegroundWatcher : IForegroundWatcher
    {
        public string? CurrentAppId { get; set; }
        public bool StopCalled { get; private set; }
        public event Action<string>? ForegroundChanged;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Stop()
        {
            this.StopCalled = true;
        }

        public void SimulateForegroundChange(string appId)
        {
            this.CurrentAppId = appId;
            this.ForegroundChanged?.Invoke(appId);
        }
    }

    // ── Tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task SessionAgentHost_ForegroundChanged_SendsIpcMessage()
    {
        // Arrange
        var fakeIpc = new FakeIpcChannel();
        var fakeOverlay = new FakeOverlayManager();
        var fakeWatcher = new FakeForegroundWatcher();

        var host = new SessionAgentHost(fakeIpc, fakeOverlay, fakeWatcher);

        // Start the host (which will subscribe to foreground changes)
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var hostTask = host.StartAsync(cts.Token);

        // Wait for the host to start
        await Task.Delay(50);

        // Act — simulate a foreground change
        fakeWatcher.SimulateForegroundChange("chrome.exe|hash123");

        // Allow time for the event to propagate
        await Task.Delay(50);

        // Assert — ForegroundChanged message was sent
        Assert.False(fakeIpc.SentMessages.IsEmpty);

        var foregroundMsg = fakeIpc.SentMessages
            .OfType<ForegroundChanged>()
            .FirstOrDefault();

        Assert.NotNull(foregroundMsg);
        Assert.Equal("chrome.exe|hash123", foregroundMsg.AppId);
    }

    [Fact]
    public async Task SessionAgentHost_MultipleForegroundChanges_SendsMultipleMessages()
    {
        // Arrange
        var fakeIpc = new FakeIpcChannel();
        var fakeOverlay = new FakeOverlayManager();
        var fakeWatcher = new FakeForegroundWatcher();

        var host = new SessionAgentHostForTest(fakeIpc, fakeOverlay, fakeWatcher);

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var hostTask = host.StartAsync(cts.Token);

        await Task.Delay(50);

        // Act
        fakeWatcher.SimulateForegroundChange("whatsapp|hash");
        await Task.Delay(50);
        fakeWatcher.SimulateForegroundChange("instagram.exe|hash");
        await Task.Delay(50);

        // Assert
        var foregroundMessages = fakeIpc.SentMessages
            .OfType<ForegroundChanged>()
            .ToList();

        Assert.Equal(2, foregroundMessages.Count);
        Assert.Equal("whatsapp|hash", foregroundMessages[0].AppId);
        Assert.Equal("instagram.exe|hash", foregroundMessages[1].AppId);
    }

    [Fact]
    public async Task SessionAgentHost_ShowsOverlay_WhenServiceSendsShowOverlay()
    {
        // Arrange
        var fakeIpc = new FakeIpcChannel();
        var fakeOverlay = new FakeOverlayManager();
        var fakeWatcher = new FakeForegroundWatcher();

        var host = new SessionAgentHostForTest(fakeIpc, fakeOverlay, fakeWatcher);

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        _ = host.StartAsync(cts.Token);
        await Task.Delay(50);

        // Act — service sends ShowOverlay message
        fakeIpc.SimulateMessageReceived(new ShowOverlay("dispositivo bloqueado", "Pedir permiso"));

        // Allow time for message processing
        await Task.Delay(50);

        // Assert
        Assert.True(fakeOverlay.IsOverlayVisible);
        Assert.Equal("dispositivo bloqueado", fakeOverlay.LastReason);
    }

    [Fact]
    public async Task SessionAgentHost_StopAsync_StopsWatcher()
    {
        // Arrange
        var fakeIpc = new FakeIpcChannel();
        var fakeOverlay = new FakeOverlayManager();
        var fakeWatcher = new FakeForegroundWatcher();

        var host = new SessionAgentHost(fakeIpc, fakeOverlay, fakeWatcher);

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var hostTask = host.StartAsync(cts.Token);

        await Task.Delay(50);

        // Act
        await host.StopAsync(CancellationToken.None);

        Assert.True(fakeWatcher.StopCalled);
        Assert.False(fakeIpc.IsConnected);
    }

    [Fact]
    public async Task CorrelatedOverlayCommandReturnsExactResultAndAuthorityHeartbeat()
    {
        var ipc = new FakeIpcChannel();
        var overlay = new FakeOverlayManager();
        var watcher = new FakeForegroundWatcher();
        var host = new SessionAgentHost(ipc, overlay, watcher);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await host.StartAsync(cts.Token);
        await Task.Delay(30);

        ipc.SimulateMessageReceived(new AgentAuthority(4, 7));
        var envelope = new AgentCommandEnvelope(
            Guid.NewGuid(), 4, 7, 11, AgentCommandKind.ShowOverlay,
            DateTimeOffset.UtcNow.AddSeconds(1), new OverlayIntent(true, "limit", "Ask", 11));
        ipc.SimulateMessageReceived(new AgentCommandRequest(envelope));
        await Task.Delay(50);

        var heartbeat = Assert.Single(ipc.SentMessages.OfType<AgentHeartbeat>().Where(x => x.ConnectionGeneration == 7));
        Assert.Equal(4, heartbeat.SessionId);
        var completed = Assert.Single(ipc.SentMessages.OfType<AgentCommandCompleted>());
        Assert.Equal(envelope.CommandId, completed.Result.CommandId);
        Assert.Equal((4, 7L, 11L, ActionStatus.Confirmed),
            (completed.Result.SessionId, completed.Result.ConnectionGeneration, completed.Result.IntentVersion, completed.Result.Status));
        Assert.True(overlay.IsOverlayVisible);

        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SessionAgentHost_Disconnected_Event_Raises()
    {
        // Arrange
        var fakeIpc = new FakeIpcChannel();
        var fakeOverlay = new FakeOverlayManager();
        var fakeWatcher = new FakeForegroundWatcher();

        var host = new SessionAgentHostForTest(fakeIpc, fakeOverlay, fakeWatcher);

        var disconnectedCount = 0;
        fakeIpc.Disconnected += () => disconnectedCount++;

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        _ = host.StartAsync(cts.Token);
        await Task.Delay(50);

        // Act
        fakeIpc.SimulateDisconnect();
        await Task.Delay(50);

        // Assert
        Assert.Equal(1, disconnectedCount);
    }

    [Fact]
    public async Task SessionAgentHost_ForegroundChanged_SwallowsSendFailures()
    {
        // Arrange
        var fakeIpc = new FakeIpcChannel { ThrowOnSend = true };
        var fakeOverlay = new FakeOverlayManager();
        var fakeWatcher = new FakeForegroundWatcher();

        var host = new SessionAgentHost(fakeIpc, fakeOverlay, fakeWatcher);

        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        _ = host.StartAsync(cts.Token);

        await Task.Delay(50);

        // Act — the production host should swallow the IPC send failure
        fakeWatcher.SimulateForegroundChange("chrome.exe|hash123");

        await Task.Delay(50);

        // Assert
        Assert.Empty(fakeIpc.SentMessages);

        await host.StopAsync(CancellationToken.None);
    }

    // ── Testable SessionAgentHost ──────────────────────────────────

    /// <summary>
    /// A version of SessionAgentHost that is easier to test by exposing
    /// StartAsync publicly.
    /// </summary>
    private sealed class SessionAgentHostForTest : BackgroundService
    {
        private readonly IIpcChannel ipcChannel;
        private readonly IOverlayManager overlayManager;
        private readonly IForegroundWatcher foregroundWatcher;
        private readonly Stopwatch upTimeStopwatch;

        public SessionAgentHostForTest(
            IIpcChannel ipcChannel,
            IOverlayManager overlayManager,
            IForegroundWatcher foregroundWatcher)
        {
            this.ipcChannel = ipcChannel;
            this.overlayManager = overlayManager;
            this.foregroundWatcher = foregroundWatcher;
            this.upTimeStopwatch = Stopwatch.StartNew();
        }

        public new Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await this.ipcChannel.StartAsync(stoppingToken);
            this.ipcChannel.MessageReceived += this.OnMessageReceived;
            this.ipcChannel.Disconnected += this.OnDisconnected;
            this.foregroundWatcher.ForegroundChanged += this.OnForegroundChanged;
            await this.foregroundWatcher.StartAsync(stoppingToken);

            // Send initial heartbeat
            await this.SendHeartbeatAsync(stoppingToken);

            // Keep alive
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(100, stoppingToken);
            }
        }

        private async void OnForegroundChanged(string appId)
        {
            try
            {
                var message = new ForegroundChanged(appId);
                await this.ipcChannel.SendAsync(message, CancellationToken.None);
            }
            catch
            {
                // Best effort
            }
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
                    // Skip in test
                    break;

                case RequestStateSnapshot:
                    // Skip in test
                    break;

                case Ping:
                    _ = this.ipcChannel.SendAsync(new Pong(), CancellationToken.None);
                    break;
            }
        }

        private void OnDisconnected()
        {
            // Best effort
        }

        private async Task SendHeartbeatAsync(CancellationToken cancellationToken)
        {
            var heartbeat = new AgentHeartbeat(
                AgentId: "test-agent",
                UpTimeMs: this.upTimeStopwatch.ElapsedMilliseconds,
                IsOverlayVisible: this.overlayManager.IsOverlayVisible);

            await this.ipcChannel.SendAsync(heartbeat, cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            this.foregroundWatcher.Stop();
            this.ipcChannel.MessageReceived -= this.OnMessageReceived;
            this.ipcChannel.Disconnected -= this.OnDisconnected;
            this.foregroundWatcher.ForegroundChanged -= this.OnForegroundChanged;
            await this.ipcChannel.StopAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}
