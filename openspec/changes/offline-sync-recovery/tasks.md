# Tasks: Offline Sync Recovery

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 1,300–1,700 additions/deletions; measured B2 test file alone is 446 lines |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1A claim admission; PR1B1 completion/failure; PR1B2a recovery; PR1B2b bridge; PR1C schema; PR2 coordinator/REST; PR3 reconciliation/backup; PR4 evidence |
| Delivery strategy | auto-chain |
| Chain strategy | feature-branch-chain; no size exception |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | PR | Base / budget |
|---|---|---|---|
| 1.1A | Claim admission/fixture | PR1A | tracker; ≤400 lines |
| 1.1B1 | Conditional completion/failure | PR1B1 | immediate PR1A; ≤400 lines |
| 1.1B2a | Recovery/restart | PR1B2a | immediate PR1B1; ≤400 lines |
| 1.1B2b | Bridge/integration | PR1B2b | immediate PR1B2a; ≤400 lines |
| 1.2 | Schema adoption | PR1C | immediate PR1B2b; ≤400 lines |
| 2–4 | Downstream behavior/evidence | PR2–PR4 | each immediate parent |

## Phase 1: Durable Foundation

- [ ] 1.1A **RED → minimal GREEN → TRIANGULATE/REFACTOR** in dedicated `tests/ControlParental.Service.Tests/OutboxClaimAdmissionTests.cs` with shared fixture support owned by A: combine contracts/status/entities with real claim admission, eligibility/order-before-`LIMIT`, bounded pages, and concurrent claims in `src/ControlParental.Domain/IOutboxManager.cs`, `OutboxEntryStatus.cs`, `OutboxEntry.cs`, `src/ControlParental.Service/PolicyDbEntity.cs`, `OutboxManager.cs`, and `ControlParentalDbContext.cs`; later children reuse helpers with no double-count. Preserve unrelated pre-existing `OutboxManagerTests.cs` content. Behavior proves invariants; enum/input-only assertions are forbidden.
- [ ] 1.1B1 **RED → minimal GREEN → TRIANGULATE/REFACTOR** in `tests/ControlParental.Service.Tests/OutboxLifecycleCompletionTests.cs` using 1.1A support: implement conditional complete/fail, reclaim generations/stale guards, mixed outcomes, exhaustion/dead-letter, and parameterized SQL in `src/ControlParental.Service/OutboxManager.cs` and `ControlParentalDbContext.cs`; ≤400 lines.
- [ ] 1.1B2a **RED → minimal GREEN → TRIANGULATE/REFACTOR** in owned `tests/ControlParental.Service.Tests/OutboxRecoveryTests.cs`: implement bounded expired recovery, real file-backed restart/no-double-effect, cancellation/busy bound, and transaction rollback in the relevant `OutboxManager.cs`/`ControlParentalDbContext.cs` hunks; ≤400 lines.
- [ ] 1.1B2b **RED → minimal GREEN → TRIANGULATE/REFACTOR** in owned `tests/ControlParental.Service.Tests/OutboxBridgeIntegrationTests.cs`: implement audit/authorized idempotent requeue, redaction, invalid-source guards, and real scheduler success/failure integration in `OutboxManager.cs`, `ScheduledWorkService.cs`, and scheduler tests; keep production-compatible `MarkSentAsync`/`MarkFailedAsync` durable and non-deleting until Unit 2 migrates ownership—never no-op active calls; ≤400 lines.
- [x] 1.2 **RED → minimal GREEN → TRIANGULATE/REFACTOR** in `tests/ControlParental.Service.Tests/OutboxManagerTests.cs`: test fresh/existing `EnsureCreated`, adoption, schema version/checksum, backfill, defaults/constraints/indexes, restart/rollback; add bootstrap/version wiring and remove invalid migration/snapshot artifacts.

## Phase 2: Delivery and Admission (PR2)

- [ ] 2.1 **RED → minimal GREEN → TRIANGULATE/REFACTOR** in `BackendClientTests.cs`, `BackendClientSingleRequestTests.cs`, and `AuthenticatedBackendClientTests.cs`; update `BackendClient.cs`/`IBackendClient.cs` for T10-B identity gating, idempotency, redacted outcomes, bounded transport retry, timeout, and cancellation.
- [ ] 2.2 **RED → minimal GREEN → TRIANGULATE/REFACTOR** in `ScheduledWorkServiceTests.cs`, `ScheduledWorkServiceBackoffDecrementTests.cs`, `ScheduledWorkServiceAsyncDispatchTests.cs`, and `ScheduledWorkServiceIdentityTests.cs`; update `ScheduledWorkService.cs` for one owner, finite backoff/scans, connectivity, non-overlap, lifecycle, and shutdown bounds; remove bridge.

## Phase 3: Restart and Backup Composition (PR3)

- [ ] 3.1 **RED → minimal GREEN → TRIANGULATE/REFACTOR** in `UsageReconcilerTests.cs`; update `UsageReconciler.cs`/`IUsageReconciler.cs` for bounded cancellable checkpoints, applied markers, restart continuity, duplicate safety, and single-flight.
- [ ] 3.2 **RED → minimal GREEN → TRIANGULATE/REFACTOR** in `TaskSchedulerBackupServiceTests.cs`, `ProgramBackupArgsTests.cs`, and `ProgramHardeningTests.cs`; update backup interfaces/service and `Program.cs` for trigger-only shared admission and lifecycle ordering.

## Phase 4: Final Verification (PR4)

- [ ] 4.1 Require each child to have an immutable sequential patch or actual branch/commit diff with exhaustive no-double-count numstat; verify ≤400-line boundaries, security, complexity, concurrency, restart, cancellation, bounds, and branch evidence. Historical compile/corrective RED chronology exists in `apply-progress`; raw standalone RED is incomplete and remains a warning.
- [ ] 4.2 Report changed-scope line coverage >80% and final evidence; mark live backend, unsupported Windows matrix, SDD5, SDD7, and SDD8 pending—no fabricated runtime claims.
