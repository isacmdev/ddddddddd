# Design: T23 — Agent Integrity + Obfuscation

## Status: designed (purged — limited to R1–R12 from backlog, no extra behavior)

## Executive Summary

Implement R1–R12 from T23 verbatim: local Authenticode verification (`WinVerifyTrust`) and SHA256 hash computation on startup, periodic reporting to the existing `POST /rest/v1/integrity_report` endpoint (T14), and AOT/R2R publish with trimming directives. Deferred: TPM/DHA ("Opcional MANAGED"), CI signing pipeline (not in backlog), and any mechanism beyond what R5 demands.

---

## Requirements Mapping (R1–R12 from backlog)

| # | Backlog requirement | Design response |
|---|---|---|
| R1 | Verify Authenticode (`WinVerifyTrust`) | `WinTrustFileInfo.IsSigned(Environment.ProcessPath)` |
| R2 | Verify binary hash | `SHA256.Create()(File.ReadAllBytes(Environment.ProcessPath))` |
| R3 | Report to backend integrity endpoint | `IBackendClient.ReportIntegrityAsync(report)` → `POST /rest/v1/integrity_reports` |
| R4 | Negative verdict → alert + degradation | Add `BinaryIntegrityFailure` to `EnforcementIssueType`; wire to existing `IEnforcementLevelMonitor` pipeline (T12 already defines `DEGRADED`) |
| R5 | Without breaking on false positives | Mechanism not prescribed by backlog; implement simplest viable approach (see §False Positive Handling) |
| R6 | NativeAOT/R2R publish | `<PublishAot>true</PublishAot>` in `ControlParental.Service.csproj` Release |
| R7 | IL obfuscator for domain/motor | `ControlParental.Domain.dll` — interpretation from architecture (contains `RulesEngine.cs`) |
| R8 | Preserve source-gen serialization, EF, reflection | `[DynamicallyAccessedMembers]` on DbContext + `[JsonDerivedType]` on IPC DTOs |
| R9 | Anti-debug basic stub | Simple `Debugger.IsAttached` check; not a primary defense |
| R10 | Signed build → valid server verdict | Test: signed test binary passes `WinVerifyTrust` |
| R11 | Altered/unsigned build → detected | Test: hash mismatch or `WinVerifyTrust=false` triggers `BinaryIntegrityFailure` |
| R12 | AOT/obfuscated build still serializes/queries DB | Test: full suite passes on published output |

---

## Scope Decisions

### In Scope (R1–R12 only)
- Local binary integrity self-check (Authenticode + SHA256)
- Report to T14 `POST /rest/v1/integrity_reports`
- Negative verdict → alert + degraded via existing T12 pipeline
- Non-catastrophic false-positive handling (simplest viable mechanism)
- NativeAOT publish with trimming rescue
- IL obfuscation of Domain.dll (interpreted from "dominio/motor")
- Anti-debug stub

### Out of Scope (explicitly deferred or not in backlog)
- **TPM/DHA attestation** — "Opcional MANAGED" per backlog
- **CI signing pipeline** — not mentioned in T23
- **New degraded-mode behavior** — T12 already defines DEGRADED; T23 only adds a trigger cause
- **Specific false-positive mechanism** — backlog requires non-catastrophic outcome, not a specific algorithm

---

## Data Flow

