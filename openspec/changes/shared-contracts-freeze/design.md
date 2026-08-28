# Design: Shared Contracts Freeze v1

## Contract envelope

Every JSON message uses an explicit envelope:

```json
{"contract":"control-parental.windows","version":1,"message_type":"...","correlation_id":"uuid","payload":{}}
```

`version` is the major contract version. The wire limit is **65,536 UTF-8 bytes for the complete envelope** and **49,152 UTF-8 bytes for `payload`** (the smaller limit applies after canonical serialization, before transport framing). JSON nesting is limited to 16 levels, arrays to 256 items, and any string to 4,096 UTF-8 bytes unless a stricter field limit below applies. Readers MUST reject malformed envelopes, unknown required fields, unsupported major versions, invalid enum values, oversized payloads, and missing correlation/idempotency identifiers without applying side effects. Limits are inclusive; boundary values are accepted and the next byte/item/level is rejected.

Required envelope members are exactly `contract`, `version`, `message_type`, `correlation_id`, and `payload`; their types are string, integer, string, UUID string, and object respectively. `contract` MUST equal `control-parental.windows`, `version` MUST equal `1`, and `correlation_id` MUST be a non-zero UUID. Unknown members outside `extensions` are rejected (including a member that a newer peer treats as required). The optional `extensions` object MAY occur at envelope or payload level, is capped at 8 keys and 8,192 UTF-8 bytes, and its keys MUST match `^x-[a-z0-9][a-z0-9._-]{0,63}$`; v1 readers ignore its values without side effects. `extensions` is the only forward-compatible extension point. It MUST NOT contain authority, identity, grant, verdict, or secret material. Serialization uses System.Text.Json source generation and stable snake_case wire names. Enums are strings, never numeric ordinals.

All timestamps are RFC 3339 UTC (`Z`) with seconds precision (fractional seconds are rejected). The following table is exhaustive: the field's origin determines its validation window, and no generic future-skew rule overrides it.

| field | origin | validation rule | authority |
|---|---|---|---|
| `requested_at`, `created_at`, `collected_at` | client/agent | Service clock: age ≤24h and future ≤300s | Service receipt time wins |
| `observed_at`, `next_attempt_at` | Service/local durable state | UTC seconds; not client supplied; `next_attempt_at` may be null | Service clock/scheduler wins |
| `evaluated_at` | backend verdict | UTC seconds; backend-issued; no client age/skew exception | backend verdict wins |

UUIDs are canonical lowercase text; identifiers are never selected from an unvalidated client value.

Field limits: `reason` is optional and, when present, 1–256 UTF-8 bytes; `reason_code` is required wherever the contract requires a reason code and MUST match `^[a-z][a-z0-9_]{0,63}$` (1–64 ASCII bytes); `minutes` is required and an integer from 1 through 180; `policy_version`, `state_version`, and verdict versions are unsigned integers from 0 through 2^63−1. `device_id` is a UUID when present. WNS/Realtime hints are opaque JSON objects of at most 1,024 UTF-8 bytes, containing only optional `hint_type` (1–32 ASCII characters) and `correlation_hint` (UUID); any other member or payload data is rejected. A hint is retained for at most 1,000 coalesced intents per device generation and expires after 15 minutes.

Canonical examples and negative fixtures are in `fixtures/`: `valid-set-protected-account.json` demonstrates an ignored additive extension; `invalid-missing-required.json`, `invalid-unknown-required-member.json`, and `invalid-reason-too-long.json` MUST be rejected before side effects.

## Ownership handoff before parallel lanes

One contractual pre-lane owns the freeze implementation contract before lanes A/B/C begin: **Contract Pre-lane — Domain/JSON/CT owner: `device-sync-identity`**. It owns `src/ControlParental.Domain/**`, every JSON source-generation context and wire fixture, and the CT-01…CT-12 acceptance suite. Lanes A (runtime), B (sync/backend), and C (UI/IPC) consume that pre-lane output and MUST NOT redefine types, wire names, limits, or semantics. Changes discovered downstream return to the pre-lane for contract decision; no lane may silently fork a contract.

Lane D (packaging) invokes `Build-MSIX.ps1` at repository root and may additionally use `build/installer/**`; `build/installer/Build-MSIX.ps1` is not the UI/MSIX entry point.

## Protected account and runtime activation

`SetProtectedAccountRequest` (UI → Service) contains `operation_id`, normalized `username`, normalized Windows `sid`, and `requested_at`; `operation_id` is a non-zero UUID, `username` is 1–256 UTF-8 bytes, and `sid` is 1–184 ASCII bytes matching the Windows SID grammar. It contains no JWT, password, or secret. The Service independently resolves the account and verifies standard-user status. Client-provided username/SID is a selector, not authority.

`SetProtectedAccountResponse` contains the same `operation_id`, `status` (`accepted|pending|rejected|failed`), `activation_state`, `correlation_id`, and safe `reason_code`. The ACK means the selected account is durably committed, not merely received. Repeating the same operation with the same normalized identity returns the same result; reusing the operation ID with different identity is rejected.

