// <copyright file="ServiceHealthMonitor.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T10 — Implementación de IServiceHealthMonitor.
/// Monitorea heartbeats del agente y detecta cuando muere.
/// </summary>
public sealed class ServiceHealthMonitor : IServiceHealthMonitor, IDisposable
{
    private readonly TimeSpan agentHeartbeatTimeout;
    private readonly TimeSpan healthCheckInterval;
    private readonly ITimeProvider timeProvider;
    private readonly Action onAgentDied;
    private readonly Action<string> onServiceUnhealthy;

    private Timer? healthCheckTimer;
    private long? lastAgentHeartbeatTicks;
    private int agentRestartCount;
    private bool isRunning;
    private bool disposed;

    private const int MaxAgentDeathsBeforeAlert = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceHealthMonitor"/> class.
    /// </summary>
    /// <param name="timeProvider">Time provider.</param>
    /// <param name="onAgentDied">Callback cuando el agente muere.</param>
    /// <param name="onServiceUnhealthy">Callback cuando el servicio está enfermo.</param>
    /// <param name="agentHeartbeatTimeout">Timeout para detectar agente muerto (default 30s).</param>
    /// <param name="healthCheckInterval">Intervalo de verificación de salud (default 5s).</param>
    public ServiceHealthMonitor(
        ITimeProvider timeProvider,
        Action onAgentDied,
        Action<string> onServiceUnhealthy,
        TimeSpan? agentHeartbeatTimeout = null,
        TimeSpan? healthCheckInterval = null)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.onAgentDied = onAgentDied ?? throw new ArgumentNullException(nameof(onAgentDied));
        this.onServiceUnhealthy = onServiceUnhealthy ?? throw new ArgumentNullException(nameof(onServiceUnhealthy));
        this.agentHeartbeatTimeout = agentHeartbeatTimeout ?? TimeSpan.FromSeconds(30);
        this.healthCheckInterval = healthCheckInterval ?? TimeSpan.FromSeconds(5);
    }

    /// <inheritdoc />
    public bool IsAgentHealthy
    {
        get
        {
            if (!this.lastAgentHeartbeatTicks.HasValue)
            {
                return false;
            }

            var elapsed = this.timeProvider.MonotonicNow - this.lastAgentHeartbeatTicks.Value;
            // Convert ticks to TimeSpan for comparison
            var elapsedTime = TimeSpan.FromTicks(elapsed);
            return elapsedTime < this.agentHeartbeatTimeout;
        }
    }

    /// <inheritdoc />
    public bool IsServiceHealthy => this.isRunning && !this.disposed;

    /// <inheritdoc />
    public DateTimeOffset? LastAgentHeartbeat
    {
        get
        {
            if (!this.lastAgentHeartbeatTicks.HasValue)
            {
                return null;
            }

            // Approximate conversion for display purposes
            // This is not exact but useful for logging
            return DateTimeOffset.UtcNow - TimeSpan.FromTicks(
                this.timeProvider.MonotonicNow - this.lastAgentHeartbeatTicks.Value);
        }
    }

    /// <inheritdoc />
    public int AgentRestartCount => this.agentRestartCount;

    /// <inheritdoc />
    public event EventHandler<AgentDiedEventArgs>? AgentDied;

    /// <inheritdoc />
    public event EventHandler<ServiceUnhealthyEventArgs>? ServiceBecameUnhealthy;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(ServiceHealthMonitor));
        }

        if (this.isRunning)
        {
            return Task.CompletedTask;
        }

        this.isRunning = true;
        this.healthCheckTimer = new Timer(
            this.HealthCheckCallback,
            null,
            this.healthCheckInterval,
            this.healthCheckInterval);

        System.Diagnostics.Debug.WriteLine("[ServiceHealthMonitor] Started.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        this.isRunning = false;
        this.healthCheckTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        System.Diagnostics.Debug.WriteLine("[ServiceHealthMonitor] Stopped.");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void RecordAgentHeartbeat()
    {
        this.lastAgentHeartbeatTicks = this.timeProvider.MonotonicNow;
        System.Diagnostics.Debug.WriteLine("[ServiceHealthMonitor] Agent heartbeat recorded.");
    }

    /// <inheritdoc />
    public void RecordAgentDeath()
    {
        this.agentRestartCount++;
        var timestampTicks = this.timeProvider.MonotonicNow;

        System.Diagnostics.Debug.WriteLine(
            $"[ServiceHealthMonitor] Agent death recorded. Count: {this.agentRestartCount}");

        // Fire event
        this.AgentDied?.Invoke(
            this,
            new AgentDiedEventArgs
            {
                DeathCount = this.agentRestartCount,
                Timestamp = DateTimeOffset.UtcNow,
                Reason = null,
            });

        // Callback
        this.onAgentDied();
    }

    /// <inheritdoc />
    public void ResetAgentRestartCount()
    {
        this.agentRestartCount = 0;
        System.Diagnostics.Debug.WriteLine("[ServiceHealthMonitor] Agent restart count reset.");
    }

    /// <summary>
    /// Obtiene el estado de salud como enumerable de issues.
    /// </summary>
    public IReadOnlyList<string> GetHealthIssues()
    {
        var issues = new List<string>();

        if (!this.isRunning)
        {
            issues.Add("Monitor is not running");
        }

        if (this.lastAgentHeartbeatTicks.HasValue)
        {
            var elapsed = this.timeProvider.MonotonicNow - this.lastAgentHeartbeatTicks.Value;
            var elapsedTime = TimeSpan.FromTicks(elapsed);
            if (elapsedTime >= this.agentHeartbeatTimeout)
            {
                issues.Add($"Agent heartbeat overdue by {elapsedTime.TotalSeconds:F1}s");
            }
        }
        else
        {
            issues.Add("No agent heartbeat received yet");
        }

        if (this.agentRestartCount >= MaxAgentDeathsBeforeAlert)
        {
            issues.Add($"Agent died {this.agentRestartCount} times - possible instability");
        }

        return issues;
    }

    /// <summary>
    /// Indica si el agente está en estado crítico (muchas muertes).
    /// </summary>
    public bool IsAgentInCriticalState => this.agentRestartCount >= MaxAgentDeathsBeforeAlert;

    private void HealthCheckCallback(object? state)
    {
        if (!this.isRunning || this.disposed)
        {
            return;
        }

        try
        {
            this.PerformHealthCheck();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ServiceHealthMonitor] Health check error: {ex.Message}");
        }
    }

    private void PerformHealthCheck()
    {
        // Check agent health
        if (this.lastAgentHeartbeatTicks.HasValue && !this.IsAgentHealthy)
        {
            var elapsed = TimeSpan.FromTicks(
                this.timeProvider.MonotonicNow - this.lastAgentHeartbeatTicks.Value);
            System.Diagnostics.Debug.WriteLine(
                $"[ServiceHealthMonitor] Agent heartbeat overdue. Elapsed: {elapsed.TotalSeconds:F1}s");

            // Record death and trigger recovery
            this.RecordAgentDeath();
        }

        // Check service health issues
        var issues = this.GetHealthIssues();
        if (issues.Count > 0)
        {
            this.onServiceUnhealthy(string.Join("; ", issues));

            this.ServiceBecameUnhealthy?.Invoke(
                this,
                new ServiceUnhealthyEventArgs
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Issue = string.Join("; ", issues),
                });
        }
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
            this.isRunning = false;
            this.healthCheckTimer?.Dispose();
            this.healthCheckTimer = null;
        }

        GC.SuppressFinalize(this);
    }
}
