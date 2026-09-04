# Apply Progress: Offline Enforcement Safety Loop

## Current Status

Implementation complete at 10/10 — Phase 2, Phase 3, and tasks 4.1-4.3 are complete. A post-verification corrective slice also addresses all six CRITICAL findings from the current FAIL report. The production path now composes a session safety loop, generation-correlated command/result IPC (including workstation lock), authoritative heartbeat and time-change ordering, exact foreground targets, durable overlay intent, revision-aware policy caching, and non-overlapping schedulers. The expanded conservative changed-scope calculation is 1,419/1,754 lines (80.90%), with 388/532 branches (72.93%) reported separately. A fresh safe supported-Windows run is 5/5 PASS; a fresh performance run remains finite and bounded; and two consecutive current full-solution runs each pass all 1,289 tests. This apply closeout does not declare final verification PASS; the next phase is `sdd-verify`.

## Completed Tasks

- [x] 1.1 Contracts / port closure (previously completed; see `tasks.md`).
- [x] 2.1 Coordinator ordering, latest-dirty coalescing, and critical admission.
- [x] 2.2 Strict-TDD saturation, reconnect, stale-result, and duplicate-command matrix.
- [x] 2.3 Complexity, architecture, flow, logic, and optimization review.
- [x] 3.1 Generation replacement, typed ACK/result correlation, and bounded retry/timeout behavior.
- [x] 3.2 Typed overlay lifecycle, PID-reuse-safe exact-target termination, and workstation-lock outcomes.
- [x] 3.3 Canonical AppId propagation, durable semantic issues, restoration, and authoritative health plumbing.
- [x] 4.1 Restart convergence, same-app threshold crossing, stale heartbeat, agent death, child-admin, and PID-reuse managed integration scenarios.
- [x] 4.2 Supported-Windows runtime evidence for five safety flows plus greater-than-80-percent changed-scope line coverage and branch reporting.
- [x] 4.3 Canonical closeout with finite allocation, queue, timer-drift, and processing-latency evidence.

## Phase 3 Progress

- `AgentCommandPort` rejects duplicate command IDs, creates a fresh command ID for each bounded retry, retains the intent version, divides the remaining deadline across attempts, and rejects expired or mismatched results.
- Replacing an agent connection completes old pending commands as `ConnectionReplaced`; late results cannot mutate the new generation.
- `OverlayIntentPolicy` persists desired intent through its store contract and reconciles show, replace, and clear against the current generation/version.
- `ProcessTerminator` consumes only `ObservedProcessTarget`, validates PID/session/start time and protected-process policy, waits asynchronously, and revalidates identity before forced termination.
- `OverlayWindow.Apply` and `SessionAgentHost.MapLockResult` expose typed managed/native outcomes rather than inferring success from dispatch.
- `ForegroundWatcher` now separates canonical `AppId` from ephemeral exact-process evidence, and `EnforcementEngine` foreground evaluation no longer performs process, overlay, or lock side effects from `AppId` alone.
- Semantic issue keys deduplicate repeated evidence by session/type/cause and preserve independent causes. `FileIssueStore` atomically persists a versioned bounded document, retains first/last evidence and revisions, and persists auditable resolution evidence.
- `EnforcementLevelMonitor` restores active issues before evaluation, persists authoritative probe transitions, resolves only from matching recovery evidence, and emits a critical restore issue rather than replacing corrupt state with a healthy empty document.
- `ServiceHealthMonitor` now requires successful restore, a fresh real heartbeat, current critical-action confirmation, no blocking issues, and a healthy runtime-security verdict. Foreground messages no longer refresh liveness; agent death invalidates heartbeat and confirmation. `ServiceRecoveryManager` consumes this projection instead of declaring health from zero counters or a successful relaunch.
- `ForegroundWatcher` compares canonical identities ordinally, keeps exact targets separate, and fixes fallback polling state so the same canonical AppId is not repeatedly republished.

## Phase 3 TDD Cycle Evidence

| Scope | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 3.1 retry/correlation | `SessionCoordinatorTests.cs` | Port/coordinator unit | Prior focused matrices green | Constructor/retry tests failed with `CS1739` because bounded-attempt configuration did not exist | Focused Service matrix passed 65/65 after adding bounded retries, fresh command IDs, deadline partitioning, and current-generation ACK checks | Timeout/replacement/stale/duplicate/retry cases cover distinct outcomes | Retry and result lookup remain expected O(1); waits are asynchronous and attempts are capped at three |
| 3.2 overlay/exact target/native outcome | `ExactTargetAndOverlayPolicyTests.cs`, `OverlayManagerTests.cs` | Unit/native-adapter seam | Prior focused matrices green | Tests failed while replace-overlay, overlay payloads, exact-target identity checks, `Apply`, and lock-result mapping were absent | Service matrix passed 65/65 and SessionAgent matrix passed 32/32 after implementing the typed adapters | Show/replace/clear, PID mismatch/reuse, harmless absence, native failure, and confirmed paths use different inputs/outcomes | Native seams isolate Windows calls; forced termination revalidates process identity and uses cancellable async waits |
| 3.3 canonical identity/durable semantic issues/health | `DurableIssueStoreTests.cs`, `ServiceHealthMonitorTests.cs`, `EnforcementLevelMonitorTests.cs`, `EnforcementEngineTests.cs`, `ForegroundWatcherTests.cs` | Unit/persistence integration | Service 75/75; SessionAgent 16/16 | Service RED failed with `CS0246` for missing `IIssueStore`; health/recovery REDs showed success counters could invent healthy state; SessionAgent RED failed because ordinal observation comparison did not exist | 82 focused Service tests and 17 focused SessionAgent tests pass after atomic persistence/restore, semantic resolution, authoritative health gates, agent-death invalidation, and ordinal AppId/target comparison | Restart/resolution, corrupt document, 32-way concurrent duplicate evidence, active-versus-resolved restore, authoritative probe recovery, stale/death health, case-distinct AppId, and changed-target paths cover alternate branches | Persistence uses one async gate and bounded 1024-record state; health projection is isolated behind `IAuthoritativeHealthSink`; ordinal observation comparison is a pure helper |

## Phase 3 Verification Evidence

