# Verification Report

**Change**: `remote-signal-integrity` — FINAL FRESH authoritative SDD7 Unit 4B1 Lifecycle Ownership + Resources only  
**Branch**: `feat/sdd7-4b-integrity-lifecycle-races`  
**Base / parent**: `b5a2fee36f224bcb66eed1d560d6652443d4394c` / approved Unit 4A  
**Mode**: Strict TDD, hybrid persistence, independent report-only verification  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**

## Executive Summary

All fresh sequential `--no-restore` execution passed: three portable-PDB builds, intended B1 matrix 8/8, the separately required concurrent-Stop branch 1/1, AntiTamper focus 32/32, combined Unit4A+B1 focus 72/72, full Service 1,186/1,186, full App.UI 192/192, and portable-PDB coverage 1,186/1,186. The content diff is exactly `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs`, exactly **384/400** changed lines. Unit4A/C content, dependencies, projects, and configuration remain unchanged; three out-of-slice status entries are line-ending/stat dirt only.

The remediation closes the prior shared-Start success/cancellation, generation-global callback, owned-task snapshot, tick sequencing, restart identity/disposal, and callback self-drain findings. B1 still fails its mandatory acceptance for two evidence gaps and one source-level ownership violation: concurrent Start has no passing covering test for a shared **failure** result; repeated/concurrent Dispose and Dispose during independently blocked work are not runtime-covered; and initial/periodic admissions plus owned outbox admission can invoke external dependencies while the lifecycle lock is held. The latter violates the explicit no-lock-across-backend/store/outbox/user-callback contract and can serialize or deadlock lifecycle operations on synchronous dependency work.

**B1 local commit approval: NOT APPROVED.** Cumulative state remains intentionally **9/14**. Unit4B2, Unit4C, and final full Unit4 verification remain mandatory. No fix, commit, B2, C, Unit5, push, PR, archive, dependency, config, branch, or worktree operation was performed.

## Boundary, Freshness, and Preservation

- Read proposal, all four specs, design, updated B1/B2/C tasks, cumulative apply progress, historical full Unit4 FAIL, and `verify-report-unit-4b1.md`.
- CodeGraph was attempted first. The worktree has no `.codegraph` index; CodeGraph declined operation. Focused direct inspection followed and no index was created.
- Inspected branch/base/status, full two-file content diff, name-status, numstat, diff-check, deletions, test weakening, line-ending-only dirt, dependency/config drift, required lifecycle branches, tests, and coverage.
- Historical report Git object hashes before final report creation: full Unit4 final `2435f7a652b429e67c6e41cc407ab948e7f4292d`; prior B1 `b66f51d55a3f76befdbcd2f4d5533ba9812827c4`.
- No source/test/task/apply/spec/design/config/dependency/git/worktree/external mutation was performed. Ignored build/coverage output is execution evidence; this report is the sole authored filesystem artifact.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| Mandatory B1 audit items | 13 |
| Compliant | 10 |
| Partial | 1 |
| Failing / untested | 2 |
| Intended B1 matrix | 8/8 passed |
| Additional required concurrent-Stop branch | 1/1 passed |

The planned 9/14 state is not itself a B1 defect. Tasks 4.1–4.3 correctly remain unchecked pending B2, C, and final full Unit4 verification.

## Workspace, Diff, Drift, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD | Expected branch; HEAD is exact base `b5a2fee36f224bcb66eed1d560d6652443d4394c` |
| Content diff | Exactly `AntiTamperMonitor.cs`, `AntiTamperMonitorTests.cs` |
| Deleted files/tests | None |
| Unit4A / Unit4C content diff | None in handler/runtime-path/handler-test files |
| Dependency/project/config drift | None |
| Status-only dirt | `IntegrityVerdictHandler.cs`, `IntegrityRuntimePathTests.cs`, and `IntegrityVerdictHandlerTests.cs`; empty content diff, LF→CRLF notices only |
| Weakening/deletion | No Unit4A test or behavior weakening found; baseline Start tests were replaced by stronger lifecycle tests within B1 |
| `git diff --check` | Exit 0 |
| Restore | None; every .NET command used `--no-restore` |
| `.codegraph` | Absent before and after |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 204 | 75 | 279 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 53 | 52 | 105 |
| **Total** | **257** | **127** | **384/400** |

