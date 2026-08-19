# Verification Report

**Change**: `offline-sync-recovery`
**Verification scope**: task 1.2 only
**Artifact store**: hybrid
**Mode**: Strict TDD
**Date**: 2026-08-19
**Verdict**: **FAIL**

Task 1.2 has substantial working implementation and its focused suites pass, but it does not meet the exact task acceptance or the Strict TDD gate. The schema checksum is absent, existing lifecycle-column shape is not validated, restart evidence is not file-backed, Round 1 has no raw RED/GREEN evidence, the task has no TDD row in `apply-progress.md`, one hosted-work assertion is disconnected from production behavior, and the independently executed full Service regression exited non-zero.

## Scope and Completeness

| Metric | Result |
|---|---|
| Tasks evaluated | 1 (`1.2`) |
| Task checkbox | Unchecked; unchanged as instructed |
| Objectively complete | 0/1 |
| Foundation tasks 1.1A/1.1B1/1.1B2a/1.1B2b | Intentionally pending historical delivery/provenance gates; not defects in this slice |
| Units 2–4 | Outside this slice and still pending |

Exact task wording verified from `tasks.md:36`: fresh/existing `EnsureCreated`, adoption, schema version/checksum, backfill, defaults/constraints/indexes, restart/rollback, bootstrap/version wiring, and removal of invalid migration/snapshot artifacts under RED → GREEN → TRIANGULATE/REFACTOR.

## Build and Runtime Test Evidence

All commands used finite tool timeouts and `--no-restore`. No live backend or Windows matrix was run.

| Check | Command | Independent result |
|---|---|---|
| Focused schema adoption | `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --verbosity quiet --filter "FullyQualifiedName~SchemaAdoptionTests"` | ✅ 11 passed, 0 failed |
| Startup/compiled-model | `dotnet test ... --no-restore --verbosity quiet --filter "FullyQualifiedName~NativeAotCompiledModelTests|FullyQualifiedName~ServiceHostStartTests"` | ✅ 11 passed, 0 failed |
| Service build | `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity quiet` | ✅ 0 errors; 1 pre-existing `NU1601` warning |
| Full Service regression | `dotnet test ... --no-restore --no-build --verbosity quiet` | ❌ 1091 passed, 1 failed, 0 skipped; `NamedPipeUIServerHostedAdapterTests.Listener_DoesNotReportReadyUntilPipeCreationSucceeds` |
| Failed-test reproduction | Same test alone, quiet | ❌ Failed |
| Failed-test second rerun | Same test alone, normal | ✅ Passed; result is unstable/flaky, but the required full-regression command still exited non-zero |
| Coverage run | `dotnet test ... --no-restore --no-build --collect:"XPlat Code Coverage" --filter "...SchemaAdoptionTests|...NativeAotCompiledModelTests|...ServiceHostStartTests"` | ✅ 22 passed; Cobertura generated |

The historical declaration of 1092/1092 is therefore not accepted as current regression evidence.

## Behavioral and Acceptance Compliance Matrix

