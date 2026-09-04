## Verification Report

**Change**: `offline-sync-recovery`
**Scope**: final independent verification of task 2.1
**Mode**: Strict TDD
**Artifact mode**: hybrid
**Date**: 2026-08-19
**Verdict**: **PASS WITH WARNINGS**

All task-2.1 acceptance behaviors now have source and fresh runtime evidence. The three previously reported CRITICAL defects are resolved: heartbeat failures are redacted, caller cancellation propagates, and definitive-identity/authenticated-send timeouts map to exact `Request timeout` without caller cancellation. The autonomous 313-line code/test delta remains below 400 lines. Warnings are limited to one non-blocking changed-branch combination, existing package warnings, and accepted historical foundation provenance.

### Authority and Completeness

| Check | Result |
|---|---|
| Worktree | ✅ `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-2-1` |
| Branch | ✅ `feat/sdd6-2-1-failure-classification` |
| HEAD / exact prerequisite | ✅ `3315ffb72bf8eafa85761ea69b1f4e377a599beb` |
| Parent / audited foundation | ✅ `24fc369f434bef7fcd6ed5837f0e1fe526ae7d5a` / `7b74a0a4b6430610b344cea0afa9da7493e1a084`; verified ancestry |
| Filesystem checkboxes | ✅ 1.1A–1.1B2b, 1.2, and 2.1 checked; 2.2–4.2 unchecked |
| Engram checkboxes | ✅ same state; formal progress 6/11 |
| Prior reports | ✅ `verify-report-task-2.1.md` and `verify-report-task-2.1-approved.md` preserved unchanged |

Task 2.1 may remain checked. This verdict does not approve the full change: five tasks remain unchecked and archive readiness is blocked.

### Complete Diff and Review Boundary

Relative to exact prerequisite `3315ffb`:

| File | Additions | Deletions | Touched |
|---|---:|---:|---:|
| `src/ControlParental.Service/BackendClient.cs` | 42 | 25 | 67 |
| `tests/ControlParental.Service.Tests/BackendClientTests.cs` | 199 | 0 | 199 |
| `tests/ControlParental.Service.Tests/AuthenticatedBackendClientTests.cs` | 47 | 0 | 47 |
| **Code/test total** | **288** | **25** | **313** |

✅ The complete task slice is autonomous and below the 400-line boundary. Prerequisite commits and governance/evidence documents are excluded from the budget. No unrelated production/test, dependency/version, or lockfile change exists. `IBackendClient.cs` and `BackendClientSingleRequestTests.cs` remain unchanged from the prerequisite.

### Fresh Command Matrix

All commands used finite 300-second execution timeouts and `--no-restore`. No required command exited nonzero.

| Gate | Result |
|---|---|
| Actual `ControlParental.Service.Tests.csproj` build | ✅ 0 errors; 5 existing package warnings |
| Exact focused discovery: `BackendClientTests\|BackendClientSingleRequestTests\|AuthenticatedBackendClientTests` | ✅ 70 exact cases |
| Exact focused execution | ✅ 70 passed, 0 failed, 0 skipped |
| Exact prerequisite compatibility filter | ✅ 3 passed, 0 failed, 0 skipped |
| Exactly one full Service regression from validated build | ✅ 1122 passed, 0 failed, 0 skipped |
| Fresh exact focused coverage | ✅ 70 passed; Cobertura SHA-256 `440ED3836846275B94EFAF05B09762164817F55CB3D285B84FAFB60DEC4598B3` |

Validated test DLL: 1,515,520 bytes, SHA-256 `A075D7F600344D651F9D1EEF4D63DA844DCC49FA0693C512C092B016A160E7D4`. Build/test/coverage created no tracked generated or dependency change.

Exact LF compatibility hashes remain valid with zero CRLF:

- harness: `2910260E8E704338AB6854C855E94A24010530A69BE44D3AA1B60229DA14AB0F`
- receipt schema: `25942EF6D419F468F77DB1067C2CAD5333FBB533F3468A81667B098DC5415A43`

The stale 1115/3 gate remains correctly classified as invalid stale-artifact evidence. Fresh rebuilding, compatibility 3/3, and full regression 1122/1122 establish the current baseline.

### Prior CRITICAL Resolution Matrix

