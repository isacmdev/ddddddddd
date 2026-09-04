# Verification Report: PR1 Foundation Behavior Repartition

## Verification Report

**Change**: `offline-sync-recovery`  
**Scope**: children `1.1A`, `1.1B1`, and `1.1B2`  
**Mode**: Strict TDD / hybrid  
**Date**: 2026-08-18  
**Source and tests**: read-only; only this report was added

## Executive Verdict

The behavior-oriented repartition fixes the earlier assertion-quality and test-filtering defects. All three child suites execute independently with the expected counts, all required SDD6 test identities are unique, all focused behavior passes, and focused production coverage is strong.

The foundation nevertheless **FAILS the delivery gate**. The claimed sequential allocations `384 / 185 / 313` do not account for the current complete diff. They total 882 touched lines, while the current scoped source/test delta is 1,171 touched lines after excluding eight unrelated `ElapsedSeconds` lines and including the shared helper requested by the verification contract. In particular, the new B2 test file alone is 446 added lines, so B2 cannot be a ≤400-line child diff as currently materialized. No size exception exists.

| Child | Behavior | Assertions/discovery | Review budget | Child verdict |
|---|---|---|---|---|
| 1.1A | ✅ 13/13 focused; autonomous calls are limited to enqueue/pending/count/claim/constructor behavior | ✅ Clean; 13 unique Service cases plus 2 unique Domain contract cases | ❌ Claimed 384 is not an exhaustive current-diff allocation | **FAIL** |
| 1.1B1 | ✅ 4/4 focused completion/failure cases | ✅ Clean; 4 unique cases | ❌ Claimed 185 participates in a ledger that omits current changed lines | **FAIL** |
| 1.1B2 | ✅ 19/19 focused recovery/bridge cases | ✅ Clean; 19 unique cases | ❌ `OutboxRecoveryBridgeTests.cs` alone adds 446 lines; claimed 313 is impossible for the current child diff | **FAIL** |

**Foundation verdict**: **FAIL** — runtime correctness passes; mandatory no-exception review accounting fails.

## Completeness

| Metric | Value |
|---|---:|
| Tasks total | 10 |
| Tasks checked | 3 |
| Tasks unchecked | 7 |
| Foundation children behaviorally complete | 3/3 |
| Foundation children passing every gate | 0/3 |

Task 1.2 and tasks 2.1–4.2 remain intentionally pending. The full change is **NOT READY** and is not archive-ready.

## Runtime Evidence

All commands used finite tool timeouts and `--no-restore`; no install or restore command was run.

| Command/scope | Result |
|---|---|
| Domain project, `dotnet test ...ControlParental.Domain.Tests.csproj --no-restore` | ✅ 97 passed, 0 failed, 0 skipped |
| 1.1A filter, `FullyQualifiedName~OutboxManagerTests` | ✅ 13 passed, 0 failed, 0 skipped |
| 1.1B1 filter, `FullyQualifiedName~OutboxLifecycleCompletionTests` | ✅ 4 passed, 0 failed, 0 skipped |
| 1.1B2 filter, `FullyQualifiedName~OutboxRecoveryBridgeTests` | ✅ 19 passed, 0 failed, 0 skipped |
| Combined child filter | ✅ 36 passed, 0 failed, 0 skipped |
| Exact real scheduler-manager failure integration | ✅ 1 passed, 0 failed, 0 skipped |
| `FullyQualifiedName~ScheduledWorkService` | ✅ 89 passed, 0 failed, 0 skipped |
| Service build, `dotnet build ... --no-restore` | ✅ 0 errors; existing NU1601 warning |
| Full Service regression, `--no-restore --no-build` | ✅ 1081 passed, 0 failed, 0 skipped |
| Combined child XPlat coverage run | ✅ 36 passed |
| Domain-contract XPlat coverage run | ✅ 2 passed |

## Autonomous 1.1A Check

`OutboxManagerTests` is now a distinct sealed class. Its 13 focused cases call only:

- `EnqueueAsync`
- `GetPendingEntriesAsync`
- `GetPendingCountAsync`
- `EnqueueIntegrityNotificationAsync`
- `ClaimAsync`
- the `OutboxManager` constructor

The focused tests do not call `CompleteAsync`, `FailAsync`, `RecoverExpiredClaimsAsync`, `RequeueDeadLetterAsync`, `MarkSentAsync`, or `MarkFailedAsync`. They prove real SQLite enqueue/deduplication, stable identity/time, pending ordering, eligibility-before-limit, finite limits, concurrent disjoint claims, invalid bounds without mutation, and constructor guards. B1 and B2 use separate classes and reuse the A-owned fixture.

The Domain contract suite contains two record/DTO contract tests. The removed enum/input-only assertion remains absent.

## Behavioral Compliance

