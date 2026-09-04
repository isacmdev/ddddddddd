# Backend Identity Contract Specification

## Purpose

Define client-ready identity, transport, WNS IPC, offline safety, and acceptance handoff without claiming live integration.

## Requirements

### Requirement: Service authority owns atomic identity

The Service MUST solely authorize `Unpaired → PrePairSession → PairingPending → DefinitiveSession`. Pairing evidence, JWT claims, and backend identity MUST bind to the exact `device_id`. Credentials MUST commit as one protected atomic generation. Secrets MUST NOT enter outcomes, logs, receipts, or diagnostics.

#### Scenario: Definitive identity activates
- GIVEN valid pairing evidence and exact JWT/device binding
- WHEN the Service authorizes activation
- THEN one authoritative generation MUST become definitive

#### Scenario: Binding is inconsistent
- GIVEN inconsistent evidence or migration, corruption, write, ACL, or protection failure
- WHEN identity is activated, loaded, or used
- THEN partial state MUST be quarantined and access denied without secret disclosure

### Requirement: Pairing resists ambiguity and abuse

Pairing MUST enforce timeout, expiry, single use, bounded attempts, typed 404/410/429, and `Retry-After`. Lost responses MUST use reconciliation or a fresh code. Retries MUST resist replay, duplication, and enumeration idempotently.

#### Scenario: Pairing is uncertain
- GIVEN reuse, loss, timeout, 404, 410, or 429
- WHEN pairing resolves
- THEN success MUST NOT be assumed and bounded recovery MUST apply without disclosure

### Requirement: Authenticated transport and composition fail closed

REST and refresh MUST authenticate the definitive generation. Refresh MUST be skew-aware, cancellable, bounded, and single-flight. Retries MUST use bounded jittered backoff, timeouts, cancellation, and idempotency. Rotation, expiry, reconnect, and rejection MUST prevent overlap. TLS MUST validate certificate and host without fallback; pins MUST permit safe rotation overlap. Production composition MUST select these controls and no service-role credential.

#### Scenario: Concurrent refresh succeeds
- GIVEN concurrent refresh demand
- WHEN the current generation refreshes
- THEN one authenticated operation MUST serve all waiters

#### Scenario: Response becomes stale
- GIVEN authority advances during an in-flight operation
- WHEN an older result arrives
- THEN current credentials MUST remain unchanged

#### Scenario: Trust validation fails
- GIVEN invalid certificate, host, pin, identity, or production configuration
- WHEN remote access is attempted
- THEN access MUST fail without fallback or disclosure

### Requirement: WNS registration is Service-owned

The UI MUST request WNS registration through authenticated Service IPC and MUST NOT own channel credentials. Migration MUST pass the unchanged WnsRegistration E2E through real App.UI→Service IPC on each required x64 cell: Windows 10 22H2 x64 (legacy), Windows 11 24H2 x64, and Windows 11 25H2 x64. Each cell MUST produce exact typed safe outcomes, finite test/session/command timeouts, W3C cleanup, stable payload and harness hashes, redacted XML/log/PNG evidence, and exact OS edition, display version, build, and native architecture. WinAppDriver MUST be attempted first where applicable; NovaWindows2 MAY be used only after a recorded WinAppDriver failure, with unchanged assertions. Live WNS fan-out proof is deferred.

#### Scenario: Migrated UI is accepted
- GIVEN a required x64 cell and an interactive desktop
- WHEN unchanged WnsRegistration E2E requests registration through real App.UI→Service IPC
- THEN typed safe outcomes, finite timeouts, W3C cleanup, stable hashes, and redacted XML/log/PNG evidence MUST be recorded with exact OS identity

#### Scenario: Driver fallback preserves coverage
- GIVEN WinAppDriver fails and the failure is recorded
- WHEN NovaWindows2 reruns the same WnsRegistration E2E
- THEN assertions MUST remain unchanged and the cell MUST retain the same evidence requirements

#### Scenario: Unsupported platforms do not expand the claim
- GIVEN ARM64, x86, Windows Server, S Mode, or Windows 11 23H2
- WHEN matrix acceptance is evaluated
- THEN the platform MUST be excluded from this change; ARM64 MUST remain deferred, unverified, unsupported, non-blocking, and without a compatibility claim

### Requirement: Offline behavior remains fail closed

Backend loss MUST NOT disable cached enforcement, weaken rollback, or convert timeout, replay, uncertainty, or unacknowledged actions into success.

#### Scenario: Remote outcome is unknown
- GIVEN backend loss or an unacknowledged action with cached enforcement
- WHEN offline behavior resolves uncertainty
- THEN enforcement MUST continue from the safe prior state and uncertainty remain visible

### Requirement: Client-ready produces an acceptance package

The client MUST produce a versioned acceptance manifest, deterministic contract harness, redacted receipt schema, and exact future execution runbook. Every local receipt MUST expose immutable `ExternalVerified=false`. Mocks and harnesses MAY prove local contract conformance but MUST NOT claim backend, JWT, RLS, TLS, or WNS fan-out verification. The required matrix evidence MUST NOT be substituted by compile, static, or unit tests; coverage above 80% MUST NOT replace native runtime evidence. Win10 22H2 remains a legacy/EOL caveat.

#### Scenario: Local conformance runs
- GIVEN controlled identity, time, errors, and concurrency
- WHEN the deterministic harness executes
- THEN results MUST be reproducible and labeled local-only

#### Scenario: Client-ready closes
- GIVEN complete local evidence and no live receipts
- WHEN readiness is reported
- THEN `ExternalVerified` MUST remain false

#### Scenario: Future acceptance executes
- GIVEN an available external environment
- WHEN `backend-integration-acceptance` runs later
- THEN live JWT, RLS, TLS, and WNS fan-out receipts MAY be recorded

### Requirement: Downstream status preserves integration truth

SDD6/SDD7 MAY develop contract-first against the harness, but MUST NOT be declared live-integrated without external receipts from `backend-integration-acceptance`.

#### Scenario: Downstream work uses the harness
- GIVEN no live external receipts
- WHEN SDD6/SDD7 consume local evidence
- THEN their status MUST remain contract-first and not live-integrated

### Requirement: Verification proves bounded clean behavior

Verification MUST use strict RED→GREEN→TRIANGULATE→REFACTOR, exceed 80% changed-scope line coverage, and report branches. Negative, concurrency, cancellation, security, no-secrets, complexity, and efficiency checks MUST be verifiable. If a product or harness defect is found during matrix execution, it MUST be handled as a separate strict RED→GREEN→REFACTOR work unit; changed-scope line coverage MUST exceed 80% with a branch report before the affected cell is rerun. Behavior MUST preserve O(1) state operations, O(bytes) crypto/serialization, bounded O(n) batches and O(k) retries, without unbounded polling, scans, blocking waits, N+1 access, or overlap.

#### Scenario: Quality evidence passes
- GIVEN strict TDD evidence
- WHEN required positive, negative, concurrency, and no-secrets suites run
- THEN changed-scope lines MUST exceed 80% and branches be reported

#### Scenario: Bounds or evidence fail
- GIVEN missing evidence, leaked secrets, or violated complexity bounds
- WHEN client-ready completion is evaluated
- THEN verification MUST fail

#### Scenario: Matrix defect is corrected
- GIVEN a product or harness defect blocks an affected native cell
- WHEN the separate TDD work unit is completed
- THEN RED→GREEN→REFACTOR, changed-scope line coverage above 80%, and a branch report MUST precede rerunning that cell
