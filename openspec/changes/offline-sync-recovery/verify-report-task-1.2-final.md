# Final Slice Verification Report

**Change**: `offline-sync-recovery`
**Scope**: task 1.2 only, after remediation
**Mode**: Strict TDD
**Artifact mode**: hybrid
**Date**: 2026-08-19
**Verdict**: **FAIL**

The remediation materially improves task 1.2: all independently executed focused tests and the single full regression pass, checksum metadata is deterministic, file-backed restart and a production-linked host boundary are covered, cancellation type is preserved, and fresh changed-scope coverage exceeds 80%. The slice nevertheless remains non-compliant because ambiguous partial/current lifecycle schemas are still adopted, index definitions are not validated exactly, a previously identified disconnected assertion remains, and the remediation patch is a 1,162-line cumulative/mixed patch rather than an independently reviewable ≤400-line code+test unit with no unrelated hunks.

## Scope, Checkbox, and Artifact Consistency

| Check | Result |
|---|---|
| OpenSpec task 1.2 | `[x]` in `tasks.md:36` |
| OpenSpec apply progress | `[x]` in `apply-progress.md:15` |
| Engram tasks artifact | `[x]` task 1.2 |
| Engram apply-progress artifact | `[x]` task 1.2 |
| Hybrid consistency | ✅ OpenSpec and Engram agree |
| Verification recommendation | ❌ Reopen/leave unchecked until critical findings are resolved |
| Foundation 1.1 children | Intentionally unchecked and delivery-open; not defects in this slice |
| Tasks 2–4 | Pending and outside this slice |

The previous failed report, `verify-report-task-1.2.md`, was preserved unchanged.

## Independent Build and Runtime Evidence

All commands used finite tool timeouts and `--no-restore`. Exactly one full Service regression was executed during this final verification. No live backend or Windows matrix was run.

| Gate | Command | Result |
|---|---|---|
| Focused schema/startup/compiled model | `dotnet test ... --no-restore --filter "FullyQualifiedName~SchemaAdoptionTests|FullyQualifiedName~ServiceHostStartTests|FullyQualifiedName~NativeAotCompiledModelTests"` | ✅ 27 passed, 0 failed, 0 skipped |
| Service build | `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity quiet` | ✅ 0 errors; one existing `NU1601` warning |
| Full Service regression, single run | `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --verbosity quiet` | ✅ 1097 passed, 0 failed, 0 skipped |
| Focused discovery | `dotnet test ... --no-restore --no-build --list-tests --filter ...` | ✅ 27 unique discovered cases; no duplicate IDs/names |
| Coverage execution | Focused 27 tests with `XPlat Code Coverage` | ✅ 27 passed; fresh Cobertura report generated |

## Acceptance Matrix

