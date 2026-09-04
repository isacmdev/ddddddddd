# Verification Report

**Change**: `remote-signal-integrity` — Unit 4C1B1 Pure Decision Contract  
**Branch**: `feat/sdd7-4c1b-ordered-effects`  
**HEAD / requested base / merge-base**: `d73834c7de5c1f9d3f9852d50e02f350dec29a2f`  
**Mode**: Strict TDD, independent read-only verification  
**Verdict**: **FAIL**  
**Commit readiness**: **NOT APPROVED FOR 4C1B1 COMMIT**

## Scope and Exclusions

This report judges only the redesigned autonomous 4C1B1 pure decision contract: handler purity, immutable decision identity, sync/async compatibility, no-effect constructor compatibility, minimal existing AntiTamper reaction consumption, runtime alignment, 4C1A preservation, and removal of the rejected mailbox.

It does **not** judge deferred 4C1B2 stale effect leases, notification delivery/FIFO/fault ownership, or AntiTamper decision-token admission. It also excludes 4C2 durability/deadline restart/clock/agent-death ownership and Unit 5 compatibility/composition.

Global tasks remain intentionally `9/14`; unchecked aggregate tasks 4.1–4.3 and Unit 5 tasks are not counted as B1 failures.

## Reset Chronology and Failed Candidate Evidence

| Check | Result |
|---|---|
| Failed mailbox patch exists outside repository | ✅ `C:/Users/Usuario/AppData/Local/Temp/opencode/sdd7-unit4c1b-failed-mailbox.patch` |
| SHA-256 | ✅ `D36FEE043D35E28B71BF3F473B97A922578D4456846AC6417DF43D8FB0C640D9` |
| Patch contents | ✅ exact rejected four-file mailbox candidate, `351` additions / `49` deletions |
| Restore chronology | ✅ apply progress records all four candidate files restored from `d73834c` before RED; current AntiTamper source/test remain byte-equivalent to base |
| Mailbox FAIL report | ✅ preserved unchanged as `verify-report-unit-4c1b1-independent.md` |
| Mailbox remnants in current handler | ✅ none: no mailbox gate/item/drainer/Channel/queue/effect-token/continuation/sync wait |

The historical reset itself cannot be replayed without destroying the current uncommitted pure candidate. The external patch hash, patch file list/stat, current base-equal AntiTamper files, apply chronology, and absence of mailbox symbols are mutually consistent with the claimed reset.

## Completeness

| Metric | Value |
|---|---:|
| Global tasks | 14 |
| Complete | 9 |
| Intentionally deferred | 5 |
| Changed tracked CODE+TEST files | 3 |
| CODE+TEST budget | **348/400** (`182` additions + `166` deletions) |
| Pure acceptance groups fully compliant | **5/8** |

## Build and Runtime Evidence

All retained commands used `--no-restore` and ran sequentially. Fresh evidence is under `C:/Users/Usuario/AppData/Local/Temp/opencode/verify-unit4c1b1-pure-independent-20260824`.

| Gate | Result |
|---|---:|
| Five portable-PDB builds: Domain, Service, Service.Tests, App.UI, App.UI.Tests | ✅ 5/5, zero errors; existing warnings |
| Pure decision focused matrix | ✅ 4/4 |
| `IntegrityVerdictHandlerTests` | ✅ 33/33 |
| `IntegrityRuntimePathTests` | ✅ 9/9 |
| `AntiTamperMonitorTests` | ✅ 55/55 |
| IntegrityChecker + Enforcement | ✅ 31/31 |
| Explicit inherited B1/B2 lifecycle matrix | ✅ 21/21 |
| Combined Unit 4 matrix | ✅ 128/128 |
| Dedicated inherited late-failure hosts A/B | ✅ 1/1 and 1/1 |
| Full Service default-parallel A/B | ✅ 1,218/1,218 and 1,218/1,218 |
| Full App.UI | ✅ 192/192 |
| Fresh full coverage A/B | ✅ 1,218/1,218 and 1,218/1,218; nonempty Cobertura |

Service hosts emitted the existing duplicate xUnit-ID notice. Retained executions reported zero skipped tests.

## Coverage Evidence

Fresh artifacts:

- `coverage-full-a/50197d81-f2fa-41b7-b7f7-7dea6773aaee/coverage.cobertura.xml` — 4,857,486 bytes.
- `coverage-full-b/f0137703-9886-4c80-969f-53948d7fa006/coverage.cobertura.xml` — 4,857,484 bytes.

Both independently report:

| Symbol | Line | Branch |
|---|---:|---:|
| `IntegrityVerdictHandler` | 100% | 96.96% |
| `VerdictDecision` | 100% | 100% |
| `NotificationCommand` | 60% | 100% |
| `AntiTamperMonitor` | 99.15% | 87.75% |

Positive hits exist for compatibility collaborators being ignored, local acceptance clock capture, sequence/epoch allocation, reaction commit, notification metadata, idempotency construction, exact pre-cancel true/false branches, local-failure decisions, deadline decisions, no-op Dispose, and unchanged AntiTamper reaction consumption.

