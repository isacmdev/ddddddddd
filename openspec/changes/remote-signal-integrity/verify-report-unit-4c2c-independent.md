# Independent Verification Report: SDD7 Unit 4C2C

**Change**: `remote-signal-integrity` — Unit 4C2C only  
**Branch**: `feat/sdd7-4c2c-owner-rehydrate`  
**Base / HEAD**: `1fc5b60dc801bbb12d25f2131cb17201c4f04b47` / `1fc5b60dc801bbb12d25f2131cb17201c4f04b47`  
**Mode**: Strict TDD (authoritative launch override; repository config still says `strict_tdd: false`)  
**Persistence**: hybrid  
**Verdict**: **FAIL**

The fresh build, focused tests, full Service suite, scope allowlist, and 302/400 budget pass. The implementation is not spec-complete: the actual durable deadline callback has zero runtime coverage, restart rollback can extend the deadline, trust does not cancel the owner timer, notification-only recovery is skipped, rehydration faults/cancellation are swallowed, and production DI wiring has no runtime test.

## Completeness

| Metric | Result |
|---|---:|
| Tasks total | 14 |
| Tasks complete | 12 |
| Tasks incomplete | 2 (`5.1`, `5.2`, Unit 5 only) |
| Unit 4C2C tasks | 3/3 checked |
| Normalized required scenarios | 18 |
| Compliant | 9 |
| Partial | 3 |
| Untested/failing | 6 |

The two unchecked Unit 5 tasks are outside this verification slice and do not block the Unit 4C2C task-completeness judgment. They do prevent archive/readiness claims for the complete SDD7 change.

## Fresh Command Evidence

All commands ran sequentially in the requested worktree. Test/coverage outputs were isolated under `C:\Users\Usuario\AppData\Local\Temp\opencode`.

| Gate | Command | Result |
|---|---|---|
| Root/status | `git rev-parse --show-toplevel; git status --short --branch; Test-Path .codegraph` | Correct worktree/branch; `.codegraph` initially absent |
| CodeGraph | `codegraph init <worktree>` then `codegraph explore ...` | 363 files, 8,648 nodes, 22,524 edges; used before broad source exploration |
| Focus discovery | `dotnet test ... --list-tests --filter "FullyQualifiedName~ControlParental.Service.Tests.IntegrityVerdictHandlerTests|FullyQualifiedName~ControlParental.Service.Tests.AntiTamperMonitorTests|FullyQualifiedName~ControlParental.Service.Tests.IntegrityRuntimePathTests"` | Exactly 127 cases: AntiTamper 81, handler 37, runtime-path 9 |
| Focused execution | Same exact three-class filter with `--no-build` and isolated TRX | **127 passed, 0 failed, 0 skipped** |
| Service build | `dotnet build src\ControlParental.Service\ControlParental.Service.csproj --no-restore --configuration Debug --verbosity minimal` | **0 errors**, 1 existing NU1601 warning |
| Full Service | `dotnet test tests\ControlParental.Service.Tests\ControlParental.Service.Tests.csproj --no-restore --configuration Debug --no-build ...` | **1,277 passed, 0 failed, 0 skipped**; existing duplicate-ID discovery notice |
| Coverage | Exact three-class filter, `--collect:"XPlat Code Coverage"`, isolated directory | **127 passed**; Cobertura `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-unit4c2c-independent-coverage\fabc0d0b-1d4b-4404-a965-ce8767a14cc6\coverage.cobertura.xml` |
| Domain | Not run | Carried/skipped: exact base diff contains no Domain path; Service build rebuilt `ControlParental.Domain.dll` successfully |

### Exact focused mapping

The 127 selected cases were limited to these exact classes, not accidental substring neighbors:

- `AntiTamperMonitorTests`: 81 cases, including all lifecycle/effect barriers and the three new `DurableOwner_*` cases.
- `IntegrityVerdictHandlerTests`: 37 cases, including before/at deadline, trust cancellation, one-shot recovery, snapshot/restore, and rollback handler evaluation.
- `IntegrityRuntimePathTests`: 9 cases, including the canonical direct-handler deadline path and non-definitive/stale/cancellation paths.

The five newly added tests all passed:

1. `DurableOwner_RehydratesBeforeRemoteAndForwardsExactReactionKey`
2. `DurableOwner_UsesBKeyedReactionAdmissionWithoutChangingKey`
3. `DurableOwner_ReconcilesPendingReactionBeforeAcceptingRemoteWork`
4. `SnapshotRestore_PreservesCountersDeadlineAndDecisionIdentity`
5. `RestoreState_RejectsWallClockRollbackAndFailsClosed`

## Changed-Path Coverage

Aggregate class percentages are not accepted as deadline proof. Generated async methods and callback bodies were inspected separately.

