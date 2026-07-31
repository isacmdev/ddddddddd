// <copyright file="CategoryLimit.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json.Serialization;

/// <summary>
/// A daily time limit for a category of apps.
/// </summary>
public sealed record CategoryLimit
{
    /// <summary>
    /// Gets the category name (e.g., "games", "social").
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets the maximum minutes per day for this category.
    /// </summary>
    [JsonPropertyName("minutes")]
    public int Minutes { get; init; }

    /// <summary>
    /// Validates the category limit.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the limit is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(this.Category))
        {
            throw new ArgumentException(
                "CategoryLimit must have a non-empty category.",
                nameof(this.Category));
        }

        if (this.Minutes <= 0)
        {
            throw new ArgumentException(
                $"CategoryLimit minutes must be positive. Got {this.Minutes}.",
                nameof(this.Minutes));
        }
    }
}
