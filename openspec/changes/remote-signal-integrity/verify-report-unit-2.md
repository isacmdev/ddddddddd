# Verification Report

**Change**: `remote-signal-integrity` — Unit 2 tasks 2.1–2.3 only  
**Boundary**: `feat/sdd7-2-wns-convergence`, exact parent/HEAD `e6ea7ceaf49c71edef9b9f3c7c3f71cb89a270e2`  
**Worktree**: `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-2`  
**Mode**: Strict TDD, hybrid persistence, fresh independent verification  
**Source mutation**: None; this report is the only created file  
**Verdict**: **FAIL**

Unit 2 is **not approved** for the local feature-chain boundary. The changed terminal-`Denied` reconciliation branch and legacy-production-registration removal are correct and pass fresh tests, but required scenario-4 lifecycle paths do not all have direct passing runtime proof: a new operation ID replacing a persisted terminal `Denied` intent is untested, and persisted-expiry/accepted-restart terminal behavior is only indirectly covered. The store-write-failure path is also uncovered and leaves the in-memory latest intent populated despite failed durability.

Full SDD7 remains partial. The task artifacts are cumulatively checked at 6/14, but independently approved scope remains Unit 1 only; SDD7 is not final-verified or archive-ready.

## Scope and Completeness

| Metric | Value |
|---|---:|
| Unit 2 tasks | 3 |
| Unit 2 checked | 3 |
| Unit 2 unchecked | 0 |
| Cumulative checked tasks | 6/14 |
| Unit 3–5 tasks | 8 unchecked; intentionally excluded |
| Normative scenario | `remote-signal-sync` scenario 4 only |

`tasks.md` and cumulative `apply-progress.md` retain Unit 1 evidence, add only 2.1–2.3 as newly checked, and leave 3.1–5.2 unchecked. Unit 1 was regression-tested through the full Service and App.UI suites, not re-judged.

## Workspace, Diff, and Boundary Audit

- CodeGraph was attempted first against the exact worktree and reported that no `.codegraph/` index exists. Verification then used direct source/artifact inspection; no index was created.
- Branch: `feat/sdd7-2-wns-convergence`.
- `HEAD`, merge-base, and required PR 2 parent all equal `e6ea7ceaf49c71edef9b9f3c7c3f71cb89a270e2`.
- Changed source/tests: exactly four expected files. Cumulative OpenSpec artifacts remain untracked.
- No Realtime, integrity, enforcement, or Unit 3+ source/test file changed. No assertion was deleted or weakened; the test diff is additive only.
- Fresh `git diff --check e6ea7cea...` exited 0 before and after execution. Git emitted only line-ending conversion warnings.

### Exact Changed-Line Budget

| File | Additions | Deletions | Total |
|---|---:|---:|---:|
| `src/ControlParental.Service/Program.cs` | 0 | 15 | 15 |
| `src/ControlParental.Service/WnsRegistrationCoordinator.cs` | 3 | 1 | 4 |
| `tests/ControlParental.Service.Tests/HostRegistrationTests.cs` | 18 | 0 | 18 |
| `tests/ControlParental.Service.Tests/UIMessageHandlerWnsTests.cs` | 18 | 0 | 18 |
| **CODE+TEST total** | **39** | **16** | **55** |

**Budget**: 55/400, 345 lines headroom. OpenSpec artifacts are excluded from the code/test review budget.

## Production Composition and Runtime Ownership

Production registration is centralized in `Program.ConfigureBackendIdentityServices`:

- `WnsRegistrationCoordinator` concrete singleton: once.
- `IWnsRegistrationCoordinator` alias to that singleton: once.
- `WnsRegistrationReconciliationService` hosted service: once and registered after `BackendIdentityStartupService`.
- `IPushNotificationService`: no production registration.
- `WnsNotificationServiceHostedAdapter`: no production hosted registration.

`ProgramCs_RegistersDurableWnsCoordinatorAndReconciliation_ExactlyOnce` and `ProgramCs_DoesNotRegisterLegacyWnsOwners` pass against current production source. `ProductionComposition_RestoresDefinitiveGenerationBeforeHostedRemoteWorkStarts` starts the helper-composed host and proves identity restoration precedes hosted remote work; the reconciliation service executes once. Full host/composition tests also pass. The legacy classes/files remain compiled and their isolated tests pass, but production DI cannot resolve or start them because their descriptors were removed. No second polling, renewal, transport, or retry owner is registered.

This is combined runtime/source composition evidence. There is no existing runtime test that enumerates the production `IServiceCollection` and asserts the exact WNS descriptor counts; exact absence/count is source-guarded.

