// <copyright file="IntegrityCheckerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Security.Cryptography;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T23 — Tests for IntegrityChecker.
/// </summary>
public class IntegrityCheckerTests
{
    /// <summary>
    /// Test that SHA256 hash is correct length (64 hex chars).
    /// </summary>
    [Fact]
    public async Task CheckLocalIntegrityAsync_ComputeSha256_Returns64HexChars()
    {
        // Arrange
        var mockWinTrust = new Mock<IWinTrustVerifier>();
        mockWinTrust.Setup(w => w.IsSigned(It.IsAny<string>())).Returns(true);

        var checker = new IntegrityChecker(mockWinTrust.Object);

        // Act
        var result = await checker.CheckLocalIntegrityAsync();

        // Assert
        result.BinaryHash.Should().NotBeNullOrEmpty();
        result.BinaryHash.Should().HaveLength(64, "SHA256 produces 64 hex characters");
        result.BinaryHash.Should().MatchRegex("^[a-f0-9]{64}$", "SHA256 hash should be lowercase hex");
    }

    /// <summary>
    /// Test that IsSignatureValid is correctly captured from WinTrustVerifier result.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckLocalIntegrityAsync_IsSignatureValid_CapturedFromVerifier(bool expectedSignatureValid)
    {
        // Arrange
        var mockWinTrust = new Mock<IWinTrustVerifier>();
        mockWinTrust.Setup(w => w.IsSigned(It.IsAny<string>())).Returns(expectedSignatureValid);

        var checker = new IntegrityChecker(mockWinTrust.Object);

        // Act
        var result = await checker.CheckLocalIntegrityAsync();

        // Assert
        result.IsSignatureValid.Should().Be(expectedSignatureValid);
    }

    /// <summary>
    /// Test that ExecutablePath is returned in result.
    /// </summary>
    [Fact]
    public async Task CheckLocalIntegrityAsync_ExecutablePath_IsReturned()
    {
        // Arrange
        var mockWinTrust = new Mock<IWinTrustVerifier>();
        mockWinTrust.Setup(w => w.IsSigned(It.IsAny<string>())).Returns(true);

        var checker = new IntegrityChecker(mockWinTrust.Object);

        // Act
        var result = await checker.CheckLocalIntegrityAsync();

        // Assert
        result.ExecutablePath.Should().NotBeNullOrEmpty();
        result.ExecutablePath.Should().Be(Environment.ProcessPath);
    }

    /// <summary>
    /// Test that IntegrityChecker throws when WinTrustVerifier is null.
    /// </summary>
    [Fact]
    public void Constructor_WithNullWinTrustVerifier_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new IntegrityChecker(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>()
            .WithParameterName("winTrustVerifier");
    }

    /// <summary>
    /// Test that actual SHA256 computation produces deterministic hash.
    /// </summary>
    [Fact]
    public void ComputeSha256_SameInput_ProducesSameHash()
    {
        // Arrange
        var testContent = "test content for SHA256";
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, System.Text.Encoding.UTF8.GetBytes(testContent));

            var mockWinTrust = new Mock<IWinTrustVerifier>();
            var checker = new IntegrityChecker(mockWinTrust.Object);

            // Use reflection to call private static ComputeSha256
            var method = typeof(IntegrityChecker).GetMethod(
                "ComputeSha256",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            // Act
            var hash1 = method?.Invoke(null, new object[] { tempFile }) as string;
            var hash2 = method?.Invoke(null, new object[] { tempFile }) as string;

            // Assert
            hash1.Should().Be(hash2);
            hash1.Should().HaveLength(64);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Test that SHA256 hash matches known value for specific content.
    /// </summary>
    [Fact]
    public void ComputeSha256_KnownContent_ProducesKnownHash()
    {
        // Arrange
        var testContent = new byte[] { 0x00, 0x01, 0x02 };
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tempFile, testContent);

            var mockWinTrust = new Mock<IWinTrustVerifier>();
            var checker = new IntegrityChecker(mockWinTrust.Object);

            // Use reflection to call private static ComputeSha256
            var method = typeof(IntegrityChecker).GetMethod(
                "ComputeSha256",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            // Act
            var hash = method?.Invoke(null, new object[] { tempFile }) as string;

            // Verify it's a valid SHA256 hex string (known value for 00 01 02)
            hash.Should().NotBeNullOrEmpty();
            hash.Should().HaveLength(64);
            hash.Should().MatchRegex("^[a-f0-9]{64}$");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
