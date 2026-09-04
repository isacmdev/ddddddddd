# C1B2 Independent SDD Verification

**Branch / HEAD:** `feat/sdd7-4c2c1b2-stale-fault-hardening` / `1091d8732e17902752fa94fbb766a976c6407ba3`  
**Mode:** Strict TDD  
**Verdict:** **FAIL**

## Blockers

1. **Stale startup reaction faults are not suppressed.** `ReconcileDurableEffectsAsync` awaits startup `ResolveIssueAsync`/`AddIssueAsync` at `AntiTamperMonitor.cs:360/364` without a stale-generation catch. The post-success guard at line 367 cannot run after a fault. The only new reaction race test (`StartupReactionCompletionAfterStop_DoesNotSaveOrNotify`, test line 1328) covers success after Stop, not fault after Stop.
2. **Stale cancellation-ignoring startup Load faults are not suppressed.** `RehydrateAsync` awaits `LoadAsync` at `AntiTamperMonitor.cs:335` without a stale-generation catch; line 336 only guards successful completion. Active load faults correctly remain visible, but a Load fault arriving after Stop still escapes the lifecycle drain.
3. **Not every new stale catch has behavioral proof.** The notification-progress Save catch at `AntiTamperMonitor.cs:385` is uncovered (`hits=0`) in fresh external coverage. The notification test faults inside notification (line 381 catch); it never proves a notification succeeds, its subsequent progress Save ignores cancellation, then faults after Stop.
4. **Retry convergence proof is incomplete.** The two same-process retry tests prove exact collaborator keys and call order/counts, but do not restart from the state actually produced by either injected failure. The progress test only calls `store.Last.Validate()` and does not assert final pending/completed keys or second-restart no-op. Existing C1B1 restart tests start from manually constructed pending state, so they do not close this causal gap.

## Requirement Matrix

| Requirement | Evidence | Result |
|---|---|---|
| 1. Pre-effect Save failure: no effect; exact-key retry/restart convergence | New focused test proves no first effect and exact-key same-process retry | **PARTIAL** — no restart from failure-produced state |
| 2. Effect/progress-Save failure leaves valid pending state and converges in order | New progress-Save test plus inherited reaction/notification failure tests passed | **PARTIAL** — no asserted final IDs or restart from injected failure |
| 3. Startup reaction completion after Stop starts no Save/notification | `StartupReactionCompletionAfterStop_DoesNotSaveOrNotify` passed | **COMPLIANT for success; FAILING for required stale reaction fault suppression** |
| 4. Startup Save completion/fault after Stop emits no notification and remains restartable | `StartupReactionSaveFaultAfterStop_IsSuppressedAndRemainsPending` passed | **PARTIAL** — stale fault covered for reaction-progress Save; successful late Save/restart not exercised |
| 5. Startup notification completion/fault after Stop starts no progress Save | `StartupNotificationFaultAfterStop_IsSuppressedWithoutProgressSave` passed | **PARTIAL** — notification fault covered; subsequent notification-progress Save fault catch untested |
| 6. Active startup/admitted faults surface; admitted chain drains across Stop/Dispose | Relevant inherited focus 9/9; whole class 91/91 | **COMPLIANT** |
| 7. No retry/schema/timer/DI/Piranha drift; sole owner/no I/O under lock | Exact two-file diff; source inspection | **COMPLIANT** |

## Strict-TDD Audit

| Log | SHA-256 | Judgment |
|---|---|---|
| `sdd7-c1b2-red.log` | `9A10D51772E718095DC301B5DEDBBC72A5D9C15E18054459F06058A84A08F2A2` | Hash matches. Build/test harness ran, but all three failures occurred only when Stop/Dispose surfaced the already observed active fault. Those discarded stop-only expectations contradict the clarified contract and are **not valid product RED**. |
| `sdd7-c1b2-startup-red.log` | `3481F15D34010F570B26551FED084F308B221D984A3A07107783B2C077C863C9` | 0/3; all failed because tests incorrectly awaited Start success instead of its lifecycle cancellation. Not authoritative product RED. |
| `sdd7-c1b2-startup-red-final.log` | `2977124C9425E7668E697C677B754951D8823B92EF8BC3D2AFAFDDD3347005E2` | **Authoritative intended behavioral RED:** 2/3 passed; reaction-success-after-Stop failed because `SaveCalls` was 1, expected 0. It proves only that success path, not stale reaction/load/save/notification faults. |

Strict-TDD evidence is therefore incomplete for the stated C1B2 fault matrix.

## Fresh Gates

| Gate | Result |
|---|---|
| `git status`, `git diff --numstat/stat`, staged audit | Exact two tracked files; staged 0; `.codegraph/` untracked |
| `git diff --check` | Exit 0 |
| Service build `--no-restore -c Debug` | Exit 0; 0 errors; existing NU1601 |
| Exact five new C1B2 tests | 5/5 |
| Relevant inherited admitted/fault/lifecycle focus | 9/9 |
| Complete `AntiTamperMonitorTests` | 91/91 |
| Exact-exclusion Service safety net | 1,292/1,292; duplicate-ID notice |
| Portable-PDB test build | Exit 0; 0 errors; existing analyzer/package warning corpus |
| External AntiTamper coverage | 91/91; non-empty Cobertura |

Coverage artifact: `C:\Users\Usuario\AppData\Local\Temp\opencode\sdd7-c1b2-independent-coverage-20260826\b4b48324-d004-4a88-b5c4-45ab075b1c36\coverage.cobertura.xml`  
SHA-256: `0B4F6CE39C20AF8C23DC35DC46818B350CB0A157F402B12B777C7CDAD6CE9EBF`  
`AntiTamperMonitor`: 98.97% line / 84.93% branch. Changed executable lines: **25/26 = 96.15%**; uncovered changed line: **385**. Relevant method rates: RunOwnedStartup 100/100, Rehydrate 100/85.71, Reconcile 87.09/77.77, SaveEffectProgress 100/91.66, ExecuteDecisionChain 92.85/93.75, SaveState 100/50 (line/branch).

## Scope / Budget / Status

- Production: 29 additions + 13 deletions.
- Tests: 92 additions + 1 deletion.
- Exact CODE+TEST: **135/400**.
- No Handler, Program, schema, timer, DI, retry framework, or Piranha drift.
- Final tracked modifications remain the exact expected two files. `.codegraph/` remains untracked; this report is the only verifier-created artifact.

## Readiness

Task **4.2b cannot be checked**. The candidate **must not be offered for commit** until stale startup reaction/Load fault suppression and the missing notification-progress Save race are behaviorally covered, and failure-produced retry/restart convergence is proven.

Verification did not edit production code or tests and did not stage, commit, push, or create a PR.
