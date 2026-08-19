// <copyright file="BackendIdentityCoordinator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// Pairing input owned by the Service. The operation identifier makes reconciliation explicit.
/// </summary>
public sealed record PairingCommand(
    string Code,
    AgeBand AgeBand,
    string OperationId,
    string DeviceName,
    string OsVersion,
    string AppVersion);

public sealed record BackendSessionStepV1(
    BackendIdentityErrorV1 Error,
    string? AccessToken,
    string? RefreshToken,
    DateTimeOffset ExpiresAt,
    BackendIdentityClaimV1? Claim)
{
    public bool IsSuccess => this.Error == BackendIdentityErrorV1.None;

    public static BackendSessionStepV1 PrePair(string accessToken, string refreshToken, DateTimeOffset expiresAt)
        => new(BackendIdentityErrorV1.None, accessToken, refreshToken, expiresAt, null);

    public static BackendSessionStepV1 Definitive(
        string accessToken,
        string refreshToken,
        DateTimeOffset expiresAt,
        BackendIdentityClaimV1? claim)
        => new(BackendIdentityErrorV1.None, accessToken, refreshToken, expiresAt, claim);

    public static BackendSessionStepV1 Failed(BackendIdentityErrorV1 error)
        => new(error, null, null, default, null);
}

public sealed record BackendPairingStepV1(
    BackendIdentityErrorV1 Error,
    string? DeviceId,
    string? ParentId,
    int PolicyVersion,
    TimeSpan? RetryAfter,
    bool IsUncertain)
{
    public bool IsSuccess => this.Error == BackendIdentityErrorV1.None;

    public static BackendPairingStepV1 Accepted(string deviceId, string parentId, int policyVersion)
        => new(BackendIdentityErrorV1.None, deviceId, parentId, policyVersion, null, false);

    public static BackendPairingStepV1 Failed(
        BackendIdentityErrorV1 error,
        TimeSpan? retryAfter = null,
        bool uncertain = false)
        => new(error, null, null, 0, retryAfter, uncertain);
}

public sealed record BackendPairingLifecycleResult(
    BackendIdentityErrorV1 Error,
    string? DeviceId = null,
    string? ParentId = null,
    int PolicyVersion = 0,
    TimeSpan? RetryAfter = null)
{
    public bool IsSuccess => this.Error == BackendIdentityErrorV1.None;
}

/// <summary>
/// Minimal replaceable port for the v1 pre-pair, pair, reconcile, and definitive refresh steps.
/// Implementations own transport timeouts; the coordinator never polls or retries a one-use code.
/// </summary>
public interface IBackendIdentityLifecyclePortV1
{
    Task<BackendSessionStepV1> RecoverOrCreatePrePairSessionAsync(CancellationToken cancellationToken);

    Task<BackendPairingStepV1> PairOnceAsync(PairingCommand command, CancellationToken cancellationToken);

    Task<BackendPairingStepV1> ReconcilePairingAsync(string operationId, CancellationToken cancellationToken);

    Task<BackendSessionStepV1> RefreshDefinitiveSessionAsync(string refreshToken, CancellationToken cancellationToken);
}

public interface IBackendIdentityCredentialStore
{
    Task<IdentityStoreResult> ReadIdentityAsync(CancellationToken cancellationToken = default);

    Task<IdentityStoreResult> WriteIdentityAsync(
        BackendIdentityCredentialSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<bool> InvalidateIdentityAsync(long generation, CancellationToken cancellationToken = default);
}

public sealed record BackendDefinitiveSession(long Generation, string DeviceId, string AccessToken, DateTimeOffset ExpiresAt);

public sealed record BackendDefinitiveSessionResult(BackendIdentityErrorV1 Error, BackendDefinitiveSession? Session = null)
{
    public bool IsSuccess => this.Error == BackendIdentityErrorV1.None && this.Session is not null;

    public static BackendDefinitiveSessionResult Authorized(BackendDefinitiveSession session) => new(BackendIdentityErrorV1.None, session);

