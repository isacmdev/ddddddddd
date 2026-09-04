# Verification Report

**Change**: `remote-signal-integrity` — SDD7 Unit 4A Backend Authority + Durable Persistence only  
**Branch**: `feat/sdd7-4-runtime-integrity-enforcement`  
**Base / target**: `3082559551ce4aa4363bd14a9e09dda0b1176f5f` / `feat/sdd7-3-foreground-realtime`  
**Worktree**: `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-4`  
**Mode**: Strict TDD, hybrid persistence, final fresh authoritative independent report-only verification  
**Verification time**: 2026-08-21, local offset `-05:00`  
**Verdict**: **FAIL**

## Executive Summary

All required fresh sequential `--no-restore` execution passed: Domain and Service builds, portable-PDB Service test build, 9/9 runtime-path tests, 67/67 Unit 4A focus, 1,181/1,181 Service tests, 192/192 App.UI tests, and portable-PDB coverage. The authority, stable identity, real refresh, cancellation, complete physical snapshot, exact recovery, recreation, parent escalation scheduling, and parent Stop/Dispose remediations are now supported by source inspection and runtime evidence. The CODE+TEST slice is exactly **379/400** changed lines, with no deleted file/test or dependency/configuration drift.

Unit 4A still fails its explicit slice-purity acceptance. `IntegrityVerdictHandler` adds a new `stateGate` and serializes `HandleVerdict`, `HandleLocalFailure`, and shadow-mode state relative to the parent. The re-sliced tasks assign **concurrent verdict serialization** to Unit 4B, and acceptance item 9 forbids Unit4B race leakage in Unit4A. No Unit 4A concurrent-verdict RED or runtime test covers this new synchronization. This is a real Unit4B implementation fragment in the 4A diff, despite `RunMonitorLoopAsync` being absent and the monitor/timer baseline otherwise being restored.

**Unit 4A is not approved for its local commit boundary.** The expected cumulative state remains **9/14**; unchecked tasks 4.1–4.3 are not themselves a Unit4A failure. Unit4B remains mandatory and full SDD7 is not archive-ready.

## Verification Boundary and Freshness

- Read the full proposal, all four delta specs, design, re-sliced tasks, cumulative apply progress, and the three historical reports requested by the verifier.
- Historical reports were preserved unchanged. Fresh SHA-256 values before writing this report:
  - `verify-report-unit-4.md`: `FA44D9A41B3ADF96054E4AB6EDA91CF78B2980828A5093C51E78DEDFF3798EEE`
  - `verify-report-unit-4a.md`: `88A7080A40242BA708625EF900C5E9C51B87ECA9767C92B5E222E8324C19D5F1`
  - `verify-report-unit-4a-final.md`: `701CED0D722D2D6B0164FED7588B89D7C0B738498B994ED06A4DBD83A78531BB`
- CodeGraph was attempted first by checking the exact worktree. The exact worktree has no `.codegraph` index; direct focused inspection was used and no index was created.
- Inspected the exact source/test diff, the complete new runtime test, production `BackendClient`, `BackendIdentityCoordinator`, `EnforcementLevelMonitor`, `FileIssueStore`, durable records, DI, parent behavior, callers/defaults, deletions, test weakening, dependencies, and configuration drift.
- No implementation, test, task, apply, spec, design, configuration, dependency, git, branch, worktree, Unit4B, Unit5, external system, commit, push, or PR mutation was performed. This report is the only authored artifact.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD7 tasks | 14 |
| Cumulative checked | 9 |
| Intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| Unit4A acceptance items | 10 |
| Fully compliant | 9 |
| Failing | 1 (slice purity) |
| Runtime-path tests | 9/9 passed |

The planned partial 9/14 task state is not treated as a Unit4A incomplete-task critical.