`RuntimeActivationState` is `not_configured|activating|active|degraded|failed`, with safe `reason_code`, `observed_at`, and monotonic `state_version`. Transitions are serialized and persisted by the Service. UI MUST show pending/degraded/failed distinctly and MUST NOT advance onboarding on transport success alone. Unknown states fail closed to `failed`/non-active behavior.

## Time request and durable outbox

`CreateTimeRequest` contains stable `request_id`, `scope` (the T28 time-request scope, 1–64 ASCII bytes), `minutes` (1–180), optional `reason` (1–256 UTF-8 bytes and treated as child-provided content), `origin` (`status_page|overlay`), `policy_version`, `device_id` only as an informational consistency check, and `created_at`. The authenticated Service identity is authoritative; a client-supplied `device_id` MUST NOT select another device. `scope` is required on the wire and is not an outbox state field.

The durable local record is `{request_id, device_generation, wire_request, outbox_state, attempt_count, next_attempt_at}` and is keyed by `(device_generation, request_id)`. `outbox_state` is `queued|pending|approved|denied|failed|applied`; it is never serialized as part of the wire request. Retries and restart reconciliation reuse that key. `queued` means not sent; `pending` means accepted for server decision; `approved` is a server decision; `applied` requires a newer, validated policy/grant snapshot containing the grant; `failed` is terminal only for a typed non-retryable error. Timeout, lost response, duplicate response, WNS, or Realtime hint MUST NOT imply approval or application. WNS/Realtime only trigger authenticated pull.

## Normative member matrix

The following is the v1 source-of-truth table. `R` means required, `O` optional, and `—` absent. All names are stable snake_case; enum values are exhaustive and strings, not ordinals. Envelope fields are required for every row.

| message_type | member | R/O | type / format | bounds / enum |
|---|---|---:|---|---|
| `set_protected_account.request` | `operation_id`, `username`, `sid`, `requested_at` | R | UUID; UTF-8 string; ASCII SID; RFC3339 UTC seconds | username 1–256 bytes; SID 1–184 bytes |
| `set_protected_account.request` | `extensions` | O | object | max 8 `x-` keys / 8,192 bytes |
| `set_protected_account.response` | `operation_id`, `status`, `activation_state`, `correlation_id`, `reason_code` | R | UUID; string; string; UUID; safe ASCII code | status `accepted|pending|rejected|failed`; activation `not_configured|activating|active|degraded|failed` |
| `runtime_activation.state` | `activation_state`, `reason_code`, `observed_at`, `state_version` | R | string; safe code; RFC3339; uint64 | state enum above; version 0..2^63−1 |
| `create_time_request` | `request_id`, `scope`, `minutes`, `origin`, `policy_version`, `device_id`, `created_at` | R | UUID; ASCII string; integer; string; uint64; UUID; RFC3339 | scope 1–64 bytes; minutes 1..180; origin `status_page|overlay`; device_id informational only |
| `create_time_request` | `reason`, `extensions` | O | UTF-8 string; object | reason 1–256 bytes; max 8 `x-` keys / 8,192 bytes |
| `time_request.outbox` | `request_id`, `device_generation`, `wire_request`, `outbox_state`, `attempt_count`, `next_attempt_at` | R | UUID; UUID; object; string; uint32; RFC3339/null | state `queued|pending|approved|denied|failed|applied`; local-only, never wire |
| `policy.snapshot` | `device_id`, `version`, `device_state`, `daily_screen_time_minutes`, `schedules`, `category_limits`, `app_policies`, `category_assignments`, `grants`, `snapshot_hash` | R | UUID; uint64; enum; integer; array; array; array; object; array; SHA-256 hex | `device_state` `active|locked|downtime`; minutes 0..1440; collections 0..256 items; hash exactly 64 lowercase hex |
| `policy.snapshot.schedules[]` | `id`, `days`, `from`, `to`, `action`, `allow_list` | R/O | ASCII; enum array; HH:mm; HH:mm; enum; ASCII array | days are unique `MON|TUE|WED|THU|FRI|SAT|SUN`; `action` `lock|allow_only`; `allow_list` required and non-empty for `allow_only`; `from>to` crosses midnight |
| `policy.snapshot.category_limits[]` | `category`, `minutes` | R | ASCII string; integer | category 1..64 bytes; minutes 0..1440; no other members |
| `policy.snapshot.app_policies[]` | `package_name`, `state`, `daily_limit_minutes`, `category`, `allowed_windows` | R/O | ASCII; enum; integer; ASCII; window array | state `allowed|blocked|limited|always_allowed`; limit required for `limited`; windows use `days/from/to` and are optional |
| `policy.snapshot.category_assignments` | package name → category | R | object map | each key is a canonical package name and each value a category; apps absent from the map do not count toward category limits |
| `policy.snapshot.grants[]` | `id`, `request_id`, `scope`, `minutes`, `granted_at`, `expires_at`, `source` | R | string; string; ASCII; integer; RFC3339; RFC3339; enum | `scope` `device|<package_name>|<category>`; minutes 1..180; `expires_at>granted_at`; source `extra_time|reward|manual` |
| `policy.snapshot` canonical hash | `snapshot_hash` | R | lowercase hex string | SHA-256 of canonical UTF-8 JSON of the complete policy payload excluding `snapshot_hash`, stable snake_case, ordinal member order, no whitespace |
| `integrity.evidence` | `evidence_id`, `device_generation`, `agent_version`, `binary_sha256`, `signature_result`, `signer_summary`, `collected_at`, `evidence_schema_version` | R | UUID; UUID; ASCII; SHA-256; enum; redacted ASCII string; RFC3339; uint32 | agent_version 1–128 bytes; signer_summary 1–256 bytes; signature `valid|invalid|unknown`; no paths/credentials |
| `integrity.verdict` | `verdict`, `evidence_id`, `evaluated_at`, `verdict_version`, `reason_code` | R | enum; UUID; RFC3339; uint64; safe ASCII code | verdict `trust|revoked|unknown`; reason_code `^[a-z][a-z0-9_]{0,63}$`; backend timestamp authoritative |
| `wns.hint` / `realtime.hint` | `hint_type`, `correlation_hint` | O | ASCII string; UUID | at least one; hint_type 1–32 bytes; object ≤1,024 bytes; no other members |

