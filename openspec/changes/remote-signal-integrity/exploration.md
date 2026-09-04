# Exploration: remote-signal-integrity / 4C2A2 (corrected)

## Normative Spec Evidence
- `openspec/changes/remote-signal-integrity/specs/runtime-integrity/spec.md:31-33,40-43` requires durable monotonic ingress sequence/epoch, stale effects suppressed, work outside locks, and observed faults/cancellation. It does **not** require cross-process serialization, lock files, retries, or persistence-layer stale/equal admission.
- `openspec/changes/remote-signal-integrity/specs/offline-enforcement-safety-loop/spec.md:31-33,50-57` assigns durable state, deadline, latches, and crash convergence to the durable owner; `openspec/specs/offline-enforcement-safety-loop/spec.md:9-13,74-82` assigns deterministic session authority and generation correlation. No actual SPEC makes 4C2A2 responsible for rejecting stale/equal `Save` callers.
- `openspec/changes/remote-signal-integrity/specs/remote-signal-sync/spec.md:9-29` keeps retry/convergence ownership in the scheduler, not this store. Proposal/design/tasks are editable planning and were not treated as normative; their cross-process/monotonic-store MUST language is corrected rather than followed.

## Runtime Ownership and Call Flow
- `src/ControlParental.Service/Program.cs:368-390,402-425` registers one singleton `IIssueStore`, one singleton `IIntegrityVerdictHandler`, and one singleton `IAntiTamperMonitor` in the service process. `src/ControlParental.SessionAgent/Program.cs:35-49,53-66` is a separate IPC/overlay/foreground process and has no escalation store. Actual runtime evidence shows no multiple store owners.
- `src/ControlParental.Service/AntiTamperMonitor.cs:157-181,220-303` has per-generation admission, cancellation, and a `SemaphoreSlim` gate. Its integrity path is generation-owned at `AntiTamperMonitor.cs:556-618`; effects are admitted through `AntiTamperMonitor.cs:706-721,768-821`. `IntegrityVerdictHandler` serializes policy transitions under `IntegrityVerdictHandler.cs:185-205,321-341`; no stale/equal `Save` caller or concurrent multi-process owner was found. The search found only `FileIssueStore.SaveAsync` callers at `FileIssueStore.cs:73,105`.
- Retained from the existing exploration: the service singleton boundary, handler in-memory escalation state (`IntegrityVerdictHandler.cs:116-130`), production effect ownership, and affected runtime/test areas. Corrected: its claims that planning artifacts mandated cross-process locks, process tests, and store-level monotonic admission.

## Corrected Recommendation
Implement one DI singleton `IIntegrityEscalationStateStore` file store with a per-instance `SemaphoreSlim`, not cross-process lock files. Retain envelope/state validation (`src/ControlParental.Domain/IntegrityEscalationState.cs:25-130`), SHA-256 identity-derived paths, source-generated JSON, direct-open missing-vs-access/I/O classification, same-directory unique temp + write/flush + atomic move + best-effort cleanup, and old-document preservation on cancellation/failure before replacement. Keep identities isolated.

Persistence itself need not compare/reject monotonic epoch/sequence/equal-version values: no actual SPEC assigns that admission to 4C2A2 and no stale `Save` caller exists. 4C2C/AntiTamper remains the owner of monotonic state/effects: handler transitions are serialized (`IntegrityVerdictHandler.cs:185-205`), and generation/effect admission and draining are owned by `AntiTamperMonitor.cs:240-303,706-721`. This preserves owner-level monotonicity without defensive store policy. Use at most one narrow internal write-failure seam only if deterministic atomic-failure proof otherwise cannot be obtained; do not add an injectable filesystem abstraction.

## Retain / Remove
- **Retain:** envelope/identity validation; SHA-256 per-identity path; direct-open missing distinction; source-generated JSON; atomic durable write and cleanup; cancellation/failure preservation of the old document; identity isolation; singleton DI ownership; existing generation/handler ordering and production owner boundaries.
- **Remove:** cross-process lock files and process tests; retries/backoff/timeouts; retained lock artifacts; persistence stale/equal admission; defensive migration/backups/extra abstractions. No actual SPEC mandates cross-process serialization, and no actual SPEC makes 4C2A2 own stale/equal admission.

## Budget
Projected **300–340 CODE+TEST**: the final proportional line audit is 330 across 13 minimum behavior cases and readable tests, with no scope added. Do not spend budget on process coordination, retries, or speculative seams.

## Risks
- Filesystem crash durability remains bounded by the platform despite flush/atomic move; preserve old-or-new semantics and surface non-missing I/O errors.
- Owner/store integration must preserve persist-before-effect and cancellation observation; these are runtime-owner concerns, not reasons to add store-level speculation (`openspec/specs/offline-enforcement-safety-loop/spec.md:53-57`; `AntiTamperMonitor.cs:706-721`).
- The prior exploration’s cross-process and monotonic-admission conclusions came from `design.md:11-14,49-64` and `tasks.md:49-51`, not actual specs; those conclusions are intentionally corrected while valid ownership and atomic-I/O observations remain.

## 4C2B Exploration: Exact-Key Enforcement Dedupe (approved A2 `47e712966e795e50bc3df5c2b129239173cdac5b`)

