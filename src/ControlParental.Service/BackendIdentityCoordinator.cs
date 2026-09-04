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

/// <summary>Publishes fencing notifications when the definitive identity is invalidated.</summary>
public interface IBackendIdentityInvalidationSource
{
    event EventHandler<BackendIdentityInvalidatedEventArgs>? IdentityInvalidated;
}

public interface IBackendIdentityLifecycleSource
{
    event EventHandler<BackendIdentityLifecycleEventArgs>? LifecycleChanged;
}

public enum BackendIdentityLifecycleEventKind
{
    Activated,
    Invalidated,
    Closing,
}

internal enum BackendIdentityOperationKind
{
    HostedStart,
    HostedRefreshLoop,
    Initialize,
    Pair,
    GetSession,
    Refresh,
    Invalidate,
}

internal enum BackendIdentityAdmissionState
{
    Open,
    Closing,
    Closed,
}

internal readonly record struct BackendIdentityLifecycleSnapshot(
    BackendIdentityAdmissionState Admission,
    int InFlightCount,
    bool ShutdownTimedOut);

/// <summary>Details of an identity invalidation.</summary>
public sealed class BackendIdentityInvalidatedEventArgs : EventArgs
{
    public BackendIdentityInvalidatedEventArgs(long generation, BackendIdentityErrorV1 reason)
    {
        this.Generation = generation;
        this.Reason = reason;
    }

    public long Generation { get; }

    public BackendIdentityErrorV1 Reason { get; }
}

public sealed class BackendIdentityLifecycleEventArgs : EventArgs
{
    public BackendIdentityLifecycleEventArgs(long generation, BackendIdentityLifecycleEventKind kind, BackendIdentityErrorV1 reason = BackendIdentityErrorV1.None)
    {
        this.Generation = generation;
        this.Kind = kind;
        this.Reason = reason;
    }

    public long Generation { get; }
    public BackendIdentityLifecycleEventKind Kind { get; }
    public BackendIdentityErrorV1 Reason { get; }
}

/// <summary>
/// Service-owned authority for the definitive pairing lifecycle.
/// </summary>
public sealed class BackendIdentityCoordinator : IBackendIdentityCoordinator, IBackendIdentityInvalidationSource, IBackendIdentityLifecycleSource
{
    private readonly IBackendIdentityLifecyclePortV1 lifecyclePort;
    private readonly IBackendIdentityCredentialStore credentialStore;
    private readonly SemaphoreSlim pairingGate = new(1, 1);
    private readonly SemaphoreSlim mutationGate = new(1, 1);
    private readonly object lifecycleSync = new();
    private readonly CancellationTokenSource ownerCancellation = new();
    private readonly Dictionary<long, OperationRegistration> operations = new();
    private readonly Dictionary<long, HashSet<long>> operationIdsByGeneration = new();
    private readonly Dictionary<long, RefreshFlight> refreshByGeneration = new();
    private readonly System.TimeProvider timeProvider;
    private readonly TimeSpan refreshTimeout;
    private readonly TimeSpan shutdownTimeout;

    private BackendIdentityState currentState = BackendIdentityState.Unpaired();
    private BackendIdentityCredentialSnapshot? currentCredentials;
    private BackendIdentityAdmissionState admission = BackendIdentityAdmissionState.Open;
    private bool closingRequested;
    private Task? closeTask;
    private long nextOperationId;
    private bool shutdownTimedOut;

    private string? completedOperationId;
    private BackendPairingLifecycleResult? completedResult;

    public event EventHandler<BackendIdentityInvalidatedEventArgs>? IdentityInvalidated;
    public event EventHandler<BackendIdentityLifecycleEventArgs>? LifecycleChanged;

