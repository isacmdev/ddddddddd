# Verification Report

**Change**: `remote-signal-integrity` — SDD7 Unit 4C1B2b Effect Dedupe/Retry only  
**Version**: SDD7  
**Mode**: Strict TDD, hybrid persistence, authoritative read-only re-verification  
**Branch / base**: `feat/sdd7-4c1b2-effect-dedupe-retry` / `4a6751bed7efc8d849260355e4c33fc8e2814b8c`  
**Candidate diff hash**: `9c21c3974ce5cc6e9374104f6ce1264763512959`  
**Verdict**: **FAIL**  
**Commit gate**: **NOT APPROVED FOR 4C1B2B COMMIT**

## Scope and Completeness

This report verifies only the remediated Slice B child. The exact approved Slice A commit is the parent. Unit 4C2 durability/restart/deadline/clock/crash work, Unit 5, transport/schema work, complete Unit 4 approval, and archive readiness remain excluded. The prior `verify-report-unit-4c1b2b-independent.md` FAIL is preserved unchanged.

| Metric | Result |
|---|---:|
| Cumulative tasks | 9/14 checked; five aggregate/deferred tasks intentionally remain unchecked |
| In-scope CODE+TEST files | 2 |
| CODE+TEST budget | **311/400** (`114+12` production, `183+2` tests) |
| HEAD / merge-base | exact base `4a6751bed7efc8d849260355e4c33fc8e2814b8c` |
| Index | empty |
| Deferred-scope drift | none |

The unchecked tasks are aggregate Unit 4/Unit 5 tasks rather than omitted Slice B implementation tasks. They do not by themselves determine this slice verdict.

## Build and Runtime Evidence

| Gate | Fresh authoritative result |
|---|---|
| Five sequential builds: Domain, Service, Service.Tests, App.UI, App.UI.Tests | ✅ PASS; 0 errors, inherited warning corpus |
| Full `AntiTamperMonitorTests` | ✅ PASS 76/76 |
| AntiTamper + Outbox + Integrity + Enforcement combined focus | ✅ PASS 163/163 |
| Barrier/persist/concurrent-duplicate stress | ✅ PASS 30 fresh processes × 5 tests = 150/150 |
| Inherited late-failure isolated stress | ✅ PASS 20/20 fresh processes |
| Full Service run 1 | ❌ FAIL 1,244/1,245: `LateNonCancellableFailure_IsObservableAndHasNoEffects` reported “No exception was thrown” at line 418 |
| Full Service run 2 | ✅ PASS 1,245/1,245 |
| Full App.UI | ✅ PASS 192/192 |
| Physically isolated coverage host A (`ca`) | ✅ PASS 1,245/1,245; non-empty Cobertura |
| Physically isolated coverage host B (`cb`) | ❌ FAIL 1,244/1,245 on the same late-failure assertion; non-empty Cobertura |

The duplicate xUnit-ID discovery notice remains inherited. No executed test was reported skipped. The two non-zero full-suite commands are retained and are blocking under the verification gate; isolated success does not erase them.

### Coverage

- Passing host A: `C:\Users\Usuario\AppData\Local\Temp\opencode\ca\TestResults\authoritative-final-coverage-a\2398ae18-2eaa-44b6-8e41-12152ce5970e\coverage.cobertura.xml`
- Failing host B: `C:\Users\Usuario\AppData\Local\Temp\opencode\cb\TestResults\authoritative-final-coverage-b\b110d69a-5a47-4aa5-8e7b-878c3edd67dd\coverage.cobertura.xml`

| Changed production area | Line | Branch | Runtime audit |
|---|---:|---:|---|
| `AntiTamperMonitor` | 98.96% | 83.11% | bounded state, gate ownership, admission, faults, retry, conflicts, reset |
| `ExecuteDecisionChainAsync` state machine | 79.59% | 89.28% | reaction commit/fault, notification commit/fault, pending transition and clear; recovery-effect branch remains uncovered |
| `ValidateAdmission` | 100% | 100% | pending none/exact/mismatch, scope conflict, equal-position full-shape conflict |
| `ShouldExecute` | 87.50% | 72.22% | progress none/equal/older/newer hit; defensive final conflict throw is pre-empted by full-shape validation and has zero hits |

Passing host A shows positive hits for reaction pending assignment (`675`), notification pending transition (`697`), notification-domain clear (`693`), final clear (`702–703`), blocked mismatch (`737–739`), scope conflict (`745–747`), shape conflict (`750–752`), progress none (`760`), newer (`761`), older (`762`), equal (`763`), and durable notification retry (`680–698`).

## Behavioral Compliance Matrix

