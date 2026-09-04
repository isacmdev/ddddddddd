# Design: Backend Identity Contract — Client Ready

## Technical Approach

Preserve the implemented Domain/Service/Infrastructure split and make the Service the sole definitive identity authority. Close this change as **client-ready** through a versioned, executable local acceptance boundary; do not infer live integration. Current release validation is x64 only: Windows 10 22H2 legacy, Windows 11 24H2, and Windows 11 25H2. ARM64 has compile intent only and requires a separate future enablement effort; this change makes no ARM64 package, runtime, or user-support claim.

| Readiness | Evidence | Meaning |
|---|---|---|
| `client-ready` | deterministic local suites, E2E, manifest and local receipts | client conforms to v1; `ExternalVerified=false` |
| `backend-integrated` | future live JWT/RLS/TLS/WNS fan-out receipts | shared backend interoperability verified |

The 6.4 matrix is an execution plan, not evidence: each cell requires exact OS/build/architecture provenance, unchanged behavior, clean-install/runtime, cleanup/redaction, and hash evidence. It cannot assert `ExternalVerified` or any backend claim.

## Architecture Decisions

| Decision | Choice | Alternative / tradeoff | Rationale |
|---|---|---|---|
| Authority | Keep `BackendIdentityCoordinator` and `BackendClient` Service-owned | UI transport is simpler but splits identity | One generation controls binding, refresh, invalidation and remote access. |
| WNS migration | UI obtains the MSIX channel, then sends one authenticated named-pipe request; Service durably records latest intent and upserts through `BackendClient` | UI publishable bearer leaks authority | UI retains no backend key/token and receives only typed status. |
| Acceptance boundary | Versioned manifest + deterministic harness + local receipt schema + runbook | Mock success presented as integration | Local evidence is reproducible and structurally unable to assert external verification. |
| E2E driver ownership | Harness owns Appium lifecycle and driver process; use Appium/WinAppDriver first, then document Nova fallback only if WinAppDriver fails | Driver choice hidden in a receipt or scenario downgrade | Ownership, finite timeouts, identical assertions, and explicit fallback preserve reproducibility and expose compatibility defects. |
| Release support boundary | Validate only Win10 22H2 x64 (legacy), Win11 24H2 x64, and Win11 25H2 x64 current host or clean equivalent | Broader matrix implies unsupported release/runtime guarantees | A narrower claim is evidence-backed; Win10 EOL maintenance and absent ARM64 user guarantee remain explicit risks. |
| Environment preparation | Immutable parent plus differencing VM child may prepare a cell, but is not evidence until the child has the exact target OS/build | Treating a near-version image as the target | Prevents Win11 22H2 or other mismatched-build evidence from entering 6.4; detach temporary networking after provisioning. |
| Quality gate for discovered fixes | Isolated work unit with clean architecture, algorithmic/efficiency review, strict TDD, changed-scope line coverage above 80%, and branch evidence | Fix inline during matrix execution | Keeps validation receipts attributable and prevents an environment defect from becoming unreviewed product scope. |
| ARM64 re-entry | Record a future dedicated initiative with compile, package, runtime, and native UI cells plus rollback to x64-only release claims | Quietly expand 6.4 to ARM64 | ARM64 can be enabled only after a new matrix and evidence path are available. |

## Data Flow

```text
App.UI/MSIX -> create/renew Channel URI
    -> NamedPipeUIChannel -- RegisterWnsChannel(operationId, uri, expiry) --> NamedPipeUIServer
    -> UIMessageHandler -> protected latest-intent store -> BackendClient
    -> BackendIdentityCoordinator(definitive generation) -> backend upsert
    <- WnsRegistrationResult(Accepted|PendingOffline|Denied|Retryable)

restart/network restore -> Service restore identity -> reconcile latest intent once
WNS push -> TriggerSync IPC -> Service authenticated policy pull (payload ignored)
```

The UI owns the IPC timeout and caller cancellation, with no backend retry. Service owns bounded network timeout, stable idempotency key and at most three jittered retries; cancellation propagates. Offline acceptance means “persisted pending,” never remote success. Newer channel intent supersedes stale intent in O(1); restart reconciliation is single-flight and non-polling.

For each 6.4 cell, an immutable payload and immutable harness input produce a per-cell receipt containing exact OS/build/architecture, runner and driver provenance, payload/harness hashes, finite-timeout outcomes, redacted evidence, and cleanup status. The harness owns Appium/driver startup, readiness, command/session/test-suite timeouts, and finally-based cleanup. WinAppDriver is attempted first; a NovaWindows run is acceptable only as a documented fallback with the same payload and assertions. Temporary networking is detached after provisioning. No secrets or credentials enter payloads, receipts, logs, or artifacts.

## File Changes

