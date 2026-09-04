# Verification Report

**Change**: `remote-signal-integrity` — SDD7 Unit 4B1 Lifecycle Ownership + Resources only  
**Branch**: `feat/sdd7-4b-integrity-lifecycle-races`  
**Base / parent**: `b5a2fee36f224bcb66eed1d560d6652443d4394c` / approved Unit 4A  
**Mode**: Strict TDD, hybrid persistence, fresh independent report-only verification  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**

## Executive Summary

All requested fresh sequential `--no-restore` execution passed: Domain, Service, and portable-PDB Service-test builds; exact Unit4B1 lifecycle tests 3/3; AntiTamper focus 30/30; combined Unit4A+B1 focus 70/70; full Service 1,184/1,184; full App.UI 192/192; and portable-PDB coverage. The content diff is exactly the two expected files and exactly **377/400** changed lines. Unit4A and Unit4C files have no content diff, and no dependency/project/config drift was found.

Unit4B1 nevertheless fails its autonomous acceptance. Concurrent `StartAsync` callers do not share initialization: every caller after the first returns immediately and cannot observe the first initialization result. Periodic work and timezone callbacks use global mutable lifecycle state rather than the captured generation, so an old-generation tick or already-queued timezone callback can be admitted against or mutate a restarted generation. `DrainGenerationAsync` enumerates `OwnedTasks` without the lock used by synchronous completion continuations, creating a real `HashSet` enumeration/removal race. The three added tests do not prove the required second actual tick serialization, stop-before-start, concurrent Start, Start-vs-Dispose, active/same-context Dispose, per-resource IDs/disposal counts, or deterministic shared drain matrix.

**Unit4B1 is not approved for its local commit boundary.** The cumulative task state remains intentionally **9/14**. Unit4B2, Unit4C, and final full Unit4 verification remain mandatory regardless of this result. No fix, commit, B2, C, Unit5, push, PR, or archive work was performed.

## Verification Boundary and Freshness

- Read proposal, all four delta specs, design, latest B1/B2/C re-sliced tasks, complete cumulative apply progress, and all historical Unit4/Unit4A reports, including the full Unit4 FAIL.
- CodeGraph was attempted first by checking the exact worktree. `.codegraph` is absent; focused direct inspection was used and no index was created.
- Inspected the complete changed monitor and test file, exact base diff, lifecycle publication/admission/drain/restart/disposal paths, tests, coverage, branch/base/status, deletions, and drift.
- Historical reports retained their prior SHA-256 values before this report was authored, including full Unit4 FAIL `48A7C75265DFD8C2E334682942E37744C713C7157ABD77E6703EFDF0649F3223` and approved Unit4A `556A4167B9BD8AE509B62F4E6C4ADD517D4C1C89D9A843245673C17FFD24C676`.
- No restore ran. Build/test/coverage outputs are ignored execution artifacts. This report is the only authored filesystem artifact.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| Unit4B1 acceptance items | 12 |
| Compliant | 2 |
| Partial | 3 |
| Failing/untested | 7 |
| Added B1 tests | 3/3 fresh green |

The planned 9/14 state is not an incomplete-task defect for this partial slice.

## Workspace, Diff, Drift, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD | Expected branch; HEAD equals exact parent `b5a2fee36f224bcb66eed1d560d6652443d4394c` |
| Merge base | Exact parent |
| Content diff | Only `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` |
| Unit4A / Unit4C content diff | None in `IntegrityVerdictHandler.cs`, `IntegrityRuntimePathTests.cs`, or `IntegrityVerdictHandlerTests.cs` |
| Deleted files/tests | None |
| Dependency/project/config drift | None |
| `git diff --check` | Exit 0; LF→CRLF notices only |
| Restore | None; every .NET command used `--no-restore` |
| CodeGraph artifact | Absent before and after |
| Worktree status | Five tracked paths appear modified; three out-of-slice paths are line-ending/stat dirt only and have empty content diffs |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 217 | 42 | 259 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 118 | 0 | 118 |
| **Total** | **335** | **42** | **377/400** |

