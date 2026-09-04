# Verification Report

**Change**: `remote-signal-integrity` — FINAL FRESH SDD7 Unit 4B1 Lifecycle Ownership + Resources only  
**Branch**: `feat/sdd7-4b-integrity-lifecycle-races`  
**Base / HEAD**: `b5a2fee36f224bcb66eed1d560d6652443d4394c`  
**Mode**: Strict TDD, hybrid persistence, independent report-only verification  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**

## Executive Summary

Fresh inspection confirms that the gated-admission remediation closes the previously reported initial/periodic/timezone/outbox synchronous-prefix problem: wrapper tasks are registered under `lockObject`, every admission RCSA remains incomplete while registered, and every admission gate is signalled after the lifecycle lock is released. Shared Start failure, two concurrent active Dispose callers, complete task-set locking, exact actual-tick progression, restart resource identities, stale callback rejection, and per-resource cleanup now have passing evidence.

The candidate still does not satisfy the complete B1 gate. There is no passing runtime test for concurrent Start sharing **cancellation**, and there is no blocked-initialization **Start-vs-Dispose** test. More importantly, `CheckClockIntegrityAsync` calls `FireClockJump` while holding `lockObject`; `FireClockJump` invokes the external `OnClockJumpDetected` event before the outer lock is released. This violates the explicit no-user-work-under-lifecycle-lock invariant and leaves that callback outside the `AsyncLocal` reentrancy marker. The marker likewise does not cover the separate `TimezoneChanged` callback. The current callback test covers only `TamperDetected`.

All successful normal builds/tests are green: three portable-PDB builds, exact B1 matrix 9/9, AntiTamper focus 32/32 twice, combined Unit4A+B1/Enforcement focus 120/120, full Service 1,186/1,186, full App.UI 192/192, and a diagnostic full-Service coverage run 1,186/1,186. However, the first full coverage run hung without completing for 240 seconds and produced no artifact; the diagnostic repeat passed and is recorded as diagnostic evidence, not flaky-retry acceptance.

**B1 commit approval: NOT APPROVED.** Cumulative task state remains intentionally **9/14**. Unit 4B2, Unit 4C, and final full Unit 4 verification remain mandatory. No fix, commit, B2/C/Unit5 preparation, dependency/configuration change, or existing artifact mutation was performed.

## Boundary, Freshness, and Preservation

- Read proposal, all four delta specs, design, latest B1/B2/C task split, complete cumulative apply progress, `verify-report-unit-4b1.md`, `verify-report-unit-4b1-final.md`, and all historical Unit4/Unit4A reports.
- CodeGraph was checked first. `.codegraph` is absent; the explicit no-index-creation constraint required targeted direct inspection.
- Historical tasks/apply/report Git object hashes were captured before this report and rechecked after creation.
- All .NET commands used `--no-restore`. Build and coverage outputs are ignored execution artifacts. This report is the only authored filesystem artifact.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| B1 acceptance items audited | 14 |
| Compliant | 9 |
| Partial | 1 |
| Failing / untested | 4 |
| Exact current B1 matrix | 9/9 passed |

The planned 9/14 state is correct for this partial boundary and is not itself a defect.

## Workspace, Diff, Drift, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD / merge-base | Expected branch; HEAD and merge-base equal exact base `b5a2fee36f224bcb66eed1d560d6652443d4394c` |
| Content diff | Exactly `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` |
| Unit4A / Unit4C content diff | None |
| Status-only dirt | `IntegrityVerdictHandler.cs`, `IntegrityRuntimePathTests.cs`, `IntegrityVerdictHandlerTests.cs`; empty content diff and LF→CRLF notices only |
| Deleted files/tests | None |
| Dependency/project/config drift | None |
| `git diff --check` | Exit 0 |
| Restore | None |
| `.codegraph` | Absent; no index created |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 213 | 79 | 292 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 53 | 52 | 105 |
| **Total** | **266** | **131** | **397/400** |

