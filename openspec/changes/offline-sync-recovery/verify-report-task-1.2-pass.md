# Task 1.2 Independent Verification Report

**Change**: `offline-sync-recovery`
**Scope**: task 1.2 after the latest narrow remediation
**Mode**: Strict TDD
**Artifact mode**: hybrid
**Date**: 2026-08-19
**Verdict**: **FAIL**

The latest remediation resolves the previously reported assertion, index-definition, checksum, restart-fingerprint, cancellation-test, immutable-GREEN, and delivery-isolation defects. All independently executed test/build commands pass and latest changed-scope coverage is 40/41 executable lines and 27/28 branches. One fail-closed defect remains: a database carrying the exact current v1/checksum marker but only a recognized legacy outbox shape, or a current shape missing a required index, is repaired and committed instead of being rejected as an inconsistent current schema. No passing test covers this metadata-to-shape invariant.

## Scope and Hybrid State

| Check | Result |
|---|---|
| Filesystem `tasks.md` | Task 1.2 `[x]` |
| Filesystem `apply-progress.md` | Task 1.2 `[x]` |
| Engram tasks artifact | Task 1.2 `[x]` |
| Engram apply-progress artifact | Task 1.2 complete |
| Hybrid consistency | ✅ Consistent |
| Foundation 1.1 children | Intentionally unchecked and delivery-open; not defects in this slice |
| Tasks 2–4 | Pending and outside this slice |
| Prior failed reports | Preserved unchanged |
| Final checkbox recommendation | **Reopen / unchecked** because one current-marker fail-closed invariant remains |

## Independent Runtime Evidence

All commands used finite tool timeouts and `--no-restore`. Exactly one full Service regression was run for this verification. No live backend or Windows matrix was executed.

| Gate | Result |
|---|---|
| Focused `SchemaAdoptionTests` | ✅ 23 passed, 0 failed, 0 skipped |
| Startup + compiled-model tests | ✅ 11 passed, 0 failed, 0 skipped |
| Combined focused discovery | ✅ 34 unique cases; no duplicate names/IDs |
| Service build | ✅ 0 errors; one existing `NU1601` warning |
| Full Service regression, one run | ✅ 1104 passed, 0 failed, 0 skipped |
| Fresh coverage execution | ✅ 34 focused tests passed |

Every required command exited zero.

## Prior-Blocker Acceptance Matrix

| Requirement | Independent evidence | Result |
|---|---|---|
| Partial lifecycle schema rejected before mutation | Two-or-more-but-incomplete lifecycle columns are rejected before DDL; `PartialLifecycleSchema_IsRejectedBeforeAnyDdl` fingerprints exact columns/indexes before and after. | ✅ COMPLIANT |
| Recognized legacy shapes only | The implementation explicitly accepts zero lifecycle columns or the historical `status`-only shape; the tested `status`-only legacy schema upgrades successfully. | ✅ COMPLIANT for markerless legacy |
| Exact current shape only | Metadata is validated independently from outbox shape. A correct current v1/checksum marker plus `status`-only legacy outbox passes metadata validation, is treated as recognized legacy, and is upgraded. Likewise, a current full schema missing a required index is repaired. Current metadata therefore does not require the exact current schema it attests. | ❌ FAILING |
| Lifecycle affinity/type/nullability/default | All eight lifecycle columns are checked exactly; partial and complete wrong-shape tests preserve pre-mutation fingerprints. | ✅ COMPLIANT |
| All required index definitions | Four required names, uniqueness flags, columns, and column order are defined once and validated with `PRAGMA index_list/index_info`. | ✅ COMPLIANT |
| Incompatible same-name index | Wrong named dedup index is rejected before mutation; the generic validator applies the same comparison to every required index. | ✅ COMPLIANT |
| Unexpected index | Runtime RED/GREEN scenario proves unexpected outbox indexes fail closed before mutation. | ✅ COMPLIANT |
| Disconnected assertion removed | `hostedWorkStarted` and literal boolean assertions are absent. | ✅ COMPLIANT |
| Production-linked startup proof | A real `IHostedService` probe remains stopped when adoption fails and starts only after successful adoption through the production boundary. | ✅ COMPLIANT |
| Deterministic exact checksum | Runtime tests assert known checksum `D79AA6FDC58AF83F72F5455BBAA3A7D88429728BB511F02307121F9BBEBA8963`; source computes SHA-256 from a fixed canonical schema string. | ✅ COMPLIANT |
| Missing/NULL/wrong checksum | Dedicated passing tests reject missing column, NULL shape/value, and wrong current checksum before mutation. | ✅ COMPLIANT |
| Higher/lower/nonnumeric/NULL/malformed version | Passing theory/fact scenarios reject all cases without mutation; unrelated marker rows survive. | ✅ COMPLIANT |
| Restart fingerprint | Fingerprint now contains exact index names/uniqueness/column order and `name:version:checksum`; file-backed dispose/reopen/rerun is an exact no-op. | ✅ COMPLIANT |
| Runtime cancellation | Pre-cancelled runtime scenario receives `OperationCanceledException` and confirms no lifecycle mutation. | ✅ COMPLIANT |
| Transaction semantics | Uses a non-deferred Microsoft.Data.Sqlite serializable transaction. Microsoft documentation states SQLite transactions are serializable by default and deferred behavior requires explicit `deferred:true`; this satisfies the design's upfront write-transaction/`BEGIN IMMEDIATE` intent. Rollback uses a non-cancelable token. | ✅ COMPLIANT, documented justification |
| Fresh compiled-model schema parity | Passing production compiled-model test asserts all properties, exact lifecycle affinities/defaults/nullability, and every index definition. | ✅ COMPLIANT |
| Operation-ID backfill | Exact `operation_id=dedup_key` is asserted in legacy and restart scenarios. | ✅ COMPLIANT |
| Version/checksum persistence and idempotence | Exact values persist across rerun/restart and the complete fingerprint is unchanged. | ✅ COMPLIANT |
| Rollback/no partial mutation | Duplicate data, malformed metadata, partial shape, wrong indexes, and wrong current shape preserve fingerprints. | ✅ COMPLIANT |
| Fixed identifiers/parameterized values/safe diagnostics/resources | Identifiers are private fixed tuples/constants; values are parameters; diagnostics are generic; commands/readers/transactions and file-backed connections are disposed. | ✅ COMPLIANT |
| Invalid migration/snapshot artifacts absent | `src/ControlParental.Service/Migrations` remains absent; no conflicting migration path found. | ✅ COMPLIANT |

