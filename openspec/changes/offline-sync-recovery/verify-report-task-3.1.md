## Verification Report

**Change**: `offline-sync-recovery`  
**Scope**: independent verification of task 3.1 only  
**Mode**: Strict TDD  
**Artifact mode**: hybrid  
**Date**: 2026-08-19  
**Verdict**: **FAIL**

Fresh build, focused, full-regression, and coverage gates pass, but task 3.1 does not satisfy the restart-safe bounded-reconciliation specification. The implementation can permanently skip an older open event after checkpointing a later closed event, one invocation drains an unbounded number of batches and repeatedly materializes an unbounded cumulative event prefix, required interrupted-restart and in-flight cancellation scenarios are not runtime-tested, and the supplied final patch is not reproducible against the exact base because two files contain corrupted Unicode context.

### Authority and Completeness

| Check | Result |
|---|---|
| Worktree | ✅ `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-3-1` |
| Branch | ✅ `feat/sdd6-3-1-usage-reconciliation` |
| HEAD | ✅ `7e0a173b248cab7b3e42d568efd9cf629e65ab09` |
| Exact parent/base | ✅ `7e0a173b248cab7b3e42d568efd9cf629e65ab09` |
| Scope | ✅ only task-3.1 source/test plus task/apply documents before this report |
| Filesystem status | ✅ eight checked: prior seven plus 3.1; 3.2, 4.1, 4.2 open |
| Engram status | ✅ same recorded state, **8/11** |

The recorded formal state is 8/11, but this verification does not approve task 3.1. Recommended verified state remains **7/11** until the CRITICAL findings are remediated and independently re-verified. No checkbox was edited.

### Runtime Matrix

All commands were finite and used `--no-restore`. Exactly one full Service regression was run after the validated test-project build.

| Gate | Result |
|---|---|
| Actual `ControlParental.Service.Tests.csproj` build | ✅ exit 0, 0 errors; five existing package warnings |
| Exact `UsageReconcilerTests` discovery | ✅ 32 unique cases |
| Exact task-3.1/relevant SQLite-restart execution | ✅ 32 passed, 0 failed, 0 skipped |
| Exactly one full Service regression | ✅ 1135 passed, 0 failed, 0 skipped |
| Fresh focused coverage execution | ✅ 32 passed; Cobertura produced |

