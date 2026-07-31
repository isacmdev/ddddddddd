// <copyright file="ITlsPolicy.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Net.Http;

/// <summary>
/// Unified TLS/pinning policy for all service-owned HTTPS traffic.
/// </summary>
public interface ITlsPolicy
{
    /// <summary>
    /// Configures an <see cref="HttpClientHandler"/> with the policy's TLS settings.
    /// </summary>
    /// <returns>A configured handler.</returns>
    HttpClientHandler ConfigureHandler();

    /// <summary>
    /// Gets the default request timeout for this policy.
    /// </summary>
    TimeSpan DefaultTimeout { get; }
}
