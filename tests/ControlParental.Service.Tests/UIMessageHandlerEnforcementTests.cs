// <copyright file="UIMessageHandlerEnforcementTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// T26 PR #10 — Pins the <see cref="UIMessageHandler"/> dispatch for the
/// <see cref="GetEnforcementLevel"/> query. The handler MUST route the query
/// to <see cref="EnforcementLevelQueryHandler"/> and return its response
/// untouched — a future refactor that bypasses the aggregator (e.g. by
/// issuing a synthetic "all-pass" response) would re-introduce the inflated
/// progress bar the audit flagged in Fase 2 (ADR-005).
/// </summary>
public sealed class UIMessageHandlerEnforcementTests : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly OnboardingStateService stateService;
    private readonly Mock<IEnforcementLevelMonitor> monitor;
    private readonly UIMessageHandler handler;

    public UIMessageHandlerEnforcementTests()
    {
        var services = new ServiceCollection();
        this.provider = services.BuildServiceProvider();

        var folder = Path.Combine(Path.GetTempPath(), $"cp-handler-enf-{Guid.NewGuid():N}");
        this.stateService = new OnboardingStateService(
            folder,
            new Mock<IChildAccountStore>().Object,
            new Mock<ILogger<OnboardingStateService>>().Object);
        this.monitor = new Mock<IEnforcementLevelMonitor>();
        this.monitor.SetupGet(x => x.CurrentLevel).Returns(EnforcementLevel.Standard);
        this.monitor.SetupGet(x => x.CurrentIssues).Returns(Array.Empty<EnforcementIssue>());

        this.handler = new UIMessageHandler(
            this.stateService,
            new EnforcementLevelQueryHandler(this.monitor.Object),
            this.provider.GetRequiredService<IServiceScopeFactory>(),
            new Mock<ILogger<UIMessageHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_GetEnforcementLevel_ReturnsAggregatedSnapshot()
    {
        // Arrange — monitor reports Standard + no issues, so the aggregator
        // emits four passing checks and the wire envelope must echo that.

        // Act
        var response = await this.handler.HandleAsync(new GetEnforcementLevel());

        // Assert — the handler returns the aggregator's response (not a
        // synthetic success). The wire type is the Domain envelope because
        // UIMessageHandler runs in the Service assembly.
        var snapshot = Assert.IsType<EnforcementLevelResponse>(response);
        Assert.Equal(EnforcementLevel.Standard, snapshot.Level);
        Assert.Equal(4, snapshot.Checks.Count);
        Assert.All(snapshot.Checks, c => Assert.True(c.IsPassing));
    }

    public void Dispose()
    {
        this.provider.Dispose();
    }
}
