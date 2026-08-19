// <copyright file="OutboxEntryStatus.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Status of an outbox entry.
/// </summary>
public enum OutboxEntryStatus
{
    /// <summary>Entry is pending submission.</summary>
    Pending,

    /// <summary>Entry was submitted successfully.</summary>
    Sent,

    /// <summary>Entry failed with a retryable error.</summary>
    Failed,

    /// <summary>Entry failed with a permanent error and will not retry.</summary>
    DeadLetter,

    /// <summary>Entry is reserved by one dispatcher lease.</summary>
    Claimed,

    /// <summary>Remote acknowledgement was durably recorded.</summary>
    Acknowledged,
}

/// <summary>Durable identity and lease returned when an entry is claimed.</summary>
public sealed record OutboxClaim(
    int Id,
    string OperationId,
    long ClaimVersion,
    DateTimeOffset LeaseUntil);
