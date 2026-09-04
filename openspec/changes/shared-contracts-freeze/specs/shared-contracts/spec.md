# Shared Contracts Freeze Specification

## Purpose

Freeze the v1 contracts consumed by Windows Service, App.UI, SessionAgent, and the shared backend before implementation lanes diverge.

## Requirements

### Requirement: Protected account selection has durable acknowledgement

The Service MUST validate and durably persist the normalized standard Windows account before returning an accepted ACK. The request MUST be idempotent by operation ID and identity; conflicting reuse MUST be rejected. A transport response MUST NOT be treated as durable success.

#### Scenario: ACK survives restart
- GIVEN a valid standard account selection
- WHEN the Service commits it and returns `accepted`
- THEN a restart MUST restore the same account and operation result

#### Scenario: Invalid account or conflicting retry
- GIVEN an administrator, unknown account, mismatched SID, or reused operation ID with different data
- WHEN the request is processed
- THEN it MUST return a typed rejection and MUST NOT change the durable selection

### Requirement: Runtime activation is explicit and monotonic

The Service MUST expose `not_configured`, `activating`, `active`, `degraded`, and `failed`, with a safe reason and monotonic state version. Unknown or unverifiable state MUST NOT be rendered as active. Transitions MUST be serialized and persisted.

#### Scenario: Activation fails partway
- GIVEN account persistence succeeds but runtime wiring or ACL setup fails
- WHEN activation completes
- THEN state MUST be `degraded` or `failed`, onboarding MUST remain incomplete, and cached enforcement MUST continue

### Requirement: Time requests are durable, idempotent, and truthful

A `CreateTimeRequest` MUST include a stable request ID, bounded minutes, origin, policy version, and explicit outbox state. Local insertion and outbox intent MUST be atomic. States `queued`, `pending`, `approved`, `denied`, `failed`, and `applied` MUST remain distinguishable. Only an authenticated backend result plus a validated policy/grant snapshot may transition to `approved` and `applied`.

#### Scenario: Offline retry and lost response
- GIVEN a queued request, disconnect, retry, and lost response
- WHEN connectivity returns
- THEN one server request and at most one grant MUST result; no client signal may imply approval

#### Scenario: Denial and application failure
- GIVEN a server denial or an approved result whose grant cannot yet be applied
- WHEN the client updates state
- THEN it MUST show `denied` or remain `approved`/`failed`, never `applied` without grant evidence

### Requirement: Device identity is JWT-bound and RLS-enforced

The Service MUST accept only a definitive generation whose validated JWT has correct issuer, audience, signature, expiry, clock-skew rules, and exact `device_id` claim. RLS and RPC authorization MUST derive identity from the authenticated claim, not trust a client-supplied ID. Rotation, revocation, stale responses, and refresh races MUST fail closed.

#### Scenario: Two-device isolation
- GIVEN two valid device sessions
- WHEN either session reads or writes the other device's policy, grant, request, event, or push channel
- THEN RLS or server authorization MUST deny it without data disclosure

### Requirement: Policy application is monotonic and complete

The policy snapshot MUST preserve the backlog-defined device state, limits, schedules, app policies, category assignments, grants, device ID, version, and a canonical `snapshot_hash` over the versioned payload. The client MUST apply only a strictly newer version. Equal-version content mismatch MUST quarantine the snapshot; lower versions and stale responses MUST be ignored. A malformed or unknown policy MUST not replace the last valid policy.

#### Scenario: Out-of-order synchronization
- GIVEN snapshots v7, v9, and delayed v8
- WHEN they arrive in any order
- THEN v9 remains authoritative locally and v8 cannot roll it back

### Requirement: Integrity verdicts remain backend-authoritative

The client MUST submit typed integrity evidence through the authenticated definitive session and distinguish `trust`, `revoked`, and `unknown`. Local evidence, WNS, Realtime, timeout, or malformed responses MUST NOT manufacture an authoritative verdict. Unknown/transient outcomes preserve safe cached enforcement and remain observable.

#### Scenario: Revocation and recovery
- GIVEN a current-generation `revoked` verdict followed by `trust` for the same evidence scope
- WHEN the Service evaluates them
- THEN it MUST retain one matching durable issue for revocation and remove only that issue after authoritative trust

### Requirement: WNS and Realtime are signal-only

WNS and Realtime MUST be bounded, opaque, identity-gated synchronization hints. Duplicate, delayed, out-of-order, expired, malformed, or unauthorized hints MUST be harmless and coalesced. Neither channel may carry or authorize policy, grants, or integrity verdicts.

#### Scenario: Push arrives while offline
- GIVEN an untrusted WNS hint during backend loss
- WHEN it is received
- THEN the Service records at most a bounded sync intent and continues cached enforcement; it MUST not apply payload data

### Requirement: Compatibility and privacy are enforced at the boundary

The reader MUST reject unsupported major versions, invalid required fields, unknown enum values, oversized payloads, and unsafe identifiers without side effects. Optional additive fields are ignored safely. Secrets, child name, reason text, channel URI, pairing code, raw device identifiers, and tokens MUST not appear in logs, IPC diagnostics, or receipts.

#### Scenario: Unknown state from a newer peer
- GIVEN an unknown enum value or newer major contract
- WHEN Windows receives it
- THEN it MUST preserve protection, expose a safe unknown/degraded status, and avoid destructive interpretation
