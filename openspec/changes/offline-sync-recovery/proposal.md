# Proposal: Offline Sync Recovery

Outcome: offline events recover predictably across transient failures and restarts, with no silent loss and no dependency on a live backend during development or verification.

## Intent

Harden the existing SDD6 pipeline for T18, T20, and T10-B: authenticated REST/outbox recovery, scheduled work with bounded backoff and idempotency, and restart reconciliation/checkpoints. The current seams exist, but static code and local tests do not prove per-entry recovery, crash-window behavior, cancellation, or non-overlapping restart continuation. After bounded retries, an undeliverable event MUST enter a durable dead-letter state, retain safe/redacted diagnostics, and remain recoverable for later requeue.

## Scope

### In Scope
- Harden `BackendClient`, `OutboxManager`, `ScheduledWorkService`, `UsageReconciler`, and `TaskSchedulerBackupService` without replacing their architecture.
- Establish contract-first authenticated REST/outbox outcomes, one clear retry owner, bounded retries/timeouts/cancellation, idempotency, durable dead-letter/requeue seams, and restart-safe checkpoints.
- Add strict-TDD behavior, security, concurrency, restart, efficiency, and changed-scope line-coverage evidence (>80%); exact policies are deferred to specs/design.

### Out of Scope
- Remote WNS/Realtime/integrity (SDD7), onboarding/packaging/release (SDD8), UI redesign, live backend acceptance, or a full Windows matrix.
- SDD3 remediation or reopening SDD5; SDD5 6.4 and Unit 7 remain open, deferred, and non-blocking. No `client-ready`, verify PASS, T24 absorption, archive, live integration, or `ExternalVerified=true` claims.
- Replacement supervisors, event buses, generic workflow engines, polling loops, unbounded scans/work, or retry stacking.

## Capabilities

### New Capabilities
- `offline-sync-recovery`: Durable authenticated sync delivery, scheduled recovery, dead-letter/requeue, and restart reconciliation for T18/T20/T10-B.

### Modified Capabilities
- None. `offline-enforcement-safety-loop` is a dependency boundary, not a changed requirement.

## Approach

Retain Service-owned transport and SQLite durability. Define immutable Domain ports/outcomes, then harden one Service coordinator as the single work-admission/retry owner; keep `OutboxManager` responsible for local state, `BackendClient` for authenticated HTTP, `UsageReconciler` for local checkpoints, and Task Scheduler as a bounded trigger only. Specs/design will define retry classification, retention, requeue authority, and recovery transitions.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `src/ControlParental.Domain` | Modified | Ports and durable outcome/state contracts. |
| `src/ControlParental.Service` | Modified | Transport, outbox, scheduler, backup trigger, reconciliation lifecycle. |
| `tests/ControlParental.Service.Tests` | Modified | TDD evidence for failure, race, cancellation, idempotency, and restart paths. |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Retry amplification or crash-window duplication | Med | One owner, bounded budgets, stable idempotency, durable acknowledgements. |
| Secret leakage or unbounded work | Med | Redaction, bounded queries/queues, cancellation and complexity review. |
| Missing backend/Windows evidence | High | Contract-first tests; label runtime evidence as pending. |

## Rollback Plan

Revert implementation/test work by reviewable slice, preserving existing outbox rows and schema compatibility; disable the new recovery path and retain durable events for later handling. Do not delete dead-letter data during rollback.

## Dependencies

- SDD5 definitive identity/REST contract seam; local SQLite durability; supported Windows environment for Task Scheduler evidence. These do not authorize live-backend claims.

## Success Criteria

- [ ] T18/T20/T10-B behavior is specified and implemented with one retry owner, bounded cancellation, idempotency, and no silent event loss.
- [ ] Every exhausted event is durably dead-lettered with redacted diagnostics and a defined recovery/requeue path.
- [ ] Changed-scope line coverage exceeds 80%, with branch, behavior/security/concurrency/restart evidence and complexity/efficiency review.
- [ ] Offline/local startup remains non-blocking when identity or backend availability is absent.
