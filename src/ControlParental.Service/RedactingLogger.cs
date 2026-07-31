// <copyright file="RedactingLogger.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Text.Json;
using ControlParental.Domain;

/// <summary>
/// Structured logger that redacts sensitive fields before persisting diagnostics.
/// </summary>
public sealed class RedactingLogger
{
    private readonly IReadOnlySet<string> allowList;
    private readonly IReadOnlySet<string> denyList;
    private readonly string redactionToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedactingLogger"/> class.
    /// </summary>
    /// <param name="allowList">Field names that may be persisted as-is.</param>
    /// <param name="denyList">Field names that must always be redacted.</param>
    /// <param name="redactionToken">Token used to replace redacted values.</param>
    public RedactingLogger(
        IEnumerable<string>? allowList = null,
        IEnumerable<string>? denyList = null,
        string redactionToken = "[REDACTED]")
    {
        this.allowList = allowList?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        this.denyList = denyList?.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        this.redactionToken = redactionToken;
    }

    /// <summary>
    /// Gets default allow/deny lists for the ControlParental service.
    /// </summary>
    public static RedactingLogger Default => new(
        allowList: new[]
        {
            "event_name",
            "event_type",
            "severity",
            "detected_at",
            "device_id",
            "status",
            "version",
            "platform",
            "app_id",
            "minutes",
            "server_date",
            "timestamp",
            "timezone",
            "clock_jump_detected",
        },
        denyList: new[]
        {
            "access_token",
            "refresh_token",
            "id_token",
            "authorization",
            "apikey",
            "channel_uri",
            "push_handle",
            "raw_body",
            "credentials",
            "password",
            "secret",
            "report_hash",
            "binary_hash",
            "executable_path",
            "payload",
        });

    /// <summary>
    /// Writes a structured event after redacting disallowed fields.
    /// </summary>
    /// <param name="eventName">Name of the event.</param>
    /// <param name="fields">Original fields.</param>
    /// <returns>Redacted fields serialized as JSON.</returns>
    public string Write(string eventName, IReadOnlyDictionary<string, object?> fields)
    {
        var safe = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["event_name"] = eventName,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
        };

        foreach (var (key, value) in fields)
        {
            safe[key] = this.Sanitize(key, value);
        }

        return JsonSerializer.Serialize(safe, SharedJsonContext.Default.DictionaryStringObject);
    }

    private object? Sanitize(string key, object? value)
    {
        if (this.denyList.Contains(key))
        {
            return this.redactionToken;
        }

        if (!this.allowList.Contains(key))
        {
            // Unknown field: keep structure but strip scalar values.
            if (value is null)
            {
                return null;
            }

            if (value is string)
            {
                return this.redactionToken;
            }

            if (value is System.Collections.IEnumerable enumerable && value is not string)
            {
                return "[collection]";
            }

            return this.redactionToken;
        }

        // Allow-listed scalar values pass through, but redact nested secrets.
        if (value is IReadOnlyDictionary<string, object?> nested)
        {
            var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (k, v) in nested)
            {
                sanitized[k] = this.Sanitize(k, v);
            }

            return sanitized;
        }

        return value;
    }
}
