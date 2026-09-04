# Apply Progress: Backend Identity Contract

## Cumulative Status

- Mode: Strict TDD (evidence reconciled from completed cycles)
- Delivery: auto-chain / feature-branch-chain
- Current work unit: Unit 6B-4 / task 6.4 pending; matrix 1/3 cells passed
- Completed: 19/23 task checkboxes
- External status: unverified; no backend, staging, JWT, TLS, or production RLS availability is asserted

## Unit 6B Compatibility Reconciliation

- Status: compatible-environment runtime and real App.UI→Service IPC evidence reconciled without new product/test edits; 6.2A, 6.2B, 6.3A, 6.3B, and parent 6.3 are complete. The declared OS matrix remains pending in 6.4.
- Current compatibility cell: Windows 10 Pro `10.0.26200` build `26200.9168`, x64. This OS build is outside the declared acceptance matrix and is not evidence for any target cell.
- Environment: Node 24.17.0, npm 11.13.0, Appium 3.6.0 via `npx`, Appium Windows Driver 6.1.1, WinAppDriver FileVersion 1.2.2009.02003 installed at `C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe`, Developer Mode enabled, active interactive console session.
- `npx --yes appium@3 driver install windows` passed without adding a project package.
- `npx --yes appium@3 driver run windows install-wad` returned exit 0; installation was subsequently verified.
- Compatibility probe: pinned Appium 3.6.0 recognized Appium Windows Driver 6.1.1; `GET /status` returned HTTP 200 with `ready=true`; W3C Windows Root session creation succeeded, window handles returned `[]`, and session deletion succeeded.
- Cleanup nuance: stopping the launcher tree left orphaned npx Appium grandchild PID 944; it was identified by exact command line and terminated specifically. Final listeners on 4723/4724 and WinAppDriver processes were 0.
- Receipt: `evidence/wns-e2e-compatibility-receipt.current.json`. `ExternalVerified=false` remains explicit; matrix status remains pending and no backend/JWT/RLS/TLS/fan-out claim is made.

## Unit 6B-4 Matrix Reconciliation

- Declared current acceptance matrix: exactly `Windows 10 22H2 x64`, `Windows 11 24H2 x64`, and `Windows 11 25H2 x64`.
- ARM64 is future/non-blocking compile intent only: unverified and unsupported for this acceptance boundary; no ARM64 runtime, packaging, compatibility, or support claim is made.
- Environment preparation truth: the Hyper-V differencing VM is `ControlParental-Win11-22H2-x64`, currently `Running`; boot and heartbeat were verified. It is not a declared matrix cell and is not acceptance evidence.
- Official Windows Update 23H2 acquisition attempt failed with `ResultCode2 Count0`; the temporary VM network attachment was detached afterward. No secrets were used or persisted.
- Preparation facts above do not satisfy 6.4 acceptance. The accepted compatible-environment 6.3 result remains preserved; 6.4 and Unit 7 remain pending, and quality gates remain required for any fixes discovered during validation.

### Win11 25H2 x64 Cell Evidence (task 6.4 remains open)

- Cell status: **PASS**, 1/3 declared matrix cells; receipt: `evidence/win11-25h2-x64-cell-receipt.json`. Win10 22H2 x64 and Win11 24H2 x64 remain pending.
- Exact OS: Windows 11 Pro, DisplayVersion `25H2`, build `26200.9168`, native `x64`; interactive console and Developer Mode were present.
- WinAppDriver-first: installed `1.2.2009.02003`, SHA-256 `ED39E7E1415C4E0AEBC4A821E39B200FF70DD6A2FEB2692214BB157051A91888`. Direct interactive startup on `127.0.0.1:4725` printed listening/Press ENTER and exited immediately; no readiness response or session. Prior attempt evidence: `C:\Users\Usuario\AppData\Local\Temp\ControlParental\winappdriver-6.4-0c584c6cd2fe41f690e244539910ebfd`.
- Qualified fallback: user-owned Appium `3.6.0` with NovaWindows2 `1.1.27` on `127.0.0.1:4731`; unchanged flow produced **19 passed, 0 failed, 0 skipped**, duration **7 s**, process exit **0**, W3C `DELETE` passed, and `ExternalVerified=false`.
- Immutable inputs: Product EXE `6ADB97F6AB64CE85FC8BBE42EC65EA02B291BA1E106D323F39BDB14C44EF378B`; E2E DLL `DDEB40FCBCE7019DC8C01653B431472DC3E02111337ED67E86634A0CD3585A9F`; runbook `59978441B4B5D37DABA2E779AEA2819117974645959049E5E591BC2BA915A467`; scenario `27A65449274101EB1FA92C956640C8093A996512057603A7B4A221BFEABDE450`.
- Evidence directory: `C:\Users\Usuario\AppData\Local\Temp\ControlParental\e2e-evidence-win11-25H2-x64-20260814-172511878`; `app-startup.log` 31504 bytes / `C2FAA66DA65E8700C4175523FE92633F9CE7440A70A651D7E591417D2BA2AAB6`; `page-source.xml` 9114 bytes / `1E2248F4417282E0D6A322D31C6D76668F0BC38D51DDF7260DDF96E5EB930415`; `registration.png` 28947 bytes / `C0E901EF29CCC3612ACF1E11E53B6772B61907DEF53D24F8658445EB3111C75D`. Redaction: **PASS**.
- Cleanup: App.UI `0`, port `4725` free, Appium `4731` untouched. No raw temp evidence was copied into the repository; receipt and hashes are the repository evidence. No backend, JWT, RLS, TLS endpoint, or WNS fan-out claim is made.

## Completed Tasks

