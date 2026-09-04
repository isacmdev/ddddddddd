# Apply Progress: windows-runtime-foundations

## Canonical Current Closure Summary

**Status: ARCHIVED / COMPLETE.** This summary supersedes point-in-time status language in the historical ledger below.

- Full solution build: PASS.
- Focused App.UI filter: PASS, 72/72.
- Focused Service onboarding/UIMessageHandler filter: PASS, 43/43.
- Full Service suite: PASS, 870/870.
- Remaining closure work: none.

## Historical Apply And Evidence Ledger

Everything below this heading is preserved chronological evidence. Terms such as `current`, `pending`, `open`, or `blocked` describe the snapshot of the subsection in which they appear; they are not the current closure status.

### Recorded Status

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
- [x] Work Unit C complete — C1 complete; C2 signed harness passed; preserve Unit A/B completion
- [x] Work Unit D complete and archived

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

## Archive

Task 1.4 is complete from the persisted elevated harness evidence. Work Units B, C, and D are complete.

## Unit B implementation and evidence

Unit B adds records keyed by WTS session ID, a cancellable bounded polling loop, per-record lifecycle gates, isolated launcher/channel ownership, session-filtered callbacks, and coalesced recovery. `AgentLauncher` now awaits bounded IPC/process shutdown instead of blocking waits. Unit A implementation and evidence above are preserved unchanged.

- Focused: `dotnet test tests/ControlParental.Service.Tests --no-restore --filter "FullyQualifiedName~SessionManagerLifecycleTests|FullyQualifiedName~AgentLauncherLaunchSeamTests"` — PASS, 11/11; Session filter — PASS, 33/33.
- Broader: `dotnet test tests/ControlParental.Service.Tests --no-build --verbosity minimal` — 781/782 passed; one pre-existing `UsageAccumulatorTests.SimulateTick_DoesNotDuplicateWarning_AfterThresholdCrossed` failure and one duplicate xUnit ID skipped. Unit B tests are green.
- Coverage: exact diff/intersection against initial candidate `ca5b4c1df43ef826df25bf859ac6d04bd1430677`: AgentLauncher 12/23 = 52.17%, SessionWatcher 38/43 = 88.37%, Program/SessionManager 113/132 = 85.61%; combined 163/198 = 82.32%. Approximate branches on changed instrumented lines: 36/60 = 60.00%; Cobertura duplicates generated async state-machine classes, so exact branch union is unavailable.
- Harness: `UNAVAILABLE` — this environment cannot create two real child WTS sessions or run an elevated disposable runtime scenario; no PASS was claimed.
- Delta: native historical Unit B count 427; maintainer-approved ceiling 500. This test-only attempt added 174 authored test lines; no production or C/D files were changed.
- Rollback: revert `Program.cs`, `SessionWatcher.cs`, `AgentLauncher.cs`, `SessionManagerLifecycleTests.cs`, and this Unit B evidence; Unit A files and evidence remain intact.

Pending: runtime WTS evidence is deferred/unavailable. Work Units C and D are complete.

## C2 task 4.2 managed coverage gate

- Work unit `C2-managed-coverage-gate`; native token retained exactly `sha256:6ef323119cb54b4c2fd1e54d91e0da6e1f53a405e242929b7da161cd7059043c`; native attempt ledger was not mutated.
- Frozen C2 production scope revalidated before tests: `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`; historical/superseded classification revision `sha256:e344f980b0fddc92cbbbade3fc633d62b01f2a30632bba4dee0118b805a3f624` remains applicable.
- Current authoritative managed-gate evidence revision: `sha256:7909dfb285161af578f2f25c93f8cde13ac59d5b22d9b3400420c506060fddcd`.
- Fresh focused Cobertura suites passed Domain 9/9, Service 34/34, and SessionAgent 15/15; focused Service and SessionAgent builds passed with 0 errors.
- Exact managed union: 185/481 = 38.461538%; 193 managed lines had no Cobertura mapping and remained in the denominator. Strict `>80%`: FAIL; task 4.2 remains open.
- Branch evidence is bounded aggregate only: Domain 16/1,024 (1.56%), Service 138/3,630 (3.80%), SessionAgent 45/1,338 (3.36%). Exact changed-branch union is unavailable because Cobertura duplicates async state-machine mappings; no branch PASS is claimed.
- Historical global `192/383 = 50.13%` is distinct and was not reused as the authoritative managed gate.
- Harness disposition `invalidated` by absolute scope exclusions; no runtime, certificate, signing, signtool, named-pipe loopback, or native-ledger activity occurred. Coverage artifacts were removed only after intersection and evidence capture.
- Changed paths are evidence docs only; rollback removes this section and the corresponding current C2 evidence additions without touching source or tests.

## C2 task 4.2 bounded diagnosis continuation

