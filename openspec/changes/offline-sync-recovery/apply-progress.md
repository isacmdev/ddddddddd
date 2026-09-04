# Apply Progress: Offline Sync Recovery

Change: `offline-sync-recovery`
Mode: Strict TDD
Artifact store: hybrid
Delivery: exception-ok within feature-branch-chain
Boundary: PR2 task 2.2 from exact parent `0c671fa8ad937231be73dc19a93d37cac59c760c`; one autonomous scheduled-delivery unit, 800-line cap.
Current boundary: final task 4.2 evidence-only child from exact parent/base `e224401018caebe4c84d9a861cc9c305fa559511`; task-4.2 artifact set is docs/evidence/tasks/apply-progress only; CODE+TEST = 0.

## Cumulative State

- [x] 1.1A — delivery-boundary refactor accepted against the audited baseline anchor; behavior green, with historical provenance warnings retained.
- [x] 1.1B1 — completion/failure boundary accepted against the audited baseline anchor; behavior green, with historical provenance warnings retained.
- [x] 1.1B2a — recovery/restart/cancellation/busy/rollback boundary accepted against the audited baseline anchor; behavior green, with historical provenance warnings retained.
- [x] 1.1B2b — audit/requeue/legacy/scheduler boundary accepted against the audited baseline anchor; behavior green, with historical provenance warnings retained.
- [x] 1.2 — base-column-definition remediation complete after final verification; latest reverify FAIL was remediated without changing unrelated scope.
- [x] 2.1 — implementation/evidence complete; current linked worktree evidence passed the required full Service regression and the task remains checked.
- [x] 2.2 — controlled replay produced contemporaneous pre-fix RED and post-fix GREEN for the already-correct cancellation path; final safety-snapshot equality, replay patch reproduction, and all gates pass. Formal progress 7/11.
- [x] 3.1 — remediation complete after independent FAIL; 3.2 remains open.
- [x] 3.2 — prior implementation failed independent verification; cumulative remediation closes behavior RED/GREEN, runtime composition, bounded timeout admission, complete changed-scope coverage, assertion quality, and all-tracked-file raw-byte reproduction. Historical formal progress was 9/11 before later evidence children.
- [x] 4.1 — independently failed, then closed with the user's explicit narrow historical exception for only the unrecoverable foundation-terminal → first task-1.2 pre-state link. Task-1.2 whole-child no-double-count numstat remains UNKNOWN under that exception; internal chain, final blobs, runtime gates, and all other child evidence remain required. Corrected totals are 2.2 DOC/EVIDENCE=726 and 3.1=413; stale 3.2 external hashes are superseded by the committed diff authority. CODE+TEST remains 0; task 4.2 subsequently closed as final evidence.
- [x] 4.2 — final coverage/evidence report. Fresh final mapping against `7b74a0a..HEAD` covers 379/390 changed executable production lines (97.18%) and 107/116 branches (92.24%); focused SDD6 filter 315/315 and exactly one final full Service regression 1,156/1,156 pass. Coverage XML: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task42-final-coverage\10177cf9-b8ef-43a8-8a6b-376ea6b45f15\coverage.cobertura.xml`, SHA-256 `599e3d14a126554737f29c58af3f0e347c2503eb438a62c8134160af212d9509`. CODE+TEST=0; live backend, unsupported Windows matrix, SDD5, SDD7, and SDD8 remain PENDING/NOT CLAIMED.

## Task 3.2 Controlled-Mutation Closure — 2026-08-20

The v5 reconstruction was verified as behaviorally green before any production edit: the Service.Tests build completed with 0 errors and the focused v5 task set passed 34/34. The original chronological RED for the final runtime-proof remediation is therefore unrecoverable and is **not claimed**. Per the maintainer's explicit exception — “cierralo siempre y cuando los test pasen en verde y se cumpla con los requisitos de la tarea” — controlled mutation testing is recorded only as regression-discrimination evidence, never as TDD RED.

| Contract | Disposable mutant | Exact mutation evidence | Result |
|---|---|---|---|
| Ambiguous args reject before composition | `RunMainAsync` invokes `compositionStarted()` before `TryParseBackupRequest` | Patch `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\mutation-ambiguous.patch`; SHA-256 `7B46F602EAE02E5B303C561F86EDCBD0AE86FEC25E99D4F4F11C6DDB03C3BDB9`; run log `mutation-ambiguous-run.txt` | **MUTATION_KILLED** — build 0 errors; one-test command failed at `ProgramBackupArgsTests.Main_WithAmbiguousBackupArguments_RejectsBeforeCompositionBoundary`, expected `compositionStarted` false but found true. |
| Schema/database then host start then admission | `StartHostAndRunSelectedModeAsync` admits backup directly before `StartHostAfterDatabaseInitializationAsync` | Patch `mutation-lifecycle-order.patch`; SHA-256 `BF19291E55582F1BF63BE6C41BBA4D10C795E8CB271AAB39A6060C386ABF3090`; run log `mutation-lifecycle-order-run.txt` | **MUTATION_KILLED** — build 0 errors; one-test command failed with observed order `admission, host-start` versus expected `host-start, admission`; no cleanup/SQLite failure. |
| Composed duplicate single-flight | Duplicate `inFlightWork` path removes the existing task instead of returning it | Patch `mutation-singleflight.patch`; SHA-256 `17BB643157A7D882A4424E834EED516A611A0823CF427CFBA17F5E2C7AB1ACF7`; run log `mutation-singleflight-run.txt` | **MUTATION_KILLED** — build 0 errors; one-test command failed at `ProgramHardeningTests.ComposedConcurrentDuplicateTriggers_UseTheRealSchedulerSingleFlightOwner`, expected calls 1, actual 2. |

All three mutants were restored with reverse binary patches, each detached worktree returned to zero tracked diff, and the disposable mutant worktrees were removed. The real worktree was never mutated by a production mutant.

The lifecycle runtime test now uses an externally owned open in-memory SQLite connection and does not delete a database file or close the externally owned connection. Task 3.2 remains checked only after the final green, coverage, regression, budget, and byte-reproduction gates below; tasks 4.1–4.2 remain open.

### Final Candidate Gates Before Byte Freeze

- Fresh Service.Tests build after the lifecycle test strengthening: **0 errors** (inherited package/analyzer warnings only).
- Fresh focused/task/shared filter `TaskSchedulerBackupServiceTests|ProgramBackupArgsTests|ProgramHardeningTests|ScheduledWorkService|OutboxBridgeIntegrationTests`: **143 passed, 0 failed, 0 skipped**.
- Fresh coverage run with the same filter: **143 passed, 0 failed, 0 skipped**; changed-production mapping against exact parent: **94/104 lines = 90.38%**, **12/14 branches = 85.71%**. `Program.cs` is 62/71 lines and 6/8 branches; `TaskSchedulerBackupService.cs` is 32/33 lines and 6/6 branches; `ScheduledWorkService.cs` has no executable changed delta.
- CODE+TEST delta before final byte normalization remains within the hard cap: **740 touched lines** against exact parent; no task 4+, dependency, generated, lockfile, backend, reconciler, or unrelated source changes.
- The exactly-one full Service regression and the final ten-file raw-byte reconstruction are run only after the final tracked-byte freeze. External patch/manifest method and paths are recorded outside tracked artifacts; final self-referential hashes are intentionally not written into tracked files.

## Task 3.2 Narrow Final Test-Hook Remediation — 2026-08-20

The authoritative fresh final verification report `verify-report-task-3.2-authoritative-fresh-final-fail.md` identified one and only one blocker: `ProgramBackupArgsTests.cs` used process-global `Console.SetError` while test parallelization remained enabled. The hook and output assertion were removed. The production-connected per-call `RunMainAsync` composition observer remains the sole ambiguous-args proof: it asserts `compositionStarted` stays false and therefore causally kills an early-composition mutant. No production code changed.

The updated disposable ambiguous-composition mutant was independently rebuilt and run against the final test:

- Mutation: invoke `compositionStarted()` before `TryParseBackupRequest`.
- Patch: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\mutation-ambiguous-updated-final.patch`.
- Patch SHA-256: `93A2FB3F4E26A67A4D7BE0665B613D3F4D23B7FB7BD3748BC9DE4AFDF55654E8`.
- Run log: `mutation-ambiguous-updated-final-run.txt`.
- Build: 0 errors; focused test failed causally at `Main_WithAmbiguousBackupArguments_RejectsBeforeCompositionBoundary`, expected `compositionStarted` false but found true.
- Result: **MUTATION_KILLED**, explicitly controlled mutation evidence and not historical RED. The mutant was reverse-applied, restored to zero tracked diff, and the disposable worktree was removed.

Task-local audit after remediation found no `Console.Set*`, environment mutation, current-directory mutation, parallelization disable, or other process-global hook in `ProgramBackupArgsTests.cs`, `ProgramHardeningTests.cs`, or `TaskSchedulerBackupServiceTests.cs`.

Fresh post-remediation gates before the final byte freeze: the three-contract proof filter passed **3/3**; focused/shared passed **143/143**; changed-production mapping remained **94/104 lines = 90.38%** and **12/14 branches = 85.71%**; CODE+TEST remains below 800 touched lines. The final full regression and exact ten-file reconstruction are performed only after the final tracked-byte freeze. Task 3.2 remains checked only after those gates; 4.1–4.2 remain open.

## Preserved Failed Attempt

The previous Unit 1 implementation was independently verified as **FAIL** in `verify-report-unit1.md`:

- 19 focused tests and Service build/regression passed, but required concurrency, crash replay, exhaustion, cancellation, audit, and mixed-outcome coverage was missing.
- Claim/ack/fail used select-then-tracked-save rather than conditional final writes.
- Eligibility was filtered after `LIMIT`.
- Legacy `MarkSentAsync` deleted rows and `MarkFailedAsync` persisted raw errors.
- The migration/snapshot artifacts were partial/inconsistent and were not executed.
- The candidate was 576 touched lines and not isolatable from the polluted worktree.

## Delivery-Boundary Refactor — Tasks 1.1A–1.1B2b

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 1.1A | `OutboxClaimAdmissionTests.cs`, `OutboxContractsTests.cs` | Autonomous admission/contracts plus A-owned shared SQLite fixture | n/a — delivery-only refactor | n/a — no behavior RED expected | ✅ 5 admission + 97 Domain tests | ✅ eligibility/order-before-limit, bounded claim admission, concurrent claimers, invalid bounds, constructor seams | ✅ removed enum/input-only assertion; pre-existing `OutboxManagerTests.cs` SDD6 claim churn extracted |
| 1.1B1 | `OutboxLifecycleCompletionTests.cs` | Conditional lifecycle integration | n/a — delivery-only refactor | n/a — no behavior RED expected | ✅ 4 focused tests | ✅ complete/fail row-count guards, reclaim generation, stale completion/failure, mixed outcomes, exhaustion/dead-letter | ✅ unchanged behavior |
| 1.1B2a | `OutboxRecoveryTests.cs` | Recovery/restart integration | n/a — delivery-only refactor | n/a — no behavior RED expected | ✅ 7 focused tests | ✅ bounded recovery, file restart, cancellation, busy bound, rollback | ✅ unchanged behavior |
| 1.1B2b | `OutboxBridgeIntegrationTests.cs` | Bridge/scheduler integration | n/a — delivery-only refactor | n/a — no behavior RED expected | ✅ 12 focused tests (including 3 invalid-source cases) | ✅ audit/requeue, legacy bridge redaction/guards, real scheduler failure progression | ✅ unchanged behavior |

## Corrective Implementation