| Child | Required behavior | Runtime evidence | Result |
|---|---|---|---|
| 1.1A | Durable enqueue and stable identity | `EnqueueAsync_ValidPayload_EnqueuesEntry`; duplicate test | ✅ COMPLIANT |
| 1.1A | Eligibility/order before bounded limit | ordered, limit, eligibility-before-limit tests | ✅ COMPLIANT |
| 1.1A | Bounded/concurrent claim admission | bounded/eligible, invalid bounds, concurrent claimers | ✅ COMPLIANT |
| 1.1A | Constructor/shared SQLite seam | constructor guards plus real shared SQLite fixture | ✅ COMPLIANT |
| 1.1B1 | Conditional complete/fail and stale guards | reclaim-generation stale completion/failure test | ✅ COMPLIANT |
| 1.1B1 | Mixed outcomes remain isolated | mixed success/transient/permanent test | ✅ COMPLIANT |
| 1.1B1 | Exhaustion/dead-letter/redaction | exhaustion and per-entry redaction tests | ✅ COMPLIANT |
| 1.1B2 | Crash/restart replay | in-memory crash replay plus real file-backed reopen | ✅ COMPLIANT |
| 1.1B2 | Bounded recovery and rollback | 1000-row bound plus forced rollback/retry | ✅ COMPLIANT |
| 1.1B2 | Cancellation and busy bound | claim/transition cancellation and locked-database tests | ✅ COMPLIANT |
| 1.1B2 | Authorized audited idempotent requeue | unauthorized and repeated-authorized tests | ✅ COMPLIANT |
| 1.1B2 | Durable safe legacy bridge | success/failure/nonexistent/invalid-source tests | ✅ COMPLIANT |
| 1.1B2 | Existing scheduler-manager path | real manager failure progression and redaction | ✅ COMPLIANT |

The broader spec remains incomplete by design: identity admission/coordinator ownership, schema adoption, reconciliation, backup composition, and final evidence belong to unchecked later tasks.

## Assertion Quality

Files audited:

- `tests/ControlParental.Domain.Tests/OutboxContractsTests.cs`
- `tests/ControlParental.Service.Tests/OutboxManagerTests.cs`
- `tests/ControlParental.Service.Tests/OutboxLifecycleCompletionTests.cs`
- `tests/ControlParental.Service.Tests/OutboxRecoveryBridgeTests.cs`
- `tests/ControlParental.Service.Tests/OutboxLifecycleTestSupport.cs`

**Result**: ✅ No tautologies, assertions over test inputs, assertion-free production paths, ghost loops, or orphan empty-result checks were found. `Assert.All` in the bounded-claim test is preceded by `Assert.Equal(2, claims.Count)`, so the loop cannot pass vacuously. No-throw tests invoke real production methods.

## Test Discovery Uniqueness

| Filter | Discovered | Duplicate display groups |
|---|---:|---:|
| `OutboxManagerTests` | 13 | 0 |
| `OutboxLifecycleCompletionTests` | 4 | 0 |
| `OutboxRecoveryBridgeTests` | 19 | 0 |
| Combined Service children | 36 | 0 |
| `OutboxContractsTests` | 2 | 0 |

All required SDD6 foundation test identities are unique. The previously observed unrelated Service-suite HTTP enum-alias duplication is outside these filters and does not hide an SDD6 case.

## Coverage

Fresh reports:

- `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-sync-recovery-pr1-foundation-pass-coverage/cdbf8343-5e71-4439-84b9-0b61ba122847/coverage.cobertura.xml`
- `C:/Users/Usuario/AppData/Local/Temp/opencode/offline-sync-recovery-pr1-foundation-pass-domain-coverage/3b7d6ea3-e138-46b5-822b-1d75db3bf247/coverage.cobertura.xml`

| Changed production symbol/file | Line | Branch | Rating |
|---|---:|---:|---|
| `OutboxManager.cs` | 100% | 81.81% | ✅ |
| `ControlParentalDbContext.cs` whole class | 94.69% | 100% | ✅ |
| `OutboxEntry` in combined child run | 100% | 100% | ✅ |
| `OutboxClaim` in combined child run | 100% | 100% | ✅ |
| `OutboxDbEntity` in combined child run | 100% | 100% | ✅ |

The Domain-only two-test report is intentionally narrower (`OutboxEntry` 50%, `OutboxClaim` 100%, `OutboxDbEntity` 0%); the real SQLite A/B suites execute the entity and bring all three changed symbols to 100% line/branch in the combined report.

## Independent Review-Budget Recalculation

### Current complete diff evidence

Tracked numstat:

| File | Add | Delete | Touched |
|---|---:|---:|---:|
| `IOutboxManager.cs` | 36 | 0 | 36 |
| `OutboxEntryStatus.cs` | 13 | 0 | 13 |
| `PolicyDbEntity.cs` | 16 | 0 | 16 |
| `ControlParentalDbContext.cs` | 11 | 0 | 11 |
| `OutboxManager.cs` | 182 | 41 | 223 |
| `OutboxManagerTests.cs` | 86 | 135 | 221 |

