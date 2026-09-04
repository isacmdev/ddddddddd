# Verification Report

**Change**: `remote-signal-integrity` — AUTHORITATIVE FINAL FRESH SDD7 Unit 4B1 Lifecycle Ownership + Resources only  
**Branch**: `feat/sdd7-4b-integrity-lifecycle-races`  
**Base / HEAD**: `b5a2fee36f224bcb66eed1d560d6652443d4394c`  
**Mode**: Strict TDD, hybrid persistence, report-only verification  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**  
**B1 commit approval**: **NOT APPROVED**

## Executive Summary

All fresh executable commands completed successfully without restore or retry: four sequential portable-PDB builds, the current 12-test lifecycle matrix, AntiTamper focus twice (`35/35` each), combined Unit4A+B1 focus in both explicit filter orders (`75/75` each), full Service (`1,189/1,189`), full App.UI (`192/192`), and two new full-Service coverage processes (`1,189/1,189` each). No hang diagnostic was needed. Both coverage artifacts report `AntiTamperMonitor` at `100%` line and `91.25%` branch coverage.

The content candidate is exactly the requested two files and exactly **400/400** changed lines: `AntiTamperMonitor.cs` (`201+91`) and `AntiTamperMonitorTests.cs` (`64+44`). Unit4A/C files have no content diff; their status entries remain line-ending/stat dirt only. No deleted file, project/dependency/config drift, B2/C leakage, restore, commit, or branch mutation was found.

The candidate nevertheless fails the authoritative B1 acceptance gate. The latest remediation explicitly replaced the prior concurrent shared-success test with shared cancellation, and the current test tree contains neither the prior concurrent shared-success test nor the prior concurrent shared-failure test. Therefore two required Start outcomes have no passing covering test despite aggregate line coverage. Runtime proof is also missing for post-gate external dependency entry with the lifecycle lock demonstrably released, Dispose-before-Start allocation-free rejection, physical identities for the initial completion/operation, loop, `OwnedTasks`, and memoized drain, and `AsyncLocal` restoration/isolation plus exactly-once cleanup on the clock callback path. Green aggregate execution cannot substitute for these mandatory branch/scenario proofs.

Cumulative task state remains intentionally **9/14**. Unit4B2, Unit4C, and final full Unit4 verification remain mandatory regardless of this verdict. No implementation, task, apply, prior-report, config, dependency, git, B2/C, Unit5, commit, PR, or archive change was made.

## Boundary, Freshness, and Preservation

- Read proposal, all four delta specs, design, tasks, complete cumulative apply history, and all three prior B1 FAIL reports: `verify-report-unit-4b1.md`, `verify-report-unit-4b1-final.md`, and `verify-report-unit-4b1-approved.md`.
- CodeGraph was checked first in the exact worktree. `.codegraph` is absent; the explicit no-index-creation boundary required targeted direct inspection. No index was created.
- Inspected the complete source/test files, exact base diff, numstat/name-status, deletions and replacement weakening, lock/gate/callback paths, branch/base/status, line-ending-only dirt, coverage, and project/dependency/config drift.
- Preserved pre-report Git object hashes: tasks `4ef7f590b4f43c9b2fa8ff91e24fb5327e213bd2`; apply progress `1d70cd4ce288284a94f76ac7ba4ddf740dcb948f`; prior B1 reports `b66f51d55a3f76befdbcd2f4d5533ba9812827c4`, `0622b799df9c299de51e5402356ce01eaf1b251e`, and `83ae0b29b5128e2cbcd97a60c75ed12e64ddd357`.
- Every .NET command used `--no-restore`. Ignored build and coverage outputs are execution evidence; this report is the only authored filesystem artifact.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| B1 acceptance groups audited | 12 |
| Compliant | 7 |
| Partial | 2 |
| Failing / untested | 3 |
| Current explicit lifecycle matrix | 12/12 passed |

The planned 9/14 state is correct for this partial autonomous slice and is not itself a defect.

