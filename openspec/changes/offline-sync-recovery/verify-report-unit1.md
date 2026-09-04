# Verification Report

**Change**: `offline-sync-recovery`  
**Scope**: Work Unit 1 only — tasks 1.1–1.2  
**Version**: N/A  
**Mode**: Strict TDD / hybrid artifact store / direct project  
**Verification date**: 2026-08-18  
**Source and tests**: read-only; only this report was written

## Executive Result

**Unit 1 verdict: FAIL.** The focused suite, Service regression, and Service build pass, and best-effort attributable tracked executable-line coverage is above 80%. However, required migration paths are not executed by tests and the migration/snapshot are materially inconsistent; claim/ack atomicity is not proven under concurrency; several required crash, mixed-outcome, exhaustion, audit, and cancellation dimensions have no complete runtime scenario coverage; and the 576-line PR1 slice is neither within the 400-line child budget nor isolatable from the current polluted branch/worktree.

**Full-change archive readiness: NOT READY.** Tasks 2.1–4.2 are intentionally pending (6/8). They are not Unit 1 defects, but the full change cannot be verified or archived.

## Completeness

| Metric | Value | Verification result |
|---|---:|---|
| Full-change tasks | 8 | 2 marked complete; 6 intentionally pending |
| Unit 1 tasks | 2 | 2/2 marked complete |
| Unit 1 tasks independently proven complete | 0/2 | Both have blocking gaps: lifecycle/concurrency scenarios (1.1) and migration execution/consistency (1.2) |
| Unit 1 focused tests | 19 | 19 passed; 14 pre-existing + 5 added |

Units 2–4 were excluded from the slice verdict except where their intentional absence limits a full spec scenario. No pending Unit 2–4 checkbox is reported as a Unit 1 defect.

## Commands and Runtime Results

All commands used existing restored assets only. No restore/install, live backend, Windows matrix, branch, commit, PR, or process-lifecycle operation was performed. Tool-enforced external timeouts were finite.

| Command | Timeout | Result |
|---|---:|---|
| `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --filter FullyQualifiedName~OutboxManagerTests --no-restore --verbosity normal` | 300 s | PASS — 19 passed, 0 failed, 0 skipped; build included; 5 package compatibility/version warnings |
| `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity minimal` | 300 s | PASS — 0 errors, 1 NU1601 warning |
| `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-build --no-restore --verbosity quiet` | 600 s | PASS — 1064 passed, 0 failed, 0 skipped |
| `dotnet test ... --filter FullyQualifiedName~OutboxManagerTests --no-build --no-restore --collect:"XPlat Code Coverage" --results-directory <approved-temp> --verbosity quiet` | 300 s | PASS — 19 passed; Cobertura generated at `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-sync-recovery-unit1-coverage/7fb5f660-aa91-48fa-8d0f-1cbc00b6df80/coverage.cobertura.xml` |

Package warnings observed: NU1601 for `supabase-csharp` and `RichardSzalay.MockHttp`; NU1701 for legacy .NET Framework package compatibility. They did not fail this run.

## Work Unit 1 Spec Compliance Matrix

A scenario is compliant only when a named covering test passed at runtime. Partial rows passed but do not prove every clause.

