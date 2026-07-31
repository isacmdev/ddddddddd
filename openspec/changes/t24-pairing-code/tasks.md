# Tasks: T24 — Emparejamiento (código)

## Status: tasks

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 350–500 |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |

---

## Phase 1: Domain Types

**Goal**: Create DTOs and interfaces needed for pairing.

### Tasks

- [x] **1.1** Create `src/ControlParental.Domain/AgeBand.cs`:
  ```csharp
  public enum AgeBand { Child = 0, Preteen = 1, Teen = 2 }
  public static class AgeBandExtensions { ToString(AgeBand) -> "7-12"|"13-16"|"17-18"; TryParse(string) -> AgeBand }
  ```

- [x] **1.2** Create `src/ControlParental.Domain/PairingResult.cs`:
  ```csharp
  public sealed record PairingResult(bool Success, PairingStatus Status, string? DeviceId, string? ParentId, int PolicyVersion, string? ErrorMessage);
  public enum PairingStatus { NotPaired, Success, InvalidCode, ExpiredCode, Error }
  public static class PairingResult { NotPaired(), Success(...), InvalidCode(), ExpiredCode(), Error(message) }
  ```

- [x] **1.3** Create `src/ControlParental.Domain/PairingHttpResult.cs`:
  ```csharp
  public sealed record PairingHttpResult(bool Success, PairingHttpStatus Status, string? DeviceId, string? ParentId, int PolicyVersion, string? ErrorMessage);
  public enum PairingHttpStatus { Success, NotFound, Gone, TooManyRequests, ServerError, NetworkError }
  public static class PairingHttpResult { Success(...), NotFound(), Gone(), TooManyRequests(), ServerError(...), NetworkError(...) }
  ```

- [x] **1.4** Create `src/ControlParental.Domain/IPairingService.cs`:
  ```csharp
  public interface IPairingService
  {
      Task<PairingResult> PairAsync(string code, AgeBand ageBand, CancellationToken ct = default);
      bool IsPaired { get; }
      string? GetCurrentDeviceId();
  }
  ```

- [x] **1.5** Add to `src/ControlParental.Domain/IBackendClient.cs`:
  ```csharp
  Task<PairingHttpResult> PairAsync(PairingRequest request, CancellationToken ct = default);
  ```

- [x] **1.6** Add `PairingRequest` record to `IBackendClient.cs`:
  ```csharp
  public sealed record PairingRequest(string Code, string DeviceName, string DeviceModel, string OsVersion, string AppVersion, string AgeBand);
  ```

**Files affected (new):**
- `src/ControlParental.Domain/AgeBand.cs`
- `src/ControlParental.Domain/PairingResult.cs`
- `src/ControlParental.Domain/PairingHttpResult.cs`
- `src/ControlParental.Domain/IPairingService.cs`

**Files affected (modify):**
- `src/ControlParental.Domain/IBackendClient.cs`

**Dependencies**: None.

---

## Phase 2: BackendClient.PairAsync

**Goal**: Implement HTTP call to `POST /functions/v1/pairing`.

### Tasks

- [x] **2.1** Implement `BackendClient.PairAsync` in `src/ControlParental.Service/BackendClient.cs`:
  1. Build JSON payload from `PairingRequest`
  2. `HttpClient.PostAsJsonAsync("functions/v1/pairing", payload)`
  3. Parse response `PairingSuccessResponse { device_id, parent_id, policy_version }`
  4. Map HTTP status to `PairingHttpResult`:
     - 200 → `Success(deviceId, parentId, policyVersion)`
     - 404 → `NotFound()`
     - 410 → `Gone()`
     - 429 → `TooManyRequests()`
     - 5xx → `ServerError()`
     - exception → `NetworkError()`
  5. Timeout: 10 seconds

- [x] **2.2** Add `System.Management` NuGet reference to `ControlParental.Service.csproj` if not present:
  ```xml
  <PackageReference Include="System.Management" Version="9.0.0" />
  ```

**Files affected (modify):**
- `src/ControlParental.Service/BackendClient.cs`
- `src/ControlParental.Service/ControlParental.Service.csproj`

**Dependencies**: Phase 1 (needs `PairingRequest` and `PairingHttpResult` types).

---

## Phase 3: ComputerInfo Helper

**Goal**: Gather device name and model via WMI.

### Tasks