```
Service startup / periodic loop (every 30s via AntiTamperMonitor)
│
├─► LocalCheck()
│    ├─ WinTrustFileInfo.IsSigned(ProcessPath)  → bool
│    └─ SHA256(ProcessPath)                    → hex string
│
├─► IBackendClient.ReportIntegrityAsync(IntegrityReport)
│    └─ POST /rest/v1/integrity_reports
│        (T14 body: { binary_hash, signature_valid, timestamp })
│        HTTP 2xx = accepted; non-2xx = retry with backoff
│
├─ Backend response received?
│    ├─ HTTP error (network/server) → log, retry later (not catastrophic)
│    └─ HTTP 2xx with verdict field?
│         ├─ "revoked" → RecordTamperEvent(BinaryIntegrityFailure)
│         │              → EnforcementLevelMonitor.AddIssue(Severe)
│         │              → EnforcementLevel → Degraded (T12)
│         └─ "trust" / "unknown" / absent → no action
│
├─ False positive case:
│    Local check PASSES but server says "revoked"
│    → log warning, do NOT call RecordTamperEvent (non-catastrophic)
│
└─ Anti-debug:
     If Debugger.IsAttached → log (not a tamper event)
```

---

## File Changes

| File | Action | Backlog basis |
|---|---|---|
| `src/ControlParental.Domain/Enums.cs` | Modify | R4: Add `BinaryIntegrityFailure` to `EnforcementIssueType` |
| `src/ControlParental.Domain/IAntiTamperMonitor.cs` | Modify | R3: add `PerformBinaryIntegrityCheckAsync()` |
| `src/ControlParental.Service/IntegrityReport.cs` | Create | R3: `IntegrityReport` record sent to backend |
| `src/ControlParental.Service/IntegrityChecker.cs` | Create | R1+R2: `WinVerifyTrust` + SHA256 implementation |
| `src/ControlParental.Service/AntiTamperMonitor.cs` | Modify | R3+R4: wire integrity check into existing loop; route verdict to `EnforcementLevelMonitor` |
| `src/ControlParental.Service/ControlParental.Service.csproj` | Modify | R6: `<PublishAot>true</PublishAot>` Release; R8: trimming rescue ItemGroup |
| `Directory.Build.props` | Modify | R7: obfuscation tool reference |
| `src/ControlParental.Domain/IpcMessage.cs` | Modify | R8: `[JsonDerivedType]` for AOT-safe IPC |
| `src/ControlParental.Domain/ControlParentalDbContext.cs` | Modify | R8: `[DynamicallyAccessedMembers]` for trimming rescue |

**Not created (deferred or out of scope):**
- `IAttestationClient.cs` — TPM/DHA is "Opcional MANAGED"
- `IntegrityVerdictCache.cs` — not prescribed by backlog; implemented inline with simplest mechanism
- CI signing pipeline — not in T23

---

## Key Interface Changes

### Enums.cs — Add EnforcementIssueType
```csharp
// Added to EnforcementIssueType enum (existing enum extended)
BinaryIntegrityFailure,
```

### IAntiTamperMonitor.cs — Add method
```csharp
// Added to existing interface
Task PerformBinaryIntegrityCheckAsync(CancellationToken ct = default);
```

### IntegrityReport.cs (new)
```csharp
public sealed record IntegrityReport(
    string BinaryHash,       // SHA256 hex
    bool SignatureValid,     // WinVerifyTrust result
    string ExecutablePath,
    DateTimeOffset Timestamp);
```

### IntegrityChecker.cs (new — Service layer)
```csharp
public interface IIntegrityChecker
{
    Task<IntegrityCheckResult> CheckLocalIntegrityAsync(CancellationToken ct = default);
}

public sealed record IntegrityCheckResult(
    bool IsSignatureValid,   // WinVerifyTrust passed
    string BinaryHash,       // SHA256 hex, 64 chars
    string ExecutablePath);
```

---

## False Positive Handling (R5 — simplest viable mechanism)

**Backlog says:** "sin romper ante falsos positivos" (without breaking on false positives).

**Not prescribed by backlog:** cache strategy, retry count, grace period, or any specific algorithm.

**Implementation:** T23 implements a **best-effort local-first** approach:
1. If local `WinVerifyTrust` passes AND binary hash matches the value sent in the last successful report, treat the binary as locally trusted.
2. If server returns `revoked` but local check is trusted → log a warning, emit a `TamperEventType.BinaryIntegrityAnomaly` with severity `Warning` (not `Severe`) — does NOT drive to `Degraded`.
3. If local check fails → `BinaryIntegrityFailure` with `Severity.Severe` → `Degraded` (T12).

