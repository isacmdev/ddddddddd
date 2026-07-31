// <copyright file="TimeProviderTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System;
using ControlParental.Domain;
using Xunit;

/// <summary>
/// T04 — Tests for ITimeProvider behavior.
/// Tests the fake/interface contract; real TimeProvider requires Windows.
/// </summary>
public class TimeProviderTests
{
    // ── Fake ITimeProvider for testing ──────────────────────────────────

    private sealed class FakeTimeProvider : ITimeProvider
    {
        private DateTimeOffset wallClock;
        private long monotonic;
        private TimeZoneInfo zone;
        private DateOnly? serverDate;
        private bool serverDateUncertain;

        public event EventHandler<TimeChangedEventArgs>? TimeChanged;

        public FakeTimeProvider(DateTimeOffset now, string zoneId = "UTC")
        {
            this.wallClock = now;
            this.monotonic = 0;
            this.zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }

        public long MonotonicNow => this.monotonic;
        public DateTimeOffset WallClockNow => this.wallClock;
        public TimeZoneInfo CurrentZone => this.zone;
        public DateOnly? ServerDate => this.serverDate;
        public bool IsServerDateUncertain => this.serverDateUncertain;

        public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(this.wallClock, this.zone);

        public void SetMonotonic(long value) => this.monotonic = value;

        public void SetWallClock(DateTimeOffset value) => this.wallClock = value;

        public void SetZone(string zoneId)
        {
            this.zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }

        public void SetServerDate(long offsetMs) { }

        public void SetServerDate(DateOnly date, bool uncertain = false)
        {
            this.serverDate = date;
            this.serverDateUncertain = uncertain;
        }

        public void SetServerDate(DateOnly serverDate)
        {
            this.serverDate = serverDate;
            this.serverDateUncertain = false;
        }

        public bool DetectClockJump()
        {
            // Simplified: just reset tracking
            return false;
        }

        public void SimulateClockJump(TimeSpan delta)
        {
            this.wallClock = this.wallClock.Add(delta);
            this.TimeChanged?.Invoke(this, new TimeChangedEventArgs
            {
                Reason = TimeChangeReason.ClockJump,
                JumpDelta = delta,
            });
        }

        public void SimulateZoneChange(TimeZoneInfo newZone)
        {
            this.zone = newZone;
            this.TimeChanged?.Invoke(this, new TimeChangedEventArgs
            {
                Reason = TimeChangeReason.ZoneChange,
                NewZone = newZone,
            });
        }
    }

    // ── Tests ───────────────────────────────────────────────────────────

    [Fact]
    public void ServerDate_SetAndGet_ShouldReturnValue()
    {
        // Arrange
        var provider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var serverDate = new DateOnly(2026, 6, 11);

        // Act
        provider.SetServerDate(serverDate);

        // Assert
        Assert.Equal(serverDate, provider.ServerDate);
        Assert.False(provider.IsServerDateUncertain);
    }

    [Fact]
    public void ServerDate_UncertainFlag_ShouldBeSet()
    {
        // Arrange
        var provider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var serverDate = new DateOnly(2026, 6, 11);

        // Act
        provider.SetServerDate(serverDate, uncertain: true);

        // Assert
        Assert.Equal(serverDate, provider.ServerDate);
        Assert.True(provider.IsServerDateUncertain);
    }

    [Fact]
    public void LocalNow_ConvertsToZone_ShouldReturnLocalTime()
    {
        // Arrange — use a known UTC time
        var utcTime = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
        var provider = new FakeTimeProvider(utcTime, "Tokyo Standard Time");

        // Act
        var local = provider.LocalNow;

        // Assert — Tokyo is UTC+9, so 12:00 UTC = 21:00 Tokyo
        Assert.Equal(21, local.Hour);
        Assert.Equal(TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"), provider.CurrentZone);
    }

    [Fact]
    public void TimeChanged_ZoneChange_ShouldFireEvent()
    {
        // Arrange
        var provider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var eventFired = false;
        TimeChangedEventArgs? receivedArgs = null;

        provider.TimeChanged += (s, e) =>
        {
            eventFired = true;
            receivedArgs = e;
        };

        var newZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        // Act
        provider.SimulateZoneChange(newZone);

        // Assert
        Assert.True(eventFired);
        Assert.NotNull(receivedArgs);
        Assert.Equal(TimeChangeReason.ZoneChange, receivedArgs!.Reason);
        Assert.Equal(newZone, receivedArgs.NewZone);
    }

    [Fact]
    public void TimeChanged_ClockJump_ShouldFireEvent()
    {
        // Arrange
        var provider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var eventFired = false;
        TimeChangedEventArgs? receivedArgs = null;

        provider.TimeChanged += (s, e) =>
        {
            eventFired = true;
            receivedArgs = e;
        };

        // Act
        provider.SimulateClockJump(TimeSpan.FromMinutes(30));

        // Assert
        Assert.True(eventFired);
        Assert.NotNull(receivedArgs);
        Assert.Equal(TimeChangeReason.ClockJump, receivedArgs!.Reason);
        Assert.Equal(TimeSpan.FromMinutes(30), receivedArgs.JumpDelta);
    }

    [Fact]
    public void ITimeProvider_Interface_Contract_HasAllMembers()
    {
        // Arrange — verify the interface has all required members
        var type = typeof(ITimeProvider);

        // Assert — all members present
        Assert.NotNull(type.GetProperty(nameof(ITimeProvider.MonotonicNow)));
        Assert.NotNull(type.GetProperty(nameof(ITimeProvider.WallClockNow)));
        Assert.NotNull(type.GetProperty(nameof(ITimeProvider.CurrentZone)));
        Assert.NotNull(type.GetProperty(nameof(ITimeProvider.ServerDate)));
        Assert.NotNull(type.GetProperty(nameof(ITimeProvider.IsServerDateUncertain)));
        Assert.NotNull(type.GetProperty(nameof(ITimeProvider.LocalNow)));
        Assert.NotNull(type.GetMethod(nameof(ITimeProvider.SetServerDate)));
        Assert.NotNull(type.GetMethod(nameof(ITimeProvider.DetectClockJump)));
        Assert.NotNull(type.GetEvent(nameof(ITimeProvider.TimeChanged)));
    }

    [Fact]
    public void LocalDayOfWeek_ShouldReturnCorrectDay()
    {
        // Arrange — Monday 14:00 UTC
        var utcMonday = new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.Zero); // Monday
        var provider = new FakeTimeProvider(utcMonday, "UTC");

        // Act
        var localDow = provider.LocalNow.DayOfWeek;

        // Assert
        Assert.Equal(System.DayOfWeek.Monday, localDow);
    }

    [Fact]
    public void ServerDate_NotSet_ShouldReturnNull()
    {
        // Arrange
        var provider = new FakeTimeProvider(DateTimeOffset.UtcNow);

        // Act & Assert
        Assert.Null(provider.ServerDate);
    }

    [Fact]
    public void TimeChangeReason_HasCorrectValues()
    {
        // Verify enum values are as expected
        Assert.Equal(0, (int)TimeChangeReason.ClockJump);
        Assert.Equal(1, (int)TimeChangeReason.ZoneChange);
        Assert.Equal(2, (int)TimeChangeReason.SystemTimeChanged);
    }
}
