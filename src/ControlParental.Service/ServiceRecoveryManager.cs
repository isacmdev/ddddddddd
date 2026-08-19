// <copyright file="ServiceRecoveryManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T10 — Implementación de IServiceRecoveryManager.
/// Maneja la recuperación del agente cuando muere.
/// </summary>
public sealed class ServiceRecoveryManager : IServiceRecoveryManager
{
    private readonly IServiceHealthMonitor healthMonitor;
    private Func<Task<bool>> recoverAgentFunc;
    private readonly Action<string> onRecoveryFailed;
    private readonly Action onRecoverySucceeded;

    private int failedRecoveryAttempts;
    private string? lastRecoveryError;
    private bool isInRecoveryMode;
    private DateTimeOffset? lastRecoveryAttempt;
    private readonly object lockObject = new();

    private const int MaxConsecutiveFailures = 3;
    private const int RecoveryCooldownSeconds = 5;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceRecoveryManager"/> class.
    /// </summary>
    /// <param name="healthMonitor">Health monitor.</param>
    /// <param name="recoverAgentFunc">Function to call to recover the agent.</param>
    /// <param name="onRecoveryFailed">Callback when recovery fails.</param>
    /// <param name="onRecoverySucceeded">Callback when recovery succeeds.</param>
    public ServiceRecoveryManager(
        IServiceHealthMonitor healthMonitor,
        Func<Task<bool>> recoverAgentFunc,
        Action<string> onRecoveryFailed,
        Action onRecoverySucceeded)
    {
        this.healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        this.recoverAgentFunc = recoverAgentFunc ?? throw new ArgumentNullException(nameof(recoverAgentFunc));
        this.onRecoveryFailed = onRecoveryFailed ?? throw new ArgumentNullException(nameof(onRecoveryFailed));
        this.onRecoverySucceeded = onRecoverySucceeded ?? throw new ArgumentNullException(nameof(onRecoverySucceeded));

        // Subscribe to health monitor events
        this.healthMonitor.AgentDied += this.OnAgentDied;
    }

    internal void SetRecoverAgentFunc(Func<Task<bool>> recoverAgentFunc)
    {
        this.recoverAgentFunc = recoverAgentFunc ??
            throw new ArgumentNullException(nameof(recoverAgentFunc));
    }

    /// <inheritdoc />
    public int FailedRecoveryAttempts
    {
        get
        {
            lock (this.lockObject)
            {
                return this.failedRecoveryAttempts;
            }
        }
    }

    /// <inheritdoc />
    public string? LastRecoveryError
    {
        get
        {
            lock (this.lockObject)
            {
                return this.lastRecoveryError;
            }
        }
    }

    /// <inheritdoc />
    public bool IsInRecoveryMode
    {
        get
        {
            lock (this.lockObject)
            {
                return this.isInRecoveryMode;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> RequestAgentRecoveryAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!this.TryBeginRecoveryAttempt(out var beginFailureReason))
        {
            this.SetLastRecoveryError(beginFailureReason);
            return false;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ServiceRecoveryManager] Attempting agent recovery. Reason: {reason}");

            var success = await this.recoverAgentFunc();

            if (success)
            {
                this.CompleteSuccessfulRecovery();

                this.healthMonitor.ResetAgentRestartCount();
                this.onRecoverySucceeded();
                System.Diagnostics.Debug.WriteLine("[ServiceRecoveryManager] Recovery succeeded.");
                return true;
            }
            else
            {
                this.CompleteFailedRecovery("Recovery function returned false");

                this.onRecoveryFailed(this.lastRecoveryError!);
                return false;
            }
        }
        catch (Exception ex)
        {
            this.CompleteFailedRecovery(ex.Message);

            this.onRecoveryFailed(ex.Message);
            return false;
        }
        finally
        {
            this.ExitRecoveryMode();
        }
    }

    /// <inheritdoc />
    public void ResetRecoveryState()
    {
        lock (this.lockObject)
        {
            this.failedRecoveryAttempts = 0;
            this.lastRecoveryError = null;
            this.isInRecoveryMode = false;
        }

        this.healthMonitor.ResetAgentRestartCount();
        System.Diagnostics.Debug.WriteLine("[ServiceRecoveryManager] Recovery state reset.");
    }

    /// <inheritdoc />
    public ServiceRecoveryStatus GetRecoveryStatus()
    {
        lock (this.lockObject)
        {
            var agentDeaths = this.healthMonitor.AgentRestartCount;
            var failedRecoveries = this.failedRecoveryAttempts;

            // Recovery counters are historical evidence; only the authoritative
            // health projection may establish current healthy protection.
            var isHealthy = this.healthMonitor.IsServiceHealthy &&
                            agentDeaths == 0 &&
                            failedRecoveries == 0;

            ServiceHealthLevel healthLevel;
            string statusMessage;

            if (isHealthy)
            {
                healthLevel = ServiceHealthLevel.Healthy;
                statusMessage = "Service is healthy";
            }
            else if (failedRecoveries >= MaxConsecutiveFailures)
            {
                healthLevel = ServiceHealthLevel.Critical;
                statusMessage = $"Critical: {failedRecoveries} consecutive recovery failures";
            }
            else
            {
                healthLevel = ServiceHealthLevel.Degraded;
                statusMessage = $"Degraded: {agentDeaths} agent deaths, {failedRecoveries} failed recoveries";
            }

            return new ServiceRecoveryStatus
            {
                IsHealthy = isHealthy,
                AgentDeaths = agentDeaths,
                FailedRecoveries = failedRecoveries,
                StatusMessage = statusMessage,
                HealthLevel = healthLevel,
            };
        }
    }

    private void OnAgentDied(object? sender, AgentDiedEventArgs e)
    {
        // Automatically attempt recovery when agent dies
        _ = this.RequestAgentRecoveryAsync($"Agent died (count: {e.DeathCount})");
    }

    private bool TryBeginRecoveryAttempt(out string? failureReason)
    {
        lock (this.lockObject)
        {
            if (this.isInRecoveryMode)
            {
                failureReason = "Already in recovery mode";
                return false;
            }

            if (this.lastRecoveryAttempt.HasValue)
            {
                var elapsed = DateTimeOffset.UtcNow - this.lastRecoveryAttempt.Value;
                if (elapsed.TotalSeconds < RecoveryCooldownSeconds)
                {
                    failureReason = $"Recovery cooldown active ({elapsed.TotalSeconds:F1}s elapsed)";
                    return false;
                }
            }

            this.isInRecoveryMode = true;
        }

        failureReason = null;
        return true;
    }

    private void CompleteSuccessfulRecovery()
    {
        lock (this.lockObject)
        {
            this.lastRecoveryAttempt = DateTimeOffset.UtcNow;
            this.failedRecoveryAttempts = 0;
            this.lastRecoveryError = null;
        }
    }

    private void CompleteFailedRecovery(string errorMessage)
    {
        lock (this.lockObject)
        {
            this.failedRecoveryAttempts++;
            this.lastRecoveryError = errorMessage;
        }
    }

    private void SetLastRecoveryError(string? errorMessage)
    {
        lock (this.lockObject)
        {
            this.lastRecoveryError = errorMessage;
        }
    }

    private void ExitRecoveryMode()
    {
        lock (this.lockObject)
        {
            this.isInRecoveryMode = false;
        }
    }
}
