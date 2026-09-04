# Verification Report

**Change**: `remote-signal-integrity` — SDD7 Unit 4C1B1 Async Mailbox Core  
**Branch**: `feat/sdd7-4c1b-ordered-effects`  
**HEAD / requested base / merge-base**: `d73834c7de5c1f9d3f9852d50e02f350dec29a2f`  
**Mode**: Strict TDD, independent read-only verification  
**Verdict**: **FAIL**  
**Commit readiness**: **NOT APPROVED FOR 4C1B1 COMMIT**

## Scope and Exclusions

This report judges only autonomous 4C1B1: async verdict admission, accepted ingress sequencing, the FIFO mailbox owner, synchronous compatibility/reentrancy, cancellation and fault ownership, disposal drain, and the AntiTamper async seam.

It does **not** judge deferred 4C1B2 stale-before-stage/late-admitted effect-token barriers, atomic epoch leases, or concurrent third-revoked notification/outbox ordering. It also excludes 4C2 durability/deadline/restart/clock/agent-death ownership and Unit 5 composition.

Global tasks remain intentionally `9/14`; unchecked aggregate tasks 4.1–4.3 and deferred Unit 5 tasks are not counted as B1 failures.

## Completeness

| Metric | Value |
|---|---:|
| Global tasks | 14 |
| Complete | 9 |
| Intentionally deferred | 5 |
| Intended tracked CODE+TEST files | 4/4 |
| CODE+TEST budget | **400/400** (`351` additions + `49` deletions) |
| B1 acceptance groups fully compliant | **3/8** |

## Build and Runtime Evidence

All retained commands used `--no-restore` and ran sequentially. Results are under `C:/Users/Usuario/AppData/Local/Temp/opencode/verify-unit4c1b1-independent-20260824`.

| Gate | Result |
|---|---:|
| Five portable-PDB builds: Domain, Service, Service.Tests, App.UI, App.UI.Tests | ✅ 5/5, zero errors; existing warning corpus |
| Exact retained B1 mailbox/cancel/dispose/fault/reentrant matrix | ❌ **6/7** |
| `IntegrityVerdictHandlerTests` | ✅ 38/38 in a separate process |
| `IntegrityRuntimePathTests` | ✅ 9/9 |
| `AntiTamperMonitorTests` | ✅ 55/55 |
| IntegrityChecker + Enforcement | ✅ 31/31 |
| Combined Unit 4 matrix | ✅ 133/133 |
| Dedicated inherited late-failure hosts A/B | ✅ 1/1 and 1/1 |
| Full Service default-parallel A/B | ✅ 1,223/1,223 and 1,223/1,223 |
| Full App.UI | ✅ 192/192 |
| Fresh full coverage A/B | ✅ 1,223/1,223 and 1,223/1,223; nonempty Cobertura |

### Required focused failure

The exact seven-test B1 gate failed:

```text
IntegrityVerdictHandlerTests.AsyncMailbox_ReentrantSubmissionIsQueuedAfterCurrentEffect
Expected observations to be equal to {1L, 2L}, but {1L, 2L} contains 1 item(s) less.
IntegrityVerdictHandlerTests.cs:726
Result: 6 passed, 1 failed, 0 skipped
```

The same test happened to pass inside later handler/full-suite processes. That does not close the gate: the required deterministic reentrant FIFO behavior failed at runtime, and source inspection identifies the corresponding ownership race below.

### Coverage

Fresh artifacts:

- `coverage-full-a/1317f1ea-c2bb-40a3-8a1d-cf2a92e2cac6/coverage.cobertura.xml` — 4,904,820 bytes.
- `coverage-full-b/9d23f3d1-2877-4e2f-b3a1-4eec9791f98d/coverage.cobertura.xml` — 4,904,788 bytes.

Both report `IntegrityVerdictHandler` line coverage `100%`, branch coverage `93%`, and `AntiTamperMonitor` line coverage `99.15%`, branch coverage `87.75%`. Positive source hits cover pre-accept true/false admission, sequence allocation, drainer start, FIFO commit/effect/completion, reentrant returns, fault continuation, disposal, and the AntiTamper async seam. The OCE catch in `DrainAsync` has zero hits, consistent with the retained accepted-cancellation test expecting success rather than exercising accepted cancellation at an outbox await.

The fresh coverage runs had unique result/Cobertura directories but reused the normal compiled bin/obj tree. A fresh attempt to create unique bin/obj under `coverage-isolated-a` failed before tests because globally redirected intermediate paths made the test project seek missing project-reference assemblies under the isolated `obj/ref` path. Therefore the specifically requested **fresh physically isolated bin/obj coverage-host gate is not closed**. Prior apply artifacts are not substituted for independent fresh evidence.

