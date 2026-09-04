# Exploration: offline-sync-recovery

## Current State

### Verified baseline

- SDD6 owns **T18, T20, and T10-B**: authenticated REST policy/telemetry access, durable outbox delivery, scheduler/retries/idempotency, and restart recovery. SDD1–SDD4 boundaries remain unchanged; SDD3's evidence debt and SDD5's contract-first boundary are inputs, not reopened work.
- The repository is a dirty working tree. This exploration is structural/read-only; no tests, builds, runtime actions, installations, commits, or source/test edits were performed. Existing bytes are not attributed to SDD6.
- `openspec/config.yaml` confirms .NET 9/C#/EF Core+SQLite, Clean/Hexagonal ownership, xUnit/integration tooling, and unavailable configured E2E. The requested SDD6 gates are stricter: strict TDD, changed-scope line coverage >80%, branch evidence, bounded/cancellable work, complexity/efficiency review, and redacted diagnostics.
- Existing code is reusable baseline, **not proof of completion**. Static presence and local unit tests do not prove restart recovery, live REST behavior, backend idempotency, or Windows Task Scheduler execution.

### Exact current flow and ownership

1. `Program` composes `BackendClient`, `ScheduledWorkService`, `OutboxManager`, `UsageReconciler`, repositories, identity coordination, and hosted services. `BackendIdentityStartupService` initializes identity before authenticated work can be valid, while `ControlParentalService` starts local health/session/usage paths. `ScheduledWorkService` is the intended T18/T20/T10-B owner; backend HTTP remains Service-owned by `BackendClient`.
2. `ScheduledWorkService.StartAsync` creates four `System.Threading.Timer` instances, registers Task Scheduler backup work asynchronously, and dispatches an initial policy sync. Timer callbacks dispatch one in-flight operation per work type through `Task.Run`; cancellation is owned by a service CTS and `StopAsync` waits only within a shutdown budget.
3. Heartbeat and policy sync use `BackendClient`; policy sync is identity-gated through `IBackendIdentityCoordinator.CurrentState` and the definitive `DeviceId`. `BackendClient.SendAuthenticatedAsync` owns session acquisition, request timeout, up to three attempts, transient/401 handling, `Retry-After`, idempotency-key generation, and bounded retry delay. These are strong seams, but acceptance of endpoint semantics and remote idempotency remains contract/backend evidence.
4. Outbox data is durable SQLite state (`outbox` table, unique `DedupKey`, attempts/error timestamps). `OutboxManager.EnqueueAsync` is locally deduplicating; `GetPendingEntriesAsync` orders by creation and limits to 100; `MarkSentAsync` deletes; `MarkFailedAsync` increments attempts and truncates diagnostics. The current scheduler groups rows by table and pushes categories, but the current source still has batch-wide failure marking and reparses payloads; malformed/unknown/partial outcomes are not a complete per-entry state machine.
5. `UsageReconciler` persists one `ReconciliationHistory` row per server date and skips a completed date, then loads all foreground and usage rows, computes grouped deltas, backfills, and records completion. It also owns a WMI timer whose callback calls `.Wait()`; `StartAsync` starts watchers and the timer, while scheduler reconciliation may call `StartAsync` and `ReconcileAsync` again. The tests cover local idempotency/backfill/degraded behavior, but not concurrent runs, restart interruption/continuation, bounded query behavior, or timer cancellation.
6. `TaskSchedulerBackupService` registers three named tasks (heartbeat, outbox, reconciliation) with CreateOrUpdate and boot/logon/periodic triggers. Registration/unregistration catches failures and returns booleans, but the adapter uses synchronous Windows Task Scheduler APIs inside task-returning methods, emits exception text via `Debug.WriteLine`, has no injected executor/state-transition seam, and current coverage does not prove actual registration, restart, duplicate-instance prevention, or cancellation. It is a backup trigger, not a second retry owner.

### T18/T20/T10-B status

