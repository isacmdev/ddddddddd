# Verification Report

**Change**: `remote-signal-integrity` — SDD7 Unit 4C Verdict Ordering + Escalation only  
**Branch**: `feat/sdd7-4c-verdict-ordering-escalation`  
**HEAD / parent / merge-base**: `6c6668bbdb5eb2bc328cfe86c28f878a51149679`  
**Mode**: Strict TDD, hybrid persistence, autonomous partial work-unit gate  
**Verification time**: 2026-08-22, local offset `-05:00`  
**Verdict**: **FAIL**  
**4C commit approval**: **NOT APPROVED**

## Executive Summary

The candidate is isolated to the three claimed CODE+TEST files and is exactly **349/400** changed lines. All five portable-PDB builds passed, the exact new 4C focus passed `9/9`, the complete handler suite passed `32/32`, AntiTamper passed `55/55`, the combined Unit4 integrity/enforcement focus passed `127/127`, enforcement/health passed `89/89`, full Service passed `1,217/1,217`, full App.UI passed `192/192`, and coverage host A passed `1,217/1,217`.

Approval is nevertheless blocked by current correctness and runtime-evidence failures. Equal definitive timestamps have no authority tie-break and therefore linearize by lock acquisition/thread completion. Committed callbacks and notifications are not epoch-bound, so an older callback can run after a newer transition. Timer factory/disposal and the injected clock execute while `stateGate` is held. The production timer has no reaction owner because production DI supplies no `onReaction`; after it fires, later revoked inputs return `Warn`, so the timer can permanently consume escalation without durable enforcement. Handler count/deadline/epoch state is memory-only and is not rehydrated on restart. Recovery is emitted repeatedly on every trust after the threshold. The tests do not cover equal timestamps, concurrent mixed verdicts, stale callback completion, notification faults, clock rollback, pending-escalation restart, agent-death/enforcement races, or physical timer cleanup.

The requested second isolated coverage host also failed `LateNonCancellableFailure_IsObservableAndHasNoEffects`: no `InvalidOperationException` was observed at `AntiTamperMonitorTests.cs:416` (`1 failed, 1,216 passed`, exit `1`). This independently makes the fresh execution gate fail.

Tasks intentionally remain **9/14**. That partial task state is not itself a blocker. Unit 5 was not inspected or verified as candidate scope.

## Artifact and Normative Contract Audit

Read before judgment: proposal, exploration, all four specs, design, tasks, complete cumulative apply progress, every Unit4/4A/4B1/4B2 FAIL and approval report present in the change tree, and the strict-TDD verification module.

CodeGraph was attempted first by checking the exact worktree. `.codegraph/` is absent; the explicit no-index-creation boundary required focused direct inspection. No index was created.

### Normative verdict/restart table derived from specs

| Input/state | Normative result from specs | Exactness available |
|---|---|---|
| Definitive backend `trust` | May authoritatively recover only the matching durable issue | Defined at capability level |
| Definitive backend `revoked` | Must create/retain one durable matching issue and apply definitive enforcement | Defined at capability level |
| Unknown/missing/malformed/unavailable/timeout/cancelled | Observable; must not degrade, recover, or claim trust | Defined |
| Restart/lifecycle transition | Integrity state must be explicit and restore deterministically | Defined |
| Concurrent restart/verdict/agent-death/enforcement | One deterministic session order must preserve current authority | Defined |
| Revoked threshold | Delta specs do not state `3`, distinct vs duplicate, or consecutive semantics | **Not normatively defined** |
| Escalation delay/origin/boundary | Delta specs do not state five minutes, origin, or at-boundary behavior | **Not normatively defined** |
| Equal backend timestamps/tie-break | Delta specs do not establish timestamp trust or a tie-break sequence | **Not normatively defined** |

The source header/constants describe three consecutive revoked verdicts and five minutes (`IntegrityVerdictHandler.cs:10-18,93-98`), but those implementation comments are not a substitute for the requested spec-derived normative contract. This specification gap prevents claiming exact threshold/deadline compliance.

## Completeness