- [x] **3.1** Create `src/ControlParental.Service/ComputerInfo.cs`:
  ```csharp
  public static class ComputerInfo
  {
      public static DeviceInfoResult Gather()
      {
          // device_name = Environment.MachineName
          // device_model = WMI Win32_ComputerSystem.Model (or "Unknown" on failure)
          // os_version = Environment.OSVersion.ToString()
      }
  }
  public sealed record DeviceInfoResult(string DeviceName, string DeviceModel, string OsVersion);
  ```

**Files affected (new):**
- `src/ControlParental.Service/ComputerInfo.cs`

**Dependencies**: None.

---

## Phase 4: PairingService Implementation

**Goal**: Implement `IPairingService` orchestrator.

### Tasks

- [x] **4.1** Create `src/ControlParental.Service/PairingService.cs` implementing `IPairingService`:
  1. Constructor: inject `IDeviceAuthenticator`, `IBackendClient`, `ISecretStore`, `IPolicyRepository`, `ITimeProvider`
  2. `PairAsync`: validate code (6 chars, alphanumeric, trimmed/uppercase), validate ageBand
  3. Create anonymous session via `DeviceAuthenticator.CreateAnonymousSessionAsync()`
  4. Gather device info via `ComputerInfo.Gather()`
  5. Build `PairingRequest` and call `BackendClient.PairAsync` with retry (3x, delays 1s/2s/4s)
  6. Map `PairingHttpResult.Status` to `PairingResult`:
     - `Success` → `HandleSuccessAsync`
     - `NotFound` → `InvalidCode()`
     - `Gone` → `ExpiredCode()`
     - `TooManyRequests` → `Error("Demasiados intentos...")`
     - `ServerError` → `Error("Error del servidor...")`
     - `NetworkError` → `Error("No se pudo conectar...")`
  7. `HandleSuccessAsync`: write device_id + parent_id to SecretStore; call `FetchPolicyAsync` if version >= 1; return `Success`
  8. `IsPaired`: check SecretStore for "device_id"
  9. `GetCurrentDeviceId()`: read from SecretStore

- [x] **4.2** Register `IPairingService` in `src/ControlParental.Service/Program.cs`:
  ```csharp
  builder.Services.AddScoped<IPairingService, PairingService>();
  ```

**Files affected (new):**
- `src/ControlParental.Service/PairingService.cs`

**Files affected (modify):**
- `src/ControlParental.Service/Program.cs`

**Dependencies**: Phase 1 + Phase 2 + Phase 3.

---

## Phase 5: Unit Tests

**Goal**: Test all pairing scenarios.

### Tasks

- [x] **5.1** Create `tests/ControlParental.Service.Tests/PairingServiceTests.cs`:
  - `PairAsync_ValidCode_ReturnsSuccess_AndPersistsDeviceId` — mock backend success, verify SecretStore.WriteAsync called twice
  - `PairAsync_InvalidCode_ReturnsInvalidCode` — mock backend 404, verify no SecretStore write
  - `PairAsync_ExpiredCode_ReturnsExpiredCode` — mock backend 410, verify no SecretStore write
  - `PairAsync_NetworkError_Retries3Times_ThenReturnsError` — mock throws 3x, verify 3 backend calls
  - `PairAsync_ServerError_Retries3Times_ThenReturnsError` — mock returns 500 3x
  - `PairAsync_SecretStoreWriteFails_ReturnsError` — mock SecretStore throws
  - `PairAsync_CodeNot6Chars_ReturnsError` — input validation
  - `PairAsync_SessionFails_ReturnsError` — DeviceAuthenticator fails
  - `PairAsync_PolicyVersionFetched_OnSuccess` — verify FetchPolicyAsync called
  - `IsPaired_WhenDeviceIdExists_ReturnsTrue`
  - `IsPaired_WhenDeviceIdMissing_ReturnsFalse`
  - `GetCurrentDeviceId_ReturnsStoredValue`

- [x] **5.2** Add `PairingHttpResult` tests in `BackendClientTests.cs`:
  - Test JSON parsing of success response
  - Test status code mapping

**Files affected (new):**
- `tests/ControlParental.Service.Tests/PairingServiceTests.cs`

**Files affected (modify):**
- `tests/ControlParental.Service.Tests/BackendClientTests.cs`

**Dependencies**: Phase 4 complete.

---

## Summary

| Phase | Tasks | Blocked? |
|-------|-------|----------|
| Phase 1: Domain types | 6 | No |
| Phase 2: BackendClient.PairAsync | 2 | Phase 1 |
| Phase 3: ComputerInfo | 1 | No |
| Phase 4: PairingService | 2 | Phase 1 + 2 + 3 |
| Phase 5: Tests | 2 | Phase 4 |
| **Total** | **13** | |
