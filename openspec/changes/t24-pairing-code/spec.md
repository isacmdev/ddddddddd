# Spec: T24 — Emparejamiento (código / QR)

## Status: spec

## Overview

T24 implementa el flujo de emparejamiento dispositivo-padre. El agente recibe un código de emparejamiento de 6 caracteres del padre (vía fuera de banda, ej: email, WhatsApp, escrito a mano), lo envía al backend `POST /functions/v1/pairing`, recibe un `device_id` único, y lo persiste en `SecretStore` (T16). Tras el pairing exitoso, el agente inmediatamente descarga la política via `FetchPolicyAsync` y queda operativo.

## Backend Contract

### Endpoint
```
POST /functions/v1/pairing
Authorization: Bearer <anonymous_jwt>  (session from T17)
```

### Request
```json
{
  "code": "ABC123",          // 6-char alphanumeric, uppercase
  "device_name": "PC-JUAN",  // Environment.MachineName
  "device_model": "Dell XPS 15",  // WMI Win32_ComputerSystem.Model
  "os_version": "Windows 11 23H2",  // Environment.OSVersion
  "app_version": "1.0.0",    // from assembly
  "age_band": "7-12"         // enum: "7-12" | "13-16" | "17-18"
}
```

### Response 200 OK
```json
{
  "success": true,
  "device_id": "uuid-dispositivo",
  "parent_id": "uuid-padre",
  "policy_version": 1
}
```

### Response Errors
| HTTP | Body | Meaning |
|---|---|---|
| 404 | any | Código inválido (no existe o ya fue usado) |
| 410 | any | Código expirado (TTL ~10 min) |
| 429 | any | Rate limit |
| 5xx | any | Server error — retry with backoff |

## Functionality

### Core Flow

```
PairAsync(code, ageBand)
│
├─ Validate inputs (code format, ageBand enum)
│
├─ Create anonymous session (T17)
│   └─ DeviceAuthenticator.CreateAnonymousSessionAsync()
│
├─ Gather device info
│   ├─ device_name = Environment.MachineName
│   ├─ device_model = WMI Win32_ComputerSystem.Model (or "Unknown")
│   ├─ os_version = Environment.OSVersion.ToString()
│   └─ app_version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0"
│
├─ Call backend POST /functions/v1/pairing
│   └─ Timeout: 10 seconds
│
├─ Handle response
│   ├─ 200 + success=true
│   │   ├─ Parse device_id, parent_id, policy_version
│   │   ├─ Save device_id to SecretStore (key = "device_id")
│   │   ├─ Save parent_id to SecretStore (key = "parent_id")
│   │   └─ Return PairingResult.Success(device_id, parent_id, policy_version)
│   │
│   ├─ 404 → PairingResult.InvalidCode()
│   ├─ 410 → PairingResult.ExpiredCode()
│   ├─ 429 → PairingResult.Error("Rate limited, retry later")
│   └─ 5xx/timeout → PairingResult.Error("Backend unavailable")
│
└─ Post-pairing (on Success only)
    ├─ If policy_version >= 1
    │   └─ FetchPolicyAsync(deviceId, currentVersion: 0)
    └─ Return result
```

### Error Handling

| Error | User Message | Behavior |
|---|---|---|
| Invalid code (404) | "El código no es válido. Verificá el código e intentá de nuevo." | No retry automático |
| Expired code (410) | "El código expiró. Pedí uno nuevo al administrador." | No retry automático |
| Rate limited (429) | "Demasiados intentos. Esperá unos minutos e intentá de nuevo." | Retry con backoff |
| Backend error (5xx) | "Error de conexión. Verificá tu internet e intentá de nuevo." | Retry con backoff 3 veces |
| Network timeout | "No se pudo conectar al servidor. Verificá tu internet." | Retry con backoff 3 veces |
| SecretStore error | "Error al guardar la configuración. Reiniciá la aplicación." | Log, no retry |