| Prior CRITICAL | Source evidence | Fresh runtime evidence | Result |
|---|---|---|---|
| Heartbeat exception-detail leakage | `SendHeartbeatAsync` catches `HttpRequestException` without binding it and returns exact `Network error` | sentinel redaction test asserts exact text and absence of raw detail | ✅ RESOLVED |
| Policy caller cancellation swallowed | caller-cancel catch precedes timeout and broad catches and rethrows | policy cancellation test observes `OperationCanceledException` | ✅ RESOLVED |
| Policy non-caller timeout misclassified as remote denial | `classifyTimeout: true` maps identity `Timeout` and authenticated-send non-caller cancellation to an internal-token `TaskCanceledException`; policy catch returns exact `Request timeout` | identity-timeout test: zero sends; authenticated-send timeout test: exactly one send; both exact `Request timeout` | ✅ RESOLVED |

Genuine identity rejection remains separate: `FetchPolicyAsync_WithoutDefinitiveSession_DeniesWithoutHttp` returns failure with `Remote access denied` and performs zero HTTP sends.

### Task-2.1 Acceptance Matrix

| Acceptance | Runtime/source evidence | Result |
|---|---|---|
| T10-B definitive identity gate before HTTP | session is acquired before request construction/send; denied identity sends zero HTTP | ✅ COMPLIANT |
| Accepted identity and 401/403 handling | bearer injection; one send; generation invalidation without replay | ✅ COMPLIANT |
| Stable idempotency | one non-empty idempotency key is created outside the attempt loop and reused | ✅ COMPLIANT |
| Single request / no accidental amplification | normal and rejection paths issue one request; transient retry stops at the configured bound; cancellation/timeout do not overlap | ✅ COMPLIANT |
| Bounded retry/backoff | 1–3 attempts, delay clamped to 30 seconds, Retry-After/reconnect behavior tested | ✅ COMPLIANT |
| Caller cancellation | policy and push-usage cancellation propagate and do not retry | ✅ COMPLIANT |
| Identity acquisition timeout | bounded identity `Timeout` maps exact `Request timeout`, zero HTTP sends | ✅ COMPLIANT |
| Authenticated-send timeout | internal linked-token cancellation maps exact `Request timeout`, one send, no retry | ✅ COMPLIANT |
| Genuine identity rejection | remains exact `Remote access denied`, zero sends | ✅ COMPLIANT |
| Redacted exposed delivery outcomes | policy/usage/alerts/behavioral/heartbeat use fixed safe text or bounded status; push registration uses fixed code; bool/integrity outcomes expose no exception text | ✅ COMPLIANT |
| Existing `IBackendClient` sufficient | existing result/cancellation contracts remain sufficient; no interface change required | ✅ COMPLIANT |

`PairAsync` still has legacy exception-message outcomes, but it is internal, obsolete, absent from `IBackendClient`, and owned by the pairing coordinator. It is not an exposed task-2.1 delivery path.

### `classifyTimeout` Design and Caller Audit

`classifyTimeout` is a private, default-false implementation option. Only `FetchPolicyAsync` opts in. Static caller inspection confirms all other delivery methods retain their prior timeout/null, retry, idempotency, and redaction behavior.

The option establishes a deliberate semantic boundary:

- caller cancellation is checked first and rethrown;
- opted-in identity `Timeout` becomes an internal-token `TaskCanceledException`;
- opted-in authenticated-send cancellation becomes the same bounded timeout signal;
- non-timeout identity denial still returns `null`, preserving `Remote access denied`;
- non-opted-in callers retain their existing behavior.

This localized boolean is not an architecture replacement and does not create another retry owner. It is slightly less expressive than a typed transport outcome, but it does not break the current spec or `IBackendClient` contract.

### Catch-Path and Assertion Audit

Fresh coverage executes every task-required adjacent catch:

- shared transport: caller cancellation, opted-in timeout, non-opted-in cancellation, and `HttpRequestException` retry/failure;
- policy: network, caller cancellation, timeout, and unexpected failure;
- heartbeat: network, caller cancellation, and unexpected failure;
- usage/alerts/behavioral events: network, cancellation, and unexpected failure;
- push-token/time-request/integrity paths retain bounded non-detail outcomes.

No required adjacent catch remains untested. Tests call the real `BackendClient` and assert outcomes, send counts, headers, stable retry keys, delay values, invalidation, cancellation, timeout bounds, exact safe text, and sentinel absence. No tautology, ghost loop, disconnected assertion, meaningless type-only assertion, or mock-only substitute was found.

