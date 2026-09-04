# Tasks: Windows Runtime Foundations

## Canonical Current Closure Summary

**Status: ARCHIVED / COMPLETE.** All phases, verification, and archive work are closed on the authoritative snapshot.

- Full solution build: PASS.
- Focused App.UI filter: PASS, 72/72.
- Focused Service onboarding/UIMessageHandler filter: PASS, 43/43.
- Full Service suite: PASS, 870/870.
- Remaining closure work: none.

## Historical Planning And Execution Record

Everything below this heading is preserved chronological planning and execution evidence. Terms such as `current`, `pending`, `open`, `blocked`, or `incomplete today` describe their original snapshot and do not override the canonical closure summary above.

Preserve 23 changes. Exclude T10-B, T03, T05–T09, T11+, backend, scheduler, usage, UI pipe host, WNS, Realtime, packaging.

## Review Workload Forecast

Existing implementation exceeds 400 lines; `size:exception` approved. Managed-gate tests/evidence for 46 hits: 180–320 authored lines; remaining work unlikely to exceed 400.

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low
Delivery strategy: exception-ok

| Unit | Focused command | Runtime harness | Rollback boundary |
|---|---|---|---|
| C2 gates | `dotnet test` Domain/Service/SessionAgent C2 suites with Cobertura | One authorized signed scenario; N/A for classification/coverage | C2 evidence only; preserve production |
| D | `dotnet test tests/ControlParental.Service.Tests --no-build --filter FullyQualifiedName~Recovery` | Local SCM crash/relaunch, no backend | D files and tests only |

## Phase 1: A — Hardening and verdict

- [x] 1.1–1.3 RED/GREEN/REFACTOR: privilege verdicts, idempotent ACL, degraded health/onboarding; >80% lines and branches.
- [x] 1.4 RUNTIME: repeat elevated disposable hardening; verify equivalence/cleanup, never production registry.

## Phase 2: B — Multi-session lifecycle

- [x] 2.1–2.3 RED/GREEN/REFACTOR: cancellable isolated lifecycle, keyed ownership, single-instance recovery in `SessionWatcher.cs`, `Program.cs`, `AgentLauncher.cs`; 82.32% lines and branches.

## Phase 3: C1 — Framed codec

- [x] 3.1–3.3 RED/GREEN/REFACTOR: pure-managed `IpcFrameCodec`, bounded incremental framing; 33/39 lines (84.62%), 8/12 branches (66.67%).

## Phase 4: C2 — Native authenticated transport

- [x] 4.1 CLASSIFY (historical/superseded): candidate, per-line `design.md` classification, reasoned path+line manifest/async union; 481 managed + 156 native = 637; revision `sha256:e344f980b0fddc92cbbbade3fc633d62b01f2a30632bba4dee0118b805a3f624`.
- [x] 4.2 MANAGED GATE: fresh exact reconciliation is `433/541 = 80.036969%`; strict `>80%` passes at the exact `433`-hit threshold. Historical `232/288` evidence remains invalidated.
    - Current evidence revision: `sha256:cfa3db6bf8760b2bd9baf7a8d05f6572a0eccba8d34837fcd7d124dab4b1aa55`.
  - [x] 4.2.1 Added behavior-value tests through existing managed seams for malformed handshake value types, signer/trust rejection, process identity metadata, restricted SID helper validation, authenticated usage-state dispatch, and wrong-server-identity rejection.
   - [x] 4.2.2 Refresh the C2 classification and executable denominator for the listener lifecycle production change; preserve all native-boundary ranges. Current reconciliation is `801` source-context, `260` non-executable, `541` executable.
     - [x] 4.2.3 Reconcile fresh Cobertura by normalized path+line and settle the strict `>80%` gate without carrying forward `232/288`. Current strict result: PASS; `433/541 = 80.036969%`.
- [x] 4.3 NATIVE GATE (exclusively): corrected WinTrust ABI; disposable harness before side effects; CurrentUser-only cert; no PFX/private-key export/LocalMachine; one authorized run. Signed acceptance, ordering, dispatch, and runtime deadline proof are now complete; raw WinTrust was still not emitted by the harness and remains an evidence gap, not a blocker.
- [x] 4.4 CLOSE: both gates passed; C2 evidence recorded and Unit D unblocked. Historical global `50.13%` is not a managed-scope pass.

## Phase 5: D — SCM and local recovery

 - [x] 5.1 RED: extend `ServiceHostStartTests.cs`, `ServiceHealthMonitorTests.cs`, `ScmControllerTests.cs` for idempotency, crash/relaunch, one instance, backend independence.
 - [x] 5.2 GREEN: idempotent startup/failure actions and bounded single-flight recovery in `ScmController.cs`, `ServiceRecoveryManager.cs`, `Program.cs`.
- [x] 5.3 REFACTOR: isolate SCM rollback from scheduler/sync; proved >80% lines and branches with focused recovery tests.

Unit D is complete and archived. Verify/archive are complete. Task 4.1 remains complete under the historical revision above. Task 4.2 and 4.2.2–4.2.3 pass on fresh exact coverage. Tasks 4.3 and 4.4 are complete.

## C2-atomic-listener-ownership

- Implemented atomic immutable listener ownership and deterministic TCS-gated race tests in the existing AgentLauncher seam.
- Task 4.2 and 4.2.2–4.2.3 remain open; coverage was not run. Task 4.3 is open/blocked, and 4.4/Unit D remain blocked.
- Current evidence revision: `sha256:a565373ff7def34336fe6ab107a5a4b7c96ce70ab23fb39626b3e7f9095de8dd`.

## C2-provisional-ownership-cleanup

- [x] Corrected exception-safe provisional cleanup and the synchronous gated Dispose/publication regression in the existing AgentLauncher seam.
- Focused launcher tests passed `19/19`; Service test build passed with `0` errors. Coverage was not run; prior `232/288` remains invalidated.
- Tasks 4.2 and 4.2.2–4.2.3 remain open; 4.3 remains open/blocked; 4.4 and Unit D remain blocked. Current canonical evidence revision: `sha256:46504dbdc50a45b6bd6ef43ced3a9242fe0b097449de31b0b82d01f53f610a2b`.

## C2-final-focused-tests (current apply)

- Added focused behavior-value tests only to the existing `AgentLauncherLaunchSeamTests.cs` seam: token duplication failure, environment-block failure, process API exception, listener cleanup exceptions, idempotent disposal, and launch-after-dispose rejection.
- Authored test delta: 97 changed lines (95 additions, 2 deletions) in the existing test file; no production, package, seam, native, harness, or new-file changes.
- Fresh focused Cobertura suites passed Domain `11/11`, Service `72/72`, and SessionAgent `23/23`; focused Service build passed with `0` errors and 5 existing package warnings.
- Exact managed table is `801` source-context, `260` non-executable, `541` executable denominator, `433` hits, `108` mapped unhit; `433/541 = 80.036969%` strict PASS. Native-boundary `156` remains unchanged.
- Task 4.2 and 4.2.2–4.2.3 are complete. Task 4.3 remains next/open but blocked; 4.4 and Unit D remain blocked. Runtime harness is N/A/invalidated and native token `sha256:76661139900ac1c30c5c3635331c76aa813c473966575ec3a1b2726e1485a123` was retained without ledger call.
- Raw Cobertura was retained through exact path+line reconciliation and evidence capture, then cleaned. Canonical revision is propagated to all four C2 evidence documents.
