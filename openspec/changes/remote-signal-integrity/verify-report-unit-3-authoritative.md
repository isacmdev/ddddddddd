# Verification Report

**Change**: `remote-signal-integrity` — Unit 3 tasks 3.1–3.3 only
**Branch**: `feat/sdd7-3-foreground-realtime`
**Base / PR 3 parent**: `eea01b4e32de61b49034673fcd40805799387c43` / `feat/sdd7-2-wns-convergence`
**Mode**: Strict TDD, hybrid persistence, fresh authoritative independent report-only verification
**Verification date**: 2026-08-20 (`-05:00`)
**Verdict**: **FAIL**

## Executive Summary

The queued-lifecycle epoch remediation fixes the previously reported queued-foreground ordering defect for the exercised event-driven path: a foreground operation captures an epoch before queueing, and the deterministic test observes zero additional subscriptions after a newer background epoch. The started, non-cancellable policy subscription is also owned through completion and physically unsubscribed when stale. Fresh builds, 9/9 focused tests, 187/187 App.UI tests, 1,171/1,171 Service tests, coverage, and diff-check all pass.

Unit 3 nevertheless fails this authoritative gate. `RealtimeSubscriber` records only an epoch, not desired foreground state, and public `ConnectAsync` bypasses lifecycle authority entirely. Fresh tests explicitly connect while `FakeWindowLifecycleObserver.IsInForeground` is false. Required grant-side cancellation/stale-completion and concurrent background/dispose/repeated-dispose paths also have no complete runtime proof; Cobertura confirms the stale grant completion and repeated-dispose branches are unexecuted. The queued-ordering test additionally calls `Dispose()` before its physical-state assertions, weakening attribution of final transport closure to background handling alone.

Unit 3 is **not approved for the local feature-chain commit boundary**. Full SDD7 remains partial at **9/14** checked tasks and is not archive-ready. Units 4–5 were not verified or prepared.

## Freshness, Scope, and Completeness

- CodeGraph was attempted first against the exact worktree. The worktree had an existing `.codegraph/` index and CodeGraph returned the subscriber flow/source, but its capped result trimmed the target source and omitted the requested test file. Verification therefore fell back to direct full inspection of both changed files and all supplied context artifacts.
- Both historical FAIL reports, `verify-report-unit-3.md` and `verify-report-unit-3-final.md`, were read only as historical context, not reused as verdict evidence, and remain unchanged.
- Proposal, all four specs, design, tasks, cumulative apply-progress, both historical Unit 3 reports, the exact base diff, and both changed files were inspected.
- No code, test, task, apply, proposal, spec, design, project, dependency, configuration, branch, commit, push, PR, archive, or external system was changed by verification.

| Metric | Value |
|---|---:|
| Unit 3 tasks | 3 |
| Unit 3 complete | 3 (`3.1`–`3.3` checked) |
| Unit 3 incomplete | 0 |
| Cumulative SDD7 complete | 9/14 |
| Excluded remaining work | Units 4–5, 5 tasks |
| In-scope spec scenarios | 3 |
| Fully compliant scenarios | 1/3 |

## Workspace, Diff, Drift, and Budget Audit

Initial audit: `2026-08-20T18:48:51.3167684-05:00`–`18:48:51.8099291-05:00`, exit 0.

| Check | Fresh result |
|---|---|
| Worktree | `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-3` |
| Branch | `feat/sdd7-3-foreground-realtime` |
| `HEAD` | Exact required base `eea01b4e32de61b49034673fcd40805799387c43` |
| Changed tracked files | Exactly `src/ControlParental.App.UI/RealtimeSubscriber.cs` and `tests/ControlParental.App.UI.Tests/RealtimeSubscriberTests.cs` |
| Production numstat | 185 additions, 72 deletions = 257 |
| Test numstat | 102 additions, 41 deletions = 143 |
| CODE+TEST budget | **287 additions + 113 deletions = 400/400** |
| Budget headroom | **0 lines**; no size exception |
| Project/dependency/config drift | None |
| Service/REST/policy/identity/retry/scheduler/enforcement/Unit 4+ diff | None |
| Initial `git diff --check` | Exit 0; LF→CRLF notices only |