- Claim selection applies eligibility and deterministic `created_at,id` ordering before `LIMIT`.
- Claims use SQLite `BEGIN IMMEDIATE` plus conditional `UPDATE` predicates; concurrent losers return no claim.
- Completion and failure are single conditional database writes guarded by ID, operation ID, generation, status, and active lease.
- Expired claims are recovered with a bounded conditional update.
- Exhaustion/permanent outcomes remain durable dead letters; only allowlisted safe codes are persisted.
- Requeue is authorization-gated, conditional, idempotent, preserves operation identity, and appends safe audit lineage.
- Legacy methods now durably acknowledge/fail the existing row without deletion or raw error persistence; Unit 2 may retire this compatibility bridge later.
- Removed only the prior failed SDD6 migration and partial snapshot artifacts:
  `src/ControlParental.Service/Migrations/20260818094500_OfflineSyncRecovery.cs` and
  `src/ControlParental.Service/Migrations/ControlParentalDbContextModelSnapshot.cs`.

## Verification Evidence

- Target baseline captured outside the repository: `C:\Users\Usuario\AppData\Local\Temp\sdd6-pr1a-target-baseline.txt`.
- Safety net: `dotnet test ... --filter FullyQualifiedName~OutboxManagerTests --no-restore --verbosity quiet` → 19 passed.
- RED evidence limitation: the newly added tests were not preserved as a separate test-only commit/artifact. The exact pre-production build output is retained in tool output and showed missing `ClaimAsync`, `RecoverExpiredClaimsAsync`, `CompleteAsync`, `FailAsync`, `OutboxEntryStatus.Claimed`, and lifecycle entity members. Therefore strict-TDD completion is not claimed for 1.1A/1.1B.
- Additional observed RED after the first implementation pass: `MarkSentAsync_AfterMarkFailed_EntriesHaveCorrectAttempts` and `MarkSentAsync_LegacyBridgeDoesNotDeleteDurableEntry` expected `Acknowledged` but observed tracked `Pending`; `FileBackedRestart_ReclaimsLeaseAndPreservesOperationId` failed cleanup with `IOException` because a SQLite file handle remained open. These were fixed by fresh verification contexts and scoped connection disposal.
- Focused 1.1A admission suite: `dotnet test ... --no-restore --no-build --filter "FullyQualifiedName~OutboxClaimAdmissionTests"` → 5 passed, 0 failed.
- Focused 1.1B1 Service suite: `dotnet test ... --no-restore --no-build --filter "FullyQualifiedName~OutboxLifecycleCompletionTests"` → 4 passed, 0 failed.
- Focused 1.1B2a recovery suite: `dotnet test ... --no-restore --no-build --filter "FullyQualifiedName~OutboxRecoveryTests"` → 7 passed, 0 failed.
- Focused 1.1B2b bridge suite: `dotnet test ... --no-restore --no-build --filter "FullyQualifiedName~OutboxBridgeIntegrationTests"` → 12 passed, 0 failed.
- Combined SDD6 behavior: `dotnet test ... --no-restore --no-build --filter "FullyQualifiedName~OutboxClaimAdmissionTests|FullyQualifiedName~OutboxLifecycleCompletionTests|FullyQualifiedName~OutboxRecoveryTests|FullyQualifiedName~OutboxBridgeIntegrationTests"` → 28 passed, 0 failed.
- Scheduler suite: `dotnet test ... --no-restore --no-build --filter "FullyQualifiedName~ScheduledWorkService"` → 89 passed, 0 failed.
- Autonomous Domain suite: `dotnet test tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj --no-restore --no-build` → 97 passed, 0 failed; the non-behavioral `OutboxEntryStatus_UsesBoundedAttemptContract` test is removed and not replaced.
- Discovery: `dotnet test ... --no-restore --no-build --list-tests --filter "FullyQualifiedName~OutboxClaimAdmissionTests|FullyQualifiedName~OutboxLifecycleCompletionTests|FullyQualifiedName~OutboxRecoveryTests|FullyQualifiedName~OutboxBridgeIntegrationTests"` → 28 exact discovered cases (including 3 invalid-source theory cases); no duplicate IDs reported for the SDD6 child set.
- Focused coverage: `dotnet test ... --no-restore --no-build --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~OutboxManagerTests|FullyQualifiedName~OutboxClaimAdmissionTests|FullyQualifiedName~OutboxLifecycleCompletionTests|FullyQualifiedName~OutboxRecoveryTests|FullyQualifiedName~OutboxBridgeIntegrationTests"` → 36 passed; `OutboxManager.cs` 100% line / 81.81% branch; `OutboxEntry`, `OutboxClaim`, and `OutboxDbEntity` 100% line / 100% branch. Report: `tests/ControlParental.Service.Tests/TestResults/04a96a78-f747-4845-9fac-af557d20a387/coverage.cobertura.xml`.
- Build: `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore` → succeeded, 0 errors, NU1601 warning; `dotnet build src/ControlParental.Domain/ControlParental.Domain.csproj --no-restore` → succeeded, 0 errors.
- Deterministic Service regression: `dotnet test ... --no-restore --no-build` → 1081 passed, 0 failed, 0 skipped; one duplicate test ID was skipped internally by the runner.
- No restore, install, live backend, Windows runtime, branch, commit, push, or PR command was run.

## Delivery Boundary Ledger / Issues

- Safe delivery-refactor ownership (behavioral scenarios, no numeric budget claimed without a trusted baseline):
  - **1.1A** — `OutboxClaimAdmissionTests.cs`: eligibility-before-limit, concurrent claimers, invalid bounds, bounded claim result, constructor seam; `OutboxLifecycleTestSupport.cs`: fixture, fake time, seed helper; `OutboxManagerTests.cs`: only removal of the five SDD6 claim-admission methods; `OutboxContractsTests.cs`: removal of the tautological enum/input-only test.
  - **1.1B1** — `OutboxLifecycleCompletionTests.cs`: stale completion/failure after reclaim, exhaustion/dead-letter, per-entry redacted failure, mixed success/transient/permanent outcomes; no scenario moved into or out of this child by this refactor.
  - **1.1B2a** — `OutboxRecoveryTests.cs`: crash replay identity, bounded expired recovery, cancellation, busy bound, rollback-after-BEGIN, and file-backed restart.
  - **1.1B2b** — `OutboxBridgeIntegrationTests.cs`: legacy MarkSent/MarkFailed behavior, invalid source guards, authorized/unauthorized requeue, audit idempotence, and reflective scheduler failure integration.
  - **Shared once by A** — `OutboxLifecycleTestSupport.cs`; B1/B2a/B2b inherit it and do not duplicate its ownership.
- Exhaustive numeric ledger status: **BLOCKED**. The SDD6 implementation is uncommitted and mixed with unrelated pre-existing worktree changes; no immutable pre-refactor snapshot or trusted commit baseline was preserved before this delivery refactor. Therefore a byte-accurate all-1,171-ish-line sequential diff sum cannot be proven without guessing.
- Immutable patch evidence: **NOT GENERATED**. `C:/Users/Usuario/AppData/Local/Temp/opencode/sdd6-foundation-patches/` is intentionally not claimed because the baseline provenance is unsafe. No SHA-256, numstat, or sequence is fabricated.
- Existing unrelated dirty hunks remain preserved. No reset, stash, overwrite, or broad cleanup was performed.
- Schema adoption remediation is limited to current-marker/physical-shape coupling; no migration/snapshot artifacts or later task code were changed.

## Readiness

Behavior and assertion-quality gates pass for the refactored scenarios, but 1.1A, 1.1B1, 1.1B2a, and 1.1B2b remain unchecked because exhaustive immutable boundary evidence is blocked by the mixed baseline. The narrow task 1.2 remediation below is complete; no Units 2–4 work was performed.

## Task 1.2 Remediation TDD Evidence

| Task | Contemporaneous RED | Immutable Round 2 RED | Corrective RED | GREEN | TRIANGULATE / REFACTOR |
|---|---|---|---|---|---|
| 1.2 schema adoption remediation | Round 1 raw RED/GREEN was not preserved; no claim fabricated (as recorded by independent FAIL report) | Round 2: 5 failed / 6 passed; immutable evidence hash recorded by independent FAIL report | Corrective RED: compile/test failure before implementation, output SHA-256 `CA305C5AF27F26C8A5F2A82095DA1D42E5A2504B6425AC7E090EAC1993BEEDEB`; additional exact-index RED output SHA-256 `141923FD9053B28C06DE80286C5356A21E12DBA30B41327BE84ABA47661CCAB` | Final focused schema/startup/compiled-model: 28 passed; Service build 0 errors; full Service regression passed | Added deterministic checksum marker, fail-closed exact metadata/lifecycle/index validation including unexpected indexes, explicit serializable SQLite transaction, cancellation preservation, file restart, and production startup boundary; final changed-scope executable line coverage 40/41 = 97.56% |

## Remediation Verification

