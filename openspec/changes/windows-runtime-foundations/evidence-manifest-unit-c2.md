# Evidence Manifest — Work Unit C2

## Scope

- `src/ControlParental.Domain/IpcHandshake.cs`
- `src/ControlParental.Domain/IpcPhaseTrace.cs`
- `src/ControlParental.Service/Interop/NamedPipeServer.cs`
- `src/ControlParental.Service/Interop/WinTrust.cs`
- `src/ControlParental.Service/Interop/AuthenticodeSigner.cs`
- `src/ControlParental.Service/AgentLauncher.cs`
- `src/ControlParental.SessionAgent/Interop/NamedPipeClient.cs`
- `src/ControlParental.SessionAgent/Interop/AuthenticodeSigner.cs`
- `tests/ControlParental.Domain.Tests/IpcHandshakeTests.cs`
- `tests/ControlParental.Service.Tests/AuthenticatedTransportTests.cs`
- `src/ControlParental.Service/IntegrityChecker.cs`
- `src/ControlParental.Service/ProtectedProcessReporter.cs`
- `tests/ControlParental.SessionAgent.Tests/WinTrustAbiTests.cs`

## Native token

- `sha256:44a1a79446eda38cb4836e6191a422ef7362cddecf3b334a59cba5368fb3947e`

## RED / GREEN evidence

- RED first: the new process-handle, raw-WinTrust, and phase-trace tests were authored before the corresponding implementation; the first focused compile failed on the missing symbols as expected.
- GREEN: `AuthenticatedTransportTests` — PASS, 5/5.
- GREEN: `IpcHandshakeTests` — PASS, 4/4.
- Focused Service and SessionAgent builds — PASS, 0 errors.
- Full solution tests — PASS, exit 0; full solution build — PASS, 0 errors; warnings remain pre-existing/noise.

## Historical native harness disposition

- `invalidated`: the single authorized final attempt stopped during temporary harness project parsing before publish, signing, verification, or launch.
- Failure: generated `ServiceHarness.csproj` had a mismatched `TargetFramework` XML closing tag.
- No second harness attempt was made. This is a definitive blocked result under the supplied native token.

## Cleanup evidence

- No certificate was created because preflight failed before certificate creation.
- Temporary harness directory was removed; `ServiceHarness`, `AgentHarness`, and `Harness` process count is 0.
- No CurrentUser certificate-store mutation, machine-level store mutation, repository binary mutation, or production binary signing occurred.

## Work-unit evidence

| Evidence | Result |
|---|---|
| Focused tests | PASS — Service 5/5; Domain 4/4; SessionAgent suite 80/80 |
| Runtime harness | BLOCKED — one final preflight attempt invalidated; no signed execution |
| Coverage | BLOCKED — aggregate focused report 0.37% line / 0.13% branch; exact native changed-line gate not proven |
| Rollback boundary | Revert C2 identity/transport/trace production paths and focused tests; preserve A/B/C1 and UI pipe behavior |

## Status

- 4.1–4.3 remain pending. Signed ACL-backed loopback, exact native changed-line coverage above 80%, and branch evidence are absent.

## Historical Result Contract (superseded)

- Historical revision: `sha256:953ad485da2f97278fd93738828bdda88816ffe69e6c1c90438602f29359bad7` (superseded by the bounded ABI/GUID correction below).
- Historical status: `failed`.
- Historical executive summary: C2 preflight passed after correcting the disposable project XML, but the sole signed loopback failed with raw WinTrust `-2146762751` for both disposable harness images before authenticated dispatch.
- `artifacts`: this manifest, `apply-progress.md`, and `tasks.md`; A/B/C1 evidence and the prior invalidated attempt are preserved.
- `next_recommended`: `none` for this one-attempt apply; remain blocked and do not mark 4.1–4.3 complete.
- `risks`: signed runtime acceptance, exact native coverage, separate runtime deadlines, and authenticated dispatch remain unverified.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); no delegation, commit, push, PR, backend, D, framework, or review action.

## Historical executed verification inventory

- `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthenticatedTransportTests" --verbosity minimal` — PASS, 5/5.
- `dotnet test tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~IpcHandshakeTests` — PASS, 4/4.
- `dotnet test tests/ControlParental.SessionAgent.Tests/ControlParental.SessionAgent.Tests.csproj --no-restore --verbosity minimal` — PASS, 80/80.
- `dotnet test ControlParental.sln --no-restore --verbosity minimal` — PASS, exit 0.
- `dotnet build ControlParental.sln --no-restore --verbosity minimal` — PASS, 0 errors.
- Coverage: focused Service report — aggregate 0.37% line / 0.13% branch; `IpcPhaseTrace` 100% line/branch, `WinTrustFileInfo` 91.48% line/75% branch; exact C2 native scope not claimed.
- Temporary signed harness preflight — BLOCKED before build/publish due malformed generated XML.

## Historical diagnosis

- `MaxNativePipeInstances`: invalid `255` was replaced with `NamedPipeServerStream.MaxAllowedServerInstances`.
- Server handshake: session claim uses configured `sessionId`, not literal `0`.
- Identity: `PROCESS_QUERY_LIMITED_INFORMATION` plus `QueryFullProcessImageName` resolves the final image path; the SafeHandle remains alive through path/session/start-time/signer checks.
- WinTrust: wrapper retains raw `LONG`; success is exactly `== 0`; final harness raw code is `N/A` because preflight failed before launch.
- Deadline trace: phase/cancellation observability is unit-tested; independent runtime deadline execution was not reached.

## Historical harness disposition

- Signed harness: `invalidated`; no signed launch occurred because temporary harness XML parsing failed.
- Unsigned harness: not run in this attempt.
- Required signed-valid success, rejection paths, session/PID binding, concurrent logical sessions, reconnect, and C1 framing through C2 were not proven by a successful signed run.

## Historical process evidence

- No harness process was launched; cleanup check found zero `ServiceHarness`, `AgentHarness`, or `Harness` processes.
- Focused tests, broad solution tests, coverage, and full solution build ran after preflight failure; no signed runtime result is inferred.

## Historical six-step phase trace summary

1. **Real PID path** — unit PASS via handle-backed final-image resolution; signed phase blocked at harness XML parsing.
2. **Retained handle** — unit/build PASS for deterministic `SafeProcessHandle` ownership; no signed runtime evidence.
3. **Raw WinTrust** — unit PASS for raw result/zero semantics; raw code `N/A` in harness.
4. **Separate deadlines** — phase/cancellation trace unit PASS; runtime deadline phases not exercised.
5. **Fixed ordering** — handshake tests/source PASS; no signed dispatch/rejection ordering evidence.
6. **Signed harness** — invalidated before publish/sign/verification/launch; cleanup complete.

## Historical final validation disposition

- C2 is definitively `blocked` under native token `sha256:44a1a79446eda38cb4836e6191a422ef7362cddecf3b334a59cba5368fb3947e`.
- 4.1–4.3 are not complete. No success is inferred from unit tests, broad tests, build, or coverage.

## Historical continuation settlement evidence

- Attempt binding: `sha256:f9d10b7828beaa7e7deec89b5dad4dcd53201f50e509d6bdda4fe82b9529516c`.
- Outcome: `failed`; changed lines attributable to this continuation: 25 documentation/evidence lines, below the 100-line ceiling.
- Exact preflight outcomes: ServiceHarness restore 0, AgentHarness restore 0, ServiceHarness build 0 errors, AgentHarness build 0 errors. The corrected `</TargetFramework>` XML was validated before certificate creation.
- Certificate creation: yes, CurrentUser `My` only initially; certificate was also placed in CurrentUser `Root` and `TrustedPublisher` for the disposable trust check. No private-key export or PFX was used.
- Signed runtime run count: `1` (the sole authorized run). Signed loopback: `FAIL`.
- Raw WinTrust: Service `-2146762751`; Agent `-2146762751`. PowerShell signature status was `Valid` for both disposable EXEs.
- Phase trace: `server.start-requested`; no authenticated-agent, client-authenticated, or dispatch phase was observed. Service result was `FAIL|TimeoutException|Service did not receive the authenticated agent message.` Agent result was `FAIL|InvalidOperationException|Failed to connect to IPC pipe ... after 10 retries.`
- Harness disposition: `invalidated` because the single signed loopback failed before authenticated dispatch.
- Cleanup evidence: temporary directory absent; zero `ServiceHarness`, `AgentHarness`, or `Harness` processes; certificate thumbprint count was zero in CurrentUser `My`, `Root`, and `TrustedPublisher`.
- Process evidence: Service and Agent were the only disposable runtime processes; both exited with code 1 and were confirmed terminated. No production process or binary was signed.
- Exact C2 changed-line and branch coverage: absent; no coverage claim is made. Paths changed in the repository: `openspec/changes/windows-runtime-foundations/apply-progress.md` and this manifest only.
- `next_recommended`: none for this one-attempt continuation; keep 4.1–4.3 open and do not begin Unit D.

