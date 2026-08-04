# Authenticated Session IPC Specification

## Purpose

Define bounded, authenticated Service–SessionAgent communication that preserves per-session ownership through failure and reconnect.

## Requirements

### Requirement: Framing is bounded and read-boundary independent

The system MUST delimit messages, enforce a maximum frame boundary, and reconstruct messages across partial reads or multiple messages returned together. Malformed or oversized frames MUST be rejected without being dispatched.

#### Scenario: Partial and coalesced reads

- GIVEN a valid message arrives in fragments or several valid messages arrive together
- WHEN the receiver processes the input
- THEN each complete message is dispatched exactly once in original order

#### Scenario: Invalid or oversized input

- GIVEN input is malformed or exceeds the permitted frame boundary
- WHEN the receiver processes it
- THEN it rejects the input, does not dispatch it, and exposes the connection failure

### Requirement: Connections own their session and writes are serialized

Each connection MUST remain bound to its authorized session. Concurrent outbound messages MUST be serialized per connection, and a connection MUST NOT write through another connection's stream.

#### Scenario: Concurrent sends and multiple sessions

- GIVEN multiple sessions send messages concurrently
- WHEN their messages are written
- THEN each connection preserves message boundaries and ordering without cross-session delivery

### Requirement: Both peers authorize identity and session

The Service and SessionAgent MUST reject a connection that is not authorized for its claimed session. The SessionAgent MUST validate the Service process identifier and Authenticode identity before accepting the Service; this trust decision MUST NOT require a per-session secret.

#### Scenario: Trusted connection

- GIVEN the peer has the expected process identity and the connection is authorized for the session
- WHEN the connection is established
- THEN communication is accepted and remains bound to that session

#### Scenario: Untrusted peer

- GIVEN the peer has an unauthorized session, invalid process identity, or invalid Authenticode identity
- WHEN authentication is attempted
- THEN the connection is rejected and no session message is dispatched

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