| Acceptance / design requirement | Runtime/static evidence | Result |
|---|---|---|
| Adoption runs after DB creation/open and before hosted/background outbox work | `Program.cs:433-462` orders `EnsureCreated`, usage compatibility, adoption, then `host.RunAsync`; `InitializeDatabaseAsync` preserves the same order. Failure test calls the helper, but no runtime test observes that hosted services cannot start. | ⚠️ PARTIAL |
| Fresh production compiled model creates every SDD6 outbox property and required indexes | `FreshProductionModel_EnsureCreated_CreatesCompleteOutboxSchema`; compiled model has 16 properties and 4 indexes. | ✅ COMPLIANT |
| Fresh-schema nullability/default/type/index semantics are proved | Test checks column presence, types for new fields, status/claim defaults, a non-null operation default, and index names. It does not assert `NOT NULL`, exact operation default, or unique-index semantics. | ⚠️ PARTIAL |
| Legacy data is preserved and stable `operation_id=dedup_key` is backfilled | `LegacySchema_AdoptionBackfillsAndIsIdempotent` passed. | ✅ COMPLIANT |
| Legacy defaults, constraints, indexes, and ambiguous-shape rejection | Missing columns and named indexes are added, but the bootstrap validates only base-column names. Existing lifecycle columns with wrong type/nullability/default and same names are accepted, including with current version 1. | ❌ UNTESTED / static defect |
| Schema version and checksum behavior | Version 1 is recorded and validated. No checksum column, constant, computation, validation, or test exists. | ❌ UNTESTED |
| Idempotent rerun | Same-context second adoption produced the same schema fingerprint. | ✅ COMPLIANT |
| Restart behavior | No file-backed close/reopen/restart schema-adoption test exists. Same open in-memory connection is not restart evidence. | ❌ UNTESTED |
| Rollback/failure leaves no partial mutation | Duplicate-dedup-key adoption failure rolls back added columns and metadata; rejection tests compare pre/post fingerprints. | ✅ COMPLIANT |
| Higher/lower/unknown numeric, nonnumeric, and NULL versions fail closed | Four theory cases plus NULL case passed and preserved the fingerprint. | ✅ COMPLIANT |
| Duplicate/malformed metadata fails closed | Both tests passed without mutation. The duplicate case is rejected as malformed metadata because a valid exact table has a PK and cannot contain duplicate names. | ✅ COMPLIANT |
| Missing metadata table/row is recognized as legacy; current v1 is accepted; unrelated markers survive | Missing table is exercised by legacy adoption, missing target row/unrelated marker has a focused test, and v1 is accepted by the idempotent rerun. | ✅ COMPLIANT |
| Invalid migration/snapshot artifacts absent; no conflicting migration path | `src/ControlParental.Service/Migrations` is absent and no `Migrate()`/migration reference was found in Service source. | ✅ COMPLIANT |
| SQL identifiers fixed and values parameterized | Dynamic identifiers/definitions come only from private fixed literals; metadata values use parameters. | ✅ COMPLIANT |
| Diagnostics are safe | Adoption throws a generic message without SQL/data/secret material. | ✅ COMPLIANT |
| Cancellation, resource, and transaction behavior | Commands/readers/transaction are disposed and rollback uses `CancellationToken.None`. Cancellation is wrapped as `InvalidOperationException`, and adoption uses default `BeginTransactionAsync`, not the design's explicit exclusive transaction. | ⚠️ PARTIAL |

**Compliance summary**: 9 compliant, 3 partial, 3 untested/defective acceptance rows.

The broader spec scenarios for transport, scheduler retry ownership, dead-letter behavior, and reconciliation are downstream tasks and were not claimed by this task-1.2 slice. This slice only verifies their durable schema and startup prerequisites.

## Correctness (Static Evidence)

| Area | Status | Notes |
|---|---|---|
| Program wiring | ✅ Implemented | Adoption occurs before `host.RunAsync`; failure aborts startup. |
| Compiled-model parity | ✅ Implemented | Current source/test hashes match the Round 2 manifest for bootstrap/tests; compiled outbox model contains all 16 runtime properties. |
| Legacy adoption | ⚠️ Incomplete | Transactional and idempotent for the tested legacy shape, but no full existing-column shape validation. |
| Metadata | ⚠️ Incomplete | Fail-closed version handling exists; checksum does not. |
| Restart | ❌ Not proved | No file-backed restart scenario. |
| Migration conflict removal | ✅ Implemented | Invalid migration/snapshot artifacts are absent. |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Versioned idempotent adoption before hosted work | ✅ Yes | Implemented in `Program` and `SqliteSchemaBootstrapper`. |
| Complete EF model for fresh DB | ✅ Yes | Production compiled-model test passed. |
| Backfill, named indexes, fail-closed metadata | ✅ Mostly | Tested version rejection and backfill pass. |
| Validate final/ambiguous schema shape | ❌ No | Existing new columns are accepted by name without type/nullability/default validation. |
| Exclusive adoption transaction | ⚠️ Deviation | Uses default `BeginTransactionAsync`; no explicit exclusive/immediate mode. |
| Add `ControlParentalSchemaVersion` | ⚠️ Deviation | Version is represented by private constants rather than the designed type. |
| Task-owned test location | ⚠️ Deviation | Tests were added to `SchemaAdoptionTests.cs`, not the exact `OutboxManagerTests.cs` location named by task 1.2. |

## Strict TDD Compliance

### Raw Evidence and Hashes

