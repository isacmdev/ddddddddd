# Verification Report

**Change**: `remote-signal-integrity` — Unit 4 tasks 4.1–4.3 only
**Branch**: `feat/sdd7-4-runtime-integrity-enforcement`
**Base / PR4 parent**: `3082559551ce4aa4363bd14a9e09dda0b1176f5f` / `feat/sdd7-3-foreground-realtime`
**Worktree**: `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-4`
**Mode**: Strict TDD, hybrid persistence, fresh independent report-only verification
**Verification date**: 2026-08-20 (`-05:00`)
**Verdict**: **FAIL**

## Executive Summary

Fresh builds, the reported 33 focused tests, 1,174 Service tests, 192 App.UI tests, and portable-PDB coverage all pass. The exact five-file CODE+TEST diff is isolated and is 146/400 lines. Local integrity evidence and non-definitive verdict strings are prevented from directly invoking durable mutation in the changed monitor path.

Unit 4 nevertheless fails the runtime-integrity gate. Production never disables the singleton verdict handler's default shadow mode, so real backend `revoked` verdicts never produce an enforcement issue. The two new monitor tests replace both the backend and verdict policy with mocks and therefore do not execute the production backend-parse → real policy → durable store path. No passing test proves authoritative trust recovery physically resolves only `integrity/binary`, restart rehydration of that issue, malformed/cancelled behavior through the monitor, or stale/reordered/concurrent report completion. Source inspection additionally finds overlapping fire-and-forget timer callbacks, swallowed cancellation, no in-flight drain at stop/dispose, unsynchronized verdict counters, no completion version/identity generation guard, and a global issue key with no device identity. These are blocking authority, race, restart, identity, and lifecycle defects.

**Unit 4 is NOT approved for the local feature-chain commit boundary.** Full SDD7 remains partial at **12/14** checked tasks and is not archive-ready. Unit 5 was not prepared or verified.

## Freshness and Inspection Boundary

- Read exploration, proposal, all four specs, design, tasks, cumulative apply progress, and every prior Unit 1–3 verification report.
- CodeGraph was attempted first by checking the exact worktree. `.codegraph/` was absent; per the explicit no-artifact boundary, verification used focused direct inspection and did not initialize an index.
- Inspected all five changed files in full, the exact base diff, `BackendClient` authenticated report/parse/retry path, production DI, `ControlParentalService` start/stop ownership, `EnforcementLevelMonitor`, `FileIssueStore`, every `IEnforcementLevelMonitor` implementation, interface callers/fakes, and relevant durable-store/backend tests.
- No implementation, test, task, apply-progress, spec, design, configuration, dependency, git, branch, worktree, external system, commit, push, PR, archive, or Unit 5 content was changed. This report is the only authored artifact.

## Completeness

| Metric | Value |
|---|---:|
| Unit 4 tasks | 3 |
| Unit 4 checked | 3 (`4.1`–`4.3`) |
| Unit 4 unchecked | 0 |
| Cumulative SDD7 checked | 12/14 |
| Unit 5 excluded | 2 tasks |
| Runtime-integrity scenarios | 5 |
| Interacting offline-enforcement scenarios | 4 |
| Fully compliant in-scope scenarios | 0/9 |

Checked tasks do not establish correctness. Required runtime paths remain failing or untested.

## Workspace, Diff, Drift, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch / HEAD | `feat/sdd7-4-runtime-integrity-enforcement` / exact parent `3082559551ce4aa4363bd14a9e09dda0b1176f5f` |
| Merge base | exact parent `3082559551ce4aa4363bd14a9e09dda0b1176f5f` |
| Changed tracked files | Exactly the five expected Unit 4 files |
| Deleted files/tests | None |
| Test diff | 71 additions, 0 deletions; no unrelated test deletion found |
| Dependency/config/project drift | None |
| `git diff --check` | Exit 0 before and after execution; LF→CRLF notices only |
| Restore | No restore executed; every fresh .NET command used `--no-restore` |

### Exact CODE+TEST Budget

