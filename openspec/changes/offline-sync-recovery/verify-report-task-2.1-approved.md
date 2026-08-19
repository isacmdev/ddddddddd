## Verification Report

**Change**: `offline-sync-recovery`
**Scope**: task 2.1 narrow remediation and complete task-2.1 acceptance
**Mode**: Strict TDD
**Artifact mode**: hybrid
**Date**: 2026-08-19
**Verdict**: **FAIL**

The two previously reported CRITICAL defects are genuinely remediated: heartbeat transport failures now return the exact safe `Network error` outcome, and caller-requested policy cancellation propagates. All required fresh commands pass, the complete 243-line task boundary remains under 400 lines, and changed-line/path coverage is 100%. Approval is still blocked because the adjacent non-caller `FetchPolicyAsync` timeout path is uncovered and the production definitive-identity path returns `Remote access denied`, not the required `Request timeout`.

### Authority and Completeness

| Check | Result |
|---|---|
| Worktree | ✅ `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-2-1` |
| Branch | ✅ `feat/sdd6-2-1-failure-classification` |
| HEAD / exact prerequisite | ✅ `3315ffb72bf8eafa85761ea69b1f4e377a599beb` |
| Parent / audited foundation | ✅ `24fc369f434bef7fcd6ed5837f0e1fe526ae7d5a` / `7b74a0a4b6430610b344cea0afa9da7493e1a084`; verified ancestry |
| Filesystem state | ✅ foundation 1.1A–1.1B2b, 1.2, and 2.1 checked; 2.2–4.2 unchecked |
| Engram state | ✅ same state; formal progress 6/11 |
| Prior failed report | ✅ `verify-report-task-2.1.md` remains preserved |

The recorded state is 6/11. Because this independent re-verification still fails one required task-2.1 behavior, the checkbox recommendation is to reopen 2.1; verified readiness remains 5/11 until another remediation and re-verification. The full change remains incomplete regardless of this scoped verdict.

### Complete Diff and Review Boundary

Relative to exact prerequisite `3315ffb`:

| File | Additions | Deletions | Touched |
|---|---:|---:|---:|
| `src/ControlParental.Service/BackendClient.cs` | 24 | 20 | 44 |
| `tests/ControlParental.Service.Tests/BackendClientTests.cs` | 199 | 0 | 199 |
| **Code/test total** | **223** | **20** | **243** |

✅ The complete task slice is autonomous and below the 400-line boundary. The remediation itself is 6 production additions / 2 deletions and 46 test additions. Prerequisite commits and governance/evidence documents are excluded from the code/test budget. No unrelated production/test, dependency/version, or lockfile change exists. `IBackendClient.cs`, `BackendClientSingleRequestTests.cs`, and `AuthenticatedBackendClientTests.cs` remain unchanged from the prerequisite.

### Fresh Command Matrix

All commands used finite 300-second execution timeouts and `--no-restore`. No required command exited nonzero.

| Gate | Result |
|---|---|
| Actual `ControlParental.Service.Tests.csproj` build | ✅ 0 errors; 5 existing package warnings |
| Exact focused discovery: `BackendClientTests\|BackendClientSingleRequestTests\|AuthenticatedBackendClientTests` | ✅ 68 exact cases |
| Exact focused execution | ✅ 68 passed, 0 failed, 0 skipped |
| Exact prerequisite compatibility filter | ✅ 3 passed, 0 failed, 0 skipped |
| Exactly one full Service regression from validated build | ✅ 1120 passed, 0 failed, 0 skipped |
| Fresh exact focused coverage | ✅ 68 passed; Cobertura SHA-256 `8BC08D09C6D4C4602A9DB7AC331CF0547B5CF68A40CC35C847E1548AA5DA796B` |

Validated test DLL: 1,510,912 bytes, SHA-256 `6896689D78EA2AB976A512E00D892C05484E0F2B45255D44DAD5BF7607C48C98`. Build/test/coverage created no tracked generated or dependency change.

Exact LF compatibility hashes remain valid with zero CRLF:

