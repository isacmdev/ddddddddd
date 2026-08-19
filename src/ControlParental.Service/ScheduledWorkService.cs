// <copyright file="ScheduledWorkService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Net.NetworkInformation;
using System.Text.Json;
using ControlParental.Domain;

/// <summary>
/// T20 — Implementation of IScheduledWorkService.
/// Runs periodic heartbeat, outbox push, and usage reconciliation
/// with exponential backoff and connectivity checks.
/// Lives inside the service (does not depend on child login).
/// </summary>
public sealed class ScheduledWorkService : IScheduledWorkService, IDisposable
{
    // ── Constants ─────────────────────────────────────────────────────

    /// <summary>
    /// Heartbeat interval in seconds (1 minute).
    /// </summary>
    public const int HeartbeatIntervalSeconds = 60;

    /// <summary>
    /// Outbox push interval in seconds (30 seconds).
    /// </summary>
    public const int OutboxPushIntervalSeconds = 30;

    /// <summary>
    /// Reconciliation interval in seconds (5 minutes).
    /// </summary>
    public const int ReconciliationIntervalSeconds = 300;

    /// <summary>
    /// Initial backoff in seconds.
    /// </summary>
    public const int InitialBackoffSeconds = 1;

    /// <summary>
    /// Maximum backoff in seconds (5 minutes).
    /// </summary>
    public const int MaxBackoffSeconds = 300;

    /// <summary>
    /// Policy sync interval in seconds (30 seconds) — backup polling for T19.
    /// </summary>
    public const int PolicySyncIntervalSeconds = 30;

    /// <summary>
    /// Maximum time allowed for in-flight work during shutdown.
    /// </summary>
    public const int ShutdownBudgetSeconds = 30;

    /// <summary>Maximum number of durable entries admitted by one scan.</summary>
    public const int OutboxBatchSize = 100;

    /// <summary>Lease duration for one durable delivery attempt.</summary>
    public const int OutboxLeaseSeconds = 30;

    /// <summary>Maximum coordinator attempts before durable dead-lettering.</summary>
    public const int MaxOutboxAttempts = 3;

    // ── Dependencies ────────────────────────────────────────────────

    private readonly IBackendClient backendClient;
    private readonly IOutboxManager outboxManager;
    private readonly IUsageReconciler usageReconciler;
    private readonly IEnforcementLevelMonitor enforcementLevelMonitor;
    private readonly ITimeProvider timeProvider;
    private readonly IServiceHealthMonitor healthMonitor;
    private readonly IServiceRecoveryManager recoveryManager;
    private readonly IPolicyRepository policyRepository;
    private readonly IBackendIdentityCoordinator? identityCoordinator;
    private readonly ITaskSchedulerBackup? taskSchedulerBackup;
    private readonly JsonSerializerOptions jsonOptions;

    // ── State ─────────────────────────────────────────────────────

    private Timer? heartbeatTimer;
    private Timer? outboxPushTimer;
    private Timer? reconciliationTimer;
    private Timer? policySyncTimer;
    private CancellationTokenSource? workCancellation;
    private bool isRunning;
    private bool disposed;

    private readonly object lockObj = new();
    private readonly Dictionary<WorkType, int> backoffByWorkType = new();
    private readonly Dictionary<WorkType, DateTimeOffset> nextEligibleAtByWorkType = new();
    private readonly Dictionary<WorkType, Task> inFlightWork = new();
    private readonly SemaphoreSlim policySyncGate = new(1, 1);

    // ── Types ──────────────────────────────────────────────────────

    internal enum WorkType
    {
        Heartbeat,
        OutboxPush,
        Reconciliation,
        PolicySync,
    }

