# Proposal: Windows Runtime Foundations

## Intent

### Problem
The Windows runtime is single-session, has blocking lifecycle paths, unframed IPC, incomplete reconnect, and ambiguous health/ACL failures. Green unit tests do not prove safe simultaneous enforcement or local recovery.

### Outcome
Provide isolated enforcement, authenticated Service–SessionAgent IPC, degraded safety behavior, and backend-independent SCM recovery.

## Scope

### In Scope
- **T37:** authoritative privilege verdicts, idempotent ACL hardening, health integration, and onboarding health blocking when privilege is `Unknown` or ACL repair fails.
- **T38:** multi-session Service/SessionAgent lifecycle, per-session ownership, bounded framed IPC, cancellation, reconnect, and mutual authentication using Service PID plus Authenticode identity.
- **T10-A:** local SCM startup/failure actions and crash/relaunch recovery, independent of backend services.
- Each work unit targets >80% line coverage for changed code and reports branch coverage.

### Out of Scope
- T10-B, T03, T05–T09, T11+, scheduler, usage, backend, UI pipe host, WNS, Realtime, warning cleanup, packaging, and general refactors.
- Per-session secrets unless new evidence changes the approved decision.

## Capabilities

### New Capabilities
- `windows-runtime-hardening`: privilege/ACL verdicts and degraded health.
- `windows-session-runtime`: isolated multi-session lifecycle and local recovery.
- `authenticated-session-ipc`: framing, reconnect, and mutual identity validation.

### Modified Capabilities
- None; no existing specs.

## Approach

Repair existing runtime seams; avoid a replacement supervisor or broad redesign. Serialize ownership/recovery, await cancellation, preserve the UI pipe contract, bind connections to session and Service identity, and enforce the `DEGRADED` onboarding rule.

Work units remain behavioral slices, not detailed tasks. If the forecast exceeds 800 authored lines, use high-level auto-chain and split by behavior.

## Affected Areas

| Area | Impact |
|---|---|
| Service runtime | T37/T10-A verdicts, lifecycle, health, SCM |
| Service/SessionAgent transport | Isolation, framing, auth, reconnect |
| Tests and Windows harnesses | Safety and recovery evidence |

## Dependencies

- Windows SCM/WTS APIs, process identity, and Authenticode verification; existing .NET, xUnit, and Coverlet setup.

## Success Criteria

- [ ] Simultaneous sessions have isolated channels with no cross-session delivery or duplicate agent.
- [ ] Framing, size bounds, cancellation, authorization, disconnect, and reconnect preserve session binding.
- [ ] Service and agent validate PID/Authenticode trust without a per-session secret.
- [ ] `Unknown` privilege or ACL failure keeps enforcement active, publishes `DEGRADED`, and blocks healthy onboarding.
- [ ] Hardening/SCM setup is idempotent; crash/relaunch proves one service instance and backend independence.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Lifecycle/security race | High | Ownership, focused tests, Windows harness |
| Review exceeds 800 lines | High | Auto-chain; split by behavior |

## Rollback Plan

Revert each slice with its tests at the affected seams, restoring prior IPC/lifecycle or T37/SCM behavior. Preserve unrelated working-tree, UI, and backend code.
