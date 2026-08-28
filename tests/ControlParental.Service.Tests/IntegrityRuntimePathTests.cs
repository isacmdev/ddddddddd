namespace ControlParental.Service.Tests;

using System.Net;
using System.Text.Json;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Moq.Protected;
using Xunit;

public sealed class IntegrityRuntimePathTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"cp-integrity-{Guid.NewGuid():N}");

    [Fact]
    public void ProductionHostedComposition_OrdersIdentityControlAndWnsExactlyOnce()
    {
        var services = new ServiceCollection(); Program.ConfigureProductionHostedServices(services, new("https://example.test", "anon-key"), new(null), httpClientFactory: static () => new HttpClient());
        var hosted = services.Where(value => value.ServiceType == typeof(IHostedService)).ToArray(); hosted.Should().HaveCount(3); hosted.Select(value => value.ImplementationType).Should().Equal(typeof(BackendIdentityStartupService), typeof(ControlParentalService), typeof(WnsRegistrationReconciliationService));
    }
    [Fact]
    public async Task PairedStartup_WaitsForRehydrationBeforeWnsOrLaterHostedWork()
    {
        var identity = new Mock<IBackendIdentityCoordinator>(); identity.SetupGet(value => value.CurrentState).Returns(BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 4, "device-a"));
        var rehydration = new BlockingIntegrityStateStore(); var factory = CreateFactory(identity.Object, rehydration); var control = CreateService(CreateHealth().Object, factory: factory);
        var wns = new Mock<IWnsRegistrationCoordinator>(); var wnsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        wns.Setup(value => value.ReconcileAsync(It.IsAny<CancellationToken>())).Callback(() => wnsStarted.TrySetResult()).ReturnsAsync((WnsRegistrationResult?)null); var later = new HostedStartProbe();
        using var host = new HostBuilder().ConfigureServices((_, services) => { services.AddSingleton<IHostedService>(control); services.AddSingleton<IHostedService>(new WnsRegistrationReconciliationService(wns.Object)); services.AddSingleton<IHostedService>(later); }).Build();
        var start = host.StartAsync(); await rehydration.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var monitor = Assert.IsType<AntiTamperMonitor>(factory.CreatedMonitor); var generation = typeof(AntiTamperMonitor).GetField("generation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(monitor)!;
        start.IsCompleted.Should().BeFalse(); wnsStarted.Task.IsCompleted.Should().BeFalse(); later.Started.Should().BeFalse(); rehydration.Release.TrySetResult(); await start.WaitAsync(TimeSpan.FromSeconds(5));
        wnsStarted.Task.IsCompleted.Should().BeTrue(); later.Started.Should().BeTrue(); await host.StopAsync(); control.Dispose(); control.Dispose();
        foreach (var resource in new[] { "CancellationDisposals", "GateDisposals", "TimezoneTimerDisposals" }) ((int)generation.GetType().GetField(resource, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(generation)!).Should().Be(1);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => monitor.StartAsync());
    }
    [Fact]
    public async Task IntegrityRuntimeFactory_UsesCapturedDeviceScopeWhenIdentityChangesBeforeRehydrate()
    {
        var identity = new Mock<IBackendIdentityCoordinator>(); var current = BackendIdentityState.Restore(BackendIdentityPhase.DefinitiveSession, 4, "device-a"); identity.SetupGet(value => value.CurrentState).Returns(() => current);
        var enforcement = new Mock<IEnforcementLevelMonitor>(); enforcement.Setup(value => value.ResolveIssueAsync(It.IsAny<IssueKey>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var state = new IntegrityEscalationState("device-a", 1, IntegrityEscalationState.CurrentSchemaVersion, 1, 1, 0, 2, EscalationPhase.Degraded, null, null, DateTimeOffset.UtcNow.AddMinutes(-1), true, true, true, "reaction", null, null, null)
        {
            PendingEffectDescriptor = new(
                IntegrityEscalationEffectDescriptor.CurrentVersion,
                IntegrityEscalationReactionKind.ResolveIssue,
                null,
                "authoritative backend trust verdict",
                null,
                null,
                null,
                null),
        };
        var store = new BlockingIntegrityStateStore(state); using var monitor = Assert.IsType<AntiTamperMonitor>(CreateFactory(identity.Object, store, enforcement.Object).Create()); var start = monitor.StartAsync();
        await store.Started.Task.WaitAsync(TimeSpan.FromSeconds(5)); current = BackendIdentityState.Unpaired(); store.Release.TrySetResult(); await start.WaitAsync(TimeSpan.FromSeconds(5));
        store.Identities.Should().Equal("device-a"); store.SavedScopes.Should().NotBeEmpty(); store.SavedScopes.Should().OnlyContain(value => value == "device-a"); enforcement.Verify(value => value.ResolveIssueAsync(It.Is<IssueKey>(key => key.IdentityScope == "device-a"), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once); enforcement.Invocations.Should().OnlyContain(value => value.Arguments.OfType<IssueKey>().Single().IdentityScope == "device-a");
        await monitor.StopAsync();
    }
    [Fact]
    public async Task UnpairedStartup_StartsHealthAndLocalMonitorOnceButNoRuntimeMonitor()
    {
        var identity = new Mock<IBackendIdentityCoordinator>(); identity.SetupGet(value => value.CurrentState).Returns(BackendIdentityState.Unpaired()); var health = CreateHealth();
        var enforcement = new Mock<IEnforcementLevelMonitor>(); enforcement.Setup(value => value.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask); enforcement.Setup(value => value.StopAsync()).Returns(Task.CompletedTask);
        var factory = CreateFactory(identity.Object, Mock.Of<IIntegrityEscalationStateStore>()); var service = CreateService(health.Object, enforcement.Object, factory); await service.StartAsync(CancellationToken.None);
        factory.CreatedMonitor.Should().BeNull(); health.Verify(value => value.StartAsync(It.IsAny<CancellationToken>()), Times.Once); enforcement.Verify(value => value.StartAsync(It.IsAny<CancellationToken>()), Times.Once); await service.StopAsync(CancellationToken.None); service.Dispose();
    }
    [Theory]
    [InlineData("health")]
    [InlineData("enforcement")]
    public async Task StartupFailure_IsPropagatedWithoutHanging(string stage)
    {
        var failure = new InvalidOperationException(stage); var health = CreateHealth(); var enforcement = new Mock<IEnforcementLevelMonitor>();
        enforcement.Setup(value => value.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask); enforcement.Setup(value => value.StopAsync()).Returns(Task.CompletedTask);
        if (stage == "health") health.Setup(value => value.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException(failure)); else enforcement.Setup(value => value.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.FromException(failure));
        var identity = Mock.Of<IBackendIdentityCoordinator>(value => value.CurrentState == BackendIdentityState.Unpaired()); using var service = CreateService(health.Object, enforcement.Object, CreateFactory(identity, Mock.Of<IIntegrityEscalationStateStore>()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5)));
    }
    [Fact]
    public async Task StopAsync_ClosesBindingAdmissionBeforeSessionStopAndDrainsWinner()
    {
        var health = CreateHealth(); var healthStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        health.Setup(value => value.StopAsync()).Callback(() => healthStopped.TrySetResult()).Returns(Task.CompletedTask);
        var usage = new Mock<IUsageAccumulator>(); var reconciler = new Mock<IUsageReconciler>(); var service = CreateService(health.Object, usage: usage.Object, reconciler: reconciler.Object);
        var gate = GetPrivateField<SemaphoreSlim>(service, "bindingGate"); var channel = new DisconnectingChannel(() => gate.Wait(0));
        SessionManager? session = null;
        var launcher = new AgentLauncher("ControlParental.SessionAgent.exe", "SessionAgent", new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null), _ => true, _ => { }, () => Disconnect(session!), _ => new IntPtr(1), _ => channel, new ImmediateProcessLaunchApi());
        session = new SessionManager(string.Empty, "ControlParental.SessionAgent.exe", "SessionAgent", _ => { }, _ => { }, _ => { }, launcher, value => QueueBinding(service, value));
        SetPrivateField(service, "sessionManager", session); var safety = new SessionSafetyLoop(7, (_, _) => Task.FromResult(new EnforcementResult { Success = true, Blocked = false, Timestamp = DateTimeOffset.UtcNow }), (_, _) => Task.FromResult(ActionStatus.HarmlessAbsence), new FileOverlayIntentStore(Path.Combine(this.directory, "stop-intent.json")), Mock.Of<IAuthoritativeHealthSink>()); SetPrivateField(service, "safetyLoop", safety);
        await session.OnSessionStarted(7);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var admitted = WaitForReleaseAsync(reached, release.Task); SetPrivateField(service, "bindingTask", admitted);
        var firstStop = service.StopAsync(CancellationToken.None); await healthStopped.Task; firstStop.IsCompleted.Should().BeFalse(); release.TrySetResult(); await firstStop;
        service.StopAsync(CancellationToken.None).Should().BeSameAs(firstStop); GetPrivateField<Task>(service, "bindingTask").Should().BeSameAs(admitted); await admitted;
        health.Verify(value => value.StopAsync(), Times.Once); usage.Verify(value => value.Stop(), Times.Once); reconciler.Verify(value => value.Stop(), Times.Once); channel.StopCount.Should().Be(1); channel.DisposeCount.Should().Be(1); channel.DisconnectedCount.Should().Be(1); GetPrivateField<int>(GetPrivateField<SessionEnforcementCoordinator>(safety, "coordinator"), "disposed").Should().Be(1); Assert.Throws<ObjectDisposedException>(() => gate.Wait(0)); service.Dispose();
    }
    [Fact]
    public async Task EarlyStop_IsNullSafeAndRunsCleanupOnce()
    {
        var health = CreateHealth(); var usage = new Mock<IUsageAccumulator>(); var reconciler = new Mock<IUsageReconciler>(); var service = CreateService(health.Object, usage: usage.Object, reconciler: reconciler.Object);
        await service.StopAsync(CancellationToken.None); await service.StopAsync(CancellationToken.None); service.Dispose(); service.Dispose(); health.Verify(value => value.StopAsync(), Times.Once); usage.Verify(value => value.Stop(), Times.Once); reconciler.Verify(value => value.Stop(), Times.Once);
    }
    private static Mock<IServiceHealthMonitor> CreateHealth()
    {
        var health = new Mock<IServiceHealthMonitor>(); health.Setup(value => value.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask); health.Setup(value => value.StopAsync()).Returns(Task.CompletedTask); return health;
    }
    private static IntegrityRuntimeFactory CreateFactory(IBackendIdentityCoordinator identity, IIntegrityEscalationStateStore store, IEnforcementLevelMonitor? enforcement = null)
        => new(Mock.Of<ITimeProvider>(), Mock.Of<IOutboxManager>(), Mock.Of<IPrivilegeInspector>(value => value.IsChildStandardAsync(It.IsAny<CancellationToken>()) == Task.FromResult(true)), enforcement ?? Mock.Of<IEnforcementLevelMonitor>(), Mock.Of<IIntegrityChecker>(value => value.CheckLocalIntegrityAsync(It.IsAny<CancellationToken>()) == Task.FromResult(new IntegrityCheckResult(true, "hash", "agent.exe"))), Mock.Of<IBackendClient>(value => value.ReportIntegrityAsync(It.IsAny<IntegrityReport>(), It.IsAny<CancellationToken>()) == Task.FromResult(new IntegrityReportResult(false, null))), identity, store);
    private static ControlParentalService CreateService(IServiceHealthMonitor health, IEnforcementLevelMonitor? enforcement = null, IntegrityRuntimeFactory? factory = null, IUsageAccumulator? usage = null, IUsageReconciler? reconciler = null)
        => new(Mock.Of<IScmController>(), Mock.Of<IPrivilegeInspector>(), Mock.Of<IAccountManager>(), usage ?? Mock.Of<IUsageAccumulator>(), reconciler ?? Mock.Of<IUsageReconciler>(), Mock.Of<IWorkstationLockManager>(), Mock.Of<IOverlayPersistenceManager>(), health, Mock.Of<IServiceRecoveryManager>(), Mock.Of<ITimeProvider>(), Mock.Of<IPolicyRepository>(), Mock.Of<IProcessTerminator>(), enforcementLevelMonitor: enforcement, integrityRuntimeFactory: factory);
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

    [Fact]
    public async Task StopAsync_AntiTamperFailureStillCompletesAllCleanupAndCachesFailure()
    {
        var failure = new InvalidOperationException("anti-tamper stop failed");
        var health = CreateHealth();
        var enforcement = new Mock<IEnforcementLevelMonitor>();
        enforcement.Setup(value => value.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        enforcement.Setup(value => value.StopAsync()).Returns(Task.CompletedTask);
        var usage = new Mock<IUsageAccumulator>();
        usage.Setup(value => value.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        usage.Setup(value => value.RequestBackfillAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var reconciler = new Mock<IUsageReconciler>();
        reconciler.Setup(value => value.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var monitor = new Mock<IAntiTamperMonitor>();
        monitor.Setup(value => value.StopAsync()).ThrowsAsync(failure);
        var channel = new RecordingChannel();
        var launcher = new AgentLauncher(
            "ControlParental.SessionAgent.exe",
            "SessionAgent",
            new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
            _ => true,
            _ => { },
            () => { },
            _ => new IntPtr(1),
            _ => channel,
            new ImmediateProcessLaunchApi());
        var session = new SessionManager(string.Empty, "ControlParental.SessionAgent.exe", "SessionAgent", _ => { }, _ => { }, _ => { }, launcher);
        await session.OnSessionStarted(7);
        var safety = new SessionSafetyLoop(7, (_, _) => Task.FromResult(new EnforcementResult { Success = true, Blocked = false, Timestamp = DateTimeOffset.UtcNow }), (_, _) => Task.FromResult(ActionStatus.HarmlessAbsence), new FileOverlayIntentStore(Path.Combine(this.directory, "intent.json")), Mock.Of<IAuthoritativeHealthSink>());
        var service = CreateService(health.Object, enforcement.Object, usage: usage.Object, reconciler: reconciler.Object);
        SetPrivateField(service, "antiTamperMonitor", monitor.Object);
        SetPrivateField(service, "ownsAntiTamperMonitor", true);
        SetPrivateField(service, "sessionManager", session);
        SetPrivateField(service, "safetyLoop", safety);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reached = new[] { new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously), new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        SetPrivateField(service, "bindingTask", WaitForReleaseAsync(reached[0], release.Task));
        SetPrivateField(service, "recoveryTask", WaitForReleaseAsync(reached[1], release.Task));
        SetPrivateField(service, "timeChangeTask", WaitForReleaseAsync(reached[2], release.Task));
        await service.StartAsync(CancellationToken.None);
        var executeTask = (Task)typeof(Microsoft.Extensions.Hosting.BackgroundService).GetProperty("ExecuteTask")!.GetValue(service)!;
        var firstStop = service.StopAsync(CancellationToken.None);
        await Task.WhenAll(reached.Select(value => value.Task));
        release.TrySetResult();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(() => firstStop);
        observed.Should().BeSameAs(failure);
        var secondStop = service.StopAsync(CancellationToken.None);
        secondStop.Should().BeSameAs(firstStop);
        (await Assert.ThrowsAsync<InvalidOperationException>(() => secondStop)).Should().BeSameAs(failure);
        executeTask.IsCompleted.Should().BeTrue();
        health.Verify(value => value.StopAsync(), Times.Once);
        enforcement.Verify(value => value.StopAsync(), Times.Once);
        monitor.Verify(value => value.StopAsync(), Times.Once);
        usage.Verify(value => value.Stop(), Times.Once);
        reconciler.Verify(value => value.Stop(), Times.Once);
        channel.StopCount.Should().Be(1);
        channel.DisposeCount.Should().Be(1);
        var coordinator = GetPrivateField<SessionEnforcementCoordinator>(safety, "coordinator");
        GetPrivateField<int>(coordinator, "disposed").Should().Be(1);
        var gate = GetPrivateField<SemaphoreSlim>(service, "bindingGate");
        Assert.Throws<ObjectDisposedException>(() => gate.Wait(0));
        service.Dispose();
        monitor.Verify(value => value.Dispose(), Times.Once);
    }
    private static async Task WaitForReleaseAsync(TaskCompletionSource reached, Task release)
    {
        reached.TrySetResult();
        await release;
    }
    private static void Disconnect(SessionManager session)
        => typeof(SessionManager).GetMethod("OnAgentDisconnected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(session, new object?[] { 7 });
    private static void QueueBinding(ControlParentalService service, IIpcChannel? channel)
        => typeof(ControlParentalService).GetMethod("QueueAgentBinding", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(service, new object?[] { channel, CancellationToken.None });
    private static void SetPrivateField<T>(object instance, string name, T value)
        => instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(instance, value);
    private static T GetPrivateField<T>(object instance, string name)
        => (T)instance.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(instance)!;
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

    private sealed class HostedStartProbe : IHostedService
    { public bool Started { get; private set; } public Task StartAsync(CancellationToken cancellationToken) { this.Started = true; return Task.CompletedTask; } public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask; }
    private sealed class BlockingIntegrityStateStore : IIntegrityEscalationStateStore
    {
        public BlockingIntegrityStateStore(IntegrityEscalationState? state = null) => this.State = state;
        public IntegrityEscalationState? State { get; private set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public List<string> Identities { get; } = []; public List<string> SavedScopes { get; } = [];
        public async Task<IntegrityEscalationState?> LoadAsync(string identity, CancellationToken cancellationToken = default) { this.Identities.Add(identity); this.Started.TrySetResult(); await this.Release.Task.WaitAsync(cancellationToken); return this.State; }
        public Task SaveAsync(IntegrityEscalationStateEnvelope value, CancellationToken cancellationToken = default) { this.SavedScopes.Add(value.State.IdentityScope); this.State = value.State; return Task.CompletedTask; }
    }
    private sealed class DisconnectingChannel(Action onStop) : IIpcChannel, IDisposable
    {
        private Action? disconnected;
        public bool IsConnected { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int DisconnectedCount { get; private set; }
        public event Action? Disconnected { add => this.disconnected += value; remove { } }
        public event Action<IIpcMessage>? MessageReceived { add { } remove { } }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync() { this.StopCount++; onStop(); this.disconnected?.Invoke(); this.DisconnectedCount++; return Task.CompletedTask; }
        public Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() => this.DisposeCount++;
    }
    private sealed class RecordingChannel : IIpcChannel, IDisposable
    {
        public bool IsConnected { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public event Action? Disconnected;
        public event Action<IIpcMessage>? MessageReceived;
        public Task StartAsync(CancellationToken cancellationToken = default) { this.IsConnected = true; return Task.CompletedTask; }
        public Task StopAsync() { this.StopCount++; this.IsConnected = false; return Task.CompletedTask; }
        public Task SendAsync(IIpcMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { this.DisposeCount++; this.IsConnected = false; }
    }
    private sealed class ImmediateProcessLaunchApi : AgentLauncher.IProcessLaunchApi
    {
        public bool DuplicateTokenEx(IntPtr existingToken, int desiredAccess, IntPtr tokenAttributes, int impersonationLevel, int tokenType, out IntPtr newToken) { newToken = new(2); return true; }
        public bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit) { environment = IntPtr.Zero; return false; }
        public bool CreateProcessAsUser(IntPtr token, string? applicationName, string commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, int flags, IntPtr environment, string? currentDirectory, ref AgentLauncher.STARTUPINFO startupInfo, out AgentLauncher.PROCESS_INFORMATION processInformation) { processInformation = default; return true; }
        public bool DestroyEnvironmentBlock(IntPtr environment) => true;
        public System.Diagnostics.Process GetProcessById(int processId) => throw new ArgumentException("process exited");
        public bool CloseHandle(IntPtr handle) => true;
    }
}
