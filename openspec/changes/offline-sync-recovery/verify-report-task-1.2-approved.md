# Task 1.2 Final Independent Re-verification

**Change**: `offline-sync-recovery`
**Scope**: task 1.2 after current-marker/physical-shape coupling remediation
**Mode**: Strict TDD
**Artifact mode**: hybrid
**Date**: 2026-08-19
**Verdict**: **FAIL**

The narrow coupling remediation is genuine and fixes the previously reported current-marker repair behavior for lifecycle columns and required indexes. Its RED/GREEN evidence and isolated delivery patch are valid, focused tests/build pass, and latest changed-scope coverage is 100%. Final approval is nevertheless blocked by two independent findings: the single required full Service regression exited non-zero (1106 passed, 1 failed), and current-marker validation still checks base outbox columns only by name/count rather than exact affinity/nullability/default/primary-key shape, so an exact current marker can accept a physically non-current base-column definition.

## Scope, Checkbox, and Hybrid Consistency

| Check | Result |
|---|---|
| Filesystem `tasks.md` | Task 1.2 `[x]` |
| Filesystem `apply-progress.md` | Task 1.2 `[x]` |
| Engram tasks | Task 1.2 `[x]` |
| Engram apply progress | Task 1.2 complete; counts/hashes match current narrow evidence |
| Hybrid consistency | ✅ Consistent |
| Foundation 1.1 children | Intentionally unchecked and delivery-open; not task-1.2 defects |
| Tasks 2–4 | Pending and outside this slice |
| Prior reports | Preserved unchanged |
| Checkbox recommendation | **Reopen / unchecked** |

## Independent Command Evidence

All commands used finite tool timeouts and `--no-restore`. Exactly one full Service regression was executed. It was not rerun after failure.

| Gate | Result |
|---|---|
| Focused `SchemaAdoptionTests` | ✅ 26 passed, 0 failed, 0 skipped |
| Startup + compiled-model tests | ✅ 11 passed, 0 failed, 0 skipped |
| Combined discovery | ✅ 37 unique cases; no duplicate names/IDs |
| Service build | ✅ 0 errors; one existing `NU1601` warning |
| Full Service regression, one run | ❌ 1106 passed, 1 failed, 0 skipped |
| Regression failure | `NamedPipeUIServerHostedAdapterTests.Listener_DoesNotReportReadyUntilPipeCreationSucceeds` |
| Fresh coverage execution | ✅ 37 focused tests passed |

Per the verification contract, the non-zero regression command is CRITICAL even though the failure is outside task 1.2 and prior immutable regression evidence was green.

## Acceptance Matrix

