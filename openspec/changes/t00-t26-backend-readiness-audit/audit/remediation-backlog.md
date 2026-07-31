# Remediation Backlog

Ordered by risk, correctness, backend readiness, then maintainability. This is a planning artifact only; no remediation was applied.

| Order | Work unit | Track | Severity | Evidence | Owner | Verification | Forecast / rollback |
|---:|---|---|---|---|---|---|---|
| 1 | Restore a compilable baseline without changing audit scope | integration-infrastructure | Candidate blocker / pending | F-001, F-002; build/test receipts | Build and test owners | Build solution and run affected suites from a clean mirror; prove both blocker dispositions | 20–40 authored lines; remove only blocker-causing files or align tests with current seams |
| 2 | Make certificate pin mismatch fail closed | client-defect | Major | F-003 `Program.cs:93-105` | Service security owner | Pin-match, pin-mismatch, no-certificate, and ordinary TLS tests | 15–30 lines; revert HTTP handler callback and its focused tests |
| 3 | Redact WNS channel values from logs | client-defect | Major | F-004 `WnsHostedService.cs:50,71` | Service observability owner | Assert logs contain metadata only, never channel URI/token | 10–20 lines; revert logging change and tests |
| 4 | Prove pairing, JWT/RLS isolation, and complete policy contract in staging | backend-dependency | External / unverifiable | F-007 and `apis.md:158-197` | Backend + integration owners | Two-device isolation, refreshed JWT claim, full policy shape, unauthorized read rejection | 80–140 lines/tests plus backend migration work; rollback staging contract changes only |
| 5 | Add WNS backend registration/delivery and polling fallback proof | backend-dependency | External / unverifiable | F-007; `WnsHostedService.cs:38-73` | Backend + Windows integration owners | Channel registration/renewal, signal delivery, expired-channel cleanup, polling recovery | 100–180 lines; split into client, backend, and staging slices if forecast crosses 400 |
| 6 | Make grants and event ingestion idempotent across retries | backend-dependency | External / unverifiable | F-007; outbox `OutboxManager.cs:48-59` | Backend owner | Repeated approval/event delivery yields one grant/event and stable retry semantics | 100–180 lines; separate schema/function and client receipt slices |
| 7 | Validate secrets, deployment, ACL, integrity verdict, and degraded health | integration-infrastructure | Pending | F-006; `SecretStore.cs`, `AclHardener.cs`, `IntegrityVerdictHandler.cs` | Platform/security owners | Secret redaction, ACL checks, unavailable-verifier fail-safe, service recovery and health receipts | 80–160 lines; isolated platform/test work unit |
| 8 | Replace synchronous-over-async account flows | technical-debt | Minor | F-005 `AccountManager.cs:139,183` | Service owner | Async API tests and cancellation/deadlock regression test | 20–40 lines; revert account-flow refactor and tests |

## Forecast policy

Each future unit is independently reviewable. No forecast currently requires a chained PR; any combined backend/integration unit exceeding 400 authored changed lines MUST be split before implementation. The audit work unit itself is 320–390 authored lines forecast, below the accepted 400-line boundary.

## Accepted constraints

- Blocked collectors are recorded as blocked, never passed.
- External backend claims remain external/unverifiable until fresh backend or staging receipts exist.
- Complexity, duplication, and analyzer volume alone are not escalated to Blocker or Critical.
- Rollback of this audit deletes only `audit/` and the disposable mirror; it does not touch product, tests, configuration, index, or generated directories.
