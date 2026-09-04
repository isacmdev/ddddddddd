# Verification Report

**Change**: `remote-signal-integrity` — SDD7 Unit 4A Backend Authority + Durable Persistence only  
**Branch**: `feat/sdd7-4-runtime-integrity-enforcement`  
**Base / target**: `3082559551ce4aa4363bd14a9e09dda0b1176f5f` / `feat/sdd7-3-foreground-realtime`  
**Worktree**: `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-4`  
**Mode**: Strict TDD, hybrid persistence, fresh authoritative report-only re-verification  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**

## Executive Summary

All fresh sequential `--no-restore` execution passed: Domain and Service builds, the portable-PDB Service test build, the exact 9 runtime-path cases, the current 67-test Unit 4A focus, 1,181 Service tests, 192 App.UI tests, and portable-PDB coverage. The workspace is on the expected branch and base, the seven expected CODE+TEST paths total exactly **368/400** changed lines, no tracked file or test is deleted, and no project/dependency/configuration drift was found.

The canonical key remediation is present in production: `IssueKey(0, BinaryIntegrityFailure, "integrity/binary", DeviceId)` excludes credential generation, equality and JSON persistence include `IdentityScope`, production DI gives `BackendClient` and `AntiTamperMonitor` the same coordinator, and the runtime harness uses that production constructor. However, three mandatory acceptance proofs remain blocking:

1. The claimed caller-cancellation test never cancels. It runs only three revoked verdicts; the third returns `Warn`, so `CancelOnWriteIssueStore.UpsertActiveAsync` is never called and its `cancellation.Cancel()` line is never reached. The test therefore passes without exercising its named cancellation behavior; fresh coverage also leaves `BackendClient.ReportIntegrityAsync` caller-cancellation lines 608–609 unhit.
2. Recovery does not compare the same-device unrelated `HookTimeout` record byte/field-for-field. The comparison filters out every `device-a` record, including that unrelated record, and later checks only that an `unrelated` cause still exists. The required exact unrelated-state preservation after recovery/recreation is therefore unproven.
3. Slice purity is not restored exactly to the parent. `RunMonitorLoopAsync` is absent and `StopAsync`/`Dispose` match parent timer ownership, but the diff removes the parent call to `ScheduleEscalation(timestamp)`. This is a timer-behavior deletion inside the Unit 4A diff and violates the explicit “timer/Stop/Dispose behavior restored to parent” boundary.

In addition, the same-device generation test changes the fake credential-store snapshot and calls `InitializeAsync`; it does not execute a successful production `BackendIdentityCoordinator.RefreshAsync` transition in the end-to-end key/recovery scenario. The stable key makes the intended result structurally credible, but the mandatory real-refresh scenario is not fully covered at runtime.

**Unit 4A is not approved for its local feature-chain commit boundary.** The expected cumulative state remains **9/14**; unchecked top-level tasks 4.1–4.3 are not themselves a Unit 4A defect. Unit 4B and final full-Unit 4 verification remain mandatory, and full SDD7 is not archive-ready.

## Verification Boundary and Freshness

- Read the proposal, all four delta specs, design, updated re-sliced tasks, cumulative apply progress, and both historical FAIL reports.
- Historical reports were not reused for this verdict and remain unchanged:
  - `verify-report-unit-4.md`: SHA-256 `FA44D9A41B3ADF96054E4AB6EDA91CF78B2980828A5093C51E78DEDFF3798EEE`
  - `verify-report-unit-4a.md`: SHA-256 `88A7080A40242BA708625EF900C5E9C51B87ECA9767C92B5E222E8324C19D5F1`
- CodeGraph was attempted against the exact worktree first and reported that the worktree is unindexed. Focused direct inspection was used; `.codegraph` was not created.
- Inspected all seven expected changed paths, their parent diff, `BackendClient`, `BackendIdentityCoordinator`, `EnforcementLevelMonitor`, `FileIssueStore`, `IIssueStore`, production DI, async interface callers/defaults, and the relevant authority/persistence tests.
- No source, test, task, apply, spec, design, config, dependency, git, branch, worktree, Unit 4B, Unit 5, external system, commit, push, or PR mutation was performed. This report is the only authored SDD artifact.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Cumulative checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| Exact Unit 4A runtime-path cases | 9/9 executed and green |
| Mandatory remediation audit items | 11 |
| Fully compliant | 7 |
| Partial | 1 |
| Failing/untested | 3 |

