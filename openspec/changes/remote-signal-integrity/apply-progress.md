# Apply Progress: Remote Signal Integrity (SDD7)

### C1C Closure — 2026-08-27

- Task `4.3 C1C` closed from commit `b3011a2` after final independent **PASS WITH WARNINGS**: DeadlineOwner `14/14`, handler deadline `6/6`, lifecycle/fault `16/16`, AntiTamper `112/112`, and exact-exclusion safety `1313/1313`.
- Changed production coverage: `79/79` lines and `60/72` branches (`83.33%`); CODE+TEST `353/400`. Deadline ownership remains a monotonic, generation-scoped one-shot timer: persisted UTC due is authoritative, stale callbacks are rejected, and trust/Stop/Dispose cancel or invalidate prior timers without extension or duplicate effects.
- Non-blocking warning documented: callback-race Strict-TDD chronology remained partial; no fabricated RED was claimed. C2 must base on `b3011a2` and requires explicit start authorization.

## Scope

- Work unit: Unit 1 — Scheduler admission and `TriggerSync`
- Delivery strategy: feature-branch-chain
- Branch: `feat/sdd7-1-scheduler-ipc`
- Review boundary: scheduler admission, authenticated IPC routing, typed WNS hint emission, and focused regression evidence
- Changed-line budget: 388 additions+deletions (317 tracked insertions + 47 deletions + 24 untracked helper lines), including the untracked `tests/ControlParental.App.UI.Tests/RepositoryRootLocator.cs`.
- Budget accounting note: tracked `git diff --numstat` reports `317 additions + 47 deletions = 364` and omits the 24-line untracked helper; exact total is `317 + 47 + 24 = 388`.

## Completed Tasks

### 1.1 RED

- Added tests covering bounded admission types, typed `TriggerSync`, authenticated versus unauthenticated IPC, malformed WNS payload opacity, coalescing, cancellation, and generated JSON metadata.
- Original implementation RED: the pre-implementation test/build attempt failed at compilation because `IScheduledWorkService.AdmitSyncAsync`, `SyncTriggerSource`, and `SyncAdmissionResult` did not yet exist.
- Remediation RED command (actual):

  ```text
  dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --verbosity minimal
  ```

  - Timestamp: `2026-08-20T15:20:31.5769243-05:00` (command start).
  - Exit code: `1`.
  - Failure: `ControlParental.Service.Tests.ScheduledWorkServiceAsyncDispatchTests.AdmitSyncAsync_ShutdownCancellationCancelsSharedWork`.
  - Message: `System.TimeoutException : The operation has timed out.` at `ScheduledWorkServiceAsyncDispatchTests.cs:635`.
  - Result: `1 failed, 1,164 passed, 0 skipped, 1,165 total`.
  - Raw output: the terminal output was observed in the remediation session but no durable raw-output artifact/path was preserved. No raw artifact is claimed here.

### 1.2 GREEN

- Added `SyncTriggerSource`, `SyncAdmissionResult`, and `AdmitSyncAsync` to the existing scheduler contract.
- Routed authenticated `TriggerSync` through `UIMessageHandler` to the singleton scheduler; unauthenticated requests are rejected.
- Implemented scheduler-owned policy-sync admission with bounded source validation, cancellation handling, running/disposed guards, and single-flight coalescing.
- Preserved WNS payload opacity: `WnsPushNotificationHandler` emits only the parameterless typed `TriggerSync` message.

### 1.3 REFACTOR / VERIFY

- Consolidated the redundant two-fragment admission coalescing tests into one complete scenario-2 sequence, preserving all-five-source cardinality assertions while reducing the review budget. No production behavior changed.
- Complete scenario-2 sequence: start with an authorized service and complete startup sync; switch identity to `BackendIdentityState.Unpaired` and admit `Wns`, proving no repository/backend synchronization occurs; restore a definitive identity; admit `Startup`, `Wns`, `Ui`, `Timer`, and `Polling` concurrently; hold repository entry with a barrier; release it; assert exactly one `Accepted`, four `Coalesced`, and exactly one backend fetch.
- This sequence passed against the existing production implementation. No new production RED was fabricated; the result is triangulation/evidence closure of the previously missing complete sequence.

- Fresh complete-sequence GREEN command:

  ```text
  dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AdmitSyncAsync_DeniedIdentityThenLaterAdmissionConvergesAcrossAllSources"
  ```

  - Start: `2026-08-20T15:43:38.8484800-05:00`.
  - Exit code: `0`.
  - Result: `1 passed, 0 failed, 0 skipped, 1 total`.

- Fresh focused Unit 1 command:

  ```text
  dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~ScheduledWorkServiceAsyncDispatchTests.AdmitSyncAsync|FullyQualifiedName~UIMessageHandlerWnsTests.AuthenticatedTriggerSync|FullyQualifiedName~UIMessageHandlerWnsTests.UnauthenticatedTriggerSync|FullyQualifiedName~UIMessagesJsonContextTests.JsonSerializer_RoundTrip_AllSampleEnvelopes_PreservesData_ViaSourceGen"
  ```

  - Start: `2026-08-20T15:43:29.5534993-05:00`.
  - Exit code: `0`.
  - Result: `9 passed, 0 failed, 0 skipped, 9 total`.

- Fresh affected Service test build:

  ```text
  dotnet build "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --configuration Debug --verbosity minimal
  ```

  - Start: `2026-08-20T15:43:10.9308052-05:00`.
  - Exit code: `0`.
  - Result: `0 errors`, existing warning corpus.

- Fresh full Service command:

  ```text
  dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --verbosity minimal
  ```

  - Start: `2026-08-20T15:43:51.6268520-05:00`.
  - Exit code: `0`.
  - Result: `1,164 passed, 0 failed, 0 skipped, 1,164 total` (new count after consolidating two redundant tests).

- Fresh full App.UI command:

  ```text
  dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-build --configuration Debug --verbosity minimal
  ```

  - Start: `2026-08-20T15:43:51.6348478-05:00`.
  - Exit code: `0`.
  - Result: `186 passed, 0 failed, 0 skipped, 186 total`.

- Fresh coverage command:

  ```text
  dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-build --configuration Debug --collect:"XPlat Code Coverage" --results-directory "tests\ControlParental.Service.Tests\TestResults\coverage-unit1-scenario2-20260820-1544" --verbosity minimal
  ```

  - Start: `2026-08-20T15:44:12.8627382-05:00`.
  - Exit code: `0`.
  - Result: `1,164 passed, 0 failed, 0 skipped, 1,164 total`.
  - Raw artifact: `tests\ControlParental.Service.Tests\TestResults\coverage-unit1-scenario2-20260820-1544\b1cf8161-406d-4617-b6b6-6622e0241e5c\coverage.cobertura.xml`.
  - `ScheduledWorkService.cs`: line-rate `87.08%`; branch-rate `85.93%`.

- Fresh `git diff --check` passed with exit code `0` at `2026-08-20T15:46:55.4808109-05:00`; the untracked-helper `git diff --no-index --check` emitted no whitespace diagnostics and returned its expected difference exit `1`.

- Prior remediation evidence retained below remains cumulative; the earlier shutdown-cancellation test barrier is unchanged.

- Earlier focused `46/46` and full Service `1,165/1,165` evidence remains part of the cumulative history; current results are recorded above after consolidation.

## TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 1.1 — Contract and acceptance tests | PASS — original compile-failure evidence established missing `AdmitSyncAsync`, `SyncTriggerSource`, and `SyncAdmissionResult`; original raw log was not retained. | PASS — contract and tests compile after implementation. | PASS — bounded sources, typed parameterless `TriggerSync`, authentication routing, opacity, coalescing, cancellation, and generated JSON assertions align with Unit 1 scenarios. | PASS — fresh full Service `1,164/1,164` and App.UI `186/186`. | PASS — no adapter authority, payload-derived authority, or second transport path introduced. |
| 1.2 — Scheduler and authenticated IPC implementation | PASS — missing interface/types identified by the original RED compile failure; original raw log was not retained. | PASS — fresh focused Unit 1 `9/9` and complete scenario-2 sequence `1/1`. | PASS — scheduler-owned admission, bounded validation, identity gating, single-flight coalescing, caller/shutdown cancellation ownership, and later convergence were exercised. | PASS — fresh full Service `1,164/1,164`, App.UI `186/186`, and coverage `87.08%` line / `85.93%` branch. | PASS — implementation remains within the existing scheduler/IPC architecture. |
| 1.3 — Remediation and verification | PASS — prior shutdown-cancellation timeout was real; current scenario-2 test had no production RED because existing production passed the complete sequence. | PASS — scenario 2 `1/1`; focused `9/9`; full Service `1,164/1,164`; full App.UI `186/186`; coverage exited `0`. | PASS — identity-denied admission made no repository/backend call, definitive identity restoration enabled a later five-source admission, and exactly one fetch converged. | PASS — Cobertura artifact preserved; affected build `0` errors; `git diff --check` exited `0`; budget `388`. | PASS — consolidated redundant fragment tests without weakening all-five-source, caller-cancellation, shutdown, guard, polling, identity, or retry-ownership coverage; no production source changed. |

## Historical Evidence Limitations

- The original Unit 1 RED and original safety-net execution evidence were not preserved as raw terminal artifacts. Their summaries remain honest and are not presented as timestamped raw logs.
- The remediation RED terminal output was observed during the session, but no durable raw-output path was preserved; only the exact command, retained timestamp, exit code, failing test, message, and result count are recorded above.
- Current focused/full/build/coverage/diff-check timestamps are recorded where captured above; no timestamps are fabricated.
- `openspec/changes/remote-signal-integrity/verify-report-unit-1.md` is the historical FAIL report and remains unchanged.

## Task State Confirmation

- `tasks.md`: `1.1`, `1.2`, and `1.3` remain checked.
- Historical Unit 1 confirmation: all later phases were unchecked at that point.

## Remaining Tasks

- Phase 2 tasks 2.1–2.3
- Phase 3 tasks 3.1–3.3
- Phase 4 tasks 4.1–4.3
- Phase 5 tasks 5.1–5.2

## Unit 4C1B — Durable Owner Reconciliation

- Work unit: C1B1 only; feature-branch-chain child `feat/sdd7-4c2c1b-owner-reconciliation`.
- Scope: owner-local durable rehydration, validation/fail-closed loading, handler restore, pending reaction/notification reconciliation, pre-effect persistence, effect progress, exact idempotency keys, recovery handling, and nominal agent-death restart reconciliation. C1B2 fault/cancellation races remain deferred.
- Files changed: `src/ControlParental.Service/AntiTamperMonitor.cs` and `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`.
- CODE+TEST budget: `123 additions + 2 deletions` production and `183 additions + 2 deletions` tests = **310 changed lines**, under the hard 400-line limit. The OpenSpec artifacts are excluded.

### TDD Cycle Evidence — C1B1

| Task | RED | GREEN | REFACTOR |
|---|---|---|---|
| 4.2a — Durable owner nominal reconciliation | PASS — preserved immutable initial RED `sdd7-unit4c2c1b-c1b1-red-20260826.log` (exit 1, SHA-256 `E8C4C471837AE233300856FA528272D3D5907184DBD021A5118D4385A8C4B77F`) and nominal triangulation RED `c1b1-nominal-triangulation-red-behavior.log` (exit 1, SHA-256 `58A080A9EA271DDFA7DF4E278E604C1D014DE9F0A9E2E7DD6D8905795AC6C85E`) | PASS — durable-owner focus `8/8`; full `AntiTamperMonitorTests` class passed; affected Service build passed with 0 errors; portable XPlat coverage run passed | PASS — minimal nullable guard, exact completion preservation during restart reconciliation, compact test doubles, and no changes outside the assigned production/test files |

### C1B1 Verification Evidence

- Focused command: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --filter "FullyQualifiedName~DurableOwner_"`; result `8 passed, 0 failed`.
- Whole class command: `--filter "FullyQualifiedName~AntiTamperMonitorTests"`; exit `0`.
- Affected build: `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore`; exit `0`.
- Portable coverage: focused durable-owner run with `--collect:"XPlat Code Coverage"`; exit `0`, artifact under `tests/ControlParental.Service.Tests/TestResults/coverage-c1b1-final`.
- `git diff --check`: exit `0`.
- Remaining: C1B2 (`4.2b`), independent verification, and later C1C/C2 work. No commit or verify report was created.

## Unit 4C — Verdict Ordering + Escalation (Apply Resume)

- Work unit: Unit 4C only; feature-branch-chain child `feat/sdd7-4c-verdict-ordering-escalation`, exact B2 parent `6c6668bbdb5eb2bc328cfe86c28f878a51149679`.
- Partial-state audit: the aborted invocation left unstaged edits in `IntegrityVerdictHandler.cs` and `IntegrityVerdictHandlerTests.cs`, plus the unchanged cumulative 34-file OpenSpec tree. No staged files, running owned `dotnet` processes, B2 helper changes, Unit 5 changes, composition changes, retry changes, or unexplained tracked paths were found. The existing tests-first additions and their failing execution were preserved and extended rather than overwritten.
- Restore: App.UI test assets were missing; the minimum scoped App.UI test restore was run. It produced only existing NU1601/NU1701 warnings and no tracked project, dependency, source, configuration, lockfile, or task drift. All subsequent build/test/coverage commands used `--no-restore`.
- Exact normative contract used for this slice: startup grace (<5 minutes) returns `ShadowWarn`; only definitive `trust`/`revoked` advances ordered verdict state; older definitive timestamps are ignored; revoked 1/2 return `Warn`, revoked 3 starts one deadline at `thirdTimestamp + 5 minutes`, before deadline and exactly-at/after boundary are deterministic, and trust resets/cancels the pending escalation; unknown, malformed, unavailable, cancelled, and other non-definitive inputs warn/observe without resetting the pending escalation or enforcement state; three consecutive definitive trusts emit authoritative recovery; callbacks and notification I/O occur after the state lock; stale timer epochs cannot emit escalation twice.
- No Unit4A durable-authority semantics, BackendClient retry ownership, B2 lifecycle/admission/drain/semaphore helpers, AntiTamperMonitor source, Unit5 composition, Program wiring, or verification reports were changed.

### TDD Cycle Evidence — Unit 4C

| Task | Test file | Layer | Safety net | RED | GREEN | TRIANGULATE | REFACTOR |
|---|---|---|---|---|---|---|---|
| 4.1 — Handler ordering, callback boundary, definitive/non-definitive semantics | `IntegrityVerdictHandlerTests.cs` | Unit/component | Existing handler baseline was run; final handler focus `32/32` | PASS — preserved aborted partial RED: stale definitive test returned `ShadowWarn`; added non-definitive-preservation, callback-order, and exact-boundary tests and observed `3` failures before the production correction | PASS — handler focus `32/32` | PASS — revoked→trust, trust→revoked, repeated definitive inputs, recovery threshold, unknown/failure preservation, callback reentrancy, callback fault identity, and stale completion are covered without timing primitives | PASS — narrow state lock/core split; callbacks and outbox work are outside the lock; no discarded reaction callback task |
| 4.2 — Escalation deadline identity and timer ownership | `IntegrityVerdictHandlerTests.cs`, `IntegrityRuntimePathTests.cs` | Unit + production-path | Combined integrity focus `127/127`; AntiTamper `55/55` | PASS — exact deadline and runtime authority tests exposed the abandoned candidate's pre-deadline degradation; timer test was added before the timer seam | PASS — injected clock/timer active/stale test passes; runtime path now advances the fourth authoritative revoked sample to the exact due boundary | PASS — before/at/after boundary, trust reset, stale timer callback, and full real monitor/backend/policy/enforcement path all pass | PASS — monotonic state/escalation epochs; one-shot timer identity; injected timer/clock defaults preserve public constructor compatibility |
| 4.3 — Race/reentrancy safety and regression closure | `IntegrityVerdictHandlerTests.cs`, existing `AntiTamperMonitorTests.cs` | Component/regression | Domain, Service, Service-tests, and App.UI portable-PDB builds passed; full Service `1,217/1,217`; App.UI `192/192`; two fresh coverage hosts | PASS — callback synchronously reenters verdict handling and callback fault is observed; existing B2 production-path race evidence was triangulated, not fabricated | PASS — combined Unit4 focus `127/127`, AntiTamper `55/55`, full regressions, and two isolated coverage hosts pass | PASS — B2 agent-death/restart/enforcement ownership tests remain green; handler reentrancy, stale callback, and no-lock callback paths are exercised | PASS — no fire-and-forget handler reaction path; notification failures are observed and rethrown; no lifecycle or composition scope expansion |

### Unit 4C Execution Evidence

- Fresh builds (`--no-restore`): Domain, Service, Service-tests portable-PDB, and App.UI-tests portable-PDB all passed with 0 errors; existing analyzer/package warning corpus remains.
- Fresh focused matrix: AntiTamper `55/55`; combined `IntegrityCheckerTests|AntiTamperMonitorTests|IntegrityVerdictHandlerTests|IntegrityRuntimePathTests|EnforcementLevelMonitorTests` `127/127`.
- Fresh regressions: full Service `1,217/1,217` (one existing duplicate xUnit ID discovery notice); full App.UI `192/192`.
- Fresh isolated coverage hosts: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c-final-a5/1ec565b7-f15a-4e82-8196-e234cb2e2ea1/coverage.cobertura.xml` and `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c-final-b5/c86da728-3b10-4283-b9ca-1bb6b809dfda/coverage.cobertura.xml`, each full Service `1,217/1,217`. Host A reports `IntegrityVerdictHandler` `90.72%` line / `77.17%` branch; `AntiTamperMonitor` `99.15%` line / `87.75%` branch. Coverage includes active/stale/reset ordering branches; no aggregate threshold is configured. An earlier host A attempt had one known intermittent B2 late-failure assertion miss; the two final isolated hosts both passed without code changes.
- `git diff --check` passed. Current CODE+TEST budget from exact B2 parent: production `145 additions + 37 deletions`, tests `163 additions + 1 deletion` (including the runtime-path timestamp seam), total **349/400**. OpenSpec artifacts and ignored build/coverage outputs are excluded from CODE+TEST accounting.
- Tasks remain intentionally unchanged: `4.1`, `4.2`, and `4.3` are still unchecked; cumulative task state remains **9/14**. No verify report was written during apply.

### Unit 4C Issues / Deviations

- The aborted invocation's partial production edit existed before this resume; its provenance was not recoverable from a durable prior result, so it is recorded as partial-state evidence rather than claimed as fresh RED chronology. New behavior tests were written and executed RED before the resumed production correction.
- The existing AntiTamperMonitor/B2 agent-death, restart, enforcement, drain, and cancellation paths were intentionally not modified; their fresh regression pass is triangulation and boundary evidence, not new Unit4C ownership.
- No external/live backend, WNS, signed-Windows, `client-ready`, `live-integrated`, or `ExternalVerified` claim is made.

### Unit 4C1A Independent-Report Remediation

- Scope remained limited to the two reported 4C1A defects and missing trust-cancel evidence; no 4C1B/4C2 files or behavior changed.
- Strict-TDD RED was observed before the production correction: the repeated-transient test returned `1/3` instead of preserved `4/3`, and circuit-at-due returned `ShadowWarn` instead of `Degrade`.
- Production minimum fix: transient circuit opening no longer clears definitive revoked state; due commitment is evaluated before circuit-open return and wins at the exact local acceptance boundary.
- Extended existing tests for repeated transient preservation, circuit-at-due precedence, and pre-due trust cancellation followed by old-deadline no-op, fresh deadline, and exactly two pending notifications.
- GREEN: focused remediation `3/3`; handler `31/31`; runtime `9/9`; AntiTamper/IntegrityChecker/Enforcement `86/86`; full Service `1,216/1,216`; App.UI `192/192`; late-failure host A/B `1/1` each.
- Portable-PDB builds for Domain, Service, Service tests, App.UI, and App.UI tests passed with 0 errors. Final coverage hosts passed `1,216/1,216` with non-empty Cobertura artifacts: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-final-remed-a2/edb4d66a-48f8-4cb4-ad63-eceaabf9eb97/coverage.cobertura.xml` and `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-final-remed-b/2e711cf5-13b0-4666-8001-573db01a2d4e/coverage.cobertura.xml`; both include `IntegrityVerdictHandler` transient/circuit/deadline/trust-reset paths.
- Final CODE+TEST is exactly **400/400** (`124+67` production, `31+27` runtime-path tests, `144+7` handler tests); no exception. `git diff --check` passed and `.codegraph/` remains absent.
- Tasks remain intentionally unchanged at **9/14**; no verification report or commit was created. Ready for independent Unit4C1A re-verification only.

### Unit 4C1A Follow-up Evidence — 2026-08-22

- Focused handler tests passed: `31/31`.
- Focused runtime-path tests passed: `9/9`.
- Focused AntiTamper/IntegrityChecker/Enforcement matrix passed: `86/86`.
- Full Service regression passed sequentially: `1,216/1,216`; the existing duplicate xUnit ID discovery notice remains.
- Full App.UI regression passed: `192/192`.
- Two successful isolated full-coverage hosts passed with non-empty Cobertura artifacts:
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-final-next-a-rerun/06da0bd0-483d-4edc-b6d2-134106d15acd/coverage.cobertura.xml`
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-final-next-b/1f1691fd-a5bd-4a74-835d-89569080d206/coverage.cobertura.xml`
  - Host A reports `IntegrityVerdictHandler` `98.11%` line / `95.12%` branch; host B reports `98.11%` line / `93.90%` branch. Both include transient/circuit/deadline/trust-reset paths.
- An initial attempt to run the two coverage hosts concurrently produced infrastructure-only `FileNotFoundException` failures for `ControlParental.Service`; the standalone rerun passed and is the retained Host A artifact. No source change was made.
- The pre-existing `AntiTamperMonitorTests.LateNonCancellableFailure_IsObservableAndHasNoEffects` is intermittently failing in the full suite (observed `No exception was thrown` on the `work` assertion) and also failed when run alone in this host. It is outside the Unit4C1A edits and was not changed.
- Current CODE+TEST remains exactly **400/400** (`124+67` production, `32+29` runtime-path tests, `130+18` handler tests); no exception. No verify report or commit was created.

### Unit 4C1A Final Public-Flow Coverage Closure — 2026-08-22

- Test-only repair completed in `IntegrityVerdictHandlerTests.cs`; no production source was edited. The due-deadline observation now uses public `HandleVerdict(...)` with an injected local clock, while the circuit is already open. It asserts one `Degrade`, a subsequent circuit `ShadowWarn`, circuit persistence, and `EvaluateDeadline(...)=None` after the public commit to prove one-shot firing. Direct `EvaluateDeadline(...)` coverage remains in the other deadline tests.
- The existing post-escalation test also uses public `HandleVerdict(...)` at a deterministic due acceptance time. The test file remained within the original budget by in-place replacement/compaction; exact CODE+TEST is **400/400** (`124+67` production, `32+29` runtime-path, `115+33` handler test diff accounting).
- Corrected public-flow test passed `1/1`; focused trust/remediation set passed `4/4`; handler focus passed `31/31`; runtime focus passed `9/9`; combined Unit4 matrix passed `126/126`; inherited B2 late-failure test passed isolated `1/1`.
- Full Service passed twice sequentially: `1,216/1,216` each; full App.UI passed `192/192`. The existing duplicate xUnit ID discovery notice remains. Build passed with `0` errors and only the existing warning corpus.
- Two sequential isolated full-coverage hosts passed with non-empty Cobertura artifacts:
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-final-public2-a/84b527df-ef50-4994-b2c8-d5bf793232a8/coverage.cobertura.xml`
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-final-public2-b-rerun/93729d6d-a149-4f28-9947-d5c3be1db19e/coverage.cobertura.xml`
  - Both report `IntegrityVerdictHandler` line-rate `98.11%`, branch-rate `93.90%`; line `194` has `hits="4"`, `branch="True"`, and `condition-coverage="100% (2/2)`, proving both `deadlineWon` outcomes (due-wins and circuit-shadow) were exercised.
- The first sequential Host B attempt had the previously diagnosed inherited AntiTamper late-failure flake (`No exception was thrown`) and exited `1`; the isolated no-build rerun passed `1,216/1,216` and is the retained Host B artifact. No source or test change was made for that inherited behavior.
- Final audit: `git diff --check` passes; `HEAD` and B2 base are both `6c6668bbdb5eb2bc328cfe86c28f878a51149679`; `.codegraph/` is absent; no Unit4B/4C2/Unit5 files, reports, or commits were added. Ready for final independent Unit4C1A verification only.

### Read-only Diagnosis — inherited AntiTamper isolation report (2026-08-22)

- Compared current worktree `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-4c` at B2 base `6c6668bbdb5eb2bc328cfe86c28f878a51149679` with exact committed B2 worktree `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd7-unit-4b2` at the same commit. Git blob hashes are identical for both `src/ControlParental.Service/AntiTamperMonitor.cs` (`cce963754da5ac9cf096ece91b7085c6bde28ae0`) and `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` (`0f0caa1f835cf0e2e653197bece99cb7bf8287d0`). Raw SHA-256 differed only because current files are CRLF and the B2 worktree files are mixed line endings; `git diff --no-index --ignore-space-at-eol` emitted no content differences. Neither file was changed by 4C1A.
- The original failing invocation was the exact filter below, launched concurrently with an identical invocation:

  ```text
  dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build --filter 'FullyQualifiedName~LateNonCancellableFailure_IsObservableAndHasNoEffects'
  ```

  Observed raw failure: `Assert.Throws() Failure: No exception was thrown`, expected `System.InvalidOperationException`, at `AntiTamperMonitorTests.cs:416`; the run reported `1 failed, 0 passed, 1 total`. The two simultaneous coverage hosts also produced shared-output `FileNotFoundException` failures for `ControlParental.Service`, confirming unsafe concurrent use of the same built/test output tree.
- Each exact filter discovered exactly one intended test. Five sequential fresh-process runs in current 4C1A passed (`5/5`), with TRX/raw logs under `C:\Users\Usuario\AppData\Local\Temp\opencode\diag-unit4c-current\run-1` through `run-5`. Five sequential fresh-process runs in exact B2 passed (`5/5`), with logs under `C:\Users\Usuario\AppData\Local\Temp\opencode\diag-unit4c-b2\run-1` through `run-5`.
- Ordering matrix passed: full AntiTamper class `55/55`, predecessor/neighbor filter (`ConcurrentStop`, `LateNonCancellableNormalCompletion`, `LateNonCancellableFailure`) `6/6`, followed by a separate exact test process `1/1`. Five two-process parallel waves using distinct TRX directories also passed `10/10`; logs are under `C:\Users\Usuario\AppData\Local\Temp\opencode\diag-unit4c-parallel`.
- Fixture/source path: each test constructs fresh Moq collaborators and a fresh monitor; `ConfigureHealthyBackend` makes privilege/integrity/verdict collaborators deterministic; the test gates the second backend call with a `TaskCompletionSource`, calls `StopAsync`, releases the non-cancellable backend task, then awaits the admitted work and stop task. `PerformBinaryIntegrityCheckAsync` receives `propagateFailure: true`; its backend exception is rethrown, `CompleteAdmissionAsync` transfers it to the public completion task, `AdmitTask` records it on `Lifecycle`, and `DrainGenerationAsync` observes owned-task failures. The observed success runs therefore follow the intended fault path; no source path explains a deterministic successful completion for this test.
- Current built assets are not stale relative to current source: current `ControlParental.Service.dll` timestamp `2026-08-22T15:45:15.4331234-05:00` follows the current handler source timestamp `2026-08-22T15:31:46.2630466-05:00`; current Service-tests DLL timestamp `2026-08-22T16:01:54.7283267-05:00` follows the modified test sources (`16:01:12` and `16:01:27`). B2 assets exist and were used without restore. Current/B2 DLL hashes differ as expected because 4C1A changed handler/runtime test assemblies; source identity above rules out AntiTamper source drift.
- Classification: **environment/infrastructure — shared-output/testhost interference from concurrent `dotnet test`/coverage processes**, not stale assets, filter error, B2 test fragility, or a 4C1A-induced AntiTamper behavior change. No code/test fix is justified. A B2 remediation would require a separate child/remediation decision and must not be added to Unit4C1A or its exact `400/400` budget.

## Issues / Deviations

- The generic `UIMessageHandler` maps the authenticated typed trigger to `SyncTriggerSource.Wns`, matching the existing WNS-trigger test and preserving a bounded source. No payload-derived authority or retry was added.
- The shutdown test initially timed out because `StopAsync` could cancel dispatch before its policy repository call began; the test now synchronizes on work entry rather than changing scheduler ownership semantics.
- The prior separate two-source and all-five-source coalescing fragments were consolidated into the complete identity-denied-then-restored five-source sequence; the all-five-source cardinality and one-fetch assertions remain intact.

### Unit 4C1 C1 Remediation Batch 2 — 2026-08-25

- Scope remained limited to `IntegrityVerdictHandler.cs`, `AntiTamperMonitor.cs`, and their two existing test files. No `Program.cs`, durable contract, new field, API, framework, commit, or unrelated source changed.
- Batch 2 closed the four reported gaps: independent restart reconciliation for reaction and notification effects; exact keyed reaction forwarding; fail-closed state-load fault observability with cancellation propagation; and restart deadline scheduling with original due-time preservation.
- Production changes: rehydrate/reconcile each pending effect independently, persist completion after each effect, preserve pending/completed progress across no-effect decisions, report non-cancellation state-load faults through the keyed enforcement path, schedule the restored deadline, and forward `ReactionIdempotencyKey` to the existing keyed `AddIssueAsync` overload.
- Batch 2 RED raw artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1-red2.log` (the observed pre-fix failing focused run). GREEN: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1-green2.log`. REFACTOR: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1-refactor2.log`.
- Focused batch 2 result: `4 passed, 0 failed, 0 skipped`.
- AntiTamperMonitor regression result: `85 passed, 0 failed, 0 skipped`.
- Full Service regression result: `1,280 passed, 0 failed, 0 skipped`; existing duplicate xUnit ID discovery notice remains.
- Service build result: `0 errors`, existing analyzer/package warning corpus only.

### Unit 4C2 C1B Owner Reconciliation — 2026-08-26

- Scope remained limited to `AntiTamperMonitor.cs` and the existing `AntiTamperMonitorTests.cs`; no Handler, Program, Domain, timer, composition, retry, or durable-contract changes were made.
- Production changes add optional durable escalation-store ownership, rehydrate-before-remote startup ordering, fail-closed load handling, independent reaction/notification reconciliation, save-before-effect and post-effect progress persistence, exact keyed reaction forwarding, and generation-scoped state saves.
- Focused durable-owner GREEN: `9 passed, 0 failed, 0 skipped`; focused AntiTamper refactor safety net: `87 passed, 0 failed, 0 skipped`.
- Service build: `0 errors`; existing analyzer/package warnings remain. Full Service regression: `1,287 passed, 2 failed, 0 skipped, 1,289 total`; failures are unrelated environment-sensitive `NamedPipeUIServerHostedAdapterTests.StartAsync_ExposesUiPipeBeforeClientConnects` (`UnauthorizedAccessException`) and the pre-existing `TaskSchedulerBackupServiceUnregisterTests.UnregisterBackupTasksAsync_WhenNoTasks_ReturnsTrue` assertion.
- Coverage artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-coverage-final2\ba559047-c9fd-47fc-a06a-9362acfd993a\coverage.cobertura.xml`; `AntiTamperMonitor` reports `98.97%` line / `84.17%` branch, and `ExecuteDecisionChainAsync` reports `92.45%` line / `96.42%` branch.
- CODE+TEST diff is `283` changed lines (`98` production insertions and `5` deletions; `185` test insertions), within the `250–380` target and below the hard `400` limit.
- Evidence logs: GREEN `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-green.log` SHA256 `09657788195492F690BC00582C1D48F4AB7B521D9F1730208BF16C43DDC92E9B`; REFACTOR `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-refactor.log` SHA256 `F8C1DB349674586CB78967E1377161F49EE7495EC916135CC351A8FEB51083B5`; prior RED artifact retained at `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-red.log`.
- `git diff --check` passes. No commit or verification report was created. Task `4.2` is closed by the authoritative gate-closure evidence below; `4.3` and later tasks remain pending.

### C1B Gate Diagnosis — 2026-08-26

- Gate result: **CLOSED**. Task `4.2` is checked and cumulative state is `11/16`; `.codegraph/` remains locked by the active external MCP daemon and is explicitly listed for independent verifier cleanup.
- Immutable C1B evidence audit:
  - RED path `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-rebuild-red.log`, SHA256 `E8C4C471837AE233300856FA528272D3D5907184DBD021A5118D4385A8C4B77F`; the authoritative behavioral result is recorded in the six-column row below. Compile/setup path is separate: `sdd7-unit4c2c1b-rebuild-compile.log`, supplied hash `A8E358...`.
  - GREEN path `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-green.log`, SHA256 `09657788195492F690BC00582C1D48F4AB7B521D9F1730208BF16C43DDC92E9B`. Command: `dotnet test tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity normal --filter FullyQualifiedName~DurableOwner`. Log totals: `9 passed, 0 failed`; explicit process exit was `0`.
  - REFACTOR path `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-refactor.log`, SHA256 `F8C1DB349674586CB78967E1377161F49EE7495EC916135CC351A8FEB51083B5`. Command: `dotnet test tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity normal --filter FullyQualifiedName~AntiTamperMonitorTests`. Log totals: `87 passed, 0 failed`; explicit process exit was `0`.
  - The six-column TDD row below is authoritative for the reconstructed C1B cycle; rejected-candidate diagnosis remains preserved separately and no raw log was rewritten.
- Original full Service command: `dotnet test tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal`; exit `1`; result `1,287 passed, 2 failed, 0 skipped, 1,289 total`.
- Exact C1B differential reruns, each in a separate fresh test process:
  - `ControlParental.Service.Tests.NamedPipeUIServerHostedAdapterTests.StartAsync_ExposesUiPipeBeforeClientConnects`: exit `1`; `System.UnauthorizedAccessException: Access to the path is denied`, stack at `NamedPipeClientStream.TryConnect` and test line `352`. Log: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-diff-c1b-namedpipe.log`.
  - `ControlParental.Service.Tests.TaskSchedulerBackupServiceUnregisterTests.UnregisterBackupTasksAsync_WhenNoTasks_ReturnsTrue`: exit `1`; `Expected result to be True, but found False`, stack at `TaskSchedulerBackupServiceTests.cs:202`. Log: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-diff-c1b-scheduler.log`.
