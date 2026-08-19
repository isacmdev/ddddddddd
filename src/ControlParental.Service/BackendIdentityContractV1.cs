// <copyright file="BackendIdentityContractV1.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Globalization;
using System.Text.Json;

/// <summary>
/// Stable failures exposed by the local v1 contract harness. They are not evidence of backend availability.
/// </summary>
public enum BackendIdentityErrorV1
{
    None,
    InvalidPayload,
    UnsupportedSchema,
    UnsupportedVersion,
    NotFound,
    Gone,
    RateLimited,
    Timeout,
    Cancelled,
    ClaimMismatch,
    Duplicate,
    Replay,
    Revoked,
    Forbidden,
    CapacityExceeded,
}

/// <summary>
/// Credential-free claim shape used to prove exact backend identity binding without parsing a JWT.
/// </summary>
/// <param name="DeviceId">Backend-issued device identity.</param>
public sealed record BackendIdentityClaimV1(string DeviceId);

/// <summary>
/// Deterministic, non-secret receipt produced by a local contract probe.
/// </summary>
/// <param name="Id">Stable receipt identifier.</param>
/// <param name="OperationId">Caller-provided non-secret operation identifier.</param>
/// <param name="Error">Typed contract outcome.</param>
/// <param name="ObservedAt">Controlled fixture time.</param>
/// <param name="RetryAfter">Server-requested delay when rate limited.</param>
public sealed record BackendIdentityReceiptV1(
    string Id,
    string OperationId,
    BackendIdentityErrorV1 Error,
    DateTimeOffset ObservedAt,
    TimeSpan? RetryAfter)
{
    /// <summary>
    /// Gets a value indicating whether an external backend supplied this receipt.
    /// Local v1 receipts can never assert external verification.
    /// </summary>
    public bool ExternalVerified => false;
}

/// <summary>
/// Parsed v1 response. Tokens, pairing codes, and parent identifiers are intentionally absent.
/// </summary>
/// <param name="Error">Typed contract outcome.</param>
/// <param name="DeviceId">Backend identity only for an accepted response.</param>
/// <param name="RetryAfter">Rate-limit delay, if present.</param>
/// <param name="Receipt">Deterministic local receipt, if the envelope was valid.</param>
public sealed record BackendIdentityResponseV1(
    BackendIdentityErrorV1 Error,
    string? DeviceId = null,
    TimeSpan? RetryAfter = null,
    BackendIdentityReceiptV1? Receipt = null)
{
    public bool IsSuccess => this.Error == BackendIdentityErrorV1.None;
}

/// <summary>
/// Parses and probes the bounded backend identity contract v1 without performing network or retry work.
/// </summary>
public static class BackendIdentityContractV1
{
    public const string Schema = "backend-identity";
    public const int Version = 1;
    public const int MaximumPayloadBytes = 16 * 1024;
    private const int MaximumProbeDevices = 2;
    private static readonly HashSet<string> ProbeDevices = new(StringComparer.Ordinal)
    {
        "device-a",
        "device-b",
    };

    /// <summary>
    /// Parses one bounded JSON envelope in O(payload bytes), failing closed on unknown or incomplete input.
    /// </summary>
    public static BackendIdentityResponseV1 Parse(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return Failure(BackendIdentityErrorV1.InvalidPayload);
        }

        if (payload.Length > MaximumPayloadBytes)
        {
            return Failure(BackendIdentityErrorV1.CapacityExceeded);
        }

        try
        {
            using var document = JsonDocument.Parse(payload.ToArray());
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryGetString(root, "schema", out var schema))
            {
                return Failure(BackendIdentityErrorV1.InvalidPayload);
            }

            if (!string.Equals(schema, Schema, StringComparison.Ordinal))
            {
                return Failure(BackendIdentityErrorV1.UnsupportedSchema);
            }