| Changed production path | Line | Branch | Runtime detail |
|---|---:|---:|---|
| `AntiTamperMonitor` aggregate | 98.63% | 80.48% | Aggregate only |
| `RehydrateAndCheckAsync.MoveNext` | 82.75% | 75% | Catch/fail-closed lines 335–339 unhit |
| `ReconcilePersistedEffectsAsync.MoveNext` | 100% | 50% | Only pending-reaction branch exercised |
| `EvaluatePersistedDeadlineAsync.MoveNext` | **0%** | **0%** | Lines 364–369 all zero hits |
| Timer delegate at line 331 | 100% line | 0% branch | Delegate was invoked once, but did not execute the admitted deadline state machine |
| `PersistStateAsync.MoveNext` | 100% | 100% | Save-before-effect helper executed |
| `GetDecisionShape(IntegrityEscalationState)` | **0%** | **0%** | Rehydrated completed-progress shape path unhit |
| `IntegrityVerdictHandler` aggregate | 96.28% | 94.31% | Aggregate only |
| `SnapshotState` | 100% | 66.66% | Snapshot branches not exhaustive |
| `RestoreState` | 100% | 100% | Direct restore executed |
| `FailClosed` | **0%** | 100% (no branches) | Rehydrate failure path unhit |
| `EvaluateDeadlineDecision` | 100% | 100% | Direct handler evaluation, not owner timer callback |
| `Program` / `RunMainAsync` registration path | **0%** | **0%** | No production DI graph runtime execution |

**Critical distinction**: `IntegrityVerdictHandler.EvaluateDeadline*` passing does not cover `Timer → RunAdmittedStageAsync → EvaluatePersistedDeadlineAsync → PersistStateAsync → ExecuteDecisionChainAsync`. Cobertura proves that production callback state machine has zero hits.

## 18-Scenario Behavioral Compliance Matrix

The two duplicate non-definitive scenarios in the runtime-integrity and offline-enforcement specs are normalized into one row; all other named scenarios remain separate, yielding 18 distinct scenarios.

| # | Spec scenario | Passing runtime evidence | Result |
|---:|---|---|---|
| 1 | Valid evidence receives trust | `ActiveGeneration_UsesCanonicalResolveStageExactlyOnce`; full suite passed | ✅ COMPLIANT |
| 2 | Equal timestamp conflicting verdicts | `EqualTimestamps_AreOrderedByAcceptanceAndAcceptedDuplicatesCount` | ✅ COMPLIANT |
| 3 | Unknown/transient response does not degrade protection (two duplicate spec scenarios) | `NonDefinitivePreservesPendingAndDeadlineIsPureAndOneShot`; five `CanonicalAuthority_NonDefinitiveCase_*` cases | ✅ COMPLIANT |
| 4 | Restart and recovery preserve semantics | Direct handler recovery and existing issue restart tests pass, but no C durable-owner restart/recovery execution | ⚠️ PARTIAL |
| 5 | Missing or corrupt state fails closed | No covering C test; new missing-store test expects remote acceptance; catch/fail-closed path has zero hits | ❌ FAILING / UNTESTED |
| 6 | Concurrent mixed order | `ConcurrentAdmittedDecisions_AreSerializedByGenerationGate` | ✅ COMPLIANT |
| 7 | Stale callback and callback fault | Existing stale-generation and fault tests pass, but the new deadline callback stale/fault path is not executed | ⚠️ PARTIAL |
| 8 | Key and pure-handler boundary | `DurableOwner_UsesBKeyedReactionAdmissionWithoutChangingKey`; pure handler tests; notification key tests | ✅ COMPLIANT |
| 9 | Supported safety evidence degrades health | Existing AntiTamper/runtime enforcement tests in full suite | ✅ COMPLIANT |
| 10 | Semantic issue recovery survives restart | Existing durable issue restart/recovery tests in full suite | ✅ COMPLIANT |
| 11 | Agent-death and enforcement race | Existing AntiTamper generation/race suite passed | ✅ COMPLIANT |
| 12 | First through fourth revoked | Handler staging tests pass, but no durable-owner store/deadline/effect assertion covers the four accepted observations | ⚠️ PARTIAL |
| 13 | Exact deadline and trust cancellation | Direct handler tests pass; real owner callback, callback firing, cancellation, and stale callback branches have no passing execution | ❌ UNTESTED |
| 14 | Pending effect recovery after restart | Only pending-reaction startup is tested; completed-reaction/pending-notification recovery is skipped by source | ❌ FAILING / UNTESTED |
| 15 | Three-trust recovery | Handler one-shot recovery and active resolve tests pass | ✅ COMPLIANT |
| 16 | Restart and crash convergence | No owner restart test proves original deadline/no extension or crash between each persistence/effect boundary | ❌ UNTESTED |
| 17 | Forward or rollback clock change | Direct handler rollback test passes; owner rehydrate schedules from rolled-back wall time and does not exercise fail-closed path | ❌ FAILING / UNTESTED |
| 18 | Failure and lifecycle shutdown | Existing generic fault/Stop/Dispose tests pass, but deadline callback and rehydrate cancellation/fault branches are uncovered and rehydrate exceptions are swallowed | ❌ FAILING / UNTESTED |

