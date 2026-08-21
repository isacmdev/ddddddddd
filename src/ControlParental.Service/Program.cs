// <copyright file="Program.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Collections.Generic;
using System.Data.Common;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlParental.Domain;
using ControlParental.Service.Interop;
using ControlParental.Service.CompiledModels;

/// <summary>
/// T19 — WNS configuration loaded from environment variables.
/// </summary>
public sealed record WnsConfig(string PackageSid, string ClientSecret);

/// <summary>
/// T22 — TLS/pinning configuration loaded from environment variables.
/// </summary>
public sealed record TlsPinningConfig(string? CertPin);

/// <summary>
/// Entry point for the Windows Service.
/// </summary>
public static class Program
{
    /// <summary>
    /// Name of the Windows service registered with the SCM.
    /// </summary>
    public const string ServiceName = "ControlParental";

    /// <summary>
    /// Path to the agent installation folder (Program Files).
    /// </summary>
    public static string AgentFolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "ControlParental",
        "Agent");

    /// <summary>
    /// Path to the service data folder (ProgramData).
    /// </summary>
    public static string DataFolderPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "ControlParental");

    /// <summary>
    /// Path to the agent executable.
    /// </summary>
    public static string AgentExePath { get; } = Path.Combine(
        AgentFolderPath,
        "ControlParental.SessionAgent.exe");

    /// <summary>
    /// Registry key path for the service configuration.
    /// </summary>
    public static string ServiceRegistryKey { get; } = @"SYSTEM\CurrentControlSet\Services\" + ServiceName;

    /// <summary>
    /// Path to the service executable.
    /// </summary>
    public static string ServiceExePath { get; } = Environment.ProcessPath
        ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ControlParental.Service.exe");

    /// <summary>
    /// Name of the IPC pipe between service and agent.
    /// </summary>
    public const string IpcPipeName = "SessionAgent";

    internal static readonly TimeSpan DefaultBackupAdmissionTimeout = TimeSpan.FromSeconds(30);

    internal static bool TryParseBackupMode(string[]? args, out BackupMode mode)
    {
        mode = default;
        if (args == null || args.Length != 1)
        {
            return false;
        }

        switch (args[0])
        {
            case "--backup-heartbeat":
                mode = BackupMode.Heartbeat;
                return true;
            case "--backup-outbox":
                mode = BackupMode.Outbox;
                return true;
            case "--backup-reconcile":
                mode = BackupMode.Reconciliation;
                return true;
            default:
                return false;
        }
    }

    internal static bool TryParseBackupRequest(
        string[]? args,
        out bool isBackupMode,
        out BackupMode mode)
    {
        var hasBackupArgument = args?.Any(arg => arg.StartsWith("--backup", StringComparison.Ordinal)) == true;
        isBackupMode = TryParseBackupMode(args, out mode);
        return !hasBackupArgument || isBackupMode;
    }

    /// <summary>
    /// T22 — Creates an HttpClient with platform trust, revocation, and optional rotating SPKI pins.
    /// </summary>
    /// <param name="certPin">
    /// Optional semicolon-delimited current and next SPKI pins.
    /// If null or empty, platform certificate and hostname validation remains mandatory.
    /// </param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    private static HttpClient CreateSupabaseHttpClient(string? certPin)
    {
        var policy = new CertificatePinningPolicy(certPin);
        return new HttpClient(policy.ConfigureHandler())
        {
            Timeout = policy.DefaultTimeout,
        };
    }

    internal static void ConfigureBackendIdentityServices(
        IServiceCollection services,
        SupabaseConfig supabaseConfig,
        TlsPinningConfig tlsPinningConfig,
        Func<HttpClient>? httpClientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(supabaseConfig);
        ArgumentNullException.ThrowIfNull(tlsPinningConfig);
        httpClientFactory ??= () => CreateSupabaseHttpClient(tlsPinningConfig.CertPin);

        services.AddSingleton<IBackendIdentityCredentialStore>(sp =>
            sp.GetRequiredService<ISecretStore>() as IBackendIdentityCredentialStore
            ?? throw new InvalidOperationException("The production secret store must own the identity snapshot."));
        services.AddSingleton<IBackendIdentityLifecyclePortV1>(_ =>
            new BackendIdentityLifecycleClient(httpClientFactory(), supabaseConfig.Url, supabaseConfig.AnonKey));
        services.AddSingleton<BackendIdentityCoordinator>();
        services.AddSingleton<IBackendIdentityCoordinator>(sp => sp.GetRequiredService<BackendIdentityCoordinator>());
        services.AddHostedService<BackendIdentityStartupService>();
        services.AddSingleton<IBackendClient>(sp =>
            new BackendClient(httpClientFactory(), supabaseConfig.Url, sp.GetRequiredService<IBackendIdentityCoordinator>()));
        services.AddSingleton<IWnsRegistrationIntentStore>(sp =>
            new SecretStoreWnsRegistrationIntentStore(sp.GetRequiredService<ISecretStore>()));
        services.AddSingleton<WnsRegistrationCoordinator>();
        services.AddSingleton<IWnsRegistrationCoordinator>(sp => sp.GetRequiredService<WnsRegistrationCoordinator>());
        services.AddHostedService<WnsRegistrationReconciliationService>();
        services.AddScoped<IPairingService, PairingService>();
    }

    internal static void ConfigureBackupAdmission(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<ITaskSchedulerBackup>(sp =>
            new TaskSchedulerBackupService((mode, cancellationToken) =>
                sp.GetRequiredService<IScheduledWorkService>().RunBackupAsync(mode, cancellationToken)));
    }

    internal static async Task RunBackupModeAsync(
        IServiceProvider services,
        BackupMode mode,
        CancellationToken callerToken = default,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var timeoutCts = new CancellationTokenSource(timeout ?? DefaultBackupAdmissionTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            callerToken,
            timeoutCts.Token);

        try
        {
            await services.GetRequiredService<ITaskSchedulerBackup>()
                .TriggerBackupAsync(mode, linkedCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException("Backup admission exceeded its finite timeout.");
        }
        finally
        {
            timeoutCts.Dispose();
        }
    }

    internal static async Task RunBackupHostModeAsync(
        IHost host,
        BackupMode mode,
        CancellationToken callerToken = default,
        TimeSpan? timeout = null)
    {
        try
        {
            await RunBackupModeAsync(host.Services, mode, callerToken, timeout)
                .ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine("[Program] Backup admission timed out.");
        }
        finally
        {
            await host.StopAsync().ConfigureAwait(false);
        }
    }

    internal static async Task RunSelectedModeAsync(
        IHost host,
        bool isBackupMode,
        BackupMode backupMode,
        CancellationToken callerToken = default)
    {
        if (isBackupMode)
        {
            await RunBackupHostModeAsync(host, backupMode, callerToken)
                .ConfigureAwait(false);
            return;
        }

        await host.WaitForShutdownAsync().ConfigureAwait(false);
    }

    public static Task Main(string[] args) => RunMainAsync(args);

    internal static Task RunMainAsync(string[] args) => RunMainAsync(args, static () => { });

    internal static async Task RunMainAsync(string[] args, Action compositionStarted)
    {
        if (!TryParseBackupRequest(args, out var isBackupMode, out var backupMode))
        {
            Console.Error.WriteLine("[Program] Invalid or ambiguous backup arguments.");
            return;
        }

        compositionStarted();

        // T18: Load Supabase config from .env (repo root or ProgramData)
        if (!ConfigurationLoader.TryLoad(out var supabaseConfig))
        {
            Console.Error.WriteLine(
                "[Program] FATAL: Could not load Supabase configuration from .env. " +
                $"Searched: {ConfigurationLoader.EnvFilePath}");
            return;
        }

        // T22: Prefer overlapping current/next pins; retain the legacy single-pin variable.
        var tlsPinningConfig = new TlsPinningConfig(
            Environment.GetEnvironmentVariable("SUPABASE_CERT_PINS")
                ?? Environment.GetEnvironmentVariable("SUPABASE_CERT_PIN"));

        var builder = Host.CreateApplicationBuilder(args);

        // T37: Register privilege/account/ACL infrastructure
        builder.Services.AddSingleton<IPrivilegeInspector, PrivilegeInspector>();
        builder.Services.AddSingleton<IAclHardener, AclHardener>();
        builder.Services.AddSingleton<IScmController, ScmController>();
        builder.Services.AddSingleton<IChildAccountStore>(sp =>
        {
            var aclHardener = sp.GetRequiredService<IAclHardener>();
            return new ChildAccountStore(DataFolderPath, aclHardener);
        });
        builder.Services.AddSingleton<IAccountManager>(sp =>
        {
            var privilegeInspector = sp.GetRequiredService<IPrivilegeInspector>();
            var accountStore = sp.GetRequiredService<IChildAccountStore>();
            return new AccountManager(
                privilegeInspector,
                accountStore,
                Path.GetDirectoryName(ServiceExePath) ?? AgentFolderPath,
                DataFolderPath);
        });
        builder.Services.AddSingleton<IProtectedProcessReporter>(
            _ => new ProtectedProcessReporter(ServiceExePath));

        // T38: IPC channel and session management
        builder.Services.AddSingleton<SessionManager>();

        // T03: Register PolicyRepository and DbContext factory
        var dataFolder = DataFolderPath;
         builder.Services.AddDbContextFactory<ControlParentalDbContext>(options =>
         {
             ConfigureDbContextOptions(
                 options,
                 connectionString: $"Data Source={Path.Combine(dataFolder, "controlparental.db")}",
                 useCompiledModel: OperatingSystem.IsWindows() &&
                     string.Equals(builder.Environment.EnvironmentName, Environments.Production, StringComparison.Ordinal));
         });
        builder.Services.AddScoped<ControlParentalDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<ControlParentalDbContext>>().CreateDbContext());
        builder.Services.AddSingleton<IPolicyRepository, PolicyRepository>();
        builder.Services.AddSingleton<PolicyRepository>(); // Concrete for UsageAccumulator

        // T04: Register TimeProvider
        builder.Services.AddSingleton<ITimeProvider, TimeProvider>();

        // T07: Register UsageReconciler (IPC channel set via SetIpcChannel after SessionManager creates it)
        builder.Services.AddSingleton<IUsageReconciler>((sp) =>
        {
            var dbContextFactory = sp.GetRequiredService<IDbContextFactory<ControlParentalDbContext>>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var ipcChannel = sp.GetService<IIpcChannel>(); // Nullable — set via SetIpcChannel
            Func<string, string> resolveAppId = Interop.AppIdentityResolver.Resolve;
            return new UsageReconciler(dbContextFactory, timeProvider, ipcChannel, resolveAppId);
        });

        // T06: Register UsageAccumulator with optional IUsageReconciler for backfill
        builder.Services.AddSingleton<IUsageAccumulator>((sp) =>
        {
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var reconciler = sp.GetService<IUsageReconciler>(); // Optional - may not be resolved yet
            // IPC channel is set via SetIpcChannel after SessionManager creates it
            return new UsageAccumulator(
                ipcChannel: null,
                repository: sp.GetRequiredService<PolicyRepository>(),
                timeProvider: timeProvider,
                usageReconciler: reconciler);
        });

        // T09: Register WorkstationLockManager and OverlayPersistenceManager
        builder.Services.AddSingleton<IWorkstationLockManager>((sp) =>
        {
            var ipcChannel = sp.GetService<IIpcChannel>(); // Nullable — set via SetIpcChannel
            return new WorkstationLockManager(ipcChannel);
        });
        builder.Services.AddSingleton<IOverlayPersistenceManager, OverlayPersistenceManager>();

        // T10: Register ServiceHealthMonitor and ServiceRecoveryManager
        builder.Services.AddSingleton<ServiceHealthMonitor>(sp =>
            new ServiceHealthMonitor(
                timeProvider: sp.GetRequiredService<ITimeProvider>(),
                onAgentDied: () => System.Diagnostics.Debug.WriteLine(
                    "[ServiceHealthMonitor] Agent death callback received."),
                onServiceUnhealthy: _ => System.Diagnostics.Debug.WriteLine(
                    "[ServiceHealthMonitor] Service health callback received.")));
        builder.Services.AddSingleton<IServiceHealthMonitor>(sp =>
            sp.GetRequiredService<ServiceHealthMonitor>());
        builder.Services.AddSingleton<IAuthoritativeHealthSink>(sp =>
            sp.GetRequiredService<ServiceHealthMonitor>());
        builder.Services.AddSingleton<IServiceRecoveryManager>((sp) =>
        {
            var healthMonitor = sp.GetRequiredService<IServiceHealthMonitor>();
            // The actual recovery function is provided by ControlParentalService
            return new ServiceRecoveryManager(
                healthMonitor: healthMonitor,
                recoverAgentFunc: () => Task.FromResult(false), // Placeholder, set by service
                onRecoveryFailed: issue => System.Diagnostics.Debug.WriteLine($"[Recovery] Failed: {issue}"),
                onRecoverySucceeded: () => System.Diagnostics.Debug.WriteLine("[Recovery] Succeeded."));
        });

        // T11: Register EnforcementEngine components
        builder.Services.AddSingleton<IProcessTerminator, ProcessTerminator>();

        // T12: Register EnforcementLevelMonitor
        builder.Services.AddSingleton<IPreventiveLayerDetector, WindowsPreventiveLayerDetector>();
        builder.Services.AddSingleton<IIssueStore>(
            _ => new FileIssueStore(Path.Combine(DataFolderPath, "enforcement-issues.v1.json")));
        builder.Services.AddSingleton<IEnforcementLevelMonitor>((sp) =>
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
                onIssueDetected: issue =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EnforcementLevelMonitor] Issue detected: {issue.Type} - {issue.Description}");
                },
                preventiveLayerDetector: sp.GetRequiredService<IPreventiveLayerDetector>(),
                accountManager: sp.GetRequiredService<IAccountManager>(),
                issueStore: sp.GetRequiredService<IIssueStore>());
        });

        // T11: Enforcement loop

        // T03: Register OutboxManager
        builder.Services.AddSingleton<IOutboxManager, OutboxManager>();

        // T23: Register IntegrityChecker for binary integrity verification
        builder.Services.AddSingleton<IWinTrustVerifier, WinTrustVerifier>();
        builder.Services.AddScoped<IIntegrityChecker>(sp =>
            new IntegrityChecker(sp.GetRequiredService<IWinTrustVerifier>()));

        // T23: Register IntegrityVerdictHandler (singleton — maintains state across service lifetime)
        builder.Services.AddSingleton<IIntegrityVerdictHandler>(sp =>
            new IntegrityVerdictHandler(sp.GetRequiredService<IOutboxManager>()));

        // T13: Register AntiTamperMonitor
        builder.Services.AddSingleton<IAntiTamperMonitor>((sp) =>
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
                 identityCoordinator: sp.GetRequiredService<IBackendIdentityCoordinator>(),
                 onTamperDetected: tamperEvent =>
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AntiTamperMonitor] Tamper event: {tamperEvent.Type} - {tamperEvent.Description}");
                });
        });

        // T16: Register SecretStore (infrastructure for T17/T18)
        // Uses DPAPI for secure storage in ProgramData
        builder.Services.AddSingleton<ISecretStore>((sp) =>
        {
            var basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ControlParental",
                "Secrets");
            return new SecretStore(basePath);
        });

        // SDD5 Unit 6: one production identity authority owns pairing and backend authorization.
        ConfigureBackendIdentityServices(builder.Services, supabaseConfig, tlsPinningConfig);

        // T25: Register ConsentService for data collection consent
        builder.Services.AddScoped<IConsentService, ConsentService>();

        // T27: Register UsageStateQueryHandler for UI queries
        builder.Services.AddScoped<UsageStateQueryHandler>();

        // T26: Register the App.UI IPC server and its message graph
        builder.Services.AddSingleton<OnboardingStateService>(sp =>
            new OnboardingStateService(
                DataFolderPath,
                sp.GetRequiredService<IChildAccountStore>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<OnboardingStateService>>(),
                () => sp.GetRequiredService<ServiceHealthMonitor>().CanProceedWithHealthyOnboarding));
        builder.Services.AddSingleton<IOnboardingStateService>(sp =>
            sp.GetRequiredService<OnboardingStateService>());
        builder.Services.AddSingleton<EnforcementLevelQueryHandler>();
        builder.Services.AddSingleton<UIMessageHandler>(sp =>
            new UIMessageHandler(
                sp.GetRequiredService<OnboardingStateService>(),
                sp.GetRequiredService<EnforcementLevelQueryHandler>(),
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<UIMessageHandler>>(),
                 sp.GetRequiredService<IWnsRegistrationCoordinator>(),
                 sp.GetRequiredService<IScheduledWorkService>()));
        builder.Services.AddSingleton<NamedPipeUIServer>(sp =>
            new NamedPipeUIServer(
                null,
                null,
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<NamedPipeUIServer>>()));
        builder.Services.AddHostedService<NamedPipeUIServerHostedAdapter>();

        // T20: Register TaskSchedulerBackupService as safety net for timer failures
        ConfigureBackupAdmission(builder.Services);

        // T20: Register ScheduledWorkService as hosted service
        builder.Services.AddSingleton<IScheduledWorkService>((sp) =>
        {
            var backendClient = sp.GetRequiredService<IBackendClient>();
            var outboxManager = sp.GetRequiredService<IOutboxManager>();
            var usageReconciler = sp.GetRequiredService<IUsageReconciler>();
            var enforcementLevelMonitor = sp.GetRequiredService<IEnforcementLevelMonitor>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var healthMonitor = sp.GetRequiredService<IServiceHealthMonitor>();
            var recoveryManager = sp.GetRequiredService<IServiceRecoveryManager>();
            var policyRepository = sp.GetRequiredService<IPolicyRepository>();
            var taskSchedulerBackup = sp.GetService<ITaskSchedulerBackup>();
            return new ScheduledWorkService(
                backendClient,
                outboxManager,
                usageReconciler,
                enforcementLevelMonitor,
                timeProvider,
                healthMonitor,
                recoveryManager,
                policyRepository,
                taskSchedulerBackup,
                sp.GetRequiredService<IBackendIdentityCoordinator>());
        });
        builder.Services.AddHostedService<ScheduledWorkServiceHostedAdapter>();

        // T10: Service persistence
        builder.Services.AddWindowsService();
        builder.Services.AddHostedService<ControlParentalService>();

        var host = builder.Build();

        // T37: Apply hardening BEFORE DB creation.
        // The hardening sets Deny ACLs on the data folder for the Users group;
        // LocalSystem (the service account) has FullControl Allow so it's unaffected.
        // We must harden first so the Deny is already in place when EnsureCreated
        // opens the file — otherwise the second run fails because the Deny was
        // applied by the first run's ApplyHardeningAsync and persists on the folder.
        await ApplyHardeningAsync(host.Services);

        await StartHostAndRunSelectedModeAsync(
            host,
            isBackupMode,
            backupMode,
            host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
    }

    internal static void ConfigureDbContextOptions(
        DbContextOptionsBuilder options,
        string connectionString,
        bool useCompiledModel)
    {
        options.UseSqlite(connectionString);
        if (useCompiledModel)
        {
            options.UseModel(ControlParentalDbContextModel.Instance);
        }
    }

    internal static async Task InitializeDatabaseAsync(ControlParentalDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureUsageTodayElapsedSecondsColumnAsync(db);
        await SqliteSchemaBootstrapper.AdoptAsync(db, cancellationToken);
    }

    internal static async Task StartHostAfterDatabaseInitializationAsync(IHost host, CancellationToken cancellationToken = default)
    {
        using var scope = host.Services.CreateScope();
        await InitializeDatabaseAsync(
            scope.ServiceProvider.GetRequiredService<ControlParentalDbContext>(),
            cancellationToken);
        await host.StartAsync(cancellationToken);
    }

    internal static async Task StartHostAndRunSelectedModeAsync(
        IHost host,
        bool isBackupMode,
        BackupMode backupMode,
        CancellationToken cancellationToken = default)
    {
        await StartHostAfterDatabaseInitializationAsync(host, cancellationToken);
        await RunSelectedModeAsync(host, isBackupMode, backupMode, cancellationToken);
    }

    private static async Task EnsureUsageTodayElapsedSecondsColumnAsync(ControlParentalDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        var hasElapsedSecondsColumn = false;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(usage_today)";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (string.Equals(reader.GetString(1), "elapsed_seconds", StringComparison.OrdinalIgnoreCase))
                {
                    hasElapsedSecondsColumn = true;
                    break;
                }
            }
        }

        if (!hasElapsedSecondsColumn)
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE usage_today ADD COLUMN elapsed_seconds INTEGER NOT NULL DEFAULT 0";
            await alter.ExecuteNonQueryAsync();
        }

        using var backfill = connection.CreateCommand();
        backfill.CommandText = @"
