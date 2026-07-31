// <copyright file="EnforcementLevelMonitor.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.Domain;

/// <summary>
/// T12 — Stub implementation of <see cref="IEnforcementLevelMonitor"/> for the UI process.
/// Real monitoring is done by the Windows Service. This stub returns Unknown until
/// proper IPC-based querying is implemented.
/// </summary>
public sealed class EnforcementLevelMonitor : IEnforcementLevelMonitor
{
    /// <inheritdoc/>
    public EnforcementLevel CurrentLevel => EnforcementLevel.Unknown;

    /// <inheritdoc/>
    public bool IsCritical => false;

    /// <inheritdoc/>
    public IReadOnlyList<EnforcementIssue> CurrentIssues => Array.Empty<EnforcementIssue>();

    /// <inheritdoc/>
    public DateTimeOffset? LastEvaluationTime => null;

    /// <inheritdoc/>
    public event EventHandler<EnforcementLevelChangedEventArgs>? LevelChanged;

    /// <inheritdoc/>
    public event EventHandler<EnforcementIssueDetectedEventArgs>? IssueDetected;

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public Task StopAsync() => Task.CompletedTask;

    /// <inheritdoc/>
    public Task EvaluateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc/>
    public void RecordAgentAlive() { }

    /// <inheritdoc/>
    public void RecordAgentHeartbeat() { }

    /// <inheritdoc/>
    public void RecordForegroundChange() { }

    /// <inheritdoc/>
    public void AddIssue(EnforcementIssueType type, EnforcementIssueSeverity severity, string description) { }
}
