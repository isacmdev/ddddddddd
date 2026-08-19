# Technical Design: `offline-enforcement-safety-loop`

## Technical Approach

Create one `SessionEnforcementCoordinator` per interactive Windows session and make it the sole owner of restoration, foreground observations, threshold ticks, policy intents, critical commands/results, supported T13 inputs, issues, and effective-health transitions. The current passive bounded coordinator, `AgentCommandContracts`, `AgentCommandPort`, `OverlayIntentPolicy`, `ThresholdReevaluator`, and exact-target `ProcessTerminator` seam are retained and completed rather than introducing a second orchestration model.

The coordinator consumes a bounded, single-reader mailbox. A replaceable `IAgentCommandPort` owns connection generation and transport waits but never owns enforcement state. Commands and results are correlated by session, connection generation, command ID, and intent version. Late, duplicate, wrong-session, wrong-generation, expired, or superseded results may be logged as evidence but cannot mutate current enforcement, overlay, issue, or health state.

Pure policy evaluation remains separate from execution. Canonical `AppId` is the stable policy, usage, cache, and reporting identity; an ephemeral `ObservedProcessTarget` identifies the exact foreground PID instance and is revalidated immediately before termination. Overlay, exact-target termination, and `LockWorkStation` return typed outcomes. Protection becomes healthy only after durable state is restored and the current intent converges on the current agent generation.

## Architecture Decisions

| Decision | Choice and rationale |
|---|---|
| Session authority | One serialized coordinator per session removes the current `async void` race and prevents competing state owners. |
| Mailbox | Capacity 256, one reader, backpressure for non-droppable inputs, and safe dirty-slot coalescing only for foreground observations and ticks. Critical actions/results, restore, connection, and T13 transitions are never dropped. |
| Agent boundary | `IAgentCommandPort` is replaceable. Replacement completes old waits as `ConnectionReplaced` without discarding coordinator intent or restored state. |
| Correlation | Session, generation, command, and intent identity are all required; no single field establishes authority. |
| Policy versus target | `AppId` remains canonical. Termination accepts only observed PID/session/start-time evidence, never a process name or `AppId`. |
| Policy purity | `EnforcementEngine` becomes deterministic evaluation over policy, usage, time, and `AppId`; it does not own IPC, overlay flags, process execution, or lock execution. |
| Overlay | Desired overlay is durable intent, not inferred from a send attempt or heartbeat. Reconnect converges current intent and does not replay obsolete commands. |
| Health/issues | Health is a projection of authoritative coordinator state. Issues use durable semantic identity and authoritative resolution evidence, not message text or timestamps. |
| Scope | Reuse existing T13 clock/timezone, agent-death, and child-admin observations only. Add no watcher, event bus, CQRS layer, schema migration, or SDD1-SDD3 redesign. |

## Components and Ownership

| Component | Responsibility |
|---|---|
| `SessionEnforcementCoordinator` | Ordered session state machine, mailbox/coalescing, intent versions, retries, current confirmations, invalidation, issues, and health derivation. |
| `IAgentCommandPort` / `AgentCommandPort` | Current sender/generation, bounded command wait, timeout, result matching, and replacement completion. |
| `EnforcementEngine` | Pure `AppId` policy decision; no transport or native side effects. |
| `ForegroundWatcher` and IPC message mapping | Observe canonical `AppId` plus exact process evidence; post observations only. |
| `OverlayIntentPolicy` / `IOverlayIntentStore` | Persist desired overlay and reconcile it against the current generation. |
| `OverlayWindow` adapter | Native show/replace/clear, message-pump/topmost lifecycle, and typed confirmation. |
| `ProcessTerminator` | Revalidate PID/session/start time and terminate only that process instance with cancellable async waiting. |
| Workstation-lock adapter | Invoke `LockWorkStation` in the intended session and return native status. |
| `ServiceHealthMonitor` and reporting | Publish coordinator-derived effective health; never claim enforcement from `IsRunning`, a send attempt, or a constant flag. |
| `IIssueStore` | Atomic durable upsert/resolve by semantic key. With no database schema change, use a versioned service-owned state document under the existing protected service data root. |

## Conceptual Contracts

The current records in `ControlParental.Domain/AgentCommandContracts.cs` are the baseline. Command payloads are completed as a closed union so target and overlay data travel with the correlated envelope.

