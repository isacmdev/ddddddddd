namespace ControlParental.App.UI;

using ControlParental.Domain;

/// <summary>
/// Fail-closed channel used until the realtime transport is provisioned by the
/// host. It never fabricates broadcasts or applies payload state.
/// </summary>
internal sealed class FailClosedRealtimeChannel : IRealtimeChannel
{
    public bool IsSubscribed => false;

    public event EventHandler<Broadcast>? BroadcastReceived;

    public Task SubscribeAsync() => Task.CompletedTask;

    public void Unsubscribe() { }

    public void Dispose() => this.BroadcastReceived = null;
}