| Capability | Static implementation | Local tests/evidence | Verified gap |
|---|---|---|---|
| T18 REST + identity gating | `BackendClient` and `ScheduledWorkService` exist; policy uses definitive identity; retries/timeouts/idempotency are centralized in the client | Backend client, identity, scheduler tests exist | No live backend, canonical REST proof, restart recovery, or proof every operation uses one retry owner |
| T18 outbox/recovery | Durable SQLite outbox, unique dedup key, attempt/error fields, category pushes | `OutboxManagerTests` and per-entry regression tests are present in the current tree | Current scheduler still can mark unrelated/unsent rows failed after one category failure; payloads are reparsed; permanent/transient/dead-letter policy and crash-safe claim/ack semantics are absent |
| T20 scheduled work | Four timers, bounded in-flight map, backoff timestamps, Task Scheduler backup adapter | Scheduler/backoff/composition/backup unit tests are present | Timer callbacks are not a single cancellable scheduler loop; backup registration is fire-and-forget and synchronous underneath; actual Windows registration/restart evidence is missing |
| T10-B usage reconciliation | Durable daily history and delta backfill; scheduler entry point | Broad `UsageReconcilerTests` cover local behavior | `.Wait()` timer callback, full-table materialization, no explicit single-flight persistence claim, and no interrupted/restarted recovery evidence |

## Affected Areas

- `src/ControlParental.Service/ScheduledWorkService.cs` — single owner for timer dispatch, policy sync, heartbeat, outbox classification, backoff, cancellation, and backup-trigger routing; primary T18/T20 boundary.
- `src/ControlParental.Service/OutboxManager.cs`, `ControlParentalDbContext.cs`, `src/ControlParental.Domain/IOutboxManager.cs` — durable entry lifecycle, deduplication, bounded selection, per-entry outcomes, and restart-safe state.
- `src/ControlParental.Service/BackendClient.cs`, `src/ControlParental.Domain/IBackendClient.cs`, `BackendIdentityCoordinator.cs` — authenticated REST ownership, definitive identity gate, endpoint-specific idempotency and retry policy; must not depend on live backend availability.
- `src/ControlParental.Service/TaskSchedulerBackupService.cs`, `src/ControlParental.Domain/ITaskSchedulerBackup.cs` — T20 backup trigger adapter and its testable Windows seam; no competing sync/retry implementation.
- `src/ControlParental.Service/UsageReconciler.cs`, `src/ControlParental.Domain/IUsageReconciler.cs` — T10-B reconciliation, timer/lifecycle ownership, query bounds, durable history, and restart continuation.
- `src/ControlParental.Service/Program.cs`, `BackendIdentityStartupService.cs` — hosted-service ordering and identity gating; must ensure offline/local startup is not blocked by remote availability.
- `tests/ControlParental.Service.Tests/{OutboxManager,ScheduledWorkService,TaskSchedulerBackupService,UsageReconciler,BackendClient}*` — behavior-focused TDD evidence for mixed outcomes, cancellation, restart, idempotency, retry bounds, and no overlap. Existing tests prove seams, not the full capability.
- `openspec/changes/windows-backend-independent-readiness/*` — SDD3 evidence debt, especially scheduler/outbox/contract-deferred findings; reference only, do not alter or duplicate.
- `openspec/changes/backend-identity-contract/*` — SDD5 contract-first identity and REST boundary; 6.4 and Unit 7 (7.1–7.3) remain open, deferred, and non-blocking because Windows environments are unavailable. SDD6 may consume the local contract seam but cannot claim `client-ready`, verification PASS, T24 absorption, archive, live integration, or `ExternalVerified=true`.

## Approaches

1. **Harden the existing pipeline** — retain Service-owned `BackendClient`, `BackendIdentityCoordinator`, SQLite outbox, `ScheduledWorkService`, and `TaskSchedulerBackupService`; introduce one explicit scheduler/retry ownership boundary, per-entry outbox outcomes, durable reconciliation checkpoints, and deterministic restart/cancellation tests.
   - Pros: smallest blast radius; preserves SDD5 identity contract; directly addresses observed defects; contract-first and backend-independent; straightforward rollback by work unit.
   - Cons: requires careful migration of timer and outbox semantics; existing provisional endpoint methods remain contract-dependent; Windows Task Scheduler proof still needs a supported environment.
   - Effort: Medium–High

2. **Replace scheduling/sync with a new supervisor and queue/repository model** — add a new durable work coordinator, transport abstraction, scheduler host, and migration adapter around existing consumers.
   - Pros: cleaner long-term state machine and easier future multi-endpoint scheduling.
   - Cons: duplicates already-established identity/transport ownership, expands migration and review risk, can create stacked retry/queue semantics, and would blur SDD3/SDD5 boundaries.
   - Effort: High

## Recommendation

