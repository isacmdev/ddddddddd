// <copyright file="UsageReconciler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;

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

    // ── Dependencies ────────────────────────────────────────────────

    private readonly ControlParentalDbContext dbContext;
    private readonly ITimeProvider timeProvider;
    private IIpcChannel? ipcChannel;
    private readonly Func<string, string> resolveAppId;

    // ── State ───────────────────────────────────────────────────────

    private readonly object lockObj = new();
    private readonly ConcurrentDictionary<int, ProcessRecord> activeProcesses = new();
    private readonly Timer reconciliationTimer;

    private ManagementEventWatcher? processStartWatcher;
    private ManagementEventWatcher? processStopWatcher;
    private bool isRunning;
    private bool isDegraded;
    private bool disposed;

    public UsageReconciler(
        ControlParentalDbContext dbContext,
        ITimeProvider timeProvider,
        IIpcChannel? ipcChannel,
        Func<string, string> resolveAppId)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(resolveAppId);

        this.dbContext = dbContext;
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
        this.ipcChannel = channel;
    }

    /// <inheritdoc />
    public async Task<ReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var serverDate = this.timeProvider.ServerDate
                ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // Idempotency: skip if already reconciled today
            var existing = await this.dbContext.ReconciliationHistory
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ServerDate == serverDate, cancellationToken);

            if (existing != null && existing.CompletedAt != null)
            {
                Debug.WriteLine($"[UsageReconciler] Already reconciled for {serverDate}, skipping.");
                return ReconciliationResult.Ok(0, 0, 0, sw.Elapsed);
            }

            // Get all foreground events for today
            var events = await this.dbContext.ForegroundEvents
                .Where(e => e.ServerDate == serverDate && e.EndedAt != null)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Group by AppId and sum durations
            var wmiDurations = events
                .GroupBy(e => e.AppId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(e =>
                    {
                        var end = e.EndedAt ?? this.timeProvider.WallClockNow;
                        return (int)(end - e.StartedAt).TotalMinutes;
                    }));

            // Get recorded usage for today
            var usageRecords = await this.dbContext.UsageToday
                .Where(u => u.ServerDate == serverDate)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var recorded = usageRecords.ToDictionary(u => u.AppId, u => u.Minutes);

            var appsReconciled = 0;
            var appsBackfilled = 0;
            var discrepanciesFound = 0;

            // For each app with WMI data, check if backfill is needed
            foreach (var (appId, wmiMinutes) in wmiDurations)
            {
                appsReconciled++;

                var recordedMinutes = recorded.GetValueOrDefault(appId, 0);

                if (wmiMinutes > recordedMinutes)
                {
                    var delta = wmiMinutes - recordedMinutes;

                    // Backfill
                    await this.BackfillAppAsync(appId, delta, serverDate, cancellationToken);

                    appsBackfilled++;
                    discrepanciesFound++;

                    var discrepancy = new ReconciliationDiscrepancy(
                        appId,
                        wmiMinutes,
                        recordedMinutes,
                        delta,
                        serverDate,
                        DiscrepancyReason.BackfillNeeded);

                    this.DiscrepancyFound?.Invoke(discrepancy);
                }
            }

            // Record reconciliation history
            await this.RecordReconciliationAsync(
                serverDate,
                appsReconciled,
                appsBackfilled,
                discrepanciesFound,
                null,
                cancellationToken);

            Debug.WriteLine(
                $"[UsageReconciler] Reconciled {appsReconciled} apps, " +
                $"backfilled {appsBackfilled}, discrepancies {discrepanciesFound}");

            sw.Stop();
            return ReconciliationResult.Ok(appsReconciled, appsBackfilled, discrepanciesFound, sw.Elapsed);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UsageReconciler] Reconciliation failed: {ex.Message}");

            try
            {
                var serverDate = this.timeProvider.ServerDate
                    ?? DateOnly.FromDateTime(DateTime.UtcNow);
                await this.RecordReconciliationAsync(
                    serverDate, 0, 0, 0, ex.Message, cancellationToken);
            }
            catch
            {
                // Ignore secondary failures
            }

            sw.Stop();
            return ReconciliationResult.Fail(ex.Message, sw.Elapsed);
        }
    }

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
            var serverDate = this.timeProvider.ServerDate
                ?? DateOnly.FromDateTime(DateTime.UtcNow);

            // Check if there's an open event for this app (not yet closed)
            var existingEvent = await this.dbContext.ForegroundEvents
                .Where(e => e.AppId == appId && e.EndedAt == null && e.ServerDate == serverDate)
                .OrderByDescending(e => e.StartedAt)
                .FirstOrDefaultAsync();

            if (existingEvent != null)
            {
                // Close the previous event
                existingEvent.EndedAt = startedAt;
                await this.dbContext.SaveChangesAsync();
            }

            // Open a new event
            this.dbContext.ForegroundEvents.Add(new ForegroundEventDbEntity
            {
                AppId = appId,
                StartedAt = startedAt,
                EndedAt = null,
                ServerDate = serverDate,
                Source = source,
            });

            await this.dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UsageReconciler] Failed to record foreground event: {ex.Message}");
        }
    }

    private async Task BackfillAppAsync(
        string appId,
        int deltaMinutes,
        DateOnly serverDate,
        CancellationToken ct)
    {
        var existing = await this.dbContext.UsageToday
            .FirstOrDefaultAsync(u => u.AppId == appId && u.ServerDate == serverDate, ct);

        if (existing != null)
        {
            existing.Minutes += deltaMinutes;
            existing.LastUpdated = this.timeProvider.WallClockNow;
        }
        else
        {
            this.dbContext.UsageToday.Add(new UsageTodayDbEntity
            {
                AppId = appId,
                ServerDate = serverDate,
                Minutes = deltaMinutes,
                LastUpdated = this.timeProvider.WallClockNow,
            });
        }

        await this.dbContext.SaveChangesAsync(ct);

        Debug.WriteLine(
            $"[UsageReconciler] Backfilled {deltaMinutes} min for {appId}");
    }

    private async Task RecordReconciliationAsync(
        DateOnly serverDate,
        int appsReconciled,
        int appsBackfilled,
        int discrepancies,
        string? error,
        CancellationToken ct)
    {
        var existing = await this.dbContext.ReconciliationHistory
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
            this.dbContext.ReconciliationHistory.Add(new ReconciliationHistoryDbEntity
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

        await this.dbContext.SaveChangesAsync(ct);
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

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.Stop();
        this.reconciliationTimer.Dispose();
        this.processStartWatcher?.Dispose();
        this.processStopWatcher?.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Internal Records (for WMI tracking) ─────────────────────────

    private sealed record ProcessRecord(int ProcessId, string ProcessName, DateTimeOffset StartedAt);
}
