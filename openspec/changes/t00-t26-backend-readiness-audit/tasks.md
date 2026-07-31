# Tasks: T00–T26 Backend Readiness Audit

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated authored changed lines (audit artifacts only) | 320–390 |
| Estimated review burden | Medium: five evidence artifacts plus receipts and cross-checks |
| 400-line budget risk | Medium |
| Chained PRs recommended | No |
| Suggested split | One local audit artifact work unit; future remediation is separate |
| Delivery strategy | auto-forecast |
| Chain strategy | pending |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: pending
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Focused test command | Runtime harness | Rollback boundary |
|---|---|---|---|---|
| 1 | Complete read-only audit and evidence package | Artifact/schema and receipt validation commands | N/A: audit has no application runtime | Delete only `audit/` artifacts |

## Phase 1: Identity and Scope

- [x] 1.1 Freeze UTC time, canonical root, tool versions, HEAD, tracked diff/status/modes, and hashed untracked manifest in `audit/snapshot.json`; **depends on none**. Accept only deterministic second-capture identity; rollback deletes the audit artifact.
- [x] 1.2 Build and validate `audit/traceability.md` for T00–T26 against P1–P12, including retired T15 disposition; **depends on 1.1**. Accept exactly-once primary coverage, duplicate/missing report, and partition evidence/gap columns.
- [x] 1.3 Inventory and isolate `App.UI/Program.cs` and `AgentLauncherLaunchSeamTests.cs` blockers in receipts and coverage notes; **depends on 1.1**. Accept explicit pending/unverifiable affected scope, never pass/fail inference; rollback removes blocker notes only.

## Phase 2: Evidence Collection

- [x] 2.1 Audit P1–P12 current source and contracts across every applicable risk/data, flow/state, complexity/optimization, architecture/SOLID/DRY, behavior-test, and backend-readiness dimension; **depends on 1.2–1.3**. Accept path:line citations or explicit evidence gaps.
- [x] 2.2 Run only safe collectors in the disposable mirror: source/dependency/architecture checks; `dotnet build --no-restore`; `dotnet test --no-build --verbosity normal`; coverage; decomposed format/analyzer checks; and backend contract/config checks. **Depends on 1.3**. Record exact command, cwd, tools, exit, and stdout/stderr hashes; blocked is never passed.
- [x] 2.3 Revalidate prior-report leads against the frozen snapshot and deduplicate by `(partition,T-unit,dimension,location-or-contract,causal-claim)`; **depends on 2.1–2.2**. Accept confirmed, pending, external, unverifiable, or retracted state for every lead.

## Phase 3: Findings and Deliverables

- [x] 3.1 Consolidate findings into `client-defect`, `integration-infrastructure`, `technical-debt`, and `backend-dependency` tracks with evidence-bound severity, owner, impact, confidence, status, and priority; **depends on 2.3**. Reject unsupported confirmed blockers.
- [x] 3.2 Produce `audit/partition-report.md` and `audit/remediation-backlog.md`, ordered risk → correctness → backend readiness → maintainability; **depends on 3.1**. Accept all partition verdicts, dimension coverage, executable state, gaps, verification, forecast, and rollback fields.
- [x] 3.3 Validate artifact schemas, receipt hashes, coverage/backlog ordering, reproducibility, and pre/post identity; verify only audit paths changed and clean disposable mirror; **depends on 3.2**. Accept no application, test, backend, index, or generated-directory mutation; rollback deletes only audit artifacts and temp mirror.
