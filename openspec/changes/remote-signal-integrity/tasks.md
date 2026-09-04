# Tasks: SDD7 Unit 4 Durable Escalation

## Review Workload Forecast

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

Chain: A2 `47e7129` → B `1fc5b60` → C1A `feat/sdd7-4c2c1a-handler-durable-state` → C1B `feat/sdd7-4c2c1b-owner-reconciliation` → C1C `feat/sdd7-4c2c1c-deadline-owner` → C2 `feat/sdd7-4c2c2-runtime-composition` → integrated gate/Unit5. Auto-chain/feature-branch-chain; no exception. Each child requires predecessor independent PASS and explicit authorization before start or commit.

Commands: H=`dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --filter "FullyQualifiedName~IntegrityVerdictHandler"`; M=`dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --filter "FullyQualifiedName~AntiTamperMonitor"`; R=`dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --filter "FullyQualifiedName~IntegrityRuntimePath"`.

## Phase 1: Admission

- [x] 1.1 Admission rejection/coalescing/cancellation/single-flight tests.
- [x] 1.2 Bounded authenticated admission implementation.
- [x] 1.3 Bounds, identity, cancellation, coalescing, and polling verification.

## Phase 2: WNS Convergence

- [x] 2.1 WNS lifecycle, identity, idempotency, reconciliation, restart, and legacy tests.
- [x] 2.2 WNS coordinator completion and legacy registration removal.
- [x] 2.3 Redaction, bounds, retry ownership, DI, and restart verification.

## Phase 3: Realtime

- [x] 3.1 Foreground, refresh, background, stale, cancellation, and resubscribe tests.
- [x] 3.2 Lifecycle/generation gates and typed invalidation implementation.
- [x] 3.3 Service-authority and polling-preservation verification.

## Phase 4: Runtime Integrity — C1A/C1B/C1C/C2 (in progress; cumulative 13/17)

- [x] 4.1 C1A RED→GREEN→refactor — In existing `IntegrityVerdictHandlerTests.cs`, prove pure snapshot/restore and durable-state boundaries, then modify only `IntegrityVerdictHandler.cs`; gate with the focused handler test command and meaningful method/branch evidence. Implementation complete means ready for independent verification, not verified; 120–220 CODE+TEST, hard <=400.
- [x] 4.2a C1B1 RED→GREEN→refactor — In existing `AntiTamperMonitorTests.cs`, cover nominal durable-owner rehydration before remote work, valid/invalid/unavailable load behavior, revoked/trust state progression, deadline/key persistence, agent-death restart reconciliation, exact reaction/notification forwarding, and recovery authority; then modify only `AntiTamperMonitor.cs`. Gate with the exact C1B1 focus and portable coverage; 250–380 CODE+TEST, hard <=400. Base: C1A commit.
- [x] 4.2b C1B2 RED→GREEN→refactor — In existing `AntiTamperMonitorTests.cs`, cover cancellation/fault races, stale generation/effect suppression, and retry/ownership edges without expanding durable contracts; then modify only `AntiTamperMonitor.cs`. Gate with the focused C1B2 monitor command; 250–380 CODE+TEST, hard <=400. Base: C1B1 commit.
- [x] 4.3 C1C RED→GREEN→refactor — In existing `AntiTamperMonitorTests.cs`, test real deadline callback before/at/after, rollback/no extension, cancellation, stale callback, and one-shot recovery; implement only deadline ownership in `AntiTamperMonitor.cs`. Gate with focused monitor tests and method/branch evidence; 180–300 CODE+TEST, hard <=400.
- [ ] 4.4 C2 RED→GREEN — In existing `IntegrityRuntimePathTests.cs`, test real singleton store/monitor resolution, startup-before-remote, and cleanup; modify only `Program.cs` and composition tests, with no owner logic. Gate with the focused runtime-path command; <=200 CODE+TEST, hard <=400.
- [ ] 4.5 Integrated Unit 4 gate — After C2, independently verify all slices, >80% meaningful changed-production coverage with method/branch proof, preserved external-log SHA references, changed-path allowlist, `git diff --check`, and hard budgets. Coverage command: `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --collect:"XPlat Code Coverage"`. No commit without explicit authorization.

Rejected evidence: the initial ~301-line C candidate and second ~342-line candidate/false GREEN are rejected; exact initial RED names/raw are unavailable. Rebuild cleanly; reuse no patch or retrospective TDD claim. Preserve exclusions: retries, distributed locks, migration, new background framework, durable fields, and keyed recovery API.

## Phase 5: Compatibility

- [ ] 5.1 Remove only obsolete compatibility wiring after Unit 4 approval; add receipt assertions.
- [ ] 5.2 Run separate local/backend/WNS/Realtime/Windows verification; prohibit `live-integrated`, `client-ready`, and `ExternalVerified` claims.
