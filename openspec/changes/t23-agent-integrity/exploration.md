# Exploration: T23 — Agent Integrity + Obfuscation

## status: explored

## executive_summary
The codebase already has significant groundwork for T23: `WinVerifyTrust` P/Invoke wrappers exist in `Interop/WinTrust.cs`, `ProtectedProcessReporter` uses them to check Authenticode signatures, `IntegrityReport` and `IBackendClient.ReportIntegrityAsync` (T14) already model server-side reporting, and `AntiTamperMonitor` (T13) provides a framework for detecting and reacting to integrity failures. The main gaps are: (1) no binary hash verification beyond signature, (2) no actual invocation of `ReportIntegrityAsync` wired into the service startup or heartbeat loop, (3) no TPM/DHA attestation, and (4) no AOT/R2R publishing configuration or IL obfuscation. The Domain project is positioned as pure net9.0 (no Windows-specific deps), which is the right boundary for what can be AOT'd — the Service project (windows-specific, `AllowUnsafeBlocks`) would be the target for AOT/R2R publishing.

## findings

### 1. WinVerifyTrust / Authenticode (EXISTING — partial)

**Files:**
- `src/ControlParental.Service/Interop/WinTrust.cs` — Full P/Invoke for `wintrust.dll!WinVerifyTrust`, struct marshalling for `WINTRUST_DATA`/`WINTRUST_FILE_INFO`, `WinTrustFileInfo` wrapper that returns `IsSigned` (bool). Currently used only by `ProtectedProcessReporter`.
- `src/ControlParental.Service/ProtectedProcessReporter.cs` — Implements `IProtectedProcessReporter`. Calls `WinTrustFileInfo` to verify the service binary's Authenticode signature. Returns `IsPplCapableAsync` (bool) and `GetStatusDescriptionAsync` (string). Does NOT compute binary hash — only checks signature validity.
- `src/ControlParental.SessionAgent/Interop/AppIdentityResolver.cs` — `TryGetPublisherFromSignature` stub at line 214–223 with comment "T23 will wire up WinVerifyTrust for proper integrity verification" — not implemented.
- `src/ControlParental.Service/BackendClient.cs` — `ReportIntegrityAsync` (line 370) sends `IntegrityReport` to `/rest/v1/integrity_reports`. Report contains: `ReportHash`, `Timestamp`, `AgentVersion`, `Platform`. **This method exists but is never called anywhere in the codebase.**

**Assessment:** P/Invoke and signature-check wrapper are ready. Binary hash computation and the wiring of the integrity verdict into the service heartbeat loop is the primary gap.

### 2. IntegrityReport and Backend Reporting (EXISTING — dormant)

**Files:**
- `src/ControlParental.Domain/IBackendClient.cs` — `IntegrityReport` class (lines 390–411) with `ReportHash`, `Timestamp`, `AgentVersion`, `Platform`. `ReportIntegrityAsync` returns `bool`.
- `src/ControlParental.Service/BackendClient.cs` — Implementation of `ReportIntegrityAsync` (lines 369–397). Correctly POSTs to `/rest/v1/integrity_reports` with authenticated request.

**Assessment:** The reporting model and HTTP call exist. The service never invokes `ReportIntegrityAsync`. T23 needs to: compute binary hash (SHA256 of self), call `ReportIntegrityAsync` with verdict, and react to a negative server verdict (degraded enforcement, alert).

### 3. AntiTamperMonitor (EXISTING — T13, partial coverage)

**Files:**
- `src/ControlParental.Service/AntiTamperMonitor.cs` — Monitors clock drift/jumps, timezone changes, privilege escalation, agent death. Has `PerformIntegrityCheckAsync` (line 239) that runs every 30 seconds but only checks: clock integrity, timezone, privilege status. Does NOT check binary integrity/signature.
- `src/ControlParental.Domain/IAntiTamperMonitor.cs` — Interface defining `TamperEventType` enum and monitor contract.

**Assessment:** AntiTamperMonitor is the natural place to integrate binary integrity checks. `PerformIntegrityCheckAsync` is the right hook. A new `PerformBinaryIntegrityCheckAsync` method should be added, computing SHA256 hash of `Environment.ProcessPath` and verifying Authenticode signature via `WinTrustFileInfo`.

### 4. Build / Publish Pipeline (PARTIAL)

**Files:**
- `Directory.Build.props` — Lines 24–28 show existing R2R configuration:
  ```xml
  <PublishSingleFile>false</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <PublishReadyToRun>true</PublishReadyToRun>  <!-- R2R is ON for Release -->
  <PublishTrimmed>false</PublishTrimmed>
  ```
  R2R is already enabled. No NativeAOT is configured. No IL obfuscation is configured.
- `src/ControlParental.Service/ControlParental.Service.csproj` — `AllowUnsafeBlocks>true` (line 11), uses `Microsoft.NET.Sdk.Worker` SDK.
- `src/ControlParental.Domain/ControlParental.Domain.csproj` — Pure net9.0, no Windows deps, `UseWindowsForms>false`. This is the correct AOT candidate — EF Core, System.Text.Json, System.Reactive are all AOT-compatible.

**Assessment:** R2R is already in place. T23 needs to:
- Configure NativeAOT (via `<PublishAot>true</PublishAot>` in Release for Service project)
- Add IL obfuscation (e.g., Babel, Dotfuscator, or ConfuserEx — tooling not present in repo)
- Add trimming directives for types that must not be trimmed (EF Core model types, JSON serializable DTOs, IPC message types)
- Test that AOT/trimmed build preserves serialization and DB access

