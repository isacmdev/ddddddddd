// <copyright file="OutboxManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Text.Json;
using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// T03 — Outbox Manager implementation.
/// Uses the outbox pattern for reliable event publishing.
/// </summary>
public sealed class OutboxManager : IOutboxManager
{
    private readonly IDbContextFactory<ControlParentalDbContext> dbContextFactory;
    private readonly ITimeProvider timeProvider;
    private readonly JsonSerializerOptions jsonOptions;

    public OutboxManager(IDbContextFactory<ControlParentalDbContext> dbContextFactory, ITimeProvider timeProvider)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };
    }

    /// <inheritdoc />
    public async Task EnqueueAsync(
        string tableName,
        object payload,
        string dedupKey,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();

        var payloadJson = JsonSerializer.Serialize(payload, this.jsonOptions);

        var entry = new OutboxDbEntity
        {
            EventType = tableName,
            PayloadJson = payloadJson,
            DedupKey = dedupKey,
            Attempts = 0,
            CreatedAt = this.timeProvider.WallClockNow,
            OperationId = dedupKey,
            Status = OutboxEntryStatus.Pending,
        };

        try
        {
            // Try to insert; ignore if duplicate (dedup key is unique)
            dbContext.Outbox.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
        {
            // Already exists - ignore (idempotent)
            System.Diagnostics.Debug.WriteLine(
                $"[OutboxManager] Duplicate entry ignored: {dedupKey}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxEntry>> GetPendingEntriesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();

        var pageSize = Math.Clamp(limit, 0, 1000);
        var now = this.timeProvider.WallClockNow;
        var entries = await dbContext.Outbox
            .FromSqlInterpolated($"SELECT * FROM outbox WHERE status = {(int)OutboxEntryStatus.Pending} AND (next_eligible_at IS NULL OR next_eligible_at <= {now}) ORDER BY created_at, id LIMIT {pageSize}")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return entries.Select(Map).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public async Task MarkSentAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE outbox SET status = {(int)OutboxEntryStatus.Acknowledged}, claimed_until = NULL WHERE id = {id} AND status = {(int)OutboxEntryStatus.Pending} AND claimed_until IS NULL", cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkFailedAsync(
        int id,
        string error,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();
        var safeCode = Redact(error);
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE outbox SET status = {(int)OutboxEntryStatus.Pending}, attempts = attempts + 1, safe_failure_code = {safeCode}, last_error = {safeCode}, last_attempt_at = {this.timeProvider.WallClockNow}, next_eligible_at = {this.timeProvider.WallClockNow}, claimed_until = NULL WHERE id = {id} AND status IN ({(int)OutboxEntryStatus.Pending}, {(int)OutboxEntryStatus.Claimed})", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();
        var now = this.timeProvider.WallClockNow;
        var rows = await dbContext.Outbox
            .FromSqlInterpolated($"SELECT * FROM outbox WHERE status = {(int)OutboxEntryStatus.Pending} AND (next_eligible_at IS NULL OR next_eligible_at <= {now}) LIMIT 1000")
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return rows.Count;
    }

    public async Task<IReadOnlyList<OutboxEntry>> ClaimAsync(
        int limit,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0 || leaseDuration <= TimeSpan.Zero)
        {
            return Array.Empty<OutboxEntry>();
        }

        await using var dbContext = this.dbContextFactory.CreateDbContext();
        await BeginImmediateAsync(dbContext, cancellationToken);
        var now = this.timeProvider.WallClockNow;
        var candidates = await dbContext.Outbox
            .FromSqlInterpolated($"SELECT * FROM outbox WHERE ((status = {(int)OutboxEntryStatus.Pending} AND (next_eligible_at IS NULL OR next_eligible_at <= {now})) OR (status = {(int)OutboxEntryStatus.Claimed} AND claimed_until <= {now})) ORDER BY created_at, id LIMIT {Math.Min(limit, 1000)}")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var leaseUntil = now.Add(leaseDuration);
        var claimed = new List<OutboxDbEntity>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE outbox SET status = {(int)OutboxEntryStatus.Claimed}, operation_id = CASE WHEN operation_id = '' THEN dedup_key ELSE operation_id END, claim_version = claim_version + 1, claimed_until = {leaseUntil}, last_attempt_at = {now}, attempts = attempts + 1 WHERE id = {candidate.Id} AND ((status = {(int)OutboxEntryStatus.Pending} AND (next_eligible_at IS NULL OR next_eligible_at <= {now})) OR (status = {(int)OutboxEntryStatus.Claimed} AND claimed_until <= {now}))", cancellationToken);
            if (changed == 1)
            {
                candidate.Status = OutboxEntryStatus.Claimed;
                candidate.OperationId = string.IsNullOrEmpty(candidate.OperationId) ? candidate.DedupKey : candidate.OperationId;
                candidate.ClaimVersion++;
                candidate.ClaimedUntil = leaseUntil;
                candidate.LastAttemptAt = now;
                candidate.Attempts++;
                claimed.Add(candidate);
            }
        }

        await CommitImmediateAsync(dbContext, cancellationToken);
        return claimed.Select(Map).ToList().AsReadOnly();
    }

    public Task<bool> CompleteAsync(OutboxClaim claim, CancellationToken cancellationToken = default)
        => CompleteAsync(claim.Id, claim.OperationId, claim.ClaimVersion, cancellationToken);

    public async Task<bool> CompleteAsync(OutboxEntry entry, CancellationToken cancellationToken = default)
        => await CompleteAsync(entry.Id, entry.OperationId, entry.ClaimVersion, cancellationToken);

    private async Task<bool> CompleteAsync(
        int id,
        string operationId,
        long claimVersion,
        CancellationToken cancellationToken)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();
        var now = this.timeProvider.WallClockNow;
        return await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE outbox SET status = {(int)OutboxEntryStatus.Acknowledged}, claimed_until = NULL WHERE id = {id} AND operation_id = {operationId} AND claim_version = {claimVersion} AND status = {(int)OutboxEntryStatus.Claimed} AND claimed_until > {now}", cancellationToken) == 1;
    }

    public async Task<bool> FailAsync(
        OutboxEntry entry,
        string safeFailureCode,
        DateTimeOffset? nextEligibleAt = null,
        bool permanent = false,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();
        var now = this.timeProvider.WallClockNow;
        var safeCode = Redact(safeFailureCode);
        var status = permanent || entry.AttemptCount >= Math.Max(1, maxAttempts)
            ? OutboxEntryStatus.DeadLetter
            : OutboxEntryStatus.Pending;
        var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE outbox SET status = {(int)status}, safe_failure_code = {safeCode}, last_error = {safeCode}, next_eligible_at = {nextEligibleAt}, dead_lettered_at = {(status == OutboxEntryStatus.DeadLetter ? now : null)}, claimed_until = NULL WHERE id = {entry.Id} AND operation_id = {entry.OperationId} AND claim_version = {entry.ClaimVersion} AND status = {(int)OutboxEntryStatus.Claimed} AND claimed_until > {now}", cancellationToken);
        return changed == 1;
    }

    public async Task<int> RecoverExpiredClaimsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();
        var now = this.timeProvider.WallClockNow;
        await BeginImmediateAsync(dbContext, cancellationToken);
        try
        {
            var changed = await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE outbox SET status = {(int)OutboxEntryStatus.Pending}, claimed_until = NULL WHERE id IN (SELECT id FROM outbox WHERE status = {(int)OutboxEntryStatus.Claimed} AND claimed_until <= {now} ORDER BY claimed_until, id LIMIT 1000)", cancellationToken);
            await CommitImmediateAsync(dbContext, cancellationToken);
            return changed;
        }
        catch
        {
            await dbContext.Database.ExecuteSqlRawAsync("ROLLBACK");
            throw;
        }
    }

    public async Task<bool> RequeueDeadLetterAsync(
        int id,
        string authorization,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(authorization, "outbox.requeue", StringComparison.Ordinal))
        {
            return false;
        }

        await using var dbContext = this.dbContextFactory.CreateDbContext();
        var now = this.timeProvider.WallClockNow;
        var audit = $"requeue:{now:O}";
        return await dbContext.Database.ExecuteSqlInterpolatedAsync($"UPDATE outbox SET status = {(int)OutboxEntryStatus.Pending}, next_eligible_at = {now}, claimed_until = NULL, audit_reference = CASE WHEN audit_reference IS NULL OR audit_reference = '' THEN {audit} ELSE audit_reference || ';' || {audit} END WHERE id = {id} AND status = {(int)OutboxEntryStatus.DeadLetter}", cancellationToken) == 1;
    }

    /// <inheritdoc />
    public async Task EnqueueIntegrityNotificationAsync(
        string notificationType,
        string title,
        string body,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
        => await this.EnqueueIntegrityNotificationAsync(
            notificationType,
            title,
            body,
            timestamp,
            $"integrity_{notificationType}_{timestamp.ToUnixTimeMilliseconds()}",
            cancellationToken);

    /// <inheritdoc />
    public async Task EnqueueIntegrityNotificationAsync(
        string notificationType,
        string title,
        string body,
        DateTimeOffset timestamp,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            throw new ArgumentException("An explicit idempotency key is required.", nameof(idempotencyKey));
        }

        var payload = new
        {
            NotificationType = notificationType,
            Title = title,
            Body = body,
            Timestamp = timestamp.ToString("O"),
        };

        await this.EnqueueAsync(
            "notifications",
            payload,
            idempotencyKey,
            cancellationToken);
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        // SQLite unique constraint violation
        return ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true ||
               ex.InnerException?.Message.Contains("duplicate key") == true;
    }

    private static async Task BeginImmediateAsync(
        ControlParentalDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("BEGIN IMMEDIATE", cancellationToken);
    }

    private static async Task CommitImmediateAsync(
        ControlParentalDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync("COMMIT", cancellationToken);
        await dbContext.Database.CloseConnectionAsync();
    }

    private static OutboxEntry Map(OutboxDbEntity entry) => new()
    {
        Id = entry.Id,
        TableName = entry.EventType,
        PayloadJson = entry.PayloadJson,
        DedupKey = entry.DedupKey,
        AttemptCount = entry.Attempts,
        CreatedAt = entry.CreatedAt,
        LastAttemptAt = entry.LastAttemptAt,
        LastError = entry.LastError,
        Status = entry.Status,
        OperationId = entry.OperationId,
        ClaimVersion = entry.ClaimVersion,
        ClaimedUntil = entry.ClaimedUntil,
        NextEligibleAt = entry.NextEligibleAt,
        DeadLetteredAt = entry.DeadLetteredAt,
        SafeFailureCode = entry.SafeFailureCode,
        AuditReference = entry.AuditReference,
    };

    private static string Redact(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "temporary" or "permanent" or "network" or "timeout" or "cancelled"
            ? normalized
            : "redacted-failure";
    }
}