The candidate's 377/400 claim is exact. OpenSpec artifacts are excluded as requested.

## Fresh Build, Test, and Coverage Execution

All commands ran sequentially in the exact worktree.

| Evidence | Exact command | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain portable-PDB build | `dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:WarningLevel=0` | `12:49:22.7900647` → `12:49:24.9937948` | 0 | 0 errors/warnings |
| Service portable-PDB build | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:WarningLevel=0` | `12:49:24.9948225` → `12:49:31.8357373` | 0 | 0 errors; existing NU1601 |
| Service tests portable-PDB build | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:WarningLevel=0` | `12:49:31.8367074` → `12:49:46.1558364` | 0 | 0 errors; existing NU1601/NU1701 |
| Exact Unit4B1 lifecycle tests | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AntiTamperMonitorTests.RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks|FullyQualifiedName~AntiTamperMonitorTests.Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer|FullyQualifiedName~AntiTamperMonitorTests.ConcurrentStopAndDisposeShareLifecycleOwnership"` | `12:49:46.1568352` → `12:49:50.3531087` | 0 | **3 passed** |
| AntiTamper full focus | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AntiTamperMonitorTests"` | `12:49:50.3541086` → `12:49:52.8619308` | 0 | **30 passed** |
| Combined Unit4A+B1 focus | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests"` | `12:49:52.8619308` → `12:49:55.9416900` | 0 | **70 passed** |
| Full Service | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `12:49:55.9426922` → `12:50:08.3598257` | 0 | **1,184 passed**; existing duplicate-ID notice |
| Full App.UI | `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `12:50:08.3608561` → `12:50:11.7363913` | 0 | **192 passed** |
| Portable-PDB coverage | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug -p:DebugType=portable --collect:"XPlat Code Coverage" --results-directory tests\ControlParental.Service.Tests\TestResults\coverage-unit4b1-fresh-20260821 --verbosity minimal -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | `12:50:11.7363913` → `12:50:44.8437771` | 0 | **1,184 passed**; Cobertura produced |

**Coverage artifact**: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-fresh-20260821/0f9b4758-0936-4bad-a38f-61075f96f584/coverage.cobertura.xml`  
**Aggregate coverage**: 54.15% line / 58.23% branch.  
**Changed production class**: `AntiTamperMonitor` 85.71% line / 82.69% branch.

## Unit4B1 Acceptance Matrix

