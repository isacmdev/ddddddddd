namespace ControlParental.Service;

public sealed class ThresholdReevaluator(
    SessionEnforcementCoordinator coordinator,
    Func<string, CancellationToken, ValueTask> evaluate)
{
    private string? currentAppId;

    public bool Observe(string appId)
    {
        Volatile.Write(ref this.currentAppId, appId);
        return coordinator.TryPostCoalescible(SessionInputKind.Foreground, this.EvaluateCurrentAsync);
    }

    public bool Tick() => coordinator.TryPostCoalescible(SessionInputKind.Tick, this.EvaluateCurrentAsync);

    private ValueTask EvaluateCurrentAsync(CancellationToken token)
    {
        var appId = Volatile.Read(ref this.currentAppId);
        return appId is null ? ValueTask.CompletedTask : evaluate(appId, token);
    }
}
