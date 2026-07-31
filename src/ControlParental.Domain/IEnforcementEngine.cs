// <copyright file="IEnforcementEngine.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T11 — Motor de enforcement que coordina la evaluación de políticas
/// y la aplicación de bloqueos.
/// </summary>
public interface IEnforcementEngine
{
    /// <summary>
    /// Evalúa y aplica la política ante un cambio de foreground.
    /// </summary>
    /// <param name="appId">AppId de la app en primer plano.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado del enforcement.</returns>
    Task<EnforcementResult> EnforceForegroundChangeAsync(
        string appId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fuerza el bloqueo total del dispositivo (T09).
    /// </summary>
    /// <param name="reason">Razón del bloqueo.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LockDeviceAsync(string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Desbloquea el dispositivo (quitar overlay pero mantener estado).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnlockDeviceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el estado actual del enforcement.
    /// </summary>
    EnforcementStatus GetStatus();
}

/// <summary>
/// Resultado de una acción de enforcement.
/// </summary>
public sealed class EnforcementResult
{
    /// <summary>
    /// Indica si la acción fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Indica si la app fue bloqueada.
    /// </summary>
    public required bool Blocked { get; init; }

    /// <summary>
    /// Código de razón del bloqueo/permiso.
    /// </summary>
    public int? ReasonCode { get; init; }

    /// <summary>
    /// Texto de razón para mostrar al usuario.
    /// </summary>
    public string? ReasonText { get; init; }

    /// <summary>
    /// Mensaje de error si hubo fallo.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Timestamp de la evaluación.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }
}

/// <summary>
/// Estado actual del enforcement engine.
/// </summary>
public sealed class EnforcementStatus
{
    /// <summary>
    /// Indica si hay un overlay activo.
    /// </summary>
    public required bool IsOverlayActive { get; init; }

    /// <summary>
    /// Razón del overlay activo, si existe.
    /// </summary>
    public string? ActiveOverlayReason { get; init; }

    /// <summary>
    /// Indica si el dispositivo está bloqueado totalmente.
    /// </summary>
    public required bool IsDeviceLocked { get; init; }

    /// <summary>
    /// Última evaluación realizada.
    /// </summary>
    public Decision? LastDecision { get; init; }

    /// <summary>
    /// AppId de la última app evaluada.
    /// </summary>
    public string? LastAppId { get; init; }
}
