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
    private readonly ControlParentalDbContext dbContext;
    private readonly JsonSerializerOptions jsonOptions;

    public OutboxManager(ControlParentalDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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
        var payloadJson = JsonSerializer.Serialize(payload, this.jsonOptions);

        var entry = new OutboxDbEntity
        {
            EventType = tableName,
            PayloadJson = payloadJson,
            DedupKey = dedupKey,
            Attempts = 0,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        try
        {
            // Try to insert; ignore if duplicate (dedup key is unique)
            this.dbContext.Outbox.Add(entry);
            await this.dbContext.SaveChangesAsync(cancellationToken);
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
        var entries = await this.dbContext.Outbox
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .Select(e => new OutboxEntry
            {
                Id = e.Id,
                TableName = e.EventType,
                PayloadJson = e.PayloadJson,
                DedupKey = e.DedupKey,
                AttemptCount = e.Attempts,
                CreatedAt = e.CreatedAt,
                LastAttemptAt = e.LastAttemptAt,
                LastError = e.LastError,
            })
            .ToListAsync(cancellationToken);

        return entries.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task MarkSentAsync(int id, CancellationToken cancellationToken = default)
    {
        var entry = await this.dbContext.Outbox.FindAsync(
            new object[] { id },
            cancellationToken);

        if (entry != null)
        {
            this.dbContext.Outbox.Remove(entry);
            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task MarkFailedAsync(
        int id,
        string error,
        CancellationToken cancellationToken = default)
    {
        var entry = await this.dbContext.Outbox.FindAsync(
            new object[] { id },
            cancellationToken);

        if (entry != null)
        {
            entry.Attempts++;
            entry.LastAttemptAt = DateTimeOffset.UtcNow;
            entry.LastError = error.Length > 500 ? error[..500] : error;

            await this.dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Outbox.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task EnqueueIntegrityNotificationAsync(
        string notificationType,
        string title,
        string body,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default)
    {
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
            $"integrity_{notificationType}_{timestamp.ToUnixTimeMilliseconds()}",
            cancellationToken);
    }

    private static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        // SQLite unique constraint violation
        return ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true ||
               ex.InnerException?.Message.Contains("duplicate key") == true;
    }
}
