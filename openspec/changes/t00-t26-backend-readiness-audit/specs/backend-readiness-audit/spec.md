# Backend Readiness Audit Specification

## Purpose

Define a reproducible, read-only audit of the current T00–T26 workspace. The audit produces evidence, partition verdicts, and a risk-prioritized remediation backlog; it does not implement fixes or require backend completion.

## Requirements

### Requirement: Freeze an immutable workspace identity

The audit MUST record a timestamp, repository HEAD, tracked-diff identity, every tracked changed path and mode, and a content-hashed manifest of untracked paths and modes. Snapshot capture MUST NOT modify source, tests, backend code, or generated artifacts.

#### Scenario: Snapshot is reproducible
- GIVEN a working tree with tracked and untracked changes
- WHEN the audit snapshot is captured
- THEN the manifest contains all required identities and a second read can detect drift

#### Scenario: Capture cannot mutate the workspace
- GIVEN snapshot commands are executed
- WHEN capture completes or fails
- THEN source and test content and repository state are unchanged except for audit artifacts

### Requirement: Map the complete audit scope

The report MUST assign every unit T00 through T26 exactly once to a primary partition among the 12 approved partitions P1–P12, with optional secondary references, and MUST expose missing or duplicate assignments.

#### Scenario: Complete mapping is accepted
- GIVEN the partition matrix contains T00–T26 and P1–P12
- WHEN coverage is validated
- THEN no roadmap unit is silently omitted and each partition has a verdict, evidence status, and gap status

### Requirement: Apply current-source evidence rules

Prior reports MAY provide leads only. Every accepted finding MUST cite fresh `path:line` or a dated, hashed command/test receipt; blocked, external, absent, and unverifiable evidence MUST be explicit. Executable coverage MUST never be inferred from stale outputs or non-runnable tests.

#### Scenario: Stale lead is retracted
- GIVEN a prior report conflicts with current source
- WHEN the cited path or receipt cannot be reverified
- THEN the claim is marked retracted or unverifiable and earns no confirmed severity

#### Scenario: Build blocker isolates confidence
- GIVEN `App.UI/Program.cs` or `AgentLauncherLaunchSeamTests.cs` blocks compilation
- WHEN affected tests are inventoried
- THEN the blocker is classified, its evidence impact is isolated, and affected coverage is pending rather than passing or failing

### Requirement: Enforce severity and classification policy

Each finding MUST have evidence, rationale, owner, severity, priority, and one track: client-owned defect; unready integration/infrastructure; non-blocking technical debt; or legitimate backend dependency. Blocker requires concrete release-stopping evidence; otherwise use candidate blocker/evidence pending, Critical, Major, or Minor. Unsupported optimization, DRY, complexity, or duplication concerns MUST remain non-blocking.

#### Scenario: Unsupported blocker is downgraded
- GIVEN a concern has no failing test, data-loss proof, privilege proof, or required evidence path
- WHEN severity is assigned
- THEN it is not a confirmed Blocker

### Requirement: Assess all approved audit dimensions

Each partition MUST address applicable risk/security/privileges/data; bugs/flow/state/business logic; complexity/performance/optimization; Clean/Hexagonal Architecture, SOLID, and DRY; behavior-focused test evidence; and backend readiness across contracts/auth, sync/data, notifications, observability, deployment/configuration, and secrets.

#### Scenario: Backend readiness is assessed case by case
- GIVEN a client seam, adapter, contract, or infrastructure dependency exists
- WHEN readiness is evaluated
- THEN contracts, adapters, seams, failure behavior, configuration, and infrastructure receive an explicit verdict without requiring backend implementation

### Requirement: Produce bounded audit deliverables

The audit MUST produce a partition-by-partition report and a backlog ordered risk → correctness → backend readiness → maintainability. Each backlog item MUST include owner, evidence, severity, track, priority, verification, and a review-size forecast; forecasts above 400 authored changed lines MUST recommend chained reviewable work units.

#### Scenario: Deliverables are reviewable
- GIVEN all partition findings and evidence statuses are complete
- WHEN the backlog is generated
- THEN every finding maps to an ordered remediation item or an explicit accepted dependency/debt disposition

### Requirement: Remain read-only and lifecycle-safe

The audit MUST NOT edit application or test code, complete backend work, freeze speculative contracts, or call review lifecycle commands.

#### Scenario: Read-only boundary is enforced
- GIVEN the audit encounters a defect or blocked evidence
- WHEN the audit records the result
- THEN it records remediation or dependency status without applying a fix or invoking any `gentle-ai review` lifecycle command
