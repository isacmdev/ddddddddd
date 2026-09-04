# Proposal: Remote Signal Integrity (SDD7 Unit 4 Remediation)

## Intent

Preserve approved Unit 4C policy while delivering durable escalation without replay inflation or restart races. B is approved at `1fc5b60dc801bbb12d25f2131cb17201c4f04b47`, based on A2 `47e712966e795e50bc3df5c2b129239173cdac5b`. Independent verification rejected the second, green 342/400 C1 rebuild as commit-ineligible: stale timer/new generation, rollback extension, coupled reaction/notification completion, recovery `AddIssue` versus `ResolveIssue`, non-definitive sequence mutation, five untested scenarios, and a false Batch1 GREEN ledger remained. Completely missing state remains valid first-start behavior. The clean closure is split because the combined scope exceeds the hard <=400 limit.

## Scope

### In Scope
- Preserve approved A (`38de466`), B/C ordering, pure decisions, barriers, bounded effects, and notification keys.
- 4C2A: complete immutable domain contract, including `IIntegrityEscalationStateStore`, envelope/error taxonomy, envelope version/schema validation, and exhaustive bounded invariant validation only; Domain tests cover exact boundaries and reaction-only behavior; estimate 320–340 CODE+TEST for exhaustive envelope/state contract validation.
- 4C2A2: approved at `47e712966e795e50bc3df5c2b129239173cdac5b`; implement the minimum evidence-based file boundary with a DI-singleton `FileIntegrityEscalationStateStore`, a per-instance `SemaphoreSlim` matching the existing `FileIssueStore` pattern, identity-SHA-256-derived paths, direct-open actual-missing-vs-I/O classification, source-generated JSON, and envelope/identity validation. Use same-directory temp/write/flush/atomic move with `finally` cleanup; cancellation or failure preserves the prior complete file, and distinct identities remain isolated. No store-level epoch/sequence/equal-progress comparison; monotonic policy remains 4C2C AntiTamper sole-owner/generation responsibility. Service tests; estimate 300–340 CODE+TEST after a final proportional line audit of 330 across 13 minimum behavior cases and readable tests, with no scope added; hard <=400.
- 4C2B: on `feat/sdd7-4c2b-idempotent-enforcement` from approved A2, add a clean Domain-contract/Service-implementation keyed boundary while preserving legacy store and monitor overloads. The store overload returns `IssueUpsertResult(DurableIssue Issue, bool IsReplay)` and takes `string? idempotencyKey` before cancellation; optional final `DurableIssue.LastIdempotencyKey = null` preserves legacy JSON. Under the existing gate, use O(1) issue lookup plus one bounded O(n), `n <= 1024`, key scan; exact same-key `IssueKey` including scope, severity, and evidence replays the unchanged durable record even if resolved or the new `observedAt` differs, retaining its original `LastObservedAt`; mismatch in those keyed dimensions conflicts before mutation/save, and null/empty stays legacy. The keyed monitor overload uses the result and suppresses `IssueDetected`/reaction projection on replay. C later supplies the owner key; B does not integrate `AntiTamperMonitor`.
- 4C2C1A: keep `IntegrityVerdictHandler` pure for snapshot/restore, definitive-only epoch/sequence/counters, rollback/timing validation, and phase/recovery semantic decision identity. Handler and tests only.
- 4C2C1B: keep `AntiTamperMonitor` as sole async owner for rehydrate-before-remote, missing-valid/fault/cancel fail-closed handling, save-before-effect, independent reaction/notification reconciliation including recovery `ResolveIssue`, generation/effect monotonicity, and lifecycle faults. Pass `VerdictDecision.ReactionIdempotencyKey` and notification keys unchanged. No deadline timer or `Program`.
- 4C2C1C: add only actual monotonic relative deadline ownership: before/at/after behavior, restart/no extension, rollback fail-closed, trust cancellation, stale generation, shutdown, and fault handling. No `Program`.
- 4C2C2: add only production composition: register the singleton state store in `Program`, prove real container resolution and monitor startup/cleanup, then run the integrated Unit 4 gate.
- Persist state and pending effect identity before effects. Three accepted definitive `revoked` observations start one five-minute deadline; trust cancellation/recovery and non-definitive preservation remain unchanged. `TimingValid=false` Pending is rejected by A; C detects runtime wall-clock invalidity and transitions fail-closed, never persisting incomplete Pending state. The design's 19-field record is retained.

### Out of Scope
- Unit 5 remains receipts/compatibility cleanup, not first enforcement ownership; no WNS/Realtime, packaging, or readiness claims.
- 4C2B excludes `AntiTamperMonitor`, `Program`, outbox, escalation state store, 4C2C/Unit 5 integration, migrations, and unbounded key history/index/registry.

## Capabilities

### New Capabilities
None.

### Modified Capabilities
- `runtime-integrity`: durable accepted-observation ordering, snapshot rehydration, and exact replay semantics.
- `offline-enforcement-safety-loop`: durable one-shot escalation, timing fail-closed behavior, and production ownership.

## Approach

Auto-chain: approved A (`38de466`) → approved A2 (`47e712966e795e50bc3df5c2b129239173cdac5b`) → approved B (`1fc5b60dc801bbb12d25f2131cb17201c4f04b47`) → 4C2A → approved 4C2A2 → 4C2B → 4C2C1A → 4C2C1B → 4C2C1C → 4C2C2; Unit5 is deferred. Every autonomous child remains hard-bounded to <=400 CODE+TEST lines, with no exception:

Each slice is rebuilt from its clean approved predecessor using auditable behavior-first logs, independent verification, and explicit commit authorization; no patch reuse.

