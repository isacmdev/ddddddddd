// <copyright file="UsageReconciler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using ControlParental.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

/// <summary>
/// T07 — Usage reconciler.
/// Backfills usage from WMI process events when T06 was not running or had gaps.
/// - Listens to WMI `Win32_ProcessStartTrace`/`Win32_ProcessStopTrace` for process lifetime.
/// - Resolves AppId via <see cref="Interop.AppIdentityResolver"/>.
/// - Records foreground events in SQLite for reconciliation.
/// - On reconcile: compares WMI event durations vs recorded usage, backfills gaps.
/// - Idempotent: records last reconciliation date; re-running today has no extra effect.
/// - Degrades gracefully if WMI is unavailable (no crash, feeds T12).
/// </summary>
public sealed class UsageReconciler : IUsageReconciler, IDisposable
{
    // ── Constants ─────────────────────────────────────────────────────

    /// <summary>
    /// How often to run a reconciliation check (minutes).
    /// </summary>
    private const int ReconciliationIntervalMinutes = 30;

    /// <summary>
    /// Minimum process duration to consider for reconciliation (seconds).
    /// Filters out very short-lived processes that don't contribute to usage.
    /// </summary>
    private const int MinProcessDurationSeconds = 5;

    private const int ReconciliationBatchSize = 50;

    // ── Dependencies ────────────────────────────────────────────────

    private readonly IDbContextFactory<ControlParentalDbContext> dbContextFactory;
    private readonly ITimeProvider timeProvider;
    private IIpcChannel? ipcChannel;
    private readonly Func<string, string> resolveAppId;

    // ── State ───────────────────────────────────────────────────────

    private readonly object lockObj = new();
    private readonly ConcurrentDictionary<int, ProcessRecord> activeProcesses = new();
    private readonly Timer reconciliationTimer;
    private readonly SemaphoreSlim reconciliationGate = new(1, 1);

    private ManagementEventWatcher? processStartWatcher;
    private ManagementEventWatcher? processStopWatcher;
    private bool isRunning;
    private bool isDegraded;
    private bool disposed;

    public UsageReconciler(
        IDbContextFactory<ControlParentalDbContext> dbContextFactory,
        ITimeProvider timeProvider,
        IIpcChannel? ipcChannel,
        Func<string, string> resolveAppId)
    {
        ArgumentNullException.ThrowIfNull(dbContextFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(resolveAppId);

        this.dbContextFactory = dbContextFactory;
        this.timeProvider = timeProvider;
        this.ipcChannel = ipcChannel;
        this.resolveAppId = resolveAppId;

        this.reconciliationTimer = new Timer(
            callback: _ => this.ReconcileAsync(CancellationToken.None).Wait(),
            state: null,
            dueTime: Timeout.Infinite,
            period: ReconciliationIntervalMinutes * 60 * 1000);
    }

    /// <inheritdoc />
    public bool IsDegraded => this.isDegraded;

    /// <inheritdoc />
    public bool IsRunning => this.isRunning;

    /// <inheritdoc />
    public event Action<ReconciliationDiscrepancy>? DiscrepancyFound;

    /// <inheritdoc />
    public event Action<bool>? DegradedModeChanged;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (this.lockObj)
        {
            if (this.isRunning)
            {
                return;
            }

            this.isRunning = true;
        }

        // Subscribe to IPC foreground changes from T05
        if (this.ipcChannel != null)
        {
            this.ipcChannel.MessageReceived += this.OnIpcMessage;
        }

        // Start WMI watchers
        this.StartWmiWatchers();

        // Schedule periodic reconciliation
        this.reconciliationTimer.Change(
            TimeSpan.FromMinutes(5), // First run after 5 minutes
            TimeSpan.FromMinutes(ReconciliationIntervalMinutes));

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (this.lockObj)
        {
            if (!this.isRunning)
            {
                return;
            }

            this.isRunning = false;
            this.reconciliationTimer.Change(Timeout.Infinite, Timeout.Infinite);

            if (this.ipcChannel != null)
            {
                this.ipcChannel.MessageReceived -= this.OnIpcMessage;
            }

            this.StopWmiWatchers();
        }
    }

    /// <inheritdoc />
    public void SetIpcChannel(IIpcChannel channel)
    {
        lock (this.lockObj)
        {
            if (ReferenceEquals(this.ipcChannel, channel))
            {
                return;
            }

            if (this.isRunning && this.ipcChannel != null)
            {
                this.ipcChannel.MessageReceived -= this.OnIpcMessage;
            }

            this.ipcChannel = channel;

            if (this.isRunning)
            {
                this.ipcChannel.MessageReceived += this.OnIpcMessage;
            }
        }
    }

