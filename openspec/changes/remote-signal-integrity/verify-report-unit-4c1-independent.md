## Verification Report

**Change**: remote-signal-integrity — Unit 4C1 Ordering Synchronization
**Version**: N/A
**Mode**: Strict TDD (hybrid, partial work-unit gate)
**Verified base / HEAD**: `6c6668bbdb5eb2bc328cfe86c28f878a51149679`

### Scope

This report verifies only Unit 4C1. Unit 4C2 durability, restart/rehydration, monotonic timer ownership, agent-death convergence, and Unit 5 composition/receipts are excluded and are not scored as 4C1 failures.

### Completeness

| Metric | Value |
|---|---:|
| Global tasks total | 14 |
| Global tasks complete | 9 |
| Global tasks incomplete | 5 |
| Unit 4C1 CODE+TEST budget | 357/400 |
| Tracked changed CODE+TEST files | 3 |

Tasks 4.1–4.3 intentionally remain unchecked because they aggregate 4C1, 4C2, and final Unit 4 approval. Their state is recorded but is not used as a blocker for this narrower gate.

### Build & Tests Execution

**Build**: ✅ Passed

Sequential `--no-restore` portable-PDB builds passed for Domain, Service, Service tests, App.UI, and App.UI tests with zero errors. The existing analyzer warning corpus remains.

**Tests**: ✅ All executed tests passed

| Gate | Result |
|---|---:|
| `IntegrityVerdictHandlerTests` | 30/30 |
| `IntegrityRuntimePathTests` | 9/9 |
| Combined Unit 4 focus | 126/126 |
| `LateNonCancellableFailure_IsObservableAndHasNoEffects` host A | 1/1 |
| `LateNonCancellableFailure_IsObservableAndHasNoEffects` host B | 1/1 |
| Full Service coverage host A | 1,215/1,215 |
| Full Service coverage host B | 1,215/1,215 |
| Full App.UI | 192/192 |

Both full Service hosts emitted the pre-existing duplicate-ID discovery message for `HttpResponseClassifierTests`, but reported 1,215 passed, zero failed, and zero skipped.

**Coverage**: No aggregate threshold configured.

