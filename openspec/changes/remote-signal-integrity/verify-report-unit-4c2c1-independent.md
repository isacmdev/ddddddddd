# Verification Report

**Change**: `remote-signal-integrity` — rebuilt SDD7 Unit 4C2C1 only  
**Branch/base/HEAD**: `feat/sdd7-4c2c1-owner-rehydrate` / `1fc5b60dc801bbb12d25f2131cb17201c4f04b47` / same  
**Mode**: Strict TDD (authoritative launch override)  
**Verdict**: **FAIL**

## Scope and Completeness

| Dimension | Result | Evidence |
|---|---:|---|
| C1 task state | 1/1 checked, not verified complete | `tasks.md` 4.1 is checked, but required C1 scenarios below fail or remain untested |
| Cumulative tasks | 10/14 checked | 4.2, 4.3, 5.1, and 5.2 remain unchecked |
| C2 composition | NOT EVALUATED | Explicitly deferred; `Program.cs` blob equals HEAD (`96d75f43f3f694e44c73aaef835ab0d1eeacdd9e`) |
| Changed-path allowlist | PASS | Exactly the two C1 production files and their two test files are tracked modifications |
| CODE+TEST budget | PASS | `121+5`, `38+0`, `156+3`, `19+0` = **342/400** |
| Staged changes | PASS | None |
| Diff hygiene | PASS | `git diff --check` exited 0; only line-ending warnings |

Unchecked C2/integrated tasks are not failures in this C1-only slice. Task 4.1 itself is not complete because checked acceptance claims are contradicted by implementation and runtime-evidence gaps.

## Fresh Execution Evidence

| Gate | Result | Evidence |
|---|---:|---|
| Exact two-class discovery | PASS | `AntiTamperMonitorTests` and `IntegrityVerdictHandlerTests` discovered |
| Exact two-class execution | PASS | **121 passed**, 0 failed, 0 skipped |
| Service build | PASS | 0 errors; existing NU/analyzer warning corpus |
| Full Service suite | PASS | **1,280 passed**, 0 failed, 0 skipped; existing duplicate xUnit-ID notice |
| Isolated portable-PDB coverage | PASS | **121 passed**; Cobertura `...independent-coverage-portable/750a61aa-3bbd-4486-9a28-5b685c756574/coverage.cobertura.xml` |

Fresh focused coverage:

| Production surface | Line | Branch |
|---|---:|---:|
| `AntiTamperMonitor` | 99.04% | 81.11% |
| `IntegrityVerdictHandler` | 99.61% | 94.79% |
| `ExecuteDecisionChainAsync` state machine | 80.35% | 87.50% |
| `RehydrateAsync` state machine | 90.62% | 84.61% |
| `TriggerDeadlineCallbackAsync` | 100% | 50% |
| `ScheduleDeadline` | 100% | 62.50% |
| `ReconcileReactionAsync` / `ReconcileNotificationAsync` | 100% | 100% |
| `SaveProgressAsync` / `SaveStateAsync` | 100% | 50% / 100% |
| `EvaluateDeadlineDecision` | 100% | 90% |
| `Restore` | 100% | 75% |

High aggregate coverage does not prove missing behavioral branches; multiple required scenarios have no passing covering test or are contradicted by source.

## Strict-TDD Evidence Audit

| Cycle | Verdict | Evidence |
|---|---:|---|
| Batch 1 RED | PASS | Preserved compile RED, 8 missing-API errors, `EXIT=1`; hash matches ledger |
| Batch 1 GREEN | **FAIL** | Raw `sdd7-unit4c2c1-green.log` contains failing `HandleVerdict_RecoveryThreshold_EmitsAuthoritativeRecoverySignal` and `DeadlineCallback_IsNotBeforeDueAndFiresAtDueOnce`, then `EXIT=1`. `apply-progress.md:412` incorrectly claims this log passed |
| Batch 1 REFACTOR | PASS | Preserved final run exits 0 |
| Batch 2 RED | PASS | Four named tests fail, 0 pass |
| Batch 2 GREEN | PASS | 4/4 pass |
| Batch 2 REFACTOR | PASS | 4/4 pass |

The Batch 1 GREEN ledger is materially false even though a later refactor run and all fresh gates are green. Strict-TDD chronology therefore fails verification.

## C1 Behavioral Compliance Matrix

Runtime PASS requires a passing covering test, not source inspection alone.

