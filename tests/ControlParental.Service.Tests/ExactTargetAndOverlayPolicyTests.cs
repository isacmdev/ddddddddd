namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Xunit;

public sealed class ExactTargetAndOverlayPolicyTests
{
    private static readonly DateTimeOffset Started = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AppIdAloneCannotAuthorizeTermination()
    {
        var terminator = new ProcessTerminator(new FakeProcess());
        Assert.False(await terminator.TerminateAsync("notepad", "blocked"));
    }

    [Fact]
    public async Task ExactTargetTerminatesAndWaitsAsynchronously()
    {
        var process = new FakeProcess();
        var status = await new ProcessTerminator(process).TerminateAsync(Target());
        Assert.Equal(ActionStatus.Confirmed, status);
        Assert.True(process.CloseCalled);
    }

    [Theory]
    [InlineData(9, 4, 0)]
    [InlineData(8, 5, 0)]
    [InlineData(8, 4, 1)]
    public async Task IdentityMismatchAndPidReuseAreRejected(int pid, int session, int seconds)
    {
        var process = new FakeProcess { Identity = new(pid, session, Started.AddSeconds(seconds), "notepad") };
        Assert.Equal(ActionStatus.InvalidTarget, await new ProcessTerminator(process).TerminateAsync(Target()));
        Assert.False(process.CloseCalled);
    }

    [Theory]
    [InlineData(true, false, ActionStatus.HarmlessAbsence)]
    [InlineData(false, true, ActionStatus.AccessDenied)]
    public async Task ExitAndInaccessibleMetadataAreTyped(bool exited, bool denied, ActionStatus expected)
    {
        var process = new FakeProcess { Exited = exited, DenyMetadata = denied };
        Assert.Equal(expected, await new ProcessTerminator(process).TerminateAsync(Target()));
    }

    [Fact]
    public async Task ProtectedProcessIsNeverTargeted()
    {
        var process = new FakeProcess { Identity = new(8, 4, Started, "winlogon") };
        Assert.Equal(ActionStatus.InvalidTarget, await new ProcessTerminator(process).TerminateAsync(Target()));
        Assert.False(process.CloseCalled);
    }

    [Fact]
    public async Task IntentRestoresAndConvergesAfterAbsentOrReplacedAgent()
    {
        var store = new MemoryIntentStore();
        var first = new OverlayIntentPolicy(store);
        first.SetDesired(true, "limit", "Ask");
        var restored = new OverlayIntentPolicy(store);
        Assert.Equal(first.Intent, restored.Intent);
        Assert.Equal(ActionStatus.ConnectionReplaced, await restored.ReconcileAsync(4, null));

        var port = new AgentCommandPort();
        AgentCommandEnvelope? sent = null;
        port.Replace(2, (command, _) => { sent = command; return ValueTask.CompletedTask; });
        var pending = restored.ReconcileAsync(4, port).AsTask();
        await WaitUntilAsync(() => sent is not null);
        Assert.True(port.TryAccept(Result(sent!, ActionStatus.Confirmed)));
        Assert.Equal(ActionStatus.Confirmed, await pending);
        Assert.Equal(ActionStatus.HarmlessAbsence, await restored.ReconcileAsync(4, port));

        port.Replace(3, (command, _) => { sent = command; return ValueTask.CompletedTask; });
        sent = null;
        pending = restored.ReconcileAsync(4, port).AsTask();
        await WaitUntilAsync(() => sent is not null);
        Assert.True(port.TryAccept(Result(sent!, ActionStatus.Confirmed)));
        Assert.Equal(ActionStatus.Confirmed, await pending);
    }

    [Fact]
    public async Task DuplicateAndStaleIntentCannotBecomeDelivered()
    {
        var store = new MemoryIntentStore();
        var policy = new OverlayIntentPolicy(store);
        policy.SetDesired(true, "limit");
        var version = policy.Intent.Version;
        policy.SetDesired(true, "limit");
        Assert.Equal(version, policy.Intent.Version);
        var port = new AgentCommandPort();
        AgentCommandEnvelope? sent = null;
        port.Replace(1, (command, _) => { sent = command; return ValueTask.CompletedTask; });
        var pending = policy.ReconcileAsync(4, port).AsTask();
        await WaitUntilAsync(() => sent is not null);
        policy.SetDesired(false, "relaxed");
        Assert.True(port.TryAccept(Result(sent!, ActionStatus.Confirmed)));
        Assert.Equal(ActionStatus.Stale, await pending);
    }

