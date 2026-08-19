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
    private readonly IDbContextFactory<ControlParentalDbContext> dbContextFactory;
    private readonly ITimeProvider timeProvider;
    private const string DefaultDeviceId = "local";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsentService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    /// <exception cref="ArgumentNullException">Thrown when dbContext is null.</exception>
    public ConsentService(IDbContextFactory<ControlParentalDbContext> dbContextFactory, ITimeProvider timeProvider)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc/>
    public bool IsConsentGranted
    {
        get
        {
            using var dbContext = this.dbContextFactory.CreateDbContext();
            return dbContext.Consent.Any(e => e.Status == ConsentStatus.Granted);
        }
    }

    /// <inheritdoc/>
    public async Task<ConsentRecord> GetConsentStatusAsync(CancellationToken ct = default)
    {
        await using var dbContext = this.dbContextFactory.CreateDbContext();

        var consent = await dbContext.Consent
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
        await using var dbContext = this.dbContextFactory.CreateDbContext();

        var existing = await dbContext.Consent
            .FirstOrDefaultAsync(e => e.DeviceId == DefaultDeviceId, ct);

        if (existing != null)
        {
            existing.Status = ConsentStatus.Granted;
            existing.GrantedAt = this.timeProvider.WallClockNow;
            existing.GrantedByDeviceId = grantedByDeviceId ?? DefaultDeviceId;
        }
        else
        {
            dbContext.Consent.Add(new ConsentDbEntity
            {
                DeviceId = DefaultDeviceId,
                Status = ConsentStatus.Granted,
                GrantedAt = this.timeProvider.WallClockNow,
                GrantedByDeviceId = grantedByDeviceId ?? DefaultDeviceId,
            });
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
