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
    private readonly Func<string, (bool Success, string Output)> runScCommand;
    private readonly object stateLock = new();
    private readonly HashSet<string> configuredFailureActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> configuredStartupTypes = new(StringComparer.OrdinalIgnoreCase);

    public ScmController()
        : this(null)
    {
    }

    internal ScmController(Func<string, (bool Success, string Output)>? runScCommand)
    {
        this.runScCommand = runScCommand ?? this.RunScCommand;
    }

    /// <inheritdoc />
    public Task<bool> IsServiceRunningAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                var result = this.runScCommand($"query \"{serviceName}\"");
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
            async () =>
            {
                if (await this.IsServiceRunningAsync(serviceName, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }

                var result = this.runScCommand($"start \"{serviceName}\"");
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
            async () =>
            {
                if (!await this.IsServiceRunningAsync(serviceName, cancellationToken).ConfigureAwait(false))
                {
                    return true;
                }

                var result = this.runScCommand($"stop \"{serviceName}\"");
                return result.Success;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ConfigureFailureActionsAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        lock (this.stateLock)
        {
            if (this.configuredFailureActions.Contains(serviceName))
            {
                return Task.FromResult(true);
            }
        }

        return Task.Run(
            () =>
            {
                // Configure failure actions: restart on first, second, and subsequent failures
                // Reset period = 1 day (86400 seconds)
                // Restart delay = 60 seconds
                var result = this.runScCommand(
                    $"failure \"{serviceName}\" " +
                    $"actions= restart/60000/restart/60000/restart/60000 " +
                    $"reset= 86400");

                if (!result.Success)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ScmController] Failed to configure failure actions: {result.Output}");
                    return false;
                }

                lock (this.stateLock)
                {
                    this.configuredFailureActions.Add(serviceName);
                }

                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> SetStartupTypeAsync(
        string serviceName,
        string startupType,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = startupType.ToLowerInvariant() switch
        {
            "auto" or "automatic" => "auto",
            "delayed-auto" or "delayed" => "delayed-auto",
            "manual" => "demand",
            "disabled" => "disabled",
            _ => startupType.ToLowerInvariant(),
        };

        lock (this.stateLock)
        {
            if (this.configuredStartupTypes.TryGetValue(serviceName, out var currentType) &&
                string.Equals(currentType, normalizedType, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(true);
            }
        }

        return Task.Run(
            () =>
            {
                var result = this.runScCommand(
                    $"config \"{serviceName}\" start= {normalizedType}");

                if (!result.Success)
                {
                    return false;
                }

                lock (this.stateLock)
                {
                    this.configuredStartupTypes[serviceName] = normalizedType;
                }

                return true;
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
