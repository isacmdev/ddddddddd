# Verification Report

**Change**: `remote-signal-integrity` — final authoritative Unit 4B2 verification
**Version**: N/A
**Mode**: Strict TDD, hybrid persistence, autonomous partial-slice gate
**Verdict date**: 2026-08-21

## Completeness

| Metric | Value |
|---|---:|
| Parent change tasks total | 14 |
| Tasks complete | 9 |
| Tasks incomplete | 5 |
| Unit 4B2 CODE+TEST diff | 336 / 400 lines |

The unchecked parent tasks remain intentional because Unit 4C and Unit 5 are outside this child slice. This report does not claim completion of parent tasks 4.1–4.3.

## Build & Tests Execution

**Build**: ✅ Passed with 0 errors and 5,169 existing warnings.

```text
dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore -p:DebugType=portable
```

**Focused completion/lifecycle matrix**: ✅ 6 passed, 0 failed, 0 skipped.

```text
dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --filter <five focused methods/theories>
```

**AntiTamperMonitor suite**: ✅ 53 passed, 0 failed, 0 skipped.

```text
dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --filter "FullyQualifiedName~AntiTamperMonitorTests"
```

**Full Service three-run gate**: ➖ Not rerun after the source audit found a blocking completion-order defect. Historical apply evidence reports 1,207/1,207 three times, but that evidence cannot override the missing happens-before guarantee or substitute for a covering runtime test.

**Coverage**: ➖ No fresh final-verification coverage run. Historical post-fix coverage hosts both report 1,207/1,207; coverage does not exercise the missing terminal-order assertion.

## Behavioral Compliance Matrix

| Requirement / scenario | Test evidence | Result |
|---|---|---|
| Non-cancellable admitted work drains during Stop | `ConcurrentStop_SharesDrainUntilAdmittedWorkReleases`, `AdmittedMutationTask_IsOwnedAndDrained` | ✅ COMPLIANT |
| Reentrant same-generation Stop does not self-deadlock | `SynchronousHandler_ReentrantStopReturnsCompletedBeforeConsumption` | ✅ COMPLIANT |
| Caller cancellation remains externally observable while owner drains | `ExternalCancellation_IsReturnedToCallerAndOwnerCanDrain` | ✅ COMPLIANT |
| Owned admission is removed before drain cleanup | `SynchronousAdmission_IsRemovedBeforeDrain` | ✅ COMPLIANT |
| Stop/Dispose cannot complete before the corresponding externally returned operation task is terminal | No test asserts the operation task's terminal state at the instant the drain/Stop task completes | ❌ UNTESTED |
| Runtime-integrity restart/recovery semantics | Unit 4B2 tests cover lifecycle suppression and fresh generations; full requirement also depends on Unit 4A/4C | ⚠️ PARTIAL |

**Compliance summary**: 4 compliant, 1 untested, 1 partial for the Unit 4B2 completion/lifecycle gate.

## Correctness (Static Evidence)

| Requirement | Status | Notes |
|---|---|---|
| No inline external continuation before ownership removal | ✅ Implemented | External completion uses `RunContinuationsAsynchronously`; the prior dump-proven self-drain cycle is addressed. |
| External and owned completion preserve success/fault/cancellation shape | ⚠️ Partial | Both completion sources receive the same terminal category and exception collection, but exact cancellation-token identity is not preserved by parameterless `TrySetCanceled()`. |
| Drain completion orders before external task terminality | ❌ Not guaranteed | `owned.TrySet*` runs before `completion.TrySet*`. The owned task's synchronous removal continuation can unblock `DrainGenerationAsync`, complete the shared drain, and complete `StopAsync` before the external completion source is set. |
| No lifecycle lock spans admitted collaborator execution | ✅ Implemented | Work starts only after the admission gate is released outside the lifecycle lock. |

## Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| One generation owns cancellation, admitted tasks, and drain | ✅ Yes | Ownership remains generation-scoped. |
| Lifecycle completion is deterministic and externally observable | ❌ No | The split completion removes the deadlock but introduces an ordering window between owner drain completion and public task terminality. |
| Unit 4C and Unit 5 remain untouched | ✅ Yes | Only `AntiTamperMonitor.cs` and its test file have tracked content changes. |

## TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Unit 4B2 evidence and dump-backed self-drain RED are present in `apply-progress.md`. |
| RED chronology complete | ⚠️ | The dump-backed self-drain defect has genuine RED evidence; earlier B2 edits retain an explicitly acknowledged historical RED gap. |
| GREEN confirmed | ✅ | Focused 6/6 and AntiTamperMonitor 53/53 passed in this verification. |
| Triangulation adequate | ❌ | No test triangulates Stop/drain completion against the public operation task's terminal state. |
| Safety net | ⚠️ | Focused suite passed; final full Service three-run and coverage gates were skipped after the blocker was found. |

**TDD Compliance**: incomplete; clean PASS is impossible because of the historical chronology warning, and the missing terminal-order test is blocking.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit/component-isolation | 53 | 1 | xUnit, Moq, FluentAssertions |
| Integration | 0 in this focused file | 0 | — |
| E2E | 0 | 0 | — |

## Changed File Coverage

No fresh final-verification coverage was collected after the blocking audit finding. Historical apply coverage is retained as non-authoritative supporting evidence only.

## Assertion Quality

The changed test file uses production-calling barriers and behavioral/resource assertions; no tautology or ghost-loop assertion was found. The critical omission is semantic: existing assertions never observe public operation terminality at the exact Stop/drain completion boundary.

## Quality Metrics

**Build/type checking**: ✅ 0 errors  
**Analyzers**: ⚠️ 5,169 existing warnings across the test build; no new changed-file error was reported.  
**Diff check**: ✅ Passed.  
**Preserved reports**: ✅ SHA-256 values remain `681747c3...013d` and `968bb883...e519`.

## Issues Found

### CRITICAL

1. **Public completion can lag Stop/drain completion.** In `AntiTamperMonitor.cs:235`, the continuation completes `owned` before `completion`. Completing `owned` synchronously runs ownership removal (`AntiTamperMonitor.cs:268`), which may release the drain's `Task.WhenAll` (`AntiTamperMonitor.cs:281`) and allow `StopAsync` to finish (`AntiTamperMonitor.cs:195`) before `completion.TrySet*` executes. `RunContinuationsAsynchronously` protects against inline external continuations, but it does not make the external task terminal before drain completion.
2. **The required ordering scenario has no passing runtime test.** `SynchronousHandler_ReentrantStopReturnsCompletedBeforeConsumption` proves the callback can request Stop and the generation eventually drains, but it does not assert that the externally returned operation task is already terminal when Stop completes.

### WARNING

1. Strict-TDD chronology remains incomplete for earlier B2 changes, as already acknowledged in `apply-progress.md`.
2. Parent tasks remain 9/14 because this is a partial child slice; no parent-level completion may be claimed.

### SUGGESTION

None.

## Verdict

**FAIL**

The dump-proven inline-continuation deadlock is fixed, and all focused tests pass, but the replacement completion ordering does not prove the required Stop/public-task terminality guarantee. Unit 4B2 is not ready for approval or archive.
