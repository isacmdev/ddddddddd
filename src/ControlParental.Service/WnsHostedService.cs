// <copyright file="WnsHostedService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ControlParental.Domain;

/// <summary>
/// T19 — Hosted service adapter for WNS push notification lifecycle.
/// Registers and renews WNS channel URI with the backend.
/// </summary>
public sealed class WnsNotificationServiceHostedAdapter : IHostedService
{
    private readonly IPushNotificationService wns;
    private readonly IBackendClient backend;
    private readonly IOutboxManager outbox;
    private readonly ILogger<WnsNotificationServiceHostedAdapter> logger;

    // Renewal interval: 25 days (WNS channels expire ~30 days)
    private const int RenewalIntervalDays = 25;
    private Timer? renewalTimer;

    public WnsNotificationServiceHostedAdapter(
        IPushNotificationService wns,
        IBackendClient backend,
        IOutboxManager outbox,
        ILogger<WnsNotificationServiceHostedAdapter> logger)
    {
        this.wns = wns ?? throw new ArgumentNullException(nameof(wns));
        this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
        this.outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync(CancellationToken ct)
    {
        // Get or renew WNS channel
        var result = await this.wns.GetOrRenewTokenAsync(ct);
        if (result.Success && !string.IsNullOrEmpty(result.ChannelUri))
        {
            // Register with backend
            await this.backend.RegisterPushTokenAsync(
                result.ChannelUri,
                "wns",
                result.ExpiresAt,
                ct);
            this.logger.LogInformation("[WNS] Channel registered: {Uri}", result.ChannelUri);

            // Schedule renewal
            this.renewalTimer = new Timer(
                _ => _ = this.RenewChannelAsync(ct),
                null,
                TimeSpan.FromDays(RenewalIntervalDays),
                TimeSpan.FromDays(RenewalIntervalDays));
        }
        else
        {
            this.logger.LogWarning("[WNS] Failed to get channel: {Error}", result.ErrorMessage);
        }
    }

    private async Task RenewChannelAsync(CancellationToken ct)
    {
        var result = await this.wns.GetOrRenewTokenAsync(ct);
        if (result.Success && !string.IsNullOrEmpty(result.ChannelUri))
        {
            await this.backend.RegisterPushTokenAsync(result.ChannelUri, "wns", result.ExpiresAt, ct);
            this.logger.LogInformation("[WNS] Channel renewed: {Uri}", result.ChannelUri);
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        this.renewalTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        this.renewalTimer?.Dispose();
    }
}
