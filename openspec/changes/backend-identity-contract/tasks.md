# Tasks: Backend Identity Contract — Client Ready

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed/authored lines | 3,500–4,700 / 4,000–5,300; 6.3A pre-GREEN ≈342, with only ≈58 lines remaining before the 400-line budget |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Delivery strategy | auto-chain |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High
Task checkboxes: 23 total; 19 complete, 4 pending (6.4 and 7.1–7.3 remain incomplete)

### Suggested Work Units

| Unit | Goal | Forecast | Boundary / base |
|---|---|---:|---|
| 1–5 | Completed foundations | Preserved | Existing chain |
| 6A | Acceptance + Service WNS IPC | 500–800 authored | PR6A base = PR5; reviews ≤400 |
| 6B-1 / 6.2A | App.UI WNS handler cutover | ≤400 churn | PR6B-1 base = PR6A; handler RED/GREEN/REFACTOR tests; direct backend/bearer/log removal |
| 6B-2 / 6.2B | IPC compatibility and deterministic transport seam | ≤400 churn | PR6B-2 base = PR6B-1; marker/context catalogue, cancellation/timeout/fail-closed tests |
| 6B-3A1 / 6.3A1 | Registration port and safe typed view-model | ≤400 churn | PR6B-3A1 base = PR6B-2; strict TDD/unit tests only, no XAML; 7/7 focused tests exist, full-suite/coverage/reconciliation pending |
| 6B-3A2 / 6.3A2 | Dedicated registration surface and composition | ≤400 churn | PR6B-3A2 base = PR6B-3A1; XAML/AutomationIds, code-behind/command, DI, deterministic navigation, structural RED/GREEN/REFACTOR tests |
| 6B-3B / 6.3B | Appium harness and compatible-environment spike | ≤400 churn | PR6B-3B base = PR6B-3A2; unchanged harness/runbook/spike and finite cleanup/receipt |
| 6B-4 / 6.4 | Real E2E compatibility matrix | Pending | PR6B-4 base = PR6B-3B; unchanged scenarios and declared OS matrix |
| 7 | Verify/T24 | 150–350 authored | PR7 base = PR6B-3 |

## Phase 1: Contract and Deterministic Harness

- [x] 1.1 RED→GREEN: v1 claim/error/time/replay/idempotency fixtures and tests.
- [x] 1.2 TRIANGULATE→REFACTOR: bounded two-device/revocation/RLS probes; receipts never imply availability.

## Phase 2: Identity State and Persistence

- [x] 2.1 RED→GREEN: credential-free transition/generation model and tests.
- [x] 2.2 TRIANGULATE→REFACTOR: atomic DPAPI envelope, ACL, migration, quarantine, and legacy deletion.

## Phase 3: Definitive Pairing Lifecycle

- [x] 3.1 RED→GREEN: Service lifecycle, exact binding, reconciliation, refresh, replay, and cancellation.
- [x] 3.2 TRIANGULATE→REFACTOR: pairing/auth adapters and typed results retain T24 mappings without duplicate authority.

## Phase 4: Authenticated REST and Reliability

- [x] 4.1 RED→GREEN: generation-safe auth/retry, stale results, revocation, timeout/cancel, jitter, and idempotency.
- [x] 4.2 TRIANGULATE→REFACTOR: centralize authorization/retry ownership; prove bounded, cancellable, non-overlapping behavior.

## Phase 5: TLS and Local Acceptance Boundary

- [x] 5.1 RED→GREEN→TRIANGULATE→REFACTOR: fail-closed TLS validation, revocation, and pin rotation.
- [x] 5.2 RED→GREEN→TRIANGULATE→REFACTOR: acceptance manifest/schema/runbook and immutable-false local receipts; defer live execution.

## Phase 6A: Service-Owned WNS Composition

- [x] 6.1 RED→GREEN→TRIANGULATE→REFACTOR: WNS IPC contracts, authenticated dispatch, intent persistence/reconciliation, typed outcomes, bounded retries, and definitive upsert.

## Phase 6B: UI Cutover and Real E2E

- [x] 6.2 RED→GREEN→TRIANGULATE→REFACTOR: complete both autonomous UI IPC work units; coverage and final reconciliation recorded in cumulative apply evidence.
  - [x] 6.2A (PR6B-1): Cut over `WnsPushNotificationHandler.cs` to typed Service IPC; remove direct backend/bearer/log paths and complete handler RED/GREEN/REFACTOR tests in `WnsLifecycleTests.cs` (target ≤400 churn).
  - [x] 6.2B (PR6B-2): Lock canonical message-marker/context compatibility and the active App.UI source-generation catalogue; finish deterministic `NamedPipeUIChannel` transport seam plus cancellation/timeout/fail-closed tests (target ≤400 churn; depends on PR6B-1).
- [x] 6.3 RED→GREEN→REFACTOR: complete the registration UI composition sequence below; parent acceptance hierarchy is complete for the compatible-environment 6.3B result.
  - [x] 6.3A (rollup): complete both autonomous registration composition slices without changing Service authority or claiming live integration.
    - [x] 6.3A1 / PR6B-3A1, base PR6B-2: Add the registration port and safe typed view-model in `src/ControlParental.App.UI/`; strict TDD/unit tests, no XAML. Preserve 7/7 focused-test evidence, full-suite, coverage, and reconciliation evidence recorded in apply progress.
    - [x] 6.3A2 / PR6B-3A2, base PR6B-3A1: Add dedicated reachable XAML with `AutomationId`s, code-behind/command, DI, deterministic navigation, and structural RED/GREEN/REFACTOR tests; target ≤400 churn.
  - [x] 6.3B / PR6B-3B, base PR6B-3A2: Preserve the existing Appium harness/runbook/spike, finite cleanup, and redacted receipt history; reconcile the current receipt to the compatible-environment functional PASS with no scenario downgrade.
- [ ] 6.4 (base PR6B-3B): Run unchanged `WnsRegistrationE2ETests.cs` on exactly: Win10 22H2 x64 legacy; Win11 24H2 x64; Win11 25H2 x64. For each cell, record exact OS edition/version/build/native architecture, clean install where applicable, unchanged WNS E2E App.UI→Service IPC, WinAppDriver first and documented Nova fallback only after WinAppDriver failure, bounded timeouts, cleanup, payload/harness/evidence hashes, safe redacted XML/log/PNG, and `ExternalVerified=false`. A cell cannot be checked from static/compile/unit evidence or current Win11 22H2 preparation. ARM64 is future dedicated enablement: compile-intent only, unverified, unsupported, non-blocking, and not a task in this change. Any defect fix requires a separately planned strict TDD work unit with changed-scope coverage >80% and complexity/efficiency/clean-architecture review before rerun. Keep Unit 7 pending; NativeAOT Release verification remains there unless it blocks a matrix cell.

## Phase 7: Client-Ready Verification and Closure

- [ ] 7.1 Repeat full suites; verify >80% changed-scope lines, branches, complexity/allocations/contention, secret/log scans, format/analyzers, DoD-G, and bounded-behavior receipts.
- [ ] 7.2 Issue only a `client-ready` verdict with `ExternalVerified=false`; permit SDD6/SDD7 contract-first and defer all live claims.
- [ ] 7.3 After 7.2 passes, record T24 as absorbed/superseded without duplicating implementation.

## Next Step

Apply PR6B-3A1 from PR6B-2, then PR6B-3A2 from PR6B-3A1, then close PR6B-3B from PR6B-3A2 after compatible-environment acceptance reconciliation; 6.4 remains the declared-matrix follow-up. Do not change Service authority or claim live integration.
