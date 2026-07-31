# Design: T24 — Emparejamiento (código)

## Status: designed

## Executive Summary

Implement `IPairingService` that orchestrates the pairing flow: validate inputs, create anonymous session (T17), call `POST /functions/v1/pairing` (T14), persist `device_id` + `parent_id` in `SecretStore` (T16), and trigger initial policy fetch.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    App.UI (future T26)                   │
└─────────────────────┬───────────────────────────────────┘
                      │ PairAsync(code, ageBand)
                      ▼
┌─────────────────────────────────────────────────────────┐
│                  PairingService                         │
│  - Validates code format + ageBand enum                 │
│  - Orchestrates the full flow                          │
│  - Retry policy (3x, exponential backoff)                │
│  - Error classification                                 │
└───────┬─────────────┬───────────────┬───────────────────┘
        │             │               │
        ▼             ▼               ▼
┌───────────┐  ┌────────────┐  ┌──────────────┐
│DeviceAuth │  │BackendClient│  │ SecretStore  │
│(T17)     │  │ (T14)      │  │ (T16)        │
│Anonymous │  │ PairAsync  │  │ Write device_ │
│session   │  │            │  │ id + parent_ │
└───────────┘  └────────────┘  │ id           │
                              └──────────────┘
```

## Key Interface: IPairingService

```csharp
public interface IPairingService
{
    Task<PairingResult> PairAsync(string code, AgeBand ageBand, CancellationToken ct = default);
    bool IsPaired { get; }
    string? GetCurrentDeviceId();
}
```

## AgeBand Enum

```csharp
public enum AgeBand
{
    Child   = 0,   // "7-12"
    Preteen = 1,   // "13-16"
    Teen    = 2,   // "17-18"
}

public static class AgeBandExtensions
{
    public static string ToString(this AgeBand band)
        => band switch
        {
            AgeBand.Child   => "7-12",
            AgeBand.Preteen => "13-16",
            AgeBand.Teen    => "17-18",
            _ => throw new ArgumentOutOfRangeException(nameof(band)),
        };

    public static bool TryParse(string value, out AgeBand band)
    {
        // "7-12" → Child, "13-16" → Preteen, "17-18" → Teen
        // case-insensitive, trimmed
    }
}
```

## PairingResult

```csharp
public sealed record PairingResult(
    bool Success,
    PairingStatus Status,
    string? DeviceId,
    string? ParentId,
    int PolicyVersion,
    string? ErrorMessage);

public enum PairingStatus
{
    NotPaired,   // Initial state
    Success,     // Pairing completed
    InvalidCode,  // 404 from backend
    ExpiredCode,  // 410 from backend
    Error,        // Network/server error
}

public static class PairingResult
{
    public static PairingResult NotPaired()
        => new(false, PairingStatus.NotPaired, null, null, 0, null);

    public static PairingResult Success(string deviceId, string parentId, int policyVersion)
        => new(true, PairingStatus.Success, deviceId, parentId, policyVersion, null);

    public static PairingResult InvalidCode()
        => new(false, PairingStatus.InvalidCode, null, null, 0,
               "El código no es válido. Verificá el código e intentá de nuevo.");

    public static PairingResult ExpiredCode()
        => new(false, PairingStatus.ExpiredCode, null, null, 0,
               "El código expiró. Pedí uno nuevo al administrador.");

    public static PairingResult Error(string message)
        => new(false, PairingStatus.Error, null, null, 0, message);
}
```

## PairingRequest

```csharp
public sealed record PairingRequest(
    string Code,         // 6-char alphanumeric, uppercase
    string DeviceName,   // Environment.MachineName
    string DeviceModel,  // WMI Win32_ComputerSystem.Model or "Unknown"
    string OsVersion,   // Environment.OSVersion.ToString()
    string AppVersion,   // Assembly version
    string AgeBand);    // "7-12" | "13-16" | "17-18"