| Evidence | Independent result |
|---|---|
| Round 1 patch SHA-256 | ✅ `639129F9A711529AA22ACD393BB939A8E5A10393181FD139AB1AEC8B67392A42` |
| Round 1 raw RED/GREEN | ❌ No raw RED or GREEN files exist in the Round 1 directory; the patch combines tests and production implementation. Tests-first chronology cannot be proved. |
| Round 2 patch SHA-256 | ✅ `25EC0860C12F5D233F74489E50B9C5B91C5E8211175F8CC2AEDF7886A7572C55` |
| Round 2 RED hash/result | ✅ `0C775D...386A9`; 5 failed and 6 passed. Failures were the new unsupported-version and NULL-version expectations. |
| Round 2 GREEN hash/result | ✅ `066F5D...BB758`; 11 passed and 0 failed. |
| Round 2 evidence hashes | ✅ RED, GREEN, startup, build, and regression hashes match `manifest.txt`. |
| `apply-progress.md` TDD row for 1.2 | ❌ Missing; it still says task 1.2 is intentionally not implemented. |

Round 2 honestly demonstrates a tests-first corrective cycle for fail-closed version metadata. It does not establish a Strict TDD cycle for the original Round 1 schema/bootstrap implementation.

| Strict TDD check | Result | Details |
|---|---|---|
| TDD evidence reported in apply progress | ❌ | No task-1.2 TDD row; progress artifact is stale. |
| Test file exists | ✅ | `SchemaAdoptionTests.cs` exists. |
| RED confirmed | ⚠️ | Round 2 only; no Round 1 raw RED. |
| GREEN confirmed now | ✅ | 11/11 schema and 11/11 startup/compiled-model tests pass. |
| Triangulation adequate | ⚠️ | Version cases are triangulated; checksum, restart, and malformed existing-column shapes are absent. |
| Safety net | ⚠️ | Round 2 baseline hashes exist; Round 1 pre-production safety-net/RED output does not. |

**Strict TDD compliance**: 2/6 checks fully passed.

### Test Layer Distribution

| Layer | Tests | Files | Notes |
|---|---:|---:|---|
| Unit | 0 | 0 | — |
| Integration | 22 | 3 | Real SQLite schema/model and host-composition tests |
| E2E | 0 | 0 | Out of scope |
| **Total** | **22** | **3** | `SchemaAdoptionTests`, `NativeAotCompiledModelTests`, `ServiceHostStartTests` |

### Assertion Quality

| File / line | Assertion | Issue | Severity |
|---|---|---|---|
| `SchemaAdoptionTests.cs:80-83` | `hostedWorkStarted = false; Assert.False(hostedWorkStarted)` | Disconnected constant: production code cannot change the variable, so the assertion always passes and does not prove hosted work was prevented. | CRITICAL |
| `SchemaAdoptionTests.cs:35` | `Assert.NotNull(columns["operation_id"].DefaultValue)` | Weak default assertion; it does not prove the required exact default. | WARNING |

No mock-heavy tests or ghost loops were found.

## Changed-Scope Coverage

Coverage artifact: `C:/Users/Usuario/AppData/Local/Temp/opencode/sdd6-task12-verify-coverage-20260819/ea11cd85-34f8-4376-826f-33ec1b6552ed/coverage.cobertura.xml`.

| Task-owned production scope | Line coverage | Branch coverage | Uncovered executable lines |
|---|---:|---:|---|
| `SqliteSchemaBootstrapper.cs` | 108/113 = **95.58%** | 62/64 = **96.88%** | 15–17, 57–58 |
| `Program.cs` task-1.2 touched executable lines | 4/6 = **66.67%** | N/A | 441, 482 |
| `ControlParentalDbContext.cs` task-1.2 touched lines | 2/2 = **100%** | N/A | — |
| `CompiledModels/OutboxDbEntityEntityType.cs` task-1.2 touched executable lines | 30/30 = **100%** | N/A | — |
| **Patch-derived changed scope** | **144/151 = 95.36%** | **62/64 = 96.88%** | 7 executable lines |

**Executable-line gate (>80%)**: ✅ Passed. Coverage is informational under Strict TDD and does not override missing acceptance tests or the failed regression command.

## Quality Metrics

- **Build/type check**: ✅ 0 errors.
- **Analyzer/linter**: no dedicated changed-file lint command is configured/executed; build emitted the existing package warning only in this incremental run.
- **Resource review**: async readers/commands/transaction are disposed; cancellation semantics remain an informational concern noted below.

## Delivery Evidence and Exact Boundaries

### Round 1

- Hash independently matched: `639129F9...A42`.
- Numstat independently reproduced.
- Production + coupled tests, excluding audit ledger: **276 touched lines** (272 additions, 4 deletions).
- Audit ledger: 4 touched lines (2 additions, 2 deletions).
- Total patch: **280 touched lines**, within the 400-line budget.