The current candidate accounting is exact.

## Fresh Build, Test, and Coverage Execution

All successful commands ran sequentially in the requested worktree.

| Evidence | Exact command | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain portable-PDB build | `dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0` | `15:27:10.5223626` → `15:27:12.7093580` | 0 | 0 errors/warnings |
| Service portable-PDB build | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0` | `15:27:19.8440081` → `15:27:26.5079537` | 0 | 0 errors; existing NU1601 |
| Service-tests portable-PDB build | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0` | `15:27:33.3391013` → `15:27:47.1200892` | 0 | 0 errors; existing NU1601/NU1701 |
| Exact B1 matrix | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AntiTamperMonitorTests.ConcurrentStart_AwaitsOneSharedInitializationResult|FullyQualifiedName~AntiTamperMonitorTests.ConcurrentStart_SharesFailureAndCleansOneGeneration|FullyQualifiedName~AntiTamperMonitorTests.StopBeforeStart_AllowsLaterStart|FullyQualifiedName~AntiTamperMonitorTests.ConcurrentStop_SharesDrainUntilAdmittedWorkReleases|FullyQualifiedName~AntiTamperMonitorTests.RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks|FullyQualifiedName~AntiTamperMonitorTests.Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer|FullyQualifiedName~AntiTamperMonitorTests.ConcurrentStopAndDisposeShareLifecycleOwnership|FullyQualifiedName~AntiTamperMonitorTests.TamperCallback_CanRequestStopWithoutSelfDeadlock|FullyQualifiedName~AntiTamperMonitorTests.SynchronousAdmission_IsRemovedBeforeDrain"` | `15:27:57.5567681` → `15:28:01.9512912` | 0 | **9 passed** |
| AntiTamper focus, pass 1 | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AntiTamperMonitorTests"` | `15:28:14.2717717` → `15:28:16.7973025` | 0 | **32 passed** |
| AntiTamper focus, pass 2 | Same command | `15:28:22.9970013` → `15:28:25.5349560` | 0 | **32 passed** |
| Combined Unit4A+B1+Enforcement | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests|FullyQualifiedName~EnforcementLevelMonitor"` | `15:28:32.3363596` → `15:28:35.2752013` | 0 | **120 passed** |
| Full Service | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `15:28:42.5687823` → `15:28:56.1393039` | 0 | **1,186 passed**; existing duplicate-ID notice |
| Full App.UI | `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `15:29:02.9589409` → `15:29:05.9181493` | 0 | **192 passed** |
| First full coverage | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug -p:DebugType=portable --collect:"XPlat Code Coverage" --results-directory tests\ControlParental.Service.Tests\TestResults\coverage-unit4b1-approved-fresh-20260821 --verbosity minimal -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | Wrapper start timestamp was not emitted because the command never returned; terminated after 240,000 ms | timeout | No completion/result/artifact |
| Diagnostic full coverage repeat | Same command with results directory `coverage-unit4b1-approved-diagnostic-20260821` | `15:33:34.2266095` → `15:34:09.0296330` | 0 | **1,186 passed**; Cobertura produced |

**Accepted diagnostic artifact**: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-approved-diagnostic-20260821/da3e4e11-a429-4ec4-9e23-e5beb6afc080/coverage.cobertura.xml`  
**Aggregate coverage**: 54.27% line / 58.59% branch.  
**Changed production class**: `AntiTamperMonitor` 99.00% line / 87.50% branch.

The second coverage run is diagnostic evidence only. It does not erase the first fresh hang.

## B1 Acceptance Matrix