## Workspace, Drift, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD | Expected branch; HEAD equals exact base `3082559551ce4aa4363bd14a9e09dda0b1176f5f` |
| Target / merge-base | Target and merge-base both equal the exact base |
| Tracked CODE+TEST paths | Exactly six expected modified files |
| Untracked CODE+TEST | Only `IntegrityRuntimePathTests.cs` |
| Deleted files/tests | None |
| Tracked `git diff --check` | Exit 0 |
| Untracked test check | Expected `--no-index` difference exit 1; no whitespace diagnostic |
| Dependency/project/config drift | None |
| Restore | None; every fresh .NET command used `--no-restore` |
| `.codegraph` artifact | Absent |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Domain/IEnforcementLevelMonitor.cs` | 20 | 1 | 21 |
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 48 | 13 | 61 |
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | 25 | 6 | 31 |
| `src/ControlParental.Service/Program.cs` | 3 | 2 | 5 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 1 | 0 | 1 |
| `tests/ControlParental.Service.Tests/IntegrityVerdictHandlerTests.cs` | 20 | 3 | 23 |
| `tests/ControlParental.Service.Tests/IntegrityRuntimePathTests.cs` | 237 | 0 | 237 |
| **Total** | **354** | **25** | **379/400** |

No test file or production file was deleted. The only test deletions are three replaced lines in `IntegrityVerdictHandlerTests`; no scenario/assertion weakening was found. The stale test name `IsShadowMode_DefaultsToTrue` remains misleading while its assertion correctly expects `false`.

## Fresh Build, Test, and Coverage Execution

All commands ran sequentially in the exact worktree.

| Evidence | Exact command | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain build | `dotnet build src\ControlParental.Domain\ControlParental.Domain.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:WarningLevel=0` | `10:07:52.2866249` → `10:07:55.0664476` | 0 | 0 errors |
| Service build | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:WarningLevel=0` | `10:08:00.4696775` → `10:08:07.9713360` | 0 | 0 errors; existing NU1601 |
| Service test portable-PDB build | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable -p:WarningLevel=0` | `10:08:15.9293552` → `10:08:30.9876410` | 0 | 0 errors; existing NU1601/NU1701 |
| Exact runtime path | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityRuntimePathTests"` | `10:08:39.2760874` → `10:08:44.6600017` | 0 | **9 passed** |
| Unit4A focus | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests"` | `10:08:51.9618467` → `10:08:55.2078198` | 0 | **67 passed** |
| Full Service | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `10:09:01.5271015` → `10:09:14.2801716` | 0 | **1,181 passed**; existing duplicate-ID notice |
| Full App.UI | `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `10:09:20.0848826` → `10:09:23.7694504` | 0 | **192 passed** |
| Portable-PDB coverage | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug -p:DebugType=portable --collect:"XPlat Code Coverage" --results-directory tests\ControlParental.Service.Tests\TestResults\coverage-unit4a-authoritative-final-20260821 --verbosity minimal -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | `10:09:31.0389524` → `10:10:08.4791123` | 0 | **1,181 passed**; Cobertura produced |

**Coverage artifact**: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4a-authoritative-final-20260821/d3b3a506-8393-4717-b7f6-cfa4bde03937/coverage.cobertura.xml`  
**Aggregate coverage**: 54.02% line / 58.03% branch (informational; no aggregate threshold configured).

## Runtime-Backed Unit4A Acceptance Matrix

| # | Acceptance | Fresh evidence | Result |
|---:|---|---|---|
| 1 | Same production coordinator, current backend constructor, active default policy | `Program` registers one singleton coordinator and supplies it to both `BackendClient` and `AntiTamperMonitor`; runtime tests pass the same real coordinator; handler defaults `shadowMode:false` | ✅ COMPLIANT |
| 2 | Real HTTP → parser → policy → file-backed store | Runtime suite uses current `BackendClient`, real parser, real handler, real enforcement, and `FileIssueStore` for revoked/trust and non-definitive paths | ✅ COMPLIANT |
| 3 | Exact semantic stable key and identity scope | Runtime persistence asserts `IssueKey(0, BinaryIntegrityFailure, "integrity/binary", "device-a")`; generation is excluded; record equality/JSON include `IdentityScope`; device B key is unequal | ✅ COMPLIANT |
| 4 | Real refresh | Credential is expired through `MutableTimeProvider`; real `GetDefinitiveSessionAsync` enters production `RefreshAsync`; generation increments, device remains `device-a`, HTTP uses `token-a-refreshed`, and trust resolves the pre-refresh key | ✅ COMPLIANT |
| 5 | Real caller cancellation | Controlled HTTP blocks on the supplied request token; cancellation fires, handler stops, `OperationCanceledException` is observed, cancellation-rethrow lines 608–609 each have one hit, and the complete durable snapshot is unchanged | ✅ COMPLIANT |
| 6 | Exact non-degradation matrix | Unknown, pending, absent, malformed, stale generation, refresh timeout, HTTP 503/transport failure, and caller cancellation execute production paths and compare complete serialized durable snapshots | ✅ COMPLIANT |
| 7 | Duplicate and exact recovery isolation | Duplicate revoked leaves one semantic active issue; exact-key recovery after recreation resolves only the pre-refresh target; same-device `HookTimeout` and device-B integrity records are included in the exact serialized comparison and retain cardinality/fields | ✅ COMPLIANT |
| 8 | Recreation and authority boundary | Real file store, enforcement, and policy are recreated; physical recovery works; local evidence remains report-only; no client baseline, App.UI, local, or UI authority was added | ✅ COMPLIANT |
| 9 | Slice purity and parent lifecycle/timer equivalence | `RunMonitorLoopAsync` is absent; `ScheduleEscalation(timestamp)` is restored and has four hits; Stop/Dispose match parent. However, new `stateGate` locking implements Unit4B-owned concurrent verdict serialization in Unit4A without concurrent runtime proof | ❌ FAILING |
| 10 | Async interfaces, DI, regressions, and physical assertions | Service overrides durable async methods; defaults keep other implementations coherent; production DI composes; Service/App.UI regressions pass; assertions inspect physical state before cleanup | ✅ COMPLIANT |