Choose **Approach 1**, with a minimal clean-architecture boundary: Domain ports and immutable outcome/state contracts; one Service application coordinator owns scheduling, work admission, retry budgets, cancellation, and restart recovery; `OutboxManager` owns only durable local entry state; `BackendClient` owns authenticated HTTP and endpoint retry classification; `TaskSchedulerBackupService` only triggers bounded work through the coordinator; `UsageReconciler` owns local reconciliation persistence but not remote retry policy. No event bus, generic workflow engine, second retry layer, or polling loop should be introduced.

The proposal should first freeze contract-independent invariants, then implement in reviewable units such as: (A) scheduler admission/backoff/cancellation and identity gating, (B) outbox per-entry classification/ack/restart recovery, (C) reconciliation lifecycle/checkpoint/restart behavior, and (D) Task Scheduler adapter/evidence. Keep tests with the behavior they prove. Suggested acceptance/evidence targets are bounded deterministic tests for mixed success/failure, duplicate enqueue, crash between send and acknowledgement, restart replay with stable idempotency, revoked/unavailable identity, retry exhaustion, `Retry-After`, cancellation, no overlap, and redacted diagnostics; plus supported Windows Task Scheduler/runtime receipts when an environment is available. These are evidence targets, not task checkboxes.

### Out of scope and dependencies

- Out of scope: production backend changes, live REST/RLS/JWT/TLS/WNS verification, backend route/DTO invention, full Windows compatibility matrix, `client-ready`, verification PASS, T24 absorption, archive, live integration, `ExternalVerified=true`, UI redesign, SDD3 remediation, SDD5 Unit 7/6.4 closure, and unrelated runtime/IPC work.
- Dependencies: SDD5's definitive identity/REST contract seam; backend-provided idempotency and endpoint semantics; local SQLite durability; a supported Windows environment for actual Task Scheduler/runtime evidence. SDD5's open Windows work is explicitly non-blocking.
- Likely PR/work-unit shape: chained, independently rollbackable slices; keep each within the project review budget and never split tests from their production behavior. Do not create exact task checkboxes during exploration.

## Risks

- Activating dormant sync can expose provisional backend assumptions; identity readiness and explicit capability gating must remain fail-closed.
- A crash after remote acceptance but before local acknowledgement can duplicate or lose work unless stable operation/dedup keys and backend idempotency are contractually proven.
- Multiple retry owners (client, scheduler, Task Scheduler, and outbox) can amplify traffic; one owner per failure domain is mandatory.
- Current timer callbacks, WMI `.Wait()`, full materialization, and fire-and-forget backup registration can violate cancellation, bounded-work, or shutdown guarantees.
- Local deduplication is not proof of remote idempotency; no backend claim may be inferred from mocks.
- Dirty work and stale prior artifacts make historical counts non-authoritative; all future evidence must identify the current snapshot and distinguish static existence from executed behavior.

## Ready for Proposal

**Yes, conditionally.** The change is sufficiently mapped for `sdd-propose` if the proposal preserves the SDD3 evidence debt and SDD5 contract-first/non-blocking boundaries, names the minimal coordinator and single retry owner, carries the strict quality gates, and labels Windows/backend/runtime evidence as pending where unavailable. No additional product decision is required to begin a contract-first proposal; proposal must not claim completion of any deferred SDD5 or live-integration evidence.

## Section D Envelope

- `status`: success
- `executive_summary`: Existing Service-owned REST, SQLite outbox, scheduler, Task Scheduler backup, and usage reconciliation seams cover the intended SDD6 shape, but static code and local tests do not prove reliable offline recovery. The highest-confidence path is to harden the existing pipeline with one coordinator/retry owner, per-entry durable outcomes, cancellation-safe scheduling, and restart-focused evidence.
- `artifacts`: Filesystem `openspec/changes/offline-sync-recovery/exploration.md`; Engram topic `sdd/offline-sync-recovery/explore`, project `ddddddddd`, session `sdd6-offline-sync-recovery-20260818`.
- `next_recommended`: Run `sdd-propose` for `offline-sync-recovery`; preserve T18/T20/T10-B scope and explicitly carry pending evidence/dependencies.
- `risks`: Retry amplification, crash-window idempotency, timer/WMI blocking, missing Windows Task Scheduler/runtime evidence, provisional backend contracts, and dirty-tree reproducibility.
- `skill_resolution`: Followed installed `sdd-explore` and OpenSpec conventions; used CodeGraph first for structure/call flow, then read config/specs/prior SDD artifacts and current production/tests. Cognitive-document design applied through result-first headings and verified-baseline/gap separation.
