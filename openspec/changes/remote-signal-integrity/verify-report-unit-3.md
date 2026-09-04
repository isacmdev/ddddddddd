# Verification Report

**Change**: `remote-signal-integrity` — Unit 3 tasks 3.1–3.3 only  
**Boundary**: branch `feat/sdd7-3-foreground-realtime`; exact base/PR-3 parent and current `HEAD` `eea01b4e32de61b49034673fcd40805799387c43`; parent branch `feat/sdd7-2-wns-convergence`  
**Worktree**: `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-3`  
**Mode**: Strict TDD, fresh independent verification, hybrid report-only persistence  
**Verification date**: 2026-08-20 (`-05:00`)  
**Source/test mutation**: None

## Verdict

**FAIL**

Unit 3 is **not approved for the local feature-chain boundary**. Fresh builds and all current tests pass, the exact diff is isolated and within budget, and typed foreground events are correctly non-authoritative. However, the cancellation design cancels only `WaitAsync`, not the underlying non-cancellable channel `SubscribeAsync`. A late policy subscription can therefore complete after background `Unsubscribe()` and become active again. The barrier test does not assert the policy channel is closed after late completion, and coverage confirms that the normal stale-completion branch is not executed. Required failed-reconnect cleanup, stale callback suppression, lifecycle-exception observation, and concurrent dispose paths also lack runtime proof.

Full SDD7 remains **partial**. Planning artifacts show 9/14 tasks checked, but Unit 3 is not independently approved; SDD7 is not final-verified or archive-ready. Unit 4+ was not verified.

## Scope and Completeness

| Metric | Result |
|---|---:|
| Unit 3 tasks | 3/3 checked (`3.1`–`3.3`) |
| Cumulative checked tasks | 9/14 (`1.1`–`3.3`) |
| Unit 4–5 tasks | 5 unchecked; intentionally excluded |
| In-scope normative scenarios | 3 |
| Fully compliant scenarios | 1/3 |
| Changed tracked files | 2 |
| Changed CODE+TEST lines | **367/400** |

The cumulative `tasks.md` and `apply-progress.md` retain Units 1 and 2 evidence, add only tasks 3.1–3.3 as checked, and leave 4.1–5.2 unchecked. Final Unit 1 and Unit 2 reports remain present. This verification did not re-judge those units.

## Workspace, Diff, and Boundary Audit

- CodeGraph availability was checked first against the exact worktree. `.codegraph/` was absent, so the worktree was unindexed and verification fell back to direct inspection of every changed source/test line and all supplied SDD context files. No index or configuration was created.
- Verified branch: `feat/sdd7-3-foreground-realtime`.
- Verified `HEAD` and required parent: `eea01b4e32de61b49034673fcd40805799387c43`.
- Tracked diff contains exactly:
  - `src/ControlParental.App.UI/RealtimeSubscriber.cs`
  - `tests/ControlParental.App.UI.Tests/RealtimeSubscriberTests.cs`
- No Service production file, Unit 4+ source/test, project file, props, targets, lock file, NuGet config, dependency declaration, or branch/history change exists.
- The test diff is additive: 98 additions and no deletions. No assertion weakening was found.
- Fresh `git diff --check eea01b4e...` passed with exit 0. Git emitted informational LF→CRLF working-copy notices only.

### Exact CODE+TEST Budget

| File | Additions | Deletions | Total |
|---|---:|---:|---:|
| `RealtimeSubscriber.cs` | 193 | 76 | 269 |
| `RealtimeSubscriberTests.cs` | 98 | 0 | 98 |
| **Total** | **291** | **76** | **367** |
| Review limit |  |  | **400** |
| Headroom |  |  | **33** |

## Fresh Build and Test Evidence

Every .NET command below used `--no-restore`. No restore was performed during verification.

