namespace ControlParental.Service.Tests;
using ControlParental.Domain;
using Xunit;
public sealed class SessionCoordinatorTests
{
    [Fact]
    public async Task SameSessionWorkIsSerialized()
    {
        await using var coordinator = new SessionEnforcementCoordinator(7, 4);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = 0;
        var maximum = 0;

        await coordinator.PostAsync(async _ =>
        {
            maximum = Math.Max(maximum, Interlocked.Increment(ref running));
            firstStarted.SetResult();
            await gate.Task;
            Interlocked.Decrement(ref running);
        });
        await firstStarted.Task;
        await coordinator.PostAsync(_ =>
        {
            maximum = Math.Max(maximum, Interlocked.Increment(ref running));
            Interlocked.Decrement(ref running);
            return ValueTask.CompletedTask;
        });

        gate.SetResult(); await coordinator.WhenIdleAsync();
        Assert.Equal(1, maximum);
    }

    [Fact]
    public async Task DifferentSessionsRunIndependently()
    {
        await using var blocked = new SessionEnforcementCoordinator(1, 1);
        await using var free = new SessionEnforcementCoordinator(2, 1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await blocked.PostAsync(async _ => await gate.Task);
        await free.PostAsync(_ => { completed.SetResult(); return ValueTask.CompletedTask; });

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(1)); gate.SetResult();
    }

    [Fact]
    public async Task QueueIsBoundedAndCoalescesOnlySafeInputs()
    {
        await using var coordinator = new SessionEnforcementCoordinator(3, 1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await coordinator.PostAsync(async _ => { started.SetResult(); await gate.Task; });
        await started.Task;

        Assert.True(coordinator.TryPostCoalescible(SessionInputKind.Foreground, _ => ValueTask.CompletedTask));
        Assert.False(coordinator.TryPostCoalescible(SessionInputKind.Foreground, _ => ValueTask.CompletedTask));
        Assert.True(coordinator.TryPostCoalescible(SessionInputKind.Tick, _ => ValueTask.CompletedTask));
        await coordinator.PostAsync(_ => ValueTask.CompletedTask);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await coordinator.PostAsync(_ => ValueTask.CompletedTask, cancellation.Token));
        gate.SetResult();
    }

