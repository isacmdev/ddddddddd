namespace ControlParental.Service.Tests;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Xunit;

public sealed class FileIntegrityEscalationStateStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cp-escalation-{Guid.NewGuid():N}");

    [Fact]
    public async Task MissingIdentity_ReturnsNull_WithoutCreatingDirectory()
    {
        var result = await Store().LoadAsync("device-a");
        result.Should().BeNull();
        Directory.Exists(this.directory).Should().BeFalse();
    }
    [Fact]
    public async Task SaveLoad_RoundTripsAllFields_ReplacesLatest_AndUsesHashedFilename()
    {
        var latest = Pending("device-a", 2);
        await Store().SaveAsync(Pending("device-a", 1));
        await Store().SaveAsync(latest);
        (await Store().LoadAsync("device-a")).Should().Be(latest.State);
        Path.GetFileName(Directory.GetFiles(this.directory).Should().ContainSingle().Subject)
            .Should().Be(Hash("device-a") + ".json");
    }
    [Fact]
    public async Task Identities_AreIsolated()
    {
        await Store().SaveAsync(Pending("device-a", 1));
        await Store().SaveAsync(Pending("device-b", 2));
        (await Store().LoadAsync("device-a"))!.PolicyVersion.Should().Be(1);
        (await Store().LoadAsync("device-b"))!.PolicyVersion.Should().Be(2);
    }
    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task MalformedOrNullDocument_IsCorrupt(string json)
    {
        await WriteRawAsync("device-a", json);
        (await LoadErrorAsync()).Error.Should().Be(IntegrityEscalationStateError.CorruptDocument);
    }
    [Fact]
    public async Task UnsupportedAndInvalidDocuments_RetainTypedErrors()
    {
        await WriteRawAsync("device-a", "{\"documentVersion\":2,\"schemaVersion\":1,\"state\":null}");
        (await LoadErrorAsync()).Error.Should().Be(IntegrityEscalationStateError.UnsupportedDocumentVersion);
        await WriteRawAsync("device-a", "{\"documentVersion\":1,\"schemaVersion\":2,\"state\":null}");
        (await LoadErrorAsync()).Error.Should().Be(IntegrityEscalationStateError.UnsupportedSchemaVersion);
        var invalid = Pending("device-a", 1) with { State = Pending("device-a", 1).State with { Phase = (EscalationPhase)99 } };
        await WriteRawAsync("device-a", JsonSerializer.Serialize(invalid, SharedJsonContext.Default.IntegrityEscalationStateEnvelope));
        (await LoadErrorAsync()).Error.Should().Be(IntegrityEscalationStateError.InvalidState);
    }

    [Fact]
    public async Task CopiedDocumentUnderAnotherHash_IsWrongIdentity()
    {
        await WriteRawAsync("device-a", JsonSerializer.Serialize(Pending("device-a", 1), SharedJsonContext.Default.IntegrityEscalationStateEnvelope));
        File.Copy(Path.Combine(this.directory, Hash("device-a") + ".json"), Path.Combine(this.directory, Hash("device-b") + ".json"));
        (await LoadErrorAsync("device-b")).Error.Should().Be(IntegrityEscalationStateError.WrongIdentity);
    }

    [Fact]
    public async Task ExistingStatePathDirectory_IsStoreUnavailable()
    {
        Directory.CreateDirectory(Path.Combine(this.directory, Hash("device-a") + ".json"));
        (await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => Store().LoadAsync("device-a")))
            .Error.Should().Be(IntegrityEscalationStateError.StoreUnavailable);
    }

    [Fact]
    public async Task SaveDirectoryThatIsAFile_IsStoreUnavailableWithoutPublication()
    {
        var path = Path.Combine(this.directory, "not-a-directory");
        Directory.CreateDirectory(this.directory);
        await File.WriteAllTextAsync(path, "sentinel");
        var error = await Assert.ThrowsAsync<IntegrityEscalationStateException>(
            () => new FileIntegrityEscalationStateStore(path).SaveAsync(Pending("device-a", 1)));
        error.Error.Should().Be(IntegrityEscalationStateError.StoreUnavailable);
        File.ReadAllText(path).Should().Be("sentinel");
        Directory.Exists(path).Should().BeFalse();
    }

    [Fact]
    public async Task PreCanceledSave_PreservesOldDocument_AndCreatesNoTemp()
    {
        var store = Store();
        await store.SaveAsync(Pending("device-a", 1));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(Pending("device-a", 2), canceled.Token));
        await AssertPreservedAsync(store);
    }

    [Fact]
    public async Task PartialCancellation_PreservesOldDocument_AndCleansTemp()
    {
        var store = Store();
        await store.SaveAsync(Pending("device-a", 1));
        using var canceled = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seam = new FileIntegrityEscalationStateStore(this.directory, (stream, bytes, token) => WritePartThenWaitAsync(stream, bytes, token, entered, canceled));
        var save = seam.SaveAsync(Pending("device-a", 2), canceled.Token);
        await entered.Task;
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);
        await AssertPreservedAsync(store);
    }

    [Fact]
    public async Task PartialIOException_PreservesOldDocument_AndCleansTemp()
    {
        var store = Store();
        await store.SaveAsync(Pending("device-a", 1));
        var seam = new FileIntegrityEscalationStateStore(this.directory, async (stream, bytes, token) =>
        {
            await stream.WriteAsync(bytes[..(bytes.Length / 2)], token);
            throw new IOException("partial write");
        });
        (await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => seam.SaveAsync(Pending("device-a", 2))))
            .Error.Should().Be(IntegrityEscalationStateError.StoreUnavailable);
        await AssertPreservedAsync(store);
    }

    [Fact]
    public async Task InterleavedSavesAndLoads_ExposeOnlyCompleteDocuments()
    {
        var store = Store();
        await store.SaveAsync(Pending("device-a", 1));
        var operations = Enumerable.Range(2, 20).Select(async version =>
        {
            await store.SaveAsync(Pending("device-a", version));
            return await store.LoadAsync("device-a");
        });
        var results = await Task.WhenAll(operations);
        results.Should().NotContainNulls();
        results.Select(value => value!.PolicyVersion).Should().OnlyContain(version => version >= 1 && version <= 21);
    }

    private FileIntegrityEscalationStateStore Store() => new(this.directory);
    private async Task<IntegrityEscalationStateException> LoadErrorAsync(string identity = "device-a")
        => await Assert.ThrowsAsync<IntegrityEscalationStateException>(() => Store().LoadAsync(identity));
    private async Task WriteRawAsync(string identity, string json)
    {
        Directory.CreateDirectory(this.directory);
        await File.WriteAllTextAsync(Path.Combine(this.directory, Hash(identity) + ".json"), json);
    }
    private async Task AssertPreservedAsync(FileIntegrityEscalationStateStore store)
    {
        (await store.LoadAsync("device-a"))!.PolicyVersion.Should().Be(1);
        Directory.GetFiles(this.directory, "*.tmp").Should().BeEmpty();
    }
    private static async Task WritePartThenWaitAsync(Stream stream, ReadOnlyMemory<byte> bytes, CancellationToken token, TaskCompletionSource entered, CancellationTokenSource canceled)
    {
        await stream.WriteAsync(bytes[..(bytes.Length / 2)], token);
        entered.SetResult();
        await Task.Delay(Timeout.InfiniteTimeSpan, canceled.Token);
    }
    private static IntegrityEscalationStateEnvelope Pending(string identity, int policyVersion)
    {
        var origin = new DateTimeOffset(2026, 8, 25, 18, 0, 0, TimeSpan.Zero);
        var due = origin.Add(IntegrityEscalationState.EscalationDeadlineDelay);
        var state = new IntegrityEscalationState(identity, policyVersion, 1, 4, 7, 3, 0, EscalationPhase.Pending, origin, due, due, true, false, false, "reaction", "reaction", "notification", "notification");
        return new(1, 1, state);
    }
    private static string Hash(string identity) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    public void Dispose()
    {
        if (Directory.Exists(this.directory))
            Directory.Delete(this.directory, recursive: true);
    }
}
