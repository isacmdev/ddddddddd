// <copyright file="RealtimeIdentityPipeRedTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Service.Interop;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// RED coverage for the authenticated Service UI-pipe identity seam.
/// These tests deliberately exercise the real listener deserializer and handler.
/// </summary>
public sealed class RealtimeIdentityPipeRedTests
{
    private const int MaximumEnvelopeBytes = 65_536;
    private static readonly SecurityIdentifier ParentSid =
        new(WellKnownSidType.BuiltinUsersSid, null);

    [Fact]
    public async Task AuthenticatedPipe_RoundTripsTheServiceOwnedLeaseAndCorrelation()
    {
        // Intentional green control: this is the productive
        // PipeServerListener -> source-generated deserializer -> handler ->
        // response path, including the Service-owned authority.
        var correlationId = "00000000-0000-4000-8000-000000000101";
        var coordinator = new TestCoordinator
        {
            Result = BackendDefinitiveSessionResult.Authorized(
                new BackendDefinitiveSession(
                    4,
                    "device-a",
                    "header.payload.signature",
                    DateTimeOffset.UtcNow.AddMinutes(5))),
        };
        var request = new GetRealtimeIdentity
        {
            ContractVersion = 1,
            CorrelationId = correlationId,
        };
        var pipe = new TestUiPipe(Serialize(request));

        await RunAuthenticatedListenerAsync(pipe, coordinator);

        Assert.Equal(1, coordinator.RefreshCalls);
        var response = DeserializeResponse(pipe.Writes.Single());
        Assert.True(response.Success);
        Assert.Equal("device-a", response.DeviceId);
        Assert.Equal(4, response.Generation);
        Assert.Equal("header.payload.signature", response.AccessToken);
        Assert.Equal(correlationId, response.CorrelationId);
    }

    [Fact]
    public async Task AuthenticatedIdentityPath_NeverLogsBearerOrRawDeviceId()
    {
        // Intentional green control: the productive pipe -> handler ->
        // Service-authority path writes no credential or raw device diagnostic.
        const string token = "header.sensitive-bearer.signature";
        const string deviceId = "device-sensitive";
        var coordinator = new TestCoordinator
        {
            Result = BackendDefinitiveSessionResult.Authorized(
                new BackendDefinitiveSession(
                    4,
                    deviceId,
                    token,
                    DateTimeOffset.UtcNow.AddMinutes(5))),
        };
        var logger = new RecordingLogger<UIMessageHandler>();
        var request = new GetRealtimeIdentity
        {
            ContractVersion = 1,
            CorrelationId = "00000000-0000-4000-8000-000000000107",
        };
        var pipe = new TestUiPipe(Serialize(request));

        await RunAuthenticatedListenerAsync(pipe, coordinator, logger: logger);

        Assert.DoesNotContain(token, logger.Text.ToString());
        Assert.DoesNotContain(deviceId, logger.Text.ToString());
    }

    [Fact]
    public async Task AuthenticatedPipe_RejectsAnEmptyCorrelationBeforeTheHandler()
    {
        var coordinator = NewAuthorizedCoordinator();
        var request = new GetRealtimeIdentity
        {
            ContractVersion = 1,
            CorrelationId = string.Empty,
        };
        var pipe = new TestUiPipe(Serialize(request));

        await RunAuthenticatedListenerAsync(pipe, coordinator);

        Assert.Equal(0, coordinator.RefreshCalls);
        Assert.Empty(pipe.Writes);
    }

    [Theory]
    [InlineData("00000000-0000-4000-8000-000000000106")]
    [InlineData("00000000-0000-4000-8000-00000000010A")]
    public async Task AuthenticatedPipe_RequiresCanonicalNonEmptyCorrelation(string correlationId)
    {
        var coordinator = NewAuthorizedCoordinator();
        var request = new GetRealtimeIdentity
        {
            ContractVersion = 1,
            CorrelationId = correlationId,
        };
        var pipe = new TestUiPipe(Serialize(request));

        await RunAuthenticatedListenerAsync(pipe, coordinator);

        var expectedAccepted = correlationId == correlationId.ToLowerInvariant();
        Assert.Equal(expectedAccepted, coordinator.RefreshCalls == 1);
        Assert.Equal(expectedAccepted, pipe.Writes.Count == 1);
    }

