// <copyright file="ScmController.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

using System.Diagnostics;

/// <summary>
/// Windows implementation of <see cref="IScmController"/>.
/// Controls the Windows Service Control Manager (SCM) using sc.exe.
/// Used by T10 (persistence) and T12 (health monitoring).
/// </summary>
public sealed class ScmController : IScmController
{
    private const string ScExePath = "sc.exe";

    /// <inheritdoc />
    public Task<bool> IsServiceRunningAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var result = this.RunScCommand($"query \"{serviceName}\"");
                if (!result.Success)
                {
                    return false;
                }

                // Check if the service is in a running state
                return result.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> StartServiceAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var result = this.RunScCommand($"start \"{serviceName}\"");
                return result.Success;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> StopServiceAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var result = this.RunScCommand($"stop \"{serviceName}\"");
                return result.Success;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ConfigureFailureActionsAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                // Configure failure actions: restart on first, second, and subsequent failures
                // Reset period = 1 day (86400 seconds)
                // Restart delay = 60 seconds
                var result = this.RunScCommand(
                    $"failure \"{serviceName}\" " +
                    $"actions= restart/60000/restart/60000/restart/60000 " +
                    $"reset= 86400");

                if (!result.Success)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ScmController] Failed to configure failure actions: {result.Output}");
                }

                return result.Success;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> SetStartupTypeAsync(
        string serviceName,
        string startupType,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var normalizedType = startupType.ToLowerInvariant() switch
                {
                    "auto" or "automatic" => "auto",
                    "delayed-auto" or "delayed" => "delayed-auto",
                    "manual" => "demand",
                    "disabled" => "disabled",
                    _ => startupType.ToLowerInvariant(),
                };

                var result = this.RunScCommand(
                    $"config \"{serviceName}\" start= {normalizedType}");

                return result.Success;
            },
            cancellationToken);
    }

    private (bool Success, string Output) RunScCommand(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ScExePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                return (false, "Failed to start sc.exe");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(TimeSpan.FromSeconds(30));

            var success = process.ExitCode == 0;
            var message = success ? output : $"{output}\n{error}";

            return (success, message);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