UPDATE usage_today
SET elapsed_seconds = CASE
    WHEN elapsed_seconds = 0 AND minutes > 0 THEN minutes * 60
    ELSE elapsed_seconds
END";
        await backfill.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Applies all hardening measures on first run.
    /// </summary>
    private static async Task ApplyHardeningAsync(IServiceProvider services)
    {
        var aclHardener = services.GetRequiredService<IAclHardener>();
        var privilegeLevel = await services.GetRequiredService<IPrivilegeInspector>()
            .GetPrivilegeLevelAsync(cancellationToken: CancellationToken.None);

        if (!Directory.Exists(DataFolderPath))
        {
            try
            {
                Directory.CreateDirectory(DataFolderPath);
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[Program] Cannot create data folder.");
            }
        }

        var aclSucceeded = await aclHardener.HardenAllAsync(
            AgentFolderPath,
            DataFolderPath,
            ServiceRegistryKey,
            ServiceExePath,
            CancellationToken.None);

        var verdict = RuntimeSecurityVerdictEvaluator.Evaluate(privilegeLevel, aclSucceeded);
        services.GetRequiredService<ServiceHealthMonitor>().ApplySecurityVerdict(verdict);

        var scmHardening = new ServiceRecoveryHardeningCoordinator(
            services.GetRequiredService<IScmController>());

        _ = await scmHardening.ApplyAsync(ServiceName, CancellationToken.None);
    }
}

