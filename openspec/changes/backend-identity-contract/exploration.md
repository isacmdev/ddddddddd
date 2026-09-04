# Exploration: SDD5 `backend-identity-contract`

## Current State

SDD1–SDD4 are closed and SDD4 is archived. The only main specification currently present is `openspec/specs/offline-enforcement-safety-loop/spec.md`; there is no canonical main identity/backend specification yet. SDD5 therefore needs a new identity/backend delta rather than modifying the offline safety-loop contract. `openspec/config.yaml` still says strict TDD is disabled, coverage threshold is zero, and E2E is unavailable; the explicit SDD5 gates below override those defaults for this change and must be repeated in proposal, specs, design, tasks, apply evidence, and verification.

The production path is partially implemented but is not a stable identity contract:

1. App.UI sends `PairDevice` over named-pipe IPC and `UIMessageHandler` invokes `IPairingService` in the Service process. The current dirty tree contains real pairing UI wiring and mock-based UI tests, but no configured Appium/WinAppDriver suite.
2. `PairingService.PairAsync` always creates a new anonymous session, gathers machine data synchronously through WMI, retries the pairing call, writes plain keys `device_id` and `parent_id`, and fetches policy. It does not recover an existing pre-pairing session, refresh after pairing, prove that a definitive JWT carries the returned `device_id`, or atomically commit identity state.
3. `DeviceAuthenticator` creates a client-generated identifier and sends it as anonymous signup metadata. It stores tokens and that identifier under `supabase-session-*`, while pairing separately stores the backend-returned identity under plain `device_id`. It also treats JWT `sub` as a fallback `device_id`. These are conflicting identity authorities.
4. The post-pairing transition is missing: the backend may update `app_metadata`, but the client neither forces a refresh nor rejects a refreshed JWT whose `device_id` differs from the pairing response. JWT inspection is unverified payload decoding; `ValidateSessionAsync` checks token shape and local expiry only, not issuer, audience, signature, claim binding, revocation, or server acceptance.
5. Session initialization and refresh APIs exist, but no production caller was found for `IDeviceAuthenticator.InitializeAsync`, `RefreshIfNeededAsync`, or `RefreshTokenAsync`. `BackendClient` simply reads the current in-memory access token. Concurrent refreshes are not single-flight and can race refresh-token rotation.
6. Scheduled policy sync still calls `get_device_policy` with the literal `"default"`, not the paired identity. REST methods serialize and send authenticated requests, but authentication readiness, refresh-before-send, 401 recovery, and device/JWT agreement are not enforced centrally.
7. `SecretStore` uses DPAPI LocalMachine, but writes are non-atomic, write-result failures are ignored by session/pairing code, and `ApplyServiceAcl` does not actually harden the ACL and fails open. Partial writes can leave tokens, pairing identity, and parent identity inconsistent.
8. TLS 1.3 is configured. The active `Program.CreateSupabaseHttpClient` uses a single optional SPKI pin, disables revocation checking when configured, and bypasses ordinary chain errors after a pin match. A mismatch currently throws from `CertificatePinningValidator`, which should abort the callback despite the stale audit claiming an unconditional pass; nevertheless the active path must be replaced by the existing fail-closed policy and covered end to end. Missing pin currently disables pinning entirely, and there is no overlapping-pin rotation model.
9. RLS, Custom Access Token Hook behavior, pairing-code single use, JWT claim issuance, and backend idempotency are assertions in backlog/artifacts, not locally proven behavior. The readiness audit correctly classifies P4/T14/T17/T22/T24 as integration-unready or external. No backend repository or staging receipt is available in this workspace.
10. Existing unit tests are mostly mocks. They prove local mappings and calls, not pairing-code replay resistance, definitive claim issuance, two-device RLS isolation, refresh races, TLS host/certificate behavior, or real WinUI interaction.

Implementation classification:

- **Real production seams:** App.UI → IPC → `UIMessageHandler` → `PairingService`; `BackendClient`; `DeviceAuthenticator`; DPAPI `SecretStore`; TLS handler; policy/outbox REST adapters.
- **Placeholders/debt:** client-generated/preemptive `device_id`, `sub` fallback, `"default"` policy identity, no production auth initialization/refresh orchestration, no-op ACL hardening, optional single pin, synchronous secret reads in `IPairingService`, synchronous WMI collection.
- **Legacy/planning evidence:** active `t24-pairing-code` artifacts claim T14/T17 are complete and T24 is done, but they lack definitive-JWT/RLS proof and a verify report; `apis.md` also contains open contract questions and endpoint naming that diverges from the backlog/current client.
- **Tests only:** mocked HTTP/IPC pairing and token payload tests; no backend identity harness and no instrumented UI E2E.