### 5. DI Structure (EXISTING)

**Files:**
- `src/ControlParental.Service/Program.cs` — All DI registration is here (lines 133–347). Key registrations:
  - `IProtectedProcessReporter` → `ProtectedProcessReporter(ServiceExePath)` (line 151–152)
  - `IBackendClient` → `BackendClient` (line 284)
  - `IAntiTamperMonitor` → `AntiTamperMonitor` (line 252)
  - `ISecretStore` → `SecretStore` (line 273) — mentions "opcionalmente TPM" in XML doc

**Assessment:** Adding a new `IIntegrityService` or extending `IAntiTamperMonitor` with binary integrity checks fits cleanly into the existing DI pattern.

### 6. Domain Boundaries (clear)

- **Domain project** (`ControlParental.Domain.csproj`): pure net9.0, no Windows-specific deps. Contains: interfaces, models, rules engine. AOT-compatible.
- **Service project** (`ControlParental.Service.csproj`): windows-specific (`net9.0-windows10.0.19041.0`), `AllowUnsafeBlocks`, P/Invoke (`Interop/` folder), Win32 API calls.
- **SessionAgent project** (`ControlParental.SessionAgent.csproj`): windows-specific, smaller surface.

**Assessment:** AOT/R2R publishing applies to the Service project. The Domain project can be AOT-compiled as part of the Service publish since it's a dependency. Obfuscation should target the Service's domain/motor assemblies, not Domain interfaces needed for EF migrations or JSON serialization.

### 7. EF Core / Serialization Compatibility (AOT concerns)

- EF Core 9.0 with SQLite — source-gen'd DbContext is used (`ControlParentalDbContext`). Need to verify the source-gen is compatible with trimming/AOT. This is a known pain point — EF Core navigation properties and lazy loading can break with trimming.
- `System.Text.Json` — source-gen serializers are recommended for AOT. Current codebase uses `JsonSerializerOptions` with `PropertyNamingPolicy` — may need `[JsonSerializable]` source-gen registration.
- IPC messages (`IpcMessage.cs` and subtypes) — polymorphic deserialization via `JsonSerializer` — needs `[JsonDerivedType]` attributes for AOT safety.

**Assessment:** AOT/trimming without careful attribute decoration will break EF Core queries and JSON serialization. This is a significant risk that needs design attention.

### 8. TPM / Device Health Attestation (NOT PRESENT)

- `ISecretStore.cs` XML doc line 154: "Provee cifrado con DPAPI/DataProtectionProvider y opcionalmente TPM." — intent noted but TPM is not implemented.
- No references to `Tbs.dll`, `DeviceHealthAttestation`, or Windows DHA APIs anywhere in the codebase.

**Assessment:** TPM attestation is a future/optional step. T23 backlog item says "Opcional MANAGED" — should be deferred to a follow-up unless explicitly prioritized.

### 9. Existing Test Patterns for Platform-Specific Code

**Files:**
- `tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj` — `AllowUnsafeBlocks>true`, references Service project directly.
- `tests/ControlParental.Service.Tests/BackendClientTests.cs` — Tests `ReportIntegrityAsync` (lines 512–567, 921–945). Uses `HttpClient` mocking via `MockHttpMessageHandler`.
- No existing tests for `WinTrustFileInfo` or `WinVerifyTrust` P/Invoke.
- No existing tests for AOT/trimming behavior.

**Assessment:** Platform-specific code (P/Invoke) is currently not unit-tested (would require Windows test runner). Integration tests with actual Authenticode signing would need a signed test binary and a proper test harness.

### 10. Openspec Artifacts

- `openspec/config.yaml` — exists with project context.
- `openspec/specs/` — directory exists but is empty (no main specs yet).
- `openspec/changes/t23-agent-integrity/` — created for this exploration.
- No prior SDD artifacts for T23 exist yet.

---

## risks

1. **EF Core + AOT trimming conflict**: The `ControlParentalDbContext` and EF Core queries are known to be fragile under trimming. Must use `[DynamicallyAccessedMembers]` attributes on DbContext and entity types, and may need to exclude certain paths from trimming.
2. **JSON serialization breakages under AOT**: Polymorphic IPC messages (`IpcMessage` subtypes) use `JsonSerializer` without source-gen — will fail at runtime in AOT builds. Requires `[JsonSerializable]` and `[JsonDerivedType]` attributes.
3. **No IL obfuscation tooling in repo**: T23 backlog calls for IL obfuscator but no NuGet package or tooling is referenced. Would need to evaluate Babel/Blowfish/ConfuserEx as a dotnet tool.
4. **Authenticode signing not automated**: The R2R/AOT build produces binaries, but signing with Authenticode EV certificate is a manual/post-build step not captured in any `.csproj` target.
5. **TPM/DHA not implemented**: The "optional MANAGED" attestation is not started — risk is low (it's optional) but the gap should be documented.
6. **Server-side integrity endpoint may not exist**: `ReportIntegrityAsync` calls `/rest/v1/integrity_reports` — the Supabase backend table/function may not be created yet. This is a cross-team dependency.

---

## next_recommended

**propose** — proceed to `sdd-propose` to define the change scope, approach (Authenticode-only vs Authenticode+hash), and which items are in-scope vs deferred (TPM/DHA, obfuscation). The proposal should also address the AOT risks and define the trimming exclusion strategy before any build changes.

---

## artifacts

- `openspec/changes/t23-agent-integrity/exploration.md` — this file

---

## skill_resolution

`paths-injected` — orchestrator provided no skill paths; no fallback registry found; proceeded with phase skill only.