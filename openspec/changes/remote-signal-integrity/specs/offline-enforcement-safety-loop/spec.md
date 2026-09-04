# Delta for Offline Enforcement Safety Loop

## MODIFIED Requirements

### Requirement: Heartbeat, health, and durable issues reflect authoritative truth

Heartbeats MUST identify the actual session and agent generation and MUST contain only measured or authoritative values. Missing, malformed, stale, or generation-mismatched heartbeats MUST degrade health and MUST NOT refresh liveness. Effective health MUST derive from restore status, heartbeat freshness, current critical outcomes, required overlay or action confirmation, durable issues, supported safety signals, and definitive integrity verdicts. Recovery MUST require authoritative recovery evidence; transient or malformed integrity states MUST NOT degrade protection. Issues MUST be durable, deduplicated by stable semantic identity, auditable, and resolved only by matching authoritative evidence. Integrity state MUST survive restart and lifecycle transitions without claiming external completion.
(Previously: Heartbeat, health, and durable issues reflected authoritative truth for supported safety signals but did not include definitive integrity verdicts or their recovery semantics.)

#### Scenario: Supported safety evidence degrades health

- GIVEN a stale heartbeat, clock/timezone change, agent death, child-admin evidence, or definitive revoked integrity verdict
- WHEN health is derived
- THEN health MUST reflect the authoritative current condition
- AND one correctly scoped durable issue MUST be persisted

#### Scenario: Semantic issue recovery survives restart

- GIVEN repeated evidence for one semantic issue followed by authoritative recovery
- WHEN issue state is persisted and restored
- THEN no duplicate active issue MUST exist
- AND the resolved state MUST survive restart

#### Scenario: Transient integrity failure does not degrade protection

- GIVEN integrity reporting is unavailable, cancelled, malformed, or transient
- WHEN health and enforcement are derived
- THEN protection MUST remain non-degraded and no false recovery or trust MUST be recorded

#### Scenario: Concurrent recovery is serialized

- GIVEN restart, integrity verdict, agent death, and enforcement events race
- WHEN state transitions are processed
- THEN one deterministic session order MUST preserve current issues, health, and enforcement authority