    /// <inheritdoc />
    public async Task<ReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        await this.reconciliationGate.WaitAsync(cancellationToken);
        try
        {
            await using var dbContext = this.dbContextFactory.CreateDbContext();
            var serverDate = this.timeProvider.ServerDate
                ?? DateOnly.FromDateTime(DateTime.UtcNow);

            await this.EnsureRecoveryTablesAsync(dbContext, cancellationToken);
            var appsReconciled = 0;
            var appsBackfilled = 0;
            var discrepanciesFound = 0;
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var checkpoint = await this.ReadCheckpointAsync(dbContext, transaction, serverDate, cancellationToken);
            checkpoint = await this.RepairCheckpointAsync(dbContext, transaction, serverDate, checkpoint, cancellationToken);
            var selectedEvents = await dbContext.ForegroundEvents
                .Where(e => e.ServerDate == serverDate && e.Id > checkpoint.LastEventId)
                .OrderBy(e => e.Id)
                .Take(ReconciliationBatchSize)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
            var events = selectedEvents.TakeWhile(e => e.EndedAt != null).ToList();
            if (events.Count > 0)
            {
                var contributions = new Dictionary<string, long>();
                foreach (var foregroundEvent in events)
                {
                    if (await this.MarkAppliedAsync(dbContext, transaction, serverDate, foregroundEvent, cancellationToken) == 1)
                    {
                        contributions[foregroundEvent.AppId] = contributions.GetValueOrDefault(foregroundEvent.AppId) +
                            (long)(foregroundEvent.EndedAt!.Value - foregroundEvent.StartedAt).TotalSeconds;
                    }
                }

                foreach (var (appId, seconds) in contributions)
                {
                    appsReconciled++;
                    await this.AddObservedSecondsAsync(dbContext, transaction, serverDate, appId, seconds, cancellationToken);
                    var observedSeconds = await this.ReadObservedSecondsAsync(dbContext, transaction, serverDate, appId, cancellationToken);
                    var usage = await dbContext.UsageToday
                        .FirstOrDefaultAsync(u => u.AppId == appId && u.ServerDate == serverDate, cancellationToken);
                    var recordedSeconds = usage == null ? 0 : this.GetElapsedSeconds(usage);
                    var delta = observedSeconds - recordedSeconds;
                    if (delta <= 0)
                    {
                        continue;
                    }

                    await this.BackfillAppAsync(dbContext, appId, delta, serverDate, cancellationToken);
                    appsBackfilled++;
                    discrepanciesFound++;
                    this.DiscrepancyFound?.Invoke(new ReconciliationDiscrepancy(
                        appId, (int)(observedSeconds / 60L), (int)(recordedSeconds / 60L), (int)delta,
                        serverDate, DiscrepancyReason.BackfillNeeded));
                }
            }

            var complete = selectedEvents.Count < ReconciliationBatchSize && events.Count == selectedEvents.Count;
            await this.WriteCheckpointAsync(
                dbContext,
                transaction,
                serverDate,
                events.Count == 0 ? checkpoint.LastEventId : events[^1].Id,
                complete,
                cancellationToken);
            if (complete)
            {
                await this.RecordReconciliationAsync(dbContext, serverDate, appsReconciled, appsBackfilled, discrepanciesFound, null, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            Debug.WriteLine(
                $"[UsageReconciler] Reconciled {appsReconciled} apps, " +
                $"backfilled {appsBackfilled}, discrepancies {discrepanciesFound}");

            sw.Stop();
            return ReconciliationResult.Ok(appsReconciled, appsBackfilled, discrepanciesFound, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UsageReconciler] Reconciliation failed: {ex.GetType().Name}");

            try
            {
                var serverDate = this.timeProvider.ServerDate
                    ?? DateOnly.FromDateTime(DateTime.UtcNow);
                await this.RecordReconciliationAsync(
                    null,
                    serverDate, 0, 0, 0, "Reconciliation failed", cancellationToken);
            }
            catch
            {
                // Ignore secondary failures
            }

            sw.Stop();
            return ReconciliationResult.Fail("Reconciliation failed", sw.Elapsed);
        }
        finally
        {
            this.reconciliationGate.Release();
        }
    }

    private async Task EnsureRecoveryTablesAsync(ControlParentalDbContext dbContext, CancellationToken ct)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS usage_reconciliation_checkpoint (server_date TEXT NOT NULL PRIMARY KEY, last_event_id INTEGER NOT NULL DEFAULT 0, completed INTEGER NOT NULL DEFAULT 0);", ct);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS usage_reconciliation_applied (server_date TEXT NOT NULL, event_id INTEGER NOT NULL, app_id TEXT NOT NULL, contribution_seconds INTEGER NOT NULL, PRIMARY KEY(server_date, event_id));", ct);
        await dbContext.Database.ExecuteSqlRawAsync(
            "CREATE TABLE IF NOT EXISTS usage_reconciliation_totals (server_date TEXT NOT NULL, app_id TEXT NOT NULL, observed_seconds INTEGER NOT NULL, PRIMARY KEY(server_date, app_id));", ct);
    }

    private async Task<ReconciliationCheckpoint> RepairCheckpointAsync(
        ControlParentalDbContext dbContext,
        IDbContextTransaction transaction,
        DateOnly serverDate,
        ReconciliationCheckpoint checkpoint,
        CancellationToken ct)
    {
        var openEvent = await dbContext.ForegroundEvents
            .Where(e => e.ServerDate == serverDate && e.Id <= checkpoint.LastEventId && e.EndedAt == null)
            .OrderBy(e => e.Id)
            .Take(1)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);
        return openEvent == null
            ? checkpoint
            : checkpoint with { LastEventId = Math.Max(0, openEvent.Id - 1), Completed = false };
    }

    private async Task<ReconciliationCheckpoint> ReadCheckpointAsync(
        ControlParentalDbContext dbContext,
        IDbContextTransaction transaction,
        DateOnly serverDate,
        CancellationToken ct)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT last_event_id, completed FROM usage_reconciliation_checkpoint WHERE server_date = $date;";
        command.Parameters.Add(new SqliteParameter("$date", serverDate.ToString("yyyy-MM-dd")));
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new ReconciliationCheckpoint(reader.GetInt32(0), reader.GetInt32(1) == 1)
            : new ReconciliationCheckpoint(0, false);
    }

    private async Task<int> MarkAppliedAsync(
        ControlParentalDbContext dbContext,
        IDbContextTransaction transaction,
        DateOnly serverDate,
        ForegroundEventDbEntity foregroundEvent,
        CancellationToken ct)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "INSERT OR IGNORE INTO usage_reconciliation_applied(server_date, event_id, app_id, contribution_seconds) VALUES ($date, $event, $app, $seconds);";
        command.Parameters.Add(new SqliteParameter("$date", serverDate.ToString("yyyy-MM-dd")));
        command.Parameters.Add(new SqliteParameter("$event", foregroundEvent.Id));
        command.Parameters.Add(new SqliteParameter("$app", foregroundEvent.AppId));
        command.Parameters.Add(new SqliteParameter("$seconds", (long)(foregroundEvent.EndedAt!.Value - foregroundEvent.StartedAt).TotalSeconds));
        return await command.ExecuteNonQueryAsync(ct);
    }

    private async Task AddObservedSecondsAsync(
        ControlParentalDbContext dbContext,
        IDbContextTransaction transaction,
        DateOnly serverDate,
        string appId,
        long seconds,
        CancellationToken ct)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "INSERT INTO usage_reconciliation_totals(server_date, app_id, observed_seconds) VALUES ($date, $app, $seconds) ON CONFLICT(server_date, app_id) DO UPDATE SET observed_seconds = observed_seconds + excluded.observed_seconds;";
        command.Parameters.Add(new SqliteParameter("$date", serverDate.ToString("yyyy-MM-dd")));
        command.Parameters.Add(new SqliteParameter("$app", appId));
        command.Parameters.Add(new SqliteParameter("$seconds", seconds));
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<long> ReadObservedSecondsAsync(
        ControlParentalDbContext dbContext,
        IDbContextTransaction transaction,
        DateOnly serverDate,
        string appId,
        CancellationToken ct)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT observed_seconds FROM usage_reconciliation_totals WHERE server_date = $date AND app_id = $app;";
        command.Parameters.Add(new SqliteParameter("$date", serverDate.ToString("yyyy-MM-dd")));
        command.Parameters.Add(new SqliteParameter("$app", appId));
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private async Task WriteCheckpointAsync(
        ControlParentalDbContext dbContext,
        IDbContextTransaction transaction,
        DateOnly serverDate,
        int lastEventId,
        bool completed,
        CancellationToken ct)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "INSERT INTO usage_reconciliation_checkpoint(server_date, last_event_id, completed) VALUES ($date, $event, $completed) ON CONFLICT(server_date) DO UPDATE SET last_event_id = excluded.last_event_id, completed = excluded.completed;";
        command.Parameters.Add(new SqliteParameter("$date", serverDate.ToString("yyyy-MM-dd")));
        command.Parameters.Add(new SqliteParameter("$event", lastEventId));
        command.Parameters.Add(new SqliteParameter("$completed", completed ? 1 : 0));
        await command.ExecuteNonQueryAsync(ct);
    }

    private sealed record ReconciliationCheckpoint(int LastEventId, bool Completed);

    // ── WMI Event Handling ───────────────────────────────────────────

    private void StartWmiWatchers()
    {
        try
        {
            // Process start event
            var startQuery = new WqlEventQuery(
                "__InstanceCreationEvent",
                TimeSpan.FromSeconds(5),
                "TargetInstance ISA 'Win32_ProcessStartTrace'");

            this.processStartWatcher = new ManagementEventWatcher(startQuery);
            this.processStartWatcher.EventArrived += this.OnProcessStarted;
            this.processStartWatcher.Start();

            // Process stop event
            var stopQuery = new WqlEventQuery(
                "__InstanceCreationEvent",
                TimeSpan.FromSeconds(5),
                "TargetInstance ISA 'Win32_ProcessStopTrace'");

            this.processStopWatcher = new ManagementEventWatcher(stopQuery);
            this.processStopWatcher.EventArrived += this.OnProcessStopped;
            this.processStopWatcher.Start();

            Debug.WriteLine("[UsageReconciler] WMI watchers started.");
        }
        catch (ManagementException ex)
        {
            Debug.WriteLine(
                $"[UsageReconciler] WMI not available (degraded mode): {ex.Message}");

            this.SetDegradedMode(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                $"[UsageReconciler] Failed to start WMI watchers: {ex.Message}");

            this.SetDegradedMode(true);
        }
    }

    private void StopWmiWatchers()
    {
        try
        {
            this.processStartWatcher?.Stop();
            this.processStopWatcher?.Stop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UsageReconciler] Error stopping WMI watchers: {ex.Message}");
        }

        this.processStartWatcher?.Dispose();
        this.processStopWatcher?.Dispose();
        this.processStartWatcher = null;
        this.processStopWatcher = null;
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var target = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var processId = Convert.ToInt32(target["ProcessId"]);
            var processName = target["ProcessName"]?.ToString() ?? string.Empty;

            Debug.WriteLine($"[UsageReconciler] Process started: PID={processId} Name={processName}");

            var record = new ProcessRecord(processId, processName, this.timeProvider.WallClockNow);
            this.activeProcesses[processId] = record;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UsageReconciler] Error handling process start: {ex.Message}");
        }
    }

    private void OnProcessStopped(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var target = (ManagementBaseObject)e.NewEvent["TargetInstance"];
            var processId = Convert.ToInt32(target["ProcessId"]);

            if (this.activeProcesses.TryRemove(processId, out var record))
            {
                var duration = this.timeProvider.WallClockNow - record.StartedAt;

                if (duration.TotalSeconds >= MinProcessDurationSeconds)
                {
                    // Resolve AppId from process name
                    var appId = this.resolveAppId(record.ProcessName);

                    // Record the foreground event
                    this.RecordForegroundEventAsync(appId, record.StartedAt, record.ProcessName)
                        .Wait(TimeSpan.FromSeconds(2));
                }

                Debug.WriteLine(
                    $"[UsageReconciler] Process stopped: PID={processId} " +
                    $"Duration={duration.TotalSeconds:F1}s AppId={this.resolveAppId(record.ProcessName)}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UsageReconciler] Error handling process stop: {ex.Message}");
        }
    }

    // ── IPC Message Handling ─────────────────────────────────────────

    private void OnIpcMessage(IIpcMessage message)
    {
        if (message is ForegroundChanged fg && fg.AppId != null)
        {
            this.RecordForegroundEventAsync(
                fg.AppId,
                this.timeProvider.WallClockNow,
                source: "T05")
                .Wait(TimeSpan.FromSeconds(1));
        }
    }

    // ── Database Operations ──────────────────────────────────────────

    private async Task RecordForegroundEventAsync(
        string appId,
        DateTimeOffset startedAt,
        string? processName = null,
        string source = "T07")
    {
        if (string.IsNullOrEmpty(appId))
        {
            return;
        }

        try
        {
            await using var dbContext = this.dbContextFactory.CreateDbContext();

            var serverDate = this.timeProvider.ServerDate
                ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // Check if there's an open event for this app (not yet closed)
            var existingEvent = await dbContext.ForegroundEvents
                .Where(e => e.AppId == appId && e.EndedAt == null && e.ServerDate == serverDate)
                .OrderByDescending(e => e.StartedAt)
                .FirstOrDefaultAsync();

            if (existingEvent != null)
            {
                // Close the previous event
                existingEvent.EndedAt = startedAt;
                await dbContext.SaveChangesAsync();
            }

            // Open a new event
            dbContext.ForegroundEvents.Add(new ForegroundEventDbEntity
            {
                AppId = appId,
                StartedAt = startedAt,
                EndedAt = null,
                ServerDate = serverDate,
                Source = source,
            });

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UsageReconciler] Failed to record foreground event: {ex.Message}");
        }
    }

    private async Task BackfillAppAsync(
        ControlParentalDbContext dbContext,
        string appId,
        long deltaSeconds,
        DateOnly serverDate,
        CancellationToken ct)
    {
        var existing = await dbContext.UsageToday
            .FirstOrDefaultAsync(u => u.AppId == appId && u.ServerDate == serverDate, ct);

        if (existing != null)
        {
            var currentElapsedSeconds = this.GetElapsedSeconds(existing);
            var totalElapsedSeconds = currentElapsedSeconds + deltaSeconds;
            existing.ElapsedSeconds = totalElapsedSeconds;
            existing.Minutes = (int)(totalElapsedSeconds / 60L);
            existing.LastUpdated = this.timeProvider.WallClockNow;
        }
        else
        {
            dbContext.UsageToday.Add(new UsageTodayDbEntity
            {
                AppId = appId,
                ServerDate = serverDate,
                ElapsedSeconds = deltaSeconds,
                Minutes = (int)(deltaSeconds / 60L),
                LastUpdated = this.timeProvider.WallClockNow,
            });
        }

        await dbContext.SaveChangesAsync(ct);

        Debug.WriteLine(
            $"[UsageReconciler] Backfilled {deltaSeconds} sec for {appId}");
    }

    private async Task RecordReconciliationAsync(
        ControlParentalDbContext? dbContext,
        DateOnly serverDate,
        int appsReconciled,
        int appsBackfilled,
        int discrepancies,
        string? error,
        CancellationToken ct)
    {
        var ownsContext = dbContext is null;
        var scopedDbContext = dbContext ?? this.dbContextFactory.CreateDbContext();

        try
        {
            var existing = await scopedDbContext.ReconciliationHistory
                .FirstOrDefaultAsync(r => r.ServerDate == serverDate, ct);

            if (existing != null)
            {
                existing.AppsReconciled = appsReconciled;
                existing.AppsBackfilled = appsBackfilled;
                existing.DiscrepanciesFound = discrepancies;
                existing.CompletedAt = this.timeProvider.WallClockNow;
                existing.ErrorMessage = error;
            }
            else
            {
                scopedDbContext.ReconciliationHistory.Add(new ReconciliationHistoryDbEntity
                {
                    ServerDate = serverDate,
                    AppsReconciled = appsReconciled,
                    AppsBackfilled = appsBackfilled,
                    DiscrepanciesFound = discrepancies,
                    StartedAt = this.timeProvider.WallClockNow.AddMinutes(-ReconciliationIntervalMinutes),
                    CompletedAt = this.timeProvider.WallClockNow,
                    ErrorMessage = error,
                });
            }

            await scopedDbContext.SaveChangesAsync(ct);
        }
        finally
        {
            if (ownsContext)
            {
                await scopedDbContext.DisposeAsync();
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private void SetDegradedMode(bool degraded)
    {
        if (this.isDegraded == degraded)
        {
            return;
        }

        this.isDegraded = degraded;
        Debug.WriteLine($"[UsageReconciler] Degraded mode: {degraded}");
        this.DegradedModeChanged?.Invoke(degraded);
    }

    private long GetElapsedSeconds(UsageTodayDbEntity record)
        => record.ElapsedSeconds > 0 ? record.ElapsedSeconds : record.Minutes * 60L;

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.Stop();
        this.reconciliationTimer.Dispose();
        this.reconciliationGate.Dispose();
        this.processStartWatcher?.Dispose();
        this.processStopWatcher?.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Internal Records (for WMI tracking) ─────────────────────────

    private sealed record ProcessRecord(int ProcessId, string ProcessName, DateTimeOffset StartedAt);
}