| Required C1 scenario | Status | Evidence / reason |
|---|---:|---|
| Restart and recovery preserve semantics | FAIL | Recovery crash replay is reconciled as `AddIssueAsync`, never `ResolveIssueAsync` (`AntiTamperMonitor.cs:761-766`) |
| Missing state is a valid first start | PASS | `StartAsync_RehydratesBeforeRemoteAndMissingStateIsNormal` |
| Corrupt or unavailable state fails closed | UNTESTED | Load fault/cancellation passes, but no runtime test covers corrupt/unsupported persisted state |
| Concurrent mixed order | PASS | Existing serialized-generation decision tests pass |
| Stale callback and callback fault | FAIL | Timer callback does not retain its owner generation; it resolves the then-current generation (`797-818`) |
| Key and pure-handler boundary | PASS | Exact keyed production overload is used; focused tests pass |
| Supported safety evidence degrades health | PASS | Existing focused safety/enforcement behavior remains green |
| Semantic issue recovery survives restart | FAIL | Pending recovery identity is replayed as warning `AddIssueAsync`, the opposite semantic effect |
| Transient integrity failure preserves definitive state | FAIL | `HandleVerdictDecision` advances sequence/epoch before classifying unsuccessful/non-definitive input (`186-206`) |
| Agent-death and enforcement race | UNTESTED | No passing C1 scenario test combines the required race with durable rehydration/effect progress |
| First through fourth revoked | PASS | Handler staged-response tests pass |
| Owner callback before/at/after due | UNTESTED | Owner test covers before and at only; after-due and repeated owner callback are not covered together |
| Trust cancellation and stale timer callback | FAIL | Trust clears handler state but does not cancel the timer; an old timer can admit against a newer generation |
| Save-before-effect ordering | PASS | `AcceptedRevoked_SavesBeforeReactionAndPreservesExactKeys` passes |
| Restart resumes reaction independently | FAIL | Reaction reconciliation writes `PendingNotificationId` as completed (`766`), skipping a still-pending notification after crash |
| Restart resumes notification independently | PASS | Notification-only rehydration path has a passing runtime test |
| Three-trust recovery | UNTESTED | Pure handler test passes, but no owner/store test proves durable one-shot recovery and exact external resolve |
| Restart and crash convergence | FAIL | Reconciliation cannot distinguish add/degrade from recovery and can prematurely complete notification progress |
| Forward or rollback clock change | FAIL | Scheduling uses `DateTimeOffset.UtcNow` rather than monotonic elapsed time (`812`); restore does not compare persisted max clock with current clock (`482-487`), allowing rollback extension until a later evaluation |
| Rehydrate/effect fault and lifecycle cancellation | UNTESTED | Load fault/cancel is covered, but the combined save/effect/Stop/Dispose stale-or-duplicate guarantees are not covered for the new durable paths |

Summary: **7 PASS, 8 FAIL, 5 UNTESTED**.

## Correctness Findings

| Severity | Finding | Evidence |
|---|---|---|
| CRITICAL | Stale timer callbacks can target a newer generation | `ScheduleDeadline` discards the captured `owner`; `TriggerDeadlineCallbackAsync` looks up `this.generation` when invoked (`797-818`) |
| CRITICAL | Countdown is not monotonic and rollback can extend Pending | Direct wall-clock subtraction at `812`; restore only compares persisted state to the handler's prior in-memory max, not current wall clock (`482-487`) |
| CRITICAL | Reaction reconciliation is not independent | `SaveProgressAsync(state.PendingReactionId, state.PendingNotificationId, ...)` marks notification complete immediately after reaction (`766`) |
| CRITICAL | Crash replay cannot preserve recovery semantics | `ReconcileReactionAsync` always calls warning `AddIssueAsync` (`761-766`), including a pending authoritative recovery that must resolve |
| CRITICAL | Non-definitive reports mutate definitive sequence/epoch | Allocation and commit happen before verdict/success classification (`186-206`) |
| CRITICAL | Required scenario coverage is incomplete | Five scenarios are UNTESTED; SDD verify rules require passing runtime coverage |
| CRITICAL | Strict-TDD Batch 1 GREEN evidence is misreported | Raw log exits 1 while ledger says PASS |
| WARNING | State identity is captured once at construction | `stateIdentity = identityCoordinator?.CurrentState.DeviceId ?? "local"` (`AntiTamperMonitor.cs:97`); identity initialization/rotation can diverge. C2 composition is deferred, so production impact is not evaluated here |
| WARNING | Callback cancellation token is unused | `TriggerDeadlineCallbackAsync(CancellationToken)` never observes its parameter |

## Design Coherence

| Decision | Result |
|---|---:|
| `AntiTamperMonitor` is the sole async owner | PASS |
| Handler remains synchronous/pure | PASS |
| Save-before-effect through existing store/keyed APIs | PASS for first dispatch |
| One monotonic, generation-safe deadline owner | FAIL |
| Independent effect reconciliation | FAIL |
| Fail-closed restart/rollback convergence | FAIL |
| C2 Program composition | NOT EVALUATED |

## Issues

### CRITICAL

1. Fix generation ownership so a disposed/old timer cannot bind itself to a newer generation, and add a runtime stale-callback/fault/cancellation test.
2. Use monotonic elapsed countdown and fail closed immediately on persisted rollback/unusable timing without extending the due time.
3. Persist reaction completion without marking notification complete; prove both crash cuts independently in one durable state sequence.
4. Persist enough existing semantic state to replay authoritative recovery as resolve rather than warning add, without inventing a keyed recovery contract.
5. Preserve definitive sequence/epoch/counters/deadline for non-definitive outcomes and add assertions for those exact fields.
6. Add passing runtime coverage for every UNTESTED C1 scenario.
7. Correct the strict-TDD ledger: Batch 1's preserved GREEN artifact failed and must not be represented as a passing GREEN.

### WARNING

1. Revalidate identity selection across initialization/rotation during C2; the current constructor snapshot can remain `local`.
2. Either honor or remove the unused callback cancellation token.

### SUGGESTION

1. Keep C2 explicitly separate until C1 is reworked and independently reverified; green aggregate suites and high line coverage are not a substitute for the failed normative branches.

## Final Verdict

**FAIL** — not commit-ready and not archive-ready. Build, focused/full tests, coverage, scope, and budget are green, but required C1 behavior is incorrect or untested and the strict-TDD Batch 1 GREEN claim is contradicted by its preserved raw log. C2 remains **NOT EVALUATED**.
