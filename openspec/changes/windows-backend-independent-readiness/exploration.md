## Exploration: windows-backend-independent-readiness

### Current State

The Windows solution already has the right broad ownership boundary: `ControlParental.Service` owns SQLite, authentication, scheduled work, outbox processing, integrity checks, and backend access; `ControlParental.App.UI` owns the in-process WNS receive path and signals the Service through IPC; `ControlParental.Domain` contains the interfaces and IPC messages. The current report is directionally accurate, but the current code exposes several Windows-only fixes that can be implemented and verified without selecting new backend routes or DTOs.

The most immediate correctness defect is scheduled-work gating. `ScheduledWorkService` initializes every backoff value to `1`, while `ShouldRun` only permits work when the value is `0` (`src/ControlParental.Service/ScheduledWorkService.cs:126-129,616-627`). No timer path decrements the value, so heartbeat, outbox, and reconciliation work are permanently skipped. The same service uses synchronous `.Wait(timeout)` timer callbacks and starts an untracked startup policy task, which creates overlap, cancellation, and observability risks (`ScheduledWorkService.cs:167-204,229-303`). Policy sync also uses the literal identity `"default"` instead of the persisted authenticated device identity (`ScheduledWorkService.cs:589-605`).

Session persistence exists, but initialization returns success even when the access token is expired or its expiry metadata is missing: it sets `NeedsRefresh` and still returns `DeviceAuthResult.Succeeded` (`DeviceAuthenticator.cs:139-182`). Anonymous-session creation generates a local device identifier before pairing and sends it as auth metadata (`DeviceAuthenticator.cs:196-254`); pairing later writes the backend device identifier independently and does not refresh the session before the first authenticated policy fetch (`PairingService.cs:80-100,163-200`). `PairingService` also reads and writes the unscoped `device_id` secret while `DeviceAuthenticator` uses the session-prefixed secret namespace (`PairingService.cs:52-67,165-170`; `DeviceAuthenticator.cs:43-58`). These are Windows lifecycle and identity ownership issues, but the final post-pairing response/session contract remains a backend dependency.

`BackendClient` is nominally the service-owned HTTP boundary, but it currently contains speculative endpoint and DTO assumptions for policy, usage, alerts, behavioral events, heartbeat, time requests, integrity, push registration, and pairing (`src/ControlParental.Service/BackendClient.cs:60-477`). The Windows-safe slice is to centralize request construction, authentication, TLS behavior, timeout/cancellation, JSON parsing, and typed error classification behind this boundary. It must not freeze or broaden backend routes, field names, response DTOs, or age-band values before canonical contracts are published.

The outbox has stable local deduplication and durable attempt/error fields (`src/ControlParental.Service/OutboxManager.cs:31-118`; `src/ControlParental.Service/ControlParentalDbContext.cs:98-113`), but processing is not per-entry: grouped success marks every pending entry of a table as sent, and one failure subsequently marks every pending entry as failed (`ScheduledWorkService.cs:438-533`). Malformed JSON is marked failed, but unknown table names can be silently ignored. The local retry slice can therefore be completed independently by introducing per-entry outcomes, explicit permanent/transient classification, and bounded backoff without deciding the remote schema.

TLS pinning is applied to the Supabase auth and backend clients through `CreateSupabaseHttpClient`, but WNS uses a separate unconfigured `HttpClient` in both the service and UI (`Program.cs:86-115,299-320,383-396`; `WnsNotificationService.cs:32-41`; `WnsPushNotificationHandler.cs:184-193`). The safe Windows work is consistency and ownership: one configurable transport policy for service-owned backend traffic, no leaked secret values in logs, and explicit handling for pin mismatch versus ordinary transport failure. WNS endpoints are a separate platform transport and must not inherit a Supabase pin blindly.

WNS support detection and lifecycle are split between two competing implementations. App.UI checks `WNS_AAD_APP_ID`, registers `PushNotificationManager`, creates a channel, and has a shutdown path, but `OnPushReceived` only obtains a deferral and never calls `SendTriggerSyncAsync` (`src/ControlParental.App.UI/WnsPushNotificationHandler.cs:69-163`). Its channel registration uses only the publishable key, not the authenticated device session (`WnsPushNotificationHandler.cs:168-207`). The Service-side `WnsNotificationService` instead contains server-send credentials and attempts to request a channel itself (`src/ControlParental.Service/WnsNotificationService.cs:45-203`), and its hosted adapter renews/registers on a timer (`WnsHostedService.cs:38-79`). Windows can safely detect capability, own channel receive/renewal, send a raw-push sync signal over the existing `TriggerSync` IPC contract, and remove client-side sending credentials. Backend registration remains deferred to its canonical authenticated contract.

