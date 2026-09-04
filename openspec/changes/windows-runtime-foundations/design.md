# Design: Windows Runtime Foundations

## Technical Approach

Preserve A/B/C1/D and update only the C2 evidence contract. The existing framed, session-bound, fail-closed `NamedPipeServer`/`NamedPipeClient` seams remain the preferred test path. C2 closes only when the executable-managed gate and disposable signed native harness both pass.

## Architecture Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Classification | Classify every changed C2 addition by behavior: managed-deterministic for protocol/state/ownership/serialization/validation/retry/deadline logic; native-boundary for P/Invoke, marshalling, OS PID/SID/session/path lookup, WinTrust, and kernel pipe ACL/identity. | Mixed files must not turn testable security behavior into a native waiver. |
| Denominator | From compiler/PDB sequence points, derive executable lines within managed ranges; reconcile them with normalized Cobertura repository-relative source path+line records. | Makes the denominator reproducible rather than dependent on report shape. |
| Evidence | A non-executable addition may be excluded only in a path+line manifest with a specific reason: declaration, signature, brace, continuation, or generated async projection lacking an executable source point. Missing Cobertura mapping alone never proves exclusion; executable managed lines absent or unhit remain uncovered. | Prevents denominator gaming and distinguishes compiler gaps from untested behavior. |
| Async/branches | Union duplicate async mappings by normalized path+line; report exact branch union only when the evidence is defensible, otherwise report branch evidence as limited without changing line coverage. | Avoids double counting while preserving honest branch uncertainty. |
| Seams/native proof | Prefer existing factory/authentication seams. Any new production seam requires a named behavior, then candidate, classification, and denominator refresh. Native behavior remains a disposable signed harness gate. | Limits architecture to measurable testability needs. |

## Data Flow

`baseline 90a5a2a… → immutable candidate → changed additions → behavior classification → PDB sequence points + Cobertura path/line union → managed report`; the signed harness independently proves native behavior. Any failed gate keeps C2 closed.

Current diagnostic state is implementation evidence: 481 managed additions, 193 proven compiler/source-mapping gaps, 288 executable managed lines, 185 hit, and 103 mapped-but-unhit. The authoritative starting point is **185/288 = 64.236111%**. This is not threshold relaxation. Strict `>80%` requires at least 231/288 (more than 230.4), hence at least 46 additional executable hits, subject to denominator refresh after production changes.

## File Changes

| File | Action | Description |
|---|---|---|
| `openspec/changes/windows-runtime-foundations/design.md` | Modify | Define executable-managed denominator, C2 evidence, gates, and rollback. |
| Existing C2 production/test paths | No implementation change | Use current seams; preserve A/B/C1/D and unrelated scope. |

Per-file evidence records classification, additions, executable denominator, hits, unhit lines, reasoned exclusions, percentage, and branches. No historical aggregate (including 50.13%) substitutes for this report.

## Interfaces / Contracts

Reject malformed/oversized frames, wrong PID/SID/session/signer, unauthorized reconnect, and deadline failure before dispatch; repeat authorization on reconnect; serialize writes; bound cancellation and cleanup. Preserve all fail-closed requirements.

## Testing Strategy

Managed tests cover protocol, transitions, validation, ownership, ordering, serialization, reconnect, retries, and deadlines through existing seams. The signed harness proves trusted acceptance, fail-closed rejection, ordered handshake, authenticated dispatch, session binding, reconnect reauthorization, separate deadlines, and process/pipe/certificate cleanup. Unit D remains blocked until both C2 gates pass.

## Threat Matrix

| Boundary | Status and response |
|---|---|
| Documentation-like paths | N/A: no executable-document classification or execution boundary. |
| Git repository selection | N/A: no routing or Git command automation. |
| Commit state | N/A: no commit automation. |
| Push state | N/A: no push automation. |
| PR commands | N/A: no PR command composition. |
| Process/executable identity and native pipe | Applicable: accept only matching PID/session/SID/signer and ACL; reject and close before dispatch on failure. RED coverage is required in the signed harness. |

## Migration / Rollout

No protocol migration. No over-engineering: add no seam unless a named managed behavior is unreachable, then refresh candidate/classification/denominator. Roll back only C2 changes and evidence; preserve A/B/C1/D. C2 and Unit D blocking remain unchanged.

## Open Questions

None.
