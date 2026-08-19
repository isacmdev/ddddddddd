# Exploration: t00-t26-backend-readiness-audit

> Phase: explore (read-only). This artifact is the AUDIT MAP — it defines scope
> partitions, an evidence strategy, a severity rubric, and classification tracks
> for a later evidence-based audit report + prioritized remediation plan. It does
> NOT contain the full audit findings (those are the report deliverable) and it
> does NOT modify code or tests.

## Current State

The repository is a .NET 9 / C# / WinUI 3 parental-control agent for Windows
with a Clean/Hexagonal layout: `ControlParental.Domain` (pure net9.0, 89 source
files), `ControlParental.Service` (windows-specific, 76 files, owns SQLite /
auth / scheduled work / outbox / integrity / WNS / backend access / enforcement),
`ControlParental.SessionAgent` (17 files, foreground watcher + overlay + pipe
client), `ControlParental.App.UI` (98 files, WinUI onboarding / status /
realtime / WNS handler / IPC channel), and `DbInspector` (utility). Tests live
per project: App.UI (38 .cs), Service (71), Domain (23), SessionAgent (16), plus
obfuscation/installer harnesses. `openspec/config.yaml` records xUnit + Moq/
NSubstitute + FluentAssertions + RichardSzalay.MockHttp + EF InMemory +
coverlet; no configured E2E harness.

**Critical baseline fact — the audited state is mostly uncommitted.** `main` has
only four commits; `git status` shows ~118 modified and ~110 untracked paths.
The "current source" therefore is a working tree, not a released baseline. Any
finding cited in the later report must be anchored to a path/line in this tree,
and reproducibility requires a snapshot/commit decision before the report is
treated as authoritative.

**T26 live composition is RESTORED in the current tree** (newest truth, per
Engram `sdd/t26-live-flow-closure/resume-state`, 2026-07-28 21:40). Verified
against current source: `App.xaml.cs` now composes the IPC-backed
`IUIChannel`/`NamedPipeUIChannel`, `IIpcOnboardingStateService`/
`IpcOnboardingStateService`, `IConsentService`/`IpcConsentService`, and
`ServiceEnforcementLevelMonitor`, and constructs `new OnboardingViewModel(
onboardingClient, monitor)` (the IPC-only constructor). `MainWindow.xaml.cs` is
the route host that selects via `OnboardingRouteCatalog` and mounts the six
in-app pages (Pairing/Consent/Transparency/Account/ServiceSetup/Demo/Managed)
with guarded completion callbacks. `ConsentDialog.cs` is deleted (`D` in git
status). This supersedes the earlier `t26-live-flow-closure/verify-report.md`
"FAIL" verdict, which predated the restoration.

**Two pre-existing untracked files block the solution build and most executable
evidence**, both confirmed present and out of any single roadmap unit:
- `src/ControlParental.App.UI/Program.cs` — declares
  `DISABLE_XAML_GENERATED_MAIN` is set in the csproj, but the csproj does NOT
  set it (grep returned no match), so both this `Program` and the WinAppSDK
  auto-generated `Program` compile → `CS0101` duplicate, which cascades to
  `MSB3073` (XamlCompiler exit 1). This is a T00 tooling/packaging
  build-consistency defect, not a T26 defect.
- `tests/ControlParental.Service.Tests/AgentLauncherLaunchSeamTests.cs` —
  references stale AgentLauncher seams (`INamedPipeServiceChannel`,
  `IProcessLaunchApi`, `STARTUPINFO`, `PROCESS_INFORMATION`) absent from current
  `AgentLauncher.cs` → blocks `Service.Tests` compilation.

Consequence: Domain/Service/SessionAgent projects build, but App.UI and
Service.Tests do NOT reach runtime, so most App.UI/Service behavior cannot be
proven with passing tests in the current snapshot. This is the single biggest
constraint on the audit's executable-evidence level.