- [x] 1.1 Added fail-closed v1 parsing and deterministic claim/error/time/replay/idempotency harness tests.
- [x] 1.2 Added bounded two-device, revocation, and RLS probes with receipts that cannot assert external verification.
- [x] 2.1 Added credential-free immutable identity phases, guarded transitions, restart restore, invalidation, and monotonic generations.
- [x] 2.2 Added one encrypted v1 credential envelope with serialized atomic writes, fail-closed protection/ACL ports, safe restart, corruption quarantine, and compatible legacy migration/deletion.
- [x] 3.1 Added the Service-owned pre-pair → pending → reconcile → definitive coordinator with exact claim binding, atomic activation, bounded single-use behavior, and restart restore.
- [x] 3.2 Made pairing a coordinator facade, preserved typed T24 mappings/`Retry-After`, removed blocking/plain-key authority, and rejected JWT `sub` fallback.
- [x] 4.1 Added definitive-generation bearer authorization, skew-aware refresh, one shared refresh per generation, stale-result rejection, 401/403 invalidation, bounded timeout/reconnect/`Retry-After` retry, jitter, and stable idempotency keys.
- [x] 4.2 Centralized retry ownership and bounded response buffering in the REST adapter; coordinator authorization/invalidation remains O(1), cancellable, non-blocking, and fail-closed.
- [x] 5.1 Added one fail-closed platform TLS policy with hostname/chain/error enforcement, online platform revocation checks, explicit validity checks, optional O(1) current/next SPKI pin rotation, malformed-config denial, and pin-safe diagnostics; wired the Service HTTP factory to it.
- [x] 5.2 Added the versioned external-acceptance manifest, deterministic local receipt harness, immutable-false receipt schema, artifact hashes, and exact signed-receipt future runbook; no external verification is claimed.
- [x] 6.1 Added credential-free WNS IPC request/result contracts, authenticated named-pipe dispatch, protected latest-intent persistence, definitive Service upsert, typed/redacted outcomes, bounded single-owner retries, idempotency, and one-shot restart reconciliation.

## TDD Cycle Evidence

| Task | Test File | Layer | Safety Net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1 | `BackendIdentityContractV1Tests.cs` | Contract/unit | N/A (new files) | Compile failed because v1 contract and exact-claim symbols did not exist | 13 focused cases passed after minimum parser/harness implementation | 22 cases covered exact claim match/mismatch and exposed a string-version `InvalidOperationException`; explicit JSON kind validation fixed it fail-closed | Parser helpers and typed outcomes retained; 22/22 passed |
| 1.2 | `BackendIdentityContractV1Harness.cs`, `BackendIdentityContractV1Tests.cs` | Contract/unit | N/A (new files) | Tests referenced missing receipt/error/RLS probe types | Two-device own-row, cross-device, and revocation paths passed | Added unknown-device, bounded-set, stable 404/410/429, timeout/cancel, duplicate/replay, and `Retry-After` paths | Extracted `ResolveProbeError`; all focused tests remained green |
| 2.1 | `BackendIdentityStateTests.cs` | Domain unit | 30 existing secret/store-scope tests passed before edits | Compile failed because `BackendIdentityState` and phase symbols did not exist | Valid lifecycle and generation tests passed in the 44-case focused run | Added five invalid edges, restore validation, definitive-ID denial, and invalidation; 48 focused cases passed | Kept the model immutable and credential-free; focused suite remained green |
| 2.2 | `SecretStoreTests.cs` | Service integration/unit | 30/30 baseline passed | Compile failed because atomic identity-store, snapshot, protection, and ACL port symbols did not exist; stale-generation and oversized-envelope tests then failed against permissive writes | Restart, encrypted envelope, ACL/protection failure, corruption, migration, concurrency, generation compare, and size bounds passed | Added cancellation, reverse-order writes, stale-generation rejection, and a 64 KiB plaintext-envelope cap; 50/50 focused cases passed | Removed the prior no-op/fail-open ACL helper, centralized atomic writes, and retained the previous committed file on pre-replace failure |
| 3.1 | `BackendIdentityCoordinatorTests.cs` | Service unit/integration | 115/115 identity/pairing/store tests passed | Compile failed because coordinator, lifecycle port, command, and typed step symbols did not exist; malformed successful steps later failed as unsafe success | 17 coordinator cases passed after malformed success became `InvalidPayload` | Happy/lost-response paths were triangulated with 404/410/429, duplicate/replay, malformed steps, missing/mismatched claim, revocation, stale generation, restart, mid-flight cancellation, and concurrent duplicate cases | Kept one async gate, one pair call, at most one reconcile call, and activation strictly after atomic commit; 17/17 remained green |
| 3.2 | `BackendIdentityAdaptersTests.cs` | Service unit | Existing PairingService/DeviceAuthenticator tests were included in the 115-test baseline | Compile failed because coordinator facade and exact-claim reader did not exist | 8 adapter cases passed | T24 success/404/410/429/replay mappings, `Retry-After`, definitive-only reads, malformed JWT, missing claim, and `sub` rejection passed | Removed plain-key/blocking pairing authority and legacy retry/policy ownership; 25/25 Unit 3 tests remained green |
| 4.1 | `AuthenticatedBackendClientTests.cs`, `BackendIdentityCoordinatorTests.cs` | Service unit/integration | 67/67 backend-client/coordinator tests passed | Compile failed because definitive authorization, reliability options, invalidation, and refresh-timeout seams did not exist | 29 focused cases passed after single-flight authorization and authenticated send were added | 401/403, expiry/skew, stale generation, cancellation, timeout, reconnect, `Retry-After`, jitter, idempotency, and persistent-failure bounds produced 31 passing focused cases | One send/retry owner and one generation-keyed refresh task remained; focused tests stayed green |
| 4.2 | Same plus existing `BackendClient*Tests.cs` and `SecretStoreTests.cs` | Service unit/integration | Included in the 67-test baseline | Existing split request construction could not use coordinator authorization or bounded replay | All REST methods used the centralized adapter while legacy composition remained available for Unit 6 | Definitive denial, waiter cancellation, stale completion, sanitized network failure, and bounded retries covered distinct branches | Response buffering, upsert creation, delay policy, and invalidation were centralized; 119/119 safety tests passed |
| 5.1 | `CertificatePinningPolicyTests.cs`, `CertificatePinningValidatorTests.cs` | Service security/unit | 10/10 existing validator tests passed | Compile failed because the policy lacked deterministic validity time and rotating-pin support; the existing mismatch exception also disclosed pin material | 20/20 focused cases passed after platform trust errors, revocation configuration, validity, pin lookup, redaction, and shared Program transport were implemented | Added malformed/hash-length/over-capacity pin configuration and non-HTTPS denial; 22/22 focused cases passed | Centralized all Service HTTP TLS construction in `CertificatePinningPolicy`; final focused suite and build remained green |
| 6.1 (composition baseline) | `ProductionBackendIdentityCompositionTests.cs`, `BackendIdentityLifecycleClientTests.cs`, `ScheduledWorkServiceIdentityTests.cs` | Real composition/integration | 121/121 composition, scheduler, identity, and authenticated-client tests passed before edits | Compile failed on the missing production composition method/coordinator scheduler dependency; lifecycle tests then failed on forbidden-only transport behavior | Real host startup restored generation 17 before remote hosted work; definitive-only DI, contract lifecycle, and scheduler identity tests passed | Added pre-pair → pair → definitive claim, unpaired denial, 401 revocation, 403 denial, restored startup, and unpaired/definitive scheduling branches; 95/95 Unit 6 focused tests passed | Centralized production registrations, one startup restore service, bounded lifecycle parsing, and definitive policy identity; UI cutover remains task 6.2 |
| 5.2 | `BackendIdentityAcceptancePackageTests.cs`, `BackendIdentityContractV1Harness.cs` | Contract/evidence | Existing 22-case v1 harness suite passed | Compile failed because `RunLocalAcceptance` and receipt type did not exist; manifest/schema files were absent | Deterministic local receipts and package shape/hash checks passed | Repeated harness executions matched; schema `const: false`, secret scan, artifact hashes, future signing/reproducibility gates, and exact cleanup were added | Kept local and future external receipt authorities structurally separate; `ExternalVerified=false` remains immutable |
| 6.1 | `UIMessageHandlerWnsTests.cs` | Domain/Service/IPC | 1047/1047 Service tests passed after the production slice; Units 1–5 remained green | Focused build failed on missing WNS IPC/coordinator/store contracts | Valid authenticated IPC persisted intent and reached definitive `BackendClient`; typed/redacted result passed | Invalid URI/channel/expiry, unpaired/stale/revoked, finite timeout/cancel, duplicate/conflict, stable idempotency retry, restart, oversize, redaction, and unauthorized IPC reached 21/21 focused green | One O(1) latest intent, one async gate, one backend retry owner, one startup reconcile, source-generated single-parse IPC, and no UI dependency remained |
| 6.2 (blocked) | Existing `ControlParental.App.UI.Tests` suite | UI contract/regression | 146/146 current UI tests passed | No UI production/test file was edited by Unit 6 | Existing observable UI contracts remain green | Static inspection found `WnsPushNotificationHandler.RegisterChannelAsync` sends the publishable key as bearer directly to `device_push_tokens` and logs response bodies | Per the UI gate, no UI edit was made; moving this caller behind Service IPC changes production behavior and requires real Appium/WinAppDriver E2E |