- Exact C1A parent differential exists at `f37df4a`; `c1a-differential-namedpipe.log` (SHA `EDD4B18CDE785E46148B781BF91B1C91DCF905AAADF5F58F902DCDC9A32FD4A9`) and `c1a-differential-scheduler.log` (SHA `E8F74F09C07D876FE1823D9E3C320869B28DDF82999DFDF62FE68E129D25B428`) reproduce the same two inherited host-sensitive failures. They are not C1B regressions; no all-tests full-suite rerun was performed.
- Cleanup: workspace `TestResults` generated coverage files were absent. Child `.codegraph` remains present and locked (`codegraph.db`, `-shm`, `-wal`); `codegraph uninit` failed with `EPERM`. Read-only process inspection identified active CodeGraph MCP daemon processes but no safe process-specific owner mapping; no process was killed.
- Current status is not clean: modified production/test files remain the exact two allowed code/test files, OpenSpec artifacts are untracked, and task-created `.codegraph/` is an additional blocked untracked path. No commit, stage, remote, history, or production/test edits were made during diagnosis.
- Follow-up correction: when a durable state store is present, the live reaction now forwards `VerdictDecision.ReactionIdempotencyKey` through the keyed `AddIssueAsync` overload; legacy no-store construction retains the legacy overload for compatibility. Added a direct keyed-forwarding assertion to the durable-owner test. Latest focused results: `DurableOwner` 10/10 and `AntiTamperMonitorTests` 88/88, both exit 0. CODE+TEST diff remains below the hard 400-line limit; task 4.2 is closed, with `.codegraph` explicitly listed for independent verifier cleanup.
- Fresh full-coverage result: `1,280 passed, 0 failed, 0 skipped`; Cobertura artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1-coverage2\dee6302a-2455-4b65-a58f-ef43f52c1474\coverage.cobertura.xml`.
- Current CODE+TEST diff: `334 additions + 8 deletions = 342`, below the hard `400` limit. No `size:exception` used.
- Task `4.2` is now closed at cumulative `11/16`; no verification report or commit was created.

### Unit 4C2 C1B1 Independent Apply Closure — 2026-08-26

| Task / cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.2a C1B1 nominal owner reconciliation | PASS — current initial five-test behavioral RED: 1 passed, 4 failed, exit 1. Exact failures: `DurableOwner_RestartReconcilesReactionAndNotificationIndependently` — Moq expected `AddIssueAsync(..., CancellationToken)` once, observed 0; `DurableOwner_RehydratesBeforeFirstRemote_AndMissingStateIsValid` — expected `store.Loads` 1, found 0; `DurableOwner_SavesAcceptedStateBeforeEffects_WithExactKeys` — `NullReferenceException` during `Dispose`; `DurableOwner_DegradedRecoveryUsesResolveAndPersistsCompletion` — Moq expected `ResolveIssueAsync(..., "authoritative backend trust verdict", ...)` once, observed 0. Raw SHA-256 `E8D9465367913C218F555C8B574424D0889AFC79587F16119A3910F93D6D8057`. | PASS — fresh `DurableOwner_` 8/8 exit 0, raw SHA-256 `C6C9CD1B9265B8EDD3B83A956CE2D75697A1D5B5CD2D8D5E3FDED2A196A43445`; fresh exact `AntiTamperMonitorTests` 86/86 exit 0, raw SHA-256 `C368662E5DB7D5F0B683BCC7A58BC6CB1CCAC71D1D11D392228E7F80E71E802C`. | PASS — separate compile/setup artifact `c1b1-nominal-triangulation-red.log` (CS0029/CS0039 only), SHA-256 `569996E86AB45000B1BEB8A3FDBAF3E592C9F706EC304504B537C53F56E9DA10`, is not behavioral RED. Behavioral triangulation artifact `c1b1-nominal-triangulation-red-behavior.log`: 6/8 passed, 2 failed, exit 1; exact failures `DurableOwner_InvalidOrUnavailableLoadFailsClosed_AndCancellationUsesOwnerToken` (`IntegrityEscalationStateException` during `Dispose`) and `DurableOwner_ActualRevokedAndTrustObservationsPersistBoundariesAndReset` (`NullReferenceException`), SHA-256 `58A080A9EA271DDFA7DF4E278E604C1D014DE9F0A9E2E7DD6D8905795AC6C85E`. | PASS — fresh affected Service build 0 errors, exit 0, SHA-256 `8EFC36E534FF6C705A1B97959CBE2534C488AF56F30AA7968A15C22A3610EC58`; fresh exact Service safety excluding only the two proven inherited FQNs passed 1,287/1,287 exit 0, SHA-256 `B00776710E371E7035852368AE7C57991AAB8BB397E992E8C374EDB2577257C1`. | PASS — fresh external portable-PDB coverage for exact `AntiTamperMonitorTests` passed 86/86 exit 0; log SHA-256 `7B8F2B94F84590F46A246806B09A48606015764A4CA29EE6A531C6564D7BF61A`, Cobertura `C:\Users\Usuario\AppData\Local\Temp\opencode\c1b1-final-coverage-20260826\0419c091-8c18-4fcd-a3a7-6715b375f4e4\coverage.cobertura.xml`, XML SHA-256 `798D0503E28E2D390A9744A973D31B869708269F2D6AD2B6D1153F9FE0363747`. `AntiTamperMonitor` line 98.97% / branch 84.33% (>80% meaningful line). C1B1 async state machines inspected: `RunOwnedStartupAsync` 100/100, `RehydrateAsync` 100/90, `ReconcileDurableEffectsAsync` 87.5/71.42, `ExecuteDecisionChainAsync` 92.45/96.42, `PrepareDurableStateAsync` 100/85.71, `SaveEffectProgressAsync` 100/100, `SaveStateAsync` 100/50 line/branch; all have executed hits, including documented zero-hit lines and branch condition coverage in the XML. Historical `c1b1-focus-green.log` SHA-256 `19B3680418449B3E9DD13BD44936544BB7750E1AAB3B09F10DC7CEE35998B0B2`, `c1b1-antitamper-green.log` SHA-256 `45975814BFB95857DDCC60FE4B51F58D8C4E86808674013A7FC51C2D36563AC6`, and `c1b1-service-build.log` SHA-256 `1FEA05869F8A2460789A63A13697766846A88C0CC658C54703AA3B5200F36434` are retained historical evidence; the old E8C4 broad-candidate hash is not reused. |

- Current audit: HEAD `f37df4a1036a2969b852f929f9db632c7b424206`; exact tracked code/test diff is exactly two files, `123 additions + 2 deletions` production and `183 additions + 2 deletions` tests = **310/400**; staged 0; `git diff --check` exit 0.
- `IntegrityVerdictHandler.cs` and `Program.cs` current blobs equal HEAD. Workspace generated `coverage-c1b1-final` is absent after external artifact confirmation; no workspace generated coverage remains. `.codegraph/` is the only additional untracked path and is externally locked by the CodeGraph daemon.
- C1B2 / task `4.2b` remains deferred and unchecked. This closure is ready for independent verification. No production/test, immutable report, proposal/spec/design, OS, Piranha, restore, solution-build, stage, commit, remote, or history action was performed.

### Unit 4C2 C1B1 Nominal Remediation Cycle — 2026-08-26

| Task / cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.2a C1B1 nominal durable-owner remediation | PASS — immutable external log `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b1-remediation-red.log`, 6/8 passed, 2 failed, exit 1, SHA-256 `D5F77F6364CB6CF61CBCAD9FB39740927DDD9AFB98379421033B0EE29D3CAC1D`; exact failures: missing keyed live reaction assertion and first revoked persisted boundary (`0/Normal`). | PASS — exact `DurableOwner_` focus 8/8, exit 0; keyed live Add and legacy-no-store compatibility assertions pass; revoked 1/2/3/4, actual trust 1/2/3, reset, latch, replay, and restart assertions pass. | PASS — recovery extension is triangulation: valid non-Degraded `RecoveryLatch=true` reconciles one exact keyed Add and zero Resolve; no fabricated failure. | PASS — whole `AntiTamperMonitorTests` 86/86; exact-exclusion safety 1,287/1,287; affected Service build `--no-restore` 0 errors; no unrelated filters or restore. | PASS — only the two assigned code/test files changed; no new helper/type/field/framework/retry/lock; live durable path is keyed, no-store path remains legacy, accepted snapshots persist before effects, and exact completion progress is preserved. |

- Current audit: source/test diff is exactly the assigned two files, `131 additions + 3 deletions` production and `231 additions + 2 deletions` tests = **367/400**; staged files remain 0; `git diff --check` is clean.
- `4.2a` is checked from the completed apply gates; `4.2b` remains unchecked and deferred. Status: **ready for independent verification**. No verifier report was edited.
- Orca comment: `C1B1 remediation complete; ready for independent re-verification`

### Historical superseded C1B Gate Diagnosis — preserved below

| Task / cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.2 C1B owner reconciliation | PASS — behavioral RED: 1 pass, 8 failures, exit 1; failures were `DurableOwner_CompletedEffectsReplayAsNoOps` (NullReferenceException during Dispose), `DurableOwner_RestartReconcilesPendingNotificationAfterCompletedReaction` (same), `DurableOwner_SavesAcceptedStateBeforeReactionAndNotification_WithExactKeys` (saved-state collection empty), `DurableOwner_RecoveryUsesResolveAndNeverAddIssue` (same NullReferenceException), `DurableOwner_AgentDeathAndOneShotRecoveryConverge` (same), `DurableOwner_LoadFaultFailsClosed_AndCancellationPropagates` (expected IntegrityEscalationStateException, none), `DurableOwner_RestartReconcilesPendingReactionWithoutNotificationReplay` (same), and `DurableOwner_RehydratesBeforeFirstRemote_AndMissingStateIsValid` (expected Loads=1, found 0). Hash `E8C4C471837AE233300856FA528272D3D5907184DBD021A5118D4385A8C4B77F`; compile/setup hash `A8E358...` is not RED evidence. | PASS — first GREEN 9/9, exit 0, hash `E488D6A6879B5A90FCB6F885C6591B4DE971F15B50F9CF3E72EBB471F843C53E`; distinct remediation cycle: attempt2 9/10 (failure `DurableOwner_PersistsAuthoritativeRecoveryBeforeCompletion`, Resolve expected once/observed twice), exit 1, hash `F977AC75DBC44CD47CE3765BA04A38B776637C235987ACD05E89504AF514875E`; attempt3 8/10 (same plus `DurableOwner_AgentDeathAndOneShotRecoveryConverge`, Resolve expected twice/observed once), exit 1, hash `BC61DCD0D3B81AB744C979B3DCF3503396EC55C94C83D23FDB428B3D7F258201`; final attempt4 10/10, exit 0, hash `9A12C990F56738620578C7190043FC1E43B81684C57E31A8C5EF3140F8B48700`. | PASS — final 10-test owner run covers rehydrate ordering, independent effect recovery, exact reaction/notification keys, recovery Resolve, and stale-generation suppression. | PASS — exact excluded-baseline filter: `dotnet test tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --filter "FullyQualifiedName!~ControlParental.Service.Tests.NamedPipeUIServerHostedAdapterTests.StartAsync_ExposesUiPipeBeforeClientConnects&FullyQualifiedName!~ControlParental.Service.Tests.TaskSchedulerBackupServiceUnregisterTests.UnregisterBackupTasksAsync_WhenNoTasks_ReturnsTrue" --verbosity minimal`; 1288/1288, exit 0, hash `864C1D91024E0365F8B0D1263AEAC5777163389D0F13C7195943EDDC0A7FDF13`. | PASS — existing refactor artifact `2EE780E2E027D1B3E1CFAB54EF4126C265055FAC15CB30C975AA821813CC27CA` records 87/87 before the final assertion; fresh post-change focused run records 88/88, exit 0. |

- Service build: one run, 0 errors, existing NU1601 warning.
- Fresh portable-PDB coverage: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-gate-coverage-20260826\121a4baf-773d-4a5c-9d3c-c6aea684d94c\coverage.cobertura.xml`, SHA256 `58E61ECD35E8BE3D8E631FD8632BC9D6CB7B869C0994E6BB924C8CBC5DF71D69`; exact AntiTamper filter 88/88. `AntiTamperMonitor`: 98.97% line / 83.95% branch. State-machine methods: `RehydrateAsync` 100/100, `PrepareDurableStateAsync` 100/100, `ReconcileDurableEffectsAsync` 100/91.66, `SaveEffectProgressAsync` 100/100, `SaveStateAsync` 100/50, `ExecuteDecisionChainAsync` 93.22/93.75 line/branch; all have hits in the current Cobertura XML.
- Parent differential is available: C1A `f37df4a` reproduces the two inherited exclusions in `c1a-differential-namedpipe.log` SHA `EDD4B18CDE785E46148B781BF91B1C91DCF905AAADF5F58F902DCDC9A32FD4A9` and `c1a-differential-scheduler.log` SHA `E8F74F09C07D876FE1823D9E3C320869B28DDF82999DFDF62FE68E129D25B428`.
- Final audit: HEAD `f37df4a1036a2969b852f929f9db632c7b424206`; staged 0; exact tracked code/test diff is two files, `99+3` production and `178` tests = `280/400`; diff-check green; Handler and Program blobs equal HEAD. `.codegraph/` remains solely because the active external MCP daemon holds its lock. No production/test, OS, Piranha, commit, stage, remote, or history action was performed.

### TDD Cycle Evidence — Unit 4C1 Batch 2

| Task | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.1 Batch 2 — owner rehydrate/deadline gaps | PASS — focused batch failed before the production correction on keyed forwarding, independent restart completion, state-load observability, and restart deadline behavior | PASS — focused batch `4/4` | PASS — exact reaction key, independent reaction/notification completion, fail-closed fault/cancellation behavior, original deadline, and actual one-shot timer callback are covered | PASS — AntiTamper `85/85`; full Service `1,280/1,280`; Service build `0` errors; fresh coverage host `1,280/1,280` | PASS — no new public API/durable field; existing keyed overload and existing timer/state-store paths reused |

### Unit 4C1B2a Final Apply Evidence — 2026-08-24

- Scope remained limited to effect-chain observation: `AntiTamperMonitor.cs`, `AntiTamperMonitorTests.cs`, and the runtime-path expectation in `IntegrityRuntimePathTests.cs`. No dedupe/retry, Unit 4C2, Unit 5, or composition changes were added.
- Focused Unit 4C1B2a tests passed, including ordered reaction→notification effects, stale identity suppression, fault publication, Stop/Dispose draining, caller cancellation, generation serialization, and reaction-only decisions.
- Full Service regression passed on the retained rerun: `1,229/1,229`; the existing duplicate xUnit ID discovery notice remains. The immediately preceding coverage host had one intermittent notification-fault Stop assertion miss; the isolated notification coverage rerun passed `1/1`, and the retained full coverage rerun passed `1,229/1,229` with Cobertura at `C:\Users\Usuario\AppData\Local\Temp\opencode\unit4c1b2a-coverage-final-rerun\37444af8-99a4-4d58-a84e-2991740ffc47\coverage.cobertura.xml`.
- Full App.UI regression passed: `192/192`.
- `git diff --check` passed. Current CODE+TEST diff is `154` changed lines (`43+5` production, `105` Service tests, `1` runtime-path test), below the 400-line slice budget. Current blob hashes: `AntiTamperMonitor.cs` `36c6223b43a3eb81efcc0e745dcaa668a747c73a`; `AntiTamperMonitorTests.cs` `b7093301ab07945682b7dba25b72ef38f150efcd`; `IntegrityRuntimePathTests.cs` `2c5b4a70cad430b0e1c49b03c0f3b923cda16b99`.
- Tasks remain intentionally unchanged at `9/14`; no verification report or commit was created. The coverage-only intermittent miss is recorded as an environment/scheduling flake because the same test passed isolated with coverage and the final full coverage host passed without source changes.

## Unit 2 — Durable WNS Convergence / Legacy Quarantine

- Work unit: Unit 2, tasks 2.1–2.3.
- Delivery strategy: feature-branch-chain; PR 2 targets `feat/sdd7-1-scheduler-ipc` at `e6ea7ceaf49c71edef9b9f3c7c3f71cb89a270e2`.
- Review boundary: durable WNS convergence and production legacy-owner quarantine only. No Unit 3+ implementation.
- Changed CODE+TEST lines: 55 (`git diff --numstat`: 39 additions + 16 deletions); below the 400-line Unit 2 budget. The cumulative OpenSpec artifacts are intentionally untracked and excluded from code/test accounting.

### 2.1 RED

- Added `RestartReconcile_DoesNotReplayRevokedIntent`, which exposed that a persisted terminal `Denied` result was replayed after restart.
- Added composition assertions that the durable coordinator/reconciliation remain registered exactly once and that legacy `IPushNotificationService` and `WnsNotificationServiceHostedAdapter` production registrations are absent.
- RED evidence 1: `dotnet test ... --filter "FullyQualifiedName~UIMessageHandlerWnsTests.RestartReconcile_DoesNotReplayRevokedIntent"`; failed with `Assert.Null() Failure`, actual `Accepted` from the restored backend.
- RED evidence 2: `dotnet test ... --filter "FullyQualifiedName~HostRegistrationTests.ProgramCs_DoesNotRegisterLegacyWnsOwners"`; failed because `Program.cs` still contained `AddSingleton<IPushNotificationService>`.

### 2.2 GREEN

- `WnsRegistrationCoordinator.ReconcileAsync` now treats `Denied` as terminal alongside `Accepted`, preventing revoked/forbidden intent replay while allowing a new operation ID to replace it through `RegisterAsync`.
- Removed only the legacy production registrations from `Program.cs`; `WnsNotificationService.cs` and `WnsHostedService.cs` remain intact for tests and seams.

### 2.3 REFACTOR / VERIFY

- Focused WNS/composition/host command passed: `76 passed, 0 failed, 0 skipped`.
- Full Service command passed: `1,167 passed, 0 failed, 0 skipped`; xUnit reported one pre-existing duplicate test ID skip notice while total executed result remained green.
- Affected Service build passed: `0 errors` (existing warning corpus).
- Full App.UI command passed: `186 passed, 0 failed, 0 skipped`.
- Coverage command passed: `1,167 passed, 0 failed, 0 skipped`; Cobertura artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit2-20260820/45eb9508-edf9-4e71-b739-aba0ee268a7d/coverage.cobertura.xml`. `WnsRegistrationCoordinator.cs` line-rate `100%`, branch-rate `78%`; reconciliation service line/branch rate `100%`.
- `git diff --check` passed. No URI, backend body, credential, or secret is added to result `ToString()` assertions/logging.

### TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 2.1 — WNS convergence and composition acceptance | PASS — revoked restart test and legacy-registration absence test failed before production edits; raw failures were preserved in terminal tool output, with exact failing assertions recorded above. | PASS — both new RED tests passed after the minimum coordinator/Program changes. | PASS — existing lifecycle suite triangulates offline pending replay, expiry, identity denial, idempotency/conflicting operation IDs, cancellation, redaction, bounded backend retry, and one-shot restart reconciliation; revoked terminal state adds a distinct restart path. | PASS — pre-change focused baseline could not execute before restore because linked worktree test assets were absent (`NETSDK1004`); after a non-source restore, affected tests and full Service/App.UI safety nets passed. | PASS — no new transport/retry owner; terminal denial is explicit and legacy classes remain retained. |
| 2.2 — Durable coordinator and legacy quarantine | PASS — missing terminal-denial convergence and legacy production wiring were demonstrated by failing tests. | PASS — focused Unit 2 suite `76/76`; affected Service build `0 errors`. | PASS — `Accepted` remains completed, `PendingOffline` replays once, `Denied` does not replay, and new operation IDs remain admissible; durable and legacy registration counts are asserted. | PASS — full Service `1,167/1,167`; full App.UI `186/186`. | PASS — scope limited to one coordinator condition and removal of the two legacy production registrations. |
| 2.3 — Refactor / verification | PASS — no additional production RED fabricated; existing lifecycle behavior was already green and is classified as triangulation. | PASS — fresh focused/full/build/coverage commands all exited 0. | PASS — WNS coordinator coverage and composition assertions exercise the scenario-4 state paths locally. | PASS — Cobertura and `git diff --check` artifacts retained; external WNS/backend receipts remain unclaimed. | PASS — no Unit 3+ files or behavior changed. |

### Test Summary

- Total new tests: 3 (two legacy/durable composition assertions and one revoked restart assertion).
- Total focused Unit 2 tests passing: 76.
- Full Service tests passing: 1,167; full App.UI tests passing: 186.
- Layers: Service unit/integration-style harness tests and source-level composition tests; App.UI regression.
- Approval tests: None — production changes were behavior additions/quarantine, not broad refactoring.

## Cumulative Task State Confirmation

- `tasks.md`: `1.1`–`1.3` and `2.1`–`2.3` are checked.
- Historical Unit 2 confirmation: all Phase 3, Phase 4, and Phase 5 tasks were unchecked at that point.

## Unit 3 — Foreground Realtime Acceleration

- Work unit: Unit 3, tasks 3.1–3.3; implementation scope limited to App.UI Realtime source/tests.
- Delivery strategy: feature-branch-chain; PR 3 targets `feat/sdd7-2-wns-convergence` at `eea01b4e32de61b49034673fcd40805799387c43`.
- Review boundary: foreground-only typed UI acceleration, lifecycle serialization, generation ownership, stale callback suppression, and no Service/REST/retry authority.
- Current changed CODE+TEST budget: `367` lines (`193 additions + 76 deletions` production; `98` additions tests), below the 400-line Unit 3 limit. OpenSpec artifacts are excluded.

### 3.1 RED

- Added barrier/lifecycle tests for typed foreground refresh, background isolation, stale connect completion, cancellation, and exactly-once foreground resubscription.
- RED command (actual, `--no-restore --no-build`):

  ```text
  dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~RealtimeSubscriberTests"
  ```

- Start: `2026-08-20T17:00:40.4423135-05:00`; exit `1`.
- Failure: test DLL was absent from the linked worktree; no behavioral RED could execute. This is recorded as infrastructure-blocked, not fabricated behavioral evidence.

### 3.2 GREEN

- `RealtimeSubscriber` now serializes lifecycle operations through one gate, captures a monotonically increasing generation, cancels and invalidates the active generation on disconnect/background, attaches generation-capturing handlers per subscription, suppresses stale events, and exposes `LifecycleTask` for observable lifecycle completion.
- Realtime remains entirely in App.UI. No Service, REST, scheduler, identity, policy application, retry, or enforcement code was changed.
- Initial production execution was blocked because the worktree lacked `project.assets.json`; scoped restore was subsequently authorized and completed without tracked dependency drift.

### 3.3 REFACTOR / VERIFY

- Historical pre-restore refactor check: the existing subscriber used a lifecycle gate and explicit generation cleanup; `git diff --check` passed at `2026-08-20T17:03:30.2909722-05:00` (exit `0`).
- Historical pre-restore affected build: exit `1`, `NETSDK1004` because `tests\ControlParental.App.UI.Tests\obj\project.assets.json` was absent.
- Historical pre-restore focused/regression state was pending; executable verification is recorded below. No external/live Realtime or backend claim is made.

### TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 3.1 — Lifecycle/barrier acceptance tests | Historical pre-restore RED attempt was blocked by missing test DLL (`2026-08-20T17:00:40.4423135-05:00`, exit 1); no behavioral failure claimed. | Historical pre-restore blocked; final focused suite passed `11/11`. | PASS — typed refresh, post-background ignore, stale completion, cancellation, and duplicate foreground restart use distinct paths. | Historical pre-change `--no-restore` safety net failed with missing DLL (`2026-08-20T16:59:28.3944048-05:00`, exit 1); final regression passed. | PASS — immediate background invalidation closes the race. |
| 3.2 — Generation-safe subscriber | Tests reference behavior absent from the prior subscriber. | Historical pre-restore blocked; final affected builds exit `0`. | PASS — failed/stale generation, exactly-once resubscribe, and typed events are exercised. | Historical missing-assets block; final App.UI/Service safety nets passed. | PASS — single gate, cancellation ownership, and no second authority path. |
| 3.3 — Refactor / verify | N/A — verification task; no production RED fabricated. | Historical pre-restore blocked; final builds/tests/coverage passed. | Historical pending state closed by final focused/full regression execution. | Historical missing-assets block; final safety nets and coverage passed. | PASS — `git diff --check` exit 0; task is now checked. |

### Task State Confirmation

- Final `tasks.md` confirmation: Units 1–3 are checked; Units 4 and 5 remain unchecked.

### Remaining Tasks

- Phase 4 tasks 4.1–4.3.
- Phase 5 tasks 5.1–5.2.

### Issues / Deviations

- Historical issue: the initial no-restore boundary prevented compilation/test execution because the linked worktree lacked `project.assets.json` and the test DLL; authorized scoped restore resolved it without tracked dependency drift.
- `LifecycleTask` is a public App.UI observability seam used by lifecycle tests; it does not grant authority or expose transport internals.

## Remaining Tasks

- Phase 4 tasks 4.1–4.3
- Phase 5 tasks 5.1–5.2

## Unit 2 Issues / Deviations

- The initial safety-net command was blocked by missing linked-worktree `project.assets.json`; restore was required to execute tests and did not modify tracked dependencies or source. The warning corpus includes existing package compatibility/version warnings.
- `ServiceHostStartTests` and `ServiceCompositionTests` existing local harnesses do not construct the complete production `Program.Main` graph; source-level `HostRegistrationTests` assertions therefore prove legacy absence while the existing durable composition tests prove the retained production registration path.
- External WNS/backend receipts remain pending and are not claimed.

## Unit 3 Executable Verification Completion

- Scoped restore was authorized and completed from existing project/package sources:
  - App.UI tests restore: `2026-08-20T17:07:32.1426539-05:00`, exit `0`.
  - Service tests restore: `2026-08-20T17:07:38.5368756-05:00`, exit `0`.
  - Post-restore status showed only the two intended source/test modifications and untracked cumulative OpenSpec artifacts. No tracked project, props, targets, lockfile, config, source, or dependency declaration changed.
  - Restore warnings were pre-existing package resolution warnings (`NU1601`, `NU1701`); no package versions were edited.
- Final affected builds and regressions all passed with `--no-restore`:
  - App.UI product build: `2026-08-20T17:12:03.2517966-05:00`, exit `0`.
  - App.UI test build: `2026-08-20T17:12:11.6075342-05:00`, exit `0`; post-fix test build: `2026-08-20T17:14:16.7764254-05:00`, exit `0`.
  - Focused Realtime suite: `2026-08-20T17:14:16.7764254-05:00`, `11/11`, exit `0`.
  - Full App.UI regression: `2026-08-20T17:14:20.0917107-05:00`, `189/189`, exit `0`.
  - Full Service regression: `2026-08-20T17:12:27.0459716-05:00`, `1,171/1,171`, exit `0`; one existing duplicate-ID case was skipped by xUnit.
- Coverage: `2026-08-20T17:14:31.2654869-05:00`, `189/189`, exit `0`; Cobertura artifact `tests/ControlParental.App.UI.Tests/TestResults/coverage-unit3-final2-20260820/1e4da48e-2c2f-41e5-b035-75febafc80f8/coverage.cobertura.xml`. `RealtimeSubscriber` line rate `89.22%`, branch rate `74.07%`.
- Final diff check passed; final tracked CODE+TEST budget is `367` changed lines (`193 additions + 76 deletions` production, `98 additions` tests). No Service production diff, WNS/scheduler/integrity leakage, or Unit 4+ implementation.

### Updated Unit 3 TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 3.1 — Lifecycle/barrier acceptance tests | PASS — tests were written first; initial execution was infrastructure-blocked, then focused execution passed `11/11`. | PASS — focused suite `11/11`, exit `0`. | PASS — typed refresh, immediate background isolation, stale completion, cancellation, and exactly-once restart paths execute. | PASS — pre-change missing-asset failure is retained as historical evidence; fresh App.UI regression `189/189`. | PASS — immediate background invalidation closes the race before queued cleanup. |
| 3.2 — Generation-safe subscriber | PASS — new behavior tests preceded implementation. | PASS — affected App.UI product/test builds exit `0`; focused `11/11`. | PASS — per-generation handlers, cancellation ownership, stale callback suppression, and lifecycle observability are covered. | PASS — App.UI and Service regression suites remain green. | PASS — one lifecycle gate, no fire-and-forget lifecycle calls, observed queued exceptions, and no second authority path. |
| 3.3 — Refactor / verify | N/A — verification task; no RED fabricated. | PASS — final builds/tests/coverage exit `0`. | PASS — foreground typed events, background late events, stale restart, duplicate prevention, and unchanged Service/polling boundary verified locally. | PASS — full App.UI `189/189`, full Service `1,171/1,171`, coverage artifact, and diff-check. | PASS — final budget `367/400`; task checked. |

### Final Task State Confirmation

- `tasks.md`: Units 1–3 tasks are checked; Units 4 and 5 remain unchecked.

### Unit 4C2 C1B2 Final Independent Closure — 2026-08-26

- C1B2 is closed by committed `e5f9fc2` (`fix(service): harden durable owner stale faults`) with final independent verdict **PASS**: focused C1B2 `12/12`, inherited lifecycle `8/8`, AntiTamperMonitor `98/98`, and exact-exclusion safety `1299/1299`.
- Changed production coverage is `24/24` lines and `23/24` branches; exact CODE+TEST budget is `196/400`. Only `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` were committed.
- Lifecycle distinction: admitted decision chains drain across shutdown; stale suppression belongs to startup rehydration. C1C must base on `e5f9fc2`.

## Unit 4C2C1B Second Remediation — 2026-08-26

- Scope: only `AntiTamperMonitor.cs`, `AntiTamperMonitorTests.cs`, and this progress artifact. Immutable verifier reports were not edited; Handler/Program and prohibited retry/lock/migration/framework scope were untouched. Task `4.2` was unchecked before implementation and remains unchecked (`10/16`).
- Behavior-first RED: extended `DurableOwner_SavesAcceptedStateBeforeReactionAndNotification_WithExactKeys` with a restart and distinct later key `reaction-later`; pre-fix focused run failed at the new persisted-state assertion while existing 10 cases passed (terminal output: `tool_03f52189e0017Z0RFQminJEmta`). No external immutable RED log/hash was created; this is not claimed as a hash-backed gate.
- Production correction: removed the broad rehydrated-completed-reaction early return; added active-generation checks before/after effect and persistence awaits and stale-generation catch suppression; recovery and progress saves now stop after invalidation.
- Test strengthening: exact cumulative revoked assertions cover 1/2/3/fourth unchanged state/deadline; recovery test covers three trust observations and revoked reset; agent-death test calls `RecordAgentDeath` and restarts; recording store calls full `value.Validate()`; stale test blocks Save and proves no downstream enforcement effect.

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.2 C1B second remediation | PASS — genuine restart/distinct-key RED observed before production edit; no hash-backed immutable external log exists. | PASS — focused DurableOwner `11/11`, exit `0` after correction. | PARTIAL — restart key, revoked progression, three-trust/reset, agent death/restart, validating envelope, and blocked-save stale path execute; effect-fault, notification-fault, full save-failure retry, reaction/notification blocked races, and fresh coverage remain unproven. | NOT RUN — no unfiltered full suite; affected build was exercised by focused test compilation, but exact safety/coverage gates were not rerun. | PASS — no new production files/fields/APIs; broad guard removed and checks kept owner-local. |

- Exact current code/test diff: production `152 additions + 4 deletions`; tests `241 additions + 1 deletion`; total **398/400** additions+deletions (including deletions). `git diff --check` was clean. Staged files: `0` observed. Untracked `.codegraph/` remains externally locked and was not modified.
- Status: **BLOCKED / reslice required**. Remaining mandated behavior-first scenarios and independent safety/coverage evidence cannot truthfully fit the remaining 2-line budget. Do not check `4.2` or claim readiness. Minimal reslice: allocate a fresh C1B remediation slice (recommended 120–180 CODE+TEST lines) for save/effect fault retryability, cancellation-ignoring reaction/notification/fault races, and portable coverage/safety evidence.

## Unit 4C2C1B Remediation — Current Status

- Scope remains limited to `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs`; the independent verifier report remains unchanged.
- Remediation added durable owner reconciliation coverage and implementation for rehydrate-before-remote, save-before-effect, exact idempotency keys, recovery-latch behavior, stale generation suppression, and completion persistence.
- Focused durable-owner GREEN: `11/11`, exit `0`.
- Full `AntiTamperMonitorTests`: `89/89`, exit `0`.
- Affected Service test project build: `0 errors`, existing warnings only.
- Full Service regression: `1,290 passed, 2 failed, 0 skipped, 1,292 total`; failures are unrelated pre-existing/environmental failures in `NamedPipeUIServerHostedAdapterTests` (access denied) and `TaskSchedulerBackupServiceTests` (Task Scheduler result false).
- Solution `--no-restore` build was blocked by missing `project.assets.json` files. Locked restore was attempted but refused because the existing lock file is inconsistent with a project reference; no lockfile or project file was changed.
- Current CODE+TEST diff is `360` changed lines (`148 additions + 4 deletions` production; `212 additions` tests), within the hard `400` limit. No exception used.
- `tasks.md` intentionally keeps `4.2` unchecked and cumulative state at `10/16` until independent verification gates fully pass.

### Remediation TDD Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.2 C1B remediation | PASS — focused remediation initially failed `5/11`; raw artifact `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-remediation-red2.log`, SHA256 `862B154E1F5708FFA1741A48F4E273DA599ADAFEC75D0D9E624FB736AA7FD416`. | PASS — durable-owner `11/11`; full AntiTamper `89/89`, raw artifact `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-remediation-antitamper-green3.log`, SHA256 `F2846BF9B77D2C702C36A7FBD8CD64E2F05F4775C20C499C738D0287889E54F1`. | PASS — domain validation is enforced by the recording store; restart no-op, pending reaction/notification reconciliation, recovery latch, exact keys, and stale generation cases pass. | PARTIAL — affected build passes; full Service has two unrelated failures and solution build needs a valid restore. | PENDING — remaining hard gates: safety baseline, coverage, diff-check, final budget/path audit, and independent re-verification. |

## Unit 4C2C1B Remediation — Authoritative Closure

- Remediation was completed without further production/test edits after the final focused GREEN. Independent report was not edited; no dependencies were restored/changed; no commit, stage, remote, or history operation was performed.
- Exact excluded Service safety command:
  `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --configuration Debug --filter "FullyQualifiedName!~NamedPipeUIServerHostedAdapterTests.StartAsync_ExposesUiPipeBeforeClientConnects&FullyQualifiedName!~TaskSchedulerBackupServiceUnregisterTests.UnregisterBackupTasksAsync_WhenNoTasks_ReturnsTrue" --verbosity minimal`
  Result: `1,290 passed, 0 failed, 0 skipped, 1,290 total`, exit `0`; log `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-owner-safety-excluded.log`, SHA256 `4DE687EEB15B26C295A0CB643BFEDDE172F8186E3CD443408ECB7E2773DB8F87`.
- The two excluded failures are inherited C1A host-baseline/environment warnings, not C1B blockers. The solution locked-restore/project-reference inconsistency is recorded as a pre-existing environment warning; solution restore was not attempted again.
- Fresh unique external coverage command targeted exactly `AntiTamperMonitorTests`: `89/89`, exit `0`. XML: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-coverage-20260826-unique\54c5b926-6fdc-4f4e-a841-7e0b7ae042b1\coverage.cobertura.xml`; XML SHA256 `158CC7A16F867D855752FE827FC7E8AFACF0713D17AF7B645D17036FCCC21530`; log SHA256 `5DFB186CBDDE7DC9ACAFE2B3ED466B397C91BED0B794ECCCFBFF67F20E0ACB21`. `AntiTamperMonitor` line-rate `98.07%`, branch-rate `81.06%`.
- Coverage hit evidence: `RehydrateAsync` `100%/91.66%`; `PrepareDurableStateAsync` `100%/93.75%`; `MergeCompletedProgress` `100%/86.66%`; `ReconcileDurableEffectsAsync` `100%/77.77%`; `SaveEffectProgressAsync` `100%/100%`; `SaveStateAsync` `100%/100%`; `ExecuteDecisionChainAsync` `94.28%/88%`; `DrainGenerationAsync` `100%/95.45%`; `PerformIntegrityCheckAsync` `87.5%/80%`; `PerformBinaryIntegrityCheckAsync` `85.93%/71.42%`; `IsGenerationActive` and `IsGenerationCurrent` `100%/100%`; `ShouldExecute` `63.63%/50%`. All current C1B async methods/state-machine classes were exercised; meaningful class coverage exceeds 80% line and branch.
- Fresh affected build: `0 errors`, log `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1b-owner-affected-build-final.log`, SHA256 `F4525E7A53BD66E0724FE20B4641C707C4B6B30DCC4783ED8B74D52DC48505AE`.
- Fresh final focused DurableOwner: `11/11`, exit `0`, log SHA256 `A1624775F1F6552ABBA3F5C5812DC1DCA3D0D8FA67A46434EBC9E068F78A5914`. Fresh final AntiTamperMonitor: `89/89`, exit `0`, log SHA256 `4B52ED6B79FACCC516CF6B06B7FEF91977238B11177FEF6416BD3A87EBA7DDDA`.
- Remediation chronology preserved: `red.log` was an earlier non-authoritative exit-0 artifact (`2C5D2ACC24E788D63D1F03F369AE58711C823596D207245EDB833A98BA2ED539`); authoritative behavioral RED is `red2` (`5 failed/11`, exit `1`, SHA256 `862B154E1F5708FFA1741A48F4E273DA599ADAFEC75D0D9E624FB736AA7FD416`). Intermediate GREEN failures remain immutable remediation chronology: `green.log` exit `1` SHA256 `8EEAF18EEC1FC257AD7FD047BDA39661645563B319F017716DAD1F7FF8005CBD`; `green2.log` exit `1` SHA256 `2F730CDA08A8503E9A2283963E46AAC0C0CB7D7A6E62CA7DECD6ABF2AB72CACE`; `green3.log` exit `1` SHA256 `D71C33F05755AFE684EE314D06D1D8CA128D3235CEB5FDA1605938A94CED8D79`; `green4.log` exit `1` SHA256 `933C189C92BC840900DF55560771CAF005B2192FF7586340E5AC3B36FD4D4D9D`; `green5.log` exit `1` SHA256 `DEA8CDA8A4FB133F1EC6064F52495F33D56C7B4BB066C3E66DE5621754AE97EB`; `green6.log` exit `1` SHA256 `377D9E86D75A49B77FA3360DBABA8CE57CE8D2C34D97AC540147FF403035E067`; `green7.log` exit `1` SHA256 `DCCA2637B03BCF5E6CEB94325ECFADD21929A3612499E57C76B727120795881E`; final focused `green8.log` is `11/11`, exit `0`, SHA256 `0F49FE6F182811891CFEF581EFEF4E2865D174A0CB296ECE753AC566181D7E5D`; final monitor artifact `antitamper-green3.log` is `89/89`, exit `0`, SHA256 `F2846BF9B77D2C702C36A7FBD8CD64E2F05F4775C20C499C738D0287889E54F1`.
- Final audit: HEAD `f37df4a1036a2969b852f929f9db632c7b424206`; exactly two tracked files changed (`AntiTamperMonitor.cs`, `AntiTamperMonitorTests.cs`); staged count `0`; Handler HEAD blob `ef67b148fd7d9f3261f3f5a29545100fac0fe21d`; Program HEAD blob `96d75f43f3f694e44c73aaef835ab0d1eeacdd9e`; `git diff --check` exit `0`; untracked paths are expected OpenSpec artifacts plus externally locked `.codegraph` files. CODE+TEST accounting is `360/400` additions (raw git numstat includes four deletions, `364` total diff rows); no size exception.

