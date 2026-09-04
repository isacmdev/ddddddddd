# Verification Report: PR1A

**Change**: `offline-sync-recovery`  
**Scope**: revised task 1.1 / PR1A only  
**Mode**: Strict TDD / hybrid / direct project  
**Verification date**: 2026-08-18  
**Source/tests**: read-only; only this report was written

## Executive Result

**PR1A verdict: FAIL.** The corrected `OutboxManager` lifecycle is substantially stronger: 30 focused shared-SQLite tests pass, conditional claim/complete/fail writes work in the exercised races, focused changed executable coverage is above 80%, and the Service builds. PR1A is nevertheless not safe or review-ready because the active production scheduler still calls legacy methods that are now no-ops, so successful rows are never acknowledged and failed rows never record a durable outcome; expired-claim recovery is unbounded; the required crash scenario does not execute a real process/database restart; one independently executed full regression run exited non-zero; and the real required PR1A child scope is approximately 650 touched lines, not the reported corrective delta below 400.

**Full-change readiness: NOT READY.** Only task 1.1 is marked complete (1/8). Task 1.2 and Units 2–4 are intentionally pending and are not defects in this slice, but archive readiness is unavailable.

## Completeness

| Metric | Value | Verification result |
|---|---:|---|
| Full-change tasks | 8 | 1 complete; 7 intentionally pending |
| PR1A assigned tasks | 1 | 1.1 marked complete |
| PR1A independently proven complete | 0/1 | Blocking production-flow, boundedness, restart-evidence, regression, and child-boundary findings remain |
| Focused tests | 30 | 30 passed |

## Commands and Runtime Evidence

All commands used existing restored assets with finite external timeouts. No restore/install, live backend, Windows matrix, branch/rebase/stash/reset, commit, or PR operation was performed.

| Command | Timeout | Result |
|---|---:|---|
| `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --filter FullyQualifiedName~OutboxManagerTests --no-restore --verbosity normal` | 300 s | ✅ PASS — 30 passed, 0 failed, 0 skipped; 5 package warnings |
| `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity minimal` | 300 s | ✅ PASS — 0 errors, 1 NU1601 warning |
| `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-build --no-restore --verbosity quiet` | 600 s | ❌ FAIL — 1074 passed, 1 failed: `NamedPipeUIServerHostedAdapterTests.Listener_DoesNotReportReadyUntilPipeCreationSucceeds` |
| Isolated rerun of the failing test | 300 s | ✅ PASS — 1 passed |
| Sequential full regression rerun | 600 s | ✅ PASS — 1075 passed |
| Focused XPlat coverage, `--no-build --no-restore` | 300 s | ✅ PASS — 30 passed; `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-sync-recovery-pr1a-verify/ffeea2d9-f574-432e-b957-161c30dc25e8/coverage.cobertura.xml` |

The reruns indicate an unrelated flaky regression rather than a deterministic task 1.1 failure, but the independently requested regression did produce a non-zero command and therefore prevents a clean verification PASS.

## Behavioral and Spec Compliance Matrix

Only task 1.1-relevant portions are counted. Identity/coordinator, schema adoption, reconciliation, and trigger composition remain assigned to later tasks.

