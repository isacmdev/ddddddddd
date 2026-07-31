// <copyright file="IntegrityVerdictHandlerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using ControlParental.Service;
using FluentAssertions;
using Moq;
using Xunit;

/// <summary>
/// T23 — Tests for IntegrityVerdictHandler and its 8 anti-false-positive mechanisms.
/// </summary>
public class IntegrityVerdictHandlerTests : IDisposable
{
    private readonly Mock<IOutboxManager> mockOutboxManager;
    private readonly IntegrityVerdictHandler handler;

    public IntegrityVerdictHandlerTests()
    {
        this.mockOutboxManager = new Mock<IOutboxManager>();
        this.mockOutboxManager
            .Setup(o => o.EnqueueAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        this.handler = new IntegrityVerdictHandler(this.mockOutboxManager.Object);
    }

    public void Dispose()
    {
        this.handler.Dispose();
    }

    /// <summary>
    /// Mechanism 5: Grace period — within 5 min of startup, all verdicts return ShadowWarn.
    /// </summary>
    [Fact]
    public void HandleVerdict_WithinGracePeriod_ReturnsShadowWarn()
    {
        // Arrange - handler already has serviceStartTime = now, so use very recent timestamp
        var recentTimestamp = DateTimeOffset.UtcNow;

        // Act
        var reaction = this.handler.HandleVerdict("revoked", true, recentTimestamp);

        // Assert
        reaction.Action.Should().Be(VerdictAction.ShadowWarn);
        reaction.Reason.Should().Contain("Grace period active");
    }

    /// <summary>
    /// Mechanism 5: After grace period, revoked verdict is processed.
    /// </summary>
    [Fact]
    public void HandleVerdict_AfterGracePeriod_ProcessesVerdict()
    {
        // Arrange - set service start time to 10 minutes ago
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.DisableShadowMode();
        var timestamp = DateTimeOffset.UtcNow;

        // Act - first revoked
        var reaction = this.handler.HandleVerdict("revoked", true, timestamp);

        // Assert - should be Warn since only 1/3 consecutive
        reaction.Action.Should().Be(VerdictAction.Warn);
        reaction.Reason.Should().Contain("1/3");
    }

    /// <summary>
    /// Mechanism 2: Count threshold — first 2 "revoked" return Warn, 3rd triggers escalation/degrade.
    /// </summary>
    [Theory]
    [InlineData(1, VerdictAction.Warn, "1/3")]
    [InlineData(2, VerdictAction.Warn, "2/3")]
    public void HandleVerdict_ConsecutiveRevoked_StagedResponse(int count, VerdictAction expectedAction, string expectedCount)
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.DisableShadowMode();

        var timestamp = DateTimeOffset.UtcNow;

        // Act - send revoked verdicts
        VerdictReaction? finalReaction = null;
        for (int i = 0; i < count; i++)
        {
            finalReaction = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(i));
        }