The 9/14 task state is the authorized partial-slice state and is not reported as an incomplete-task critical for Unit 4A.

## Workspace, Diff, Drift, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD | Expected branch; HEAD `3082559551ce4aa4363bd14a9e09dda0b1176f5f` |
| Base / target / merge-base | All resolve to `3082559551ce4aa4363bd14a9e09dda0b1176f5f` |
| Tracked changes | Exactly six expected modified paths |
| Untracked CODE+TEST | Only expected `IntegrityRuntimePathTests.cs` |
| OpenSpec | Cumulative artifacts untracked; historical reports preserved |
| Deleted files/tests | None |
| Project/dependency/config drift | None (`*.csproj`, props, targets, lockfiles, `global.json`, JSON/YAML/config) |
| Tracked `git diff --check` | Exit 0 |
| Untracked test `--no-index --check` | Expected difference exit 1; no whitespace diagnostic |
| Restore | None; every .NET command used `--no-restore` |
| CodeGraph artifact | Absent before and after |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Domain/IEnforcementLevelMonitor.cs` | 20 | 1 | 21 |
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 48 | 13 | 61 |
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | 25 | 7 | 32 |
| `src/ControlParental.Service/Program.cs` | 3 | 2 | 5 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 1 | 0 | 1 |
| `tests/ControlParental.Service.Tests/IntegrityVerdictHandlerTests.cs` | 20 | 3 | 23 |
| `tests/ControlParental.Service.Tests/IntegrityRuntimePathTests.cs` | 225 | 0 | 225 |
| **Total** | **342** | **26** | **368/400** |

## Fresh Build, Test, and Coverage Execution

All commands ran sequentially in the exact worktree.

| Evidence | Exact command | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain build | `dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal` | `09:51:06.2037900` → `09:51:08.9158795` | 0 | 0 errors; 1,006 existing warnings |
| Service build | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal` | `09:51:08.9264216` → `09:51:17.9404398` | 0 | 0 errors; 3,133 existing warnings |
| Service test portable-PDB build | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable` | `09:51:17.9414392` → `09:51:35.5485919` | 0 | 0 errors; 7,762 existing warnings |
| Exact runtime path | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityRuntimePathTests"` | `09:51:35.5485919` → `09:51:40.7982955` | 0 | **9 passed**, 0 failed/skipped |
| Authorized Unit 4A focus | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests"` | `09:51:40.7992980` → `09:51:44.0002042` | 0 | **67 passed**, 0 failed/skipped |
| Full Service | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `09:51:44.0002042` → `09:51:57.2368541` | 0 | **1,181 passed**, 0 failed/skipped; existing duplicate-ID discovery notice |
| Full App.UI | `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `09:51:57.2378249` → `09:52:00.3725006` | 0 | **192 passed**, 0 failed/skipped |
| Portable-PDB coverage | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug -p:DebugType=portable --collect:"XPlat Code Coverage" --results-directory tests\ControlParental.Service.Tests\TestResults\coverage-unit4a-final-fresh-20260821 --verbosity minimal -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | `09:52:00.3725006` → `09:52:34.5812415` | 0 | **1,181 passed**; Cobertura produced |

**Coverage artifact**: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4a-final-fresh-20260821/5df3a90c-8de8-41ab-877f-5ebdc451d762/coverage.cobertura.xml`  
**Aggregate coverage**: 53.98% line / 58.00% branch (informational; no aggregate threshold is configured).

## Mandatory Remediation Audit

