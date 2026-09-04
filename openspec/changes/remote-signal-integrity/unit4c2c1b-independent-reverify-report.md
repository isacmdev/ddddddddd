# Independent Re-verification Report

**Change**: `remote-signal-integrity` — Unit 4C2C1B owner reconciliation remediation  
**Branch / HEAD**: `feat/sdd7-4c2c1b-owner-reconciliation` / `f37df4a1036a2969b852f929f9db632c7b424206`  
**Mode**: Strict TDD  
**Verdict**: **FAIL**

## Executive Summary

The remediation closes the original invalid combined-progress envelope, cumulative progress merge, and phase+latch recovery defects for the tested paths. Build, focused tests, exact-exclusion safety, hashes, scope, and aggregate coverage are green. It does **not** close every prior CRITICAL: stale cancellation-ignoring save/effect/fault paths remain untested and incorrect, several formerly title-only requirements still lack behavioral proof, and a new restart guard suppresses every later non-recovery reaction after any completed durable reaction.

## Completeness and Scope

| Metric | Result |
|---|---:|
| Tasks total / complete | 16 / 11 |
| C1B task 4.2 | Checked |
| Tracked changed files | Exactly 2 |
| Staged files | 0 |
| Production numstat | 148 additions + 4 deletions |
| Test numstat | 212 additions + 0 deletions |
| Exact CODE+TEST budget | **364/400** additions+deletions |

The apply artifact's `360` is insertion-only and is not the review-budget total. The correct contract count is `148 + 4 + 212 = 364`.

Handler and Program working blobs equal HEAD (`ef67b148...`, `96d75f43...`). `git diff --check` passes. The old immutable FAIL report remains present and unchanged with SHA-256 `3DCB8A65F41F3FE28C81E4221E5B3FCD5C495BD7A3AF5F58F56ABEBF35356DAF`.

## Fresh Command Evidence

| Command | Exit | Result |
|---|---:|---|
| `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore -c Debug` | 0 | 0 errors; existing package warnings |
| `dotnet test ... --no-build --filter FullyQualifiedName~DurableOwner` | 0 | 11/11 passed |
| `dotnet test ... --no-build --filter FullyQualifiedName~AntiTamperMonitorTests` | 0 | 89/89 passed |
| Exact safety filter excluding only the two inherited FQNs | 0 | 1290/1290 passed; duplicate-ID notice |
| Fresh unique external AntiTamper coverage | 0 | 89/89 passed |
| Exact NamedPipe baseline test | 1 | `UnauthorizedAccessException: Access to the path is denied`, line 352 |
| Exact Scheduler baseline test | 1 | `Expected result to be True, but found False`, line 202 |
| `git diff --check` | 0 | Clean |