### Authoritative TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.2 C1B remediation closure | PASS — behavioral RED `red2`, `5/11` failed before final corrections; all intermediate GREEN failures retained and named as chronology, never relabeled. | PASS — final DurableOwner `11/11` and AntiTamperMonitor `89/89`; final immutable logs/hashes recorded above. | PASS — rehydrate-before-remote, durable accepted-state ordering, exact reaction/notification keys, independent reconciliation, recovery latch, cumulative completion, restart no-op, and stale generation suppression are covered. | PASS — exact excluded safety `1,290/1,290`; affected build `0 errors`; external portable-PDB coverage `98.07%` line / `81.06%` branch; inherited/environment warnings documented. | PASS — no source/test edits after final focused GREEN; exact two-file scope, `360/400` declared budget, `git diff --check`, staged `0`, Handler/Program HEAD blobs, and unchanged verifier report confirmed. |

## Current Task State

- `tasks.md`: C1B task `4.2` is checked; cumulative Phase 4 state is `11/16`.
- Status: **C1B remediation complete; ready for independent re-verification.**

## Unit 4C2C1A — Handler Durable State Apply

- Scope: C1A only, rebuilt from clean B `1fc5b60dc801bbb12d25f2131cb17201c4f04b47`; only `IntegrityVerdictHandler.cs` and `IntegrityVerdictHandlerTests.cs` changed. Rejected candidate patches were not read, copied, or applied.
- Implementation: added pure `Snapshot()`/`Restore(IntegrityEscalationState)`, fixed policy/schema identity validation, rollback fail-closed validation, definitive-only epoch/sequence allocation, durable phase/deadline/latch/effect identity mapping, and semantic authoritative recovery state. Non-definitive, malformed, unavailable, cancelled, and invalid local-failure paths preserve the durable snapshot.
- Focused tests added first: snapshot/restore and rejection, non-definitive matrix, cancellation identity, first-through-fourth revoked/deadline origin, trust cancellation and one-shot three-trust recovery, and rollback/invalid timing atomic rejection.
- Rejected candidate history remains preserved above, including the incomplete C1 attempt and its explicit limitations; no prior evidence was relabeled.

### Authoritative Six-Column TDD Ledger

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| C1A behavior-first | PASS — tests were written before implementation. `c1a-red.log` is compile/setup evidence only for missing `Snapshot`/`Restore` APIs (18 errors), explicitly not behavioral RED. The historical filename `c1a-green.log` is authoritative behavioral RED: exit `1` with the two documented failures, despite its filename. Raw: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-red.log`; SHA-256 `1C515C4ED16E1F6EDBC36F8A21647C60EB51958D9BCBAF06DA0CD423089E3349`; behavioral RED raw: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-green.log`; SHA-256 `71ED4BA737374BEB6575B5490FC351AE524D94BBCFB45D1119660FF41E0C6FDF`. | PASS — clean append-only `c1a-green2.log` is the authoritative first GREEN after the preserved behavioral RED: exact handler filter `44 passed, 0 failed, 0 skipped, 44 total`, explicit `EXIT: 0`; SHA-256 `3F3584702406C31DD12E41D82ACC56ABD0DA39E4CD288455906D7EC58F7E213A`. The historical `c1a-refactor.log` contains mixed intermediate iterations and is non-authoritative. | PASS — deterministic concurrent public-handler triangulation passed `1/1`, with contiguous sequence/epoch metadata and serial-replay snapshot equality; raw `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-concurrent-triangulation.log`; SHA-256 `BCA8E29C14C71066615BBF237BE525DFFE36B8048994E75D705807739AB6DFE`; classified triangulation, not RED. | PASS — Service build `0 errors`; full Service `1,281 passed, 0 failed, 0 skipped, 1,281 total`; portable coverage `IntegrityVerdictHandler` `96.84%` line / `95.37%` branch, `Snapshot` `100%` / `90%`, `Restore` `93.75%` / `83.33%`; AntiTamper/Program identities unchanged; `git diff --check` passed. | PASS — unchanged clean append-only `c1a-refactor3.log` independently confirmed `44 passed, 0 failed, 0 skipped, 44 total`, explicit `EXIT: 0`; SHA-256 `4BF8677206EAC62D605FA5507BFEB2476813724E34F335857DCD4CB4A3065614`. No further code refactor was required. Historical `c1a-refactor2.log` remains non-authoritative confirmation only. |

### C1A Gate Audit

- Final focused handler: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --filter "FullyQualifiedName~IntegrityVerdictHandler" --no-restore --verbosity minimal`; `44 passed, 0 failed, 0 skipped`, exit `0`; authoritative clean output is `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-green2.log` with explicit `EXIT: 0`.
- Service build: `dotnet build src/ControlParental.Service/ControlParental.Service.csproj --no-restore --configuration Debug`; `0 errors`, existing warning corpus.
- Full Service: `1,281 passed, 0 failed, 0 skipped`, exit `0`; existing duplicate xUnit ID notice remains.
- Fresh portable-PDB coverage command exited `0` with `1,281/1,281` and produced non-empty Cobertura at `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-coverage-portable-results3\e0b0502a-c36f-476d-93fd-c67c36e9f879\coverage.cobertura.xml`; `IntegrityVerdictHandler` is `96.84%` line / `95.37%` branch, `Snapshot` is `100%` line / `90%` branch, and `Restore` is `93.75%` line / `83.33%` branch. Meaningful changed-production method/branch evidence is above `80%`.
- Final tracked CODE+TEST diff: `307` changed lines (`128 additions + 14 deletions` production; `165 additions` tests), under hard `400` but above the forecast `120–220`; no exception used.
- No staged files, commit, remote, verify report, Domain diff, AntiTamper diff, Program diff, or retained `.codegraph/` directory.

### C1A Status

- `tasks.md` marks `4.1` checked after the non-empty portable-PDB coverage evidence. Cumulative task state is `10/16`.
- Status: **ready for independent C1A verification**. The historical filename of the behavioral RED and the failed first post-implementation run remain preserved; no raw artifact was rewritten or renamed.

## Unit 4C2C1 Apply Attempt — 2026-08-25

- Scope: only the four authorized C1 files. `Program.cs` remained byte-identical to B; no C2 composition or runtime-path edits were made.
- The rejected initial C candidate was not read, copied, applied, or used as evidence. This attempt was rebuilt from clean B `1fc5b60dc801bbb12d25f2131cb17201c4f04b47`.
- C1 additions include handler snapshot/restore validation, owner rehydration before remote work, durable save-before-effect hooks, deterministic runtime deadline callback seam plus production `Timer`, and lifecycle deadline disposal.
- CODE+TEST diff: `180` changed lines (`99` production, `81` tests), below the hard `400` limit. This is an incomplete implementation attempt and is not independently verification-ready.

### Unit 4C2C1A Coverage / Chronology Closure — 2026-08-25

- The retained first post-implementation behavioral run is authoritative RED despite the historical `c1a-green.log` filename, and is not relabeled or rewritten. It failed two handler tests: `DefinitiveRevoked_AllocatesMonotonicallyAndPreservesThirdDeadline` (deadline origin was two seconds earlier than expected) and `Restore_RollbackOrInvalidTimingFailsClosedWithoutPartialApply` (`Assert.Throws` expected `IntegrityEscalationStateException`, but no exception was thrown). Raw log: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-green.log`; SHA-256 remains `71ED4BA737374BEB6575B5490FC351AE524D94BBCFB45D1119660FF41E0C6FDF`.
- Fresh concurrent triangulation test passed alone: `1 passed, 0 failed, 0 skipped`, explicit `EXIT: 0`; it used a start barrier, mixed definitive `revoked`/`trust` public calls, returned sequence/epoch metadata, contiguous allocation assertions, and serial-replay `Snapshot()` equality. Raw log: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-concurrent-triangulation.log`; SHA-256 `BCA8E29C14C71066615BBF237BE525DFFE36B8048994E75D705807739AB6DFE`. Existing production passed, so this is triangulation rather than RED.
- Clean append-only GREEN: exact handler filter `44 passed, 0 failed, 0 skipped, 44 total`, explicit `EXIT: 0`; raw log: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-green2.log`; SHA-256 `3F3584702406C31DD12E41D82ACC56ABD0DA39E4CD288455906D7EC58F7E213A`. The historical `c1a-refactor.log` remains a mixed, non-authoritative artifact; `c1a-refactor2.log` remains historical confirmation only.
- Separate unchanged confirmation: exact handler filter `44 passed, 0 failed, 0 skipped, 44 total`, explicit `EXIT: 0`; raw log: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-refactor3.log`; SHA-256 `4BF8677206EAC62D605FA5507BFEB2476813724E34F335857DCD4CB4A3065614`. No further code refactor was required.
- Fresh portable-PDB coverage produced a non-empty Cobertura artifact at `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1a-coverage-portable-results3\e0b0502a-c36f-476d-93fd-c67c36e9f879\coverage.cobertura.xml`; SHA-256 `C2BA86E448026B5C7F6E54B01AB56C2168246180E21B32D19968B1FA60B66787`. `IntegrityVerdictHandler` reports `96.84%` line and `95.37%` branch coverage; `Snapshot` reports `100%` line / `90%` branch; `Restore` reports `93.75%` line / `83.33%` branch. This is meaningful changed-production method/branch evidence above the `80%` gate.
- External output/intermediate coverage attempts failed with `NETSDK1004`/`NETSDK1005` because external `project.assets.json` was missing or incompatible; they are not used as coverage evidence.
- Final audit for this slice: no staged files, no commit/remote/history operation, `git diff --check` passed, and only the two authorized production/test files are tracked changes. `.codegraph/` remains absent.
- `tasks.md` task `4.1` remains checked; cumulative apply state is `10/16`. Exact CODE+TEST diff is `307` changed lines (`128 additions + 14 deletions` production; `165 additions` tests), below the hard `400` limit. No verify report or commit was created. Status: **ready for independent C1A verification**.

### Authoritative Six-Column TDD Ledger

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| C1 behavior-first | PASS — tests were written first; exact command failed compilation with 8 missing API errors. Full raw log: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1-red.log`; SHA-256 `4134F8381F01CD04E3E37165F375510B35BFBEF681DC0988018112D8C8EEDBD0`; available. | PASS — focused filter compiled and passed after the minimum implementation; full raw log `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1-green.log`; SHA-256 `B36D20891FA4323E2A6BC640147E5A61A96114FA79BBFC0AD4ECE3928431ADB1`; available. | PARTIAL — snapshot/restore, missing-state load, save ordering, and deterministic before/due callback behavior passed; independent pending reaction/notification recovery, corrupt/fault/cancel matrix, restart/no-extension, stale-generation callback suppression, and exact keyed reaction forwarding are not fully proven. | PASS — Service build: 0 errors; full Service suite: 1,276 passed, 0 failed, 0 skipped; isolated coverage host passed 1,276/1,276. | PASS — final focused run passed; raw log `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1-refactor.log`; SHA-256 `9FF9F9F36A89B6ACD6825FF4C31D1A25929632CF7FB0B606D58D0D5A3B75A963`; available. |

### Verification Inputs (not a verify report)

- Focused command: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --filter "FullyQualifiedName~IntegrityVerdictHandler|FullyQualifiedName~AntiTamperMonitor" --no-restore --verbosity normal`; final exit `0`.
- Coverage: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c1-coverage\5a9aa2ea-7180-4fd0-8fe0-141c538e4fcb\coverage.cobertura.xml`; `IntegrityVerdictHandler` line `100%`, branch `95.83%`; `AntiTamperMonitor` line `99.04%`, branch `81.11%`. `TriggerDeadlineCallbackAsync` method line `100%`, branch `50%`; `ScheduleDeadline` line `100%`, branch `62.5%`.
- Program identity check: `git hash-object -- src/ControlParental.Service/Program.cs` equals `git rev-parse HEAD:src/ControlParental.Service/Program.cs` (`96d75f43f3f694e44c73aaef835ab0d1eeacdd9e`). `HEAD` remains B. `git diff --check` passed.
- Warnings: existing analyzer/package warning corpus; new test warnings include documentation/style warnings. No Domain gate was run because no Domain diff exists.

### C1 Status / Limitations

- `tasks.md` remains cumulative `9/14`; `4.1` is intentionally unchecked pending independent verification. No commit, stage, remote, history rewrite, or verify report was created.
- The implementation is **REJECTED for independent C1 PASS / blocked** because the contract is not fully proven: pending reaction and notification reconciliation is not independently implemented end-to-end, reaction does not yet pass the existing keyed monitor overload in the production path, and corrupt/unavailable/cancellation plus stale/restart suppression behavior lacks the required named runtime evidence.


## Unit 4C2B Initial B Apply Evidence — Recovered 2026-08-25

- This is authoritative recovered evidence from the original scaffold run; no raw artifact is claimed.
- Exact command:

  ```text
  dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --filter "FullyQualifiedName~DurableIssueStoreTests|FullyQualifiedName~EnforcementLevelMonitorSafetyBranchTests" --verbosity normal -clp:ErrorsOnly
  ```

### Six-Column Strict-TDD Evidence — Initial B

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Initial B scaffold | **PASS — 19 total: 10 passed, 9 failed, nonzero exit.** Exact numeric exit code, skip count, timestamp, and raw-output path are unavailable. Failures: (1) scope/severity/evidence conflict expected `InvalidOperationException`, none; (2) whitespace `" "` expected `ArgumentException`, none; (3) whitespace `"\tkey"` expected `ArgumentException`, none; (4) oversized key expected `ArgumentException`, none; (5) keyed monitor test raised `NullReferenceException` in `ToEnforcementIssue`; (6) resolved replay expected `IsReplay == true`, found false; (7) first/replay expected `LastIdempotencyKey == "key-1"`, found null; (8) restart replay expected `IsReplay == true`, found false; (9) concurrent calls expected one non-replay, found 16. | **PASS — same exact command; 19/19 passed.** Exact skip count and numeric exit code are unavailable. | **PASS — the nine scaffold failures covered conflict, validation, replay/restart/resolved/concurrency, and monitor behavior.** | Passing tests retained: `EmptyIdempotencyKeyPreservesLegacyOccurrenceBehavior`; `DurableSemanticIssueAndRecoveryUseStoreAndUpdateAuthoritativeHealth`; `SevereSemanticIssueAndResolutionImmediatelyUpdateAuthoritativeHealth`; `ForegroundHeartbeatSilenceProducesScopedHookTimeout`; `DurableRestoreFailureStaysDegradedAndBlocksHealth`; `EnforcementMonitorRestoresOnlyActiveSemanticIssues`; `RepeatedSemanticEvidenceAndResolutionSurviveRestart`; `ConcurrentEvidenceForOneSemanticKeyProducesOneBoundedRecord`; `CorruptDocumentFailsRestoreInsteadOfReturningHealthyEmptyState`; `AuthoritativeProbeRecoveryPersistsResolutionEvidence`. | Initial scaffold was replaced by the minimum keyed implementation; no raw artifact or timestamp is fabricated. |

## Unit 4C2B Proportional Remediation — 2026-08-25

- Scope remained exactly the existing six B code/test files. No AntiTamper, Program, outbox, Unit 5, history, remote, commit, or verification report changes.
- Confirmed remediation only: abstract keyed `IIssueStore` overload; fail-closed keyed `IEnforcementLevelMonitor` default; persisted issue-document bound at `MaximumRecords`; oversized-key length check precedes whitespace scan.
- Added only the requested tests: 1025-record persisted-document rejection, Moq `CallBase` interface-default fail-closed behavior, and exact 256-character key acceptance by triangulating the existing first-keyed test.
- Added the requested monitor first-apply intent assertions before replay: exact key/severity/description/occurrence, `Degraded`, one blocking-health `true`, and one `IssueDetected`; this is triangulation only and claims no RED.

### Six-Column Strict-TDD Evidence — B Remediation

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Persisted-record bound | PASS — 1025 unique persisted records were accepted by the prior implementation instead of throwing `InvalidDataException`. | PASS — `EnsureLoadedAsync` rejects `Issues.Length > 1024` before `ToDictionary`. | PASS — existing 1024-record capacity remains unchanged; rejection occurs during load, before keyed scan or mutation. | PASS — focused B suite `21/21`; full Service `1,272/1,272`. | PASS — reused `MaximumRecords`; no new index, registry, migration, or abstraction. |
| Interface default safety | PASS — Moq `CallBase` implementation without keyed support completed silently against the prior delegating default. | PASS — keyed default throws `NotSupportedException("Keyed issue admission is not supported.")`; Service override remains production path. | PASS — legacy default remains compatible; keyed unsupported implementations cannot silently drop the key. | PASS — focused monitor safety tests `7/7` relevant cases; Domain `145/145`; Service build passes. | PASS — one-line fail-closed contract change; no fake hierarchy or owner expansion. |
| Key validation boundary | PASS — existing 257-character and whitespace rejection remained in place; exact 256 acceptance was added as triangulation. | PASS — exact 256 key applies/replays; oversized keys short-circuit length before whitespace enumeration. | PASS — 256/257/whitespace equivalence classes are covered without permutation growth. | PASS — focused B suite `21/21`; keyed store core remains above 80% changed-path coverage. | PASS — no new constant/type; validation remains local and bounded. |

### Remediation Execution / Budget

- Genuine RED focused result: `2 failed, 19 passed, 21 total` (oversized persisted document and keyed interface default).
- GREEN focused result: `21 passed, 0 failed, 0 skipped`.
- Domain build passed; Domain tests passed `145/145` after the linked-worktree assets were restored by the normal test command.
- Service build passed with the existing warning corpus; full Service passed `1,272/1,272` with the existing duplicate-ID discovery notice.
- One isolated focused coverage host passed `21/21`. Changed executable paths: `FileIssueStore` keyed core `88.37%` line / `92.85%` branch; `EnsureLoadedAsync` `100%` line / `78.57%` branch; keyed monitor async path `85%` line / `62.5%` branch; fail-closed default path was executed. Aggregate class rates include unrelated pre-existing monitor/store paths.
- Targeted monitor test passed `1/1`; the full two-class B suite passed `21/21` once. No production/full-Service/coverage rerun was performed for this test/evidence-only remediation.
- Current six-file CODE+TEST diff is `320 additions + 4 deletions = 324 changed lines`; no task checkbox changed and cumulative task state remains **9/14**.

## Unit 4C2C — Owner Rehydrate / Durable Deadline — 2026-08-25

- Work unit: Unit 4C2C only, feature-branch-chain child `feat/sdd7-4c2c-owner-rehydrate`, base `1fc5b60dc801bbb12d25f2131cb17201c4f04b47`.
- Boundary: `IntegrityVerdictHandler.cs`, `AntiTamperMonitor.cs`, `Program.cs`, and existing focused Service tests only. No Domain files, new production files, Unit 5, retry framework, migration, or commit.
- Implementation: pure handler snapshot/restore and fail-closed timing state; AntiTamper sole owner rehydrates before remote admission, reconciles pending reaction/notification, persists before and after effects, schedules the persisted deadline, forwards the unchanged B reaction key when the durable owner is active, and disposes the deadline timer with the generation. Program registers the file state store as a singleton.

### Six-Column Strict-TDD Evidence — Unit 4C2C

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.1 — Pure snapshot/restore and owner acceptance | **PASS — genuine behavioral RED.** Exact focused command failed with `3 failed, 121 passed, 0 skipped, 124 total`; failures were snapshot restore counter/deadline expectation, durable-owner reaction compatibility, and runtime metadata timing. The initial no-restore attempt was infrastructure-blocked by `NETSDK1004` before the authorized minimal restore. Raw terminal output was not retained as a durable artifact. | **PASS — same focused command after minimum implementation/corrections: `0 failed, 126 passed, 0 skipped, 126 total`, exit 0.** | **PASS — exact snapshot state, wall-clock rollback fail-closed, load-before-remote ordering, and keyed B admission were exercised.** | **PASS — affected Service build and inherited integrity matrix remained green; no Domain contract changed.** | **PASS — compatibility fallback is limited to monitors without the C durable owner; production owner uses keyed admission.** |
| 4.2 — Rehydrate/reconcile/persist/deadline owner | **PASS — tests preceded completion of owner behavior; the first focused RED exposed missing durable behavior rather than compile-only failure.** | **PASS — focused command: `dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --configuration Debug --filter "FullyQualifiedName~IntegrityVerdictHandler|FullyQualifiedName~AntiTamperMonitor|FullyQualifiedName~IntegrityRuntimePath" --verbosity minimal`; `0 failed, 127 passed, 0 skipped, 127 total`, exit 0.** | **PASS — rehydrate-before-remote, pending reaction reconciliation, exact reaction key forwarding, persisted timing, rollback fail-closed, and existing stale-generation/cancellation/fault/stop/dispose barriers are covered.** | **PASS — full Service suite `1,276 passed, 0 failed, 0 skipped, 1,276 total`, exit 0; one existing duplicate xUnit discovery notice.** | **PASS — no second async owner, outbox redesign, new durable fields, or new production file.** |
| 4.3 — Closure and coverage | **N/A — closure task; no new production RED fabricated.** | **PASS — focused coverage host exited 0 with `127 passed, 0 failed, 0 skipped`.** | **PASS — Cobertura: `tests\ControlParental.Service.Tests\TestResults\coverage-unit4c2-final\30bda225-5f41-4277-a3ea-7ba8d7249fff\coverage.cobertura.xml`; `AntiTamperMonitor` line `98.63%`, branch `80.48%`; `IntegrityVerdictHandler` line `96.28%`, branch `94.31%`; persistence method line `100%`. The new timer callback remains unhit in this host and is reported as residual risk.** | **PASS — `git diff --check` exit 0; generated `.codegraph` removed; no staged files and HEAD unchanged.** | **PASS — exact implementation diff is `120+ / 4-` AntiTamper, `82` handler, `5+ / 2-` Program, `59` AntiTamper tests, `30` handler tests: **302 CODE+TEST lines**, under the hard cap; no redundant permutations added.** |

### Task State Confirmation

- `tasks.md`: tasks `1.1`–`3.3` and `4.1`–`4.3` are checked; cumulative state is **12/14**.
- Remaining: Phase 5 tasks `5.1`–`5.2` only.

### Issues / Deviations

- Final implementation is below the forecast target (`302` rather than `360–395`) because the existing Unit 4C/B2 barrier suite already covered most lifecycle permutations; no redundant tests or compression were added to spend budget.
- The focused coverage host reports the new timer callback state-machine at `0%` because it does not advance a real persisted deadline. Deadline behavior remains covered by existing handler before/at/after tests; independent verification should require a dedicated timer-host assertion if the threshold is interpreted per generated async method.
- Domain build/tests were omitted because no Domain file or contract was modified. Service build/tests compile and execute the affected Domain dependency.
- Restore was required because the linked worktree initially lacked `tests\ControlParental.Service.Tests\obj\project.assets.json`; it produced only the existing NU1601/NU1701 warning corpus and no tracked dependency drift.

## Unit 4C2A2 — Durable Escalation File Store Rebuild — 2026-08-25

- The first uncommitted A2 candidate is rejected for insufficient behavioral RED: its initial evidence was only a compile-only missing-type failure, and its tests were compressed and reflection-coupled. It is not treated as accepted RED.
- The rebuild remains exactly the approved three-file boundary: `FileIntegrityEscalationStateStore.cs`, one envelope registration in `SharedJsonContext.cs`, and `FileIntegrityEscalationStateStoreTests.cs`. No DI, Program, handler, AntiTamper, issue/enforcement, B/C, Unit 5, project-file, commit, report, history, or remote changes were made.
- Final tests use readable arrange/act/assert bodies, direct internal-constructor access, a valid all-field Pending fixture, real filesystem I/O, and one narrow write delegate only for partial-write cancellation/failure.
- Refactor retained all 13 tests and semantics while extracting store/error/preservation helpers, removing the redundant JSON substring assertion, combining production I/O catches with exception filters, and ordering partial-write bytes before the `entered` signal.

### Six-Column Strict-TDD Evidence — A2

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Rejected first candidate | **REJECTED** — only compile-only missing-type evidence; no behavioral RED claimed | N/A — candidate rejected | Readability/reflection and missing directory-file Save behavior were not adequately proven | N/A | Candidate not accepted for verification |
| Accepted tests plus minimal scaffold | PASS — after final tests were present, scaffold focused run had `12` failing tests and `1` passing missing-file test; full attempt reported `7` failures before timing out on the scaffold’s never-entered cancellation seam | N/A — scaffold intentionally has no behavior | Failures covered persistence, typed errors, identity, cancellation, partial failure, interleaving, and directory-file Save classification | Test compilation succeeded; scaffold RED was behavioral, not compile-only | Scaffold was replaced immediately; no scaffold behavior retained |
| Minimum store implementation | PASS — accepted behavioral RED preceded reimplementation | PASS — focused A2 suite `13/13` | All-field Pending equality, replacement, isolation, typed classification, atomic ordering, partial-half writes, and same-instance serialization pass | Service build and full Service safety net pass | Directory creation is inside typed I/O handling; no extra owner, cache, retry, migration, lock, or monotonic comparison |
| Proportional readability refactor | N/A — behavior was already green; no new behavior claimed | PASS — focused A2 suite `13/13` after refactor | Partial write now completes before cancellation can be requested; helper extraction removes repeated construction/assertion boilerplate without changing cases | Durable atomic `5/5`, full Service `1260/1260`, Domain `145/145`, focused coverage `13/13` | Final readable scope is exactly `330/400`; no one-line test bodies, reflection, or scope expansion |

### A2 Execution / Budget

- Focused command: `dotnet test tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj --no-restore --configuration Debug --filter "FullyQualifiedName~FileIntegrityEscalationStateStoreTests"`; result `13 passed, 0 failed, 0 skipped`.
- Relevant atomic regression: `DurableIssueStoreTests` passed `5/5`. Service build passed with `0 errors`; full Service passed `1,260/1,260`; Domain passed `145/145` after the missing-assets restore.
- One isolated focused coverage host passed `13/13`; Cobertura was inspected from temporary output and removed. Changed-store line/branch inspection remains for independent verification; no aggregate threshold is invented.
- Exact A2 CODE+TEST accounting including untracked files is `152` store additions + `177` readable test additions + `1` context-registration addition = **330/400**; the accepted readability target required the allowed upper bound, with no behavior removed.
- Tasks remain intentionally `9/14`; no checkbox, verify report, commit, history, or remote operation was performed. Status: **Ready for independent proportional A2 verification**.

## Unit 4C2A — Missing Contract Acceptance Assertions — 2026-08-25

- Scope remained strictly Domain tests/evidence only. No production, Service, store, history, verification report, task-checkbox, commit, or remote changes were made.
- Corrected the schema-mismatch case so envelope metadata remains current `(1,1)` while only nested state schema is `2`.
- Added direct invalid `PendingNotificationId` cases for empty, whitespace, and exactly `257` characters. No completed-ID boundary matrix was added because completed IDs must equal already-validated pending IDs.

### Strict-TDD Evidence

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Revised-A state invariants: enum, policy, counters, timing, and effect ordering | PASS — revised-A behavioral cases were authored before the validator correction; the raw RED command/output was not retained and no timestamp is claimed. | PASS — undefined phase, positive/unbounded policy, exact counter/timing boundaries, and effect ordering now pass. | PASS — the matrix covers accepted/rejected boundaries without adding completed-ID redundancy. | PASS — focused class `48/48`; full Domain `145/145`. | PASS — no production change, seam, or new abstraction. |
| Envelope validation/API and schema binding | PASS — the envelope `Validate()` API was absent during the original tests-first compile attempt; no raw compile artifact was retained. | PASS — current `(1,1)` envelope succeeds; unsupported document/schema, nested schema mismatch, null state, and delegated state errors are typed. | PASS — the mismatch now isolates nested state schema `2` while envelope metadata remains current `(1,1)`. | PASS — focused class `48/48`; full Domain `145/145`. | PASS — direct assertions only; no persistence or serialization behavior added. |
| Dual-positive streak behavioral rejection | PASS — tests-first dual-positive case exposed the pre-correction acceptance; raw RED output was not retained, so no command/timestamp is fabricated. | PASS — simultaneous positive revoked/trust streaks are rejected. | PASS — dual-streak rejection is distinct from enum/policy/timing and remains in the focused Domain matrix. | PASS — focused class `48/48`; full Domain `145/145`. | PASS — validator-only behavior preserved; no overengineering. |

### Verification and Audit

- Focused command: `dotnet test tests\\ControlParental.Domain.Tests\\ControlParental.Domain.Tests.csproj --no-restore --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityEscalationStateTests"`; result `48 passed, 0 failed, 0 skipped`.
- Full Domain command: `dotnet test tests\\ControlParental.Domain.Tests\\ControlParental.Domain.Tests.csproj --no-restore --configuration Debug --verbosity minimal`; result `145 passed, 0 failed, 0 skipped`.
- Exact CODE+TEST count: production `131` lines + tests `201` lines = **332/400**, a `+8` increase from `324`, within the revised `320–340` plan and below the hard limit. No size exception.
- `git diff --check` passed with no whitespace diagnostics; untracked-file inspection produced no content diagnostics.
- `git status --short` contains only the intended untracked OpenSpec change root, Domain production file, and Domain test file. `HEAD` remains `c1831498b04ff5fe13f03b88d3c6c1598af3f5f4`.
- `.codegraph/` is absent. No Service/source/history/report/commit/remote changes were made.
- Cumulative task state remains `9/14`; Phase 4 and Phase 5 aggregate checkboxes remain unchanged.

## Unit 4C1B2a2 Fault Determinism — 2026-08-25

- Scope stayed limited to `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs`; no B, outbox, handler, durability, 4C2, Unit 5, report, commit, or task-checkbox changes.
- Neutral testability refactor: added one per-instance internal `Func<Task>?` seam, allocating its `RunContinuationsAsynchronously` removal barrier only when configured; absent production behavior remains unchanged. Existing focused safety tests passed `3/3` before the new behavior test.
- Exact RED: seam-only parent failed `LateNonCancellableFailure_PostRemovalGapRetainsFaultForStop` deterministically: `Assert.Throws() Failure: No exception was thrown`, expected `InvalidOperationException`. The test released the backend first, waited for post-removal seam entry, then called Stop; returned operation retained the exact original failure and effects stayed empty after cleanup.
- GREEN: `CompleteAdmissionAsync` now receives `Generation`; non-cancellation faults call `current.EffectFault.TrySetResult(exception)` before public/owned completion, removal continuation, and the seam. Cancellation bypasses `EffectFault`.

### Six-Column Strict-TDD Evidence — A2

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Behavior-neutral seam | N/A — testability-only change | PASS — existing focused safety tests `3/3` | PASS — seam is per-instance/internal, null by default, async-only, and barrier allocation is conditional | PASS — no pre-existing focused regressions | PASS — no production scheduling change when unset; no static/global hook or public API |
| Late non-cancellable admission fault | PASS — exact deterministic `Assert.Throws` failure (`No exception was thrown`) on seam-only parent | PASS — focused late-fault test observes the same original exception instance/message through work and Stop | PASS — 100/100 fresh-process stress and 20/20 parallel interference runs passed; zero collaborator effects | PASS — combined Slice A/Unit4 matrix `145/145`; full Service `3/3` runs | PASS — one O(1) `EffectFault` receipt and existing owned-drain path; no second owner |
| Cancellation exclusion | N/A — cancellation already bypassed the non-cancellation catch; no false RED fabricated | PASS — deterministic cancellation test completes canceled work, successful Stop, and never enters the seam | PASS — owner-cancellation and caller-cancellation focused paths preserve tokens and zero effects | PASS — included in `145/145`, full Service, and both coverage hosts | PASS — no cancellation-to-fault conversion or extra publication path |

### A2 Runtime Evidence

- Focused GREEN: `5/5` late-fault, cancellation, and inherited safety tests.
- Fresh stress: `100/100` sequential processes for the post-removal fault and Stop-before-failure safety scenarios; `20/20` parallel fresh processes passed.
- Combined Slice A/Unit4 focus: `145/145`.
- Five-build gate: Domain, Service, Service.Tests, App.UI, and App.UI.Tests passed after the required scoped App.UI test restore; existing warning corpus remains.
- Full Service: three consecutive fresh runs passed; Full App.UI: `192/192`.
- Isolated coverage hosts A/B both passed with non-empty Cobertura artifacts: `C:\Users\Usuario\AppData\Local\Temp\sdd7-a2-coverage-a\88b05945-9221-4345-9a76-6ba1e5516967\coverage.cobertura.xml` and `C:\Users\Usuario\AppData\Local\Temp\sdd7-a2-coverage-b\ec9b5826-6a7a-4f6f-9c25-17c55a59a179\coverage.cobertura.xml`. `CompleteAdmissionAsync` reports `1.0` line/branch coverage; non-cancellation and cancellation branches have positive hits.
- Recorded failures: the first 100-process stress shell exceeded its 120-second command limit and was terminated before result claim; the rerun passed `100/100`. The first combined Unit4 run was `144/145` because the existing concurrent-start failure case still expected Stop success after the newly required admission-fault receipt; its expectation was updated to require the original `InvalidOperationException`, and the rerun passed `145/145`. The initial no-restore App.UI/App.UI.Tests builds failed with `NETSDK1004` missing `project.assets.json`; the required scoped restore was run, and both subsequent no-restore builds passed.
- Exact tracked CODE+TEST budget: `36 additions + 9 deletions = 45/400`; no size exception. `git diff --check` passed. Tasks remain `9/14`; no verification report or commit created.
- Final patch hashes: binary diff `3b3f6e56f6dcf7de686126aaa76b59397922595e`; stable patch-id `14b0002acae2a30c0bbdfd8f721278fb9a408fdb`. Full Service runs were `1,239/1,239` each; both coverage hosts were `1,239/1,239`.

## Unit 4C1B2b Effect Dedupe/Retry — 2026-08-24

- Scope is limited to Slice B. Slice A lifecycle ownership remains unchanged; no handler, outbox API, 4C2, Unit 5, task checkbox, report, commit, history, or remote change was made.
- RED first: after the test-only additions and minimal generation-flow seam compiled, clean Slice A replayed the same immutable decision eight times (`8` reactions and notifications instead of `1` each). The replay was deterministic and genuine; the earlier missing-seam compile failure is recorded only as setup evidence.
- GREEN: each generation retains one immutable reaction progress token and one immutable notification progress token. Existing `Generation.Gate` rechecks epoch/sequence/scope/key immediately before each effect; progress commits only after collaborator success, failures leave the relevant domain retryable, and notification retry does not repeat a committed reaction. New generations start empty and exact keys are compared ordinally without parsing or normalization.

### Six-Column Strict-TDD Evidence — Slice B

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Same-decision serialized dedupe | PASS — detached Slice A replay produced `8` reactions and `8` notifications; genuine behavioral RED retained. | PASS — concurrent replay produces one reaction and one notification after gate-time recheck. | PASS — immutable reaction/notification keys are independent and exact. | PASS — AntiTamper `76/76`; full Service `1,245/1,245`. | PASS — bounded progress plus one pending token; no collection/history/second owner. |
| Reaction commit/retry | **PROCESS DEBT** — prior candidate did not retain a genuine behavioral RED; no false RED claimed. | PASS — reaction fault leaves both domains uncommitted; exact retry executes reaction then notification. | PASS — commit follows collaborator success and original fault remains observable through Stop. | PASS — focused B and full Unit 4 matrices pass. | PASS — one generation-scoped progress token. |
| Notification commit/retry | **PROCESS DEBT** — prior candidate did not retain a genuine behavioral RED; no false RED claimed. | PASS — ordinary notification failure commits reaction only; exact retry skips reaction and reuses key. | PASS — explicit key is forwarded verbatim; persist-then-throw uses the real durable manager/store path. | PASS — public persist-then-throw integration test passes with durable row evidence. | PASS — no durable state added to Slice B. |
| Overtaking, complete shape, and generation identity | PASS — current overtaking, null-notification shape, and scope tests fail against the prior candidate before the barrier/shape correction. | PASS — pending retry blocks newer work, exact retry clears it, and replacement generation starts empty. | PASS — reaction/notification presence, keys, kinds, scope, epoch, and sequence are validated. | PASS — AntiTamper `76/76`; 20 fresh barrier/persist processes; combined Unit 4 `151/151`. | PASS — existing gate remains sole serialization mechanism. |
| Persist-then-throw durable convergence | **PROCESS DEBT** — current candidate already passed the deterministic safety test; no production-driving RED was fabricated. | PASS — actual `OutboxManager` persisted `notification-persisted`, controlled wrapper threw, exact retry deduped to one row, then newer work executed. | PASS — reaction once, exact key twice, durable row once, original fault through work/Stop, newer blocked then unblocked. | PASS — full Service twice, two isolated coverage hosts, combined Unit 4. | PASS — wrapper only injects post-persistence fault; production Outbox API unchanged. |

- Focused GREEN result: assigned Slice B tests pass `5/5`. Full verification gates are intentionally left to independent verification.
- Task state remains intentionally **9/14**; `4.1`–`4.3` and `5.1`–`5.2` remain unchecked. No verification report or commit was created.

## Unit 4C1B2a Amended-Design Closure — 2026-08-24

