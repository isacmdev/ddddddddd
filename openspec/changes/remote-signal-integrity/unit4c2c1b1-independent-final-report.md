# Verification Report

**Change**: `remote-signal-integrity` — task 4.2a / C1B1 independent final gate  
**Branch / HEAD**: `feat/sdd7-4c2c1b-owner-reconciliation` / `f37df4a1036a2969b852f929f9db632c7b424206`  
**Mode**: Strict TDD  
**Verdict**: **PASS WITH WARNINGS**

## Executive Summary

This new clean sequential gate is fully green. The portable-PDB build passed; the exact C1B1 focus passed `8/8`; the whole AntiTamper class passed `86/86`; the inherited timing case passed its one required isolated execution; the exact-exclusion Service safety gate passed `1,287/1,287`; and both separately hosted portable coverage commands passed `86/86` on their first and only executions and produced non-empty Cobertura files with identical AntiTamper/C1B1 rates and hit counts.

Source inspection and runtime assertions reconfirm all 15 nominal C1B1 behavior groups. The prior re-verification's one coverage-instrumented miss is classified as historical scheduling nondeterminism, not a current defect: it was not rerun as part of this invocation, the exact test and both new coverage hosts are green now, and the inherited Unit 4C1B2a2 evidence records 100 sequential plus 20 parallel fresh processes green. Task 4.2b fault/race hardening remains explicitly deferred and is not a C1B1 blocker.

## Completeness

| Metric | Result |
|---|---:|
| Tasks total | 17 |
| Tasks complete | 11 |
| Tasks incomplete | 6 |
| Task 4.2a | Checked; scoped implementation complete |
| Task 4.2b | Unchecked; explicitly deferred |
| Tracked changed files | Exactly 2 |
| Staged files | 0 |
| Production numstat | 131 additions + 3 deletions |
| Test numstat | 231 additions + 2 deletions |
| CODE+TEST budget | **367/400** |

The bounded C1B1 slice is complete. The cumulative OpenSpec change is not archive-ready because 4.2b and later tasks remain unchecked.

## Fresh Build and Test Execution

All commands below ran sequentially. No `dotnet`/testhost overlap occurred, no failing gate was rerun, and every fresh command exited `0`.

| Gate | Exact command summary | Exit | Fresh result |
|---|---|---:|---|
| Portable-PDB affected test build | `dotnet build tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore -c Debug -p:DebugType=portable -p:DebugSymbols=true --verbosity minimal` | 0 | 0 errors; 9,427 existing analyzer/package warnings |
| Exact C1B1 focus | `dotnet test ... --no-restore --no-build -c Debug --filter "FullyQualifiedName~DurableOwner_"` | 0 | 8 passed, 0 failed, 0 skipped |
| Whole AntiTamper | `dotnet test ... --filter "FullyQualifiedName~ControlParental.Service.Tests.AntiTamperMonitorTests"` | 0 | 86 passed, 0 failed, 0 skipped |
| Exact inherited timing case | `dotnet test ... --filter "FullyQualifiedName=ControlParental.Service.Tests.AntiTamperMonitorTests.LateNonCancellableFailure_PostRemovalGapRetainsFaultForStop"` | 0 | 1 passed, 0 failed, 0 skipped |
| Exact-exclusion Service safety | Excluded only `NamedPipeUIServerHostedAdapterTests.StartAsync_ExposesUiPipeBeforeClientConnects` and `TaskSchedulerBackupServiceUnregisterTests.UnregisterBackupTasksAsync_WhenNoTasks_ReturnsTrue` | 0 | 1,287 passed, 0 failed, 0 skipped; inherited duplicate-ID notice |
| Portable coverage host A | Exact AntiTamper filter; unique external results directory | 0 | 86 passed, non-empty Cobertura |
| Portable coverage host B | Exact AntiTamper filter; second unique external results directory | 0 | 86 passed, non-empty Cobertura |
| `git diff --check` | Current two-file diff | 0 | Clean |

No unfiltered suite, restore, solution build, OS/Piranha operation, stage, commit, remote, history, or restore operation was run.

## Fresh Coverage Evidence

| Host | Artifact | Bytes | SHA-256 | AntiTamper line | AntiTamper branch |
|---|---|---:|---|---:|---:|
| A | `C:\Users\Usuario\AppData\Local\Temp\opencode\c1b1-independent-final-coverage-a-20260826\17519f66-e01b-426a-86d8-f2e47904944e\coverage.cobertura.xml` | 5,073,319 | `99DA1303DB3CFE8036B6F0636CD846AC195510E3ABE3151E46F520132339BD01` | 98.97% | 84.93% |
| B | `C:\Users\Usuario\AppData\Local\Temp\opencode\c1b1-independent-final-coverage-b-20260826\0cab3042-3ffe-47d8-b791-83ffbaa3b941\coverage.cobertura.xml` | 5,073,319 | `FDB078E1024F06DD842769A257C52CD8A06721D9BE8ED0C32547E4BB264C4A3E` | 98.97% | 84.93% |