    public BackendIdentityCoordinator(
        IBackendIdentityLifecyclePortV1 lifecyclePort,
        IBackendIdentityCredentialStore credentialStore,
        System.TimeProvider? timeProvider = null,
        TimeSpan? refreshTimeout = null,
        TimeSpan? shutdownTimeout = null)
    {
        this.lifecyclePort = lifecyclePort ?? throw new ArgumentNullException(nameof(lifecyclePort));
        this.credentialStore = credentialStore ?? throw new ArgumentNullException(nameof(credentialStore));
        this.timeProvider = timeProvider ?? System.TimeProvider.System;
        this.refreshTimeout = refreshTimeout ?? TimeSpan.FromSeconds(10);
        this.shutdownTimeout = shutdownTimeout ?? TimeSpan.FromSeconds(10);
        if (this.refreshTimeout <= TimeSpan.Zero || this.refreshTimeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(refreshTimeout));
        }
        if (this.shutdownTimeout <= TimeSpan.Zero || this.shutdownTimeout > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(nameof(shutdownTimeout));
        }
    }

    public BackendIdentityState CurrentState => this.currentState;

    internal BackendIdentityLifecycleSnapshot LifecycleSnapshot
    {
        get
        {
            lock (this.lifecycleSync)
            {
                return new(this.admission, this.operations.Count, this.shutdownTimedOut);
            }
        }
    }

    public async Task<BackendIdentityErrorV1> InitializeAsync(CancellationToken cancellationToken = default)
    {
        var operation = this.Admit(BackendIdentityOperationKind.Initialize, -1, cancellationToken);
        if (operation is null)
        {
            return BackendIdentityErrorV1.Cancelled;
        }

        try
        {
            var stored = await this.credentialStore.ReadIdentityAsync(operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (this.IsClosingOrClosed())
            {
                return BackendIdentityErrorV1.Cancelled;
            }

            if (stored.Status == IdentityStoreStatus.NotFound)
            {
                return BackendIdentityErrorV1.None;
            }

            lock (this.lifecycleSync)
            {
                if (this.admission != BackendIdentityAdmissionState.Open || operation.Token.IsCancellationRequested)
                {
                    return BackendIdentityErrorV1.Cancelled;
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
        }
        finally
        {
            this.Release(operation);
        }
    }

    public Task<BackendDefinitiveSessionResult> GetDefinitiveSessionAsync(CancellationToken cancellationToken = default)
    {
        var waiter = this.Admit(BackendIdentityOperationKind.GetSession, -1, cancellationToken);
        if (waiter is null)
        {
            return Task.FromResult(BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Cancelled));
        }
        return this.GetDefinitiveSessionCoreAsync(waiter, cancellationToken);
    }

    private async Task<BackendDefinitiveSessionResult> GetDefinitiveSessionCoreAsync(
        OperationRegistration waiter,
        CancellationToken cancellationToken)
    {
        BackendIdentityCredentialSnapshot? snapshot;
        Task<BackendDefinitiveSessionResult>? pendingRefresh = null;
        try
        {
            lock (this.lifecycleSync)
        {
            if (this.admission != BackendIdentityAdmissionState.Open)
            {
                return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Cancelled);
            }

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

            if (!this.refreshByGeneration.TryGetValue(snapshot.Generation, out var flight))
            {
                var operation = this.Admit(BackendIdentityOperationKind.Refresh, snapshot.Generation, CancellationToken.None);
                if (operation is null)
                {
                    return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Cancelled);
                }
                flight = new RefreshFlight(operation);
                this.refreshByGeneration[snapshot.Generation] = flight;
                _ = this.RunRefreshFlightAsync(flight, snapshot);
            }

            pendingRefresh = flight.Result.Task;
        }

        return await pendingRefresh.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.Release(waiter);
        }
    }

    /// <summary>
    /// Cancels the shared refresh operation so its owner can drain it before shutdown.
    /// </summary>
    public void CancelRefresh()
    {
        lock (this.lifecycleSync)
        {
            foreach (var operation in this.operations.Values.ToArray())
            {
                if (operation.Kind == BackendIdentityOperationKind.Refresh)
                {
                    operation.Cancellation.Cancel();
                }
            }
        }
    }

    /// <summary>
    /// Closes refresh admission, cancels any admitted operation, and awaits its
    /// completion so no refresh can mutate state after shutdown.
    /// </summary>
    public async Task CloseRefreshAdmissionAndDrainAsync(CancellationToken cancellationToken = default)
    {
        Task close;
        lock (this.lifecycleSync)
        {
            if (this.admission == BackendIdentityAdmissionState.Closed)
            {
                return;
            }
            if (this.closeTask is null || (this.closeTask.IsCompleted && this.admission == BackendIdentityAdmissionState.Closing))
            {
                this.closingRequested = true;
                this.closeTask = this.CloseCoreAsync(cancellationToken);
            }
            close = this.closeTask;
        }

        await close.ConfigureAwait(false);
    }

    private async Task CloseCoreAsync(CancellationToken cancellationToken)
    {
        OperationRegistration[] registrations;
        long generation;
        lock (this.lifecycleSync)
        {
            this.admission = BackendIdentityAdmissionState.Closing;
            generation = this.currentState.Generation;
            registrations = this.operations.Values.ToArray();
        }

        // No cancellation, callbacks, or user events are invoked while holding lifecycleSync.
        this.ownerCancellation.Cancel();
        foreach (var registration in registrations)
        {
            registration.Cancellation.Cancel();
        }
        this.PublishLifecycle(new BackendIdentityLifecycleEventArgs(generation, BackendIdentityLifecycleEventKind.Closing));

        try
        {
            var tasks = registrations.Select(static operation => operation.Completion.Task).ToArray();
            var drain = Task.WhenAll(tasks);
            var timeout = Task.Delay(this.shutdownTimeout, this.timeProvider, cancellationToken);
            if (await Task.WhenAny(drain, timeout).ConfigureAwait(false) != drain)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lock (this.lifecycleSync)
                {
                    this.shutdownTimedOut = true;
                }
                throw new TimeoutException("Identity operations remained registered during shutdown.");
            }

            lock (this.lifecycleSync)
            {
                if (this.operations.Count != 0)
                {
                    this.shutdownTimedOut = true;
                    throw new TimeoutException("Identity operations remained registered during shutdown.");
                }
                this.admission = BackendIdentityAdmissionState.Closed;
            }
        }
        catch (TimeoutException)
        {
            lock (this.lifecycleSync)
            {
                this.shutdownTimedOut = true;
            }
            throw;
        }
        catch
        {
            throw;
        }
    }

    public async Task InvalidateAsync(
        long generation,
        BackendIdentityErrorV1 reason,
        CancellationToken cancellationToken = default)
    {
        var operation = this.Admit(BackendIdentityOperationKind.Invalidate, generation, cancellationToken);
        if (operation is null)
        {
            return;
        }

        OperationRegistration[] toCancel;
        RefreshFlight? invalidatedFlight;
        lock (this.lifecycleSync)
        {
            toCancel = this.operations.Values
                .Where(admitted => admitted.Generation == generation &&
                    admitted.Kind is BackendIdentityOperationKind.Refresh or BackendIdentityOperationKind.Pair)
                .ToArray();
            this.refreshByGeneration.TryGetValue(generation, out invalidatedFlight);
        }
        invalidatedFlight?.Result.TrySetResult(BackendDefinitiveSessionResult.Failed(reason));
        foreach (var admitted in toCancel)
        {
            admitted.Cancellation.Cancel();
        }

        var gateEntered = false;
        try
        {
            await this.mutationGate.WaitAsync(operation.Token).ConfigureAwait(false);
            gateEntered = true;
            if (!this.currentState.CanAuthorizeRemoteAccess || this.currentState.Generation != generation)
            {
                return;
            }

            operation.Token.ThrowIfCancellationRequested();
            await this.credentialStore.InvalidateIdentityAsync(generation, operation.Token).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            lock (this.lifecycleSync)
            {
                if (this.admission != BackendIdentityAdmissionState.Open ||
                    !this.currentState.CanAuthorizeRemoteAccess || this.currentState.Generation != generation)
                {
                    return;
                }
                this.currentCredentials = null;
                this.currentState = this.currentState.Invalidate();
            }
            this.IdentityInvalidated?.Invoke(this, new BackendIdentityInvalidatedEventArgs(generation, reason));
            this.LifecycleChanged?.Invoke(this, new BackendIdentityLifecycleEventArgs(generation, BackendIdentityLifecycleEventKind.Invalidated, reason));
        }
        finally
        {
            if (gateEntered)
            {
                this.mutationGate.Release();
            }
            this.Release(operation);
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

        long generation;
        lock (this.lifecycleSync)
        {
            generation = this.currentState.Generation;
        }
        var operation = this.Admit(BackendIdentityOperationKind.Pair, generation, cancellationToken);
        if (operation is null)
        {
            return new(BackendIdentityErrorV1.Cancelled);
        }

        var gateEntered = false;
        try
        {
            await this.pairingGate.WaitAsync(operation.Token).ConfigureAwait(false);
            gateEntered = true;
            if (string.Equals(this.completedOperationId, command.OperationId, StringComparison.Ordinal))
            {
                return this.completedResult!;
            }

            if (this.currentState.Phase == BackendIdentityPhase.DefinitiveSession)
            {
                return new(BackendIdentityErrorV1.Duplicate);
            }

            var result = await this.PairCoreAsync(command with { Code = command.Code.Trim().ToUpperInvariant() }, operation).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                this.PublishLifecycle(new BackendIdentityLifecycleEventArgs(this.currentState.Generation, BackendIdentityLifecycleEventKind.Activated));
            }
            return result;
        }
        catch (OperationCanceledException) when (operation.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new(BackendIdentityErrorV1.Cancelled);
        }
        finally
        {
            if (gateEntered)
            {
                this.pairingGate.Release();
            }
            this.Release(operation);
        }
    }

    private async Task<BackendPairingLifecycleResult> PairCoreAsync(
        PairingCommand command,
        OperationRegistration operation)
    {
        var cancellationToken = operation.Token;
        cancellationToken.ThrowIfCancellationRequested();
        if (this.IsClosingOrClosed())
        {
            return new(BackendIdentityErrorV1.Cancelled);
        }
        if (this.currentState.Phase != BackendIdentityPhase.Unpaired)
        {
            this.FailClosed();
        }

        var prePair = await this.lifecyclePort.RecoverOrCreatePrePairSessionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.IsClosingOrClosed())
        {
            return new(BackendIdentityErrorV1.Cancelled);
        }
        if (!prePair.IsSuccess || !HasCredentials(prePair))
        {
            return this.Complete(command.OperationId, ResolveStepError(prePair.Error));
        }

        this.currentState = this.currentState.TransitionTo(BackendIdentityPhase.PrePairSession);
        this.currentState = this.currentState.TransitionTo(BackendIdentityPhase.PairingPending);
        this.RebindGeneration(operation, this.currentState.Generation);

        var pairing = await this.lifecyclePort.PairOnceAsync(command, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.IsClosingOrClosed())
        {
            return new(BackendIdentityErrorV1.Cancelled);
        }
        if (pairing.IsUncertain)
        {
            pairing = await this.lifecyclePort.ReconcilePairingAsync(command.OperationId, cancellationToken);
        }

        if (!pairing.IsSuccess || string.IsNullOrWhiteSpace(pairing.DeviceId) || string.IsNullOrWhiteSpace(pairing.ParentId))
        {
            return this.Complete(command.OperationId, ResolveStepError(pairing.Error), retryAfter: pairing.RetryAfter);
        }

        var definitive = await this.lifecyclePort.RefreshDefinitiveSessionAsync(prePair.RefreshToken!, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (this.IsClosingOrClosed())
        {
            return new(BackendIdentityErrorV1.Cancelled);
        }
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
        this.RebindGeneration(operation, definitiveState.Generation);
        var snapshot = new BackendIdentityCredentialSnapshot(
            definitiveState.Generation,
            pairing.DeviceId,
            pairing.ParentId,
            definitive.AccessToken!,
            definitive.RefreshToken!,
            definitive.ExpiresAt);
        var gateEntered = false;
        try
        {
            await this.mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            gateEntered = true;
            cancellationToken.ThrowIfCancellationRequested();
            if (this.IsClosingOrClosed())
            {
                return new(BackendIdentityErrorV1.Cancelled);
            }
            var committed = await this.credentialStore.WriteIdentityAsync(snapshot, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!committed.Success || committed.Snapshot is null)
            {
                return this.Complete(command.OperationId, BackendIdentityErrorV1.Duplicate);
            }

            if (this.IsClosingOrClosed())
            {
                return new(BackendIdentityErrorV1.Cancelled);
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
        finally
        {
            if (gateEntered)
            {
                this.mutationGate.Release();
            }
        }
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
        long generation;
        lock (this.lifecycleSync)
        {
            generation = this.currentState.Generation;
            this.currentCredentials = null;
            this.currentState = this.currentState.Invalidate();
        }

        if (generation > 0)
        {
            this.IdentityInvalidated?.Invoke(
                this,
                new BackendIdentityInvalidatedEventArgs(generation, BackendIdentityErrorV1.InvalidPayload));
        }
    }

    private async Task<BackendDefinitiveSessionResult> RefreshAsync(
        BackendIdentityCredentialSnapshot snapshot,
        CancellationToken operationCancellation)
    {
        using var timeout = new CancellationTokenSource(this.refreshTimeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            timeout.Token,
            operationCancellation);
        BackendSessionStepV1 refreshed;
        try
        {
            refreshed = await this.lifecyclePort.RefreshDefinitiveSessionAsync(
                snapshot.RefreshToken,
                linkedCancellation.Token);
        }
        catch (OperationCanceledException) when (operationCancellation.IsCancellationRequested)
        {
            if (this.IsRefreshAdmissionClosed())
            {
                return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Cancelled);
            }

            await this.InvalidateAsync(snapshot.Generation, BackendIdentityErrorV1.Cancelled);
            return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Cancelled);
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

        var gateEntered = false;
        try
        {
            await this.mutationGate.WaitAsync(operationCancellation).ConfigureAwait(false);
            gateEntered = true;
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
                linkedCancellation.Token.ThrowIfCancellationRequested();
                var committed = await this.credentialStore.WriteIdentityAsync(next, linkedCancellation.Token);
                linkedCancellation.Token.ThrowIfCancellationRequested();
                if (!committed.Success || committed.Snapshot is null)
                {
                    this.FailClosed();
                    return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Duplicate);
                }

                lock (this.lifecycleSync)
                {
                    linkedCancellation.Token.ThrowIfCancellationRequested();
                    if (this.admission != BackendIdentityAdmissionState.Open)
                    {
                        return BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Cancelled);
                    }
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
            if (gateEntered)
            {
                this.mutationGate.Release();
            }
        }
    }

    private bool IsClosingOrClosed()
    {
        lock (this.lifecycleSync)
        {
            return this.admission != BackendIdentityAdmissionState.Open || this.closingRequested;
        }
    }

    private bool IsRefreshAdmissionClosed()
    {
        lock (this.lifecycleSync)
        {
            return this.admission != BackendIdentityAdmissionState.Open;
        }
    }

    private static BackendDefinitiveSession ToSession(BackendIdentityCredentialSnapshot snapshot)
        => new(snapshot.Generation, snapshot.DeviceId, snapshot.AccessToken, snapshot.ExpiresAt);

    private static bool HasCredentials(BackendSessionStepV1 session)
        => !string.IsNullOrWhiteSpace(session.AccessToken) &&
           !string.IsNullOrWhiteSpace(session.RefreshToken) &&
           session.ExpiresAt != default;

    private sealed class OperationRegistration : IDisposable
    {
        public OperationRegistration(long id, BackendIdentityOperationKind kind, long generation, CancellationTokenSource cancellation)
        {
            this.Id = id;
            this.Kind = kind;
            this.Generation = generation;
            this.Cancellation = cancellation;
        }

        public long Id { get; }
        public BackendIdentityOperationKind Kind { get; }
        public long Generation { get; private set; }
        public CancellationTokenSource Cancellation { get; }
        public CancellationToken Token => this.Cancellation.Token;
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Rebind(long generation) => this.Generation = generation;
        public void Dispose() => this.Cancellation.Dispose();
    }

    private sealed class RefreshFlight(OperationRegistration operation)
    {
        public OperationRegistration Operation { get; } = operation;
        public TaskCompletionSource<BackendDefinitiveSessionResult> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private OperationRegistration? Admit(BackendIdentityOperationKind kind, long generation, CancellationToken callerToken)
    {
        lock (this.lifecycleSync)
        {
            if (this.admission != BackendIdentityAdmissionState.Open || this.closingRequested)
            {
                return null;
            }

            var linked = CancellationTokenSource.CreateLinkedTokenSource(this.ownerCancellation.Token, callerToken);
            var operation = new OperationRegistration(++this.nextOperationId, kind, generation, linked);
            this.operations.Add(operation.Id, operation);
            if (generation >= 0)
            {
                if (!this.operationIdsByGeneration.TryGetValue(generation, out var ids))
                {
                    ids = new HashSet<long>();
                    this.operationIdsByGeneration.Add(generation, ids);
                }
                ids.Add(operation.Id);
            }
            return operation;
        }
    }

    private void Release(OperationRegistration operation)
    {
        lock (this.lifecycleSync)
        {
            this.operations.Remove(operation.Id);
            if (this.operationIdsByGeneration.TryGetValue(operation.Generation, out var ids))
            {
                ids.Remove(operation.Id);
                if (ids.Count == 0)
                {
                    this.operationIdsByGeneration.Remove(operation.Generation);
                }
            }
            operation.Completion.TrySetResult(true);
        }
        operation.Dispose();
    }

    private void RebindGeneration(OperationRegistration operation, long generation)
    {
        lock (this.lifecycleSync)
        {
            if (operation.Generation == generation)
            {
                return;
            }
            if (this.operationIdsByGeneration.TryGetValue(operation.Generation, out var oldIds))
            {
                oldIds.Remove(operation.Id);
                if (oldIds.Count == 0)
                {
                    this.operationIdsByGeneration.Remove(operation.Generation);
                }
            }
            operation.Rebind(generation);
            if (!this.operationIdsByGeneration.TryGetValue(generation, out var ids))
            {
                ids = new HashSet<long>();
                this.operationIdsByGeneration.Add(generation, ids);
            }
            ids.Add(operation.Id);
        }
    }

    private bool IsCurrentOpenGeneration(long generation, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        lock (this.lifecycleSync)
        {
            return this.admission == BackendIdentityAdmissionState.Open &&
                this.currentState.CanAuthorizeRemoteAccess &&
                this.currentState.Generation == generation;
        }
    }

    private async Task RunRefreshFlightAsync(RefreshFlight flight, BackendIdentityCredentialSnapshot snapshot)
    {
        try
        {
            flight.Result.TrySetResult(await this.RefreshAsync(snapshot, flight.Operation.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            flight.Result.TrySetResult(BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Cancelled));
        }
        catch (Exception)
        {
            flight.Result.TrySetResult(BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.InvalidPayload));
        }
        finally
        {
            lock (this.lifecycleSync)
            {
                if (this.refreshByGeneration.TryGetValue(snapshot.Generation, out var current) && ReferenceEquals(current, flight))
                {
                    this.refreshByGeneration.Remove(snapshot.Generation);
                }
            }
            this.Release(flight.Operation);
        }
    }

    internal Task RunOwnedOperationAsync(
        BackendIdentityOperationKind kind,
        long generation,
        Func<CancellationToken, Task> body,
        CancellationToken callerToken = default)
    {
        ArgumentNullException.ThrowIfNull(body);
        var operation = this.Admit(kind, generation, callerToken);
        if (operation is null)
        {
            return Task.FromCanceled(new CancellationToken(true));
        }
        return this.RunOwnedOperationCoreAsync(operation, body);
    }

    private async Task RunOwnedOperationCoreAsync(OperationRegistration operation, Func<CancellationToken, Task> body)
    {
        try
        {
            await body(operation.Token).ConfigureAwait(false);
        }
        finally
        {
            this.Release(operation);
        }
    }

    private void PublishLifecycle(BackendIdentityLifecycleEventArgs args)
        => this.LifecycleChanged?.Invoke(this, args);

    private static BackendIdentityErrorV1 ResolveStepError(BackendIdentityErrorV1 error)
        => error == BackendIdentityErrorV1.None ? BackendIdentityErrorV1.InvalidPayload : error;
}
