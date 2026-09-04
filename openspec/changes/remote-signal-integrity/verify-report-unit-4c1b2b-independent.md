# Verification Report

**Change**: `remote-signal-integrity` — SDD7 Unit 4C1B2b Effect Dedupe/Retry only  
**Version**: SDD7  
**Mode**: Strict TDD (orchestrator-authoritative override), hybrid persistence  
**Branch / base**: `feat/sdd7-4c1b2-effect-dedupe-retry` / `4a6751bed7efc8d849260355e4c33fc8e2814b8c`  
**Verdict**: **FAIL**  
**Commit gate**: **NOT APPROVED FOR 4C1B2B COMMIT**

## Scope and Completeness

This report independently verifies only Slice B. Approved Slice A is the exact parent. Unit 4C2 durability, Unit 5, complete Unit 4 approval, and archive readiness are excluded. The cumulative task state remains intentionally 9/14 because tasks 4.1–4.3 and 5.1–5.2 aggregate deferred child work; those unchecked aggregate tasks are not treated as incomplete Slice B implementation tasks.

| Metric | Result |
|---|---:|
| Cumulative tasks | 9/14 checked; 5 aggregate tasks intentionally deferred |
| In-scope changed CODE+TEST files | 2 |
| CODE+TEST budget | **144/400** (`39+3` production, `102+0` tests) |
| HEAD / merge-base | exact supplied base `4a6751bed7efc8d849260355e4c33fc8e2814b8c` |
| Binary patch hash | `6fc94ec475433cae3e30c531e5416378e4e0f895` |
| Stable patch-id | `84023605aaaaea636a6f71076a7dc94c6bdd017c` |
| Index | empty |
| Deferred-scope drift | none found |

No handler, outbox API/implementation, persistence, clock/deadline, restart, transport, schema, Unit 5, task, design, or apply-progress source was changed by Slice B.

## Build and Runtime Evidence

The assigned worktree initially lacked App.UI assets. A scoped App.UI-tests restore was therefore run; all retained post-restore builds/tests used `--no-restore`, and tests used `--no-build`. Coverage results were written to two separate external result roots and are non-empty.

| Gate | Fresh result |
|---|---:|
| Five relevant builds: Domain, Service, Service.Tests, App.UI, App.UI.Tests | PASS after the allowed scoped restore; 0 errors, existing warning corpus |
| Slice B plus Slice A gate/fault/cancellation/order matrix | PASS 19/19 |
| Duplicate/retry/failure-order stress | PASS 4/4 in each of 10 fresh processes; 40/40 |
| AntiTamper + Outbox + Integrity + Enforcement combined Unit 4 focus | PASS 160/160 |
| Full Service run 1 | PASS 1,242/1,242 |
| Full Service run 2 | PASS 1,242/1,242 |
| Full App.UI known-good harness | PASS 192/192 |
| Full Service coverage host A | PASS 1,242/1,242; non-empty Cobertura |
| Full Service coverage host B | PASS 1,242/1,242; non-empty Cobertura |

The inherited duplicate xUnit-ID discovery notice remained. No executed test was reported skipped. The inherited late non-cancellable failure test passed in the focused gate and both full/coverage hosts.

### Coverage

Cobertura A: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c1b2b-independent-coverage-a\b470e74d-f5cd-4071-9fbd-2f933e2fd376\coverage.cobertura.xml`  
Cobertura B: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c1b2b-independent-coverage-b\cb5120f1-7d51-4d41-a4cf-007792517ccc\coverage.cobertura.xml`

| Changed production area | Line | Branch | Relevant evidence |
|---|---:|---:|---|
| `AntiTamperMonitor` class | 98.86% | 81.61% | gate, commit, no-commit, duplicate, retry, null-notification, newer/older, and reset paths hit |
| `ExecuteDecisionChainAsync` state machine | 85.18% | 85.00% | reaction and notification commits/faults hit; authoritative recovery lines 648–651 have zero hits |
| `ShouldExecute` | 87.50% | 75.00% | none/equal/older/newer paths partly hit; deterministic conflict throw line 698 has **zero hits** |

