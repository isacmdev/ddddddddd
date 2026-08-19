# Design: Offline Sync Recovery

## Technical Approach

Keep Service-owned SQLite durability, with `BackendClient` limited to transport attempts and one `ScheduledWorkService` owner for admission, retry, backoff, cancellation, and single-flight execution. Identity remains fail-closed through `IBackendIdentityCoordinator`.

## Architecture Decisions

| Decision | Choice | Tradeoff / rationale |
|---|---|---|
| Schema evolution | **B: versioned, idempotent SQLite schema adoption** before hosted work. Empty databases are created from the complete EF model; legacy `EnsureCreated` databases are upgraded transactionally. A `schema_version` row, validation, backfill, and named indexes make reruns safe. | A (complete EF baseline + `Migrate`) is cleaner, but the repository has no baseline migration, production/tests call `EnsureCreated`, the current migration cannot create `outbox`, and the snapshot is partial. Adopting A now would strand fresh or existing databases. EF model/snapshot remain generation/documentation artifacts; runtime availability does not depend on them until regenerated consistently. |
| Concurrency | SQLite `BEGIN IMMEDIATE` and raw conditional `UPDATE`s; return rows only when `changes=1`. | Slightly more SQL, but select-then-tracked-update cannot protect reclaim races. |
| Retry ownership | Coordinator records one durable attempt/backoff; transport may perform only its bounded request retry. | Prevents retry amplification and overlapping triggers. |

## Durable Lifecycle and SQL Semantics

`Pending`, `Claimed`, `Acknowledged`, and `DeadLetter` retain `OperationId=DedupKey`, `ClaimVersion`, lease, eligibility, attempts, safe failure code, dead-letter time, and audit lineage. Candidate SQL applies eligibility **before** `ORDER BY created_at,id LIMIT @limit`: pending rows with `next_eligible_at IS NULL OR <= @now`, plus expired claims. Each row is claimed by `UPDATE outbox SET status='Claimed', claim_version=claim_version+1, claimed_until=@lease, attempts=attempts+1 WHERE id=@id AND ((status='Pending' AND eligible) OR (status='Claimed' AND claimed_until<=@now))`; concurrent losers observe zero changes.

Completion and failure use one conditional write containing `id`, `operation_id`, `claim_version`, `status='Claimed'`, and `claimed_until>@now`. Ack becomes `Acknowledged`; cleanup is separate. Transient failure becomes eligible `Pending`, while permanent failure or `attempts >= max` becomes `DeadLetter`. A stale ack or failure changes nothing. Cancellation stops admission and does not fail the row; lease expiry permits guarded reclaim. Requeue requires authorization, conditionally changes only `DeadLetter` to `Pending`, preserves operation identity, and writes an append-only audit reference; repeated requests are safe no-ops. Raw errors and legacy deletion are forbidden: the Unit 1 compatibility bridge may durably progress only safe pending legacy rows, while Unit 2 removes it and all callers when coordinator wiring changes.

## File Changes and Tests

Modify `ControlParentalDbContext.cs`, `OutboxManager.cs`, `IOutboxManager.cs`, `OutboxEntryStatus.cs`, `PolicyDbEntity.cs`, and `Program.cs`; add `SqliteSchemaBootstrapper.cs` and `ControlParentalSchemaVersion`. Regenerate/remove the partial `Migrations/*` artifacts only when a complete model snapshot is available. Later modify `ScheduledWorkService.cs`, `BackendClient.cs`, `IBackendClient.cs`, `TaskSchedulerBackupService.cs`, and reconciliation files for ownership boundaries.

`OutboxManagerTests.cs` must execute bootstrap against empty and legacy files and assert schema version, types, constraints, indexes, stable-ID backfill, and no secret columns. Runtime scenarios must cover concurrent claim/reclaim/ack/fail, eligibility starvation, mixed success/transient/permanent outcomes, crash/restart replay without double count, stale failure, exhaustion, cancellation, redacted diagnostics, authorized audited requeue, and legacy no-delete/no-raw-error behavior.

## Migration / Rollout / Rollback

After ACL hardening and before hosted services, bootstrap uses an exclusive transaction: create/complete the schema, add compatible columns, backfill `operation_id=dedup_key` and safe defaults, validate shape, create indexes, then advance `schema_version`. Ambiguous schemas fail closed. Roll out adoption first, then coordinator callers. Rollback disables new admission but preserves rows, dead letters, and schema; the upgrader remains rerunnable.

No user-visible requirement changes; proposal/spec update is not blocked. Tasks **1.1 and 1.2 require revision**: 1.1 adds the missing lifecycle/race scenarios and legacy guard; 1.2 replaces the partial EF migration with bootstrap/adoption and fresh/legacy runtime tests.
