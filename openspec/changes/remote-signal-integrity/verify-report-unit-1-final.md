# Verification Report

**Change**: `remote-signal-integrity` — Unit 1 tasks 1.1–1.3 only  
**Version**: SDD7  
**Boundary**: `feat/sdd7-1-scheduler-ipc` at `0a72ddd3b3271e2844805d2b092b2574d4b703dc`, based on tracker `feat/sdd7-remote-signal-integrity` at the same commit  
**Mode**: Strict TDD, hybrid persistence, authoritative fresh independent verification  
**Verification date**: 2026-08-20 (`-05:00`)  
**Source mutation**: None. Historical reports were not changed or used as verdict evidence.

## Verdict

**PASS WITH WARNINGS**

Unit 1 is **APPROVED for its local feature-chain boundary**. All three in-scope `remote-signal-sync` scenarios have fresh passing runtime evidence, the complete scenario-2 lifecycle passes, affected builds and both full regressions are green, coverage is non-empty and meaningful, the admission state machine is directly covered, and the exact CODE+TEST budget is 388/400.

Full SDD7 remains partial at 3/14 tasks and is **not archive-ready**. Units 2–5 were not verified or implemented by this unit.

## Scope and Completeness

| Metric | Value |
|---|---:|
| Unit 1 tasks | 3 |
| Unit 1 complete | 3 |
| Unit 1 incomplete | 0 |
| Global SDD7 tasks | 14 |
| Global SDD7 complete | 3 |
| Global SDD7 incomplete | 11 |
| In-scope scenarios | 3 |
| Runtime-compliant scenarios | 3 |

Excluded: registration lifecycle scenario 4 and all Phase 2–5 WNS, Realtime, integrity, enforcement, composition, and external-receipt work.

## Fresh Workspace Inspection

- CodeGraph was attempted first against the exact linked worktree. Because no index existed, it was initialized, queried for scheduler/IPC call paths and changed symbols, then removed after inspection so no CodeGraph artifact remained in the worktree.
- Branch and HEAD were verified as `feat/sdd7-1-scheduler-ipc` / `0a72ddd3b3271e2844805d2b092b2574d4b703dc`.
- Tracker resolved to the same base commit.
- Inspected proposal, all four specs, design, tasks, apply progress, complete changed source/test diff, untracked helper, current source, test assertions, numstat, and whitespace checks.
- The two historical FAIL reports remained unchanged and were not reused for this verdict.

### Changed CODE+TEST Budget

| Component | Additions | Deletions | Total |
|---|---:|---:|---:|
| Tracked `src/` + `tests/` diff | 317 | 47 | 364 |
| Untracked test-only `RepositoryRootLocator.cs` | 24 | 0 | 24 |
| **Exact CODE+TEST total** | **341** | **47** | **388** |
| Review limit |  |  | **400** |
| Headroom |  |  | **12** |

`git diff --check 0a72ddd3...` passed. The helper's `git diff --no-index --check` emitted no whitespace diagnostic; exit 1 was the expected content-difference result.

### Boundary Audit

The production diff is limited to:

- `src/ControlParental.Domain/IScheduledWorkService.cs`
- `src/ControlParental.Service/Program.cs`
- `src/ControlParental.Service/ScheduledWorkService.cs`
- `src/ControlParental.Service/UIMessageHandler.cs`

No Unit 2+ production files were changed: no `WnsRegistrationCoordinator`, legacy registration removal, Realtime, integrity, enforcement, or evidence-boundary implementation is present. App.UI changes are test-only repository-root compatibility changes. `RepositoryRootLocator` is internal to the App.UI test assembly and has no production reference.

## Build and Test Execution

All commands ran from:

`C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-1`