The XML hashes differ because the independently generated documents carry host/run metadata. Their C1B1 method rates, covered-line counts, and aggregate hit counts are identical:

| Method | Line | Branch | Covered lines | Hits A / B |
|---|---:|---:|---:|---:|
| `RunOwnedStartupAsync` | 100% | 100% | 10/10 | 725 / 725 |
| `RehydrateAsync` | 100% | 90% | 19/19 | 524 / 524 |
| `ReconcileDurableEffectsAsync` | 87.50% | 78.57% | 21/24 | 81 / 81 |
| `ExecuteDecisionChainAsync` | 93.22% | 93.75% | 55/59 | 1,808 / 1,808 |
| `PrepareDurableStateAsync` | 100% | 96.42% | 17/17 | 713 / 713 |
| `SaveEffectProgressAsync` | 100% | 100% | 8/8 | 216 / 216 |
| `SaveStateAsync` | 100% | 50% | 10/10 | 400 / 400 |
| `ShouldExecute` | 90% | 80.76% | 9/10 | 532 / 532 |

Changed production line coverage is excellent and exceeds the planned 80% meaningful threshold. The modified test assembly is not instrumented by this collector.

## Behavioral Compliance Matrix

| Required nominal behavior | Runtime and source evidence | Result |
|---|---|---|
| Rehydrate before first remote; missing state is valid | `DurableOwner_RehydratesBeforeFirstRemote_AndMissingStateIsValid`; startup source awaits `RehydrateAsync` before remote integrity work | ✅ COMPLIANT |
| Corrupt/unavailable state fails closed; cancellation propagates | Focused invalid/unavailable/cancellation test observes no backend/effects and the owner cancellation token | ✅ COMPLIANT |
| Exact keyed live durable dispatch and legacy no-store path | Save-before-effect test asserts the keyed overload exactly once and legacy never; source uses legacy only when `stateStore is null` | ✅ COMPLIANT |
| First through fourth revoked persisted boundaries and deadline | Public observations persist sequence/streak/phase `(2,1,Normal)`, `(3,2,Normal)`, `(4,3,Pending)`, `(5,3,Pending)`; fourth preserves the third due time/key | ✅ COMPLIANT |
| Public three-trust recovery and revoked reset | Runtime drives trust 1, trust 2, recovery to Normal/0, then revoked to streak 1 | ✅ COMPLIANT |
| Degraded versus non-Degraded latch classification | Degraded+latch resolves; valid non-Degraded+latch performs exact keyed Add and never Resolve | ✅ COMPLIANT |
| Save before effects, full envelope validation, cumulative progress | Store validates each envelope; effect callbacks observe persisted pending IDs; reaction completion is saved before notification completion | ✅ COMPLIANT |
| Pending reaction and notification reconcile independently | Separate restart cuts execute only the pending domain and persist its completion | ✅ COMPLIANT |
| Completed same-key replay is a no-op | Explicit same-key `ExecuteDecisionAsync` leaves enforcement invocations empty | ✅ COMPLIANT |
| Distinct later decision persists and executes | Later public revoked observation changes the persisted pending key and reaches the durable Add path | ✅ COMPLIANT |
| `RecordAgentDeath` restart convergence | Stop/start and second restart preserve completed IDs and do not increase same-key Add/notification counts | ✅ COMPLIANT |
| Exact notification identity | Live callback and restart assertions use the unchanged pending notification key | ✅ COMPLIANT |
| Exact recovery authority | Recovery requires `Phase == Degraded && RecoveryLatch` and invokes `ResolveIssueAsync` with authoritative trust evidence | ✅ COMPLIANT |
| Handler purity, lock boundary, sole owner | Handler blob equals HEAD; persistence/effect awaits are outside `lockObject`; AntiTamper remains the async owner | ✅ COMPLIANT |
| No C1C/C2 expansion | Handler and Program blobs equal HEAD; no deadline-owner or composition diff exists | ✅ COMPLIANT |

**Nominal C1B1 compliance summary**: **15/15 grouped behaviors compliant**. The four prior nominal defect groups remain closed.

## Correctness and Design Coherence

| Decision / invariant | Status | Evidence |
|---|---|---|
| Startup rehydrates before remote acceptance | ✅ | `RunOwnedStartupAsync` orders load/restore/reconciliation before integrity check |
| Accepted definitive snapshots persist even without an immediate effect | ✅ | `PrepareDurableStateAsync` snapshots and saves without the old no-effect early return |
| Pending and completed identities are cumulative and domain-independent | ✅ | Reaction and notification completion are saved separately |
| Same-key suppression is exact, not a broad restart guard | ✅ | `ShouldExecute` compares durable completion key and ordered decision shape |
| Recovery uses existing semantic authority | ✅ | Rehydrate resolves only for Degraded plus recovery latch; no keyed resolve API invented |
| Store and collaborator I/O occurs outside monitor state locks | ✅ | Source inspection confirms no awaited store/effect operation under `lockObject` |
| Scope remains C1B1 nominal owner work | ✅ | Only AntiTamper source/test differ; 4.2b stale/fault hardening remains deferred |

