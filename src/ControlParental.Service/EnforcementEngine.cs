// <copyright file="EnforcementEngine.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T11 — Implementación de IEnforcementEngine.
/// Coordina la evaluación de políticas y la aplicación de bloqueos.
/// El Service envía comandos al Agente via IPC para mostrar overlays.
/// </summary>
public sealed class EnforcementEngine : IEnforcementEngine, IDisposable
{
    private readonly IPolicyRepository policyRepository;
    private readonly IUsageAccumulator usageAccumulator;
    private readonly IProcessTerminator processTerminator;
    private readonly IWorkstationLockManager workstationLockManager;
    private readonly ITimeProvider timeProvider;
    private readonly IIpcChannel? ipcChannel;

    private bool isOverlayActive;
    private string? activeOverlayReason;
    private bool isDeviceLocked;
    private Decision? lastDecision;
    private string? lastAppId;
    private bool isEvaluationDegraded;
    private string? lastEvaluationFailure;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnforcementEngine"/> class.
    /// </summary>
    public EnforcementEngine(
        IPolicyRepository policyRepository,
        IUsageAccumulator usageAccumulator,
        IProcessTerminator processTerminator,
        IWorkstationLockManager workstationLockManager,
        ITimeProvider timeProvider,
        IIpcChannel? ipcChannel = null)
    {
        this.policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        this.usageAccumulator = usageAccumulator ?? throw new ArgumentNullException(nameof(usageAccumulator));
        this.processTerminator = processTerminator ?? throw new ArgumentNullException(nameof(processTerminator));
        this.workstationLockManager = workstationLockManager ?? throw new ArgumentNullException(nameof(workstationLockManager));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.ipcChannel = ipcChannel;
    }

    /// <inheritdoc />
    public async Task<EnforcementResult> EnforceForegroundChangeAsync(
        string appId,
        CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return new EnforcementResult
            {
                Success = false,
                Blocked = false,
                ErrorMessage = "Engine disposed",
                Timestamp = DateTimeOffset.UtcNow,
            };
        }

        this.lastAppId = appId;
        var timestamp = DateTimeOffset.UtcNow;

        try
        {
            // Get current policy
            var policy = await this.policyRepository.GetPolicyAsync(cancellationToken);
            if (policy == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[EnforcementEngine] No policy available. Allowing {appId}.");
                this.isEvaluationDegraded = false;
                this.lastEvaluationFailure = null;
                return new EnforcementResult
                {
                    Success = true,
                    Blocked = false,
                    ReasonText = "Sin política activa",
                    IsDegraded = false,
                    Timestamp = timestamp,
                };
            }

            // Check if device is in locked state - if so, always block
            if (policy.DeviceState == DeviceState.Locked || this.isDeviceLocked)
            {
                var decision = Decision.Block(2, "dispositivo bloqueado");
                this.lastDecision = decision;
                this.isEvaluationDegraded = false;
                this.lastEvaluationFailure = null;

                return new EnforcementResult
                {
                    Success = true,
                    Blocked = true,
                    ReasonCode = 2,
                    ReasonText = "dispositivo bloqueado",
                    IsDegraded = false,
                    Timestamp = timestamp,
                };
            }

            // Get usage snapshot
            var usage = await this.usageAccumulator.GetSnapshotAsync(cancellationToken);

            // Evaluate policy
            var zonaHoraria = this.timeProvider.CurrentZone;
            var policyDecision = RulesEngine.Evaluar(
                policy,
                appId,
                usage,
                this.timeProvider.WallClockNow,
                zonaHoraria);

            this.lastDecision = policyDecision;
            this.isEvaluationDegraded = false;
            this.lastEvaluationFailure = null;

            if (policyDecision.IsBlocked)
            {
                // BLOCK: Show overlay and terminate process
                System.Diagnostics.Debug.WriteLine(
                    $"[EnforcementEngine] Blocking {appId}. Reason: {policyDecision.ReasonText}");

                return new EnforcementResult
                {
                    Success = true,
                    Blocked = true,
                    ReasonCode = policyDecision.ReasonCode,
                    ReasonText = policyDecision.ReasonText,
                    IsDegraded = false,
                    Timestamp = timestamp,
                };
            }
            else
            {
                // ALLOW: Hide overlay if it was showing
                System.Diagnostics.Debug.WriteLine(
                    $"[EnforcementEngine] Allowing {appId}.");

                return new EnforcementResult
                {
                    Success = true,
                    Blocked = false,
                    ReasonText = "Permitido",
                    IsDegraded = false,
                    Timestamp = timestamp,
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[EnforcementEngine] Error enforcing foreground change: {ex.Message}");

            this.isEvaluationDegraded = true;
            this.lastEvaluationFailure = ex.Message;

            var safeDecision = this.lastDecision;

            return new EnforcementResult
            {
                Success = false,
                Blocked = safeDecision?.IsBlocked ?? false,
                ReasonCode = safeDecision?.ReasonCode,
                ReasonText = safeDecision?.ReasonText ?? "Evaluación degradada",
                ErrorMessage = ex.Message,
                IsDegraded = true,
                Timestamp = timestamp,
            };
        }
    }

    /// <inheritdoc />
    public async Task LockDeviceAsync(
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[EnforcementEngine] Locking device. Reason: {reason}");

        this.ShowOverlayInternal(reason, "Consultar padre");

        // Only a current typed confirmation can establish locked state.
        this.isDeviceLocked = await this.workstationLockManager.LockNowAsync(cancellationToken);

        // Enforce persistent overlay
        // (This will be restored on session unlock via OverlayPersistenceManager)
    }

    /// <inheritdoc />
    public Task UnlockDeviceAsync(CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            return Task.CompletedTask;
        }

        System.Diagnostics.Debug.WriteLine(
            "[EnforcementEngine] Unlocking device.");

        this.isDeviceLocked = false;
        this.HideOverlayInternal();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public EnforcementStatus GetStatus()
    {
        return new EnforcementStatus
        {
            IsOverlayActive = this.isOverlayActive,
            ActiveOverlayReason = this.activeOverlayReason,
            IsDeviceLocked = this.isDeviceLocked,
            LastDecision = this.lastDecision,
            LastAppId = this.lastAppId,
            IsEvaluationDegraded = this.isEvaluationDegraded,
            LastEvaluationFailure = this.lastEvaluationFailure,
        };
    }

    private void ShowOverlayInternal(string reason, string? ctaLabel)
    {
        this.isOverlayActive = true;
        this.activeOverlayReason = reason;

        // Send ShowOverlay to the agent via IPC
        if (this.ipcChannel != null && this.ipcChannel.IsConnected)
        {
            var message = new ShowOverlay(reason, ctaLabel);
            _ = this.ipcChannel.SendAsync(message, CancellationToken.None);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                "[EnforcementEngine] Cannot show overlay: IPC not connected.");
        }
    }

    private void HideOverlayInternal()
    {
        this.isOverlayActive = false;
        this.activeOverlayReason = null;

        // Send HideOverlay to the agent via IPC
        if (this.ipcChannel != null && this.ipcChannel.IsConnected)
        {
            var message = new HideOverlay();
            _ = this.ipcChannel.SendAsync(message, CancellationToken.None);
        }
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
            this.isOverlayActive = false;
            this.isDeviceLocked = false;
        }

        GC.SuppressFinalize(this);
    }
}
