# Verification Report

**Change**: `remote-signal-integrity` — independent SDD7 Unit 4B2 Completion Safety + Faults only  
**Branch**: `feat/sdd7-4b2-lifecycle-completion-safety`  
**Parent / HEAD / merge-base**: `1f00184dd1fe1d96c9d683bbf7d2af356ddc30f4`  
**Mode**: Strict TDD, hybrid persistence, partial work-unit verification  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**

## Executive Summary

Fresh execution is green: five `--no-restore` portable-PDB builds, exact B2 focus `8/8`, B1+B2 lifecycle matrix `22/22`, AntiTamper `44/44`, Unit4A+B1+B2 `132/132`, Service `1,198/1,198`, App.UI `192/192`, and two isolated full-Service coverage hosts `1,198/1,198`. Both coverage artifacts report `AntiTamperMonitor` at `99.02%` line / `88.63%` branch.

B2 is nevertheless **not safe as an autonomous commit**. Source inspection finds unclosed invalidation races after external awaits: privilege completion can emit a tamper event and ownerless outbox work before the caller rechecks the generation; backend-to-handler and handler-to-store checks are non-atomic with Stop; and timezone/clock event publication has check-to-publication windows. The B2 tests stop only while the backend is blocked, so they do not execute the specifically requested backend→policy or policy→store cancellation windows.

The restart-after-fault test proves only a fresh generation identity. It does not execute stale periodic/timezone/manual callback or completion against the new generation. The B2 late-completion tests also do not assert exact before/after complete collaborator/state snapshots or physical cleanup counts. Therefore acceptance items 6, 7, and 9 lack required passing runtime evidence; item 7 also has a source-level correctness defect.

The exact normalized CODE+TEST diff is `91/400` and concern-pure. Tasks remain intentionally `9/14`; that planned partial state is not a failure. No Unit4C or Unit5 implementation was inspected as candidate scope or approved.

## Boundary and Artifact Consumption

- Read proposal, runtime-integrity, remote-signal-sync and offline-enforcement delta specs, design, tasks/slice definitions, cumulative apply progress, prior Unit4/Unit4A reports, and the authoritative approved B1 report.
- Inspected the complete production/test diff and current complete `AntiTamperMonitor.cs` / `AntiTamperMonitorTests.cs` files.
- CodeGraph was attempted first. `.codegraph/` was absent; per the explicit constraint no index was created, and focused direct inspection was used.
- The apply summary was treated only as a claim and independently checked.
- This report is B2-specific and does not overwrite the full-change verify report.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| B2 acceptance items | 10 |
| Compliant | 7 |
| Partial / failing / untested | 3 |
| B2 exact focus | 8/8 passed |

The unchecked aggregate tasks are expected for this partial chain slice and are not used as a blocker. The verdict concerns B2 autonomous commit safety only.

## Workspace, Ancestry, Status, Drift, and Budget

| Check | Fresh result |
|---|---|
| Branch | Exact requested branch |
| HEAD | Exact requested parent `1f00184...` (candidate is uncommitted) |
| Merge base / ahead-behind | Exact parent / `0 0` |
| Staged files | None |
| Tracked modifications | Exactly `AntiTamperMonitor.cs`, `AntiTamperMonitorTests.cs` |
| Untracked | Cumulative `openspec/changes/remote-signal-integrity/`, including this new B2 report |
| Deleted files/tests | None |
| Unit4C / Unit5 content drift | None |
| Project/dependency/config drift | None |
| Restore during verify | None; scoped assets already existed |
| `git diff --check` | Exit 0 before and after execution |
| Line endings | Index LF; worktree reported `w/mixed` and future LF→CRLF warnings on both changed files; normalized and `--ignore-cr-at-eol` numstat remain identical, so no separate content drift |
| Commit | None |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 37 | 16 | 53 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 38 | 0 | 38 |
| **Total** | **75** | **16** | **91/400** |

OpenSpec/docs are excluded from CODE+TEST accounting and reported separately as the cumulative untracked artifact tree.

## Fresh Build and Test Execution

Every .NET command used `--no-restore`. Tests used `--no-build` after fresh builds.

