## Verification Report

**Change**: `remote-signal-integrity` — Unit 1 tasks 1.1–1.3 only (Scheduler / IPC Admission)  
**Version**: SDD7 partial apply at base `0a72ddd3b3271e2844805d2b092b2574d4b703dc`  
**Mode**: Strict TDD  
**Verified branch/worktree**: `feat/sdd7-1-scheduler-ipc` at `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-1`  
**Evidence timestamp**: 2026-08-20T15:06:49.6369834-05:00  
**Verdict**: **FAIL**

### Scope and Boundary

- `HEAD`, tracker branch, and merge base all resolve to the approved boundary `0a72ddd3b3271e2844805d2b092b2574d4b703dc`; no implementation commit exists.
- Scoped tasks 1.1–1.3 are checked. Global progress is 3/14; Units 2–5 are intentionally pending and are not counted as Unit 1 incompleteness.
- Normative scenarios reviewed: `remote-signal-sync` scenarios 1–3 only. Scenario 4 and all other SDD7 capability scenarios were excluded.
- CodeGraph was checked first. This worktree has no `.codegraph/`; per the requested fallback rule, CodeGraph was not used again and all changed source/test symbols were inspected directly.
- The only product/test changes are the eight expected Unit 1 files. `Program.cs` only injects the existing scheduler singleton into `UIMessageHandler`; legacy WNS registration remains unchanged for Unit 2. No Unit 2+ implementation behavior was found.

### Completeness

| Metric | Value |
|---|---:|
| Scoped tasks total | 3 |
| Scoped tasks complete | 3 |
| Scoped tasks incomplete | 0 |
| Global tasks complete | 3/14 |
| Units 2–5 | Intentionally pending; excluded from this verdict |

### Changed-Line Budget and Diff Integrity

| Check | Result | Evidence |
|---|---|---|
| Changed code/test files | ✅ | 4 source + 4 test files |
| Additions | 202 | `git diff --numstat 0a72ddd... -- src/** tests/**` |
| Deletions | 4 | Same command |
| Review-budget changed lines | **206** | Additions + deletions; below 400 by 194 lines |
| Apply-progress claim | ⚠️ | Reports 202 lines, which counts insertions only rather than the 206-line review budget |
| `git diff --check` | ✅ exit 0 | Fresh at 2026-08-20T15:06:49-05:00 |
| Unit 2+ behavior | ✅ absent | No legacy WNS removal, Realtime, integrity, or enforcement changes |

Per-file numstat: Domain scheduler contract 29/0; `Program.cs` 2/1; scheduler 48/0; handler 16/1; tests 107/2 combined.

### Build & Test Execution

All commands ran from the exact requested worktree.

| Command | Start | Exit | Result |
|---|---|---:|---|
| `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --configuration Debug --verbosity minimal` | 2026-08-20T15:00:57.0398145-05:00 | 0 | ✅ 0 errors, 1 existing NU1601 warning |
| `dotnet build tests/ControlParental.App.UI.Tests/ControlParental.App.UI.Tests.csproj --no-restore --configuration Debug --verbosity minimal` | 2026-08-20T15:06:20.091-05:00 | 0 | ✅ 0 errors, 896 analyzer/package warnings |
| `dotnet build ControlParental.sln --no-restore --configuration Debug --verbosity minimal` | 2026-08-20T15:01:05.188-05:00 | 1 | ⚠️ 3 unrelated projects lacked `obj/project.assets.json`; 900 warnings, 3 NETSDK1004 errors. No restore was performed because dependency mutation was forbidden. |
| Focused Unit 1 Service filter (admission, IPC, serialization) | 2026-08-20T15:01:54.8133524-05:00 | 0 | ✅ 6/6 passed, 0 failed/skipped |
| Expanded scenario-support filter (admission, identity, polling, IPC, serialization) | 2026-08-20T15:06:00.0127433-05:00 | 0 | ✅ 21/21 passed, 0 failed/skipped |
| Full `ControlParental.Service.Tests` | 2026-08-20T15:02:05.9275543-05:00 | 0 | ✅ 1,161/1,161 passed, 0 failed/skipped |
| App.UI `WnsLifecycleTests` | 2026-08-20T15:02:29.2763001-05:00 | 0 | ✅ 19/19 passed, 0 failed/skipped |
| Full `ControlParental.App.UI.Tests` | 2026-08-20T15:02:39.2458837-05:00 | 1 | ❌ 174 passed, 12 failed, 0 skipped, total 186 |

