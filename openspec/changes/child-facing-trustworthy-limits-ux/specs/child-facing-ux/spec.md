# Child-Facing UX Specification

## Purpose

Define a truthful, dignified, age-appropriate contract for limits, monitoring,
progress, requests, rewards, repair/accessibility. The Service is
authoritative; UI surfaces MUST NOT imply that policy can be disabled or
negotiated when it cannot.

## Evidence boundary and dependencies

Requirements are acceptance criteria grounded in T25–T30 and the
service-authoritative architecture. Visual conventions are non-blocking
heuristics. Claims that colors, shapes, mascots, animations, or phrases
universally calm, build trust, improve mental health, or eliminate punishment
perception are prohibited. Hermes MAY review it but is not its authority.

Unresolved dependencies/questions MUST remain open: onboarding counts conflict
(`0 of 4`, six-step route, `5 of 5`); T25 third-party-sharing wording conflicts
with backend T32 events; age differentiation, request-time, reward, and repair
flows are incomplete and MUST NOT be invented.

## Requirements

### Requirement: Limits are truthful and dignified

Every limit MUST state what happened, truthful source/authority, why it applies,
duration or next availability when known, and real actions available. Copy MUST
describe state, never character, and MUST NOT shame, blame, threaten, frighten,
moralize, surveil theatrically, or frame the limit as punishment. Choices MUST
be functional; “Request more time” MAY appear only when supported, with truthful
delivery and status.

#### Scenario: Limit explains a hard boundary
- GIVEN the Service reports a blocked app and no request path
- WHEN the child sees the limit
- THEN the surface identifies reason, authority, duration/next availability when known, and real actions

#### Scenario: Supported request is pending
- GIVEN the Service supports a request for this scope
- WHEN the child submits “Request more time”
- THEN the UI reports each request status truthfully

### Requirement: Surfaces do not manipulate or mock

The UI MUST NOT use false urgency, deceptive countdowns, manipulative nagging,
unsafe flashing, or mascots/celebrations mocking denial. Rewards MUST communicate
an outcome, not moral worth, and MUST NOT imply they override hard policy.

#### Scenario: Time is unknown
- GIVEN authoritative availability is unavailable
- WHEN a limit is rendered
- THEN no fabricated timer, urgency, promise, or mocking denial treatment appears

### Requirement: Monitoring is visible and understandable

Monitoring MUST remain discoverable, age-appropriate, and state truthful
what/what-not/who/purpose statements. Hidden mode MUST NOT exist.

#### Scenario: Child opens monitoring information
- GIVEN monitoring is active or configured
- WHEN the child opens its information surface
- THEN it explains collected/excluded data, access, and purpose without theater

### Requirement: Status and progress mirror authority

Progress, success, approval, reward, protection, degraded, and recovery states
MUST reflect Service evidence. `DEGRADED` MUST NOT appear fully protected, and
recovery MUST NOT be claimed before evidence. Onboarding counts remain unresolved.

#### Scenario: Protection degrades
- GIVEN the Service reports a missing critical foundation
- WHEN status is shown
- THEN the UI says `DEGRADED`, explains the cause, and offers a real repair action

### Requirement: Accessibility and age variants are validated

Applicable native Windows accessibility (not a web-conformance claim) MUST provide non-sensory meaning,
accessible names/status, logical focus, keyboard operation, adequate contrast,
reduced motion, and no unsafe flashing. Variants MUST exist for ages 7–12,
13–16, and 17–18; wording and visual interpretation remain subject to
validation, not universal claims.

#### Scenario: Keyboard and assistive technology are used
- GIVEN a child uses keyboard navigation or assistive technology on Windows
- WHEN a limit, status, request, or repair surface is used
- THEN meaning, focus, controls, and status remain perceivable and operable without sensory-only cues

### Requirement: Research separates acceptance from hypotheses

Review MUST separate hard acceptance results from non-blocking heuristics and
research hypotheses. Research MUST use teach-back and open-ended interpretation
across all age bands for dignity, authority, monitoring, progress, requests,
rewards, repair, failure, and recovery; engagement metrics alone MUST NOT
establish comprehension or trust.

#### Scenario: Research validates a proposed variant
- GIVEN an age-band variant is proposed
- WHEN children and caregivers explain it in their own words
- THEN interpretation, misunderstandings, and dignity concerns are recorded separately from engagement
