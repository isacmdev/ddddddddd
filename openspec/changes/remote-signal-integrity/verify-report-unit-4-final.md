# Verification Report

**Change**: `remote-signal-integrity` — FINAL FRESH full SDD7 Unit 4 (approved Unit 4A base + current Unit 4B lifecycle/race slice), tasks 4.1–4.3  
**Branch**: `feat/sdd7-4b-integrity-lifecycle-races`  
**Base / PR4B parent**: `b5a2fee36f224bcb66eed1d560d6652443d4394c` / `feat/sdd7-4-runtime-integrity-enforcement`  
**Unit 4A parent chain**: `3082559551ce4aa4363bd14a9e09dda0b1176f5f` → `b5a2fee36f224bcb66eed1d560d6652443d4394c`  
**Mode**: Strict TDD, hybrid persistence, final fresh independent report-only verification  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**

## Executive Summary

Every requested fresh sequential `--no-restore` build/test command passed: Domain and Service product builds, portable-PDB Service test build, exact Unit 4B focus 5/5, combined Unit 4 focus 72/72, full Service 1,186/1,186, full App.UI 192/192, and portable-PDB coverage. The Unit 4B diff contains exactly the five expected source/test files, no deletion or dependency/config/project drift, and is **300/400 CODE+TEST lines** (not the claimed 299).

The approved Unit 4A backend-authority and durable-persistence behavior remains intact and fresh-green. However, full Unit 4 fails the lifecycle/race gate. The five new focused tests do not execute a real second periodic timer tick, Dispose, stop-before-start, repeated stop, restart, non-cancellable late normal/failure completion, exact escalation deadline, mixed concurrent trust/revoked/recovery, or stale/reordered backend completion. Coverage confirms the periodic loop body is unexecuted. Source inspection also finds that Stop awaits only `lifecycleTask`, not independently admitted `TriggerIntegrityCheckAsync` work; Start can publish a timezone timer after concurrent Stop/Dispose; restart overwrites undisposed lifecycle resources; Dispose can dispose the semaphore while an unowned trigger still uses/releases it; and post-initial loop exceptions are caught then hidden as successful task completion.

**Full Unit 4 is not approved. The local Unit 4B feature-chain commit boundary is not approved.** SDD7 remains partial at **12/14** and is not archive-ready. Unit 5 remains excluded and untouched.

## Verification Boundary and Preservation

- Read proposal, exploration, all four specs, design, re-sliced tasks, complete cumulative apply progress, the historical full Unit 4 FAIL, and every Unit 4A FAIL/approval report.
- CodeGraph was attempted first by checking the exact worktree. `.codegraph/` was absent; focused direct inspection was used and no index was created.
- Inspected exact branch/base/status/diff/stat/numstat/diff-check, all five changed files, Unit 4A authority paths, identity coordinator, backend parser/retry/cancellation, DI, enforcement monitor, file store, changed/deleted tests, coverage, and configuration/dependency drift.
- Historical report hashes remained unchanged:
  - Unit 4: `FA44D9A41B3ADF96054E4AB6EDA91CF78B2980828A5093C51E78DEDFF3798EEE`
  - Unit 4A: `88A7080A40242BA708625EF900C5E9C51B87ECA9767C92B5E222E8324C19D5F1`
  - Unit 4A final: `701CED0D722D2D6B0164FED7588B89D7C0B738498B994ED06A4DBD83A78531BB`
  - Unit 4A authoritative final: `E88FE7727237F299C5560009AC9EDA6B2F2BBA6081B47377D14EC8CA8732F155`
  - Unit 4A approved: `556A4167B9BD8AE509B62F4E6C4ADD517D4C1C89D9A843245673C17FFD24C676`
- No source/test/task/apply/spec/design/config/dependency/git/branch/worktree/external mutation was performed. Ignored build/coverage outputs are execution evidence; this report is the sole authored SDD artifact.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Checked | 12 (`1.1`–`4.3`) |
| Unchecked/excluded | 2 (`5.1`–`5.2`) |
| Unit 4 tasks checked | 3/3 |
| Authoritative Unit 4 scenarios compliant | 8/9 |
| Exact Unit 4B focused tests | 5/5 passed |
| Required lifecycle/race variants fully covered | No |

Checked task boxes do not establish completion when required scenarios lack passing runtime coverage. The uncovered concurrent-recovery/lifecycle variants are CRITICAL and block tasks 4.1–4.3 approval.

