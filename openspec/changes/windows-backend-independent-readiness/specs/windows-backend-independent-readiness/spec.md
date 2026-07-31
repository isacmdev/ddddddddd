# windows-backend-independent-readiness Specification

## Purpose

This change makes the current Windows stack safer to run without freezing backend contracts. It tightens the App.UI WNS receive path, service scheduler/session behavior, outbox processing, local integrity evidence, and JSON handling around the APIs that already exist, while keeping backend routes, DTOs, and verdict payloads deferred.

## Requirements

### Requirement: App.UI WNS receipt triggers Service sync

The App.UI WNS handler MUST treat incoming pushes as opaque sync hints and MUST send `TriggerSync` to the Service through the existing IPC path when a push is received. If the PushNotifications contract or `WNS_AAD_APP_ID` is unavailable, App.UI MUST disable WNS without crashing. Push payload contents MUST NOT be required for sync decisions.

#### Scenario: Raw push arrives while IPC is available

- **GIVEN** App.UI has an active IPC channel to the Service
- **WHEN** a WNS push is received
- **THEN** App.UI sends `TriggerSync` and completes the WNS deferral

#### Scenario: WNS support is unavailable

- **GIVEN** the runtime does not expose the PushNotifications contract or `WNS_AAD_APP_ID` is invalid
- **WHEN** App.UI starts
- **THEN** WNS receive setup is skipped
- **AND** the app remains usable

### Requirement: Scheduler and session state are truthful

Service scheduling MUST allow heartbeat, outbox push, reconciliation, and policy sync to become runnable from startup; initial gating state MUST NOT permanently suppress work. Service session/auth state MUST distinguish authenticated, refresh-needed, and unavailable conditions, and backend-bound work MUST use the persisted authenticated device identity rather than a placeholder. If identity is not available, the work item MUST not be reported as successful.

#### Scenario: First scheduled work runs after startup

- **GIVEN** the Service has just started
- **WHEN** the first scheduled policy sync or recurring work window fires
- **THEN** the work executes once using the persisted identity or returns a non-success outcome

#### Scenario: Session metadata is stale

- **GIVEN** persisted session metadata is expired or incomplete
- **WHEN** session initialization runs
- **THEN** the Service marks the session refresh-needed instead of pretending success

### Requirement: Outbox entries are processed independently

The Service MUST inspect each pending outbox entry independently. Valid entries MAY be sent in batches by table when the backend call succeeds, but only confirmed entries MAY be marked sent. Malformed JSON, unknown table names, or failed sends MUST be recorded as failures for the affected entry only and MUST NOT silently drop or misclassify unrelated entries.

#### Scenario: Mixed valid and malformed rows

- **GIVEN** one valid outbox row and one malformed row are pending
- **WHEN** outbox processing runs
- **THEN** the valid row may be sent
- **AND** the malformed row is marked failed

#### Scenario: Unknown table name is encountered

- **GIVEN** a pending row has an unrecognized table name
- **WHEN** processing reaches that row
- **THEN** the row is marked failed
- **AND** processing continues for other entries

### Requirement: Local integrity evidence is standalone

The Service MUST compute local integrity evidence from the running binary: signature validity, binary hash, and executable path. This evidence MUST be available even when remote verification is unreachable, and local logging or downstream reaction MUST not depend on a speculative remote verdict contract. Remote transport and verdict semantics remain out of scope.

#### Scenario: Remote verifier is unavailable

- **GIVEN** remote verification cannot be reached
- **WHEN** integrity is checked locally
- **THEN** the Service still produces signature, hash, and path evidence

#### Scenario: Debugger is attached

- **GIVEN** a debugger is attached
- **WHEN** integrity is checked
- **THEN** diagnostics may note the debugger
- **AND** the local evidence result is still returned

### Requirement: Shared JSON parsing fails closed

Service HTTP/IPC and App.UI WNS payload handling MUST use the shared JSON contract where available and MUST reject malformed or unknown JSON without crashing the process. Errors MUST be bounded and the failing message or entry MUST be isolated to the affected operation. Raw payloads MUST not be used as a source of truth for backend or policy behavior.

#### Scenario: Malformed IPC JSON arrives

- **GIVEN** an IPC message contains malformed JSON
- **WHEN** the parser reads it
- **THEN** the parser fails closed
- **AND** the process remains alive

#### Scenario: Malformed outbox JSON is encountered

- **GIVEN** an outbox entry contains malformed JSON
- **WHEN** outbox processing reaches it
- **THEN** only that entry is failed
- **AND** other entries continue normally