```csharp
record ObservedProcessTarget(
    int ProcessId, int SessionId, DateTimeOffset StartedAt,
    string? ExecutablePathFingerprint);

record AgentCommandEnvelope(
    Guid CommandId, int SessionId, long ConnectionGeneration,
    long IntentVersion, AgentCommand Payload, DateTimeOffset Deadline);

enum ActionStatus
{
    Confirmed, HarmlessAbsence, TimedOut, ConnectionReplaced, Stale,
    InvalidTarget, InvalidState, NativeFailure, AccessDenied
}

record AgentActionResult(
    Guid CommandId, int SessionId, long ConnectionGeneration,
    long IntentVersion, ActionStatus Status, int? NativeError);

interface IAgentCommandPort
{
    long Generation { get; }
    ValueTask<AgentActionResult> ExecuteAsync(
        AgentCommandEnvelope command, CancellationToken cancellationToken);
}

record IssueKey(int SessionId, string Kind, string Cause);
interface IIssueStore
{
    Task UpsertActiveAsync(IssueKey key, IssueEvidence evidence, CancellationToken ct);
    Task ResolveAsync(IssueKey key, RecoveryEvidence evidence, CancellationToken ct);
}
```

`AgentCommand` contains overlay show/replace/clear, exact-process termination, and workstation lock. `HarmlessAbsence` is valid only when absence proves the requested final state, such as clearing an already absent overlay or finding the exact target already exited. It is not a generic transport success.

## State and Ordering Semantics

The lifecycle is `Restoring -> Disconnected | Converging -> Healthy`; any state can enter `Degraded`. `Healthy` requires successful restore, a fresh heartbeat from the current session/generation, no unresolved health-blocking issue, and confirmation that every required current critical intent has converged.

Inputs receive a coordinator sequence. Restore completion precedes policy activation. Connection replacement invalidates old-generation confirmations before convergence starts. Intent change increments `IntentVersion`; retries retain that intent version but use fresh command IDs. Only one critical command is in flight per session, preserving intent order.

Foreground coalescing uses a latest-value slot plus a queued/dirty marker. A newer observation overwrites only the pending observation data; after evaluation, the consumer requeues once if the slot became dirty during execution. Tick coalescing records that reevaluation is owed and follows the same drain rule. This avoids the current unsafe case where a full mailbox can clear a pending bit and lose required work. Distinct intent transitions are materialized by the coordinator and cannot be overwritten. Critical results, restore, connection, clock/timezone, agent-death, and child-admin inputs use backpressure or reserved admission and are never coalesced away. Critical admission has priority so observation floods cannot starve safety work.

## End-to-End Flows

### Restore and Reconnect

1. Create the session in `Restoring`; load overlay/enforcement intent, active/resolved issues, policy revision, and required usage state.
2. On restore failure, persist/activate a restore issue and enter `Degraded`, never a healthy empty state.
3. Atomically attach the agent port, increment generation, and invalidate old pending authority.
4. Enter `Converging`; issue only commands needed for the current intent.
5. Accept results only when session, generation, command, deadline, and intent remain current.
6. Enter `Healthy` only after current heartbeat and all required confirmations are authoritative.

### Foreground and Same-App Tick

1. Capture `AppId` and `ObservedProcessTarget` together and post the latest observation.
2. Resolve policy/usage through revisioned O(1) lookups and evaluate pure policy.
3. If the required action changes, persist intent before dispatch and increment its version.
4. Reconcile overlay, termination, or lock sequentially through the current port/adapters.
5. A single non-overlapping periodic source posts a tick; it never evaluates directly.
6. The tick reevaluates the current `AppId`, allowing a threshold crossing without another foreground transition.

### Health and Existing T13 Signals

Heartbeats refresh liveness only when their session and generation match and fields are measured or authoritative; `ForegroundChanged` is not a heartbeat. Clock/timezone changes invalidate wall-clock-derived decisions and enqueue reevaluation. Agent death disconnects the port, invalidates pending confirmations, and upserts its issue. Existing child-admin evidence upserts or resolves the corresponding session-scoped issue. Recovery requires a matching heartbeat, successful convergence, standard-account evidence, or other cause-specific authoritative proof; timer passage or agent replacement alone cannot resolve an issue.

## Failure Semantics

