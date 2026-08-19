// <copyright file="WnsChannelIntent.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

/// <summary>Internal channel data forwarded once to the Service registration port.</summary>
public sealed record WnsChannelIntent(string Uri, DateTimeOffset ExpiresAt);
