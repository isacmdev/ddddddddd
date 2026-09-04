# Verification Report

## Verification Report

**Change**: `remote-signal-integrity` — Unit 3 tasks 3.1–3.3 only  
**Branch**: `feat/sdd7-3-foreground-realtime`  
**Base / PR 3 parent**: `eea01b4e32de61b49034673fcd40805799387c43` / `feat/sdd7-2-wns-convergence`  
**Mode**: Strict TDD, hybrid persistence, report-only authoritative fresh re-verification  
**Verdict**: **FAIL**

### Verification Boundary and Freshness

- CodeGraph was attempted first against the exact worktree and reported that no `.codegraph/` index exists. Verification therefore used direct full inspection of both changed files, the exact base diff, and all Unit 3 planning artifacts.
- The historical `verify-report-unit-3.md` was not used as verdict evidence and was not changed.
- Units 1–2 were treated as the approved parent baseline. Units 4–5 were excluded.
- All fresh `dotnet` commands used `--no-restore`. No restore was run.
- No code, test, task, apply, spec, design, project, dependency, configuration, branch, commit, push, PR, archive, or external system was modified by verification.

### Completeness

| Metric | Value |
|---|---:|
| Unit 3 tasks | 3 |
| Unit 3 complete | 3 (`3.1`–`3.3` checked) |
| Unit 3 incomplete | 0 |
| Cumulative SDD7 complete | 9/14 |
| Excluded remaining work | Units 4–5, 5 tasks |

Task checkmarks are complete, but implementation correctness blocks Unit 3 approval.

### Diff, Scope, and Budget Audit

| Check | Fresh result |
|---|---|
| Branch | `feat/sdd7-3-foreground-realtime` |
| Changed tracked files vs base | Exactly `src/ControlParental.App.UI/RealtimeSubscriber.cs` and `tests/ControlParental.App.UI.Tests/RealtimeSubscriberTests.cs` |
| Production numstat | 196 additions, 76 deletions |
| Test numstat | 85 additions, 32 deletions |
| CODE+TEST budget | **281 additions + 108 deletions = 389/400** |
| Project/dependency/config drift | None |
| Service/REST/policy/identity/retry/scheduler/enforcement/Unit 4+ diff | None |
| Initial `git diff --check` | Exit 0, `2026-08-20T17:45:43.6190906-05:00`–`17:45:44.0480842-05:00` |
| Final `git diff --check` | Exit 0, `2026-08-20T17:53:08.9896607-05:00`–`17:53:09.3016289-05:00` |

The deleted `ConnectAsync_Idempotent`, `DisconnectAsync_Idempotent`, and malformed-payload tests were consolidated into the three lifecycle tests. Basic duplicate-connect, repeated-disconnect, stale-callback, and malformed-payload expectations remain, but transport cleanup after each current-generation failure and the queued-foreground-before-background race are not asserted.

### Build and Runtime Evidence

All authoritative commands below used `--no-restore`.

| Command | Start | Exit | Result |
|---|---|---:|---|
| `dotnet build src\ControlParental.App.UI\ControlParental.App.UI.csproj --no-restore --configuration Debug --verbosity quiet -p:WarningLevel=0` | `2026-08-20T17:51:08.6814167-05:00` | 0 | 0 errors; 2 existing NU1601 warnings |
| `dotnet build tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --configuration Debug --verbosity quiet -p:DebugType=portable -p:WarningLevel=0` | `2026-08-20T17:51:09.6288249-05:00` | 0 | 0 errors; portable PDB build; 3 existing NU1601 warnings |
| `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~RealtimeSubscriberTests"` | `2026-08-20T17:48:29.1758295-05:00` | 0 | **8 passed, 0 failed, 0 skipped** |
| `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `2026-08-20T17:48:37.3106537-05:00` | 0 | **186 passed, 0 failed, 0 skipped** |
| `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | `2026-08-20T17:48:52.0296900-05:00` | 0 | **1,171 passed, 0 failed, 0 skipped**; existing duplicate-ID notice |
| `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --collect:"XPlat Code Coverage" --results-directory tests\ControlParental.App.UI.Tests\TestResults\coverage-unit3-authoritative-20260820-1749` | `2026-08-20T17:49:12.1059786-05:00` | 0 | **186 passed**; coverage collected |