## Behavioral Compliance Matrix

| Spec scenario | Runtime evidence | Result |
|---|---|---|
| Runtime integrity — valid evidence receives trust | Real refreshed authenticated token, parser, policy threshold, exact-key durable resolution after recreation | ✅ COMPLIANT |
| Runtime integrity — revocation changes enforcement | Four real revoked responses create one active severe device-scoped issue | ✅ COMPLIANT |
| Runtime integrity — unknown/transient is non-degrading | Exact snapshots for unknown, pending, absent, malformed, stale, timeout, HTTP failure, and caller cancellation | ✅ COMPLIANT |
| Runtime integrity — restart/recovery preserve semantics | Recreated durable components recover only target key and preserve same-/other-device unrelated records exactly | ✅ COMPLIANT |
| Runtime integrity — deferred release scope not claimed | No forbidden external readiness claim | ✅ COMPLIANT |
| Offline enforcement — revoked evidence persists scoped issue | Real authority chain creates one durable scoped issue | ✅ COMPLIANT |
| Offline enforcement — semantic recovery survives restart | Duplicate cardinality and exact isolated recovery pass | ✅ COMPLIANT |
| Offline enforcement — transient failure does not degrade | Complete physical snapshots remain unchanged for every required Unit4A outcome | ✅ COMPLIANT |
| Offline enforcement — concurrent recovery serialized | Correctly remains a Unit4B requirement, but Unit4A contains an untested partial handler-serialization implementation | ❌ SLICE VIOLATION |

**In-scope behavioral compliance**: 8/8 Unit4A behavior scenarios pass. The verdict fails on autonomous slice purity, not on authority/persistence behavior.

## Correctness and Call-Path Inspection

`ControlParentalService` → `AntiTamperMonitor` → local `IntegrityChecker` evidence → `BackendClient.ReportIntegrityAsync` → `IBackendIdentityCoordinator.GetDefinitiveSessionAsync` / `RefreshAsync` → authenticated HTTP/parser → `IntegrityVerdictHandler` → async `EnforcementLevelMonitor` → serialized `FileIssueStore`.

| Invariant | Finding |
|---|---|
| Backend authority | Production handler is active by default; grace period remains distinct from shadow mode |
| Canonical identity | Same singleton coordinator owns transport authorization and monitor identity capture |
| Stable issue identity | Cause is exactly `integrity/binary`, `SessionId` is fixed `0`, and `IdentityScope` is device ID |
| Stale generation | Post-transport state comparison suppresses stale completion; runtime branch passes |
| Definitive-only mutation | Only successful exact `trust`/`revoked` enters durable reaction |
| Local evidence | Handler may observe it, but monitor never applies its reaction to durable enforcement |
| Parser outcomes | Missing/malformed/arbitrary verdicts remain non-definitive and physically non-mutating |
| Retry owner | `BackendClient.SendAuthenticatedAsync` remains the only bounded transport retry owner |
| Exact recovery | `FileIssueStore.ResolveAsync` uses dictionary equality on the full `IssueKey`; runtime comparison includes unrelated same-device and other-device records |
| Parent timer baseline | `ScheduleEscalation(timestamp)` restored; monitor Stop/Dispose timer handling matches parent |
| Unit4B leakage | New `stateGate` serializes mutable verdict state and shadow access, which tasks explicitly assign to Unit4B |

## Changed-File Coverage

| Changed production file/scope | Line | Branch | Required hits / assessment |
|---|---:|---:|---|
| `IEnforcementLevelMonitor` defaults | 0% | N/A | Compatibility defaults uncovered; production Service overrides both (warning) |
| `AntiTamperMonitor` | 83.97% | 84.09% | Binary check 96.00%/75.00%; reaction path 71.05%/57.14% |
| `IntegrityVerdictHandler` | 92.94% | 86.95% | `HandleRevokedVerdict` 97.50%/90.00%; `ScheduleEscalation` 90.00%/50.00%; schedule call line 318 hit 4 times |
| `Program` | 88.31% | 80.00% | DI source inspected; current constructor path compiles and full Service regression passes |
| `BackendClient.ReportIntegrityAsync` | 100% | 100% | caller-cancellation catch/rethrow lines 608–609 each hit once |
| `BackendIdentityCoordinator.RefreshAsync` | 80.39% | 66.66% | successful production refresh is joined to stable-key recovery by runtime test |
| `EnforcementLevelMonitor` | 94.76% | 86.95% | durable add 82.35%/75.00%; resolve 100%/100% |

