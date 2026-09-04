# Verification Report

**Change**: `remote-signal-integrity` — Unit 2 tasks 2.1–2.3 only  
**Boundary**: branch `feat/sdd7-2-wns-convergence`; HEAD/base/PR-2 parent `e6ea7ceaf49c71edef9b9f3c7c3f71cb89a270e2`; parent branch `feat/sdd7-1-scheduler-ipc`  
**Mode**: Strict TDD, hybrid persistence, report-only source policy  
**Verification**: authoritative fresh independent re-verification after durability-ordering and lifecycle remediation  
**CodeGraph**: attempted first on the exact worktree; `.codegraph/` was absent, so verification used direct inspection of all changed code/tests and all SDD artifacts. No index was initialized and no source/configuration was changed.

## Verdict

**PASS WITH WARNINGS**

Unit 2 is **approved for its local feature-chain boundary**. All required local durability, lifecycle, composition, regression, and changed-method coverage evidence passed. The warnings are evidence/tooling limitations, not behavior gaps: historical pre-change raw RED/safety-net logs are unavailable; the default embedded-PDB collector produced empty reports before a fresh portable-PDB command-line build produced valid coverage; and the repository retains existing build/analyzer warnings plus one duplicate xUnit test-ID notice.

Full SDD7 remains **partial at 6/14 tasks**. It is not final-verified or archive-ready. Units 3–5 and external WNS/backend receipts remain excluded and unclaimed.

## Scope and Completeness

| Metric | Result |
|---|---:|
| Unit 2 tasks | 3/3 complete (`2.1`–`2.3`) |
| Cumulative SDD7 tasks | 6/14 complete (`1.1`–`2.3`) |
| Unit 3+ tasks | 0 verified; excluded |
| Changed tracked files | 4 |
| Changed CODE+TEST lines | 143 additions + 18 deletions = **161** |
| Review budget | **161/400**, compliant; 239 lines below limit |
| Diff whitespace check | exit 0 |
| Dependency/config drift | none |

Exact `git diff --numstat e6ea7ce...`:

```text
0   15  src/ControlParental.Service/Program.cs
11  3   src/ControlParental.Service/WnsRegistrationCoordinator.cs
18  0   tests/ControlParental.Service.Tests/HostRegistrationTests.cs
114 0   tests/ControlParental.Service.Tests/UIMessageHandlerWnsTests.cs
```

Only these four tracked CODE+TEST files differ from the parent. The untracked `openspec/changes/remote-signal-integrity/` tree is cumulative planning/evidence material and is excluded from the code-review budget. No Unit 3+ source/test file changed.

## Fresh Build and Test Evidence

All commands ran in the exact Unit 2 worktree. Every build/test/coverage command used `--no-restore`; no restore occurred during this verification.