    [Fact]
    public async Task AuthenticatedPipe_RejectsAnUnsupportedIdentityContractVersionBeforeTheHandler()
    {
        // Intentional green control: the listener rejects the identity
        // contract version before source-generated dispatch or authority use.
        var coordinator = NewAuthorizedCoordinator();
        var request = new GetRealtimeIdentity
        {
            ContractVersion = 2,
            CorrelationId = "00000000-0000-4000-8000-000000000106",
        };
        var pipe = new TestUiPipe(Serialize(request));

        await RunAuthenticatedListenerAsync(pipe, coordinator);

        Assert.Equal(0, coordinator.RefreshCalls);
        Assert.Empty(pipe.Writes);
    }

    [Theory]
    [InlineData(2_048, true)]
    [InlineData(2_049, false)]
    public async Task AuthenticatedPipe_EnforcesTheInclusiveUtf8WnsUriBoundary(
        int uriLength,
        bool expectedAccepted)
    {
        // OperationId and all envelope fields are valid; only the documented
        // WNS URI field bound changes between the two rows.
        var coordinator = NewAuthorizedCoordinator();
        const string uriPrefix = "https://push.control-parental.test/";
        var uri = uriPrefix + new string('x', uriLength - uriPrefix.Length);
        Assert.Equal(uriLength, Encoding.UTF8.GetByteCount(uri));
        var request = new RegisterWnsChannel(
            "00000000-0000-4000-8000-000000000105",
            uri,
            "wns",
            DateTimeOffset.UtcNow.AddMinutes(5));
        var registration = new RecordingWnsRegistrationCoordinator();
        var pipe = new TestUiPipe(Serialize(request));

        await RunAuthenticatedListenerAsync(pipe, coordinator, wnsRegistrationCoordinator: registration);

        Assert.Equal(expectedAccepted, registration.RegisterCalls == 1);
        Assert.Equal(expectedAccepted, pipe.Writes.Count == 1);
    }

    [Fact]
    public async Task AuthenticatedPipe_RejectsUnknownShapeBeforeTheHandler()
    {
        // Intentional green control: DeserializeMessage uses the source-
        // generated catalogue with unmapped members rejected before dispatch.
        var coordinator = NewAuthorizedCoordinator();
        var request = Encoding.UTF8.GetBytes(
            "{\"MessageType\":\"GetRealtimeIdentity\",\"ContractVersion\":1,\"CorrelationId\":\"00000000-0000-4000-8000-000000000102\",\"unexpected\":true}");
        var pipe = new TestUiPipe(request);

        await RunAuthenticatedListenerAsync(pipe, coordinator);

        Assert.Equal(0, coordinator.RefreshCalls);
        Assert.Empty(pipe.Writes);
    }

