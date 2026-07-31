// <copyright file="UsageStateQueryHandler.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T27 — Handles GetUsageState queries from App.UI.
/// </summary>
public sealed class UsageStateQueryHandler
{
    private readonly IUsageAccumulator usageAccumulator;
    private readonly PolicyRepository policyRepository;
    private readonly IEnforcementLevelMonitor enforcementLevelMonitor;
    private readonly ITimeProvider timeProvider;

    public UsageStateQueryHandler(
        IUsageAccumulator usageAccumulator,
        PolicyRepository policyRepository,
        IEnforcementLevelMonitor enforcementLevelMonitor,
        ITimeProvider timeProvider)
    {
        this.usageAccumulator = usageAccumulator;
        this.policyRepository = policyRepository;
        this.enforcementLevelMonitor = enforcementLevelMonitor;
        this.timeProvider = timeProvider;
    }

    public async Task<UsageStateResponse> HandleAsync(CancellationToken ct = default)
    {
        var now = this.timeProvider.WallClockNow;
        var currentAppId = this.usageAccumulator.CurrentAppId ?? "device";
        var isPaused = this.usageAccumulator.IsPaused;

        var minutesRemaining = await this.usageAccumulator.GetMinutesRemainingAsync(
            currentAppId, now, ct);

        var activeGrants = await this.policyRepository.GetActiveGrantsAsync(now, ct);
        var grantInfos = activeGrants
            .Select(g => new GrantInfo(
                g.Scope,
                Math.Max(0, (int)(g.ExpiresAt - now).TotalMinutes),
                g.Source,
                g.ExpiresAt))
            .ToList();

        var issues = this.enforcementLevelMonitor.CurrentIssues
            .Select(i => new ActiveIssue(i.Type, i.Severity, i.Description))
            .ToList();

        return new UsageStateResponse(
            minutesRemaining,
            this.usageAccumulator.CurrentAppId,
            isPaused,
            grantInfos,
            this.enforcementLevelMonitor.CurrentLevel,
            issues);
    }
}
