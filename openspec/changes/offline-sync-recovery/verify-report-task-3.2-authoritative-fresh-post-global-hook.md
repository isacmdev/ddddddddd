# Authoritative Fresh Independent Verification — Task 3.2 After Global-Hook Removal

## Verification Report

**Change**: `offline-sync-recovery`  
**Scope**: SDD6 task 3.2 only  
**Mode**: Strict TDD with the user-authorized final-proof exception  
**Artifact mode**: hybrid OpenSpec + Engram  
**Date**: 2026-08-20  
**Verdict**: **PASS WITH WARNINGS**

The last blocking process-global test hook is absent from the current bytes. All three required production-connected runtime contracts pass and independently kill buildable causal mutants. Fresh build, focused/shared tests, complete changed-production coverage, exact-parent raw-byte reproduction, scope/budget checks, and exactly one final full Service regression pass.

### Authority and Completeness

| Check | Independently verified result |
|---|---|
| Worktree | `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-3-2` |
| Branch | `feat/sdd6-3-2-backup-admission` |
| HEAD / parent / merge-base | `3ef873100f7d800e35032558136039367e741a42` / same / same |
| Formal task state | **9/11 checked**; 4.1 and 4.2 open/out of scope |
| Independently verified state | **9/11** through task 3.2 |
| Task 3.2 may remain checked | **Yes** |
| Prior task-3.2 FAIL reports | Preserved unchanged |

### Build and Runtime Evidence

| Gate | Fresh result |
|---|---|
| Service.Tests build | **PASS** — 0 errors; 7,595 warnings |
| Three mandatory contract tests | **PASS** — 3 passed, 0 failed, 0 skipped |
| Focused/task/shared execution | **PASS** — 143 passed, 0 failed, 0 skipped |
| Focused coverage execution | **PASS** — 143 passed, 0 failed, 0 skipped |
| Exact focused discovery | 143 runtime cases; no task-local duplicate-ID warning |
| Final byte-stability check | `BYTES_STABLE=True`, 10/10 |
| Full Service regression | **PASS** — exactly one fresh final run: **1,156 passed, 0 failed, 0 skipped** |
| `git diff --check` | **PASS** |

The first focused/three-contract attempt was incorrectly launched concurrently with coverage against the same build output and produced immediate instrumentation/testhost interference. It was rejected as invalid infrastructure evidence. Sequential reruns after coverage completed passed 3/3 and 143/143. No additional full Service regression was run.

### Mandatory Runtime Compliance

| Requirement | Runtime evidence | Result |
|---|---|---|
| Ambiguous `Program.Main` arguments skip composition | `ProgramBackupArgsTests.Main_WithAmbiguousBackupArguments_RejectsBeforeCompositionBoundary` invokes the production-used `RunMainAsync` gate and observes the per-call composition callback | ✅ COMPLIANT |
| Database/schema → host start → one-shot admission; safe teardown | `ProgramHardeningTests.BackupOrchestration_StartsHostedDependenciesBeforeAdmission` runs real SQLite initialization and a real host; exact observed order is `host-start, admission` | ✅ COMPLIANT |
| Actual Program DI → real `TaskSchedulerBackupService` → same `ScheduledWorkService`; duplicate admission once | `ProgramHardeningTests.ComposedConcurrentDuplicateTriggers_UseTheRealSchedulerSingleFlightOwner` sends overlapping triggers through actual registration into one real scheduler; observed calls = 1 | ✅ COMPLIANT |
| Exact backup args and normal path | Parser/request theories cover all three exact modes, null/empty/normal, duplicate, mixed, and ambiguous requests; normal mode runtime waits for shutdown | ✅ COMPLIANT |
| Caller cancellation | Program composition propagates caller cancellation; scheduler has live in-flight cancellation coverage | ✅ COMPLIANT |
| Finite timeout | Production default is exactly 30 seconds; a shorter injected timeout proves finite cancellation and `TimeoutException` conversion | ✅ COMPLIANT |
| Safe disposal | Disposed triggers reject and disposal cancels an active admission | ✅ COMPLIANT |
| Trigger-only; no second owner | Program/adapter delegate to `ScheduledWorkService.RunBackupAsync`; no direct backend, retry, delivery, claim, or second single-flight path exists | ✅ COMPLIANT |