Coverage artifact:

`tests/ControlParental.App.UI.Tests/TestResults/coverage-unit3-authoritative-20260820-1749/951a3eb7-11f8-43e1-8d7a-abc22c92c6c6/coverage.cobertura.xml`

One earlier verifier attempt ran the product and test builds concurrently and the test build exited 1 because both WinUI builds contended for `obj/.../input.json`. Both isolated authoritative builds above passed. This was verifier-induced file contention, not a test failure or a reproducible product build failure.

### Spec Compliance Matrix

| Requirement | Scenario | Runtime tests | Result |
|---|---|---|---|
| Realtime is foreground-only and non-authoritative | Foreground event refreshes UI | `PolicyBroadcast_FiresPolicyChangedWithCorrectVersion`; `GrantBroadcast_FiresGrantsChangedWithCorrectData`; full App.UI and Service regressions | ✅ COMPLIANT |
| Realtime is foreground-only and non-authoritative | Background and reconnect failure are isolated | `EnteredBackground_TriggersUnsubscribe`; `StaleConnectCompletionAfterBackground_DoesNotResubscribeOldGeneration`; `FailedConnectAndQueuedFailure_CleanUpAndReconnect` | ❌ FAILING — a queued foreground connect can begin after a background event and temporarily establish both transport subscriptions while backgrounded |
| Realtime is foreground-only and non-authoritative | Cancellation and restart recover safely | `StaleConnectCompletionAfterBackground_DoesNotResubscribeOldGeneration`; `OldPolicyAndGrantCallbacks_AreIgnoredAfterRestart`; focused suite 8/8 | ✅ COMPLIANT for the exercised started-connect/background/dispose and restart paths; the missing pre-start queue race is charged to scenario 2 |

**Compliance summary**: **2/3 scenarios compliant**.

### Concurrency and Lifecycle Audit

| Area | Result | Fresh evidence |
|---|---|---|
| Non-cancellable late completion after a connect has started | ✅ Correct for exercised policy path | `WaitAsync` cancellation is followed by awaiting the owned underlying subscription task; stale completion then unsubscribes. The barrier test asserts physical `policy.IsSubscribed == false`, `grants.SubscribeCallCount == 0`, and logical disconnection. |
| Background invalidation of a queued-but-not-started foreground connect | ❌ Defect | `OnEnteredForeground` queues `ConnectAsync` without capturing a foreground epoch/state. If `OnEnteredBackground` runs before that continuation enters `ConnectCoreAsync`, `InvalidateCurrentGeneration` sees no active generation and returns. The older queued connect then starts, subscribes policy and grant, and can set `readyGeneration` while backgrounded; only the later queued disconnect removes it. |
| Stale normal completion branch | ❌ Untested required path | The `!IsCurrentGeneration` branch after successful policy `WaitAsync` at lines 105–109 has zero hits in Cobertura. The barrier test reaches cancellation/catch cleanup instead. |
| Stale failure completion | ✅ Source-safe; runtime exercised through background cancellation/late ownership | Catch awaits the underlying task, consumes its fault, unsubscribes stale active transport, invalidates, and returns for stale generations. |
| Current policy/grant subscribe failure | ⚠️ Source-safe, assertions incomplete | Catch observes the task and `InvalidateCurrentGeneration` detaches both handlers and unsubscribes both channels. The merged test proves thrown direct failures, contained queued failure, false logical state after queued failure, and later reconnect counts `(4,2)`, but does not assert physical channel state/handler cardinality after each failure. |
| Old callbacks/current callbacks | ✅ Restart path | Generation-capturing handlers and `readyGeneration` suppress old policy/grant callbacks; current callbacks publish typed events exactly once in the restart test. A distinct background-old-callback test is absent. |
| Concurrent background/dispose after blocked connect starts | ✅ Exercised | Invalidation is immediate; repeated dispose/disconnect is deterministic; late policy completion cannot remain subscribed. |
| Underlying task ownership/unobserved faults | ✅ | Every started subscription task is awaited directly or awaited in catch before cleanup; queued lifecycle exceptions are caught and traced. No `_ =` fire-and-forget remains. |
| Locks and reentrancy | ✅ | No channel operation or user event invocation occurs while `lockObj` is held. Event callbacks release the lock before invoking subscribers. |
| Lifecycle gate/deadlock | ✅ for inspected paths | `SemaphoreSlim` acquisition/release is paired in `finally`; queued continuations run on `TaskScheduler.Default`; awaits use `ConfigureAwait(false)`. |
| Duplicate active handlers/subscriptions | ⚠️ Covered for ordinary restart, not all races | Duplicate direct connect is prevented and restart event cardinality is one. The queued foreground/background race still permits a background interval with an active subscription. |
| `LifecycleTask` seam | ⚠️ Non-blocking design warning | The seam is test-only in practice and exposes queue drain, while queued failures are intentionally swallowed and traced. It does not hide logical state (`IsConnected` remains false), but its public, undocumented success semantics can be misread as successful connection rather than completed/contained lifecycle work. |

