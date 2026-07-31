// <copyright file="TimeProvider.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Diagnostics;
using System.Timers;
using ControlParental.Domain;
using Microsoft.Win32;
using Timer = System.Timers.Timer;

/// <summary>
/// T04 — Windows implementation of ITimeProvider.
/// Uses Stopwatch.GetTimestamp() for monotonic time and DateTimeOffset.UtcNow
/// for wall-clock. Wires SystemEvents.TimeChanged for clock jump detection and
/// a polling timer for zone change detection (TimeZoneChanged is not available
/// in .NET Core/.NET 9 as a SystemEvents event; registry polling is used instead).
/// </summary>
public sealed class TimeProvider : ITimeProvider, IDisposable
{
    private readonly object lockObj = new();
    private readonly Stopwatch monotonicStopwatch;
    private readonly Timer zonePollTimer;
    private readonly string initialZoneId;

    private DateTimeOffset lastWallClock;
    private long lastMonotonic;
    private TimeZoneInfo currentZone;
    private DateOnly? serverDate;
    private bool isServerDateUncertain;
    private bool isDisposed;
    private long clockOffsetMs;

    public TimeProvider()
    {
        this.monotonicStopwatch = Stopwatch.StartNew();
        this.lastMonotonic = this.monotonicStopwatch.ElapsedTicks;
        this.lastWallClock = DateTimeOffset.UtcNow;
        this.currentZone = TimeZoneInfo.Local;
        this.initialZoneId = TimeZoneInfo.Local.Id;

        // Poll zone every 30 seconds (TimeZoneChanged not available in .NET Core)
        this.zonePollTimer = new Timer(30_000);
        this.zonePollTimer.Elapsed += this.OnZonePoll;
        this.zonePollTimer.AutoReset = true;
        this.zonePollTimer.Start();

        SystemEvents.TimeChanged += this.OnTimeChanged;
    }

    /// <inheritdoc />
    public long MonotonicNow => this.monotonicStopwatch.ElapsedTicks;

    /// <inheritdoc />
    public DateTimeOffset WallClockNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public TimeZoneInfo CurrentZone
    {
        get
        {
            lock (this.lockObj)
            {
                return this.currentZone;
            }
        }
    }

    /// <inheritdoc />
    public DateOnly? ServerDate
    {
        get
        {
            lock (this.lockObj)
            {
                return this.serverDate;
            }
        }
    }

    /// <inheritdoc />
    public bool IsServerDateUncertain
    {
        get
        {
            lock (this.lockObj)
            {
                return this.isServerDateUncertain;
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<TimeChangedEventArgs>? TimeChanged;

    /// <inheritdoc />
    public void SetServerDate(long offsetMs)
    {
        lock (this.lockObj)
        {
            this.clockOffsetMs = offsetMs;
        }
    }

    /// <summary>
    /// Current UTC time adjusted by the server clock offset.
    /// </summary>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow.AddMilliseconds(this.clockOffsetMs);

    /// <inheritdoc />
    public bool DetectClockJump()
    {
        lock (this.lockObj)
        {
            var now = DateTimeOffset.UtcNow;
            var monotonicNow = this.monotonicStopwatch.ElapsedTicks;

            // If wall clock moved > 5 seconds while monotonic moved < 1 second,
            // it's a jump (not normal time progression).
            var wallDelta = Math.Abs((now - this.lastWallClock).TotalSeconds);
            var monoDeltaSeconds = Math.Abs((monotonicNow - this.lastMonotonic) * 1.0 / Stopwatch.Frequency);

            if (wallDelta > 5 && monoDeltaSeconds < 1)
            {
                var delta = now - this.lastWallClock;
                this.lastWallClock = now;
                this.lastMonotonic = monotonicNow;
                return true;
            }

            this.lastWallClock = now;
            this.lastMonotonic = monotonicNow;
            return false;
        }
    }

    private void OnTimeChanged(object sender, EventArgs e)
    {
        lock (this.lockObj)
        {
            var now = DateTimeOffset.UtcNow;
            var jumpDelta = now - this.lastWallClock;
            this.lastWallClock = now;

            // Server date becomes uncertain after manual time change
            this.isServerDateUncertain = true;

            this.TimeChanged?.Invoke(this, new TimeChangedEventArgs
            {
                Reason = TimeChangeReason.ClockJump,
                JumpDelta = jumpDelta,
            });
        }
    }

    private void OnZonePoll(object? sender, System.Timers.ElapsedEventArgs e)
    {
        lock (this.lockObj)
        {
            var currentLocal = TimeZoneInfo.Local;
            if (currentLocal.Id != this.currentZone.Id)
            {
                var oldZone = this.currentZone;
                this.currentZone = currentLocal;
                this.lastWallClock = DateTimeOffset.UtcNow; // Reset to avoid false jump detection

                this.TimeChanged?.Invoke(this, new TimeChangedEventArgs
                {
                    Reason = TimeChangeReason.ZoneChange,
                    NewZone = this.currentZone,
                });
            }
        }
    }

    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        SystemEvents.TimeChanged -= this.OnTimeChanged;
        this.zonePollTimer.Stop();
        this.zonePollTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
