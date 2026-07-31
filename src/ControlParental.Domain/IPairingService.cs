// <copyright file="IPairingService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T24 — Interfaz para el servicio de emparejamiento de dispositivos.
/// </summary>
public interface IPairingService
{
    /// <summary>
    /// Empareja el dispositivo con un padre usando un código de emparejamiento.
    /// </summary>
    /// <param name="code">Código de 6 caracteres.</param>
    /// <param name="ageBand">Rango de edad del menor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado del emparejamiento.</returns>
    Task<PairingResult> PairAsync(string code, AgeBand ageBand, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el dispositivo ya está emparejado.
    /// </summary>
    bool IsPaired { get; }

    /// <summary>
    /// Obtiene el device_id actual si está emparejado.
    /// </summary>
    string? GetCurrentDeviceId();
}