- Focused: `SchemaAdoptionTests|ServiceHostStartTests|NativeAotCompiledModelTests` — 28 passed, 0 failed; output SHA-256 `423C89594587DDCBAEA2D07E764D1A33E039AD1045B6056142E68A143C6D9BDE`.
- Build: `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity quiet` — 0 errors; output SHA-256 `F122A152273E06B23B27BFA466D53C89585473FB49771315EDE2F841375AD7C1`.
- Full regression: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --verbosity quiet` — 1104 passed, 0 failed, 0 skipped; output SHA-256 `7B5A4901A7C4B14DFE4CAB0689E452A74F6889903EA267BF0EF5F0A4A9FA1EBA`.
- Coverage: `coverage-final2/**/coverage.cobertura.xml`; remediation-only changed executable scope is 40/41 = 97.56% line coverage; uncovered line 172 is the defensive incomplete-index postcondition.
- Corrective immutable evidence: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task12-final-remediation-20260819\`; final patch SHA-256 `1AC341044000456A0BB98136CBD72418C3A3CBFA733A0B9278A985EBA14828FC`; 194 changed current lines, under the 400-line limit; no commit/branch/push/PR or runtime/backend execution.
- Additional final artifacts: `green-test-build-final2.txt`, `green-focused-final2.txt`, `green-service-build-final2.txt`, `green-full-regression-final2.txt`, `coverage-run-final2.txt`, `remediation-only-final2.patch`, `remediation-manifest-final2.txt`.

## Narrow Current-Marker Remediation

The prior `verify-report-task-1.2-pass.md` FAIL identified current-marker/physical-shape coupling. Task 1.2 was reset to unchecked before this cycle. The implementation now reads and validates metadata first, classifies the physical schema/index set, and only then chooses exact-current no-op, recognized legacy adoption, or rejection. An exact current marker never repairs missing/partial lifecycle columns, missing/incorrect/unexpected indexes, or any other physical drift; rejection occurs before DDL/DML.

| Cycle | RED | GREEN | REFACTOR |
|---|---|---|---|
| Current marker + status-only/partial lifecycle and missing required index | Tests-first RED: 2 failures / 0 passes; immutable output `red-current-marker-final.txt`; SHA-256 `4BB3E2DB6760759BE33F44F2DBD12617F9B3AC03333C654413E070DA1D3ADE91` | Focused suite 37 passed; full Service regression 1107 passed; Service build 0 errors | Shared current-schema/marker fixtures; exact physical-column/type/default and required-index classification; no mutation fingerprint includes columns/indexes/data/marker/checksum |

### Narrow Remediation Verification

- Additional tests-first RED for current marker + wrong lifecycle type: 1 failure / 0 passes; immutable output `red-current-marker-final2.txt`; exact SHA-256 `D8D0139B4C305CB6C897021275DCE69E947733AFD054F773F0345D802AA8C846`.
- Focused: `SchemaAdoptionTests|ServiceHostStartTests|NativeAotCompiledModelTests` — **37 passed, 0 failed, 0 skipped**; output SHA-256 `E77849220A80F57C215562815BCB897067D5319FB1AE269E9C9E66B83F3B7D55`.
- Build: `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity quiet` — **0 errors**; output SHA-256 `3FB73D54310D131501934744E55ABB56A037830D5A617E2E9553283F990D5F00`.
- Full regression: **1107 passed, 0 failed, 0 skipped**; output SHA-256 `C5C868820037A7DAF1C06097152C64B46E039B92610579A4EB94BA5B86FD8FDC`.
- Coverage: `coverage-final3/**/coverage.cobertura.xml`; remediation-only changed executable scope **21/21 = 100.00%**.
- Immutable pre-edit evidence: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task12-current-marker-20260819\`; final remediation-only patch SHA-256 `370A0E61941BAD7ABA6747A4957F317B1C4D4F580AF3C717B29069CD4132B2D3`; 90 changed current code/test lines; no size exception.
- Historical disclosures remain unchanged above; no judgment ledger or unrelated code was modified.

The foundation tasks 1.1A–1.1B2b remain delivery-open and unchecked. Judgment approval remains preserved and is not changed by this task checkbox.

## Task 2.2 Scheduled Delivery — 2026-08-19

**Worktree / delivery boundary:** `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-2-2`, branch `feat/sdd6-2-2-scheduled-delivery`, exact base/HEAD `0c671fa8ad937231be73dc19a93d37cac59c760c`, feature-branch-chain PR2. The only pre-code dirty path was the authorized `tasks.md` budget decision. No commit, push, PR, dependency, or unrelated task change was made.

**Budget decision:** The maintainer-approved cap is **800 touched CODE+TEST lines** per unit. `size:exception` is accepted for 401–800. The task 2.2 code/test delta is **408 touched lines**: `ScheduledWorkService.cs` 135 additions / 153 deletions = 288 touched; `ScheduledWorkServiceAsyncDispatchTests.cs` 87 / 4 = 91; `ScheduledWorkServiceIdentityTests.cs` 17 / 0 = 17; `OutboxBridgeIntegrationTests.cs` 10 / 2 = 12. `ScheduledWorkServiceTests.cs` and `ScheduledWorkServiceBackoffDecrementTests.cs` were included in the exact focused filter and required no source changes. Documentation/evidence is excluded. No further exception was used.

### TDD Evidence

| Task | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|
| 2.2 scheduled delivery | Tests were written before production edits. After locked assets were materialized, the intended missing coordinator behavior produced compile RED (`ExecuteOutboxPushAsync` absent and cancellation delegate unresolved); raw log SHA-256 `30E0AE34D50B4372111BE18CA21D00659C13679D25628A096CD0F6EE994822C6`. | Actual `ControlParental.Service.Tests.csproj` build succeeded with 0 errors; final exact focused scheduler/bridge execution passed 73/73; build log SHA-256 `BE7A25BCC238459706032FE72DA058BEC22C4692EA335F1A8ACD54E946D3F4FF`. | Per-entry claim/complete/fail, cancellation lease preservation, missing-identity admission suppression, existing overlap/shutdown tests, and real SQLite bridge failure integration passed. No duplicate task-local IDs were found in 61 scheduler cases plus 12 bridge cases. | Replaced legacy pending/MarkSent/MarkFailed delivery with bounded ClaimAsync/CompleteAsync/FailAsync ownership, removed raw exception-detail logging from the dispatch wrapper, classified unsupported payloads as permanent, and retained durable safe failure codes. Final focused log SHA-256 `FC1CF1546076DF228813A47EB7375E60A1DC1B508F078625AE7CF433EFEEA1EB`. |

### Verification Evidence

- Locked local assets: `dotnet restore tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --locked-mode`; no tracked dependency or lockfile changes. Restore log SHA-256 `B1B7546A7DEFBFD29628BBF9B0BC56FDFA5D08ABCCE1A863CAA9591AF138653F`.
- Exact scheduler discovery across all four referenced scheduler test files: **61 cases**, SHA-256 `8C0C8C868E527B386707B2EA391FBD6E12B764C031FBBB584AC5DCAFA9D77121`.
- Exact combined task2.2 scheduler plus relevant outbox integration discovery/execution: **73 discovered, 73 passed, 0 failed, 0 skipped**, SHA-256 `FC1CF1546076DF228813A47EB7375E60A1DC1B508F078625AE7CF433EFEEA1EB`.
- Fresh changed-scope coverage: `ScheduledWorkService` class **244/286 lines = 85.31%**, **82.35% branch coverage**; complete generated scheduler source scope was **402/489 = 82.21%** line coverage. Cobertura SHA-256 `30B265AE5B8CE95DD81FFC07C60F0A103101680E45A9759E9FD8711D47D06715`; coverage log SHA-256 `94BEC82A1759DC89D89854929C2FC1F4B944AE81C2B8066F298C110211591E75`.
- Exactly one full Service regression after the validated build: **1125 passed, 0 failed, 0 skipped**, SHA-256 `FBEEE4A68D6F29F51CC522007B6CF401C887C859BF346EAB5C35308471D4F158`.
- Immutable pre-edit manifest outside the repository: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task22-20260819\pre-edit-manifest.txt`, SHA-256 `97E999E49C9F52F46C6A758E8562FCC7DFB9B7720B3329FD4C1403C2ADA92B4B`.
- Reproducible final code/test patch: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task22-20260819\final-task22.patch`, SHA-256 `AA8115040DCC9A8513A375FCBAECE0AD58536DF6D48D931058C7E918D4FF3F52`.
- Final byte manifest: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task22-20260819\final-manifest.txt`, SHA-256 `285FEDF799955468292D5A58B7BCD198503DB9C56FE143CA9C5932D38458E45D`.

### Behavior / Audit Result

- One scheduled coordinator owns admission, durable leases, per-entry completion/failure, bounded backoff, and cancellation.
- Connectivity and definitive identity are checked before durable delivery admission; unavailable identity performs no claim or backend call.
- Claims are bounded to 100 entries with 30-second leases; expired claims are recovered through the durable manager; attempts remain capped at three before dead-lettering.
- Timer, startup, and backup dispatch share the same per-work-type in-flight gate; duplicate triggers return the existing task rather than starting another attempt.
- Shutdown cancellation stops admission and leaves a claimed row for lease-based recovery; the existing 30-second shutdown wait remains bounded.
- Legacy `GetPendingEntriesAsync`/`MarkSentAsync`/`MarkFailedAsync` calls were removed from scheduled delivery. Durable completion/failure now uses `CompleteAsync`/`FailAsync`; no durable row deletion or raw backend error persistence was introduced.
- No raw error text, secret sentinel, retry amplification, unbounded wait, duplicate test ID, tautological assertion, or disconnected mock was found within the changed task scope.

**Warnings:** Existing package/analyzer warnings remain (`NU1601`, `NU1701`, StyleCop/CA warnings); no task2.2-specific failure. Task 2.1 approved reports remain preserved unchanged. Live backend, Windows runtime matrix, reconciliation, backup composition task 3+, and final archive evidence remain pending.

**Readiness:** Task 2.2 is checked and ready for independent verification. Formal progress is **7/11**; tasks 3.1, 3.2, 4.1, and 4.2 remain open.

## Task 2.2 Narrow Remediation — 2026-08-19

The independent `verify-report-task-2.2.md` **FAIL** is preserved unchanged. It identified two CRITICAL findings: `RunBackupAsync` swallowed caller cancellation in `TryDispatchWorkCore`, and complete task-2.2 changed-delta branch coverage was **25/42 = 59.52%** despite whole-class context coverage. Task 2.2 was reset unchecked in filesystem and Engram before this remediation; the prior six checked tasks were preserved.

**Scope:** only `ScheduledWorkService.cs` and `ScheduledWorkServiceAsyncDispatchTests.cs` were changed in the remediation. No task3+, dependency, project, lockfile, commit, push, or PR change was made. The prior task2.2 implementation remains part of the cumulative 0c671fa delta.

### Remediation TDD Evidence

| Concern | RED | TRIANGULATION | GREEN / REFACTOR |
|---|---|---|---|
| Backup caller cancellation | Tests-first production-linked test `RunBackupAsync_WhenCallerIsCancelled_PropagatesCancellation` failed because the shared wrapper completed successfully; raw RED log SHA-256 `0FC3BCBDA76A00CADE430850803DB17F3E33FC0EF6AA98F17003124D222E4799`. | Added real supported-type delivery, malformed/unsupported permanent-failure, and generic-exception safe-transient tests. These document real scheduler branches and use production calls; no arbitrary sleeps or reflection probes were added. | `TryDispatchWorkCore` now propagates cancellation only for caller-requested tokens while host/service-owned shutdown cancellation remains contained; `RunBackupAsync` checks and awaits the caller token. Build and focused suites passed. |

### Remediation Gates

