// <copyright file="IProcessTerminator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// T11 — Termina procesos de aplicaciones bloqueadas.
/// Solo termina procesos no-exentos del sistema.
/// </summary>
public interface IProcessTerminator
{
    /// <summary>
    /// Termina el proceso con el AppId dado.
    /// </summary>
    /// <param name="appId">AppId del proceso a terminar.</param>
    /// <param name="reason">Razón del bloqueo para logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True si el proceso fue terminado exitosamente.</returns>
    Task<bool> TerminateAsync(string appId, string reason, CancellationToken cancellationToken = default);

    Task<ActionStatus> TerminateAsync(
        ObservedProcessTarget target,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si un AppId puede ser terminado (no es proceso del sistema).
    /// </summary>
    /// <param name="appId">AppId a verificar.</param>
    /// <returns>True si el proceso puede ser terminado.</returns>
    bool CanTerminate(string appId);

    /// <summary>
    /// Obtiene el PID de un proceso por su AppId.
    /// </summary>
    /// <param name="appId">AppId a buscar.</param>
    /// <returns>PID del proceso o null si no se encuentra.</returns>
    int? GetProcessId(string appId);
}
