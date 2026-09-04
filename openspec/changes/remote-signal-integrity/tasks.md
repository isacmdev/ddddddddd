# Tasks: SDD7 Unit 4C Durable Escalation

## Review Workload Forecast

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

| Unit | Goal | Base / branch | Budget |
|---|---|---|---:|
| A | Approved predecessor | `c183149` | existing |
| A2 | Approved predecessor | A | existing |
| B | Approved predecessor | A2 | existing |
| 4C2A | Contract/invariant validation | `c183149` / `feat/sdd7-4c2a-escalation-state` | 320–340 CODE+TEST |
| 4C2A2 | Robust file store | future approved A / `feat/sdd7-4c2a2-escalation-store` | <=390 |
| 4C2B | Exact-key enforcement dedupe | approved A2 | 320–380 |
| 4C2C/C1B1 | Nominal durable owner reconciliation | approved B / C1B1 commit | <=400 CODE+TEST |
| 4C2C/C1B2 | Stale/fault hardening child | C1B1 commit | 120–180 CODE+TEST |
| Unit 5 | Receipts/compatibility | deferred | — |

Chain: A → A2 → B (`c183149`) → 4C2A → 4C2A2 → 4C2B → C1B1 → C1B2; C1C base=C1B2 commit; Unit 5 deferred. Every child is hard <=400 CODE+TEST; no exception.

## Phase 1: Admission

- [x] 1.1 `TriggerSync` rejection/coalescing/cancellation/single-flight tests in `UIMessageHandlerWnsTests.cs`, `ScheduledWorkService*Tests.cs`, and `UIMessagesJsonContextTests.cs`.
- [x] 1.2 Implement bounded authenticated admission in `IScheduledWorkService.cs`, `UIMessageHandler.cs`, `ScheduledWorkService.cs`, `UIMessages.cs`, and `WnsPushNotificationHandler.cs`.
- [x] 1.3 Verify bounds, identity, cancellation, coalescing, and polling.

## Phase 2: WNS Convergence

- [x] 2.1 Test WNS lifecycle, identity, idempotency, reconciliation, restart, and legacy absence in `Wns*Tests.cs`, `ServiceCompositionTests.cs`, and `ServiceHostStartTests.cs`.
- [x] 2.2 Complete `WnsRegistrationCoordinator.cs`; remove legacy production registrations from `Program.cs`.
- [x] 2.3 Verify redaction, bounds, retry ownership, DI, and restart convergence.

## Phase 3: Realtime

- [x] 3.1 Add `RealtimeSubscriberTests.cs` barriers for foreground, refresh, background, stale, cancellation, and resubscribe.
- [x] 3.2 Implement lifecycle/generation gates and typed invalidation in `RealtimeSubscriber.cs`; retain Service/REST authority.
- [x] 3.3 Verify disconnect/background leaves enforcement and polling unchanged with App.UI tests.

## Phase 4: Runtime Integrity (unchecked; cumulative 10/17)

- [ ] 4.1 Lifecycle, ordering, restart, and race tests across `AntiTamperMonitorTests.cs`, `IntegrityVerdictHandlerTests.cs`, `DurableIssueStoreTests.cs`, `IntegrityRuntimePathTests.cs`, and `EnforcementLevelMonitor*Tests.cs`.
- [ ] 4.2a / C1B1 (<=400): Strict-TDD immutable RED/GREEN nominal owner reconciliation in `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs`: rehydrate before remote; persist valid accepted snapshots; cumulative reaction+notification progress; exact keys; same-key restart no-op; distinct post-restart key accepted; phase+latch recovery; first-through-fourth revoked/trust-reset/agent-death nominal convergence. No timer/DI/fault hardening.
  Affected build, focused/full monitor, exact baseline-excluded safety, portable coverage, independent verification, and <=400 gate are required.
- [ ] 4.2b / C1B2 (base=C1B1 commit; 120–180): Strict-TDD immutable RED/GREEN stale/fault hardening in the same two files: cancellation-ignoring save/reaction/notification boundaries; admitted-work B2-consistent stale completion/fault suppression; durable save/effect/progress failure retry/restart convergence. No new abstractions/schema fields.
  Affected build, focused/full monitor, exact baseline-excluded safety, portable coverage, independent verification, and <=400 gate are required.
- [ ] 4.3 Run the final Unit 4 gate only after all 4C2A/4C2A2/4C2B/C1B1/C1B2 approvals and verification.

### Strict-TDD Execution Plans

- **4C2A RED/GREEN** — A owns the `IIntegrityEscalationStateStore` contract boundary plus immutable state/envelope, document/schema/null-state validation, closed `EscalationPhase`, typed errors, and validator in `src/ControlParental.Domain/`; A2 implements the store in `src/ControlParental.Service/`. Domain tests must first prove RED then GREEN for undefined enum; `PolicyVersion` `1..int.MaxValue` valid and `0`/negative invalid; `IdentityScope` length `1..256` and each present effect ID length `1..256` (with whitespace rejected); `Epoch`/`Sequence` `0..IntegrityEscalationState.MaximumCounter` valid (`MaximumCounter` currently documented as `1_000_000_000`) and `MaximumCounter+1` invalid; null state and schema mismatch typed validation; `Pending` with `TimingValid=false`, fired, or non-UTC due rejected; and valid reaction-only/no-effect/full-progress plus invalid reaction-before-notification effect-key shapes.
- **4C2A2 RED/GREEN** — A2 adds source-generated JSON, hashed identity paths, cross-process lock, atomic temp/write/flush/replace/cleanup, and monotonic admission; tests must prove actual-missing vs access/I/O, corrupt/version/wrong-identity errors, two instances/processes same-identity serialization, stale Epoch/Sequence rejection, equal-version effect-progress regression rejection, and distinct-identity isolation. No fake migration.
- **4C2A2 safety/closure** — Prove cancellation and injected write/flush/replace failure cleanup, atomic old-or-new reads, full Service/App.UI regressions, changed-scope coverage, and hard budget gates; then pass approved A2 state to 4C2B and approved B to 4C2C without changing pure decisions, persist-before-effect ordering, exact deadline/trust/non-definitive semantics, generation barriers, or production `AntiTamperMonitor` ownership.

## Phase 5: Compatibility

- [ ] 5.1 Remove only obsolete compatibility wiring after 4C2C approval; add convergence/receipt assertions in `ServiceCompositionTests.cs`, `ServiceHostStartTests.cs`, `ScheduledWorkServiceAsyncDispatchTests.cs`, and `SessionCoordinatorPerformanceEvidenceTests.cs`.
- [ ] 5.2 Run SDD7 verification with separate local/backend/WNS/Realtime/Windows receipts; prohibit `live-integrated`, `client-ready`, and `ExternalVerified` claims.