Timeout, missing ACK, native/access failure, malformed heartbeat, agent death, uncertain lock outcome, and restore failure are non-success and degrade health. Retries are correlated to current intent, use bounded exponential delay, and stop after three attempts or immediately when intent/generation changes. Duplicate results are idempotently ignored. Uncertain outcomes remain visible after reconnect and never become success through reporting.

Termination reopens the PID and compares process ID, Windows session, start time, and optional executable evidence immediately before graceful close and again before forced kill. Mismatch is `InvalidTarget`; no lookup by app/process name is allowed in the integrated path. All waits are cancellable async waits; blocking `WaitForExit` is removed from this path.

## Complexity and Resource Bounds

| Path | Before | After / required bound |
|---|---|---|
| Foreground evaluation | O(U+A) database/materialization or scans per event | O(1) session lookup plus O(1) revisioned `AppId` policy/usage lookup |
| Threshold scheduling | Overlapping timer callbacks or foreground-only reevaluation | O(S) across active sessions; at most one owed tick per session |
| Issue deduplication | O(I) message/timestamp comparison and transient lists | O(1) deterministic semantic-key lookup/upsert |
| Exact termination | O(P) name search plus blocking wait | O(1) supplied-target validation plus cancellable async wait |
| Result correlation | Fire-and-forget or implicit state | Expected O(1) command-ID lookup |

The mailbox capacity is 256 per active session. There is at most one queued latest-foreground marker, one owed tick marker, one in-flight critical command per session, and one periodic scheduler timer for all sessions. Non-coalescible producers receive bounded backpressure; retries are capped at three. Foreground posting allocates no user/app collections and performs no database query. Steady-state unchanged decisions emit no command and should allocate only the input/envelope required by the transport; benchmarks record bytes/event, queue high-water mark, tick drift, and p50/p95 processing time.

Cache invalidation ownership is explicit: policy cache on policy revision; usage cache on accumulation/reconciliation commit; target on foreground or session change; restored state on restore generation; time-derived decisions on clock/timezone change; confirmation cache on intent or connection-generation change. Tests compare cached and uncached authoritative evaluation after every invalidation source.

## Persistence and Schema

No relational schema or IPC framing redesign is required. Overlay intent and semantic issue state are persisted as versioned, atomically replaced service-owned documents with deterministic identifiers. The issue document records semantic key, status, first/last evidence, resolution evidence, and revision. Corrupt or unreadable documents produce a durable restore issue and degraded health; they are not replaced by a healthy empty document. Historical outcomes remain audit evidence, not current confirmation.

## File and Symbol Impact

| File / symbol | Planned impact |
|---|---|
| `ControlParental.Domain/AgentCommandContracts.cs` | Complete closed command payloads and exact target/typed correlation contracts. |
| `ControlParental.Domain/IpcMessage.cs` and JSON contexts | Carry observed target, authoritative heartbeat identity, commands, and typed results without redesigning IPC transport. |
| `ControlParental.Service/SessionEnforcementCoordinator.cs` | Complete session state machine, admission priority, latest-value drain semantics, intents, retries, and health projection. |
| `AgentCommandPort.cs`, `ThresholdReevaluator.cs`, `OverlayIntentPolicy.cs` | Preserve replaceability/coalescing seams; add current-intent checks, safe saturation behavior, and convergence integration. |
| `ControlParentalService.OnForegroundChanged`, `SessionManager`, composition in `Program.cs` | Replace `async void`, stale channel binding, and direct enforcement with session input posting and lifecycle ownership. |
| `EnforcementEngine.cs` | Remove `IIpcChannel` and native action ownership; expose pure policy evaluation. |
| `ForegroundWatcher.cs`, SessionAgent host | Emit exact process evidence and execute correlated commands/results instead of fire-and-forget callbacks. |
| `OverlayWindow.cs`, `ProcessTerminator.cs`, workstation-lock adapter | Return typed native outcomes; preserve exact-target PID reuse checks and async waits. |
| `OverlayPersistenceManager.cs`, new issue-store adapter | Replace in-memory-only state with atomic durable intent/semantic issue documents. |
| `ServiceHealthMonitor.cs`, `EnforcementLevelMonitor.cs`, query/report handlers | Remove constant/independent success inference and consume authoritative effective health/issues. |
| `AntiTamperMonitor.cs`, scheduler | Wire only existing clock/timezone, agent-death, and child-admin signals through non-overlapping coordinator inputs. |

## TDD and Evidence Matrix

