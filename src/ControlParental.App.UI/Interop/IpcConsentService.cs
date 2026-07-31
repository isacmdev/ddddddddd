// <copyright file="IpcConsentService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Interop;

using ControlParental.Domain;
using DomainConsent = ControlParental.Domain.IConsentService;
using DomainConsentRecord = ControlParental.Domain.ConsentRecord;
using DomainConsentStatus = ControlParental.Domain.ConsentStatus;

/// <summary>
/// T25/T26 — IPC-backed implementation of <see cref="DomainConsent"/>.
/// Persists consent through the Service's SQLite store (Fase 1 of T26,
/// audit finding #2). The local cache holds only the last acknowledged
/// snapshot from the Service — there is no local write path.
/// </summary>
public sealed class IpcConsentService : DomainConsent
{
    private readonly IUIChannel channel;
    private DomainConsentRecord currentRecord = new(DomainConsentStatus.NotStarted, default, null);

    /// <summary>
    /// Initializes a new instance of the <see cref="IpcConsentService"/> class.
    /// </summary>
    /// <param name="channel">The IPC channel used to talk to the Service.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="channel"/> is null.</exception>
    public IpcConsentService(IUIChannel channel)
    {
        this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
    }

    /// <inheritdoc/>
    public bool IsConsentGranted => this.currentRecord.Status == DomainConsentStatus.Granted;

    /// <inheritdoc/>
    public async Task<DomainConsentRecord> GetConsentStatusAsync(CancellationToken ct = default)
    {
        var snapshot = await this.channel.QueryAsync<
            ControlParental.App.UI.GetConsentStatus,
            ControlParental.App.UI.ConsentStatusSnapshot>(
            new ControlParental.App.UI.GetConsentStatus(),
            ct).ConfigureAwait(false);

        if (snapshot is not null)
        {
            this.currentRecord = new DomainConsentRecord(
                snapshot.IsGranted ? DomainConsentStatus.Granted : DomainConsentStatus.NotStarted,
                snapshot.GrantedAt,
                snapshot.GrantedByDeviceId);
        }

        return this.currentRecord;
    }

    /// <inheritdoc/>
    /// <exception cref="ConsentServiceUnavailableException">
    /// Thrown when the Service does not acknowledge the grant (pipe unavailable, timeout,
    /// or unexpected null response). The caller MUST surface this to the user and MUST
    /// NOT advance the onboarding step — see ADR-001 in t26 design.
    /// </exception>
    public async Task GrantConsentAsync(string? grantedByDeviceId, CancellationToken ct = default)
    {
        var snapshot = await this.channel.QueryAsync<
            ControlParental.App.UI.GrantConsent,
            ControlParental.App.UI.ConsentStatusSnapshot>(
            new ControlParental.App.UI.GrantConsent(grantedByDeviceId),
            ct).ConfigureAwait(false);

        if (snapshot is null)
        {
            throw new ConsentServiceUnavailableException(
                "Consent service did not acknowledge the grant. The Service may be unavailable.");
        }

        this.currentRecord = new DomainConsentRecord(
            snapshot.IsGranted ? DomainConsentStatus.Granted : DomainConsentStatus.NotStarted,
            snapshot.GrantedAt,
            snapshot.GrantedByDeviceId);
    }
}

/// <summary>
/// T25/T26 — Thrown when the consent service cannot acknowledge a grant
/// over IPC. Callers must surface this and refuse to advance the onboarding flow.
/// </summary>
public sealed class ConsentServiceUnavailableException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentServiceUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The error message describing the failure.</param>
    public ConsentServiceUnavailableException(string message)
        : base(message)
    {
    }
}
