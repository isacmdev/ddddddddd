// <copyright file="AgeBand.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T24 — Age band for the child account.
/// </summary>
public enum AgeBand
{
    /// <summary>
    /// Child: 7-12 years old.
    /// </summary>
    Child = 0,

    /// <summary>
    /// Preteen: 13-16 years old.
    /// </summary>
    Preteen = 1,

    /// <summary>
    /// Teen: 17-18 years old.
    /// </summary>
    Teen = 2,
}

/// <summary>
/// T24 — Extension methods for AgeBand.
/// </summary>
public static class AgeBandExtensions
{
    /// <summary>
    /// Converts an AgeBand to its string representation (e.g., "7-12").
    /// </summary>
    /// <param name="band">The age band.</param>
    /// <returns>String representation of the age band.</returns>
    public static string ToString(this AgeBand band)
        => band switch
        {
            AgeBand.Child => "7-12",
            AgeBand.Preteen => "13-16",
            AgeBand.Teen => "17-18",
            _ => throw new ArgumentOutOfRangeException(nameof(band)),
        };

    /// <summary>
    /// Attempts to parse a string to an AgeBand.
    /// </summary>
    /// <param name="value">The string to parse (e.g., "7-12", "13-16", "17-18").</param>
    /// <param name="band">The parsed age band if successful.</param>
    /// <returns>True if parsing succeeded; otherwise, false.</returns>
    public static bool TryParse(string value, out AgeBand band)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            band = default;
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.ToUpperInvariant() switch
        {
            "7-12" => SetBand(AgeBand.Child, out band),
            "13-16" => SetBand(AgeBand.Preteen, out band),
            "17-18" => SetBand(AgeBand.Teen, out band),
            _ => TryParseCaseInsensitive(trimmed, out band),
        };
    }

    private static bool SetBand(AgeBand value, out AgeBand band)
    {
        band = value;
        return true;
    }

    private static bool TryParseCaseInsensitive(string value, out AgeBand band)
    {
        var upper = value.ToUpperInvariant();
        if (upper is "CHILD" or "PRETWEEN" or "TEEN")
        {
            band = upper switch
            {
                "CHILD" => AgeBand.Child,
                "PRETWEEN" => AgeBand.Preteen,
                "TEEN" => AgeBand.Teen,
                _ => default,
            };
            return true;
        }

        band = default;
        return false;
    }
}