| Evidence | Exact command | Start → end | Exit | Result |
|---|---|---|---:|---|
| Service affected build | `dotnet build "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --configuration Debug --verbosity minimal` | `15:50:51.9611767` → `15:50:54.0778296` | 0 | 0 errors; 5 existing dependency warnings |
| App.UI affected build | `dotnet build "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --configuration Debug --verbosity minimal` | `15:50:54.0798288` → `15:51:04.8013240` | 0 | 0 errors; existing analyzer warning corpus |
| Focused Service Unit 1 | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~ScheduledWorkServiceAsyncDispatchTests.AdmitSyncAsync|FullyQualifiedName~UIMessageHandlerWnsTests.AuthenticatedTriggerSync|FullyQualifiedName~UIMessageHandlerWnsTests.UnauthenticatedTriggerSync|FullyQualifiedName~UIMessagesJsonContextTests.JsonSerializer_RoundTrip_AllSampleEnvelopes_PreservesData_ViaSourceGen|FullyQualifiedName~NamedPipeUIServerDeserializeTests.DeserializeMessage_TriggerSync_NotNull|FullyQualifiedName~NamedPipeUIServerDeserializeTests.DeserializeMessage_MalformedJson_ReturnsNull"` | `15:51:24.5654613` → `15:51:27.3504321` | 0 | **11 passed, 0 failed, 0 skipped** |
| Exact complete scenario 2 | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName=ControlParental.Service.Tests.ScheduledWorkServiceAsyncDispatchTests.AdmitSyncAsync_DeniedIdentityThenLaterAdmissionConvergesAcrossAllSources"` | `15:51:27.3514333` → `15:51:30.0024949` | 0 | **1 passed, 0 failed, 0 skipped** |
| Focused App.UI opacity | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~WnsLifecycleTests.HandleRawNotificationAsyncWithMalformedPayloadEmitsTypedTriggerSync|FullyQualifiedName~WnsLifecycleTests.HandleRawNotificationAsyncWithValidPayloadEmitsTypedTriggerSync|FullyQualifiedName~WnsLifecycleTests.TriggerSyncRoundTripsThroughSourceGeneratedContext"` | `15:51:30.0024949` → `15:51:32.9208152` | 0 | **3 passed, 0 failed, 0 skipped** |
| Full Service regression | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --verbosity minimal` | `15:51:42.1484598` → `15:51:55.6083908` | 0 | **1,164 passed, 0 failed, 0 skipped** |
| Full App.UI regression | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-build --configuration Debug --verbosity minimal` | `15:51:42.1809996` → `15:51:48.0953974` | 0 | **186 passed, 0 failed, 0 skipped** |

The linked-worktree App.UI regression proves the test-only root locator recognizes both `.git` directories and worktree `.git` files. No restore or dependency mutation was performed. A full-solution build was not required because both affected project builds and full regressions independently passed.

## Coverage Evidence

Fresh command:

```text
dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --collect:"XPlat Code Coverage" --results-directory "tests\ControlParental.Service.Tests\TestResults\coverage-unit1-final-20260820-1552" --verbosity minimal
```

- Start: `2026-08-20T15:52:02.6255247-05:00`
- End: `2026-08-20T15:52:29.5405655-05:00`
- Exit: `0`
- Tests: **1,164 passed, 0 failed, 0 skipped**
- Artifact: `tests\ControlParental.Service.Tests\TestResults\coverage-unit1-final-20260820-1552\55a68dde-3199-4e1b-9504-319922378fc2\coverage.cobertura.xml`
- Cobertura aggregate: 11,479/17,372 lines (**66.07%**) and 2,627/3,988 branches (**65.87%**); non-empty.

### Changed File Coverage

| File / executable class | Line | Branch | Relevant uncovered lines | Rating |
|---|---:|---:|---|---|
| `ScheduledWorkService.cs` / primary class | **87.08%** | **85.93%** | 240, 246, 252, 258, 317–350 timer callbacks, 725–726, 807–809, 832–836 | Acceptable |
| `ScheduledWorkService.AdmitSyncAsync` (162–206) | **100% instrumented lines** | **100% reported conditions** | None | Excellent |
| `UIMessageHandler.cs` / `HandleCoreAsync` generated state machine | 64.10% | 77.27% | Other unrelated message branches; changed `TriggerSync` lines 130–140 all hit | Low whole-method coverage; in-scope branch covered |
| `Program.cs` / primary `Program` class | 88.31% | 80.00% | Changed DI composition is build/source-asserted rather than host-start executed | Acceptable |
| `IScheduledWorkService.cs` | N/A | N/A | Interface/enums produce no executable sequence points | Not applicable |