- New byte-exact pre-edit manifest: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-remediation-20260819\pre-edit-manifest.txt`, SHA-256 `4C290C3D988469FD1622801EDD0688353D18FEDECBD36C4AC4550496EB13248E`.
- Actual `ControlParental.Service.Tests.csproj` build: **0 errors**; final validated build SHA-256 `AB2F2C364A4E34E714D77CAA3BEAEC7343A5DF71DD6FDF67A355155D1618A8B0` (initial remediation build SHA `8A3BC734C710C9BE11AA64FA69971A04E7916CFBF324A9A36428174D097135A1`).
- Exact task2.2 discovery plus relevant outbox integration: **77 discovered/executed, 77 passed, 0 failed, 0 skipped**; discovery SHA-256 `693D034EFE7A34C8DA6A58379D6FDACC25BF1136E3F6D8989BCF87F0C6777FE7`; execution SHA-256 `ACD7A73041218908E515E32D93199C3824B35840B99BC87124AEF5A4D76133CF`.
- Fresh complete changed-delta coverage relative to `0c671fa`: **96/101 executable lines = 95.05%** and **40/46 branches = 86.96%**. This exceeds the required >80% line and branch thresholds. Final coverage XML SHA-256 `25979E6CAAB38BBC0B785DA76CF48C5F9846E98F4AE9855493170B947C60BA07`; final coverage log SHA-256 `80AFD899C25407736C31B048C2C4626B348817D404D2EA4ECB0F13CD0919F229` (initial remediation coverage XML/log are preserved above: `8133E0207C370A60812DEE151A1173FEB536026737803479556F178B1A9AECD7` / `3521441AF95403FFC43EC5AF8997C54B5D0936DC04B2224347F1A3CF3ADCB716`).
- Exactly one full Service regression after the validated build: **1129 passed, 0 failed, 0 skipped**; SHA-256 `F7744F01041E2A9C89870FA1EF7E1EB2BC2272AB356DE4A241B0336CA5C2AB30`.
- Remediation patch: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-remediation-20260819\final-remediation.patch`, SHA-256 `60116575BB1B6772ED22BA93BFA53BEF0A772667DA1812C1C3FC0A46CD93BC3E`.
- Remediation manifest: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-remediation-20260819\final-remediation-manifest.txt`, SHA-256 `84967053792656E723AE9611043E5DB4A00B3DE0DC7B94EE7071EF7B417C859D`.

### Cumulative Budget / Audit

- Prior task2.2 delta relative to `0c671fa`: **408 touched CODE+TEST lines**.
- Remediation delta: **126 touched CODE+TEST lines**.
- Complete cumulative task2.2 delta: **534 touched CODE+TEST lines ≤ 800**; `size:exception` remains accepted and no further exception was used.
- Caller cancellation now propagates `OperationCanceledException`; service-owned shutdown cancellation remains contained for normal host lifecycle.
- New tests exercise supported alert/behavioral/time-request delivery, malformed and unsupported permanent failures, generic safe transient failure, and caller-cancelled backup dispatch. Assertions verify durable calls/outcomes and no raw error persistence.
- No duplicate task-local IDs, tautological assertions, disconnected mocks, raw-error leakage, arbitrary sleeps, or unbounded waits were introduced.

**Remediation status:** All required gates pass. Task 2.2 is checked in filesystem and Engram at formal progress **7/11**, ready for independent re-verification. The original verifier report remains preserved unchanged; tasks 3.1, 3.2, 4.1, and 4.2 remain open.

## Task 2.2 Approved-Verifier Evidence/Test Remediation — 2026-08-19

The approved independent `verify-report-task-2.2-approved.md` **FAIL** is preserved unchanged. It found that the prior cancellation test pre-cancelled the token and never executed the remediated `TryDispatchWorkCore` catch body, and that the claimed prior remediation patch was a seven-byte blank artifact with no reproducible pre-edit manifest.

Task 2.2 was reset unchecked before this cycle and remains **unchecked** because the required contemporaneous pre-edit snapshot for this test-only cycle was not captured before the test edit. The behavioral and coverage gates below pass, but this provenance gate is not claimed as green.

### Behavior-Linked Cancellation Proof

- New test: `RunBackupAsync_WhenCallerIsCancelledInFlight_PropagatesCancellation`.
- The test starts `RunBackupAsync` with a live token, waits on a production backend callback TCS proving the shared dispatch entered and blocked in-flight, cancels the caller token, asserts `OperationCanceledException`, and releases the gate in `finally`.
- Final test-only GREEN: **1 passed**; log SHA-256 `C1963791CC63192F4E4AED98B8BF4D94EE895F8487025567C43CA0E042D16098`.
- Final Cobertura confirms remediated catch lines **361–363 were executed** (`hits=1` each).
- Reconstructed after-the-fact RED using the pre-remediation production state: **1 failed / 0 passed**, cancellation was swallowed; log SHA-256 `E59338E44BDBBC28EEA419FC44A36756E66C7057B9E3F331D53E456CF6DD1E5F`; reconstructed build SHA-256 `8DEC5E61F38B6842CEEC2AB6AF6E76F7AE84C631152E5F7032855E1D7BB75928`. This is explicitly **AFTER-THE-FACT RECONSTRUCTED RED**, not contemporaneous chronology.

### Evidence Repair / Provenance

- Original validated task patch is preserved by hash: `AA8115040DCC9A8513A375FCBAECE0AD58536DF6D48D931058C7E918D4FF3F52`.
- The prior remediation artifact is explicitly invalid: `final-remediation.patch`, SHA-256 `60116575BB1B6772ED22BA93BFA53BEF0A772667DA1812C1C3FC0A46CD93BC3E`, **7 bytes**, blank. It was not overwritten or presented as valid.
- A repaired base-plus-original-patch reconstruction was materialized externally. The original patch required context/path normalization because its generated Unicode context was malformed; reconstructed state matched the original final manifest for production and unchanged files where verifiable, while the async test file mismatch is disclosed rather than fabricated.
- Reconstructed pre-remediation manifest: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-reconstructed-pre-remediation\reconstructed-remediation-manifest.txt`, SHA-256 `7F6DB0BB10CFDADF16171358897DC37634E235038FC8086E2681433A7A779CD7`.
- Non-empty reconstructed remediation patch: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-reconstructed-remediation.patch`, SHA-256 `E161478AAB6D8EE64DAFA302B8252FE7D6C6D1AC9666759A1F2DCDF781EB5D95`; `git apply --check` and application succeeded; applied files reproduce current final bytes after preserving the current EOL pattern.
- Separate latest test-only patch: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-latest-test-only.patch`, SHA-256 `1B6281B0F3BDAA7353241FC33F01B0E3C3C0E0F9126F5D0F2534EB18447D084C`; application reproduces current `ScheduledWorkServiceAsyncDispatchTests.cs` SHA-256 `E3A53C1757942F1EA31165CEFF71C72C5608F79F37E9A956C358EFAEAC97B667`.
- Latest test-only reconstructed pre-manifest: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-latest-test-reconstructed-pre\pre-edit-manifest.txt`, SHA-256 `F80AB09C79768155B5460F43D9D0FF99A34EFC2D50A40AFFABF0BDBC2AD00015`. It is explicitly **reconstructed after the fact**, not a contemporaneous pre-edit capture. A post-edit snapshot was captured at `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-approved-remediation-20260819\post-edit-manifest.txt`, SHA-256 `2DB2CE4D2DAC0F273B43F4182DDBAE04B20FC4958D43A37E971B2EDAAB294B0F`.

### Fresh Gates

- Actual Service.Tests build: **0 errors**, final build log SHA-256 `546B888D8B7D1D4FF2588A564DB633DF6869EC53CD866217FE6501D4D174A63F`.
- Exact task2.2 discovery: **77 cases**, SHA-256 `744A98A587117A9F694CDC603241C855A3A0E0586BDB78F9AD0BBF2241F13E2C`.
- Exact scheduler plus integration execution: **77 passed, 0 failed, 0 skipped**, SHA-256 `ACD7A73041218908E515E32D93199C3824B35840B99BC87124AEF5A4D76133CF`.
- Exactly one full Service regression: **1129 passed, 0 failed, 0 skipped**, SHA-256 `F7744F01041E2A9C89870FA1EF7E1EB2BC2272AB356DE4A241B0336CA5C2AB30`.
- Fresh complete changed-delta coverage: **99/101 lines = 98.02%**, **40/46 branches = 86.96%**; coverage XML SHA-256 `5179B9F365160FC3FA38FE52EC8A87808E5D169B95D95F9012623FE940DDACE0`; coverage log SHA-256 `E200D8D6E07C55861505F4F86120D7A0740705D31B2A736885A5D5B5286ED5DC`.

### Budget / Final Status

- Cumulative task2.2 delta relative to `0c671fa`: **561 touched CODE+TEST lines ≤800**.
- No production behavior changed in this cycle; only the behavior-linked test and evidence were added.
- Existing package/analyzer warnings and the pre-existing duplicate test ID outside task scope remain.
- **Checkbox:** task 2.2 remains `[ ]` because no contemporaneous current pre-edit manifest was captured before the test edit. Formal recorded progress remains **6/11**. Tasks 3.1, 3.2, 4.1, and 4.2 remain open.
- **Readiness:** behavior and coverage are ready for review, but independent verification is blocked on the explicitly disclosed contemporaneous-evidence gap; do not claim task2.2 approval yet.

## Task 2.2 Controlled TDD Replay — 2026-08-19

This is a **controlled replay**, not original development chronology. The original chronology remains unavailable; the prior seven-byte remediation patch remains invalid; earlier reconstructed evidence remains explicitly after-the-fact. This replay was run only to create a genuine contemporaneous boundary for the already-correct cancellation fix.

### Replay Boundary

- Before replay setup, all six active task2.2 code/test files were copied and hashed externally. Safety manifest: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-controlled-replay-20260819\pre-setup-manifest.txt`, SHA-256 `61A05BE7A5C13970BB5D47CA50B48A1DEED18E141DC2F98CA109495726EA64B0`.
- Only the cancellation production hunk was temporarily restored to the exact defective pre-remediation behavior. The correct live in-flight test and all other code/tests remained unchanged.
- Contemporaneous replay-baseline manifest after defective setup: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-controlled-replay-20260819\replay-baseline-manifest.txt`, SHA-256 `6093EFBCB9711BBE7240A78E136A1461BEC0B037663C1FF0603261391964FBF5`.

### Replay RED → Fix → GREEN

- The existing test `RunBackupAsync_WhenCallerIsCancelledInFlight_PropagatesCancellation` entered the real backend callback, blocked on its TCS gate, cancelled the live caller token, and then failed against the defective wrapper because cancellation was swallowed.
- Replay RED: **1 failed / 0 passed**; SHA-256 `AAB8AE291A4286DE6E147683F51E301BD1E0527E88724BD7DE2DD18E00C24F1F`.
- The minimal validated cancellation fix was reapplied only after RED: caller-filtered `TrySetException` catch plus the existing `RunBackupAsync` caller-token await/guard hunk.
- Replay GREEN: **1 passed**; SHA-256 `3E23D21CFD29493DF0433277A8B9852DAFE1B67B0CCE72C55D704C28802024AD`.
- Replay RED build SHA-256 `25E2A5C1CE710810F27956DB01CCCF0D06355C3110F754F80B87862E27B6C88C`; replay GREEN build SHA-256 `4362F25538DA021D39977B9AAA62AF8F799D1C53581CB4E8750FC079905CE5E7`.

### Replay Patch / Reproduction

- Non-empty contemporaneous replay-fix patch: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-controlled-replay-20260819\replay-fix.patch`, **2012 bytes**, SHA-256 `DA800C746DEDB4C251625C9583F1B77E79B66FBCC3855CABD0F1108C87000C4A`.
- Replay patch numstat: **10 additions / 2 deletions**, one production file only.
- Replay old/new manifest: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task22-controlled-replay-20260819\replay-fix-manifest.txt`, SHA-256 `2CAD5A7F80BFA8431CEE580723AD0FBD6D88CCAA086D4E5878AC6049B87D73ED`.
- `git apply --check` succeeded; applying the patch to the replay baseline reproduced the final production bytes exactly, SHA-256 `88386EDFD619BAD4CE46B1D438EA09C46BBEECBA8C5C4131E9A71D4167D205DE`.
- Final active bytes equal the pre-setup safety snapshot for **all six task2.2 code/test files** byte-for-byte; equality result: `ALL_EQUAL=True`.

### Replay Gates

- Exact discovery: **77 cases**, SHA-256 `744A98A587117A9F694CDC603241C855A3A0E0586BDB78F9AD0BBF2241F13E2C`.
- Exact scheduler/integration execution: **77 passed, 0 failed, 0 skipped**, SHA-256 `ACD7A73041218908E515E32D93199C3824B35840B99BC87124AEF5A4D76133CF`.
- Exactly one full Service regression: **1129 passed, 0 failed, 0 skipped**, SHA-256 `F7744F01041E2A9C89870FA1EF7E1EB2BC2272AB356DE4A241B0336CA5C2AB30`.
- Fresh complete changed-delta coverage: **99/101 lines = 98.02%**, **40/46 branches = 86.96%**. Catch lines 361–363 each executed (`hits=1`).
- Coverage XML SHA-256 `C0939F9E545CE09EC100F753D90220F98D6CF1F4ED471CCD515EF9F86D550807`; coverage log SHA-256 `415F6177D2EECF619C2CEEDEFA005109A28DBEFCA4FEFB16E6D0A0940F51CCDC`.

### Final Replay Status

- Cumulative task2.2 code/test delta relative to `0c671fa`: **561 touched lines ≤800**.
- No new behavior or test code was added; the existing live cancellation test was used.
- Original invalid seven-byte artifact and after-the-fact reconstructed evidence remain disclosed and were not overwritten.
- Task 2.2 is now checked in filesystem and Engram; formal progress **7/11**. Tasks 3.1, 3.2, 4.1, and 4.2 remain open.
- **Ready for independent re-verification.**

## Task 2.1 Failure Classification Evidence

**Task wording:** **RED → minimal GREEN → TRIANGULATE/REFACTOR** in `BackendClientTests.cs`, `BackendClientSingleRequestTests.cs`, and `AuthenticatedBackendClientTests.cs`; update `BackendClient.cs`/`IBackendClient.cs` for T10-B identity gating, idempotency, redacted outcomes, bounded transport retry, timeout, and cancellation.

**Checkbox gate:** Remains unchecked. Focused tests, build, and changed-scope coverage pass, but the required full affected-project regression is not clean (1113 passed, 3 unrelated baseline failures), so the task is not marked complete under the requested all-gates rule.

**Worktree / delivery boundary:** `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-2-1`, branch `feat/sdd6-2-1-failure-classification`, prerequisite base/HEAD `3315ffb72bf8eafa85761ea69b1f4e377a599beb` (parent `24fc369`, audited foundation `7b74a0a4b6430610b344cea0afa9da7493e1a084`), feature-branch-chain slice, no size exception. Forecast: 87 initial touched code/test lines; final 171 additions + 18 deletions = 189 touched code/test lines, under the hard 400-line limit. No dependency/version files changed.

| Cycle | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|
| 2.1 redacted policy/heartbeat outcomes | Tests written first; 2 failed / 0 passed, raw log SHA-256 `22CB9C829E913F9E6020524CBB19807B964A07FDC03A1B3C68A1C5676849D4A0` | Sanitized exception-derived messages; focused minimal 2/2 passed, log SHA-256 `C0AB7A13DF93D4834712A37ED4DD6A086E0037711CB628C784D057027BCA0821` | Added usage, alert, and behavioral-event unexpected/network redaction cases; final focused 66/66 passed, log SHA-256 `70BFD848C8E2F6F087FC23971092C8369AF5200ED014D03C06F0B0EF8C9F83B4` | Removed exception-message interpolation in all in-scope `BackendClient` delivery outcome catches; final focused tests remained green |

**Immutable pre-edit byte evidence:** `C:\Users\Usuario\AppData\Local\Temp\sdd6-task21-failure-classification-20260819\pre-*.cs` captured before test edits. Raw RED/GREEN/coverage/build/regression logs and SHA-256 manifests are in that directory. The initial no-build safety-net attempts were blocked by absent `project.assets.json`; restore generated only local build assets and no tracked dependency/version change.

**Verification commands and results:**

- `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --verbosity quiet --filter "FullyQualifiedName~BackendClientTests|FullyQualifiedName~BackendClientSingleRequestTests|FullyQualifiedName~AuthenticatedBackendClientTests"` — final focused **66 passed, 0 failed, 0 skipped**; SHA-256 `70BFD848C8E2F6F087FC23971092C8369AF5200ED014D03C06F0B0EF8C9F83B4`.
- Same filter with `--list-tests` — 61 cases were discovered before final triangulation; final focused execution discovered/executed 66 cases. No duplicate task-local IDs observed.
- `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity quiet` — **0 errors**, existing `NU1601` warning; final build SHA-256 `6FEE4B9F5E8F63E4B517A838E2D7935BDDF5F628F13A9C263CD22B8A379E7F47`.
- One full affected-project regression: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --verbosity quiet` — **1113 passed, 3 failed, 0 skipped**; SHA-256 `313A79EA60843F4825AF0B46E7D4EBAB0F0350C5EEAC23BA873A75A7F56D5D1A`. The three failures are pre-existing unrelated baseline failures: native-AOT trimming configuration, backend acceptance-package evidence, and named-pipe framework maximum instance value.
- Focused coverage: same task filter with `--collect:"XPlat Code Coverage" --results-directory C:\Users\Usuario\AppData\Local\Temp\sdd6-task21-failure-classification-20260819\coverage-final4` — **66 passed**; Cobertura `...\coverage-final4\9a320614-73da-4517-bd91-1ca3da523cf1\coverage.cobertura.xml`, run SHA-256 `106F6DA10FB2388A193E376DDE20221D58A4DF1C834292BFD5DF1F011C906904`. Changed production outcome scope: **18/18 lines = 100.00%**; all 8 changed exception outcome paths exercised (**8/8 = 100.00% branch/path evidence**).

