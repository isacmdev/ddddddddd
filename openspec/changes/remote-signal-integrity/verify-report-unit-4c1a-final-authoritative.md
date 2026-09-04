# Verification Report

**Change**: `remote-signal-integrity` — final Unit 4C1A verification after trust-at-deadline evidence repair  
**Branch**: `feat/sdd7-4c-verdict-ordering-escalation`  
**HEAD / base / merge-base**: `6c6668bbdb5eb2bc328cfe86c28f878a51149679`  
**Mode**: Strict TDD, hybrid persistence, isolated sequential hosts  
**Verdict**: **FAIL**  
**4C1A commit approval**: **NOT APPROVED**

## Scope and Exclusions

This report scores only the sequential/reentrant 4C1A policy state machine. Deferred 4C1B ingress sequencing, concurrent effect ordering, stale-effect suppression, FIFO mailbox, and async API behavior are skipped. Deferred 4C2 persistence, timer ownership, restart/rollback/agent-death convergence, and Unit 5 are also skipped. Tasks remain intentionally `9/14`; 4.1–4.3 remain unchecked.

No source, test, or task file was edited during verification. Prior reports were preserved. Because this verdict is not successful, no `approved-authoritative` report was created.

## Completeness

| Metric | Value |
|---|---:|
| Global tasks | 14 |
| Complete | 9 |
| Intentionally deferred | 5 |
| Tracked CODE+TEST files | 3 |
| CODE+TEST budget | **400/400** |
| Required 4C1A scenarios compliant | **9/10** |

## Host Isolation and Execution Method

- All build, test, and coverage processes ran sequentially.
- Stale `testhost`/`vstest` processes were checked before and after every retained host; final result was none.
- Five retained builds used physically separate copied workspaces, giving every build a distinct default `BaseOutputPath` and `BaseIntermediateOutputPath` while reusing existing restore assets.
- Focused test hosts copied the fresh Service-test output into distinct host directories and used unique TRX/results/diagnostic paths.
- Full and coverage hosts used complete distinct workspaces because source-inspection tests require a discoverable repository root.
- Two coverage hosts used different workspaces, binaries, result directories, TRX files, diagnostics, and Cobertura destinations.

Three discarded harness experiments were inspected rather than attributed to code: SDK artifacts relocation lacked restored assets/runtime-pack copying; an isolated copied WinUI workspace lacked one generated deployment extension; and a bin-only full host could not discover repository source. The retained compatible isolated reruns below all passed. These are verification-harness warnings, not product reproductions.

## Build and Runtime Evidence

Every retained .NET gate used `--no-restore`; builds used portable PDBs.

| Gate | Result |
|---|---:|
| Domain portable-PDB build | ✅ 0 errors |
| Service portable-PDB build | ✅ 0 errors |
| Service-tests portable-PDB build | ✅ 0 errors |
| App.UI portable-PDB build | ✅ 0 errors |
| App.UI-tests portable-PDB build | ✅ 0 errors |
| Exact trust/remediation | ✅ 4/4 |
| `IntegrityVerdictHandlerTests` | ✅ 31/31 |
| `IntegrityRuntimePathTests` | ✅ 9/9 |
| `AntiTamperMonitorTests` | ✅ 55/55 |
| IntegrityChecker + Enforcement | ✅ 31/31 |
| Explicit B1+B2 lifecycle matrix | ✅ 22/22 |
| Combined Unit4A+B1+B2+4C1A | ✅ 127/127 |
| Isolated late-failure host A | ✅ 1/1 |
| Isolated late-failure host B | ✅ 1/1 |
| Full Service host A | ✅ 1,216/1,216 |
| Full Service host B | ✅ 1,216/1,216 |
| Full App.UI | ✅ 192/192 |
| Coverage host A | ✅ 1,216/1,216 |
| Coverage host B | ✅ 1,216/1,216 |

The Service hosts emitted the existing duplicate xUnit-ID notice for `HttpResponseClassifierTests`; no test was reported skipped.

AntiTamper source and test blobs are byte-identical to base/B2:

- `AntiTamperMonitor.cs`: `cce963754da5ac9cf096ece91b7085c6bde28ae0`
- `AntiTamperMonitorTests.cs`: `0f0caa1f835cf0e2e653197bece99cb7bf8287d0`

