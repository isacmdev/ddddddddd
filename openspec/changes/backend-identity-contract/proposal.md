# Proposal: Backend Identity Contract

## Intent

Close SDD5 as **client-ready** while the shared backend is unavailable. Deliver a clean, fail-closed client against an executable v1 contract without asserting live backend integration. The backlog explicitly states that this client consumes the existing shared backend; it does not build it. This amendment records the approved release-validation boundary without changing that original intent or history.

## Scope

### In Scope
- Full client implementation: v1 contract; deterministic state machine; pairing/JWT/exact `device_id` binding; atomic protected persistence; authenticated REST/refresh; fail-closed TLS policy; production composition; and WNS registration through Service IPC.
- Strict RED→GREEN→TRIANGULATE→REFACTOR, clean architecture, deterministic contract/security/concurrency tests, real Appium/WinAppDriver E2E for affected UI, and quality/coverage/performance/security gates. Release validation is limited to Windows 10 22H2 x64 (legacy), Windows 11 24H2 x64, and Windows 11 25H2 x64.
- 6.4 remains pending until all three approved x64 cells have unchanged E2E, clean-install/runtime, cleanup/redaction, and hash evidence.
- Local external-acceptance package: manifest, deterministic contract harness, redacted receipt schema, and exact future execution runbook. All local receipts expose immutable `ExternalVerified=false`.
- Absorb T24 client evidence without duplicating its implementation.

### Out of Scope
- Building or changing the backend, parent app, QR/unpair UX, or speculative frameworks.
- Live RLS/JWT/TLS and WNS fan-out acceptance. These move to `backend-integration-acceptance`, which MUST NOT reimplement client behavior except for narrowly proven contractual incompatibilities.
- Any `backend-integrated` or live-availability claim.
- No backend/JWT/RLS/TLS/WNS fan-out claim is made by this change.
- Windows 11 23H2, removed because official historical media/update is unavailable and its lifecycle is near or at retirement.
- ARM64 enablement, which is compile-intent only, unverified and unsupported, non-blocking here, and deferred to a separate future initiative; no ARM64 compatibility claim. x86, Windows Server, and S Mode also remain outside scope.

## Capabilities

### New Capabilities
- `backend-identity-contract`: Client-ready identity lifecycle, secure transport/composition, Service-owned WNS IPC, deterministic harness, and external-acceptance handoff.

### Modified Capabilities
None. `offline-enforcement-safety-loop` remains authoritative and unchanged.

## Approach

Domain owns typed invariants; Application owns transitions and single-flight generations; Infrastructure owns HTTP/DPAPI/TLS; UI sees typed outcomes and uses Service IPC. Require exact JWT/device binding, bounded retries/timeouts/cancellation/idempotency, atomic commits, and redacted diagnostics. SDD6/SDD7 may proceed contract-first against harnesses, but MUST remain not live-integrated until external acceptance. Any defect found during matrix work becomes a separate strict RED→GREEN→REFACTOR work unit with changed-scope coverage >80% and complexity, efficiency, and clean-architecture review.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `src/ControlParental.Domain/` | Modified | States, invariants, ports, typed outcomes |
| `src/ControlParental.Service/` | Modified | Coordinator, auth, persistence, REST/TLS, WNS IPC, composition |
| `src/ControlParental.App.UI/` | Modified | Route WNS registration through Service IPC |
| `tests/` and `openspec/changes/backend-identity-contract/evidence/` | New/Modified | TDD, real UI E2E, gates, manifest/schema/runbook |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Harness mistaken for live proof | High | Immutable false receipt flag and separate readiness states |
| Replay, refresh races, secret/TLS leakage | High | Generations, atomic storage, redaction, security negatives |
| Future backend incompatibility | Med | Versioned manifest/runbook; narrow follow-up fixes only |

## Rollback Plan

Disable remote activation and WNS registration, quarantine uncertain credentials, and revert by work unit. Preserve cached offline enforcement; uncertainty, timeout, replay, and unacknowledged actions remain fail-closed.

## Dependencies

- Shared backend availability is deferred; `backend-integration-acceptance` later consumes the acceptance package.
- Approved matrix evidence is the prerequisite for 6.4; it does not establish external verification.

## Success Criteria

### Client-ready — Required Now
- [ ] Full client scope and production composition pass strict TDD, deterministic harness, real UI E2E, clean-architecture, coverage (>80% changed lines), branch, performance, security, and redaction gates.
- [ ] Acceptance manifest, harness, receipt schema, and exact runbook are complete; every local receipt has immutable `ExternalVerified=false`.
- [ ] Offline enforcement and rollback remain fail-closed.
- [ ] The present release claim is limited to the three approved x64 cells; Win10 remains a legacy/EOL caveat, ARM64 is a future roadmap item, and no evidence is fabricated.

### Backend-integrated — Deferred
- [ ] `backend-integration-acceptance` executes live RLS/JWT/TLS/WNS fan-out checks and records redacted external receipts. This is explicitly outside SDD5 closure.
