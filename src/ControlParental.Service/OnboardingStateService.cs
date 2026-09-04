// <copyright file="OnboardingStateService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Text.Json;
using ControlParental.Domain;
using Microsoft.Extensions.Logging;

/// <summary>
/// T26 — Service-side implementation of <see cref="IOnboardingStateService"/>.
/// Persists onboarding state to JSON in ProgramData so state survives App.UI restarts
/// and so the App.UI never writes the canonical state to disk (ADR-002).
/// Writes use an atomic `.tmp` + `File.Move(overwrite: true)` so a crash mid-write
/// cannot corrupt the canonical file (ADR-009, design §12).
/// </summary>
public sealed class OnboardingStateService : IOnboardingStateService
{
    private readonly string stateFilePath;
    private readonly IChildAccountStore childAccountStore;
    private readonly ILogger<OnboardingStateService> logger;
    private readonly Func<bool> canProceedWithHealthyOnboarding;
    private readonly SemaphoreSlim fileLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingStateService"/> class.
    /// </summary>
    /// <param name="dataFolderPath">Path to the ProgramData folder.</param>
    /// <param name="childAccountStore">The child account store for persistence.</param>
    /// <param name="logger">Logger for persistence failures.</param>
    public OnboardingStateService(
        string dataFolderPath,
        IChildAccountStore childAccountStore,
        ILogger<OnboardingStateService> logger,
        Func<bool>? canProceedWithHealthyOnboarding = null)
    {
        this.childAccountStore = childAccountStore;
        this.logger = logger;
        this.canProceedWithHealthyOnboarding = canProceedWithHealthyOnboarding ?? (() => true);
        Directory.CreateDirectory(dataFolderPath);
        this.stateFilePath = Path.Combine(dataFolderPath, "onboarding_state.json");
    }

    /// <inheritdoc />
    public async Task<OnboardingState> GetStateAsync(CancellationToken ct = default)
    {
        await this.fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(this.stateFilePath))
            {
                return this.CreateInitialState();
            }

