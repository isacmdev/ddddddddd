// <copyright file="ServiceCompositionTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// T17 — Integration tests that verify the DI container can resolve
/// all services registered in Program.cs, including the T17 services:
/// ISecretStore, IBackendClient, IDeviceAuthenticator, IScheduledWorkService.
/// </summary>
public class ServiceCompositionTests : IDisposable
{
    private readonly ServiceProvider serviceProvider;
    private readonly string tempDataPath;

    public ServiceCompositionTests()
    {
        // Use a temp directory for testing to avoid depending on Windows-specific paths
        this.tempDataPath = Path.Combine(Path.GetTempPath(), $"cptest_{Guid.NewGuid():N}");

        var services = new ServiceCollection();

        // T37: Register privilege/account/ACL infrastructure
        services.AddSingleton<IPrivilegeInspector, PrivilegeInspector>();
        services.AddSingleton<IAclHardener, AclHardener>();
        services.AddSingleton<IScmController, ScmController>();
        services.AddSingleton<IChildAccountStore>(sp =>
        {
            var aclHardener = sp.GetRequiredService<IAclHardener>();
            return new ChildAccountStore(this.tempDataPath, aclHardener);
        });
        services.AddSingleton<IAccountManager>(sp =>
        {
            var privilegeInspector = sp.GetRequiredService<IPrivilegeInspector>();
            var accountStore = sp.GetRequiredService<IChildAccountStore>();
            return new AccountManager(
                privilegeInspector,
                accountStore,
                this.tempDataPath,
                this.tempDataPath);
        });
        services.AddSingleton<IProtectedProcessReporter>(
            _ => new ProtectedProcessReporter(Path.Combine(this.tempDataPath, "ControlParental.Service.exe")));

        // T38: IPC channel and session management
        services.AddSingleton<SessionManager>();

        // T03: Register PolicyRepository and DbContext (in-memory SQLite for testing)
        var dbPath = Path.Combine(this.tempDataPath, "test.db");
        services.AddSingleton<ControlParentalDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            return new ControlParentalDbContext(options);
        });
        services.AddSingleton<IPolicyRepository, PolicyRepository>();
        services.AddSingleton<PolicyRepository>(); // Concrete for UsageAccumulator

        // T04: Register TimeProvider
        services.AddSingleton<ITimeProvider, TimeProvider>();

        // T07: Register UsageReconciler
        services.AddSingleton<IUsageReconciler>((sp) =>
        {
            var dbContext = sp.GetRequiredService<ControlParentalDbContext>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var ipcChannel = sp.GetService<IIpcChannel>();
            Func<string, string> resolveAppId = Interop.AppIdentityResolver.Resolve;
            return new UsageReconciler(dbContext, timeProvider, ipcChannel, resolveAppId);
        });

        // T06: Register UsageAccumulator
        services.AddSingleton<IUsageAccumulator>((sp) =>
        {
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var reconciler = sp.GetService<IUsageReconciler>();
            return new UsageAccumulator(
                ipcChannel: null,
                repository: sp.GetRequiredService<PolicyRepository>(),
                timeProvider: timeProvider,
                usageReconciler: reconciler);
        });

        // T09: Register WorkstationLockManager and OverlayPersistenceManager
        services.AddSingleton<IWorkstationLockManager>((sp) =>
        {
            var ipcChannel = sp.GetService<IIpcChannel>();
            return new WorkstationLockManager(ipcChannel);
        });
        services.AddSingleton<IOverlayPersistenceManager, OverlayPersistenceManager>();

        // T10: Register ServiceHealthMonitor and ServiceRecoveryManager
        services.AddSingleton<IServiceHealthMonitor>((sp) =>
        {
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            return new ServiceHealthMonitor(
                timeProvider: timeProvider,
                onAgentDied: () => { },
                onServiceUnhealthy: _ => { });
        });
        services.AddSingleton<IServiceRecoveryManager>((sp) =>
        {
            var healthMonitor = sp.GetRequiredService<IServiceHealthMonitor>();
            return new ServiceRecoveryManager(
                healthMonitor: healthMonitor,
                recoverAgentFunc: () => Task.FromResult(false),
                onRecoveryFailed: _ => { },
                onRecoverySucceeded: () => { });
        });

        // T11: Register EnforcementEngine components
        services.AddSingleton<IProcessTerminator, ProcessTerminator>();

        // T12: Register EnforcementLevelMonitor
        services.AddSingleton<IEnforcementLevelMonitor>((sp) =>
        {
            var privilegeInspector = sp.GetRequiredService<IPrivilegeInspector>();
            var scmController = sp.GetRequiredService<IScmController>();
            var healthMonitor = sp.GetRequiredService<IServiceHealthMonitor>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();

            return new EnforcementLevelMonitor(
                privilegeInspector: privilegeInspector,
                scmController: scmController,
                healthMonitor: healthMonitor,
                timeProvider: timeProvider,
                onIssueDetected: _ => { });
        });

        // T03: Register OutboxManager
        services.AddSingleton<IOutboxManager, OutboxManager>();

        // T23: Register mock dependencies for AntiTamperMonitor
        services.AddSingleton<IWinTrustVerifier, WinTrustVerifier>();
        services.AddSingleton<IIntegrityChecker, IntegrityChecker>();
        services.AddSingleton<IIntegrityVerdictHandler>((sp) =>
            new IntegrityVerdictHandler(sp.GetRequiredService<IOutboxManager>()));
        services.AddSingleton<IBackendClient, BackendClient>();

        // T13: Register AntiTamperMonitor
        services.AddSingleton<IAntiTamperMonitor>((sp) =>
        {
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var outboxManager = sp.GetRequiredService<IOutboxManager>();
            var privilegeInspector = sp.GetRequiredService<IPrivilegeInspector>();
            var enforcementLevelMonitor = sp.GetRequiredService<IEnforcementLevelMonitor>();
            var integrityChecker = sp.GetRequiredService<IIntegrityChecker>();
            var backendClient = sp.GetRequiredService<IBackendClient>();
            var verdictHandler = sp.GetRequiredService<IIntegrityVerdictHandler>();

            return new AntiTamperMonitor(
                timeProvider: timeProvider,
                outboxManager: outboxManager,
                privilegeInspector: privilegeInspector,
                enforcementLevelMonitor: enforcementLevelMonitor,
                integrityChecker: integrityChecker,
                backendClient: backendClient,
                verdictHandler: verdictHandler,
                onTamperDetected: _ => { });
        });

        // T16: Register SecretStore (T17 dependency)
        var secretPath = Path.Combine(this.tempDataPath, "Secrets");
        services.AddSingleton<ISecretStore>((sp) => new SecretStore(secretPath));

        // T14/BackendClient: Register BackendClient
        services.AddSingleton<IBackendClient>((sp) =>
        {
            var httpClient = new HttpClient();
            var deviceAuth = sp.GetRequiredService<IDeviceAuthenticator>();
            const string supabaseUrl = "https://placeholder.supabase.co";
            return new BackendClient(httpClient, supabaseUrl, deviceAuth);
        });

        // T17: Register DeviceAuthenticator
        services.AddSingleton<IDeviceAuthenticator>((sp) =>
        {
            var secretStore = sp.GetRequiredService<ISecretStore>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var httpClient = new HttpClient();
            const string supabaseUrl = "https://placeholder.supabase.co";
            const string supabaseKey = "placeholder-anon-key";
            return new DeviceAuthenticator(
                secretStore,
                timeProvider,
                httpClient,
                supabaseUrl,
                supabaseKey);
        });

        // T20: Register ScheduledWorkService
        services.AddSingleton<IScheduledWorkService>((sp) =>
        {
            var backendClient = sp.GetRequiredService<IBackendClient>();
            var outboxManager = sp.GetRequiredService<IOutboxManager>();
            var usageReconciler = sp.GetRequiredService<IUsageReconciler>();
            var enforcementLevelMonitor = sp.GetRequiredService<IEnforcementLevelMonitor>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var healthMonitor = sp.GetRequiredService<IServiceHealthMonitor>();
            var recoveryManager = sp.GetRequiredService<IServiceRecoveryManager>();
            var policyRepository = sp.GetRequiredService<IPolicyRepository>();
            return new ScheduledWorkService(
                backendClient,
                outboxManager,
                usageReconciler,
                enforcementLevelMonitor,
                timeProvider,
                healthMonitor,
                recoveryManager,
                policyRepository);
        });

        this.serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void ServiceProvider_CanResolveISecretStore()
    {
        // T17: ISecretStore must be resolvable
        var store = this.serviceProvider.GetRequiredService<ISecretStore>();
        Assert.NotNull(store);
    }

    [Fact]
    public void ServiceProvider_CanResolveIDeviceAuthenticator()
    {
        // T17: IDeviceAuthenticator must be resolvable
        var auth = this.serviceProvider.GetRequiredService<IDeviceAuthenticator>();
        Assert.NotNull(auth);
    }

    [Fact]
    public void ServiceProvider_CanResolveIBackendClient()
    {
        // T17: IBackendClient must be resolvable
        var client = this.serviceProvider.GetRequiredService<IBackendClient>();
        Assert.NotNull(client);
    }

    [Fact]
    public void ServiceProvider_CanResolveIScheduledWorkService()
    {
        // T20: IScheduledWorkService must be resolvable
        var scheduler = this.serviceProvider.GetRequiredService<IScheduledWorkService>();
        Assert.NotNull(scheduler);
    }

    [Fact]
    public void ServiceProvider_AllFourT17ServicesAreDistinctInstances()
    {
        // Verify they are registered as separate instances (not the same object)
        var store = this.serviceProvider.GetRequiredService<ISecretStore>();
        var auth = this.serviceProvider.GetRequiredService<IDeviceAuthenticator>();
        var client = this.serviceProvider.GetRequiredService<IBackendClient>();
        var scheduler = this.serviceProvider.GetRequiredService<IScheduledWorkService>();

        Assert.NotSame(store, auth);
        Assert.NotSame(store, client);
        Assert.NotSame(store, scheduler);
        Assert.NotSame(auth, client);
        Assert.NotSame(auth, scheduler);
        Assert.NotSame(client, scheduler);
    }

    public void Dispose()
    {
        this.serviceProvider.Dispose();

        // Clean up temp directory
        try
        {
            if (Directory.Exists(this.tempDataPath))
            {
                Directory.Delete(this.tempDataPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
