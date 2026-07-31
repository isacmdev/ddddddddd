// <copyright file="HttpOutcome.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Outcome of an HTTP call through the centralized backend boundary.
/// </summary>
public enum HttpOutcome
{
    /// <summary>Success (2xx).</summary>
    Success,

    /// <summary>Retryable failure (5xx, timeout, cancellation).</summary>
    Transient,

    /// <summary>Non-retryable failure (4xx).</summary>
    Permanent,

    /// <summary>Network transport failure.</summary>
    Network,

    /// <summary>Local serialization/parsing failure.</summary>
    Malformed,
}
