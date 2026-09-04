# Verification Report

**Change**: `remote-signal-integrity` — task 4.2a / C1B1 nominal durable owner  
**Branch / HEAD**: `feat/sdd7-4c2c1b-owner-reconciliation` / `f37df4a1036a2969b852f929f9db632c7b424206`  
**Mode**: Strict TDD  
**Verdict**: **FAIL**

## Executive Summary

The resliced C1B1 candidate builds and all requested fresh gates pass: DurableOwner `8/8`, AntiTamper `86/86`, exact-exclusion safety `1,287/1,287`, and portable coverage `98.97%` line / `84.33%` branch. Scope, budget, hygiene, and the supplied Strict-TDD hashes are also correct. The candidate nevertheless fails the current nominal boundary: live reactions use the unkeyed `AddIssueAsync` overload, the first revoked observation is not persisted (the test explicitly accepts `0/Normal`), and required actual three-trust/revoked-reset and non-Degraded-latch behavior have no runtime proof.

The two earlier broad C1B FAIL reports were read in full and are retained only as superseded provenance. Their C1B2 cancellation-ignoring save/effect/fault and durable failure-retry findings are deferred by task 4.2b and are not blockers in this C1B1 verdict.

## Completeness

| Metric | Result |
|---|---:|
| Tasks total | 17 |
| Tasks complete | 11 |
| Tasks incomplete | 6 |
| Task 4.2a | Checked |
| Task 4.2b | Unchecked and explicitly deferred |
| Tracked changed files | Exactly 2 |
| Staged files | 0 |
| Production numstat | 123 additions + 2 deletions |
| Test numstat | 183 additions + 2 deletions |
| CODE+TEST budget | **310/400** |

Unchecked future tasks do not block this bounded verification except where task 4.2a itself is not behaviorally complete.

## Fresh Build and Runtime Evidence

| Command | Exit | Result |
|---|---:|---|
| `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore -c Debug --verbosity minimal` | 0 | 0 errors; existing package warnings |
| `dotnet test ... --no-restore --no-build -c Debug --filter "FullyQualifiedName~DurableOwner_"` | 0 | 8 passed, 0 failed, 0 skipped |
| `dotnet test ... --no-restore --no-build -c Debug --filter "FullyQualifiedName~AntiTamperMonitorTests"` | 0 | 86 passed, 0 failed, 0 skipped |
| Exact safety filter excluding only the inherited NamedPipe and Scheduler FQNs | 0 | 1,287 passed, 0 failed, 0 skipped; existing duplicate-ID notice |
| Unique external portable coverage, exact AntiTamper class | 0 | 86 passed, 0 failed, 0 skipped |
| `git diff --check` | 0 | Clean |

Authoritative fresh coverage artifact:

`C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b1-independent-portable-coverage-20260826-200829-010\df1ad37e-0d38-4c58-93bd-2e4dd560427e\coverage.cobertura.xml`

An initial `--no-build` coverage attempt produced an empty Cobertura document because the existing build lacked portable symbols. It is not counted as evidence; the unique portable-PDB rerun above is the authoritative result.

## Behavioral Compliance Matrix

| Exact C1B1 behavior | Runtime/static evidence | Result |
|---|---|---|
| Rehydrate before first remote; missing valid | `DurableOwner_RehydratesBeforeFirstRemote_AndMissingStateIsValid` observes load before the first backend call | ✅ COMPLIANT |
| Invalid/unavailable fail closed; owner cancellation propagates | `DurableOwner_InvalidOrUnavailableLoadFailsClosed_AndCancellationUsesOwnerToken` covers invalid state, unavailable store, no remote/effects, and the observed owner token | ✅ COMPLIANT |
| Save accepted snapshot and pending identity before reaction/notification | Callbacks inspect the validating store before effects; however first revoked is not saved at all | ⚠️ PARTIAL |
| Exact keyed live `AddIssue` reaction | `ExecuteDecisionChainAsync` calls the four-argument legacy overload at `AntiTamperMonitor.cs:747`; the test only reads the persisted key and never verifies keyed forwarding | ❌ FAILING |
| Exact notification key | Notification callback receives and matches `PendingNotificationId` | ✅ COMPLIANT |
| Every saved envelope fully valid; reaction completion carried into notification completion | `RecordingEscalationStore.SaveAsync` calls full `value.Validate()`; `owner.DurableState` is updated after reaction completion before notification completion | ✅ COMPLIANT |
| Pending reaction and pending notification restart independently | One test runs each pending domain separately and verifies the corresponding effect only | ✅ COMPLIANT for one reconciliation pass |
| Completed same-key immediate replay no-op | Static rehydrate predicates skip `pending == completed`, but the test clears invocations after startup without asserting the no-op | ⚠️ PARTIAL |
| Distinct later decision persists and executes once; no broad restart guard | `DurableOwner_CompletedReplayIsNoOp_ButDistinctLaterDecisionExecutes` proves a later Add executes; source uses exact-key comparisons rather than the superseded broad guard | ✅ COMPLIANT for execution; persistence is indirect |
| Recovery requires `Phase == Degraded && RecoveryLatch` and exact Resolve evidence | Source checks both; degraded+latch test verifies one exact Resolve and no keyed Add | ✅ COMPLIANT |
| Valid non-Degraded latch must not Resolve solely by latch | Source predicate is correct, but no current C1B1 test constructs this valid state and proves keyed Add/no Resolve | ❌ UNTESTED |
| First/second/third/fourth revoked persisted boundaries and deadline | The test expects `(0, Normal)` after the first revoked because `PrepareDurableStateAsync` returns before saving Warn; this contradicts the required persisted first boundary. It then observes 2/Normal, 3/Pending, 3/Pending and unchanged due | ❌ FAILING |
| Actual three-trust recovery and revoked reset | Recovery test starts at `TrustStreak=2` and supplies one trust; no test drives three trusts or inserts revoked to prove reset | ❌ UNTESTED |
| Actual `RecordAgentDeath`, stop/restart, enforcement convergence, no duplicate same-key effect | Test calls `RecordAgentDeath`, starts/stops, restarts, and proves no added same-key reaction/notification plus persisted completions | ✅ COMPLIANT for nominal convergence |
| Handler pure; AntiTamper sole async owner; no I/O under lock | Handler blob equals HEAD; store/effect awaits are outside `lockObject` | ✅ COMPLIANT statically |
| No C1C timer / C2 Program | Program and Handler blobs equal HEAD; no timer or composition diff | ✅ COMPLIANT statically |

