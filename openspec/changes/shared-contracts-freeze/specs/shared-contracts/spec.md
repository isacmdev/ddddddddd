# Shared Contracts Freeze Specification

## Purpose

Freeze the v1 contracts consumed by Windows Service, App.UI, SessionAgent, and the shared backend before implementation lanes diverge.

## Requirements

### Requirement: v1 wire boundaries and extension semantics are deterministic

The complete UTF-8 envelope MUST be at most 65,536 bytes and its `payload` at most 49,152 bytes. JSON MUST be at most 16 levels deep, arrays at most 256 items, and ordinary strings at most 4,096 UTF-8 bytes. `reason` MUST be optional and, when present, 1–256 bytes; `minutes` MUST be an integer in 1..180. Timestamps MUST be RFC3339 UTC with seconds precision and MUST follow the exhaustive field/origin validation table in `design.md`: client/agent `requested_at`, `created_at`, and `collected_at` use the Service-clock age ≤24h/future ≤300s tolerance; Service/local `observed_at` and `next_attempt_at` are not client supplied; backend `evaluated_at` is authoritative and has no client skew exception. WNS/Realtime hints MUST be opaque, at most 1,024 bytes, and contain only `hint_type` and/or a UUID `correlation_hint`.

The required envelope members are `contract`, `version`, `message_type`, `correlation_id`, and `payload`; unknown members MUST be rejected before side effects unless they are inside the optional `extensions` object. Extension keys MUST use the `x-` namespace, with at most 8 keys and 8,192 bytes; v1 MUST ignore extension values and MUST never treat them as identity, authority, grant, verdict, or secret material. A field unknown to v1 is therefore not an optional extension merely because a newer peer knows it. Canonical examples and negative fixtures are in `fixtures/`; the complete normative member matrix is in `design.md`.

#### Scenario: Optional extension versus unknown required member
- GIVEN a valid v1 envelope with an `x-example` member under `extensions`
- WHEN Windows deserializes it
- THEN it MUST accept the known contract and ignore only that extension
- GIVEN the same envelope with `required_by_newer_peer` outside `extensions`
- WHEN Windows deserializes it
- THEN it MUST reject it without side effects

#### Scenario: Boundary values are enforced
- GIVEN a request with 180 minutes, a 256-byte reason, and a timestamp exactly 300 seconds in the future
- WHEN it is validated
- THEN it MAY proceed subject to the remaining contract rules
- GIVEN 181 minutes, a 257-byte reason, a 301-second future timestamp, or a 1,025-byte hint
- WHEN it is validated
- THEN it MUST be rejected without side effects

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

The Service MUST expose `not_configured`, `activating`, `active`, `degraded`, and `failed`, with a safe `reason_code` matching `^[a-z][a-z0-9_]{0,63}$` and monotonic state version. Unknown or unverifiable state MUST NOT be rendered as active. Transitions MUST be serialized and persisted.

#### Scenario: Activation fails partway
- GIVEN account persistence succeeds but runtime wiring or ACL setup fails
- WHEN activation completes
- THEN state MUST be `degraded` or `failed`, onboarding MUST remain incomplete, and cached enforcement MUST continue

### Requirement: Time requests are durable, idempotent, and truthful

A wire `CreateTimeRequest` MUST include a stable request ID, integer `minutes` in 1..180, optional child-provided reason, T28 `scope`, origin, policy version, informational device ID, and created-at timestamp. It MUST NOT contain local outbox state. The local SQLite/outbox record carries the same request ID plus explicit `outbox_state`; local insertion and outbox intent MUST be atomic. States `queued`, `pending`, `approved`, `denied`, `failed`, and `applied` MUST remain distinguishable. Only an authenticated backend result plus a validated policy/grant snapshot may transition to `approved` and `applied`.

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

The policy snapshot MUST preserve the T01 shape literally: `device_id`, `version`, `device_state` (`active|locked|downtime`), `daily_screen_time_minutes`, `schedules` (`id`, `days` using `MON`…`SUN`, `from`, `to`, `action`, and non-empty `allow_list` for `allow_only`), `category_limits`, `app_policies` (`package_name`, `state`, `daily_limit_minutes`, `category`, `allowed_windows`), `category_assignments` as a `package_name`→`category` map, and grants (`id`, `request_id`, `scope`, `minutes`, `granted_at`, `expires_at`, `source`). It MUST include a real lowercase SHA-256 `snapshot_hash` over the canonical UTF-8 policy payload excluding that field. The client MUST apply only a strictly newer version. Equal-version content mismatch MUST quarantine the snapshot; lower versions and stale responses MUST be ignored. A malformed or unknown policy MUST not replace the last valid policy.

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

WNS and Realtime MUST be opaque, identity-gated synchronization hints of at most 1,024 UTF-8 bytes, containing only an optional 1–32 ASCII-byte `hint_type` and/or UUID `correlation_hint`. Duplicate, delayed, out-of-order, expired, malformed, or unauthorized hints MUST be harmless and coalesced into at most 1,000 intents per device generation, each expiring after 15 minutes. Neither channel may carry or authorize policy, grants, or integrity verdicts.

#### Scenario: Push arrives while offline
- GIVEN an untrusted WNS hint during backend loss
- WHEN it is received
- THEN the Service records at most one of 1,000 coalesced sync intents for that generation, expiring after 15 minutes, and continues cached enforcement; it MUST not apply payload data

### Requirement: Compatibility and privacy are enforced at the boundary

The reader MUST reject unsupported major versions, invalid required fields, unknown enum values, oversized payloads, and unsafe identifiers without side effects. Only additive members inside the namespaced `extensions` object are ignored safely; unknown members elsewhere are rejected. Secrets, child name, reason text, channel URI, pairing code, raw device identifiers, and tokens MUST not appear in logs, IPC diagnostics, or receipts.

#### Scenario: Unknown state from a newer peer
- GIVEN an unknown enum value or newer major contract
- WHEN Windows receives it
- THEN it MUST preserve protection, expose a safe unknown/degraded status, and avoid destructive interpretation