## Affected Areas

- `openspec/changes/t24-pairing-code/*` — completed T24 implementation evidence to absorb without duplicating; its completion claims require qualification.
- `openspec/changes/t00-t26-backend-readiness-audit/audit/*` — authoritative evidence that pairing/JWT/RLS and backend contract behavior remain externally unverified.
- `openspec/changes/t26-onboarding-ipc-ageband-consent/*` — owns pairing UI/IPC work; SDD5 must not reimplement it, but any SDD5 UI modification inherits instrumented E2E gating.
- `backlog-control-parental-windows.md` and `apis.md` — competing contract descriptions that SDD5 must reconcile into one versioned minimum contract.
- `src/ControlParental.Domain/IDeviceAuthenticator.cs`, `IPairingService.cs`, `IBackendClient.cs`, identity/result records — Domain/Application-facing states and ports need one authority and typed transitions.
- `src/ControlParental.Service/DeviceAuthenticator.cs` — pre-pair session, definitive JWT, refresh/rotation/revocation, claim validation, and race control.
- `src/ControlParental.Service/PairingService.cs`, `BackendClient.cs`, `UIMessageHandler.cs` — pairing orchestration, idempotency/reconciliation, authenticated REST, typed failures, and policy bootstrap.
- `src/ControlParental.Service/SecretStore.cs` — atomic credential-set persistence, ACL failure behavior, corruption recovery, and secret deletion.
- `src/ControlParental.Service/Program.cs`, `CertificatePinningPolicy.cs`, `CertificatePinningValidator.cs`, configuration loading — shared secure transport, host validation, pin rotation, timeouts, and cancellation.
- `src/ControlParental.Service/ScheduledWorkService.cs` — remove the `"default"` identity and consume only a validated definitive session.
- `src/ControlParental.App.UI/PairingPage*`, `PairingViewModel.cs`, pairing IPC contracts — affected only if contract/status changes require UI changes; then real instrumented E2E is mandatory.
- `tests/ControlParental.Service.Tests/*`, `tests/ControlParental.App.UI.Tests/*` — strict TDD evidence, concurrency/security-negative tests, contract harness, coverage, and optional UI instrumentation.
- **External/test infrastructure not present:** backend contract fixture or staging backend capable of anonymous auth, pairing mutation, JWT refresh, RLS policies, revocation, TLS, and deterministic clock control.

## Approaches

1. **Patch existing services independently** — keep the two stores/identifiers and add refresh calls, validations, and tests around current methods.
   - Pros: Smaller initial diff; fewer interface changes.
   - Cons: Preserves split identity authority, makes refresh/pairing races hard to reason about, leaks transport/session policy across adapters, and creates unsafe foundations for SDD6/SDD7.
   - Effort: Medium

2. **Minimal explicit identity state machine with one authenticated transport port** — model `Unpaired → PrePairSession → PairingPending → DefinitiveSession` and invalid/revoked states; make one Service-owned application component atomically persist and expose the validated identity; let REST adapters request an authenticated/cancellable request from it.
   - Pros: One identity authority; explicit invalid transitions; replaceable backend/transport; deterministic refresh and retry behavior; clean dependency surface for offline sync and remote signals.
   - Cons: Requires migration of current secret keys and focused changes across auth, pairing, transport, scheduling, and tests.
   - Effort: High

## Recommendation

Choose approach 2, but keep it deliberately small: one application-level identity/session coordinator, one backend contract port, one secure credential-store adapter, and one authenticated transport adapter. Do not introduce an event bus, CQRS, a generic workflow engine, or a new backend SDK.

The minimum versioned contract should be frozen as a checked fixture/schema plus executable harness before changing behavior. It must identify contract version, auth endpoints, pairing request/response and stable error codes, token/claim shape, REST/RPC names and payloads, idempotency semantics, RLS expectations, time/clock-skew policy, revocation semantics, and TLS requirements. Anything not confirmed by an available backend must be labeled **required backend/test-harness capability**, not represented as already available.

Required state flow:

1. **Unpaired:** no usable identity; stale/partial credentials are quarantined or deleted.
2. **PrePairSession (T17-A):** recover or create exactly one anonymous session; it has an auth-user identity but MUST NOT be treated as a paired `device_id` authority.
3. **PairingPending (T24):** submit a normalized code once per operation with bounded timeout/cancellation and a client operation/idempotency identifier only if the backend contract supports it. A lost success response requires status reconciliation or a fresh code; blindly replaying a one-use code is unsafe.
4. **PairingAccepted:** receive backend-assigned `device_id`; do not expose paired readiness yet.
5. **DefinitiveSession (T17-B):** refresh/reissue the session, validate server acceptance and exact `jwt.device_id == pairing.device_id`, then atomically persist the credential set and activate REST. Never substitute JWT `sub` for `device_id`.
6. **Expiry/refresh:** use skew-aware proactive refresh with one in-flight refresh per session generation. Waiters share the result; stale refresh completions cannot overwrite a newer generation.
7. **Revocation/mismatch:** fail closed for remote access, clear/quarantine credentials, preserve offline enforcement, require controlled re-pairing, and never retry indefinitely.
8. **Reconnect/retry:** bounded exponential backoff with jitter, maximum attempts and wall-clock budget, `Retry-After` support, cancellation, and endpoint-specific retry classification. Duplicate/replayed/stale responses are rejected through operation/session generation.