```

## IBackendClient.PairAsync

```csharp
// New in IBackendClient.cs
Task<PairingHttpResult> PairAsync(PairingRequest request, CancellationToken ct = default);

// In BackendClient.cs implementation
public async Task<PairingHttpResult> PairAsync(PairingRequest request, CancellationToken ct = default)
{
    var payload = new
    {
        code = request.Code,
        device_name = request.DeviceName,
        device_model = request.DeviceModel,
        os_version = request.OsVersion,
        app_version = request.AppVersion,
        age_band = request.AgeBand,
    };

    var response = await this.httpClient.PostAsJsonAsync("functions/v1/pairing", payload, ct);

    if (response.IsSuccessStatusCode)
    {
        var json = await response.Content.ReadFromJsonAsync<PairingSuccessResponse>(ct);
        return PairingHttpResult.Success(json!.DeviceId, json.ParentId, json.PolicyVersion);
    }

    return response.StatusCode switch
    {
        HttpStatusCode.NotFound => PairingHttpResult.NotFound(),
        HttpStatusCode.Gone => PairingHttpResult.Gone(),
        HttpStatusCode.TooManyRequests => PairingHttpResult.TooManyRequests(),
        _ => PairingHttpResult.ServerError(response.StatusCode.ToString()),
    };
}
```

```csharp
public sealed record PairingHttpResult(
    bool Success,
    PairingHttpStatus Status,
    string? DeviceId,
    string? ParentId,
    int PolicyVersion,
    string? ErrorMessage);

public enum PairingHttpStatus
{
    Success,
    NotFound,       // 404
    Gone,           // 410
    TooManyRequests, // 429
    ServerError,    // 5xx
    NetworkError,   // exception
}

public static class PairingHttpResult
{
    public static PairingHttpResult Success(string deviceId, string parentId, int policyVersion)
        => new(true, PairingHttpStatus.Success, deviceId, parentId, policyVersion, null);

    public static PairingHttpResult NotFound()
        => new(false, PairingHttpStatus.NotFound, null, null, 0, null);

    public static PairingHttpResult Gone()
        => new(false, PairingHttpStatus.Gone, null, null, 0, null);

    public static PairingHttpResult TooManyRequests()
        => new(false, PairingHttpStatus.TooManyRequests, null, null, 0, null);

    public static PairingHttpResult ServerError(string details)
        => new(false, PairingHttpStatus.ServerError, null, null, 0, details);

    public static PairingHttpResult NetworkError(string message)
        => new(false, PairingHttpStatus.NetworkError, null, null, 0, message);
}
```

## PairingService Implementation

```csharp
public sealed class PairingService : IPairingService
{
    private readonly IDeviceAuthenticator deviceAuthenticator;
    private readonly IBackendClient backendClient;
    private readonly ISecretStore secretStore;
    private readonly IPolicyRepository policyRepository;
    private readonly ITimeProvider timeProvider;

    private const int MaxRetries = 3;
    private static readonly TimeSpan[] RetryDelays = [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
    ];

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

    public bool IsPaired
    {
        get
        {
            var deviceId = this.secretStore.ReadAsync("device_id").GetAwaiter().GetResult();
            return !string.IsNullOrEmpty(deviceId);
        }
    }

    public string? GetCurrentDeviceId()
        => this.secretStore.ReadAsync("device_id").GetAwaiter().GetResult();