- harness: `2910260E8E704338AB6854C855E94A24010530A69BE44D3AA1B60229DA14AB0F`
- receipt schema: `25942EF6D419F468F77DB1067C2CAD5333FBB533F3468A81667B098DC5415A43`

The stale 1115/3 gate remains correctly classified as invalid stale-artifact evidence. Fresh rebuilding, the 3/3 compatibility filter, and the 1120/1120 full regression establish the current baseline.

### Prior CRITICAL Remediation

| Prior issue | Source evidence | Runtime test | Result |
|---|---|---|---|
| Heartbeat `HttpRequestException` leaked exception details | `BackendClient.cs:451-453` catches without binding the exception and returns exact `Network error` | `SendHeartbeatAsync_WhenNetworkError_RedactsExceptionDetails` asserts failure, exact safe text, and sentinel absence | ✅ RESOLVED |
| Policy caller cancellation was swallowed | `BackendClient.cs:249-251` rethrows caller cancellation before timeout and broad catches | `FetchPolicyAsync_WhenCallerCancellationIsRequested_PropagatesCancellation` observes `OperationCanceledException` | ✅ RESOLVED |

The catch ordering now prevents the broad catch from swallowing caller cancellation. Both remediation tests call the real `BackendClient`; they are not tautologies or disconnected mocks.

### Complete Task-2.1 Acceptance Matrix

| Acceptance | Evidence | Result |
|---|---|---|
| T10-B definitive identity gate before HTTP | shared send path obtains a definitive session before request creation/send; denied-identity test asserts zero sends | ✅ COMPLIANT |
| Accepted identity / 401–403 handling | bearer injection and invalidation-without-replay tests pass | ✅ COMPLIANT |
| Stable idempotency | retry test asserts one non-empty key reused across both attempts | ✅ COMPLIANT |
| Single request / no accidental retry amplification | normal/non-retryable tests assert one request; transient paths stop at configured bound; cancellation and timeout do not overlap or retry | ✅ COMPLIANT |
| Bounded transport retry/backoff | attempts restricted to 1–3; delay clamped to 30 seconds; Retry-After and reconnect tests pass | ✅ COMPLIANT |
| Caller cancellation | policy and push-usage cancellation tests propagate and do not retry | ✅ COMPLIANT |
| Non-caller bounded timeout reports `Request timeout` | policy's production identity path catches its linked-token timeout inside `SendAuthenticatedAsync` and returns `null`; `FetchPolicyAsync` consequently returns `Remote access denied`. Lines 253–255 are uncovered by all 68 focused tests | ❌ UNTESTED / NOT IMPLEMENTED |
| All exposed delivery outcomes redact exception details | policy/usage/alerts/behavioral/heartbeat outcomes are fixed allowlisted text or bounded HTTP status; push registration uses a fixed code; bool/integrity results carry no exception text | ✅ COMPLIANT apart from timeout classification above |
| Existing `IBackendClient` contract suffices | result contracts and cancellation-token parameters remain sufficient; no interface diff needed | ✅ COMPLIANT |

`PairAsync` still contains legacy exception-message outcomes, but it is `internal`, obsolete, absent from `IBackendClient`, and explicitly owned by the pairing coordinator; it is not an exposed task-2.1 delivery path. No other adjacent exposed catch returns raw exception detail.

### Timeout and Catch-Path Analysis

`SendAuthenticatedAsync` creates a linked timeout token and catches non-caller `OperationCanceledException` at lines 139–142, returning `null`. Therefore `FetchPolicyAsync` does not receive a `TaskCanceledException` from the production definitive-identity path and cannot reach its `Request timeout` return at lines 253–255. The focused Cobertura report confirms all three timeout-catch lines have zero hits.

The existing production timeout test is `PushUsageLogsAsync_RequestTimeout_FailsWithoutOverlappingRetry`; it proves one send and failure, but does not assert policy timeout classification. No `FetchPolicyAsync` non-caller timeout test was discovered. Under the runtime-evidence rule, the required exact timeout outcome is not compliant.

### Design Coherence

