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
- [ ] 2.1–2.2 — delivery/admission.
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
