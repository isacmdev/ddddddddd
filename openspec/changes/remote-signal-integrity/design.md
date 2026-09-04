# Design: Remote Signal Integrity — Unit 4C Durable Escalation

## Technical Approach

Chain approved B `c183149` → 4C2A (contract/validation, 320–340 CODE+TEST) → 4C2A2 (robust state store, ≤390) → existing 4C2B (idempotent enforcement) → existing 4C2C (owner rehydrate/deadline) → Unit 5. Approved A/A2/B ordering, pure decisions, generation barriers, bounded effects, notification keys, and all existing scenarios remain unchanged. `IntegrityVerdictHandler` stays pure; `AntiTamperMonitor` remains the sole asynchronous owner.

## Architecture Decisions

| Decision | Choice | Alternatives rejected | Rationale |
|---|---|---|---|
| 4C2A boundary | Domain-only immutable `IntegrityEscalationState`, immutable envelope, closed `EscalationPhase`, typed validation errors, exhaustive bounded validation, and the `IIntegrityEscalationStateStore` contract. | File implementation, JSON, timers, or service wiring in 4C2A. | Makes the complete contract independently reviewable and keeps the 320–340 line audit boundary safe. |
| 4C2A2 persistence | Implementation of `IIntegrityEscalationStateStore` using source-generated JSON. Identity filenames are SHA-256 hashes; identity never appears in paths or errors. | Reuse `IIssueStore`, `File.Exists`, or in-process `SemaphoreSlim`. | The current `FileIssueStore` has per-instance locking and collapses access failures into “missing”; neither is safe across processes. |
| Cross-process serialization | Same-directory hashed lock file opened with `FileMode.OpenOrCreate`, `FileShare.None`; OS handle release makes crashes recoverable. Acquisition has a fixed maximum wait, cancellation, and deterministic backoff. Timeout/access/I/O failures are typed non-missing store failures. The retained O(1)-per-identity lock file is redacted and is not deleted during normal operation. | Named semaphore that can remain permanently decremented after process death, or deleting lock files during normal operation. | Windows file-handle ownership provides crash recovery without identity leakage, permanent lock state, or inode-coordination races. |
| Admission | Under the inter-instance lock, reload current state and apply monotonic admission before atomic replacement. | Trust caller-cached state. | Prevents stale instances from regressing durable progress. |

## State Contract and Validation

The positional constructor may remain for compatibility:

```csharp
record IntegrityEscalationState(
 string IdentityScope, int PolicyVersion, int SchemaVersion, long Epoch, long Sequence,
 int RevokedStreak, int TrustStreak, EscalationPhase Phase,
 DateTimeOffset? DeadlineOriginUtc, DateTimeOffset? DeadlineDueUtc,
 DateTimeOffset MaxWallClockSeenUtc, bool TimingValid, bool FiredLatch, bool RecoveryLatch,
 string? PendingReactionId, string? CompletedReactionId,
 string? PendingNotificationId, string? CompletedNotificationId);
record IntegrityEscalationStateEnvelope(int DocumentVersion, int SchemaVersion,
 IntegrityEscalationState State);
interface IIntegrityEscalationStateStore {
 Task<IntegrityEscalationState?> LoadAsync(string identity, CancellationToken ct = default);
 Task SaveAsync(IntegrityEscalationStateEnvelope value, CancellationToken ct = default);
}
// Both immutable contract types expose Validate(); failures use the typed taxonomy.
void IntegrityEscalationStateEnvelope.Validate();
```

`IntegrityEscalationStateEnvelope.Validate()` rejects unsupported document/schema versions, null or incomplete state, and envelope/state schema mismatch, then delegates to `State.Validate()`; all failures are typed. `EscalationPhase` is a closed enum (`Normal`, `Pending`, `Degraded`); undefined values are rejected. Identity and effect IDs are non-whitespace and bounded. `PolicyVersion` is any positive `int`, consistent with the existing `Policy.Validate` contract; `int` itself supplies the representation bound, with no additional policy-counter cap. `SchemaVersion`, `DocumentVersion`, `Epoch`, and `Sequence` follow the current document contract, with epoch/sequence in `0..bound`. The contract names `DefinitiveRevokedThreshold`, `DefinitiveTrustRecoveryThreshold`, and `EscalationDeadlineDelay = TimeSpan.FromMinutes(5)`; validation and behavior never rely on magic `3` or `5` literals. Clocks are UTC and non-default.

