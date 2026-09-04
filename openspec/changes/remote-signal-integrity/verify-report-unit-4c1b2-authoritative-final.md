# Verification Report

**Change**: `remote-signal-integrity` — Unit4C1B2 AntiTamper Effect Ownership only  
**Version**: SDD7  
**Mode**: Strict TDD (authoritative override)  
**Branch / base**: `feat/sdd7-4c1b2-effect-safety` / `05ba25adcd0b894e351729e173ec31be77f72a61`  
**Verdict**: **FAIL**  
**Commit gate**: **NOT APPROVED FOR 4C1B2 COMMIT**

## Scope

This report re-verifies only the remediated Unit4C1B2 effect-owner slice. It does not approve or assess 4C2, Unit5, transports, schema, retry, or `Program` work. The previous `verify-report-unit-4c1b2-independent.md` is preserved unchanged.

| Metric | Result |
|---|---:|
| Cumulative tasks | 9/14 checked |
| Tasks incomplete | 5/14; aggregate 4C and Unit5 gates remain open |
| Unit4C1B2 execution-plan step | Implemented, but not independently approved |
| Changed tracked CODE+TEST files | 3 |
| CODE+TEST budget | 364/400 (`98+9`, `251+0`, `5+1`) |
| HEAD / supplied base | `05ba25adcd0b894e351729e173ec31be77f72a61` / same |
| Fresh stable patch-id | `fffb986f2b4d1ab71c9c69719f100bb653e288e7` |
| Scope drift | None found |

The five unchecked aggregate tasks block archive readiness. They are not represented as completed by this child-slice report.

## Build and Runtime Evidence

All authoritative test evidence was produced in physically isolated linked worktrees. No source, test, design, task, or apply-progress fix was made during verification.

| Gate | Result | Evidence |
|---|---|---|
| Service test build, host A | PASS | Build completed with 0 errors |
| Service test build, host B | PASS | Independent isolated build completed with 0 errors |
| Focused `EffectOwner_*` | PASS 8/8 | Current remediation tests all passed in the focused run |
| AntiTamper suite | PASS 63/63 | Current isolated host |
| Integrity/Enforcement excluding AntiTamper | PASS 75/75 | Current isolated host |
| Combined Unit4 | PASS 138/138 | Current isolated host |
| Inherited late-failure gate | PASS 1/1 | `LateNonCancellableFailure_IsObservableAndHasNoEffects` |
| Full Service, host A | PASS 1,228/1,228 | Current isolated host |
| Full Service, host B | PASS 1,228/1,228 | Second isolated non-coverage run |
| Full App.UI | PASS 192/192 | Physically isolated linked-worktree output |
| Coverage host A | PASS 1,228/1,228 | Non-empty Cobertura, 4,881,524 bytes |
| Coverage host B | **FAIL 1; PASS 1,227** | Non-empty Cobertura, 4,881,471 bytes |

### Blocking Runtime Failure

Coverage host B exited non-zero:

```text
AntiTamperMonitorTests.EffectOwner_NotificationFaultIsObservedAfterExactlyOneReaction
Assert.Throws() Failure: No exception was thrown
Expected: typeof(System.InvalidOperationException)
Result: Failed 1, Passed 1,227
```

The same test passed in the focused run and coverage host A. That makes the behavior nondeterministic, not green. The scenario requires every collaborator fault to be observed; one real execution where the expected notification fault was not surfaced is sufficient to fail the gate.

## Strict-TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | PASS | Six-column Unit4C1B2 evidence exists in `apply-progress.md` |
| Test files exist | PASS | Eight `EffectOwner_*` tests plus the modified public runtime scenario exist |
| Original notification/order RED | PASS | Exact B1 replay failed behaviorally: two enforcement effects, no required notification |
| Original admitted-stop RED | FAIL | Exact B1 replay passed `EffectOwner_DrainsAdmittedReactionAfterStop`; this is inherited triangulation, not RED |
| Remediation reconstruction | PARTIAL | Claimed rejected object `00b13628...` was unavailable; reconstruction hash `d91a8961...` is approximate and is not accepted as exact historical proof |
| Current GREEN | **FAIL** | One authoritative coverage execution failed the notification-fault scenario |
| Triangulation | PASS | Stale admission, notification-bearing stop, reaction fault, notification fault, cancellation, and concurrent consumption scenarios now exist |
| Safety net | PASS | Unit4, full Service twice, App.UI, inherited late-failure, and two coverage hosts were executed |