| # | Acceptance | Fresh runtime/static evidence | Result |
|---:|---|---|---|
| 1 | One per-generation owner contains all lifecycle resources; no dead global lifecycle state/timer | `Generation` owns CTS, gate, initial completion, task set, loop/drain, timezone and tick timers. A second mutable global `timezoneTimer` alias remains, and tests depend on it rather than inspecting all owned resources. | ⚠️ PARTIAL |
| 2 | Atomic creation/publication; Start uses captured state and Stop/Dispose cannot miss later publication | Initial generation/timezone/loop publication is locked, but `TickTimer` is created later outside the state lock. Tick callbacks call global `TriggerIntegrityCheckAsync`, and timezone callbacks call global `CheckTimezone`; neither validates the captured generation. | ❌ FAILING |
| 3 | Concurrent/idempotent Start shares one generation and one initialization result | At `StartAsync` lines 159–162, every later caller returns immediately when `isRunning`; it neither awaits nor observes `current.InitialCheck`. A first-call initialization failure/cancellation can therefore be reported as success to concurrent callers. No concurrent-Start test exists. | ❌ FAILING |
| 4 | Stop-before-start; repeated/concurrent Stop share one immutable drain and remain blocked together | Static stop-before-start returns deterministically and later Start is structurally possible. The only new test blocks the initial Start and combines two Stops with `Task.Run(Dispose)`; it does not test never-started Stop→Start, drain identity, independent admitted work, or repeated Stop after completion. | ⚠️ PARTIAL |
| 5 | Add-before-continuation registration; synchronous completion not retained; every admitted task belongs to drain | Add precedes the synchronous continuation and synchronous completion is removable. However, drain enumerates `OwnedTasks.ToArray()` without `lockObject` while completion continuations remove under that lock, so `HashSet` enumeration can race removal. Old-generation ticks can also be admitted into the current generation through the global trigger. | ❌ FAILING |
| 6 | Stop→Start uses fresh resources; old cleanup cannot affect new; exact once disposal | Restart test proves different timezone timer/generation objects and one aggregate `ResourceDisposals` increment. It does not identify/count CTS, gate, tick timer, timezone timer, and loop/task resources independently. A queued old timezone callback can observe new global `isRunning` and mutate new lifecycle state. | ❌ FAILING |
| 7 | Deterministic actual-loop semantic tick serialization | The injected tick branch executes, but the test blocks report call 2, signals tick 2, then immediately asserts `calls >= 2`. That assertion was already true before tick 2, so it never proves the second actual tick was consumed/queued or that progression reached exactly 3 after release. | ❌ UNTESTED |
| 8 | Dispose-before-start, active/repeated/concurrent Dispose, Start-vs-Dispose, no gate race/deadlock/survivor | Basic before-start and repeated fixture Dispose paths pass; one active Dispose runs on `Task.Run`. No Start-vs-Dispose test, same-synchronization-context active Dispose test, per-resource survivor check, or concurrent Dispose matrix exists. Synchronous `GetAwaiter().GetResult()` can block a context needed by dependency continuations. | ❌ UNTESTED |
| 9 | No async-void/fire-and-forget timer work; no lock over backend/store/user work; dispose after full drain | Backend/store work is outside `lockObject`, and periodic checks are registered as owned tasks. However, timer-triggered timezone handling can call `RecordTamperEvent`, whose outbox task is fire-and-forget and not in the generation drain; old queued timezone callbacks are not generation-gated. | ⚠️ PARTIAL |
| 10 | Unit4A authority/identity/persistence unchanged and green | No Unit4A content diff; combined focus 70/70 and full regressions pass. | ✅ COMPLIANT |
| 11 | Slice purity: no B2 effect/fault policy and no C ordering/escalation diff | Content diff is limited to monitor lifecycle and its tests. Handler/runtime-path files have no content diff; no B2 late-result policy or Unit4C logic was added. | ✅ COMPLIANT |
| 12 | Physical IDs/counts/barriers/cardinality; no tautology/sleeps/reimplementation | Tests use production monitor and barriers, but only timezone/generation IDs plus one aggregate disposal counter are checked. Tick cardinality is `>=2`, shared drain identity is not checked, and required lifecycle variants are absent. | ❌ FAILING |

**Acceptance summary**: **2/12 compliant**, 3 partial, 7 failing/untested.

## Correctness and Race Audit

| Area | Finding | Assessment |
|---|---|---|
| Concurrent Start | Later callers return before shared initialization settles | ❌ correctness defect |
| Generation capture | Tick and timezone paths route through monitor-global state rather than a captured generation | ❌ stale-generation defect |
| Owned-task collection | Continuations mutate under lock; drain snapshots the `HashSet` without that lock | ❌ collection race |
| Admission closure | Stop sets `isRunning=false` before drain; normal new admissions close | ✅ static, except stale old callback can target restarted global state |
| Resource disposal | Drain waits loop/tasks before gate/CTS/timer disposal and reuses `Drain` | ✅ core shape; exact resource proof incomplete |
| Lock-held backend/store work | None found in the monitor lifecycle path | ✅ |
| Periodic overlap | Shared gate serializes admitted checks | ✅ static; required second actual-tick runtime proof absent |
| Dispose synchronization | Shared drain avoids obvious double cleanup; synchronous active Dispose deadlock matrix absent | ⚠️ partial |

