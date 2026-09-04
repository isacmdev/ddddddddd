# Delta for Offline Enforcement Safety Loop

## MODIFIED Requirements

### Requirement: Heartbeat, health, and durable issues reflect authoritative truth

Heartbeats MUST identify the actual session and generation. Effective health MUST derive from restore status, liveness, critical outcomes, durable issues, safety signals, and definitive integrity verdicts. Issues MUST be durable and deduplicated by stable semantic identity, while each accepted report remains an observation unless a stable event ID proves duplication. Recovery MUST require authoritative evidence. (Previously: Heartbeats and issues covered supported safety signals and definitive integrity, but not accepted-observation ordering or durable escalation state.)

#### Scenario: Supported safety evidence degrades health
- GIVEN stale heartbeat, agent death, child-admin evidence, or definitive revoked evidence
- WHEN health is derived
- THEN current health and one correctly scoped issue MUST be persisted

#### Scenario: [C1B] Semantic issue recovery uses resolution
- GIVEN repeated evidence for one issue followed by authoritative recovery
- WHEN state is restored
- THEN one active issue identity and its resolved state MUST remain durable, and recovery MUST call ResolveIssue rather than AddIssue

#### Scenario: [C1A] Transient integrity failure does not degrade protection
- GIVEN integrity reporting is unavailable, cancelled, malformed, or transient
- WHEN health and enforcement are derived
- THEN definitive state, counters, deadline, and effective enforcement MUST be preserved

#### Scenario: [C1B] Agent-death and enforcement race
- GIVEN agent death, restart, integrity verdict, and enforcement race concurrently
- WHEN events are accepted
- THEN deterministic linearization MUST retain one stable issue and prevent stale downgrade or recovery

## ADDED Requirements

### Requirement: Durable revoked escalation is exact and one-shot

The durable owner MUST persist and rehydrate the complete accepted state and pending effect identity before effects are committed. First, second, and third consecutive definitive revoked observations MUST produce WARN, LIMIT, and LIMIT-with-pending-notification respectively. The third MUST persist exactly one deadline origin/due pair and notify admin exactly once; later revoked observations MUST NOT extend that persisted pair. Definitive trust before degradation cancels the streak and deadline; after degradation, three consecutive definitive trust observations MUST recover once, and revoked resets that recovery count. Collaborator work and effects MUST be outside state locks, and faults, cancellation, Stop, and Dispose MUST be observed.

#### Scenario: [C1B] First through fourth revoked
- GIVEN no trust interrupt
- WHEN the first, second, third, then fourth definitive revoked observations are accepted
- THEN phases are WARN, LIMIT, pending LIMIT with one notification/deadline, and unchanged pending state

#### Scenario: [C1C] Owner timer callback at each deadline boundary
- GIVEN a persisted Pending deadline and an owner timer callback
- WHEN the actual callback observes time before, at, or after the due time, including repeated callbacks
- THEN before due it MUST preserve Pending, at/after due it MUST degrade exactly once, and repeats MUST emit no second effect

#### Scenario: [C1C] Trust cancellation and stale timer callback
- GIVEN a Pending deadline and a definitive trust observation before due
- WHEN trust is persisted and the captured old-generation timer callback later runs during normal operation or shutdown
- THEN the deadline MUST be cancelled, no degradation MUST occur, and the stale callback MUST be suppressed with callback faults observed

#### Scenario: [C1B] Save-before-effect ordering
- GIVEN an accepted decision with a reaction or notification effect identity
- WHEN the owner processes the decision
- THEN accepted state and the pending identity MUST be durably saved before that effect is invoked

#### Scenario: [C1B] Crash cut resumes pending reaction independently
- GIVEN a process crash occurs after a pending reaction identity was saved and before reaction completion
- WHEN the owner rehydrates and reconciles
- THEN it MUST resume that reaction identity once without replaying completed notification progress

#### Scenario: [C1B] Crash cut resumes pending notification independently
- GIVEN a process crash occurs after reaction completion and a pending notification identity was saved
- WHEN the owner rehydrates and reconciles
- THEN it MUST resume that notification identity once without replaying the completed reaction

#### Scenario: [C1B] Three-trust recovery
- GIVEN degraded state
- WHEN three consecutive definitive trust observations arrive, with no revoked interruption
- THEN the owner emits one authoritative recovery; a revoked observation resets the trust count

### Requirement: Durable timing and ownership fail closed

Active countdown MUST use monotonic elapsed time. A forward jump reaching the deadline MUST expire it. Wall-clock rollback or an unusable persisted timing basis MUST NOT extend a pending deadline; using only the existing MaxWallClockSeenUtc and TimingValid contract, the owner MUST fail closed and degrade once rather than persist an invalid Pending state. Deadline evaluation MUST reach a production-used durable enforcement owner before Unit 5; Unit 5 MAY compose receipts but MUST NOT define first ownership. Persistence/effect crashes MUST converge idempotently. The production DI graph MUST register one Service owner/store wiring; no retry, migration, cross-process guarantee, or new framework is implied.

#### Scenario: [C1C] Restart and crash convergence
- GIVEN pending state or a crash between persistence and external effect
- WHEN the process restarts
- THEN the original deadline resumes without a new five minutes or extension, monotonic relative countdown remains authoritative, and repeated reconciliation converges to one effect

#### Scenario: [C1C] Forward or rollback clock change
- GIVEN a pending deadline and a forward jump, rollback, or unusable timing basis
- WHEN timing is evaluated
- THEN forward expiry degrades once, while rollback MUST fail closed immediately without extension and invalid timing MUST NOT remain Pending

#### Scenario: [C1B] Rehydrate fault, effect fault, and lifecycle cancellation
- GIVEN a non-cancellation rehydrate fault, save/effect fault, cancellation, Stop, or Dispose
- WHEN reconciliation or an effect is in progress
- THEN non-cancellation faults MUST be observable to enforcement, cancellation MUST propagate, cleanup MUST complete, and no stale or duplicate effect MUST be emitted

#### Scenario: [C2] Production composition, startup, and integrated runtime gate
- GIVEN the production Service DI composition is built
- WHEN it resolves the singleton escalation store and AntiTamperMonitor and starts the owner
- THEN startup rehydration MUST complete before remote acceptance, cleanup MUST release owner resources, and the integrated Unit4 runtime gate MUST exercise the composed path rather than synthetic registrations