| Evidence | Local start → end | Exit | Result |
|---|---|---:|---|
| Domain portable-PDB build | `17:46:17.7345396` → `17:46:19.8940776` | 0 | 0 errors |
| Service portable-PDB build | `17:46:19.9001097` → `17:46:26.6794622` | 0 | 0 errors; existing NU1601 |
| Service-tests portable-PDB build | `17:46:26.6804330` → `17:46:40.4908785` | 0 | 0 errors; existing NU1601/NU1701 |
| App.UI portable-PDB build | `17:46:40.4908785` → `17:47:06.8563490` | 0 | 0 errors; existing NU1601 |
| App.UI-tests portable-PDB build | `17:47:06.8563490` → `17:47:37.1357954` | 0 | 0 errors |
| Exact B2 focus | `17:47:53.0122182` → `17:47:57.4150790` | 0 | **8 passed** |
| B1+B2 lifecycle matrix | `17:47:57.4210797` → `17:47:59.8441291` | 0 | **22 passed** |
| Full AntiTamper | `17:47:59.8441291` → `17:48:02.4521934` | 0 | **44 passed** |
| Unit4A+B1+B2 focus | `17:48:02.4521934` → `17:48:05.4038235` | 0 | **132 passed** |
| Full Service | `17:48:12.8329650` → `17:48:25.6448142` | 0 | **1,198 passed**; existing duplicate-ID notice |
| Full App.UI | `17:48:25.6518139` → `17:48:31.8879337` | 0 | **192 passed** |

### Exact Commands

```text
dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0
dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0
dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0
dotnet build src\ControlParental.App.UI\ControlParental.App.UI.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0
dotnet build tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:DebugSymbols=true -p:WarningLevel=0

dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~LateNonCancellableNormalCompletion_IsIgnoredAfterStop|FullyQualifiedName~LateNonCancellableFailure_IsObservableAndHasNoEffects|FullyQualifiedName~PostInitialLoopFault_IsObservableAndRestartUsesFreshGeneration|FullyQualifiedName~ExternalCancellation_IsReturnedToCallerAndOwnerCanDrain|FullyQualifiedName~RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks|FullyQualifiedName~Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer"

dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~StartAsync_WhenDisposed_ThrowsObjectDisposedException|FullyQualifiedName~ConcurrentStart_SharesOutcomeAndCleansOneGeneration|FullyQualifiedName~StartAndDispose_CancelsOwnedInitializationAndCleansOnce|FullyQualifiedName~StopBeforeStart_AllowsLaterStart|FullyQualifiedName~ConcurrentStop_SharesDrainUntilAdmittedWorkReleases|FullyQualifiedName~LateNonCancellableNormalCompletion_IsIgnoredAfterStop|FullyQualifiedName~LateNonCancellableFailure_IsObservableAndHasNoEffects|FullyQualifiedName~PostInitialLoopFault_IsObservableAndRestartUsesFreshGeneration|FullyQualifiedName~ExternalCancellation_IsReturnedToCallerAndOwnerCanDrain|FullyQualifiedName~ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock|FullyQualifiedName~RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks|FullyQualifiedName~Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer|FullyQualifiedName~ConcurrentStopAndDisposeShareLifecycleOwnership|FullyQualifiedName~TamperCallback_CanRequestStopWithoutSelfDeadlock|FullyQualifiedName~SynchronousAdmission_IsRemovedBeforeDrain"

dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AntiTamperMonitorTests"
dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests|FullyQualifiedName~EnforcementLevelMonitor"
dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal
dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal
```

## Coverage Evidence

Both hosts ran sequentially in separate result directories.

| Host | Local start → end | Exit / tests | Aggregate | `AntiTamperMonitor` |
|---|---|---|---|---|
| A | `17:48:52.3348004` → `17:49:26.2235540` | 0 / `1,198` | 54.29% line / 58.68% branch | **99.02% / 88.63%** |
| B | `17:49:26.2320503` → `17:49:59.8621153` | 0 / `1,198` | 54.31% line / 58.70% branch | **99.02% / 88.63%** |

Artifacts and freshness:

1. `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-independent-a-20260821/4b51621d-9393-4fdb-8c93-c55fbbd3979a/coverage.cobertura.xml` — written `2026-08-21T22:49:25.7666672Z`, 4,831,861 bytes.
2. `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-independent-b-20260821/0cb34cd6-d344-436e-872a-203f98fd86f7/coverage.cobertura.xml` — written `2026-08-21T22:49:59.4059917Z`, 4,831,855 bytes.