| Start (UTC-05:00) | Command | Exit | Result |
|---|---|---:|---|
| 2026-08-20T16:40:20.0978933 | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --configuration Debug --verbosity minimal` | 0 | 0 errors, 1 existing package warning |
| 2026-08-20T16:40:27.9754885 | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --configuration Debug --verbosity minimal` | 0 | 0 errors, 5 existing package warnings |
| 2026-08-20T16:40:38.7526675 | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~UIMessageHandlerWnsTests|FullyQualifiedName~HostRegistrationTests.ProgramCs_RegistersDurableWnsCoordinatorAndReconciliation_ExactlyOnce|FullyQualifiedName~HostRegistrationTests.ProgramCs_DoesNotRegisterLegacyWnsOwners|FullyQualifiedName~ServiceCompositionTests|FullyQualifiedName~ServiceHostStartTests|FullyQualifiedName~WnsHostedServiceTests|FullyQualifiedName~WnsNotificationServiceTests"` | 0 | **72 passed**, 0 failed, 0 skipped |
| 2026-08-20T16:46:46.0903468 | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~ProductionBackendIdentityCompositionTests.ProductionComposition_RestoresDefinitiveGenerationBeforeHostedRemoteWorkStarts|FullyQualifiedName~HostRegistrationTests.ProgramCs_RegistersDurableWnsCoordinatorAndReconciliation_ExactlyOnce|FullyQualifiedName~HostRegistrationTests.ProgramCs_DoesNotRegisterLegacyWnsOwners"` | 0 | **3 passed**, 0 failed, 0 skipped |
| 2026-08-20T16:40:49.4114912 | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | 0 | **1,171 passed**, 0 failed, 0 skipped; one existing duplicate-ID discovery notice |
| 2026-08-20T16:41:09.8009350 | `dotnet build tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --configuration Debug --verbosity minimal` | 0 | 0 errors; existing warning corpus |
| 2026-08-20T16:41:44.0810273 | `dotnet test tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal` | 0 | **186 passed**, 0 failed, 0 skipped |
| 2026-08-20T16:44:26.3765448 | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --configuration Debug -p:DebugType=portable --collect:"XPlat Code Coverage" --results-directory tests\ControlParental.Service.Tests\TestResults\coverage-unit2-final-reverify-portable-20260820-1645 --verbosity minimal` | 0 | **1,171 passed**, valid non-empty coverage |
| 2026-08-20T16:46:02.4367944 | `git diff --check e6ea7ceaf49c71edef9b9f3c7c3f71cb89a270e2` | 0 | no whitespace errors |

Coverage artifact:

```text
tests\ControlParental.Service.Tests\TestResults\coverage-unit2-final-reverify-portable-20260820-1645\3eddf634-9858-4728-a86e-229389ef71b8\coverage.cobertura.xml
```

- Size: **4,782,010 bytes**.
- Overall Service assembly: 14,450/26,810 lines, **53.89% line**, **57.63% branch**. No whole-assembly threshold is specified for this Unit.
- Two earlier fresh collector attempts ran 1,171/1,171 tests but produced 235-byte reports with zero valid lines under the repository's embedded-PDB Debug setting. They are explicitly rejected as evidence. The accepted run rebuilt from current assets with command-line-only portable PDBs and still used `--no-restore`; no project/config file changed.

## Durability and Lifecycle Invariants

| Invariant | Runtime evidence | Source evidence | Result |
|---|---|---|---|
| Candidate is durable before becoming `latest` | `FailedIntentWrite_DoesNotPublishPhantom_AndSameOperationRetriesDurably` passed | `RegisterAsync` writes local `candidate` at lines 112–121, then assigns `latest` at line 123 | ✅ COMPLIANT |
| Failed write creates no phantom authority; same operation genuinely retries | Same test asserts first `Denied`, null store, retry `Accepted`, 3 writes, and exactly 1 backend send | Failed write returns before publication/backend; retry is not caught by same-operation idempotency | ✅ COMPLIANT |
| Terminal `Denied` survives restart without replay and a new operation replaces it normally | `TypedRemoteFailure_IsRedactedAndFailClosed(Revoked, Denied)`, `RestartReconcile_DoesNotReplayRevokedIntent`, and `RestartReconcile_DeniedIntentCanBeReplacedByNewOperation` all passed | `SendLatestAsync` persists `Denied`; `ReconcileAsync` skips it; `RegisterAsync` permits a distinct operation ID | ✅ COMPLIANT |
| Persisted `Accepted` remains terminal | `RestartReconcile_DoesNotReplayAcceptedIntent` passed with no restored-backend call | `ReconcileAsync` terminal guard includes `Accepted` | ✅ COMPLIANT |
| Persisted expired intent behavior is deterministic | `RestartReconcile_DoesNotSendPersistedExpiredIntent` passed with null result and no backend call | `ReconcileAsync` returns null when `ExpiresAt <= now` | ✅ COMPLIANT to current local contract |

The expired-intent result is precisely: **the persisted expired intent is not replayed by reconciliation**. The implementation does not delete or renew it in this path; a later valid registration operation must replace it. This report makes no live-renewal, WNS-delivery, or external convergence claim.

## Remote-Signal-Sync Scenario 4 Path Matrix

Requirement: one redacted, bounded, idempotent durable registration intent converges only after identity authorization across renewal/expiry/revocation/offline/restart lifecycle changes.

| Path | Passing runtime test(s) | Source path | Status |
|---|---|---|---|
| Valid authorized registration | `AuthenticatedRequest_ValidPayload_UsesDefinitiveBackendAndRedactsResult` | validate → durable pending write → backend → persisted `Accepted` | ✅ COMPLIANT |
| URI/channel/expiry/input bounds | `InvalidUriOrChannel_IsDeniedWithoutBackendCall`; `InvalidExpiry_IsDenied`; `OversizedPayload_IsDeniedAndSecretNeverAppearsInResult` | `IsValid` bounds operation, HTTPS WNS host, channel, URI length, and 30-day expiry | ✅ COMPLIANT |
| Definitive identity gate | `NonDefinitiveIdentity_IsDeniedButIntentRemainsPersisted` (two identity phases) | pending intent remains durable; `SendLatestAsync` makes no backend call without authorization | ✅ COMPLIANT |
| Offline pending replay once | `RetryableFailure_IsPersistedPendingWithoutErrorBodyDisclosure`; `RestartReconcile_ReplaysLatestIntentOnce_WithoutPolling` | transient maps to `PendingOffline`; restart loads and sends once | ✅ COMPLIANT |
| Revoked/forbidden terminality | `TypedRemoteFailure_IsRedactedAndFailClosed`; `RestartReconcile_DoesNotReplayRevokedIntent` | typed failures map to persisted `Denied`; reconciliation skips terminal state | ✅ COMPLIANT |
| Denied A → restart/no replay → accepted B replacement | `RestartReconcile_DeniedIntentCanBeReplacedByNewOperation` plus typed-denial assertion | distinct operation bypasses same-operation guard, persists candidate and final `Accepted` | ✅ COMPLIANT |
| Accepted terminal restart | `RestartReconcile_DoesNotReplayAcceptedIntent` | `Accepted` guard returns null | ✅ COMPLIANT |
| Expired persisted intent | `RestartReconcile_DoesNotSendPersistedExpiredIntent` | expiry guard returns null, no send | ✅ COMPLIANT to current contract |
| Durable-write failure and same-operation retry | `FailedIntentWrite_DoesNotPublishPhantom_AndSameOperationRetriesDurably` | durable-before-publish ordering | ✅ COMPLIANT |
| Idempotent duplicate/conflicting operation reuse | `Duplicate_IsIdempotent_AndConflictingReuseIsDenied` | same operation+same intent returns stored status; changed intent is denied | ✅ COMPLIANT |
| Cancellation ownership | `Cancellation_PropagatesWithoutRetryOrSuccess` | cancellation propagates through the one backend call | ✅ COMPLIANT |
| Redaction | authorized, oversized, retryable, and typed-failure tests | result contains operation/correlation/status, not URI, credential, or backend body | ✅ COMPLIANT |
| Stable bounded retry/idempotency key | `BackendTimeout_IsFiniteAndDoesNotOverlapAttempts`; `BackendTransientRetry_IsBoundedAndReusesIdempotencyKey` | retry remains in `BackendClient`, not coordinator | ✅ COMPLIANT |
| Exactly one startup reconciliation path | `ProductionComposition_RestoresDefinitiveGenerationBeforeHostedRemoteWorkStarts`; host-registration tests; valid coverage of reconciliation service `StartAsync`/`StopAsync` | one hosted `WnsRegistrationReconciliationService`; `StartAsync` invokes one `ReconcileAsync` | ✅ COMPLIANT |

**Scenario 4 compliance**: all required local paths are covered by passing runtime tests. External WNS/backend behavior is outside this proof.

## Changed-Method Coverage

Cobertura represents async methods as generated state-machine `MoveNext` methods.

| Scope | Line rate | Branch rate | Assessment |
|---|---:|---:|---|
| `WnsRegistrationCoordinator.RegisterAsync` | 100% | 100% | all changed executable branches covered |
| `WnsRegistrationCoordinator.ReconcileAsync` | 100% | 100% | terminal/null/expiry/send paths covered |
| `WnsRegistrationCoordinator.SendLatestAsync` | 100% | 100% | identity and typed backend-result paths covered |
| `WnsRegistrationReconciliationService.StartAsync` | 100% | 100% | host startup executes reconciliation |
| `WnsRegistrationReconciliationService.StopAsync` | 100% | 100% | covered |
| Coordinator class aggregate | 100% | **78%** | warning only: uncovered branches are in unchanged helper/constructor branches; changed executable methods are 100%/100% |

The aggregate 78% coordinator branch rate is below 80%, but it does not hide a changed behavior gap. Changed-scope executable branch coverage is adequate and directly triangulated by the focused tests.

## Composition and Design Coherence

| Decision/invariant | Evidence | Result |
|---|---|---|
| One durable coordinator owner | production registers concrete coordinator once and maps `IWnsRegistrationCoordinator` to that same singleton | ✅ |
| One reconciliation owner | exactly one `AddHostedService<WnsRegistrationReconciliationService>()`; host startup test passed | ✅ |
| Legacy production owners removed | `Program.cs` has no `AddSingleton<IPushNotificationService>` and no legacy hosted-adapter registration | ✅ |
| Legacy classes retained for tests/seams | `WnsNotificationServiceTests` and `WnsHostedServiceTests` compile and pass; class files were not deleted | ✅ |
| Backend owns transport retry | coordinator performs one backend call; bounded retry/idempotency-key tests cover `BackendClient` | ✅ |
| Unit 1 not regressed | full Service 1,171/1,171 and App.UI 186/186 passed; no Unit 1 source/test file changed | ✅ |
| Unit 3+ excluded | changed tracked paths are limited to Unit 2 coordinator/composition and their tests | ✅ |
| External receipts unclaimed | proposal/design/report retain contract-first boundary | ✅ |

## Strict TDD Audit

The cumulative `apply-progress.md` contains the required six evidence columns (`RED`, `GREEN`, `TRIANGULATE`, `SAFETY NET`, `REFACTOR`, plus task/cycle identity). Updated remediation rows cover 2.1 and 2.2; the earlier cumulative table retains 2.3 evidence.

| Task | RED | GREEN | Triangulate | Safety net | Refactor | Audit |
|---|---|---|---|---|---|---|
| 2.1 | durability-ordering and earlier revoked/composition failures reported; raw logs not retained | remediation/lifecycle tests pass | failed write, retry, terminal states, bounds, identity, retry ownership | fresh Service/App.UI/build/coverage green | durable-before-publish only | ⚠️ complete behavior evidence; historical raw RED warning |
| 2.2 | terminal-denial and legacy-owner failures reported | lifecycle/composition tests pass | offline, denied, accepted, expiry, replacement, exactly-one owner | fresh full regressions green | no new transport owner | ⚠️ complete behavior evidence; historical raw RED warning |
| 2.3 | no fabricated additional RED | all fresh verification commands pass | full scenario-4 matrix covered | no-restore builds/tests and valid coverage | no Unit 3+ leakage | ✅ |

### Restore/Dependency Audit

- Historical apply evidence states the earlier linked-worktree baseline initially lacked `project.assets.json` and required a non-source restore.
- Current diff contains no `.csproj`, lock file, `Directory.Build.*`, or `global.json` change; dependency drift is absent.
- This fresh verification performed **no restore**. Every `dotnet` build/test/coverage command explicitly used `--no-restore`; `--no-build` was additionally used where appropriate.
- Missing historical raw pre-change logs remain a warning, as authorized. No current behavior gap was found.

## Test Quality and Layers

| Layer | Changed tests | Evidence |
|---|---:|---|
| Unit / state-machine harness | 5 new lifecycle/durability tests | real coordinator, controlled durable stores, strict backend/identity mocks |
| Integration/composition | 2 new source-composition tests, plus existing production host startup | exact registration counts/absence and hosted startup path |
| E2E/external | 0 | correctly excluded; no external receipt claim |

**Assertion quality**: ✅ no tautologies, ghost loops, smoke-only assertions, orphan type-only checks, or assertions that avoid production code were found in changed tests. Backend/write call counts are meaningful ownership and durability invariants, not incidental implementation checks.

## Quality Metrics

- Compiler/type analysis: ✅ builds completed with 0 errors.
- Analyzer/package warnings: ⚠️ existing repository warning corpus; no changed-scope compile error.
- `git diff --check`: ✅ exit 0.
- Line-ending notices: informational LF→CRLF working-copy warnings only.
- xUnit: ⚠️ one existing duplicate test-ID discovery notice in `HttpResponseClassifierTests`; 1,171 tests still executed and passed.

## Issues

### CRITICAL

None.

### WARNING

1. Historical pre-change RED and initial safety-net raw logs were not preserved. Current runtime behavior is fully green, but strict-TDD chronology cannot be independently reconstructed from raw artifacts.
2. The default embedded-PDB coverage attempts produced structurally empty files and were rejected. Valid coverage required a command-line-only `-p:DebugType=portable` build; no project configuration changed.
3. Existing package/analyzer warning corpus and one duplicate xUnit test-ID notice remain outside Unit 2's changed scope.
4. Coordinator aggregate branch coverage is 78%; all changed executable methods are nevertheless 100% line/branch covered, so this is non-blocking.

### SUGGESTION

Preserve raw RED/safety-net logs for subsequent units and use a documented portable-PDB coverage invocation to avoid empty collector artifacts.

## Approval Boundary and Next Eligibility

Unit 2 is approved only for the local PR-2 feature-chain boundary over `feat/sdd7-1-scheduler-ipc` at `e6ea7ceaf49c71edef9b9f3c7c3f71cb89a270e2`.

If separately authorized by the user, the next eligible action is a local Unit 2 CODE+TEST commit, followed by Unit 3 preparation from that exact commit. Neither action was performed here.