**Compliance summary**: 9 compliant, 3 partial, 6 failing/untested. Required scenarios without a passing covering test are CRITICAL under the verification contract.

## Correctness and Design Coherence

| Requirement / decision | Status | Evidence |
|---|---|---|
| Handler remains synchronous/pure with minimum snapshot/restore | ✅ | `IntegrityVerdictHandler.cs:221-284`; no I/O/timer/background owner added |
| AntiTamper is sole async owner and rehydrates before normal remote check | ⚠️ | Source order at `AntiTamperMonitor.cs:311-343`; missing state still accepts remote and rehydrate errors are swallowed |
| A2 store remains per-instance/atomic only | ✅ | No Domain/store diff; no retry, lock, migration, CAS, or new framework |
| One immutable deadline, no extension, trust cancellation | ❌ | Handler origin/due is stable, but owner scheduling at lines 328–332 uses wall-clock remainder and trust never disposes/replaces `DeadlineTimer` |
| Monotonic epoch/sequence/generation/effect progress | ⚠️ | Existing progress suppression passes; owner deadline/stale timer progression is untested |
| Save accepted state + pending identity before effects | ✅ for exercised normal decision path | `ExecuteDecisionChainAsync` lines 728–789; `PersistStateAsync` lines 374–385 |
| Reconcile reaction, notification, deadline after restart | ❌ | `ReconcilePersistedEffectsAsync` returns at line 347 when reaction is complete, skipping a pending notification |
| No collaborator I/O under owner state lock | ✅ | Pure handler call is inside short owner admission lock; store/enforcement/outbox calls occur outside it |
| Exact keyed reaction; unchanged notification; no keyed resolve invention | ✅ | `AddReactionAsync` lines 791–820 and notification lines 765–779; recovery remains existing unkeyed resolve |
| Cancellation/fault/Stop/Dispose observe deadline/lifecycle | ⚠️ | Generic owned-task drain passes, but rehydrate catch swallows faults/cancellation and callback-specific paths are untested |
| Singleton store and real composition path | ⚠️ source / ❌ runtime | Registration exists at `Program.cs:401-428`; hosted start exists at `Program.cs:1252-1256`; coverage is zero and no C composition test names the store |

## CRITICAL Findings

1. **The production deadline callback is untested.** Cobertura reports `AntiTamperMonitor/<EvaluatePersistedDeadlineAsync>d__74.MoveNext` at **0% line / 0% branch**, lines `364-369`. The timer delegate at line `331` received one hit, but the admitted callback body did not run. Before/at/after direct handler tests do not satisfy the callback scenario. Scenarios 13, 16, 17, and the deadline portion of 18 are therefore UNTESTED.

2. **Restart wall-clock rollback can extend the deadline instead of failing closed.** `AntiTamperMonitor.cs:328-332` computes `delay = persistedDue - WallClockNow` and schedules that larger delay after rollback. `RestoreState` does not compare current owner time with `MaxWallClockSeenUtc`; fail-closed evaluation occurs only if the delayed callback or another report eventually reaches the handler. This violates the no-extension/rollback requirement.

3. **Trust does not cancel the owner deadline timer.** The handler clears its pending origin/due (`IntegrityVerdictHandler.cs:345-350`), but `AntiTamperMonitor` never disposes or invalidates `Generation.DeadlineTimer` except during final drain (`AntiTamperMonitor.cs:393`). The stale timer can later admit `EvaluateDeadlineDecision`, advance epoch/sequence, and save a no-op decision. No passing test executes this branch.

4. **Notification-only restart recovery is skipped.** `AntiTamperMonitor.cs:347` returns whenever reaction is absent or already completed, before checking pending notification state. A persisted `CompletedReactionId == PendingReactionId` with an incomplete notification never reaches lines `348-360`. `GetDecisionShape(IntegrityEscalationState)` is also at 0% runtime coverage. Scenario 14 fails.

5. **Rehydration failure/cancellation is not observable and does not apply durable fail-closed enforcement.** The blanket catch at `AntiTamperMonitor.cs:335-339` swallows load, restore, reconciliation, persistence, effect, and cancellation failures, sets only in-memory handler state, then allows startup to complete without executing/persisting a severe reaction. The catch and `IntegrityVerdictHandler.FailClosed` both have zero coverage. This violates the missing/corrupt/cancel/fault scenarios.