This satisfies R5 without prescribing a specific caching mechanism not found in the backlog.

**Why not more complex:** The backlog does not specify the mechanism. A simple local-trust gate is sufficient to demonstrate non-catastrophic behavior.

---

## AOT/R2R Configuration (R6, R7, R8)

### Service.csproj — Release publish
```xml
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <PublishAot>true</PublishAot>
  <PublishTrimmed>true</PublishTrimmed>
</PropertyGroup>
```

### Trimming rescue — Domain.dll entities and DbContext
```csharp
// In ControlParentalDbContext.cs
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public partial class ControlParentalDbContext { }
```

### AOT-safe IPC — IpcMessage.cs
```csharp
[JsonSerializable(typeof(ForegroundChanged))]
[JsonSerializable(typeof(OverlayShowCommand))]
[JsonSerializable(typeof(OverlayHideCommand))]
// ... all IpcMessage subtypes
public partial class IpcMessageContext : JsonSerializerContext { }
```

### Obfuscation — Domain.dll only
**Interpretation basis:** "ofuscador IL del dominio/motor" + architecture shows `RulesEngine.cs` lives in `ControlParental.Domain`.

ConfuserEx as `dotnet tool` in `Directory.Build.props`:
```xml
<PropertyGroup>
  <ObfuscateDomainDotNetTool>confuserex</ObfuscateDomainDotNetTool>
</PropertyGroup>
<Target Name="ObfuscateDomain" AfterTargets="Publish" 
        Condition="'$(Configuration)' == 'Release'">
  <Exec Command="confuserize $(OutputPath)ControlParental.Domain.dll ..." />
</Target>
```

**Not obfuscating:** `ControlParental.Service.dll` (P/Invoke layer), `ControlParental.SessionAgent.dll`, `ControlParental.App.UI.dll`.

---

## Anti-Debug Stub (R9)

```csharp
// In IntegrityChecker.CheckLocalIntegrityAsync
if (System.Diagnostics.Debugger.IsAttached)
{
    // Log but do not treat as tamper
    // "Anti-debug basic stub" per backlog — not a primary defense
}
```

---

## Verification Matrix

| Done criterion | Test approach |
|---|---|
| Firma/hash verificados en servidor | Unit: `IntegrityChecker` returns correct SHA256 (64 hex) + `WinVerifyTrust` result. Integration: signed binary passes, unsigned fails. |
| Reacción sin falsos positivos catastróficos | Unit: local check trusted + server says `revoked` → `TamperEventType.BinaryIntegrityAnomaly` (Warning), not `BinaryIntegrityFailure` (Severe) → no DEGRADED. |
| AOT/ofuscación sin romper serialización/EF | `dotnet build -c Release && dotnet test` on published output; all existing tests pass. |
| DoD-G | All above pass + full suite green post-AOT. |

---

## Risks

| Risk | Severity | Mitigation |
|---|---|---|
| T14 backend response format unknown | Med | T23 sends report; non-2xx = retry (not catastrophic); verdict field handled if present |
| EF Core queries break under trimming | High | Add `[DynamicallyAccessedMembers]` before enabling `PublishTrimmed`; test with trimmed build first |
| JSON serialization fails in AOT for IPC | High | Add `[JsonDerivedType]` + `[JsonSerializable]` before AOT publish |
| Obfuscation breaks EF/JSON reflection | Med | Obfuscate Domain only; run full suite post-obfuscation |

---

## Open Questions

1. **Backend response format**: T14 does not specify whether `POST /rest/v1/integrity_reports` returns a verdict field. T23 will handle `verdict` if present; if absent, no reaction is possible from the server side.
2. **Obfuscation tool**: No tool is specified in the backlog. ConfuserEx proposed; team must confirm.