    public async Task<PairingResult> PairAsync(string code, AgeBand ageBand, CancellationToken ct = default)
    {
        // 1. Validate inputs
        if (string.IsNullOrWhiteSpace(code) || code.Length != 6)
            return PairingResult.Error("El código debe tener 6 caracteres.");

        var normalizedCode = code.Trim().ToUpperInvariant();

        // 2. Create anonymous session (T17)
        var sessionResult = await this.deviceAuthenticator.CreateAnonymousSessionAsync(ct);
        if (!sessionResult.Success)
            return PairingResult.Error("No se pudo crear la sesión. Verificá tu conexión.");

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
        var httpResult = await this.CallPairingWithRetryAsync(request, ct);

        // 5. Handle result
        return httpResult.Status switch
        {
            PairingHttpStatus.Success => await this.HandleSuccessAsync(httpResult, ct),

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
                    return result;

                // Server error or network error — retry after delay
                if (i < MaxRetries - 1)
                    await Task.Delay(RetryDelays[i], ct);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Retry on cancellation from outside? No — only retry on actual timeout/network
                throw;
            }
            catch (HttpRequestException)
            {
                if (i < MaxRetries - 1)
                    await Task.Delay(RetryDelays[i], ct);
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
                await this.policyRepository.UpsertPolicyAsync(
                    new Policy { Version = result.PolicyVersion, RawJson = fetchResult.PolicyJson },
                    ct);
            }
        }
        catch (Exception ex)
        {
            // Policy fetch failed — non-fatal, pairing succeeded
            System.Diagnostics.Debug.WriteLine($"[PairingService] Policy fetch failed: {ex.Message}");
        }

        return PairingResult.Success(result.DeviceId!, result.ParentId!, result.PolicyVersion);
    }
}
```

## ComputerInfo

```csharp
public static class ComputerInfo
{
    public static DeviceInfoResult Gather()
    {
        string deviceModel;
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT Model FROM Win32_ComputerSystem");
            foreach (var obj in searcher.Get())
            {
                deviceModel = obj["Model"]?.ToString() ?? "Unknown";
                break;
            }
        }
        catch
        {
            deviceModel = "Unknown";
        }

        return new DeviceInfoResult(
            DeviceName: Environment.MachineName,
            DeviceModel: deviceModel,
            OsVersion: Environment.OSVersion.ToString());
    }
}

public sealed record DeviceInfoResult(
    string DeviceName,
    string DeviceModel,
    string OsVersion);
```

## File Changes

| File | Action |
|---|---|
| `src/ControlParental.Domain/AgeBand.cs` | **Create** — enum |
| `src/ControlParental.Domain/PairingResult.cs` | **Create** — result types |
| `src/ControlParental.Domain/IPairingService.cs` | **Create** — interface |
| `src/ControlParental.Domain/IBackendClient.cs` | **Modify** — add `PairAsync` |
| `src/ControlParental.Domain/BackendClient.cs` | **Modify** — implement `PairAsync` |
| `src/ControlParental.Service/ComputerInfo.cs` | **Create** — WMI helper |
| `src/ControlParental.Service/PairingService.cs` | **Create** — implementation |
| `src/ControlParental.Service/Program.cs` | **Modify** — register `IPairingService` |
| `src/ControlParental.Service/ControlParental.Service.csproj` | **Modify** — add `System.Management` reference |
| `tests/ControlParental.Service.Tests/PairingServiceTests.cs` | **Create** — unit tests |

## Dependencies

- **T14**: `IBackendClient` — adds `PairAsync`
- **T16**: `ISecretStore` — persists device_id, parent_id
- **T17**: `IDeviceAuthenticator` — anonymous session
- **System.Management**: WMI for `Win32_ComputerSystem.Model`

## Verification Matrix

| Scenario | Test approach |
|---|---|
| Valid code → success + persisted | Unit: mock backend returns success; verify SecretStore.WriteAsync called |
| Invalid code (404) → InvalidCode result | Unit: mock backend returns 404; verify no SecretStore write |
| Expired code (410) → ExpiredCode result | Unit: mock backend returns 410; verify no SecretStore write |
| Network error → retry 3x then error | Unit: mock throws; verify 3 calls + backoff delays |
| SecretStore write fails → Error result | Unit: mock SecretStore throws; verify PairingResult.Error returned |
| Already paired | Unit: mock SecretStore returns device_id; verify early return or re-pair |
| Policy version fetched after success | Unit: verify FetchPolicyAsync called with correct device_id |