## Maintainer-authorized diagnostic correction

- Work unit: `C2-wintrust-abi-correction`.
- Native attempt binding: `sha256:4e1d86ec997cee153156bba32b4746ea17882d206cc5d696636bfc604a1fd3a1`; the native attempt ledger was not acquired, settled, reset, or mutated.
- Diagnosis confirmed in source: both Service and SessionAgent had malformed `WINTRUST_DATA` state/pointer fields and omitted `dwProvFlags`, shifting subsequent offsets. Service `IntegrityChecker` and `ProtectedProcessReporter` also carried malformed action GUIDs.
- Correction: both layouts now use the official sequential field order and managed `uint`/`IntPtr` types; `dwProvFlags` is explicitly zero-initialized. Each assembly centralizes the official Generic Verify V2 GUID `00AAC60B-0000-0000-C000-000000000046`. WinTrust retains the raw `LONG` result and treats only `== 0` as success.
- Focused tests: Service `AuthenticatedTransportTests` — PASS, 8/8; SessionAgent `WinTrustAbiTests` — PASS, 3/3.
- Focused builds: Service and SessionAgent project builds — PASS, 0 errors. Warnings are pre-existing analyzer noise.
- Broad tests/build: not run; the focused ABI/GUID correction was verified with proportionate targeted tests and project builds. No runtime harness is authorized in this scope.
- Harness disposition: `N/A` for this correction — no signed runtime/coverage proof was authorized or executed.
- Cleanup evidence: no certificate creation, certificate-store mutation, signing/signtool invocation, temporary signed copy, temporary harness, harness process, named-pipe loopback, or runtime cleanup activity occurred in this correction. Existing historical cleanup evidence above is preserved unchanged.
- Changed lines: 105 authored additions+deletions for this correction; below the 200-line maximum. Exact native C2 coverage remains absent.
- Correction objective: `passed` — the bounded ABI/GUID correction and its focused verification completed successfully.
- Remaining blockers: signed ACL-backed runtime acceptance, exact C2 changed-line/branch coverage, and tasks 4.1–4.3 proof requirements. C2 remains `blocked`; do not claim C2 PASS or begin Unit D.

## Historical Result Contract (superseded by the bounded focused-test continuation)

- `evidence_revision`: `sha256:1b4bdeb433c4b46419b48f866c6c522ff050c5f5b89e429d1eb016eecf7d111b` (SHA-256 of canonical UTF-8 manifest bytes with this self-referential line omitted).
- `status`: `passed` for measurement production; C2 remains blocked.
- `executive_summary`: Exact C2 changed-line measurement was produced: 71/315 instrumentable lines hit (22.54%), below the strict >80% threshold. Focused Domain, Service, and SessionAgent runs passed 4/4, 31/31, and 8/8; no runtime PASS is claimed.
- `artifacts`: this manifest and `apply-progress.md`; historical signed-loopback and invalidated-preflight evidence remain preserved above, and tasks 4.1–4.3 remain open.
- `next_recommended`: `none`; keep 4.1–4.3 open, C2 blocked, and Unit D pending.
- `risks`: signed runtime acceptance and the >80% changed-line threshold remain unmet; exact branch union is unavailable. Do not claim runtime PASS, C2 completion, or verification readiness.
- `harness_disposition`: `invalidated`; no certificate, signing, signtool, harness, loopback, or native-ledger activity occurred.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); measurement-only continuation with no source/test edits, certificate, signing, harness, commit, push, PR, review, or delivery action.

## Exact changed-line measurement continuation

- Work unit `C2-exact-coverage-measurement`; native token retained, not acquired/settled/reset/mutated: `sha256:ba476cb425757d4f64629455abbd453a23733c78687042ca6d95725d9c39c16d`.
- Base identity verified as `90a5a2a44299d99b67aff18da2a9182bc167fc75` (`90a5a2a`). Candidate identity: uncommitted ten-file C2 production snapshot `sha256:a378125e79b64b7cf234c8d9ec658e3378bf522363adf85f9bb7e6cdb59c69e5`.
- Tests: Domain `IpcHandshakeTests` 4/4; Service `AuthenticatedTransportTests|AgentLauncherCommandLineTests|AgentLauncherLaunchSeamTests|NamedPipeSecurityTests|IntegrityCheckerTests` 31/31; SessionAgent `WinTrustAbiTests|SessionAgentHostTests` 8/8. Each used `--collect:"XPlat Code Coverage"`.
- Commands (all exit 0): `dotnet test tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~IpcHandshakeTests --collect:"XPlat Code Coverage"`; `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthenticatedTransportTests|FullyQualifiedName~AgentLauncherCommandLineTests|FullyQualifiedName~AgentLauncherLaunchSeamTests|FullyQualifiedName~NamedPipeSecurityTests|FullyQualifiedName~IntegrityCheckerTests" --collect:"XPlat Code Coverage"`; `dotnet test tests/ControlParental.SessionAgent.Tests/ControlParental.SessionAgent.Tests.csproj --no-restore --filter "FullyQualifiedName~WinTrustAbiTests|FullyQualifiedName~SessionAgentHostTests" --collect:"XPlat Code Coverage"`.
- Exact C2 production diff intersection (additions only): 521 changed lines; 315 instrumentable; 71 hit; 206 excluded/non-instrumentable; `71/315 = 22.54%`, below the strict `>80%` threshold.

| Production file | Changed | Instrumentable | Hit | Excluded |
|---|---:|---:|---:|---:|
| `Domain/IpcHandshake.cs` | 55 | 32 | 25 | 23 |
| `Domain/IpcPhaseTrace.cs` | 41 | 12 | 12 | 29 |
| `Service/AgentLauncher.cs` | 3 | 3 | 3 | 0 |
| `Service/IntegrityChecker.cs` | 1 | 1 | 0 | 0 |
| `Service/Interop/AuthenticodeSigner.cs` | 95 | 54 | 22 | 41 |
| `Service/Interop/NamedPipeServer.cs` | 99 | 77 | 0 | 22 |
| `Service/Interop/WinTrust.cs` | 13 | 8 | 8 | 5 |
| `Service/ProtectedProcessReporter.cs` | 3 | 3 | 0 | 0 |
| `SessionAgent/Interop/AuthenticodeSigner.cs` | 129 | 64 | 1 | 65 |
| `SessionAgent/Interop/NamedPipeClient.cs` | 82 | 61 | 0 | 21 |

- Branch result: exact union unavailable because Cobertura maps duplicate async state-machine methods to the same source lines. The most precise defensible source-line projection is approximately `13/118 = 11.02%` conditions; it is explicitly not an exact branch claim.
- Aggregate project coverage is distinct: Domain `25/7,789` lines (`0.32%`), Service `513/21,125` (`2.42%`), SessionAgent `5/8,817` (`0.05%`), with aggregate branch rates `1.07%`, `2.65%`, and `0%` respectively.
- Result: measurement `passed` (measurement was produced); changed-line threshold failed. `harness_disposition=invalidated`; no runtime PASS, C2 PASS, or task completion is claimed. Tasks 4.1–4.3 remain open.
- Cleanup/process: temporary Cobertura directories were removed; no certificate creation/store mutation, signing/signtool, disposable signed file, harness, named-pipe loopback, or harness process occurred.
- Non-authoritative broad inventory: Domain 88/88 and SessionAgent 83/83 passed; Service 790/791 failed one unrelated `ScheduledWorkServiceAsyncDispatchTests` assertion, so that report was not used for the exact union.
- Evidence revision is recomputed below using the existing convention: SHA-256 of canonical UTF-8 manifest bytes with this self-referential revision line omitted.

## C2-targeted-coverage-tests continuation