| Command/scope | Result |
|---|---|
| Focused Service build and tests | 82 passed, 0 failed (task 3.3 matrix; prior 65/65 evidence preserved above) |
| Focused SessionAgent build and tests | 17 passed, 0 failed for `ForegroundWatcherTests` (prior Phase 3 matrix 32/32 remains historical evidence) |
| Full Service suite | 910 passed, 0 failed |
| Full SessionAgent suite | 106 passed, 0 failed |
| Focused coverage collection | Completed for both projects; branch data recorded below |

Selected managed coverage from the focused Cobertura reports:

| Class | Line coverage | Branch coverage |
|---|---:|---:|
| `AgentCommandPort` | 95.74% | 86.36% |
| `OverlayIntentPolicy` | 100% | 100% |
| `EnforcementEngine` | 91.17% | 68.18% |
| `EnforcementLevelMonitor` | 92.66% | 77.50% |
| `ProcessTerminator` | 74.74% | 46.15% |
| `OverlayWindow` | 53.35% | 25.51% |

These are whole-class collector values, not a claim that final changed-scope closure coverage is satisfied. The required greater-than-80-percent changed-scope calculation and additional native-failure branch coverage remain correctly open under task 4.2.

### Task 3.3 Final Coverage Evidence

Focused Cobertura reports:

- Service: `tests/ControlParental.Service.Tests/TestResults/ee1b6c89-7f4e-4aa7-aff2-7f3d0462242d/coverage.cobertura.xml`
- SessionAgent: `tests/ControlParental.SessionAgent.Tests/TestResults/d2548b09-4a59-45a8-b1c4-4f7c763985cf/coverage.cobertura.xml`

File-level collector aggregation (async state-machine classes merged by source line):

| File | Lines | Branches |
|---|---:|---:|
| `EnforcementEngine.cs` | 158/174 (90.80%) | 29/42 (69.05%) |
| `EnforcementLevelMonitor.cs` | 355/466 (76.18%) | 74/108 (68.52%) |
| `FileIssueStore.cs` | 111/131 (84.73%) | 31/42 (73.81%) |
| `ServiceHealthMonitor.cs` | 160/168 (95.24%) | 53/60 (88.33%) |
| `ServiceRecoveryManager.cs` | 77/152 (50.66%) | 12/28 (42.86%) |
| `ForegroundWatcher.cs` | 6/262 (2.29%) | 4/70 (5.71%) |

The focused behavior added in 3.3 is exercised, but whole-file coverage is not above 80% across every touched legacy/native class. This is not represented as satisfying the Phase 4 changed-scope closure gate. Existing whole-class debt also remains explicit: `ProcessTerminator` 74.74% lines / 46.15% branches and `OverlayWindow` 53.35% lines / 25.51% branches. Phase 4.2 must calculate final changed-scope coverage and add managed/native branch evidence without fabricating Windows runtime results.

## Phase 3.3 Quality Review

- **Idempotence/restart:** repeated semantic evidence updates one key; resolution is revisioned and survives a new store instance; resolved records do not restore as active.
- **Corruption safety:** malformed or unsupported documents throw, create a restore-failure issue, and keep health degraded; the corrupt source is not overwritten by a healthy empty document.
- **Concurrency/bounds:** one asynchronous semaphore serializes load/upsert/resolve/atomic replacement; dictionary lookup is expected O(1); state is capped at 1024 records and evicts only the oldest resolved record before refusing additional unbounded active state.
- **Health authority:** foreground activity is not heartbeat evidence; stale heartbeat and agent death degrade health; relaunch success and cleared counters cannot establish health; current critical confirmation must be supplied by the authoritative command path.
- **Canonical identity:** `AppId` is compared with `StringComparison.Ordinal` and passed unchanged to usage/policy/reporting; PID/session/start time remain an independent ephemeral target.
- **Blocking/overlap:** durable file I/O, gate acquisition, and tests use async waits. No blocking process wait was added. `ForegroundWatcher` retains its pre-existing dedicated fallback polling thread sleeps; they do not run on the serialized enforcement/health path. Timer/allocation/runtime measurement remains Phase 4 evidence.

## Phase 2 Unblocker Delta

- Critical work now uses the bounded channel exclusively, so safe foreground/tick observations cannot consume critical admission capacity.
- Foreground and tick inputs use one latest-value owed slot each and wake the single consumer through a semaphore.
- The consumer always selects queued critical work before owed foreground and tick work.
- Owed-slot state and latest values are transferred atomically under one lock, preventing lost idle accounting during concurrent replacement.
- The bounded-queue test now fills the queue with critical work before verifying producer backpressure; safe observation slots are tested independently of critical capacity.

## TDD Cycle Evidence

| Scope | Test File | Layer | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|
| Phase 2 saturation unblocker | `tests/ControlParental.Service.Tests/SessionCoordinatorTests.cs` | Coordinator/race unit | `SaturatedObservationsPreserveCriticalAdmissionAndOwedInputs` hung until the 15-second blame timeout while the awaited critical post was blocked by an observation marker | 12/12 `SessionCoordinatorTests` passed after separating critical capacity from owed observation slots | Existing foreground/tick latest-state, bounded-critical-backpressure, serialization, reconnect, stale-result, and duplicate-result cases passed | Pending marker and latest-value transfer were made atomic; 12/12 tests remained green |
| 2.1 ordered latest-dirty slots | `tests/ControlParental.Service.Tests/SessionCoordinatorTests.cs` | Coordinator/race unit | `CoalescedInputsPreserveOwedOrderAndLatestState` failed: expected `latest-tick, foreground`, actual `foreground, latest-tick` | Added constant-space first-owed sequence metadata and O(1) slot selection; focused test passed | The existing foreground-first saturation case and the new tick-first/latest-value case cover both ordering directions; final matrix 14/14 | Replaced kind-biased drain logic with `TakeNextPending`; renamed colliding locals discovered by source build; 14/14 remained green |
| 2.2 reconnect/stale/duplicate matrix | `tests/ControlParental.Service.Tests/SessionCoordinatorTests.cs` | Port/coordinator unit | Cumulative Phase 1 RED was CS0246 before the port/contracts existed; saturation RED is retained above | Reconnect replacement, wrong session/generation, stale intent, duplicate result, and concurrent duplicate command all pass | Added `ConcurrentDuplicateCommandIsRejectedWithoutDuplicateSend`; it characterizes the already-green Phase 1 deduplication behavior and proves one send only | Kept deduplication in the O(1) command dictionary; no production port change or Phase 3 work was introduced |
| 2.3 optimization-risk review | `tests/ControlParental.Service.Tests/SessionCoordinatorTests.cs` | Structural plus coordinator/race unit | Uses the 2.1 ordering RED because over-coalescing was the optimization defect under review | Both owed-order directions, critical priority, latest-state replacement, bounded backpressure, and single-reader serialization pass | 14 focused cases cover independent sessions, no overlap, saturation, reconnect, timeout, stale/mismatch, duplicate result, and duplicate command | Review confirmed constant-space slots, expected O(1) operations, async waits only, and no competing authority in this passive unit |

