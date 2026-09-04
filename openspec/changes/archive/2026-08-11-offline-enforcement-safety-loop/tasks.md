# Tasks: Offline Enforcement Safety Loop

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated authored lines | 1200-1600 total; 800-1200 remaining |
| Work-unit budget | Maximum 800 authored lines |
| 800-line budget risk | High for the whole change; controlled by the split below |
| Chained PRs recommended | Yes |
| Suggested split | Unit 1 → Unit 2 → Unit 3 |
| Delivery strategy | Automatic unless an error is found |
| Chain strategy | feature-branch-chain |
| Chaining trigger | Automatic only when the 800-line budget or dependencies require it |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

The `400-line budget risk` line is retained for guard compatibility; the authoritative execution gate is 800 authored lines per work unit. The total estimate and dependencies require automatic chaining, but no PR is created in this phase.

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Contracts / port closeout | PR 1 | ≤400 authored lines; already complete |
| 2 | Coordinator, mailbox, scheduling, and TDD | PR 2 | 400-600 authored lines; depends on Unit 1 |
| 3 | Native actions, health, restore, runtime evidence, and closure | PR 3 | 400-600 authored lines; depends on Unit 2 |

## Phase 1: Contracts / Port Closure

- [x] 1.1 Complete `ControlParental.Domain/AgentCommandContracts.cs`, `src/ControlParental.Service/AgentCommandPort.cs`, `ControlParental.Service/SessionEnforcementCoordinator.cs`, and `ControlParental.Service.Tests/SessionCoordinatorTests.cs` (RED CS0246 → GREEN 9/9; coverage 144/152 lines 94.74%, branches 35/40 87.5%; O(1) queue/ACK, O(P) replacement, queue 256; exactly 400 authored additions; evidence revision `sha256:1377e35f4de244bbe5637b8dc652a6ef2e6da991efdc6d4514764a6a7e2a3d43`).

## Phase 2: Coordinator / Scheduling

- [x] 2.1 In `SessionEnforcementCoordinator.cs`, serialize restore/foreground/tick/result ordering, keep latest-dirty coalescing, and preserve critical admission.
- [x] 2.2 Drive strict RED/GREEN/REFACTOR for saturation, reconnect, stale-result, and duplicate-command cases in `SessionCoordinatorTests.cs`.
- [x] 2.3 Review complexity/inefficiency/clean-architecture/flow/logic/optimization against O(1) lookup, no overlap, and no blocking waits.

## Phase 3: Agent / Native Actions / Health

- [x] 3.1 Complete `AgentCommandPort.cs` generation replacement, typed ACK/result correlation, and bounded retry/timeout behavior.
- [x] 3.2 Finish `OverlayIntentPolicy.cs`, `OverlayWindow.cs`, and `ProcessTerminator.cs` for overlay show/replace/clear, PID-reuse-safe exact-target termination, and `LockWorkStation` outcomes.
- [x] 3.3 Update `EnforcementEngine.cs`, `ForegroundWatcher.cs`, and health/issue plumbing so `AppId` stays canonical and durable issues dedupe semantically.

## Phase 4: Verification / Runtime / Closeout

- [x] 4.1 Drive strict RED/GREEN/REFACTOR for restart-converges-intent, same-app-threshold-crossing, stale-heartbeat, agent-death, child-admin, and PID-reuse scenarios.
- [x] 4.2 Record Windows runtime evidence for overlay lifecycle, exact termination, `LockWorkStation`, reconnect, and heartbeat loss/recovery; include branch report and >80% changed-scope line coverage.
- [x] 4.3 Perform clean canonical closeout: current truth only, no contradictory status, no obsolete evidence leakage, and no hidden rollback ambiguity.

  Completed with the finite CLI harness in `SessionCoordinatorPerformanceEvidenceTests.cs` and structured `performance-evidence.json`. The post-verification corrective checkpoint in `apply-progress.md` preserves the original measurements and records the current evidence: two consecutive 1,289-test full-suite passes, 80.90% line / 72.93% branch expanded changed-scope coverage, a fresh 5/5 Windows runtime PASS, and a fresh finite performance run with 784.0624 bytes/event, queue high-water 256/256 with backpressure/drain, timer drift p50/p95 0.445/1.0455 ms, and processing latency p50/p95 5.1/9.7 microseconds. Final verification remains a separate `sdd-verify` step.