- Work unit `C2-complete-managed-coverage-gate`; native token retained exactly `sha256:15ae8f7922db8d8033a75e70c23c2e7b73043b649489fa33594b28d6cc7fbd5d`; the native attempt ledger was not acquired or mutated.
- Fresh baseline mapping reran the Domain `IpcHandshakeTests` suite (9/9), Service C2 filter (34/34), and SessionAgent C2 filter (15/15), all with XPlat Cobertura collection. Focused Service and SessionAgent builds exited 0 errors.
- Scope revalidation is unchanged: 481 `managed-deterministic` + 156 `native-boundary` = 637 additions; no production/test file changed in this work unit, so the candidate and classification remain authoritative.
- Diagnosis of all 193 unmapped managed lines: `193 compiler/source mapping` (no Cobertura source mapping; retained in the denominator), `0 unexecuted behavior`, `0 unreachable defensive path`, and `0 classification defect`. The remaining 103 mapped-but-unhit lines are meaningful unexecuted managed behavior: handshake/validation failure, transport lifecycle, serialized writes, reconnect/retry/deadline, signer orchestration, and fail-closed paths.
- Exact managed union is 185/481 = 38.461538%; mapped-line ceiling is 288/481 = 59.875260%, below strict `>80%` even if every currently mapped line were hit. Therefore the gate cannot be honestly completed without a broad source-layout/instrumentation rewrite, coverage exclusion, or denominator manipulation; none is permitted by the design.
- Branch evidence remains bounded aggregate only: Domain 16/1,024 (1.56%), Service 138/3,630 (3.80%), SessionAgent 45/1,338 (3.36%). Cobertura async duplicate mappings prevent an exact changed-branch union; no branch PASS is claimed.
- Outcome: `blocked`; task 4.2 remains unchecked, task 4.3 remains next but blocked by the open managed gate and absolute native exclusions, and 4.4/Unit D remain blocked.
- Current authoritative managed-gate evidence revision: `sha256:7909dfb285161af578f2f25c93f8cde13ac59d5b22d9b3400420c506060fddcd`.
- Harness disposition `invalidated` by absolute exclusions; no runtime, certificate/store mutation, signing, signtool, named-pipe loopback, or native-ledger activity occurred. Temporary coverage artifacts were removed after intersection; no harness process remained.
- Changed paths: this progress artifact and the three C2 evidence artifacts only. Rollback removes this continuation and its corresponding evidence updates without touching source/tests or prior history.

## Historical C2 attempt (preserved, rolled back)

The prior C2 attempt added authenticated native transport, session handshake, PID/SID/AuthentiCode checks, reconnect, serialized pipe writes, and native security/identity tests. Its evidence remained partial/blocked: exact changed-line coverage was 48/210 (22.86%), the ACL-backed loopback timed out, the real client loopback hung under blame-hang, and AuthentiCode was unavailable/invalidated. These facts are preserved historically; no C2 implementation remains in the working tree.

## Isolated C1 final state

C1 retains only `IpcFrameCodec`: pure managed 4-byte little-endian framing, a strict 64 KiB payload limit, incremental decoding, and JSON-object validation. Tests now live in `tests/ControlParental.Domain.Tests/IpcFrameCodecTests.cs` and cover fragmented/coalesced, malformed, and oversized frames. The managed test harness, focused coverage run, and full solution build have passed.

- Native token: `sha256:507d746b56c92c34c4ed75a63c146decc493cb86934f461531af330ced30557c`.
- C2 paths verified identical to Unit B commit `736d56907d62135e325b22c8f2aa43fc9169408b`.
- C1 verification: focused codec tests 2/2; full solution build 0 errors; exact changed-line coverage 33/39 (84.62%), branches 8/12 (66.67%).
- Evidence manifest: `evidence-manifest-unit-c.md`; current verification revision is calculated after this document is settled.
- Unit A/B implementation and evidence remain preserved; Unit D remains untouched.

## C2 implementation attempt and evidence

C2 adds a framed session-bound handshake, native client/server PID discovery, SID/session checks, fail-closed Authenticode signer matching, per-connection ownership, serialized writes, authenticated reconnect, and no raw-stream fallback. The C1 codec remains the sole framing implementation.

- Focused handshake tests: `dotnet test tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~IpcHandshakeTests` — PASS, 4/4.
- Focused Service and SessionAgent builds — PASS, 0 errors; full solution build — PASS, 0 errors (pre-existing warnings only).
- Native harness: `invalidated`; the one final attempt under `sha256:44a1a79446eda38cb4836e6191a422ef7362cddecf3b334a59cba5368fb3947e` stopped before publish/sign/launch because the generated ServiceHarness project had malformed XML. No runtime PASS is claimed and no second attempt was made.
- Follow-up diagnosis: `NamedPipeServer` uses `NamedPipeServerStream.MaxAllowedServerInstances`; the server handshake advertises the configured session ID instead of `0`. Identity now resolves the final image path from a retained query handle and WinTrust exposes raw `LONG` semantics. The one final harness attempt failed before any runtime probe.
- Settlement: 4.1–4.3 remain open. Signed harness acceptance and exact C2 coverage are absent. Result Contract, executed-test inventory, diagnosis, harness disposition, cleanup, and process evidence are merged into `evidence-manifest-unit-c2.md`; A/B/C1 status remains preserved.
- Historical evidence revision: `sha256:198c16fc423618a61b4494fbcb538a1fccfc2f3580456623aa95ac83709b0656` (superseded by the final correction revision below).
- Final authorized C2 validation: `invalidated`; signed loopback did not launch because preflight project parsing failed. C2 is definitively blocked under native token `sha256:44a1a79446eda38cb4836e6191a422ef7362cddecf3b334a59cba5368fb3947e`.
- Cleanup: proven — temporary directory absent, zero harness processes, and no certificate created.
- Exact changed-line and branch coverage: not proven; 4.3 remains blocked.
- Evidence manifest: `evidence-manifest-unit-c2.md`; native token is `sha256:46a20633d03cf4b51aebca9d1c813d2da1186c4ddaff557ca0c14c7efcd58b0e`.

## C2 continuation: signed harness preflight and run

- Native attempt binding: `sha256:f9d10b7828beaa7e7deec89b5dad4dcd53201f50e509d6bdda4fe82b9529516c`.
- Preflight: corrected disposable `ServiceHarness.csproj` and `AgentHarness.csproj`; restore and both builds passed with 0 errors. No certificate was created before these outcomes.
- Signed runtime: one and only one run began after CurrentUser-only certificate creation and disposable EXE signing. The loopback failed: Service timed out before receiving the authenticated agent message; Agent failed after 10 connection retries.
- Raw WinTrust: Service `-2146762751`; Agent `-2146762751`. Both `Get-AuthenticodeSignature` checks reported `Valid`, but the production WinTrust path did not accept the disposable self-signed certificate.
- Phase trace: `server.start-requested`; no authenticated-agent, client-authenticated, or message-dispatch phases were observed.
- Cleanup: temporary harness directory absent, zero harness processes, and the certificate thumbprint absent from CurrentUser `My`, `Root`, and `TrustedPublisher`. No PFX, LocalMachine store access, or production binary signing occurred.
- Result: `failed`; harness disposition `invalidated`. Tasks 4.1–4.3 remain open; signed loopback PASS, exact C2 changed-line coverage, and branch coverage remain absent.