## Focused Evidence

All four commands below were executed under an independent 60-second parent-process timeout that would terminate the complete process tree and report exit code 124. None reached that timeout.

| Variable isolation | Command | Result | Process exit | Wall time |
|---|---|---|---:|---:|
| Normal | `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --filter FullyQualifiedName~SessionCoordinatorTests --verbosity minimal` | 12 passed | 0 | 2.610 s |
| Coverage, no blame | `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --filter FullyQualifiedName~SessionCoordinatorTests --verbosity minimal --collect:"XPlat Code Coverage"` | 12 passed; Cobertura written | 0 | 18.906 s |
| Blame, no coverage | `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --filter FullyQualifiedName~SessionCoordinatorTests --verbosity minimal --blame-hang --blame-hang-timeout 15s` | 12 passed; blame reported all tests completed | 0 | 2.938 s |
| Coverage plus blame | `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --filter FullyQualifiedName~SessionCoordinatorTests --verbosity minimal --collect:"XPlat Code Coverage" --blame-hang --blame-hang-timeout 15s` | 12 passed; Cobertura written; blame reported all tests completed | 0 | 18.891 s |

The approximately 16-second post-test delay follows the coverage collector alone: adding blame changes neither the delay nor successful process shutdown. No `testhost`, `vstest.console`, or `datacollector` process remained after the matrix. This disproves a coordinator/test resource leak in the reproduced runs and does not justify a production or versioned test-configuration change. The earlier 30-second session-timeout observation is treated as runner/collector timing from that invocation, not as a reproducible project defect.

Canonical stable focused coverage command:

`dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --filter FullyQualifiedName~SessionCoordinatorTests --verbosity minimal --collect:"XPlat Code Coverage"`

Historical unblocker Cobertura evidence for `ControlParental.Service.SessionEnforcementCoordinator` before the final ordering branch was added: 98.75% line coverage and 100% branch coverage. The authoritative current result is recorded below.

No RED/GREEN/REFACTOR cycle was started for this diagnostic because no project defect reproduced and no production or test code was modified.

## Phase 2 Final Focused Evidence

All final commands used an independent 60-second parent-process timeout that terminates the process tree and returns 124 on expiry. Neither command reached the timeout.

| Command | Result | Wall time |
|---|---|---:|
| `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --filter FullyQualifiedName~SessionCoordinatorTests --verbosity minimal` | 14 passed, 0 failed | 2.376 s |
| `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --filter FullyQualifiedName~SessionCoordinatorTests --verbosity minimal --collect:"XPlat Code Coverage"` | 14 passed, 0 failed; Cobertura `b23114f9-2340-4614-9016-fb1544060e60` | 18.636 s |

Current focused coverage:

| Changed-scope class | Line coverage | Branch coverage |
|---|---:|---:|
| `SessionEnforcementCoordinator` | 96.29% | 95.00% |
| `AgentCommandPort` (matrix dependency, unchanged in this unit) | 100% | 100% |

## Phase 2 Quality Review

- **Complexity and allocation:** critical admission is a bounded channel operation; result correlation is an expected O(1) dictionary lookup; foreground/tick posting and selection are O(1) over two fixed slots. No user/application collection, database materialization, scan, or per-event cache rebuild exists in this path. Memory is bounded by channel capacity plus one foreground and one tick slot.
- **Ordering and flow:** one consumer awaits and executes one delegate at a time. Restore/result work uses the critical FIFO and is selected before coalescible work. Foreground/tick slots retain the sequence assigned when first owed while replacements update only the latest delegate. Work posted during execution becomes newly owed and is drained later.
- **No overlap:** `SameSessionWorkIsSerialized` proves maximum concurrency is one; independent session coordinators can progress independently. Timers do not execute enforcement in this unit.
- **No blocking waits:** production uses `WriteAsync`, `WaitAsync`, and awaited delegates. There is no `.Wait()`, `.Result`, `WaitForExit`, synchronous process wait, or blocking timer callback in the reviewed coordinator/port path.
- **Clean architecture:** the coordinator owns sequencing only; `AgentCommandPort` owns generation/correlation only. This passive unit adds no database, IPC framing, native action, health, or policy dependency and creates no competing enforcement authority.
- **Optimization risks:** tests cover both foreground-first and tick-first owed ordering, latest-value replacement, saturation, critical priority, reconnect invalidation, stale/mismatched results, duplicate results, and duplicate concurrent commands. Allocation/latency benchmarks and runtime timer-drift evidence remain correctly deferred to Phase 4.

## Deviations and Issues

None from the design. A source build exposed a pre-existing C# local-name collision in `TakePending`; the refactor renamed the two locals while preserving behavior before the behavioral RED was recorded. Existing repository analyzer/package warnings remain outside this work unit.

## Remaining Tasks

- None. Final verification remains intentionally separate under `sdd-verify`.

## Historical Task 4.3 Pre-Measurement Audit — Superseded

This pre-measurement audit is retained for chronology only. Its blocked state and
1,275-test count are superseded by the canonical Task 4.3 evidence below.

| Check | Current truth |
|---|---|
| Task state | Historical: 9/10 complete; 4.3 was pending |
| Build | 0 errors; 229 warnings |
| Tests | 1,275/1,275 passed; 0 failed; 0 skipped |
| Changed-scope coverage | 1,148/1,430 lines (80.28%); 323/448 branches (72.10%) |
| Windows runtime | 5/5 PASS; artifact JSON validated; destructive runtime actions were not repeated |
| Historical verification | The 2026-08-10 FAIL report is explicitly labeled historical and superseded in `verify-report.md` |
| Closure blocker | Historical: measured bytes/event, queue high-water, timer drift, and p50/p95 processing results were not yet present |

