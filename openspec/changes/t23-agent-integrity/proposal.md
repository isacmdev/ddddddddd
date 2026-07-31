# Proposal: T23 — Agent Integrity + Obfuscation

## Intent

Verify that the ControlParental agent binary has not been tampered with or re-packaged, and make reverse-engineering of the domain/motor logic harder. The service self-checks its Authenticode signature and SHA256 hash on startup and periodically, reporting verdicts to the backend. If the backend declares the agent compromised, it degrades enforcement gracefully rather than catastrophically.

## Scope

### In Scope
- Binary integrity self-check (Authenticode `WinVerifyTrust` + SHA256 hash of `Environment.ProcessPath`)
- Wiring `ReportIntegrityAsync` into the service startup and heartbeat loop (via `AntiTamperMonitor`)
- Server-side verdict reaction: alert + degraded enforcement mode (not hard crash)
- NativeAOT/R2R publish profile with trimming directives that preserve EF Core and JSON serialization
- IL obfuscation of the Service assembly (domain/motor layers)

### Out of Scope
- TPM / Device Health Attestation (deferred, marked "Opcional MANAGED" in backlog)
- Post-build Authenticode signing pipeline (manual EV cert signing assumed)
- Anti-debug as a primary defense (basic stubs only)
- Build pipeline changes outside the `.csproj` / `Directory.Build.props`

## Capabilities

### New Capabilities
- `agent-binary-integrity`: Self-verification of Authenticode signature and SHA256 hash on startup and every 30s via `AntiTamperMonitor`. Reports `IntegrityReport` to `/rest/v1/integrity_reports`. On negative server verdict, enters degraded mode (reduced enforcement, logged alert).
- `agent-aot-obfuscation`: NativeAOT-compiled, R2R precompiled, IL-obfuscated Service build that preserves EF Core queries, JSON serialization of IPC messages, and required reflection paths via explicit trimming directives.

### Modified Capabilities
- `backend-integrity-endpoint` (T14): Must accept `IntegrityReport` POST and return a verdict (`{ "verdict": "trust" | "revoked" | "unknown" }`). If T14 endpoint is not ready, T23 can be validated with a mock server.

## Approach

### 1. Binary Integrity Self-Check
- Add `PerformBinaryIntegrityCheckAsync()` to `IAntiTamperMonitor` / `AntiTamperMonitor`
- On each check: compute SHA256 of `Environment.ProcessPath`, call `WinTrustFileInfo.IsSigned()`, send `IntegrityReport` via `IBackendClient.ReportIntegrityAsync()`
- Cache server verdict for 5 minutes to avoid flooding the backend
- On `verdict == "revoked"` or signature invalid: raise `TamperEventType.BinaryIntegrityFailure`, enter degraded mode

### 2. Server Verdict Reaction (Degraded Mode)
- Degraded mode: disable policy enforcement changes, continue monitoring and logging, surface a non-blocking warning in the UI
- **False positive safeguard**: if local signature is valid AND binary hash matches last-known-good cached value, ignore a conflicting server verdict (requires manual override to clear cache)
- Degraded mode clears automatically when a clean verdict is received

### 3. NativeAOT + Obfuscation
- Add `<PublishAot>true</PublishAot>` to `ControlParental.Service.csproj` Release configuration
- Add `<PublishTrimmed>true</PublishTrimmed>` with explicit `[DynamicallyAccessedMembers]` on: EF Core entity types, DbContext, IPC message DTOs, JSON-serializable types
- Apply IL obfuscation (ConfuserEx or Babel as a `dotnet tool`) as a post-publish step targeting `ControlParental.Service.dll`
- R2R already enabled in `Directory.Build.props` — no change needed there

### 4. TPM/DHA Attestation
- Deferred. Document interface `ITpmAttestation` in `ISecretStore.cs` comments for future implementation.

## Proposal Question Round

Before proceeding to specs, the following product/business decisions need clarification:

1. **Degraded mode scope**: When the server returns `verdict: "revoked"`, what exactly should degrade? (a) Block all new enforcement policy changes but keep existing filters active, (b) reduce filtering to allowlist-only with parental override, or (c) something else?

2. **False positive tolerance**: Should the agent accept a 1-attempt false negative (server says revoked but local signature is valid and hash matches cached value) and stay in normal mode, or always defer to the server verdict?

3. **Obfuscation scope**: Should obfuscation cover only the domain/motor logic (`ControlParental.Domain.dll`) or also the Service's IPC and backend client layers? Over-obfuscating IPC may break debugging without adding real security.

4. **Authenticode signing in CI**: Is an EV code-signing certificate available for the pipeline, or is this still a manual step? (Affects whether we can automate the "build → sign → publish" chain in T23.)

5. **TPM attestation priority**: Is TPM/DHA a hard requirement for v1, or genuinely optional post-MVP? (If hard requirement, it needs to be scoped into T23's first slice.)

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| EF Core queries break under trimming | High | Add `[DynamicallyAccessedMembers]` on DbContext and all entity types; validate with `dotnet build --verbosity normal` in Release first |
| JSON serialization fails in AOT for IPC messages | High | Add `[JsonDerivedType]` attrs and source-gen `[JsonSerializable]` registration before publishing |
| IL obfuscation breaks reflection needed by EF/JSON | Med | Run full test suite after obfuscation; exclude assemblies that use reflection heavily from obfuscation pass |
| Server `/integrity_reports` endpoint not yet implemented | Med | Mock server in tests; flag as T14 cross-team dependency |
| Authenticode signing not automated | Low (if cert available) | Document manual step; do not block T23 on CI signing pipeline |

## Rollback Plan

- Revert `Directory.Build.props` and `ControlParental.Service.csproj` publish settings to disable AOT/trimming
- Remove `PerformBinaryIntegrityCheckAsync` from `AntiTamperMonitor`
- Remove `ReportIntegrityAsync` calls from startup/heartbeat
- No database migration needed; no persisted state created by integrity checks

## Success Criteria

- [ ] Build produces a signed (or signable) binary that passes local `WinVerifyTrust`
- [ ] `IntegrityReport` is POSTed to `/rest/v1/integrity_reports` on startup and every 30s
- [ ] A negative server verdict triggers degraded mode without crashing the service
- [ ] Degraded mode clears when a `trust` verdict is received
- [ ] AOT/R2R build passes all existing tests (serialization, DB queries, IPC)
- [ ] IL obfuscated build passes all existing tests
- [ ] Altered binary (hash mismatch) is detected by both local check and server verdict