## Fresh Build and Test Execution

All commands ran from the exact requested worktree using current assets and `--no-restore`.

| Evidence | Exact command | Start → end (`-05:00`) | Exit | Result |
|---|---|---|---:|---|
| Service product build | `dotnet build "src\ControlParental.Service\ControlParental.Service.csproj" --no-restore --configuration Debug --verbosity minimal` | `2026-08-20T16:20:53.0977677` → `16:20:54.7912382` | 0 | 0 errors, 1 existing NU1601 warning |
| Service test build | `dotnet build "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --configuration Debug --verbosity minimal` | `16:20:54.7932393` → `16:20:56.9675493` | 0 | 0 errors, 5 existing package warnings |
| Focused Unit 2 lifecycle/composition/host | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --no-restore --configuration Debug --verbosity minimal --filter "FullyQualifiedName~UIMessageHandlerWnsTests|FullyQualifiedName~HostRegistrationTests|FullyQualifiedName~ProductionBackendIdentityCompositionTests|FullyQualifiedName~ServiceCompositionTests|FullyQualifiedName~ServiceHostStartTests|FullyQualifiedName~WnsHostedServiceTests|FullyQualifiedName~WnsNotificationServiceTests"` | `16:21:07.1325302` → `16:21:09.6233351` | 0 | **78 passed, 0 failed, 0 skipped** |
| Full Service regression | `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --no-restore --configuration Debug --verbosity minimal` | `16:21:34.2478909` → `16:21:46.6416088` | 0 | **1,167 passed, 0 failed, 0 skipped** |
| Full App.UI regression | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-build --no-restore --configuration Debug --verbosity minimal` | `16:21:46.6436417` → `16:21:49.8587869` | 0 | **186 passed, 0 failed, 0 skipped** |
| Focused App.UI WNS lifecycle | `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-build --no-restore --configuration Debug --verbosity minimal --filter "FullyQualifiedName~WnsLifecycleTests"` | `16:23:36.4741014` → `16:23:38.7623167` | 0 | **19 passed, 0 failed, 0 skipped** |

The full Service runner emitted one pre-existing duplicate-test-ID notice for `HttpResponseClassifierTests`; VSTest still reported 1,167 executed, 0 skipped, and exit 0.

## Fresh Coverage Evidence

Command:

```text
dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --no-restore --configuration Debug --collect:"XPlat Code Coverage" --results-directory "tests\ControlParental.Service.Tests\TestResults\coverage-unit2-fresh-20260820-1622" --verbosity minimal
```

- Start: `2026-08-20T16:21:58.4297928-05:00`
- End: `2026-08-20T16:22:33.0005138-05:00`
- Exit: 0
- Tests: 1,167 passed, 0 failed, 0 skipped
- Artifact: `tests\ControlParental.Service.Tests\TestResults\coverage-unit2-fresh-20260820-1622\a0892518-55d0-42c1-87c1-aeb8fb6bc4cd\coverage.cobertura.xml`
- Aggregate: 14,442/26,804 lines (53.88%), 2,957/5,132 branches (57.61%).

### Changed File / Condition Coverage

| Scope | Line | Branch | Direct evidence | Rating |
|---|---:|---:|---|---|
| `WnsRegistrationCoordinator` primary class | 100% | 78% | Aggregate includes validation/helper conditions | Low aggregate branch rate |
| `ReconcileAsync` state machine | **100%** | **100%** | Changed `Accepted or Denied` terminal condition at lines 132–136 reports 10/10 conditions; revoked-restart test directly executes the new `Denied` branch | Excellent for changed condition |
| `RegisterAsync` state machine | 90.9% | 91.66% | Lines 114–115 (intent-store write failure) have 0 hits | Warning |
| `SendLatestAsync` state machine | 100% | 100% | Identity and all typed remote-status branches execute | Excellent |
| `WnsRegistrationReconciliationService` | 100% | 100% | Constructor, one-shot `StartAsync`, and `StopAsync` execute | Excellent |
| `Program.cs` registration removals | N/A | N/A | Deletions have no sequence points; proved by source count/absence plus host startup/composition tests | Composition evidence |

The changed coordinator condition itself is directly covered. Aggregate class branch coverage below 80% is not treated as sufficient by itself. The uncovered store-write-failure branch is called out because it affects the durable-intent requirement even though that branch was not introduced by Unit 2.

## Scenario-4 Lifecycle Compliance Matrix

