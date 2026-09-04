# Design: Shared Contracts Freeze v1

## Contract envelope

Every JSON message uses an explicit envelope:

```json
{"contract":"control-parental.windows","version":1,"message_type":"...","correlation_id":"uuid","payload":{}}
```

`version` is the major contract version. Readers MUST reject malformed envelopes, unknown required fields, unsupported major versions, invalid enum values, oversized payloads, and missing correlation/idempotency identifiers without applying side effects. Additive optional fields are forward-compatible; a major change requires a new version. Serialization uses System.Text.Json source generation and stable snake_case wire names. Enums are strings, never numeric ordinals.

## Protected account and runtime activation

`SetProtectedAccountRequest` (UI → Service) contains `operation_id`, normalized `username`, normalized Windows `sid`, and `requested_at`; it contains no JWT, password, or secret. The Service independently resolves the account and verifies standard-user status. Client-provided username/SID is a selector, not authority.

`SetProtectedAccountResponse` contains the same `operation_id`, `status` (`accepted|pending|rejected|failed`), `activation_state`, `correlation_id`, and safe `reason_code`. The ACK means the selected account is durably committed, not merely received. Repeating the same operation with the same normalized identity returns the same result; reusing the operation ID with different identity is rejected.

`RuntimeActivationState` is `not_configured|activating|active|degraded|failed`, with safe `reason_code`, `observed_at`, and monotonic `state_version`. Transitions are serialized and persisted by the Service. UI MUST show pending/degraded/failed distinctly and MUST NOT advance onboarding on transport success alone. Unknown states fail closed to `failed`/non-active behavior.

## Time request and durable outbox

`CreateTimeRequest` contains stable `request_id`, `minutes` (bounded positive integer), optional `reason` (length bounded and treated as child-provided content), `origin` (`status_page|overlay`), `policy_version`, `device_id` only as an informational consistency check, and `created_at`. The authenticated Service identity is authoritative; a client-supplied `device_id` MUST NOT select another device.

The durable local state is `queued|pending|approved|denied|failed|applied`. A request is inserted with its outbox intent in one SQLite transaction, keyed by `(device_generation, request_id)`. Retries and restart reconciliation reuse that key. `queued` means not sent; `pending` means accepted for server decision; `approved` is a server decision; `applied` requires a newer, validated policy/grant snapshot containing the grant; `failed` is terminal only for a typed non-retryable error. Timeout, lost response, duplicate response, WNS, or Realtime hint MUST NOT imply approval or application. WNS/Realtime only trigger authenticated pull.

Backend acceptance MUST atomically authorize the request and, on approval, create at most one grant keyed by `request_id`, advance policy version, and expose the result for polling. Duplicate creates return the original outcome. Denial creates no grant. The client applies grants only if `policy.version > local.version`; equal-version hash mismatch is quarantine/degraded, and lower versions are ignored.

## JWT, device identity, and RLS

Pairing creates or activates one definitive device identity generation. The returned access/refresh session is bound to the exact `device_id`; JWT validation requires issuer, audience, signature/algorithm allow-list, `exp`, `nbf` with bounded clock skew, and a non-empty `device_id` claim. The Service rejects a token whose claim differs from its protected generation and never accepts a caller-provided device ID as authority.

All device queries and writes are RLS-scoped from the validated JWT claim (or an equivalent server-side identity mapping). RPCs accepting `device_id` MUST assert it equals the authenticated claim or ignore the argument and derive identity server-side. Two-device tests MUST prove read/write isolation, including policy, grants, requests, outbox/event ingestion, and push-channel registration. Refresh is single-flight, cancellable, bounded, stale-generation-safe, and revocation forces quarantine/re-pairing. Tokens and refresh material remain in protected storage and never in logs, IPC payloads, receipts, or WNS.

## Integrity evidence and verdict

`IntegrityEvidence` contains a stable `evidence_id`, device generation binding, agent version, binary SHA-256, Authenticode/signature result, signer identity summary, collected-at timestamp, and evidence schema version. It contains no paths that reveal user data and no credentials. The backend returns `IntegrityVerdict` with `verdict` (`trust|revoked|unknown`), `evidence_id`, authoritative `evaluated_at`, verdict version, and safe reason code.

Only an authenticated, current-generation backend verdict is authoritative. `revoked` creates/retains the matching durable integrity issue; `trust` removes only that matching issue; `unknown`, malformed, timeout, cancellation, or unavailable outcomes remain observable without claiming trust and do not weaken cached enforcement. Local signature/hash evidence alone cannot manufacture `trust` or `revoked`.

## WNS and Realtime

WNS payloads are opaque bounded hints containing at most a type and correlation hint. They never carry a policy, grant, verdict, or authority. Realtime events follow the same rule. Duplicates, stale hints, out-of-order hints, disconnects, and expiry coalesce into one authenticated Service sync. Channel registration is Service-owned, identity-gated, idempotent, and redacted; expired channels are removed only after server authorization.

## Privacy and observability

Logs expose status, safe reason code, correlation ID, contract/version, device-generation hash, and timing—not JWTs, refresh tokens, pairing codes, channel URIs, publishable keys, request reason text, child name, or raw identifiers. Every receipt is local-only unless an external acceptance process supplies a separate receipt.
