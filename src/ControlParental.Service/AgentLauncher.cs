// <copyright file="AgentLauncher.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Diagnostics;
using System.Runtime.InteropServices;
using ControlParental.Domain;

/// <summary>
/// Launches the Session Agent in the interactive session of the target child user.
/// Uses CreateProcessAsUser to spawn the agent with the child's token.
/// </summary>
public sealed class AgentLauncher : IDisposable
{
    private readonly string agentExePath;
    private readonly string pipeName;
    private readonly Func<string, bool> validateClientSid;
    private readonly Action<IIpcMessage> onAgentMessage;
    private readonly Action onAgentDisconnected;
    private Process? agentProcess;
    private Interop.NamedPipeServer? ipcServer;
    private CancellationTokenSource? cts;
    private bool disposed;

    /// <summary>
    /// Gets the IPC channel used to communicate with the agent.
    /// Returns null if the agent is not running.
    /// </summary>
    public IIpcChannel? AgentChannel => this.ipcServer;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentLauncher"/> class.
    /// </summary>
    /// <param name="agentExePath">Path to the Session Agent executable.</param>
    /// <param name="pipeName">Name of the IPC pipe.</param>
    /// <param name="validateClientSid">Function to validate the client SID.</param>
    /// <param name="onAgentMessage">Callback when a message is received from the agent.</param>
    /// <param name="onAgentDisconnected">Callback when the agent disconnects.</param>
    public AgentLauncher(
        string agentExePath,
        string pipeName,
        Func<string, bool> validateClientSid,
        Action<IIpcMessage> onAgentMessage,
        Action onAgentDisconnected)
    {
        this.agentExePath = agentExePath ?? throw new ArgumentNullException(nameof(agentExePath));
        this.pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
        this.validateClientSid = validateClientSid ?? throw new ArgumentNullException(nameof(validateClientSid));
        this.onAgentMessage = onAgentMessage ?? throw new ArgumentNullException(nameof(onAgentMessage));
        this.onAgentDisconnected = onAgentDisconnected ?? throw new ArgumentNullException(nameof(onAgentDisconnected));
    }

    /// <summary>
    /// Launches the agent in the specified session.
    /// </summary>
    /// <param name="sessionId">The session ID to launch the agent in.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the agent was launched successfully.</returns>
    public async Task<bool> LaunchAgentAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        if (this.disposed)
        {
            throw new ObjectDisposedException(nameof(AgentLauncher));
        }

        // Kill existing agent if any
        await this.KillAgentAsync();