### Correctness Against Remediated Claims

| Claim | Status |
|---|---|
| 1. Late non-cancellable completion is observed and stale/background/disposed cleanup is deterministic; no background reactivation | ❌ Not fully true: started-connect cleanup is correct, but an older queued foreground operation can start after background and activate transport |
| 2. Barrier proves underlying policy transport is unsubscribed | ✅ `policy.IsSubscribed.Should().BeFalse()` is explicit |
| 3. Policy/grant failures clean up, avoid false connection, contain/observe queued failure, reconnect once | ⚠️ Source supports it and runtime reconnect counts pass; physical transport/handler cleanup is not asserted after each failure |
| 4. Old callbacks ignored; current callbacks exactly once | ✅ For disconnect/restart; background-specific old callback is not separately triangulated |
| 5. Background/dispose during blocked connect invalidates immediately | ✅ Once connect has started; ❌ queued-before-start case remains |
| 6. No fire-and-forget fault, lock/channel misuse, gate deadlock, or ordinary duplicate subscription | ✅ Static audit and runtime tests support this, subject to the queued lifecycle defect |
| 7. App.UI-only non-authoritative acceleration | ✅ Exact diff contains only App.UI subscriber and App.UI tests; no authority leakage |
| 8. `LifecycleTask` is minimal and semantically honest | ⚠️ Queue completion is observable and state remains honest, but success does not communicate contained lifecycle failure and the public seam is undocumented |

### Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Realtime remains App.UI-only | ✅ Yes | No Service or authority-layer diff |
| Generation-aware foreground lifecycle | ❌ Incomplete | Active generations are protected, but queued foreground intent is not invalidated by a newer background transition |
| Typed UI invalidation only | ✅ Yes | Policy/grant events remain typed acceleration notifications |
| No synchronization, identity, REST, retry, scheduler, or enforcement ownership | ✅ Yes | No such calls or changes found |
| One serialized lifecycle gate | ✅ Yes | Gate serializes operations, but serialization alone preserves stale queued intent rather than rejecting it |

### Changed-File and Branch Coverage

| File | Line coverage | Branch coverage | Rating |
|---|---:|---:|---|
| `src/ControlParental.App.UI/RealtimeSubscriber.cs` | **95.23%** | **79.62%** | ⚠️ Aggregate acceptable, required lifecycle branch missing |
| `tests/ControlParental.App.UI.Tests/RealtimeSubscriberTests.cs` | Test code | Test code | Unit test layer |

Changed lifecycle method evidence:

- `InvalidateCurrentGeneration`: line 100%, decision branches 100%.
- `IsCurrentGenerationLocked`: branch 100%.
- `RunLifecycleCoreAsync`: line/branch 100%.
- `ConnectCoreAsync`: line 90.74%, branch 81.25%; uncovered required stale-success path at lines 105–109, plus unexercised disposed-connect lines 82–83.
- `HandlePolicyBroadcast`: line 78.94%, branch 80%; stale-generation branch covered, parsing/error branches partial.
- `HandleGrantBroadcast`: line 83.33%, branch 78.57%; stale-generation branch covered, parsing/error branches partial.
- `Dispose`: line/branch 100%, including repeated-dispose branch.