## Spec Compliance for This Partial Slice

Unit4B1 is lifecycle infrastructure for the runtime-integrity restart/concurrency requirements; B2 and C remain explicitly excluded.

| Spec scenario | Unit4B1 contribution | Result |
|---|---|---|
| Runtime integrity — restart and recovery preserve semantics | Fresh generation objects are partially proven, but stale callback/admission isolation and exact resource cleanup are not | ❌ UNTESTED |
| Offline enforcement — concurrent recovery is serialized | Gate serialization exists, but actual second tick, Start/Stop/Dispose races, and generation isolation are not runtime-proven; final verdict ordering belongs to C | ❌ UNTESTED for B1 contribution |
| Runtime integrity — authority/non-degradation Unit4A scenarios | Unit4A production path remains unchanged and green | ✅ COMPLIANT baseline |

No full Unit4 scenario completion is claimed by this B1 report.

## Strict TDD Audit

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Cumulative apply progress contains Unit4B1 RED/GREEN tables |
| Test file exists | ✅ | `AntiTamperMonitorTests.cs` exists and contains the three added tests |
| Genuine behavioral RED retained | ⚠️ PARTIAL | Apply progress reports a restart test observing no published timer and prior drain/tick behavioral failures. No exact command, timestamp, raw log, or commit chronology is retained for the final 377-line bytes; NETSDK1004/compile-only evidence is not credited |
| Fresh GREEN | ✅ | Exact B1 3/3; AntiTamper 30/30; combined 70/70; full safety nets green |
| Triangulation | ❌ | Required concurrent Start, stop-before-start, actual second tick, resource matrix, and Dispose races are absent |
| Safety nets | ✅ | Builds, full Service/App.UI, coverage, diff, and drift checks passed |
| Candidate evidence accuracy | ⚠️ | Apply claims 33 AntiTamper, 66 combined, and 1,187 Service; fresh current bytes produce 30, 70, and 1,184 respectively |

Strict-TDD chronology is not fabricated. The reported behavioral RED summary is plausible against the flawed parent ownership bytes but is not independently reconstructable from retained raw evidence.

### Test Layer Distribution

| Layer | Added B1 tests | Files | Assessment |
|---|---:|---:|---|
| Unit/mock lifecycle | 3 | 1 | Production monitor with mocked ports and deterministic barriers |
| Production-component integration | 0 | 0 | Unit4A runtime harness remains green but does not cover B1 lifecycle races |
| E2E/external | 0 | 0 | Correctly excluded |

### Changed File Coverage

| File | Line | Branch | Required uncovered behavior | Rating |
|---|---:|---:|---|---|
| `AntiTamperMonitor.cs` | 85.71% | 82.69% | concurrent Start semantics, trigger guards, Stop no-generation branch, exact disposal/resource variants; normative race combinations are absent even where aggregate lines are hit | ⚠️ Acceptable aggregate, insufficient behavioral proof |
| `AntiTamperMonitorTests.cs` | N/A | N/A | Test assemblies are not included in product coverage | ➖ |

Coverage specifically reports `StartAsync` 100%/100%, but branch hits do not prove concurrent callers observe shared initialization. `RunMonitorLoopAsync` is 72.41%/66.66%; the injected branch is hit, yet the second-tick semantic assertion remains non-probative.

### Assertion Quality

| File | Line | Assertion | Issue | Severity |
|---|---:|---|---|---|
| `AntiTamperMonitorTests.cs` | 503, 506 | `calls.Should().BeGreaterThanOrEqualTo(2)` | Already satisfied by the blocked first tick report; does not prove second actual tick admission or serialized progression | CRITICAL |
| `AntiTamperMonitorTests.cs` | 533–540 | timer identity + aggregate `ResourceDisposals` | Does not inspect/disambiguate CTS, gate, periodic tick source, timezone timer, and owned task disposal individually | CRITICAL omission |
| `AntiTamperMonitorTests.cs` | 556–565 | incomplete Stop/Dispose tasks + one disposal counter | Does not prove shared logical drain identity or the full Stop/Dispose admission/resource matrix | CRITICAL omission |

