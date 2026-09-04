# Verification Report

**Change**: `remote-signal-integrity` — Unit 4C1B2a Effect Chain Observation only  
**Version**: SDD7  
**Mode**: Strict TDD, hybrid persistence  
**Branch / base**: `feat/sdd7-4c1b2-effect-chain-observation` / `05ba25adcd0b894e351729e173ec31be77f72a61`  
**Verdict**: **FAIL**  
**Commit gate**: **NOT APPROVED FOR 4C1B2A COMMIT**

## Scope and Completeness

This verification is limited to remediated 4C1B2a. It does not approve 4C1B2b dedupe/retry, 4C2 durability, Unit 5, or archive readiness. The cumulative task state remains intentionally 9/14 for this child slice.

| Metric | Result |
|---|---:|
| Cumulative tasks | 9/14 checked |
| In-scope changed CODE+TEST files | 6 |
| CODE+TEST budget | **329/400** (`316 additions + 13 deletions`) |
| HEAD / merge-base | exact supplied base `05ba25ad...` |
| Stable patch-id | `ec63a1c2626fc7499a247487daab737a6a1e5c54` |
| Index | empty |
| Deferred-scope drift | none found |

Changed files are exactly the three production/API files and three Service test files declared by the revised design. `IntegrityVerdictHandler.cs` remains unchanged from the approved base. No 4C1B2b progress state, 4C2 durability, Unit 5, composition, schema, WNS, Realtime, or scheduler file is changed.

## Prior CRITICAL Remediation Map

| Prior CRITICAL | Implementation evidence | Fresh runtime evidence | Result |
|---|---|---|---|
| Stale decision staged before generation/identity admission | `AntiTamperMonitor.cs:604-609` validates current generation, admission, and identity while holding `lockObject`, then invokes the pure synchronous handler under `lockObject -> stateGate`; collaborators run after release | `StaleAdmissionDoesNotStageHandlerStateBeforeNextAcceptedDecision`; focused matrix and 20-process stress | ✅ Closed for the tested admission race |
| Immutable notification key discarded | Explicit `IOutboxManager` overload; AntiTamper forwards `NotificationIdempotencyKey`; OutboxManager passes it unchanged to `EnqueueAsync` | legacy and explicit-key Outbox tests plus runtime-path exact key; coverage hits both overloads | ✅ Closed for valid non-empty keys |
| Accepted caller cancellation not independently observable | caller completion has a per-admission cancellation registration; owned work uses non-cancellable collaborator tokens after work starts | both accepted-cancellation tests, focused matrix, and 20-process stress | ✅ Closed for cancellation after collaborator entry |
| Missing public proof for the three blockers | public/internal production-flow tests now exist | focused 24/24 and stress 80/80 | ✅ Closed for those exact test scenarios |
| Inaccurate cumulative Strict-TDD evidence | remediation prose was appended | artifact audit only | ❌ Not closed; see Strict-TDD section |

## Build and Runtime Evidence

All retained current-worktree commands after the available restore state used `--no-restore`; tests used `--no-build`. Coverage hosts were physically separate detached worktrees with independent source, `bin`, `obj`, testhost, and result paths.

| Gate | Fresh result |
|---|---:|
| Service test build | PASS, 0 errors; existing warning corpus |
| Admission/key/cancel/order/fault/Stop/Dispose/gate + direct outbox/runtime focus | PASS 24/24 |
| Critical race stress | PASS 4/4 in each of 20 fresh processes, 80/80 total |
| `AntiTamperMonitorTests` | PASS 66/66 |
| `OutboxManagerTests` | PASS 9/9 |
| Integrity B1/runtime/checker | PASS 51/51 |
| Enforcement | PASS 24/24 |
| Combined Unit 4 | PASS 141/141 |
| Late-failure isolated hosts | PASS 1/1 twice |
| Full Service attempt 1 | PASS 1,232/1,232 |
| Full Service attempt 2 | **FAIL 1; PASS 1,231/1,232** — inherited `LateNonCancellableFailure_IsObservableAndHasNoEffects` observed no exception |
| Post-failure exact isolated rerun | PASS 1/1 |
| Full Service reruns 3 and 4 | PASS 1,232/1,232 twice |
| Full App.UI | PASS 192/192 |
| Isolated full-Service coverage host A | PASS 1,232/1,232; Cobertura 4,877,663 bytes |
| Isolated full-Service coverage host B | PASS 1,232/1,232; Cobertura 4,877,640 bytes |

