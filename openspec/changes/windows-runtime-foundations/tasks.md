# Tasks: Windows Runtime Foundations

Preserve all 23 pre-existing working-tree changes. Exclude T10-B, T03, T05–T09, T11+, backend, scheduler, usage, UI pipe host, WNS, Realtime, packaging, and warning cleanup.

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | 900–1,300 authored lines |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR A → PR B → PR C → PR D; each merges to main before the next |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|---|
| A | Authoritative verdict, ACL, degraded health | PR A | `dotnet test tests/ControlParental.Service.Tests --no-build --filter FullyQualifiedName~Hardening` | Elevated Windows harness: disposable folder/ProgramData/registry targets, harden twice, compare equivalent rules/effective access, cleanup; never production or real service registry | `PrivilegeInspector.cs`, `AclHardener.cs`, health/onboarding verdict wiring and tests |
| B | Cancellable per-session ownership/lifecycle | PR B | `dotnet test tests/ControlParental.Service.Tests --no-build --filter FullyQualifiedName~Session` | Windows harness: two sessions, fast switch, stop cancellation | `SessionWatcher.cs`, `Program.cs` session records, `AgentLauncher.cs` lifecycle and tests |
| C | Framed authenticated IPC and reconnect | PR C | `dotnet test tests/ControlParental.Service.Tests tests/ControlParental.SessionAgent.Tests --no-build --filter FullyQualifiedName~Pipe` | Windows loopback pipe: fragmentation, reconnect, two sessions | `NamedPipeServer.cs`, `NamedPipeClient.cs`, identity seams and tests |
| D | Local SCM setup and crash recovery | PR D | `dotnet test tests/ControlParental.Service.Tests --no-build --filter FullyQualifiedName~Recovery` | Windows local SCM crash/relaunch; no backend | `ScmController.cs`, `ServiceRecoveryManager.cs`, startup wiring and tests |

## Phase 1: A — Hardening and verdict

- [ ] 1.1 RED: extend `PrivilegeInspectorTests`, `AclHardenerTests`, `ServiceHealthMonitorTests`, and `OnboardingStateServiceTests` for Standard, Administrator, Unknown, repeated ACL, and ACL-failure outcomes; assert enforcement stays active, `DEGRADED`, and onboarding blocks.
- [ ] 1.2 GREEN: add `RuntimeSecurityVerdict` and authoritative `IAclHardener` seam usage in `PrivilegeInspector.cs`, `AclHardener.cs`, `ServiceHealthMonitor.cs`, `OnboardingStateService.cs`, and `Program.cs`.
- [ ] 1.3 REFACTOR: keep verdict ownership service-side, make ACL rules idempotent, remove duplicate health interpretation; prove >80% changed-code line coverage and report branch coverage.
- [ ] 1.4 RUNTIME: add/run elevated `AclHardeningWindowsHarnessTests.cs` against disposable folder/ProgramData/registry targets; harden twice, verify equivalent rules and expected effective access, then safely clean up without production or real service-registry use.

## Phase 2: B — Multi-session lifecycle

- [ ] 2.1 RED: add `SessionManagerLifecycleTests.cs` beside `AgentLauncherLaunchSeamTests.cs` for cancellation, simultaneous sessions/fast switching, duplicate launches, and session-filtered dispatch; cover watcher callbacks from `SessionWatcher.cs`.
- [ ] 2.2 GREEN: replace current-session state with keyed records, bounded event queue, per-session gates, owned cancellation, and single-instance launch/stop in `SessionWatcher.cs`, `Program.cs`, and `AgentLauncher.cs`.
- [ ] 2.3 REFACTOR: await owned shutdown/recovery only; prove >80% changed-code line coverage and report branch coverage.

## Phase 3: C — IPC framing, auth, reconnect

- [ ] 3.1 RED: before production code, extend `NamedPipeSecurityTests.cs`, `AppIdentityResolverTests.cs`, and `SessionAgentHostTests.cs` for wrong client SID/PID/session, fake/unsigned/signer-mismatch server, fragmented/coalesced/malformed/>64 KiB frames, cross-session writes, and repeated disconnects.
- [ ] 3.2 GREEN: implement the 4-byte little-endian/64 KiB decoder, serialized writes, session-bound handshake, PID/Authenticode validation, rejection-before-dispatch, and authorized reconnect in `NamedPipeServer.cs`, `NamedPipeClient.cs`, and identity seams.
- [ ] 3.3 REFACTOR: preserve the UI pipe contract and remove raw-stream fallback; prove >80% changed-code line coverage and report branch coverage.

## Phase 4: D — SCM and local recovery

- [ ] 4.1 RED: extend `ServiceHostStartTests.cs`, `ServiceHealthMonitorTests.cs`, and new `ScmControllerTests.cs` for repeated configuration, crash/relaunch, one active instance, and backend-independent recovery.
- [ ] 4.2 GREEN: add idempotent startup/failure actions and bounded single-flight local recovery in `ScmController.cs`, `ServiceRecoveryManager.cs`, and `Program.cs`.
- [ ] 4.3 REFACTOR: isolate SCM rollback from scheduler/sync; prove >80% changed-code line coverage and report branch coverage.