    [Theory]
    [InlineData(MaximumEnvelopeBytes, true)]
    [InlineData(MaximumEnvelopeBytes + 1, false)]
    public async Task AuthenticatedPipeWriter_EnforcesTheInclusiveUtf8ResponseBoundary(
        int responseSize,
        bool expectedAccepted)
    {
        // The token remains small and the response keeps the known source-
        // generated shape. ErrorCode is padding only so this probe isolates
        // the total UTF-8 envelope bound from the UI token bound.
        var coordinator = NewAuthorizedCoordinator();
        var handler = new UIMessageHandler(
            onboardingStateService: null!,
            enforcementLevelQueryHandler: null!,
            scopeFactory: null!,
            logger: NullLogger<UIMessageHandler>.Instance,
            realtimeIdentityAuthority: new BackendRealtimeIdentityAuthority(coordinator));
        using var stop = new CancellationTokenSource();
        var pipe = new TestUiPipe(Array.Empty<byte>(), stopAfterConnection: true);
        using var listener = new NamedPipeUIServer.PipeServerListener(
            "red-ui-pipe",
            ParentSid,
            childSid: null,
            handler,
            onDisconnected: static () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeUIServer.IUiPipeServer>(pipe),
            delay: null,
            logger: NullLogger.Instance);

        var start = listener.StartAsync();
        await pipe.ConnectionStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await listener.SendAsync(ResponseForSize(responseSize));

        Assert.Equal(expectedAccepted, pipe.Writes.Count == 1);
        if (!expectedAccepted)
        {
            Assert.Empty(pipe.Writes);
        }

        stop.Cancel();
        await start.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AuthenticatedPipe_RejectsAClientWithTheWrongSidBeforeTheHandler()
    {
        // Intentional green control: ValidateClient compares the impersonated
        // SID before ReadMessagesAsync can invoke UIMessageHandler.
        var coordinator = NewAuthorizedCoordinator();
        var request = new GetRealtimeIdentity
        {
            ContractVersion = 1,
            CorrelationId = "00000000-0000-4000-8000-000000000104",
        };
        var pipe = new TestUiPipe(Serialize(request), ParentSid, stopAfterConnection: true);

        await RunAuthenticatedListenerAsync(pipe, coordinator, ParentSid: new(WellKnownSidType.BuiltinAdministratorsSid, null));

        Assert.Equal(0, coordinator.RefreshCalls);
        Assert.Empty(pipe.Writes);
    }

    [Fact]
    public async Task HostedOwner_RefreshesBeforeExpiry_AndStopClearsTheLease()
    {
        // RED behavior probe over the concrete hosted owner. The current
        // production owner has no refresh loop and exposes no clock/delay
        // injection, so deadline scheduling cannot be made deterministic here
        // without a production seam. The executable assertion records that
        // missing lifecycle behavior instead of using a compile-time fake.
        var now = DateTimeOffset.UtcNow;
        var store = new InMemoryIdentityStore(new BackendIdentityCredentialSnapshot(
            4,
            "device-a",
            "parent-a",
            "header.payload.signature",
            "refresh-material",
            now.AddSeconds(90)));
        var lifecycle = new RecordingLifecyclePort();
        lifecycle.RefreshResult = BackendSessionStepV1.Definitive(
            "header.refreshed.signature",
            "refresh-material-next",
            now.AddSeconds(30),
            new BackendIdentityClaimV1("device-a"));
        var coordinator = new BackendIdentityCoordinator(
            lifecycle,
            store,
            new FixedTimeProvider(now),
            refreshTimeout: TimeSpan.FromSeconds(1));
        using var authority = new BackendRealtimeIdentityAuthority(coordinator);
        var owner = new BackendIdentityStartupService(
            coordinator,
            authority,
            new FixedTimeProvider(now),
            static (interval, token) => interval == TimeSpan.FromSeconds(1)
                ? Task.Delay(Timeout.InfiniteTimeSpan, token)
                : Task.CompletedTask);

        await owner.StartAsync(CancellationToken.None);

        Assert.Equal(1, lifecycle.RefreshCalls);
        Assert.NotNull(authority.Current);
        await lifecycle.SecondRefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.True(lifecycle.RefreshCalls >= 2);
        await owner.StopAsync(CancellationToken.None);
        Assert.Null(authority.Current);
    }

    [Fact]
    public async Task HostedOwner_StopAsync_CancelsAnInFlightQueryAndDrainsTheLifecycle()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemoryIdentityStore(new BackendIdentityCredentialSnapshot(
            4,
            "device-a",
            "parent-a",
            "header.payload.signature",
            "refresh-material",
            now.AddSeconds(30)));
        var lifecycle = new BlockingLifecyclePort();
        var coordinator = new BackendIdentityCoordinator(
            lifecycle,
            store,
            new FixedTimeProvider(now),
            refreshTimeout: TimeSpan.FromSeconds(30));
        await coordinator.InitializeAsync();
        using var authority = new BackendRealtimeIdentityAuthority(coordinator);
        var owner = new BackendIdentityStartupService(coordinator, authority);
        using var startCancellation = new CancellationTokenSource();
        var start = owner.StartAsync(startCancellation.Token);
        await lifecycle.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await owner.StopAsync(CancellationToken.None);
        Assert.True(start.IsCompleted);
        await lifecycle.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Null(authority.Current);
    }