**Scoped compliance summary**: **8/8 task-3.2 requirement groups compliant**; the three final proof contracts are **3/3 runtime-passing and mutation-discriminating**.

### Controlled Mutation Evidence

Each independent reproduction began from exact parent, applied the final candidate patch under `core.autocrlf=true`, then introduced only the causal mutation in a disposable detached worktree. Every mutant built with **0 errors**. Compiler, cleanup, infrastructure, source-text, and unrelated failures were not accepted.

| Contract / current final test | External patch hash check | Independent current-byte result |
|---|---|---|
| Ambiguous args skip composition | Updated supplied patch SHA-256 **`93A2FB3F4E26A67A4D7BE0665B613D3F4D23B7FB7BD3748BC9DE4AFDF55654E8`** matched | **MUTATION_KILLED** — expected `compositionStarted=False`, actual `True` |
| Lifecycle order | Supplied patch SHA-256 `BF19291E55582F1BF63BE6C41BBA4D10C795E8CB271AAB39A6060C386ABF3090` matched | **MUTATION_KILLED** — expected `host-start, admission`, actual `admission, host-start`; no cleanup/SQLite failure |
| Shared single-flight | Supplied patch SHA-256 `17BB643157A7D882A4424E834EED516A611A0823CF427CFBA17F5E2C7AB1ACF7` matched | **MUTATION_KILLED** — expected calls `1`, actual `2` |

These are controlled regression-discrimination runs, **not original RED**.

### Strict TDD Compliance

| Check | Result | Evidence |
|---|---|---|
| TDD evidence reported | ✅ | Task 3.2 sections exist in `apply-progress.md` |
| Genuine behavior-changing RED | ✅ | `Dispose_CancelsAnInFlightAdmission` compiled and failed 1/0 before lifetime cancellation; `red-clean.txt` SHA-256 `0C8E04FBE05D4D9036C624247F3B5C9C00E434C4426D3B1513EE21FD742C565E` |
| Initial compiler-defect RED | ➖ Excluded | `CS0182` test-authoring failure is not accepted as behavioral RED |
| Final proof-only seams | ⚠️ Authorized exception | Clean v5 was already behaviorally green, so historical RED is unrecoverable and not claimed |
| Controlled discrimination | ✅ | All three required causal mutants independently killed |
| Current GREEN | ✅ | 3/3 contracts, 143/143 focused/shared, 1,156/1,156 full Service |

The scoped exception is accepted as a **WARNING** because the user expressly authorized closure when final tests and every task requirement pass. It does not weaken runtime, mutation, coverage, or reproduction gates.

### Assertion Quality and Global-State Audit

The task-3.2 additions in the three task-local test files contain:

- no `Console.Set*`, environment mutation, current-directory mutation, global-static mutation, or parallelization-disable workaround;
- no source-text substitute, reflection-only task-3.2 check, ghost mock, tautology, empty/disconnected assertion, or potentially empty assertion loop;
- no task-local duplicate test ID or process-global/racy hook.

The fixed parser loop has three literal non-empty cases and executes production assertions. The only reflection-based test in `ProgramHardeningTests` is the inherited pre-task `ApplyHardeningAsync` test; task 3.2 did not introduce it. Mocks in the composed single-flight test provide dependencies to a real `ScheduledWorkService` and the assertion observes actual production dispatch.

**Assertion quality**: ✅ no CRITICAL or WARNING defect in task-3.2 assertions.

### Test Layer Distribution

| Layer | Task-3.2 evidence |
|---|---|
| Unit/component | Argument parsing, request admission, timeout, cancellation, disposal, direct trigger behavior |
| Integration | Actual Microsoft DI registration, real `TaskSchedulerBackupService`, real `ScheduledWorkService`, real host, real in-memory SQLite schema initialization |
| E2E | None; correctly not claimed |

### Fresh Complete Changed-Production Coverage