Direct admission evidence is strong: unknown-source, pre-cancelled, stopped/disposed, accepted, coalesced, caller-cancelled waiter, and shutdown-cancelled shared-work branches all recorded hits. The lower whole-method `UIMessageHandler` number is a Strict-TDD informational warning, not an in-scope scenario gap.

## Spec Compliance Matrix

| Requirement | Scenario | Runtime evidence | Current source evidence | Result |
|---|---|---|---|---|
| Hints admit bounded durable convergence | Concurrent hints coalesce | `AdmitSyncAsync_DeniedIdentityThenLaterAdmissionConvergesAcrossAllSources` passed independently and inside focused/full runs; asserts five bounded sources, exactly 1 `Accepted`, 4 `Coalesced`, and one backend fetch | Bounded `SyncTriggerSource`; `inFlightWork[PolicySync]` single-flight; shared scheduler dispatch | ✅ COMPLIANT |
| Hints admit bounded durable convergence | Offline or cancelled admission recovers | Complete scenario-2 test passed: authorized startup, unpaired admission with zero repository/backend calls, definitive identity restored, then five-source convergence. `CallerCancellationOnlyCancelsThatWaiter` and `ShutdownCancellationCancelsSharedWork` also passed | Caller token is used only by `dispatch.WaitAsync`; scheduler CTS owns work; `StopAsync` cancels scheduler CTS; polling timer remains | ✅ COMPLIANT |
| Hints admit bounded durable convergence | Malformed or unauthorized hint is harmless | Unauthorized handler, unknown source, malformed pipe JSON, malformed raw WNS payload, valid raw payload, source-generated round trip, and full regressions passed | Parameterless `TriggerSync`; authenticated handler gate; invalid enum rejection; raw WNS bytes ignored; adapters do not call backend | ✅ COMPLIANT |

**Compliance summary**: **3/3 Unit 1 scenarios compliant**. Registration lifecycle scenario 4 belongs to Unit 2 and is explicitly excluded.

## Scenario-2 Lifecycle Proof

The passing test executes one continuous lifecycle:

1. Starts the service with a definitive authorized identity and waits for startup policy sync to complete.
2. Clears startup invocations, changes identity to `Unpaired`, and admits WNS.
3. Verifies no repository version read and no backend policy fetch occurred.
4. Restores a definitive identity.
5. Admits `Startup`, `Wns`, `Ui`, `Timer`, and `Polling` while repository entry is barrier-held.
6. Releases the barrier and verifies exactly one `Accepted`, four `Coalesced`, and one backend fetch.

This closes the earlier fragmented-evidence risk without relying on prior verdicts.

## Correctness (Static Evidence)

| Claim | Status | Evidence |
|---|---|---|
| Bounded source/result contracts | Implemented | Five explicit sources and four explicit outcomes; undefined enum rejected |
| Caller cancellation ownership | Implemented | Admission does not pass caller token into shared dispatch; only waiter uses it |
| Scheduler shutdown ownership | Implemented | Shared work receives `workCancellation.Token`; `StopAsync` cancels it and drains in-flight work |
| Coalesced callers remain healthy | Implemented | Existing dispatch task is reused; caller cancellation test proves the coalesced waiter completes |
| Identity-gated sync | Implemented | Policy work checks definitive authorization and non-empty device ID before repository/backend access |
| Polling fallback | Preserved | 30-second policy timer dispatches through the same `PolicySync` work type |
| Opaque parameterless trigger | Preserved | `TriggerSync` remains parameterless; raw payload is discarded before generated serialization |
| Authenticated IPC routing | Implemented | Named pipe listener invokes `HandleAuthenticatedAsync`; generic `HandleAsync` rejects `TriggerSync` |
| No adapter/backend authority leakage | Preserved | App.UI emits typed IPC only; Service scheduler owns identity, fetch, cancellation, and single-flight |
| Test-only root helper | Confirmed | Internal helper exists only under `tests/ControlParental.App.UI.Tests` and production has no reference |
| Unit 2+ leakage | None | No Unit 2–5 production file or behavior was added |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| One scheduler owner | ✅ Yes | Every admitted source converges on `ScheduledWorkService` `PolicySync` single-flight work |
| Opaque WNS/UI hint | ✅ Yes | Parameterless generated message; payload content cannot grant authority |
| Identity and retry remain Service-owned | ✅ Yes | Identity gate and backend fetch remain inside policy sync; adapter has no retry/backend client |
| Authenticated pipe boundary | ✅ Yes | Production composition injects singleton scheduler into handler; listener uses authenticated dispatch |
| Polling remains fallback | ✅ Yes | Existing timer remains active and shares admission work type |
| Feature-chain isolation | ✅ Yes | Unit 1 diff is below budget and contains no Unit 2+ implementation |