## Unit 6B Strict TDD and Verification Evidence

| Task | Test File | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|
| 6.2A | `WnsLifecycleTests.cs` | Handler tests intentionally failed on direct Supabase construction and missing typed IPC seam | Focused 3/3 passed | WnsLifecycle full 15/15, then TRIANGULATE 19/19 | Caller cancellation propagates; internal timeout remains null fail-closed |
| 6.2B | NamedPipe/catalogue transport tests | Deterministic seam and compatibility catalogue coverage were missing/incomplete | NamedPipe deterministic suite 9/9, including real in-process unique named-pipe E2E in 64 ms | Canonical markers/context and active source-generated catalogue verified | One typed `QueryAsync` per intent; cancellation/timeout/fail-closed behavior remains bounded |
| 6.3A1 | `WnsRegistrationViewModelTests.cs` | 4 focused tests failed because `IWnsRegistrationPort`, `WnsRegistrationViewModel`, and `WnsRegistrationDisplayStatus` were missing | Dedicated safe registration port/view-model implemented; `WnsPushNotificationHandler` implements the port through existing typed IPC; focused suite 7/7 passed in 27 ms | A1-scoped full suite excluding only intentionally RED pending A2 `WnsRegistrationPageCompositionTests`: 170/170 passed, 0 skipped, 1 s; changed/new view-model executable coverage 40/45 = 88.89%, gate >80% passed | Typed statuses only; null unavailable is fail-closed; busy flag clears in `finally`; cancellation propagates; one invocation; no URI/operation/correlation/backend/body/bearer/key/credential disclosure; O(1), no retries/polling |

- Full App.UI suite: 163/163 passed, 0 skipped, duration 868 ms.
- Final changed-scope executable line coverage: 62/71 = 87.32%; `WnsPushNotificationHandler` 23/31 = 74.19%; `NamedPipeUIChannel` 39/40 = 97.5%. Interface/metadata declarations have zero executable sequence points; aggregate gate >80% passes. Coverage methodology/report: final App.UI test-results coverage report from the completed full-suite run, with changed executable sequence points measured per production type and declaration-only points excluded.
- Security/architecture: App.UI direct WNS HTTP/Supabase bearer/publishable key/body/URI logging removed; Service remains authority; one typed `QueryAsync` per intent; O(1), no retries/polling/N+1. Domain plus explicitly registered active App.UI source-generated catalogues are covered. `ExternalVerified=false` remains unchanged; backend JWT/RLS acceptance is deferred. Root Appium compatibility is separate evidence only; actual App.UI E2E remains 6.3–6.4.