Coverage confirms the absence of runtime proof for equal-position conflict. It also does not cover the required outbox persist-then-throw convergence edge.

## Behavioral Compliance Matrix

| Invariant / scenario | Runtime/static evidence | Result |
|---|---|---|
| Exactly O(1) generation-scoped progress | two nullable `EffectProgress` records, one per domain; no Slice B collection/history | ✅ COMPLIANT |
| Existing generation gate owns recheck/effect/commit | source inspection; concurrent duplicate/order tests | ✅ COMPLIANT |
| No second queue/mailbox/channel/actor/drainer/semaphore/untracked task | complete Slice B diff inspection | ✅ COMPLIANT |
| Separate immutable reaction/notification keys are forwarded verbatim | ordinal `Key` comparison and explicit outbox call; retry test asserts exact notification key | ✅ COMPLIANT |
| Reaction commit-after-success and retry of both domains | `ReactionFailure_LeavesBothDomainsUncommittedAndRetrySucceeds` | ✅ COMPLIANT for same-position immediate retry |
| Notification commit-after-success and notification-only retry | `NotificationFailure_CommitsReactionAndRetriesOnlyNotificationWithSameKey` | ✅ COMPLIANT for ordinary throw-before-success |
| Original Slice A fault remains observable through work and Stop | both failure tests plus inherited fault matrix | ✅ COMPLIANT |
| Concurrent same decision executes each domain at most once | `SameDecision_IsConsumedOncePerEffectDomainAfterGateRecheck`; 10-process stress | ✅ COMPLIANT |
| Newer decisions and reaction-only/null-notification behavior | `NewerDomainsAndReactionOnlyDecisionsAreNotSuppressed` | ✅ COMPLIANT for covered order |
| Equal position with conflicting key/domain throws deterministically | source contains a per-domain key-conflict throw, but no public runtime test; null-notification then notification at the same position executes silently because notification progress is absent | ❌ UNTESTED / FAILING |
| Older failed decision cannot be overtaken and permanently lost | `GenerationReplacementStartsWithEmptyProgressAndGateCannotBeOvertaken` actually proves the queued newer decision proceeds after the older fault and commits sequence 11; an older sequence-10 retry would then be classified stale and skipped | ❌ FAILING |
| Stale older completed domains skip; legitimate newer domains execute | newer/older branches have runtime hits | ✅ COMPLIANT only after completed progress; contradicted for failed-then-overtaken retry |
| Generation replacement starts with fresh progress | generation replacement portion of the gate test | ✅ COMPLIANT |
| Old-generation admitted ownership preserves Slice A semantics | inherited Stop/Dispose/identity/fault/cancellation matrix | ✅ COMPLIANT |
| Outbox persist-then-throw retries exact key and persists once | no AntiTamper + durable Outbox test or equivalent fault-after-persist seam | ❌ UNTESTED |
| Slice A and Unit 4A/B/4C1A/B1 regressions | focused 160/160, full Service twice, App.UI, coverage twice | ✅ COMPLIANT |
| 4C2/Unit5 scope remains absent | diff and source audit | ✅ COMPLIANT |

**Slice B compliance summary**: 12/15 compliant, 1 functional failure, 2 required scenarios untested/failing.

## Critical Monotonic-Failure Defect

The existing gate serializes work but does not close admission after an effect fault. In `GenerationReplacementStartsWithEmptyProgressAndGateCannotBeOvertaken`:

1. sequence 10 enters the reaction collaborator and fails;
2. sequence 11 is already queued on the same `Generation.Gate`;
3. after sequence 10 releases the gate, sequence 11 executes successfully and commits `ReactionProgress=(scope,11,11,key)`;
4. Stop later observes the original `EffectFault`, but that receipt does not prevent step 3;
5. a retry of sequence 10 now reaches `ShouldExecute`, compares older than committed sequence 11, and returns `false` permanently.

