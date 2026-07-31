# Apply Progress: T24 — Emparejamiento (código / QR)

## Status: ✅ DONE

**Change**: t24-pairing-code
**Mode**: Standard
**Delivery strategy**: ask-on-risk

---

## Overview

T24 implementa la lógica de emparejamiento por código de 6 caracteres. La UI de emparejamiento (ingreso de código, QR) es responsabilidad de **T26** — el spec de T24 lo declara explícitamente: *"UI for pairing (T26)"*.

---

## Completed Tasks

### Phase 1: Domain Types
- [x] **1.1** Created `AgeBand.cs` enum + extensions (`ToString`, `TryParse`)
- [x] **1.2** Created `PairingResult.cs` (sealed record + `PairingStatus` enum + static factories)
- [x] **1.3** Created `PairingHttpResult.cs` (sealed record + `PairingHttpStatus` enum + static factories)
- [x] **1.4** Created `IPairingService.cs` interface (`PairAsync`, `IsPaired`, `GetCurrentDeviceId`)
- [x] **1.5** Added `PairAsync` + `PairingRequest` record to `IBackendClient.cs`

### Phase 2: BackendClient.PairAsync
- [x] **2.1** Implemented `BackendClient.PairAsync`: HTTP POST to `POST /functions/v1/pairing`, status code mapping, 10s timeout
- [x] **2.2** `System.Management` NuGet reference present in Service.csproj

### Phase 3: ComputerInfo Helper
- [x] **3.1** Created `ComputerInfo.cs`: `Environment.MachineName`, WMI `Win32_ComputerSystem.Model`, `Environment.OSVersion`

### Phase 4: PairingService Implementation
- [x] **4.1** Created `PairingService.cs` implementing `IPairingService`:
  - Input validation (code format, ageBand)
  - Anonymous session via `IDeviceAuthenticator`
  - Device info via `ComputerInfo.Gather()`
  - HTTP call with retry (3x, backoff 1s/2s/4s)
  - Status → result mapping (Success/InvalidCode/ExpiredCode/Error)
  - `HandleSuccessAsync`: persists device_id + parent_id to SecretStore; fetches initial policy
  - `IsPaired` / `GetCurrentDeviceId()` from SecretStore
- [x] **4.2** Registered `IPairingService` in `Program.cs` DI

### Phase 5: Unit Tests
- [x] **5.1** Created `PairingServiceTests.cs` (12 tests):
  - `PairAsync_ValidCode_ReturnsSuccess_AndPersistsDeviceId`
  - `PairAsync_InvalidCode_ReturnsInvalidCode`
  - `PairAsync_ExpiredCode_ReturnsExpiredCode`
  - `PairAsync_NetworkError_Retries3Times_ThenReturnsError`
  - `PairAsync_ServerError_Retries3Times_ThenReturnsError`
  - `PairAsync_SecretStoreWriteFails_ReturnsError`
  - `PairAsync_CodeNot6Chars_ReturnsError`
  - `PairAsync_SessionFails_ReturnsError`
  - `PairAsync_PolicyVersionFetched_OnSuccess`
  - `IsPaired_WhenDeviceIdExists_ReturnsTrue`
  - `IsPaired_WhenDeviceIdMissing_ReturnsFalse`
  - `GetCurrentDeviceId_ReturnsStoredValue`
- [x] **5.2** Added `PairingHttpResult` parsing tests in `BackendClientTests.cs`

---

## Files Changed

| File | Action |
|------|--------|
| `src/ControlParental.Domain/AgeBand.cs` | Created |
| `src/ControlParental.Domain/PairingResult.cs` | Created |
| `src/ControlParental.Domain/PairingHttpResult.cs` | Created |
| `src/ControlParental.Domain/IPairingService.cs` | Created |
| `src/ControlParental.Domain/IBackendClient.cs` | Modified: added `PairAsync` + `PairingRequest` |
| `src/ControlParental.Service/BackendClient.cs` | Modified: implemented `PairAsync` |
| `src/ControlParental.Service/ComputerInfo.cs` | Created |
| `src/ControlParental.Service/PairingService.cs` | Created |
| `src/ControlParental.Service/Program.cs` | Modified: registered `IPairingService` |
| `tests/ControlParental.Service.Tests/PairingServiceTests.cs` | Created: 12 tests |
| `tests/ControlParental.Service.Tests/BackendClientTests.cs` | Modified: added pairing tests |

---

## Done Criteria Verification

| Backlog criterion | Status |
|-----------------|--------|
| ☑ Empareja por código | PairAsync valid code → Success + device_id persisted |
| ☑ Persiste sesión | SecretStore: device_id + parent_id written |
| ☑ Maneja errores | InvalidCode (404), ExpiredCode (410), RateLimit (429), ServerError (5xx), NetworkError |
| ☑ Retry con backoff | 3 retries, delays 1s/2s/4s for 5xx/timeout |
| ☑ DoD-G | dotnet build ✅ · dotnet test: 12 PairingService tests pass ✅ |

---

## Remaining Work

### T26 — Pairing UI
`ExecutePairingStepAsync` currently marks the step complete without calling `PairingService`. This is the correct behavior until T26 builds the pairing UI (text input for code, age band selection, QR scanning in future). T26 will replace this skeleton with:
1. UI: code entry + age band selector
2. Wire `PairingService.PairAsync(code, ageBand)` to the "Emparejar" button
3. Handle `PairingResult` (show error messages, advance step on success)

---

## Workload / PR Boundary

- **Mode**: Single PR
- **Boundary**: T24 complete. All backlog criteria met. Remaining work is T26 (pairing UI).