**Acceptance summary**: 20 compliant, 1 failing current-marker-to-schema invariant.

The broader scheduler, transport, dead-letter, and reconciliation scenarios remain downstream tasks and are not claimed here.

## Correctness and Design Coherence

| Design decision | Status | Notes |
|---|---|---|
| Versioned idempotent adoption before hosted work | ✅ | Production-linked tests pass. |
| Exact complete compiled model | ✅ | Runtime schema/index assertions pass. |
| Recognized legacy adoption | ✅ | Markerless historical shape is accepted and backfilled. |
| Ambiguous partial schema fail closed | ✅ Mostly | Incomplete lifecycle shapes are rejected, except when the outbox is a recognized legacy shape carrying an inconsistent current marker. |
| Current checksum attests exact current shape | ❌ | Metadata and physical-shape classification are not coupled. |
| Exact named indexes | ✅ | Name, order, columns, uniqueness, and unexpected indexes validated. |
| Exclusive/upfront transaction intent | ✅ | Non-deferred serializable provider transaction; documented above. |
| Cancellation preservation | ✅ | Source and runtime evidence agree. |
| Migration conflict removal | ✅ | No invalid artifacts/path. |
| Test file named by task | ⚠️ Deviation | Tests remain in `SchemaAdoptionTests.cs`, not `OutboxManagerTests.cs`; behavior is otherwise directly covered. |

## Strict TDD Matrix

### Raw and Immutable Evidence

| Evidence | Result |
|---|---|
| Latest exact-index RED | ✅ `UnexpectedIndex_IsRejectedBeforeMutation` failed 1/1 before production fix |
| Actual latest RED SHA-256 | `141923FD9053B28C06DE80286C5356A21E12DBA30B41327BE84ABA47661CCAB0` |
| Apply-progress/Engram RED hash | ⚠️ Ends in `...CCAB`, omitting the final `0`; artifact typo/inconsistency |
| Earlier narrow RED | ✅ `red-new.txt`: 2 failed/19 passed; `red-all-tests-first.txt`: 4 failed/18 passed |
| Immutable final focused GREEN | ✅ SHA-256 `423C89594587DDCBAEA2D07E764D1A33E039AD1045B6056142E68A143C6D9BDE`; log records 34/34 passed |
| Immutable final build GREEN | ✅ SHA-256 `F122A152273E06B23B27BFA466D53C89585473FB49771315EDE2F841375AD7C1`; 0 errors |
| Immutable final full regression | ✅ SHA-256 `7B5A4901A7C4B14DFE4CAB0689E452A74F6889903EA267BF0EF5F0A4A9FA1EBA`; 1104/1104 passed |
| Round 2 patch/RED/GREEN | ✅ Hashes independently reconfirmed: patch `25EC...C55`, RED `0C775...86A9`, GREEN `066F...B758` |
| Round 1 raw chronology | ⚠️ Missing and honestly disclosed; task 4.1 explicitly retains it as a warning |
| Apply-progress focused count | ⚠️ States 28, while immutable final GREEN and current discovery/execution show 34 |

