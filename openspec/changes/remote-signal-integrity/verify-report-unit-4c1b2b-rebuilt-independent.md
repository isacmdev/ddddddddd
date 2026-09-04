# Verification Report — Rebuilt Unit 4C1B2b

**Change**: `remote-signal-integrity` — reconstructed SDD7 Unit `4C1B2b Effect Dedupe/Retry` only  
**Version**: SDD7  
**Mode**: Strict TDD, hybrid persistence, independent read-only verification  
**Branch / exact parent**: `feat/sdd7-4c1b2b-effect-dedupe-retry` / `c1dbba6f0fa6866cabaaca438919fe12b123e29a`  
**Candidate binary diff hash**: `41b699468b131077f98733c19d0d6d966a5a5a74`  
**Verdict**: **PASS WITH WARNINGS**  
**Commit gate**: **APPROVED FOR 4C1B2B COMMIT**

This report approves only reconstructed Unit 4C1B2b on approved A2. Unit 4C2, Unit 5, aggregate Unit 4 completion, and archive readiness remain excluded. Both prior Slice B FAIL reports and the A2 PASS report remain preserved and unchanged.

## Completeness

| Metric | Value |
|---|---:|
| In-scope CODE+TEST files | 2/2 |
| Slice B tests | 8/8 passing |
| Full `AntiTamperMonitorTests` | 78/78 passing |
| Cumulative SDD tasks | 9/14 checked |
| Deferred aggregate tasks | 5 (`4.1`–`4.3`, `5.1`–`5.2`) |

The unchecked tasks are intentionally deferred aggregate Unit 4/Unit 5 tasks, not omitted 4C1B2b implementation tasks. The cumulative change is not archive-ready.

## Three-Way Reconstruction Integrity

| Check | Independent evidence | Result |
|---|---|---|
| Exact A2 parent | `HEAD`, branch OID, and merge-base are `c1dbba6f...`; sole parent is `4a6751b...` | ✅ |
| No history mutation | Branch reflog contains only creation from `c1dbba6f`; no merge/rebase/cherry-pick commit exists | ✅ |
| A2 commit identity | `c1dbba6f` is `fix(service): retain late integrity faults`, with the expected 36 additions/9 deletions in the two AntiTamper files | ✅ |
| A2 source preservation | Current-vs-old-final-B comparison differs only by A2: per-instance removal-gap seam, generation passed to `CompleteAdmissionAsync`, inline `EffectFault` publication before completion/removal/seam, cancellation exclusion, and removal signal | ✅ |
| A2 tests preservation | Post-removal exact-fault test, cancellation exclusion test, and concurrent-start Stop fault expectation are the only intentional test deltas from final old B | ✅ |
| Preserved B patch | External binary patch SHA-256 independently recomputed as `74b62677e6d21f1cf746dc69e3c4adaf5de1881e18b024841f40da37365bb065` | ✅ |
| B-owned equivalence | Old final B worktree versus rebuilt worktree has no B-region or B-test delta; the complete diff consists only of the A2-owned changes above | ✅ |

No B invariant, test, retry edge, shape field, exact key assertion, or durable persist-then-throw proof was dropped or weakened during reconstruction.

## Build and Runtime Evidence

All retained gates ran sequentially in fresh processes. After the initial Service test build, test commands used `--no-restore --no-build`; the five build gate used `--no-restore`. The two coverage hosts were separate clones with independent restore/build/output trees and independently applied candidate hash `41b6994...`.

| Gate | Fresh result |
|---|---:|
| Service test build before focused execution | PASS, 0 errors |
| Slice B exact filter | 8/8 passed |
| Full AntiTamper class (A/A2/B) | 78/78 passed |
| A2 post-removal + Stop-before-failure stress | 100/100 fresh processes; 200/200 tests passed |
| B duplicate/retry/barrier/shape/persist stress | 30/30 fresh processes; 240/240 tests passed |
| AntiTamper + Outbox + Integrity + Enforcement Unit 4 focus | 165/165 passed |
| Inherited late-failure stress | 20/20 fresh processes passed |
| Five builds: Domain, Service, Service.Tests, App.UI, App.UI.Tests | 5/5 passed, 0 errors |
| Full Service run 1 | 1,247/1,247 passed |
| Full Service run 2 | 1,247/1,247 passed |
| Full Service run 3 | 1,247/1,247 passed |
| Full App.UI | 192/192 passed |
| Physically isolated coverage host A | 1,247/1,247 passed; non-empty Cobertura |
| Physically isolated coverage host B | 1,247/1,247 passed; non-empty Cobertura |

