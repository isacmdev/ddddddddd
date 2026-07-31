// <copyright file="Grant.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json.Serialization;

/// <summary>
/// A time grant (permission) created when the tutor approves a time request.
/// </summary>
public sealed record Grant
{
    /// <summary>
    /// Gets the unique identifier for this grant.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Gets the request_id from the original time_request (for idempotency).
    /// </summary>
    [JsonPropertyName("request_id")]
    public string? RequestId { get; init; }

    /// <summary>
    /// Gets the scope: device, package_name, or category.
    /// </summary>
    [JsonPropertyName("scope")]
    public string Scope { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of minutes granted.
    /// </summary>
    [JsonPropertyName("minutes")]
    public int Minutes { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the grant was created.
    /// </summary>
    [JsonPropertyName("granted_at")]
    public DateTimeOffset GrantedAt { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the grant expires.
    /// Must be after granted_at.
    /// </summary>
    [JsonPropertyName("expires_at")]
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>
    /// Gets the source: extra_time, reward, or manual.
    /// </summary>
    [JsonPropertyName("source")]
    public GrantSource Source { get; init; }

    /// <summary>
    /// Validates the grant invariants.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the grant is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.Id))
        {
            throw new ArgumentException("Grant must have a non-empty Id.", nameof(this.Id));
        }

        if (string.IsNullOrWhiteSpace(this.Scope))
        {
            throw new ArgumentException("Grant must have a non-empty scope.", nameof(this.Scope));
        }

        if (this.Minutes <= 0)
        {
            throw new ArgumentException(
                $"Grant minutes must be positive. Got {this.Minutes}.",
                nameof(this.Minutes));
        }

        if (this.ExpiresAt <= this.GrantedAt)
        {
            throw new ArgumentException(
                $"Grant expires_at ({this.ExpiresAt}) must be after granted_at ({this.GrantedAt}).",
                nameof(this.ExpiresAt));
        }
    }

    /// <summary>
    /// Checks if the grant is currently active (within the validity window).
    /// </summary>
    /// <param name="now">The current time to check against.</param>
    /// <returns>True if the grant is active.</returns>
    public bool IsActive(DateTimeOffset now) => now >= this.GrantedAt && now < this.ExpiresAt;
}
