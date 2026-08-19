// <copyright file="WnsRegistrationCoordinator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlParental.Domain;
using Microsoft.Extensions.Hosting;

public sealed record WnsRegistrationIntent(
    string OperationId,
    string ChannelUri,
    string Channel,
    DateTimeOffset ExpiresAt,
    WnsRegistrationStatus Status);

public interface IWnsRegistrationIntentStore
{
    Task<WnsRegistrationIntent?> ReadAsync(CancellationToken cancellationToken = default);

    Task<bool> WriteAsync(WnsRegistrationIntent intent, CancellationToken cancellationToken = default);
}

public interface IWnsRegistrationCoordinator
{
    Task<WnsRegistrationResult> RegisterAsync(RegisterWnsChannel request, CancellationToken cancellationToken = default);

    Task<WnsRegistrationResult?> ReconcileAsync(CancellationToken cancellationToken = default);
}

/// <summary>Protected, bounded adapter for the single latest WNS registration intent.</summary>
public sealed class SecretStoreWnsRegistrationIntentStore(ISecretStore secretStore) : IWnsRegistrationIntentStore
{
    private const string SecretName = "wns-registration-intent-v1";
    private const int MaximumEnvelopeCharacters = 4096;

    public async Task<WnsRegistrationIntent?> ReadAsync(CancellationToken cancellationToken = default)
    {
        var read = await secretStore.ReadAsync(SecretName, cancellationToken).ConfigureAwait(false);
        if (!read.Success || read.Value is null || read.Value.Length > MaximumEnvelopeCharacters)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WnsRegistrationIntent>(read.Value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<bool> WriteAsync(WnsRegistrationIntent intent, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(intent);
        return json.Length <= MaximumEnvelopeCharacters &&
            (await secretStore.WriteAsync(SecretName, json, cancellationToken).ConfigureAwait(false)).Success;
    }
}

/// <summary>Service-owned WNS intent state machine. Backend retries remain exclusively in <see cref="BackendClient"/>.</summary>
public sealed class WnsRegistrationCoordinator : IWnsRegistrationCoordinator
{
    private const int MaximumUriCharacters = 2048;
    private const int MaximumOperationCharacters = 64;
    private readonly IWnsRegistrationIntentStore store;
    private readonly IBackendClient backendClient;
    private readonly IBackendIdentityCoordinator identityCoordinator;
    private readonly System.TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool loaded;
    private WnsRegistrationIntent? latest;

    public WnsRegistrationCoordinator(
        IWnsRegistrationIntentStore store,
        IBackendClient backendClient,
        IBackendIdentityCoordinator identityCoordinator,
        System.TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.backendClient = backendClient ?? throw new ArgumentNullException(nameof(backendClient));
        this.identityCoordinator = identityCoordinator ?? throw new ArgumentNullException(nameof(identityCoordinator));
        this.timeProvider = timeProvider ?? System.TimeProvider.System;
    }

    public async Task<WnsRegistrationResult> RegisterAsync(
        RegisterWnsChannel request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!this.IsValid(request))
        {
            return Result(request.OperationId, WnsRegistrationStatus.Denied);
        }

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await this.LoadOnceAsync(cancellationToken).ConfigureAwait(false);
            if (this.latest?.OperationId == request.OperationId)
            {
                return SameIntent(this.latest, request)
                    ? Result(request.OperationId, this.latest.Status)
                    : Result(request.OperationId, WnsRegistrationStatus.Denied);
            }

            this.latest = new(request.OperationId, request.ChannelUri, request.Channel, request.ExpiresAt, WnsRegistrationStatus.PendingOffline);
            if (!await this.store.WriteAsync(this.latest, cancellationToken).ConfigureAwait(false))
            {
                return Result(request.OperationId, WnsRegistrationStatus.Denied);
            }

            return await this.SendLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    public async Task<WnsRegistrationResult?> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await this.LoadOnceAsync(cancellationToken).ConfigureAwait(false);
            if (this.latest is null || this.latest.Status == WnsRegistrationStatus.Accepted || this.latest.ExpiresAt <= this.timeProvider.GetUtcNow())
            {
                return null;
            }

            return await this.SendLatestAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async Task<WnsRegistrationResult> SendLatestAsync(CancellationToken cancellationToken)
    {
        if (!this.identityCoordinator.CurrentState.CanAuthorizeRemoteAccess)
        {
            return Result(this.latest!.OperationId, WnsRegistrationStatus.Denied);
        }

        var remote = await this.backendClient.RegisterPushTokenAsync(
            this.latest!.ChannelUri,
            this.latest.Channel,
            this.latest.ExpiresAt,
            cancellationToken).ConfigureAwait(false);
        var status = remote.Success
            ? WnsRegistrationStatus.Accepted
            : remote.FailureKind switch
            {
                PushTokenRegistrationFailureKind.Revoked or PushTokenRegistrationFailureKind.Forbidden => WnsRegistrationStatus.Denied,
                PushTokenRegistrationFailureKind.RateLimited => WnsRegistrationStatus.Retryable,
                _ => WnsRegistrationStatus.PendingOffline,
            };
        this.latest = this.latest with { Status = status };
        await this.store.WriteAsync(this.latest, cancellationToken).ConfigureAwait(false);
        return Result(this.latest.OperationId, status);
    }

    private async Task LoadOnceAsync(CancellationToken cancellationToken)
    {
        if (!this.loaded)
        {
            this.latest = await this.store.ReadAsync(cancellationToken).ConfigureAwait(false);
            this.loaded = true;
        }
    }

    private bool IsValid(RegisterWnsChannel request)
    {
        var now = this.timeProvider.GetUtcNow();
        return request.OperationId is { Length: > 0 and <= MaximumOperationCharacters } &&
            request.OperationId.All(static value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_') &&
            request.ChannelUri is { Length: > 0 and <= MaximumUriCharacters } &&
            string.Equals(request.Channel, "wns", StringComparison.Ordinal) &&
            request.ExpiresAt > now && request.ExpiresAt <= now.AddDays(30) &&
            Uri.TryCreate(request.ChannelUri, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) &&
            (string.Equals(uri.Host, "notify.windows.com", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.EndsWith(".notify.windows.com", StringComparison.OrdinalIgnoreCase));
    }

    private static bool SameIntent(WnsRegistrationIntent intent, RegisterWnsChannel request)
        => string.Equals(intent.ChannelUri, request.ChannelUri, StringComparison.Ordinal) &&
           string.Equals(intent.Channel, request.Channel, StringComparison.Ordinal) &&
           intent.ExpiresAt == request.ExpiresAt;

    private static WnsRegistrationResult Result(string? operationId, WnsRegistrationStatus status)
    {
        operationId = operationId is { Length: > 0 and <= MaximumOperationCharacters } ? operationId : "invalid";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(operationId)))[..16];
        return new(operationId, status, hash);
    }
}

/// <summary>Performs exactly one non-polling reconciliation after identity startup.</summary>
public sealed class WnsRegistrationReconciliationService(IWnsRegistrationCoordinator coordinator) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
        => _ = await coordinator.ReconcileAsync(cancellationToken).ConfigureAwait(false);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
