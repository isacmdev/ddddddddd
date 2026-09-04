# Exploration: remote-signal-integrity (SDD7)

## Current State

The verified roadmap scope is **T19 WNS push plus polling fallback, T21 foreground-only UI Realtime, and T23 local/remote integrity**. The backlog marks T19, T21, and T23 done, but the current evidence does not support treating that as integrated completion: the readiness audit still classifies T19, T21, and T23 as pending/external, and SDD5 explicitly permits only contract-first downstream work until external receipts exist.

The runtime direction is already partly present:

- **WNS**: `WnsPushNotificationHandler` treats raw payloads as opaque and sends `TriggerSync` over the UI named pipe; `WnsRegistrationCoordinator` durably stores one bounded registration intent, gates backend delivery on `IBackendIdentityCoordinator`, and performs one startup reconciliation. `WnsNotificationService` separately contains a legacy/service-side OAuth/channel implementation. The current `UIMessageHandler` dispatch shown by CodeGraph handles `RegisterWnsChannel` but has no `TriggerSync` case, and the queried `ScheduledWorkService` exposes timers but no visible signal-triggered policy-sync admission. This is the most important T19-to-SDD6 integration gap.
- **Realtime**: `RealtimeSubscriber` and `RealtimeChannelAdapter` keep Supabase Realtime in App.UI, subscribe/unsubscribe on foreground/background, and convert broadcasts to typed policy/grant events. It is correctly positioned as a UI refresh accelerator, not a Service control channel. The subscriber currently fire-and-forgets lifecycle connect/disconnect callbacks and does not itself converge through the durable sync owner.
- **Integrity**: `IntegrityChecker` computes SHA-256 and verifies Authenticode through an injected WinTrust seam. `AntiTamperMonitor` runs the check at startup and every 30 seconds, posts `IntegrityReport` through `BackendClient`, and passes local/server outcomes to `IntegrityVerdictHandler`. The handler has timeout-graceful, threshold, hysteresis, escalation, startup grace, staged, shadow-mode, and circuit-breaker behavior; transient backend failures do not degrade enforcement. However, local hash computation is reported but no trusted baseline/mismatch comparison is visible in the current flow, and trust recovery is logged rather than visibly removing the enforcement issue.
- **Identity and durable sync**: `BackendIdentityCoordinator` is the definitive authority; `BackendClient` owns authenticated transport and bounded request retry. SDD6 assigns `ScheduledWorkService` ownership of admission, backoff, cancellation, and single-flight work, with durable outbox/reconciliation state below it. Remote hints must therefore enter a bounded scheduler trigger, never call REST directly from WNS/Realtime/UI adapters.

### Roadmap and contradiction audit

The backlog is authoritative for the grouping and T19/T21/T23 requirements, including the T23 eight false-positive mechanisms and the T14 response-body verdict. Historical T23 artifacts are not authoritative where they conflict with current code or later backlog evidence:

- `t14-gap-analysis.md` records the earlier `Task<bool>`/missing-verdict gap; current `BackendClient` returns `IntegrityReportResult` and parses `verdict`, so that document is historical.
- T23 `design.md` says server verdict handling was unknown and proposes a simpler local-first mechanism; current code has `IntegrityVerdictHandler` with the later eight-mechanism policy, so the design is superseded for behavior but still useful for identifying intended boundaries.
- T23 `tasks.md` says “ALL DONE” while explicitly leaving IL obfuscation blocked and Release/full-output verification blocked. Current code contains no obfuscator evidence, no external verdict receipt, and the readiness audit says remote integrity is unconfirmed. Therefore T23 is substantial local groundwork, not externally verified completion.
- T23 artifacts also claim local hash mismatch handling, but the current `AntiTamperMonitor` only branches locally on `IsSignatureValid`; a future proposal must specify what trusted reference makes a hash mismatch meaningful.

## Affected Areas

- `src/ControlParental.App.UI/WnsPushNotificationHandler.cs` — WNS lifecycle, opaque notification-to-IPC signal, channel creation/renewal, and registration adapter.
- `src/ControlParental.Service/WnsRegistrationCoordinator.cs` — durable, identity-gated channel registration and startup reconciliation; should remain separate from sync-trigger policy.
- `src/ControlParental.Service/UIMessageHandler.cs` — authenticated UI pipe dispatch; current evidence lacks a `TriggerSync` route.
- `src/ControlParental.Service/ScheduledWorkService.cs` — SDD6 scheduler owner; needs a bounded, single-flight signal admission path reusable by WNS and any UI-originated refresh hint.
- `src/ControlParental.Service/BackendClient.cs` and `src/ControlParental.Domain/IBackendClient.cs` — authenticated backend seam, identity generation authorization, integrity report/verdict contract, and no caller-owned retry.
- `src/ControlParental.Service/BackendIdentityCoordinator.cs` — definitive identity/capability gate for every remote operation.
- `src/ControlParental.Service/IntegrityChecker.cs`, `AntiTamperMonitor.cs`, `IntegrityVerdictHandler.cs` — local evidence, periodic reporting, verdict policy, and enforcement reaction.
- `src/ControlParental.App.UI/RealtimeSubscriber.cs` and `RealtimeChannelAdapter.cs` — foreground-only UI acceleration; must not acquire sync/retry authority.
- `src/ControlParental.Service/Program.cs` and hosted-service composition — startup ordering, DI lifetimes, identity initialization before remote work, and one scheduler owner.
- `tests/ControlParental.App.UI.Tests/WnsLifecycleTests.cs`, `RealtimeSubscriberTests.cs`, `tests/ControlParental.Service.Tests/WnsHostedServiceTests.cs`, `IntegrityCheckerTests.cs`, `IntegrityVerdictHandlerTests.cs`, `BackendClientTests.cs`, scheduler/identity tests — existing seams are useful, but the cross-component convergence and current TriggerSync dispatch need explicit coverage.
- `openspec/changes/offline-sync-recovery/specs/offline-sync-recovery/spec.md` and `design.md` — normative SDD6 constraints: durable admission, identity gating, bounded retry, single-flight, restart safety, and offline continuity.

