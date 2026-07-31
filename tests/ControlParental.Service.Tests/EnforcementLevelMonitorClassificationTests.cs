// <copyright file="EnforcementLevelMonitorClassificationTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using System;
using System.Collections.Concurrent;
using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T12 — Deterministic classification tests for <see cref="EnforcementLevelMonitor"/>.
///
/// These tests pin the fail-safe matrix mandated by the T12 specification:
/// <list type="bullet">
///   <item>Positive preventive-layer evidence + healthy foundations + standard child → <c>MANAGED</c>.</item>
///   <item>Verified absence / unsupported layer + healthy foundations + standard child → <c>STANDARD</c>.</item>
///   <item>Any indeterminate detection, thrown detector, access-denied detector, or unhealthy foundation → <c>DEGRADED</c>.</item>
///   <item>The configured child account (via <see cref="IAccountManager"/>) is authoritative; missing / admin / unverified child never yields <c>MANAGED</c>.</item>
/// </list>
///
/// Strict-TDD is OFF for this change. The matrix is still asserted with
/// deterministic mocks so behavior is pinned and recoverable from regressions.
/// </summary>
public sealed class EnforcementLevelMonitorClassificationTests : IDisposable
{
    private readonly Mock<IPrivilegeInspector> mockPrivilegeInspector;
    private readonly Mock<IScmController> mockScmController;
    private readonly Mock<IServiceHealthMonitor> mockHealthMonitor;
    private readonly Mock<ITimeProvider> mockTimeProvider;
    private readonly Mock<IAccountManager> mockAccountManager;
    private readonly Mock<IPreventiveLayerDetector> mockDetector;
    private readonly List<EnforcementLevelChangedEventArgs> levelChangeEvents;
    private readonly List<EnforcementIssue> detectedIssues;
    private EnforcementLevelMonitor? monitor;

    public EnforcementLevelMonitorClassificationTests()
    {
        this.mockPrivilegeInspector = new Mock<IPrivilegeInspector>(MockBehavior.Strict);
        this.mockScmController = new Mock<IScmController>(MockBehavior.Strict);
        this.mockHealthMonitor = new Mock<IServiceHealthMonitor>(MockBehavior.Strict);
        this.mockTimeProvider = new Mock<ITimeProvider>(MockBehavior.Strict);
        this.mockAccountManager = new Mock<IAccountManager>(MockBehavior.Strict);
        this.mockDetector = new Mock<IPreventiveLayerDetector>(MockBehavior.Strict);
        this.levelChangeEvents = new List<EnforcementLevelChangedEventArgs>();
        this.detectedIssues = new List<EnforcementIssue>();

        this.mockTimeProvider.SetupGet(t => t.WallClockNow).Returns(DateTimeOffset.UtcNow);
    }

