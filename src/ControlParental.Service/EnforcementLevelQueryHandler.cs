// <copyright file="EnforcementLevelQueryHandler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T26 — Handles GetEnforcementLevel queries from App.UI.
/// Returns the current enforcement level and individual check results.
/// </summary>
public sealed class EnforcementLevelQueryHandler
{
    private readonly IEnforcementLevelMonitor enforcementLevelMonitor;

    public EnforcementLevelQueryHandler(IEnforcementLevelMonitor enforcementLevelMonitor)
    {
        this.enforcementLevelMonitor = enforcementLevelMonitor;
    }

    /// <summary>
    /// Handles a GetEnforcementLevel query.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<EnforcementLevelResponse> HandleAsync(CancellationToken ct = default)
    {
        // Trigger an immediate evaluation
        await this.enforcementLevelMonitor.EvaluateAsync(ct).ConfigureAwait(false);

        var level = this.enforcementLevelMonitor.CurrentLevel;
        var issues = this.enforcementLevelMonitor.CurrentIssues;
        var issueTypes = issues.Select(i => i.Type).ToHashSet();

        var checks = new List<EnforcementLevelCheck>();

        // child_account_standard: passes if child is not administrator
        var childAccountName = this.GetChildAccountName();
        var isChildStandard = !issueTypes.Contains(EnforcementIssueType.ChildIsAdministrator);
        checks.Add(new EnforcementLevelCheck(
            "child_account_standard",
            isChildStandard,
            isChildStandard
                ? $"Child account '{childAccountName}' is standard (non-admin)"
                : "Child account has administrator privileges"));

        // service_running: passes if service is running
        var serviceRunning = !issueTypes.Contains(EnforcementIssueType.ServiceNotRunning);
        checks.Add(new EnforcementLevelCheck(
            "service_running",
            serviceRunning,
            serviceRunning
                ? "ControlParental service is running"
                : "ControlParental service is not running"));

        // agent_emitting: passes if agent is responding
        var agentEmitting = !issueTypes.Contains(EnforcementIssueType.AgentNotResponding)
            && !issueTypes.Contains(EnforcementIssueType.HookTimeout);
        checks.Add(new EnforcementLevelCheck(
            "agent_emitting",
            agentEmitting,
            agentEmitting
                ? "Session Agent is responding"
                : "Session Agent is not responding"));

        // preventive_layer: passes if WDAC/AppLocker/MDM is available
        var preventiveLayerAvailable = !issueTypes.Contains(EnforcementIssueType.PreventiveLayerUnavailable);
        checks.Add(new EnforcementLevelCheck(
            "preventive_layer",
            preventiveLayerAvailable,
            preventiveLayerAvailable
                ? "Preventive layer (WDAC/AppLocker/MDM) is available"
                : "No preventive layer detected"));

        return new EnforcementLevelResponse(level, checks);
    }

    private string GetChildAccountName()
    {
        // Try to get from the account store via reflection to avoid circular dependency
        // This is a simplified approach - in production, inject IChildAccountStore
        try
        {
            var childAccountStoreType = Type.GetType("ControlParental.Service.ChildAccountStore, ControlParental.Service");
            if (childAccountStoreType != null)
            {
                // We need to get the child account name - this requires access to the store
                // For now, return a placeholder
            }
        }
        catch
        {
            // Ignore
        }

        return "Unknown";
    }
}
