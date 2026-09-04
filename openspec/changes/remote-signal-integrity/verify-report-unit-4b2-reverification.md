# Verification Report

**Change**: `remote-signal-integrity` — authoritative Unit 4B2 remediation re-verification only  
**Branch**: `feat/sdd7-4b2-lifecycle-completion-safety`  
**Parent / HEAD / merge-base**: `1f00184dd1fe1d96c9d683bbf7d2af356ddc30f4`  
**Mode**: Strict TDD, hybrid persistence, autonomous partial-slice gate  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**

## Executive Summary

All claimed execution counts reproduce from fresh sequential `--no-restore` commands: remediation `6/6`, B2 `14/14`, B1+B2 `28/28`, AntiTamper `50/50`, Unit4A+B1+B2 `138/138`, Service `1,204/1,204`, App.UI `192/192`, and two isolated coverage hosts `1,204/1,204`. The exact diff is `259/400`, only `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs`; the failed report remains byte-preserved at SHA-256 `681747c3f409f570e555e1232a13e016760d38e9f672b264eb9d1b187268013d`.

The production admission helper is structurally sound for the tested cases: it checks generation state and registers the wrapper task while holding the lifecycle lock, releases its RCSA gate after leaving the lock, executes/awaits collaborators outside the lock, and lets Stop drain accepted work. Active canonical Resolve and Stop-before-Resolve rejection both execute fresh.

Approval is still blocked by runtime-evidence defects hidden by green counts. `FaultedGeneration_RejectsStaleManualAndTimezoneCallbacksAfterRestart` captures G1/G2 from a local `monitor` but invokes stale callbacks through `InvokeGeneration`, which is hard-wired to the unrelated fixture `this.monitor`. Its event assertion also reads the fixture queue, not the local monitor. It therefore does not exercise stale G1 callbacks against G2. It never exercises a stale manual completion or asserts backend/collaborator non-invocation.

The claimed Add barrier is also not executed. `LatePolicyStage_AfterStopDoesNotStartDurableMutation(false)` closes Stop while the backend is blocked; the handler admission is rejected, so the code never reaches the Add admission boundary. Coverage confirms the Limit/Add lambda at source `613-615` has zero hits. No test races Stop immediately after an Add/Resolve action Task is created to prove accepted asynchronous work is owned and drained. The 75% branch coverage in `RunAdmittedStageAsync` leaves the required wrong-generation rejection branch unproven; only the admission-closed rejection is demonstrated.

## Artifact and Boundary Audit

- Read the original proposal/spec/design/tasks, cumulative apply progress, authoritative B1 report, preserved B2 FAIL report, complete current source/test files, and exact parent diff.
- CodeGraph was checked first. `.codegraph` remains absent; no index was created, and focused direct inspection was used.
- Unit4C, Unit5, `IntegrityVerdictHandler`, `IntegrityRuntimePathTests`, handler tests, and `BackendClient` have no diff from the approved parent.
- Tasks remain intentionally `9/14`; this is not an archive-readiness verification.
- This report is separate from and does not modify `verify-report-unit-4b2-independent.md`.

## Completeness

| Metric | Result |
|---|---:|
| Cumulative tasks | 9/14 intentionally checked |
| Exact remediation outcomes | 6/6 passed |
| B2 outcomes | 14/14 passed |
| B2 acceptance groups fully proven | 7/10 |
| Blocking evidence/correctness groups | 3 |

## Workspace, Drift, and Budget

| Check | Fresh result |
|---|---|
| Branch / HEAD / merge-base | Exact requested values; ahead/behind `0/0` |
| Staged files | None |
| Tracked candidate paths | Exactly two source/test paths |
| Untracked | Cumulative OpenSpec tree, including both B2 reports |
| Dependency/project/config drift | None |
| Unit4C/Unit5/handler drift | None |
| BackendClient retry-owner drift | None |
| Restore | None; ignored assets already present |
| `git diff --check` | Exit 0 before and after execution |
| Line endings | Index LF, worktree mixed warnings; normalized accounting unchanged |

### CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 90 | 42 | 132 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 126 | 1 | 127 |
| **Total** | **216** | **43** | **259/400** |

## Fresh Build and Test Execution

Every .NET command used `--no-restore`; tests and coverage used `--no-build` after the builds.