- Focused tests: 22 passed, 0 failed, 0 skipped; external timeout 180 seconds.
- Solution build: succeeded with 0 errors and 7 pre-existing package-resolution warnings.
- Changed production scope coverage: 134/144 lines = 93.06%; 97/120 branches = 80.83%.
- Coverage receipt: `tests/ControlParental.Service.Tests/TestResults/BackendIdentityContractV1Final5/efa965ba-4446-48a4-833a-18fb9302b417/coverage.cobertura.xml`.
- Security negative: exact claim mismatch, malformed/unknown schema and version, missing accepted identity, unknown status, oversized payload, hostile cross-device request, revocation, timeout/cancel, duplicate/replay, and bounded-capacity denial are covered.
- Secret safety: fixtures use symbolic cases and non-secret operation/device labels; no tokens, pairing codes, parent identifiers, credentials, response bodies, or logs were added.
- Unit 2 focused tests: 50 passed, 0 failed, 0 skipped; 20 new executable cases over a 30-test safety net; timeout 180 seconds.
- Unit 2 solution build: succeeded with 0 errors; the dirty solution still emits its broad analyzer/package warning baseline, including documentation/style findings on new public contracts.
- Unit 2 changed production scope: 210/254 executable lines = 82.68%; 79/100 branches = 79.00%.
- Unit 2 coverage receipt: `tests/ControlParental.Service.Tests/TestResults/BackendIdentityUnit2Final4/86875b83-6b08-4d4f-967c-578db3a6082d/coverage.cobertura.xml`.
- Unit 2 negative/security paths: invalid transitions, missing definitive identity, protection/ACL failure, corruption, partial/conflicting migration, cancellation, and concurrent-write overlap.
- `git diff --check` passed for the allowed roots; only line-ending notices were emitted. App.UI was not changed.
- Unit 3 focused safety suite: 127 passed, 0 failed, 0 skipped; Unit 3-only suite: 25 passed; every command had a 180-second timeout.
- Unit 3 solution build: succeeded with 0 errors and the dirty solution's pre-existing analyzer/package warning baseline.
- Unit 3 coverage receipt: `tests/ControlParental.Service.Tests/TestResults/BackendIdentityUnit3Final4/edf10ad3-29cf-49dd-b987-b8f91b041316/coverage.cobertura.xml`; changed methods exceeded 80% lines (`PairCoreAsync` 93.61%, exact-claim reader 84.37%, pairing facade 83.33%). Branches: 91.66%, 90%, and 66.66% respectively.
- Unit 3 security/concurrency negatives cover no-claim/mismatch, revocation, stale commit, replay/duplicate, throttling, uncertain timeout reconciliation, mid-flight cancellation, restart, and concurrent single-flight. No sensitive logging was added.
- Unit 4 focused tests: 32 passed; expanded backend-client/coordinator/store safety suite: 119 passed; every command had a 180-second external timeout.
- Unit 4 solution build succeeded with 0 errors and the dirty solution's analyzer/package warning baseline.
- Unit 4 coverage receipt: `tests/ControlParental.Service.Tests/TestResults/BackendIdentityUnit4Final4/59f47df1-04ff-4d48-8276-d7ea119e488d/coverage.cobertura.xml`. Changed async methods: authenticated send 80.30% lines/75% branches; authorize 90.90%/75%; invalidate 88.23%/50%; refresh 80.39%/66.66%; persisted invalidation 85.71%/83.33%.
- Unit 4 negatives cover non-definitive denial, 401/403, expiry/skew, stale refresh, waiter cancellation, request/refresh timeout, reconnect exhaustion without exception disclosure, and retry bounds. No bodies, auth headers, tokens, secrets, or sensitive URLs are logged.
- Unit 5 TLS safety baseline: 10 passed before edits. RED compile failure named only missing constructor behavior; no credentials or pins were emitted.
- Unit 5 focused TLS/security suite: 22 passed, 0 failed, 0 skipped with a 180-second external timeout.
- Unit 5 solution build succeeded with 0 errors; the dirty solution retained its pre-existing analyzer/package warning baseline.
- Unit 5 coverage receipt: `tests/ControlParental.Service.Tests/TestResults/BackendIdentityUnit5Final/88dee412-fe0f-4dee-9f07-1593540c299d/coverage.cobertura.xml`. `CertificatePinningPolicy` reached 93.44% lines and 96.42% branches; changed validator redaction was executed. Program composition was compile-verified.
- Unit 5 security negatives cover hostname mismatch, chain errors, expiry/not-yet-valid, absent certificate, pin mismatch, malformed/oversized rotation configuration, non-HTTPS input, and platform revocation enablement. Valid current/next overlap and no-pin platform-trust behavior are positive controls.
- `git diff --check` passed for all allowed roots; only line-ending notices were emitted. No UI, Unit 6, or Unit 7 files were advanced.
- Unit 6 pre-edit safety net: 121 passed, 0 failed. RED was observed as missing composition/scheduler APIs and forbidden-only lifecycle behavior.
- Unit 6 focused composition/lifecycle/scheduling coverage suite: 95 passed, 0 failed, 0 skipped; finite 180-second timeout.
- Unit 6 local Units 1–5 regression plus handler/scheduler suite: 243 passed, 0 failed, 0 skipped; task 5.2 remains external and no receipt was invented.
- App.UI regression suite: 146 passed, 0 failed, 0 skipped. Unit 6 did not edit App.UI or App.UI.Tests.
- 6.3A1 reconciliation: strict RED was the four focused missing-symbol failures above; GREEN was 7/7 focused tests in 27 ms. The 170/170 full-suite result intentionally excludes only the pending A2 `WnsRegistrationPageCompositionTests`, which remain RED 5/5 because XAML/DI/navigation are not implemented; this is not an A1 regression and A2 is not marked complete.
- 6.3A1 security/architecture: the view-model exposes only typed display statuses, maps unknown/null outcomes to unavailable, propagates caller cancellation, prevents overlap with one in-flight invocation, and does not expose URI, operation/correlation data, backend details, bodies, bearer material, keys, or credentials.
- Solution build succeeded with 0 errors and the dirty solution's existing warning baseline.
- Unit 6 coverage receipt: `tests/ControlParental.Service.Tests/TestResults/BackendIdentityUnit6Coverage/ec0950db-1296-4c95-b8c4-85db8453bba7/coverage.cobertura.xml`. Changed production methods/classes: lifecycle client 86.11% lines/42.42% branches, startup service 100%/100%, production composition method 100%/50%, policy-sync state machine 92.59%/94.44%.
- Static scan removed productive `"default"`, `sub` substitution, public legacy backend pairing, and production `IDeviceAuthenticator` registration. The obsolete `BackendClient(IDeviceAuthenticator)` and legacy `PairAsync(PairingRequest)` are internal-only compatibility seams for the existing test assembly and are unreachable from production DI.
- UI gate blocker: `App.UI/WnsPushNotificationHandler.RegisterChannelAsync` directly posts to Supabase with the publishable key as bearer and logs backend response bodies. Correcting that caller requires a new Service IPC path and therefore real Appium/WinAppDriver E2E; Unit 6 stopped before touching UI.
- Unit 6A focused WNS suite: 21 passed, 0 failed, 0 skipped; finite 180-second outer timeout. It includes real `BackendClient` timeout and transient retry/idempotency paths.
- Unit 6A contract/composition coverage suite: 57 passed before final timeout triangulation; the final focused suite remained green.
- Unit 6A Service regression: 1047 passed, 0 failed, 0 skipped in 10 seconds; Units 1–5 safety remained green.
- Unit 6A solution build: succeeded with 0 errors and the dirty tree's existing analyzer/package warning baseline.
- Unit 6A coverage receipt: `tests/ControlParental.Service.Tests/TestResults/BackendIdentityUnit6AFinal/e899900e-7c86-4f74-a7ee-0fb3f9d9cb7f/coverage.cobertura.xml`. WNS coordinator file reached 94.17% lines; core coordinator 100% lines/78% branches, register 90.90%/91.66%, send/reconcile/startup 100%/100%. New IPC wrappers and typed results reached 83.33–100% lines.
- Security scan found no Service publishable bearer, productive `"default"`, or error-body extraction pattern. WNS outcomes expose only operation/correlation identifiers and typed status; channel URI and backend errors are never returned or logged.
- `git diff --check` passed for all allowed roots with line-ending notices only. No Unit 6A edit touched App.UI or App.UI.Tests.

## Complexity and Architecture

