// <copyright file="PairingResult.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T24 — Status of a pairing operation.
/// </summary>
public enum PairingStatus
{
    /// <summary>
    /// Device is not paired.
    /// </summary>
    NotPaired = 0,

    /// <summary>
    /// Pairing completed successfully.
    /// </summary>
    Success = 1,

    /// <summary>
    /// The pairing code is invalid.
    /// </summary>
    InvalidCode = 2,

    /// <summary>
    /// The pairing code has expired.
    /// </summary>
    ExpiredCode = 3,

    /// <summary>
    /// An error occurred during pairing.
    /// </summary>
    Error = 4,

    /// <summary>
    /// Pairing was rate limited and may be retried later.
    /// </summary>
    TooManyRequests = 5,
}

/// <summary>
/// T24 — Result of a pairing operation.
/// </summary>
public sealed class PairingResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Status del resultado.
    /// </summary>
    public required PairingStatus Status { get; init; }

    /// <summary>
    /// Device ID si fue exitoso.
    /// </summary>
    public string? DeviceId { get; init; }

    /// <summary>
    /// Parent ID si fue exitoso.
    /// </summary>
    public string? ParentId { get; init; }

    /// <summary>
    /// Versión de la política.
    /// </summary>
    public int PolicyVersion { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Server-provided delay for a rate-limited operation.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// Crea un resultado indicando que no está emparejado.
    /// </summary>
    public static PairingResult NotPaired()
        => new() { Success = false, Status = PairingStatus.NotPaired, DeviceId = null, ParentId = null, PolicyVersion = 0, ErrorMessage = null };

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    public static PairingResult SuccessResult(string deviceId, string parentId, int policyVersion)
        => new() { Success = true, Status = PairingStatus.Success, DeviceId = deviceId, ParentId = parentId, PolicyVersion = policyVersion, ErrorMessage = null };

    /// <summary>
    /// Crea un resultado de código inválido.
    /// </summary>
    public static PairingResult InvalidCode()
        => new()
        {
            Success = false,
            Status = PairingStatus.InvalidCode,
            DeviceId = null,
            ParentId = null,
            PolicyVersion = 0,
            ErrorMessage = "El código no es válido. Verificá el código e intentá de nuevo."
        };

    /// <summary>
    /// Crea un resultado de código expirado.
    /// </summary>
    public static PairingResult ExpiredCode()
        => new()
        {
            Success = false,
            Status = PairingStatus.ExpiredCode,
            DeviceId = null,
            ParentId = null,
            PolicyVersion = 0,
            ErrorMessage = "El código expiró. Pedí uno nuevo al administrador."
        };

    /// <summary>
    /// Crea un resultado indicando que se alcanzó el límite de intentos.
    /// </summary>
    public static PairingResult TooManyRequests(TimeSpan? retryAfter = null)
        => new()
        {
            Success = false,
            Status = PairingStatus.TooManyRequests,
            DeviceId = null,
            ParentId = null,
            PolicyVersion = 0,
            ErrorMessage = "Demasiados intentos. Esperá unos minutos e intentá de nuevo.",
            RetryAfter = retryAfter,
        };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static PairingResult Error(string message)
        => new() { Success = false, Status = PairingStatus.Error, DeviceId = null, ParentId = null, PolicyVersion = 0, ErrorMessage = message };
}
