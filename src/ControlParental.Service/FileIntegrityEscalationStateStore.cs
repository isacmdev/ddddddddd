namespace ControlParental.Service;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlParental.Domain;

public sealed class FileIntegrityEscalationStateStore : IIntegrityEscalationStateStore
{
    private readonly string directory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Func<Stream, ReadOnlyMemory<byte>, CancellationToken, Task> writeAsync;

    public FileIntegrityEscalationStateStore(string directory)
        : this(directory, static (stream, bytes, cancellationToken) =>
            stream.WriteAsync(bytes, cancellationToken).AsTask())
    {
    }

    internal FileIntegrityEscalationStateStore(
        string directory,
        Func<Stream, ReadOnlyMemory<byte>, CancellationToken, Task> writeAsync)
    {
        this.directory = string.IsNullOrWhiteSpace(directory)
            ? throw new ArgumentException("A durable escalation directory is required.", nameof(directory))
            : directory;
        this.writeAsync = writeAsync ?? throw new ArgumentNullException(nameof(writeAsync));
    }

    public async Task<IntegrityEscalationState?> LoadAsync(
        string identity,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(identity);
        var path = GetPath(identity);
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                await using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                var envelope = await JsonSerializer.DeserializeAsync(
                    stream, SharedJsonContext.Default.IntegrityEscalationStateEnvelope, cancellationToken);
                if (envelope is null)
                {
                    throw Corrupt("The escalation document is corrupt.");
                }

                envelope.Validate();
                if (!string.Equals(envelope.State.IdentityScope, identity, StringComparison.Ordinal))
                {
                    throw new IntegrityEscalationStateException(
                        IntegrityEscalationStateError.WrongIdentity,
                        "The escalation document identity does not match.");
                }

                return envelope.State;
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (JsonException exception)
            {
                throw Corrupt("The escalation document is corrupt.", exception);
            }
            catch (IntegrityEscalationStateException)
            {
                throw;
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                throw Unavailable("The escalation store is unavailable.", exception);
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task SaveAsync(
        IntegrityEscalationStateEnvelope value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        ValidateIdentity(value.State.IdentityScope);
        var path = GetPath(value.State.IdentityScope);
        var temporaryPath = Path.Combine(this.directory, $".{Guid.NewGuid():N}.tmp");
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            try
            {
                Directory.CreateDirectory(this.directory);
                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    value, SharedJsonContext.Default.IntegrityEscalationStateEnvelope);
                await using (var stream = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await this.writeAsync(stream, bytes, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    await stream.FlushAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporaryPath, path, overwrite: true);
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                throw Unavailable("The escalation store is unavailable.", exception);
            }
        }
        finally
        {
            TryDelete(temporaryPath);
            this.gate.Release();
        }
    }

    private string GetPath(string identity) => Path.Combine(this.directory, Hash(identity) + ".json");

    private static string Hash(string identity) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();

    private static void ValidateIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity) || identity.Length > IntegrityEscalationState.MaximumIdentityLength)
        {
            throw new ArgumentException("A bounded identity is required.", nameof(identity));
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static IntegrityEscalationStateException Corrupt(string message, Exception? inner = null)
        => new(IntegrityEscalationStateError.CorruptDocument, message, inner);

    private static IntegrityEscalationStateException Unavailable(string message, Exception inner)
        => new(IntegrityEscalationStateError.StoreUnavailable, message, inner);
}
