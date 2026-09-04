namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Xunit;

public sealed class ProductionSafetyLoopTests
{
    private static readonly DateTimeOffset Started = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RestartRestoresIntentAndConvergesOnlyOnCurrentGeneration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"overlay-{Guid.NewGuid():N}.json");
        try
        {
            var store = new FileOverlayIntentStore(path);
            store.Save(new OverlayIntent(true, "limit", "Ask", 7));
            var sink = new RecordingHealthSink();
            await using var loop = CreateLoop(store, sink, _ => Allowed());

            var commands = new List<AgentCommandEnvelope>();
            await loop.AttachAgentAsync(3, (command, _) =>
            {
                commands.Add(command);
                loop.AcceptResult(Result(command, ActionStatus.Confirmed));
                return ValueTask.CompletedTask;
            });

            Assert.Single(commands);
            Assert.Equal(AgentCommandKind.ShowOverlay, commands[0].Command);
            Assert.Equal((4, 3L, 7L), (commands[0].SessionId, commands[0].ConnectionGeneration, commands[0].IntentVersion));
            Assert.True(sink.RestoreSucceeded);
            Assert.True(sink.CriticalActionsConfirmed);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SameAppTickCrossesThresholdWithoutAnotherForegroundEvent()
    {
        var blocked = false;
        var evaluations = 0;
        var terminator = new RecordingTerminator(ActionStatus.Confirmed);
        var sink = new RecordingHealthSink();
        await using var loop = CreateLoop(new MemoryIntentStore(), sink, _ =>
        {
            evaluations++;
            return blocked ? Blocked("limit") : Allowed();
        }, terminator);
        await AttachConfirmingAgentAsync(loop, 2);

        loop.Observe(new ForegroundChanged("app", Target()));
        await loop.WhenIdleAsync();
        blocked = true;
        loop.Tick();
        await loop.WhenIdleAsync();

        Assert.Equal(2, evaluations);
        Assert.Equal(Target(), terminator.LastTarget);
        Assert.True(sink.CriticalActionsConfirmed);
    }

    [Fact]
    public async Task StaleAndWrongSessionHeartbeatsDoNotRefreshAuthority()
    {
        var accepted = 0;
        await using var loop = CreateLoop(new MemoryIntentStore(), new RecordingHealthSink(), _ => Allowed(), heartbeat: () => accepted++);
        await AttachConfirmingAgentAsync(loop, 9);

        Assert.False(loop.AcceptHeartbeat(new AgentHeartbeat("agent", 10, false, 4, 8)));
        Assert.False(loop.AcceptHeartbeat(new AgentHeartbeat("agent", 10, false, 3, 9)));
        Assert.True(loop.AcceptHeartbeat(new AgentHeartbeat("agent", 10, false, 4, 9)));
        Assert.Equal(1, accepted);
    }

    [Fact]
    public async Task AgentDeathInvalidatesPendingAuthorityAndDegradesHealth()
    {
        var deaths = 0;
        var sink = new RecordingHealthSink();
        await using var loop = CreateLoop(new MemoryIntentStore(), sink, _ => Blocked("limit"), death: () => deaths++);
        var acknowledge = true;
        await loop.AttachAgentAsync(1, (command, _) =>
        {
            if (acknowledge)
            {
                loop.AcceptResult(Result(command, ActionStatus.Confirmed));
            }

            return ValueTask.CompletedTask;
        });
        acknowledge = false;
        loop.Observe(new ForegroundChanged("app", Target()));
        await Task.Delay(10);

        await loop.AgentDiedAsync();
        await loop.WhenIdleAsync();

        Assert.Equal(1, deaths);
        Assert.False(sink.CriticalActionsConfirmed);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ChildAdminEvidenceUpdatesScopedIssueAndHealth(bool isAdmin, bool expectedBlocking)
    {
        var evidence = new List<bool>();
        var sink = new RecordingHealthSink();
        await using var loop = CreateLoop(new MemoryIntentStore(), sink, _ => Allowed(), childAdmin: evidence.Add);

        await loop.ChildAdminChangedAsync(isAdmin);
        await loop.WhenIdleAsync();

        Assert.Equal([isAdmin], evidence);
        Assert.Equal(expectedBlocking, sink.HasBlockingIssues);
    }

    [Fact]
    public async Task PidReuseFailurePreservesExactTargetAndCannotConfirmProtection()
    {
        var reused = new RecordingTerminator(ActionStatus.InvalidTarget);
        var sink = new RecordingHealthSink();
        await using var loop = CreateLoop(new MemoryIntentStore(), sink, _ => Blocked("limit"), reused);
        await AttachConfirmingAgentAsync(loop, 6);

        loop.Observe(new ForegroundChanged("canonical-app", Target()));
        await loop.WhenIdleAsync();

        Assert.Equal(Target(), reused.LastTarget);
        Assert.False(sink.CriticalActionsConfirmed);
    }

    [Theory]
    [InlineData(TimeChangeReason.ClockJump)]
    [InlineData(TimeChangeReason.ZoneChange)]
    public async Task TimeChangeIsSerializedAfterForegroundAndReevaluatesCurrentApp(TimeChangeReason reason)
    {
        var firstEvaluationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstEvaluation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        var evaluations = 0;
        await using var loop = new SessionSafetyLoop(
            4,
            async (_, _) =>
            {
                order.Add($"evaluate-{++evaluations}");
                if (evaluations == 1)
                {
                    firstEvaluationStarted.SetResult();
                    await releaseFirstEvaluation.Task;
                }

                return Allowed();
            },
            (_, _) => Task.FromResult(ActionStatus.Confirmed),
            new MemoryIntentStore(),
            new RecordingHealthSink(),
            timeChanged: changed => order.Add($"time-{changed}"));
        await AttachConfirmingAgentAsync(loop, 2);

        loop.Observe(new ForegroundChanged("app", Target()));
        await firstEvaluationStarted.Task;
        var timeChange = loop.TimeChangedAsync(reason);
        Assert.False(timeChange.IsCompleted);

        releaseFirstEvaluation.SetResult();
        await timeChange;
        await loop.WhenIdleAsync();

        Assert.Equal(["evaluate-1", $"time-{reason}", "evaluate-2"], order);
    }

    [Fact]
    public async Task TypedLockCommandUsesCurrentCoordinatorAuthority()
    {
        await using var loop = CreateLoop(new MemoryIntentStore(), new RecordingHealthSink(), _ => Allowed());
        AgentCommandEnvelope? sent = null;
        await loop.AttachAgentAsync(5, (command, _) =>
        {
            sent = command;
            loop.AcceptResult(Result(command, ActionStatus.Confirmed));
            return ValueTask.CompletedTask;
        });

        var command = new AgentCommandEnvelope(
            Guid.NewGuid(), 4, 5, 11, AgentCommandKind.LockWorkstation,
            DateTimeOffset.UtcNow.AddSeconds(2));
        var result = await loop.ExecuteAgentCommandAsync(command);

        Assert.Equal(command, sent);
        Assert.Equal(ActionStatus.Confirmed, result.Status);
    }

    private static SessionSafetyLoop CreateLoop(
        IOverlayIntentStore store,
        RecordingHealthSink sink,
        Func<string, EnforcementResult> evaluate,
        RecordingTerminator? terminator = null,
        Action? heartbeat = null,
        Action? death = null,
        Action<bool>? childAdmin = null) =>
        new(4, (appId, _) => Task.FromResult(evaluate(appId)),
            (target, _) => (terminator ?? new RecordingTerminator(ActionStatus.Confirmed)).TerminateAsync(target),
            store, sink, heartbeat, death, childAdmin);

    private static async Task AttachConfirmingAgentAsync(SessionSafetyLoop loop, long generation) =>
        await loop.AttachAgentAsync(generation, (command, _) =>
        {
            loop.AcceptResult(Result(command, ActionStatus.Confirmed));
            return ValueTask.CompletedTask;
        });

    private static ObservedProcessTarget Target() => new(42, 4, Started);
    private static EnforcementResult Allowed() => Result(false, "allowed");
    private static EnforcementResult Blocked(string reason) => Result(true, reason);
    private static EnforcementResult Result(bool blocked, string reason) =>
        new() { Success = true, Blocked = blocked, ReasonText = reason, Timestamp = Started };
    private static AgentActionResult Result(AgentCommandEnvelope command, ActionStatus status) =>
        new(command.CommandId, command.SessionId, command.ConnectionGeneration, command.IntentVersion, status, null);

    private sealed class MemoryIntentStore : IOverlayIntentStore
    {
        private OverlayIntent? value;
        public OverlayIntent? Load() => this.value;
        public void Save(OverlayIntent intent) => this.value = intent;
    }

    private sealed class RecordingHealthSink : IAuthoritativeHealthSink
    {
        public bool RestoreSucceeded { get; private set; }
        public bool CriticalActionsConfirmed { get; private set; }
        public bool HasBlockingIssues { get; private set; }
        public void SetRestoreStatus(bool succeeded) => this.RestoreSucceeded = succeeded;
        public void SetCurrentCriticalActionsConfirmed(bool confirmed) => this.CriticalActionsConfirmed = confirmed;
        public void SetHealthBlockingIssues(bool hasBlockingIssues) => this.HasBlockingIssues = hasBlockingIssues;
    }

    private sealed class RecordingTerminator(ActionStatus status)
    {
        public ObservedProcessTarget? LastTarget { get; private set; }
        public Task<ActionStatus> TerminateAsync(ObservedProcessTarget target)
        {
            this.LastTarget = target;
            return Task.FromResult(status);
        }
    }
}
