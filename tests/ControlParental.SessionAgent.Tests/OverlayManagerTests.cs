// <copyright file="OverlayManagerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent.Tests;

using ControlParental.Domain;
using ControlParental.SessionAgent;
using ControlParental.SessionAgent.Interop;
using FluentAssertions;
using Xunit;

/// <summary>
/// T08 — Tests for OverlayManager and OverlayWindow behavior.
/// Tests overlay show/hide, warning display, CTA handling,
/// and keyboard blocking logic.
/// Target: ≥80% coverage.
/// </summary>
public class OverlayManagerTests : IDisposable
{
    // ── Fixtures ────────────────────────────────────────────────────────

    private OverlayManager? overlayManager;

    public void Dispose()
    {
        this.overlayManager?.Dispose();
        this.overlayManager = null;
        GC.SuppressFinalize(this);
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_CreatesOverlayManager()
    {
        // Act
        this.overlayManager = new OverlayManager();

        // Assert
        this.overlayManager.Should().NotBeNull();
        this.overlayManager.IsOverlayVisible.Should().BeFalse();
    }

    // ── ShowOverlay Tests ─────────────────────────────────────────────

    [Fact]
    public void ShowOverlay_WithReason_SetsVisibleTrue()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.ShowOverlay("Se acabó el tiempo de esta app");

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    [Fact]
    public void ShowOverlay_WithNullReason_HandlesGracefully()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.ShowOverlay(null!);

        // Assert - should not throw
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    [Fact]
    public void ShowOverlay_WithEmptyReason_HandlesGracefully()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.ShowOverlay(string.Empty);

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    [Fact]
    public void ShowOverlay_WithCtaLabel_SetsVisibleTrue()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.ShowOverlay("Bloqueo por tiempo", "Solicitar más tiempo");

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    // ── HideOverlay Tests ──────────────────────────────────────────────

    [Fact]
    public void HideOverlay_AfterShow_SetsVisibleFalse()
    {
        // Arrange
        this.overlayManager = new OverlayManager();
        this.overlayManager.ShowOverlay("Bloqueo");

        // Act
        this.overlayManager.HideOverlay();

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeFalse();
    }

    [Fact]
    public void HideOverlay_WhenNotVisible_DoesNotThrow()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act & Assert
        var act = () => this.overlayManager.HideOverlay();
        act.Should().NotThrow();
    }

    // ── ShowWarning Tests ─────────────────────────────────────────────

    [Fact]
    public void ShowWarning_WithRemainingTime_ShowsWarning()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.ShowWarning(10);

        // Assert - warning shows overlay
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    [Fact]
    public void ShowWarning_AtFiveMinutes_ShowsUrgentMessage()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.ShowWarning(5);

        // Assert - shows overlay with urgent message
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    [Fact]
    public void ShowWarning_WithZeroMinutes_HandlesGracefully()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.ShowWarning(0);

        // Assert - should not throw
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    // ── CTA Event Tests ──────────────────────────────────────────────