### Current State and Verified Flow
- `src/ControlParental.Domain/IIssueStore.cs:3-31` defines the only production `IIssueStore`; `src/ControlParental.Service/FileIssueStore.cs:6-193` is its only production implementation. `UpsertActiveAsync` is currently `(IssueKey, severity, evidence, observedAt, CancellationToken)` at `IIssueStore.cs:7-12` and `FileIssueStore.cs:38-43`; existing calls are positional or Moq positional setups (`EnforcementLevelMonitor.cs:426-427,585-586`; `DurableIssueStoreTests.cs:22-24,46,73-74`; `EnforcementLevelMonitorSafetyBranchTests.cs:33-37`; `IntegrityRuntimePathTests.cs:200`).
- `FileIssueStore` uses a singleton-owned `Dictionary<IssueKey, DurableIssue>` and one `SemaphoreSlim` (`FileIssueStore.cs:10-12`), persists `IssueDocument(int Version, DurableIssue[] Issues)` (`:144-161,193`), and bounds records at `MaximumRecords=1024` (`:8-9,51-59`). `DurableIssue` has no replay metadata (`IIssueStore.cs:21-31`); absent JSON members load as null/default-compatible values, so one nullable member needs no migration.
- Service composition registers one `IIssueStore` singleton (`Program.cs:366-390`), and one singleton `EnforcementLevelMonitor` owns the store reference. `StartAsync` restores active durable issues (`EnforcementLevelMonitor.cs:118-145,519-568`); evaluation persists health issues and calculates the enforcement level (`:251-281,571-613`). B's boundary is the keyed persistence plus keyed monitor behavior; `AntiTamperMonitor` remains outside B and C later supplies owner integration.
- A replay currently mutates `OccurrenceCount`/`Revision` (`FileIssueStore.cs:61-73`) and re-fires the monitor effect path (`EnforcementLevelMonitor.cs:426-440`). B must deduplicate both durable mutation and the keyed monitor effect boundary.

### Recommended Minimum API, Data, and Algorithm
- Keep legacy `IIssueStore.UpsertActiveAsync(..., CancellationToken)` unchanged. Add overload `Task<IssueUpsertResult> UpsertActiveAsync(..., string? idempotencyKey, CancellationToken cancellationToken = default)`, with `IssueUpsertResult(DurableIssue Issue, bool IsReplay)`. This preserves the legacy return type and positional cancellation-token calls without a broad request type.
- Add only optional final `LastIdempotencyKey = null` to `DurableIssue`. Missing JSON continues to load as null with no explicit migration. Null/empty keys delegate to unchanged legacy behavior. Whitespace or nonempty keys longer than 256 characters are rejected.
- Under the existing gate/load, do the O(1) dictionary lookup. For a nonempty key, do one bounded O(n), `n <= 1024`, scan of persisted records. If the same key is found, exact `IssueKey` including scope, `Severity`, and `LastEvidence` returns the existing record unchanged with `IsReplay=true`, even if later resolved and regardless of the newly supplied `observedAt`; replay retains the original `LastObservedAt`. A same-key mismatch in `IssueKey` including scope, severity, or evidence throws before mutation/save. `observedAt` is not a conflict dimension because `EnforcementLevelMonitor` generates `WallClockNow` on every call; excluding it enables crash replay without persisted request timestamps or request objects. First apply saves `LastIdempotencyKey` and returns false. No extra index/history/fingerprint/registry.
- Keep legacy `IEnforcementLevelMonitor.AddIssueAsync(..., CancellationToken)` unchanged. Add the keyed overload with `string? idempotencyKey` before `CancellationToken`; `EnforcementLevelMonitor` uses the keyed store result, projects issue/health, and suppresses only duplicate notification/effect work on replay. Replay leaves occurrence, revision, level, and events unchanged. B must not modify `AntiTamperMonitor` or pass `VerdictDecision` keys; C calls this overload.

### Affected Files and Exact Test Equivalence Classes
- `src/ControlParental.Domain/IIssueStore.cs` — keyed overload, `IssueUpsertResult`, and nullable durable metadata.
- `src/ControlParental.Domain/IEnforcementLevelMonitor.cs` — keyed `AddIssueAsync` overload while preserving the legacy overload.
- `src/ControlParental.Service/FileIssueStore.cs` — keyed atomic decision, bounded conflict scan, optional JSON member, and unchanged legacy path.
- `src/ControlParental.Service/EnforcementLevelMonitor.cs` — keyed result projection and replay-only effect suppression.
- `tests/ControlParental.Service.Tests/DurableIssueStoreTests.cs` and `EnforcementLevelMonitorSafetyBranchTests.cs` — focused persistence and monitor boundary evidence.
- Minimum classes: legacy unchanged including empty key; first keyed apply; exact replay with changed `observedAt` retaining the original `LastObservedAt`; one scope conflict; severity/evidence conflicts only as needed for branches; restart replay with legacy missing field; concurrent same-key calls; and monitor first/replay exactly-once behavior. Capacity reuse is optional and only justified if existing capacity ordering is touched.

### Coverage, Complexity, Estimate, and Exclusions
- Strict RED→GREEN→REFACTOR: write behavior tests first. Changed production lines require >80% meaningful line coverage and critical branch inspection for legacy/keyed, replay/conflict, scan hit/miss, and first-apply/replay effect paths.
- Corrected target: **300–360 CODE+TEST**, hard ≤400. The earlier 320–380 range included work now explicitly excluded from B. No AntiTamper, Program, outbox, state store, C, Unit 5, rehydration/deadline, migration, extra index, or cross-process coordination.
- No real blocker found. B proves the keyed store/monitor boundary; C later supplies the owner key and integration.