| Invariant / scenario | Runtime and static evidence | Result |
|---|---|---|
| Strict O(1) per generation | exactly nullable `ReactionProgress`, `NotificationProgress`, and one nullable `PendingRetry`; no Slice B collection/history/cache/window/durable state | ✅ COMPLIANT |
| Existing `Generation.Gate` is sole transition serializer | source inspection; duplicate/order/barrier tests; no second gate, owner, queue, actor, channel, or drainer | ✅ COMPLIANT |
| Pending retry recorded before gate release | collaborator catches assign `PendingRetry` before rethrow; gate release is in the outer `finally` | ✅ COMPLIANT |
| Older fault blocks queued/newer/older/different-shape decisions | `FailedOlderDecision_BlocksQueuedNewerDecisionUntilExactRetry`; persist test; 30-process stress; zero additional collaborator attempts while blocked | ✅ COMPLIANT |
| Exact failed decision/domain retry and explicit newer retry after clear | reaction retry, notification retry, and persist-then-throw test | ✅ COMPLIANT |
| Reaction failure leaves both domains uncommitted | `ReactionFailure_LeavesBothDomainsUncommittedAndRetrySucceeds` | ✅ COMPLIANT |
| Notification failure commits reaction only and reuses exact key | `NotificationFailure_CommitsReactionAndRetriesOnlyNotificationWithSameKey` | ✅ COMPLIANT |
| Original collaborator fault remains observable | same exception observed by work; `EffectFault.TrySetResult` preserves first fault for Stop | ✅ COMPLIANT in focused Slice B tests |
| Full shape and same-generation scope validation before effects | null-notification conflict and scope conflict tests; `ValidateAdmission` 100% line/branch | ✅ COMPLIANT for covered public shapes |
| Different legitimate newer shape executes after clear | persist test retries blocked newer decision after clear; newer/reaction-only test | ✅ COMPLIANT |
| Real SQLite persist-then-throw convergence | actual `OutboxManager` persists exact key once; controlled wrapper throws after persistence; exact retry dedupes to one row and clears | ✅ COMPLIANT |
| Structural progress comparison and separate domains | direct epoch/sequence and ordinal record/string comparisons; no key parsing, reconstruction, normalization, or logging; separate progress tokens | ✅ COMPLIANT |
| Generation replacement resets bounded state without cross-generation suppression | failed-older test stops/restarts and executes the newer decision in a fresh generation; inherited ownership tests pass in focused execution | ✅ COMPLIANT |
| Slice A full-suite fault/Stop preservation | one full Service run and isolated coverage host B fail the inherited late-failure Stop assertion | ❌ FAILING |
| No 4C2/Unit5/transport/schema drift | exact two-file diff and source audit | ✅ COMPLIANT |

**Compliance summary**: 14/15 invariant rows compliant; Slice A full-suite fault observability is failing nondeterministically.

## Static Correctness

| Requirement | Status | Notes |
|---|---|---|
| Bounded state | ✅ Implemented | two completion tokens plus at most one full-shape/domain pending token |
| Gate and owner integrity | ✅ Implemented | existing generation gate and owned admission path only |
| Overtaking closure | ✅ Implemented | non-exact pending shape throws before effects; exact retry progresses and clears/transitions |
| Commit-after-success | ✅ Implemented | progress writes follow successful awaited collaborators |
| Full structural shape | ✅ Implemented | scope, epoch, sequence, reaction key/kind, notification presence/key/kind |
| Durable dedupe edge | ✅ Implemented/proven | real manager/store and SQLite unique-key path; wrapper only injects post-persistence failure |
| Deferred-scope exclusion | ✅ Preserved | no new durable retry/restart/deadline/clock/crash/transport/schema state |
| Slice A late-failure observation | ❌ Runtime unstable | two independent broad executions failed the same public assertion despite 20/20 isolated passes |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Single B2 owner/gate | ✅ Yes | no second serialization or task owner |
| O(1) separate-domain progress plus one pending token | ✅ Yes | exact intended state shape |
| Complete immutable decision shape | ✅ Yes | record equality is structural/ordinal |
| Retry barrier before gate release | ✅ Yes | catches assign then rethrow |
| Verbatim key and real outbox dedupe | ✅ Yes | exact key retained and persisted once |
| Slice A behavior preserved | ❌ Not fully proven | broad late-failure executions are nondeterministic |
| 4C2 remains sole future durable escalation owner | ✅ Yes | no scope leakage |

## Strict-TDD Compliance

The exact parent was replayed in a detached worktree with the current duplicate test and only the minimal generation-flow compile seam, with no progress/dedupe logic. The first run failed with **8 reactions** instead of 1. A diagnostic assertion-only rerun accepted 8 reactions and then failed with **8 notifications** instead of 1. This confirms the exact 8+8 behavioral RED.