## Erratum and precedence (P0.5)

This freeze is the normative contract consumed by the next lanes. For P0.5, the single owner is `device-sync-identity`; its scope is `src/ControlParental.Domain/**`, JSON contexts/fixtures, and CT-01..CT-12, and it runs before lanes A/B/C. Lane D invokes root `Build-MSIX.ps1` and may additionally use `build/installer/**`; the latter is not a replacement entry point. If P0.5 text or manifests disagree, this paragraph and the matrix above govern the Gate; this card does not edit P0.5.

Backend acceptance MUST atomically authorize the request and, on approval, create at most one grant keyed by `request_id`, advance policy version, and expose the result for polling. Duplicate creates return the original outcome. Denial creates no grant. The client applies grants only if `policy.version > local.version`; equal-version hash mismatch is quarantine/degraded, and lower versions are ignored.

## JWT, device identity, and RLS

Pairing creates or activates one definitive device identity generation. The returned access/refresh session is bound to the exact `device_id`; JWT validation requires issuer, audience, signature/algorithm allow-list, `exp`, `nbf` with a 300-second clock-skew allowance, and a non-empty `device_id` claim. The Service rejects a token whose claim differs from its protected generation and never accepts a caller-provided device ID as authority.

All device queries and writes are RLS-scoped from the validated JWT claim (or an equivalent server-side identity mapping). RPCs accepting `device_id` MUST assert it equals the authenticated claim or ignore the argument and derive identity server-side. Two-device tests MUST prove read/write isolation, including policy, grants, requests, outbox/event ingestion, and push-channel registration. Refresh is single-flight, cancellable, subject to a 30-second operation deadline, stale-generation-safe, and revocation forces quarantine/re-pairing. Tokens and refresh material remain in protected storage and never in logs, IPC payloads, receipts, or WNS.

## Integrity evidence and verdict

`IntegrityEvidence` contains a stable `evidence_id`, device generation binding, agent version, binary SHA-256, Authenticode/signature result, signer identity summary, collected-at timestamp, and evidence schema version. It contains no paths that reveal user data and no credentials. The backend returns `IntegrityVerdict` with `verdict` (`trust|revoked|unknown`), `evidence_id`, authoritative `evaluated_at`, verdict version, and safe reason code.

Only an authenticated, current-generation backend verdict is authoritative. `revoked` creates/retains the matching durable integrity issue; `trust` removes only that matching issue; `unknown`, malformed, timeout, cancellation, or unavailable outcomes remain observable without claiming trust and do not weaken cached enforcement. Local signature/hash evidence alone cannot manufacture `trust` or `revoked`.

## WNS and Realtime

WNS payloads are opaque hints of at most 1,024 UTF-8 bytes containing only `hint_type` (1–32 ASCII bytes) and/or `correlation_hint` (UUID). Realtime events use the same shape. They never carry a policy, grant, verdict, or authority. Duplicates, stale hints, out-of-order hints, disconnects, and expiry coalesce into at most 1,000 sync intents per device generation, each expiring after 15 minutes, and trigger one authenticated Service sync. Channel registration is Service-owned, identity-gated, idempotent, and redacted; expired channels are removed only after server authorization.

## Privacy and observability

Logs expose status, safe reason code, correlation ID, contract/version, device-generation hash, and timing—not JWTs, refresh tokens, pairing codes, channel URIs, publishable keys, request reason text, child name, or raw identifiers. Every receipt is local-only unless an external acceptance process supplies a separate receipt.
