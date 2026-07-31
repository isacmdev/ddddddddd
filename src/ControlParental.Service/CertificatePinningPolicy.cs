// <copyright file="CertificatePinningPolicy.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ControlParental.Domain;

/// <summary>
/// Unified TLS/pinning policy for all service-owned HTTPS traffic.
/// </summary>
public sealed class CertificatePinningPolicy : ITlsPolicy
{
    private readonly string? certPin;

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificatePinningPolicy"/> class.
    /// </summary>
    /// <param name="certPin">Optional SPKI pin (Base64 SHA-256 of the certificate's SubjectPublicKeyInfo).</param>
    public CertificatePinningPolicy(string? certPin = null)
    {
        this.certPin = certPin;
    }

    /// <inheritdoc />
    public TimeSpan DefaultTimeout => TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public HttpClientHandler ConfigureHandler()
    {
        var handler = new HttpClientHandler
        {
            SslProtocols = SslProtocols.Tls13,
            ServerCertificateCustomValidationCallback = this.RemoteCertificateValidationCallback,
        };

        return handler;
    }

    private bool RemoteCertificateValidationCallback(
        HttpRequestMessage request,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors errors)
    {
        if (certificate == null)
        {
            return this.certPin == null;
        }

        if (string.IsNullOrWhiteSpace(this.certPin))
        {
            return errors == SslPolicyErrors.None;
        }

        try
        {
            _ = CertificatePinningValidator.Validate(this.certPin, certificate);
            return true;
        }
        catch (CertificatePinValidationException)
        {
            return false;
        }
    }
}