        // Assert
        finalReaction.Should().NotBeNull();
        finalReaction!.Action.Should().Be(expectedAction);
        finalReaction.Reason.Should().Contain(expectedCount);
    }

    /// <summary>
    /// Mechanism 2 + 4: At 3 consecutive revoked, escalation is triggered (not immediate degrade).
    /// </summary>
    [Fact]
    public void HandleVerdict_ThreeConsecutiveRevoked_TriggersEscalationNotImmediateDegrade()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.DisableShadowMode();

        var timestamp = DateTimeOffset.UtcNow;

        // Act - 3 consecutive revoked
        this.handler.HandleVerdict("revoked", true, timestamp);
        this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(1));
        var third = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(2));

        // Assert - 3rd triggers escalation warning, not immediate Degrade
        third.Action.Should().Be(VerdictAction.Warn);
        third.Reason.Should().Contain("5 minutes"); // escalation delay message
    }

    /// <summary>
    /// Mechanism 4: After escalation timer, next revoked returns Degrade.
    /// </summary>
    [Fact]
    public void HandleVerdict_AfterEscalationWindow_SubsequentRevoked_ReturnsDegrade()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.DisableShadowMode();

        var timestamp = DateTimeOffset.UtcNow;

        // Act - 3 consecutive revoked (triggers escalation)
        this.handler.HandleVerdict("revoked", true, timestamp);
        this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(1));
        this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(2));

        // Next revoked after escalation notification is sent
        var degrade = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(3));

        // Assert - should now degrade
        degrade.Action.Should().Be(VerdictAction.Degrade);
    }

    /// <summary>
    /// Mechanism 1: Timeout graceful — success=false doesn't degrade.
    /// </summary>
    [Fact]
    public void HandleVerdict_SuccessFalse_DoesNotDegrade()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var reaction = this.handler.HandleVerdict(null, false, timestamp);

        // Assert - should be None, not Degrade
        reaction.Action.Should().Be(VerdictAction.None);
        reaction.Reason.Should().Contain("Backend unavailable");
    }

    /// <summary>
    /// Mechanism 1 + 8: After 5 consecutive failures, circuit breaker opens.
    /// </summary>
    [Fact]
    public void HandleVerdict_FiveConsecutiveFailures_OpensCircuitBreaker()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));

        var timestamp = DateTimeOffset.UtcNow;

        // Act - 5 consecutive failures
        for (int i = 0; i < 5; i++)
        {
            this.handler.HandleVerdict(null, false, timestamp.AddSeconds(i));
        }

        // Assert - circuit should be open now
        this.handler.IsCircuitOpen.Should().BeTrue();
    }

    /// <summary>
    /// Mechanism 8: Circuit breaker — open state skips all verdicts.
    /// </summary>
    [Fact]
    public void HandleVerdict_CircuitOpen_ReturnsShadowWarn()
    {
        // Arrange - open the circuit manually
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.SetCircuitOpenedAt(DateTimeOffset.UtcNow);
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.SetCircuitOpenedAt(DateTimeOffset.UtcNow); // Circuit opened just now

        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var reaction = this.handler.HandleVerdict("revoked", true, timestamp);

        // Assert
        reaction.Action.Should().Be(VerdictAction.ShadowWarn);
        reaction.Reason.Should().Contain("Circuit breaker open");
    }

    /// <summary>
    /// Mechanism 8: Circuit breaker — recloses after 15 minutes.
    /// </summary>
    [Fact]
    public void HandleVerdict_CircuitReclosesAfter15Minutes()
    {
        // Arrange - circuit opened 16 minutes ago
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-20));
        this.handler.SetCircuitOpenedAt(DateTimeOffset.UtcNow.AddMinutes(-16));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-20));
        this.handler.SetCircuitOpenedAt(DateTimeOffset.UtcNow.AddMinutes(-16));
        this.handler.DisableShadowMode();

        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var reaction = this.handler.HandleVerdict("revoked", true, timestamp);

        // Assert - circuit should be closed now, so verdict is processed
        this.handler.IsCircuitOpen.Should().BeFalse();
        reaction.Action.Should().Be(VerdictAction.Warn); // First revoked = Warn
    }

    /// <summary>
    /// Mechanism 3: Hysteresis — "trust" verdicts don't recover until 3rd consecutive.
    /// </summary>
    [Fact]
    public void HandleVerdict_TrustAfterRevoked_ResetsCountNotImmediateRecovery()
    {
        // Arrange - 2 revoked verdicts
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));

        var timestamp = DateTimeOffset.UtcNow;
        this.handler.HandleVerdict("revoked", true, timestamp);
        this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(1));

        // Act - 3 trust verdicts
        var trust1 = this.handler.HandleVerdict("trust", true, timestamp.AddSeconds(2));
        var trust2 = this.handler.HandleVerdict("trust", true, timestamp.AddSeconds(3));
        var trust3 = this.handler.HandleVerdict("trust", true, timestamp.AddSeconds(4));

        // Assert - none of the intermediate trusts cause degradation
        trust1.Action.Should().Be(VerdictAction.None);
        trust2.Action.Should().Be(VerdictAction.None);
        trust3.Action.Should().Be(VerdictAction.None);
    }

    /// <summary>
    /// Mechanism 7: Shadow mode — verdicts return ShadowWarn even when threshold met.
    /// </summary>
    [Fact]
    public void HandleVerdict_ShadowMode_ReturnsShadowWarn_EvenWhenThresholdMet()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));

        var timestamp = DateTimeOffset.UtcNow;

        // Act - 3 consecutive revoked in shadow mode
        var reaction1 = this.handler.HandleVerdict("revoked", true, timestamp);
        var reaction2 = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(1));
        var reaction3 = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(2));

        // Assert - all should be ShadowWarn because shadowMode is true
        reaction1.Action.Should().Be(VerdictAction.ShadowWarn);
        reaction2.Action.Should().Be(VerdictAction.ShadowWarn);
        reaction3.Action.Should().Be(VerdictAction.ShadowWarn);
    }

    /// <summary>
    /// Mechanism 7: Shadow mode disable — after DisableShadowMode, verdicts act normally.
    /// </summary>
    [Fact]
    public void HandleVerdict_AfterDisableShadowMode_ActsNormally()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));

        var timestamp = DateTimeOffset.UtcNow;

        // Disable shadow mode
        this.handler.DisableShadowMode();

        // Act - 2 consecutive revoked (threshold is 3)
        var reaction1 = this.handler.HandleVerdict("revoked", true, timestamp);
        var reaction2 = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(1));

        // Assert - should now be Warn, not ShadowWarn
        reaction1.Action.Should().Be(VerdictAction.Warn);
        reaction2.Action.Should().Be(VerdictAction.Warn);
    }

    /// <summary>
    /// Mechanism 6: Staged response — consecutive revoked counts 1/2 = Warn, 3+ = escalation/degrade.
    /// </summary>
    [Fact]
    public void HandleVerdict_StagedResponse_EscalatesThroughStages()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.DisableShadowMode();
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));

        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var r1 = this.handler.HandleVerdict("revoked", true, timestamp);
        var r2 = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(1));
        var r3 = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(2));

        // Assert
        r1.Action.Should().Be(VerdictAction.Warn);
        r2.Action.Should().Be(VerdictAction.Warn);
        r3.Action.Should().Be(VerdictAction.Warn); // 3rd triggers escalation, not immediate degrade
        r3.Reason.Should().Contain("5 minutes"); // Escalation message
    }

    /// <summary>
    /// Mechanism 9 (from spec): Local failure bypasses threshold — returns Degrade directly.
    /// </summary>
    [Fact]
    public void HandleLocalFailure_ReturnsDegrade_DirectlyNotWarn()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.DisableShadowMode();
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var reaction = this.handler.HandleLocalFailure("Signature invalid", timestamp);

        // Assert - local failure bypasses threshold, degrades directly
        reaction.Action.Should().Be(VerdictAction.Degrade);
        reaction.Severity.Should().Be(EnforcementIssueSeverity.Severe);
    }

    /// <summary>
    /// Mechanism 5: Local failure within grace period returns ShadowWarn.
    /// </summary>
    [Fact]
    public void HandleLocalFailure_WithinGracePeriod_ReturnsShadowWarn()
    {
        // Arrange - use recent timestamp (within grace period)
        var recentTimestamp = DateTimeOffset.UtcNow;

        // Act
        var reaction = this.handler.HandleLocalFailure("Signature invalid", recentTimestamp);

        // Assert
        reaction.Action.Should().Be(VerdictAction.ShadowWarn);
        reaction.Reason.Should().Contain("Grace period active");
    }

    /// <summary>
    /// Verdict "trust" resets all counters.
    /// </summary>
    [Fact]
    public void HandleVerdict_Trust_ResetsRevokedCounter()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.DisableShadowMode();
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));

        var timestamp = DateTimeOffset.UtcNow;

        // 2 revoked
        this.handler.HandleVerdict("revoked", true, timestamp);
        this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(1));

        // Act - trust
        var reaction = this.handler.HandleVerdict("trust", true, timestamp.AddSeconds(2));

        // Assert - counter should be reset, so next revoked is 1/3 again
        reaction.Action.Should().Be(VerdictAction.None);

        var nextRevoked = this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(3));
        nextRevoked.Reason.Should().Contain("1/3");
    }

    /// <summary>
    /// Verdict "unknown" resets both counters.
    /// </summary>
    [Fact]
    public void HandleVerdict_Unknown_ResetsBothCounters()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.DisableShadowMode();
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));

        var timestamp = DateTimeOffset.UtcNow;

        // 2 revoked
        this.handler.HandleVerdict("revoked", true, timestamp);
        this.handler.HandleVerdict("revoked", true, timestamp.AddSeconds(1));

        // Act - unknown
        var reaction = this.handler.HandleVerdict("unknown", true, timestamp.AddSeconds(2));

        // Assert - should be Warn (unknown resets counters then warns)
        reaction.Action.Should().Be(VerdictAction.Warn);
    }

    /// <summary>
    /// Circuit opened notification is sent via outbox manager.
    /// </summary>
    [Fact]
    public async Task HandleVerdict_CircuitOpened_SendsNotification()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        this.handler.ResetForTesting();
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));

        var timestamp = DateTimeOffset.UtcNow;

        // Act - 5 consecutive failures to open circuit
        for (int i = 0; i < 5; i++)
        {
            this.handler.HandleVerdict(null, false, timestamp.AddSeconds(i));
        }

        // Assert - notification should have been enqueued
        this.mockOutboxManager.Verify(
            o => o.EnqueueAsync(
                "notifications",
                It.Is<object>(p => p.ToString()!.Contains("Circuit Breaker")),
                It.Is<string>(k => k.Contains("circuit_opened")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Shadow mode starts as true by default.
    /// </summary>
    [Fact]
    public void IsShadowMode_DefaultsToTrue()
    {
        // Arrange & Act
        var newHandler = new IntegrityVerdictHandler();

        // Assert
        newHandler.IsShadowMode.Should().BeTrue();
        newHandler.Dispose();
    }

    /// <summary>
    /// Shadow mode can be disabled.
    /// </summary>
    [Fact]
    public void DisableShadowMode_SetsIsShadowModeToFalse()
    {
        // Arrange
        this.handler.IsShadowMode.Should().BeTrue();

        // Act
        this.handler.DisableShadowMode();

        // Assert
        this.handler.IsShadowMode.Should().BeFalse();
    }

    /// <summary>
    /// Handler works without outbox manager (optional dependency).
    /// </summary>
    [Fact]
    public void HandleVerdict_NoOutboxManager_DoesNotThrow()
    {
        // Arrange
        var handlerWithoutOutbox = new IntegrityVerdictHandler(null);
        handlerWithoutOutbox.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        handlerWithoutOutbox.DisableShadowMode();

        var timestamp = DateTimeOffset.UtcNow;

        // Act - 5 consecutive failures
        VerdictReaction? lastReaction = null;
        for (int i = 0; i < 5; i++)
        {
            lastReaction = handlerWithoutOutbox.HandleVerdict(null, false, timestamp.AddSeconds(i));
        }

        // Assert - should work without throwing
        lastReaction.Should().NotBeNull();
        handlerWithoutOutbox.Dispose();
    }

    /// <summary>
    /// Local failure in shadow mode returns ShadowWarn.
    /// </summary>
    [Fact]
    public void HandleLocalFailure_ShadowMode_ReturnsShadowWarn()
    {
        // Arrange
        this.handler.SetServiceStartTime(DateTimeOffset.UtcNow.AddMinutes(-10));
        // Shadow mode is true by default
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var reaction = this.handler.HandleLocalFailure("Signature invalid", timestamp);

        // Assert
        reaction.Action.Should().Be(VerdictAction.ShadowWarn);
        reaction.Reason.Should().Contain("Shadow mode");
    }
}