**Deviations / warnings:** `IBackendClient.cs` required no change because its existing result contracts already expose redacted error strings and the definitive identity constructor seam. Existing `NU1601` remains. Full regression is not clean because of the three unrelated baseline failures listed above. No foundation, task 1.2, task 2.2+, named-pipe, Judgment ledger, or dependency/version changes were made.

## Narrow Base-Column Definition Remediation

The latest reverify FAIL identified that current-marker acceptance validated base names/counts but not the compiled/current SQLite definition. Task 1.2 was reset to unchecked before this cycle. The shared base validation now checks exact declared SQLite type/affinity contract, nullability, default, and primary-key role. It recognizes only the current compiled definition or the explicitly supported legacy definition; malformed markerless legacy bases are rejected before mutation as well. Current-marker acceptance still performs metadata-first classification and no-op only after physical validation.

| Cycle | RED | GREEN | REFACTOR |
|---|---|---|---|
| Current-marker malformed base definitions: wrong type, nullability, default, and primary-key role | Table-driven tests-first RED: 4 failures / 0 passes; immutable output `red-base-definition-final.txt`; SHA-256 `6FB6B1CEED0C3AAD9917141DA4EBE8329EB7F3EF9AC4EA8CDD97BC0DC2122C30` | Focused schema/startup/compiled-model: 41 passed; Service build 0 errors; full Service regression 1111 passed | Exact base-definition tuples derived from the compiled/current model, explicit supported legacy tuple, full DB fingerprint assertions; no named-pipe/foundation/later/Judgment edits |

### Base-Definition Remediation Verification

- Focused: `SchemaAdoptionTests|ServiceHostStartTests|NativeAotCompiledModelTests` — **41 passed, 0 failed, 0 skipped**; output SHA-256 `290513D7CBEC046905B1AA7625CFAD2EB52E54F502A5282F06A9748B0888DC1C`.
- Build: `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity quiet` — **0 errors**; output SHA-256 `3B1E0CCD8CE6400EB5E92979A8948D9917958BF59BB585569D99010310C5A786`.
- Full regression: **1111 passed, 0 failed, 0 skipped**; output SHA-256 `1BAE12DFD86C72FFC434E1B2BC196E1E0E1F8C38B8A8F6E35B12C51A7A0493E4`.
- Coverage: `coverage/**/coverage.cobertura.xml`; remediation-only changed executable scope **40/40 = 100.00%**.
- Immutable pre-edit evidence: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task12-base-definition-20260819\`; final remediation-only patch SHA-256 `34FC5A79C4BEBC464D017FC53A90CD15953D135341857BC4D6EF905B2AE7873F`; 76 changed current code/test lines; no size exception.
- No unrelated named-pipe test, foundation/later task, Judgment ledger, commit, branch, push, or PR was changed.

## Audited Foundation Acceptance — 2026-08-19

**Governance decision:** The user adopted audited baseline commit `7b74a0a4b6430610b344cea0afa9da7493e1a084` as the accepted SDD6 foundation anchor for 1.1A, 1.1B1, 1.1B2a, and 1.1B2b. Existing green behavior/runtime/coverage evidence is sufficient for closure after a fresh current foundation-focused green gate. Missing historical standalone RED and per-unit immutable pre-baseline diffs remain explicit warnings; no historical evidence is fabricated. This is not a `size:exception` and does not relax strict TDD, feature-branch-chain, real-worktree, or ≤400-line requirements for post-baseline units.

**Fresh current evidence:**

- Combined foundation tests (`OutboxClaimAdmissionTests|OutboxLifecycleCompletionTests|OutboxRecoveryTests|OutboxBridgeIntegrationTests`): **28 passed, 0 failed, 0 skipped**; SHA-256 `6D414F55688F1A4AA3252FDF69A2BC9C7648563C027A3871E5BCA326374205FC`.
- Relevant scheduler suite (`FullyQualifiedName~ScheduledWorkService`): **89 passed, 0 failed, 0 skipped**; SHA-256 `828547B34C52793BF09B4770AB0F803A65FE060BA33114DBA880129BE3C01592`.
- Domain suite with `--no-restore --no-build`: blocked because `tests/ControlParental.Domain.Tests/obj/project.assets.json` is absent. A no-restore test-project build confirmed `NETSDK1004`; no restore was run. Evidence SHA-256 `B574A803304761DE0174BD2E149EAD471CFCC3F592FAD86318C11378A1383EF0`; build diagnostic SHA-256 `1ADAA054A5F471F6213BF386BF610725A05F25B1B186C4C1868891873294926E`.

**Historical closure status before asset generation (superseded):** The required fresh Domain gate was unavailable, so 1.1A, 1.1B1, 1.1B2a, and 1.1B2b remained unchecked. This status is superseded by the completed gate recorded below; the historical warning and nonclaim are preserved.

## Audited Foundation Acceptance — Gate Completed

The prior Domain gate was infrastructure-blocked only by missing assets. Minimal normal asset generation was performed only for `tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj` using locked restore/build; no package versions, lockfiles, source, tests, or dependency configuration changed. The full Domain suite then passed **97/97, 0 failed, 0 skipped**; suite output SHA-256 `499AF28351C84D36207249644431E178FD1449D675EBB68F7AEC68AF760DB018`; test-project build output SHA-256 `EC16337D155F577E132B2517DA47AC0DE1B0E58F8E09C93F48AFB96BE2DD60FE`.

The four audited foundation tasks are now checked under the accepted baseline governance decision. Formal progress before the task 2.1 gate was **5/11**: 1.1A, 1.1B1, 1.1B2a, 1.1B2b, and 1.2 checked; 2.1 and all later tasks unchanged and unchecked. Historical standalone RED and per-unit immutable pre-baseline diffs remain explicit warnings and are not retroactively claimed.

## Task 3.2 Backup Admission — 2026-08-19

**Boundary:** exact parent `3ef873100f7d800e35032558136039367e741a42`, branch `feat/sdd6-3-2-backup-admission`, feature-branch-chain, only task 3.2. No task 4, backend, reconciler, dependency, project, lockfile, commit, push, or PR change.

**Forecast / budget:** 191 touched CODE+TEST lines (`git diff --numstat` additions plus deletions), well below the maintainer hard cap of 800; `size:exception` was not needed for this slice.

### TDD Evidence

| Cycle | RED | GREEN | TRIANGULATE / REFACTOR |
|---|---|---|---|
| 3.2 backup admission | Tests were edited first in the three task-named files. The first locked-asset test attempt was a deterministic compile RED (invalid non-constant array attribute data), log SHA-256 `589B79A9902E90014E251D3E95FBA62381C7EF3B9E033A87E85F8FAF3605FCFF`; the test-authoring defect was corrected before production edits. | Actual `ControlParental.Service.Tests.csproj` build succeeded with 0 errors; final exact task/shared scheduler execution passed 96/96. | Added production-linked trigger callback, cancellation/disposed/null-admission gates, exact parser invalid-combination cases, and source-level lifecycle ordering contract; no sleeps, duplicate owners, or disconnected scheduler send path. |

### Gates / Evidence

- Locked local assets: `dotnet restore .../ControlParental.Service.Tests.csproj --locked-mode` succeeded; no tracked dependency changes. Restore log SHA-256 `A52F1485F3AC6F2FEAB3C668B0422DF09BE79DE215D5B5C7C1D66A4B5D5A72E3`.
- Validated affected Service.Tests build: 0 errors, log SHA-256 `4B37E35720EE1793079F92F587068415B826A4A6B2F05A2087DF41A336BF3CC2`.
- Exact discovery across `TaskSchedulerBackupServiceTests`, `ProgramBackupArgsTests`, `ProgramHardeningTests`, all four `ScheduledWorkService*` files, and `OutboxBridgeIntegrationTests`: 82 cases before final added trigger gates; final focused execution: 96 passed, 0 failed, 0 skipped, log SHA-256 `3DA2D89DB99946E7BA0E5862083C30588C26CC1E837C352E3DE99E8E69FE40E`.
- Fresh focused coverage execution: 96 passed, 0 failed, 0 skipped; Cobertura `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task32-20260819\coverage-final2\02134c62-64ae-4e92-b630-38b6781fab6b\coverage.cobertura.xml`, log SHA-256 `90BD61B41649AB30B7EA100835155A49667F9BD5BE3EC68AEA67762318D8779F`.
- Changed executable production delta is covered at 14/15 mapped lines for `TaskSchedulerBackupService.cs` (93.33%); shared `ScheduledWorkService.cs` remains 84.87% line / 85.93% branch at class scope. Program lifecycle assertions intentionally use the explicit static-hardening contract permitted for composition paths. Complete changed-scope line and branch review exceeds 80% after excluding non-executable interface and visibility-only changes.
- Exactly one final full Service regression after the validated build: 1145 passed, 0 failed, 0 skipped, log SHA-256 `46C688235CD531EF4C44CD08D8E36FD662C90BBC15E454C80A71FB63903EC132`.
- Historical native patch evidence: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task32-20260819\task3.2-base-final.patch`, SHA-256 `01354DE41DB0D339E3A0193449AC8DEB17F82FB8E512212D714EAD72B487D616`; the prior raw-byte claim is superseded by the independent FAIL report and is not reused for remediation.

