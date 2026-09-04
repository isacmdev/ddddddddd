# C1C Independent SDD Verification

**Change:** `remote-signal-integrity` task 4.3 / C1C deadline owner  
**Branch / HEAD:** `feat/sdd7-4c2c1c-deadline-owner` / `e5f9fc2e7205bcb1822f6bd6722cca7ca35ef876`  
**Mode:** Strict TDD, full proposal/spec/design/tasks verification  
**Verdict:** **FAIL**

## CRITICAL / BLOCKER Findings

1. **The callback explicitly re-subtracts wall time and replaces the authoritative timer.** `RunDeadlineAsync` handles a synthetic/early callback by calling `ArmDeadline` (`AntiTamperMonitor.cs:326`); `ArmDeadline` reads wall time again and calculates `DeadlineDueUtc - now` (`:340,344`) before creating a replacement timer (`:346`). This directly contradicts design line 32: calculate one relative delay, then do not resubtract wall time when the callback runs. It also violates the supplied contract that an early callback preserves Pending while the already-owned timer remains authoritative. A wall rollback/freeze that remains above `MaxWallClockSeenUtc` can therefore add time after the original monotonic countdown expires.
2. **Mandatory C1C runtime scenarios are absent.** The two new tests invoke private `AdmitDeadlineCheck` by reflection; neither waits for the actual `System.Threading.Timer` callback. There is no runtime proof for actual at/past-due OS callback, third-revoked timer creation callback, restart remaining delay, trust cancellation plus old callback, stale G1→G2 deadline callback, Stop/Dispose timer disposal, active/stale callback fault handling, forward jump, invalid timing, or deadline-specific retry/restart convergence. Per the SDD verification rule, unexecuted required scenarios are `UNTESTED` and critical.
3. **Deadline cleanup is not proved and the fired timer reference is discarded without disposal.** Admission assigns `DeadlineTimer = null` (`:305-306`) before owned work. The fired timer is then unavailable to Stop/Dispose/drain; the disposal body at `:354-356` had zero coverage hits. This does not prove lifecycle-owned timer cleanup and can leave cleanup to GC.
4. **Useful changed-code branch coverage fails the required threshold.** Fresh portable-PDB coverage mapped 74 changed executable production lines: **71/74 = 95.95% lines**, but only **48/66 = 72.73% branch outcomes**. `RunDeadlineAsync` is only **58.33% branch-covered** and `ArmDeadline` **62.5%**. High aggregate class coverage (98.17% line / 83.51% branch) cannot substitute for these missing mandatory outcomes.
5. **Strict-TDD evidence is insufficient for the claimed behavior set.** The RED artifact is authentic and chronological for two test names only, but it contains no recorded command and proves no RED for the remaining mandatory C1C scenarios. The first failure is a `NullReferenceException` while reflecting absent `DeadlineTimer`, before its before/due/repeat assertions execute. The rollback test fails with pre-existing `IntegrityEscalationStateException: The escalation state is from the future`; it does not assert the error taxonomy and does not independently prove timer no-extension. The test title therefore overstates the behavior reached in RED.

## Completeness

| Dimension | Result |
|---|---|
| Proposal/spec correctness | **FAIL** — monotonic/no-resubtraction contract is violated |
| Design coherence | **FAIL** — callback replacement contradicts design lines 32 and 53 |
| Task 4.3 completeness | **FAIL** — required callback/lifecycle/fault branches are untested |
| Runtime gates | **PASS** for existing selected suites, but insufficient scenario coverage |
| Changed coverage | **FAIL** — useful branch outcomes 72.73%, below >80% |
| Scope/budget | **PASS** |

## Requirement-by-Requirement Runtime Scrutiny

| # | Requirement | Evidence / judgment | Status |
|---:|---|---|---|
| 1 | Third revoked creates Pending and arms timer | Existing owner test reaches Pending; coverage hits `SaveStateAsync:990`, but no assertion or actual callback from that creation path | **UNTESTED** |
| 2 | Future delay is original `due-current`; restart gains no time | Future arm branch is hit, but delay is not observed and restart remaining time is not tested; callback can recalculate it | **FAILING** |
| 3 | Exact/past due actual OS callback | New test uses reflection and manually changes mocked wall time; zero-delay arm branch `:344` is uncovered | **UNTESTED** |
| 4 | Before-due no effect and no re-arm/resubtraction | No-effect assertion passes, but production calls `ArmDeadline` and replaces the timer | **FAILING** |
| 5 | At/after save-before-effect exact key; repeat once | Synthetic reflected path degrades once; inherited chain tests cover ordering, but no actual timer callback or crash cut | **PARTIAL / UNTESTED** |
| 6 | Trust cancellation, old callback suppression, fresh identity | Source version gate exists; no deadline-specific runtime test | **UNTESTED** |
| 7 | Stop/Dispose, stale G1→G2, resource disposal | General lifecycle tests pass; deadline disposal body has 0 hits and no stale deadline callback is run | **UNTESTED** |
| 8 | Active fault observable, stale fault suppressed, convergence | General C1B2 fault/retry tests pass; deadline callback fault paths are not executed | **UNTESTED** |
| 9 | Forward jump; rollback/invalid fail closed, no extension | Startup rollback test passes; forward jump and invalid timing absent; callback wall re-arm permits extension | **FAILING** |
| 10 | Owner-level three-trust recovery; revoked reset | Existing owner composition test passes and handler deadline tests pass; no new duplicate handler-only scenario | **COMPLIANT** |
| 11 | No lock-held I/O/fire-and-forget; null-store/legacy preserved | Source keeps collaborator I/O outside `lock`; admitted task faults are owned; C1B2 and class suites pass | **COMPLIANT** |

