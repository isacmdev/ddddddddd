# Verification Report

**Change**: `remote-signal-integrity` — final Unit 4C1A approval after public circuit-at-due coverage closure  
**Branch**: `feat/sdd7-4c-verdict-ordering-escalation`  
**HEAD / base / merge-base**: `6c6668bbdb5eb2bc328cfe86c28f878a51149679`  
**Mode**: Strict TDD, hybrid persistence, sequential isolated hosts  
**Verdict**: **PASS WITH WARNINGS**  
**4C1A commit approval**: **APPROVED FOR 4C1A COMMIT**

## Scope and Exclusions

This report approves only the sequential/reentrant 4C1A policy state machine. Deferred 4C1B ingress sequencing, concurrent effect ordering, stale-effect suppression, FIFO mailbox, and async API behavior are not judged. Deferred 4C2 persistence, durable timer ownership, restart/rollback/agent-death convergence, and Unit 5 are also excluded.

Tasks remain intentionally `9/14`; aggregate tasks 4.1–4.3 stay unchecked until all Unit 4C children and the final Unit 4 gate pass. No source, test, task, project, dependency, configuration, commit, or prior verification report was modified during this verification. The only new repository artifact is this noncolliding approval report.

## Completeness

| Metric | Value |
|---|---:|
| Global tasks | 14 |
| Complete | 9 |
| Intentionally deferred | 5 |
| Tracked CODE+TEST files | 3 |
| CODE+TEST budget | **400/400** |
| Required 4C1A scenarios compliant | **10/10** |

## Isolation and Execution Method

- All retained build, test, regression, and coverage processes ran sequentially.
- Every retained test process used its own physical test binaries, source-root copy, result directory, TRX file, and diagnostic log.
- The five retained builds used separate physical workspaces and therefore separate default output/intermediate trees. App.UI builds used short isolated roots to satisfy generated Windows App SDK path limits.
- Full Service and coverage hosts used independent complete workspaces so repository-root/source-inspection tests and the coverage collector could resolve normally.
- No stale `testhost` or `vstest.console` process existed before or after retained gates; final count was zero.

Discarded harness attempts were not used as evidence: one long-path App.UI copy lacked a generated Windows App SDK deployment extension, one command accidentally targeted the original worktree and was discarded, and direct `vstest` coverage passed tests but could not resolve `XPlat Code Coverage`. Compatible isolated replacements passed.

## Build and Runtime Evidence

All retained builds used `--no-restore`, Debug portable PDBs, and zero errors. Test assemblies came from those fresh builds; retained coverage used `dotnet test --no-restore --no-build`.

| Gate | Result | Evidence root |
|---|---:|---|
| Domain build | ✅ 0 errors | `verify-unit4c1a-final-approval-20260822/build-domain` |
| Service build | ✅ 0 errors | `verify-unit4c1a-final-approval-20260822/build-service` |
| Service-tests build | ✅ 0 errors | `verify-unit4c1a-final-approval-20260822/build-service-tests` |
| App.UI build | ✅ 0 errors | `u4ca` |
| App.UI-tests build | ✅ 0 errors | `u4cb` |
| Focused repaired 4C1A scenarios | ✅ 5/5 | `s4c01/results/focused-4c1a.trx` |
| `IntegrityVerdictHandlerTests` | ✅ 31/31 | `s4c02/results/handler.trx` |
| `IntegrityRuntimePathTests` | ✅ 9/9 | `s4c03/results/runtime.trx` |
| `AntiTamperMonitorTests` | ✅ 55/55 | `s4c04/results/antitamper.trx` |
| IntegrityChecker + Enforcement | ✅ 31/31 | `s4c05/results/integrity-enforcement.trx` |
| Explicit B1+B2 lifecycle matrix | ✅ 22/22 | `s4c06/results/b1-b2-lifecycle.trx` |
| Combined Unit4A+B1+B2+4C1A | ✅ 127/127 | `s4c07/results/combined-unit4.trx` |
| Isolated late-failure host A | ✅ 1/1 | `s4c08/results/late-failure-a.trx` |
| Isolated late-failure host B | ✅ 1/1 | `s4c09/results/late-failure-b.trx` |
| Same-host reproduction after full-suite miss | ✅ 1/1 | `s4c10/late-after-full-failure/late-after-full-failure.trx` |
| Retained full Service A | ✅ 1,216/1,216 | `s4c11/full-service-retained-a/full-service-retained-a.trx` |
| Retained full Service B | ✅ 1,216/1,216 | `s4c14/full-service-retained-b/full-service-retained-b.trx` |
| Full App.UI | ✅ 192/192 | `a4c01/results/full-appui.trx` |
| Retained coverage A | ✅ 1,216/1,216 | `c4a/coverage-final-a` |
| Retained coverage B | ✅ 1,216/1,216 | `c4b/coverage-final-b` |

Service hosts emitted the existing duplicate xUnit-ID notice for `HttpResponseClassifierTests`; retained runs reported zero skipped tests.