## Workspace, Drift, Restore, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD | Expected branch; HEAD is exact Unit 4A base `b5a2fee36f224bcb66eed1d560d6652443d4394c` |
| Merge base | Exact `b5a2fee36f224bcb66eed1d560d6652443d4394c` |
| Changed tracked paths | Exactly the five expected Unit 4B files |
| Additional tracked path / deletion | None |
| Unit 4A weakening/deletion | None found; canonical identity/non-degradation/restart assertions remain |
| Dependency/config/project drift | None |
| Generated tracked cache | None |
| `git diff --check` | Exit 0 before and after execution; LF→CRLF notices only |
| Restore | No restore executed; every .NET command used `--no-restore` |
| `.codegraph` | Absent before and after |

### Exact Unit 4B CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 103 | 23 | 126 |
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | 22 | 2 | 24 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 97 | 3 | 100 |
| `tests/ControlParental.Service.Tests/IntegrityRuntimePathTests.cs` | 11 | 6 | 17 |
| `tests/ControlParental.Service.Tests/IntegrityVerdictHandlerTests.cs` | 32 | 1 | 33 |
| **Total** | **265** | **35** | **300/400** |

The hard budget passes, but cumulative apply progress and the candidate claim undercount the slice by one line (`299` claimed versus `300` fresh).

## Fresh Build, Test, and Coverage Execution

All commands ran sequentially in the exact worktree.

| Evidence | Exact command | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain build | `dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:WarningLevel=0` | `11:30:47.9067517` → `11:30:50.0585047` | 0 | 0 errors/warnings |
| Service build | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:WarningLevel=0` | `11:30:58.8114449` → `11:31:05.7174761` | 0 | 0 errors; existing NU1601 |
| Portable-PDB Service test build | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:WarningLevel=0` | `11:31:11.2661468` → `11:31:24.8096951` | 0 | 0 errors; existing NU1601/NU1701 |
| Exact Unit 4B focus | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AntiTamperMonitorTests.TriggerIntegrityCheckAsync_IsSingleFlightWhenReportBlocks|FullyQualifiedName~AntiTamperMonitorTests.StopAsync_CancelsAndDrainsOwnedReport|FullyQualifiedName~AntiTamperMonitorTests.TriggerIntegrityCheckAsync_PreservesCallerCancellation|FullyQualifiedName~IntegrityVerdictHandlerTests.HandleVerdict_EscalationDoesNotDegradeBeforeDueTimestamp|FullyQualifiedName~IntegrityVerdictHandlerTests.HandleVerdict_ConcurrentInputsAreSerialized"` | `11:31:37.2081704` → `11:31:41.6852939` | 0 | 5 passed |
| Combined Unit 4 focus | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests"` | `11:31:49.5049670` → `11:31:52.4108038` | 0 | 72 passed |
| Full Service | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `11:31:58.6451202` → `11:32:11.4264870` | 0 | 1,186 passed; existing duplicate-ID discovery notice |
| Full App.UI | `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `11:32:18.8374909` → `11:32:21.5618274` | 0 | 192 passed |
| Portable-PDB coverage | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug -p:DebugType=portable --collect:"XPlat Code Coverage" --results-directory tests\ControlParental.Service.Tests\TestResults\coverage-unit4-final-fresh-20260821 --verbosity minimal -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | `11:32:30.0619291` → `11:33:03.2795144` | 0 | 1,186 passed; Cobertura produced |

**Coverage artifact**: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4-final-fresh-20260821/f94e0af9-4434-4f1d-a997-2337f868abb8/coverage.cobertura.xml`  
**Aggregate**: 54.09% line / 58.14% branch (informational; no aggregate threshold configured).

## Nine-Scenario Behavioral Compliance Matrix

