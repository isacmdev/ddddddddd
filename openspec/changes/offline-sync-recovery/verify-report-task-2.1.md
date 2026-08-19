## Verification Report

**Change**: `offline-sync-recovery`
**Scope**: task 2.1 only
**Mode**: Strict TDD
**Artifact mode**: hybrid
**Date**: 2026-08-19
**Verdict**: **FAIL**

Task 2.1 has a valid autonomous 189-line delivery boundary, preserved RED evidence, a clean test-project build, 66/66 focused tests, 3/3 prerequisite compatibility tests, 1118/1118 in exactly one full Service regression, and 100% coverage of the changed production lines. It nevertheless fails behavioral acceptance: `SendHeartbeatAsync` still returns raw `HttpRequestException.Message`, and `FetchPolicyAsync` still converts caller cancellation into `"Unexpected error"`. Neither defective path has a covering assertion in the focused suite.

### Authority and Completeness

| Check | Result |
|---|---|
| Worktree | ✅ `C:\Users\Usuario\AppData\Local\Temp\opencode\control-parental-windows-sdd6-task-2-1` |
| Branch | ✅ `feat/sdd6-2-1-failure-classification` |
| HEAD / prerequisite | ✅ `3315ffb72bf8eafa85761ea69b1f4e377a599beb` |
| Parent / audited foundation | ✅ `24fc369f434bef7fcd6ed5837f0e1fe526ae7d5a` / `7b74a0a4b6430610b344cea0afa9da7493e1a084`; both verified ancestors |
| Filesystem checkboxes | ✅ foundation 1.1A–1.1B2b, 1.2, and 2.1 checked; 2.2–4.2 unchecked |
| Engram checkboxes | ✅ same current state; formal recorded progress 6/11 |
| Verification scope | task 2.1 only; full change remains incomplete |

Earlier unchecked task-2.1 lines in `apply-progress.md` are retained chronology and are superseded by its final gate section. Current recorded state is 6/11, but this independent FAIL means task 2.1 should be reopened; verified readiness is 5/11 until remediation and re-verification.

### Diff and Review Boundary

Relative to exact prerequisite `3315ffb`:

| File | Additions | Deletions | Touched |
|---|---:|---:|---:|
| `src/ControlParental.Service/BackendClient.cs` | 18 | 18 | 36 |
| `tests/ControlParental.Service.Tests/BackendClientTests.cs` | 153 | 0 | 153 |
| **Code/test total** | **171** | **18** | **189** |

✅ The slice is autonomous and below the 400-line review limit. The only production/test changes are `BackendClient.cs` and `BackendClientTests.cs`. Prerequisite commits, four governance documents, and the foundation verification report are excluded from the task-2.1 code/test budget. No dependency/version/lockfile or unrelated production/test change exists. `IBackendClient.cs`, `BackendClientSingleRequestTests.cs`, and `AuthenticatedBackendClientTests.cs` are unchanged from the prerequisite.

### Command and Runtime Evidence

All commands used finite 300-second execution timeouts and `--no-restore`. No required command exited nonzero.

| Gate | Command | Result |
|---|---|---|
| Actual test-project build | `dotnet build tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --nologo` | ✅ 0 errors; 5 existing package warnings |
| Exact focused discovery | `dotnet test ... --no-restore --no-build --list-tests --filter "FullyQualifiedName~BackendClientTests|FullyQualifiedName~BackendClientSingleRequestTests|FullyQualifiedName~AuthenticatedBackendClientTests"` | ✅ 66 exact cases |
| Exact focused execution | same filter, normal execution | ✅ 66 passed, 0 failed, 0 skipped |
| Prerequisite compatibility | exact filter for acceptance-manifest, named-pipe maximum-instance, and NativeAOT trimming tests | ✅ 3 passed, 0 failed, 0 skipped |
| Full Service regression | exactly one `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --no-build` after the validated build | ✅ 1118 passed, 0 failed, 0 skipped |
| Fresh changed-scope coverage | exact focused filter with `XPlat Code Coverage` | ✅ 66 passed; artifact SHA-256 `20C9130CF2555C554A9776ABD9ECE75428EA632852C1F5BC816174D9B4AF8366` |