## Strict TDD Audit

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| Six-column evidence reported | ✅ | `apply-progress.md` contains Task/Cycle, RED, GREEN, TRIANGULATE, SAFETY NET, REFACTOR |
| All Unit 1 tasks have tests | ✅ | 3/3 tasks map to existing executable tests |
| RED evidence honest | ⚠️ | Original compile RED and remediation timeout are described, but original/raw RED logs were not retained |
| GREEN confirmed fresh | ✅ | Focused 11/11, exact scenario 2 1/1, App.UI opacity 3/3, Service 1,164/1,164, App.UI 186/186 |
| Triangulation adequate | ✅ | Bounds, identity denial/restoration, all-source cardinality, cancellation ownership, shutdown, auth, malformed input, opacity, and serialization vary outcomes |
| Safety net confirmed fresh | ✅ | Both full regressions and both affected builds passed |
| Refactor claim coherent | ✅ | Scenario fragments were consolidated without production changes or weakened assertions |

**TDD compliance**: current behavior/spec proof is complete. Missing preserved raw original RED remains a warning only.

### Test Layer Distribution

| Layer | Focused evidence | Files | Notes |
|---|---:|---:|---|
| Unit | 11 | 4 | Scheduler, handler, generated JSON, malformed deserialization |
| Component/harness | 3 | 1 | WNS raw-payload opacity and generated typed emission |
| E2E | 0 | 0 | Not required for this local Unit 1 boundary |
| **Total** | **14** | **5** | Exact scenario-2 rerun is duplicate execution, not an extra case |

### Assertion Quality

**Result**: ✅ No trivial, tautological, ghost-loop, smoke-only, or production-free assertions were found in changed tests.

The scenario tests assert outcomes and observable authority boundaries: admission cardinality, backend/repository calls, cancellation observation, response success/failure, generated round trips, and malformed/opaque handling. `Assert.NotNull` occurrences have companion value/shape assertions and are not standalone type-only proof.

### Quality Metrics

- **Compiler/type checking**: passed for both affected test projects; 0 errors.
- **Analyzers**: existing warning corpus remains. The new test-only helper reports style/header spacing warnings; no runtime or assertion defect.
- **Whitespace**: tracked diff check passed; untracked helper produced no whitespace diagnostic.
- **Coverage quality**: meaningful and non-empty; `ScheduledWorkService` is above 80%, direct admission is fully exercised. Whole-method `UIMessageHandler.HandleCoreAsync` remains below 80% because unrelated message branches are outside Unit 1.

## Issues Found

### CRITICAL

None.

### WARNING

1. Original/raw RED output was not durably retained. The six-column evidence states this honestly; fresh runtime/spec proof is complete, so this does not block Unit 1 approval.
2. Strict changed-file coverage warning: `UIMessageHandler.HandleCoreAsync` is 64.10% line / 77.27% branch across all message branches, although every changed `TriggerSync` line and both authenticated/unauthenticated outcomes were hit.
3. The test-only `RepositoryRootLocator.cs` adds analyzer style/header warnings in the already warning-heavy App.UI build. It remains test-only and the full App.UI regression passes.

### SUGGESTION

None within this verification-only boundary.

## Approval and Next Eligibility

**Unit 1 is approved for its local feature-chain boundary.** Full SDD7 remains partial and not archive-ready.

Because the verdict is PASS WITH WARNINGS, the next eligible action is either:

- a separately user-authorized local Unit 1 commit, provided the planning-artifact boundary also remains review-safe; or
- Unit 2 preparation after preserving the exact approved Unit 1 bytes.

Neither action was performed.
