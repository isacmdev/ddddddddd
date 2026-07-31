// <copyright file="IServiceHealthMonitor.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T10 — Monitorea la salud del servicio y del agente.
/// Provee eventos cuando se detecta que el agente murió o el servicio está en mal estado.
/// </summary>
public interface IServiceHealthMonitor
{
    /// <summary>
    /// Indica si el agente está vivo y respondiendo.
    /// </summary>
    bool IsAgentHealthy { get; }

    /// <summary>
    /// Indica si el servicio está en estado saludable.
    /// </summary>
    bool IsServiceHealthy { get; }

    /// <summary>
    /// Último timestamp de heartbeat del agente.
    /// </summary>
    DateTimeOffset? LastAgentHeartbeat { get; }

    /// <summary>
    /// Número de veces que el agente ha muerto y sido relanzado.
    /// </summary>
    int AgentRestartCount { get; }

    /// <summary>
    /// Inicia el monitoreo de salud.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene el monitoreo de salud.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Registra un heartbeat del agente.
    /// </summary>
    void RecordAgentHeartbeat();

    /// <summary>
    /// Registra que el agente murió.
    /// </summary>
    void RecordAgentDeath();

    /// <summary>
    /// Resetea el contador de reinicios del agente.
    /// </summary>
    void ResetAgentRestartCount();

    /// <summary>
    /// Evento cuando el agente muere y necesita ser relanzado.
    /// </summary>
    event EventHandler<AgentDiedEventArgs>? AgentDied;

    /// <summary>
    /// Evento cuando se detecta que el servicio está en mal estado.
    /// </summary>
    event EventHandler<ServiceUnhealthyEventArgs>? ServiceBecameUnhealthy;
}

/// <summary>
/// Args para el evento AgentDied.
/// </summary>
public sealed class AgentDiedEventArgs : EventArgs
{
    /// <summary>
    /// Número de muerte del agente.
    /// </summary>
    public required int DeathCount { get; init; }

    /// <summary>
    /// Timestamp de la muerte.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Razón de la muerte, si se conoce.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Args para el evento ServiceUnhealthy.
/// </summary>
public sealed class ServiceUnhealthyEventArgs : EventArgs
{
    /// <summary>
    /// Timestamp cuando se detectó el mal estado.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Descripción del problema.
    /// </summary>
    public required string Issue { get; init; }
}