| Requirement | Independent assessment | Result |
|---|---|---|
| Exact current marker/checksum takes no mutation path | `hasCurrentMarker` now enters validation-only logic, commits, and returns without DDL/DML. File-backed rerun proves full-fingerprint no-op. | ✅ COMPLIANT |
| Current marker + status-only/partial lifecycle | Dedicated passing test rejects and preserves full fingerprint before mutation. | ✅ COMPLIANT |
| Current marker + wrong lifecycle shape | Dedicated passing test rejects exact-marker database with malformed `status` and preserves full fingerprint. | ✅ COMPLIANT |
| Current marker + missing required index | Dedicated passing test rejects and preserves full fingerprint. | ✅ COMPLIANT |
| Current marker + wrong/unexpected index | `ValidateIndexesAsync(requireAll:true)` checks every existing index name, uniqueness, columns, order, rejects unknown names, and requires all definitions. Wrong/unexpected paths are runtime-covered in markerless pre-mutation tests; current-marker missing-index path exercises the strict branch. | ✅ COMPLIANT / shared-path evidence |
| Exact current column set | Current marker requires exactly the eight base names plus eight lifecycle names; missing/extra names reject. | ✅ COMPLIANT |
| Exact current base-column definitions | `ValidateBaseSchema` and `ValidateCurrentPhysicalSchema` inspect only base-column names/count. They do not validate base affinity, nullability, defaults, or PK shape. A current marker with, for example, `attempts TEXT NULL` and otherwise exact lifecycle/index definitions can pass. | ❌ FAILING |
| Exact current lifecycle definitions | All lifecycle affinity/nullability/default values are checked. | ✅ COMPLIANT |
| Marker-absent recognized legacy | Historical status-only legacy remains accepted, backfilled, indexed, versioned, and rerunnable. | ✅ COMPLIANT |
| Unsupported/malformed metadata | Higher/lower/nonnumeric/NULL/duplicate/malformed version and missing/NULL/wrong checksum fail closed; unrelated markers survive. | ✅ COMPLIANT |
| Deterministic checksum | Known exact value `D79AA6FDC58AF83F72F5455BBAA3A7D88429728BB511F02307121F9BBEBA8963` is asserted. | ✅ COMPLIANT |
| Exact indexes | All four names, uniqueness flags, columns, and order are asserted and validated. | ✅ COMPLIANT |
| Fresh production compiled model | All required properties, lifecycle defaults/nullability/types, and index definitions are runtime-tested. | ✅ COMPLIANT |
| Operation-ID backfill | Exact `operation_id=dedup_key` is asserted in legacy and restart paths. | ✅ COMPLIANT |
| File-backed restart/idempotence | Dispose/reopen/rerun preserves data, exact marker/checksum, exact index definitions, and complete fingerprint. | ✅ COMPLIANT |
| Startup ordering | Real hosted-service probe starts only after adoption and remains stopped on failure. | ✅ COMPLIANT |
| Rollback/no mutation | Failure scenarios preserve full columns/index/data/metadata fingerprint. | ✅ COMPLIANT |
| Cancellation | Runtime test preserves `OperationCanceledException` and no mutation. | ✅ COMPLIANT |
| Transaction semantics | Non-deferred serializable Microsoft.Data.Sqlite transaction satisfies the design's upfront `BEGIN IMMEDIATE` intent; rollback is non-cancelable. | ✅ COMPLIANT |
| Fixed identifiers/parameters/diagnostics/resources | Fixed private identifiers, parameterized metadata values, generic diagnostics, and scoped disposal verified. | ✅ COMPLIANT |
| Invalid migration/snapshot artifacts | `Migrations` remains absent; no conflicting migration path found. | ✅ COMPLIANT |

**Acceptance summary**: 20 compliant, 1 failing exact-base-definition requirement.

Broader scheduler, transport, dead-letter, and reconciliation scenarios remain downstream tasks and are not claimed.

## Correctness and Design Coherence

| Decision | Status | Notes |
|---|---|---|
| Marker-current means validation-only/no repair | ✅ | Corrected. |
| Current lifecycle/index coupling | ✅ | Exact and fail-closed. |
| Exact current physical schema | ⚠️ Incomplete | Base names are exact, base definitions are not. |
| Markerless legacy adoption | ✅ | Preserved. |
| Transactional startup adoption | ✅ | Production-linked and provider-semantics justified. |
| Checksum/version/backfill/restart | ✅ | Runtime evidence passes. |
| Invalid migrations removed | ✅ | No conflicting path. |
| Task-directed test file | ⚠️ Deviation | Tests remain in `SchemaAdoptionTests.cs`, not task-named `OutboxManagerTests.cs`. |

## Strict TDD Matrix

### Latest RED/GREEN Evidence