| Evidence | Start → end (`-05:00`) | Exit | Result |
|---|---|---:|---|
| Domain portable-PDB build | `18:28:38.9777903` → `18:28:41.8635648` | 0 | 0 errors |
| Service portable-PDB build | `18:28:41.8692316` → `18:28:49.5477838` | 0 | 0 errors |
| Service-tests portable-PDB build | `18:28:49.5477838` → `18:29:04.3020800` | 0 | 0 errors |
| App.UI portable-PDB build | `18:29:04.3020800` → `18:29:31.9612670` | 0 | 0 errors |
| App.UI-tests portable-PDB build | `18:29:31.9612670` → `18:30:02.5719541` | 0 | 0 errors |
| Exact remediation filter | `18:30:21.7099859` → `18:30:26.4085082` | 0 | **6 passed** |
| Exact B2 filter | `18:30:26.4135073` → `18:30:29.1405838` | 0 | **14 passed** |
| B1+B2 lifecycle | `18:30:29.1415841` → `18:30:31.9184362` | 0 | **28 passed** |
| AntiTamper | `18:30:31.9184362` → `18:30:34.8529969` | 0 | **50 passed** |
| Unit4A+B1+B2 | `18:30:34.8539636` → `18:30:38.1325056` | 0 | **138 passed** |
| Full Service | `18:30:45.6220464` → `18:30:58.2488612` | 0 | **1,204 passed** |
| Full App.UI | `18:30:58.2548603` → `18:31:05.0302398` | 0 | **192 passed** |

Existing NU1601/NU1701 warnings and one duplicate xUnit ID notice remain; declarations are unchanged.

## Coverage Evidence

| Host | Start → end (`-05:00`) | Exit / tests | Aggregate | AntiTamperMonitor |
|---|---|---|---|---|
| A | `18:31:14.3604568` → `18:31:48.6611970` | 0 / 1,204 | 54.39% line / 58.82% branch | 99.08% / 87.50% |
| B | `18:31:48.6701978` → `18:32:23.3991065` | 0 / 1,204 | 54.37% line / 58.80% branch | 99.08% / 87.50% |

Artifacts:

1. `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-reverification-a-20260821/9e6b49e7-1f54-4e83-bb79-f5d776d67e7a/coverage.cobertura.xml` — written `2026-08-21T23:31:47.9099494Z`, 4,840,157 bytes.
2. `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-reverification-b-20260821/1e4f7639-bd66-432c-835b-90a151e94829/coverage.cobertura.xml` — written `2026-08-21T23:32:22.6400942Z`, 4,840,122 bytes.

Required mapping findings in both hosts:

- `RunAdmittedStageAsync`: 100% line / 75% branch. Active path and `AdmissionOpen == false` rejection execute. The `ReferenceEquals(generation,current) == false` rejection required for stale G1 is not proven by the remediation test.
- Resolve lambda `ProcessVerdictReactionAsync>b__0`, source `593-594`: 100% line / 100% branch, 6 hits.
- Degrade/Add lambda `b__2`, source `622-624`: 100% line / 50% branch, 6 hits.
- Limit/Add lambda `b__1`, source `613-615`: **0% line / 0% branch**. This is not independently blocking because Limit and Degrade share the same helper, but it confirms the claimed Add race test does not reach that lambda.
- Privilege publication lambda body `484-490`: zero hits; only the rejected-stage continuation line maps as hit. The late privilege rejection is covered, but active privilege publication ownership is not runtime-proven by the remediation suite.

## Admission Helper Adversarial Inspection

`RunAdmittedStageAsync` (`AntiTamperMonitor.cs:220-234`) performs generation/disposed/admission checks and wrapper registration under `lockObject`, then releases the RCSA gate and awaits outside the lock. `AdmitLocked` registers the wrapper before release (`:219`, `:240-244`). This prevents a lost accepted task in the immediate pre-release Stop race. Task-returning actions are invoked by `RunAfterGateAsync` outside the monitor lock and their returned tasks are awaited (`:235`). Non-OCE exceptions retain identity through the wrapper and lifecycle observer.

No counter underflow, double gate release, external await under `lockObject`, retry-owner move, or Unit4C ordering change was found. Owner cancellation remains filtered during drain, caller cancellation remains linked and observable, and B1 lifecycle suites remain green.

However, required runtime proof is incomplete:

1. No test stops immediately after `AddIssueAsync`/`ResolveIssueAsync` returns an incomplete Task to prove the already-created external Task remains owned and Stop stays incomplete until it settles.
2. The wrong-generation helper branch central to G1→G2 rejection lacks a valid production call from the claimed test.
3. Synchronous handler reentrant `StopAsync` is exercised without awaiting it inside the handler. Synchronous handler reentrant `Dispose` or synchronously waiting on Stop is not covered; because the generic admitted stage does not set the callback-generation self-drain marker, that case remains a self-drain risk rather than proven safe.

## Previous-Failure Re-audit

