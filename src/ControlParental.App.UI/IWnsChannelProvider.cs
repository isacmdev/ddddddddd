// <copyright file="IWnsChannelProvider.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

/// <summary>Provides a real WNS channel intent without exposing transport metadata to the UI.</summary>
public interface IWnsChannelProvider
{
    /// <summary>Creates one channel intent, or returns null when WNS is unavailable.</summary>
    Task<WnsChannelIntent?> CreateChannelAsync(CancellationToken cancellationToken = default);
}