### Historical Safe Closeout Smoke

| Command | Result |
|---|---|
| `dotnet build ControlParental.sln --no-restore --verbosity minimal` | Exit 0; 0 errors; 229 warnings; 15.168 s |
| `dotnet test ControlParental.sln --no-restore --no-build --verbosity minimal` | Exit 0; Domain 95, Service 924, SessionAgent 110, App.UI 146; 1,275 passed; 16.137 s |
| Canonical `changed-scope-coverage.py` command with the task-4.2 Cobertura files and declared production file set | 1,148/1,430 lines (80.28%); 323/448 branches (72.10%) |
| Structured runtime-evidence validation | Required five flows present and PASS; timestamps valid |
| `git diff --check` | Exit 0; no whitespace errors |

### Conceptual Boundary and Rollback in the Current Tree

No commit or PR exists for these units. The boundaries below describe selective
reversal in the current working tree; they are not Git object references.

| Unit | Boundary | Conceptual rollback |
|---|---|---|
| 1 — contracts/port | Correlated contracts, replaceable command port, and passive coordinator seams | First deactivate dependent Unit 2/3 composition, then reverse the contract/port registrations and additions. Preserve compatible durable state and never map an unconfirmed action to success. |
| 2 — coordinator/scheduling | Serialized session authority, bounded mailbox, foreground routing, and same-app ticks | Disable `SessionSafetyLoop` composition and route only prior usage accumulation while keeping health explicitly degraded. Do not restore direct critical-action authority or fire-and-forget success reporting. |
| 3 — native actions/health/runtime/closure | Correlated agent execution, exact target, overlay intent, authoritative health/issues, safe runtime harness, coverage, and closeout artifacts | Disable correlated native command dispatch and authoritative publisher activation together; retain durable intent/issue documents for compatibility and audit. The runtime harness and evidence tooling can be removed independently because they do not alter production behavior. |

Rollback order is Unit 3 → Unit 2 → Unit 1. Partial reversal must not leave two
enforcement authorities active. Stored intent/issues remain data, not proof of
current protection, and the prior reporting path may be restored only with
explicit degraded-health disclosure.

## Workload Boundary

Autonomous feature-branch-chain Unit 2 slice: coordinator/mailbox ordering plus focused TDD and review only. Approximate code delta for this continuation is 43 net authored lines (well below 800), plus this cumulative evidence update. Exact process targeting, overlays, threshold integration, native actions, restore persistence, and health were not advanced.

Autonomous feature-branch-chain Unit 3 implementation slice now includes tasks 3.1–3.2 and the non-durable portion of 3.3. It stops before claiming durable health/issue completion or Phase 4 runtime closure. No commit or PR was created.

Autonomous final Phase 3 delta completes 3.3 only: durable semantic issue persistence/restoration, authoritative health projection, and canonical identity observation. It does not advance 4.x, does not claim Windows runtime evidence, and creates no commit or PR.

## Phase 4.1 Integration-Unblocker Progress

### Cause and correction by verified finding

- **Passive production seams / discarded exact target:** `ControlParentalService` now creates `SessionSafetyLoop` for the active session. Foreground messages retain `ObservedProcessTarget`; policy evaluation, overlay reconciliation, and exact-target termination run through the serialized coordinator. A one-second non-overlapping owed tick reevaluates the same AppId.
- **Uncorrelated critical IPC:** `AgentAuthority`, `AgentCommandRequest`, and `AgentCommandCompleted` transport session, generation, command, and intent identity. `AgentCommandPort` accepts only current correlated results; agent replacement/death invalidates pending authority.
- **Misleading heartbeat:** `AgentHeartbeat` now carries session and generation. Only exact current authority refreshes `ServiceHealthMonitor` and `EnforcementLevelMonitor`; foreground activity is not heartbeat evidence.
- **`async void` / fire-and-forget enforcement:** service foreground handling posts synchronously to the coordinator; the production agent uses a single-reader async work queue for foreground forwarding and command execution. Binding and recovery tasks are retained and awaited during shutdown.
- **Restart intent:** `FileOverlayIntentStore` atomically persists a versioned overlay intent document and reloads it before current-generation convergence.
- **Tautological assertions:** both `Assert.True(true)` assertions were replaced with observable lifecycle assertions; the host stop assertion now executes the production `SessionAgentHost`.

### Phase 4.1 TDD Cycle Evidence

| Scenario | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| Restart converges intent | `ProductionSafetyLoopTests.cs` | File integration | Service 96/96 | `CS0246` for missing `SessionSafetyLoop` after the scenario was written first | Current-generation show command, persisted version 7, restore and confirmation passed | Absent/replaced behavior remains covered by `ExactTargetAndOverlayPolicyTests`; current file-backed restart covers the durable path | Atomic versioned store isolated from coordinator |
| Same-app threshold crossing | `ProductionSafetyLoopTests.cs` | Managed integration | Service 96/96 | Same compile RED before production loop existed | Observe-allow then tick-block passed without another foreground event | Existing relax path plus new crossing path cover both intent directions | Fixed-slot coordinator tick remains O(1) and non-overlapping |
| Stale heartbeat | `ProductionSafetyLoopTests.cs` | Managed integration | Service 96/96 | Same compile RED; authoritative API absent | Matching session/generation alone increments liveness | Wrong generation and wrong session both reject before the current heartbeat accepts | Correlation check is a pure constant-time guard |
| Agent death | `ProductionSafetyLoopTests.cs` | Race/integration | Service 96/96 | Same compile RED; no production authority invalidation API | Death replaced a pending command generation and degraded confirmation | Confirmed attach followed by unacknowledged current intent proves invalidation, not startup absence | Binding serialized through an async gate; shutdown awaits observed tasks |
| Child admin | `ProductionSafetyLoopTests.cs` | Managed integration | Service 96/96 | Same compile RED; no serialized child-admin input | Admin evidence sets blocking state; recovery evidence clears it | Theory executes both admin and standard-account branches | Input is non-coalescible coordinator work |
| PID reuse | `ProductionSafetyLoopTests.cs` | Managed/native seam integration | Service 96/96 | Same compile RED; foreground target was not consumed by a production loop | Exact PID/session/start-time target reached terminator; `InvalidTarget` prevented confirmation | Existing pre-close and post-graceful-close PID-reuse tests cover both mismatch times | Canonical AppId and ephemeral target remain separate |
| Correlated agent execution | `SessionAgentHostTests.cs` | Agent integration seam | SessionAgent 23/23 | Result/authority-heartbeat collections were empty | Overlay command returned exact correlation and current authority heartbeat | Existing overlay show/hide tests plus stale service-port matrix cover alternate payload/result paths | Replaced callback fire-and-forget with one awaited work queue |

