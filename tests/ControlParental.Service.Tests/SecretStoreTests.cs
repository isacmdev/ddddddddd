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
        this.sut = new SecretStore(this.testPath, accessPolicy: new RecordingAccessPolicy());
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
        var instance1 = new SecretStore(this.testPath, accessPolicy: new RecordingAccessPolicy());
        var secretName = "local-machine-cross-instance";
        var secretValue = "machine-bound-secret-value";
        await instance1.WriteAsync(secretName, secretValue);

        // Act — decrypt with second instance (same path, different object)
        var instance2 = new SecretStore(this.testPath, accessPolicy: new RecordingAccessPolicy());
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

    [Fact]
    public async Task IdentityEnvelope_RestartRestoresOneGenerationWithoutPlaintext()
    {
        var snapshot = CreateSnapshot(9, "device-a");
        var write = await this.sut.WriteIdentityAsync(snapshot);

        using var restarted = new SecretStore(this.testPath, accessPolicy: new RecordingAccessPolicy());
        var read = await restarted.ReadIdentityAsync();
        var raw = await File.ReadAllBytesAsync(Path.Combine(this.testPath, "backend-identity-v1.secret"));

        Assert.True(write.Success);
        Assert.Equal(IdentityStoreStatus.Found, read.Status);
        Assert.Equal(snapshot, read.Snapshot);
        Assert.DoesNotContain(snapshot.AccessToken, Encoding.UTF8.GetString(raw));
    }

    [Fact]
    public async Task IdentityEnvelope_AclFailurePreservesPreviousValidGeneration()
    {
        var previous = CreateSnapshot(4, "device-a");
        Assert.True((await this.sut.WriteIdentityAsync(previous)).Success);
        using var failing = new SecretStore(this.testPath, accessPolicy: new ThrowingAccessPolicy());

        var failed = await failing.WriteIdentityAsync(CreateSnapshot(5, "device-b"));
        var persisted = await this.sut.ReadIdentityAsync();

        Assert.False(failed.Success);
        Assert.Equal(previous, persisted.Snapshot);
    }

    [Fact]
    public async Task IdentityEnvelope_ProtectionFailureDoesNotCreatePartialState()
    {
        using var failing = new SecretStore(
            this.testPath,
            protector: new ThrowingProtector(),
            accessPolicy: new RecordingAccessPolicy());

        var result = await failing.WriteIdentityAsync(CreateSnapshot(1, "device-a"));

        Assert.False(result.Success);
        Assert.False(File.Exists(Path.Combine(this.testPath, "backend-identity-v1.secret")));
    }

    [Fact]
    public async Task IdentityEnvelope_OversizedCredentialSetIsRejectedWithoutPersistence()
    {
        var oversized = CreateSnapshot(1, "device-a") with { AccessToken = new string('x', 70 * 1024) };

        var result = await this.sut.WriteIdentityAsync(oversized);

        Assert.False(result.Success);
        Assert.Equal("EnvelopeTooLarge", result.Error);
        Assert.False(File.Exists(Path.Combine(this.testPath, "backend-identity-v1.secret")));
    }

    [Fact]
    public async Task IdentityEnvelope_CorruptionIsQuarantinedAndDenied()
    {
        Assert.True((await this.sut.WriteIdentityAsync(CreateSnapshot(2, "device-a"))).Success);
        var path = Path.Combine(this.testPath, "backend-identity-v1.secret");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);

        var result = await this.sut.ReadIdentityAsync();

        Assert.Equal(IdentityStoreStatus.Corrupt, result.Status);
        Assert.Null(result.Snapshot);
        Assert.False(File.Exists(path));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(this.testPath, "quarantine")));
    }

    [Fact]
    public async Task MigrateIdentityAsync_CompatibleLegacySetCommitsThenDeletesLegacyKeys()
    {
        await WriteLegacySetAsync(this.sut, "device-a", complete: true);

        var result = await this.sut.MigrateIdentityAsync();
        using var restarted = new SecretStore(this.testPath, accessPolicy: new RecordingAccessPolicy());
        var persisted = await restarted.ReadIdentityAsync();

        Assert.Equal(IdentityStoreStatus.Migrated, result.Status);
        Assert.Equal("device-a", persisted.Snapshot!.DeviceId);
        Assert.Equal(1, persisted.Snapshot.Generation);
        Assert.False(await this.sut.ExistsAsync("device_id"));
        Assert.False(await this.sut.ExistsAsync("supabase-session-access_token"));
    }

    [Fact]
    public async Task MigrateIdentityAsync_PartialOrConflictingLegacySetIsQuarantined()
    {
        await WriteLegacySetAsync(this.sut, "device-a", complete: false);

        var result = await this.sut.MigrateIdentityAsync();

        Assert.Equal(IdentityStoreStatus.Corrupt, result.Status);
        Assert.Null(result.Snapshot);
        Assert.False(await this.sut.ExistsAsync("device_id"));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(this.testPath, "quarantine")));
    }

    [Fact]
    public async Task IdentityEnvelope_ConcurrentWritesNeverOverlapAndLeaveOneCompleteSnapshot()
    {
        var policy = new OverlapDetectingAccessPolicy();
        using var store = new SecretStore(this.testPath, accessPolicy: policy);

        var results = await Task.WhenAll(
            Enumerable.Range(1, 12).Reverse().Select(i => store.WriteIdentityAsync(CreateSnapshot(i, $"device-{i}"))));
        var read = await store.ReadIdentityAsync();

        Assert.False(policy.OverlapDetected);
        Assert.InRange(policy.ApplyCount, 1, 12);
        Assert.Contains(results, result => !result.Success && result.Error == "StaleGeneration");
        Assert.Equal(IdentityStoreStatus.Found, read.Status);
        Assert.Equal(12, read.Snapshot!.Generation);
        Assert.Equal($"device-{read.Snapshot.Generation}", read.Snapshot.DeviceId);
    }

    [Fact]
    public async Task IdentityEnvelope_StaleGenerationCannotOverwriteNewerSnapshot()
    {
        var current = CreateSnapshot(8, "device-current");
        Assert.True((await this.sut.WriteIdentityAsync(current)).Success);

        var stale = await this.sut.WriteIdentityAsync(CreateSnapshot(7, "device-stale"));
        var persisted = await this.sut.ReadIdentityAsync();

        Assert.False(stale.Success);
        Assert.Equal(current, persisted.Snapshot);
    }

    [Fact]
    public async Task IdentityEnvelope_InvalidationQuarantinesOnlyMatchingGeneration()
    {
        Assert.True((await this.sut.WriteIdentityAsync(CreateSnapshot(8, "device-a"))).Success);

        Assert.False(await this.sut.InvalidateIdentityAsync(7));
        Assert.Equal(8, (await this.sut.ReadIdentityAsync()).Snapshot?.Generation);
        Assert.True(await this.sut.InvalidateIdentityAsync(8));
        Assert.True(await this.sut.InvalidateIdentityAsync(8));

        using var restarted = new SecretStore(this.testPath, accessPolicy: new RecordingAccessPolicy());
        Assert.Equal(IdentityStoreStatus.NotFound, (await restarted.ReadIdentityAsync()).Status);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(this.testPath, "quarantine")));
    }

    [Fact]
    public async Task IdentityEnvelope_CancelledWritePreservesPreviousGeneration()
    {
        var previous = CreateSnapshot(3, "device-a");
        Assert.True((await this.sut.WriteIdentityAsync(previous)).Success);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => this.sut.WriteIdentityAsync(CreateSnapshot(4, "device-b"), cancellation.Token));
        var persisted = await this.sut.ReadIdentityAsync();

        Assert.Equal(previous, persisted.Snapshot);
    }

    private static BackendIdentityCredentialSnapshot CreateSnapshot(long generation, string deviceId)
        => new(generation, deviceId, "parent-a", "access-token", "refresh-token", DateTimeOffset.Parse("2030-01-01T00:00:00Z"));

    private static async Task WriteLegacySetAsync(SecretStore store, string deviceId, bool complete)
    {
        await store.WriteAsync("device_id", deviceId);
        await store.WriteAsync("parent_id", "parent-a");
        await store.WriteAsync("supabase-session-access_token", "access-token");
        await store.WriteAsync("supabase-session-refresh_token", "refresh-token");
        await store.WriteAsync("supabase-session-device_id", complete ? deviceId : "conflicting-device");
        if (complete)
        {
            await store.WriteAsync("supabase-session-expires_at", "2030-01-01T00:00:00.0000000+00:00");
        }
    }

    private sealed class RecordingAccessPolicy : ICredentialFileAccessPolicy
    {
        public void Apply(string path)
        {
            Assert.True(File.Exists(path));
        }
    }

    private sealed class ThrowingAccessPolicy : ICredentialFileAccessPolicy
    {
        public void Apply(string path) => throw new UnauthorizedAccessException("denied");
    }

    private sealed class ThrowingProtector : ICredentialProtector
    {
        public byte[] Protect(byte[] plaintext) => throw new InvalidOperationException("protection failed");

        public byte[] Unprotect(byte[] protectedData) => throw new InvalidOperationException("not used");
    }

    private sealed class OverlapDetectingAccessPolicy : ICredentialFileAccessPolicy
    {
        private int active;

        public int ApplyCount { get; private set; }

        public bool OverlapDetected { get; private set; }

        public void Apply(string path)
        {
            if (Interlocked.Increment(ref this.active) != 1)
            {
                this.OverlapDetected = true;
            }

            Thread.Sleep(2);
            this.ApplyCount++;
            Interlocked.Decrement(ref this.active);
        }
    }
}