The focused Service command was:

```text
dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-build --configuration Debug --filter "FullyQualifiedName~ScheduledWorkServiceAsyncDispatchTests.AdmitSyncAsync|FullyQualifiedName~UIMessageHandlerWnsTests.AuthenticatedTriggerSync|FullyQualifiedName~UIMessageHandlerWnsTests.UnauthenticatedTriggerSync|FullyQualifiedName~UIMessagesJsonContextTests.JsonSerializer_RoundTrip_AllSampleEnvelopes_PreservesData_ViaSourceGen"
```

The expanded 21-test command additionally selected `ScheduledWorkServiceIdentityTests.ExecutePolicySyncAsync` and `ScheduledWorkServiceBackupPollingTests`.

### Full App.UI Failure Investigation

Fresh execution reproduced exactly 12 failures. All fail before behavioral assertions while locating the repository root:

- 7 `WnsRegistrationPageCompositionTests`
- 2 `DeadCodeRemovalTests`
- 2 `AppStartupDiagnosticsTests`
- 1 `AppResourceCompositionTests`

The worktree uses a `.git` **file**, not a directory (`DOTGIT_FILE=True`, `DOTGIT_DIR=False`). The four failing test files require `Directory.Exists(.../.git)`, so they cannot discover a linked-worktree root.

These failures are proven pre-existing and out of the Unit 1 diff: the four failing test files and `src/ControlParental.App.UI/**` have no diff from `0a72ddd...`. Commit `3315ffb72bf8eafa85761ea69b1f4e377a599beb` is an ancestor of the approved base, but it only updates `.gitattributes`, `NamedPipeUIServerHostedAdapterTests.cs`, and `NativeAotPublishConfigurationTests.cs`; it does **not** make these App.UI tests linked-worktree compatible. Therefore the failures are not a Unit 1 baseline regression, but the fresh full-suite command is still non-zero and cannot be reported as passing.

### Coverage

Command:

```text
dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --configuration Debug --no-restore --collect "XPlat Code Coverage"
```

Start 2026-08-20T15:04:04.3859935-05:00; exit 0; 1,161/1,161 tests passed. Coverlet generated:

`tests/ControlParental.Service.Tests/TestResults/2466985e-1abd-4f61-b924-ba357244e4a5/coverage.cobertura.xml`

The generated Cobertura document contains `lines-covered="0"`, `lines-valid="0"`, `branches-covered="0"`, `branches-valid="0"`, and no packages. A preceding attempt with the nonexistent reported settings path `tests/coverage.runsettings` exited 1; no runsettings file exists in the repository.

| Changed file | Line coverage | Branch coverage | Uncovered lines | Rating |
|---|---:|---:|---|---|
| `IScheduledWorkService.cs` | N/A (0/0 reported) | N/A (0/0) | Not measurable | ⚠️ No meaningful data |
| `Program.cs` | N/A (0/0 reported) | N/A (0/0) | Not measurable | ⚠️ No meaningful data |
| `ScheduledWorkService.cs` | N/A (0/0 reported) | N/A (0/0) | Not measurable | ⚠️ No meaningful data |
| `UIMessageHandler.cs` | N/A (0/0 reported) | N/A (0/0) | Not measurable | ⚠️ No meaningful data |

**Changed-scope coverage**: exact line/branch percentages cannot be calculated from the empty 0/0 collector output. This is a WARNING, not fabricated as 0% coverage.

### Spec Compliance Matrix