| Evidence | Exact command | Start → end (`-05:00`) | Exit | Result |
|---|---|---|---:|---|
| App.UI product build | `dotnet build "src\ControlParental.App.UI\ControlParental.App.UI.csproj" --no-restore --configuration Debug --verbosity minimal` | `2026-08-20T17:20:53.3257916` → `17:21:00.7029949` | 0 | 0 errors; 274 existing package/analyzer warnings |
| App.UI test build | `dotnet build "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --configuration Debug --verbosity minimal` | `2026-08-20T17:21:20.7473365` → `17:21:28.7902709` | 0 | 0 errors; 275 existing package/analyzer warnings |
| Focused Realtime tests | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~RealtimeSubscriberTests"` | `2026-08-20T17:21:36.0370503` → `17:21:38.6176408` | 0 | **11 passed**, 0 failed, 0 skipped |
| Full App.UI regression | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal` | `2026-08-20T17:21:44.3187525` → `17:21:48.5884713` | 0 | **189 passed**, 0 failed, 0 skipped |
| Full Service regression | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal` | `2026-08-20T17:21:54.1081043` → `17:22:06.5650257` | 0 | **1,171 passed**, 0 failed, 0 skipped; one existing duplicate-ID discovery notice |
| Portable-PDB coverage | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --configuration Debug -p:DebugType=portable --collect:"XPlat Code Coverage" --results-directory "C:\Users\Usuario\AppData\Local\Temp\opencode\coverage-sdd7-unit3-20260820-1722" --verbosity minimal -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura` | `2026-08-20T17:22:16.7649369` → `17:23:02.5498908` | 0 | **189 passed**; valid non-empty Cobertura |
| Final diff check | `git diff --check eea01b4e32de61b49034673fcd40805799387c43` | `2026-08-20T17:23:37.7115494` → `17:23:37.8175455` | 0 | no whitespace errors |

Coverage artifact:

```text
C:\Users\Usuario\AppData\Local\Temp\opencode\coverage-sdd7-unit3-20260820-1722\5ae534e8-2e74-4747-a135-8ed290a93226\coverage.cobertura.xml
```

The coverage run rebuilt with command-line-only portable PDBs and did not edit project configuration.

## Three-Scenario Compliance Matrix

| Scenario | Fresh runtime evidence | Static/concurrency evidence | Result |
|---|---|---|---|
| 1. Foreground typed event refreshes/invalidate UI at most once for the current generation without Service, REST, policy, identity, retry, scheduler, or background-channel authority | `PolicyBroadcast_FiresPolicyChangedWithCorrectVersion`, `GrantBroadcast_FiresGrantsChangedWithCorrectData`, `ConnectAsync_Idempotent`, focused 11/11, App.UI 189/189, and Service 1,171/1,171 passed | Typed `PolicyChanged`/`GrantsChanged` publication occurs only when `readyGeneration` matches; changed production is App.UI-only; no backend, Service, scheduler, identity, retry, or policy-application dependency exists | ✅ **COMPLIANT** |
| 2. Background/close cancels and closes the current subscription; disconnect/reconnect failure is isolated; late events are ignored; Service/polling remains unchanged | Background and post-background event tests pass; Service regression passes. No test causes a current-generation policy/grants subscribe failure or failed reconnect | Background invalidates generation and detaches handlers immediately, but `SubscribeAsync().WaitAsync(token)` cancels only the wait. The adapter's `SubscribeAsync()` has no token. A late underlying subscribe can complete after `Unsubscribe()`, restoring a transport subscription. Failed-connect cleanup is uncovered | ❌ **FAILING / UNTESTED** |
| 3. Cancellation racing with foreground restart suppresses stale completion/callbacks, creates no duplicate active subscription/handlers/authority, observes lifecycle exceptions, and latest foreground generation resubscribes exactly once | `StaleConnectCompletionAfterBackground_DoesNotResubscribeOldGeneration` and `ForegroundRestartAfterCancellation_ResubscribesExactlyOnce` pass | The stale test asserts `IsConnected == false` and zero grants subscribes, but never asserts `policy.IsSubscribed == false` after release. Coverage shows the normal stale-completion branch and stale callback guards are unhit. Queue exception observation and concurrent dispose are untested | ❌ **FAILING / PARTIAL** |

**Compliance summary**: **1/3 scenarios fully compliant**.

## Concurrency and Lifecycle Audit

| Area | Finding | Assessment |
|---|---|---|
| Lifecycle gate | `SemaphoreSlim` serializes `ConnectAsync`/`DisconnectAsync` core operations. Immediate invalidation occurs outside the gate so background need not wait for a blocked connect | Sound intent; no gate deadlock found in tested paths |
| Generation increments | Connect increments once; invalidation increments once when active. `readyGeneration` prevents publication before both subscriptions finish | Correct for state publication; stale callback branch lacks runtime execution |
| Cancellation source ownership/disposal | One CTS is created per generation; invalidation removes it under lock, then cancels/disposes outside the lock | Ownership is clear; disposal is safe for copied tokens |
| Underlying connect cancellation | `IRealtimeChannel.SubscribeAsync()` and `RealtimeChannelAdapter.SubscribeAsync()` accept no cancellation token. `WaitAsync` abandons waiting but does not cancel/undo the underlying subscribe | **Critical race**: late completion can reactivate a background channel after the one `Unsubscribe()` call |
| Handler attach/detach | Per-generation delegates are attached before subscribe and detached on invalidation | No obvious duplicate delegate in ordinary paths; callback already captured by an event invocation relies on the untested generation guard |
| Background invalidation timing | Background invalidates synchronously before queuing disconnect | Correctly suppresses immediate post-background publication, as the passing test proves |
| Failed-connect cleanup | Catch attempts cleanup only if the generation is still current | Current-generation policy/grants subscribe failure and reconnect failure are not tested; cleanup branch has zero coverage |
| Reentrancy/deadlock | User event handlers are invoked outside `lockObj`; channel unsubscribe occurs outside the lock; lifecycle gate is released in `finally` | No direct lock reentrancy/deadlock found. A blocked non-cancellable subscribe can still retain the lifecycle gate indefinitely |
| Concurrent foreground/background | Queue chaining plus gate serializes queued lifecycle work; direct public calls also use the gate | Basic race test passes, but transport cardinality after late completion is not asserted |
| Concurrent dispose | `Dispose` marks disposed then queues disconnect; it does not synchronously invalidate/cancel a blocked connect | A hanging underlying subscribe can delay cleanup indefinitely; dispose race and idempotent-dispose branch are untested |
| Exception observation | Queued lifecycle exceptions are caught and traced, preventing unobserved task faults | Catch lines 216–219 have zero hits. `LifecycleTask` completes successfully after swallowed failures, so callers cannot observe failure state |
| Active-subscription cardinality | Exactly-once test counts subscribe calls for a completed cycle | It does not count active underlying subscriptions/handlers after stale completion. Exact active cardinality is not proven and the late-completion ordering can violate it |

### Concrete Late-Completion Sequence

1. Policy `SubscribeAsync()` starts and blocks inside the channel.
2. Background calls `InvalidateCurrentGeneration()`, cancels the generation token, detaches handlers, and calls `Unsubscribe()` once.
3. `WaitAsync` throws cancellation; because the generation is stale, the subscriber suppresses the exception and performs no second unsubscribe.
4. The original channel subscribe later completes. In the test fake, completion sets `subscribed = true`; the production adapter similarly delegates to a non-cancellable Supabase subscribe task.
5. The UI remains logically disconnected while the transport may be subscribed in background. A later foreground connect may issue another subscribe.

The current stale-completion test releases the blocked subscribe but asserts only logical `IsConnected` and grants-call count. It never asserts the policy transport is closed, so it passes without detecting this sequence.

## Changed Executable Coverage

Cobertura aggregate for all instrumented assemblies is 5,024/39,476 lines (**12.72%**) and 649/7,554 branches (**8.59%**); whole-run aggregate is not the acceptance criterion. `RealtimeSubscriber` is **89.22% line / 74.07% branch**. The aggregate branch percentage is not accepted by itself; changed behavior is mapped below.

| Changed method/branch | Line / branch coverage | Behavioral assessment |
|---|---|---|
| `ConnectCoreAsync` current-generation happy path/idempotency | Happy path hit; connected guard 2/2 | Covered |
| Connect-after-dispose | line 81 condition 1/2; throw lines 82–83 unhit | Uncovered defensive/public lifecycle branch; warning |
| Normal stale completion after policy subscribe | line 101 condition 1/2; return lines 102–103 unhit | **Uncovered required stale-completion behavior; critical** |
| Failed current connect cleanup/rethrow | catch entered once only for stale cancellation; line 121 condition 1/2; rethrow line 126 unhit | **Failed reconnect/current failure untested; critical** |
| `DisconnectCurrentGenerationAsync` current cleanup | condition 1/2; cleanup line 256 unhit | **Required failed-connect cleanup untested; critical** |
| `InvalidateCurrentGeneration` state, cancellation, detach, unsubscribe | 100% line and reported branch | Covered for ordinary active/inactive paths |
| `GetGenerationCancellation` stale-token outcome | 100% line, 50% branch | Stale token branch unproven; warning/related to race gap |
| Queue lifecycle exception catch | method 80% line; catch lines 216–219 unhit | **Lifecycle exception observation not runtime-proven; critical for normative race** |
| Per-generation handler attachment | 100% line | Attachment executes; exact active handler cardinality after late completion is not asserted |
| Policy stale callback guard | line 265 condition 1/2; stale return unhit | **Stale callback suppression branch untested; critical** |
| Grants stale callback guard | line 289 condition 1/2; stale return unhit | **Stale callback suppression branch untested; critical** |
| Typed policy parsing/event | event happy path hit; malformed and exception defenses partly/unhit | Required typed happy path covered; unchanged defensive parsing gaps are warnings only |
| Typed grant parsing/event | event happy path hit; optional/exception defenses partly/unhit | Required typed happy path covered; unchanged defensive parsing gaps are warnings only |
| Foreground/background callbacks | 100% line | Covered |
| `Dispose` cleanup/idempotency/race | 83.33% line, 50% branch; repeated-dispose return unhit | Concurrent dispose and blocked-connect cleanup untested; warning/critical where it intersects foreground-only closure |
| `LifecycleTask` getter | 100% line | Getter executes, but exception outcome is structurally swallowed and untested |

Missing changed behavior coverage is blocking even though the class aggregate is above 80% line coverage.

## `LifecycleTask` API Assessment

`LifecycleTask` is not part of `IRealtimeSubscriber`; it is a new public member on the concrete production class and is used only by the new test to await queued lifecycle work. It grants no Service/REST authority and does not expose transport internals, so it is not a critical architecture violation. It is nevertheless an avoidable production API/test seam: an internal seam (with test assembly visibility), an explicit lifecycle abstraction, or awaitable observer operation could provide observability without expanding the public concrete API. More importantly, because queued exceptions are caught and swallowed before task completion, `LifecycleTask` observes completion but not lifecycle failure.

**Assessment**: WARNING.

## Design Coherence

| Decision/invariant | Result | Evidence |
|---|---|---|
| Realtime remains App.UI-only | ✅ | Only App.UI subscriber and App.UI tests differ; Service regression is unchanged/green |
| No Service enforcement, policy application, REST, scheduler, identity, or retry authority | ✅ | Subscriber dependencies are channels, lifecycle observer, and device ID only |
| Foreground-only transport | ❌ | Logical state/handlers are invalidated, but a non-cancellable late subscribe can reactivate the transport after background unsubscribe |
| Generation-aware stale UI publication suppression | ⚠️ | Design exists; immediate background event test passes; stale callback branches are not executed |
| One lifecycle owner/gate | ✅ | One gate and one generation CTS owner exist |
| Failed reconnect is isolated | ❌ | No runtime test; changed cleanup/rethrow path uncovered |
| Exact active subscription/handler cardinality | ❌ | Subscribe-call count is asserted, active transport cardinality after late completion is not |
| Polling remains fallback | ✅ | No Service production diff and full Service regression passes |
| Unit 4+ isolation | ✅ | No integrity/enforcement/composition Unit 4+ source/test change |

## Strict TDD Audit

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| Six-column evidence reported | ✅ | `apply-progress.md` contains RED, GREEN, TRIANGULATE, SAFETY NET, and REFACTOR evidence for Unit 3 |
| Tests were written before production changes | ⚠️ | Reported chronology is retained, but initial execution was blocked by missing DLL/assets |
| Behavioral RED proven | ⚠️ | Not proven. The initial command failed because assets were missing; it is correctly treated as infrastructure-blocked, never as behavioral RED |
| Current GREEN confirmed fresh | ✅ | Focused 11/11, App.UI 189/189, Service 1,171/1,171, both affected builds, and coverage all exit 0 |
| Triangulation adequate | ❌ | Failed reconnect, normal stale completion, stale callbacks, active-subscription cardinality, queued exception observation, and concurrent dispose are missing |
| Safety net confirmed fresh | ✅ | Full App.UI and Service regressions pass |
| Assertion quality | ⚠️ | No tautologies/ghost loops, but the stale-completion assertion stops short of checking `policy.IsSubscribed == false` after release |

**Strict-TDD result**: current GREEN is real, but required race behavior is not fully triangulated. Historical RED remains an honest warning, not proven RED.

### Test Layer Distribution

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Unit/lifecycle harness | 11 focused cases | 1 | Controlled fake and barrier channels |
| Integration/E2E | 0 | 0 | External/live Realtime intentionally excluded |

### Assertion Quality

- No tautological, ghost-loop, smoke-only, or production-free assertions were found.
- `WaitForAsync` ends with a real assertion and does not silently pass on timeout.
- Subscribe call-count assertions are meaningful for duplicate-start detection.
- Blocking weakness: `StaleConnectCompletionAfterBackground_DoesNotResubscribeOldGeneration` does not assert the late policy subscribe remains closed after `ReleaseSubscribe()`. The fake explicitly sets `subscribed = true` after release, so the missing assertion hides the active-transport race.

## Restore and Dependency Audit

- Historical apply evidence honestly records that initial RED/safety-net execution was blocked by missing `project.assets.json`/test DLL and that an authorized scoped restore later populated assets.
- This fresh verification performed **no restore**. Every build/test/coverage command used `--no-restore`.
- Fresh diff inspection found zero tracked `.csproj`, `.props`, `.targets`, lock/config, package, or dependency changes.
- Existing `NU1601`, analyzer/style, and compatibility warnings remain; no package version was edited.
- The full Service run retained one pre-existing duplicate xUnit test-ID discovery notice while reporting 1,171 passed, 0 failed, 0 skipped.

## Issues

### CRITICAL

1. **Late underlying subscribe can reactivate Realtime in background.** Generation cancellation cancels `WaitAsync`, not `IRealtimeChannel.SubscribeAsync`. Background performs its only `Unsubscribe()` before the blocked subscribe completes; stale completion can set the channel active afterward. This violates foreground-only closure and exact active-subscription cardinality.
2. **The stale-completion test does not prove transport closure.** It never asserts `policy.IsSubscribed == false` after releasing the barrier. Cobertura confirms the normal stale-return branch is not hit; the test exercises cancellation catch/suppression instead.
3. **Required reconnect/failure isolation has no runtime test.** No test makes policy or grants subscribe fail for the current generation and proves cleanup, no duplicate handler/subscription, and a later exactly-once successful foreground reconnect. Relevant cleanup/rethrow branches are uncovered.
4. **Required stale callback and lifecycle-exception behavior is untested.** Both generation-guard stale-return branches and the queued lifecycle exception catch have zero hits. Source inspection alone cannot make those normative paths compliant.

### WARNING

1. `LifecycleTask` is a public concrete-class test seam absent from `IRealtimeSubscriber`; it expands production API while swallowing lifecycle failures rather than exposing their outcome.
2. `Dispose` does not immediately invalidate/cancel a blocked connect; cleanup is queued behind lifecycle work. Concurrent dispose and repeated dispose are not tested.
3. Strict-TDD chronology has no behavioral RED because initial execution was infrastructure-blocked. This is honestly reported and is not itself the reason for failure.
4. `RealtimeSubscriber` aggregate branch coverage is 74.07%; several uncovered branches are unchanged defensive parsing/constructor paths, but the uncovered changed lifecycle branches listed as critical are blocking.
5. Existing package/analyzer warnings and the Service duplicate-test-ID notice remain outside Unit 3 scope.

### SUGGESTION

None. Verification is report-only and does not prescribe or perform a fix.

## Approval and Next Eligibility

**Unit 3 is not approved for the local feature-chain boundary.** The approval-only next actions named in the request—a local Unit 3 code/test commit and then Unit 4 preparation from that exact commit—are **not eligible** under this verdict and were not performed.

The next eligible action is separately authorized Unit 3 remediation followed by another fresh independent Unit 3 verification. Full SDD7 remains partial at 9/14 checked tasks and is not final-verified or archive-ready.