The inherited duplicate xUnit-ID discovery notice and existing analyzer/package warning corpus remained visible. No current-candidate build, focused test, stress process, full suite, or retained coverage host failed or timed out.

### Coverage Artifacts

- Host A: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-rebuilt-independent-coverage-20260825-a\TestResults\independent-rebuilt-a\7619a99a-0623-4c63-a778-5cff8f5cf31d\coverage.cobertura.xml` (`4,899,206` bytes; SHA-256 `75002384e712cdf0fc3d2ad45e0ef613c4e457380268579ee48ab0071a84e351`).
- Host B: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-rebuilt-independent-coverage-20260825-b\TestResults\independent-rebuilt-b\ecd5b3f0-c588-4356-a07f-176dba1fe4b2\coverage.cobertura.xml` (`4,899,175` bytes; SHA-256 `8ebfa64933ae2b85cb3e15261687d21ba707fdfa0413ceecf2a0c0c96a70210a`).

Both hosts report `AntiTamperMonitor` at **98.96% line / 83.54% branch**. A2 `CompleteAdmissionAsync` is 100% line/branch: cancellation line 256 has 36 hits and non-cancellation publication/seam line 257 has 102 hits. B `ValidateAdmission` is 100% line/branch; pending none/mismatch, scope conflict, and full-shape conflict have positive hits. Reaction commit/fault, notification commit/fault, pending transition/clear, exact retry, progress newer/older/equal, and persist-then-throw notification paths all have positive hits. The unrelated authoritative-recovery effect branch remains uncovered.

## Behavioral Compliance Matrix

| Invariant / scenario | Runtime and static evidence | Result |
|---|---|---|
| Strict O(1) progress per generation | exactly nullable `ReactionProgress`, nullable `NotificationProgress`, and at most one nullable `PendingRetry`; no B collection/history/cache/window/durable state | ✅ COMPLIANT |
| Existing gate is sole serializer | all gate-time validation/effect/commit runs under existing `Generation.Gate`; no second owner/gate/task/queue/channel/drainer | ✅ COMPLIANT |
| Same-decision per-domain dedupe | `SameDecision_IsConsumedOncePerEffectDomainAfterGateRecheck`; 30-process B stress | ✅ COMPLIANT |
| Commit only after collaborator success | progress assignments follow successful awaited calls; failure paths assign pending retry then rethrow | ✅ COMPLIANT |
| Reaction failure retries both domains | exact retry runs reaction again and notification once | ✅ COMPLIANT |
| Notification failure retries exact notification only | committed reaction is skipped; exact notification key is attempted twice | ✅ COMPLIANT |
| Pending retry precedes gate release | catch assigns `PendingRetry`; outer `finally` releases the gate afterward | ✅ COMPLIANT |
| Non-exact work cannot overtake | queued/newer/older/different shape throws before effects while pending; exact retry alone advances | ✅ COMPLIANT |
| Explicit clear/transition permits progress | reaction retry clears after full success; notification failure transitions to notification domain; exact notification retry clears | ✅ COMPLIANT |
| Full shape/scope conflict before effects | shape includes scope, epoch, sequence, reaction key/kind, notification presence/key/kind; null→notification and scope tests pass | ✅ COMPLIANT |
| Real SQLite persist-then-throw | public AntiTamper flow + real `OutboxManager`; exact key, one physical row, reaction once, blocked newer, exact retry, then clear | ✅ COMPLIANT |
| Reset/null/distinct-newer/stale-completed behavior | newer/reaction-only and failed-older generation-reset tests; progress comparisons have runtime hits | ✅ COMPLIANT |
| Exact keys remain opaque | ordinal structural equality only; no parse/reconstruct/normalize/log path added | ✅ COMPLIANT |
| A2 retained late fault and cancellation | post-removal and Stop-before-failure stress 100/100; cancellation branch hit; exact object identity assertions pass | ✅ COMPLIANT |
| Slice A and prior Unit 4 regressions | AntiTamper 78/78, Unit 4 focus 165/165, Service x3, App.UI, coverage x2 | ✅ COMPLIANT |
| Deferred scope excluded | no handler/outbox API/transport/schema/4C2/Unit5 CODE+TEST path changed | ✅ COMPLIANT |