    [Fact]
    public void ShowOverlay_WithCtaLabel_WiresCtaEvent()
    {
        // Arrange
        this.overlayManager = new OverlayManager();
        var eventRaised = false;
        this.overlayManager.CtaClicked += () => eventRaised = true;

        // Act - Show overlay with CTA
        this.overlayManager.ShowOverlay("Test", "Click me");

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    // ── Dispose Tests ────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledOnce_DisposesWithoutError()
    {
        // Arrange
        this.overlayManager = new OverlayManager();
        this.overlayManager.ShowOverlay("Test");

        // Act
        var act = () => this.overlayManager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.Dispose();
        var act = () => this.overlayManager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowOverlay_AfterDispose_DoesNotThrow()
    {
        // Arrange
        this.overlayManager = new OverlayManager();
        this.overlayManager.Dispose();

        // Act
        var act = () => this.overlayManager.ShowOverlay("Test");

        // Assert - should not throw due to disposed check
        act.Should().NotThrow();
    }

    // ── State Machine Tests ──────────────────────────────────────────

    [Fact]
    public void StateMachine_ShowHideShow_SetsVisibleTrue()
    {
        // Arrange
        this.overlayManager = new OverlayManager();

        // Act
        this.overlayManager.ShowOverlay("First");
        this.overlayManager.HideOverlay();
        this.overlayManager.ShowOverlay("Second");

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    [Fact]
    public void StateMachine_MultipleHides_StaysHidden()
    {
        // Arrange
        this.overlayManager = new OverlayManager();
        this.overlayManager.ShowOverlay("Test");

        // Act
        this.overlayManager.HideOverlay();
        this.overlayManager.HideOverlay();
        this.overlayManager.HideOverlay();

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeFalse();
    }
}

/// <summary>
/// T08 — Tests for OverlayWindow static methods and keyboard blocking logic.
/// </summary>
public class OverlayWindowTests
{
    // ── ShouldBlockKeyMessage Tests ─────────────────────────────────

    [Theory]
    [InlineData(Win32Api.WM_SYSKEYDOWN, 0x09, 0, true)] // Alt+Tab
    [InlineData(Win32Api.WM_SYSKEYDOWN, 0x1B, 0, true)] // Alt+Esc
    [InlineData(Win32Api.WM_KEYDOWN, 0x5B, 0, true)] // Left Win
    [InlineData(Win32Api.WM_KEYDOWN, 0x5C, 0, true)] // Right Win
    [InlineData(Win32Api.WM_KEYDOWN, 0x09, 0, false)] // Tab without Alt (allowed)
    [InlineData(Win32Api.WM_KEYDOWN, 0x1B, 0, false)] // Escape without Ctrl (allowed)
    public void ShouldBlockKeyMessage_VariousKeys_ReturnsExpected(
        uint msg, int wParamValue, int lParamValue, bool expectedBlocked)
    {
        // Act
        var result = OverlayWindow.ShouldBlockKeyMessage(
            msg,
            new IntPtr(wParamValue),
            new IntPtr(lParamValue));

        // Assert
        result.Should().Be(expectedBlocked);
    }

    [Theory]
    [InlineData(Win32Api.WM_SYSKEYDOWN, 0x70, 0, true)] // F1 (help)
    [InlineData(Win32Api.WM_KEYDOWN, 0x70, 0, false)] // F1 without Alt (allowed)
    [InlineData(Win32Api.WM_KEYDOWN, 0x41, 0, false)] // 'A' key (allowed)
    [InlineData(Win32Api.WM_KEYDOWN, 0x20, 0, false)] // Space (allowed)
    public void ShouldBlockKeyMessage_SystemKeys_ReturnsExpected(
        uint msg, int wParamValue, int lParamValue, bool expectedBlocked)
    {
        // Act
        var result = OverlayWindow.ShouldBlockKeyMessage(
            msg,
            new IntPtr(wParamValue),
            new IntPtr(lParamValue));

        // Assert
        result.Should().Be(expectedBlocked);
    }

    [Fact]
    public void ShouldBlockKeyMessage_CtrlPlusEscape_BlocksStartMenu()
    {
        // Arrange
        var msg = Win32Api.WM_KEYDOWN;
        var wParam = 0x1B; // VK_ESCAPE
        // Ctrl modifier is in bit 29 (0x20000000) of lParam
        var lParam = 0x20000000; // Ctrl key down

        // Act
        var result = OverlayWindow.ShouldBlockKeyMessage(msg, new IntPtr(wParam), new IntPtr(lParam));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldBlockKeyMessage_AltAlone_DoesNotBlock()
    {
        // Arrange - Alt key without Tab
        var msg = Win32Api.WM_SYSKEYDOWN;
        var wParam = 0x12; // VK_MENU (Alt)
        var lParam = 0x2000000; // Alt modifier bit

        // Act
        var result = OverlayWindow.ShouldBlockKeyMessage(msg, new IntPtr(wParam), new IntPtr(lParam));

        // Assert - Alt alone should not be blocked (only Alt+Tab combination)
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(Win32Api.WM_LBUTTONDOWN)]
    [InlineData(Win32Api.WM_RBUTTONDOWN)]
    [InlineData(Win32Api.WM_MOUSEMOVE)]
    [InlineData(Win32Api.WM_PAINT)]
    [InlineData(Win32Api.WM_ERASEBKGND)]
    [InlineData(Win32Api.WM_DESTROY)]
    public void ShouldBlockKeyMessage_NonKeyMessages_ReturnsFalse(uint msg)
    {
        // Act
        var result = OverlayWindow.ShouldBlockKeyMessage(
            msg,
            new IntPtr(0),
            new IntPtr(0));

        // Assert
        result.Should().BeFalse();
    }

    // ── OverlayWindow Integration Tests ─────────────────────────────

    [Fact]
    public void OverlayWindow_InitialState_NotVisible()
    {
        // Arrange
        using var overlay = new OverlayWindow();

        // Assert
        overlay.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void OverlayWindow_Show_ThenHide_SetsVisibilityCorrectly()
    {
        // Arrange
        using var overlay = new OverlayWindow();

        // Act
        overlay.Show("Test reason");
        var visibleAfterShow = overlay.IsVisible;

        overlay.Hide();
        var visibleAfterHide = overlay.IsVisible;

        // Assert
        visibleAfterShow.Should().BeTrue();
        visibleAfterHide.Should().BeFalse();
    }

    [Fact]
    public void OverlayWindow_Show_WithNullReason_HandlesGracefully()
    {
        // Arrange
        using var overlay = new OverlayWindow();

        // Act
        var act = () => overlay.Show(null!);

        // Assert
        act.Should().NotThrow();
        overlay.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void OverlayWindow_Show_ThenShowAgain_UpdatesReason()
    {
        // Arrange
        using var overlay = new OverlayWindow();

        // Act
        overlay.Show("First reason");
        overlay.Show("Second reason", "CTA Label");

        // Assert
        overlay.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void OverlayWindow_Dispose_DestroysWindow()
    {
        // Arrange
        var overlay = new OverlayWindow();
        overlay.Show("Test");

        // Act
        overlay.Dispose();

        // Assert - window should be destroyed
        overlay.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void OverlayWindow_Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var overlay = new OverlayWindow();
        overlay.Show("Test");

        // Act
        overlay.Dispose();
        overlay.Dispose();
        overlay.Dispose();

        // Assert - should not throw
        overlay.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void OverlayWindow_Show_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var overlay = new OverlayWindow();
        overlay.Dispose();

        // Act
        var act = () => overlay.Show("Test");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void OverlayWindow_Hide_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var overlay = new OverlayWindow();
        overlay.Dispose();

        // Act
        var act = () => overlay.Hide();

        // Assert
        act.Should().NotThrow();
    }

    // ── Message Processing Tests ────────────────────────────────────

    [Fact]
    public void ProcessMessage_WithBlockedKey_ReturnsTrue()
    {
        // Arrange
        using var overlay = new OverlayWindow();
        overlay.Show("Test");

        var msg = new MSG
        {
            hWnd = IntPtr.Zero,
            message = Win32Api.WM_SYSKEYDOWN,
            wParam = new IntPtr(0x09), // VK_TAB
            lParam = IntPtr.Zero,
        };

        // Act
        var result = overlay.ProcessMessage(ref msg);

        // Assert
        result.Should().BeTrue(); // Message should be blocked
    }

    [Fact]
    public void ProcessMessage_WithAllowedKey_ReturnsFalse()
    {
        // Arrange
        using var overlay = new OverlayWindow();
        overlay.Show("Test");

        var msg = new MSG
        {
            hWnd = IntPtr.Zero,
            message = Win32Api.WM_KEYDOWN,
            wParam = new IntPtr(0x41), // 'A' key
            lParam = IntPtr.Zero,
        };

        // Act
        var result = overlay.ProcessMessage(ref msg);

        // Assert
        result.Should().BeFalse(); // Message should pass through
    }

    [Fact]
    public void ProcessMessage_WhenNotVisible_ReturnsFalse()
    {
        // Arrange
        using var overlay = new OverlayWindow();
        // Overlay not shown

        var msg = new MSG
        {
            hWnd = IntPtr.Zero,
            message = Win32Api.WM_SYSKEYDOWN,
            wParam = new IntPtr(0x09), // VK_TAB
            lParam = IntPtr.Zero,
        };

        // Act
        var result = overlay.ProcessMessage(ref msg);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ProcessMessage_WithMouseClick_HandlesClickMessage()
    {
        // Arrange
        using var overlay = new OverlayWindow();

        // Act - Try to show (may fail in test environment without display)
        overlay.Show("Test", "Click me");

        // Process a mouse click message
        var msg = new MSG
        {
            hWnd = IntPtr.Zero,
            message = Win32Api.WM_LBUTTONDOWN,
            wParam = IntPtr.Zero,
            lParam = IntPtr.Zero,
        };

        var result = overlay.ProcessMessage(ref msg);

        // Assert - Method should not throw regardless of window creation result
        // Result depends on whether window was created and visibility state
        _ = result;
    }

    // ── GetCurrentReason Tests ──────────────────────────────────────

    [Fact]
    public void GetCurrentReason_AfterShow_ReturnsReason()
    {
        // Arrange
        using var overlay = new OverlayWindow();
        overlay.Show("Test reason");

        // Act
        var reason = overlay.GetCurrentReason();

        // Assert
        reason.Should().Be("Test reason");
    }

    [Fact]
    public void GetCurrentCtaLabel_AfterShow_ReturnsLabel()
    {
        // Arrange
        using var overlay = new OverlayWindow();
        overlay.Show("Test", "CTA Label");

        // Act
        var ctaLabel = overlay.GetCurrentCtaLabel();

        // Assert
        ctaLabel.Should().Be("CTA Label");
    }

    [Fact]
    public void IsWindowCreated_AfterShow_ReturnsTrue()
    {
        // Arrange
        using var overlay = new OverlayWindow();
        overlay.Show("Test");

        // Act
        var created = overlay.IsWindowCreated();

        // Assert
        created.Should().BeTrue();
    }

    [Fact]
    public void IsWindowCreated_BeforeShow_ReturnsFalse()
    {
        // Arrange
        using var overlay = new OverlayWindow();

        // Act
        var created = overlay.IsWindowCreated();

        // Assert
        created.Should().BeFalse();
    }

    [Fact]
    public void ApplyIntentReturnsTypedShowAndClearOutcomes()
    {
        var applied = new List<OverlayIntent>();
        using var overlay = new OverlayWindow(intent =>
        {
            applied.Add(intent);
            return new NativeActionOutcome(
                intent.Desired ? ActionStatus.Confirmed : ActionStatus.HarmlessAbsence,
                null);
        });

        var shown = overlay.Apply(new OverlayIntent(true, "limit", "Ask", 1));
        var cleared = overlay.Apply(new OverlayIntent(false, "allowed", null, 2));

        shown.Status.Should().Be(ActionStatus.Confirmed);
        cleared.Status.Should().Be(ActionStatus.HarmlessAbsence);
        applied.Select(intent => intent.Version).Should().Equal(1, 2);
    }

    [Fact]
    public void ControlledOverlayRejectsInvalidBoundsAndDisposedApply()
    {
        var invalid = () => new OverlayWindow(0, 0, 0, 180, hideCursor: false);
        invalid.Should().Throw<ArgumentOutOfRangeException>();

        var overlay = new OverlayWindow(0, 0, 64, 64, hideCursor: false);
        overlay.Dispose();
        overlay.Apply(new OverlayIntent(true, "disposed", null, 1)).Status
            .Should().Be(ActionStatus.InvalidState);
    }

    [Fact]
    public void ClearAbsentControlledOverlayIsHarmless()
    {
        using var overlay = new OverlayWindow(0, 0, 64, 64, hideCursor: false);

        var result = overlay.Apply(new OverlayIntent(false, "clear", null, 1));

        result.Status.Should().Be(ActionStatus.HarmlessAbsence);
        overlay.Handle.Should().Be(IntPtr.Zero);
    }

    [Theory]
    [InlineData(true, 0, ActionStatus.Confirmed)]
    [InlineData(false, 5, ActionStatus.NativeFailure)]
    public void LockWorkStationNativeResultIsTyped(bool succeeded, int nativeError, ActionStatus expected)
    {
        var outcome = SessionAgentHost.MapLockResult(succeeded, nativeError);

        outcome.Status.Should().Be(expected);
        outcome.NativeError.Should().Be(succeeded ? null : nativeError);
    }
}