**Format gate**: `dotnet format whitespace/style --verify-no-changes` exit 0
(clean); `dotnet format analyzers --verify-no-changes` exits 2 with ~6,391
warning lines across 263 files, including current T26/IPC/onboarding surfaces
(not purely unrelated debt). `DoD-G` analyzer gate is therefore not green.

**Prior reports are INPUTS, not truths.** Three existing artifacts already map
defects with file/line evidence and must be re-verified against current source
in the audit report (do not trust stale summaries):
- `openspec/changes/windows-backend-independent-readiness/exploration.md` —
  scheduler backoff/session/outbox/TLS/WNS/integrity/JSON defects.
- `openspec/changes/windows-backend-independent-readiness/apply-progress-f5-fixes.md`
  — states that 4 of those were partially fixed (per-entry outbox, expired
  session now surfaces `NeedsRefresh`, debugger probe seam, non-object IPC JSON
  fail-closed); the audit must confirm which remain.
- `docs/backend-blockers-windows-implementation.md` — six BACKEND-side
  blockers (pairing/JWT identity, policy completeness/RLS, WNS fan-out, atomic
  grant, event dedup, integrity verdict) plus a `child_first_name` contract
  contradiction. These reference a SEPARATE repo (`ParentalControls`) not present
  here, so they are external evidence that this audit can cite but cannot
  re-verify.

## Affected Areas (audit surface, grouped by partition)

- Build/tooling/packaging: `src/ControlParental.App.UI/Program.cs`,
  `ControlParental.App.UI.csproj`, `Directory.Build.props`,
  `Directory.Packages.props`, MSIX/MSI authoring, `stylecop.json`,
  `.editorconfig`, `global.json`.
- Domain purity/engine: `src/ControlParental.Domain/*` (RulesEngine, Policy,
  Schedule/Window, Grant, enums, ITimeProvider, IPC messages, OnboardingState).
- Persistence/offline: `src/ControlParental.Service/ControlParentalDbContext.cs`,
  `OutboxManager.cs`, `PolicyRepository.cs`, `UsageAccumulator.cs`.
- Sync/transport/contracts: `src/ControlParental.Service/BackendClient.cs`,
  `src/ControlParental.Domain/IBackendClient.cs`, `CertificatePinningValidator.cs`,
  `ConfigurationLoader.cs`, `Program.cs` (service), `HttpResponseClassifier.cs`,
  `ExponentialBackoffPolicy.cs`.
- WNS/push/polling: `src/ControlParental.App.UI/WnsPushNotificationHandler.cs`,
  `WnsChannelPlanner.cs`, `WnsJsonContext.cs`; `src/ControlParental.Service/
  WnsNotificationService.cs`, `WnsHostedService.cs`.
- Integrity/secrets/anti-tamper: `src/ControlParental.Service/IntegrityChecker.cs`,
  `Interop/WinTrust.cs`, `AntiTamperMonitor.cs`, `IntegrityVerdictHandler.cs`,
  secret-store implementation (DPAPI/TPM seam).
- Foreground/usage/IPC: `src/ControlParental.SessionAgent/ForegroundWatcher.cs`,
  `OverlayWindow.cs`, `Interop/NamedPipeClient.cs`, `AppIdentityResolver.cs`;
  `src/ControlParental.Service/Interop/NamedPipeServer.cs`,
  `NamedPipeUIServer.cs`, `SessionWatcher.cs`, `UsageReconciler.cs`.
- Enforcement/overlay/hard block: `src/ControlParental.Service/EnforcementEngine.cs`,
  `ProcessTerminator.cs`, `WorkstationLockManager.cs`;
  `src/ControlParental.SessionAgent/OverlayWindow.cs`.
- Health/scheduled work/observability: `src/ControlParental.Service/
  ScheduledWorkService.cs`, `EnforcementLevelMonitor.cs`,
  `EnforcementLevelQueryHandler.cs`, `RedactingLogger.cs`,
  `ServiceRecoveryManager.cs`, `ScmController.cs`.
