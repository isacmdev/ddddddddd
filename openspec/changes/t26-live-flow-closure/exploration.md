# Exploration: t26-live-flow-closure divergence audit

### Current State
Current production composition is split-brain. `src/ControlParental.App.UI/OnboardingViewModel.cs` has the newer IPC-only constructor and service-confirmed/fail-closed logic, and many T26 pages/adapters/tests exist as untracked files. But the live app shell still uses the legacy composition: `App.xaml.cs` resolves `IOnboardingStateStore`, constructs `new ConsentDialog(null, null)`, and calls `new OnboardingViewModel(stateStore, consentDialog, null)`; `MainWindow.xaml` is still the generic title/description/Execute/Next shell; `MainWindow.xaml.cs` only sets `DataContext` and never hosts routed pages or `OnboardingRouteCatalog`. `ConsentDialog.cs` still exists and is only style-formatted, not deleted.

This directly contradicts the T26 tasks/apply-progress claims that Unit 1 routed the live shell, Unit 2 completed service-owned state/setup/funnel IPC, and Unit 3 removed `ConsentDialog`. The intended implementation is partially present in the working tree, mostly as untracked files plus a modified `OnboardingViewModel`, but it is not coherently composed and is not present in `MainWindow` at all.

### Affected Areas
- `src/ControlParental.App.UI/App.xaml.cs` — modified only by formatting; still registers local/store-style services and constructs `ConsentDialog` + old `OnboardingViewModel` shape. This is the primary production-composition divergence.
- `src/ControlParental.App.UI/MainWindow.xaml` — tracked and unchanged; still has `ExecuteButton`/`NextButton` generic shell, no `PageHost`, no routed page host.
- `src/ControlParental.App.UI/MainWindow.xaml.cs` — tracked and unchanged; only initializes the VM, no `OnboardingRouteCatalog`, no guarded callbacks, no page construction.
- `src/ControlParental.App.UI/OnboardingViewModel.cs` — modified to IPC-only constructor and service-confirmed state/progress/funnel calls; now incompatible with the current `App.xaml.cs` constructor call.
- `src/ControlParental.App.UI/ConsentDialog.cs` — still present and referenced by `App.xaml.cs`; only formatting changed.
- `src/ControlParental.App.UI/{ConsentPage,TransparencyPage,PairingPage,AccountStepPage,ServiceSetupPage,DemoStepPage,ManagedStepPage}*.xaml(.cs)` — untracked intended page surfaces; present but not hosted by production `MainWindow`.
- `src/ControlParental.App.UI/OnboardingRouteCatalog.cs` — untracked route catalog with canonical `pairing, consent, account, service, demo, managed` order; not used by production shell.
- `src/ControlParental.App.UI/Interop/IIpcOnboardingStateService.cs`, `IpcOnboardingStateService.cs` — untracked IPC adapters with fail-closed state/setup/funnel methods; not registered/composed by `App.xaml.cs`.
- `src/ControlParental.Service/OnboardingStateService.cs`, `UIMessageHandler.cs` — untracked/modified service-side six-step state, setup verification, funnel dedupe, and IPC handlers appear present.
- `tests/ControlParental.App.UI.Tests/*Onboarding*`, `ConsentFlowTests.cs`, `DeadCodeRemovalTests.cs`, `OnboardingLauncherThreatTests.cs` — untracked/modified tests assert the intended route shell and legacy removal, but current source would fail those assertions.
- `tests/ControlParental.Service.Tests/*Onboarding*`, `UIMessageHandler*Tests.cs` — untracked service tests for service-owned state and IPC behavior.
- `src/ControlParental.App.UI/Program.cs` — untracked pre-existing WinUI entry-point blocker, separate from T26 live-flow composition.
- `tests/ControlParental.Service.Tests/AgentLauncherLaunchSeamTests.cs` — untracked pre-existing Service.Tests blocker, separate from T26 live-flow composition.

### Approaches
1. **Small coherent restoration of Units 1–3 composition** — Recompose the live App.UI shell around the already-present IPC/pages/service surfaces.
   - Pros: Minimal scope that matches the existing T26 spec/tasks; preserves the untracked page/service/test work already present; directly fixes the artifact/source divergence.
   - Cons: Requires touching the main production composition files and deleting/retiring the legacy `ConsentDialog`; App.UI tests remain blocked until the pre-existing `Program.cs` issue is isolated/resolved.
   - Effort: Medium

