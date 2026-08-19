// <copyright file="BackendIdentityCoordinatorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using Xunit;

public sealed class BackendIdentityCoordinatorTests
{
    private static readonly DateTimeOffset ExpiresAt = new(2026, 8, 11, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PairAsync_ExactDefinitiveClaim_CommitsThenActivates()
    {
        var port = LifecyclePort.Accepting("device-a");
        var store = new MemoryIdentityStore();
        var sut = new BackendIdentityCoordinator(port, store);

        var result = await sut.PairAsync(Command("op-happy"));

        Assert.Equal(BackendIdentityErrorV1.None, result.Error);
        Assert.Equal(BackendIdentityPhase.DefinitiveSession, sut.CurrentState.Phase);
        Assert.Equal("device-a", sut.CurrentState.DeviceId);
        Assert.Equal("device-a", store.Snapshot?.DeviceId);
        Assert.True(store.WriteCompletedBeforeStateRead);
        Assert.Equal(1, port.PairCalls);
        Assert.Equal(1, port.RefreshCalls);
    }

    [Fact]
    public async Task PairAsync_LostAcceptedResponse_ReconcilesWithoutReplayingCode()
    {
        var port = LifecyclePort.Accepting("device-a");
        port.PairResult = BackendPairingStepV1.Failed(BackendIdentityErrorV1.Timeout, uncertain: true);
        port.ReconcileResult = BackendPairingStepV1.Accepted("device-a", "parent-a", 3);
        var sut = new BackendIdentityCoordinator(port, new MemoryIdentityStore());

        var result = await sut.PairAsync(Command("op-lost"));

        Assert.Equal(BackendIdentityErrorV1.None, result.Error);
        Assert.Equal(1, port.PairCalls);
        Assert.Equal(1, port.ReconcileCalls);
    }

    [Theory]
    [InlineData(BackendIdentityErrorV1.NotFound)]
    [InlineData(BackendIdentityErrorV1.Gone)]
    [InlineData(BackendIdentityErrorV1.Duplicate)]
    [InlineData(BackendIdentityErrorV1.Replay)]
    public async Task PairAsync_TerminalPairingFailure_DoesNotRefreshOrPersist(BackendIdentityErrorV1 error)
    {
        var port = LifecyclePort.Accepting("device-a");
        port.PairResult = BackendPairingStepV1.Failed(error);
        var store = new MemoryIdentityStore();
        var sut = new BackendIdentityCoordinator(port, store);

        var result = await sut.PairAsync(Command("op-terminal"));

        Assert.Equal(error, result.Error);
        Assert.False(sut.CurrentState.CanAuthorizeRemoteAccess);
        Assert.Null(store.Snapshot);
        Assert.Equal(0, port.RefreshCalls);
    }

    [Fact]
    public async Task PairAsync_RateLimited_PreservesRetryAfterWithoutRetry()
    {
        var port = LifecyclePort.Accepting("device-a");
        port.PairResult = BackendPairingStepV1.Failed(BackendIdentityErrorV1.RateLimited, TimeSpan.FromSeconds(29));
        var sut = new BackendIdentityCoordinator(port, new MemoryIdentityStore());

        var result = await sut.PairAsync(Command("op-throttled"));

        Assert.Equal(BackendIdentityErrorV1.RateLimited, result.Error);
        Assert.Equal(TimeSpan.FromSeconds(29), result.RetryAfter);
        Assert.Equal(1, port.PairCalls);
        Assert.Equal(0, port.ReconcileCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("device-b")]
    public async Task PairAsync_MissingOrMismatchedClaim_FailsClosed(string? claimDeviceId)
    {
        var port = LifecyclePort.Accepting("device-a");
        port.RefreshResult = BackendSessionStepV1.Definitive(
            "access-definitive",
            "refresh-definitive",
            ExpiresAt,
            claimDeviceId is null ? null : new BackendIdentityClaimV1(claimDeviceId));
        var store = new MemoryIdentityStore();
        var sut = new BackendIdentityCoordinator(port, store);

        var result = await sut.PairAsync(Command("op-mismatch"));

        Assert.Equal(BackendIdentityErrorV1.ClaimMismatch, result.Error);
        Assert.Equal(BackendIdentityPhase.Unpaired, sut.CurrentState.Phase);
        Assert.Null(store.Snapshot);
    }

    [Fact]
    public async Task PairAsync_RevokedDefinitiveRefresh_FailsClosed()
    {
        var port = LifecyclePort.Accepting("device-a");
        port.RefreshResult = BackendSessionStepV1.Failed(BackendIdentityErrorV1.Revoked);
        var sut = new BackendIdentityCoordinator(port, new MemoryIdentityStore());

        var result = await sut.PairAsync(Command("op-revoked"));

        Assert.Equal(BackendIdentityErrorV1.Revoked, result.Error);
        Assert.False(sut.CurrentState.CanAuthorizeRemoteAccess);
    }

    [Theory]
    [InlineData("pre-pair")]
    [InlineData("pairing")]
    [InlineData("definitive")]
    public async Task PairAsync_MalformedSuccessfulStep_FailsClosed(string step)
    {
        var port = LifecyclePort.Accepting("device-a");
        if (step == "pre-pair")
        {
            port.PrePairResult = BackendSessionStepV1.PrePair(string.Empty, "refresh", ExpiresAt);
        }
        else if (step == "pairing")
        {
            port.PairResult = new(BackendIdentityErrorV1.None, null, "parent-a", 1, null, false);
        }
        else
        {
            port.RefreshResult = BackendSessionStepV1.Definitive("access", string.Empty, ExpiresAt, new BackendIdentityClaimV1("device-a"));
        }

        var sut = new BackendIdentityCoordinator(port, new MemoryIdentityStore());

        var result = await sut.PairAsync(Command($"op-malformed-{step}"));

        Assert.Equal(BackendIdentityErrorV1.InvalidPayload, result.Error);
        Assert.False(sut.CurrentState.CanAuthorizeRemoteAccess);
    }

    [Fact]
    public async Task PairAsync_StaleGenerationCommit_IsRejected()
    {
        var store = new MemoryIdentityStore { WriteStatus = IdentityStoreStatus.Failed };
        var sut = new BackendIdentityCoordinator(LifecyclePort.Accepting("device-a"), store);

        var result = await sut.PairAsync(Command("op-stale"));

        Assert.Equal(BackendIdentityErrorV1.Duplicate, result.Error);
        Assert.Equal(BackendIdentityPhase.Unpaired, sut.CurrentState.Phase);
        Assert.False(sut.CurrentState.CanAuthorizeRemoteAccess);
    }

    [Fact]
    public async Task InitializeAsync_RestoresOnlyAtomicDefinitiveSnapshotAcrossRestart()
    {
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(12, "device-a", "parent-a", "access", "refresh", ExpiresAt),
        };
        var sut = new BackendIdentityCoordinator(LifecyclePort.Accepting("device-a"), store);

        var result = await sut.InitializeAsync();

        Assert.Equal(BackendIdentityErrorV1.None, result);
        Assert.Equal(12, sut.CurrentState.Generation);
        Assert.Equal("device-a", sut.CurrentState.DeviceId);
    }

    [Fact]
    public async Task GetDefinitiveSessionAsync_ConcurrentExpiry_RefreshesOnceForAllWaiters()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(12, "device-a", "parent-a", "old", "refresh", now.AddMinutes(1)),
        };
        var port = LifecyclePort.Accepting("device-a");
        var release = new TaskCompletionSource<BackendSessionStepV1>(TaskCreationOptions.RunContinuationsAsynchronously);
        port.Refresh = (_, _) => release.Task;
        var sut = new BackendIdentityCoordinator(port, store, new FixedTimeProvider(now));
        await sut.InitializeAsync();

