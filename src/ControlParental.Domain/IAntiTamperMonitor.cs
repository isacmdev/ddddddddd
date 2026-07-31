// <copyright file="IAntiTamperMonitor.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T13 — Tipos de eventos anti-tamper.
/// </summary>
public enum TamperEventType
{
    /// <summary>
    /// Intento de detener el servicio.
    /// </summary>
    ServiceStopAttempt,

    /// <summary>
    /// Agente detectado como muerto.
    /// </summary>
    AgentKillDetected,

    /// <summary>
    /// Intento de desinstalar.
    /// </summary>
    UninstallAttempt,

    /// <summary>
    /// Cambio de reloj sospechoso.
    /// </summary>
    ClockTamperSuspected,

    /// <summary>
    /// Cambio de zona horaria.
    /// </summary>
    TimezoneChanged,

    /// <summary>
    /// Menor detectado como administrador.
    /// </summary>
    ChildIsAdminDetected,
}

/// <summary>
/// Evento anti-tamper detectado.
/// </summary>
public class TamperEvent
{
    /// <summary>
    /// Tipo de evento.
    /// </summary>
    public required TamperEventType Type { get; init; }

    /// <summary>
    /// Descripción del evento.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Momento de detección.
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// Severidad del evento.
    /// </summary>
    public TamperSeverity Severity { get; init; } = TamperSeverity.Info;
}

/// <summary>
/// Severidad del evento anti-tamper.
/// </summary>
public enum TamperSeverity
{
    /// <summary>
    /// Informativo.
    /// </summary>
    Info,

    /// <summary>
    /// Advertencia.
    /// </summary>
    Warning,

    /// <summary>
    /// Severo.
    /// </summary>
    Severe,

    /// <summary>
    /// Crítico.
    /// </summary>
    Critical,
}

/// <summary>
/// Args para eventos de tamper.
/// </summary>
public class TamperEventArgs : EventArgs
{
    /// <summary>
    /// Evento detectado.
    /// </summary>
    public required TamperEvent Event { get; init; }
}

/// <summary>
/// Args para eventos de zona horaria.
/// </summary>
public class TimezoneChangedEventArgs : EventArgs
{
    /// <summary>
    /// Zona anterior.
    /// </summary>
    public string? OldTimezone { get; init; }

    /// <summary>
    /// Zona nueva.
    /// </summary>
    public required string NewTimezone { get; init; }

    /// <summary>
    /// Momento del cambio.
    /// </summary>
    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// Args para eventos de salto de reloj.
/// </summary>
public class ClockJumpEventArgs : EventArgs
{
    /// <summary>
    /// Offset del salto en segundos.
    /// </summary>
    public required double OffsetSeconds { get; init; }

    /// <summary>
    /// Dirección del salto (positivo = adelante, negativo = atrás).
    /// </summary>
    public required int Direction { get; init; }

    /// <summary>
    /// Momento del salto.
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }
}

/// <summary>
/// Interfaz para el vigilante anti-tamper.
/// Detecta y reporta intentos de manipulación del sistema de control parental.
/// </summary>
public interface IAntiTamperMonitor : IDisposable
{
    /// <summary>
    /// Obtiene los eventos tamper detectados desde el inicio.
    /// </summary>
    IReadOnlyList<TamperEvent> DetectedEvents { get; }

    /// <summary>
    /// Obtiene la zona horaria actual.
    /// </summary>
    string CurrentTimezone { get; }

    /// <summary>
    /// Indica si se detectó un salto de reloj.
    /// </summary>
    bool ClockJumpDetected { get; }

    /// <summary>
    /// Indica si se detectó un cambio de zona horaria.
    /// </summary>
    bool TimezoneChangedDetected { get; }

    /// <summary>
    /// Inicia el monitoreo.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Detiene el monitoreo.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Registra un intento de detener el servicio.
    /// </summary>
    void RecordServiceStopAttempt(string? reason = null);

    /// <summary>
    /// Registra la muerte del agente.
    /// </summary>
    void RecordAgentDeath(int? exitCode = null);

    /// <summary>
    /// Registra un intento de desinstalación detectado.
    /// </summary>
    void RecordUninstallAttempt(string? packageName = null);

    /// <summary>
    /// Fuerza verificación de integridad del reloj contra fecha de servidor.
    /// </summary>
    Task VerifyClockAgainstServerTimeAsync(DateTimeOffset serverTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evento cuando se detecta un evento tamper.
    /// </summary>
    event EventHandler<TamperEventArgs>? TamperDetected;

    /// <summary>
    /// Evento cuando cambia la zona horaria.
    /// </summary>
    event EventHandler<TimezoneChangedEventArgs>? TimezoneChanged;

    /// <summary>
    /// Evento cuando se detecta un salto de reloj.
    /// </summary>
    event EventHandler<ClockJumpEventArgs>? OnClockJumpDetected;
}
