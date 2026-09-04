# Exploration: windows-runtime-foundations

## Current State

### Verified baseline and ownership boundary

- The current working tree is **not clean**: `git status --short` reports 23 pre-existing modified files, including `AgentLauncher.cs`, `NamedPipeServer.cs`, `Program.cs`, and related tests. This exploration treats the current bytes as the baseline and does not attribute those edits to this change.
- `dotnet test --no-build --verbosity minimal` passed **1,071/1,071** tests: Domain 82, SessionAgent 80, App.UI 144, Service 765. The Service run reports one duplicate xUnit test ID warning but no failure.
- `dotnet build --no-restore --verbosity minimal` passed with **0 errors and 627 warnings**. The warning volume is pre-existing quality debt, not a runtime-foundations acceptance criterion.
- No coverage artifact is present in the repository. The project config exposes Coverlet collection, but the requested future rule (>80% line coverage for new/modified code and reported branch coverage) must be enforced by each future work unit rather than inferred from this exploration.
- The UI named-pipe host is present and tested in `src/ControlParental.Service/Interop/NamedPipeUIServer.cs` and `tests/ControlParental.Service.Tests/NamedPipeSecurityTests.cs`; it is not an absent component and is intentionally out of this change.

### Production flow: SCM → Service → SessionWatcher/SessionManager → AgentLauncher → NamedPipe → SessionAgent

1. `Program.Main` builds the hosted .NET service, registers `ControlParentalService`, applies ACL/SCM hardening before SQLite creation, then runs the host. `ApplyHardeningAsync` calls `AclHardener.HardenAllAsync`, `ScmController.ConfigureFailureActionsAsync`, and `ScmController.SetStartupTypeAsync("auto")`.
2. Windows SCM owns process startup and crash restart through the configured `sc.exe failure` actions. `ScmController` is a thin `sc.exe` wrapper; it does not itself coordinate service-instance ownership, wait for state transitions, or perform a backend-dependent recovery.
3. `ControlParentalService.ExecuteAsync` starts health/enforcement monitors, checks the persisted child account, constructs `SessionManager`, wires agent-death recovery, and awaits `SessionManager.StartAsync`.
4. `SessionManager.StartAsync` resolves the child SID, creates one `AgentLauncher`, creates one `SessionWatcher`, and awaits `SessionWatcher.StartAsync`.
5. `SessionWatcher.StartAsync` returns the `Task.Run` task for its infinite polling loop. Therefore the `await` in `SessionManager.StartAsync` does not complete during normal operation. The service never reaches the later IPC-channel injection, enforcement-engine setup, usage start, or steady-state loop until cancellation.
6. On a detected child session, `SessionWatcher` invokes `SessionManager.OnSessionStarted`, which calls `AgentLauncher.LaunchAgentAsync`. The launcher kills any existing process, obtains a WTS user token, creates a `NamedPipeServer`, starts it, then calls `CreateProcessAsUser` with `--pipe=SessionAgent`.
7. The SessionAgent parses the pipe argument, constructs one `NamedPipeClient`, connects with ten one-second retries, subscribes to messages/disconnects, starts foreground monitoring, and emits heartbeats every five seconds.
8. Agent-to-Service messages are dispatched by `NamedPipeServer` to `SessionManager`, then to foreground/heartbeat/snapshot callbacks. Service-to-agent messages use the same channel for overlay, lock, ping, and snapshot commands.

### Shutdown, reconnect, and session behavior

- `SessionWatcher` uses `Thread.Sleep` (two seconds normally, five seconds after errors), so cancellation is not prompt and `Dispose` does not await the polling task.
- `SessionManager.StopAsync` calls `SessionWatcher.Stop/Dispose`, then `AgentLauncher.KillAgentAsync/Dispose`. `AgentLauncher.StopIpcServer` synchronously waits up to one second on `StopAsync`; `AgentLauncher.Dispose` synchronously waits up to two seconds. These blocking waits are on lifecycle paths and can race active pipe reads.
- `AgentLauncher.LaunchAgentAsync` always kills the previous agent and replaces the IPC server. A disconnect callback only records health/death and requests recovery; recovery can race with session changes, shutdown, or another death callback.
- The client retries only during its initial connection. After a connected pipe breaks, `OnDisconnected` logs and the host remains in its heartbeat loop; there is no client reconnection loop.
- The server accepts a pipe instance, starts `ReadMessagesAsync` without awaiting it, and immediately loops. The listener stores one mutable `pipeServer` field, so it is not a per-connection stream model. `SendAsync` writes to whichever mutable stream is current.
- `SessionWatcher` stores one `currentSessionId`; `SessionManager` stores one `currentSessionId`, one launcher, and one channel. Fast user switching replaces the previous session rather than representing multiple child sessions. The current design therefore implements single-session semantics, not multi-session correctness.

