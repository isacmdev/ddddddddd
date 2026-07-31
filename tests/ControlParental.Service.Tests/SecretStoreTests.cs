// <copyright file="SecretStoreTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.IO;
using System.Text;
using ControlParental.Domain;
using Xunit;

public class SecretStoreTests : IDisposable
{
    private readonly string testPath;
    private readonly SecretStore sut;

    public SecretStoreTests()
    {
        // Crear directorio temporal para tests
        this.testPath = Path.Combine(Path.GetTempPath(), $"SecretStoreTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.testPath);
        this.sut = new SecretStore(this.testPath);
    }

    public void Dispose()
    {
        this.sut.Dispose();

        // Limpiar directorio de test
        try
        {
            if (Directory.Exists(this.testPath))
            {
                Directory.Delete(this.testPath, recursive: true);
            }
        }
        catch
        {
            // Ignorar errores de limpieza
        }
    }

    [Fact]
    public async Task WriteAsync_WithValidInput_Succeeds()
    {
        // Arrange
        var name = "test-secret";
        var value = "my-secret-value-12345";

        // Act
        var result = await this.sut.WriteAsync(name, value);

        // Assert
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ReadAsync_WhenSecretExists_ReturnsValue()
    {
        // Arrange
        var name = "read-test";
        var expectedValue = "my-secret-value-xyz";
        await this.sut.WriteAsync(name, expectedValue);

        // Act
        var result = await this.sut.ReadAsync(name);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(expectedValue, result.Value);
    }

    [Fact]
    public async Task ReadAsync_WhenSecretDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var name = "non-existent-secret";

        // Act
        var result = await this.sut.ReadAsync(name);

        // Assert
        Assert.True(result.Success);
        Assert.Null(result.Value);
        Assert.True(result.NotFound);
    }

    [Fact]
    public async Task ReadAsync_WithNullName_ReturnsFailed()
    {
        // Act
        var result = await this.sut.ReadAsync(null!);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ReadAsync_WithEmptyName_ReturnsFailed()
    {
        // Act
        var result = await this.sut.ReadAsync("");

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WriteAsync_WithNullValue_ReturnsFailed()
    {
        // Act
        var result = await this.sut.WriteAsync("test", null!);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task WriteAsync_WithEmptyValue_ReturnsFailed()
    {
        // Act
        var result = await this.sut.WriteAsync("test", string.Empty);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WriteAsync_WithNullName_ReturnsFailed()
    {
        // Act
        var result = await this.sut.WriteAsync(null!, "value");

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_WhenSecretExists_DeletesAndReturnsTrue()
    {
        // Arrange
        var name = "delete-test";
        await this.sut.WriteAsync(name, "some-value");

        // Act
        var result = await this.sut.DeleteAsync(name);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Deleted);

        // Verify deleted
        var exists = await this.sut.ExistsAsync(name);
        Assert.False(exists);
    }

    [Fact]
    public async Task DeleteAsync_WhenSecretDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var name = "non-existent-delete";

        // Act
        var result = await this.sut.DeleteAsync(name);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_WithNullName_ReturnsFailed()
    {
        // Act
        var result = await this.sut.DeleteAsync(null!);

        // Assert
        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExistsAsync_WhenSecretExists_ReturnsTrue()
    {
        // Arrange
        var name = "exists-test";
        await this.sut.WriteAsync(name, "value");

        // Act
        var result = await this.sut.ExistsAsync(name);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_WhenSecretDoesNotExist_ReturnsFalse()
    {
        // Act
        var result = await this.sut.ExistsAsync("non-existent");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsAsync_WithNullName_ReturnsFalse()
    {
        // Act
        var result = await this.sut.ExistsAsync(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task WriteAsync_UpdatesExistingSecret()
    {
        // Arrange
        var name = "update-test";
        await this.sut.WriteAsync(name, "original-value");

        // Act
        var result = await this.sut.WriteAsync(name, "updated-value");

        // Assert
        Assert.True(result.Success);

        // Verify updated value
        var readResult = await this.sut.ReadAsync(name);
        Assert.True(readResult.Success);
        Assert.Equal("updated-value", readResult.Value);
    }

    [Fact]
    public async Task WriteAsync_SecretIsEncryptedOnDisk()
    {
        // Arrange
        var name = "encryption-test";
        var value = "my-secret-data";
        await this.sut.WriteAsync(name, value);

        // Act - Read raw file
        var filePath = Path.Combine(this.testPath, $"{name}.secret");
        var encryptedData = await File.ReadAllBytesAsync(filePath);

        // Assert - Encrypted data should not contain plaintext
        var encryptedString = System.Text.Encoding.UTF8.GetString(encryptedData);
        Assert.DoesNotContain(value, encryptedString);
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(value), encryptedData);
    }

    [Fact]
    public async Task ReadAsync_CanRoundTripLargeSecret()
    {
        // Arrange
        var name = "large-secret-test";
        var largeValue = new string('x', 10000); // 10KB secret

        // Act
        await this.sut.WriteAsync(name, largeValue);
        var result = await this.sut.ReadAsync(name);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(largeValue, result.Value);
    }

    [Fact]
    public async Task ReadAsync_CanRoundTripUnicodeSecret()
    {
        // Arrange
        var name = "unicode-secret-test";
        var unicodeValue = "Secret with ñ, 中文, emoji 🎉 and ümläüts";

        // Act
        await this.sut.WriteAsync(name, unicodeValue);
        var result = await this.sut.ReadAsync(name);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(unicodeValue, result.Value);
    }

    [Fact]
    public async Task ReadAsync_CanRoundTripSpecialCharacters()
    {
        // Arrange
        var name = "special-char-test";
        var specialValue = "Secret with <>&\"' and newlines\n\t\r";

        // Act
        await this.sut.WriteAsync(name, specialValue);
        var result = await this.sut.ReadAsync(name);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(specialValue, result.Value);
    }

    [Fact]
    public async Task WriteAsync_SanitizesInvalidCharacters()
    {
        // Arrange
        var nameWithInvalidChars = "test/secret<name>with*invalid?chars";
        var value = "test-value";

        // Act
        var result = await this.sut.WriteAsync(nameWithInvalidChars, value);

        // Assert
        Assert.True(result.Success);

        // Should be able to read it back
        var readResult = await this.sut.ReadAsync(nameWithInvalidChars);
        Assert.True(readResult.Success);
        Assert.Equal(value, readResult.Value);
    }

    [Fact]
    public async Task ReadAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var name = "cancel-test";
        await this.sut.WriteAsync(name, "value");
        using var cts = new CancellationTokenSource();

        // Note: File.ReadAllBytesAsync in .NET 9 doesn't support CancellationToken
        // but we still pass it for API consistency. This test verifies the API accepts it.

        // Act - We test that the API signature is correct
        var result = await this.sut.ReadAsync(name, cts.Token);

        // Assert - Should succeed
        Assert.True(result.Success);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public async Task WriteAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Note: File.WriteAllBytesAsync in .NET 9 doesn't support CancellationToken
        // but we still pass it for API consistency. This test verifies the API accepts it.

        // Act - We test that the API signature is correct
        var result = await this.sut.WriteAsync("test", "value", cts.Token);

        // Assert - Should succeed
        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        var name = "cancel-delete-test";
        await this.sut.WriteAsync(name, "value");
        using var cts = new CancellationTokenSource();

        // Note: File.Delete doesn't support CancellationToken
        // but we still pass it for API consistency. This test verifies the API accepts it.

        // Act - We test that the API signature is correct
        var result = await this.sut.DeleteAsync(name, cts.Token);

        // Assert - Should succeed
        Assert.True(result.Success);
    }

    [Fact]
    public async Task MultipleSecrets_CanBeStoredIndependently()
    {
        // Arrange & Act
        await this.sut.WriteAsync("secret1", "value1");
        await this.sut.WriteAsync("secret2", "value2");
        await this.sut.WriteAsync("secret3", "value3");

        // Assert
        var result1 = await this.sut.ReadAsync("secret1");
        var result2 = await this.sut.ReadAsync("secret2");
        var result3 = await this.sut.ReadAsync("secret3");

        Assert.Equal("value1", result1.Value);
        Assert.Equal("value2", result2.Value);
        Assert.Equal("value3", result3.Value);
    }

    [Fact]
    public async Task DeleteSecret_OtherSecretsRemain()
    {
        // Arrange
        await this.sut.WriteAsync("secret1", "value1");
        await this.sut.WriteAsync("secret2", "value2");

        // Act
        await this.sut.DeleteAsync("secret1");

        // Assert
        var result1 = await this.sut.ReadAsync("secret1");
        var result2 = await this.sut.ReadAsync("secret2");

        Assert.True(result1.NotFound);
        Assert.Equal("value2", result2.Value);
    }

    [Fact]
    public async Task LocalMachine_DifferentInstanceSamePath_CanDecrypt()
    {
        // T16 requires DPAPI scope=máquina so that any process on the same machine
        // (including LocalSystem) can decrypt. This test validates that two distinct
        // SecretStore instances sharing the same path can read each other's secrets.
        // With CurrentUser scope this would fail if the encrypting process identity
        // differs from the decrypting one. With LocalMachine scope it succeeds because
        // the key is bound to the machine, not the user.

        // Arrange — encrypt with first instance
        var instance1 = new SecretStore(this.testPath);
        var secretName = "local-machine-cross-instance";
        var secretValue = "machine-bound-secret-value";
        await instance1.WriteAsync(secretName, secretValue);

        // Act — decrypt with second instance (same path, different object)
        var instance2 = new SecretStore(this.testPath);
        var result = await instance2.ReadAsync(secretName);

        // Assert
        Assert.True(result.Success, "LocalMachine scope should allow decryption by a different instance on the same machine");
        Assert.Equal(secretValue, result.Value);
    }

    [Fact]
    public async Task LocalMachine_EncryptedBlobHasNoPlaintext()
    {
        // T16 requires that inspecting the raw file on disk does not reveal the secret value.
        // This validates the encryption layer is working correctly with LocalMachine scope.
        var secretName = "dpapi-encrypted-secret";
        var secretValue = "MySecretPassword123!";
        await this.sut.WriteAsync(secretName, secretValue);

        // Read raw bytes from disk
        var filePath = Path.Combine(this.testPath, $"{secretName}.secret");
        var encryptedBytes = await File.ReadAllBytesAsync(filePath);

        // The raw blob must not contain the plaintext value
        var encryptedString = Encoding.UTF8.GetString(encryptedBytes);
        Assert.DoesNotContain(secretValue, encryptedString);

        // The blob must not be the plaintext bytes directly
        Assert.NotEqual(Encoding.UTF8.GetBytes(secretValue), encryptedBytes);

        // The blob must be non-empty (encrypted, not skipped)
        Assert.True(encryptedBytes.Length > 0);
    }
}
