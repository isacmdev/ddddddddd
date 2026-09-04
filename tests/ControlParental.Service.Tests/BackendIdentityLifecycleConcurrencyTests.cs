namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using Xunit;

public sealed class BackendIdentityLifecycleConcurrencyTests
{
    [Fact]
    public async Task CloseRefreshAdmissionAndDrainAsync_ClosesAdmissionAndReportsNoInflightOperations()
    {
        var coordinator = new BackendIdentityCoordinator(
            new LifecyclePort(),
            new MemoryIdentityStore());

        await coordinator.InitializeAsync();
        await coordinator.CloseRefreshAdmissionAndDrainAsync();

        var snapshot = coordinator.LifecycleSnapshot;
        Assert.Equal(BackendIdentityAdmissionState.Closed, snapshot.Admission);
        Assert.Equal(0, snapshot.InFlightCount);
        Assert.False(snapshot.ShutdownTimedOut);
    }

    [Fact]
    public async Task CloseRefreshAdmissionAndDrainAsync_CancelsAnAdmittedRefreshWithItsOwnerToken()
    {
        var now = new DateTimeOffset(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(4, "device-a", "parent-a", "old", "refresh", now),
        };
        var port = new LifecyclePort();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        port.Refresh = async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return BackendSessionStepV1.Failed(BackendIdentityErrorV1.Cancelled);
        };
        var coordinator = new BackendIdentityCoordinator(port, store, new FixedTimeProvider(now));
        await coordinator.InitializeAsync();

        var refresh = coordinator.GetDefinitiveSessionAsync();
        await entered.Task;
        await coordinator.CloseRefreshAdmissionAndDrainAsync();

