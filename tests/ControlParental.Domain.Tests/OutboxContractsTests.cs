namespace ControlParental.Domain.Tests;

using ControlParental.Domain;
using Xunit;

/// <summary>
/// Autonomous contracts-only coverage for SDD6 child 1.1A.
/// This file intentionally does not construct SQLite, a DbContext, or OutboxManager.
/// </summary>
public sealed class OutboxContractsTests
{
    [Fact]
    public void OutboxEntry_PreservesStableOperationIdentityAndBoundedDiagnostics()
    {
        var entry = new OutboxEntry
        {
            Id = 7,
            TableName = "events",
            PayloadJson = "{}",
            DedupKey = "operation-7",
            OperationId = "operation-7",
            Status = OutboxEntryStatus.Pending,
            AttemptCount = 0,
            SafeFailureCode = "redacted-failure",
        };

        Assert.Equal(entry.DedupKey, entry.OperationId);
        Assert.Equal(OutboxEntryStatus.Pending, entry.Status);
        Assert.Equal("redacted-failure", entry.SafeFailureCode);
    }

    [Fact]
    public void OutboxClaim_RequiresIdentityGenerationAndLease()
    {
        var until = DateTimeOffset.UtcNow.AddMinutes(1);
        var claim = new OutboxClaim(7, "operation-7", 3, until);

        Assert.Equal(7, claim.Id);
        Assert.Equal("operation-7", claim.OperationId);
        Assert.Equal(3, claim.ClaimVersion);
        Assert.True(claim.LeaseUntil > DateTimeOffset.UtcNow);
    }

}
