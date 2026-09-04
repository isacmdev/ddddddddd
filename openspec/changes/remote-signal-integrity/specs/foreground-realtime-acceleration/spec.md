# Foreground Realtime Acceleration Specification

## Purpose

Provide foreground UI refresh acceleration while preserving Service enforcement and durable polling authority.

## Requirements

### Requirement: Realtime is foreground-only and non-authoritative

Realtime subscription MUST exist only while the UI is foreground. Events MUST be typed UI acceleration hints, not policy authority, and MUST NOT own synchronization, identity, or backend retry.

#### Scenario: Foreground event refreshes UI

- GIVEN the UI is foreground and a valid typed event arrives
- WHEN the event is received
- THEN UI refresh/invalidation MAY occur through bounded admission
- AND Service enforcement authority MUST remain unchanged

#### Scenario: Background and reconnect failure are isolated

- GIVEN the UI backgrounds, disconnects, or cannot reconnect
- WHEN lifecycle handling completes
- THEN the subscription MUST close or remain unavailable without affecting enforcement
- AND polling MUST continue eventual convergence

#### Scenario: Cancellation and restart recover safely

- GIVEN subscription cancellation races with foreground restart
- WHEN lifecycle transitions overlap
- THEN stale events MUST be ignored, no duplicate authority created, and the next foreground state MAY resubscribe