| Spec | Scenario | Fresh passing runtime evidence | Result |
|---|---|---|---|
| Runtime integrity | Valid evidence receives trust | Combined focus preserves real coordinator → authenticated backend/parser → policy → file-store recovery after production refresh | ✅ COMPLIANT |
| Runtime integrity | Revocation changes enforcement | TimeProvider-driven before/after escalation path plus real revoked authority creates one scoped durable issue | ✅ COMPLIANT |
| Runtime integrity | Unknown/transient response is non-degrading | Runtime matrix preserves exact physical snapshots for unknown, pending, absent, malformed, stale generation, refresh timeout, HTTP failure, and cancellation | ✅ COMPLIANT |
| Runtime integrity | Restart and recovery preserve semantics | Recreated file store/enforcement/policy resolves only target and preserves same-/cross-device unrelated records | ✅ COMPLIANT |
| Runtime integrity | Deferred release scope is not claimed | Full Service receipt-boundary tests remain green and artifacts make no forbidden readiness claim | ✅ COMPLIANT |
| Offline enforcement | Supported safety evidence degrades health | Real authoritative revoked path persists severe scoped issue through production components | ✅ COMPLIANT |
| Offline enforcement | Semantic issue recovery survives restart | Duplicate cardinality and exact target-only recovery pass after recreation | ✅ COMPLIANT |
| Offline enforcement | Transient integrity failure does not degrade protection | Complete physical non-degradation matrix remains green | ✅ COMPLIANT |
| Offline enforcement | Concurrent recovery is serialized | Only three concurrent revoked calls are tested. No deterministic mixed trusted/revoked/recovery final state, restart/agent-death/enforcement race, stale/reordered backend completion, or late completion after lifecycle change is runtime-covered | ❌ UNTESTED |

**Compliance summary**: **8/9 compliant**. The required concurrent recovery scenario is blocking.

## Combined Authority and Persistence Audit

| Invariant | Fresh finding | Assessment |
|---|---|---|
| Canonical identity owner | Production registers one singleton coordinator and supplies it to current `BackendClient` and `AntiTamperMonitor` | ✅ |
| Stable durable identity | `IssueKey(0, BinaryIntegrityFailure, "integrity/binary", DeviceId)` excludes credential generation; real refresh retains device/key | ✅ |
| Complete production path | Controlled HTTP → current parser → real policy → `EnforcementLevelMonitor` → `FileIssueStore` remains exercised | ✅ |
| Definitive-only mutation | Durable reaction remains gated to successful exact backend `trust|revoked`; local evidence is report-only | ✅ |
| Non-definitive preservation | Unknown/pending/absent/malformed/stale/timeout/HTTP failure/cancellation preserve exact snapshots | ✅ |
| Duplicate/restart/recovery | One active semantic issue; recreated state and target-only recovery preserve unrelated/cross-device records | ✅ |
| Retry owner | `BackendClient.SendAuthenticatedAsync` remains the sole bounded transport retry/backoff owner | ✅ |
| Client/UI authority | No baseline API, local/UI/Realtime/WNS durable authority, or Unit 5 leakage | ✅ |
| Unit 4A regression | No approved Unit 4A assertion was removed or weakened; runtime path remains 9 cases inside the 72-test focus | ✅ |

## Lifecycle, Race, Cancellation, and Escalation Audit

| Requirement | Fresh source/runtime finding | Result |
|---|---|---|
| One lifecycle owner / no overlap | `PeriodicTimer` loop is awaited and manual triggers share a semaphore, but the blocked test invokes the internal seam manually; coverage lines 230–232 prove no periodic second tick executed | ❌ UNTESTED normative timer behavior |
| Idempotent Start/Stop and exact drain | Start is superficially idempotent. Stop sets `isRunning=false`, cancels, and awaits only `lifecycleTask`; it does not track/drain independently admitted trigger tasks, clear/dispose the generation, or prevent Start from assigning a timer after concurrent Stop | ❌ |
| Stop-before-start / repeated stop / restart | No focused runtime test. Restart overwrites the old CTS/task/TCS and timezone timer reference without disposing the stopped generation/timer | ❌ UNTESTED / resource leak |
| Dispose | Only simple sequential Dispose coverage exists. `disposed` is set outside the lifecycle lock; concurrent Start can create `timezoneTimer` after Dispose, and Dispose can dispose `integrityGate` while an unowned trigger later releases it | ❌ |
| Caller versus owner cancellation | Caller cancellation is observable and loop-owned cancellation is contained, but full drain is not established for unowned/late non-cancellable work | ⚠️ PARTIAL |
| Late normal/failure completion | Cancellation is checked immediately after backend completion, but no runtime test covers a backend that ignores cancellation and completes normally/fails after Stop/generation change; Stop can return before an external trigger settles | ❌ UNTESTED |
| Stale/reordered backend completion | Identity generation guard remains, and the semaphore prevents overlap among admitted checks. No test proves actual stale/reordered backend completion or restart-generation rejection | ❌ UNTESTED |
| Handler atomicity | Main verdict/local transitions use `stateGate`, but `IsCircuitOpen`, `IsShadowMode`, Dispose, and test-state mutation are not consistently synchronized; mixed trust/revoked/recovery exact final state is absent | ❌ PARTIAL |
| Lock across callback/I/O | `HandleVerdict` invokes fire-and-forget `EnqueueNotificationAsync` while holding `stateGate`; the outbox call begins synchronously under that lock and is not lifecycle-owned | ❌ |
| Retry/backoff | No second backend retry loop was added; lifecycle loop stops scheduling on its token | ✅ static |
| Escalation timing | Due timestamp is derived from production wall-clock timestamps; before/after pass. Exact deadline, restart during pending escalation, and timer effect are untested. Real `Timer` only logs; durable effect requires a later revoked report | ❌ PARTIAL |
| Exception observability | `RunMonitorLoopAsync` catches every non-cancellation exception, attempts `initialCheck.TrySetException`, then completes normally. After initial success, `TrySetException` is ineffective, so the owner can falsely report success | ❌ |
| CTS/timer/semaphore disposal race | Old generation CTS/timer is not disposed on Stop/restart; semaphore disposal can race an unowned trigger `finally` release | ❌ |