## Prior Timing Miss Classification

The prior re-verification recorded one failed coverage-instrumented execution of `LateNonCancellableFailure_PostRemovalGapRetainsFaultForStop`, followed immediately by `86/86` on a unique rerun. That old non-zero result is historical evidence only and was not treated as a command in this invocation.

The new independent evidence is uniformly green without retries: whole AntiTamper `86/86`, the exact timing test `1/1`, coverage host A `86/86`, and coverage host B `86/86`. The historical Unit 4C1B2a2 independent report additionally records this inherited timing/fault pair green in 100 sequential fresh processes and 20 parallel fresh processes. With unchanged current source since re-verification and no fresh miss, the old single failure is classified as an isolated scheduling miss rather than a reproducible product or C1B1 defect. No code/test change is justified for that historical event.

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Current six-column C1B1 remediation row exists in `apply-progress.md` |
| Genuine RED | ✅ | `sdd7-c1b1-remediation-red.log` hashes to `D5F77F6364CB6CF61CBCAD9FB39740927DDD9AFB98379421033B0EE29D3CAC1D`; 6/8 with the two intended keyed-live and first-boundary failures |
| Current GREEN | ✅ | Fresh exact focus 8/8 |
| Triangulation | ✅ | Keyed/legacy split, revoked 1–4, trust 1–3/reset, latch classification, replay, independent restart, and agent death |
| Safety net | ✅ | Whole AntiTamper, exact inherited timing case, exact-exclusion safety, and both coverage hosts are fresh green |
| Refactor/scope discipline | ✅ | Exact two files, 367/400, no C1C/C2/retry/lock/framework expansion |

**TDD compliance**: **6/6 current checks pass**. The previously supplied remediation GREEN hash still lacks a matching named immutable artifact; fresh executable GREEN evidence is authoritative for this gate, so that provenance gap remains a warning only.

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit/component with mocked collaborators | 86 total; 8 C1B1 focus | 1 | xUnit, Moq, FluentAssertions |
| Integration added by C1B1 | 0 | 0 | — |
| E2E added by C1B1 | 0 | 0 | — |

The exact-exclusion 1,287-test Service safety gate supplies broader regression evidence without claiming C1C/C2 composition coverage.

## Assertion Quality

No tautology, ghost loop, assertion-without-production-call, or precondition-skipped C1B1 assertion was found. The loop-based invalid-state and revoked-boundary tests have fixed non-empty inputs and assertions after execution.

**WARNING**: the distinct-later branch proves new persistence and an Add invocation, but its exact keyed-once proof is distributed across that test, the save-before-effect test, and the shared keyed source path rather than repeated locally in the replay test.

## Quality, Scope, Budget, and Hygiene Audit

- HEAD is exactly `f37df4a1036a2969b852f929f9db632c7b424206`.
- The only tracked modified files are `src/ControlParental.Service/AntiTamperMonitor.cs` and `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs`.
- Numstat is exactly production `131+3`, tests `231+2`, total **367/400**; no `size:exception`.
- Staged files: 0. `git diff --check`: exit 0.
- `IntegrityVerdictHandler.cs` current blob equals HEAD (`ef67b148fd7d9f3261f3f5a29545100fac0fe21d`).
- `Program.cs` current blob equals HEAD (`96d75f43f3f694e44c73aaef835ab0d1eeacdd9e`).
- `.codegraph/` is the only additional tool index path and remains allowed as externally lock-held state.
- OpenSpec artifacts are cumulative and untracked by the current base; this verifier added only this new report and did not edit any prior report.
- No separate changed-file linter was run. Compiler/type checking passed with 0 errors; the existing analyzer/package warning corpus remains non-blocking.

## Issues Found

### CRITICAL

None.

### WARNING

1. The originally supplied remediation GREEN hash still has no matching named immutable artifact, although the exact current GREEN passed 8/8 and every fresh safety/coverage gate passed.
2. Exact keyed-once evidence for the distinct-later branch is distributed across two tests plus source inspection rather than asserted wholly in that one test.
3. Existing analyzer/package warnings and the duplicate xUnit test-ID notice remain.
4. The two inherited host-sensitive tests remain exact baseline exclusions; neither is in the C1B1 two-file diff.
5. Six cumulative tasks remain unchecked, including explicitly deferred 4.2b; therefore the whole change is not archive-ready.

### SUGGESTION

None. Verification made no source or test fixes.

## Final Verdict and Readiness

**PASS WITH WARNINGS** — all required fresh commands passed on their first execution, both independent portable coverage hosts are non-empty and behaviorally identical, all 15 nominal C1B1 behavior groups are compliant, Strict TDD evidence is credible, and scope/budget/hygiene pass. The warnings are provenance, assertion-local, inherited baseline, and cumulative-task limitations rather than C1B1 correctness failures.

**C1B1 independent gate**: **PASS**.  
**Commit readiness for the bounded C1B1 slice**: **Yes, with warnings; explicit authorization still required**.  
**Archive readiness for the cumulative change**: **No**.
