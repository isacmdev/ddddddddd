// <copyright file="IUIPipeClient.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.Domain;

/// <summary>
/// T27 — Interface for UI to query usage state from the Service.
/// </summary>
public interface IUIPipeClient : IDisposable
{
    /// <summary>
    /// Gets the current usage state from the Service.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The current usage state.</returns>
    Task<UsageStateResponse> GetUsageStateAsync(CancellationToken ct = default);
}