- Exact REDs retained: null and empty explicit keys initially reached persistence (`DbUpdateException` for null, no exception for empty); the old five-argument explicit call failed to compile after the trailing token was made mandatory. The queued-generation-cancellation test was executed against the pre-fix candidate with the corrected gate choreography; the candidate did not provide a stable observable effect failure before shutdown, and one diagnostic attempt hung. This is recorded as evidence limitation rather than an invented contemporaneous RED. The cancellation+fault test is safety/triangulation because the retained candidate already passed the isolated fault path.
- API GREEN: the explicit overload now requires both `string idempotencyKey` and non-optional trailing `CancellationToken`; null/empty reject with exact `ArgumentException` before `EnqueueAsync`; valid keys remain verbatim; the legacy overload and five-argument `default` source call remain compatible.
- Cancellation GREEN: caller tokens are passed to the actual `Generation.Gate.WaitAsync` through a linked owner/caller gate token; pre-acquisition cancellation completes the caller as canceled without effects; after acquisition, caller cancellation is detached and the chain uses generation ownership; gate release remains guarded by the single `acquired` flag.
- Current amended focused matrix passed `5/5`; queued-cancel + cancellation/fault stress passed `30/30` fresh processes. Combined Unit4 + Outbox focus passed `155/155`. Isolated late-failure test passed `20/20`.
- Full Service runs after the amended implementation: consecutive fresh passes `1,237/1,237` and `1,237/1,237`. A later coverage host failed once with the inherited `LateNonCancellableFailure_IsObservableAndHasNoEffects` flake (`1,236/1,237`); the sequential coverage rerun passed `1,237/1,237`. This failure is visible and not hidden. App.UI passed `192/192`.
- Coverage hosts: passing `C:\Users\Usuario\AppData\Local\Temp\opencode\unit4c1b2a-amended-final-coverage-a\2cee609e-b9cc-46cb-b9e0-99d65198e031\coverage.cobertura.xml` and `C:\Users\Usuario\AppData\Local\Temp\opencode\unit4c1b2a-amended-final-coverage-b-rerun\d7530093-2f8c-4cd3-b329-e5727b88dc9d\coverage.cobertura.xml`; both full Service `1,237/1,237`, with `AntiTamperMonitor` `99.17%` line / `85.18%` branch, `IntegrityVerdictHandler` `100%` / `97.36%`, and `OutboxManager` `100%` / `81.81%`. Null/empty/valid API and cancellation gate paths are covered.
- Historical key-forwarding proof remains process debt: current public runtime key assertion plus direct real Outbox persistence are retained, but no pre-production public AntiTamper+real-Outbox test or rejected-patch replay is claimed.
- Final audit remains `git diff --check` clean, `.codegraph` absent, tasks `9/14`, no commit/report. Final CODE+TEST is recorded below after the amended tests; status is **Ready for independent amended-design re-verification**, with the visible inherited full-coverage flake noted.

## Unit 4C1B2a Final Gate Closure — 2026-08-24

- Scope remained limited to the three confirmed Unit 4C1B2a blockers: atomic stale admission, verbatim notification-key forwarding with legacy compatibility, and accepted caller-cancellation ownership. No task checkbox, verification report, commit, dedupe/retry, durability, Unit 4C2, or Unit 5 change was made.
- Focused blocker matrix passed `4/4`; ten sequential stress iterations of stale admission, accepted cancellation, notification fault, and concurrent generation-gate tests passed with `0` failures. Combined Unit 4 matrix passed `141/141`.
- Full Service regression passed twice sequentially at `1,232/1,232` each. Full App.UI regression passed `192/192`. The existing duplicate xUnit ID discovery notice remains; no test failure occurred.
- Final full-Service coverage passed `1,232/1,232` and produced non-empty Cobertura at `C:\Users\Usuario\AppData\Local\Temp\opencode\unit4c1b2a-final-coverage\ae8af2f0-997b-4c39-8036-7f22662a5367\coverage.cobertura.xml`. Target classes report `IntegrityVerdictHandler` `100%` line / `97.36%` branch and `AntiTamperMonitor` `99.17%` line / `85.18%` branch. The artifact contains positive hits for admission, key forwarding, cancellation ownership/drain, ordering, faults, and serialization paths.
- The prior full-suite timeout is superseded by the two fresh full-suite passes and final coverage pass; no source change was made to address it beyond the scoped contract remediation already recorded above.
- `git diff --check` passed. Final amended CODE+TEST numstat is `366 additions + 15 deletions = 381 changed lines`, below the hard `400` limit with `19` lines remaining. The generated `.codegraph` directory was removed; OpenSpec artifacts remain intentionally preserved for cumulative evidence.
- Tasks remain intentionally `9/14`; no verify report or commit was created. Status: **Ready for independent Unit 4C1B2a verification**.

## Unit 4C1B2a Fresh Reconstruction — 2026-08-24

- Fresh slice started from approved `05ba25adcd0b894e351729e173ec31be77f72a61`; the rejected backup was used for lessons only. No handler, Unit4C2, Unit5, task checkbox, report, or commit was changed.
- Tests-first additions in `AntiTamperMonitorTests.cs` produced genuine RED for pure decision order: the clean base emitted one reaction and zero notification effects. Production `AntiTamperMonitor.cs` now consumes `VerdictDecision`, admits current generation/identity decisions before staging, runs reaction then optional notification under the existing `Generation.Gate`, forwards notification metadata verbatim, and retains a generation-owned effect fault receipt for Stop/drain observation.

### Strict-TDD Evidence — 4C1B2a Current Candidate

The earlier reconstruction table is superseded by the cumulative remediation table appended below. Historical rows remain chronology only and are not active status claims.

### Execution / budget

- Scoped restore was required because linked-worktree assets were absent; subsequent build/test commands used `--no-restore`.
- Affected Service test build passed with `0` errors; existing warning corpus remains.
- Focused fault/admission subset passed `4/4`: order, stale identity, reaction fault, and notification fault.
- `AdmittedNotificationChain_DrainsAfterStopAndIdentityRotation` hangs after its reaction barrier while Stop/drain is active; the hang-guard host was terminated. The concurrent gate test and broad matrix are not closed.
- CODE+TEST diff: **129 changed lines** (`43+ / 5-` production, `81+` tests), below the hard 400-line cap. `git diff --check` passed. Patch hash: `f643484e2f597db4aa4873427a951033bc6923ba`.
- Tasks remain **9/14**; `4.1`–`4.3` remain unchecked. No verification report or commit was created.

### Blocker

- **Blocked before independent 4C1B2a verification:** the fresh admitted notification chain does not complete its Stop/drain barrier. Diagnose and fix this gate/owner interaction without adding a second owner or weakening the required test before claiming readiness.

## Unit 4C1B2 Split Decision and Rejected-Candidate Record — 2026-08-24

- This section is cumulative apply-progress only. No implementation, testing, verification, task-checkbox, proposal, design, spec, report, config, commit, history, or remote work was performed for this update.
- All prior Unit 1–4C1B1 completion entries and evidence above are preserved exactly. The cumulative task state remains **9/14**; `4.1`–`4.3` and `5.1`–`5.2` remain unchecked.
- Rejected Unit4C1B2 candidate history is preserved: the first candidate was **174/400**, followed by the remediated candidate at **364/400**. Both candidates were rejected and neither is approved for commit.
- External rejected-candidate binary backup: SHA-256 `4533120e60595c38b709603af0465579b8d7b55bf8a3c378dcccb65b23dd71bc`. Rejected candidate stable patch-id: `fffb986f2b4d1ab71c9c69719f100bb653e288e7`.

### Preserved FAIL verdicts and critical reasons

- **First 174/400 FAIL** (`verify-report-unit-4c1b2-independent.md`): accepted notification-bearing work could become partial after Stop/Dispose/identity change; required stale-before-stage, owner fault/cancellation/dispose, duplicate/race, and notification-bearing lifecycle scenarios were untested; the reaction idempotency key was retained but not consumed; and the claimed admitted-late RED was false because `EffectOwner_DrainsAdmittedReactionAfterStop` passed on the exact B1 baseline. The exact notification/order RED was genuine. The candidate was not approved for commit.
- **Remediated 364/400 FAIL** (`verify-report-unit-4c1b2-authoritative-final.md`): notification-fault observation was nondeterministic (coverage host B failed `EffectOwner_NotificationFaultIsObservedAfterExactlyOneReaction`); generation-lifetime `ReactionKeys`/`NotificationKeys` were unbounded; failed effect reservations were committed before collaborator work and could not be retried; and strict-TDD proof remained incomplete because the admitted-stop test was inherited GREEN and exact rejected-patch proof was unavailable. The candidate was not approved for commit.
- TDD history is corrected explicitly: the original notification/order test had genuine RED. `EffectOwner_DrainsAdmittedReactionAfterStop` was inherited GREEN/safety evidence and was never RED. The approximate rejected-patch reconstruction is triangulation only, not exact historical RED proof.

### Approved split status

- **4C1B2a — Effect Chain Observation:** active and unstarted clean slice at `05ba25a` on `feat/sdd7-4c1b2-effect-chain-observation`; expected **280–330 CODE+TEST** lines; no dedupe state. Scope is paired admitted reaction→notification ownership, `Generation.Gate` serialization, deterministic fault publication independent of lifecycle cancellation, and Stop/Dispose/cancellation/fault barriers.
- **4C1B2b — Effect Dedupe/Retry:** pending child of approved 4C1B2a on `feat/sdd7-4c1b2-effect-dedupe-retry`; expected **100–160 CODE+TEST** lines. Scope is bounded separate reaction/notification progress, commit-on-success, retry-after-failure, concurrent duplicate/order behavior, and verbatim keys; no durable state.
- **4C2:** pending child of approved 4C1B2b. No 4C2 work is started or claimed here.

### Planned Strict-TDD Evidence — NOT STARTED

The following rows are planning placeholders only. They contain no RED, GREEN, triangulation, safety-net, or refactor evidence and must not be read as fabricated execution history.

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4C1B2a — Effect Chain Observation | **NOT STARTED** | **NOT STARTED** | **NOT STARTED** | **NOT STARTED** | **NOT STARTED** |
| 4C1B2b — Effect Dedupe/Retry | **NOT STARTED** | **NOT STARTED** | **NOT STARTED** | **NOT STARTED** | **NOT STARTED** |

### Split Status

- Tasks remain **9/14**; no task checkbox was changed.
- Unit4C1B2a is the current active/unstarted apply slice. Unit4C1B2b and 4C2 remain pending in the approved chain.
- Status: **Apply-progress updated only; no implementation/testing/verification work performed.**

## Unit 4C1B2 Final Remediation Evidence — 2026-08-24

- Scope stayed on Unit4C1B2 only. No handler, Unit4C2, Unit5, task checkbox, commit, or verification report was changed.
- Added minimum public-flow tests for reaction/store faults, notification/outbox faults, accepted owner cancellation during a blocked effect, concurrent public triggers, and admitted notification completion across Stop plus identity rotation.
- Initial RED chronology: the first fault/cancellation assertions exposed test setup assumptions (the explicit baseline decision admission was missing), so those failures are not claimed as production RED. The first complete full-coverage run then produced a genuine RED: `EffectOwner_NotificationFaultIsObservedAfterExactlyOneReaction` observed the work fault but `StopAsync` returned successfully instead of observing the same owner failure.
- GREEN production fix: `DrainGenerationAsync` now observes a fault retained on the generation lifecycle task after an owned task's completion continuation removes that task from `OwnedTasks`. This preserves the existing owner/Stop drain contract without a new task owner.
- The inherited `EffectOwner_DrainsAdmittedReactionAfterStop` baseline remains **GREEN/safety evidence**, not RED. Its test predates this remediation and is explicitly not used as new RED evidence.
- Focused new ownership tests: **8/8**. Combined Unit4 focus (`IntegrityCheckerTests|AntiTamperMonitorTests|IntegrityVerdictHandlerTests|IntegrityRuntimePathTests|EnforcementLevelMonitorTests`): **138/138**.
- Late-failure host: **1/1** twice. Full Service regression: **1,228/1,228** twice. Full Service coverage: **1,228/1,228** with non-empty Cobertura output; the temporary coverage directory was removed after inspection.
- Changed-source coverage inspection: `AntiTamperMonitor` **99.15% line / 87.75% branch**; `ProcessVerdictDecisionAsync` generated state machine **100% line / 92.85% branch**; `DrainGenerationAsync` generated state machine **100% line / 87.5% branch**. Reaction-only, notification-bearing, fault, Stop/cancellation, stale-admission, and concurrent-effect paths have runtime hits; remaining branch misses are unrelated defensive paths.
- Service product/test builds passed with `0` errors. App.UI build was attempted but is infrastructure-blocked by `XamlCompiler.exe` exit code `1`; App.UI `--no-build` could not run because `ControlParental.App.UI.Tests.dll` is absent. App.UI is not claimed as rerun evidence.
- Dedupe audit: reaction and notification keys are separate ordinal sets, keyed by the immutable supplied strings, created per generation and therefore lifecycle-bounded. The sets are updated only after `AdmitLocked` returns an owned admission and never globally; reaction-only decisions have no notification key and cannot suppress notifications in the separate domain.
- Final tracked CODE+TEST `git diff --numstat`: `98/9` production, `251/0` tests, and `5/1` authorized runtime-path migration = **364 changed lines**, under the hard 400-line limit. `git diff --check` passes.
- Patch hash (`git diff --binary | git hash-object --stdin`): `e59dc22b6bf87b07a4c2385f68cec530a9a44ab8`.
- Final status audit: only the three authorized source/test files are modified; OpenSpec remains untracked; `.codegraph` is absent; generated coverage output was removed; tasks remain **9/14**; no handler/4C2/Unit5 drift.
- Status: **Ready for authoritative Unit4C1B2 re-verification**. App.UI remains separately infrastructure-blocked and is not part of this readiness claim.

## Unit 4C1B2b Remediation Continuation — 2026-08-24

- Added genuine RED tests before the production correction for failed-older/newer overtaking, equal-position null-notification versus notification conflict, and same-generation scope mismatch. The three tests failed against the prior candidate because newer work executed, absent notification shape was accepted, and scope mismatch was executable.
- GREEN implementation keeps the existing `Generation.Gate`, adds one immutable `PendingRetry` token, and validates a complete decision shape before effects. Non-exact pending decisions throw retry-blocked with zero effects; exact retries execute only the failed domain and clear the barrier after success. Reaction-to-notification failure transitions the same barrier.
- Progress tokens now retain the complete shape, including notification presence/type/key, so equal-position conflicts are rejected before collaborators run. Scope mismatch within a generation is an invariant failure.
- Focused remediation matrix passed `5/5`; full `AntiTamperMonitorTests` passed `75/75`; Service test build passed with `0` errors and the inherited warning corpus. `git diff --check` passed.
- Current tracked CODE+TEST diff is `262` changed lines (`114 additions + 12 deletions` production; `136 additions` tests), leaving `138` lines under the hard 400-line budget. No task checkbox, report, commit, handler, 4C2, or Unit 5 artifact was changed.
- At the continuation checkpoint, persist-then-throw proof and broad gates were still outstanding; the final gate closure below supersedes that pending status.

## Unit 4C1B2b Final Remediation Gates — 2026-08-24

- Public persist-then-throw proof passed: exact durable key `notification-persisted`; first call persisted one row then threw `InvalidOperationException("persisted then throw")`; reaction ran once; exact retry called the same key, durable dedupe retained one row, notification completed, and the previously blocked newer decision then executed once. Stop retained and rethrew the original fault.
- Focused B/shape/barrier set: `76/76` AntiTamper tests. Combined Unit 4 focus (`IntegrityCheckerTests|AntiTamperMonitorTests|IntegrityVerdictHandlerTests|IntegrityRuntimePathTests|EnforcementLevelMonitorTests`): `151/151`.
- Barrier/persist stress: `20/20` fresh processes passed. Five requested builds passed with `0` errors: Domain, Service, Service.Tests, App.UI, App.UI.Tests. Full Service passed twice at `1,245/1,245`; known-good App.UI harness passed `192/192`.
- Isolated full Service coverage hosts both passed `1,245/1,245`: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-b-coverage-a\ba5c1ee0-d81d-4006-b927-8ce4c505d82f\coverage.cobertura.xml` and `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-b-coverage-b\246264bf-4bf6-4f44-a7ee-c07a5b32b013\coverage.cobertura.xml`. PendingRetry none/exact/mismatch, reaction→notification transition, clear, shape/scope conflicts, and durable retry paths are exercised by the focused tests; no threshold is configured.
- Final audit: `git diff --check` passed; binary diff hash `9c21c3974ce5cc6e9374104f6ce1264763512959`; tracked CODE+TEST numstat is `114+183` additions and `12+2` deletions = **311/400**; `.codegraph` absent; only the two authorized CODE+TEST files are modified, OpenSpec artifacts remain untracked; tasks remain `9/14`; no commit or verification report was created.
- Status: **Ready for independent Unit 4C1B2b verification**.

## Unit 4C1B2 Remediation Continuation — 2026-08-24

- Scope remains limited to Unit4C1B2 effect ownership. No verification report, task checkbox, Unit4C2, or Unit5 artifact was changed.
- Removed the post-await lifecycle-currentness guard from the admitted decision's paired notification. Once a decision is atomically admitted, enforcement and its notification remain one generation-owned effect chain; Stop drains the admitted work instead of dropping the notification.
- Added lifecycle-scoped ordinal key consumption for both immutable `ReactionIdempotencyKey` and `NotificationIdempotencyKey`, using ordinal comparison and the supplied key values verbatim. Admission now rejects older/equal `(Epoch, Sequence)` and previously consumed effect keys under the same generation/identity lease.
- Strict-TDD evidence correction: stale-before-stage coverage passes against the rejected candidate because the existing generation/identity admission guard already suppresses it. It is valid regression coverage, but it is not claimed as a genuine behavioral RED.
- Focused ownership tests passed `4/4`: notification key/order, admitted reaction drain after Stop, stale-before-stage no effects, and notification-bearing admitted completion after Stop.
- AntiTamper plus runtime focused matrix passed `68/68`; Service product build passed with `0` errors; `git diff --check` passed.
- Current tracked CODE+TEST diff is `232` changed lines (`103` production additions / 10 deletions; `133` test additions), below the 400-line limit. Existing analyzer warnings remain; no generated coverage artifact is retained.
- Remaining independent evidence: public reaction/outbox fault and cancellation assertions for the new decision path, concurrent duplicate/race proof, full regression/coverage gates, final drift audit, and independent Unit4C1B2 verification.

## Unit 4C1B2 — AntiTamper Effect Ownership Apply

- Scope: only Unit4C1B2 on `feat/sdd7-4c1b2-effect-safety`; 4C1B1 is consumed as the pure `VerdictDecision` predecessor. Unit4C2, Unit5, task checkboxes, reports, and commits remain untouched.
- Worktree baseline started clean for tracked product/test files at `05ba25adcd0b894e351729e173ec31be77f72a61`; OpenSpec artifacts and the CodeGraph index were pre-existing/generated context only.
- Implementation: AntiTamper now atomically leases a generation/identity/epoch/sequence effect token with the existing B2 admission, awaits enforcement before notification, revalidates the lease after collaborator awaits, consumes the immutable notification idempotency key verbatim through `EnqueueAsync`, and preserves the legacy reaction path for non-B1 handler seams.
- CODE+TEST budget: **174/400** (`95 additions + 9 deletions` production; `69 additions + 1 deletion` tests). No generated coverage output is included in this accounting.

### Six-Column Strict-TDD Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Decision-key effect ownership and ordering | PASS — `EffectOwner_UsesDecisionNotificationKeyAfterEnforcement` failed against the B1-only baseline because the notification effect was absent; observed `2` enforcement effects and `0` notifications instead of the required ordered notification. | PASS — focused test `1/1`; exact key `integrity/scope-1/integrity-binary/4/4/notification` and enforcement-before-notification pass. | PASS — one run covers non-notification trust admission, two enforcement-only revoked stages, and the notification-bearing third revoked stage; runtime path separately verifies `device-a` exact key and non-definitive preservation. | PASS — combined Unit4 matrix `132/132`; full Service `1,222/1,222` twice; late-failure inherited coverage remains green in the full suite. | PASS — token admission is folded into the existing B2 `AdmitLocked` path; no second mailbox/actor, timer, fire-and-forget owner, key reconstruction, or handler effect ownership added. |
| Admitted-late ownership / lifecycle boundary | PASS — behavior test was authored before the B2 effect-owner implementation and uses a barrier at real enforcement admission; the baseline lacked the decision-consuming owner path. | PASS — `EffectOwner_DrainsAdmittedReactionAfterStop` focused result `1/1`; Stop remains pending until the admitted enforcement collaborator releases. | PASS — existing B2 lifecycle matrix covers generation rotation, stale callbacks, cancellation, owned drain, and late completion; new barrier exercises the concrete B1 decision path. | PASS — full Service regressions `1,222/1,222` twice and combined Unit4 `132/132`. | PASS — accepted work remains owned by the existing generation/drain lifecycle; post-await lease validation suppresses later notification after stop/identity invalidation. |

### Execution Evidence

- Required restore was run once because the linked worktree had no test assets; subsequent build/test/coverage commands used `--no-restore` and isolated coverage output.
- RED: focused command `dotnet test ... --no-restore --configuration Debug --verbosity minimal --filter "FullyQualifiedName~EffectOwner_UsesDecisionNotificationKeyAfterEnforcement"` failed `1/1` before production changes with the expected missing notification.
- GREEN: same focused command passed `1/1`; focused ownership barrier passed `1/1` after the implementation.
- Combined Unit4 focus (`IntegrityCheckerTests|AntiTamperMonitorTests|IntegrityVerdictHandlerTests|IntegrityRuntimePathTests|EnforcementLevelMonitorTests`): `132/132`.
- Full Service regression passed sequentially twice: `1,222/1,222` each; existing duplicate xUnit ID discovery notice remains. App.UI apply attempt was infrastructure-blocked because its linked-worktree test DLL was absent; no source restore or App.UI edit was performed.
- Full Service coverage passed `1,222/1,222` with non-empty Cobertura output under the isolated `coverage-unit4c1b2-20260824` result directory; output was removed after inspection.
- `git diff --check` passed. No task checkbox or verification report was changed. The generated `.codegraph/` index could not be removed because its SQLite files remained locked by the CodeGraph MCP process; it is untracked and must be cleaned by the workspace owner before commit/PR.

### Status

- `tasks.md` remains intentionally **9/14**; `4.1`–`4.3` and `5.1`–`5.2` remain unchecked.
- Current Unit4C1B2 implementation is ready for independent verification, subject to the noted generated-index cleanup and the independent verifier’s full race/fault/cancellation review.

## Unit 4C1B2b Reconstruction Resume — 2026-08-25

- Interruption audit completed before any reapplication. HEAD is the exact A2 parent `c1dbba6f0fa6866cabaaca438919fe12b123e29a`; the actual rebuilt branch is `feat/sdd7-4c1b2b-effect-dedupe-retry`. The interrupted worktree contained only the two authorized modified CODE+TEST files plus the cumulative untracked OpenSpec tree; no staged, unknown, unrelated, handler, outbox-API, 4C2, Unit 5, or history edits were found.
- The partial edits were semantically valid and retained, not blindly reapplied. Three-way audit against A2 and preserved B patch `slice-b.patch` (SHA-256 `74b62677e6d21f1cf746dc69e3c4adaf5de1881e18b024841f40da37365bb065`) found only intentional A2-overlap deltas: A2’s `CompleteAdmissionAsync` generation/fault publication, removal-gap seam, cancellation exclusion, exact fault tests, and the corrected concurrent-start Stop expectation. B progress/shape/pending-retry behavior and tests match the preserved final B blobs; no B behavior was lost.
- Replayed the preserved B test-only patch on an exact detached A2 parent in a disposable worktree with B production logic absent. The truthful RED was a compile failure because `ExecuteDecisionAsync` is absent on A2 (`CS1061`, 8 B tests); no behavioral RED is fabricated. The preserved cumulative evidence retains the genuine duplicate `8/8` replay RED and the overtaking/shape REDs. The disposable worktree and its generated assets were removed.
- Current GREEN focused B set passed `8/8`; A2 late-fault/Stop-before-failure stress passed `100/100` fresh processes; B duplicate/retry/barrier/shape/persist stress passed `30/30` fresh processes. The first B stress shell timed out after reporting 26 passes at the 120-second tool limit; it produced no test failure, and runs 27–30 were then executed as fresh processes and passed. This is recorded as an execution-limit interruption, not masked retry evidence.

### Current Six-Column Strict-TDD Evidence — Rebuilt B

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Same-decision serialized dedupe | PASS — preserved genuine `8` reactions/`8` notifications replay RED; exact-parent test-only replay was compile-blocked because the B seam/API was intentionally absent | PASS — focused `1/1`; fresh B stress `30/30` | Separate O(1) reaction/notification progress and exact ordinal keys | Combined Unit 4 `153/153`; full Service `1,247/1,247` x3 | Existing `Generation.Gate` remains sole serializer; no history/collection owner |
| Reaction and notification commit/retry | PROCESS DEBT — no retained pre-production behavioral RED for either isolated fault row; no false RED claimed | PASS — focused `2/2`; exact retry commits only after success and preserves original fault | Reaction failure leaves both domains retryable; notification failure retains reaction and retries exact notification key | B stress `30/30`; full Service and coverage hosts green | One bounded progress token per domain; no durable state added |
| Overtaking, full shape, scope, and generation reset | PASS — preserved genuine overtaking/null-notification/scope REDs | PASS — focused `3/3`; newer work blocks until exact retry, then succeeds in a fresh generation | Full shape and scope are checked before effects; reset/null/distinct-newer semantics retained | Combined Unit 4 `153/153` | No handler, outbox API, 4C2, or Unit 5 edits |
| Persist-then-throw convergence | PROCESS DEBT — preserved candidate already passed the deterministic safety proof; no production-driving RED fabricated | PASS — focused `1/1`; durable row remains exactly once and exact retry unblocks newer work | Real SQLite Outbox path, exact key, one row, original fault through work/Stop | B stress `30/30`; two isolated coverage hosts `1,247/1,247` | Wrapper is test-only post-persistence fault injection |

### Rebuilt B Gate Evidence

- Five sequential `--no-restore` builds passed: Domain, Service, Service.Tests, App.UI, and App.UI.Tests; zero errors, existing warning corpus only.
- Full Service passed three sequential fresh runs: `1,247/1,247` each. App.UI passed `192/192`.
- Two physically isolated sequential full-Service coverage hosts passed `1,247/1,247`, with non-empty Cobertura artifacts at `sdd7-rebuilt-coverage-a/9d0d3821-f834-4684-b7e7-1a3758e32d86/coverage.cobertura.xml` and `sdd7-rebuilt-coverage-b/56084153-d0cb-48e5-a856-ebfdccdd9f27/coverage.cobertura.xml`; both hosts were removed after inspection. The existing duplicate xUnit ID discovery notice was recorded on each full Service run.
- Final audit: `git diff --check` passed; tracked CODE+TEST numstat is `114+183` additions and `12+2` deletions = **311/400**. Current binary diff hash is `41b699468b131077f98733c19d0d6d966a5a5a74`; the preserved B patch hash remains independently verified. `.codegraph` is absent; only the two authorized CODE+TEST paths are modified and OpenSpec remains untracked. No commit, report, rebase, or remote operation was performed.
- Tasks remain intentionally **9/14**; `4.1`–`4.3` and `5.1`–`5.2` remain unchecked. Status: **Ready for independent rebuilt-B verification**.

## Unit 4C1B2a Blocker Remediation — 2026-08-24

- Scope is restricted to the three confirmed admission/key/cancellation blockers and evidence repair. The preserved independent FAIL report remains unchanged. No dedupe/retry, durability, Unit 5, task checkbox, commit, or verification report was changed.
- The current-candidate RED run was performed before the production correction. Exact failures: `StaleAdmissionDoesNotStageHandlerStateBeforeNextAcceptedDecision` observed one unexpected `AddIssueAsync` for `Revoked verdict 2/3` after the stale transition; `AcceptedCallerCancellationCancelsObservationWhileOwnedChainDrains` expected caller cancellation but no exception was thrown. The key RED observation captured the legacy public-flow key as `integrity_integrity_degrade_pending_{timestampMillis}`, which differed from the decision key; the old API could not carry the immutable key, so compile-time explicit-key coverage was added only after the compatible overload GREEN change.

### Current Strict-TDD Evidence — 4C1B2a

| Cycle | RED | GREEN | REFACTOR | TRIANGULATE | SAFETY NET | STATUS |
|---|---|---|---|---|---|---|
| Queued cancellation at the actual `Generation.Gate` | PASS — current candidate queued caller cancellation could complete the caller task while the owned waiter later acquired the gate and executed effects. | PASS — caller token is linked into the actual gate wait; cancellation before acquisition cancels/removes the owned operation without reaction or notification; after acquisition the chain uses generation ownership. | PASS — one gate-release path guarded by `acquired`; no caller token is used after acquisition. | PASS — fresh-process stress `30/30`; combined Unit4 and full Service gates also pass. | PASS — existing FIFO, Stop/Dispose drain, stale admission, and ordering tests remain green. | PASS |
| Caller cancellation followed by collaborator fault | N/A — current behavior already surfaced the later fault in isolated execution; this is safety/triangulation, not a contemporaneous behavioral RED. | PASS — caller task is canceled; a subsequent reaction fault is stored in owned lifecycle state and Stop observes the exact exception once. | PASS — cancellation is not classified as `EffectFault`; no orphan or duplicate effect path added. | PASS — dedicated test passed; isolated late-failure test passed `20/20`; full-run failure is recorded below as a known parallel scheduling flake. | PASS — notification/reaction fault and drain regressions remain green. | PASS |
| Explicit API boundary and key validation | PASS — null and empty explicit keys did not throw before persistence; null reached the database and empty persisted. The old five-argument explicit call also failed to compile once the token became mandatory. | PASS — explicit overload requires `string idempotencyKey` plus trailing non-optional `CancellationToken`; null/empty throw exact `ArgumentException` before persistence; valid keys persist verbatim; legacy five-argument `default` binds unchanged. | PASS — no normalization, logging, dedupe, retry, or durable-scope change. | PASS — valid/null/empty/legacy tests passed; OutboxManager class is covered in both final Cobertura hosts. | PASS — existing payload, duplicate, claim, and lifecycle Outbox tests remain green. | PASS |
| Historical key-forwarding proof | N/A — the public real-Outbox proof was not written before the prior production candidate; no contemporaneous RED is claimed. | PASS — current public AntiTamper runtime seam asserts the exact decision key, and direct Outbox persistence proves verbatim storage. | PASS — retained mock seam avoids expanding the budget beyond the 400-line cap. | PASS — current candidate plus preserved rejected-patch/FAIL artifacts show the pre-fix reconstructed `integrity_{type}_{timestamp}` path versus the decision key. | PASS — runtime/full Service/coverage gates pass; no new historical replay claim is made. | PROCESS DEBT RECORDED |

- GREEN focused command after implementation: AntiTamper blocker tests, explicit/legacy OutboxManager tests, and runtime path passed `6/6`; the three blocker tests alone passed `3/3`.
- Existing `ExternalCancellation_IsReturnedToCallerAndOwnerCanDrain` was updated to the revised contract: caller observation is canceled immediately, while Stop remains pending until the owned collaborator releases. It passes alongside the new cancellation test.
- Amended RED command after adding the new tests: queued-cancel and cancellation+fault tests passed as a pair against the retained candidate; explicit null/empty tests failed exactly as required (`2` failures), while the valid/legacy tests passed. The source-compatibility correction then required updating the existing valid explicit call to provide `CancellationToken.None`; the legacy five-argument `default` call compiles and binds the old overload.
- Historical key-forwarding remains reconstructed process debt: no pre-production public AntiTamper + real Outbox test was available and no rejected patch was replayed. The preserved independent FAIL report and prior candidate evidence are retained as historical facts, not contemporaneous RED.

## Unit 4C1B1 Full-Service Coverage Closure — 2026-08-24

- Two sequential, physically isolated full-Service coverage hosts completed successfully with exact counters `1,223/1,223`, `0` failed, `0` errors, `0` timeouts, and `0` aborted:
  - `C:\Users\Usuario\AppData\Local\Temp\opencode\unit4c1b-coverage-isolated-a\results\full-service-coverage-a.trx`
  - `C:\Users\Usuario\AppData\Local\Temp\opencode\unit4c1b-coverage-isolated-b\results\full-service-coverage-b.trx`
- Both hosts produced non-empty Cobertura artifacts (`4,904,779` and `4,904,782` bytes) for `IntegrityVerdictHandler` and `AntiTamperMonitor`. Both report `IntegrityVerdictHandler` line-rate `100%`, branch-rate `93%`, and `AntiTamperMonitor` line-rate `99.15%`, branch-rate `87.75%`; sampled pre-accept cancellation, sequence allocation, FIFO/drainer, callback/outbox, and async AntiTamper seam lines have positive hits. No dump or Sequence artifact was generated.
- The earlier unfiltered coverage timeout is classified as **environment/infrastructure — shared-output/testhost/process interference**, not an in-scope production defect: the timed-out invocation left owned `dotnet` test processes that were terminated; the later isolated hosts completed with no test failure and no hang artifact. The remaining `dotnet` processes are the long-lived Roslyn `MSBuild`/`VBCSCompiler` daemons and have no Unit4C1B command-line ownership.
- Final audit remains exact CODE+TEST **400/400**, `git diff --check` passes, no staged files or `.codegraph`, and only the four previously in-scope source/test files are changed. No 4C1B2, 4C2, or Unit 5 work was added.
- Tasks remain intentionally unchanged at **9/14**; no verification report or commit was created. Unit 4C1B1 implementation evidence is now ready for independent verification.

## Unit 4C1B1 Async Mailbox Core Closure — 2026-08-24

## Unit 4C1B1 Pure Contract Completion — 2026-08-24

## Unit 4C1B1 Independent Invariant Remediation — 2026-08-24

- Remediated only the pure-decision invariant blockers: canonical identity scope, separate reaction/notification idempotency keys, local acceptance `ObservedAt`, and checked sequence/epoch overflow allocation before policy mutation.
- `VerdictDecision` now carries `IdentityScope`, `ReactionIdempotencyKey`, and nullable `NotificationIdempotencyKey`. AntiTamper supplies its existing canonical `DeviceId`/IssueKey scope through the pure handler path; compatibility callers retain explicit `local` scope.
- Added public-contract tests for cross-scope key separation, reaction/notification key separation and notification absence, backend/audit timestamp separation for backend and local failure, and overflow seed seam with subsequent valid epoch inspection. The overflow seed is an internal compile seam, not claimed as behavioral RED. New invariant tests were authored before production edits; no behavioral RED output was retained for the newly introduced identity fields because the prior contract lacked the required scope/key API.
- Final CODE+TEST diff is **397/400**: `IntegrityVerdictHandler.cs`, `AntiTamperMonitor.cs`, `IntegrityVerdictHandlerTests.cs`, and the previously authorized `IntegrityRuntimePathTests.cs`; no deferred 4C1B2/4C2/Unit5 drift.
- Focused invariant/handler/runtime/AntiTamper matrix passed `99/99`; combined Unit4 matrix passed `130/130`. Five sequential `--no-restore` builds passed with `0` errors. Full Service passed sequentially twice `1,220/1,220`; App.UI passed `192/192`; late-failure A/B passed `1/1` each.
- Sequential isolated coverage hosts passed `1,220/1,220` each:
  - `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c1b-invariant-final2\coverage-a\83cdc402-74ef-4d0e-82c5-10f5360cf936\coverage.cobertura.xml`
  - `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c1b-invariant-final2\coverage-b\772c7946-c4a2-44c4-8805-f9dbfe1e6a03\coverage.cobertura.xml`
  - Both are non-empty, report handler `100%` line / `97.36%` branch and AntiTamper `99.15%` line / `87.75%` branch; invariant and AntiTamper paths have positive hits; no dump/Sequence artifacts were produced.
- Final audit: `git diff --check` passes; HEAD/base remain `d73834c7de5c1f9d3f9852d50e02f350dec29a2f`; staged state empty; `.codegraph` absent; failed-candidate backup hash remains `D36FEE043D35E28B71BF3F473B97A922578D4456846AC6417DF43D8FB0C640D9`. Tasks remain **9/14**; no report or commit.

- Authorized test migration completed in `IntegrityRuntimePathTests.cs`: removed direct callback/outbox expectations, added public `VerdictDecision` metadata assertions for third-revoked notification, fourth/pending behavior, trust reset, fresh epoch/key, and preserved real AntiTamper reaction/enforcement and canonical issue-state assertions. The former failure is classified as specification/design alignment, not production RED.
- Strict-TDD evidence: behavioral RED `HandleVerdict_ThirdRevoked_IsPureAndDoesNotInvokeEffects` failed against the restored 4C1A baseline with callback count `3`; GREEN passes after the pure implementation. Compile-seam/contract tests cover contiguous sequence and epoch across definitive, non-definitive, and local-failure observations; exact pre-call cancellation token/no-gap; immediate sync/async progress; metadata-only notification semantics; and no handler-owned I/O.
- Focused Unit4 matrix passed `128/128`; handler `33/33`; AntiTamper `55/55`; runtime migration plus those suites passed `97/97`. Five sequential `--no-restore` builds passed with `0` errors (Domain, Service, Service.Tests, App.UI, App.UI.Tests).
- Full Service regression passed sequentially twice: `1,218/1,218` and `1,218/1,218`. Full App.UI regression passed `192/192`. Inherited late-failure test passed in isolated hosts A/B (`1/1` each).
- Sequential isolated full-Service coverage hosts passed `1,218/1,218` each with non-empty Cobertura artifacts:
  - `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c1b-final-evidence\coverage-a\f54ca2f8-67f7-4432-8db2-95f533af9cae\coverage.cobertura.xml`
  - `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c1b-final-evidence\coverage-b\028d2cd8-95ce-4078-ae15-9983f2c89915\coverage.cobertura.xml`
  - Both report `IntegrityVerdictHandler` `100%` line / `96.96%` branch and `AntiTamperMonitor` `99.15%` line / `87.75%` branch; pure decision, sequence/epoch, metadata, deadline, and AntiTamper consumption paths have hits. No dump/Sequence artifacts were produced.
- Final CODE+TEST diff against `d73834c7de5c1f9d3f9852d50e02f350dec29a2f` is **348/400**: handler `198`, handler tests `120`, runtime-path migration `30`; `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` remain unchanged. Current production handler contains no mailbox, drainer, Channel, background-task, callback invocation, notification flush, or outbox I/O. `git diff --check` passes; staged state is empty; `.codegraph` is absent; no 4C1B2/4C2/Unit5 files changed.
- Tasks remain intentionally **9/14**; no verify report or commit was created. Status: **Ready for independent Unit4C1B1 verification only**.

### Six-Column TDD Evidence — Pure Decision Contract

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Pure no-effect boundary | PASS — restored 4C1A third-revoked public test observed callback/outbox behavior (`callbackCalls=3`). | PASS — handler no-effect test passes. | PASS — constructor compatibility collaborators are not invoked; metadata is returned instead. | PASS — handler `33/33`, AntiTamper `55/55`, runtime `97/97`. | PASS — removed effect queue/flush/task ownership while retaining 4C1A policy behavior. |
| Decision identity and cancellation | PASS — compile seam was absent before new decision APIs. | PASS — sequence/epoch matrix and pre-cancel async tests pass. | PASS — equal timestamps, definitive/non-definitive/local failure, exact token, immediate completion, and unique deterministic keys are covered. | PASS — two full Service regressions and two coverage hosts. | PASS — compatibility `HandleVerdict`/`HandleLocalFailure` wrappers remain reaction-only. |
| Notification metadata and runtime migration | PASS — legacy runtime test failed because it expected callback/outbox effects. | PASS — migrated public runtime test and pure metadata assertions pass. | PASS — third-revoked metadata only once, fourth no command, trust reset fresh command/key, AntiTamper still owns reaction/enforcement consumption. | PASS — full Service `1,218/1,218` twice, App.UI `192/192`, late-failure A/B, coverage A/B. | PASS — no 4C1B2 stale/effect execution or 4C2 durability added. |

## Unit 4C1B1 Pure Decision Reset and TDD — 2026-08-24

- Failed mailbox candidate backup saved outside the worktree at `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c1b-failed-mailbox.patch` with SHA-256 `D36FEE043D35E28B71BF3F473B97A922578D4456846AC6417DF43D8FB0C640D9`.
- The four candidate files were restored exactly from `d73834c7de5c1f9d3f9852d50e02f350dec29a2f`; OpenSpec artifacts were preserved; `.codegraph` remains absent and staged state is empty.
- Strict-TDD behavioral RED was genuine: `HandleVerdict_ThirdRevoked_IsPureAndDoesNotInvokeEffects` observed callback count `3` before production changes. GREEN now passes the pure handler suite `33/33` and AntiTamper suite `55/55`; five no-restore builds pass with `0` errors.
- Revised implementation is currently `297` CODE+TEST changed lines against `d73834c`, limited to `IntegrityVerdictHandler.cs` and `IntegrityVerdictHandlerTests.cs`; AntiTamper files are unchanged because the existing reaction compatibility path compiles.
- Full Unit4 gate is blocked by an existing 4C1A runtime test that still expects the deprecated handler callback/outbox effects (`CanonicalAuthority_PreservesExactStateAcrossNonDefinitiveResultsAndCommitsAtDeadline`, `reactions` remains empty). The pure contract explicitly forbids those effects, so resolving this requires approval to update `IntegrityRuntimePathTests.cs` or to weaken the pure contract; no such change was made.

- Added only `PreAcceptedCancellationDoesNotAllocateOrMutateMailboxState` to `IntegrityVerdictHandlerTests.cs`; no 4C1B2 stale-token or concurrent-notification barriers were added.
- Test-first evidence: the first run was RED because `Assert.ThrowsAsync<OperationCanceledException>` required an exact base type while the public task surfaced `TaskCanceledException`; the correction was test-only. No genuine production RED occurred, so no production cancellation fix was made. Existing `Submit` pre-check remains the minimum cancellation-before-allocation implementation.
- The final test asserts accepted sequence `1`, canceled task state and exact token through `TaskCanceledException`, unchanged callback/outbox counts during rejected admission, next accepted sequence `2` with the unmutated policy action, and clean disposal through `using`.
- GREEN: exact pre-accept test `1/1`; handler class `38/38`; AntiTamper class `55/55`; focused combined Unit4/B1+B2 matrix `133/133`; late-failure host A/B `1/1` each; full Service final A/B `1,223/1,223` each; App.UI `192/192`.
- Five sequential `--no-restore` portable-PDB builds passed: Domain, Service, Service.Tests, App.UI, and App.UI.Tests. Two sequential focused coverage hosts passed `133/133` each with Cobertura artifacts under `TestResults/coverage-unit4c1b1-focused-a` and `TestResults/coverage-unit4c1b1-focused-b`.
- An unfiltered full-Service coverage attempt exceeded the 10-minute diagnostic timeout after the inherited late-failure path began; it was terminated without a code change. The retained coverage evidence is the two deterministic focused hosts above.
- An earlier broad focused filter exposed a nondeterministic existing reentrant assertion timing miss; the reentrant test passed isolated and the full handler class passed. An earlier full Service run also hit the inherited `LateNonCancellableFailure_IsObservableAndHasNoEffects` intermittent miss; isolated late-failure A/B and two final full Service runs passed. These are evidence limitations, not B1 fixes.
- Final audit: exact CODE+TEST diff is **400/400**; `git diff --check` passes; HEAD `d73834c7de5c1f9d3f9852d50e02f350dec29a2f`, base `6c6668bbdb5eb2bc328cfe86c28f878a51149679`; no staged files; `.codegraph` absent; changed code/test files remain limited to the 4C1B1/4C1A boundary. Tasks remain **9/14** and no verify report or commit was created.

## Unit 4C1B — Concurrent Ordering + Effect Ownership (Apply)

- Scope: only the 4C1B ordered mailbox seam; 4C2 persistence/deadline ownership and Unit 5 remain untouched. Tasks remain `9/14`; `4.1`–`4.3` remain unchecked.
- Contract implemented: accepted observations receive a checked monotonic ingress sequence under the mailbox gate; committed reactions carry sequence, epoch, and `VerdictEffectToken`; synchronous callers wait for their own terminal effect unless re-entering the owner, where nested work is deferred FIFO; async callers receive `Task<VerdictReaction>` with caller cancellation and observed callback/outbox faults.
- FIFO owner: one scheduled drainer owns the mailbox, uses RCSA completion sources, processes each item after narrow state commit, continues after item failure, and synchronously observes notifications outside `stateGate`. Dispose closes admission and waits for an active drain unless called reentrantly by that drain.

### Strict-TDD Cycle Evidence — 4C1B

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4C1B mailbox contract | PASS — new async/sequence tests first failed to compile against 4C1A because `HandleVerdictAsync`, `IngressSequence`, and effect metadata were absent. | PASS — focused mailbox matrix `3/3`; handler regression `35/35`; combined integrity focus after the latest fix `128/128` except no fresh rerun retained here. | PASS — controlled blocked-first FIFO, equal timestamp sequence identity, callback fault continuation, and reentrant nested submission. | PASS — existing handler baseline `31/31`; latest handler `35/35`; targeted inherited AntiTamper cancellation/reentrancy tests passed after preserving sync adapter behavior. | PASS — sync draining avoids thread-pool starvation; local-failure callback behavior remains compatible; no delay/yield/stress/reflection/private-helper test constructs. |

### 4C1B Execution Evidence

- Scoped Service restore was required because linked-worktree assets were absent; restore emitted only the existing NU1601/NU1701 warning corpus. Subsequent commands used `--no-restore`.
- Builds passed with `0` errors. Focused handler/mailbox tests passed `35/35`; the combined Unit4 focus reached `126/129` before the final compatibility correction, and the corrected targeted inherited tests plus handler/runtime tests passed. Full Service/App.UI, portable-PDB, coverage, and final independent verification gates remain pending.
- Current CODE+TEST diff from `d73834c7de5c1f9d3f9852d50e02f350dec29a2f` is below the hard limit: handler production `147 additions + 30 deletions`, handler tests `73 additions`; the AntiTamper file is byte-equivalent in content after indentation cleanup. No staged files and `.codegraph` remains absent.
- Status: **Not ready for independent Unit4C1B verification** until the requested stale-after-commit, cancellation/disposal, notification concurrency, full regression, coverage, and final drift gates are executed and retained.

### Follow-up 4C1B Seam Closure

- ✅ AntiTamper now uses `HandleVerdictAsync` through its existing admitted B2 stage, preserves the callback-generation context, forwards the operation cancellation token, and suppresses an effect-bearing reaction when `IsEffectCurrent` rejects its epoch token. Existing AntiTamper mocks were migrated to the async seam without changing production authority.
- ✅ Added RED/GREEN evidence for outbox fault ownership: notification enqueue faults now propagate through the mailbox completion while the FIFO drainer continues with later observations.
- ✅ Focused results after the seam correction: `AntiTamperMonitorTests` `55/55`; `IntegrityVerdictHandlerTests` `35/35`; isolated outbox-fault test `1/1`; Service product build `0` errors. Existing NU1601/NU1701 dependency warnings remain.
- ✅ Current CODE+TEST diff is `272 additions / 46 deletions = 318 changed lines`, below the hard 400-line cap; `git diff --check` passes. Four tracked source/test files are in scope; OpenSpec artifacts remain untracked by design.
- Remaining independent gates: stale-before-start/after-completion and cancellation/disposal matrix, concurrent notification proof, portable-PDB builds, isolated coverage hosts, final drift audit, and independent verification report.
- ✅ Full Service regression: `1,220/1,220` passed; full App.UI regression: `192/192` passed. App.UI required a scoped restore because its linked-worktree assets were absent; restore retained only the known NU1601 dependency warnings. No test failures or build errors.

### Latest Gap-Driven TDD Update

- ✅ RED/GREEN: `CancellationAfterAcceptanceDoesNotAbandonOwnedEffect` first failed with `TaskCanceledException`; removing the pre-commit cancellation check preserves accepted work ownership while retaining pre-accept cancellation in `Submit`.
- ✅ RED/GREEN: `DisposeClosesAdmissionAndDrainsAcceptedQueueWithoutPostDisposeEffects` first observed only effect `1`; accepted queued work was incorrectly rejected by the disposed state check. Disposal now closes admission, returns disposed directly for new submissions, and allows already accepted mailbox work to drain.
- ✅ Added and retained deterministic tests for concurrent acceptance/FIFO, effect-token invalidation, reentrant local failure, cancellation after acceptance, disposal drain, pre-accept cancellation, concurrent notification identity, then removed the redundant over-budget cases after triangulation. Final retained handler suite had `43/43` immediately before the latest full-run host timeout.
- ⚠️ Latest full Service default-parallel run after the final test reduction timed out at 180/300 seconds without a test failure summary; an earlier post-seam full run passed `1,228/1,228`. The latest handler-only rerun also timed out after prior `43/43` success. Treat full-gate evidence as **not closed** until reproduced in fresh isolated hosts.
- Current source/test diff: `329 additions / 49 deletions = 378 changed CODE+TEST lines`; `git diff --check` passes. Collection serialization was added only between handler and AntiTamper lifecycle tests to prevent barrier-host starvation under default parallel execution; this is not a production synchronization mechanism.

## Timeout Diagnosis — 2026-08-22

- Pre-run and post-run process audit found no stale `testhost.exe` or `vstest.console.exe`; only the long-lived Roslyn `VBCSCompiler.dll` process `dotnet.exe` PID `18152`, parent `2136`, created at `19:34:23`, with no Unit4C1B command-line/worktree ownership. Nothing was terminated.
- Bounded handler reproduction with `--blame-hang --blame-hang-timeout 30s`, TRX, and VSTest diagnostic log completed `37/37` in `230ms`; no Sequence XML or dump was generated because no hang occurred. Artifacts: `C:\Users\Usuario\AppData\Local\Temp\opencode\unit4c1b-diagnostic-20260822-handler-01\handler-01.trx`, `vstest-diag.log`.
- Three subsequent sequential handler runs completed `37/37`, `37/37`, `37/37` in `189ms`, `194ms`, and `187ms`; artifacts are under `unit4c1b-diagnostic-20260822-handler-1..3`.
- Predecessor/neighbor order `IntegrityRuntimePathTests → IntegrityVerdictHandlerTests → AntiTamperMonitorTests → IntegrityVerdictHandlerTests` completed `9/9`, `37/37`, `55/55`, `37/37`. Full Service excluding handler completed `1,185/1,185`. A fresh bounded full Service run completed `1,222/1,222` in `10s` with no Sequence XML/dump; artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\unit4c1b-diagnostic-20260822-full-01\full-01.trx` and `vstest-diag.log`.
- No managed dump, CDB/SOS stack, or hang-time task/object IDs exist because every bounded reproduction completed before hang collection. Therefore no exact wait graph or root cause can be proven from current evidence; the earlier timeout remains classified as unreproduced transient harness/process interference, not a code-confirmed mailbox deadlock.
- Static audit: mailbox owner is `DrainAsync` (`IntegrityVerdictHandler.cs:255-289`); `mailboxGate` protects queue/start state; `stateGate` is not held across callback/outbox calls; work-item completion uses `RunContinuationsAsynchronously` (`:141`); async start uses a tracked continuation (`:236-237`); per-item catches continue the queue (`:278-287`); drain ownership is restored in `finally` (`:289`); Dispose closes admission and waits for the captured drain (`:585-605`). No lost-wakeup or owner-leak path is proven by the completed diagnostics.
- Decision: **no production/test edits made during timeout diagnosis** and no missing scenarios are claimed complete. Continue only with the requested remaining isolated gates or stop if the 22-line budget cannot accommodate a proven fix.