**Compliance summary**: 9 compliant, 3 partial, 2 failing, 2 untested (grouped exact C1B1 behaviors).

## Correctness Findings

### 1. Live reaction drops the required exact idempotency key

`PrepareDurableStateAsync` persists `decision.ReactionIdempotencyKey`, but `ExecuteDecisionChainAsync` dispatches live Limit/Degrade through:

`AddIssueAsync(issue, severity, reason, CancellationToken.None)`

rather than the existing keyed overload. Restart reconciliation uses the keyed overload, so live and restart paths are inconsistent. `DurableOwner_SavesAcceptedStateBeforeEffects_WithExactKeys` never asserts the Add invocation's key and therefore passes while the contract is broken.

### 2. First revoked accepted boundary is not durable

`PrepareDurableStateAsync` returns when a decision has no Limit/Degrade/recovery reaction and no notification. The first revoked Warn therefore mutates the pure handler but performs no store save. The boundary test masks this by converting a null store state to `(0, Normal)` and asserting that as the first expected result. A restart after the first accepted revoked observation loses its streak and sequence authority.

### 3. Two required nominal scenarios lack runtime evidence

- The non-Degraded `RecoveryLatch=true` branch is correct by inspection but has no covering passing test.
- The recovery test shortcuts the threshold with a preloaded trust streak of 2 and never proves a revoked reset, so it does not cover actual three-trust recovery/reset.

These are required C1B1 scenarios, not deferred C1B2 fault/race work.

## Strict TDD Chronology and Hash Audit

The current top C1B1 ledger row has exactly six semantic columns (seven pipe delimiters including boundaries). It does not reuse the superseded broad-candidate hashes/counts.

| Artifact | Independently computed SHA-256 | Audit |
|---|---|---|
| Initial behavioral RED `sdd7-unit4c2c1b-c1b1-red-20260826.log` | `E8D9465367913C218F555C8B574424D0889AFC79587F16119A3910F93D6D8057` | Match; 1/5 passed, 4 failed, exit 1 |
| Compile/setup triangulation RED | `569996E86AB45000B1BEB8A3FDBAF3E592C9F706EC304504B537C53F56E9DA10` | Match; CS0029/CS0039 only, not behavioral RED |
| Behavioral triangulation RED | `58A080A9EA271DDFA7DF4E278E604C1D014DE9F0A9E2E7DD6D8905795AC6C85E` | Match; 6/8 passed, 2 failed, exit 1 |
| Final DurableOwner 8/8 | `C6C9CD1B9265B8EDD3B83A956CE2D75697A1D5B5CD2D8D5E3FDED2A196A43445` | Match |
| Final AntiTamper 86/86 | `C368662E5DB7D5F0B683BCC7A58BC6CB1CCAC71D1D11D392228E7F80E71E802C` | Match |
| Exact-exclusion safety 1,287/1,287 | `B00776710E371E7035852368AE7C57991AAB8BB397E992E8C374EDB2577257C1` | Match |
| Affected build | `8EFC36E534FF6C705A1B97959CBE2534C488AF56F30AA7968A15C22A3610EC58` | Match |
| Apply coverage XML | `798D0503E28E2D390A9744A973D31B869708269F2D6AD2B6D1153F9FE0363747` | Match |

Initial behavioral RED failures/messages match the ledger:

1. Restart reconciliation expected legacy Add once and observed zero.
2. Rehydrate ordering expected `Loads == 1` and observed zero.
3. Save-before-effect failed with `NullReferenceException` during disposal.
4. Degraded recovery expected exact Resolve once and observed zero.