**TDD compliance**: **FAIL**. One claimed original RED was actually green, exact rejected-patch proof is unavailable, and current runtime GREEN is disproven by host B.

### Historical RED Replay

- Exact B1 base: `EffectOwner_UsesDecisionNotificationKeyAfterEnforcement` failed for the intended missing notification behavior.
- Exact B1 base: `EffectOwner_DrainsAdmittedReactionAfterStop` passed 1/1 and must not be claimed as RED.
- Approximate rejected-patch reconstruction: `EffectOwner_NotificationBearingAdmissionCompletesAfterStop` failed because no outbox enqueue occurred; `EffectOwner_NotificationFaultIsObservedAfterExactlyOneReaction` passed. This evidence is useful triangulation only and is explicitly not an exact reconstruction of patch `00b13628...`.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit/component | 8 focused scenarios | 1 | xUnit, Moq, FluentAssertions |
| Integration/runtime path | 1 modified scenario | 1 | xUnit with real AntiTamper/handler flow |
| E2E | 0 | 0 | Not configured for this slice |
| **Total** | **9 changed scenarios** | **2** | |

## Changed-File Coverage

| File | Line | Branch | Uncovered lines | Rating |
|---|---:|---:|---|---|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 99.15% | 87.75% | 470-471 in the primary class record | Excellent line / acceptable branch |
| Changed test files | N/A | N/A | Product collector excludes tests | Not instrumented |

Both coverage reports had the same class rates. The remediated decision-owner admission and effect body had runtime hits, including stale rejection, reaction, notification, and drain paths. Coverage does not override the failed behavioral execution.

## Assertion Quality

**Assertion quality**: PASS — the changed tests call production code and assert observable enforcement, notification ordering/key use, lifecycle completion, fault identity, cancellation, and duplicate counts. No tautology, ghost loop, type-only-only, or production-free assertion was found.

## Quality Metrics

**Build/type check**: PASS — both isolated Service test builds completed with 0 errors.  
**Linter**: Not separately configured for the changed C# files.  
**Diff whitespace check**: PASS — `git diff --check` exited zero.

## Behavioral Compliance Matrix

| Requirement / scenario | Covering runtime evidence | Result |
|---|---|---|
| Handler remains pure; AntiTamper is sole asynchronous effect owner | B1 regressions, combined Unit4, source inspection | COMPLIANT |
| Atomic generation/identity/epoch/sequence admission | `EffectOwner_StaleBeforeStageProducesNoDecisionEffects` | COMPLIANT for tested identity rotation |
| Stale-before-stage produces no decision effects | Same focused test | COMPLIANT |
| Enforcement completes before its paired notification | `EffectOwner_UsesDecisionNotificationKeyAfterEnforcement` | COMPLIANT |
| Admitted notification-bearing work drains after lifecycle/identity change | `EffectOwner_NotificationBearingAdmissionCompletesAfterStop` | COMPLIANT in current focused and full passing hosts |
| Reaction fault is observed by caller and lifecycle owner | `EffectOwner_ReactionFaultIsObservedByWorkAndStopWithoutNotification` | COMPLIANT |
| Notification fault is observed after exactly one reaction | `EffectOwner_NotificationFaultIsObservedAfterExactlyOneReaction` | **FAILING** — one authoritative host observed no expected exception |
| Owner cancellation drains without duplicate effects | `EffectOwner_AcceptedOwnerCancellationDrainsBlockedDecisionWithoutDuplicateEffects` | COMPLIANT |
| Concurrent accepted decisions execute once and preserve notification count | `EffectOwner_ConcurrentPublicTriggersConsumeEachDecisionOnce` | COMPLIANT for that run |
| Immutable notification key is consumed verbatim | Focused owner test plus `IntegrityRuntimePathTests` | COMPLIANT |
| Idempotency bookkeeping remains bounded for a long-lived generation | No covering test; source uses generation-lifetime sets with no removal/bound | **FAILING** |
| Failed effect can be retried without permanent in-memory suppression | No covering test; reservation occurs before effect and is retained after fault | **FAILING** |
| Unit4A/4B/4C1A behavior remains intact | Combined Unit4 and full regressions | COMPLIANT |
| No 4C2/Unit5/transport/schema/retry drift | Diff inspection | COMPLIANT |