### Design Coherence

| Decision / invariant | Result |
|---|---|
| Service-owned definitive identity transport | ✅ |
| Identity acceptance before authenticated HTTP | ✅ |
| Finite transport retry, separate from task-2.2 coordinator ownership | ✅ |
| Stable retry idempotency | ✅ |
| Safe/redacted diagnostics | ✅ |
| Caller cancellation propagation | ✅ |
| Distinct identity rejection and timeout outcomes | ✅ |
| No unrelated scope or architecture replacement | ✅ |

### Strict TDD Evidence

| Evidence | Result |
|---|---|
| Original task RED | ✅ `22CB9C829E913F9E6020524CBB19807B964A07FDC03A1B3C68A1C5676849D4A0` re-hashed |
| First remediation RED | ✅ `52BFB765561B6A468107B067C094F7C6622EF87571C1E6F38FC7F4EE4D31BA2E` re-hashed |
| Latest timeout RED | ✅ 1 failed / 0 passed; `C3963AEAF046A5BA3F9474E38C246E74F9ADEFCA0D9E818D2055D23BC4932DF5` re-hashed |
| Latest GREEN / triangulation | ✅ GREEN log present; timeout triangulation 2/2 hash `5CDEA81BB8F5F47809D3EFC17B839C99DF59233D045AF89B3B609257A4A7AE9D` |
| Historical final build/focused/compatibility/full logs | ✅ all hashes match `apply-progress.md` |
| Immutable pre-edit bytes | ✅ source `1B821B...BFFD`; tests `C5D077...D301`; hashes, blobs, and lengths match |
| Final patch | ✅ SHA-256 `5DAF5C84C1301DD55426F6A634D521ADF5192802119A065FCE33B4DFA383760E` |
| Patch reproducibility | ✅ patch old/new blob IDs exactly match immutable pre copies and current files |
| Final manifest | ✅ SHA-256 `1A269AE64B0952E9AA260828A82E4871EAEBAEF1DF9E5B82BB0711C0422E610B`; final hashes/lengths match current bytes |
| Triangulation | ✅ identity-result timeout and authenticated-send timeout produce the same safe policy outcome with different send counts |

### Changed-Scope Coverage

Fresh artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task21-independent-final-verify-20260819-coverage\3d964c10-1e56-4a89-adde-1061efd86a1d\coverage.cobertura.xml`.

| Scope | Line | Branch/path | Result |
|---|---:|---:|---|
| Complete task-2.1 added/replacement executable production delta | **35/35 = 100%** | changed branch-bearing conditions **5/6 = 83.33%**; all required timeout/cancellation/redaction paths executed | ✅ Acceptable |
| `FetchPolicyAsync` complete method | **100%** | **100%** | ✅ Excellent |
| Previously uncovered policy timeout catch | **3/3 = 100%** | identity timeout + send timeout | ✅ Resolved |

The one uncovered changed-branch combination is `classifyTimeout == false` together with identity error `Timeout`. Static caller audit confirms it returns the unchanged `null` outcome for non-policy callers. It is not required by task 2.1 and does not undermine the two opted-in policy-timeout scenarios, but exact branch coverage is reported transparently.

### Issues Found

**CRITICAL**: None.

**WARNING**:

1. The new `classifyTimeout && authorization.Error == Timeout` condition has 3/4 condition outcomes covered; the non-policy `Timeout` combination is statically safe but lacks a dedicated test.
2. Existing package warnings remain (`NU1601`, `NU1701`); they are unrelated to task 2.1.
3. Historical foundation strict-TDD provenance warnings remain accepted and outside this scoped verdict.

**SUGGESTION**: A future typed internal transport outcome could replace the private boolean if timeout classifications expand; no redesign is required for task 2.1.

### Recommendation and Readiness

- **Task 2.1 checkbox**: keep checked.
- **Formal and verified progress**: **6/11**.
- **Tasks 2.2, 3.1, 3.2, 4.1, 4.2**: remain unchecked.
- **Full change / archive readiness**: blocked by five remaining tasks.

### Verdict

**PASS WITH WARNINGS — task 2.1 only.** All required behaviors and prior CRITICALs are resolved with fresh runtime evidence. Non-blocking coverage, package, and historical provenance warnings prevent an unqualified PASS but do not require reopening task 2.1.
