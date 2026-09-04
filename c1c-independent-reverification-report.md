# C1C Independent Remediation Re-verification

**Change:** `remote-signal-integrity` task 4.3 / C1C deadline owner  
**Branch / HEAD:** `feat/sdd7-4c2c1c-deadline-owner` / `e5f9fc2e7205bcb1822f6bd6722cca7ca35ef876`  
**Mode:** Strict TDD, full proposal/spec/design/tasks re-verification  
**Verdict:** **FAIL**

## CRITICAL / BLOCKER Findings

1. **The remediation still lacks runtime proof for most mandatory C1C lifecycle and convergence behavior.** There is no deadline-specific test for trust cancellation/old callback suppression/fresh identity, Stop or Dispose cleanup cardinality, stale G1→G2 callback, active Save/effect callback faults, stale rejected-callback collaborator suppression, restart exact-key convergence after callback failure, or recovery starting from an actual deadline callback. General C1B2 tests do not execute these deadline-owner branches.
2. **`DeadlineCallbackAdmitted` can drop the authoritative elapsed callback.** `AdmitDeadlineCheck` rejects every callback while the flag is true (`AntiTamperMonitor.cs:304`). If the real one-shot timer fires while a synthetic early callback is admitted or waiting on `Gate`, the elapsed callback returns and is never replayed; the synthetic callback can then preserve Pending, leaving an already-fired one-shot timer as the nominal owner. The flag is also reset outside `lockObject` in `RunDeadlineAsync`'s `finally` (`:329`). No race test covers synthetic-vs-elapsed, cancellation, fault, trust, Stop, or fresh-version interleavings.
3. **Required rollback normalization production code is entirely unexecuted.** Fresh coverage reports zero hits at `AntiTamperMonitor.cs:414-419`. The rollback test reaches the separate comparison at `:425` because the pure handler's real clock does not throw in that setup; it does not test the new `StaleState` normalization catch. Unusable persisted timing (`TimingValid=false`) remains rejected by `state.Validate()` rather than demonstrated to converge fail-closed, while unknown corruption behavior is not triangulated in a C1C test.
4. **Recovery proof does not satisfy the requested owner composition.** `DurableOwner_ActualRevokedAndTrustObservationsPersistBoundariesAndReset` manually overwrites the store to `Degraded` at test line 1239. It does not obtain Degraded through the deadline-owner callback, so it cannot prove callback degradation followed by three-trust one-shot `Resolve` and revoked reset.
5. **Restart/no-extension and callback-fault convergence remain untested.** The revoked-boundary test proves third/fourth due equality but does not inspect the timer/version, restart remaining delay, or a fresh deadline after trust. The actual timer theory covers only immediate zero-delay exact/past state; it does not prove a future relative OS countdown remains authoritative under wall freeze/rollback.

## Prior Finding Closure

| Prior FAIL finding | Current evidence | Result |
|---|---|---|
| Callback re-arms/resubtracts wall time | `RunDeadlineAsync` now returns on synthetic before-due; test preserves exact Timer reference/version | **CLOSED** |
| Mandatory real timer/lifecycle/fault scenarios absent | Exact/past real timer and forward-jump tests added; trust/stale/Stop/Dispose/fault/restart/recovery paths still absent | **PARTIAL — OPEN** |
| Fired timer discarded; disposal unproved | Timer reference is retained and disposal body `:354` is hit; no explicit disposal cardinality/resource assertions or stale/Stop/Dispose deadline test | **PARTIAL — OPEN** |
| Changed branch coverage 72.73% | Fresh changed outcomes are **54/66 = 81.82%** | **NUMERICALLY CLOSED**, required branches still unhit |
| Strict-TDD evidence only covered two narrow tests | Remediation RED adds genuine synthetic-preservation and forward-jump failures, but no RED/runtime evidence for remaining mandatory behavior | **PARTIAL — OPEN** |