- Onboarding/consent/transparency/copy: `src/ControlParental.App.UI/App.xaml.cs`,
  `MainWindow.xaml(.cs)`, `OnboardingViewModel.cs`, `OnboardingRouteCatalog.cs`,
  `*StepPage.xaml(.cs)`, `Strings/`, `StringsAdapter.cs`;
  `src/ControlParental.Service/OnboardingStateService.cs`, `UIMessageHandler.cs`,
  `OnboardingStateJsonContext.cs`; `src/ControlParental.Domain/UIMessages.cs`,
  `UIMessagesJsonContext.cs`, `IOnboardingStateService.cs`.
- Backend readiness (contract dependencies, not client code to write): the six
  `docs/backend-blockers-windows-implementation.md` items, `child_first_name`,
  age-band wire values, atomic-grant uniqueness, RLS device isolation.
- Test evidence: every `tests/ControlParental.*.Tests/` project, the two untracked
  build blockers, and the absence of an E2E harness.

## Scope Partitions (bounded, independently reviewable)

The audit is split into 12 partitions. Each carries a verdict, severity, and
"evidence required for a blocker" note in the report. This keeps the report
reviewable and lets the remediation plan map onto roadmap units.

| # | Partition | Roadmap units | Primary audit dimension |
|---|-----------|---------------|-------------------------|
| P1 | Build, tooling, packaging, reproducibility | T00, T37 install, DoD-G | Operational risk, readiness |
| P2 | Domain purity, rules engine, policy/time model | T01, T02, T04 | Correctness, architecture |
| P3 | Local persistence, outbox, offline-first | T03, T18(local) | Data loss/integrity, correctness |
| P4 | Sync, transport, contracts, session/auth | T14, T17, T18, T22 | Security, backend readiness |
| P5 | WNS push, channel lifecycle, polling | T19 | Backend readiness, failure handling |
| P6 | Integrity, anti-tamper, secrets | T13, T16, T23 | Security, privileges |
| P7 | Foreground watch, usage counting, IPC | T05, T06, T07, T38 | Flow, state, privileges |
| P8 | Enforcement, overlay, hard/total block | T08, T09, T11 | Security, operational risk |
| P9 | Health monitor, scheduled work, observability | T12, T13, T20 | Correctness, data exposure |
| P10 | Onboarding live flow, consent, transparency, copy | T24, T25, T26 | Flow, state, security |
| P11 | Backend readiness & contract dependencies | T14/T17/T18/T19/T23/T24/T28 | Integration/infra (case-by-case) |
| P12 | Test evidence & TDD readiness | cross-cutting | Behavior coverage |

## Evidence Strategy

1. **Evidence level for THIS exploration = structural** (read-only source
   inspection + git state + re-reading prior reports as inputs). No code edits,
   no review lifecycle calls.
2. **Current-source re-verification rule**: every defect carried forward from a
   prior report MUST be re-checked against the current tree before it earns a
   severity in the report. Known drift to confirm: per-entry outbox (partially
   fixed), `DeviceAuthenticator` expired-session `NeedsRefresh` (fixed),
   non-object IPC JSON fail-closed (fixed), T26 live composition (restored).
3. **Cite or retract**: each finding cites `path:line` from current source, or is
   explicitly marked "unverifiable in this repo" (e.g., backend-side blockers in
   the external `ParentalControls` repo).
4. **Executable-evidence inventory**: for each partition, the report states
   whether a covering unit/component test EXISTS and whether it can RUN given the
   two build blockers. Until `Program.cs`/`AgentLauncherLaunchSeamTests.cs` are
   isolated or excluded, App.UI and Service.Tests executable evidence is
   unavailable — the report must mark those partitions "executable evidence
   pending" rather than infer pass/fail.