| Requirement / scenario | Named runtime test | Result | Evidence gap |
|---|---|---|---|
| Identity-gated durable delivery — ready identity admits an event (durable/stable-identity portion assigned to Unit 1) | `EnqueueAsync_ValidPayload_EnqueuesEntry`; `ClaimAsync_IsBoundedAndClaimsOnlyEligibleEntries` | ⚠️ PARTIAL | Durable insertion and a non-empty operation ID after claim pass, but identity admission is Unit 2 and stable identity-before-dispatch is not asserted in the enqueue test. |
| Per-entry crash-safe outbox — mixed batch outcomes remain isolated | `FailAsync_IsPerEntryAndDeadLettersWithRedactedDiagnostics` | ⚠️ PARTIAL | Only permanent + transient outcomes are used; no successful third entry, no conditional acknowledgement in the mixed batch, and no assertion that an unrelated entry remains normally eligible. |
| Per-entry crash-safe outbox — crash precedes acknowledgement | `ClaimAsync_ExpiredClaimIsRecoveredAndStaleCompletionIsRejected` | ⚠️ PARTIAL | Expiry and stale completion pass, but no process/context restart at this crash window, same-operation-ID replay assertion, remote-acceptance simulation, or no-double-count assertion exists. |
| Exhausted work — retry exhaustion retains evidence | (none) | ❌ UNTESTED | The only dead-letter runtime path uses `permanent: true`; the finite-attempt exhaustion branch is not tested. |
| Exhausted work — explicit recovery requeues safely | `RequeueDeadLetterAsync_IsAuthorizedAndIdempotent` | ⚠️ PARTIAL | Authorization, stable resulting state, and operation ID pass; `AuditReference`, normal retry ownership, and migration persistence are not asserted. The second call returns `false`, so only state idempotence is shown. |
| Diagnostics safe by default — redacted durable failure evidence (Unit 1 portion) | `FailAsync_IsPerEntryAndDeadLettersWithRedactedDiagnostics`; `OutboxLifecycle_SurvivesContextRestartWithoutSecrets` | ⚠️ PARTIAL | Safe failure-code redaction and current-model column names pass. Cancellation is untested, raw payload sensitivity is not audited, and migration-created schema is not exercised. |

**Compliance summary**: 0/6 fully compliant; 5 partial; 1 untested. Scheduler, missing-identity, remote-outage, reconciliation, and repeated-trigger scenarios remain assigned to Units 2–3 and are not counted as Unit 1 defects.

## Correctness and Concurrency Audit

| Dimension | Result | Evidence |
|---|---|---|
| Bounded claim pages | ✅ Static + runtime partial | `ClaimAsync` caps at 1000 and the two-of-three test passes. Invalid limit/lease branches and pages >1000 are not tested. |
| Eligible selection | ⚠️ PARTIAL | Filtering occurs after `Take(limit)`. Ineligible low-ID rows can consume the page and defer later eligible rows; no test places an ineligible row ahead of an eligible row. |
| Expired claim recovery | ✅ Runtime for one row | Named expiry test passed; recovery is capped at 1000. Concurrent recovery is untested. |
| Conditional claim | ❌ Not proven atomic | Claim performs query then tracked updates in a transaction, but there is no concurrent-claimer test and no conditional SQL update/row-version predicate. Two claimers are not proven to receive disjoint rows or a deterministic loser result. |
| Conditional ack/fail and stale rejection | ❌ Not proven atomic | The methods first select by token/status, then save an update by entity key. The predicate is not part of the final SQL update, so a recovery/reclaim race between select and save is not protected. Only sequential stale completion is tested; stale failure is untested. |
| Mixed per-entry outcomes | ⚠️ PARTIAL | Two failure classes pass; success + transient + permanent isolation is not triangulated. |
| Crash before ack / no silent loss | ⚠️ PARTIAL | Lease recovery is shown sequentially. The active coordinator still has callers of legacy `MarkSentAsync`, which deletes rows; replacement by conditional durable acknowledgement is deferred to Unit 2. |
| Dead-letter on permanent outcome | ✅ Runtime | Permanent failure is retained and redacted. |
| Dead-letter on exhaustion | ❌ UNTESTED | No runtime coverage of `Attempts >= maxAttempts`. |
| Explicit idempotent requeue | ⚠️ PARTIAL | Authorized transition and stable operation ID pass; audit lineage is written statically but not asserted. |
| Stable operation IDs | ✅ Runtime partial | Enqueue derives `OperationId` from `DedupKey`; restart/requeue tests preserve it. Migration backfill is untested. |
| Cancellation | ❌ UNTESTED | Tokens are forwarded to EF calls, but no cancelled claim/recovery/ack/fail test passed. |
| Redaction | ⚠️ PARTIAL | Token/password/authorization/body failure text is replaced. Empty/sanitized/truncation branches are incompletely triangulated, and legacy `MarkFailedAsync` still persists raw error text. |
| No silent loss | ❌ Not proven | Required concurrent/crash transition coverage is absent, and legacy deletion remains reachable pending Unit 2. |