Untracked/new files count every line as an addition:

| File | Add | Touched |
|---|---:|---:|
| `OutboxContractsTests.cs` | 44 | 44 |
| `OutboxLifecycleTestSupport.cs` | 72 | 72 |
| `OutboxLifecycleCompletionTests.cs` | 76 | 76 |
| `OutboxRecoveryBridgeTests.cs` | 446 | 446 |
| `TrackingDbContextFactory.cs` | 21 | 21 |

Raw total: **1,179 touched lines**. Eight `ElapsedSeconds` lines are unrelated to SDD6 (seven in `PolicyDbEntity.cs`, one in `ControlParentalDbContext.cs`), producing a conservative SDD6 total of **1,171 touched lines**.

The claimed child ledger is:

| Child | Claimed |
|---|---:|
| 1.1A | 384 |
| 1.1B1 | 185 |
| 1.1B2 | 313 |
| **Claimed total** | **882** |

The ledger therefore omits **289 scoped touched lines**. Even if `TrackingDbContextFactory.cs` were proven to predate this SDD6 slice, the ledger would still omit **268 lines**.

There are two independently decisive inconsistencies:

1. The manager allocations in `apply-progress.md` are 75 + 82 + 30 = **187**, but `OutboxManager.cs` numstat is **223**, leaving 36 manager touched lines unassigned despite the statement that every hunk was assigned once.
2. B2 claims 283 test lines, but the B2-owned untracked `OutboxRecoveryBridgeTests.cs` is **446 added lines** before counting any production hunk. A new file cannot be reduced to 283 review lines by logical method ownership.

Because there are no actual child branches/commits or immutable per-child patches, no alternate sequential baseline proves that these additions already existed in an earlier reviewed child. Pre-seeding B2's test file in A/B1 would also violate the stated behavior ownership and must itself be counted there. Therefore the exact `384 / 185 / 313` claim is not verified, B2 exceeds 400, and the no-size-exception review gate fails.

## Strict TDD Evidence

| Check | Result | Evidence |
|---|---|---|
| TDD table exists | ✅ | Child-specific table in `apply-progress.md` |
| Test files exist | ✅ | All named files present |
| GREEN is current | ✅ | 13/4/19 and combined 36 pass |
| Triangulation | ✅ | Positive/negative, race, stale, mixed, bound, restart, cancellation, busy, rollback, authorization variants |
| Assertion quality | ✅ | Prior non-behavioral assertion removed; audit clean |
| Immutable corrective RED ordering | ⚠️ | Historical chronology exists, but no independently immutable test-only revision |

Raw artifact identity remains stable:

| Artifact | SHA-256 |
|---|---|
| `sdd6-pr1a-red.txt` | `3007be567bae01e21b19107f3686d5abde89e0ef442da941629b00a28e6520c2` |
| `sdd6-offline-sync-recovery-20260818-b2-red.log` | `59ce4327e32742256ecdee2c5bb0d36982e952f877a162b21e7b6f4f3347ea9c` |
| `sdd6-offline-sync-recovery-20260818-b2-red-runtime.log` | `d10dae8408855daef8df4d40af1b75d1982df86e53f45464118af350563980ad` |

The RED limitation is a **WARNING only** under the configured contract and is not the reason for failure.

## Artifact and Migration Consistency

- `tasks.md`, `apply-progress.md`, and Engram task/progress memories agree on ownership, 3/10 completion, later pending work, and the claimed 384/185/313 ledger.
- They are inconsistent with current numstat/new-file evidence on review-budget completeness.
- `src/ControlParental.Service/Migrations/` contains no files; the invalid migration and partial snapshot remain absent.
- Historical failed verification reports remain unchanged.
- No branch, commit, reset, stash, install, restore, push, PR, live backend, or unsupported Windows-runtime claim was made.

## Issues

### CRITICAL

1. The claimed sequential review ledger omits at least 268 scoped touched lines (289 when the requested shared helper is included).
2. B2's owned new test file is 446 lines by itself, exceeding the mandatory 400-line child budget before production changes. No size exception exists.
3. Seven tasks remain unchecked; the full change is not ready.

### WARNING

1. Corrective RED chronology is credible but not independently immutable.
2. Actual child diffs cannot be reconstructed from real branches/commits because the work remains in a polluted monolithic worktree.

### SUGGESTION

1. Split the B2 test file into another autonomous review child and preserve real per-child patches/commits so the next verification can use actual sequential numstat rather than a logical ownership ledger.

## Final Status

**Behavioral foundation**: **PASS WITH WARNINGS**  
**Review/delivery foundation**: **FAIL**  
**Overall foundation**: **FAIL**  
**Full change**: **NOT READY** (`3/10` tasks complete)