    public static BackendDefinitiveSessionResult Failed(BackendIdentityErrorV1 error) => new(error);
}

/// <summary>
/// Service-owned authority for the definitive pairing lifecycle.
/// </summary>
public interface IBackendIdentityCoordinator
{
    BackendIdentityState CurrentState { get; }

    Task<BackendPairingLifecycleResult> PairAsync(
        PairingCommand command,
        CancellationToken cancellationToken = default);

    Task<BackendDefinitiveSessionResult> GetDefinitiveSessionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Forbidden));

    Task InvalidateAsync(long generation, BackendIdentityErrorV1 reason, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>
/// Service-owned authority for the definitive pairing lifecycle.
/// </summary>
public sealed class BackendIdentityCoordinator : IBackendIdentityCoordinator
{
    private readonly IBackendIdentityLifecyclePortV1 lifecyclePort;
    private readonly IBackendIdentityCredentialStore credentialStore;
    private readonly SemaphoreSlim pairingGate = new(1, 1);
    private readonly SemaphoreSlim authorityGate = new(1, 1);
    private readonly object refreshSync = new();
    private readonly System.TimeProvider timeProvider;
    private readonly TimeSpan refreshTimeout;
    private BackendIdentityState currentState = BackendIdentityState.Unpaired();
    private BackendIdentityCredentialSnapshot? currentCredentials;
    private long refreshGeneration = -1;
    private Task<BackendDefinitiveSessionResult>? refreshTask;
    private string? completedOperationId;
    private BackendPairingLifecycleResult? completedResult;

    public BackendIdentityCoordinator(
        IBackendIdentityLifecyclePortV1 lifecyclePort,
        IBackendIdentityCredentialStore credentialStore,
        System.TimeProvider? timeProvider = null,
        TimeSpan? refreshTimeout = null)
    {
        this.lifecyclePort = lifecyclePort ?? throw new ArgumentNullException(nameof(lifecyclePort));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.timeProvider = timeProvider ?? System.TimeProvider.System;
        this.refreshTimeout = refreshTimeout ?? TimeSpan.FromSeconds(10);
        if (this.refreshTimeout <= TimeSpan.Zero || this.refreshTimeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(refreshTimeout));
        }
    }

    public BackendIdentityState CurrentState => this.currentState;

    public async Task<BackendIdentityErrorV1> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var stored = await this.credentialStore.ReadIdentityAsync(cancellationToken);
        if (stored.Status == IdentityStoreStatus.NotFound)
        {
            return BackendIdentityErrorV1.None;
        }

        if (!stored.Success || stored.Snapshot is null)
        {
            this.currentState = this.currentState.Invalidate();
            return BackendIdentityErrorV1.InvalidPayload;
        }

        this.currentState = BackendIdentityState.Restore(
            BackendIdentityPhase.DefinitiveSession,
            stored.Snapshot.Generation,
            stored.Snapshot.DeviceId);
        this.currentCredentials = stored.Snapshot;
        return BackendIdentityErrorV1.None;
    }

    public async Task<BackendDefinitiveSessionResult> GetDefinitiveSessionAsync(
        CancellationToken cancellationToken = default)
    {
        BackendIdentityCredentialSnapshot? snapshot;
        Task<BackendDefinitiveSessionResult>? pendingRefresh = null;
        lock (this.refreshSync)
        {
            snapshot = this.currentCredentials;
            if (!this.currentState.CanAuthorizeRemoteAccess ||
                snapshot is null ||
                snapshot.Generation != this.currentState.Generation)
            {
                return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Forbidden);
            }

            if (snapshot.ExpiresAt > this.timeProvider.GetUtcNow().AddMinutes(2))
            {
                return BackendDefinitiveSessionResult.Authorized(ToSession(snapshot));
            }

            if (this.refreshTask is null || this.refreshGeneration != snapshot.Generation)
            {
                this.refreshGeneration = snapshot.Generation;
                this.refreshTask = this.RefreshAsync(snapshot);
            }

            pendingRefresh = this.refreshTask;
        }

        return await pendingRefresh.WaitAsync(cancellationToken);
    }

    public async Task InvalidateAsync(
        long generation,
        BackendIdentityErrorV1 reason,
        CancellationToken cancellationToken = default)
    {
        await this.authorityGate.WaitAsync(cancellationToken);
        try
        {
            if (!this.currentState.CanAuthorizeRemoteAccess || this.currentState.Generation != generation)
            {
                return;
            }

            await this.credentialStore.InvalidateIdentityAsync(generation, cancellationToken);
            lock (this.refreshSync)
            {
                this.currentCredentials = null;
                this.currentState = this.currentState.Invalidate();
            }
        }
        finally
        {
            this.authorityGate.Release();
        }
    }

    public async Task<BackendPairingLifecycleResult> PairAsync(
        PairingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.OperationId) ||
            string.IsNullOrWhiteSpace(command.Code) ||
            command.Code.Length != 6)
        {
            return new(BackendIdentityErrorV1.InvalidPayload);
        }

        await this.pairingGate.WaitAsync(cancellationToken);
        try
        {
            if (string.Equals(this.completedOperationId, command.OperationId, StringComparison.Ordinal))
            {
                return this.completedResult!;
            }

            if (this.currentState.Phase == BackendIdentityPhase.DefinitiveSession)
            {
                return new(BackendIdentityErrorV1.Duplicate);
            }

            return await this.PairCoreAsync(command with { Code = command.Code.Trim().ToUpperInvariant() }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            this.FailClosed();
            throw;
        }
        finally
        {
            this.pairingGate.Release();
        }
    }

    private async Task<BackendPairingLifecycleResult> PairCoreAsync(
        PairingCommand command,
        CancellationToken cancellationToken)
    {
        if (this.currentState.Phase != BackendIdentityPhase.Unpaired)
        {
            this.FailClosed();
        }

        var prePair = await this.lifecyclePort.RecoverOrCreatePrePairSessionAsync(cancellationToken);
        if (!prePair.IsSuccess || !HasCredentials(prePair))
        {
            return this.Complete(command.OperationId, ResolveStepError(prePair.Error));
        }

        this.currentState = this.currentState.TransitionTo(BackendIdentityPhase.PrePairSession);
        this.currentState = this.currentState.TransitionTo(BackendIdentityPhase.PairingPending);

        var pairing = await this.lifecyclePort.PairOnceAsync(command, cancellationToken);
        if (pairing.IsUncertain)
        {
            pairing = await this.lifecyclePort.ReconcilePairingAsync(command.OperationId, cancellationToken);
        }

        if (!pairing.IsSuccess || string.IsNullOrWhiteSpace(pairing.DeviceId) || string.IsNullOrWhiteSpace(pairing.ParentId))
        {
            return this.Complete(command.OperationId, ResolveStepError(pairing.Error), retryAfter: pairing.RetryAfter);
        }

        var definitive = await this.lifecyclePort.RefreshDefinitiveSessionAsync(prePair.RefreshToken!, cancellationToken);
        if (!definitive.IsSuccess || !HasCredentials(definitive))
        {
            return this.Complete(command.OperationId, ResolveStepError(definitive.Error));
        }

        var claimError = BackendIdentityContractV1.ValidateDeviceClaim(pairing.DeviceId, definitive.Claim);
        if (claimError != BackendIdentityErrorV1.None)
        {
            return this.Complete(command.OperationId, claimError);
        }

        var definitiveState = this.currentState.TransitionToDefinitive(pairing.DeviceId);
        var snapshot = new BackendIdentityCredentialSnapshot(
            definitiveState.Generation,
            pairing.DeviceId,
            pairing.ParentId,
            definitive.AccessToken!,
            definitive.RefreshToken!,
            definitive.ExpiresAt);
        var committed = await this.credentialStore.WriteIdentityAsync(snapshot, cancellationToken);
        if (!committed.Success || committed.Snapshot is null)
        {
            return this.Complete(command.OperationId, BackendIdentityErrorV1.Duplicate);
        }

        this.currentState = definitiveState;
        this.currentCredentials = snapshot;
        return this.Complete(
            command.OperationId,
            BackendIdentityErrorV1.None,
            pairing.DeviceId,
            pairing.ParentId,
            pairing.PolicyVersion);
    }

    private BackendPairingLifecycleResult Complete(
        string operationId,
        BackendIdentityErrorV1 error,
        string? deviceId = null,
        string? parentId = null,
        int policyVersion = 0,
        TimeSpan? retryAfter = null)
    {
        if (error != BackendIdentityErrorV1.None)
        {
            this.FailClosed();
        }

        this.completedOperationId = operationId;
        this.completedResult = new(error, deviceId, parentId, policyVersion, retryAfter);
        return this.completedResult;
    }

    private void FailClosed()
    {
        lock (this.refreshSync)
        {
            this.currentCredentials = null;
            this.currentState = this.currentState.Invalidate();
        }
    }

    private async Task<BackendDefinitiveSessionResult> RefreshAsync(BackendIdentityCredentialSnapshot snapshot)
    {
        using var timeout = new CancellationTokenSource(this.refreshTimeout);
        BackendSessionStepV1 refreshed;
        try
        {
            refreshed = await this.lifecyclePort.RefreshDefinitiveSessionAsync(snapshot.RefreshToken, timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            await this.InvalidateAsync(snapshot.Generation, BackendIdentityErrorV1.Timeout);
            return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Timeout);
        }

        if (!refreshed.IsSuccess || !HasCredentials(refreshed))
        {
            var error = ResolveStepError(refreshed.Error);
            await this.InvalidateAsync(snapshot.Generation, error);
            return BackendDefinitiveSessionResult.Failed(error);
        }

        var claimError = BackendIdentityContractV1.ValidateDeviceClaim(snapshot.DeviceId, refreshed.Claim);
        if (claimError != BackendIdentityErrorV1.None)
        {
            await this.InvalidateAsync(snapshot.Generation, claimError);
            return BackendDefinitiveSessionResult.Failed(claimError);
        }

        await this.authorityGate.WaitAsync();
        try
        {
            if (!this.currentState.CanAuthorizeRemoteAccess ||
                this.currentState.Generation != snapshot.Generation ||
                this.currentCredentials?.Generation != snapshot.Generation)
            {
                return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Revoked);
            }

            var next = snapshot with
            {
                Generation = checked(snapshot.Generation + 1),
                AccessToken = refreshed.AccessToken!,
                RefreshToken = refreshed.RefreshToken!,
                ExpiresAt = refreshed.ExpiresAt,
            };
            var committed = await this.credentialStore.WriteIdentityAsync(next);
            if (!committed.Success || committed.Snapshot is null)
            {
                this.FailClosed();
                return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Duplicate);
            }

            lock (this.refreshSync)
            {
                this.currentCredentials = next;
                this.currentState = BackendIdentityState.Restore(
                    BackendIdentityPhase.DefinitiveSession,
                    next.Generation,
                    next.DeviceId);
            }

            return BackendDefinitiveSessionResult.Authorized(ToSession(next));
        }
        finally
        {
            this.authorityGate.Release();
        }
    }

    private static BackendDefinitiveSession ToSession(BackendIdentityCredentialSnapshot snapshot)
        => new(snapshot.Generation, snapshot.DeviceId, snapshot.AccessToken, snapshot.ExpiresAt);

    private static bool HasCredentials(BackendSessionStepV1 session)
        => !string.IsNullOrWhiteSpace(session.AccessToken) &&
           !string.IsNullOrWhiteSpace(session.RefreshToken) &&
           session.ExpiresAt != default;

    private static BackendIdentityErrorV1 ResolveStepError(BackendIdentityErrorV1 error)
        => error == BackendIdentityErrorV1.None ? BackendIdentityErrorV1.InvalidPayload : error;
}