## Migration and Snapshot Audit

`dotnet-ef` is unavailable. That does not excuse runtime migration coverage: tests could call EF migration APIs directly, but none do.

| Dimension | Result | Exact evidence |
|---|---|---|
| Fresh-database migration | ❌ UNPROVEN / structurally invalid | The migration directory contains only this migration and the snapshot. `Up` begins with `AddColumn(..., "outbox")`; it never creates the base schema. Applying the migration set to an empty database therefore has no migration that creates `outbox`. |
| Prior-schema upgrade | ❌ UNTESTED | No test creates the old schema and calls `Migrate`/`MigrateAsync`; backfill SQL (`operation_id = dedup_key`) is never executed. |
| Current-model fresh schema | ✅ Runtime, but not migration evidence | Test setup calls `Database.EnsureCreated()`, which creates the current model directly and bypasses migrations/history. |
| Migration discovery metadata | ❌ UNPROVEN | No generated `.Designer.cs` exists and no `[Migration]` / `[DbContext]` attributes appear in the only migration partial. Standard EF discovery was not executed. |
| Model/snapshot consistency | ❌ FAIL | The snapshot contains only `OutboxDbEntity`, while `ControlParentalDbContext` maps all application entities. It is not a full context snapshot. |
| `audit_reference` consistency | ❌ FAIL | Migration declares `audit_reference` as `DateTimeOffset`; entity/model/snapshot declare `AuditReference` as `string`. |
| Length constraints | ❌ FAIL / unproven | Migration specifies 256/80 max lengths for operation/failure codes; `OnModelCreating` and snapshot do not configure these max lengths. SQLite enforcement is not tested. |
| Index consistency | ⚠️ PARTIAL | Current model creates lifecycle/operation indexes; migration uses explicit lowercase names; snapshot assigns those names, but `OnModelCreating` does not. No `PRAGMA index_list/index_info` test executes migration or asserts names/columns. |
| Pending defaults | ⚠️ PARTIAL | Current CLR/model defaults produce pending rows, but no database-default test performs raw SQL insert, and the migration default is not executed. |
| Existing-row stable ID backfill | ❌ UNTESTED | SQL exists statically; no old row is migrated and verified. |
| No secret columns | ⚠️ PARTIAL | `PRAGMA table_info(outbox)` passes against `EnsureCreated` current schema and rejects column names `token`/`secret`; migration schema is not tested, and sensitive payload content is outside that assertion. |
| Generated artifact coverage | ➖ Excluded from >80% gate | Migration 0/21 executable lines; snapshot 0/28. This confirms they were compiled but not executed by focused tests; generated/schema artifacts are reported separately from attributable production-method coverage. |

## Strict TDD Audit

| Check | Result | Details |
|---|---|---|
| TDD evidence table present | ✅ | `apply-progress.md` contains rows for 1.1 and 1.2. |
| Baseline | ⚠️ Partially validated | Current diff adds five tests to a 14-test HEAD file, matching 14 → 19 statically. The historical 14-pass runtime was not independently reproducible without altering the dirty tree; only the apply report records it. |
| RED | ⚠️ Unproven historical ordering | The five test additions exist, but no preserved test-only revision/log proves the stated compile-failing RED run or that production followed it. |
| GREEN | ✅ | Current focused execution is 19/19. |
| TRIANGULATE | ❌ Inadequate | Five added tests do not cover all named task dimensions. Migration execution, concurrent claim/ack races, success+transient+permanent mixed outcomes, retry exhaustion, cancellation, audit persistence, and full crash replay are absent. |
| Safety net | ⚠️ Report-only | The source file was modified, and `14/14` is reported, but no independently retained baseline runtime artifact is available. |
| Task test files exist | ✅ | 2/2 task rows point to the existing SQLite test file. |

