## Verification Report

**Change**: `offline-sync-recovery`  
**Scope**: new independent final re-verification of task 3.2 only, after second remediation  
**Mode**: Strict TDD  
**Artifact mode**: hybrid  
**Date**: 2026-08-20  
**Verdict**: **FAIL**

Fresh source inspection, raw-byte reconstruction, build, focused/shared execution, changed-production coverage, and exactly one full Service regression were performed from the current stable bytes. Build, tests, coverage, scope, byte reproduction, cancellation, timeout, disposal, and the underlying single-owner production design pass. The mandatory behavioral assertion gate does not: the ambiguous-argument `Program.Main` test is production-connected and checks the exact rejection message, but it would still pass if the rejection were printed and execution then continued into service composition. A task-local source-text test remains the only proof of database/host/admission ordering, and no runtime test drives concurrent duplicate triggers through the actual Program DI → `TaskSchedulerBackupService` → real `ScheduledWorkService` path.

### Authority and Completeness

| Check | Result |
|---|---|
| Worktree | ✅ `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-3-2` |
| Branch | ✅ `feat/sdd6-3-2-backup-admission` |
| HEAD / exact parent / merge-base | ✅ `3ef873100f7d800e35032558136039367e741a42` |
| Formal task count | **9/11 checked**; 4.1 and 4.2 remain open |
| Independently verified count after this verdict | **8/11** |
| Prior task-3.2 reports | ✅ both FAIL reports preserved unchanged |
| CodeGraph | ✅ initialized and used first for call-flow/symbol inspection; generated index removed afterward |

Only this new report was added. Production, tests, task checkboxes, dependencies, branch, index, commits, and prior reports were not modified.

### Build and Runtime Evidence

| Gate | Fresh result |
|---|---|
| Service.Tests build | ✅ 0 errors; 7,578 existing package/analyzer warnings |
| Task 3.2 + shared scheduler/integration filter | ✅ **118 passed, 0 failed, 0 skipped** |
| Coverage execution, same filter | ✅ **118 passed, 0 failed, 0 skipped** |
| Exactly one full Service regression after stable-byte proof | ✅ **1,155 passed, 0 failed, 0 skipped** |

