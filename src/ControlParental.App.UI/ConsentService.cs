// <copyright file="ConsentService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using ControlParental.Domain;

/// <summary>
/// T25 — In-memory implementation of <see cref="IConsentService"/> for the UI process.
/// Real persistence is handled by the Windows Service via IPC.
/// </summary>
public sealed class ConsentService : IConsentService
{
    private ConsentRecord currentConsent = new(ConsentStatus.NotStarted, default, null);
    private bool consentGranted;

    /// <inheritdoc/>
    public bool IsConsentGranted => this.consentGranted;

    /// <inheritdoc/>
    public Task<ConsentRecord> GetConsentStatusAsync(CancellationToken ct = default)
        => Task.FromResult(this.currentConsent);

    /// <inheritdoc/>
    public Task GrantConsentAsync(string? grantedByDeviceId, CancellationToken ct = default)
    {
        this.consentGranted = true;
        this.currentConsent = new ConsentRecord(
            ConsentStatus.Granted,
            DateTimeOffset.UtcNow,
            grantedByDeviceId ?? "local");
        return Task.CompletedTask;
    }
}
