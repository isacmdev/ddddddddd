// <copyright file="BackendIdentityState.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Credential-free phases controlled by the Service identity authority.
/// </summary>
public enum BackendIdentityPhase
{
    Unpaired,
    PrePairSession,
    PairingPending,
    DefinitiveSession,
}

/// <summary>
/// Immutable identity authority state. Tokens and persistence details never enter Domain.
/// </summary>
public sealed record BackendIdentityState
{
    private BackendIdentityState(BackendIdentityPhase phase, long generation, string? deviceId)
    {
        if (generation < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        if (phase == BackendIdentityPhase.DefinitiveSession && string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException("A definitive session requires a backend device identity.", nameof(deviceId));
        }

        if (phase != BackendIdentityPhase.DefinitiveSession && deviceId is not null)
        {
            throw new ArgumentException("Only a definitive session may expose a device identity.", nameof(deviceId));
        }

        this.Phase = phase;
        this.Generation = generation;
        this.DeviceId = deviceId;
    }

    public BackendIdentityPhase Phase { get; }

    public long Generation { get; }

    public string? DeviceId { get; }

    public bool CanAuthorizeRemoteAccess => this.Phase == BackendIdentityPhase.DefinitiveSession;

    public static BackendIdentityState Unpaired() => new(BackendIdentityPhase.Unpaired, 0, null);

    public static BackendIdentityState Restore(BackendIdentityPhase phase, long generation, string? deviceId = null)
    {
        if (generation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(generation));
        }

        return new(phase, generation, deviceId);
    }

    public BackendIdentityState TransitionTo(BackendIdentityPhase next)
    {
        var valid = (this.Phase, next) switch
        {
            (BackendIdentityPhase.Unpaired, BackendIdentityPhase.PrePairSession) => true,
            (BackendIdentityPhase.PrePairSession, BackendIdentityPhase.PairingPending) => true,
            _ => false,
        };

        return valid
            ? new(next, checked(this.Generation + 1), null)
            : throw new InvalidOperationException($"Invalid backend identity transition: {this.Phase} -> {next}.");
    }

    public BackendIdentityState TransitionToDefinitive(string deviceId)
    {
        if (this.Phase != BackendIdentityPhase.PairingPending)
        {
            throw new InvalidOperationException($"Invalid backend identity transition: {this.Phase} -> {BackendIdentityPhase.DefinitiveSession}.");
        }

        return new(BackendIdentityPhase.DefinitiveSession, checked(this.Generation + 1), deviceId);
    }

    public BackendIdentityState Invalidate()
        => new(BackendIdentityPhase.Unpaired, checked(this.Generation + 1), null);
}