| Requirement / scenario | Named passing runtime test | Result | Notes |
|---|---|---|---|
| Durable accepted event and stable operation identity | `EnqueueAsync_DuplicateDedupKey_IgnoresDuplicate`; `ClaimAsync_IsBoundedAndClaimsOnlyEligibleEntries`; `CrashBeforeAcknowledgement_ReplaysSameOperationIdentity` | ✅ COMPLIANT for manager scope | Deduplication, stable ID, bounded claim, and replay identity pass. Identity gating remains Unit 2. |
| Mixed success/transient/permanent outcomes remain isolated | `Lifecycle_MixedSuccessTransientAndPermanentOutcomesRemainIsolated` | ✅ COMPLIANT | Acknowledged, pending, and dead-letter states are independently asserted; only the transient row remains pending. |
| Crash precedes acknowledgement | `CrashBeforeAcknowledgement_ReplaysSameOperationIdentity`; `ClaimAsync_StaleCompletionAndFailureCannotMutateReclaimedRow` | ⚠️ PARTIAL | Stable replay identity and one winning local completion pass, but the test keeps a shared in-memory database alive and does not stop/restart a process or reopen a file database. It does not prove the specified restart boundary or remote double-count behavior. |
| Retry exhaustion retains durable redacted evidence | `FailAsync_ExhaustionRetainsDurableDeadLetterAndAudit`; `FailAsync_IsPerEntryAndDeadLettersWithRedactedDiagnostics` | ✅ COMPLIANT | Exhaustion and permanent paths retain dead-letter state, safe code, and timestamp; unsafe diagnostics are redacted. |
| Explicit recovery requeues safely | `RequeueDeadLetterAsync_IsAuthorizedAndIdempotent`; `RequeueDeadLetterAsync_PersistsSafeAuditAndPreservesIdentity` | ✅ COMPLIANT for manager scope | Unauthorized request fails, first authorized transition succeeds, repeat is a safe no-op, operation identity and audit reference persist. |
| Cancellation does not mutate lifecycle state | `ClaimAsync_CancellationDoesNotMutateRows`; `CompletionAndFailureCancellationDoesNotMutateClaim` | ⚠️ PARTIAL | Pre-cancelled claim/complete/fail calls do not mutate rows. Cancellation while blocked on `BEGIN IMMEDIATE` or midway through a multi-row claim is not exercised. |

**Compliance summary**: 4/6 compliant; 2 partial; 0 currently failing named focused tests.

## Conditional SQL, Concurrency, and Safety Audit

| Dimension | Result | Evidence |
|---|---|---|
| Shared-SQLite concurrency | ✅ | Two physical shared-cache connections target the same in-memory database; concurrent claimers receive disjoint results (2 and 0). |
| Conditional claim row count | ✅ | `BEGIN IMMEDIATE`; eligible candidates are conditionally updated, and only `changed == 1` rows are returned. |
| Conditional completion | ✅ | One parameterized `UPDATE` includes ID, operation ID, generation, claimed state, and active lease; success requires row count 1. |
| Conditional failure | ✅ | Same token/lease guard and row-count semantics; stale failure after reclaim returns false. |
| Reclaim races | ✅ | Expiry, generation increment, stale completion, stale failure, and new completion are covered. |
| Eligibility/order before limit | ✅ with minor gap | SQL filters eligibility before `ORDER BY created_at,id LIMIT`; starvation regression passes. Equal-time ID tie ordering is not separately triangulated. |
| Mixed outcomes | ✅ | Success/transient/permanent three-way test passes. |
| Retry exhaustion/dead letter | ✅ | Attempt-two exhaustion with max two is durable and redacted. |
| Authorization/audit/redaction | ✅ | Exact authorization, allowlisted safe codes, default redaction, stable ID, and safe audit are covered. |
| SQL parameterization | ✅ | Data values use `FromSqlInterpolated` / `ExecuteSqlInterpolatedAsync`; raw SQL is limited to constant `BEGIN IMMEDIATE` and `COMMIT`. No dynamic identifier/value concatenation was found. |
| Claim boundedness | ✅ | Invalid bounds are no-ops; positive pages are capped at 1000. Complexity is O(page), with one candidate query plus up to 1000 guarded updates. |
| Pending read/count boundedness | ✅ | Reads/counts are capped at 1000. |
| Expired recovery boundedness | ❌ FAIL | `RecoverExpiredClaimsAsync` executes one `UPDATE` over every expired claimed row with no page/limit. This contradicts bounded-work requirements and apply-progress line 39. |
| Contention bound | ⚠️ PARTIAL | `BEGIN IMMEDIATE` serializes writers, but no explicit busy timeout/configurable contention bound or cancellation-while-waiting test is present. The write lock is held across selection and up to 1000 updates. |
| Transaction cleanup | ⚠️ PARTIAL | Normal commit is explicit. There is no explicit rollback/finally around the manual transaction; exceptional cleanup relies on context/connection disposal and is not triangulated during mid-claim failure. |

