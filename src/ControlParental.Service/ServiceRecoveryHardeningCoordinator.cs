// <copyright file="ServiceRecoveryHardeningCoordinator.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// Isolates SCM recovery hardening from the rest of startup hardening.
/// </summary>
internal sealed class ServiceRecoveryHardeningCoordinator
{
    private readonly IScmController scmController;

    public ServiceRecoveryHardeningCoordinator(IScmController scmController)
    {
        this.scmController = scmController ?? throw new ArgumentNullException(nameof(scmController));
    }

    public async Task<bool> ApplyAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        var failureActionsConfigured = await this.scmController.ConfigureFailureActionsAsync(
            serviceName,
            cancellationToken).ConfigureAwait(false);

        if (!failureActionsConfigured)
        {
            return false;
        }

        return await this.scmController.SetStartupTypeAsync(
            serviceName,
            "auto",
            cancellationToken).ConfigureAwait(false);
    }
}
