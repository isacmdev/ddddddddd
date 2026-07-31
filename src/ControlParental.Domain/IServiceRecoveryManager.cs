// <copyright file="IServiceRecoveryManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T10 — Gestiona las acciones de recuperación del servicio cuando el agente muere.
/// </summary>
public interface IServiceRecoveryManager
{
    /// <summary>
    /// Número de intentos de recuperación fallidos.
    /// </summary>
    int FailedRecoveryAttempts { get; }

    /// <summary>
    /// Último error de recuperación, si hubo alguno.
    /// </summary>
    string? LastRecoveryError { get; }

    /// <summary>
    /// Indica si el servicio está en modo de recuperación.
    /// </summary>
    bool IsInRecoveryMode { get; }

    /// <summary>
    /// Solicita recuperación del agente.
    /// </summary>
    /// <param name="reason">Razón de la recuperación.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True si la recuperación fue exitosa.</returns>
    Task<bool> RequestAgentRecoveryAsync(string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resetea el estado de recuperación.
    /// </summary>
    void ResetRecoveryState();

    /// <summary>
    /// Obtiene el estado de salud general del servicio.
    /// </summary>
    ServiceRecoveryStatus GetRecoveryStatus();
}

/// <summary>
/// Estado de recuperación del servicio.
/// </summary>
public sealed class ServiceRecoveryStatus
{
    /// <summary>
    /// Indica si el sistema está saludable.
    /// </summary>
    public required bool IsHealthy { get; init; }

    /// <summary>
    /// Número de muertes del agente.
    /// </summary>
    public required int AgentDeaths { get; init; }

    /// <summary>
    /// Número de recuperaciones fallidas.
    /// </summary>
    public required int FailedRecoveries { get; init; }

    /// <summary>
    /// Mensaje de estado.
    /// </summary>
    public string? StatusMessage { get; init; }

    /// <summary>
    /// Nivel de salud: HEALTHY, DEGRADED, CRITICAL.
    /// </summary>
    public required ServiceHealthLevel HealthLevel { get; init; }
}

/// <summary>
/// Nivel de salud del servicio.
/// </summary>
public enum ServiceHealthLevel
{
    /// <summary>
    /// Servicio saludable.
    /// </summary>
    Healthy,

    /// <summary>
    /// Servicio degradado pero funcional.
    /// </summary>
    Degraded,

    /// <summary>
    /// Servicio en estado crítico.
    /// </summary>
    Critical,
}
