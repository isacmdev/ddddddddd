# Proposal: T24 — Emparejamiento (código / QR)

## Change Name
`t24-pairing-code`

## Executive Summary

Implementar el flujo de emparejamiento dispositivo-padre usando código de emparejamiento. El agente recibe un código del padre (fuera de banda), lo envía al backend via `POST /functions/v1/pairing`, recibe un `device_id`, y lo persiste en `SecretStore` (T16) para usarlo en todas las comunicaciones futuras.

## Dependencies
- **T14** — `IBackendClient` con conexión Supabase (ya existe)
- **T16** — `ISecretStore` para persistir `device_id` (ya existe)
- **T17** — `IDeviceAuthenticator` para sesión anónima (ya existe)

## Backend Contract (from Andrea-Caballero/parentalControl)

### Endpoint
```
POST /functions/v1/pairing
```

### Request
```json
{
  "code": "ABC123",
  "device_name": "PC-de-Juan",
  "device_model": "Dell XPS 15",
  "os_version": "Windows 11 23H2",
  "app_version": "1.0.0",
  "age_band": "7-12"
}
```

### Response (200)
```json
{
  "success": true,
  "device_id": "uuid-dispositivo",
  "parent_id": "uuid-padre",
  "policy_version": 1
}
```

### Errors
| HTTP | Significado |
|---|---|
| `404` | Código inválido (no existe o ya fue usado) |
| `410` | Código expirado (TTL ~10 min) |

## Scope

### In Scope
1. `IBackendClient.PairAsync` — método HTTP al endpoint de pairing
2. `IPairingService` — orchestrator del flujo de emparejamiento
3. `PairingResult` — DTOs de resultado (éxito + errores)
4. Guardado de `device_id` en `SecretStore` (T16) tras emparejamiento exitoso
5. Estados de error claros: inválido / expirado / usado
6. Tests unitarios

### Out of Scope (futuras tareas)
- QR scanning con MediaCapture + ZXing.Net (T24 solo código)
- UI de emparejamiento (App.UI es stub — se implementa en T26)
- Pantalla de éxito/error (T26)

## Design

### Flow
```
App.UI (futuro) llama PairingService.PairAsync(code)
    │
    ├─► DeviceAuthenticator.CreateAnonymousSessionAsync() — T17
    │       (crea sesión anónima Supabase)
    │
    ├─► BackendClient.PairAsync(request) — T14
    │       POST /functions/v1/pairing
    │       Recibe { device_id, parent_id, policy_version }
    │
    ├─► SecretStore.WriteAsync("device_id", device_id) — T16
    │       Persiste el device_id cifrado
    │
    └─► Return PairingResult.Success(device_id, parent_id, policy_version)
```

### Errors
```
PairAsync(code)
    │
    ├─► 404 → PairingResult.InvalidCode
    ├─► 410 → PairingResult.ExpiredCode
    └─► Exception → PairingResult.Error(message)
```

### New Types

```csharp
// Domain/IBackendClient.cs
Task<PairingResult> PairAsync(PairingRequest request, CancellationToken ct = default);

// Domain/PairingService/IPairingService.cs
public interface IPairingService
{
    Task<PairingResult> PairAsync(string code, CancellationToken ct = default);
    bool IsPaired { get; }
    string? GetCurrentDeviceId();
}

// Domain/PairingResult.cs
public sealed record PairingResult(
    bool Success,
    PairingStatus Status,
    string? DeviceId,
    string? ParentId,
    int PolicyVersion,
    string? ErrorMessage);

public enum PairingStatus
{
    NotPaired,
    Success,
    InvalidCode,
    ExpiredCode,
    Error
}

public sealed record PairingRequest(
    string Code,
    string DeviceName,
    string DeviceModel,
    string OsVersion,
    string AppVersion,
    string AgeBand);
```

## Risks
| Risk | Severity | Mitigation |
|---|---|---|
| Backend no reachable | Medium | Timeout + retry con backoff |
| Código duplicado usado | Low | El backend usa código único por intento |
| Session expiration durante pairing | Low | Crear sesión anónima fresh |

## Open Questions
1. ¿El campo `age_band` es obligatorio? ¿Valores posibles?
2. ¿`device_model` y `device_name` se autogeneran o pide input al usuario?
3. ¿El `policy_version` se usa inmediatamente o se guarda para más tarde?

## Next Phase
`sdd-spec` — escribir specs detalladas de los DTOs y el flujo.