### IPC framing, payloads, and authorization

- Both sides serialize JSON bytes directly to a message-mode named pipe and read into a fixed 64 KiB buffer. They assume one `ReadAsync` equals one JSON payload; there is no explicit length-prefix, delimiter, maximum-frame policy, or accumulation for partial/coalesced reads.
- The server has a protected ACL allowing LocalSystem and the child SID, then validates the impersonated client SID. This is a useful existing boundary, but it is server-side client authorization only; the client does not authenticate the server identity or bind the connection to a session identity.
- Unknown message types and malformed JSON are silently dropped. The current maps cover the principal agent messages, but there is no version/correlation/error envelope and no explicit payload-size validation. The existing UI pipe contract is separate and must not be broadened accidentally.

### T37 status

Implemented in part:

- `PrivilegeInspector` detects Standard/Administrator/Unknown and is consumed by `EnforcementLevelMonitor` and `AntiTamperMonitor`.
- `AccountManager` creates/converts accounts through `net.exe`, persists the selected child account through `ChildAccountStore`, and `ControlParentalService` checks the selected account at startup.
- `AclHardener` covers the agent folder, data folder, service registry key, and service binary; startup invokes `HardenAllAsync` before database creation.

Defective or incomplete for this change:

- `AccountManager.IsAccountStandardAsync` duplicates `WindowsIdentity` logic instead of using the injected `IPrivilegeInspector`; `Unknown` and lookup failures collapse into `false` without a distinct health/security reason.
- The startup standard-account result only writes a debug warning. It does not publish a health issue/state transition, prevent unsafe operation, or make the distinction between administrator and unknown explicit.
- ACL methods use `AddAccessRule` on every invocation and do not remove/replace equivalent rules. Repeated startup is not proven idempotent. `ChildAccountStore` calls hardening best-effort and suppresses failure, so a protected account file can be reported as persisted without an enforceable ACL.
- The folder hardening intentionally denies delete/change-permissions, not all writes. That may be correct for service database creation, but the security owner must confirm which files/folders are writable by the child and which must be protected.
- Existing tests mostly assert return values and rule presence; they do not prove repeated application, effective access, failure propagation, or health-state integration.

### T10-A status

Partially implemented:

- SCM failure actions and automatic startup are configured locally through `sc.exe`; this is backend-independent and belongs in scope.
- Agent heartbeat timeout/disconnect detection and `ServiceRecoveryManager` exist, with cooldown, duplicate-recovery suppression, counters, and health levels.

Defective or incomplete:

- The DI registration initially supplies `recoverAgentFunc: () => Task.FromResult(false)` and relies on a later type-specific `SetRecoverAgentFunc` mutation. This is a fragile composition seam and can leave recovery permanently false if startup does not reach that assignment.
- Disconnect and heartbeat expiry can independently request recovery. The current lock is checked before setting recovery mode, leaving a race between concurrent callers; cooldown uses wall-clock time and is not cancellation-aware.
- SCM recovery is not verified as idempotent, does not wait for service state, and has no runtime harness proving crash → SCM relaunch → one service instance.
- T10-B sync/scheduler closure is excluded. `ScheduledWorkService`, backend polling, outbox synchronization, and usage reconciliation are not proposed as fixes here.

## Affected Areas

