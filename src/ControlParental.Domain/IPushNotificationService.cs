// <copyright file="IPushNotificationService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T14/T19 — Resultado de obtener token WNS.
/// </summary>
public class WnsTokenResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Channel URI del token WNS.
    /// </summary>
    public string? ChannelUri { get; init; }

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
    public static WnsTokenResult Succeeded(string channelUri, DateTimeOffset? expiresAt = null)
        => new() { Success = true, ChannelUri = channelUri, ExpiresAt = expiresAt };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static WnsTokenResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// T14/T19 — Interfaz para el servicio de notificaciones push (WNS).
/// </summary>
public interface IPushNotificationService
{
    /// <summary>
    /// Obtiene o renueva el token WNS.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado con el token si fue exitoso.</returns>
    Task<WnsTokenResult> GetOrRenewTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el token actual sin renovarlo.
    /// </summary>
    /// <returns>Channel URI actual o null si no hay.</returns>
    string? GetCurrentToken();

    /// <summary>
    /// Obtiene la fecha de expiración del token actual.
    /// </summary>
    /// <returns>Fecha de expiración o null.</returns>
    DateTimeOffset? GetTokenExpiresAt();

    /// <summary>
    /// Indica si el token necesita renovación.
    /// </summary>
    /// <returns>True si necesita renovación.</returns>
    bool NeedsRenewal();
}
