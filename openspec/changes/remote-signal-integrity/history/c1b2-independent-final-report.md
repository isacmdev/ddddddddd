# C1B2 Final Independent Closure Verification

**Branch / HEAD:** `feat/sdd7-4c2c1b2-stale-fault-hardening` / `1091d8732e17902752fa94fbb766a976c6407ba3`  
**Mode:** Strict TDD  
**Verdict:** **FAIL**

## Remaining Blocker

The three success races identified by the prior re-verification are now closed at runtime, but one authoritative lifecycle scenario remains untested: **a cancellation-ignoring startup `LoadAsync` that succeeds after Stop**.

`AntiTamperMonitor.cs:343` is the post-Load generation guard. Fresh coverage reports `1/2` outcomes there, and repository-wide test inspection finds only `StartupLoadFaultAfterStop_IsSuppressedWithoutContinuations`; no test releases a successful blocked Load after direct `StopAsync()` and proves no restore, reconciliation, Save, effect, notification, or remote integrity continuation. The authoritative lifecycle clarification covers startup Load completion as well as fault, and SDD compliance requires runtime evidence. Therefore this is `UNTESTED`, not a non-required branch.

The other remaining partial changed branch, `SaveEffectProgressAsync` line 893 (`stateStore != null` while `owner.DurableState == null`), is a defensive impossible/unsupported contract state for the pure-handler durable production path and is not a required stale/fault scenario.

## Prior-Finding Closure

| Prior finding | Current evidence | Result |
|---|---|---|
| Stale startup Load fault escaped | Deterministic blocked-Load fault test passes | **CLOSED for fault** |
| Stale startup reaction success/fault | Success/fault theory passes both outcomes | **CLOSED** |
| Reaction-progress Save success/fault after Stop | Success/fault theory persists exact completion only on success and starts no notification | **CLOSED** |
| Notification completion success/fault after Stop | Success/fault theory proves no progress Save and pending completion state | **CLOSED** |
| Notification-progress Save success/fault after Stop | Success/fault theory persists exact completion only on success without stale fault | **CLOSED** |
| Pre-effect exact-key retry final IDs | Exact pending/completed IDs and absent notification asserted | **CLOSED** |
| Failure-produced progress restart and final no-op | Fresh monitor reconciles exact keys; final restart emits no reaction/notification | **CLOSED** |
| Startup Load success after Stop | No runtime test; changed line 343 branch remains 1/2 | **NOT CLOSED** |

## Requirement Matrix

| Requirement | Runtime/static evidence | Result |
|---|---|---|
| Pre-effect Save failure leaves no effect and same-process exact retry converges | Focused retry test passes with final exact IDs | **COMPLIANT** |
| Effect/progress-Save failure leaves valid pending state and restart converges in order | Failure-produced store restart + final no-op passes | **COMPLIANT** |
| Startup Load/reaction completion or fault after Stop starts no downstream work | Load fault and reaction success/fault pass; Load success absent | **PARTIAL / UNTESTED Load-success cut** |
| Startup reaction-progress Save success/fault after Stop | Paired theory passes; exact completion/no-notification assertions | **COMPLIANT** |
| Startup notification success/fault after Stop | Paired theory passes; no progress Save begins | **COMPLIANT** |
| Startup notification-progress Save success/fault after Stop | Paired theory passes; exact completion/pending outcomes | **COMPLIANT** |
| Active startup/admitted faults remain visible | Active Load/validation and admitted reaction/notification fault focus passes | **COMPLIANT** |
| Admitted reaction→notification chain drains across Stop/Dispose | Inherited drain/order tests pass; production chain unchanged | **COMPLIANT** |
| Null-store progress behavior | `SaveEffectProgressAsync` retains immediate in-memory progress when `stateStore` is null; inherited tests pass | **COMPLIANT** |
| No validation swallowing, lock-held I/O, ownership/schema/retry/timer/DI/Piranha drift | Narrow inactive startup catch; exact two-file diff; source inspection | **COMPLIANT** |

## Fresh Commands and Results