| Acceptance requirement | Evidence and assessment | Result |
|---|---|---|
| Deterministic exact checksum metadata | `ControlParentalSchemaVersion.Checksum` is SHA-256 over a fixed canonical schema string; insertion and current-marker validation use exact ordinal equality. Tests prove 64 uppercase hex and wrong-checksum rejection, but do not assert a known exact checksum value. | ⚠️ PARTIAL |
| Invalid/missing checksum fails closed before mutation | Wrong checksum is runtime-tested. Missing/NULL/malformed checksum columns are rejected statically by exact metadata-table shape validation, but no dedicated passing scenarios cover every checksum variant. | ⚠️ PARTIAL |
| Higher/lower/unknown/nonnumeric/NULL version fails closed | Four theory cases plus NULL and malformed metadata pass and preserve the database fingerprint. | ✅ COMPLIANT |
| Missing metadata table/target row recognized as legacy | Legacy-without-table and missing-target-row scenarios pass. | ✅ COMPLIANT |
| Current v1 accepted and unrelated markers preserved | Idempotent rerun accepts current version/checksum; unrelated row version 42 survives. | ✅ COMPLIANT |
| Exact lifecycle affinity/type/nullability/default validation | All eight lifecycle columns are compared against expected affinity, nullability, and default. Wrong `status` shape is runtime-tested. | ✅ COMPLIANT |
| Recognized legacy accepted | The existing legacy shape is upgraded, backfilled, and rerunnable. | ✅ COMPLIANT |
| Ambiguous partial/current shape rejected | The implementation adds every missing lifecycle column before validating. A partial schema with correctly shaped existing lifecycle columns, including a current-marker database missing columns, is silently completed rather than rejected. No legacy-shape classifier distinguishes recognized legacy from ambiguous partial/current state. | ❌ FAILING (static defect) |
| Exact required index validation and uniqueness | Fresh test proves required index names and dedup uniqueness. Production validation checks only that the named dedup index is unique; it does not verify its indexed columns or any other index definition/order. A same-name wrong index can pass. | ❌ FAILING (static defect) |
| Fresh production compiled-model exact parity | Fresh compiled model test proves all columns, lifecycle types/defaults/nullability, required names, and dedup uniqueness; generated outbox model has 16 properties and four indexes. Exact non-dedup index column order is not queried at runtime. | ⚠️ PARTIAL |
| File-backed adopt/dispose/reopen/restart/idempotent no-op | Passing file-backed test closes the first connection, reopens, fingerprints before/after rerun, and verifies operation-ID backfill and checksum format. Fingerprint preserves data, version, and index names but not exact checksum value/index definitions. | ⚠️ PARTIAL |
| Exact operation-ID backfill | Both in-memory and file-backed tests assert exact `operation_id=dedup_key` values. | ✅ COMPLIANT |
| Production-linked startup completion/failure ordering | `StartHostAfterDatabaseInitializationAsync` awaits DB initialization/adoption before `host.StartAsync`. Real `IHostedService` probe stays stopped on failure and starts after success. | ✅ COMPLIANT |
| Rollback/no partial mutation | Failed adoption and metadata rejection preserve schema/data markers; transaction rollback uses `CancellationToken.None`. | ✅ COMPLIANT |
| Transaction mode | Uses `IsolationLevel.Serializable`; no explicit `BEGIN EXCLUSIVE` is issued and no runtime test proves the design's exclusive-lock mode. | ⚠️ PARTIAL / design deviation |
| Cancellation type | `OperationCanceledException` is caught separately, rollback occurs, and the same cancellation exception is rethrown. No focused runtime cancellation test exists. | ⚠️ PARTIAL |
| Resource handling | Commands/readers/transactions are disposed; file-backed connections are closed and the DB can be deleted. | ✅ COMPLIANT |
| Fixed SQL identifiers and parameterized values | Interpolated table/column identifiers are supplied only by private fixed constants/tuples; metadata values are parameters; no user input reaches identifiers. | ✅ COMPLIANT |
| Safe diagnostics | Public failure messages contain no SQL, schema values, payload, token, or secret material. | ✅ COMPLIANT |
| Invalid migration/snapshot path absent | `src/ControlParental.Service/Migrations` is absent and no conflicting Service migration path was found. | ✅ COMPLIANT |

**Acceptance summary**: 11 compliant, 7 partial, 2 failing static acceptance requirements.

Broader transport, retry ownership, dead-letter, and reconciliation scenarios remain downstream tasks and are not claimed by this slice.

## Correctness and Design Coherence

