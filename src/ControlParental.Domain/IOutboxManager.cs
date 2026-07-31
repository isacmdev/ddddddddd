// <copyright file="IOutboxManager.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Interfaz para el gestor de outbox (patrón outbox para confiabilidad).
/// </summary>
public interface IOutboxManager
{
    /// <summary>
    /// Encola un evento en la outbox.
    /// </summary>
    /// <param name="tableName">Nombre de la tabla destino.</param>
    /// <param name="payload">Payload JSON del evento.</param>
    /// <param name="dedupKey">Clave de desduplicación.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnqueueAsync(
        string tableName,
        object payload,
        string dedupKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los eventos pendientes de la outbox.
    /// </summary>
    /// <param name="limit">Máximo número de eventos.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Lista de eventos pendientes.</returns>
    Task<IReadOnlyList<OutboxEntry>> GetPendingEntriesAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un evento como enviado.
    /// </summary>
    /// <param name="id">ID del evento.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkSentAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un evento como fallido.
    /// </summary>
    /// <param name="id">ID del evento.</param>
    /// <param name="error">Mensaje de error.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkFailedAsync(int id, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el conteo de eventos pendientes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// T23 — Enqueues an admin notification for integrity verdict escalation.
    /// </summary>
    /// <param name="notificationType">Type of notification: "integrity_warning" | "integrity_degrade_pending" | "integrity_degraded" | "circuit_opened".</param>
    /// <param name="title">Notification title.</param>
    /// <param name="body">Notification body.</param>
    /// <param name="timestamp">When the notification was created.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EnqueueIntegrityNotificationAsync(
        string notificationType,
        string title,
        string body,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Entrada de la outbox.
/// </summary>
public class OutboxEntry
{
    /// <summary>
    /// ID de la entrada.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Tabla destino.
    /// </summary>
    public required string TableName { get; init; }

    /// <summary>
    /// Payload JSON.
    /// </summary>
    public required string PayloadJson { get; init; }

    /// <summary>
    /// Clave de desduplicación.
    /// </summary>
    public required string DedupKey { get; init; }

    /// <summary>
    /// Número de intentos.
    /// </summary>
    public int AttemptCount { get; init; }

    /// <summary>
    /// Fecha de creación.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Fecha del último intento.
    /// </summary>
    public DateTimeOffset? LastAttemptAt { get; init; }

    /// <summary>
    /// Último error.
    /// </summary>
    public string? LastError { get; init; }
}
