// <copyright file="CertificatePinningValidator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// T22 — Exception thrown when certificate pin validation fails.
/// </summary>
public sealed class CertificatePinValidationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CertificatePinValidationException"/> class.
    /// </summary>
    public CertificatePinValidationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificatePinValidationException"/> class.
    /// </summary>
    /// <param name="message">The message describing the failure.</param>
    public CertificatePinValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CertificatePinValidationException"/> class.
    /// </summary>
    /// <param name="message">The message describing the failure.</param>
    /// <param name="innerException">The inner exception.</param>
    public CertificatePinValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// T22 — Validates TLS certificate pins using SPKI (Subject Public Key Info) hash.
///
/// <para>
/// <b>SPKI format:</b> SHA-256 hash of the DER-encoded SubjectPublicKeyInfo from the
/// certificate's public key. This format survives certificate renewals that reuse the
/// same key pair, while still providing protection against key compromise.
/// </para>
///
/// <para>
/// <b>Rotation procedure:</b>
/// When the backend rotates its certificate (new key pair):
/// 1. Obtain the new certificate (e.g., via OpenSSL or from the backend team).
/// 2. Calculate the new pin:
///    <c>openssl x509 -in cert.pem -pubkey -noout | openssl pkey -pubin -outform DER | openssl sha256 -binary | openssl base64</c>
/// 3. Update <c>SUPABASE_CERT_PIN</c> in .env / ProgramData.
/// 4. Restart the service.
/// </para>
/// </summary>
public static class CertificatePinningValidator
{
    /// <summary>
    /// Validates that the presented certificate matches the expected SPKI pin.
    /// </summary>
    /// <param name="expectedPin">The expected SHA-256 SPKI pin in Base64 format.</param>
    /// <param name="certificate">The certificate presented by the server (may be null).</param>
    /// <returns><c>true</c> if the pin matches and no exception is thrown.</returns>
    /// <exception cref="CertificatePinValidationException">
    /// Thrown when the certificate's SPKI pin does not match the expected pin,
    /// when the certificate is null, or when the public key cannot be extracted.
    /// </exception>
    public static bool Validate(string expectedPin, X509Certificate2? certificate)
    {
        if (certificate == null)
        {
            throw new CertificatePinValidationException(
                "No certificate was presented by the server. Connection rejected.");
        }

        byte[] spkiDer;
        try
        {
            // SPKI = DER-encoded SubjectPublicKeyInfo (RFC 5280 §4.1)
            // Use recommended API to extract public key bytes (avoids obsolete PublicKey.Key)
            var rsa = certificate.GetRSAPublicKey();
            if (rsa != null)
            {
                spkiDer = rsa.ExportSubjectPublicKeyInfo();
            }
            else
            {
                var ecdsa = certificate.GetECDsaPublicKey();
                if (ecdsa != null)
                {
                    spkiDer = ecdsa.ExportSubjectPublicKeyInfo();
                }
                else
                {
                    throw new CertificatePinValidationException(
                        "Certificate public key algorithm is not supported. Only RSA and ECDSA are supported.");
                }
            }
        }
        catch (CertificatePinValidationException)
        {
            throw;
        }
        catch (CryptographicException ex)
        {
            throw new CertificatePinValidationException(
                $"Failed to extract public key from certificate: {ex.Message}", ex);
        }

        if (spkiDer == null || spkiDer.Length == 0)
        {
            throw new CertificatePinValidationException(
                "Certificate public key (SPKI) is empty or invalid.");
        }

        byte[] hash;
        try
        {
            hash = SHA256.HashData(spkiDer);
        }
        catch (CryptographicException ex)
        {
            throw new CertificatePinValidationException(
                $"Failed to compute SHA-256 of SPKI: {ex.Message}", ex);
        }

        string computedPin = Convert.ToBase64String(hash);

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(computedPin),
                System.Text.Encoding.ASCII.GetBytes(expectedPin)))
        {
            throw new CertificatePinValidationException(
                "Certificate pin mismatch. Connection rejected.");
        }

        return true;
    }

    /// <summary>
    /// Calculates the SPKI pin (SHA-256 of DER-encoded SubjectPublicKeyInfo) from a certificate.
    /// </summary>
    /// <param name="certificate">The certificate to pin.</param>
    /// <returns>The SPKI pin in Base64 format.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="certificate"/> is null.</exception>
    public static string CalculateSpkiPin(X509Certificate2 certificate)
    {
        if (certificate == null)
        {
            throw new ArgumentNullException(nameof(certificate));
        }

        byte[] spkiDer;
        var rsa = certificate.GetRSAPublicKey();
        if (rsa != null)
        {
            spkiDer = rsa.ExportSubjectPublicKeyInfo();
        }
        else
        {
            var ecdsa = certificate.GetECDsaPublicKey();
            if (ecdsa != null)
            {
                spkiDer = ecdsa.ExportSubjectPublicKeyInfo();
            }
            else
            {
                throw new CertificatePinValidationException(
                    "Certificate public key algorithm is not supported. Only RSA and ECDSA are supported.");
            }
        }
        byte[] hash = SHA256.HashData(spkiDer);
        return Convert.ToBase64String(hash);
    }
}
