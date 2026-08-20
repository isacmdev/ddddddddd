# SDD6 Task 4.1 — Sequential Branch Evidence Audit

**Change**: `offline-sync-recovery`  
**Scope**: task 4.1 only; evidence/docs-only work unit  
**Mode**: Strict TDD reconciliation; no new production RED  
**Artifact mode**: hybrid OpenSpec + Engram  
**Worktree**: `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-4-1`  
**Branch**: `feat/sdd6-4-1-branch-evidence`  
**Verdict**: **READY FOR INDEPENDENT RE-VERIFICATION — PASS WITH NARROW HISTORICAL WARNING**

## Boundary and result

The exact approved task-3.2 commit is `6b48044a1b07163480bdba60cd08c67dfea7ca6d`. HEAD and task-4.1 base are identical. No production or test file was changed. Task 4.2 remains open. The exhaustive raw manifest is in `task-4.1-numstat.txt`; task-1.2 reconstruction details are in `task-4.1-task1.2-boundary-audit.md`.

There are **9 formal child units through task 3.2**: 1.1A, 1.1B1, 1.1B2a, 1.1B2b, 1.2, 2.1, 2.2, 3.1, and 3.2. The four foundation children use the approved audited anchor `7b74a0a4b6430610b344cea0afa9da7493e1a084`. The user explicitly authorized a narrow historical exception for only task 1.2's unrecoverable foundation-terminal → first-pre-state link; it is recorded as a warning, not as fabricated child evidence.

### Exception contract and runtime support

The exception waives only that one historical predecessor link. It does not waive task-1.2's internal patch progression, final blob equality, runtime functionality, green tests, coverage, the ≤800 boundary, or any other child's exact evidence. The authoritative task-1.2 report records **41 focused tests passed**, **1,111/1,111** slice full regression, and **100% changed-scope lines and branches**; later integrated evidence records **1,156/1,156** full Service regression. These are preserved historical facts, not new execution. Task-1.2 whole-child no-double-count numstat is **UNKNOWN under the authorized historical boundary exception**, never 94, 76, 194, or another partial total.

## Child boundary table

| Child | Exact child / authority | Immediate parent | Branch/worktree evidence | CODE+TEST touched | DOC/EVIDENCE touched | Size exception | Approval |
|---|---|---|---|---:|---:|---|---|
| 1.1A | audited anchor `7b74a0a` | no child patch recoverable | approved foundation report | NOT CLAIMED | NOT CLAIMED | no claim | foundation accepted, warning |
| 1.1B1 | audited anchor `7b74a0a` | no child patch recoverable | approved foundation report | NOT CLAIMED | NOT CLAIMED | no claim | foundation accepted, warning |
| 1.1B2a | audited anchor `7b74a0a` | no child patch recoverable | approved foundation report | NOT CLAIMED | NOT CLAIMED | no claim | foundation accepted, warning |
| 1.1B2b | audited anchor `7b74a0a` | no child patch recoverable | approved foundation report | NOT CLAIMED | NOT CLAIMED | no claim | foundation accepted, warning |
| 1.2 | external remediation chain inspected; no child commit | missing foundation-terminal → first-pre-state link | `task-4.1-task1.2-boundary-audit.md` | UNKNOWN under authorized exception | UNKNOWN under authorized exception | **Warning only; narrowly authorized** |
| 2.1 | `0c671fa` | `8fdfb89` | task-2.1 branch/worktree report | 313 | 586 | No | PASS WITH WARNINGS |
| 2.2 | `7e0a173` | `0c671fa` | task-2.2 branch/worktree report | 561 | 726 | **Yes** |
| 3.1 | `3ef8731` | `7e0a173` | task-3.1 branch/worktree report | 625 | 413 | **Yes** |
| 3.2 | `6b48044` | `3ef8731` | task-3.2 branch/worktree report | 728 | 1167 | **Yes** |

For 2.1–3.2, each row is calculated from a fresh exhaustive `git diff --numstat` against the commit's exact Git parent. CODE+TEST and DOC/EVIDENCE totals reconcile to every classified source/test path exactly once. No row exceeds 800; 401–800 is explicitly the accepted `size:exception`. No generated, binary, package, lockfile, or later-task path appears in those child ranges. Task 1.2's whole-child no-double-count numstat is explicitly **UNKNOWN under the authorized historical boundary exception**, not a partial numeric claim.

## Sequential ancestry and immutability