- `src/ControlParental.Service/Program.cs` — hosted-service composition, startup hardening, lifecycle ordering, recovery callback wiring, and health integration.
- `src/ControlParental.Service/SessionWatcher.cs` — non-blocking start/stop, cancellation, session transition semantics, and multi-session observation.
- `src/ControlParental.Service/AgentLauncher.cs` — launch/kill serialization, process/pipe ownership, reconnect trigger, and rollback-safe shutdown.
- `src/ControlParental.Service/Interop/NamedPipeServer.cs` — per-connection stream ownership, framing, bounded payload reads, SID validation, and cancellation.
- `src/ControlParental.SessionAgent/Interop/NamedPipeClient.cs` — framed reads/writes, reconnect after disconnect, cancellation, and server authentication boundary.
- `src/ControlParental.SessionAgent/Program.cs` — agent host lifecycle and reconnect behavior without `async void` loss of errors.
- `src/ControlParental.Service/PrivilegeInspector.cs`, `AccountManager.cs`, `ChildAccountStore.cs`, `AclHardener.cs` — T37 standard-account verdicts, ACL idempotence, and explicit failure semantics.
- `src/ControlParental.Service/ServiceHealthMonitor.cs`, `ServiceRecoveryManager.cs` — health state publication and single-flight local agent recovery.
- `src/ControlParental.Service/ScmController.cs`, `src/ControlParental.Domain/IScmController.cs` — local SCM state/relaunch seam only; no backend synchronization.
- `tests/ControlParental.Service.Tests/*`, `tests/ControlParental.SessionAgent.Tests/*` — focused behavioral tests, cancellation/lifecycle tests, framing/auth tests, and fake process/pipe seams.

## Approaches

1. **Small repair of the existing runtime seams** — retain the current `SessionWatcher`, `AgentLauncher`, `IIpcChannel`, named-pipe ACL, and health interfaces; fix their ownership, cancellation, framing, single-flight recovery, and explicit T37 verdicts.
   - Pros: smallest compatible change; preserves existing UI pipe and domain contracts; directly addresses observed deadlocks and races.
   - Cons: requires careful synchronization in Windows-specific code; multi-session semantics must be specified before implementation.
   - Effort: High

2. **Replace the runtime with a new supervisor/transport abstraction** — introduce a new lifecycle supervisor, transport protocol, and session registry, then adapt current consumers.
   - Pros: cleaner long-term model for multiple sessions and protocol evolution.
   - Cons: speculative abstractions, larger review surface, higher rollback cost, and likely absorption of excluded scheduler/UI/backend concerns.
   - Effort: High

## Recommendation

Use Approach 1, but keep the implementation in independently rollbackable work units. The minimum coherent runtime is: a non-blocking cancellable session watcher; serialized launch/stop/recovery ownership; a framed, bounded, per-connection IPC stream with explicit cancellation and mutual identity checks; explicit T37 standard/unknown health outcomes with idempotent ACL application; and local SCM failure-action/relaunch proof. Do not redesign the UI pipe host, add backend synchronization, or refactor unrelated services.

### Candidate work units and evidence boundaries

| Unit | Deliverable behavior | Focused behavioral tests | Runtime harness / scenario | Rollback boundary |
|---|---|---|---|---|
| A — T37 verdict and ACL hardening | Standard-account inspection uses one authoritative verdict; administrator/unknown outcomes reach health/degraded state; ACL application is repeatable and failures are observable. | Fake privilege outcomes; repeated ACL application has stable effective rules; protected account-file failure; health classification and startup ordering. | Windows elevated harness applies hardening twice to disposable ProgramData/agent/registry targets, then verifies effective access and service can still create/read its DB. | Revert T37 service-start health/ACL changes and T37 tests only; retain existing account persistence contract.
| B — Session lifecycle supervisor | `StartAsync` returns after starting observation; stop cancels and awaits owned tasks; session changes cannot launch/kill concurrently; one recovery attempt is in flight. | Start/stop completion deadlines; cancellation during polling; session start/end/lock/unlock transitions; launch/kill serialization; shutdown does not block on `.Wait`. | Windows harness logs child session start, lock/unlock, fast switch, service stop, and agent process count; expected no orphan or duplicate agent. | Revert `SessionWatcher`, lifecycle portions of `SessionManager`/`AgentLauncher`, and their tests without reverting IPC protocol code.
| C — Agent IPC transport | Length-delimited/bounded payloads, one read loop per connection, serialized writes, cancellation, SID authorization, server identity binding, and reconnect after disconnect. | Partial/coalesced frames; malformed/oversize payloads; cancellation; unauthorized SID; disconnect/reconnect; concurrent sends; all supported message payloads. | Windows service/agent harness starts the real named pipe, launches a disposable agent, kills/restarts either side, and proves ordered heartbeats plus no cross-session delivery. | Revert transport implementation and transport tests as a unit; preserve the lifecycle supervisor only if its channel seam remains compilable.
| D — T10-A local SCM recovery | Idempotent startup/failure-action configuration and local crash/relaunch behavior with duplicate prevention; recovery is backend-independent. | Fake `sc.exe`/SCM state transitions; repeated configuration; recovery single-flight/cooldown; no backend invocation. | Windows harness registers an isolated service name, crashes/stops the service, waits for SCM relaunch, and verifies one running instance and one agent tree. | Revert `ScmController`/recovery wiring and T10-A tests; do not revert T10-B scheduler/sync code.

