// <copyright file="OverlayManagerTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent.Tests;

using System.Text;
using ControlParental.Domain;
using ControlParental.SessionAgent;
using ControlParental.SessionAgent.Interop;
using FluentAssertions;
using Xunit;

/// <summary>
/// T08 — Tests for OverlayManager, OverlayWindow (blocking enforcement surface)
/// and the independent warning toast surface. Behavioral, native-state tests:
/// real message-pump dispatch (paint, input, timers, DPI/display changes),
/// independent enforcement/warning surfaces, accessible CTA (real child button),
/// topmost re-assertion, and idempotent cursor ownership.
/// Target: &gt;80% line coverage on changed production code + branch report.
/// </summary>
public class OverlayManagerTests : IDisposable
{
    // ── Fixtures ────────────────────────────────────────────────────────

    private OverlayManager? overlayManager;

    /// <summary>
    /// Test cleanup — disposes any manager created during the test.
    /// </summary>
    public void Dispose()
    {
        this.overlayManager?.Dispose();
        this.overlayManager = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates a bounded manager (real surfaces, never covering the whole desktop)
    /// with an optional cursor seam and overridable topmost re-assert interval.
    /// </summary>
    private static OverlayManager NewManager(
        TimeSpan? warningDuration = null,
        List<(bool Show, bool Result)>? cursorCalls = null,
        TimeSpan? topmostReassertInterval = null)
    {
        Func<bool, bool>? cursorSeam = null;
        if (cursorCalls != null)
        {
            cursorSeam = b =>
            {
                var result = Win32Api.ShowCursor(b);
                cursorCalls.Add((b, result));
                return result;
            };
        }

        return new OverlayManager(
            warningDuration ?? TimeSpan.FromMinutes(5),
            cursorSeam,
            (0, 0, 200, 120),
            topmostReassertInterval);
    }

    private static bool SpinUntil(Func<bool> condition, int timeoutMs = 5000)
    {
        return SpinWait.SpinUntil(condition, timeoutMs);
    }

    private static RECT GetRect(IntPtr hwnd)
    {
        _ = Win32Api.GetWindowRect(hwnd, out var rect);
        return rect;
    }

    private static string GetText(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        _ = Win32Api.GetWindowText(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var sb = new StringBuilder(128);
        _ = Win32Api.GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    // ── Constructor Tests ─────────────────────────────────────────────

    [Fact]
    public void Constructor_CreatesOverlayManager()
    {
        // Act
        using var manager = new OverlayManager();

        // Assert
        manager.Should().NotBeNull();
        manager.IsOverlayVisible.Should().BeFalse();
    }

    // ── ShowOverlay Tests ─────────────────────────────────────────────

    [Fact]
    public void ShowOverlay_WithReason_SetsVisibleTrue()
    {
        // Arrange
        this.overlayManager = NewManager();

        // Act
        this.overlayManager.ShowOverlay("Se acabó el tiempo de esta app");

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
        this.overlayManager.BlockWindowHandle.Should().NotBe(IntPtr.Zero);
        Win32Api.IsWindow(this.overlayManager.BlockWindowHandle).Should().BeTrue();
        Win32Api.IsWindowVisible(this.overlayManager.BlockWindowHandle).Should().BeTrue();
    }

    [Fact]
    public void ShowOverlay_WithNullReason_HandlesGracefully()
    {
        // Arrange
        this.overlayManager = NewManager();

        // Act
        this.overlayManager.ShowOverlay(null!);

        // Assert - should not throw
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    [Fact]
    public void ShowOverlay_WithEmptyReason_HandlesGracefully()
    {
        // Arrange
        this.overlayManager = NewManager();

        // Act
        this.overlayManager.ShowOverlay(string.Empty);

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    [Fact]
    public void ShowOverlay_WithCtaLabel_SetsVisibleTrue()
    {
        // Arrange
        this.overlayManager = NewManager();

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
        this.overlayManager = NewManager();
        this.overlayManager.ShowOverlay("Bloqueo");

        // Act
        this.overlayManager.HideOverlay();

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeFalse();
        Win32Api.IsWindowVisible(this.overlayManager.BlockWindowHandle).Should().BeFalse();
    }

    [Fact]
    public void HideOverlay_WhenNotVisible_DoesNotThrow()
    {
        // Arrange
        this.overlayManager = NewManager();

        // Act & Assert
        var act = () => this.overlayManager.HideOverlay();
        act.Should().NotThrow();
    }

    // ── ShowWarning Tests ─────────────────────────────────────────────

    [Fact]
    public void ShowWarning_WithRemainingTime_IsNonBlockingAndExposesWarning()
    {
        // Arrange
        this.overlayManager = NewManager();

        // Act
        this.overlayManager.ShowWarning(10);

        // Assert
        this.overlayManager.IsOverlayVisible.Should().BeFalse();
        this.overlayManager.IsWarningVisible.Should().BeTrue();
        this.overlayManager.CurrentWarningMessage.Should().Contain("10");
        this.overlayManager.WarningWindowHandle.Should().NotBe(IntPtr.Zero);
        Win32Api.IsWindowVisible(this.overlayManager.WarningWindowHandle).Should().BeTrue();
    }

    [Fact]
    public void ShowWarning_ReplacesPreviousAndAutoClosesWithoutChangingBlockingState()
    {
        this.overlayManager = NewManager(TimeSpan.FromMilliseconds(60));
        this.overlayManager.ShowOverlay("blocked");

        this.overlayManager.ShowWarning(10);
        this.overlayManager.ShowWarning(5);

        this.overlayManager.CurrentWarningMessage.Should().Contain("5");
        this.overlayManager.IsOverlayVisible.Should().BeTrue();

        SpinUntil(() => !this.overlayManager.IsWarningVisible).Should().BeTrue();

        // The enforcement surface is untouched after the toast auto-closes.
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
        Win32Api.IsWindowVisible(this.overlayManager.WarningWindowHandle).Should().BeFalse();
    }

    [Fact]
    public void ShowWarning_AtFiveMinutes_ShowsUrgentMessage()
    {
        // Arrange
        this.overlayManager = NewManager();

        // Act
        this.overlayManager.ShowWarning(5);

        // Assert - shows warning without activating the blocking overlay
        this.overlayManager.IsOverlayVisible.Should().BeFalse();
        this.overlayManager.IsWarningVisible.Should().BeTrue();
        this.overlayManager.CurrentWarningMessage.Should().Contain("5");
    }

    [Fact]
    public void ShowWarning_WithZeroMinutes_HandlesGracefully()
    {
        // Arrange
        this.overlayManager = NewManager();

        // Act
        this.overlayManager.ShowWarning(0);

        // Assert - should not throw and must remain non-blocking
        this.overlayManager.IsOverlayVisible.Should().BeFalse();
        this.overlayManager.IsWarningVisible.Should().BeTrue();
    }

    // ── W-06: warning lives on an independent surface ────────────────

    [Fact]
    public void Warning_UsesIndependentSurface()
    {
        // Arrange
        this.overlayManager = NewManager();
        this.overlayManager.ShowOverlay("blocked");
        var blockHandle = this.overlayManager.BlockWindowHandle;

        // Act
        this.overlayManager.ShowWarning(10);

        // Assert — distinct native handles, both visible.
        var warningHandle = this.overlayManager.WarningWindowHandle;
        warningHandle.Should().NotBe(IntPtr.Zero);
        warningHandle.Should().NotBe(blockHandle);
        Win32Api.IsWindowVisible(blockHandle).Should().BeTrue();
        Win32Api.IsWindowVisible(warningHandle).Should().BeTrue();
    }

    [Fact]
    public void ShowBlock_ThenWarning_KeepsBlockSurfaceAndCtaIntact()
    {
        // Arrange — reviewer round-1 repro: Show -> ShowWarning used to resize the same HWND.
        this.overlayManager = NewManager(TimeSpan.FromMilliseconds(150));
        this.overlayManager.ShowOverlay("blocked", "Solicitar más tiempo");
        var blockHandle = this.overlayManager.BlockWindowHandle;
        var ctaHandle = this.overlayManager.CtaButtonHandle;
        var beforeBounds = GetRect(blockHandle);
        beforeBounds.Width.Should().Be(200);
        beforeBounds.Height.Should().Be(120);

        // Act
        this.overlayManager.ShowWarning(10);

        // Assert — block bounds, visibility, CTA and reason are untouched DURING the warning.
        var duringBounds = GetRect(blockHandle);
        duringBounds.Should().Be(beforeBounds);
        Win32Api.IsWindowVisible(blockHandle).Should().BeTrue();
        this.overlayManager.CtaButtonHandle.Should().Be(ctaHandle);
        Win32Api.IsWindow(ctaHandle).Should().BeTrue();
        this.overlayManager.GetBlockRenderPlan().Reason.Should().Be("blocked");
        this.overlayManager.GetBlockRenderPlan().CtaLabel.Should().Be("Solicitar más tiempo");

        // After auto-close the block is still the full enforcement surface.
        SpinUntil(() => !this.overlayManager.IsWarningVisible).Should().BeTrue();
        GetRect(blockHandle).Should().Be(beforeBounds);
        Win32Api.IsWindowVisible(blockHandle).Should().BeTrue();
        this.overlayManager.GetBlockRenderPlan().CtaLabel.Should().Be("Solicitar más tiempo");
    }

    [Fact]
    public void ShowWarning_ThenBlock_DoesNotDisturbBlockSurface()
    {
        // Arrange — other order: warning first, then the enforcement overlay.
        this.overlayManager = NewManager(TimeSpan.FromMilliseconds(150));
        this.overlayManager.ShowWarning(10);

        // Act
        this.overlayManager.ShowOverlay("blocked", "Pedir más tiempo");

        // Assert
        var blockHandle = this.overlayManager.BlockWindowHandle;
        Win32Api.IsWindowVisible(blockHandle).Should().BeTrue();
        GetRect(blockHandle).Width.Should().Be(200);
        GetRect(blockHandle).Height.Should().Be(120);
        this.overlayManager.GetBlockRenderPlan().Reason.Should().Be("blocked");
        Win32Api.IsWindowVisible(this.overlayManager.WarningWindowHandle).Should().BeTrue();
    }

    [Fact]
    public void Warning_BeforeDuringAfterAutoClose_BlockStateNativeAndStable()
    {
        // Arrange
        this.overlayManager = NewManager(TimeSpan.FromMilliseconds(80));
        this.overlayManager.ShowOverlay("blocked", null);
        var blockHandle = this.overlayManager.BlockWindowHandle;
        var before = GetRect(blockHandle);

        // During
        this.overlayManager.ShowWarning(10);
        SpinUntil(() => this.overlayManager.IsWarningVisible).Should().BeTrue();
        Win32Api.IsWindowVisible(blockHandle).Should().BeTrue();
        GetRect(blockHandle).Should().Be(before);

        // After auto-close
        SpinUntil(() => !this.overlayManager.IsWarningVisible).Should().BeTrue();
        Win32Api.IsWindowVisible(blockHandle).Should().BeTrue();
        Win32Api.IsWindowVisible(this.overlayManager.WarningWindowHandle).Should().BeFalse();
        GetRect(blockHandle).Should().Be(before);
        this.overlayManager.IsOverlayVisible.Should().BeTrue();
    }

    // ── W-04: message-pump dispatch (paint, input, timers) ────────────

    [Fact]
    public void BlockSurface_PaintsOnOwnerThread()
    {
        // Arrange — painting only happens if the dedicated pump dispatches WM_PAINT.
        this.overlayManager = NewManager();
        this.overlayManager.ShowOverlay("blocked", "OK");

        // Act & Assert
        SpinUntil(() => this.overlayManager.BlockPaintCount > 0).Should().BeTrue();
    }

    [Fact]
    public void WarningSurface_PaintsAndAutoClosesOnOwnerThread()
    {
        // Arrange
        this.overlayManager = NewManager(TimeSpan.FromMilliseconds(80));

        // Act
        this.overlayManager.ShowWarning(5);

        // Assert — real WM_PAINT dispatch and real auto-close timer.
        SpinUntil(() => this.overlayManager.WarningPaintCount > 0).Should().BeTrue();
        SpinUntil(() => !this.overlayManager.IsWarningVisible).Should().BeTrue();
        SpinUntil(() => !Win32Api.IsWindowVisible(this.overlayManager.WarningWindowHandle)).Should().BeTrue();
    }

    [Fact]
    public void CtaClick_ByMouse_RaisesCtaEvent()
    {
        // Arrange
        this.overlayManager = NewManager();
        var eventRaised = false;
        this.overlayManager.CtaClicked += () => eventRaised = true;
        this.overlayManager.ShowOverlay("Test", "Click me");
        var cta = this.overlayManager.CtaButtonHandle;
        cta.Should().NotBe(IntPtr.Zero);

        // Act — real button hit-testing: down + up inside the button.
        var pt = (10 << 16) | 10;
        _ = Win32Api.SendMessage(cta, Win32Api.WM_LBUTTONDOWN, new IntPtr(Win32Api.MK_LBUTTON), new IntPtr(pt));
        _ = Win32Api.SendMessage(cta, Win32Api.WM_LBUTTONUP, IntPtr.Zero, new IntPtr(pt));

        // Assert
        SpinUntil(() => eventRaised).Should().BeTrue();
    }

    [Fact]
    public void CtaClick_ByEnterKey_RaisesCtaEvent()
    {
        // Arrange
        this.overlayManager = NewManager();
        var eventRaised = false;
        this.overlayManager.CtaClicked += () => eventRaised = true;
        this.overlayManager.ShowOverlay("Test", "OK");
        var cta = this.overlayManager.CtaButtonHandle;

        // Act — keyboard activation through the real button (default pushbutton).
        _ = Win32Api.SendMessage(cta, Win32Api.WM_KEYDOWN, new IntPtr(0x0D), IntPtr.Zero);
        _ = Win32Api.SendMessage(cta, Win32Api.WM_KEYUP, new IntPtr(0x0D), IntPtr.Zero);

        // Assert
        SpinUntil(() => eventRaised).Should().BeTrue();
    }

    [Fact]
    public void CtaClick_BySpaceKey_RaisesCtaEvent()
    {
        // Arrange
        this.overlayManager = NewManager();
        var eventRaised = false;
        this.overlayManager.CtaClicked += () => eventRaised = true;
        this.overlayManager.ShowOverlay("Test", "OK");
        var cta = this.overlayManager.CtaButtonHandle;

        // Act — Space activates a push button natively.
        _ = Win32Api.SendMessage(cta, Win32Api.WM_KEYDOWN, new IntPtr(0x20), IntPtr.Zero);
        _ = Win32Api.SendMessage(cta, Win32Api.WM_KEYUP, new IntPtr(0x20), IntPtr.Zero);

        // Assert
        SpinUntil(() => eventRaised).Should().BeTrue();
    }

    // ── W-04: DPI / accessibility / topmost ───────────────────────────

    [Fact]
    public void BlockSurface_ReportsRealWindowDpi()
    {
        // Arrange
        this.overlayManager = NewManager();
        this.overlayManager.ShowOverlay("blocked");

        // Act
        var plan = this.overlayManager.GetBlockRenderPlan();

        // Assert — real DPI from the actual window, not a hardcoded 96.
        var nativeDpi = Win32Api.GetDpiForWindow(this.overlayManager.BlockWindowHandle);
        plan.Dpi.Should().Be(nativeDpi);
        plan.Dpi.Should().BeGreaterThanOrEqualTo(96u);
    }

    [Fact]
    public void Cta_IsAccessibleChildButton()
    {
        // Arrange
        this.overlayManager = NewManager();
        this.overlayManager.ShowOverlay("blocked", "Solicitar más tiempo");
        var cta = this.overlayManager.CtaButtonHandle;
        cta.Should().NotBe(IntPtr.Zero);

        // Act
        var className = GetClassName(cta);
        var text = GetText(cta);
        var style = unchecked((long)Win32Api.GetWindowLongPtr(cta, Win32Api.GWL_STYLE));
        var id = Win32Api.GetDlgCtrlID(cta);

        // Assert — a real child button: native UIA/MSAA semantics, tabstop, label and id.
        className.Should().Be("Button");
        text.Should().Be("Solicitar más tiempo");
        (style & Win32Api.WS_TABSTOP).Should().NotBe(0);
        (style & Win32Api.WS_CHILD).Should().NotBe(0);
        id.Should().Be(0x4D01);
        Win32Api.IsWindowEnabled(cta).Should().BeTrue();
    }

    [Fact]
    public void Cta_GetsKeyboardFocus_OnShow()
    {
        // Arrange
        this.overlayManager = NewManager();
        this.overlayManager.ShowOverlay("blocked", "OK");

        // Act & Assert — the surface takes focus and routes it to the CTA.
        SpinUntil(() => this.overlayManager.CtaFocused).Should().BeTrue();
    }

    [Fact]
    public void BlockSurface_IsTopmostAfterShow()
    {
        // Arrange
        this.overlayManager = NewManager();
        this.overlayManager.ShowOverlay("blocked");

        // Act
        var exStyle = unchecked((long)Win32Api.GetWindowLongPtr(this.overlayManager.BlockWindowHandle, Win32Api.GWL_EXSTYLE));

        // Assert — WS_EX_TOPMOST is really set.
        (exStyle & Win32Api.WS_EX_TOPMOST).Should().NotBe(0);
    }

    [Fact]
    public void BlockSurface_ReassertsTopmostPeriodically()
    {
        // Arrange — very short re-assert interval proves the WM_TIMER pump path fires.
        this.overlayManager = NewManager(topmostReassertInterval: TimeSpan.FromMilliseconds(80));
        this.overlayManager.ShowOverlay("blocked");

        // Act & Assert
        var before = this.overlayManager.TopmostReassertCount;
        SpinUntil(() => this.overlayManager.TopmostReassertCount > before, 3000).Should().BeTrue();
        var exStyle = unchecked((long)Win32Api.GetWindowLongPtr(this.overlayManager.BlockWindowHandle, Win32Api.GWL_EXSTYLE));
        (exStyle & Win32Api.WS_EX_TOPMOST).Should().NotBe(0);
    }

    [Fact]
    public void BlockSurface_OnDpiChanged_UpdatesDpiAndScalesCta()
    {
        // Arrange
        this.overlayManager = NewManager();
        this.overlayManager.ShowOverlay("blocked", "OK");
        // Act — simulate WM_DPICHANGED (wParam = new DPI in HIWORD).
        var suggested = new RECT { Left = 0, Top = 0, Right = 400, Bottom = 300 };
        _ = Win32Api.SendMessage(
            this.overlayManager.BlockWindowHandle,
            Win32Api.WM_DPICHANGED,
            new IntPtr(192 << 16),
            ref suggested);

        // Assert — real window DPI updated and CTA geometry scaled up.
        this.overlayManager.GetBlockRenderPlan().Dpi.Should().Be(192);
        var ctaAfter = this.overlayManager.GetBlockRenderPlan().CtaBounds.Width;
        ctaAfter.Should().BeGreaterThan(0);
        this.overlayManager.GetBlockRenderPlan().Dpi.Should().Be(192);
    }

    [Fact]
    public void WarningSurface_OnDpiChanged_HonorsSuggestedRect()
    {
        // Arrange
        this.overlayManager = NewManager();
        this.overlayManager.ShowWarning(10);
        var warningHandle = this.overlayManager.WarningWindowHandle;
        warningHandle.Should().NotBe(IntPtr.Zero);

        // Act — WM_DPICHANGED with a suggested RECT (144 DPI).
        var suggested = new RECT { Left = 10, Top = 10, Right = 500, Bottom = 400 };
        _ = Win32Api.SendMessage(warningHandle, Win32Api.WM_DPICHANGED, new IntPtr(144 << 16), ref suggested);

        // Assert — toast re-scaled and placed inside the suggested rect (right-top).
        var rect = GetRect(warningHandle);
        rect.Left.Should().Be(10);
        rect.Top.Should().BeGreaterThanOrEqualTo(10);
        rect.Right.Should().BeLessThanOrEqualTo(500);
        rect.Width.Should().BeGreaterThan(300);
        rect.Height.Should().BeGreaterThan(90);
    }

    // ── Cursor ownership ──────────────────────────────────────────────

    [Fact]
    public void BlockShowHide_BalancesCursor()
    {
        // Arrange
        var calls = new List<(bool Show, bool Result)>();
        this.overlayManager = NewManager(cursorCalls: calls);
        this.overlayManager.ShowOverlay("blocked");

        // Act
        this.overlayManager.HideOverlay();

        // Assert — exactly one hide and one un-hide, ref count back to zero.
        this.overlayManager.CursorHideRefs.Should().Be(0);
        calls.Count(c => c.Show).Should().Be(1); // ShowCursor(true) on release
        calls.Count(c => !c.Show).Should().Be(1); // ShowCursor(false) on acquire
    }

    [Fact]
    public void RepeatedShow_BalancesCursor()
    {
        // Arrange
        var calls = new List<(bool Show, bool Result)>();
        this.overlayManager = NewManager(cursorCalls: calls);
        this.overlayManager.ShowOverlay("one");
        this.overlayManager.ShowOverlay("two"); // idempotent while visible

        // Act
        this.overlayManager.HideOverlay();
        this.overlayManager.ShowOverlay("three");
        this.overlayManager.HideOverlay();

        // Assert — each visible interval owns one balanced cursor hide.
        this.overlayManager.CursorHideRefs.Should().Be(0);
        calls.Count(c => c.Show).Should().Be(calls.Count(c => !c.Show));
        calls.Count(c => !c.Show).Should().Be(2);
    }

    [Fact]
    public void DisposeWhileBlockVisible_RestoresCursor()
    {
        // Arrange
        var calls = new List<(bool Show, bool Result)>();
        this.overlayManager = NewManager(cursorCalls: calls);
        this.overlayManager.ShowOverlay("blocked");
        this.overlayManager.CursorHideRefs.Should().Be(1);

        // Act
        this.overlayManager.Dispose();

        // Assert — dispose restored the cursor fully (ref count zero, balanced calls).
        this.overlayManager.CursorHideRefs.Should().Be(0);
        calls.Count(c => c.Show).Should().Be(calls.Count(c => !c.Show));
    }

    [Fact]
    public void Warning_10_5_0_Sequence_NeverTouchesCursor()
    {
        // Arrange
        var calls = new List<(bool Show, bool Result)>();
        this.overlayManager = NewManager(TimeSpan.FromMilliseconds(70), cursorCalls: calls);

        // Act — a warning sequence must not unbalance a blocked overlay's cursor.
        this.overlayManager.ShowWarning(10);
        this.overlayManager.ShowWarning(5);
        this.overlayManager.ShowWarning(0);
        SpinUntil(() => !this.overlayManager.IsWarningVisible).Should().BeTrue();

        // Assert — warnings never call ShowCursor at all.
        calls.Should().BeEmpty();
    }

    [Fact]
    public void WarningWithVisibleBlock_NeverTouchesCursor()
    {
        // Arrange
        var calls = new List<(bool Show, bool Result)>();
        this.overlayManager = NewManager(TimeSpan.FromMilliseconds(70), cursorCalls: calls);
        this.overlayManager.ShowOverlay("blocked");
        this.overlayManager.CursorHideRefs.Should().Be(1);
        var cursorCallsBeforeWarning = calls.Count;

        // Act
        this.overlayManager.ShowWarning(5);
        SpinUntil(() => !this.overlayManager.IsWarningVisible).Should().BeTrue();

        // Assert — the warning does not disturb the block's cursor ownership.
        calls.Count.Should().Be(cursorCallsBeforeWarning);
        this.overlayManager.CursorHideRefs.Should().Be(1);
    }

    // ── Dispose Tests ────────────────────────────────────────────────

    [Fact]
    public void Dispose_CalledOnce_DisposesWithoutError()
    {
        // Arrange
        this.overlayManager = NewManager();
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
        this.overlayManager = NewManager();

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
        this.overlayManager = NewManager();
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
        this.overlayManager = NewManager();

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
        this.overlayManager = NewManager();
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
/// T08 — Tests for OverlayWindow blocking-surface static helpers and direct
/// window behavior (bounded geometry so no real desktop is covered).
/// </summary>
public class OverlayWindowTests : IDisposable
{
    private OverlayWindow? overlay;

    public void Dispose()
    {
        this.overlay?.Dispose();
        this.overlay = null;
        GC.SuppressFinalize(this);
    }

    private OverlayWindow NewBounded(Func<bool, bool>? showCursor = null, TimeSpan? topmostReassertInterval = null)
    {
        this.overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true, showCursor, topmostReassertInterval);
        return this.overlay;
    }

    [Fact]
    public void BuildRenderPlan_PreservesContentAndCreatesAccessibleHitTarget()
    {
        var plan = OverlayWindow.BuildRenderPlan(
            new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            "Tiempo agotado", "Solicitar más tiempo", 144);

        plan.Background.Should().Be("#101820");
        plan.Reason.Should().Be("Tiempo agotado");
        plan.CtaBounds.Width.Should().BeGreaterThan(0);
        plan.CtaBounds.Height.Should().BeGreaterThan(0);
        plan.CtaBounds.Right.Should().BeLessThanOrEqualTo(plan.Bounds.Right);
        plan.CtaBounds.Bottom.Should().BeLessThanOrEqualTo(plan.Bounds.Bottom);
        plan.Dpi.Should().Be(144);
        plan.IsKeyboardFocusable.Should().BeTrue();
    }

    [Fact]
    public void BuildRenderPlan_ScaledGeometry_ForHighDpi()
    {
        // Act
        var plan192 = OverlayWindow.BuildRenderPlan(
            new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1080 },
            "reason", "CTA", 192);

        // Assert — high DPI is reflected in the render plan and geometry remains valid.
        plan192.Dpi.Should().Be(192);
        plan192.CtaBounds.Width.Should().BeGreaterThan(0);
        plan192.CtaBounds.Height.Should().BeGreaterThan(0);
    }

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

    // ── OverlayWindow direct integration tests (bounded) ─────────────

    [Fact]
    public void InitialState_NotVisible()
    {
        // Arrange
        using var overlay = new OverlayWindow();

        // Assert
        overlay.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Show_ThenHide_SetsVisibilityCorrectly()
    {
        // Arrange
        using var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true);

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
    public void Show_WithNullReason_HandlesGracefully()
    {
        // Arrange
        using var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true);

        // Act
        var act = () => overlay.Show(null!);

        // Assert
        act.Should().NotThrow();
        overlay.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void Show_ThenShowAgain_UpdatesReason()
    {
        // Arrange
        using var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true);

        // Act
        overlay.Show("First reason");
        overlay.Show("Second reason", "CTA Label");

        // Assert
        overlay.IsVisible.Should().BeTrue();
        overlay.GetCurrentReason().Should().Be("Second reason");
        overlay.GetCurrentCtaLabel().Should().Be("CTA Label");
    }

    [Fact]
    public void Dispose_DestroysWindow()
    {
        // Arrange
        var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true);
        overlay.Show("Test");

        // Act
        overlay.Dispose();

        // Assert - window should be destroyed
        overlay.IsVisible.Should().BeFalse();
        overlay.IsWindowCreated().Should().BeFalse();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true);
        overlay.Show("Test");

        // Act
        overlay.Dispose();
        overlay.Dispose();
        overlay.Dispose();

        // Assert - should not throw
        overlay.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Show_AfterDispose_DoesNotThrow()
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
    public void Hide_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var overlay = new OverlayWindow();
        overlay.Dispose();

        // Act
        var act = () => overlay.Hide();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetCurrentReason_AfterShow_ReturnsReason()
    {
        // Arrange
        using var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true);
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
        using var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true);
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
        using var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true);
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

    [Fact]
    public void CursorOwnership_DisposeWhileVisible_RestoresCursorAtWindowLevel()
    {
        // Arrange
        var calls = new List<(bool Show, bool Result)>();
        Func<bool, bool> seam = b =>
        {
            var result = Win32Api.ShowCursor(b);
            calls.Add((b, result));
            return result;
        };
        var overlay = new OverlayWindow(0, 0, 200, 120, hideCursor: true, seam);
        overlay.Show("blocked");

        // Act
        overlay.Dispose();

        // Assert
        calls.Count(c => c.Show).Should().Be(calls.Count(c => !c.Show));
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