This is the exact forbidden failure edge: queued newer work proceeds after the older fault and makes the older retry stale/lost. The test name and apply-progress claim say the gate “cannot be overtaken,” but its assertions prove the opposite behavior and never retry the older decision.

## Static Correctness

| Requirement | Status | Notes |
|---|---|---|
| Bounded state | ✅ Implemented | exactly two nullable per-generation progress records |
| Commit-after-success | ✅ Implemented | assignments follow awaited collaborator success |
| Gate-time recheck | ✅ Implemented | `ShouldExecute` runs while the existing gate is held |
| Structural ordering | ⚠️ Partial | epoch/sequence and ordinal key are structural; scope mismatch returns executable immediately without ordering comparison |
| Equal-position invariant failure | ⚠️ Partial | different key in an already-progressed domain throws; effect-shape/domain conflicts with absent progress are not represented |
| Monotonic failure edge | ❌ Defective | `EffectFault` records but does not close queued admission; newer success can stale an older failed retry |
| Persist-then-throw convergence | ➖ Unproven | implementation appears to reuse the exact key, but required runtime proof is absent |
| Deferred scope exclusion | ✅ Preserved | no durable/restart/deadline/transport/schema state |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Existing B2 owner and gate | ✅ Yes | no second owner |
| O(1) separate domain progress | ✅ Yes | one nullable token per domain |
| Commit-on-success / rollback-on-failure | ✅ Yes for immediate same-decision retry | later overtaking breaks retry availability |
| Deterministic monotonic ordering | ❌ No | queued newer work proceeds after older effect fault |
| Immutable exact keys | ✅ Yes | no parsing, reconstruction, normalization, or logging added |
| 4C2 remains durable owner | ✅ Yes | no persistence in Slice B |

## Strict-TDD Compliance

An exact detached Slice A worktree at `4a6751b` received the current Slice B test diff plus only the minimal `ExecuteDecisionAsync` generation-flow seam needed for the tests to compile; no dedupe/progress logic was present. The exact current duplicate test failed behaviorally with **8 reactions**. A diagnostic rerun with only the two assertion lines reversed failed with **8 notifications**. This establishes the claimed 8+8 behavioral RED rather than a compile/setup failure.

| Check | Result | Details |
|---|---|---|
| Six-column Slice B evidence exists | ✅ | one four-cycle table is present in `apply-progress.md` |
| Duplicate RED | ✅ Genuine behavioral RED | detached Slice A produced 8 reactions and 8 notifications |
| Reaction-failure retry RED | ❌ Not demonstrated | evidence says only “baseline had no progress seam”; no behavioral failing execution is retained |
| Notification-only retry RED | ❌ Not demonstrated | no detached behavioral failure proving repeated reaction on retry is retained |
| Distinct/newer and generation-reset RED | ❌ Not demonstrated | apply evidence labels the cycle PASS without retained behavioral RED output |
| Equal-position conflict RED/GREEN | ❌ Missing | no test exists; coverage reports zero hits on the conflict throw |
| Current GREEN | ✅ for existing tests | all present focused/stress/full/coverage commands passed |
| Safety net | ✅ | Slice A and broad Unit 4 regressions passed |

The current six-column table is not authoritative for all production-driving behavior: only same-decision dedupe has a reproduced genuine behavioral RED. The reaction, notification, ordering/reset rows describe absent seams or final behavior, not retained failing executions. The older Slice A warning about historical RED fidelity remains applicable and is not contradicted here.

**TDD compliance**: **FAIL** — required production-driving REDs and the conflict scenario are missing, despite current existing tests being green.

## Test Layer Distribution