6. **Missing durable state is accepted as normal despite the spec's fail-closed scenario.** `LoadAsync` returning null falls through to remote work at `AntiTamperMonitor.cs:342`. `DurableOwner_RehydratesBeforeRemoteAndForwardsExactReactionKey` explicitly uses a null initial store and asserts `load → remote → save` (`AntiTamperMonitorTests.cs:1124-1140`). That test proves the opposite of the runtime-integrity missing-state scenario.

7. **Production DI/start wiring is not runtime-proven.** Source registers the singleton store and injects it (`Program.cs:401-428`), and the hosted service starts AntiTamper (`Program.cs:1252-1256`), but no changed or existing C test resolves this graph. Focused Cobertura reports Program at 0%; no `HostRegistrationTests` assertion mentions `IIntegrityEscalationStateStore`.

## WARNING Findings

1. **Strict-TDD history is not independently reproducible.** The ledger records behavioral `3 failed / 121 passed → 126 passed → 127 passed`, and honestly states no raw RED artifact was retained. It gives failure descriptions, not exact test method names/messages, so historical RED identity and the two test-count increments cannot be independently reconstructed. Current GREEN is proven by fresh `127/127`.

2. **Focused tests overstate what they assert.** `DurableOwner_RehydratesBeforeRemoteAndForwardsExactReactionKey` asserts only order, not a key; `DurableOwner_ReconcilesPendingReactionBeforeAcceptingRemoteWork` asserts only reaction count, not before-remote order, notification, progress persistence, or deadline scheduling; `SnapshotRestore_PreservesCountersDeadlineAndDecisionIdentity` does not assert restored sequence/epoch identity. Assertions are non-trivial but triangulation is insufficient.

3. **Changed production coverage is uneven despite high aggregate percentages.** Program is 0%, fail-closed is 0%, deadline callback is 0%, rehydrated decision shape is 0%, and key restart branches are only 50% even though aggregate AntiTamper line coverage exceeds 98%.

## Strict-TDD Audit

| Check | Result | Details |
|---|---|---|
| Six-column ledger present | ✅ | RED, GREEN, TRIANGULATE, SAFETY NET, REFACTOR present for 4.1–4.3 |
| Behavioral RED, not compile-only | ✅ reported | `3 failed, 121 passed`; initial NETSDK1004 is correctly separated as infrastructure |
| RED exact command/failure identity | ⚠️ | Command inferable as the same 3-class focus; exact failing method names/messages and raw output absent |
| GREEN current | ✅ | Fresh 127/127 |
| Triangulation | ❌ | Required callback/restart/notification-only/rollback branches absent |
| Safety net | ✅ | Fresh full Service 1,277/1,277 and build 0 errors |
| Refactor/scope | ✅ | No framework/retry/lock/migration expansion; 302/400 |

**TDD compliance**: current GREEN and a genuine behavioral RED are credible, but strict evidence is incomplete and scenario triangulation fails.

## Test Layer and Assertion Quality

| Layer | Cases | Files | Notes |
|---|---:|---:|---|
| Unit/component | 118 | 2 | Handler 37; AntiTamper 81, mostly mocked collaborators |
| Integration/runtime-path | 9 | 1 | Backend/file issue path, but no C escalation store or owner timer callback |
| E2E | 0 | 0 | Not configured |

**Assertion quality**: no tautologies, ghost loops, or production-free tests found in the two modified files. The three scope-overstatement/triangulation warnings above remain.

## Scope, Budget, and Repository Hygiene

Exact diff from base/HEAD `1fc5b60dc801bbb12d25f2131cb17201c4f04b47`:

| Allowed file | + | - | Changed |
|---|---:|---:|---:|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 120 | 4 | 124 |
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | 82 | 0 | 82 |
| `src/ControlParental.Service/Program.cs` | 5 | 2 | 7 |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | 59 | 0 | 59 |
| `tests/ControlParental.Service.Tests/IntegrityVerdictHandlerTests.cs` | 30 | 0 | 30 |
| **CODE+TEST** | **296** | **6** | **302/400** |

- Exact allowlist: PASS; only the five designed code/test files differ from base.
- No Domain diff: PASS; Domain gates carried/skipped with exact path proof.
- Staged files: none.
- Generated coverage/TRX: isolated outside worktree.
- Existing cumulative `openspec/changes/remote-signal-integrity/` remains untracked as expected; this report is the only verification file added by this run.
- `git diff --check`: PASS.
- Generated `.codegraph`: initialized for required analysis and removed before final status.

## Final Verdict

**FAIL**

Unit 4C2C is **ready for remediation, not ready for commit authorization**. A later explicit commit authorization would be premature until direct passing runtime tests cover the actual deadline callback (fire/stale/trust-cancel/Stop/Dispose), restart no-extension/rollback, notification-only reconciliation, rehydrate fault/cancellation, and the real production DI path—and the identified source defects are corrected in a separate apply phase.