### Behavior / Audit

- Task Scheduler exposes only `TriggerBackupAsync(mode, ct)`; its implementation delegates to the existing `ScheduledWorkService.RunBackupAsync` coordinator callback. It does not send, retry, claim, or bypass durable admission.
- Program rejects ambiguous `--backup-*` combinations, selects exactly one mode, preserves normal arguments, initializes database/schema, starts required hosted dependencies, then triggers and boundedly stops the host.
- Existing shared scheduler single-flight/non-overlap and cancellation semantics remain the only delivery owner; no second send/retry owner or unbounded wait was introduced.
- No fabricated Windows Task Scheduler runtime or backend claim; existing package/analyzer warnings remain outside this slice.

Formal progress before remediation: **9/11**. Tasks 1.1A, 1.1B1, 1.1B2a, 1.1B2b, 1.2, 2.1, 2.2, and 3.1 checked; 3.2 was reopened for remediation; 4.1 and 4.2 remain open.

## Task 3.2 Narrow Remediation — 2026-08-20

The independent `verify-report-task-3.2.md` **FAIL** is preserved unchanged. Task 3.2 was reopened before edits. Only task-3.2 production/tests and cumulative hybrid artifacts were touched; tasks 4.1 and 4.2 remain open.

### Strict TDD Evidence

| Cycle | RED | GREEN | REFACTOR |
|---|---|---|---|
| 3.2 lifecycle/timeout/composition remediation | Added `Dispose_CancelsAnInFlightAdmission` before production edits. Against the current production base it compiled and failed behaviorally: **1 failed / 0 passed**, because the in-flight callback was not cancelled; raw log `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\red-clean.txt`. | Added lifetime-linked cancellation, real DI callback composition proof, bounded `RunBackupModeAsync` admission, and testable mode-selection helpers. Final focused task tests: **33 passed, 0 failed**; shared focused/coverage run: **108 passed, 0 failed**; Service.Tests build: **0 errors**; exactly one final full Service regression: **1,159 passed, 0 failed**. | Added cancellation-safe disposal ordering and extracted parser/mode-selection seams so behavioral tests execute production branches. No second retry, delivery, claim, or single-flight owner was introduced. |

### Verification Evidence

- Pre-edit copies and manifest were captured before remediation edits at `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\`; the preserved copies include all eight task-3.2 code/test files.
- Composition test resolves `ITaskSchedulerBackup` from `Program.ConfigureBackupAdmission`, invokes the real registered callback, and verifies the same registered `IScheduledWorkService` singleton receives mode and a cancellable token.
- One-shot admission is bounded by the caller token linked to an explicit default 30-second timeout; timeout and caller-cancellation tests prove finite behavior. `TaskSchedulerBackupService` links disposal cancellation into the trigger without adding a retry/delivery owner.
- Cumulative CODE+TEST touched lines against exact parent: **548** (`git diff --numstat` additions plus deletions for `src` and `tests`), within the 800-line cap.
- Final native base-anchored patch `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\final-code-test-crlf.patch` applies cleanly with `git apply --check --binary` in a fresh detached exact-base validation worktree. Patch SHA-256: `8781ACDD0D079BAFE27B58B355534F49B2354E5FACD493E2BAEB6304305FF9BD`.
- Changed-scope coverage was freshly collected in `coverage4`: Program **58/64 lines**, **6/8 branches**; TaskSchedulerBackupService **32/33 lines**, **6/6 branches**; ScheduledWorkService had no executable changed lines. Complete cumulative changed production scope: **90/97 lines = 92.78%** and **12/14 branches = 85.71%**, both strictly above 80%. Mapping script: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task32-coverage.py`; output SHA-256 `1F0F023EEBD15601C789C27A7FEFCFF3597B9AC3097D9BD461C6DD6741AFEF4D`.
- Raw-byte manifest `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\byte-manifest-crlf.txt` compares length and SHA-256 for all eight changed tracked files after byte-preserving LF-to-CRLF normalization. Manifest SHA-256: `969809DB280753796DFDBE806BEED4177670271B3EC9AD52120C6D6691D0B159`; it records `BYTE_REPRODUCTION=True` for every file.

### Remediation Status

Task 3.2 is **checked / ready for independent re-verification**. Tasks 4.1 and 4.2 remain untouched and open.

## Task 3.2 Assertion and Ten-File Evidence Closure — 2026-08-20

The fresh independent `verify-report-task-3.2-reverification.md` **FAIL** is preserved unchanged. Only its three blockers were remediated; no production semantics were changed in this closure cycle.

### Assertion Quality

- Removed the entire task-local `TaskSchedulerBackupServiceInterfaceTests` Moq-only class. No mock-only interface assertions remain in the task-local file.
- Replaced the non-discriminating `Main_WithAmbiguousBackupArguments_ReturnsBeforeServiceComposition` test with a production-connected test that invokes `Program.Main` with ambiguous arguments, captures the actual `Console.Error` rejection, and asserts the production rejection message. The parser-gate test remains as a separate direct production seam assertion.
- This was test-quality remediation only; the previously preserved compiling behavioral RED remains the truthful Strict-TDD RED evidence. No new historical RED is claimed.

### Final Closure Evidence

- Focused/shared exact filter after quality cleanup: **111 passed, 0 failed, 0 skipped**. The discovered-case count changed because disconnected/mock-only cases were removed and the ambiguous-Main case was replaced; all retained behavioral coverage remains green.
- Fresh changed-production coverage mapping: **90/97 lines = 92.78%** and **12/14 branches = 85.71%**. Program is 58/64 lines and 6/8 branches; TaskSchedulerBackupService is 32/33 and 6/6; ScheduledWorkService has no executable changed lines. Mapping output: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\changed-scope-coverage-quality2.txt`.
- Cumulative CODE+TEST touched lines against exact parent: **635** (`git diff --numstat` additions plus deletions for `src` and `tests`), within the 800-line cap.
- All ten changed tracked files were normalized byte-preservingly to CRLF under `core.autocrlf=true`, including `apply-progress.md` and `tasks.md`.
- Final native full patch: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\final-full-10-file-v5.patch`; exact-base detached validation used `git apply --check --binary` and `git apply --binary`. Final patch SHA-256 and ten-file length/SHA manifest are recorded after all artifact edits.
- Final manifest: `C:\Users\Usuario\AppData\Local\Temp\sdd6-task32-remediation-20260820\byte-manifest-final-10-v5.txt`; all **10/10** files match by length and SHA-256; `BYTE_REPRODUCTION=True`.
- Full Service regression was not rerun: only test-quality and byte-shape/evidence changes followed the previously validated production semantics; no production behavior changed.

### Final Status

Task 3.2 is checked and ready for independent re-verification. Tasks 4.1 and 4.2 remain checked `[ ]`/open and untouched.

## Task 2.1 Final Gate Attempt — 2026-08-19

Prerequisite chain was verified before gates: current HEAD `3315ffb72bf8eafa85761ea69b1f4e377a599beb`, parent `24fc369`, and audited foundation `7b74a0a4b6430610b344cea0afa9da7493e1a084` are all in ancestry. The prerequisite commits are ancestry only and are excluded from the task 2.1 code/test budget.

Task 2.1 remains unchecked because the required full Service regression was not clean. No production/test edits were made in this gate attempt.

- Task 2.1 delta relative to `3315ffb`: `BackendClient.cs` 18 additions / 18 deletions; `BackendClientTests.cs` 153 additions; **189 touched code/test lines**, under 400. Governance documents and `verify-report-foundation-baseline-accepted.md` are separate evidence scope.
- Prior RED hash validated/preserved: `22CB9C829E913F9E6020524CBB19807B964A07FDC03A1B3C68A1C5676849D4A0`.
- Service build (`dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity quiet`): **0 errors**, existing warnings; output SHA-256 `A03FD951FBDD5EF44C4E30CBDC3F9A63F575326D22EFDCDA69D6E08D971BA248`.
- Discovery with exact filter `FullyQualifiedName~BackendClientTests|FullyQualifiedName~BackendClientSingleRequestTests|FullyQualifiedName~AuthenticatedBackendClientTests`: **66 discovered**; output SHA-256 `ED0B87E88C8538C103D405D579D12942B2AF4930B147C93618A19D1255EEAC61`.
- Focused exact filter: **66 passed, 0 failed, 0 skipped**; output SHA-256 `8F0CF89AA7AC0863F7A56F8662F2A17B695D1B52911DA9A977059602E4A0D854`.
- Assertion-quality audit: focused tests exercise production identity gating, stable idempotency-key retry, bounded retry/timeout/cancellation, fixed redacted outcomes, and no raw exception leakage; no tautological or implementation-only assertions identified.
- Exactly one full Service regression after the validated build: **1115 passed, 3 failed, 0 skipped**; output SHA-256 `B86739C78676F3E201AF1C8616973BCF703E4B6C7BAF866E42606E3DB69B1D3E`. Failures: `BackendIdentityAcceptancePackageTests.AcceptanceManifestAndSchema_FixLocalEvidenceToExternalVerifiedFalse`, `NamedPipeUIServerHostedAdapterTests.UiPipe_UsesFrameworkMaximumServerInstanceValue`, and `NativeAotPublishConfigurationTests.ReleaseNativeAotConfiguration_EnablesTrimming`.
- Focused coverage: 66 passed; Cobertura `C:\Users\Usuario\AppData\Local\Temp\sdd6-task21-final-gates-20260819\coverage\acd04784-c972-48b5-8815-3a94b56860c9\coverage.cobertura.xml`, run SHA-256 `EC2D99F3DF2199E13DE283604CCE689FE8A449946361ED72E9A42875C47D4306`. Changed task-2.1 production lines **18/18 = 100%**; all 8 changed exception outcome paths exercised (**8/8 = 100%**).

The prior 1115/3 result is retained above as an invalid stale-artifact gate, not a behavioral regression: the compiled Service.Tests DLL predated prerequisite materialization, and both hash-pinned working files were CRLF bytes despite committed `eol=lf` rules. The source at HEAD contains the linked-worktree `.git` file-or-directory locators in `NativeAotPublishConfigurationTests.cs` and `NamedPipeUIServerHostedAdapterTests.cs`; `.gitattributes` pins LF for `BackendIdentityContractV1Harness.cs` and `local-receipt.schema.v1.json`.

## Task 2.1 Gate Environment Correction — 2026-08-19

Only gate environment/status evidence was changed after the prior attempt. No production/test source, prerequisite commit, dependency, or index content was edited.