## Workspace, Diff, Drift, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD / merge-base | Expected branch; HEAD and merge-base equal exact base `b5a2fee36f224bcb66eed1d560d6652443d4394c` |
| Content diff | Exactly `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` |
| Deleted files | None |
| Unit4A / Unit4C content diff | None |
| Status-only dirt | `IntegrityVerdictHandler.cs`, `IntegrityRuntimePathTests.cs`, and `IntegrityVerdictHandlerTests.cs`; empty normalized content diff with LF→CRLF notices |
| Dependency/project/config drift | None |
| `git diff --check` | Exit 0; line-ending notices only |
| Restore | None |
| `.codegraph` | Absent before and after; no index created |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 201 | 91 | 292 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 64 | 44 | 108 |
| **Total** | **265** | **135** | **400/400** |

The candidate's exact `400/400` claim is confirmed. OpenSpec artifacts and ignored execution output are excluded.

### Deletion / Weakening Audit

- No production or test file was deleted.
- Relative to the approved Unit4A base, `VerifyClockAgainstServerTimeAsync_WhenDriftExceedsJumpThreshold_FiresClockJump` was removed and replaced by the stronger reentrant clock callback case.
- The simple disposed-Start case was removed; active Start-vs-Dispose now proves post-dispose rejection, but there is no equivalent allocation-free **Dispose-before-Start then Start** assertion.
- Most importantly, cumulative history records that `ConcurrentStart_AwaitsOneSharedInitializationResult` and `ConcurrentStart_SharesFailureAndCleansOneGeneration` existed in prior B1 candidates. The latest apply entry explicitly says the shared-success test was replaced by `ConcurrentStart_SharesCancellationAndCleansOneGeneration`; neither prior success nor failure test exists in the current tree. This is acceptance-evidence weakening, not merely a stale test name.

## Fresh Build, Test, and Coverage Execution

All commands ran sequentially in the exact worktree. No command failed, hung, or was retried.

