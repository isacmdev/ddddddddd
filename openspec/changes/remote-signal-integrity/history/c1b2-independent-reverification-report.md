# C1B2 Independent SDD Re-verification

**Branch / HEAD:** `feat/sdd7-4c2c1b2-stale-fault-hardening` / `1091d8732e17902752fa94fbb766a976c6407ba3`  
**Mode:** Strict TDD  
**Verdict:** **FAIL**

## Remaining Blocker

**Required stale startup success cuts remain untested.** The prior FAIL report explicitly left successful late Save/restart unexercised, and lifecycle requirements require cancellation-ignoring notification/Save completion as well as faults after Stop. Current tests cover reaction-progress Save **fault**, notification **fault**, and notification-progress Save **fault**, but none covers: (a) reaction-progress Save succeeding after Stop, (b) notification completing after Stop before any progress Save starts, or (c) notification-progress Save succeeding after Stop.

Fresh Cobertura confirms the gap: all changed executable lines are hit, but `AntiTamperMonitor.cs:377`, `:385`, and `:388` each have only `1/2` branch outcomes. These are the post-reaction-progress-Save, post-notification, and post-notification-progress-Save generation guards. Because runtime closure for every prior finding was required, these success scenarios remain `UNTESTED` and block task closure.

## Prior-Finding Closure

| Prior finding | Fresh evidence | Result |
|---|---|---|
| Stale startup reaction fault escaped | `StartupReactionAfterStop_DoesNotContinueOrSurfaceStaleFault(true)` passes; outer stale catch hit | **CLOSED** |
| Stale cancellation-ignoring Load fault escaped | `StartupLoadFaultAfterStop_IsSuppressedWithoutContinuations` passes | **CLOSED** |
| Notification-progress Save stale fault catch lacked proof | `StartupNotificationProgressSaveFaultAfterStop_IsSuppressedAndRemainsPending` passes; old per-Save catch removed | **CLOSED** |
| Failure-produced retry/restart convergence absent | Progress-Save test restarts from the exact pending store, completes unchanged keys, then performs a final no-effect restart | **CLOSED** |
| Pre-effect retry lacked final durable identity assertions | Test now asserts exact pending/completed reaction IDs and null notification IDs | **CLOSED** |
| Successful late notification/Save continuations were unexercised | No covering test; changed guard branches at lines 377/385/388 remain partial | **NOT CLOSED** |

## Requirement Matrix

| Requirement | Runtime/source evidence | Result |
|---|---|---|
| 1. Pre-effect Save failure leaves no effect; exact retry converges | Exact retry test passes and asserts final exact IDs; restart is not a valid cut because failed pre-effect state was never persisted | **COMPLIANT** |
| 2. Effect/progress-Save failure leaves valid pending state; restart converges in order | Failure-produced pending store is rehydrated, exact reaction then notification complete, final restart has zero effects | **COMPLIANT** |
| 3. Stale startup reaction completion/fault starts no downstream work | Success/fault theory passes both cases | **COMPLIANT** |
| 4. Stale startup Save completion/fault starts no notification and remains restartable | Fault path passes; successful late Save path has no runtime test | **PARTIAL / UNTESTED success path** |
| 5. Stale notification completion/fault starts no later progress work | Notification fault and notification-success-before-Stop→progress-Save-fault tests pass; notification completion after Stop is untested | **PARTIAL / UNTESTED completion-after-Stop path** |
| 6. Active faults remain visible; admitted chain drains across Stop/Dispose | Inherited active/admitted focus `8/8`; complete class `94/94` | **COMPLIANT** |
| 7. No speculative retry/schema/timer/DI/Piranha drift | Exact two-file diff; admitted chain unchanged; no lock-held store/effect I/O | **COMPLIANT** |

## Strict-TDD Audit