### Executed evidence

- Focused service safety-loop/coordinator/production-handler matrix: 27 passed, 0 failed.
- Focused SessionAgent lifecycle/command matrix: 24 passed, 0 failed.
- Full solution: Domain 95, Service 917, SessionAgent 107, App.UI 146; **1,265 passed, 0 failed, 0 skipped** under a 600-second external timeout.
- Build: solution succeeded with 0 errors under a 300-second external timeout; existing package/analyzer warnings remain.
- Final portable-PDB coverage reports: Service `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-safety-service-final2/72f1f31e-0ee0-4bb7-b037-cf0ff13f0abc/coverage.cobertura.xml`; SessionAgent `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-safety-agent/b1fbda94-eb85-4e12-83ec-72767cdeb529/coverage.cobertura.xml`.
- Historical task-4.1 baseline from `changed-scope-coverage.py`: the conservative dirty-tree proxy was **999/1,385 lines (72.13%)** and **269/420 branches (64.05%)**. The greater-than-80% line gate was not satisfied at that point.
- Historical task-4.1 runtime baseline: managed IPC/agent harness only. No real topmost overlay, uncontrolled process termination, or `LockWorkStation` invocation occurred in that batch; Windows runtime records were **0/5** at that point.

### Historical task-4.1 boundary and remaining work

That automatic `feature-branch-chain` unit completed task 4.1 and stopped below the 800-authored-line boundary. It did not mark 4.2 or 4.3. At that point, raising conservative changed-scope coverage above 80% and producing all five safe real-Windows records remained assigned to the next autonomous runtime/coverage unit.

## Phase 4.2 Runtime and Coverage Closure

### TDD Cycle Evidence

| Scope | Test File / Tool | Safety Net | RED | GREEN | REFACTOR |
|---|---|---|---|---|---|
| Safe Windows runtime harness | `tests/ControlParental.SessionAgent.Tests/WindowsRuntimeSafetyLoopTests.cs` | Existing Service and SessionAgent suites remained available | The test was written first and failed to compile because controlled `OverlayWindow` construction/handle access and the safe workstation-lock delegate seam did not exist; runtime evidence was 0/5 | One supported-Windows execution passed all five flows and persisted `windows-runtime-evidence.json` without locking the workstation or targeting an uncontrolled process | Native behavior is bounded to a 320x180 overlay and a controlled child process; `LockWorkStation` production dispatch is exercised through an injected delegate after resolving the real user32 export |
| Changed-scope closure | `changed-scope-coverage.py`, `EnforcementLevelMonitorSafetyBranchTests.cs`, `OverlayManagerTests.cs`, and runtime harness | Baseline full suites passed before coverage expansion | Reproducible conservative coverage was 999/1,385 lines (72.13%), below the strict greater-than-80-percent gate | Final weighted result is 1,148/1,430 lines (80.28%); branch result is 323/448 (72.10%) | Coverage aggregation merges generated classes by source line and intersects tracked files with current Git hunks; uncovered lines remain explicit rather than excluded |
| Retry test stability found by final gate | `SessionCoordinatorTests.TimedOutAttemptRetriesWithFreshCommandIdentity` | The isolated Service suite had passed previously | Full parallel solution execution expired the test's 150 ms wall-clock deadline before its third accepted retry | The same three-attempt/fresh-ID assertions pass with a 2-second scheduling budget; the final full solution passes 1,275/1,275 | Only test timing changed; production retry limits, deadlines, and behavior were not modified |

### Supported Windows Runtime Evidence

- Environment: Microsoft Windows NT 10.0.26200.0, interactive session 1.
- Command scope: `WindowsRuntimeSafetyLoopTests` with `CONTROL_PARENTAL_RUNTIME_EVIDENCE` targeting `windows-runtime-evidence.json`.
- Process execution: start `2026-08-11T14:02:27.3094526Z`, end `2026-08-11T14:02:37.6998280Z`, 10,390 ms, exit code 0, 1 passed / 0 failed.
- Artifact execution window: start `2026-08-11T14:02:33.1773578Z`, finish `2026-08-11T14:02:36.9126090Z`.

| Flow | Result | Duration | Evidence |
|---|---:|---:|---|
| Overlay lifecycle | PASS | 54.293 ms | Native topmost 320x180 show/replace/clear lifecycle |
| Exact termination | PASS | 3,200.2851 ms | Wrong start time rejected; only the exact controlled child was terminated |
| `LockWorkStation` safe seam | PASS | 470.1381 ms | user32 export resolved; production dispatch called the safe delegate once; workstation remained unlocked |
| Agent reconnect | PASS | 470.1381 ms | Real named-pipe generation replacement 1 → 2; stale generation rejected |
| Heartbeat loss/recovery | PASS | 470.1381 ms | Bounded 300 ms silence observed; generation-2 authoritative heartbeat accepted |

### Final Changed-Scope Coverage

The calculator uses the artifact-declared production file set, merges compiler-generated records by source line, and treats all coverable lines in untracked files as changed. For tracked files it intersects coverable lines with `git diff --unified=0 HEAD`. Because the working tree contains overlapping changes, this is a conservative reproducible dirty-tree proxy, not commit-isolated attribution.

Coverage reports:

- Service: `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-safety-42-service-final2/df684471-008c-45a9-b037-b228ca432582/coverage.cobertura.xml`
- SessionAgent: `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-safety-42-agent-final3/cb9034d4-aeee-4b3b-83b2-febc6a94441c/coverage.cobertura.xml`