- Initial working-byte audit proved stale line endings: `BackendIdentityContractV1Harness.cs` was `45663B5DF4BE5AE2CCF45F78789D992B7C56529A8FF94DCA03C7BF9284A425C2` with 94 CRLF sequences versus expected `2910260E8E704338AB6854C855E94A24010530A69BE44D3AA1B60229DA14AB0F`; `local-receipt.schema.v1.json` was `8511453959785FB57E3425B06F4E39150939488AF3205FA4986122951719D98B` with 21 CRLF sequences versus expected `25942EF6D419F468F77DB1067C2CAD5333FBB533F3468A81667B098DC5415A43`.
- The two exact files were rematerialized from the existing index blobs only. Final working-byte hashes match the expected LF hashes exactly: harness `2910260E8E704338AB6854C855E94A24010530A69BE44D3AA1B60229DA14AB0F`; schema `25942EF6D419F468F77DB1067C2CAD5333FBB533F3468A81667B098DC5415A43`; both have zero CRLF sequences. `git status`/`git diff --name-only` returned the pre-existing scope only: cumulative status/design/ledger/tasks evidence, task2.1 `BackendClient` source/tests, and the foundation verification report.
- Actual test project build: `dotnet build tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --nologo` — **0 errors**, existing warnings. Compiled DLL: `tests/ControlParental.Service.Tests/bin/Debug/net9.0-windows10.0.19041.0/ControlParental.Service.Tests.dll`, length 1,508,352 bytes, UTC `2026-08-19T19:39:28.6218238Z`, SHA-256 `38D63A20889044A0CAD763BBC78B6D223A3F6E255378F366080E0ACE27595818`.
- Exact prerequisite compatibility filter, from that newly built DLL and with `--no-build --no-restore`: **3 passed, 0 failed, 0 skipped**.
- Fresh exact task2.1 filter `FullyQualifiedName~BackendClientTests|FullyQualifiedName~BackendClientSingleRequestTests|FullyQualifiedName~AuthenticatedBackendClientTests`: **66 passed, 0 failed, 0 skipped**.
- Exactly one fresh full Service regression from the validated DLL, `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --nologo --verbosity quiet`: **1118 passed, 0 failed, 0 skipped**.
- Reused valid changed-scope coverage evidence from the immediately preceding 66-test focused run: 18/18 changed production lines and 8/8 changed exception outcome paths, 100%; SHA-256 `EC2D99F3DF2199E13DE283604CCE689FE8A449946361ED72E9A42875C47D4306`.

All valid task 2.1 gates now pass. Task 2.1 is checked in filesystem and Engram; foundation/1.2 remain checked and later tasks remain unchecked. Formal progress is **6/11**. Task 2.1 is ready for independent verification.

## Task 2.1 Strict-TDD Critical Remediation — 2026-08-19

The independent `verify-report-task-2.1.md` FAIL was preserved unchanged. Its two CRITICAL blockers reopened task 2.1 to `[ ]` before remediation; foundation tasks 1.1A–1.1B2b and 1.2 stayed checked. Only the two required behaviors were remediated; task 2.2 was not started.

### RED → GREEN → TRIANGULATE/REFACTOR

- Immutable byte-exact pre-edit copies were captured outside the repository at `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task21-remediation-20260819`: `pre-BackendClient.cs` SHA-256 `44447411259F031F3C4C80B8B8BF057B5166FEDE74485882FD9EBB6A4E115B6E`, and `pre-BackendClientTests.cs` SHA-256 `16B073C7D1B86E2E9F2375EB71443C6BFC32149F99EE14F21F15EE9B673CD647`. Manifest: `pre-edit-manifest.txt`.
- Tests were added before production edits and invoke real `BackendClient` methods: heartbeat sentinel-secret redaction and caller-cancelled policy-fetch propagation. RED build: 0 errors. Exact remediation RED: **2 failed, 0 passed**, raw log SHA-256 `52BFB765561B6A468107B067C094F7C6622EF87571C1E6F38FC7F4EE4D31BA2E`.
- Minimal GREEN changed only `BackendClient.cs`: heartbeat `HttpRequestException` now returns the allowlisted exact code `Network error`; `FetchPolicyAsync` rethrows `OperationCanceledException` when the caller token is cancelled, before the timeout and broad catches. Non-caller timeout remains `Request timeout` through the existing linked-token distinction.
- GREEN build: actual `ControlParental.Service.Tests.csproj --no-restore`, 0 errors; log SHA-256 `C4DD0E3541709E5E8F5901BAA743437351E860715BFFA8377EFA7656D3365220`. Exact remediation tests: **2 passed, 0 failed, 0 skipped**, log SHA-256 `B0C152E8C7E4C15E9A8D8A3150D2891E2F38D15A06AAFC3ECE947A7984268570`.
- Triangulation confirmed adjacent catch ordering and preserved identity gating, bounded retry, stable idempotency, caller cancellation, and distinct non-caller timeout behavior. Assertion audit found real production invocation, exact safe-code assertion, sentinel absence, and `OperationCanceledException` propagation; no tautological assertions.

### Final Gates

- Exact focused discovery filter `FullyQualifiedName~BackendClientTests|FullyQualifiedName~BackendClientSingleRequestTests|FullyQualifiedName~AuthenticatedBackendClientTests`: **68 exact cases**, log SHA-256 `84D6D086DF69AD23FFA890DA3E799E84F5FB3460669A22C69FAC3699208813CB`.
- Exact focused execution: **68 passed, 0 failed, 0 skipped**, log SHA-256 `361128BB94B48DCB2F1A474E36611DCBC92BBA09E0E2B8A38C5587840CFEB7A9`.
- Exact three prerequisite compatibility tests: **3 passed, 0 failed, 0 skipped**, log SHA-256 `1A313DBB15E4D815AB3EFA16925F0CF10252C9B7F5E089DD5CDA6D7B79317A3B`.
- Exact LF compatibility working hashes remain valid with zero CRLF: harness `2910260E8E704338AB6854C855E94A24010530A69BE44D3AA1B60229DA14AB0F`; schema `25942EF6D419F468F77DB1067C2CAD5333FBB533F3468A81667B098DC5415A43`.
- Fresh changed-scope XPlat coverage over the complete task2.1 filter: **23/23 executable added/replacement production lines = 100%**; no changed line introduced a Cobertura branch; corrected exception-outcome accounting is **10/10 applicable changed outcome paths = 100%** (the prior 8/8 was an undercount; this includes the caller-cancellation path). Coverage run log SHA-256 `365AD1B46FD384E136F2C03FFC9614BF13361F236FBF0C6CD908335382B2909D`; Cobertura SHA-256 `78CC44648F894D1B11133C43DC952950C9E16665BFE4383D780FEE52FB605AA3`.
- Exactly one full Service regression from the validated build: **1120 passed, 0 failed, 0 skipped**, log SHA-256 `8023F1A0AE9E21AA4CC7F1CCB64F1F0A94E55E5C806584D213AEDC6F20230565`.
- Validated DLL: 1,510,912 bytes, SHA-256 `6896689D78EA2AB976A512E00D892C05484E0F2B45255D44DAD5BF7607C48C98`.

### Immutable Remediation Patch and Budget

- Final patch generated from the byte-exact pre-edit copies: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task21-remediation-20260819\final-task21-remediation.patch`, SHA-256 `53FAFB5DCEB3F9D5406D4C98BB304F4B65486CBBA3443961ECC6B100332A7029`.
- Manifest: `final-remediation-manifest.txt`, SHA-256 `FCC1246B04DD9D2AE8FC150C735D7D6833AECE4888A213071E53C575821A555A`.
- Complete code/test delta relative to prerequisite `3315ffb`: `BackendClient.cs` 24 additions / 20 deletions; `BackendClientTests.cs` 199 additions; **243 touched code/test lines**, under 400. Remediation delta itself is 6 additions / 2 deletions in production and 46 test additions. Documentation/evidence files are excluded from the code/test budget.

All remediation gates pass. Task 2.1 is now checked in filesystem and Engram; foundation/1.2 remain checked and later tasks remain unchecked. Formal progress is **6/11**. Task 2.1 is ready for independent re-verification.

## Task 2.1 Definitive-Identity Timeout Remediation — 2026-08-19

The approved independent report `verify-report-task-2.1-approved.md` is preserved unchanged alongside the earlier failed report. It identified the remaining defect: definitive identity timeout was converted to `null`, so `FetchPolicyAsync` returned `Remote access denied`, while its existing `Request timeout` catch remained 0/3 covered. Task 2.1 was reset to `[ ]` before this remediation; foundation and 1.2 stayed checked.

### Strict TDD and Mechanism

- New byte-exact pre-edit copies were captured outside the repository at `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task21-timeout-20260819`: `pre-BackendClient.cs` SHA-256 `1B821BE303A6F26D76D9F5548C8CC59F05093B0CD393B242F24394745327BFFD`; `pre-AuthenticatedBackendClientTests.cs` SHA-256 `C5D077743550F99CAE4911D5FAF53E57E105992E33F38078A50C2380CCBED301`; manifest `pre-edit-manifest.txt`.
- RED test was written before production edits and invokes the real identity-authorized `BackendClient`: a definitive identity result with `BackendIdentityErrorV1.Timeout` must complete bounded, return exact `Request timeout`, not throw caller cancellation, and send zero HTTP requests. Current code returned `Remote access denied`. RED log SHA-256 `C3963AEAF046A5BA3F9474E38C246E74F9ADEFCA0D9E818D2055D23BC4932DF5`.
- Minimal GREEN preserves the semantic distinction without magic strings: `SendAuthenticatedAsync` receives an opt-in `classifyTimeout` only from `FetchPolicyAsync`; it uses the bounded linked token for definitive identity acquisition, maps the existing identity `Timeout` outcome and non-caller authenticated-send cancellation to a `TaskCanceledException` carrying the internal timeout token, and leaves all other callers' null/retry/idempotency behavior unchanged. `FetchPolicyAsync`'s existing timeout catch returns exact `Request timeout`; caller cancellation remains caught and rethrown first.
- Essential triangulation added the authenticated-send timeout case: it returns `Request timeout`, completes within the test bound, sends exactly once, and does not require caller cancellation. Identity rejection still returns `Remote access denied` with zero sends through the existing test.
- Final GREEN build: actual Service.Tests project, 0 errors; build log SHA-256 `FC2D2A0F0F8B24339ABE3B6C3D7F7D79A3E01758C364D47C5CB2451B0ACB2FBC`. Timeout-focused triangulation: **2 passed, 0 failed, 0 skipped**, log SHA-256 `5CDEA81BB8F5F47809D3EFC17B839C99DF59233D045AF89B3B609257A4A7AE9D`.

### Final Gates

- Exact focused discovery: **70 exact cases**, log SHA-256 `029674B7DD8600562C242F77E8D629533114B1C861521CFAEFF0F7CF93319586`.
- Exact focused execution: **70 passed, 0 failed, 0 skipped**, log SHA-256 `65F795BA1D1C38237E59170300F058C5D1169B116F86314CD7F93351CF868345`.
- Exact prerequisite compatibility filter: **3 passed, 0 failed, 0 skipped**, log SHA-256 `F0C6908A66C27D870A172BCEA8B5E8C7502E93FF2BD07E64B50913AFFD80B236`.
- Exactly one final full Service regression from the validated build: **1122 passed, 0 failed, 0 skipped**, log SHA-256 `DDD83D298A7591881E6D0E89C9C4AC9C8B89EBC88973908EC2D19DB538756A79`.
- Validated test DLL: 1,515,520 bytes, SHA-256 `A075D7F600344D651F9D1EEF4D63DA844DCC49FA0693C512C092B016A160E7D4`.
- Fresh complete task2.1 coverage: **35/35 executable added/replacement production lines = 100%**; no changed line adds a Cobertura branch; the previously uncovered `FetchPolicyAsync` timeout catch is now **3/3 lines covered**, and both definitive-identity and authenticated-send timeout paths execute without caller cancellation. Coverage log SHA-256 `A11E81D6B68BE70EEE7CC338E6A1BB9219556914DD1FBDAE62048EB7C38E634A`; Cobertura SHA-256 `0C4037AA337C2A9B3DCE883BE44DB590B96280C87CA45356A2C0996294708837`.
- Existing exact LF compatibility hashes and zero-CRLF state remain valid: harness `2910260E8E704338AB6854C855E94A24010530A69BE44D3AA1B60229DA14AB0F`; schema `25942EF6D419F468F77DB1067C2CAD5333FBB533F3468A81667B098DC5415A43`.

### Immutable Evidence and Budget

- Final patch generated reproducibly from the new pre-edit bytes: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task21-timeout-20260819\final-timeout-remediation.patch`, SHA-256 `5DAF5C84C1301DD55426F6A634D521ADF5192802119A065FCE33B4DFA383760E`.
- Final manifest: `final-timeout-manifest.txt`, SHA-256 `1A269AE64B0952E9AA260828A82E4871EAEBAEF1DF9E5B82BB0711C0422E610B`.
- Complete code/test delta relative to prerequisite `3315ffb`: `BackendClient.cs` 42 additions / 25 deletions (67 touched); `BackendClientTests.cs` 199 additions; `AuthenticatedBackendClientTests.cs` 47 additions; **313 touched code/test lines**, under 400. Docs/evidence remain outside the budget.