| Requirement | Scenario | Source evidence | Passing runtime evidence | Result |
|---|---|---|---|---|
| Hints admit bounded durable convergence | Concurrent hints coalesce | `ScheduledWorkService.cs:162-207`, existing startup/timer dispatch at 258-269 and 347-350 | `AdmitSyncAsync_ConcurrentHintsCoalesceToOnePolicyFetch`; polling tests | ⚠️ PARTIAL — only WNS + polling are concurrent; startup completes first, and UI/timer concurrency plus “no required convergence lost” are not covered by one passing scenario test |
| Hints admit bounded durable convergence | Offline or cancelled admission recovers | Cancellation result at `ScheduledWorkService.cs:171-174,203-205`; polling timer retained | pre-cancel test plus separate polling/identity tests | ⚠️ PARTIAL — no test proves recovery after an in-flight cancelled/identity-denied admission, and static cancellation composition has a defect described below |
| Hints admit bounded durable convergence | Malformed or unauthorized hint is harmless | Opaque payload in `WnsPushNotificationHandler.cs:193-206`; authenticated route in `UIMessageHandler.cs:130-140`; bounded enum validation at scheduler 166-169 | malformed/valid WNS lifecycle tests; authenticated/unauthenticated IPC handler tests; unknown-source test; identity-denied scheduler tests | ✅ COMPLIANT |

**Compliance summary**: **1/3 scenarios fully compliant**. Passing fragments do not upgrade scenarios 1–2 to compliant because the required combined behavior is not covered at runtime.

### Correctness (Static Evidence)

| Contract | Status | Evidence |
|---|---|---|
| Existing parameterless `TriggerSync` remains opaque | ✅ | `UIMessages.cs:205-209`; no production change; WNS ignores raw bytes |
| Authenticated route reaches scheduler | ✅ static / ✅ handler unit test | Named pipe calls `HandleAuthenticatedAsync`; handler admits `SyncTriggerSource.Wns` |
| Unauthenticated hint is harmless | ✅ | `HandleAsync` returns unsuccessful response and never calls scheduler |
| Bounded source/result types | ✅ | finite enums in `IScheduledWorkService.cs:48-65`; undefined source rejected |
| Running/disposed guard | ✅ static only | admission rejects when stopped/disposed; no dedicated admission runtime test |
| Single-flight/coalescing | ✅ for normal completion | existing `inFlightWork` owner reused; one-fetch test passes |
| Cancellation and shutdown safety | ❌ | caller token replaces, rather than links with, scheduler lifetime cancellation; coalesced callers can observe another caller's cancellation exception |
| Polling fallback retained | ✅ | 30-second timer and existing polling tests remain |
| Identity/policy/retry ownership retained | ✅ | policy sync still checks coordinator identity and uses `IBackendClient`; adapters add no REST/retry/policy path |

### Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| One scheduler owner | ✅ | `UIMessageHandler` receives the registered `IScheduledWorkService`; no second scheduler/transport added |
| Existing interface plus bounded source/result | ✅ | Matches the design contract shape |
| Pipe → handler → scheduler | ✅ static / ⚠️ runtime gap | Handler-level test passes; no full pipe-to-real-scheduler scenario test |
| Preserve scheduler cancellation ownership | ❌ | `TryDispatchWorkCore` chooses a caller token instead of linking it with `workCancellation` |
| Keep adapter non-authoritative | ✅ | WNS only serializes parameterless `TriggerSync`; no REST, identity, retry, or policy authority added |
| `Program.cs` change necessary and scoped | ✅ | Only injects the existing singleton scheduler into handler; no Unit 2 legacy quarantine |

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ⚠️ | Table exists in `apply-progress.md`, but omits required TRIANGULATE and SAFETY NET columns |
| All scoped tasks have tests | ✅ | Four modified Service test files exist; relevant focused tests execute |
| RED confirmed | ⚠️ | Current diff proves tests reference symbols absent from the base, but no preserved pre-production command, timestamp, or raw compiler output proves historical RED order |
| GREEN confirmed | ✅ | 6/6 direct tests, 21/21 expanded tests, and 1,161/1,161 Service regression tests pass now |
| Triangulation adequate | ❌ | Only scenario 3 is fully covered; scenarios 1–2 are partial |
| Safety net before modification | ⚠️ | Current full regression passes, but the claimed pre-modification safety-net execution is not preserved |
| REFACTOR | ➖ | Subjective; current code inspected without modification |

