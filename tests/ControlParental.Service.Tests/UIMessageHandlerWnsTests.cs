// <copyright file="UIMessageHandlerWnsTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net;
using ControlParental.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

public sealed class UIMessageHandlerWnsTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private const string ValidUri = "https://db5.notify.windows.com/?token=redacted-fixture";
    private readonly string folder = Path.Combine(Path.GetTempPath(), $"wns-ipc-{Guid.NewGuid():N}");

    [Fact]
    public async Task AuthenticatedRequest_ValidPayload_UsesDefinitiveBackendAndRedactsResult()
    {
        var backend = Backend(success: true);
        var store = new SecretStoreWnsRegistrationIntentStore(CreateSecretStore(this.folder));
        var coordinator = Coordinator(store, backend.Object, definitive: true);
        var handler = CreateHandler(coordinator);

        var result = Assert.IsType<WnsRegistrationResult>(await handler.HandleAuthenticatedAsync(
            Request("op-valid")));

        Assert.Equal(WnsRegistrationStatus.Accepted, result.Status);
        Assert.Equal("op-valid", result.OperationId);
        Assert.DoesNotContain("notify.windows.com", result.ToString(), StringComparison.OrdinalIgnoreCase);
        backend.Verify(x => x.RegisterPushTokenAsync(ValidUri, "wns", Now.AddDays(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("http://db5.notify.windows.com/?token=x", "wns")]
    [InlineData("https://example.test/channel", "wns")]
    [InlineData("not-a-uri", "wns")]
    [InlineData("https://db5.notify.windows.com/?token=x", "other")]
    public async Task InvalidUriOrChannel_IsDeniedWithoutBackendCall(string uri, string channel)
    {
        var backend = Backend(success: true);
        var coordinator = Coordinator(new MemoryStore(), backend.Object, definitive: true);

        var result = await coordinator.RegisterAsync(new RegisterWnsChannel("op-invalid", uri, channel, Now.AddDays(1)));

        Assert.Equal(WnsRegistrationStatus.Denied, result.Status);
        backend.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(31)]
    public async Task InvalidExpiry_IsDenied(int days)
    {
        var coordinator = Coordinator(new MemoryStore(), Backend(true).Object, definitive: true);
        var result = await coordinator.RegisterAsync(Request("op-expiry") with { ExpiresAt = Now.AddDays(days) });
        Assert.Equal(WnsRegistrationStatus.Denied, result.Status);
    }

    [Fact]
    public async Task UnauthenticatedIpc_IsDeniedBeforeCoordinator()
    {
        var coordinator = new Mock<IWnsRegistrationCoordinator>(MockBehavior.Strict);
        var handler = CreateHandler(coordinator.Object);

        var result = Assert.IsType<WnsRegistrationResult>(await handler.HandleAsync(Request("op-auth")));

        Assert.Equal(WnsRegistrationStatus.Denied, result.Status);
        coordinator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthenticatedTriggerSync_IsAdmittedByScheduler_WithoutBackendAuthority()
    {
        var scheduler = new Mock<IScheduledWorkService>(MockBehavior.Strict);
        scheduler
            .Setup(x => x.AdmitSyncAsync(SyncTriggerSource.Wns, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SyncAdmissionResult.Accepted);
        var handler = CreateHandler(new Mock<IWnsRegistrationCoordinator>().Object, scheduler.Object);

        var result = Assert.IsType<StepCompletedResponse>(await handler.HandleAuthenticatedAsync(new TriggerSync()));

        Assert.True(result.Success);
        scheduler.Verify(x => x.AdmitSyncAsync(SyncTriggerSource.Wns, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnauthenticatedTriggerSync_IsRejectedWithoutSchedulerAdmission()
    {
        var scheduler = new Mock<IScheduledWorkService>(MockBehavior.Strict);
        var handler = CreateHandler(new Mock<IWnsRegistrationCoordinator>().Object, scheduler.Object);

        var result = Assert.IsType<StepCompletedResponse>(await handler.HandleAsync(new TriggerSync()));

        Assert.False(result.Success);
        scheduler.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(BackendIdentityPhase.Unpaired)]
    [InlineData(BackendIdentityPhase.PairingPending)]
    public async Task NonDefinitiveIdentity_IsDeniedButIntentRemainsPersisted(BackendIdentityPhase phase)
    {
        var backend = Backend(true);
        var store = new MemoryStore();
        var coordinator = Coordinator(store, backend.Object, definitive: false, phase);

        var result = await coordinator.RegisterAsync(Request("op-nondef"));

        Assert.Equal(WnsRegistrationStatus.Denied, result.Status);
        Assert.Equal("op-nondef", (await store.ReadAsync())!.OperationId);
        backend.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Duplicate_IsIdempotent_AndConflictingReuseIsDenied()
    {
        var backend = Backend(true);
        var coordinator = Coordinator(new MemoryStore(), backend.Object, definitive: true);

        var first = await coordinator.RegisterAsync(Request("op-duplicate"));
        var duplicate = await coordinator.RegisterAsync(Request("op-duplicate"));
        var conflict = await coordinator.RegisterAsync(Request("op-duplicate") with { ExpiresAt = Now.AddDays(2) });

        Assert.Equal(first, duplicate);
        Assert.Equal(WnsRegistrationStatus.Denied, conflict.Status);
        backend.Verify(x => x.RegisterPushTokenAsync(It.IsAny<string>(), "wns", It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OversizedPayload_IsDeniedAndSecretNeverAppearsInResult()
    {
        var secret = new string('s', 3000);
        var coordinator = Coordinator(new MemoryStore(), Backend(true).Object, definitive: true);

        var result = await coordinator.RegisterAsync(Request("op-large") with { ChannelUri = $"https://db5.notify.windows.com/?token={secret}" });

        Assert.Equal(WnsRegistrationStatus.Denied, result.Status);
        Assert.DoesNotContain(secret, result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_PropagatesWithoutRetryOrSuccess()
    {
        var backend = Backend(true);
        backend.Setup(x => x.RegisterPushTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var coordinator = Coordinator(new MemoryStore(), backend.Object, definitive: true);

        await Assert.ThrowsAsync<OperationCanceledException>(() => coordinator.RegisterAsync(Request("op-cancel")));
        backend.Verify(x => x.RegisterPushTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BackendTimeout_IsFiniteAndDoesNotOverlapAttempts()
    {
        var sends = 0;
        var handler = new StubHandler(async (_, token) =>
        {
            Interlocked.Increment(ref sends);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = CreateAuthenticatedClient(handler, maximumAttempts: 3, TimeSpan.FromMilliseconds(20));

        var result = await client.RegisterPushTokenAsync(ValidUri, "wns", Now.AddDays(1)).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(result.Success);
        Assert.Equal(PushTokenRegistrationFailureKind.RemoteUnavailable, result.FailureKind);
        Assert.Equal(1, sends);
    }

    [Fact]
    public async Task BackendTransientRetry_IsBoundedAndReusesIdempotencyKey()
    {
        var keys = new List<string?>();
        var handler = new StubHandler((request, _) =>
        {
            keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
            return Task.FromResult(new HttpResponseMessage(keys.Count == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Created));
        });
        var client = CreateAuthenticatedClient(handler, maximumAttempts: 2, TimeSpan.FromSeconds(1));

        var result = await client.RegisterPushTokenAsync(ValidUri, "wns", Now.AddDays(1));

        Assert.True(result.Success);
        Assert.Equal(2, keys.Count);
        Assert.Equal(keys[0], keys[1]);
    }

    [Fact]
    public async Task RetryableFailure_IsPersistedPendingWithoutErrorBodyDisclosure()
    {
        var backend = Backend(false, "sensitive backend body");
        var store = new MemoryStore();
        var coordinator = Coordinator(store, backend.Object, definitive: true);

        var result = await coordinator.RegisterAsync(Request("op-retry"));

        Assert.Equal(WnsRegistrationStatus.PendingOffline, result.Status);
        Assert.DoesNotContain("sensitive", result.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("op-retry", (await store.ReadAsync())!.OperationId);
    }

    [Theory]
    [InlineData(PushTokenRegistrationFailureKind.Revoked, WnsRegistrationStatus.Denied)]
    [InlineData(PushTokenRegistrationFailureKind.Forbidden, WnsRegistrationStatus.Denied)]
    [InlineData(PushTokenRegistrationFailureKind.RateLimited, WnsRegistrationStatus.Retryable)]
    public async Task TypedRemoteFailure_IsRedactedAndFailClosed(
        PushTokenRegistrationFailureKind failure,
        WnsRegistrationStatus expected)
    {
        var backend = Backend(false, "private response");
        backend.Setup(x => x.RegisterPushTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushTokenRegistrationResult.Failed("private response", failure));

        var result = await Coordinator(new MemoryStore(), backend.Object, definitive: true).RegisterAsync(Request("op-typed"));

        Assert.Equal(expected, result.Status);
        Assert.DoesNotContain("private", result.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestartReconcile_ReplaysLatestIntentOnce_WithoutPolling()
    {
        var store = new MemoryStore();
        var offline = Coordinator(store, Backend(false).Object, definitive: true);
        await offline.RegisterAsync(Request("op-restart"));
        var restoredBackend = Backend(true);
        var restored = Coordinator(store, restoredBackend.Object, definitive: true);

        var result = await restored.ReconcileAsync();

        Assert.Equal(WnsRegistrationStatus.Accepted, result!.Status);
        restoredBackend.Verify(x => x.RegisterPushTokenAsync(ValidUri, "wns", Now.AddDays(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestartReconcile_DoesNotReplayRevokedIntent()
    {
        var store = new MemoryStore();
        var revokedBackend = Backend(false);
        revokedBackend
            .Setup(x => x.RegisterPushTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushTokenRegistrationResult.Failed("redacted", PushTokenRegistrationFailureKind.Revoked));

        await Coordinator(store, revokedBackend.Object, definitive: true).RegisterAsync(Request("op-revoked"));

        var restoredBackend = Backend(true);
        var result = await Coordinator(store, restoredBackend.Object, definitive: true).ReconcileAsync();

        Assert.Null(result);
        restoredBackend.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FailedIntentWrite_DoesNotPublishPhantom_AndSameOperationRetriesDurably()
    {
        var store = new FailingThenSucceedStore();
        var backend = Backend(true);
        var coordinator = Coordinator(store, backend.Object, definitive: true);

        var first = await coordinator.RegisterAsync(Request("op-write"));

        Assert.Equal(WnsRegistrationStatus.Denied, first.Status);
        Assert.Null(await store.ReadAsync());

        var retry = await coordinator.RegisterAsync(Request("op-write"));

        Assert.Equal(WnsRegistrationStatus.Accepted, retry.Status);
        Assert.Equal("op-write", (await store.ReadAsync())!.OperationId);
        Assert.Equal(3, store.WriteCount);
        backend.Verify(x => x.RegisterPushTokenAsync(ValidUri, "wns", Now.AddDays(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestartReconcile_DeniedIntentCanBeReplacedByNewOperation()
    {
        var store = new MemoryStore();
        var revokedBackend = Backend(false);
        revokedBackend
            .Setup(x => x.RegisterPushTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PushTokenRegistrationResult.Failed("redacted", PushTokenRegistrationFailureKind.Revoked));

        await Coordinator(store, revokedBackend.Object, definitive: true).RegisterAsync(Request("op-old"));

        var acceptedBackend = Backend(true);
        var restored = Coordinator(store, acceptedBackend.Object, definitive: true);

        Assert.Null(await restored.ReconcileAsync());
        var result = await restored.RegisterAsync(Request("op-new"));

        Assert.Equal(WnsRegistrationStatus.Accepted, result.Status);
        Assert.Equal("op-new", (await store.ReadAsync())!.OperationId);
        acceptedBackend.Verify(x => x.RegisterPushTokenAsync(ValidUri, "wns", Now.AddDays(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RestartReconcile_DoesNotReplayAcceptedIntent()
    {
        var store = new MemoryStore();
        var initialBackend = Backend(true);
        await Coordinator(store, initialBackend.Object, definitive: true).RegisterAsync(Request("op-accepted"));

        var restoredBackend = Backend(true);
        Assert.Null(await Coordinator(store, restoredBackend.Object, definitive: true).ReconcileAsync());

        restoredBackend.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RestartReconcile_DoesNotSendPersistedExpiredIntent()
    {
        var store = new MemoryStore();
        await store.WriteAsync(new WnsRegistrationIntent(
            "op-expired",
            ValidUri,
            "wns",
            Now.AddMinutes(-1),
            WnsRegistrationStatus.PendingOffline));
        var backend = Backend(true);

        Assert.Null(await Coordinator(store, backend.Object, definitive: true).ReconcileAsync());

        backend.VerifyNoOtherCalls();
    }

    private static RegisterWnsChannel Request(string operationId) => new(operationId, ValidUri, "wns", Now.AddDays(1));

    private static Mock<IBackendClient> Backend(bool success, string error = "RemoteUnavailable")
    {
        var backend = new Mock<IBackendClient>(MockBehavior.Strict);
        backend.Setup(x => x.RegisterPushTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(success ? PushTokenRegistrationResult.Succeeded(Now.AddDays(1)) : PushTokenRegistrationResult.Failed(error));
        return backend;
    }

    private static BackendClient CreateAuthenticatedClient(HttpMessageHandler handler, int maximumAttempts, TimeSpan timeout)
    {
        var identity = new Mock<IBackendIdentityCoordinator>();
        identity.Setup(x => x.GetDefinitiveSessionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BackendDefinitiveSessionResult.Authorized(new BackendDefinitiveSession(7, "device-a", "symbolic-access", Now.AddHours(1))));
        return new BackendClient(
            new HttpClient(handler),
            "https://example.test",
            identity.Object,
            new BackendReliabilityOptions(timeout, maximumAttempts, (_, _) => TimeSpan.Zero, (_, _) => Task.CompletedTask));
    }

    private static WnsRegistrationCoordinator Coordinator(
        IWnsRegistrationIntentStore store,
        IBackendClient backend,
        bool definitive,
        BackendIdentityPhase phase = BackendIdentityPhase.Unpaired)
    {
        var identity = new Mock<IBackendIdentityCoordinator>();
        identity.SetupGet(x => x.CurrentState).Returns(definitive
            ? BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 7, "device-a")
            : phase == BackendIdentityPhase.Unpaired
                ? BackendIdentityState.Unpaired()
                : BackendIdentityState.Restore(phase, 7));
        return new WnsRegistrationCoordinator(store, backend, identity.Object, new FixedTimeProvider(Now));
    }

    private static UIMessageHandler CreateHandler(
        IWnsRegistrationCoordinator coordinator,
        IScheduledWorkService? scheduler = null)
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(x => x.CurrentIssues).Returns([]);
        var onboarding = new OnboardingStateService(Path.GetTempPath(), Mock.Of<IChildAccountStore>(), NullLogger<OnboardingStateService>.Instance);
        return new UIMessageHandler(onboarding, new EnforcementLevelQueryHandler(monitor.Object), services.GetRequiredService<IServiceScopeFactory>(), NullLogger<UIMessageHandler>.Instance, coordinator, scheduler);
    }

    private static SecretStore CreateSecretStore(string path) => new(path, new PassThroughProtector(), new NoOpAccessPolicy());

    public void Dispose()
    {
        if (Directory.Exists(this.folder)) Directory.Delete(this.folder, true);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : System.TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MemoryStore : IWnsRegistrationIntentStore
    {
        private WnsRegistrationIntent? value;
        public Task<WnsRegistrationIntent?> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(this.value);
        public Task<bool> WriteAsync(WnsRegistrationIntent intent, CancellationToken cancellationToken = default) { this.value = intent; return Task.FromResult(true); }
    }

    private sealed class FailingThenSucceedStore : IWnsRegistrationIntentStore
    {
        private WnsRegistrationIntent? value;
        private bool failNextWrite = true;

        public int WriteCount { get; private set; }

        public Task<WnsRegistrationIntent?> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(this.value);

        public Task<bool> WriteAsync(WnsRegistrationIntent intent, CancellationToken cancellationToken = default)
        {
            this.WriteCount++;
            if (this.failNextWrite)
            {
                this.failNextWrite = false;
                return Task.FromResult(false);
            }

            this.value = intent;
            return Task.FromResult(true);
        }
    }

    private sealed class PassThroughProtector : ICredentialProtector
    {
        public byte[] Protect(byte[] plaintext) => plaintext.ToArray();
        public byte[] Unprotect(byte[] protectedData) => protectedData.ToArray();
    }

    private sealed class NoOpAccessPolicy : ICredentialFileAccessPolicy
    {
        public void Apply(string path) { }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => send(request, cancellationToken);
    }
}
