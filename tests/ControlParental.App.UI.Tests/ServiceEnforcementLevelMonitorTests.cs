// <copyright file="ServiceEnforcementLevelMonitorTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.App.UI.Tests;

using System.Collections.Concurrent;
using ControlParental.App.UI.Interop;
using ControlParental.Domain;
using Xunit;

#pragma warning disable SA1649 // File name must match first type name

/// <summary>
/// T26 PR #10 — Regression tests for <see cref="ServiceEnforcementLevelMonitor"/>.
///
/// The proxy replaces the deleted App.UI stub <c>EnforcementLevelMonitor</c> with
/// a thin IPC client: it issues a single <c>GetEnforcementLevel</c> query, caches
/// the result for a short window, and surfaces IPC failures as
/// <see cref="ConsentServiceUnavailableException"/> so the VM can fail closed
/// (ADR-001). These tests pin that contract so a future refactor cannot
/// silently fall back to a local snapshot or skip the cache.
/// </summary>
public sealed class ServiceEnforcementLevelMonitorTests
{
    [Fact]
    public async Task EvaluateAsyncCallsIpcOnceOnFreshSnapshot()
    {
        // Arrange — channel reports no calls yet; the first EvaluateAsync must
        // hit the wire exactly once.
        var channel = new RecordingUIChannel();
        channel.OnGetEnforcementLevel(BuildResponse(EnforcementLevel.Standard, allPassing: true));
        var monitor = new ServiceEnforcementLevelMonitor(channel);

        // Act
        await monitor.EvaluateAsync().ConfigureAwait(false);

        // Assert
        Assert.Equal(1, channel.GetEnforcementLevelCalls);
        Assert.Equal(EnforcementLevel.Standard, monitor.CurrentLevel);
    }

    [Fact]
    public async Task EvaluateAsyncWithinCacheWindowDoesNotCallIpcAgain()
    {
        // Arrange — second call inside the 30s cache window must NOT issue
        // another IPC. A regression that drops the cache would double the
        // IPC load every time the VM polls.
        var channel = new RecordingUIChannel();
        channel.OnGetEnforcementLevel(BuildResponse(EnforcementLevel.Standard, allPassing: true));
        var monitor = new ServiceEnforcementLevelMonitor(channel);

        await monitor.EvaluateAsync().ConfigureAwait(false);
        await monitor.EvaluateAsync().ConfigureAwait(false);
        await monitor.EvaluateAsync().ConfigureAwait(false);

        // Assert — exactly one IPC call, not three.
        Assert.Equal(1, channel.GetEnforcementLevelCalls);
    }

    [Fact]
    public async Task EvaluateAsyncAfterCacheExpiresCallsIpcAgain()
    {
        // Arrange — once the cache window has elapsed, the next EvaluateAsync
        // MUST re-issue the IPC. We simulate elapsed time by handing the
        // monitor a cache timestamp in the past (the cache is internal but
        // the public surface exposes LastEvaluationTime which drives the
        // comparison).
        var channel = new RecordingUIChannel();
        channel.OnGetEnforcementLevel(BuildResponse(EnforcementLevel.Standard, allPassing: true));
        var monitor = new ServiceEnforcementLevelMonitor(channel);

        await monitor.EvaluateAsync().ConfigureAwait(false);
        var firstCalls = channel.GetEnforcementLevelCalls;

        // Force the cached timestamp far enough back that the next call
        // exceeds the 30s window.
        ForceCachedAtIntoPast(monitor, TimeSpan.FromSeconds(60));

        await monitor.EvaluateAsync().ConfigureAwait(false);

        // Assert — second IPC issued because the cache expired.
        Assert.Equal(firstCalls + 1, channel.GetEnforcementLevelCalls);
    }

    [Fact]
    public async Task EvaluateAsyncWhenChannelReturnsNullThrows()
    {
        // Arrange — a null response means the Service declined to answer.
        // The proxy must surface this as ConsentServiceUnavailableException so
        // the VM can keep its "Estado desconocido" degraded state (ADR-001).
        var channel = new RecordingUIChannel();
        channel.OnGetEnforcementLevel(null);
        var monitor = new ServiceEnforcementLevelMonitor(channel);

        // Act & Assert
        await Assert.ThrowsAsync<ConsentServiceUnavailableException>(
            () => monitor.EvaluateAsync()).ConfigureAwait(false);
    }

