// <copyright file="BackendRealtimeIdentityAuthorityTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Service;
using ControlParental.Domain;
using FluentAssertions;
using Xunit;

public sealed class BackendRealtimeIdentityAuthorityTests
{
    [Fact]
    public async Task RefreshAsync_PublishesOnlyTheCurrentDefinitiveSession()
    {
        var coordinator = new FakeCoordinator
        {
            Result = BackendDefinitiveSessionResult.Authorized(
                new BackendDefinitiveSession(4, "device-4", "header.payload.signature", DateTimeOffset.UtcNow.AddMinutes(5))),
        };
        var authority = new BackendRealtimeIdentityAuthority(coordinator);

        (await authority.RefreshAsync()).Should().Be(BackendIdentityErrorV1.None);
        authority.Current.Should().NotBeNull();
        authority.Current!.DeviceId.Should().Be("device-4");
        authority.Current.Generation.Should().Be(4);
        authority.Current.AccessToken.Should().Be("header.payload.signature");
    }

    [Fact]
    public async Task RefreshAsync_ClearsLeaseWhenCoordinatorRejectsTheSession()
    {
        var coordinator = new FakeCoordinator
        {
            Result = BackendDefinitiveSessionResult.Authorized(
                new BackendDefinitiveSession(4, "device-4", "header.payload.signature", DateTimeOffset.UtcNow.AddMinutes(5))),
        };
        var authority = new BackendRealtimeIdentityAuthority(coordinator);
        await authority.RefreshAsync();
        coordinator.Result = BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Revoked);

        (await authority.RefreshAsync()).Should().Be(BackendIdentityErrorV1.Revoked);
        authority.Current.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_RejectsMalformedDefinitiveSession()
    {
        var coordinator = new FakeCoordinator
        {
            Result = BackendDefinitiveSessionResult.Authorized(
                new BackendDefinitiveSession(0, string.Empty, string.Empty, DateTimeOffset.UtcNow.AddMinutes(5))),
        };
        var authority = new BackendRealtimeIdentityAuthority(coordinator);

        (await authority.RefreshAsync()).Should().Be(BackendIdentityErrorV1.InvalidPayload);
        authority.Current.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_DoesNotPublishAnOlderConcurrentResult()
    {
        var firstStarted = NewSignal();
        var releaseFirst = NewSignal();
        var call = 0;
        var coordinator = new FakeCoordinator
        {
            Handler = async _ =>
            {
                if (Interlocked.Increment(ref call) == 1)
                {
                    firstStarted.SetResult(true);
                    await releaseFirst.Task;
                    return BackendDefinitiveSessionResult.Authorized(
                        new BackendDefinitiveSession(4, "device-4", "older.payload.signature", DateTimeOffset.UtcNow.AddMinutes(5)));
                }

                return BackendDefinitiveSessionResult.Authorized(
                    new BackendDefinitiveSession(5, "device-4", "newer.payload.signature", DateTimeOffset.UtcNow.AddMinutes(5)));
            },
        };
        var authority = new BackendRealtimeIdentityAuthority(coordinator);

        var older = authority.RefreshAsync();
        await firstStarted.Task;
        var newer = authority.RefreshAsync();
        await newer;
        releaseFirst.SetResult(true);
        await older;

        authority.Current.Should().NotBeNull();
        authority.Current!.Generation.Should().Be(5);
        authority.Current.AccessToken.Should().Be("newer.payload.signature");
    }

    private sealed class FakeCoordinator : IBackendIdentityCoordinator
    {
        public BackendDefinitiveSessionResult Result { get; set; } =
            BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Forbidden);

        public Func<CancellationToken, Task<BackendDefinitiveSessionResult>>? Handler { get; set; }

        public BackendIdentityState CurrentState => BackendIdentityState.Unpaired();

        public Task<BackendPairingLifecycleResult> PairAsync(
            PairingCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BackendPairingLifecycleResult(BackendIdentityErrorV1.Forbidden));

        public Task<BackendDefinitiveSessionResult> GetDefinitiveSessionAsync(
            CancellationToken cancellationToken = default)
            => this.Handler?.Invoke(cancellationToken) ?? Task.FromResult(this.Result);
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
