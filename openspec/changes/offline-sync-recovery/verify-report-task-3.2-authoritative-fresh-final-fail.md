# Authoritative Fresh Independent Verification — Task 3.2

## Verification Report

**Change**: `offline-sync-recovery`  
**Scope**: SDD6 task 3.2 only, final frozen bytes  
**Mode**: Strict TDD with the user-authorized final-proof-only mutation exception  
**Artifact mode**: hybrid OpenSpec + Engram  
**Date**: 2026-08-20  
**Verdict**: **FAIL**

All runtime, mutation, build, focused/shared, coverage, budget, scope, byte-reproduction, and full-regression gates pass. The candidate nevertheless fails the user's explicit assertion-quality gate because `ProgramBackupArgsTests.Main_WithAmbiguousBackupArguments_RejectsBeforeCompositionBoundary` mutates process-global `Console.Error` while test parallelization remains enabled. That is a global/racy test hook; it is not needed for the causal composition assertion and cannot be accepted on final frozen bytes.

### Authority and Completeness

| Check | Result |
|---|---|
| Worktree | `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-3-2` |
| Branch | `feat/sdd6-3-2-backup-admission` |
| HEAD / parent / merge-base | `3ef873100f7d800e35032558136039367e741a42` / same / same |
| Formal task state | **9/11 checked**; 4.1 and 4.2 open/out of scope |
| Independently verified after this verdict | **8/11** |
| Task 3.2 may remain checked | **No**; report-only verification did not mutate the checkbox |
| Prior task-3.2 FAIL reports | Preserved unchanged |

### Build and Runtime Execution

| Gate | Fresh result |
|---|---|
| Service.Tests build | **PASS** — 0 errors; inherited package/analyzer warnings |
| Focused/task/shared execution | **PASS** — 143 passed, 0 failed, 0 skipped |
| Focused coverage execution | **PASS** — 143 passed, 0 failed, 0 skipped |
| Exact focused discovery | **143 cases**; no task-local duplicate ID observed |
| Full Service regression | **PASS** — exactly one fresh run after `BYTES_STABLE=True`: **1,156 passed, 0 failed, 0 skipped** |
| `git diff --check` | **PASS** |

Commands included:

```text
dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --nologo -v:minimal
dotnet test ... --no-restore --no-build --filter "FullyQualifiedName~TaskSchedulerBackupServiceTests|FullyQualifiedName~ProgramBackupArgsTests|FullyQualifiedName~ProgramHardeningTests|FullyQualifiedName~ScheduledWorkService|FullyQualifiedName~OutboxBridgeIntegrationTests"
dotnet test ... --collect:"XPlat Code Coverage" --results-directory C:\Users\Usuario\AppData\Local\Temp\sdd6-auth-coverage-20260820 --filter <same-filter>
dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --nologo --verbosity quiet
```

Fresh Cobertura SHA-256: `83A52AD0948CC56F1F7F69BB52B22499735EBA1989F50D2356D202B0FF91AA42` (4,754,227 bytes).

### Behavioral Compliance Matrix

| Requirement / scenario | Runtime evidence | Result |
|---|---|---|
| Exact backup args select one mode | `TryParseBackupMode_RecognizesBackupArgs` and request-selection theory | ✅ COMPLIANT |
| Ambiguous args reject before composition | `Main_WithAmbiguousBackupArguments_RejectsBeforeCompositionBoundary`; ambiguous mutant expected false/found true | ✅ Behavior compliant; ❌ test uses forbidden global hook |
| Normal args remain normal mode | normal request/parser cases and `RunSelectedModeAsync_NormalModeWaitsForShutdown` | ✅ COMPLIANT |
| Database/schema initialization and host start precede admission | `BackupOrchestration_StartsHostedDependenciesBeforeAdmission`; lifecycle mutant observed `admission, host-start` instead of `host-start, admission` | ✅ COMPLIANT |
| Safe teardown/disposal | host-stop tests, disposed-trigger rejection, in-flight disposal cancellation | ✅ COMPLIANT |
| Caller cancellation | `RunBackupModeAsync_PropagatesCallerCancellationThroughRealComposition` and scheduler cancellation tests | ✅ COMPLIANT |
| Finite outer timeout | production default is exactly 30 seconds; runtime short-bound timeout converts cancellation to `TimeoutException` | ✅ COMPLIANT |
| Actual Program DI → real `TaskSchedulerBackupService` → same registered `IScheduledWorkService` singleton | two production-composition tests | ✅ COMPLIANT |
| Composed concurrent duplicates use one real `ScheduledWorkService` single-flight owner | composed overlap test; bypass mutant expected 1/found 2 | ✅ COMPLIANT |
| Trigger-only seam; no direct backend or second retry/delivery/claim owner | source and call-path inspection; `TaskSchedulerBackupService` only invokes the admission delegate | ✅ COMPLIANT |

**Compliance summary**: runtime behavior is compliant for all task-3.2 scenarios, but final acceptance is blocked by test-hook quality.

### Controlled Mutation Testing — Independently Reproduced

Each supplied mutation patch was independently applied from the exact parent in a new detached Temp worktree with `core.autocrlf=true`. Each mutant built with **0 errors**, and exactly one production-connected test failed for the required causal assertion. No compiler, cleanup, timeout, source-text, or unrelated kill was accepted.

