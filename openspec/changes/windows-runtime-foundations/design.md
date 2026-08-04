# Design: Windows Runtime Foundations

## Technical Approach

Repair the existing `SessionWatcher` → `SessionManager` → `AgentLauncher` → named-pipe seams. Keep the Service host, UI pipe, backend, usage, scheduler, packaging, and warnings unchanged. The 23 modified files are pre-existing; the delta is limited to the seams and tests below.

## Architecture Decisions

| Decision | Choice | Rejected | Rationale |
|---|---|---|---|
| Session ownership | `SessionManager` owns records keyed by WTS session ID; each owns one `AgentLauncher`, connection, cancellation source, and lifecycle gate. | Replacement supervisor or global current-session state. | Preserves the launch seam while preventing cross-session actions and duplicate agents. |
| Lifecycle | Queue watcher events and serialize each record's start/stop/recover operation with a per-session gate; lifecycle awaits only owned tasks with bounded cancellation. | Blocking `.Wait`, `Thread.Sleep`, or global locks. | Makes fast switching and shutdown deterministic without redesigning the host. |
| Recovery | `ServiceRecoveryManager` requests recovery for the affected session; a record-local single-flight task coalesces disconnect and timeout signals. | Independent recovery per callback. | Retains health/recovery integration and guarantees at most one relaunch. |
| IPC | Use a 4-byte little-endian length prefix, maximum 64 KiB frame, incremental decoder, per-connection write semaphore, and bound session ID. | Raw `ReadAsync` boundaries or a new transport framework. | Compatible with current JSON and fixes partial/coalesced reads. |
| Identity | Handshake claims session ID and PID. Service validates client PID/token SID; SessionAgent obtains server PID with `GetNamedPipeServerProcessId`, then verifies the server image with `WinVerifyTrust` and the same trusted signer identity as the agent. | Per-session secrets or process replacement. | Meets the approved identity model without introducing secret storage. |
| Security verdict | Service-owned `RuntimeSecurityVerdict` is the sole source for privilege, ACL, health, and onboarding. | Independent health flags. | Prevents `Unknown` or failed ACL repair from being reported healthy. |

## Data Flow

`SessionWatcher` → bounded event queue → `SessionManager[sessionId]` → gated `AgentLauncher` → authenticated framed connection → session handlers.

On startup, `PrivilegeInspector` and idempotent ACL hardening publish one verdict to `ServiceHealthMonitor`; `OnboardingStateService` blocks healthy progression for `Unknown` or repair failure while enforcement remains active. `ScmController` applies startup/failure actions idempotently; recovery excludes scheduler/sync.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/ControlParental.Service/Program.cs` | Modify | Replace `currentSessionId` ownership with per-session records; bounded async lifecycle and session-filtered dispatch. |
| `src/ControlParental.Service/SessionWatcher.cs` | Modify | Cancellation-aware event delivery; report session IDs for end/lock/unlock without blocking sleeps. |
| `src/ControlParental.Service/AgentLauncher.cs` | Modify | Per-session process/channel ownership, single-flight launch/stop, PID binding, and bounded cancellation. |
| `src/ControlParental.Service/Interop/NamedPipeServer.cs` and `src/ControlParental.SessionAgent/Interop/NamedPipeClient.cs` | Modify | Shared framing/auth contract, bounded decoder, serialized writes, authenticated reconnect. |
| `src/ControlParental.Service/ServiceHealthMonitor.cs`, `PrivilegeInspector.cs`, `OnboardingStateService.cs` | Modify | Consume the authoritative security verdict and expose `DEGRADED`/onboarding blocking. |
| `src/ControlParental.Service/ScmController.cs` | Modify | Idempotent SCM configuration and local crash/relaunch verification only. |
| `tests/...` | Modify/Create | Focused RED/GREEN tests and Windows runtime/SCM harness seams. |

## Interfaces / Contracts

Internal seams only: `IProcessIdentityVerifier`, `IAclHardener`, `IScmController`, and clock/process/Pipe API seams for native calls. `RuntimeSecurityVerdict = HealthyStandard | Administrator | Unknown | AclFailure`; the last two keep enforcement enabled but force `DEGRADED`. Handshake rejection closes before dispatch and records session failure.

## Testing Strategy

Use RED/GREEN/REFACTOR work units: (1) session ownership/lifecycle, (2) framing/auth/reconnect, (3) privilege/ACL health/onboarding, (4) SCM setup/recovery. Each changed production scope requires >80% line coverage and reported branch coverage. Unit tests use seams; Windows integration tests use loopback pipes and a local signed Service/Agent harness for two sessions, crash/reconnect, and SCM recovery. No backend is required.

## Threat Matrix

| Threat | Applicability / safe and failure behavior | Planned RED test |
|---|---|---|
| Wrong client SID/PID or session | Applicable; accept only token/session match, otherwise close before dispatch. | Unauthorized client cannot deliver a message. |
| Wrong server PID or Authenticode signer | Applicable; agent rejects and reports disconnected/degraded. | Fake PID, unsigned image, and signer mismatch are rejected. |
| Frame smuggling/oversize/malformed input | Applicable; bounded decoder rejects without dispatch. | Fragmented, coalesced, malformed, and >64 KiB frames. |
| Cross-session connection/write | Applicable; connection record and write gate are session-bound. | Concurrent two-session sends never cross streams. |
| Reconnect race | Applicable; authorization repeats and recovery is single-flight. | Repeated disconnects produce one recovery and preserve session ID. |
| Documentation/Git/commit/push/PR command boundaries | N/A; this change executes no repository or shell automation. | None. |

## Migration / Rollout

Protocol rollout accepts only the new framed handshake; an unauthenticated or legacy raw stream is rejected, so no silent downgrade exists. Roll back one work unit with its tests: session ownership, IPC, security verdict, and SCM are independent boundaries. SCM rollback restores prior actions without touching scheduler or sync.

## Open Questions

No blocking questions; signer validation uses the existing signed-binary identity.