    // ── Constructor ───────────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledWorkService"/> class.
    /// </summary>
    /// <param name="backendClient">Backend client for RPC calls.</param>
    /// <param name="outboxManager">Outbox manager for pending entries.</param>
    /// <param name="usageReconciler">Usage reconciler for backfill.</param>
    /// <param name="enforcementLevelMonitor">Enforcement level monitor.</param>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="healthMonitor">Health monitor.</param>
    /// <param name="recoveryManager">Recovery manager for Task Scheduler registration.</param>
    /// <param name="policyRepository">Policy repository for version guard and persistence.</param>
    /// <param name="taskSchedulerBackup">Optional Task Scheduler backup for T20.</param>
    public ScheduledWorkService(
        IBackendClient backendClient,
        IOutboxManager outboxManager,
        IUsageReconciler usageReconciler,
        IEnforcementLevelMonitor enforcementLevelMonitor,
        ITimeProvider timeProvider,
        IServiceHealthMonitor healthMonitor,
        IServiceRecoveryManager recoveryManager,
        IPolicyRepository policyRepository,
        ITaskSchedulerBackup? taskSchedulerBackup = null,
        IBackendIdentityCoordinator? identityCoordinator = null)
    {
        this.backendClient = backendClient ?? throw new ArgumentNullException(nameof(backendClient));
        this.outboxManager = outboxManager ?? throw new ArgumentNullException(nameof(outboxManager));
        this.usageReconciler = usageReconciler ?? throw new ArgumentNullException(nameof(usageReconciler));
        this.enforcementLevelMonitor = enforcementLevelMonitor ?? throw new ArgumentNullException(nameof(enforcementLevelMonitor));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        this.recoveryManager = recoveryManager ?? throw new ArgumentNullException(nameof(recoveryManager));
        this.policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        this.taskSchedulerBackup = taskSchedulerBackup;
        this.identityCoordinator = identityCoordinator;

        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        // Initialize backoff counters
        this.backoffByWorkType[WorkType.Heartbeat] = InitialBackoffSeconds;
        this.backoffByWorkType[WorkType.OutboxPush] = InitialBackoffSeconds;
        this.backoffByWorkType[WorkType.Reconciliation] = InitialBackoffSeconds;

        // Startup eligibility is truthful: every work type starts immediately eligible.
        this.nextEligibleAtByWorkType[WorkType.Heartbeat] = DateTimeOffset.MinValue;
        this.nextEligibleAtByWorkType[WorkType.OutboxPush] = DateTimeOffset.MinValue;
        this.nextEligibleAtByWorkType[WorkType.Reconciliation] = DateTimeOffset.MinValue;
    }

