// <copyright file="WnsChannelPlanner.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

/// <summary>
/// Pure decision logic for WNS channel lifecycle so it can be unit-tested
/// without invoking <c>PushNotificationManager</c> at runtime.
///
/// T19: WNS channels expire roughly 30 days after creation. The client must
/// renew before the deadline to avoid silent push loss. The Service falls
/// back to polling (T18) when WNS is unavailable, so a missed renewal never
/// blocks sync — it just defers push acceleration.
/// </summary>
public static class WnsChannelPlanner
{
    /// <summary>
    /// Renewal window in days: renew when the channel expires within this many days.
    /// </summary>
    public const int RenewalWindowDays = 5;

    /// <summary>
    /// Periodic check interval for renewal. Cheap: a single DateTime comparison per tick.
    /// </summary>
    public static readonly TimeSpan RenewalCheckInterval = TimeSpan.FromHours(1);

    /// <summary>
    /// Returns true when the channel needs to be renewed at <paramref name="now"/>.
    /// </summary>
    /// <param name="now">Current wall-clock time (UTC).</param>
    /// <param name="expirationUtc">Channel expiration (UTC). When null, the channel must be renewed.</param>
    /// <returns></returns>
    public static bool ShouldRenew(DateTimeOffset now, DateTimeOffset? expirationUtc)
    {
        if (expirationUtc == null)
        {
            return true;
        }

        var daysUntilExpiry = (expirationUtc.Value - now).TotalDays;
        return daysUntilExpiry <= RenewalWindowDays;
    }

    /// <summary>
    /// Computes the renewal deadline that should be registered with the backend.
    /// When the channel exposes a real expiration we trust it; otherwise fall back
    /// to the canonical 30-day window minus the renewal safety margin.
    /// </summary>
    /// <returns></returns>
    public static DateTimeOffset ResolveExpiration(
        DateTimeOffset now,
        DateTimeOffset? channelExpiration)
    {
        if (channelExpiration.HasValue && channelExpiration.Value != DateTimeOffset.MinValue)
        {
            return channelExpiration.Value;
        }

        return now.AddDays(30 - RenewalWindowDays);
    }
}