| Production file | Lines | Branches |
|---|---:|---:|
| `Domain/AgentCommandContracts.cs` | 10/10 | 0/0 |
| `Domain/IEnforcementEngine.cs` | 3/3 | 0/0 |
| `Domain/IIssueStore.cs` | 11/11 | 0/0 |
| `Domain/IpcMessage.cs` | 12/12 | 0/0 |
| `Service/AgentCommandPort.cs` | 106/116 | 39/48 |
| `Service/EnforcementEngine.cs` | 20/20 | 4/4 |
| `Service/EnforcementLevelMonitor.cs` | 261/272 | 54/60 |
| `Service/ExactProcessHandle.cs` | 13/13 | 0/0 |
| `Service/FileIssueStore.cs` | 111/131 | 31/42 |
| `Service/FileOverlayIntentStore.cs` | 31/36 | 7/12 |
| `Service/OverlayIntentPolicy.cs` | 44/44 | 26/26 |
| `Service/ProcessTerminator.cs` | 47/49 | 13/16 |
| `Service/Program.cs` | 58/226 | 21/78 |
| `Service/ServiceHealthMonitor.cs` | 13/13 | 12/12 |
| `Service/ServiceRecoveryManager.cs` | 56/57 | 11/12 |
| `Service/SessionEnforcementCoordinator.cs` | 109/117 | 23/26 |
| `Service/SessionSafetyLoop.cs` | 96/100 | 25/32 |
| `Service/ThresholdReevaluator.cs` | 12/12 | 1/2 |
| `SessionAgent/ForegroundWatcher.cs` | 3/37 | 2/10 |
| `SessionAgent/IForegroundWatcher.cs` | 4/4 | 2/2 |
| `SessionAgent/OverlayWindow.cs` | 57/63 | 35/42 |
| `SessionAgent/Program.cs` | 71/84 | 17/24 |
| **Weighted aggregate** | **1,148/1,430 (80.28%)** | **323/448 (72.10%)** |

The line gate is satisfied strictly above 80%. Branch coverage is reported, not treated as a threshold. `IAuthoritativeHealthSink.cs` is in the declared scope but contributes no executable sequence point to the denominator.

### Final Verification

| Command / scope | Result |
|---|---|
| `dotnet build ControlParental.sln --no-restore --verbosity minimal` | Exit 0; 0 errors; 229 existing warnings; 11,778 ms (`2026-08-11T14:05:28.0439865Z` → `2026-08-11T14:05:39.8260593Z`) |
| Focused stabilized retry test | 1 passed / 0 failed; exit 0; 19,430 ms including build (`2026-08-11T14:06:52.2555364Z` → `2026-08-11T14:07:11.6899422Z`) |
| `dotnet test ControlParental.sln --no-restore --no-build --verbosity minimal` | Domain 95, Service 924, SessionAgent 110, App.UI 146; **1,275 passed / 0 failed / 0 skipped**; exit 0; 14,953 ms (`2026-08-11T14:07:18.6800689Z` → `2026-08-11T14:07:33.6387414Z`) |
| `git diff --check` | Exit 0; line-ending conversion warnings only, no whitespace errors |

### Workload Boundary and Residual Risk

This autonomous `feature-branch-chain` Unit 3 runtime/coverage slice completes task 4.2 only. It adds safe test seams, runtime/coverage evidence, and focused coverage tests; it does not perform task 4.3 canonical closeout and creates no commit or PR. The batch remains under the 800-authored-line unit limit.

Residual coverage concentration remains explicit in legacy production composition (`Service/Program.cs`) and native observation (`ForegroundWatcher.cs`); the weighted changed-scope line gate passes, but these areas still warrant focused future tests. Existing analyzer warnings and the xUnit duplicate-ID notice are pre-existing repository findings and are not represented as fixed by this task.

## Task 4.3 Canonical Performance and Closeout Evidence

### Harness and methodology

`tests/ControlParental.Service.Tests/SessionCoordinatorPerformanceEvidenceTests.cs`
is an opt-in xUnit CLI harness. It runs only when
`CONTROL_PARENTAL_PERFORMANCE_EVIDENCE` is set, so the canonical suite does not
inherit a 30-second measurement window. It adds no production instrumentation or
dependency and uses the real `SessionSafetyLoop`, `SessionEnforcementCoordinator`,
bounded channel, and production-equivalent `Task.Delay` scheduling primitive.

- Allocation setup, loop construction, attachment, initial observation, and
  2,000-event warmup are excluded. Three 20,000-event windows use
  `GC.GetTotalAllocatedBytes(true)` around fully processed unchanged-decision ticks.
- Queue load gates one running item, admits exactly the design capacity of 256
  critical items, verifies the next producer remains blocked, drains completely,
  and repeats on a fresh coordinator.
- Timer drift uses monotonic `Stopwatch` timestamps for three finite windows of
  ten one-second delays. Drift is actual interval minus the requested one-second
  production period.
- End-to-end latency measures from `SessionSafetyLoop.Tick()` admission through
  coordinator idle after the unchanged O(1) decision, with 500 warmups and three
  2,000-event samples.
- The spec defines queue capacity 256 but no numeric allocation, drift, or latency
  threshold. No additional limit is inferred; those values are measurements for
  final verification.

This is a pure measurement harness over existing behavior, not a behavioral
change. Fabricating a RED would not establish a missing requirement. The harness
instead asserts finite sample counts, exact queue capacity/high-water, blocked
backpressure, complete drain, and successful cleanup. Existing strict-TDD
behavioral evidence remains unchanged.

### Measured results

Structured evidence: `openspec/changes/offline-enforcement-safety-loop/performance-evidence.json`.
Environment: Windows 10.0.26200, .NET 9.0.18 x64, 12 logical processors,
commit context `90a5a2a44299d99b67aff18da2a9182bc167fc75`, dirty worktree explicitly
recorded without claiming cleanliness.