    [Fact]
    public async Task Coordinator_ShutdownPreservesDurableSnapshotAndRejectsLateRefreshCommit()
    {
        var now = DateTimeOffset.UtcNow;
        var initial = new BackendIdentityCredentialSnapshot(
            4,
            "device-a",
            "parent-a",
            "header.payload.signature",
            "refresh-material",
            now.AddSeconds(30));
        var store = new InMemoryIdentityStore(initial);
        var lifecycle = new CancellationIgnoringLifecyclePort
        {
            Result = BackendSessionStepV1.Definitive(
                "header.late.signature",
                "refresh-late",
                now.AddMinutes(5),
                new BackendIdentityClaimV1("device-a")),
        };
        var coordinator = new BackendIdentityCoordinator(
            lifecycle,
            store,
            new FixedTimeProvider(now),
            refreshTimeout: TimeSpan.FromSeconds(1));
        await coordinator.InitializeAsync();

        var refresh = coordinator.GetDefinitiveSessionAsync();
        await lifecycle.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var drain = coordinator.CloseRefreshAdmissionAndDrainAsync();
        lifecycle.Release();

        await drain;
        var result = await refresh;

        Assert.Equal(BackendIdentityErrorV1.Cancelled, result.Error);
        Assert.Equal(initial, store.Snapshot);
        Assert.Equal(4, coordinator.CurrentState.Generation);
    }

    [Fact]
    public async Task ServiceAuthority_ErrorRefreshClearsThePreviousLeaseImmediately()
    {
        // Intentional green control: the existing Service-owned authority
        // already clears its in-memory lease on a definitive refresh error.
        var now = DateTimeOffset.UtcNow;
        var time = new FixedTimeProvider(now);
        var store = new InMemoryIdentityStore(new BackendIdentityCredentialSnapshot(
            4,
            "device-a",
            "parent-a",
            "header.payload.signature",
            "refresh-material",
            now.AddMinutes(5)));
        var coordinator = new BackendIdentityCoordinator(
            new RecordingLifecyclePort(),
            store,
            time,
            refreshTimeout: TimeSpan.FromSeconds(1));
        await coordinator.InitializeAsync();
        using var authority = new BackendRealtimeIdentityAuthority(coordinator);

        Assert.Equal(BackendIdentityErrorV1.None, await authority.RefreshAsync());
        Assert.NotNull(authority.Current);
        time.Advance(TimeSpan.FromMinutes(6));

        var error = await authority.RefreshAsync();

        Assert.Equal(BackendIdentityErrorV1.Forbidden, error);
        Assert.Null(authority.Current);
        Assert.False(coordinator.CurrentState.CanAuthorizeRemoteAccess);
    }

    [Fact]
    public async Task ServiceAuthority_RevocationFencesAnOlderRefreshFromRepublishing()
    {
        // Intentional green control: the existing coordinator generation fence
        // prevents an older refresh from republishing after invalidation.
        var now = DateTimeOffset.UtcNow;
        var time = new FixedTimeProvider(now);
        var store = new InMemoryIdentityStore(new BackendIdentityCredentialSnapshot(
            4,
            "device-a",
            "parent-a",
            "header.payload.signature",
            "refresh-material",
            now.AddSeconds(30)));
        var lifecycle = new BlockingLifecyclePort
        {
            Result = BackendSessionStepV1.Definitive(
                "header.next.signature",
                "refresh-next",
                now.AddMinutes(5),
                new BackendIdentityClaimV1("device-a")),
        };
        var coordinator = new BackendIdentityCoordinator(
            lifecycle,
            store,
            time,
            refreshTimeout: TimeSpan.FromSeconds(30));
        await coordinator.InitializeAsync();
        using var authority = new BackendRealtimeIdentityAuthority(coordinator);
        time.Advance(TimeSpan.FromSeconds(31));

        var refresh = authority.RefreshAsync();
        await lifecycle.RefreshStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await coordinator.InvalidateAsync(4, BackendIdentityErrorV1.Revoked);
        Assert.Null(authority.Current);

        lifecycle.Release();
        await refresh;

        Assert.Null(authority.Current);
        Assert.Equal(BackendIdentityPhase.Unpaired, coordinator.CurrentState.Phase);
    }