Fresh Cobertura SHA-256: `290A5453B2BF4B415C76DE86B8AFC55E1C0C44A6F6A8003AF3A2C90981C05075` (4,754,225 bytes). Coverage was remapped against every added/replacement executable production line and branch in the cumulative exact-parent diff.

| File | Lines | Branches | Uncovered changed executable lines |
|---|---:|---:|---|
| `Program.cs` | **62/71 = 87.32%** | **6/8 = 75.00%** | 236, 238, 248, 477, 534–538 |
| `TaskSchedulerBackupService.cs` | **32/33 = 96.97%** | **6/6 = 100%** | 86 |
| `ScheduledWorkService.cs` | 0/0 | 0/0 | Visibility-only delta |
| **Complete changed executable production** | **94/104 = 90.38%** | **12/14 = 85.71%** | **PASS — both strictly >80%** |

The Domain interfaces contain no executable sequence points. Program's per-file branch coverage is below 80%, but the mandated complete cumulative branch result is strictly above 80%.

### Scope, Budget, and Design Coherence

| Group | Additions | Deletions | Touched |
|---|---:|---:|---:|
| Production/interfaces | 188 | 9 | **197** |
| Tests | 456 | 75 | **531** |
| **Cumulative CODE+TEST** | **644** | **84** | **728 ≤ 800** |

- The accepted 401–800 `size:exception` applies within the feature-branch-chain.
- Exact tracked candidate boundary: 10 authorized files — 2 SDD artifacts, 5 production/interface files, and 3 tests.
- No task 4+, dependency/project/package/lockfile, generated migration, backend, reconciler, or unrelated source change exists.
- Design remains coherent: one scheduler owns admission/retry/cancellation/single-flight; Task Scheduler is trigger-only; schema/host lifecycle precedes one-shot admission.

### Independent Exact-Parent Raw-Byte Reproduction

| Check | Result |
|---|---|
| Final patch | 62,027 bytes; SHA-256 **`B657209E2E738AAFE4E6529FEE9EC6D5550018E4888050CBBD8B8D491511E193`** — MATCH |
| Final manifest | 2,967 bytes; SHA-256 **`D8770D76B0F1BAD40E2597A4980BCCAE4D7670EB521C61FFDF16F2C0D1DA93AE`** — MATCH |
| Fresh exact-parent detached worktree | `core.autocrlf=true` |
| Native validation/application | `git apply --check --binary` and `git apply --binary` — PASS |
| Every changed tracked file | **10/10 SHA-256 and length equal** |
| Final result | **`BYTE_REPRODUCTION=True`** |

Reproduced current bytes matched the manifest for `apply-progress.md`, `tasks.md`, both Domain interfaces, all three Service files, and all three test files.

### Warnings and Issues

**CRITICAL**: None.

**WARNING**:

1. The user-authorized final-proof Strict-TDD exception remains: clean v5 was already green, so original historical RED for the three final proof seams cannot be recovered. Controlled mutations are not relabeled as RED.
2. `Program.cs` changed-branch coverage is 6/8 (75%) per file, while complete changed-production branch coverage passes at 12/14 (85.71%).
3. Inherited build debt remains: NU1601/NU1701 package warnings and broad existing analyzer/style warnings. Task-local non-blocking analyzer warnings include documentation/layout/naming findings around new public seams/tests (for example CS1591/CA1068/StyleCop); build has 0 errors.

**SUGGESTION**: None for task 3.2.

### Final Verdict and Next Action

**PASS WITH WARNINGS — task 3.2 only.** Every required behavior, discrimination, runtime, coverage, scope, budget, diff, byte-reproduction, and final regression gate passes. The only remaining items are disclosed non-blocking warnings.

- Formal count: **9/11 checked**.
- Independently verified count: **9/11**.
- Task 3.2 may remain checked: **Yes**.
- Exact next action: **task 4.1**, if approved. Task 4.2 remains open after it.

### Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-3.2-authoritative-fresh-post-global-hook.md`
- Engram: `sdd/offline-sync-recovery/verify/task-3-2-authoritative-fresh-post-global-hook`
- No implementation, test, task status, prior report, commit, branch, push, PR, reset, stash, rebase, or amend mutation was performed.
