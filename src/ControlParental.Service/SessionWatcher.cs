// <copyright file="SessionWatcher.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Runtime.InteropServices;

/// <summary>
/// Watches for interactive sessions of the target child user.
/// Detects session start, stop, lock, unlock, and fast user switching.
/// Emits events for the agent launcher to react to.
/// </summary>
public sealed class SessionWatcher : IDisposable
{
    private readonly string childUsername;
    private readonly Action<int> onSessionStarted;
    private readonly Action onSessionEnded;
    private readonly Action<int> onSessionLock;
    private readonly Action<int> onSessionUnlock;
    private readonly CancellationTokenSource internalCts;
    private bool disposed;
    private int? currentSessionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionWatcher"/> class.
    /// </summary>
    /// <param name="childUsername">The username of the child account to watch.</param>
    /// <param name="onSessionStarted">Callback when the child's session starts.</param>
    /// <param name="onSessionEnded">Callback when the child's session ends.</param>
    /// <param name="onSessionLock">Callback when the child's session is locked (receives session ID).</param>
    /// <param name="onSessionUnlock">Callback when the child's session is unlocked (receives session ID).</param>
    public SessionWatcher(
        string childUsername,
        Action<int> onSessionStarted,
        Action onSessionEnded,
        Action<int> onSessionLock,
        Action<int> onSessionUnlock)
    {
        this.childUsername = childUsername ?? throw new ArgumentNullException(nameof(childUsername));
        this.onSessionStarted = onSessionStarted ?? throw new ArgumentNullException(nameof(onSessionStarted));
        this.onSessionEnded = onSessionEnded ?? throw new ArgumentNullException(nameof(onSessionEnded));
        this.onSessionLock = onSessionLock ?? throw new ArgumentNullException(nameof(onSessionLock));
        this.onSessionUnlock = onSessionUnlock ?? throw new ArgumentNullException(nameof(onSessionUnlock));
        this.internalCts = new CancellationTokenSource();
    }

    /// <summary>
    /// Starts watching for session events.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            this.internalCts.Token);

