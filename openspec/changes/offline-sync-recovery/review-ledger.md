# Judgment Day Review Ledger: offline-sync-recovery

## Final State

- Target: SDD6 foundation
- Round: final scoped Round 2 re-judgment
- Scoped Judge A verdict: `APPROVED`
- Scoped Judge B verdict: `APPROVED`
- New BLOCKER/CRITICAL findings: 0
- Fix rounds used: 2/2
- Further fix round: none
- State: `JUDGMENT: APPROVED ✅`

This approval validates the Judgment Day code-risk findings and their fixes. It does not authorize the separate approximately 1,200-line `size:exception`, a commit, push, PR, SDD completion, or archive.

## Scoped Round 2 Re-Judgment

| item | Judge A | Judge B |
| --- | --- | --- |
| Patch SHA-256 | `25EC0860C12F5D233F74489E50B9C5B91C5E8211175F8CC2AEDF7886A7572C55` matched | `25EC0860C12F5D233F74489E50B9C5B91C5E8211175F8CC2AEDF7886A7572C55` matched |
| Allowed-file scope | Confirmed; allowed files only | Confirmed; allowed files only |
| Verdict | `APPROVED` | `APPROVED` |
| New BLOCKER/CRITICAL | 0 | 0 |

The scoped patch contained only:

- `src/ControlParental.Service/SqliteSchemaBootstrapper.cs`
- `tests/ControlParental.Service.Tests/SchemaAdoptionTests.cs`

## Runtime Evidence

| evidence | result |
| --- | --- |
| RED | 5 failed, 6 passed |
| GREEN SchemaAdoption | 11/11 passed |
| Startup | 11/11 passed |
| Service build | 0 errors |
| Full Service regression | 1092/1092 passed |

## Findings

| id | bucket | source | lens | location | severity | status | assessment | evidence |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| JD-001 | confirmed | Judge A and Judge B | reliability | `src/ControlParental.Service/Program.cs:192-198`<br>`src/ControlParental.Service/CompiledModels/OutboxDbEntityEntityType.cs:18-24`<br>`src/ControlParental.Service/ControlParentalDbContext.cs:99-124` | CRITICAL | verified |  | Production uses a compiled EF model that lacks the SDD6 outbox columns while `OutboxManager` unconditionally queries and writes them; fresh production databases can be created with an incompatible schema. |
| JD-002 | confirmed | Judge A and Judge B | reliability | `src/ControlParental.Service/Program.cs:433-440`<br>`src/ControlParental.Service/ControlParentalDbContext.cs:99-124` | CRITICAL | verified |  | `EnsureCreated` does not upgrade an existing outbox table and no schema adoption/upgrader exists yet; existing installations can fail on missing lifecycle columns. |
| JD-003 | suspect-untouched | Judge A | resilience | `src/ControlParental.Service/ScheduledWorkService.cs:420-482`<br>`src/ControlParental.Service/OutboxManager.cs:93-101` | CRITICAL | info |  | Malformed or unknown legacy scheduler events may remain pending indefinitely because the bridge bypasses bounded `FailAsync` exhaustion/dead-letter. This untouched one-judge Round 1 suspect is pending separate manual triage, is not an active confirmed blocker, and does not authorize an automatic fix. |
| JD-004 | suspect-untouched | Judge B | risk/reliability | `src/ControlParental.Service/OutboxManager.cs:93-100` | CRITICAL | info |  | Legacy `MarkFailedAsync` uses id without an operation, claim, or lease guard and may requeue a stale active claim. This untouched one-judge Round 1 suspect is pending separate manual triage, is not an active confirmed blocker, and does not authorize an automatic fix. |
| JD-005 | info | Judge A | risk | `src/ControlParental.Service/OutboxManager.cs:207-220` | WARNING | info | real | A hard-coded caller string authorizes requeue. |
| JD-006 | info | Judge B | risk | `src/ControlParental.Service/OutboxManager.cs:33-52` | WARNING | info | real | An empty dedup key can collapse malformed distinct events. |
| JD-007 | info | Judge A and Judge B | delivery | repository candidate provenance/test files | WARNING | info | real | Mixed uncommitted provenance prevents historical slice attribution; this is review risk, not a runtime defect. |
| JD-R1-B-001 | confirmed-by-focused-triage | Judge B + focused triage; verified by Round 2 Judge A and Judge B | reliability | `src/ControlParental.Service/SqliteSchemaBootstrapper.cs` | CRITICAL | verified |  | Round 1 focused triage confirmed that unsupported or malformed schema metadata could be relabeled. The fix validates the exact `schema_version` metadata shape before outbox DDL/DML, fails closed for higher/lower/unknown/nonnumeric/NULL/duplicate metadata, adopts supported missing state transactionally, and preserves unrelated rows and rejected databases. Both scoped Round 2 judges verified the fix. |
| JD-R2-A-001 | info | Judge A | reliability/tests | `tests/ControlParental.Service.Tests/SchemaAdoptionTests.cs` | WARNING | info | theoretical/coverage-gap | The NULL case uses a nullable version-column shape, so it proves malformed table rejection rather than NULL data rejection in an otherwise valid metadata schema. This test-coverage gap is non-blocking. |
| JD-R2-B-001 | info | Judge B | resilience | `src/ControlParental.Service/SqliteSchemaBootstrapper.cs:51-54` | WARNING | info | real/future-scope | Cancellation during transactional adoption is wrapped and rethrown as `InvalidOperationException` instead of preserving `OperationCanceledException`. This is a real but non-blocking future-scope warning. |

Warnings remain canonical `WARNING`/`info` entries and do not drive another fix round. `JD-003` and `JD-004` remain untouched prior one-judge suspects pending separate manual triage; neither is an active confirmed blocker.

## Counts

| category | count |
| --- | ---: |
| New Round 2 BLOCKER/CRITICAL | 0 |
| Confirmed open critical | 0 |
| Verified critical | 3 |
| Prior suspect/info critical | 2 |
| Warning/info | 5 |
| Fix rounds used | 2/2 |

## Fixes Applied

- Round 1: restored compiled-model parity for the outbox schema.
- Round 1: added transactional, idempotent legacy schema adoption.
- Round 2: added fail-closed schema-version metadata validation and rejection preservation for `JD-R1-B-001`.

No other findings were fixed. The convergence budget is exhausted at 2/2 fix rounds, and no further fix round is authorized or required by this Judgment Day result.

## Skill Resolution

- `C:\Users\Usuario\.config\opencode\skills\judgment-day\SKILL.md`
- `C:\Users\Usuario\.config\opencode\skills\cognitive-doc-design\SKILL.md`

## Authorization Boundary

This terminal approval is limited to Judgment Day code-risk findings and fixes. It does not approve or authorize the separate approximately 1,200-line `size:exception`, commit, push, PR, SDD completion, or archive; each remains outside this judgment and requires its own gate.

## Judgment

JUDGMENT: APPROVED ✅