### Inherited late-failure reproduction

One otherwise isolated full-Service attempt in `s4c10` reported the inherited `LateNonCancellableFailure_IsObservableAndHasNoEffects` assertion miss (`1 failed, 1,215 passed`). Unlike the earlier diagnosis, this host had unique physical binaries/results and no stale process, so shared-output interference is not a complete explanation.

The exact test then passed in that same host, both dedicated fresh hosts passed, two untouched full-Service hosts passed, both coverage hosts passed, and the AntiTamper production/test blobs are exactly unchanged from base:

- `AntiTamperMonitor.cs`: `cce963754da5ac9cf096ece91b7085c6bde28ae0`
- `AntiTamperMonitorTests.cs`: `0f0caa1f835cf0e2e653197bece99cb7bf8287d0`

This is retained as an inherited nondeterminism warning, not attributed to the 4C1A three-file change.

## 4C1A Compliance Matrix

| # | Requirement / scenario | Runtime evidence | Result |
|---:|---|---|---|
| 1 | Equal timestamps and accepted duplicates count sequentially | `EqualTimestamps_AreOrderedByAcceptanceAndAcceptedDuplicatesCount` | ✅ COMPLIANT |
| 2 | Revoked 1/2/3/4 produce WARN, LIMIT, pending LIMIT, unchanged pending | Staged handler tests and runtime path | ✅ COMPLIANT |
| 3 | Third revoked creates one local five-minute deadline and one notification identity | Handler/outbox assertions and injected-clock runtime path | ✅ COMPLIANT |
| 4 | Before/equal/after deadline is exact and one-shot through public and direct evaluation | Deadline tests, runtime path, coverage lines 194/365 | ✅ COMPLIANT |
| 5 | Trust accepted exactly at due first Degrades, counts recovery 1/3, recovers on third total trust, and does not repeat | `TrustAtDeadline_DegradesThenRecoversExactlyOnce` | ✅ COMPLIANT |
| 6 | Trust before due cancels; old deadline is inert; fresh revoked sequence creates a fresh deadline/notification | `TrustBeforeDeadlineCancelsPendingEscalationAndFreshSequenceDegrades` | ✅ COMPLIANT |
| 7 | Non-definitive accepted at due commits Degrade without changing revoked state | Deadline/non-definitive handler tests | ✅ COMPLIANT |
| 8 | Repeated transient threshold preserves definitive pending state | `NonDefinitivePreservesPendingAndDeadlineIsPureAndOneShot` | ✅ COMPLIANT |
| 9 | Public observation at due while circuit-open commits one Degrade before subsequent circuit ShadowWarn | `HandleVerdict_CircuitOpenAtDeadline_DegradesOnceThenShadows`; line 194 100% branch | ✅ COMPLIANT |
| 10 | Recovery is one-shot; revoked starts a fresh later recovery sequence; reentrant callbacks/faults are observed outside lock; no timer owner is added | Recovery, reentrancy, callback-fault, and source audits | ✅ COMPLIANT |

**Compliance summary**: **10/10 required 4C1A scenarios compliant**.

## Circuit-at-Due Closure

The repaired public-flow test:

1. creates a pending deadline with three accepted revoked observations;
2. opens the circuit with five non-definitive failures without clearing pending state;
3. advances the injected local acceptance clock exactly five minutes;
4. calls public `HandleVerdict(...)` and asserts `Degrade`;
5. calls public `HandleVerdict(...)` again and asserts circuit `ShadowWarn`;
6. asserts the circuit remains open and direct `EvaluateDeadline(...)` returns `None`, proving no duplicate transition.

Both fresh retained coverage artifacts independently report:

- circuit branch line 194: `hits="4"`, `100% (2/2)`;
- trust-at-due branch line 257: `hits="17"`, `100% (2/2)`;
- direct `EvaluateDeadline` transition line 365: `hits="8"`, `100% (2/2)`.

## Correctness and Design Coherence

| Decision | Status | Notes |
|---|---|---|
| Deadline commits before circuit guard | ✅ Followed | `CommitDeadlineIfDueLocked` precedes `IsCircuitOpenAtLocked`; both circuit outcomes execute. |
| Transients preserve revoked/pending state | ✅ Followed | Five failures open the circuit without clearing the pending deadline. |
| Trust-at-due precedence and recovery count | ✅ Followed | Exact due trust Degrades first and remains recovery observation 1/3. |
| Pre-degrade trust cancellation | ✅ Followed | Old deadline becomes inert; fresh revoked sequence creates a new transition. |
| One-shot recovery | ✅ Followed | Later trust does not repeat recovery; later degradation enables one fresh recovery. |
| Narrow state lock | ✅ Followed | Acceptance clock is read before lock; state transition alone is locked. |
| External work outside lock | ✅ Followed | Notification flush and reaction callbacks execute after the state lock. |
| No ownerless timer/task | ✅ Followed | 4C1A retains explicit/opportunistic `EvaluateDeadline`; no timer/task/resource owner was added. |
| 4C1B/4C2 exclusion | ✅ Followed | No mailbox, sequence API, durable store, timer owner, restart, Program, or composition drift. |

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ⚠️ Partial | Six-column evidence exists for the underlying policy/recovery work; final public-flow closure is prose evidence. |
| Test files exist | ✅ | Handler and runtime test files are present and freshly executed. |
| RED chronology | ⚠️ Historical limitation | Early policy RED was compile-only and latest raw RED logs were not retained; no RED is fabricated. |
| GREEN confirmed | ✅ | All retained functional, build, regression, and coverage gates pass. |
| Triangulation adequate | ✅ | Trust-at-due, pre-due cancellation, non-definitive due, circuit-at-due true/false, direct deadline, and recovery are distinct cases. |
| Safety net | ⚠️ | Retained matrix is green; inherited isolated late-failure nondeterminism remains documented. |

