# Offline Sync Recovery Specification

## Purpose

Define contract-first, offline-safe delivery and recovery for T18, T20, and T10-B. The system MUST remain useful without a live backend; this specification makes no endpoint, credential, Windows-matrix, client-ready, ExternalVerified, or remote-integration claim.

## Requirements

### Requirement: Identity-gated durable delivery

Authenticated REST work MUST require the definitive local identity/capability state. Accepted work MUST be represented durably before delivery is attempted, with a stable operation identity and bounded payload/selection work.

#### Scenario: Ready identity admits an event
- GIVEN identity and capability state are valid
- WHEN an event is accepted
- THEN one durable outbox item with a stable idempotency identity MUST exist before dispatch

#### Scenario: Missing identity fails closed
- GIVEN identity is absent, revoked, or indeterminate
- WHEN sync work is triggered
- THEN no authenticated request MUST be sent and local startup MUST remain non-blocking

### Requirement: Per-entry outbox state is crash-safe and idempotent

Outbox state MUST support durable claim, per-entry outcome, and acknowledgement transitions. Enqueue and replay MUST deduplicate by stable identity; a crash at any transition MUST permit deterministic restart without silent loss or unsafe acknowledgement.

#### Scenario: Mixed batch outcomes remain isolated
- GIVEN selected entries produce success, transient failure, and permanent failure
- WHEN the batch completes
- THEN each entry MUST receive only its own outcome and unrelated entries MUST remain eligible

#### Scenario: Crash precedes acknowledgement
- GIVEN remote acceptance may have occurred but local acknowledgement did not
- WHEN the process restarts
- THEN the item MUST replay with the same idempotency identity and MUST NOT be silently deleted or double-counted

### Requirement: One bounded scheduler owns retry admission

Exactly one coordinator MUST own admission, retry budgets, backoff, and cancellation for each work item. Retry budgets and scans MUST be finite and configurable; retry stacking, unbounded polling, and overlapping execution MUST NOT occur. Connectivity MAY defer eligibility. Task Scheduler MUST only trigger the same coordinator and MUST NOT create concurrent duplicate work.

#### Scenario: Transient work backs off once
- GIVEN a transient outcome and a finite remaining retry budget
- WHEN the coordinator schedules recovery
- THEN one bounded backoff MUST be recorded and no second retry owner or overlapping run MUST be created

#### Scenario: Cancellation and shutdown propagate
- GIVEN scheduled work is running
- WHEN cancellation or shutdown is requested
- THEN in-flight work MUST observe cancellation, stop admitting new work, and complete within the configured shutdown bound

### Requirement: Exhausted work is durably dead-lettered

After bounded retries or a proven permanent outcome, the item MUST be retained durably as dead-letter with safe, redacted diagnostics. It MUST NOT be silently discarded. Requeue MUST be explicit, authorized, and preserve idempotency and audit context.

#### Scenario: Retry exhaustion retains evidence
- GIVEN an item reaches its finite retry limit
- WHEN the final attempt is classified
- THEN it MUST enter dead-letter state with redacted diagnostics and no secret leakage

#### Scenario: Explicit recovery requeues safely
- GIVEN an authorized operator or recovery policy selects a dead-letter item
- WHEN it is requeued
- THEN the transition MUST be explicit, auditable, and idempotent, without bypassing normal eligibility or retry ownership

### Requirement: Reconciliation checkpoints survive restart

Usage reconciliation MUST persist restart-safe checkpoints and idempotent replay markers. It MUST use bounded, cancellable work and single-flight execution so interruption, retry, or machine restart cannot double-count usage or lose continuity.

#### Scenario: Restart continues an interrupted period
- GIVEN reconciliation stops after partial progress
- WHEN the service restarts
- THEN it MUST resume from durable checkpoints and produce the same final totals as one uninterrupted run

#### Scenario: Duplicate replay does not count twice
- GIVEN an already-applied usage contribution is replayed
- WHEN reconciliation processes it again
- THEN the contribution MUST be recognized as applied and totals MUST remain unchanged

### Requirement: Diagnostics and lifecycle are safe by default

All recovery diagnostics MUST redact credentials, tokens, authorization material, and sensitive payload data. Startup and shutdown MUST be deterministic, cancellation-aware, and non-blocking; failure to reach remote services MUST remain an observable local outcome rather than a process failure.

#### Scenario: Remote outage preserves local operation
- GIVEN the backend is unavailable
- WHEN startup or scheduled recovery occurs
- THEN local services MUST remain available, work MUST remain durably eligible or dead-lettered, and no secret MUST appear in diagnostics

#### Scenario: Repeated triggers remain single-flight
- GIVEN timer, startup, and Task Scheduler triggers arrive concurrently
- WHEN the coordinator evaluates them
- THEN at most one applicable execution MUST proceed and all triggers MUST resolve without retry amplification