The untracked `.codegraph/` index and cumulative OpenSpec directory already existed at verification start. Coverage output is test-run evidence, not a tracked source/config change.

## Fresh Build and Runtime Evidence

Every .NET command used `--no-restore`; no restore was performed. WinUI product and test builds were run sequentially to avoid build-artifact races.

| Evidence | Exact command | Start → end (`-05:00`) | Exit | Result |
|---|---|---|---:|---|
| App.UI product build | `dotnet build "src\ControlParental.App.UI\ControlParental.App.UI.csproj" --no-restore --configuration Debug --verbosity minimal` | `18:49:57.8095225` → `18:50:05.0204018` | 0 | 0 errors; 287 warnings |
| App.UI test build, portable PDB | `dotnet build "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --configuration Debug --verbosity minimal -p:DebugType=portable` | `18:50:29.3252565` → `18:50:39.7514784` | 0 | 0 errors; 1,336 warnings across the graph |
| Focused Realtime | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~RealtimeSubscriberTests"` | `18:50:50.4832724` → `18:50:53.0690888` | 0 | **9 passed**, 0 failed, 0 skipped |
| Full App.UI regression | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal` | `18:50:59.2552744` → `18:51:01.9976697` | 0 | **187 passed**, 0 failed, 0 skipped |
| Full Service regression | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal` | `18:51:10.3390252` → `18:51:24.1682853` | 0 | **1,171 passed**, 0 failed, 0 skipped; existing duplicate-ID discovery notice |
| Portable-PDB coverage | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --collect:"XPlat Code Coverage" --results-directory "tests\ControlParental.App.UI.Tests\TestResults\coverage-unit3-authoritative-fresh-20260820-1851" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | `18:51:33.7119555` → `18:52:05.7190470` | 0 | **187 passed**; Cobertura produced |
| Final tracked/report diff check | `git diff --check eea01b4e32de61b49034673fcd40805799387c43` and `git diff --no-index --check NUL "openspec/changes/remote-signal-integrity/verify-report-unit-3-authoritative.md"` | `18:55:15.3231707` → `18:55:15.5580518` | 0 / expected 1 | Tracked diff clean; report clean as a new-file difference; LF→CRLF notices only |

Coverage artifact:

`tests/ControlParental.App.UI.Tests/TestResults/coverage-unit3-authoritative-fresh-20260820-1851/9a1d9e41-e12e-4e04-ac40-1ca009fb9b66/coverage.cobertura.xml`

## Three-Scenario Compliance Matrix

| Requirement | Scenario | Source and fresh runtime evidence | Result |
|---|---|---|---|
| Realtime is foreground-only and non-authoritative | Foreground event refreshes UI | `PolicyBroadcast_FiresPolicyChangedWithCorrectVersion` and `GrantBroadcast_FiresGrantsChangedWithCorrectData` call production, publish bounded typed values once, and pass in focused 9/9. Exact diff is App.UI-only; Service 1,171/1,171 remains green. | ✅ **COMPLIANT** |
| Realtime is foreground-only and non-authoritative | Background and reconnect failure are isolated | Epoch rejection, current policy/grant failure cleanup, queued failure containment, and later reconnect pass. However, public `ConnectAsync` ignores `IsInForeground`; `ConnectAsync_SetsIsConnectedTrue` passes while the fake lifecycle is initially background. The queued-order test also disposes before physical-state assertions. | ❌ **FAILING** |
| Realtime is foreground-only and non-authoritative | Cancellation and restart recover safely | Policy-side blocked completion cleanup, old policy/grant callback suppression, current exactly-once events, and ordinary duplicate connect prevention pass. No test exercises cancellation after grant subscribe starts, and no concurrent background/dispose plus repeated-dispose test exists; required changed branches remain uncovered. | ⚠️ **PARTIAL / UNTESTED** |