Fresh coverage artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b-independent-reverify-coverage-20260826-unique\ea95f282-529c-4d02-a520-bf4519ee9e92\coverage.cobertura.xml`.

## Prior CRITICAL Closure Matrix

| Prior CRITICAL | Adversarial result | Status |
|---|---|---|
| 1. Combined reaction+notification must persist valid cumulative IDs and restart without replay | `RecordingEscalationStore.SaveAsync` validates every state; the exact-key test asserts reaction and notification completion, validates final state, restarts, and asserts no effect replay. `MergeCompletedProgress` carries reaction completion into notification completion. | ✅ CLOSED for state validity/cumulative IDs/restart no-replay |
| 2. Later snapshots must not regress completed progress | The merge preserves completion when pending keys match and clears stale completion when a genuinely new key replaces it. The same exact-key test restarts from final progress. | ⚠️ PARTIAL — no test performs completion → later no-effect decision → restart; see new guard defect below |
| 3. Stale generation after cancellation-ignoring load/save/effect awaits | The only new race blocks `LoadAsync`, stops, releases, and asserts no save/backend/enforcement. It does not block save, reaction, notification, or fault completion. Catch paths still surface stale collaborator faults. | ❌ NOT CLOSED |
| 4. Recovery requires `Phase == Degraded && RecoveryLatch` | Production checks both. A valid Pending+latch state asserts no Resolve, exact keyed Add once, and persisted completion. | ✅ CLOSED |
| 5. Formerly title-only behaviors require real assertions | Some progress assertions were added, but first-through-fourth revoked durability, three-trust with revoked reset, actual agent-death/restart race, durable save/effect fault retry, and broad lifecycle cancellation remain absent. | ❌ NOT CLOSED |

## New Blocking Correctness Finding

`AntiTamperMonitor.cs:660-663` returns from every non-recovery decision whenever a rehydrated generation has any `DurableCompletedReaction` and no in-memory `ReactionProgress`:

```text
owner.Rehydrated && owner.ReactionProgress is null &&
owner.DurableCompletedReaction is not null && !reaction.IsAuthoritativeRecovery
```

This predicate does not compare the completed key, epoch, or sequence to the new decision. After restart, one previously completed reaction therefore suppresses all later revoked/limit/degrade decisions, and the early return occurs before `PrepareDurableStateAsync`, so the newly accepted observation is not persisted either. The restart test proves only immediate no-replay and never submits a distinct later decision. This violates deterministic accepted-observation persistence, first-through-fourth/restart convergence, and monotonic effect progression.

## Behavioral Compliance Matrix

| C1B behavior | Exact runtime assertion | Result |
|---|---|---|
| Rehydrate before first remote; missing valid | `DurableOwner_RehydratesBeforeFirstRemote_AndMissingStateIsValid`: load callback sees zero backend calls; then Loads=1/backend=1 | ✅ COMPLIANT |
| Corrupt/unavailable/cancel fail closed | `DurableOwner_LoadFaultFailsClosed_AndCancellationPropagates` proves corrupt fault, no backend, critical RestoreFailure, and cancellation propagation. Generic catch statically covers unavailable; File store owns classification. | ✅ COMPLIANT with static unavailable branch |
| Accepted state and pending IDs saved before effect | Exact-key test inspects saved state inside reaction/notification callbacks | ✅ COMPLIANT |
| Exact reaction and notification keys | Reaction key equals persisted pending reaction; notification callback finds exact pending key | ✅ COMPLIANT |
| Combined progress valid and cumulative | Validating store rejects every invalid state; test asserts completed reaction + notification and `State.Validate()` | ✅ COMPLIANT |
| Pending reaction restart reconciliation | Exact Add once, no notification, completed reaction persisted | ✅ COMPLIANT |
| Pending notification restart reconciliation | No Add, exact notification once, completed notification persisted | ✅ COMPLIANT |
| Completed replay no-op | Completed-effects test plus exact-key restart assert no enforcement/outbox | ✅ COMPLIANT for immediate restart |
| Later distinct decision after completed restart | No test; line 660 guard suppresses it | ❌ FAILING statically / UNTESTED dynamically |
| Recovery Resolve evidence | Degraded+latch test invokes `ResolveIssueAsync` once and never keyed Add | ✅ COMPLIANT |
| Non-Degraded latch | Pending+latch test never resolves; exact keyed Add and completion asserted | ✅ COMPLIANT |
| Stale cancellation-ignoring load | Actual blocked load race; stop before release; no save/backend/enforcement | ✅ COMPLIANT |
| Stale cancellation-ignoring save | No blocked-save test or post-save stale-fault assertion | ❌ UNTESTED |
| Stale cancellation-ignoring reaction/notification | No blocked-effect test. Effects use `CancellationToken.None`; post-await checks cannot undo an already emitted effect. | ❌ UNTESTED / incomplete |
| Stale fault suppression; active fault visibility | No durable stale-fault race. `RehydrateAsync` catch and effect catches throw without a current-generation guard. Generic non-durable fault tests do not prove this owner state path. | ❌ UNTESTED / incorrect statically |
| First through fourth revoked durable phases/deadline unchanged | Exact-key test executes three revoked checks after startup but does not assert WARN/LIMIT/Pending/fourth unchanged state or deadline identity | ❌ UNTESTED |
| Three-trust recovery and revoked reset | `PersistsAuthoritativeRecoveryBeforeCompletion` starts from an already latched recovery and expects two Resolve calls; it does not prove three trusts or revoked reset | ❌ UNTESTED |
| Agent-death/restart/enforcement convergence | Named test never calls `RecordAgentDeath`, creates no race, and creates no second monitor restart; it only starts/stops once and asserts recovery completion | ❌ UNTESTED |
| Save fault retryability | No validating store save-failure injection or retry/restart assertion | ❌ UNTESTED |
| Effect fault retryability | Existing generic monitor tests cover active reaction/notification faults, but no durable pending-progress retry/restart assertion exists | ⚠️ PARTIAL |
| Lifecycle cancellation/cleanup | Load cancellation and blocked-load Stop are asserted; save/effect cancellation and stale fault are not | ⚠️ PARTIAL |
| No I/O under owner lock | Durable loads/saves/effects are awaited outside `lockObject`; lock use is state/admission only | ✅ COMPLIANT statically |
| Handler pure; AntiTamper sole owner | Handler blob equals HEAD; durable async ownership is in AntiTamper | ✅ COMPLIANT statically |
| C1C/C2 absent | Program blob equals HEAD; no deadline scheduling or DI composition added | ✅ COMPLIANT statically |

## Assertion Quality Audit

| Test | Quality judgment |
|---|---|
| Exact-key/cumulative/restart | Strong: ordering, exact values, validation, completion, and restart no-effect assertions |
| Pending reaction/notification restart | Strong for one reconciliation pass and completion persistence |
| Pending+latch recovery predicate | Strong exact negative Resolve + positive keyed Add assertion |
| Stale generation | Strong only for blocked load; test name still overclaims completion **and fault** across save/effect domains |
| Agent-death convergence | **CRITICAL title/behavior mismatch**: no agent death, no race, no restart |
| Authoritative recovery | **CRITICAL scenario mismatch**: no three-trust threshold or revoked reset |

No tautology or ghost-loop assertion was found. The blocking issue is missing preconditions/behavior rather than trivial assertion syntax. The validating store calls `value.State.Validate()` rather than full `value.Validate()`; current production envelope constants make the tested invalid-state defect visible, but it is not fully production-fidelity for document/schema envelope validation.

## Strict TDD Audit

The authoritative remediation ledger has six columns (7 boundary pipes) and the old FAIL remains visible. Hashes independently match:

| Artifact | SHA-256 | Audit |
|---|---|---|
| Behavioral remediation RED2 | `862B154E1F5708FFA1741A48F4E273DA599ADAFEC75D0D9E624FB736AA7FD416` | Match; 6 pass/5 fail, exit 1 |
| GREEN1 | `8EEAF18EEC1FC257AD7FD047BDA39661645563B319F017716DAD1F7FF8005CBD` | Match; failed attempt retained |
| GREEN2 | `2F730CDA08A8503E9A2283963E46AAC0C0CB7D7A6E62CA7DECD6ABF2AB72CACE` | Match; failed attempt retained |
| GREEN3 | `D71C33F05755AFE684EE314D06D1D8CA128D3235CEB5FDA1605938A94CED8D79` | Match; failed attempt retained |
| GREEN4 | `933C189C92BC840900DF55560771CAF005B2192FF7586340E5AC3B36FD4D4D9D` | Match; failed attempt retained |
| GREEN5 | `DEA8CDA8A4FB133F1EC6064F52495F33D56C7B4BB066C3E66DE5621754AE97EB` | Match; failed attempt retained |
| GREEN6 | `377D9E86D75A49B77FA3360DBABA8CE57CE8D2C34D97AC540147FF403035E067` | Match; failed attempt retained |
| GREEN7 | `DCCA2637B03BCF5E6CEB94325ECFADD21929A3612499E57C76B727120795881E` | Match; failed attempt retained |
| Final focused GREEN8 | `0F49FE6F182811891CFEF581EFEF4E2865D174A0CB296ECE753AC566181D7E5D` | Match; 11/11 |
| Final AntiTamper GREEN3 | `F2846BF9B77D2C702C36A7FBD8CD64E2F05F4775C20C499C738D0287889E54F1` | Match; 89/89 |
| Excluded safety | `4DE687EEB15B26C295A0CB643BFEDDE172F8186E3CD443408ECB7E2773DB8F87` | Match; 1290/1290 |

RED2 failed for the intended completion defects: pending notification completion was null; pending reaction completion was null; recovery completion was null; combined exact-key flow raised `IntegrityEscalationStateException`; valid Pending+latch reconciliation also raised invalid-state failure. It did **not** establish RED for stale save/effect/fault, first-through-fourth, three-trust/reset, or actual agent-death convergence. Intermediate failed attempts are honestly retained and need not individually be green.

## Fresh Coverage

| File / method | Line | Branch | Hit evidence |
|---|---:|---:|---|
| `AntiTamperMonitor` | 98.07% | 81.06% | Uncovered main-class lines 491–492, 906–909 |
| `RehydrateAsync` | 100% | 91.66% | 25/25 lines; 543 hits |
| `PrepareDurableStateAsync` | 100% | 93.75% | 10/10 lines; 368 hits |
| `MergeCompletedProgress` | 100% | 86.66% | 14/14 lines; 198 hits |
| `ReconcileDurableEffectsAsync` | 100% | 77.77% | 28/28 lines; 107 hits |
| `SaveEffectProgressAsync` | 100% | 100% | 8/8 lines; 172 hits |
| `SaveStateAsync` | 100% | **50%** | 4/4 lines; 76 hits; condition 2/4 |
| `ExecuteDecisionChainAsync` | 94.28% | 88% | 66/70 lines; 1,865 hits |
| `IsGenerationActive` | 100% | 100% | 4/4 branch outcomes |
| `IsGenerationCurrent` | 100% | 100% | 2/2 branch outcomes |
| `ShouldExecute` | 63.63% | 50% | Lines 906–909 unexecuted |

Fresh values reproduce the aggregate and most apply method figures. The apply claim `SaveStateAsync 100%/100%` is inaccurate for this independent artifact; branch coverage is 50%. Coverage confirms execution, not the missing race/scenario semantics.

### Test Layer Distribution

| Layer | Tests | Files |
|---|---:|---:|
| Unit/component with mocked collaborators | 89 (11 DurableOwner) | 1 |
| Integration/E2E | 0 | 0 |

## Baseline and Environment Judgment

The exact isolated failures reproduce the unchanged C1A differential evidence and identical messages/locations. They remain valid host-only exclusions and are warnings, not C1B regressions. The exact-exclusion safety suite and affected test-project build both pass; this is sufficient for the C1B two-file slice. The pre-existing solution locked-restore/project-reference inconsistency does not justify dependency expansion or restore and is not a C1B blocker.

## Issues

### CRITICAL

1. Restart guard at `AntiTamperMonitor.cs:660-663` suppresses every later non-recovery decision after any durable completed reaction and skips accepted-state persistence.
2. Stale save/effect/notification/fault behavior remains untested; stale catch paths can still surface/report faults after generation invalidation.
3. First-through-fourth revoked persistence/deadline behavior lacks runtime assertions.
4. Three-trust recovery with revoked reset lacks runtime assertions.
5. Agent-death/restart/enforcement convergence remains a title-only test.
6. Durable save/effect fault retryability and full lifecycle cancellation lack runtime proof.

### WARNING

- Two exact inherited host tests fail and are valid baseline exclusions.
- `SaveStateAsync` independent branch coverage is 50%, not the reported 100%.
- The recording store validates state rather than the full envelope.
- Existing package-resolution and duplicate-test-ID warnings remain.

### SUGGESTION

- None; remediation is outside this independent verification.

## Final Verdict and Readiness

**FAIL** — the remediation improves and closes several original defects, but it does not close every prior CRITICAL and introduces a blocker that prevents legitimate post-restart non-recovery decisions.

**Archive readiness**: No.  
**Commit readiness**: No.  
**Required next action**: behavior-first remediation for the restart guard and the still-missing stale save/effect/fault, first-through-fourth, three-trust/reset, agent-death race, and durable retry/cancellation scenarios, followed by another independent verification.
