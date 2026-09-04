# Verification Report: PR1 Foundation Children

**Change**: `offline-sync-recovery`  
**Scope**: tasks 1.1A, 1.1B1, and 1.1B2 only  
**Mode**: Strict TDD / hybrid / direct project  
**Verification date**: 2026-08-18  
**Source/tests**: read-only; only this report was written

## Executive Result

| Child | Verdict | Summary |
|---|---|---|
| **1.1A** | **FAIL** | Domain fields/contracts exist and are covered through the combined suite, but the claimed child test allocation includes a shared fixture that requires the later `OutboxManager` constructor/SQLite activation. No independently buildable “contracts only, no production activation” child is demonstrated, and its test ownership was overstated by 64 B1 scenario lines. |
| **1.1B1** | **PASS WITH WARNINGS** | Conditional parameterized claim/complete/fail, shared-SQLite concurrency, stale guards, eligibility-before-limit, mixed outcomes, and exhaustion pass at runtime. The corrected real budget is 376 touched lines, not 290. |
| **1.1B2** | **FAIL** | Bounded 1000 recovery, file-backed reopen, contention bound, safe bridge success, and scheduler success integration pass. Required rollback execution and unauthorized/repeated requeue scenarios are absent; `MarkSentAsync` can transition any row, including dead-letter/stale states, because it has no state guard. |

**PR1 foundation verdict: FAIL.**  
**Full-change readiness: NOT READY.** Three of ten tasks are checked; task 1.2 and later work are intentionally pending and are not treated as defects in this slice.

## Completeness and Artifact State

| Check | Result |
|---|---|
| Filesystem task checkboxes | ✅ 3/10 checked: 1.1A, 1.1B1, 1.1B2 |
| `apply-progress.md` state | ✅ Same three checked; 1.2 and later pending |
| Engram task state | ✅ Same checkbox state |
| Budget metadata consistency | ⚠️ Inconsistent: filesystem apply reports 212/290/302; Engram task memory reports 230/284/302; Engram apply memory reports 212/290/302 |
| Prior failed reports preserved | ✅ `verify-report-unit1.md` and `verify-report-pr1a.md` unchanged |
| Invalid migration/snapshot | ✅ Absent on disk and absent from `HEAD`; task 1.2 remains pending |

## Commands and Runtime Results

All commands used finite external timeouts and existing restored assets. No restore/install, live backend, Windows matrix, branch/rebase/stash/reset, commit, or PR operation was performed.

| Command | Timeout | Result |
|---|---:|---|
| `dotnet test ... --filter FullyQualifiedName~OutboxManagerTests --no-restore --verbosity normal` | 300 s | ✅ 33 passed, 0 failed, 0 skipped |
| `dotnet test ... --filter FullyQualifiedName~ScheduledWorkService --no-build --no-restore` | 300 s | ✅ 89 passed |
| Exact `RealSchedulerManagerPath_UsesDurableLegacyBridgeForSuccess` | 300 s | ✅ 1 passed |
| `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore` | 300 s | ✅ 0 errors; NU1601 warning |
| Full Service regression, `--no-build --no-restore --verbosity normal` | 600 s | ✅ 1078 passed, 0 failed; runner skipped one unrelated duplicate-ID case |
| Focused XPlat coverage | 300 s | ✅ 33 passed; `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-sync-recovery-pr1-foundation-verify/73d9fceb-3b8d-48bb-a5f2-ce6ea0cdb49a/coverage.cobertura.xml` |

## Behavioral Compliance Matrix

