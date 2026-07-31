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

    /// <inheritdoc />
    public async Task<bool> TerminateAsync(
        string appId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!this.CanTerminate(appId))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ProcessTerminator] Cannot terminate protected process: {appId}");
            return false;
        }

        var pid = this.GetProcessId(appId);
        if (!pid.HasValue)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ProcessTerminator] Process not found for AppId: {appId}");
            return false;
        }

        return await this.TerminateProcessByIdAsync(pid.Value, reason, cancellationToken);
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

    private async Task<bool> TerminateProcessByIdAsync(
        int pid,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(pid);

            System.Diagnostics.Debug.WriteLine(
                $"[ProcessTerminator] Terminating process {pid} ({process.ProcessName}): {reason}");

            // Give the process a chance to close gracefully
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(3000))
                {
                    // Force terminate if it doesn't close in 3 seconds
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // Process already exited
                System.Diagnostics.Debug.WriteLine(
                    $"[ProcessTerminator] Process {pid} already exited.");
                return true;
            }

            await process.WaitForExitAsync(cancellationToken);
            System.Diagnostics.Debug.WriteLine(
                $"[ProcessTerminator] Process {pid} terminated successfully.");
            return true;
        }
        catch (ArgumentException)
        {
            // Process no longer exists
            System.Diagnostics.Debug.WriteLine(
                $"[ProcessTerminator] Process {pid} not found (already exited).");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[ProcessTerminator] Error terminating process {pid}: {ex.Message}");
            return false;
        }
    }
}