| Layer | RED/GREEN coverage | Required evidence |
|---|---|---|
| Domain/unit | Correlation, state transitions, typed statuses, semantic keys, policy purity, PID reuse, cache invalidation | Deterministic tests for every stale/mismatch and `ActionStatus` branch |
| Coordinator/race | Serialization, independent sessions, mailbox saturation, dirty-slot drain, critical priority, retries, cancellation | Stress/race tests proving no overlap, starvation, lost intent, or stale mutation |
| Persistence/integration | Restore failure, atomic intent/issues, restart, reconnect, same-app crossing, heartbeat authority, recovery | Restart and corruption tests with current-state convergence |
| Performance | Foreground/tick/result/issue paths | Before/after BenchmarkDotNet or equivalent measurements for Big-O assumptions, allocations, queue high-water mark, and timer drift |
| Windows runtime harness | Overlay show/replace/clear/topmost, exact test-process termination, `LockWorkStation`, heartbeat loss/recovery, agent kill/restart, console/RDP reconnect, child privilege and clock/timezone change | Signed supported Windows 10/11 run with native codes, session/generation traces, cleanup, and pass/fail record |

Strict RED/GREEN/REFACTOR applies to every work unit. Changed-scope line coverage must exceed 80%; branch coverage is reported separately. Runtime-only native behavior does not waive testable managed decision, correlation, retry, or ordering logic.

## Reviewable Work Units and Rollback

| Unit | Scope, target size, rollback boundary |
|---|---|
| 1. Contracts/port | Correlation, typed results, replacement; roughly 300-400 authored lines. Passive until composed; remove registration to roll back. |
| 2. Exact target/overlay intent | PID reuse-safe adapter and durable overlay policy; roughly 300-400 lines. Keep legacy path unselected until verified. |
| 3. Coordinator integration | State machine, safe mailbox, foreground/ticks; roughly 350-400 lines. Session-scoped composition switch restores prior usage pipeline. |
| 4. Agent/native commands | Correlated overlay/lock/termination execution; roughly 300-400 lines. Disable command dispatch independently. |
| 5. Restore/health/issues/T13 | Durable restore, semantic issues, health authority, existing signals; split adapters if needed to stay below roughly 400 lines. Roll back publisher authority while remaining explicitly degraded. |
| 6. Performance/runtime/closure | Instrumentation and harness only; no production behavior rollback required. |

Rollback never reinterprets unconfirmed action as success. It disables coordinator/port activation while retaining compatible policy, usage, and durable state, and exposes degraded health on the prior reporting path. No unit reopens SDD1-SDD3.

## Threat Matrix

| Threat / failure | Mitigation and verification |
|---|---|
| PID reuse or same-name process | Session/start-time/path evidence, immediate revalidation, PID-reuse RED test |
| Late, duplicate, or spoofed stale result | Existing authenticated IPC plus full correlation and deadline checks |
| Agent death during action | Generation invalidation, timeout, durable issue, no success |
| Queue flood/starvation | Capacity 256, dirty-slot coalescing, reserved/prioritized critical admission, stress test |
| Over-coalescing misses threshold/intent | Owed-work drain loop and distinct intent preservation tests |
| Clock/timezone manipulation | Serialized invalidation/reevaluation and durable signal issue |
| Stale policy/usage cache | Named invalidation owners and cached-versus-authoritative tests |
| Overlay dismissal, restart, or reconnect | Durable intent and current-generation convergence before healthy |
| Misleading heartbeat | Session/generation match and authoritative fields only; foreground is not liveness |
| Corrupt durable state | Atomic replace, validation, degraded restore issue, no healthy-empty fallback |
| Native hang or timer overlap | Deadlines, cancellable async waits, one scheduler timer, no direct timer enforcement |

## Clean Rollout

Ship contracts and passive measurements first. Enable exact-target and native typed adapters independently, then activate the coordinator for one session-scoped composition path. Enable durable restore/issues before making coordinator-derived health canonical. Validate Windows runtime harness evidence and performance bounds before broad activation. Keep one canonical status source, remove the legacy competing enforcement path after stabilization, and archive with current coverage, branch, complexity, allocation, queue/timer, threat, runtime, and rollback evidence clearly separated from historical results.

No data migration, event bus, CQRS framework, new T13 watcher, schema change, or SDD1-SDD3 reopening is part of rollout.

## Open Questions

None.