| Metric | Value |
|---|---:|
| Parent SDD7 tasks | 14 |
| Checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| Unit4C acceptance groups | 6 |
| Fully compliant | 0 |
| Partial | 2 |
| Failing/untested | 4 |

The planned `9/14` partial state is not classified as an incomplete-task defect for this autonomous slice.

## Source Correctness and Acceptance Matrix

| # | Acceptance group | Source/runtime evidence | Result |
|---:|---|---|---|
| 1 | State synchronization / linearization | Mutable handler state is generally under `stateGate` (`145-172`, `177-183`, `207-304`, `307-318`, `428-444`, `495-539`). Older timestamps are rejected only with `<` (`214-218`); equal trust/revoked values both mutate and whichever thread acquires the lock first wins. No observation sequence or tie-break exists. Callback dispatch occurs after unlock (`184-188`, `314-317`, `444`) but is not bound to the committed epoch, so newer state may commit before an older callback completes/emits. Tests `553-704` are sequential and do not cover mixed concurrent authority. | ❌ FAILING / UNTESTED |
| 2 | Callback / fault ownership | Synchronous reaction and notification faults are not discarded; `GetAwaiter().GetResult()` observes outbox faults (`192-204`, `451-489`). However timer construction/disposal occurs under `stateGate` (`263-271`, `378-390`, `414-422`), and `clock()` is called under the lock (`431-435`), violating the no timer/clock/user work under state lock rule. Timer callback exceptions have no lifecycle/task owner. A reaction callback fault prevents notification flush, leaving queued stale notification work for a later call. No notification-fault, concurrent callback-order, or stale callback-completion runtime test exists. | ❌ FAILING |
| 3 | Authority / mixed order | Production monitor still gates durable mutation to successful exact `trust|revoked` (`AntiTamperMonitor.cs:560-599`) and uses Unit4A canonical durable key/reaction flow (`608-655`). Non-definitive runtime snapshots remain green. But equal definitive timestamps are treated as independent authoritative samples, and the production runtime test deliberately sends the first three revoked reports with the same wall-clock timestamp (`IntegrityRuntimePathTests.cs:40`), without a documented trustworthy timestamp/tie-break contract. | ⚠️ PARTIAL |
| 4 | Exact escalation | Current source starts a deadline on the third accepted revoked (`344-395`, `414-425`) and tests before/after behavior. The test named exact deadline checks one tick before and one tick **after**, not equality (`IntegrityVerdictHandlerTests.cs:592-613`). Equal/duplicate revoked samples count toward the threshold. After timer fire, `escalationFired` makes subsequent revoked return `Warn` (`398-404`). Production DI constructs the handler without `onReaction` (`Program.cs:402-404`), so timer fire (`428-448`) cannot enter `AntiTamperMonitor.ProcessVerdictReactionAsync`; it can consume the one-shot escalation without creating the durable issue. If the injected clock is behind at physical fire, the one-shot callback returns without rescheduling. | ❌ FAILING |
| 5 | Restart / agent death / enforcement | Durable Unit4A issue recovery remains green, but all handler ordering/deadline fields are memory-only (`100-121`). Recreating `IntegrityVerdictHandler` resets revoked/trust counts, due time, epoch, and timer. `IntegrityRuntimePathTests.cs:64-76` recreates policy only after a durable issue already exists; it does not rehydrate a pending count/deadline or prevent a fresh five-minute window. No production-path test races agent death/restart with real handler trust/revoked/recovery and enforcement completion. No handler timer/subscription physical cleanup assertions exist. | ❌ FAILING / UNTESTED |
| 6 | Boundary / regression | No B2 helper, Unit4A authority, BackendClient, composition/Program, dependency/configuration, or Unit5 content diff exists. Exact scope and 349-line budget pass. But `onReaction`, `timerFactory`, and `clock` are public constructor seams whose escalation reaction path is test-only in current production composition; the production timer is not wired to durable enforcement. Coverage host B also failed inherited B2 completion-safety runtime evidence. | ⚠️ PARTIAL |

## Detailed Blocking Findings

### 1. Equal timestamps and duplicate inputs are completion-order authoritative