The duplicate xUnit-ID discovery notice remains. No executed test result was reported skipped. The fresh non-zero full-Service command is CRITICAL under the verification gate even though the exact test and two later full runs passed.

### Coverage

| Changed production file | Line | Branch | Relevant hits |
|---|---:|---:|---|
| `AntiTamperMonitor.cs` | 99.17% | 85.18% | admission check 59; cancellation completion 318; gate cancellation both branches; owner drain/effect fault; notification call 5 |
| `OutboxManager.cs` | 100% | 81.81% | legacy overload 1; explicit overload 2; exact key pass-through lines 255-259 hit 2 |
| `IOutboxManager.cs` | N/A | N/A | interface-only change; build/API evidence |

`IntegrityVerdictHandler` remains unchanged and reports 100% line / 97.36% branch. `ExecuteDecisionChainAsync` reports 86.36% line / 85.71% branch; authoritative recovery is not hit in that method. `IsCurrentDecisionLocked` reports 100% line / 62.5% branch. No aggregate threshold is configured.

## Behavioral Compliance Matrix

| Invariant / scenario | Covering test or evidence | Result |
|---|---|---|
| Handler remains pure/effect-free; AntiTamper/B2 is sole async owner | unchanged handler; full diff; Unit 4 regression | ✅ COMPLIANT |
| No second queue/mailbox/channel/actor/drainer/semaphore or fire-and-forget effect owner | complete changed-source inspection | ✅ COMPLIANT |
| Atomic generation/identity admission before handler mutation | stale no-mutation test; lock-order inspection | ✅ COMPLIANT for tested race |
| No collaborator call under lifecycle lock | pure handler is the intentional exception; `identityCoordinator.CurrentState` is also read under `lockObject` | ⚠️ PARTIAL — production getter is lock-free, but the interface boundary is still invoked under lock |
| Reaction then optional notification under existing `Generation.Gate` | order, drain, fault, Dispose, serialization tests | ✅ COMPLIANT |
| Reaction failure prevents notification; notification failure follows one reaction | reaction/notification fault tests | ✅ COMPLIANT |
| Stop/Dispose/identity change cannot split accepted chain | admitted-chain and Dispose tests | ✅ COMPLIANT |
| Legacy outbox behavior unchanged | direct legacy test asserts exact legacy key | ✅ COMPLIANT |
| Explicit key forwarded and persisted verbatim | runtime path + direct explicit-key test | ✅ COMPLIANT for valid key |
| Existing production callers and Moq call sites compile | full build and full Service suite discovery/execution | ✅ COMPLIANT |
| Overload has no source ambiguity and defines null/empty behavior | static API audit; no null/empty tests or guards | ❌ FAILING / UNTESTED |
| Cancellation before API admission produces no work | existing pre-cancel path and suite | ✅ COMPLIANT |
| Cancellation while waiting `Generation.Gate` produces no effect | no covering test; caller token is removed before `RunOwnedCheckAsync` waits the generation gate | ❌ FAILING / UNTESTED |
| Cancellation after accepted collaborator entry cancels caller while owned chain drains | two accepted-cancellation tests | ✅ COMPLIANT |
| Collaborator fault after caller cancellation has deterministic precedence/EffectFault observation | separate cancellation and fault tests only | ❌ UNTESTED |
| EffectFault is O(1) and lifecycle-independent | one generation `TaskCompletionSource<Exception>`; drain inspection | ✅ IMPLEMENTED |
| Forbidden 4C1B2b/4C2 state absent | full diff/search | ✅ COMPLIANT |
| Unit 4A/4B/4C1A/4C1B1 behavior preserved | focused Unit 4, Service, App.UI | ⚠️ Runtime gate unstable due one fresh full-suite failure |