The candidate's 384/400 accounting is exact. OpenSpec artifacts are excluded.

## Fresh Build, Test, and Coverage Execution

All commands ran sequentially in the exact worktree.

| Evidence | Command summary | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain portable-PDB build | `dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental ... -p:DebugType=portable` | `14:31:21.8079888` → `14:31:24.6396420` | 0 | 0 errors/warnings |
| Service portable-PDB build | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --no-incremental ... -p:DebugType=portable` | `14:31:30.9025438` → `14:31:38.1578545` | 0 | 0 errors; existing NU1601 |
| Service-tests portable-PDB build | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-incremental ... -p:DebugType=portable` | `14:31:44.3521670` → `14:31:58.1636584` | 0 | 0 errors; existing NU1601/NU1701 |
| Exact intended B1 matrix | Exact eight names recorded in final apply evidence | `14:32:15.3771750` → `14:32:20.0940829` | 0 | **8 passed** |
| Required concurrent-Stop branch | `ConcurrentStop_SharesDrainUntilAdmittedWorkReleases` | `14:32:27.6488068` → `14:32:30.2645588` | 0 | **1 passed** |
| AntiTamper focus | `--filter FullyQualifiedName~AntiTamperMonitorTests` | `14:32:44.2568412` → `14:32:47.0588427` | 0 | **32 passed** |
| Combined Unit4A+B1 | IntegrityChecker + AntiTamper + handler + runtime path | `14:32:56.4029706` → `14:32:59.6565755` | 0 | **72 passed** |
| Full Service | Service test project, no filter | `14:33:06.2695817` → `14:33:21.3222268` | 0 | **1,186 passed**; existing duplicate-ID notice |
| Full App.UI | App.UI test project, no filter | `14:33:29.7492121` → `14:33:33.2697875` | 0 | **192 passed** |
| Portable-PDB coverage | Full Service + XPlat Cobertura | `14:33:42.8572094` → `14:34:17.9720223` | 0 | **1,186 passed** |
| Final diff/drift check | diff-check, numstat, name-status, config and out-of-scope diffs, status | `14:35:57.0569155` → `14:35:58.2281505` | 0 | Clean content boundary; expected status dirt |

