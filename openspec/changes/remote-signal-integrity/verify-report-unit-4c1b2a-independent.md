# Verification Report

**Change**: `remote-signal-integrity` — Unit 4C1B2a Effect Chain Observation only  
**Version**: SDD7  
**Mode**: Strict TDD (orchestrator-authoritative override), hybrid persistence  
**Branch / base**: `feat/sdd7-4c1b2-effect-chain-observation` / `05ba25adcd0b894e351729e173ec31be77f72a61`  
**Verdict**: **FAIL**  
**Commit gate**: **NOT APPROVED FOR 4C1B2A COMMIT**

## Scope and Completeness

This report independently verifies only 4C1B2a. It does not approve 4C1B2b dedupe/retry, 4C2 durability, Unit 5, or archive readiness. The cumulative `9/14` task state is intentional for this child slice; tasks 4.1–4.3 and 5.1–5.2 remain deferred.

| Metric | Result |
|---|---:|
| Cumulative tasks | 9/14 checked |
| In-scope changed CODE+TEST files | 3 |
| CODE+TEST budget | **154/400** (`43+5`, `105+0`, `1+0`) |
| HEAD / merge-base | exact supplied base `05ba25ad...` |
| Fresh patch hash | `5cb0c632f26af10be031bc4b870cad14e138aea0` |
| Scope drift | None found |

The handler is unchanged. No 4C1B2b, 4C2, Unit 5, durable-state, retry, composition, schema, WNS, Realtime, or scheduler file is changed.

## Build and Runtime Evidence

Restores were performed only to populate fresh linked-worktree assets. All retained builds and tests after restore used `--no-restore`; test invocations used `--no-build`. The two full Service/coverage hosts used separate linked worktrees, source trees, `bin`, `obj`, testhost, TRX, result, and Cobertura paths.

| Gate | Result |
|---|---:|
| Service test build, current worktree | PASS, 0 errors |
| Current 4C1B2a focused scenarios | PASS 9/9 |
| Fault/Stop/Dispose/cancellation stress | PASS 5/5 in each of 20 fresh processes |
| `AntiTamperMonitorTests` | PASS 64/64 |
| Integrity/handler/runtime/enforcement excluding AntiTamper | PASS 75/75 |
| Combined Unit 4 | PASS 139/139 |
| Isolated late-failure hosts A/B, sequential rerun | PASS 1/1 and 1/1 |
| Full Service host A | PASS 1,229/1,229 |
| Full Service host B | PASS 1,229/1,229 |
| Full App.UI linked-worktree harness | PASS 192/192 |
| Full Service coverage host A | PASS 1,229/1,229; Cobertura 4,873,217 bytes |
| Full Service coverage host B | PASS 1,229/1,229; Cobertura 4,873,251 bytes |

The existing duplicate xUnit-ID discovery notice remained. No executed test was reported skipped.

### Coverage inspection

Both coverage hosts report `AntiTamperMonitor` at **99.15% line / 85.84% branch**. Host A additionally shows:

- `RunOwnedCheckAsync`: 100% line / 100% branch; gate wait/release paths have hits.
- `DrainGenerationAsync`: 100% line / 95.45% branch; dedicated effect-fault receipt line 290 has 73 hits.
- `ExecuteDecisionChainAsync`: 85.71% line / 85.71% branch; reaction and notification paths have hits, notification call lines 641–646 have 5 hits, and the notification-fault completion path has 4 hits.
- `PerformBinaryIntegrityCheckAsync`: 93.65% line / 73.80% branch; effect-fault publication line 597 has 8 hits.
- Gate-time decision rejection at line 594 has only **50% condition coverage**. Coverage does not exercise a Stop/Dispose race between the pre-handler currentness check and handler state mutation.

## Behavioral Compliance Matrix

