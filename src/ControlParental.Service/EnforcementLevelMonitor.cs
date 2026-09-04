// <copyright file="EnforcementLevelMonitor.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T12 — Implementación de IEnforcementLevelMonitor.
/// Monitorea el nivel de enforcement y detecta estados de degradación.
/// </summary>
public sealed class EnforcementLevelMonitor : IEnforcementLevelMonitor, IDisposable
{
    private readonly IPrivilegeInspector privilegeInspector;
    private readonly IScmController scmController;
    private readonly IServiceHealthMonitor healthMonitor;
    private readonly ITimeProvider timeProvider;
    private readonly Action<EnforcementIssue> onIssueDetected;
    private readonly IPreventiveLayerDetector? preventiveLayerDetector;
    private readonly IAccountManager? accountManager;
    private readonly IIssueStore? issueStore;

    private Timer? evaluationTimer;
    private EnforcementLevel currentLevel = EnforcementLevel.Unknown;
    private List<EnforcementIssue> currentIssues = new();
    private readonly Dictionary<IssueKey, EnforcementIssue> semanticIssues = [];
    private DateTimeOffset? lastEvaluationTime;
    private DateTimeOffset? lastAgentHeartbeat;
    private DateTimeOffset? lastForegroundChange;
    private bool isRunning;
    private bool disposed;
    private readonly object lockObject = new();

    private const int ForegroundChangeTimeoutSeconds = 120;
    private const int AgentHeartbeatTimeoutSeconds = 60;
    private const int EvaluationIntervalSeconds = 30;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnforcementLevelMonitor"/> class.
    /// </summary>
    public EnforcementLevelMonitor(
        IPrivilegeInspector privilegeInspector,
        IScmController scmController,
        IServiceHealthMonitor healthMonitor,
        ITimeProvider timeProvider,
        Action<EnforcementIssue>? onIssueDetected = null,
        IPreventiveLayerDetector? preventiveLayerDetector = null,
        IAccountManager? accountManager = null,
        IIssueStore? issueStore = null)
    {
        this.privilegeInspector = privilegeInspector ?? throw new ArgumentNullException(nameof(privilegeInspector));
        this.scmController = scmController ?? throw new ArgumentNullException(nameof(scmController));
        this.healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.onIssueDetected = onIssueDetected ?? (_ => { });
        this.preventiveLayerDetector = preventiveLayerDetector;
        this.accountManager = accountManager;
        this.issueStore = issueStore;
    }

    /// <inheritdoc />
    public EnforcementLevel CurrentLevel
    {
        get
        {
            lock (this.lockObject)
            {
                return this.currentLevel;
            }
        }
    }