/// <summary>
/// T20 — Adapter to host IScheduledWorkService as IHostedService.
/// ScheduledWorkService does not inherit from BackgroundService;
/// this adapter bridges the two patterns.
/// </summary>
file sealed class ScheduledWorkServiceHostedAdapter : IHostedService
{
    private readonly IScheduledWorkService inner;

    public ScheduledWorkServiceHostedAdapter(IScheduledWorkService inner)
    {
        this.inner = inner;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
        => this.inner.StartAsync(cancellationToken);

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
        => this.inner.StopAsync(cancellationToken);
}

/// <summary>
/// Manages the session watcher and agent launcher for T38.
/// Holds the state of the current session and the running agent.
/// Wires session lock/unlock to the usage counter for T06 pause/resume.
/// Coordinates with IOverlayPersistenceManager for T09 persistent overlay.
/// Integrates with IServiceHealthMonitor for T10 recovery.
/// </summary>
public sealed class SessionManager : IDisposable
{
    private readonly string childUsername;
    private readonly string agentExePath;
    private readonly string pipeName;
    private readonly Action<ForegroundChanged> onForegroundChanged;
    private readonly Action<AgentHeartbeat> onHeartbeat;
    private readonly Action<StateSnapshot> onStateSnapshot;
    private readonly Action<AgentActionResult>? onAgentActionResult;
    private readonly IUsageAccumulator? usageAccumulator;
    private readonly IOverlayPersistenceManager? overlayPersistenceManager;
    private readonly Func<string, string?, IIpcMessage?, Task> sendOverlayToAgentAsync;
    private readonly IServiceHealthMonitor? healthMonitor;
    private readonly IServiceRecoveryManager? recoveryManager;
    private readonly Action<IIpcChannel?>? onAgentChannelChanged;
    private readonly Action? onAgentRecoveryNeeded;
    private readonly Action? onAgentDeathDetected;
    private CancellationToken serviceStoppingToken = default;
    private SessionWatcher? sessionWatcher;
    private readonly Dictionary<int, SessionRecord> sessionRecords = new();
    private readonly object recordsLock = new();
    private Func<int, AgentLauncher>? launcherFactory;
    private int? activeSessionId;
    private bool disposed;
    private const int RecoveryAttemptLimit = 3;
    private static readonly TimeSpan[] RecoveryBackoffDelays =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(250),
    ];

    /// <summary>
    /// Gets the IPC channel for communicating with the agent.
    /// May be null if the agent is not running.
    /// </summary>
    public IIpcChannel? AgentChannel
    {
        get
        {
            lock (this.recordsLock)
            {
                return this.activeSessionId is int id && this.sessionRecords.TryGetValue(id, out var record)
                    ? record.Launcher.AgentChannel
                    : null;
            }
        }
    }

    public int? ActiveSessionId
    {
        get
        {
            lock (this.recordsLock)
            {
                return this.activeSessionId;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionManager"/> class.
    /// </summary>
    /// <param name="childUsername">The username of the child account.</param>
    /// <param name="agentExePath">Path to the agent executable.</param>
    /// <param name="pipeName">Name of the IPC pipe.</param>
    /// <param name="onForegroundChanged">Callback for foreground change messages.</param>
    /// <param name="onHeartbeat">Callback for heartbeat messages.</param>
    /// <param name="onStateSnapshot">Callback for state snapshot messages.</param>
    /// <param name="usageAccumulator">Optional usage counter for T06 pause/resume.</param>
    /// <param name="overlayPersistenceManager">Optional overlay persistence manager for T09.</param>
    /// <param name="sendOverlayToAgentAsync">Function to send overlay command to agent.</param>
    /// <param name="healthMonitor">Optional health monitor for T10 recovery.</param>
    /// <param name="recoveryManager">Optional recovery manager for T10.</param>
    /// <param name="onAgentRecoveryNeeded">Callback when agent recovery is needed.</param>
    /// <param name="onAgentDeathDetected">Callback when agent death is detected (T13).</param>
    public SessionManager(
        string childUsername,
        string agentExePath,
        string pipeName,
        Action<ForegroundChanged> onForegroundChanged,
        Action<AgentHeartbeat> onHeartbeat,
        Action<StateSnapshot> onStateSnapshot,
        Action<AgentActionResult>? onAgentActionResult = null,
        IUsageAccumulator? usageAccumulator = null,
        IOverlayPersistenceManager? overlayPersistenceManager = null,
        Func<string, string?, IIpcMessage?, Task>? sendOverlayToAgentAsync = null,
        IServiceHealthMonitor? healthMonitor = null,
        IServiceRecoveryManager? recoveryManager = null,
        Action<IIpcChannel?>? onAgentChannelChanged = null,
        Action? onAgentRecoveryNeeded = null,
        Action? onAgentDeathDetected = null)
    {
        this.childUsername = childUsername;
        this.agentExePath = agentExePath;
        this.pipeName = pipeName;
        this.onForegroundChanged = onForegroundChanged;
        this.onHeartbeat = onHeartbeat;
        this.onStateSnapshot = onStateSnapshot;
        this.onAgentActionResult = onAgentActionResult;
        this.usageAccumulator = usageAccumulator;
        this.overlayPersistenceManager = overlayPersistenceManager;
        this.sendOverlayToAgentAsync = sendOverlayToAgentAsync ?? this.DefaultSendOverlayAsync;
        this.healthMonitor = healthMonitor;
        this.recoveryManager = recoveryManager;
        this.onAgentChannelChanged = onAgentChannelChanged;
        this.onAgentRecoveryNeeded = onAgentRecoveryNeeded;
        this.onAgentDeathDetected = onAgentDeathDetected;
    }

    internal SessionManager(
        string childUsername,
        string agentExePath,
        string pipeName,
        Action<ForegroundChanged> onForegroundChanged,
        Action<AgentHeartbeat> onHeartbeat,
        Action<StateSnapshot> onStateSnapshot,
        AgentLauncher agentLauncher,
        Action<IIpcChannel?>? onAgentChannelChanged = null)
        : this(childUsername, agentExePath, pipeName, onForegroundChanged, onHeartbeat, onStateSnapshot)
    {
        var injectedLauncher = agentLauncher ?? throw new ArgumentNullException(nameof(agentLauncher));
        this.launcherFactory = _ => injectedLauncher;
        this.onAgentChannelChanged = onAgentChannelChanged;
    }

    /// <summary>
    /// Starts watching for sessions and managing the agent.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        this.serviceStoppingToken = cancellationToken;
        var childSid = (SecurityIdentifier)new NTAccount(this.childUsername)
            .Translate(typeof(SecurityIdentifier));

        // Create the agent launcher
        this.launcherFactory = sessionId => new AgentLauncher(
            this.agentExePath,
            $"{this.pipeName}-{sessionId}",
            childSid,
            sid => string.Equals(sid, childSid.Value, StringComparison.Ordinal),
            message => this.HandleAgentMessage(sessionId, message),
            () => this.OnAgentDisconnected(sessionId));

        // Create the session watcher
        this.sessionWatcher = new SessionWatcher(
            this.childUsername,
            async sessionId => await this.OnSessionStarted(sessionId),
            sessionId => _ = this.OnSessionEnded(sessionId),
            sessionId => this.OnSessionLocked(sessionId),
            sessionId => this.OnSessionUnlocked(sessionId));

        // Start watching for sessions
        await this.sessionWatcher.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the session manager and kills the agent.
    /// </summary>
    public async Task StopAsync()
    {
        if (this.sessionWatcher != null)
        {
            await this.sessionWatcher.StopAsync();
            this.sessionWatcher.Dispose();
            this.sessionWatcher = null;
        }

        SessionRecord[] records;
        lock (this.recordsLock)
        {
            records = this.sessionRecords.Values.ToArray();
            this.sessionRecords.Clear();
            this.activeSessionId = null;
        }

        foreach (var record in records)
        {
            await record.StopAsync();
            record.Dispose();
        }
    }

    /// <summary>
    /// Sends a command to the agent.
    /// </summary>
    public async Task SendToAgentAsync(IIpcMessage message, CancellationToken cancellationToken = default)
    {
        SessionRecord? record;
        lock (this.recordsLock)
        {
            record = this.activeSessionId is int id && this.sessionRecords.TryGetValue(id, out var active)
                ? active
                : null;
        }

        if (record != null)
        {
            await record.Launcher.SendToAgentAsync(message, cancellationToken);
        }
    }

    internal async Task OnSessionStarted(int sessionId)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[SessionManager] Session started for child user: {sessionId}");

        SessionRecord record;
        lock (this.recordsLock)
        {
            this.activeSessionId = sessionId;
            if (!this.sessionRecords.TryGetValue(sessionId, out record!))
            {
                record = new SessionRecord(sessionId, this.launcherFactory!(sessionId));
                this.sessionRecords.Add(sessionId, record);
            }
        }

        await record.StartAsync(sessionId);
        this.NotifyAgentChannelChanged();
    }

    internal async Task OnSessionEnded(int sessionId)
    {
        System.Diagnostics.Debug.WriteLine(
            "[SessionManager] Session ended for child user.");

        SessionRecord? record;
        lock (this.recordsLock)
        {
            this.sessionRecords.Remove(sessionId, out record);
            if (this.activeSessionId == sessionId)
            {
                this.activeSessionId = this.sessionRecords.Keys.FirstOrDefault();
            }
        }

        if (record != null)
        {
            await record.StopAsync();
            record.Dispose();
            this.NotifyAgentChannelChanged();
        }
    }

    private void OnSessionLocked(int sessionId)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[SessionManager] Session locked: {sessionId}");
        // T06: Pause the usage counter when the session is locked
        this.usageAccumulator?.Pause();
    }

    private void OnSessionUnlocked(int sessionId)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[SessionManager] Session unlocked: {sessionId}");
        // T06: Resume the usage counter when the session is unlocked
        this.usageAccumulator?.Resume();

        // T09: Restore persistent overlay if active
        this.overlayPersistenceManager?.OnSessionUnlocked(this.RestorePersistentOverlay);
    }

    /// <summary>
    /// Restores the persistent overlay by sending ShowOverlay to the agent.
    /// </summary>
    private void RestorePersistentOverlay(string reason, string? ctaLabel)
    {
        var launcher = this.GetActiveLauncher();
        if (launcher == null || !launcher.IsAgentRunning)
        {
            System.Diagnostics.Debug.WriteLine(
                "[SessionManager] Cannot restore overlay: agent not running.");
            return;
        }

        var overlayMessage = new ShowOverlay(reason, ctaLabel);
        launcher.SendToAgentAsync(overlayMessage, CancellationToken.None)
            .ContinueWith(_ => System.Diagnostics.Debug.WriteLine(
                $"[SessionManager] Persistent overlay restored: {reason}"));
    }

    /// <summary>
    /// Default implementation of send overlay async (used when no custom function provided).
    /// </summary>
    private async Task DefaultSendOverlayAsync(string reason, string? ctaLabel, IIpcMessage? message)
    {
        var launcher = this.GetActiveLauncher();
        if (launcher != null && launcher.IsAgentRunning && message != null)
        {
            await launcher.SendToAgentAsync(message, CancellationToken.None);
        }
    }

    private void HandleAgentMessage(int sessionId, IIpcMessage message)
    {
        switch (message)
        {
            case ForegroundChanged fg:
                this.onForegroundChanged(fg);
                break;

            case AgentHeartbeat hb:
                this.onHeartbeat(hb);
                break;

            case AgentCommandCompleted completed:
                this.onAgentActionResult?.Invoke(completed.Result);
                break;

            case StateSnapshot snapshot:
                this.onStateSnapshot(snapshot);
                break;

            default:
                System.Diagnostics.Debug.WriteLine(
                    $"[SessionManager] Unknown message type: {message.MessageType}");
                break;
        }
    }

    private void OnAgentDisconnected(int sessionId)
    {
        System.Diagnostics.Debug.WriteLine(
            "[SessionManager] Agent disconnected.");

        // T10: Record agent death and trigger recovery
        this.healthMonitor?.RecordAgentDeath();

        // T13: Record tamper event
        this.onAgentDeathDetected?.Invoke();

        // If we have a recovery callback, trigger it
        this.onAgentRecoveryNeeded?.Invoke();
        this.onAgentChannelChanged?.Invoke(null);
    }

    internal async Task<bool> RecoverAgentAsync(CancellationToken cancellationToken = default)
    {
        SessionRecord? record;
        lock (this.recordsLock)
        {
            record = this.activeSessionId is int id && this.sessionRecords.TryGetValue(id, out var active)
                ? active
                : null;
        }

        if (record == null)
        {
            return false;
        }

        using var linkedCts = this.serviceStoppingToken.CanBeCanceled || cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.serviceStoppingToken)
            : null;

        var recovered = await record.RecoverAsync(linkedCts?.Token ?? cancellationToken);
        if (recovered)
        {
            this.NotifyAgentChannelChanged();
        }

        return recovered;
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            _ = this.StopAsync();
            this.disposed = true;
        }
    }

    private AgentLauncher? GetActiveLauncher()
    {
        lock (this.recordsLock)
        {
            return this.activeSessionId is int id && this.sessionRecords.TryGetValue(id, out var record)
                ? record.Launcher
                : null;
        }
    }

    private void NotifyAgentChannelChanged()
    {
        this.onAgentChannelChanged?.Invoke(this.AgentChannel);
    }

    private sealed class SessionRecord : IDisposable
    {
        private readonly SemaphoreSlim lifecycleGate = new(1, 1);
        private bool started;
        private Task<bool>? recoveryTask;

        public SessionRecord(int sessionId, AgentLauncher launcher)
        {
            this.SessionId = sessionId;
            this.Launcher = launcher;
        }

        public int SessionId { get; }
        public AgentLauncher Launcher { get; }

        public async Task StartAsync(int sessionId)
        {
            await this.lifecycleGate.WaitAsync();
            try
            {
                if (!this.started)
                {
                    this.started = await this.Launcher.LaunchAgentAsync(sessionId);
                }
            }
            finally
            {
                this.lifecycleGate.Release();
            }
        }

        public async Task StopAsync()
        {
            await this.lifecycleGate.WaitAsync();
            try
            {
                await this.Launcher.KillAgentAsync();
                this.started = false;
            }
            finally
            {
                this.lifecycleGate.Release();
            }
        }

        public Task<bool> RecoverAsync(CancellationToken cancellationToken)
        {
            lock (this)
            {
                return this.recoveryTask ??= RecoverCoreAsync(cancellationToken);
            }
        }

        private async Task<bool> RecoverCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                await this.lifecycleGate.WaitAsync(cancellationToken);
                try
                {
                    for (var attempt = 0; attempt < RecoveryAttemptLimit; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        this.started = await this.Launcher.LaunchAgentAsync(this.SessionId, cancellationToken);
                        if (this.started)
                        {
                            return true;
                        }

                        if (attempt < RecoveryAttemptLimit - 1)
                        {
                            await Task.Delay(RecoveryBackoffDelays[attempt], cancellationToken);
                        }
                    }

                    return false;
                }
                finally
                {
                    this.lifecycleGate.Release();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            finally
            {
                lock (this)
                {
                    this.recoveryTask = null;
                }
            }
        }

        public void Dispose()
        {
            this.Launcher.Dispose();
            this.lifecycleGate.Dispose();
        }
    }
}

