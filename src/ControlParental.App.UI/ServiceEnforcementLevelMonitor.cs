// <copyright file="ServiceEnforcementLevelMonitor.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.App.UI.Interop;
using ControlParental.Domain;

/// <summary>
/// App.UI proxy for the Service-owned enforcement snapshot.
/// </summary>
public sealed class ServiceEnforcementLevelMonitor : IEnforcementLevelMonitor
{
    private static readonly TimeSpan CacheWindow = TimeSpan.FromSeconds(30);
    private readonly IUIChannel channel;
    private readonly object sync = new();
    private EnforcementLevel currentLevel = EnforcementLevel.Unknown;
    private IReadOnlyList<EnforcementIssue> currentIssues = Array.Empty<EnforcementIssue>();
    private DateTimeOffset? lastEvaluationTime;
    private DateTimeOffset? cachedAt;

    /// <inheritdoc/>
    public ServiceEnforcementLevelMonitor(IUIChannel channel)
    {
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <inheritdoc/>
    public EnforcementLevel CurrentLevel
    {
        get
        {
            lock (this.sync)
            {
                return this.currentLevel;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsCritical
    {
        get
        {
            lock (this.sync)
            {
                return this.currentLevel == EnforcementLevel.Degraded
                    && this.currentIssues.Any(i => i.Severity >= EnforcementIssueSeverity.Severe);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<EnforcementIssue> CurrentIssues
    {
        get
        {
            lock (this.sync)
            {
                return this.currentIssues;
            }
        }
    }

    /// <inheritdoc/>
    public DateTimeOffset? LastEvaluationTime
    {
        get
        {
            lock (this.sync)
            {
                return this.lastEvaluationTime;
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<EnforcementLevelChangedEventArgs>? LevelChanged;

    /// <inheritdoc/>
    public event EventHandler<EnforcementIssueDetectedEventArgs>? IssueDetected;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default) => this.EvaluateAsync(cancellationToken);

    /// <inheritdoc/>
    public Task StopAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task EvaluateAsync(CancellationToken cancellationToken = default)
    {
        lock (this.sync)
        {
            if (this.cachedAt.HasValue && DateTimeOffset.UtcNow - this.cachedAt.Value < CacheWindow)
            {
                return;
            }
        }

        EnforcementLevelResponse? response;
        try
        {
            response = await this.channel.QueryAsync<GetEnforcementLevel, EnforcementLevelResponse>(
                new GetEnforcementLevel(), cancellationToken).ConfigureAwait(false);
            if (response is null)
            {
                throw new ConsentServiceUnavailableException("The enforcement service did not respond.");
            }
        }
        catch (ConsentServiceUnavailableException)
        {
            if (this.cachedAt.HasValue)
            {
                return;
            }

            throw;
        }

        var level = Enum.TryParse<EnforcementLevel>(response.Level, true, out var parsedLevel)
            ? parsedLevel
            : EnforcementLevel.Unknown;

        var issues = response.Checks
            .Where(check => !check.IsPassing)
            .Select(check => new EnforcementIssue
            {
                Type = MapIssueType(check.CheckName),
                Severity = EnforcementIssueSeverity.Warning,
                Description = check.Details,
                DetectedAt = DateTimeOffset.UtcNow,
            })
            .ToList()
            .AsReadOnly();

        EnforcementLevel previousLevel;
        lock (this.sync)
        {
            previousLevel = this.currentLevel;
            this.currentLevel = level;
            this.currentIssues = issues;
            this.lastEvaluationTime = DateTimeOffset.UtcNow;
            this.cachedAt = this.lastEvaluationTime;
        }

        if (previousLevel != level)
        {
            this.LevelChanged?.Invoke(this, new EnforcementLevelChangedEventArgs
            {
                PreviousLevel = previousLevel,
                NewLevel = level,
                Issues = issues,
            });
        }
    }

    /// <inheritdoc/>
    public void RecordAgentAlive()
    {
    }

    /// <inheritdoc/>
    public void RecordAgentHeartbeat()
    {
    }

    /// <inheritdoc/>
    public void RecordForegroundChange()
    {
    }

    /// <inheritdoc/>
    public void AddIssue(EnforcementIssueType type, EnforcementIssueSeverity severity, string description)
    {
    }

    private static EnforcementIssueType MapIssueType(string checkName) => checkName switch
    {
        "child_account_standard" => EnforcementIssueType.ChildIsAdministrator,
        "service_running" => EnforcementIssueType.ServiceNotRunning,
        "agent_emitting" => EnforcementIssueType.AgentNotResponding,
        "preventive_layer" => EnforcementIssueType.PreventiveLayerUnavailable,
        _ => EnforcementIssueType.BinaryIntegrityFailure,
    };
}