| Design decision | Status | Notes |
|---|---|---|
| Versioned, idempotent adoption before hosted work | ✅ Followed | Production boundary and runtime probe tests pass. |
| Complete compiled model for fresh databases | ✅ Mostly | Runtime schema parity is strong; exact definitions of every non-dedup index are not queried. |
| Backfill stable operation identity | ✅ Followed | Exact value asserted. |
| Validate shape and fail closed for ambiguous schemas | ❌ Not followed | Correctly shaped partial/current lifecycle schemas are completed rather than rejected. |
| Named indexes with exact required semantics | ❌ Incomplete | Only named dedup uniqueness is validated. |
| Exclusive adoption transaction | ⚠️ Deviation | Serializable transaction is used, not an explicit exclusive transaction. |
| Cancellation-aware lifecycle | ✅ Source / ⚠️ test gap | Cancellation type is preserved; no runtime cancellation scenario. |
| `ControlParentalSchemaVersion` artifact | ✅ Followed | Dedicated internal type now owns name, version, and checksum. |
| Invalid migration/snapshot removal | ✅ Followed | No conflicting artifacts remain. |
| Task-directed test ownership | ⚠️ Deviation | Schema tests remain in `SchemaAdoptionTests.cs`, not the task-named `OutboxManagerTests.cs`. |

## Strict TDD Matrix

### Immutable Evidence Inspection

| Evidence | Independent finding |
|---|---|
| Corrective RED requested hash | ✅ `red-corrective-executed.txt` SHA-256 is exactly `CA305C5AF27F26C8A5F2A82095DA1D42E5A2504B6425AC7E090EAC1993BEEDEB` |
| Corrective RED behavior | ✅ Executed RED: 4 failed, 11 passed; an earlier RED also failed compilation because the production startup boundary did not exist |
| Remediation GREEN files | ⚠️ `green-corrective.txt` and `green-corrective-2.txt` are compile failures; `green-corrective-3.txt` still reports 5 failed/10 passed. No immutable final passing GREEN output exists in the evidence directory. |
| Current GREEN | ✅ Independently rerun focused selection: 27/27 passed |
| Round 2 immutable evidence | ✅ Prior immutable patch/hash and RED 5 failed/6 passed remain supporting evidence |
| Round 1 raw chronology | ⚠️ Still absent and honestly disclosed; no historical claim fabricated |
| Apply-progress TDD row | ✅ Present and records Round 1 limitation, Round 2 RED, corrective RED, and claimed GREEN |

The corrective work has credible tests-first RED evidence and current GREEN behavior. Strict TDD chronology is **partial**, not fully immutable: Round 1 raw evidence is absent and the remediation directory does not preserve the final passing GREEN output claimed by the manifest/apply progress.

| Strict TDD check | Result |
|---|---|
| TDD evidence row exists | ✅ |
| Test files exist | ✅ |
| Corrective RED confirmed | ✅ |
| Current GREEN confirmed | ✅ |
| Immutable final GREEN preserved | ⚠️ No |
| Triangulation adequate | ❌ Index/partial-shape/checksum/cancellation gaps remain |
| Safety-net/provenance honesty | ⚠️ Round 1 missing evidence disclosed |

### Test Layer Distribution

| Layer | Cases | Files |
|---|---:|---:|
| Integration | 27 | 3 |
| Unit | 0 | 0 |
| E2E | 0 | 0 |

### Assertion Quality

| File / line | Finding | Severity |
|---|---|---|
| `SchemaAdoptionTests.cs:94-97` | `hostedWorkStarted` is initialized to `false`, cannot be changed by production code, and is asserted false. It is the previously reported disconnected assertion and always passes. | CRITICAL |
| `SchemaAdoptionTests.cs:82,230` | Regex-only checksum assertions prove format, not deterministic exact value or preservation. | WARNING |

The new `StartupProbe` assertions are production-linked and meaningful, but adding them did not remove the existing banned assertion.

**Discovery uniqueness**: ✅ 27 distinct focused cases; no duplicate IDs/names.

## Fresh Changed-Scope Coverage

Coverage artifact: `C:/Users/Usuario/AppData/Local/Temp/sdd6-task12-final-coverage-20260819/565e1516-3647-4227-9ec3-564c5d8823f6/coverage.cobertura.xml`.

