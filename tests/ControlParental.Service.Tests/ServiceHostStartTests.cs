// <copyright file="ServiceHostStartTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// Verifies that the service host can start without a real Windows Service registration.
/// Uses temp directories to avoid C:\ProgramData permission issues when running as a normal user.
/// </summary>
public class ServiceHostStartTests : IDisposable
{
    private readonly string _tempDataPath;
    private readonly IHost _host;
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public ServiceHostStartTests()
    {
        this._tempDataPath = Path.Combine(Path.GetTempPath(), $"cp_hosttest_{Guid.NewGuid():N}");

        var builder = Host.CreateApplicationBuilder();

        // T37
        builder.Services.AddSingleton<IPrivilegeInspector, PrivilegeInspector>();
        builder.Services.AddSingleton<IAclHardener, AclHardener>();
        builder.Services.AddSingleton<IScmController, ScmController>();
        builder.Services.AddSingleton<IChildAccountStore>(sp =>
            new ChildAccountStore(this._tempDataPath, sp.GetRequiredService<IAclHardener>()));
        builder.Services.AddSingleton<IAccountManager>(sp =>
            new AccountManager(
                sp.GetRequiredService<IPrivilegeInspector>(),
                sp.GetRequiredService<IChildAccountStore>(),
                this._tempDataPath,
                this._tempDataPath));
        builder.Services.AddSingleton<IProtectedProcessReporter>(
            _ => new ProtectedProcessReporter(Path.Combine(this._tempDataPath, "ControlParental.Service.exe")));

        // T38
        builder.Services.AddSingleton<SessionManager>();

        // T03
        var dbPath = Path.Combine(this._tempDataPath, "controlparental.db");
        builder.Services.AddSingleton<ControlParentalDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;
            return new ControlParentalDbContext(options);
        });
        builder.Services.AddSingleton<IPolicyRepository, PolicyRepository>();
        builder.Services.AddSingleton<PolicyRepository>();

        // T04
        builder.Services.AddSingleton<ITimeProvider, TimeProvider>();

        // T07
        builder.Services.AddSingleton<IUsageReconciler>((sp) =>
        {
            var dbContext = sp.GetRequiredService<ControlParentalDbContext>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var ipcChannel = sp.GetService<IIpcChannel>();
            Func<string, string> resolveAppId = Interop.AppIdentityResolver.Resolve;
            return new UsageReconciler(dbContext, timeProvider, ipcChannel, resolveAppId);
        });

        // T06
        builder.Services.AddSingleton<IUsageAccumulator>((sp) =>
        {
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var reconciler = sp.GetService<IUsageReconciler>();
            return new UsageAccumulator(
                ipcChannel: null,
                repository: sp.GetRequiredService<PolicyRepository>(),
                timeProvider: timeProvider,
                usageReconciler: reconciler);
        });

        // T09
        builder.Services.AddSingleton<IWorkstationLockManager>((sp) =>
            new WorkstationLockManager(sp.GetService<IIpcChannel>()));
        builder.Services.AddSingleton<IOverlayPersistenceManager, OverlayPersistenceManager>();

        // T10
        builder.Services.AddSingleton<IServiceHealthMonitor>((sp) =>
            new ServiceHealthMonitor(
                sp.GetRequiredService<ITimeProvider>(),
                onAgentDied: () => { },
                onServiceUnhealthy: _ => { }));
        builder.Services.AddSingleton<IServiceRecoveryManager>((sp) =>
            new ServiceRecoveryManager(
                sp.GetRequiredService<IServiceHealthMonitor>(),
                () => Task.FromResult(false),
                _ => { },
                () => { }));

        // T11
        builder.Services.AddSingleton<IProcessTerminator, ProcessTerminator>();

        // T12
        builder.Services.AddSingleton<IEnforcementLevelMonitor>((sp) =>
            new EnforcementLevelMonitor(
                sp.GetRequiredService<IPrivilegeInspector>(),
                sp.GetRequiredService<IScmController>(),
                sp.GetRequiredService<IServiceHealthMonitor>(),
                sp.GetRequiredService<ITimeProvider>(),
                _ => { }));

        // T03
        builder.Services.AddSingleton<IOutboxManager, OutboxManager>();

        // T23: Register mock dependencies for AntiTamperMonitor
        builder.Services.AddSingleton<IWinTrustVerifier, WinTrustVerifier>();
        builder.Services.AddSingleton<IIntegrityChecker, IntegrityChecker>();
        builder.Services.AddSingleton<IIntegrityVerdictHandler>((sp) =>
            new IntegrityVerdictHandler(sp.GetRequiredService<IOutboxManager>()));

        // T14: BackendClient
        builder.Services.AddSingleton<IBackendClient>((sp) =>
            new BackendClient(
                new HttpClient(),
                "https://placeholder.supabase.co",
                sp.GetRequiredService<IDeviceAuthenticator>()));

        // T13
        builder.Services.AddSingleton<IAntiTamperMonitor>((sp) =>
            new AntiTamperMonitor(
                sp.GetRequiredService<ITimeProvider>(),
                sp.GetRequiredService<IOutboxManager>(),
                sp.GetRequiredService<IPrivilegeInspector>(),
                sp.GetRequiredService<IEnforcementLevelMonitor>(),
                sp.GetRequiredService<IIntegrityChecker>(),
                sp.GetRequiredService<IBackendClient>(),
                sp.GetRequiredService<IIntegrityVerdictHandler>(),
                _ => { }));

        // T16: SecretStore with temp path
        var secretPath = Path.Combine(this._tempDataPath, "Secrets");
        builder.Services.AddSingleton<ISecretStore>(_ => new SecretStore(secretPath));

        // T17: DeviceAuthenticator
        builder.Services.AddSingleton<IDeviceAuthenticator>((sp) =>
            new DeviceAuthenticator(
                sp.GetRequiredService<ISecretStore>(),
                sp.GetRequiredService<ITimeProvider>(),
                new HttpClient(),
                "https://placeholder.supabase.co",
                "placeholder-anon-key"));

        // T20: ScheduledWorkService
        builder.Services.AddSingleton<IScheduledWorkService>((sp) =>
            new ScheduledWorkService(
                sp.GetRequiredService<IBackendClient>(),
                sp.GetRequiredService<IOutboxManager>(),
                sp.GetRequiredService<IUsageReconciler>(),
                sp.GetRequiredService<IEnforcementLevelMonitor>(),
                sp.GetRequiredService<ITimeProvider>(),
                sp.GetRequiredService<IServiceHealthMonitor>(),
                sp.GetRequiredService<IServiceRecoveryManager>(),
                sp.GetRequiredService<IPolicyRepository>()));
        // T20 adapter: IScheduledWorkService → IHostedService
        builder.Services.AddHostedService<DevScheduledWorkServiceAdapter>();

        // NOTE: ControlParentalService and SessionManager are NOT started here
        // because they require Windows session management (WTSQueryUserToken, etc.)
        // We only verify the host can be built and StartAsync() completes without throwing.

        this._host = builder.Build();
    }

    [Fact]
    public void HostBuilder_ServicesResolve_WithoutThrowing()
    {
        // This test verifies that GetRequiredService does NOT throw for any registered service.
        // It exercises the full DI graph including ISecretStore, IDeviceAuthenticator,
        // IBackendClient, and IScheduledWorkService.

        var sp = this._host.Services;

        var secretStore = sp.GetRequiredService<ISecretStore>();
        Assert.NotNull(secretStore);

        var auth = sp.GetRequiredService<IDeviceAuthenticator>();
        Assert.NotNull(auth);

        var bc = sp.GetRequiredService<IBackendClient>();
        Assert.NotNull(bc);

        var sws = sp.GetRequiredService<IScheduledWorkService>();
        Assert.NotNull(sws);
    }

    [Fact]
    public async Task HostStartAsync_DoesNotThrow()
    {
        // This is the critical test: Host.StartAsync() must not throw.
        // If SecretStore, DbContext, or any other service constructor throws,
        // this test will fail.

        Exception? caught = null;
        try
        {
            await this._host.StartAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.Null(caught);
    }

    [Fact]
    public async Task HostStartAsync_CompletesWithinReasonableTime()
    {
        // Verify the host starts quickly (no infinite loops in constructors)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var stopwatch = Stopwatch.StartNew();
        await this._host.StartAsync(cts.Token);
        stopwatch.Stop();

        Assert.True(
            stopwatch.ElapsedMilliseconds < 5000,
            $"Host.StartAsync took {stopwatch.ElapsedMilliseconds}ms — suspiciously long");
    }

    [Fact]
    public void SecretStore_UsesTempPath_CreatedSuccessfully()
    {
        // Verify the temp secret path was created by resolving SecretStore
        var secretPath = Path.Combine(this._tempDataPath, "Secrets");
        var store = this._host.Services.GetRequiredService<ISecretStore>();
        Assert.NotNull(store);
        Assert.True(Directory.Exists(secretPath), $"SecretStore should have created {secretPath}");
    }

    [Fact]
    public async Task HostStopAsync_DoesNotThrow()
    {
        await this._host.StartAsync(CancellationToken.None);

        Exception? caught = null;
        try
        {
            await this._host.StopAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        Assert.Null(caught);
    }

    public void Dispose()
    {
        this._host.Dispose();

        // Cleanup temp directory
        try
        {
            if (Directory.Exists(this._tempDataPath))
            {
                Directory.Delete(this._tempDataPath, recursive: true);
            }
        }
        catch
        {
            // Best-effort
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Dev adapter mirroring ScheduledWorkServiceHostedAdapter from Program.cs.
/// Used because the original is a 'file' class inaccessible to the test project.
/// </summary>
file sealed class DevScheduledWorkServiceAdapter : IHostedService
{
    private readonly IScheduledWorkService inner;

    public DevScheduledWorkServiceAdapter(IScheduledWorkService inner)
    {
        this.inner = inner;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
        => this.inner.StartAsync(cancellationToken);

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
        => this.inner.StopAsync(cancellationToken);
}