- JSON parsing is single-pass O(payload bytes), with a 16 KiB input cap and no repeated deserialization.
- Harness operation storage is capped at 8 entries; idempotency and replay use O(1) dictionary/hash-set lookups.
- RLS probes are fixed to two fixture identities and use O(1) set membership; over-capacity fixture input fails closed.
- No network calls, polling, retries, blocking waits, scans, N+1 work, secret storage, HTTP composition, or UI dependencies were introduced.
- Contract and harness remain in Service/test infrastructure; no HTTP or secrets entered Domain, and App.UI was not modified.
- Unit 2 state transitions and identity lookup are O(1). The immutable state is one bounded record with no scans or cache.
- Identity JSON and DPAPI are O(envelope bytes); the envelope is one snapshot capped at 64 KiB plaintext/72 KiB protected and is deserialized once per read.
- Migration examines exactly six legacy keys, O(1) in key count and O(total protected bytes); compatible evidence must be complete and device IDs must agree.
- A per-store `SemaphoreSlim` serializes writes asynchronously. Temp-write, durable flush, ACL application, then same-directory replace prevents overlapping writes and preserves the prior valid file on failure.
- No polling, retry layer, N+1 calls, blocking task waits, repeated deserialization, network access, pairing/JWT activation, UI dependency, or unbounded collection was introduced.
- DPAPI LocalMachine and Windows SYSTEM/Administrators ACL are production adapters. Unit 2 proves their replaceable failure boundary and fail-closed behavior; it does not claim a service-account ACL deployment receipt.
- Unit 3 state/operation lookup and single-flight are O(1). Pairing performs one submit plus at most one reconciliation; no code replay, polling, scan, blocking wait, overlapping request, or policy/REST call exists.
- JWT claim parsing is one O(token bytes) pass in `DeviceAuthenticator`; exact binding is ordinal and `sub` is never substituted. Definitive state is assigned only after the Unit 2 atomic store accepts the next generation.
- Unit 4 generation/state/session lookup and single-flight are O(1). Refresh is one network operation per generation; waiters share its task, stale commits cannot advance authority, and invalidation quarantines only the matching persisted generation.
- REST request/response work is O(payload bytes), responses are capped at 64 KiB, batches remain bounded O(n), and retries are O(k) with `1 <= k <= 3`, a 30-second delay cap, one timeout per attempt, and no polling, blocking waits, repeated parsing, stacked retries, or unsafe replay.
- Unit 5 computes one SPKI SHA-256 in O(certificate bytes), then performs O(1) `HashSet` pin lookup. Configuration is bounded to at most two overlapping pins and parsed once per handler; certificate checks perform no scans, network retries, polling, blocking waits, or fallback callbacks.
- Hostname, chain, and revocation remain owned by the Windows/.NET platform validator. Pins only add a check after `SslPolicyErrors.None`; they can never bypass chain, hostname, expiry, or revocation failures. Offline enforcement paths were not changed.
- Unit 6 production identity restore/state lookup is O(1); hosted startup reads one bounded identity envelope before remote hosted work. Policy sync reads the current immutable state once and performs no backend call while unpaired.
- The lifecycle adapter caches at most one pre-pair session in O(1), submits pairing once, performs one definitive refresh, parses each bounded 16 KiB response once in O(payload bytes), and has no polling, N+1, blocking waits, duplicate refresh, or retry layer. Reconciliation remains fail-closed pending the externally approved route.
- Unit 6A stores exactly one protected latest WNS intent and one cached result, so identity/idempotency and supersession are O(1) with bounded state. URI/JSON work is O(bytes), IPC deserializes from one parsed document, and retries remain bounded O(k) solely in `BackendClient`.
- Restart reconciliation runs once as an ordered hosted service after identity restore. There is no polling, blocking wait, scan, N+1 access, repeated parse, retry stacking, or overlapping registration.

## External Gates

- Routes, issuer/audience, definitive JWT issuance, backend pairing semantics, indexes, and production RLS remain pending external-owner approval and executable backend/staging receipts. Local TLS policy behavior is proven, but no external server TLS receipt is claimed.
- Local receipts have `ExternalVerified == false` by construction and cannot be promoted by callers. The manifest binds harness/schema hashes and requires separately signed, reproducible external receipts.
- Live JWT/RLS/TLS endpoint/WNS fan-out execution remains exclusively deferred to `backend-integration-acceptance`; no staging values or secrets were used.
- SDD6/SDD7 remain contract-first only; Unit 6A does not claim production backend readiness.

## Workload Boundary

- Unit 1 authored additions: 599 lines (543 implementation/tests, 56 OpenSpec progress/task additions); 2 task lines replaced.
- Unit 2 authored code/test additions before this cumulative progress update: 715 lines; the hard 800-authored-line limit is satisfied.
- Unit 2 exceeds the 400 changed-line review objective when tests remain coupled to implementation. Preserve the Unit 2 boundary, or review it as two immediate child slices: state model/tests, then atomic store/migration/tests.
- If the tracker enforces the 400-line target strictly, preserve TDD coupling by splitting Unit 1 into two child reviews: typed envelope/parser with its tests, then bounded deterministic harness/RLS probes with their tests. No commits or PRs were created here.
- Unit 3 authored approximately 733 code/test lines before this progress update, below the 800-line cap. Removing 325 lines of superseded legacy pairing tests and replacing the 202-line split-authority service pushes review change above the 400-line objective; keep coordinator/tests and facade cleanup as immediate child review slices if required.
- Unit 4 authored approximately 790 code/test lines, below the 800-line cap, and preserves the autonomous PR4 boundary. Production composition intentionally remains for Unit 6; no TLS/RLS, Program wiring, ScheduledWorkService, handler, or UI work was advanced.
- Unit 5 task 5.1 authored approximately 230 implementation/test lines, below the 800-line cap. It preserves the PR5 boundary; live external execution remains deferred and no mock receipt was substituted.
- Unit 6 authored approximately 650 implementation/test/progress lines, below the hard 800-line cap but above the 400 changed-line review objective. Preserve it as the PR6 local composition slice; the UI remediation/E2E work is not included and must be a separately approved boundary.
- Unit 6A adds approximately 790 implementation/test/evidence lines before cumulative tracking, within the 800 authored-line cap and above the 400 review objective. Review as immediate child slices: acceptance package, then Service WNS IPC with coupled tests; both remain under the feature-branch-chain PR6A boundary.

## Remaining Tasks

- [x] 6.3A2 / PR6B-3A2 is complete from structural RED/GREEN/REFACTOR evidence: dedicated XAML, stable AutomationIds, typed safe bindings, code-behind cancellation, DI registration, and deterministic MainWindow PageHost navigation.
- [x] 6.3B is accepted for the compatible environment: latest live run 19 passed, 0 failed, 0 skipped with real App.UI→Service IPC, safe typed states/lifecycle, W3C DELETE, and App.UI0. The declared OS matrix remains pending for 6.4.