    [Theory]
    [InlineData(SessionInputKind.Foreground)]
    [InlineData(SessionInputKind.Tick)]
    public async Task PendingCoalescibleInputEvaluatesTheLatestState(SessionInputKind kind)
    {
        await using var coordinator = new SessionEnforcementCoordinator(5, 1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var evaluated = new List<string>();
        await coordinator.PostAsync(async _ => { started.SetResult(); await gate.Task; });
        await started.Task;

        Assert.True(coordinator.TryPostCoalescible(
            kind,
            _ => { evaluated.Add("stale"); return ValueTask.CompletedTask; }));
        Assert.False(coordinator.TryPostCoalescible(
            kind,
            _ => { evaluated.Add("latest"); return ValueTask.CompletedTask; }));

        gate.SetResult();
        await coordinator.WhenIdleAsync();

        Assert.Equal(["latest"], evaluated);
    }

    [Fact]
    public async Task SaturatedObservationsPreserveCriticalAdmissionAndOwedInputs()
    {
        await using var coordinator = new SessionEnforcementCoordinator(6, 1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        await coordinator.PostAsync(async _ => { order.Add("restore"); started.SetResult(); await gate.Task; });
        await started.Task;

        Assert.True(coordinator.TryPostCoalescible(
            SessionInputKind.Foreground,
            _ => { order.Add("foreground"); return ValueTask.CompletedTask; }));
        Assert.True(coordinator.TryPostCoalescible(
            SessionInputKind.Tick,
            _ => { order.Add("tick"); return ValueTask.CompletedTask; }));
        await coordinator.PostAsync(
            _ => { order.Add("result"); return ValueTask.CompletedTask; }).AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        gate.SetResult();
        await coordinator.WhenIdleAsync();

        Assert.Equal(["restore", "result", "foreground", "tick"], order);
    }

    [Fact]
    public async Task CoalescedInputsPreserveOwedOrderAndLatestState()
    {
        await using var coordinator = new SessionEnforcementCoordinator(8, 1);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<string>();
        await coordinator.PostAsync(async _ => { started.SetResult(); await gate.Task; });
        await started.Task;

        Assert.True(coordinator.TryPostCoalescible(
            SessionInputKind.Tick,
            _ => { order.Add("stale-tick"); return ValueTask.CompletedTask; }));
        Assert.True(coordinator.TryPostCoalescible(
            SessionInputKind.Foreground,
            _ => { order.Add("foreground"); return ValueTask.CompletedTask; }));
        Assert.False(coordinator.TryPostCoalescible(
            SessionInputKind.Tick,
            _ => { order.Add("latest-tick"); return ValueTask.CompletedTask; }));

        gate.SetResult();
        await coordinator.WhenIdleAsync();

        Assert.Equal(["latest-tick", "foreground"], order);
    }

    [Fact]
    public async Task ShutdownCancelsRunningWorkAndRejectsNewWork()
    {
        var coordinator = new SessionEnforcementCoordinator(4, 1);
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await coordinator.PostAsync(async token =>
        {
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { cancelled.SetResult(); }
        });

        await coordinator.DisposeAsync();

        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await coordinator.PostAsync(_ => ValueTask.CompletedTask));
    }

    [Fact]
    public async Task PortReplacementCompletesOldCommandWithoutUsingOldResult()
    {
        var port = new AgentCommandPort();
        port.Replace(1, (_, _) => ValueTask.CompletedTask);
        var command = Command(generation: 1);
        var pending = port.ExecuteAsync(command).AsTask();

        port.Replace(2, (_, _) => ValueTask.CompletedTask);

        Assert.Equal(ActionStatus.ConnectionReplaced, (await pending).Status);
        Assert.False(port.TryAccept(Result(command, ActionStatus.Confirmed)));
        Assert.Equal(2, port.Generation);
    }

    [Fact]
    public async Task MissingResultTimesOut()
    {
        var port = new AgentCommandPort();
        port.Replace(1, (_, _) => ValueTask.CompletedTask);
        var command = Command(generation: 1, deadline: DateTimeOffset.UtcNow.AddMilliseconds(30));

        var result = await port.ExecuteAsync(command);

        Assert.Equal(ActionStatus.TimedOut, result.Status);
    }

    [Fact]
    public async Task TimedOutAttemptRetriesWithFreshCommandIdentity()
    {
        var port = new AgentCommandPort(maxAttempts: 3, retryDelay: TimeSpan.Zero);
        var sent = new List<AgentCommandEnvelope>();
        port.Replace(1, (attempt, _) =>
        {
            sent.Add(attempt);
            if (sent.Count == 3)
            {
                Assert.True(port.TryAccept(Result(attempt, ActionStatus.Confirmed)));
            }

            return ValueTask.CompletedTask;
        });
        var command = Command(generation: 1, deadline: DateTimeOffset.UtcNow.AddSeconds(2));

        var result = await port.ExecuteAsync(command);

        Assert.Equal(ActionStatus.Confirmed, result.Status);
        Assert.Equal(3, sent.Count);
        Assert.Equal(3, sent.Select(item => item.CommandId).Distinct().Count());
        Assert.All(sent, item =>
        {
            Assert.Equal(command.SessionId, item.SessionId);
            Assert.Equal(command.ConnectionGeneration, item.ConnectionGeneration);
            Assert.Equal(command.IntentVersion, item.IntentVersion);
        });
    }

    [Fact]
    public async Task MissingResultsStopAtConfiguredAttemptBound()
    {
        var port = new AgentCommandPort(maxAttempts: 2, retryDelay: TimeSpan.Zero);
        var sent = new List<AgentCommandEnvelope>();
        port.Replace(1, (attempt, _) =>
        {
            sent.Add(attempt);
            Assert.True(port.TryAccept(Result(attempt, ActionStatus.TimedOut)));
            return ValueTask.CompletedTask;
        });
        var command = Command(generation: 1, deadline: DateTimeOffset.UtcNow.AddSeconds(2));

        var result = await port.ExecuteAsync(command);

        Assert.Equal(ActionStatus.TimedOut, result.Status);
        Assert.Equal(2, sent.Count);
        Assert.Equal(2, sent.Select(item => item.CommandId).Distinct().Count());
    }

    [Fact]
    public async Task ExpiredCommandIsNotSentOrRetried()
    {
        var port = new AgentCommandPort();
        var sendCount = 0;
        port.Replace(1, (_, _) =>
        {
            Interlocked.Increment(ref sendCount);
            return ValueTask.CompletedTask;
        });

        var result = await port.ExecuteAsync(Command(1, DateTimeOffset.UtcNow.AddMilliseconds(-1)));

        Assert.Equal(ActionStatus.TimedOut, result.Status);
        Assert.Equal(0, sendCount);
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(7, 2)]
    public async Task WrongSessionOrGenerationResultIsIgnored(int sessionId, long generation)
    {
        var port = new AgentCommandPort();
        port.Replace(1, (_, _) => ValueTask.CompletedTask);
        var command = Command(generation: 1);
        var pending = port.ExecuteAsync(command).AsTask();

        Assert.False(port.TryAccept(Result(command, ActionStatus.Confirmed) with
        {
            SessionId = sessionId,
            ConnectionGeneration = generation,
        }));
        Assert.True(port.TryAccept(Result(command, ActionStatus.Confirmed)));
        Assert.Equal(ActionStatus.Confirmed, (await pending).Status);
    }

    [Fact]
    public async Task DuplicateAndStaleIntentResultsAreIgnored()
    {
        var port = new AgentCommandPort();
        port.Replace(1, (_, _) => ValueTask.CompletedTask);
        var command = Command(generation: 1);
        var pending = port.ExecuteAsync(command).AsTask();

        Assert.False(port.TryAccept(Result(command, ActionStatus.Confirmed) with { IntentVersion = 9 }));
        var accepted = Result(command, ActionStatus.Confirmed);
        Assert.True(port.TryAccept(accepted));
        Assert.False(port.TryAccept(accepted));
        Assert.Equal(ActionStatus.Confirmed, (await pending).Status);
    }

    [Fact]
    public async Task ConcurrentDuplicateCommandIsRejectedWithoutDuplicateSend()
    {
        var port = new AgentCommandPort();
        var sendCount = 0;
        port.Replace(1, (_, _) => { Interlocked.Increment(ref sendCount); return ValueTask.CompletedTask; });
        var command = Command(generation: 1);
        var pending = port.ExecuteAsync(command).AsTask();

        var duplicate = await port.ExecuteAsync(command);

        Assert.Equal(ActionStatus.Stale, duplicate.Status);
        Assert.Equal(1, sendCount);
        Assert.True(port.TryAccept(Result(command, ActionStatus.Confirmed)));
        Assert.Equal(ActionStatus.Confirmed, (await pending).Status);
    }

    private static AgentCommandEnvelope Command(long generation, DateTimeOffset? deadline = null) =>
        new(Guid.NewGuid(), 7, generation, 10, AgentCommandKind.ShowOverlay, deadline ?? DateTimeOffset.UtcNow.AddSeconds(2));

    private static AgentActionResult Result(AgentCommandEnvelope command, ActionStatus status) =>
        new(command.CommandId, command.SessionId, command.ConnectionGeneration, command.IntentVersion, status, null);
}
