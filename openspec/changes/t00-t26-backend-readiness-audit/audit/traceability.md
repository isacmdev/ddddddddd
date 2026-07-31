# T00–T26 Traceability Matrix

Snapshot: `snapshot.json` (2026-07-29 UTC). Primary coverage is exactly once; P12 is cross-cutting evidence and intentionally has no primary T-unit.

| Unit | Primary partition | Current-source evidence / lead | Executable state | Gap or disposition |
|---|---|---|---|---|
| T00 | P1 | `ControlParental.sln`; `Directory.Build.props`; untracked App.UI entry point | Pending: solution build blocked | Packaging and build identity require blocker removal |
| T01 | P2 | `src/ControlParental.Domain/Policy.cs`; `PolicyDbEntity.cs`; policy tests | Pending: no fresh suite receipt | Contract shape is source-supported; runnable proof unavailable |
| T02 | P2 | `src/ControlParental.Domain/RulesEngine.cs`; `RulesEngineTests.cs` | Pending | Rule behavior needs a clean executable receipt |
| T03 | P3 | `ControlParental.Service/ControlParentalDbContext.cs`; `OutboxManager.cs` | Pending | SQLite/outbox integration remains unexecuted in this snapshot |
| T04 | P2 | `ControlParental.Service/TimeProvider.cs`; time tests | Pending | Clock and server-date behavior not executable here |
| T05 | P7 | `SessionAgent/ForegroundWatcher.cs`; identity resolver tests | Pending | Windows API path requires runnable test/build |
| T06 | P7 | `Service/UsageAccumulator.cs`; usage tests | Pending | Current source and tests exist; suite is blocked |
| T07 | P7 | `Service/UsageReconciler.cs`; reconciliation tests | Pending | Permission/data degradation needs runtime evidence |
| T08 | P8 | `SessionAgent/OverlayWindow.cs`; overlay tests | Pending | UI/session behavior not runnable from this audit |
| T09 | P8 | `Service/WorkstationLockManager.cs`; lock tests | Pending | OS lock/unlock path needs runtime evidence |
| T10 | P9 | `Service/ServiceRecoveryManager.cs`; SCM/task scheduler code | Pending | Service recovery requires elevated host validation |
| T11 | P8 | `Service/EnforcementEngine.cs`; enforcement tests | Pending | Offline loop has source/test coverage but no executable receipt |
| T12 | P9 | `Service/EnforcementLevelMonitor.cs`; health tests | Pending | Health degradation matrix not executable |
| T13 | P9 | `Service/AntiTamperMonitor.cs`; integrity tests | Pending | Platform and policy reaction need runtime/backend evidence |
| T14 | P4 | `Service/BackendClient.cs`; `apis.md`; backend blocker lead | Pending/external | Contract is client-visible; backend implementation is external |
| T15 | P11 | Backlog states T15 merged into T14 | Not applicable | Retired intentionally; no duplicate primary assignment |
| T16 | P6 | `Service/SecretStore.cs`; `AclHardener.cs`; secret tests | Pending | OS secret-store and ACL evidence unavailable |
| T17 | P4 | `Service/DeviceAuthenticator.cs`; authentication tests | Pending/external | Client seam exists; JWT/RLS proof belongs to backend/staging |
| T18 | P4 | `Service/ScheduledWorkService.cs`; backend client | Pending | Sync contract and failure behavior need clean execution |
| T19 | P5 | `WnsNotificationService.cs`; `WnsHostedService.cs`; WNS tests | Pending/external | WNS identity and endpoint delivery are not locally provable |
| T20 | P9 | `ScheduledWorkService.cs`; scheduler tests | Pending | Timer/retry behavior requires runnable evidence |
| T21 | P5 | `RealtimeSubscriber.cs`; realtime tests | Pending/external | Foreground-only backend channel needs integration proof |
| T22 | P4 | `Program.cs` TLS handler; pinning validator/tests | Pending | Collector blocked before analyzer/build execution |
| T23 | P6 | `IntegrityChecker.cs`; `IntegrityVerdictHandler.cs`; tests | Pending/external | Local check exists; remote verdict cannot be confirmed here |
| T24 | P10 | pairing service and onboarding pairing tests | Pending/external | Pairing identity/RLS depends on backend staging |
| T25 | P10 | onboarding strings/pages and localization tests | Pending | Source evidence exists; UI suite blocked |
| T26 | P10 | onboarding state service/pages and T26 tests | Pending | App composition/build blocker prevents executable confirmation |

## Coverage validation

- Primary assignments: 27 roadmap units, exactly one assignment for each `T00`–`T26`.
- Missing primary units: none.
- Duplicate primary units: none.
- Retired unit: T15 is explicitly disposed in P11 as merged into T14; it is not silently omitted.
- P12: cross-cutting test evidence only; no T-unit is assigned a second primary partition.
- All partitions P1–P12 have a verdict, executable state, evidence reference, and gap status in `partition-report.md`.