The fresh 55/55, 22/22, two late-failure hosts, and 127/127 combined gate corroborate the earlier diagnosis that concurrent shared-output failures were infrastructure interference, not 4C1A or B2 drift.

## 4C1A Compliance Matrix

| # | Requirement / scenario | Runtime evidence | Result |
|---:|---|---|---|
| 1 | Equal timestamps and accepted duplicates count sequentially | Equal-timestamp handler test | ✅ COMPLIANT |
| 2 | Revoked 1/2/3/4 produce WARN, LIMIT, pending LIMIT, unchanged pending | Handler staging and real runtime-path tests | ✅ COMPLIANT |
| 3 | Third revoked creates one local five-minute deadline and one notification identity | Handler/outbox assertions and fake-clock runtime path | ✅ COMPLIANT |
| 4 | Before/equal/after deadline is exact and one-shot through explicit/opportunistic evaluation | Deadline tests and runtime path | ✅ COMPLIANT |
| 5 | Trust accepted exactly at due first returns Degrade, counts as recovery 1/3, recovers on third total trust, and does not repeat | `TrustAtDeadline_DegradesThenRecoversExactlyOnce`; trust deadline branch now covered | ✅ COMPLIANT |
| 6 | Trust before due cancels; old deadline is inert; a fresh revoked sequence creates a fresh deadline/notification | `TrustBeforeDeadlineCancelsPendingEscalationAndFreshSequenceDegrades` | ✅ COMPLIANT |
| 7 | Unknown/non-definitive accepted at due commits Degrade without changing revoked count | `NonDefinitiveAtDeadlineDegradesWithoutChangingRevokedCount` | ✅ COMPLIANT |
| 8 | Repeated transient threshold preserves definitive pending state | `NonDefinitivePreservesPendingAndDeadlineIsPureAndOneShot` reaches 4/3 after five failures | ✅ COMPLIANT |
| 9 | An observation accepted at due while the circuit is open commits the deadline before returning circuit `ShadowWarn` | Source implements the ordering, but no current test executes `deadlineWon == true` inside the open-circuit branch | ❌ UNTESTED |
| 10 | Recovery is one-shot; revoked starts a fresh later recovery sequence; reentrant callbacks and faults are observed outside the lock; no timer/task owner is added | Handler recovery/reentrancy/fault tests and source inspection | ✅ COMPLIANT |

**Compliance summary**: **9/10 required scenarios compliant**.

### Blocking circuit-at-due evidence regression

The trust-at-deadline repair repurposed the previous circuit-at-due case. The remaining transient test opens the circuit but calls `EvaluateDeadline(...)` directly; it does not call `HandleVerdict(...)` at due while the circuit is open.

Both fresh full-suite coverage artifacts show:

- `IntegrityVerdictHandler.cs:194` (`if (deadlineWon)` inside the open-circuit branch): **50% (1/2)** condition coverage.
- Line 195 (`ShadowWarn`) is hit, proving only the false outcome runs.
- The true outcome that returns Degrade before the circuit result is not executed.

Source order at lines 189–195 is correct, but the verification contract does not treat source inspection as proof of a required scenario.

## Correctness and Design Coherence

| Decision | Status | Notes |
|---|---|---|
| Deadline commits before circuit guard | ✅ Implemented | `CommitDeadlineIfDueLocked` precedes `IsCircuitOpenAtLocked`. |
| Transients preserve revoked/pending state | ✅ Implemented | Circuit opening no longer clears definitive revoked state. |
| Trust-at-due precedence and recovery count | ✅ Implemented and covered | Trust branch `deadlineWon == true` is 100% condition-covered. |
| Pre-degrade trust cancellation | ✅ Implemented and covered | Old deadline is inert; fresh sequence degrades. |
| One-shot recovery | ✅ Implemented and covered | Later trust does not repeat; later degradation starts fresh. |
| Narrow lock / external effects | ✅ 4C1A-compliant | Clock, outbox, and callback work remain outside `stateGate`. |
| No ownerless timer | ✅ | Deadline seam remains pure/opportunistic; no timer/task added. |
| Circuit-at-due runtime proof | ❌ Missing | Correct source branch is not exercised by any current test. |
| 4C1B/4C2 exclusion | ✅ | No deferred owner/API/store/timer drift. |

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ⚠️ Partial | Six-column evidence and retained logs exist for one-shot recovery; latest remediation is prose-only. |
| Historical RED | ⚠️ | Early callback/clock/deadline RED was compile-only; retained raw RED logs do not contain the latest transient/circuit failures. |
| GREEN confirmed | ✅ | Every retained runtime/build/coverage host passed. |
| Trust repair triangulation | ✅ | Exact due trust, pre-due cancellation, non-definitive due, and recovery all pass. |
| Circuit-at-due triangulation | ❌ | Repurposing removed the only covering scenario. |
| Safety net | ✅ | Isolated focused, lifecycle, full, late-failure, App.UI, and two coverage hosts pass. |

