// <copyright file="PairingHttpResult.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T24 — HTTP status of a pairing API call.
/// </summary>
public enum PairingHttpStatus
{
    /// <summary>
    /// Pairing was successful.
    /// </summary>
    Success = 0,

    /// <summary>
    /// Pairing code not found (HTTP 404).
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// Pairing code has expired (HTTP 410).
    /// </summary>
    Gone = 2,

    /// <summary>
    /// Too many requests (HTTP 429).
    /// </summary>
    TooManyRequests = 3,

    /// <summary>
    /// Server error (HTTP 5xx).
    /// </summary>
    ServerError = 4,

    /// <summary>
    /// Network error (exception).
    /// </summary>
    NetworkError = 5,
}

/// <summary>
/// T24 — Result of an HTTP pairing call.
/// </summary>
public sealed class PairingHttpResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Status HTTP del resultado.
    /// </summary>
    public required PairingHttpStatus Status { get; init; }

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
    /// Crea un resultado exitoso de HTTP.
    /// </summary>
    public static PairingHttpResult SuccessResult(string deviceId, string parentId, int policyVersion)
        => new() { Success = true, Status = PairingHttpStatus.Success, DeviceId = deviceId, ParentId = parentId, PolicyVersion = policyVersion, ErrorMessage = null };

    /// <summary>
    /// Crea un resultado not found (404).
    /// </summary>
    public static PairingHttpResult NotFound()
        => new() { Success = false, Status = PairingHttpStatus.NotFound, DeviceId = null, ParentId = null, PolicyVersion = 0, ErrorMessage = null };

    /// <summary>
    /// Crea un resultado gone (410).
    /// </summary>
    public static PairingHttpResult Gone()
        => new() { Success = false, Status = PairingHttpStatus.Gone, DeviceId = null, ParentId = null, PolicyVersion = 0, ErrorMessage = null };

    /// <summary>
    /// Crea un resultado too many requests (429).
    /// </summary>
    public static PairingHttpResult TooManyRequests()
        => new() { Success = false, Status = PairingHttpStatus.TooManyRequests, DeviceId = null, ParentId = null, PolicyVersion = 0, ErrorMessage = null };

    /// <summary>
    /// Crea un resultado de error de servidor (5xx).
    /// </summary>
    public static PairingHttpResult ServerError(string details)
        => new() { Success = false, Status = PairingHttpStatus.ServerError, DeviceId = null, ParentId = null, PolicyVersion = 0, ErrorMessage = details };

    /// <summary>
    /// Crea un resultado de error de red.
    /// </summary>
    public static PairingHttpResult NetworkError(string message)
        => new() { Success = false, Status = PairingHttpStatus.NetworkError, DeviceId = null, ParentId = null, PolicyVersion = 0, ErrorMessage = message };
}