## C2 diagnostic correction: WinTrust ABI and action GUID

- Work unit: `C2-wintrust-abi-correction`; native attempt binding retained as supplied: `sha256:4e1d86ec997cee153156bba32b4746ea17882d206cc5d696636bfc604a1fd3a1`.
- Corrected the Service and SessionAgent `WINTRUST_DATA` sequential layouts to the official native order, including `dwStateAction`, `hWVTStateData`, `pwszURLReference`, `dwProvFlags`, `dwUIContext`, and pointer-sized fields. The layouts are asserted for x86 (52 bytes) and x64 (88 bytes).
- Centralized the official Generic Verify V2 action GUID (`00AAC60B-0000-0000-C000-000000000046`) in each assembly and replaced the malformed Service `IntegrityChecker` and `ProtectedProcessReporter` values.
- Focused verification: Service `AuthenticatedTransportTests` PASS, 8/8; SessionAgent `WinTrustAbiTests` PASS, 3/3. Focused Service and SessionAgent builds PASS, 0 errors (pre-existing analyzer warnings only).
- Runtime harness: `N/A`/`invalidated` by authorization; no signed runtime, certificate, signing, or harness activity was performed in this correction.
- Exact changed-line coverage and runtime C2 acceptance remain unproven; tasks 4.1–4.3 remain open and C2 is not PASS.
- Changed-line budget: 105 authored additions+deletions for this correction, below the 200-line unit ceiling; historical C2 evidence and prior working-tree changes remain preserved.
- Rollback: revert only the WinTrust ABI/GUID production files, the two focused test files, and these appended evidence sections; preserve A/B/C1 and existing C2 transport seams.
- Documentation-only rerun: corrected the canonical Result Contract and preserved historical signed-loopback/preflight sections; no production/test code or verification activity was performed.
- Final evidence revision: `sha256:2c35c5531ee20db193c8fdb1c37003e8f0117904efa5196e7615940886b4954a` (same canonical manifest revision as `evidence-manifest-unit-c2.md`, computed after final bytes were stable with the self-referential revision line omitted).
- Correction objective: `passed`; C2 remains `blocked` because no signed runtime or exact coverage proof was authorized. Service `AuthenticatedTransportTests` 8/8 PASS, SessionAgent `WinTrustAbiTests` 3/3 PASS, and both focused builds 0 errors.
- Tasks 4.1–4.3 remain open; Unit D remains pending. Do not claim broad tests, runtime PASS, C2 completion, or verification readiness.

## C2 continuation: exact coverage measurement only

- Work unit `C2-exact-coverage-measurement`; native binding retained exactly as supplied: `sha256:ba476cb425757d4f64629455abbd453a23733c78687042ca6d95725d9c39c16d`. No native ledger action occurred.
- Baseline verified: `90a5a2a44299d99b67aff18da2a9182bc167fc75` (`90a5a2a`), the isolated C1 commit. Candidate was the uncommitted source snapshot `sha256:a378125e79b64b7cf234c8d9ec658e3378bf522363adf85f9bb7e6cdb59c69e5` over those exact ten C2 production paths.
- Focused XPlat/Cobertura commands passed: Domain `IpcHandshakeTests` 4/4; Service C2 transport/identity/launcher/security filters 31/31; SessionAgent ABI/host filters 8/8. No source or test edits were made.
- Exact changed-line intersection: 71/315 instrumentable lines hit, `22.54%`; 206/521 changed lines were non-instrumentable/excluded. Per-file detail and branch limitation are in `evidence-manifest-unit-c2.md`.
- Branch evidence: exact union unavailable because Cobertura duplicates generated async state-machine mappings; defensible source-line projection is approximately 13/118 conditions, `11.02%`, not an exact branch union.
- Aggregate project reports are separate and not the gate: Domain 25/7,789 lines (0.32%), Service 513/21,125 (2.42%), SessionAgent 5/8,817 (0.05%).
- Outcome: `passed` for measurement production, threshold `>80%` not met; `harness_disposition=invalidated`. No runtime PASS or C2 completion is claimed; tasks 4.1–4.3 and Unit D remain open.
- Cleanup/process evidence: temporary coverage results were removed after measurement; no certificate, signing, signtool, harness, named-pipe loopback, or certificate-store activity occurred; no harness process remained.
- Canonical evidence revision: `sha256:1b4bdeb433c4b46419b48f866c6c522ff050c5f5b89e429d1eb016eecf7d111b`.

## C2 continuation: bounded focused coverage tests

