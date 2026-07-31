// <copyright file="AppIdentityResolver.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Interop;

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

/// <summary>
/// T05/T07 — Resolves a process name/path to a canonical AppId.
/// Win32 apps: exe name + publisher Authenticode (e.g. "chrome.exe|Google Inc")
/// Falls back to: exe name alone, then exe name + file hash.
/// </summary>
public static class AppIdentityResolver
{
    // Noise filter: common system processes that should be ignored
    private static readonly HashSet<string> SystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Idle", "svchost.exe", "csrss.exe", "wininit.exe",
        "services.exe", "lsass.exe", "smss.exe", "winlogon.exe",
        "dwm.exe", "explorer.exe", "conhost.exe", "taskhostw.exe",
        "RuntimeBroker.exe", "SearchHost.exe", "sihost.exe",
        "ctfmon.exe", "dllhost.exe", "wmiprvse.exe", "audiodg.exe",
        "fontdrvhost.exe", "WmiPrvSE.exe", "msiexec.exe", "rundll32.exe",
        "cmd.exe", "powershell.exe", "werfault.exe", "wusa.exe",
        "spoolsv.exe", "fontdrvhost.exe", "sihost.exe",
    };

    /// <summary>
    /// Resolves a process name (from WMI) or process path to a canonical AppId.
    /// </summary>
    /// <param name="processNameOrPath">Process name (e.g. "chrome.exe") or full path.</param>
    /// <returns>A stable AppId string, or the process name if resolution fails.</returns>
    public static string Resolve(string processNameOrPath)
    {
        if (string.IsNullOrEmpty(processNameOrPath))
        {
            return string.Empty;
        }

        var fileName = Path.GetFileName(processNameOrPath);
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = processNameOrPath;
        }

        // Ignore system processes
        if (SystemProcesses.Contains(fileName))
        {
            return string.Empty;
        }

        // Try to get publisher info from Authenticode
        var publisher = TryGetPublisher(processNameOrPath);
        if (!string.IsNullOrEmpty(publisher))
        {
            return $"{fileName}|{publisher}";
        }

        // Fallback to file hash
        var hash = TryComputeFileHash(processNameOrPath);
        if (!string.IsNullOrEmpty(hash))
        {
            return $"{fileName}|{hash}";
        }

        // Last resort: just the exe name
        return fileName;
    }

    /// <summary>
    /// Quick resolve for WMI process names (just the exe name, no file system access).
    /// </summary>
    /// <param name="processName">Process name (e.g. "chrome.exe").</param>
    /// <returns>AppId = processName (or empty if system process).</returns>
    public static string ResolveQuick(string processName)
    {
        if (string.IsNullOrEmpty(processName))
        {
            return string.Empty;
        }

        if (SystemProcesses.Contains(processName))
        {
            return string.Empty;
        }

        return processName;
    }

    private static string? TryGetPublisher(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(filePath);
            var company = versionInfo.CompanyName;

            if (string.IsNullOrEmpty(company))
            {
                return null;
            }

            // Normalize: trim, collapse spaces, remove common suffixes
            var normalized = Regex.Replace(company.Trim(), @"\s+", " ");
            normalized = Regex.Replace(
                normalized,
                @"\s+(Inc|LLC|Corp|Ltd| GmbH| Ltd)\.?$",
                string.Empty,
                RegexOptions.IgnoreCase);

            return normalized.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryComputeFileHash(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hashBytes = sha256.ComputeHash(stream);

            // First 4 bytes of SHA256 (16 bits) — enough for our use case
            return Convert.ToHexString(hashBytes, 0, 4).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }
}
