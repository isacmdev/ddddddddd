# Verification Report

**Change**: `remote-signal-integrity` — Unit 4C1B1 Pure Decision Contract remediation  
**Version**: uncommitted slice over `d73834c7de5c1f9d3f9852d50e02f350dec29a2f`  
**Branch**: `feat/sdd7-4c1b-ordered-effects`  
**Mode**: Strict TDD, authoritative read-only re-verification  
**Verdict**: **PASS WITH WARNINGS**  
**Commit readiness**: **APPROVED FOR THE 4C1B1 SLICE ONLY**

## Scope

This report verifies only the remediated 4C1B1 pure-decision boundary: checked decision identity allocation, canonical identity scoping, separate reaction/notification idempotency metadata, local acceptance time, handler purity, immediate sync/async compatibility, existing AntiTamper identity propagation, regression safety, and removal of the rejected mailbox.

It does not approve 4C1B2 effect admission/FIFO/fault ownership, 4C2 persistence/deadline recovery, Unit 5, or archive readiness. Global tasks remain intentionally `9/14`; the five unchecked aggregate tasks span those deferred slices and are not represented as completed by this approval.

## Completeness

| Metric | Value |
|---|---:|
| Global tasks | 14 |
| Complete | 9 |
| Deferred outside this slice | 5 |
| In-scope remediation groups | 8/8 |
| Changed tracked CODE+TEST files | 4 |
| CODE+TEST budget | **397/400** (`231` additions + `166` deletions) |

## Build & Tests Execution

All commands were executed sequentially with `--no-restore`. Five portable-PDB builds passed with zero errors: Domain, Service, Service.Tests, App.UI, and App.UI.Tests. Existing analyzer/package warnings remain.

| Gate | Result |
|---|---:|
| Remediation invariant matrix | ✅ 5/5 |
| `IntegrityVerdictHandlerTests` | ✅ 35/35 |
| `IntegrityRuntimePathTests` | ✅ 9/9 |
| `AntiTamperMonitorTests` | ✅ 55/55 |
| Combined Unit 4 matrix | ✅ 130/130 |
| Inherited late-failure hosts A/B | ✅ 1/1 and 1/1 |
| Full Service hosts A/B | ✅ 1,220/1,220 and 1,220/1,220 |
| Full App.UI | ✅ 192/192 |
| Physically isolated coverage hosts A/B | ✅ 1,220/1,220 and 1,220/1,220 |

The Service runs emitted the existing duplicate xUnit-ID discovery notice. Executed counters reported zero skipped tests.

### Physical isolation and coverage

Fresh copied workspaces gave each coverage host separate source, `bin`, `obj`, testhost, and results trees:

- `C:/Users/Usuario/AppData/Local/Temp/opencode/verify-unit4c1b1-authoritative-isolated-a-20260824/results/83627084-47ae-44b7-b364-f54f89280bfd/coverage.cobertura.xml` — `4,864,369` bytes.
- `C:/Users/Usuario/AppData/Local/Temp/opencode/verify-unit4c1b1-authoritative-isolated-b-20260824/results/379ceecb-d2e3-4f6c-8a9c-b01010d95182/coverage.cobertura.xml` — `4,864,383` bytes.

Both artifacts report identical relevant metrics.

## Spec Compliance Matrix