- Work unit `C2-targeted-coverage-tests`; native token retained exactly as supplied: `sha256:c744d9f7d7884a04c2c3c827f3c89636b462a5c5623d2d79bc04088bcc627fdf`. The native ledger was not acquired, settled, reset, or mutated.
- Added only focused tests in the existing C2 test projects. The tests exercise malformed/invalid handshakes, blank signer rejection, validator-exception fail-closed behavior, send-before-start ownership rejection, disconnected client no-fallback behavior, unavailable process identity, and missing/unsigned signer paths.
- Focused tests passed: Domain `IpcHandshakeTests` 9/9; Service C2 transport/identity/launcher/security filters 33/33; SessionAgent `WinTrustAbiTests|SessionAgentHostTests` 14/14.
- Focused builds passed: Service test project 0 errors (5 pre-existing package warnings); SessionAgent test project 0 errors/0 warnings.
- Exact changed-line measurement against verified baseline `90a5a2a44299d99b67aff18da2a9182bc167fc75`: 521 changed production additions, 315 instrumentable, 74 hit, `74/315 = 23.49%`; threshold `>80%` remains unmet.
- Per-file exact intersection: `IpcHandshake` 25/32, `IpcPhaseTrace` 12/12, `AgentLauncher` 3/3, `IntegrityChecker` 0/1, Service `AuthenticodeSigner` 22/54, `NamedPipeServer` 3/77, `WinTrust` 8/8, `ProtectedProcessReporter` 0/3, SessionAgent `AuthenticodeSigner` 22/64, `NamedPipeClient` 4/61.
- Branch evidence remains non-exact: Cobertura reports aggregate Domain 16/1,024 (`1.56%`), Service 102/3,622 (`2.81%`), and SessionAgent 10/1,332 (`0.75%`) branches. Exact changed-branch union is unavailable because async state-machine mappings duplicate source lines; no branch PASS is claimed.
- Runtime harness: `N/A`/`invalidated` by authorization and absolute exclusions. No signed runtime, certificate, signing, signtool, named-pipe loopback, or native harness activity occurred.
- Cleanup/process evidence: temporary coverage directories were removed; no certificate/store mutation, signed artifact, harness process, or native-ledger activity occurred.
- Result: `passed` for bounded test work and valid exact measurement production; C2 coverage gate failed. Tasks 4.1–4.3 remain open, C2 remains blocked, and Unit D remains pending.
- Rollback boundary: revert only the three focused test-file additions and this continuation evidence; preserve all existing production, A/B/C1, historical C2, and unrelated working-tree changes.
- Historical canonical manifest revision for this latest focused-test/coverage section: `sha256:8c8b7b904992a41b10ede6ac02f1c767278667915c53e8af7ebbc83391912bfc`. The earlier `sha256:2c35c5531ee20db193c8fdb1c37003e8f0117904efa5196e7615940886b4954a` remains historical and is not rewritten.

## C2-minimal-testability-refactor continuation

- Work unit `C2-minimal-testability-refactor`; native token retained exactly as supplied: `sha256:ef3c5c043bd3b9b691deb190d8d8d22f78ec0c40255534dba0e571507ba3caee`. The native ledger was not acquired, settled, reset, or mutated.
- Added internal-only pipe connection seams and injectable connection/auth/signer delegates to the existing `NamedPipeServer` listener and `NamedPipeClient`. Public constructors and default native behavior remain unchanged; no raw-stream fallback or security decision was weakened.
- Added focused seam tests for server authentication-before-hello/dispatch ordering and client authenticated handshake write behavior. Focused Service tests passed 11/11; focused SessionAgent seam test passed 1/1; coverage runs passed Domain 9/9, Service 30/30, and SessionAgent 15/15.
- Focused builds passed with 0 errors for Service and SessionAgent test projects. Warnings are existing analyzer/package noise plus seam-test style warnings.
- Raw Cobertura aggregate results from the remeasurement were Domain 29/7,789 lines and 16/1,024 branches; Service 660/21,150 lines and 129/3,630 branches; SessionAgent 167/8,837 lines and 45/1,338 branches. These are aggregate-vs-exact figures and are not the C2 changed-line gate.
- Exact changed-line intersection against baseline `90a5a2a44299d99b67aff18da2a9182bc167fc75` was not defensibly recomputed from the available Cobertura artifacts in this bounded continuation; the strict `>80%` gate is therefore not claimed. Exact changed-branch union remains unavailable because Cobertura duplicates async state-machine mappings.
- Runtime harness: `N/A`/`invalidated` by the absolute exclusions. No signed runtime, certificate, signing, signtool, named-pipe loopback, or native harness activity occurred.
- Cleanup/process evidence: only focused test/build processes ran; temporary coverage directories were removed; no certificate/store mutation, harness process, or native-ledger activity occurred.
- Final evidence revision: `sha256:98d8bce95bc60af32ceba277eb50267e0556683dc420403e614b7f1fdaf57a0a` (canonical manifest hash with its self-referential revision line omitted).
- Result Contract: `status=blocked; harness_disposition=invalidated; next_recommended=none`; tasks 4.1–4.3 remain open and Unit D remains pending.
- Rollback boundary: revert only the injected internal pipe seams, adapter types, `AuthenticatedTransportTests` seam test, `NamedPipeClientSeamTests`, and this continuation evidence; preserve historical C2, A/B/C1, and unrelated working-tree changes.

## C2 continuation: post-seam exact coverage measurement

- Work unit `C2-post-seam-exact-coverage`; native token retained exactly as supplied: `sha256:57105482cc3909821964dbfb9dc8366de9ba8549c79ab4b55c1c568b3b03ea3a`; the native ledger was not mutated.
- Baseline verified as isolated C1 boundary `90a5a2a44299d99b67aff18da2a9182bc167fc75`; candidate production-scope identity before measurement: `sha256:55f001dae27ae4ec1f809ce7d3ecc5c17d6fba8eb311176ec5a3266e9a01275d`.
- Fresh focused Cobertura suites passed: Domain `IpcHandshakeTests` 9/9, Service authenticated transport/testability filters 34/34, SessionAgent WinTrust/client-seam filters 15/15. Retained Cobertura artifacts were intersected after all three runs.
- Exact changed-line union: 192/383 instrumentable lines hit (`50.13%`); 254/637 changed production additions had no Cobertura line mapping and were excluded without treating missing mappings as covered. The strict `>80%` gate failed.
- Per-file detail and branch limitation are in `evidence-manifest-unit-c2.md`. Aggregate reports are Domain `29/7,789` lines and `16/1,024` branches; Service `680/21,150` and `138/3,630`; SessionAgent `167/8,837` and `45/1,338`.
- Result: measurement `passed`; C2 remains blocked, tasks 4.1–4.3 remain open, and no signed runtime acceptance is claimed. Harness disposition is `invalidated` by the absolute exclusions.
- Cleanup/process evidence: temporary Cobertura outputs were removed only after intersection; no certificate/store/signing/signtool/harness/loopback/native-ledger activity occurred and no harness process remained.
- Changed paths: this file and `evidence-manifest-unit-c2.md` only; rollback is limited to these documentary additions.