| # | Acceptance | Fresh runtime/static evidence | Result |
|---:|---|---|---|
| 1 | One physical generation owns lifecycle resources and one drain | `Generation` owns linked CTS, semaphore gate, initial TCS/operation, tick source/timer, timezone timer/work, loop, `OwnedTasks`, and memoized drain. Restart test checks physical identities and exact disposal counters. | ✅ COMPLIANT |
| 2 | Atomic publication, gated start, all signals after lock, no external work under lifecycle lock | Admission wrappers are registered before all four gate-signal sites, and all signals are textually after lock release. However, periodic clock processing calls `FireClockJump` under the outer lifecycle lock, and that method invokes `OnClockJumpDetected` before the outer lock is released. No runtime reentrancy test covers this path. | ❌ FAILING |
| 3 | Concurrent Starts share success, failure, cancellation, one generation/resources/operation | Success and failure fan-out pass with one backend call/generation. No concurrent shared-cancellation test exists; generic cancellation hits do not prove both Start callers receive the same outcome. | ❌ UNTESTED |
| 4 | Stop-before-start; repeated/concurrent Stops share one immutable drain; admission closes atomically | Dedicated stop-before-start and concurrent blocked-admission Stop tests pass. `BeginDrainLocked` has full branch coverage and closure/snapshot/add/remove use the same lock. | ✅ COMPLIANT |
| 5 | Add precedes continuation; synchronous completion not retained; HashSet never concurrently enumerated | `SynchronousAdmission_IsRemovedBeforeDrain` passes. Add precedes continuation; add/removal/snapshot all use `lockObject`; drain branches are fully hit. | ✅ COMPLIANT |
| 6 | Exact actual tick serialization | Channel test proves tick1 consumption/call2 block, tick2 unconsumed and call3 absent before release, then exact tick2 consumption and call3. No Delay/Yield. | ✅ COMPLIANT |
| 7 | Generation-captured periodic/timezone/manual callbacks; owned timezone/outbox | Callback entry points capture `Generation`, validate identity/open admission, and timezone outbox work enters the owned set through a gated wrapper. Restart test rejects old entries. | ✅ COMPLIANT |
| 8 | Restart physical matrix and exact cleanup | G1/G2 CTS/gate/tick/timezone identities differ; old callbacks cannot affect G2; exact per-resource counters are 1 for stopped generations and 0 while G2 is active; repeated cleanup is stable. | ✅ COMPLIANT |
| 9 | Full Dispose matrix, including blocked initial/admitted work and Start-vs-Dispose | Two dedicated long-running Dispose callers share the active independently admitted drain; repeated Dispose and allocation-free post-dispose Start rejection pass. There is no blocked-initialization Start-vs-Dispose test, so the mandatory matrix is incomplete. | ❌ UNTESTED |
| 10 | Callback reentrancy and AsyncLocal restoration/isolation | `TamperCallback_CanRequestStopWithoutSelfDeadlock` proves `TamperDetected` can request Stop/Dispose and eventual cleanup occurs once. The marker is instance-local and restored in `finally`, but separate `OnClockJumpDetected` and `TimezoneChanged` user callbacks are outside it; clock-jump callbacks can also execute under the lifecycle lock. | ⚠️ PARTIAL |
| 11 | No lock across external work; no mutable global factory; no suite-order hang | Gated privilege/integrity/backend/store/outbox prefixes are outside admission locks and no static mutable factory exists. The clock-jump user callback remains lock-held, and one fresh full-coverage execution hung for 240 seconds before a diagnostic repeat passed. | ❌ FAILING |
| 12 | No resource survivor/dispose race; all B1 tasks observed; B2 policy deferred | Covered drain/restart paths dispose only after loop and owned tasks settle; faults are observed by continuations/drain. Detailed late outcome/fault policy remains correctly deferred to B2. | ✅ COMPLIANT for covered B1 paths |
| 13 | Unit4A unchanged/green; no B2/C leakage | Only the two B1 files have content diff; combined focus, full Service, and App.UI pass. | ✅ COMPLIANT |
| 14 | Physical, pre-cleanup, non-tautological lifecycle tests | B1 lifecycle tests use physical identities, exact counters/cardinality, barriers, and production calls before cleanup. No B1 Delay/Yield, tautology, ghost loop, assertion-free lifecycle test, or parallelization disable was found. | ✅ COMPLIANT |