Remediation RED: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b2-remediation-red.log`  
SHA-256: `88D43ADF99E0E94003910875FF9B61826DE609D025B1A18144A8BA320A885CA4` (**match**).

- Runtime: `1 passed, 2 failed`; the failures were exactly stale startup reaction fault and stale startup Load fault escaping as `InvalidOperationException` where lifecycle cancellation was expected.
- The test host discovered and ran the intended tests; failures were behavioral, not compile/assets/filter/harness failures.
- Timestamp evidence supports chronology: RED completed `2026-08-26T22:15:53Z`; production source was modified `2026-08-26T22:16:14Z`.
- Prior judgments remain unchanged: `sdd7-c1b2-red.log` and `startup-red.log` are not valid product RED; `startup-red-final.log` remains the earlier valid reaction-success RED.
- Current GREEN: exact C1B2/remediation set `8/8`, inherited focus `8/8`, class `94/94`, safety `1295/1295`.

Strict-TDD evidence is valid for the two remediation faults, but the missing late Save-success scenario has no RED/GREEN runtime cycle.

## Fresh Commands and Results

| Gate | Result |
|---|---|
| HEAD/branch/status/staged/numstat audit | Correct HEAD/branch; exact two tracked files; staged `0` |
| `git diff --check` | Exit `0` |
| Service build `--no-restore -c Debug` | Exit `0`; 0 errors; existing NU1601 |
| Exact C1B2/remediation filter | `8/8` |
| Inherited active fault/admitted drain filter | `8/8` |
| Complete `AntiTamperMonitorTests` | `94/94` |
| Exact-exclusion Service safety filter | `1295/1295`; duplicate-ID notice |
| Portable-PDB test build | Exit `0`; 0 errors; existing analyzer/package warnings |
| Fresh external AntiTamper coverage | `94/94`; non-empty Cobertura |

The safety filter excluded only:

1. `ControlParental.Service.Tests.NamedPipeUIServerHostedAdapterTests.StartAsync_ExposesUiPipeBeforeClientConnects`
2. `ControlParental.Service.Tests.TaskSchedulerBackupServiceUnregisterTests.UnregisterBackupTasksAsync_WhenNoTasks_ReturnsTrue`

## Fresh Coverage

Artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b2-independent-reverification-coverage-20260826\6a7f7886-c0fe-4b16-a76b-df1ba385abcf\coverage.cobertura.xml`  
SHA-256: `B6E13788DB56E995E667A409735642A74BB19D34773AB5553432BB6A708E2B1A`

| Scope/method | Line | Branch |
|---|---:|---:|
| Changed executable implementation | **24/24 = 100%** | **19/24 = 79.17%** |
| `AntiTamperMonitor` | 98.97% | 84.93% |
| `RunOwnedStartupAsync` | 100% | 100% |
| `RehydrateAsync` | 100% | 85.71% |
| `ReconcileDurableEffectsAsync` | 89.28% | 72.72% |
| `SaveEffectProgressAsync` | 100% | 91.66% |
| `ExecuteDecisionChainAsync` | 92.85% | 93.75% |

Changed-line coverage exceeds 80%, with no uncovered changed executable line. Changed-branch coverage is below 80% and specifically exposes the untested inactive outcomes after reaction-progress Save, notification, and notification-progress Save at lines 377, 385, and 388.

## Scope, Quality, and Status

- Production: `28 additions + 9 deletions`.
- Tests: `145 additions + 1 deletion`.
- Exact CODE+TEST: **183/400**.
- Only `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` are tracked modified.
- No handler, schema, retries, timer, DI, lock-held I/O, async-owner, Program, or Piranha drift.
- Assertion audit found no tautology, ghost loop, assertion-without-production-call, or title-only scenario in the new tests.
- Test layer: 8 unit/component cases in one xUnit/Moq file; no new integration/E2E tests.
- Candidate-local analyzer warnings include undisposed local monitor warnings in the two retry tests (`CA2000`); non-blocking but should not be hidden.

## Readiness

Task **4.2b cannot yet be checked**, and the candidate **cannot yet be offered for commit authorization**, because successful late notification/Save behavior still lacks required runtime closure.

No production/test edit, stage, commit, push, PR, or history action was performed by this verifier.