## C2 task 4.1 classification

- Work unit: `C2-scope-classification`; no native token was acquired or mutated.
- Baseline: verified commit `90a5a2a44299d99b67aff18da2a9182bc167fc75`, tree `c33bd1e53d46abbb95d33f345e6e135790dfeda3`.
- Candidate snapshot: full working-tree hash `sha256:04dea78271d9dd88db51f13dbb823cb6ce9b1a5ae0897a711dfdd5a5d13ce5c5`; C2 production-scope hash `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`.
- Exact classification: 481 `managed-deterministic` + 156 `native-boundary` = 637 changed C2 production additions. Mixed files were split by path and current line range; no blanket file exclusion was used. The dedicated manifest contains every range and native reason.
- Historical `192/383 = 50.13%` is not the scoped managed gate. Branch normalization is documented for the later gate; no coverage or runtime PASS is claimed here.
- Status: 4.1 complete; 4.2 and 4.3 remain open; 4.4 and Unit D remain blocked.
- Historical/superseded classification revision: `sha256:e344f980b0fddc92cbbbade3fc633d62b01f2a30632bba4dee0118b805a3f624`.
- Rollback: remove only this documentary section, the dedicated scope manifest, and the task checkbox/evidence pointer; preserve all production/test paths and prior evidence.

## C2 task 4.2 executable-managed gate continuation

- Work unit `C2-executable-managed-gate`; native token retained exactly `sha256:33f313406b5a1f35702940ddcb597d22359afe98de5c9bb18cb9a82ca8a4d282`; native attempt ledger was not acquired or mutated.
- Fresh focused coverage runs passed Domain 9/9, Service C2 filter 49/49, and SessionAgent C2 filter 17/17; focused Service and SessionAgent builds completed with 0 errors.
- No production seam was added. Existing internal pipe/authentication seams were used; public APIs and default runtime behavior remain unchanged. The bounded test additions are approximately 195 authored lines, below the 400-line work-unit limit.
- Exact executable-managed gate result: 213/288 = 73.958333% is the sole authoritative gate; strict gate FAILED and 18 more hits are needed to reach 231/288 (`>80%`), so 4.2 remains FAIL/open. Source/global context is 213/481 = 44.282744% and is never authoritative. The denominator contains 193 independently proven compiler/PDB gaps, and 75 mapped executable lines remain unhit. Service focused result: 49/49.
- Per-file source/global context: `IpcHandshake` 29/55, `IpcPhaseTrace` 12/41, `AgentLauncher` 3/3, Service `AuthenticodeSigner` 29/72, Service `NamedPipeServer` 66/138, Service `WinTrust` 0/1, SessionAgent `AuthenticodeSigner` 18/61, SessionAgent `NamedPipeClient` 51/110. The authoritative executable denominator is 288; 481 is source/global context only.
- Branch evidence is bounded aggregate only: Domain 16/1,024, Service 138/3,630, SessionAgent 45/1,338; async duplicate mappings prevent exact changed-branch union and no branch PASS is claimed.
- Harness disposition `invalidated` by absolute exclusions. No certificate/store mutation, signing/signtool, harness, real named-pipe loopback, native ledger action, or Unit D activity occurred. Temporary Cobertura outputs were removed after intersection; no harness process remained.
- Outcome: `blocked`; strict gate FAILED with 18 more hits needed. Tasks 4.2 and 4.2.1–4.2.3 remain unchecked. 4.3 is the only next task but remains blocked; 4.4 and Unit D remain blocked. No production attribution or native activity occurred.
- Current canonical evidence revision: `sha256:9bd629515fadf6a7a9548dfe90efe23faabd830898bd50c7d066953cd77bdeaf`.

## Historical C2 task 4.2 final executable-table measurement-only continuation (superseded)

- Work unit `C2-final-executable-table`; native binding retained exactly `sha256:c8278810c51508d6a20a75e1a4058070807d804efc4eabee65fdeaccfc5e41d1`; native attempt ledger was not acquired or mutated.
- Candidate identity check: current ten-file C2 production snapshot is `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`, matching the frozen production candidate. Current four-file focused-test snapshot is `sha256:555c2c68d9eb90519d6ff20f83b910ca0fa3e5798a69a4ea2ff5b62b5b293f77`; the prior 240/288 result is historical and unreproducible, did not retain a comparable test snapshot, and fresh coverage did not reproduce 240 hits. No candidate identities were mixed.
- Fresh focused coverage runs passed Domain `9/9`, Service `54/54`, and SessionAgent `20/20`; the build-producing run completed with 0 errors for Service and SessionAgent (pre-existing package warnings only).
- Exact executable-managed reconciliation: `213/288 = 73.958333%`; strict `>80%` FAIL. Source/global context is `213/481 = 44.282744%` only and is never authoritative. The denominator is exactly 288 and the fresh hit total is exactly 213; 75 mapped executable lines remain unhit and 193 managed lines remain non-executable by path+line proof.
- The current table and exclusion ranges are retained in `evidence-manifest-unit-c2.md`; native-boundary ranges remain retained in `evidence-manifest-unit-c2-scope.md`.
- Branch evidence is one bounded aggregate set only: Domain `16/1,024` (1.56%), Service `171/3,630` (4.71%), SessionAgent `48/1,338` (3.59%). Exact changed-branch union availability: unavailable because Cobertura duplicates async state-machine mappings; these aggregate values do not establish a changed-branch gate.
- Harness disposition `invalidated` by absolute exclusions. No certificate/store mutation, signing/signtool, signed or unsigned harness, real named-pipe loopback, native ledger action, or Unit D activity occurred. Coverage outputs were retained through reconciliation and cleanup occurred only afterward; no harness process remained.
- Task 4.2 and 4.2.1–4.2.3 remain open because the fresh strict gate failed. Task 4.3 remains next/open but blocked by the excluded native signed proof; 4.4 and Unit D remain blocked.
- Changed paths attributed to this continuation are the four C2 evidence/progress artifacts only; no production or test files were edited.
- Current canonical evidence revision: `sha256:3733a52fc3f673f8ebd24d4011cd66a763cf617a199da0d41e304a47abb2f331`.