| Task-1.2 production scope | Executable lines | Branches | Uncovered executable lines |
|---|---:|---:|---|
| `SqliteSchemaBootstrapper.cs` | 120/126 = **95.24%** | 101/104 = **97.12%** | 34, 54–57, 147 |
| `Program.cs` initialization/start boundary, lines 433–465 | 19/22 = **86.36%** | 2/2 = **100%** | 434, 436–437 |
| `ControlParentalDbContext.cs` outbox model, lines 100–124 | 25/25 = **100%** | 0/0 | — |
| Compiled outbox model, lines 18–130 | 98/98 = **100%** | 0/0 | — |
| **Changed-scope aggregate** | **262/271 = 96.68%** | **103/106 = 97.17%** | 9 lines |

**Executable-line gate (>80%)**: ✅ Passed.

## Delivery Matrix

| Evidence | Finding |
|---|---|
| Remediation patch SHA-256 | ✅ `B94A8640E903AD44A60DE16C6B630BE10240475E47A7DB55035CF74B4F6FD0DF` matches manifest |
| Patch numstat | `SqliteSchemaBootstrapper.cs` 169 additions; `SchemaAdoptionTests.cs` 386 additions; `Program.cs` 420 additions/187 deletions = **1,162 touched lines** |
| Allowed scope | ❌ Patch contains extensive unrelated pre-existing `Program.cs` changes, including TLS, identity, DI, health, UI, and other composition hunks |
| Claimed remediation budget | `budget-audit.txt` derives 16 bootstrap + 165 tests + 41 Program = **222 touched lines** |
| Independently reviewable ≤400 patch | ❌ Not present. The 222-line figure is arithmetic against prior/cumulative state, not the numstat of an immutable remediation-only patch. Baseline hashes exist, but baseline file contents/scoped delta are not preserved in this evidence directory. |
| Cumulative Judgment patches | Supporting prior evidence only; not treated as a sequential historical baseline |
| Audit docs separation | Permissible, but no audit docs are hiding code/test lines here; the blocker is the mixed cumulative implementation patch itself |

The remediation may conceptually be 222 lines, but the supplied immutable patch does **not** prove a ≤400-line, unrelated-hunk-free delivery unit. No false sequential provenance is claimed.

## Issues by Severity

### CRITICAL

1. Ambiguous partial/current lifecycle schemas are still adopted instead of rejected fail closed.
2. Required indexes are not validated exactly; only the name and uniqueness flag of the dedup index are checked.
3. The previously identified disconnected `hostedWorkStarted` assertion remains, violating the Strict TDD assertion-quality gate.
4. The remediation delivery artifact is a mixed 1,162-line patch with unrelated `Program.cs` hunks, not a proven ≤400-line remediation-only code+test unit.

### WARNING

1. Tests do not assert the known exact checksum or dedicated missing/NULL checksum cases.
2. Restart fingerprinting does not prove exact checksum/index-definition preservation.
3. No runtime cancellation-type/rollback scenario exists.
4. Serializable transaction use does not prove the design's explicit exclusive transaction mode.
5. Immutable final passing GREEN output is missing; current GREEN was independently confirmed.
6. Round 1 raw RED/GREEN chronology remains absent and honestly disclosed.
7. Tests remain outside the exact task-named `OutboxManagerTests.cs` file.

### SUGGESTION

None. This verification is read-only and performed no remediation.

## Readiness and Recommendation

| Gate | Recommendation |
|---|---|
| Task 1.2 checkbox | **Reopen / unchecked** |
| Task 1.2 slice | **Not ready** |
| Foundation | 1.1 children remain intentionally delivery-open; task 1.2 independently fails final acceptance |
| Full change | **Not ready**; tasks 2–4 remain pending |
| Archive | **Blocked** |

## Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-1.2-final.md`
- Engram topic: `sdd/offline-sync-recovery/verify/task-1-2-final`
- Session: `sdd6-offline-sync-recovery-20260818`
- Prior report preserved: `openspec/changes/offline-sync-recovery/verify-report-task-1.2.md`

## Final Verdict

**FAIL** — runtime/build/coverage gates now pass, but exact fail-closed schema/index acceptance, Strict TDD assertion quality, and immutable ≤400-line delivery evidence still fail.