Under the configured contract, the missing historical Round 1 raw chronology remains a non-blocking warning because later corrective RED and immutable final GREEN are genuine and no chronology was fabricated. The latest RED hash/count transcription errors are artifact warnings, not behavioral failures.

### TDD Compliance

| Check | Result |
|---|---|
| Evidence row exists | ✅ |
| Corrective RED is genuine | ✅ |
| Immutable final GREEN exists and matches | ✅ |
| Current GREEN passes | ✅ |
| Triangulation of remediated behaviors | ⚠️ No current-marker-plus-legacy/missing-index scenario |
| Historical chronology honesty | ✅ with warning |

### Assertion Quality and Discovery

- Disconnected assertion: removed.
- Tautologies/literal assertions: none found.
- Ghost loops: none found.
- Mock-heavy tests: none; schema tests execute real SQLite behavior.
- Startup assertions observe a real hosted-service state transition.
- Discovery: 34 distinct focused cases; no duplicates.

## Fresh Changed-Scope Coverage

Coverage artifact: `C:/Users/Usuario/AppData/Local/Temp/sdd6-task12-pass-coverage-20260819/fe3effbb-fc9f-4707-8447-7e8e8d2c97da/coverage.cobertura.xml`.

The latest remediation patch adds 48 current production lines; 41 are executable.

| Scope | Executable lines | Branches | Uncovered |
|---|---:|---:|---|
| Latest remediation additions in `SqliteSchemaBootstrapper.cs` | **40/41 = 97.56%** | **27/28 = 96.43%** | Line 172, defensive incomplete-index postcondition |
| Whole current bootstrapper, informational | 152/158 = 96.20% | 119/122 = 97.54% | 6 executable lines |

**Executable-line gate (>80%)**: ✅ Passed.

## Delivery Matrix

| Check | Result |
|---|---|
| Pre-edit byte copies | ✅ Bootstrapper, schema tests, and unchanged Program copies exist |
| Pre-copy continuity | ✅ Pre hashes match the source/test hashes recorded by the preceding independent verification; `Program.cs.pre` equals current Program byte-for-byte |
| Patch SHA-256 | ✅ `1AC341044000456A0BB98136CBD72418C3A3CBFA733A0B9278A985EBA14828FC` |
| Patch applies to pre copies | ✅ Independently applied in memory; resulting lines equal both current files exactly |
| Files in patch | Exactly `SqliteSchemaBootstrapper.cs` and `SchemaAdoptionTests.cs` |
| Unrelated hunks | ✅ None found |
| Patch numstat | Bootstrapper +48/-11; tests +146/-11 |
| Review-budget count | **216 additions+deletions**; **194 added/current changed lines**; both ≤400 |
| Program changes | None; pre/current SHA-256 both `4D529F...61E4B` |
| Docs/audit | Separate from implementation/test patch |

The delivery boundary is valid. The manifest's “194 changed current lines” is an additions/current-line metric; conventional review churn is 216, still safely below 400.

## Issues by Severity

### CRITICAL

1. A correct current v1/checksum marker is not coupled to the exact current outbox lifecycle/index shape. Current metadata plus a legacy physical shape or missing required index is repaired instead of rejected before mutation.

### WARNING

1. Apply-progress and Engram omit the trailing `0` from the actual latest RED SHA-256.
2. Apply-progress/Engram report 28 focused tests, while immutable/current evidence shows 34.
3. Historical Round 1 raw RED/GREEN remains unavailable and explicitly disclosed.
4. Tests remain in `SchemaAdoptionTests.cs` rather than the exact task-named file.

### SUGGESTION

None. Verification was read-only and made no source/test/task changes.

## Checkbox and Readiness Recommendation

| Gate | Recommendation |
|---|---|
| Task 1.2 checkbox | **Reopen / unchecked** |
| Task 1.2 slice | **Not ready** due current-marker/physical-shape inconsistency |
| Foundation | 1.1 children remain intentionally delivery-open and unaffected |
| Full change | **Not ready**; tasks 2–4 remain pending |
| Archive | **Blocked** |

## Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-1.2-pass.md`
- Engram topic: `sdd/offline-sync-recovery/verify/task-1-2-pass`
- Session: `sdd6-offline-sync-recovery-20260818`
- Prior failed reports preserved unchanged.

## Final Verdict

**FAIL** — all commanded runtime/build/coverage and delivery gates pass, but exact-current fail-closed adoption is still incomplete because current metadata does not require the exact current physical schema before mutation.