            var json = await File.ReadAllTextAsync(this.stateFilePath, ct).ConfigureAwait(false);
            var persisted = JsonSerializer.Deserialize(
                json,
                OnboardingStateJsonContext.Default.OnboardingState) ?? this.CreateInitialState();
            return this.NormalizeState(persisted);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            this.logger.LogError(
                ex,
                "Failed to load onboarding state from {StateFilePath}; returning initial state.",
                this.stateFilePath);
            return this.CreateInitialState();
        }
        finally
        {
            this.fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<OnboardingState> RecordStepCompletedAsync(string stepId, CancellationToken ct = default)
    {
        // T26 PR (P2 onboarding ownership) — Service is the canonical owner.
        // 1. Read the snapshot atomically (uses the same fileLock as SaveState).
        // 2. If the step id is unknown OR already Completed, return the snapshot
        //    untouched — duplicate / no-op completions must NOT re-persist (idempotent).
        // 3. Otherwise flip just that step to Completed and persist ONCE, returning
        //    the post-completion snapshot so callers can refresh their observable
        //    surface without re-reading state.
        var state = await this.GetStateAsync(ct).ConfigureAwait(false);
        var targetIndex = -1;
        for (int i = 0; i < state.Steps.Count; i++)
        {
            if (string.Equals(state.Steps[i].Id, stepId, StringComparison.Ordinal))
            {
                targetIndex = i;
                break;
            }
        }

        // Unknown step id — return current snapshot, no mutation, no write.
        if (targetIndex < 0)
        {
            this.logger.LogWarning(
                "RecordStepCompletedAsync called with unknown stepId '{StepId}'; returning current state without changes.",
                stepId);
            return state;
        }

        // Already Completed — idempotent no-op, no write. Returning the same
        // snapshot keeps the contract symmetric with the happy path.
        if (state.Steps[targetIndex].Status == OnboardingStepStatus.Completed)
        {
            return state;
        }

        var steps = state.Steps.ToList();
        steps[targetIndex] = steps[targetIndex] with { Status = OnboardingStepStatus.Completed };
        var newState = state with { Steps = steps };
        await this.SaveStateAsync(newState, ct).ConfigureAwait(false);
        return newState;
    }

    /// <inheritdoc />
    public async Task<OnboardingState> RecordFunnelEventAsync(string eventName, string stepId, CancellationToken ct = default)
    {
        var state = await this.GetStateAsync(ct).ConfigureAwait(false);

        if (!Enum.TryParse<FunnelEventType>(eventName, out var eventType)
            || string.IsNullOrWhiteSpace(stepId)
            || state.Events.Any(e => e.Type == eventType && string.Equals(e.StepId, stepId, StringComparison.Ordinal)))
        {
            return state;
        }

        var events = state.Events.ToList();
        events.Add(new FunnelEvent(eventType, stepId, DateTimeOffset.UtcNow));
        var newState = state with { Events = events };
        await this.SaveStateAsync(newState, ct).ConfigureAwait(false);
        return newState;
    }

    /// <inheritdoc />
    public async Task<OnboardingState> AdvanceAsync(int newIndex, CancellationToken ct = default)
    {
        var state = await this.GetStateAsync(ct).ConfigureAwait(false);

        if (newIndex >= 3 && !this.canProceedWithHealthyOnboarding())
        {
            this.logger.LogWarning("Healthy onboarding is blocked by the runtime security verdict.");
            return state;
        }

        if (newIndex >= state.Steps.Count)
        {
            var completed = state with { IsCompleted = true };
            await this.SaveStateAsync(completed, ct).ConfigureAwait(false);
            return completed;
        }

        var steps = state.Steps.ToList();
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i].Index < newIndex)
            {
                steps[i] = steps[i] with { Status = OnboardingStepStatus.Completed };
            }
            else if (steps[i].Index == newIndex)
            {
                steps[i] = steps[i] with { Status = OnboardingStepStatus.InProgress };
            }
        }

        var advanced = state with { CurrentStepIndex = newIndex, Steps = steps };
        await this.SaveStateAsync(advanced, ct).ConfigureAwait(false);
        return advanced;
    }

    /// <inheritdoc />
    public async Task<OnboardingState> ResetAsync(CancellationToken ct = default)
    {
        await this.fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(this.stateFilePath))
            {
                File.Delete(this.stateFilePath);
            }

            var tmpPath = this.stateFilePath + ".tmp";
            if (File.Exists(tmpPath))
            {
                File.Delete(tmpPath);
            }

            return this.CreateInitialState();
        }
        finally
        {
            this.fileLock.Release();
        }
    }

    /// <summary>
    /// Atomically writes the state to disk: serialize to <c>{stateFilePath}.tmp</c>,
    /// then <see cref="File.Move(string, string, bool)"/> over the canonical file.
    /// A crash between the two operations leaves the prior canonical file untouched
    /// (ADR-009, design §12 atomic write race).
    /// </summary>
    /// <param name="state">The state to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task SaveStateAsync(OnboardingState state, CancellationToken ct)
    {
        await this.fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(
                state,
                OnboardingStateJsonContext.Default.OnboardingState);
            var tmpPath = this.stateFilePath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, json, ct).ConfigureAwait(false);
            File.Move(tmpPath, this.stateFilePath, overwrite: true);
        }
        finally
        {
            this.fileLock.Release();
        }
    }

    private OnboardingState NormalizeState(OnboardingState state)
    {
        if (state.Steps.Any(step => string.Equals(step.Id, "service", StringComparison.Ordinal)))
        {
            return state;
        }

        var canonical = this.CreateInitialState().Steps;
        var statuses = state.Steps.ToDictionary(step => step.Id, step => step.Status, StringComparer.Ordinal);
        var steps = canonical
            .Select(step => statuses.TryGetValue(step.Id, out var status) ? step with { Status = status } : step)
            .ToList();
        var currentId = state.Steps.FirstOrDefault(step => step.Index == state.CurrentStepIndex)?.Id;
        var currentIndex = currentId is null
            ? Math.Min(state.CurrentStepIndex, steps.Count - 1)
            : steps.First(step => string.Equals(step.Id, currentId, StringComparison.Ordinal)).Index;

        if (currentId is "demo" or "managed")
        {
            steps[3] = steps[3] with { Status = OnboardingStepStatus.InProgress };
            currentIndex = 3;
        }

        return state with { CurrentStepIndex = currentIndex, Steps = steps, IsCompleted = false };
    }

    private OnboardingState CreateInitialState()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Emparejar dispositivo", "Pedile a tu tutor el código de emparejamiento.", "Emparejar", OnboardingStepStatus.Pending),
            new(1, "consent", "Consentimiento", "Antes de continuar, necesitamos tu consentimiento para el monitoreo.", "Dar consentimiento", OnboardingStepStatus.Locked),
            new(2, "account", "Cuenta del menor", "Creá una cuenta estándar para el menor.", "Crear cuenta", OnboardingStepStatus.Locked),
            new(3, "service", "Configurar protección", "Verificá que el servicio y el agente estén activos.", "Verificar", OnboardingStepStatus.Locked),
            new(4, "demo", "Probemos tu protección", "Veamos cómo funciona la protección.", "Probar", OnboardingStepStatus.Locked, IsFirstWin: true),
            new(5, "managed", "Subir el nivel", "Activá la capa preventiva MANAGED (WDAC/AppLocker).", "Activar", OnboardingStepStatus.Locked),
        };
        return new OnboardingState(0, false, false, steps, Array.Empty<FunnelEvent>());
    }
}