| Child | Required behavior | Named runtime evidence | Result |
|---|---|---|---|
| 1.1A | Stable operation identity and deduplication | `EnqueueAsync_ValidPayload_EnqueuesEntry`; `EnqueueAsync_DuplicateDedupKey_IgnoresDuplicate`; replay tests | ✅ Behavior passes in combined implementation |
| 1.1A | Bounded/redacted state invariants without activation | Invalid-bound and redaction tests execute only with the activated B1 manager/fixture | ❌ No autonomous 1.1A proof |
| 1.1B1 | Actual shared-SQLite concurrent claimers | `ClaimAsync_ConcurrentClaimersReceiveDisjointRows` | ✅ COMPLIANT |
| 1.1B1 | Conditional row-count guarded claim | Concurrent loser result plus claimed-row assertions | ✅ COMPLIANT |
| 1.1B1 | Conditional complete/fail and stale complete/fail | `ClaimAsync_StaleCompletionAndFailureCannotMutateReclaimedRow` | ✅ COMPLIANT |
| 1.1B1 | Reclaim and generation increment | Stale/reclaim and expired-claim tests | ✅ COMPLIANT |
| 1.1B1 | Eligibility/order before `LIMIT` | `GetPendingEntriesAsync_FiltersEligibilityBeforeLimit`; ordering test | ✅ COMPLIANT |
| 1.1B1 | Mixed success/transient/permanent isolation | `Lifecycle_MixedSuccessTransientAndPermanentOutcomesRemainIsolated` | ✅ COMPLIANT |
| 1.1B1 | Exhaustion/dead-letter/redaction | Exhaustion and unsafe-diagnostic tests | ✅ COMPLIANT |
| 1.1B1 | SQL parameterization | Static inspection of EF interpolated APIs | ✅ COMPLIANT |
| 1.1B2 | Recovery page capped at 1000, leaving overflow | `RecoverExpiredClaims_IsBoundedBeforeUpdatingRows` with 1005 rows | ✅ COMPLIANT |
| 1.1B2 | File-backed dispose/reopen, stable ID, no duplicate completion | `FileBackedRestart_ReclaimsLeaseAndPreservesOperationId` | ✅ COMPLIANT |
| 1.1B2 | Cancellation without mutation | Claim and transition cancellation tests | ✅ Pre-cancel paths pass |
| 1.1B2 | Busy/contention bound | `ClaimAsync_WhenDatabaseIsLocked_FailsWithinBoundAndDoesNotMutate` | ✅ COMPLIANT for lock-before-BEGIN path |
| 1.1B2 | Explicit rollback after transaction begins | (none) | ❌ UNTESTED — recovery catch/rollback lines are uncovered |
| 1.1B2 | Authorized, audited, idempotent requeue | `RequeueDeadLetterAsync_PersistsSafeAuditAndPreservesIdentity` | ⚠️ PARTIAL — success/audit pass; unauthorized and repeated no-op paths have no current test |
| 1.1B2 | Safe legacy sent progression | Direct bridge tests and real scheduler success integration | ⚠️ PARTIAL — pending success becomes acknowledged, but SQL has no source-state guard and can acknowledge dead-letter/stale rows |
| 1.1B2 | Safe legacy failed progression | Direct bridge redaction/attempt tests | ✅ Pending/claimed guard and redacted error pass |
| 1.1B2 | Real scheduler-manager durable transition | `RealSchedulerManagerPath_UsesDurableLegacyBridgeForSuccess` | ✅ Success path passes; real failure path is not integrated |

## Concurrency, Boundedness, Complexity, and SQL Safety

- `ClaimAsync` uses `BEGIN IMMEDIATE`, filters eligibility before deterministic `created_at,id` ordering and `LIMIT`, caps pages at 1000, and performs at most one guarded update per selected row: O(page), at most 1001 SQL statements while holding the writer lock.
- Claim/complete/fail data values use `FromSqlInterpolated` or `ExecuteSqlInterpolatedAsync`; raw SQL is limited to static `BEGIN IMMEDIATE`, `COMMIT`, and `ROLLBACK`. No dynamic identifier/value concatenation was found.
- Shared-cache tests use two physical SQLite connections with `Default Timeout=1`; concurrent claimers and lock contention run against the same database.
- Recovery uses a deterministic subquery with `LIMIT 1000`; 1005-row evidence proves exactly 1000 transition and five remain claimed.
- Claim cancellation is proven only before transaction admission. The busy test proves finite lock failure/no mutation before `BEGIN IMMEDIATE` succeeds. No test forces an exception after a manual transaction has begun, so explicit rollback behavior is unproven.
- Failure diagnostics are allowlist-based (`temporary`, `permanent`, `network`, `timeout`, `cancelled`); all other values become `redacted-failure`.
- `MarkFailedAsync` restricts mutation to Pending/Claimed rows. `MarkSentAsync` has no status predicate, claim token, or row-count result and therefore permits invalid dead-letter/stale-to-acknowledged transitions.

