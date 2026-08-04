# Apply Progress: windows-runtime-foundations

## Status

- [x] 1.1 RED
- [x] 1.2 GREEN
- [x] 1.3 REFACTOR
- [x] 1.4 RUNTIME
- [x] Unit A complete
- [x] 2.1 RED
- [x] 2.2 GREEN
- [x] 2.3 REFACTOR — changed-line coverage gate satisfied; WTS runtime evidence deferred/unavailable
- [x] Unit B complete — runtime WTS harness deferred/unavailable
- [x] 3.1 RED — C1 codec tests isolated in Domain.Tests; historical C2 RED attempt retained below
- [x] 3.2 GREEN — C1 pure managed codec isolated; historical C2 GREEN attempt rolled back and remains pending
- [x] 3.3 REFACTOR — exact C1 changed-line coverage gate satisfied; branch coverage reported
- [ ] Work Unit C complete — C1 complete; C2 deferred/pending; preserve Unit A/B completion
- [ ] Work Unit D pending

## Implementation

Added the service-owned `RuntimeSecurityVerdict` interpretation for Standard, Administrator, Unknown, and ACL failure outcomes. `ServiceHealthMonitor` now publishes degraded health for Unknown/ACL failure while enforcement remains active. `OnboardingStateService` blocks healthy progression at the service step when the verdict is degraded. `AclHardener` uses replacement semantics for equivalent rules, and `Program` wires startup privilege plus ACL results into the monitor.

## Evidence

- Focused: `dotnet test tests/ControlParental.Service.Tests --no-build --filter "FullyQualifiedName~Hardening"` — PASS, 11/11.
- Coverage: focused XPlat report: 11/11, `RuntimeSecurityVerdict` 100% line/100% branch, `AclHardener` 82.73% file/90% branch with all seven changed `SetAccessRule` lines hit; broad service XPlat report: 775/775, line 47.38%, branch 44.66% aggregate. Changed-line scope for the new/modified Unit A behavior exceeds 80%; aggregate legacy-file rates are not the Unit A gate.
- Harness: PASS. Elevated disposable Windows harness applied ACL twice to a temporary folder and repeated an HKCU registry ACL; equivalent rules and cleanup were verified. No production path or real service registry was touched.
- Broader verification: 774/775 passed; the single failure is the pre-existing `UsageAccumulatorTests.SimulateTick_DoesNotDuplicateWarning_AfterThresholdCrossed` failure (two `ShowWarning` messages).
- Delta: 163 authored additions+deletions estimated for Unit A, below the 400-line limit; the 23 pre-existing changes remain preserved.
- Evidence manifest: `evidence-manifest.md`; evidence revision: `sha256:38ff772b6c1375d781143ff18f72a2fca1bd0dcbd5a6099b08782b7e18adfb80`; native attempt: COMPLETE.

## Rollback Boundary

Revert only the Unit A production and test paths listed in `evidence-manifest.md`, plus the SDD progress artifacts. This does not revert the 23 pre-existing working-tree changes and does not touch B, C, or D seams.

## Pending

Task 1.4 is complete from the persisted elevated harness evidence. Work Units B, C, and D remain pending.

## Unit B implementation and evidence

Unit B adds records keyed by WTS session ID, a cancellable bounded polling loop, per-record lifecycle gates, isolated launcher/channel ownership, session-filtered callbacks, and coalesced recovery. `AgentLauncher` now awaits bounded IPC/process shutdown instead of blocking waits. Unit A implementation and evidence above are preserved unchanged.

- Focused: `dotnet test tests/ControlParental.Service.Tests --no-restore --filter "FullyQualifiedName~SessionManagerLifecycleTests|FullyQualifiedName~AgentLauncherLaunchSeamTests"` — PASS, 11/11; Session filter — PASS, 33/33.
- Broader: `dotnet test tests/ControlParental.Service.Tests --no-build --verbosity minimal` — 781/782 passed; one pre-existing `UsageAccumulatorTests.SimulateTick_DoesNotDuplicateWarning_AfterThresholdCrossed` failure and one duplicate xUnit ID skipped. Unit B tests are green.
- Coverage: exact diff/intersection against initial candidate `ca5b4c1df43ef826df25bf859ac6d04bd1430677`: AgentLauncher 12/23 = 52.17%, SessionWatcher 38/43 = 88.37%, Program/SessionManager 113/132 = 85.61%; combined 163/198 = 82.32%. Approximate branches on changed instrumented lines: 36/60 = 60.00%; Cobertura duplicates generated async state-machine classes, so exact branch union is unavailable.
- Harness: `UNAVAILABLE` — this environment cannot create two real child WTS sessions or run an elevated disposable runtime scenario; no PASS was claimed.
- Delta: native historical Unit B count 427; maintainer-approved ceiling 500. This test-only attempt added 174 authored test lines; no production or C/D files were changed.
- Rollback: revert `Program.cs`, `SessionWatcher.cs`, `AgentLauncher.cs`, `SessionManagerLifecycleTests.cs`, and this Unit B evidence; Unit A files and evidence remain intact.

Pending: runtime WTS evidence is deferred/unavailable. Work Units C and D remain pending.

## Historical C2 attempt (preserved, rolled back)

The prior C2 attempt added authenticated native transport, session handshake, PID/SID/AuthentiCode checks, reconnect, serialized pipe writes, and native security/identity tests. Its evidence remained partial/blocked: exact changed-line coverage was 48/210 (22.86%), the ACL-backed loopback timed out, the real client loopback hung under blame-hang, and AuthentiCode was unavailable/invalidated. These facts are preserved historically; no C2 implementation remains in the working tree.

## Isolated C1 final state

C1 retains only `IpcFrameCodec`: pure managed 4-byte little-endian framing, a strict 64 KiB payload limit, incremental decoding, and JSON-object validation. Tests now live in `tests/ControlParental.Domain.Tests/IpcFrameCodecTests.cs` and cover fragmented/coalesced, malformed, and oversized frames. The managed test harness, focused coverage run, and full solution build have passed.

- Native token: `sha256:507d746b56c92c34c4ed75a63c146decc493cb86934f461531af330ced30557c`.
- C2 paths verified identical to Unit B commit `736d56907d62135e325b22c8f2aa43fc9169408b`.
- C1 verification: focused codec tests 2/2; full solution build 0 errors; exact changed-line coverage 33/39 (84.62%), branches 8/12 (66.67%).
- Evidence manifest: `evidence-manifest-unit-c.md`; current verification revision is calculated after this document is settled.
- Unit A/B implementation and evidence remain preserved; Unit D remains untouched.
