# Verification Report

**Change**: `remote-signal-integrity` — Unit 4C1A Escalation Policy State Machine only  
**Branch**: `feat/sdd7-4c-verdict-ordering-escalation`  
**HEAD / base / merge-base**: `6c6668bbdb5eb2bc328cfe86c28f878a51149679`  
**Mode**: Strict TDD, hybrid persistence, autonomous partial work-unit gate  
**Verdict**: **FAIL**  
**4C1A commit approval**: **NOT APPROVED**

## Scope and Exclusions

This report scores only the synchronized sequential/reentrant 4C1A policy state machine. It does **not** fail 4C1A for deferred 4C1B monotonic ingress sequence, concurrent equal-timestamp linearization, stale delayed-effect suppression, FIFO mailbox/callback ordering, or async API work. It also excludes 4C2 persistence, rehydration, timer/deadline ownership, restart/rollback/agent-death convergence, and all Unit 5 work.

The revised design and tasks establish the 4C1A → 4C1B → 4C2 boundary. The proposal and delta specs remain broader capability artifacts; their explicitly deferred dimensions are recorded as skipped rather than treated as 4C1A failures. Tasks remain intentionally `9/14`; unchecked 4.1–4.3 are not a blocker for this slice.

CodeGraph fallback: `.codegraph/` was absent at verification start, and this task explicitly requires it to remain absent. No index was initialized; source was inspected directly.

## Completeness

| Metric | Value |
|---|---:|
| Global tasks | 14 |
| Complete | 9 |
| Intentionally incomplete | 5 |
| 4C1A CODE+TEST | **393/400** |
| Tracked CODE+TEST files | 3 |

## Build and Runtime Evidence

Every .NET command used `--no-restore`; tests used fresh portable-PDB builds and `--no-build`.

| Gate | Result |
|---|---:|
| Domain portable-PDB build | ✅ 0 errors |
| Service portable-PDB build | ✅ 0 errors |
| Service-tests portable-PDB build | ✅ 0 errors |
| App.UI portable-PDB build | ✅ 0 errors |
| App.UI-tests portable-PDB build | ✅ 0 errors |
| One-shot recovery exact test | ✅ 1/1 |
| `IntegrityVerdictHandlerTests` | ✅ 31/31 |
| `IntegrityRuntimePathTests` | ✅ 9/9 |
| `AntiTamperMonitorTests` | ✅ 55/55 |
| `IntegrityCheckerTests` | ✅ 7/7 |
| `EnforcementLevelMonitorTests` | ✅ 24/24 |
| Explicit B1+B2 lifecycle matrix | ✅ 22/22 |
| Combined Unit4A+B1+B2+4C1A | ✅ 127/127 |
| Inherited late-failure host A | ✅ 1/1 |
| Inherited late-failure host B | ✅ 1/1 |
| Full Service default-parallel host A | ✅ 1,216/1,216 |
| Full Service default-parallel host B | ✅ 1,216/1,216 |
| Full App.UI | ✅ 192/192 |
| Coverage host A | ✅ 1,216/1,216 |
| Coverage host B | ✅ 1,216/1,216 |

Both full/coverage Service runs emitted the existing duplicate xUnit ID notice for `HttpResponseClassifierTests`; all reported zero failed and zero skipped.

## 4C1A Acceptance Matrix