5. **Verify-phase command set** (identified now, run later): `dotnet build
   ControlParental.sln --no-restore --verbosity minimal`;
   `dotnet test --no-build --verbosity normal` per project (Domain, Service,
   SessionAgent, App.UI) plus focused filters for onboarding/IPC/WNS/integrity;
   `dotnet format whitespace|style|analyzers --verify-no-changes` decomposed;
   `dotnet test --collect:"XPlat Code Coverage"`. Results are hashed and dated;
   stale TRX/binaries are not credited.

## Severity Rubric & Classification Tracks

**Severity (strict):**
- **Blocker** — must be fixed before any production claim; REQUIRES concrete
  evidence (failing test, data-loss/privilege proof, or a stated "evidence
  required" path). No speculation. If evidence cannot be produced now, the
  finding is "candidate blocker — evidence pending", not a confirmed blocker.
- **Critical** — serious risk with current-source evidence but not necessarily
  release-stopping.
- **Major** — correctness/flow/state defect with bounded impact.
- **Minor / non-blocking debt** — duplication, complexity, or inefficiency
  WITHOUT demonstrated impact (per audit criteria, these do not block).

**Classification tracks (per the audit criteria, kept separate):**
- (a) **Client-owned defect** — fix belongs to this repo.
- (b) **Unready integration/infrastructure** — client seam exists but is not
  production-ready; fix is client-side hardening behind a contract.
- (c) **Non-blocking debt** — maintainability only.
- (d) **Legitimate backend dependency** — cannot be resolved in this repo;
  documented as a dependency with required staging evidence.

Priority order: risk → correctness → backend readiness → maintainability.

**Explicit non-blocking rule**: complexity/duplication/inefficiency without a
demonstrated correctness, performance, or failure-handling impact is (c), not a
blocker.

## Approaches

1. **Partitioned report with per-partition verdicts + prioritized remediation
   backlog** — One report structured by the 12 partitions above; each section
   yields a verdict, severities with evidence-required notes, and a track
   (a/b/c/d); a final backlog ranks remediation by risk.
   - Pros: matches the "audit report + prioritized remediation plan" deliverable;
     each partition is reviewable within the 400-line cognitive budget; maps
     directly onto roadmap units for follow-up tasks.
   - Cons: cross-cutting findings (e.g., observability, secrets, build
     reproducibility) appear in multiple partitions and need a summary.
   - Effort: Medium
2. **Single monolithic report** — One continuous narrative across T00–T26.
   - Pros: complete in one pass.
   - Cons: too large for the 400-line review budget; high review fatigue; hard to
     slice into remediation tasks.
   - Effort: High
3. **Phased/staged audit** — Deliver P1/P2 (foundations) first, then the rest.
   - Pros: smallest first slice.
   - Cons: does not meet the "audit across T00–T26" goal in one deliverable.

## Recommendation

Use **Approach 1**. The report is bounded by the 12 partitions; each partition
is independently reviewable and each finding carries (i) a current-source
citation or an "unverifiable" mark, (ii) a severity with evidence-required note,
and (iii) a classification track (a/b/c/d). The remediation plan is a risk-ranked
backlog that respects the priority order (risk → correctness → backend readiness
→ maintainability) and uses the 4-track separation so client defects are not
confused with backend dependencies.

**Out of scope for the audit (must be stated in the proposal):**
- Implementing fixes (report + plan only; no code/test edits).
- Requiring a complete backend; backend blockers (P11) are documented as
  dependencies, not as work this client must do.
- Freezing speculative backend routes/DTOs/age-band wire values/pairing-response
  shapes — the audit flags over-speculative `BackendClient` assumptions as (b)
  unready integration, not as a reason to freeze contracts.
- Calling review lifecycle commands.

**Review-workload guard for the remediation plan**: if the plan's authored
remediation exceeds the 400-line budget, the proposal should recommend chained
PRs by partition cluster (e.g., P1 build-readiness → P3/P9 data+scheduler →
P4/P5 transport+WNS → P10 onboarding), each with a clear start, finish,
autonomous scope, verification, and rollback.

## Risks

- **Uncommitted baseline**: ~228 modified/untracked paths on a 4-commit `main`
  make the audit's "current source" non-reproducible unless snapshotted; a
  baseline commit/tag decision is needed before the report is cited as evidence.
- **Blocked executable evidence**: the two pre-existing untracked build blockers
  prevent App.UI and Service.Tests from compiling; large portions of the audit
  (P7/P8/P10/P12 partly) cannot cite passing runtime evidence until they are
  isolated or excluded.
- **Stale-vs-current conflict**: prior artifacts disagree (verify-report "FAIL"
  vs resume-state "restored"); the report must baseline to current source and
  explicitly state which prior claims are superseded.
- **Analyzer noise vs signal**: ~6,391 analyzer warnings across 263 files risk
  masking real defects or being dismissed wholesale; the report must separate
  signal (e.g., `CA2007`, `CA2000`, `SA1600` on T26 surfaces) from bulk debt.
- **Backend evidence not re-verifiable here**: the six backend blockers and
  `child_first_name` live in an external repo; over- or under-reporting them as
  client defects is a real risk, mitigated by track (d).
- **Speculative-contract blur**: `BackendClient` carries provisional
  endpoints/DTOs; the line between "client defect" and "unready integration" is
  genuinely fuzzy and must be judged per-call, not blanket-classified.
- **Remediation size**: the backlog may exceed the 400-line review budget;
  without chained-PR planning the remediation could be rejected as oversized.

## Ready for Proposal

**Yes, conditionally.** The audit map (partitions, evidence strategy, severity
rubric, classification tracks, out-of-scope list) is sufficiently defined for a
proposal. Before proposal approval, the product owner should answer three
decisions:

1. **Baseline**: snapshot/commit the working tree (or tag) as the audit baseline,
   or continue auditing the live working tree as-is?
2. **Build blockers**: isolate/exclude `Program.cs` and
   `AgentLauncherLaunchSeamTests.cs` BEFORE the audit report (so executable
   evidence is available), or treat their absence of evidence as a finding
   within the report?
3. **Remediation delivery**: single prioritized backlog, or chained PRs by
   partition cluster when the plan exceeds the 400-line budget?

The proposal must carry: the 12 partitions, the severity rubric, the 4-track
classification, the evidence strategy (including re-verification of prior
defects), and the explicit out-of-scope/deferred-to-backend list so no
speculative backend route, DTO, verdict, pairing response, or age-band value
becomes an audit requirement.

## Section D Envelope

- `status`: success
- `executive_summary`: Mapped an evidence-based, read-only audit of the current
  T00–T26 repository into 12 bounded partitions with a severity rubric, a 4-track
  client/integration/debt/backend-dependency classification, and an evidence
  strategy that re-verifies prior reports against current source. Confirmed the
  T26 live composition is restored and that two pre-existing untracked build
  blockers (Program.cs, AgentLauncherLaunchSeamTests.cs) currently prevent
  App.UI/Service.Tests executable evidence.
- `artifacts`: OpenSpec
  `openspec/changes/t00-t26-backend-readiness-audit/exploration.md`; Engram
  `sdd/t00-t26-backend-readiness-audit/explore`
- `next_recommended`: sdd-propose (carry partitions, rubric, tracks,
  out-of-scope, and the three owner decisions)
- `risks`: uncommitted baseline; two build blockers block executable evidence;
  prior artifact conflict; analyzer signal/noise; backend evidence not
  re-verifiable here; speculative-contract blur; remediation may exceed the
  400-line budget
- `skill_resolution`: paths-injected — loaded `sdd-explore` SKILL.md plus
  `_shared/sdd-phase-common.md` and `_shared/openspec-convention.md` from the
  injected `## Skills to load before work` block
