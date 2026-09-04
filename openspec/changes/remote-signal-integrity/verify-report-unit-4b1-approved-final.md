# Verification Report

**Change**: `remote-signal-integrity` — FINAL FRESH authoritative SDD7 Unit 4B1 only  
**Branch**: `feat/sdd7-4b-integrity-lifecycle-races`  
**Base / HEAD / merge-base**: `b5a2fee36f224bcb66eed1d560d6652443d4394c`  
**Mode**: Strict TDD, hybrid persistence, report-only  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**  
**B1 commit approval**: **NOT APPROVED**

## Executive Summary

All fresh `--no-restore` execution completed without failure, hang, or retry: four portable-PDB builds; restored proof set `7/7`; full B1 lifecycle matrix `13/13`; explicit shared-Start outcome audit `3/3`; AntiTamper twice `38/38`; combined Unit4A+B1 in both filter orders `78/78`; full Service `1,192/1,192`; full App.UI `192/192`; and two fresh full-Service coverage hosts `1,192/1,192` each. Both coverage artifacts report `AntiTamperMonitor` at `100%` line / `91.25%` branch coverage.

The candidate is exactly the requested two-file content diff and **391/400** changed CODE+TEST lines: `AntiTamperMonitor.cs` (`201+91`) and `AntiTamperMonitorTests.cs` (`71+28`). No deleted files, project/dependency/config drift, Unit4A behavior diff, or B2/C leakage was found. The restored theory genuinely executes shared success, unique failure, and cancellation; the backend-entry callback proves the lifecycle lock is free through `Monitor.TryEnter` plus property reentry; restart physically compares the complete requested generation resources; both Dispose race families pass; and baseline disposed-Start evidence is restored.

One explicit acceptance proof remains incomplete. `ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock` proves marker absence before, presence during, restoration after, callback cardinality, reentrant Stop/Dispose completion, and a second monitor's own marker. It does **not** capture either generation or assert its exact resource-disposal counters, and it does not assert the first monitor's marker remains null *inside* the second monitor callback. More importantly, reentrant `StopAsync` intentionally returns without awaiting the generation drain when its marker matches, so `await stop` in that test is not evidence that cleanup completed exactly once. Source shape and other tests strongly support correctness, but the user explicitly required runtime proof of clock-path exact cleanup and cross-monitor isolation; aggregate coverage cannot substitute for that scenario assertion.

Tasks remain intentionally **9/14**. Unit4B2, Unit4C, and final full Unit4 verification remain mandatory. No implementation, task, apply, prior-report, dependency/configuration, commit, branch, B2/C, Unit5, PR, or archive mutation was performed.

## Boundary, Freshness, and Preservation

- Read proposal, all four delta specs, design, latest tasks, complete cumulative apply progress, and every prior B1 report: `verify-report-unit-4b1.md`, `verify-report-unit-4b1-final.md`, `verify-report-unit-4b1-approved.md`, and especially `verify-report-unit-4b1-authoritative-final.md`.
- CodeGraph was checked first. `.codegraph` was absent; the no-mutation boundary prohibited initialization, so focused filesystem inspection was used. No index was created.
- Pre/post report preservation hashes: tasks `4ef7f590b4f43c9b2fa8ff91e24fb5327e213bd2`; apply `9350f88c4bb6a5a3dddcff5af2321c8a6eae774c`; prior reports `b66f51d55a3f76befdbcd2f4d5533ba9812827c4`, `0622b799df9c299de51e5402356ce01eaf1b251e`, `83ae0b29b5128e2cbcd97a60c75ed12e64ddd357`, and `369dd535e2af3c0524fb5274bfd381f17a1b8b7a`.
- Build and coverage outputs are ignored execution artifacts. This report is the only authored filesystem artifact.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| Requested B1 acceptance groups | 8 |
| Compliant | 7 |
| Partial / untested | 1 |
| Restored proof set | 7/7 passed |
| Full B1 matrix | 13/13 passed |

The planned 9/14 state is correct for this partial autonomous slice and is not itself a defect.

## Workspace, Diff, Drift, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD / merge-base | Exact requested branch and base |
| Normalized content diff | Exactly `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` |
| Deleted files/tests | None |
| Unit4A / Unit4C content diff | None |
| Status-only dirt | `IntegrityVerdictHandler.cs`, `IntegrityRuntimePathTests.cs`, `IntegrityVerdictHandlerTests.cs`; empty content diff with LF→CRLF notices |
| Dependency/project/config drift | None |
| `git diff --check` | Exit 0 |
| Restore | None |
| B2/C/Unit5 leakage | None |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 201 | 91 | 292 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 71 | 28 | 99 |
| **Total** | **272** | **119** | **391/400** |