The rebuilt DLL remains the expected 1,508,352-byte artifact with SHA-256 `38D63A20889044A0CAD763BBC78B6D223A3F6E255378F366080E0ACE27595818`. Build/test/coverage commands created no tracked generated or dependency change. The exact LF working hashes remain valid with zero CRLF: harness `2910260E8E704338AB6854C855E94A24010530A69BE44D3AA1B60229DA14AB0F`; receipt schema `25942EF6D419F468F77DB1067C2CAD5333FBB533F3468A81667B098DC5415A43`.

The stale 1115-pass/3-fail gate remains correctly retained as an invalid stale-artifact result. Fresh rebuilding and the 3/3 compatibility gate prove those failures are not current regressions.

### Behavioral / Spec Compliance Matrix

| Task-2.1 acceptance | Source and passing-test evidence | Result |
|---|---|---|
| T10-B definitive identity gating; no request before acceptance | `SendAuthenticatedAsync` obtains `GetDefinitiveSessionAsync` before request creation/send; `FetchPolicyAsync_WithoutDefinitiveSession_DeniesWithoutHttp` asserts zero sends | ✅ COMPLIANT |
| Accepted identity injects bearer; rejection does not replay | definitive-session and 401/403 theory tests assert bearer, one send, and generation invalidation | ✅ COMPLIANT |
| Stable idempotency across bounded retry | `PushUsageLogsAsync_RetryAfter_ReusesIdempotencyKeyAndRetriesOnce` asserts non-empty identical keys and exactly two attempts | ✅ COMPLIANT |
| Single-request/no amplification | single-request tests assert one normal request; production retry tests assert only one bounded retry and no retry on identity rejection/cancellation/timeout | ✅ COMPLIANT |
| Bounded transport retry/backoff/timeout | constructor limits attempts to 1–3; retry delay clamps to 30 seconds; transient/reconnect and timeout tests pass | ✅ COMPLIANT |
| Caller cancellation propagates | push-usage cancellation test passes, but `FetchPolicyAsync` has no caller-cancellation catch and its final `catch (Exception)` converts cancellation to a failed result | ❌ FAILING / PARTIAL |
| Redacted bounded outcomes on all in-scope delivery paths | policy, usage, alert, behavioral-event, heartbeat-unexpected, and fixed-code push-token paths have passing assertions; however heartbeat network failure still returns `$"Network error: {ex.Message}"` | ❌ FAILING / UNTESTED |
| Existing `IBackendClient` contract suffices unchanged | existing bounded result strings and cancellation-token parameters support the implementation; no interface diff is required | ✅ COMPLIANT |

The redaction defect is at `src/ControlParental.Service/BackendClient.cs:447-450`. The focused `SendHeartbeatAsync_WhenNetworkError_ReturnsFailed` test supplies a harmless message and only asserts that the result contains `"Network error"`; it does not assert absence of exception details. The cancellation defect is at `BackendClient.cs:245-255`: caller cancellation rethrown by the shared transport reaches the broad final catch. No focused test exercises caller cancellation for policy fetch.

### Correctness and Design Coherence

| Decision / invariant | Result | Notes |
|---|---|---|
| Service-owned transport with definitive identity authority | ✅ | Production composition uses `IBackendIdentityCoordinator`; request send follows successful identity resolution. |
| Transport retry is finite and separate from later coordinator ownership | ✅ | At most three attempts; current tests configure two. Task 2.2 coordinator ownership remains pending and is not claimed. |
| Retry identity remains stable | ✅ | One idempotency key is created outside the attempt loop. |
| Safe diagnostics by default | ❌ | Heartbeat network exception text leaks into the returned diagnostic. |
| Cancellation-aware lifecycle | ❌ | Policy fetch swallows caller cancellation. |
| No architecture replacement / no unrelated scope | ✅ | Existing client and contract retained; no 2.2+ implementation present. |

### TDD Compliance

