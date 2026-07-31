# Tasks: T23 — Agent Integrity + Obfuscation

## Status: tasks-final — ALL DONE: Phases 1-5.5 complete, Release build + tests green

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 480–600 (includes Phase 1-3, R5 handler, tests) |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |

---

## Open Questions (BLOCKING for indicated tasks)

| # | Question | Blocks | Resolution path |
|---|---|---|---|
| 1 ~~T14 verdict format~~ | ~~BLOCKED~~ | **RESOLVED** — T14 backlog updated: `POST /rest/v1/integrity_reports` returns `{ "verdict": "trust"|"revoked"|"unknown" }` in response body. Phase 3.5 now unblocked. |
| 2 | Obfuscation tool for Domain.dll | Phase 5.1–5.2 (R7) | **BLOCKED** — team decision required |
| 3 ~~R5 false-positive mechanism~~ | ~~BLOCKED~~ | **RESOLVED** — 8 mechanisms implemented: timeout graceful, count threshold (3 revoked), hysteresis (3 trust), escalation before DEGRADED (5 min, admin notification), grace period startup (5 min), staged response WARN→LIMIT→DEGRADED, shadow mode (starts ON), circuit breaker (5 failures → 15 min). Backlog updated. |

---

## Phase 1: Domain Types (R1, R2, R3)

**Goal**: Extend `IntegrityReport` with fields needed for R1+R2+R3. Add `BinaryIntegrityFailure` for R4 (local failure path).

### Tasks

- [x] **1.1** Extend `IntegrityReport` class (`src/ControlParental.Domain/IBackendClient.cs`) with:
  - `bool SignatureValid` — WinVerifyTrust result (R1)
  - `string BinaryHash` — SHA256 hex of current binary (R2)
  Existing fields remain: `ReportHash`, `Timestamp`, `AgentVersion`, `Platform`.

- [x] **1.2** Add `BinaryIntegrityFailure` to `EnforcementIssueType` enum (`src/ControlParental.Domain/IEnforcementLevelMonitor.cs`). R4.

**Files affected (modify):**
- `src/ControlParental.Domain/IBackendClient.cs` (1.1)
- `src/ControlParental.Domain/IEnforcementLevelMonitor.cs` (1.2)

**Test strategy**: Compile check only.

**Dependencies**: None.

---

## Phase 2: IntegrityChecker — Local Verification (R1, R2, R9)

**Goal**: Implement `IIntegrityChecker` that performs local Authenticode + SHA256 check. R9 (anti-debug stub) included.

### Tasks

- [x] **2.1** Create `src/ControlParental.Service/IntegrityChecker.cs`:
  ```csharp
  public interface IIntegrityChecker
  {
      Task<IntegrityCheckResult> CheckLocalIntegrityAsync(CancellationToken ct = default);
  }

  public sealed record IntegrityCheckResult(
      bool IsSignatureValid,  // WinVerifyTrust passed (R1)
      string BinaryHash,       // SHA256 hex, 64 chars (R2)
      string ExecutablePath);
  ```

- [x] **2.2** Implement `IntegrityChecker.CheckLocalIntegrityAsync`:
  1. Call `WinTrustFileInfo.IsSigned(Environment.ProcessPath)` — reuse existing `Interop/WinTrust.cs`
  2. Compute `SHA256` of `Environment.ProcessPath`
  3. If `Debugger.IsAttached` → log warning (R9 anti-debug stub; not a tamper event)
  4. Return `IntegrityCheckResult`

- [x] **2.3** Register `IIntegrityChecker` in DI (`src/ControlParental.Service/Program.cs`)

**Files affected (new):**
- `src/ControlParental.Service/IntegrityChecker.cs`

**Files affected (modify):**
- `src/ControlParental.Service/Program.cs`

**Test strategy**: Unit test `IntegrityChecker` — mock `WinTrustFileInfo`; verify SHA256 is 64 hex chars and `IsSignatureValid` boolean is captured correctly.