## Test Organization and Duplicate-ID Audit

The three files compile into one partial class, `ControlParental.Service.Tests.OutboxManagerTests`, and share one fixture. Independent discovery found:

- **33 discovered OutboxManager test IDs**.
- **33 unique OutboxManager test IDs**.
- Focused execution printed and passed all 33, including every B1/B2 named scenario currently present.
- No required Outbox scenario was hidden by an xUnit discovery collision.

The full assembly separately reports one duplicate ID:

`HttpResponseClassifierTests.Classify_OtherCodes_DefaultToTransient(status: MultipleChoices)`

One copy is skipped by xUnit. It is unrelated to these children, but the apply statement “one duplicate test ID was skipped” is confirmed. List output also contains repeated display text for parameterized backend-identity cases, but the runner emitted no duplicate-ID warning for those.

The partial-class organization itself is valid, but it obscures child ownership: four B1 tests remain in `OutboxManagerTests.cs`, not `OutboxLifecycleCoreTests.cs`, and all child tests depend on fixture/helpers in the base partial.

## Changed-Scope Coverage

| Production file/symbol | Added executable lines covered | Line % | Changed branch evidence |
|---|---:|---:|---:|
| `IOutboxManager.cs` / `OutboxEntry` | 8/8 | 100% | N/A |
| `OutboxEntryStatus.cs` / `OutboxClaim` | 5/5 | 100% | N/A |
| `PolicyDbEntity.cs` | 8/9 | 88.89% | N/A |
| `ControlParentalDbContext.cs` | 11/11 | 100% | N/A |
| `OutboxManager.cs` | 122/128 | 95.31% | 48/50 (96.00%) |
| **Tracked production aggregate** | **154/161** | **95.65%** | **48/50 (96.00%)** |

Uncovered changed manager lines are recovery rollback lines 200–203 and unauthorized requeue lines 213–214. The one uncovered entity line is unrelated `ElapsedSeconds`. Excluding the unrelated `ElapsedSeconds` property and mapping gives best-effort foundation-attributable **153/159 lines = 96.23%** and **48/50 branches = 96.00%**. Whole-class `OutboxManager` remains **100% line / 81.81% branch** only when generated async class aggregation is viewed without restricting to newly added lines; the changed-line calculation above is the stricter result.

The >80% changed executable line gate passes, but coverage precisely confirms the missing B2 rollback and unauthorized-requeue scenarios.

## Independent Child Budget Reconciliation

Current distinct foundation diff:

| Component | Additions | Deletions | Touched |
|---|---:|---:|---:|
| Domain + DbContext + manager + tracked base tests | 410 | 67 | 477 |
| `OutboxLifecycleCoreTests.cs` | 109 | 0 | 109 |
| `OutboxRecoveryBridgeTests.cs` | 241 | 0 | 241 |
| **Raw distinct total** | **760** | **67** | **827** |

Eight additions are unrelated pre-existing `ElapsedSeconds` hunks (seven in `PolicyDbEntity.cs`, one DbContext mapping), leaving **819 required touched lines**.

The apply allocations total only 804 lines (212 + 290 + 302) and misassign/omit required code. Independent no-double-count reconciliation:

| Child | Gross/attributable touched | Adjustment and ownership | Budget result |
|---|---:|---|---|
| **1.1A** | 148 gross / **141 attributable** | Start from reported 212; remove 64 added B1 tests still located in base `OutboxManagerTests.cs`; remove seven unrelated domain lines. Includes shared fixture/helpers and prerequisite contract tests. | ✅ ≤400, but not autonomous without later constructor activation |
| **1.1B1** | **376 attributable** | Reported 290 + 64 B1 base-file test lines + 10 required DbContext model lines omitted from budget + 12 required shared manager additions omitted from manager hunk allocation. | ✅ ≤400, only 24-line headroom |
| **1.1B2** | **302 attributable** | Recovery/bridge manager hunks, 241-line recovery file, and legacy bridge test changes. | ✅ ≤400 |
| **Distinct required total** | **819** | 141 + 376 + 302; exactly reconciles required source/test scope with no double count. | ✅ Arithmetic closes |

