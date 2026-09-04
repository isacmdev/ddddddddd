# C1C Authoritative Independent Runtime Closure Verification

**Change:** `remote-signal-integrity` task 4.3 / C1C deadline owner  
**Branch / HEAD:** `feat/sdd7-4c2c1c-deadline-owner` / `e5f9fc2e7205bcb1822f6bd6722cca7ca35ef876`  
**Base:** `e5f9fc2e7205bcb1822f6bd6722cca7ca35ef876`  
**Mode:** Strict TDD, final test-only closure verification  
**Final verdict:** **FAIL**

## Executive Finding

The real-timer Save/effect fault blocker is closed. The trust/fresh-identity/Dispose blocker is **not** closed by the new test: its fixture reaches Pending again but does not create the required fresh timer. The assertions accept `secondTimer == null`, and isolated coverage proves only one timer allocation across the whole trust test. Consequently the stale-old-callback and direct-Dispose assertions operate against a null fresh-timer reference and do not prove preservation or cleanup of the required fresh timer.

## Prior Blocker Closure

| Prior blocker | Independent evidence | Result |
|---|---|---|
| Pending→trust cancellation, old callback suppression, fresh three-revoked timer identity, direct repeated Dispose | Trust test passes and proves Normal persistence, first-timer disposal, rejected old version, and repeated Dispose not throwing. However `secondTimer` is never asserted non-null. Isolated coverage has `ArmDeadline` entry line 336 hit twice but timer allocation line 343 hit only once, so the second arm returned before allocating. Final disposal count remains one—the first timer only. | **OPEN — CRITICAL** |
| Real timer Save-before-effect failure and keyed effect failure; observable fault; valid retryable store; fresh restart exact-key convergence | Both theory cases use `Timer.Change(Zero, Infinite)` on the generation-owned `System.Threading.Timer`, wait for the expected failing stage, observe `InvalidOperationException` through `StopAsync`, validate persisted/attempted state, create a fresh monitor/handler, and converge `CompletedReactionId` to the unchanged attempted key. | **CLOSED** |

## Fresh Command Evidence

| Gate | Result |
|---|---|
| Branch / HEAD / base | Correct: `feat/sdd7-4c2c1c-deadline-owner`, HEAD/base `e5f9fc2...` |
| Tracked names | Exactly `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` |
| Staged set | Empty |
| `git diff --check` | Exit 0 |
| Service build `--no-restore` | Exit 0; 0 errors; existing `NU1601` |
| Exact new trust/fault tests | **3/3 passed** |
| Trust test repeated independently | **5/5 separate runs passed** |
| All `DeadlineOwner_` cases | **14/14 passed** |
| Integrity handler deadline focus | **6/6 passed** |
| Relevant lifecycle/fault/admitted/stale focus | **16/16 passed** |
| Whole `AntiTamperMonitorTests` | **112/112 passed**, twice with coverage |
| Exact-exclusion safety net | **1313/1313 passed**; only the two authorized FQNs excluded; existing duplicate-ID notice |

Known package/analyzer warnings are pre-existing and non-blocking.

## Production Immutability

- Actual production SHA-256: `C15B7C55E0FBDCA8D1F61F71920539DC541E941AC338688333C4816ECECDB10E`.
- The supplied hash text (`...CDB10`) is a 63-character prefix missing the terminal `E`; it matches the actual hash prefix.
- Production mtime remains `2026-08-26T19:36:19.9263229-05:00`, predating the final test-only additions.
- Production diff remains exactly `90 additions / 5 deletions`; no new production edit is present.

## New Test Fixture and Assertion Audit

### Trust / fresh identity / Dispose

What the test genuinely proves:

- Loads a future persisted Pending state and starts with a real timer.
- One trust observation persists Normal and clears deadline fields.
- The first timer is nulled and its disposal count increases exactly once.
- The captured old version is rejected before admission; isolated coverage records line 304 but zero hits at line 305 for this test.
- Three revoked observations persist Pending with the original non-extended due/origin.
- Repeated `Dispose()` does not throw.

What it does **not** prove:

1. `secondTimer` at test line 1470 is not asserted non-null.
2. `secondTimer.Should().NotBeSameAs(firstTimer)` passes when `secondTimer` is null.
3. After the old callback, `DeadlineTimer.Should().BeSameAs(secondTimer)` also passes when both are null.
4. The final disposal count assertion remains `1`. Since the same generation already disposed the first timer, a genuinely allocated and then disposed fresh timer would require an additional disposal relative to that baseline.
5. Isolated trust-only coverage confirms the gap: `ArmDeadline` guard line 336 is hit twice, but allocation line 343 is hit once. The second arm attempt exits before creating a timer (the relevant guard at lines 337–338 is exercised).

This is an incomplete-precondition assertion pattern: the test passes while the required fresh resource does not exist. Under the Strict-TDD assertion-quality gate, it is **CRITICAL**, not merely cosmetic implementation-detail coupling.

### Real timer fault/restart theory

- No manual call to `AdmitDeadlineCheck` seeds either failure. Both cases force the existing real `System.Threading.Timer` due immediately.
- Save-fault case captures `LastAttempted` before the store rejects the save; persisted state remains valid Pending and no keyed effect can precede the failed save.
- Effect-fault case persists valid Degraded state with pending exact reaction key and no completed reaction.
- Both faults are owned and surface through Stop/drain.
- Restart constructs a fresh monitor and pure handler. Save failure retries from persisted Pending through a new immediate real timer; effect failure reconciles the persisted Degraded pending effect.
- In both cases the final `CompletedReactionId` equals the exact pre-restart attempted/pending key. No notification key is introduced, and inherited durable ordering/notification tests remain green.

**Judgment:** fixture/store/generation origin is valid; this blocker is closed.

## Requirements Matrix

| # | Requirement | Runtime judgment | Status |
|---:|---|---|---|
| 1 | Third revoked arms timer | Existing initial and boundary scenarios pass | **COMPLIANT** |
| 2 | Persisted future due; fourth/restart no extension | Persisted due, stable due/origin, and Stop→G2 tests pass | **COMPLIANT** |
| 3 | Exact/past actual OS timer callback | Real timer theory passes exact and past due | **COMPLIANT** |
| 4 | Synthetic before-due callback does not re-arm/resubtract | Exact Timer reference/version/save assertions pass | **COMPLIANT** |
| 5 | Save-before-effect and exact-key one-shot behavior | Real callback and fault theory plus durable ordering tests pass | **COMPLIANT** |
| 6 | Trust cancels old timer; fresh identity; old callback cannot cancel fresh timer | First cancellation and stale rejection pass; fresh timer does not exist in the new fixture | **UNTESTED / FAILING PRECONDITION** |
| 7 | Stop/Dispose cleanup and stale G1→G2 | Stop→G2 passes; direct Dispose runs, but not against the required fresh timer | **PARTIAL** |
| 8 | Active Save/effect faults observable; restart exact-key convergence | Both real-timer theory cases pass with valid retryable state | **COMPLIANT** |
| 9 | Forward jump; rollback/invalid timing normalization; unknown corruption rejection | All focused cases pass | **COMPLIANT** |
| 10 | Callback-origin three-trust recovery and no repeat | Actual callback degradation/recovery theory passes | **COMPLIANT** |
| 11 | No lock-held I/O, scope drift, or unowned callback work | Production hash unchanged; prior source audit plus lifecycle suites remain green | **COMPLIANT** |

## Coverage

Authoritative full-class portable-PDB artifact (second reproducibility run):

- Path: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1c-runtime-closure-rerun-20260826\60e095bc-1390-45fd-a0c5-ce6a22b43940\coverage.cobertura.xml`
- Size: `5,100,882` bytes
- SHA-256: `649D0F8F01DB00CD7B2E91E386CAC60923BAB8421AF5598A5F446CBF316CABFE`
- Changed executable lines: **79/79 = 100%**.
- Changed branch outcomes: **60/72 = 83.33%**.
- Partial changed branches: line 314 `3/6`; line 317 `2/4`; lines 319 and 322 `3/4`; line 326 `1/2`; line 336 `2/4`; lines 423 and 424 `1/2`.
- A second fresh full-class artifact independently produced the same **60/72** mapping. This differs from the apply claim of `63/72`, but remains above the required 80% threshold.

Focused new-test artifact:

- Path: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1c-runtime-focused-coverage-20260826\947e6400-f074-4c31-afe7-adf607cfc9ff\coverage.cobertura.xml`
- Size: `5,097,824` bytes
- SHA-256: `28EB87E4BB9E1B3A27FE0F3C0759E1999C876087B2F431BB1A230EAF85C99E1D`
- Semantic hits include deadline admission/decision (`304–326`), arming/cancellation (`336–352`), durable Save/phase transitions (`990–996`), and Dispose/drain (`1177–1185`).

