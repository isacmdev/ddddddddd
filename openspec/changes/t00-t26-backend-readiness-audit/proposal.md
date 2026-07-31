# Proposal: T00–T26 Backend Readiness Audit

## Intent

Produce a reproducible, read-only T00–T26 evidence report and risk-prioritized remediation plan without changing application code or tests.

## Scope

### In Scope
- Audit security/privileges/data risk; functional flow/logic; complexity/optimization; Clean/Hexagonal Architecture, SOLID, and DRY; behavior-focused tests; and backend readiness across contracts/auth, synchronization/data, notifications, observability, deployment/configuration, and secrets.
- Preserve 12 bounded partitions: P1 build/tooling/packaging; P2 domain/rules/time; P3 persistence/outbox/offline; P4 sync/transport/contracts/auth; P5 WNS/polling; P6 integrity/anti-tamper/secrets; P7 foreground/usage/IPC; P8 enforcement/overlay/blocking; P9 health/scheduling/observability; P10 onboarding/consent/copy; P11 backend dependencies; P12 test evidence.

### Out of Scope
- Source/test remediation, backend completion, speculative contract freezing, and review lifecycle commands.

## Capabilities

### New Capabilities
- `backend-readiness-audit`: Snapshot identity, evidence rules, partition verdicts, classification, severity, and remediation priority.

### Modified Capabilities
None.

## Approach

Freeze the current workspace using timestamp, HEAD, tracked-diff identity, and a hashed untracked-file manifest. Revalidate prior-report leads against this snapshot; cite `path:line` or fresh command receipts, otherwise mark evidence pending or external evidence unverifiable here.

Classify every finding as (1) client-owned defect, (2) unready integration/infrastructure, (3) non-blocking technical debt, or (4) legitimate backend dependency. Backend-only work is decided case by case.

Blocker severity requires concrete release-stopping evidence; use candidate blocker/evidence pending, Critical, Major, or Minor otherwise. Complexity/duplication without demonstrated impact is non-blocking. Treat untracked `App.UI/Program.cs` and `AgentLauncherLaunchSeamTests.cs` as preconditions: classify and isolate their evidence impact without fixing them.

## Deliverables
- Snapshot/evidence manifest; 12-partition report showing roadmap coverage, verdict, findings, runnable-test evidence, and gaps.
- Remediation backlog ordered risk → correctness → backend readiness → maintainability. Use reviewable work units; recommend chained PRs only when later remediation forecasts above 400 changed lines.

## Affected Areas and Dependencies

Audit artifacts are new; runtime areas are read-only. Executable confidence depends on isolating the two known build blockers. Backend evidence may remain external.

## Risks and Rollback

Risks are snapshot drift, blocked tests, analyzer noise, and external evidence. Mitigate through immutable identity and explicit pending/unverifiable labels. Rollback deletes only audit artifacts.

## Success Criteria
- [ ] Every T00–T26 unit maps to a partition without silent omissions.
- [ ] Every finding has current evidence, severity rationale, track, owner, and priority.
- [ ] Executable coverage uses fresh runnable receipts; blocked/absent evidence is explicit.
- [ ] No source, test, or backend implementation is changed.

## Proposal Question Round

The launch brief resolves the question round: freeze the snapshot, treat both blockers as constraints, and auto-forecast later remediation. No proposal-level decision remains open.