## C2 managed coverage correction (current)

- Work unit `C2-managed-coverage-correction`; native binding retained exactly `sha256:662caee5cd82b9181fd2b05eacbefc0f44749a7896d6274ec59a61ceda9a3de9`; native ledger was not called or mutated.
- Added tests only in existing C2 files: malformed handshake JSON value types; service signer raw-trust and process identity metadata; SID/security helper rejection; authenticated `UsageStateResponse` dispatch; client untrusted local signer and wrong server process identity.
- Changed-line settlement: 103 authored test additions from the acquired candidate; under the 120-line correction ceiling. No production files, seams, refactors, packages, or dependencies changed.
- Fresh focused Cobertura suites passed Domain `11/11`, Service `58/58`, and SessionAgent `23/23`; focused Service and SessionAgent builds passed with 0 errors (existing warnings only).
- Exact executable-managed reconciliation remains denominator `288`: Domain `IpcHandshake` `32/32`, `IpcPhaseTrace` `12/12`; Service `AgentLauncher` `3/3`, `AuthenticodeSigner` `33/38`, `NamedPipeServer` `71/97`, `WinTrust` `0/0`; SessionAgent `AuthenticodeSigner` `28/31`, `NamedPipeClient` `53/75`. Total `232/288 = 80.555556%`; strict `>80%` PASS.
- The retained managed source/global totals remain `481`, with `193` path+line non-executable exclusions, `232` hits, and `249` source/global unmapped/unhit context. The authoritative denominator is unchanged at `288`; no `240/288` claim was reused.
- Branch evidence remains bounded aggregate only: Domain `16/1,024`, Service `171/3,630`, SessionAgent `48/1,338`; exact changed-branch union remains unavailable because Cobertura duplicates async state-machine mappings.
- Candidate identity: production `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`; focused tests `sha256:6402caea1b7fcee2e134e005311165bef34c7a3325e7e5a7334e28e60fe3f74c`; baseline `90a5a2a44299d99b67aff18da2a9182bc167fc75`.
- Harness disposition `invalidated`/N/A by absolute exclusions. No certificate/store mutation, signing, signtool, harness, real named-pipe loopback, native ledger, Unit D, commit, push, PR, or review activity occurred. Temporary coverage outputs were removed after reconciliation; no harness process remained.
- Tasks 4.2 and 4.2.1–4.2.3 are complete. Task 4.3 remains next/open and blocked by native proof; 4.4 and Unit D remain blocked. Verification/archive remain outside this unit.
- Current canonical evidence revision: `sha256:da1a800c0ff3891b0aa763e0d98b4fdd6b04d51b2afbb72b13caa56a96706f31`.

## C2-signed-native-acceptance settlement

- Work unit `C2-signed-native-acceptance`; supplied binding retained exactly: `sha256:1fbe9d17fcda65ccb0384ac7af42b412e8365e13c168b3e5bd440c2cb9059aca`; the native ledger was not called or mutated.
- Final disposable preflight passed before certificate creation: after an initial disposable compile failure caused by missing harness-only implicit usings, corrected ServiceHarness and AgentHarness restore, build, and publish completed with 0 errors under `C:\Users\Usuario\AppData\Local\Temp\opencode\c2-signed-native-acceptance`; no certificate existed during the correction.
- One disposable CurrentUser-only code-signing certificate was created without private-key export/PFX or LocalMachine access. Temporary trust was added only to CurrentUser `My`, `Root`, and `TrustedPublisher`; both EXEs were signed and independently verified by SignTool exit 0 and PowerShell `Valid`.
- Exactly one signed loopback ran. Service path/PID/SID/session: `...\publish\service\ServiceHarness.exe` / `17804` / `S-1-5-21-3623415481-2756580284-2510936249-1001` / `1`. Agent: `...\publish\agent\AgentHarness.exe` / `26244` / same SID / `1`.
- Observed result: Service timed out with `TaskCanceledException`; Agent exhausted its built-in ten connection attempts with `InvalidOperationException`. No authenticated-agent, client-authenticated, or dispatch phase was observed; both processes exited failure and were terminated.
- Raw WinTrust return was not emitted by either harness and is `N/A` for this attempt; no diagnostic rerun was made. Separate connect/auth/response runtime deadlines and production phase trace were therefore not proven.
- Cleanup passed: temporary directory absent; zero ServiceHarness/AgentHarness processes; tracked certificate absent from CurrentUser `My`, `Root`, and `TrustedPublisher`. No production/test/package/binary file changed; exactly the four evidence docs changed. Authored documentation delta remained below 120 lines.
 - Result: `failed`; task 4.3 remains open, task 4.4 and Unit D remain blocked. Preserve task 4.2 evidence at `232/288 = 80.555556%`; verification/archive remain blocked.

## C2-listener-lifecycle-correction

