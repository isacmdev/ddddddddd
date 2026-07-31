// <copyright file="ConsentServiceTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// T25 — Unit tests for ConsentService.
/// </summary>
public class ConsentServiceTests : IDisposable
{
    private readonly ControlParentalDbContext dbContext;
    private readonly ConsentService consentService;

    public ConsentServiceTests()
    {
        var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseInMemoryDatabase(databaseName: $"ConsentTest_{Guid.NewGuid():N}")
            .Options;

        this.dbContext = new ControlParentalDbContext(options);
        this.consentService = new ConsentService(this.dbContext);
    }

    [Fact]
    public async Task GetConsentStatusAsync_WhenNoConsent_ReturnsNotStarted()
    {
        // Act
        var result = await this.consentService.GetConsentStatusAsync();

        // Assert
        Assert.Equal(ConsentStatus.NotStarted, result.Status);
        Assert.Equal(default, result.GrantedAt);
        Assert.Null(result.GrantedByDeviceId);
    }

    [Fact]
    public async Task GrantConsentAsync_WhenFirstTime_SetsGranted()
    {
        // Act
        await this.consentService.GrantConsentAsync(null);

        // Assert
        var result = await this.consentService.GetConsentStatusAsync();
        Assert.Equal(ConsentStatus.Granted, result.Status);
        Assert.NotEqual(default, result.GrantedAt);
        Assert.Equal("local", result.GrantedByDeviceId);
    }

    [Fact]
    public async Task GrantConsentAsync_WhenAlreadyGranted_UpdatesGrantedAt()
    {
        // Arrange - grant consent first time
        await this.consentService.GrantConsentAsync(null);
        var firstGrant = await this.consentService.GetConsentStatusAsync();
        await Task.Delay(10); // Ensure time difference

        // Act - grant consent again
        await this.consentService.GrantConsentAsync("another-device");

        // Assert
        var result = await this.consentService.GetConsentStatusAsync();
        Assert.Equal(ConsentStatus.Granted, result.Status);
        Assert.True(result.GrantedAt > firstGrant.GrantedAt);
        Assert.Equal("another-device", result.GrantedByDeviceId);
    }

    [Fact]
    public void IsConsentGranted_WhenGranted_ReturnsTrue()
    {
        // Arrange
        this.dbContext.Consent.Add(new ConsentDbEntity
        {
            DeviceId = "local",
            Status = ConsentStatus.Granted,
            GrantedAt = DateTimeOffset.UtcNow,
        });
        this.dbContext.SaveChanges();

        // Act & Assert
        Assert.True(this.consentService.IsConsentGranted);
    }

    [Fact]
    public void IsConsentGranted_WhenNotGranted_ReturnsFalse()
    {
        // Arrange - no consent record

        // Act & Assert
        Assert.False(this.consentService.IsConsentGranted);
    }

    public void Dispose()
    {
        this.dbContext.Dispose();
    }
}