## Changed Coverage and Required Branches

| Scope | Line | Branch | Required uncovered evidence |
|---|---:|---:|---|
| `AntiTamperMonitor` | 84.10% | 85.71% | Aggregate acceptable, normative lifecycle branches missing |
| `StartAsync` state machine | 100% | 100% | No concurrent Start/Stop/Dispose or restart-resource assertion |
| `StopAsync` state machine | 100% | 87.50% | No stop-before-start/repeated-stop/unowned-trigger drain proof |
| `TriggerIntegrityCheckAsync` | 80.00% | 50.00% | disposed/not-running guards lines 198–204 unhit |
| `RunMonitorLoopAsync` | **57.89%** | **50.00%** | periodic tick body lines 230–232 and generic exception path 238–241 unhit |
| `PerformBinaryIntegrityCheckAsync` | 96.29% | 83.33% | disposed/not-running guard unhit; late completion variants absent |
| `ProcessVerdictReactionAsync` | 71.05% | 64.28% | Limit/ShadowWarn branches unhit |
| `IntegrityVerdictHandler` | 93.25% | 86.00% | disposed paths, one stage branch, and timer callback construction branch incomplete |
| `HandleVerdict` | 96.42% | 94.44% | disposed branch unhit |
| `HandleRevokedVerdict` | 97.72% | 85.71% | one staging branch unhit |
| `ScheduleEscalation` | 90.00% | 50.00% | timer callback line 353 unhit |
| `BackendClient.ReportIntegrityAsync` | 100% | 100% | Unit 4A cancellation authority retained |
| `BackendIdentityCoordinator.RefreshAsync` | 80.39% | 66.66% | failure branches remain outside Unit 4B; success path retained |
| `EnforcementLevelMonitor.AddIssueAsync` | 82.35% | 75.00% | fallback branch uncovered |
| `EnforcementLevelMonitor.ResolveIssueAsync` | 100% | 100% | target recovery retained |

Aggregate coverage cannot substitute for the zero-hit periodic tick, generic lifecycle fault, disposal/restart, late completion, exact deadline, and mixed ordering variants.

## Strict TDD Audit

| Check | Result | Details |
|---|---|---|
| TDD evidence table | ✅ | Unit 4B table exists in cumulative apply progress |
| Genuine Unit 4B behavioral RED | ⚠️ MISSING | Initial `NETSDK1004` was missing infrastructure. The later compile RED proved the internal lifecycle seam was absent, but no executed pre-production behavioral failure for overlap, drain, ordering, or escalation is retained |
| Fresh GREEN | ✅ | 5/5 Unit 4B focus; 72/72 combined focus; full regressions and coverage pass |
| Triangulation | ❌ | Five tests omit required lifecycle/dispose/restart/late/stale/mixed/deadline variants |
| Safety nets | ✅ | Domain/Service/test builds, full Service, full App.UI, coverage, diff and drift checks pass |
| Refactor safety | ❌ | Static lifecycle ownership/disposal/fault defects and untested normative branches remain |

The missing behavioral RED is reported as a strict-TDD WARNING, not fabricated. The verdict fails independently on current correctness and scenario-coverage defects.

## Test Layer Distribution and Assertion Quality

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Unit/mock lifecycle | 5 new focused | 2 | Production classes are called, but monitor dependencies are mocked |
| Production-component harness | 9 Unit 4A runtime cases | 1 | Real coordinator/backend parser/policy/enforcement/file store |
| E2E/external | 0 | 0 | Correctly excluded; no external claim |

