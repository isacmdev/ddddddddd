// <copyright file="WnsPushNotificationHandler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

#pragma warning disable SA1633, SA1512

// <copyright file="WnsPushNotificationHandler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Microsoft.Windows.PushNotifications;
using Windows.Foundation.Metadata;

/// <summary>
/// T19 — Handles WNS push notifications in App.UI.
///
/// Uses Windows App SDK 1.8+ <see cref="PushNotificationManager"/> to receive
/// WNS push notifications. Requires MSIX package identity and an AAD App ID
/// registered with WNS.
///
/// When a raw "sync now" notification arrives, this handler signals the Service
/// via IPC to trigger an immediate policy sync (T18).
///
/// Registration flow:
/// 1. Call <see cref="PushNotificationManager.Register"/> to enable in-process delivery
/// 2. Create channel via <see cref="PushNotificationManager.CreateChannelAsync"/>
/// 3. Register channel URI with backend
/// 4. Subscribe to <see cref="PushNotificationManager.PushReceived"/>
/// 5. Periodically renew the channel while running (see <see cref="WnsChannelPlanner"/>)
///
/// When App.UI is not running, the Service falls back to polling (T18/T20).
/// </summary>
public sealed class WnsPushNotificationHandler : IDisposable, IWnsRegistrationPort, IWnsChannelProvider
{
    // Named pipe to the Service UI endpoint. WNS is an accelerator; the Service owns sync.
    private const string PipeName = "ControlParental.UI";
    private const int ConnectTimeoutMs = 5000;

    private readonly IUIChannel uiChannel;
    private readonly IDisposable? ownedChannel;
    private readonly Func<string, CancellationToken, Task>? sendTriggerSyncOverride;
    private readonly CancellationTokenSource cts;
    private bool disposed;

    // Renewal timer + the most recent channel reference, used by the renewal loop.
    private Timer? renewalTimer;
    private PushNotificationChannel? activeChannel;

