// <copyright file="ConsentService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// T25 — Consent service implementation using SQLite persistence.
/// </summary>
public sealed class ConsentService : IConsentService
{
    private readonly ControlParentalDbContext dbContext;
    private const string DefaultDeviceId = "local";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public ConsentService(ControlParentalDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc/>
    public bool IsConsentGranted =>
        this.dbContext.Consent.Any(e => e.Status == ConsentStatus.Granted);

    /// <inheritdoc/>
    public async Task<ConsentRecord> GetConsentStatusAsync(CancellationToken ct = default)
    {
        var consent = await this.dbContext.Consent
            .FirstOrDefaultAsync(e => e.DeviceId == DefaultDeviceId, ct);

        if (consent == null)
        {
            return new ConsentRecord(ConsentStatus.NotStarted, default, null);
        }

        return new ConsentRecord(consent.Status, consent.GrantedAt, consent.GrantedByDeviceId);
    }

    /// <inheritdoc/>
    public async Task GrantConsentAsync(string? grantedByDeviceId, CancellationToken ct = default)
    {
        var existing = await this.dbContext.Consent
            .FirstOrDefaultAsync(e => e.DeviceId == DefaultDeviceId, ct);

        if (existing != null)
        {
            existing.Status = ConsentStatus.Granted;
            existing.GrantedAt = DateTimeOffset.UtcNow;
            existing.GrantedByDeviceId = grantedByDeviceId ?? DefaultDeviceId;
        }
        else
        {
            this.dbContext.Consent.Add(new ConsentDbEntity
            {
                DeviceId = DefaultDeviceId,
                Status = ConsentStatus.Granted,
                GrantedAt = DateTimeOffset.UtcNow,
                GrantedByDeviceId = grantedByDeviceId ?? DefaultDeviceId,
            });
        }

        await this.dbContext.SaveChangesAsync(ct);
    }
}