| Evidence | Exact command | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain portable-PDB build | `dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0` | `15:58:30.4248039` → `15:58:32.6393914` | 0 | 0 errors/warnings |
| Service portable-PDB build | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0` | `15:58:40.1659293` → `15:58:46.9549039` | 0 | 0 errors; existing NU1601 |
| Service-tests portable-PDB build | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0` | `15:58:55.2164773` → `15:59:09.0001027` | 0 | 0 errors; existing NU1601/NU1701 |
| App.UI-tests portable-PDB build | `dotnet build tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0` | `15:59:16.8723712` → `15:59:47.2177390` | 0 | 0 errors/warnings |
| Current B1 matrix | Service tests, `--no-restore --no-build`, exact 12 current lifecycle names | `16:00:15.5110622` → `16:00:19.9551342` | 0 | **12 passed** |
| AntiTamper focus 1 | `dotnet test ... --filter "FullyQualifiedName~AntiTamperMonitorTests"` | `16:00:26.7122030` → `16:00:29.2464407` | 0 | **35 passed** |
| AntiTamper focus 2 | Same command, fresh process | `16:00:36.3560448` → `16:00:38.9284375` | 0 | **35 passed** |
| Unit4A→B1 order | Filter order `IntegrityCheckerTests|IntegrityVerdictHandlerTests|IntegrityRuntimePathTests|AntiTamperMonitorTests` | `16:00:49.4798061` → `16:00:52.6911527` | 0 | **75 passed** |
| B1→Unit4A order | Reverse filter order `AntiTamperMonitorTests|IntegrityRuntimePathTests|IntegrityVerdictHandlerTests|IntegrityCheckerTests` | `16:01:01.2411592` → `16:01:04.4792644` | 0 | **75 passed** |
| Full Service | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `16:01:21.1593542` → `16:01:34.8340034` | 0 | **1,189 passed**; existing duplicate-ID notice |
| Full App.UI | `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `16:01:41.9937379` → `16:01:48.2305262` | 0 | **192 passed** |
| Coverage process A | Full Service, portable PDB, XPlat Cobertura, new results directory | `16:01:59.9394073` → `16:02:33.9093129` | 0 | **1,189 passed** |
| Coverage process B | Same command, second new results directory and process | `16:02:45.1024675` → `16:03:19.2128008` | 0 | **1,189 passed** |

### Coverage Artifacts

1. `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-authoritative-final-a-20260821-1602/79e782a2-3cf5-4f56-81bb-51193c64f9da/coverage.cobertura.xml`
   - Aggregate: `54.30%` line / `58.66%` branch.
   - `ControlParental.Service.AntiTamperMonitor`: `100%` line / `91.25%` branch; no uncovered instrumented line.
2. `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-authoritative-final-b-20260821-1602/6f9304ba-1f48-4deb-bee9-b849a881c192/coverage.cobertura.xml`
   - Aggregate: `54.29%` line / `58.66%` branch.
   - `ControlParental.Service.AntiTamperMonitor`: `100%` line / `91.25%` branch; no uncovered instrumented line.

Aggregate/line coverage does not establish the omitted concurrent outcomes, object identities, lock-state assertion, or AsyncLocal isolation.

## B1 Acceptance Matrix

| # | Acceptance | Fresh runtime/static evidence | Result |
|---:|---|---|---|
| 1 | Per-generation physical ownership of CTS, loop, local initial completion, tick/timezone resources, gate, `OwnedTasks`, and drain | Source places every listed object on `Generation`. Restart tests physically compare CTS/gate/tick/timezone only; they do not compare `InitialCheck`, `InitialOperation`, `Loop`, `OwnedTasks`, or memoized `Drain` across G1/G2. | ⚠️ PARTIAL |
| 2 | Atomic publication, RCSA gated owned work, all signals and external calls after lifecycle lock release | Creation/publication/admission occur under `lockObject`; every admission wrapper starts behind an incomplete RCSA and every `TrySetResult` is textually after lock release. Clock/timezone/tamper events are invoked after their state locks. No test instruments an external privilege/integrity/backend/store/outbox entry to assert the lifecycle lock is actually free. | ❌ UNTESTED required branch proof |
| 3 | Concurrent Start fan-out for shared success, unique failure, and cancellation; one generation/allocation/initial work and shared cleanup | `ConcurrentStart_SharesCancellationAndCleansOneGeneration` passes and proves one backend call plus common cancellation type/cleanup. No current concurrent-success or concurrent-failure test exists; the prior tests were removed/replaced. | ❌ UNTESTED two mandatory outcomes |
| 4 | Stop-before-start, later Start, repeated/concurrent Stop, lock-consistent admission/snapshot/removal, synchronous completion removal | `StopBeforeStart_AllowsLaterStart`, `ConcurrentStop_SharesDrainUntilAdmittedWorkReleases`, and `SynchronousAdmission_IsRemovedBeforeDrain` pass. Source uses one lock for add/remove/snapshot and one memoized drain. | ✅ COMPLIANT |
| 5 | Real loop tick1/tick2 deterministic sequencing without delay/manual substitution | Channel-backed production tick seam proves tick1 consumed/call2 blocked, tick2 remains unconsumed before release, then tick2 consumed/call3 exactly entered. No sleep/Yield/retry/parallel-disable is used as proof. | ✅ COMPLIANT |
| 6 | Restart G1/G2 identity/disposal and old periodic/timezone callback rejection; old cleanup cannot touch G2 | Dedicated restart test proves distinct CTS/gate/tick/timezone objects, exact counters, G2 live state, old callback rejection, and repeated cleanup stability. | ✅ COMPLIANT for covered resources |
| 7 | Complete Dispose matrix and observed dedicated threads | Active blocked initialization Start-vs-Dispose passes; independently admitted blocked work with two `LongRunning` Dispose callers passes; both callers are awaited; repeated Dispose and post-active-dispose allocation-free rejection pass. There is no asserted Dispose-before-Start allocation/rejection branch. | ⚠️ PARTIAL |
| 8 | Clock/timezone callbacks outside lock; reentrant clock/tamper Stop+Dispose; AsyncLocal finally restoration/isolation; exactly-once eventual cleanup | Source dispatches callbacks outside state locks and restores the instance AsyncLocal in `finally`. Clock and tamper reentry tests pass; tamper path later asserts resource cleanup. Clock path does not assert resource counters, marker restoration, or isolation, and no cross-context/instance marker test exists. | ❌ UNTESTED complete marker/cleanup contract |
| 9 | Late initial completion checks cancellation before successful initialization | `RunMonitorLoopAsync` checks owner cancellation immediately after `InitialOperation`; `StartAndDispose_CancelsOwnedInitializationAndCleansOnce` releases a late normal backend result after cancellation and observes Start cancellation. | ✅ COMPLIANT |
| 10 | No static/global seam, suite-order dependence, survivor, release/dispose race, or lock-held external call | Tick seam and callback marker are instance fields; no static/global mutable test seam or parallel disable was found. Focus twice, both combined orders, full suites, and two coverage processes passed. Source gate ordering avoids lock-held dependency prefixes; omitted runtime lock-state assertion remains item 2. | ✅ static/execution |
| 11 | Unit4A unchanged/green; zero B2/C leakage | Only the two B1 files have normalized content diff. Both combined orders and full Service/App.UI pass. Handler/runtime-path status entries have empty content diff. | ✅ COMPLIANT |
| 12 | Tests are physical/pre-cleanup and do not use timing/retry/parallel-disable as B1 proof | Tick/restart/Dispose tests use barriers, exact counts, physical references, and assertions before cleanup. `Task.Delay(Timeout.Infinite, token)` is only a cancellation-controlled blocker; the pre-existing 100 ms outbox test is not B1 lifecycle proof. No Yield/retry/parallel-disable or copied implementation is used. | ✅ COMPLIANT |

**Acceptance summary**: **7 compliant**, **2 partial**, **3 failing/untested**.

## Required Branch-Hit Audit

| Required branch/outcome | Fresh evidence | Assessment |
|---|---|---|
| Shared Start success | Only sequential success and already-running-after-success tests; prior concurrent-success test is absent | ❌ UNTESTED |
| Shared Start unique failure | No current concurrent-failure test; prior test is absent | ❌ UNTESTED |
| Shared Start cancellation | `ConcurrentStart_SharesCancellationAndCleansOneGeneration` | ✅ |
| Admission after lifecycle lock | Source gate ordering only; no dependency-entry lock-state/reentrancy assertion | ❌ UNTESTED runtime branch contract |
| Clock callback | `ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock` | ✅ callback/reentry; ⚠️ marker cleanup contract incomplete |
| Synchronous completion | `SynchronousAdmission_IsRemovedBeforeDrain` | ✅ |
| Repeated/concurrent Stops | `ConcurrentStop_SharesDrainUntilAdmittedWorkReleases`; repeat Stop in restart test | ✅ |
| Actual tick | `RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks` | ✅ |
| Restart | `Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer` | ✅ covered resource subset |
| Concurrent Dispose | `ConcurrentStopAndDisposeShareLifecycleOwnership`, two observed long-running callers | ✅ |
| Start/Dispose | `StartAndDispose_CancelsOwnedInitializationAndCleansOnce` | ✅ |
| Callback reentry | Clock and tamper dedicated tests | ✅ deadlock behavior; ⚠️ restoration/isolation not asserted |
| Cancellation before initial success | `StartAndDispose_CancelsOwnedInitializationAndCleansOnce` | ✅ |
| Cleanup | Exact per-resource counters in restart/Dispose/tamper tests | ✅ covered paths; clock and omitted generation objects incomplete |

## Correctness and Ownership Audit

| Area | Finding | Assessment |
|---|---|---|
| Generation publication | Complete source owner is published atomically; initial wrapper is registered before release | ✅ static |
| Shared Start implementation | All concurrent callers capture `InitialCheck.Task`; success/failure/cancellation structurally fan out | ✅ static, ❌ incomplete runtime proof |
| Owned task set | Add, removal, and drain snapshot use `lockObject`; synchronous completion is removed | ✅ |
| Stop/drain | Admission closes before cancellation; one memoized drain owns loop, tasks, resource disposal, and generation removal | ✅ |
| Tick sequencing | Exact two-tick progression is runtime-proven | ✅ |
| Restart isolation | Captured periodic/timezone entries require exact current generation; old covered resources cannot affect G2 | ✅ covered subset |
| Disposal | Active initialization, active admitted work, two concurrent callers, repetition, and thread observation pass | ✅ active paths; ⚠️ before-start assertion absent |
| Callback lock boundary | Clock, timezone, and tamper callbacks occur after state locks; callback owner marker uses `finally` | ✅ static; runtime restoration/isolation incomplete |
| External-call lock boundary | RCSA gates prevent privilege/integrity/backend/store/outbox prefixes from starting before lifecycle lock release | ✅ static; required runtime lock-state hit absent |
| Late initial success | Owner cancellation is checked before `InitialCheck.TrySetResult` | ✅ runtime-covered |

No new source-level race or lock-held external invocation was found in the remediated bytes. The FAIL is driven by mandatory absent runtime evidence and test weakening, not by a fabricated implementation defect.

## Partial Spec Compliance

| Spec scenario | B1 contribution | Result |
|---|---|---|
| Runtime integrity — restart and recovery preserve semantics | Restart/captured callback/resource behavior is substantially covered, but complete generation-object identity and callback marker isolation are not | ⚠️ PARTIAL |
| Offline enforcement — concurrent recovery is serialized | B1 gate, task set, tick, Stop, and active Dispose behavior pass; all three shared-Start outcomes and post-gate lock-state proof do not | ❌ UNTESTED complete B1 contribution |
| Runtime integrity — late completion/cancellation/fault safety | Cancellation-before-initial-success is B1-covered; broader late result/fault policy remains explicitly Unit4B2 | ➖ DEFERRED remainder |
| Unit4A authority/non-degradation baseline | No content drift; combined and full suites green | ✅ COMPLIANT baseline |

No full Unit4 scenario completion is claimed.

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Cumulative apply progress contains B1 RED/GREEN/remediation history |
| Test file exists | ✅ | `AntiTamperMonitorTests.cs` exists and executes |
| Genuine behavioral RED retained | ⚠️ PARTIAL | Behavioral tick, callback, admission, and lifecycle defects are described. Exact raw tests-first chronology for the final bytes is incomplete; NETSDK/compile-only failures receive no credit |
| Fresh GREEN | ✅ | Current matrix 12/12; AntiTamper 35/35 twice; combined 75/75 twice; full suites and two coverage processes green |
| Triangulation | ❌ | Concurrent Start success/failure, admission-after-lock runtime assertion, Dispose-before-Start allocation guard, complete generation identities, and AsyncLocal isolation are absent |
| Safety net | ✅ | No build/test/coverage command failed or hung |
| Refactor/slice safety | ✅ | Exact two-file 400/400 boundary; Unit4A/C unchanged |

Strict-TDD chronology is not fabricated. Missing raw logs alone would be warning-only; the current missing covering scenarios are independently blocking.

## Test Layer Distribution

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Unit/mock lifecycle | 12 current matrix; 35 AntiTamper total | 1 | Production monitor with mocked ports, barriers, channels, and reflection-backed object/counter evidence |
| Production-component harness | Unit4A runtime paths within combined 75 | Existing | Authority/persistence baseline only; not substituted for omitted B1 races |
| E2E/external | 0 | 0 | Correctly excluded |

## Changed File Coverage

| File | Line | Branch | Uncovered normative behavior | Rating |
|---|---:|---:|---|---|
| `AntiTamperMonitor.cs` | 100% | 91.25% | Concurrent shared-success/failure outcomes, dependency-entry lock state, omitted physical object identities, before-start disposal allocation guard, and AsyncLocal isolation are behavioral/concurrency contracts not established by aggregate hits | ✅ Excellent aggregate; behavior gaps remain |
| `AntiTamperMonitorTests.cs` | N/A | N/A | Test assembly excluded from product coverage | ➖ |

## Assertion Quality

- No literal tautology, ghost loop, assertion-free added lifecycle test, copied lifecycle implementation, B1 timing retry, `Task.Yield`, sleep-as-proof, or test parallelization disable was found.
- Actual tick assertions are exact and ordered before/after release.
- Restart and active Dispose assertions use physical references and exact disposal counters.
- Both dedicated long-running Dispose caller tasks are awaited and observed to terminate.
- `ConcurrentStart_SharesCancellationAndCleansOneGeneration` is meaningful, but cannot substitute for the removed concurrent success/failure outcome tests.
- The physical restart test does not inspect initial completion/operation, loop, task-set, or drain identity.
- The clock reentry test does not assert marker restoration/isolation or exact cleanup counters.

**Assertion quality**: no trivial assertions; mandatory outcome and identity omissions remain.

## Quality Metrics

**Compiler/type check**: ✅ Four portable-PDB builds, 0 errors.  
**Package/analyzer corpus**: ⚠️ Existing NU1601/NU1701 and duplicate xUnit test-ID notice.  
**Dedicated linter**: ➖ Not separately configured/executed.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| One generation lifecycle owner | ✅ source | Every lifecycle object is generation-scoped; complete physical test matrix is partial |
| Deterministic shared drain | ✅ | One memoized drain and locked task set |
| Generation-safe restart | ✅ covered resource subset | Captured old periodic/timezone entries are rejected and G2 remains independent |
| No lifecycle lock over external/user work | ✅ static | RCSA gates and callback placement are outside locks; runtime dependency-entry assertion is absent |
| One retry owner | ✅ | BackendClient remains unchanged |
| B1/B2/C surgical split | ✅ | No completion-fault policy or verdict ordering/escalation leakage |

## Issues Found

### CRITICAL

1. **Concurrent Start success and unique failure are no longer runtime-covered.** The current remediation preserves only shared cancellation. Prior B1 history and reports identify dedicated concurrent-success and concurrent-failure tests, but both are absent from the current tree. The required three-outcome contract therefore lacks two passing covering tests.
2. **The required admission-after-lock branch has only static evidence.** The RCSA source shape is correct, but no passing test observes privilege/integrity/backend/store/outbox entry and asserts or reenters lifecycle state while the lifecycle lock is free. The user explicitly required this branch hit.
3. **Complete physical lifecycle/marker proof is absent.** Tests do not physically distinguish G1/G2 initial completion/operation, loop, `OwnedTasks`, or memoized drain; Dispose-before-Start allocation-free rejection is not asserted; and the clock callback does not prove AsyncLocal restoration/isolation plus exact cleanup. These are explicit B1 acceptance requirements, so source inspection alone cannot mark them compliant.

### WARNING

1. Strict-TDD behavioral RED summaries exist, but exact retained raw command/timestamp chronology is incomplete for the final remediation bytes.
2. Existing NU1601/NU1701 warnings and one duplicate xUnit test-ID discovery notice remain; dependency declarations are unchanged.
3. Three out-of-slice tracked paths remain status-dirty because of line endings although their normalized content diffs are empty.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

**B1 commit approval: NOT APPROVED.** The current implementation has the expected generation/gate/drain shape and every fresh executable run is green, but authoritative B1 approval requires passing covering tests for every named concurrency and ownership scenario. The removal of concurrent shared-success/failure coverage, plus missing admission-lock, complete physical-owner, Dispose-before-Start, and AsyncLocal isolation evidence, prevents approval.

Cumulative state remains **9/14**. Unit4B2, Unit4C, and final full Unit4 verification remain mandatory. Do not commit this B1 candidate and do not prepare B2/C from this verdict.
