// <copyright file="IDeviceAuthenticator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T17 — Estado de autenticación del dispositivo.
/// </summary>
public enum DeviceAuthState
{
    /// <summary>
    /// No hay sesión válida.
    /// </summary>
    Unauthenticated = 0,

    /// <summary>
    /// Sesión válida y vigente.
    /// </summary>
    Authenticated = 1,

    /// <summary>
    /// Sesión expirada, necesita refresh.
    /// </summary>
    NeedsRefresh = 2,

    /// <summary>
    /// Error de autenticación que requiere re-emparejamiento.
    /// </summary>
    RequiresRePairing = 3,
}

/// <summary>
/// T17 — Resultado de autenticación.
/// </summary>
public class DeviceAuthResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Estado de autenticación actual.
    /// </summary>
    public DeviceAuthState State { get; init; }

    /// <summary>
    /// Token de acceso si fue exitoso.
    /// </summary>
    public string? AccessToken { get; init; }

    /// <summary>
    /// Claim device_id del token.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Indica si se requiere re-emparejamiento.
    /// </summary>
    public bool RequiresRePairing => this.State == DeviceAuthState.RequiresRePairing;

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    public static DeviceAuthResult Succeeded(string accessToken, string deviceId)
        => new() { Success = true, State = DeviceAuthState.Authenticated, AccessToken = accessToken, DeviceId = deviceId };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static DeviceAuthResult Failed(string error, DeviceAuthState state = DeviceAuthState.RequiresRePairing)
        => new() { Success = false, State = state, ErrorMessage = error };
}

/// <summary>
/// T17 — Interfaz para autenticación de dispositivo contra Supabase.
/// </summary>
public interface IDeviceAuthenticator
{
    /// <summary>
    /// Obtiene el estado actual de autenticación sin hacer peticiones de red.
    /// </summary>
    DeviceAuthState CurrentState { get; }

    /// <summary>
    /// Obtiene el device_id actual si existe sesión válida.
    /// </summary>
    string? CurrentDeviceId { get; }

    /// <summary>
    /// Obtiene el token de acceso actual si existe sesión válida.
    /// </summary>
    string? CurrentAccessToken { get; }

    /// <summary>
    /// Inicializa el autenticador, restaurando sesión previa si existe.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado de la inicialización.</returns>
    Task<DeviceAuthResult> InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea una nueva sesión anónima para el dispositivo.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado con la nueva sesión.</returns>
    Task<DeviceAuthResult> CreateAnonymousSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresca el token de acceso actual.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado con el token refrescado.</returns>
    Task<DeviceAuthResult> RefreshTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresca el token solo si está por expirar (dentro del umbral).
    /// </summary>
    /// <param name="refreshThreshold">Umbral de expiración restante para refrescar.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado del refresh si se realizó.</returns>
    Task<DeviceAuthResult> RefreshIfNeededAsync(TimeSpan refreshThreshold, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca la sesión como corrupta y fuerza re-emparejamiento.
    /// </summary>
    /// <param name="reason">Razón de la invalidación.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task.</returns>
    Task InvalidateSessionAsync(string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica que la sesión actual sea válida contra el backend.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True si la sesión es válida.</returns>
    Task<bool> ValidateSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Fuerza rotación de sesión (nuevo device_id).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado con la nueva sesión.</returns>
    Task<DeviceAuthResult> RotateSessionAsync(CancellationToken cancellationToken = default);
}
