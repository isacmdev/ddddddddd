// <copyright file="BackendIdentityStateTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Xunit;

public sealed class BackendIdentityStateTests
{
    [Fact]
    public void ValidLifecycle_AdvancesGenerationAndExposesIdentityOnlyWhenDefinitive()
    {
        var state = BackendIdentityState.Unpaired();

        state = state.TransitionTo(BackendIdentityPhase.PrePairSession);
        state = state.TransitionTo(BackendIdentityPhase.PairingPending);
        state = state.TransitionToDefinitive("device-a");

        Assert.Equal(BackendIdentityPhase.DefinitiveSession, state.Phase);
        Assert.Equal(3, state.Generation);
        Assert.Equal("device-a", state.DeviceId);
        Assert.True(state.CanAuthorizeRemoteAccess);
    }

    [Theory]
    [InlineData(BackendIdentityPhase.Unpaired, BackendIdentityPhase.PairingPending)]
    [InlineData(BackendIdentityPhase.Unpaired, BackendIdentityPhase.DefinitiveSession)]
    [InlineData(BackendIdentityPhase.PrePairSession, BackendIdentityPhase.DefinitiveSession)]
    [InlineData(BackendIdentityPhase.PairingPending, BackendIdentityPhase.PrePairSession)]
    [InlineData(BackendIdentityPhase.DefinitiveSession, BackendIdentityPhase.PairingPending)]
    public void InvalidTransition_IsRejectedWithoutChangingAuthority(
        BackendIdentityPhase from,
        BackendIdentityPhase to)
    {
        var state = BackendIdentityState.Restore(from, generation: 7, deviceId: from == BackendIdentityPhase.DefinitiveSession ? "device-a" : null);

        var error = Assert.Throws<InvalidOperationException>(() => state.TransitionTo(to));

        Assert.Contains($"{from} -> {to}", error.Message, StringComparison.Ordinal);
        Assert.Equal(7, state.Generation);
    }

    [Fact]
    public void Invalidation_AdvancesGenerationAndRemovesAuthoritativeIdentity()
    {
        var definitive = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 41, "device-a");

        var invalidated = definitive.Invalidate();

        Assert.Equal(BackendIdentityPhase.Unpaired, invalidated.Phase);
        Assert.Equal(42, invalidated.Generation);
        Assert.Null(invalidated.DeviceId);
        Assert.False(invalidated.CanAuthorizeRemoteAccess);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Restore_WithInvalidGeneration_FailsClosed(long generation)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => BackendIdentityState.Restore(BackendIdentityPhase.PrePairSession, generation));
    }

    [Fact]
    public void DefinitiveState_RequiresNonEmptyBackendIdentity()
    {
        Assert.Throws<ArgumentException>(
            () => BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 1, " "));
    }
}
