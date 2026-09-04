# Verification Report

**Change**: `remote-signal-integrity` — authorized Unit 4A Backend Authority + Durable Persistence slice only
**Branch**: `feat/sdd7-4-runtime-integrity-enforcement`
**Base / target**: `3082559551ce4aa4363bd14a9e09dda0b1176f5f` / `feat/sdd7-3-foreground-realtime`
**Worktree**: `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-4`
**Mode**: Strict TDD, hybrid persistence, fresh independent report-only verification
**Verification date**: 2026-08-21 (`-05:00`)
**Verdict**: **FAIL**

## Executive Summary

All fresh no-restore builds and tests passed: the real-path filter passed 2/2, the authorized focused filter passed 60/60, full Service passed 1,174/1,174, full App.UI passed 192/192, and portable-PDB coverage completed. The diff is isolated to the seven authorized CODE+TEST paths and is exactly 286/400 changed lines; no dependency, project, or configuration drift was found.

Unit 4A nevertheless fails its autonomous acceptance. The runtime harness does not exercise production canonical identity transport, does not prove timeout/cancellation/stale outcomes preserve the exact durable state, and does not preserve one stable key across a same-device credential-generation refresh. It also does not seed/preserve an unrelated issue or prove duplicate verdict idempotency after the issue is active. Finally, Unit 4B lifecycle work was not fully restored to parent behavior: an uncalled cancellation-loop method remains in the diff, and `Dispose` no longer disposes either timer.

**Unit 4A is NOT approved for a local feature-chain commit boundary.** Top-level Unit 4 remains intentionally incomplete at cumulative **9/14**, is not archive-ready, and still requires Unit 4B plus final full-Unit verification. No implementation was fixed and no Unit 4B/Unit 5 work was prepared.

## Verification Boundary and Freshness

- Read exploration, proposal, all four delta specs, design, re-sliced tasks, cumulative apply progress, and the unchanged historical full-Unit FAIL `verify-report-unit-4.md`.
- `.codegraph/` was absent in the exact worktree. Per the explicit no-artifact constraint, no index was created; focused direct inspection was used.
- Inspected the seven expected changed paths, exact diff/status/numstat/check, production `BackendClient`, `BackendIdentityCoordinator`, identity state transitions, `EnforcementLevelMonitor`, `FileIssueStore`, interface implementations/callers, DI composition, and relevant tests.
- The historical full-Unit FAIL remains unchanged and is not reused as this verdict.

## Completeness

| Metric | Value |
|---|---:|
| Cumulative SDD tasks | 14 |
| Cumulative checked | 9 |
| Cumulative intentionally unchecked | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |
| Unit 4A autonomous acceptance items | 13 |
| Fully compliant | 5/13 |
| Partial | 3/13 |
| Failing/untested | 5/13 |

The expected unchecked top-level tasks `4.1`–`4.3` are **not** classified as a Unit 4A defect.

## Workspace, Drift, Restore, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD | Expected branch; HEAD equals exact base `3082559551ce4aa4363bd14a9e09dda0b1176f5f` |
| Target / merge-base | Target and merge-base both equal exact base |
| Tracked paths | Exactly six expected tracked modifications |
| Untracked CODE+TEST | Only expected `IntegrityRuntimePathTests.cs` |
| OpenSpec | Cumulative artifacts remain untracked; prior artifacts unchanged before this report |
| Dependency/config/project drift | None (`*.csproj`, props, targets, lockfile, `global.json`, NuGet/config) |
| Generated cache drift | No generated cache appeared in `git status --untracked-files=all`; coverage/build output remains ignored execution output |
| Restore | No restore executed; every fresh `dotnet` command used `--no-restore` |
| `git diff --check` | Exit 0; untracked `--no-index --check` returned expected difference exit 1 with no whitespace diagnostic |
| CodeGraph artifact | Absent before and after verification |

### Exact CODE+TEST Budget