## B1 Behavioral Compliance Matrix

| # | Requirement | Runtime/static evidence | Result |
|---:|---|---|---|
| 1 | Async API owns non-reentrant effects; sync compatibility is deadlock-safe; reentrant nested fault has an explicit owner | Non-reentrant callback/outbox fault identity passes. Exact reentrant FIFO test failed. A nested synchronous item returns only a placeholder; if its later callback/outbox faults, `DrainAsync` faults its private completion and swallows the fault from the drain, leaving no caller/owner to observe it. | ❌ FAILING |
| 2 | Atomic accepted sequence, one owner, no lost wakeup/double drainer/stall, basic FIFO starts/completions | Sequence/FIFO happy path passes. The empty-queue handoff is not atomic with owner release: `DrainAsync` can dequeue `null` while `drainTask` is still incomplete; a concurrent submit then enqueues without starting a new drain, after which the old drain returns. This permits a stalled item/lost wakeup. | ❌ FAILING |
| 3 | Pre-accept exact-token cancellation; post-accept cancellation cannot abandon effect and has the designed caller outcome | Pre-accept exact-token/contiguous-sequence test passes. After acceptance, the caller token is forwarded into `FlushNotifications`; cancellation can abort an accepted outbox effect and cancel completion. The retained accepted-cancellation test covers only a no-outbox reaction and expects successful completion, contrary to design text requiring caller cancellation to be reported while accepted effect ownership continues. | ❌ FAILING |
| 4 | Matching callback/outbox fault identity, coherent commit, later FIFO continuation | Callback and outbox fault tests pass with exact exception identity and later progress for non-reentrant callers. Nested sync fault ownership remains a failure under requirement 1. | ✅ COMPLIANT (non-reentrant) |
| 5 | Atomic admission close, drain accepted work exactly once, no effect after Dispose returns, stable repeated/concurrent/reentrant Dispose | Retained disposal happy path passes. In synchronous first-start admission, `drainStarting` is set under the gate but `drainTask` is assigned only after unlocking; concurrent Dispose can capture `null` and return before the accepted drain/effect runs. | ❌ FAILING |
| 6 | No relevant lock across user work/await; all completions observed; RCSA; no unowned drainer/task | State/mailbox locks are not held over handler callback/outbox work; item TCS uses RCSA; no `Task.Run`/`async void` was added. The nested sync completion fault is unowned, and the lost-wakeup owner transition is unsafe. | ❌ FAILING |
| 7 | AntiTamper awaits async seam with generation/cancellation context and no lifecycle lock across await | `PerformBinaryIntegrityCheckAsync` awaits `HandleVerdictAsync` inside admitted generation work, restores `callbackGeneration` in `finally`, forwards the operation token, and performs no lifecycle-lock await. Deferred B2 atomic effect-lease behavior is not judged. | ✅ COMPLIANT |
| 8 | 4C1A regression and no deferred drift | Handler/runtime/AntiTamper/checker/enforcement, full Service twice, App.UI, and late-failure hosts pass. Diff is limited to the four intended tracked files; no Program/store/durable-owner drift. | ✅ COMPLIANT |

## Correctness (Static Evidence)

| Area | Status | Notes |
|---|---|---|
| Admission sequence | ✅ Basic path | Checked `long` guard and increment occur under `mailboxGate` after cancellation/disposal acceptance checks. |
| Drainer ownership / empty race | ❌ Defect | Queue-empty observation and drain-owner release are not one atomic mailbox-gate transition. |
| Reentrant progress | ⚠️ Partial | Nested work is queued without self-wait, but deterministic completion failed and nested faults have no observer. |
| Cancellation ownership | ❌ Defect | Accepted token can cancel outbox work; retained test does not exercise that path or the designed caller cancellation outcome. |
| Disposal | ❌ Defect | Sync-start publication race allows Dispose to miss accepted drain ownership. |
| Lock boundary | ✅ | `stateGate` and `mailboxGate` are not held while invoking the callback or outbox await bridge. |
| AntiTamper seam | ✅ B1 scope | Production awaits the async handler within existing generation ownership. |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| 4C1B1 autonomous mailbox boundary | ❌ | Lost-wakeup, reentrant ownership, cancellation, and disposal contracts are not closed. |
| Async production API | ⚠️ Partial | Primary async verdict API exists; non-reentrant terminal ownership works, reentrant nested fault ownership does not. |
| Cancellation ownership | ❌ | Accepted notification work remains cancellable by the caller token and the retained test asserts success after cancellation. |
| 4C1B2/4C2 exclusion | ✅ | No deferred stale-token race, durable timing, store, restart, or Unit 5 claim is used as a B1 failure. |

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| Six-column evidence present | ⚠️ Partial | A broad acceptance/FIFO row exists, plus prose closure; it is not complete per-behavior evidence for all final retained B1 cases. |
| Genuine behavioral RED — accepted cancellation/disposal | ✅ | Apply progress records real behavioral failures followed by changes. |
| Genuine behavioral RED — mailbox ordering/reentrancy/fault | ❌ | Mailbox API/metadata evidence is compile-only; fault/reentrancy rows do not retain genuine behavioral RED evidence. Compile-only seams are not counted as behavioral RED. |
| Pre-accept cancellation | ➖ Triangulation | Honest green test-only correction; no production RED claimed. |
| GREEN confirmed | ❌ | Exact required B1 gate is 6/7, with reentrant ordering failure. |
| Safety net | ✅ | Broad regressions and two fresh coverage executions pass. |

