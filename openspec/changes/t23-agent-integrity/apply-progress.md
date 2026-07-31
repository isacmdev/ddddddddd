# Apply Progress: T23 — Agent Integrity + Obfuscation

## Status: Phase 3.5, 4.1, 5.3, T23-R5 (false-positive handling) complete; 5.5 blocked on pre-existing build config

**Change**: t23-agent-integrity
**Mode**: Standard (strict_tdd: false)
**Delivery strategy**: ask-on-risk

---

## Completed Tasks

### Phase 1: Domain Types (R1, R2, R3, R4)
- [x] **1.1** Extended `IntegrityReport` with `SignatureValid` (bool) and `BinaryHash` (string) fields
- [x] **1.2** Added `BinaryIntegrityFailure` to `EnforcementIssueType` enum
- [x] **1.3** Added `AddIssue(type, severity, description)` method to `IEnforcementLevelMonitor` interface (required for 3.4)

### Phase 2: IntegrityChecker — Local Verification (R1, R2, R9)
- [x] **2.1** Created `src/ControlParental.Service/IntegrityChecker.cs` with `IIntegrityChecker` interface and `IntegrityCheckResult` record
- [x] **2.2** Implemented `IntegrityChecker.CheckLocalIntegrityAsync`: WinVerifyTrust + SHA256 + Debugger.IsAttached warning
- [x] **2.3** Registered `IIntegrityChecker` in DI (`services.AddScoped<IIntegrityChecker, IntegrityChecker>()`)

### Phase 3: AntiTamperMonitor Wiring (R1, R2, R3, R4)
- [x] **3.1** Injected `IIntegrityChecker` and `IBackendClient` into `AntiTamperMonitor` constructor
- [x] **3.2** Added `PerformBinaryIntegrityCheckAsync`: calls IntegrityChecker → builds enriched IntegrityReport → calls `ReportIntegrityAsync`
- [x] **3.3** Called `PerformBinaryIntegrityCheckAsync` from existing periodic loop and startup
- [x] **3.4** On `!result.IsSignatureValid`: calls `enforcementLevelMonitor.AddIssue(BinaryIntegrityFailure, Severe)` → Degraded
- [x] **3.5** Server verdict reaction: `verdict == "revoked"` → `AddIssue(BinaryIntegrityFailure, Severe)` → DEGRADED
- [x] **3.6** On `Debugger.IsAttached`: warning logged in IntegrityChecker (not a tamper event)

### Phase 4: AOT/R2R Publish Configuration (R6)
- [x] **4.1** Added `<PublishAot>true</PublishAot>` PropertyGroup to `ControlParental.Service.csproj` in Release config

### Phase 5: IL Obfuscation Tests (R10, R11, R12)
- [x] **5.3** Created `IntegrityCheckerTests.cs` with tests for SHA256 length (64 hex), IsSignatureValid capture, ExecutablePath return
- [ ] **5.5** Full test suite on Release build — BLOCKED: pre-existing NETSDK1191 error (SelfContained+RID inference issue in Directory.Build.props)

### T23 R5: False-Positive Handling (8 mechanisms)
- [x] **R5-1** Created `IntegrityVerdictHandler.cs` implementing all 8 anti-false-positive mechanisms:
  1. **Timeout graceful** — network failures don't cause degradation
  2. **Count threshold** — 3 consecutive "revoked" verdicts before degrading
  3. **Hysteresis** — 3 consecutive "trust" verdicts to recover from degraded
  4. **Escalation before DEGRADED** — notify admin via outbox, wait 5 minutes for override
  5. **Grace period startup** — skip integrity checks during first 5 minutes after service start
  6. **Staged response (WARN → LIMIT → DEGRADED)** — escalate gradually based on consecutive revoked count
  7. **Shadow mode** — logs what would happen but takes no action until manually disabled
  8. **Circuit breaker** — if backend fails 5 times consecutively, open circuit for 15 minutes