## Production Call-Flow Audit — Legacy Bridges

The no-op bridge is safe against deletion and raw-error persistence in isolation, but it **does not preserve current product behavior**.

Current production path in `ScheduledWorkService.ExecuteOutboxPushAsync`:

1. Line 420 calls `GetPendingEntriesAsync(100)`; it does not claim rows.
2. Successful backend batches call `MarkSentAsync` at lines 495, 514, 533, and 565.
3. Parse and push failures call `MarkFailedAsync` at lines 481 and 580.
4. `OutboxManager.MarkSentAsync` and `MarkFailedAsync` now return without mutation.

Consequences before Unit 2 exists:

- A remotely successful row remains `Pending` and is sent again on every eligible scheduler run.
- A failed or malformed row records no attempt, safe code, eligibility, or dead-letter progression.
- The scheduler may log/reset backoff as if work succeeded while durable state remains unchanged.
- The new `ClaimAsync` / `CompleteAsync` / `FailAsync` path is not used by active production delivery.

No test combines a real `ScheduledWorkService` with the real corrected `OutboxManager`; test search found legacy calls only in `OutboxManagerTests`. Passing focused/full suites therefore do not cover this regression. Deferring caller replacement to Unit 2 leaves PR1A behaviorally unsafe as an independently deliverable child.

## Strict TDD Audit

| Check | Result | Details |
|---|---|---|
| Cumulative evidence retained | ✅ | Prior failed report is preserved; corrected apply-progress appends the new cycle. |
| Safety baseline | ⚠️ Partial | Prior report independently proves 19 focused tests passed. External UTF-16 baseline file (66,568 bytes) preserves the pre-correction diff. |
| Corrective RED | ⚠️ Proven behaviorally, exact count unproven | New assertions clearly target prior observed defects, and the preserved prior source/report demonstrates those behaviors were red. However, the external baseline file contains a diff, not the claimed 20-failure test output; exact `20 failures` provenance cannot be independently reproduced from retained evidence. |
| GREEN | ✅ | Independent execution confirms 30/30. |
| Triangulation | ✅ with gaps | The 11 corrective tests cover the reported manager races/outcomes. Real process restart, bounded recovery, in-contention cancellation, and production scheduler integration remain absent. |
| Assertion quality | ✅ | No tautologies, ghost loops, production-free assertions, or empty-only assertions were found. |

**Test layer distribution**: 30 integration tests in one file using xUnit, EF Core, and real SQLite connections; no E2E tests in this slice.

## Changed-Scope Coverage

Coverage was independently generated from the 30 passing focused tests and intersected with current added executable lines against `HEAD`.

| Production file | Covered added executable lines | Line % | Changed branches |
|---|---:|---:|---:|
| `IOutboxManager.cs` | 8/8 | 100% | N/A |
| `OutboxEntryStatus.cs` | 5/5 | 100% | N/A |
| `PolicyDbEntity.cs` | 8/9 | 88.89% | N/A |
| `ControlParentalDbContext.cs` | 11/11 | 100% | N/A |
| `OutboxManager.cs` | 117/117 | 100% | 47/48 (97.92%) |
| **Tracked production aggregate** | **149/150** | **99.33%** | **47/48 (97.92%)** |

The one uncovered line is unrelated pre-existing dirty scope: `UsageTodayDbEntity.ElapsedSeconds`. Excluding that property and its covered DbContext mapping yields best-effort PR1A-attributable executable coverage of **148/148 lines (100%)** and **47/48 branches (97.92%)**. Whole-class `OutboxManager` coverage is independently confirmed at **100% line / 81.81% branch**. The >80% changed executable gate passes.

## Removed Migration/Snapshot Audit