- `work_unit`: `C2-targeted-coverage-tests`
- `native_token`: `sha256:c744d9f7d7884a04c2c3c827f3c89636b462a5c5623d2d79bc04088bcc627fdf` (retained; not acquired, settled, reset, or mutated)
- `changed_lines`: 521 production additions relative to verified baseline `90a5a2a44299d99b67aff18da2a9182bc167fc75`; 315 instrumentable; 74 hit; exact coverage `74/315 = 23.49%`.

| Production file | Changed | Instrumentable | Hit | Excluded |
|---|---:|---:|---:|---:|
| `Domain/IpcHandshake.cs` | 55 | 32 | 25 | 23 |
| `Domain/IpcPhaseTrace.cs` | 41 | 12 | 12 | 29 |
| `Service/AgentLauncher.cs` | 3 | 3 | 3 | 0 |
| `Service/IntegrityChecker.cs` | 1 | 1 | 0 | 0 |
| `Service/Interop/AuthenticodeSigner.cs` | 95 | 54 | 22 | 41 |
| `Service/Interop/NamedPipeServer.cs` | 99 | 77 | 3 | 22 |
| `Service/Interop/WinTrust.cs` | 13 | 8 | 8 | 5 |
| `Service/ProtectedProcessReporter.cs` | 3 | 3 | 0 | 0 |
| `SessionAgent/Interop/AuthenticodeSigner.cs` | 129 | 64 | 22 | 65 |
| `SessionAgent/Interop/NamedPipeClient.cs` | 82 | 61 | 4 | 21 |
| **Total** | **521** | **315** | **74** | **206** |

- `focused_tests`: Domain `IpcHandshakeTests` 9/9; Service C2 transport/identity/launcher/security filters 33/33; SessionAgent `WinTrustAbiTests|SessionAgentHostTests` 14/14. All exited 0.
- `focused_builds`: Service test project exited 0 with 0 errors and 5 pre-existing package warnings; SessionAgent test project exited 0 with 0 errors and 0 warnings.
- `branch_coverage`: aggregate Cobertura Domain 16/1,024 (`1.56%`), Service 102/3,622 (`2.81%`), SessionAgent 10/1,332 (`0.75%`). Exact changed-branch union is unavailable because Cobertura duplicates generated async state-machine mappings onto source lines; these aggregate figures are not the C2 gate and no branch PASS is claimed.
- `harness_disposition`: `invalidated`; runtime harness is excluded/unauthorized for this continuation. No signed runtime evidence is claimed.
- `cleanup_evidence`: coverage result directories removed; no certificate, store mutation, signing, signtool, signed artifact, named-pipe loopback, harness process, or native-ledger activity.
- `process_evidence`: only focused test/build processes ran; no harness process remained.
- `changed_paths`: `tests/ControlParental.Domain.Tests/IpcHandshakeTests.cs`, `tests/ControlParental.Service.Tests/AuthenticatedTransportTests.cs`, `tests/ControlParental.SessionAgent.Tests/WinTrustAbiTests.cs`, this manifest, and `apply-progress.md`.
- `remaining_blockers`: exact changed-line threshold `>80%`, exact changed-branch union, signed ACL-backed runtime acceptance, and the open 4.1–4.3 proof requirements. Unit D remains pending.

## Historical Result Contract — C2-targeted-coverage-tests (superseded)

- `evidence_revision`: `sha256:8c8b7b904992a41b10ede6ac02f1c767278667915c53e8af7ebbc83391912bfc` (SHA-256 of canonical UTF-8 manifest bytes with this self-referential line omitted).
- `status`: `passed` for bounded focused-test work and valid exact measurement production; C2 remains blocked.
- `executive_summary`: The bounded continuation added behavioral/security rejection tests and produced valid exact changed-line coverage of 74/315 instrumentable lines (23.49%), below the strict >80% threshold. Focused Domain, Service, and SessionAgent runs passed 9/9, 33/33, and 14/14.
- `artifacts`: this manifest and `apply-progress.md`; historical A/B/C1/C2 evidence remains preserved, and tasks 4.1–4.3 remain open.
- `next_recommended`: `none`; do not claim C2 PASS, runtime PASS, verification readiness, or begin Unit D.
- `risks`: signed runtime acceptance, exact changed-line threshold, and exact changed-branch union remain unmet.
- `harness_disposition`: `invalidated`; no runtime harness or signed runtime evidence was executed.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); no production edits, native ledger mutation, certificate, signing, harness, commit, push, PR, review, or delivery action.

## C2-minimal-testability-refactor continuation

- `work_unit`: `C2-minimal-testability-refactor`
- `native_token`: `sha256:ef3c5c043bd3b9b691deb190d8d8d22f78ec0c40255534dba0e571507ba3caee` (retained; not acquired, settled, reset, or mutated)
- `outcome`: `blocked`; internal transport seams and focused behavior tests were implemented and built, but the strict exact changed-line gate was not proven in this bounded continuation.
- `changed_paths`: `src/ControlParental.Service/Interop/NamedPipeServer.cs`, `src/ControlParental.SessionAgent/Interop/NamedPipeClient.cs`, `tests/ControlParental.Service.Tests/AuthenticatedTransportTests.cs`, `tests/ControlParental.SessionAgent.Tests/NamedPipeClientSeamTests.cs`, this manifest, and `apply-progress.md` (other pre-existing working-tree paths are preserved and not attributed to this unit).
- `focused_tests`: Service `AuthenticatedTransportTests` 11/11; SessionAgent `NamedPipeClientSeamTests` 1/1; coverage runs Domain 9/9, Service 30/30, SessionAgent 15/15. All exited 0.
- `focused_builds`: Service and SessionAgent test projects exited 0 with 0 errors.
- `runtime_harness`: `N/A`; signed runtime, real named-pipe loopback, certificate/signing, and native harness activity are absolutely excluded. `harness_disposition=invalidated`.
- `cleanup_evidence`: temporary coverage directories were removed; no certificate/store mutation, signed artifact, harness process, named-pipe loopback, or native-ledger activity occurred.
- `process_evidence`: only focused test/build processes ran and no harness process remained.
- `aggregate_coverage`: Domain `29/7,789` lines (`0.37%`), `16/1,024` branches (`1.56%`); Service `660/21,150` lines (`3.12%`), `129/3,630` branches (`3.55%`); SessionAgent `167/8,837` lines (`1.88%`), `45/1,338` branches (`3.36%`). These are not exact C2 changed-scope metrics.
- `exact_changed_line_coverage`: not defensibly recomputed from the available Cobertura artifacts in this continuation; no exact numerator/denominator or threshold PASS is claimed. Baseline remains verified `90a5a2a44299d99b67aff18da2a9182bc167fc75`.
- `exact_changed_branch_coverage`: unavailable; Cobertura duplicates generated async state-machine mappings onto source lines. Aggregate branch values above are reported only as the best available metric.
- `remaining_blockers`: exact changed-line `>80%` proof, exact changed-branch union, signed ACL-backed runtime acceptance, and task-specific 4.1–4.3 conditions. Unit D remains pending.
- `rollback_boundary`: revert only the internal pipe seams/adapters, the two seam-test additions, and this continuation evidence; preserve prior C2 evidence and A/B/C1.

## Historical Result Contract — C2-minimal-testability-refactor (superseded)

- `evidence_revision`: `sha256:98d8bce95bc60af32ceba277eb50267e0556683dc420403e614b7f1fdaf57a0a`
- `status`: `blocked`
- `executive_summary`: Minimal internal pipe seams made transport ordering and handshake writes executable without native runtime activity; focused tests/builds passed, but exact changed-line coverage and signed runtime acceptance remain unproven.
- `artifacts`: this manifest and `apply-progress.md`; historical evidence remains preserved and tasks 4.1–4.3 remain open.
- `next_recommended`: `none`; do not claim C2 PASS, runtime PASS, verification readiness, or begin Unit D.
- `risks`: exact changed-line/branch coverage and signed runtime acceptance remain unmet.
- `harness_disposition`: `invalidated`
- `cleanup/process_evidence`: complete as stated above; no harness or native-ledger activity.

## Post-seam exact coverage measurement