| Requirement / scenario | Runtime evidence | Result |
|---|---|---|
| Handler remains unchanged and pure | Handler blob/diff inspection; B1 and full regressions | ✅ COMPLIANT |
| Reuse B2 generation owner and `Generation.Gate`; no second owner | Source inspection; `ConcurrentAdmittedDecisions_AreSerializedByGenerationGate` | ✅ COMPLIANT |
| Distinct concurrent chains cannot overlap collaborator execution | Deterministic blocked-first barrier; second-entry signal remains incomplete until release | ✅ COMPLIANT |
| Stale identity before handler staging yields zero effects | `StaleIdentityBeforeStage_ProducesNoDecisionEffects` | ✅ COMPLIANT for the tested backend/identity window |
| Generation/identity/epoch admission is atomic before handler staging | Handler mutates sequence/epoch/policy at line 591 before the lock/rejection at lines 592–595 | ❌ FAILING |
| Reaction is awaited before optional notification | `PureDecision_ExecutesReactionBeforeItsNotification` | ✅ COMPLIANT |
| Null notification/key means reaction only | `DecisionWithoutNotification_ExecutesReactionOnly` | ✅ COMPLIANT |
| Exact immutable notification key is forwarded verbatim | No API call or test carries the key; `OutboxManager` reconstructs a timestamp key | ❌ FAILING |
| Admitted chain drains after Stop and identity rotation | `AdmittedNotificationChain_DrainsAfterStopAndIdentityRotation` | ✅ COMPLIANT for the combined tested race |
| Reaction fault prevents notification and reaches work/Stop | `ReactionFault_IsObservedByWorkAndStopWithoutNotification`; stress | ✅ COMPLIANT |
| Notification fault follows one reaction and reaches work/Stop | `NotificationFault_IsObservedAfterExactlyOneReaction`; stress | ✅ COMPLIANT in fresh runtime evidence |
| Accepted caller cancellation is deterministically observable while chain drains | Current test expects eventual success; effect collaborators receive `CancellationToken.None`; cancellation is absent from `EffectFault` | ❌ FAILING / UNTESTED contract |
| Dispose drains admitted work | `Dispose_DrainsBlockedAdmittedDecisionChain`; stress | ✅ COMPLIANT |
| Unit 4A/4B/4C1A/4C1B1 regressions remain green | Combined Unit 4, Service twice, App.UI, late-failure hosts | ✅ COMPLIANT |
| Forbidden dedupe/retry/durable state is absent | Complete diff/source search | ✅ COMPLIANT |

**Compliance summary**: **11/14 compliant; 3 blocking failures**.

## Static Correctness

| Contract | Status | Evidence |
|---|---|---|
| One owned chain | Implemented | The existing admitted `RunOwnedCheckAsync` holds `Generation.Gate` through `PerformIntegrityCheckAsync`, reaction, and notification. No second queue/channel/actor/semaphore is added. |
| No fire-and-forget decision effect | Implemented | The new chain awaits store then outbox. The unrelated pre-existing tamper-event fire-and-forget path is unchanged. |
| Admission before staging | **Not implemented atomically** | `HandleVerdictDecision(...)` executes and mutates pure-handler sequence/epoch/policy before `IsCurrentDecisionLocked`. A lifecycle/identity transition in this window can reject effects only after stale state was staged. No decision epoch/sequence is validated by AntiTamper. |
| Complete admitted chain after lifecycle change | Implemented | Once `ExecuteDecisionChainAsync` starts, it uses no post-reaction lifecycle rejection and uses non-cancellable collaborator tokens. |
| Verbatim notification key | **Not implemented** | `decision.NotificationIdempotencyKey` is checked only for null. `IOutboxManager.EnqueueIntegrityNotificationAsync` has no key parameter; `OutboxManager.cs:242` reconstructs `integrity_{type}_{timestampMillis}` instead. |
| Dedicated O(1) fault receipt | Partial | `Generation.EffectFault` is O(1), independent of `Lifecycle`, and records the first non-cancellation chain exception. It does not record accepted `OperationCanceledException` or a caller cancellation accepted after gate acquisition. |
| Fault race closure | Implemented for tested non-cancellation faults | Receipt publication precedes rethrow; drain reads the receipt after owned-task draining. Twenty isolated stress runs remained green. |
| Deferred-scope exclusion | Implemented | No `LastEffect`, reaction/notification sets, dedupe, retry/commit progress, durable state, or deferred files are present in the diff. |

## Design Coherence

| Design decision | Followed? | Notes |
|---|---|---|
| Pure handler and AntiTamper/B2 sole owner | ✅ Yes | Handler unchanged; existing generation and gate reused. |
| Atomic current generation/identity/epoch admission before staging | ❌ No | State-producing handler call precedes the currentness lock. |
| Paired reaction-before-notification chain | ✅ Yes | Structurally awaited and runtime-covered. |
| Exact immutable key forwarding | ❌ No | Key is discarded after a null check and reconstructed downstream. |
| Independent O(1) fault/cancellation observation | ⚠️ Partial | Non-cancellation collaborator faults are retained; accepted cancellation is not. |
| 4C1B2a contains no dedupe/retry/durability | ✅ Yes | Forbidden state is absent. |

## Strict-TDD Compliance

An exact detached `05ba25a` worktree received the current AntiTamper test diff only, then restored/built once and ran with `--no-restore --no-build`.