        var result = await refresh;
        Assert.Equal(BackendIdentityErrorV1.Cancelled, result.Error);
        Assert.Equal(4, store.Snapshot?.Generation);
        Assert.Equal(BackendIdentityAdmissionState.Closed, coordinator.LifecycleSnapshot.Admission);
    }

    [Fact]
    public async Task PairCompletionAfterClose_DoesNotWriteOrPublishIdentity()
    {
        var definitiveRelease = new TaskCompletionSource<BackendSessionStepV1>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new MemoryIdentityStore();
        var port = new LifecyclePort
        {
            PrePair = (_, _) => Task.FromResult(BackendSessionStepV1.PrePair("pre-access", "pre-refresh", DateTimeOffset.UtcNow.AddHours(1))),
            Pair = (_, _) => Task.FromResult(BackendPairingStepV1.Accepted("device-a", "parent-a", 1)),
            Refresh = (_, _) =>
            {
                refreshEntered.SetResult();
                return definitiveRelease.Task;
            },
        };
        var coordinator = new BackendIdentityCoordinator(port, store);
        var pairing = coordinator.PairAsync(new PairingCommand("ABC123", AgeBand.Child, "late-pair", "device", "windows", "1"));

        await refreshEntered.Task;
        var stop = coordinator.CloseRefreshAdmissionAndDrainAsync();
        definitiveRelease.SetResult(BackendSessionStepV1.Definitive(
            "access", "refresh", DateTimeOffset.UtcNow.AddHours(1), new BackendIdentityClaimV1("device-a")));
        await stop;
        var result = await pairing;

        Assert.Equal(BackendIdentityErrorV1.Cancelled, result.Error);
        Assert.Null(store.Snapshot);
        Assert.False(coordinator.CurrentState.CanAuthorizeRemoteAccess);
        Assert.Equal(BackendIdentityAdmissionState.Closed, coordinator.LifecycleSnapshot.Admission);
    }

    [Fact]
    public async Task CloseRefreshAdmissionAndDrainAsync_IsIdempotentAndPublishesClosingOnce()
    {
        var coordinator = new BackendIdentityCoordinator(new LifecyclePort(), new MemoryIdentityStore());
        var closing = 0;
        coordinator.LifecycleChanged += (_, args) =>
        {
            if (args.Kind == BackendIdentityLifecycleEventKind.Closing)
            {
                Interlocked.Increment(ref closing);
            }
        };

        await Task.WhenAll(
            coordinator.CloseRefreshAdmissionAndDrainAsync(),
            coordinator.CloseRefreshAdmissionAndDrainAsync());

        Assert.Equal(1, closing);
        Assert.Equal(BackendIdentityAdmissionState.Closed, coordinator.LifecycleSnapshot.Admission);
        await coordinator.CloseRefreshAdmissionAndDrainAsync();
        Assert.Equal(1, closing);
    }

    [Fact]
    public async Task OperationAdmittedAfterClose_IsCancelledWithoutStartingBody()
    {
        var coordinator = new BackendIdentityCoordinator(new LifecyclePort(), new MemoryIdentityStore());
        await coordinator.CloseRefreshAdmissionAndDrainAsync();
        var started = false;

        var operation = coordinator.RunOwnedOperationAsync(
            BackendIdentityOperationKind.HostedStart,
            -1,
            _ =>
            {
                started = true;
                return Task.CompletedTask;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.False(started);
        Assert.Equal(0, coordinator.LifecycleSnapshot.InFlightCount);
    }

    [Fact]
    public async Task ClosePreservesDurableSnapshotWhenOwnerCancelsOperations()
    {
        var now = DateTimeOffset.UtcNow.AddHours(1);
        var stored = new BackendIdentityCredentialSnapshot(4, "device-a", "parent-a", "access", "refresh", now);
        var store = new MemoryIdentityStore { Snapshot = stored };
        var coordinator = new BackendIdentityCoordinator(new LifecyclePort(), store);

        Assert.Equal(BackendIdentityErrorV1.None, await coordinator.InitializeAsync());
        await coordinator.CloseRefreshAdmissionAndDrainAsync();

        Assert.Equal(stored, store.Snapshot);
        Assert.Equal(BackendIdentityPhase.DefinitiveSession, coordinator.CurrentState.Phase);
    }

    [Fact]
    public async Task InvalidateAfterClose_DoesNotChangeDurableSnapshot()
    {
        var stored = new BackendIdentityCredentialSnapshot(4, "device-a", "parent-a", "access", "refresh", DateTimeOffset.UtcNow.AddHours(1));
        var store = new MemoryIdentityStore { Snapshot = stored };
        var coordinator = new BackendIdentityCoordinator(new LifecyclePort(), store);
        await coordinator.InitializeAsync();
        await coordinator.CloseRefreshAdmissionAndDrainAsync();

        await coordinator.InvalidateAsync(4, BackendIdentityErrorV1.Revoked);

        Assert.Equal(stored, store.Snapshot);
        Assert.Equal(BackendIdentityPhase.DefinitiveSession, coordinator.CurrentState.Phase);
    }

    [Fact]
    public async Task OwnerCancellationOfPair_DoesNotInvalidateDurableIdentity()
    {
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(4, "device-a", "parent-a", "access", "refresh", DateTimeOffset.UtcNow.AddHours(1)),
        };
        var coordinator = new BackendIdentityCoordinator(new LifecyclePort(), store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.PairAsync(
            new PairingCommand("ABC123", AgeBand.Child, "cancelled", "device", "windows", "1"), cancellation.Token));

        Assert.Equal(4, store.Snapshot?.Generation);
    }

    [Fact]
    public async Task CloseReportsZeroInflightAfterAdmittedOperationCompletes()
    {
        var coordinator = new BackendIdentityCoordinator(new LifecyclePort(), new MemoryIdentityStore());
        var operation = coordinator.RunOwnedOperationAsync(
            BackendIdentityOperationKind.HostedStart,
            -1,
            async token => await Task.Delay(Timeout.InfiniteTimeSpan, token));

        await coordinator.CloseRefreshAdmissionAndDrainAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);

        Assert.Equal(BackendIdentityAdmissionState.Closed, coordinator.LifecycleSnapshot.Admission);
        Assert.Equal(0, coordinator.LifecycleSnapshot.InFlightCount);
    }

    [Fact]
    public async Task CallerCancellationWhileWaitingForPairingGate_ReleasesRegistration()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var port = new LifecyclePort
        {
            PrePair = (_, _) => Task.FromResult(BackendSessionStepV1.PrePair("a", "r", DateTimeOffset.UtcNow.AddHours(1))),
            Pair = async (_, token) =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(token);
                return BackendPairingStepV1.Failed(BackendIdentityErrorV1.NotFound);
            },
        };
        var coordinator = new BackendIdentityCoordinator(port, new MemoryIdentityStore());
        var command = new PairingCommand("ABC123", AgeBand.Child, "op-1", "device", "windows", "1");

        var first = coordinator.PairAsync(command);
        await firstEntered.Task;
        using var caller = new CancellationTokenSource();
        var second = coordinator.PairAsync(command with { OperationId = "op-2" }, caller.Token);
        caller.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);

        releaseFirst.SetResult();
        await first;
        Assert.Equal(0, coordinator.LifecycleSnapshot.InFlightCount);

        await coordinator.CloseRefreshAdmissionAndDrainAsync();
        Assert.Equal(BackendIdentityAdmissionState.Closed, coordinator.LifecycleSnapshot.Admission);
        Assert.Equal(0, coordinator.LifecycleSnapshot.InFlightCount);
    }

    [Fact]
    public async Task ShutdownTimeout_IsFailClosedAndRetryClosesAfterSurvivorDrains()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new BackendIdentityCoordinator(
            new LifecyclePort(),
            new MemoryIdentityStore(),
            shutdownTimeout: TimeSpan.FromMilliseconds(25));
        var owned = coordinator.RunOwnedOperationAsync(
            BackendIdentityOperationKind.HostedRefreshLoop,
            -1,
            async _ => await release.Task.ConfigureAwait(false));

        await Assert.ThrowsAsync<TimeoutException>(() => coordinator.CloseRefreshAdmissionAndDrainAsync());
        Assert.Equal(BackendIdentityAdmissionState.Closing, coordinator.LifecycleSnapshot.Admission);
        Assert.True(coordinator.LifecycleSnapshot.ShutdownTimedOut);
        release.SetResult();
        await owned;
        await coordinator.CloseRefreshAdmissionAndDrainAsync();
        Assert.Equal(BackendIdentityAdmissionState.Closed, coordinator.LifecycleSnapshot.Admission);
        Assert.Equal(0, coordinator.LifecycleSnapshot.InFlightCount);
    }

    [Fact]
    public async Task AuthoritativeFailureWhileOpen_InvalidatesCurrentGeneration()
    {
        var now = DateTimeOffset.UtcNow.AddHours(1);
        var store = new MemoryIdentityStore
        {
            Snapshot = new BackendIdentityCredentialSnapshot(4, "device-a", "parent-a", "access", "refresh", now),
        };
        var port = new LifecyclePort
        {
            Refresh = (_, _) => Task.FromResult(BackendSessionStepV1.Failed(BackendIdentityErrorV1.Forbidden)),
        };
        var coordinator = new BackendIdentityCoordinator(port, store, new FixedTimeProvider(now));
        await coordinator.InitializeAsync();

        var result = await coordinator.GetDefinitiveSessionAsync();

        Assert.Equal(BackendIdentityErrorV1.Forbidden, result.Error);
        Assert.Null(store.Snapshot);
        Assert.False(coordinator.CurrentState.CanAuthorizeRemoteAccess);
        await coordinator.CloseRefreshAdmissionAndDrainAsync();
    }

    private sealed class LifecyclePort : IBackendIdentityLifecyclePortV1
    {
        public Func<string, CancellationToken, Task<BackendSessionStepV1>>? Refresh { get; set; }
        public Func<string, CancellationToken, Task<BackendSessionStepV1>>? PrePair { get; set; }
        public Func<PairingCommand, CancellationToken, Task<BackendPairingStepV1>>? Pair { get; set; }

        public Task<BackendSessionStepV1> RecoverOrCreatePrePairSessionAsync(CancellationToken cancellationToken)
            => this.PrePair?.Invoke(string.Empty, cancellationToken)
                ?? Task.FromResult(BackendSessionStepV1.Failed(BackendIdentityErrorV1.NotFound));

        public Task<BackendPairingStepV1> PairOnceAsync(PairingCommand command, CancellationToken cancellationToken)
            => this.Pair?.Invoke(command, cancellationToken)
                ?? Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.NotFound));

        public Task<BackendPairingStepV1> ReconcilePairingAsync(string operationId, CancellationToken cancellationToken)
            => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.NotFound));

        public Task<BackendSessionStepV1> RefreshDefinitiveSessionAsync(string refreshToken, CancellationToken cancellationToken)
            => this.Refresh?.Invoke(refreshToken, cancellationToken)
                ?? Task.FromResult(BackendSessionStepV1.Failed(BackendIdentityErrorV1.NotFound));
    }

    private sealed class MemoryIdentityStore : IBackendIdentityCredentialStore
    {
        public BackendIdentityCredentialSnapshot? Snapshot { get; set; }

        public Task<IdentityStoreResult> ReadIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(this.Snapshot is null
                ? new IdentityStoreResult(IdentityStoreStatus.NotFound, null, null)
                : new IdentityStoreResult(IdentityStoreStatus.Found, this.Snapshot, null));

        public Task<IdentityStoreResult> WriteIdentityAsync(BackendIdentityCredentialSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            this.Snapshot = snapshot;
            return Task.FromResult(new IdentityStoreResult(IdentityStoreStatus.Found, snapshot, null));
        }

        public Task<bool> InvalidateIdentityAsync(long generation, CancellationToken cancellationToken = default)
        {
            if (this.Snapshot?.Generation == generation)
            {
                this.Snapshot = null;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : System.TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
}
