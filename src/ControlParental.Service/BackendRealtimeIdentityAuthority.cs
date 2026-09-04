// <copyright file="BackendRealtimeIdentityAuthority.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// Service-owned in-memory lease used by authenticated realtime composition.
/// It is populated only from <see cref="IBackendIdentityCoordinator"/> and
/// clears immediately when the coordinator cannot authorize the session.
/// </summary>
public sealed class BackendRealtimeIdentityAuthority : IRealtimeIdentityAuthority, IDisposable
{
    private readonly IBackendIdentityCoordinator coordinator;
    private readonly System.TimeProvider timeProvider;
    private readonly object sync = new();
    private long refreshSequence;
    private RealtimeIdentitySnapshot? current;
    private bool publicationClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackendRealtimeIdentityAuthority"/> class.
    /// </summary>
    /// <param name="coordinator">The Service-owned identity coordinator.</param>
    public BackendRealtimeIdentityAuthority(
        IBackendIdentityCoordinator coordinator,
        System.TimeProvider? timeProvider = null)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        this.timeProvider = timeProvider ?? System.TimeProvider.System;
        if (coordinator is IBackendIdentityInvalidationSource invalidationSource)
        {
            invalidationSource.IdentityInvalidated += this.OnIdentityInvalidated;
        }
        if (coordinator is IBackendIdentityLifecycleSource lifecycleSource)
        {
            lifecycleSource.LifecycleChanged += this.OnLifecycleChanged;
        }
    }

    /// <inheritdoc />
    public RealtimeIdentitySnapshot? Current
    {
        get
        {
            lock (this.sync)
            {
                return this.current;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>
    /// Refreshes the lease from the definitive Service-owned session.
    /// </summary>
    /// <returns>The coordinator outcome; non-success always clears the lease.</returns>
    public async Task<BackendIdentityErrorV1> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var sequence = Interlocked.Increment(ref this.refreshSequence);
        BackendDefinitiveSessionResult result;
        try
        {
            result = await this.coordinator.GetDefinitiveSessionAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return BackendIdentityErrorV1.Cancelled;
        }
        catch (Exception)
        {
            this.Clear();
            return BackendIdentityErrorV1.InvalidPayload;
        }

        var session = result.IsSuccess && result.Session is not null && IsUsableSession(result.Session)
            ? new RealtimeIdentitySnapshot(
                result.Session.AccessToken,
                result.Session.DeviceId,
                result.Session.Generation)
            {
                ExpiresAt = result.Session.ExpiresAt,
            }
            : null;
        var error = !result.IsSuccess
            ? result.Error
            : session is not null ? result.Error : BackendIdentityErrorV1.InvalidPayload;
        this.TryPublish(sequence, session);
        return error;
    }

    private void TryPublish(long sequence, RealtimeIdentitySnapshot? snapshot)
    {
        lock (this.sync)
        {
            if (this.publicationClosed || sequence != Volatile.Read(ref this.refreshSequence))
            {
                return;
            }

            this.current = snapshot;
        }

        this.Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool IsUsableSession(BackendDefinitiveSession session)
    {
        return session.Generation > 0
            && !string.IsNullOrWhiteSpace(session.DeviceId)
            && LooksLikeJwt(session.AccessToken)
            && session.ExpiresAt > this.timeProvider.GetUtcNow();
    }

    private static bool LooksLikeJwt(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        var parts = accessToken.Split('.', StringSplitOptions.None);
        return parts.Length == 3 && parts.All(static part => part.Length > 0);
    }

    /// <summary>
    /// Clears the lease after revocation, credential corruption, or shutdown.
    /// </summary>
    public void Clear()
    {
        Interlocked.Increment(ref this.refreshSequence);
        lock (this.sync)
        {
            this.current = null;
        }

        this.Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ClosePublication()
    {
        Interlocked.Increment(ref this.refreshSequence);
        lock (this.sync)
        {
            this.publicationClosed = true;
            this.current = null;
        }
        this.Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (this.coordinator is IBackendIdentityInvalidationSource invalidationSource)
        {
            invalidationSource.IdentityInvalidated -= this.OnIdentityInvalidated;
        }
        if (this.coordinator is IBackendIdentityLifecycleSource lifecycleSource)
        {
            lifecycleSource.LifecycleChanged -= this.OnLifecycleChanged;
        }

        this.Clear();
    }

    private void OnIdentityInvalidated(object? sender, BackendIdentityInvalidatedEventArgs args)
    {
        this.Clear();
    }

    private void OnLifecycleChanged(object? sender, BackendIdentityLifecycleEventArgs args)
    {
        if (args.Kind == BackendIdentityLifecycleEventKind.Closing)
        {
            this.ClosePublication();
        }
        else if (args.Kind == BackendIdentityLifecycleEventKind.Activated)
        {
            this.Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
