# Proposal: Shared Contracts Freeze

## Intent

Freeze the cross-process and backend-facing contracts required before parallel Windows lanes start. This is a specification-only change: it does not implement pairing, runtime activation, time requests, synchronization, JWT, RLS, WNS, or integrity verification.

## Authority and scope

The backlog `backlog-control-parental-windows.md` remains product authority. The Service owns device identity, durable state, synchronization, enforcement, and authoritative outcomes. App.UI and SessionAgent render or request; they never manufacture a backend decision. WNS and Realtime are signals only.

In scope:

- Versioned JSON contracts for protected-account selection/ACK, runtime activation, time requests/outbox, and integrity evidence/verdict.
- Serialization, compatibility, unknown-state, privacy, idempotency, ordering, and failure semantics.
- JWT/device binding, RLS assumptions, monotonic policy application, and backend-facing acceptance tests.
- Explicit `child_first_name` decision compatible with the backlog.

Out of scope:

- Source-code implementation or migrations.
- Changes to the backlog, existing APIs, or archived receipts.
- Live Supabase, WNS, Realtime, staging, or Windows-matrix claims.

## Decision: child_first_name

`child_first_name` is not a required Windows pairing input. The tutor/backend owns it as optional pairing metadata; the Windows agent sends the backlog-defined `code`, device metadata, and required `age_band`, but never invents or derives a child's name. A backend may return a redacted/display-safe name only if already authorized. Missing name MUST NOT block pairing, policy sync, or enforcement. This preserves the backlog T24 request shape and prevents unnecessary child PII collection.

## Readiness boundary

Local contract tests may establish `contract-first` conformance only. They MUST NOT claim `backend-integrated`, live JWT/RLS, WNS fan-out, or `ExternalVerified=true`.