    /// <inheritdoc />
    public bool IsCritical
    {
        get
        {
            lock (this.lockObject)
            {
                return this.currentLevel == EnforcementLevel.Degraded &&
                       this.currentIssues.Any(i => i.Severity >= EnforcementIssueSeverity.Severe);
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EnforcementIssue> CurrentIssues
    {
        get
        {
            lock (this.lockObject)
            {
                return this.currentIssues.ToList().AsReadOnly();
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? LastEvaluationTime
    {
        get
        {
            lock (this.lockObject)
            {
                return this.lastEvaluationTime;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<EnforcementLevelChangedEventArgs>? LevelChanged;

    /// <inheritdoc />
    public event EventHandler<EnforcementIssueDetectedEventArgs>? IssueDetected;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(EnforcementLevelMonitor));
        }

        if (this.isRunning)
        {
            return;
        }

        var restoreSucceeded = await this.RestoreIssuesAsync(cancellationToken);
        this.HealthSink?.SetRestoreStatus(restoreSucceeded);
        this.isRunning = true;

        // Initial evaluation
        await this.EvaluateAsync(cancellationToken);

        // Start periodic evaluation
        this.evaluationTimer = new Timer(
            _ => _ = this.EvaluateAsync(CancellationToken.None),
            null,
            TimeSpan.FromSeconds(EvaluationIntervalSeconds),
            TimeSpan.FromSeconds(EvaluationIntervalSeconds));

        System.Diagnostics.Debug.WriteLine("[EnforcementLevelMonitor] Started.");
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        this.isRunning = false;
        this.evaluationTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        System.Diagnostics.Debug.WriteLine("[EnforcementLevelMonitor] Stopped.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task EvaluateAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return;
        }

        var previousLevel = this.CurrentLevel;
        try
        {
            var newIssues = new List<EnforcementIssue>();

            // Check service running status
            var serviceRunning = await this.IsServiceRunningAsync(cancellationToken);
            if (!serviceRunning)
            {
                newIssues.Add(new EnforcementIssue
                {
                    Key = HealthKey(EnforcementIssueType.ServiceNotRunning, "service-state"),
                    Type = EnforcementIssueType.ServiceNotRunning,
                    Severity = EnforcementIssueSeverity.Critical,
                    Description = "Service is not running",
                    DetectedAt = this.timeProvider.WallClockNow,
                });
            }

            // Check agent health
            var agentHealthy = this.healthMonitor.IsAgentHealthy;
            if (!agentHealthy)
            {
                var heartbeatTimeout = this.healthMonitor.LastAgentHeartbeat.HasValue
                    ? (this.timeProvider.WallClockNow - this.healthMonitor.LastAgentHeartbeat.Value).TotalSeconds
                    : double.MaxValue;

                newIssues.Add(new EnforcementIssue
                {
                    Key = HealthKey(EnforcementIssueType.AgentNotResponding, "heartbeat-freshness"),
                    Type = EnforcementIssueType.AgentNotResponding,
                    Severity = heartbeatTimeout > AgentHeartbeatTimeoutSeconds
                        ? EnforcementIssueSeverity.Severe
                        : EnforcementIssueSeverity.Warning,
                    Description = $"Agent not responding (heartbeat overdue by {heartbeatTimeout:F0}s)",
                    DetectedAt = this.timeProvider.WallClockNow,
                });
            }

            var isChildStandard = await this.IsConfiguredChildStandardAsync(cancellationToken);
            if (!isChildStandard)
            {
                newIssues.Add(new EnforcementIssue
                {
                    Key = HealthKey(EnforcementIssueType.ChildIsAdministrator, "child-account-authority"),
                    Type = EnforcementIssueType.ChildIsAdministrator,
                    Severity = EnforcementIssueSeverity.Critical,
                    Description = this.accountManager == null && this.preventiveLayerDetector != null
                        ? "No configured child account authority"
                        : "Child account has administrator privileges or could not be verified",
                    DetectedAt = this.timeProvider.WallClockNow,
                });
            }

            // Check foreground hook timeout
            if (this.lastForegroundChange.HasValue)
            {
                var elapsed = this.timeProvider.WallClockNow - this.lastForegroundChange.Value;
                if (elapsed.TotalSeconds > ForegroundChangeTimeoutSeconds)
                {
                    newIssues.Add(new EnforcementIssue
                    {
                        Key = HealthKey(EnforcementIssueType.HookTimeout, "foreground-observation"),
                        Type = EnforcementIssueType.HookTimeout,
                        Severity = EnforcementIssueSeverity.Warning,
                        Description = $"No foreground changes detected in {elapsed.TotalSeconds:F0}s",
                        DetectedAt = this.timeProvider.WallClockNow,
                    });
                }
            }

            var preventiveLayer = await this.DetectPreventiveLayerAsync(cancellationToken);
            if (preventiveLayer.Status != PreventiveLayerDetectionStatus.Present)
            {
                newIssues.Add(new EnforcementIssue
                {
                    Key = HealthKey(EnforcementIssueType.PreventiveLayerUnavailable, "preventive-layer"),
                    Type = EnforcementIssueType.PreventiveLayerUnavailable,
                    Severity = preventiveLayer.Status is PreventiveLayerDetectionStatus.Absent
                        or PreventiveLayerDetectionStatus.Unsupported
                        ? EnforcementIssueSeverity.Info
                        : EnforcementIssueSeverity.Severe,
                    Description = preventiveLayer.Detail,
                    DetectedAt = this.timeProvider.WallClockNow,
                });
            }

            await this.SynchronizeDurableHealthIssuesAsync(newIssues, cancellationToken);

            // Calculate new level
            var newLevel = this.CalculateEnforcementLevel(newIssues);

            lock (this.lockObject)
            {
                newIssues.AddRange(this.semanticIssues.Values);
                newLevel = this.CalculateEnforcementLevel(newIssues);
            }

            lock (this.lockObject)
            {
                this.currentIssues = newIssues;
                this.lastEvaluationTime = this.timeProvider.WallClockNow;
                this.currentLevel = newLevel;
            }

            this.HealthSink?.SetHealthBlockingIssues(
                newIssues.Any(issue => issue.Severity >= EnforcementIssueSeverity.Severe));

            // Detect new issues
            foreach (var issue in newIssues)
            {
                this.FireIssueDetected(issue);
            }

            // Fire level changed event if level changed
            if (previousLevel != newLevel)
            {
                this.FireLevelChanged(previousLevel, newLevel, newIssues);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[EnforcementLevelMonitor] Evaluated: Level={newLevel}, Issues={newIssues.Count}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failureIssue = new EnforcementIssue
            {
                Key = HealthKey(EnforcementIssueType.EvaluationFailure, "health-evaluation"),
                Type = EnforcementIssueType.EvaluationFailure,
                Severity = EnforcementIssueSeverity.Severe,
                Description = $"Enforcement evaluation failed: {ex.Message}",
                DetectedAt = DateTimeOffset.UtcNow,
            };

            lock (this.lockObject)
            {
                this.currentIssues = [failureIssue, .. this.semanticIssues.Values];
                this.lastEvaluationTime = failureIssue.DetectedAt;
                this.currentLevel = EnforcementLevel.Degraded;
            }

            this.FireIssueDetected(failureIssue);
            this.HealthSink?.SetHealthBlockingIssues(hasBlockingIssues: true);

            if (previousLevel != EnforcementLevel.Degraded)
            {
                this.FireLevelChanged(previousLevel, EnforcementLevel.Degraded, new List<EnforcementIssue> { failureIssue });
            }

            System.Diagnostics.Debug.WriteLine(
                $"[EnforcementLevelMonitor] Evaluation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void RecordAgentAlive()
    {
        lock (this.lockObject)
        {
            this.lastAgentHeartbeat = this.timeProvider.WallClockNow;
        }
    }

    /// <inheritdoc />
    public void RecordAgentHeartbeat()
    {
        lock (this.lockObject)
        {
            this.lastAgentHeartbeat = this.timeProvider.WallClockNow;
        }
    }

    /// <inheritdoc />
    public void RecordForegroundChange()
    {
        lock (this.lockObject)
        {
            this.lastForegroundChange = this.timeProvider.WallClockNow;
        }
    }

    /// <inheritdoc />
    public void AddIssue(EnforcementIssueType type, EnforcementIssueSeverity severity, string description)
    {
        this.AddIssue(new IssueKey(-1, type, type.ToString()), severity, description);
    }

    public void AddIssue(IssueKey key, EnforcementIssueSeverity severity, string description)
    {
        if (this.disposed)
        {
            return;
        }

        EnforcementIssue issue;
        lock (this.lockObject)
        {
            this.semanticIssues.TryGetValue(key, out var existing);
            issue = new EnforcementIssue
            {
                Key = key,
                Type = key.Type,
                Severity = severity,
                Description = description,
                DetectedAt = existing?.DetectedAt ?? this.timeProvider.WallClockNow,
                OccurrenceCount = (existing?.OccurrenceCount ?? 0) + 1,
            };
            this.semanticIssues[key] = issue;
            this.currentIssues.RemoveAll(current => current.Key == key);
            this.currentIssues.Add(issue);
            this.lastEvaluationTime = this.timeProvider.WallClockNow;
            this.currentLevel = this.CalculateEnforcementLevel(this.currentIssues);
        }

        this.FireIssueDetected(issue);
        this.HealthSink?.SetHealthBlockingIssues(
            this.CurrentIssues.Any(current => current.Severity >= EnforcementIssueSeverity.Severe));

        var previousLevel = this.currentLevel;
        System.Diagnostics.Debug.WriteLine(
            $"[EnforcementLevelMonitor] Issue added: {key} ({severity}) -> Level={this.currentLevel}");
    }

    public void ResolveIssue(IssueKey key)
    {
        var removed = false;
        lock (this.lockObject)
        {
            if (!this.semanticIssues.Remove(key))
            {
                return;
            }

            this.currentIssues.RemoveAll(issue => issue.Key == key);
            this.lastEvaluationTime = this.timeProvider.WallClockNow;
            this.currentLevel = this.CalculateEnforcementLevel(this.currentIssues);
            removed = true;
        }

        if (removed)
        {
            this.HealthSink?.SetHealthBlockingIssues(
                this.CurrentIssues.Any(current => current.Severity >= EnforcementIssueSeverity.Severe));
        }
    }

    public async Task AddIssueAsync(
        IssueKey key,
        EnforcementIssueSeverity severity,
        string description,
        CancellationToken cancellationToken = default)
    {
        if (this.issueStore == null)
        {
            this.AddIssue(key, severity, description);
            return;
        }

        var durable = await this.issueStore.UpsertActiveAsync(
            key, severity, description, this.timeProvider.WallClockNow, cancellationToken);
        var issue = ToEnforcementIssue(durable);
        lock (this.lockObject)
        {
            this.semanticIssues[key] = issue;
            this.currentIssues.RemoveAll(current => current.Key == key);
            this.currentIssues.Add(issue);
            this.currentLevel = this.CalculateEnforcementLevel(this.currentIssues);
        }

        this.HealthSink?.SetHealthBlockingIssues(
            this.CurrentIssues.Any(current => current.Severity >= EnforcementIssueSeverity.Severe));
        this.FireIssueDetected(issue);
    }

    public async Task AddIssueAsync(
        IssueKey key,
        EnforcementIssueSeverity severity,
        string description,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(idempotencyKey) || this.issueStore == null)
        {
            await this.AddIssueAsync(key, severity, description, cancellationToken);
            return;
        }

        var result = await this.issueStore.UpsertActiveAsync(
            key, severity, description, this.timeProvider.WallClockNow, idempotencyKey, cancellationToken);
        if (result.IsReplay)
        {
            return;
        }

        var issue = ToEnforcementIssue(result.Issue);
        lock (this.lockObject)
        {
            this.semanticIssues[key] = issue;
            this.currentIssues.RemoveAll(current => current.Key == key);
            this.currentIssues.Add(issue);
            this.currentLevel = this.CalculateEnforcementLevel(this.currentIssues);
        }

        this.HealthSink?.SetHealthBlockingIssues(
            this.CurrentIssues.Any(current => current.Severity >= EnforcementIssueSeverity.Severe));
        this.FireIssueDetected(issue);
    }

    public async Task ResolveIssueAsync(
        IssueKey key,
        string recoveryEvidence,
        CancellationToken cancellationToken = default)
    {
        if (this.issueStore != null)
        {
            await this.issueStore.ResolveAsync(
                key, recoveryEvidence, this.timeProvider.WallClockNow, cancellationToken);
        }

        this.ResolveIssue(key);
    }

    private EnforcementLevel CalculateEnforcementLevel(List<EnforcementIssue> issues)
    {
        // Critical issues = DEGRADED
        if (issues.Any(i => i.Severity >= EnforcementIssueSeverity.Severe))
        {
            return EnforcementLevel.Degraded;
        }

        // No critical issues but some warnings = STANDARD
        if (issues.Any(i => i.Severity >= EnforcementIssueSeverity.Warning))
        {
            return EnforcementLevel.Standard;
        }

        // Check if child is admin
        if (issues.Any(i => i.Type == EnforcementIssueType.ChildIsAdministrator))
        {
            return EnforcementLevel.Degraded;
        }

        // Check if service is running
        if (!issues.Any(i => i.Type == EnforcementIssueType.ServiceNotRunning))
        {
            return issues.Any(i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable)
                ? EnforcementLevel.Standard
                : EnforcementLevel.Managed;
        }

        // Something is wrong but not critical
        return EnforcementLevel.Standard;
    }

    private async Task<bool> IsConfiguredChildStandardAsync(CancellationToken cancellationToken)
    {
        if (this.accountManager == null)
        {
            return this.preventiveLayerDetector == null
                ? await this.privilegeInspector.IsChildStandardAsync(cancellationToken)
                : false;
        }

        try
        {
            var childName = this.accountManager.GetChildAccountName();
            if (string.IsNullOrWhiteSpace(childName))
            {
                return false;
            }

            return await this.accountManager.IsAccountStandardAsync(childName, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnforcementLevelMonitor] Child account check failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> RestoreIssuesAsync(CancellationToken cancellationToken)
    {
        if (this.issueStore == null)
        {
            return true;
        }

        try
        {
            var restored = await this.issueStore.LoadAsync(cancellationToken);
            lock (this.lockObject)
            {
                foreach (var durable in restored.Where(issue => issue.IsActive))
                {
                    this.semanticIssues[durable.Key] = ToEnforcementIssue(durable);
                }

                this.currentIssues = this.semanticIssues.Values.ToList();
                this.currentLevel = this.CalculateEnforcementLevel(this.currentIssues);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var key = HealthKey(EnforcementIssueType.RestoreFailure, "durable-issue-document");
            var issue = new EnforcementIssue
            {
                Key = key,
                Type = key.Type,
                Severity = EnforcementIssueSeverity.Critical,
                Description = $"Durable issue restore failed: {exception.Message}",
                DetectedAt = this.timeProvider.WallClockNow,
                OccurrenceCount = 1,
            };
            lock (this.lockObject)
            {
                this.semanticIssues[key] = issue;
                this.currentIssues = [issue];
                this.currentLevel = EnforcementLevel.Degraded;
            }

            this.HealthSink?.SetHealthBlockingIssues(hasBlockingIssues: true);
            this.FireIssueDetected(issue);
            return false;
        }
    }

    private async Task SynchronizeDurableHealthIssuesAsync(
        List<EnforcementIssue> evaluatedIssues,
        CancellationToken cancellationToken)
    {
        if (this.issueStore == null)
        {
            return;
        }

        var currentKeys = evaluatedIssues.Where(issue => issue.Key != null)
            .Select(issue => issue.Key!)
            .ToHashSet();
        foreach (var issue in evaluatedIssues.Where(issue => issue.Key != null).ToArray())
        {
            var durable = await this.issueStore.UpsertActiveAsync(
                issue.Key!, issue.Severity, issue.Description, issue.DetectedAt, cancellationToken);
            lock (this.lockObject)
            {
                this.semanticIssues[durable.Key] = ToEnforcementIssue(durable);
            }
        }

        IssueKey[] resolvedKeys;
        lock (this.lockObject)
        {
            resolvedKeys = this.semanticIssues.Keys
                .Where(key => key.SessionId == -1 && key.Cause.StartsWith("health/", StringComparison.Ordinal))
                .Where(key => !currentKeys.Contains(key))
                .ToArray();
        }

        foreach (var key in resolvedKeys)
        {
            await this.issueStore.ResolveAsync(
                key, "authoritative health probe recovered", this.timeProvider.WallClockNow, cancellationToken);
            lock (this.lockObject)
            {
                this.semanticIssues.Remove(key);
            }
        }

        evaluatedIssues.RemoveAll(issue => issue.Key != null);
    }

    private static IssueKey HealthKey(EnforcementIssueType type, string cause) =>
        new(-1, type, $"health/{cause}");

    private IAuthoritativeHealthSink? HealthSink => this.healthMonitor as IAuthoritativeHealthSink;

    private static EnforcementIssue ToEnforcementIssue(DurableIssue issue) => new()
    {
        Key = issue.Key,
        Type = issue.Key.Type,
        Severity = issue.Severity,
        Description = issue.LastEvidence,
        DetectedAt = issue.FirstObservedAt,
        OccurrenceCount = issue.OccurrenceCount,
    };

    private async Task<PreventiveLayerDetectionResult> DetectPreventiveLayerAsync(
        CancellationToken cancellationToken)
    {
        if (this.preventiveLayerDetector == null)
        {
            return new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Absent,
                null,
                "No preventive layer (WDAC/AppLocker/MDM) detected");
        }

        try
        {
            return await this.preventiveLayerDetector.DetectAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new PreventiveLayerDetectionResult(
                PreventiveLayerDetectionStatus.Indeterminate,
                null,
                $"Preventive-layer detection failed: {ex.Message}");
        }
    }

    private async Task<bool> IsServiceRunningAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await this.scmController.IsServiceRunningAsync(Program.ServiceName, cancellationToken);
        }
        catch
        {
            return false;
        }
    }

    private void FireLevelChanged(
        EnforcementLevel previousLevel,
        EnforcementLevel newLevel,
        List<EnforcementIssue> issues)
    {
        var args = new EnforcementLevelChangedEventArgs
        {
            PreviousLevel = previousLevel,
            NewLevel = newLevel,
            Issues = issues,
        };

        this.LevelChanged?.Invoke(this, args);
        System.Diagnostics.Debug.WriteLine(
            $"[EnforcementLevelMonitor] Level changed: {previousLevel} -> {newLevel}");
    }

    private void FireIssueDetected(EnforcementIssue issue)
    {
        this.IssueDetected?.Invoke(this, new EnforcementIssueDetectedEventArgs { Issue = issue });
        this.onIssueDetected(issue);
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
            this.isRunning = false;
            this.evaluationTimer?.Dispose();
            this.evaluationTimer = null;
        }

        GC.SuppressFinalize(this);
    }
}