| Decision / invariant | Result |
|---|---|
| Service-owned definitive identity transport | ✅ |
| Finite transport retry, separate from pending task-2.2 coordinator ownership | ✅ |
| Stable retry idempotency | ✅ |
| Safe/redacted exposed diagnostics | ✅ |
| Caller cancellation propagation | ✅ |
| Distinct bounded timeout classification | ❌ production policy path collapses timeout into remote denial |
| No unrelated scope or architecture replacement | ✅ |

### Strict TDD Evidence

| Evidence | Result |
|---|---|
| Original task RED history | ✅ original `22CB9C829E913F9E6020524CBB19807B964A07FDC03A1B3C68A1C5676849D4A0` re-hashed |
| Remediation RED | ✅ 2 failed / 0 passed; hash `52BFB765561B6A468107B067C094F7C6622EF87571C1E6F38FC7F4EE4D31BA2E` re-hashed |
| Remediation GREEN | ✅ 2 passed; hash `B0C152E8C7E4C15E9A8D8A3150D2891E2F38D15A06AAFC3ECE947A7984268570` re-hashed |
| Build / focused / compatibility / full historical logs | ✅ all hashes match `apply-progress.md` |
| Immutable pre-edit bytes | ✅ source `444474...15B6E`; tests `16B073...D647`; hashes and lengths match manifests |
| Final patch | ✅ SHA-256 `53FAFB5DCEB3F9D5406D4C98BB304F4B65486CBBA3443961ECC6B100332A7029` |
| Patch reproducibility | ✅ patch old/new Git blob IDs exactly match the immutable pre copies and current final files |
| Final manifest | ✅ SHA-256 `FCC1246B04DD9D2AE8FC150C735D7D6833AECE4888A213071E53C575821A555A`; final hashes/lengths match current bytes |
| Triangulation | ⚠️ prior two defects triangulated, but adjacent policy timeout classification remains uncovered |

### Assertion Quality

The 68 focused tests invoke production behavior and assert outcomes, headers, send counts, retry keys, delays, invalidation, cancellation, and sentinel absence. No tautology, ghost loop, assertion without production invocation, meaningless type-only assertion, or mock-only substitute was found. The new heartbeat test verifies exact value plus negative leakage; the cancellation test verifies the thrown type. The missing policy timeout assertion is a coverage gap, not a defect in those two new assertions.

### Changed-Scope Coverage

Fresh artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task21-independent-reverify-20260819-coverage\054ac6e7-c308-42d1-b85e-49b7a2621593\coverage.cobertura.xml`.

| Scope | Line coverage | Branch/path evidence | Result |
|---|---:|---:|---|
| Entire task-2.1 added/replacement executable production delta | **23/23 = 100%** | no changed line adds a Cobertura branch; **10/10** applicable changed exception outcomes executed | ✅ Excellent |
| Adjacent policy timeout catch | **0/3** lines | no covering path | ❌ Gap |

Changed-line coverage is accurate but cannot substitute for the explicitly required adjacent timeout behavior.

### Issues Found

**CRITICAL**

1. Non-caller `FetchPolicyAsync` timeout is neither implemented nor runtime-proven as exact `Request timeout` on the production definitive-identity path. The shared transport converts it to `null`, producing `Remote access denied`; the adjacent timeout catch is 0/3 covered.

**WARNING**

1. Existing package warnings remain (`NU1601`, `NU1701`); they are unrelated to task 2.1.
2. Historical foundation strict-TDD provenance warnings remain accepted and outside this scoped verdict.

**SUGGESTION**: None. Remediation should remain narrowly scoped to policy timeout classification and one real production-constructor covering test.

### Recommendation and Readiness

- **Task 2.1 checkbox**: reopen / set unchecked after status editing is authorized; this verifier did not edit it.
- **Formal recorded progress**: **6/11** in filesystem and Engram.
- **Verified readiness**: **5/11** until policy timeout behavior passes independent re-verification.
- **Tasks 2.2, 3.1, 3.2, 4.1, 4.2**: remain unchecked.
- **Full change / archive readiness**: blocked.

### Verdict

**FAIL — task 2.1 only.** The two prior CRITICALs are fixed and all fresh commands are green, but the explicitly required adjacent non-caller policy-timeout outcome remains uncovered and inconsistent with the production transport path.
