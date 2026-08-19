// <copyright file="SessionCoordinatorPerformanceEvidenceTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using ControlParental.Domain;
using Xunit;

public sealed class SessionCoordinatorPerformanceEvidenceTests
{
    private const int Capacity = 256;
    private const int AllocationWarmup = 2_000;
    private const int AllocationSamples = 20_000;
    private const int LatencyWarmup = 500;
    private const int LatencySamples = 2_000;
    private const int TimerSamples = 10;
    private static readonly TimeSpan TimerPeriod = TimeSpan.FromSeconds(1);

    [Fact]
    [Trait("Category", "PerformanceEvidence")]
    public async Task FiniteCoordinatorLoadProducesReproducibleResourceEvidence()
    {
        var evidencePath = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_PERFORMANCE_EVIDENCE");
        if (string.IsNullOrWhiteSpace(evidencePath))
        {
            return;
        }

        var startedAt = DateTimeOffset.UtcNow;
        var allocationRuns = new List<Distribution>(3);
        var latencyRuns = new List<Distribution>(3);
        var timerRuns = new List<Distribution>(3);

        for (var run = 0; run < 3; run++)
        {
            allocationRuns.Add(await MeasureAllocationsAsync());
            latencyRuns.Add(await MeasureLatencyAsync());
            timerRuns.Add(await MeasureTimerDriftAsync());
        }

        var queueRuns = new[]
        {
            await MeasureQueueBoundAsync(),
            await MeasureQueueBoundAsync(),
        };

        Assert.All(queueRuns, result =>
        {
            Assert.Equal(Capacity, result.HighWater);
            Assert.True(result.ExtraProducerBlocked);
            Assert.True(result.Drained);
        });

        Assert.All(allocationRuns, result => Assert.Equal(AllocationSamples, result.SampleSize));
        Assert.All(latencyRuns, result => Assert.Equal(LatencySamples, result.SampleSize));
        Assert.All(timerRuns, result => Assert.Equal(TimerSamples, result.SampleSize));

        var evidence = new
        {
            schemaVersion = 1,
            startedAtUtc = startedAt,
            finishedAtUtc = DateTimeOffset.UtcNow,
            environment = new
            {
                os = RuntimeInformation.OSDescription,
                runtime = RuntimeInformation.FrameworkDescription,
                architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                processorCount = Environment.ProcessorCount,
                stopwatchFrequency = Stopwatch.Frequency,
                commit = Git("rev-parse HEAD"),
                worktreeContext = Git("status --short").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length +
                    " modified/untracked entries; cleanliness is not assumed",
            },
            command = Environment.GetEnvironmentVariable("CONTROL_PARENTAL_PERFORMANCE_COMMAND") ??
                "dotnet test --filter FullyQualifiedName~SessionCoordinatorPerformanceEvidenceTests",
            exitCode = 0,
            parameters = new
            {
                executorTimeoutSeconds = 120,
                testHostHangTimeoutSeconds = 60,
                repetitions = 3,
                allocationWarmup = AllocationWarmup,
                allocationSamples = AllocationSamples,
                latencyWarmup = LatencyWarmup,
                latencySamples = LatencySamples,
                queueCapacity = Capacity,
                queueRepetitions = queueRuns.Length,
                timerPeriodMs = TimerPeriod.TotalMilliseconds,
                timerSamples = TimerSamples,
            },
            methodology = new
            {
                allocations = "Production SessionSafetyLoop unchanged-decision ticks; loop setup and warmup precede forced GC and the measured window; GC total allocated bytes divided by fully processed ticks.",
                queue = "A running item is gated, exactly 256 critical items are admitted, and the next producer must block. The load is drained and repeated on a fresh coordinator.",
                timer = "Task.Delay periodic strategy equivalent to production is measured with monotonic Stopwatch timestamps; drift is actual interval minus requested period.",
                latency = "End-to-end time starts before SessionSafetyLoop.Tick and ends when its coordinator is idle after the unchanged O(1) decision; continuous sequential load and warmup are finite.",
            },
            allocationsBytesPerEvent = allocationRuns,
            queue = queueRuns,
            timerDriftMicroseconds = timerRuns,
            processingLatencyMicroseconds = latencyRuns,
            summary = new
            {
                bytesPerEventMedianOfRuns = Percentile(allocationRuns.Select(item => item.P50).ToArray(), 0.50),
                queueHighWaterMaximum = queueRuns.Max(item => item.HighWater),
                timerDriftP50 = Percentile(timerRuns.SelectMany(item => item.Values).ToArray(), 0.50),
                timerDriftP95 = Percentile(timerRuns.SelectMany(item => item.Values).ToArray(), 0.95),
                timerDriftMaximum = timerRuns.Max(item => item.Maximum),
                processingLatencyP50 = Percentile(latencyRuns.SelectMany(item => item.Values).ToArray(), 0.50),
                processingLatencyP95 = Percentile(latencyRuns.SelectMany(item => item.Values).ToArray(), 0.95),
                processingLatencyMaximum = latencyRuns.Max(item => item.Maximum),
            },
        };

        await File.WriteAllTextAsync(
            evidencePath,
            JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<Distribution> MeasureAllocationsAsync()
    {
        await using var loop = await CreateSafetyLoopAsync();
        for (var index = 0; index < AllocationWarmup; index++)
        {
            Assert.True(loop.Tick());
            await loop.WhenIdleAsync();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetTotalAllocatedBytes(precise: true);
        for (var index = 0; index < AllocationSamples; index++)
        {
            Assert.True(loop.Tick());
            await loop.WhenIdleAsync();
        }

        var bytesPerEvent = (GC.GetTotalAllocatedBytes(precise: true) - before) / (double)AllocationSamples;
        return Distribution.Single(AllocationSamples, bytesPerEvent);
    }

    private static async Task<Distribution> MeasureLatencyAsync()
    {
        await using var loop = await CreateSafetyLoopAsync();
        for (var index = 0; index < LatencyWarmup; index++)
        {
            Assert.True(loop.Tick());
            await loop.WhenIdleAsync();
        }

        var values = new double[LatencySamples];
        for (var index = 0; index < values.Length; index++)
        {
            var began = Stopwatch.GetTimestamp();
            Assert.True(loop.Tick());
            await loop.WhenIdleAsync();
            values[index] = Stopwatch.GetElapsedTime(began).TotalMicroseconds;
        }

        return Distribution.From(values);
    }

    private static async Task<SessionSafetyLoop> CreateSafetyLoopAsync()
    {
        var decision = Task.FromResult(new EnforcementResult
        {
            Success = true,
            Blocked = false,
            Timestamp = DateTimeOffset.UtcNow,
        });
        SessionSafetyLoop? loop = null;
        loop = new SessionSafetyLoop(
            1,
            (_, _) => decision,
            (_, _) => Task.FromResult(ActionStatus.HarmlessAbsence),
            new MemoryIntentStore(),
            new NullHealthSink());
        await loop.AttachAgentAsync(1, (command, _) =>
        {
            loop.AcceptResult(new AgentActionResult(
                command.CommandId,
                command.SessionId,
                command.ConnectionGeneration,
                command.IntentVersion,
                ActionStatus.Confirmed,
                null));
            return ValueTask.CompletedTask;
        });
        loop.Observe(new ForegroundChanged("performance.app"));
        await loop.WhenIdleAsync();
        return loop;
    }

    private static async Task<Distribution> MeasureTimerDriftAsync()
    {
        var values = new double[TimerSamples];
        var previous = Stopwatch.GetTimestamp();
        for (var index = 0; index < values.Length; index++)
        {
            await Task.Delay(TimerPeriod);
            var current = Stopwatch.GetTimestamp();
            values[index] = (Stopwatch.GetElapsedTime(previous, current) - TimerPeriod).TotalMicroseconds;
            previous = current;
        }

        return Distribution.From(values);
    }

    private static async Task<QueueResult> MeasureQueueBoundAsync()
    {
        await using var coordinator = new SessionEnforcementCoordinator(3, Capacity);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await coordinator.PostAsync(async _ =>
        {
            started.SetResult();
            await gate.Task;
        });
        await started.Task;

        for (var index = 0; index < Capacity; index++)
        {
            await coordinator.PostAsync(_ => ValueTask.CompletedTask);
        }

        var extra = coordinator.PostAsync(_ => ValueTask.CompletedTask).AsTask();
        await Task.Delay(50);
        var blocked = !extra.IsCompleted;
        gate.SetResult();
        await extra.WaitAsync(TimeSpan.FromSeconds(2));
        await coordinator.WhenIdleAsync().WaitAsync(TimeSpan.FromSeconds(2));
        return new(Capacity, blocked, coordinator.WhenIdleAsync().IsCompleted);
    }

    private static Distribution Summarize(double[] values)
    {
        Array.Sort(values);
        return new(values.Length, Percentile(values, 0.50), Percentile(values, 0.95), values[^1], values);
    }

    private static double Percentile(double[] values, double percentile)
    {
        Array.Sort(values);
        var index = (int)Math.Ceiling(percentile * values.Length) - 1;
        return values[Math.Clamp(index, 0, values.Length - 1)];
    }

    private static string Git(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("git", arguments)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        return process is null ? "unavailable" : process.StandardOutput.ReadToEnd().Trim();
    }

    private sealed record Distribution(
        int SampleSize,
        double P50,
        double P95,
        double Maximum,
        [property: JsonIgnore] double[] Values)
    {
        public static Distribution From(double[] values) => Summarize(values);

        public static Distribution Single(int sampleSize, double value) =>
            new(sampleSize, value, value, value, new[] { value });
    }

    private sealed record QueueResult(int HighWater, bool ExtraProducerBlocked, bool Drained);

    private sealed class MemoryIntentStore : IOverlayIntentStore
    {
        private OverlayIntent? intent;

        public OverlayIntent? Load() => this.intent;

        public void Save(OverlayIntent intent) => this.intent = intent;
    }

    private sealed class NullHealthSink : IAuthoritativeHealthSink
    {
        public void SetRestoreStatus(bool succeeded) { }

        public void SetCurrentCriticalActionsConfirmed(bool confirmed) { }

        public void SetHealthBlockingIssues(bool hasBlockingIssues) { }
    }
}