The two fresh coverage executions used unique results/Cobertura directories but the same normal bin/obj tree. A fresh `--artifacts-path` isolation probe failed before compilation with `NETSDK1004` because `--no-restore` left the isolated artifacts tree without `project.assets.json`. Therefore the explicitly requested **fresh unique bin/obj/results coverage-host gate is not closed**. Earlier apply-phase isolated hosts are corroborating history, not substituted as fresh independent evidence.

## Pure Contract Compliance Matrix

| # | Requirement | Runtime/static evidence | Result |
|---:|---|---|---|
| 1 | Handler is pure; no callback/outbox/store/timer/queue/drainer/background task/discarded task | Callback/outbox compatibility test passes with zero invocations. Source contains only lock-protected state transition, immediate `Task.FromResult`/`Task.FromCanceled`, and no mailbox/effect owner. | ✅ COMPLIANT |
| 2 | Immutable decision and all sequence/epoch/reaction/time/notification/idempotency invariants | Records are immutable; normal sequence/epoch, reaction, third-revoked metadata, and local `ObservedAt` source path are covered. However sequence/epoch use unchecked `++long` and silently wrap, and the key omits identity scope/stable IssueKey and cannot distinguish both reaction and notification effect kinds carried by one decision. | ❌ FAILING |
| 3 | Immediate pure sync API; async only immediate result or exact pre-cancel | Focused cancellation test preserves exact token and next sequence `1`; source has no post-commit await/cancellation path. Sync wrappers immediately return the committed reaction. | ✅ COMPLIANT |
| 4 | Deprecated constructor compatibility never invokes collaborators | Runtime test supplies callback and outbox and proves neither is invoked. Constructor explicitly ignores both; no hidden effect task exists. | ✅ COMPLIANT |
| 5 | Minimal AntiTamper consumption preserves policy/runtime and does not implement B2 | AntiTamper source/test are exact base. It consumes the compatibility reaction synchronously; runtime, enforcement, AntiTamper, and combined Unit4 suites pass. No decision lease/notification delivery is claimed. | ✅ COMPLIANT |
| 6 | Runtime tests prove metadata/no-I/O and canonical enforcement without private-helper or false B2 delivery claims | Runtime test uses public decision APIs and real AntiTamper/enforcement paths, asserts no handler outbox calls, and does not invoke private helpers. It correctly treats notification as metadata only. Identity-scoped key behavior and distinct backend timestamp versus local `ObservedAt` remain without a covering runtime scenario. | ❌ UNTESTED/PARTIAL |
| 7 | Preserve 4C1A and avoid deferred drift | Handler 33/33, runtime 9/9, combined 128/128, full Service twice, App.UI, and inherited late-failure hosts pass. Only handler plus handler/runtime tests differ from base. | ✅ COMPLIANT |
| 8 | Failed mailbox symbols absent; Dispose is resource-free no-op | Source and diff show no mailbox/drainer/effect-token/resource ownership. Dispose only suppresses finalization and has coverage. | ✅ COMPLIANT |

## Critical Decision-Invariant Defects

### 1. Sequence and epoch silently overflow

`HandleVerdictDecision`, `HandleLocalFailureDecision`, and `EvaluateDeadlineDecision` allocate identity with unchecked `++this.sequence` and `++this.epoch`. At `long.MaxValue`, both wrap to `long.MinValue`, violating strict increase, uniqueness, contiguity, and the explicit no-silent-overflow/collision requirement. There is no checked guard and no overflow test.

### 2. Idempotency key is not identity-scoped and is not effect-kind complete

The emitted shape is:

```text
integrity/integrity-verdict/{epoch}/{sequence}/{effect}
```

It omits the device/identity scope and canonical Unit4A `IssueKey` required by the revised design (`integrity/{identityScope}/{IssueKey}/{Epoch}/{Sequence}/{EffectKind}`). Two handler instances for different devices can emit the same key for the same local sequence/epoch/effect. In addition, the third-revoked decision carries both a `Limit` reaction and notification metadata but exposes one key classified only as `notification`; it cannot provide collision-free keys for both effect types if B2 consumes both commands.

The retained uniqueness tests cover only sequential decisions from one handler and fresh escalation epochs. They do not cover cross-identity collisions, stable IssueKey inclusion, or separate effect kinds.

### 3. Required runtime distinctions are not fully tested

Source correctly assigns `ObservedAt` from the injected local clock, but retained tests assert only non-default values or use backend and local timestamps that are initially equal. No passing scenario proves that an intentionally different/untrusted backend timestamp cannot become `ObservedAt`. Likewise, no runtime test covers identity-scoped key separation because identity is absent from the decision API/key.

## Correctness (Static Evidence)