| Slice | Base | Boundary | Budget |
|---|---|---|---:|
| `feat/sdd7-4c2a-escalation-state` | approved A `38de466` | Immutable bounded domain contract, envelope version/schema validation, errors, exact-boundary/reaction-only contract tests | 320–340 |
| `feat/sdd7-4c2a2-escalation-store` | approved 4C2A | Singleton file store, identity isolation, atomicity, cancellation, validation, and Service tests; no store-level monotonic admission | 300–340 CODE+TEST (hard <=400) |
| `feat/sdd7-4c2b-idempotent-enforcement` | approved A2 `47e712966e795e50bc3df5c2b129239173cdac5b` | Exact keyed result/store/monitor boundary; no AntiTamper integration until C | 300–360 CODE+TEST (hard <=400) |
| `feat/sdd7-4c2c1a-handler-durable-state` | B `1fc5b60d` | Pure handler snapshot/restore, definitive-only counters/sequence, rollback/timing validation, and semantic decision identity; handler + tests only | 120–220 (hard <=400) |
| `feat/sdd7-4c2c1b-owner-reconciliation` | approved C1A | AntiTamper rehydrate/reconciliation, fail-closed faults, save-before-effect, exact keys, independent effects, generation/effect monotonicity, and lifecycle faults; no timer/Program | 250–380 (hard <=400) |
| `feat/sdd7-4c2c1c-deadline-owner` | approved C1B | Actual monotonic relative deadline timer, boundary/restart/no-extension/cancel/stale/shutdown/fault behavior; no Program | 180–300 (hard <=400) |
| `feat/sdd7-4c2c2-runtime-composition` | approved C1C | Program singleton registration, real container resolution, monitor startup/cleanup, and integrated Unit 4 gate | <=200 (hard <=400) |

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| State domain/store | New | 4C2A owns the complete contract; 4C2A2 provides its robust durable implementation. |
| `IIssueStore`, `IEnforcementLevelMonitor`, `FileIssueStore`, `EnforcementLevelMonitor` | Modified | Clean Domain/Service keyed result boundary and bounded replay protection; tests stay in the two focused Service test files. |
| `IntegrityVerdictHandler.cs` and handler tests | Modified by 4C2C1A | Pure durable-state snapshot/restore and semantic decision identity. |
| `AntiTamperMonitor.cs` and owner tests | Modified by 4C2C1B/1C | Rehydration/reconciliation and deadline ownership; no Program wiring. |
| `Program.cs` and composition tests | Modified by 4C2C2 | Singleton store registration, real production resolution, monitor startup/cleanup, and integrated composition proof. |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Crash replay or clock rollback repeats/extends enforcement | Med | Persist pending state first; monotonic deadline; exact-key dedupe; fail closed. |
| Missing-vs-I/O ambiguity or interrupted replacement loses usable state | Med | Direct-open classification; same-directory atomic move; flush and `finally` cleanup; retain the prior complete file on cancellation/failure. |

## Rollback Plan

Revert 4C2C2, then 4C2C1C, 4C2C1B, and 4C2C1A, followed by 4C2B, 4C2A2, and 4C2A back to approved A/A2/B. Retain legacy issues and disable only new escalation effects.

## Dependencies

- Preserve approved A (`38de466`), B/C decisions and the exact auto-chain feature-branch-chain.
- A2 is approved at `47e712966e795e50bc3df5c2b129239173cdac5b`; B is approved at `1fc5b60dc801bbb12d25f2131cb17201c4f04b47` from A2, and C1A uses that B base. C1B and C1C use clean approved predecessors; C2 follows approved C1C.
- Review inputs: undefined `EscalationPhase` is invalid; an arbitrary `PolicyVersion` maximum of 4 is invalid—the existing convention permits any positive `int`, with higher versions overriding lower ones, and A validates `>0` within the type's natural upper bound; `File.Exists` can collapse access failure to missing. There is no supported cross-process writer or lock, and the specs require owner monotonicity—not compare-and-swap storage—so lock files, process tests, retry/backoff/timeouts, a filesystem abstraction, distributed coordination, and migration are excluded.

## Success Criteria

- [ ] State rehydrates deterministically; pending deadlines never gain five minutes or extend on rollback.
- [ ] Same-key crash replay causes one reaction/notification and no occurrence increment; null/empty keys retain legacy behavior.
- [ ] 4C2B exceeds 80% changed-production line coverage with meaningful legacy/keyed, replay/conflict, scan, concurrency, restart, and monitor branches; no duplicate hosts.
- [ ] C1A proves pure durable-state semantics; C1B proves owner rehydration, fail-closed faults, save-before-effect, exact-key independent reconciliation, recovery `ResolveIssue`, and lifecycle monotonicity; C1C proves real deadline boundaries, restart/no extension, cancellation, stale generation, shutdown, rollback, and fault behavior. Each slice exceeds 80% meaningful changed-production coverage where applicable; C2 proves real Program DI/startup/cleanup and the integrated Unit 4 gate.
- [ ] 4C2A succeeds only with envelope version/schema validation and exact-boundary/reaction-only contract coverage; its estimate is 320–340 CODE+TEST for exhaustive envelope/state contract validation.
- [ ] 4C2A2 uses the bounded singleton file-store boundary, preserves the prior complete file on cancellation/failure, and remains 300–340 CODE+TEST with a hard <=400 limit.
- [ ] Monotonic epoch/sequence/effect-progress policy remains owned and tested by C1B/C1C, not the store; missing-state first start remains valid, and no fields, APIs, framework, retries, locks, migration, patch reuse, or keyed resolve are added.
