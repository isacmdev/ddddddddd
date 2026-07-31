// <copyright file="CertificatePinningValidatorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControlParental.Service;
using FluentAssertions;
using Xunit;

/// <summary>
/// T22 — Tests for <see cref="CertificatePinningValidator"/>.
/// </summary>
public class CertificatePinningValidatorTests : IDisposable
{
    /// <summary>
    /// Helper: creates a self-signed X509Certificate2 with RSA-2048.
    /// </summary>
    private static X509Certificate2 CreateRsaCertificate(string commonName)
    {
        var dn = new X500DistinguishedName($"CN={commonName}");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(dn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(1);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>
    /// Helper: creates a self-signed X509Certificate2 with ECDSA P-256.
    /// </summary>
    private static X509Certificate2 CreateEcdsaCertificate(string commonName)
    {
        var dn = new X500DistinguishedName($"CN={commonName}");
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(dn, ecdsa, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));

        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(1);
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    [Fact]
    public void Validate_CertA_AgainstPinA_Succeeds()
    {
        // Arrange: create cert A, compute its real SPKI pin
        using var certA = CreateRsaCertificate("supabase.co");
        var pinA = CertificatePinningValidator.CalculateSpkiPin(certA);

        // Act: validate cert A against its own pin
        var result = CertificatePinningValidator.Validate(pinA, certA);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Validate_CertB_AgainstPinA_Throws()
    {
        // Arrange: create cert A and compute its pin; create cert B with a DIFFERENT key
        using var certA = CreateRsaCertificate("supabase.co");
        using var certB = CreateRsaCertificate("other-domain.com");
        var pinA = CertificatePinningValidator.CalculateSpkiPin(certA);

        // Act: validate cert B against pin A → different SPKI → must reject
        var act = () => CertificatePinningValidator.Validate(pinA, certB);

        // Assert
        act.Should().Throw<CertificatePinValidationException>()
            .WithMessage("*mismatch*");
    }

    [Fact]
    public void Validate_NullCertificate_Throws()
    {
        // Arrange
        var pin = "anypin123";

        // Act
        var act = () => CertificatePinningValidator.Validate(pin, null!);

        // Assert
        act.Should().Throw<CertificatePinValidationException>()
            .WithMessage("*No certificate*");
    }

    [Fact]
    public void Validate_RsaCertAgainstEcdsaPin_Throws()
    {
        // Arrange: create an RSA cert and an ECDSA cert (different algorithms, different keys)
        using var rsaCert = CreateRsaCertificate("supabase.co");
        using var ecdsaCert = CreateEcdsaCertificate("supabase.co");
        var rsaPin = CertificatePinningValidator.CalculateSpkiPin(rsaCert);

        // Act: validate ECDSA cert against RSA pin → different SPKI → must reject
        var act = () => CertificatePinningValidator.Validate(rsaPin, ecdsaCert);

        // Assert
        act.Should().Throw<CertificatePinValidationException>()
            .WithMessage("*mismatch*");
    }

    [Fact]
    public void CalculateSpkiPin_TwoCertsWithSameKeyPair_ProduceSamePin()
    {
        // Arrange: create two certificates from the same RSA key pair
        using var rsa = RSA.Create(2048);
        var dn1 = new X500DistinguishedName("CN=supabase.co");
        var dn2 = new X500DistinguishedName("CN=supabase.co");

        var request1 = new CertificateRequest(dn1, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var notBefore = DateTimeOffset.UtcNow;
        var notAfter = notBefore.AddDays(1);
        using var cert1 = request1.CreateSelfSigned(notBefore, notAfter);

        // Same RSA key, just different validity dates
        var request2 = new CertificateRequest(dn2, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert2 = request2.CreateSelfSigned(notBefore, notAfter.AddDays(1));

        // Act
        var pin1 = CertificatePinningValidator.CalculateSpkiPin(cert1);
        var pin2 = CertificatePinningValidator.CalculateSpkiPin(cert2);

        // Assert: same key → same SPKI → same pin
        pin1.Should().Be(pin2);
    }

    [Fact]
    public void CalculateSpkiPin_DifferentKeys_ProduceDifferentPins()
    {
        // Arrange
        using var certA = CreateRsaCertificate("supabase.co");
        using var certB = CreateRsaCertificate("supabase.co");

        // Act
        var pinA = CertificatePinningValidator.CalculateSpkiPin(certA);
        var pinB = CertificatePinningValidator.CalculateSpkiPin(certB);

        // Assert: different keys (different key pairs) → different SPKI → different pins
        pinA.Should().NotBe(pinB);
    }

    [Fact]
    public void CalculateSpkiPin_CertificateHasNoPrivateKey_StillComputesPin()
    {
        // Arrange: create cert, then export without private key
        using var certWithKey = CreateRsaCertificate("supabase.co");
        var derBytes = certWithKey.Export(X509ContentType.Cert);
        using var certWithoutKey = new X509Certificate2(derBytes);

        // Act
        var pin = CertificatePinningValidator.CalculateSpkiPin(certWithoutKey);

        // Assert: public key exists regardless of private key presence
        pin.Should().NotBeNullOrEmpty();
        pin.Should().Be(CertificatePinningValidator.CalculateSpkiPin(certWithKey));
    }

    [Fact]
    public void Validate_CertB_AgainstPinA_ThrowsWithCorrectPinInMessage()
    {
        // Arrange
        using var certA = CreateRsaCertificate("supabase.co");
        using var certB = CreateRsaCertificate("attacker.com");
        var pinA = CertificatePinningValidator.CalculateSpkiPin(certA);

        // Act
        var act = () => CertificatePinningValidator.Validate(pinA, certB);

        // Assert: error message contains both expected and computed pins
        act.Should().Throw<CertificatePinValidationException>()
            .WithMessage($"*{pinA}*");
    }

    [Fact]
    public void CalculateSpkiPin_ECDsa_Succeeds()
    {
        // Arrange
        using var ecdsaCert = CreateEcdsaCertificate("supabase.co");

        // Act
        var pin = CertificatePinningValidator.CalculateSpkiPin(ecdsaCert);

        // Assert
        pin.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_ECDsaCertAgainstOwnPin_Succeeds()
    {
        // Arrange
        using var ecdsaCert = CreateEcdsaCertificate("supabase.co");
        var pin = CertificatePinningValidator.CalculateSpkiPin(ecdsaCert);

        // Act
        var result = CertificatePinningValidator.Validate(pin, ecdsaCert);

        // Assert
        result.Should().BeTrue();
    }

    public void Dispose()
    {
        // Certificates are disposed in each test via `using`
    }
}
