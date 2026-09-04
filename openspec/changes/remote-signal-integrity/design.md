# Design: Remote Signal Integrity — C1A/C1B/C1C/C2 Auto-Chain

## Decision First

The second combined C1 candidate is rejected. Use four clean child branches: **C1A** handler state/decision semantics, **C1B** owner rehydration/effects, **C1C** deadline timer, then unchanged **C2** composition. No patch reuse: each branch starts from the approved predecessor and records fresh RED→GREEN→triangulation→safety-net evidence.

| Branch | Predecessor | Boundary | Forecast |
|---|---|---|---:|
| C1A | B `1fc5b60dc801bbb12d25f2131cb17201c4f04b47` | Pure handler only | 120–220 |
| C1B | approved C1A | Owner, no deadline timer or Program | 250–380 |
| C1C | approved C1B | Deadline timer only | 180–300 |
| C2 | approved C1C | Production composition only | ≤200 |

Each slice has a hard 400 CODE+TEST cap; C1A/B/C are separately reviewable and C2 is not started until C1C verification passes.

## C1A — Pure Handler Contract

`IntegrityVerdictHandler` remains synchronous and side-effect free. Reuse the existing 19-field `IntegrityEscalationState` and existing methods `Snapshot()`, `Restore(state)`, `HandleVerdictDecision(...)`, `HandleLocalFailureDecision(...)`, and `EvaluateDeadlineDecision(...)`/`EvaluateDeadlineAtClock(...)`; add no durable field or type.

Before allocating or committing `Epoch`/`Sequence`, classify input. Only an authenticated successful `trust` or `revoked` verdict, a valid local failure, or an explicit deadline evaluation is definitive. Failed backend calls, unknown/malformed verdicts, invalid local-failure input, and cancellation return the existing non-degrading/cancelled result without changing counters, deadline, epoch, sequence, or max clock. Preserve exact revoked threshold/staging, trust threshold/recovery, one-shot deadline transition, reaction and notification key formulas, and pure rollback/timing validation. `Restore` validates identity, schema, UTC/timing, counters, and `MaxWallClockSeenUtc` before mutation; rollback is `StaleState` and fail-closed to the caller.

## C1B — Owner Without Timer or Program

Append an optional `IIntegrityEscalationStateStore?` to the existing `AntiTamperMonitor` constructor so positional callers remain compatible. C1B owns `RehydrateAsync`, durable snapshots, effect progress, and lifecycle barriers; it does **not** schedule a deadline and does not modify `Program.cs`.

Startup loads before the first remote check. Missing is valid first start. Corrupt, unavailable, or wrong-identity state fails closed; load faults and cancellation are not swallowed and remain observable through the existing lifecycle/start task. Store/effect calls occur outside the owner state lock. Persist accepted state and each pending identity before its effect. Reaction and notification progress are persisted independently, so a crash after reaction cannot mark notification complete.

Recovery uses existing state only: `Phase` plus `RecoveryLatch` identifies authoritative recovery (`ResolveIssueAsync`); otherwise the pending reaction is an `AddIssueAsync` using the unchanged `ReactionIdempotencyKey`. `PendingReactionId`/`CompletedReactionId` and notification counterparts are progress, not a new keyed resolve contract. Stale generation, stale effect identity, cancellation, fault, Stop, and Dispose must suppress or observe work without duplicate effects.

## C1C — Deadline Timer Only

Modify only the existing owner timer path and focused tests. When rehydrating or creating `Pending`, calculate one relative OS timer delay from persisted `DeadlineDueUtc` and the current validated clock/max-clock state. Due in the past or exactly now fires immediately; future due uses `due - current`. If current wall clock is below persisted `MaxWallClockSeenUtc`, fail closed immediately and never schedule a larger replacement delay. Do not resubtract wall time when the callback runs.

Capture the owning `Generation` in the callback. It may execute only if that generation is still active; trust cancellation invalidates/disposes its timer, and stale generation, shutdown, callback cancellation, or callback fault is observed without mutating a newer generation. The callback admits the existing deadline decision, then follows C1B save-before-effect and exact keyed effect flow. All timer behavior is O(1); no polling loop or second async owner.

## C2 — Composition Unchanged

Modify only `src/ControlParental.Service/Program.cs` and an existing Program test seam. Register the existing `FileIntegrityEscalationStateStore` as the production singleton and resolve both `IIntegrityEscalationStateStore` and `IAntiTamperMonitor` from the real container. If `RunMainAsync` is not safely resolvable in the test host, extract only a minimal internal testable composition boundary. Do not move owner logic into Program or add a host/framework.

## Exact Files, Tests, and Complexity

| Slice | Production | Tests |
|---|---|---|
| C1A | `IntegrityVerdictHandler.cs` | `IntegrityVerdictHandlerTests.cs` |
| C1B | `AntiTamperMonitor.cs` | `AntiTamperMonitorTests.cs` |
| C1C | `AntiTamperMonitor.cs` | `AntiTamperMonitorTests.cs` |
| C2 | `Program.cs` | Existing `ProgramHardeningTests.cs` or `ProgramBackupArgsTests.cs` seam |

Owner state, effect identity, timer scheduling, and handler transitions are O(1) per operation. No unbounded history, retry, lock, migration, filesystem abstraction, or store-level monotonic comparison is added.

## Report Findings Addressed

This split directly closes the rejected candidate's failures: non-definitive epoch mutation (C1A); recovery incorrectly using Add instead of Resolve and coupled notification progress (C1B); missing/fault/cancel observability (C1B); rollback extension, wall-clock resubtraction, trust cancellation, and stale callbacks (C1C); zero runtime deadline-callback coverage and absent DI proof (C1C/C2). Aggregate coverage is insufficient: tests must execute the production callback state machine and real container.

Strict-TDD logs must retain exact RED commands, test names/messages, exit codes, GREEN output, triangulation, and safety-net output. The prior Batch-1 GREEN artifact that exited 1 must be marked failed, not reused or relabeled.

## Rollback and Impossible State Caveats

Rollback C2 to approved C1C, C1C to approved C1B, C1B to approved C1A, and C1A to B; remove child composition before rolling back its owner. No migration. The 19-field state cannot persist circuit-breaker/failure counters or startup grace time; those remain process-local. A keyed recovery resolve does not exist, so `RecoveryLatch` plus existing effect IDs provide the only supported durable distinction.
