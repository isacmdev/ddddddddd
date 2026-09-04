// <copyright file="CertificatePinningPolicyTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControlParental.Service;
using Xunit;

public sealed class CertificatePinningPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ConfigureHandler_EnablesPlatformRevocationAndSecureProtocols()
    {
        using var handler = new CertificatePinningPolicy().ConfigureHandler();

        Assert.True(handler.CheckCertificateRevocationList);
        Assert.NotNull(handler.ServerCertificateCustomValidationCallback);
        Assert.Equal(SslProtocols.Tls12 | SslProtocols.Tls13, handler.SslProtocols);
    }

    [Theory]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch)]
    [InlineData(SslPolicyErrors.RemoteCertificateChainErrors)]
    [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateChainErrors)]
    public void Validate_PlatformHostOrChainFailure_IsRejected(SslPolicyErrors errors)
    {
        using var certificate = CreateCertificate(Now.AddHours(-1), Now.AddHours(1));
        var pin = CertificatePinningValidator.CalculateSpkiPin(certificate);

        Assert.False(Validate(new CertificatePinningPolicy(pin, () => Now), certificate, errors));
    }

    [Theory]
    [InlineData(-2, -1)]
    [InlineData(1, 2)]
    public void Validate_ExpiredOrNotYetValidCertificate_IsRejected(int notBeforeHours, int notAfterHours)
    {
        using var certificate = CreateCertificate(
            Now.AddHours(notBeforeHours),
            Now.AddHours(notAfterHours));
        var pin = CertificatePinningValidator.CalculateSpkiPin(certificate);

        Assert.False(Validate(new CertificatePinningPolicy(pin, () => Now), certificate));
    }

    [Fact]
    public void Validate_PinMismatch_IsRejectedWithoutBypassingPlatformTrust()
    {
        using var trusted = CreateCertificate(Now.AddHours(-1), Now.AddHours(1));
        using var attacker = CreateCertificate(Now.AddHours(-1), Now.AddHours(1));
        var trustedPin = CertificatePinningValidator.CalculateSpkiPin(trusted);

        Assert.False(Validate(new CertificatePinningPolicy(trustedPin, () => Now), attacker));
    }

    [Fact]
    public void Validate_OverlappingPins_AcceptCurrentAndNextDuringRotation()
    {
        using var current = CreateCertificate(Now.AddHours(-1), Now.AddHours(1));
        using var next = CreateCertificate(Now.AddHours(-1), Now.AddHours(2));
        var pins = string.Join(
            ';',
            CertificatePinningValidator.CalculateSpkiPin(current),
            CertificatePinningValidator.CalculateSpkiPin(next));
        var policy = new CertificatePinningPolicy(pins, () => Now);

        Assert.True(Validate(policy, current));
        Assert.True(Validate(policy, next));
    }

    [Fact]
    public void Validate_NoPinConfiguration_RequiresPlatformTrustAndCertificate()
    {
        using var certificate = CreateCertificate(Now.AddHours(-1), Now.AddHours(1));
        var policy = new CertificatePinningPolicy(null, () => Now);

        Assert.True(Validate(policy, certificate));
        Assert.False(Validate(policy, certificate, SslPolicyErrors.RemoteCertificateChainErrors));
        Assert.False(Validate(policy, null));
    }

    [Fact]
    public void Constructor_MalformedPinConfiguration_FailsClosed()
    {
        Assert.Throws<ArgumentException>(() => new CertificatePinningPolicy("not-a-sha256-pin"));
    }

    [Fact]
    public void Constructor_WrongHashLengthOrMoreThanRotationPair_FailsClosed()
    {
        var shortPin = Convert.ToBase64String(new byte[16]);
        var validPin = Convert.ToBase64String(new byte[32]);

        Assert.Throws<ArgumentException>(() => new CertificatePinningPolicy(shortPin));
        Assert.Throws<ArgumentException>(
            () => new CertificatePinningPolicy(string.Join(';', validPin, validPin, validPin)));
    }

    [Fact]
    public void Constructor_AcceptsCanonicalSha256Pins()
    {
        var pin = "sha256/" + Convert.ToBase64String(new byte[32]);

        var policy = new CertificatePinningPolicy(pin, () => Now);

        Assert.NotNull(policy);
    }

    [Fact]
    public void Validate_NonHttpsRequest_IsRejected()
    {
        using var certificate = CreateCertificate(Now.AddHours(-1), Now.AddHours(1));
        using var handler = new CertificatePinningPolicy(null, () => Now).ConfigureHandler();
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://backend.example.test/");

        Assert.False(
            handler.ServerCertificateCustomValidationCallback!(
                request,
                certificate,
                null,
                SslPolicyErrors.None));
    }

    private static bool Validate(
        CertificatePinningPolicy policy,
        X509Certificate2? certificate,
        SslPolicyErrors errors = SslPolicyErrors.None)
    {
        using var handler = policy.ConfigureHandler();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://backend.example.test/");
        return handler.ServerCertificateCustomValidationCallback!(request, certificate, null, errors);
    }

    private static X509Certificate2 CreateCertificate(DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=backend.example.test",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        return request.CreateSelfSigned(notBefore, notAfter);
    }
}