/// <summary>
/// Main hosted service for ControlParental.
/// </summary>
public sealed class ControlParentalService : BackgroundService
{
    private readonly IScmController scmController;
    private readonly IPrivilegeInspector privilegeInspector;
    private readonly IAccountManager accountManager;
    private readonly IUsageAccumulator usageAccumulator;
    private readonly IUsageReconciler usageReconciler;
    private readonly IWorkstationLockManager workstationLockManager;
    private readonly IOverlayPersistenceManager overlayPersistenceManager;
    private readonly IServiceHealthMonitor healthMonitor;
    private readonly IServiceRecoveryManager recoveryManager;
    private readonly ITimeProvider timeProvider;
    private readonly IPolicyRepository policyRepository;
    private readonly IProcessTerminator processTerminator;
    private readonly IProtectedProcessReporter? protectedProcessReporter;
    private readonly IEnforcementLevelMonitor? enforcementLevelMonitor;
    private readonly IAntiTamperMonitor? antiTamperMonitor;
    private SessionManager? sessionManager;
    private IIpcChannel? boundAgentChannel;
    private IEnforcementEngine? enforcementEngine;
    private SessionSafetyLoop? safetyLoop;
    private Task bindingTask = Task.CompletedTask;
    private Task recoveryTask = Task.CompletedTask;
    private Task timeChangeTask = Task.CompletedTask;
    private readonly SemaphoreSlim bindingGate = new(1, 1);
    private long agentGeneration;
    private bool childIsAdmin;
    private TimeChangeReason? pendingTimeChange;