- `work_unit`: `C2-post-seam-exact-coverage`
- `native_token`: `sha256:57105482cc3909821964dbfb9dc8366de9ba8549c79ab4b55c1c568b3b03ea3a` (retained; not acquired, settled, reset, or mutated)
- Baseline `90a5a2a44299d99b67aff18da2a9182bc167fc75` was verified as the isolated C1 boundary. The candidate production-scope identity was snapshotted before measurement as `sha256:55f001dae27ae4ec1f809ce7d3ecc5c17d6fba8eb311176ec5a3266e9a01275`.
- Exact commands, all exit 0: `dotnet test tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj --no-restore --filter FullyQualifiedName~IpcHandshakeTests --collect:"XPlat Code Coverage" --results-directory <temp>/domain`; `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --filter "FullyQualifiedName~AuthenticatedTransportTests|FullyQualifiedName~AgentLauncherCommandLineTests|FullyQualifiedName~AgentLauncherLaunchSeamTests|FullyQualifiedName~NamedPipeSecurityTests|FullyQualifiedName~IntegrityCheckerTests" --collect:"XPlat Code Coverage" --results-directory <temp>/service`; `dotnet test tests/ControlParental.SessionAgent.Tests/ControlParental.SessionAgent.Tests.csproj --no-restore --filter "FullyQualifiedName~WinTrustAbiTests|FullyQualifiedName~NamedPipeClientSeamTests|FullyQualifiedName~SessionAgentHostTests" --collect:"XPlat Code Coverage" --results-directory <temp>/sessionagent`.
- Fresh focused results: Domain 9/9, Service 34/34, SessionAgent 15/15. All three Cobertura files were retained through intersection.

| Production file | Changed | Instrumentable | Hit | Excluded |
|---|---:|---:|---:|---:|
| `Domain/IpcHandshake.cs` | 55 | 32 | 16 | 23 |
| `Domain/IpcPhaseTrace.cs` | 41 | 12 | 12 | 29 |
| `Service/AgentLauncher.cs` | 3 | 3 | 3 | 0 |
| `Service/IntegrityChecker.cs` | 1 | 1 | 0 | 0 |
| `Service/Interop/AuthenticodeSigner.cs` | 95 | 54 | 22 | 41 |
| `Service/Interop/NamedPipeServer.cs` | 168 | 118 | 60 | 50 |
| `Service/Interop/WinTrust.cs` | 13 | 8 | 8 | 5 |
| `Service/ProtectedProcessReporter.cs` | 3 | 3 | 0 | 0 |
| `SessionAgent/Interop/AuthenticodeSigner.cs` | 129 | 64 | 22 | 65 |
| `SessionAgent/Interop/NamedPipeClient.cs` | 129 | 88 | 49 | 41 |
| **Total** | **637** | **383** | **192** | **254** |

- Exact changed-line coverage is `192/383 = 50.13%`; strict `>80%` verdict: `FAIL`. Changed lines were additions from the baseline-to-candidate diff, including all current lines for new files. Cobertura records were merged by normalized source path plus line; duplicate mappings count as hit only when any mapping reports hits. The 254 exclusions are changed lines with no retained Cobertura mapping; they were not counted as covered.
- Exact changed-branch union is not defensible because Cobertura duplicates async state-machine mappings on source lines. Most precise retained aggregate: Domain `16/1,024` (`1.56%`), Service `138/3,630` (`3.80%`), SessionAgent `45/1,338` (`3.36%`). These aggregate figures are not changed-scope branch coverage and no branch PASS is claimed.
- Aggregate line coverage is distinct from the exact changed-line gate: Domain `29/7,789` (`0.37%`), Service `680/21,150` (`3.22%`), SessionAgent `167/8,837` (`1.89%`).
- `outcome`: `passed` for producing a defensible exact measurement; threshold result failed. `harness_disposition=invalidated`; no signed runtime acceptance is claimed and 4.1–4.3 remain open.
- Cleanup/process: Cobertura directories were removed after intersection and evidence capture; no certificate/store mutation, signing, signtool, harness, named-pipe loopback, or native-ledger activity occurred; no harness process remained.
- `changed_paths`: `openspec/changes/windows-runtime-foundations/apply-progress.md`, `openspec/changes/windows-runtime-foundations/evidence-manifest-unit-c2.md`.

## Historical Result Contract — C2-post-seam-exact-coverage (superseded)

- `evidence_revision`: `sha256:9f3b7f053d9e00e4ffbe5c15c1c964fd56a519c1d8b84b0f65ff9d73d006d201`
- `status`: `passed` for measurement production; C2 remains blocked because the strict exact-line threshold failed.
- `executive_summary`: Post-seam focused suites passed 9/9, 34/34, and 15/15; exact C2 changed-line coverage is 192/383 instrumentable lines (`50.13%`), below `>80%`.
- `artifacts`: this manifest and `apply-progress.md`; historical evidence remains preserved.
- `next_recommended`: `none`; keep 4.1–4.3 open, do not claim signed runtime acceptance, and do not begin Unit D.
- `risks`: exact changed-branch union remains unavailable; signed ACL-backed runtime acceptance and the changed-line threshold remain unmet.
- `harness_disposition`: `invalidated`
- `settlement_evidence`: outcome `passed`; diagnosis `exact measurement produced, threshold failed`; cleanup complete after artifact capture; no prohibited native activity.

## Task 4.1 scope-classification settlement

- `work_unit`: `C2-scope-classification`
- `status`: `passed` for exact classification reconciliation; C2 remains blocked.
- `baseline`: verified `90a5a2a44299d99b67aff18da2a9182bc167fc75`, tree `c33bd1e53d46abbb95d33f345e6e135790dfeda3`.
- `candidate`: full-tree snapshot `sha256:04dea78271d9dd88db51f13dbb823cb6ce9b1a5ae0897a711dfdd5a5d13ce5c5`; C2 production-scope snapshot `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`.
- `classification`: 481 managed-deterministic + 156 native-boundary = 637 changed production additions, exactly reconciled. The dedicated path/range manifest is `evidence-manifest-unit-c2-scope.md`.
- `scope_rule`: mixed files are split by behavior and line range; missing Cobertura mappings, async generation, private visibility, and low coverage are not native reasons. Historical `192/383 = 50.13%` is not the scoped managed gate.
- `result`: task 4.1 is complete; tasks 4.2–4.3 remain open; C2 and Unit D remain blocked. No coverage, branch, runtime, or C2 PASS is claimed by this classification.
- `historical_classification_revision`: `sha256:e344f980b0fddc92cbbbade3fc633d62b01f2a30632bba4dee0118b805a3f624` (superseded; the current managed-gate contract below is authoritative).
- `rollback_boundary`: remove only the classification manifest and these documentary additions; preserve production/test paths and historical evidence.

## Historical Result Contract — C2-scope-classification (superseded)

