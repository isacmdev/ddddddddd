# C1C Independent Closure Verification

**Change:** `remote-signal-integrity` task 4.3 / C1C deadline owner  
**Branch / HEAD:** `feat/sdd7-4c2c1c-deadline-owner` / `e5f9fc2e7205bcb1822f6bd6722cca7ca35ef876`  
**Mode:** Strict TDD, final independent closure verification  
**Final verdict:** **FAIL**

## Completeness

| Dimension | Result | Evidence |
|---|---|---|
| Production implementation | Implemented | Generation-owned one-shot timer, version checks, admitted callback work, timing normalization, phase-based arm/cancel, drain cleanup |
| Focused deadline tests | 11/11 passed | Nine methods with two theory expansions |
| Full monitor regression | 109/109 passed | Fresh `AntiTamperMonitorTests` execution |
| Repository safety net | 1310/1310 passed | Exact exclusion of only the two authorized FQNs |
| Required scenario coverage | Incomplete | No passing timer-origin tests for trust cancellation/fresh identity, active Save/effect faults and restart convergence, or direct Dispose cleanup |
| Strict-TDD proof | Incomplete | Final RED captured the two timing-normalization failures, but the callback-loss race was already green |

## Build, Tests, and Coverage Evidence

| Gate | Fresh result |
|---|---|
| Service build, `--no-restore` | Exit 0; 0 errors; existing `NU1601` warning |
| `DeadlineOwner_` focus | **11/11 passed** |
| Integrity handler deadline focus | **6/6 passed** |
| Whole `AntiTamperMonitorTests` | **109/109 passed** |
| Exact-exclusion safety net | **1310/1310 passed**; existing duplicate-test-ID notice |
| `git diff --check` | Exit 0 |
| Staged set | Empty |

Coverage artifact:

- Path: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1c-independent-closure-20260826\35f422c1-4033-48f6-8771-074a0da5869e\coverage.cobertura.xml`
- Size: `5,100,707` bytes
- SHA-256: `59D0297F25F716D9EFA6DCF2BD405F5B3523BAAC22F9147A8F180345F6E3D624`
- Main `AntiTamperMonitor` class: **99.07% line / 85.63% branch**.
- Changed production executable lines: **79/79 = 100%**.
- Changed production branch outcomes: **63/72 = 87.50%**.
- Partial changed branches: lines `317` (2/4), `319` (3/4), `322` (3/4), `326` (1/2), `336` (2/4), `423` (1/2), and `424` (1/2).
- The numeric changed-code thresholds exceed 80%, but aggregate coverage does not substitute for missing required behavioral scenarios.

Known package/analyzer warnings are pre-existing and non-blocking.

## Behavioral Compliance Matrix

| # | Required behavior | Runtime evidence | Status |
|---:|---|---|---|
| 1 | Third revoked observation arms one owner timer | Existing boundary test plus focused timer tests exercise Pending arming | **COMPLIANT** |
| 2 | Persisted future due is authoritative; restart does not extend it | Exact persisted due is used; Stop→Start creates a fresh generation with the same due | **COMPLIANT** |
| 3 | Exact/past due fires through a real one-shot timer | Real `System.Threading.Timer` theory passes for exact and past due | **COMPLIANT** |
| 4 | Synthetic early callback preserves timer/version and does not resubtract | Exact Timer reference, version, save count, and no-effect assertions pass | **COMPLIANT** |
| 5 | Callback persists before effect and repeats with the exact key at most as designed | Real callback proves one keyed degradation; generic durable-owner tests prove chain ordering/retry, but no repeated old timer callback asserts the same key | **PARTIAL** |
| 6 | Trust cancels pending timer; old callback is suppressed; later pending state has fresh identity | No deadline-origin runtime test performs Pending→trust cancellation, invokes the old callback, and verifies a fresh timer/version/key | **UNTESTED — CRITICAL** |
| 7 | Stop/Dispose cleanup and stale G1→G2 suppression | Stop/restart disposal cardinality and stale G1 rejection pass; direct Dispose with an armed deadline timer is not tested | **PARTIAL — UNTESTED DISPOSE SCENARIO** |
| 8 | Active Save/effect callback faults surface; restart converges with exact key; stale faults are suppressed | Generic durable/admission tests pass, but none originate from the deadline timer callback | **UNTESTED — CRITICAL** |
| 9 | Forward jump and rollback/unusable timing fail closed; unknown corruption remains rejected | Forward jump, rollback, `TimingValid=false`, and unknown-corruption focused cases pass | **COMPLIANT** |
| 10 | Actual callback degradation recovers after three trust observations and does not repeat | Actual timer theory degrades, restarts from persisted state, resolves once after three trusts, and remains one-shot | **COMPLIANT** |
| 11 | No collaborator I/O under lock; callback work is generation-owned | Source inspection and admitted-work lifecycle tests confirm lock-free effects and owned draining | **COMPLIANT** |

## Correctness

| Area | Judgment |
|---|---|
| Synthetic/elapsed race | The former callback-drop guard is gone. Both admissions are owned; gate serialization allows the synthetic early check to return and the elapsed callback to degrade afterward. The new race test passes. |
| Timing normalization | Pending rollback and invalid timing are normalized narrowly before validation and then fail closed. Non-timing corruption still fails validation. |
| Timer ownership | Timer, armed state, and version belong to one generation. Stop, phase transition, and drain invalidate the version and dispose the timer. |
| Restart/stale isolation | Old-generation admission is rejected before collaborator work; the new generation retains its timer. |
| Fault semantics | Source routes callback faults through owned admission and `EffectFault`, but required timer-origin Save/effect fault behavior lacks runtime proof. |

## Design Coherence

| Decision | Result |
|---|---|
| Single generation-owned one-shot timer | Conforms |
| Persisted UTC deadline remains authority | Conforms |
| OS timer callback enters admitted integrity/effect path | Conforms |
| No polling or second deadline owner | Conforms |
| Stale generation/version rejected before effects | Conforms by source and Stop→Start test |
| Durable fail-closed convergence | Conforms for successful and timing-defect paths; timer-origin fault convergence remains unproved |

## Strict-TDD Audit

- Original RED SHA-256: `1B2150B5E1899E51CF579E56C1A956196D7FA9FCC249F0B9618BC42B7FF85683`.
- First-remediation RED SHA-256: `8D22716AFB768B69D118D8E6FBC2EEF6F7B08A1F5F66EE88339AE928BF6B1091`.
- Final-remediation RED: `sdd7-c1c-final-remediation-red-20260826.log`, SHA-256 `3C2DC92BB4917199B51FDBF4FB058DF0F7A0616CD1C3F2E70FDA7B04A8A6F507`, size `6,129,036` bytes.
- Final RED chronology precedes the final production/test modification times.
- Captured final result is effectively **1/3 passed and 2/3 failed**: both `DeadlineOwner_TimingDefectsNormalizeOnlyPendingState` theory cases failed in the intended pre-fix run; `DeadlineOwner_ElapsedCallbackSurvivesQueuedSyntheticAdmission` did not fail.
- Therefore the timing-normalization change has genuine behavior-first RED evidence, while the callback-loss race does not. Missing timer-origin fault/trust/Dispose scenarios have neither RED nor GREEN tests.

## Issues

### CRITICAL

1. **Trust cancellation/fresh deadline identity has no passing covering test.** The required Pending→trust cancellation, old callback suppression, and later fresh timer/version/key sequence is absent.
2. **Deadline callback fault convergence has no passing covering test.** Active Save-before-effect and effect/progress-save faults are not induced from a real deadline callback, and no restart proves exact-key convergence from those timer-origin cuts.
3. **Strict-TDD evidence is incomplete.** The final callback-loss race case was already green in the final RED artifact; the mandatory trust/fault/Dispose scenarios were not captured RED at all.

### WARNING

1. Direct `Dispose()` cleanup with an armed deadline timer is not asserted; only Stop and later idempotent disposal are covered.
2. Real callback exact-key evidence checks non-null identity and one effect, but does not explicitly replay the expired/old callback and assert the identical key/no duplicate effect.

### SUGGESTION

1. Keep the deadline-specific lifecycle/fault scenarios under the `DeadlineOwner_` focus so future closure runs cannot pass using only generic admission/durable-chain coverage.

## Scope and Budget

- Tracked diff is exactly:
  - `src/ControlParental.Service/AntiTamperMonitor.cs`: `90 additions / 5 deletions`
  - `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`: `200 additions / 1 deletion`
- Exact CODE+TEST budget: **296/400**, compliant.
- Only the two authorized tracked files are modified; the staged set is empty.
- Both historical FAIL reports remain intact.

## Final Readiness

**FAIL. Task 4.3 MUST NOT be checked and the candidate MUST NOT be offered for commit authorization.** The implementation closes the callback race and timing-normalization defects and all fresh gates are green, but required trust cancellation/fresh-identity and timer-origin fault/restart scenarios still lack passing runtime coverage; Strict-TDD proof is also incomplete.

This verifier made no production/test edits and performed no stage, commit, push, PR, merge, rebase, or other history action.