| Check | Result | Details |
|---|---|---|
| Current tests exist | ✅ | Nine new public-flow tests in `AntiTamperMonitorTests.cs`; one runtime-path assertion. |
| Clean-base missing notification/order RED | ✅ Genuine behavioral RED | Expected `reaction, reaction, notification`; actual `reaction, reaction`. |
| Clean-base current fault/chain REDs | ✅ | Reaction fault, notification fault, and admitted notification-chain tests failed behaviorally; clean-base run was 5 passed / 4 failed. |
| Inherited `EffectOwner_DrainsAdmittedReactionAfterStop` classification | ✅ Historical correction retained | It was GREEN on exact B1 and is safety evidence only; it is absent from the reconstructed current test set and is not claimed as RED. |
| Current GREEN | ✅ Runtime only | All focused/current/stress/full/coverage test commands passed. |
| Six-column cumulative evidence accuracy | ❌ | `apply-progress.md:388–392` still records blocked Stop/drain, blocked gate GREEN, and no serialization closure, while the later final section claims completion without replacing the six-column table. |
| Stop/drain hang classification | ⚠️ Unresolved historical evidence | The current test starts Stop, asserts it is incomplete, then releases the collaborator; it does **not** await Stop before release. Current and stress runs pass. The exact earlier 129-line test/patch was not retained, so the historical hang cannot be accepted as a product RED or dismissed as invalid choreography. |
| Production correction discipline | ⚠️ Partial | Four current scenarios are genuine clean-base behavioral REDs. The stale, gate, caller-cancellation, Dispose, and reaction-only tests were baseline GREEN/safety tests and must not be represented as production REDs. |

**TDD compliance**: **FAIL** because the authoritative six-column evidence is internally stale/inaccurate and required key/admission/cancellation behavior remains unproven or contradicted.

## Test Layers and Assertion Quality

| Layer | Tests | Files | Tool |
|---|---:|---:|---|
| Unit/component | 9 new scenarios | 1 | xUnit, Moq, FluentAssertions |
| Runtime integration | 1 modified scenario | 1 | xUnit with real monitor/handler/enforcement path |
| E2E | 0 | 0 | Not configured for this slice |

The new tests call public/internal production flow and use deterministic TCS barriers. They add no sleep, private reflection, ghost loop, or tautology. `WaitAsync` is used only as a bounded failure guard. However, the notification tests cannot assert verbatim key forwarding because the invoked outbox API does not accept the key, and the accepted-cancellation test explicitly accepts successful completion rather than proving cancellation observation.

**Assertion quality**: no trivial assertions; two semantic contract gaps are blocking.

## Quality, Hygiene, Budget, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | ✅ exact requested values |
| Staged/index changes | ✅ none |
| Changed tracked CODE+TEST paths | ✅ exactly three authorized files |
| CODE+TEST budget | ✅ 154/400 |
| `git diff --check` | ✅ exit 0; LF→CRLF notices only |
| Handler content | ✅ unchanged from base |
| Current blobs | ✅ `36c6223...`, `b709330...`, `2c5b4a7...`, matching apply-progress |
| `.codegraph/` | ✅ removed after structural inspection |
| Generated output in assigned worktree | ✅ none added to status |
| OpenSpec reports | ✅ untracked and excluded from commit/budget |
| Readability | ⚠️ compressed production/test one-liners reduce reviewability despite the small diff |

## Issues Found

### CRITICAL

1. **The immutable notification idempotency key is discarded.** `AntiTamperMonitor.cs:639–646` uses it only as a null guard. The called outbox API has no key parameter, and `OutboxManager.cs:242` reconstructs a timestamp-based key. The required exact verbatim forwarding contract and public test do not exist.
2. **Decision admission is not atomic before staging.** `HandleVerdictDecision` mutates handler sequence, epoch, and policy state before the generation/identity lock check. Stop, Dispose, or identity rotation in that window can stage a stale decision and suppress only its external effects. No AntiTamper epoch/sequence check closes the race.
3. **Accepted cancellation is not retained by the dedicated fault state.** `EffectFault` catches only non-`OperationCanceledException`; admitted effect collaborators receive `CancellationToken.None`; drain ignores owned cancellation. The current caller-cancellation test proves drain-to-success, not deterministic cancellation observation.
4. **Required public runtime proof is incomplete.** No test can prove exact notification-key forwarding, no barrier targets the pre-handler-staging currentness race, and no test proves accepted cancellation observation.
5. **Strict-TDD cumulative evidence is not accurate.** The active six-column 4C1B2a table remains BLOCKED/NOT CLOSED while later prose claims final GREEN. The historical hang lacks the exact retained patch needed to classify it, and baseline-GREEN safety tests must not be reused as RED.

### WARNING

1. `ExecuteDecisionChainAsync` has 85.71% line/branch coverage and omits authoritative-recovery execution; the broader class remains high coverage, but coverage cannot prove the missing contracts.
2. The assigned additions are highly compressed into long one-line tests and production statements, increasing review cost.
3. Existing analyzer/package warnings and the duplicate xUnit-ID notice remain outside this slice.

### SUGGESTION

None. Functional, race, cancellation, and TDD-evidence blockers must be resolved before optional improvements.

## Final Verdict

**FAIL**

Fresh focused, stress, full Service, App.UI, late-failure, build, and two-host coverage evidence is green, and the gate serialization/ordered-chain runtime behavior is materially improved. Approval is nevertheless blocked: the required immutable notification key is not forwarded, decision state can be staged before atomic lifecycle/identity admission, accepted cancellation is not independently retained/observed, required public tests are missing, and the cumulative strict-TDD evidence is internally inaccurate.

**NOT APPROVED FOR 4C1B2A COMMIT**