## Three-Scenario Budget Decision — 2026-08-22

- Existing map confirms pre-accept cancellation is implemented in `Submit` (`:211-215`) but has no explicit exact-token/no-sequence test; it can be covered by extending the existing async mailbox test only with a small assertion block.
- Stale-before-start cannot occur inside the handler's single FIFO drainer (`:255-289`) because commit, token creation, notification flush, and callback are serialized. The real remaining boundary is AntiTamper's token check at `:603-607` before `ProcessVerdictReactionAsync`; existing `StopBeforeResolveAdmission` proves lifecycle staleness, not epoch check-call race. A genuine epoch-token production-flow test requires a mock reaction/token plus an admission barrier and exceeds a few lines.
- Concurrent notification order is not explicitly proven by the retained tests. `HandleVerdict_CircuitOpened_SendsNotification` proves identity/count only; `AsyncMailbox_AssignsAcceptanceOrderAndCompletesEffectsInFifo` proves callback/effect order only. A deterministic concurrent notification barrier requires a new outbox gate/assertion block well above the remaining budget when combined with the stale-owner proof.
- Current CODE+TEST remains `378/400`; no source/test edits were made for this decision. **Stop and recommend a further auto-chain split** rather than compressing unreadable tests, weakening evidence, or crossing 400. Tasks remain `9/14`; no verify report/commit.

### Current Six-Column TDD Evidence

| Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Acceptance/FIFO/effect ownership | Existing 4C1B async tests plus added gap tests exposed missing explicit acceptance and stale-token coverage; cancellation/disposal tests produced genuine behavioral REDs. | Focused mailbox, cancellation, disposal, handler, AntiTamper, runtime, enforcement, and build gates passed before the latest host timeout. | Equal timestamps, FIFO effects, callback/outbox fault continuation, epoch token invalidation, B2 generation/cancellation, and owned drain are covered; concurrent notification and stale-before-start still require independent retained evidence. | Full Service previously `1,228/1,228`; App.UI `192/192`; portable-PDB builds `5/5`. | Kept scope below 400 lines; removed redundant gap tests after triangulation and recorded unresolved contracts rather than fabricating evidence. |

## 4C1B Contract Map Before Further Edits

| Contract | Existing handler evidence | Existing AntiTamper evidence | Gap status / required smallest proof |
|---|---|---|---|
| 1. Acceptance sequence | `EqualTimestamps_AreOrderedByAcceptanceAndAcceptedDuplicatesCount`; `AsyncMailbox_AssignsAcceptanceOrderAndCompletesEffectsInFifo`; sequential policy/deadline tests (`HandleVerdict_ConsecutiveRevoked_StagedResponse`, `TrustAtDeadline...`, `NonDefinitivePreserves...`) | `ActiveGeneration_UsesCanonicalResolveStageExactlyOnce`; `QueuedCallerCancellation...` | **Gap:** no controlled concurrent B/C acceptance with unique sequence and exact reaction/effect order. Add barrier test. |
| 2. FIFO | `AsyncMailbox_AssignsAcceptanceOrderAndCompletesEffectsInFifo`; `AsyncMailbox_ReentrantSubmissionIsQueuedAfterCurrentEffect` | `AdmittedMutationTask_IsOwnedAndDrained`; `ConcurrentStop_SharesDrainUntilAdmittedWorkReleases` | **Partial:** effect FIFO is covered; add explicit non-reentrant completion-order observation. |
| 3. Stale before/after | `IsEffectCurrent` is only indirectly exercised by metadata-producing async tests; no delayed stale token test | `StopBeforeResolveAdmission_SuppressesResolveAndPreservesSnapshot`; `LatePolicyStage_AfterStopDoesNotStartDurableMutation`; `LateNonCancellableNormalCompletion_IsIgnoredAfterStop`; `LatePrivilegeCompletion_AfterStopHasNoTamperOrOutboxEffects` | **Gap:** direct stale-before-start and stale-after-completion token proof; add public-token tests without reflection. |
| 4. Reentrant sync compatibility | `ReentrantCallbackRunsAfterCommitAndFaultRemainsObservable`; `AsyncMailbox_ReentrantSubmissionIsQueuedAfterCurrentEffect` | `SynchronousHandler_ReentrantStopReturnsCompletedBeforeConsumption`; `ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock`; `TamperCallback_CanRequestStopWithoutSelfDeadlock` | **Gap:** nested `HandleLocalFailure` and explicit nested-fault owner. Add handler barrier test. |
| 5. Fault/cancellation identity | `AsyncMailbox_PropagatesCallbackFaultAndContinuesLaterItems`; `AsyncMailbox_PropagatesOutboxFaultAndContinuesLaterItems` | `ExternalCancellation_IsReturnedToCallerAndOwnerCanDrain`; `OwnerCancellation_ForwardsOperationTokenAndDrains`; `QueuedCallerCancellation_DoesNotReleaseUnacquiredGate` | **Gap:** handler cancellation identity before/after acceptance and no unobserved task. Add two handler tests. |
| 6. Concurrent notification | `HandleVerdict_CircuitOpened_SendsNotification`; `TrustBeforeDeadlineCancelsPendingEscalationAndFreshSequenceDegrades`; deadline/circuit tests | `ActiveGeneration_UsesCanonicalResolveStageExactlyOnce` | **Gap:** concurrent third-revoked with newer trust/revoked, FIFO notification identity and exactly-once. Add barrier/outbox test. |
| 7. Cancellation/disposal | `AsyncMailbox_*` tests cover normal completion only; `Dispose` fixture cleanup | `StartAndDispose_CancelsOwnedInitializationAndCleansOnce`; `ConcurrentStopAndDisposeShareLifecycleOwnership`; `SynchronousAdmission_IsRemovedBeforeDrain` | **Gap:** accepted cancellation must still drain effect; close admission/no post-dispose handler effect. Add handler tests. |
| 8. Overflow | No injectable sequence setup; `EqualTimestamps...` proves normal increment only | No sequence ownership test | **Justification:** sequence is private and no reflection/injection is permitted; source has checked `long.MaxValue` guard. Do not add a production-only test seam; retain source proof. |
| 9. AntiTamper async seam | `HandleVerdictAsync`/token metadata generated by handler tests | `LatePolicyStage...`, `StopBeforeResolve...`, `SynchronousHandler_ReentrantStop...`, cancellation and generation tests | **Covered after async migration:** B2 admission, generation AsyncLocal, cancellation forwarding, stale token suppression; rerun full AntiTamper suite after gap additions. |

### Existing Handler Test Inventory (35 discovered cases)

Policy/grace: `WithinGracePeriod`, `AfterGracePeriod`, `ConsecutiveRevoked_StagedResponse`, `ThreeConsecutiveRevoked`, `AfterEscalationWindow`, `SuccessFalse`, `FiveConsecutiveFailures`, `CircuitOpen`, `CircuitRecloses`, `TrustAfterRevoked`, `ShadowMode`, `AfterDisableShadowMode`, `StagedResponse`, `LocalFailure_ReturnsDegrade`, `LocalFailure_WithinGracePeriod`, `Trust_ResetsRevokedCounter`, `Unknown`, `CircuitOpened_SendsNotification`, `IsShadowMode_DefaultsToTrue`, `DisableShadowMode`, `NoOutboxManager`, `LocalFailure_ShadowMode`, `RecoveryThreshold`, `EqualTimestamps`, `NonDefinitivePreservesPending`, `ReentrantCallback`, `TrustAtDeadline`, `TrustBeforeDeadline`, `AuthoritativeRecovery`, `CircuitOpenAtDeadline`; 4C1B seam: `AsyncMailbox_AssignsAcceptanceOrderAndCompletesEffectsInFifo`, `AsyncMailbox_PropagatesCallbackFaultAndContinuesLaterItems`, `AsyncMailbox_PropagatesOutboxFaultAndContinuesLaterItems`, `AsyncMailbox_ReentrantSubmissionIsQueuedAfterCurrentEffect`.

### AntiTamper Inventory

Constructor/initial state/event/outbox tests cover validation and non-runtime behavior; lifecycle groups cover `Start/Stop`, concurrent start/stop/dispose, generation restart, cancellation, late completion/fault, stale callbacks, and owned-task draining; 4C1B-relevant groups are `LateNonCancellable*`, `ExternalCancellation`, `OwnerCancellation`, `QueuedCallerCancellation`, `LatePolicyStage`, `ActiveGeneration_UsesCanonicalResolveStageExactlyOnce`, `StopBeforeResolveAdmission`, `AdmittedMutationTask_IsOwnedAndDrained`, `SynchronousHandler_ReentrantStopReturnsCompletedBeforeConsumption`, `ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock`, `TamperCallback_CanRequestStopWithoutSelfDeadlock`, and `SynchronousAdmission_IsRemovedBeforeDrain`.

## Unit 4C1 — Ordering Synchronization Apply Evidence

- Scope: Unit 4C1 only on feature-branch-chain child `feat/sdd7-4c-verdict-ordering-escalation`, exact B2 parent `6c6668bbdb5eb2bc328cfe86c28f878a51149679`. Unit 4C2, Unit 5, B2 helpers, composition, retry, persistence, and production timer ownership were not changed.
- Failed broad-candidate audit completed before refactoring: exact three-file source/test diff was saved outside the repository at `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c-failed-candidate.patch`; SHA-256 `1a18c38033a7bf3b29d814d0fcefdd46fb61a14261aef0c3ea2e5ec55f94fe1a`. The three candidate files were restored exactly to B2; reset audit reported zero tracked code/test diff before RED.
- Normative contract implemented: synchronized acceptance is the ingress linearization point; equal timestamps and accepted duplicates count independently; definitive trust/revoked alone mutate sequence state; non-definitive observations preserve it; revoked transitions are WARN, LIMIT, pending LIMIT with one notification and a five-minute local-acceptance deadline; fourth-plus observations do not extend or duplicate; trust cancels pre-degrade pending state; pure `EvaluateDeadline` degrades once; three consecutive post-degrade trusts emit one recovery; revoked resets recovery.
- Effects are staged outside the state lock. Notification faults are synchronously observed; callbacks are invoked after committed state and can re-enter immediately. Ownerless timer activation was removed; `EvaluateDeadline` is the explicit 4C1 seam and scheduling remains 4C2.

### TDD Cycle Evidence — Unit 4C1

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4C1 ordering/count/deadline | PASS — new tests referenced the absent callback/clock/deadline contract before production edits; compile RED was genuine. | PASS — focused handler suite `27/27`. | PASS — equal timestamps, duplicate observations, staged counts, non-definitive preservation, exact deadline, one-shot transition, and recovery are exercised. | PASS — B2 handler baseline `32/32` before edits; exact reset audit completed. | PASS — state/core split, explicit deadline seam, queued notifications, no timer owner, and callback-after-commit boundary. |
| 4C1 callback/effect boundary | PASS — reentrant callback and fault tests were added first and failed to compile against B2. | PASS — focused handler suite `27/27`, including reentrancy and fault identity. | PASS — reentrant progress, callback fault propagation, notification queue flushing, and repeated deadline suppression are covered. | PASS — no unrelated tracked paths changed. | PASS — no fire-and-forget notification path remains in the handler. |

### Unit 4C1 Execution Evidence

- Fresh affected test build: `dotnet build tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj --no-restore --configuration Debug`; exit `0` (`sdd7-unit4c-build.log`, existing warning corpus only).
- Focused handler GREEN: `dotnet test tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --filter "FullyQualifiedName~IntegrityVerdictHandlerTests"`; `27/27` passed.
- Current exact tracked Unit 4C1 CODE+TEST diff from B2: production `95 additions + 63 deletions`, tests `66 additions + 7 deletions`, total **231/400**. `git diff --check` passed. `IntegrityRuntimePathTests.cs` remains exact B2 content.
- Tasks remain intentionally unchanged at **9/14**; `4.1`, `4.2`, and `4.3` remain unchecked. No verify report was written during apply.

### Remaining 4C1 Gates / Status

- Full requested Unit4C1/AntiTamper/B1/B2/Service/App.UI/coverage/two-host inherited safety gates are pending independent verification; the inherited B2 `LateNonCancellableFailure_IsObservableAndHasNoEffects` two-host gate was not rerun during this apply.
- Status: implementation GREEN for the focused handler slice; **not ready to claim independent Unit4C1 verification** until the full requested gate matrix closes.
- Verification blocker observed while probing the inherited runtime path: the unchanged B2 `IntegrityRuntimePathTests` expects the old fourth-revoked immediate-degrade behavior, while 4C1 intentionally exposes deadline evaluation as a pure seam and defers production deadline invocation to 4C2. The runtime test was restored to exact B2 content as required; no compatibility workaround was added to 4C1.

## Unit 4C1 — Opportunistic Deadline Attempt

- Retained the exact runtime RED command and assertion. Fresh isolated runtime execution still fails only at `IntegrityRuntimePathTests.cs:79`: the fourth accepted revoked observation leaves the canonical issue `Warning` with `LastEvidence = Escalation pending`; no test weakening or deletion was performed.
- Added RED-first handler cases for revoked, trust, and non-definitive observations accepted exactly at the deadline. Production now evaluates the local acceptance clock before applying the observation, commits degradation once, gives degradation precedence, preserves non-definitive counters, and lets trust recovery continue at 1/3 → 2/3 → 3/3.
- Focused handler GREEN is now `30/30`; AntiTamper `55/55`; IntegrityChecker `7/7`; EnforcementLevelMonitor `24/24`. The deadline helper captures acceptance time outside the state lock; no timer, store, Program, persistence, or 4C2 owner was added.
- Fresh full Service hosts A and B both fail the unchanged runtime test only: `1,214 passed, 1 failed, 0 skipped, 1,215 total`. Raw outputs: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c-full-service-a.log` and `...sdd7-unit4c-full-service-b.log`.
- Inherited B2 gate passed in two isolated hosts with retained raw output: `LateNonCancellableFailure_IsObservableAndHasNoEffects` `1/1` each. Raw outputs: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c-b2-late-host-a.log` and `...sdd7-unit4c-b2-late-host-b.log`.
- Full App.UI passed `192/192`; raw output: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c-full-appui.log`.
- Current exact CODE+TEST diff from B2 is `290/400` (`110 additions + 64 deletions` production; `109 additions + 7 deletions` tests). `IntegrityRuntimePathTests.cs` remains exact B2. `git diff --check` passes.
- Root cause/blocker: the inherited runtime test performs four rapid observations with no local clock advancement, so its fourth acceptance is strictly before the five-minute deadline. Making it degrade would violate the explicit `< deadline` 4C1 contract; changing AntiTamper to evaluate a production deadline owner would be 4C2. No B2 compatibility hack was added.

## Unit 4C1 — Runtime-Path Alignment Follow-up

- Corrected `IntegrityRuntimePathTests` to inject a deterministic local clock through the real `AntiTamperMonitor` path and assert WARN → LIMIT → pending LIMIT, rapid-fourth non-degradation, one-tick-before-deadline behavior, exact deadline degradation, and one-shot notification behavior.
- The inherited recovery-after-refresh assertions were removed from this ordering-focused runtime test because the current 4C1 slice does not own durable/recovery rehydration; handler-level recovery remains covered by `IntegrityVerdictHandlerTests`. No production compatibility behavior was added.
- Fresh affected test build passed with `0 errors` (existing analyzer warning corpus). Focused runtime-path test passed `1/1`; complete `IntegrityRuntimePathTests` passed `9/9`. `git diff --check` passed.
- Current tracked CODE+TEST diff from B2 is `357/400` (`117 additions + 66 deletions` production; `140 additions + 34 deletions` tests). Tasks remain intentionally unchanged at `9/14`; `4.1`, `4.2`, and `4.3` remain unchecked. No verify report was written.
- Remaining gates: combined Unit 4 focus, full Service/App.UI regressions, two-host coverage/safety gates, final drift/budget audit, and independent verification. This apply slice is not ready to claim final Unit 4 verification.

## Unit 4C1 — Final Apply Evidence Closure

### Six-Column Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4C1 ordering/count/deadline | PASS — existing RED-first handler scenarios remained the acceptance basis; no production/test edits were made during this evidence-only run. | PASS — `IntegrityVerdictHandlerTests` `30/30` (handler log completion `2026-08-22T14:33:17.2949226-05:00`); corrected `IntegrityRuntimePathTests` `9/9`. | PASS — exact handler suite covers equal timestamps, ordered revoked counts, concurrent ingress, definitive/non-definitive preservation, stale epoch, reentrancy/fault, deadline boundary, and recovery; runtime path covers WARN/LIMIT/pending/exact deadline/one-shot notification. | PASS — no fresh gate exposed a genuine defect; only the initially attempted `--debug:portable` CLI spelling was invalid and was rerun correctly with `-p:DebugType=portable -p:DebugSymbols=true`. | PASS — no source/test edits in this evidence pass; 4C1 remains timerless and does not claim 4C2 persistence/restart ownership. |
| 4C1 runtime authority path | PASS — corrected runtime test was already RED against the inherited immediate-degrade expectation before alignment. | PASS — full runtime-path filter `IntegrityRuntimePathTests` `9/9`. | PASS — real `AntiTamperMonitor` + backend client + verdict handler + enforcement monitor path exercised with injected local clock. | PASS — full Service twice: `1,215/1,215` each; App.UI `192/192`; late B2 host A/B `1/1` each. | PASS — no sleeps/yields/stress/reflection/private-helper additions in the diff; pre-existing infinite-delay cancellation seams are unchanged. |
| 4C1/B1/B2 regression matrix | N/A — evidence-only regression. | PASS — explicit B1+B2 lifecycle matrix `22/22`; AntiTamper `55/55`; IntegrityChecker `7/7`; EnforcementLevelMonitor `24/24`. | PASS — combined Unit4A+B1+B2+4C1 filter `126/126`: `IntegrityCheckerTests|AntiTamperMonitorTests|IntegrityVerdictHandlerTests|IntegrityRuntimePathTests|EnforcementLevelMonitorTests` plus `BackendAuthority` coverage. | PASS — two fresh isolated `LateNonCancellableFailure_IsObservableAndHasNoEffects` hosts passed. | PASS — no B1/B2 helper or lifecycle owner drift. |
| Portable-PDB builds and coverage | N/A — evidence-only build/coverage. | PASS — sequential `--no-restore` portable-PDB builds for Domain, Service, Service-tests, App.UI, and App.UI-tests; all `0 errors`, completion timestamps `14:40:40.671` through `14:41:11.674 -05:00`. | PASS — two fresh isolated full-Service coverage hosts each ran `1,215/1,215`; Cobertura is non-empty (`14,782/27,090` lines covered/valid). | PASS — fresh artifacts: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1-final-a2/d239907b-5d0b-4ee6-b3a4-f78fd336453f/coverage.cobertura.xml`; `.../coverage-unit4c1-final-b2/208f828b-a7c3-4ba9-b740-6f81597c1e3d/coverage.cobertura.xml`. | PASS — source hits include ingress/deadline/recovery paths; no aggregate-only claim. |

### Coverage Metrics (both fresh hosts identical)

| File / method | Line | Branch | Source-hit interpretation |
|---|---:|---:|---|
| `IntegrityVerdictHandler.cs` / class | 98.06% | 96.25% | Ingress, count, non-definitive, deadline, recovery, stale, callback, and fault paths executed. |
| `HandleVerdict` | 100% | 100% | Synchronized ingress and post-lock effects. |
| `HandleVerdictCore` | 96.72% | 93.33% | Definitive/non-definitive ordering, exact deadline precedence, and recovery transitions. |
| `HandleRevokedVerdict` | 100% | 100% | First/second/third/fourth-plus revoked sequence. |
| `EvaluateDeadline` | 100% | 100% | Explicit pre-deadline and due/after-deadline seam. |
| `CommitDeadlineIfDueLocked` | 100% | 100% | One-shot deadline commit and stale/duplicate suppression. |
| `FlushNotifications` | 100% | 100% | Notification work is staged and observed outside the state lock. |
| `AntiTamperMonitor.cs` / class | 99.15% | 87.75% | Real runtime ingress and effect application path. |
| `PerformBinaryIntegrityCheckAsync` | 92.45% | 77.77% | Backend verdict, local evidence, identity generation, and reaction routing. |
| `ProcessVerdictReactionAsync` | 88.88% | 81.25% | Limit/degrade/recovery effect routing and outbox observation. |

### Final Audit / Status

- Branch `feat/sdd7-4c-verdict-ordering-escalation`; `HEAD`, requested B2 base, and merge-base all equal `6c6668bbdb5eb2bc328cfe86c28f878a51149679`.
- Tracked code/test changes remain exactly three intended files: `IntegrityVerdictHandler.cs`, `IntegrityRuntimePathTests.cs`, and `IntegrityVerdictHandlerTests.cs`. No staged files. Untracked content is the existing `.codegraph/` index and cumulative OpenSpec artifacts only.
- `git diff --check` passed. Exact tracked CODE+TEST diff is `357/400`: `117 additions + 66 deletions` production and `140 additions + 34 deletions` tests.
- No added `Task.Delay`, `Task.Yield`, sleep, stress, reflection, private-helper, ownerless timer/task/resource, or unobserved collaborator/effect path; existing cancellation-delay seams are unchanged. All handler collaborators/effects are staged/observed outside the state lock.
- No 4C2 durable store/timer/restart changes; no Unit5, Program, composition, retry-owner, or B2 helper drift. Failed candidate remains at `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c-failed-candidate.patch`, SHA-256 `1A18C38033A7BF3B29D814D0FCEFDD46FB61A14261AEF0C3EA2E5EC55F94FE1A`.
- `tasks.md` remains exactly `9/14`; tasks `4.1`, `4.2`, and `4.3` remain unchecked. No verify report or commit was created/modified.
- Status: **Ready for independent Unit4C1 verification**. This is apply evidence only; it does not approve or complete the OpenSpec tasks.

## Unit 4C1A — One-Shot Recovery Remediation

### Six-Column TDD Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4C1A recovery state machine | PASS — added `AuthoritativeRecovery_IsOneShotUntilLaterDegradationStartsFreshTrustSequence` through the public handler flow. It reached exact-deadline degradation, asserted trust 1/2 false, trust 3 true, trust 4/5 false, then asserted a later revoked sequence enables exactly one future recovery. RED failed because repeated trust emitted recovery again. | PASS — minimum fix gates recovery on `degraded`, then atomically clears `degraded`, trust count, pending latch, due time, and fired latch before returning the authoritative recovery reaction. One-shot test `1/1`. | PASS — pre-degrade trust cancellation, revoked trust reset, exact deadline precedence, and existing callback behavior remain covered by the prior 31 handler tests and 9 runtime-path tests. | PASS — no new failure in AntiTamper, B1/B2 lifecycle, combined Unit4, full Service, App.UI, late-failure hosts, builds, or coverage. | PASS — no sequence/concurrency/mailer/async API was added; only neutral/recovered state transition was changed. |

### Unit 4C1A Execution Evidence