| Metric | Run samples | Current result |
|---|---:|---:|
| Allocated bytes per fully processed unchanged tick | 3 × 20,000 after 2,000 warmup | Runs 784.9596, 784.0256, 784.0400; median 784.0400 bytes/event |
| Critical queue high-water | 2 finite repetitions | 256/256 both runs; producer 257 blocked; both runs drained |
| Timer drift | 3 × 10 at 1,000 ms | p50 8.6940 ms; p95 15.7455 ms; max 15.8848 ms |
| End-to-end processing latency | 3 × 2,000 after 500 warmup | p50 2.5 µs; p95 6.5 µs; max 172.8 µs |

### Canonical commands and current gates

Every command used a finite executor timeout. The measurement command also used
`--blame-hang --blame-hang-timeout 60s`; all resources are `using`/`await using`
scoped and both queue repetitions prove complete drain.

| Command / scope | Timeout | Exit | Result |
|---|---:|---:|---|
| Performance command embedded in `performance-evidence.json` | 120 s external / 60 s test-host hang | 0 | 1/1 passed; 30-second finite measurement window |
| `dotnet build ControlParental.sln --no-restore --verbosity minimal` | 300 s | 0 | 0 errors; 229 existing warnings |
| `dotnet test ControlParental.sln --no-restore --no-build --verbosity minimal` | 600 s | 0 | Domain 95, Service 925, SessionAgent 110, App.UI 146; 1,276/1,276 passed |
| Fresh Service portable-PDB coverage | 600 s | 0 | 925/925 passed; Cobertura `offline-safety-43-service2/373846e0-b174-4182-974b-2fe52c350ae8` |
| Fresh SessionAgent portable-PDB coverage | 300 s | 0 | 110/110 passed; Cobertura `offline-safety-43-agent/5f79db7d-7867-4c7c-9e03-fbc7998f088b` |
| Canonical `changed-scope-coverage.py` with fresh reports and declared production set | 120 s | 0 | 1,148/1,430 lines (80.28%); 323/448 branches (72.10%) |
| Structured runtime evidence validation | 30 s | 0 | Existing safe Windows runtime record remains 5/5 PASS; destructive flows were not repeated |
| `git diff --check` | 60 s | 0 | No whitespace errors; line-ending conversion warnings only |

Two unrelated timing-sensitive tests failed only during earlier full parallel
runs and passed in isolation; the final canonical full run passed 1,276/1,276.
An attempted parallel coverage collection exited nonzero because both builds
contended for `ControlParental.Domain.dll`; the authoritative collections above
were rerun sequentially and both exited 0. No unrelated source was changed.

### Closeout and rollback

Task 4.3 is complete because the missing finite measurements now exist, all ten
tasks are checked, current smoke/coverage/runtime gates are coherent, and the old
FAIL remains clearly historical/superseded. Rollback remains Unit 3 → Unit 2 →
Unit 1. The measurement harness and JSON can be removed independently without
changing production semantics. Disabling production composition must never
restore two authorities or reinterpret an unconfirmed critical action as success.

No final verification PASS is declared by apply. Next step: `sdd-verify`.

### Workload / PR boundary

This autonomous measurement-only Unit 3 closeout slice starts from completed task
4.2 and ends with task 4.3 evidence and canonical artifacts. It adds 297 harness
source lines plus compact generated evidence and documentation, remains below the
800-authored-line limit, changes no production semantics, and creates no commit or
PR.

## Post-Verification Corrective Apply (2026-08-11)

### Corrected findings

1. `WorkstationLockManager` now requires a current typed `AgentActionResult` correlated by command, session, generation, and intent. Missing authority, timeout, replacement, stale/mismatched result, native failure, and all other non-confirmed statuses remain non-success. `EnforcementEngine` records locked state only after confirmation.
2. Clock/timezone changes are admitted as non-coalescible serialized work through `SessionSafetyLoop.TimeChangedAsync`. Production preserves an event observed before loop attachment and drains it after attachment.
3. `PolicyRepository` maintains a synchronized current snapshot and invalidates it explicitly after authoritative policy mutations; repeated enforcement evaluation no longer materializes/deserializes SQLite policy per event.
4. `UsageAccumulator` uses one awaitable `PeriodicTimer` loop. `StopAsync` cancels and drains the active tick, so callbacks neither block on `.Wait()` nor overlap.
5. The changed host idempotence test now verifies `IHostApplicationLifetime.ApplicationStopped` instead of using `Assert.True(true)`. A repository-wide C# search found no remaining `Assert.True(true)`.
6. Retry-bound tests use deterministic timed-out results and fresh command identities. Native overlay windows own independent Win32 classes and delegates, so disposing one window cannot invalidate another.

### Corrective TDD cycle evidence

| Finding / scope | RED | GREEN | REFACTOR |
|---|---|---|---|
| Typed workstation lock | New lock-manager and enforcement tests rejected missing, stale, mismatched, timed-out, replaced, and failed typed outcomes before the production authority was connected | Focused typed lock matrix passed; both full suites passed | One typed executor/correlation path is authoritative; legacy send completion cannot establish success |
| Serialized time changes | `ProductionSafetyLoopTests` showed time-change work had no coordinator API or production ordering path | Time-change callback and reevaluation ordering tests passed | One pending pre-attachment event and one serialized coordinator input avoid a second authority |
| Policy hot-path cache | Repository tests observed repeated persistence reads/deserialization and missing invalidation | Cache reuse and mutation invalidation tests passed | Snapshot ownership is synchronized and invalidation remains at authoritative mutation boundaries |
| Non-overlapping usage scheduling | Scheduler tests exposed blocking callback/overlap and incomplete shutdown drain | `PeriodicTicksDoNotOverlapAndStopAsyncDrainsRunningTick` and canceled-caller shutdown coverage passed | One awaitable loop replaces callback blocking; shutdown always drains the scheduler task |
| Assertion quality | The verifier's static assertion-quality gate failed on `Assert.True(true)` | `HostStartAndStopAsync_AreIdempotent` passed with observable lifetime state; no tautology remains | The test now observes the host lifecycle rather than asserting a constant |
| Retry/native window stability | Full-suite evidence exposed deadline-sensitive attempt counting and cross-window class invalidation | Deterministic retry-bound tests and `DisposingOneControlledOverlayDoesNotInvalidateAnotherWindow` passed; safe Windows runtime rerun passed | Test deadlines no longer define attempt semantics; each native window owns and unregisters its class safely |

### Current corrective evidence

