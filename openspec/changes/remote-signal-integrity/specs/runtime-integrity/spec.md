# Runtime Integrity Specification

## Purpose

Collect authenticated local integrity evidence and apply only definitive remote trust decisions, within runtime-integrity scope.

## Requirements

### Requirement: Integrity evidence and verdicts are authoritative only when definitive

The system MUST compare the executable hash with an authoritative hash reference, record signature/hash evidence, and report it through authenticated identity-gated transport. Trust, revoked, and unknown verdicts MUST be distinct; malformed, unavailable, cancelled, or transient results MUST NOT degrade enforcement. Trust recovery MUST remove only the matching durable integrity issue; revoked evidence MUST create or retain it. State MUST be explicit across startup, restart, and monitor lifecycle.

#### Scenario: Valid evidence receives trust

- GIVEN signature and hash match the authoritative reference
- WHEN an authenticated report returns trust
- THEN evidence MUST be recorded and any matching issue MUST resolve authoritatively

#### Scenario: Revocation changes enforcement

- GIVEN validly authenticated evidence receives revoked
- WHEN verdict policy evaluates it
- THEN one durable integrity issue MUST be active and enforcement MUST apply the definitive reaction

#### Scenario: Unknown or transient response is non-degrading

- GIVEN a missing, unknown, malformed, unavailable, timeout, or cancelled response
- WHEN policy evaluates it
- THEN the state MUST be observable without falsely degrading protection or claiming trust

#### Scenario: Restart and recovery preserve semantics

- GIVEN monitoring restarts after revoked or transient state
- WHEN authoritative evidence later returns trust or removal
- THEN lifecycle state MUST restore deterministically and only the matching issue MUST recover

#### Scenario: Deferred release scope is not claimed

- GIVEN local contract evidence exists without signed Windows-matrix or external receipts
- WHEN readiness is reported
- THEN it MUST remain contract-first and MUST NOT claim live-integrated, client-ready, or ExternalVerified