- RED build/test: `sdd7-unit4c1a-red-build.log`, `sdd7-unit4c1a-red-test.log`; the new test failed at the repeated-trust assertion because the existing degraded latch remained active. No unexplained partial edit was present before the fix.
- GREEN one-shot command: `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AuthoritativeRecovery_IsOneShotUntilLaterDegradationStartsFreshTrustSequence"`; `1/1`.
- Full handler: `31/31`; runtime path: `9/9`; AntiTamper: `55/55`; IntegrityChecker: `7/7`; EnforcementLevelMonitor: `24/24`; explicit B1+B2 lifecycle matrix: `22/22`.
- Combined Unit4A+B1+B2+4C1A filter (`BackendAuthority|AntiTamperMonitorTests|IntegrityCheckerTests|IntegrityVerdictHandlerTests|IntegrityRuntimePathTests|EnforcementLevelMonitorTests`): `127/127`.
- Fresh late-failure hosts: `sdd7-unit4c1a-late-host-a.log` `1/1`; `sdd7-unit4c1a-late-host-b.log` `1/1`.
- Sequential portable-PDB builds (`-p:DebugType=portable -p:DebugSymbols=true`) for Domain, Service, Service-tests, App.UI, and App.UI-tests: all `0 errors`.
- Full Service default parallelism: `1,216/1,216` twice. Full App.UI: `192/192`.

### Unit 4C1A Coverage

- Fresh host A: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-final-a2/e360a43c-d91e-454a-a5d7-c62585b09681/coverage.cobertura.xml`.
- Fresh host B: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1a-final-b2/1ed9bd56-3863-4ea1-8f49-b2fd0ae6bd68/coverage.cobertura.xml`.
- Both hosts: `14,787/27,095` lines covered/valid; both full-Service runs `1,216/1,216`.
- `IntegrityVerdictHandler` class: `98.11%` line / `96.25%` branch.
- `HandleVerdict`: `100%` / `100%`; `HandleVerdictCore`: `96.96%` / `93.33%`; `HandleRevokedVerdict`: `100%` / `100%`; `EvaluateDeadline`: `100%` / `100%`; `CommitDeadlineIfDueLocked`: `100%` / `100%`; `FlushNotifications`: `100%` / `100%`.
- `AntiTamperMonitor` class: `99.15%` line / `87.75%` branch. Coverage includes the public runtime ingress, exact deadline, one-shot degradation, and recovery effect path.

### Cleanup / Final Audit

- Audited this worktree’s untracked `.codegraph/`: it contained only `codegraph.db`, `codegraph.db-shm`, and `codegraph.db-wal`, generated index artifacts. Removed the directory entirely. No repository-root or other-worktree `.codegraph` was touched; final `Test-Path .codegraph` is `False`.
- No added `Delay`, `Yield`, sleep, stress, reflection, or private-helper test constructs; diff audit returned no such additions. No 4C1B sequence/concurrent stale-effect/FIFO mailbox work and no 4C2 durability work were added.
- Branch/HEAD/base/merge-base remain `feat/sdd7-4c-verdict-ordering-escalation` / `6c6668bbdb5eb2bc328cfe86c28f878a51149679` / `6c6668bbdb5eb2bc328cfe86c28f878a51149679` / `6c6668bbdb5eb2bc328cfe86c28f878a51149679`.
- `git diff --check` passed; no staged files; only the three intended tracked files are modified plus cumulative OpenSpec artifacts. CODE+TEST is `393/400` (`123 additions + 66 deletions` production; `170 additions + 34 deletions` tests).
- `tasks.md` remains exactly `9/14`; 4.1–4.3 remain unchecked. Failed reports remain preserved; no verify report or commit was created.
- Status: **Ready for independent Unit4C1A verification**, with explicit exclusions: 4C1B sequence/concurrent stale-effect/FIFO mailbox and 4C2 durability remain unimplemented and unclaimed.

## Unit 4B2 Cancellation-Token Forwarding Remediation

- Scope is limited to the latest authoritative cancellation-token blocker. Unit4C/Unit5/handler files, all three failed Unit4B2 reports, and `tasks.md` remain unchanged; task state remains `9/14`.
- Genuine RED was recorded before the production edit: the strengthened external-cancellation test failed because the awaited cancellation token was non-cancellable, and the owner-cancellation case initially failed because the test exercised non-owner-canceling admission. The owner case was corrected to use `TriggerIntegrityCheckAsync(true)` and a real backend cancellation path before GREEN.
- Strengthened `ExternalCancellation_IsReturnedToCallerAndOwnerCanDrain` through real monitor flow to assert canceled status, cancellable exception token identity equal to the external caller token, pending Stop at cancellation, successful drain, generation removal, and exact resource disposal.
- Added `OwnerCancellation_ForwardsOperationTokenAndDrains` to assert owner-canceling admission preserves a cancellable operation/owner token and Stop drains successfully without false faulting.
- Replaced parameterless completion forwarding with `CompleteAdmissionAsync`: it awaits the execution task, forwards the caught OCE token when cancellable, falls back to the generation owner token otherwise, and completes public completion first and owned completion second. Success and non-OCE exception identity forwarding remain unchanged; no lifecycle lock crosses user work and no unobserved helper exception is left.

### TDD Cycle Evidence — Cancellation Token

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| B2 cancellation-token forwarding | PASS — real external monitor flow failed on `CancellationToken.CanBeCanceled == false`; owner case was then made genuinely owner-canceling before production GREEN. | PASS — exact external/owner token tests `2/2`; public tasks are `Canceled` and preserve caller/operation token identity. | PASS — caller token wins when caller cancellation is requested; owner/operation token is preserved for owner cancellation; parameterless OCE falls back to the cancellable owner token. | PASS — AntiTamper `54/54`; combined Unit4A+B1+B2 integrity focus `87/87`; full Service `1,208/1,208` three consecutive runs; App.UI `192/192`. | PASS — tiny admission-completion helper; public-first/owned-second ordering, RCSA, success, fault, and drain behavior retained. |

### Cancellation Remediation Execution Evidence

- Sequential no-restore Debug builds passed with `0 errors`: Domain (`0` warnings), Service (`1`), Service-tests (`5`), App.UI (`347`), and App.UI-tests (`977`); existing warnings only.
- Two fresh coverage hosts passed full Service `1,208/1,208`:
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-token-final-a-20260821/3091bf33-2bca-419b-8ea1-8c638f60ab6a/coverage.cobertura.xml`
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-token-final-b-20260821/d36f0e48-645c-406d-8927-d99041d6c954/coverage.cobertura.xml`
  `CompleteAdmissionAsync` reports `100%` line/branch coverage; the cancellation-token selection branch is `100% (2/2)`.
- Final audit passed `git diff --check`; only the intended production/test files are tracked content changes; no staged files; CODE+TEST is `351/400` (`130+50` production and `165+6` tests). The three Unit4B2 report hashes remain unchanged: authoritative `A54BB617D3CC3D61C054158118926E8BA218D34538CDFD299ACE4F4351CBF3C4`, independent `681747C3F409F570E555E1232A13E016760D38E9F672B264EB9D1B187268013D`, and re-verification `968BB88352AA71604377869966FABA6C961921FB9F3CAFA8072B9A049103E519`.
- **Evidence gate result:** exact token semantics and requested safety gates are complete; ready for independent Unit4B2 verification. Historical strict-TDD warnings remain preserved; no verification report or task checkbox was changed.

## Unit 4B2 Queued Pre-Acquisition Cancellation Remediation

- Scope is limited to the queued pre-acquisition gate-release blocker. Unit4C/Unit5/handler files, all failed reports, and `tasks.md` remain unchanged; task state remains `9/14`.
- Genuine RED was captured through `QueuedCallerCancellation_DoesNotReleaseUnacquiredGate`: operation A blocked in the real backend, operation B was canceled through its external CTS while queued, and the existing unconditional `finally { gate.Release(); }` caused `SemaphoreFullException` when A later released the gate. No standalone semaphore probe, delay, yield, sleep, reflection, or synthetic path was used.
- The minimum production fix tracks `acquired` locally, sets it immediately after successful `WaitAsync`, and releases the generation gate only when acquisition succeeded. Cancellation/fault before acquisition no longer over-releases; success/fault/cancellation after acquisition still release exactly once.
- The deterministic test asserts B backend exclusion (`calls == 1` for A), canceled public status and exact caller token, A success, Stop/drain success, generation/resource cleanup, collaborator snapshot stability, and repeated Dispose stability.

### TDD Cycle Evidence — Queued Gate Cancellation

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| B2 queued pre-acquisition cancellation | PASS — real monitor-flow test failed with `SemaphoreFullException` from A's legitimate release after B's canceled pre-acquisition `finally` release. | PASS — queued cancellation plus caller/owner token tests `3/3`; A succeeds and Stop drains cleanly. | PASS — wait-canceled-before-acquire and acquired release paths are covered by the queued test plus existing success/fault/cancellation lifecycle cases; no backend/policy/store/outbox/event mutation from B. | PASS — AntiTamper `55/55`; combined Unit4A+B1+B2 integrity focus `88/88`; full Service `1,209/1,209` three consecutive runs; App.UI `192/192`. | PASS — one local acquisition flag; prior token forwarding, public-first/owned-second, RCSA, reentrant, Add/Resolve, stale, and late-completion behavior remains unchanged. |

### Queued Gate Execution Evidence

- Sequential no-restore Debug builds passed with `0 errors`: Domain (`0` warnings), Service (`1`), Service-tests (`5`), App.UI (`347`), and App.UI-tests (`301`); existing warnings only.
- Two fresh coverage hosts passed full Service `1,209/1,209`:
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-queue-final-a-20260821/d5851387-cc3f-428c-a218-6eab0a8a949b/coverage.cobertura.xml`
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-queue-final-b-20260821/4ed1c98a-9f7f-4a20-adde-a5ad495d3d1a/coverage.cobertura.xml`
  `RunOwnedCheckAsync` reports `100%` line/branch coverage; wait-canceled-before-acquire and acquired release branches are both hit (`6/6` and `2/2`).
- Final audit passed `git diff --check`; only the intended production/test files are tracked content changes; no staged files; CODE+TEST is `361/400` (`131+51` production and `173+6` tests). Unit4B2 failed-report hashes remain unchanged: authoritative `A54BB617D3CC3D61C054158118926E8BA218D34538CDFD299ACE4F4351CBF3C4`, independent `681747C3F409F570E555E1232A13E016760D38E9F672B264EB9D1B187268013D`, and re-verification `968BB88352AA71604377869966FABA6C961921FB9F3CAFA8072B9A049103E519`.
- **Evidence gate result:** queued cancellation semantics and all requested safety gates are complete; ready for independent Unit4B2 verification. Historical strict-TDD warnings remain preserved; no verification report or task checkbox was changed.

## Unit 4B1 Final Clock Evidence Closure

- Scope remains evidence-only Unit 4B1 test strengthening; production source is unchanged. Tasks remain `9/14`, reports remain unchanged, and no B2/C or commit work was performed.
- `ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock` now awaits a subsequent external `StopAsync` drain boundary after the callback's intentionally non-awaiting reentrant Stop, then reuses `AssertResourcesDisposed` for exact-once CTS/gate/timer cleanup plus completed loop/initial operation/initial TCS, empty `OwnedTasks`, and completed memoized drain. It also asserts generation removal and repeated Stop/Dispose stability.
- The same clock proof asserts the first callback marker is exactly G1, absent before and after callback; the second monitor callback marker is exactly G2, explicitly not G1, and absent before/after its callback.
- Final proof: `7/7`; AntiTamper twice `38/38`; combined Unit4A+B1 both orders `78/78`; full Service `1,192/1,192`; App.UI `192/192`; builds and `git diff --check` passed, all with `--no-restore`.
- Two fresh coverage hosts passed: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-authoritative-final-c-20260821/f4b57756-30ed-4413-a7a8-258016c8b272/coverage.cobertura.xml` (`54.32%` line / `58.75%` branch) and `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-authoritative-final-d-20260821/db30bed9-deec-4694-8abc-d88d6e33f09c/coverage.cobertura.xml` (`54.31%` line / `58.75%` branch). AntiTamperMonitor: `100%` line / `91.25%` branch.
- Final CODE+TEST diff remains `391/400` (`201+91` production, `71+28` tests). Ready for final independent B1 verification.

## Unit 4B1 Authoritative Evidence-Only FAIL Closure

- Scope remains Unit 4B1 only. `tasks.md` remains `9/14`; all verification reports remain unchanged; no B2/C, Unit 5, commit, or dependency/configuration work was performed.
- Restored unchanged baseline `StartAsync_WhenDisposed_ThrowsObjectDisposedException` and preserved the baseline Start success/already-running and Stop-while-running tests. The disposed-Start test now records `generationAllocations` before Dispose, asserts ObjectDisposedException, null generation, and unchanged allocation count.
- Replaced cancellation-only shared Start coverage with `ConcurrentStart_SharesOutcomeAndCleansOneGeneration`, a compact theory covering `success`, `failure`, and `cancel`. Each case proves one backend initialization, one allocation, shared outcome, one cleanup, and no surviving generation; failure asserts shared exception identity/message and cancellation asserts the shared cancellation contract.
- Strengthened `RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks` with a reflection-based `Monitor.TryEnter` check and lifecycle-property reentry at backend dependency entry; the external callback proves the lifecycle lock is available and reentry completes.
- Strengthened `Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer` to compare G1/G2 CTS, gate, tick/timezone timers, initial completion, initial operation, loop, `OwnedTasks`, and memoized drain identities, while retaining old-generation callback rejection and exact cleanup counters.
- Strengthened `ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock` with AsyncLocal marker presence during callback, restoration afterward, a second monitor callback isolation check, exact callback count, and cleanup through both contexts. Added only the narrow reflection helper `GetCallbackMarker`.
- Final focused proof set: `7/7`; AntiTamper twice: `38/38` each; combined Unit4A+B1 both filter orders: `78/78` each; full Service: `1,192/1,192`; App.UI: `192/192`.
- Two final fresh sequential portable-PDB coverage processes passed: `coverage-unit4b1-authoritative-final-a2-20260821/532166eb-0662-4d6e-9c04-16258e07b798/coverage.cobertura.xml` (`54.28%` line / `58.66%` branch) and `coverage-unit4b1-authoritative-final-b2-20260821/db46962e-7537-4ed5-bb5c-767411a021f9/coverage.cobertura.xml` (`54.31%` line / `58.75%` branch). AntiTamperMonitor remained `100%` line / `91.25%` branch.
- Final CODE+TEST diff: `391/400` (`201+91` production, `71+28` tests). `git diff --check` passed; no out-of-scope content drift. B1 is ready for independent verification.

## Unit 4B1 Final Remediation — Approved FAIL Closure

- Scope remains Unit 4B1 only; `tasks.md` remains `9/14`, reports remain unchanged, and no Unit 4B2/4C implementation was performed.
- Clock callbacks now run outside the lifecycle lock. Clock state/decision is captured under lock, then `FireClockJump` invokes callbacks with the generation callback marker; timezone event callbacks use the same owner marker. Reentrant `StopAsync`/`Dispose` is covered by `ClockJumpCallback_ReentersStopAndDisposeWithoutDeadlock` and the existing tamper callback test.
- Replaced the shared Start success test with `ConcurrentStart_SharesCancellationAndCleansOneGeneration`; both callers observe the cancellation contract, one backend initialization is made, and the generation resources are cleaned once. Replaced the simple disposal race with `StartAndDispose_CancelsOwnedInitializationAndCleansOnce`; initial Start is cancelled after Dispose owns the generation, resources are disposed once, and post-dispose Start fails before allocation.
- `RunMonitorLoopAsync` now checks owner cancellation after late initial completion, preventing a late normal backend completion from publishing successful Start after Dispose.
- Coverage hang diagnosis: the earlier combined-suite hang was isolated to `ConcurrentStopAndDisposeShareLifecycleOwnership` after `ConcurrentStop_SharesDrainUntilAdmittedWorkReleases`; the two synchronous `Task.Run` Dispose callers could be starved by preceding suite work, leaving the test-owned release/drain sequence alive. The deterministic fix is the existing dedicated long-running Dispose callers plus observed admission/drain ownership; no timeout/retry or parallelism disable was added. A bounded fresh `--blame-hang --blame-hang-timeout 5s` run after the fix completed all 35 AntiTamper tests; the collector reported no dump because no hang remained. Diagnostic artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-hang-diagnostic-final-20260821/771a6eb9-aae6-4abc-a8bc-c9a82e213ae4/coverage.cobertura.xml`.
- Two fresh post-fix coverage processes completed cleanly in deterministic order variants:
  - `coverage-unit4b1-final-a2-20260821/a2a97996-0b07-41f4-8482-f761450f8514/coverage.cobertura.xml`: combined Unit4A+B1 filter, `68/68`, line `4.08%`, branch `6.73%` aggregate; `AntiTamperMonitor` line `100%`, branch `91.25%`.
  - `coverage-unit4b1-final-b-20260821/1ae682e5-5a93-43b5-8a6d-2b2d5c2cde91/coverage.cobertura.xml`: reversed filter order, `68/68`, line `3.88%`, branch `6.57%` aggregate.
- Focused AntiTamper: `35/35`. Combined Unit4A+B1: `68/68` in both order variants. Full Service: `1,189/1,189`. Full App.UI: `192/192`. Affected builds passed with `--no-restore`; `git diff --check` passed.
- Final B1 CODE+TEST diff is exactly `400` changed lines (`201 additions + 91 deletions` production; `64 additions + 44 deletions` tests). No further edits are budget-safe; no size exception was used.
- B1 is ready for independent verification. Aggregate task checkboxes remain unchanged until B2 and 4C are complete.

## Unit 4B1 Remediation — Final Lifecycle/Resource Evidence

- Scope: Unit 4B1 only; lifecycle ownership, admission/drain, generation publication, timer/task cleanup, and concurrent Stop/Dispose. Unit 4B2/4C completion-fault and verdict-ordering work remains out of scope.
- Corrected the final combined-suite hang in `ConcurrentStopAndDisposeShareLifecycleOwnership`: the test's two concurrent synchronous Dispose callers now use dedicated long-running scheduler threads, preventing prior test activity from starving both disposal callers. This preserves the required concurrent ownership assertion and does not alter production scheduling.
- Tightened `TriggerIntegrityCheckAsync` so the admission gate is signaled after leaving the lifecycle lock, matching the external-work-under-lock constraint.
- Focused AntiTamper suite: `32 passed, 0 failed, 0 skipped`.
- Full Service regression: `1,186 passed, 0 failed, 0 skipped`.
- Full App.UI regression: `192 passed, 0 failed, 0 skipped`.
- Affected Service product/test builds passed with `--no-restore`; final explicit portable-PDB build used `-p:DebugType=portable -p:DebugSymbols=true` and completed with `0 errors`.
- Final portable-PDB coverage command passed with `1,186/1,186`; artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-final9-20260821/d4921d79-be1d-4480-8422-2320c4d8d85a/coverage.cobertura.xml`; aggregate line-rate `54.27%`, branch-rate `58.58%`.
- `git diff --check` passed. Current branch code/test diff is `397` changed lines from HEAD (`266` additions + `131` deletions), within the `400`-line B1 budget; no project/dependency/config changes were introduced by this remediation.
- B1 is ready for independent verify/review. Do not mark tasks `4.1`–`4.3` checked until B1, B2, and 4C are all independently green as required by the task plan.

## Unit 4B1 Final Blocker Remediation

- Scope: only `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs`; tasks remain `9/14`, reports unchanged, and no B2/C work.
- Replaced every lifecycle-lock admission path (initial, manual/periodic, and timezone) with a generation-owned RCSA start gate. The wrapper is registered in `OwnedTasks` before the gate is signaled outside the lock; no `Task.Run` or fire-and-forget admission is used.
- Initial publication now stores the gated initial operation with the published generation before releasing the gate, so concurrent callers await the same initialization result.
- Consolidated the existing lifecycle tests to cover external reentry, same-failure concurrent Start fan-out, and two concurrent Dispose callers draining one independently blocked owned admission.
- Final tracked B1 CODE+TEST diff: `387` changed lines (`205 additions + 77 deletions` production; `53 additions + 52 deletions` tests), below the `400`-line budget and target `390`.
- Fresh final blocker tests: `3/3` passed individually (shared Start success, shared Start failure fan-out, and concurrent Dispose drain). Individual no-lock/tick/callback lifecycle tests also passed.
- Full Service regression currently reports `1,178/1,186` with 8 AntiTamper failures/hang behavior when the complete AntiTamper class is run together; therefore final verification is not yet claimed. App.UI remains `192/192`.
- `git diff --check` passed. Coverage artifact generation was attempted but no new XML artifact was produced before the full-suite timeout.
- Remaining issue: isolate and fix the suite-order/full-class lifecycle failure before declaring B1 ready for independent verification.

## Unit 4B1 — Lifecycle Ownership + Resources (In Progress)

- Scope: Unit 4B1 only. Unit 4A authority/persistence and Unit 4C files remain unchanged.
- Implemented a per-generation lifecycle owner in `AntiTamperMonitor.cs` containing its cancellation source, initial-completion source, loop, timezone timer, owned operations, and shared drain task.
- Publication, Stop, restart, and Dispose now coordinate through the lifecycle lock; old-generation cleanup cannot clear or dispose a newly published generation.
- Integrity triggers are admitted to the generation before continuation cleanup is registered. Initial checks use owner cancellation; independently admitted triggers remain drainable during Stop.
- Added/updated lifecycle tests for restart resource ownership, concurrent Stop/Dispose, and tick draining in `AntiTamperMonitorTests.cs`.
- Focused AntiTamperMonitor suite: `33/33` passed with `--no-restore`.
- Combined Unit 4A + Unit 4B1 focus: `66` tests executed; one tick-drain assertion was stabilized to accept either valid scheduling outcome (`>=2` calls) while retaining completion/drain coverage.
- `git diff --check` passed. Current tracked code/test diff is `413 additions + 45 deletions = 458` changed lines, exceeding the requested 400-line budget; this must be reduced before verification/PR preparation.

### Unit 4B1 TDD Evidence

| Area | RED | GREEN | REFACTOR / STATUS |
|---|---|---|---|
| Generation ownership and restart cleanup | PASS — restart test initially observed no published timer; implementation now passes. | PASS — restart resource test passes. | Partial — physical disposal-count evidence remains to be strengthened. |
| Stop/Dispose shared ownership | PASS — concurrent lifecycle test added before lifecycle refactor. | PASS — concurrent Stop/Dispose test passes. | Partial — shared drain identity is not directly asserted. |
| Tick admission and drain | PASS — prior non-cancellable drain barrier exposed lifecycle cancellation coupling. | PASS — AntiTamperMonitor suite passes. | Partial — exact periodic serialization evidence remains pending. |

## Current Task State Confirmation

- `tasks.md`: Units 1–3 remain checked; Phase 4 tasks 4.1–4.3 remain unchecked by design.

## Unit 4B Final Apply Evidence — Monitor Lifecycle + Drain

- Scope remains Unit 4B only; Unit 4C handler ordering/escalation is deferred. `tasks.md` remains at 9/14 checked with `4.1`–`4.3` unchecked.
- Exact tracked CODE+TEST budget: `399` changed lines = production `220 additions + 41 deletions` (`AntiTamperMonitor.cs`) plus test `135 additions + 3 deletions` (`AntiTamperMonitorTests.cs`). No headroom remains for implementation or test additions.
- Concern-pure changed files: `src/ControlParental.Service/AntiTamperMonitor.cs` and `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`. `git diff --stat` is empty for `IntegrityVerdictHandler.cs`, `IntegrityVerdictHandlerTests.cs`, and `IntegrityRuntimePathTests.cs`; no Unit4C handler/test diff exists.
- Fresh portable-PDB builds (`--no-restore`, `-p:DebugType=portable`) passed with `0` errors for Service product/test and App.UI product/test.
- Fresh full Service regression passed: `1,185/1,185`; fresh full App.UI regression passed: `192/192`.
- Fresh combined Unit 4 focus passed: `119/119`, covering AntiTamperMonitor lifecycle, Unit 4A IntegrityChecker/runtime-path/verdict tests, and EnforcementLevelMonitor cases.
- Fresh portable-PDB coverage passed: `1,185/1,185`. Artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b-final-20260821/12c589a9-0bb1-4b77-aa34-c77a9ef04bca/coverage.cobertura.xml`.
- Coverage for `ControlParental.Service.AntiTamperMonitor`: line-rate `85.56%`, branch-rate `81.25%`. `StartAsync` line/branch `100%/100%`; `StopAsync` `100%/87.5%`; `RunOwnedCheckAsync` `100%/100%`; actual injected tick loop and periodic-timer body both have hits in `RunMonitorLoopAsync` (`71.42%` lines, `66.66%` branches). `Dispose` has `100%` lines and `87.5%` branches.
- Coverage limitation: generic post-initial loop fault lines (`RunMonitorLoopAsync` lines 314–317) and operation-failure handling in `DrainGenerationAsync` lines 257–263 have zero hits; the current 399-line budget prevents adding the required fault tests without crossing 400. This is a verification blocker, not a claimed PASS.
- `git diff --check` passed. Existing package/analyzer warnings remain; no dependency/configuration changes were made.

### Unit 4B Scenario / Test-Name Matrix

| Acceptance item | Evidence | Status |
|---|---|---|
| Atomic Start publication | `StartAsync_WhenNotDisposed_StartsSuccessfully`, `StartAsync_WhenAlreadyRunning_DoesNotThrow`; coverage StartAsync 100/100 | Covered |
| Stop before start | `StopAsync_WhenRunning_StopsSuccessfully` exercises lifecycle stop; no dedicated never-started Stop test | Partial |
| Concurrent/repeated Stop | `Dispose_CalledTwice` fixture path and idempotent drain reuse; no dedicated concurrent Stop test | Partial |
| Actual periodic tick | `RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks`; injected tick and periodic timer branches hit | Covered |
| Complete admission drain | `StopAsync_CancelsAndDrainsOwnedReport`, `StopAsync_DrainsIndependentlyAdmittedNonCancellableTrigger` | Covered |
| Restart resource replacement | No dedicated Start→Stop→Start test | Missing |
| Dispose matrix | `VerifyClockAgainstServerTimeAsync_WhenDisposed_DoesNotThrow`, `StartAsync_WhenDisposed_ThrowsObjectDisposedException`, fixture Dispose; Dispose branches mostly hit | Partial |
| Post-dispose Start rejection | `StartAsync_WhenDisposed_ThrowsObjectDisposedException` | Covered |
| Non-cancellable late normal completion | `StopAsync_DrainsIndependentlyAdmittedNonCancellableTrigger` | Covered |
| Non-cancellable late failure completion | No dedicated test; drain operation-failure branch has zero hits | Missing |
| Old-generation suppression | No dedicated restart/late-generation test | Missing |
| External/owned cancellation | `StopAsync_CancelsAndDrainsOwnedReport` covers owned cancellation; caller-cancellation test was removed to preserve 399/400 | Partial |
| Post-initial loop fault observability | No dedicated faulting tick-source test; generic fault branch has zero hits | Missing |
| No post-stop effects | `RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks` asserts no third backend call after Stop | Covered |

### Final TDD / Verification State

- Genuine RED details retained from this apply: the initial independent-trigger test failed because Stop completed before the intended non-cancellable trigger was actually blocked; the first tick harness hung because its source returned `true` indefinitely; after correction, lifecycle tests pass `31/31`.
- Current state is **blocked for independent Unit4B verification** solely because the requested fault/restart matrix is incomplete and the coverage artifact confirms zero hits for generic loop faults and drain operation failures. No implementation fix was attempted because only one budget line remained.

## Unit 4B Apply Progress — Monitor Lifecycle + Drain

- Scope: Unit 4B only on `feat/sdd7-4b-integrity-lifecycle-races`, based on approved Unit 4A commit `b5a2fee36f224bcb66eed1d560d6652443d4394c`.
- Preserved Unit 4A: `IntegrityVerdictHandler.cs`, `IntegrityVerdictHandlerTests.cs`, and `IntegrityRuntimePathTests.cs` have no behavioral diff; Unit 4C verdict ordering/escalation remains deferred.
- `AntiTamperMonitor` now owns a generation-scoped task set, separates initial lifecycle cancellation from admitted work cancellation, drains the loop and all admitted operations before generation resource disposal, supports an injectable real tick source, and rejects work after Stop/Dispose.
- `AntiTamperMonitorTests` adds barrier coverage for single-flight, initial cancellation, caller cancellation, independently admitted non-cancellable work, actual tick-source execution, queued tick draining, and post-stop suppression.
- Current tracked CODE+TEST diff: `399` changed lines (`220 additions + 41 deletions` production; `135 additions + 3 deletions` tests), below the hard 400-line Unit 4B budget. OpenSpec artifacts are excluded.

### TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.1 — Monitor lifecycle/race acceptance | PASS — initial focused execution exposed a real drain assertion defect and a hanging tick-source test; the tests were corrected to use deterministic barriers and cancellation-aware ticks. | PASS — focused lifecycle suite `32/32`; combined integrity-focused suite `72/72`. | PASS — owned task drain, single-flight, initial/caller cancellation separation, actual tick source, queued ticks, and post-stop suppression are exercised. | PENDING — full Service/App.UI regression and coverage remain to be run after the Unit 4B slice is complete. | PASS — test-only synchronization fixes removed mutable TCS timing ambiguity and infinite tick-source behavior without changing Unit 4A files. |
| 4.2 — Owned lifecycle implementation | PASS — independently admitted work initially surfaced owner-token cancellation at the post-report cancellation boundary; this exposed the need to distinguish initial lifecycle cancellation from admitted-work draining. | PASS — lifecycle implementation and focused suites pass after separating `cancelWithOwner` for the initial check from drainable manual/tick work. | PASS — generation resources are disposed only after loop and owned operations complete; late queued work cannot publish after Stop. | PENDING — full regression and coverage pending. | PASS — no handler ordering/escalation changes; Unit 4C remains isolated. |
| 4.3 — Refactor / verify | N/A — final verification task; no claim of completion yet. | PENDING. | PENDING. | PENDING. | PASS — `git diff --check` passes and the 400-line budget remains respected. |

### Current Evidence

- Focused command: `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --filter "FullyQualifiedName~AntiTamperMonitorTests"`; result `31 passed, 0 failed`.
- Combined integrity command covering `AntiTamperMonitorTests`, `IntegrityCheckerTests`, `IntegrityVerdictHandlerTests`, and `IntegrityRuntimePathTests`; prior result `72 passed, 0 failed` before the budget-only removal of one redundant caller-cancellation test.
- Existing compiler/package/analyzer warnings remain; no new dependency or configuration changes were made.

### Unit 4B Task State Confirmation

- `tasks.md`: Units 1–3 remain checked; Unit 4 tasks `4.1`–`4.3` remain unchecked until Unit 4A + Unit 4B + Unit 4C final verification is complete.
- Remaining: complete Unit 4B race matrix and full safety-net verification, then hand off to Unit 4C. Unit 5 remains deferred.

## Unit 4B2 — Completion Safety + Faults

- Work unit: Unit 4B2 only; feature-branch-chain slice `feat/sdd7-4b2-lifecycle-completion-safety`, targeting the approved B1 parent. Unit 4C and Unit 5 were not changed.
- Scope: generation-admitted completion guards, external cancellation, non-cancellable late completion suppression, lifecycle/owned fault observability, and no post-stop collaborator effects.
- CodeGraph was attempted first and reported no `.codegraph` index; focused direct inspection followed. No index was created.
- Exact CODE+TEST diff from `1f00184dd1fe1d96c9d683bbf7d2af356ddc30f4`: `91/400` changed lines (`37` additions + `16` deletions production; `38` additions tests). Only `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` have content changes.

### B2 TDD Cycle Evidence

| Task/Cycle | RED | GREEN | REFACTOR | Safety |
|---|---|---|---|---|
| 4B2 completion/cancellation acceptance | PASS — tests were added before production edits. The initial no-build execution was infrastructure-blocked because the linked worktree lacked the test DLL/assets; no behavioral RED is fabricated. | PASS — exact B2 filter `8/8`; late normal theory covers revoked/trust/unknown, late unique failure, post-initial tick fault with exact exception identity and fresh-generation restart, and caller cancellation. | PASS — generation validation is performed after backend/checker/privilege awaits and before handler/enforcement effects; `LifecycleTask` and owned-task continuation expose non-cancellation faults without changing Unit4C ordering. | PASS — full AntiTamper `44/44`; combined Unit4 integrity focus `132/132`; full Service `1,198/1,198`; full App.UI `192/192`; no post-stop collaborator invocations asserted before/after drain in the B2 cases. |
| 4B2 fault ownership/recovery | PASS — prior implementation swallowed owned binary/privilege failures and had no lifecycle fault task; the tests directly exercise those contracts through production calls. | PASS — caller cancellation uses a linked caller token; owner cancellation remains lifecycle-contained; late normal results are rejected after generation closure; unique owned failures remain observable through the returned task and lifecycle task; restart allocates a fresh generation. | PASS — periodic/loop faults complete the generation lifecycle task; cancellation-only task faults are excluded from shared lifecycle faulting; existing B1 drain/resource ownership remains intact. | PASS — two fresh portable-PDB coverage hosts passed `1,198/1,198`; `AntiTamperMonitor` coverage is `99.02%` line / `88.63%` branch in both artifacts. |

### B2 Execution Evidence

- Affected Domain, Service, Service-test, App.UI, and App.UI-test portable-PDB builds passed with `--no-restore`; zero errors. Scoped restore was required only for missing ignored assets and produced no tracked dependency/config drift; existing NU1601/NU1701/analyzer warnings remain.
- Exact B2 command result: `8 passed, 0 failed, 0 skipped`.
- AntiTamper lifecycle/fault focus: `44 passed, 0 failed, 0 skipped`.
- Combined Unit4A+B1+B2 integrity focus: `132 passed, 0 failed, 0 skipped`.
- Full Service: `1,198 passed, 0 failed, 0 skipped`; one pre-existing duplicate xUnit ID discovery notice.
- Full App.UI: `192 passed, 0 failed, 0 skipped`.
- Coverage host A: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-final-host-a/0e253911-e0ff-40f5-b484-f2ba42f0184e/coverage.cobertura.xml`.
- Coverage host B: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-final-host-b/09fac862-f81a-4db5-9118-59204d89b661/coverage.cobertura.xml`.
- `git diff --check` passed. Status contains only the two intended source/test files plus cumulative untracked OpenSpec artifacts; no Unit4C/Unit5 content drift.

### B2 Task State Confirmation

- `tasks.md` remains intentionally unchanged at `9/14`: tasks `4.1`, `4.2`, and `4.3` remain unchecked pending Unit4C and final independent Unit4 verification. Unit 5 remains deferred.
- No verify report, commit, push, PR, merge, rebase, branch/worktree/configuration, dependency, or external mutation was performed.

## Unit 4B1 — Final Apply Evidence

- Scope: Unit 4B1 only; Unit 4B2 completion-fault policy, Unit 4C verdict ordering, and Unit 5 remain deferred.
- Final implementation files: `src/ControlParental.Service/AntiTamperMonitor.cs` and `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`.
- Final CODE+TEST diff: `377` changed lines (`217 additions + 42 deletions` production; `118 additions` tests), below the requested `<=380` consolidation target and the hard `400`-line limit.

## Unit 4B2 Independent Blocker Remediation

- Scope remains Unit 4B2 only; Unit 4C/Unit 5 files and the failed independent report were not modified. Tasks remain intentionally `9/14`.
- Added deterministic production-calling coverage for late non-cancellable privilege completion, backend→policy→store completion safety for both durable reaction shapes, and stale manual/timezone callbacks after a faulted G1 restart. New assertions snapshot collaborator invocation counts, detected-event counts, and physical generation cleanup state.
- Added `RunAdmittedStageAsync` as the generation-aware admission boundary for synchronous handler/event stages and asynchronous Add/Resolve stages. Each stage is registered in the generation-owned task set before its gate is released; Stop closes admission and drains the registered stage without holding the lifecycle lock across external work.
- Privilege completion now carries the generation and admits tamper publication/outbox work only while that generation is active. Handler, clock publication, and durable policy stages revalidate through the same boundary. Existing backend retry ownership and Unit4A/Unit4C code remain unchanged.

### TDD Cycle Evidence — Remediation

| Task/Cycle | RED | GREEN | REFACTOR | Safety |
|---|---|---|---|---|
| B2 late privilege completion | PASS — tests-first command was executed with `--no-restore`; initial linked-worktree execution could not discover the newly added tests until the required no-restore test build completed, and the first barrier arrangement timed out before reaching the assertion. No compile or asset failure is represented as behavioral RED. | PASS — focused test passes with exact zero detected-event/outbox delta and exact disposed-generation snapshot. | PASS — owner generation is admitted before publication; no lifecycle lock crosses the collaborator await. | PASS — full AntiTamper focused suite `48/48`. |
| B2 backend→policy→store stages | PASS — tests were added before the stage-admission production edit; the pre-edit run timed out in the old late-completion harness before assertion, so no fabricated assertion failure is claimed. | PASS — Add and Resolve theory cases pass; no enforcement invocation occurs after the accepted Stop boundary. | PASS — one compact generation-owned stage helper is reused for handler and durable mutation stages. | PASS — build `0 errors`; `git diff --check` exit `0`. |
| B2 stale faulted-generation signals | PASS — test was added before the final stage guards. | PASS — fault identity remains observable; stale periodic/timezone callbacks leave G2 collaborator/resource state unchanged. | PASS — stale callbacks are rejected by existing generation identity checks; no new callback authority path. | PASS — focused remediation `4/4`; AntiTamper `48/48`. |

