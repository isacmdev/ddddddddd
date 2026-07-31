// <copyright file="PolicyRepository.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Text.Json;
using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// T03 — Repository for policy and usage persistence.
/// Handles atomic version guard, usage accumulation, and outbox.
/// </summary>
public sealed class PolicyRepository : IPolicyRepository
{
    private readonly ControlParentalDbContext db;
    private readonly ITimeProvider timeProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower) },
    };

    public PolicyRepository(ControlParentalDbContext db, ITimeProvider timeProvider)
    {
        this.db = db;
        this.timeProvider = timeProvider;
    }

    // ── Policy persistence ──────────────────────────────────────────────

    /// <summary>
    /// Upserts the policy atomically: applies only if <paramref name="newVersion"/>
    /// is greater than the locally stored version. Downgrades are discarded.
    /// </summary>
    /// <returns>True if applied, false if discarded.</returns>
    public async Task<bool> UpsertPolicyAsync(Policy policy, CancellationToken ct = default)
    {
        var existing = await this.db.Policies
            .FirstOrDefaultAsync(p => p.DeviceId == policy.DeviceId, ct);

        if (existing != null && existing.Version >= policy.Version)
        {
            // Downgrade or same version — discard
            return false;
        }

        // Serialize full policy
        var policyJson = JsonSerializer.Serialize(policy, PolicyJsonContext.Default.Policy);
        var categoryAssignmentsJson = JsonSerializer.Serialize(policy.CategoryAssignments);

        if (existing != null)
        {
            // Update existing — modify properties; EF Core will detect change
            // when SaveChanges is called (no explicit Update() call to avoid cascade).
            existing.Version = policy.Version;
            existing.PolicyJson = policyJson;
            existing.CategoryAssignmentsJson = categoryAssignmentsJson;
            existing.LastUpdated = this.timeProvider.WallClockNow;
        }
        else
        {
            // Insert new
            this.db.Policies.Add(new PolicyDbEntity
            {
                DeviceId = policy.DeviceId,
                Version = policy.Version,
                PolicyJson = policyJson,
                CategoryAssignmentsJson = categoryAssignmentsJson,
                LastUpdated = this.timeProvider.WallClockNow,
            });
        }

        // Update app policies
        await this.SyncAppPoliciesAsync(policy, ct);

        // Update grants (replace all for simplicity)
        await this.SyncGrantsAsync(policy, ct);

        await this.db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Gets the current policy from SQLite, or null if not found.
    /// </summary>
    public async Task<Policy?> GetPolicyAsync(CancellationToken ct = default)
    {
        var entity = await this.db.Policies
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (entity == null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Policy>(entity.PolicyJson, JsonOptions);
    }

    /// <summary>
    /// Gets the locally stored policy version, or 0 if not found.
    /// </summary>
    public async Task<int> GetLocalVersionAsync(string deviceId, CancellationToken ct = default)
    {
        var entity = await this.db.Policies
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.DeviceId == deviceId, ct);

        return entity?.Version ?? 0;
    }

    // ── Usage tracking ──────────────────────────────────────────────────

    /// <summary>
    /// Adds <paramref name="deltaMinutes"/> to the usage for <paramref name="appId"/>
    /// today (server date). Creates the record if it doesn't exist.
    /// Performs rollover: if server date changed, the old date is preserved
    /// (historical) and a new record for today is started.
    /// </summary>
    public async Task AccumulateUsageAsync(
        string appId,
        int deltaMinutes,
        CancellationToken ct = default)
    {
        var serverDate = this.timeProvider.ServerDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await this.db.UsageToday
            .FirstOrDefaultAsync(u => u.AppId == appId && u.ServerDate == serverDate, ct);

        if (existing != null)
        {
            existing.Minutes += deltaMinutes;
            existing.LastUpdated = this.timeProvider.WallClockNow;
        }
        else
        {
            this.db.UsageToday.Add(new UsageTodayDbEntity
            {
                AppId = appId,
                ServerDate = serverDate,
                Minutes = deltaMinutes,
                LastUpdated = this.timeProvider.WallClockNow,
            });
        }

        await this.db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets a UsageSnapshot for the rules engine (T02).
    /// Computes: per-app minutes, per-category minutes, global minutes,
    /// and exempt app set (always_allowed apps).
    /// </summary>
    public async Task<UsageSnapshot> GetUsageSnapshotAsync(CancellationToken ct = default)
    {
        var serverDate = this.timeProvider.ServerDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var usageRecords = await this.db.UsageToday
            .Where(u => u.ServerDate == serverDate)
            .AsNoTracking()
            .ToListAsync(ct);

        // Load category assignments from policy
        var policyEntity = await this.db.Policies
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        Dictionary<string, string> categoryAssignments = [];
        if (policyEntity != null && !string.IsNullOrEmpty(policyEntity.CategoryAssignmentsJson))
        {
            try
            {
                categoryAssignments = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    policyEntity.CategoryAssignmentsJson, JsonOptions) ?? [];
            }
            catch
            {
                // Corrupt JSON — use empty
            }
        }

        // Load always_allowed apps
        var alwaysAllowedApps = await this.db.AppPolicies
            .Where(a => a.State == "AlwaysAllowed")
            .Select(a => a.PackageName)
            .AsNoTracking()
            .ToListAsync(ct);

        // Compute aggregates
        var appMinutes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var categoryMinutes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var exemptApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in alwaysAllowedApps)
        {
            exemptApps.Add(app);
        }

        foreach (var record in usageRecords)
        {
            appMinutes[record.AppId] = record.Minutes;

            if (!exemptApps.Contains(record.AppId))
            {
                if (categoryAssignments.TryGetValue(record.AppId, out var category))
                {
                    categoryMinutes[category] = categoryMinutes.GetValueOrDefault(category) + record.Minutes;
                }
            }
        }

        var globalMinutes = appMinutes
            .Where(kv => !exemptApps.Contains(kv.Key))
            .Sum(kv => kv.Value);

        return new UsageSnapshot(
            AppMinutes: appMinutes,
            CategoryMinutes: categoryMinutes,
            GlobalMinutes: globalMinutes,
            ExemptAppIds: exemptApps);
    }

    /// <summary>
    /// Gets usage for a specific app today.
    /// </summary>
    public async Task<int> GetAppUsageAsync(string appId, CancellationToken ct = default)
    {
        var serverDate = this.timeProvider.ServerDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var record = await this.db.UsageToday
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AppId == appId && u.ServerDate == serverDate, ct);

        return record?.Minutes ?? 0;
    }

    // ── Outbox ─────────────────────────────────────────────────────────

    /// <summary>
    /// Enqueues an event to the outbox. Uses <paramref name="dedupKey"/> for
    /// deduplication (idempotent send).
    /// </summary>
    public async Task EnqueueOutboxEventAsync(
        string eventType,
        string payloadJson,
        string dedupKey,
        CancellationToken ct = default)
    {
        var existing = await this.db.Outbox
            .FirstOrDefaultAsync(o => o.DedupKey == dedupKey, ct);

        if (existing != null)
        {
            // Already enqueued — skip
            return;
        }

        this.db.Outbox.Add(new OutboxDbEntity
        {
            EventType = eventType,
            PayloadJson = payloadJson,
            DedupKey = dedupKey,
            Attempts = 0,
            CreatedAt = this.timeProvider.WallClockNow,
        });

        await this.db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets pending outbox events (attempts &lt; 5).
    /// </summary>
    public async Task<OutboxDbEntity[]> GetPendingOutboxEventsAsync(int maxCount = 50, CancellationToken ct = default)
    {
        return await this.db.Outbox
            .Where(o => o.Attempts < 5)
            .OrderBy(o => o.CreatedAt)
            .Take(maxCount)
            .ToArrayAsync(ct);
    }

    /// <summary>
    /// Marks an outbox event as sent (removes it).
    /// </summary>
    public async Task MarkOutboxSentAsync(int id, CancellationToken ct = default)
    {
        var entity = await this.db.Outbox.FindAsync([id], ct);
        if (entity != null)
        {
            this.db.Outbox.Remove(entity);
            await this.db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Records a failed attempt for an outbox event.
    /// </summary>
    public async Task MarkOutboxFailedAsync(int id, string error, CancellationToken ct = default)
    {
        var entity = await this.db.Outbox.FindAsync([id], ct);
        if (entity != null)
        {
            entity.Attempts++;
            entity.LastAttemptAt = this.timeProvider.WallClockNow;
            entity.LastError = error.Length > 500 ? error[..500] : error;
            await this.db.SaveChangesAsync(ct);
        }
    }

    // ── Grants ──────────────────────────────────────────────────────────

    /// <summary>
    /// Gets active grants for the given time.
    /// </summary>
    public async Task<Grant[]> GetActiveGrantsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var entities = await this.db.Grants
            .Where(g => g.GrantedAt <= now && now < g.ExpiresAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return entities.Select(e => new Grant
        {
            Id = e.GrantId,
            RequestId = e.RequestId,
            Scope = e.Scope,
            Minutes = e.Minutes,
            GrantedAt = e.GrantedAt,
            ExpiresAt = e.ExpiresAt,
            Source = Enum.TryParse<GrantSource>(e.Source, out var src) ? src : GrantSource.Manual,
        }).ToArray();
    }

    /// <summary>
    /// Cleans up expired grants.
    /// </summary>
    public async Task CleanupExpiredGrantsAsync(CancellationToken ct = default)
    {
        var now = this.timeProvider.WallClockNow;
        var expired = await this.db.Grants
            .Where(g => g.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expired.Count > 0)
        {
            this.db.Grants.RemoveRange(expired);
            await this.db.SaveChangesAsync(ct);
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────

    private async Task SyncAppPoliciesAsync(Policy policy, CancellationToken ct)
    {
        // Remove existing app policies for this device
        var existing = await this.db.AppPolicies
            .Where(a => a.DeviceId == policy.DeviceId)
            .ToListAsync(ct);
        this.db.AppPolicies.RemoveRange(existing);

        // Add new app policies
        foreach (var appPolicy in policy.AppPolicies)
        {
            var allowedWindowsJson = appPolicy.AllowedWindows != null && appPolicy.AllowedWindows.Length > 0
                ? JsonSerializer.Serialize(appPolicy.AllowedWindows)
                : null;

            this.db.AppPolicies.Add(new AppPolicyDbEntity
            {
                DeviceId = policy.DeviceId,
                PackageName = appPolicy.PackageName,
                State = appPolicy.State.ToString(),
                DailyLimitMinutes = appPolicy.DailyLimitMinutes,
                Category = appPolicy.Category,
                AllowedWindowsJson = allowedWindowsJson,
            });
        }
    }

    private async Task SyncGrantsAsync(Policy policy, CancellationToken ct)
    {
        // Remove existing grants for this device
        var existing = await this.db.Grants
            .Where(g => g.DeviceId == policy.DeviceId)
            .ToListAsync(ct);
        this.db.Grants.RemoveRange(existing);

        // Add new grants
        foreach (var grant in policy.Grants)
        {
            this.db.Grants.Add(new GrantDbEntity
            {
                DeviceId = policy.DeviceId,
                GrantId = grant.Id,
                RequestId = grant.RequestId,
                Scope = grant.Scope,
                Minutes = grant.Minutes,
                GrantedAt = grant.GrantedAt,
                ExpiresAt = grant.ExpiresAt,
                Source = grant.Source.ToString(),
            });
        }
    }
}
