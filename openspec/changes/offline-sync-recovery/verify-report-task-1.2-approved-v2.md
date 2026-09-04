# Task 1.2 Final Independent Re-verification V2

## Verification Report

**Change**: `offline-sync-recovery`  
**Scope**: task 1.2 after exact base-column-definition remediation  
**Version**: N/A  
**Mode**: Strict TDD  
**Artifact mode**: hybrid  
**Date**: 2026-08-19  
**Slice verdict**: **PASS WITH WARNINGS**

The exact-base-definition defect from the preceding report is fixed. Current-marker acceptance now validates every base column's declared SQLite type, nullability, default, and primary-key role before taking the no-op path. Four malformed variants passed as rejection/no-mutation tests, focused and startup suites passed, the Service built, and the single independently executed full Service regression passed 1111/1111.

### Completeness

| Metric | Value |
|---|---:|
| Task 1.2 slice tasks | 1 |
| Task 1.2 complete | 1 |
| Task 1.2 incomplete | 0 |
| Full change tasks | 11 |
| Full change complete | 1 |
| Full change incomplete | 10 |

Task 1.2 is approved and may remain checked. The full change is not complete or archive-ready: foundation children 1.1A–1.1B2b and tasks 2.1–4.2 remain unchecked.

### Build & Tests Execution

All verification commands used finite timeouts and `--no-restore`. Exactly one full Service regression was executed.

| Gate | Command | Result |
|---|---|---|
| Service build | `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --verbosity quiet` | ✅ 0 errors; one existing `NU1601` warning |
| Focused schema tests | `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --verbosity quiet --filter "FullyQualifiedName~SchemaAdoptionTests"` | ✅ 30 passed, 0 failed, 0 skipped |
| Startup + compiled model | `dotnet test ... --filter "FullyQualifiedName~ServiceHostStartTests|FullyQualifiedName~NativeAotCompiledModelTests"` | ✅ 11 passed, 0 failed, 0 skipped |
| Discovery | `dotnet test ... --list-tests --filter "FullyQualifiedName~SchemaAdoptionTests|FullyQualifiedName~ServiceHostStartTests|FullyQualifiedName~NativeAotCompiledModelTests"` | ✅ 41 unique cases, including four malformed-base theory cases |
| Full Service regression | `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --verbosity quiet` | ✅ 1111 passed, 0 failed, 0 skipped |
| Focused coverage | `dotnet test ... --collect:"XPlat Code Coverage" --filter "FullyQualifiedName~SchemaAdoptionTests|FullyQualifiedName~ServiceHostStartTests|FullyQualifiedName~NativeAotCompiledModelTests"` | ✅ 41 passed; coverage artifact produced |

The unrelated named-pipe test that failed in the preceding verification passed as part of the one full regression. It was not changed by this remediation patch.

### Task 1.2 Acceptance Compliance Matrix

| Requirement | Covering runtime evidence | Result |
|---|---|---|
| Fresh production model creates complete outbox schema | `FreshProductionModel_EnsureCreated_CreatesCompleteOutboxSchema` | ✅ COMPLIANT |
| Exact current base type/nullability/default/PK | Four cases of `CurrentMarker_WithMalformedBaseDefinition_IsRejectedBeforeMutation` plus current-marker success/restart paths | ✅ COMPLIANT |
| Malformed base rejection is pre-mutation | Each malformed case compares the complete before/after database fingerprint | ✅ COMPLIANT |
| Exact current marker is validation-only | `FileBackedRestart_PreservesAdoptedSchemaDataAndNoOp` | ✅ COMPLIANT |
| Current marker rejects partial lifecycle | `CurrentMarker_WithStatusOnlySchema_IsRejectedBeforeMutation` | ✅ COMPLIANT |
| Current marker rejects wrong lifecycle definitions | `CurrentMarker_WithWrongLifecycleShape_IsRejectedBeforeMutation` | ✅ COMPLIANT |
| Current marker rejects missing required index | `CurrentMarker_WithMissingRequiredIndex_IsRejectedBeforeMutation` | ✅ COMPLIANT |
| Wrong and unexpected indexes fail closed | `ExistingWrongNamedIndex_IsRejectedBeforeMutation`, `UnexpectedIndex_IsRejectedBeforeMutation` | ✅ COMPLIANT |
| Recognized legacy schema adopts and reruns safely | `LegacySchema_AdoptionBackfillsAndIsIdempotent` | ✅ COMPLIANT |
| Stable operation identity is backfilled | Legacy and file-backed restart tests assert `operation_id = dedup_key` | ✅ COMPLIANT |
| Version/checksum metadata is deterministic | Known checksum and version assertions; malformed/unsupported metadata tests | ✅ COMPLIANT |
| Failure rolls back without mutation | Failure, malformed metadata, lifecycle, index, and base-definition fingerprint assertions | ✅ COMPLIANT |
| Cancellation preserves cancellation semantics | `Cancellation_PreservesOperationCanceledExceptionAndDoesNotMutate` | ✅ COMPLIANT |
| Startup adoption precedes hosted work | Two `ProductionStartupBoundary_*` tests | ✅ COMPLIANT |
| Production compiled model is wired | `NativeAotServiceOptions_UseTheGeneratedCompiledModel` | ✅ COMPLIANT |
| Invalid migration/snapshot path is absent | No `src/ControlParental.Service/Migrations/**` files found | ✅ COMPLIANT |