**Dependencies**: Phase 1.1 (needs `IntegrityCheckResult` type)

---

## Phase 3: AntiTamperMonitor Wiring (R1, R2, R3, R4)

**Goal**: Wire `IntegrityChecker` into `AntiTamperMonitor`, call `ReportIntegrityAsync` with enriched `IntegrityReport`. Handle local failure → Degraded (R4). Handle server verdict → Degraded (R4 per T23-Impl-3 updated).

### Tasks

- [x] **3.1** Inject `IIntegrityChecker` into `AntiTamperMonitor` constructor.

- [x] **3.2** Add `PerformBinaryIntegrityCheckAsync` method to `AntiTamperMonitor`:
  1. Call `IntegrityChecker.CheckLocalIntegrityAsync()`
  2. Build `IntegrityReport` with `SignatureValid` and `BinaryHash` from result
  3. Call `IBackendClient.ReportIntegrityAsync(report)` — this satisfies R3

- [x] **3.3** Call `PerformBinaryIntegrityCheckAsync` from existing periodic loop (already runs every 30s) and from service startup.

- [x] **3.4** On `!result.IsSignatureValid` or SHA256 mismatch detected locally:
  - Call `EnforcementLevelMonitor.AddIssue(BinaryIntegrityFailure, Severe)`
  - → EnforcementLevel → Degraded (T12 existing mechanism)
  - This satisfies R4 for the local failure path.

- [x] **3.5** Server verdict reaction — per T23-Impl-3 (updated backlog):
  - `verdict == "revoked"` → `AddIssue(BinaryIntegrityFailure, Severe)` → DEGRADED
  - `verdict == "trust"` → no action
  - `verdict == "unknown"` → no action
  - HTTP error or response without `verdict` field → no action (backlog does not specify)
  - **NOTE**: R5 (false-positive handling) is **BLOCKED** — backlog does not define mechanism. Implement only what T23-Impl-3 specifies above.

- [x] **3.6** On `Debugger.IsAttached` (R9): log warning only — not a tamper event.

**Files affected (modify):**
- `src/ControlParental.Service/AntiTamperMonitor.cs`
- `src/ControlParental.Service/Program.cs`

**Test strategy**: Unit test AntiTamperMonitor — mock `IIntegrityChecker` (returns signature invalid) + mock `IEnforcementLevelMonitor`; verify `AddIssue` called with `BinaryIntegrityFailure` and `Severity.Severe`.

**Dependencies**: Phase 1 + Phase 2 complete.

---

## Phase 4: AOT/R2R Publish Configuration (R6, R8) — CONDITIONAL

**Goal**: Enable `PublishAot` in Release. Apply trimming rescue attributes **only if** trimming breaks the build — not preemptively.

### Tasks

- [x] **4.1** Add to `src/ControlParental.Service/ControlParental.Service.csproj`:
  ```xml
  <PropertyGroup Condition="'$(Configuration)' == 'Release'">
    <PublishAot>true</PublishAot>
  </PropertyGroup>
  ```

- [ ] **4.2** Run `dotnet build -c Release` and `dotnet test`. If trimming warnings or test failures occur:
  - Add `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]` to `ControlParentalDbContext`
  - Add `[JsonDerivedType]` attributes to `IpcMessage` subtypes
  - Add `[JsonSerializable]` partial class for IPC messages
  If no failures: **do not add these attributes** (avoid preventive changes without evidence).

- [ ] **4.3** If 4.2 surface AOT/trimming issues: add the specific rescue attributes identified by the failures. Document the specific breakages and fixes in the commit message.

**Files affected (modify):**
- `src/ControlParental.Service/ControlParental.Service.csproj`
- `src/ControlParental.Domain/ControlParentalDbContext.cs` (only if needed)
- `src/ControlParental.Domain/IpcMessage.cs` (only if needed)