`IntegrityVerdictHandler.cs:214-218` rejects only timestamps strictly older than `lastDefinitiveTimestamp`. Equal timestamps pass and mutate counters/state at `261-299`. There is no monotonic observation ID, identity generation, or deterministic same-time precedence. The real runtime test sends three revoked reports at one identical `now` (`IntegrityRuntimePathTests.cs:40`), making three duplicates sufficient to start escalation. No passing test defines trust-vs-revoked equality order.

### 2. Callback/notification effects are not commit-epoch guarded

State commits under the lock, then `onReaction` and notification flush run outside it (`177-204`). Another thread can commit newer trust/revoked state before the prior callback or outbox operation completes. The callback carries only `VerdictReaction`, not an epoch, timestamp, or generation. A stale pending escalation notification may therefore emit after trust reset. The sequential list assertion at `IntegrityVerdictHandlerTests.cs:664-681` does not exercise this race.

### 3. Timer escalation is not production-owned

The timer publishes only through optional `onReaction` (`IntegrityVerdictHandler.cs:428-448`). Production DI creates `new IntegrityVerdictHandler(outbox)` and supplies no callback (`Program.cs:402-404`). `AntiTamperMonitor` applies only the reaction returned synchronously by `HandleVerdict` (`AntiTamperMonitor.cs:581-599`). Consequently, timer-only `Degrade` never reaches durable enforcement in production. Once timer fire sets `escalationFired`, later revoked inputs return pending `Warn` at `398-404`.

### 4. Restart silently erases pending escalation

No durable owner stores `consecutiveRevokedCount`, `consecutiveTrustCount`, `lastDefinitiveTimestamp`, `escalationDueAt`, `stateEpoch`, or `escalationEpoch`. A process/handler restart starts at zero and grants a new threshold/window. The existing restart path verifies durable issue recovery, not pending escalation rehydration.

### 5. Duplicate recovery reactions are possible

Every trust increments `consecutiveTrustCount`; every value `>= 3` returns `IsAuthoritativeRecovery=true` (`261-286`). There is no one-shot recovery latch. Fourth and subsequent trust samples can therefore emit repeated recovery callbacks and durable resolve attempts, contrary to at-most-one reaction per committed transition.

### 6. Fresh second coverage host failed

At `2026-08-22T12:27:39.7579870-05:00` → `12:28:13.3954347-05:00`, coverage host B exited `1`: `AntiTamperMonitorTests.LateNonCancellableFailure_IsObservableAndHasNoEffects` expected `InvalidOperationException` but no exception was thrown at test line 416. Result: `1 failed, 1,216 passed`. This is the same completion/fault area required by the 4C restart/enforcement race gate and cannot be accepted as a successful second host.

## Fresh Build and Test Execution

Every .NET command used `--no-restore`; tests/coverage used `--no-build` after fresh builds.

| Evidence | Start → end (`-05:00`) | Exit | Result |
|---|---|---:|---|
| Domain portable-PDB build | `12:29:28.9196893` → `12:29:31.3766895` | 0 | 0 errors |
| Service portable-PDB build | `12:29:31.3856878` → `12:29:38.5024691` | 0 | 0 errors; existing NU1601 |
| Service-tests portable-PDB build | `12:29:38.5024691` → `12:29:52.8680139` | 0 | 0 errors; existing NU1601/NU1701 |
| App.UI portable-PDB build | `12:29:52.8690147` → `12:30:19.5761101` | 0 | 0 errors; existing NU1601 |
| App.UI-tests portable-PDB build | `12:30:19.5771092` → `12:30:49.9921402` | 0 | 0 errors |
| Exact new 4C focus | `12:25:41.4459321` → `12:25:46.4338703` | 0 | `9/9` |
| Full `IntegrityVerdictHandlerTests` | `12:25:52.6837709` → `12:25:55.2866891` | 0 | `32/32` |
| Full AntiTamper (B1+B2 lifecycle/fault superset) | `12:26:02.2172600` → `12:26:05.1411122` | 0 | `55/55` |
| Combined Unit4A+B1+B2+4C/enforcement focus | `12:26:12.3456391` → `12:26:15.6163312` | 0 | `127/127` |
| Enforcement + health suites | `12:28:49.2555784` → `12:28:52.0641142` | 0 | `89/89` |
| Full Service | `12:26:22.0651530` → `12:26:35.8003017` | 0 | `1,217/1,217`; existing duplicate-ID notice |
| Full App.UI | `12:26:42.2021916` → `12:26:48.0504365` | 0 | `192/192` |
| Coverage host A | `12:26:56.7220348` → `12:27:31.7154570` | 0 | `1,217/1,217` |
| Coverage host B | `12:27:39.7579870` → `12:28:13.3954347` | **1** | **1 failed, 1,216 passed** |