Behavioral triangulation RED failed on the invalid/unavailable-load test (`IntegrityEscalationStateException` during disposal) and actual revoked/trust boundary test (`NullReferenceException`). The separate compile artifact contains only test-authoring CS0029/CS0039 and is correctly excluded from behavioral RED.

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Current six-column C1B1 row present |
| RED chronology/hash integrity | ✅ | All three RED hashes and messages match |
| GREEN currently executable | ✅ | 8/8 and 86/86 fresh |
| Triangulation adequate | ❌ | Exact keyed live Add, persisted first revoked, non-Degraded latch, and actual three-trust/reset are not proven |
| Safety net | ✅ | Exact exclusions only, 1,287/1,287 |

**TDD judgment**: credible chronology, incomplete behavioral completion.

## Test Layer Distribution and Assertion Quality

| Layer | Tests | Files |
|---|---:|---:|
| Unit/component with mocked collaborators | 86 total; 8 C1B1 focus | 1 |
| Integration/E2E | 0 | 0 |

**Assertion quality**: no tautology or ghost loop was found. Two material mismatches remain:

- `DurableOwner_SavesAcceptedStateBeforeEffects_WithExactKeys` does not inspect the live Add key.
- `DurableOwner_ActualRevokedAndTrustObservationsPersistBoundariesAndReset` asserts a missing first persisted boundary and performs no trust/reset sequence despite its name.

## Changed-File Coverage

| File / method | Line | Branch | Hit evidence |
|---|---:|---:|---|
| `AntiTamperMonitor` | 98.97% | 84.33% | Fresh portable Cobertura class |
| `RunOwnedStartupAsync` | 100% | 100% | Executed |
| `RehydrateAsync` | 100% | 90% | Executed |
| `ReconcileDurableEffectsAsync` | 87.5% | 71.42% | Executed; incomplete branches |
| `ExecuteDecisionChainAsync` | 92.45% | 96.42% | Executed |
| `PrepareDurableStateAsync` | 100% | 85.71% | Executed |
| `SaveEffectProgressAsync` | 100% | 100% | Executed |
| `SaveStateAsync` | 100% | 50% | Executed |
| `ShouldExecute` | 90% | 76.92% | Exact-key branches partly executed |

Meaningful changed-production line coverage exceeds 80%, but coverage cannot compensate for the incorrect live overload and missing/incorrect scenario assertions.

## Design, Scope, Budget, and Hygiene

- Exact tracked allowlist: `src/ControlParental.Service/AntiTamperMonitor.cs` and `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`.
- `IntegrityVerdictHandler.cs` and `Program.cs` working blobs equal HEAD.
- Staged files: 0. `git diff --check`: exit 0.
- Budget: `123+2 + 183+2 = 310/400`; no exception.
- No new abstraction, schema field, retry, lock, framework, C1C timer, or C2 Program composition was introduced.
- `.codegraph/` remains untracked and externally lock-held; it was not killed or removed.
- No source/test or historical report was edited by verification. No stage, commit, remote, history, restore, solution build, OS, or Piranha action was performed.

## Baseline Warning

The two inherited parent differential artifacts match exactly:

- NamedPipe: `EDD4B18CDE785E46148B781BF91B1C91DCF905AAADF5F58F902DCDC9A32FD4A9`
- Scheduler: `E8F74F09C07D876FE1823D9E3C320869B28DDF82999DFDF62FE68E129D25B428`

The fresh safety command excludes only those two exact FQNs and passes 1,287/1,287. Neither excluded test is in the two-file C1B1 diff. This remains a parent/host baseline warning, not a C1B1 blocker.

## Deferred Task 4.2b Boundary

The following are explicitly deferred and were not used as C1B1 blockers: cancellation-ignoring save/reaction/notification races; stale effect/fault classification; and durable save/effect/progress failure retry/restart. The current FAIL is based only on nominal C1B1 contract violations and missing nominal runtime scenarios.

## Issues

### CRITICAL

1. Live Limit/Degrade dispatch uses unkeyed `AddIssueAsync`, violating exact keyed reaction forwarding.
2. The first accepted revoked observation is not persisted; the test masks the loss as `0/Normal`.
3. Actual three-trust recovery with revoked reset has no covering passing runtime test.
4. Valid non-Degraded latch behavior has no covering passing runtime test.

### WARNING

- The two exact inherited host tests remain valid baseline exclusions.
- Existing package/analyzer and duplicate-test-ID notices remain.
- Immediate completed replay no-op and distinct-later persistence are not asserted as strongly as their combined test name implies.

### SUGGESTION

- None; remediation is outside this independent verification.

## Final Verdict and Readiness

**FAIL** — runtime gates, hashes, coverage, scope, budget, and hygiene pass, but nominal C1B1 exact-key dispatch and first-boundary durability are incorrect, and two required nominal scenarios remain untested.

**Commit readiness**: **No**.  
**Archive readiness**: **No**.