- Host A: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1-independent-a/46e2345e-a3e2-4690-aa3a-0f30837c7243/coverage.cobertura.xml`
- Host B: `tests/ControlParental.Service.Tests/TestResults/coverage-unit4c1-independent-b/e19b3d76-7a35-4e45-a393-ff6fe72b943b/coverage.cobertura.xml`

### Spec Compliance Matrix

| Requirement | Scenario | Runtime evidence | Result |
|---|---|---|---|
| Definitive evidence authority | Equal timestamp conflicting verdicts | `EqualTimestamps_AreOrderedByAcceptanceAndAcceptedDuplicatesCount` | ✅ COMPLIANT for sequential equal-timestamp behavior |
| Definitive evidence authority | Unknown/transient response preserves state | Handler preservation test plus runtime non-definitive theory | ✅ COMPLIANT |
| Deterministic ingress authority | Concurrent mixed order | No concurrent handler acceptance test exists | ❌ UNTESTED |
| Deterministic ingress authority | Stale callback and callback fault | `ReentrantCallbackRunsAfterCommitAndFaultRemainsObservable` proves reentrancy/fault propagation only; no older-epoch suppression test | ⚠️ PARTIAL |
| Exact revoked escalation | First through fourth revoked | Handler and production runtime-path tests | ✅ COMPLIANT |
| Exact revoked escalation | Before/exact deadline and trust cancellation | Exact boundary passes; no test starts a pending deadline, accepts pre-deadline trust, and proves cancellation | ⚠️ PARTIAL |
| Exact revoked escalation | Three-trust one-shot recovery and revoked reset | First recovery is tested; repeated trust after recovery and revoked-reset behavior are not | ⚠️ PARTIAL |

**Compliance summary**: 3/7 in-scope scenarios fully compliant. Two required concurrency/epoch dimensions have no passing covering scenario test.

### Correctness (Static Evidence)

| Requirement | Status | Notes |
|---|---|---|
| Serialized state transition | ⚠️ Partial | `HandleVerdictCore` runs under `stateGate`, and collaborator work is moved outside that lock. |
| Monotonic ingress sequence | ❌ Missing | No ingress-sequence field or transition result exists. Lock acquisition orders mutation internally, but the required monotonic sequence is neither assigned nor carried. |
| Every transition advances an epoch | ❌ Incorrect | `stateEpoch` changes only on trust, third revoked, and deadline commit (`IntegrityVerdictHandler.cs:236,327,383`); first/second revoked transitions do not advance it. |
| Epoch-bound stale-effect suppression | ❌ Missing | `stateEpoch` is never read or attached to a callback/notification. `HandleVerdict` invokes `onReaction` directly after unlock (`166–173`). |
| Ordered one-shot external effects | ❌ Incorrect | Concurrent callers can independently enter `FlushNotifications` and invoke callbacks after later state commits. There is no effect-drain ownership or epoch validation (`387–400`). |
| One-shot authoritative recovery | ❌ Incorrect | At threshold, the handler returns recovery without clearing `degraded`, latching recovery, or resetting the trust count (`238–248`), so every later trust emits another authoritative recovery. |
| Exact deadline transition | ✅ Implemented | `CommitDeadlineIfDueLocked` commits once at equality or later. |
| No collaborator work under state lock | ✅ Implemented for verdict path | Clock, outbox, and callback execution occur outside `stateGate`. |

### Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| Explicit `IngressSequence` inside serialized acceptance gate | ❌ No | Serialization exists, explicit sequence does not. |
| Epoch carried into effects; stale effects suppressed | ❌ No | Epoch is write-only state. |
| Callbacks/outbox outside state lock with observed work | ⚠️ Partial | They run outside the lock and synchronously; notification exceptions are swallowed after debug logging, and ordering is not protected across concurrent drains. |
| No ownerless production timer in 4C1 | ✅ Yes | `EvaluateDeadline` is a pure explicit seam; 4C2 ownership remains excluded. |
| 4C1 remains within review budget | ✅ Yes | 357/400 CODE+TEST lines. |

### TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Unit 4C1 six-column evidence exists in `apply-progress.md`. |
| Test files exist | ✅ | Both claimed changed test files exist and execute. |
| RED evidence | ⚠️ | Compile-RED chronology is documented, but raw RED output is not independently available in the worktree. |
| GREEN confirmed | ✅ | All reported executable gates rerun green. |
| Triangulation adequate | ❌ | Apply evidence claims concurrent ingress and stale epoch coverage, but `IntegrityVerdictHandlerTests.cs` contains neither a concurrent acceptance test nor an older-epoch suppression test. |
| Safety net | ✅ | Focused, combined, full, two-host, and App.UI gates passed. |

**TDD Compliance**: 3/5 substantive checks passed; required scenario coverage is incomplete.

### Test Layer Distribution

| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit/component | 30 | 1 | xUnit, FluentAssertions, Moq |
| Integration/runtime path | 9 | 1 | xUnit, real monitor/backend/enforcement path with controlled ports |
| E2E | 0 | 0 | Not applicable to this slice |
| **Total** | **39** | **2** | |

### Changed File Coverage

| File | Line % | Branch % | Uncovered lines | Rating |
|---|---:|---:|---|---|
| `src/ControlParental.Service/IntegrityVerdictHandler.cs` | 98.06% | 96.25% | 179–180, 279–280, 436–440 | ✅ Excellent |

High structural coverage does not substitute for the absent concurrent/stale-epoch scenarios.

### Assertion Quality

| File | Line | Assertion | Issue | Severity |
|---|---:|---|---|---|
| `IntegrityRuntimePathTests.cs` | 88 | Compare a snapshot to an immediate reread | No production operation occurs between expected and actual; the assertion cannot prove recovery or preservation behavior | WARNING |

No tautological literal, ghost-loop, or smoke-only assertions were found. The larger quality problem is missing race/epoch assertions rather than weak assertions in the existing handler tests.

### Quality Metrics

**Analyzer/type build**: ✅ Zero errors; existing warnings only.
**Diff check**: ✅ `git diff --check` passed.
**Dependency/config drift**: ✅ No tracked project, lock, dependency, or configuration changes.
**Workspace cleanliness**: ⚠️ `.codegraph/` is unexpectedly present and untracked; cumulative OpenSpec artifacts are also untracked. No staged files exist.

### Issues Found

**CRITICAL**

1. The required monotonic ingress sequence and epoch-bearing effect model are absent. `stateEpoch` is never read, so stale callbacks/notifications cannot be suppressed.
2. Required concurrent mixed-order and stale-callback scenarios have no passing runtime tests. Apply evidence incorrectly claims both are covered.
3. Recovery is not one-shot: after the third trust, subsequent trusts continue returning `IsAuthoritativeRecovery = true` because no recovery latch/state reset is committed.
4. Concurrent post-lock effect drains are not serialized or epoch-validated, so older callbacks/effects can execute after newer observations commit.

**WARNING**

1. Pre-deadline trust cancellation and revoked-reset-after-degradation are implemented or implied but not covered as complete scenarios.
2. The runtime-path test contains a no-op snapshot assertion at line 88 after removal of its former recovery operation.
3. Unexpected untracked `.codegraph/` content prevents a strictly clean-worktree claim.

**SUGGESTION**: None beyond resolving the blocking 4C1 requirements with deterministic barrier tests rather than sleeps/stress loops.

### Verdict

**FAIL**

Builds and the existing 1,215-test Service safety net are green, but Unit 4C1 is not commit-ready: core ingress-sequence, epoch-bound stale-effect suppression, concurrent scenario evidence, and one-shot recovery requirements are missing or incorrect.