**Compliance summary**: **1/3 scenarios fully compliant**.

## Mandatory Source and Concurrency Audit

| # | Invariant | Fresh finding | Assessment |
|---:|---|---|---|
| 1 | Desired lifecycle intent and monotonic epoch synchronously recorded before queueing | Foreground/background/dispose increment `lifecycleEpoch` under `lockObj` before queueing. No desired-foreground field is recorded; `lifecycleObserver.IsInForeground` is never read. | ❌ Incomplete |
| 2 | Queued connect captures origin epoch and rejects stale work before transport/ready | Event-driven connect captures epoch and checks it under the state lock before subscribing; generation/epoch is rechecked after each subscribe and before ready. The barrier test proves no new queued subscription (`policy` stays at the original call count 1; grants 0). It calls `Dispose()` before physical closure assertions, so closure attribution is weakened. | ⚠️ Ordering fixed; proof partially contaminated |
| 3 | Started non-cancellable subscribe remains owned and is cleaned when stale | `subscription` is retained; cancellation catch awaits the underlying task, consumes its fault, unsubscribes stale `activeChannel`, invalidates, and returns. The policy barrier path passes and ends physically unsubscribed. | ✅ Policy path proven |
| 4 | Current policy and grant failures physically clean, remain disconnected, observe failure, permit one reconnect | `FailedConnectAndQueuedFailure_CleanUpAndReconnect` asserts both transports false and both handler counts zero after direct policy failure, queued policy failure, and grant failure; queued fault is caught; later direct reconnect leaves one handler each. | ✅ Covered, though final reconnect is direct rather than a foreground event |
| 5 | Old policy/grant callbacks ignored; current callbacks exactly once | Generation-capturing handlers plus `readyGeneration` guard suppress retained old delegates; current policy/grant broadcasts yield `(1,1)`. | ✅ Covered |
| 6 | Concurrent background/dispose and repeated dispose invalidate immediately and clean deterministically | Source invalidates synchronously and marks disposed under lock. No test overlaps background and dispose; no test calls `Dispose` twice. Cobertura leaves the repeated-dispose return uncovered. | ❌ Required runtime proof absent |
| 7 | Direct connect, event connect, and `LifecycleTask` remain coherent | Direct public connect has no foreground/epoch authority and can subscribe while background. `LifecycleTask` observes queue drain but queued failures are swallowed/traced, so successful task completion is not successful connection. | ❌ Foreground bypass; ⚠️ ambiguous task success |
| 8 | One gate/owned chain; no lock over channel/user code; no deadlock/reentrancy/duplicates | One `SemaphoreSlim` gate and one continuation chain are used. Channel methods and user event invocation occur outside `lockObj`; gate release is in `finally`. Ordinary duplicate connect is tested. Grant-side stale completion remains untested. | ⚠️ Static design sound; incomplete race proof |
| 9 | App.UI-only acceleration; no authority or Unit 4+ leakage | Exact tracked diff is only the App.UI subscriber and App.UI unit tests. No Service/REST/scheduler/identity/retry/enforcement/integrity change exists. | ✅ |
| 10 | Consolidated/deleted tests retain behavior and prove physical/cardinality state | Current tests call production and assert typed values, transport state, handler counts, and subscribe counts. Basic idempotency and malformed payload behavior are folded into larger tests. Missing grant barrier/concurrent-dispose coverage and dispose-before-assert weaken completeness. | ⚠️ No trivial assertions; required gaps remain |

### Concrete Foreground-Authority Bypass

1. A new `FakeWindowLifecycleObserver` starts with `IsInForeground == false`.
2. `ConnectAsync_SetsIsConnectedTrue` calls public production `ConnectAsync()` without a foreground event.
3. `ConnectCoreAsync` receives `requestedEpoch == null`; it never checks lifecycle foreground state.
4. Both channels subscribe and `IsConnected` becomes true while lifecycle state is background.

This is direct fresh runtime evidence of behavior contrary to “subscription MUST exist only while the UI is foreground,” not a theoretical static concern.