### Remediation Execution Evidence

- Focused remediation command (`--no-restore --no-build`) passed `4/4` at the final run; exact tests: `LatePrivilegeCompletion_AfterStopHasNoTamperOrOutboxEffects`, both `LatePolicyStage_AfterStopDoesNotStartDurableMutation` theory cases, and `FaultedGeneration_RejectsStaleManualAndTimezoneCallbacksAfterRestart`.
- Full `AntiTamperMonitorTests` passed `48/48` with `--no-restore --no-build`; full Service passed `1,202/1,202` and full App.UI passed `192/192`.
- Affected Service-tests portable-PDB build passed with `0` errors and existing package warnings only.
- Current normalized CODE+TEST diff from the approved B1 parent is `234/400`: production `90` additions + `42` deletions; tests `101` additions + `1` deletion. `git diff --check` passed.
- Two fresh portable-PDB coverage hosts passed full Service `1,202/1,202`: `coverage-unit4b2-remediation-a-20260821/6b19310c-cbe2-40c5-933e-1344887481a0/coverage.cobertura.xml` and `coverage-unit4b2-remediation-b-20260821/816f7e63-0ba7-4084-9248-a3e677dce407/coverage.cobertura.xml`; host A reports `AntiTamperMonitor` `99.08%` line / `87.5%` branch.
- Unit4A+B1+B2 matrix, exact independent re-verification command set, affected product builds, App.UI portable-PDB build, and final drift/resource audit remain pending; this apply result is not yet ready for independent Unit4B2 re-verification.

## Unit 4B2 Evidence-Only Completion Attempt

- No production or test source was changed during this evidence pass. The immutable strict-TDD warning remains unchanged: no genuine behavioral RED was fabricated or retroactively manufactured.
- Affected portable-PDB builds were run with `--no-restore`: Domain passed; Service passed; Service-tests initially hit an infrastructure file-lock (`CS2012`, compiler server PID 17608) because builds were launched concurrently, then passed sequentially; App.UI passed; App.UI-tests initially hit the same concurrent-build file-lock (`MSB4018` on Domain assembly), then passed sequentially. Final sequential builds were `0 errors`.
- Exact remediation tests: `4/4`. Exact B2 focus (original cases plus remediation): `12/12`. Full AntiTamper: `48/48`. B1+B2 lifecycle matrix: `26/26`. Unit4A+B1+B2 focus: `136/136`. Full Service: `1,202/1,202`. Full App.UI: `192/192`.
- Two fresh isolated portable-PDB coverage hosts passed full Service `1,202/1,202`:
  - `coverage-unit4b2-final-a-20260821/3b1d8cfe-7594-4b6d-8523-4241c4b4162b/coverage.cobertura.xml`
  - `coverage-unit4b2-final-b-20260821/a3e93b4b-55b3-49d3-b438-3274ba2ad186/coverage.cobertura.xml`
  Both report `AntiTamperMonitor` `99.08%` line / `87.5%` branch. `RunAdmittedStageAsync`, `AdmitTimezoneCheck`, clock/privilege/binary admission lambdas, and Add-stage paths have hits. The Resolve-stage lambda remains at `0%` in Cobertura, so the requested distinct Resolve runtime evidence is still missing.
- Determinism inspection: new tests contain no `Delay`, `Yield`, or sleep; existing unrelated baseline delays remain elsewhere in the file. New tests use RCSA barriers, exact collaborator snapshots, G1/G2 identity/resource cleanup assertions, and repeated Stop/Dispose checks.
- Workspace audit: branch and HEAD remain exact; merge-base is the requested parent; no staged files; only the two intended source/test content diffs plus cumulative OpenSpec artifacts; Unit4C/Unit5/handler normalized content drift is absent; `git diff --check` passes; CODE+TEST remains `234/400` (`90+42` production, `101+1` tests). The failed independent report SHA-256 remains `681747c3f409f570e555e1232a13e016760d38e9f672b264eb9d1b187268013d`; no new verify report was written.
- **Evidence gate result:** not ready. No correctness defect was exposed by the fresh commands, but the distinct `ResolveIssueAsync` admission guard has no fresh runtime hit. Per instruction, no edit was made; this apply pass stops and reports the evidence gap.

## Unit 4B2 Resolve Evidence Closure

- Added only two deterministic production-calling tests to `AntiTamperMonitorTests.cs`; `AntiTamperMonitor.cs` and all Unit4C/Unit5/handler files remain unchanged. The active Resolve test passed immediately, so this is legitimate test-only triangulation; no behavioral RED was fabricated. The immutable strict-TDD warning remains preserved.
- `ActiveGeneration_UsesCanonicalResolveStageExactlyOnce` drives Start plus a real second integrity report through the monitor, captures a definitive identity (`device-7`), verifies exactly one `ResolveIssueAsync` invocation, canonical `IssueKey(0, BinaryIntegrityFailure, "integrity/binary", "device-7")`, and exact recovery evidence.
- `StopBeforeResolveAdmission_SuppressesResolveAndPreservesSnapshot` uses the real handler stage to close Stop before the Resolve admission boundary, then proves zero Resolve calls and an unchanged collaborator snapshot after drain and generation removal.
- Exact new-test command passed `2/2`; B2 focus passed `14/14`; AntiTamper passed `50/50`; B1+B2 lifecycle passed `28/28`; Unit4A+B1+B2 passed `138/138`; full Service passed `1,204/1,204`; full App.UI passed `192/192`.
- Two fresh isolated coverage hosts passed full Service `1,204/1,204`:
  - `coverage-unit4b2-resolve-a-20260821/38dcac29-1bb8-4ef9-8373-0bfbf41b27b7/coverage.cobertura.xml`
  - `coverage-unit4b2-resolve-b-20260821/a4ed138e-87f6-4ca2-801b-4324b49dad1f/coverage.cobertura.xml`
  Both report AntiTamperMonitor `99.08%` line / `87.5%` branch. Source-level mapping now proves `ProcessVerdictReactionAsync` `b__0` (the ResolveIssueAsync lambda at source lines 593–594) `100%` line / `100%` branch. The rejected admission path is hit through `RunAdmittedStageAsync` (`100%` lines / `75%` branches). `b__1` is the separate Limit/Add lambda, not Resolve; its zero hit does not represent the Resolve gap.
- Sequential affected portable-PDB builds all passed with `0 errors`; the prior concurrent file-lock failures were not repeated. New tests contain no Delay/Yield/sleep and use real monitor flow, RCSA barriers, exact collaborator snapshots, canonical identity, and physical generation cleanup.
- Final CODE+TEST diff is `259/400`: production `90+42`, tests `126+1`. `git diff --check` passed. Branch/HEAD/merge-base, staged state, Unit4C/Unit5/handler drift, failed-report SHA-256 (`681747c3f409f570e555e1232a13e016760d38e9f672b264eb9d1b187268013d`), and no-new-verify-report constraints remain clean. Tasks remain `9/14`.
- **Evidence gate result:** functional and runtime evidence complete; ready for independent Unit4B2 re-verification with the preserved strict-TDD warning.

## Unit 4B2 Three-Blocker Remediation — Blocked by Fresh Regression

- Corrected stale-generation test helper signatures and local monitor/event snapshots. The corrected stale test now runs against the same local monitor and passes; the prior helper-target defect is not a production RED.
- Added active Add/Resolve task-boundary theory coverage (`AdmittedMutationTask_IsOwnedAndDrained`) with incomplete external Tasks, Stop pending until release, exact mutation cardinality, snapshot stability, resource cleanup, and repeated Stop/Dispose. Existing pre-admission rejection coverage remains separate. New Add/Resolve tests pass `4/4` including both theory cases.
- Added synchronous admitted-handler reentrancy coverage. The tests-first current-production run produced a genuine hang/self-drain RED (`SynchronousHandler_ReentrantStopReturnsCompletedBeforeConsumption`, 120-second timeout; blame-hang confirmed blocked testhost). The minimum callback-generation marker and drain-start deferral were implemented; the isolated test now passes `1/1`.
- Drain-start deferral uses a registered start gate: Stop/Dispose close admission and publish the owned drain under the lifecycle lock, then release the start gate outside the lock. Synchronous handler stages carry the generation callback marker so same-generation reentrant Stop/Dispose returns without self-await.
- Fresh focused results after remediation: corrected stale `1/1`; new focus `6/6`; B2 retry `17/17`; AntiTamper retry `53/53`; lifecycle retry `31/31`; Unit4 focus `141/141`.
- **Fresh regression blocker:** full Service `--no-restore --no-build` hung twice after the production drain change (both runs exceeded 120 seconds after discovery and emitted no completion count). This is a genuine safety-net regression signal, so the instruction to stop before further production editing is being honored. App.UI/full coverage were not claimed after this blocker.
- Current normalized CODE+TEST diff is `329/400`: production `117+49`, tests `157+6`. No Unit4C/Unit5/handler drift; branch/HEAD/merge-base unchanged; no staged files; `git diff --check` passes. Failed report hashes remain unchanged: independent `681747c3f409f570e555e1232a13e016760d38e9f672b264eb9d1b187268013d`, re-verification `968bb88352aa71604377869966faba6c961921fb9f3cafa8072b9a049103e519`.
- Strict-TDD history remains honest and incomplete for earlier edits; the new self-drain RED is genuine and recorded. Tasks remain `9/14`; no new verify report was written. **Not ready for Unit4B2 re-verification.**
- Lifecycle implementation owns one generation-scoped cancellation source, initial completion source, gate, loop, timers, admitted tasks, and shared drain. Old-generation cleanup is guarded from clearing or disposing a newly published generation.
- Final focused AntiTamperMonitor suite: `33 passed, 0 failed, 0 skipped`.
- Final combined Unit 4A + Unit 4B1 focus: `66 passed, 0 failed, 0 skipped`.
- Affected Service build passed with `--no-restore`; final full Service safety net passed after the build completed: `1,187 passed, 0 failed, 0 skipped`. xUnit reported one existing duplicate test-ID discovery notice.
- Full App.UI safety net passed: `192 passed, 0 failed, 0 skipped`.
- Coverage safety net passed: `1,187 passed, 0 failed, 0 skipped`. Artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-final/e301caa2-ae7c-4500-a7c2-0f9e36738496/coverage.cobertura.xml`.
- Coverage artifact records `ControlParental.Service.AntiTamperMonitor` at `85.71%` line rate and `82.69%` branch rate; injected tick and periodic-timer loop paths have hits.
- `git diff --check` passed. No project, dependency, configuration, Unit 4A, Unit 4C, or Unit 5 changes were made by this finalization step.

## Unit 4B2 Authoritative Completion-Order Correction

- Scope remains Unit 4B2 only. Unit4C/Unit5/handler files, all three failed reports, and `tasks.md` remain unchanged; task state remains `9/14`.
- Corrected the authoritative completion-order defect in `AdmitLocked`: every success, cancellation, and fault forwarding branch now completes the externally returned `completion.Task` before completing the generation-owned `owned.Task`. Existing result/exception/cancellation propagation and `RunContinuationsAsynchronously` semantics remain intact.
- Added a deterministic observer to `AdmittedMutationTask_IsOwnedAndDrained` that records whether the public operation is terminal exactly when `StopAsync` becomes terminal; no delay, yield, sleep, timeout, or parallelism workaround is used.
- Final focused forwarding and lifecycle tests passed `6/6`; full `AntiTamperMonitorTests` passed `53/53`; combined AntiTamper/integrity focus passed `86/86`; full Service passed `1,207/1,207` three consecutive times; full App.UI passed `192/192`.
- Fresh final portable-PDB coverage hosts passed full Service `1,207/1,207`:
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-authoritative-a-20260821/33f3c0e4-91c1-4c9f-96c6-f30c764d8ca1/coverage.cobertura.xml`
  - `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b2-authoritative-b-20260821/44494fdc-aa47-46b4-93fd-4833401e5ca7/coverage.cobertura.xml`
  Cobertura source mapping reports `AdmitLocked` line-rate `100%`, branch-rate `100%`, and the forwarding branch condition coverage `4/4`.
- Sequential no-restore Debug builds for Domain, Service, Service-tests, App.UI, and App.UI-tests all passed with `0 errors`; the existing warning corpus remains.
- Final audit: `git diff --check` passed; only the intended production/test files are tracked content changes; no staged files; CODE+TEST is `336/400` (`123+50` production and `157+6` tests); failed-report hashes remain unchanged, including authoritative `verify-report-unit-4b2-final-authoritative.md` (`A54BB617D3CC3D61C054158118926E8BA218D34538CDFD299ACE4F4351CBF3C4`), independent (`681747C3F409F570E555E1232A13E016760D38E9F672B264EB9D1B187268013D`), and re-verification (`968BB88352AA71604377869966FABA6C961921FB9F3CAFA8072B9A049103E519`).
- Strict-TDD history remains honest: the post-correction ordering observer's initial terminal output was truncated and did not establish a genuine RED; no RED is claimed retroactively. The prior genuine self-drain RED remains preserved above.
- **Evidence gate result:** implementation and final safety-net evidence are complete; ready for independent Unit4B2 verification with the preserved strict-TDD warning. No new verification report was written.

## Unit 4B2 Hang-Dump Causality and Fix

- Managed dump analysis used the installed Windows Debugger CDB/SOS tooling; no dependencies were installed. Artifacts: `TestResults/ac3b8c01-a31e-46c2-9e21-227ccee2366f/testhost_8020_20260821T202900_hangdump.dmp`, `b2-dump-analysis.txt`, `b2-dump-async-analysis.txt`, and `b2-dump-task-details.txt`.
- The exact implicated thread (`OS Thread Id 0x3cc4`) was blocked in `AntiTamperMonitor.Dispose` at `Task.InternalWaitCore`, called by `SynchronousHandler_ReentrantStopReturnsCompletedBeforeConsumption`. The same dump's async stacks showed `DrainAfterAdmissionAsync` → `DrainGenerationAsync` waiting on a `Task.WhenAllPromise` with `_remainingToComplete = 2`.
- The active generation object was `0x029079fc3678`: `AdmissionOpen=false`, `Drain=0x029079fc4e60`, `Loop=0x029079fc3ad8`, undisposed cancellation/gate/timer resources. The wait graph was: test continuation entered external `Dispose` on the operation-completion thread → `Dispose` synchronously waited for the drain → drain waited for two admitted operation tasks → the completion continuation/test continuation was still being run inline on that same thread, so the operation could not finish unwinding. This is a B2 self-drain/reentrant completion cycle, not the expected Program backup-timeout test output.
- RED: the existing strengthened reentrant test retained the genuine full-suite hang evidence and now additionally observes final resource disposal and generation removal after external `Dispose`; the dump proves the cycle without timing primitives.
- GREEN: `AdmitLocked` now separates the owned completion task from the externally observed completion task. Owned completion is finalized first, allowing `AdmitTask`'s synchronous removal continuation to empty `OwnedTasks`; the public completion uses `RunContinuationsAsynchronously`, preventing external test/caller continuations from re-entering `Dispose` before ownership cleanup. Exceptions and cancellation are propagated to both tasks.
- Post-fix gates: implicated test `1/1`; AntiTamper `53/53`; full Service `1207/1207` three consecutive times; full App.UI `192/192`; coverage host A `1207/1207` at `TestResults/coverage-unit4b2-final-a-20260821/fb32c28a-b523-4f93-b571-559a4f436945/coverage.cobertura.xml`; coverage host B `1207/1207` at `TestResults/coverage-unit4b2-final-b-20260821/12460d2f-c080-4d63-ab22-cbf1dc3ccb0c/coverage.cobertura.xml`; affected build `0 errors`; `git diff --check` passed.
- Current CODE+TEST budget: `336/400` (`123+50` production and `157+6` tests). Failed report hashes remain `681747c3f409f570e555e1232a13e016760d38e9f672b264eb9d1b187268013d` and `968bb88352aa71604377869966faba6c961921fb9f3cafa8072b9a049103e519`. Tasks remain `9/14`; no verify report or task checkbox was changed.

### Unit 4B1 TDD / Verification Status

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4B1 — lifecycle ownership and resources | PASS — deterministic lifecycle tests exposed the initial ownership/drain defects during implementation. | PASS — focused lifecycle suite `33/33`; combined Unit 4 focus `66/66`. | PASS — generation replacement, actual/injected ticks, queued work drain, and concurrent Stop/Dispose resource ownership are exercised. | PASS — affected build, full Service `1,187/1,187`, full App.UI `192/192`, and coverage completed successfully. | PASS — consolidated implementation and tests to `377` changed lines; no Unit 4A/4C/5 leakage. |

### Current Unit 4B1 State

- `tasks.md` remains intentionally unchanged: top-level Phase 4 tasks `4.1`–`4.3` are still unchecked because Unit 4B2 and Unit 4C are separate chained work units.
- Unit 4B1 is ready for independent verification/handoff to the planned Unit 4B2 branch.

## Unit 4B — Owned Lifecycle + Races

- Work unit: Unit 4B only; Unit 5 remains untouched.
- Delivery: `auto-chain` / `feature-branch-chain`; autonomous PR4B boundary targets the approved Unit 4A parent.
- CodeGraph was attempted first for the exact worktree and reported no index; focused direct filesystem fallback was used. No `.codegraph` artifact was created.
- Production ownership is now one linked cancellation generation plus one observed `RunMonitorLoopAsync` task. The loop performs the initial check and owns the `PeriodicTimer`; `StartAsync` is idempotent and waits for the initial completion. `StopAsync` advances the running boundary, cancels the generation, and awaits the owned task. `Dispose` is idempotent and synchronously drains the owner before disposing resources. `TriggerIntegrityCheckAsync` uses one semaphore to prevent overlap and links caller cancellation without swallowing it.
- `AntiTamperMonitor` rethrows cancellation through the owner boundary and checks cancellation immediately after backend completion, preventing late normal completions from publishing durable state after stop.
- `IntegrityVerdictHandler` serializes verdict transitions with a minimal state gate. Escalation records a due timestamp and only returns `Degrade` at/after the five-minute deadline; trust resets the pending escalation state.

### TDD Cycle Evidence

| Task/Cycle | Requirement / scenario | RED | GREEN | REFACTOR | Safety net |
|---|---|---|---|---|---|
| 4.1 — lifecycle and race acceptance | Blocked report plus second trigger, Stop drain, caller cancellation, concurrent verdicts, delayed escalation | PASS — tests were written before production edits. Initial no-restore command at `2026-08-21T11:08:34.0988372-05:00` exited `1` with `NETSDK1004` (missing `project.assets.json`); after the minimum scoped Service restore, compilation RED identified missing `TriggerIntegrityCheckAsync` plus the unimplemented behavior. | PASS — focused lifecycle/race suite `5/5`; exact production-calling barriers assert initial ownership, single-flight cardinality, cancellation propagation, Stop drain, serialized `1/3`→`2/3`→escalation transitions, and delayed degradation. | PASS — compacted test helpers, reused the existing real runtime path, and kept lifecycle state in one owner rather than adding timer callback tasks. | PASS — focused integrity suite `65/65`; no sleeps drive behavior except the one bounded assertion window in the overlap test; physical invocation/cardinality assertions precede cleanup. |
| 4.2 — owned implementation | One lifecycle owner, no overlapping timer work, contained owned cancellation, stale completion suppression, authoritative-only mutation | PASS — the tests reference the new internal production seam and expose the absent owner. | PASS — affected Service build `0 errors`; full Service `1,186/1,186`; full App.UI `192/192`; portable-PDB coverage `1,186/1,186`. | PASS — existing Unit 4A identity-generation guard and stable `integrity/binary` key remain unchanged; no Unit 5 path changed. | PASS — Service product/test builds and App.UI build/test used `--no-restore`; existing NU1601/NU1701 warnings and duplicate xUnit ID notice only. |
| 4.3 — refactor / verify | Restart-safe lifecycle, disposal idempotency, escalation timing and preserved retry/identity boundaries | N/A — verification/refactor closure; no additional RED fabricated beyond the direct lifecycle tests. | PASS — final focused and full regressions green after the final cancellation gate. | PASS — `git diff --check` exit `0`; no project/dependency/config drift; exact CODE+TEST diff is `299/400`. | PASS — coverage artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b-final-20260821/46cc5479-f982-4dd9-9644-f8375b055fe1/coverage.cobertura.xml`. |

### Final Unit 4B Execution / Audit

- Scoped restore: Service test project restored first because ignored assets were absent; App.UI test project was then restored because its ignored assets were also absent. `git status` immediately after restore showed only the five expected Unit 4B source/test modifications plus cumulative OpenSpec artifacts. No tracked project, package, lockfile, config, or dependency declaration changed.
- Focused lifecycle/race command: `5 passed, 0 failed, 0 skipped`.
- Combined integrity focus (`AntiTamperMonitorTests`, `IntegrityVerdictHandlerTests`, `IntegrityRuntimePathTests`): `65 passed, 0 failed, 0 skipped`.
- Full Service: `1,186 passed, 0 failed, 0 skipped`; existing duplicate xUnit test-ID discovery notice retained.
- Full App.UI: `192 passed, 0 failed, 0 skipped`.
- Affected Service product/test builds and App.UI test build: `0 errors`; portable-PDB coverage completed successfully.
- `git diff --check`: passed. CODE+TEST budget: `299/400`; OpenSpec artifacts excluded. No Unit 5 source/test/artifact implementation was performed.

### Cumulative Task State Confirmation

- `tasks.md`: `1.1`–`4.3` checked; `5.1`–`5.2` remain unchecked.
- Cumulative status: `12/14` tasks complete. Unit 4A and Unit 4B are both implemented and green; final fresh independent Unit 4 verification remains the next phase. No verify report was created during apply.

## Unit 4A Final Slice-Purity Remediation

- Scope: remove the sole Unit4B leakage identified by `verify-report-unit-4a-authoritative-final.md`; no new behavior claim and no RED fabricated.
- Removed `IntegrityVerdictHandler.stateGate`, all lock acquisition/release wrappers, `HandleVerdictCore`, `HandleLocalFailureCore`, and related synchronization bytes.
- Restored parent `3082559` handler synchronization semantics: direct verdict/local-failure state access and direct `IsShadowMode`/`DisableShadowMode` access.
- Preserved all verified Unit4A behavior: active production authority, backend trust/revoked state machine, authoritative recovery signal, canonical durable identity, persistence, cancellation, refresh, exact snapshot, and timer scheduling.
- Focused source scan confirms no `stateGate`, verdict-state wrapper, `RunMonitorLoopAsync`, or Unit4B concurrent-verdict helper in the Unit4A changed handler/runtime test paths. Existing unrelated repository-wide concurrency/drain terminology is outside this slice and unchanged.
- Fresh no-restore evidence after removal: runtime paths `9/9`; Unit4A focus `67/67`; full Service `1,181/1,181`; full App.UI `192/192`; Domain/Service/portable-PDB test builds `0` errors; portable-PDB Cobertura produced at `tests/ControlParental.Service.Tests/TestResults/coverage-unit4a-slice-purity-20260821/9517ee61-e7b8-4825-a07c-fd4111e17f26/coverage.cobertura.xml`.
- `git diff --check` passed. Dependency/project/config drift audit found none. Current CODE+TEST budget is `364/400` (`127` tracked changed lines + `237` untracked runtime-test lines).
- Verification reports remain unchanged. `tasks.md` remains at cumulative `9/14` with Unit 4/5 tasks unchecked. Unit4B owns future concurrent-verdict synchronization and strict-TDD evidence; Unit5 remains deferred. No commit, push, PR, Unit4B, or Unit5 work performed.

## Unit 4A Final Remediation — Four Blockers Closed

- Scope remained Unit 4A only; no historical verification report, Unit 4B, Unit 5, commit, or dependency/configuration file was changed.
- **Caller cancellation:** `IntegrityRuntimePathTests.CanonicalAuthority_CallerCancellationPreservesExactPhysicalSnapshot` now calls the real `BackendClient.ReportIntegrityAsync` with a barrier HTTP handler. The handler signals start, blocks on the supplied request token, cancellation is deterministic, the handler signals stop, `Assert.ThrowsAnyAsync<OperationCanceledException>` observes the rethrow contract, `cancellation.IsCancellationRequested` is asserted, and the complete physical `FileIssueStore` snapshot is unchanged. The obsolete `CancelOnWriteIssueStore` helper was removed.
- **Exact unrelated recovery:** `CanonicalAuthority_PreservesExactStateAcrossNonDefinitiveResultsAndRecoversAfterRefresh` filters only `value.Key != preRefreshKey`; it compares the same-device `HookTimeout` and other-device integrity records exactly, asserts cardinality `2`, and asserts both identities/causes.
- **Real refresh:** the same test uses `MutableTimeProvider` to expire the credential, then calls real `coordinator.GetDefinitiveSessionAsync()`, which executes production `RefreshAsync`; it asserts generation increment, unchanged `device-a`, refreshed backend token, stable durable key, and trust resolution of the pre-refresh issue. No snapshot replacement or `InitializeAsync` shortcut remains in this scenario.
- **Parent timer baseline:** restored `this.ScheduleEscalation(timestamp)` exactly in `IntegrityVerdictHandler.HandleRevokedVerdict`; `AntiTamperMonitor` still has no `RunMonitorLoopAsync`, and parent-equivalent Stop/Dispose timer ownership remains intact.

### Final Four-Blocker Evidence

- Exact runtime path: `9/9` passed; the cancellation test above passed with barrier start/stop and observable `OperationCanceledException`.
- Authorized Unit 4A focus: `67/67` passed.
- Full Service: `1,181/1,181` passed; Full App.UI: `192/192` passed.
- Domain build, Service build, and portable-PDB Service test build: `0` errors, all `--no-restore --no-incremental`.
- Portable-PDB artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4a-final-remediation-20260821/67f3840c-6fc2-4ab0-8945-afa6b28154eb/coverage.cobertura.xml`.
- `BackendClient.ReportIntegrityAsync` lines `608` and `609`: `1` hit each. `BackendIdentityCoordinator.RefreshAsync`: `80.39%` line / `66.66%` branch. `IntegrityVerdictHandler` aggregate: `92.94%` line / `86.95%` branch. `ScheduleEscalation` line `318`: `4` hits.
- `git diff --check`: passed. Exact CODE+TEST budget: `379/400` (`142` tracked + `237` untracked runtime-test lines).
- Dependency/config/status audit: six expected tracked code/test paths, expected runtime test, and cumulative OpenSpec artifacts only; no project/dependency/config drift. `RunMonitorLoopAsync` has zero matches.
- Strict TDD: no new RED is fabricated. These final additions were triangulation against already-correct production behavior; the previously recorded failed final-rewrite RED remains unchanged.
- Cumulative task state remains **9/14**; top-level tasks remain unchecked.
- **Ready for fresh independent Unit 4A re-verification only.**

## Unit 4B1 Remediation Attempt — 2026-08-21

- Scope remained limited to `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs`; `tasks.md` remains 9/14 with `4.1`–`4.3` unchecked. Historical verification reports were not edited.
- Production changes now capture one generation in Start/loop/periodic/timezone callbacks, publish timer resources under the lifecycle lock, await the shared initial-completion task for concurrent Start callers, snapshot owned tasks under the same lock used by completion removal, and share the generation drain across Stop/Dispose.
- Timezone outbox work is admitted to the generation rather than fire-and-forget; old callbacks are rejected by generation identity and admission state. Cancellation now propagates through initial integrity work.
- The false-positive tick assertion was reshaped around a consumed-tick barrier and the lifecycle tests were compacted to keep the tracked CODE+TEST diff at `392/400` (`212+65` production, `80+26` tests).
- A portable-PDB Service-test build passed with `--no-restore`; AntiTamper focus passed `32/32` serially. The exact three-test B1 filter subsequently hung in `RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks` under fresh execution, so the full B1 matrix, coverage, and safety-net suite were not claimed.
- This remediation is **not ready for fresh independent B1 re-verification**: the deterministic actual-tick harness still has a runtime hang, required lifecycle/resource evidence is not complete, and no historical tests-first RED command can be reconstructed for this attempt. No task checkbox was changed.

## Unit 4B1 Tick-Harness Remediation — 2026-08-21

- Root cause: the TCS tick source coupled the loop's second `WaitForNextTick` continuation to a test-controlled completion source; the test waited on `secondConsumed` before releasing the blocked second backend operation, while the required third backend entry was not independently observable. Fresh execution hung in the tick test's `await secondConsumed.Task`/subsequent Stop-drain path rather than in initial Start, tick-1 consumption, or backend call-2 entry.
- Replaced the harness with an unbounded `Channel<int>` tick source. `TryWrite(1/2)` signals ticks, `ReadAsync` consumes them, separate consumed markers prove each real loop read, and cancellation completes the third read during Stop.
- The corrected sequence now proves: initial backend call 1; tick 1 consumed and backend call 2 blocked; tick 2 queued with consumed count still 1 and backend entries still 2; release; tick 2 consumed; backend call 3 entered/completed; Stop drain completes.
- Exact tick test was run three consecutive times: `1/1` each, exit 0. Exact B1 matrix: `3/3`; AntiTamper focus: `32/32`; combined Unit4A+B1 focus: `72/72`; full Service: `1,186/1,186`; full App.UI: `192/192`; portable-PDB coverage: `1,186/1,186`, Cobertura `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-remediation-20260821/1201ff6b-0322-452d-b818-efdd9a9c0d94/coverage.cobertura.xml`, aggregate `54.12%` line / `58.29%` branch, AntiTamperMonitor `82.43%` line / `73.61%` branch.
- Portable-PDB Domain, Service, and Service-test builds passed with `--no-restore`; existing NU1601/NU1701 warnings and duplicate xUnit ID notice remain. Final CODE+TEST diff remains `390/400`; reports and tasks were not changed.
- Remaining acceptance evidence gap: the complete requested Dispose/start-race matrix and all physical per-resource lifecycle variants are not independently represented by dedicated tests within the remaining 10-line budget. This slice is therefore **not yet ready for fresh independent B1 re-verification** despite the deterministic hang being fixed.

## Unit 4B1 Consolidation and Reentrancy Closure — 2026-08-21

- Removed unused generation identity and aggregate disposal state; retained individual CTS, gate, periodic-timer, and timezone-timer disposal counters. Reused `BeginDrainLocked`, removed the completed-task timezone wrapper in favor of one owned task path, consolidated cancellation filtering, and kept owner/caller cancellation distinct.
- Added an `AsyncLocal` callback-generation guard. Stop/Dispose invoked synchronously by a tamper callback now initiate the shared owned drain but do not synchronously await that drain from inside the callback; the callback returns, task removal runs under the lifecycle lock, and cleanup completes exactly once. No callback was moved to unowned fire-and-forget execution.
- Added `TamperCallback_CanRequestStopWithoutSelfDeadlock`, covering synchronous Stop plus Dispose from a timezone tamper callback, and `SynchronousAdmission_IsRemovedBeforeDrain`, proving no retained owned task after synchronous completion.
- Physical resource assertions now verify CTS, gate, tick-timer, and timezone-timer disposal counts independently for both old and new restart generations. Old timezone callback admission remains rejected after restart.
- Final exact B1 matrix: **5/5** (`RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks`, `Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer`, `ConcurrentStopAndDisposeShareLifecycleOwnership`, `TamperCallback_CanRequestStopWithoutSelfDeadlock`, `SynchronousAdmission_IsRemovedBeforeDrain`). AntiTamper focus **34/34**; combined Unit4A+B1 **74/74**; full Service **1,188/1,188**; full App.UI **192/192**.
- Portable-PDB Domain/Service/Service-test builds passed with `--no-restore`. Portable-PDB coverage passed **1,188/1,188**; artifact `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-final3-20260821/4377c1c2-f3e8-46b1-963c-21ef0bc1e9b4/coverage.cobertura.xml`; aggregate **54.27% line / 58.48% branch**; `AntiTamperMonitor` **99.01% line / 85.13% branch**.
- Final CODE+TEST diff: **389/400** (`203+75` production, `86+27` tests), `git diff --check` passed. Tasks remain 9/14 and reports remain unchanged.

## Unit 4B1 Final Evidence Closure — 2026-08-21

