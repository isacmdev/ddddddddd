// <copyright file="ConsentServiceTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using Microsoft.Data.Sqlite;
using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// T25 — Unit tests for ConsentService.
/// </summary>
public class ConsentServiceTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2032, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private readonly ControlParentalDbContext dbContext;
    private readonly SqliteConnection connection;
    private readonly FakeTimeProvider timeProvider;
    private readonly ConsentService consentService;
    private readonly TrackingDbContextFactory dbContextFactory;

    public ConsentServiceTests()
    {
        this.connection = new SqliteConnection("Data Source=:memory:");
        this.connection.Open();

        var options = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseSqlite(this.connection)
            .Options;

        this.dbContext = new ControlParentalDbContext(options);
        this.dbContext.Database.EnsureCreated();
        this.timeProvider = new FakeTimeProvider(FixedNow);
        this.dbContextFactory = new TrackingDbContextFactory(options);
        this.consentService = new ConsentService(this.dbContextFactory, this.timeProvider);
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
        Assert.Equal(FixedNow, result.GrantedAt);
        Assert.Equal("local", result.GrantedByDeviceId);
    }

    [Fact]
    public async Task GrantConsentAsync_WhenAlreadyGranted_UpdatesGrantedAt()
    {
        // Arrange - grant consent first time
        await this.consentService.GrantConsentAsync(null);
        this.timeProvider.SetWallClockNow(FixedNow.AddMinutes(5));

        // Act - grant consent again
        await this.consentService.GrantConsentAsync("another-device");

        // Assert
        var result = await this.consentService.GetConsentStatusAsync();
        Assert.Equal(ConsentStatus.Granted, result.Status);
        Assert.Equal(FixedNow.AddMinutes(5), result.GrantedAt);
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
            GrantedAt = FixedNow,
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
        this.connection.Dispose();
    }

    private sealed class FakeTimeProvider : ITimeProvider
    {
        public FakeTimeProvider(DateTimeOffset wallClock)
        {
            this.WallClockNow = wallClock;
        }

        public long MonotonicNow => 0;
        public DateTimeOffset WallClockNow { get; private set; }
        public TimeZoneInfo CurrentZone => TimeZoneInfo.Utc;
        public DateOnly? ServerDate => DateOnly.FromDateTime(this.WallClockNow.UtcDateTime);
        public bool IsServerDateUncertain => false;
        public event EventHandler<TimeChangedEventArgs>? TimeChanged;
        public void SetServerDate(long offsetMs) { }
        public bool DetectClockJump() => false;

        public void SetWallClockNow(DateTimeOffset now) => this.WallClockNow = now;
    }
}
