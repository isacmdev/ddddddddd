namespace ControlParental.Service.Tests;

using System.Net;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

public sealed class IntegrityRuntimePathTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cp-integrity-{Guid.NewGuid():N}");

    [Fact]
    public async Task CanonicalAuthority_PreservesExactStateAcrossNonDefinitiveResultsAndCommitsAtDeadline()
    {
        var path = Path.Combine(this.directory, "issues.json");
        var now = DateTimeOffset.UtcNow;
        var localNow = now;
        var body = "{\"verdict\":\"revoked\"}";
        var status = HttpStatusCode.Created;
        string? token = null;
        string? notificationKey = null;
        var notifications = new Mock<IOutboxManager>();
        notifications.Setup(value => value.EnqueueAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        notifications.Setup(value => value.EnqueueIntegrityNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Callback<string, string, string, DateTimeOffset, string, CancellationToken>((_, _, _, _, key, _) => notificationKey = key).Returns(Task.CompletedTask);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage request, CancellationToken _) =>
            {
                token = request.Headers.Authorization?.Parameter;
                return new HttpResponseMessage(status) { Content = new StringContent(body) };
            });
        var clock = new MutableTimeProvider(now);
        var store = new MemoryIdentityStore(new(4, "device-a", "parent-a", "token-a", "refresh-a", now.AddMinutes(10)));
        var coordinator = new BackendIdentityCoordinator(new IdentityPort(), store, clock);
        await coordinator.InitializeAsync();
        using var client = new HttpClient(handler.Object);
        var backend = new BackendClient(client, "https://example.test", coordinator);
        var checker = Mock.Of<IIntegrityChecker>(value => value.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>()) == Task.FromResult(new IntegrityCheckResult(false, "hash", "agent.exe")));
        using var policy = new IntegrityVerdictHandler(notifications.Object, clock: () => localNow);
        policy.SetServiceStartTime(now.AddMinutes(-10));
        using var metadataPolicy = new IntegrityVerdictHandler(clock: () => localNow);
        metadataPolicy.SetServiceStartTime(now.AddMinutes(-10));
        var metadataThird = metadataPolicy.HandleVerdictDecision("revoked", true, now);
        metadataPolicy.HandleVerdictDecision("revoked", true, now);
        var metadataEscalation = metadataPolicy.HandleVerdictDecision("revoked", true, now);
        metadataThird.Notification.Should().BeNull();
        metadataEscalation.Notification.Should().NotBeNull();
        metadataEscalation.Notification!.Type.Should().Be("integrity_degrade_pending");
        metadataEscalation.NotificationIdempotencyKey.Should().Contain($"/{metadataEscalation.Epoch}/{metadataEscalation.Sequence}/notification");
        metadataPolicy.HandleVerdictDecision("revoked", true, now).Notification.Should().BeNull();
        metadataPolicy.HandleVerdictDecision("trust", true, now);
        metadataPolicy.HandleVerdictDecision("revoked", true, now);
        metadataPolicy.HandleVerdictDecision("revoked", true, now);
        var freshEscalation = metadataPolicy.HandleVerdictDecision("revoked", true, now);
        freshEscalation.Notification.Should().NotBeNull();
        freshEscalation.NotificationIdempotencyKey.Should().NotBe(metadataEscalation.NotificationIdempotencyKey);
        using var enforcement = CreateEnforcement(new FileIssueStore(path));
        await RunOnce(backend, checker, enforcement, policy, coordinator, localClock: () => localNow, outbox: notifications.Object);
        await RunOnce(backend, checker, enforcement, policy, coordinator, localClock: () => localNow, outbox: notifications.Object);
        await RunOnce(backend, checker, enforcement, policy, coordinator, localClock: () => localNow, outbox: notifications.Object);

        await RunOnce(backend, checker, enforcement, policy, coordinator, localClock: () => localNow, outbox: notifications.Object);
        localNow = now.AddMinutes(5).AddTicks(-1);
        await RunOnce(backend, checker, enforcement, policy, coordinator, localClock: () => localNow, outbox: notifications.Object);
        Assert.Contains(await new FileIssueStore(path).LoadAsync(), value => value.IsActive && value.Key.IdentityScope == "device-a" && value.Severity == EnforcementIssueSeverity.Warning);
        localNow = now.AddMinutes(5);
        await RunOnce(backend, checker, enforcement, policy, coordinator, localClock: () => localNow, outbox: notifications.Object);
        Assert.Contains(await new FileIssueStore(path).LoadAsync(), value => value.IsActive && value.Key.IdentityScope == "device-a" && value.Severity == EnforcementIssueSeverity.Severe);
        await RunOnce(backend, checker, enforcement, policy, coordinator, localClock: () => localNow, outbox: notifications.Object);
        notifications.Verify(value => value.EnqueueIntegrityNotificationAsync("integrity_degrade_pending", "Integrity Degradation Pending", It.IsAny<string>(), It.IsAny<DateTimeOffset>(), "integrity/device-a/integrity-binary/6/6/notification", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("integrity/device-a/integrity-binary/6/6/notification", notificationKey);
        notifications.VerifyNoOtherCalls();

        var issue = Assert.Single(await new FileIssueStore(path).LoadAsync(), value => value.IsActive && value.Key.IdentityScope == "device-a");
        var preRefreshKey = issue.Key;
        Assert.Equal(new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", "device-a"), issue.Key);
        Assert.Equal("token-a", token);
        Assert.NotEqual(preRefreshKey, new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", "device-b"));
        await RunOnce(backend, checker, enforcement, policy, coordinator);
        Assert.Single(await new FileIssueStore(path).LoadAsync(), value => value.IsActive && value.Key == preRefreshKey);
        await enforcement.AddIssueAsync(new IssueKey(0, EnforcementIssueType.HookTimeout, "unrelated", "device-a"), EnforcementIssueSeverity.Warning, "unrelated");
        await enforcement.AddIssueAsync(new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", "device-b"), EnforcementIssueSeverity.Severe, "other device");
        var before = JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync());

        foreach (var payload in new[] { "{\"verdict\":\"unknown\"}", "{\"verdict\":\"pending\"}", "{}", "{\"verdict\":" })
        {
            body = payload;
            await RunOnce(backend, checker, enforcement, policy, coordinator);
            Assert.Equal(before, JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync()));
        }
        status = HttpStatusCode.ServiceUnavailable;
        await RunOnce(backend, checker, enforcement, policy, coordinator);
        Assert.Equal(before, JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync()));
        await RunOnce(backend, checker, enforcement, policy, coordinator);
        Assert.Equal(before, JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync()));
    }

    [Fact]
    public async Task CanonicalAuthority_RefreshTimeoutPreservesExactPhysicalSnapshot()
    {
        var path = Path.Combine(this.directory, "stale.json");
        var now = DateTimeOffset.UtcNow;
        var store = new MemoryIdentityStore(new(4, "device-a", "parent-a", "token-a", "refresh-a", now.AddSeconds(1)));
        var port = new IdentityPort { BlockRefresh = true };
        var coordinator = new BackendIdentityCoordinator(port, store, refreshTimeout: TimeSpan.FromMilliseconds(20));
        await coordinator.InitializeAsync();
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"verdict\":\"trust\"}") });
        using var client = new HttpClient(handler.Object);
        var backend = new BackendClient(client, "https://example.test", coordinator);
        using var policy = new IntegrityVerdictHandler();
        policy.SetServiceStartTime(now.AddMinutes(-10));
        using var enforcement = CreateEnforcement(new FileIssueStore(path));
        await enforcement.AddIssueAsync(new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", "device-a"), EnforcementIssueSeverity.Severe, "existing");
        var before = JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync());
        await RunOnce(backend, Mock.Of<IIntegrityChecker>(value => value.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>()) == Task.FromResult(new IntegrityCheckResult(true, "hash", "agent.exe"))), enforcement, policy, coordinator);
        Assert.Equal(before, JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync()));
    }

    [Theory]
    [InlineData("unknown", "{\"verdict\":\"unknown\"}", 201)]
    [InlineData("pending", "{\"verdict\":\"pending\"}", 201)]
    [InlineData("absent", "{}", 201)]
    [InlineData("malformed", "{\"verdict\":", 201)]
    [InlineData("transport", "", 503)]
    public async Task CanonicalAuthority_NonDefinitiveCase_PreservesExactPhysicalSnapshot(string caseName, string responseBody, int statusCode)
    {
        var path = Path.Combine(this.directory, $"{caseName}.json");
        var store = new MemoryIdentityStore(new(4, "device-a", "parent-a", "token-a", "refresh-a", DateTimeOffset.UtcNow.AddMinutes(10)));
        var coordinator = new BackendIdentityCoordinator(new IdentityPort(), store);
        await coordinator.InitializeAsync();
        var body = "{\"verdict\":\"revoked\"}";
        var status = HttpStatusCode.Created;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() => new HttpResponseMessage(status) { Content = new StringContent(body) });
        var backend = new BackendClient(new HttpClient(handler.Object), "https://example.test", coordinator);
        using var policy = new IntegrityVerdictHandler();
        policy.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        using var enforcement = CreateEnforcement(new FileIssueStore(path));
        var checker = Mock.Of<IIntegrityChecker>(value => value.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>()) == Task.FromResult(new IntegrityCheckResult(false, "hash", "agent.exe")));
        await RunOnce(backend, checker, enforcement, policy, coordinator);
        var before = JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync());
        body = responseBody;
        status = (HttpStatusCode)statusCode;
        await RunOnce(backend, checker, enforcement, policy, coordinator);
        Assert.Equal(before, JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync()));
    }

    [Fact]
    public async Task CanonicalAuthority_StaleIdentityGenerationPreservesExactPhysicalSnapshot()
    {
        var path = Path.Combine(this.directory, "stale-generation.json");
        var store = new MemoryIdentityStore(new(4, "device-a", "parent-a", "token-a", "refresh-a", DateTimeOffset.UtcNow.AddMinutes(10)));
        var coordinator = new BackendIdentityCoordinator(new IdentityPort(), store);
        await coordinator.InitializeAsync();
        var responseReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).Returns(async () => { responseReady.SetResult(); await release.Task; return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{\"verdict\":\"trust\"}") }; });
        var backend = new BackendClient(new HttpClient(handler.Object), "https://example.test", coordinator);
        using var policy = new IntegrityVerdictHandler();
        policy.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        using var enforcement = CreateEnforcement(new FileIssueStore(path));
        var key = new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", "device-a");
        await enforcement.AddIssueAsync(key, EnforcementIssueSeverity.Severe, "existing");
        var before = JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync());
        var run = RunOnce(backend, Mock.Of<IIntegrityChecker>(value => value.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>()) == Task.FromResult(new IntegrityCheckResult(true, "hash", "agent.exe"))), enforcement, policy, coordinator);
        await responseReady.Task;
        await coordinator.InvalidateAsync(4, BackendIdentityErrorV1.Revoked);
        release.SetResult();
        await run;
        Assert.Equal(before, JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync()));
    }

    [Fact]
    public async Task CanonicalAuthority_CallerCancellationPreservesExactPhysicalSnapshot()
    {
        var path = Path.Combine(this.directory, "cancelled.json");
        var store = new MemoryIdentityStore(new(4, "device-a", "parent-a", "token-a", "refresh-a", DateTimeOffset.UtcNow.AddMinutes(10)));
        var coordinator = new BackendIdentityCoordinator(new IdentityPort(), store);
        await coordinator.InitializeAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).Returns<HttpRequestMessage, CancellationToken>(async (_, requestToken) =>
        {
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, requestToken); } catch (OperationCanceledException) when (requestToken.IsCancellationRequested) { stopped.SetResult(); throw; }
            return null!;
        });
        var backend = new BackendClient(new HttpClient(handler.Object), "https://example.test", coordinator);
        using var cancellation = new CancellationTokenSource();
        var key = new IssueKey(0, EnforcementIssueType.BinaryIntegrityFailure, "integrity/binary", "device-a");
        var fileStore = new FileIssueStore(path);
        await fileStore.UpsertActiveAsync(key, EnforcementIssueSeverity.Severe, "existing", DateTimeOffset.UtcNow);
        var before = JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync());
        var request = backend.ReportIntegrityAsync(new IntegrityReport { ReportHash = "report", BinaryHash = "hash", SignatureValid = true, Timestamp = DateTimeOffset.UtcNow, AgentVersion = "test", Platform = "test" }, cancellation.Token);
        await started.Task;
        cancellation.Cancel();
        await stopped.Task;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(before, JsonSerializer.Serialize(await new FileIssueStore(path).LoadAsync()));
    }

    private static async Task RunOnce(IBackendClient backend, IIntegrityChecker checker, IEnforcementLevelMonitor enforcement, IIntegrityVerdictHandler policy, IBackendIdentityCoordinator identity, CancellationToken cancellationToken = default, Func<DateTimeOffset>? localClock = null, IOutboxManager? outbox = null)
    {
        var acceptedAt = localClock?.Invoke() ?? DateTimeOffset.UtcNow;
        using var monitor = new AntiTamperMonitor(Mock.Of<ITimeProvider>(value => value.WallClockNow == acceptedAt && value.MonotonicNow == 1), outbox ?? Mock.Of<IOutboxManager>(), Mock.Of<IPrivilegeInspector>(value => value.IsChildStandardAsync(It.IsAny<CancellationToken>()) == Task.FromResult(true)), enforcement, checker, backend, policy, identityCoordinator: identity);
        await monitor.StartAsync(cancellationToken);
        await monitor.StopAsync();
    }

    private static EnforcementLevelMonitor CreateEnforcement(IIssueStore store)
        => new(Mock.Of<IPrivilegeInspector>(), Mock.Of<IScmController>(), Mock.Of<IServiceHealthMonitor>(value => value.IsAgentHealthy == true), Mock.Of<ITimeProvider>(value => value.WallClockNow == DateTimeOffset.UtcNow), issueStore: store);

    public void Dispose() { if (Directory.Exists(this.directory)) Directory.Delete(this.directory, true); }

    private sealed class MemoryIdentityStore(BackendIdentityCredentialSnapshot? snapshot) : IBackendIdentityCredentialStore
    {
        public BackendIdentityCredentialSnapshot? Snapshot { get; set; } = snapshot;
        public Task<IdentityStoreResult> ReadIdentityAsync(CancellationToken cancellationToken = default) => Task.FromResult(new IdentityStoreResult(this.Snapshot is null ? IdentityStoreStatus.NotFound : IdentityStoreStatus.Found, this.Snapshot, null));
        public Task<IdentityStoreResult> WriteIdentityAsync(BackendIdentityCredentialSnapshot value, CancellationToken cancellationToken = default) { this.Snapshot = value; return Task.FromResult(new IdentityStoreResult(IdentityStoreStatus.Found, value, null)); }
        public Task<bool> InvalidateIdentityAsync(long generation, CancellationToken cancellationToken = default) { if (this.Snapshot?.Generation == generation) this.Snapshot = null; return Task.FromResult(true); }
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : System.TimeProvider
    {
        private DateTimeOffset current = now;
        public void Advance(TimeSpan amount) => this.current += amount;
        public override DateTimeOffset GetUtcNow() => this.current;
    }

    private sealed class IdentityPort : IBackendIdentityLifecyclePortV1
    {
        public bool BlockRefresh { get; init; }
        public Task<BackendSessionStepV1> RecoverOrCreatePrePairSessionAsync(CancellationToken cancellationToken) => Task.FromResult(BackendSessionStepV1.Failed(BackendIdentityErrorV1.Forbidden));
        public Task<BackendPairingStepV1> PairOnceAsync(PairingCommand command, CancellationToken cancellationToken) => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden));
        public Task<BackendPairingStepV1> ReconcilePairingAsync(string operationId, CancellationToken cancellationToken) => Task.FromResult(BackendPairingStepV1.Failed(BackendIdentityErrorV1.Forbidden));
        public async Task<BackendSessionStepV1> RefreshDefinitiveSessionAsync(string refreshToken, CancellationToken cancellationToken)
        {
            if (this.BlockRefresh) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return BackendSessionStepV1.Definitive("token-a-refreshed", "refresh-a-2", DateTimeOffset.UtcNow.AddHours(1), new BackendIdentityClaimV1("device-a"));
        }
    }

}