    // ── IScheduledWorkService ──────────────────────────────────────

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (this.lockObj)
            {
                return this.isRunning;
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (this.lockObj)
        {
            if (this.disposed)
            {
                throw new ObjectDisposedException(nameof(ScheduledWorkService));
            }

            if (this.isRunning)
            {
                return Task.CompletedTask;
            }

            this.isRunning = true;

            this.workCancellation = new CancellationTokenSource();

            this.heartbeatTimer = new Timer(
                callback: _ => this.OnHeartbeatTimer(),
                state: null,
                dueTime: TimeSpan.FromSeconds(HeartbeatIntervalSeconds),
                period: TimeSpan.FromSeconds(HeartbeatIntervalSeconds));

            this.outboxPushTimer = new Timer(
                callback: _ => this.OnOutboxPushTimer(),
                state: null,
                dueTime: TimeSpan.FromSeconds(OutboxPushIntervalSeconds),
                period: TimeSpan.FromSeconds(OutboxPushIntervalSeconds));

            this.reconciliationTimer = new Timer(
                callback: _ => this.OnReconciliationTimer(),
                state: null,
                dueTime: TimeSpan.FromSeconds(ReconciliationIntervalSeconds),
                period: TimeSpan.FromSeconds(ReconciliationIntervalSeconds));

            this.policySyncTimer = new Timer(
                callback: _ => this.OnPolicySyncTimer(),
                state: null,
                dueTime: TimeSpan.FromSeconds(PolicySyncIntervalSeconds),
                period: TimeSpan.FromSeconds(PolicySyncIntervalSeconds));
        }

        // Register Task Scheduler backup tasks (T10 chain)
        this.RegisterTaskSchedulerTasks();

        // T18: Initial policy sync on startup
        this.TryDispatchScheduledWork(WorkType.PolicySync, this.ExecutePolicySyncAsync);

        System.Diagnostics.Debug.WriteLine("[ScheduledWorkService] Started.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cancellation;
        Task[] tasks;

        lock (this.lockObj)
        {
            this.isRunning = false;

            this.heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            this.outboxPushTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            this.reconciliationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            this.policySyncTimer?.Change(Timeout.Infinite, Timeout.Infinite);

            cancellation = this.workCancellation;
            tasks = this.inFlightWork.Values.ToArray();
        }

        cancellation?.Cancel();
        cancellationToken.ThrowIfCancellationRequested();

        if (tasks.Length > 0)
        {
            try
            {
                await Task.WhenAll(tasks)
                    .WaitAsync(TimeSpan.FromSeconds(ShutdownBudgetSeconds), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[ScheduledWorkService] In-flight work exceeded the shutdown budget.");
            }
        }

        System.Diagnostics.Debug.WriteLine("[ScheduledWorkService] Stopped.");
    }

    // ── Timer Callbacks ───────────────────────────────────────────

    private void OnHeartbeatTimer()
    {
        if (!this.ShouldRun(WorkType.Heartbeat))
        {
            return;
        }

        this.TryDispatchScheduledWork(WorkType.Heartbeat, this.ExecuteHeartbeatAsync);
    }

    private void OnOutboxPushTimer()
    {
        if (!this.ShouldRun(WorkType.OutboxPush))
        {
            return;
        }

        this.TryDispatchScheduledWork(WorkType.OutboxPush, this.ExecuteOutboxPushAsync);
    }

    private void OnReconciliationTimer()
    {
        if (!this.ShouldRun(WorkType.Reconciliation))
        {
            return;
        }

        this.TryDispatchScheduledWork(WorkType.Reconciliation, this.ExecuteReconciliationAsync);
    }

    // T19: Backup polling callback — does not trust WNS payload, always fetches fresh
    private void OnPolicySyncTimer()
    {
        this.TryDispatchScheduledWork(WorkType.PolicySync, this.ExecutePolicySyncAsync);
    }

    private Task TryDispatchWork(WorkType workType, Func<Task> work)
    {
        return this.TryDispatchWorkCore(workType, _ => work());
    }

    private Task TryDispatchScheduledWork(
        WorkType workType,
        Func<CancellationToken, Task> work,
        CancellationToken requestedCancellation = default,
        bool allowWhenStopped = false)
    {
        return this.TryDispatchWorkCore(
            workType,
            work,
            requestedCancellation,
            allowWhenStopped);
    }

    private Task TryDispatchWorkCore(
        WorkType workType,
        Func<CancellationToken, Task> work,
        CancellationToken requestedCancellation = default,
        bool allowWhenStopped = false)
    {
        TaskCompletionSource completion;
        CancellationToken cancellationToken;
        var propagateCallerCancellation = requestedCancellation.CanBeCanceled;

        lock (this.lockObj)
        {
            if ((!this.isRunning && !allowWhenStopped) || this.disposed)
            {
                return Task.CompletedTask;
            }

            if (this.inFlightWork.TryGetValue(workType, out var existing))
            {
                return existing;
            }

            cancellationToken = requestedCancellation.CanBeCanceled
                ? requestedCancellation
                : this.workCancellation?.Token ?? CancellationToken.None;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            this.inFlightWork.Add(workType, completion.Task);
        }

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await work(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException ex)
                    when (propagateCallerCancellation && requestedCancellation.IsCancellationRequested)
                {
                    completion.TrySetException(ex);
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ScheduledWorkService] {workType} failed.");
                }
                finally
                {
                    lock (this.lockObj)
                    {
                        if (this.inFlightWork.TryGetValue(workType, out var current)
                            && ReferenceEquals(current, completion.Task))
                        {
                            this.inFlightWork.Remove(workType);
                        }
                    }

                    completion.TrySetResult();
                }
            },
            CancellationToken.None);