Required B2 code has nonzero hits, including both trigger overloads, fault-observing `AdmitTask`, drain, loop-fault, and owned-check paths. Coverage also exposes the proof gaps: `CheckPrivilegeStatusAsync` is only 25% line / 25% branch, `PerformIntegrityCheckAsync` 75% / 62.5%, `ProcessVerdictReactionAsync` 70.73% / 55%, and trigger guards are only partially branched. Aggregate coverage does not prove the missing invalidation windows.

## B2 Acceptance Matrix

| # | Contract | Source and fresh runtime evidence | Result |
|---:|---|---|---|
| 1 | External caller cancellation propagates without falsely stopping/faulting healthy owner | Linked caller/owner token at `AntiTamperMonitor.cs:230-235`; non-cancellation fault filter at `:225-229`; `ExternalCancellation_IsReturnedToCallerAndOwnerCanDrain` passes and subsequent Stop succeeds | ✅ COMPLIANT |
| 2 | Owner shutdown OCE contained; admissions close, cancel, drain once | Admission closure/cancel/drain at `:179-198`, cancellation containment at `:247-263`, exact B1 cleanup matrix remains green in 22/22 and AntiTamper 44/44 | ✅ COMPLIANT |
| 3 | Late non-cancellable normal backend completion has no post-stop effects and Stop waits | Active-generation checks at `:483-505`, handler/store gates at `:510-535`; three verdict variants assert Stop incomplete and zero handler/enforcement/outbox calls, all pass | ✅ COMPLIANT for the tested backend-blocked phase |
| 4 | Late unique failure remains observable with exact identity and no effects | Failure propagation at `:230-245`, lifecycle observation at `:225-229`; test observes same exception from operation and Stop with zero handler/enforcement/outbox calls | ✅ COMPLIANT |
| 5 | Post-initial source/loop/owned fault observable; restart/resources stable | Lifecycle TCS at `:41-43`, loop completion/fault at `:253-264`; post-initial tick fault test observes exact identity and a fresh generation; B1 resource/repeated-stop tests remain green | ✅ COMPLIANT |
| 6 | Restart after fault rejects every stale periodic/timezone/manual callback/completion | Fresh generation identity is asserted, and B1 rejects stale periodic/timezone admission after a normal drain. No passing test drives stale periodic/timezone/manual callback or completion from the faulted generation while the new generation is active | ❌ UNTESTED |
| 7 | Recheck after every external await and before every side effect, including backend→policy and policy→store windows | Checks exist, but they are not atomic with invalidation. Privilege completion can mutate before the caller's post-await check; backend/policy/store check-to-call windows remain. No test places a barrier in either requested window | ❌ FAILING (source defect + missing runtime test) |
| 8 | No unobserved fault, lock over external await, deadlock/B1 regression, retry move, or Unit4C change | Fault continuation observes task exceptions; external awaits occur outside `lockObject`; B1 22/22, Unit4 focus 132/132; no BackendClient, handler, C, or Unit5 diff | ✅ COMPLIANT for changed scope |
| 9 | Exact snapshots and physical counts with deterministic harness; no Delay/Yield/sleep/mock-only proof | New tests use RCSA TCS barriers and no Delay/Yield/sleep. However they assert selected mock invocation emptiness, not exact before/after complete state/collaborator snapshots, and do not assert physical cleanup counts for late normal/failure/fault cases | ❌ UNTESTED |
| 10 | Unit4A authority/persistence and B1 lifecycle remain green | Unit4 focus 132/132, B1+B2 22/22, AntiTamper 44/44, Service 1,198/1,198, App.UI 192/192 | ✅ COMPLIANT |

**Acceptance summary**: **7/10 compliant**. Items 6, 7, and 9 block B2 approval.

## Source Correctness Findings

### CRITICAL — invalidation is not closed around external-await side effects

1. `CheckPrivilegeStatusAsync` awaits the external inspector at `AntiTamperMonitor.cs:446`, then can call `RecordTamperEvent` at `:448-464` without a generation argument or active-generation recheck. The caller checks generation only after the entire method returns at `:355`. If Stop closes admission while the inspector ignores cancellation, the late `false` result can append a tamper event, fire callbacks, and enter ownerless outbox behavior (`:607-637`) after invalidation.
2. Backend completion is checked at `:502-505`, but Stop can invalidate between that check and `HandleLocalFailure` (`:513-517`) or `HandleVerdict` (`:521-525`). The late-normal tests stop while the backend is still blocked, so they only prove rejection at the first post-backend check, not the requested backend→policy cancellation window.
3. Policy-to-store checks at `:559`, `:581`, and `:592` are separate from external `ResolveIssueAsync` / `AddIssueAsync` calls at `:560-563`, `:582-586`, and `:593-597`. Stop can invalidate between the check and call. No deterministic policy barrier test executes this window.
4. `CheckTimezone` mutates under a valid-generation lock, then publishes tamper/timezone callbacks after releasing it (`:412-432`). Stop can invalidate between the mutation and publication; `RecordTamperEvent` itself adds the event before validating the owner (`:618-624`). `CheckClockIntegrityAsync` similarly calls `FireClockJump` outside the state lock at `:396` without a final owner validity boundary.