**Compliance summary**: 16/16 task-1.2 acceptance checks compliant. The broader specification's delivery, scheduler, dead-letter, and reconciliation scenarios belong to unchecked tasks and are not claimed by this slice.

### Correctness (Static Evidence)

| Requirement | Status | Notes |
|---|---|---|
| Validate metadata before mutation | ✅ Implemented | `AdoptAsync` reads marker and table metadata before any DDL/DML. |
| Couple current marker to exact base shape | ✅ Implemented | `ValidateBaseSchema` requires `BaseColumns` or the explicit `LegacyBaseColumns`; `MatchesBaseDefinition` compares type, `notnull`, default, and PK role. |
| Match current compiled model | ✅ Implemented | Current tuples match the generated model/production `EnsureCreated` shape; legacy tuples retain the explicitly supported historical `id`/`attempts` definitions. |
| Reject before current-marker no-op | ✅ Implemented | Base validation executes before the `hasCurrentMarker` branch. |
| Preserve exact lifecycle/index checks | ✅ Implemented | Existing strict lifecycle and index validation remains after base validation. |

### Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| Versioned, idempotent SQLite adoption before hosted work | ✅ Yes | Runtime bootstrap remains wired before `host.StartAsync`. |
| Ambiguous physical schemas fail closed | ✅ Yes | Malformed base definitions now reject before mutation. |
| Fresh schema comes from complete EF model | ✅ Yes | Production compiled model and fresh `EnsureCreated` are runtime-tested. |
| Marker-current means validation-only, never repair | ✅ Yes | Exact base/lifecycle/index validation precedes no-op return. |
| Task-directed test location | ⚠️ Deviation | Tests are in the more focused `SchemaAdoptionTests.cs`, not task-named `OutboxManagerTests.cs`; behavior and traceability are stronger, but the artifact differs from the literal task wording. |

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | `apply-progress.md` contains the base-definition RED/GREEN cycle. |
| Test file exists | ✅ | `SchemaAdoptionTests.cs` contains the four-case theory. |
| RED confirmed | ✅ | Immutable RED is 4 failed / 0 passed; SHA-256 `6FB6B1CEED0C3AAD9917141DA4EBE8329EB7F3EF9AC4EA8CDD97BC0DC2122C30`. |
| GREEN confirmed | ✅ | Independent execution passed all 30 schema tests and all 41 focused tests with startup/compiled-model coverage. |
| Triangulation adequate | ✅ | Wrong type, nullability, default, and primary-key role are distinct cases. |
| No-mutation behavior asserted | ✅ | Every malformed case snapshots and compares the database fingerprint. |
| Historical safety net | ⚠️ | Round 1 standalone raw chronology was not preserved and remains honestly disclosed; current corrective cycle evidence is immutable. |

**TDD compliance**: 6/7 checks pass; one historical warning remains.

### Test Layer Distribution

| Layer | Tests | Files | Tool |
|---|---:|---:|---|
| Unit | 0 | 0 | xUnit |
| Integration | 41 | 3 | xUnit + real SQLite/host/EF model |
| E2E | 0 | 0 | Not applicable to this schema slice |
| **Total** | **41** | **3** | |

The four corrective cases are integration tests against real SQLite and production bootstrap code.

### Changed File Coverage