| File | Add | Delete | Total |
|---|---:|---:|---:|
| `src/ControlParental.Domain/IEnforcementLevelMonitor.cs` | 19 | 0 | 19 |
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 63 | 22 | 85 |
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | 25 | 7 | 32 |
| `src/ControlParental.Service/Program.cs` | 3 | 2 | 5 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 1 | 0 | 1 |
| `tests/ControlParental.Service.Tests/IntegrityVerdictHandlerTests.cs` | 20 | 3 | 23 |
| `tests/ControlParental.Service.Tests/IntegrityRuntimePathTests.cs` | 121 | 0 | 121 |
| **Total** | **252** | **34** | **286/400** |

## Fresh Build, Test, and Coverage Execution

All commands ran sequentially in the exact worktree.

| Evidence | Exact command | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Domain build | `dotnet build "src\ControlParental.Domain\ControlParental.Domain.csproj" --no-restore --no-incremental --configuration Debug --verbosity minimal` | `09:08:42.530` → `09:08:51.361` | 0 | 0 errors; existing warning corpus |
| Service product build | `dotnet build "src\ControlParental.Service\ControlParental.Service.csproj" --no-restore --no-incremental --configuration Debug --verbosity minimal` | `09:08:51.363` → `09:09:09.380` | 0 | 0 errors; existing NU1601/analyzer corpus |
| Service test build, portable PDB | `dotnet build "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-incremental --configuration Debug --verbosity minimal -p:DebugType=portable` | `09:09:09.380` → `09:09:47.487` | 0 | 0 errors; existing warning corpus |
| Exact real runtime path | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityRuntimePathTests"` | `09:10:23.936` → `09:10:29.123` | 0 | 2 passed, 0 failed/skipped |
| Focused monitor/policy/runtime | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests"` | `09:10:29.125` → `09:10:31.930` | 0 | 53 passed |
| Authorized Unit 4A focus | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests"` | `09:11:04.522` → `09:11:07.364` | 0 | 60 passed |
| Full Service regression | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal` | `09:10:31.930` → `09:10:46.582` | 0 | 1,174 passed; existing duplicate-ID notice |
| Full App.UI regression | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal` | `09:10:46.583` → `09:10:52.194` | 0 | 192 passed |
| Portable-PDB coverage | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --collect:"XPlat Code Coverage" --results-directory "tests\ControlParental.Service.Tests\TestResults\coverage-unit4a-fresh-20260821-0911" --verbosity minimal -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | `09:11:07.366` → `09:11:41.876` | 0 | 1,174 passed; Cobertura produced |

