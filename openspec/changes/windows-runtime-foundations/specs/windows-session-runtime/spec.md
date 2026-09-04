# Windows Session Runtime Specification

## Purpose

Define cancellable, isolated child-session enforcement and backend-independent local recovery.

## Requirements

### Requirement: Session lifecycle is cancellable and per-session

The system MUST start and stop session observation without blocking normal startup or shutdown. It MUST maintain independent ownership for every simultaneous child session, including fast switching, and MUST prevent delivery, launch, or termination actions from crossing session boundaries.

#### Scenario: Start and stop complete safely

- GIVEN session observation is requested or cancellation is signaled
- WHEN start or stop is invoked
- THEN the operation returns after owned work is started or stopped, and cancellation completes without an unbounded wait

#### Scenario: Simultaneous sessions and fast switching

- GIVEN two child sessions exist or the active session changes rapidly
- WHEN session events are observed
- THEN each session retains isolated enforcement ownership and no session receives another session's actions

### Requirement: Launch and local recovery are single-instance and idempotent

The system MUST serialize launch, relaunch, stop, and recovery ownership per session. Relaunch after agent loss MUST converge to at most one agent for that session, and concurrent recovery signals MUST produce one in-flight recovery attempt.

#### Scenario: Agent launch or relaunch

- GIVEN a session starts or its agent exits unexpectedly
- WHEN launch or relaunch is requested
- THEN the session has no duplicate agent and recovery preserves its session ownership

#### Scenario: SCM setup and crash recovery

- GIVEN local service startup or failure-action configuration is repeated
- WHEN the service crashes and local SCM recovery relaunches it
- THEN configuration remains equivalent, one service instance is active, and recovery succeeds without backend services

### Requirement: Work-unit evidence proves changed behavior

Each implementation work unit MUST provide line coverage greater than 80% for its new or modified production code and MUST report branch coverage. Evidence MUST include the focused behavioral results for the covered scenarios.

#### Scenario: Coverage evidence is reviewed

- GIVEN a work unit changes production code
- WHEN its verification evidence is produced
- THEN line coverage is strictly greater than 80% for that changed scope and branch coverage is reported