Coverage artifact: `C:/Users/Usuario/AppData/Local/Temp/sdd6-task12-base-definition-20260819/verify-v2-coverage/6321c241-5f8c-4325-b4de-7f2d9c46f11c/coverage.cobertura.xml`.

| File / remediation scope | Line % | Branch % | Uncovered lines | Rating |
|---|---:|---:|---|---|
| `src/ControlParental.Service/SqliteSchemaBootstrapper.cs` — latest 45 changed current lines, 40 executable | **40/40 = 100%** | **16/16 = 100%** | — | ✅ Excellent |

Test source is not counted as production coverage. No changed executable production line is uncovered.

### Assertion Quality

- No tautologies, assertions disconnected from production code, ghost loops, or mock-heavy tests were found.
- The four corrective cases call `SqliteSchemaBootstrapper.AdoptAsync`, assert the expected exception, and compare full database fingerprints.
- The fixed four-entry theory data is non-empty, so its assertions cannot silently skip.

**Assertion quality**: ✅ All corrective assertions verify real behavior.

### Quality Metrics

**Compiler/analyzers**: ✅ Service build completed with 0 errors.  
**Dependency warning**: ⚠️ Existing `NU1601`: requested `supabase-csharp >= 0.15.4`, resolved `0.16.0`.  
**Dedicated linter**: ➖ Not configured separately; .NET analyzers run through build.  
**Type checker**: ✅ C# compilation passed.

### Immutable Evidence and Delivery

| Check | Result |
|---|---|
| Corrective RED hash | ✅ `6FB6B1CEED0C3AAD9917141DA4EBE8329EB7F3EF9AC4EA8CDD97BC0DC2122C30` |
| Immutable focused GREEN hash | ✅ `290513D7CBEC046905B1AA7625CFAD2EB52E54F502A5282F06A9748B0888DC1C` |
| Immutable Service build hash | ✅ `3B1E0CCD8CE6400EB5E92979A8948D9917958BF59BB585569D99010310C5A786` |
| Immutable full regression hash | ✅ `1BAE12DFD86C72FFC434E1B2BC196E1E0E1F8C38B8A8F6E35B12C51A7A0493E4` |
| Patch hash | ✅ `34FC5A79C4BEBC464D017FC53A90CD15953D135341857BC4D6EF905B2AE7873F` |
| Pre-edit basis | ✅ Pre copies exist; in-memory unified-diff regeneration reproduces the current source/test files exactly |
| Pre/final bytes | ✅ Bootstrapper 13,723 → 15,471; tests 34,425 → 36,046 |
| Patch scope | ✅ Only `SqliteSchemaBootstrapper.cs` and `SchemaAdoptionTests.cs` |
| Review budget | ✅ 76 changed current code/test lines, ≤400 |
| Unrelated named-pipe test | ✅ Absent from remediation patch |

### Issues Found

**CRITICAL — task 1.2 slice**: None.  
**CRITICAL — full-change/archive readiness**: Ten implementation/final-verification tasks remain unchecked; the full change cannot be archived.  
**WARNING**:
1. Historical Round 1 standalone RED/GREEN chronology remains unavailable under the configured warning policy.
2. Tests live in `SchemaAdoptionTests.cs` rather than the literal `OutboxManagerTests.cs` path named by task 1.2.
3. The existing `NU1601` package-resolution warning remains.

**SUGGESTION**: None. Verification did not alter source, tests, or task checkboxes.

### Recommendation and Verdict

| Gate | Recommendation |
|---|---|
| Task 1.2 checkbox | Keep checked |
| Task 1.2 slice | **Approved** |
| Foundation 1.1 children | Remain delivery-open and unchecked |
| Full change | Not complete; tasks 2–4 also remain pending |
| Archive | Blocked |

## Final Verdict

**PASS WITH WARNINGS — task 1.2 only.** The prior exact-base-definition defect and current full-regression failure are both resolved by current evidence. Historical TDD chronology, test-file placement, and the package warning remain non-blocking for this slice; unchecked work still blocks approval of the complete change.

## Persistence

- OpenSpec: `openspec/changes/offline-sync-recovery/verify-report-task-1.2-approved-v2.md`
- Engram: `sdd/offline-sync-recovery/verify/task-1-2-approved-v2`
- Session: `sdd6-offline-sync-recovery-20260818`
- All prior verification reports preserved.
