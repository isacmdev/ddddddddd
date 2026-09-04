## Verification Report

**Change**: `offline-sync-recovery`  
**Scope**: third fresh, independent verification of task 3.2 only after runtime-proof remediation  
**Mode**: Strict TDD  
**Artifact mode**: hybrid  
**Date**: 2026-08-20  
**Verdict**: **FAIL**

The three formerly missing runtime contracts are now covered by production-connected, discriminating tests and passed fresh execution. Build, focused/shared tests, complete changed-production coverage, budget, scope, and exactly one full Service regression also pass. Approval is still blocked by two mandatory evidence gates: the final runtime-proof remediation has no compiling behavioral RED for the three remediated contracts, and the current native patch reproduces Git content but only **5/10** current raw files by SHA-256 and length under the matching checkout settings. `BYTE_REPRODUCTION=False`.

### Authority and Completeness

| Check | Result |
|---|---|
| Worktree | `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-3-2` |
| Branch | `feat/sdd6-3-2-backup-admission` |
| HEAD / exact parent / merge-base | `3ef873100f7d800e35032558136039367e741a42` |
| Formal tasks | **9/11 checked**; 4.1 and 4.2 remain open |
| Independently approved before task 3.2 | **8/11** |
| Prior task-3.2 FAIL reports | All three preserved unchanged |
| CodeGraph | Initialized and used before broad source exploration; generated index removed afterward |

Only task 3.2 was judged. Prior-approved work through 3.1 was treated as inherited, and tasks 4.1/4.2 were not evaluated or modified.

### Build and Runtime Evidence

| Gate | Fresh result |
|---|---|
| Service.Tests build | ✅ 0 errors; 5 package warnings |
| Exact discovery | ✅ 119 task/shared cases; no task-local duplicate-ID warning |
| Task 3.2 + four shared scheduler files + outbox bridge | ✅ **119 passed, 0 failed, 0 skipped** |
| Coverage execution, same filter | ✅ **119 passed, 0 failed, 0 skipped** |
| Exactly one full Service regression after byte-stability check | ✅ **1,156 passed, 0 failed, 0 skipped** |

The pre-regression ten-file SHA check returned `BYTES_STABLE=True`. No second full Service regression was run.