| # | Acceptance | Source and passing runtime evidence | Result |
|---:|---|---|---|
| 1 | Sequential accepted observations count without payload/timestamp dedupe | Equal-timestamp duplicate revoked calls produce WARN then LIMIT; trust resets; same-time revoked starts at 1/3 | ✅ COMPLIANT |
| 2 | Revoked WARN → LIMIT → pending LIMIT; fourth-plus does not extend/duplicate | Handler staging tests and runtime path prove first/second/third/rapid-fourth plus one notification | ✅ COMPLIANT |
| 3 | Local acceptance due-at; before pending, equality/after one Degrade; explicit/opportunistic idempotency | Injected handler/runtime local clock; `NonDefinitivePreservesPendingAndDeadlineIsPureAndOneShot`, revoked-at-due test, runtime exact due | ✅ COMPLIANT |
| 4 | Due precedence for revoked/trust/non-definitive | Dedicated revoked, trust, and unknown-at-due handler tests pass; trust at due becomes recovery observation 1/3 | ✅ COMPLIANT |
| 5 | Pre-degrade trust cancellation; exact one-shot three-trust recovery; fresh later sequence | One-shot public-flow test proves trust 1/2 false, 3 true, 4/5 false, then a later revoked/degrade cycle recovers once | ⚠️ PARTIAL — pending-deadline cancellation has source support but no complete covering test |
| 6 | Unknown/transient/cancelled preserve definitive state and never independently degrade | One unknown and one failure preserve a pending deadline; runtime snapshot theory/cancellation pass | ❌ FAILING / PARTIAL — repeated failures reset revoked state and can block due evaluation |
| 7 | Narrow lock; clock/callback/notification/outbox outside; sequential reentrancy/fault observation; no fire-and-forget | `HandleVerdictCore` mutates under `stateGate`; clock is captured first; callback and synchronously observed outbox work occur after unlock; reentrant callback and exact callback fault tests pass | ✅ COMPLIANT for sequential/reentrant 4C1A boundary |
| 8 | No ownerless timer; opportunistic observations; stable sequential dispose/idempotency | No timer/task is created; `EvaluateDeadline` is explicit and observations call the same commit seam; dispose is sequentially idempotent by source | ❌ PARTIAL — an open circuit returns before opportunistic due evaluation |
| 9 | Fake-local-clock runtime integration and stable canonical degradation | Production monitor/backend/handler/enforcement path proves rapid fourth pending, due-minus-one pending, exact due Degrade, one stable key, one notification | ✅ COMPLIANT |
| 10 | No 4C1B/4C2/Unit5/Program/retry/B2-helper drift | Diff contains only the handler and two claimed tests; no async sequence/mailbox API, timer/store/Program/helper change | ✅ COMPLIANT |

### Blocking policy defect

`HandleVerdictCore` checks `IsCircuitOpenAtLocked` before `CommitDeadlineIfDueLocked` (`IntegrityVerdictHandler.cs:189–195`). In the non-success path, the fifth transient failure opens the circuit and resets `consecutiveRevokedCount` (`198–218`, specifically `206`). This violates the in-scope requirement that transient outcomes preserve definitive counters/state. It also means a pending deadline can become due while production observations return `ShadowWarn` at the circuit check instead of opportunistically committing the due degradation. There is no production deadline owner until 4C2, so the explicit seam does not repair this 4C1A observation-path failure.

No passing test covers either sequence:

1. revoked 1/2 → five transient failures → next definitive revoked remains the third observation; or
2. pending deadline → transient circuit opens → observation at due still commits Degrade once.

Because a required scenario is both statically incorrect and lacks passing runtime evidence, this is CRITICAL under the verification contract.

## Correctness and Design Coherence

| 4C1A decision | Status | Notes |
|---|---|---|
| One synchronized policy authority | ✅ | Main verdict transition is under one `stateGate`. |
| Exact revoked staging and local due time | ✅ | Threshold and deadline branches match the scoped policy. |
| One-shot recovery latch | ✅ | Recovery now atomically clears degraded/trust/pending/due/fired state before returning recovery. |
| Revoked after recovery starts fresh sequence | ✅ | Public-flow test proves a later full escalation/recovery cycle. |
| Non-definitive preservation | ❌ | Fifth failure resets the definitive revoked count; circuit precedence can defer a due transition. |
| Sequential effects outside policy lock | ✅ | Clock, callback, and outbox execution are outside `stateGate`; no ownerless task/timer is added. |
| 4C1B ordering deferred | ✅ | No sequence/epoch mailbox or concurrent-order claim is scored here. |
| 4C2 durability deferred | ✅ | No store/timer/restart ownership added or required here. |

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| Base reset / failed-candidate preservation | ✅ documented and corroborated | Apply progress records exact restoration to B2 before the original 4C1 RED cycle. Preserved patch exists with SHA-256 `1A18C38033A7BF3B29D814D0FCEFDD46FB61A14261AEF0C3EA2E5EC55F94FE1A`, length 26,005. |
| One-shot behavior RED | ✅ genuine | Retained RED output shows the public-flow test failed at its fourth-trust assertion: expected `IsAuthoritativeRecovery == false`, found `true`, at test line 658. |
| Same-test GREEN | ✅ | Exact one-shot filter passed 1/1; full handler passed 31/31. |
| Earlier policy RED history | ⚠️ incomplete | Earlier callback/clock/deadline additions were compile-only RED, not behavioral RED. The runtime spec-alignment update is correctly not credited as RED. This is retained as historical process debt, not rewritten. |
| Triangulation | ❌ | Required repeated-transient preservation/circuit-deadline precedence and complete pending-trust cancellation scenarios are absent. |
| Safety net | ✅ | All requested focused, lifecycle, full, two-host, App.UI, and coverage gates passed. |