**Coverage artifact**: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4a-fresh-20260821-0911/d3f2275d-a118-46d0-b791-a2d4aab59f50/coverage.cobertura.xml`
**Aggregate coverage**: 53.93% line / 57.89% branch (informational; no configured aggregate threshold).

## Unit 4A Acceptance Matrix

| # | Acceptance | Runtime/static evidence | Result |
|---:|---|---|---|
| 1 | Production authority active by default and canonical DI dependencies supplied | Handler constructor defaults `shadowMode:false`; `Program` injects `IBackendIdentityCoordinator` into both production backend client and monitor | ✅ COMPLIANT |
| 2 | Complete fake HTTP → real parser → real policy → real durable store path | Runtime test uses real parser/policy/store, but constructs `BackendClient` through the obsolete `IDeviceAuthenticator` compatibility constructor while separately faking monitor identity | ⚠️ PARTIAL |
| 3 | Revoked creates one identity-scoped durable issue idempotently | Four revoked reports produce one active physical record; no duplicate verdict is sent after the issue becomes active and occurrence/revision stability is not asserted | ⚠️ PARTIAL |
| 4 | Same-identity recovery removes exactly its issue and preserves unrelated/other identities | Same test recreates enforcement/policy and resolves its key; cross-device test preserves device A. No unrelated issue is seeded, and a same-device generation refresh changes the key | ❌ FAILING |
| 5 | Unknown/pending/absent/malformed/stale/transport/timeout/cancellation preserve exact prior durable state | Unknown/pending/absent/malformed/503 retain an active key, but tests do not compare exact prior record state; stale, timeout, and cancellation lack complete durable-path tests | ❌ UNTESTED |
| 6 | Restart recreates production persistence/enforcement then applies backend recovery | `EnforcementLevelMonitor`, `FileIssueStore`, and policy are recreated before three backend trust responses | ✅ COMPLIANT |
| 7 | Canonical identity flows through transport/report/key; stable key survives retry/recreation; no cross-identity clearing | Production source wiring is static-only; runtime path splits legacy auth and a separate coordinator. Key includes credential generation, which increments on same-device token refresh | ❌ FAILING |
| 8 | Local integrity evidence is report-only and establishes no client baseline | Local failure reaction is discarded; no expected-hash/baseline API, DTO, or persistence exists | ✅ COMPLIANT |
| 9 | Duplicate verdicts are idempotent; recovery preserves unrelated cardinality | Stable store key deduplicates cardinality structurally, but active duplicate and unrelated-preservation acceptance are not executed | ❌ UNTESTED |
| 10 | No client/App.UI/Realtime/WNS authority or Unit 4B/5 leakage | No new baseline or UI/Realtime/WNS authority; however Unit 4B loop/disposal code remains in `AntiTamperMonitor` diff | ⚠️ PARTIAL |
| 11 | Unit 4B lifecycle/race concerns restored exactly to parent behavior | Uncalled `RunMonitorLoopAsync` remains; changed `Dispose` no longer disposes `monitorTimer`/`timezoneTimer` as parent did | ❌ FAILING |
| 12 | Async add/resolve interface changes are coherent; no prior-unit/offline regression | Production implementation, default interface compatibility, DI, and full Service/App.UI regressions pass | ✅ COMPLIANT |
| 13 | Tests call production and inspect physical state without tautology or hidden cleanup | Real components and physical file state are used; assertions occur before cleanup; no tautology/ghost loop found | ✅ COMPLIANT |

## Spec Compliance for the Authorized Slice

| Spec scenario | Covering test/evidence | Result |
|---|---|---|
| Runtime integrity — valid evidence receives trust | Real parser/policy/store recovery test, but unrelated state and same-device generation stability are absent | ⚠️ PARTIAL |
| Runtime integrity — revocation changes enforcement | Real path creates one durable active device-scoped issue after policy threshold | ✅ COMPLIANT |
| Runtime integrity — unknown/transient response is non-degrading | Payload/503 fragments pass; stale, timeout, cancellation, and exact-state equality are missing | ❌ UNTESTED |
| Runtime integrity — restart/recovery preserve semantics | Recreated store/enforcement/policy recover the tested key, but unrelated and generation-refresh semantics are unproven/broken | ⚠️ PARTIAL |
| Runtime integrity — deferred release scope not claimed | No live-integrated/client-ready/ExternalVerified claim | ✅ COMPLIANT |
| Offline enforcement — revoked evidence persists scoped issue | Real revoked path persists one active issue | ✅ COMPLIANT |
| Offline enforcement — semantic recovery survives restart | Tested issue resolves after recreation; unrelated preservation and stable same-device generation are missing | ⚠️ PARTIAL |
| Offline enforcement — transient integrity does not degrade | Incomplete normative outcome matrix | ❌ UNTESTED |
| Offline enforcement — concurrent recovery serialized | Deferred to Unit 4B and not claimed complete for Unit 4A | ➖ DEFERRED |

## Production Call Path, Authority, Identity, and Restart Audit

### Production path

`ControlParentalService` → production `AntiTamperMonitor` → `IntegrityChecker` evidence → `BackendClient.ReportIntegrityAsync` → `IBackendIdentityCoordinator.GetDefinitiveSessionAsync` → HTTP parse → `IntegrityVerdictHandler` → `EnforcementLevelMonitor.AddIssueAsync/ResolveIssueAsync` → `FileIssueStore`.

| Invariant | Finding |
|---|---|
| Backend authority | Active by default; no production activation toggle is required |
| Definitive verdict gate | Durable mutation is gated to successful exact `trust|revoked` results |
| Local evidence | Report-only for durable enforcement; handler reaction is discarded |
| Durable key | Device ID is embedded, but credential generation is also used as `SessionId` |
| Same-device refresh | `BackendIdentityCoordinator.RefreshAsync` increments generation; later trust targets a new key and cannot resolve the old active issue |
| Stale completion | Monitor compares captured identity state after transport; branch exists but has zero fresh coverage |
| Runtime identity harness | Uses obsolete legacy authenticator constructor for transport and a separate coordinator mock for durable key, so it is not the production canonical identity chain |
| Restart | File store, enforcement monitor, and policy are recreated; backend/coordinator are retained test objects |
| Recovery isolation | Cross-device preservation is covered; unrelated semantic issue preservation is not |

## Changed-Method and Branch Coverage

| Scope | Line | Branch | Required uncovered behavior |
|---|---:|---:|---|
| `AntiTamperMonitor` | 83.10% | 87.50% | Class aggregate acceptable |
| `PerformBinaryIntegrityCheckAsync` | 92.00% | 70.83% | stale identity return lines 405–406 have zero hits; disposed/not-running path uncovered |
| `ProcessVerdictReactionAsync` | 70.27% | 55.55% | Limit and ShadowWarn branches uncovered; identity fallback branches partial |
| `RunMonitorLoopAsync` | 0% | 0% | Entire unauthorized/deferred loop seam uncovered and uncalled |
| `IntegrityVerdictHandler` | 87.57% | 82.60% | disposed paths and dead escalation timer methods uncovered |
| `BackendClient` | 91.22% | 63.88% | aggregate branch rate low |
| `BackendClient.ReportIntegrityAsync` | 94.73% | 83.33% | caller-cancellation rethrow lines 608–609 have zero hits |
| `EnforcementLevelMonitor` | 94.76% | 86.95% | class aggregate strong |
| `AddIssueAsync` | 82.35% | 75.00% | no-store fallback uncovered |
| `ResolveIssueAsync` | 100% | 100% | covered |
| Default interface async methods | 0% | N/A | Compatibility defaults compile but are not executed |

Aggregate coverage does not establish the required stale/timeout/cancellation/exact-state behavior. Those uncovered normative behaviors are blocking.

## Strict TDD Audit

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Unit 4A table exists in cumulative `apply-progress.md` |
| Historical genuine RED | ✅ retained summary | Build followed by focused `LocalEvidence_DoesNotChangeEnforcement_WhenBackendVerdictIsNonAuthoritative`; local timestamp `2026-08-20 20:34:51`; exit 1; unexpected `AddIssueAsync` for `integrity/binary`, evidence `untrusted` |
| Raw RED log | ⚠️ unavailable | Apply progress explicitly records that no durable raw-output path was preserved; none is fabricated |
| Tests-first chronology | ⚠️ limited | Artifact chronology is coherent, but final uncommitted bytes cannot independently prove every edit order |
| Fresh GREEN | ✅ | 2/2 real path, 60/60 focused, 1,174/1,174 Service, 192/192 App.UI |
| Triangulation | ❌ | Missing canonical production identity, same-device refresh, stale, timeout, cancellation, exact-state, active duplicate, and unrelated issue cases |
| Safety nets | ✅ | Builds and both full regressions pass with `--no-restore` |
| Slice refactor boundary | ❌ | Deferred loop seam and changed timer disposal remain |

### Test Layer Distribution

| Layer | Tests | Files | Notes |
|---|---:|---:|---|
| Unit/mock | 58 focused | 3 | Checker, handler, and monitor tests |
| Production-component harness | 2 | 1 | Real backend parser/policy/store, but not production canonical identity transport |
| E2E/external | 0 | 0 | Correctly excluded; no external claim |

### Assertion Quality

- No tautology, ghost loop, or assertion-free new Unit 4A acceptance test was found.
- The payload loop is over a fixed non-empty array and executes physical store assertions.
- Blocking quality gaps are omission-based: exact durable record equality is not asserted after non-definitive outcomes; no unrelated issue is seeded; no duplicate is sent after issue activation; the runtime transport bypasses the production identity constructor.

**Assertion quality**: 0 trivial assertions; blocking acceptance coverage omissions remain.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Backend owns hash reference | ✅ | No client baseline added |
| Checker → authenticated backend → policy → durable enforcement | ⚠️ | Production static path exists; runtime harness bypasses canonical identity transport |
| Definitive verdicts only mutate | ✅ | Exact success/verdict gate exists |
| Unknown/malformed/transient non-degrading | ❌ proof | Complete normative runtime matrix is absent |
| Stable identity-scoped issue | ❌ | Key changes on same-device credential generation refresh |
| Restart-safe exact recovery | ⚠️ | Tested key recovers; unrelated state and generation stability are not proven |
| Unit 4A/4B surgical split | ❌ | Deferred loop seam and disposal regression remain in Unit 4A diff |
| One retry owner | ✅ | BackendClient remains the transport retry owner |

## Issues Found

### CRITICAL

1. **The durable identity key is not stable for the same canonical device.** `IssueKey.SessionId` is the credential generation. `BackendIdentityCoordinator.RefreshAsync` increments generation while preserving `DeviceId`; later trust resolves the new-generation key and leaves the old active issue persisted.
2. **The runtime test does not prove the production canonical identity chain.** It constructs `BackendClient` with the obsolete `IDeviceAuthenticator` compatibility constructor and independently supplies a coordinator mock to `AntiTamperMonitor`; transport authorization identity and durable-key identity can diverge.
3. **Required non-degradation outcomes lack complete runtime evidence.** Stale identity, timeout, and cancellation have no real parser→policy→durable-state test, and tested unknown/pending/absent/malformed/503 cases assert only active-key presence rather than exact prior durable state. Fresh coverage confirms zero hits on stale-return lines 405–406 and cancellation-rethrow lines 608–609.
4. **Recovery/idempotency isolation is incomplete.** No unrelated issue is seeded and preserved, and no duplicate revoked verdict is sent after the issue becomes active; exact cardinality/revision behavior is therefore unproven.
5. **Unit 4B work was not fully removed and parent lifecycle behavior regressed.** The diff retains an uncalled `RunMonitorLoopAsync` cancellation-loop seam (0% coverage), while changed `Dispose` no longer disposes either timer as the parent implementation did. This violates the authorized slice boundary.

### WARNING

1. Existing NU1601/NU1701, analyzer/style warnings, and one duplicate Service xUnit test-ID notice remain. Builds have zero errors and no dependency declarations changed.
2. The default async interface compatibility methods are uncovered; production Service overrides them and App.UI remains non-authoritative through defaults.
3. Handler escalation methods remain dead/uncovered parent risk. Escalation/lifecycle/race completion is deferred to Unit 4B and is not claimed fixed here.

### SUGGESTION

None. This verification is report-only.

## Final Verdict and Approval

**FAIL**

Unit 4A is **not approved** for a local commit boundary. Passing aggregate tests, active default authority, real durable parser/policy/store fragments, and the 286/400 budget do not compensate for unstable same-device issue identity, a non-production identity harness, missing required non-degradation and isolation proofs, and retained Unit 4B lifecycle changes.

Top-level Unit 4 remains **9/14**, incomplete, and **not archive-ready**. Unit 4B and final full-Unit verification remain required. No commit, push, PR, archive, implementation fix, Unit 4B preparation, or Unit 5 work was performed.