Coverage artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-independent-verify-coverage-20260819\381f0baa-b809-492e-b865-908294a1c563\coverage.cobertura.xml`.

### Spec Compliance Matrix

| Requirement / scenario | Runtime and source evidence | Result |
|---|---|---|
| Bounded checkpoint batches, cap 50 | Query uses `Take(50)`, but `while (true)` drains every batch in one invocation and `appliedEvents` materializes every closed event up to the checkpoint on every batch | ❌ FAILING |
| Cancellation at meaningful boundaries | Production checks cancellation per batch and passes tokens to SQL/EF, but the new test pre-cancels before semaphore acquisition and never proves in-flight rollback, checkpoint usability, retry, or release | ❌ UNTESTED |
| Durable checkpoint/applied markers and transactional batch atomicity | Marker, usage update, and checkpoint share the EF transaction; rollback test proves marker rollback and retry. Applied-marker insertion result is ignored and markers are not used to select contributions | ⚠️ PARTIAL |
| Restart continues an interrupted period | File-backed test completes the first run fully, disposes, then performs a no-op second run. It never restarts from partial progress | ❌ UNTESTED |
| Duplicate replay does not count twice | Same-instance rerun after a completed checkpoint leaves totals unchanged, but no marker-only replay or crash window is forced | ⚠️ PARTIAL |
| No continuity loss | Query filters to closed events and advances by event ID. If event 1 is still open while later event 2 is closed, checkpoint advances to 2; when event 1 closes it is forever excluded by `Id > checkpoint` | ❌ FAILING |
| Failure/rollback is consistent and retryable | Controlled SQLite trigger proves applied marker rollback and successful retry, but checkpoint/history fingerprints are not asserted | ⚠️ PARTIAL |
| Repeated triggers remain single-flight | `SemaphoreSlim(1,1)` serializes one instance and a three-call test reaches one final total, but the test has no deterministic entry/release gate and does not prove non-overlap; semaphore disposal can race an in-flight final release | ⚠️ PARTIAL |
| Safe diagnostics | Reconciliation failure logs only exception type and returns/persists fixed `Reconciliation failed` | ✅ COMPLIANT |
| Parameterized SQL | Date, event ID, app ID, and duration values are parameters | ✅ COMPLIANT |
| API compatibility | `IUsageReconciler.ReconcileAsync` signature is unchanged and documentation is expanded | ✅ COMPLIANT |

**Compliance summary**: 3 compliant, 4 partial, 2 untested, 2 failing checks. The two required specification scenarios—interrupted restart and duplicate replay—do not both have passing discriminating runtime coverage.

### Correctness Matrix

| Concern | Result | Notes |
|---|---|---|
| No unbounded work | ❌ | `while (true)` has no per-invocation batch budget; cumulative foreground and usage queries are not bounded. For many batches, the prefix re-scan is also quadratic in total events. |
| Checkpoint continuity | ❌ | Event-ID checkpointing can overtake unfinished lower-ID rows and silently lose them. |
| Atomic batch update | ✅ | EF writes and raw marker/checkpoint commands use the same active transaction. |
| Applied-marker semantics | ⚠️ | `INSERT OR IGNORE` is durable, but its affected-row result is ignored and the contribution query reads `foreground_events`, not applied markers. |
| Retry after database failure | ✅/partial | Trigger failure returns a fixed safe result; same-instance retry succeeds. Complete checkpoint/history state is not asserted. |
| Semaphore release | ✅/partial | `finally` releases after acquired runs and failure retry passes. In-flight cancellation and concurrent `Dispose` are not covered. |
| Schema initialization/adoption | ⚠️ | Additive `CREATE TABLE IF NOT EXISTS` avoids EF model drift, but existing table shape is never validated; malformed pre-existing tables are silently accepted until later SQL fails. |
| Caller/lifecycle semantics | ⚠️ | Interface signatures remain compatible, but `ScheduledWorkService` treats `IsRunning` (watcher lifecycle) as reconciliation activity, while this task's actual single-flight state is private. No caller-level test proves the resulting trigger semantics. |
| Sensitive logging | ✅ in changed reconciliation path | New failure logging is redacted. Existing unrelated WMI/IPC diagnostic behavior was not expanded. |
| Task 3.2+ / scheduler/backend expansion | ✅ absent | No task-3.2, scheduler, backend, dependency, project, or package source change. |

### Design Coherence Matrix

| Decision / invariant | Result |
|---|---|
| Service-owned SQLite durability | ✅ followed |
| Restart-safe checkpoints and replay markers | ❌ continuity defect and missing interrupted-restart proof |
| Bounded, cancellable work | ❌ per-query page is 50, but invocation and cumulative scans are unbounded |
| Single-flight execution | ⚠️ source semaphore exists; discriminating runtime proof and disposal safety are incomplete |
| Versioned/idempotent schema compatibility | ⚠️ additive tables do not modify the EF model, but have no shape/version validation |
| Preserve existing architecture/API | ✅ no replacement architecture or signature break |

### Strict TDD / Evidence Matrix

| Evidence | Result |
|---|---|
| Pre-edit manifest | ✅ SHA `5D8E4267FE5F3A0D8038DE62DA0191F117C836E6E4D0614490B2C5212BDD9D0D`; timestamp precedes RED; three pre-edit source/test Git blobs exactly equal base |
| RED | ✅ SHA `C82657A9DAA4E87846F7ED254D4B4025187339CEF142339C59BCFEC7C33CB745`; UTF-16 log reports 2 failed / 28 passed; failures are bounded-batch and cancellation tests |
| RED discrimination | ⚠️ The failures establish missing implementation, but the final tests still do not discriminate interrupted restart, in-flight cancellation, actual batch bound, or open-row checkpoint continuity |
| GREEN now | ✅ fresh 32/32 focused and 1135/1135 full regression |
| Final patch hash | ✅ bytes hash to requested `F6A09A5DDB85285018EAD0307FFE5AE23AD65994DA28FCAFD1AAFDEAE78DB627` |
| Final manifest hash | ✅ `56A3F873B28591AEA22E37C2248B539D248AEB8E5B535D5CE4347BABEB3E135C`; all three listed current SHA-256 values match |
| Patch reproduction | ❌ direct and cached `git apply --check` against the exact base fail for `IUsageReconciler.cs` and `UsageReconciler.cs`; patch Unicode context contains mojibake bytes (`ÔÇö`) instead of the base UTF-8 em dash |
| Exact diff identity | ❌ native-byte stable patch IDs differ: live diff `11ab65059d6e889dca8e4e246a4852bdf6a884eb`, artifact `9bfad43ef25c82ebf25cbc9ec3963ba140824866` |

### Test Layer Distribution

| Layer | Tests | Files | Notes |
|---|---:|---:|---|
| Unit/component | 26 | 1 | constructor, lifecycle, backfill, event behavior |
| SQLite integration | 6 task-3.1 additions | 1 | in-memory plus one file-backed no-op restart |
| E2E/live backend | 0 | 0 | not required or claimed |
| **Total focused** | **32** | **1** | |

### Assertion Quality

No new tautology, ghost loop, duplicate test ID, or disconnected production call was found. However:

1. `ReconcileAsync_PersistsAppliedMarkersAndProcessesBoundedBatches` asserts only the final marker count, not a maximum-50 boundary or intermediate checkpoint.
2. `ReconcileAsync_FileBackedRestartResumesFromDurableState` performs a fully completed first run, so its name overstates interrupted-restart coverage.
3. `ReconcileAsync_CancellationPropagatesAndLeavesCheckpointUsable` pre-cancels before acquiring the gate and asserts only absent history; it does not inspect checkpoint/markers or retry.
4. `ReconcileAsync_ConcurrentTriggersRemainSingleFlight` asserts final effects without a deterministic overlap probe.

**Assertion quality**: 0 tautologies; 4 task-critical coverage/claim mismatches.

### Changed-Scope Coverage

Fresh complete production delta relative to exact base:

| Scope | Line | Branch | Result |
|---|---:|---:|---|
| `UsageReconciler.cs` changed executable lines | **124/127 = 97.64%** | **32/32 = 100%** across 13 branch-bearing changed lines | ✅ >80% both |

Uncovered changed executable lines: 256–258, the `OperationCanceledException` catch/rethrow. Coverage is numerically strong but does not replace missing scenario evidence.

### Delivery Matrix

| File | Additions | Deletions | Touched |
|---|---:|---:|---:|
| `src/ControlParental.Domain/IUsageReconciler.cs` | 3 | 1 | 4 |
| `src/ControlParental.Service/UsageReconciler.cs` | 174 | 90 | 264 |
| `tests/ControlParental.Service.Tests/UsageReconcilerTests.cs` | 197 | 0 | 197 |
| **CODE+TEST total** | **374** | **91** | **465** |

✅ **465 ≤ 800**; the maintainer-approved `size:exception` covers the >400 work unit. Documentation is separate: 24 additions / 2 deletions = 26 touched lines in `apply-progress.md` and `tasks.md` before this report. No tracked generated, dependency, lockfile, project, props, targets, package-version, task-3.2, scheduler, or backend change exists. `git diff --check` is clean. The temporary CodeGraph index used for source mapping was removed before delivery audit.

### Issues Found

**CRITICAL**:

1. Checkpoint advancement can permanently lose a lower-ID event that was open while a later closed event was processed.
2. Reconciliation is not bounded per invocation: it has an unlimited batch loop and unbounded cumulative prefix/materialization queries.
3. The required real interrupted-period restart scenario has no covering test; the file-backed test restarts only after full completion.
4. Cancellation is tested only before work starts, not at a meaningful in-flight boundary with durable rollback/retry evidence.
5. The final patch artifact cannot be applied to the exact base and is not byte/semantic-diff identical to the live change.

**WARNING**:

1. Applied markers are persisted but not used to select accepted contributions; `INSERT OR IGNORE` results are ignored.
2. Existing recovery-table shapes are not validated or versioned before `CREATE TABLE IF NOT EXISTS` no-op behavior.
3. Single-flight and semaphore release are not proven with deterministic in-flight gates; concurrent disposal can race final release.
4. Existing package/analyzer warnings remain; no new dependency change exists.

**SUGGESTION**: Treat one invocation as a finite page or finite configured page budget, derive application from durable marker ownership, and add deterministic file-backed tests for partial commit/restart, an older open row overtaken by a later closed row, in-flight cancellation, and overlap/release.

### Recommendation and Readiness

| Gate | Recommendation |
|---|---|
| Task 3.1 checkbox | **Reopen / mark unchecked after orchestration**; verifier did not edit status |
| Recorded formal progress | 8/11 |
| Verified progress | **7/11** |
| Task 3.1 readiness | ❌ blocked |
| Tasks 3.2, 4.1, 4.2 | remain open |
| Full-change readiness | ❌ incomplete |
| Archive readiness | ❌ blocked |

## Final Verdict

**FAIL — task 3.1 only.** Runtime and numeric coverage gates pass, and the 465-line delivery fits the accepted cap, but source correctness, required restart/cancellation runtime evidence, and immutable patch reproduction fail mandatory gates.

## Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-3.1.md`
- Engram: `sdd/offline-sync-recovery/verify/task-3-1`
- Session: `sdd6-offline-sync-recovery-task-3-1-verify-20260819`
- Prior reports preserved; no source, tests, task status, commit, push, or PR was changed by verification.