| File | Additions | Deletions | Total |
|---|---:|---:|---:|
| `src/ControlParental.Domain/IEnforcementLevelMonitor.cs` | 19 | 0 | 19 |
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 34 | 12 | 46 |
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | 9 | 1 | 10 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 54 | 0 | 54 |
| `tests/ControlParental.Service.Tests/IntegrityVerdictHandlerTests.cs` | 17 | 0 | 17 |
| **Total** | **133** | **13** | **146/400** |

The apply total of 146 is correct, but its decomposition is not: the fresh diff contains 75 production lines (19 Domain + 56 Service) and 71 test lines, not “19 Domain + 43 Service + 71 tests.”

## Fresh Build, Test, and Coverage Evidence

All commands ran sequentially in the exact worktree.

| Evidence | Exact command | Local start → end | Exit | Result |
|---|---|---|---:|---|
| Service product build | `dotnet build "src\ControlParental.Service\ControlParental.Service.csproj" --no-restore --configuration Debug --verbosity minimal` | `20:43:53.39` → `20:43:55.08` | 0 | 0 errors; 1 existing NU1601 warning |
| Service test build | `dotnet build "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --configuration Debug --verbosity minimal` | `20:44:01.01` → `20:44:02.82` | 0 | 0 errors; existing NU1601/NU1701 warnings |
| App.UI product build | `dotnet build "src\ControlParental.App.UI\ControlParental.App.UI.csproj" --no-restore --configuration Debug --verbosity quiet -p:WarningLevel=0` | `20:44:08.73` → `20:44:15.86` | 0 | 0 errors; 2 existing NU1601 warnings |
| App.UI test build, portable PDB | `dotnet build "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --configuration Debug --verbosity quiet -p:DebugType=portable -p:WarningLevel=0` | `20:44:21.74` → `20:44:33.87` | 0 | 0 errors; 3 existing NU1601 warnings |
| Exact reported Unit 4 focus | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~AntiTamperMonitorTests.LocalEvidence_DoesNotChangeEnforcement_WhenBackendVerdictIsNonAuthoritative|FullyQualifiedName~AntiTamperMonitorTests.BackendRevokedVerdict_IsTheOnlyIntegrityEnforcementAuthority"` | `20:44:41.44` → `20:44:43.95` | 0 | **33 passed**, 0 failed/skipped |
| Full Service regression | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal` | `20:44:49.65` → `20:45:03.90` | 0 | **1,174 passed**, 0 failed/skipped; existing duplicate-ID notice |
| Full App.UI regression | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal` | `20:45:09.40` → `20:45:13.14` | 0 | **192 passed**, 0 failed/skipped |
| Portable-PDB Service coverage | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --configuration Debug -p:DebugType=portable --collect:"XPlat Code Coverage" --results-directory "tests\ControlParental.Service.Tests\TestResults\coverage-unit4-fresh-20260820-2046" --verbosity minimal -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | `20:45:31.1322408` → `20:46:17.9017737` | 0 | **1,174 passed**; non-empty Cobertura |

One earlier verifier coverage invocation at `20:45:22.23` exited 1 with `MSB1008` because nested `cmd` quoting split `XPlat Code Coverage`. It executed no tests and is rejected as evidence; the exact corrected command above passed. No restore occurred in either invocation.