### Cancellation ownership defect

`AdmitCheckLocked` calls `RunOwnedCheckAsync(current, cancelWithOwner)` without the caller token (`AntiTamperMonitor.cs:233-234`). `RunAfterGateAsync` checks the caller token once before invoking that work (`:274-279`), but `RunOwnedCheckAsync` then waits on `Generation.Gate` using no caller token (`:290-296`). Therefore cancellation after the admission gate check but while queued on `Generation.Gate` cancels the caller-facing completion yet does not cancel the owned waiter; once the gate opens, the canceled operation can execute the integrity decision and effects.

`QueuedCallerCancellation_DoesNotReleaseUnacquiredGate` does not close this scenario: after releasing the first operation it immediately calls `StopAsync`, so lifecycle closure can reject the canceled waiter before collaborators execute. There is no test that cancels a queued caller, releases the generation gate while the generation remains active, and proves zero decision effects.

## API Compatibility Audit

- Current repository callers compile, AntiTamper alone calls the explicit overload, and the legacy overload reconstructs the exact historical `integrity_{type}_{timestampMillis}` key.
- The explicit path does not normalize, reconstruct, or log a valid supplied key; it persists the value unchanged.
- The overload pair is source-ambiguous for a five-argument caller using the C# `default` literal because the fifth parameter can bind to either `CancellationToken` or `string`. This contradicts unconditional source-compatibility.
- No explicit `null`/empty key guard exists in the interface implementation or `EnqueueAsync`, and no test defines the required result. A null reaches entity persistence; an empty value is passed through. The API boundary is therefore not authoritatively closed.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Pure handler; AntiTamper sole owner | ✅ Yes | no handler or second-owner change |
| `lockObject -> stateGate` admission | ✅ Yes | no reverse handler path found |
| Existing generation gate owns the complete chain | ✅ Yes | reaction and notification remain under the admitted check's gate |
| Caller cancellation state machine | ❌ No | cancellation while waiting the generation gate can leave executable owned work |
| Compatible explicit-key overload | ❌ Partial | current callers/valid keys work; `default` ambiguity and null/empty contract remain |
| Independent O(1) EffectFault | ✅ Yes | first non-cancellation effect fault retained |
| No A dedupe/retry/durability | ✅ Yes | no `LastEffect`, key sets/cache, retry/progress, ReactionKey consumption, or durable changes |

## Strict-TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ⚠️ Partial | remediation table exists, but it is five columns, merging safety/triangulation and refactor/evidence rather than the required six-column cycle |
| Atomic no-mutation behavioral RED | ✅ | exact prior candidate produced the skipped-state behavioral failure; current public GREEN passes |
| Exact-key behavioral RED | ❌ Not replayable authoritatively | narrative records observing a legacy reconstructed key, but current explicit-key tests require the post-GREEN API; an API compile/setup failure is not a behavioral RED |
| Post-gate caller-cancellation behavioral RED | ✅ | prior candidate completed successfully instead of canceling; current post-entry GREEN passes |
| Original notification/order RED | ✅ | retained and correctly classified genuine |
| Inherited admitted-Stop/baseline tests | ✅ | classified safety-only, not RED |
| Historical 129-line hang | ✅ | retained as unknown and not used |
| Current GREEN stability | ❌ | one fresh full-Service process failed, despite later green reruns |
| Cumulative status consistency | ❌ | historical six-column `NOT STARTED` status remains in the same cumulative artifact while final state is declared ready; the current remediation table does not replace it with the required six columns |