- `7b74a0a` is the accepted foundation anchor; `8fdfb89` is the governance acceptance commit and its parent is `3315ffb`; `0c671fa` has exact parent `8fdfb89`.
- `0c671fa → 7e0a173 → 3ef8731 → 6b48044` is a continuous ancestry chain with no missing commit between approved child boundaries.
- `6b48044` is both the exact task-3.2 endpoint and task-4.1 start. The current branch points at that immutable object.
- External dirty worktrees are not used as authority. The authoritative evidence is committed Git objects plus the approved reports; immutable patch SHA-256, patch-id, tree IDs, and raw numstat are recorded in `task-4.1-numstat.txt`.
- Foundation per-unit patches/numstats remain covered by the explicit four-child exception. Task 1.2's sequential base link is unavailable and is covered only by the user's explicit narrow historical exception; no historical standalone RED or child allocation is fabricated.

## No-double-count reconciliation

| Child | CODE additions/deletions | TEST additions/deletions | CODE+TEST | DOC/EVIDENCE | Reconcile |
|---|---:|---:|---:|---:|---|
| 2.1 | 42/25 | 246/0 | 313 | 586 | PASS |
| 2.2 | 147/156 | 252/6 | 561 | 726 | PASS |
| 3.1 | 213/89 | 323/0 | 625 | 413 | PASS |
| 3.2 | 188/9 | 456/75 | 728 | 1167 | PASS |

The manifest retains every raw path and numstat row. Patch hashes are reproducibility metadata only and are never added to the Git numstat totals. The 3.2 committed range is the authoritative boundary; stale external final-patch/manifest hashes are historical superseded evidence only.

## Security, reliability, and design audit

| Concern | Evidence and result |
|---|---|
| Security/data exposure | Approved 2.1–3.2 reports find fixed/redacted transport, outbox, reconciliation, and backup diagnostics; no new raw secret/error path in the audited diffs. PASS WITH WARNINGS for inherited package/analyzer debt. |
| Complexity/bounds | Claim pages, scheduler admission, reconciliation worksets, leases, retries, backoff, and shutdown waits are finite in approved reports. No unbounded loop or later-task scope appears in the child diffs. PASS. |
| Concurrency/single-flight | 2.2 and 3.2 reports provide real shared-owner/non-overlap evidence; 3.1 provides semaphore and cancellation/reuse evidence. PASS WITH WARNINGS for the separately disclosed foundation provenance. |
| Restart/durability | Foundation/2.2 outbox replay and 3.1 file-backed checkpoint restart are approved; 3.2 lifecycle ordering is runtime-proven. PASS. |
| Cancellation/rollback | Approved reports cover caller/host cancellation, lease preservation, transactional rollback, and bounded shutdown. No new code is introduced by 4.1. PASS. |
| Branch/base evidence | Actual parent IDs and continuous ancestry are proven for 2.1–3.2; foundation has its explicit exception; task 1.2's one predecessor link is a warning under the user's narrow exception. PASS WITH WARNING. |
| Later scope/dependency churn | Fresh ranges contain no task 4.2, package, lockfile, generated, binary, or unrelated hidden path. PASS. |

## Strict TDD historical status

Task 4.1 is evidence-only. A new production RED would be fabricated and is intentionally not created. The historical record is reconciled honestly: foundation standalone RED and per-unit immutable boundaries are incomplete; task 1.2 Round 1 chronology is incomplete; task 3.2 final corrective RED is unrecoverable and controlled mutation is explicitly **not** relabeled as RED. Prior approved reports retain the compile/corrective RED chronology and all warnings. Status: **WARNING, not fabricated**.

## Validation performed

- CodeGraph was initialized in the real worktree and used for focused call-flow inspection; `.codegraph/` is removed before delivery.
- `git rev-parse --show-toplevel`, `git worktree list --porcelain`, branch/ref inspection, exact parent/tree inspection, and `git rev-list --ancestry-path` were run.
- Fresh `git diff --numstat`, `git diff --name-status`, `git diff --binary` SHA-256, and `git patch-id --stable` were run for each committed child range.
- `git diff --check 6b48044..HEAD` passed before the evidence edit; the final current-only `git diff --check` is required after editing.
- No product tests or broad regression were rerun: this unit changes only evidence/docs, and prior approved reports contain the relevant runtime gates.

## Files for task 4.1

- `task-4.1-numstat.txt` — deterministic raw manifest, classifications, corrected hashes, and all committed child numstat rows.
- `task-4.1-task1.2-boundary-audit.md` — exhaustive read-only reconstruction, exact missing link, explicit exception scope, and runtime support.
- `verify-report-task-4.1.md` — readable audit summary.
- `tasks.md` and `apply-progress.md` — hybrid completion state only; task 4.2 remains open.

The prior apply-created task-4.1 docs/evidence delta was **207 additions / 2 deletions = 209 touched lines**. The current artifact set is **272 additions / 2 deletions = 274 touched docs/evidence lines**, excluding the preserved independent FAIL report. This remediation remains docs/evidence-only; **CODE+TEST = 0**.

**Result**: **10/11 formal tasks after marking 4.1; 4.2 remains open. CODE+TEST for remediation evidence = 0. READY for independent re-verification with one explicit historical warning.**
