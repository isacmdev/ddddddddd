// <copyright file="OverlayWindow.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ControlParental.Domain;
using ControlParental.SessionAgent.Interop;

/// <summary>
/// T08 — Blocking full-surface overlay for the Session Agent. Owns a dedicated
/// STA message-pump thread so painting, input, timers, display changes and DPI
/// changes are really dispatched through a native window procedure. The
/// non-blocking warning toast lives on an independent surface
/// (<see cref="WarningToastWindow"/>) so it can never degrade the enforcement
/// overlay's bounds, reason, CTA or visibility; hiding the UI never deactivates
/// policy (which lives in the Service, not here).
/// </summary>
public sealed class OverlayWindow : IDisposable
{
    // ── Constants ─────────────────────────────────────────────────────

    /// <summary>
    /// Class-name prefix for the overlay window class.
    /// </summary>
    private const string WindowClassNamePrefix = "ControlParentalBlockOverlayClass";

    /// <summary>
    /// Window title.
    /// </summary>
    private const string WindowTitle = "ControlParental Bloqueo";

    /// <summary>
    /// Child id used for the accessible CTA button.
    /// </summary>
    private const int CtaButtonId = 0x4D01;

    /// <summary>
    /// Timer id used to periodically re-assert HWND_TOPMOST.
    /// </summary>
    private const uint TopmostReassertTimerId = 0x4D02;