| Layer | Slice B tests | Files | Tools |
|---|---:|---:|---|
| Unit/component | 5 | 1 | xUnit, Moq, FluentAssertions |
| Integration | 0 new | 0 | broader inherited runtime suites only |
| E2E | 0 | 0 | not configured |
| **Total** | **5** | **1** | |

## Assertion Quality

The five Slice B tests invoke production flow and contain no tautology, ghost loop, orphan empty assertion, type-only proof, or smoke-only assertion. They are heavily coupled to mock invocation counts, but those counts represent the externally required at-most-once/retry behavior.

One assertion set is semantically misleading: `GenerationReplacementStartsWithEmptyProgressAndGateCannotBeOvertaken` asserts that the queued newer operation succeeds after the older fault, then never retries the older decision. It therefore does not prove its name or the required failure edge.

**Assertion quality**: 0 trivial-assertion CRITICAL findings; 1 functional coverage mismatch, classified CRITICAL through the behavioral matrix.

## Quality, Hygiene, Budget, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | exact requested values |
| Staged/index changes | none |
| CODE+TEST paths | exactly 2 authorized files |
| CODE+TEST budget | 144/400 |
| Binary patch hash | exact claimed `6fc94ec475433cae3e30c531e5416378e4e0f895` |
| `git diff --check` | pass; LF→CRLF notices only |
| `.codegraph/` | removed after required structural inspection |
| Generated output in assigned worktree status | none added; ignored restore/build outputs only |
| OpenSpec artifacts/reports | preserved, untracked, excluded from budget |
| Readability | warning: dense one-line production and test statements |
| Deferred-scope drift | none |

## Issues Found

### CRITICAL

1. **Older failed work can be overtaken and permanently lost.** The queued newer decision proceeds after the older reaction fault, commits newer progress, and causes the older retry to be skipped as stale. `EffectFault` makes the original error observable at Stop but does not close admission or preserve retryability.
2. **Equal-position conflict behavior lacks required runtime proof and is incomplete for effect-shape/domain conflict.** No test executes the deterministic throw; Cobertura line 698 has zero hits. A first decision with null notification followed by the same position with a notification executes the new notification because no notification progress exists, rather than rejecting the ambiguous equal-position decision.
3. **The required outbox persist-then-throw scenario is untested.** Existing ordinary notification-fault and Outbox duplicate tests do not prove that a persisted notification followed by a thrown exception retries through durable dedupe and then commits in-memory progress without a duplicate row.
4. **Strict-TDD evidence is incomplete.** Only duplicate dedupe has reproduced genuine behavioral RED. Reaction retry, notification-only retry, newer/reset, and conflict behavior do not have the required retained behavioral RED evidence.

### WARNING

1. `ShouldExecute` treats scope mismatch as immediately executable before epoch/sequence comparison. That may be valid only if every identity/scope replacement necessarily creates a new generation; the method itself does not enforce that precondition.
2. `ExecuteDecisionChainAsync` authoritative-recovery lines remain uncovered, and `ShouldExecute` branch coverage is 75%.
3. Dense one-line statements reduce reviewability. Existing analyzer/package warnings and the duplicate xUnit-ID notice remain inherited.
4. The current config says `strict_tdd: false`, but this verification correctly followed the orchestrator-authoritative strict-TDD override. The artifact mismatch remains process debt.

### SUGGESTION

None. Functional ordering/retry, required runtime scenarios, and Strict-TDD evidence must be corrected before optional cleanup.

## Final Verdict

**FAIL**

The implementation is bounded, uses the correct existing gate, commits ordinary reaction/notification progress only after success, forwards exact keys, and passes all present focused, stress, full Service, App.UI, and two-host coverage commands. Approval is nevertheless blocked by a proven monotonic failure defect: newer queued work succeeds after an older effect fault and permanently stales the older retry. Equal-position conflict and persist-then-throw behavior are also missing required runtime proof, and Strict-TDD evidence is incomplete.

**NOT APPROVED FOR 4C1B2B COMMIT**
