// <copyright file="NamedPipeUIAdapter.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI;

using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using ControlParental.Domain;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T27 — Named pipe client for querying the Service from App.UI.
/// Connects to the same pipe as the Session Agent (same user context).
/// </summary>
public sealed class NamedPipeUIAdapter : IUIPipeClient
{
    private const string PipeName = "ControlParental.SessionAgent";
    private const int TimeoutMs = 5000;
    private bool disposed;

    /// <inheritdoc />
    public async Task<UsageStateResponse> GetUsageStateAsync(CancellationToken ct = default)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(TimeoutMs, ct);

            var query = new GetUsageState();
            var json = JsonSerializer.Serialize(query);
            var bytes = Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes, ct);

            var buffer = new byte[65536];
            var bytesRead = await pipe.ReadAsync(buffer, ct);
            var responseJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            var response = JsonSerializer.Deserialize<UsageStateResponse>(responseJson);

            return response ?? this.CreateFallbackResponse();
        }
        catch
        {
            return this.CreateFallbackResponse();
        }
    }

    private UsageStateResponse CreateFallbackResponse()
    {
        return new UsageStateResponse(
            MinutesRemaining: null,
            CurrentAppId: null,
            IsPaused: false,
            ActiveGrants: Array.Empty<GrantInfo>(),
            CurrentLevel: EnforcementLevel.Unknown,
            ActiveIssues: Array.Empty<ActiveIssue>());
    }

    public void Dispose()
    {
        if (!this.disposed)
        {
            this.disposed = true;
        }
    }
}