**Coverage artifact**: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-final-authoritative-20260821/33c59dbe-4a2c-40bf-9fa8-a6c8e1ad66e6/coverage.cobertura.xml`  
**Aggregate**: 54.27% line / 58.49% branch.  
**Changed production class**: `AntiTamperMonitor` 99.01% line / 86.48% branch.

## Complete B1 Behavioral Matrix

| B1 behavior | Passing runtime evidence | Static / physical evidence | Result |
|---|---|---|---|
| Shared Start success | `ConcurrentStart_AwaitsOneSharedInitializationResult` | Start captures `current.InitialCheck.Task`; one allocation/report | ✅ COMPLIANT |
| Shared Start cancellation | `ConcurrentStart_SharesCancellationResult` | Both callers await same initial TCS | ✅ COMPLIANT |
| Shared Start failure | None | `RunMonitorLoopAsync` can set the shared exception, but coverage line 274 has zero hits and no test proves both callers receive it | ❌ UNTESTED |
| Stop before Start then Start | `StopBeforeStart_AllowsLaterStart` | Null-generation Stop path hit | ✅ COMPLIANT |
| Concurrent/repeated Stop drain | `ConcurrentStop_SharesDrainUntilAdmittedWorkReleases`; restart test repeats Stop | `BeginDrainLocked` 100% branch; admissions close before drain | ✅ COMPLIANT |
| Actual loop tick sequencing | `RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks` | Tick1 consumed/call2 blocked; tick2 unconsumed and calls exactly 2; release; tick2 consumed/call3 exactly 3 | ✅ COMPLIANT |
| Synchronous task completion removal | `SynchronousAdmission_IsRemovedBeforeDrain` | add precedes synchronous continuation; add/snapshot/removal share `lockObject` | ✅ COMPLIANT |
| Restart resource identity/lifetime | `Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer` | Distinct G1/G2 CTS, gate, tick timer, timezone timer; exact disposal counters 1/0/1 | ✅ COMPLIANT |
| Captured old callbacks | Same restart test invokes old periodic/timezone entry points | Both require exact generation identity and open admission | ✅ COMPLIANT |
| Start-vs-Dispose / blocked initialization | `ConcurrentStopAndDisposeShareLifecycleOwnership` | Dispose closes admission, drains, removes generation, and post-dispose Start preserves allocation count | ✅ COMPLIANT |
| Repeated/concurrent Dispose and Dispose during independently blocked work | Repeated Dispose after completion is covered; no two concurrent Dispose callers and no active independent-work Dispose test | Shared drain shape is static only for omitted variants | ❌ UNTESTED |
| Callback reentrancy | `TamperCallback_CanRequestStopWithoutSelfDeadlock` | AsyncLocal owner initiates shared drain without unowned dispatch; external Stop observes eventual cleanup | ✅ COMPLIANT |
| Unit4A unchanged / no B2/C leakage | Combined 72/72, full regressions | Only two B1 content files differ | ✅ COMPLIANT |

## Mandatory B1 Audit

| # | Requirement | Fresh finding | Result |
|---:|---|---|---|
| 1 | One generation physically owns lifecycle resources and one drain | `Generation` owns linked CTS, gate, initial TCS, tick source/timer, timezone timer/work, loop, task set, and one memoized drain | ✅ |
| 2 | Atomic publication; Start never awaits mutable/replaced initial state | Resource construction and generation/loop publication occur under `lockObject`; Start retains captured initialization/drain tasks | ✅ |
| 3 | Concurrent Start shares one blocked success/failure/cancellation and one allocation pair | Success and cancellation pass with exact one report/allocation shape; failure has no passing covering test | ❌ UNTESTED normative outcome |
| 4 | Stop-before-start and shared repeated/concurrent drain | Dedicated Stop-before-start and concurrent blocked-work Stop tests pass; admission closes before wait | ✅ |
| 5 | Locked task add/snapshot/removal; synchronous completion not retained | All operations use `lockObject`; dedicated synchronous-completion test passes | ✅ |
| 6 | Actual tick1/tick2 serialization | Deterministic Channel-backed production loop source proves exact pre-release and post-release cardinality; no Delay/Yield | ✅ |
| 7 | Restart identities and exactly-once per-resource cleanup | G1/G2 physical identities and all four counters asserted; old callback entry points rejected; repeated cleanup does not increment | ✅ |
| 8 | Complete Dispose matrix and post-dispose allocation guard | Before-start baseline, blocked initialization, repeated-after-completion, Start-vs-Dispose, and allocation guard exist; concurrent Dispose callers and independently blocked-work Dispose are missing | ❌ PARTIAL/UNTESTED |
| 9 | Generation-captured callbacks and owned timezone/outbox work | Timer entry points capture generation; timezone outbox task enters owned set; no timer fire-and-forget path | ✅ |
| 10 | Callback reentrancy without self-deadlock or unowned dispatch | Dedicated synchronous callback test passes and exact cleanup follows | ✅, but returned reentrant Stop task itself is not asserted against broader drain semantics |
| 11 | No lock across backend/store/outbox/user callbacks; every B1 task observed | `StartAsync` calls `RunMonitorLoopAsync` while holding `lockObject`; its synchronous prefix can invoke privilege/integrity/backend dependencies before the lock is released. `AdmitPeriodicCheck` has the same shape. `RecordTamperEvent` calls `EnqueueToOutboxAsync` while holding `lockObject`, allowing the outbox dependency's synchronous prefix to run under the lifecycle lock. | ❌ FAILING |
| 12 | Unit4A green; no B2/C leakage | Combined/full suites pass; no handler/runtime-path content diff | ✅ |
| 13 | Physical, non-tautological, deterministic tests | IDs, exact counters, cardinality, and barriers are meaningful; no lifecycle Delay/Yield, tautology, ghost loop, or assertion-free lifecycle test | ✅ except omitted outcomes above |

## Correctness and Ownership Audit

| Area | Finding | Assessment |
|---|---|---|
| Generation publication | Complete resources are created before publication; loop and owner published atomically | ✅ |
| Shared Start | Captured TCS provides success/cancellation/failure fan-out structurally | ✅ static; failure runtime proof missing |
| Stop/drain | One memoized drain, closed admission, loop then task-set drain, exact cleanup | ✅ |
| Task-set race | Prior unlocked snapshot defect is closed | ✅ |
| Tick serialization | Exact second-tick progression now probative | ✅ |
| Restart isolation | Old callbacks cannot address or dispose G2; G2 remains live to own Stop | ✅ |
| Disposal | Idempotent cleanup and Start rejection are correct in covered paths | ⚠️ omitted concurrent Dispose/independent-work variants |
| Lock boundary | External dependency synchronous prefixes can execute under lifecycle lock | ❌ correctness/deadlock risk |
| B1 task/fault observation | Owned tasks are observed; B2 late completion/cancellation/fault policy remains correctly deferred | ✅ scope |

## Partial Spec Compliance

| Spec scenario | B1 contribution | Result |
|---|---|---|
| Runtime integrity — restart and recovery preserve semantics | Generation/resource restart and stale callback isolation pass | ✅ COMPLIANT for B1 contribution |
| Offline enforcement — concurrent recovery is serialized | B1 gate/tick/Stop ownership passes, but final verdict ordering remains Unit4C and full scenario remains pending | ⚠️ PARTIAL by contract |
| Runtime integrity — late completion/cancellation/fault safety | Explicitly owned by Unit4B2 | ➖ DEFERRED, not claimed |
| Unit4A authority/non-degradation baseline | Combined focus and full Service remain green | ✅ COMPLIANT baseline |

No full Unit4 scenario completion is claimed.

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Cumulative apply progress contains B1 remediation RED/GREEN history |
| Retained genuine behavioral RED | ⚠️ PARTIAL | Tick hang/sequence, callback self-drain, generation callback/resource, and lifecycle defects are described as behavioral findings. Exact raw command/timestamp chronology for concurrent Start/global-callback/resource final bytes is not retained; NETSDK/compile-only failures are not credited |
| Fresh GREEN | ✅ | 8/8 intended matrix, 1/1 concurrent Stop, 32/32 focus, 72/72 combined, full safety nets |
| Triangulation | ❌ | Shared Start failure and complete concurrent/active Dispose matrix are absent |
| Safety nets | ✅ | Three builds, full Service/App.UI, coverage, diff and drift checks |
| Refactor safety | ❌ | Lifecycle lock can invoke external dependency synchronous work |

Strict-TDD chronology is not fabricated. Missing retained RED detail is a warning; current missing runtime scenarios and lock-boundary defect independently fail B1.

## Test Layer Distribution

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Unit/mock lifecycle | 9 B1 branch/matrix cases | 1 | Production monitor with mocked ports, physical reflection IDs, and deterministic barriers |
| Production-component harness | Unit4A runtime cases in combined 72 | 1 | Baseline authority/persistence only; not claimed as B1 race coverage |
| E2E/external | 0 | 0 | Correctly excluded |

## Changed File Coverage

| File | Line | Branch | Uncovered normative behavior | Rating |
|---|---:|---:|---|---|
| `AntiTamperMonitor.cs` | 99.01% | 86.48% | Shared initialization exception line 274; concurrent Dispose variants are concurrency outcomes not inferable from aggregate hits | ✅ Excellent aggregate; behavior gaps remain |
| `AntiTamperMonitorTests.cs` | N/A | N/A | Test assembly excluded from product coverage | ➖ |

Aggregate coverage does not substitute for a passing covering scenario.

## Assertion Quality

- No tautology, ghost loop, assertion-free lifecycle test, Delay/Yield proof, or copied lifecycle implementation was found.
- Tick assertions are now ordered and exact: tick2 is not consumed and call3 is not entered before release; both occur exactly after release.
- Restart assertions use physical object identities and per-resource exact disposal counters.
- `ConcurrentStopAndDisposeShareLifecycleOwnership` covers one Dispose racing Start, not concurrent Stop+Dispose despite its name.
- `TamperCallback_CanRequestStopWithoutSelfDeadlock` proves eventual cleanup via a later external Stop but does not assert the task returned to the reentrant Stop caller remains tied to the shared drain.

**Assertion quality**: no trivial assertions; two mandatory outcome omissions remain.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| One generation lifecycle owner | ✅ | All B1 resources live on `Generation` |
| Deterministic shared drain | ✅ covered paths | One memoized drain and locked task set |
| Generation-safe restart | ✅ | Identity, stale callback rejection, and disposal cardinality proven |
| No lock over external work | ❌ | Initial/periodic admission and owned outbox admission can synchronously enter dependencies under `lockObject` |
| One retry owner | ✅ | BackendClient remains unchanged |
| B1/B2/C surgical split | ✅ | No late-result/fault policy or verdict ordering/escalation leakage |

## Issues Found

### CRITICAL

1. **The no-lock-across-external-work invariant is violated.** `StartAsync` starts `RunMonitorLoopAsync` under `lockObject`; the async synchronous prefix can call privilege, integrity, backend, policy, store, or user code before returning an incomplete task. `AdmitPeriodicCheck` repeats that pattern. `RecordTamperEvent` starts owned outbox enqueue while holding the same lock. Synchronous dependency work or reentrancy can block/deadlock lifecycle operations.
2. **Concurrent Start failure fan-out is untested.** Success and cancellation are covered, but no passing test drives the shared initialization exception path; coverage confirms line 274 has zero hits. The mandatory three-outcome contract therefore lacks runtime proof.
3. **The mandatory Dispose matrix is incomplete.** No passing test covers two concurrent Dispose callers or Dispose while independently admitted work is blocked. A single Dispose racing blocked initialization plus repeated post-completion Dispose cannot substitute for those normative branches.

### WARNING

1. Strict-TDD behavioral RED summaries exist, but exact retained command/timestamp/raw evidence is incomplete for the final concurrent-Start/global-callback/resource bytes.
2. The reentrant callback test proves eventual cleanup only through a later external Stop; it does not directly prove that the task returned by reentrant Stop preserves the broader shared-drain observation contract.
3. Existing NU1601/NU1701 warnings and one duplicate xUnit test-ID discovery notice remain; dependency declarations are unchanged.
4. Three out-of-slice tracked paths remain status-dirty from line endings although their content diffs are empty.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

Unit4B1 is **not approved as a local autonomous commit candidate**. The remediation successfully closes most prior ownership/resource defects and all fresh execution is green, but mandatory B1 runtime proof is absent for shared initialization failure and complete Dispose concurrency, while source still invokes external dependency synchronous work under the lifecycle lock.

Cumulative state remains **9/14**. Unit4B2, Unit4C, and final full Unit4 verification remain mandatory regardless of this B1 verdict. No commit or B2/C preparation was performed.