Local integrity collection is already independently implementable: `IntegrityChecker` calls WinVerifyTrust and computes a lowercase SHA-256 hash of the running executable (`src/ControlParental.Service/IntegrityChecker.cs:45-116`; `src/ControlParental.Service/Interop/WinTrust.cs:101-119`). `AntiTamperMonitor` currently couples that local result to a speculative remote report/verdict and processes a missing/failed remote verdict through the verdict handler (`AntiTamperMonitor.cs:373-424`). The safe slice is to make local evidence collection, result semantics, logging, and local reaction deterministic; remote submission and verdict interpretation must remain behind a capability/contract seam.

JSON handling is not uniformly robust. Service HTTP clients use several independently-created options, some default reflection-based serializers, and broad `catch (Exception)` blocks; WNS IPC serialization in `WnsPushNotificationHandler` does not use the shared source-generated context (`WnsPushNotificationHandler.cs:226-229`). Named-pipe paths do use source-generated contexts and explicit message dispatch (`src/ControlParental.Service/Interop/NamedPipeUIServer.cs:215-311`; `src/ControlParental.App.UI/Interop/NamedPipeUIChannel.cs:52-106`), providing an existing pattern for shared JSON robustness.

Tests are predominantly unit/component tests with HTTP mocks, EF in-memory support, and source-generated IPC contract tests. There is no configured end-to-end harness in `openspec/config.yaml:45-56`. The safe verification strategy is deterministic unit/component coverage for clocks, scheduler state transitions, session persistence, transport/error classification, outbox per-entry outcomes, TLS callback behavior, WNS capability/lifecycle state, IPC signaling, local integrity evidence, and malformed/unknown JSON. Do not add tests that require speculative backend routes, DTOs, verdicts, pairing responses, or age-band wire values.

### Affected Areas

- `src/ControlParental.Service/ScheduledWorkService.cs` — fix timer/backoff state semantics, avoid batch-wide acknowledgement, use authenticated identity for local policy selection, and add structured observability.
- `src/ControlParental.Service/DeviceAuthenticator.cs` — define persisted-session initialization, refresh, expiry, rotation, and failure transitions without inventing a backend response shape.
- `src/ControlParental.Service/PairingService.cs` — remove split/local identity authority and make post-pairing sequencing explicit; defer final response/session handling.
- `src/ControlParental.Service/BackendClient.cs` and `src/ControlParental.Domain/IBackendClient.cs` — make the Service the single HTTP boundary with transport/error seams; preserve provisional operations until contracts are canonical.
- `src/ControlParental.Service/OutboxManager.cs` and `src/ControlParental.Service/ControlParentalDbContext.cs` — preserve local durable identity while adding per-entry confirmation and retry metadata semantics.
- `src/ControlParental.Service/Program.cs`, `CertificatePinningValidator.cs`, and `ConfigurationLoader.cs` — consolidate service HTTP transport configuration, TLS/pinning policy, and safe configuration diagnostics.
- `src/ControlParental.App.UI/WnsPushNotificationHandler.cs` — capability detection, channel lifecycle, authenticated-registration seam, raw push acknowledgement, and actual `TriggerSync` IPC dispatch.
- `src/ControlParental.Service/WnsNotificationService.cs` and `WnsHostedService.cs` — remove client-side send/channel ownership from the Windows Service or isolate it behind an explicitly disabled server-only adapter; prevent secret loading on the client path.
- `src/ControlParental.Service/IntegrityChecker.cs`, `Interop/WinTrust.cs`, `AntiTamperMonitor.cs`, and `IntegrityVerdictHandler.cs` — separate local Authenticode/SHA-256 evidence from a future remote verdict.
- `src/ControlParental.Domain/UIMessagesJsonContext.cs`, `src/ControlParental.Service/Interop/NamedPipeUIServer.cs`, `src/ControlParental.App.UI/Interop/NamedPipeUIChannel.cs`, and HTTP/WNS serializers — establish shared JSON options/context and bounded malformed-input behavior.
- `tests/ControlParental.Service.Tests/*`, `tests/ControlParental.App.UI.Tests/*`, and `tests/ControlParental.Domain.Tests/*` — add contract-independent regression tests and observability assertions.

### Approaches

1. **Windows hardening around the existing seams** — Correct scheduler/session/outbox/TLS/WNS/integrity/JSON behavior while keeping `IBackendClient` as the only backend-facing abstraction and using neutral result types for unresolved operations.
   - Pros: immediately verifiable; small blast radius; preserves backend contract flexibility; aligns with current Clean/Hexagonal ownership.
   - Cons: cannot prove end-to-end backend success; some methods remain provisional until backend contracts arrive.
   - Effort: Medium