## Requirement-by-Requirement Runtime Scrutiny

| # | Requirement | Evidence / judgment | Status |
|---:|---|---|---|
| 1 | Third revoked arms timer | Production arm path is hit and third/fourth due is stable, but no timer/version assertion from creation | **PARTIAL** |
| 2 | Original future delay; restart gains no time | Future and zero-delay arm branches are hit; restart remaining delay and future monotonic countdown are not tested | **UNTESTED** |
| 3 | Exact/past actual OS callback | `DeadlineOwner_ActualOneShotTimerDegradesAtOrAfterDue(0/-1)` waits on the real `System.Threading.Timer` | **COMPLIANT** |
| 4 | Synthetic before-due preserves exact timer/version; no re-arm | Exact reference/version/save/effect assertions pass; no wall-time resubtraction remains in callback | **COMPLIANT** |
| 5 | Save-before-effect exact key; repeat once | Real timer persists Degraded and invokes one keyed effect; existing chain tests prove ordering, but no repeated actual callback/race or crash cut | **PARTIAL** |
| 6 | Trust cancellation, old callback suppression, fresh identity | No deadline-owner runtime test | **UNTESTED** |
| 7 | Stop/Dispose, stale G1→G2, explicit cleanup | Disposal code is hit in aggregate; no deadline-specific cardinality/stale-generation/Stop/Dispose test | **UNTESTED** |
| 8 | Active fault observable; stale fault suppressed; restart convergence | No deadline callback Save/effect fault test | **UNTESTED** |
| 9 | Forward jump; rollback/unusable timing fail closed | Forward production clock-check path and rollback comparison pass; normalization catch and unusable timing remain unhit | **PARTIAL / FAILING EVIDENCE** |
| 10 | Callback-Degraded three-trust recovery; revoked reset | Inherited test starts recovery from manually assigned Degraded state | **UNTESTED** |
| 11 | No I/O under lock/unowned callback/scope/null-store regression | Source retains admitted task ownership and lock-free collaborator I/O; scope and regression suites pass | **COMPLIANT**, subject to callback-loss race above |

## Implementation Semantics

- `elapsed=true` from the real timer forces deadline admission using persisted due, so a fired monotonic timer is not extended by a frozen/backward wall clock. Synthetic admission uses current wall time and returns before due without re-arming.
- Duplicate effect admission is normally bounded by `DeadlineCallbackAdmitted`, `DeadlineVersion`, the generation gate, durable phase change, and exact-key effect flow. However, rejecting the one real elapsed callback while a synthetic admission is in flight loses the authoritative one-shot event; this race blocks closure.
- `SaveStateAsync` only arms/cancels on phase change. Fourth same-phase Pending and effect-progress saves do not replace the timer.
- Active faults should flow through the admitted owned task and lifecycle drain by source inspection, including after Degraded cancels the timer, but no deadline-specific runtime test proves this.
- Stale-state normalization is narrowly guarded after general `state.Validate()`, but its complete zero-hit body means neither its intended behavior nor its non-regression boundary is runtime-proven.

## Strict-TDD Audit

- Original RED: `sdd7-c1c-red-final-20260826.log`, SHA-256 `1B2150B5E1899E51CF579E56C1A956196D7FA9FCC249F0B9618BC42B7FF85683` — unchanged; remains partial as previously judged.
- Remediation RED: `sdd7-c1c-remediation-red-20260826.log`, SHA-256 `8D22716AFB768B69D118D8E6FBC2EEF6F7B08A1F5F66EE88339AE928BF6B1091`, size `6,096,604` bytes.
- Chronology: RED created `2026-08-26 19:15:21`, completed `19:16:08`; production modified `19:16:49`; tests modified `19:20:52`. This supports test-before-production for the captured remediation run, with later test additions/edits.
- Captured result: **1/3 passed, 2/3 failed**. Genuine behavioral REDs were (A) synthetic early callback replaced the Timer reference and (B) forward-jump path timed out. The third captured case was already green, so A/B/C cannot all be classified as genuine failures.
- The artifact does not contain explicit A-I labels. Current exact/past real-timer cases and other later additions are honest GREEN triangulation, not RED. D-I behavior categories such as trust/stale/fault/restart/recovery are not implemented as deadline-owner tests and cannot be credited from aggregate green counts.
- **Strict-TDD validity: PARTIAL / INSUFFICIENT for task completion.** Cumulative logs honestly prove the two remediated failures, but not all production changes or mandatory scenarios.