These are source-level violations of the explicit “recheck after every external await and before every side effect” contract, not coverage-percentage concerns.

## Strict TDD Audit

| Check | Result | Details |
|---|---|---|
| TDD evidence table | ✅ | B2 table exists in cumulative apply progress |
| Initial missing assets | ➖ | Correctly infrastructure-blocked, not behavioral RED |
| Genuine pre-production behavioral RED | ❌ MISSING | No exact post-restore, pre-production command/timestamp/failing production assertion is retained for any B2 production-requiring behavior |
| RED assertion quality | ❌ NOT PROVEN | Apply claims prior swallowing/fault behavior but provides no executed assertion failure; compile/missing assets receive no credit |
| Fresh GREEN | ✅ | Exact B2 8/8 and all safety nets pass |
| Same RED→GREEN test | ❌ NOT RECONSTRUCTABLE | No genuine RED command/output exists to match against current GREEN |
| Refactor safety | ⚠️ | Current behavior remains green, but mandatory invalidation scenarios are missing |

Project precedent in the authoritative Unit4 reports classifies missing genuine/raw RED chronology as a **Strict-TDD WARNING**, not an independent approval blocker when current correctness and required runtime coverage are complete. That precedent is followed here. It does not rescue B2: current correctness and required runtime coverage are independently blocking.

## Test Layer Distribution and Assertion Quality

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Unit/mock lifecycle | 8 exact B2 outcomes; 44 AntiTamper total | 1 | Calls production monitor with deterministic TCS/Channel seams and mocked ports |
| Production-component harness | Unit4A cases inside 132 focus | Existing | Authority/persistence baseline remains green |
| E2E/external | 0 | 0 | Correctly outside B2 |

- No tautology, ghost loop, `Task.Yield`, sleep, or timing delay exists in the added B2 tests.
- The tests call production code and use deterministic TCS barriers.
- Assertion quality is still insufficient for acceptance 9: selected mock invocation lists are not complete physical snapshots, and the late/fault tests omit exact resource/state before/after assertions.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Backend remains authority and retry owner | ✅ | No BackendClient or local baseline change |
| B1 generation ownership/resources preserved | ✅ | B1 matrix and full suites green |
| Definitive-only durable mutation | ✅ baseline | Unit4A focus green |
| Completion invalidation before all effects | ❌ | Privilege/event and check-to-handler/store windows remain |
| Fault observability | ✅ partial | Explicit operation, LifecycleTask, and Stop paths covered for tested failures |
| Unit4C ordering/escalation excluded | ✅ | No handler/order/escalation source or test diff |

## Issues Found

### CRITICAL

1. External-await invalidation is not closed: late privilege completion can publish tamper/outbox effects, and backend→policy / policy→store checks have check-to-use races.
2. No passing runtime test covers stale periodic/timezone/manual callback or completion from a faulted generation against a restarted generation.
3. No passing B2 test provides the required exact before/after complete collaborator/state snapshots and physical cleanup counts for late normal, late failure, and fault paths.

### WARNING

1. Strict TDD has no genuine executed behavioral RED after assets were restored and before B2 production edits. Per existing project policy this is warning-only, but it must not be rewritten as a RED success.
2. Existing NU1601/NU1701 warnings and one duplicate xUnit test-ID discovery notice remain; no dependency declarations changed.
3. Both changed files report mixed worktree line endings and future LF→CRLF conversion warnings. Normalized content/budget is stable and `git diff --check` passes.

### SUGGESTION

None. Verification was report-only.

## Final Verdict

**FAIL**

B2 is **not approved for autonomous commit**. Green aggregate execution and 99.02% line coverage do not prove the mandatory invalidation windows, stale-after-fault restart isolation, or exact snapshot/resource contracts. Tasks remain intentionally 9/14, and no claim is made about Unit4C, Unit5, full Unit4 completion, or archive readiness.
