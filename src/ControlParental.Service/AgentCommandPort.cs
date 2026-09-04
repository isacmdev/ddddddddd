namespace ControlParental.Service;

using ControlParental.Domain;

public sealed class AgentCommandPort : IAgentCommandPort
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, PendingCommand> pending = [];
    private readonly HashSet<Guid> activeCommands = [];
    private readonly int maxAttempts;
    private readonly TimeSpan retryDelay;
    private Func<AgentCommandEnvelope, CancellationToken, ValueTask>? sender;
    private long generation;

    public AgentCommandPort(int maxAttempts = 3, TimeSpan? retryDelay = null)
    {
        if (maxAttempts is < 1 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));
        }

        this.maxAttempts = maxAttempts;
        this.retryDelay = retryDelay ?? TimeSpan.FromMilliseconds(50);
    }

    public long Generation => Interlocked.Read(ref this.generation);

    public void Replace(long generation, Func<AgentCommandEnvelope, CancellationToken, ValueTask> sender)
    {
        List<PendingCommand> replaced;
        lock (this.sync)
        {
            this.sender = sender;
            Interlocked.Exchange(ref this.generation, generation);
            replaced = [.. this.pending.Values];
            this.pending.Clear();
        }

        foreach (var item in replaced)
        {
            item.Completion.TrySetResult(CreateResult(item.Command, ActionStatus.ConnectionReplaced));
        }
    }

    public void Disconnect(long generation)
    {
        this.Replace(generation, static (_, _) => ValueTask.FromException(
            new InvalidOperationException("The agent is disconnected.")));
    }

    public async ValueTask<AgentActionResult> ExecuteAsync(
        AgentCommandEnvelope command, CancellationToken cancellationToken = default)
    {
        lock (this.sync)
        {
            if (this.sender is null || command.ConnectionGeneration != this.Generation)
            {
                return CreateResult(command, ActionStatus.ConnectionReplaced);
            }

            if (!this.activeCommands.Add(command.CommandId))
            {
                return CreateResult(command, ActionStatus.Stale);
            }
        }

        try
        {
            for (var attemptNumber = 1; attemptNumber <= this.maxAttempts; attemptNumber++)
            {
                var attempt = attemptNumber == 1 ? command : command with { CommandId = Guid.NewGuid() };
                if (DateTimeOffset.UtcNow >= attempt.Deadline)
                {
                    return CreateResult(attempt, ActionStatus.TimedOut);
                }

                var result = await this.ExecuteAttemptAsync(attempt, attemptNumber, cancellationToken);
                if (result.Status != ActionStatus.TimedOut || attemptNumber == this.maxAttempts)
                {
                    return result;
                }

                var delay = TimeSpan.FromTicks(this.retryDelay.Ticks * (1L << (attemptNumber - 1)));
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }

            return CreateResult(command, ActionStatus.TimedOut);
        }
        finally
        {
            lock (this.sync) { this.activeCommands.Remove(command.CommandId); }
        }
    }

    private async ValueTask<AgentActionResult> ExecuteAttemptAsync(
        AgentCommandEnvelope command,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        PendingCommand item;
        Func<AgentCommandEnvelope, CancellationToken, ValueTask>? currentSender;
        lock (this.sync)
        {
            currentSender = this.sender;
            if (currentSender is null || command.ConnectionGeneration != this.Generation)
            {
                return CreateResult(command, ActionStatus.ConnectionReplaced);
            }

            item = new(command);
            this.pending.Add(command.CommandId, item);
        }

        try
        {
            await currentSender(command, cancellationToken);
            var remaining = command.Deadline - DateTimeOffset.UtcNow;
            var attemptsLeft = this.maxAttempts - attemptNumber + 1;
            var timeout = attemptsLeft > 1
                ? TimeSpan.FromTicks(Math.Max(1, remaining.Ticks / attemptsLeft))
                : remaining;
            return timeout <= TimeSpan.Zero
                ? this.Timeout(command, item)
                : await item.Completion.Task.WaitAsync(timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return this.Timeout(command, item);
        }
        catch when (!cancellationToken.IsCancellationRequested)
        {
            return CreateResult(command, ActionStatus.NativeFailure);
        }
        finally
        {
            lock (this.sync) { this.pending.Remove(command.CommandId, out _); }
        }
    }

    public bool TryAccept(AgentActionResult result)
    {
        PendingCommand? item;
        lock (this.sync)
        {
            if (result.ConnectionGeneration != this.Generation ||
                !this.pending.TryGetValue(result.CommandId, out item) ||
                result.SessionId != item.Command.SessionId ||
                result.ConnectionGeneration != item.Command.ConnectionGeneration ||
                result.IntentVersion != item.Command.IntentVersion ||
                DateTimeOffset.UtcNow > item.Command.Deadline)
            {
                return false;
            }

            this.pending.Remove(result.CommandId);
        }

        return item.Completion.TrySetResult(result);
    }

    private AgentActionResult Timeout(AgentCommandEnvelope command, PendingCommand item)
    {
        lock (this.sync) { this.pending.Remove(command.CommandId); }
        var result = CreateResult(command, ActionStatus.TimedOut);
        item.Completion.TrySetResult(result);
        return result;
    }

    private static AgentActionResult CreateResult(AgentCommandEnvelope command, ActionStatus status) =>
        new(command.CommandId, command.SessionId, command.ConnectionGeneration, command.IntentVersion, status, null);

    private sealed record PendingCommand(AgentCommandEnvelope Command)
    {
        public TaskCompletionSource<AgentActionResult> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