| Evidence | Result |
|---|---|
| Primary current-marker RED | ✅ SHA-256 `4BB3E2DB6760759BE33F44F2DBD12617F9B3AC03333C654413E070DA1D3ADE91`; 2 failed/0 passed |
| Additional wrong-lifecycle RED | ✅ SHA-256 `D8D0139B4C305CB6C897021275DCE69E947733AFD054F773F0345D802AA8C846`; 1 failed/0 passed |
| Immutable focused GREEN | ✅ SHA-256 `E77849220A80F57C215562815BCB897067D5319FB1AE269E9C9E66B83F3B7D55`; 37/37 passed |
| Immutable Service build | ✅ SHA-256 `3FB73D54310D131501934744E55ABB56A037830D5A617E2E9553283F990D5F00`; 0 errors |
| Immutable full regression | ✅ SHA-256 `C5C868820037A7DAF1C06097152C64B46E039B92610579A4EB94BA5B86FD8FDC`; 1107/1107 passed historically |
| Current focused GREEN | ✅ 37/37 |
| Current full regression | ❌ 1106/1107; immutable prior GREEN does not override current non-zero execution |
| Round 2 evidence | ✅ Previously verified immutable patch/RED/GREEN remains intact |
| Round 1 raw chronology | ⚠️ Missing and honestly disclosed; non-blocking warning under current contract |

The latest narrow cycle is valid tests-first TDD. Historical Round 1 absence remains the configured warning only. Final slice failure comes from current correctness/regression evidence, not fabricated chronology.

### Assertion Quality and Discovery

- `hostedWorkStarted` disconnected assertion: absent.
- Literal tautologies: none found.
- Ghost loops: none found.
- Mock-heavy schema tests: none; real SQLite is used.
- Production startup probe asserts a real hosted-service transition.
- Discovery: 37 unique focused cases.

## Fresh Changed-Scope Coverage

Coverage artifact: `C:/Users/Usuario/AppData/Local/Temp/sdd6-task12-approved-coverage-20260819/07e346b1-d497-4a19-aeba-51cb4775270f/coverage.cobertura.xml`.

The latest patch adds 28 current production lines; 21 are executable.

| Scope | Lines | Branches | Uncovered |
|---|---:|---:|---|
| Latest `SqliteSchemaBootstrapper.cs` remediation | **21/21 = 100.00%** | **18/18 = 100.00%** | — |

**Executable-line gate (>80%)**: ✅ Passed.

## Delivery Matrix

| Check | Result |
|---|---|
| Patch SHA-256 | ✅ `370A0E61941BAD7ABA6747A4957F317B1C4D4F580AF3C717B29069CD4132B2D3` |
| Pre-edit byte copies | ✅ Bootstrapper/tests copies exist and hashes equal the prior independent verification's current hashes |
| Patch basis | ✅ In-memory application to pre copies reproduces both current files exactly |
| Files | Only `SqliteSchemaBootstrapper.cs` and `SchemaAdoptionTests.cs` |
| Unrelated hunks | ✅ None |
| Numstat | Bootstrapper +28/-4; tests +62/-0 |
| Review budget | **94 additions+deletions**; **90 added/current changed lines**; both ≤400 |
| Docs | Separate from implementation/test patch |

Delivery evidence is valid and reviewable.

## Issues by Severity

### CRITICAL

1. The required full Service regression exited non-zero: 1106 passed, 1 failed.
2. Current-marker validation does not validate exact base-column affinity/nullability/default/PK definitions, only their names/count.

### WARNING

1. Historical Round 1 raw chronology remains unavailable and disclosed under the configured warning policy.
2. Tests remain in `SchemaAdoptionTests.cs` rather than the task-named file.
3. The regression failure is outside task 1.2 and has previously shown instability, but the required command was intentionally not rerun.

### SUGGESTION

None. Verification was read-only and made no source/test/task changes.

## Recommendation and Readiness

| Gate | Recommendation |
|---|---|
| Task 1.2 checkbox | **Reopen / unchecked** |
| Task 1.2 slice | **Not approved** |
| Foundation | 1.1 children remain intentionally delivery-open and unaffected |
| Full change | **Not ready**; tasks 2–4 remain pending |
| Archive | **Blocked** |

## Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-1.2-approved.md`
- Engram: `sdd/offline-sync-recovery/verify/task-1-2-approved`
- Session: `sdd6-offline-sync-recovery-20260818`
- All prior reports preserved.

## Final Verdict

**FAIL** — narrow marker coupling, TDD, focused behavior, coverage, and delivery pass, but the mandatory current full regression failed and exact current base-column definitions remain unchecked.