### Forecast and delivery

The combined authored change is likely above the supplied **800-line review budget** because Units B and C touch two executables plus Windows-specific tests and harness seams. A practical auto-chain is Slice 1 (A + D, local security/SCM foundations) followed by Slice 2 (B + C, session/IPC runtime), with each slice independently buildable and testable. If implementation forecasting shows either slice above 800 authored additions+deletions, split Unit B and Unit C into separate chained slices; do not split tests away from the behavior they prove. Every unit must record focused test results, runtime-harness result or explicit N/A, line coverage >80% for new/modified code, reported branch coverage, and exact rollback files/behavior.

## Risks

- The existing `SessionManager.StartAsync` awaits an infinite watcher task, so runtime startup correctness is currently unproven despite green unit tests.
- Shared mutable pipe state, fire-and-forget read loops, and unframed JSON can corrupt payload boundaries or deliver messages to the wrong connection during reconnect.
- “Mutual authentication” is a security decision, not merely a transport detail: the acceptable server identity proof and whether a per-session secret is required must be confirmed before specification.
- Multi-session behavior changes enforcement semantics. Human confirmation is required on whether multiple simultaneous child sessions are supported, rejected, or serialized to one active session.
- Human confirmation is required for the fail-safe policy when privilege inspection returns `Unknown`, when ACL hardening fails, or when the child is already an administrator; these choices affect protection and usability.
- Repeated ACL changes can lock out service/test cleanup if access rules are too broad. The exact writable surface for ProgramData, the database, secrets, and the selected-account file must be approved.
- SCM failure actions may restart the service while a previous instance is still stopping. State-transition waits and duplicate-instance boundaries require a Windows runtime harness, not only mocks.
- The working tree contains 23 unrelated modifications. Any implementation must preserve ownership boundaries and avoid reverting or formatting those edits.
- Existing health/recovery callbacks can be triggered from both disconnect and heartbeat timeout. Without single-flight coordination, recovery can kill a newly launched agent or create an orphan process.
- The current build has 627 warnings; warning cleanup and broad refactoring are out of scope unless a touched line makes a warning a direct acceptance blocker.

## Ready for Proposal

**No, pending human decisions.** The codebase is sufficiently mapped for a proposal after confirming: (1) multi-session policy, (2) mutual service/agent authentication mechanism and trust boundary, and (3) fail-safe behavior for Unknown privilege and ACL-hardening failure. No product defaults are invented here.

## Section D Envelope

- `status`: success
- `executive_summary`: Current code confirms a green 1,071-test/zero-error-build baseline but not a trustworthy runtime: SessionManager startup awaits the infinite watcher loop, IPC is unframed and single-stream mutable, reconnect is incomplete, and session state is single-session. T37 and T10-A foundations exist but require explicit health/error semantics, idempotent hardening, and local recovery proof.
- `artifacts`: OpenSpec `openspec/changes/windows-runtime-foundations/exploration.md`; Engram `sdd/windows-runtime-foundations/explore` in project `ddddddddd`
- `next_recommended`: Resolve the three human security/product decisions, then run `sdd-propose`; do not implement from exploration alone.
- `risks`: High runtime race/deadlock and security-boundary risk; combined implementation forecast likely exceeds 800 authored changed lines, so auto-chain is recommended if the forecast remains high.
- `skill_resolution`: paths-injected — loaded the exact `sdd-explore`, `work-unit-commits`, `cognitive-doc-design`, and project `.github/copilot-instructions.md` paths before investigation.
