## Verification Report

**Change**: `remote-signal-integrity` — Unit 1 tasks 1.1–1.3 only  
**Base / tracker**: `0a72ddd3b3271e2844805d2b092b2574d4b703dc` / `feat/sdd7-remote-signal-integrity`  
**Verified branch**: `feat/sdd7-1-scheduler-ipc`  
**Worktree**: `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-1`  
**Mode**: Strict TDD, fresh post-remediation re-verification  
**Verdict**: **FAIL**

### Verification Boundary

- `HEAD` and merge-base both resolve to `0a72ddd3b3271e2844805d2b092b2574d4b703dc`; there is no implementation commit.
- This is an independent verdict from current bytes. The historical `verify-report-unit-1.md` was inspected but not reused as evidence or modified; its hash remained `2001d2ebf96f9770c2800c64aebca6d3791a459d` before this report was created.
- CodeGraph was attempted first against the exact worktree. It reported no `.codegraph/` index, so verification used direct source/artifact inspection.
- Normative scope is `remote-signal-sync` scenarios 1–3. Scenario 4 and Units 2–5 are intentionally excluded.
- All 12 tracked changed code/test files and the untracked `RepositoryRootLocator.cs` were inspected. No Realtime, integrity, enforcement, durable WNS reconciliation, or legacy-WNS quarantine behavior from Units 2–5 was added.

### Completeness

| Metric | Value |
|---|---:|
| Scoped Unit 1 tasks | 3 |
| Scoped complete | 3 |
| Scoped incomplete | 0 |
| Global SDD7 progress | 3/14 |
| Units 2–5 | Intentionally pending |

### Diff Integrity and Review Budget

| Check | Fresh result |
|---|---|
| Tracked numstat | 328 additions + 47 deletions = 375 |
| Untracked helper | 24 added lines, final newline present |
| Exact review budget | **352 additions + 47 deletions = 399** |
| 400-line hard limit | ✅ 399 ≤ 400 (one line remaining) |
| `git diff --check` | ✅ Exit 0 at `2026-08-20T15:37:26.2665377-05:00` |
| Untracked-helper whitespace check | ✅ No whitespace diagnostics (`git diff --no-index --check`; exit 1 only because the file differs from `NUL`) |
| Scope | ✅ Unit 1 production behavior plus a test-infrastructure-only linked-worktree safety net |

`apply-progress.md` has the correct total of 399, although its note calls the helper 25 lines; the actual file is 24 lines and the arithmetic is 328 + 24 + 47 = 399.

### Build and Test Execution

All commands ran fresh from the requested worktree.

| Command | Start | Exit | Result |
|---|---|---:|---|
| `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --configuration Debug --verbosity minimal` | `2026-08-20T15:33:50.4400635-05:00` | 0 | ✅ 0 errors, 1 warning |
| `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --configuration Debug --verbosity minimal` | `2026-08-20T15:33:52.2707286-05:00` | 0 | ✅ 0 errors; existing warning corpus |
| `dotnet build tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --configuration Debug --verbosity minimal` | `2026-08-20T15:34:04.3582206-05:00` | 0 | ✅ 0 errors; existing warning corpus |
| Focused Service Unit 1/remediation filter | `2026-08-20T15:34:36.9182123-05:00` | 0 | ✅ 10 passed, 0 failed/skipped |
| Focused App.UI opaque-TriggerSync filter | `2026-08-20T15:34:41.6742305-05:00` | 0 | ✅ 3 passed, 0 failed/skipped |
| Full Service regression | `2026-08-20T15:34:45.7516127-05:00` | 0 | ✅ 1,165 passed, 0 failed/skipped |
| Full App.UI regression | `2026-08-20T15:34:59.2392803-05:00` | 0 | ✅ 186 passed, 0 failed/skipped |
| Full solution `--no-restore` | `2026-08-20T15:36:45.4373006-05:00` | 1 | ⚠️ 3 unchanged NETSDK1004 missing-assets errors in SessionAgent, Domain.Tests, and SessionAgent.Tests; affected projects above are green |

Focused Service command:

```text
dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~ScheduledWorkServiceAsyncDispatchTests.AdmitSyncAsync|FullyQualifiedName~UIMessageHandlerWnsTests.AuthenticatedTriggerSync|FullyQualifiedName~UIMessageHandlerWnsTests.UnauthenticatedTriggerSync|FullyQualifiedName~UIMessagesJsonContextTests.JsonSerializer_RoundTrip_AllSampleEnvelopes_PreservesData_ViaSourceGen"
```

Focused App.UI command:

```text
dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~WnsLifecycleTests.HandleRawNotificationAsyncWithMalformedPayloadEmitsTypedTriggerSync|FullyQualifiedName~WnsLifecycleTests.HandleRawNotificationAsyncWithValidPayloadEmitsTypedTriggerSync|FullyQualifiedName~WnsLifecycleTests.TriggerSyncRoundTripsThroughSourceGeneratedContext"
```

### Coverage

Fresh command (start `2026-08-20T15:35:14.8712963-05:00`, exit 0, 1,165/1,165 passed):