    [Fact]
    public async Task OverlayReconciliationEmitsShowReplaceAndClearWithCurrentPayload()
    {
        var policy = new OverlayIntentPolicy(new MemoryIntentStore());
        var sent = new List<AgentCommandEnvelope>();
        var port = new AgentCommandPort();
        port.Replace(5, (command, _) =>
        {
            sent.Add(command);
            Assert.True(port.TryAccept(Result(command, ActionStatus.Confirmed)));
            return ValueTask.CompletedTask;
        });

        policy.SetDesired(true, "limit", "Ask");
        Assert.Equal(ActionStatus.Confirmed, await policy.ReconcileAsync(4, port));
        policy.SetDesired(true, "downtime");
        Assert.Equal(ActionStatus.Confirmed, await policy.ReconcileAsync(4, port));
        policy.SetDesired(false, "allowed");
        Assert.Equal(ActionStatus.Confirmed, await policy.ReconcileAsync(4, port));

        Assert.Equal(
            [AgentCommandKind.ShowOverlay, AgentCommandKind.ReplaceOverlay, AgentCommandKind.ClearOverlay],
            sent.Select(command => command.Command));
        Assert.Equal("limit", sent[0].Overlay!.Reason);
        Assert.Equal("downtime", sent[1].Overlay!.Reason);
        Assert.Equal(policy.Intent, sent[2].Overlay);
    }

    [Fact]
    public async Task PidReuseAfterGracefulCloseDoesNotKillReplacement()
    {
        var process = new FakeProcess
        {
            ExitOnClose = false,
            IdentityAfterWait = new(8, 4, Started.AddMinutes(1), "notepad"),
        };
        var terminator = new ProcessTerminator(process, TimeSpan.FromMilliseconds(1));

        var status = await terminator.TerminateAsync(Target());

        Assert.Equal(ActionStatus.InvalidTarget, status);
        Assert.False(process.KillCalled);
    }

    [Fact]
    public async Task SameAppTickCrossesAndRelaxesThroughCoordinator()
    {
        await using var coordinator = new SessionEnforcementCoordinator(4);
        var blocked = false;
        var decisions = new List<bool>();
        var reevaluator = new ThresholdReevaluator(coordinator, (_, _) =>
        {
            decisions.Add(blocked);
            return ValueTask.CompletedTask;
        });
        reevaluator.Observe("app");
        await coordinator.WhenIdleAsync();
        blocked = true; Assert.True(reevaluator.Tick()); await coordinator.WhenIdleAsync();
        blocked = false; Assert.True(reevaluator.Tick()); await coordinator.WhenIdleAsync();
        Assert.Equal([false, true, false], decisions);
    }

    private static ObservedProcessTarget Target() => new(8, 4, Started);
    private static AgentActionResult Result(AgentCommandEnvelope command, ActionStatus status) =>
        new(command.CommandId, command.SessionId, command.ConnectionGeneration, command.IntentVersion, status, null);
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++) await Task.Delay(1);
        Assert.True(condition());
    }

    private sealed class MemoryIntentStore : IOverlayIntentStore
    {
        public OverlayIntent? Value { get; private set; }
        public OverlayIntent? Load() => this.Value;
        public void Save(OverlayIntent intent) => this.Value = intent;
    }

    private sealed class FakeProcess : IExactProcessHandle
    {
        public ProcessIdentity Identity { get; set; } = new(8, 4, Started, "notepad");
        public bool Exited { get; set; }
        public bool DenyMetadata { get; set; }
        public bool ExitOnClose { get; set; } = true;
        public ProcessIdentity? IdentityAfterWait { get; set; }
        public bool CloseCalled { get; private set; }
        public bool KillCalled { get; private set; }
        public ProcessIdentity ReadIdentity() => this.DenyMetadata ? throw new UnauthorizedAccessException() : this.Identity;
        public bool HasExited => this.Exited;
        public bool CloseMainWindow() { this.CloseCalled = true; this.Exited = this.ExitOnClose; return true; }
        public void Kill() { this.KillCalled = true; this.Exited = true; }
        public async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            if (this.Exited)
            {
                return;
            }

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            finally
            {
                this.Identity = this.IdentityAfterWait ?? this.Identity;
            }
        }
        public void Dispose() { }
    }
}
