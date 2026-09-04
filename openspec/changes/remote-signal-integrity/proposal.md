# Proposal: Remote Signal Integrity (SDD7)

## Intent

Complete the runtime contract for remote signals and integrity without treating current partial code or historical “done” claims as integrated completion. WNS and foreground Realtime should accelerate convergence; integrity evidence must produce authenticated, definitive, and fail-safe enforcement decisions.

## Scope

### In Scope
- T19: opaque WNS hints, durable registration/reconciliation, and visible authenticated `TriggerSync` admission into the SDD6 scheduler.
- T21: foreground-only UI Realtime acceleration, lifecycle/reconnect behavior, and typed UI refresh without Service/REST authority.
- T23 runtime integrity: hash/signature evidence with an authoritative reference, authenticated reporting, definitive verdict policy, trust recovery/removal, and enforcement integration.
- Resolve overlapping legacy/new WNS paths and prove DI, lifecycle, restart, coalescing, identity gating, bounded retry, and single-flight behavior.

### Out of Scope
- IL obfuscation, AOT/R2R publishing, signing pipeline, Release packaging evidence, or signed Windows-matrix receipts; defer to SDD8/release work.
- Live backend, WNS identity/fan-out, Realtime service, or external readiness claims.

## Capabilities

### New Capabilities
- `remote-signal-sync`: Opaque WNS and permitted UI hints converge through durable, identity-gated, bounded SDD6 scheduler admission with polling fallback.
- `foreground-realtime-acceleration`: Foreground-only Realtime subscription provides UI acceleration and never owns synchronization, retry, or identity policy.
- `runtime-integrity`: Authenticated local evidence and remote verdicts flow through `BackendClient`, verdict policy, trust recovery, and enforcement.

### Modified Capabilities
- `offline-enforcement-safety-loop`: Integrate definitive integrity verdicts and authoritative recovery into durable enforcement issues and health; transient or malformed remote states MUST NOT degrade protection.

## Approach

Keep adapters thin: untrusted WNS/Realtime signals enter one SDD6 durable, identity-gated, bounded, single-flight scheduler; polling remains eventual-convergence fallback. Integrity remains a parallel checker → authenticated `BackendClient` → verdict policy → enforcement flow. Establish hash-reference semantics and recovery before claiming mismatch detection. Preserve SDD5 contract-first dependency and SDD6 local approval; neither is externally complete.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| App.UI WNS/Realtime adapters | Modified | Opaque signals, foreground lifecycle, typed IPC/UI refresh. |
| Service scheduler, UI dispatch, identity, integrity, enforcement | Modified | Admission, authenticated evidence, verdicts, recovery, composition. |
| Tests and evidence manifests | Modified | Local harness/contract evidence and receipt classification. |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Signal overlap or false integrity degradation | Med | One scheduler; definitive verdicts only; grace, hysteresis, circuit-breaker, and recovery tests. |
| External dependency mistaken for proof | High | Separate local receipts from backend/WNS/Realtime/signed-Windows receipts; forbid `live-integrated`, `client-ready`, or `ExternalVerified` without them. |

## Rollback Plan

Disable signal-triggered admission and integrity enforcement reactions while retaining polling, durable registration/outbox state, diagnostics, and local checks. Revert the SDD7 adapter, dispatch, scheduler, and verdict-policy changes independently; preserve schema/data and restart-safe recovery.

## Dependencies

- SDD5 contract-first baseline and SDD6 local-approved scheduler/durable-sync contracts; external completion remains unclaimed.

## Success Criteria

- [ ] WNS `TriggerSync` visibly reaches authenticated scheduler admission; WNS, startup, timer, and UI hints coalesce without direct adapter REST/retry ownership.
- [ ] Realtime is foreground-only and non-authoritative; polling converges when signals are absent.
- [ ] Hash/signature evidence, authenticated reporting, definitive verdicts, trust recovery, and enforcement effects are locally proven without transient false degradation.
- [ ] Evidence labels distinguish locally provable harness success from required external receipts; no forbidden readiness claim is made.