**Coverage artifact**: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4-fresh-20260820-2046/4c1f5daf-4618-42f5-9034-d7497ab8de9c/coverage.cobertura.xml`
**Aggregate**: 53.93% line / 57.85% branch (informational; no aggregate threshold is specified).

## Authoritative Scenario Matrix

Per the verification gate, source evidence or passing fragments do not make a scenario compliant without one complete passing runtime path.

| Spec | Scenario | Passing fragments | Missing/failed complete proof | Result |
|---|---|---|---|---|
| Runtime integrity | Valid evidence receives trust | Integrity checker hash/signature tests; backend trust parse test; handler emits recovery after three trusts | No production path test creates the stable issue, reports through real backend parsing, applies three real trust verdicts, calls `ResolveIssueAsync`, verifies physical durable resolution, and preserves unrelated issues | ❌ UNTESTED |
| Runtime integrity | Revocation changes enforcement | New monitor test verifies `AddIssueAsync` when a mocked handler returns `Degrade` | Production handler remains in shadow mode forever; the test mocks away verdict policy and durable store. Real backend revoked → real policy → durable enforcement is not proven and does not occur in production composition | ❌ FAILING |
| Runtime integrity | Unknown or transient response is non-degrading | New unknown test; handler `success=false` test; backend network/timeout parsing tests | No complete monitor test covers absent, malformed, unavailable, timeout, and cancellation while asserting durable state and no false trust/recovery; cancellation is swallowed by the monitor | ❌ UNTESTED |
| Runtime integrity | Restart and recovery preserve semantics | Generic `FileIssueStore` restart tests pass in the full suite | No integrity-key restart test rehydrates active `integrity/binary`, applies later backend trust through the monitor, resolves only that key, and proves the resolved state after a second restart | ❌ UNTESTED |
| Runtime integrity | Deferred release scope is not claimed | Artifacts remain contract-first and make no forbidden readiness claim | No Unit 4 runtime/readiness test covers the scenario; external receipt evidence remains intentionally absent | ❌ UNTESTED |
| Offline enforcement | Supported safety evidence degrades health | Generic enforcement and durable issue tests; mocked monitor add test | No real definitive revoked verdict reaches durable issue/health in production because shadow mode is never disabled | ❌ FAILING |
| Offline enforcement | Semantic issue recovery survives restart | Generic store tests prove deduplication, resolution, and restart for other keys | No complete `integrity/binary` authoritative-recovery path or unrelated-issue preservation test through production monitor/policy | ❌ UNTESTED |
| Offline enforcement | Transient integrity failure does not degrade protection | Handler failure and new unknown fragment tests | Missing complete absent/malformed/unavailable/timeout/cancelled monitor path with persisted-state assertions; cancellation/lifecycle behavior is incorrect | ❌ UNTESTED |
| Offline enforcement | Concurrent recovery is serialized | `FileIssueStore` serializes concurrent upserts for one generic key | No test races restart, integrity verdicts, agent death, enforcement, reordered report completions, or recovery. Verdict policy itself is unsynchronized and report completion has no ordering/version guard | ❌ UNTESTED |

**Compliance summary**: **0/9 scenarios compliant**.

## Authority and Complete Call-Path Audit

### Production call path

`ControlParentalService.ExecuteAsync` → singleton `IAntiTamperMonitor.StartAsync` → `PerformIntegrityCheckAsync` → `IntegrityChecker.CheckLocalIntegrityAsync` → construct hash/signature `IntegrityReport` → production `BackendClient.ReportIntegrityAsync` → `SendAuthenticatedAsync` obtains a definitive identity session and performs bounded retry → successful HTTP response body is parsed for `verdict` → `IntegrityVerdictHandler.HandleVerdict` → private `ProcessVerdictReactionAsync` → Service `EnforcementLevelMonitor.AddIssueAsync/ResolveIssueAsync` → serialized `FileIssueStore.UpsertActiveAsync/ResolveAsync`.

| Authority invariant | Finding | Assessment |
|---|---|---|
| Local hash/signature evidence is report-only | Changed monitor discards `HandleLocalFailure` reaction and never passes it to durable mutation | ✅ Static path correct |
| Only successfully received/parsed backend verdict may mutate | Monitor gates mutation on `Success` and exact `trust|revoked`; production backend uses authenticated transport | ✅ Static gate, but runtime production path untested |
| Arbitrary local verdict string cannot directly mutate | `HandleVerdict` remains public but returns only a reaction; the only production mutation caller is the monitor's private method | ✅ No direct local mutation caller found |
| Backend verdict policy is active | `IntegrityVerdictHandler.shadowMode` defaults true; production DI constructs it and no production caller invokes `DisableShadowMode` | ❌ Real revoked verdicts remain `ShadowWarn` forever |
| Backend parse is authoritative and bounded | HTTP success with absent/malformed verdict yields `Success=true, Verdict=null`, then monitor gate blocks mutation; exact lowercase `trust|revoked` accepted | ✅ Static normalization; incomplete runtime matrix |
| Stable issue identity | `IssueKey(-1, BinaryIntegrityFailure, "integrity/binary")` is used for add/resolve; store upsert deduplicates records | ✅ Stable semantic key, but identity scope is defective below |
| Recovery removes only matching issue | `ResolveIssueAsync` receives only `integrity/binary` | ✅ Static; ❌ no complete passing runtime test |
| No client baseline | No expected-hash/baseline API, DTO, endpoint, or persistence addition exists; backend remains reference authority | ✅ |
| App.UI/Realtime/WNS remain non-authoritative | No Unit 4 diff in those paths; App.UI proxy implementations remain read/no-op for mutation | ✅ |

## Restart, Race, Identity, Retry, and Cancellation Audit

| Area | Fresh finding | Assessment |
|---|---|---|
| Durable restart | `FileIssueStore` and `EnforcementLevelMonitor.StartAsync` restore active semantic issues | ✅ Generic mechanism exists; ❌ integrity lifecycle not runtime-proven |
| Verdict-policy restart | Revoked/trust counters, pending escalation, circuit state, shadow state, and last verdict are memory-only and reset on service restart | ❌ Required integrity state is not explicit/preserved across restart |
| Duplicate verdicts | Stable store key prevents duplicate records but increments occurrence/revision for every completed add | ⚠️ One active record; no Unit 4 cardinality/restart test |
| Reordered/concurrent reports | Timer callbacks can overlap; no single-flight gate, sequence, report revision, monotonic timestamp check, or response generation check exists | ❌ Older revoked/trust completion can overwrite newer authoritative state |
| Verdict handler concurrency | Singleton mutable counters/timers/booleans have no synchronization | ❌ Concurrent callbacks can lose/reorder threshold and recovery state |
| Device identity | Backend transport obtains a definitive session for each attempt, but returned `IntegrityReportResult` carries no device/generation and completion is not revalidated against current identity | ❌ A response from an old identity generation can mutate current local state |
| Durable issue scope | Integrity key uses `SessionId=-1` and contains no device ID | ❌ Re-pair/device rotation collapses identities; a new device trust can resolve the prior device's issue |
| Retry owner | `BackendClient.SendAuthenticatedAsync` is the sole bounded transport retry owner (maximum 1–3 attempts, stable per-call idempotency key) | ✅ No second retry loop added |
| Monitor scheduling | `System.Threading.Timer` callback uses `_ => _ = PerformIntegrityCheckAsync(CancellationToken.None)` | ❌ Fire-and-forget overlapping work with unobserved ownership |
| Cancellation | Both `PerformIntegrityCheckAsync` and `PerformBinaryIntegrityCheckAsync` catch `Exception`, including `OperationCanceledException`, and only log | ❌ Caller/shutdown cancellation is swallowed |
| Stop/dispose | `StopAsync` only disables future timer ticks; `Dispose` disposes timers but neither cancels nor awaits an in-flight report/store mutation | ❌ Teardown race; mutation can complete after stop/dispose |
| Lock-held I/O | Changed verdict/store calls are not made while `AntiTamperMonitor.lockObject` is held; `FileIssueStore` intentionally serializes its own I/O gate | ✅ No monitor lock-held I/O found |
| Escalation | Third revoked schedules a timer whose callback only logs; it never emits a reaction or adds an issue. A fourth report degrades immediately based on `pendingDegradeNotified`, not after the five-minute timer | ❌ Stated escalation behavior is not implemented |

## Changed Coverage

| Changed production scope | Line | Branch | Required uncovered paths | Assessment |
|---|---:|---:|---|---|
| `AntiTamperMonitor` class | 83.97% | 81.81% | Timer callbacks lines 148/155; lifecycle branches | ⚠️ Aggregate acceptable, required lifecycle ownership absent |
| `PerformBinaryIntegrityCheckAsync` | 95.55% | 77.77% | disposed/not-running guard | ⚠️ |
| `ProcessVerdictReactionAsync` | **36.36%** | **41.66%** | authoritative recovery 441–446; None, Warn, Limit, ShadowWarn branches | ❌ Required recovery and state branches unexecuted |
| `IntegrityVerdictHandler` class | 90.79% | 84.78% | arbitrary/malformed verdict path 239–241; disposed and escalation callback branches | ❌ Required malformed/escalation behavior incomplete |
| `IEnforcementLevelMonitor` default async methods | 0% | N/A | all new default mutation methods | ⚠️ Service override is covered; defaults are unexecuted |
| `EnforcementLevelMonitor.AddIssueAsync` | 82.35% | 50% | no-store fallback | ⚠️ Existing generic durable path covered |
| `EnforcementLevelMonitor.ResolveIssueAsync` | 100% | 100% | Generic path only, not reached from the changed monitor | ⚠️ |
| `BackendClient.ReportIntegrityAsync` | 94.73% | 83.33% | caller cancellation rethrow | ❌ Required cancellation completion path uncovered |

Aggregate coverage does not rescue required uncovered branches. The authoritative recovery branch in the changed monitor has zero runtime hits, and there is no race/restart/identity coverage.

## Interface, DI, and Prior-Unit Coherence

| Check | Result |
|---|---|
| Service implementation | Implements durable async add/resolve through `IIssueStore` |
| App.UI implementations | Compile through interface defaults; their mutation methods remain no-op/read-only, preserving non-authority |
| Mocks/fakes/callers | Service and App.UI projects compile; full regressions pass |
| DI ownership | Service registers one `IIssueStore`, one `IEnforcementLevelMonitor`, one singleton verdict handler, and one singleton anti-tamper monitor |
| Start ordering | Enforcement store restores before anti-tamper initial report |
| Stop ordering | Enforcement monitor is stopped before anti-tamper monitor, while anti-tamper in-flight work is not drained; this can race a late durable mutation |
| Units 1–3 | Full Service and App.UI regressions remain green; exact Unit 4 diff has no scheduler/WNS/Realtime production change |
| Offline semantics | Generic durable store semantics remain green, but Unit 4 does not prove or safely serialize integrity integration |

## Strict TDD Audit

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Unit 4 table exists in cumulative `apply-progress.md` |
| RED evidence retained | ⚠️ | Artifact records build + exact focused filter, local timestamp `2026-08-20 20:34:51`, exit 1, and unexpected `AddIssueAsync` for `integrity/binary` with evidence `untrusted`; no separate raw log path is retained |
| Tests preceded production edits | ⚠️ | Apply chronology states this and the current diff contains the reported test, but uncommitted final bytes cannot independently prove edit ordering; no chronology is fabricated |
| Fresh GREEN | ✅ | Focused 33/33, Service 1,174/1,174, App.UI 192/192, builds and accepted coverage all pass |
| Triangulation | ❌ | No production shadow-mode, real policy-to-store, recovery, restart, malformed/cancelled monitor, stale completion, concurrent verdict, or identity-rotation case |
| Safety nets | ✅ | Full Service and App.UI regressions pass under `--no-restore` |
| Refactor safety | ❌ | Required authority/lifecycle paths remain incorrect or untested despite green aggregate suites |

The reported RED is coherent with the changed gate and is not fabricated. It proves only that unknown/local evidence previously invoked durable add; it does not prove the normative trust/revoked/restart/race matrix.

### Test Layer Distribution

| Layer | Focused tests | Files | Assessment |
|---|---:|---:|---|
| Unit / mock harness | 33 | 3 | Integrity checker, verdict handler, and two monitor cases |
| Durable component harness | 0 newly focused | 0 | Existing generic durable tests ran only through full regression |
| Production-path integration | 0 | 0 | No real backend parser + real handler + real durable monitor/store test |
| E2E/external | 0 | 0 | Correctly excluded; no external claim |

### Assertion Quality

- No tautology, ghost loop, or assertion-free production call was found.
- `BackendRevokedVerdict_IsTheOnlyIntegrityEnforcementAuthority` is mock-heavy and replaces both authoritative input parsing and real verdict policy; it proves private monitor routing only, not backend authority or durable state.
- `LocalEvidence_DoesNotChangeEnforcement_WhenBackendVerdictIsNonAuthoritative` asserts only that `AddIssueAsync` was not called; it does not assert `ResolveIssueAsync` was also absent or inspect physical durable state.
- `HandleVerdict_RecoveryThreshold_EmitsAuthoritativeRecoverySignal` uses reflection instead of the compile-time `IsAuthoritativeRecovery` property and never calls the monitor/store recovery path.
- No changed test verifies issue cardinality, occurrence/revision, unrelated-issue preservation, restart, race ordering, cancellation propagation, or teardown.

**Assertion quality**: 0 trivial assertions; blocking scenario coverage and production-path substitution defects remain.

## Issues Found

### CRITICAL

1. **Production revoked verdicts cannot enforce.** `IntegrityVerdictHandler` defaults to shadow mode, production DI never disables it, and every real revoked verdict returns `ShadowWarn`. The only passing enforcement test mocks the handler to return `Degrade`, bypassing the production defect.
2. **Required trust/revoked/restart scenarios have no complete passing production-path tests.** In particular, changed monitor recovery lines 441–446 have zero coverage; no test physically resolves durable `integrity/binary` or proves unrelated issues survive.
3. **Concurrent/reordered completion is unsafe and untested.** Overlapping fire-and-forget timer checks feed an unsynchronized singleton handler and then a completion-ordered store with no sequence/version/generation guard. A stale outcome can supersede a newer authoritative state.
4. **Identity is not preserved across report completion or durable issue scope.** Backend results carry no identity generation, completion is not revalidated, and the global `SessionId=-1` key collapses device rotations, permitting cross-device add/resolve mutation.
5. **Cancellation and teardown ownership violate the runtime contract.** Timer work is fire-and-forget with `CancellationToken.None`; cancellation is swallowed; stop/dispose neither cancels nor drains in-flight reporting or durable mutation; enforcement is stopped first.
6. **Escalation does not enforce as documented.** The five-minute timer only logs; exactly three revoked verdicts never create the issue, while a fourth can degrade immediately regardless of timer completion.
7. **The authoritative scenario matrix is 0/9 compliant.** Passing fragments do not cover every required trust, revoked, unknown/absent/malformed/stale/failure/cancellation, restart, recovery, identity, and concurrency transition at runtime.

### WARNING

1. Strict-TDD RED summary is retained with timestamp/filter/exit/failure, but no raw output artifact exists and edit chronology cannot be independently reconstructed from the uncommitted final diff.
2. The apply total budget is correct, but its production/test decomposition is inaccurate; fresh exact accounting is 75 production + 71 test = 146.
3. The new interface default async methods have no XML documentation and are uncovered; compile compatibility is preserved, but default `ResolveIssue` is intentionally empty for implementations that do not override it.
4. Existing NU1601/NU1701/analyzer warnings and one duplicate Service xUnit test-ID notice remain outside Unit 4 scope.
5. One verifier coverage invocation failed from command quoting and was rejected; the corrected portable-PDB run passed and produced the accepted artifact.

### SUGGESTION

None. This verification is report-only and does not prescribe or perform remediation.

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Backend owns authoritative hash reference | ✅ | No client baseline added |
| Checker → authenticated backend → policy → enforcement | ⚠️ | Static path exists; production policy remains permanently shadowed |
| Definitive verdicts only mutate | ✅ static | Exact monitor gate exists; complete runtime proof absent |
| Unknown/malformed/transient non-degrading | ⚠️ | Durable gate blocks strings, but full lifecycle/cancellation proof is absent |
| Stable durable issue and trust recovery | ⚠️ | Stable key/store exist; recovery branch unexecuted and identity scope is global |
| Restart-safe semantics | ❌ | Store survives; policy counters/escalation/identity context do not |
| Serialized deterministic races | ❌ | Store serializes writes only; verdict/report authority order is not serialized/versioned |
| One retry/cancellation owner | ❌ | Retry owner is singular, but monitor creates an unowned periodic async loop and swallows cancellation |

## Final Verdict and Approval

**FAIL**

Unit 4 tasks 4.1–4.3 are **not approved for the local feature-chain commit boundary**. Green aggregate tests and a 146/400 isolated diff do not compensate for a production policy that remains permanently shadowed, missing complete scenario tests, unsafe completion/identity ordering, and broken cancellation/teardown ownership.

Full SDD7 remains partial at **12/14** checked tasks and is **not archive-ready**. No commit, push, PR, archive, implementation fix, or Unit 5 preparation was performed.