    [Fact]
    public async Task EvaluateAsyncMapsResponseLevelAndChecksCorrectly()
    {
        // Arrange — one passing check + one failing check. The proxy must
        // reflect the level AND expose the failing check as an
        // EnforcementIssue so the VM's CalculateRealProgress honours it.
        var checks = new List<ControlParental.App.UI.EnforcementLevelCheck>
        {
            new("child_account_standard", true, "ok"),
            new("service_running", false, "service down"),
            new("agent_emitting", true, "ok"),
            new("preventive_layer", true, "ok"),
        };
        var channel = new RecordingUIChannel();
        channel.OnGetEnforcementLevel(
            new ControlParental.App.UI.EnforcementLevelResponse(EnforcementLevel.Degraded.ToString(), checks));
        var monitor = new ServiceEnforcementLevelMonitor(channel);

        // Act
        await monitor.EvaluateAsync().ConfigureAwait(false);

        // Assert — level mapped from the wire string, failing check surfaced
        // as a typed issue (service_running → ServiceNotRunning).
        Assert.Equal(EnforcementLevel.Degraded, monitor.CurrentLevel);
        Assert.Single(monitor.CurrentIssues);
        Assert.Equal(EnforcementIssueType.ServiceNotRunning, monitor.CurrentIssues[0].Type);
    }

    [Fact]
    public void ConstructorWithNullChannelThrows()
    {
        // The proxy requires a channel — DI must not be allowed to resolve
        // a null channel. Pin this so a refactor cannot silently degrade
        // back to the deleted local-stub fallback.
        Assert.Throws<ArgumentNullException>(() => GC.KeepAlive(new ServiceEnforcementLevelMonitor(null!)));
    }

    private static ControlParental.App.UI.EnforcementLevelResponse BuildResponse(EnforcementLevel level, bool allPassing)
    {
        var checks = new List<ControlParental.App.UI.EnforcementLevelCheck>
        {
            new("child_account_standard", allPassing, allPassing ? "ok" : "fail"),
            new("service_running", allPassing, allPassing ? "ok" : "fail"),
            new("agent_emitting", allPassing, allPassing ? "ok" : "fail"),
            new("preventive_layer", allPassing, allPassing ? "ok" : "fail"),
        };
        return new ControlParental.App.UI.EnforcementLevelResponse(level.ToString(), checks);
    }

    private static void ForceCachedAtIntoPast(ServiceEnforcementLevelMonitor monitor, TimeSpan offset)
    {
        // LastEvaluationTime is the public handle on the cache timestamp. The
        // setter is internal but we can still steer the window by waiting —
        // here we reflect the field through the instance and push it back.
        var field = typeof(ServiceEnforcementLevelMonitor)
            .GetField("cachedAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field!.SetValue(monitor, DateTimeOffset.UtcNow - offset);
    }

    /// <summary>
    /// Focused <see cref="IUIChannel"/> test double that records each
    /// <c>GetEnforcementLevel</c> query so the tests can assert how many
    /// IPC round-trips the proxy issued (the canonical
    /// <c>MockNamedPipeUIChannel</c> only covers onboarding-state traffic).
    /// </summary>
    private sealed class RecordingUIChannel : IUIChannel
    {
        private readonly ConcurrentQueue<object> sent = new();
        private Func<ControlParental.App.UI.EnforcementLevelResponse?>? getEnforcementLevel;

        public int GetEnforcementLevelCalls { get; private set; }

        public void OnGetEnforcementLevel(ControlParental.App.UI.EnforcementLevelResponse? response)
        {
            this.getEnforcementLevel = () => response;
        }

        public Task<TResponse?> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken ct = default)
            where TQuery : ControlParental.Domain.IUIMessage
            where TResponse : class, ControlParental.Domain.IUIMessage
        {
            // Match by runtime type name to sidestep the Domain/App.UI
            // record duplication (both namespaces define GetEnforcementLevel).
            if (query?.GetType().FullName == typeof(ControlParental.App.UI.GetEnforcementLevel).FullName)
            {
                this.GetEnforcementLevelCalls++;
                var response = this.getEnforcementLevel?.Invoke();
                if (response is null)
                {
                    return Task.FromResult<TResponse?>(null);
                }

                return Task.FromResult<TResponse?>((TResponse)(object)response);
            }

            return Task.FromResult<TResponse?>(null);
        }

        public Task SendAsync<T>(T message, CancellationToken ct = default)
            where T : ControlParental.Domain.IUIMessage
        {
            this.sent.Enqueue(message!);
            return Task.CompletedTask;
        }
    }
}
