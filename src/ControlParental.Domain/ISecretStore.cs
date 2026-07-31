// <copyright file="ISecretStore.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T16 — Resultado de leer un secreto.
/// </summary>
public class SecretReadResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Valor del secreto si fue exitoso.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Indica si el secreto no existe.
    /// </summary>
    public bool NotFound => this.Success && this.Value == null;

    /// <summary>
    /// Crea un resultado exitoso con valor.
    /// </summary>
    public static SecretReadResult Succeeded(string value)
        => new() { Success = true, Value = value };

    /// <summary>
    /// Crea un resultado exitoso sin valor (no existe).
    /// </summary>
    public static SecretReadResult NotFoundResult()
        => new() { Success = true, Value = null };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static SecretReadResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// T16 — Resultado de escribir un secreto.
/// </summary>
public class SecretWriteResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Indica si el secreto fue creado (vs actualizado).
    /// </summary>
    public bool Created { get; init; }

    /// <summary>
    /// Crea un resultado exitoso de creación.
    /// </summary>
    public static SecretWriteResult NewSecret()
        => new() { Success = true, Created = true };

    /// <summary>
    /// Crea un resultado exitoso de actualización.
    /// </summary>
    public static SecretWriteResult ExistingSecret()
        => new() { Success = true, Created = false };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static SecretWriteResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// T16 — Resultado de eliminar un secreto.
/// </summary>
public class SecretDeleteResult
{
    /// <summary>
    /// Indica si la operación fue exitosa.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Indica si el secreto existía y fue eliminado.
    /// </summary>
    public bool Deleted { get; init; }

    /// <summary>
    /// Mensaje de error si falló.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Crea un resultado exitoso con eliminación.
    /// </summary>
    public static SecretDeleteResult Succeeded(bool deleted = true)
        => new() { Success = true, Deleted = deleted };

    /// <summary>
    /// Crea un resultado de error.
    /// </summary>
    public static SecretDeleteResult Failed(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// T16 — Excepción para secretos corruptos que requieren re-emparejamiento.
/// </summary>
public class SecretCorruptedException : Exception
{
    /// <summary>
    /// Nombre del secreto corrupto.
    /// </summary>
    public string SecretName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretCorruptedException"/> class.
    /// </summary>
    public SecretCorruptedException(string secretName)
        : base($"Secret '{secretName}' is corrupted and cannot be recovered.")
    {
        this.SecretName = secretName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretCorruptedException"/> class.
    /// </summary>
    public SecretCorruptedException(string secretName, Exception innerException)
        : base($"Secret '{secretName}' is corrupted and cannot be recovered.", innerException)
    {
        this.SecretName = secretName;
    }
}

/// <summary>
/// T16 — Interfaz para el almacén de secretos seguro.
/// Provee cifrado con DPAPI/DataProtectionProvider y opcionalmente TPM.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Lee un secreto del almacén.
    /// </summary>
    /// <param name="name">Nombre del secreto.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado con el valor si existe.</returns>
    Task<SecretReadResult> ReadAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Escribe un secreto en el almacén.
    /// </summary>
    /// <param name="name">Nombre del secreto.</param>
    /// <param name="value">Valor a guardar (se cifra antes de persistir).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado de la operación.</returns>
    Task<SecretWriteResult> WriteAsync(string name, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Elimina un secreto del almacén.
    /// </summary>
    /// <param name="name">Nombre del secreto.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado de la operación.</returns>
    Task<SecretDeleteResult> DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si un secreto existe.
    /// </summary>
    /// <param name="name">Nombre del secreto.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True si existe.</returns>
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
}