**Acceptance summary**: 9 compliant, 1 partial, 4 failing/untested.

## Required Branch-Hit Audit

| Required branch/outcome | Fresh evidence | Assessment |
|---|---|---|
| Shared Start success | `ConcurrentStart_AwaitsOneSharedInitializationResult`; `StartAsync` 100% line/branch | ✅ |
| Shared Start exception | `ConcurrentStart_SharesFailureAndCleansOneGeneration`; `RunMonitorLoopAsync` exception line 274 hit 4 times | ✅ |
| Shared Start cancellation | No concurrent cancellation test; cancellation catch hits arise from lifecycle Stop paths and do not establish two-caller fan-out | ❌ |
| Gate signal after lock | Source proves every admission RCSA signal occurs after lock syntax; no external reentrant lifecycle test proves the lock is free at dependency entry | ⚠️ static only |
| Synchronous completion | Dedicated test passes; add/removal/drain branches hit | ✅ |
| Concurrent Dispose | Two long-running callers block on one active drain and complete after release | ✅ |
| Start-vs-Dispose | No test starts Dispose while initialization itself is blocked | ❌ |
| Resource restart/disposal | Dedicated physical identity/counter test | ✅ |
| Actual tick | Dedicated exact Channel progression test | ✅ |
| Callback reentrancy | `TamperDetected` path covered; clock/timezone event paths not covered by marker semantics | ⚠️ |

## Partial Spec Compliance

| Spec scenario | B1 contribution | Result |
|---|---|---|
| Runtime integrity — restart and recovery preserve semantics | Physical lifecycle restart/resource replacement and stale periodic/timezone callback rejection pass | ✅ COMPLIANT for covered B1 contribution |
| Offline enforcement — concurrent recovery is serialized | B1 admission/drain/tick ownership is substantially covered, but full shared-Start cancellation, Start-vs-Dispose, and all callback reentrancy/lock boundaries are not | ❌ UNTESTED complete B1 contribution |
| Runtime integrity — late completion/cancellation/fault safety | Explicitly assigned to Unit 4B2 | ➖ DEFERRED |
| Unit4A authority/non-degradation baseline | No content drift; combined/full suites green | ✅ COMPLIANT baseline |

No full Unit4 scenario completion is claimed.

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Cumulative apply progress contains B1 RED/GREEN/remediation history |
| Genuine behavioral RED retained | ⚠️ PARTIAL | Tick hang/sequence, callback self-drain, generation/resource and gate defects are described, but exact raw chronology for final shared-Start/global-callback/resource bytes is incomplete; NETSDK/compile-only evidence is not credited |
| Fresh GREEN | ✅ | Exact matrix 9/9, focus 32/32 twice, combined 120/120, full Service/App.UI green |
| Triangulation | ❌ | Shared Start cancellation and blocked-initialization Start-vs-Dispose are absent; clock/timezone callback reentrancy is not covered |
| Safety net | ❌ | Builds and ordinary suites pass, but the first full coverage run hung and produced no artifact |
| Slice boundary | ✅ | Unit4A unchanged; B2/C implementation absent |

Strict-TDD chronology is not fabricated. Missing raw chronology is a warning; current missing scenarios and the lock-held user callback independently fail B1.

## Test Layer Distribution

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Unit/mock lifecycle | 9 exact B1 cases; 32 AntiTamper total | 1 | Production monitor with mocked ports, reflection-backed physical IDs, and deterministic barriers |
| Production-component harness | Unit4A runtime path within combined 120 | existing | Baseline authority/persistence only |
| E2E/external | 0 | 0 | Correctly excluded |

## Changed File Coverage