- No literal tautology or ghost loop was found.
- The blocked-report overlap assertion occurs before cleanup, but it exercises two manual internal-seam calls, not two timer ticks.
- The 50 ms delay in `TriggerIntegrityCheckAsync_IsSingleFlightWhenReportBlocks` is timing-based; the barrier proves first entry, but the test does not deterministically prove the second call reached the gate before the assertion.
- `StopAsync_CancelsAndDrainsOwnedReport` covers only the initial owner report, not periodic work or an independently admitted trigger.
- `DetectedEvents.Should().BeEmpty()` does not prove no report/store mutation for caller cancellation.
- Concurrent handler coverage checks reaction counts only; it does not assert exact final state/recovery or mixed outcomes.

**Assertion quality**: no tautologies; blocking omission/substitution defects remain.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Backend owns authoritative hash reference | ✅ | No client baseline added |
| Checker → authenticated backend → policy → durable enforcement | ✅ | Unit 4A production path remains green |
| Definitive verdicts only mutate | ✅ | Local evidence remains report-only |
| Stable identity-scoped durable issue | ✅ | Generation excluded; device included |
| Restart-safe exact authority/persistence | ✅ Unit 4A | Durable target recovery remains intact |
| One lifecycle owner and deterministic drain | ❌ | Untracked external triggers and Start/Stop/Dispose publication races remain |
| Serialized deterministic concurrent recovery | ❌ | Mixed/stale/reordered/restart variants lack proof; handler synchronization is incomplete |
| One retry owner | ✅ | BackendClient remains sole transport retry owner |
| Bounded escalation | ⚠️ | Before/after timestamp behavior exists; deadline/restart/timer effect is unproven |

## Issues Found

### CRITICAL

1. **Stop/Dispose do not establish a complete ownership-and-drain boundary.** Stop awaits only `lifecycleTask`; independently admitted trigger tasks can outlive it. Concurrent Start can assign a timezone timer after Stop/Dispose, and restart overwrites undisposed CTS/timer resources.
2. **Dispose can race active work and resource disposal.** It sets `disposed` outside the lifecycle lock and disposes `integrityGate` without proving all trigger users are drained, allowing a late `Release`/disposed-object race and a timer surviving concurrent Dispose.
3. **Required lifecycle/race scenarios are untested.** No passing test covers a real second periodic tick, stop-before-start, repeated stop, restart, Dispose drain/idempotency under activity, non-cancellable late normal/failure completion, stale/reordered backend completion, or exact no-post-stop mutation/fault state.
4. **Concurrent recovery serialization is not proven.** The only concurrent handler test sends three revoked inputs and counts reactions; it does not cover deterministic concurrent trusted/revoked/recovery final state or restart/agent-death/enforcement races. The ninth authoritative scenario is UNTESTED.
5. **Lifecycle faults can be hidden.** After the initial check completes, `RunMonitorLoopAsync` catches an exception, fails to update the already-completed initial TCS, and returns successfully, so the owner can falsely report success.
6. **Handler synchronization violates the complete atomicity/callback boundary.** State access/disposal is not consistently gated, and outbox async work is invoked fire-and-forget while `stateGate` is held.
7. **Escalation acceptance is incomplete.** Exact-deadline and pending-escalation restart behavior are untested; the real timer only logs and does not itself publish durable enforcement/health change.

### WARNING

1. Strict TDD has no genuine executed Unit 4B behavioral RED for overlap/drain/order/escalation; missing assets and absent-seam compilation are not behavioral RED evidence.
2. Fresh CODE+TEST accounting is 300/400, not the claimed 299/400.
3. The overlap test uses a 50 ms delay and manual trigger seam rather than a deterministic second timer tick.
4. Existing NU1601/NU1701 warnings and one duplicate xUnit test-ID discovery notice remain; no dependency declarations changed.
5. Default `IEnforcementLevelMonitor` async compatibility methods remain uncovered; production Service overrides both.
6. `IsShadowMode_DefaultsToTrue` remains a stale test name while correctly asserting `false`.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

Full Unit 4 tasks 4.1–4.3 are **not approved**. The local Unit 4B feature-chain commit boundary is **not approved**. Fresh green aggregate execution and preserved Unit 4A authority/persistence do not compensate for incomplete lifecycle ownership/drain/disposal, hidden fault behavior, and missing runtime evidence for mandatory timer, restart, late/stale ordering, mixed recovery, and exact escalation variants.

SDD7 remains partial at **12/14** and is **not archive-ready**. No commit, push, PR, archive, fix, or Unit 5 preparation was performed.