```text
dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-build --configuration Debug --collect:"XPlat Code Coverage" --results-directory tests\ControlParental.Service.Tests\TestResults\coverage-unit1-reverification-20260820-1535 --verbosity minimal
```

Cobertura artifact:

```text
tests\ControlParental.Service.Tests\TestResults\coverage-unit1-reverification-20260820-1535\3b9d99b6-686c-4588-b77e-130ad3b8b769\coverage.cobertura.xml
```

The report is meaningful, not empty: root totals are 11,483/17,372 lines (**66.10%**) and 2,631/3,988 branches (**65.97%**).

| Changed production scope | Line | Branch | Uncovered / changed-scope evidence | Rating |
|---|---:|---:|---|---|
| `IScheduledWorkService.cs` | N/A | N/A | Contract/enums have no executable sequence points | ➖ |
| `Program` class in `Program.cs` | 88.31% | 80.00% | Main-class uncovered lines 122–128, 236, 238; the changed `RunMainAsync` composition site was not executed | ⚠️ Composition site unhit |
| `ScheduledWorkService` class | **87.08%** | **85.93%** | Uncovered 240, 246, 252, 258, 317–320, 323–324, 327–330, 333–334, 337–340, 343–344, 348–350, 725–726, 807–809, 832–836 | ⚠️ Acceptable |
| `AdmitSyncAsync` state machine | **100%** | **100%** | All executable lines and branches covered | ✅ Excellent |
| `UIMessageHandler.HandleCoreAsync` | 64.10% | 77.27% | Uncovered unrelated cases 84–85, 88–89, 92–93, 96, 99, 102, 105, 108, 143–145; TriggerSync guard at line 131 is 4/4, result predicate at line 139 is 1/2 | ⚠️ Low file method coverage; added route executes |

The independently reproduced `ScheduledWorkService.cs` class rates exactly validate the reported **87.08% line / 85.93% branch** values.

### Spec Compliance Matrix

| Requirement | Scenario | Source evidence | Fresh passing runtime evidence | Result |
|---|---|---|---|---|
| Hints admit bounded durable convergence | 1. Concurrent hints coalesce | Bounded enum in `IScheduledWorkService.cs:49-56`; admission and shared `PolicySync` slot in `ScheduledWorkService.cs:162-206, 387-396`; startup/timer/polling use that same slot at 257-268 and 346-350 | `AdmitSyncAsync_AllBoundedSourcesCoalesceWithoutLosingConvergence`; `AdmitSyncAsync_ConcurrentHintsCoalesceToOnePolicyFetch` | ✅ COMPLIANT |
| Hints admit bounded durable convergence | 2. Offline or cancelled admission recovers | Caller token only wraps `dispatch.WaitAsync` at 197; scheduler lifetime owns underlying work token at 392-395; `StopAsync` cancels it at 289-301; identity/offline checks return safely at 659-668; polling timer remains | Caller-wait cancellation, shutdown cancellation, restartability, identity denial, backend failure, and polling tests pass separately | ❌ UNTESTED as a complete scenario — no passing test drives offline/backend/identity-denied or shutdown-prevented admission and then proves a subsequent admission or polling attempt converges |
| Hints admit bounded durable convergence | 3. Malformed or unauthorized hint is harmless | Opaque parameterless `TriggerSync`; authenticated handler guard at `UIMessageHandler.cs:130-140`; undefined source rejection at 166-169 | Authenticated/unauthenticated handler tests; malformed and valid payload opacity tests; unknown-source test; generated JSON round-trip | ✅ COMPLIANT |

**Compliance summary**: **2/3 scenarios compliant**. Scenario 2 remains a critical runtime-evidence gap under the rule that a scenario is compliant only when a covering test passes.

### Correctness (Static and Dynamic)

| Contract | Status | Evidence |
|---|---|---|
| Scheduler owns underlying cancellation | ✅ | Admission does not pass the caller token into dispatch; dispatch uses `workCancellation`; shutdown cancels it |
| Caller cancellation affects only that wait | ✅ | `WaitAsync(cancellationToken)` plus `AdmitSyncAsync_CallerCancellationOnlyCancelsThatWaiter` |
| Coalesced callers are not poisoned | ✅ | Cancelled owner returns `Cancelled`; coalesced waiter returns `Coalesced`; one fetch |
| Shutdown cancels shared work | ✅ | `AdmitSyncAsync_ShutdownCancellationCancelsSharedWork` observes the scheduler token cancellation after repository entry |
| Running/disposed behavior | ✅ | Guards at 180-182 and passing stopped/disposed test |
| All bounded sources share one flight | ✅ | One accepted, four coalesced, one backend fetch |
| Polling fallback remains | ✅ static / ⚠️ recovery proof gap | 30-second timer remains, but no end-to-end denied/offline-then-poll recovery test |
| IPC authentication and adapter authority | ✅ | Unauthenticated calls never reach scheduler; WNS emits only parameterless `TriggerSync`; no REST/retry/identity authority added |

### Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| One scheduler owner | ✅ | Handler resolves the existing singleton `IScheduledWorkService`; no second transport or scheduler |
| Bounded source/result contract | ✅ | Finite enums; undefined source rejected |
| Pipe → handler → scheduler | ✅ | Production composition is wired and handler tests pass |
| Preserve scheduler cancellation ownership | ✅ | Caller token is waiter-local; service lifetime owns shared work |
| Keep WNS payload opaque and adapter non-authoritative | ✅ | Existing App.UI tests pass for malformed and valid payloads |
| Polling provides eventual recovery | ⚠️ | Architecture remains present, but scenario-2 recovery is not covered by one passing runtime flow |

### App.UI Linked-Worktree Regression Safety Net

`RepositoryRootLocator` is internal test infrastructure only. It accepts either a `.git` directory or linked-worktree `.git` file, and four existing source/composition test classes now delegate only root discovery to it. No production project references it, no production source changed, and no behavioral assertion was deleted or weakened. The fresh full App.UI suite passed **186/186**. This is reasonable Unit 1 chain safety-net scope because the exact linked worktree is required for the mandated regression proof.

### TDD Compliance

The updated table is a real six-column table: RED, GREEN, TRIANGULATE, SAFETY NET, and REFACTOR plus task/cycle identity.

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Six-column table exists for all three scoped cycles |
| All scoped tasks have tests | ✅ | 3/3 task cycles reference existing test files |
| RED confirmed | ⚠️ | Missing symbols are consistent with base bytes and remediation RED details are recorded, but original raw RED and remediation raw output were not preserved |
| GREEN confirmed | ✅ | 10 focused Service, 3 focused App.UI, 1,165 Service, and 186 App.UI tests passed fresh |
| Triangulation adequate | ❌ | Scenario 2 has passing fragments but no complete recovery sequence |
| Safety net | ⚠️ | Current full suites are green; original pre-change raw safety-net execution/timestamps were not preserved |
| REFACTOR | ➖ | Subjective; current implementation inspected without modification |

**TDD compliance**: current GREEN and the remediation cancellation behavior are real. Historical raw RED limitations remain warnings; the current scenario-2 test gap is critical.

### Test Layer Distribution

| Layer | Focused tests | Files | Notes |
|---|---:|---:|---|
| Unit / in-memory harness | 13 | 5 | Scheduler, handler, serialization, and App.UI WNS signal tests |
| Integration | 0 | 0 | No real hosted pipe-to-scheduler recovery flow in this slice |
| E2E | 0 | 0 | Not required for local Unit 1 |
| **Total focused** | **13** | **5** | All passed |

### Assertion Quality

**Assertion quality**: ✅ No tautologies, ghost loops, production-free assertions, smoke-only assertions, or assertion weakening were found in the Unit 1 additions. Loop assertions have non-empty/setup guarantees or explicit cardinality assertions. The remaining problem is missing scenario sequencing, not trivial assertions.

### Quality Metrics

- **Compiler/type check**: ✅ All affected builds have 0 errors.
- **Analyzers**: ⚠️ Existing warning corpus remains; the new helper has style warnings but no build error or behavioral weakening.
- **Coverage**: ✅ Meaningful Cobertura reproduced; ⚠️ changed composition/handler aggregate coverage is below 80%, while the new admission state machine is 100%/100%.
- **Diff hygiene**: ✅ tracked and untracked content have no whitespace diagnostics.

### Issues Found

**CRITICAL**

1. **Remote-signal-sync scenario 2 still has no passing covering recovery test.** Current tests prove caller isolation, shutdown cancellation, stopped/disposed guards, identity denial, backend failure, restartability, and polling as separate fragments. None drives a prevented admission (offline/backend failure/identity denial or shutdown cancellation) and then proves a subsequent admission or polling attempt reaches one successful convergence. Therefore scenario 2 is `UNTESTED` as a complete scenario and blocks Unit 1 approval.

**WARNING**

1. Original raw RED and pre-change safety-net logs were not preserved. The report does not fabricate them.
2. Full solution `--no-restore` still fails only because three unrelated projects lack `project.assets.json`; affected Service and App.UI builds/tests are green, and dependencies were not mutated.
3. Cobertura shows the changed `Program` composition site was not executed and `UIMessageHandler.HandleCoreAsync` is below 80% aggregate coverage, although the added TriggerSync guard executed and `AdmitSyncAsync` is 100% line/branch covered.
4. `apply-progress.md` says the helper is 25 lines; it is 24. Its total budget claim of 399 is nevertheless correct.

**SUGGESTION**

- Add one barrier-controlled scheduler test that transitions from denied/offline/cancelled work to a later admitted or polling-triggered successful fetch, without changing authority or retry ownership.

### Verdict

**FAIL**

The prior cancellation defect, App.UI linked-worktree regression, meaningful coverage, and 399-line budget are all successfully remediated and independently proven. However, strict spec verification still finds remote-signal-sync scenario 2 without a passing covering recovery sequence. Unit 1 is therefore **not approved** for its local feature-chain boundary.

Full SDD7 remains **3/14**, is not final-verified, and is not archive-ready. The next eligible action is a user-authorized Unit 1 remediation followed by another fresh Unit 1 verification; no commit, Unit 2 preparation, PR, push, or archive was performed.