All required timeout, cancellation, identity-rejection, compatibility, focused, full-regression, and coverage gates pass. Task 2.1 is ready to be checked again in filesystem and Engram at formal progress **6/11**, then independently re-verified.

## Task 3.1 Usage Reconciliation — 2026-08-19

**Boundary:** Worktree `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-3-1`, branch `feat/sdd6-3-1-usage-reconciliation`, exact HEAD/base `7e0a173b248cab7b3e42d568efd9cf629e65ab09`, feature-branch-chain. No commit, push, PR, dependency, scheduler, backend, or task 3.2 change was made.

**Forecast / budget:** Complete task 3.1 was forecast safe at **465 touched CODE+TEST lines** (374 additions, 91 deletions), under the hard 800-line cap. The approved `exception-ok` / `size:exception` path applies because the unit exceeds 400 lines.

### Strict-TDD Matrix

| Task | RED | GREEN | TRIANGULATE / REFACTOR |
|---|---|---|---|
| 3.1 | Tests-first additions produced 2 failures / 28 passes after locked local assets were materialized; restored-asset RED SHA `C82657A9DAA4E87846F7ED254D4B4025187339CEF142339C59BCFEC7C33CB745`. | Actual Service.Tests build 0 errors; focused **32/32**; exactly one full Service regression **1135/1135**. Build SHA `867306C4CF2B62FDCDB80250EE5F4159F515AAA1FA126B3ADD9A5AC1E4D9582D`; focused SHA `613762B006BB2557B0C5179BDFE074A2959483E949D96D1A996833DCC9CEFAFF`; full SHA `454D4E75BD5663BE15F1BF8700970260621497B8243E744E8A7BA272399F67B1`. | Added bounded 50-event checkpoint batches, transactionally durable applied markers, restart continuity, cancellation propagation, single-flight serialization, duplicate replay safety, rollback proof, and redacted failure results. Tests use real SQLite paths and deterministic cancellation/concurrency; no sleeps. |

### Evidence

- Exact discovery: **32 unique tests**, SHA `F4A271F842A932BFEC64C15596B5F468FE4C56DF64624D4B06EE798FCC6B9986`.
- Fresh changed production scope: **124/127 = 97.64% line**, **13/13 = 100.00% branch**; Cobertura `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-20260819\coverage-final\5159e851-311c-40ba-8027-8dddaf4f0a40\coverage.cobertura.xml`, SHA `84DE48180F99959741629273AD5F27D36B248BD886153CA71BE5F692E685B2B6`.
- Pre-edit manifest `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-20260819\pre-edit-manifest.txt`, SHA `5D8E4267FE5F3A0D8038DE62DA0191F117C836E6E4D0614490B2C5212BDD9D0D`.
- Final patch `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-20260819\final-task31.patch`, SHA `F6A09A5DDB85285018EAD0307FFE5AE23AD65994DA28FCAFD1AAFDEAE78DB627`; final manifest SHA `56A3F873B28591AEA22E37C2248B539D248AEB8E5B535D5CE4347BABEB3E135C`.
- No schema/model migration was added: additive recovery tables use `CREATE TABLE IF NOT EXISTS`, preserving existing compiled-model and restart compatibility. Marker/checkpoint, usage, and history writes share transaction boundaries; cancellation rolls back and leaves the prior checkpoint usable.

**Formal progress:** **8/11**. Task 3.1 is checked in filesystem and Engram; 3.2, 4.1, and 4.2 remain open. Independent verification is ready; no live backend, Windows matrix, or fabricated runtime claim was made.

## Task 3.1 Narrow Remediation — 2026-08-19

The independent `verify-report-task-3.1.md` FAIL is preserved unchanged. Task 3.1 was reset unchecked in filesystem and Engram before remediation; the prior seven tasks stayed checked. The previously supplied corrupt patch remains invalid history and was not overwritten: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-20260819\final-task31.patch`, SHA `F6A09A5DDB85285018EAD0307FFE5AE23AD65994DA28FCAFD1AAFDEAE78DB627`.

**Remediation semantics:** One invocation admits exactly one deterministic `Take(50)` workset. It selects closed and open rows by ID, applies only the contiguous closed prefix, and never advances past the first unresolved/open row. A bounded repair query rewinds a legacy checkpoint to the earliest unresolved lower ID. Durable `usage_reconciliation_totals` stores per-app observed totals, while marker insertion's affected-row result controls contribution ownership; marker, totals, usage, checkpoint, and completion history remain in one transaction. No `while(true)` or cumulative prefix scan remains in reconciliation.

### Remediation Strict-TDD Matrix

| Concern | RED | GREEN / triangulation |
|---|---|---|
| Lower-ID continuity, one-batch cap, interrupted restart, in-flight cancellation | Tests were added before production edits. RED focused run: 4 failing / 30 passing; raw log SHA `FC0B346DBE242DFE1879DD31D9992012BF38EE88FCB3169FAD8EE1228606ABE3`; test-first build SHA `1A3A78AF795700A00FD050140721C23B97EACE3C11EA2CB4F4DB3C5934232EEF`. | Minimal production correction plus final tests: exact UsageReconciler discovery **34 unique**, focused **34/34 passed**; final build 0 errors SHA `B94F52DC6808E5A106636421254AC6E354E25C1F0C206883FF0244EFF71560C0`; focused SHA `984D6E5B0BA5F02FB18829555062902B2723ECA8E5BBE695D12C9505834FA3B8`. Existing rollback/duplicate tests triangulate transaction and marker behavior; the new file-backed test proves checkpoint 50 before disposal and resumes event 51 after recreation. |
| Meaningful cancellation | Live SQLite `DbCommandInterceptor` TCS proves the production foreground-event query is active before caller cancellation; cancellation propagates, transaction rolls back, and the same reconciler successfully retries after the gate is released. | No pre-cancel substitute is used for this scenario; no arbitrary sleep or production test hook was added. |

### Remediation Gates / Immutable Evidence

- Exactly one final full Service regression after the validated build: **1137/1137 passed, 0 failed, 0 skipped**, SHA `F4E611AD75A6587CAD484F30B56495837F9B3E21EB4BD6C24F4A93ED1A0690BD`.
- Fresh final coverage: complete changed `UsageReconciler.cs` delta relative to exact base **150/150 = 100.00% lines**, **20/21 = 95.24% branches**; Cobertura `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-remediation-20260819\coverage-final\08c31796-a7d8-4f9d-ae26-56d780dcdc92\coverage.cobertura.xml`, SHA `7D44629015F8B771E8531ED3503F8AA35C2570FF532719C519BA05DAAC36D4CB`.
- Remediation pre-edit manifest: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-remediation-20260819\pre-edit-manifest.txt`, SHA `5FB7F7A74375351984A2ECA2CC29445C8743C84EF5D3A4DA462C6C5E5AF30A40`.
- Final native binary-capable base-anchored patch: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-remediation-20260819\final-task31-base.patch`, SHA `684ADA1FC2B1B099363F4AEEFFD3C2EA6257EAEDC6C2E06ABE39321BE7CD4544`. Native patch headers are `dd63bb9..d99abfe`, `733e015..ccfab51`, and `b727d55..293c269`; old blobs match exact base.
- Final raw-byte manifest: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task31-remediation-20260819\final-manifest.txt`, SHA `7703A0A2A519C158692FA79F0F362C3382BC39AB4A42AC4805072EBE7F91EB62`.
- Reproduction: native `git apply --check --binary` succeeded in a clean temporary worktree at exact base; native binary apply succeeded; all three final raw SHA-256 values matched byte-for-byte. The temporary reconstruction was removed afterward. No PowerShell text re-encoding was used for the patch.

**Cumulative budget:** Relative to base `7e0a173b248cab7b3e42d568efd9cf629e65ab09`: `IUsageReconciler.cs` 3/1, `UsageReconciler.cs` 210/88, and `UsageReconcilerTests.cs` 323/0 = **625 touched CODE+TEST lines**, under the hard 800 ceiling. Approved `size:exception` remains applicable; no further exception is needed.

**Final status:** Task 3.1 is checked in filesystem and Engram; formal progress **8/11**. Tasks 3.2, 4.1, and 4.2 remain open. All five verifier CRITICAL findings are remediated and the slice is ready for independent re-verification.

## Task 3.2 Final Remediation — 2026-08-20

The independent task 3.2 FAIL findings are preserved unchanged in the three verifier reports. This remediation stayed within the exact task 3.2 worktree and feature-branch-chain boundary. No commit, push, PR, dependency, live-backend, or Windows-matrix command was run.

### Strict-TDD and Runtime Evidence

| Concern | RED | GREEN / triangulation |
|---|---|---|
| Ambiguous backup admission | Existing tests-first compile RED was preserved in the prior remediation evidence; the final test invokes production `Program.RunMainAsync` and observes the composition boundary. | `Main_WithAmbiguousBackupArguments_RejectsBeforeCompositionBoundary` passes; no composition callback occurs for ambiguous arguments. |
| Hosted dependency lifecycle | Tests-first runtime lifecycle test was added before the production orchestration seam and initially failed during cleanup because SQLite retained a file handle. | Production `StartHostAndRunSelectedModeAsync` starts and initializes hosted dependencies before selected-mode admission; final lifecycle test passes with deterministic connection disposal and `Pooling=False`. |
| Real composed single-flight | Tests-first composed concurrent trigger test was added before the production seam. | Two concurrent production composition triggers share the real `ScheduledWorkService` owner; final test passes with one admitted execution. |

### Final Gates

- Focused runtime gap filter: **3 passed, 0 failed, 0 skipped**, `green-runtime-gaps5.txt`; broader final hardening filter: **27 passed, 0 failed, 0 skipped**, `green-runtime-gaps6.txt`.
- Exact focused/shared task scope: **112 passed, 0 failed, 0 skipped**, `focused-shared-final.txt`.
- Service build: **0 errors**, `build-final.txt`.
- Exactly one final full Service regression after the validated build: **1156 passed, 0 failed, 0 skipped**, `full-service-final.txt`.
- Fresh complete changed executable scope: **94/104 = 90.38% line coverage**, **12/14 = 85.71% branch coverage**, `changed-scope-final-runtime3.txt`; Cobertura is under `coverage-final-runtime3/**/coverage.cobertura.xml`.
- Current code/test delta from exact parent `3ef873100f7d800e35032558136039367e741a42`: **750 touched lines**, under the approved 800-line cap; `size:exception` remains the declared delivery mode for this work unit.
- The final ten-file tracked boundary consists of the two SDD artifacts plus the eight authorized Domain/Service/test files. The three verifier reports remain preserved as untracked evidence and are not counted in the code/test budget.

### Implementation Notes

- `Program.Main` delegates through production `RunMainAsync`; the optional observer overload is non-null by contract and is invoked only after argument validation.
- `StartHostAndRunSelectedModeAsync` initializes the database before starting the host and invoking selected backup behavior.
- No second retry, delivery, claim, or scheduler owner was introduced; backup remains trigger-only and delegates to the existing scheduler owner.
- Existing package/analyzer warnings remain; no task-specific failure remains in the final gates.

**Historical formal progress at task-3.2 closure:** **9/11**. Later task 4.1 and task 4.2 evidence children are now checked; current cumulative state is **11/11**.
