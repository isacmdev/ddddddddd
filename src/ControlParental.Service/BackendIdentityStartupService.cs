// <copyright file="BackendIdentityStartupService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using Microsoft.Extensions.Hosting;

internal sealed class BackendIdentityStartupService : IHostedService
{
    private readonly BackendIdentityCoordinator coordinator;

    public BackendIdentityStartupService(BackendIdentityCoordinator coordinator)
    {
        this.coordinator = coordinator;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await this.coordinator.InitializeAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