- Implemented owned, non-blocking listener startup in `AgentLauncher`: the listener task is retained, terminal faults are observed, completed faults fail launch, and cancellation/process-launch failure/session disposal stop and dispose the channel.
- The confirmed defect is bounded to `LaunchAgentAsync` awaiting the listener's accept loop before `CreateProcessAsUserCore`; the prior disposable harness launched Agent independently and is not claimed as proof of this root cause.
- Focused Service seam tests passed `10/10`; fresh C2 coverage runs passed Domain `11/11`, Service `61/61`, and SessionAgent `23/23`; Service test build passed with `0` errors.
- Coverage disposition: the prior `232/288` gate is invalidated because production executable lines changed. Exact C2 path+line denominator/hit reconciliation is required; task 4.2 and 4.2.2–4.2.3 are open again. No coverage PASS is claimed.
- Runtime harness: `N/A`/`invalidated` by absolute exclusions; no certificate, signing, signtool, named-pipe loopback, native probe, or ledger action occurred. Temporary coverage outputs were removed and no harness process remained.
- Rollback: revert only `src/ControlParental.Service/AgentLauncher.cs`, `tests/ControlParental.Service.Tests/AgentLauncherLaunchSeamTests.cs`, and this C2 correction evidence; preserve historical signed-run facts and all A/B/C1/D paths.
- Current canonical evidence revision: `sha256:676fe314fd2af9be1addd9e79f5851dfb88c10dbfc88cf21410dc7dd55727ff0`.

## C2 listener lifecycle corrective rerun (current)

- Work unit `C2-listener-lifecycle-correction-rerun`; native binding retained exactly `sha256:31b4c613ff0f0aeb5ca40ff677d350a190fe64e5491e929f0e07f6b3cbd3663a`; ledger was not called or mutated.
- Removed only unintended `currentSessionId` propagation into the default `NamedPipeServer` factory and its assignment. The pre-correction session-binding contract is preserved exactly: `AgentLauncher` uses the original three-argument constructor, retaining the server's existing default session behavior; no PID/SID/session authentication logic changed.
- Added a late-fault seam test: listener starts active, process creation is entered, then the listener task faults; `LaunchAgentAsync` observes and propagates that terminal fault. Fake stop/dispose are observable, and assertions prove `StopCalled`, `DisposeCalled`, completed listener task, and `IsConnected == false`.
- Focused launcher Service tests: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --filter "FullyQualifiedName~AgentLauncher" --verbosity minimal` — PASS, 14/14. Service test build — PASS, 0 errors (pre-existing warnings only). No additional suites or coverage run.
- Corrective code/test delta: 45 authored additions+deletions; below the 120-line ceiling. No production refactor, authentication/cert/store/signing/harness/loopback/package change occurred.
- Prior `232/288` coverage is invalidated and no new coverage was measured. Task 4.2 and 4.2.2–4.2.3 remain open; 4.3 remains open; 4.4 and Unit D remain blocked. Historical signed-run facts remain unchanged.
- Canonical revision: `sha256:83369bd59957272e8f26b5a6ac45fd64582a7a758f7759e7527dffe18bc1d4c7`, propagated after final bytes to this artifact, `tasks.md`, `evidence-manifest-unit-c2.md`, and `evidence-manifest-unit-c2-scope.md`.
- Rollback: revert only `AgentLauncher.cs`, `AgentLauncherLaunchSeamTests.cs`, and this corrective documentation; preserve historical C2 evidence and A/B/C1/D paths.

## C2-post-return-listener-cleanup (current apply)

- Work unit `C2-post-return-listener-cleanup`; native binding retained exactly `sha256:3677ebac82a97d37fa81739f97efcb8f4401644143bf50b9055fac58d0fce07a`; the ledger was not called or mutated.
- When the owned listener faults after `LaunchAgentAsync` returns, the fault is logged and cleanup atomically detaches the expected channel/task/CTS under `ipcSync`, cancels the listener, awaits stop without awaiting the faulted listener task itself, then disposes the channel and CTS. Explicit disposal and launch-failure cleanup use the same ownership detachment and cannot double-dispose or reclaim a replacement channel.
- Added one deterministic post-return seam test: successful process creation and launch return occur while the listener remains connected; the test then faults the listener and awaits a disposal completion source before asserting stop, disposal, completed task, disconnected state, and cleared launcher channel. The existing immediate-fault, launch-failure, cancellation, and disposal tests remain unchanged.
- Focused launcher Service tests: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --filter "FullyQualifiedName~AgentLauncher" --verbosity minimal` — PASS, 15/15. Service test build — PASS, 0 errors (pre-existing warnings only). No coverage measurement.
- Prior `232/288` coverage remains invalidated; task 4.2 and 4.2.2–4.2.3 remain open, task 4.3 remains open/blocked, and 4.4/Unit D remain blocked. No native runtime, certificate/store/signing, harness, loopback, package, ledger, commit, push, PR, or review activity occurred.
- Canonical revision: `sha256:e0b8a7e55c4cdec31dc355e067c5d9f29a417d7e0231d6a263a14c4b0968cb7b`, propagated after final evidence bytes to all four C2 evidence artifacts.
- Rollback: revert only `AgentLauncher.cs`, `AgentLauncherLaunchSeamTests.cs`, and the current C2 evidence additions; preserve historical signed-run facts and all A/B/C1/D paths.

## C2-atomic-listener-ownership (current apply)

- Replaced parallel IPC fields with immutable `IpcOwnership(CTS, channel, start task)` and atomic identity detachment under `ipcSync`; retained the original three-argument NamedPipeServer construction.
- Added deterministic TCS-gated tests for Dispose during StartAsync, Dispose during process creation, and fault-versus-Dispose cleanup claimant races; launcher filter passed 18/18 and Service test build passed with 0 errors.
- Coverage was not run. Prior 232/288 remains invalidated; task 4.2 and 4.2.2–4.2.3 remain open, 4.3 open/blocked, and 4.4/Unit D blocked.
- Native token retained exactly `sha256:dc4053370ccc75890cc37cafc5468c510d88e6e7b5b8641fe42e0a98d96e66c7`; ledger was not called or mutated. Current evidence revision: `sha256:a565373ff7def34336fe6ab107a5a4b7c96ce70ab23fb39626b3e7f9095de8dd`.