| File | Action | Description |
|---|---|---|
| `src/ControlParental.Domain/IpcMessage.cs` | Modify | Add credential-free WNS request/result types and operation ID. |
| `src/ControlParental.Domain/UIMessagesJsonContext.cs` | Modify | Source-generated IPC serialization. |
| `src/ControlParental.App.UI/WnsPushNotificationHandler.cs` | Modify | Remove HTTP/key/bearer/body logging; call `IUIChannel` only. |
| `src/ControlParental.App.UI/Interop/NamedPipeUIChannel.cs` | Modify | Use existing bounded query/response IPC path. |
| `src/ControlParental.Service/UIMessageHandler.cs`, `Program.cs` | Modify | Authenticate/dispatch WNS request and compose Service handler. |
| `src/ControlParental.Service/BackendClient.cs` | Modify | Definitive-session WNS upsert with centralized reliability/redaction. |
| `tests/ControlParental.App.UI.Tests/WnsLifecycleTests.cs` | Modify | Prove no direct backend transport and typed IPC outcomes. |
| `tests/ControlParental.Service.Tests/UIMessageHandlerWnsTests.cs` | Create | Auth, offline, retry, restart and stale-intent cases. |
| `tests/ControlParental.Service.Tests/BackendIdentityContractV1Harness.cs`, `BackendIdentityContractV1Tests.cs` | Modify | Emit and validate deterministic local-only acceptance receipts. |
| `tests/ControlParental.App.UI.Tests/E2E/WnsRegistrationE2ETests.cs` | Create | Real interactive UI scenario using `AutomationId`. |
| `tests/ControlParental.App.UI.Tests/E2E/run-windows-e2e.ps1` | Create | Preflight, finite timeouts and guaranteed process/session cleanup. |
| `openspec/changes/backend-identity-contract/evidence/external-acceptance-manifest.v1.json` | Create | Contract version, routes/claims as parameters, probe IDs and artifact hashes. |
| `openspec/changes/backend-identity-contract/evidence/local-receipt.schema.v1.json` | Create | Require local mode, redacted environment/results and `ExternalVerified: { const: false }`. |
| `openspec/changes/backend-identity-contract/evidence/backend-integration-runbook.md` | Create | Exact prerequisites, secret injection, probes, receipt validation and cleanup. |

## Interfaces / Contracts

IPC rejects oversized/malformed messages, unauthenticated SID/process identity, duplicate conflicting operation IDs and non-definitive Service state. Logs/receipts contain status, correlation ID, generation and hashes only—never JWTs, publishable keys, pairing codes, Channel URIs, bodies, pins or sensitive URLs.

The local harness uses controlled identity, clock, errors and concurrency; parses each bounded payload once and emits schema-validated receipts. Manifest hashes bind contract, harness and schema. External execution remains impossible in this change because local receipts hard-code `ExternalVerified=false`.

## Testing Strategy

Strict RED→GREEN→TRIANGULATE→REFACTOR covers unit, IPC integration, security/concurrency and real E2E. Changed-scope line coverage must exceed 80%; report branch coverage. Prove O(1) state/intent/idempotency, O(bytes) JSON/crypto, bounded O(n) batches and O(k) retries; reject blocking waits, polling, N+1 access, repeated parsing and retry stacking. Any discovered code fix is a separate isolated work unit and must pass clean-architecture plus algorithmic/efficiency review before entering the matrix. NativeAOT Release baseline concerns remain deferred to Unit7 unless they block the matrix runtime.

E2E preflight installs Appium 3 and its Windows driver, enables Developer Mode, verifies WinAppDriver, and runs on an unlocked interactive desktop—not Session 0. Add stable `AutomationId`s to affected controls. Execute only these lanes: Win10 22H2 x64 legacy, Win11 24H2 x64 VM, and Win11 25H2 x64 current host or clean equivalent. Do not record Win11 22H2 evidence. Each scenario has session/command/test-suite timeouts and `finally` cleanup for app, Appium, driver, temporary state and pending registration. If WinAppDriver fails, run the same assertions through a documented NovaWindows fallback and identify the driver in the receipt.

## Migration / Rollout / Rollback

Ship IPC contract, Service handler/store, then UI cutover and E2E. On first run, ignore/remove UI backend configuration and reconcile only the latest Service-owned intent after definitive identity restore. Rollback disables WNS registration and quarantines pending channel data while preserving cached offline enforcement; it never restores publishable bearer use. No backend/data migration is required. If a future ARM64 initiative fails, re-enter through a separately approved matrix and receipts, then roll back to the x64-only claim without invalidating x64 artifacts.

## Open Questions

- Future acceptance owner will supply staging routes, issuer/audience, pins, two device identities, revocation control and WNS fan-out credentials; this does not block `client-ready`.
- NativeAOT Release baseline remains a Unit7 concern unless it blocks matrix runtime.
