# Verification Report

**Change**: `remote-signal-integrity` — task 4.2a / C1B1 nominal durable owner remediation  
**Branch / HEAD**: `feat/sdd7-4c2c1b-owner-reconciliation` / `f37df4a1036a2969b852f929f9db632c7b424206`  
**Mode**: Strict TDD  
**Verdict**: **FAIL**

## Executive Summary

The remediation closes all four nominal C1B1 defects from the prior report. Live durable Limit/Degrade dispatch now uses the persisted reaction key while the no-store path remains legacy; first-through-fourth revoked state is persisted; the public three-trust and subsequent revoked paths run; and non-Degraded latch rehydration performs keyed Add rather than Resolve. Completed same-key replay is a runtime no-op, and a distinct later observation persists and executes through the keyed durable branch.

The quality gate is nevertheless not fully green. Build, DurableOwner `8/8`, AntiTamper `86/86`, safety `1,287/1,287`, and a repeated portable-coverage run `86/86` passed, but the first fresh portable-coverage command exited 1 because `LateNonCancellableFailure_PostRemovalGapRetainsFaultForStop` failed (`Assert.Throws`: no exception). The immediate unique rerun passed. Under the verification rule that any relevant non-zero test command is CRITICAL, this nondeterministic result prevents PASS even though it is outside the C1B1 remediation assertions.

## Completeness

| Metric | Result |
|---|---:|
| Tasks total | 17 |
| Tasks complete | 11 |
| Tasks incomplete | 6 |
| Task 4.2a | Checked and nominal behavior complete |
| Task 4.2b | Unchecked and explicitly deferred |
| Tracked changed files | Exactly 2 |
| Staged files | 0 |
| Production numstat | 131 additions + 3 deletions |
| Test numstat | 231 additions + 2 deletions |
| CODE+TEST budget | **367/400** |

Task 4.2b cancellation/fault hardening and later Unit 4/5 tasks are outside this bounded C1B1 judgment. Full-change archive readiness remains false.

## Build and Runtime Evidence

| Command | Exit | Result |
|---|---:|---|
| `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore -c Debug --verbosity minimal` | 0 | 0 errors; 5 inherited package warnings |
| `dotnet test ... --no-restore --no-build -c Debug --filter "FullyQualifiedName~DurableOwner_"` | 0 | 8 passed, 0 failed/skipped |
| `dotnet test ... --no-restore --no-build -c Debug --filter "FullyQualifiedName~ControlParental.Service.Tests.AntiTamperMonitorTests"` | 0 | 86 passed, 0 failed/skipped |
| Exact safety filter excluding only the inherited NamedPipe and Scheduler FQNs | 0 | 1,287 passed, 0 failed/skipped; duplicate-ID notice retained |
| First unique external portable-coverage run, exact AntiTamper class | 1 | 85 passed, 1 failed: `LateNonCancellableFailure_PostRemovalGapRetainsFaultForStop` |
| Immediate second unique external portable-coverage run, same command/filter | 0 | 86 passed, 0 failed/skipped |
| `git diff --check` | 0 | Clean |

Authoritative successful rerun artifact:

`C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b1-independent-reverify-coverage-rerun-20260826-153532094\5e70b0bf-ed96-4b6f-a664-57826595b868\coverage.cobertura.xml`

SHA-256: `2395B187A33F59A3374D63CDE20E30D140C2E004592AC28B35D0D7016DF84E90`.

## Behavioral Compliance Matrix

