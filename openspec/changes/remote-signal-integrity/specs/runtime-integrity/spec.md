# Delta for Runtime Integrity

## MODIFIED Requirements

### Requirement: Integrity evidence and verdicts are authoritative only when definitive

The system MUST compare executable evidence with an authoritative reference and report it through authenticated identity-gated transport. Trust, revoked, and non-definitive outcomes MUST remain distinct. Each accepted authenticated definitive report MUST be one observation unless a stable backend event ID proves duplication; equal payload and timestamp alone MUST NOT deduplicate it. Durable issue identity MUST be deduplicated separately. Non-definitive outcomes MUST preserve definitive sequence, counters, deadline, and effective enforcement. Before accepting a remote observation, the owner MUST rehydrate durable state; it MUST save accepted state and effect identity before any reaction or notification. (Previously: Definitive evidence controlled issues and enforcement, but observation identity and preservation rules were unspecified.)

#### Scenario: [C1A] Definitive classification precedes mutation
- GIVEN matching evidence and an authenticated trust report
- WHEN the report is classified and accepted
- THEN definitive classification MUST complete before epoch or sequence mutation, and the matching issue MUST resolve authoritatively without claiming external completion

#### Scenario: [C1A] Equal timestamp conflicting verdicts
- GIVEN authenticated trust and revoked reports have equal backend timestamps
- WHEN both are accepted
- THEN ingress sequence MUST determine their order and the later definitive state MUST be authoritative

#### Scenario: [C1A] Failed, cancelled, malformed, and non-definitive input preserves durable state
- GIVEN a missing, unknown, malformed, unavailable, timeout, or cancelled response
- WHEN policy evaluates it
- THEN epoch, sequence, revoked/trust streaks, phase, deadline origin/due, wall-clock/timing fields, latches, effect identities, and effective enforcement MUST remain unchanged

#### Scenario: [C1A] Snapshot, restore, and recovery preserve semantic phase
- GIVEN monitoring restarts after revoked or transient state
- WHEN the snapshot is restored and authoritative evidence later returns trust
- THEN the semantic phase and definitive counters MUST be preserved, invalid timing/rollback state MUST NOT restore as Pending or extend a deadline, and only the matching durable issue MUST recover deterministically

#### Scenario: [C1B] Missing state is a valid first start
- GIVEN no durable state file exists for the identity on first start
- WHEN startup initializes the owner and a remote observation arrives
- THEN the owner MUST establish the contract's Normal baseline and MAY accept the observation

#### Scenario: [C1B] Corrupt or unavailable state fails closed
- GIVEN a persisted document is corrupt, unsupported, unavailable, or rehydration is cancelled
- WHEN a remote observation is received or startup reconciliation runs
- THEN no observation or effect MUST be accepted from unrehydrated state, protection MUST fail closed, and cancellation MUST propagate

## ADDED Requirements

### Requirement: Accepted observations have deterministic ingress authority

At acceptance, the owner MUST assign and persist a monotonic ingress sequence to every accepted authenticated report. This sequence MUST linearize concurrent or equal-timestamp inputs; backend timestamps MAY provide audit or staleness metadata but MUST NOT be the sole authority. Every transition MUST advance an epoch, and stale epoch, sequence, generation, or effect-progress work MUST NOT replay or overwrite an older effect. The owner MUST own monotonic admission because the existing store provides only LoadAsync/SaveAsync, per-instance serialization, and atomic replacement, not cross-process or store-monotonic guarantees. Collaborator work MUST occur outside state locks, and every fault or cancellation MUST be observed.

#### Scenario: [C1A] Concurrent mixed definitive order
- GIVEN concurrent accepted revoked and trust reports
- WHEN acceptance completes in sequence order
- THEN state MUST reflect that order and stale work MUST NOT overwrite it

#### Scenario: [C1B] Stale generation, effect, lifecycle, and callback fault
- GIVEN an older callback is pending when a newer epoch commits
- WHEN the callback runs or faults
- THEN stale generation/effect/lifecycle work MUST be suppressed, and the fault or cancellation MUST be observed without corrupting committed state

#### Scenario: [C1B] Key and pure-handler boundary
- GIVEN a keyed decision reaches the production owner
- WHEN reaction and notification are dispatched
- THEN VerdictDecision.ReactionIdempotencyKey and NotificationIdempotencyKey MUST be forwarded unchanged, no keyed recovery key MUST be invented, the handler MUST remain pure, and AntiTamperMonitor MUST be the sole async owner