## Fresh Build and Test Execution

Every command used `--no-restore`; test commands also used `--no-build` after the fresh builds.

| Evidence | Command summary | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain portable-PDB build | `dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental ... -p:DebugType=portable -p:DebugSymbols=true` | `16:23:03.8641354` → `16:23:06.2321724` | 0 | 0 errors |
| Service portable-PDB build | Same options, Service project | `16:23:06.2421425` → `16:23:13.9451279` | 0 | 0 errors |
| Service-tests portable-PDB build | Same options, Service tests | `16:23:13.9461269` → `16:23:31.5140374` | 0 | 0 errors |
| App.UI-tests portable-PDB build | Same options, App.UI tests | `16:23:31.5150364` → `16:24:02.6677369` | 0 | 0 errors |
| Exact restored proof set | Disposed-Start + three-outcome Start theory + tick/lock + restart + clock callback | `16:24:28.7186531` → `16:24:33.1234760` | 0 | **7 passed** |
| Full B1 matrix | Exact 11 names; theory expands to 3 cases | `16:24:33.1334778` → `16:24:35.5472864` | 0 | **13 passed** |
| AntiTamper pass 1 | `--filter FullyQualifiedName~AntiTamperMonitorTests` | `16:24:47.3749317` → `16:24:49.9525466` | 0 | **38 passed** |
| AntiTamper pass 2 | Same command, fresh host | `16:24:49.9598877` → `16:24:52.5155976` | 0 | **38 passed** |
| Unit4A→B1 | Checker, handler, runtime path, AntiTamper | `16:24:52.5165621` → `16:24:55.5236041` | 0 | **78 passed** |
| B1→Unit4A | Reverse explicit filter order | `16:24:55.5246044` → `16:24:58.4741206` | 0 | **78 passed** |
| Full Service | Entire Service test project | `16:24:58.4751211` → `16:25:11.0213996` | 0 | **1,192 passed** |
| Full App.UI | Entire App.UI test project | `16:25:17.6537127` → `16:25:23.6372493` | 0 | **192 passed** |
| Shared-Start outcome hit audit | Theory only, detailed console logger | `16:27:35.5892509` → `16:27:38.2289020` | 0 | **3 passed**: `success`, `cancel`, `failure` individually listed |

No command failed or hung; no retry was performed.

## Coverage Evidence

| Host | Local start → end | Result | Aggregate | `AntiTamperMonitor` |
|---|---|---|---|---|
| A | `16:25:32.0757334` → `16:26:05.3631892` | `1,192/1,192`, exit 0 | 54.31% line / 58.75% branch | 100% line / 91.25% branch |
| B | `16:26:12.1485901` → `16:26:45.8510283` | `1,192/1,192`, exit 0 | 54.30% line / 58.73% branch | 100% line / 91.25% branch |

Artifacts:

1. `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-approved-final-a-20260821/6a096688-199d-41d4-bd88-3bc30e2bc1e0/coverage.cobertura.xml`
2. `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-approved-final-b-20260821/908ff245-b76d-4d02-8b2c-859267812a5e/coverage.cobertura.xml`

## B1 Acceptance and Required Outcome Matrix