**TDD compliance**: current GREEN is confirmed; the baseline/RED sequence is not independently proven, and triangulation materially contradicts the broad apply-progress claims.

### Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit | 0 | 0 | xUnit available |
| Integration (SQLite in-memory, real EF provider) | 19 | 1 | xUnit + EF Core SQLite |
| E2E | 0 | 0 | Not used/required for this slice |
| **Total** | **19** | **1** | |

### Assertion Quality

**Assertion quality**: ✅ No tautologies, ghost loops, production-free assertions, or empty-only assertions were found. `Assert.All` is preceded by an exact count, and the empty-database assertion has non-empty companion tests.

## Changed-Scope Coverage

Coverage was collected from the passing 19-test focused suite and intersected with added executable line numbers from the current `git diff` against `HEAD`.

| Tracked production file | Added executable lines hit | Line coverage | Uncovered added executable lines |
|---|---:|---:|---|
| `IOutboxManager.cs` | 8/8 | 100% | — |
| `OutboxEntryStatus.cs` | 0/5 | 0% | 32–36 (`OutboxClaim`, not instantiated) |
| `PolicyDbEntity.cs` | 8/9 | 88.89% | 161 (`ElapsedSeconds`; pre-existing unrelated dirty scope) |
| `ControlParentalDbContext.cs` | 11/11 | 100% | — |
| `OutboxManager.cs` | 161/166 | 96.99% | 145–146, 177, 218–219 |
| **Tracked diff aggregate** | **188/199** | **94.47%** | **11 lines** |

Changed tracked branches: **54/70 = 77.14%**. The report does not invent branch evidence for migration/snapshot.

The clearly unrelated pre-existing `ElapsedSeconds` property and mapping are excluded from the best-effort Unit 1 attribution, yielding **187/197 = 94.92% executable-line coverage**, above the strict >80% line gate. This is a best-effort result, not a fully provenance-backed figure: the apply evidence says assigned files were already dirty but does not preserve a pre-apply patch, so exact attribution of every line is impossible. Generated migration/snapshot lines are excluded from the executable-line threshold and audited separately above.

## Design Coherence

| Decision | Result | Notes |
|---|---|---|
| SQLite-owned durable lifecycle | ⚠️ Partial | States/leases/versions exist and sequential tests pass; migration path is not deployable/proven. |
| Durable ack before cleanup | ⚠️ Partial | New `Acknowledged` transition exists, but legacy `MarkSentAsync` still deletes and remains called pending Unit 2. |
| Conditional stale-token protection | ❌ Deviation | Predicate is used for the read, not guaranteed on the final update; no concurrency proof. |
| Bounded page work | ✅ With warning | 1000 caps exist; post-`Take` eligibility filtering can starve eligible later rows. |
| Dead-letter retention/redaction/requeue audit | ⚠️ Partial | Permanent retention/redaction pass; exhaustion and audit persistence do not. |
| Full EF migration/snapshot | ❌ Deviation | Snapshot is partial and schema types/configuration diverge from migration/model. |
| Stable idempotency identity | ✅ Partial | Runtime enqueue/requeue path preserves identity; migration backfill is untested. |

## Diff and Feature Branch Chain Boundary Audit

Current Unit 1-related tracked numstat:

- Six tracked files: **451 additions + 44 deletions = 495 touched lines**.
- New migration: **38 additions**.
- New snapshot: **43 additions**.
- PR1 candidate total: **532 additions + 44 deletions = 576 touched lines** across eight production/test artifacts.