| Requirement / scenario | Covering evidence | Result |
|---|---|---|
| Every accepted pure call receives one monotonic sequence and epoch | `PureDecisionCallsMakeImmediateProgressAndPreserveCancellationBoundary`; ordinary handler matrix | ✅ COMPLIANT |
| Equal timestamps and duplicate payloads remain distinct observations | `EqualTimestamps_AreOrderedByAcceptanceAndAcceptedDuplicatesCount` | ✅ COMPLIANT |
| Cancellation before acceptance allocates no identity | `PureDecisionCallsMakeImmediateProgressAndPreserveCancellationBoundary` | ✅ COMPLIANT |
| Counter exhaustion fails before counter or policy commit | `DecisionCounterOverflowFailsBeforeCommit`; covered allocation-before-mutation source path | ✅ COMPLIANT |
| Identity scopes cannot collide for equal local counters | `DecisionsForDifferentCanonicalScopesHaveDifferentReactionKeys` | ✅ COMPLIANT |
| Reaction and notification effects have separate keys; absent notification has no key | `DecisionKeysSeparateReactionAndNotificationEffects` | ✅ COMPLIANT |
| `ObservedAt` is local acceptance time, not backend/audit time | `PureDecisionCallsMakeImmediateProgressAndPreserveCancellationBoundary` covers backend verdict and local failure with a deliberately different timestamp | ✅ COMPLIANT |
| Handler is pure and compatibility collaborators are never invoked | `HandleVerdict_ThirdRevoked_IsPureAndDoesNotInvokeEffects`; source inspection | ✅ COMPLIANT |
| AntiTamper forwards canonical `DeviceId` scope without taking deferred B2 ownership | Covered `PerformBinaryIntegrityCheckAsync` lines 575–590 plus runtime/AntiTamper suites | ✅ COMPLIANT |
| Non-definitive outcomes preserve effective enforcement | `CanonicalAuthority_NonDefinitiveCase_PreservesExactPhysicalSnapshot` theory and full runtime suite | ✅ COMPLIANT |

**Compliance summary**: **10/10 in-scope scenarios compliant**. Concurrent stale-effect suppression and notification delivery remain explicitly deferred to 4C1B2.

## Correctness (Static Evidence)

| Requirement | Status | Notes |
|---|---|---|
| Checked allocation | ✅ Implemented | `AllocateDecisionIdentityLocked` rejects either `long.MaxValue` and computes both next values before assigning counters or mutating policy state. |
| Immutable command metadata | ✅ Implemented | `VerdictDecision` carries identity scope and independent reaction/notification keys. |
| Key collision resistance | ✅ Implemented | Shape is `integrity/{scope}/integrity-binary/{epoch}/{sequence}/{effect}`; reaction action and notification use different terminal segments. |
| Acceptance time | ✅ Implemented | Verdict, local-failure, and deadline decisions read the injected local clock separately from backend/audit timestamps. |
| Pure boundary | ✅ Implemented | No mailbox, Channel, callback invocation, outbox/store/timer I/O, drainer, background task, or handler-owned resource exists. |
| AntiTamper scope | ✅ Implemented | Existing identity generation is checked before the decision and `identity?.DeviceId` is passed to both pure decision paths. |
| Deferred-scope isolation | ✅ Implemented | No B2 command lease/effect delivery, 4C2 persistence, Program/composition, retry, or Unit 5 change was added. |

## Coherence (Design)

| Decision | Followed? | Notes |
|---|---|---|
| Pure synchronized handler | ✅ Yes | Narrow state lock and immutable return command; effects remain outside the handler. |
| One sequence and epoch per accepted call | ✅ Yes | Pre-call cancellation is the only non-accepting async path. |
| Identity/effect idempotency separation | ✅ Yes | Device scope, stable integrity issue segment, epoch, sequence, and effect kind are present. |
| Sync/async compatibility | ✅ Yes | Async is immediate `Task.FromResult` or exact-token `Task.FromCanceled`. |
| B2/4C2 boundary | ✅ Yes | No deferred ownership is claimed. |

## TDD Compliance

| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | Apply progress contains the six-column pure-contract table and remediation chronology. |
| Genuine behavioral RED | ✅ | Restored 4C1A invoked the deprecated callback three times before the pure-boundary implementation. |
| New identity API RED classification | ⚠️ | Identity fields and overflow seed were honestly classified as compile seams; no behavioral RED was fabricated. |
| GREEN confirmed | ✅ | All focused, regression, inherited-safety, and isolated coverage gates passed freshly. |
| Triangulation | ✅ | Distinct identity, effect, timestamp, cancellation, normal progression, and both overflow dimensions are covered. |
| Safety net | ✅ | Service twice, App.UI, late-failure twice, and isolated coverage twice passed. |