| Check | Result | Evidence |
|---|---|---|
| Tests-first RED | ✅ | 2 failed / 0 passed; `red.txt` SHA-256 `22CB9C829E913F9E6020524CBB19807B964A07FDC03A1B3C68A1C5676849D4A0` independently re-hashed |
| Current GREEN | ✅ | fresh 66/66 focused and 1118/1118 full regression |
| Immutable pre-edit bytes | ✅ | all five `pre-*.cs` Git blob IDs exactly match their `3315ffb` blobs |
| Final apply evidence | ✅ | `green-focused-final4.txt` hash matches `70BFD848C8E2F6F087FC23971092C8369AF5200ED014D03C06F0B0EF8C9F83B4`; coverage-run hash matches the ledger |
| Saved immutable task patch/manifest | ⚠️ | no patch or manifest file exists in the claimed evidence directory; the exact prerequisite-anchored Git diff is available but remains a working-tree diff |
| Triangulation | ❌ | redaction and cancellation do not cover the heartbeat-network and policy-caller-cancellation variants |

### Test Layer Distribution and Assertion Quality

| Layer | Tests | Files |
|---|---:|---:|
| Transport/unit-component | 66 | 3 |
| Integration/E2E/live backend | 0 | 0 |

The tests invoke the real `BackendClient` with controlled HTTP and identity seams and assert returned results, request counts, headers, retries, delays, invalidation, and cancellation. They are appropriate transport-boundary tests, not disconnected/mock-only substitutes. No tautology, ghost loop, assertion without production invocation, or meaningless type-only assertion was found. Request-count assertions intentionally prove the no-amplification contract.

### Changed File Coverage

Fresh Cobertura artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd6-task21-independent-verify-20260819-coverage\4eaaae24-66df-4867-99f7-6ce8d5e20bd5\coverage.cobertura.xml`.

| Production scope | Line coverage | Branch/path coverage | Rating |
|---|---:|---:|---|
| Exact 18 added/replacement lines in `BackendClient.cs` | **18/18 = 100%** | no changed line introduces a Cobertura branch; all **9/9** changed exception outcome catches executed | ✅ Excellent |

The apply ledger's `8/8` path count is an undercount: the 18 changed lines form nine catch/return pairs. This does not cure the unchanged heartbeat-network leak or the unchanged policy-cancellation defect; changed-line coverage is not full behavioral acceptance.

### Issues Found

**CRITICAL**

1. `SendHeartbeatAsync` leaks raw `HttpRequestException.Message` through `ErrorMessage`, violating task 2.1 redaction and the safe-diagnostics requirement. The focused suite has no secret-bearing heartbeat network assertion.
2. `FetchPolicyAsync` swallows caller cancellation in `catch (Exception)` and returns `"Unexpected error"`, violating task 2.1 cancellation semantics. No focused policy-cancellation test covers this path.

**WARNING**

1. The apply evidence directory contains immutable pre-edit bytes and logs, but no saved task patch or manifest despite the ledger's manifest wording.
2. Existing package warnings remain (`NU1601`, `NU1701`); they are outside task 2.1 and non-blocking here.
3. Historical foundation TDD provenance warnings from the accepted baseline remain unchanged and outside this verdict.

**SUGGESTION**: None; remediation must be scoped to the two critical behaviors and their tests.

### Recommendation and Readiness

- **Task 2.1 checkbox**: reopen / set unchecked after the orchestrator authorizes status editing; this verifier did not edit task status.
- **Formal recorded progress**: filesystem and Engram currently say **6/11**.
- **Verified progress after this verdict**: **5/11** until task 2.1 is remediated and independently re-verified.
- **Tasks 2.2, 3.1, 3.2, 4.1, 4.2**: remain unchecked.
- **Full change / archive readiness**: **blocked**.

### Verdict

**FAIL — task 2.1 only.** Runtime gates, review budget, changed-line coverage, identity gating, idempotency, and bounded retry are strong, but two required delivery behaviors are demonstrably incomplete and untested. The full SDD change remains incomplete regardless of this scoped result.
