// <copyright file="WnsJsonContext.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.Text.Json.Serialization;

/// <summary>
/// Payload posted to the backend when the WNS channel is registered or renewed.
/// Source-generated JSON catalogue so the App.UI client does not depend on
/// reflection-based serialization for its outbound push-token registration.
/// </summary>
public sealed class WnsChannelRegistration
{
    /// <inheritdoc/>
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = "wns";

    /// <inheritdoc/>
    [JsonPropertyName("push_handle")]
    public string PushHandle { get; set; } = string.Empty;

    /// <inheritdoc/>
    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; set; } = string.Empty;
}

/// <summary>
/// Source-generated JSON catalogue for the App.UI WNS payload types.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false)]
[JsonSerializable(typeof(WnsChannelRegistration))]
public sealed partial class WnsJsonContext : JsonSerializerContext
{
}
