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

    private void TryDispatchWork(WorkType workType, Func<Task> work)
    {
        this.TryDispatchWorkCore(workType, _ => work());
    }

    private void TryDispatchScheduledWork(
        WorkType workType,
        Func<CancellationToken, Task> work)
    {
        this.TryDispatchWorkCore(workType, work);
    }

    private void TryDispatchWorkCore(
        WorkType workType,
        Func<CancellationToken, Task> work)
    {
        TaskCompletionSource completion;
        CancellationToken cancellationToken;

        lock (this.lockObj)
        {
            if (!this.isRunning || this.disposed || this.inFlightWork.ContainsKey(workType))
            {
                return;
            }

            cancellationToken = this.workCancellation?.Token ?? CancellationToken.None;
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ScheduledWorkService] {workType} error: {ex.Message}");
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

    private async Task ExecuteOutboxPushAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsNetworkAvailable())
        {
            this.ApplyBackoff(WorkType.OutboxPush);
            System.Diagnostics.Debug.WriteLine(
                "[ScheduledWorkService] Outbox push skipped: no network.");
            return;
        }

        var pending = await this.outboxManager.GetPendingEntriesAsync(100, cancellationToken);

        if (pending.Count == 0)
        {
            this.ResetBackoff(WorkType.OutboxPush);
            return;
        }

        // Group by table name
        var usageLogs = new List<UsageLogEntry>();
        var deviceAlerts = new List<DeviceAlertEntry>();
        var behavioralEvents = new List<BehavioralEventEntry>();
        var timeRequests = new List<TimeRequestEntry>();

        foreach (var entry in pending)
        {
            try
            {
                var payload = JsonSerializer.Deserialize<object>(entry.PayloadJson, this.jsonOptions);
                var dedupKey = entry.DedupKey;

                switch (entry.TableName)
                {
                    case "usage_logs":
                        var usageEntry = JsonSerializer.Deserialize<UsageLogEntry>(entry.PayloadJson, this.jsonOptions);
                        if (usageEntry != null)
                        {
                            usageLogs.Add(usageEntry);
                        }
                        break;

                    case "device_alerts":
                        var alertEntry = JsonSerializer.Deserialize<DeviceAlertEntry>(entry.PayloadJson, this.jsonOptions);
                        if (alertEntry != null)
                        {
                            deviceAlerts.Add(alertEntry);
                        }
                        break;

                    case "behavioral_events":
                        var eventEntry = JsonSerializer.Deserialize<BehavioralEventEntry>(entry.PayloadJson, this.jsonOptions);
                        if (eventEntry != null)
                        {
                            behavioralEvents.Add(eventEntry);
                        }
                        break;

                    case "time_requests":
                        var tr = JsonSerializer.Deserialize<TimeRequestEntry>(entry.PayloadJson, this.jsonOptions);
                        if (tr != null)
                        {
                            timeRequests.Add(tr);
                        }

                        break;
                }
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ScheduledWorkService] Failed to parse outbox entry {entry.Id}: {ex.Message}");
                await this.outboxManager.MarkFailedAsync(entry.Id, ex.Message, cancellationToken);
            }
        }

        var anyFailed = false;

        // Push usage logs
        if (usageLogs.Count > 0)
        {
            var result = await this.backendClient.PushUsageLogsAsync(usageLogs, cancellationToken);
            if (result.Success)
            {
                foreach (var entry in pending.Where(e => e.TableName == "usage_logs"))
                {
                    await this.outboxManager.MarkSentAsync(entry.Id, cancellationToken);
                }
            }
            else
            {
                anyFailed = true;
                System.Diagnostics.Debug.WriteLine(
                    $"[ScheduledWorkService] Usage logs push failed: {result.ErrorMessage}");
            }
        }

        // Push device alerts
        if (deviceAlerts.Count > 0)
        {
            var result = await this.backendClient.PushDeviceAlertsAsync(deviceAlerts, cancellationToken);
            if (result.Success)
            {
                foreach (var entry in pending.Where(e => e.TableName == "device_alerts"))
                {
                    await this.outboxManager.MarkSentAsync(entry.Id, cancellationToken);
                }
            }
            else
            {
                anyFailed = true;
                System.Diagnostics.Debug.WriteLine(
                    $"[ScheduledWorkService] Device alerts push failed: {result.ErrorMessage}");
            }
        }

        // Push behavioral events
        if (behavioralEvents.Count > 0)
        {
            var result = await this.backendClient.PushBehavioralEventsAsync(behavioralEvents, cancellationToken);
            if (result.Success)
            {
                foreach (var entry in pending.Where(e => e.TableName == "behavioral_events"))
                {
                    await this.outboxManager.MarkSentAsync(entry.Id, cancellationToken);
                }
            }
            else
            {
                anyFailed = true;
                System.Diagnostics.Debug.WriteLine(
                    $"[ScheduledWorkService] Behavioral events push failed: {result.ErrorMessage}");
            }
        }

        // Push time requests
        if (timeRequests.Count > 0)
        {
            // Build lookup: RequestId -> outbox entry Id
            var timeRequestEntryIds = new Dictionary<string, int>();
            foreach (var entry in pending.Where(e => e.TableName == "time_requests"))
            {
                var tr = JsonSerializer.Deserialize<TimeRequestEntry>(entry.PayloadJson, this.jsonOptions);
                if (tr != null)
                {
                    timeRequestEntryIds[tr.RequestId] = entry.Id;
                }
            }

            foreach (var tr in timeRequests)
            {
                var ok = await this.backendClient.CreateTimeRequestAsync(tr, cancellationToken);
                if (ok)
                {
                    if (timeRequestEntryIds.TryGetValue(tr.RequestId, out var entryId))
                    {
                        await this.outboxManager.MarkSentAsync(entryId, cancellationToken);
                    }
                }
                else
                {
                    anyFailed = true;
                }
            }
        }

        if (anyFailed)
        {
            this.ApplyBackoff(WorkType.OutboxPush);
            foreach (var entry in pending)
            {
                await this.outboxManager.MarkFailedAsync(entry.Id, "Push failed", cancellationToken);
            }
        }
        else
        {
            this.ResetBackoff(WorkType.OutboxPush);
            System.Diagnostics.Debug.WriteLine(
                $"[ScheduledWorkService] Outbox push succeeded: {pending.Count} entries.");
        }
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
    internal Task RunBackupAsync(BackupMode mode, CancellationToken cancellationToken = default)
    {
        return mode switch
        {
            BackupMode.Heartbeat => this.ExecuteHeartbeatAsync(cancellationToken),
            BackupMode.Outbox => this.ExecuteOutboxPushAsync(cancellationToken),
            BackupMode.Reconciliation => this.ExecuteReconciliationAsync(cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown backup mode."),
        };
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ScheduledWorkService] Failed to register Task Scheduler tasks: {ex.Message}");
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