Coverage is adequate for required Unit4A authority/persistence branches. Defensive handler/interface branches remain warnings. Coverage does not cure the untested Unit4B synchronization leakage.

## Strict TDD Audit

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Unit4A evidence exists in cumulative apply progress |
| Genuine original RED | ✅ retained summary | Historical local-evidence authority failure has command, timestamp, exit 1, and failing mutation observation |
| Raw original RED artifact | ⚠️ | Not retained; none is fabricated |
| Final canonical rewrite RED | ⚠️ MISSING | Apply progress explicitly states the final canonical rewrite followed production edits |
| Fresh GREEN | ✅ | 9/9 runtime, 67/67 focus, 1,181/1,181 Service, 192/192 App.UI |
| Triangulation | ✅ authority/persistence | Refresh, cancellation, stale, timeout, malformed, exact recovery, duplicate, and restart paths are distinct |
| Safety nets | ✅ | Fresh builds, full regressions, coverage, and diff checks pass |
| Unit4B leakage TDD | ❌ | New concurrent verdict serialization has neither a Unit4A RED nor concurrent runtime coverage and belongs to Unit4B |

The missing final canonical RED remains an explicit strict-TDD warning, not fabricated evidence. The untested out-of-slice `stateGate` implementation is independently blocking because it violates the autonomous Unit4A boundary.

## Test Layer Distribution and Assertion Quality

| Layer | Tests | Files | Notes |
|---|---:|---:|---|
| Unit/mock | 58 focused | 3 | Checker, handler, monitor |
| Production-component harness | 9 | 1 | Real coordinator/backend/parser/policy/enforcement/file store; controlled HTTP and ports |
| E2E/external | 0 | 0 | Correctly excluded; no external readiness claim |
| **Total focused** | **67** | **4** | |

No tautology, ghost loop, assertion-free production call, or cleanup-before-assertion was found. The fixed payload array is non-empty. Cancellation has observable start/stop/OCE assertions. Exact recovery compares every non-target serialized record, including same-device unrelated state. The only assertion-quality warning is the stale `IsShadowMode_DefaultsToTrue` test name.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Backend owns authoritative hash reference | ✅ | No client baseline/API/persistence added |
| Checker → authenticated backend → policy → durable enforcement | ✅ | Complete production-component runtime path |
| Definitive verdicts only mutate | ✅ | Exact successful verdict gate |
| Unknown/malformed/transient non-degrading | ✅ | Full physical snapshot matrix |
| Stable identity-scoped issue | ✅ | Generation excluded; device included |
| Restart-safe exact recovery | ✅ | Target-only resolution and exact unrelated preservation |
| Parent timer/Stop/Dispose behavior | ✅ | Schedule restored with hits; monitor lifecycle matches parent |
| Unit4A/Unit4B surgical split | ❌ | Unit4B-owned concurrent verdict serialization remains in Unit4A |
| One retry owner | ✅ | No monitor-side retry owner |

## Issues Found

### CRITICAL

1. **Unit4B race implementation leaks into Unit4A.** Relative to parent, `IntegrityVerdictHandler` adds `stateGate` and serializes `HandleVerdict`, `HandleLocalFailure`, `IsShadowMode`, and `DisableShadowMode`. The re-sliced tasks explicitly assign concurrent verdict serialization to Unit4B, while Unit4A acceptance explicitly forbids Unit4B race leakage. No concurrent-verdict test or valid RED covers this implementation fragment. The autonomous 4A commit boundary is therefore impure.

### WARNING

1. Strict TDD has no genuine behavioral RED for the final canonical rewrite, and the retained original RED has no durable raw log artifact.
2. Existing NU1601/NU1701 warnings and one duplicate xUnit test-ID discovery notice remain; no dependency declaration changed.
3. Default async compatibility methods on `IEnforcementLevelMonitor` remain uncovered; the production Service implementation overrides both.
4. `IsShadowMode_DefaultsToTrue` remains a stale test name while correctly asserting the new default is `false`.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

Unit4A is **not approved for its local commit boundary**. The cancellation/refresh/snapshot/timer remediations now pass fresh authoritative verification, but the slice still contains an untested Unit4B-owned concurrent-verdict serialization change, violating the explicit autonomous boundary.

Cumulative state remains **9/14**. Unit4B is required, final full Unit4 verification remains required, and full SDD7 is **not archive-ready**. No commit, push, PR, Unit4B, or Unit5 work was performed.
