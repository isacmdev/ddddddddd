// <copyright file="RealtimeChannelAdapter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

/// <summary>
/// T21 — Adapts the real Supabase RealtimeChannel to IRealtimeChannel.
/// This keeps the realtime dependency contained in App.UI only.
/// </summary>
public sealed class RealtimeChannelAdapter : Domain.IRealtimeChannel
{
    private readonly Supabase.Realtime.RealtimeChannel channel;
    private readonly Supabase.Realtime.RealtimeBroadcast<Supabase.Realtime.Models.BaseBroadcast> broadcast;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RealtimeChannelAdapter"/> class.
    /// </summary>
    /// <param name="channel">The underlying Supabase realtime channel.</param>
    /// <param name="broadcast">The broadcast handler for this channel.</param>
    public RealtimeChannelAdapter(
        Supabase.Realtime.RealtimeChannel channel,
        Supabase.Realtime.RealtimeBroadcast<Supabase.Realtime.Models.BaseBroadcast> broadcast)
    {
        this.channel = channel;
        this.broadcast = broadcast;
        this.broadcast.AddBroadcastEventHandler(this.OnBroadcast);
    }

    /// <inheritdoc />
    public bool IsSubscribed => !this.disposed && this.channel.IsSubscribed;

    /// <inheritdoc />
    public Task SubscribeAsync() => this.channel.Subscribe();

    /// <inheritdoc />
    public void Unsubscribe() => this.channel.Unsubscribe();

    /// <inheritdoc />
    public event EventHandler<Domain.Broadcast>? BroadcastReceived;

    private void OnBroadcast(Supabase.Realtime.Interfaces.IRealtimeBroadcast sender, Supabase.Realtime.Models.BaseBroadcast? broadcast)
    {
        var payload = broadcast?.Payload != null
            ? new Dictionary<string, object?>(broadcast.Payload)
            : new Dictionary<string, object?>();

        this.BroadcastReceived?.Invoke(this, new Domain.Broadcast(payload));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.broadcast.RemoveBroadcastEventHandler(this.OnBroadcast);
        this.channel.Unsubscribe();
    }
}