| # | Acceptance | Fresh runtime/static evidence | Result |
|---:|---|---|---|
| 1 | Two concurrent Starts share success, unique failure, and cancellation through one blocked initial generation/resources/outcome/cleanup | Detailed theory host lists all three cases. The test blocks one backend initialization, starts caller B only after entry, verifies one backend call and one allocation, shared failure identity/message, common cancellation type, one physical generation, exact resource counters, and generation removal. | ✅ COMPLIANT |
| 2 | External admission occurs after lifecycle lock release in a real callback | The backend dependency callback executes, obtains `Monitor.TryEnter(lockObject)`, exits it, then reenters `CurrentTimezone`; exact tick test passes. The callback cannot be vacuous because backend call cardinality reaches three around deterministic barriers. | ✅ COMPLIANT |
| 3 | Restart physically separates all G1/G2 resources, drains exactly once, and rejects old callbacks | Restart test compares CTS, semaphore gate, tick/timezone timers, initial TCS, initial operation, loop, and `OwnedTasks`; separately compares memoized drains; asserts per-resource disposal counters; invokes old periodic/timezone entries; proves G2 remains current. | ✅ COMPLIANT |
| 4 | Dispose-before-Start, active Start-vs-Dispose, and two concurrent Dispose callers over independent blocked work | Restored baseline captures allocation count before Dispose and proves rejection before allocation. Start-vs-Dispose blocks initial backend work and observes cancellation/cleanup. Two `LongRunning` Dispose callers block on independent admitted work, then share exact cleanup. | ✅ COMPLIANT |
| 5 | Clock callback marker scope/isolation, reentrant Stop/Dispose, and exact cleanup once | Marker absent/during/after and callback count pass; second monitor observes its own marker; reentrant Stop/Dispose does not deadlock. Missing: first marker assertion inside second callback and exact per-generation cleanup counters on the clock path. Reentrant `StopAsync` returns without awaiting drain by design, so `await stop` is not exact cleanup proof. | ❌ UNTESTED remainder |
| 6 | Baseline tests restored without weakening or duplication replacing stronger evidence | Disposed-Start baseline is restored and strengthened. Start success/already-running and running Stop remain. Concurrent Start is one three-case theory rather than weaker duplicated tests. Removed clock-jump baseline is subsumed behaviorally by the reentrant callback test. | ✅ COMPLIANT |
| 7 | Entire B1 architecture remains correct and slice-pure | Source inspection confirms atomic generation publication, RCSA-gated external start after lock, lock-consistent task add/remove/snapshot, deterministic actual ticks, stop/restart/dispose matrices, generation-captured timezone/outbox work, callback reentrancy marker, one memoized drain, no global seam, and no B2/C leakage. No new source race was found. | ✅ COMPLIANT |
| 8 | No sleep/Yield/retry/parallel-disable as B1 proof | B1 tests use RCSA barriers, Channel ticks, physical identities, exact counters, and dedicated threads. `Task.Delay(Timeout.Infinite, token)` is cancellation-controlled blocking, not timing proof. The pre-existing 100 ms outbox baseline is not used for B1 lifecycle acceptance. | ✅ COMPLIANT |

**Acceptance summary**: **7 compliant**, **1 untested remainder**.

## Correctness and Ownership Audit

| Area | Finding | Assessment |
|---|---|---|
| Atomic publication | Generation and initial admission are published under one lock; gate releases afterward | ✅ |
| Shared Start | All callers await the same `InitialCheck`; all three outcomes run | ✅ |
| Owned tasks | Add/removal/snapshot use `lockObject`; synchronous completion is removed | ✅ |
| Tick semantics | Exact two-tick queue/consume/cardinality sequence runs | ✅ |
| External lock boundary | Real backend callback acquires lifecycle lock and reenters a property | ✅ |
| Restart | Complete requested object identity matrix and old callback rejection run | ✅ |
| Dispose | Before-start, initial-work race, independent-work race, repetition, and two callers run | ✅ |
| Callback dispatch | Clock/timezone/tamper callbacks are outside lifecycle state locks and marker restoration uses `finally` | ✅ static/runtime scope |
| Clock cleanup evidence | The callback test does not await the physical drain or assert exact counters; cross-instance null isolation is not asserted during the second callback | ❌ evidence gap |
| Slice purity | Unit4A unchanged; no B2/C/final Unit4 claim | ✅ |

No source-level correctness defect was found in the current B1 bytes. The blocking finding is missing mandatory runtime evidence, not a claim that cleanup is currently implemented incorrectly.

## Partial Spec Compliance

| Spec scenario | B1 contribution | Result |
|---|---|---|
| Runtime integrity — restart and recovery preserve semantics | Full generation/resource restart and old-callback isolation pass; clock reentrant exact-cleanup/isolation assertion remains incomplete | ⚠️ PARTIAL |
| Offline enforcement — concurrent recovery is serialized | Start outcomes, admission lock boundary, tick, Stop, restart, and Dispose matrices pass; final ordering remains Unit4C | ✅ COMPLIANT for B1 contribution |
| Runtime integrity — late completion/cancellation/fault safety | Initial cancellation-before-success is covered; broader completion/fault policy remains explicitly Unit4B2 | ➖ DEFERRED remainder |
| Unit4A authority/non-degradation baseline | No content drift; both combined orders and full regression pass | ✅ COMPLIANT baseline |

