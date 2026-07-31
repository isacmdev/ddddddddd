// <copyright file="IpcMessage.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Domain;

/// <summary>
/// Base interface for all IPC messages between Service and Session Agent.
/// The direction indicates who sends the message.
/// </summary>
public interface IIpcMessage
{
    /// <summary>
    /// Gets the message type for routing and deserialization.
    /// </summary>
    string MessageType { get; }
}

/// <summary>
/// Messages sent from the Session Agent to the Service.
/// </summary>
public interface IAgentToServiceMessage : IIpcMessage
{
}

/// <summary>
/// Messages sent from the Service to the Session Agent.
/// </summary>
public interface IServiceToAgentMessage : IIpcMessage
{
}

/// <summary>
/// Agent → Service: notifies when the foreground app changes.
/// </summary>
public sealed record ForegroundChanged(string AppId) : IAgentToServiceMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ForegroundChanged);
}

/// <summary>
/// Agent → Service: periodic heartbeat to confirm the agent is alive.
/// </summary>
public sealed record AgentHeartbeat(
    string AgentId,
    long UpTimeMs,
    bool IsOverlayVisible) : IAgentToServiceMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(AgentHeartbeat);
}

/// <summary>
/// Service → Agent: command to show a blocking overlay.
/// </summary>
public sealed record ShowOverlay(string Reason, string? CtaLabel = null) : IServiceToAgentMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ShowOverlay);
}

/// <summary>
/// Service → Agent: command to hide the blocking overlay.
/// </summary>
public sealed record HideOverlay() : IServiceToAgentMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(HideOverlay);
}

/// <summary>
/// Service → Agent: command to show a time warning toast.
/// </summary>
public sealed record ShowWarning(int MinutesRemaining) : IServiceToAgentMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(ShowWarning);
}

/// <summary>
/// Service → Agent: command to lock the workstation immediately.
/// </summary>
public sealed record LockWorkstation() : IServiceToAgentMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(LockWorkstation);
}

/// <summary>
/// Service → Agent: request for a state snapshot.
/// </summary>
public sealed record RequestStateSnapshot() : IServiceToAgentMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(RequestStateSnapshot);
}

/// <summary>
/// Agent → Service: response with the current state.
/// </summary>
public sealed record StateSnapshot(
    string AppId,
    bool IsOverlayVisible,
    long UpTimeMs) : IAgentToServiceMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(StateSnapshot);
}

/// <summary>
/// Service → Agent: ping/keepalive to check if agent is responsive.
/// </summary>
public sealed record Ping() : IServiceToAgentMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(Ping);
}

/// <summary>
/// Agent → Service: response to ping.
/// </summary>
public sealed record Pong() : IAgentToServiceMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(Pong);
}

/// <summary>
/// T27 — Messages sent from App.UI to the Service via IPC.
/// </summary>
public interface IUIMessage : IIpcMessage
{
}

/// <summary>
/// T27 — UI → Service: request for the current usage state.
/// </summary>
public sealed record GetUsageState() : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(GetUsageState);
}

/// <summary>
/// T27 — Service → UI: response with the current usage state.
/// </summary>
/// <param name="MinutesRemaining">Minutes remaining for the current foreground app (null if unlimited).</param>
/// <param name="CurrentAppId">The app package name currently in foreground (null if none).</param>
/// <param name="IsPaused">Whether usage tracking is paused (session locked/suspended).</param>
/// <param name="ActiveGrants">Time grants currently in effect.</param>
/// <param name="CurrentLevel">Current enforcement level (Standard/Managed/etc).</param>
/// <param name="ActiveIssues">Current enforcement issues (e.g. service not running, agent down).</param>
public sealed record UsageStateResponse(
    int? MinutesRemaining,
    string? CurrentAppId,
    bool IsPaused,
    IReadOnlyList<GrantInfo> ActiveGrants,
    EnforcementLevel CurrentLevel,
    IReadOnlyList<ActiveIssue> ActiveIssues) : IUIMessage
{
    /// <inheritdoc />
    public string MessageType => nameof(UsageStateResponse);
}

/// <summary>
/// T27 — Summary of an active time grant for the UI.
/// </summary>
/// <param name="Scope">Grant scope (device, package name, or category).</param>
/// <param name="MinutesRemaining">Minutes remaining on this grant.</param>
/// <param name="Source">Source: ExtraTime, Reward, or Manual.</param>
/// <param name="ExpiresAt">When this grant expires (UTC).</param>
public sealed record GrantInfo(
    string Scope,
    int MinutesRemaining,
    GrantSource Source,
    DateTimeOffset ExpiresAt);

/// <summary>
/// T27 — Summary of an active enforcement issue for the UI.
/// </summary>
/// <param name="Type">The type of issue.</param>
/// <param name="Severity">Severity level.</param>
/// <param name="Description">Human-readable description.</param>
public sealed record ActiveIssue(
    EnforcementIssueType Type,
    EnforcementIssueSeverity Severity,
    string Description);