| Required path | Source evidence | Fresh passing test evidence | Result |
|---|---|---|---|
| One bounded/redacted durable intent | URI/op/expiry bounds at lines 181–193; one latest intent and bounded protected envelope | valid, invalid URI/channel, invalid expiry, oversized/redaction, and SecretStore-backed registration tests in focused 78 | ⚠️ PARTIAL — store-write failure is untested and leaves `latest` populated |
| Definitive identity gate | `SendLatestAsync` lines 149–152 | `NonDefinitiveIdentity_IsDeniedButIntentRemainsPersisted` (two phases); production identity host start | ✅ COMPLIANT |
| Pending/offline persists and restarts once | pending write before send; retryable/offline status persisted; one-shot reconciliation hosted service | `RetryableFailure_IsPersistedPendingWithoutErrorBodyDisclosure`; `RestartReconcile_ReplaysLatestIntentOnce_WithoutPolling` | ✅ COMPLIANT |
| Revoked/forbidden becomes terminal `Denied` | typed mapping lines 161–166; changed reconcile guard lines 132–136 | typed failure theory plus `RestartReconcile_DoesNotReplayRevokedIntent` | ✅ COMPLIANT |
| A genuinely new operation replaces terminal `Denied` | different operation ID bypasses same-ID guard and assigns a new pending intent at lines 105–118 | App.UI proves separate invocations generate distinct IDs, but no test drives persisted `Denied` → new operation → backend acceptance | ❌ UNTESTED |
| `Accepted` remains terminal | same-ID idempotency guard and reconciliation terminal guard | `Duplicate_IsIdempotent_AndConflictingReuseIsDenied` proves same-ID accepted idempotency/no second backend call | ⚠️ PARTIAL — no accepted-intent restart/reconcile test |
| Expiry/renewal deterministic | registration expiry bounds and reconciliation expiry guard; App.UI renewal policy | invalid-expiry theory and 19/19 App.UI lifecycle tests cover renewal windows/expired channel | ⚠️ PARTIAL — no persisted intent that expires before reconciliation is exercised |
| Conflicting operation reuse deterministic | same operation ID requires exact same intent | `Duplicate_IsIdempotent_AndConflictingReuseIsDenied` | ✅ COMPLIANT |
| Cancellation bounded | gate/backend cancellation propagates; no coordinator retry | `Cancellation_PropagatesWithoutRetryOrSuccess`; App.UI propagation test | ✅ COMPLIANT |
| No secret/body/URI disclosure | result contains operation/status/hash only; coordinator has no logging | valid URI, oversized secret, retry body, and typed remote body redaction assertions | ✅ COMPLIANT |
| Retry bounded/no overlap | retry remains in `BackendClient`, not coordinator/reconciliation | timeout sends once; transient retry uses two attempts and one stable idempotency key | ✅ COMPLIANT |
| Single production owner | one durable coordinator/reconciliation registration; legacy registrations removed | source registration tests, production host start, full host/composition regression | ✅ COMPLIANT locally |
| External receipts | no live WNS/backend evidence claimed | artifacts explicitly keep receipts pending | ✅ COMPLIANT boundary |

**Scenario-4 result**: **FAILING**. The broad scenario has strong passing fragments, but the explicitly normative new-operation-after-terminal-denial lifecycle has no covering runtime test. Static reachability is not runtime compliance.

## Correctness and Design Coherence

| Decision / invariant | Status | Evidence |
|---|---|---|
| Durable coordinator retained | ✅ | Concrete and interface singleton registrations remain once |
| Legacy WNS owner quarantined | ✅ | Production descriptors removed; classes and isolated tests retained |
| Identity startup before reconciliation | ✅ | Registration order plus passing production host-start test |
| One non-polling reconciliation | ✅ | Hosted service calls `ReconcileAsync` once and creates no timer |
| No transport/retry duplication | ✅ | Coordinator calls `IBackendClient` once; backend owns bounded retry |
| Unit 1 scheduler/IPC preserved | ✅ | Full Service 1,167/1,167 and App.UI 186/186 |
| New operation can replace denied | ⚠️ Static only | Source permits it, but no covering runtime lifecycle test |
| Durable write failure remains coherent | ❌ | Failed `WriteAsync` returns `Denied` but leaves in-memory `latest`; same-operation retry can return `PendingOffline` without durable persistence |
| Unit 3+ isolation | ✅ | No Unit 3+ source/test changes |