| # | Acceptance | Evidence | Result |
|---:|---|---|---|
| 1 | Canonical durable key excludes credential generation and includes semantic cause plus explicit identity scope | Production creates `IssueKey(0, BinaryIntegrityFailure, "integrity/binary", identity?.DeviceId)`. Record equality and default JSON serialization include `IdentityScope`; runtime persistence reads it back. | ✅ COMPLIANT |
| 2 | Same device survives real generation refresh; different devices are unequal and isolated | Stable-key equality, refreshed token use, and cross-device inequality/isolation pass. The end-to-end test manually replaces the fake store snapshot and calls `InitializeAsync`; it does not execute successful production `RefreshAsync` in the same key/recovery scenario. | ⚠️ PARTIAL |
| 3 | Current production backend constructor and same real coordinator instance | Every runtime-path backend uses `BackendClient(HttpClient, url, coordinator)` and passes the same real coordinator to the monitor. Program DI resolves one singleton coordinator for both. | ✅ COMPLIANT |
| 4 | Fake HTTP → real parser → real policy → real file-backed store, with token and persisted identity assertions | Exact runtime suite executes this chain and asserts `token-a` / `token-a-refreshed` and persisted `device-a`. | ✅ COMPLIANT |
| 5 | Duplicate revoked remains one issue; recovery preserves unrelated/other-device fields exactly | Active duplicate cardinality and exact other-device preservation pass. Exact same-device unrelated `HookTimeout` preservation after recovery is not compared because the snapshot filter excludes all `device-a` records. | ❌ UNTESTED |
| 6 | All non-definitive outcomes preserve complete physical snapshots | Unknown, pending, absent, malformed, stale generation, refresh timeout, and HTTP 503 compare exact snapshots. The caller-cancellation test never reaches its cancellation trigger and is not valid runtime evidence. | ❌ UNTESTED |
| 7 | Recreation uses production persistence/enforcement/policy components and later authoritative recovery works | `FileIssueStore`, `EnforcementLevelMonitor`, and `IntegrityVerdictHandler` are recreated; three trust verdicts physically resolve the tested key. | ✅ COMPLIANT |
| 8 | Local evidence remains report-only; no default shadow toggle or client/UI/WNS/Realtime authority | Local reaction is discarded, production handler defaults active, no baseline API exists, and no App.UI/Realtime/WNS durable authority was added. | ✅ COMPLIANT |
| 9 | Unit 4B purity and exact parent timer/Stop/Dispose behavior | `RunMonitorLoopAsync` is absent; `StopAsync` and `Dispose` match parent. The diff nevertheless deletes parent `ScheduleEscalation(timestamp)` invocation, changing timer behavior in Unit 4A. | ❌ FAILING |
| 10 | Async add/resolve changes are coherent across implementation, DI, callers, and fakes; regressions stay green | Default compatibility methods compile, Service overrides persist, production DI is coherent, and full Service/App.UI regressions pass. | ✅ COMPLIANT |
| 11 | Assertions call production and are physical/pre-cleanup; no tautology, ghost path, reimplementation, or baseline weakening | Most runtime assertions are physical and production-calling, but the cancellation test's precondition never reaches cancellation; the escalation timer invocation is also deleted without a covering behavior test. | ❌ FAILING |

## Unit 4A Scenario Matrix

| Spec scenario | Runtime evidence | Result |
|---|---|---|
| Runtime integrity — valid evidence receives trust | Real parser/policy/store recovery resolves the device key after recreation; successful production refresh and exact same-device unrelated preservation are incomplete | ⚠️ PARTIAL |
| Runtime integrity — revocation changes enforcement | Real authenticated revoked path creates one durable identity-scoped issue after policy threshold | ✅ COMPLIANT |
| Runtime integrity — unknown/transient is non-degrading | Exact snapshots pass for seven outcomes; caller cancellation is not actually triggered | ❌ UNTESTED |
| Runtime integrity — restart/recovery preserve semantics | Physical issue resolves after component recreation and other-device state is exact; same-device unrelated field equality is unproven | ⚠️ PARTIAL |
| Runtime integrity — deferred release scope is not claimed | No `live-integrated`, `client-ready`, or `ExternalVerified` claim is made | ✅ COMPLIANT |
| Offline enforcement — supported revoked evidence degrades health | Real durable severe issue is produced through the authority chain | ✅ COMPLIANT |
| Offline enforcement — semantic recovery survives restart | Duplicate active cardinality and physical resolution pass; required exact unrelated preservation is incomplete | ⚠️ PARTIAL |
| Offline enforcement — transient integrity does not degrade | Cancellation case lacks executed cancellation behavior | ❌ UNTESTED |
| Offline enforcement — concurrent recovery is serialized | Explicitly deferred to Unit 4B under the authorized partial-slice contract | ➖ DEFERRED |

**Unit 4A in-scope compliance**: 3 compliant, 3 partial, 2 untested; 1 concurrency scenario explicitly deferred to Unit 4B.

## Correctness and Production Call Path

