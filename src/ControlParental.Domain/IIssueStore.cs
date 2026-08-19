namespace ControlParental.Domain;

public interface IIssueStore
{
    Task<IReadOnlyList<DurableIssue>> LoadAsync(CancellationToken cancellationToken = default);

    Task<DurableIssue> UpsertActiveAsync(
        IssueKey key,
        EnforcementIssueSeverity severity,
        string evidence,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default);

    Task ResolveAsync(
        IssueKey key,
        string recoveryEvidence,
        DateTimeOffset resolvedAt,
        CancellationToken cancellationToken = default);
}

public sealed record DurableIssue(
    IssueKey Key,
    EnforcementIssueSeverity Severity,
    string LastEvidence,
    DateTimeOffset FirstObservedAt,
    DateTimeOffset LastObservedAt,
    int OccurrenceCount,
    bool IsActive,
    DateTimeOffset? ResolvedAt,
    string? ResolutionEvidence,
    long Revision);