| Cycle | RED | Current GREEN | Classification |
|---|---|---|---|
| Same-decision duplicate | ✅ exact-parent behavioral RED: 8+8 | ✅ current 1+1; 30-process stress | complete |
| Reaction retry | ⚠️ no retained genuine historical RED | ✅ focused/current runtime | truthful process debt |
| Notification retry | ⚠️ no retained genuine historical RED | ✅ focused/current runtime | truthful process debt |
| Overtaking and equal-shape/scope conflicts | ⚠️ artifacts and Engram record genuine prior-candidate failures, but the exact rejected candidate binary/raw output was not preserved for this independent replay | ✅ current tests/stress/coverage | historical provenance debt, not fabricated |
| Persist-then-throw | ➖ safety: candidate was already green | ✅ real SQLite path | honestly classified safety |

The current active five-row table in `apply-progress.md` is internally consistent: it labels reaction/notification and persist edges as process debt/safety, and does not replace functional proof with prose. Older planning/placeholders are explicitly marked historical/superseded. The missing exact prior-candidate replay artifact remains a warning, not a newly invented RED.

### Test Layer Distribution

| Layer | Slice B tests | Files | Tools |
|---|---:|---:|---|
| Unit/component | 7 | 1 | xUnit, Moq, FluentAssertions |
| Integration | 1 | 1 | real `OutboxManager`, EF Core, SQLite |
| E2E | 0 | 0 | not configured |
| **Total** | **8** | **1** | |

### Assertion Quality

All Slice B tests invoke production behavior. No tautology, ghost loop, orphan empty assertion, standalone type-only proof, or smoke-only assertion was found. Mock invocation counts are contract observations for at-most-once/retry behavior; the persist test additionally asserts physical durable rows.

**Assertion quality**: ✅ no trivial assertion finding.

### Quality Metrics

**Linter/analyzers**: inherited warning corpus; changed code also remains densely formatted and receives existing readability/style warnings.  
**Type/build checks**: ✅ five builds completed with 0 errors.

## Hygiene, Budget, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | exact requested branch and base |
| Candidate binary diff hash | exact `9c21c3974ce5cc6e9374104f6ce1264763512959` |
| CODE+TEST numstat | exact **311/400** |
| `git diff --check` | pass; only LF→CRLF notices |
| Staged/index changes | none |
| Changed CODE+TEST paths | exactly `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` |
| `.codegraph/` | removed after required structural inspection |
| Generated output | coverage/build output ignored; no generated path added to status |
| OpenSpec | cumulative artifacts and reports remain untracked/excluded; prior FAIL preserved; this report is new |
| Deferred scope | no handler/outbox API/4C2/Unit5/transport/schema change |

## Issues Found

### CRITICAL

1. **Fresh full-suite Slice A fault observability is nondeterministic.** Main full Service run 1 and physically isolated coverage host B both failed `LateNonCancellableFailure_IsObservableAndHasNoEffects` because the expected `InvalidOperationException` was not observed. Main run 2, host A, and 20/20 isolated stress passes do not erase two non-zero authoritative gates. This violates the required preservation gate and blocks commit approval.
2. **Two passing physically isolated coverage hosts were not obtained.** Host A passed; host B produced non-empty coverage but failed the same late-failure test. The requested coverage gate is therefore incomplete/failing.

### WARNING

1. Exact rejected-prior-candidate RED replay is unavailable because that uncommitted candidate/raw output was not preserved. Current artifacts consistently record overtaking and shape-conflict behavioral REDs, but this verifier cannot elevate that history to exact replay evidence.
2. Reaction-retry and notification-retry historical REDs remain explicitly recorded process debt; persist-then-throw is correctly classified as safety.
3. `ExecuteDecisionChainAsync` recovery-effect lines 653–664 remain uncovered; `ShouldExecute`'s defensive final throw is also unhit because full-shape validation rejects conflicts earlier.
4. Dense one-line source/test statements reduce reviewability. Existing package/analyzer warnings and duplicate xUnit-ID notice remain inherited.

### SUGGESTION

None. The broad late-failure runtime blocker must be resolved or authoritatively isolated before optional cleanup.

## Final Verdict

**FAIL**

The Slice B remediation itself is bounded, uses the sole existing gate, closes overtaking, validates complete shapes, retries exact failed domains, preserves durable outbox dedupe, stays at 311/400, and passes all focused/stress evidence. Approval is nevertheless blocked because two fresh broad executions failed an inherited Slice A fault-observation contract, including one physically isolated coverage host. A non-zero runtime gate is authoritative and cannot be converted to a warning by isolated reruns.

**NOT APPROVED FOR 4C1B2B COMMIT**
