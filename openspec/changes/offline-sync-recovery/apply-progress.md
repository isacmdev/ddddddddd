# Apply Progress: Offline Sync Recovery

Change: `offline-sync-recovery`
Mode: Strict TDD
Artifact store: hybrid
Delivery: auto-chain / feature-branch-chain
Boundary: PR1A base `feature/tracker`; conditional durable lifecycle only.

## Cumulative State

- [x] 1.1A — delivery-boundary refactor accepted against the audited baseline anchor; behavior green, with historical provenance warnings retained.
- [x] 1.1B1 — completion/failure boundary accepted against the audited baseline anchor; behavior green, with historical provenance warnings retained.
- [x] 1.1B2a — recovery/restart/cancellation/busy/rollback boundary accepted against the audited baseline anchor; behavior green, with historical provenance warnings retained.
- [x] 1.1B2b — audit/requeue/legacy/scheduler boundary accepted against the audited baseline anchor; behavior green, with historical provenance warnings retained.
- [x] 1.2 — base-column-definition remediation complete after final verification; latest reverify FAIL was remediated without changing unrelated scope.
- [ ] 2.1 — implementation/evidence complete, but checkbox remains delivery-open because the required full Service regression has three unrelated baseline failures; 2.2 remains pending.
- [ ] 3.1–3.2 — reconciliation/backup.
- [ ] 4.1–4.2 — final evidence.

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