    public ControlParentalService(
        IScmController scmController,
        IPrivilegeInspector privilegeInspector,
        IAccountManager accountManager,
        IUsageAccumulator usageAccumulator,
        IUsageReconciler usageReconciler,
        IWorkstationLockManager workstationLockManager,
        IOverlayPersistenceManager overlayPersistenceManager,
        IServiceHealthMonitor healthMonitor,
        IServiceRecoveryManager recoveryManager,
        ITimeProvider timeProvider,
        IPolicyRepository policyRepository,
        IProcessTerminator processTerminator,
        IProtectedProcessReporter? protectedProcessReporter = null,
        IEnforcementLevelMonitor? enforcementLevelMonitor = null,
        IAntiTamperMonitor? antiTamperMonitor = null)
    {
        this.scmController = scmController;
        this.privilegeInspector = privilegeInspector;
        this.accountManager = accountManager;
        this.usageAccumulator = usageAccumulator;
        this.usageReconciler = usageReconciler;
        this.workstationLockManager = workstationLockManager;
        this.overlayPersistenceManager = overlayPersistenceManager;
        this.healthMonitor = healthMonitor;
        this.recoveryManager = recoveryManager;
        this.timeProvider = timeProvider;
        this.policyRepository = policyRepository;
        this.processTerminator = processTerminator;
        this.protectedProcessReporter = protectedProcessReporter;
        this.enforcementLevelMonitor = enforcementLevelMonitor;
        this.antiTamperMonitor = antiTamperMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.timeProvider.TimeChanged += this.OnTimeChanged;

        // T10: Start health monitoring
        await this.healthMonitor.StartAsync(stoppingToken);
        System.Diagnostics.Debug.WriteLine("[ControlParentalService] Health monitor started.");

        await this.ReportProtectedProcessStatusAsync(stoppingToken);

        // T12: Start enforcement level monitoring
        if (this.enforcementLevelMonitor != null)
        {
            await this.enforcementLevelMonitor.StartAsync(stoppingToken);
            System.Diagnostics.Debug.WriteLine("[ControlParentalService] Enforcement level monitor started.");
        }

        // T13: Start anti-tamper monitoring
        if (this.antiTamperMonitor != null)
        {
            await this.antiTamperMonitor.StartAsync(stoppingToken);
            System.Diagnostics.Debug.WriteLine("[ControlParentalService] Anti-tamper monitor started.");
        }

        // T37: Verify child's account is standard
        var childName = this.accountManager.GetChildAccountName();
        if (!string.IsNullOrEmpty(childName))
        {
            var isStandard = await this.accountManager.IsAccountStandardAsync(
                childName,
                stoppingToken);

            if (!isStandard)
            {
                this.childIsAdmin = true;
                System.Diagnostics.Debug.WriteLine(
                    $"[ControlParentalService] WARNING: Child account '{childName}' " +
                    "is an administrator. This is a DEGRADED state.");
            }
        }

        this.enforcementEngine = new EnforcementEngine(
            this.policyRepository, this.usageAccumulator, this.processTerminator,
            this.workstationLockManager, this.timeProvider);

        // T38: Start session management
        if (!string.IsNullOrEmpty(childName))
        {
            // T10: Recovery callback for when agent dies
            Action OnAgentRecoveryNeeded = () =>
            {
                this.recoveryTask = this.RequestAgentRecoveryAsync();
            };

            this.sessionManager = new SessionManager(
                childName,
                Program.AgentExePath,
                Program.IpcPipeName,
                fg => this.OnForegroundChanged(fg),
                hb => this.OnHeartbeat(hb),
                snapshot => this.OnStateSnapshot(snapshot),
                onAgentActionResult: result => this.safetyLoop?.AcceptResult(result),
                usageAccumulator: this.usageAccumulator,
                healthMonitor: this.healthMonitor,
                onAgentChannelChanged: channel => this.QueueAgentBinding(channel, stoppingToken),
                onAgentRecoveryNeeded: OnAgentRecoveryNeeded,
                onAgentDeathDetected: () => this.antiTamperMonitor?.RecordAgentDeath());

            if (this.recoveryManager is ServiceRecoveryManager serviceRecoveryManager)
            {
                serviceRecoveryManager.SetRecoverAgentFunc(
                    () => this.sessionManager.RecoverAgentAsync());
            }

            await this.sessionManager.StartAsync(stoppingToken);

            this.QueueAgentBinding(this.sessionManager.AgentChannel, stoppingToken);
            await this.bindingTask;
        }

        // T07: Start the usage reconciler (WMI event listener)
        await this.usageReconciler.StartAsync(stoppingToken);

        await this.usageAccumulator.StartAsync(stoppingToken);

        // T07/T10: Trigger initial backfill through the accumulator seam.
        await this.usageAccumulator.RequestBackfillAsync(stoppingToken);

        // TODO: T20 - Heartbeat and sync

        var healthLogTicks = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            this.safetyLoop?.Tick();
            if (++healthLogTicks >= 60)
            {
                healthLogTicks = 0;
                var status = this.recoveryManager.GetRecoveryStatus();
                System.Diagnostics.Debug.WriteLine($"[ControlParentalService] Health: {status.HealthLevel}");
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private void OnForegroundChanged(ForegroundChanged message)
    {
        // T06: Update usage counter with foreground change
        this.usageAccumulator.OnForegroundChanged(message.AppId);

        // T12: Record foreground change for enforcement level monitoring
        this.enforcementLevelMonitor?.RecordForegroundChange();

        this.safetyLoop?.Observe(message);
    }

    private void OnHeartbeat(AgentHeartbeat message)
    {
        // T20: Log the heartbeat and check agent health
        System.Diagnostics.Debug.WriteLine(
            $"[ControlParentalService] Agent heartbeat: {message.AgentId}, " +
            $"uptime={message.UpTimeMs}ms, overlay={message.IsOverlayVisible}");

        if (this.safetyLoop?.AcceptHeartbeat(message) == true)
        {
            this.healthMonitor.RecordAgentHeartbeat();
            this.enforcementLevelMonitor?.RecordAgentHeartbeat();
        }
    }

    private void OnStateSnapshot(StateSnapshot message)
    {
        // T12: Update the health status with the agent's state
        System.Diagnostics.Debug.WriteLine(
            $"[ControlParentalService] State snapshot: {message.AppId}, " +
            $"overlay={message.IsOverlayVisible}");
    }

    private void QueueAgentBinding(IIpcChannel? channel, CancellationToken cancellationToken) =>
        this.bindingTask = this.BindSessionAgentChannelAsync(channel, cancellationToken);

    private async Task BindSessionAgentChannelAsync(IIpcChannel? channel, CancellationToken cancellationToken)
    {
        await this.bindingGate.WaitAsync(cancellationToken);
        try
        {
            if (ReferenceEquals(this.boundAgentChannel, channel))
            {
                return;
            }

            this.boundAgentChannel = channel;

            if (channel is null)
            {
                if (this.safetyLoop != null)
                {
                    await this.safetyLoop.AgentDiedAsync(cancellationToken);
                }

                return;
            }

            var sessionId = this.sessionManager?.ActiveSessionId;
            if (sessionId is null || this.enforcementEngine is null ||
                this.healthMonitor is not IAuthoritativeHealthSink healthSink)
            {
                return;
            }

            if (this.safetyLoop is null)
            {
                var intentPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "ControlParental", $"overlay-intent-{sessionId}.json");
                this.safetyLoop = new SessionSafetyLoop(
                    sessionId.Value,
                    this.enforcementEngine.EnforceForegroundChangeAsync,
                    this.processTerminator.TerminateAsync,
                    new FileOverlayIntentStore(intentPath),
                    healthSink);
                if (this.pendingTimeChange is { } pendingReason)
                {
                    this.pendingTimeChange = null;
                    await this.safetyLoop.TimeChangedAsync(pendingReason, cancellationToken);
                }
                if (this.childIsAdmin)
                {
                    await this.safetyLoop.ChildAdminChangedAsync(true, cancellationToken);
                }
            }

            var generation = Interlocked.Increment(ref this.agentGeneration);
            await channel.SendAsync(new AgentAuthority(sessionId.Value, generation), cancellationToken);
            await this.safetyLoop.AttachAgentAsync(
                generation,
                (command, token) => new ValueTask(
                    channel.SendAsync(new AgentCommandRequest(command), token)),
                cancellationToken);

            if (this.usageAccumulator is UsageAccumulator accumulator)
            {
                accumulator.SetIpcChannel(channel);
            }

            if (this.usageReconciler is UsageReconciler reconciler)
            {
                reconciler.SetIpcChannel(channel);
            }

            if (this.workstationLockManager is WorkstationLockManager lockManager)
            {
                lockManager.SetCommandExecutor(
                    sessionId.Value,
                    generation,
                    (command, token) => new ValueTask<AgentActionResult>(
                        this.safetyLoop.ExecuteAgentCommandAsync(command, token)));
            }

            await this.usageAccumulator.RequestBackfillAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ControlParentalService] Failed to bind agent channel: {ex.Message}");
        }
        finally
        {
            this.bindingGate.Release();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        this.timeProvider.TimeChanged -= this.OnTimeChanged;

        // T10: Stop health monitoring
        await this.healthMonitor.StopAsync();

        // T12: Stop enforcement level monitoring
        await this.enforcementLevelMonitor?.StopAsync()!;

        // T13: Stop anti-tamper monitoring
        await this.antiTamperMonitor?.StopAsync()!;

        if (this.usageAccumulator is UsageAccumulator accumulator)
        {
            await accumulator.StopAsync(cancellationToken);
        }
        else
        {
            this.usageAccumulator.Stop();
        }
        this.usageReconciler.Stop();
        await this.bindingTask;
        await this.recoveryTask;
        await this.timeChangeTask;
        if (this.sessionManager != null)
        {
            await this.sessionManager.StopAsync();
            this.sessionManager.Dispose();
        }

        if (this.safetyLoop != null)
        {
            await this.safetyLoop.DisposeAsync();
        }
        this.bindingGate.Dispose();

        await base.StopAsync(cancellationToken);
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs args)
    {
        if (this.safetyLoop is { } loop)
        {
            this.timeChangeTask = loop.TimeChangedAsync(args.Reason);
        }
        else
        {
            this.pendingTimeChange = args.Reason;
        }
    }

    private async Task RequestAgentRecoveryAsync()
    {
        var succeeded = await this.recoveryManager.RequestAgentRecoveryAsync("Agent died unexpectedly");
        System.Diagnostics.Debug.WriteLine(
            $"[ControlParentalService] Agent recovery requested. Success: {succeeded}");
    }

    private async Task ReportProtectedProcessStatusAsync(CancellationToken cancellationToken)
    {
        if (this.protectedProcessReporter is null)
        {
            return;
        }

        try
        {
            var status = await this.protectedProcessReporter.GetStatusDescriptionAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[ControlParentalService] PPL status: {status}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ControlParentalService] Failed to report PPL status: {ex.Message}");
        }
    }
}
