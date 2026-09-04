# Offline Enforcement Safety Loop Specification

## Purpose

Define trustworthy offline enforcement for T08, T09, T11, T12, and only the already-observable T13 clock/timezone, agent-death, and child-admin signals. The change MUST NOT add uninstall, ACL, registry, or other OS-wide watchers.

## Requirements

### Requirement: Enforcement authority and ordering are session-scoped

Each interactive Windows session MUST have exactly one logical enforcement authority. It MUST serialize restoration, observations, reevaluations, critical commands, results, and health transitions in deterministic order. Queueing MUST be bounded. Equivalent work MAY be coalesced only when no required reevaluation, intent transition, or action can be lost, and critical work MUST NOT be starved.

The agent-command connection MUST be replaceable without discarding enforcement state. Commands, intents, and results MUST carry generation, session, command, and intent correlation sufficient to establish current authority. Late, duplicate, wrong-session, wrong-generation, and superseded results MUST NOT alter enforcement, overlay, issue, or health state.

#### Scenario: Concurrent reconnect and enforcement are serialized

- GIVEN threshold, heartbeat, and reconnect events race with a critical command
- WHEN results arrive out of order
- THEN one deterministic session order MUST result
- AND stale or mismatched outcomes MUST be suppressed

#### Scenario: Queue saturation preserves required work

- GIVEN foreground observations and reevaluation ticks exceed the bounded queue capacity
- WHEN equivalent work is coalesced
- THEN the latest required state MUST still be evaluated
- AND no critical action or distinct intent transition MUST be lost

### Requirement: Application identity and process targets remain distinct

Canonical `AppId` MUST remain the policy and reporting identity. Process termination MUST target only the exact observed foreground process instance using its PID, Windows session, and process start-time or equivalent instance evidence. The system MUST NOT terminate by application name or `AppId` and MUST fail safely if the target exited, changed session, or cannot be distinguished from PID reuse.

#### Scenario: PID is reused before termination

- GIVEN a blocked application's observed process exits and its PID is reused
- WHEN exact-target termination is attempted
- THEN the replacement process MUST NOT be terminated
- AND the outcome MUST remain unconfirmed or failed

### Requirement: Critical actions have typed confirmed outcomes

Overlay show, replace, and clear operations, exact-process termination, and `LockWorkStation` MUST produce typed outcomes distinguishing confirmed success, timeout, connection replacement, stale result, invalid target or state, harmless absence where applicable, and native or access failure. Timeout, missing acknowledgement, or agent death MUST NOT be interpreted as success. Protection MUST be reported only from a confirmed current outcome.

Retries for uncertain critical outcomes MUST be bounded and correlated to the current intent. Uncertainty MUST remain visible and MUST NOT become success through retry, reconnection, or reporting.

#### Scenario: Critical action completion is reported truthfully

- GIVEN an overlay, termination, or workstation lock is required
- WHEN execution succeeds, times out, fails natively, loses its agent, lacks an acknowledgement, or returns stale
- THEN the typed outcome MUST reflect exactly that result
- AND protection MUST NOT be reported unless current success is confirmed

### Requirement: Restored state converges and thresholds are reevaluated

Persisted enforcement state, overlay intent, durable issues, and required policy and usage state MUST be restored before healthy protection is reported. Restore failure MUST degrade health rather than silently create a healthy empty state.

Required overlay intent MUST survive foreground changes, service restart, agent death, and reconnect until authoritative policy state permits clearing. A replacement agent MUST converge to current intent and MUST NOT replay obsolete commands.

Usage thresholds MUST be reevaluated while the same application remains foreground. Reevaluation MUST NOT depend on another foreground-change event. Policy, usage, target, restore, and connection changes MUST invalidate affected derived state.

#### Scenario: Restart converges persistent overlay intent

- GIVEN a restart with persisted enforcement or overlay intent
- WHEN restoration and agent reconnection complete
- THEN the current intent MUST converge before healthy status is reported
- AND obsolete overlay commands MUST NOT be replayed

#### Scenario: Same application crosses its threshold

- GIVEN the same application remains foreground below its usage limit
- WHEN bounded reevaluation observes that the limit has been crossed
- THEN enforcement MUST begin without a foreground transition

### Requirement: Heartbeat, health, and durable issues reflect authoritative truth

Heartbeats MUST identify the actual session and agent generation and MUST contain only measured or authoritative values. Missing, malformed, stale, or generation-mismatched heartbeats MUST degrade health and MUST NOT refresh liveness.

Effective health MUST derive from restore status, heartbeat freshness, current critical outcomes, required overlay or action confirmation, durable issues, and supported safety signals. Recovery MUST require authoritative recovery evidence.

Issues MUST be durable and deduplicated by stable semantic identity rather than message text or timestamps. Repeated evidence MUST update one active issue; different sessions or causes MUST remain distinct. Resolution MUST be persisted and auditable.

Clock or timezone changes MUST trigger serialized reevaluation so stale wall-clock decisions cannot remain effective. Agent death MUST invalidate pending command authority and degrade health until a replacement is established. Child-admin changes MUST update the corresponding issue and health state. No additional T13 watcher is required by this change.

#### Scenario: Supported safety evidence degrades health

- GIVEN a stale heartbeat, clock or timezone change, agent death, or child-admin evidence
- WHEN health is derived
- THEN health MUST reflect the authoritative current condition
- AND one correctly scoped durable issue MUST be persisted

#### Scenario: Semantic issue recovery survives restart

- GIVEN repeated evidence for one semantic issue followed by authoritative recovery
- WHEN issue state is persisted and restored
- THEN no duplicate active issue MUST exist
- AND the resolved state MUST survive restart

### Requirement: Hot-path work is bounded and non-overlapping

Foreground handling MUST use bounded lookups and MUST NOT perform per-event full user or application scans or database materialization. Scheduling MUST prevent overlapping ticks and blocking process waits. Hot-path time, allocations, queue bounds, and timer behavior MUST be measured. Optimizations MUST NOT introduce stale caches, missed thresholds, timer-drift errors, or unsafe over-coalescing.

#### Scenario: Sustained activity remains bounded

- GIVEN sustained foreground observations, reevaluation ticks, and reconnect activity
- WHEN enforcement executes
- THEN work MUST remain non-overlapping and within documented complexity, allocation, and queue bounds
- AND required reevaluations and actions MUST NOT be missed

### Requirement: Verification and closure provide current evidence

Delivery MUST follow strict RED/GREEN/REFACTOR TDD and cover normal, race, timeout, native-failure, agent-death, reconnect, duplicate, stale-result, restart, PID-reuse, and recovery paths end to end.

Changed-scope line coverage MUST exceed 80%, and branch coverage MUST be reported. Evidence MUST include supported Windows runtime execution for overlay lifecycle, exact termination, `LockWorkStation`, heartbeat loss and recovery, and reconnect behavior.

Canonical closure MUST record current results, complexity and allocation evidence, optimization-risk tests, residual risks, and Windows runtime evidence. It MUST clearly separate historical evidence and MUST contain no contradictory completion status.

#### Scenario: Change is eligible for clean closure

- GIVEN implementation and verification are complete
- WHEN the change is prepared for closure
- THEN strict TDD evidence, greater-than-80-percent changed-scope line coverage, branch reporting, and supported Windows runtime evidence MUST be present
- AND the canonical record MUST present one clean, non-contradictory current status