## Fresh Command Evidence

| Gate | Result |
|---|---|
| Branch/HEAD/status/staged/numstat audit | Correct branch/HEAD; staged set empty; exactly two tracked files |
| `git diff --check` | Exit 0 |
| Service build `--no-restore` | Exit 0; 0 errors; existing `NU1601` |
| Current `DeadlineOwner_` focus | **6/6 passed** |
| Six C1A handler deadline cases | **6/6 passed** |
| Relevant C1B2 lifecycle/fault/admitted/stale focus | **16/16 passed** |
| Whole `AntiTamperMonitorTests` | **104/104 passed** |
| Exact-exclusion safety net | **1305/1305 passed**; only the two authorized FQNs excluded; existing duplicate-ID notice |
| Portable-PDB/XPlat coverage | **104/104 passed**; external Cobertura generated |

Known package/analyzer warnings remain non-blocking. One verifier-only PowerShell coverage-mapping command had a syntax error and was immediately corrected; no product build/test gate failed.

## Changed Coverage Artifact and Mapping

- Artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1c-independent-reverify-20260826\2773f843-4e6f-43b4-bcb4-3791a0ae96eb\coverage.cobertura.xml`
- Size: `5,100,031` bytes
- SHA-256: `2785CA956AE61D6BFD2341F76F569A312EF3AD597E1AE8741C8C6CE4B30A62C9`
- Main class: **99.08% line / 85.78% branch**.
- Changed production: **77/83 executable lines = 92.77%**; **54/66 changed branch outcomes = 81.82%**.
- Required unhit changed lines: `AntiTamperMonitor.cs:414-419` (entire rollback `StaleState` normalization catch).
- Material partial changed branches: `RunDeadlineAsync :315 3/6, :318 2/4, :320 3/4, :323 3/4, :327 1/2`; `ArmDeadline :337 2/4`; rollback result guards `:428` and `:429` each `1/2`. `RunDeadlineAsync` method branch rate is only **60%**.
- Explicit hits exist for synthetic and elapsed admission, before/due paths, duplicate guard outcomes, future/zero delay arm, cancel with/without timer, actual disposal, forward-jump direction, phase-change arm/cancel, Stop/Dispose/drain. They do not establish the missing semantic scenarios listed above.

## Scope / Budget / Status

- Tracked diff is exactly:
  - `AntiTamperMonitor.cs`: `96 additions / 6 deletions`
  - `AntiTamperMonitorTests.cs`: `133 additions / 1 deletion`
- Exact CODE+TEST budget: **236/400**, compliant.
- Only the two authorized tracked files are modified. Existing `.codegraph/` and prior `c1c-independent-verification-report.md` remain untracked; this re-verification report is separately untracked.
- No Program, handler, Domain, schema, API, migration, retry, polling, framework, or second-owner change is present.

## Final Readiness

**FAIL. Task 4.3 MUST NOT be checked and the candidate MUST NOT be offered for commit authorization.** Numeric coverage and the original re-arm defect are closed, but the authoritative callback can still be lost in a synthetic/elapsed race, and mandatory trust/stale/lifecycle/fault/restart/recovery plus rollback-normalization runtime evidence remains absent.

This verifier made no production/test edits and performed no stage, commit, push, PR, merge, rebase, or other history action.
