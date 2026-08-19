// <copyright file="PolicyDbEntity.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T03 — Stored policy entity in SQLite.
/// Stores the complete JSON policy along with version for atomic upgrade guard.
/// </summary>
public sealed class PolicyDbEntity
{
    /// <summary>
    /// Device ID (primary key).
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Policy version number (for atomic upgrade guard: apply only if newVersion > versionLocal).
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Full JSON of the policy (for debugging/audit).
    /// </summary>
    public string PolicyJson { get; set; } = string.Empty;

    /// <summary>
    /// Last update timestamp (UTC).
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }

    /// <summary>
    /// Category assignments map (JSON) for quick lookup by T02.
    /// </summary>
    public string CategoryAssignmentsJson { get; set; } = "{}";
}

/// <summary>
/// T03 — Stored app policy entity.
/// </summary>
public sealed class AppPolicyDbEntity
{
    /// <summary>
    /// Auto-generated primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Device ID (foreign key to PolicyDbEntity).
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// App package name.
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Policy state as string (Allowed, Blocked, Limited, AlwaysAllowed).
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// Daily limit minutes (null if not limited).
    /// </summary>
    public int? DailyLimitMinutes { get; set; }

    /// <summary>
    /// App category name.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Allowed windows JSON array.
    /// </summary>
    public string? AllowedWindowsJson { get; set; }

    /// <summary>
    /// Navigation property.
    /// </summary>
    public PolicyDbEntity? Policy { get; set; }
}

/// <summary>
/// T03 — Stored grant entity.
/// </summary>
public sealed class GrantDbEntity
{
    /// <summary>
    /// Auto-generated primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Device ID (foreign key to PolicyDbEntity).
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Grant ID.
    /// </summary>
    public string GrantId { get; set; } = string.Empty;

    /// <summary>
    /// Request ID that created this grant.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Grant scope (device, package name, or category).
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Minutes granted.
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// When the grant was given.
    /// </summary>
    public DateTimeOffset GrantedAt { get; set; }

    /// <summary>
    /// When the grant expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Source of the grant (extra_time, reward, manual).
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property.
    /// </summary>
    public PolicyDbEntity? Policy { get; set; }
}

/// <summary>
/// T03 — Daily usage tracking entity.
/// Key: (AppId, ServerDate).
/// </summary>
public sealed class UsageTodayDbEntity
{
    /// <summary>
    /// App package name.
    /// </summary>
    public string AppId { get; set; } = string.Empty;

    /// <summary>
    /// Server date (UTC date, from ITimeProvider.ServerDate).
    /// </summary>
    public DateOnly ServerDate { get; set; }

    /// <summary>
    /// Total elapsed usage seconds used today for this app.
    /// This is the canonical persisted value; Minutes remains as the legacy floor.
    /// </summary>
    public long ElapsedSeconds { get; set; }

    /// <summary>
    /// Minutes used today for this app.
    /// Legacy compatibility column retained for rollback/backfill.
    /// </summary>
    public int Minutes { get; set; }

    /// <summary>
    /// Last update timestamp.
    /// </summary>
    public DateTimeOffset LastUpdated { get; set; }
}

/// <summary>
/// T03 — Outbox entity for offline sync.
/// </summary>
public sealed class OutboxDbEntity
{
    /// <summary>
    /// Auto-generated primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Event type (usage_log, device_alert, behavioral_event, time_request, heartbeat).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// JSON payload of the event.
    /// </summary>
    public string PayloadJson { get; set; } = string.Empty;

    /// <summary>
    /// Deduplication key (prevents double-send).
    /// </summary>
    public string DedupKey { get; set; } = string.Empty;

    /// <summary>
    /// Number of send attempts.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Last attempt timestamp.
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>
    /// Last error message (if any).
    /// </summary>
    public string? LastError { get; set; }

    public OutboxEntryStatus Status { get; set; } = OutboxEntryStatus.Pending;
    public string OperationId { get; set; } = string.Empty;
    public long ClaimVersion { get; set; }
    public DateTimeOffset? ClaimedUntil { get; set; }
    public DateTimeOffset? NextEligibleAt { get; set; }
    public DateTimeOffset? DeadLetteredAt { get; set; }
    public string? SafeFailureCode { get; set; }
    public string? AuditReference { get; set; }
}