## Changed-File and Changed-Branch Coverage

| File | Line coverage | Branch coverage | Rating |
|---|---:|---:|---|
| `src/ControlParental.App.UI/RealtimeSubscriber.cs` | **94.04%** | **77.58%** | ⚠️ Aggregate acceptable; required behavior remains uncovered |
| `tests/ControlParental.App.UI.Tests/RealtimeSubscriberTests.cs` | Test code | Test code | Unit/lifecycle harness |

| Changed method/branch | Fresh Cobertura evidence | Assessment |
|---|---|---|
| Event epoch rejection before subscribe | `ConnectCoreAsync` line 83: 4/4 conditions | ✅ Covered |
| Connected/idempotent guard | line 85: 2/2 | ✅ Covered |
| Stale policy normal completion | line 103: 2/2 | ✅ Covered by `StaleNormalCompletion_UnsubscribesPolicy` |
| Stale grant normal completion | line 108: **1/2** | ❌ Required cancellation ordering untested |
| Current/stale catch ownership | lines 123, 127, 132: 2/2; line 129: 1/2 | ⚠️ Core ownership covered; one active-channel unsubscribe branch outcome absent |
| Ready-generation decision | line 112: 2/2 | ✅ Covered |
| Invalidation cleanup | 100% line/branch; line 148 4/4 and detach/cancel decisions 2/2 | ✅ Covered |
| Stale generation token | `GetGenerationCancellation` line 186: **2/4** | ⚠️ Pre-token invalidation ordering not directly covered |
| Old policy callback guard | line 253: 2/2 | ✅ Covered |
| Old grant callback guard | line 277: 2/2 | ✅ Covered |
| Dispose idempotency | lines 320–321 uncovered; line 319: **1/2** | ❌ Repeated dispose untested |
| Connect after dispose | lines 79–80 uncovered; line 78: **1/2** | ⚠️ Defensive public branch untested |
| Policy/grant parse exception defenses | lines 265–268 and 294–297 uncovered | ⚠️ Defensive, non-normative gap |

The 77.58% aggregate is not itself blocking. The unexecuted grant-side stale completion and dispose lifecycle branches are blocking because they correspond to expressly required lifecycle orderings.

## Design Coherence

| Decision | Followed? | Evidence |
|---|---|---|
| Realtime remains App.UI-only | ✅ Yes | Only App.UI subscriber/test files differ |
| Typed UI acceleration only | ✅ Yes | Events carry typed policy version/grant values; no policy application or REST path |
| No synchronization, identity, retry, scheduler, enforcement, or integrity authority | ✅ Yes | No dependency/call/diff in those areas |
| Generation-aware foreground lifecycle | ⚠️ Partial | Epoch fixes stale event-queue work, but desired foreground is not represented and direct connect bypasses epoch authority |
| One serialized lifecycle owner | ✅ Yes | One gate, generation owner, and queued chain |
| Foreground-only physical transport | ❌ No | Public direct connection succeeds while observer reports background |
| Polling remains fallback | ✅ Yes | No Service change; Service regression remains green |

## Strict-TDD Audit

Historical chronology is preserved exactly: the attempted new-test RED used `--no-build` against a stale DLL and produced **`No test matches the given testcase filter`**. It was not a behavioral assertion failure and is not treated as RED.

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Unit 3 six-column evidence exists in `apply-progress.md` |
| All Unit 3 tasks have tests | ✅ | `RealtimeSubscriberTests.cs` exists; focused 9/9 passes fresh |
| Behavioral RED confirmed | ⚠️ No | Stale no-build DLL produced no matching test; no behavioral RED exists |
| Current GREEN confirmed | ✅ | Focused 9/9, App.UI 187/187, Service 1,171/1,171, builds and coverage all exit 0 |
| Triangulation adequate | ❌ | Direct-background bypass, grant-side stale completion, concurrent background/dispose, and repeated dispose are not adequately tested |
| Safety net confirmed | ✅ | Both full regressions pass fresh under `--no-restore` |
| Assertion quality | ⚠️ | No tautologies/ghost loops, but queued-order physical assertions occur after `Dispose()` |