All reconstructed sizes are below 400, but the current worktree contains no actual child branches/commits. More importantly, 1.1A's allocated partial-class fixture constructs the later factory/time-based `OutboxManager`; without B1 production constructor changes, the advertised “no activation” child is not independently buildable. Review-size arithmetic passes, while autonomous child readiness does not.

Current branch remains `fix/windows-runtime-foundations`; no actual feature-chain child diff was created or claimed by this verification.

## Strict TDD Audit

| Check | Result |
|---|---|
| Original compile RED chronology | ⚠️ Contemporaneously reported and specific missing symbols are listed; standalone raw test-only revision/output is not retained |
| Corrective RED chronology | ⚠️ Named observed failures are retained, but raw standalone count/output is incomplete |
| Current GREEN | ✅ 33/33 focused, 89/89 scheduler, exact integration 1/1, full regression 1078/1078 |
| Triangulation B1 | ✅ Adequate for required core behaviors |
| Triangulation B2 | ❌ Incomplete: rollback, unauthorized/repeated requeue, invalid MarkSent source-state transition, and real scheduler failure path are absent |
| Assertion quality | ✅ No tautologies, ghost loops, production-free assertions, or empty-only assertions found |

Missing raw historical RED count remains a warning rather than a standalone failure. It does not excuse the currently missing B2 runtime scenarios.

## Design and Progress Coherence

- Current tasks and implementation require durable compatibility bridge progression, while `design.md` line 19 still says Unit 1 bridge methods are fail-closed/non-mutating. The design is stale relative to tasks/code.
- Task checkboxes are synchronized across filesystem/apply/Engram, but child budget values are not synchronized and the published allocations do not reconcile all required hunks.
- Invalid migration artifacts are absent; no schema bootstrap exists; task 1.2 correctly remains pending.

## Issues Found

### CRITICAL

1. **1.1A is not demonstrated as an autonomous no-activation child**: its shared test fixture requires the later activated manager constructor/SQLite setup.
2. **1.1B2 has no executed rollback scenario** after a transaction begins; recovery catch/rollback lines are uncovered.
3. **1.1B2 lacks authorized-idempotent requeue triangulation**: unauthorized and repeated no-op paths are not present in the current 33 tests.
4. **Legacy `MarkSentAsync` is not state-guarded** and can acknowledge dead-letter or stale rows, violating safe lifecycle progression.

### WARNING

1. Published child budgets (212/290/302) omit/misassign 23 raw touched lines; independently reconciled budgets are 141/376/302 attributable.
2. Strict-TDD standalone RED output/count is incomplete, though chronology and current GREEN evidence are credible.
3. Real scheduler integration covers success only, not the durable failure bridge.
4. One unrelated full-suite duplicate test ID is skipped by xUnit.
5. `design.md` bridge semantics are stale relative to current tasks/code.
6. NU1601/NU1701 package warnings remain in build/focused output.

### SUGGESTION

1. Preserve child-specific test-only RED output and actual stacked diffs so review budgets and autonomous compilation can be verified directly rather than reconstructed from a polluted worktree.

## Final Verdicts

- **1.1A: FAIL** — size passes, but no autonomous contracts-only/no-activation child is proven.
- **1.1B1: PASS WITH WARNINGS** — behavior and corrected 376-line budget pass; provenance/allocation warnings remain.
- **1.1B2: FAIL** — required rollback/requeue scenarios and safe sent-state guard are incomplete.
- **PR1 foundation: FAIL**.
- **Full change: NOT READY** — task 1.2 and later tasks remain intentionally pending.

## Persistence

- OpenSpec report: `openspec/changes/offline-sync-recovery/verify-report-pr1-foundation.md`
- Engram topic: `sdd/offline-sync-recovery/verify/pr1-foundation`
- Session: `sdd6-offline-sync-recovery-20260818`
- Older failed reports remain unchanged.