    private static TaskCompletionSource<T> NewSignal<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : System.TimeProvider
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => this.UtcNow;

        public void Advance(TimeSpan amount) => this.UtcNow += amount;
    }

    private sealed class InMemoryIdentityStore(BackendIdentityCredentialSnapshot? initial)
        : IBackendIdentityCredentialStore
    {
        private BackendIdentityCredentialSnapshot? snapshot = initial;

        public BackendIdentityCredentialSnapshot? Snapshot => this.snapshot;

        public Task<IdentityStoreResult> ReadIdentityAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(this.snapshot is null
                ? new IdentityStoreResult(IdentityStoreStatus.NotFound, null, null)
                : new IdentityStoreResult(IdentityStoreStatus.Found, this.snapshot, null));

        public Task<IdentityStoreResult> WriteIdentityAsync(
            BackendIdentityCredentialSnapshot next,
            CancellationToken cancellationToken = default)
        {
            this.snapshot = next;
            return Task.FromResult(new IdentityStoreResult(IdentityStoreStatus.Found, next, null));
        }

        public Task<bool> InvalidateIdentityAsync(
            long generation,
            CancellationToken cancellationToken = default)
        {
            if (this.snapshot?.Generation == generation)
            {
                this.snapshot = null;
            }

            return Task.FromResult(true);
        }
    }

    private sealed class RecordingLifecyclePort : IBackendIdentityLifecyclePortV1
    {
        public int RefreshCalls { get; private set; }

        public TaskCompletionSource<bool> SecondRefreshStarted { get; } = NewSignal<bool>();

        public BackendSessionStepV1 RefreshResult { get; set; } =
            BackendSessionStepV1.Failed(BackendIdentityErrorV1.Forbidden);

        public Task<BackendSessionStepV1> RecoverOrCreatePrePairSessionAsync(CancellationToken cancellationToken)
            => Task.FromResult(BackendSessionStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public Task<BackendPairingStepV1> PairOnceAsync(
            PairingCommand command,
            CancellationToken cancellationToken)
            => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public Task<BackendPairingStepV1> ReconcilePairingAsync(
            string operationId,
            CancellationToken cancellationToken)
            => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public Task<BackendSessionStepV1> RefreshDefinitiveSessionAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            if (++this.RefreshCalls >= 2)
            {
                this.SecondRefreshStarted.TrySetResult(true);
            }

            return Task.FromResult(this.RefreshResult);
        }
    }

    private sealed class BlockingLifecyclePort : IBackendIdentityLifecyclePortV1
    {
        private readonly TaskCompletionSource<bool> release = NewSignal<bool>();

        public TaskCompletionSource<bool> RefreshStarted { get; } = NewSignal<bool>();

        public TaskCompletionSource<bool> CancellationObserved { get; } = NewSignal<bool>();

        public BackendSessionStepV1 Result { get; set; } =
            BackendSessionStepV1.Failed(BackendIdentityErrorV1.Timeout);

        public Task<BackendSessionStepV1> RecoverOrCreatePrePairSessionAsync(CancellationToken cancellationToken)
            => Task.FromResult(BackendSessionStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public Task<BackendPairingStepV1> PairOnceAsync(
            PairingCommand command,
            CancellationToken cancellationToken)
            => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public Task<BackendPairingStepV1> ReconcilePairingAsync(
            string operationId,
            CancellationToken cancellationToken)
            => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public async Task<BackendSessionStepV1> RefreshDefinitiveSessionAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            this.RefreshStarted.TrySetResult(true);
            try
            {
                await this.release.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                this.CancellationObserved.TrySetResult(true);
                throw;
            }
            return this.Result;
        }

        public void Release() => this.release.TrySetResult(true);
    }

    private sealed class CancellationIgnoringLifecyclePort : IBackendIdentityLifecyclePortV1
    {
        private readonly TaskCompletionSource<bool> release = NewSignal<bool>();

        public TaskCompletionSource<bool> RefreshStarted { get; } = NewSignal<bool>();

        public BackendSessionStepV1 Result { get; set; } =
            BackendSessionStepV1.Failed(BackendIdentityErrorV1.Timeout);

        public Task<BackendSessionStepV1> RecoverOrCreatePrePairSessionAsync(CancellationToken cancellationToken)
            => Task.FromResult(BackendSessionStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public Task<BackendPairingStepV1> PairOnceAsync(PairingCommand command, CancellationToken cancellationToken)
            => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public Task<BackendPairingStepV1> ReconcilePairingAsync(string operationId, CancellationToken cancellationToken)
            => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden));

        public async Task<BackendSessionStepV1> RefreshDefinitiveSessionAsync(
            string refreshToken,
            CancellationToken cancellationToken)
        {
            this.RefreshStarted.TrySetResult(true);
            await this.release.Task;
            return this.Result;
        }

        public void Release() => this.release.TrySetResult(true);
    }

    private static TestCoordinator NewAuthorizedCoordinator()
        => new()
        {
            Result = BackendDefinitiveSessionResult.Authorized(
                new BackendDefinitiveSession(
                    4,
                    "device-a",
                    "header.payload.signature",
                    DateTimeOffset.UtcNow.AddMinutes(5))),
        };

    private static async Task RunAuthenticatedListenerAsync(
        TestUiPipe pipe,
        TestCoordinator coordinator,
        SecurityIdentifier? ParentSid = null,
        IWnsRegistrationCoordinator? wnsRegistrationCoordinator = null,
        ILogger<UIMessageHandler>? logger = null)
    {
        var handler = new UIMessageHandler(
            onboardingStateService: null!,
            enforcementLevelQueryHandler: null!,
            scopeFactory: null!,
            logger: logger ?? NullLogger<UIMessageHandler>.Instance,
            wnsRegistrationCoordinator: wnsRegistrationCoordinator,
            realtimeIdentityAuthority: new BackendRealtimeIdentityAuthority(coordinator));
        using var stop = new CancellationTokenSource();
        using var listener = new NamedPipeUIServer.PipeServerListener(
            "red-ui-pipe",
            ParentSid ?? RealtimeIdentityPipeRedTests.ParentSid,
            childSid: null,
            handler,
            onDisconnected: static () => { },
            stop.Token,
            _ => Task.FromResult<NamedPipeUIServer.IUiPipeServer>(pipe),
            delay: null,
            logger: NullLogger.Instance);

        await listener.StartAsync();
    }

    private static byte[] Serialize(GetRealtimeIdentity request)
        => JsonSerializer.SerializeToUtf8Bytes(
            request,
            ControlParental.Domain.UIMessagesJsonContext.Default.GetRealtimeIdentity);

    private static byte[] Serialize(RegisterWnsChannel request)
        => JsonSerializer.SerializeToUtf8Bytes(
            request,
            ControlParental.Domain.UIMessagesJsonContext.Default.RegisterWnsChannel);

    private static RealtimeIdentityResponse DeserializeResponse(byte[] bytes)
        => JsonSerializer.Deserialize(
            bytes,
            ControlParental.Domain.UIMessagesJsonContext.Default.RealtimeIdentityResponse)!;

    private static RealtimeIdentityResponse ResponseForSize(int responseSize)
    {
        var probe = new RealtimeIdentityResponse(
            false,
            null,
            null,
            0,
            default,
            "x")
        {
            ContractVersion = 1,
            CorrelationId = "00000000-0000-4000-8000-000000000103",
        };
        var probeBytes = JsonSerializer.SerializeToUtf8Bytes(
            probe,
            ControlParental.Domain.UIMessagesJsonContext.Default.RealtimeIdentityResponse);
        var candidate = probe with { ErrorCode = new string('x', responseSize - probeBytes.Length + 1) };
        var candidateBytes = JsonSerializer.SerializeToUtf8Bytes(
            candidate,
            ControlParental.Domain.UIMessagesJsonContext.Default.RealtimeIdentityResponse);
        Assert.Equal(responseSize, candidateBytes.Length);
        return candidate;
    }

    private static string TokenWithLength(int tokenLength)
    {
        const string prefix = "header.";
        const string suffix = ".signature";
        Assert.True(tokenLength > prefix.Length + suffix.Length);
        return prefix + new string('x', tokenLength - prefix.Length - suffix.Length) + suffix;
    }

    private sealed class RecordingWnsRegistrationCoordinator : IWnsRegistrationCoordinator
    {
        public int RegisterCalls { get; private set; }

        public Task<WnsRegistrationResult> RegisterAsync(
            RegisterWnsChannel request,
            CancellationToken cancellationToken = default)
        {
            this.RegisterCalls++;
            return Task.FromResult(new WnsRegistrationResult(
                request.OperationId,
                WnsRegistrationStatus.Accepted,
                "pipe-correlation"));
        }

        public Task<WnsRegistrationResult?> ReconcileAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<WnsRegistrationResult?>(null);
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public StringBuilder Text { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            this.Text.Append(formatter(state, exception));
        }
    }

    private sealed class TestCoordinator : IBackendIdentityCoordinator
    {
        public BackendDefinitiveSessionResult Result { get; set; } =
            BackendDefinitiveSessionResult.Failed(BackendIdentityErrorV1.Forbidden);

        public int RefreshCalls { get; private set; }

        public BackendIdentityState CurrentState => BackendIdentityState.Unpaired();

        public Task<BackendPairingLifecycleResult> PairAsync(
            PairingCommand command,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new BackendPairingLifecycleResult(BackendIdentityErrorV1.Forbidden));

        public Task<BackendDefinitiveSessionResult> GetDefinitiveSessionAsync(
            CancellationToken cancellationToken = default)
        {
            this.RefreshCalls++;
            return Task.FromResult(this.Result);
        }
    }

    private sealed class TestUiPipe : NamedPipeUIServer.IUiPipeServer
    {
        private readonly byte[] request;
        private readonly SecurityIdentifier clientSid;
        private readonly bool stopAfterConnection;
        private readonly TaskCompletionSource<bool> listenerExit =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> emptyReadRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int connectionCount;
        private int readCount;

        public TestUiPipe(
            byte[] request,
            SecurityIdentifier? clientSid = null,
            bool stopAfterConnection = false)
        {
            this.request = request;
            this.clientSid = clientSid ?? ParentSid;
            this.stopAfterConnection = stopAfterConnection;
        }

        public List<byte[]> Writes { get; } = new();

        public TaskCompletionSource<bool> ConnectionStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsConnected { get; private set; } = true;

        public async Task WaitForConnectionAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref this.connectionCount) == 1)
            {
                this.ConnectionStarted.TrySetResult(true);
                return;
            }

            if (this.stopAfterConnection)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            await this.listenerExit.Task.WaitAsync(cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }

        public Task<int> ReadAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref this.readCount) == 1)
            {
                if (this.request.Length == 0)
                {
                    return this.ReadEmptyAsync(cancellationToken);
                }

                Buffer.BlockCopy(this.request, 0, buffer, 0, this.request.Length);
                return Task.FromResult(this.request.Length);
            }

            this.IsConnected = false;
            this.listenerExit.TrySetResult(true);
            return Task.FromResult(0);
        }

        private async Task<int> ReadEmptyAsync(CancellationToken cancellationToken)
        {
            await this.emptyReadRelease.Task.WaitAsync(cancellationToken);
            this.IsConnected = false;
            this.listenerExit.TrySetResult(true);
            return 0;
        }

        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            this.Writes.Add(buffer.ToArray());
            this.emptyReadRelease.TrySetResult(true);
            this.IsConnected = false;
            this.listenerExit.TrySetResult(true);
            return Task.CompletedTask;
        }

        public SecurityIdentifier GetImpersonationUserSid() => this.clientSid;

        public void Dispose() => this.IsConnected = false;
    }
}