- [x] 6.2A and 6.2B are complete from the recorded strict-TDD, deterministic transport, coverage, security, and architecture evidence. T24 remains frozen and unchanged; its mappings are retained through `PairingService`/`UIMessageHandler` and absorption is deferred to SDD5 verify.
- [x] 6.2 parent is complete; actual App.UI E2E is intentionally not included and remains exclusively in 6.3–6.4.
- [x] 6.3 is complete because 6.3A and 6.3B acceptance evidence are complete.
- [ ] 6.4 remains pending for the declared Windows 10 22H2 x64, Windows 11 24H2 x64, and Windows 11 25H2 x64 matrix and receipts; one cell (Win11 25H2 x64) is now passed.
- [ ] 7.1–7.3 remain pending. Unit 7 verification follows Unit 6B; live external acceptance remains a separate future change.

## Current Blocker / Next Action

- Exact authoritative task count: 23 total, 19 complete, 4 pending (`6.4`, `7.1`, `7.2`, `7.3`).
- First pending Unit 6B task: `6.4`, the unchanged declared-matrix execution and receipt follow-up.
- 6.3A and 6.3B acceptance hierarchy is mechanically complete. Historical blocked runs remain recorded below; the final compatible-environment result does not claim independent OS exit observation or external integration.
- Required next action: complete 6.4 and then Unit 7 verification. Keep `ExternalVerified=false`; backend acceptance remains deferred.

## User-Approved Pause State

- Status: **paused-by-user**; pending prerequisites remain for matrix 1/3 completion. No task checkbox was changed: 6.4 and 7.1–7.3 remain unchecked; the authoritative count remains 19/23 complete.
- Win11 25H2 x64: **PASS remains valid** and is the only current scoped functionality evidence.
- Win11 24H2 x64: **blocked** because official media/channel is unavailable.
- Win10 22H2 x64: **blocked** because no licensed test environment is available.
- Unit 7 was deliberately not started or completed because it follows 6.4. Leaving these tasks open has no runtime or product-code impact.
- This pause makes no client-ready, full-matrix-support, verify-PASS, archive, backend/JWT/RLS/TLS/fan-out, or `ExternalVerified` claim. `ExternalVerified=false` remains unchanged.
- Resume when official Win11 24H2 media/channel and a licensed Win10 environment are available, or after a future explicit formal scope amendment.

## 6.3B SCM Startup Blocker Reconciliation

- NativeAOT EF Core blocker: EF Core 9.0.0 compiled-model generation was run with the official `dotnet ef dbcontext optimize` workflow after adding the required design-time package/factory. The generated `CompiledModels/` artifacts are committed to the working tree and Service production composition calls `UseModel(ControlParentalDbContextModel.Instance)`.
- AclHardener blocker: added a narrow injectable registry opener and a `SecurityException` boundary catch. The catch emits only a sanitized diagnostic and returns `false`; ACL success is never claimed.
- Strict TDD RED: `NativeAotCompiledModelTests` and `HardenRegistryKeyAsync_WhenRegistryBoundaryThrowsSecurityException` initially failed to compile because the production seams/generated model did not exist.
- Strict TDD GREEN: focused `AclHardenerTests|NativeAotCompiledModelTests` passed 6/6 in 302 ms; relevant Service composition/hardening suite passed 23/23.
- Refactor: generated metadata remains tool-produced; the assembly discovery attribute was removed after it caused non-AOT test contexts to consume an uninitialized relational model. Production `UseModel` remains narrow to Windows Production composition.
- Exact `Build-ServiceInstaller.ps1` Release/win-x64: passed and staged both binaries. Documented elevated installer: passed; receipt is `Installed`, SCM `RUNNING`, Authenticode `NotSigned`.
- Finite pipe probe: `ControlParental.UI` connected successfully; final machine state is service `RUNNING`, pipe ready.
- Full Service suite: 1051/1059 passed, 8 failed. Failures are existing/changed-scope named-pipe test assumptions (`MissingMethodException`/registration source assertion), not NativeAOT or ACL startup failures; this prevents full-suite acceptance.
- Dedicated 6.3B E2E: not executed. The unchanged runbook stopped at the external Appium 3.6.0 / Windows Driver 6.1.1 status gate because no Appium listener was running. No E2E acceptance is claimed.

### 6.3B Follow-up: Service Regression Triage and Appium Attempt

- The original 8 full-Service failures were classified as stale test setup, not production regressions: six `NamedPipeUIServerDeserializeTests` used the pre-seam six-argument private listener constructor; one `HostRegistrationTests` source assertion searched for the removed parameterless registration form; and one `NamedPipeUIServerHostedAdapterTests` asserted `IsReady == false` after the injected backoff had already completed synchronously. Tests were adapted to the current constructor/DI/lifecycle contracts without weakening production behavior.
- The full Service suite then exposed one additional stale timing assumption in `SessionManagerLifecycleTests.WatcherCancellationBoundsProviderFailureShutdown`; its fixed-delay observation was replaced with a deterministic provider-called TCS. Final full Service result: **1059/1059 passed, 0 failed, 0 skipped**, bounded by 180 seconds.
- Appium was started by this run using `npx --yes appium@3.6.0 --base-path /`; Windows driver `6.1.1` loaded and `/status` returned ready. The unchanged E2E was invoked with `E2E_REQUIRED=1`, exact Appium URL and evidence directory, and the generated `msix_staging/ControlParental.App.UI.exe`.
- Appium created a W3C session and spawned WinAppDriver on 4724, but the E2E request to create the session timed out after 10 seconds while WinAppDriver was starting/serving the downstream session. Appium logs show the downstream WinAppDriver session returned HTTP 200, then no subsequent Appium command completed before the test timeout. This is an environment/driver startup failure, not a proven product assertion failure; no E2E acceptance is claimed.
- Appium launcher ownership was cleaned up. Final machine state: `ControlParental` remains `RUNNING`; finite `ControlParental.UI` pipe probe remains ready; no owned Appium/UI process is retained. MSIX build completed successfully at `ControlParental.App.msix` (unsigned development artifact).

### 6.3B Follow-up: Initial UI Readiness Contract

