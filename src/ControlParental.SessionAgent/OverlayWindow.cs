// <copyright file="OverlayWindow.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ControlParental.SessionAgent.Interop;

/// <summary>
/// T08 — Win32 overlay window that covers all monitors.
/// Displays a blocking screen with the reason and a CTA button.
/// Blocks common keyboard shortcuts (Alt+Tab, Win key, etc.).
/// Note: Ctrl+Alt+Supr (SAS) cannot be intercepted from user mode;
/// mitigated by policy in MANAGED mode (T31), not by this window.
/// </summary>
public sealed class OverlayWindow : IDisposable
{
    // ── Constants ─────────────────────────────────────────────────────

    /// <summary>
    /// Class name for the overlay window.
    /// </summary>
    private const string WindowClassName = "ControlParentalOverlayClass";

    /// <summary>
    /// Window title for the overlay.
    /// </summary>
    private const string WindowTitle = "ControlParental Overlay";

    // ── Delegate for Window Procedure ─────────────────────────────────

    /// <summary>
    /// Delegate for the WndProc callback.
    /// </summary>
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Static WndProc that delegates to the instance.
    /// </summary>
    private static WndProcDelegate? wndProcDelegate;

    // ── State ────────────────────────────────────────────────────────

    private IntPtr hwnd;
    private bool isVisible;
    private string currentReason = string.Empty;
    private string? currentCtaLabel;
    private Action? onCtaClicked;
    private bool disposed;
    private bool classRegistered;

    /// <summary>
    /// Lock object for thread safety.
    /// </summary>
    private readonly object lockObj = new();

    // ── Public Properties ────────────────────────────────────────────

    /// <inheritdoc />
    public bool IsVisible
    {
        get
        {
            lock (this.lockObj)
            {
                return this.isVisible;
            }
        }
    }

    // ── Public Methods ───────────────────────────────────────────────

    /// <summary>
    /// Initializes a new instance of the <see cref="OverlayWindow"/> class.
    /// </summary>
    public OverlayWindow()
    {
        this.hwnd = IntPtr.Zero;
        this.isVisible = false;
    }

    /// <summary>
    /// Shows the overlay with the specified reason and optional CTA.
    /// </summary>
    /// <param name="reason">The reason for the block.</param>
    /// <param name="ctaLabel">Optional label for the CTA button.</param>
    /// <param name="onCtaClicked">Callback when CTA is clicked.</param>
    public void Show(string reason, string? ctaLabel = null, Action? onCtaClicked = null)
    {
        lock (this.lockObj)
        {
            if (this.disposed)
            {
                return;
            }

            this.currentReason = reason ?? string.Empty;
            this.currentCtaLabel = ctaLabel;
            this.onCtaClicked = onCtaClicked;

            // Mark as visible immediately
            this.isVisible = true;

            // Create window if needed
            this.EnsureWindowCreated();

            // Position window to cover all monitors
            this.PositionWindowToCoverAllMonitors();

            // Show the window
            this.ShowWindow();

            Debug.WriteLine($"[OverlayWindow] Showing overlay: {reason}");
        }
    }

    /// <summary>
    /// Hides the overlay.
    /// </summary>
    public void Hide()
    {
        lock (this.lockObj)
        {
            if (this.disposed)
            {
                return;
            }

            this.HideWindow();
            this.isVisible = false;
            Debug.WriteLine("[OverlayWindow] Hiding overlay.");
        }
    }

