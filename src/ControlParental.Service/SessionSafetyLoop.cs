namespace ControlParental.Service;

using ControlParental.Domain;

public sealed class SessionSafetyLoop : IAsyncDisposable
{
    private readonly int sessionId;
    private readonly Func<string, CancellationToken, Task<EnforcementResult>> evaluate;
    private readonly Func<ObservedProcessTarget, CancellationToken, Task<ActionStatus>> terminate;
    private readonly IAuthoritativeHealthSink health;
    private readonly Action heartbeatAccepted;
    private readonly Action agentDeath;
    private readonly Action<bool> childAdminChanged;
    private readonly Action<TimeChangeReason> timeChanged;
    private readonly SessionEnforcementCoordinator coordinator;
    private readonly AgentCommandPort port = new();
    private readonly OverlayIntentPolicy overlay;
    private ForegroundChanged? currentForeground;
    private long generation;

    public SessionSafetyLoop(
        int sessionId,
        Func<string, CancellationToken, Task<EnforcementResult>> evaluate,
        Func<ObservedProcessTarget, CancellationToken, Task<ActionStatus>> terminate,
        IOverlayIntentStore intentStore,
        IAuthoritativeHealthSink health,
        Action? heartbeatAccepted = null,
        Action? agentDeath = null,
        Action<bool>? childAdminChanged = null,
        Action<TimeChangeReason>? timeChanged = null)
    {
        this.sessionId = sessionId;
        this.evaluate = evaluate;
        this.terminate = terminate;
        this.health = health;
        this.heartbeatAccepted = heartbeatAccepted ?? (() => { });
        this.agentDeath = agentDeath ?? (() => { });
        this.childAdminChanged = childAdminChanged ?? (_ => { });
        this.timeChanged = timeChanged ?? (_ => { });
        this.coordinator = new(sessionId);
        this.overlay = new(intentStore);
    }

    public Task WhenIdleAsync() => this.coordinator.WhenIdleAsync();

    public async Task AttachAgentAsync(
        long generation,
        Func<AgentCommandEnvelope, CancellationToken, ValueTask> sender,
        CancellationToken cancellationToken = default)
    {
        await this.coordinator.PostAsync(async token =>
        {
            this.generation = generation;
            this.port.Replace(generation, sender);
            this.health.SetRestoreStatus(true);
            var status = await this.overlay.ReconcileAsync(this.sessionId, this.port, token);
            this.health.SetCurrentCriticalActionsConfirmed(IsConfirmed(status));
        }, cancellationToken);
        await this.coordinator.WhenIdleAsync();
    }

    public bool Observe(ForegroundChanged observation)
    {
        if (observation.Target is { } target && target.SessionId != this.sessionId)
        {
            return false;
        }

        Volatile.Write(ref this.currentForeground, observation);
        return this.coordinator.TryPostCoalescible(SessionInputKind.Foreground, this.EvaluateCurrentAsync);
    }

    public bool Tick() =>
        this.coordinator.TryPostCoalescible(SessionInputKind.Tick, this.EvaluateCurrentAsync);

    public bool AcceptResult(AgentActionResult result) => this.port.TryAccept(result);

    public bool AcceptHeartbeat(AgentHeartbeat heartbeat)
    {
        if (heartbeat.SessionId != this.sessionId || heartbeat.ConnectionGeneration != this.generation)
        {
            return false;
        }

        this.heartbeatAccepted();
        return true;
    }

    public async Task AgentDiedAsync(CancellationToken cancellationToken = default)
    {
        await this.coordinator.PostAsync(_ =>
        {
            this.port.Disconnect(checked(this.generation + 1));
            this.generation = this.port.Generation;
            this.health.SetCurrentCriticalActionsConfirmed(false);
            this.agentDeath();
            return ValueTask.CompletedTask;
        }, cancellationToken);
    }

    public async Task ChildAdminChangedAsync(bool isAdmin, CancellationToken cancellationToken = default)
    {
        await this.coordinator.PostAsync(_ =>
        {
            this.health.SetHealthBlockingIssues(isAdmin);
            this.childAdminChanged(isAdmin);
            return ValueTask.CompletedTask;
        }, cancellationToken);
    }

    public Task TimeChangedAsync(TimeChangeReason reason, CancellationToken cancellationToken = default) =>
        this.PostAndWaitAsync(async token =>
        {
            this.timeChanged(reason);
            await this.EvaluateCurrentAsync(token);
        }, cancellationToken);

    public Task<AgentActionResult> ExecuteAgentCommandAsync(
        AgentCommandEnvelope command,
        CancellationToken cancellationToken = default) =>
        this.PostAndWaitAsync(token => this.port.ExecuteAsync(command, token), cancellationToken);

    public ValueTask DisposeAsync() => this.coordinator.DisposeAsync();

    private async ValueTask EvaluateCurrentAsync(CancellationToken cancellationToken)
    {
        var foreground = Volatile.Read(ref this.currentForeground);
        if (foreground is null)
        {
            return;
        }

        var decision = await this.evaluate(foreground.AppId, cancellationToken);
        this.overlay.SetDesired(decision.Blocked, decision.ReasonText ?? string.Empty, decision.Blocked ? "Ask" : null);
        var overlayStatus = await this.overlay.ReconcileAsync(this.sessionId, this.port, cancellationToken);
        if (!decision.Blocked)
        {
            this.health.SetCurrentCriticalActionsConfirmed(IsConfirmed(overlayStatus));
            return;
        }

        var targetResult = await this.ExecuteTerminationAsync(
            foreground.Target, this.overlay.Intent.Version, cancellationToken);
        var targetIsCurrent = targetResult.SessionId == this.sessionId &&
            targetResult.ConnectionGeneration == this.generation &&
            targetResult.IntentVersion == this.overlay.Intent.Version;
        this.health.SetCurrentCriticalActionsConfirmed(
            IsConfirmed(overlayStatus) && targetIsCurrent && IsConfirmed(targetResult.Status));
    }

    private async Task<AgentActionResult> ExecuteTerminationAsync(
        ObservedProcessTarget? target,
        long intentVersion,
        CancellationToken cancellationToken)
    {
        var command = new AgentCommandEnvelope(
            Guid.NewGuid(), this.sessionId, this.generation, intentVersion,
            AgentCommandKind.TerminateProcess, DateTimeOffset.UtcNow.AddSeconds(3), Target: target);
        var status = target is null
            ? ActionStatus.InvalidTarget
            : await this.terminate(target, cancellationToken);
        return new AgentActionResult(
            command.CommandId, command.SessionId, command.ConnectionGeneration,
            command.IntentVersion, status, null);
    }

    private static bool IsConfirmed(ActionStatus status) =>
        status is ActionStatus.Confirmed or ActionStatus.HarmlessAbsence;

    private async Task PostAndWaitAsync(
        Func<CancellationToken, ValueTask> work,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await this.coordinator.PostAsync(async token =>
        {
            try
            {
                await work(token);
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }, cancellationToken);
        await completion.Task.WaitAsync(cancellationToken);
    }

    private async Task<T> PostAndWaitAsync<T>(
        Func<CancellationToken, ValueTask<T>> work,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        await this.coordinator.PostAsync(async token =>
        {
            try
            {
                completion.TrySetResult(await work(token));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }, cancellationToken);
        return await completion.Task.WaitAsync(cancellationToken);
    }
}