- `src/ControlParental.Service/Migrations/20260818094500_OfflineSyncRecovery.cs` and `ControlParentalDbContextModelSnapshot.cs` are absent on disk.
- Neither path exists in `HEAD`; the prior failed report recorded them as untracked additions.
- Their removal therefore removes only failed-attempt artifacts and does not delete committed baseline migration history.
- No replacement bootstrap/schema adoption exists in PR1A, consistently leaving task 1.2 unchecked and pending.

## Real PR1A Diff and Feature Branch Chain Boundary

Required task 1.1 files currently differ from `HEAD` by:

| File | Additions | Deletions | Touched |
|---|---:|---:|---:|
| `IOutboxManager.cs` | 36 | 0 | 36 |
| `OutboxEntryStatus.cs` | 13 | 0 | 13 |
| `PolicyDbEntity.cs` | 16 | 0 | 16 |
| `ControlParentalDbContext.cs` | 11 | 0 | 11 |
| `OutboxManager.cs` | 168 | 41 | 209 |
| `OutboxManagerTests.cs` | 347 | 27 | 374 |
| **Current target total** | **591** | **68** | **659** |

The `ElapsedSeconds` property/comment and DbContext mapping are unrelated pre-existing hunks (9 additions). Removing only those known unrelated lines leaves the **real required PR1A code/test scope at approximately 582 additions + 68 deletions = 650 touched lines**. Contracts, status/claim types, durable entity fields, EF mappings, the complete lifecycle implementation, and all corresponding tests are prerequisites of PR1A and cannot be excluded merely because they originated in the failed Unit 1 attempt.

Therefore:

- Actual PR1A exceeds the 400-line child budget by approximately **250 lines**.
- No size exception is authorized.
- Current branch is `fix/windows-runtime-foundations`, not a PR1A child.
- Neither local nor remote `feature/tracker` exists.
- The broader worktree remains heavily polluted, so an isolatable child diff is not demonstrated.

The apply report's “corrective delta below 400” is not the review boundary required to deliver the feature. **PR readiness fails.** Required correction is a genuine reviewable split or smaller child boundaries containing all prerequisites, based on an established tracker branch; no branch operation was performed by verification.

## Issues Found

### CRITICAL

1. **Active production delivery is regressed**: `ScheduledWorkService` still calls no-op legacy sent/failed methods, so successes remain pending and failures receive no durable lifecycle outcome.
2. **Expired-claim recovery is unbounded**, contrary to the revised design/task and apply claim.
3. **The required crash/restart scenario is only a shared-memory/context replay**, not a process or file-database restart; full restart/no-double-effect behavior remains unproven.
4. **A required full regression command exited non-zero** (1074/1075), even though isolated and full reruns passed and indicate flakiness.
5. **The real PR1A child is approximately 650 touched lines**, exceeds 400 with no exception, is mixed into a polluted unrelated branch, and has no intended tracker ref.

### WARNING

1. Exact corrective RED count (`20 failures`) is not retained; available provenance contains the baseline diff and narrative, not RED runtime output.
2. Cancellation while waiting for SQLite contention or during a multi-row claim is untested; no explicit configurable busy timeout was found.
3. Manual `BEGIN IMMEDIATE` lacks explicit rollback/finally coverage under mid-operation failure.
4. Equal-created-time ID ordering is implemented but not separately triangulated.
5. NU1601/NU1701 package warnings remain.

### SUGGESTION

1. Preserve machine-readable RED output with test names and exit code for future Strict TDD audits.

## Final Verdict

### PR1A: **FAIL**

The manager-level correction passes its focused tests and coverage gate, but production behavior, bounded recovery, restart evidence, regression cleanliness, and the review boundary do not satisfy PR1A verification.

### Full change: **NOT READY**

Task 1.2 and Units 2–4 remain intentionally pending, and task 1.1 has blocking verification findings. Do not archive or claim a full-change PASS.

## Persistence

- OpenSpec report: `openspec/changes/offline-sync-recovery/verify-report-pr1a.md`
- Engram topic: `sdd/offline-sync-recovery/verify/pr1a`
- Session: `sdd6-offline-sync-recovery-20260818`
- Prior failed report remains unchanged at `verify-report-unit1.md`.