**TDD compliance**: **FAIL**. The accurate atomic and post-entry cancellation cycles are present, but exact-key behavioral RED replay, six-column cumulative evidence, and stable full-suite GREEN are not closed.

## Test Layers and Assertion Quality

| Layer | Changed tests | Files | Tool |
|---|---:|---:|---|
| Unit/component | 11 AntiTamper scenarios + 2 Outbox scenarios | 2 | xUnit, Moq, FluentAssertions |
| Runtime integration | 1 modified canonical runtime scenario | 1 | xUnit with real monitor/handler/enforcement path |
| E2E | 0 | 0 | not configured |

The changed tests invoke production flows and use deterministic TCS barriers. No tautology, ghost loop, empty orphan assertion, smoke-only assertion, or assertion without production execution was found. Several dense one-line tests and invocation-count assertions increase implementation coupling and review cost.

**Assertion quality**: 0 CRITICAL trivial assertions; readability/implementation-coupling warning only.

## Quality, Hygiene, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | exact requested values |
| Staged/index changes | none |
| CODE+TEST paths and budget | exactly 6 paths; 329/400 |
| `git diff --check` | pass; only LF-to-CRLF notices |
| `.codegraph/` | absent |
| Generated output in assigned worktree status | none |
| Temporary coverage harness worktrees | removed after execution |
| OpenSpec artifacts | untracked and excluded from budget |
| Prior independent FAIL report | unchanged; SHA-256 `e68252d0b564435929e9a51d38f7825da04404799d50f6ea48ab48c33259f39f` |
| Analyzer/package quality | 0 build errors; existing large warning corpus |
| Readability | warning: dense one-line production/test statements |

## Issues Found

### CRITICAL

1. **Caller cancellation while waiting `Generation.Gate` can still execute the canceled operation.** The caller token is checked before `RunOwnedCheckAsync`, then discarded before the actual generation-gate wait. The existing queued-cancellation test relies on Stop to close admission and does not cover release while active.
2. **The required cancellation/fault precedence scenario is untested.** No runtime test combines accepted caller cancellation with a later reaction/notification fault and proves deterministic EffectFault/Stop observation without duplicate cancellation or orphaning.
3. **The explicit-key API compatibility gate is incomplete.** The overload pair is ambiguous for a legacy five-argument `default` literal, and null/empty key behavior has neither validation nor tests.
4. **Strict-TDD evidence is not authoritative for every required RED.** Exact-key evidence cannot be replayed as a behavioral RED with the retained tests, and the cumulative artifact lacks a current six-column replacement table.
5. **A fresh required test command exited non-zero.** The second full-Service process failed `LateNonCancellableFailure_IsObservableAndHasNoEffects` (1 failed, 1,231 passed). Later exact and full reruns passed, but the strict runtime stability gate is not clean.

### WARNING

1. `identityCoordinator.CurrentState` is read through a collaborator interface while `lockObject` is held. The production getter is currently lock-free, but this weakens the stated no-collaborator-under-lock invariant.
2. `ExecuteDecisionChainAsync` has 86.36% line / 85.71% branch coverage; its authoritative-recovery branch is not hit in this owner method.
3. Dense one-line statements reduce reviewability. Existing analyzer/package warnings and the duplicate xUnit-ID notice remain.

### SUGGESTION

None. Functional cancellation, API, TDD, and runtime-stability gates must close before optional cleanup.

## Final Verdict

**FAIL**

The three prior behavioral blockers are materially remediated for their retained scenarios, the 329/400 scope is clean, focused/stress/Unit4/App.UI/two-host coverage evidence is strong, and valid explicit keys are forwarded verbatim. Approval is nevertheless blocked by a real waiting-generation-gate cancellation hole, missing cancellation-plus-fault runtime proof, incomplete overload/null/empty compatibility, non-authoritative Strict-TDD evidence, and one fresh full-Service failure.

**NOT APPROVED FOR 4C1B2A COMMIT**