The aggregate 79.62% branch rate cannot be reduced to a warning because the required stale normal-completion path is not directly covered and the more serious queued-foreground-after-background path has no guard to cover.

### Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Unit 3 table exists in `apply-progress.md` |
| All Unit 3 tasks have tests | ✅ | `RealtimeSubscriberTests.cs` exists; 8 focused tests pass |
| Initial behavioral RED | ⚠️ | Initial execution was infrastructure-blocked by missing prior assets; no behavioral RED is fabricated |
| Current GREEN | ✅ | Focused 8/8; App.UI 186/186; Service 1,171/1,171 |
| Triangulation | ❌ | Started-connect cancellation/restart is triangulated, but queued-before-start background invalidation and stale normal completion are absent |
| Safety net | ✅ | Both full regressions passed fresh with `--no-restore` |
| Portable-PDB coverage | ✅ | Test project rebuilt with `-p:DebugType=portable`; fresh Cobertura artifact retained |

### Test Layer Distribution

| Layer | Tests | Files | Tool |
|---|---:|---:|---|
| Unit/lifecycle concurrency | 8 | 1 | xUnit, FluentAssertions, barrier fakes |
| Integration | 0 | 0 | Not used for Unit 3 |
| E2E/live Realtime | 0 | 0 | Out of scope; no external claim |

### Assertion Quality

No tautologies, ghost loops, type-only standalone assertions, or assertions without production calls were found. Consolidation retains direct typed-value, physical subscription, logical state, callback cardinality, and call-count assertions. The failure-cleanup test is nevertheless incomplete because it does not inspect physical subscription/handler state after each policy/grant failure, and no test covers background invalidation before a queued foreground connect starts.

### Restore and Drift Audit

- Every fresh `dotnet` command included `--no-restore`.
- No restore command was executed.
- Final tracked diff remained exactly the two assigned files.
- No project, props, targets, lockfile, package/dependency declaration, or configuration drift was found.
- Existing NU1601 resolution warnings and the Service duplicate-test-ID notice remain baseline observations, not Unit 3 regressions.

### Issues Found

**CRITICAL**

1. **Foreground-only contract violation**: a foreground lifecycle operation queued before a later background event can start after the background event because queued work carries no lifecycle epoch/desired-foreground state. The background callback invalidates only an already-active generation, then queues disconnect behind the stale connect. A non-cancellable connect can therefore establish policy and grant subscriptions and set `readyGeneration` while the UI is backgrounded.
2. **Required changed lifecycle path is untested**: portable-PDB coverage shows zero hits for the stale successful policy-subscribe branch at lines 105–109. Per the Unit 3 verification gate, an uncovered required changed lifecycle branch is critical.

**WARNING**

1. Initial RED was infrastructure-blocked, so Strict-TDD chronology lacks behavioral RED evidence; current GREEN, triangulation attempts, and safety nets are fresh.
2. The merged failure test does not directly assert physical transport and handler cleanup after each current-generation policy and grant failure.
3. `LifecycleTask` is a public, undocumented queue-drain seam whose successful completion includes contained/traced lifecycle failures; callers must inspect `IsConnected` rather than interpret task success as connection success.
4. Existing analyzer/package warnings remain; changed `RealtimeSubscriber.cs` contributes style/documentation warnings but no build errors.

**SUGGESTION**

- None beyond resolving the critical lifecycle intent race and adding direct barrier/cardinality coverage before re-verification.

### Final Verdict

**FAIL**

Unit 3 is **not approved** for the local feature-chain boundary. The fresh runtime suites are green and the 389-line/scope boundary is exact, but the implementation can activate Realtime in background when foreground connect is queued but has not started, and a required stale normal-completion lifecycle branch lacks direct runtime coverage.

Full SDD7 remains partial at **9/14 checked tasks**, but the checked Unit 3 state is not verification-approved. SDD7 is not final-verified or archive-ready. A Unit 3 code/test commit and Unit 4 preparation are **not eligible** from this verdict and were not performed.