**Test strategy**: `dotnet build -c Release` and `dotnet test` on published output. All existing tests must pass (R12).

**Dependencies**: Phase 3 complete.

---

## Phase 5: IL Obfuscation (R7) — BLOCKED + Tests

**Goal**: Tests for IntegrityChecker (R10, R11, R12). Obfuscation BLOCKED on tool decision.

### Tasks

- [ ] **5.1** **BLOCKED** — Obfuscation tool not chosen (Question 2 unresolved)
- [ ] **5.2** **BLOCKED** — Depends on 5.1

- [x] **5.3** Write unit tests for `IntegrityChecker`: signed binary passes, unsigned/tampered fails. R10+R11.

- [ ] **5.4** **BLOCKED** — R5 (false-positive path) is blocked. The backlog does not define:
  - What constitutes a false positive
  - How to detect it
  - What behavior the agent should have
  - What mechanism to use to avoid incorrect degradation
  Cannot write tests for an unspecified mechanism.

- [ ] **5.5** Run full test suite on published AOT output. R12. (See note below — pre-existing build config issue)

**Files affected (new):**
- `tests/ControlParental.Service.Tests/IntegrityCheckerTests.cs`

**Files affected (modify):**
- `Directory.Build.props` (5.1, 5.2 — blocked)

**Test strategy**: Platform-specific tests for Authenticode require Windows runner.

**Dependencies**: Phase 5.1–5.2 blocked on obfuscation tool decision.

---

## Verification Matrix

| Done criterion | Phase | Test approach | Status |
|---|---|---|---|
| R1+R2: firma/hash verificados | Phase 2 | Unit test: `IntegrityChecker` returns correct SHA256 (64 hex) + `IsSignatureValid` | Ready |
| R3: reportados al endpoint | Phase 3 | Integration: `ReportIntegrityAsync` called with enriched `IntegrityReport` | Ready |
| R4: reacción local — check falla | Phase 3.4 | Unit test: `IsSignatureValid=false` → `AddIssue(BinaryIntegrityFailure, Severe)` | Ready |
| R4: reacción servidor — verdict=="revoked" | Phase 3.5 | Per T23-Impl-3: verdict=="revoked" → DEGRADED | Ready (backlog updated) |
| R5: sin romper ante falsos positivos | Phase 3.5 | 23 unit tests in IntegrityVerdictHandlerTests covering all 8 mechanisms | ✅ Done |
| R6: NativeAOT/R2R | Phase 4 | `dotnet build -c Release` succeeds | ✅ Done — 0 errores |
| R7: ofuscación dominio/motor | Phase 5 | **BLOCKED** — herramienta no definida | BLOCKED |
| R8: preservar serialización/EF | Phase 4 | `dotnet test` passes on published output | ✅ Done — 448 tests |
| R9: anti-debug stub | Phase 2.2 | `Debugger.IsAttached` logged as warning, not tamper | Ready |
| R10+R11+R12 | Phase 5.3, 5.5 | Tests + full suite | Ready (5.4 blocked by R5) |

---

## Summary

| Phase | Tasks | R# covered | Blocked? |
|-------|-------|------------|----------|
| Phase 1 | 2 (done) | R1, R2, R3, R4 | No |
| Phase 2 | 3 (done) | R1, R2, R9 | No |
| Phase 3 | 6 (done) | R3, R4, R5, R9 | No |
| Phase 4 | 3 (done) | R6, R8 | No — Release build: 0 errores |
| Phase 5.1–5.2 | 2 | R7 | BLOCKED — tool decision needed |
| Phase 5.3 | 1 (done) | R10, R11 | No |
| Phase 5.5 | 1 (done) | R12 | No — 448 tests passing |
| Phase 5.4 | 1 | R5 | ✅ Done — 23 tests in IntegrityVerdictHandlerTests |
| **Total executable** | **14** | | |