Exact 4C filter: the eight newly added handler scenarios plus `CanonicalAuthority_PreservesExactStateAcrossNonDefinitiveResultsAndRecoversAfterRefresh`. Combined filter: `IntegrityCheckerTests|AntiTamperMonitorTests|IntegrityVerdictHandlerTests|IntegrityRuntimePathTests|EnforcementLevelMonitorTests`.

## Coverage Audit

Accepted host A artifact:  
`tests/ControlParental.Service.Tests/TestResults/coverage-unit4c-independent-a-20260822/cb6578bf-32ad-48c3-aa1c-23122455a69f/coverage.cobertura.xml`  
Written `2026-08-22T12:27:30.9774671-05:00`.

Failed host B artifact (not accepted as a passing coverage host):  
`tests/ControlParental.Service.Tests/TestResults/coverage-unit4c-independent-b-20260822/d1dd5e2a-bced-4109-b026-60605f248c1c/coverage.cobertura.xml`  
Written `2026-08-22T12:28:12.6432215-05:00`.

Both XML files report identical handler instrumentation because the failed test is in AntiTamper:

| Scope | Line | Branch | Required gap |
|---|---:|---:|---|
| `IntegrityVerdictHandler` | 97.98% | 91.66% | Aggregate does not establish concurrency/restart contracts |
| `HandleVerdict` | 100% | 100% | No equal-time mixed concurrency outcome |
| `HandleVerdictCore` | 96.66% | 96.66% | Disposed branch unhit; equal timestamps not a separate branch |
| `HandleRevokedVerdict` | 97.87% | 87.50% | Stage switch fallback line 369 unhit; one pending branch missing |
| `ScheduleEscalation` | 100% | 50% | Existing-timer disposal branch incomplete |
| `OnEscalationTimer` | 100% | 92.85% | Optional callback branch incomplete; no production durable owner |

Coverage hits active/stale timer branches, but it does not cover pending restart rehydration, equal timestamp authority, clock rollback/reschedule, stale callback completion, one-shot recovery, or physical production timer ownership.

## Strict TDD Audit

| Check | Result | Details |
|---|---|---|
| TDD evidence table | ✅ | Unit4C table exists in cumulative `apply-progress.md` |
| Aborted apply provenance | ⚠️ | Production handler edits already existed when the resumed apply began; provenance is explicitly unrecoverable |
| Genuine RED for resumed corrections | ⚠️ PARTIAL | Apply claims three behavioral failures before correction but does not retain exact command, timestamp, test names/output, or raw artifact |
| Compile/missing-assets failures | ➖ | Correctly not credited as behavioral RED |
| Same-test fresh GREEN | ✅ for reported tests | Exact focus `9/9`, handler `32/32` |
| RED for every production-changing ordering/deadline behavior | ❌ | No retained RED/test for equal-time ties, mixed concurrency, restart rehydration, production timer ownership, callback completion ordering, notification fault, or clock rollback |
| Triangulation | ❌ | Required scenarios above are absent; sequential tests cannot prove concurrent order |
| Safety net | ❌ | Coverage host B failed |

Per prior project verification policy, unrecoverable early chronology is retained as a **WARNING**, not rewritten as a clean RED. Current missing mandatory runtime scenarios and the failed host independently block approval.

## Test Layer Distribution and Assertion Quality