- `evidence_revision`: `sha256:e344f980b0fddc92cbbbade3fc633d62b01f2a30632bba4dee0118b805a3f624` (canonical UTF-8 manifest hash with this self-referential revision line omitted).
- `status`: passed for classification only; overall C2 blocked.
- `executive_summary`: Task 4.1 froze the verified baseline and immutable C2 candidate, classified every changed C2 production addition by behavior, and reconciled the scope exactly without claiming managed coverage, native runtime acceptance, or C2 completion.
- `artifacts`: `tasks.md`, `apply-progress.md`, `evidence-manifest-unit-c2.md`, and `evidence-manifest-unit-c2-scope.md`.
- `next_recommended`: task 4.2 managed coverage gate only.
- `risks`: managed coverage and native runtime remain unverified; tasks 4.2–4.4 remain open and Unit D remains blocked.
- `harness_disposition`: invalidated/not run by scope; no runtime or harness activity was performed for classification.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`).
- `baseline`: commit `90a5a2a44299d99b67aff18da2a9182bc167fc75`, tree `c33bd1e53d46abbb95d33f345e6e135790dfeda3`.
- `candidate`: full-tree `sha256:04dea78271d9dd88db51f13dbb823cb6ce9b1a5ae0897a711dfdd5a5d13ce5c5`; C2 production scope `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`.
- `counts`: 481 `managed-deterministic` + 156 `native-boundary` = 637 changed C2 production additions.
- `reconciliation`: exact by normalized repository path and current source line range; no addition was counted twice or omitted.

## Historical Result Contract — C2-complete-managed-coverage-gate (superseded)

- `evidence_revision`: `sha256:7909dfb285161af578f2f25c93f8cde13ac59d5b22d9b3400420c506060fddcd` (canonical UTF-8 manifest hash with this self-referential line omitted).
- `status`: `blocked`; `outcome`: fresh exact managed measurement and bounded diagnosis completed, but strict managed coverage gate failed and overall C2 remains blocked.
- `executive_summary`: Domain, Service, and SessionAgent Cobertura suites passed 9/9, 34/34, and 15/15. Exact managed coverage is 185/481 = 38.461538%; 193 managed lines are unmapped and remain in the denominator. The 288 currently instrumented lines impose a hard ceiling of 59.875260%, so `>80%` cannot be reached without prohibited broad source-layout/instrumentation changes or denominator manipulation.
- `changed_lines_attribution`: no production or test lines changed in this work unit; 481 managed-deterministic and 156 native-boundary additions remain unchanged from the authoritative C2 scope.
- `diagnosis`: `193 compiler/source mapping`, `0 unexecuted behavior`, `0 unreachable defensive path`, `0 classification defect`; separately, 103 mapped managed lines are currently unhit and represent meaningful unexecuted handshake, lifecycle, serialization, reconnect/deadline, signer-orchestration, and fail-closed behavior.
- `artifacts`: `tasks.md`, `apply-progress.md`, `evidence-manifest-unit-c2.md`, and `evidence-manifest-unit-c2-scope.md`; source and test files were not changed.
- `next_recommended`: `none`; keep task 4.2 open. Task 4.3 is not authorized to start because 4.2 is not a strict PASS; task 4.4 and Unit D remain blocked.
- `risks`: exact changed-branch union remains unavailable; signed native acceptance remains outside this work unit and unproven.
- `harness_disposition`: `invalidated`; runtime harness, certificate/store mutation, signing, signtool, named-pipe loopback, and native-ledger activity were excluded and not performed.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); `work_unit=C2-complete-managed-coverage-gate`; delivery `exception-ok/size-exception`; no delegation, commit, push, PR, review, merge, framework, or package action.
- `focused_commands`: three fresh `dotnet test --no-restore --collect:"XPlat Code Coverage"` commands for `IpcHandshakeTests`, the Service C2 filter, and the SessionAgent WinTrust/client-seam/host filter; results 9/9, 34/34, 15/15, all exit 0. Focused Service and SessionAgent builds exited 0 errors.
- `managed_per_file`:

| File | Managed total | Hit | Exact % | Unmapped | Mapped-unhit |
|---|---:|---:|---:|---:|---:|
| Domain/IpcHandshake.cs | 55 | 29 | 52.727273% | 23 | 3 |
| Domain/IpcPhaseTrace.cs | 41 | 12 | 29.268293% | 29 | 0 |
| Service/AgentLauncher.cs | 3 | 3 | 100.000000% | 0 | 0 |
| Service/Interop/AuthenticodeSigner.cs | 72 | 14 | 19.444444% | 34 | 24 |
| Service/Interop/NamedPipeServer.cs | 138 | 60 | 43.478261% | 41 | 37 |
| Service/Interop/WinTrust.cs | 1 | 0 | 0.000000% | 1 | 0 |
| SessionAgent/Interop/AuthenticodeSigner.cs | 61 | 18 | 29.508197% | 30 | 13 |
| SessionAgent/Interop/NamedPipeClient.cs | 110 | 49 | 44.545455% | 35 | 26 |
| **Total** | **481** | **185** | **38.461538%** | **193** | **103** |
- `branch_evidence`: bounded aggregate only — Domain 16/1,024 (1.56%), Service 138/3,630 (3.80%), SessionAgent 45/1,338 (3.36%). Async duplicate mappings prevent exact changed-branch union; no branch PASS is claimed.
- `cleanup_process_evidence`: Cobertura artifacts were retained through normalized path+line intersection, then removed; only focused test/build processes ran, no harness process remained, and no prohibited native activity occurred.
- `settlement_evidence`: `status=blocked`; `outcome=diagnosis complete, strict gate FAIL`; exact managed numerator/denominator `185/481`; unmapped managed `193`; hard mapped ceiling `288/481`; task checkbox remains open; rollback is documentary additions only.

## Historical Result Contract — C2-executable-managed-gate (superseded)

- `evidence_revision`: `sha256:9bd629515fadf6a7a9548dfe90efe23faabd830898bd50c7d066953cd77bdeaf` (canonical UTF-8 manifest hash with this self-referential line omitted).
- `status`: `blocked`; `outcome`: deterministic managed behavior tests and fresh normalized coverage completed, but the strict executable-managed threshold failed.
- `executive_summary`: Focused Domain, Service, and SessionAgent suites passed 9/9, 49/49, and 17/17. The sole authoritative executable-managed gate is 213/288 = 73.958333%; the strict gate FAILED and 18 more hits are needed to reach 231/288 (`>80%`). Source/global context is 213/481 = 44.282744% only and is never authoritative. There are 193 proven non-executable compiler/PDB gaps and 75 mapped executable unhit lines.
- `changed_lines_attribution`: no production lines changed; only existing C2 test seams were exercised and two existing C2 test files received bounded behavior assertions. Candidate, 4.1 classification, and denominator remain unchanged.
- `named_behaviors_tested`: authentication rejection/no pre-auth dispatch; known managed message dispatch; per-connection serialized writes; cancellation/read-failure cleanup; signer fail-closed outcomes; client handshake/payload ordering and cancelled writes.
- `managed_per_file`: `IpcHandshake` 29/55; `IpcPhaseTrace` 12/41; `AgentLauncher` 3/3; Service `AuthenticodeSigner` 29/72; Service `NamedPipeServer` 71/138; Service `WinTrust` 0/1; SessionAgent `AuthenticodeSigner` 18/61; SessionAgent `NamedPipeClient` 51/110.
- `non_executable`: 193 compiler/PDB sequence-point gaps remain documented and excluded only by existing path+line evidence; no missing mapping was reclassified as covered.
- `unhit`: 75 mapped executable managed lines remain uncovered and are retained in the denominator.
- `branch_evidence`: bounded aggregate only — Domain 16/1,024 (1.56%), Service 138/3,630 (3.80%), SessionAgent 45/1,338 (3.36%); exact changed-branch union is unavailable because Cobertura duplicates async state-machine mappings.
- `focused_commands`: fresh `dotnet test --no-restore --collect:"XPlat Code Coverage"` for Domain `IpcHandshakeTests` — PASS 9/9; Service C2 filter — PASS 49/49; SessionAgent C2 filter — PASS 17/17. Focused Service and SessionAgent builds — PASS, 0 errors.
- `runtime_harness`: `N/A`; signed/unsigned harness, certificate/signing, real named-pipe loopback, and native runtime activity are absolutely excluded. `harness_disposition=invalidated`.
- `cleanup_process_evidence`: temporary coverage directories were removed after normalized intersection; only focused test/build processes ran; no harness process remained; native attempt ledger was not mutated.
- `artifacts`: `tasks.md`, `apply-progress.md`, this manifest, and the scope manifest; production was unchanged after the temporary seam experiment was reverted.
- `next_recommended`: `none`; keep 4.2 and 4.2.1–4.2.3 open. 4.3 is the only next task but remains blocked; 4.4 and Unit D remain blocked.
- `remaining_blockers`: strict managed coverage threshold, exact changed-branch union, and separately blocked native signed acceptance.
- `rollback_boundary`: revert only the bounded C2 test additions and this continuation evidence; preserve prior C2 history, production paths, A/B/C1, and unrelated working-tree changes.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); work unit `C2-executable-managed-gate`; `exception-ok/size-exception`; no delegation, commit, push, PR, review, merge, package, framework, native ledger, or harness action.
- `settlement_evidence`: `status=blocked`; `outcome=bounded behavior-test continuation and coverage refresh complete, strict gate FAIL`; sole authoritative executable-managed gate `213/288`; 18 more hits are needed; source/global context `213/481` only; non-executable `193`; unhit `75`; Service focused result `49/49`; task checkboxes remain open. Tasks 4.2/4.2.1–4.2.3 remain open; 4.3/4.4 and Unit D remain blocked. No production attribution or native activity occurred. Changed continuation paths: `tests/ControlParental.Service.Tests/AuthenticatedTransportTests.cs`, `tests/ControlParental.SessionAgent.Tests/NamedPipeClientSeamTests.cs`, this manifest, `apply-progress.md`, and `tasks.md`. Service aggregate branch evidence is 165/3,630 (4.54%); SessionAgent is 47/1,338 (3.51%); Domain remains 16/1,024 (1.56%).

## Historical Result Contract — C2-final-executable-table (superseded)

- `evidence_revision`: `sha256:3733a52fc3f673f8ebd24d4011cd66a763cf617a199da0d41e304a47abb2f331` (SHA-256 of canonical UTF-8 manifest bytes with this self-referential line omitted).
- `status`: `blocked`; `outcome`: fresh measurement and exact per-file reconciliation completed, but the current candidate did not reproduce the prior 240/288 result.
- `executive_summary`: Current production identity matches the frozen C2 production candidate, but the retained current test snapshot is `sha256:555c2c68d9eb90519d6ff20f83b910ca0fa3e5798a69a4ea2ff5b62b5b293f77` and no comparable prior test identity was retained. The prior `240/288` result is historical and unreproducible. Fresh focused suites passed Domain `9/9`, Service `54/54`, and SessionAgent `20/20`; the authoritative executable-managed result is `213/288 = 73.958333%`, so strict `>80%` FAIL. No candidate identities were mixed.
- `candidate_identity`: production `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`; focused tests `sha256:555c2c68d9eb90519d6ff20f83b910ca0fa3e5798a69a4ea2ff5b62b5b293f77`; baseline commit `90a5a2a44299d99b67aff18da2a9182bc167fc75`.
- `changed_lines_attribution`: no production or test files were edited in this measurement-only unit. The current candidate was measured as-is.
- `named_behaviors_tested`: existing final focused suites only; no new tests or production seams were added.
- `managed_per_file`:

| Production file | Managed source lines | Executable denominator | Hit | Mapped unhit | Non-executable |
|---|---:|---:|---:|---:|---:|
| `Domain/IpcHandshake.cs` | 55 | 32 | 29 | 3 | 23 |
| `Domain/IpcPhaseTrace.cs` | 41 | 12 | 12 | 0 | 29 |
| `Service/AgentLauncher.cs` | 3 | 3 | 3 | 0 | 0 |
| `Service/Interop/AuthenticodeSigner.cs` | 72 | 38 | 29 | 9 | 34 |
| `Service/Interop/NamedPipeServer.cs` | 138 | 97 | 71 | 26 | 41 |
| `Service/Interop/WinTrust.cs` | 1 | 0 | 0 | 0 | 1 |
| `SessionAgent/Interop/AuthenticodeSigner.cs` | 61 | 31 | 18 | 13 | 30 |
| `SessionAgent/Interop/NamedPipeClient.cs` | 110 | 75 | 51 | 24 | 35 |
| **Total** | **481** | **288** | **213** | **75** | **193** |

- `non_executable_path_line_proof`: Domain/IpcHandshake.cs `1-5,7-10,12-13,16,23-24,29-30,36,40,44,48,50-51,55`; Domain/IpcPhaseTrace.cs `1-22,24,26,28-29,34-35,41`; Service/Interop/AuthenticodeSigner.cs `1-10,12,16,20,22,24-25,32-33,35,39,43,45,51-54,62,67-68,80,85,87,89,95`; Service/Interop/NamedPipeServer.cs `21,27,29-31,44-45,49,108,189,194,198-202,291,327,339,361,373,381,389-390,400,402,447,450,454-466`; Service/Interop/WinTrust.cs `72`; SessionAgent/Interop/AuthenticodeSigner.cs `1-9,11-12,14,18,22,24,26-27,34-35,37,41,45,47,53-56,63,81-82`; SessionAgent/Interop/NamedPipeClient.cs `18,21-25,27,39,152-153,159-160,165-166,184,190,203-204,210-211,220-221,229,273-274,277-285,296`; AgentLauncher.cs has none. These are path+line mapping gaps, not native exclusions.
- `native_path_line_proof`: unchanged and retained in `evidence-manifest-unit-c2-scope.md`; native-boundary ranges remain 156 lines and are not inferred from missing mappings.
- `unhit`: 75 mapped executable managed lines remain uncovered and are retained in the denominator.
- `branch_evidence`: one bounded aggregate set only — Domain `16/1,024` (1.56%), Service `171/3,630` (4.71%), SessionAgent `48/1,338` (3.59%). Exact changed-branch union availability: unavailable because Cobertura duplicates async state-machine mappings; aggregate values do not establish a changed-branch gate.
- `focused_commands`: fresh first run with build and coverage, then exact no-build rerun, using `dotnet test --no-restore --filter ... --collect:"XPlat Code Coverage" --results-directory <temp>` for Domain `IpcHandshakeTests` — PASS `9/9`; Service C2 filter — PASS `54/54`; SessionAgent C2 filter — PASS `20/20`. Focused Service and SessionAgent builds — PASS, 0 errors.
- `runtime_harness`: `N/A`; signed/unsigned harness, certificate/signing, real named-pipe loopback, and native runtime activity are absolutely excluded. `harness_disposition=invalidated`.
- `cleanup_process_evidence`: temporary coverage outputs were retained through exact path+line intersection and evidence capture, then removed; only focused test/build processes ran; no harness process remained; native attempt ledger was not mutated.
- `artifacts`: `tasks.md`, `apply-progress.md`, this manifest, and the scope manifest.
- `next_recommended`: `none`; 4.3 remains next/open but blocked by excluded native proof; 4.4 and Unit D remain blocked.
- `remaining_blockers`: strict executable-managed threshold, prior candidate/test identity reconciliation, signed native acceptance, and exact changed-branch union.
- `rollback_boundary`: revert only the four documentary updates from this measurement settlement; preserve all production/test paths, prior C2 history, A/B/C1, and unrelated working-tree changes.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); work unit `C2-final-executable-table`; `exception-ok/size-exception`; no delegation, commit, push, PR, review, merge, package, framework, native ledger, or harness action.
- `settlement_evidence`: `status=blocked`; exact executable-managed `213/288 = 73.958333%`; mapped unhit `75`; non-executable `193`; fresh focused suites `9/9`, `54/54`, `20/20`; 4.2 and subtasks remain open; 4.3 next/open but blocked; 4.4 and Unit D blocked; cleanup occurred only after evidence capture.

## Historical Result Contract — C2-managed-coverage-correction

- `evidence_revision`: `sha256:da1a800c0ff3891b0aa763e0d98b4fdd6b04d51b2afbb72b13caa56a96706f31` (SHA-256 of canonical UTF-8 manifest bytes with this self-referential line omitted).
- `status`: `passed`; `outcome`: bounded behavior-value test correction completed and the fresh strict executable-managed coverage gate passed.
- `executive_summary`: Existing C2 test files now cover additional managed rejection, identity, trust-result, and authenticated-dispatch behaviors without production edits. Fresh exact coverage is `232/288 = 80.555556%`, strictly above 80%; the prior invalid `240/288` result remains historical and was not reused.
- `artifacts`: `tasks.md`, `apply-progress.md`, this manifest, and `evidence-manifest-unit-c2-scope.md`; current task 4.2 and 4.2.1–4.2.3 are complete, while 4.3 remains next/open and blocked.
- `candidate_identity`: production `sha256:b4dc9f7002a294b227ecb848dce860984ccd2154a408ebd03e84719451c6810c`; focused tests `sha256:6402caea1b7fcee2e134e005311165bef34c7a3325e7e5a7334e28e60fe3f74c`; baseline `90a5a2a44299d99b67aff18da2a9182bc167fc75`.
- `changed_lines_attribution`: 103 authored test additions from the acquired candidate; existing C2 test files only; no production edits or seams.
- `named_behaviors_tested`: malformed handshake value-type rejection; signer raw-trust and process identity metadata; missing/unexpected SID and null security-helper inputs; authenticated `UsageStateResponse` dispatch; untrusted local signer rejection; wrong server process identity rejection.
- `managed_per_file`:

| Production file | Managed source lines | Executable denominator | Hit | Mapped unhit | Non-executable |
|---|---:|---:|---:|---:|---:|
| `Domain/IpcHandshake.cs` | 55 | 32 | 32 | 0 | 23 |
| `Domain/IpcPhaseTrace.cs` | 41 | 12 | 12 | 0 | 29 |
| `Service/AgentLauncher.cs` | 3 | 3 | 3 | 0 | 0 |
| `Service/Interop/AuthenticodeSigner.cs` | 72 | 38 | 33 | 5 | 34 |
| `Service/Interop/NamedPipeServer.cs` | 138 | 97 | 71 | 26 | 41 |
| `Service/Interop/WinTrust.cs` | 1 | 0 | 0 | 0 | 1 |
| `SessionAgent/Interop/AuthenticodeSigner.cs` | 61 | 31 | 28 | 3 | 30 |
| `SessionAgent/Interop/NamedPipeClient.cs` | 110 | 75 | 53 | 22 | 35 |
| **Total** | **481** | **288** | **232** | **56** | **193** |

- `non_executable_path_line_proof`: retained unchanged from the prior current table; all 193 exclusions remain independently proven compiler/PDB path+line gaps and were not converted into coverage.
- `branch_evidence`: bounded aggregate only — Domain `16/1,024` (1.56%), Service `171/3,630` (4.71%), SessionAgent `48/1,338` (3.59%); exact changed-branch union unavailable because Cobertura duplicates async state-machine mappings.
- `focused_commands`: fresh `dotnet test --no-restore --filter ... --collect:"XPlat Code Coverage" --results-directory <temp>` for Domain `IpcHandshakeTests` — PASS `11/11`; Service C2 filter — PASS `58/58`; SessionAgent `WinTrustAbiTests|SessionAgentHostTests|NamedPipeClientSeamTests` — PASS `23/23`. Focused Service and SessionAgent builds — PASS, 0 errors.
- `runtime_harness`: `N/A`; signed/unsigned harness, certificate/signing, real named-pipe loopback, and native runtime activity are absolutely excluded. `harness_disposition=invalidated`.
- `cleanup_process_evidence`: coverage outputs were retained through exact path+line intersection and evidence capture, then removed; only focused test/build processes ran; no harness process remained; native attempt ledger was not mutated.
- `next_recommended`: task 4.3 only, but it remains blocked by the excluded native signed proof; 4.4 and Unit D remain blocked.
- `rollback_boundary`: revert only the added behavior-value test lines and this current documentary settlement; preserve all production paths, prior C2 history, A/B/C1, and unrelated working-tree changes.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); `work_unit=C2-managed-coverage-correction`; delivery `exception-ok/size-exception`; no delegation, commit, push, PR, review, package, native ledger, or harness action.
- `settlement_evidence`: `status=passed`; exact executable-managed `232/288 = 80.555556%`; denominator unchanged; 193 exclusions retained; fresh suites/builds passed; task 4.2 and subtasks complete; 4.3 next/open; 4.4 and Unit D blocked; cleanup occurred only after evidence capture.

## Historical Result Contract — C2-signed-native-acceptance

- `evidence_revision`: `sha256:676fe314fd2af9be1addd9e79f5851dfb88c10dbfc88cf21410dc7dd55727ff0` (SHA-256 of canonical UTF-8 manifest bytes with this self-referential line omitted).
- `status`: `failed`; `outcome`: disposable signed native acceptance did not reach authenticated dispatch in the single authorized run.
- `executive_summary`: Full disposable ServiceHarness/AgentHarness preflight passed before side effects. One CurrentUser-only certificate was created, both disposable executables were signed and independently verified, and exactly one signed loopback was attempted. Service timed out while Agent exhausted ten connection attempts; no authenticated dispatch was observed.
- `artifacts`: `tasks.md`, `apply-progress.md`, this manifest, and `evidence-manifest-unit-c2-scope.md`; task 4.2 evidence remains preserved and complete at `232/288 = 80.555556%`.
- `preflight`: final restore/build/publish passed with 0 errors for both disposable harnesses under `C:\Users\Usuario\AppData\Local\Temp\opencode\c2-signed-native-acceptance` after an initial disposable compile failure caused by missing harness-only implicit usings; no certificate existed during the correction or before final preflight completion.
- `certificate_store_boundary`: one non-exportable disposable code-signing certificate; CurrentUser `My` creation and temporary `Root`/`TrustedPublisher` trust only; no PFX/private-key export and no LocalMachine access. The thumbprint was tracked privately and is intentionally omitted.
- `signature_verification`: SignTool `/verify /pa /all` exit 0 and PowerShell `Get-AuthenticodeSignature=Valid` for both disposable EXEs. Real image paths were `...\publish\service\ServiceHarness.exe` and `...\publish\agent\AgentHarness.exe`.
- `exactly_one_run`: `1`; no retry, second certificate, second signature, or diagnostic rerun occurred.
- `identity_and_process`: Service PID/SID/session `17804` / `S-1-5-21-3623415481-2756580284-2510936249-1001` / `1`; Agent `26244` / same SID / `1`. Both were disposable temp images; both exited failure and no process remained after cleanup.
- `raw_wintrust`: `N/A` for both sides because the harness did not emit the raw `LONG` return; no post-run diagnostic invocation was authorized. This is an evidence gap, not an inferred value.
- `phase_ordering_and_deadlines`: observed only `server.start-requested` and `client.start-requested`; no authenticated-agent, client-authenticated, server/client hello, dispatch, or response phase. Service cancellation deadline was 20 seconds; Agent overall deadline was 15 seconds with ten internal 1-second connection attempts. Separate connect/auth/response deadlines were not proven.
- `dispatch_outcome`: `FAIL`; no message reached the Service handler. Service observed `TaskCanceledException`; Agent observed `InvalidOperationException` after ten retries. Process exits were failure (`1` by harness return path).
- `cleanup_proof`: temporary directory absent; zero `ServiceHarness`/`AgentHarness` processes; tracked certificate absent from CurrentUser `My`, `Root`, and `TrustedPublisher`. No production/test/package/binary file changed; exactly the four evidence docs changed.
- `next_recommended`: `none`; keep 4.3 open, keep 4.4 and Unit D blocked, and do not claim C2 PASS or verification readiness.
- `risks`: authenticated acceptance, raw WinTrust values, handshake ordering, dispatch, reconnect, and separate runtime deadlines remain unproven. The managed gate remains preserved as `232/288 = 80.555556%`.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); work unit `C2-signed-native-acceptance`; exactly one signed native attempt; no production/test edits, native ledger, commit, push, PR, review, Unit D, or package action.
- `rollback_boundary`: revert only this current C2 native settlement and the corresponding task/progress documentary additions; preserve historical C2 evidence, task 4.2 proof, A/B/C1, production/test paths, and unrelated working-tree changes.
- `settlement_evidence`: preflight PASS, signature verification PASS, signed runtime FAIL before authenticated dispatch, cleanup PASS, task 4.3 open, task 4.4 and Unit D blocked.

## Historical Result Contract — C2-atomic-listener-ownership (superseded)

- `evidence_revision`: `sha256:a565373ff7def34336fe6ab107a5a4b7c96ce70ab23fb39626b3e7f9095de8dd` (SHA-256 of canonical UTF-8 manifest bytes with this self-referential line omitted).
- `status`: `passed`; C2 remains blocked by managed coverage remeasurement and native acceptance.
- `executive_summary`: replaced parallel listener fields with immutable three-part ownership and identity-based atomic detachment; publication after Dispose is rejected and locally cleaned.
- `ownership_invariants`: all disposed checks, transitions, publication, expected-owner comparison, and detachment use `ipcSync`; cancellation, stop, await, process, and disposal work never runs under the lock. Only the successful detacher cleans, no detached ownership is republished, and cleanup skips self-await.
- `interleaving_proof`: Dispose during gated StartAsync sets disposed before publication, so no process creation occurs and the candidate is cancelled/stopped/disposed once. Dispose during process creation wins the post-call ownership check, so launch returns false and cleanup is exactly once. Fault and Dispose race on identity detachment, leaving one claimant and one stop/dispose pair.
- `session_behavior_preserved`: original three-argument NamedPipeServer construction and all auth, naming, WinTrust, PID/SID/session, and handshake behavior remain unchanged.
- `artifacts`: `src/ControlParental.Service/AgentLauncher.cs`, `tests/ControlParental.Service.Tests/AgentLauncherLaunchSeamTests.cs`, and the four C2 evidence artifacts.
- `focused_verification`: launcher filter passed `18/18`; Service test build passed with `0` errors. Deterministic TCS-gated tests cover pre-publication Dispose, process-creation Dispose, and fault-versus-Dispose claimant races; prior immediate/late/post-return fault, launch failure, cancellation, and disposal tests remain passing.
- `coverage_disposition`: coverage was not run; prior `232/288` remains invalidated and task 4.2 plus 4.2.2–4.2.3 remain open.
- `runtime_harness`: `N/A`/invalidated; no certificate/store, signing, loopback, harness, native-ledger, or coverage activity occurred.
- `next_recommended`: recompute task 4.2 exact coverage; 4.3 remains open/blocked, with 4.4 and Unit D blocked.
- `risks`: a transient process may exist after the native creation call begins, but post-call ownership validation prevents reporting launch success and converges through process cleanup.
- `skill_resolution`: `sdd-apply`, Standard mode (`strict_tdd: false`); `exception-ok/size-exception`; no delegation, ledger, harness, commit, push, PR, review, package, or Unit D action.
- `rollback_boundary`: revert only `AgentLauncher.cs`, its launcher seam tests, and this current C2 evidence addition; preserve historical C2, A/B/C1, and unrelated working-tree changes.

## Historical Result Contract — C2-provisional-ownership-cleanup (superseded)

- `evidence_revision`: `sha256:46504dbdc50a45b6bd6ef43ced3a9242fe0b097449de31b0b82d01f53f610a2b` (canonical UTF-8 manifest bytes, normalized to LF, with this self-referential line omitted).
- `status`: `passed`; this bounded correction fixes pre-publication provisional cleanup and deterministic publication rejection; C2 remains blocked.
- `executive_summary`: synchronous CTS/channel/event/StartAsync failures now clean only resources actually created, preserving the original exception; the gated race proves Dispose precedes publication.
- `cleanup_guarantees`: provisional cleanup unsubscribes only subscribed handlers, cancels and disposes the CTS, invokes channel stop and IDisposable disposal at most once, and observes an already-returned start task when present; published ownership continues through the one-record atomic detachment protocol.
- `gated_interleaving`: StartAsync signals entry, synchronously blocks on the TCS gate before returning its Task; the launch thread is blocked there, Dispose runs, the gate releases, publication is rejected, no process is created, cancellation is observed, AgentChannel is null, and stop/dispose counts are exactly 1/1.
- `focused_verification`: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-build --filter "FullyQualifiedName~AgentLauncher" --verbosity minimal` — PASS, 19/19; Service test build — PASS, 0 errors.
- `coverage_disposition`: not run; prior `232/288` remains invalidated. Tasks 4.2, 4.2.2, and 4.2.3 remain open; 4.3 is open/blocked; 4.4 and Unit D remain blocked.
- `runtime_harness`: N/A/invalidated; no native loopback, certificate/store/signing, harness, coverage, or ledger activity.
- `artifacts`: `AgentLauncher.cs`, `AgentLauncherLaunchSeamTests.cs`, and the four C2 evidence artifacts. `skill_resolution`: `sdd-apply` and `work-unit-commits`, Standard mode (`strict_tdd: false`), paths-injected; no delegation, commit, push, PR, or review.
- `risks`: cleanup logging remains best-effort under existing policy; native acceptance and exact managed coverage remain unproven. `next_recommended`: recompute task 4.2; keep 4.3/4.4/Unit D blocked.
- `rollback_boundary`: revert only the provisional cleanup changes, gated/seam test additions, and this current four-document evidence update.