The one-shot remediation itself has valid RED→GREEN evidence. Strict-TDD history for earlier policy edits remains a WARNING; the current missing/failing non-definitive scenario independently blocks approval.

## Test Layers

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Handler unit/component | 31 | 1 | Sequential policy, deadline, reentrancy, fault, one-shot recovery |
| Runtime integration | 9 | 1 | Real monitor/backend/enforcement/store path with controlled local time |
| E2E | 0 | 0 | Correctly outside this slice |

## Changed-File Coverage

Fresh artifacts:

- `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-independent-a/443fa5f3-4bf7-45b2-be63-e1210065e91c/coverage.cobertura.xml`
- `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-independent-b/02467a84-347b-4e11-ac53-5fb24b676c42/coverage.cobertura.xml`

Both hosts report:

| Scope | Line | Branch | Uncovered lines |
|---|---:|---:|---|
| `IntegrityVerdictHandler` | 98.11% | 96.25% | 179–180, 285–286 |
| `HandleVerdict` | 100% | 100% | — |
| `HandleVerdictCore` | 96.96% | 93.33% | disposed return branch |
| `HandleRevokedVerdict` | 100% | 100% | — |
| `EvaluateDeadline` | 100% | 100% | — |
| `CommitDeadlineIfDueLocked` | 100% | 100% | — |
| `FlushNotifications` | 100% | 100% | — |

High aggregate branch coverage does not prove the missing multi-step transient/circuit sequences.

## Assertion Quality

| File | Location | Issue | Severity |
|---|---:|---|---|
| `IntegrityVerdictHandlerTests.cs` | 412–430 | `HandleVerdict_Unknown_ResetsBothCounters` has a stale name/comment and asserts only the warning reaction; it does not prove either reset or required preservation | WARNING |
| `IntegrityRuntimePathTests.cs` | 86–88 | Snapshot is compared to an immediate reread with no intervening production operation | WARNING |

The new one-shot recovery test is behaviorally meaningful and calls the public production flow. No literal tautology, ghost loop, sleep/stress loop, or smoke-only assertion was found.

## Workspace, Drift, and Budget

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | ✅ exact requested values |
| Staged files | ✅ none |
| Tracked CODE+TEST diff | ✅ exactly three claimed files |
| CODE+TEST budget | ✅ 393/400 (`123+66` production; `170+34` tests) |
| `git diff --check` | ✅ exit 0; LF→CRLF notices only |
| `.codegraph/` | ✅ absent before and after execution |
| Untracked content | cumulative OpenSpec artifacts, including this new report only |
| Patch hash | ✅ exact requested SHA-256 |
| Project/dependency/config drift | ✅ none |
| 4C1B API / 4C2 store-timer / Unit5-Program / B2 helper drift | ✅ none |

## Issues

### CRITICAL

1. Five transient failures reset `consecutiveRevokedCount`, violating definitive-state preservation.
2. Circuit-open precedence occurs before opportunistic deadline evaluation, so a production observation at/after due can return `ShadowWarn` instead of committing the one allowed Degrade.
3. No passing runtime test covers repeated-transient preservation/circuit-at-due behavior; the complete pending-deadline trust-cancellation scenario is also untested.

### WARNING

1. Earlier 4C1A policy changes have compile-only rather than behavioral RED evidence; only the one-shot remediation has retained genuine public-flow RED→GREEN.
2. Two changed tests contain weak/stale assertions described above.
3. Existing analyzer/package warnings and duplicate xUnit-ID notice remain outside this slice; no declaration drift was introduced.

### SUGGESTION

None. Verification was report-only and made no source, test, or task changes.

## Final Verdict

**FAIL**

The one-shot recovery remediation is correct and all requested fresh execution gates pass, but 4C1A is not autonomously commit-ready. Repeated transient failures still mutate definitive escalation state and can prevent opportunistic due-deadline commitment while the circuit is open, and the required scenario has no passing test.

**NOT APPROVED FOR 4C1A COMMIT.**
