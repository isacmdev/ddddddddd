// <copyright file="ProgramHardeningTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System.Reflection;
using ControlParental.Domain;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

/// <summary>
/// T10 — Tests for Program hardening wiring.
/// </summary>
public class ProgramHardeningTests
{
    [Fact]
    public async Task BackupOrchestration_StartsHostedDependenciesBeforeAdmission()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var events = new List<string>();
        var services = new ServiceCollection();
        services.AddDbContextFactory<ControlParentalDbContext>(options =>
            options.UseSqlite(connection));
        services.AddSingleton(events);
        services.AddSingleton<LifecycleProbe>();
        services.AddHostedService(sp => sp.GetRequiredService<LifecycleProbe>());
        services.AddSingleton<IScheduledWorkService>(
            new RecordingScheduledWorkService(events));
        Program.ConfigureBackupAdmission(services);

        using (var host = Host.CreateDefaultBuilder()
            .ConfigureServices(collection =>
            {
                foreach (var descriptor in services)
                {
                    collection.Add(descriptor);
                }
            })
            .Build())
        {
            await Program.StartHostAndRunSelectedModeAsync(host, true, BackupMode.Heartbeat);

            var probe = host.Services.GetRequiredService<LifecycleProbe>();
            probe.Started.Should().BeTrue();
            probe.DatabaseReady.Should().BeTrue();
            events.Should().Equal("host-start", "admission");
        }
    }

    [Fact]
    public async Task ComposedConcurrentDuplicateTriggers_UseTheRealSchedulerSingleFlightOwner()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var reconciler = new Mock<IUsageReconciler>(MockBehavior.Strict);
        reconciler.SetupGet(r => r.IsRunning).Returns(false);
        reconciler.Setup(r => r.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        reconciler.Setup(r => r.ReconcileAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => ReconcileOnceAsync(token));
        var scheduler = CreateScheduler(reconciler.Object);
        var services = new ServiceCollection();
        services.AddSingleton<IScheduledWorkService>(scheduler);
        Program.ConfigureBackupAdmission(services);

        using var provider = services.BuildServiceProvider();
        var backup = provider.GetRequiredService<ITaskSchedulerBackup>();
        var first = backup.TriggerBackupAsync(BackupMode.Reconciliation);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = backup.TriggerBackupAsync(BackupMode.Reconciliation);

        release.SetResult();
        await Task.WhenAll(first, second);

        calls.Should().Be(1);

        async Task<ReconciliationResult> ReconcileOnceAsync(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return ReconciliationResult.Ok(0, 0, 0, TimeSpan.Zero);
        }
    }

    [Fact]
    public async Task ConfigureBackupAdmission_ResolvesRealBackupAndSharedScheduler()
    {
        using var scheduler = CreateScheduler();
        var services = new ServiceCollection();
        services.AddSingleton<IScheduledWorkService>(scheduler);
        Program.ConfigureBackupAdmission(services);

        using var provider = services.BuildServiceProvider();
        var backup = provider.GetRequiredService<ITaskSchedulerBackup>();

        backup.Should().BeOfType<TaskSchedulerBackupService>();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            backup.TriggerBackupAsync((BackupMode)99));
    }

    [Fact]
    public async Task ConfigureBackupAdmission_InvokesTheSameRegisteredSchedulerSingleton()
    {
        var scheduler = new RecordingScheduledWorkService();
        var services = new ServiceCollection();
        services.AddSingleton<IScheduledWorkService>(scheduler);
        Program.ConfigureBackupAdmission(services);

        using var provider = services.BuildServiceProvider();
        var backup = provider.GetRequiredService<ITaskSchedulerBackup>();
        using var caller = new CancellationTokenSource();

        await backup.TriggerBackupAsync(BackupMode.Outbox, caller.Token);

        scheduler.Calls.Should().ContainSingle();
        scheduler.Calls[0].Mode.Should().Be(BackupMode.Outbox);
        scheduler.Calls[0].Token.CanBeCanceled.Should().BeTrue();
        provider.GetRequiredService<IScheduledWorkService>().Should().BeSameAs(scheduler);
    }

    [Fact]
    public async Task RunBackupModeAsync_PropagatesCallerCancellationThroughRealComposition()
    {
        using var scheduler = CreateScheduler();
        var services = new ServiceCollection();
        services.AddSingleton<IScheduledWorkService>(scheduler);
        Program.ConfigureBackupAdmission(services);
        using var provider = services.BuildServiceProvider();
        using var caller = new CancellationTokenSource();
        caller.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Program.RunBackupModeAsync(provider, BackupMode.Heartbeat, caller.Token, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task RunBackupModeAsync_ConvertsAdmissionTimeoutWithoutWaitingForDefaultBound()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IScheduledWorkService, BlockingScheduledWorkService>();
        Program.ConfigureBackupAdmission(services);
        using var provider = services.BuildServiceProvider();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<TimeoutException>(() =>
            Program.RunBackupModeAsync(provider, BackupMode.Heartbeat, timeout: TimeSpan.FromMilliseconds(20)));
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RunBackupModeAsync_CompletesThroughTheSharedScheduler()
    {
        using var scheduler = CreateScheduler();
        var services = new ServiceCollection();
        services.AddSingleton<IScheduledWorkService>(scheduler);
        Program.ConfigureBackupAdmission(services);
        using var provider = services.BuildServiceProvider();

        await Program.RunBackupModeAsync(provider, BackupMode.Reconciliation, timeout: TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task RunBackupHostModeAsync_StopsHostAfterAdmission()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaskSchedulerBackup>(
            new TaskSchedulerBackupService((_, _) => Task.CompletedTask));
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(collection =>
            {
                foreach (var descriptor in services)
                {
                    collection.Add(descriptor);
                }
            })
            .Build();

        await host.StartAsync();
        await Program.RunBackupHostModeAsync(host, BackupMode.Heartbeat, timeout: TimeSpan.FromSeconds(1));
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested
            .Should().BeTrue();
    }

    [Fact]
    public async Task RunSelectedModeAsync_UsesBackupAdmissionOrNormalShutdown()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaskSchedulerBackup>(
            new TaskSchedulerBackupService((_, _) => Task.CompletedTask));
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(collection =>
            {
                foreach (var descriptor in services)
                {
                    collection.Add(descriptor);
                }
            })
            .Build();

        await host.StartAsync();
        await Program.RunSelectedModeAsync(host, true, BackupMode.Heartbeat);
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested
            .Should().BeTrue();
    }

    [Fact]
    public async Task RunSelectedModeAsync_NormalModeWaitsForShutdown()
    {
        using var host = Host.CreateDefaultBuilder().Build();
        await host.StartAsync();
        host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        await Program.RunSelectedModeAsync(host, false, BackupMode.Heartbeat);
    }

    [Fact]
    public async Task RunBackupHostModeAsync_StopsHostAfterAdmissionTimeout()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IScheduledWorkService, BlockingScheduledWorkService>();
        Program.ConfigureBackupAdmission(services);
        using var host = Host.CreateDefaultBuilder()
            .ConfigureServices(collection =>
            {
                foreach (var descriptor in services)
                {
                    collection.Add(descriptor);
                }
            })
            .Build();

        await host.StartAsync();
        await Program.RunBackupHostModeAsync(host, BackupMode.Heartbeat, timeout: TimeSpan.FromMilliseconds(20));
        host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested
            .Should().BeTrue();
    }

    [Fact]
    public async Task ApplyHardeningAsync_InvokesAclAndSCMHardeningBoundaries()
    {
        var aclHardener = new Mock<IAclHardener>(MockBehavior.Strict);
        aclHardener.Setup(h => h.HardenAllAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var privilegeInspector = new Mock<IPrivilegeInspector>(MockBehavior.Strict);
        privilegeInspector.Setup(p => p.GetPrivilegeLevelAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrivilegeLevel.Standard)
            .Verifiable();

        var scm = new Mock<IScmController>(MockBehavior.Strict);
        scm.Setup(s => s.ConfigureFailureActionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();
        scm.Setup(s => s.SetStartupTypeAsync(It.IsAny<string>(), "auto", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .Verifiable();

        var timeProvider = new Mock<ITimeProvider>(MockBehavior.Strict);
        timeProvider.SetupGet(t => t.MonotonicNow).Returns(0);

        var healthMonitor = new ServiceHealthMonitor(
            timeProvider.Object,
            onAgentDied: () => { },
            onServiceUnhealthy: _ => { });

        var services = new ServiceCollection();
        services.AddSingleton(aclHardener.Object);
        services.AddSingleton(privilegeInspector.Object);
        services.AddSingleton(scm.Object);
        services.AddSingleton(healthMonitor);

        var provider = services.BuildServiceProvider();
        var applyHardening = typeof(Program).GetMethod(
            "ApplyHardeningAsync",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var task = (Task)applyHardening.Invoke(null, new object[] { provider })!;
        await task;

        aclHardener.Verify();
        privilegeInspector.Verify();
        scm.Verify();
        healthMonitor.SecurityVerdict.Should().Be(RuntimeSecurityVerdict.HealthyStandard);
    }

    private static ScheduledWorkService CreateScheduler(IUsageReconciler? usageReconciler = null)
    {
        var backend = new Mock<IBackendClient>().Object;
        var outbox = new Mock<IOutboxManager>().Object;
        usageReconciler ??= new Mock<IUsageReconciler>().Object;
        var enforcement = new Mock<IEnforcementLevelMonitor>().Object;
        var time = new Mock<ITimeProvider>();
        time.SetupGet(t => t.WallClockNow).Returns(DateTimeOffset.UtcNow);
        var health = new Mock<IServiceHealthMonitor>().Object;
        var recovery = new Mock<IServiceRecoveryManager>().Object;
        var policy = new Mock<IPolicyRepository>().Object;
        return new ScheduledWorkService(
            backend,
            outbox,
            usageReconciler,
            enforcement,
            time.Object,
            health,
            recovery,
            policy);
    }

    private sealed class BlockingScheduledWorkService : IScheduledWorkService
    {
        public bool IsRunning => true;

        public Task<SyncAdmissionResult> AdmitSyncAsync(SyncTriggerSource source, CancellationToken cancellationToken = default) =>
            Task.FromResult(SyncAdmissionResult.Accepted);

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RunBackupAsync(BackupMode mode, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class RecordingScheduledWorkService : IScheduledWorkService
    {
        private readonly List<string>? events;

        public RecordingScheduledWorkService(List<string>? events = null)
        {
            this.events = events;
        }

        public List<(BackupMode Mode, CancellationToken Token)> Calls { get; } = new();

        public bool IsRunning => true;

        public Task<SyncAdmissionResult> AdmitSyncAsync(SyncTriggerSource source, CancellationToken cancellationToken = default)
        {
            this.events?.Add("sync-admission");
            return Task.FromResult(SyncAdmissionResult.Accepted);
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task RunBackupAsync(BackupMode mode, CancellationToken cancellationToken = default)
        {
            this.events?.Add("admission");
            this.Calls.Add((mode, cancellationToken));
            return Task.CompletedTask;
        }
    }

    private sealed class LifecycleProbe : IHostedService
    {
        private readonly IDbContextFactory<ControlParentalDbContext> dbFactory;
        private readonly List<string> events;

        public LifecycleProbe(
            IDbContextFactory<ControlParentalDbContext> dbFactory,
            List<string> events)
        {
            this.dbFactory = dbFactory;
            this.events = events;
        }

        public bool Started { get; private set; }

        public bool DatabaseReady { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var db = await this.dbFactory.CreateDbContextAsync(cancellationToken);
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_version';";
            this.DatabaseReady = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 1;
            this.Started = true;
            this.events.Add("host-start");
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