| Exact C1B1 behavior | Runtime/static evidence | Result |
|---|---|---|
| Rehydrate before first remote; missing state valid | `DurableOwner_RehydratesBeforeFirstRemote_AndMissingStateIsValid` observes load before one backend call | ✅ COMPLIANT |
| Invalid/unavailable load fails closed; cancellation uses owner token | The focused test covers both failures, no remote/effects, and a cancellable observed token | ✅ COMPLIANT |
| Save accepted snapshot and pending identities before effects | Store callbacks validate and inspect pending reaction/notification keys before collaborator completion | ✅ COMPLIANT |
| Live durable Limit/Degrade forwards exact persisted reaction key once | Test compares keyed Add argument to `store.Last.State.PendingReactionId`, verifies keyed once and legacy never; source selects keyed overload when a store exists | ✅ COMPLIANT |
| No-store compatibility remains legacy | `ExecuteDecisionChainAsync` selects the legacy overload only when `stateStore is null`; the full AntiTamper class passes | ✅ COMPLIANT |
| First through fourth revoked persist sequence/streak/phase/deadline | Actual public observations persist `(2,1,Normal)`, `(3,2,Normal)`, `(4,3,Pending)`, `(5,3,Pending)`; validation proves no deadline outside Pending and fourth preserves the third deadline | ✅ COMPLIANT |
| Public three-trust recovery and revoked reset | Test drives trust streak 1, 2, then recovery to Normal/0, followed by revoked to streak 1/Normal | ✅ COMPLIANT |
| Degraded+latch recovery uses Resolve | Exact Resolve once and keyed Add never are asserted | ✅ COMPLIANT |
| Non-Degraded+latch does not Resolve solely on latch | Restart asserts keyed Add with `non-degraded-latch` once and no Resolve | ✅ COMPLIANT |
| Completed same-key replay is a no-op | After clearing startup calls, explicit same-key `ExecuteDecisionAsync` leaves enforcement invocations empty | ✅ COMPLIANT |
| Distinct later decision persists and executes without broad restart suppression | A later public revoked observation produces Add and persists a pending key different from `completed-old`; static dispatch is the same keyed durable branch | ✅ COMPLIANT, assertion is indirect on exact count/key |
| Pending reaction and notification reconcile independently | Separate restart cuts execute the pending domain and persist completion | ✅ COMPLIANT |
| Agent-death restart converges without duplicate same-key effect | `RecordAgentDeath`, stop/start, and second restart preserve call counts and exact completed IDs | ✅ COMPLIANT |
| Handler remains pure; AntiTamper owns async I/O outside monitor locks | Handler blob equals HEAD; store/effect awaits occur outside `lockObject` | ✅ COMPLIANT statically |
| No C1C/C2 expansion | Handler and Program working blobs equal HEAD; no timer/composition change | ✅ COMPLIANT statically |

**Nominal C1B1 compliance summary**: 15/15 grouped behaviors compliant. The prior four nominal CRITICAL groups are closed.

## Correctness (Static Evidence)

| Requirement | Status | Notes |
|---|---|---|
| Durable keyed live reaction | ✅ Implemented | `AddIssueAsync(..., decision.ReactionIdempotencyKey, ...)` when `stateStore` exists |
| Legacy construction compatibility | ✅ Implemented | Null-store branch retains `AddIssueAsync(..., CancellationToken.None)` |
| Save every accepted definitive snapshot | ✅ Implemented | `PrepareDurableStateAsync` no longer skips no-effect accepted observations |
| Preserve completion progress for same pending identity | ✅ Implemented | Completion IDs carry forward only when pending IDs match |
| Recovery classification | ✅ Implemented | Rehydrate Resolve requires both `Phase == Degraded` and `RecoveryLatch` |
| Exact-key replay predicate | ✅ Implemented | Durable completion suppresses only matching keys; distinct keys are not broadly blocked |
| Lock boundary | ✅ Implemented | Monitor lock protects admission/decision mutation; persistence and collaborator awaits are outside it |

## Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| `AntiTamperMonitor` is the sole durable async owner | ✅ Yes | Store load/save and effect reconciliation remain in the monitor |
| `IntegrityVerdictHandler` remains pure | ✅ Yes | Blob equals HEAD; no I/O added |
| Exact effect identities survive restart | ✅ Yes | Pending/completed reaction and notification progress are stored and reconciled independently |
| C1B1 nominal slice excludes stale/fault hardening | ✅ Yes | No C1B2 retry/race expansion was added |
| No I/O under state lock | ✅ Yes | Awaited store/effect calls are outside `lockObject` |

## Strict TDD Compliance

The active remediation ledger row has the required six semantic columns.

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Separate C1B1 remediation row exists |
| RED behavior and immutable artifact | ✅ | 6/8 passed, 2 failed, exit 1; exact missing-key and first-boundary failures |
| RED hash | ✅ | `D5F77F6364CB6CF61CBCAD9FB39740927DDD9AFB98379421033B0EE29D3CAC1D` independently matches |
| GREEN currently executable | ✅ | Fresh focused 8/8 and plain AntiTamper 86/86 |
| GREEN supplied hash | ⚠️ | Expected `392717DD97DAF8DDBEEF5D78C731BE55CDB593C60437BFD37A057FFBD7B46550` was supplied, but no named immutable GREEN log was available to hash independently |
| Triangulation | ✅ | Keyed/legacy split, revoked 1–4, trust 1–3/reset, latch classification, replay, restart |
| Safety net | ❌ | Plain safety passed, but one relevant coverage-instrumented AntiTamper command exited 1 before the green rerun |
| Scope/refactor discipline | ✅ | Exact two files, 367/400, no new abstraction/field/framework/retry/lock beyond the planned optional store owner state |