## Approaches

1. **Signal adapters directly invoke REST/sync** — let WNS IPC or Realtime handlers call policy fetch immediately.
   - Pros: smallest apparent latency path; little scheduler plumbing.
   - Cons: violates SDD6 retry/admission ownership, permits overlap and retry amplification, couples UI/WNS to identity and backend policy, and makes remote failure behavior inconsistent.
   - Effort: Low initially, High to repair safely.

2. **Canonical signal-to-scheduler convergence** — normalize WNS and optional UI-originated Realtime hints into a typed, bounded trigger; `ScheduledWorkService` coalesces/single-flights policy sync, while polling remains the eventual-convergence source. Integrity remains a separate periodic/authenticated report path, with definitive verdict reactions handled by its policy component and no degradation on transient transport failure.
   - Pros: preserves SDD6 invariants, keeps adapters thin, supports offline startup and fallback polling, and gives one place to test bounds/cancellation/identity admission.
   - Cons: requires completing the missing TriggerSync dispatch and adding explicit trigger/coalescing contracts; Realtime UI refresh must distinguish display refresh from authoritative policy application.
   - Effort: Medium.

3. **Backend verdict/push-driven orchestration** — make WNS or Realtime carry integrity/policy truth and use remote messages as the primary control path.
   - Pros: potentially lowest remote latency.
   - Cons: contradicts the backlog’s opaque WNS rule and foreground-only Realtime rule, increases trust surface, and fails when the UI/push channel is unavailable.
   - Effort: High.

## Recommendation

Use **Approach 2**. First close the concrete WNS signal path: authenticated UI-pipe `TriggerSync` dispatch must call the existing SDD6 scheduler admission, not `BackendClient` directly. Coalesce WNS, startup, timer, and any permitted UI hint into the existing scheduler; preserve polling as fallback and durable policy-version guards as the authority. Keep Realtime foreground-only and limited to UI invalidation/refresh notification; it may request the same bounded refresh intent but must never own retry, identity, or synchronization policy.

Treat integrity as a parallel authenticated evidence flow: local checker → `BackendClient.ReportIntegrityAsync` → verdict handler → existing enforcement-level pipeline. Define the authoritative hash reference and trust-recovery removal semantics before implementation. A remote `revoked` verdict can influence enforcement only through the handler’s definitive policy; timeout, unavailable backend, malformed/absent verdict, and circuit-open states remain observable but non-degrading. All remote calls continue through the definitive identity generation and SDD6 bounded transport seam.

Review-sized implementation slices:

1. **Trigger contract and dispatch** — add/verify typed `TriggerSync` routing, authenticated pipe boundary, scheduler coalescing, cancellation, and tests proving no direct REST/retry ownership in adapters.
2. **WNS convergence** — connect the existing handler/coordinator to the scheduler, preserve durable registration/reconciliation, polling fallback, renewal, and identity-denied/offline outcomes.
3. **Realtime UI convergence** — prove foreground-only subscription, background close, reconnect failure tolerance, typed UI invalidation, and no Service/REST dependency.
4. **Integrity semantics** — settle hash baseline/mismatch and trust recovery, then test local failure, definitive revoked/trust sequences, transient failures, startup grace, and enforcement effects through the existing pipeline.
5. **Composition and evidence** — verify startup ordering/DI lifetimes and contract-first harness behavior; keep external backend, WNS identity/fan-out, Realtime service, signing/AOT/obfuscation, and Windows-matrix claims in a separate evidence gate.

## Risks

- The WNS `TriggerSync` message is defined and emitted, but the current dispatch evidence does not route it to scheduler admission; assuming it works would create a false completion claim.
- `WnsNotificationService` and the newer App.UI WNS handler/coordinator represent overlapping generations; proposal work must choose the active runtime path and quarantine legacy paths without widening scope.
- Integrity hash reporting without an authoritative expected hash can detect/report but cannot prove local tampering by comparison; signature validity alone is insufficient for the stated hash requirement.
- `IntegrityVerdictHandler` is singleton stateful policy, while `AntiTamperMonitor` is hosted/runtime state; composition and restart semantics need explicit tests.
- Existing T23 “done” claims conflict with blocked obfuscation/Release evidence and with external-readiness status; do not carry those claims into proposal or verification.
- WNS identity/MSIX, backend JWT/RLS/verdict, Realtime delivery, signed artifacts, and Windows-version behavior require external receipts; local mocks/harnesses prove contracts only.
- Remote signals can arrive concurrently with polling, startup, Task Scheduler, or shutdown; without scheduler coalescing they can amplify work or race identity generations.

## Ready for Proposal

**Yes, with explicit proposal constraints.** The proposal should name the SDD7 runtime boundary as “untrusted acceleration signals converging on SDD6 durable sync,” make the missing `TriggerSync` dispatch and integrity hash/recovery semantics first-class gaps, preserve identity-gated authenticated transport, and split local contract acceptance from external backend/WNS/Realtime/Windows-matrix evidence. It should not claim live-integrated, client-ready, or `ExternalVerified` status without external receipts.