2. **Replace the current client with a new canonical protocol layer now** — Rewrite backend calls and DTOs as if the report's recommended routes and payloads were final.
   - Pros: could reduce later migration if assumptions happen to match.
   - Cons: freezes unapproved routes/DTOs; duplicates backend decisions; increases rework and review size; violates the stated independence boundary.
   - Effort: High

### Recommendation

Proceed only with Approach 1, sliced by independently verifiable Windows behavior. First establish identity/session and scheduler invariants, then make outbox outcomes and the Service-owned HTTP transport explicit, then correct WNS capability/channel/IPC ownership and local integrity evidence, and finally harden shared JSON and observability. Each slice should use fakes, local persistence, deterministic clocks, mocked HTTP handlers, and IPC test doubles; no slice should assert a speculative backend route or DTO.

The following MUST remain out of scope until the backend publishes canonical contracts: final policy routes/DTOs; heartbeat routes/DTOs; telemetry routes/DTOs for usage, behavioral events, and time requests; alert routes/DTOs; integrity-verdict route/DTO and verdict semantics; WNS registration route/DTO; pairing response/session contract; and age-band wire values. Windows may prepare adapters, validation, error handling, and test seams for these capabilities, but must not freeze their wire details.

Product/behavior decisions requiring clarification before proposal are: whether an unpaired installation may create and retain an anonymous session or only initialize it at pairing; whether pairing must atomically replace any pre-pairing identity and refresh before any authenticated read; whether local integrity failure is warning-only, limiting, or fail-closed before a remote verdict exists; whether WNS is an optional acceleration with polling as the required behavior; what local retention/dead-letter policy applies to permanently rejected outbox entries; and which observability fields may be persisted locally without sensitive payloads.

### Recommendation Details and Safe Slicing

- **Slice A — scheduler and session invariants:** inject a clock/backoff policy, make initial work runnable, ensure failure delays are measurable rather than permanently blocking, serialize or cancel overlapping executions, and make initialization/refresh state truthful. Verify with deterministic unit tests only.
- **Slice B — identity and HTTP boundary:** make the authenticated Service identity authoritative locally, remove fallback/default device identifiers from backend-bound operations, centralize request headers/timeouts/JSON/error classification, and leave unresolved route builders isolated. Verify with mocked handlers and no live endpoint assumptions.
- **Slice C — outbox reliability:** return per-entry outcomes, mark only confirmed entries sent, classify malformed/permanent/transient failures, retain bounded diagnostics, and preserve stable deduplication. Verify mixed success/failure, retry, cancellation, and restart scenarios using EF in-memory or SQLite test fixtures.
- **Slice D — WNS and IPC:** detect unsupported/unpackaged configuration without crashing, create/renew/close channels in App.UI, invoke `SendTriggerSyncAsync` from raw receipt, and ensure no WNS send secret is loaded by a client-facing path. Verify state transitions and `TriggerSync` messages with fakes; do not verify backend registration wire data.
- **Slice E — local integrity, TLS, JSON, observability:** make local evidence usable without remote verdicts, apply one explicit policy to service-owned HTTPS, preserve WNS transport separation, use shared JSON handling, and emit redacted structured events. Verify local cryptographic and malformed-input behavior.

### Risks

- Fixing the scheduler may activate currently dormant network writes, exposing the existing provisional backend assumptions sooner; feature flags or an explicit backend-contract capability gate may be needed.
- Refresh-token rotation and post-pairing identity replacement can lose access if persistence is not transactional or if an old token is overwritten before the new session is durable.
- Removing default identities can strand pre-existing local data; migration and recovery behavior must be decided before destructive cleanup.
- Per-entry confirmation cannot guarantee remote idempotency until canonical backend keys/constraints are published; local deduplication only limits client duplicates.
- Certificate pinning can cause a fleet-wide outage during uncoordinated certificate/key rotation; rotation and rollback procedures need two-pin or controlled configuration support.
- WNS channel expiry, unsupported package identity, and permission failures are normal states, not fatal service failures; polling must remain reliable.
- Local Authenticode success is evidence, not a cloud attestation verdict; enforcement must not silently escalate on an undefined remote result.
- Error logs currently risk including server response bodies; structured observability must redact tokens, channel URIs where appropriate, and arbitrary payloads.

### Ready for Proposal

**Yes, conditionally.** The Windows-independent implementation boundary is sufficiently understood for a proposal, provided the proposal is explicitly limited to local correctness, lifecycle, transport seams, outbox reliability, WNS receive/IPC behavior, local integrity evidence, JSON robustness, tests, and redacted observability. Before proposal approval, the product owner must answer the six behavior decisions above; the proposal must also carry an explicit contract-deferred list so no speculative backend route, DTO, verdict, pairing response, or age-band value becomes a requirement.