## CURRENT Result Contract — C2-post-ownership-exact-coverage

- `evidence_revision`: `sha256:cfa3db6bf8760b2bd9baf7a8d05f6572a0eccba8d34837fcd7d124dab4b1aa55` (canonical normalized-LF UTF-8 manifest bytes with this self-referential value omitted; propagated identically to the four authorized artifacts).
- `status`: `passed`; `outcome`: fresh exact managed measurement completed and the strict executable-managed gate passed.
- `executive_summary`: Focused AgentLauncher behavior-value tests added 12 mapped executable hits. The exact managed result is `433/541 = 80.036969%`, strictly above `80%`; historical `232/288 = 80.555556%` is invalidated and is not current evidence.
- `threshold_arithmetic`: Strict `>80%` of `541` requires `433` hits because `433/541 = 80.036969%`; the current `433` hits meet the threshold exactly. Any `13`-hit statement from the already-settled native diagnosis is historical arithmetic error only and does not alter this current contract.
- `candidate_identity`: baseline `90a5a2a44299d99b67aff18da2a9182bc167fc75`; production `sha256:af7340bd6af3babef79c5df644c787b82f9ccf28e124b0b5f37f2734fc24bce7`; focused tests `sha256:edbe0e4b7b4fb2c766ffd522b71b607e2ad95eafeca4a352f906e0067e3d8db8`.
- `managed_per_file`:

| Production file | Source-context | Non-executable | Executable denominator | Hit | Mapped unhit |
|---|---:|---:|---:|---:|---:|
| `Domain/IpcHandshake.cs` | 55 | 23 | 32 | 32 | 0 |
| `Domain/IpcPhaseTrace.cs` | 41 | 29 | 12 | 12 | 0 |
| `Service/AgentLauncher.cs` | 323 | 67 | 256 | 203 | 53 |
| `Service/Interop/AuthenticodeSigner.cs` | 72 | 34 | 38 | 34 | 4 |
| `Service/Interop/NamedPipeServer.cs` | 138 | 41 | 97 | 71 | 26 |
| `Service/Interop/WinTrust.cs` | 1 | 1 | 0 | 0 | 0 |
| `SessionAgent/Interop/AuthenticodeSigner.cs` | 61 | 30 | 31 | 28 | 3 |
| `SessionAgent/Interop/NamedPipeClient.cs` | 110 | 35 | 75 | 53 | 22 |
| **Total** | **801** | **260** | **541** | **433** | **108** |

- `classification`: managed `801` plus unchanged native-boundary `156` equals `957` current C2 production additions. The 260 exclusions are the prior 193 exact path+line syntax/compiler proofs plus the 67 AgentLauncher ranges recorded in the scope manifest; missing mapping alone was never used as an exclusion.
- `focused_verification`: fresh Cobertura Domain `11/11`, Service `72/72`, SessionAgent `23/23`; focused Service build `0` errors (5 existing package warnings). Aggregate branches: Domain `16/1,024` (1.56%), Service `216/3,676` (5.875%), SessionAgent `53/1,338` (3.961%). Exact changed-branch union is unavailable because Cobertura duplicates async state-machine mappings.
- `runtime_harness`: `N/A`/`invalidated` by absolute scope exclusions. No certificate/store mutation, signing/signtool, harness, named-pipe loopback, native ledger, or Unit D activity occurred; harness is invalidated, not a PASS.
- `cleanup_process_evidence`: raw Cobertura was retained through path+line intersection, table reconciliation, and canonical evidence capture, then removed. Only focused test/build processes ran; no harness process remained.
- `artifacts`: exactly these four evidence documents were updated; only the existing `AgentLauncherLaunchSeamTests.cs` was changed for behavior tests. `4.2`/`4.2.2`/`4.2.3` are complete; `4.3` remains next/open but blocked; `4.4` and Unit D remain blocked.
- `next_recommended`: `4.3` only, still blocked; do not claim C2 PASS or start Unit D. `harness_invalidated`: true. `skill_resolution`: `sdd-apply` + `work-unit-commits`, Standard mode (`strict_tdd: false`); focused tests/coverage only, no delegation, ledger, native loopback, commit, push, PR, review, or harness action.
- `rollback_boundary`: revert only the focused behavior tests and these four documentary updates; preserve all production paths, prior C2 history, A/B/C1, and unrelated working-tree changes.

## CURRENT Result Contract — C2-signed-native-acceptance rerun

- `status`: `passed`
- `executive_summary`: The disposable signed C2 harness was rerun with the correct one-argument invocation (pipe name only). Both EXEs were re-published and re-signed, and the mutually authenticated loopback completed successfully: the server received exactly one `ForegroundChanged` and sent one `Ping`; the client sent one `ForegroundChanged` and received one `Ping`.
- `observed_phases`: server `Connected,ServerPid,ProcessHandle,Path,AuthResult,ClientHelloRead,ServerHelloWrite`; client `Connected,ServerPid,ProcessHandle,Path,AuthResult,ClientHelloWrite,ServerHelloRead,Authenticated`.
- `artifact_scope`: disposable temp harness only; no repository source/test changes were required for this rerun.
- `result`: `4.3 complete`, `4.4 complete`, `Unit D unblocked/pending`.
- `cleanup_process_evidence`: the rerun terminated cleanly with no remaining disposable harness processes.
- `notes`: raw WinTrust was not emitted by the harness output and remains an evidence gap only; it did not block acceptance because signed authenticated dispatch and ordering proof completed successfully.
