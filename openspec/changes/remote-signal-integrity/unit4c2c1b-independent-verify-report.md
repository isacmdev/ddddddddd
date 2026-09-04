# Verification Report

**Change**: `remote-signal-integrity` — Unit 4C2C1B owner reconciliation  
**Branch / HEAD**: `feat/sdd7-4c2c1b-owner-reconciliation` / `f37df4a1036a2969b852f929f9db632c7b424206`  
**Mode**: Strict TDD  
**Verdict**: **FAIL**

## Completeness

| Metric | Result |
|---|---:|
| Tasks total | 16 |
| Tasks complete | 11 |
| Tasks incomplete | 5 (future C1C/C2/integrated/compatibility work) |
| C1B task 4.2 | Checked |
| C1B tracked files | Exact two-file allowlist |
| Staged files | 0 |
| CODE+TEST budget | 280/400 (`99+3` production, `178` tests) |

The checked task is not verified complete: required C1B durable-progress and stale-generation scenarios lack valid runtime proof, and source inspection identifies contract-breaking paths.

## Build and Runtime Evidence

| Command | Exit | Result |
|---|---:|---|
| `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore -c Debug` | 0 | 0 errors; existing NU1601 warning |
| `dotnet test ... --filter FullyQualifiedName~DurableOwner` | 0 | 10/10 passed |
| `dotnet test ... --filter FullyQualifiedName~AntiTamperMonitorTests` | 0 | 88/88 passed |
| Exact safety net excluding only the two supplied FQNs | 0 | 1289/1289 passed; one duplicate-ID discovery notice |
| Exact NamedPipe baseline test | 1 | `UnauthorizedAccessException: Access to the path is denied`, line 352 |
| Exact TaskScheduler baseline test | 1 | `Expected result to be True, but found False`, line 202 |
| Fresh external AntiTamper coverage | 0 | 88/88; unique artifact below |
| `git diff --check` | 0 | Clean |