## Test Layers

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Handler unit/component | 31 | 1 | Policy, deadline, circuit, cancellation, callback, and recovery |
| Runtime integration | 9 | 1 | Real monitor/backend/enforcement/store path with controlled local time |
| E2E | 0 | 0 | Outside this slice |

## Changed-File Coverage

Fresh nonempty artifacts:

- Host A: `C:/Users/Usuario/AppData/Local/Temp/opencode/verify-unit4c1a-approved/coverage/host-a/results/7a8610c6-3d72-432c-94e4-a4f7a5184743/coverage.cobertura.xml` — 4,859,400 bytes.
- Host B: `C:/Users/Usuario/AppData/Local/Temp/opencode/verify-unit4c1a-approved/coverage/host-b/results/870598e0-5d03-48c1-80ee-8792c7da9b2b/coverage.cobertura.xml` — 4,859,388 bytes.

Both report:

| Changed production symbol | Line | Branch | Uncovered lines |
|---|---:|---:|---|
| `IntegrityVerdictHandler` | 98.11% | 93.90% | 179–180, 285–286 |
| `HandleVerdict` | 100% | 100% | — |
| `HandleVerdictCore` | 96.96% | 90.62% | Disposed return; circuit-at-due true outcome missing |
| `HandleRevokedVerdict` | 100% | 100% | — |
| `EvaluateDeadline` | 100% | 90% | One condition outcome missing |
| `CommitDeadlineIfDueLocked` | 100% | 100% | — |
| `FlushNotifications` | 100% | 100% | — |

Trust line 257 is now **100% (2/2)** condition-covered. Circuit line 194 remains **50% (1/2)**.

## Assertion Quality

- The exact trust-at-due test uses the public handler flow, exact fake-clock equality, Degrade action, recovery false/true/false sequence, and meaningful production assertions.
- Pre-deadline trust test name now matches its body.
- The runtime snapshot assertion now has an intervening public `RunOnce(...)` operation at line 86; the prior empty immediate-reread warning is resolved.
- No tautology, ghost loop, sleep/stress loop, smoke-only assertion, or production-free assertion was found in the repaired area.

## Workspace, Budget, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | ✅ exact requested values |
| Staged files | ✅ none |
| Tracked CODE+TEST files | ✅ exactly handler plus two test files |
| CODE+TEST budget | ✅ `400/400`: production `124+67`; runtime tests `32+29`; handler tests `130+18` |
| `git diff --check` | ✅ exit 0; LF→CRLF notices only |
| Patch hash | ✅ `1A18C38033A7BF3B29D814D0FCEFDD46FB61A14261AEF0C3EA2E5EC55F94FE1A`, length 26,005 |
| `.codegraph/` | ✅ absent before and after execution |
| Project/dependency/config drift | ✅ none |
| AntiTamper/B1/B2 drift | ✅ none; exact base blobs |
| 4C1B/4C2/Unit5 drift | ✅ none |

## Issues

### CRITICAL

1. The required circuit-open-at-due observation scenario has no passing covering test. Fresh coverage proves the `deadlineWon == true` outcome inside the circuit branch is unexecuted.

### WARNING

1. Historical early policy RED was compile-only, and the latest transient/circuit RED has no retained raw log.
2. Initial isolation harness layouts were incompatible with restore-free SDK/WinUI/source-discovery behavior; raw failures were inspected and all retained compatible isolated gates passed.
3. Existing analyzer/package warnings and the duplicate xUnit-ID notice remain outside this slice.

### SUGGESTION

None. Verification did not alter implementation artifacts.

## Final Verdict

**FAIL**

All retained isolated runtime, build, regression, late-failure, and coverage hosts pass. The trust-at-deadline repair is valid and covered. However, repurposing the previous case removed runtime proof that a due deadline wins while the circuit is open. Required scenario coverage is therefore incomplete.

**NOT APPROVED FOR 4C1A COMMIT.**