## Test Layers

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Handler unit/component | 31 | 1 | Policy, deadline, circuit, cancellation, callback, and recovery behavior |
| Runtime integration | 9 | 1 | Real monitor/backend/policy/enforcement/store path with controlled local time |
| E2E | 0 | 0 | Outside this slice |

## Changed-File Coverage

Fresh nonempty artifacts:

- Host A: `C:/Users/Usuario/AppData/Local/Temp/opencode/c4a/coverage-final-a/2dc70c95-456a-4045-8b0d-a92c36d72204/coverage.cobertura.xml` — 4,859,430 bytes.
- Host B: `C:/Users/Usuario/AppData/Local/Temp/opencode/c4b/coverage-final-b/31a81589-19c4-487e-a829-623dec905e58/coverage.cobertura.xml` — 4,859,406 bytes.

Both report identical changed-production coverage:

| Symbol | Line | Branch | Uncovered lines |
|---|---:|---:|---|
| `IntegrityVerdictHandler` | 98.11% | 93.90% | 179–180, 285–286 |
| `HandleVerdict` | 100% | 100% | — |
| `HandleVerdictCore` | 96.96% | 90.62% | Disposed defensive return |
| `HandleRevokedVerdict` | 100% | 100% | — |
| `EvaluateDeadline` | 100% | 90% | One defensive condition outcome |
| `CommitDeadlineIfDueLocked` | 100% | 100% | — |
| `FlushNotifications` | 100% | 100% | — |

## Assertion Quality

- Circuit-at-due test name, setup, exact injected local clock, public calls, and assertions align.
- The test asserts both public outcomes and the no-duplicate direct follow-up.
- Trust-at-due, pre-due cancellation, transient preservation, and one-shot recovery names match their bodies.
- Direct `EvaluateDeadline` remains exercised independently.
- No tautology, ghost loop, smoke-only assertion, production-free assertion, sleep/stress loop, or empty immediate reread was found in the modified tests.

**Assertion quality**: ✅ All repaired assertions verify observable behavior.

## Workspace, Budget, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | ✅ exact requested values |
| Staged files | ✅ none |
| Tracked CODE+TEST files | ✅ exactly handler plus two intended tests |
| CODE+TEST budget | ✅ `400/400`: production `124+67`; runtime tests `32+29`; handler tests `115+33` |
| Production unchanged in final closure | ✅ handler diff remains `124+67`; latest closure is test-only |
| `git diff --check` | ✅ exit 0; LF→CRLF notices only |
| Failed-candidate patch | ✅ SHA-256 `1A18C38033A7BF3B29D814D0FCEFDD46FB61A14261AEF0C3EA2E5EC55F94FE1A`, length 26,005 |
| `.codegraph/` | ✅ absent before and after |
| AntiTamper/B1/B2 drift | ✅ none; exact base blobs |
| Project/dependency/config drift | ✅ none |
| 4C1B/4C2/Unit5 drift | ✅ none |
| Stale test processes | ✅ zero |

## Issues

### CRITICAL

None.

### WARNING

1. Historical strict-TDD evidence remains incomplete: early policy RED was compile-only, and the latest remediation raw RED logs were not retained.
2. The inherited AntiTamper late-failure assertion missed once in a fully isolated full-Service host, then passed immediately in the same host and across dedicated, full, and coverage reruns. Shared output is therefore not the sole possible cause; residual inherited nondeterminism remains outside the unchanged 4C1A files.
3. Existing analyzer/package warnings and the duplicate xUnit-ID discovery notice remain outside this slice.
4. Initial long-path and direct-vstest coverage harness layouts were discarded and replaced by passing compatible isolated hosts.

### SUGGESTION

None for 4C1A approval. Investigate the inherited AntiTamper nondeterminism in its own remediation slice if it recurs.

## Final Verdict

**PASS WITH WARNINGS**

All ten required 4C1A scenarios now have passing runtime evidence. Both fresh retained coverage hosts pass and independently prove the public circuit-at-due true/false outcomes, exact trust-at-due outcome, and direct deadline path. Scope, budget, drift, isolation, and regression gates are satisfied.

**APPROVED FOR 4C1A COMMIT.**