Scope:

- T14-A minimum versioned backend contract and deterministic contract harness.
- T17-A pre-pairing session; T24 pairing transition; T17-B definitive JWT bound to backend `device_id`.
- T22 fail-closed TLS/certificate validation and two-device RLS/negative security proof against a backend test environment.
- Migration from split/plain identity keys and `"default"` callers to one atomic Service-owned identity snapshot.
- Security-safe logs and typed diagnostics without tokens, codes, parent IDs, certificate pins, response bodies, or sensitive URLs.

Exclusions:

- Building the production backend, parent app, QR pairing, unpair UX, WNS delivery, offline synchronization/recovery (SDD6), and remote signal integrity (SDD7).
- Assuming Supabase hooks, RLS, revocation, idempotency, or staging availability without executable receipts.
- Pairing UI redesign unless strictly required by a contract/status change.
- Caches without explicit invalidation/versioning and speculative frameworks.

Autonomous work units (each MUST create at most 800 lines, target at most 400 total changed lines for normal review, and include its own RED→GREEN→TRIANGULATE→REFACTOR evidence):

1. **Contract v1 + deterministic backend harness** — schemas/fixtures, stable errors, clock controls, two-device identities, replay/idempotency and RLS scenarios. Forecast: 300–500 created lines; split fixture server from client assertions if changed diff exceeds 400.
2. **Identity state model + atomic credential migration** — typed states/generations, one authority, legacy-key migration/quarantine, no synchronous-over-async. Forecast: 250–400 created lines.
3. **Pre-pair/pair/definitive-session transition** — recover/create, pair once, force refresh, exact claim binding, invalid-state and lost-response handling. Forecast: 300–500 created lines; likely review-chain boundary.
4. **Authenticated REST + refresh concurrency** — single-flight refresh, 401/revocation behavior, remove `"default"`, bounded request construction. Forecast: 250–450 created lines.
5. **TLS/RLS/security-negative verification** — shared fail-closed handler, chain + pin overlap/rotation, backend two-device isolation, logging redaction. Forecast: 250–450 created lines.
6. **Pairing UI instrumentation only if UI changes** — Appium/WinAppDriver setup and real valid/invalid/expired/rate-limit/reconnect interaction. Forecast: 300–600 created lines; isolate driver/bootstrap from scenarios if review diff exceeds 400.

Overall 400-line review risk is **High** and chained/stacked review is recommended. No unit may be bundled merely to reduce PR count; contract/harness, identity persistence, transition, transport, security proof, and optional UI evidence must remain independently reviewable and reversible.

## Risks

- **Pairing-code theft/replay/enumeration:** codes are bearer credentials. Require short TTL, one use, server-side attempt limits, uniform public errors where feasible, no logging/telemetry, and replay/lost-response tests. Client behavior alone cannot enforce this.
- **JWT/device mismatch:** current local IDs, JWT `sub`, pairing response, and stored plain key can disagree. No backend call is authorized until exact definitive claim binding is proven.
- **Refresh races and clock skew:** concurrent refresh-token rotation can invalidate a good session; local wall clock can refresh too late/early. Use single-flight generation checks and bounded skew from trusted server time.
- **Secret compromise/inconsistency:** current ACL hardening is ineffective and writes are partial/non-atomic. Fail closed on ACL/write failure and never expose secrets to UI or Domain objects.
- **RLS bypass:** publishable-key configuration, malformed/missing claims, RPC security-definer behavior, and explicit `device_id` parameters may bypass intended isolation unless proven with two-device negative tests. Never ship or accept `service_role`.
- **TLS/certificate failure:** optional pinning, single-pin rotation, disabled revocation, chain-error bypass, wrong host, proxy interception, and certificate expiry need explicit policy and tests. Pin rotation requires overlapping current/next pins and a rollback path.
- **Sensitive logs:** current backend error bodies/exceptions and pin validator details may expose identifiers, tokens, codes, pins, or URLs. Structured allowlisted diagnostics are required.
- **Retry amplification:** repeated one-use pairing, stacked retry layers, polling, and unbounded reconnect can create duplicate work or lockouts. One owner per retry policy; no unnecessary polling.
- **Backend uncertainty:** no backend or staging implementation is present here. Contract claims remain hypotheses until the harness or agreed test backend proves them.
- **Dirty-tree/review interference:** the workspace already contains extensive unrelated modifications. Future apply must isolate SDD5 slices without resetting or absorbing unrelated work.