| Command / scope | Result |
|---|---|
| `dotnet build ControlParental.sln --no-restore --verbosity minimal` | Exit 0; 0 errors; 229 existing warnings |
| Full solution, consecutive run 1 | Domain 95, Service 937, SessionAgent 111, App.UI 146; 1,289/1,289 passed |
| Full solution, consecutive run 2 | Domain 95, Service 937, SessionAgent 111, App.UI 146; 1,289/1,289 passed |
| Fresh Service portable-PDB coverage | 937/937 passed; `offline-safety-corrective-service2/405b6e35-2a54-41a5-8f7c-d399405eef7e` |
| Fresh SessionAgent portable-PDB coverage | 111/111 passed; `offline-safety-corrective-agent/339a422c-ec1f-4ef0-845c-f2097c2705c4` |
| Expanded changed-scope calculation | 1,419/1,754 lines (80.90%); 388/532 branches (72.93%) |
| Safe Windows runtime rerun | 1/1 test and all five flows PASS; external artifact `offline-safety-corrective-runtime.json` |
| Performance rerun | 1/1 passed; 784.0624 bytes/event, queue 256/256 with backpressure/drain, drift p50/p95 0.445/1.0455 ms, latency p50/p95 5.1/9.7 µs |

The expanded coverage scope includes the corrective lock, policy-cache, usage-scheduler, time-change, and native-window files in addition to the original declared set. `Win32Api.cs` contributes no executable changed sequence point. The dirty tree has no commit-isolated pre-correction baseline, so an exact authored-line delta for only this corrective slice cannot be reconstructed from Git; the edited corrective regions are estimated below 800 lines, and no PR or commit was created.

### Status and boundary

All 10 planned tasks remain checked. This autonomous `feature-branch-chain` corrective Unit 3 slice starts from the failed verification findings and ends with implementation plus fresh build, two-run regression, coverage, Windows runtime, and performance evidence. The canonical verification verdict remains FAIL until an independent `sdd-verify` re-evaluates these corrections; archive is not authorized by this apply artifact.

## Policy-Sync Race Corrective Apply (2026-08-11)

### Root cause and minimum correction

`ScheduledWorkService.ExecuteHeartbeatAsync` returned immediately after launching
heartbeat-triggered policy sync in an untracked `Task.Run`. Completion therefore
did not include `FetchPolicyAsync`, cancellation and faults were detached from the
caller, and direct/timer policy-sync entry points could overlap. The prior test
also called `StartAsync`, allowing startup policy sync to satisfy its invocation
assertion independently of the heartbeat path.

The corrective delta removes that fire-and-forget dispatch. Heartbeat now awaits
`ExecutePolicySyncAsync` with the caller token, while one asynchronous
`SemaphoreSlim` gate serializes all policy-sync entry points. Waiting is
non-blocking, cancellation propagates through gate and backend waits, errors remain
observable to direct/backup callers or the existing scheduled-dispatch boundary,
and the gate is always released in `finally`.

### Strict TDD cycle evidence

| Scope | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| Awaitable/cancellable/non-overlapping policy sync | `tests/ControlParental.Service.Tests/ScheduledWorkServiceAsyncDispatchTests.cs` | Service unit/concurrency seam | Existing class matrix 31/31 passed before edits | Three deterministic tests failed without sleeps: heartbeat completed before the gated fetch; cancellation was not propagated; concurrent sync entered policy work twice | All 3/3 passed after awaiting sync and adding the async single-entry gate | Completion, caller cancellation, and two concurrent entry points exercise three distinct paths; existing malformed-policy test retains fault observability | Kept one gate at the policy-sync boundary and one `finally` release; no scheduler, warning, or unrelated refactor was added |

### Corrective execution evidence

Every command used a finite external timeout and the final gate sequence ran
serially.

| Command / scope | Timeout | Exit | Result |
|---|---:|---:|---|
| RED: three new/changed policy-sync scenarios | 120 s | 1 | 0 passed / 3 failed for the expected completion, cancellation, and overlap reasons |
| GREEN: same three scenarios | 120 s | 0 | 3/3 passed |
| Former flaky heartbeat scenario repeated sequentially | 180 s | 0 | 10/10 independent invocations passed |
| Focused `FullyQualifiedName~ScheduledWorkService` matrix | 240 s | 0 | 89/89 passed |
| `dotnet build ControlParental.sln --no-restore --verbosity minimal` | 300 s | 0 | 0 errors; existing analyzer/package warnings remain |
| Full solution, consecutive run 1 | 600 s | 0 | Domain 95, Service 939, SessionAgent 111, App.UI 146; 1,291/1,291 passed |
| Full solution, consecutive run 2 | 600 s | 0 | Domain 95, Service 939, SessionAgent 111, App.UI 146; 1,291/1,291 passed |
| Service portable-PDB coverage | 600 s | 0 | 939/939 passed; `offline-safety-policy-sync-service-portable-coverage/3e846956-6feb-485a-aafd-f81db35e9488/coverage.cobertura.xml` |
| Expanded changed-scope calculator | 120 s | 0 | 1,448/1,793 lines (80.76%); 395/546 branches (72.34%) |
| `git diff --check` | 120 s | 0 | No whitespace errors; line-ending conversion warnings only |

The first coverage collector invocation exited 0 but emitted an empty report
because the normal build uses embedded PDBs. It is non-authoritative; the table
records the successful portable-PDB rerun used by the calculator.

### Runtime, performance, scope, and status

The correction touches only managed `ScheduledWorkService` policy-sync ordering
and its focused tests. It does not alter SessionAgent/native commands,
`SessionSafetyLoop`, coordinator queueing, runtime harness behavior, or performance
instrumentation. The existing fresh Windows runtime 5/5 PASS and finite
performance evidence are therefore not invalidated and destructive runtime flows
were not repeated. `UsageReconciler.Wait()` remains the documented non-blocking
warning outside this delta.

All 10 planned tasks remain checked. The corrective slice is well below the
800-authored-line boundary and changes only `ScheduledWorkService.cs`, its focused
test file, and these OpenSpec status artifacts. Verification is
**awaiting re-verification**; no PASS, archive, commit, or PR is declared. Next
recommended phase: `sdd-verify`.