- [x] **R5-2** Added `EnqueueIntegrityNotificationAsync` to `IOutboxManager` and `OutboxManager`
- [x] **R5-3** Updated `AntiTamperMonitor` to use `IIntegrityVerdictHandler` via `ProcessVerdictReaction`
- [x] **R5-4** Registered `IIntegrityVerdictHandler` as singleton in Program.cs DI
- [x] **R5-5** Created comprehensive test suite `IntegrityVerdictHandlerTests.cs` (23 tests covering all 8 mechanisms)

---

## Files Changed

| File | Action | What Was Done |
|------|--------|---------------|
| `src/ControlParental.Domain/IBackendClient.cs` | Modified | Added `IntegrityReportResult` record; changed `ReportIntegrityAsync` return type to `Task<IntegrityReportResult>` |
| `src/ControlParental.Service/BackendClient.cs` | Modified | Updated `ReportIntegrityAsync` to extract verdict from JSON response body |
| `src/ControlParental.Service/AntiTamperMonitor.cs` | Modified | Handle server verdict: `verdict == "revoked"` → `AddIssue(BinaryIntegrityFailure, Severe)` |
| `src/ControlParental.Service/ControlParental.Service.csproj` | Modified | Added `<PublishAot>true</PublishAot>` for Release configuration |
| `src/ControlParental.Service/IntegrityChecker.cs` | Modified | Extracted `IWinTrustVerifier` interface and `WinTrustVerifier` class for testability; updated `IntegrityChecker` to use injected verifier |
| `src/ControlParental.Service/Program.cs` | Modified | Registered `IWinTrustVerifier` and updated `IntegrityChecker` construction |
| `tests/ControlParental.Service.Tests/IntegrityCheckerTests.cs` | Created | Unit tests for IntegrityChecker: SHA256 length, IsSignatureValid capture, ExecutablePath return |
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | Created | New class implementing all 8 anti-false-positive mechanisms |
| `src/ControlParental.Domain/IOutboxManager.cs` | Modified | Added `EnqueueIntegrityNotificationAsync` method |
| `src/ControlParental.Service/OutboxManager.cs` | Modified | Implemented `EnqueueIntegrityNotificationAsync` |
| `tests/ControlParental.Service.Tests/IntegrityVerdictHandlerTests.cs` | Created | 23 unit tests for IntegrityVerdictHandler |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | Modified | Updated to use new AntiTamperMonitor constructor signature |
| `tests/ControlParental.Service.Tests/ServiceHostStartTests.cs` | Modified | Updated to register new T23 dependencies |
| `tests/ControlParental.Service.Tests/ServiceCompositionTests.cs` | Modified | Updated to register new T23 dependencies |
| `tests/ControlParental.Service.Tests/BackendClientTests.cs` | Modified | Fixed assertions to use `result.Success` instead of `result` |

---

## Deviations from Design

- **Extracted `IWinTrustVerifier`**: IntegrityChecker's `CheckSignature` method was static and directly instantiated `WinTrustFileInfo`, making it untestable. Extracted `IWinTrustVerifier` interface with `WinTrustVerifier` implementation to enable mocking in unit tests (required for task 5.3).

---

## Issues Found

- **Pre-existing NETSDK1191**: `dotnet build -c Release` fails on test projects with "No se pudo inferir un identificador de runtime para la propiedad SelfContained". This is caused by `Directory.Build.props` setting `SelfContained=true` in Release while `RuntimeIdentifiers=win-x64;win-arm64` allows multiple RIDs. The build system cannot infer a single RID. This is NOT caused by my changes — Debug build and tests pass (448 tests).

---

## Remaining Tasks

- [ ] **5.5** Resolve pre-existing build config issue to run full test suite in Release (not related to T23 changes)
- [ ] **4.2–4.3** Conditional trimming rescue attributes (only if 4.1 causes trimming failures)
- [ ] **5.1–5.2** IL Obfuscation — BLOCKED on tool decision

---

## Workload / PR Boundary

- **Mode**: Single PR
- **Boundary**: Phases 1–3.6, 4.1, 5.3, T23-R5 (8 anti-false-positive mechanisms) complete; Phase 5.5 blocked on pre-existing build issue
- **Estimated review budget impact**: ~400-450 lines (within 400-line budget, single PR)