## Strict TDD Audit

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| Six-column evidence reported | ✅ | Unit 2 table contains RED, GREEN, TRIANGULATE, SAFETY NET, REFACTOR |
| All Unit 2 tasks map to tests | ✅ | 3/3; two changed test files exist; three new tests were added |
| RED evidence | ⚠️ | Apply progress records the two exact failing assertions, but no durable raw RED log/path is available to this verification; none is fabricated |
| GREEN confirmed fresh | ✅ | Focused 78/78, Service 1,167/1,167, App.UI 186/186, WNS lifecycle 19/19 |
| Triangulation adequate | ❌ | No test covers terminal `Denied` followed by a genuinely new operation; accepted-restart and persisted-expiry are indirect only |
| Safety net | ⚠️ | Current full suites pass; pre-change execution was blocked until restore, so no successful pre-change safety-net run exists |
| Refactor/scope claim | ✅ | Minimal condition change and registration deletions; no Unit 3+ leakage |

**TDD compliance**: current GREEN is real, but lifecycle triangulation is incomplete and raw historical RED output was not retained as an artifact.

### Test Layer Distribution

| Layer | Changed-file cases | Files | Notes |
|---|---:|---:|---|
| Unit / in-memory harness | 24 | 1 | Coordinator, backend retry, cancellation, restart |
| Source-level composition | 10 | 1 | Production registration counts/absence plus descriptor behavior |
| Integration/host | 0 newly changed | 0 | Existing production identity host and host-start tests were included in focused 78 |
| E2E | 0 | 0 | External WNS/backend receipts intentionally out of scope |

Three test cases were newly added: two source-composition guards and one revoked-restart lifecycle test.

### Assertion Quality

**Result**: ✅ No tautology, ghost loop, production-free assertion, smoke-only assertion, or assertion weakening was found in the Unit 2 additions. The new revoked test asserts both null result and zero restored-backend calls; composition tests assert exact counts and absence.

## Dependency-Restore Audit

- The initial apply safety-net could not run because linked-worktree `project.assets.json` was absent (`NETSDK1004`). Apply then performed a non-source restore.
- Current assets exist. Their last-write times predate this fresh verification: Service Tests `2026-08-20T16:10:40.1708564-05:00`; App.UI Tests `2026-08-20T16:13:55.1492593-05:00`.
- All fresh builds/tests/coverage used `--no-restore`; asset timestamps did not need to change.
- `git diff --quiet e6ea7cea...` across all tracked `*.csproj`, `packages.lock.json`, `Directory.Build.props`, and `Directory.Packages.props` exited 0. No tracked dependency/config mutation or package-lock drift exists.
- Existing NU1601/NU1701 compatibility/version warnings remain. No new package reference or lock-file change was introduced.

**Policy assessment**: the restore was a deviation from a strict “no worktree mutation at all” interpretation because it created/updated ignored `obj` assets, but it did not mutate tracked source, configuration, dependencies, branch history, or package locks. Fresh verification successfully executed from those current assets without restore.

## Issues Found

### CRITICAL

1. **Required new-operation-after-terminal-`Denied` lifecycle is untested.** Source permits replacement, but no passing test starts from persisted revoked/forbidden `Denied`, registers a genuinely new operation ID, and proves one normal backend registration. Under spec-driven verification this path is `UNTESTED`.
2. **Durable write-failure behavior is neither covered nor coherent with the durable-intent invariant.** `RegisterAsync` sets `latest` before persistence; when `WriteAsync` returns false, it returns `Denied` without clearing `latest`. A same-operation retry can therefore report `PendingOffline` without durable storage. Coverage confirms lines 114–115 have zero hits.

### WARNING

1. Accepted terminality after restart and persisted-expiry reconciliation are not directly tested; current evidence is split across accepted idempotency, input expiry validation, App.UI renewal policy, source, and aggregate coverage.
2. Historical Unit 2 RED output is summarized but no durable raw log/path is available; this report does not claim one.
3. The pre-change safety net did not pass before implementation because assets were missing. Only post-restore safety nets are green.
4. Exact production WNS descriptor counts/legacy absence are source-guarded rather than asserted by runtime enumeration of the production service collection; host startup and reconciliation execution provide complementary runtime evidence.
5. Existing package warnings and the duplicate xUnit test-ID notice remain outside Unit 2 changes.

### SUGGESTION

None. Verification is report-only and does not prescribe or perform remediation.

## Verdict and Eligibility

**FAIL**

Unit 2 is **not approved** for the local feature-chain boundary. No commit, Unit 3 preparation, push, PR, archive, dependency change, or source/test fix was performed.

Because Unit 2 is not approved, the next eligible action is a separately user-authorized Unit 2 remediation followed by fresh verification. The approval-only next actions named in the request—a local Unit 2 code/test commit or Unit 3 preparation after preserving the boundary—are **not yet eligible**.
