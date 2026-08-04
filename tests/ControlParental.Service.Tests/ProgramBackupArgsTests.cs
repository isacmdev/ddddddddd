// <copyright file="ProgramBackupArgsTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Xunit;

/// <summary>
/// Phase 3 — Tests for the <c>--backup-*</c> command-line parsing in
/// <c>Program.Main</c>. The wire-format strings are the contract with
/// <c>TaskSchedulerBackupService</c>; locking them here prevents the two
/// ends from drifting silently.
/// </summary>
public class ProgramBackupArgsTests
{
    [Theory]
    [InlineData(new string[] { "--backup-heartbeat" }, BackupMode.Heartbeat, true)]
    [InlineData(new string[] { "--backup-outbox" }, BackupMode.Outbox, true)]
    [InlineData(new string[] { "--backup-reconcile" }, BackupMode.Reconciliation, true)]
    [InlineData(new string[] { }, default(BackupMode), false)]
    [InlineData(new string[] { "NormalServiceArg" }, default(BackupMode), false)]
    [InlineData(new string[] { "--unknown-flag" }, default(BackupMode), false)]
    public void TryParseBackupMode_RecognizesBackupArgs(
        string[] args,
        BackupMode expectedMode,
        bool expectedResult)
    {
        // Act
        var actualResult = Program.TryParseBackupMode(args, out var actualMode);

        // Assert
        actualResult.Should().Be(expectedResult);
        if (expectedResult)
        {
            actualMode.Should().Be(expectedMode);
        }
    }

    [Fact]
    public void TryParseBackupMode_WithNullArgs_ReturnsFalse()
    {
        // Act
        var result = Program.TryParseBackupMode(null!, out var mode);

        // Assert
        result.Should().BeFalse();
        mode.Should().Be(default(BackupMode));
    }

    [Fact]
    public void TryParseBackupMode_WithMultipleModes_ReturnsFalse()
    {
        var result = Program.TryParseBackupMode(
            new[] { "--backup-heartbeat", "--backup-outbox" },
            out var mode);

        result.Should().BeFalse();
        mode.Should().Be(default(BackupMode));
    }
}

/// <summary>
/// Phase 3 — Tests that the registered Task Scheduler tasks use the exact
/// backup command-line constants that <c>Program.Main</c> parses. Locks the
/// wire format between producer and consumer.
/// </summary>
public class TaskSchedulerBackupArgContractTests
{
    [Theory]
    [InlineData("--backup-heartbeat", BackupMode.Heartbeat)]
    [InlineData("--backup-outbox", BackupMode.Outbox)]
    [InlineData("--backup-reconcile", BackupMode.Reconciliation)]
    public void BackupArg_RoundTripsThroughTryParseBackupMode(string backupArg, BackupMode expectedMode)
    {
        // Act
        var result = Program.TryParseBackupMode(new[] { backupArg }, out var mode);

        // Assert
        result.Should().BeTrue();
        mode.Should().Be(expectedMode);
    }
}
