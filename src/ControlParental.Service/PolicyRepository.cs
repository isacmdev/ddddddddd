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
public sealed class PolicyRepository : IPolicyRepository, IDisposable
{
    private readonly IDbContextFactory<ControlParentalDbContext> dbContextFactory;
    private readonly ITimeProvider timeProvider;
    private readonly SemaphoreSlim policyGate = new(1, 1);
    private readonly object policySnapshotSync = new();
    private Policy? policySnapshot;
    private bool policySnapshotInitialized;
    private long policySnapshotRevision;
    private bool policyQuarantined;
    private string? policyQuarantineReason;
    private const string SameVersionHashMismatchReason = "same_version_snapshot_hash_mismatch";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.SnakeCaseLower) },
    };

    public PolicyRepository(
        IDbContextFactory<ControlParentalDbContext> dbContextFactory,
        ITimeProvider timeProvider)
    {
        this.dbContextFactory = dbContextFactory;
        this.timeProvider = timeProvider;
    }

    public bool IsPolicyQuarantined
    {
        get
        {
            lock (this.policySnapshotSync)
            {
                return this.policyQuarantined;
            }
        }
    }

    public string? PolicyQuarantineReason
    {
        get
        {
            lock (this.policySnapshotSync)
            {
                return this.policyQuarantineReason;
            }
        }
    }

    // ── Policy persistence ──────────────────────────────────────────────

    /// <summary>
    /// Upserts the policy atomically: applies only if <paramref name="newVersion"/>
    /// is greater than the locally stored version. Downgrades are discarded.
    /// </summary>
    /// <returns>True if applied, false if discarded.</returns>
    public async Task<bool> UpsertPolicyAsync(Policy policy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        try
        {
            policy.Validate();
        }
        catch (ArgumentException)
        {
            // Invalid remote input must not open a database transaction or
            // publish a partially materialized policy.
            return false;
        }

        await this.policyGate.WaitAsync(ct);
        try
        {
            await using var db = this.dbContextFactory.CreateDbContext();

            var existing = await db.Policies
                .FirstOrDefaultAsync(p => p.DeviceId == policy.DeviceId, ct);

            if (existing != null && existing.Version >= policy.Version)
            {
                if (existing.Version == policy.Version
                    && !string.Equals(existing.SnapshotHash, policy.SnapshotHash, StringComparison.Ordinal))
                {
                    existing.IsQuarantined = true;
                    existing.QuarantineReason = SameVersionHashMismatchReason;
                    await db.SaveChangesAsync(ct);
                    // The conflict is authoritative immediately after the durable
                    // write. Do not leave the previous snapshot available to the
                    // active enforcement process while the row is quarantined.
                    this.SetPolicySnapshot(null, SameVersionHashMismatchReason);

                }
                return false;
            }

            // Serialize full policy
            var policyJson = JsonSerializer.Serialize(policy, PolicyJsonContext.Default.Policy);
            var categoryAssignmentsJson = JsonSerializer.Serialize(policy.CategoryAssignments);

            if (existing != null)
            {
                existing.Version = policy.Version;
                existing.SnapshotHash = policy.SnapshotHash;
                existing.IsQuarantined = false;
                existing.QuarantineReason = null;
                existing.PolicyJson = policyJson;
                existing.CategoryAssignmentsJson = categoryAssignmentsJson;
                existing.LastUpdated = this.timeProvider.WallClockNow;
            }
            else
            {
                db.Policies.Add(new PolicyDbEntity
                {
                    DeviceId = policy.DeviceId,
                    Version = policy.Version,
                    SnapshotHash = policy.SnapshotHash,
                    PolicyJson = policyJson,
                    CategoryAssignmentsJson = categoryAssignmentsJson,
                    LastUpdated = this.timeProvider.WallClockNow,
                });
            }

            await PolicyRepository.SyncAppPoliciesAsync(db, policy, ct);

            await PolicyRepository.SyncGrantsAsync(db, policy, ct);

            await db.SaveChangesAsync(ct);
            this.SetPolicySnapshot(policy, null);
            return true;
        }
        finally
        {
            this.policyGate.Release();
        }
    }

    /// <summary>
    /// Gets the current policy from SQLite, or null if not found.
    /// </summary>
    public async Task<Policy?> GetPolicyAsync(CancellationToken ct = default)
    {
        await this.policyGate.WaitAsync(ct);
        try
        {
            lock (this.policySnapshotSync)
            {
                if (this.policySnapshotInitialized)
                {
                    return this.policySnapshot;
                }
            }

            await using var db = this.dbContextFactory.CreateDbContext();
            var entity = await db.Policies.AsNoTracking().FirstOrDefaultAsync(ct);
            // A conflicting rewrite is a durable quarantine. Never expose the
            // previous snapshot to enforcement after restart or cache invalidation.
            var policy = entity == null || entity.IsQuarantined
                ? null
                : JsonSerializer.Deserialize<Policy>(entity.PolicyJson, JsonOptions);
            var quarantineReason = entity?.IsQuarantined == true
                ? entity.QuarantineReason ?? SameVersionHashMismatchReason
                : null;
            this.SetPolicySnapshot(policy, quarantineReason);
            return policy;
        }
        finally
        {
            this.policyGate.Release();
        }
    }

    public void InvalidatePolicySnapshot()
    {
        lock (this.policySnapshotSync)
        {
            this.policySnapshot = null;
            this.policySnapshotInitialized = false;
            this.policySnapshotRevision++;
        }
    }

    private void SetPolicySnapshot(Policy? policy, string? quarantineReason)
    {
        lock (this.policySnapshotSync)
        {
            this.policySnapshot = policy;
            this.policySnapshotInitialized = true;
            this.policyQuarantined = quarantineReason is not null;
            this.policyQuarantineReason = quarantineReason;
            this.policySnapshotRevision++;
        }
    }


    internal long PolicySnapshotRevision
    {
        get
        {
            lock (this.policySnapshotSync)
            {
                return this.policySnapshotRevision;
            }
        }
    }

    public void Dispose() => this.policyGate.Dispose();

    /// <summary>
    /// Gets the locally stored policy version, or 0 if not found.
    /// </summary>
    public async Task<int> GetLocalVersionAsync(string deviceId, CancellationToken ct = default)
    {
        await using var db = this.dbContextFactory.CreateDbContext();

        var entity = await db.Policies
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
        long elapsedSeconds,
        CancellationToken ct = default)
    {
        await using var db = this.dbContextFactory.CreateDbContext();

        var serverDate = this.GetServerDate();

        var existing = await db.UsageToday
            .FirstOrDefaultAsync(u => u.AppId == appId && u.ServerDate == serverDate, ct);

        if (existing != null)
        {
            var currentElapsedSeconds = existing.ElapsedSeconds > 0
                ? existing.ElapsedSeconds
                : existing.Minutes * 60L;

            currentElapsedSeconds += elapsedSeconds;
            existing.ElapsedSeconds = currentElapsedSeconds;
            existing.Minutes = (int)(currentElapsedSeconds / 60L);
            existing.LastUpdated = this.timeProvider.WallClockNow;
        }
        else
        {
            db.UsageToday.Add(new UsageTodayDbEntity
            {
                AppId = appId,
                ServerDate = serverDate,
                ElapsedSeconds = elapsedSeconds,
                Minutes = (int)(elapsedSeconds / 60L),
                LastUpdated = this.timeProvider.WallClockNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets a UsageSnapshot for the rules engine (T02).
    /// Computes: per-app minutes, per-category minutes, global minutes,
    /// and exempt app set (always_allowed apps).
    /// </summary>
    public async Task<UsageSnapshot> GetUsageSnapshotAsync(CancellationToken ct = default)
    {
        await using var db = this.dbContextFactory.CreateDbContext();

        var serverDate = this.GetServerDate();

        var usageRecords = await db.UsageToday
            .Where(u => u.ServerDate == serverDate)
            .AsNoTracking()
            .ToListAsync(ct);

        // Load category assignments from policy
        var policyEntity = await db.Policies
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
        var alwaysAllowedApps = await db.AppPolicies
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
            var elapsedSeconds = this.GetElapsedSeconds(record);
            var appUsageMinutes = (int)(elapsedSeconds / 60L);
            appMinutes[record.AppId] = appUsageMinutes;

            if (!exemptApps.Contains(record.AppId))
            {
                if (categoryAssignments.TryGetValue(record.AppId, out var category))
                {
                    categoryMinutes[category] = categoryMinutes.GetValueOrDefault(category) + appUsageMinutes;
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
        await using var db = this.dbContextFactory.CreateDbContext();

        var serverDate = this.GetServerDate();
        var record = await db.UsageToday
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.AppId == appId && u.ServerDate == serverDate, ct);

        return record == null ? 0 : (int)this.GetElapsedSeconds(record);
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
        await using var db = this.dbContextFactory.CreateDbContext();

        var existing = await db.Outbox
            .FirstOrDefaultAsync(o => o.DedupKey == dedupKey, ct);

        if (existing != null)
        {
            // Already enqueued — skip
            return;
        }

        db.Outbox.Add(new OutboxDbEntity
        {
            EventType = eventType,
            PayloadJson = payloadJson,
            DedupKey = dedupKey,
            Attempts = 0,
            CreatedAt = this.timeProvider.WallClockNow,
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets pending outbox events (attempts &lt; 5).
    /// </summary>
    public async Task<OutboxDbEntity[]> GetPendingOutboxEventsAsync(int maxCount = 50, CancellationToken ct = default)
    {
        await using var db = this.dbContextFactory.CreateDbContext();

        var entries = await db.Outbox
            .ToListAsync(ct);

        return entries
            .Where(o => o.Attempts < 5)
            .OrderBy(o => o.CreatedAt)
            .Take(maxCount)
            .ToArray();
    }

    /// <summary>
    /// Marks an outbox event as sent (removes it).
    /// </summary>
    public async Task MarkOutboxSentAsync(int id, CancellationToken ct = default)
    {
        await using var db = this.dbContextFactory.CreateDbContext();

        var entity = await db.Outbox.FindAsync([id], ct);
        if (entity != null)
        {
            db.Outbox.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Records a failed attempt for an outbox event.
    /// </summary>
    public async Task MarkOutboxFailedAsync(int id, string error, CancellationToken ct = default)
    {
        await using var db = this.dbContextFactory.CreateDbContext();

        var entity = await db.Outbox.FindAsync([id], ct);
        if (entity != null)
        {
            entity.Attempts++;
            entity.LastAttemptAt = this.timeProvider.WallClockNow;
            entity.LastError = error.Length > 500 ? error[..500] : error;
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Grants ──────────────────────────────────────────────────────────

    /// <summary>
    /// Gets active grants for the given time.
    /// </summary>
    public async Task<Grant[]> GetActiveGrantsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        await using var db = this.dbContextFactory.CreateDbContext();

        var entities = await db.Grants
            .AsNoTracking()
            .ToListAsync(ct);

        return entities
            .Where(g => g.GrantedAt <= now && now < g.ExpiresAt)
            .Select(e => new Grant
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
        await using var db = this.dbContextFactory.CreateDbContext();

        var expired = await db.Grants
            .Where(g => g.ExpiresAt <= now)
            .ToListAsync(ct);

        if (expired.Count > 0)
        {
            db.Grants.RemoveRange(expired);
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────

    private static async Task SyncAppPoliciesAsync(ControlParentalDbContext db, Policy policy, CancellationToken ct)
    {
        // Remove existing app policies for this device
        var existing = await db.AppPolicies
            .Where(a => a.DeviceId == policy.DeviceId)
            .ToListAsync(ct);
        db.AppPolicies.RemoveRange(existing);

        // Add new app policies
        foreach (var appPolicy in policy.AppPolicies)
        {
            var allowedWindowsJson = appPolicy.AllowedWindows != null && appPolicy.AllowedWindows.Length > 0
                ? JsonSerializer.Serialize(appPolicy.AllowedWindows)
                : null;

                db.AppPolicies.Add(new AppPolicyDbEntity
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

    private static async Task SyncGrantsAsync(ControlParentalDbContext db, Policy policy, CancellationToken ct)
    {
        // Remove existing grants for this device
        var existing = await db.Grants
            .Where(g => g.DeviceId == policy.DeviceId)
            .ToListAsync(ct);
        db.Grants.RemoveRange(existing);

        // Add new grants
        foreach (var grant in policy.Grants)
        {
                db.Grants.Add(new GrantDbEntity
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

    private DateOnly GetServerDate()
    {
        return this.timeProvider.ServerDate ?? DateOnly.FromDateTime(this.timeProvider.WallClockNow.UtcDateTime);
    }

    private long GetElapsedSeconds(UsageTodayDbEntity record)
    {
        return record.ElapsedSeconds > 0 ? record.ElapsedSeconds : record.Minutes * 60L;
    }
}