## Strict-TDD Audit

- RED artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1c-red-final-20260826.log`
- Verified SHA-256: `1B2150B5E1899E51CF579E56C1A956196D7FA9FCC249F0B9618BC42B7FF85683` (matches report).
- Artifact timestamps: created `2026-08-26 18:53:04`, completed `18:54:17`; production file modified `18:58:11`, test file `18:59:11`. This supports a two-test RED before production, but the artifact does not preserve the exact command and later test edits are not separately ledgered.
- RED result: 0/2 passed. Failure 1: absent timer seam reached as reflection `NullReferenceException`; later deadline assertions were not reached. Failure 2: existing future/stale-state exception surfaced during lifecycle drain. It is a valid pressure test for startup rollback handling, not proof of all rollback/no-extension branches.
- **Validity:** **PARTIAL / INSUFFICIENT**. Test-first evidence exists only for two narrow tests, not for each claimed production behavior required by strict TDD.

## Fresh Command Evidence

| Gate | Result |
|---|---|
| `git diff --check` | Exit 0 |
| Service build `--no-restore` | Exit 0; 0 errors; existing `NU1601` |
| New `DeadlineOwner_` focus | **2/2 passed** |
| Six existing handler deadline tests | **6/6 passed** |
| Relevant C1B2 lifecycle/fault/admitted/stale focus | **16/16 passed** |
| Whole `AntiTamperMonitorTests` | **100/100 passed** |
| Exact-exclusion safety net | **1301/1301 passed**; only the two authorized host-dependent FQNs excluded; existing duplicate-ID notice |
| Portable-PDB/XPlat coverage run | **100/100 passed**; artifact generated outside workspace |

Known package/analyzer warnings remain non-blocking. The first attempted handler and lifecycle filter forms matched zero tests; corrected exact FQN filters then passed 6/6 and 16/16. No new relevant failing test/build command occurred.

## Coverage Artifact and Mapping

- Artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1c-independent-verify-20260826\6b305623-be55-433b-b24b-1029da39f04c\coverage.cobertura.xml`
- Size: `5,098,917` bytes
- SHA-256: `DFADE8744E751F0C4F75248703D2FE3CBD8093EC3F1F58625B21D8174D1AFC7D`
- Changed executable lines: **71/74 (95.95%)**; changed branch outcomes: **48/66 (72.73%)**.
- Unhit changed executable lines: `AntiTamperMonitor.cs:354-356` (actual timer disposal body).
- Material partial branches: `RunDeadlineAsync :316 3/6, :319 2/4, :321 2/4, :324 3/4, :326 3/4, :329 1/2`; `ArmDeadline :339 2/4, :344 1/2`; `CancelDeadlineLocked :353 1/2`. Actual zero-delay/past-due arm, timer disposal, inactive/state/non-handler/stale/fault outcomes remain absent.
- Runtime-hit classification: synthetic due/non-due/repeat and future arm were hit; actual OS callback, due/past zero-delay callback, cancel/dispose of a live timer, stale generation/version callback after replacement, and deadline-specific fault outcomes were not proved.

## Scope / Budget / Status

- HEAD matches required base/current commit; branch matches request.
- Tracked diff is exactly:
  - `AntiTamperMonitor.cs`: `85 additions / 5 deletions`
  - `AntiTamperMonitorTests.cs`: `59 additions / 1 deletion`
- Exact CODE+TEST budget: **150 changed lines**, within hard `<=400` and requested `180–300` estimate is advisory rather than the hard gate.
- Staged set: empty. No Program, handler, schema, API, type, migration, retry, framework, polling, second-owner, or planning-file change.
- Untracked before this report: `.codegraph/`. This report is the sole verifier-created child-root file.

## Final Readiness

**FAIL. Task 4.3 MUST NOT be checked, and this candidate MUST NOT be offered for commit authorization.** The implementation must remove callback-time wall-clock re-arming/resubtraction and obtain real runtime evidence for all mandatory timer, lifecycle, stale, fault, restart, forward/rollback, and recovery branches with >80% useful changed-code coverage.

This verifier made no production/test edits and performed no stage, commit, push, PR, merge, rebase, or other history action.