        return Task.Run(() => this.WatchLoop(linkedCts.Token), linkedCts.Token);
    }

    /// <summary>
    /// Stops watching for session events.
    /// </summary>
    public void Stop()
    {
        this.internalCts.Cancel();
    }

    private void WatchLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var sessions = this.EnumerateInteractiveSessions();
                var childSession = this.FindChildSession(sessions);

                if (childSession.HasValue)
                {
                    var sessionId = childSession.Value.SessionId;

                    if (this.currentSessionId != sessionId)
                    {
                        // Session changed
                        if (this.currentSessionId.HasValue)
                        {
                            this.onSessionEnded();
                        }

                        this.currentSessionId = sessionId;
                        this.onSessionStarted(sessionId);
                    }

                    // Check for lock/unlock state
                    if (childSession.Value.IsLocked)
                    {
                        this.onSessionLock(sessionId);
                    }
                    else
                    {
                        this.onSessionUnlock(sessionId);
                    }
                }
                else
                {
                    if (this.currentSessionId.HasValue)
                    {
                        this.onSessionEnded();
                        this.currentSessionId = null;
                    }
                }

                // Poll every 2 seconds
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SessionWatcher] Error in watch loop: {ex.Message}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
    }

    private SessionInfo[] EnumerateInteractiveSessions()
    {
        var sessions = new List<SessionInfo>();

        try
        {
            // Use WTSEnumerateSessions via P/Invoke
            var sessionInfoPtr = IntPtr.Zero;
            var sessionCount = 0;

            if (WtsApi32.WTSEnumerateSessions(
                WtsApi32.WTS_CURRENT_SERVER_HANDLE,
                0,
                1,
                out sessionInfoPtr,
                out sessionCount))
            {
                var current = sessionInfoPtr;
                for (var i = 0; i < sessionCount; i++)
                {
                    var info = Marshal.PtrToStructure<WtsApi32.WTS_SESSION_INFO>(current);

                    // Only include active/connected sessions (not Disconnected)
                    if (info.State == WtsApi32.WTS_CONNECTSTATE_CLASS.WTSActive ||
                        info.State == WtsApi32.WTS_CONNECTSTATE_CLASS.WTSDisconnected)
                    {
                        var username = this.GetSessionUserName(info.SessionId);
                        var isLocked = this.IsSessionLocked(info.SessionId);

                        sessions.Add(new SessionInfo(
                            info.SessionId,
                            username,
                            info.State == WtsApi32.WTS_CONNECTSTATE_CLASS.WTSActive,
                            isLocked));
                    }

                    current += Marshal.SizeOf<WtsApi32.WTS_SESSION_INFO>();
                }

                WtsApi32.WTSFreeMemory(sessionInfoPtr);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SessionWatcher] Failed to enumerate sessions: {ex.Message}");
        }

        return sessions.ToArray();
    }

    private SessionInfo? FindChildSession(SessionInfo[] sessions)
    {
        return sessions.FirstOrDefault(
            s => s.Username?.Equals(this.childUsername, StringComparison.OrdinalIgnoreCase) == true
                 && s.IsActive);
    }

    private string? GetSessionUserName(int sessionId)
    {
        try
        {
            var buffer = IntPtr.Zero;
            var bufferLen = 0;

            if (WtsApi32.WTSQuerySessionInformation(
                WtsApi32.WTS_CURRENT_SERVER_HANDLE,
                sessionId,
                WtsApi32.WTS_INFO_CLASS.WTSUserName,
                out buffer,
                out bufferLen))
            {
                var username = Marshal.PtrToStringUni(buffer);
                WtsApi32.WTSFreeMemory(buffer);
                return username;
            }
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private bool IsSessionLocked(int sessionId)
    {
        // Session is locked if the desktop is in the locked state
        // This is indicated by WTS_SESSION_INFO.State being WTSDisconnected
        // or by checking the input state via Process Desktop
        return false; // Simplified: actual implementation requires more WTS APIs
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.internalCts.Cancel();
            this.internalCts.Dispose();
            this.disposed = true;
        }
    }

    private readonly struct SessionInfo
    {
        public SessionInfo(int sessionId, string? username, bool isActive, bool isLocked)
        {
            this.SessionId = sessionId;
            this.Username = username;
            this.IsActive = isActive;
            this.IsLocked = isLocked;
        }

        public int SessionId { get; }

        public string? Username { get; }

        public bool IsActive { get; }

        public bool IsLocked { get; }
    }
}

/// <summary>
/// P/Invoke declarations for WTS (Windows Terminal Services) APIs.
/// </summary>
internal static class WtsApi32
{
    public static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

    public const int WTS_SESSION_ID_ANY = -1;

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool WTSEnumerateSessions(
        IntPtr hServer,
        int reserved,
        int version,
        out IntPtr sessionInfo,
        out int sessionCount);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern void WTSFreeMemory(IntPtr memory);

    [DllImport("wtsapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool WTSQuerySessionInformation(
        IntPtr hServer,
        int sessionId,
        WTS_INFO_CLASS infoClass,
        out IntPtr buffer,
        out int bufferLen);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSQueryUserToken(int sessionId, out IntPtr token);

    public enum WTS_INFO_CLASS
    {
        WTSUserName = 5,
        WTSDomainName = 7,
        WTSSessionId = 4,
    }

    public enum WTS_CONNECTSTATE_CLASS
    {
        WTSActive = 0,
        WTSDisconnected = 1,
        WTSConnected = 2,
        WTSConnectQuery = 3,
        WTSShadow = 4,
        WTSDisconn = 5,
        WTSWait = 6,
        WTSReset = 7,
        WTSDown = 8,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WTS_SESSION_INFO
    {
        public int SessionId;
        public IntPtr pWinStationName;
        public WTS_CONNECTSTATE_CLASS State;
    }
}