    /// <summary>
    /// Initializes a new instance of the <see cref="WnsPushNotificationHandler"/> class.
    /// </summary>
    /// <param name="uiChannel">Typed channel to the Service.</param>
    public WnsPushNotificationHandler(IUIChannel uiChannel)
    {
        this.uiChannel = uiChannel ?? throw new ArgumentNullException(nameof(uiChannel));
        this.cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Compatibility constructor for callers that have not yet resolved the UI channel.
    /// Registration authority remains exclusively in the Service.
    /// </summary>
    /// <param name="supabaseUrl">Obsolete and ignored.</param>
    /// <param name="supabaseKey">Obsolete and ignored.</param>
    /// <param name="sendTriggerSyncOverride">Optional test seam for the IPC send.</param>
    public WnsPushNotificationHandler(string supabaseUrl, string supabaseKey, Func<string, CancellationToken, Task>? sendTriggerSyncOverride = null)
    {
        _ = supabaseUrl ?? throw new ArgumentNullException(nameof(supabaseUrl));
        _ = supabaseKey ?? throw new ArgumentNullException(nameof(supabaseKey));
        this.uiChannel = new NamedPipeUIChannel();
        this.ownedChannel = this.uiChannel as IDisposable;
        this.sendTriggerSyncOverride = sendTriggerSyncOverride;
        this.cts = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts the WNS push notification handler.
    /// Enables in-process push delivery, creates a WNS channel, registers it
    /// with the backend, and listens for push notifications.
    /// This method blocks until cancellation is requested.
    /// </summary>
    /// <returns>A task that completes when the handler exits.</returns>
    public async Task StartAsync()
    {
        try
        {
            // T19: Guard WNS APIs at runtime — App.UI may run on Windows versions or packaged
            // identities that do not expose the PushNotifications contract.
            if (!ApiInformation.IsApiContractPresent(
                "Microsoft.Windows.PushNotifications.PushNotificationsContract", 1, 0))
            {
                Debug.WriteLine("[WNS] PushNotifications API contract not present — push notifications disabled");
                return;
            }

            // T19: Load AAD App ID from environment — required by PushNotificationManager.CreateChannelAsync
            var appIdStr = Environment.GetEnvironmentVariable("WNS_AAD_APP_ID");
            if (string.IsNullOrEmpty(appIdStr) || !Guid.TryParse(appIdStr, out var appId))
            {
                Debug.WriteLine("[WNS] WNS_AAD_APP_ID not set or invalid — push notifications disabled");
                return;
            }

            Debug.WriteLine($"[WNS] Registering PushNotificationManager for AAD App ID {appId}...");

            // Step 1: Register for in-process push notification delivery
            // MUST be called before subscribing to PushReceived or creating a channel,
            // otherwise COMException "Element not found" is thrown.
            try
            {
                PushNotificationManager.Default.Register();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WNS] PushNotificationManager.Register() failed: {ex.Message}");
                return;
            }

            Debug.WriteLine("[WNS] PushNotificationManager registered.");

            // Step 2: Subscribe to PushReceived event BEFORE creating the channel
            PushNotificationManager.Default.PushReceived += this.OnPushReceived;

            // Step 3: Create the WNS channel
            Debug.WriteLine("[WNS] Creating push channel...");
            PushNotificationChannel? channel = null;
            try
            {
                var result = await PushNotificationManager.Default.CreateChannelAsync(appId)
                    .AsTask(this.cts.Token)
                    .ConfigureAwait(false);

                if (result.Status != PushNotificationChannelStatus.CompletedSuccess)
                {
                    Debug.WriteLine($"[WNS] Channel creation failed: {result.Status}, extended error: {result.ExtendedError}");
                    return;
                }

                channel = result.Channel;
                Debug.WriteLine($"[WNS] Channel created, expires {channel.ExpirationTime}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WNS] Failed to create WNS channel (AAD App ID not registered with WNS?): {ex.Message}");
                return;
            }

            // Step 4: Register channel URI with backend
            await this.RegisterChannelAsync(channel, this.cts.Token).ConfigureAwait(false);

            // Step 5: T19 — renew the channel while we stay running. We hold a
            // reference to the channel so the renewal callback can re-register
            // its URI with the backend when the deadline approaches.
            this.activeChannel = channel;
            this.renewalTimer = new Timer(
                callback: _ => _ = this.RenewChannelIfNeededAsync(),
                state: null,
                dueTime: WnsChannelPlanner.RenewalCheckInterval,
                period: WnsChannelPlanner.RenewalCheckInterval);

            // Keep alive until cancelled
            try
            {
                await Task.Delay(Timeout.Infinite, this.cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown
            }
            finally
            {
                this.renewalTimer?.Dispose();
                this.renewalTimer = null;
                this.activeChannel = null;
                PushNotificationManager.Default.PushReceived -= this.OnPushReceived;
                channel?.Close();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WNS] Push notification handler failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles a raw WNS notification as an opaque sync hint.
    /// The payload is deliberately ignored; only the typed <see cref="TriggerSync"/>
    /// IPC message is sent to the Service.
    /// </summary>
    /// <param name="rawPayload">Raw WNS bytes. The handler does not inspect them.</param>
    /// <param name="cancellationToken">Cancellation token for the IPC send.</param>
    /// <returns>A task that completes after the sync signal is sent or rejected.</returns>
    public Task HandleRawNotificationAsync(
        ReadOnlyMemory<byte> rawPayload,
        CancellationToken cancellationToken = default)
    {
        _ = rawPayload;
        return this.SendTriggerSyncAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the handler and cancels any pending work.
    /// </summary>
    public void Stop()
    {
        this.cts.Cancel();
    }

    /// <summary>
    /// Releases the handler resources.
    /// </summary>
    public void Dispose()
    {
        if (!this.disposed)
        {
            this.cts.Cancel();
            this.cts.Dispose();
            this.ownedChannel?.Dispose();
            this.disposed = true;
        }
    }

    /// <summary>
    /// Handles a push notification received event.
    /// The raw notification from WNS means "sync now" — we signal the Service
    /// via IPC to trigger an immediate policy pull (T18).
    /// Per T19: the Service does NOT trust the payload; it only uses the signal
    /// to trigger an authenticated GET policy sync.
    /// </summary>
    private void OnPushReceived(PushNotificationManager sender, PushNotificationReceivedEventArgs args)
    {
        // The payload is intentionally not parsed. A WNS receipt is only a sync hint.
        Debug.WriteLine("[WNS] PushReceived; treating payload as an opaque sync hint");

        // T19: Acknowledge the push so WNS stops delivery attempts, then signal the Service.
        var deferral = args.GetDeferral();
        _ = Task.Run(async () =>
        {
            try
            {
                await this.HandleRawNotificationAsync(ReadOnlyMemory<byte>.Empty, this.cts.Token).ConfigureAwait(false);
            }
            finally
            {
                deferral?.Complete();
            }
        });
    }

    /// <summary>
    /// Registers the WNS channel URI through the Service's typed IPC contract.
    /// </summary>
    public Task<WnsRegistrationResult?> RegisterChannelAsync(
        string channelUri,
        DateTimeOffset expiresAt,
        CancellationToken ct = default)
    {
        var request = new RegisterWnsChannel(
            Guid.NewGuid().ToString("N"),
            channelUri,
            "wns",
            expiresAt);

        return this.QueryRegistrationAsync(request, ct);
    }

    /// <summary>Creates one real WNS channel without registering or exposing its value.</summary>
    public async Task<WnsChannelIntent?> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        if (!ApiInformation.IsApiContractPresent(
            "Microsoft.Windows.PushNotifications.PushNotificationsContract", 1, 0))
        {
            return null;
        }

        var appIdText = Environment.GetEnvironmentVariable("WNS_AAD_APP_ID");
        if (string.IsNullOrWhiteSpace(appIdText) || !Guid.TryParse(appIdText, out var appId))
        {
            return null;
        }

        try
        {
            PushNotificationManager.Default.Register();
            var result = await PushNotificationManager.Default.CreateChannelAsync(appId)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            if (result.Status != PushNotificationChannelStatus.CompletedSuccess)
            {
                return null;
            }

            var channel = result.Channel;
            var expiresAt = WnsChannelPlanner.ResolveExpiration(
                DateTimeOffset.UtcNow,
                channel.ExpirationTime != DateTime.MinValue
                    ? DateTimeOffset.FromFileTime(channel.ExpirationTime.ToFileTime())
                    : null);
            return new WnsChannelIntent(channel.Uri.ToString(), expiresAt);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private Task<WnsRegistrationResult?> RegisterChannelAsync(PushNotificationChannel channel, CancellationToken ct)
    {
        var expiresAt = WnsChannelPlanner.ResolveExpiration(
            DateTimeOffset.UtcNow,
            channel.ExpirationTime != DateTime.MinValue ? DateTimeOffset.FromFileTime(channel.ExpirationTime.ToFileTime()) : null);

        return this.RegisterChannelAsync(channel.Uri.ToString(), expiresAt, ct);
    }

    private async Task<WnsRegistrationResult?> QueryRegistrationAsync(
        RegisterWnsChannel request,
        CancellationToken ct)
    {
        return await this.uiChannel.QueryAsync<RegisterWnsChannel, WnsRegistrationResult>(request, ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Periodic renewal callback — checks the active channel against
    /// <see cref="WnsChannelPlanner.ShouldRenew"/> and re-creates it when needed.
    /// </summary>
    private async Task RenewChannelIfNeededAsync()
    {
        var channel = this.activeChannel;
        if (channel == null)
        {
            return;
        }

        DateTimeOffset? expiration = null;
        try
        {
            if (channel.ExpirationTime != DateTime.MinValue)
            {
                expiration = DateTimeOffset.FromFileTime(channel.ExpirationTime.ToFileTime());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WNS] Failed to read channel expiration: {ex.Message}");
        }

        if (!WnsChannelPlanner.ShouldRenew(DateTimeOffset.UtcNow, expiration))
        {
            return;
        }

        try
        {
            channel.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WNS] Failed to close old channel: {ex.Message}");
        }

        var appIdStr = Environment.GetEnvironmentVariable("WNS_AAD_APP_ID");
        if (string.IsNullOrEmpty(appIdStr) || !Guid.TryParse(appIdStr, out var appId))
        {
            Debug.WriteLine("[WNS] Cannot renew: WNS_AAD_APP_ID not set");
            return;
        }

        try
        {
            var result = await PushNotificationManager.Default.CreateChannelAsync(appId)
                .AsTask(this.cts.Token)
                .ConfigureAwait(false);

            if (result.Status != PushNotificationChannelStatus.CompletedSuccess || result.Channel == null)
            {
                Debug.WriteLine($"[WNS] Channel renewal failed: {result.Status}");
                return;
            }

            this.activeChannel = result.Channel;
            await this.RegisterChannelAsync(result.Channel, this.cts.Token).ConfigureAwait(false);
            Debug.WriteLine("[WNS] Channel renewed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WNS] Channel renewal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends a TriggerSync IPC message to the Service via the named pipe.
    /// This signals that a WNS push was received and the Service should sync now.
    /// </summary>
    private async Task SendTriggerSyncAsync(CancellationToken cancellationToken = default)
    {
        var effectiveCancellationToken = cancellationToken.CanBeCanceled
            ? cancellationToken
            : this.cts.Token;

        try
        {
            var message = new TriggerSync();

            // T26/E5: use the source-generated JSON catalogue instead of reflection.
            var json = JsonSerializer.Serialize(
                message,
                ControlParental.Domain.UIMessagesJsonContext.Default.TriggerSync);

            if (this.sendTriggerSyncOverride != null)
            {
                await this.sendTriggerSyncOverride(json, effectiveCancellationToken).ConfigureAwait(false);
                return;
            }

            using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(ConnectTimeoutMs, effectiveCancellationToken).ConfigureAwait(false);

            var bytes = Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes, effectiveCancellationToken).ConfigureAwait(false);
            await pipe.FlushAsync(effectiveCancellationToken).ConfigureAwait(false);

            Debug.WriteLine("[WNS] TriggerSync sent to Service");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WNS] Failed to send TriggerSync to Service: {ex.Message}");
        }
    }
}
