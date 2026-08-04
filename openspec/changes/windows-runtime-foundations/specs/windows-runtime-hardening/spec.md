# Windows Runtime Hardening Specification

## Purpose

Define authoritative privilege and ACL outcomes so local enforcement remains safe and health accurately reflects startup security.

## Requirements

### Requirement: Authoritative privilege verdicts control health

The system MUST distinguish an authorized standard-account outcome, an administrator outcome, and an `Unknown` outcome. `Unknown` MUST be treated as a security failure for health and onboarding, not as a successful healthy verdict.

#### Scenario: Standard account is verified

- GIVEN privilege inspection completes with a standard-account verdict
- WHEN runtime hardening evaluates the result
- THEN enforcement remains active and the runtime is eligible for healthy onboarding

#### Scenario: Privilege inspection is unknown or fails

- GIVEN privilege inspection returns `Unknown` or cannot establish a trusted verdict
- WHEN the runtime evaluates startup health
- THEN enforcement remains active, health is `DEGRADED`, and healthy onboarding is blocked

### Requirement: ACL hardening is idempotent and failure-observable

The system MUST apply the required protection repeatedly without accumulating equivalent access outcomes. A failed hardening operation MUST remain observable to health and MUST NOT be reported as successful persistence or healthy startup.

#### Scenario: Hardening is repeated

- GIVEN the same protected targets are hardened more than once
- WHEN each hardening attempt completes
- THEN effective protection is equivalent and no duplicate equivalent access outcome is introduced

#### Scenario: ACL repair fails

- GIVEN a required target cannot be hardened
- WHEN startup health is evaluated
- THEN enforcement remains active, health is `DEGRADED`, and healthy onboarding is blocked