        // Get the user token for the session
        var userToken = this.GetSessionUserToken(sessionId);
        if (userToken == null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AgentLauncher] Could not get user token for session {sessionId}.");
            return false;
        }

        // Start the IPC server
        this.cts = new CancellationTokenSource();
        this.ipcServer = new Interop.NamedPipeServer(this.pipeName, this.validateClientSid);
        this.ipcServer.MessageReceived += this.onAgentMessage;
        this.ipcServer.Disconnected += this.onAgentDisconnected;
        await this.ipcServer.StartAsync(this.cts.Token);

        // Launch the agent with the user's token
        if (userToken == null)
        {
            return false;
        }

        var success = this.CreateProcessAsUser(userToken.Value, sessionId);

        if (!success)
        {
            this.ipcServer.StopAsync().Wait(TimeSpan.FromSeconds(1));
            this.ipcServer.Dispose();
            this.ipcServer = null;
            this.cts.Dispose();
            this.cts = null;
        }

        return success;
    }

    /// <summary>
    /// Kills the running agent process.
    /// </summary>
    public Task KillAgentAsync()
    {
        if (this.agentProcess != null && !this.agentProcess.HasExited)
        {
            try
            {
                this.agentProcess.Kill(entireProcessTree: true);
                this.agentProcess.WaitForExit(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AgentLauncher] Failed to kill agent: {ex.Message}");
            }

            this.agentProcess = null;
        }

        // Stop IPC server
        if (this.ipcServer != null)
        {
            this.ipcServer.StopAsync().Wait(TimeSpan.FromSeconds(1));
            this.ipcServer.Dispose();
            this.ipcServer = null;
        }

        this.cts?.Cancel();
        this.cts?.Dispose();
        this.cts = null;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message to the agent over the IPC channel.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendToAgentAsync(IIpcMessage message, CancellationToken cancellationToken = default)
    {
        if (this.ipcServer != null && this.ipcServer.IsConnected)
        {
            await this.ipcServer.SendAsync(message, cancellationToken);
        }
    }

    /// <summary>
    /// Checks if the agent process is running.
    /// </summary>
    public bool IsAgentRunning =>
        this.agentProcess != null && !this.agentProcess.HasExited;

    private IntPtr? GetSessionUserToken(int sessionId)
    {
        try
        {
            // Call WTSQueryUserToken to get the user token
            IntPtr token;
            if (WtsApi32.WTSQueryUserToken(sessionId, out token))
            {
                return token;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AgentLauncher] WTSQueryUserToken failed: {ex.Message}");
        }

        return null;
    }

    private bool CreateProcessAsUser(IntPtr userToken, int sessionId)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = this.agentExePath,
                Arguments = $"--pipe={this.pipeName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };

            // Duplicate the token to allow process creation
            var duplicatedToken = IntPtr.Zero;
            if (!DuplicateTokenEx(
                userToken,
                0,
                IntPtr.Zero,
                SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation,
                TOKEN_TYPE.TokenPrimary,
                out duplicatedToken))
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AgentLauncher] DuplicateTokenEx failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            // Create the process with the duplicated token
            // Note: CreateProcessAsUser requires the token to have SE_ASSIGN_PRIMARY_TOKEN
            // and SE_INCREASE_QUOTA_NAME privileges, which LocalSystem typically has.
            var processInfo = new STARTUPINFO();
            processInfo.cb = Marshal.SizeOf<STARTUPINFO>();

            var environmentBlock = IntPtr.Zero;
            if (!CreateEnvironmentBlock(out environmentBlock, duplicatedToken, false))
            {
                environmentBlock = IntPtr.Zero;
            }

            var success = CreateProcessAsUser(
                duplicatedToken,
                null,
                this.agentExePath,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                CreateProcessFlags.CREATE_NO_WINDOW | CreateProcessFlags.CREATE_UNICODE_ENVIRONMENT,
                environmentBlock,
                null,
                ref processInfo,
                out var procInfo);

            if (environmentBlock != IntPtr.Zero)
            {
                DestroyEnvironmentBlock(environmentBlock);
            }

            if (success)
            {
                this.agentProcess = Process.GetProcessById((int)procInfo.dwProcessId);
                CloseHandle(procInfo.hProcess);
                CloseHandle(procInfo.hThread);
            }

            CloseHandle(duplicatedToken);
            return success;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AgentLauncher] CreateProcessAsUser failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.KillAgentAsync().Wait(TimeSpan.FromSeconds(2));
            this.disposed = true;
        }
    }

    private enum SECURITY_IMPERSONATION_LEVEL
    {
        SecurityImpersonation = 2,
    }

    private enum TOKEN_TYPE
    {
        TokenPrimary = 1,
    }

    [Flags]
    private enum CreateProcessFlags
    {
        CREATE_NO_WINDOW = 0x08000000,
        CREATE_UNICODE_ENVIRONMENT = 0x00000400,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFO
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        int desiredAccess,
        IntPtr tokenAttributes,
        SECURITY_IMPERSONATION_LEVEL impersonationLevel,
        TOKEN_TYPE tokenType,
        out IntPtr newToken);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr token,
        string? applicationName,
        string commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        CreateProcessFlags flags,
        IntPtr environment,
        string? currentDirectory,
        ref STARTUPINFO startupInfo,
        out PROCESS_INFORMATION processInformation);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(
        out IntPtr environment,
        IntPtr token,
        bool inherit);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
