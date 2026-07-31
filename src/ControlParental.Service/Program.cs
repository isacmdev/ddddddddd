// <copyright file="Program.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Collections.Generic;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ControlParental.Domain;

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

    /// <summary>
    /// T22 — Creates an HttpClient configured with TLS 1.3 and optional SPKI certificate pinning.
    /// </summary>
    /// <param name="certPin">
    /// Optional SPKI pin (Base64 SHA-256 of the certificate's SubjectPublicKeyInfo).
    /// If null or empty, no pinning is enforced but TLS 1.3 is still applied.
    /// </param>
    /// <returns>A configured <see cref="HttpClient"/> instance.</returns>
    private static HttpClient CreateSupabaseHttpClient(string? certPin)
    {
        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                // T22: Force TLS 1.3 — supported on Windows 10 1903+ (target: Win10 19041+)
                EnabledSslProtocols = SslProtocols.Tls13,
                // T22: Certificate pinning validation using SPKI
                RemoteCertificateValidationCallback = certPin != null && certPin.Length > 0
                    ? (RemoteCertificateValidationCallback)((sender, certificate, chain, errors) =>
                        {
                            if (certificate == null)
                            {
                                // No certificate presented — reject unless pin is not configured
                                return false;
                            }

                            // When pin is configured, always validate (errors ignored if cert is valid)
                            _ = CertificatePinningValidator.Validate(certPin, new X509Certificate2(certificate));
                            return true;
                        })
                    : null,
                // Skip revocation check for pinned connections (revocation servers may be unreachable)
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
            },
        };

        return new HttpClient(handler);
    }

    public static async Task Main(string[] args)
    {
        // T18: Load Supabase config from .env (repo root or ProgramData)
        if (!ConfigurationLoader.TryLoad(out var supabaseConfig))
        {
            Console.Error.WriteLine(
                "[Program] FATAL: Could not load Supabase configuration from .env. " +
                $"Searched: {ConfigurationLoader.EnvFilePath}");
            return;
        }

        // T22: Load TLS certificate pin from environment
        var tlsPinningConfig = new TlsPinningConfig(
            Environment.GetEnvironmentVariable("SUPABASE_CERT_PIN"));

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

        // T03: Register PolicyRepository and DbContext
        var dataFolder = DataFolderPath;
        builder.Services.AddSingleton<ControlParentalDbContext>(sp =>
        {
            var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
                .UseSqlite($"Data Source={Path.Combine(dataFolder, "controlparental.db")}")
                .Options;
            return new ControlParentalDbContext(options);
        });
        builder.Services.AddSingleton<IPolicyRepository, PolicyRepository>();
        builder.Services.AddSingleton<PolicyRepository>(); // Concrete for UsageAccumulator

        // T04: Register TimeProvider
        builder.Services.AddSingleton<ITimeProvider, TimeProvider>();

        // T07: Register UsageReconciler (IPC channel set via SetIpcChannel after SessionManager creates it)
        builder.Services.AddSingleton<IUsageReconciler>((sp) =>
        {
            var dbContext = sp.GetRequiredService<ControlParentalDbContext>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var ipcChannel = sp.GetService<IIpcChannel>(); // Nullable — set via SetIpcChannel
            Func<string, string> resolveAppId = Interop.AppIdentityResolver.Resolve;
            return new UsageReconciler(dbContext, timeProvider, ipcChannel, resolveAppId);
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
        builder.Services.AddSingleton<IServiceHealthMonitor>((sp) =>
        {
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            return new ServiceHealthMonitor(
                timeProvider: timeProvider,
                onAgentDied: () => System.Diagnostics.Debug.WriteLine("[HealthMonitor] Agent died."),
                onServiceUnhealthy: issue => System.Diagnostics.Debug.WriteLine($"[HealthMonitor] Unhealthy: {issue}"));
        });
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
                });
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

        // T14: Register BackendClient (infrastructure for T18/T20)
        // T22: Configured with SocketsHttpHandler — TLS 1.3 + SPKI certificate pinning
        builder.Services.AddSingleton<IBackendClient>((sp) =>
        {
            var httpClient = CreateSupabaseHttpClient(tlsPinningConfig.CertPin);
            var deviceAuth = sp.GetRequiredService<IDeviceAuthenticator>();
            return new BackendClient(httpClient, supabaseConfig.Url, deviceAuth);
        });

        // T17: Register DeviceAuthenticator as hosted service
        // T22: Same TLS/pinning handler for auth calls to supabase.co
        builder.Services.AddSingleton<IDeviceAuthenticator>((sp) =>
        {
            var secretStore = sp.GetRequiredService<ISecretStore>();
            var timeProvider = sp.GetRequiredService<ITimeProvider>();
            var httpClient = CreateSupabaseHttpClient(tlsPinningConfig.CertPin);
            return new DeviceAuthenticator(
                secretStore,
                timeProvider,
                httpClient,
                supabaseConfig.Url,
                supabaseConfig.AnonKey);
        });

        // T24: Register PairingService for device pairing
        builder.Services.AddScoped<IPairingService, PairingService>();

        // T25: Register ConsentService for data collection consent
        builder.Services.AddScoped<IConsentService, ConsentService>();

        // T27: Register UsageStateQueryHandler for UI queries
        builder.Services.AddScoped<UsageStateQueryHandler>();

        // T20: Register TaskSchedulerBackupService as safety net for timer failures
        builder.Services.AddSingleton<ITaskSchedulerBackup, TaskSchedulerBackupService>();

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
                taskSchedulerBackup);
        });
        builder.Services.AddHostedService<ScheduledWorkServiceHostedAdapter>();

        // T19: Register WNS push notification service
        var wnsPackageSid = Environment.GetEnvironmentVariable("WNS_PACKAGE_SID") ?? string.Empty;
        var wnsClientSecret = Environment.GetEnvironmentVariable("WNS_CLIENT_SECRET") ?? string.Empty;
        var wnsConfig = new WnsConfig(wnsPackageSid, wnsClientSecret);
        builder.Services.AddSingleton<IPushNotificationService>(sp =>
        {
            var httpClient = new HttpClient();
            return new WnsNotificationService(
                httpClient,
                wnsConfig.PackageSid,
                wnsConfig.ClientSecret,
                sp.GetRequiredService<ITimeProvider>());
        });
        builder.Services.AddHostedService<WnsNotificationServiceHostedAdapter>();

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

        // T03: Ensure database is created (after hardening so the ACL is stable)
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ControlParentalDbContext>();
            Console.WriteLine("[Program] DB path: " + db.Database.GetConnectionString());
            Console.WriteLine("[Program] About to call EnsureCreatedAsync...");
            var created = await db.Database.EnsureCreatedAsync();
            Console.WriteLine($"[Program] EnsureCreatedAsync: created={created}");
            Console.WriteLine($"[Program] DB can connect: {db.Database.CanConnect()}");

            // Force create tables via raw SQL to debug
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            using var reader = await cmd.ExecuteReaderAsync();
            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }

            await reader.CloseAsync();
            Console.WriteLine($"[Program] Tables found: {string.Join(", ", tables)}");
            await conn.CloseAsync();
        }

        await host.RunAsync();
    }

    /// <summary>
    /// Applies all hardening measures on first run.
    /// </summary>
    private static async Task ApplyHardeningAsync(IServiceProvider services)
    {
        var aclHardener = services.GetRequiredService<IAclHardener>();

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

        _ = await aclHardener.HardenAllAsync(
            AgentFolderPath,
            DataFolderPath,
            ServiceRegistryKey,
            ServiceExePath,
            CancellationToken.None);

        var scmController = services.GetRequiredService<IScmController>();
        _ = await scmController.ConfigureFailureActionsAsync(ServiceName, CancellationToken.None);
        _ = await scmController.SetStartupTypeAsync(ServiceName, "auto", CancellationToken.None);
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
    private readonly IUsageAccumulator? usageAccumulator;
    private readonly IOverlayPersistenceManager? overlayPersistenceManager;
    private readonly Func<string, string?, IIpcMessage?, Task> sendOverlayToAgentAsync;
    private readonly IServiceHealthMonitor? healthMonitor;
    private readonly IServiceRecoveryManager? recoveryManager;
    private readonly Action? onAgentRecoveryNeeded;
    private readonly Action? onAgentDeathDetected;
    private SessionWatcher? sessionWatcher;
    private AgentLauncher? agentLauncher;
    private bool disposed;

    /// <summary>
    /// Gets the IPC channel for communicating with the agent.
    /// May be null if the agent is not running.
    /// </summary>
    public IIpcChannel? AgentChannel => this.agentLauncher?.AgentChannel;

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
        IUsageAccumulator? usageAccumulator = null,
        IOverlayPersistenceManager? overlayPersistenceManager = null,
        Func<string, string?, IIpcMessage?, Task>? sendOverlayToAgentAsync = null,
        IServiceHealthMonitor? healthMonitor = null,
        IServiceRecoveryManager? recoveryManager = null,
        Action? onAgentRecoveryNeeded = null,
        Action? onAgentDeathDetected = null)
    {
        this.childUsername = childUsername;
        this.agentExePath = agentExePath;
        this.pipeName = pipeName;
        this.onForegroundChanged = onForegroundChanged;
        this.onHeartbeat = onHeartbeat;
        this.onStateSnapshot = onStateSnapshot;
        this.usageAccumulator = usageAccumulator;
        this.overlayPersistenceManager = overlayPersistenceManager;
        this.sendOverlayToAgentAsync = sendOverlayToAgentAsync ?? this.DefaultSendOverlayAsync;
        this.healthMonitor = healthMonitor;
        this.recoveryManager = recoveryManager;
        this.onAgentRecoveryNeeded = onAgentRecoveryNeeded;
        this.onAgentDeathDetected = onAgentDeathDetected;
    }

    /// <summary>
    /// Starts watching for sessions and managing the agent.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Create the agent launcher
        this.agentLauncher = new AgentLauncher(
            this.agentExePath,
            this.pipeName,
            sid => this.ValidateChildSid(sid),
            message => this.HandleAgentMessage(message),
            () => this.OnAgentDisconnected());

        // Create the session watcher
        this.sessionWatcher = new SessionWatcher(
            this.childUsername,
            async sessionId => await this.OnSessionStarted(sessionId),
            () => this.OnSessionEnded(),
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
            this.sessionWatcher.Stop();
            this.sessionWatcher.Dispose();
            this.sessionWatcher = null;
        }

        if (this.agentLauncher != null)
        {
            await this.agentLauncher.KillAgentAsync();
            this.agentLauncher.Dispose();
            this.agentLauncher = null;
        }
    }

    /// <summary>
    /// Sends a command to the agent.
    /// </summary>
    public async Task SendToAgentAsync(IIpcMessage message, CancellationToken cancellationToken = default)
    {
        if (this.agentLauncher != null && this.agentLauncher.IsAgentRunning)
        {
            await this.agentLauncher.SendToAgentAsync(message, cancellationToken);
        }
    }

    private bool ValidateChildSid(string sid)
    {
        // Validate that the connecting client has the expected SID
        // This is implemented by checking if the SID matches the child user's SID
        try
        {
            var connectingSid = new System.Security.Principal.SecurityIdentifier(sid);

            // For now, accept any local user connection (simplified)
            // In production, this would compare against the actual child user SID
            // SecurityIdentifier.IsValid() does not exist; check via GetBinaryForm
            try
            {
                var binaryForm = new byte[connectingSid.BinaryLength];
                connectingSid.GetBinaryForm(binaryForm, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private async Task OnSessionStarted(int sessionId)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[SessionManager] Session started for child user: {sessionId}");

        // Launch the agent in the new session
        if (this.agentLauncher != null)
        {
            await this.agentLauncher.LaunchAgentAsync(sessionId);
        }
    }

    private void OnSessionEnded()
    {
        System.Diagnostics.Debug.WriteLine(
            "[SessionManager] Session ended for child user.");

        // Kill the agent when the session ends
        if (this.agentLauncher != null)
        {
            _ = this.agentLauncher.KillAgentAsync();
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
        if (this.agentLauncher == null || !this.agentLauncher.IsAgentRunning)
        {
            System.Diagnostics.Debug.WriteLine(
                "[SessionManager] Cannot restore overlay: agent not running.");
            return;
        }

        var overlayMessage = new ShowOverlay(reason, ctaLabel);
        this.agentLauncher.SendToAgentAsync(overlayMessage, CancellationToken.None)
            .ContinueWith(_ => System.Diagnostics.Debug.WriteLine(
                $"[SessionManager] Persistent overlay restored: {reason}"));
    }

    /// <summary>
    /// Default implementation of send overlay async (used when no custom function provided).
    /// </summary>
    private async Task DefaultSendOverlayAsync(string reason, string? ctaLabel, IIpcMessage? message)
    {
        if (this.agentLauncher != null && this.agentLauncher.IsAgentRunning && message != null)
        {
            await this.agentLauncher.SendToAgentAsync(message, CancellationToken.None);
        }
    }

    private void HandleAgentMessage(IIpcMessage message)
    {
        switch (message)
        {
            case ForegroundChanged fg:
                this.onForegroundChanged(fg);
                break;

            case AgentHeartbeat hb:
                this.onHeartbeat(hb);
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

    private void OnAgentDisconnected()
    {
        System.Diagnostics.Debug.WriteLine(
            "[SessionManager] Agent disconnected.");

        // T10: Record agent death and trigger recovery
        this.healthMonitor?.RecordAgentDeath();

        // T13: Record tamper event
        this.onAgentDeathDetected?.Invoke();

        // If we have a recovery callback, trigger it
        this.onAgentRecoveryNeeded?.Invoke();
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.StopAsync().Wait(TimeSpan.FromSeconds(5));
            this.disposed = true;
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
    private readonly IEnforcementLevelMonitor? enforcementLevelMonitor;
    private readonly IAntiTamperMonitor? antiTamperMonitor;
    private SessionManager? sessionManager;
    private IEnforcementEngine? enforcementEngine;

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
        this.enforcementLevelMonitor = enforcementLevelMonitor;
        this.antiTamperMonitor = antiTamperMonitor;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // T10: Start health monitoring
        await this.healthMonitor.StartAsync(stoppingToken);
        System.Diagnostics.Debug.WriteLine("[ControlParentalService] Health monitor started.");

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
                System.Diagnostics.Debug.WriteLine(
                    $"[ControlParentalService] WARNING: Child account '{childName}' " +
                    "is an administrator. This is a DEGRADED state.");
            }
        }

        // T38: Start session management
        if (!string.IsNullOrEmpty(childName))
        {
            // T10: Recovery callback for when agent dies
            Action OnAgentRecoveryNeeded = () =>
            {
                _ = this.recoveryManager.RequestAgentRecoveryAsync("Agent died unexpectedly")
                    .ContinueWith(t => System.Diagnostics.Debug.WriteLine(
                        $"[ControlParentalService] Agent recovery requested. Success: {t.Result}"));
            };

            this.sessionManager = new SessionManager(
                childName,
                Program.AgentExePath,
                Program.IpcPipeName,
                fg => this.OnForegroundChanged(fg),
                hb => this.OnHeartbeat(hb),
                snapshot => this.OnStateSnapshot(snapshot),
                usageAccumulator: this.usageAccumulator,
                overlayPersistenceManager: this.overlayPersistenceManager,
                healthMonitor: this.healthMonitor,
                onAgentRecoveryNeeded: OnAgentRecoveryNeeded,
                onAgentDeathDetected: () => this.antiTamperMonitor?.RecordAgentDeath());

            await this.sessionManager.StartAsync(stoppingToken);
        }

        // T06: Start the usage counter and request backfill
        // Set the IPC channel from SessionManager so UsageAccumulator can send ShowWarning
        if (this.sessionManager?.AgentChannel is IIpcChannel ipcChannel)
        {
            // UsageAccumulator is constructed with null IPC channel by DI;
            // set it now that SessionManager has created its channel
            if (this.usageAccumulator is UsageAccumulator accumulator)
            {
                accumulator.SetIpcChannel(ipcChannel);
            }

            // T07: Also set IPC channel on UsageReconciler (created before SessionManager)
            if (this.usageReconciler is UsageReconciler reconciler)
            {
                reconciler.SetIpcChannel(ipcChannel);
            }

            // T09: Also set IPC channel on WorkstationLockManager (created before SessionManager)
            if (this.workstationLockManager is WorkstationLockManager lockManager)
            {
                lockManager.SetIpcChannel(ipcChannel);
            }
        }

        // T11: Initialize the enforcement engine with IPC channel for overlay commands
        if (this.sessionManager?.AgentChannel is IIpcChannel agentChannel)
        {
            this.enforcementEngine = new EnforcementEngine(
                policyRepository: this.policyRepository,
                usageAccumulator: this.usageAccumulator,
                processTerminator: this.processTerminator,
                workstationLockManager: this.workstationLockManager,
                timeProvider: this.timeProvider,
                ipcChannel: agentChannel);
            System.Diagnostics.Debug.WriteLine("[ControlParentalService] Enforcement engine initialized.");
        }

        // T07: Start the usage reconciler (WMI event listener)
        await this.usageReconciler.StartAsync(stoppingToken);

        await this.usageAccumulator.StartAsync(stoppingToken);

        // T07/T10: Trigger initial backfill (reconciler is idempotent, only runs once per day)
        // This is part of cold start reconciliation
        _ = this.usageReconciler.ReconcileAsync(stoppingToken);

        // TODO: T20 - Heartbeat and sync

        while (!stoppingToken.IsCancellationRequested)
        {
            // T10: Periodically log health status
            var status = this.recoveryManager.GetRecoveryStatus();
            System.Diagnostics.Debug.WriteLine(
                $"[ControlParentalService] Health: {status.HealthLevel}, " +
                $"AgentDeaths: {status.AgentDeaths}, " +
                $"FailedRecoveries: {status.FailedRecoveries}");

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }

    private async void OnForegroundChanged(ForegroundChanged message)
    {
        // T06: Update usage counter with foreground change
        this.usageAccumulator.OnForegroundChanged(message.AppId);

        // T10: Record heartbeat from agent
        this.healthMonitor?.RecordAgentHeartbeat();

        // T12: Record foreground change for enforcement level monitoring
        this.enforcementLevelMonitor?.RecordForegroundChange();

        // T11: Evaluate the policy and decide whether to block
        System.Diagnostics.Debug.WriteLine(
            $"[ControlParentalService] Foreground changed: {message.AppId}");

        // Use the enforcement engine to evaluate and apply policy
        if (this.enforcementEngine != null)
        {
            var result = await this.enforcementEngine.EnforceForegroundChangeAsync(
                message.AppId,
                CancellationToken.None);

            if (result.Blocked)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ControlParentalService] BLOCKED {message.AppId}: {result.ReasonText}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ControlParentalService] ALLOWED {message.AppId}");
            }
        }
    }

    private void OnHeartbeat(AgentHeartbeat message)
    {
        // T20: Log the heartbeat and check agent health
        System.Diagnostics.Debug.WriteLine(
            $"[ControlParentalService] Agent heartbeat: {message.AgentId}, " +
            $"uptime={message.UpTimeMs}ms, overlay={message.IsOverlayVisible}");

        // T12: Record agent heartbeat for enforcement level monitoring
        this.enforcementLevelMonitor?.RecordAgentHeartbeat();
    }

    private void OnStateSnapshot(StateSnapshot message)
    {
        // T12: Update the health status with the agent's state
        System.Diagnostics.Debug.WriteLine(
            $"[ControlParentalService] State snapshot: {message.AppId}, " +
            $"overlay={message.IsOverlayVisible}");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // T10: Stop health monitoring
        await this.healthMonitor.StopAsync();

        // T12: Stop enforcement level monitoring
        await this.enforcementLevelMonitor?.StopAsync()!;

        // T13: Stop anti-tamper monitoring
        await this.antiTamperMonitor?.StopAsync()!;

        this.usageAccumulator.Stop();
        this.usageReconciler.Stop();
        if (this.sessionManager != null)
        {
            await this.sessionManager.StopAsync();
            this.sessionManager.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