## Ready for Proposal

**Yes, conditionally.** Proposal may proceed if it treats the backend contract/harness as the first deliverable, preserves T24 evidence without claiming backend completion, explicitly carries all quality gates below, and does not declare pairing/JWT/RLS/TLS complete until external receipts exist. If no test backend can be supplied, client work may proceed only to contract-ready status and verification must remain blocked rather than mocked as complete.

## T24 Reconciliation

- **Absorb:** the active change's pairing request/response draft, typed 404/410/429 mappings, Service-side orchestration seam, IPC integration, unit tests, and implementation history.
- **Migrate into SDD5 requirements/design:** pre-pair session recovery, one-use/lost-response semantics, definitive JWT refresh and exact `device_id` binding, atomic identity persistence, cancellation/time budget, replay/duplicate/invalid-state handling, backend harness, security negatives, and real UI E2E if UI changes.
- **Supersede:** claims that T14/T17 already exist as complete, that storing plain `device_id`/`parent_id` equals persisting the session, that mocked unit tests satisfy DoD, and that T24 is integration-complete.
- **Preserve as evidence:** all current `t24-pairing-code` artifacts and code/test history. Do not move, archive, delete, or rewrite them now.
- **Concrete disposition:** keep `t24-pairing-code` active and frozen during proposal/design. SDD5 references it as prior implementation evidence and owns all remaining identity/backend acceptance. Only after SDD5 verification should the orchestrator close T24 as **absorbed/superseded by `backend-identity-contract`**, preserving its audit trail; do not run a second T24 implementation track.

## Quality Gates

1. **Algorithmic complexity:** document collection bounds and expected complexity for every hot path. Identity/token lookup and state transitions: O(1); JWT/crypto and serialization: O(token or payload bytes); bounded batch serialization: O(n) time/O(n) payload for declared maximum n; RLS device lookup: indexed O(log N) or backend-proven equivalent, never an unbounded table scan; retries: O(k) with fixed maximum k and wall-clock budget. Record measurements for allocations, token parsing, crypto, serialization, and concurrent refresh waiters.
2. **No avoidable inefficiency:** no unnecessary polling, N+1 calls, repeated deserialization, unbounded scans, synchronous blocking (`.Result`, `.Wait`, `GetAwaiter().GetResult()`), duplicated WMI/device discovery, duplicate refreshes, or stacked retry loops. Batch sizes and response-body limits are explicit.
3. **Clean architecture:** Domain holds typed identity states/invariants only; Application orchestrates transitions; Infrastructure owns Supabase/HTTP/DPAPI/TLS; UI sees typed outcomes only. Backend and transport remain replaceable. Tokens, keys, codes, pins, and persistence details stay outside Domain/UI. Ports/adapters are minimal; no speculative bus/CQRS/framework.
4. **Strict TDD:** every behavior follows RED → GREEN → TRIANGULATE → REFACTOR with retained failing/passing evidence per work unit. Tests written after implementation do not satisfy the gate.
5. **Flow/logic:** executable scenarios cover pre-pairing, pairing, definitive JWT, expiry, skew, rotation, retry, revocation, reconnect, duplicate, replay, lost response, stale generation, mismatch, corruption, cancellation, and every invalid transition.
6. **Optimization/reliability:** bounded retries with jitter and `Retry-After`, explicit timeouts/cancellation, endpoint-specific idempotency, one retry owner, no premature cache. Any cache requires key, version, invalidation, size bound, and stale-data tests.
7. **Testing:** changed-scope line coverage MUST exceed 80%; branch coverage MUST be reported; suites must be deterministic; include security-negative, malformed payload, concurrency/race, cancellation, TLS, two-device RLS, and secret-redaction tests. Mocks prove local behavior only. Backend contract/RLS/JWT requires a deterministic harness or backend test environment. If pairing UI changes, real instrumented Appium/WinAppDriver E2E is a mandatory gate from SDD5; unit tests and mocked IPC do not substitute.

## Next Step

Run `sdd-propose` for `backend-identity-contract`, carrying this exploration verbatim as constraints. The proposal must name the contract-v1/harness work unit first, reference (not duplicate) `t24-pairing-code`, declare SDD6/SDD7 dependencies, preserve the high review/chaining risk, and state which acceptance criteria are blocked until a backend test environment is available.
