namespace ControlParental.Service;

using System.Text.Json;
using ControlParental.Domain;

public sealed class FileIssueStore : IIssueStore
{
    private const int CurrentDocumentVersion = 1;
    private const int MaximumRecords = 1024;
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private Dictionary<IssueKey, DurableIssue>? issues;

    public FileIssueStore(string path)
    {
        this.path = string.IsNullOrWhiteSpace(path)
            ? throw new ArgumentException("A durable issue path is required.", nameof(path))
            : path;
    }

    public async Task<IReadOnlyList<DurableIssue>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            await this.EnsureLoadedAsync(cancellationToken);
            return this.issues!.Values.OrderBy(issue => issue.Key.SessionId)
                .ThenBy(issue => issue.Key.Type)
                .ThenBy(issue => issue.Key.Cause, StringComparer.Ordinal)
                .ToArray();
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task<DurableIssue> UpsertActiveAsync(
        IssueKey key,
        EnforcementIssueSeverity severity,
        string evidence,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            await this.EnsureLoadedAsync(cancellationToken);
            this.issues!.TryGetValue(key, out var existing);
            if (existing == null && this.issues.Count >= MaximumRecords)
            {
                this.RemoveOldestResolvedRecord();
            }

            if (existing == null && this.issues.Count >= MaximumRecords)
            {
                throw new InvalidOperationException($"Durable issue state reached its {MaximumRecords}-record bound.");
            }

            var updated = new DurableIssue(
                key,
                severity,
                evidence,
                existing?.FirstObservedAt ?? observedAt,
                observedAt,
                checked((existing?.OccurrenceCount ?? 0) + 1),
                IsActive: true,
                ResolvedAt: null,
                ResolutionEvidence: null,
                Revision: checked((existing?.Revision ?? 0) + 1));
            this.issues[key] = updated;
            await this.SaveAsync(cancellationToken);
            return updated;
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task ResolveAsync(
        IssueKey key,
        string recoveryEvidence,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        await this.gate.WaitAsync(cancellationToken);
        try
        {
            await this.EnsureLoadedAsync(cancellationToken);
            if (!this.issues!.TryGetValue(key, out var existing) || !existing.IsActive)
            {
                return;
            }

            this.issues[key] = existing with
            {
                IsActive = false,
                ResolvedAt = resolvedAt,
                ResolutionEvidence = recoveryEvidence,
                Revision = checked(existing.Revision + 1),
            };
            await this.SaveAsync(cancellationToken);
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (this.issues != null)
        {
            return;
        }

        if (!File.Exists(this.path))
        {
            this.issues = [];
            return;
        }

        try
        {
            await using var stream = new FileStream(
                this.path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            var document = await JsonSerializer.DeserializeAsync<IssueDocument>(stream, cancellationToken: cancellationToken);
            if (document == null || document.Version != CurrentDocumentVersion || document.Issues == null)
            {
                throw new InvalidDataException("The durable issue document has an unsupported or incomplete format.");
            }

            this.issues = document.Issues.ToDictionary(issue => issue.Key);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The durable issue document is corrupt.", exception);
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(this.path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = $"{this.path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    new IssueDocument(CurrentDocumentVersion, this.issues!.Values.ToArray()),
                    cancellationToken: cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, this.path, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private void RemoveOldestResolvedRecord()
    {
        var oldest = this.issues!.Values.Where(issue => !issue.IsActive)
            .OrderBy(issue => issue.ResolvedAt)
            .FirstOrDefault();
        if (oldest != null)
        {
            this.issues.Remove(oldest.Key);
        }
    }

    private static void ValidateKey(IssueKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (string.IsNullOrWhiteSpace(key.Cause))
        {
            throw new ArgumentException("A semantic issue cause is required.", nameof(key));
        }
    }

    private sealed record IssueDocument(int Version, DurableIssue[] Issues);
}
