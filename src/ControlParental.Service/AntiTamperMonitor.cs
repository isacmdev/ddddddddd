// <copyright file="AntiTamperMonitor.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T13 — Implementación de IAntiTamperMonitor.
/// Detecta y reporta intentos de manipulación.
/// </summary>
public sealed class AntiTamperMonitor : IAntiTamperMonitor, IDisposable
{
    private readonly ITimeProvider timeProvider;
    private readonly IOutboxManager outboxManager;
    private readonly IPrivilegeInspector privilegeInspector;
    private readonly IEnforcementLevelMonitor enforcementLevelMonitor;
    private readonly IIntegrityChecker integrityChecker;
    private readonly IBackendClient backendClient;
    private readonly IIntegrityVerdictHandler verdictHandler;
    private readonly IIntegrityEscalationStateStore? stateStore;
    private readonly Action<TamperEvent>? onTamperDetected;

    private bool disposed;
    private readonly object lockObject = new();
    private Generation? generation;
    private int generationAllocations;
    private readonly Func<CancellationToken, ValueTask<bool>>? tickSource;
    private readonly IBackendIdentityCoordinator? identityCoordinator;
    private readonly List<TamperEvent> detectedEvents = new();
    private string currentTimezone;
    private long lastMonotonicTick;
    private DateTimeOffset lastWallClockTime;
    private bool clockJumpDetected;
    private bool timezoneChangedDetected;
    private readonly AsyncLocal<Generation?> callbackGeneration = new();
    internal Func<Task>? AdmissionCompletionRemovalGapForTesting { get; set; }
    private sealed class Generation
    {
        internal readonly CancellationTokenSource Cancellation;
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal readonly TaskCompletionSource InitialCheck = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource Lifecycle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<Exception> EffectFault = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task? InitialOperation;
        internal readonly HashSet<Task> OwnedTasks = new();
        internal readonly Func<CancellationToken, ValueTask<bool>>? TickSource;
        internal Timer? TimezoneTimer; internal PeriodicTimer? TickTimer; internal Task? Loop; internal Task? Drain;
        internal int CancellationDisposals; internal int GateDisposals; internal int TickTimerDisposals; internal int TimezoneTimerDisposals;
        internal bool AdmissionOpen = true; internal string Timezone = TimeZoneInfo.Local.Id;
        internal EffectProgress? ReactionProgress;
        internal EffectProgress? NotificationProgress;
        internal PendingRetry? PendingRetry;
        internal IntegrityEscalationState? DurableState;
        internal bool Rehydrated;
        internal Generation(CancellationToken cancellationToken, Func<CancellationToken, ValueTask<bool>>? tickSource) { this.TickSource = tickSource; this.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); }
    }
    private sealed record EffectProgress(DecisionShape Shape, string Key);
    private sealed record DecisionShape(string Scope, long Epoch, long Sequence, string ReactionKey, string ReactionKind, bool HasNotification, string? NotificationKey, string? NotificationKind);
    private sealed record PendingRetry(bool ReactionDomain, DecisionShape Shape);
    private sealed record Admission(Task Task, TaskCompletionSource Gate);

    // Thresholds
    private const double MaxAllowedClockDriftSeconds = 300; // 5 minutes
    private const double MaxAllowedClockJumpSeconds = 60; // 1 minute
    private const int MonitorIntervalSeconds = 30;
    private const int TimezoneCheckIntervalSeconds = 60;

    /// <summary>
    /// Initializes a new instance of the <see cref="AntiTamperMonitor"/> class.
    /// </summary>
    public AntiTamperMonitor(
        ITimeProvider timeProvider,
        IOutboxManager outboxManager,
        IPrivilegeInspector privilegeInspector,
        IEnforcementLevelMonitor enforcementLevelMonitor,
        IIntegrityChecker integrityChecker,
        IBackendClient backendClient,
        IIntegrityVerdictHandler verdictHandler,
        Action<TamperEvent>? onTamperDetected = null,
        IBackendIdentityCoordinator? identityCoordinator = null,
        Func<CancellationToken, ValueTask<bool>>? tickSource = null,
        IIntegrityEscalationStateStore? stateStore = null)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.outboxManager = outboxManager ?? throw new ArgumentNullException(nameof(outboxManager));
        this.privilegeInspector = privilegeInspector ?? throw new ArgumentNullException(nameof(privilegeInspector));
        this.enforcementLevelMonitor = enforcementLevelMonitor ?? throw new ArgumentNullException(nameof(enforcementLevelMonitor));
        this.integrityChecker = integrityChecker ?? throw new ArgumentNullException(nameof(integrityChecker));
        this.backendClient = backendClient ?? throw new ArgumentNullException(nameof(backendClient));
        this.verdictHandler = verdictHandler ?? throw new ArgumentNullException(nameof(verdictHandler));
        this.onTamperDetected = onTamperDetected ?? (_ => { });
        this.identityCoordinator = identityCoordinator;
        this.tickSource = tickSource;
        this.stateStore = stateStore;
        this.currentTimezone = TimeZoneInfo.Local.Id;
        this.lastMonotonicTick = timeProvider.MonotonicNow;
        this.lastWallClockTime = timeProvider.WallClockNow;
    }

    /// <inheritdoc />
    public IReadOnlyList<TamperEvent> DetectedEvents
    {
        get
        {
            lock (this.lockObject)
            {
                return this.detectedEvents.ToList().AsReadOnly();
            }
        }
    }

    /// <inheritdoc />
    public string CurrentTimezone
    {
        get
        {
            lock (this.lockObject)
            {
                return this.currentTimezone;
            }
        }
    }

    /// <inheritdoc />
    public bool ClockJumpDetected
    {
        get
        {
            lock (this.lockObject)
            {
                return this.clockJumpDetected;
            }
        }
    }

    /// <inheritdoc />
    public bool TimezoneChangedDetected
    {
        get
        {
            lock (this.lockObject)
            {
                return this.timezoneChangedDetected;
            }
        }
    }

    internal Task LifecycleTask => this.generation?.Lifecycle.Task ?? Task.CompletedTask;

    /// <inheritdoc />
    public event EventHandler<TamperEventArgs>? TamperDetected;

    /// <inheritdoc />
    public event EventHandler<TimezoneChangedEventArgs>? TimezoneChanged;

    /// <inheritdoc />
    public event EventHandler<ClockJumpEventArgs>? OnClockJumpDetected;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task initialization; Task? stopping = null; Admission? release = null;
            lock (this.lockObject)
            {
                if (this.disposed) throw new ObjectDisposedException(nameof(AntiTamperMonitor));
                var current = this.generation;
                if (current is not null && current.AdmissionOpen) initialization = current.InitialCheck.Task;
                else if (current is not null) { stopping = current.Drain!; initialization = Task.CompletedTask; }
                else
                {
                    current = new Generation(cancellationToken, this.tickSource)
                    {
                        TimezoneTimer = new Timer(_ => this.AdmitTimezoneCheck(current), null, TimeSpan.FromSeconds(TimezoneCheckIntervalSeconds), TimeSpan.FromSeconds(TimezoneCheckIntervalSeconds)),
                        TickTimer = this.tickSource is null ? new PeriodicTimer(TimeSpan.FromSeconds(MonitorIntervalSeconds)) : null,
                    };
                    this.generation = current; this.generationAllocations++; this.currentTimezone = current.Timezone;
                    this.lastMonotonicTick = this.timeProvider.MonotonicNow; this.lastWallClockTime = this.timeProvider.WallClockNow;
                    var initial = this.AdmitLocked(current, () => this.RunOwnedStartupAsync(current), default); current.InitialOperation = initial.Task; current.Loop = this.RunMonitorLoopAsync(current); initialization = current.InitialCheck.Task; release = initial;
                }
            }
            release?.Gate.TrySetResult(); if (stopping is not null) { await stopping.ConfigureAwait(false); continue; }
            await initialization.ConfigureAwait(false); break;
        }
        System.Diagnostics.Debug.WriteLine("[AntiTamperMonitor] Started.");
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        var callbackOwner = this.callbackGeneration.Value;
        Task task; TaskCompletionSource? startDrain = null;
        lock (this.lockObject)
        {
            var current = this.generation;
            if (current is null) task = Task.CompletedTask;
            else
            {
                current.AdmissionOpen = false; current.Cancellation.Cancel();
                current.TimezoneTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                task = this.BeginDrainLocked(current, out startDrain);
            }
        }
        startDrain?.TrySetResult();
        if (!ReferenceEquals(callbackOwner, this.generation)) await task.ConfigureAwait(false);

        System.Diagnostics.Debug.WriteLine("[AntiTamperMonitor] Stopped.");
    }

    private Task BeginDrainLocked(Generation current, out TaskCompletionSource? start)
    {
        if (current.Drain is not null) { start = null; return current.Drain; }
        start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        current.Drain = this.DrainAfterAdmissionAsync(current, start.Task);
        return current.Drain;
    }
    private async Task DrainAfterAdmissionAsync(Generation current, Task start)
    {
        await start.ConfigureAwait(false);
        await this.DrainGenerationAsync(current).ConfigureAwait(false);
    }
    internal Task TriggerIntegrityCheckAsync(bool cancelWithOwner = false)
        => this.TriggerIntegrityCheckAsync(cancelWithOwner, default);

    internal Task TriggerIntegrityCheckAsync(CancellationToken cancellationToken)
        => this.TriggerIntegrityCheckAsync(false, cancellationToken);

    private Task TriggerIntegrityCheckAsync(bool cancelWithOwner, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
        Admission? admission = null;
        lock (this.lockObject)
        {
            if (this.disposed) return Task.CompletedTask;
            var current = this.generation;
            if (current is null || !current.AdmissionOpen) return Task.CompletedTask;
            admission = this.AdmitCheckLocked(current, cancelWithOwner, cancellationToken);
        }
        admission.Gate.TrySetResult(); return admission.Task;
    }

    private Admission AdmitCheckLocked(Generation current, bool cancelWithOwner, CancellationToken cancellationToken = default)
        => this.AdmitLocked(current, () => this.RunOwnedCheckAsync(current, cancelWithOwner, cancellationToken), cancellationToken);
    private Admission AdmitLocked(Generation current, Func<Task> work)
    {
        return this.AdmitLocked(current, work, default);
    }
    private Admission AdmitLocked(Generation current, Func<Task> work, CancellationToken callerToken)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var owned = new TaskCompletionSource(); var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var acquired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var execution = RunAfterGateAsync(gate.Task, acquired, callerToken, work); var removalCompleted = this.AdmissionCompletionRemovalGapForTesting is null ? null : new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = CompleteAdmissionAsync(current, execution, completion, owned, removalCompleted, this.AdmissionCompletionRemovalGapForTesting, callerToken);
        this.AdmitTask(current, owned.Task, removalCompleted); return new(completion.Task, gate);
    }
    private static async Task CompleteAdmissionAsync(Generation current, Task execution, TaskCompletionSource completion, TaskCompletionSource owned, TaskCompletionSource? removalCompleted, Func<Task>? removalGap, CancellationToken callerToken)
    {
        using var registration = callerToken.Register(() => completion.TrySetCanceled(callerToken));
        try { await execution.ConfigureAwait(false); completion.TrySetResult(); owned.TrySetResult(); }
        catch (OperationCanceledException exception) { var token = exception.CancellationToken.CanBeCanceled ? exception.CancellationToken : current.Cancellation.Token; completion.TrySetCanceled(token); owned.TrySetCanceled(token); }
        catch (Exception exception) { current.EffectFault.TrySetResult(exception); completion.TrySetException(exception); owned.TrySetException(exception); if (removalGap is not null) { await removalCompleted!.Task.ConfigureAwait(false); await removalGap().ConfigureAwait(false); } }
    }
    private Task RunAdmittedStageAsync(Generation current, Func<Task> work)
    {
        Admission? admission = null;
        lock (this.lockObject)
        {
            if (!this.disposed && ReferenceEquals(this.generation, current) && current.AdmissionOpen)
            {
                admission = this.AdmitLocked(current, work);
            }
        }

        if (admission is null) return Task.CompletedTask;
        admission.Gate.TrySetResult();
        return admission.Task;
    }
    private Task RunAdmittedSynchronousStageAsync(Generation current, Action work)
        => this.RunAdmittedStageAsync(current, () =>
        {
            var previous = this.callbackGeneration.Value; this.callbackGeneration.Value = current;
            try { work(); } finally { this.callbackGeneration.Value = previous; }
            return Task.CompletedTask;
        });
    private static async Task RunAfterGateAsync(Task gate, TaskCompletionSource acquired, CancellationToken callerToken, Func<Task> work)
    {
        await gate.ConfigureAwait(false);
        if (callerToken.IsCancellationRequested) throw new OperationCanceledException(callerToken);
        acquired.TrySetResult();
        await work().ConfigureAwait(false);
    }
    private void AdmitTimezoneCheck(Generation current)
    {
        Admission? admission = null; lock (this.lockObject) if (!this.disposed && ReferenceEquals(this.generation, current) && current.AdmissionOpen) admission = this.AdmitLocked(current, () => { this.CheckTimezone(current); return Task.CompletedTask; }); admission?.Gate.TrySetResult();
    }
    private void AdmitTask(Generation current, Task task, TaskCompletionSource? removalCompleted = null)
    {
        current.OwnedTasks.Add(task);
        _ = task.ContinueWith(completed => { var fault = completed.Exception?.Flatten().InnerExceptions.FirstOrDefault(e => e is not OperationCanceledException); if (fault is not null) current.Lifecycle.TrySetException(fault); lock (this.lockObject) current.OwnedTasks.Remove(completed); removalCompleted?.TrySetResult(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
    private async Task RunOwnedCheckAsync(Generation current, bool cancelWithOwner, CancellationToken callerToken = default)
    {
        var ownerToken = current.Cancellation.Token; var gate = current.Gate; using var gateCancellation = CancellationTokenSource.CreateLinkedTokenSource(ownerToken, callerToken); var acquired = false;
        try { await gate.WaitAsync(gateCancellation.Token).ConfigureAwait(false); acquired = true; var workToken = cancelWithOwner ? ownerToken : CancellationToken.None; await this.PerformIntegrityCheckAsync(current, workToken, true).ConfigureAwait(false); }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw new OperationCanceledException(callerToken); }
        catch (OperationCanceledException) { throw; }
        finally { if (acquired) gate.Release(); }
    }

    private async Task RunOwnedStartupAsync(Generation current)
    {
        await current.Gate.WaitAsync(current.Cancellation.Token).ConfigureAwait(false);
        try
        {
            try
            {
                await this.RehydrateAsync(current).ConfigureAwait(false);
            }
            catch (Exception) when (!this.IsGenerationActive(current))
            {
                return;
            }
            if (!this.IsGenerationActive(current)) return;
            await this.PerformIntegrityCheckAsync(current, current.Cancellation.Token, true).ConfigureAwait(false);
        }
        finally
        {
            current.Gate.Release();
        }
    }

    private async Task RehydrateAsync(Generation owner)
    {
        if (this.stateStore is null)
        {
            owner.Rehydrated = true;
            return;
        }

        var identity = this.identityCoordinator?.CurrentState.DeviceId ?? "local";
        var state = await this.stateStore.LoadAsync(identity, owner.Cancellation.Token).ConfigureAwait(false);
        if (!this.IsGenerationActive(owner)) return;
        if (state is not null)
        {
            state.Validate();
            if (this.verdictHandler is IntegrityVerdictHandler pureHandler)
            {
                pureHandler.Restore(state);
            }

            owner.DurableState = state;
            await this.ReconcileDurableEffectsAsync(owner, state).ConfigureAwait(false);
            if (!this.IsGenerationActive(owner)) return;
        }

        owner.Rehydrated = true;
    }

    private async Task ReconcileDurableEffectsAsync(Generation owner, IntegrityEscalationState state)
    {
        if (state.PendingReactionId is not null && state.PendingReactionId != state.CompletedReactionId)
        {
            var issue = new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", this.identityCoordinator?.CurrentState.DeviceId);
            if (state.Phase == EscalationPhase.Degraded && state.RecoveryLatch)
            {
                await this.enforcementLevelMonitor.ResolveIssueAsync(issue, "authoritative backend trust verdict", CancellationToken.None).ConfigureAwait(false);
            }
            else
            {
                await this.enforcementLevelMonitor.AddIssueAsync(issue, EnforcementIssueSeverity.Severe, "Integrity reaction", state.PendingReactionId, CancellationToken.None).ConfigureAwait(false);
            }

            if (!this.IsGenerationActive(owner)) return;
            state = state with { CompletedReactionId = state.PendingReactionId };
            await this.SaveStateAsync(owner, state).ConfigureAwait(false);
            if (!this.IsGenerationActive(owner)) return;
        }

        if (state.PendingNotificationId is not null && state.PendingNotificationId != state.CompletedNotificationId)
        {
            await this.outboxManager.EnqueueIntegrityNotificationAsync(
                "integrity_degrade_pending", "Integrity degradation pending", "Integrity escalation requires attention",
                this.timeProvider.WallClockNow, state.PendingNotificationId, CancellationToken.None).ConfigureAwait(false);
            if (!this.IsGenerationActive(owner)) return;
            state = state with { CompletedNotificationId = state.PendingNotificationId };
            await this.SaveStateAsync(owner, state).ConfigureAwait(false);
            if (!this.IsGenerationActive(owner)) return;
        }

        owner.DurableState = state;
    }
    private async Task DrainGenerationAsync(Generation current)
    {
        var failure = await CaptureFailureAsync(current.Loop, current.Cancellation.IsCancellationRequested).ConfigureAwait(false);
        if (current.Loop?.IsFaulted == true && current.Lifecycle.Task.IsFaulted) failure = null;
        while (true) { Task[] owned; lock (this.lockObject) owned = current.OwnedTasks.ToArray(); if (owned.Length == 0) break; failure ??= await CaptureFailureAsync(Task.WhenAll(owned), true).ConfigureAwait(false); }
        if (failure is null && current.EffectFault.Task.Status == TaskStatus.RanToCompletion) failure = current.EffectFault.Task.Result;

        lock (this.lockObject) { if (current.TimezoneTimer is not null) { current.TimezoneTimer.Dispose(); current.TimezoneTimerDisposals++; } if (current.TickTimer is not null) { current.TickTimer.Dispose(); current.TickTimerDisposals++; } current.Gate.Dispose(); current.GateDisposals++; if (ReferenceEquals(this.generation, current)) this.generation = null; }

        current.Cancellation.Dispose(); current.CancellationDisposals++;
        if (failure is not null) throw failure;
    }
    private static async Task<Exception?> CaptureFailureAsync(Task? task, bool ignoreCancellation)
    {
        if (task is null) return null;
        try { await task.ConfigureAwait(false); return null; }
        catch (OperationCanceledException) when (ignoreCancellation) { return null; } catch (Exception exception) { return exception; }
    }
    private async Task RunMonitorLoopAsync(Generation current)
    {
        var cancellationToken = current.Cancellation.Token;
        try
        {
            await current.InitialOperation!.ConfigureAwait(false); cancellationToken.ThrowIfCancellationRequested(); current.InitialCheck.TrySetResult();
            if (current.TickSource is not null) while (await current.TickSource(cancellationToken).ConfigureAwait(false)) this.AdmitPeriodicCheck(current);
            else while (await current.TickTimer!.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) this.AdmitPeriodicCheck(current);
            current.Lifecycle.TrySetResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { current.InitialCheck.TrySetCanceled(cancellationToken); current.Lifecycle.TrySetCanceled(cancellationToken); } catch (Exception exception) { current.InitialCheck.TrySetException(exception); current.Lifecycle.TrySetException(exception); throw; }
    }
    private void AdmitPeriodicCheck(Generation current)
    {
        Admission? admission = null; lock (this.lockObject) if (current.AdmissionOpen && ReferenceEquals(this.generation, current)) admission = this.AdmitCheckLocked(current, false); admission?.Gate.TrySetResult();
    }

    /// <inheritdoc />
    public void RecordServiceStopAttempt(string? reason = null)
    {
        this.RecordTamperEvent(
            TamperEventType.ServiceStopAttempt,
            reason ?? "Service stop attempt detected",
            TamperSeverity.Critical);

        System.Diagnostics.Debug.WriteLine("[AntiTamperMonitor] Service stop attempt recorded.");
    }

    /// <inheritdoc />
    public void RecordAgentDeath(int? exitCode = null)
    {
        var description = exitCode.HasValue
            ? $"Agent death detected with exit code {exitCode}"
            : "Agent death detected";

        this.RecordTamperEvent(
            TamperEventType.AgentKillDetected,
            description,
            TamperSeverity.Severe);

        System.Diagnostics.Debug.WriteLine("[AntiTamperMonitor] Agent death recorded.");
    }

    /// <inheritdoc />
    public void RecordUninstallAttempt(string? packageName = null)
    {
        var description = packageName != null
            ? $"Uninstall attempt detected for package: {packageName}"
            : "Uninstall attempt detected";

        this.RecordTamperEvent(
            TamperEventType.UninstallAttempt,
            description,
            TamperSeverity.Severe);

        System.Diagnostics.Debug.WriteLine("[AntiTamperMonitor] Uninstall attempt recorded.");
    }

    /// <inheritdoc />
    public async Task VerifyClockAgainstServerTimeAsync(
        DateTimeOffset serverTime,
        CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return;
        }

        var localTime = this.timeProvider.WallClockNow;
        var drift = (localTime - serverTime).TotalSeconds;

        if (Math.Abs(drift) > MaxAllowedClockDriftSeconds)
        {
            // Check if this is a suspicious jump (not just normal drift)
            if (Math.Abs(drift) > MaxAllowedClockJumpSeconds)
            {
                this.FireClockJump(drift, drift > 0 ? 1 : -1);
            }

            // Record the clock tamper event
            this.RecordTamperEvent(
                TamperEventType.ClockTamperSuspected,
                $"Clock drift of {drift:F1} seconds detected against server time",
                Math.Abs(drift) > MaxAllowedClockJumpSeconds
                    ? TamperSeverity.Severe
                    : TamperSeverity.Warning);
        }
    }

    private async Task PerformIntegrityCheckAsync(Generation current, CancellationToken cancellationToken, bool propagateFailure = false)
    {
        if (!this.IsGenerationActive(current))
        {
            return;
        }

        try
        {
            await this.CheckClockIntegrityAsync(current, cancellationToken); if (!this.IsGenerationActive(current)) return;
            await this.RunAdmittedStageAsync(current, () => { this.CheckTimezone(current); return Task.CompletedTask; }).ConfigureAwait(false);
            if (!this.IsGenerationActive(current)) return;

            // Check if child became admin
            await this.CheckPrivilegeStatusAsync(current, cancellationToken, propagateFailure); if (!this.IsGenerationActive(current)) return;

            // T23: Check binary integrity (Authenticode + SHA256)
            await this.PerformBinaryIntegrityCheckAsync(current, cancellationToken, propagateFailure);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (propagateFailure) throw;
            System.Diagnostics.Debug.WriteLine($"[AntiTamperMonitor] Integrity check failed: {ex.Message}");
        }
    }

    private async Task CheckClockIntegrityAsync(Generation current, CancellationToken cancellationToken)
    {
        var currentMonotonic = this.timeProvider.MonotonicNow;
        var currentWallClock = this.timeProvider.WallClockNow;
        double jump = 0; var direction = 0;

        lock (this.lockObject)
        {
            var monotonicDelta = currentMonotonic - this.lastMonotonicTick;
            var wallClockDelta = (currentWallClock - this.lastWallClockTime).TotalSeconds;

            // Monotonic time should always increase
            // Wall clock should also increase (unless time was changed)

            // If wall clock went backwards more than a small threshold, it's suspicious
            if (wallClockDelta < -MaxAllowedClockJumpSeconds && monotonicDelta > 0)
            {
                jump = wallClockDelta; direction = -1;
            }
            // If wall clock jumped forward significantly without monotonic increase, suspicious
            else if (wallClockDelta > MaxAllowedClockJumpSeconds && monotonicDelta < wallClockDelta * 1000)
            {
                jump = wallClockDelta; direction = 1;
            }

            this.lastMonotonicTick = currentMonotonic;
            this.lastWallClockTime = currentWallClock;
        }

        if (direction != 0)
        {
            await this.RunAdmittedStageAsync(current, () =>
            {
                this.FireClockJump(jump, direction, current);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
        }

        await Task.CompletedTask;
    }

    private bool IsGenerationActive(Generation current) { lock (this.lockObject) return !this.disposed && ReferenceEquals(this.generation, current) && current.AdmissionOpen; }

    private void CheckTimezone(Generation current)
    {
        if (!this.IsGenerationActive(current))
        {
            return;
        }

        var newTimezone = TimeZoneInfo.Local.Id;

        string? oldTimezone = null;
        lock (this.lockObject)
        {
            if (current.AdmissionOpen && ReferenceEquals(this.generation, current) && !string.Equals(current.Timezone, newTimezone, StringComparison.OrdinalIgnoreCase))
            {
                oldTimezone = current.Timezone;
                current.Timezone = newTimezone;
                this.currentTimezone = newTimezone;
                this.timezoneChangedDetected = true;
            }
        }

        if (oldTimezone is not null)
        {
            this.RecordTamperEvent(
                TamperEventType.TimezoneChanged,
                $"Timezone changed from '{oldTimezone}' to '{newTimezone}'",
                TamperSeverity.Warning,
                current);
            this.FireTimezoneChanged(oldTimezone, newTimezone, current);
            System.Diagnostics.Debug.WriteLine(
                $"[AntiTamperMonitor] Timezone changed: {oldTimezone} -> {newTimezone}");
        }
    }

    private async Task CheckPrivilegeStatusAsync(Generation current, CancellationToken cancellationToken, bool propagateFailure = false)
    {
        if (this.disposed)
        {
            return;
        }

        try
        {
            var isChildStandard = await this.privilegeInspector.IsChildStandardAsync(cancellationToken);

            if (!isChildStandard)
            {
                // Check if this was already detected
                var alreadyDetected = false;
                lock (this.lockObject)
                {
                    alreadyDetected = this.detectedEvents.Any(
                        e => e.Type == TamperEventType.ChildIsAdminDetected);
                }

                if (!alreadyDetected)
                {
                    await this.RunAdmittedSynchronousStageAsync(current, () =>
                    {
                        this.RecordTamperEvent(
                            TamperEventType.ChildIsAdminDetected,
                            "Child account detected with administrator privileges",
                            TamperSeverity.Critical,
                            current);
                    }).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (propagateFailure) throw;
            System.Diagnostics.Debug.WriteLine($"[AntiTamperMonitor] Privilege check failed: {ex.Message}");
        }
    }

    private async Task PerformBinaryIntegrityCheckAsync(Generation current, CancellationToken cancellationToken, bool propagateFailure = false)
    {
        if (this.disposed)
        {
            return;
        }

        try
        {
            var result = await this.integrityChecker.CheckLocalIntegrityAsync(cancellationToken);
            if (!this.IsGenerationActive(current)) return;

            // Build IntegrityReport with binary integrity data
            var report = new IntegrityReport
            {
                ReportHash = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.Create().ComputeHash(
                        System.Text.Encoding.UTF8.GetBytes($"{result.BinaryHash}:{result.ExecutablePath}")))
                    .ToLowerInvariant(),
                Timestamp = this.timeProvider.WallClockNow,
                AgentVersion = typeof(AntiTamperMonitor).Assembly.GetName().Version?.ToString() ?? "unknown",
                Platform = Environment.OSVersion.VersionString,
                SignatureValid = result.IsSignatureValid,
                BinaryHash = result.BinaryHash,
            };

            // R3: Report to backend
            var identity = this.identityCoordinator?.CurrentState;
            var reportResult = await this.backendClient.ReportIntegrityAsync(report, cancellationToken) ?? new(false, null);
            cancellationToken.ThrowIfCancellationRequested();
            if (!this.IsGenerationActive(current)) return;
            if (identity is not null && identity != this.identityCoordinator!.CurrentState)
            {
                return;
            }

            // Local evidence is report-only. It cannot change durable enforcement.
            if (!result.IsSignatureValid)
            {
                if (!this.IsGenerationActive(current)) return;
                var timestamp = this.timeProvider.WallClockNow;
                    await this.RunAdmittedSynchronousStageAsync(current, () =>
                    {
                        _ = this.verdictHandler is IntegrityVerdictHandler pureHandler
                            ? pureHandler.HandleLocalFailureDecision($"Signature invalid for {result.ExecutablePath}", timestamp, identity?.DeviceId).Reaction
                            : this.verdictHandler.HandleLocalFailure($"Signature invalid for {result.ExecutablePath}", timestamp);
                    }).ConfigureAwait(false);
            }

            // T23: Handle server verdict via verdict handler
            if (!this.IsGenerationActive(current)) return;
            VerdictDecision? decision = null;
            VerdictReaction? verdictReaction = null;
            if (this.verdictHandler is IntegrityVerdictHandler pureHandler)
            {
                var decisionTimestamp = this.timeProvider.WallClockNow;
                lock (this.lockObject)
                {
                    if (!this.IsCurrentDecisionLocked(current, identity)) return;
                    decision = pureHandler.HandleVerdictDecision(reportResult.Verdict, reportResult.Success, decisionTimestamp, identity?.DeviceId);
                }
                try { await this.ExecuteDecisionChainAsync(current, decision!, identity).ConfigureAwait(false); }
                catch (Exception exception) when (exception is not OperationCanceledException) { current.EffectFault.TrySetResult(exception); throw; }
                return;
            }

            await this.RunAdmittedSynchronousStageAsync(current, () => verdictReaction = this.verdictHandler.HandleVerdict(reportResult.Verdict, reportResult.Success, this.timeProvider.WallClockNow)).ConfigureAwait(false);
            verdictReaction ??= new(VerdictAction.None, null, null);

            if (reportResult.Success
                && reportResult.Verdict is "trust" or "revoked")
            {
                await this.ProcessVerdictReactionAsync(
                    verdictReaction,
                    $"server verdict: {reportResult.Verdict}",
                    cancellationToken,
                    identity,
                    current);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (propagateFailure) throw;
            System.Diagnostics.Debug.WriteLine($"[AntiTamperMonitor] Binary integrity check failed: {ex.Message}");
        }
    }

    private bool IsCurrentDecisionLocked(Generation owner, BackendIdentityState? identity)
        => !this.disposed && ReferenceEquals(this.generation, owner) && owner.AdmissionOpen
            && (this.identityCoordinator is null || Equals(identity, this.identityCoordinator.CurrentState));

    private async Task ExecuteDecisionChainAsync(Generation owner, VerdictDecision decision, BackendIdentityState? identity)
    {
        var issue = new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", identity?.DeviceId);
        var reaction = decision.Reaction;
        var shape = this.GetDecisionShape(decision);
        this.ValidateAdmission(owner, shape);
        await this.PrepareDurableStateAsync(owner, decision).ConfigureAwait(false);
        if (reaction.IsAuthoritativeRecovery && this.ShouldExecute(owner, shape, decision.ReactionIdempotencyKey, true))
        {
            try
            {
                await this.enforcementLevelMonitor.ResolveIssueAsync(issue, "authoritative backend trust verdict", CancellationToken.None).ConfigureAwait(false);
                await this.SaveEffectProgressAsync(owner, shape, reactionDomain: true).ConfigureAwait(false);
            }
            catch
            {
                owner.PendingRetry = new(true, shape);
                throw;
            }
        }
        else if (reaction.Action is VerdictAction.Limit or VerdictAction.Degrade
            && this.ShouldExecute(owner, shape, decision.ReactionIdempotencyKey, true))
        {
            try
            {
                if (this.stateStore is null)
                {
                    await this.enforcementLevelMonitor.AddIssueAsync(issue, reaction.Severity ?? EnforcementIssueSeverity.Warning, reaction.Reason ?? "Integrity reaction", CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    await this.enforcementLevelMonitor.AddIssueAsync(issue, reaction.Severity ?? EnforcementIssueSeverity.Warning, reaction.Reason ?? "Integrity reaction", decision.ReactionIdempotencyKey, CancellationToken.None).ConfigureAwait(false);
                }
                await this.SaveEffectProgressAsync(owner, shape, reactionDomain: true).ConfigureAwait(false);
            }
            catch
            {
                owner.PendingRetry = new(true, shape);
                throw;
            }
        }

        if (decision.Notification is not null && decision.NotificationIdempotencyKey is not null
            && this.ShouldExecute(owner, shape, decision.NotificationIdempotencyKey, false))
        {
            try
            {
                await this.outboxManager.EnqueueIntegrityNotificationAsync(
                    decision.Notification.Type,
                    decision.Notification.Title,
                    decision.Notification.Body,
                    decision.Notification.Timestamp,
                    decision.NotificationIdempotencyKey,
                    CancellationToken.None).ConfigureAwait(false);
                await this.SaveEffectProgressAsync(owner, shape, reactionDomain: false).ConfigureAwait(false);
                if (owner.PendingRetry is { ReactionDomain: false }) owner.PendingRetry = null;
            }
            catch
            {
                owner.PendingRetry = new(false, shape);
                throw;
            }
        }

        if (owner.PendingRetry is not null && owner.PendingRetry.Shape == shape) owner.PendingRetry = null;
    }

    internal Task ExecuteDecisionAsync(VerdictDecision decision)
    {
        Generation? owner;
        lock (this.lockObject)
        {
            owner = !this.disposed && this.generation is { AdmissionOpen: true } current ? current : null;
            if (owner is null) return Task.CompletedTask;
        }

        return this.RunAdmittedStageAsync(owner, async () =>
        {
            await owner.Gate.WaitAsync().ConfigureAwait(false);
            try { await this.ExecuteDecisionChainAsync(owner, decision, null).ConfigureAwait(false); }
            catch (Exception exception) when (exception is not OperationCanceledException) { owner.EffectFault.TrySetResult(exception); throw; }
            finally { owner.Gate.Release(); }
        });
    }

    private DecisionShape GetDecisionShape(VerdictDecision decision)
        => new(
            decision.IdentityScope,
            decision.Epoch,
            decision.Sequence,
            decision.ReactionIdempotencyKey,
            decision.Reaction.IsAuthoritativeRecovery ? "recovery" : decision.Reaction.Action.ToString(),
            decision.Notification is not null,
            decision.NotificationIdempotencyKey,
            decision.Notification?.Type);

    private void ValidateAdmission(Generation owner, DecisionShape shape)
    {
        var pending = owner.PendingRetry;
        if (pending is not null && pending.Shape != shape)
        {
            throw new InvalidOperationException("A failed integrity effect must be retried before another decision can execute.");
        }

        foreach (var progress in new[] { owner.ReactionProgress, owner.NotificationProgress })
        {
            if (progress is null) continue;
            if (!string.Equals(progress.Shape.Scope, shape.Scope, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A generation cannot contain multiple identity scopes.");
            }

            if (progress.Shape.Epoch == shape.Epoch && progress.Shape.Sequence == shape.Sequence && progress.Shape != shape)
            {
                throw new InvalidOperationException("Conflicting effect identity at the same decision position.");
            }
        }
    }

    private bool ShouldExecute(Generation owner, DecisionShape shape, string key, bool reactionDomain)
    {
        var durableCompleted = reactionDomain ? owner.DurableState?.CompletedReactionId : owner.DurableState?.CompletedNotificationId;
        if (string.Equals(durableCompleted, key, StringComparison.Ordinal)) return false;
        var progress = reactionDomain ? owner.ReactionProgress : owner.NotificationProgress;
        if (progress is null) return true;
        if (shape.Epoch > progress.Shape.Epoch || shape.Epoch == progress.Shape.Epoch && shape.Sequence > progress.Shape.Sequence) return true;
        if (shape.Epoch < progress.Shape.Epoch || shape.Epoch == progress.Shape.Epoch && shape.Sequence < progress.Shape.Sequence) return false;
        if (string.Equals(progress.Key, key, StringComparison.Ordinal)) return false;
        throw new InvalidOperationException("Conflicting effect identity at the same decision position.");
    }

    private async Task PrepareDurableStateAsync(Generation owner, VerdictDecision decision)
    {
        if (this.stateStore is null || this.verdictHandler is not IntegrityVerdictHandler pureHandler) return;
        var prior = owner.DurableState;
        var snapshot = pureHandler.Snapshot() ?? throw new InvalidOperationException("Handler snapshot was null.");
        var pendingReaction = decision.Reaction.Action is VerdictAction.Limit or VerdictAction.Degrade || decision.Reaction.IsAuthoritativeRecovery
            ? decision.ReactionIdempotencyKey : prior?.PendingReactionId;
        var pendingNotification = decision.NotificationIdempotencyKey ??
            (prior is not null && prior.PendingReactionId == pendingReaction ? prior.PendingNotificationId : null);
        var state = snapshot with
        {
            PendingReactionId = pendingReaction,
            CompletedReactionId = prior is not null && prior.PendingReactionId == pendingReaction ? prior.CompletedReactionId : null,
            PendingNotificationId = pendingNotification,
            CompletedNotificationId = prior is not null && prior.PendingNotificationId == pendingNotification ? prior.CompletedNotificationId : null,
        };
        await this.SaveStateAsync(owner, state).ConfigureAwait(false);
    }

    private async Task SaveEffectProgressAsync(Generation owner, DecisionShape shape, bool reactionDomain)
    {
        if (this.stateStore is null)
        {
            if (reactionDomain) owner.ReactionProgress = new(shape, shape.ReactionKey);
            else if (shape.NotificationKey is not null) owner.NotificationProgress = new(shape, shape.NotificationKey);
            return;
        }
        if (owner.DurableState is null) return;
        var state = owner.DurableState;
        state = reactionDomain
            ? state with { CompletedReactionId = state.PendingReactionId }
            : state with { CompletedNotificationId = state.PendingNotificationId };
        await this.SaveStateAsync(owner, state).ConfigureAwait(false);
        if (reactionDomain) owner.ReactionProgress = new(shape, state.PendingReactionId!);
        else owner.NotificationProgress = new(shape, state.PendingNotificationId!);
    }

    private async Task SaveStateAsync(Generation owner, IntegrityEscalationState state)
    {
        if (this.stateStore is null) return;
        var envelope = new IntegrityEscalationStateEnvelope(
            IntegrityEscalationStateEnvelope.CurrentDocumentVersion,
            IntegrityEscalationState.CurrentSchemaVersion,
            state);
        envelope.Validate();
        await this.stateStore.SaveAsync(envelope, owner.Cancellation.Token).ConfigureAwait(false);
        owner.DurableState = state;
    }

    private async Task ProcessVerdictReactionAsync(
        VerdictReaction reaction,
        string context,
        CancellationToken cancellationToken,
        BackendIdentityState? identity,
        Generation current)
    {
        var integrityIssueKey = new IssueKey(
            0,
            EnforcementIssueType.BinaryIntegrityFailure,
            "integrity/binary",
            identity?.DeviceId);
        if (reaction.IsAuthoritativeRecovery)
        {
            if (!this.IsGenerationActive(current)) return;
            await this.RunAdmittedStageAsync(current, () => this.enforcementLevelMonitor.ResolveIssueAsync(
                integrityIssueKey, "authoritative backend trust verdict", cancellationToken)).ConfigureAwait(false);
            return;
        }

        switch (reaction.Action)
        {
            case VerdictAction.None:
                // No action needed
                break;

            case VerdictAction.Warn:
                System.Diagnostics.Debug.WriteLine(
                    $"[AntiTamperMonitor] Integrity warning ({context}): {reaction.Reason}");
                break;

            case VerdictAction.Limit:
                System.Diagnostics.Debug.WriteLine(
                    $"[AntiTamperMonitor] Integrity limit ({context}): {reaction.Reason}");
                if (!this.IsGenerationActive(current)) return;
                await this.RunAdmittedStageAsync(current, () => this.enforcementLevelMonitor.AddIssueAsync(
                    integrityIssueKey, reaction.Severity ?? EnforcementIssueSeverity.Warning,
                    reaction.Reason ?? "Integrity limit reached", cancellationToken)).ConfigureAwait(false);
                break;

            case VerdictAction.Degrade:
                System.Diagnostics.Debug.WriteLine(
                    $"[AntiTamperMonitor] Integrity degrade ({context}): {reaction.Reason}");
                if (!this.IsGenerationActive(current)) return;
                await this.RunAdmittedStageAsync(current, () => this.enforcementLevelMonitor.AddIssueAsync(
                    integrityIssueKey, reaction.Severity ?? EnforcementIssueSeverity.Severe,
                    reaction.Reason ?? "Integrity degradation triggered", cancellationToken)).ConfigureAwait(false);
                break;

            case VerdictAction.ShadowWarn:
                System.Diagnostics.Debug.WriteLine(
                    $"[AntiTamperMonitor] Shadow mode ({context}): {reaction.Reason}");
                break;
        }
    }

    private void RecordTamperEvent(TamperEventType type, string description, TamperSeverity severity, Generation? owner = null)
    {
        Admission? outbox = null;
        var tamperEvent = new TamperEvent
        {
            Type = type,
            Description = description,
            DetectedAt = this.timeProvider.WallClockNow,
            Severity = severity,
        };

        lock (this.lockObject)
        {
            this.detectedEvents.Add(tamperEvent);
            if (owner is not null && owner.AdmissionOpen && ReferenceEquals(this.generation, owner))
            {
                outbox = this.AdmitLocked(owner, () => this.EnqueueToOutboxAsync(tamperEvent, owner.Cancellation.Token));
            }
        }

        outbox?.Gate.TrySetResult();

        // Enqueue to outbox (T03)
        if (owner is null)
        {
            _ = this.EnqueueToOutboxAsync(tamperEvent, CancellationToken.None);
        }

        var previous = this.callbackGeneration.Value; this.callbackGeneration.Value = owner;
        try { this.TamperDetected?.Invoke(this, new TamperEventArgs { Event = tamperEvent }); this.onTamperDetected(tamperEvent); }
        finally { this.callbackGeneration.Value = previous; }

        System.Diagnostics.Debug.WriteLine(
            $"[AntiTamperMonitor] Tamper detected: {type} - {description}");
    }

    private async Task EnqueueToOutboxAsync(TamperEvent tamperEvent, CancellationToken cancellationToken)
    {
        try
        {
            var eventType = tamperEvent.Type switch
            {
                TamperEventType.ServiceStopAttempt => "service_stop_attempt",
                TamperEventType.AgentKillDetected => "agent_kill_detected",
                TamperEventType.UninstallAttempt => "uninstall_attempt",
                TamperEventType.ClockTamperSuspected => "clock_tamper_suspected",
                TamperEventType.TimezoneChanged => "timezone_changed",
                TamperEventType.ChildIsAdminDetected => "child_is_admin_detected",
                _ => "unknown_tamper_event",
            };

            var payload = new
            {
                EventType = eventType,
                Description = tamperEvent.Description,
                Severity = tamperEvent.Severity.ToString(),
                DetectedAt = tamperEvent.DetectedAt.ToString("O"),
                Timezone = this.CurrentTimezone,
                ClockJumpDetected = this.ClockJumpDetected,
            };

            await this.outboxManager.EnqueueAsync(
                "device_alerts",
                payload,
                tamperEvent.DetectedAt.ToUnixTimeMilliseconds().ToString(),
                cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AntiTamperMonitor] Failed to enqueue to outbox: {ex.Message}");
        }
    }

    private void FireClockJump(double offsetSeconds, int direction, Generation? owner = null)
    {
        lock (this.lockObject)
        {
            this.clockJumpDetected = true;
        }

        var args = new ClockJumpEventArgs
        {
            OffsetSeconds = offsetSeconds,
            Direction = direction,
            DetectedAt = this.timeProvider.WallClockNow,
        };

        var previous = this.callbackGeneration.Value; this.callbackGeneration.Value = owner;
        try { this.OnClockJumpDetected?.Invoke(this, args); }
        finally { this.callbackGeneration.Value = previous; }
    }

    private void FireTimezoneChanged(string? oldTimezone, string newTimezone, Generation? owner = null)
    {
        var args = new TimezoneChangedEventArgs
        {
            OldTimezone = oldTimezone,
            NewTimezone = newTimezone,
            ChangedAt = this.timeProvider.WallClockNow,
        };

        var previous = this.callbackGeneration.Value; this.callbackGeneration.Value = owner;
        try { this.TimezoneChanged?.Invoke(this, args); }
        finally { this.callbackGeneration.Value = previous; }
    }

    public void Dispose()
    {
        var callbackOwner = this.callbackGeneration.Value;
        Task drain; TaskCompletionSource? startDrain = null;
        lock (this.lockObject)
        {
            this.disposed = true;
            var current = this.generation;
            if (current is null)
            {
                drain = Task.CompletedTask;
            }
            else
            {
                current.AdmissionOpen = false;
                current.Cancellation.Cancel();
                current.TimezoneTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                drain = this.BeginDrainLocked(current, out startDrain);
            }
        }
        startDrain?.TrySetResult();

        if (!ReferenceEquals(callbackOwner, this.generation)) drain.GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

}
