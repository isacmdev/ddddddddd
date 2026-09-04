# Remote Signal Sync Specification

## Purpose

Converge untrusted remote/UI hints through the SDD6 durable scheduler without transferring authority to adapters.

## Requirements

### Requirement: Hints admit bounded durable convergence

WNS payloads MUST be treated as opaque, untrusted hints. Accepted WNS/UI hints MUST enter one authenticated, identity-gated, bounded sync admission that coalesces concurrent, startup, timer, and polling triggers into single-flight work. Polling MUST remain an eventual-convergence fallback. Adapters MUST NOT own transport retry, identity, or authoritative policy application.

#### Scenario: Concurrent hints coalesce

- GIVEN startup, WNS, UI, timer, and polling hints arrive concurrently
- WHEN admission processes them
- THEN bounded single-flight sync MUST result and no required convergence MUST be lost

#### Scenario: Offline or cancelled admission recovers

- GIVEN identity, backend, or shutdown cancellation prevents sync
- WHEN connectivity or startup returns
- THEN durable state MUST remain safe and polling/reconciliation MUST retry through the scheduler

#### Scenario: Malformed or unauthorized hint is harmless

- GIVEN an opaque, malformed, stale, or identity-denied notification
- WHEN it reaches the boundary
- THEN it MUST be rejected or reduced to a bounded hint without policy application or adapter-owned retry

#### Scenario: Registration survives lifecycle changes

- GIVEN WNS registration is renewed, expired, revoked, offline, or restarted
- WHEN reconciliation runs
- THEN one redacted, bounded, idempotent durable intent MUST converge only after identity authorization