    /// <summary>
    /// Processes a Windows message. Call this from your message pump.
    /// </summary>
    /// <param name="msg">The MSG structure.</param>
    /// <returns>True if the message was handled by the overlay.</returns>
    public bool ProcessMessage(ref MSG msg)
    {
        lock (this.lockObj)
        {
            if (this.hwnd == IntPtr.Zero || !this.isVisible)
            {
                return false;
            }

            if (msg.hWnd == this.hwnd || msg.hWnd == IntPtr.Zero)
            {
                // Block Alt+Tab, Alt+Esc, Win key, etc.
                if (OverlayWindow.ShouldBlockKeyMessage(msg.message, msg.wParam, msg.lParam))
                {
                    return true; // Message handled (blocked)
                }

                // Handle mouse clicks on the CTA button area
                if (msg.message == Win32Api.WM_LBUTTONDOWN && this.onCtaClicked != null)
                {
                    // Check if click is in the CTA button area (simplified: bottom portion of screen)
                    var yPos = (int)((uint)msg.lParam >> 16);
                    var screenHeight = Win32Api.GetSystemMetrics(Win32Api.SM_CYVIRTUALSCREEN);
                    if (yPos > screenHeight * 2 / 3)
                    {
                        Debug.WriteLine("[OverlayWindow] CTA button clicked.");
                        this.onCtaClicked.Invoke();
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a key message should be blocked.
    /// </summary>
    internal static bool ShouldBlockKeyMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        // Block Alt+Tab (VK_TAB with Alt modifier)
        if (msg == Win32Api.WM_SYSKEYDOWN && (int)wParam == 0x09) // VK_TAB
        {
            return true;
        }

        // Block Alt+Esc
        if (msg == Win32Api.WM_SYSKEYDOWN && (int)wParam == 0x1B) // VK_ESCAPE
        {
            return true;
        }

        // Block Windows key (VK_LWIN = 0x5B, VK_RWIN = 0x5C)
        if (msg == Win32Api.WM_KEYDOWN || msg == Win32Api.WM_SYSKEYDOWN)
        {
            var vk = (int)wParam;
            if (vk == 0x5B || vk == 0x5C) // VK_LWIN or VK_RWIN
            {
                return true;
            }
        }

        // Block Ctrl+Esc (Start menu)
        if (msg == Win32Api.WM_KEYDOWN && (int)wParam == 0x1B) // VK_ESCAPE
        {
            // Check for Ctrl modifier
            var controlState = (int)lParam & 0x20000000;
            if (controlState != 0)
            {
                return true;
            }
        }

        // Block F1 (help) and other system keys
        if (msg == Win32Api.WM_SYSKEYDOWN && (int)wParam == 0x70) // VK_F1
        {
            return true;
        }

        return false;
    }

    // ── Private Methods ──────────────────────────────────────────────

    private void EnsureWindowCreated()
    {
        if (this.hwnd != IntPtr.Zero)
        {
            return;
        }

        // Register window class
        this.RegisterWindowClass();

        // Create the window
        this.hwnd = Win32Api.CreateWindowEx(
            Win32Api.WS_EX_TOPMOST | Win32Api.WS_EX_TRANSPARENT,
            WindowClassName,
            WindowTitle,
            Win32Api.WS_POPUP | Win32Api.WS_VISIBLE,
            0,
            0,
            0,
            0, // Will be positioned later
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (this.hwnd == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[OverlayWindow] Failed to create window. Error: {error}");
        }
    }

    private void RegisterWindowClass()
    {
        if (this.classRegistered)
        {
            return;
        }

        // Create static delegate for window procedure
        if (wndProcDelegate == null)
        {
            wndProcDelegate = this.WindowProcStatic;
        }

        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProcDelegate),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = IntPtr.Zero,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszMenuName = null,
            lpszClassName = WindowClassName,
            hIconSm = IntPtr.Zero,
        };

        if (Win32Api.RegisterClassEx(ref wc) == 0)
        {
            var error = Marshal.GetLastWin32Error();
            Debug.WriteLine($"[OverlayWindow] Failed to register window class. Error: {error}");
        }
        else
        {
            this.classRegistered = true;
        }
    }

    /// <summary>
    /// Static window procedure that delegates to the instance method.
    /// </summary>
    private IntPtr WindowProcStatic(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        => this.WindowProc(hWnd, msg, wParam, lParam);

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32Api.WM_DESTROY:
                // Window destroyed, clear handle
                lock (this.lockObj)
                {
                    this.hwnd = IntPtr.Zero;
                }
                break;

            case Win32Api.WM_PAINT:
                this.PaintContent();
                break;

            case Win32Api.WM_ERASEBKGND:
                // Return non-zero to indicate we handled it
                return new IntPtr(1);
        }

        return Win32Api.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void PaintContent()
    {
        // Note: Real implementation would use GDI or Direct2D to draw
        // the overlay content (background, reason text, CTA button).
        // For now, this is a placeholder that prevents crashes.
        Debug.WriteLine("[OverlayWindow] Painting content...");
    }

    private void PositionWindowToCoverAllMonitors()
    {
        if (this.hwnd == IntPtr.Zero)
        {
            return;
        }

        // Get the virtual screen bounds (all monitors combined)
        var virtualX = Win32Api.GetSystemMetrics(Win32Api.SM_XVIRTUALSCREEN);
        var virtualY = Win32Api.GetSystemMetrics(Win32Api.SM_YVIRTUALSCREEN);
        var virtualWidth = Win32Api.GetSystemMetrics(Win32Api.SM_CXVIRTUALSCREEN);
        var virtualHeight = Win32Api.GetSystemMetrics(Win32Api.SM_CYVIRTUALSCREEN);

        // Fallback to primary monitor if virtual screen metrics not available
        if (virtualWidth <= 0 || virtualHeight <= 0)
        {
            virtualX = 0;
            virtualY = 0;
            virtualWidth = Win32Api.GetSystemMetrics(Win32Api.SM_CXSCREEN);
            virtualHeight = Win32Api.GetSystemMetrics(Win32Api.SM_CYSCREEN);
        }

        // Position and resize the window
        Win32Api.SetWindowPos(
            this.hwnd,
            new IntPtr(-1), // HWND_TOPMOST
            virtualX,
            virtualY,
            virtualWidth,
            virtualHeight,
            Win32Api.SWP_NOZORDER | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);

        Debug.WriteLine(
            $"[OverlayWindow] Positioned to cover virtual screen: {virtualX},{virtualY} {virtualWidth}x{virtualHeight}");
    }

    private void ShowWindow()
    {
        if (this.hwnd == IntPtr.Zero)
        {
            return;
        }

        // Re-affirm topmost and visibility
        Win32Api.SetWindowPos(
            this.hwnd,
            new IntPtr(-1), // HWND_TOPMOST
            0,
            0,
            0,
            0,
            Win32Api.SWP_NOZORDER | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_SHOWWINDOW);

        Win32Api.ShowCursor(false);
        this.isVisible = true;
    }

    private void HideWindow()
    {
        if (this.hwnd == IntPtr.Zero)
        {
            return;
        }

        Win32Api.SetWindowPos(
            this.hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            Win32Api.SWP_NOZORDER | Win32Api.SWP_NOACTIVATE | Win32Api.SWP_HIDEWINDOW);

        Win32Api.ShowCursor(true);
        this.isVisible = false;
    }

    /// <summary>
    /// Gets the current reason text.
    /// </summary>
    internal string GetCurrentReason()
    {
        lock (this.lockObj)
        {
            return this.currentReason;
        }
    }

    /// <summary>
    /// Gets the current CTA label.
    /// </summary>
    internal string? GetCurrentCtaLabel()
    {
        lock (this.lockObj)
        {
            return this.currentCtaLabel;
        }
    }

    /// <summary>
    /// Gets whether the window handle is valid.
    /// </summary>
    internal bool IsWindowCreated()
    {
        lock (this.lockObj)
        {
            return this.hwnd != IntPtr.Zero;
        }
    }

    // ── IDisposable ───────────────────────────────────────────────────

    /// <summary>
    /// Disposes of the overlay window.
    /// </summary>
    public void Dispose()
    {
        lock (this.lockObj)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            if (this.hwnd != IntPtr.Zero)
            {
                Win32Api.DestroyWindow(this.hwnd);
                this.hwnd = IntPtr.Zero;
            }

            this.isVisible = false;
        }

        GC.SuppressFinalize(this);
    }
}