Fresh coverage artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b-independent-coverage-20260826-unique\972bf8b4-fc26-48ad-97e4-ab6428e2c018\coverage.cobertura.xml`.

The fresh excluded safety count is 1289 rather than the historical 1288; both exact excluded tests were independently selected and failed when run alone, so the exclusion boundary itself is confirmed.

## Behavioral Compliance Matrix

| Requirement / scenario | Runtime evidence | Status |
|---|---|---|
| Missing state is valid and load precedes first remote | `DurableOwner_RehydratesBeforeFirstRemote_AndMissingStateIsValid` | ✅ COMPLIANT |
| Corrupt/unavailable/cancelled load fails closed | `DurableOwner_LoadFaultFailsClosed_AndCancellationPropagates` proves corrupt + cancellation; unavailable is only represented by the same generic injected failure path | ⚠️ PARTIAL |
| Save accepted state/pending identity before effects | `DurableOwner_SavesAcceptedStateBeforeReactionAndNotification_WithExactKeys` asserts ordering | ✅ COMPLIANT for ordering |
| Exact live reaction key and notification key | Same test checks keyed reaction and pending notification key | ✅ COMPLIANT |
| Restart pending reaction independently | `DurableOwner_RestartReconcilesPendingReactionWithoutNotificationReplay` proves dispatch/no notification, but does not assert completion persistence or restart no-op | ⚠️ PARTIAL |
| Restart pending notification independently | `DurableOwner_RestartReconcilesPendingNotificationAfterCompletedReaction` proves dispatch/no reaction, but does not assert completion persistence or second-restart no-op | ⚠️ PARTIAL |
| Completed effects replay as no-ops | `DurableOwner_CompletedEffectsReplayAsNoOps` | ✅ COMPLIANT |
| Recovery uses `ResolveIssue`, never `AddIssue` | `DurableOwner_RecoveryUsesResolveAndNeverAddIssue` | ✅ COMPLIANT for the supplied degraded state |
| Phase + recovery latch determine authoritative recovery | No test covers a valid non-Degraded state with `RecoveryLatch=true`; implementation checks the latch alone | ❌ UNTESTED / incorrect statically |
| First-through-fourth revoked, durable unchanged pending state | No real validating-store test; subsequent snapshots can regress completed effect progress | ❌ UNTESTED |
| Three-trust recovery and revoked reset | Named test starts from an already pending recovery and never proves three trusts or revoked reset | ❌ UNTESTED |
| Agent-death/restart/enforcement convergence | Named test does not call `RecordAgentDeath`, create a race, or restart | ❌ UNTESTED |
| Stale generation/effect completion and fault suppression | Named test only performs start/stop/start and a non-decreasing save-count assertion; it creates no stale completion or fault | ❌ UNTESTED |
| Save/effect fault retryability and lifecycle cancellation | Existing monitor tests exercise generic effect/lifecycle behavior, but no durable save-fault/crash-progress scenario proves C1B convergence | ⚠️ PARTIAL |
| Handler purity / AntiTamper sole async owner / no I/O under owner lock | Handler and Program blobs equal HEAD; durable store/effect awaits are outside `lockObject` | ✅ COMPLIANT statically |
| No C1C timer or C2 Program composition | No Handler/Program diff; no new deadline scheduling path | ✅ COMPLIANT statically |

**Scenario compliance**: 6 compliant, 4 partial, 5 untested/incorrect (grouped C1B behaviors above). Runtime passage of the focused suite does not establish full spec compliance.

## Critical Correctness Findings

1. **Combined reaction + notification completion can produce an invalid durable envelope.** `ExecuteDecisionChainAsync` passes the original `durableState` to both progress saves (`AntiTamperMonitor.cs:664/686,708`). After reaction completion is saved, notification completion is built again from the pre-reaction object (`SaveEffectProgressAsync`, lines 788–793), so `CompletedNotificationId` can be non-null while `CompletedReactionId` remains null. `IntegrityEscalationState.Validate()` rejects that state. The test double at `AntiTamperMonitorTests.cs:1267` never calls `value.Validate()`, masking the production `FileIntegrityEscalationStateStore.SaveAsync` contract.

2. **Completed progress can be durably rolled back by a later accepted/no-effect decision.** Effect completion is saved only to the store; the handler snapshot is not updated. `PrepareDurableStateAsync` later snapshots the handler and saves it, potentially replacing completed IDs with stale/null values. The store intentionally has no monotonic comparison, so restart can replay effects that were already completed. No test performs effect completion, a later decision, restart, and no-replay assertion.

3. **Stale generation can continue rehydrate/save/effect work.** Generation activity is checked before awaits, not after them. `RehydrateAsync` can restore and reconcile after a cancellation-ignoring load completes; `PrepareDurableStateAsync` can return a state after a cancellation-ignoring save; `ExecuteDecisionChainAsync` then invokes effects without a fresh active-generation check. The purported stale-generation test creates no blocked collaborator, stale completion, or fault.

4. **Recovery classification is not the designed phase+latch predicate.** `ReconcileDurableEffectsAsync` uses only `current.RecoveryLatch` at line 754. The domain validator permits `RecoveryLatch=true` outside `Degraded`, while the design requires `Phase` plus `RecoveryLatch`; such a valid state can incorrectly resolve rather than add/reconcile the pending reaction.

## Strict TDD Audit

The authoritative row has six columns (7 pipe delimiters including boundaries) and correctly separates setup from behavioral RED.

| Artifact | SHA-256 audit | Result |
|---|---|---|
| Setup compile | `A8E358B4B2D47F990DA9B7C524F05F163A990FBE1951EE4D6555002BF12ADD7D` | Match; setup only |
| Behavioral RED | `E8C4C471837AE233300856FA528272D3D5907184DBD021A5118D4385A8C4B77F` | Match; 1 pass/8 fail, exit 1 |
| First GREEN | `E488D6A6879B5A90FCB6F885C6591B4DE971F15B50F9CF3E72EBB471F843C53E` | Match; 9/9, exit 0 |
| Remediation attempt 2 | `F977AC75DBC44CD47CE3765BA04A38B776637C235987ACD05E89504AF514875E` | Match; 9/10, exit 1 |
| Remediation attempt 3 | `BC61DCD0D3B81AB744C979B3DCF3503396EC55C94C83D23FDB428B3D7F258201` | Match; 8/10, exit 1 |
| Remediation attempt 4 | `9A12C990F56738620578C7190043FC1E43B81684C57E31A8C5EF3140F8B48700` | Match; 10/10, exit 0 |
| Historical refactor | `2EE780E2E027D1B3E1CFAB54EF4126C265055FAC15CB30C975AA821813CC27CA` | Match; correctly 87/87 historically; fresh post-assertion run is 88/88 |
| Excluded safety | `864C1D91024E0365F8B0D1263AEAC5777163389D0F13C7195943EDDC0A7FDF13` | Match; 1288/1288, exit 0 |

RED failures match the ledger: completed/restart/recovery cases failed with `NullReferenceException`; save-before-effect had an empty saved-state collection; load fault expected `IntegrityEscalationStateException` but none was thrown; missing-state expected one load but observed zero. Attempts 2/3 match the recorded Resolve call-count failures.

**TDD compliance judgment**: chronology/hashes are credible, but triangulation and assertion quality are insufficient. Two tests are materially title-only (`DurableOwner_StaleGenerationCompletionAndFaultCannotPersistOrEmit`, `DurableOwner_AgentDeathAndOneShotRecoveryConverge`), and the recording store omits production validation. Strict TDD behavioral completion therefore fails.

## Fresh Changed-File Coverage

| File / method state machine | Line | Branch | Hit evidence |
|---|---:|---:|---|
| `AntiTamperMonitor` | 98.97% | 83.95% | Current Cobertura class |
| `RehydrateAsync` | 100% | 100% | 19/19 lines, 481 total hits |
| `PrepareDurableStateAsync` | 100% | 100% | 9/9 lines, 423 total hits |
| `ReconcileDurableEffectsAsync` | 100% | 91.66% | 24/24 lines, 81 total hits; line 769 branch 1/2 |
| `SaveEffectProgressAsync` | 100% | 100% | 6/6 lines, 166 total hits |
| `SaveStateAsync` | 100% | 50% | 4/4 lines, 100 total hits; line 798 branch 2/4 |
| `ExecuteDecisionChainAsync` | 93.22% | 93.75% | 55/59 lines, 1629 total hits; partial branches at lines 679/683 |

Coverage matches the reported aggregate/method values, but high execution coverage does not close the invalid-store, stale-generation, or progress-regression scenarios because the test double accepts states the production store rejects and the relevant races are not constructed.

## Design, Scope, and Hygiene

- Exact tracked diff: only `src/ControlParental.Service/AntiTamperMonitor.cs` and `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`.
- Handler and Program working blobs equal HEAD.
- No staged files; `git diff --check` passes; budget is exactly 280/400 changed CODE+TEST lines.
- `.codegraph/` remains untracked and externally lock-held; it was used read-only and is not product scope.
- OpenSpec remains untracked as expected; this report is the only verifier-created artifact.
- No C1C deadline behavior, C2 DI composition, retry, distributed lock, migration, field, framework, source/test edit, stage, commit, remote, OS, or Piranha action was introduced.
- Apply-progress retains superseded intermediate counts (283/342, 87) before the authoritative 280/400 and fresh 88 wording. The final closure is understandable, but the stale intermediate prose is a documentation warning.

## Baseline Differential Judgment

Both supplied C1A differential hashes match exactly:

- NamedPipe: `EDD4B18CDE785E46148B781BF91B1C91DCF905AAADF5F58F902DCDC9A32FD4A9`
- TaskScheduler: `E8F74F09C07D876FE1823D9E3C320869B28DDF82999DFDF62FE68E129D25B428`

Fresh C1B failures reproduce the same exception/assertion and source locations. Neither affected file is in the C1B diff. These are valid inherited host-only baseline exclusions and are warnings, not C1B blockers.

## Issues

### CRITICAL

- Invalid combined reaction/notification completion state is masked by a non-validating store double.
- Later snapshots can overwrite completed durable progress and cause restart replay.
- Stale generation/cancellation suppression is not implemented across collaborator awaits and has no meaningful runtime test.
- Phase+latch recovery classification is implemented as latch-only.
- Required agent-death, first-through-fourth revoked durability, three-trust/reset, save-fault retryability, and durable stale-generation scenarios have no passing behavioral assertions.

### WARNING

- Two inherited host tests fail; parent differential validates the exclusions.
- Fresh excluded suite count is 1289 versus historical 1288, while both exclusions remain exact.
- Apply-progress contains stale intermediate C1B counts before the authoritative closure section.
- Existing NU1601 and duplicate-test-ID notices remain.

### SUGGESTION

- None; fixes are intentionally outside this verification task.

## Final Verdict and Readiness

**FAIL** — focused tests, build, safety net, hashes, coverage, scope, and budget are green, but C1B does not prove or correctly implement durable effect-progress monotonicity, production-store validity, stale-generation suppression, or authoritative recovery classification.

**Archive readiness**: No.  
**Commit readiness**: No.  
**Blockers**: the five CRITICAL groups above require remediation and fresh behavior-first evidence before re-verification.
