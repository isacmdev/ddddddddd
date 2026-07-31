// <copyright file="IEnforcementLevelMonitor.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T12 — Monitorea el nivel de enforcement y detecta estados de degradación.
/// </summary>
public interface IEnforcementLevelMonitor
{
    /// <summary>
    /// Obtiene el nivel de enforcement actual.
    /// </summary>
    EnforcementLevel CurrentLevel { get; }

    /// <summary>
    /// Indica si el nivel es crítico (DEGRADED severo).
    /// </summary>
    bool IsCritical { get; }

    /// <summary>
    /// Lista de issues actuales que causan degradación.
    /// </summary>
    IReadOnlyList<EnforcementIssue> CurrentIssues { get; }

    /// <summary>
    /// Timestamp de la última evaluación.
    /// </summary>
    DateTimeOffset? LastEvaluationTime { get; }

    /// <summary>
    /// Inicia el monitoreo de nivel de enforcement.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene el monitoreo.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Fuerza una re-evaluación del nivel de enforcement.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EvaluateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra que el agente está vivo.
    /// </summary>
    void RecordAgentAlive();

    /// <summary>
    /// Registra que se recibió un heartbeat del agente.
    /// </summary>
    void RecordAgentHeartbeat();

    /// <summary>
    /// Registra que se recibió un foreground change.
    /// </summary>
    void RecordForegroundChange();

    /// <summary>
    /// Agrega un issue de enforcement manualmente (ej: fallo de integridad binaria).
    /// </summary>
    /// <param name="type">Tipo de issue.</param>
    /// <param name="severity">Severidad del issue.</param>
    /// <param name="description">Descripción del issue.</param>
    void AddIssue(EnforcementIssueType type, EnforcementIssueSeverity severity, string description);

    /// <summary>
    /// Evento cuando el nivel de enforcement cambia.
    /// </summary>
    event EventHandler<EnforcementLevelChangedEventArgs>? LevelChanged;

    /// <summary>
    /// Evento cuando se detecta un issue crítico.
    /// </summary>
    event EventHandler<EnforcementIssueDetectedEventArgs>? IssueDetected;
}

/// <summary>
/// Issue detectado que afecta el nivel de enforcement.
/// </summary>
public sealed class EnforcementIssue
{
    /// <summary>
    /// Tipo de issue.
    /// </summary>
    public required EnforcementIssueType Type { get; init; }

    /// <summary>
    /// Severidad del issue.
    /// </summary>
    public required EnforcementIssueSeverity Severity { get; init; }

    /// <summary>
    /// Descripción del issue.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Timestamp cuando se detectó.
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// Número de veces que este issue ha ocurrido.
    /// </summary>
    public int OccurrenceCount { get; init; }
}

/// <summary>
/// Tipos de issues de enforcement.
/// </summary>
public enum EnforcementIssueType
{
    /// <summary>
    /// El servicio no está corriendo.
    /// </summary>
    ServiceNotRunning,

    /// <summary>
    /// El agente no está respondiendo.
    /// </summary>
    AgentNotResponding,

    /// <summary>
    /// El menor tiene privilegios de administrador.
    /// </summary>
    ChildIsAdministrator,

    /// <summary>
    /// No se recibió foreground change por timeout.
    /// </summary>
    HookTimeout,

    /// <summary>
    /// Sin red por tiempo prolongado.
    /// </summary>
    NetworkUnavailable,

    /// <summary>
    /// Reloj o zona horaria manipulados.
    /// </summary>
    ClockTampering,

    /// <summary>
    /// Capa preventiva no disponible.
    /// </summary>
    PreventiveLayerUnavailable,

    /// <summary>
    /// Fallo de integridad binaria — firma inválida o hash no coincide.
    /// </summary>
    BinaryIntegrityFailure,
}

/// <summary>
/// Severidad de issues.
/// </summary>
public enum EnforcementIssueSeverity
{
    /// <summary>
    /// Información nomal.
    /// </summary>
    Info,

    /// <summary>
    /// Advertencia.
    /// </summary>
    Warning,

    /// <summary>
    /// Error severo que degrada el sistema.
    /// </summary>
    Severe,

    /// <summary>
    /// Crítico: el sistema no puede proteger.
    /// </summary>
    Critical,
}

/// <summary>
/// Args para el evento LevelChanged.
/// </summary>
public sealed class EnforcementLevelChangedEventArgs : EventArgs
{
    /// <summary>
    /// Nivel anterior.
    /// </summary>
    public required EnforcementLevel PreviousLevel { get; init; }

    /// <summary>
    /// Nivel nuevo.
    /// </summary>
    public required EnforcementLevel NewLevel { get; init; }

    /// <summary>
    /// Issues que causaron el cambio.
    /// </summary>
    public required IReadOnlyList<EnforcementIssue> Issues { get; init; }
}

/// <summary>
/// Args para el evento IssueDetected.
/// </summary>
public sealed class EnforcementIssueDetectedEventArgs : EventArgs
{
    /// <summary>
    /// Issue detectado.
    /// </summary>
    public required EnforcementIssue Issue { get; init; }
}