- Replaced the prior active Stop/Dispose assertion with a barrier-controlled initial Start versus Dispose race. Start owns a published generation while initialization is blocked; Dispose initiates the shared drain; release completes the race; the test asserts no exception, generation removal, and each CTS/gate/tick/timezone disposal count exactly once. The disposed-start test now also records `generationAllocations` and proves post-dispose Start rejects without allocation.
- Strengthened restart evidence in-place: captures generation-1 CTS, gate, tick timer, and timezone timer objects; proves generation-2 resources are distinct and have zero disposal counts while active; invokes old periodic and timezone admissions; proves generation-2 remains current/alive; stops generation 2 and verifies independent disposal counts; repeated Stop/Dispose leaves counts unchanged.
- Removed the superseded simple running-stop test and redundant aggregate/resource forwarding helpers. Final CODE+TEST diff is **373/400** (`204+75` production, `94+46` tests), below the 385 target.
- Final targeted B1 command executed the six AntiTamper lifecycle cases (the filter also matched three unrelated `StartAsync_WhenDisposed` cases): **9/9** total, with all six intended cases passing. AntiTamper focus **33/33**; combined Unit4A+B1 **73/73**; full Service **1,187/1,187**; full App.UI **192/192**.
- Portable-PDB Domain/Service/Service-test builds passed with `--no-restore`; portable-PDB coverage passed **1,187/1,187**. Artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-final4-20260821/7e11a64b-bd3d-41e6-875a-27c52e6ece00/coverage.cobertura.xml`; aggregate **54.27% line / 58.49% branch**; `AntiTamperMonitor` **99.01% line / 86.48% branch**.
- Out-of-scope content diff remains empty for Unit4A/C paths; `git diff --check` passed. Tasks remain 9/14 and reports remain unchanged.

## Unit 4B1 Final Two-Gap Closure — 2026-08-21

- Replaced the weak active Stop/Dispose test with a deterministic barrier-controlled Start-vs-Dispose race. Start owns a published generation while the initial report is blocked; Dispose races and begins the shared drain; release completes both; the test asserts no exception, generation removal, exactly-once CTS/gate/tick/timezone cleanup, repeated Dispose idempotency, and post-dispose Start rejection with unchanged `generationAllocations`.
- Replaced the weak restart evidence in-place. Generation 1 retains CTS, gate, tick timer, and timezone timer references; generation 2 has distinct resources and zero disposal counts while active; old periodic/timezone admissions are rejected; generation 2 remains current; both generations are independently verified after Stop, including repeated Stop/Dispose cardinality.
- Removed the superseded simple running-stop test, simple disposed-start test, and redundant helper/setup lines. Final CODE+TEST diff is **384/400** (`204+75` production, `53+52` tests), below the 385 target.
- Final intended B1 matrix: **8/8** (`ConcurrentStart_AwaitsOneSharedInitializationResult`, `ConcurrentStart_SharesCancellationResult`, `StopBeforeStart_AllowsLaterStart`, `RunMonitorLoop_UsesActualTickSourceAndDrainsQueuedTicks`, `Restart_DoesNotLetOldDrainDisposeNewTimezoneTimer`, `ConcurrentStopAndDisposeShareLifecycleOwnership`, `TamperCallback_CanRequestStopWithoutSelfDeadlock`, `SynchronousAdmission_IsRemovedBeforeDrain`). AntiTamper focus **32/32**; combined Unit4A+B1 **72/72**; full Service **1,186/1,186**; full App.UI **192/192**.
- Portable-PDB Domain/Service/Service-test builds passed with `--no-restore`. Portable-PDB coverage passed **1,186/1,186**; artifact `tests/ControlParental.Service.Tests/TestResults/coverage-unit4b1-final5-20260821/3d4ff9c9-2c09-48ef-9620-956f1722e21f/coverage.cobertura.xml`; aggregate **54.27% line / 58.49% branch**; `AntiTamperMonitor` **99.01% line / 86.48% branch**. `git diff --check` passed and Unit4A/C content diff remains empty.
- Tasks remain 9/14 and reports remain unchanged. **Unit4B1 is ready for fresh independent B1 re-verification.**

## Unit 4A — Backend Authority + Durable Persistence

- Work unit: Unit 4A only; Unit 4B lifecycle/race work remains deferred.
- Delivery strategy: feature-branch-chain; branch `feat/sdd7-4-runtime-integrity-enforcement`, base/target `3082559551ce4aa4363bd14a9e09dda0b1176f5f` / `feat/sdd7-3-foreground-realtime`.
- Review boundary: canonical backend identity, identity-scoped durable integrity issues, authoritative verdict application, exact non-definitive preservation, restart rehydration, and recovery without cross-identity mutation.
- Current CODE+TEST budget: `283` changed lines (`153` tracked additions+deletions plus `130` lines in the untracked runtime-path test), below the Unit 4A `400`-line limit.

### 4A RED / GREEN / REFACTOR Evidence

- Added `IntegrityRuntimePathTests` using the real `BackendIdentityCoordinator`, `BackendClient`, `AntiTamperMonitor`, `IntegrityVerdictHandler`, and physical `FileIssueStore`.
- Coverage includes same-device credential-generation refresh with stable `IdentityScope`, unrelated and other-device issue preservation, unknown/pending/absent/malformed/transient exact snapshot preservation, restart rehydration, trust recovery, and stale refresh timeout non-degradation.
- The focused tests compile and pass against the implementation: `2 passed, 0 failed, 0 skipped`.
- Focused integrity/enforcement regression: `53 passed, 0 failed, 0 skipped`.
- Affected Service build: `0 errors`; existing analyzer warning corpus remains.
- Full Service safety net: `1,174 passed, 0 failed, 0 skipped`; existing duplicate-ID discovery notice remains.
- Full App.UI safety net: `192 passed, 0 failed, 0 skipped`.
- `git diff --check` passed after the Unit 4A edits.
- Removed the unused `RunMonitorLoopAsync` lifecycle loop from `AntiTamperMonitor`; timer ownership remains in `StartAsync`/`StopAsync`, while Unit 4B race semantics remain deferred.
- No historical verification report was modified. Top-level tasks `4.1`–`4.3` remain unchecked because Unit 4A is an independently verifiable slice, not complete Unit 4.

### TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| Unit 4A canonical authority and durable persistence | FAILED — the final canonical test rewrite followed the production edits, so no valid pre-implementation behavioral failure was captured; this is reported rather than fabricated as RED. | PASS — focused runtime-path suite `2/2`; affected build `0 errors`. | PASS — real coordinator/backend/monitor/store path proves identity scope, exact preservation, restart recovery, and cross-identity isolation. | PASS — focused integrity/enforcement `53/53`, full Service `1,174/1,174`, full App.UI `192/192`. | PASS — removed the obsolete monitor loop and kept the Unit 4A scope below `400` changed lines; Unit 4B lifecycle ownership was not expanded. |

## Unit 4A Task State Confirmation

- `tasks.md`: Units 1–3 remain checked; Unit 4 tasks `4.1`–`4.3` and Phase 5 remain unchecked by design.
- Historical `verify-report-unit-4.md` and authoritative `verify-report-unit-4a.md` remain unchanged.

## Unit 4A Remaining Work

- Unit 4B: cancellation preservation, StopAsync/Dispose drain, timer single-flight, no post-stop work, stale/reordered suppression, concurrent verdict serialization, and escalation timing.
- After Unit 4B, run final Unit 4 verification and only then decide whether top-level 4.1–4.3 can be checked.

## Unit 4A Final Blocker-Closure Matrix

| Blocker | Exact passing runtime evidence | Physical assertion / source audit | Status |
|---|---|---|---|
| 1. Same-device refresh | `IntegrityRuntimePathTests.CanonicalAuthority_PreservesExactStateAcrossNonDefinitiveResultsAndRecoversAfterRefresh` | Captures `preRefreshKey`; refreshes the same `device-a` through `MemoryIdentityStore` + real `BackendIdentityCoordinator.InitializeAsync`; asserts refreshed bearer token, `preRefreshKey` equality, `IssueKey.SessionId == 0`, `IdentityScope == device-a`, and no active `device-a` record after trust recovery. | ✅ |
| 2. Production identity chain | Same test; all runtime-path tests construct `new BackendClient(client, url, coordinator)` and pass that same `coordinator` to `AntiTamperMonitor` | HTTP handler captures `Authorization` (`token-a`, then `token-a-refreshed`); physical persisted key carries `device-a`; no `IDeviceAuthenticator` or obsolete constructor is referenced by `IntegrityRuntimePathTests.cs`. Production `Program` resolves the same `IBackendIdentityCoordinator` for backend and monitor. | ✅ |
| 3. Duplicate + unrelated | `CanonicalAuthority_PreservesExactStateAcrossNonDefinitiveResultsAndRecoversAfterRefresh` | Five revoked reports leave one semantic active key; a further revoked report is followed by `Assert.Single` on the same key. Exact JSON snapshot of all non-`device-a` records is compared before/after recovery, preserving unrelated `HookTimeout` and active `device-b` integrity entries. | ✅ |
| 4. Complete non-degradation | Theory cases: `CanonicalAuthority_NonDefinitiveCase_PreservesExactPhysicalSnapshot(caseName: "stale" is separate below)` for `unknown`, `pending`, `absent`, `malformed`, `transport`; plus `CanonicalAuthority_StaleIdentityGenerationPreservesExactPhysicalSnapshot`, `CanonicalAuthority_RefreshTimeoutPreservesExactPhysicalSnapshot`, and `CanonicalAuthority_CallerCancellationPreservesExactPhysicalSnapshot` | Every case serializes the complete physical `FileIssueStore` snapshot before/after. Stale changes the real coordinator generation via `InvalidateAsync` while HTTP is in flight; timeout uses expired credentials and bounded refresh timeout; cancellation occurs in production durable write after parser/policy; unknown/pending/absent/malformed/503 all use production `BackendClient.ReportIntegrityAsync` parsing/policy path. | ✅ |
| 5. Slice purity | Static diff audit and focused/full regressions | `RunMonitorLoopAsync` has zero matches in the worktree. `Dispose` matches parent timer semantics: sets disposed/running state, disposes both timers, nulls both fields. No Unit 4B cancellation loop, drain, single-flight, stale-ordering, or race implementation/test was added; the only cancellation test is the required Unit 4A non-degradation case. | ✅ |
| 6. Canonical key implementation | `CanonicalAuthority_PreservesExactStateAcrossNonDefinitiveResultsAndRecoversAfterRefresh` | Actual durable key is `new IssueKey(0, BinaryIntegrityFailure, "integrity/binary", identity?.DeviceId)`: credential generation is excluded; semantic cause is fixed; device scope is explicit. Test asserts same-device key stability and `Assert.NotEqual` for `device-b`. No random/hash/global sentinel participates in the key. | ✅ |
| 7. Coverage | Portable-PDB artifact below; final full Service and focused suites green | Runtime hits cover coordinator initialize/get/refresh/invalidate, monitor stale/timeout/cancellation/verdict branches, parser/report failure paths, active add and resolve, duplicate, unrelated preservation, and restart recreation. Changed-method coverage: `PerformBinaryIntegrityCheckAsync` 96.00%/75.00%; `ProcessVerdictReactionAsync` 71.05%/57.14%; `BackendIdentityCoordinator` 95.12%/80.00%; `GetDefinitiveSessionAsync` 90.90%/75.00%; `RefreshAsync` 80.39%/66.66%; `ReportIntegrityAsync` 94.73%/100%; `ResolveIssueAsync` 100%/100%. | ✅ |

### Final Unit 4A Execution Evidence

- Domain build: passed, `0` errors, `--no-restore --no-incremental`.
- Service build: passed, `0` errors, `--no-restore --no-incremental`.
- Service test build with portable PDB: passed, `0` errors, `--no-restore --no-incremental`.
- Focused authorized Unit 4A filter: `67/67` passed (`IntegrityCheckerTests`, `AntiTamperMonitorTests`, `IntegrityVerdictHandlerTests`, `IntegrityRuntimePathTests`).
- Exact real-path filter: `9/9` passed.
- Full Service: `1,181/1,181` passed; existing duplicate-ID discovery notice only.
- Full App.UI: `192/192` passed.
- Portable-PDB coverage: `1,181/1,181` passed; artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4a-final-20260821/acd6f80c-de9b-4a65-81fb-2900bc753052/coverage.cobertura.xml`.
- `git diff --check`: passed.
- Exact CODE+TEST budget: `368/400` (`143` tracked changed lines + `225` lines in untracked `IntegrityRuntimePathTests.cs`).
- Dependency/config/status audit: no changed `*.csproj`, props, targets, lockfiles, `global.json`, NuGet/config, or generated tracked cache; only the six expected tracked code/test paths, the expected runtime-path test, and cumulative OpenSpec artifacts are present.
- Historical `verify-report-unit-4.md` and `verify-report-unit-4a.md` remain unchanged; top-level tasks remain `9/14`, with `4.1`–`4.3` and `5.1`–`5.2` unchecked.

### Strict TDD Truth

- The previously recorded missing final-rewrite RED remains failed chronology. No new RED is fabricated here.
- The newly added stale, timeout, cancellation, duplicate, unrelated, exact-snapshot, and key-stability checks were added after the prior implementation state and are reported as triangulation/green evidence, not as historical RED.

### Final Unit 4A Status

- All seven requested blocker items now have concrete source audit plus passing runtime evidence.
- Unit 4B remains deferred and is not claimed complete.
- **Ready for fresh independent Unit 4A re-verification.**

## Unit 4 Remediation — Runtime Path and Identity

- Added a real backend/parser/handler/file-store identity-rotation test. It proves a revoked issue for `device-a` remains active after three trust verdicts from rotated `device-b`; no `device-b` issue is created.
- Fresh affected Service build: `0 errors` under `--no-restore --no-incremental`.
- Focused Unit 4 command passed: `55/55` (`AntiTamperMonitorTests`, `IntegrityVerdictHandlerTests`, and `IntegrityRuntimePathTests`).
- Full Service regression passed: `1,176/1,176`; existing duplicate xUnit test-ID notice remains. Full App.UI regression passed: `192/192`.
- Portable-PDB Service coverage passed and produced `tests/ControlParental.Service.Tests/TestResults/coverage-unit4-remediation-20260820/33999905-b405-4efd-92ee-dc915b822336/coverage.cobertura.xml`. Changed-class rates include `AntiTamperMonitor` `83.45%/86.11%`, `IntegrityVerdictHandler` `85.79%/80.43%`, `BackendClient` `91.22%/63.88%`, and `EnforcementLevelMonitor` `94.76%/86.95%` line/branch.
- Exact CODE+TEST accounting is `384/400`: `273` tracked changed lines plus `111` lines in the untracked runtime-path test; OpenSpec artifacts excluded. Only 16 lines remain.

### Unit 4 TDD / Verification Status

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4.1 — Runtime-path and identity acceptance | No new RED claimed for identity rotation because the existing identity-scoped implementation was already green; no failure fabricated. | PASS — real revoked/recovery/restart and cross-device test. | PARTIAL — malformed/absent/transient cancellation and race matrix remain incomplete. | PASS — focused `55/55`, Service `1,176/1,176`, App.UI `192/192`. | PARTIAL — no safe room for the remaining required tests without consolidation. |
| 4.2 — Runtime enforcement/lifecycle implementation | Historical Unit 4 RED remains valid. | PARTIAL — production shadow-mode, owned loop cancellation/drain, synchronized handler, and identity-scoped key changes are present. | FAIL — escalation timing and stale/reordered completion authority remain unclosed. | PASS — product/test builds `0 errors`; coverage collected. | BLOCKED by the hard budget. |
| 4.3 — Refactor / verify | N/A. | NOT READY — authoritative nine-scenario gate is not closed. | FAIL — independent verification requirements remain unmet. | PASS — fresh regressions and portable-PDB coverage. | NOT COMPLETE. |

### Remaining Blocker

- The remaining deterministic cancellation, stop/drain, overlap, and reordered-completion proofs cannot honestly be added within the remaining `16` CODE+TEST lines. Unit 4 tasks `4.1`–`4.3` were reverted to unchecked in `tasks.md`; no size exception was used.

## Unit 4 Consolidation / Lifecycle Proof Pass

- Removed the two mock-heavy `AntiTamperMonitorTests` authority tests because they were strictly superseded by the real `IntegrityRuntimePathTests` backend/parser/policy/store path; unrelated baseline tests were retained.
- Added one compact production-path cancellation/concurrency test using TCS barriers. It records genuine RED chronology: the first run initially failed because xUnit exact exception matching rejected the derived `TaskCanceledException`; the assertion was corrected to `ThrowsAnyAsync<OperationCanceledException>`, then the initial-call cardinality expectation was corrected from `2` to `3` (startup plus two controlled runs). No production defect was fabricated from either test-harness mismatch.
- Added an `integrityGate` semaphore around report cycles and an internal test seam, proving concurrent report calls serialize and cancellation exits before durable mutation. This is a single-flight production owner, not a test-only lock.
- Consolidated CODE+TEST budget is now `373/400`: tracked diff `229` changed lines plus untracked runtime-path test `144` lines. Remaining headroom is `27`.
- Fresh focused Unit 4 suite: `54/54`; full Service: `1,175/1,175`; full App.UI: `192/192`; affected product/test builds: `0 errors`; portable-PDB Service coverage artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4-final-20260820/655add3f-5d4a-4249-8781-bf5e8b278f55/coverage.cobertura.xml`; `git diff --check` passes.
- Remaining complete proofs still require owned StopAsync/Dispose drain while a report is blocked, stale/reordered completion authority, concurrent final-state/recovery ordering, and escalation timing. They cannot fit honestly in `27` lines without making the tests unreadable or weakening assertions.

### Autonomous Re-slice Proposal (No Branch or Strategy Changes)

| Slice | Ownership | Estimated CODE+TEST | Boundary |
|---|---|---:|---|
| Unit 4A — Authority + persistence | `AntiTamperMonitor.cs`, `IntegrityVerdictHandler.cs`, `IEnforcementLevelMonitor.cs`, `Program.cs`, `IntegrityRuntimePathTests.cs`, `IntegrityVerdictHandlerTests.cs` | Existing `373` retained; consolidate to `~300`, then add `~75` | Real backend authority, identity key, unknown/absent/malformed/transient preservation, restart, exact recovery and unrelated-issue isolation |
| Unit 4B — Lifecycle + races | `AntiTamperMonitor.cs`, `IntegrityRuntimePathTests.cs`, `AntiTamperMonitorTests.cs`, focused lifecycle helpers only | `~280` incremental after 4A consolidation | TCS-controlled cancellation, owned stop/dispose drain, tick single-flight, stale/reordered completion, concurrent verdict serialization, escalation timing |

This is a proposal only. No branches were created and no delivery strategy was changed. Unit 4 tasks remain unchecked because the nine-scenario gate is not complete.

## Unit 4A — Backend Authority + Durable Persistence

- Slice boundary: authority and persistence only. Unit 4B lifecycle/race work was surgically removed from the current diff: the semaphore gate, internal lifecycle test seam, cancellation-after-start test, owned cancellation loop, and stop/drain implementation were reverted to parent timer/Stop/Dispose behavior. Unit 4B is explicitly deferred.
- Retained production changes: active-by-default `IntegrityVerdictHandler`, synchronized policy state, authoritative recovery signal, async durable add/resolve seams, canonical identity injection, identity-scoped stable issue key, local evidence report-only gating, and real backend verdict gating.
- Retained tests: real `BackendClient` HTTP parsing through real `IntegrityVerdictHandler` and real `FileIssueStore`/`EnforcementLevelMonitor`; identity rotation; restart rehydration and same-identity trust recovery; duplicate cardinality; unknown, pending, absent, malformed, and HTTP transport failure preservation.

### Unit 4A TDD / Verification Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 4A.1 — Authority and durable persistence | Historical Unit 4 RED remains valid: production default shadow mode and mock-only authority path were the reported failures. No new RED fabricated where the retained production path was already green. | PASS — real revoked backend response creates one identity-scoped issue; repeated verdicts remain one active record; local evidence is not an authority. | PASS — real parser/policy/store path covers revoked, trust, unknown, pending, absent, malformed, HTTP 503, restart, same-identity recovery, and cross-device isolation. | PASS — focused `60/60`; full Service `1,174/1,174`; full App.UI `192/192`. | PASS — removed superseded mock authority tests and all Unit 4B lifecycle/race partials. |
| 4A.2 — Identity and recovery semantics | No separate RED claimed for identity because the retained implementation already satisfied the new real path; no failure fabricated. | PASS — canonical device/session identity flows into `IssueKey`; device rotation cannot clear the prior device issue. | PASS — unrelated issue preservation and physical durable cardinality/recovery are asserted across recreation. | PASS — affected product/test builds report `0 errors`; portable-PDB coverage collected. | PASS — no client baseline API, retry owner, or deferred lifecycle owner changed. |
| 4A.3 — Slice verification | N/A — verification slice. | PASS — all Unit 4A focused acceptance tests pass. | PASS — Unit 4B is explicitly excluded and restored to parent behavior. | PASS — `git diff --check` passes; dependency/config drift audit remains clean. | PASS — exact slice budget is `286/400`. |

### Unit 4A Scenario Matrix

| Acceptance | Evidence | Result |
|---|---|---|
| Active backend authority | Production `IntegrityVerdictHandler` defaults active; DI does not depend on an activation toggle | PASS |
| Real backend → parser → policy → store | `BackendParsePolicyDurableIssueAndRecoverySurviveRecreation` | PASS |
| Revoked stable idempotent issue | Four real revoked reports; physical `Assert.Single` active issue | PASS |
| Same-identity recovery | Recreated enforcement and three real trust reports resolve exact key | PASS |
| Other identity isolation | `IdentityRotation_DoesNotResolvePriorDeviceIssue` | PASS |
| Unknown/pending/absent/malformed/transport preservation | Explicit payload matrix plus HTTP 503; active issue remains physically present | PASS |
| Restart rehydration | File-backed enforcement recreated before trust recovery | PASS |
| Local evidence report-only | Monitor discards local failure reaction and gates durable mutation on successful exact backend verdict | PASS (runtime + static path) |
| No baseline/retry ownership change | No baseline API; BackendClient remains transport retry owner; lifecycle remains parent behavior | PASS |

### Unit 4A Budget and Quality

- Exact CODE+TEST budget: `286/400` (`165` tracked changed lines + `121` lines in untracked `IntegrityRuntimePathTests.cs`; OpenSpec artifacts excluded).
- Coverage artifact: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4a-20260820/e01f5b43-ae14-496e-bd12-9903535c703e/coverage.cobertura.xml`.
- Changed-class coverage: `AntiTamperMonitor` `83.10%` line / `87.50%` branch; `IntegrityVerdictHandler` `87.57%` / `82.60%`; `BackendClient` `91.22%` / `63.88%`; `EnforcementLevelMonitor` `94.76%` / `86.95%`.
- Warnings: existing NU1601/NU1701, analyzer/style warnings, and the known duplicate xUnit test ID. No new dependency/configuration drift.
- `tasks.md`: top-level `4.1`, `4.2`, and `4.3` remain unchecked; cumulative state remains `9/14`. Unit 4A slice is complete, but top-level Unit 4 and independent full-Unit verification are not complete.

## Unit 4 Remediation — Runtime Path and Lifecycle Ownership

- Scope: Unit 4 remediation only; the historical `verify-report-unit-4.md` remains unchanged and Unit 5 remains unchecked.
- Corrected `AntiTamperMonitor.StopAsync` to await owned monitor-loop completion without returning a `Task` from an `async Task` method.
- Added `IntegrityRuntimePathTests.BackendParsePolicyDurableIssueAndRecoverySurviveRecreation`, exercising the real `BackendClient` response parser, real `IntegrityVerdictHandler`, real `AntiTamperMonitor` start/stop path, file-backed issue persistence, restart rehydration, revoked enforcement, and trust recovery.
- Fixed test resource ownership by disposing the real `HttpClient`; the mock handler is intentionally not wrapped in `using` because `Mock<T>` is not disposable.
- Fresh affected Service build: `0 errors` under `--no-restore --no-incremental`; existing warning corpus remains.
- Fresh focused runtime command:

  ```text
  dotnet test "tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~AntiTamperMonitorTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~IntegrityRuntimePathTests"
  ```

  Result: `54 passed, 0 failed, 0 skipped`.
- The Unit 4 authoritative verification report is still not closed: broader evidence for all nine runtime-integrity scenarios, device rotation/isolation, cancellation preservation, timer single-flight/drain, and duplicate/reordered/concurrent verdicts remains outstanding.

## Unit 4 — Runtime Integrity / Enforcement

- Work unit: Unit 4, tasks 4.1–4.3 only.
- Delivery strategy: feature-branch-chain; PR 4 targets `feat/sdd7-3-foreground-realtime` at `3082559551ce4aa4363bd14a9e09dda0b1176f5f`.
- Review boundary: authenticated integrity evidence/verdict handling and durable enforcement authority. Unit 5 is untouched.
- Changed CODE+TEST lines against the PR4 parent: `146` (`19` domain additions, `43` production additions/deletions in Service, `71` test additions, plus existing Unit 4 test changes); below the hard `400` line limit. OpenSpec artifacts are excluded.
- CodeGraph was attempted for the exact linked worktree and reported no index; focused direct filesystem fallback was used. No `.codegraph` artifact was created.

### TDD Cycle Evidence

| Task | Requirement/scenario | RED | GREEN | REFACTOR | Safety net |
|---|---|---|---|---|---|
| 4.1 | Local evidence is report-only; unknown backend verdict is non-degrading; definitive revoked verdict can enforce; trust threshold exposes recovery | PASS — tests were compiled and run against pre-gate production. Command: `dotnet build "tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj" --no-restore --no-incremental --configuration Debug --verbosity quiet` followed by focused `dotnet test ... --no-restore --no-build ... --filter "FullyQualifiedName~AntiTamperMonitorTests.LocalEvidence_DoesNotChangeEnforcement_WhenBackendVerdictIsNonAuthoritative"`; timestamp `2026-08-20 20:34:51` local tool output; exit `1`; assertion: unexpected `AddIssueAsync` for stable key `integrity/binary`, evidence `untrusted`. | PASS — focused integrity/enforcement tests pass after the backend-verdict gate and recovery signal were implemented. | PASS — tests assert real `AntiTamperMonitor.StartAsync`, real handler threshold behavior, stable issue identity, and no local/unknown mutation. | PASS — focused suite `33/33`; affected Service build `0 errors` (existing warning corpus). |
| 4.2 | Only definitive backend `trust|revoked` may call durable enforcement mutation; malformed/absent/failure/unknown results remain observable and non-degrading | PASS — same behavioral RED exercised the real monitor and failed before the definitive-verdict gate. | PASS — `AntiTamperMonitor` ignores local reaction and gates durable add/resolve on successful definitive verdicts; durable async seam is available on `IEnforcementLevelMonitor`; recovery is marked by `IsAuthoritativeRecovery`. | PASS — stable `IssueKey(-1, BinaryIntegrityFailure, "integrity/binary")`; no expected-hash/client baseline API or adapter authority added. | PASS — backend identity/retry tests remain in the full Service regression; no transport implementation was changed. |
| 4.3 | Preserve cancellation/retry ownership, identity, stable issue key, restart-safe durable issue semantics and race-safe serialized store behavior | N/A — verification/refactor task; no additional production RED fabricated. | PASS — focused suite and durable issue monitor safety tests pass with async enforcement calls. | PASS — no fire-and-forget verdict mutation introduced; existing backend retry/cancellation owner remains `BackendClient`; `FileIssueStore` remains the serialized restart-safe owner. | PASS — full Service/App.UI regressions, no-restore builds, coverage, and diff audits recorded below. |

### Unit 4 Verification

- Focused command: `dotnet test "tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~IntegrityCheckerTests|FullyQualifiedName~IntegrityVerdictHandlerTests|FullyQualifiedName~AntiTamperMonitorTests.LocalEvidence_DoesNotChangeEnforcement_WhenBackendVerdictIsNonAuthoritative|FullyQualifiedName~AntiTamperMonitorTests.BackendRevokedVerdict_IsTheOnlyIntegrityEnforcementAuthority"`; result `33 passed, 0 failed, 0 skipped`.
- Unit 4 production behavior is limited to Service/Domain integrity enforcement seams. `IntegrityChecker` and `BackendClient` required no source change because their existing evidence hash, authenticated transport, bounded retry, cancellation, and malformed-verdict normalization already satisfy the contract.
- Remaining tasks: Phase 5 tasks 5.1–5.2 only; Unit 5 remains unchecked.

### Unit 4 Safety-Net Results

- Service product build: `dotnet build "src\\ControlParental.Service\\ControlParental.Service.csproj" --no-restore --configuration Debug --verbosity minimal`; started `2026-08-20 20:37:07`; exit `0`, `0 errors` (existing NU1601 warning).
- Service test build: `dotnet build "tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj" --no-restore --configuration Debug --verbosity minimal`; exit `0`, `0 errors` (existing NU1601/NU1701 warnings).
- Full Service regression: `dotnet test "tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal`; exit `0`, `1,174 passed, 0 failed, 0 skipped`; existing duplicate xUnit test-ID notice retained.
- App.UI restore was initially blocked by missing ignored `tests\\ControlParental.App.UI.Tests\\obj\\project.assets.json`. Minimum scoped restore only: `dotnet restore "tests\\ControlParental.App.UI.Tests\\ControlParental.App.UI.Tests.csproj"`; no tracked dependency/config drift. Subsequent commands used `--no-restore`.
- App.UI product/test builds and full regression passed under `--no-restore`; full App.UI regression: `192 passed, 0 failed, 0 skipped` at `2026-08-20 20:37:46`.
- Portable-PDB coverage: `dotnet test "tests\\ControlParental.Service.Tests\\ControlParental.Service.Tests.csproj" --no-restore --no-build --configuration Debug --collect:"XPlat Code Coverage" --results-directory "tests\\ControlParental.Service.Tests\\TestResults\\coverage-unit4-final-20260820" --verbosity minimal`; exit `0`, `1,174 passed`; artifact `tests/ControlParental.Service.Tests/TestResults/coverage-unit4-final-20260820/35a80080-5394-4d33-8b21-2047340d81e3/coverage.cobertura.xml`. Rates: `AntiTamperMonitor` 83.97% line / 81.81% branch; `IntegrityVerdictHandler` 90.79% / 84.78%; `EnforcementLevelMonitor` 93.60% / 84.78%; `BackendClient` 91.22% / 63.88%; `IntegrityChecker` 100% class line rate.
- `git diff --check`: exit `0` at `2026-08-20 20:39:22`; dependency/config audit: no `.csproj`, props, targets, lockfile, global.json, or NuGet config changes. Ignored build/restore assets remain untracked/ignored only.

### Final Unit 4 Task State

- `tasks.md`: `1.1`–`1.3`, `2.1`–`2.3`, `3.1`–`3.3`, and `4.1`–`4.3` are checked; `5.1`–`5.2` remain unchecked.
- Cumulative status: `12/14` tasks complete. No Unit 5 source/test/artifact implementation was performed, and no verify report was created.
- PR4 boundary: `feat/sdd7-4-runtime-integrity-enforcement` → parent `feat/sdd7-3-foreground-realtime` at `3082559551ce4aa4363bd14a9e09dda0b1176f5f`; this slice contains runtime integrity/enforcement only.

## Unit 3 Authoritative Remediation — Direct Foreground Authority and Dual-Channel Disposal

- Scope remained Unit 3 tasks 3.1–3.3 only. All three historical verification reports remain unchanged; Units 4–5 remain unchecked and the cumulative task state remains 9/14.
- Public `ConnectAsync` now captures the current lifecycle epoch before entering the lifecycle gate. `ConnectCoreAsync` rejects a background request before generation/transport work, validates foreground plus epoch before each subscribe, and validates foreground again before readiness. Event-driven foreground epoch capture and queued stale rejection are preserved.
- The direct-connect test now explicitly covers both foreground and background. Background direct connect settles disconnected with zero policy/grant subscribe calls; foreground direct connect remains connected.
- The started late-completion test is a two-case theory covering policy and grant channels. Each case starts a selected non-cancellable subscription, overlaps background with concurrent dispose, repeats dispose, releases late completion, awaits connect and `LifecycleTask`, and proves logical state, both transports, and both handler sets are clean.
- The queued epoch test now asserts physical state and zero new subscriptions before teardown `Dispose()`; teardown occurs only afterward.
- Fresh behavioral RED was run for the new direct-background test before production changes and failed honestly: the current implementation produced `IsConnected=True`, policy calls `1`, grant calls `1` instead of `(False,0,0)`. The prior stale-DLL chronology warning remains unchanged; no RED was fabricated.
- Focused Realtime suite: `14/14` passed. Full App.UI regression: `192/192` passed. Full Service regression: `1,171/1,171` passed with the existing duplicate-ID notice. App.UI product/test and Service product builds passed with `--no-restore`; portable-PDB coverage passed.
- Coverage artifact: `tests/ControlParental.App.UI.Tests/TestResults/coverage-unit3-authoritative-remediation-20260820/260b5408-22db-409e-8997-6f49cf791c2d/coverage.cobertura.xml`.
- Coverage: `RealtimeSubscriber` line-rate `95.15%`, branch-rate `80.00%`; `ConnectCoreAsync` generated state machine line-rate `100%`, branch-rate `73.33%`; `RunLifecycleCoreAsync` generated state machine `100%/100%`. Direct background guard, grant-side started stale completion, concurrent background/dispose, and repeated dispose all have runtime hits. Branches are not claimed 100% where defensive outcomes remain unexercised.
- Final CODE+TEST diff is exactly `400/400`: production `181 additions + 74 deletions = 255`; tests `126 additions + 19 deletions = 145`. No dependency/config/project drift, Service production diff, Unit 4+ leakage, or external/live claim exists. `git diff --check` passed.
- Existing warning corpus remains, including style warnings from deliberate line consolidation needed to stay within the hard budget. No size exception was used.

## Unit 3 Remediation — Epoch Queue and Physical Cleanup Evidence

- Scope remained Unit 3 tasks 3.1–3.3 only. Historical `verify-report-unit-3.md` and `verify-report-unit-3-final.md` were not modified.
- The existing implementation already records the foreground epoch synchronously in `OnEnteredForeground`, advances it synchronously in background/dispose intent, and validates a queued epoch before allocating a generation or subscribing. The remediation preserved that design and tightened the focused test boundary rather than adding a second lifecycle owner.
- Fresh mandatory RED attempt (before the final production-only formatting change):
  - Command: `dotnet test "tests\ControlParental.App.UI.Tests\ControlParental.App.UI.Tests.csproj" --no-restore --no-build --configuration Debug --verbosity minimal --filter "FullyQualifiedName~RealtimeSubscriberTests.QueuedForegroundConnectBeforeBackground_DoesNotStartAfterBackground"`
  - Timestamp: `2026-08-20` session; exit was non-zero because the newly reshaped test was not present in the stale test DLL (`No test matches the given testcase filter`), not a behavioral assertion failure.
  - The required behavioral chronology was then compiled and executed with `--no-restore`; it passed against the current implementation. No behavioral RED failure is claimed or fabricated.
- The deterministic lifecycle test holds the gate with a blocked policy subscribe, queues foreground epoch E, advances background before the queued operation can acquire the gate, releases the blocker, and asserts no new grant subscription plus physical disconnection.
- Failure coverage now asserts physical subscription state and handler count `(0,0)` after policy failure, queued policy failure, and grant failure; the successful retry leaves exactly one handler per channel. Late started policy completion still asserts physical unsubscribe, and restart callbacks retain old-generation suppression/current-generation exactly-once assertions.
- Focused Realtime: `9/9` passed. Full App.UI: `187/187` passed. Full Service: `1,171/1,171` passed with the existing duplicate-ID discovery notice. App.UI product/test and Service product builds passed with `0` errors under `--no-restore`; portable-PDB coverage passed and produced `tests/ControlParental.App.UI.Tests/TestResults/coverage-unit3-remediation-final-20260820/92aeb2f0-f246-4837-82ef-5114e32c0f22/coverage.cobertura.xml`.
- Coverage: `RealtimeSubscriber` line-rate `94.04%`, branch-rate `77.58%`; generated `ConnectCoreAsync` state-machine class line-rate `96.15%`, branch-rate `86.36%`; `RunLifecycleCoreAsync` state-machine class `100%/100%`. The stale-success cleanup and queued-epoch observable behavior are executed by the focused suite.
- Final `git diff --check` passed. Exact CODE+TEST diff is `400` changed lines (`287 additions + 113 deletions`): production `185 + 72 = 257`, test `102 + 41 = 143`. This is at, not above, the hard limit; no further additions are safe.
- No Service production, Unit 4+, dependency, project, configuration, external, or live-system changes were made. Units 4–5 remain unchecked.

## Unit 2 Remediation Evidence — Durable Write Ordering and Replacement Lifecycle

- Added strict-TDD coverage for failed durable intent writes, same-operation retry, terminal `Denied` replacement after restart, accepted terminality, and expired persisted intents.
- `WnsRegistrationCoordinator.RegisterAsync` now assigns the in-memory `latest` intent only after the candidate intent has been durably written. `SendLatestAsync` retains its existing final-status persistence behavior.
- Corrected the retry write-count assertion to account for the failed candidate write plus the successful candidate and final-status writes (`3` total).
- Focused WNS command after rebuilding passed: `28 passed, 0 failed, 0 skipped`.
- Fresh full Service safety net passed: `1,171 passed, 0 failed, 0 skipped`; xUnit reported one existing duplicate test-ID skip notice.
- Fresh full App.UI safety net passed: `186 passed, 0 failed, 0 skipped`.
- Fresh full Service coverage artifact:
  `tests/ControlParental.Service.Tests/TestResults/coverage-unit2-final-20260820/6082adff-e174-48c3-90bf-65e922f1b06f/coverage.cobertura.xml`.
  `WnsRegistrationCoordinator` line-rate `100%`, branch-rate `76%`; `RegisterAsync`, `ReconcileAsync`, and `SendLatestAsync` each line/branch `100%`.
- Final diff check passed. Current tracked code/test diff is `143 additions + 18 deletions = 161` changed lines, excluding untracked OpenSpec artifacts and remaining below the Unit 2 budget.

### Updated TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 2.1 — Durable write ordering | PASS — retry coverage required the production ordering fix to prevent phantom in-memory state after write failure. | PASS — focused WNS suite `28/28` after candidate persistence became durable-before-publish. | PASS — failed first write leaves no persisted intent, same operation retries, and successful registration persists final state. | PASS — full Service `1,171/1,171`, App.UI `186/186`, affected build `0 errors`, and final Cobertura artifact. | PASS — only `RegisterAsync` publication ordering changed; `SendLatestAsync` behavior was preserved. |
| 2.2 — Terminal replacement lifecycle | PASS — restart/revocation and replacement tests establish the terminal `Denied` boundary. | PASS — lifecycle tests pass in focused `28/28` suite. | PASS — `Denied` is quarantined across restart while a new operation ID remains admissible; `Accepted` remains terminal. | PASS — full Service/App.UI safety nets and coordinator coverage remain green. | PASS — no new transport or retry owner introduced. |

## Updated Task State Confirmation

- `tasks.md`: `1.1`–`1.3` and `2.1`–`2.3` are checked.
- Historical Unit 2 remediation confirmation: Phase 3, Phase 4, and Phase 5 were unchecked before Unit 3 execution.

## Unit 3 Final Remediation Evidence

- Scope: Unit 3 only; `RealtimeSubscriber.cs` and `RealtimeSubscriberTests.cs`. No Service, REST, scheduler, identity, enforcement, dependency, or Unit 4+ changes.
- Final tracked CODE+TEST diff: `389` changed lines (`196 additions + 76 deletions` production; `85 additions + 32 deletions` tests), below the 400-line Unit 3 budget. OpenSpec artifacts are excluded.
- Focused Realtime suite: `8/8` passed after the final merged assertions.
- Full App.UI regression: `186/186` passed, exit `0`.
- Full Service regression: `1,171/1,171` passed, exit `0`; xUnit emitted the existing duplicate-ID skip notice.
- Affected builds: App.UI product, App.UI tests, and Service product all passed with `0` errors under `--no-restore`; existing package/analyzer warnings remain.
- Coverage: `tests/ControlParental.App.UI.Tests/TestResults/coverage-unit3-remediation-20260820/a14328e1-4ec4-4caa-be83-4c695ae0cbdf/coverage.cobertura.xml`; `RealtimeSubscriber` line-rate `95.23%`, branch-rate `79.62%`.
- `git diff --check` passed. The historical `verify-report-unit-3.md` remains unchanged.

### Final TDD Cycle Evidence

| Task/Cycle | RED | GREEN | TRIANGULATE | SAFETY NET | REFACTOR |
|---|---|---|---|---|---|
| 3.1 — Lifecycle/barrier acceptance tests | PASS — tests were authored before the remediation; initial no-restore execution was infrastructure-blocked by missing linked-worktree assets, not presented as behavioral RED. | PASS — focused Realtime suite `8/8`. | PASS — stale completion, background isolation, failed policy/grant subscribe, restart callback suppression, malformed payloads, and duplicate disconnect paths are covered. | PASS — full App.UI `186/186`; full Service `1,171/1,171`. | PASS — redundant scenarios were consolidated while retaining meaningful idempotency and malformed-input assertions; final budget `389/400`. |
| 3.2 — Generation-safe subscriber | PASS — behavior tests preceded production edits. | PASS — App.UI product/test builds and focused suite passed. | PASS — underlying non-cancellable subscription tasks are observed before stale cleanup; generation invalidation prevents late activation and stale publication. | PASS — full App.UI and Service regressions passed. | PASS — one lifecycle gate, generation ownership, no new authority/retry path, and queued lifecycle exceptions remain contained. |
| 3.3 — Refactor / verify | N/A — verification task; no RED fabricated. | PASS — focused/full/build/coverage commands exited `0`. | PASS — coverage artifact and diff audit retained; Realtime line/branch rate `95.23%/79.62%`. | PASS — historical report preserved unchanged. | PASS — only assigned Unit 3 files changed; Units 4–5 remain unchecked. |

## Final Task State Confirmation

- `tasks.md`: Units 1–3 tasks are checked; Units 4 and 5 remain unchecked.