| Mutant | Patch SHA-256 | Independent causal result |
|---|---|---|
| Ambiguous args enter composition | `7B46F602EAE02E5B303C561F86EDCBD0AE86FEC25E99D4F4F11C6DDB03C3BDB9` | **KILLED** — expected `compositionStarted=False`, actual `True` |
| Admission occurs before host start | `BF19291E55582F1BF63BE6C41BBA4D10C795E8CB271AAB39A6060C386ABF3090` | **KILLED** — expected `host-start, admission`, actual `admission, host-start` |
| Duplicate bypasses single-flight return | `17BB643157A7D882A4424E834EED516A611A0823CF427CFBA17F5E2C7AB1ACF7` | **KILLED** — expected calls `1`, actual `2` |

All three patches were reversed, `git diff --exit-code` and tracked status were clean in every disposable worktree, and the worktrees were removed. These are explicitly **controlled mutation tests**, not original historical RED.

### Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Task 3.2 evidence exists in `apply-progress.md` |
| Genuine behavioral RED for actual remediation | ✅ | `Dispose_CancelsAnInFlightAdmission` compiled and failed before lifetime-cancellation behavior changed |
| Initial compiler-defect RED | ➖ Excluded | `CS0182` was a test-authoring defect and is not accepted as behavioral RED |
| Final three proof-only tests | ⚠️ Authorized exception | v5 reconstruction was already behaviorally green; no historical RED is claimed |
| Controlled discrimination substitute | ✅ | All three required causal mutants independently killed |
| GREEN | ✅ | 143 focused/shared and 1,156 full Service tests passed |

The scoped historical-evidence exception is accepted as a **WARNING**, not a CRITICAL. It does not cause this FAIL.

### Changed-Production Coverage

| File | Lines | Branches | Rating |
|---|---:|---:|---|
| `Program.cs` | 62/71 | 6/8 | Line >80%; branch 75% per file |
| `TaskSchedulerBackupService.cs` | 32/33 | 6/6 | Excellent |
| `ScheduledWorkService.cs` | 0/0 executable delta | 0/0 | Visibility-only change |
| **Complete changed executable production** | **94/104 = 90.38%** | **12/14 = 85.71%** | **PASS — both strictly >80%** |

### Scope, Budget, and Design Coherence

| Check | Result |
|---|---|
| Cumulative CODE+TEST touched lines | **739** = 655 additions + 84 deletions; ≤800 |
| 401–800 authorization | Valid `size:exception` within feature-branch-chain |
| Changed tracked boundary | Exactly 10 authorized files: 2 SDD artifacts, 5 production/interface files, 3 tests |
| Task 4+, dependencies, lockfiles, generated files, backend/reconciler, unrelated scope | None |
| One coordinator owns admission/retry/cancellation/single-flight | Followed |
| Task Scheduler remains trigger-only | Followed |
| Lifecycle starts schema/host before one-shot admission | Followed |

### Independent Raw-Byte Reproduction

| Check | Result |
|---|---|
| Reported patch | SHA-256 `9809CCECE9F00B2F45DF3DB6276EFD8F719DFB1364BB942F1F91FE9E33CA6F06`, 60,056 bytes — **MATCH** |
| Reported manifest | SHA-256 `EE5DD895A4AA6E6824DE73875DF87B502222883D0AF105834DD03EEA41FD9805`, 2,953 bytes — **MATCH** |
| Fresh detached exact-parent checkout | `core.autocrlf=true` |
| Native patch validation | `git apply --check --binary` and `git apply --binary` — **PASS** |
| Every changed tracked file | **10/10 SHA-256 and length matches** |
| Final result | `BYTE_REPRODUCTION=True` |

### Test Layers and Assertion Quality

Changed test files contain direct unit tests for argument/admission boundaries and integration tests for real host, SQLite schema readiness, DI composition, and real scheduler single-flight behavior. No source-text substitute, ghost loop, tautology, disconnected mock-only interface test, duplicate ID, or empty assertion was found in the final task proof.

**CRITICAL**

1. `tests/ControlParental.Service.Tests/ProgramBackupArgsTests.cs:102-115` replaces process-global `Console.Error` through `Console.SetError`. The assembly has no global parallelization disable and this class has no nonparallel collection. The test can race with any concurrently executing console writer or another console-capturing test. This violates the explicit final gate forbidding global/racy test hooks.

**WARNING**

1. The authorized final-proof Strict-TDD exception remains transparently recorded; controlled mutation evidence is not original RED.
2. `Program.cs` changed-branch coverage is 6/8 (75%) per file, although the required aggregate is 12/14 (85.71%).
3. Existing package/analyzer warnings remain; the fresh build has no errors.

**SUGGESTION**: None beyond the required narrow test-only remediation.

### Final Verdict and Next Action

**FAIL — task 3.2 only.** Product behavior and all execution/reproduction gates pass, but the explicit no-global/racy-hook acceptance criterion does not.

- Formal count: **9/11 checked**.
- Independently verified count: **8/11**.
- Task 3.2 may remain checked: **No**.
- Required next action: perform a narrow test-only remediation that removes the process-global `Console.SetError` hook while preserving the per-call composition-boundary assertion, then run a fresh independent task-3.2 verification. **Do not proceed to 4.1 yet.** If that remediation is independently approved, 4.1 is next.

## Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-3.2-authoritative-fresh-final-fail.md`
- Engram: `sdd/offline-sync-recovery/verify/task-3-2-authoritative-fresh-final-fail`
- No source, test, task-status, commit, branch, push, PR, reset, stash, rebase, or amend mutation was performed.