| Prior blocker | Finding | Result |
|---|---|---|
| Privilege/local-failure post-stop admission | Source now uses atomic stage admission; late privilege rejection passes with snapshot/resources. No dedicated local-failure race and no active privilege-body hit | ⚠️ PARTIAL |
| Backend→policy and policy→Add/Resolve | Backend→handler rejection and Resolve rejection/active path execute. Claimed Add barrier stops before handler, never at Add; no post-Task-creation drain test | ❌ PARTIAL / REQUIRED GAP |
| Faulted G1→G2 stale callbacks/completions | Test invokes private callbacks on `this.monitor`, not the local restarted monitor, checks unrelated event queue, and omits stale manual completion/backend snapshot | ❌ UNTESTED |
| Exact snapshots/resources | Privilege and rejected Resolve add useful assertions, but stale test snapshots the wrong object and Resolve generation-removal assertion uses `this.GetGeneration()` from the fixture | ❌ PARTIAL |
| Active/rejected canonical Resolve | Exact canonical key/reason and one invocation; rejected admission zero invocation and snapshot | ✅ COMPLIANT |

## B2 Compliance Matrix

| # | Contract | Result |
|---:|---|---|
| 1 | External caller cancellation remains distinct and healthy owner drains | ✅ COMPLIANT |
| 2 | Owner OCE is contained; close/cancel/drain exactly once | ✅ COMPLIANT |
| 3 | Late normal backend completion cannot create effects; Stop drains | ✅ COMPLIANT for covered backend phase |
| 4 | Late unique failure identity remains observable | ✅ COMPLIANT |
| 5 | Post-initial fault and fresh restart remain observable/stable | ✅ COMPLIANT |
| 6 | Faulted G1 stale periodic/timezone/manual callbacks/completions cannot affect G2 | ❌ UNTESTED due wrong monitor/queue and missing completion/backend assertions |
| 7 | Every external-await transition and Add/Resolve stage is atomically admitted and drain-proven | ⚠️ PARTIAL; source pattern is sound, but Add boundary and post-Task-creation drain lack covering tests |
| 8 | No unobserved fault, lock-across-await, deadlock, retry/C regression | ⚠️ PARTIAL; normal paths pass, generic synchronous handler Dispose/self-wait is unproven |
| 9 | Exact before/after snapshots and physical counts for each race | ⚠️ PARTIAL; several new assertions target the fixture instead of the exercised local monitor |
| 10 | Unit4A authority/persistence and B1 lifecycle remain green | ✅ COMPLIANT |

**Compliance summary**: **7/10 fully compliant**. Required evidence remains incomplete for items 6, 7, 8, and 9, grouped into three blocking issues below.

## Strict TDD Audit

- Tests were authored before remediation source edits, but the attempted pre-edit run failed through discovery/barrier timeout rather than an intended production assertion.
- No genuine post-restore/pre-production behavioral RED exists. No RED is fabricated or upgraded.
- Fresh GREEN evidence is real and complete for the commands listed above.
- Per current project verification policy, this immutable process defect is a **WARNING**, not an independent correctness blocker. The current verdict fails on runtime-evidence and self-drain gaps instead.

## Test Quality

- Added remediation tests contain no `Task.Delay`, `Task.Yield`, sleep, retry, or timing poll. Retained baseline tests still contain an unrelated 100 ms outbox delay and cancellation-controlled infinite delay; neither is part of the new remediation proof.
- Production code is called, and TCS barriers are deterministic.
- The stale-generation test is invalid evidence: reflection dispatch targets the wrong monitor. This violates the explicit no direct-helper/reflection coverage-gaming gate even though the test passes.
- The rejected Resolve generation-removal assertion is also against the fixture monitor and is therefore tautological for the local monitor under test.

## Issues Found

### CRITICAL

1. `FaultedGeneration_RejectsStaleManualAndTimezoneCallbacksAfterRestart` does not invoke G1 callbacks on the restarted monitor; it uses a helper bound to `this.monitor`, checks the wrong event queue, omits stale manual completion/backend assertions, and leaves the required wrong-generation stage branch unproven.
2. The claimed Add barrier test stops at backend→handler rejection and never reaches Add admission. No test covers the immediate-after-action-Task-creation Stop race for Add or Resolve.
3. Generic admitted synchronous handler work has no callback marker. Reentrant handler `Dispose` or synchronously waiting on Stop can self-drain; no existing test proves this required case safe.

### WARNING

1. Strict TDD lacks a genuine behavioral RED after restore and before production remediation; immutable project-policy warning.
2. Active privilege publication and dedicated local-failure race paths lack direct runtime evidence despite source admission hardening.
3. Existing package/analyzer/duplicate-ID and line-ending warnings remain without declaration/content drift.

### SUGGESTION

None; report-only verification.

## Final Verdict

**FAIL**

Unit4B2 is **not approved for commit**. Aggregate GREEN and high coverage do not substitute for a test that targets the wrong monitor, an Add race that never reaches Add, and the unproven synchronous-handler self-drain case. Tasks remain intentionally `9/14`; no Unit4C, Unit5, full Unit4, or archive-readiness claim is made.
