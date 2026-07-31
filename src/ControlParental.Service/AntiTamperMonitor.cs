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
    private readonly Action<TamperEvent>? onTamperDetected;

    private Timer? monitorTimer;
    private Timer? timezoneTimer;
    private bool isRunning;
    private bool disposed;
    private readonly object lockObject = new();
    private readonly List<TamperEvent> detectedEvents = new();
    private string currentTimezone;
    private long lastMonotonicTick;
    private DateTimeOffset lastWallClockTime;
    private bool clockJumpDetected;
    private bool timezoneChangedDetected;

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
        Action<TamperEvent>? onTamperDetected = null)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.outboxManager = outboxManager ?? throw new ArgumentNullException(nameof(outboxManager));
        this.privilegeInspector = privilegeInspector ?? throw new ArgumentNullException(nameof(privilegeInspector));
        this.enforcementLevelMonitor = enforcementLevelMonitor ?? throw new ArgumentNullException(nameof(enforcementLevelMonitor));
        this.integrityChecker = integrityChecker ?? throw new ArgumentNullException(nameof(integrityChecker));
        this.backendClient = backendClient ?? throw new ArgumentNullException(nameof(backendClient));
        this.verdictHandler = verdictHandler ?? throw new ArgumentNullException(nameof(verdictHandler));
        this.onTamperDetected = onTamperDetected ?? (_ => { });

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

    /// <inheritdoc />
    public event EventHandler<TamperEventArgs>? TamperDetected;

    /// <inheritdoc />
    public event EventHandler<TimezoneChangedEventArgs>? TimezoneChanged;

    /// <inheritdoc />
    public event EventHandler<ClockJumpEventArgs>? OnClockJumpDetected;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(AntiTamperMonitor));
        }

        if (this.isRunning)
        {
            return;
        }

        this.isRunning = true;

        // Initialize timezone
        this.currentTimezone = TimeZoneInfo.Local.Id;
        this.lastMonotonicTick = this.timeProvider.MonotonicNow;
        this.lastWallClockTime = this.timeProvider.WallClockNow;

        // Start periodic integrity checks
        this.monitorTimer = new Timer(
            _ => _ = this.PerformIntegrityCheckAsync(CancellationToken.None),
            null,
            TimeSpan.FromSeconds(MonitorIntervalSeconds),
            TimeSpan.FromSeconds(MonitorIntervalSeconds));

        // Start timezone monitoring
        this.timezoneTimer = new Timer(
            _ => this.CheckTimezone(),
            null,
            TimeSpan.FromSeconds(TimezoneCheckIntervalSeconds),
            TimeSpan.FromSeconds(TimezoneCheckIntervalSeconds));

        // Initial check
        await this.PerformIntegrityCheckAsync(cancellationToken);

        System.Diagnostics.Debug.WriteLine("[AntiTamperMonitor] Started.");
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        this.isRunning = false;
        this.monitorTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        this.timezoneTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        System.Diagnostics.Debug.WriteLine("[AntiTamperMonitor] Stopped.");
        return Task.CompletedTask;
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

    private async Task PerformIntegrityCheckAsync(CancellationToken cancellationToken)
    {
        if (this.disposed || !this.isRunning)
        {
            return;
        }

        try
        {
            // Check for clock jumps
            await this.CheckClockIntegrityAsync(cancellationToken);

            // Check timezone changes
            this.CheckTimezone();

            // Check if child became admin
            await this.CheckPrivilegeStatusAsync(cancellationToken);

            // T23: Check binary integrity (Authenticode + SHA256)
            await this.PerformBinaryIntegrityCheckAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AntiTamperMonitor] Integrity check failed: {ex.Message}");
        }
    }

    private async Task CheckClockIntegrityAsync(CancellationToken cancellationToken)
    {
        var currentMonotonic = this.timeProvider.MonotonicNow;
        var currentWallClock = this.timeProvider.WallClockNow;

        lock (this.lockObject)
        {
            var monotonicDelta = currentMonotonic - this.lastMonotonicTick;
            var wallClockDelta = (currentWallClock - this.lastWallClockTime).TotalSeconds;

            // Monotonic time should always increase
            // Wall clock should also increase (unless time was changed)

            // If wall clock went backwards more than a small threshold, it's suspicious
            if (wallClockDelta < -MaxAllowedClockJumpSeconds && monotonicDelta > 0)
            {
                this.FireClockJump(wallClockDelta, -1);
            }
            // If wall clock jumped forward significantly without monotonic increase, suspicious
            else if (wallClockDelta > MaxAllowedClockJumpSeconds && monotonicDelta < wallClockDelta * 1000)
            {
                this.FireClockJump(wallClockDelta, 1);
            }

            this.lastMonotonicTick = currentMonotonic;
            this.lastWallClockTime = currentWallClock;
        }

        await Task.CompletedTask;
    }

    private void CheckTimezone()
    {
        if (this.disposed || !this.isRunning)
        {
            return;
        }

        var newTimezone = TimeZoneInfo.Local.Id;

        lock (this.lockObject)
        {
            if (!string.Equals(this.currentTimezone, newTimezone, StringComparison.OrdinalIgnoreCase))
            {
                var oldTimezone = this.currentTimezone;
                this.currentTimezone = newTimezone;
                this.timezoneChangedDetected = true;

                this.RecordTamperEvent(
                    TamperEventType.TimezoneChanged,
                    $"Timezone changed from '{oldTimezone}' to '{newTimezone}'",
                    TamperSeverity.Warning);

                this.FireTimezoneChanged(oldTimezone, newTimezone);

                System.Diagnostics.Debug.WriteLine(
                    $"[AntiTamperMonitor] Timezone changed: {oldTimezone} -> {newTimezone}");
            }
        }
    }

    private async Task CheckPrivilegeStatusAsync(CancellationToken cancellationToken)
    {
        if (this.disposed || !this.isRunning)
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
                    this.RecordTamperEvent(
                        TamperEventType.ChildIsAdminDetected,
                        "Child account detected with administrator privileges",
                        TamperSeverity.Critical);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AntiTamperMonitor] Privilege check failed: {ex.Message}");
        }
    }

    private async Task PerformBinaryIntegrityCheckAsync(CancellationToken cancellationToken)
    {
        if (this.disposed || !this.isRunning)
        {
            return;
        }

        try
        {
            var result = await this.integrityChecker.CheckLocalIntegrityAsync(cancellationToken);

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
            var reportResult = await this.backendClient.ReportIntegrityAsync(report, cancellationToken);

            // T23: Handle local integrity failure via verdict handler
            if (!result.IsSignatureValid)
            {
                var timestamp = this.timeProvider.WallClockNow;
                var reaction = this.verdictHandler.HandleLocalFailure(
                    $"Signature invalid for {result.ExecutablePath}",
                    timestamp);

                this.ProcessVerdictReaction(reaction, "local failure");
            }

            // T23: Handle server verdict via verdict handler
            var verdictReaction = this.verdictHandler.HandleVerdict(
                reportResult.Verdict,
                reportResult.Success,
                this.timeProvider.WallClockNow);

            this.ProcessVerdictReaction(verdictReaction, $"server verdict: {reportResult.Verdict}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AntiTamperMonitor] Binary integrity check failed: {ex.Message}");
        }
    }

    private void ProcessVerdictReaction(VerdictReaction reaction, string context)
    {
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
                this.enforcementLevelMonitor.AddIssue(
                    EnforcementIssueType.BinaryIntegrityFailure,
                    reaction.Severity ?? EnforcementIssueSeverity.Warning,
                    reaction.Reason ?? "Integrity limit reached");
                break;

            case VerdictAction.Degrade:
                System.Diagnostics.Debug.WriteLine(
                    $"[AntiTamperMonitor] Integrity degrade ({context}): {reaction.Reason}");
                this.enforcementLevelMonitor.AddIssue(
                    EnforcementIssueType.BinaryIntegrityFailure,
                    reaction.Severity ?? EnforcementIssueSeverity.Severe,
                    reaction.Reason ?? "Integrity degradation triggered");
                break;

            case VerdictAction.ShadowWarn:
                System.Diagnostics.Debug.WriteLine(
                    $"[AntiTamperMonitor] Shadow mode ({context}): {reaction.Reason}");
                break;
        }
    }

    private void RecordTamperEvent(TamperEventType type, string description, TamperSeverity severity)
    {
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
        }

        // Enqueue to outbox (T03)
        this.EnqueueToOutboxAsync(tamperEvent, CancellationToken.None).ConfigureAwait(false);

        // Fire events
        this.TamperDetected?.Invoke(this, new TamperEventArgs { Event = tamperEvent });
        this.onTamperDetected(tamperEvent);

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

    private void FireClockJump(double offsetSeconds, int direction)
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

        this.OnClockJumpDetected?.Invoke(this, args);
    }

    private void FireTimezoneChanged(string? oldTimezone, string newTimezone)
    {
        var args = new TimezoneChangedEventArgs
        {
            OldTimezone = oldTimezone,
            NewTimezone = newTimezone,
            ChangedAt = this.timeProvider.WallClockNow,
        };

        this.TimezoneChanged?.Invoke(this, args);
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
            this.isRunning = false;
            this.monitorTimer?.Dispose();
            this.timezoneTimer?.Dispose();
            this.monitorTimer = null;
            this.timezoneTimer = null;
        }

        GC.SuppressFinalize(this);
    }
}
