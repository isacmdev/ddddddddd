// <copyright file="PairingService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using ControlParental.Domain;

/// <summary>
/// T24 application facade. Identity authority and credentials remain in the coordinator.
/// </summary>
public sealed class PairingService : IPairingService
{
    private readonly IBackendIdentityCoordinator coordinator;

    public PairingService(IBackendIdentityCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public bool IsPaired => this.coordinator.CurrentState.CanAuthorizeRemoteAccess;

    public string? GetCurrentDeviceId() => this.coordinator.CurrentState.DeviceId;

    public async Task<PairingResult> PairAsync(
        string code,
        AgeBand ageBand,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6)
        {
            return PairingResult.Error("El código debe tener 6 caracteres.");
        }

        var command = new PairingCommand(
            code.Trim().ToUpperInvariant(),
            ageBand,
            Guid.NewGuid().ToString("N"),
            Environment.MachineName,
            Environment.OSVersion.VersionString,
            typeof(PairingService).Assembly.GetName().Version?.ToString() ?? "1.0.0");
        var result = await this.coordinator.PairAsync(command, cancellationToken);
        return result.Error switch
        {
            BackendIdentityErrorV1.None => PairingResult.SuccessResult(
                result.DeviceId!, result.ParentId!, result.PolicyVersion),
            BackendIdentityErrorV1.NotFound => PairingResult.InvalidCode(),
            BackendIdentityErrorV1.Gone => PairingResult.ExpiredCode(),
            BackendIdentityErrorV1.RateLimited => PairingResult.TooManyRequests(result.RetryAfter),
            BackendIdentityErrorV1.Cancelled => PairingResult.Error("La operación fue cancelada."),
            BackendIdentityErrorV1.Timeout => PairingResult.Error("No se pudo confirmar el emparejamiento."),
            _ => PairingResult.Error("No se pudo completar el emparejamiento."),
        };
    }
}
