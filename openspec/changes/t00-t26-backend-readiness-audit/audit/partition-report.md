# Partition Report

## Executive assessment

The client contains substantial local contracts, adapters, persistence, enforcement, onboarding, and test intent. This snapshot cannot claim executable pass coverage: the disposable mirror's build, no-build tests, coverage, and analyzer build all exited non-zero, while decomposed whitespace and style verification exited zero. Backend integration remains partly external and must not be inferred from client source.

| Partition | Scope | Verdict | Dimensions covered | Executable evidence | Gaps |
|---|---|---|---|---|---|
| P1 | Build, tooling, packaging | **Blocked** | architecture; test evidence; deployment/config | Build receipt `build-no-restore` exit 1 | Missing assets in mirror plus known untracked App.UI entry-point blocker; packaging not proven |
| P2 | Domain, rules, time | **Source-ready / execution-pending** | risk/data; flow/state; complexity; architecture; behavior tests | No passing receipt | Deterministic behavior is represented in source/tests but cannot be claimed passing |
| P3 | Persistence, outbox, offline | **Source-ready / execution-pending** | data; flow/state; architecture; backend sync seam; behavior tests | No passing receipt | SQLite migrations, atomic guards, and outbox retry need execution |
| P4 | Sync, transport, contracts, auth | **Integration-unready** | security/auth; data; failure behavior; architecture; backend readiness | No passing receipt; external backend | JWT/RLS/policy completeness and backend contract behavior are external |
| P5 | WNS and polling | **Integration-unready** | flow/state; failure behavior; observability; backend readiness | No passing receipt; WNS external | WNS identity, registration, delivery, expiry cleanup, and foreground realtime are unproven |
| P6 | Integrity, anti-tamper, secrets | **Client partial / backend pending** | security/privileges; data; failure behavior; secrets; behavior tests | No passing receipt | Remote integrity verdict and OS ACL/secret-store behavior require environment/staging evidence |
| P7 | Foreground, usage, IPC | **Source-ready / execution-pending** | flow/state; performance; architecture; behavior tests | No passing receipt | Windows session and IPC failure behavior require runtime evidence |
| P8 | Enforcement, overlay, blocking | **Source-ready / execution-pending** | security; flow/state; performance; architecture; behavior tests | No passing receipt | Hard blocking and overlay interaction need executable Windows evidence |
| P9 | Health, scheduling, observability | **Source-ready / execution-pending** | failure behavior; performance; observability; deployment; behavior tests | No passing receipt | SCM, timers, recovery, and degraded-state reporting need host validation |
| P10 | Onboarding, consent, copy | **Composition blocked** | flow/state; architecture; localization; behavior tests | No passing receipt | App.UI compile blocker prevents confirmation of live composition |
| P11 | Backend dependencies | **External / pending** | contracts; auth; sync/data; notifications; deployment/config; secrets | No local backend receipt | Prior backend report is a lead only; backend repository/staging was not part of this workspace |
| P12 | Cross-cutting test evidence | **Blocked** | behavior-focused tests; all applicable dimensions | `test-no-build`, coverage, analyzer exits 1 | No pass/fail inference from stale TestResults or blocked commands |

## Confirmed current-source findings

Findings below are admitted only where the current workspace provides a direct citation or a fresh receipt. Severity is evidence-bound; no unsupported Blocker is asserted.

| ID | Track | Severity | Evidence and impact | Status / owner |
|---|---|---|---|---|
| F-001 | integration-infrastructure | Candidate blocker / pending | Untracked `src/ControlParental.App.UI/Program.cs:20-26` declares `ControlParental.App.UI.Program`; prior current-source verification records duplicate generated WinUI `Program` (`t26-live-flow-closure/verify-report.md:47,100`). App.UI compilation and dependent UI tests are affected. | Pending isolation; application build owner |
| F-002 | integration-infrastructure | Candidate blocker / pending | `tests/ControlParental.Service.Tests/AgentLauncherLaunchSeamTests.cs:34-44,157,187` consumes injectable seams; prior current-source verification records absent/inaccessible seams (`t26-live-flow-closure/verify-report.md:48,101`). Service.Tests compilation is affected. | Pending isolation; test/launcher owner |
| F-003 | client-defect | Major | `src/ControlParental.Service/Program.cs:93-105` calls `CertificatePinningValidator.Validate(...)` but returns `true` regardless of its result. A pin mismatch therefore has no demonstrated fail-closed effect in this client path. | Confirmed source finding; service security owner |
| F-004 | client-defect | Major | `src/ControlParental.Service/WnsHostedService.cs:50,71` logs `{Uri}` for a WNS channel. Channel URIs are bearer-like endpoint credentials and should not be emitted verbatim. | Confirmed source finding; service observability owner |
| F-005 | technical-debt | Minor | `src/ControlParental.Service/AccountManager.cs:139,183` uses `.Result` inside account-management flows. This is a synchronous-over-async seam with deadlock/latency risk, but no release impact was demonstrated in this audit. | Confirmed non-blocking debt; service owner |
| F-006 | integration-infrastructure | Pending | `src/ControlParental.Service/ConfigurationLoader.cs:24-30,68-86` searches `.env`, loads environment credentials, and prints candidate paths. Production deployment and secret ownership are not validated by a local receipt. | Pending deployment validation; platform owner |
| F-007 | backend-dependency | External / unverifiable | Prior report `docs/backend-blockers-windows-implementation.md:46-268` claims pairing/JWT, policy/RLS, WNS, grants, event idempotency, and integrity-verdict gaps in external `ParentalControls`. The referenced backend repository is outside this workspace and was not revalidated. | External lead; backend owner |

## Dimension coverage rule

Each partition was reviewed for risk/security/privileges/data; flow/state/business logic; complexity/performance/optimization; Clean/Hexagonal Architecture, SOLID, and DRY; behavior-focused tests; and backend readiness across contracts/auth, sync/data, notifications, observability, deployment/configuration, and secrets. “Pending” means evidence was not executable or external; it does not mean pass or fail.