**TDD compliance**: 5/6 fully passed; one honest compile-seam/process-evidence warning.

## Test Layer Distribution

| Layer | Tests | Files | Tool |
|---|---:|---:|---|
| Handler unit/component | 35 | 1 | xUnit |
| Runtime integration | 9 | 1 | xUnit + real backend/identity/AntiTamper/enforcement/store path |
| AntiTamper lifecycle/component safety net | 55 | 1 unchanged | xUnit |
| E2E | 0 | 0 | Outside slice |

## Changed File Coverage

| File | Line | Branch | Uncovered lines | Rating |
|---|---:|---:|---|---|
| `IntegrityVerdictHandler.cs` | 98.80% aggregate; main class 100% | 97.37% | record boilerplate L89–90, L99 | ✅ Excellent |
| `AntiTamperMonitor.cs` | 93.46% aggregate; main class 99.15% | 83.40% aggregate; main class 87.75% | inherited exception/rare paths including L393–394, L412–413, L496–531, L608–609, L735–739 | ⚠️ Acceptable |
| `IntegrityVerdictHandlerTests.cs` | N/A | N/A | Test assembly not instrumented | ➖ |
| `IntegrityRuntimePathTests.cs` | N/A | N/A | Test assembly not instrumented | ➖ |

New decision allocation, overflow, key construction, local-failure metadata, and AntiTamper identity-forwarding lines all have positive hits in both artifacts.

## Assertion Quality

The modified tests call public/internal production seams and assert observable reaction, sequence, epoch, exact cancellation token, key separation, notification absence/presence, local acceptance time, overflow exception, and durable runtime state. No tautology, ghost loop, smoke-only assertion, or production-free assertion was found.

**Assertion quality**: ✅ All assertions verify real behavior.

## Quality, Workspace, Budget, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / merge-base | ✅ exact requested branch and `d73834c...` |
| Staged files | ✅ none |
| Changed tracked paths | ✅ exactly four authorized source/test files |
| CODE+TEST budget | ✅ `397/400` |
| `git diff --check` | ✅ exit 0; LF→CRLF notices only |
| Failed-mailbox patch hash | ✅ `D36FEE043D35E28B71BF3F473B97A922578D4456846AC6417DF43D8FB0C640D9` |
| Mailbox remnants | ✅ none |
| `.codegraph/` | ✅ absent |
| Type/build checks | ✅ five builds, zero errors; existing warnings |

## Issues Found

### CRITICAL

None within the 4C1B1 slice.

### WARNING

1. Global tasks remain `9/14`; this slice approval is not full-change completion or archive readiness.
2. Overflow and newly introduced identity fields use an honestly reported compile-seam RED rather than retained behavioral RED output. Current behavior is covered and green.
3. The idempotency path uses the stable semantic segment `integrity-binary` rather than a documented serialization of the structural Unit4A `IssueKey` record. It is deterministic and collision-safe for this single issue, but B2 should consume the exact emitted contract rather than reconstructing it.
4. Existing analyzer/package warnings and the duplicate xUnit-ID discovery notice remain outside this slice.

### SUGGESTION

Document the stable idempotency-key segment before 4C1B2 consumes it.

## Verdict

**PASS WITH WARNINGS**

All in-scope implementation, runtime, regression, budget, drift, and fresh physical-isolation gates are closed. The prior overflow, identity collision, effect-kind collision, timestamp ambiguity, and coverage-isolation blockers are remediated. Unit 4C1B1 is approved as an autonomous commit boundary; deferred 4C1B2/4C2/Unit5 work remains unapproved.

**APPROVED FOR 4C1B1 COMMIT ONLY.**