| Layer | Evidence | Assessment |
|---|---|---|
| Handler unit/component | 32 handler tests; 8 new 4C cases | Production handler called, mostly sequential |
| Production-component runtime | 9 Unit4A runtime cases; one modified for exact due wall clock | Real backend/parser/monitor/enforcement/store, but no pending-restart/timer callback path |
| Lifecycle/component | 55 AntiTamper tests | B1/B2 superset; second coverage host failed one fault case |
| External/E2E | 0 | Correctly excluded |

No literal tautology or ghost loop was found in the changed tests. Blocking assertion-quality omissions:

1. `HandleVerdict_ExactDeadlineTransitionsOnlyAtDueTimestamp` checks due-minus-one tick and due-plus-one tick, not equality.
2. `HandleVerdict_ReactionsAreCommittedInOrderBeforeCallbacksRun` is entirely sequential and does not prove callback order under concurrent commits.
3. `EscalationTimer_UsesInjectedClockAndSuppressesStaleGeneration` uses a test-only reaction callback and never drives production monitor/enforcement.
4. No changed test asserts equal timestamp behavior, duplicate suppression, fourth/subsequent trust one-shot recovery, physical timer disposal, restart rehydration, or agent-death/enforcement races.

## Workspace, Drift, and Budget

Final audit: `2026-08-22T12:29:10.5234296-05:00` → `12:29:11.0272956-05:00`.

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | Exact requested branch and `6c6668b...` |
| Staged files | None |
| Tracked content diff | Exactly 3 claimed files |
| Untracked | Cumulative OpenSpec tree plus this report |
| Dependency/project/config drift | None |
| Unit4A/B2/BackendClient/Program/Unit5 content drift | None |
| `git diff --check` | Exit 0; LF→CRLF notices only |
| `.codegraph` | Absent before/after; not created |

### Exact CODE+TEST budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | 145 | 37 | 182 |
| `tests/ControlParental.Service.Tests/IntegrityVerdictHandlerTests.cs` | 160 | 1 | 161 |
| `tests/ControlParental.Service.Tests/IntegrityRuntimePathTests.cs` | 3 | 3 | 6 |
| **Total** | **308** | **41** | **349/400** |

## Issues

### CRITICAL

1. Equal definitive timestamps have no deterministic authority tie-break; mixed trust/revoked outcomes depend on lock acquisition/thread completion.
2. Callback and notification completion are not epoch-bound; stale committed effects can emit after newer trust/revoked state.
3. Timer factory/disposal and injected clock execute under `stateGate`, violating the explicit lock boundary.
4. Production timer escalation has no durable reaction owner; timer fire can set `escalationFired` and leave future revoked inputs at `Warn` without creating the issue.
5. Pending threshold/deadline/epoch state is not durable or rehydrated; restart silently resets the escalation window.
6. Recovery is not one-shot: every trust after threshold can emit authoritative recovery again.
7. Required mixed-order, equal-time, stale callback, clock rollback, pending-restart, agent-death/enforcement race, and physical cleanup scenarios have no passing runtime tests.
8. Coverage host B failed the B2 late-failure observability test (`1/1,217` failed), so the mandatory two-host safety gate is not green.
9. Specs do not normatively define the requested exact count, duplicate semantics, five-minute origin/boundary, or timestamp tie-break; exact spec compliance cannot be claimed.

### WARNING

1. Unit4C Strict-TDD history is incomplete: aborted production edits predate the resumed evidence, and no raw/exact RED chronology is retained for all final behaviors.
2. Existing NU1601/NU1701/analyzer warnings and the duplicate xUnit test-ID notice remain outside this slice; no declaration drift exists.
3. The stale `IsShadowMode_DefaultsToTrue` test name remains misleading while asserting `false`.

### SUGGESTION

None. Verification was report-only.

## Final Verdict

**FAIL**

**NOT APPROVED FOR 4C COMMIT.** The green focused counts and 349/400 isolated diff do not compensate for ambiguous equal-time authority, stale callback effects, lock-held timer/clock work, a production-unowned escalation timer, non-durable restart state, repeated recovery, missing mandatory runtime scenarios, and the failed second coverage host.

This verdict applies only to autonomous Unit4C commit readiness. It does not mark tasks `4.1`–`4.3`, does not verify Unit5, and does not make a full Unit4/archive-readiness claim.
