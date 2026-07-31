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

    private Timer? evaluationTimer;
    private EnforcementLevel currentLevel = EnforcementLevel.Unknown;
    private List<EnforcementIssue> currentIssues = new();
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
        Action<EnforcementIssue>? onIssueDetected = null)
    {
        this.privilegeInspector = privilegeInspector ?? throw new ArgumentNullException(nameof(privilegeInspector));
        this.scmController = scmController ?? throw new ArgumentNullException(nameof(scmController));
        this.healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.onIssueDetected = onIssueDetected ?? (_ => { });
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
        var newIssues = new List<EnforcementIssue>();

        // Check service running status
        var serviceRunning = await this.IsServiceRunningAsync(cancellationToken);
        if (!serviceRunning)
        {
            newIssues.Add(new EnforcementIssue
            {
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
                Type = EnforcementIssueType.AgentNotResponding,
                Severity = heartbeatTimeout > AgentHeartbeatTimeoutSeconds
                    ? EnforcementIssueSeverity.Severe
                    : EnforcementIssueSeverity.Warning,
                Description = $"Agent not responding (heartbeat overdue by {heartbeatTimeout:F0}s)",
                DetectedAt = this.timeProvider.WallClockNow,
            });
        }

        // Check if child is administrator
        var isChildStandard = await this.privilegeInspector.IsChildStandardAsync(cancellationToken);
        if (!isChildStandard)
        {
            newIssues.Add(new EnforcementIssue
            {
                Type = EnforcementIssueType.ChildIsAdministrator,
                Severity = EnforcementIssueSeverity.Critical,
                Description = "Child account has administrator privileges",
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
                    Type = EnforcementIssueType.HookTimeout,
                    Severity = EnforcementIssueSeverity.Warning,
                    Description = $"No foreground changes detected in {elapsed.TotalSeconds:F0}s",
                    DetectedAt = this.timeProvider.WallClockNow,
                });
            }
        }

        // Check preventive layer (WDAC/AppLocker/MDM) - placeholder
        // In production, this would check for actual preventive mechanisms
        var hasPreventiveLayer = this.CheckPreventiveLayer();
        if (!hasPreventiveLayer)
        {
            // Only add as info, not a critical issue
            newIssues.Add(new EnforcementIssue
            {
                Type = EnforcementIssueType.PreventiveLayerUnavailable,
                Severity = EnforcementIssueSeverity.Info,
                Description = "No preventive layer (WDAC/AppLocker/MDM) detected",
                DetectedAt = this.timeProvider.WallClockNow,
            });
        }

        // Calculate new level
        var newLevel = this.CalculateEnforcementLevel(newIssues);

        lock (this.lockObject)
        {
            this.currentIssues = newIssues;
            this.lastEvaluationTime = this.timeProvider.WallClockNow;
            this.currentLevel = newLevel;
        }

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
        if (this.disposed)
        {
            return;
        }

        var issue = new EnforcementIssue
        {
            Type = type,
            Severity = severity,
            Description = description,
            DetectedAt = this.timeProvider.WallClockNow,
        };

        lock (this.lockObject)
        {
            // Remove existing issue of same type to avoid duplicates
            this.currentIssues.RemoveAll(i => i.Type == type);
            this.currentIssues.Add(issue);
            this.lastEvaluationTime = this.timeProvider.WallClockNow;
            this.currentLevel = this.CalculateEnforcementLevel(this.currentIssues);
        }

        this.FireIssueDetected(issue);

        var previousLevel = this.currentLevel;
        System.Diagnostics.Debug.WriteLine(
            $"[EnforcementLevelMonitor] Issue added: {type} ({severity}) -> Level={this.currentLevel}");
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
            // Check preventive layer
            if (!issues.Any(i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable) ||
                issues.All(i => i.Severity == EnforcementIssueSeverity.Info))
            {
                // Has preventive layer = MANAGED
                // No preventive layer but service running = STANDARD
                var hasPreventiveLayer = this.CheckPreventiveLayer();
                return hasPreventiveLayer
                    ? EnforcementLevel.Managed
                    : EnforcementLevel.Standard;
            }
        }

        // Something is wrong but not critical
        return EnforcementLevel.Standard;
    }

    private bool CheckPreventiveLayer()
    {
        // Placeholder: In production, this would check for:
        // - WDAC policies
        // - AppLocker policies
        // - MDM enrollment
        // For now, return false (no preventive layer detected)
        return false;
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