**TDD compliance**: current GREEN is real, but historical RED→GREEN order is not independently provable from the uncommitted final diff. No historical proof is fabricated.

### Test Layer Distribution

| Layer | Tests | Files | Notes |
|---|---:|---:|---|
| Unit / in-memory harness | 40 | 6 | 21 expanded Service + 19 App.UI WNS tests |
| Integration (real pipe/host boundary) | 0 | 0 | Handler and send overrides are in-memory seams |
| E2E | 0 | 0 | Not required for this local contract slice |
| **Total focused evidence** | **40** | **6** | All 40 passed |

### Assertion Quality

**Assertion quality**: ✅ No tautologies, ghost loops, production-free assertions, or smoke-only assertions were found in the Unit 1 additions. The coalescing test verifies both outcomes and one backend fetch; IPC tests verify result and scheduler interaction; serialization verifies a stable round-trip.

### Quality Metrics

**Compiler/type check**: ✅ affected Service and App.UI test projects build with 0 errors.  
**Analyzers**: ⚠️ large pre-existing warning corpus; no changed-scope-only analyzer baseline is configured.  
**Coverage tool**: ⚠️ installed collector emitted an empty 0/0 report.  
**Diff hygiene**: ✅ `git diff --check` exit 0.

### Issues Found

**CRITICAL**

1. **Cancellation ownership is not safe for admitted shared work.** In `TryDispatchWorkCore`, a cancelable request token is selected instead of being linked with `workCancellation`. Shutdown therefore cannot cancel that admitted policy sync through the scheduler token. If the first admitted caller cancels while another hint is coalesced, the shared task faults with `OperationCanceledException`; the second caller does not satisfy the catch filter because its own token is not cancelled and can observe the exception. This violates cancellable, shutdown-safe, coalesced admission.
2. **Scenarios 1 and 2 lack passing covering tests.** Current tests prove useful fragments but not all-source concurrency/no-lost-convergence or cancellation/offline recovery as specified. Under spec-driven verification, these remain PARTIAL rather than compliant.
3. **The fresh full App.UI test command exits non-zero (12 failures).** The failures are conclusively pre-existing linked-worktree root-discovery defects and not a Unit 1 regression, but the requested full suite is not green. The existing compatibility commit is in the base but does not cover these four App.UI test files.

**WARNING**

1. Meaningful changed-file line/branch coverage is unavailable because Coverlet emitted an empty 0/0 Cobertura report.
2. Strict-TDD historical RED order and pre-change safety-net execution are asserted in `apply-progress.md` but not independently reproducible from preserved command evidence; only current GREEN and the base-symbol absence are verifiable.
3. Full solution `--no-restore` build is not green because three unrelated projects lack assets. Affected project builds are green; dependency restore was intentionally not performed.
4. `apply-progress.md` reports 202 changed lines, while the review-budget definition is additions + deletions = 206.

**SUGGESTION**

- Add a real authenticated pipe → `UIMessageHandler` → real scheduler harness when Unit 1 is remediated; current tests stop at in-memory seams.

### Verdict

**FAIL**

Tasks 1.1–1.3 are marked complete, affected builds and focused regressions pass, and the 206-line slice is correctly bounded. However, cancellation composition violates the Unit 1 contract, only 1/3 normative scenarios has complete passing runtime coverage, and the required full App.UI command remains non-zero. No implementation was modified.

This Unit 1 verdict does **not** verify or close full SDD7. Because Unit 1 failed, the next eligible action is a scoped `sdd-apply` remediation for Unit 1 followed by a fresh Unit 1 `sdd-verify`; Units 2–5 and archive are not yet eligible from this verification result.