The full regression emitted one inherited duplicate-ID warning for `HttpResponseClassifierTests.Classify_OtherCodes_DefaultToTransient(MultipleChoices)`; it is outside task 3.2. Fresh Cobertura: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task32-final-coverage-20260820\fb2c7b64-7fc5-45a9-9ad4-39911d263239\coverage.cobertura.xml`, SHA-256 `2242C2C0F347A0F46B60D5942C4938498B00741172932C9928737F6192A6FF24`.

### Complete Changed-Production Coverage

Coverage was independently remapped by intersecting fresh Cobertura sequence/branch points with every added/replacement production line in the cumulative diff against the exact parent.

| Production file | Lines | Branches | Uncovered changed executable lines / partial branches |
|---|---:|---:|---|
| `Program.cs` | 58/64 | 6/8 | Lines 471, 531–535; partial branches 108 and 238 (1/2 each) |
| `TaskSchedulerBackupService.cs` | 33/33 | 6/6 | None |
| `ScheduledWorkService.cs` | 0/0 | 0/0 | Visibility-only change; no changed sequence point |
| **Complete changed production** | **91/97 = 93.81%** | **12/14 = 85.71%** | ✅ both strictly >80% |

Interface declarations have no executable sequence points and were not falsely counted.

### Behavioral Compliance Matrix

| Requirement / gate | Runtime and static evidence | Result |
|---|---|---|
| Exact/invalid/normal argument selection | Production parser/request-gate tests pass for all three exact modes, normal requests, duplicates, mixed modes, and ambiguity | ✅ COMPLIANT |
| Ambiguous `Program.Main` is production-connected | Test invokes the real `Program.Main`; the asserted rejection text occurs only in production `Program.cs:240` | ✅ COMPLIANT |
| Ambiguous `Program.Main` proves composition is skipped | Assertion checks `Contains(exact rejection)` only. Removing the following `return` while retaining the write would allow composition and leave the assertion green. No composition sentinel/call counter is observed. | ❌ UNTESTED / non-discriminating for the claimed skip |
| Actual Program DI route | Runtime resolves real `ITaskSchedulerBackup`; one test reaches a real `ScheduledWorkService`, another proves the same registered `IScheduledWorkService` singleton receives mode/token | ✅ COMPLIANT |
| Trigger-only seam / no second owner | Production trigger only links cancellation and invokes `RunBackupAsync`; no backend, claim, delivery, retry loop, or second gate exists | ✅ COMPLIANT |
| Concurrent duplicates through actual composed route | Shared coordinator overlap test and composition tests pass separately, but no runtime test sends concurrent duplicates through Program DI + TaskScheduler backup + real scheduler | ❌ UNTESTED for the required composed scenario |
| Caller cancellation | Task Scheduler rejects pre-cancel; real scheduler has live in-flight cancellation evidence; Program propagates caller cancellation | ✅ COMPLIANT |
| Disposal / teardown | Disposed-before-trigger rejects; disposal cancels a live admission under the service lock | ✅ COMPLIANT |
| Explicit finite outer admission bound | `DefaultBackupAdmissionTimeout` is exactly 30 seconds; linked timeout/caller tokens are used; runtime custom-timeout test proves finite cancellation and `TimeoutException` conversion; host stops after timeout | ✅ COMPLIANT |
| Database/schema/host before admission | Source ordering is correct and host-mode runtime tests pass, but `Main_AdmitsBackupOnlyAfterDatabaseInitializationAndHostStart` reads `Program.cs` text instead of executing the ordering | ⚠️ PARTIAL; source-text is not runtime proof |

**Scoped scenario summary**: 7 compliant, 1 partial, 2 required behavioral gates untested. Under the verification contract, a required scenario is compliant only when a covering runtime test passes.

### Correctness and Design Coherence

| Concern | Result | Notes |
|---|---|---|
| Same singleton owner | ✅ | Program callback resolves `IScheduledWorkService`; real scheduler `RunBackupAsync` uses the existing per-work-type `inFlightWork` owner. |
| No retry amplification | ✅ | No second send/retry/claim/backend path was added. |
| Caller/timeout/lifetime cancellation | ✅ | Tokens are linked at Program and Task Scheduler boundaries. |
| Safe disposal | ✅ | Trigger captures a linked token under the same lock used by disposal; disposal cancels lifetime ownership. |
| 30-second outer timeout | ✅ | Explicit finite default; runtime mechanism proven with an injected shorter timeout. |
| Architecture preservation | ✅ | No supervisor, event bus, polling loop, direct backend path, or second coordinator. |

### Strict TDD Provenance

| Check | Result | Evidence |
|---|---|---|
| TDD table present | ✅ | Task 3.2 remediation section in `apply-progress.md` |
| Preserved remediation baseline | ✅ | 8/8 pre-edit copies independently match length/SHA entries; manifest SHA `5CB089271DD50DD38CE74CBFB79638314211DFEC32AF63AB291E1B893A4BCF07` |
| Compiling behavioral RED | ✅ | `red-clean.txt` reaches VSTest and fails `Dispose_CancelsAnInFlightAdmission`: 1 failed / 0 passed; SHA `0C8E04FBE05D4D9036C624247F3B5C9C00E434C4426D3B1513EE21FD742C565E` |
| Compiler-error-only RED excluded | ✅ | Original `CS0182` attempt remains disclosed and is not accepted as behavioral provenance |
| GREEN | ✅ | Fresh focused/shared 118/118 and full 1,155/1,155 |
| Second-remediation provenance | ✅ limited | It removed/replaced invalid tests without changing production behavior; no fabricated new RED is claimed |

### Test Layer and Assertion Quality

| Audit | Result |
|---|---|
| Disconnected Moq-only/interface ghost class | ✅ removed; no such class remains in `TaskSchedulerBackupServiceTests.cs` |
| Tautologies | ✅ none found |
| Empty assertion loops / ghost loops | ✅ none found; the parser loop has a fixed non-empty input set and production assertions |
| Empty/type-only/non-discriminating assertions | ❌ ambiguous-Main assertion is exact-output based but does not discriminate “reject and return” from “print rejection and continue” |
| Source-text substitute | ❌ `ProgramHardeningTests.Main_AdmitsBackupOnlyAfterDatabaseInitializationAndHostStart` reads source text for lifecycle ordering |
| Production connection | ✅ all other new task-local behavior tests call production code or actual DI/host seams |

**Assertion quality**: **2 CRITICAL**, 0 assertion WARNING: one non-discriminating skip-composition claim and one source-text runtime substitute.

### Raw-Byte Reproduction

- Independently confirmed patch SHA-256: `BC8030205FC32836E2489B2593FCB9AA8313F295256AB4E15E4A45F02DCB2FBE`.
- Independently confirmed manifest SHA-256: `3EB0835078367FC08666FB50632F57A6272F13E41D7A4130F7B00B0D8F34C7FA`.
- Fresh detached exact-base worktree used `core.autocrlf=true`.
- `git apply --check --binary` ✅.
- `git apply --binary` ✅.
- SHA-256 **and length** matched current stable bytes for every changed tracked file: **10/10**.
- **`BYTE_REPRODUCTION=True`**.
- Temporary detached worktree was removed after comparison.

### Scope and Delivery

| Group | Additions | Deletions | Touched |
|---|---:|---:|---:|
| Production/interfaces | 171 | 5 | 176 |
| Tests | 384 | 75 | 459 |
| **CODE+TEST total** | **555** | **80** | **635** |

✅ **635 ≤ 800**; the approved 401–800 `size:exception` applies. `git diff --check` is clean. No task 4+, dependency, project, package, lockfile, generated migration, backend, reconciler, commit, push, PR, branch, or index change exists. The two earlier untracked FAIL reports remain preserved.

Task-local warnings are non-blocking but remain: StyleCop layout/header warnings in `TaskSchedulerBackupServiceTests.cs`, CA1812 for the DI-created blocking helper, inherited package/analyzer volume, and the inherited duplicate test ID noted above.

### Issues Found

**CRITICAL**

1. `ProgramBackupArgsTests.cs:100–116` does not prove that ambiguous arguments stop before composition. It verifies the exact production rejection message, but it remains green if the message is emitted and the subsequent `return` is removed.
2. `ProgramHardeningTests.cs:176–190` is a source-text substitute for runtime lifecycle ordering, contrary to the explicit final assertion-quality gate.
3. No passing runtime test drives concurrent duplicate triggers through actual Program DI → real `TaskSchedulerBackupService` → real `ScheduledWorkService`; therefore the required composed repeated-trigger scenario remains untested.

**WARNING**

1. Existing task-local and inherited analyzer/package warnings remain; none caused a build or runtime failure.
2. The original compiler-defect RED remains invalid historical evidence; only the preserved remediation behavioral RED is accepted.

**SUGGESTION**: None; this read-only verification did not fix implementation or tests.

### Verdict and Next Action

**FAIL — task 3.2 only.** Runtime behavior, coverage, byte reproduction, budget, and the production architecture are strong, but the user's mandatory assertion/composed-runtime gates are not satisfied.

| Decision | Result |
|---|---|
| Formal task count | **9/11 checked** |
| Independently verified task count | **8/11** |
| May task 3.2 stay checked? | **No — it should be reopened/un-checked by the owning apply workflow** |
| Exact next action | Remediate tests only: add an observable composition tripwire proving ambiguous `Main` returns before composition, replace the source-text lifecycle test with runtime evidence, and add a concurrent duplicate-trigger test through Program's real DI registration, real `TaskSchedulerBackupService`, and one real `ScheduledWorkService`; then run a fresh independent task-3.2 re-verification. |

### Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-3.2-final-reverification.md`
- Engram topic: `sdd/offline-sync-recovery/verify/task-3-2-final-reverification`
- Session: `sdd6-task-3-2-final-reverify-20260820`
