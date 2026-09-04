# Proposal: `offline-enforcement-safety-loop`

## Intent

Complete the integrated offline-enforcement safety loop by connecting foreground usage decisions, confirmed critical actions, persistent overlay behavior, authoritative health, and already-observable production safety signals. This change follows the closed SDD1 runtime foundations, SDD2 domain persistence correctness, and SDD3 offline usage pipeline.

## Problem

Offline enforcement is not currently trustworthy end to end:

- The overlay is placeholder or dormant.
- `EnforcementEngine` IPC binding is stale or absent.
- Canonical `AppId` cannot safely identify the exact foreground PID.
- Persistence restoration is not activated.
- Heartbeats do not update authoritative health and contain misleading fields.
- Usage thresholds are not reevaluated while the same application remains foreground.
- Overlay, termination, and workstation-lock actions are fire-and-forget.
- `async void` enforcement permits races and stale outcomes.
- Health issues are transient or incorrectly deduplicated.
- Production-observable T13 signals remain dormant.
- Foreground handling performs O(U+A) allocations or database scans per event.
- Timers can overlap, and blocking `WaitForExit` can stall processing.

These gaps allow protection to be reported without confirmed enforcement and create race, reconnect, performance, and observability failures.

## Outcome

Each Windows session has one serialized coordinator that owns enforcement ordering and state transitions. It communicates through a replaceable agent-command port, rejects stale generation-correlated results, survives disconnects without dead-channel coupling, and reports protection only after typed confirmation.

Offline state is restored, same-app thresholds are reevaluated, health remains durable and truthful, and supported production signals participate in the safety loop.

## Scope

### In

- T08 integrated overlay.
- T09 lock flow and persistent overlay behavior.
- T11 serialized offline enforcement.
- T12 truthful effective health, issues, and reporting.
- T13 signals already observable in production: clock/timezone changes, agent death, and child-admin state.
- Exact foreground-process targeting separate from canonical application identity.
- Persistence restore activation.
- Typed action acknowledgements/results, reconnect handling, stale-result suppression, and bounded retry/failure behavior.
- Hot-path and timer optimization required by the integrated flow.

### Out

- New OS-wide uninstall watchers.
- New ACL or registry watchers.
- Broader T13 monitoring not already observable in production.
- Schema changes.
- Reopening or altering closed SDD1-SDD3 scope.
- Unrelated policy, persistence, UI, or reporting redesign.

## User Decisions

1. Scope is the integrated T08/T09/T11/T12 safety loop plus only the existing-observable T13 clock/timezone, agent-death, and child-admin signals.
2. Overlay, exact-target process termination, and `LockWorkStation` require typed ACK/results. The service must not claim protection without confirmation.
3. Architecture uses one serialized coordinator per session plus a replaceable generation-correlated agent-command port. Stale results are suppressed, and coordinator state must not couple to a dead command channel.

## Capabilities

- Restore authoritative offline enforcement state after startup.
- Continuously reevaluate usage while the same app remains foreground.
- Target the observed foreground PID without weakening canonical `AppId`.
- Display, persist, replace, and clear overlays through confirmed commands.
- Confirm exact-target termination and workstation locking with typed outcomes.
- Serialize policy evaluation and critical actions per session.
- Derive effective health from authoritative heartbeats, command outcomes, durable issues, and supported T13 signals.
- Recover safely across agent death, timeout, replacement, and reconnect.

## Approach

Introduce a session-scoped coordinator as the sole owner of enforcement sequencing. Route agent operations through a replaceable command port with command identity, generation/session correlation, typed results, timeouts, and stale-result rejection. Remove `async void`, fire-and-forget critical paths, overlapping timers, and blocking process waits.

Separate canonical app identity from ephemeral process targeting. Activate persistence restoration before enforcement becomes healthy. Use serialized ticks or equivalent non-overlapping scheduling for same-app reevaluation and supported safety signals. Maintain durable, correctly deduplicated health issues and derive reporting from confirmed current state.

Measure hot paths before and after changes. Replace per-foreground-event O(U+A) scans and allocations with bounded lookups, cached/indexed state, and work triggered only when relevant state changes.

## Affected Areas

- Service-side enforcement orchestration and session lifecycle.
- Agent IPC contracts, bindings, reconnect behavior, and command execution.
- Overlay and lock/termination adapters.
- Foreground observation and PID correlation.
- Persistence startup restoration.
- Heartbeat, health, issue deduplication, and reporting.
- Timers, process waiting, clock/timezone, agent-death, and child-admin handling.
- Unit, race, integration, performance, and Windows runtime tests.

## Dependencies

- Closed SDD1 runtime foundations.
- Closed SDD2 domain persistence correctness.
- Closed SDD3 offline usage pipeline.
- Existing Windows session, native overlay, process, heartbeat, and IPC facilities.

## Success Criteria

- Protection is never reported for an unconfirmed critical action.
- Enforcement is deterministic under concurrency, failure, timeout, reconnect, and stale responses.
- Restored state and same-app thresholds enforce correctly.
- Health and issues reflect current authoritative truth.
- Foreground hot-path complexity and allocations are measured, documented, and bounded.
- Native Windows overlay, session lock, exact-target termination, heartbeat, and reconnect flows have runtime evidence.

## Quality Gates

- Strict RED/GREEN/REFACTOR TDD.
- End-to-end verification of normal, flow, logic, race, failure, reconnect, and stale-result paths.
- Mandatory algorithmic-complexity, inefficiency, allocation, timer, blocking-work, and optimization review.
- Removal or prevention of overlapping and inefficient work.
- Clean architecture and ownership boundaries with no competing enforcement authority.
- Greater than 80% changed-scope line coverage; branch coverage reported.
- Windows integration/runtime evidence for native UI and session behavior.
- Optimization risks explicitly tested, including stale caches, missed reevaluation, timer drift, and over-coalescing.
- Clean canonical closeout that clearly separates current truth from historical evidence.

## Risks/Mitigations

- Serialization may delay urgent actions: prioritize critical commands and bound queueing.
- Caching may become stale: define invalidation ownership and verify against authoritative state.
- ACK loss may duplicate actions: use correlation and idempotent outcome handling.
- Reconnect may apply obsolete results: use port generations and command correlation.
- Native Windows behavior may differ from mocks: require runtime evidence on supported sessions.

## Rollback

Disable the integrated coordinator and command-port activation while retaining prior persistence and usage-pipeline behavior. Do not reinterpret unconfirmed actions as successful. Preserve compatible stored state and restore the previous reporting path only with explicit degraded-health disclosure.