        var first = sut.GetDefinitiveSessionAsync();
        var second = sut.GetDefinitiveSessionAsync();
        release.SetResult(BackendSessionStepV1.Definitive("new", "rotated", now.AddHours(1), new BackendIdentityClaimV1("device-a")));
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal("new", result.Session?.AccessToken));
        Assert.All(results, result => Assert.Equal(13, result.Session?.Generation));
        Assert.Equal(1, port.RefreshCalls);
        Assert.Equal(13, store.Snapshot?.Generation);
    }

    [Fact]
    public async Task GetDefinitiveSessionAsync_FreshBeyondSkew_DoesNotRefresh()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(12, "device-a", "parent-a", "current", "refresh", now.AddMinutes(3)),
        };
        var port = LifecyclePort.Accepting("device-a");
        var sut = new BackendIdentityCoordinator(port, store, new FixedTimeProvider(now));
        await sut.InitializeAsync();

        var result = await sut.GetDefinitiveSessionAsync();

        Assert.Equal("current", result.Session?.AccessToken);
        Assert.Equal(0, port.RefreshCalls);
    }

    [Fact]
    public async Task GetDefinitiveSessionAsync_StaleRefreshCompletion_CannotRestoreInvalidatedGeneration()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(12, "device-a", "parent-a", "old", "refresh", now),
        };
        var port = LifecyclePort.Accepting("device-a");
        var release = new TaskCompletionSource<BackendSessionStepV1>(TaskCreationOptions.RunContinuationsAsynchronously);
        port.Refresh = (_, _) => release.Task;
        var sut = new BackendIdentityCoordinator(port, store, new FixedTimeProvider(now));
        await sut.InitializeAsync();

        var refresh = sut.GetDefinitiveSessionAsync();
        await sut.InvalidateAsync(12, BackendIdentityErrorV1.Revoked);
        release.SetResult(BackendSessionStepV1.Definitive("stale", "stale-refresh", now.AddHours(1), new BackendIdentityClaimV1("device-a")));
        var result = await refresh;

        Assert.Equal(BackendIdentityErrorV1.Revoked, result.Error);
        Assert.False(sut.CurrentState.CanAuthorizeRemoteAccess);
        Assert.Null(store.Snapshot);
    }

    [Fact]
    public async Task GetDefinitiveSessionAsync_CancelledWaiter_DoesNotCancelSharedRefresh()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(12, "device-a", "parent-a", "old", "refresh", now),
        };
        var port = LifecyclePort.Accepting("device-a");
        var release = new TaskCompletionSource<BackendSessionStepV1>(TaskCreationOptions.RunContinuationsAsynchronously);
        port.Refresh = (_, _) => release.Task;
        var sut = new BackendIdentityCoordinator(port, store, new FixedTimeProvider(now));
        await sut.InitializeAsync();
        using var cancellation = new CancellationTokenSource();

        var cancelled = sut.GetDefinitiveSessionAsync(cancellation.Token);
        var survivor = sut.GetDefinitiveSessionAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        release.SetResult(BackendSessionStepV1.Definitive("new", "rotated", now.AddHours(1), new BackendIdentityClaimV1("device-a")));

        Assert.Equal("new", (await survivor).Session?.AccessToken);
        Assert.Equal(1, port.RefreshCalls);
    }

    [Fact]
    public async Task GetDefinitiveSessionAsync_RefreshTimeout_InvalidatesAndDoesNotOverlap()
    {
        var now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(12, "device-a", "parent-a", "old", "refresh", now),
        };
        var port = LifecyclePort.Accepting("device-a");
        port.Refresh = async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return port.RefreshResult;
        };
        var sut = new BackendIdentityCoordinator(
            port,
            store,
            new FixedTimeProvider(now),
            TimeSpan.FromMilliseconds(20));
        await sut.InitializeAsync();

        var result = await sut.GetDefinitiveSessionAsync();

        Assert.Equal(BackendIdentityErrorV1.Timeout, result.Error);
        Assert.Equal(1, port.RefreshCalls);
        Assert.False(sut.CurrentState.CanAuthorizeRemoteAccess);
        Assert.Null(store.Snapshot);
    }

    [Fact]
    public async Task PairAsync_Cancelled_DoesNotReconcileOrActivate()
    {
        var port = LifecyclePort.Accepting("device-a");
        var enteredPairing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        port.Pair = async (_, cancellationToken) =>
        {
            enteredPairing.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return BackendPairingStepV1.Failed(BackendIdentityErrorV1.Cancelled);
        };
        var sut = new BackendIdentityCoordinator(port, new MemoryIdentityStore());
        using var cancellation = new CancellationTokenSource();

        var pairing = sut.PairAsync(Command("op-cancel"), cancellation.Token);
        await enteredPairing.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pairing);
        Assert.Equal(0, port.ReconcileCalls);
        Assert.False(sut.CurrentState.CanAuthorizeRemoteAccess);
    }

    [Fact]
    public async Task PairAsync_ConcurrentDuplicate_IsSingleFlightAndSingleUse()
    {
        var port = LifecyclePort.Accepting("device-a");
        var release = new TaskCompletionSource<BackendPairingStepV1>(TaskCreationOptions.RunContinuationsAsynchronously);
        port.Pair = (_, _) => release.Task;
        var sut = new BackendIdentityCoordinator(port, new MemoryIdentityStore());

        var first = sut.PairAsync(Command("op-concurrent"));
        var second = sut.PairAsync(Command("op-concurrent"));
        release.SetResult(BackendPairingStepV1.Accepted("device-a", "parent-a", 1));
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal(BackendIdentityErrorV1.None, result.Error));
        Assert.Equal(1, port.PairCalls);
        Assert.Equal(1, port.RefreshCalls);
    }

    private static PairingCommand Command(string operationId)
        => new("ABC123", AgeBand.Child, operationId, "child-device", "windows", "1.0");

    private sealed class MemoryIdentityStore : IBackendIdentityCredentialStore
    {
        public BackendIdentityCredentialSnapshot? Snapshot { get; set; }

        public IdentityStoreStatus WriteStatus { get; set; } = IdentityStoreStatus.Found;

        public bool WriteCompletedBeforeStateRead { get; private set; }

        public Task<IdentityStoreResult> ReadIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(this.Snapshot is null
                ? new IdentityStoreResult(IdentityStoreStatus.NotFound, null, null)
                : new IdentityStoreResult(IdentityStoreStatus.Found, this.Snapshot, null));

        public Task<IdentityStoreResult> WriteIdentityAsync(BackendIdentityCredentialSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            if (this.WriteStatus == IdentityStoreStatus.Found)
            {
                this.Snapshot = snapshot;
                this.WriteCompletedBeforeStateRead = true;
            }

            return Task.FromResult(new IdentityStoreResult(this.WriteStatus, this.Snapshot, this.WriteStatus == IdentityStoreStatus.Found ? null : "StaleGeneration"));
        }

        public Task<bool> InvalidateIdentityAsync(long generation, CancellationToken cancellationToken = default)
        {
            var matched = this.Snapshot?.Generation == generation;
            this.Snapshot = matched ? null : this.Snapshot;
            return Task.FromResult(matched);
        }
    }

    private sealed class LifecyclePort : IBackendIdentityLifecyclePortV1
    {
        public BackendSessionStepV1 PrePairResult { get; set; } = BackendSessionStepV1.PrePair("pre-access", "pre-refresh", ExpiresAt);

        public BackendPairingStepV1 PairResult { get; set; } = BackendPairingStepV1.Accepted("device-a", "parent-a", 1);

        public BackendPairingStepV1 ReconcileResult { get; set; } = BackendPairingStepV1.Failed(BackendIdentityErrorV1.NotFound);

        public BackendSessionStepV1 RefreshResult { get; set; } = BackendSessionStepV1.Definitive("access", "refresh", ExpiresAt, new BackendIdentityClaimV1("device-a"));

        public Func<PairingCommand, CancellationToken, Task<BackendPairingStepV1>>? Pair { get; set; }

        public Func<string, CancellationToken, Task<BackendSessionStepV1>>? Refresh { get; set; }

        public int PairCalls { get; private set; }

        public int ReconcileCalls { get; private set; }

        public int RefreshCalls { get; private set; }

        public static LifecyclePort Accepting(string deviceId)
            => new()
            {
                PairResult = BackendPairingStepV1.Accepted(deviceId, "parent-a", 1),
                RefreshResult = BackendSessionStepV1.Definitive("access", "refresh", ExpiresAt, new BackendIdentityClaimV1(deviceId)),
            };

        public Task<BackendSessionStepV1> RecoverOrCreatePrePairSessionAsync(CancellationToken cancellationToken)
            => Task.FromResult(this.PrePairResult);

        public Task<BackendPairingStepV1> PairOnceAsync(PairingCommand command, CancellationToken cancellationToken)
        {
            this.PairCalls++;
            return this.Pair?.Invoke(command, cancellationToken) ?? Task.FromResult(this.PairResult);
        }

        public Task<BackendPairingStepV1> ReconcilePairingAsync(string operationId, CancellationToken cancellationToken)
        {
            this.ReconcileCalls++;
            return Task.FromResult(this.ReconcileResult);
        }

        public Task<BackendSessionStepV1> RefreshDefinitiveSessionAsync(string refreshToken, CancellationToken cancellationToken)
        {
            this.RefreshCalls++;
            return this.Refresh?.Invoke(refreshToken, cancellationToken) ?? Task.FromResult(this.RefreshResult);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : System.TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
