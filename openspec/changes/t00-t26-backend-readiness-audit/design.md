# Design: Reproducible T00–T26 Backend Readiness Audit

## Technical Approach

Execute a read-only pipeline against one frozen working-tree identity: capture evidence, assess partitions, then derive findings and a risk-ordered backlog. Runtime code, tests, the Git index, and generated directories are untouched; potentially writing commands run only in a disposable mirror.

## Architecture Decisions

| Decision | Alternative / tradeoff | Choice and rationale |
|---|---|---|
| Snapshot the dirty tree | Commit/tag mutates repository state | Record UTC time, canonical root, tool versions, `HEAD`, SHA-256 of `git diff --binary --full-index --no-ext-diff --no-textconv HEAD`, NUL-safe tracked status/modes, and an ordinal-sorted untracked manifest with lstat mode and raw SHA-256. Hash symlink targets without dereferencing. This preserves staged, unstaged, and untracked reality without normalization. |
| Isolate executable collectors | In-place commands alter outputs | Copy the frozen workspace to external temporary storage; run `--no-restore` build/tests, coverage, format, and analyzers there. Receipts contain command, cwd, environment/tools, time, exit, and stdout/stderr hashes. Blocked never means passed. |
| Evidence precedence | Prior reports can be stale | Current source/receipt wins. Prior claims are leads; deduplicate by `(partition,T-unit,dimension,location-or-contract,causal-claim)` and retract or mark unverifiable conflicts. |
| Strict admission | Broad labels inflate severity | Blocker needs a demonstrated release stop and causal evidence; Critical needs current evidence, material impact, and causal chain. Otherwise use candidate-blocker/pending, Major, Minor, external, or unverifiable. |

## Components and Data Flow

```text
Workspace -> Snapshotter -> identity + mirror -> Collectors -> receipts/citations
Roadmap -> Coverage validator -------------------------> Partition assessments
Prior leads ------------------------------------------> Evidence adjudicator
Evidence + assessments -> Finding registry -> backlog + partition report
```

Collectors cover source/architecture/dependencies; build/test/coverage; decomposed format/analyzers; and backend-facing contracts/configuration for auth, sync/data, notifications, observability, deployment, and secrets. Backend verdicts assess seams, failure behavior, and configuration without requiring completion; secret values are redacted. Group analyzer output by rule and ownership; volume alone is debt. Known build blockers isolate affected receipts as pending.

## Traceability Matrix

Every unit has exactly one primary partition; secondary cross-references remain links only.

| Partition | Primary units |
|---|---|
| P1 | T00 |
| P2 | T01, T02, T04 |
| P3 | T03 |
| P4 | T14, T17, T18, T22 |
| P5 | T19, T21 |
| P6 | T16, T23 |
| P7 | T05, T06, T07 |
| P8 | T08, T09, T11 |
| P9 | T10, T12, T13, T20 |
| P10 | T24, T25, T26 |
| P11 | T15 (intentional retired unit merged into T14; dependency disposition required) |
| P12 | Cross-cutting test evidence; no duplicated primary unit |

## Artifact Contracts

Audit output lives under `openspec/changes/t00-t26-backend-readiness-audit/audit/`: `snapshot.json`, `receipts/`, `traceability.md`, `partition-report.md`, and `remediation-backlog.md`.

A finding contains: stable content-derived ID; T-unit/partition; dimension; track (`client-defect`, `integration-infrastructure`, `technical-debt`, `backend-dependency`); severity and rationale; owner; exact `path:line` or receipt hash; impact; status; remediation; verification; priority; confidence; and evidence state (`confirmed`, `pending`, `external`, `unverifiable`, `retracted`). Partitions record verdict, dimension coverage, executable-evidence state, and gaps.

Backlog ordering is risk → correctness → backend readiness → maintainability. Each work unit forecasts authored changed lines and rollback. Chained PRs are recommended only above 400 lines; no PR or review lifecycle command is run.

## Operational Sequence and Failure Handling

1. Capture identity twice; abort on unexplained drift.
2. Validate T00–T26 uniqueness/completeness.
3. Collect source/contracts, then execute safe commands in the mirror.
4. Adjudicate evidence and partition verdicts; external backend claims cannot become client passes or confirmed local defects.
5. Validate finding/backlog schemas and recapture identity, allowing only declared audit artifacts.

Failure, timeout, missing SDK, mirror failure, or blocker produces a hashed receipt and `pending/unverifiable` scope. Rollback deletes only audit artifacts; mirror cleanup is best-effort.

## Verification Strategy

Verify manifest determinism and drift detection; NUL-safe unusual paths/modes; exact-once roadmap coverage; receipt hashes; blocker confidence isolation; stale-lead retraction; severity admission; analyzer grouping; four-track separation; backend-area coverage; backlog ordering; and the 400-line threshold. Compare pre/post repository and file identities to prove non-mutation.

## Threat Matrix

| Boundary | Applicability | Safe/failure behavior and planned RED probe |
|---|---|---|
| Documentation-like paths | Applicable | Treat every discovered path as data, never executable; disposable fixture includes `README.sh` and executable MDX. |
| Git repository selection | Applicable | Canonical workspace root is sole authority; reject cwd/root mismatch and external `-C`; fixture invokes from nested/wrong roots. |
| Commit state | Applicable | Capture staged, unstaged, empty-index, and untracked states without index writes; fixture asserts distinct stable identities. |
| Push state | N/A | No push or remote mutation exists. |
| PR commands | N/A | Only a line-count forecast is emitted. |

## Migration / Rollout

No migration. Produce artifacts in one local audit run; no unresolved design question blocks task planning.