### Round 2

- Hash independently matched: `25EC0860...C55`.
- Patch reports files from `/dev/null`, not a sequential Round-1-to-Round-2 delta:
  - `SqliteSchemaBootstrapper.cs`: 153 additions.
  - `SchemaAdoptionTests.cs`: 221 additions.
  - Production + coupled tests: **374 touched lines**, within budget.
  - `review-ledger.md`: 77 additions.
  - Combined patch: **451 touched lines**, over budget.
- The 77-line ledger can be a separate docs/audit review unit. Doing so does **not** hide production or test changes because all code + coupled tests remain visible together in the 374-line unit.
- However, `baseline-numstat.txt` is empty and this patch is cumulative for its two code/test files; it overlaps Round 1 and omits Round 1's `Program`, DbContext, and compiled-model changes. It must not be represented as an immutable sequential historical Round 2 delivery delta.
- Current bootstrap/test hashes match the Round 2 manifest. The current ledger has legitimately advanced beyond that manifest to terminal approval and therefore has a different hash.

No size exception is needed for either reviewable code+test unit. Exact historical sequential provenance remains unproved; no provenance is invented here.

## Judgment Day Reconciliation (INFO, Non-blocking)

- Terminal Judgment is independently confirmed as `APPROVED`; JD-001, JD-002, and JD-R1-B-001 are marked verified.
- JD-003/JD-004 are untouched prior one-judge suspects outside this slice.
- JD-005/JD-006 are outside this schema slice.
- JD-007 correctly describes mixed historical provenance; delivery limitations are reported above.
- JD-R2-A-001 (NULL test shape) is INFO: the implementation still fails closed before mutation.
- JD-R2-B-001 (cancellation wrapped as `InvalidOperationException`) is INFO and non-blocking for Judgment; it remains a design/coherence limitation.

Judgment approval validates its scoped code-risk fixes; it does not override SDD task acceptance, Strict TDD, regression, or delivery gates.

## Issues by Severity

### CRITICAL

1. **Required full Service regression exited non-zero** (1091/1092); the failing unrelated test was unstable across focused reruns, but the mandated regression gate did not pass.
2. **Schema checksum acceptance is absent** from production metadata and tests.
3. **Ambiguous existing lifecycle-column shapes are not validated**: same-name columns with incompatible types/nullability/defaults can be accepted, contrary to fail-closed design and task constraints acceptance.
4. **Restart behavior is untested**: no file-backed close/reopen adoption test exists.
5. **Strict TDD chronology is incomplete**: no Round 1 raw RED/GREEN and no task-1.2 TDD row in `apply-progress.md`.
6. **Assertion-quality violation**: `hostedWorkStarted` is a disconnected constant and does not prove the named behavior.

### WARNING

1. Program main-path adoption ordering is statically correct but not directly observed by a runtime hosted-service gate test.
2. Fresh-schema tests do not assert full nullability, exact operation default, or unique-index semantics.
3. Adoption transaction mode is not explicitly exclusive as designed.
4. The designed `ControlParentalSchemaVersion` type is absent; private constants are used.
5. Tests live in `SchemaAdoptionTests.cs` rather than the exact task-named `OutboxManagerTests.cs`.
6. Round 2 patch is cumulative/non-sequential evidence; the ledger split is reviewable but does not repair historical provenance.

### SUGGESTION

None. Required remediation is already captured above; this verification did not modify source/tests/tasks.

## Recommendations and Readiness

| Gate | Recommendation |
|---|---|
| Task 1.2 checkbox | **Keep unchecked** |
| Task 1.2 slice readiness | **Not ready** |
| Foundation readiness | 1.1 pending provenance gates remain intentional/non-defects; task 1.2 independently fails its own gate |
| Full-change readiness | **Not ready**; Units 2–4 and final verification remain pending |
| Archive readiness | **Blocked** |

## Artifact Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-1.2.md`
- Engram topic requested: `sdd/offline-sync-recovery/verify/task-1-2`
- Engram session requested: `sdd6-offline-sync-recovery-20260818`

## Final Verdict

**FAIL** — focused behavior, compiled-model parity, fail-closed version handling, build, and changed-scope coverage pass, but required task acceptance, Strict TDD evidence, assertion quality, and full-regression gates do not.
