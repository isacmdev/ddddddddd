# Authenticated Session IPC Specification

## Purpose

Define bounded authenticated Service–SessionAgent communication preserving session ownership through failure and reconnect.

## Requirements

### Requirement: Framing is bounded and read-boundary independent

The system MUST delimit messages, enforce a maximum frame boundary, and reconstruct messages across partial or coalesced reads. Malformed or oversized frames MUST be rejected without dispatch.

#### Scenario: Partial and coalesced reads

- GIVEN valid messages arrive fragmented or coalesced
- WHEN the receiver processes the input
- THEN each complete message is dispatched exactly once in original order

#### Scenario: Invalid or oversized input

- GIVEN input is malformed or oversized
- WHEN the receiver processes it
- THEN it rejects the input, does not dispatch it, and exposes the connection failure

### Requirement: Connections own their session and writes are serialized

Each connection MUST remain bound to its authorized session. Outbound messages MUST be serialized per connection, and a connection MUST NOT write through another stream.

#### Scenario: Concurrent sends and multiple sessions

- GIVEN multiple sessions send concurrently
- WHEN their messages are written
- THEN each connection preserves message boundaries and ordering without cross-session delivery

### Requirement: Both peers authorize identity and session

Service and SessionAgent MUST reject unauthorized claimed sessions. The SessionAgent MUST validate the Service process identifier and Authenticode identity before acceptance; a per-session secret MUST NOT be required. Acceptance MUST include a disposable signed runtime harness.

(Previously: Authorization lacked a proportional C2 evidence boundary.)

#### Scenario: Trusted connection

- GIVEN the peer has expected identity and session authorization
- WHEN the connection is established
- THEN communication is accepted and remains bound to that session

#### Scenario: Untrusted peer

- GIVEN the peer has an unauthorized session or invalid identity
- WHEN authentication is attempted
- THEN the connection is rejected and no session message is dispatched

### Requirement: C2 evidence separates managed and native scope

Managed C2 logic executable through seams MUST achieve strictly >80% changed-line coverage with branch coverage reported. The denominator MUST be managed executable changed lines with valid sequence points after documented path/line normalization. Source additions without sequence points MAY be excluded only when a path/line manifest independently proves non-executable syntax or compiler-mapping gaps (declarations, signatures, braces, continuations, or generated async projections). Missing Cobertura mapping alone is insufficient: each exclusion MUST be attributable and reasoned; unreported executable behavior remains uncovered. Async duplicates MUST use source-path/line union. Native boundaries MUST remain separately classified and proven by a disposable signed runtime harness, not unit coverage, including P/Invoke, OS process/SID/session/PID lookup, CurrentUser certificate-chain/WinTrust, and unfakeable named-pipe kernel behavior. The harness MUST prove Authenticode acceptance, fail-closed rejection, session/PID/SID binding, ordered handshake, authenticated dispatch, reconnect, and cleanup. Aggregate 50% thresholds and broad waivers are prohibited.

#### Scenario: Managed C2 coverage is assessed

- GIVEN a normalized manifest identifies executable lines and reasoned non-executable additions
- WHEN managed C2 evidence is reviewed
- THEN coverage is strictly >80% over the executable denominator, exclusions are not based solely on missing mappings, and branch coverage is reported

#### Scenario: Native C2 behavior is proven by the signed harness

- GIVEN a signed Service/SessionAgent harness executes
- WHEN it exercises trust, binding, handshake, dispatch, reconnect, and shutdown
- THEN Authenticode acceptance, fail-closed rejection, session/PID/SID binding, ordered authentication, dispatch, reconnect, and cleanup are proven

### Requirement: Disconnect recovery is bounded and single-flight

After a disconnect, the affected session MUST expose the failure and MAY reconnect only after authorization is re-established. Repeated disconnect signals MUST NOT create concurrent recovery attempts or lose the session binding.

#### Scenario: Authorized reconnect

- GIVEN an established connection is lost
- WHEN the peer becomes available and re-authenticates for the same session
- THEN communication resumes without cross-session delivery

#### Scenario: Repeated disconnect signals

- GIVEN disconnect and health-timeout signals arrive together
- WHEN recovery is requested
- THEN one recovery attempt is in flight and later signals are coalesced or safely ignored