| File | Line | Branch | Uncovered normative behavior | Rating |
|---|---:|---:|---|---|
| `AntiTamperMonitor.cs` | 99.00% | 87.50% | Concurrent shared Start cancellation and blocked-initialization Start-vs-Dispose are concurrency outcomes not proven by aggregate hits; clock/timezone callback reentrancy is incomplete | ✅ Excellent aggregate; behavior gaps remain |
| `AntiTamperMonitorTests.cs` | N/A | N/A | Test assembly excluded from product coverage | ➖ |

## Assertion Quality

- No literal tautology, ghost loop, assertion-free B1 lifecycle test, Delay/Yield proof, copied lifecycle implementation, or cleanup-before-assertion was found.
- Tick assertions are ordered and exact before/after release.
- Restart assertions use physical references and independent exact disposal counters.
- The two long-running Dispose callers do not mask the admitted-work barrier and complete after release.
- Omission remains blocking: no shared Start cancellation or blocked-initialization Start-vs-Dispose scenario.
- The callback test proves only `TamperDetected`; it does not cover the other user callback surfaces.

**Assertion quality**: no trivial assertions; mandatory outcome omissions remain.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| One generation lifecycle owner | ✅ | All B1 resource owners are generation-scoped |
| Deterministic shared drain | ✅ covered paths | One memoized drain and locked task set |
| Generation-safe restart | ✅ | Identity, old-callback rejection, lifetime, and disposal cardinality pass |
| No lock over external/user work | ❌ | `OnClockJumpDetected` can execute while the outer lifecycle lock is held |
| One retry owner | ✅ | BackendClient remains unchanged |
| B1/B2/C surgical split | ✅ | No late-result/fault policy or verdict ordering/escalation leakage |

## Issues Found

### CRITICAL

1. **A user callback can execute under the lifecycle lock.** `CheckClockIntegrityAsync` holds `lockObject` while calling `FireClockJump`; the nested method releases only its own reentrant lock scope and invokes `OnClockJumpDetected` while the outer lock is still held. This violates the explicit lock boundary and creates a reentrancy/deadlock risk.
2. **Concurrent Start cancellation fan-out is untested.** Success and failure now pass, but no passing test proves two concurrent callers receive the same initialization cancellation outcome from one generation/operation.
3. **The mandatory Start-vs-Dispose branch is untested.** Current concurrent Dispose coverage begins only after Start has completed and independently admitted work is blocked; it does not race Dispose against blocked initialization.
4. **Fresh coverage exhibited a non-reproducible hang.** The first full coverage command produced no completion or artifact after 240 seconds. A separate diagnostic repeat passed, but retry success is not accepted as proof that the original hang did not occur.

### WARNING

1. `AsyncLocal` safe-reentrancy semantics cover `TamperDetected`/`onTamperDetected`, but not the separate `OnClockJumpDetected` and `TimezoneChanged` callback surfaces.
2. Gate placement is statically correct and all gate paths have hits, but no dedicated external dependency reentrancy assertion proves lifecycle-lock freedom at dependency entry.
3. Strict-TDD behavioral RED summaries exist, but exact retained raw command/timestamp chronology is incomplete for the final gated-admission/concurrency bytes.
4. Existing NU1601/NU1701 warnings and one duplicate xUnit test-ID notice remain; dependency declarations are unchanged.
5. Three out-of-slice paths remain line-ending/status dirty with empty content diffs.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

Unit 4B1 is **not approved for its local autonomous commit boundary**. The gated-admission and concurrent-Dispose remediation closes the prior core implementation defects, and the candidate remains exactly 397/400 with Unit4A/C content unchanged. Approval is still blocked by lock-held user callback execution, missing shared Start cancellation proof, missing blocked-initialization Start-vs-Dispose proof, and the fresh coverage hang.

Cumulative task state remains **9/14**. Unit 4B2, Unit 4C, and final full Unit 4 verification remain mandatory. No commit or B2/C preparation was performed.