**Compliance summary**: 10/13 listed invariants compliant; 3 failing.

## Static Correctness

| Requirement | Status | Evidence |
|---|---|---|
| No second mailbox/actor/fire-and-forget owner | Implemented | Existing AntiTamper admission and owned-task machinery is reused |
| No collaborator work under `lockObject` | Implemented | The lock admits a gated task; store/outbox work runs after the gate |
| Complete admitted reaction/notification chain | Implemented structurally | Reaction and notification are in one admitted async delegate with no post-reaction lifecycle rejection |
| Fault/cancellation observation | **Not reliable** | Structure intends propagation, but runtime host B disproved deterministic notification-fault observation |
| Bounded idempotency state | **Not implemented** | `Generation.ReactionKeys` and `NotificationKeys` are unbounded `HashSet<string>` values cleared only when the generation ends |
| Failure-safe reservation | **Not implemented** | Keys and `LastEffect` are committed before collaborator work; no rollback/removal occurs on reaction or outbox failure |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Pure handler, AntiTamper/B2 sole effect owner | YES | No handler ownership was reintroduced |
| Atomic admission with no check-call race | YES | Validation, key reservation, token assignment, and task admission occur under one lock |
| Accepted work awaited and drained | PARTIAL | Normal, stop, reaction-fault, and cancellation tests pass; notification-fault propagation is nondeterministic |
| Exact immutable key use | YES for notification | Notification key is passed verbatim; reaction key is used only for in-memory reservation |
| Stable retry/idempotency semantics | NO | Failed reservations remain permanently consumed for the generation; no bounded successful-key strategy exists |
| Slice budget `<=400` CODE+TEST | YES | 364/400 |

## Issues Found

### CRITICAL

1. **Notification-fault observation is nondeterministic.** Coverage host B failed `EffectOwner_NotificationFaultIsObservedAfterExactlyOneReaction` because the expected `InvalidOperationException` was not thrown. The specification requires every fault/cancellation to be observed. A non-zero authoritative test command is independently blocking.
2. **Idempotency state is unbounded for a long-lived generation.** `ReactionKeys` and `NotificationKeys` retain every accepted key until generation teardown. A healthy generation may be long-lived, so accepted observations cause monotonic memory growth with no cap, eviction, or durable bounded owner.
3. **Effect failures permanently consume reservations.** `LastEffect` and both key sets are updated before enforcement/outbox execution. If either collaborator fails, no reservation is removed. A retry carrying the stable key is rejected for the rest of the generation even though the effect chain did not complete.
4. **Strict-TDD proof remains incomplete.** The original admitted-stop test was green on exact B1, and the rejected remediation patch could only be approximated. More importantly, current GREEN is disproven by the failed coverage host.
5. **The cumulative change remains incomplete.** Tasks 4.1-4.3 and 5.1-5.2 are unchecked. This child report cannot mark them complete or make the overall SDD change archive-ready.

### WARNING

1. Branch coverage for the changed production class is 87.75%; this is acceptable but leaves some lifecycle/admission outcomes uncovered.
2. The remediation diff is 364/400, leaving only 36 CODE+TEST lines before the hard slice limit.
3. The exact historical rejected patch `00b13628...` was unavailable, so claims tied to that artifact cannot be independently reproduced exactly.

### SUGGESTION

None. Correctness and runtime determinism must be resolved before optional improvements.

## Hygiene

- `git diff --check`: PASS.
- HEAD equals the supplied base; no commit was created.
- No staged files.
- Tracked edits remain limited to the three claimed CODE+TEST files.
- `.codegraph/` generated during verification was removed.
- OpenSpec artifacts remain untracked and excluded from the CODE+TEST budget.
- Supplied remediation fingerprint `e59dc22b6bf87b07a4c2385f68cec530a9a44ab8` was retained from the prior audit; the fresh stable Git patch-id is reported separately above.

## Final Verdict

**FAIL**

The remediation closes the previously reported admitted-notification gap and adds strong public-flow triangulation, but it is not safe to approve: one isolated runtime host failed the required notification-fault observation, generation-lifetime idempotency sets are unbounded, and failed effect reservations cannot be retried within the generation.

**NOT APPROVED FOR 4C1B2 COMMIT**