Fresh Cobertura: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task32-third-verify-coverage\0db32a92-1dd8-4a73-bbca-f59741d87ec9\coverage.cobertura.xml`, SHA-256 `20AD6B9A39FE1AADFC65ABD243B19C88E28B27B0DEB2E53F36A7C7FC9FF3D406`.

### Mandatory Runtime-Proof Gates

| Contract | Independent evidence | Result |
|---|---|---|
| Ambiguous `Program.Main` arguments do not enter composition | Public `Main` delegates to the production-used `RunMainAsync`; the observer overload executes the same validation path. `Main_WithAmbiguousBackupArguments_RejectsBeforeCompositionBoundary` invokes it with ambiguous arguments, asserts the production rejection, and asserts the composition callback remains false. Moving composition before validation or removing the return makes the test fail. The observer is per-call, non-global, and non-racy. | ✅ COMPLIANT |
| Database/schema initialization, then host start, then one-shot admission, with teardown | `BackupOrchestration_StartsHostedDependenciesBeforeAdmission` executes production `StartHostAndRunSelectedModeAsync` with real SQLite bootstrap and a real host. Its hosted probe observes `schema_version` before recording host start, then the real Program path records admission; exact order is `host-start`, `admission`. `RunBackupHostModeAsync` production tests prove stop after success and timeout; host disposal and file deletion complete safely. No source-text assertion remains. | ✅ COMPLIANT |
| Concurrent duplicate triggers through actual Program DI and one real scheduler | `ComposedConcurrentDuplicateTriggers_UseTheRealSchedulerSingleFlightOwner` resolves Program's actual `ITaskSchedulerBackup` registration, obtains a real `TaskSchedulerBackupService`, and sends two overlapping reconciliation triggers into the same real `ScheduledWorkService`. A gated real reconciler callback executes once; fresh test passes. | ✅ COMPLIANT |

### Behavior and Architecture Compliance

| Concern | Result | Notes |
|---|---|---|
| Exact backup arguments and invalid/normal separation | ✅ | All three exact modes, null/empty, normal flags, duplicate/mixed modes, and ambiguity are covered. |
| Same singleton actual DI composition | ✅ | Program's registration resolves the registered singleton `IScheduledWorkService`; composed concurrency uses that same instance. |
| Trigger-only backup adapter | ✅ | `TaskSchedulerBackupService` only links cancellation and invokes the admission delegate. |
| Caller cancellation | ✅ | Pre-cancel, live in-flight scheduler cancellation, and Program propagation are runtime-tested. |
| Finite outer bound | ✅ | Production default is exactly 30 seconds; linked-token timeout behavior is runtime-tested with a shorter injected duration. |
| Safe concurrent disposal | ✅ | Trigger and disposal synchronize on one lock; disposal cancels the lifetime token. Disposed-before-trigger and live in-flight disposal are runtime-tested. |
| One single-flight owner | ✅ | Only `ScheduledWorkService.inFlightWork` owns overlap suppression. |
| No direct backend/second retry/delivery/claim path | ✅ | No such path or owner was added in Program or the Task Scheduler adapter. |
| Minimal testability seams | ✅ | `ConfigureBackupAdmission`, lifecycle orchestration, and the per-call composition observer are production-used, local, and do not add a second path or owner. |

### Spec Compliance Matrix

| Requirement / scenario | Runtime coverage | Result |
|---|---|---|
| One bounded scheduler owns retry admission — no overlapping Task Scheduler work | Composed concurrent duplicate trigger test | ✅ COMPLIANT |
| Cancellation and shutdown propagate | Program cancellation/timeout/stop tests, scheduler in-flight cancellation, disposal cancellation | ✅ COMPLIANT |
| Diagnostics/lifecycle safe by default | Runtime SQLite/host/admission ordering and safe teardown | ✅ COMPLIANT |
| Repeated triggers remain single-flight | Actual Program DI → real Task Scheduler adapter → same real scheduler | ✅ COMPLIANT |

**Scoped compliance summary**: **4/4 task-3.2 scenarios compliant at runtime**.

### Strict TDD Provenance

| Check | Result | Evidence |
|---|---|---|
| TDD table present | ✅ | Task 3.2 sections exist in `apply-progress.md`. |
| Prior valid remediation RED preserved | ✅ | `red-clean.txt` compiles and fails `Dispose_CancelsAnInFlightAdmission` 1/0 against the pre-lifetime-cancellation implementation. |
| Original compiler-only RED excluded | ✅ | The historical `CS0182` test-authoring defect remains disclosed and is not accepted. |
| New runtime-proof remediation has compiling behavioral RED | ❌ | No behavioral RED proves the formerly missing ambiguous-composition or composed-single-flight contracts. `red-final-runtime.txt` compiles but fails only the lifecycle test; the apply artifact and `lifecycle-fail*.txt` show the asserted runtime order completed and failure occurred during SQLite file cleanup (`IOException`). That is cleanup/test-infrastructure failure, not a missing runtime contract. |
| GREEN | ✅ | Fresh focused/shared 119/119 and full regression 1,156/1,156. |

**TDD compliance**: **FAIL**. The final runtime tests are valid GREEN proof, but the mandatory pre-remediation behavioral RED → GREEN provenance for the runtime-proof remediation is absent. A compiler-only RED or cleanup failure cannot substitute for a behavior-discriminating RED.

### Assertion Quality and Test Layers

The three remediated tests invoke production-used seams and make non-empty, discriminating assertions. No tautology, ghost loop, source inspection, disconnected mock-only assertion, duplicate test ID, or empty assertion path was found in the task-local files.

| Layer | Evidence |
|---|---|
| Unit/component | Argument parsing, cancellation, timeout, disposal, direct trigger boundaries |
| Integration | Microsoft DI, real host, real SQLite schema adoption, real `TaskSchedulerBackupService`, real `ScheduledWorkService` |
| E2E | None; correctly not claimed |

**Assertion quality**: ✅ current final tests verify real behavior.

### Fresh Complete Changed-Production Coverage

Coverage was remapped by intersecting every added/replacement executable production line and branch in the cumulative diff against exact parent `3ef8731` with fresh Cobertura points.

| Production file | Lines | Branches | Uncovered changed executable lines |
|---|---:|---:|---|
| `Program.cs` | **62/71 = 87.32%** | **6/8 = 75.00%** | 236, 238, 248, 477, 534–538 |
| `TaskSchedulerBackupService.cs` | **33/33 = 100%** | **6/6 = 100%** | none |
| `ScheduledWorkService.cs` | 0/0 | 0/0 | visibility-only change |
| **Complete cumulative changed production** | **95/104 = 91.35%** | **12/14 = 85.71%** | ✅ both strictly >80% |

Interfaces contain no executable sequence points and were not counted as covered code. The aggregate mandatory threshold passes. Program's per-file branch coverage is below 80% and is recorded as a warning, not substituted into the aggregate result.

### Final Patch, Manifest, and Raw-Byte Reconstruction

Current evidence artifacts were independently hashed:

- Current final patch: `final-task32-ten-file.patch`, **55,174 bytes**, SHA-256 `E34E1646C6CBDF0F487D08A6B00AE578AF0226FBA37F200F82C9F82D05D2470F`.
- Current final manifest: `final-task32-ten-file-manifest.txt`, **1,221 bytes**, SHA-256 `1CF40EDC77544B6AD6056DA7AF9EDACADE0A98A4980107F28BE8DB6BA6C6AC55`.
- The manifest's ten SHA-256 entries match the current files, but it contains no lengths and does not prove reconstruction.
- The earlier v5 patch/manifest hashes (`BC8030...` / `3EB083...`) are stale and do not describe the current final runtime-remediation bytes.

An independent native patch generated from the current exact-parent diff has the same SHA-256 `E34E1646...`. In a fresh detached exact-base worktree with `core.autocrlf=true`, `git apply --check --binary` and `git apply --binary` both succeeded. Raw current-versus-reproduced comparison then produced:

| Result | Files |
|---|---|
| SHA-256 + length equal (5/10) | both Domain interfaces, `ScheduledWorkService.cs`, `TaskSchedulerBackupService.cs`, `TaskSchedulerBackupServiceTests.cs` |
| SHA-256 + length unequal (5/10) | `apply-progress.md`, `tasks.md`, `Program.cs`, `ProgramBackupArgsTests.cs`, `ProgramHardeningTests.cs` |

The five failures are mixed working-tree line-ending shapes. Representative mismatches: `Program.cs` current/repro **63,010/63,041 bytes**; `ProgramHardeningTests.cs` **16,270/16,408**; `apply-progress.md` **84,288/84,320**.

**`BYTE_REPRODUCTION=False`**.

### Scope, Budget, and Cleanliness

| Group | Additions | Deletions | Touched |
|---|---:|---:|---:|
| Production/interfaces | 188 | 9 | **197** |
| Tests | 478 | 75 | **553** |
| **CODE+TEST total** | **666** | **84** | **750** |

✅ **750 ≤ 800**; the accepted 401–800 `size:exception` applies. The tracked boundary is exactly ten files: two SDD artifacts and eight authorized code/test files. No task 4+, dependency, project, package, lockfile, generated migration, backend, reconciler, commit, push, PR, branch, index, reset, stash, rebase, or task mutation occurred. `git diff --check` passes. The three earlier FAIL reports remain preserved as untracked evidence.

### Warnings

**Inherited**: fresh build emitted two `NU1601` and three `NU1701` package-resolution/compatibility warnings.  
**Task-local**: analyzer-only naming/documentation warnings exist for the new public test methods and DI-created helper classes (`CS1591`, `CA1707`, `CA1812`); they do not affect runtime results.  
**Coverage**: `Program.cs` changed-branch coverage is 75% per file although complete cumulative branch coverage is 85.71%.

### Issues Found

**CRITICAL**

1. **Strict-TDD provenance fails for the final runtime-proof remediation.** There is no compiling, behavior-discriminating RED against the pre-remediation contracts for ambiguous composition rejection and composed concurrent single-flight. The lifecycle RED failed after behavior completed because cleanup could not delete an open SQLite file; it is not contract RED.
2. **Mandatory raw-byte reproduction fails.** The current native patch applies, but only 5/10 changed tracked files reproduce by both SHA-256 and length under matching Git settings. `BYTE_REPRODUCTION=False`.

**WARNING**

1. `Program.cs` changed-branch coverage is 6/8 = 75%, while the required complete cumulative result still passes at 12/14 = 85.71%.
2. Inherited package warnings and task-local analyzer warnings remain.

**SUGGESTION**: None; verification was read-only for implementation, tests, tasks, and Git.

### Verdict and Exact Next Action

**FAIL — task 3.2 only.** Current runtime behavior is compliant and regression-clean, but the explicit Strict-TDD provenance and raw-byte gates are mandatory and fail independently.

| Decision | Result |
|---|---|
| Formal task count | **9/11 checked** |
| Independently verified count | **8/11** |
| May task 3.2 remain checked? | **No; the owning apply workflow should reopen it** |
| Approved report created? | **No** |

**Exact next action**: preserve the current runtime implementation, reopen task 3.2, capture an immutable pre-replay snapshot, and perform a clearly labeled controlled Strict-TDD replay that produces compiling behavioral RED for each formerly missing runtime contract before restoring the current GREEN behavior. Then normalize all ten tracked working files to the declared Git byte policy, regenerate the native exact-parent patch plus a SHA-256-and-length manifest, prove 10/10 raw equality in a fresh detached worktree, and request another independent verification. Do not start 4.1/4.2 first.

### Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-3.2-third-independent.md`
- Engram topic: `sdd/offline-sync-recovery/verify/task-3-2-third-independent`
- Session: `sdd6-task-3-2-third-verification-20260820`
