// <copyright file="EnforcementLevelQueryHandlerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Moq;
using Xunit;

/// <summary>
/// T26 PR #10 — Regression tests for <see cref="EnforcementLevelQueryHandler"/>.
///
/// The handler is the Service-side aggregator that translates the current
/// <see cref="IEnforcementLevelMonitor"/> snapshot into the wire-level
/// <see cref="EnforcementLevelResponse"/> consumed by App.UI. It maps each
/// issue type to its corresponding check name and preserves the monitor's
/// severity/level so the UI never sees a watered-down picture.
///
/// These tests pin the four canonical T12 scenarios so a future refactor
/// cannot collapse checks into a single "all-pass" or swap the level when
/// only one issue trips.
/// </summary>
public sealed class EnforcementLevelQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenNoIssues_ReturnsAllPassingChecks()
    {
        // Arrange — monitor reports a clean Managed level with no issues; the
        // aggregator must emit four passing checks and echo the level through.
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Managed);
        monitor.SetupGet(m => m.CurrentIssues).Returns(Array.Empty<EnforcementIssue>());
        var handler = new EnforcementLevelQueryHandler(monitor.Object);

        // Act
        var response = await handler.HandleAsync();

        // Assert
        Assert.Equal(EnforcementLevel.Managed, response.Level);
        Assert.Equal(4, response.Checks.Count);
        Assert.All(response.Checks, c => Assert.True(c.IsPassing, $"check {c.CheckName} should pass"));
    }

    [Fact]
    public async Task HandleAsync_WhenServiceNotRunning_FailsServiceRunningCheck()
    {
        // Arrange — service_running check must flip to failing when the
        // monitor reports ServiceNotRunning. The other three remain passing.
        var issues = new[]
        {
            new EnforcementIssue
            {
                Type = EnforcementIssueType.ServiceNotRunning,
                Severity = EnforcementIssueSeverity.Critical,
                Description = "service down",
                DetectedAt = DateTimeOffset.UtcNow,
            },
        };
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Degraded);
        monitor.SetupGet(m => m.CurrentIssues).Returns(issues);
        var handler = new EnforcementLevelQueryHandler(monitor.Object);

        // Act
        var response = await handler.HandleAsync();

        // Assert
        var serviceRunning = Assert.Single(response.Checks, c => c.CheckName == "service_running");
        Assert.False(serviceRunning.IsPassing);
        Assert.All(
            response.Checks.Where(c => c.CheckName != "service_running"),
            c => Assert.True(c.IsPassing, $"{c.CheckName} must stay passing"));
    }

    [Fact]
    public async Task HandleAsync_WhenChildIsAdministrator_FailsChildAccountCheck()
    {
        // Arrange — child_account_standard check fails when the monitor
        // reports the child holds administrator rights.
        var issues = new[]
        {
            new EnforcementIssue
            {
                Type = EnforcementIssueType.ChildIsAdministrator,
                Severity = EnforcementIssueSeverity.Critical,
                Description = "child is admin",
                DetectedAt = DateTimeOffset.UtcNow,
            },
        };
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Degraded);
        monitor.SetupGet(m => m.CurrentIssues).Returns(issues);
        var handler = new EnforcementLevelQueryHandler(monitor.Object);

        // Act
        var response = await handler.HandleAsync();

        // Assert
        var childCheck = Assert.Single(response.Checks, c => c.CheckName == "child_account_standard");
        Assert.False(childCheck.IsPassing);
    }

    [Fact]
    public async Task HandleAsync_WhenMixedIssues_FailsCorrespondingChecksOnly()
    {
        // Arrange — two distinct issues must surface as exactly two failing
        // checks; the others stay passing. This pins the per-check mapping.
        var issues = new[]
        {
            new EnforcementIssue
            {
                Type = EnforcementIssueType.ChildIsAdministrator,
                Severity = EnforcementIssueSeverity.Critical,
                Description = "child is admin",
                DetectedAt = DateTimeOffset.UtcNow,
            },
            new EnforcementIssue
            {
                Type = EnforcementIssueType.PreventiveLayerUnavailable,
                Severity = EnforcementIssueSeverity.Info,
                Description = "no preventive layer",
                DetectedAt = DateTimeOffset.UtcNow,
            },
        };
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Standard);
        monitor.SetupGet(m => m.CurrentIssues).Returns(issues);
        var handler = new EnforcementLevelQueryHandler(monitor.Object);

        // Act
        var response = await handler.HandleAsync();

        // Assert — exactly the two corresponding checks failed.
        Assert.Equal(2, response.Checks.Count(c => !c.IsPassing));
        Assert.Contains(response.Checks, c => c.CheckName == "child_account_standard" && !c.IsPassing);
        Assert.Contains(response.Checks, c => c.CheckName == "preventive_layer" && !c.IsPassing);
        Assert.All(
            response.Checks.Where(c => c.CheckName is "service_running" or "agent_emitting"),
            c => Assert.True(c.IsPassing));
    }

    // ── T12 — Preventive-layer projection pins ─────────────────────────────

    [Fact]
    public async Task HandleAsync_WhenPreventiveLayerIsVerifiedAbsent_FailsCheckButKeepsStandardLevel()
    {
        // Arrange — verified absence must surface as a failing preventive_layer
        // check (truthful non-managed signal) while the effective level stays
        // STANDARD. This pins the spec's "absent ⇒ STANDARD + UI signal" rule.
        var issues = new[]
        {
            new EnforcementIssue
            {
                Type = EnforcementIssueType.PreventiveLayerUnavailable,
                Severity = EnforcementIssueSeverity.Info,
                Description = "no preventive layer (verified absent)",
                DetectedAt = DateTimeOffset.UtcNow,
            },
        };
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Standard);
        monitor.SetupGet(m => m.CurrentIssues).Returns(issues);
        var handler = new EnforcementLevelQueryHandler(monitor.Object);

        // Act
        var response = await handler.HandleAsync();

        // Assert
        Assert.Equal(EnforcementLevel.Standard, response.Level);
        var preventiveCheck = Assert.Single(
            response.Checks,
            c => c.CheckName == "preventive_layer");
        Assert.False(preventiveCheck.IsPassing);
        Assert.All(
            response.Checks.Where(c => c.CheckName != "preventive_layer"),
            c => Assert.True(c.IsPassing, $"{c.CheckName} must stay passing"));
    }

    [Fact]
    public async Task HandleAsync_WhenPreventiveLayerIsIndeterminate_FailsCheckAndReportsDegraded()
    {
        // Arrange — indeterminate detection (Severe) must surface as both a
        // failing preventive_layer check AND a DEGRADED level. The spec
        // forbids uncertainty from collapsing into STANDARD.
        var issues = new[]
        {
            new EnforcementIssue
            {
                Type = EnforcementIssueType.PreventiveLayerUnavailable,
                Severity = EnforcementIssueSeverity.Severe,
                Description = "preventive-layer detection indeterminate",
                DetectedAt = DateTimeOffset.UtcNow,
            },
        };
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Degraded);
        monitor.SetupGet(m => m.CurrentIssues).Returns(issues);
        var handler = new EnforcementLevelQueryHandler(monitor.Object);

        // Act
        var response = await handler.HandleAsync();

        // Assert
        Assert.Equal(EnforcementLevel.Degraded, response.Level);
        var preventiveCheck = Assert.Single(
            response.Checks,
            c => c.CheckName == "preventive_layer");
        Assert.False(preventiveCheck.IsPassing);
        Assert.All(
            response.Checks.Where(c => c.CheckName != "preventive_layer"),
            c => Assert.True(c.IsPassing, $"{c.CheckName} must stay passing"));
    }

    [Fact]
    public async Task HandleAsync_WhenPreventiveLayerIsPresent_OnlyPassesPreventiveCheck()
    {
        // Arrange — positive preventive evidence must NOT emit a
        // PreventiveLayerUnavailable issue, so the check stays passing and
        // the level can be MANAGED.
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(m => m.CurrentLevel).Returns(EnforcementLevel.Managed);
        monitor.SetupGet(m => m.CurrentIssues).Returns(Array.Empty<EnforcementIssue>());
        var handler = new EnforcementLevelQueryHandler(monitor.Object);

        // Act
        var response = await handler.HandleAsync();

        // Assert
        Assert.Equal(EnforcementLevel.Managed, response.Level);
        var preventiveCheck = Assert.Single(
            response.Checks,
            c => c.CheckName == "preventive_layer");
        Assert.True(preventiveCheck.IsPassing);
    }
}
