// <copyright file="Win32Api.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.SessionAgent.Interop;

using System.Runtime.InteropServices;

/// <summary>
/// T05 — Win32 P/Invoke declarations for foreground detection and app identity resolution.
/// All Win32 APIs used by the Session Agent.
/// </summary>
internal static class Win32Api
{
    // ── Window APIs ────────────────────────────────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindow(IntPtr hWnd);

    // ── Process name resolution ─────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool QueryFullProcessImageNameW(
        IntPtr hProcess,
        uint dwFlags,
        [Out] char[] lpExeName,
        ref uint lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint GetLastError();

    // Process access flags
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // ── MSIX / Package APIs ────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetPackageFullName(
        IntPtr hProcess,
        ref uint packageFullNameLength,
        [Out] char[]? packageFullName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetPackageFamilyName(
        IntPtr hProcess,
        ref uint packageFamilyNameLength,
        [Out] char[]? packageFamilyName);

    [DllImport("api-ms-win-winrt-package-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetPackagePathByFullName(
        [MarshalAs(UnmanagedType.LPWStr)] string packageFullName,
        ref uint pathLength,
        [Out] char[]? path);

    // ── WinEvent hook ──────────────────────────────────────────────────

    /// <summary>
    /// Sets an event hook to be called when the specified event occurs.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetMessageW(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr DispatchMessageW([In] ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern void PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetMessageTimeout(
        out MSG lpMsg,
        IntPtr hWnd,
        uint wMsgFilterMin,
        uint wMsgFilterMax,
        uint timeout);

    // ── Constants ────────────────────────────────────────────────────────

    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    public const uint WINEVENT_SKIPOWNTHREAD = 0x0001;
    public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
    public const uint INFINITE = 0xFFFFFFFF;

    // Win32 error codes
    public const int ERROR_SUCCESS = 0;
    public const int ERROR_NOT_FOUND = 1168;
    public const int ERROR_INVALID_PARAMETER = 87;

    public const uint WM_QUIT = 0x0012;
    public const uint WM_USER = 0x0400;

    public const uint PM_REMOVE = 0x0001;

    // Thread/Process IDs for hook
    public const uint CURRENTPROCESS = 0;
    public const uint CURRENTTHREAD = 0;

    // ── Monitor / Multi-monitor APIs ──────────────────────────────────

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        [In] ref RECT lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    // ── Window creation / style APIs ─────────────────────────────────

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int X,
        int Y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);

    [DllImport("user32.dll")]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowCursor(bool bShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetCapture(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEX lpWndClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

    // ── Message box ──────────────────────────────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int MessageBox(
        IntPtr hWnd,
        string lpText,
        string lpCaption,
        uint uType);

    // ── Constants for overlay window ─────────────────────────────────

    public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
    public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;

    // Window styles
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_TRANSPARENT = 0x00000020;

    // Window messages
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_PAINT = 0x000F;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_SYSKEYDOWN = 0x0104;

    // SetWindowPos flags
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;

    // System parameters for input blocking
    [DllImport("user32.dll")]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    public const uint SPI_GETFASTTASKING = 0x0084;
    public const uint SPI_SETFASTTASKING = 0x0085;

    // Cursor IDs
    public const int IDC_ARROW = 32512; // Standard arrow cursor
}

/// <summary>
/// Delegate for WinEventProc callback.
/// </summary>
internal delegate void WinEventDelegate(
    IntPtr hWinEventHook,
    uint eventType,
    IntPtr hWnd,
    int idObject,
    int idChild,
    uint dwEventThread,
    uint dwmsEventTime);

/// <summary>
/// Win32 POINT structure.
/// </summary>
public struct Win32Point
{
    public int X;
    public int Y;
}

/// <summary>
/// Win32 MSG structure for message loop.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MSG
{
    public IntPtr hWnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public Win32Point pt;
}

/// <summary>
/// Delegate for monitor enumeration callback.
/// </summary>
public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

/// <summary>
/// Win32 RECT structure.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public int Width => this.Right - this.Left;
    public int Height => this.Bottom - this.Top;
}

/// <summary>
/// Win32 MONITORINFO structure.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct MONITORINFO
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}

/// <summary>
/// Win32 WNDCLASSEX structure for window class registration.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct WNDCLASSEX
{
    public int cbSize;
    public int style;
    public IntPtr lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    public string? lpszMenuName;
    public string? lpszClassName;
    public IntPtr hIconSm;
}