        return completion.Task;
    }

    // ── Work Execution ────────────────────────────────────────────

    internal async Task ExecuteHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsNetworkAvailable())
        {
            this.ApplyBackoff(WorkType.Heartbeat);
            System.Diagnostics.Debug.WriteLine(
                "[ScheduledWorkService] Heartbeat skipped: no network.");
            return;
        }

        var heartbeat = new HeartbeatData
        {
            Enforcement = this.enforcementLevelMonitor.CurrentLevel,
            BatteryPct = null, // Desktop, no battery
            ClockOffsetMs = 0, // T13 would provide actual offset
            AgentUptimeMs = this.healthMonitor.IsAgentHealthy
                ? (long)(DateTimeOffset.UtcNow - (this.healthMonitor.LastAgentHeartbeat ?? DateTimeOffset.UtcNow)).TotalMilliseconds
                : 0,
        };

        var result = await this.backendClient.SendHeartbeatAsync(heartbeat, cancellationToken);

        if (result.Success)
        {
            this.ResetBackoff(WorkType.Heartbeat);
            System.Diagnostics.Debug.WriteLine("[ScheduledWorkService] Heartbeat sent.");

            // T18: Update clock offset if provided
            if (result.ServerTimeOffsetMs.HasValue)
            {
                this.timeProvider.SetServerDate(result.ServerTimeOffsetMs.Value);
            }

            // T18: Fetch new policy if server says one is available
            if (result.NewPolicyAvailable)
            {
                System.Diagnostics.Debug.WriteLine("[ScheduledWorkService] New policy available, triggering sync.");
                await this.ExecutePolicySyncAsync(cancellationToken);
            }
        }
        else
        {
            this.ApplyBackoff(WorkType.Heartbeat);
            System.Diagnostics.Debug.WriteLine(
                $"[ScheduledWorkService] Heartbeat failed: {result.ErrorMessage}");
        }
    }

    internal async Task ExecuteOutboxPushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!this.HasDefinitiveIdentity())
        {
            return;
        }

        if (!IsNetworkAvailable())
        {
            this.ApplyBackoff(WorkType.OutboxPush);
            System.Diagnostics.Debug.WriteLine(
                "[ScheduledWorkService] Outbox push skipped: no network.");
            return;
        }

        await this.outboxManager.RecoverExpiredClaimsAsync(cancellationToken);
        var pending = await this.outboxManager.ClaimAsync(
            OutboxBatchSize,
            TimeSpan.FromSeconds(OutboxLeaseSeconds),
            cancellationToken);

        if (pending.Count == 0)
        {
            this.ResetBackoff(WorkType.OutboxPush);
            return;
        }

        var anyFailed = false;
        foreach (var entry in pending)
        {
            try
            {
                if (await this.DeliverOutboxEntryAsync(entry, cancellationToken))
                {
                    await this.outboxManager.CompleteAsync(entry, cancellationToken);
                }
                else
                {
                    anyFailed = true;
                    await this.FailOutboxEntryAsync(entry, permanent: false, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException)
            {
                anyFailed = true;
                await this.FailOutboxEntryAsync(entry, permanent: true, cancellationToken);
            }
            catch (Exception)
            {
                anyFailed = true;
                await this.FailOutboxEntryAsync(entry, permanent: false, cancellationToken);
            }
        }

        if (anyFailed)
        {
            this.ApplyBackoff(WorkType.OutboxPush);
        }
        else
        {
            this.ResetBackoff(WorkType.OutboxPush);
        }
    }

    private async Task<bool> DeliverOutboxEntryAsync(OutboxEntry entry, CancellationToken cancellationToken)
    {
        switch (entry.TableName)
        {
            case "usage_logs":
                var usage = JsonSerializer.Deserialize<UsageLogEntry>(entry.PayloadJson, this.jsonOptions)
                    ?? throw new JsonException("Invalid usage payload.");
                return (await this.backendClient.PushUsageLogsAsync(new[] { usage }, cancellationToken)).Success;
            case "device_alerts":
                var alert = JsonSerializer.Deserialize<DeviceAlertEntry>(entry.PayloadJson, this.jsonOptions)
                    ?? throw new JsonException("Invalid alert payload.");
                return (await this.backendClient.PushDeviceAlertsAsync(new[] { alert }, cancellationToken)).Success;
            case "behavioral_events":
                var behavioral = JsonSerializer.Deserialize<BehavioralEventEntry>(entry.PayloadJson, this.jsonOptions)
                    ?? throw new JsonException("Invalid behavioral payload.");
                return (await this.backendClient.PushBehavioralEventsAsync(new[] { behavioral }, cancellationToken)).Success;
            case "time_requests":
                var request = JsonSerializer.Deserialize<TimeRequestEntry>(entry.PayloadJson, this.jsonOptions)
                    ?? throw new JsonException("Invalid time request payload.");
                return await this.backendClient.CreateTimeRequestAsync(request, cancellationToken);
            default:
                throw new JsonException("Unsupported outbox table.");
        }
    }

    private Task FailOutboxEntryAsync(
        OutboxEntry entry,
        bool permanent,
        CancellationToken cancellationToken)
    {
        var nextEligibleAt = permanent
            ? (DateTimeOffset?)null
            : this.timeProvider.WallClockNow.AddSeconds(this.GetBackoffSeconds(WorkType.OutboxPush));
        return this.outboxManager.FailAsync(
            entry,
            permanent ? "permanent" : "network",
            nextEligibleAt,
            permanent,
            MaxOutboxAttempts,
            cancellationToken);
    }

    internal async Task ExecuteReconciliationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Only reconcile if the reconciler is not already running
        if (this.usageReconciler.IsRunning)
        {
            System.Diagnostics.Debug.WriteLine(
                "[ScheduledWorkService] Reconciliation skipped: already running.");
            return;
        }

        if (!IsNetworkAvailable())
        {
            this.ApplyBackoff(WorkType.Reconciliation);
            System.Diagnostics.Debug.WriteLine(
                "[ScheduledWorkService] Reconciliation skipped: no network.");
            return;
        }

        try
        {
            await this.usageReconciler.StartAsync(cancellationToken);
            var result = await this.usageReconciler.ReconcileAsync(cancellationToken);

            if (result.Success)
            {
                this.ResetBackoff(WorkType.Reconciliation);
                System.Diagnostics.Debug.WriteLine(
                    $"[ScheduledWorkService] Reconciliation succeeded: " +
                    $"{result.AppsReconciled} reconciled, {result.AppsBackfilled} backfilled.");
            }
            else
            {
                this.ApplyBackoff(WorkType.Reconciliation);
                System.Diagnostics.Debug.WriteLine(
                    $"[ScheduledWorkService] Reconciliation failed: {result.ErrorMessage}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.ApplyBackoff(WorkType.Reconciliation);
            System.Diagnostics.Debug.WriteLine(
                $"[ScheduledWorkService] Reconciliation error: {ex.Message}");
        }
    }

    // ── Policy Sync ───────────────────────────────────────────────

    internal async Task ExecutePolicySyncAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await this.policySyncGate.WaitAsync(cancellationToken);

        try
        {
            if (!IsNetworkAvailable())
            {
                return;
            }

            var identity = this.identityCoordinator?.CurrentState;
            if (identity?.CanAuthorizeRemoteAccess != true || string.IsNullOrWhiteSpace(identity.DeviceId))
            {
                return;
            }

            var currentVersion = await this.policyRepository.GetLocalVersionAsync(identity.DeviceId, cancellationToken);
            var fetchResult = await this.backendClient.FetchPolicyAsync(identity.DeviceId, currentVersion, cancellationToken);

            if (fetchResult.Success && !string.IsNullOrEmpty(fetchResult.PolicyJson))
            {
                var policy = JsonSerializer.Deserialize<Policy>(fetchResult.PolicyJson, PolicyJsonContext.Default.Policy);
                if (policy != null)
                {
                    await this.policyRepository.UpsertPolicyAsync(policy, cancellationToken);
                    System.Diagnostics.Debug.WriteLine($"[ScheduledWorkService] Policy synced: version {policy.Version}");
                }
            }
        }
        finally
        {
            this.policySyncGate.Release();
        }
    }

    /// <summary>
    /// Runs one scheduled operation directly for the Task Scheduler backup path.
    /// </summary>
    internal async Task RunBackupAsync(BackupMode mode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dispatch = mode switch
        {
            BackupMode.Heartbeat => this.TryDispatchScheduledWork(
                WorkType.Heartbeat,
                this.ExecuteHeartbeatAsync,
                cancellationToken,
                allowWhenStopped: true),
            BackupMode.Outbox => this.TryDispatchScheduledWork(
                WorkType.OutboxPush,
                this.ExecuteOutboxPushAsync,
                cancellationToken,
                allowWhenStopped: true),
            BackupMode.Reconciliation => this.TryDispatchScheduledWork(
                WorkType.Reconciliation,
                this.ExecuteReconciliationAsync,
                cancellationToken,
                allowWhenStopped: true),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown backup mode."),
        };

        await dispatch.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    // ── Backoff Logic ─────────────────────────────────────────────

    private bool ShouldRun(WorkType workType)
    {
        lock (this.lockObj)
        {
            if (!this.isRunning || this.disposed)
            {
                return false;
            }

            var nextEligibleAt = this.nextEligibleAtByWorkType.GetValueOrDefault(
                workType,
                DateTimeOffset.MinValue);

            return this.timeProvider.WallClockNow >= nextEligibleAt;
        }
    }

    private void ApplyBackoff(WorkType workType)
    {
        lock (this.lockObj)
        {
            var current = this.backoffByWorkType.GetValueOrDefault(workType, InitialBackoffSeconds);
            var next = Math.Min(current * 2, MaxBackoffSeconds);
            var now = this.timeProvider.WallClockNow;

            this.backoffByWorkType[workType] = next;
            this.nextEligibleAtByWorkType[workType] = now.AddSeconds(current);

            System.Diagnostics.Debug.WriteLine(
                $"[ScheduledWorkService] Backoff for {workType}: {current}s -> {next}s; next eligible at {this.nextEligibleAtByWorkType[workType]:O}");
        }
    }

    private void ResetBackoff(WorkType workType)
    {
        lock (this.lockObj)
        {
            this.backoffByWorkType[workType] = InitialBackoffSeconds;
            this.nextEligibleAtByWorkType[workType] = DateTimeOffset.MinValue;
        }
    }

    /// <summary>
    /// Gets the current backoff for a work type (for testing).
    /// </summary>
    internal int GetBackoffForTesting(WorkType workType)
    {
        lock (this.lockObj)
        {
            return this.backoffByWorkType.GetValueOrDefault(workType, InitialBackoffSeconds);
        }
    }

    private int GetBackoffSeconds(WorkType workType)
    {
        lock (this.lockObj)
        {
            return this.backoffByWorkType.GetValueOrDefault(workType, InitialBackoffSeconds);
        }
    }

    private bool HasDefinitiveIdentity()
    {
        var identity = this.identityCoordinator?.CurrentState;
        return identity?.CanAuthorizeRemoteAccess == true
            && !string.IsNullOrWhiteSpace(identity.DeviceId);
    }

    /// <summary>
    /// Gets the next eligible time for a work type (for testing).
    /// </summary>
    internal DateTimeOffset GetNextEligibleAtForTesting(WorkType workType)
    {
        lock (this.lockObj)
        {
            return this.nextEligibleAtByWorkType.GetValueOrDefault(workType, DateTimeOffset.MinValue);
        }
    }

    // ── Connectivity Check ────────────────────────────────────────

    private static bool IsNetworkAvailable()
    {
        try
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }
        catch
        {
            return false;
        }
    }

    // ── Task Scheduler Registration ────────────────────────────────

    private void RegisterTaskSchedulerTasks()
    {
        // T20: Register backup tasks with Windows Task Scheduler
        // This ensures work runs even if the service timers fail
        try
        {
            _ = Task.Run(async () =>
            {
                if (this.taskSchedulerBackup != null)
                {
                    var success = await this.taskSchedulerBackup.RegisterBackupTasksAsync()
                        .ConfigureAwait(false);
                    System.Diagnostics.Debug.WriteLine(
                        $"[ScheduledWorkService] Task Scheduler backup registered: {success}");
                }
            });
        }
        catch (Exception)
        {
            System.Diagnostics.Debug.WriteLine(
                "[ScheduledWorkService] Task Scheduler registration failed.");
        }
    }

    // ── IDisposable ───────────────────────────────────────────────

    public void Dispose()
    {
        CancellationTokenSource? cancellation;

        lock (this.lockObj)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            this.isRunning = false;
            cancellation = this.workCancellation;

            this.heartbeatTimer?.Dispose();
            this.outboxPushTimer?.Dispose();
            this.reconciliationTimer?.Dispose();
            this.policySyncTimer?.Dispose();

            this.heartbeatTimer = null;
            this.outboxPushTimer = null;
            this.reconciliationTimer = null;
            this.policySyncTimer = null;
        }

        cancellation?.Cancel();

        GC.SuppressFinalize(this);
    }
}