| Gate | Result |
|---|---|
| HEAD/branch/status/staged/numstat audit | Correct HEAD/branch; exact two tracked files; staged `0` |
| `git diff --check` | Exit `0` |
| Service build `--no-restore -c Debug` | Exit `0`; 0 errors; existing NU1601 |
| Exact current C1B2/remediation theories | `11/11` |
| Inherited active-fault/admitted-drain focus | `8/8` |
| Complete `AntiTamperMonitorTests` | `97/97` |
| Exact-exclusion Service safety filter | `1298/1298`; duplicate-ID notice |
| Portable-PDB test build | Exit `0`; 0 errors; existing analyzer/package warnings |
| Fresh external AntiTamper coverage | `97/97`; non-empty Cobertura |

The safety filter excluded only:

1. `ControlParental.Service.Tests.NamedPipeUIServerHostedAdapterTests.StartAsync_ExposesUiPipeBeforeClientConnects`
2. `ControlParental.Service.Tests.TaskSchedulerBackupServiceUnregisterTests.UnregisterBackupTasksAsync_WhenNoTasks_ReturnsTrue`

## Strict-TDD Audit

All immutable hashes still match:

| Artifact | SHA-256 | Judgment |
|---|---|---|
| `sdd7-c1b2-red.log` | `9A10D51772E718095DC301B5DEDBBC72A5D9C15E18054459F06058A84A08F2A2` | Contract-invalid stop-only failures; not product RED |
| `sdd7-c1b2-startup-red.log` | `3481F15D34010F570B26551FED084F308B221D984A3A07107783B2C077C863C9` | Invalid Start-cancellation expectation; not product RED |
| `sdd7-c1b2-startup-red-final.log` | `2977124C9425E7668E697C677B754951D8823B92EF8BC3D2AFAFDDD3347005E2` | Valid earlier reaction-success RED |
| `sdd7-c1b2-remediation-red.log` | `88D43ADF99E0E94003910875FF9B61826DE609D025B1A18144A8BA320A885CA4` | Valid stale Load/reaction fault RED; chronology previously verified |

The latest three success theories are honestly classified as **GREEN triangulation**, not fabricated RED. They close the prior lines 377/385/388 evidence gap. There is still no test cycle for successful stale Load completion.

## Fresh Coverage

Artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b2-independent-final-coverage-20260826\fb2cfd18-7153-4ecc-8608-7baccd767d6d\coverage.cobertura.xml`  
SHA-256: `59CA4118A03219739B3A97B8C7C77F24EE9DCE4E14500A0C1FF1E64659FC687F`

Apply artifact hash also matches: `E8639A7426BE50A3E9354C9799CD3F29B576FEAAF170E9CFB470D56E9A1447B3`.

| Scope/method | Line | Branch |
|---|---:|---:|
| Changed executable implementation | **24/24 = 100%** | **22/24 = 91.67%** |
| `AntiTamperMonitor` | 98.97% | 84.93% |
| `RunOwnedStartupAsync` | 100% | 100% |
| `RehydrateAsync` | 100% | 85.71% |
| `ReconcileDurableEffectsAsync` | 89.28% | 86.36% |
| `SaveEffectProgressAsync` | 100% | 91.66% |
| `ExecuteDecisionChainAsync` | 92.85% | 93.75% |

Previously missing outcomes at lines 377, 385, and 388 are now `2/2`. Remaining partial outcomes are line 343 (required stale Load success, blocker) and line 893 (non-required defensive durable-null guard).

## Scope, Quality, and Status

- Production: `28 additions + 9 deletions`.
- Tests: `153 additions + 1 deletion`.
- Exact CODE+TEST: **191/400**.
- Only `AntiTamperMonitor.cs` and `AntiTamperMonitorTests.cs` are tracked modified.
- No production change occurred in the latest test-only triangulation.
- Assertion audit: no tautology, ghost loop, assertion-without-production-call, or title-only scenario in the new tests.
- Test layer: 11 focused unit/component cases in one xUnit/Moq file; no new integration/E2E tests.
- Existing package/analyzer warnings remain; candidate-local `CA2000` warnings for local retry-test monitors are non-blocking.

## Readiness

Task **4.2b cannot yet be checked**, and the candidate **cannot yet be offered for commit authorization**, because startup Load success after Stop still lacks runtime closure.

No production/test edit, stage, commit, push, PR, or history action was performed by this verifier.