`ControlParentalService` → `AntiTamperMonitor` → local `IntegrityChecker` evidence → production `BackendClient.ReportIntegrityAsync` → `IBackendIdentityCoordinator.GetDefinitiveSessionAsync` → authenticated HTTP/parser → `IntegrityVerdictHandler` → async `EnforcementLevelMonitor` → serialized `FileIssueStore`.

| Invariant | Finding |
|---|---|
| Active backend authority | Handler constructor defaults `shadowMode:false`; Program supplies no shadow override |
| Canonical transport authority | Production `BackendClient` and monitor resolve the same singleton coordinator |
| Stable issue identity | Credential generation is excluded; device ID is an explicit key component |
| Stale completion guard | Captured `BackendIdentityState` is compared after transport; runtime stale-generation case passes |
| Definitive-only mutation | Monitor invokes durable mutation only for successful exact `trust|revoked` verdicts |
| Local evidence | Report-only; local handler reaction is not applied to durable enforcement |
| Retry owner | `BackendClient` remains the sole bounded transport retry owner |
| Recovery scope | Store resolution is exact-key based; runtime proof is incomplete for byte-equivalent same-device unrelated state |
| Lifecycle boundary | Parent Stop/Dispose timer ownership is restored, but parent escalation timer scheduling was deleted |

## Changed Method and Branch Coverage

| Scope | Line | Branch | Required uncovered behavior / assessment |
|---|---:|---:|---|
| `AntiTamperMonitor` aggregate | 83.97% | 84.09% | Acceptable aggregate |
| `PerformBinaryIntegrityCheckAsync` | 96.00% | 75.00% | Only disposed/not-running guard uncovered |
| `ProcessVerdictReactionAsync` | 71.05% | 57.14% | `Limit` and `ShadowWarn` defensive branches uncovered |
| `IntegrityVerdictHandler` | 87.57% | 82.60% | Disposed/dead escalation timer paths uncovered; parent schedule invocation was removed |
| `BackendClient` | 91.22% | 63.88% | Broad class branch corpus remains low |
| `BackendClient.ReportIntegrityAsync` | 94.73% | 100% | **Caller cancellation rethrow lines 608–609 unhit** |
| `BackendIdentityCoordinator` | 95.12% | 80.00% | Strong aggregate |
| `BackendIdentityCoordinator.RefreshAsync` | 80.39% | 66.66% | Several refresh failure/commit branches uncovered; full-suite success coverage does not join successful refresh to key recovery |
| `EnforcementLevelMonitor` | 94.76% | 86.95% | Strong aggregate; async durable implementation covered |
| `IEnforcementLevelMonitor` default async methods | 0% | N/A | Compatibility defaults compile but remain uncovered (warning only) |

The caller-cancellation coverage gap is blocking because inspection proves the named runtime test never cancels. Defensive `Limit`, `ShadowWarn`, interface-default, and unrelated refresh branches are warnings rather than blockers for this slice.

## Strict TDD Audit

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Unit 4A table is present in cumulative apply progress |
| Original genuine RED retained | ✅ summary | Historical local-evidence authority failure is recorded with exact filter, timestamp, exit 1, and failing `AddIssueAsync` observation |
| Raw RED log | ⚠️ | No durable raw log exists; none was fabricated |
| Final canonical rewrite RED | ⚠️ MISSING | Apply progress explicitly admits the final canonical test rewrite followed production edits |
| Fresh GREEN | ✅ | 9/9 runtime, 67/67 focus, 1,181/1,181 Service, 192/192 App.UI |
| Triangulation | ❌ | Caller cancellation does not execute; exact same-device unrelated recovery and successful real refresh-to-key scenario remain incomplete |
| Safety nets | ✅ | Builds, regressions, coverage, and diff checks pass |
| Slice refactor boundary | ❌ | Parent escalation timer scheduling is deleted |

The missing final-rewrite behavioral RED remains a strict-TDD warning exactly as requested. The verdict fails on current behavioral/proof defects, not on fabricated chronology.

## Test Layer Distribution

| Layer | Tests | Files | Notes |
|---|---:|---:|---|
| Unit/mock | 58 focused | 3 | Checker, handler, and legacy monitor suites |
| Production-component harness | 9 | 1 | Real coordinator/backend/parser/policy/enforcement/file store with fake HTTP/ports |
| E2E/external | 0 | 0 | Correctly excluded; no external readiness claim |
| **Total focused** | **67** | **4** | |