This exceeds the 400-line child-review budget by **176 lines**. The full working tree is much more polluted: `git diff --stat` reports **109 tracked files, 6,945 additions, 2,263 deletions**, plus many untracked files. Current branch is `fix/windows-runtime-foundations`; neither local nor remote `feature/tracker` exists, despite the planned PR1 base. The assigned files also contain unrelated pre-existing `ElapsedSeconds` edits. Consequently, no focused/isolatable PR1 diff or valid feature-branch-chain boundary can be proven.

**Required correction before review**: establish the intended tracker base, isolate only Unit 1 code/tests/schema artifacts into a clean child diff, remove unrelated pre-existing hunks from that child, and either split the 576-line slice below 400 touched lines or record an explicit review-budget exception. Then rerun migration, concurrency, focused/regression, and coverage verification. No branch/rebase/stash/reset/commit action was performed here.

## Dirty-Work Preservation Audit

The apply report and session summary state that no reset/stash/overwrite occurred and that unrelated edits were preserved. Current diff confirms unrelated changes still exist, including unrelated hunks inside assigned files. However, there is no pre-apply patch/hash or file snapshot with which to prove byte-for-byte non-overwrite. Therefore: **no overwrite is observed from available evidence, but preservation provenance is insufficient for a definitive claim**.

## Issues Found

### CRITICAL

1. **Migration paths are not verified and the migration set cannot create a fresh database**: tests use `EnsureCreated`; no baseline migration creates `outbox`; no old-schema upgrade/backfill test runs.
2. **Migration/model/snapshot are inconsistent**: partial snapshot; `audit_reference` DateTimeOffset-vs-string mismatch; length/index configuration divergence; discovery metadata is unproven.
3. **Atomic concurrency is not proven and the implementation uses select-then-update transitions**: no concurrent claimers, concurrent recovery/ack, or conditional final-update runtime evidence exists.
4. **Required Unit 1 scenarios lack complete passing coverage**: three-way mixed outcome, full crash-before-ack replay/no-double-count, retry exhaustion, stale failure, cancellation, and requeue audit persistence.
5. **Strict-TDD triangulation claim is not supported**: only five new tests exist, and task 1.2's claimed migration tests do not execute a migration.
6. **Feature Branch Chain boundary fails**: the candidate slice is 576 touched lines, mixed with unrelated dirty work, on an unrelated branch with no tracker ref; it is not a focused/isolatable PR1 child.

### WARNING

1. Changed tracked branch coverage is 77.14%; executable-line coverage is above 80%, but exact attribution is limited by missing pre-apply provenance.
2. Eligibility filtering after `Take(limit)` can defer later eligible rows behind ineligible low-ID rows.
3. Legacy `MarkSentAsync` deletion and raw `MarkFailedAsync` diagnostics remain reachable until Unit 2 replaces coordinator usage.
4. `OutboxClaim` and its overload are unexercised; the interface currently returns `OutboxEntry`, not the designed immutable claim contract.
5. Package resolution/compatibility warnings remain in focused/build output.
6. No-overwrite provenance is insufficient for a definitive preservation claim.

### SUGGESTION

1. Retain machine-readable baseline/RED/GREEN logs or revisions for future Strict TDD verification so historical ordering can be independently audited.

## Final Verdict

### Unit 1: **FAIL**

Passing current tests/build and >80% best-effort changed executable-line coverage do not compensate for unexecuted/inconsistent migrations, missing concurrency atomicity proof, incomplete required scenario coverage, and a non-reviewable chain boundary.

### Full change: **NOT READY**

Units 2–4 remain intentionally pending, and Unit 1 has blocking findings. Do not archive or issue a full-change PASS.

## Persistence

- OpenSpec partial report: `openspec/changes/offline-sync-recovery/verify-report-unit1.md`
- Intended Engram topic: `sdd/offline-sync-recovery/verify/work-unit-1`
- Session: `sdd6-offline-sync-recovery-20260818`
- This filename is slice-specific and does not overwrite a future full-change `verify-report.md`.