            if (!root.TryGetProperty("version", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetInt32(out var version))
            {
                return Failure(BackendIdentityErrorV1.InvalidPayload);
            }

            if (version != Version)
            {
                return Failure(BackendIdentityErrorV1.UnsupportedVersion);
            }

            if (!TryGetString(root, "operation_id", out var operationId) ||
                !TryGetString(root, "status", out var status) ||
                !TryGetTimestamp(root, out var serverTime))
            {
                return Failure(BackendIdentityErrorV1.InvalidPayload);
            }

            var error = MapStatus(status);
            if (error == BackendIdentityErrorV1.InvalidPayload)
            {
                return Failure(error);
            }

            string? deviceId = null;
            if (error == BackendIdentityErrorV1.None && !TryGetString(root, "device_id", out deviceId))
            {
                return Failure(BackendIdentityErrorV1.InvalidPayload);
            }

            var retryAfter = ReadRetryAfter(root, error);
            if (error == BackendIdentityErrorV1.RateLimited && retryAfter is null)
            {
                return Failure(BackendIdentityErrorV1.InvalidPayload);
            }

            var receipt = CreateReceipt(operationId, error, serverTime, retryAfter);
            return new BackendIdentityResponseV1(error, deviceId, retryAfter, receipt);
        }
        catch (JsonException)
        {
            return Failure(BackendIdentityErrorV1.InvalidPayload);
        }
    }

    /// <summary>
    /// Maps HTTP statuses without parsing or retaining response bodies.
    /// </summary>
    public static BackendIdentityResponseV1 FromHttpFailure(int statusCode, TimeSpan? retryAfter = null)
        => statusCode switch
        {
            404 => Failure(BackendIdentityErrorV1.NotFound),
            410 => Failure(BackendIdentityErrorV1.Gone),
            429 when retryAfter > TimeSpan.Zero => new(BackendIdentityErrorV1.RateLimited, RetryAfter: retryAfter),
            _ => Failure(BackendIdentityErrorV1.InvalidPayload),
        };

    /// <summary>
    /// Validates exact, ordinal claim binding in O(identity bytes).
    /// </summary>
    public static BackendIdentityErrorV1 ValidateDeviceClaim(
        string expectedDeviceId,
        BackendIdentityClaimV1? claim)
        => !string.IsNullOrWhiteSpace(expectedDeviceId) &&
           !string.IsNullOrWhiteSpace(claim?.DeviceId) &&
           string.Equals(expectedDeviceId, claim.DeviceId, StringComparison.Ordinal)
            ? BackendIdentityErrorV1.None
            : BackendIdentityErrorV1.ClaimMismatch;

    /// <summary>
    /// Creates an O(1), deterministic local receipt. It never claims external verification.
    /// </summary>
    public static BackendIdentityReceiptV1 CreateReceipt(
        string operationId,
        BackendIdentityErrorV1 error,
        DateTimeOffset observedAt,
        TimeSpan? retryAfter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        var status = error == BackendIdentityErrorV1.None ? "accepted" : ToContractName(error);
        var id = string.Create(
            CultureInfo.InvariantCulture,
            $"v1:{operationId}:{status}:{observedAt:O}");
        return new BackendIdentityReceiptV1(id, operationId, error, observedAt, retryAfter);
    }

    /// <summary>
    /// Probes exactly two fixture identities with O(1) set lookups. Client-selected row identity is never trusted.
    /// </summary>
    public static BackendIdentityReceiptV1 ProbeTwoDeviceAccess(
        string callerDeviceId,
        string requestedDeviceId,
        IReadOnlySet<string> revokedDevices,
        DateTimeOffset observedAt)
    {
        ArgumentNullException.ThrowIfNull(revokedDevices);
        var error = ResolveProbeError(callerDeviceId, requestedDeviceId, revokedDevices);
        return CreateReceipt("rls-probe", error, observedAt);
    }

    private static BackendIdentityErrorV1 ResolveProbeError(
        string callerDeviceId,
        string requestedDeviceId,
        IReadOnlySet<string> revokedDevices)
    {
        if (revokedDevices.Count > MaximumProbeDevices)
        {
            return BackendIdentityErrorV1.CapacityExceeded;
        }

        if (!ProbeDevices.Contains(callerDeviceId) || !ProbeDevices.Contains(requestedDeviceId))
        {
            return BackendIdentityErrorV1.Forbidden;
        }

        if (revokedDevices.Contains(callerDeviceId))
        {
            return BackendIdentityErrorV1.Revoked;
        }

        return string.Equals(callerDeviceId, requestedDeviceId, StringComparison.Ordinal)
            ? BackendIdentityErrorV1.None
            : BackendIdentityErrorV1.Forbidden;
    }

    private static BackendIdentityResponseV1 Failure(BackendIdentityErrorV1 error)
        => new(error);

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetTimestamp(JsonElement root, out DateTimeOffset value)
    {
        value = default;
        return root.TryGetProperty("server_time", out var element) &&
            element.ValueKind == JsonValueKind.String &&
            element.TryGetDateTimeOffset(out value);
    }

    private static TimeSpan? ReadRetryAfter(JsonElement root, BackendIdentityErrorV1 error)
        => error == BackendIdentityErrorV1.RateLimited &&
           root.TryGetProperty("retry_after_seconds", out var element) &&
           element.ValueKind == JsonValueKind.Number &&
           element.TryGetInt32(out var seconds) &&
           seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : null;

    private static BackendIdentityErrorV1 MapStatus(string? status)
        => status switch
        {
            "accepted" => BackendIdentityErrorV1.None,
            "not_found" => BackendIdentityErrorV1.NotFound,
            "gone" => BackendIdentityErrorV1.Gone,
            "rate_limited" => BackendIdentityErrorV1.RateLimited,
            "duplicate" => BackendIdentityErrorV1.Duplicate,
            "replay" => BackendIdentityErrorV1.Replay,
            "revoked" => BackendIdentityErrorV1.Revoked,
            _ => BackendIdentityErrorV1.InvalidPayload,
        };

    private static string ToContractName(BackendIdentityErrorV1 error)
        => error switch
        {
            BackendIdentityErrorV1.RateLimited => "rate-limited",
            BackendIdentityErrorV1.CapacityExceeded => "capacity-exceeded",
            _ => error.ToString().ToLowerInvariant(),
        };
}