## Assertion Quality

| File | Line | Assertion/path | Issue | Severity |
|---|---:|---|---|---|
| `IntegrityRuntimePathTests.cs` | 179–182 | Three revoked runs followed by exact snapshot equality | Third revoked is `Warn`; `CancelOnWriteIssueStore.UpsertActiveAsync` is never called, so cancellation never occurs. The test passes without exercising its named behavior. | CRITICAL |
| `IntegrityRuntimePathTests.cs` | 61, 78 | `IdentityScope != "device-a"` snapshot comparison | Excludes the seeded same-device unrelated `HookTimeout`; later existence assertion does not prove byte/field equivalence after recovery. | CRITICAL |
| `IntegrityRuntimePathTests.cs` | 69–71 | Direct fake-store snapshot replacement + `InitializeAsync` | Simulates a refreshed generation/token but bypasses successful production `RefreshAsync` in the end-to-end key scenario. | CRITICAL |
| `IntegrityVerdictHandlerTests.cs` | 466 | Method name `IsShadowMode_DefaultsToTrue` | Assertion correctly expects false, but the stale test name/documentation is misleading. | WARNING |

No literal tautology or ghost loop was found. The cancellation case is an incomplete execution path: its assertion is physical, but the required cancellation precondition never occurs.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Backend owns authoritative hash reference | ✅ | No client baseline/API/persistence added |
| Checker → authenticated backend → policy → durable enforcement | ✅ | Production path and runtime harness use current canonical constructor |
| Definitive verdicts only mutate | ✅ | Exact successful `trust|revoked` gate |
| Unknown/malformed/transient non-degrading | ❌ proof | Caller cancellation test never cancels |
| Stable identity-scoped issue | ✅ implementation | Key excludes generation and includes `DeviceId` |
| Restart-safe exact recovery | ⚠️ | Tested key and other device are preserved; same-device unrelated record equivalence is not proven |
| Unit 4A/4B surgical split | ❌ | Parent escalation timer scheduling is deleted |
| One retry owner | ✅ | No monitor-side retry owner added |

## Issues Found

### CRITICAL

1. **Caller cancellation is untested despite a green test name.** The test performs only three revoked runs; no durable write occurs, the cancellation source is never cancelled, and the physical snapshot equality is therefore non-probative for cancellation.
2. **Exact unrelated-state preservation after recovery is unproven.** The byte/field comparison excludes all `device-a` records and therefore excludes the seeded same-device unrelated issue that the requirement specifically requires recovery not to mutate.
3. **The required successful real credential-refresh chain is not executed in the end-to-end identity test.** The test directly replaces the fake credential snapshot and reinitializes the coordinator rather than driving production `RefreshAsync` success before recovery.
4. **Unit 4A changes parent timer behavior.** The diff removes `ScheduleEscalation(timestamp)` from `IntegrityVerdictHandler`, violating the explicit slice-purity contract that timer behavior be restored to parent and constituting an uncovered baseline behavior deletion.

### WARNING

1. Strict-TDD evidence has no behavioral RED for the final canonical rewrite and no durable raw log for the retained original RED summary.
2. Existing NU1601/NU1701, analyzer/style warning corpus, and one duplicate xUnit test-ID discovery notice remain; builds have zero errors and dependency declarations are unchanged.
3. Default async compatibility methods on `IEnforcementLevelMonitor` are uncovered; the production Service implementation overrides both, and App.UI remains non-authoritative.
4. `IsShadowMode_DefaultsToTrue` has a stale name/comment while correctly asserting that production now defaults to `false`.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

Unit 4A is **not approved for its local commit boundary**. Green builds/regressions, the 368/400 isolated diff, canonical key implementation, current production identity constructor, and real parser/policy/store coverage do not compensate for an unexecuted caller-cancellation case, incomplete exact unrelated-state recovery proof, a simulated rather than production successful refresh transition in the canonical scenario, and deletion of parent escalation timer behavior.

Cumulative state remains **9/14**. Unit 4B remains mandatory, final full Unit 4 verification remains mandatory, and full SDD7 is **not archive-ready**. No commit, push, PR, Unit 4B, or Unit 5 work was performed.
