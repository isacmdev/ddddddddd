# Proposal: Shared Contracts Freeze

## Intent

Freeze and implement the cross-process and backend-facing contracts required before parallel Windows lanes start. The Contract Pre-lane owns source-generated Domain types, JSON contexts, fixtures, and CT-01..CT-12; no parallel lane may be created until this lane is integrated into the canonical base.

## Authority and scope

The backlog `backlog-control-parental-windows.md` remains product authority. The Service owns device identity, durable state, synchronization, enforcement, and authoritative outcomes. App.UI and SessionAgent render or request; they never manufacture a backend decision. WNS and Realtime are signals only.

In scope:

- Versioned JSON contracts for protected-account selection/ACK, runtime activation, time requests/outbox, and integrity evidence/verdict.
- Serialization, compatibility, unknown-state, privacy, idempotency, ordering, and failure semantics.
- JWT/device binding, RLS assumptions, monotonic policy application, and backend-facing acceptance tests.
- Explicit `child_first_name` decision compatible with the backlog.

Out of scope:

- Backend migrations and live external integration (the Domain contract implementation and local acceptance suite are in scope).
- Changes to the backlog, existing APIs, or archived receipts.
- Live Supabase, WNS, Realtime, staging, or Windows-matrix claims.

## Decision: child_first_name

`child_first_name` is not a required Windows pairing input. The tutor/backend owns it as optional pairing metadata; the Windows agent sends the backlog-defined `code`, device metadata, and required `age_band`, but never invents or derives a child's name. A backend may return a redacted/display-safe name only if already authorized. Missing name MUST NOT block pairing, policy sync, or enforcement. This preserves the backlog T24 request shape and prevents unnecessary child PII collection.

## Readiness boundary

Local contract tests may establish `contract-first` conformance only. They MUST NOT claim `backend-integrated`, live JWT/RLS, WNS fan-out, or `ExternalVerified=true`.

## Handoff and bounded wire contract

Before lanes A/B/C start, the single Contract Pre-lane (`device-sync-identity`) owns `src/ControlParental.Domain/**`, all JSON contexts/fixtures, and CT-01 through CT-12. Those lanes consume the frozen output and cannot introduce parallel definitions. Lane D uses the repository-root `Build-MSIX.ps1`; `build/installer/**` is an additional installer scope, not a replacement path.

The v1 wire contract is bounded: 65,536 UTF-8 bytes per envelope, 49,152 bytes per payload, 16 nesting levels, 256 array items, 4,096 bytes per ordinary string, 256 bytes for `reason`, 1–180 for `minutes`, RFC3339 UTC seconds timestamps with a 300-second future validation tolerance and 24-hour maximum age, and 1,024-byte complete WNS/Realtime hint messages measured before transport framing. Unknown members are rejected unless they are namespaced under optional `extensions` (`x-...`, max 8 keys/8,192 bytes), whose contents are ignored and never authoritative. Canonical valid and invalid JSON examples live under `fixtures/`; field-level rules are normative in `design.md`, with no alternate future-skew rule.
