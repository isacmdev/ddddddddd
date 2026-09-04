// <copyright file="UIMessageHandlerConsentTests.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service.Tests;

using ControlParental.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public sealed class UIMessageHandlerConsentTests : IDisposable
{
    private readonly ServiceProvider provider;
    private readonly UIMessageHandler handler;

    public UIMessageHandlerConsentTests()
    {
        var services = new ServiceCollection();
        var dbOptions = new DbContextOptionsBuilder<ControlParentalDbContext>()
            .UseInMemoryDatabase($"ipc-consent-{Guid.NewGuid():N}")
            .Options;
        services.AddSingleton<IDbContextFactory<ControlParentalDbContext>>(new TrackingDbContextFactory(dbOptions));
        services.AddSingleton<ITimeProvider, TimeProvider>();
        services.AddScoped<IConsentService, ConsentService>();
        this.provider = services.BuildServiceProvider();

        var folder = Path.Combine(Path.GetTempPath(), $"cp-handler-{Guid.NewGuid():N}");
        var stateService = new OnboardingStateService(
            folder,
            new Mock<IChildAccountStore>().Object,
            new Mock<ILogger<OnboardingStateService>>().Object);
        var monitor = new Mock<IEnforcementLevelMonitor>();
        monitor.SetupGet(x => x.CurrentIssues).Returns(Array.Empty<EnforcementIssue>());
        this.handler = new UIMessageHandler(
            stateService,
            new EnforcementLevelQueryHandler(monitor.Object),
            this.provider.GetRequiredService<IServiceScopeFactory>(),
            new Mock<ILogger<UIMessageHandler>>().Object);
    }

    [Fact]
    public async Task HandleGrantConsent_ServiceUnavailable_ReturnsError()
    {
        var emptyProvider = new ServiceCollection().BuildServiceProvider();
        var unavailable = new UIMessageHandler(
            new OnboardingStateService(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")),
                new Mock<IChildAccountStore>().Object,
                new Mock<ILogger<OnboardingStateService>>().Object),
            new EnforcementLevelQueryHandler(new Mock<IEnforcementLevelMonitor>().Object),
            emptyProvider.GetRequiredService<IServiceScopeFactory>(),
            new Mock<ILogger<UIMessageHandler>>().Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unavailable.HandleAsync(new GrantConsent("device")));
    }

    [Fact]
    public async Task HandleGetConsentStatus_OnFreshDb_ReturnsNotStarted()
    {
        var response = Assert.IsType<ConsentStatusSnapshot>(
            await this.handler.HandleAsync(new GetConsentStatus()));

        Assert.False(response.IsGranted);
        Assert.Equal(default, response.GrantedAt);
        Assert.Null(response.GrantedByDeviceId);
    }

    [Fact]
    public async Task HandleGrantConsent_ThenHandleGetConsentStatus_ReturnsGranted()
    {
        var granted = Assert.IsType<ConsentStatusSnapshot>(
            await this.handler.HandleAsync(new GrantConsent("ipc-device")));
        var queried = Assert.IsType<ConsentStatusSnapshot>(
            await this.handler.HandleAsync(new GetConsentStatus()));

        Assert.True(granted.IsGranted);
        Assert.True(queried.IsGranted);
        Assert.Equal("ipc-device", queried.GrantedByDeviceId);
    }

    public void Dispose()
    {
        this.provider.Dispose();
    }
}
