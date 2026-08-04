# Tasks: Windows Runtime Foundations

Preserve all 23 pre-existing working-tree changes. Exclude T10-B, T03, T05–T09, T11+, backend, scheduler, usage, UI pipe host, WNS, Realtime, packaging, and warning cleanup.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 900–1,300 authored lines |
| 400-line budget risk | High |
| Chained PRs recommended | Yes (technical forecast only; size exception accepted) |
| Suggested split | Commits A → B → C1 → D; C2 pending before final verify/archive and one final PR to main after the approved issue |
| Delivery strategy | exception-ok |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: size-exception
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| A | Authoritative verdict, ACL, degraded health | Final PR / commit A | `dotnet test tests/ControlParental.Service.Tests --no-build --filter FullyQualifiedName~Hardening` | Elevated Windows harness: disposable folder/ProgramData/registry targets, harden twice, compare equivalent rules/effective access, cleanup; never production or real service registry | `PrivilegeInspector.cs`, `AclHardener.cs`, health/onboarding verdict wiring and tests |
| B | Cancellable per-session ownership/lifecycle | Final PR / commit B | `dotnet test tests/ControlParental.Service.Tests --no-build --filter FullyQualifiedName~Session` | Windows harness: two sessions, fast switch, stop cancellation | `SessionWatcher.cs`, `Program.cs` session records, `AgentLauncher.cs` lifecycle and tests |
| C1 | Pure managed framed codec | Final PR / commit C1 | `dotnet test tests/ControlParental.Service.Tests --no-build --filter FullyQualifiedName~IpcFrameCodec` | N/A: pure managed framing | `IpcFrameCodec` and codec tests |
| C2 | Native authenticated transport | Final PR / pending commit C2 | `dotnet test tests/ControlParental.Service.Tests tests/ControlParental.SessionAgent.Tests --no-build --filter FullyQualifiedName~AuthenticatedSessionIpc` | Deferred: ACL-backed/authenticated Windows harness | `NamedPipeServer.cs`, `NamedPipeClient.cs`, identity seams and transport tests |
| D | Local SCM setup and crash recovery | Final PR / commit D | `dotnet test tests/ControlParental.Service.Tests --no-build --filter FullyQualifiedName~Recovery` | Windows local SCM crash/relaunch; no backend | `ScmController.cs`, `ServiceRecoveryManager.cs`, startup wiring and tests |

## Phase 1: A — Hardening and verdict

- [x] 1.1 RED: extend `PrivilegeInspectorTests`, `AclHardenerTests`, `ServiceHealthMonitorTests`, and `OnboardingStateServiceTests` for Standard, Administrator, Unknown, repeated ACL, and ACL-failure outcomes; assert enforcement stays active, `DEGRADED`, and onboarding blocks.
- [x] 1.2 GREEN: add `RuntimeSecurityVerdict` and authoritative `IAclHardener` seam usage in `PrivilegeInspector.cs`, `AclHardener.cs`, `ServiceHealthMonitor.cs`, `OnboardingStateService.cs`, and `Program.cs`.
- [x] 1.3 REFACTOR: keep verdict ownership service-side, make ACL rules idempotent, remove duplicate health interpretation; prove >80% changed-code line coverage and report branch coverage.
- [x] 1.4 RUNTIME: execute an elevated, bounded Windows harness against disposable folder/ProgramData/registry targets; apply hardening twice, verify equivalent rules and expected effective access, then safely clean up without production or real service-registry use.

## Phase 2: B — Multi-session lifecycle

- [x] 2.1 RED: add `SessionManagerLifecycleTests.cs` beside `AgentLauncherLaunchSeamTests.cs` for cancellation, simultaneous sessions/fast switching, duplicate launches, and session-filtered dispatch; cover watcher callbacks from `SessionWatcher.cs`.
- [x] 2.2 GREEN: replace current-session state with keyed records, bounded event queue, per-session gates, owned cancellation, and single-instance launch/stop in `SessionWatcher.cs`, `Program.cs`, and `AgentLauncher.cs`.
- [x] 2.3 REFACTOR: await owned shutdown/recovery only; changed-line coverage is 82.32% and branch coverage is reported with Cobertura limitation.

## Phase 3: C1 — Framed codec (managed)

- [x] 3.1 RED: add codec tests for fragmented/coalesced, malformed, and >64 KiB frames in `ControlParental.Domain.Tests`.
- [x] 3.2 GREEN: implement pure-managed `IpcFrameCodec` with 4-byte little-endian length, 64 KiB bound, and incremental decode.
- [x] 3.3 REFACTOR: prove exact changed-code line coverage >80% and report branch coverage; no native harness required. C1: 33/39 lines (84.62%), 8/12 branches (66.67%).

## Phase 4: C2 — Native authenticated transport (DEFERRED/PENDING)

- [ ] 4.1 RED: test session-bound handshake, client SID/PID/session, server PID/Authenticode, cross-session writes, reconnect, and rejection before dispatch.
- [ ] 4.2 GREEN: add per-connection streams, serialized writes, authorized reconnect, identity validation, and no raw fallback in `NamedPipeServer.cs`, `NamedPipeClient.cs`, and identity seams.
- [ ] 4.3 REFACTOR: run the real ACL-backed/authenticated Windows harness; prove exact changed-code line coverage >80% and report branch coverage.

## Phase 5: D — SCM and local recovery

- [ ] 5.1 RED: extend `ServiceHostStartTests.cs`, `ServiceHealthMonitorTests.cs`, and new `ScmControllerTests.cs` for repeated configuration, crash/relaunch, one active instance, and backend-independent recovery.
- [ ] 5.2 GREEN: add idempotent startup/failure actions and bounded single-flight local recovery in `ScmController.cs`, `ServiceRecoveryManager.cs`, and `Program.cs`.
- [ ] 5.3 REFACTOR: isolate SCM rollback from scheduler/sync; prove >80% changed-code line coverage and report branch coverage.

D may run while C2 awaits its native environment; final verify/archive remains blocked until C2 completes.

### Current C1/C2 disposition

- C1 implementation and RED tests are isolated to Domain; 3.3 remains open until verification passes.
- C2 is fully pending. Its prior native implementation and identity/security tests were rolled back to Unit B commit `736d56907d62135e325b22c8f2aa43fc9169408b` without changing A/B paths.
- The prior C2 native failures remain historical evidence and are not deleted.