**Scoped compliance summary**: 16/16 invariant rows compliant.

## Strict TDD Compliance

An independent detached clone at exact A2 `c1dbba6f` received the current B tests and only a minimum generation-flow `ExecuteDecisionAsync` compile seam. The seam used existing admission, `Generation.Gate`, `ExecuteDecisionChainAsync`, and A2 fault receipt; it contained no progress, pending, shape, dedupe, or durable logic.

| Cycle | RED reconstruction | Current GREEN | Classification |
|---|---|---|---|
| Same-decision dedupe | genuine behavioral RED: 8 reactions; diagnostic assertion-only rerun then showed 8 notifications | 1 reaction + 1 notification; 30-process stress | complete |
| Older-fault overtaking | exact-parent seam run failed because queued newer work threw no exception and overtook | pending barrier test passes | complete |
| Null-notification→notification conflict | exact-parent seam run failed `No exception was thrown` | conflict rejected before effects | complete |
| Same-generation scope conflict | exact-parent seam run failed `No exception was thrown` | conflict rejected before effects | complete |
| Reaction retry | no retained pre-production isolated behavioral RED | focused/current runtime green | truthful process debt |
| Notification-only retry | no retained pre-production isolated behavioral RED | focused/current runtime green | truthful process debt |
| Persist-then-throw | preserved candidate was already green; no RED fabricated | real SQLite safety proof green | safety/process debt |

The current active six-column B table is the rebuilt table at `apply-progress.md:586–593`. It truthfully records the same process debt. Older B branch/checkpoint tables and readiness statements are cumulative history and are superseded by the rebuilt section plus this report; both prior FAIL verdicts remain valid for their rejected candidates.

The A2 six-column table and exact seam-only `No exception was thrown` RED remain preserved in `apply-progress.md:388–394` and the approved A2 report. Current source ordering and 100-process runtime evidence independently confirm the A2 GREEN contract; this B verification does not rewrite A2 chronology.

**TDD compliance**: PASS WITH WARNINGS — all reconstructed B production-driving ordering/dedupe/shape REDs required by this gate were behaviorally reproduced; isolated reaction/notification retry and persist historical RED debt remains explicitly unclaimed.

### Test Layer Distribution

| Layer | Slice B tests | Files | Tools |
|---|---:|---:|---|
| Unit/component | 7 | 1 | xUnit, Moq, FluentAssertions |
| Integration | 1 | 1 | real `OutboxManager`, EF Core, SQLite |
| E2E | 0 | 0 | not configured |
| **Total** | **8** | **1** | |

### Changed File Coverage

| File | Line % | Branch % | Relevant uncovered area | Rating |
|---|---:|---:|---|---|
| `src/ControlParental.Service/AntiTamperMonitor.cs` | 98.96% | 83.54% | authoritative-recovery effect branch and defensive final `ShouldExecute` throw; required A2/B paths hit | ✅ Excellent |
| `tests/ControlParental.Service.Tests/AntiTamperMonitorTests.cs` | N/A | N/A | test assembly is not instrumented | ➖ N/A |

### Assertion Quality

**Assertion quality**: ✅ All eight B tests invoke production behavior. No tautology, ghost loop, orphan empty assertion, type-only proof, smoke-only assertion, or assertion-free path was found. Mock counts measure required externally observable at-most-once/retry behavior; the integration test additionally asserts the physical durable row and exact key.

### Quality Metrics

**Compiler/analyzers**: ✅ five builds, 0 errors; inherited warnings remain.  
**Type checking**: ✅ covered by successful C# builds.  
**Diff quality**: ✅ `git diff --check` exited 0; Git emitted only LF→CRLF notices.

## Static Correctness

