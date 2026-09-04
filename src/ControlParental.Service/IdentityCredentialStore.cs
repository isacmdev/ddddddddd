// <copyright file="IdentityCredentialStore.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

public interface ICredentialProtector
{
    byte[] Protect(byte[] plaintext);

    byte[] Unprotect(byte[] protectedData);
}

public interface ICredentialFileAccessPolicy
{
    void Apply(string path);
}

public sealed record BackendIdentityCredentialSnapshot(
    long Generation,
    string DeviceId,
    string ParentId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

public enum IdentityStoreStatus
{
    Found,
    NotFound,
    Migrated,
    Corrupt,
    Failed,
}

public sealed record IdentityStoreResult(IdentityStoreStatus Status, BackendIdentityCredentialSnapshot? Snapshot, string? Error)
{
    public bool Success => this.Status is IdentityStoreStatus.Found or IdentityStoreStatus.NotFound or IdentityStoreStatus.Migrated;
}

public sealed class DpapiCredentialProtector : ICredentialProtector
{
    private readonly byte[] entropy;

    public DpapiCredentialProtector(byte[] machineEntropy, byte[] applicationEntropy)
    {
        this.entropy = SHA256.HashData([.. applicationEntropy, .. machineEntropy]);
    }

    public byte[] Protect(byte[] plaintext)
        => ProtectedData.Protect(plaintext, this.entropy, DataProtectionScope.LocalMachine);

    public byte[] Unprotect(byte[] protectedData)
        => ProtectedData.Unprotect(protectedData, this.entropy, DataProtectionScope.LocalMachine);
}

public sealed class WindowsServiceCredentialFileAccessPolicy : ICredentialFileAccessPolicy
{
    public void Apply(string path)
    {
        var security = new FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        AddFullControl(security, new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
        AddFullControl(security, new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        new FileInfo(path).SetAccessControl(security);
    }

    private static void AddFullControl(FileSecurity security, SecurityIdentifier sid)
        => security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
}

public sealed partial class SecretStore
{
    private const string IdentitySecretName = "backend-identity-v1";
    private const int MaxIdentityEnvelopeBytes = 64 * 1024;
    private const int MaxProtectedIdentityBytes = 72 * 1024;
    private static readonly string[] LegacyIdentityNames =
    [
        "device_id", "parent_id", "supabase-session-access_token", "supabase-session-refresh_token",
        "supabase-session-device_id", "supabase-session-expires_at",
    ];

    public async Task<IdentityStoreResult> WriteIdentityAsync(
        BackendIdentityCredentialSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (snapshot is null || !IsValid(snapshot))
        {
            return new(IdentityStoreStatus.Failed, null, "Identity snapshot is incomplete.");
        }

        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(new IdentityEnvelope(1, snapshot));
            if (json.Length > MaxIdentityEnvelopeBytes)
            {
                return new(IdentityStoreStatus.Failed, null, "EnvelopeTooLarge");
            }

            var protectedBytes = this.protector.Protect(json);
            var written = await this.WriteIdentityAtomicallyAsync(
                this.GetFilePath(IdentitySecretName),
                protectedBytes,
                snapshot.Generation,
                cancellationToken);
            if (!written)
            {
                return new(IdentityStoreStatus.Failed, null, "StaleGeneration");
            }

            return new(IdentityStoreStatus.Found, snapshot, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new(IdentityStoreStatus.Failed, null, ex.GetType().Name);
        }
    }

    public async Task<IdentityStoreResult> ReadIdentityAsync(CancellationToken cancellationToken = default)
    {
        var path = this.GetFilePath(IdentitySecretName);
        if (!File.Exists(path))
        {
            return new(IdentityStoreStatus.NotFound, null, null);
        }

        try
        {
            if (new FileInfo(path).Length > MaxProtectedIdentityBytes)
            {
                throw new InvalidDataException("Identity envelope exceeds its bound.");
            }

            var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var envelope = JsonSerializer.Deserialize<IdentityEnvelope>(this.protector.Unprotect(protectedBytes));
            if (envelope is null || envelope.Version != 1 || !IsValid(envelope.Snapshot))
            {
                throw new InvalidDataException("Invalid identity envelope.");
            }

            return new(IdentityStoreStatus.Found, envelope.Snapshot, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Quarantine([IdentitySecretName]);
            return new(IdentityStoreStatus.Corrupt, null, ex.GetType().Name);
        }
    }

    public async Task<bool> InvalidateIdentityAsync(long generation, CancellationToken cancellationToken = default)
    {
        await this.writeGate.WaitAsync(cancellationToken);
        try
        {
            var path = this.GetFilePath(IdentitySecretName);
            if (!File.Exists(path))
            {
                return true;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            var envelope = JsonSerializer.Deserialize<IdentityEnvelope>(this.protector.Unprotect(bytes));
            if (envelope is null || envelope.Snapshot.Generation != generation)
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            this.Quarantine([IdentitySecretName]);
            return !File.Exists(path);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        finally
        {
            this.writeGate.Release();
        }
    }

    public async Task<IdentityStoreResult> MigrateIdentityAsync(CancellationToken cancellationToken = default)
    {
        var existing = await this.ReadIdentityAsync(cancellationToken);
        if (existing.Status != IdentityStoreStatus.NotFound)
        {
            return existing;
        }

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        try
        {
            foreach (var name in LegacyIdentityNames)
            {
                var read = await this.ReadAsync(name, cancellationToken);
                if (!read.Success)
                {
                    this.Quarantine(LegacyIdentityNames);
                    return new(IdentityStoreStatus.Corrupt, null, "LegacyReadFailed");
                }

                values[name] = read.Value;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.Quarantine(LegacyIdentityNames);
            return new(IdentityStoreStatus.Corrupt, null, ex.GetType().Name);
        }

        if (values.Values.All(value => value is null))
        {
            return new(IdentityStoreStatus.NotFound, null, null);
        }

        if (values.Values.Any(string.IsNullOrWhiteSpace) ||
            !string.Equals(values["device_id"], values["supabase-session-device_id"], StringComparison.Ordinal) ||
            !DateTimeOffset.TryParse(values["supabase-session-expires_at"], out var expiresAt))
        {
            this.Quarantine(LegacyIdentityNames);
            return new(IdentityStoreStatus.Corrupt, null, "LegacyIdentityMismatch");
        }

        var snapshot = new BackendIdentityCredentialSnapshot(
            1,
            values["device_id"]!,
            values["parent_id"]!,
            values["supabase-session-access_token"]!,
            values["supabase-session-refresh_token"]!,
            expiresAt);
        var write = await this.WriteIdentityAsync(snapshot, cancellationToken);
        if (!write.Success)
        {
            return write;
        }

        foreach (var name in LegacyIdentityNames)
        {
            var deleted = await this.DeleteAsync(name, cancellationToken);
            if (!deleted.Success || await this.ExistsAsync(name, cancellationToken))
            {
                this.Quarantine([IdentitySecretName]);
                return new(IdentityStoreStatus.Failed, null, "LegacyDeleteFailed");
            }
        }

        return new(IdentityStoreStatus.Migrated, snapshot, null);
    }

    private async Task WriteAtomicallyAsync(string destination, byte[] bytes, CancellationToken cancellationToken)
    {
        await this.writeGate.WaitAsync(cancellationToken);
        try
        {
            await this.WriteFileAndReplaceAsync(destination, bytes, cancellationToken);
        }
        finally
        {
            this.writeGate.Release();
        }
    }

    private async Task<bool> WriteIdentityAtomicallyAsync(
        string destination,
        byte[] bytes,
        long generation,
        CancellationToken cancellationToken)
    {
        await this.writeGate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(destination))
            {
                var currentBytes = await File.ReadAllBytesAsync(destination, cancellationToken);
                var current = JsonSerializer.Deserialize<IdentityEnvelope>(this.protector.Unprotect(currentBytes));
                if (current is null || current.Version != 1 || !IsValid(current.Snapshot))
                {
                    throw new InvalidDataException("Current identity envelope is unsafe.");
                }

                if (current.Snapshot.Generation >= generation)
                {
                    return false;
                }
            }

            await this.WriteFileAndReplaceAsync(destination, bytes, cancellationToken);
            return true;
        }
        finally
        {
            this.writeGate.Release();
        }
    }

    private async Task WriteFileAndReplaceAsync(string destination, byte[] bytes, CancellationToken cancellationToken)
    {
        var temporary = $"{destination}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            this.accessPolicy.Apply(temporary);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private void Quarantine(IEnumerable<string> names)
    {
        try
        {
            var directory = Path.Combine(this.basePath, "quarantine");
            Directory.CreateDirectory(directory);
            foreach (var name in names)
            {
                var source = this.GetFilePath(name);
                if (File.Exists(source))
                {
                    File.Move(source, Path.Combine(directory, $"{Path.GetFileName(source)}.{Guid.NewGuid():N}"));
                }
            }
        }
        catch
        {
            // Access remains denied because callers receive Corrupt and no snapshot.
        }
    }

    private static bool IsValid(BackendIdentityCredentialSnapshot snapshot)
        => snapshot.Generation > 0 &&
           !string.IsNullOrWhiteSpace(snapshot.DeviceId) &&
           !string.IsNullOrWhiteSpace(snapshot.ParentId) &&
           !string.IsNullOrWhiteSpace(snapshot.AccessToken) &&
           !string.IsNullOrWhiteSpace(snapshot.RefreshToken) &&
           snapshot.ExpiresAt != default;

    private sealed record IdentityEnvelope(int Version, BackendIdentityCredentialSnapshot Snapshot);
}
