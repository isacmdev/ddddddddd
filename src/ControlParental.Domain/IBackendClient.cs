// <copyright file="IBackendClient.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T14 — Resultado de obtener política del backend.
/// </summary>
public class PolicyFetchResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Versión de la política.
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// JSON de la política.
    /// </summary>
    public string? PolicyJson { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    public static PolicyFetchResult Succeeded(int version, string policyJson)
        => new() { Success = true, Version = version, PolicyJson = policyJson };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static PolicyFetchResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// T14 — Resultado de subir datos al backend.
/// </summary>
public class DataPushResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Número de elementos enviados.
    /// </summary>
    public int ItemsSent { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Bandera indicando si hay más datos pendientes.
    /// </summary>
    public bool HasMorePending { get; init; }

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    public static DataPushResult Succeeded(int itemsSent, bool hasMorePending = false)
        => new() { Success = true, ItemsSent = itemsSent, HasMorePending = hasMorePending };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static DataPushResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// T14 — Resultado de heartbeat.
/// </summary>
public class HeartbeatResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Offset del reloj en milisegundos.
    /// </summary>
    public long? ServerTimeOffsetMs { get; init; }

    /// <summary>
    /// Indica si hay una política nueva disponible.
    /// </summary>
    public bool NewPolicyAvailable { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    public static HeartbeatResult Succeeded(long? serverTimeOffsetMs = null, bool newPolicyAvailable = false)
        => new() { Success = true, ServerTimeOffsetMs = serverTimeOffsetMs, NewPolicyAvailable = newPolicyAvailable };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static HeartbeatResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// T14 — Resultado de registro de token push.
/// </summary>
public class PushTokenRegistrationResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Fecha de expiración del token.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    public static PushTokenRegistrationResult Succeeded(DateTimeOffset? expiresAt = null)
        => new() { Success = true, ExpiresAt = expiresAt };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static PushTokenRegistrationResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// T24 — Request payload for device pairing.
/// </summary>
/// <param name="Code">6-character pairing code (alphanumeric, uppercase).</param>
/// <param name="DeviceName">Device name from Environment.MachineName.</param>
/// <param name="DeviceModel">Device model from WMI Win32_ComputerSystem.</param>
/// <param name="OsVersion">OS version from Environment.OSVersion.</param>
/// <param name="AppVersion">Application version from assembly.</param>
/// <param name="AgeBand">Age band string ("7-12", "13-16", or "17-18").</param>
public sealed record PairingRequest(
    string Code,
    string DeviceName,
    string DeviceModel,
    string OsVersion,
    string AppVersion,
    string AgeBand);

/// <summary>
/// T14 — Interfaz para el cliente del backend de Supabase.
/// Consume el contrato documentado en T14.
/// </summary>
public interface IBackendClient
{
    /// <summary>
    /// Obtiene la política actual del backend.
    /// </summary>
    /// <param name="deviceId">ID del dispositivo.</param>
    /// <param name="currentVersion">Versión actual local (para evitar descarga innecesaria).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado con la política si hay nueva versión.</returns>
    Task<PolicyFetchResult> FetchPolicyAsync(
        string deviceId,
        int currentVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sube registros de uso al backend.
    /// </summary>
    /// <param name="usageLogs">Registros de uso a subir.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado de la operación.</returns>
    Task<DataPushResult> PushUsageLogsAsync(
        IEnumerable<UsageLogEntry> usageLogs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sube alertas de dispositivo al backend.
    /// </summary>
    /// <param name="alerts">Alertas a subir.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado de la operación.</returns>
    Task<DataPushResult> PushDeviceAlertsAsync(
        IEnumerable<DeviceAlertEntry> alerts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sube eventos de comportamiento al backend.
    /// </summary>
    /// <param name="events">Eventos a subir.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado de la operación.</returns>
    Task<DataPushResult> PushBehavioralEventsAsync(
        IEnumerable<BehavioralEventEntry> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envía heartbeat al backend.
    /// </summary>
    /// <param name="heartbeat">Datos del heartbeat.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado del heartbeat.</returns>
    Task<HeartbeatResult> SendHeartbeatAsync(
        HeartbeatData heartbeat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra el token de push (WNS) en el backend.
    /// </summary>
    /// <param name="pushToken">Token de push.</param>
    /// <param name="channel">Canal ('wns').</param>
    /// <param name="expiresAt">Fecha de expiración del token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado del registro.</returns>
    Task<PushTokenRegistrationResult> RegisterPushTokenAsync(
        string pushToken,
        string channel,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una solicitud de tiempo extra.
    /// </summary>
    /// <param name="request">Solicitud de tiempo.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True si se creó exitosamente.</returns>
    Task<bool> CreateTimeRequestAsync(
        TimeRequestEntry request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reporta integridad del dispositivo.
    /// </summary>
    /// <param name="report">Reporte de integridad.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado con éxito y veredicto del servidor.</returns>
    Task<IntegrityReportResult> ReportIntegrityAsync(
        IntegrityReport report,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Empareja el dispositivo con un padre.
    /// </summary>
    /// <param name="request">Request de emparejamiento.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado del emparejamiento.</returns>
    Task<PairingHttpResult> PairAsync(PairingRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// T23 — Resultado del reporte de integridad con veredicto del servidor.
/// </summary>
/// <param name="Success">Indica si el reporte se envió exitosamente.</param>
/// <param name="Verdict">Veredicto del servidor: "trust", "revoked", o "unknown".</param>
public sealed record IntegrityReportResult(bool Success, string? Verdict);

/// <summary>
/// Entrada de log de uso.
/// </summary>
public class UsageLogEntry
{
    /// <summary>
    /// App ID.
    /// </summary>
    public required string AppId { get; init; }

    /// <summary>
    /// Minutos de uso.
    /// </summary>
    public int Minutes { get; init; }

    /// <summary>
    /// Fecha del servidor.
    /// </summary>
    public required DateTimeOffset ServerDate { get; init; }

    /// <summary>
    /// Clave de desduplicación.
    /// </summary>
    public required string DedupKey { get; init; }
}

/// <summary>
/// Entrada de alerta de dispositivo.
/// </summary>
public class DeviceAlertEntry
{
    /// <summary>
    /// Tipo de alerta.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Descripción.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Severidad.
    /// </summary>
    public string? Severity { get; init; }

    /// <summary>
    /// Momento de detección.
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// Clave de desduplicación.
    /// </summary>
    public required string DedupKey { get; init; }
}

/// <summary>
/// Entrada de evento de comportamiento.
/// </summary>
public class BehavioralEventEntry
{
    /// <summary>
    /// Tipo de evento.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// App ID relacionada.
    /// </summary>
    public string? AppId { get; init; }

    /// <summary>
    /// Timestamp.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Metadata adicional.
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Clave de desduplicación.
    /// </summary>
    public required string DedupKey { get; init; }
}

/// <summary>
/// Datos de heartbeat.
/// </summary>
public class HeartbeatData
{
    /// <summary>
    /// Nivel de enforcement actual.
    /// </summary>
    public EnforcementLevel Enforcement { get; init; }

    /// <summary>
    /// Porcentaje de batería (null si escritorio).
    /// </summary>
    public int? BatteryPct { get; init; }

    /// <summary>
    /// Offset del reloj en ms.
    /// </summary>
    public long ClockOffsetMs { get; init; }

    /// <summary>
    /// Uptime del agente en ms.
    /// </summary>
    public long AgentUptimeMs { get; init; }
}

/// <summary>
/// Solicitud de tiempo extra.
/// </summary>
public class TimeRequestEntry
{
    /// <summary>
    /// ID de la solicitud.
    /// </summary>
    public required string RequestId { get; init; }

    /// <summary>
    /// Minutos solicitados.
    /// </summary>
    public int Minutes { get; init; }

    /// <summary>
    /// Razón de la solicitud.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Momento de creación.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Reporte de integridad.
/// </summary>
public class IntegrityReport
{
    /// <summary>
    /// Hash del reporte.
    /// </summary>
    public required string ReportHash { get; init; }

    /// <summary>
    /// Timestamp del reporte.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Versión del agente.
    /// </summary>
    public required string AgentVersion { get; init; }

    /// <summary>
    /// Plataforma.
    /// </summary>
    public required string Platform { get; init; }

    /// <summary>
    /// Resultado de verificación de firma Authenticode (WinVerifyTrust).
    /// </summary>
    public bool SignatureValid { get; init; }

    /// <summary>
    /// Hash SHA256 del binario actual en hexadecimal (64 caracteres).
    /// </summary>
    public string BinaryHash { get; init; } = string.Empty;
}