| Requirement | Status | Evidence |
|---|---|---|
| Fault-safe A2 ordering | ✅ | `EffectFault.TrySetResult(exception)` precedes public/owned completion, synchronous removal, and the test seam |
| Cancellation exclusion | ✅ | separate `OperationCanceledException` catch never publishes `EffectFault` or enters the seam |
| B bounded state | ✅ | two immutable progress references plus one full-shape/domain pending token |
| Barrier and exact retry | ✅ | full shape is validated before collaborators; catches assign pending before rethrow/gate release |
| Commit after success | ✅ | reaction/notification progress writes occur only after successful awaited collaborator return |
| No deadlock/starvation owner | ✅ | one existing gate, no lock-held collaborator await, exact retry can clear/transition, generation replacement resets state |
| Exact shape and keys | ✅ | record equality and ordinal strings; no transformed key path |
| Durable edge without B durability | ✅ | real Outbox dedupe is exercised, while B stores no durable retry state |
| Regression and scope | ✅ | only two authorized files differ from A2; all broad runtime gates pass |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Preserve A2 single owner, gate, drain, seam, and fault receipt | ✅ | no semantic weakening or alternate owner |
| Keep B progress O(1) and generation-scoped | ✅ | no collection or cross-generation state |
| Validate complete immutable decision shape | ✅ | all required fields represented and conflicts precede effects |
| Preserve exact key forwarding and durable outbox dedupe | ✅ | exact key and one-row convergence proven |
| Keep 4C2 as future durability/restart owner | ✅ | no persistence/timing/restart state added |
| Exclude Unit 5 and API/transport/schema changes | ✅ | exact two-file CODE+TEST diff |

## Hygiene, Budget, and Drift

| Check | Result |
|---|---|
| CODE+TEST numstat | `114+12` production + `183+2` tests = **311/400** |
| Candidate hash | exact `41b699468b131077f98733c19d0d6d966a5a5a74` |
| Changed CODE+TEST paths | exactly `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` |
| Index | empty |
| `.codegraph` | absent; not generated during verification |
| OpenSpec | cumulative tree remains untracked/excluded; this report is the sole new verification artifact |
| Prior reports | A2 PASS and both B FAIL reports preserved unchanged |
| Deferred drift | no handler, outbox API, transport, schema, 4C2, or Unit 5 change |

## Recorded Non-Zero / Excluded Commands

1. The intended exact-parent duplicate RED failed with 8 reactions; the assertion-only diagnostic rerun failed with 8 notifications.
2. The intended exact-parent overtaking/null-notification/scope RED command failed 3/3 with `No exception was thrown`.
3. One preliminary coverage invocation accidentally executed in the assigned worktree after creating host A; it passed 1,247/1,247 but is excluded from the physical-isolation gate. Host A and Host B were then independently restored, built, executed, and retained as the two authoritative coverage hosts above.

No unexpected current-candidate failure, timeout, aborted test process, or hidden rerun occurred.

## Issues Found

### CRITICAL

None.

### WARNING

1. Historical RED evidence remains unavailable for isolated reaction-retry and notification-retry cycles; persist-then-throw is safety evidence. This is truthfully retained process debt, not a current functional/TDD blocker.
2. Existing analyzer/package warnings, duplicate xUnit-ID discovery notice, and dense one-line formatting remain inherited/readability debt. No error or changed-contract defect was found.
3. Tasks remain 9/14; this slice is commit-ready but the cumulative SDD change is not archive-ready.

### SUGGESTION

None within this read-only verification scope.

## Final Verdict

**PASS WITH WARNINGS**

The rebuilt candidate is an exact 311-line Slice B child of committed A2, preserves every A2 fault/cancellation/seam invariant, matches the preserved final B patch except for the required A2 context, closes duplicate/overtaking/shape/persist behavior with bounded state under the sole existing gate, reproduces the required behavioral REDs, and passes every fresh focused, stress, build, full-suite, App.UI, and two-host coverage gate. No functional, race, reconstruction, scope, budget, or current-runtime blocker remains; the only TDD debt is explicitly historical and non-fabricated.

**APPROVED FOR 4C1B2B COMMIT**
