// <copyright file="OnboardingStateStore.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

using System.Text.Json;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 — JSON file-based implementation of <see cref="IOnboardingStateStore"/>.
/// Stores state in %LOCALAPPDATA%\ControlParental\onboarding_state.json.
/// </summary>
public class OnboardingStateStore : IOnboardingStateStore
{
    private readonly string stateFilePath;
    private readonly JsonSerializerOptions jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnboardingStateStore"/> class.
    /// </summary>
    public OnboardingStateStore()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ControlParental");
        Directory.CreateDirectory(dataDir);
        this.stateFilePath = Path.Combine(dataDir, "onboarding_state.json");
        this.jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    }

    /// <inheritdoc />
    public async Task<OnboardingState> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(this.stateFilePath))
        {
            return CreateInitialState();
        }

        try
        {
            var json = await File.ReadAllTextAsync(this.stateFilePath, ct);
            return JsonSerializer.Deserialize<OnboardingState>(json, this.jsonOptions) ?? CreateInitialState();
        }
        catch
        {
            return CreateInitialState();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(OnboardingState state, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(state, this.jsonOptions);
        await File.WriteAllTextAsync(this.stateFilePath, json, ct);
    }

    /// <inheritdoc />
    public async Task RecordFunnelEventAsync(FunnelEvent evt, CancellationToken ct = default)
    {
        var state = await this.LoadAsync(ct);
        var events = new List<FunnelEvent>(state.Events) { evt };
        await this.SaveAsync(state with { Events = events }, ct);
    }

    private static OnboardingState CreateInitialState()
    {
        var steps = new List<OnboardingStep>
        {
            new(0, "pairing", "Emparejar dispositivo", "Emparejá este dispositivo con la cuenta del tutor escaneando el código QR.", "Emparejar", OnboardingStepStatus.Pending),
            new(1, "consent", "Consentimiento", "Antes de continuar, necesitamos tu consentimiento para el monitoreo.", "Dar consentimiento", OnboardingStepStatus.Locked),
            new(2, "account", "Cuenta del menor", "Creá una cuenta estándar para el menor.", "Crear cuenta", OnboardingStepStatus.Locked),
            new(3, "service", "Instalar servicio", "Instalá el servicio de Control Parental con permisos de administrador.", "Instalar", OnboardingStepStatus.Locked),
            new(4, "demo", "Probemos tu protección", "Veamos cómo funciona la protección.", "Probar", OnboardingStepStatus.Locked, IsFirstWin: true),
            new(5, "managed", "Subir el nivel", "Activá la capa preventiva MANAGED (WDAC/AppLocker).", "Activar", OnboardingStepStatus.Locked),
        };
        return new OnboardingState(0, false, false, steps, Array.Empty<FunnelEvent>());
    }
}