| Area | Status | Notes |
|---|---|---|
| Purity | ✅ | No external collaborator invocation or task/resource owner. |
| Immutability | ✅ | Positional sealed records with init-only values and no delegates/services/tasks. |
| Normal sequence/epoch | ✅ | One increment under `stateGate` per accepted decision call. |
| Overflow | ❌ | Unchecked signed wrap creates duplicate/retrograde identities. |
| Local acceptance time | ✅ source / ⚠️ test | `ObservedAt = clock()` under transition lock; distinct backend/local behavior untested. |
| Notification metadata | ✅ | Exact third-revoked pending transition, reset permits one fresh command. |
| Idempotency | ❌ | Missing identity scope/canonical IssueKey and separate effect-kind keys. |
| Reaction preservation | ✅ | 4C1A handler/runtime matrix remains green. |
| Dispose | ✅ | No-op compatibility; no queue/resource. |

## Design Coherence

| Decision | Followed? | Notes |
|---|---|---|
| Pure handler | ✅ | Effect ownership removed. |
| Sequence/epoch per accepted decision | ⚠️ | Correct on ordinary range; no safe exhaustion behavior. |
| Idempotency formula | ❌ | Identity scope and canonical IssueKey are absent; one key cannot identify both carried effect types. |
| Sync/async compatibility | ✅ | Immediate and effect-free. |
| B2/4C2 exclusion | ✅ | AntiTamper and durable owners remain unchanged. |

## Strict TDD Compliance

| Check | Result | Details |
|---|---|---|
| Six-column evidence present | ✅ | Three complete rows include RED, GREEN, TRIANGULATE, SAFETY NET, and REFACTOR. |
| Genuine behavioral RED for pure boundary | ✅ | Restored 4C1A invoked callback three times; final test proves zero callback/outbox effects. |
| New decision type/API seams | ➖ Compile seam | Correctly classified separately, not fabricated as behavioral RED. |
| Runtime migration | ✅ Alignment | Explicitly classified as spec/design migration, not production RED. |
| GREEN confirmed | ✅ existing tests | Focused, handler, runtime, regressions, and coverage all pass. |
| Acceptance coverage complete | ❌ | No overflow, identity-scope/effect-kind collision, or distinct backend/local-time scenario. |

## Test Layer Distribution

| Layer | Tests | Files | Assessment |
|---|---:|---:|---|
| Handler unit/component | 33 | 1 | Policy and basic pure-decision contract |
| Runtime integration | 9 | 1 | Real backend/AntiTamper/enforcement/store path plus public metadata checks |
| AntiTamper lifecycle/component | 55 | 1 unchanged | Inherited effect-owner safety net |
| E2E | 0 | 0 | Outside this slice |

## Assertion Quality

Modified tests invoke production APIs and assert real callback/outbox absence, sequence/epoch values, cancellation token identity, notification metadata, idempotency differences, and durable enforcement state. No tautology, ghost loop, smoke-only assertion, or production-free assertion was found.

**Assertion quality**: ✅ Assertions are behavioral; the problem is missing invariant cases, not trivial assertions.

## Workspace, Budget, and Drift

| Check | Result |
|---|---|
| Branch / HEAD / base / merge-base | ✅ exact requested values |
| Staged files | ✅ none |
| Changed tracked paths | ✅ handler plus handler/runtime tests only |
| AntiTamper source/test | ✅ exact base content |
| CODE+TEST budget | ✅ `348/400` |
| `git diff --check` | ✅ exit 0; LF→CRLF notices only |
| `.codegraph/` | ✅ absent |
| Mailbox remnants | ✅ absent |
| Program/store/retry/4C1B2/4C2/Unit5 drift | ✅ none found |
| Readability | ✅ materially improved over rejected mailbox; public decision methods lack XML documentation but remain direct and reviewable |

## Issues

### CRITICAL

1. **Sequence/epoch overflow is silent**: unchecked `long` wrap violates strict monotonic uniqueness and creates identity/key collisions.
2. **Idempotency key violates the revised design**: no identity scope or canonical IssueKey, and no distinct key per reaction-versus-notification effect type.
3. **Required scenarios are untested**: no identity-scope/effect-kind collision test, overflow test, or distinct backend timestamp versus local `ObservedAt` test.
4. **Fresh physical coverage isolation gate is incomplete**: both fresh coverage runs pass, but unique bin/obj isolation could not execute under `--no-restore`.

### WARNING

1. `NotificationCommand` reports only 60% line coverage, although the exercised escalation properties needed by this slice have hits.
2. Existing package/analyzer warnings and duplicate xUnit-ID notice remain outside this slice.
3. Public decision APIs/types lack XML documentation; this is readability/API hygiene, not the blocking defect.

### SUGGESTION

None in this read-only verification. Corrections require a new apply/remediation pass.

## Final Verdict

**FAIL**

The redesign successfully removes the rejected mailbox and closes the pure no-effect, immediate API, compatibility, regression, and scope boundaries. It is still not autonomously commit-ready because sequence/epoch exhaustion silently breaks identity, the idempotency key cannot satisfy identity/effect collision requirements, required invariant scenarios lack passing tests, and the requested fresh physical coverage-isolation gate did not close.

**NOT APPROVED FOR 4C1B1 COMMIT.**
