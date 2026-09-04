// <copyright file="ProcessTerminator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

using System.Diagnostics;

/// <summary>
/// T11 — Implementación de IProcessTerminator.
/// Termina procesos de apps bloqueadas usando Win32 API.
/// </summary>
public sealed class ProcessTerminator : IProcessTerminator
{
    private readonly Func<int, IExactProcessHandle> openProcess;
    private readonly TimeSpan gracefulCloseTimeout;

    // Procesos del sistema que NUNCA deben ser terminados
    private static readonly HashSet<string> SystemProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Session agent - nunca matar
        "ControlParental.SessionAgent",
        "ControlParental.SessionAgent.exe",

        // Windows session management
        "winlogon",
        "logonui",
        "csrss",
        "smss",
        "services",
        "lsass",
        "svchost",
        "dwm",
        "explorer",
        "explorer.exe",

        // UAC / consent
        "consent",
        "consent.exe",

        // System
        "system",
        "registry",
        "Memory Compression",

        // Accessibility - nunca bloquear accesibilidad
        "ctfmon",
        "magnify",
        "magnify.exe",
        "narrator",
        "narrator.exe",
        "osk",
        "osk.exe",
        "sapi",
        "sapi.exe",
        "sapisvr",

        // Reserved system processes
        "wininit",
        "winresume",
        "fontdrvhost",
        "smss",
        "conhost",
    };

    public ProcessTerminator()
        : this(pid => new SystemProcessHandle(Process.GetProcessById(pid)), TimeSpan.FromSeconds(3))
    {
    }

    public ProcessTerminator(IExactProcessHandle process)
        : this(_ => process, TimeSpan.FromSeconds(3))
    {
    }

    public ProcessTerminator(IExactProcessHandle process, TimeSpan gracefulCloseTimeout)
        : this(_ => process, gracefulCloseTimeout)
    {
    }

    private ProcessTerminator(Func<int, IExactProcessHandle> openProcess, TimeSpan gracefulCloseTimeout)
    {
        this.openProcess = openProcess;
        this.gracefulCloseTimeout = gracefulCloseTimeout;
    }

    /// <inheritdoc />
    public async Task<bool> TerminateAsync(
        string appId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return false;
    }

    public async Task<ActionStatus> TerminateAsync(
        ObservedProcessTarget target,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = this.openProcess(target.ProcessId);
            if (process.HasExited)
            {
                return ActionStatus.HarmlessAbsence;
            }

            var identity = process.ReadIdentity();
            if (identity.ProcessId != target.ProcessId || identity.SessionId != target.SessionId ||
                identity.StartedAt != target.StartedAt || !this.CanTerminate(identity.ProcessName))
            {
                return ActionStatus.InvalidTarget;
            }

            process.CloseMainWindow();
            using var grace = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            grace.CancelAfter(this.gracefulCloseTimeout);
            try
            {
                await process.WaitForExitAsync(grace.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                identity = process.ReadIdentity();
                if (identity.ProcessId != target.ProcessId || identity.SessionId != target.SessionId ||
                    identity.StartedAt != target.StartedAt)
                {
                    return ActionStatus.InvalidTarget;
                }

                process.Kill();
                await process.WaitForExitAsync(cancellationToken);
            }

            return ActionStatus.Confirmed;
        }
        catch (UnauthorizedAccessException) { return ActionStatus.AccessDenied; }
        catch (ArgumentException) { return ActionStatus.HarmlessAbsence; }
        catch (InvalidOperationException) { return ActionStatus.HarmlessAbsence; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { return ActionStatus.NativeFailure; }
    }

    /// <inheritdoc />
    public bool CanTerminate(string appId)
    {
        // Normalize the appId - remove .exe if present
        var normalized = appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? appId[..^4]
            : appId;

        // Check against protected system processes
        if (SystemProcessNames.Contains(appId) || SystemProcessNames.Contains(normalized))
        {
            return false;
        }

        // Check if it's a process with a reserved name pattern
        var lower = normalized.ToLowerInvariant();
        if (lower.Contains("controlparental") ||
            lower.Contains("system") ||
            lower == "explorer" ||
            lower == "winlogon" ||
            lower == "csrss" ||
            lower == "services" ||
            lower == "lsass")
        {
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public int? GetProcessId(string appId)
    {
        try
        {
            // Try to find process by name (with and without .exe)
            var processes = Process.GetProcessesByName(appId);
            if (processes.Length > 0)
            {
                return processes[0].Id;
            }

            // Try without .exe
            var withoutExe = appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? appId[..^4]
                : appId;
            processes = Process.GetProcessesByName(withoutExe);
            if (processes.Length > 0)
            {
                return processes[0].Id;
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ProcessTerminator] Error getting PID for {appId}: {ex.Message}");
            return null;
        }
    }

}