**Strict-TDD result**: fresh GREEN and safety nets are real; behavioral RED chronology is absent and required lifecycle triangulation is incomplete.

### Test Layer Distribution

| Layer | Tests | Files | Tool |
|---|---:|---:|---|
| Unit/lifecycle concurrency harness | 9 | 1 | xUnit, FluentAssertions, controlled fakes/barrier |
| Integration | 0 | 0 | Not used for this slice |
| E2E/live Realtime | 0 | 0 | External/live evidence intentionally out of scope |

### Assertion Quality

- All changed tests call production code.
- No tautology, ghost loop, type-only standalone assertion, smoke-only assertion, or production-free assertion was found.
- Typed values, physical subscription state, logical state, handler cardinality, and subscribe cardinality are asserted.
- `QueuedForegroundConnectBeforeBackground_DoesNotStartAfterBackground` proves zero new queued subscriptions, but calls `subscriber.Dispose()` before asserting final physical disconnection. That allows dispose cleanup to satisfy the transport-state assertion.

## Restore, Dependency, Quality, and Budget Audit

- Every fresh .NET command included `--no-restore`; no restore command ran.
- Test binaries were rebuilt with command-line-only `-p:DebugType=portable`; no project configuration was edited.
- No `.csproj`, `.props`, `.targets`, lockfile, package declaration, NuGet config, or dependency diff exists.
- The exact CODE+TEST diff is **400/400**; the hard budget is met exactly with zero headroom and no exception.
- Builds have zero errors. Existing warning corpus remains; changed `RealtimeSubscriber.cs` contributes style/documentation warnings due compressed one-line statements.
- The Service run retains one pre-existing duplicate xUnit test-ID discovery notice while reporting 1,171 passed and 0 failed.

## Issues Found

### CRITICAL

1. **Public `ConnectAsync` bypasses foreground authority.** The implementation never reads `IWindowLifecycleObserver.IsInForeground` and direct calls carry no lifecycle epoch. A fresh passing test establishes both physical subscriptions while the fake lifecycle remains background, violating the foreground-only MUST.
2. **Required grant-side cancellation/stale completion lacks runtime proof.** The policy-side barrier is covered, but cancellation after grant subscribe begins is not. Cobertura confirms the post-grant stale check at line 108 is only 1/2 covered.
3. **Concurrent background/dispose and repeated dispose are not runtime-proven.** No test overlaps those intents or repeats `Dispose`; the idempotent-dispose branch is uncovered. Static inspection cannot establish required scenario compliance.

### WARNING

1. The queued epoch test calls `Dispose()` before physical-state assertions. Zero new subscription cardinality is valid, but final closure is not isolated to the background lifecycle operation.
2. `LifecycleTask` means queue-drain completion, not successful connection; queued failures are swallowed after tracing. `IsConnected` remains honest, but the public seam has ambiguous success semantics.
3. Strict-TDD chronology has no behavioral RED. The stale-DLL `No test matches` result is retained explicitly and not fabricated into a failing assertion.
4. Coverage is 94.04% line / 77.58% branch. Defensive parsing/constructor gaps are non-blocking, but required lifecycle gaps are charged above.
5. The exact 400-line boundary leaves no review-budget headroom; changed code also adds style warnings from compression.

### SUGGESTION

None. This verification is report-only and does not prescribe or perform remediation.

## Final Verdict and Approval

**FAIL**

The epoch remediation closes the previously reported queued foreground-before-background defect for its exercised event path and preserves deterministic cleanup of a started non-cancellable policy subscription. It does not establish the stronger foreground-only contract because direct public connect can subscribe while background, and required grant-side cancellation plus concurrent dispose orderings remain without passing runtime coverage.

**Unit 3 is not approved for the local feature-chain commit boundary.** No commit, push, PR, archive, or Unit 4 preparation was performed. Full SDD7 remains partial at **9/14** and is not archive-ready.
