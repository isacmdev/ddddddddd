// <copyright file="ExponentialBackoffPolicy.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// Exponential backoff policy: first failure waits <see cref="InitialBackoffSeconds"/>,
/// then doubles up to <see cref="MaxBackoffSeconds"/>.
/// </summary>
public sealed class ExponentialBackoffPolicy : IBackoffPolicy
{
    /// <summary>
    /// Initial backoff after the first failure.
    /// </summary>
    public const int InitialBackoffSeconds = 1;

    /// <summary>
    /// Maximum backoff in seconds.
    /// </summary>
    public const int MaxBackoffSeconds = 300;

    /// <inheritdoc />
    public int InitialDelay => 0;

    /// <inheritdoc />
    public int GetNextDelay(int currentDelay)
    {
        if (currentDelay == 0)
        {
            return InitialBackoffSeconds;
        }

        return Math.Min(currentDelay * 2, MaxBackoffSeconds);
    }
}