- Added strict harness RED/GREEN coverage for a reusable bounded initial AutomationId wait. The deterministic tests prove recovery when lookup succeeds after injected attempts and finite actionable timeout when the AutomationId remains absent; focused result: **3/3 passed**.
- The E2E now waits for `WnsRegistrationNavigationButton` before clicking, preserving accessibility-id lookup, 10-second command timeout, 30-second session-start timeout, and no sleeps/infinite retry/fallback locator.
- Final bounded E2E rerun: session creation completed in about 10.055 seconds, but every `POST /element` request for `WnsRegistrationNavigationButton` hung in WinAppDriver until the normal 10-second command timeout. Appium log confirms the exact accessibility-id locator and correct W3C session; no `no such element` response was returned. The bounded wait therefore ended with `TimeoutException` naming the AutomationId and 10-second deadline.
- Diagnostic session confirmed window handle `0x006804AE`, title `WinUI Desktop`, requested executable PID 5476, matching SHA-256 across `msix_staging` and all current Debug/Release outputs. UI source and screenshot requests were also attempted; source timed out and screenshot was captured to `%TEMP%/ControlParental/e2e-diagnostic` without persisting sensitive source.
- This remaining blocker is external WinAppDriver/UI Automation behavior on the current environment (the driver hangs on both source/element commands after a successful session), not a proven App.UI composition defect. No product UI change was made. `ControlParental` remains `RUNNING`; `ControlParental.UI` remains ready; owned Appium/UI processes were cleaned.

## Unit 6B-3A2 Strict TDD and Verification Evidence

| Task | Test File | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|
| 6.3A2 | `WnsRegistrationPageCompositionTests.cs`, `NamedPipeUIServerHostedAdapterTests.cs` | ✅ Composition contract 5/5 passed; new listener constant test failed against literal `255` | ✅ Focused composition 5/5 and Service listener/host set 22/22 after replacing `255` with `NamedPipeServerStream.MaxAllowedServerInstances` | ✅ Safe bindings, DI/no backend construction, deterministic PageHost navigation, listener readiness/backoff, and host lifecycle | ✅ Surgical constant replacement; protocol/auth/security unchanged |

### Bounded command evidence

- App.UI composition: bounded 30s test; 5/5 passed.
- Service listener/host: bounded 30s pre-change set; 27/27 passed. RED rebuild failed because source still contained literal `255`.
- GREEN/refactor: bounded 60s focused test; 22/22 passed.
- Solution build: bounded 60s `dotnet build ControlParental.sln --no-restore`; 0 errors, existing warning baseline.
- Dedicated App.UI E2E: explicit opt-in run bounded 45s; preflight passed Appium status but failed honestly with `E2E BLOCKED: ControlParental.Service IPC is unavailable: named pipe ControlParental.UI is not ready.`
- Preflight: Node 24.17.0, npm 11.13.0, Appium 3.6.0, Windows Driver 6.1.1, WinAppDriver installed, interactive desktop true, App.UI executable and Service executable present.
- Tracked Service probe: PID 9480 started and logged `Application started` plus WNS `invalid_client`; 20 named-pipe probes over 10 seconds failed. Owned process was terminated and 4723/4724 had no listeners. This is an environment/launch-readiness blocker, not proven product behavior defect.
- Diagnosis: Program registers `NamedPipeUIServer` and `NamedPipeUIServerHostedAdapter`; the adapter starts the listener on a background task, and listener failures would be logged. The bounded Service logs contain neither adapter nor listener error, while the runbook only probes `ControlParental.UI` and never starts a Service process. The first proven blocker is launch-path ownership/configuration, not a readiness-probe defect. Focused `StartAsync_ExposesUiPipeBeforeClientConnects` passed 1/1 after adding the missing `System.IO.Pipes` import.
- SCM diagnosis: read-only `sc query/qc ControlParental` returned ERROR 1060 (service not installed); `Win32_Service` returned null; shell elevation is true. Repository installer convention is `Build-ServiceInstaller.ps1` followed by elevated `Install-ControlParentalService.ps1`, default service name `ControlParental`, install path `%ProgramFiles%\ControlParental\Agent`, and binary `ControlParental.Service.exe`. No installation was attempted because the staged bundle was absent and `Build-ServiceInstaller.ps1 -SkipPublish` failed at its explicit payload validation gate.
- Real publish attempt: exact documented `Build-ServiceInstaller.ps1` defaults (Release/win-x64) was bounded at 300 seconds and failed during Service `dotnet publish`; compilation completed, then NativeAOT stopped with `PublishTrimmed is implied by native compilation and cannot be disabled`. Expected staged Service/Agent binaries were absent; installer, scans, SCM, pipe polling, and E2E did not run.

### 6.3B Follow-up: Aggregate Timeout and Clean NovaWindows2 Rerun

- The prior 30-second runbook failure was an aggregate-budget contradiction, not the first driver/UIA blocker. `CreateSessionAsync` has a 30-second HTTP budget; the initial AutomationId wait can consume 10 seconds with each lookup using the 10-second `HttpClient` command timeout; subsequent element waits, lifecycle polling, evidence source/screenshot calls, and best-effort DELETE can each consume additional finite budgets. The runbook's 30-second `WaitForExit` could therefore expire while the testhost was still executing a legitimate bounded scenario. Timestamped prior Appium evidence showed session creation completed at `23:54:32.555`, the first element request began at `23:54:32.564`, timed out at `23:54:42.571`, and DELETE remained pending until `23:55:23.178`.
- Added one deterministic timeout-contract test asserting the runbook aggregate envelope is 180 seconds with a 150-second child-process budget, then updated only those aggregate budgets. Individual session, command, readiness, polling, evidence, and security assertions remain unchanged and finite. Focused timeout contract result: **6/6 passed**.
- One fresh clean same-process-chain rerun used a new approved temp home, Appium 3.6.0, `novawindows2@1.1.27`, and port 4753. The first attempt on port 4752 exposed that the runbook did not propagate `CONTROL_PARENTAL_E2E_AUTOMATION_NAME=NovaWindows2`; the runbook now sets that explicit documented contingency before launching the child. The corrected rerun reached NovaWindows2 session creation (session HTTP 200 after 4.969s), then the first accessibility-id lookup for `WnsRegistrationNavigationButton` timed out at 10 seconds, reproducing the driver/UIA hang with one invocation-owned window. No functional acceptance was claimed; no `appTopLevelWindow` attach was attempted because the failure is in NovaWindows2 element lookup, not session foreground selection.
- Invocation-owned Appium and App.UI processes were cleaned. Final state: no App.UI remains, port 4752 is free, `ControlParental` remains RUNNING, `ControlParental.UI` remains ready, and PID 17192 remains untouched.

### 6.3B Final Follow-up: Explicit HWND Attach