## C2-provisional-ownership-cleanup (current apply)

- Native token retained exactly `sha256:74e54f6342f68835bf00991f9d21f20a52d5ec0f7594952c9e1a3671c0c3b292`; ledger was not called or mutated.
- Provisional CTS/channel/event/start resources are cleaned independently before publication; synchronous StartAsync failure preserves the original exception and cannot leak a created channel or CTS.
- The FakeIpcChannel StartAsync now signals entry and synchronously gates before returning its Task. Dispose then rejects publication; the test proves no process, canceled token, null AgentChannel, and exactly one stop/dispose.
- Focused launcher tests `19/19` PASS; Service test build PASS with `0` errors. Coverage not run; prior `232/288` remains invalidated. Tasks 4.2/4.2.2–4.2.3 open, 4.3 open/blocked, 4.4/Unit D blocked.
- Current canonical evidence revision: `sha256:46504dbdc50a45b6bd6ef43ced3a9242fe0b097449de31b0b82d01f53f610a2b`. Runtime harness and native ledger were not used. Rollback is limited to AgentLauncher, its seam tests, and this four-document evidence update.

## C2-post-ownership-exact-coverage (current apply)

- Measurement-only unit; native token `sha256:b1ce3de5bae138bbc172477ff5e2178718aaf4598c3199d8591eb211b8a47adc` was retained and no ledger was called. Frozen baseline is `90a5a2a44299d99b67aff18da2a9182bc167fc75`; production candidate is `sha256:af7340bd6af3babef79c5df644c787b82f9ccf28e124b0b5f37f2734fc24bce7`; focused-test candidate is `sha256:edbe0e4b7b4fb2c766ffd522b71b607e2ad95eafeca4a352f906e0067e3d8db8`.
- Fresh Cobertura suites passed Domain `11/11`, Service `66/66`, and SessionAgent `23/23`; focused Service and SessionAgent builds passed with `0` errors (5 existing Service warnings; SessionAgent 0 warnings).
- Recomputed current managed classification is `801` source-context lines, `260` justified syntax/compiler non-executable exclusions, `541` executable denominator, and `421` executable hits. Strict `421/541 = 77.818854%` `>80%`: **FAIL**. The historical `232/288` result is invalidated and not current evidence.
- Exact arithmetic correction: strict `>80%` of `541` requires `433` hits because `433/541 = 80.036969%`; the current `421` hits therefore need `12` additional hits. Any historical `13`-hit statement from the already-settled native diagnosis is an arithmetic error in that historical ledger entry and is not rewritten here.
- Exact per-file table, exclusion ranges, branch aggregates, cleanup, and the sole CURRENT Result Contract are in `evidence-manifest-unit-c2.md`; native ranges remain in `evidence-manifest-unit-c2-scope.md`. Exact changed-branch union remains unavailable because Cobertura duplicates async state-machine mappings.
- Harness is `invalidated`/N/A by scope: no certificate, signing, signtool, native loopback, harness, or native ledger activity. Coverage was retained through reconciliation and canonical evidence capture, then removed; no harness process remained.
- Result: `blocked`; 4.2 and 4.2.2–4.2.3 remain open, 4.3 remains next/open but blocked, and 4.4/Unit D remain blocked. Changed paths are only the four evidence artifacts; rollback is documentary only.
- Canonical evidence revision: `sha256:457be3deebac2e1469727280f060fed68d85da13aea077d7ad0ad2f7774b446b`.

## C2-final-focused-tests (current apply)

- Work unit bounded to focused behavior-value tests in the existing `AgentLauncherLaunchSeamTests.cs`; no production edits, refactors, seams, packages, native activity, harness, or new files.
- Added six tests covering token duplication failure, absent environment block, process API exception, listener stop/dispose exceptions, idempotent disposal, and launch after disposal. Authored delta is 97 changed lines (95 additions, 2 deletions); 12 mapped executable lines were newly hit.
- Fresh Cobertura suites passed Domain `11/11`, Service `72/72`, and SessionAgent `23/23`; focused Service build passed with `0` errors and 5 existing package warnings.
- Exact table: `801` source-context, `260` non-executable, `541` executable denominator, `433` hits, `108` mapped unhit; `433/541 = 80.036969%`, strict PASS. Tasks 4.2 and 4.2.2–4.2.3 are complete.
- Task 4.3 remains next/open but blocked; 4.4 and Unit D remain blocked. Runtime harness is N/A/invalidated. Native token `sha256:76661139900ac1c30c5c3635331c76aa813c473966575ec3a1b2726e1485a123` was retained; ledger was not called.
- Raw coverage was retained through reconciliation and canonical evidence capture, then removed. Current canonical revision is propagated to all four C2 evidence documents.
- Current canonical evidence revision: `sha256:cfa3db6bf8760b2bd9baf7a8d05f6572a0eccba8d34837fcd7d124dab4b1aa55`.

## C2-signed-native-acceptance rerun (current apply)

- Corrected the disposable signed harness invocation to pass only the pipe name to `C2.Server.exe` / `C2.Client.exe`, then re-published and re-signed both artifacts with the existing CurrentUser `ControlParentalDev` certificate.
- Exactly one signed loopback rerun passed end-to-end: the server authenticated, received exactly one `ForegroundChanged`, sent one `Ping`, and exited cleanly after cancellation; the client authenticated, sent one `ForegroundChanged`, received one `Ping`, and exited cleanly after cancellation.
- Observed trace phases: server `Connected,ServerPid,ProcessHandle,Path,AuthResult,ClientHelloRead,ServerHelloWrite`; client `Connected,ServerPid,ProcessHandle,Path,AuthResult,ClientHelloWrite,ServerHelloRead,Authenticated`.
- `Work Unit C` is now complete and `Work Unit D` is unblocked/pending. No repository source code changed in this continuation; only the disposable temp harness was rerun.