**Strict TDD conclusion**: required behavioral RED→GREEN evidence is incomplete, and current GREEN is disproved by the exact focused failure.

## Test Layer Distribution

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Handler unit/component | 38 | 1 | Mailbox, policy, cancellation, disposal, and fault behavior |
| AntiTamper/runtime integration-style | 64 | 2+ | Generation ownership and real runtime path |
| E2E | 0 | 0 | Outside B1 scope |

## Assertion Quality

The seven retained B1 tests call production code and assert observable sequence, effect, exception identity, cancellation token, disposal, and continuation behavior. No tautology, ghost loop, smoke-only assertion, or production-free assertion was found.

**Assertion quality**: ✅ No trivial assertions found. Coverage gaps are behavioral-scenario gaps, not meaningless assertions.

## Quality, Workspace, Budget, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / requested base / merge-base | ✅ exact |
| Staged files | ✅ none |
| Tracked source/test changes | ✅ exactly the four intended files |
| CODE+TEST budget | ✅ exact `400/400` |
| `git diff --check` | ✅ exit 0; LF→CRLF notices only |
| `.codegraph/` | ✅ absent |
| Deferred 4C1B2 / 4C2 / Unit 5 drift | ✅ none found |
| Readability | ⚠️ Dense one-line lifecycle tests and compact production statements remain difficult to review at the hard budget ceiling; no readability edit was made. |
| Linter/type checker | ✅ Builds have zero errors; existing analyzer/package warnings remain. |

## Issues

### CRITICAL

1. **Required runtime test failure**: the exact B1 matrix failed reentrant FIFO effect ordering (`6/7`).
2. **Mailbox lost-wakeup race**: queue-empty observation is not atomically coupled to drainer ownership release, so an accepted item can be stranded.
3. **Nested synchronous fault is unowned**: reentrant sync submission discards the matching completion; per-item catch prevents the owning drain from surfacing the fault.
4. **Accepted cancellation can abandon an outbox effect**: caller cancellation is forwarded into notification work after acceptance; retained coverage does not exercise this path and caller outcome is inconsistent with the design statement.
5. **Dispose can miss synchronously starting accepted work**: `drainTask` publication occurs after releasing `mailboxGate`, allowing Dispose to return before the accepted effect.
6. **Strict-TDD gate not met**: no genuine retained behavioral RED→GREEN evidence for mailbox ordering/reentrancy/fault, and current focused GREEN fails.
7. **Fresh physical isolation gate incomplete**: both fresh full coverage executions passed, but the requested unique bin/obj isolation was not achieved; the isolated build attempt failed before test execution.

### WARNING

1. The full handler/full Service runs can pass despite the exact reentrant failure, confirming timing-sensitive nondeterminism rather than deterministic contract closure.
2. Existing package/analyzer warnings and duplicate xUnit-ID discovery notice remain outside this slice.
3. The exact 400-line ceiling has produced compressed source/tests that increase review cost.

### SUGGESTION

None in this read-only verification. Corrections require a new apply/remediation pass.

## Final Verdict

**FAIL**

4C1B1 is not autonomously commit-ready. Broad regressions and coverage are green, but a required focused behavioral test fails and source inspection confirms in-scope lost-wakeup, nested-fault ownership, accepted-cancellation, and disposal-publication defects. Deferred 4C1B2/4C2 behavior was not used to reach this verdict.

**NOT APPROVED FOR 4C1B1 COMMIT.**