- NovaWindows2 `1.1.27` documentation/source confirms `appTopLevelWindow` attaches an existing top-level window and accepts a decimal or string handle (`12345` or `0x12345`). Driver source normalizes numeric values to strings, rejects simultaneous `app` + `appTopLevelWindow`, and scopes the PowerShell root to `Number(appTopLevelWindow)`; it does not launch or own the manually started application. The harness now exposes `CONTROL_PARENTAL_E2E_TOP_LEVEL_WINDOW`: when set to a positive decimal/hex handle it emits only `appium:appTopLevelWindow` and `shouldTerminateApp=false`; otherwise it preserves the existing `appium:app` launch mode and `shouldTerminateApp=true`.
- Strict TDD boundary evidence: initial RED failed because the new test used unprefixed capability keys; GREEN corrected assertions to W3C `appium:` keys. Final focused contract result: **10/10 passed**, including default launch preservation, attach replacement, positive decimal/hex validation, aggregate timeout, driver selection, and bounded readiness.
- Manual attach proof: before Appium, no App.UI existed. Manually launched exact `msix_staging\ControlParental.App.UI.exe`, PID `18664`, start/path verified, title `WinUI Desktop`, stable HWND decimal `4654512` (hex value was also validated in the preceding manual probe as a supported representation). No arbitrary foreground API was used.
- One fresh isolated attach chain used Appium `3.6.0`, NovaWindows2 `1.1.27`, new APPIUM_HOME, port `4755`, and `/status` ready. W3C session sent only `appium:appTopLevelWindow=4654512`, `shouldTerminateApp=false`; Nova log explicitly recorded `Setting root element to window handle: 4654512`, session HTTP 200 at `00:09:38.646`. The first unchanged accessibility-id command for `WnsRegistrationNavigationButton` began `00:09:38.657` and timed out at `00:09:48.659`; test failure was exactly the unchanged 10-second bounded AutomationId timeout. DELETE completed HTTP 200 at `00:09:53.931` after driver cleanup.
- This final attach experiment reproduces the same UIA hang after removing app-launch/foreground ambiguity. 6.3B is externally blocked after both app-launch and explicit-HWND modes; no product defect was proven and no task checkbox was changed. `ExternalVerified=false` remains preserved.

### 6.3B Start Gate 1: Independent Microsoft UI Automation probe

- Diagnostic-only invocation; no repository probe code, product edits, checkbox changes, commits, or pushes. `ExternalVerified=false` preserved. PID 17192 was observed as unrelated `node.exe` and untouched.
- `Inspect.exe` and Accessibility Insights were not found in PATH or checked standard installation locations. A temporary out-of-process probe under `C:\Users\Usuario\AppData\Local\Temp\opencode` used `System.Windows.Automation`/`UIAutomationClient`, `SendMessageTimeout(WM_NULL)`, and 3-second child bounds; it was removed after the run.
- No App.UI was running at preflight. SCM `ControlParental` was `RUNNING`; `\\.\pipe\ControlParental.UI` was present. Exact current binary launched: `msix_staging\ControlParental.App.UI.exe`, owned PID 15884, session 1, start `2026-08-12 19:31:06`, HWND `460680` (`0x704C8`), title `WinUI Desktop`, `Responding=True`. Tuple was revalidated before cleanup.
- `SendMessageTimeout(WM_NULL)` returned `responsive=True` in 2.1 ms; process CPU was 0.390625 s. Startup log recorded `OnLaunched`, DI/ViewModel success, `MainWindow FAILED: XamlParseException: XAML parsing failed`, `Fallback window shown`, and `Starting message loop`; the HWND was therefore the fallback window, not the intended registration tree.
- UIA root returned in 364.6 ms (wall 476.2 ms): Name `WinUI Desktop`, AutomationId empty, ControlType `ControlType.Window`, enabled true, offscreen false. `FindFirst` for `WnsRegistrationNavigationButton`, `PageHost`, `WnsRegistrationButton`, `WnsRegistrationStatus`, and `WnsRegistrationProgress`, plus Raw/Control/Content traversals, each exceeded the 3,000 ms out-of-process limit and was terminated. No Invoke/Selection was attempted.
- Classification: **B for independent UIA** (responsive window, targeted UIA discovery/tree hangs), with a directly evidenced startup/XAML fallback fault. No product fix in this gate. Next focused diagnostic: isolate the XAML parse failure with sanitized loader diagnostics and validate packaged/current generated XAML resources, then repeat this probe after intended `MainWindow` construction.
- Cleanup: only owned PID 15884 was terminated after tuple revalidation; probe children were bounded/terminated and temporary files removed. Final state: no App.UI, SCM `ControlParental=RUNNING`, `ControlParental.UI` present, PID 17192 untouched.

## 6.3B Final Accepted Reconciliation

- Acceptance status: **PASS for the compatible environment only**. Latest live run: **19 passed, 0 failed, 0 skipped**.
- Functional evidence: real App.UI→Service IPC, safe typed states/lifecycle, W3C DELETE, and App.UI0. This closes 6.3B and the mechanically complete 6.3A/6.3 parent hierarchy; it does not close 6.4.
- Runner/driver: Appium **3.6.0**, NovaWindows2 **1.1.27**; the unchanged scenario was retained and no scenario downgrade was made.
- Stable product SHA-256: `603A6FA35FC30F69723778B2C16422043CC2F35E4290948CC658B9BA2510B69D`.
- Current run E2E baseline SHA-256: `DDEB40FCBCE7019DC8C01653B431472DC3E02111337ED67E86634A0CD3585A9F`.
- Evidence directory: `C:\Users\Usuario\AppData\Local\Temp\ControlParental\e2e-evidence-20260814-110132110`.
- Evidence files were present and validated safe: `app-startup.log` (SHA-256 `A8CC86F1BA93359C8461B3C016099E9F1D34A68FB58CB7EC7C323B1F437C4E18`, 30,978 bytes), `page-source.xml` (SHA-256 `A9A49E91B7AE93BCD325F898DEABF85845AD12A20D98B038FAA15AD1F458B451`, 9,142 bytes), and `registration.png` (SHA-256 `3AA918786BA278ACDCCEE4C0633286B25C183730A6FF1DCBEA2A8F2C101715F9`, 22,645 bytes).
- Runbook focused exit contracts: **3/3 passed**, with explicit exit code **0**. Observer `Process.ExitCode=null` is a non-blocking instrumentation caveat; no independent OS exit was observed.
- Cleanup completed for invocation-owned Appium/driver/App.UI and temporary process state. Redaction validation passed. `ExternalVerified=false` remains unchanged; matrix status remains pending. No backend, JWT, RLS, TLS endpoint, or WNS fan-out claim is made.