2. **Artifact rollback / re-plan from current legacy shell** — Treat tasks/apply-progress as wrong and reset T26 status to partial.
   - Pros: No source changes; accurately reflects current runtime composition.
   - Cons: Does not close T26; wastes the already-present untracked implementation and leaves production with an incompatible `App.xaml.cs`/`OnboardingViewModel` split.
   - Effort: Low

### Recommendation
Use approach 1. The smallest coherent restoration scope is exactly the live composition seam, not a broad rewrite: restore `App.xaml.cs` DI and VM construction to use `IUIChannel`/`NamedPipeUIChannel`, `IIpcOnboardingStateService`/`IpcOnboardingStateService`, `IConsentService`/`IpcConsentService`, and `ServiceEnforcementLevelMonitor`; replace `MainWindow.xaml(.cs)` with the route-host shell that uses `OnboardingRouteCatalog` and guarded page callbacks; delete or fully detach `ConsentDialog.cs`; keep the existing untracked pages/adapters/service-state implementation; then run the T26 App.UI and Service tests once the two known pre-existing build blockers are handled or excluded.

Exact restoration files: `src/ControlParental.App.UI/App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `ConsentDialog.cs`, `OnboardingViewModel.cs` only if constructor/callback mismatches remain, `OnboardingRouteCatalog.cs`, page `.xaml(.cs)` files, `Interop/IIpcOnboardingStateService.cs`, `Interop/IpcOnboardingStateService.cs`, `Interop/IUIChannel.cs`, `Interop/NamedPipeUIChannel.cs`, `Interop/IpcConsentService.cs`, `ServiceEnforcementLevelMonitor.cs`, `src/ControlParental.Service/OnboardingStateService.cs`, `src/ControlParental.Service/UIMessageHandler.cs`, and related message contracts/json contexts.

Tests needed after restoration: `ConsentFlowTests`, `DeadCodeRemovalTests`, `OnboardingViewModelStateRouteTests`, `OnboardingViewModelIpcOwnershipTests`, `OnboardingViewModelProgressTests`, `OnboardingViewModelFunnelTests`, `OnboardingE2EStateMachineTests`, `IpcOnboardingStateServiceTests`, `OnboardingLauncherThreatTests`, `ServiceEnforcementLevelMonitorTests`, `OnboardingStateServiceTests`, `OnboardingStateAtomicTests`, `OnboardingStateResumabilityTests`, and `UIMessageHandlerStateControl/Consent/EnforcementTests`, followed by `dotnet build ControlParental.sln --no-restore --verbosity minimal` and the verify-report's focused App.UI/Service filters.

### Risks
- The current working tree has a large amount of unrelated modified/untracked debt, so restoration must be narrowly staged/owned to avoid mixing T26 with broader workspace churn.
- Current `App.xaml.cs` and `OnboardingViewModel.cs` are incompatible: the app calls a removed constructor shape. Even if `Program.cs` were removed, this T26 composition mismatch likely remains a compile/runtime blocker until restored.
- The latest apply-progress claims analyzer failures are wholly pre-existing, while verify-report says analyzer output includes T26 surfaces; verification should fail closed until current-source tests/build/format are rerun.
- `src/ControlParental.App.UI/Program.cs` and `tests/ControlParental.Service.Tests/AgentLauncherLaunchSeamTests.cs` are pre-existing, out-of-T26 blockers, but they still block proving T26 with normal solution/test commands.

### Ready for Proposal
No. This is an existing active change with a source/artifact divergence. The next phase should be a narrowly scoped remediation/apply slice to restore the already-specified Unit 1–3 composition, after explicitly isolating the two pre-existing build blockers from the T26 evidence path.

## Section D Envelope
- `status`: success
- `executive_summary`: Current source confirms the verify-report: production still uses the legacy consent/local-store shell while the intended T26 implementation exists only partially as untracked/modified files and is not composed. The divergence is a split-brain restore/apply failure: VM/pages/IPC/service surfaces are present, but `App.xaml.cs`, `MainWindow.xaml(.cs)`, and `ConsentDialog.cs` remain legacy.
- `artifacts`: OpenSpec `openspec/changes/t26-live-flow-closure/exploration.md`; Engram `sdd/t26-live-flow-closure/explore`
- `next_recommended`: sdd-apply remediation slice for live composition restoration, then sdd-verify
- `risks`: pre-existing untracked build blockers still prevent normal proof; current App/VM constructor mismatch is T26-specific; broad dirty working tree makes ownership fragile.
- `skill_resolution`: paths-injected — loaded `sdd-explore` and `_shared` paths from the prompt.