    /// <summary>
    /// Default cadence for the topmost re-assertion timer.
    /// </summary>
    private static readonly TimeSpan DefaultTopmostReassertInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Background color of the enforcement surface (#101820).
    /// </summary>
    private const uint BackgroundColor = 0x00182010;

    /// <summary>
    /// CTA accent color (#D97706).
    /// </summary>
    private const uint CtaColor = 0x00D97706;

    /// <summary>
    /// True once the process DPI-awareness context has been negotiated.
    /// </summary>
    private static int dpiAwarenessAttempted;

    // ── WndProc delegate ──────────────────────────────────────────────

    /// <summary>
    /// WndProc signature.
    /// </summary>
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Rooted WndProc delegate for this instance.
    /// </summary>
    private readonly WndProcDelegate wndProcDelegate;
    private readonly WndProcDelegate ctaWndProcDelegate;
    private IntPtr originalCtaWndProc;

    /// <summary>
    /// Unique window class name.
    /// </summary>
    private readonly string windowClassName;

    /// <summary>
    /// Dedicated STA message pump for this surface.
    /// </summary>
    private readonly OverlayMessagePump pump;

    /// <summary>
    /// Optional bounded geometry (used by tests so no real desktop is covered).
    /// </summary>
    private readonly (int X, int Y, int Width, int Height)? controlledBounds;

    /// <summary>
    /// Whether the enforcement surface owns the cursor while visible.
    /// </summary>
    private readonly bool hideCursor;

    /// <summary>
    /// Seam over ShowCursor (delegates to the real API by default).
    /// </summary>
    private readonly Func<bool, bool> showCursor;

    /// <summary>
    /// Optional behavior override for Apply (type-driven tests).
    /// </summary>
    private readonly Func<OverlayIntent, NativeActionOutcome>? applyOverride;

    /// <summary>
    /// Topmost re-assert cadence.
    /// </summary>
    private readonly TimeSpan topmostReassertInterval;

    /// <summary>
    /// State lock shared between the pump thread and callers.
    /// </summary>
    private readonly object stateLock = new();

    // ── State (owned by the pump thread; read under the state lock) ────

    private IntPtr hwnd;
    private IntPtr ctaHwnd;
    private bool classRegistered;
    private bool isVisible;
    private bool isDisposed;
    private string currentReason = string.Empty;
    private string? currentCtaLabel;
    private Action? onCtaClicked;
    private uint dpi = 96;
    private bool isCtaFocused;
    private int cursorHideRefs;
    private int paintCount;
    private int topmostReassertCount;
    private DateTimeOffset lastTopmostReassertUtc = DateTimeOffset.MinValue;
    private bool isTopmostTimerActive;

    // ── Constructors ───────────────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayWindow"/> class.
    /// </summary>
    public OverlayWindow()
        : this(hideCursor: true, showCursor: null, controlledBounds: null, topmostReassertInterval: null, applyOverride: null)
    {
    }

    internal OverlayWindow(int x, int y, int width, int height, bool hideCursor)
        : this(hideCursor, null, (x, y, width, height), null, null)
    {
    }

    internal OverlayWindow(
        int x,
        int y,
        int width,
        int height,
        bool hideCursor,
        Func<bool, bool>? showCursor,
        TimeSpan? topmostReassertInterval = null)
        : this(hideCursor, showCursor, (x, y, width, height), topmostReassertInterval, null)
    {
    }

    internal OverlayWindow(bool hideCursor, Func<bool, bool>? showCursor, TimeSpan? topmostReassertInterval = null)
        : this(hideCursor, showCursor, null, topmostReassertInterval, null)
    {
    }

    internal OverlayWindow(Func<OverlayIntent, NativeActionOutcome> applyOverride)
        : this(hideCursor: false, showCursor: null, controlledBounds: null, topmostReassertInterval: null, applyOverride)
    {
    }

    private OverlayWindow(
        bool hideCursor,
        Func<bool, bool>? showCursor,
        (int X, int Y, int Width, int Height)? controlledBounds,
        TimeSpan? topmostReassertInterval,
        Func<OverlayIntent, NativeActionOutcome>? applyOverride)
    {
        if (controlledBounds is { Width: var w, Height: var h } && (w <= 0 || h <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(controlledBounds));
        }

        this.windowClassName = $"{WindowClassNamePrefix}-{Guid.NewGuid():N}";
        this.wndProcDelegate = this.WindowProcStatic;
        this.ctaWndProcDelegate = this.CtaWindowProcStatic;
        this.pump = new OverlayMessagePump("OverlayWindow-Pump", this.ShouldSwallowMessage);
        this.hwnd = IntPtr.Zero;
        this.isVisible = false;
        this.hideCursor = hideCursor;
        this.showCursor = showCursor ?? (b => Win32Api.ShowCursor(b));
        this.controlledBounds = controlledBounds;
        this.applyOverride = applyOverride;
        this.topmostReassertInterval = topmostReassertInterval ?? DefaultTopmostReassertInterval;
        EnsureDpiAwareness();
    }

    // ── Properties ────────────────────────────────────────────────────

    /// <summary>
    /// Gets a value indicating whether the enforcement surface is logically visible.
    /// </summary>
    public bool IsVisible
    {
        get
        {
            lock (this.stateLock)
            {
                return this.isVisible;
            }
        }
    }

    /// <summary>
    /// Gets the native overlay window handle (Zero until first show).
    /// </summary>
    internal IntPtr Handle
    {
        get
        {
            lock (this.stateLock)
            {
                return this.hwnd;
            }
        }
    }

    /// <summary>
    /// Gets the native CTA child button handle (Zero when no CTA label).
    /// </summary>
    internal IntPtr CtaHandle
    {
        get
        {
            lock (this.stateLock)
            {
                return this.ctaHwnd;
            }
        }
    }

    /// <summary>
    /// Gets how many WM_PAINT operations reached this surface.
    /// </summary>
    internal int PaintCount
    {
        get
        {
            lock (this.stateLock)
            {
                return this.paintCount;
            }
        }
    }

    /// <summary>
    /// Gets how many times HWND_TOPMOST has been re-asserted.
    /// </summary>
    internal int TopmostReassertCount
    {
        get
        {
            lock (this.stateLock)
            {
                return this.topmostReassertCount;
            }
        }
    }

    /// <summary>
    /// Gets the last topmost re-assertion timestamp.
    /// </summary>
    internal DateTimeOffset LastTopmostReassertUtc
    {
        get
        {
            lock (this.stateLock)
            {
                return this.lastTopmostReassertUtc;
            }
        }
    }

    /// <summary>
    /// Gets whether keyboard focus is on the CTA button.
    /// </summary>
    internal bool IsCtaFocused
    {
        get
        {
            lock (this.stateLock)
            {
                return this.isCtaFocused;
            }
        }
    }

    /// <summary>
    /// Gets the cursor-hide ref count owned by this surface.
    /// </summary>
    internal int CursorHideRefs
    {
        get
        {
            lock (this.stateLock)
            {
                return this.cursorHideRefs;
            }
        }
    }

    /// <summary>
    /// Gets whether the topmost re-assert timer is active.
    /// </summary>
    internal bool IsTopmostTimerActive
    {
        get
        {
            lock (this.stateLock)
            {
                return this.isTopmostTimerActive;
            }
        }
    }

    /// <summary>
    /// Gets the current window DPI.
    /// </summary>
    internal uint CurrentDpi
    {
        get
        {
            lock (this.stateLock)
            {
                return this.dpi;
            }
        }
    }

    // ── Public API ────────────────────────────────────────────────────

    /// <summary>
    /// Shows the enforcement overlay with the specified reason and optional CTA.
    /// </summary>
    /// <param name="reason">The reason for the block.</param>
    /// <param name="ctaLabel">Optional accessible CTA button label.</param>
    /// <param name="onCtaClicked">Callback when the CTA is activated.</param>
    public void Show(string reason, string? ctaLabel = null, Action? onCtaClicked = null)
    {
        lock (this.stateLock)
        {
            if (this.isDisposed)
            {
                return;
            }
        }

        try
        {
            this.pump.Invoke(() => this.ShowCore(reason, ctaLabel, onCtaClicked));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OverlayWindow] Show failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Hides the enforcement overlay (policy itself is unaffected).
    /// </summary>
    public void Hide()
    {
        lock (this.stateLock)
        {
            if (this.isDisposed)
            {
                return;
            }
        }

        try
        {
            this.pump.Invoke(this.HideCore);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OverlayWindow] Hide failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies a typed overlay intent and returns a typed native outcome.
    /// </summary>
    /// <param name="intent">The desired overlay intent.</param>
    /// <returns>The native action outcome.</returns>
    internal NativeActionOutcome Apply(OverlayIntent intent)
    {
        lock (this.stateLock)
        {
            if (this.isDisposed)
            {
                return new(ActionStatus.InvalidState, null);
            }
        }

        if (this.applyOverride != null)
        {
            return this.applyOverride(intent);
        }

        try
        {
            if (!intent.Desired && !this.IsVisible)
            {
                return new(ActionStatus.HarmlessAbsence, null);
            }

            this.pump.Invoke(() =>
            {
                if (intent.Desired)
                {
                    this.ShowCore(intent.Reason, intent.CtaLabel, null);
                }
                else
                {
                    this.HideCore();
                }
            });

            return new(
                intent.Desired
                    ? (this.hwnd != IntPtr.Zero && this.isVisible ? ActionStatus.Confirmed : ActionStatus.NativeFailure)
                    : (this.isVisible ? ActionStatus.NativeFailure : ActionStatus.Confirmed),
                this.hwnd == IntPtr.Zero ? Marshal.GetLastWin32Error() : null);
        }
        catch (UnauthorizedAccessException)
        {
            return new(ActionStatus.AccessDenied, Marshal.GetLastWin32Error());
        }
        catch
        {
            return new(ActionStatus.NativeFailure, Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Gets whether the native window handle has been created.
    /// </summary>
    internal bool IsWindowCreated()
    {
        lock (this.stateLock)
        {
            return this.hwnd != IntPtr.Zero;
        }
    }

    /// <summary>
    /// Gets the current reason text.
    /// </summary>
    internal string GetCurrentReason()
    {
        lock (this.stateLock)
        {
            return this.currentReason;
        }
    }

    /// <summary>
    /// Gets the current CTA label.
    /// </summary>
    internal string? GetCurrentCtaLabel()
    {
        lock (this.stateLock)
        {
            return this.currentCtaLabel;
        }
    }

    /// <summary>
    /// Returns the exact visual and hit-test geometry for the current content.
    /// </summary>
    internal OverlayRenderPlan GetRenderPlan()
    {
        lock (this.stateLock)
        {
            var currentDpi = this.dpi;
            RECT bounds;
            if (this.controlledBounds is { } controlled)
            {
                bounds = new RECT
                {
                    Left = controlled.X,
                    Top = controlled.Y,
                    Right = controlled.X + controlled.Width,
                    Bottom = controlled.Y + controlled.Height,
                };
            }
            else if (this.hwnd != IntPtr.Zero && Win32Api.GetWindowRect(this.hwnd, out var actual))
            {
                bounds = actual;
                currentDpi = Win32Api.GetDpiForWindow(this.hwnd);
            }
            else
            {
                bounds = this.GetVirtualScreenBounds();
            }

            return BuildRenderPlan(bounds, this.currentReason, this.currentCtaLabel, currentDpi);
        }
    }

    /// <summary>
    /// Builds the deterministic render plan, scaling geometry with the real DPI.
    /// </summary>
    /// <param name="bounds">Window bounds.</param>
    /// <param name="reason">Blocking reason text.</param>
    /// <param name="ctaLabel">Optional CTA label.</param>
    /// <param name="dpi">Window DPI (scales all geometry).</param>
    /// <returns>The render plan used by painting and hit tests.</returns>
    internal static OverlayRenderPlan BuildRenderPlan(RECT bounds, string reason, string? ctaLabel, uint dpi)
    {
        var width = Math.Max(1, bounds.Width);
        var height = Math.Max(1, bounds.Height);
        var scale = Math.Max(1, dpi) / 96d;
        var margin = Math.Max(1, (int)Math.Round(24 * scale));
        var buttonWidth = Math.Min(Math.Max((int)Math.Round(220 * scale), width / 3), Math.Max(1, width - (2 * margin)));
        var buttonHeight = Math.Min(Math.Max(1, (int)Math.Round(56 * scale)), Math.Max(1, height / 5));
        var left = bounds.Left + ((width - buttonWidth) / 2);
        var top = Math.Max(bounds.Top, bounds.Bottom - buttonHeight - Math.Max(1, (int)Math.Round(24 * scale)));
        top = Math.Min(top, bounds.Bottom - buttonHeight);
        return new OverlayRenderPlan(
            "#101820",
            reason ?? string.Empty,
            ctaLabel,
            bounds,
            new RECT { Left = left, Top = top, Right = left + buttonWidth, Bottom = top + buttonHeight },
            Math.Max(96, dpi),
            ctaLabel is not null && ctaLabel.Length > 0);
    }

    /// <summary>
    /// Determines whether a key message should be swallowed by the overlay pump.
    /// </summary>
    internal static bool ShouldBlockKeyMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        // Block Alt+Tab (VK_TAB with Alt modifier)
        if (msg == Win32Api.WM_SYSKEYDOWN && wParam.ToInt32() == 0x09) // VK_TAB
        {
            return true;
        }

        // Block Alt+Esc
        if (msg == Win32Api.WM_SYSKEYDOWN && wParam.ToInt32() == 0x1B) // VK_ESCAPE
        {
            return true;
        }

        // Block Windows key (VK_LWIN = 0x5B, VK_RWIN = 0x5C)
        if (msg == Win32Api.WM_KEYDOWN || msg == Win32Api.WM_SYSKEYDOWN)
        {
            var vk = wParam.ToInt32();
            if (vk == 0x5B || vk == 0x5C) // VK_LWIN or VK_RWIN
            {
                return true;
            }
        }

        // Block Ctrl+Esc (Start menu)
        if (msg == Win32Api.WM_KEYDOWN && wParam.ToInt32() == 0x1B) // VK_ESCAPE
        {
            var controlState = (int)lParam & 0x20000000;
            if (controlState != 0)
            {
                return true;
            }
        }

        // Block F1 (help) and other system keys
        if (msg == Win32Api.WM_SYSKEYDOWN && wParam.ToInt32() == 0x70) // VK_F1
        {
            return true;
        }

        return false;
    }

    // ── Pump helpers ──────────────────────────────────────────────────

    private bool ShouldSwallowMessage(MSG msg)
    {
        if (msg.hWnd != IntPtr.Zero)
        {
            IntPtr surfaceHandle;
            lock (this.stateLock)
            {
                surfaceHandle = this.hwnd;
            }

            if (msg.hWnd != surfaceHandle && msg.hWnd != this.ctaHwnd)
            {
                return false;
            }
        }

        return ShouldBlockKeyMessage(msg.message, msg.wParam, msg.lParam);
    }

    private IntPtr WindowProcStatic(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        => this.WindowProc(hWnd, msg, wParam, lParam);

    private IntPtr CtaWindowProcStatic(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == Win32Api.WM_KEYUP && wParam.ToInt32() == 0x0D)
        {
            Action? clicked;
            lock (this.stateLock)
            {
                clicked = this.onCtaClicked;
            }

            clicked?.Invoke();
            return IntPtr.Zero;
        }

        return Win32Api.CallWindowProc(this.originalCtaWndProc, hWnd, msg, wParam, lParam);
    }

    // ── Core show/hide ────────────────────────────────────────────────

    private void ShowCore(string reason, string? ctaLabel, Action? onCtaClicked)
    {
        bool acquireCursor;
        lock (this.stateLock)
        {
            if (this.isDisposed)
            {
                return;
            }

            acquireCursor = !this.isVisible;
            this.currentReason = reason ?? string.Empty;
            this.currentCtaLabel = ctaLabel;
            this.onCtaClicked = onCtaClicked;
            this.isVisible = true;
        }

        this.EnsureWindowCreated();
        this.PositionForBounds();
        this.PositionOrDestroyCta();
        this.ShowAndActivate();
        this.ReassertTopmost();
        this.StartTopmostTimer();
        if (acquireCursor)
        {
            this.AcquireCursor();
        }
        _ = Win32Api.InvalidateRect(this.hwnd, IntPtr.Zero, false);

        Debug.WriteLine($"[OverlayWindow] Showing overlay: {reason}");
    }

    private void HideCore()
    {
        lock (this.stateLock)
        {
            if (!this.isVisible)
            {
                return;
            }

            this.isVisible = false;
            this.onCtaClicked = null;
            this.isCtaFocused = false;
        }

        this.StopTopmostTimer();

        if (this.hwnd != IntPtr.Zero)
        {
            _ = Win32Api.SetWindowPos(
                this.hwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                Win32Api.SWP_NOSIZE | Win32Api.SWP_NOMOVE | Win32Api.SWP_NOZORDER |
                Win32Api.SWP_NOACTIVATE | Win32Api.SWP_HIDEWINDOW);
        }

        this.ReleaseCursor();
        Debug.WriteLine("[OverlayWindow] Hiding overlay.");
    }

    private void EnsureWindowCreated()
    {
        if (this.hwnd != IntPtr.Zero)
        {
            return;
        }

        this.RegisterWindowClass();

        this.hwnd = Win32Api.CreateWindowEx(
            Win32Api.WS_EX_TOPMOST | Win32Api.WS_EX_TOOLWINDOW,
            this.windowClassName,
            WindowTitle,
            Win32Api.WS_POPUP | Win32Api.WS_VISIBLE,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (this.hwnd == IntPtr.Zero)
        {
            Debug.WriteLine($"[OverlayWindow] Failed to create window. Error: {Marshal.GetLastWin32Error()}");
            return;
        }

        this.dpi = Math.Max(96, Win32Api.GetDpiForWindow(this.hwnd));
    }

    private void RegisterWindowClass()
    {
        if (this.classRegistered)
        {
            return;
        }

        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(this.wndProcDelegate),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = IntPtr.Zero,
            hIcon = IntPtr.Zero,
            hCursor = Win32Api.LoadCursor(IntPtr.Zero, new IntPtr(Win32Api.IDC_ARROW)),
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = this.windowClassName,
            hIconSm = IntPtr.Zero,
        };

        if (Win32Api.RegisterClassEx(ref wc) == 0)
        {
            Debug.WriteLine($"[OverlayWindow] Failed to register window class. Error: {Marshal.GetLastWin32Error()}");
        }
        else
        {
            this.classRegistered = true;
        }
    }

    private void PositionForBounds()
    {
        if (this.hwnd == IntPtr.Zero)
        {
            return;
        }

        var bounds = this.controlledBounds is { } c
            ? new RECT { Left = c.X, Top = c.Y, Right = c.X + c.Width, Bottom = c.Y + c.Height }
            : this.GetVirtualScreenBounds();

        // HWND_TOPMOST with no SWP_NOZORDER so the insert-after is really honored.
        _ = Win32Api.SetWindowPos(
            this.hwnd,
            new IntPtr(-1),
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            Win32Api.SWP_SHOWWINDOW);
    }

    private RECT GetVirtualScreenBounds()
    {
        var x = Win32Api.GetSystemMetrics(Win32Api.SM_XVIRTUALSCREEN);
        var y = Win32Api.GetSystemMetrics(Win32Api.SM_YVIRTUALSCREEN);
        var width = Win32Api.GetSystemMetrics(Win32Api.SM_CXVIRTUALSCREEN);
        var height = Win32Api.GetSystemMetrics(Win32Api.SM_CYVIRTUALSCREEN);

        // Fallback to primary monitor if virtual screen metrics are unavailable.
        if (width <= 0 || height <= 0)
        {
            x = 0;
            y = 0;
            width = Win32Api.GetSystemMetrics(Win32Api.SM_CXSCREEN);
            height = Win32Api.GetSystemMetrics(Win32Api.SM_CYSCREEN);
        }

        return new RECT { Left = x, Top = y, Right = x + width, Bottom = y + height };
    }

    private void PositionOrDestroyCta()
    {
        var label = this.currentCtaLabel;
        if (string.IsNullOrEmpty(label) || this.hwnd == IntPtr.Zero)
        {
            if (this.ctaHwnd != IntPtr.Zero)
            {
                _ = Win32Api.DestroyWindow(this.ctaHwnd);
                this.ctaHwnd = IntPtr.Zero;
            }

            return;
        }

        var plan = this.GetRenderPlan();
        var cta = plan.CtaBounds;

        if (this.ctaHwnd == IntPtr.Zero || !CtaButtonHasText(this.ctaHwnd, label))
        {
            if (this.ctaHwnd != IntPtr.Zero)
            {
                _ = Win32Api.DestroyWindow(this.ctaHwnd);
            }

            this.ctaHwnd = Win32Api.CreateWindowEx(
                0,
                "BUTTON",
                label,
                Win32Api.WS_CHILD | Win32Api.WS_VISIBLE | Win32Api.WS_TABSTOP | Win32Api.BS_DEFPUSHBUTTON,
                cta.Left,
                cta.Top,
                cta.Width,
                cta.Height,
                this.hwnd,
                new IntPtr(CtaButtonId),
                IntPtr.Zero,
                IntPtr.Zero);
            if (this.ctaHwnd != IntPtr.Zero)
            {
                this.originalCtaWndProc = Win32Api.SetWindowLongPtr(
                    this.ctaHwnd,
                    Win32Api.GWLP_WNDPROC,
                    Marshal.GetFunctionPointerForDelegate(this.ctaWndProcDelegate));
            }
            return;
        }

        _ = Win32Api.SetWindowPos(
            this.ctaHwnd,
            IntPtr.Zero,
            cta.Left,
            cta.Top,
            cta.Width,
            cta.Height,
            Win32Api.SWP_NOZORDER | Win32Api.SWP_NOACTIVATE);
    }

    private static bool CtaButtonHasText(IntPtr button, string expected)
    {
        var sb = new StringBuilder(256);
        var length = Win32Api.GetWindowText(button, sb, sb.Capacity);
        return length == expected.Length && string.Equals(sb.ToString(0, Math.Max(0, length)), expected, StringComparison.Ordinal);
    }

    private void ShowAndActivate()
    {
        if (this.hwnd == IntPtr.Zero)
        {
            return;
        }

        // The enforcement surface activates and routes keyboard focus to the CTA.
        _ = Win32Api.SetWindowPos(
            this.hwnd,
            new IntPtr(-1),
            0,
            0,
            0,
            0,
            Win32Api.SWP_NOSIZE | Win32Api.SWP_NOMOVE | Win32Api.SWP_SHOWWINDOW);
        _ = Win32Api.SetActiveWindow(this.hwnd);

        lock (this.stateLock)
        {
            this.isCtaFocused = false;
        }

        if (this.ctaHwnd != IntPtr.Zero)
        {
            _ = Win32Api.SetFocus(this.ctaHwnd);
            lock (this.stateLock)
            {
                this.isCtaFocused = Win32Api.GetFocus() == this.ctaHwnd;
            }
        }
        else
        {
            _ = Win32Api.SetFocus(this.hwnd);
        }
    }

    private void ReassertTopmost()
    {
        if (this.hwnd == IntPtr.Zero)
        {
            return;
        }

        lock (this.stateLock)
        {
            if (!this.isVisible)
            {
                return;
            }
        }

        // No SWP_NOZORDER: HWND_TOPMOST (hWndInsertAfter == -1) is honored.
        _ = Win32Api.SetWindowPos(
            this.hwnd,
            new IntPtr(-1),
            0,
            0,
            0,
            0,
            Win32Api.SWP_NOSIZE | Win32Api.SWP_NOMOVE | Win32Api.SWP_NOACTIVATE);

        lock (this.stateLock)
        {
            this.topmostReassertCount++;
            this.lastTopmostReassertUtc = DateTimeOffset.UtcNow;
        }
    }

    private void StartTopmostTimer()
    {
        if (this.hwnd == IntPtr.Zero)
        {
            return;
        }

        var intervalMs = (uint)Math.Max(50, this.topmostReassertInterval.TotalMilliseconds);
        if (Win32Api.SetTimer(this.hwnd, (UIntPtr)TopmostReassertTimerId, intervalMs, IntPtr.Zero) != UIntPtr.Zero)
        {
            lock (this.stateLock)
            {
                this.isTopmostTimerActive = true;
            }
        }
    }

    private void StopTopmostTimer()
    {
        if (this.hwnd != IntPtr.Zero)
        {
            _ = Win32Api.KillTimer(this.hwnd, (UIntPtr)TopmostReassertTimerId);
        }

        lock (this.stateLock)
        {
            this.isTopmostTimerActive = false;
        }
    }

    private void AcquireCursor()
    {
        if (!this.hideCursor)
        {
            return;
        }

        lock (this.stateLock)
        {
            this.cursorHideRefs++;
            if (this.cursorHideRefs == 1)
            {
                _ = this.showCursor(false);
            }
        }
    }

    private void ReleaseCursor()
    {
        if (!this.hideCursor)
        {
            return;
        }

        lock (this.stateLock)
        {
            if (this.cursorHideRefs > 0)
            {
                this.cursorHideRefs--;
                if (this.cursorHideRefs == 0)
                {
                    _ = this.showCursor(true);
                }
            }
        }
    }

    private void ReleaseAllCursor()
    {
        if (!this.hideCursor)
        {
            return;
        }

        lock (this.stateLock)
        {
            var refs = this.cursorHideRefs;
            this.cursorHideRefs = 0;
            for (var i = 0; i < refs; i++)
            {
                _ = this.showCursor(true);
            }
        }
    }

    // ── Window procedure / painting ───────────────────────────────────

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32Api.WM_DESTROY:
                lock (this.stateLock)
                {
                    this.hwnd = IntPtr.Zero;
                    this.ctaHwnd = IntPtr.Zero;
                }

                return IntPtr.Zero;

            case Win32Api.WM_PAINT:
                this.PaintContent(hWnd);
                return IntPtr.Zero;

            case Win32Api.WM_ERASEBKGND:
                return new IntPtr(1);

            case Win32Api.WM_DISPLAYCHANGE:
                lock (this.stateLock)
                {
                    if (!this.isVisible)
                    {
                        break;
                    }
                }

                this.PositionForBounds();
                this.PositionOrDestroyCta();
                this.ReassertTopmost();
                _ = Win32Api.InvalidateRect(this.hwnd, IntPtr.Zero, false);
                return IntPtr.Zero;

            case Win32Api.WM_DPICHANGED:
                this.HandleDpiChanged(wParam, lParam);
                return IntPtr.Zero;

            case Win32Api.WM_SETFOCUS:
                lock (this.stateLock)
                {
                    if (this.ctaHwnd != IntPtr.Zero)
                    {
                        _ = Win32Api.SetFocus(this.ctaHwnd);
                    }
                }

                return IntPtr.Zero;

            case Win32Api.WM_KILLFOCUS:
                lock (this.stateLock)
                {
                    this.isCtaFocused = false;
                }

                return IntPtr.Zero;

            case Win32Api.WM_TIMER:
                if ((uint)wParam == TopmostReassertTimerId)
                {
                    this.ReassertTopmost();
                    return IntPtr.Zero;
                }

                break;

            case Win32Api.WM_COMMAND:
                if ((uint)(wParam.ToInt64() & 0xFFFF) == CtaButtonId &&
                    (uint)((wParam.ToInt64() >> 16) & 0xFFFF) == Win32Api.BN_CLICKED)
                {
                    Action? clicked;
                    lock (this.stateLock)
                    {
                        clicked = this.onCtaClicked;
                    }

                    clicked?.Invoke();
                    return IntPtr.Zero;
                }

                break;
        }

        return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void HandleDpiChanged(IntPtr wParam, IntPtr lParam)
    {
        var newDpi = (uint)(wParam.ToInt64() >> 16);
        newDpi = Math.Max(96, newDpi);
        _ = lParam; // The block surface covers the whole virtual screen; geometry below rescales.

        lock (this.stateLock)
        {
            this.dpi = newDpi;
            if (!this.isVisible || this.hwnd == IntPtr.Zero)
            {
                return;
            }
        }

        this.PositionForBounds();
        this.PositionOrDestroyCta();
        this.ReassertTopmost();
        _ = Win32Api.InvalidateRect(this.hwnd, IntPtr.Zero, false);
    }

    private void PaintContent(IntPtr hWnd)
    {
        var hdc = Win32Api.BeginPaint(hWnd, out var paint);
        if (hdc == IntPtr.Zero)
        {
            return;
        }

        try
        {
            var plan = this.GetRenderPlan();
            var background = Win32Api.CreateSolidBrush(BackgroundColor);
            try
            {
                var bounds = plan.Bounds;
                _ = Win32Api.FillRect(hdc, ref bounds, background);
                _ = Win32Api.SetBkMode(hdc, Win32Api.TRANSPARENT);
                _ = Win32Api.SetTextColor(hdc, 0x00FFFFFF);

                var scale = Math.Max(1, plan.Dpi) / 96d;
                var reason = plan.Bounds;
                reason.Top = Math.Min(reason.Bottom - 1, reason.Top + Math.Max((int)Math.Round(24 * scale), reason.Height / 3));
                reason.Bottom = Math.Max(reason.Top + 1, plan.CtaBounds.Top - (int)Math.Round(16 * scale));
                _ = Win32Api.DrawText(
                    hdc,
                    plan.Reason,
                    -1,
                    ref reason,
                    Win32Api.DT_CENTER | Win32Api.DT_VCENTER | Win32Api.DT_WORDBREAK | Win32Api.DT_NOPREFIX);

                _ = CtaColor;
            }
            finally
            {
                _ = Win32Api.DeleteObject(background);
            }
        }
        finally
        {
            _ = Win32Api.EndPaint(hWnd, ref paint);
        }

        lock (this.stateLock)
        {
            this.paintCount++;
        }
    }

    private static void EnsureDpiAwareness()
    {
        if (Interlocked.Exchange(ref dpiAwarenessAttempted, 1) == 1)
        {
            return;
        }

        try
        {
            _ = Win32Api.SetProcessDpiAwarenessContext(Win32Api.DpiAwarenessContextPerMonitorAwareV2);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OverlayWindow] DPI awareness unavailable: {ex.Message}");
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────

    /// <summary>
    /// Disposes of the overlay window, restoring the cursor and stopping the pump.
    /// </summary>
    public void Dispose()
    {
        bool already;
        lock (this.stateLock)
        {
            already = this.isDisposed;
            this.isDisposed = true;
            this.isVisible = false;
        }

        if (already)
        {
            return;
        }

        try
        {
            this.pump.Invoke(() =>
            {
                this.StopTopmostTimer();
                if (this.ctaHwnd != IntPtr.Zero)
                {
                    _ = Win32Api.DestroyWindow(this.ctaHwnd);
                    this.ctaHwnd = IntPtr.Zero;
                }

                if (this.hwnd != IntPtr.Zero)
                {
                    _ = Win32Api.DestroyWindow(this.hwnd);
                    this.hwnd = IntPtr.Zero;
                    this.classRegistered = false;
                }

                this.ReleaseAllCursor();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OverlayWindow] Dispose cleanup failed: {ex.Message}");
        }

        this.pump.Dispose();
        GC.SuppressFinalize(this);
    }

    // ══════════════════════════════════════════════════════════════════
    //  Dedicated STA message pump — real GetMessageW/DispatchMessageW loop.
    // ══════════════════════════════════════════════════════════════════

    private sealed class OverlayMessagePump : IDisposable
    {
        private const uint WakeMessage = Win32Api.WM_USER + 0x0100;

        private readonly ConcurrentQueue<Action> queue = new();
        private readonly Func<MSG, bool>? shouldSwallow;
        private readonly Thread thread;
        private readonly ManualResetEventSlim queueReady = new(false);
        private volatile uint nativeThreadId;
        private volatile bool running = true;

        internal OverlayMessagePump(string threadName, Func<MSG, bool>? shouldSwallow)
        {
            this.shouldSwallow = shouldSwallow;
            this.thread = new Thread(this.Loop)
            {
                Name = threadName,
                IsBackground = true,
            };
            this.thread.SetApartmentState(ApartmentState.STA);
            this.thread.Start();
            if (!this.queueReady.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new InvalidOperationException("Overlay message pump did not initialize its message queue.");
            }
        }

        internal void Invoke(Action action)
        {
            if (Environment.CurrentManagedThreadId == this.thread.ManagedThreadId)
            {
                action();
                return;
            }

            using var finished = new ManualResetEventSlim(false);
            this.queue.Enqueue(() =>
            {
                try
                {
                    action();
                }
                finally
                {
                    finished.Set();
                }
            });
            Win32Api.PostThreadMessage(this.nativeThreadId, WakeMessage, IntPtr.Zero, IntPtr.Zero);
            if (!finished.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new InvalidOperationException("Overlay message pump did not process the operation in time.");
            }
        }

        public void Dispose()
        {
            this.Stop();
        }

        private void Stop()
        {
            this.running = false;
            Win32Api.PostThreadMessage(this.nativeThreadId, Win32Api.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            if (this.thread.IsAlive)
            {
                _ = this.thread.Join(TimeSpan.FromSeconds(3));
            }
        }

        private void Loop()
        {
            try
            {
                // A thread message queue is created only when the thread first
                // calls a queue API. Initialize it before callers can post work.
                this.nativeThreadId = Win32Api.GetCurrentThreadId();
                _ = Win32Api.PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
                this.queueReady.Set();

                while (this.running)
                {
                    if (Win32Api.GetMessageW(out var msg, hWnd: IntPtr.Zero, 0, 0) <= 0)
                    {
                        break;
                    }

                    if (this.shouldSwallow != null && this.shouldSwallow(msg))
                    {
                        continue;
                    }

                    Win32Api.TranslateMessage(ref msg);
                    Win32Api.DispatchMessageW(ref msg);
                    this.Drain();
                }
            }
            finally
            {
                this.Drain();
            }
        }

        private void Drain()
        {
            while (this.queue.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OverlayMessagePump] Work item failed: {ex.Message}");
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Independent warning toast surface (W-06).
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Non-blocking, independently-owned warning toast. Never touches the
    /// enforcement surface, the cursor, focus or policy state; auto-closes
    /// after its configured duration and replaces any previous toast when shown
    /// again. Lives on its own STA message pump so its own paint and timer
    /// messages really dispatch.
    /// </summary>
    internal sealed class WarningToastWindow : IDisposable
    {
        private const string ToastClassNamePrefix = "ControlParentalWarningToastClass";
        private const string ToastTitle = "ControlParental Aviso";
        private const int LogicalWidth = 360;
        private const int LogicalHeight = 100;
        private const int Margin = 24;
        private const uint ToastTimerId = 0x4D10;
        private const uint ToastBackgroundColor = 0x00182010;

        private readonly WndProcDelegate wndProcDelegate;
        private readonly string windowClassName;
        private readonly OverlayMessagePump pump;
        private readonly object stateLock = new();

        private IntPtr hwnd;
        private bool classRegistered;
        private bool isVisible;
        private bool isDisposed;
        private string? message;
        private Action? onAutoClosed;
        private TimeSpan duration;
        private uint dpi = 96;
        private int paintCount;

        internal WarningToastWindow()
        {
            this.windowClassName = $"{ToastClassNamePrefix}-{Guid.NewGuid():N}";
            this.wndProcDelegate = this.WindowProcStatic;
            this.pump = new OverlayMessagePump("OverlayWarningToast-Pump", null);
            this.hwnd = IntPtr.Zero;
            EnsureDpiAwareness();
        }

        internal bool IsVisible
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.isVisible;
                }
            }
        }

        internal string? Message
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.message;
                }
            }
        }

        internal IntPtr Handle
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.hwnd;
                }
            }
        }

        internal int PaintCount
        {
            get
            {
                lock (this.stateLock)
                {
                    return this.paintCount;
                }
            }
        }

        internal void Show(string text, TimeSpan displayDuration, Action? onAutoClosed)
        {
            lock (this.stateLock)
            {
                if (this.isDisposed)
                {
                    return;
                }
            }

            try
            {
                this.pump.Invoke(() => this.ShowCore(text, displayDuration, onAutoClosed));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WarningToastWindow] Show failed: {ex.Message}");
            }
        }