No literal tautology, ghost loop, assertion-free added lifecycle test, or sleep exists in the three added tests. The blocker is missing and non-probative behavioral assertion coverage.

### Quality Metrics

**Compiler/type check**: ✅ Three portable-PDB builds, 0 errors.  
**Package/analyzer corpus**: ⚠️ Existing NU1601/NU1701 and duplicate xUnit ID notice.  
**Dedicated linter**: ➖ Not separately configured/executed.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Checker → authenticated backend → policy → durable enforcement | ✅ | Unit4A unchanged and green |
| One generation owner and deterministic drain | ⚠️ | Owner object exists, but global callbacks and unlocked task-set snapshot break complete ownership |
| Generation-safe restart | ❌ | Old callbacks can address restarted global state; exact resources are not proven |
| One retry owner | ✅ | BackendClient remains the transport retry owner |
| No Unit4C ordering/escalation leakage | ✅ | No handler content diff |
| B1/B2/C surgical split | ✅ scope | No B2/C implementation added; B1 itself remains incomplete |

## Issues Found

### CRITICAL

1. **Concurrent Start does not share initialization.** Later callers return immediately from `isRunning` and can report success while the first initialization is blocked, cancelled, or faulted.
2. **Generation ownership is not closed over callbacks.** Periodic ticks call a global admission method and timezone callbacks use global running/state fields, allowing old-generation callbacks to act on a restarted generation.
3. **Owned-task drain has an unsynchronized collection race.** Completion continuations remove from `OwnedTasks` under `lockObject`; drain enumerates the same `HashSet` without that lock.
4. **The actual-tick test does not prove its named second-tick behavior.** Its `>=2` cardinality is already satisfied before the second tick is signalled; no post-release exact progression is asserted.
5. **Mandatory lifecycle variants have no passing runtime coverage.** Concurrent Start/shared result, Stop-before-start→Start, shared immutable Stop drain, old queued callback isolation, Start-vs-Dispose, same-context active Dispose, and exact per-resource disposal remain uncovered.
6. **Physical resource evidence is insufficient.** One aggregate generation counter cannot prove each CTS/gate/tick/timezone/task resource has the required identity, lifetime, and exactly-once disposal.
7. **Timer-triggered side work is not fully generation-owned.** A timezone callback can launch unowned outbox work through `RecordTamperEvent`, so the generation drain is not a complete timer-effect boundary.

### WARNING

1. Strict TDD has behavioral RED summaries but no retained raw command/timestamp output or independently reconstructable tests-first chronology for the final bytes.
2. Candidate test counts are stale/inaccurate versus fresh execution: 30 rather than 33 AntiTamper, 70 rather than 66 combined, and 1,184 rather than 1,187 Service.
3. `git status` reports three out-of-slice tracked files modified even though their content diffs are empty; this is line-ending/stat worktree dirt and should not be mistaken for Unit4A/C behavior.
4. Existing NU1601/NU1701 warnings and one duplicate xUnit test-ID discovery notice remain; dependency declarations are unchanged.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

Unit4B1 is **not approved for its local commit boundary**. Green aggregate execution, preserved Unit4A behavior, slice purity, and the exact 377/400 budget do not compensate for incorrect concurrent-Start semantics, generation-global callback races, an unsynchronized owned-task drain, and missing/non-probative mandatory lifecycle evidence.

Cumulative state remains **9/14**. Unit4B2, Unit4C, and final full Unit4 verification remain mandatory. No commit or B2/C preparation was performed.
