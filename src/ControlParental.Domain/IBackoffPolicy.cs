// <copyright file="IBackoffPolicy.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Policy that decides how long to wait before the next retry attempt.
/// </summary>
public interface IBackoffPolicy
{
    /// <summary>
    /// Gets the initial delay that makes the first execution runnable.
    /// </summary>
    int InitialDelay { get; }

    /// <summary>
    /// Gets the next delay given the current delay.
    /// </summary>
    /// <param name="currentDelay">Current delay in seconds.</param>
    /// <returns>Next delay in seconds.</returns>
    int GetNextDelay(int currentDelay);
}