        internal void Hide()
        {
            lock (this.stateLock)
            {
                if (this.isDisposed)
                {
                    return;
                }
            }

            try
            {
                this.pump.Invoke(this.HideCore);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WarningToastWindow] Hide failed: {ex.Message}");
            }
        }

        internal (int Width, int Height, uint Dpi) GetGeometry()
        {
            lock (this.stateLock)
            {
                if (this.hwnd != IntPtr.Zero && Win32Api.GetWindowRect(this.hwnd, out var rect))
                {
                    return (rect.Width, rect.Height, Win32Api.GetDpiForWindow(this.hwnd));
                }

                return (0, 0, this.dpi);
            }
        }

        private void ShowCore(string text, TimeSpan displayDuration, Action? onAutoClosed)
        {
            lock (this.stateLock)
            {
                this.message = text ?? string.Empty;
                this.duration = displayDuration;
                this.onAutoClosed = onAutoClosed;
                this.isVisible = true;
            }

            this.EnsureWindowCreated();
            this.PositionToast();

            if (this.hwnd == IntPtr.Zero)
            {
                return;
            }

            _ = Win32Api.SetWindowPos(
                this.hwnd,
                new IntPtr(-1),
                0,
                0,
                0,
                0,
                Win32Api.SWP_NOSIZE | Win32Api.SWP_NOMOVE | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);
            var ms = (uint)Math.Max(20, Math.Min(int.MaxValue, (long)this.duration.TotalMilliseconds));
            _ = Win32Api.SetTimer(this.hwnd, (UIntPtr)ToastTimerId, ms, IntPtr.Zero);
            _ = Win32Api.InvalidateRect(this.hwnd, IntPtr.Zero, false);
        }

        private void HideCore()
        {
            lock (this.stateLock)
            {
                if (!this.isVisible)
                {
                    return;
                }

                this.isVisible = false;
            }

            this.ClearAutoClose();

            if (this.hwnd != IntPtr.Zero)
            {
                _ = Win32Api.SetWindowPos(
                    this.hwnd,
                    IntPtr.Zero,
                    0,
                    0,
                    0,
                    0,
                    Win32Api.SWP_NOSIZE | Win32Api.SWP_NOMOVE | Win32Api.SWP_NOZORDER |
                    Win32Api.SWP_NOACTIVATE | Win32Api.SWP_HIDEWINDOW);
            }
        }

        private void AutoClose()
        {
            Action? callback;
            lock (this.stateLock)
            {
                if (!this.isVisible)
                {
                    return;
                }

                this.isVisible = false;
                callback = this.onAutoClosed;
            }

            this.ClearAutoClose();

            if (this.hwnd != IntPtr.Zero)
            {
                _ = Win32Api.SetWindowPos(
                    this.hwnd,
                    IntPtr.Zero,
                    0,
                    0,
                    0,
                    0,
                    Win32Api.SWP_NOSIZE | Win32Api.SWP_NOMOVE | Win32Api.SWP_NOZORDER |
                    Win32Api.SWP_NOACTIVATE | Win32Api.SWP_HIDEWINDOW);
            }

            callback?.Invoke();
        }

        private void ClearAutoClose()
        {
            if (this.hwnd != IntPtr.Zero)
            {
                _ = Win32Api.KillTimer(this.hwnd, (UIntPtr)ToastTimerId);
            }
        }

        private void EnsureWindowCreated()
        {
            if (this.hwnd != IntPtr.Zero)
            {
                return;
            }

            RegisterToastClass();

            this.hwnd = Win32Api.CreateWindowEx(
                Win32Api.WS_EX_TOPMOST | Win32Api.WS_EX_TOOLWINDOW | Win32Api.WS_EX_NOACTIVATE,
                this.windowClassName,
                ToastTitle,
                Win32Api.WS_POPUP | Win32Api.WS_VISIBLE,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (this.hwnd == IntPtr.Zero)
            {
                Debug.WriteLine($"[WarningToastWindow] Failed to create window. Error: {Marshal.GetLastWin32Error()}");
                return;
            }

            this.dpi = Math.Max(96, Win32Api.GetDpiForWindow(this.hwnd));
        }

        private void RegisterToastClass()
        {
            if (this.classRegistered)
            {
                return;
            }

            var wc = new WNDCLASSEX
            {
                cbSize = Marshal.SizeOf<WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(this.wndProcDelegate),
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = IntPtr.Zero,
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = this.windowClassName,
                hIconSm = IntPtr.Zero,
            };

            if (Win32Api.RegisterClassEx(ref wc) == 0)
            {
                Debug.WriteLine($"[WarningToastWindow] Failed to register window class. Error: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                this.classRegistered = true;
            }
        }

        private void PositionToast()
        {
            if (this.hwnd == IntPtr.Zero)
            {
                return;
            }

            var dpi = Math.Max(96, Win32Api.GetDpiForWindow(this.hwnd));
            var scale = dpi / 96d;
            var width = (int)Math.Round(LogicalWidth * scale);
            var height = (int)Math.Round(LogicalHeight * scale);
            var margin = (int)Math.Round(Margin * scale);
            var screenWidth = Math.Max(width + (2 * margin), Win32Api.GetSystemMetrics(Win32Api.SM_CXSCREEN));
            var x = screenWidth - width - margin;
            var y = margin;

            lock (this.stateLock)
            {
                this.dpi = dpi;
            }

            _ = Win32Api.SetWindowPos(
                this.hwnd,
                new IntPtr(-1),
                x,
                y,
                width,
                height,
                Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);
        }

        private void HandleDpiChanged(IntPtr wParam, IntPtr lParam)
        {
            var newDpi = (uint)(wParam.ToInt64() >> 16);
            newDpi = Math.Max(96, newDpi);

            lock (this.stateLock)
            {
                this.dpi = newDpi;
                if (this.hwnd == IntPtr.Zero)
                {
                    return;
                }
            }

            // Honor the suggested RECT: keep the toast inside the new monitor bounds.
            var suggested = Marshal.PtrToStructure<RECT>(lParam);
            var scale = newDpi / 96d;
            var width = Math.Min((int)Math.Round(LogicalWidth * scale), Math.Max(1, suggested.Width));
            var height = Math.Min((int)Math.Round(LogicalHeight * scale), Math.Max(1, suggested.Height));
            var margin = Math.Min((int)Math.Round(Margin * scale), Math.Max(0, suggested.Width - width));
            var x = suggested.Right - width - margin;
            var y = suggested.Top + margin;
            if (x < suggested.Left)
            {
                x = suggested.Left;
            }

            _ = Win32Api.SetWindowPos(
                this.hwnd,
                new IntPtr(-1),
                x,
                y,
                width,
                height,
                Win32Api.SWP_NOACTIVATE);
            _ = Win32Api.InvalidateRect(this.hwnd, IntPtr.Zero, false);
        }

        private IntPtr WindowProcStatic(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
            => this.WindowProc(hWnd, msg, wParam, lParam);

        private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case Win32Api.WM_DESTROY:
                    lock (this.stateLock)
                    {
                        this.hwnd = IntPtr.Zero;
                    }

                    return IntPtr.Zero;

                case Win32Api.WM_PAINT:
                    this.PaintContent(hWnd);
                    return IntPtr.Zero;

                case Win32Api.WM_ERASEBKGND:
                    return new IntPtr(1);

                case Win32Api.WM_TIMER:
                    if ((uint)wParam == ToastTimerId)
                    {
                        this.AutoClose();
                        return IntPtr.Zero;
                    }

                    break;

                case Win32Api.WM_DPICHANGED:
                    this.HandleDpiChanged(wParam, lParam);
                    return IntPtr.Zero;

                case Win32Api.WM_DISPLAYCHANGE:
                    lock (this.stateLock)
                    {
                        if (this.isVisible)
                        {
                            this.PositionToast();
                            _ = Win32Api.InvalidateRect(this.hwnd, IntPtr.Zero, false);
                        }
                    }

                    return IntPtr.Zero;
            }

            return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        private void PaintContent(IntPtr hWnd)
        {
            var hdc = Win32Api.BeginPaint(hWnd, out var paint);
            if (hdc == IntPtr.Zero)
            {
                return;
            }

            try
            {
                string text;
                lock (this.stateLock)
                {
                    text = this.message ?? string.Empty;
                }

                _ = Win32Api.GetClientRect(hWnd, out var client);
                var brush = Win32Api.CreateSolidBrush(ToastBackgroundColor);
                try
                {
                    var rect = client;
                    _ = Win32Api.FillRect(hdc, ref rect, brush);
                    _ = Win32Api.SetBkMode(hdc, Win32Api.TRANSPARENT);
                    _ = Win32Api.SetTextColor(hdc, 0x00FFFFFF);
                    var textRect = client;
                    textRect.Left += Margin / 2;
                    textRect.Right -= Margin / 2;
                    textRect.Top += Margin / 2;
                    textRect.Bottom -= Margin / 2;
                    _ = Win32Api.DrawText(
                        hdc,
                        text,
                        -1,
                        ref textRect,
                        Win32Api.DT_CENTER | Win32Api.DT_VCENTER | Win32Api.DT_WORDBREAK | Win32Api.DT_NOPREFIX);
                }
                finally
                {
                    _ = Win32Api.DeleteObject(brush);
                }
            }
            finally
            {
                _ = Win32Api.EndPaint(hWnd, ref paint);
            }

            lock (this.stateLock)
            {
                this.paintCount++;
            }
        }

        public void Dispose()
        {
            bool already;
            lock (this.stateLock)
            {
                already = this.isDisposed;
                this.isDisposed = true;
            }

            if (already)
            {
                return;
            }

            try
            {
                this.pump.Invoke(() =>
                {
                    this.ClearAutoClose();
                    if (this.hwnd != IntPtr.Zero)
                    {
                        _ = Win32Api.DestroyWindow(this.hwnd);
                        this.hwnd = IntPtr.Zero;
                        this.classRegistered = false;
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WarningToastWindow] Dispose cleanup failed: {ex.Message}");
            }

            this.pump.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Deterministic render contract.
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Deterministic visual contract used by painting and accessibility tests.
    /// </summary>
    internal sealed record OverlayRenderPlan(
        string Background,
        string Reason,
        string? CtaLabel,
        RECT Bounds,
        RECT CtaBounds,
        uint Dpi,
        bool IsKeyboardFocusable);
}