### Age Band

| Value | Constant |
|---|---|
| "7-12" | `AgeBand.Child` |
| "13-16" | `AgeBand.Preteen` |
| "17-18" | `AgeBand.Teen` |

No otros valores aceptados — si el valor no matchea, retorna error.

### Retry Policy

- **Max retries**: 3
- **Backoff**: 1s, 2s, 4s (exponential)
- **Retry on**: 5xx, timeout, network error
- **Do NOT retry on**: 404, 410, 429 (user must act)

## Data Flow

### SecretStore Keys

| Key | Value | Persisted |
|---|---|---|
| `device_id` | UUID del dispositivo asignado por el backend | ✅ Always |
| `parent_id` | UUID del padre | ✅ Always |
| `device_jwt` | JWT de la sesión anónima | ✅ T17 already does |
| `policy_version` | Última versión conocida de política | ✅ On every policy fetch |

### Policy Fetch After Pairing

On `PairingResult.Success`:
```
if (result.PolicyVersion >= 1)
    await FetchPolicyAsync(result.DeviceId, currentVersion: 0)
```

Heartbeat también recibe `policy_version` en response — si `server_version > local_version`, hacer `FetchPolicyAsync`.

## File Changes

### New Files

| File | Purpose |
|---|---|
| `src/ControlParental.Domain/IPairingService.cs` | Interface |
| `src/ControlParental.Domain/PairingResult.cs` | DTOs |
| `src/ControlParental.Domain/AgeBand.cs` | Enum |
| `src/ControlParental.Service/PairingService.cs` | Implementation |
| `src/ControlParental.Service/ComputerInfo.cs` | WMI device model helper |
| `tests/ControlParental.Service.Tests/PairingServiceTests.cs` | Unit tests |

### Modified Files

| File | Change |
|---|---|
| `src/ControlParental.Domain/IBackendClient.cs` | Add `PairAsync` method |
| `src/ControlParental.Service/BackendClient.cs` | Implement `PairAsync` |
| `src/ControlParental.Service/Program.cs` | Register `IPairingService` |

## Interface

### IPairingService
```csharp
public interface IPairingService
{
    /// <summary>
    /// Empareja el dispositivo con un padre usando un código de emparejamiento.
    /// </summary>
    /// <param name="code">Código de 6 caracteres.</param>
    /// <param name="ageBand">Rango de edad del menor.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resultado del emparejamiento.</returns>
    Task<PairingResult> PairAsync(string code, AgeBand ageBand, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el dispositivo ya está emparejado.
    /// </summary>
    bool IsPaired { get; }

    /// <summary>
    /// Obtiene el device_id actual si está emparejado.
    /// </summary>
    string? GetCurrentDeviceId();
}
```

### IBackendClient Changes
```csharp
/// <summary>
/// Empareja el dispositivo con un padre.
/// </summary>
/// <param name="request">Request de emparejamiento.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Resultado del emparejamiento.</returns>
Task<PairingHttpResult> PairAsync(PairingRequest request, CancellationToken cancellationToken = default);
```

## Verification

| Scenario | Expected |
|---|---|
| Código válido | Success + device_id persisted |
| Código inválido (404) | PairingResult.InvalidCode |
| Código expirado (410) | PairingResult.ExpiredCode |
| Timeout/retry | 3 retries with backoff, then error |
| Already paired + Pair called | Returns current device_id (idempotent) |
| Policy version received | FetchPolicyAsync called |
| SecretStore write failure | Log error, return error result |

## Dependencies

- T14: `IBackendClient` — already implemented
- T16: `ISecretStore` — already implemented
- T17: `IDeviceAuthenticator` — already implemented
- WMI: `Win32_ComputerSystem` — requires `System.Management` reference

## Out of Scope

- QR code scanning (future enhancement)
- Pairing without code (future)
- Unpairing / re-pairing flow (future — TBD)
- UI for pairing (T26)