**TDD compliance**: behavior-first remediation is credible and all targeted GREEN assertions pass; the fresh command set is not uniformly green.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit/component with mocked collaborators | 86 total; 8 C1B1 focus | 1 | xUnit, Moq, FluentAssertions |
| Integration | 0 | 0 | — |
| E2E | 0 | 0 | — |
| **Total** | **86** | **1** | |

## Changed-File Coverage

| File / method | Line | Branch | Rating |
|---|---:|---:|---|
| `AntiTamperMonitor` | 98.97% | 84.93% | ✅ Excellent line coverage |
| `RunOwnedStartupAsync` | 100% | 100% | ✅ |
| `RehydrateAsync` | 100% | 90% | ✅ |
| `ReconcileDurableEffectsAsync` | 87.5% | 78.57% | ⚠️ Acceptable |
| `ExecuteDecisionChainAsync` | 93.22% | 93.75% | ⚠️ Acceptable |
| `PrepareDurableStateAsync` | 100% | 96.42% | ✅ |
| `SaveEffectProgressAsync` | 100% | 100% | ✅ |
| `SaveStateAsync` | 100% | 50% | ✅ line / partial branch |
| `ShouldExecute` | 90% | 80.76% | ⚠️ Acceptable |

The apply coverage artifact also independently matches SHA-256 `C589962C824ED7106A416472EEC0F8BB789C079FB766458552D7F476FD9CDC65` and reports the same `98.97% / 84.93%` class rates.

## Assertion Quality

No tautology, ghost loop, assertion-without-production-call, or precondition-skipped C1B1 test was found.

**WARNING**: `DurableOwner_CompletedReplayIsNoOp_ButDistinctLaterDecisionExecutes` directly proves no-op, later persistence, and later Add, but its later branch uses `Contain(Method.Name == AddIssueAsync)` rather than directly asserting one keyed call whose argument equals the new persisted key. Exact keyed-once behavior is proven at runtime by `DurableOwner_SavesAcceptedStateBeforeEffects_WithExactKeys` and shared production-path inspection, so this is an assertion-local weakness rather than missing behavioral coverage.

## Quality Metrics

**Linter**: ➖ No separate changed-file linter configured  
**Type checker/build**: ✅ 0 compile errors  
**Hygiene**: ✅ `git diff --check`; Handler/Program blobs equal HEAD; staged 0

## Issues Found

### CRITICAL

1. A relevant fresh test command exited non-zero: the first portable-coverage run failed `LateNonCancellableFailure_PostRemovalGapRetainsFaultForStop` because the expected `InvalidOperationException` was not observed. The same full class passed immediately before without coverage and passed on the immediate unique coverage rerun, indicating nondeterminism, but the red command remains real evidence.

### WARNING

1. Distinct-later exact keyed-once evidence is distributed across two passing tests plus the shared source path instead of asserted directly in the replay test.
2. The expected remediation GREEN hash has no discoverable named immutable log, although fresh 8/8 execution passed.
3. Existing package warnings and one duplicate xUnit test ID remain.
4. The two inherited host-sensitive tests remain exact baseline exclusions: NamedPipe access denial and Scheduler unregister false result.

### SUGGESTION

- None; verification did not remediate code or tests.

## Scope, Budget, and Deferred Boundary

- Exact tracked allowlist: `src/ControlParental.Service/AntiTamperMonitor.cs` and `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`.
- `IntegrityVerdictHandler.cs` and `Program.cs` working blobs equal HEAD.
- CODE+TEST is `131+3 + 231+2 = 367/400`; no exception.
- `.codegraph/` remains untracked and externally daemon-owned.
- C1B2 cancellation-ignoring save/effect/fault races and durable retry hardening remain deferred to task 4.2b and were not used as nominal C1B1 blockers.
- No source/test, prior report, proposal/spec/design, OS, Piranha, restore, solution build, stage, commit, remote, or history operation was performed by verification.

## Final Verdict and Readiness

**FAIL** — all four prior nominal C1B1 CRITICAL defects are closed and the successful gates establish the intended behavior, but one fresh relevant coverage command exited non-zero. The immediate green rerun makes this a nondeterministic gate failure rather than a demonstrated remediation regression; it still blocks a clean independent PASS under the verification contract.

**C1B1 nominal behavior readiness**: **Yes, with the assertion warning above**.  
**Independent verification gate**: **No**.  
**Commit readiness**: **No**.  
**Archive readiness**: **No**.