Trust-only diagnostic artifact:

- Path: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1c-trust-only-coverage-20260826\4ebc7530-522f-44eb-80db-01e8e7ce5a73\coverage.cobertura.xml`
- SHA-256: `C207A8601C2EFA37CB01186ED58647F1A774118CD55518490E10FE0D9F4093F5`
- Decisive mapping: `ArmDeadline` entry line 336 = 2 hits; timer allocation line 343 = 1 hit; old-callback admission line 305 = 0 hits; Dispose lines 1177–1185 are hit.

Coverage is numerically acceptable, but the isolated semantic mapping exposes the missing fresh-timer precondition.

## Strict-TDD Verdict

| Evidence group | Judgment |
|---|---|
| Original deadline behavior | Recorded RED evidence previously audited |
| Timing normalization, forward jump, no-rearm | Genuine recorded RED evidence previously audited |
| Final trust/fault/Dispose tests | Honest GREEN triangulation; production hash is unchanged |
| Callback-loss race | Defect was independently specified before remediation, but the attempted final RED test was already GREEN after the production change; no retrospective RED is claimed |

**Chronology decision:** the callback-race chronology gap, considered alone, is **WARNING / non-blocking** rather than a new FAIL. The defect was independently documented before remediation, the current test executes the real synthetic-versus-elapsed interleaving, all behavior is now green, and the report does not invent a RED. Strict TDD compliance therefore remains partial but honestly bounded.

That chronology warning is not the reason for the overall FAIL. The overall FAIL is caused by the trust test's missing fresh-timer runtime precondition and therefore an unproved required scenario.

## Test Layer and Assertion Quality

- Layer: unit/component tests with real `System.Threading.Timer` and mocked external collaborators; no browser/E2E layer is relevant.
- Fault theory assertions are behaviorally meaningful and exercise production callbacks.
- **CRITICAL assertion-quality issue:** `DeadlineOwner_TrustFreshIdentityAndDirectDisposeRejectStaleCallbacks` accepts a null fresh timer and therefore passes without exercising the named preservation/Dispose behavior.

## Scope / Budget / Status

- Actual tracked numstat:
  - `src/ControlParental.Service/AntiTamperMonitor.cs`: `90 additions / 5 deletions`
  - `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`: `256 additions / 2 deletions`
- Actual CODE+TEST budget by the established additions+deletions calculation: **353/400**, compliant.
- This differs from the expected `346/400`; the authoritative Git numstat is `353/400`.
- Only the two authorized tracked files are modified; staged set is empty.
- Historical reports are preserved. This report is a new untracked artifact.

## Issues

### CRITICAL

1. The new trust test does not allocate or assert a non-null fresh timer after three revoked observations. Null-reference-compatible assertions make stale-callback preservation and fresh-timer direct Dispose pass without the required resource.

### WARNING

1. Callback-loss race chronology remains partial: independently specified before remediation, but not captured as a failing executable RED before the fix.
2. Fresh changed-branch mapping is reproducibly `60/72 = 83.33%`, not the claimed `63/72 = 87.50%`; the required threshold is still met.
3. The supplied production hash omitted its final hexadecimal character, and the expected budget understated authoritative numstat by seven lines.

## Final Readiness

**FAIL. Task 4.3 MUST NOT be checked and the candidate MUST NOT be offered for commit authorization.** The real-timer fault/restart blocker is closed, but trust-driven fresh timer identity and direct fresh-timer Dispose remain unproved because the new test passes with `secondTimer == null`.

Report path: `C:\Users\Usuario\orca\workspaces\control-parental-windows\sdd7-unit-4c2c1c-deadline-owner\c1c-independent-runtime-closure-report.md`

This verifier made no production/test edits and performed no stage, commit, push, PR, merge, rebase, or other history action.