No full Unit4 scenario completion is claimed.

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Cumulative apply progress contains B1 RED/GREEN/remediation history |
| Test file exists | ✅ | Changed test file exists and executes |
| Genuine behavioral RED retained | ⚠️ PARTIAL | Behavioral tick, callback, admission, generation, and lifecycle failures are described; exact raw tests-first logs/chronology for every final byte are not retained |
| Fresh GREEN | ✅ | Proof `7/7`, matrix `13/13`, outcomes `3/3`, focus `38/38` twice, combined `78/78` twice, full suites and two coverage hosts |
| Triangulation | ❌ | Clock-path exact cleanup and active cross-monitor null isolation are not directly asserted |
| Safety net | ✅ | No command failed, hung, or required retry |
| Slice boundary | ✅ | Exact 391/400 two-file candidate; no B2/C leakage |

Missing raw logs alone are warning-only. The explicit missing clock-path assertion independently blocks approval.

## Test Layer Distribution

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Unit/mock lifecycle | 13 B1 matrix outcomes; 38 AntiTamper total | 1 | Production monitor with mocked ports, barriers, channels, reflection-backed physical identities/counters |
| Production-component harness | Unit4A runtime paths within combined 78 | Existing | Authority/persistence baseline only |
| E2E/external | 0 | 0 | Correctly excluded |

## Changed File Coverage

| File | Line | Branch | Uncovered normative behavior | Rating |
|---|---:|---:|---|---|
| `AntiTamperMonitor.cs` | 100% | 91.25% | Exact clock-reentry resource cleanup and cross-instance marker isolation are concurrency/assertion outcomes not established by line hits | ✅ Excellent aggregate; behavioral gap remains |
| `AntiTamperMonitorTests.cs` | N/A | N/A | Test assembly excluded from product coverage | ➖ |

## Assertion Quality

- No tautology, ghost loop, assertion-free B1 lifecycle test, copied implementation, `Task.Yield`, retry loop, timing sleep as lifecycle proof, or parallel-disable directive was found.
- Start theory, lock-state callback, tick, restart, and Dispose assertions are physical and barrier-controlled.
- `ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock` does not assert exact resource counters and does not assert the first monitor's marker remains null during the second monitor callback.

**Assertion quality**: **1 CRITICAL omission**, 0 trivial assertions.

## Quality Metrics

**Compiler/type check**: ✅ Four portable-PDB builds, 0 errors.  
**Analyzer/package corpus**: ⚠️ Existing NU1601/NU1701, StyleCop/analyzer warnings, and one duplicate xUnit test-ID discovery notice; no dependency/config drift.  
**Dedicated linter**: ➖ Not separately configured.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| One generation lifecycle owner | ✅ | Complete requested resource set is generation-scoped and physically compared |
| Deterministic shared drain | ✅ source/matrix | One memoized drain and lock-consistent task set |
| Generation-safe restart | ✅ | G1/G2 identities, old callbacks, lifetime, and exact counters pass |
| No lifecycle lock over external/user work | ✅ | RCSA gates and callback placement are outside lock; backend callback proves lock availability |
| One retry owner | ✅ | BackendClient unchanged |
| B1/B2/C surgical split | ✅ | No completion-fault or verdict-ordering/escalation leakage |
| Clock callback exact-cleanup proof | ❌ | Correct source shape is not accompanied by the explicitly required runtime counter/isolation assertions |

## Issues Found

### CRITICAL

1. **Clock callback exact cleanup and cross-monitor isolation are not fully runtime-proven.** The test proves reentrancy and marker scope on each monitor, but it never captures the callback generation or asserts its CTS/gate/tick/timezone disposal counters. Because reentrant `StopAsync` skips awaiting the drain, `await stop` is not proof of physical cleanup completion. The second callback also never asserts the first monitor's marker remains null during that callback. These are explicit acceptance requirements, so source inspection and 100% line coverage cannot replace the omitted assertions.

### WARNING

1. Strict-TDD behavioral RED history is honest but lacks complete retained raw logs/timestamps and independently reconstructable tests-first chronology for every final remediation byte.
2. Existing package/analyzer warning corpus and the duplicate xUnit test-ID notice remain; declarations are unchanged.
3. Three out-of-slice paths remain status-dirty from line endings despite empty content diffs.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

**B1 commit approval: NOT APPROVED.** The evidence restoration closes the prior shared-Start, admission-lock, complete restart-resource, Dispose-before-Start, active Start/Dispose, and concurrent Dispose gaps, and every fresh executable run is green. Approval is still blocked by the explicitly required clock-callback exact-cleanup and active cross-monitor isolation proof that the current test does not assert.

Cumulative state remains **9/14**. Unit4B2, Unit4C, and final full Unit4 verification remain mandatory regardless of this verdict. Do not commit this B1 candidate and do not prepare B2/C from this report.