The exact accepted state boundary is: Normal has no deadline or fired latch; Pending requires exactly one complete origin and due equal to origin plus `EscalationDeadlineDelay`, `FiredLatch=false`, revoked streak equal to `DefinitiveRevokedThreshold`, and `TimingValid=true`; Degraded requires fired and no active deadline. Reaction precedes notification, completed keys match immutable pending keys, and the supported reaction-only shapes are either a pending reaction with no notification keys or a completed reaction with no notification keys. Impossible completed/pending key shapes are rejected.

## Data Flow and Store Semantics

```text
accepted verdict → pure handler snapshot → AntiTamper persist → reaction → persist → notification → persist
                                      ↘ rehydrate: reaction, notification, deadline
```

4C2A2 writes a same-directory temp file, serializes with the source-generated context, flushes to disk, atomically replaces, then cleans up. Cancellation or failure before replacement leaves the previous complete document intact. Reads observe an atomic old-or-new document and never partial JSON. Only an actual missing state/path returns `null`; access/I/O faults surface as typed non-missing failures. No legitimate migration exists: unsupported versions fail, and no deadline is invented.

While locked, lower epoch or sequence is rejected. Equal epoch+sequence may advance only `CompletedReactionId`, then `CompletedNotificationId`, with matching immutable pending keys; it cannot clear/change completed progress or mutate policy, phase, deadline, latches, or counters. Higher epoch/sequence is structurally validated. Different identities are isolated.

## Delivery, Files, and Estimates

| Slice | Base | Files / safe boundary | Estimate |
|---|---|---|---:|
| 4C2A | `c183149` | Domain state/envelope/enum/errors/validator and `IIntegrityEscalationStateStore` contract plus Domain tests; no file/JSON/service implementation | 320–340 |
| 4C2A2 | approved 4C2A | File-store implementation, source-generated JSON, hashed lock/atomic I/O, monotonic admission, and Service tests; use `FileIssueStore` only as evidence | ≤390 |
| 4C2B | existing A2 | Existing exact-key enforcement and legacy null/empty compatibility; replay does not increment occurrence | 320–380 |
| 4C2C | approved B | Existing pure snapshot, AntiTamper rehydrate/reconcile/deadline and production owner | 360–395 |

## Testing Strategy

Use deterministic seams, no sleeps: exhaustive `Envelope.Validate()` failures for unsupported document/schema, null/incomplete state, schema mismatch, delegated state errors, and exact accepted-state boundary matrices; source-generated round trips; explicit reaction-only shapes and key-order matrices; missing-vs-I/O classification; corrupt/unsupported/wrong-identity documents; cancellation and injected write/flush/replace failures; old-or-new reads; distinct-identity isolation; two store instances and separate processes; crash-released lock acquisition; epoch/sequence and same-version progress rules. Inject bounded lock/open seams to test fixed maximum wait, deterministic backoff, cancellation, timeout, and access/I/O failures without ACL-dependent flakes. Preserve all existing handler, deadline, trust/recovery, non-definitive, generation, stale-callback, and exact reaction-before-notification scenarios. Keep all file-open seams internal, bounded, and deterministic rather than using sleeps.

## Migration / Rollback

No migration is required or invented. Deploy 4C2A, then 4C2A2, 4C2B, and 4C2C in chain order; Unit 5 remains receipts/compatibility only. Roll back 4C2C → 4C2B → 4C2A2 → 4C2A to approved B, retaining legacy issues and disabling only new escalation effects.

## Open Questions

None.
