// <copyright file="PairingService.cs" company="ControlParental">
// Copyright (c) ControlParental. All rights reserved.
// </copyright>

namespace ControlParental.Service;

using System.Text.Json;
using ControlParental.Domain;

/// <summary>
/// T24 — Implementation of the device pairing service.
/// </summary>
public sealed class PairingService : IPairingService
{
    private readonly IDeviceAuthenticator deviceAuthenticator;
    private readonly IBackendClient backendClient;
    private readonly ISecretStore secretStore;
    private readonly IPolicyRepository policyRepository;
    private readonly ITimeProvider timeProvider;

    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="PairingService"/> class.
    /// </summary>
    /// <param name="deviceAuthenticator">Device authenticator for session management.</param>
    /// <param name="backendClient">Backend client for API calls.</param>
    /// <param name="secretStore">Secret store for persisting device credentials.</param>
    /// <param name="policyRepository">Policy repository for persisting policies.</param>
    /// <param name="timeProvider">Time provider for current time.</param>
    public PairingService(
        IDeviceAuthenticator deviceAuthenticator,
        IBackendClient backendClient,
        ISecretStore secretStore,
        IPolicyRepository policyRepository,
        ITimeProvider timeProvider)
    {
        this.deviceAuthenticator = deviceAuthenticator ?? throw new ArgumentNullException(nameof(deviceAuthenticator));
        this.backendClient = backendClient ?? throw new ArgumentNullException(nameof(backendClient));
        this.secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
        this.policyRepository = policyRepository ?? throw new ArgumentNullException(nameof(policyRepository));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public bool IsPaired
    {
        get
        {
            var result = this.secretStore.ReadAsync("device_id").GetAwaiter().GetResult();
            var deviceId = result.Value;
            return !string.IsNullOrEmpty(deviceId);
        }
    }

    /// <inheritdoc />
    public string? GetCurrentDeviceId()
    {
        var result = this.secretStore.ReadAsync("device_id").GetAwaiter().GetResult();
        return result.Value;
    }

    /// <inheritdoc />
    public async Task<PairingResult> PairAsync(string code, AgeBand ageBand, CancellationToken cancellationToken = default)
    {
        // 1. Validate inputs
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
        {
            return PairingResult.Error("El código debe tener 6 caracteres.");
        }

        var normalizedCode = code.Trim().ToUpperInvariant();

        // 2. Create anonymous session (T17)
        var sessionResult = await this.deviceAuthenticator.CreateAnonymousSessionAsync(cancellationToken);
        if (!sessionResult.Success)
        {
            return PairingResult.Error("No se pudo crear la sesión. Verificá tu conexión.");
        }

        // 3. Gather device info
        var deviceInfo = ComputerInfo.Gather();
        var appVersion = typeof(PairingService).Assembly.GetName().Version?.ToString() ?? "1.0.0";

        var request = new PairingRequest(
            Code: normalizedCode,
            DeviceName: deviceInfo.DeviceName,
            DeviceModel: deviceInfo.DeviceModel,
            OsVersion: deviceInfo.OsVersion,
            AppVersion: appVersion,
            AgeBand: ageBand.ToString());

        // 4. Call backend with retry
        var httpResult = await this.CallPairingWithRetryAsync(request, cancellationToken);

        // 5. Handle result
        return httpResult.Status switch
        {
            PairingHttpStatus.Success => await this.HandleSuccessAsync(httpResult, cancellationToken),

            PairingHttpStatus.NotFound =>
                PairingResult.InvalidCode(),

            PairingHttpStatus.Gone =>
                PairingResult.ExpiredCode(),

            PairingHttpStatus.TooManyRequests =>
                PairingResult.Error("Demasiados intentos. Esperá unos minutos e intentá de nuevo."),

            PairingHttpStatus.ServerError =>
                PairingResult.Error("Error del servidor. Intentá de nuevo en unos minutos."),

            PairingHttpStatus.NetworkError =>
                PairingResult.Error("No se pudo conectar al servidor. Verificá tu internet."),

            _ => PairingResult.Error("Error desconocido."),
        };
    }

    private async Task<PairingHttpResult> CallPairingWithRetryAsync(
        PairingRequest request,
        CancellationToken ct)
    {
        for (var i = 0; i < MaxRetries; i++)
        {
            try
            {
                var result = await this.backendClient.PairAsync(request, ct);
                if (result.Status != PairingHttpStatus.ServerError && result.Status != PairingHttpStatus.NetworkError)
                {
                    return result;
                }

                // Server error or network error — retry after delay
                if (i < MaxRetries - 1)
                {
                    await Task.Delay(RetryDelays[i], ct);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Retry on cancellation from outside? No — only retry on actual timeout/network
                throw;
            }
            catch (HttpRequestException)
            {
                if (i < MaxRetries - 1)
                {
                    await Task.Delay(RetryDelays[i], ct);
                }
            }
        }

        return PairingHttpResult.NetworkError("Max retries exceeded");
    }

    private async Task<PairingResult> HandleSuccessAsync(PairingHttpResult result, CancellationToken ct)
    {
        // Persist to SecretStore
        try
        {
            await this.secretStore.WriteAsync("device_id", result.DeviceId!, ct);
            await this.secretStore.WriteAsync("parent_id", result.ParentId!, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PairingService] SecretStore error: {ex.Message}");
            return PairingResult.Error("Error al guardar la configuración. Reiniciá la aplicación.");
        }

        // Fetch initial policy
        try
        {
            var fetchResult = await this.backendClient.FetchPolicyAsync(
                result.DeviceId!,
                currentVersion: 0,
                ct);

            if (fetchResult.Success && fetchResult.PolicyJson is not null)
            {
                var policy = JsonSerializer.Deserialize(fetchResult.PolicyJson, PolicyJsonContext.Default.Policy);
                if (policy != null)
                {
                    await this.policyRepository.UpsertPolicyAsync(policy, ct);
                }
            }
        }
        catch (Exception ex)
        {
            // Policy fetch failed — non-fatal, pairing succeeded
            System.Diagnostics.Debug.WriteLine($"[PairingService] Policy fetch failed: {ex.Message}");
        }

        return PairingResult.SuccessResult(result.DeviceId!, result.ParentId!, result.PolicyVersion);
    }
}