    public void Dispose()
    {
        this.monitor?.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Builds a monitor with the full T12 dependency surface and a configured
    /// baseline of healthy foundations + a standard configured child account.
    /// Individual tests override the bits they care about.
    /// </summary>
    private void BuildMonitor(string configuredChild = "ChildUser")
    {
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockAccountManager.Setup(a => a.GetChildAccountName()).Returns(configuredChild);
        this.mockAccountManager
            .Setup(a => a.IsAccountStandardAsync(configuredChild, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        this.monitor = new EnforcementLevelMonitor(
            privilegeInspector: this.mockPrivilegeInspector.Object,
            scmController: this.mockScmController.Object,
            healthMonitor: this.mockHealthMonitor.Object,
            timeProvider: this.mockTimeProvider.Object,
            onIssueDetected: issue => this.detectedIssues.Add(issue),
            preventiveLayerDetector: this.mockDetector.Object,
            accountManager: this.mockAccountManager.Object);

        this.monitor.LevelChanged += (_, args) => this.levelChangeEvents.Add(args);
    }

    private static PreventiveLayerDetectionResult Result(
        PreventiveLayerDetectionStatus status,
        PreventiveLayerKind? kind = null,
        string detail = "test") =>
        new(status, kind, detail);

    // ── Phase 1 — Classification matrix (Task 1.1) ──────────────────────

    [Fact]
    public async Task EvaluateAsync_WhenPreventiveLayerPresent_AndFoundationsHealthy_ReturnsManaged()
    {
        // Arrange
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.Wdac));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert — positive layer + healthy foundations + standard child ⇒ MANAGED.
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Managed);
        this.monitor.CurrentIssues.Should().NotContain(
            i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPreventiveLayerAbsent_AndFoundationsHealthy_ReturnsStandard()
    {
        // Arrange — verified absence is a truthful non-managed signal.
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Absent, detail: "no policy"));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Standard);
        var issue = this.monitor.CurrentIssues.SingleOrDefault(
            i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable);
        issue.Should().NotBeNull();
        issue!.Severity.Should().Be(EnforcementIssueSeverity.Info);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPreventiveLayerUnsupported_AndFoundationsHealthy_ReturnsStandard()
    {
        // Arrange — unsupported edition cannot host a layer; not equivalent to degraded.
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Unsupported, detail: "home edition"));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Standard);
        var issue = this.monitor.CurrentIssues.SingleOrDefault(
            i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable);
        issue.Should().NotBeNull();
        issue!.Severity.Should().Be(EnforcementIssueSeverity.Info);
    }

    [Fact]
    public async Task EvaluateAsync_WhenPreventiveLayerIndeterminate_ReturnsDegraded()
    {
        // Arrange — uncertainty must NEVER collapse to STANDARD.
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Indeterminate, detail: "unknown edition"));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        var issue = this.monitor.CurrentIssues.SingleOrDefault(
            i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable);
        issue.Should().NotBeNull();
        issue!.Severity.Should().Be(EnforcementIssueSeverity.Severe);
    }

    [Fact]
    public async Task EvaluateAsync_WhenDetectorThrows_ReturnsDegraded()
    {
        // Arrange — exception during detection collapses to DEGRADED (no Info laundering).
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentIssues.Should().Contain(
            i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable
                 && i.Severity >= EnforcementIssueSeverity.Severe);
    }

    [Fact]
    public async Task EvaluateAsync_WhenDetectorAccessDenied_ReturnsDegraded()
    {
        // Arrange — access denied / unauthorized reads are indeterminate, not absent.
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("registry denied"));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentIssues.Should().Contain(
            i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable
                 && i.Severity >= EnforcementIssueSeverity.Severe);
    }

    // ── Phase 1 — Configured child authority (Task 1.2) ─────────────────

    [Fact]
    public async Task EvaluateAsync_WhenNoConfiguredChild_NeverReturnsManaged()
    {
        // Arrange — null configured child means no authoritative child authority.
        this.BuildMonitor(configuredChild: null!);
        this.mockAccountManager.Setup(a => a.GetChildAccountName()).Returns((string?)null);
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.Wdac));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert — even with present layer, missing configured child ⇒ DEGRADED.
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentLevel.Should().NotBe(EnforcementLevel.Managed);
        this.monitor.CurrentIssues.Should().Contain(
            i => i.Type == EnforcementIssueType.ChildIsAdministrator);
    }

    [Fact]
    public async Task EvaluateAsync_WhenConfiguredChildIsAdmin_NeverReturnsManaged()
    {
        // Arrange — child exists but holds admin rights.
        this.BuildMonitor();
        this.mockAccountManager
            .Setup(a => a.IsAccountStandardAsync("ChildUser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.AppLocker));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert — admin child collapses to DEGRADED even with present layer.
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentLevel.Should().NotBe(EnforcementLevel.Managed);
        this.monitor.CurrentIssues.Should().Contain(
            i => i.Type == EnforcementIssueType.ChildIsAdministrator
                 && i.Severity >= EnforcementIssueSeverity.Critical);
    }

    [Fact]
    public async Task EvaluateAsync_WhenChildStandardCheckThrows_NeverReturnsManaged()
    {
        // Arrange — unverifiable child authority ⇒ fail-safe DEGRADED.
        this.BuildMonitor();
        this.mockAccountManager
            .Setup(a => a.IsAccountStandardAsync("ChildUser", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("account store unavailable"));
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.Mdm));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentLevel.Should().NotBe(EnforcementLevel.Managed);
        this.monitor.CurrentIssues.Should().Contain(
            i => i.Type == EnforcementIssueType.ChildIsAdministrator);
    }

    [Fact]
    public async Task EvaluateAsync_WhenAllHealthy_AndPreventivePresent_AndStandardChild_ReturnsManaged()
    {
        // Arrange — full happy path: positive layer + healthy foundations + standard child.
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.Wdac));

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Managed);
        this.monitor.IsCritical.Should().BeFalse();
        this.monitor.CurrentIssues.Should().BeEmpty();
    }

    // ── Phase 1 — Truthful transitions (Task 1.3) ───────────────────────

    [Fact]
    public async Task EvaluateAsync_WhenRecoveringFromIndeterminateToPresent_TransitionsToManaged()
    {
        // Arrange — first evaluation reports indeterminate (DEGRADED), second reports present.
        this.BuildMonitor();
        var detectorResults = new Queue<PreventiveLayerDetectionResult>(new[]
        {
            Result(PreventiveLayerDetectionStatus.Indeterminate, detail: "unknown"),
            Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.Wdac),
        });
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => detectorResults.Count > 0
                ? detectorResults.Dequeue()
                : Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.Wdac));

        // Act
        await this.monitor!.EvaluateAsync();
        var firstLevel = this.monitor.CurrentLevel;
        await this.monitor.EvaluateAsync();
        var secondLevel = this.monitor.CurrentLevel;

        // Assert — first DEGRADED, then recovers to MANAGED with a single LevelChanged event
        // carrying the previous-level/new-level pair.
        firstLevel.Should().Be(EnforcementLevel.Degraded);
        secondLevel.Should().Be(EnforcementLevel.Managed);

        this.levelChangeEvents.Should().HaveCount(2);
        this.levelChangeEvents[0].PreviousLevel.Should().Be(EnforcementLevel.Unknown);
        this.levelChangeEvents[0].NewLevel.Should().Be(EnforcementLevel.Degraded);
        this.levelChangeEvents[1].PreviousLevel.Should().Be(EnforcementLevel.Degraded);
        this.levelChangeEvents[1].NewLevel.Should().Be(EnforcementLevel.Managed);
    }

    [Fact]
    public async Task EvaluateAsync_WhenRecoveringFromIndeterminateToAbsent_TransitionsToStandard()
    {
        // Arrange — verified absence after an indeterminate evaluation should
        // surface STANDARD, not remain DEGRADED.
        this.BuildMonitor();
        var detectorResults = new Queue<PreventiveLayerDetectionResult>(new[]
        {
            Result(PreventiveLayerDetectionStatus.Indeterminate, detail: "unknown"),
            Result(PreventiveLayerDetectionStatus.Absent, detail: "no policy"),
        });
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => detectorResults.Count > 0
                ? detectorResults.Dequeue()
                : Result(PreventiveLayerDetectionStatus.Absent, detail: "no policy"));

        // Act
        await this.monitor!.EvaluateAsync();
        await this.monitor.EvaluateAsync();

        // Assert
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Standard);
        this.monitor.CurrentIssues.Should().Contain(
            i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable
                 && i.Severity == EnforcementIssueSeverity.Info);
    }

    [Fact]
    public async Task EvaluateAsync_WhenStateUnchanged_DoesNotFireDuplicateLevelChange()
    {
        // Arrange — repeated evaluations on the same Windows state must NOT
        // emit duplicate LevelChanged events. They may still emit per-issue
        // IssueDetected callbacks, but the level transition itself is stable.
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Absent, detail: "no policy"));

        // Act
        await this.monitor!.EvaluateAsync();
        var levelChangesAfterFirst = this.levelChangeEvents.Count;
        await this.monitor.EvaluateAsync();
        await this.monitor.EvaluateAsync();

        // Assert — exactly one transition (Unknown → Standard); subsequent
        // evaluations on identical state emit none.
        levelChangesAfterFirst.Should().Be(1);
        this.levelChangeEvents.Should().HaveCount(1);
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Standard);
    }

    [Fact]
    public async Task EvaluateAsync_WhenSameLevel_ButPreventiveSeverityChanges_UpdatesIssueSnapshot()
    {
        // Arrange — a degradation transition is reflected through the issues
        // snapshot; recovery transitions the level back to STANDARD without
        // leaving the severe preventive-layer issue dangling.
        this.BuildMonitor();
        var detectorResults = new Queue<PreventiveLayerDetectionResult>(new[]
        {
            Result(PreventiveLayerDetectionStatus.Indeterminate, detail: "unknown edition"),
            Result(PreventiveLayerDetectionStatus.Absent, detail: "verified absent"),
        });
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => detectorResults.Count > 0
                ? detectorResults.Dequeue()
                : Result(PreventiveLayerDetectionStatus.Absent, detail: "verified absent"));

        // Act
        await this.monitor!.EvaluateAsync();
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        await this.monitor.EvaluateAsync();
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Standard);

        // Assert — the recovered snapshot reflects the truthful Info signal,
        // not the previous Severe one.
        var issue = this.monitor.CurrentIssues.SingleOrDefault(
            i => i.Type == EnforcementIssueType.PreventiveLayerUnavailable);
        issue.Should().NotBeNull();
        issue!.Severity.Should().Be(EnforcementIssueSeverity.Info);
        this.monitor.IsCritical.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_WhenFoundationsHealthy_ButPreventiveIndeterminate_FiresExactlyOneLevelChangePerTransition()
    {
        // Arrange — the spec requires transitions to fire only when the level
        // changes; deterministic re-evaluations on the same state must not
        // duplicate the change event.
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Indeterminate, detail: "unknown"));

        // Act
        await this.monitor!.EvaluateAsync();
        await this.monitor.EvaluateAsync();
        await this.monitor.EvaluateAsync();

        // Assert — exactly one transition (Unknown → Degraded), regardless of how
        // many evaluations repeat on identical state.
        this.levelChangeEvents.Should().HaveCount(1);
        this.levelChangeEvents[0].NewLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
    }

    // ── Remediation: T12 review findings ─────────────────────────────────

    /// <summary>
    /// CRITICAL fix — T12 review finding 1: a preventive-layer detector may be
    /// injected while the configured-child authority (<see cref="IAccountManager"/>)
    /// is null. The legacy fallback used to call <c>IPrivilegeInspector</c>, which
    /// checks the RUNNING service identity, not the CONFIGURED child. Combined
    /// with a Present layer that produced a false MANAGED classification.
    /// The fix must collapse to DEGRADED via a severe ChildIsAdministrator
    /// issue ("No configured child account authority") and never claim MANAGED.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenDetectorInjected_ButAccountManagerIsNull_NeverReturnsManaged()
    {
        // Arrange — T12 miswiring: detector present, accountManager null.
        // The legacy privilege inspector would return true for the running
        // service identity, so without the fix the level would become MANAGED.
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.Wdac));

        this.monitor = new EnforcementLevelMonitor(
            privilegeInspector: this.mockPrivilegeInspector.Object,
            scmController: this.mockScmController.Object,
            healthMonitor: this.mockHealthMonitor.Object,
            timeProvider: this.mockTimeProvider.Object,
            onIssueDetected: issue => this.detectedIssues.Add(issue),
            preventiveLayerDetector: this.mockDetector.Object,
            accountManager: null);

        this.monitor.LevelChanged += (_, args) => this.levelChangeEvents.Add(args);

        // Act
        await this.monitor!.EvaluateAsync();

        // Assert — even with a Present layer, no configured-child authority ⇒ DEGRADED.
        this.monitor.CurrentLevel.Should().NotBe(EnforcementLevel.Managed);
        this.monitor.CurrentLevel.Should().Be(EnforcementLevel.Degraded);
        this.monitor.CurrentIssues.Should().Contain(
            i => i.Type == EnforcementIssueType.ChildIsAdministrator
                 && i.Severity >= EnforcementIssueSeverity.Severe);

        // The configured child must be authoritative even when a configured
        // child is later added; with the wiring above, the configured child is
        // simply absent.
        this.mockDetector.Verify(
            d => d.DetectAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// WARNING fix — T12 review finding 2: OperationCanceledException from the
    /// configured-child authority probe must propagate rather than be converted
    /// into a Severe "Child account standard check failed" issue. The configured
    /// caller (startup / periodic timer / shutdown) must be able to honour the
    /// cancellation request.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenChildStandardCheckIsCancelled_PropagatesOperationCanceled()
    {
        // Arrange — IsAccountStandardAsync throws OperationCanceledException.
        this.BuildMonitor();
        this.mockAccountManager
            .Setup(a => a.IsAccountStandardAsync("ChildUser", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("cancelled by caller"));
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result(PreventiveLayerDetectionStatus.Present, PreventiveLayerKind.Wdac));

        // Act + Assert — cancellation propagates; no degraded snapshot is produced.
        Func<Task> act = () => this.monitor!.EvaluateAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();

        // The detector must have been reached because the cancellation comes
        // from the child-account check that runs before the probe. We assert
        // here that no current issues were synthesised (the cancellation must
        // bypass the result-snapshot path entirely).
        this.monitor.CurrentIssues.Should().NotContain(
            i => i.Description.Contains("Child account standard check failed",
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// WARNING fix — T12 review finding 2: OperationCanceledException from the
    /// preventive-layer detector must propagate, not be turned into a Severe
    /// "Preventive-layer detection failed" Indeterminate result.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenDetectorIsCancelled_PropagatesOperationCanceled()
    {
        // Arrange — preventive-layer detector throws OperationCanceledException.
        this.BuildMonitor();
        this.mockDetector
            .Setup(d => d.DetectAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("cancelled by caller"));

        // Act + Assert — cancellation propagates rather than being swallowed.
        Func<Task> act = () => this.monitor!.EvaluateAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    /// <summary>
    /// WARNING fix — T12 review finding 2: the legacy IPrivilegeInspector path
    /// must also propagate OperationCanceledException instead of treating it as
    /// a not-standard result.
    /// </summary>
    [Fact]
    public async Task EvaluateAsync_WhenLegacyChildStandardCheckIsCancelled_PropagatesOperationCanceled()
    {
        // Arrange — true legacy wiring (no accountManager, no detector).
        this.mockScmController
            .Setup(s => s.IsServiceRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        this.mockHealthMonitor.SetupGet(h => h.IsAgentHealthy).Returns(true);
        this.mockHealthMonitor.SetupGet(h => h.LastAgentHeartbeat).Returns(DateTimeOffset.UtcNow);
        this.mockPrivilegeInspector
            .Setup(p => p.IsChildStandardAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException("cancelled by caller"));

        this.monitor = new EnforcementLevelMonitor(
            privilegeInspector: this.mockPrivilegeInspector.Object,
            scmController: this.mockScmController.Object,
            healthMonitor: this.mockHealthMonitor.Object,
            timeProvider: this.mockTimeProvider.Object,
            onIssueDetected: issue => this.detectedIssues.Add(issue),
            preventiveLayerDetector: null,
            accountManager: null);

        // Act + Assert — cancellation propagates through the legacy path too.
        Func<Task> act = () => this.monitor!.EvaluateAsync();
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
