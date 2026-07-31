# Tasks: t26-onboarding-ipc-ageband-consent

## Review Workload Forecast

| Field | Value |
|---|---|
| Estimated changed lines | ~2,508 across 9 planned slices |
| Delivery strategy | auto-chain |
| Chain strategy | stacked-to-main |
| Suggested split | PR #1 → #2 → #3 → #4a/#4b if measured >400 → #5 → #6 → #7 → #8a/#8b if measured >400 → #9 |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

## PR #1: Remove duplicate UI pipe registration (Fase 0: build blocker)

> Estimated diff: ~3 changed lines (under 400). Depends on: nothing.
> Files: `src/ControlParental.Service/Program.cs`.

- [ ] 1.1 [P1.delete, src/ControlParental.Service/Program.cs:354-356] Delete the duplicate `NamedPipeUIServer` and hosted-adapter registrations.
- [ ] 1.2 [P1.verify] Run `dotnet build`, `dotnet test`, and a Service-start smoke test; confirm no `InvalidOperationException`.
- [ ] 1.M [P1.merge] Land PR #1 to `main` with `fix(service): remove duplicate UI pipe registration`.

## PR #2: Persist consent through IPC (Fase 1: real consent)

> Estimated diff: ~285 changed lines (under 400). Depends on: PR #1 merged.
> Files: `src/ControlParental.Domain/IpcMessage.cs`, `src/ControlParental.Service/{Program.cs,UIMessageHandler.cs}`, `src/ControlParental.App.UI/{Interop/IpcConsentService.cs,App.xaml.cs,ConsentService.cs,MainWindow.xaml.cs,OnboardingViewModel.cs}`, `tests/ControlParental.App.UI.Tests/ConsentFlowTests.cs`.

- [x] 2.1 [P2.edit, src/ControlParental.Domain/IpcMessage.cs] Add `GetConsentStatus`, `ConsentStatusSnapshot`, and `GrantConsent` contracts.
- [x] 2.2 [P2.edit, src/ControlParental.Service/Program.cs] Add deserialization arms for the consent contracts. (Resolved via NamedPipeUIServer.cs DeserializeMessage switch — Program.cs only DI-registers it.)
- [x] 2.3 [P2.edit, src/ControlParental.Service/UIMessageHandler.cs] Route status/grant messages to persistent `ConsentService` and return typed snapshots.
- [x] 2.4 [P2.new, src/ControlParental.App.UI/Interop/IpcConsentService.cs] Implement `IConsentService` over `IUIChannel`, caching only acknowledged status.
- [x] 2.5 [P2.edit, src/ControlParental.App.UI/App.xaml.cs] Bind `IConsentService` to `IpcConsentService`.
- [x] 2.6 [P2.delete, src/ControlParental.App.UI/ConsentService.cs] Remove the in-memory consent implementation.
- [x] 2.7 [P2.edit, src/ControlParental.App.UI/ConsentPage.xaml.cs] Replace the empty catch with explicit fail-closed error handling.
- [x] 2.8 [P2.edit, src/ControlParental.App.UI/MainWindow.xaml.cs] Navigate to `ConsentPage` with channel and completion callback. (ConsentPage still receives IConsentService via DI; navigation parameter wiring deferred to PR #5.)
- [x] 2.9 [P2.edit, src/ControlParental.App.UI/OnboardingViewModel.cs] Require persisted consent before recording completion or advancing.
- [x] 2.10 [P2.test, tests/ControlParental.App.UI.Tests/ConsentFlowTests.cs] Cover success, unavailable Service, blocked advance, no silent swallow, and restart persistence.
- [x] 2.11 [P2.verify] Run build/tests; verify consent survives restart and failure never advances.
- [ ] 2.M [P2.merge] Land PR #2 to `main` with `feat(onboarding): persist consent through IPC`. (Local-only: not committed per user instruction.)

## PR #3: Report honest protection progress (Fase 2: honest progress)

> Estimated diff: ~250 changed lines (under 400). Depends on: PR #2 merged.
> Files: `src/ControlParental.App.UI/{OnboardingViewModel.cs,MainWindow.xaml,Interop/ServiceEnforcementLevelMonitor.cs,App.xaml.cs,EnforcementLevelMonitor.cs}`, `tests/ControlParental.App.UI.Tests/ProgressBarHonestyTests.cs`.

- [x] 3.1 [P3.edit, src/ControlParental.App.UI/OnboardingViewModel.cs] Set total to five, remove inflation, cap counts, and show `Estado desconocido` before a real snapshot.
- [ ] 3.2 [P3.edit, src/ControlParental.App.UI/MainWindow.xaml] Set `ProgressBar.Maximum="5"`.
- [x] 3.3 [P3.new, src/ControlParental.App.UI/Interop/ServiceEnforcementLevelMonitor.cs] Proxy `GetEnforcementLevel` IPC without duplicating T12 logic.
- [x] 3.4 [P3.edit, src/ControlParental.App.UI/App.xaml.cs] Inject the Service-backed enforcement monitor.
- [x] 3.5 [P3.delete, src/ControlParental.App.UI/EnforcementLevelMonitor.cs] Delete the placeholder only after confirming no remaining consumers.
- [ ] 3.6 [P3.test, tests/ControlParental.App.UI.Tests/ProgressBarHonestyTests.cs] Cover all-pass, one-fail, unreachable, maximum five, and cap-at-five scenarios.
- [ ] 3.7 [P3.verify] Run build/tests and verify labels derive only from Service state.
- [ ] 3.M [P3.merge] Land PR #3 to `main` with `fix(onboarding): report honest protection progress`.

## PR #4: Move onboarding state ownership to Service (Fase 5: architectural flip)

> Estimated diff: ~520 changed lines — **OVER BUDGET**. Depends on: PR #3 merged. Split into #4a Service persistence/contracts and #4b App.UI migration before review if measured diff remains above 400.
> Files: `src/ControlParental.Domain/{IOnboardingStateService.cs,IpcMessage.cs,OnboardingStateStore.cs,IOnboardingStateStore.cs}`, `src/ControlParental.Service/{OnboardingStateService.cs,UIMessageHandler.cs,Interop/NamedPipeUIServer.cs}`, `src/ControlParental.App.UI/{Interop/UIMessages.cs,OnboardingViewModel.cs,App.xaml.cs}`, `tests/ControlParental.Service.Tests/OnboardingStateServiceTests.cs`, `tests/ControlParental.App.UI.Tests/OnboardingViewModelStateRouteTests.cs`.

- [ ] 4.1 [P4.edit, src/ControlParental.Domain/IOnboardingStateService.cs] Define reset, advance, completion, and funnel mutations returning authoritative state.
- [ ] 4.2 [P4.edit, src/ControlParental.Domain/IpcMessage.cs] Add state mutation contracts and AOT-safe serialization metadata.
- [ ] 4.3 [P4.edit, src/ControlParental.Service/OnboardingStateService.cs] Persist state/index atomically with `.tmp` plus replace under `fileLock`.
- [ ] 4.4 [P4.edit, src/ControlParental.Service/UIMessageHandler.cs] Handle get/complete/advance/reset/funnel messages and return persisted state.
- [ ] 4.5 [P4.edit, src/ControlParental.Service/Interop/NamedPipeUIServer.cs] Deserialize and route all new state contracts.
- [ ] 4.6 [P4.edit, src/ControlParental.App.UI/Interop/UIMessages.cs] Remove duplicate Domain state types and consume canonical contracts.
- [ ] 4.7 [P4.edit, src/ControlParental.App.UI/OnboardingViewModel.cs] Initialize and mutate through IPC; refresh only the read-only display cache from responses.
- [ ] 4.8 [P4.edit, src/ControlParental.App.UI/App.xaml.cs] Replace local-store DI with the IPC state service.
- [ ] 4.9 [P4.delete, src/ControlParental.Domain/OnboardingStateStore.cs] Delete the `%LOCALAPPDATA%` state writer.
- [ ] 4.10 [P4.delete, src/ControlParental.Domain/IOnboardingStateStore.cs] Delete the legacy store contract.
- [ ] 4.11 [P4.test, tests/ControlParental.Service.Tests/OnboardingStateServiceTests.cs] Cover atomic kill, outbox persistence, and concurrent reads/writes.
- [ ] 4.12 [P4.test, tests/ControlParental.App.UI.Tests/OnboardingViewModelStateRouteTests.cs] Cover IPC-only load/advance, no local writes, and restart resumability.
- [ ] 4.13 [P4.verify] Measure changed lines; split #4a/#4b if above 400, then run build/tests per slice.
- [ ] 4.M [P4.merge] Land the focused slice(s) to `main` with `refactor(onboarding): move state ownership to service`.

## PR #5: Wire real device pairing (Fase 3: real pairing)

> Estimated diff: ~175 changed lines (under 400). Depends on: PR #4 merged.
> Files: `src/ControlParental.App.UI/{PairingViewModel.cs,PairingPage.xaml,PairingPage.xaml.cs,MainWindow.xaml.cs}`, `src/ControlParental.Service/UIMessageHandler.cs`, `tests/ControlParental.App.UI.Tests/PairingFlowTests.cs`.

- [ ] 5.1 [P5.edit, src/ControlParental.App.UI/PairingViewModel.cs] Remove simulation; require IPC/age band; map 404 to “Ese código no es válido. Pedile uno nuevo a tu tutor”, 410 to “Ese código ya expiró. Pedile uno nuevo”, and 429 to “Probá de nuevo en un ratito”.
- [ ] 5.2 [P5.edit, src/ControlParental.App.UI/PairingPage.xaml] Start unselected, disable pairing until valid, and bind wire values through `Tag` without `años`.
- [ ] 5.3 [P5.edit, src/ControlParental.App.UI/PairingPage.xaml.cs] Receive the channel and complete through onboarding navigation.
- [ ] 5.4 [P5.edit, src/ControlParental.App.UI/MainWindow.xaml.cs] Navigate to pairing with channel and callback.
- [ ] 5.5 [P5.edit, src/ControlParental.Service/UIMessageHandler.cs] Preserve typed `PairDeviceResponse` statuses without client retries.
- [ ] 5.6 [P5.test, tests/ControlParental.App.UI.Tests/PairingFlowTests.cs] Cover 404/410/429/network/server, missing channel/band, and `"7-12"` wire format.
- [ ] 5.7 [P5.verify] Run build/tests; confirm no `Task.Delay` pairing path remains.
- [ ] 5.M [P5.merge] Land PR #5 to `main` with `feat(onboarding): wire real device pairing`.

## PR #6: Add edition-honest managed opt-in (Fase 4: managed step)

> Estimated diff: ~200 changed lines (under 400). Depends on: PR #4 merged.
> Files: `src/ControlParental.App.UI/{ManagedStepPage.xaml,ManagedStepPage.xaml.cs,ManagedStepViewModel.cs,MainWindow.xaml.cs,OnboardingViewModel.cs}`, `tests/ControlParental.App.UI.Tests/ManagedStepTests.cs`.

- [x] 6.1 [P6.new, src/ControlParental.App.UI/ManagedStepViewModel.cs] Read enforcement state; expose MANAGED upgrade on Pro+/Enterprise/Education and nonblocking “No disponible en esta edición” completion on Home.
- [x] 6.2 [P6.new, src/ControlParental.App.UI/ManagedStepPage.xaml] Render current level, upgrade card, unavailable/repair copy, and finish action.
- [x] 6.3 [P6.new, src/ControlParental.App.UI/ManagedStepPage.xaml.cs] Inject callback, monitor, and channel.
- [x] 6.4 [P6.edit, src/ControlParental.App.UI/MainWindow.xaml.cs] Route `managed` navigation with required dependencies.
- [ ] 6.5 [P6.edit, src/ControlParental.App.UI/OnboardingViewModel.cs] Complete without blocking STANDARD; report `Protección 5 de 5`, or `4 de 4` when Home accepts finish. *(No edit needed: `ExecuteStepAsync` already routes `managed`, and progress bar honours Standard/Managed levels from the monitor — out-of-the-box from PR #3.)*
- [x] 6.6 [P6.test, tests/ControlParental.App.UI.Tests/ManagedStepTests.cs] Cover edition honesty, STANDARD completion, IPC settings launch, and no UI `Process.Start`.
- [x] 6.7 [P6.verify] Run build/tests and exercise managed navigation.
- [ ] 6.M [P6.merge] Land PR #6 to `main` with `feat(onboarding): add managed opt-in step`. (Local-only: not committed per user instruction.)

## PR #7: Wire account and demo IPC (Fases 6 + 7)

> Estimated diff: ~325 changed lines (under 400). Depends on: PR #5 merged.
> Files: `src/ControlParental.Domain/IpcMessage.cs`, `src/ControlParental.Service/{UIMessageHandler.cs,Program.cs}`, `src/ControlParental.App.UI/{AccountStepViewModel.cs,AccountStepPage.xaml.cs,DemoStepViewModel.cs,DemoStepPage.xaml.cs,MainWindow.xaml.cs,OnboardingViewModel.cs}`, `tests/ControlParental.App.UI.Tests/{AccountStepTests.cs,DemoStepTests.cs}`.

- [ ] 7.1 [P7.edit, src/ControlParental.Domain/IpcMessage.cs] Add `ListAccounts`, `AccountList`, typed elevation, and `MsSettingsOpen` contracts.
- [ ] 7.2 [P7.edit, src/ControlParental.Service/Program.cs] Register deserialization arms for account/settings messages.
- [ ] 7.3 [P7.edit, src/ControlParental.Service/UIMessageHandler.cs] List real accounts, preserve `RequiresElevation`, and delegate settings/overlay commands.
- [ ] 7.4 [P7.edit, src/ControlParental.App.UI/AccountStepViewModel.cs] Remove delay/placeholder; list accounts via IPC and surface tutor-authorization copy.
- [ ] 7.5 [P7.edit, src/ControlParental.App.UI/AccountStepPage.xaml.cs] Receive completion callback and channel.
- [ ] 7.6 [P7.edit, src/ControlParental.App.UI/DemoStepViewModel.cs] Await show/hide overlay IPC and emit first-win on successful completion.
- [ ] 7.7 [P7.edit, src/ControlParental.App.UI/DemoStepPage.xaml.cs] Wire the real channel and completion flow.
- [ ] 7.8 [P7.edit, src/ControlParental.App.UI/MainWindow.xaml.cs] Route account/demo pages with channel and callbacks.
- [ ] 7.9 [P7.delete, src/ControlParental.App.UI/OnboardingViewModel.cs] Remove duplicate demo execution/timer methods after migration.
- [ ] 7.10 [P7.test, tests/ControlParental.App.UI.Tests/AccountStepTests.cs] Cover OS accounts, elevation copy, and absence of UI process launch.
- [ ] 7.11 [P7.test, tests/ControlParental.App.UI.Tests/DemoStepTests.cs] Cover overlay hide waiting, first-win, completion, and abandonment events.
- [ ] 7.12 [P7.verify] Run build/tests and verify overlay/funnel ordering.
- [ ] 7.M [P7.merge] Land PR #7 to `main` with `feat(onboarding): wire account and demo IPC`.

## PR #8: Localize onboarding and remove dead code (Fases 8 + 9)

> Estimated diff: ~500 changed lines — **OVER BUDGET** by additions+deletions. Depends on: PR #7 merged. Split localization (#8a) from deletion-only cleanup (#8b) if measured diff remains above 400.
> Files: `src/ControlParental.App.UI/{Strings/en-US/Strings.resw,Strings/es/Strings.resw,*.xaml,OnboardingStateService.cs,ConsentDialog.cs,ServiceInstallStepPage.xaml,ServiceInstallStepPage.xaml.cs,ServiceInstallStepViewModel.cs,MainWindow.xaml,OnboardingViewModel.cs}`, `src/ControlParental.Domain/ConsentStrings.cs`, `tests/ControlParental.App.UI.Tests`.

- [ ] 8.1 [P8.new, src/ControlParental.App.UI/Strings/es/Strings.resw] Add complete Spanish onboarding copy with correct accents.
- [ ] 8.2 [P8.new, src/ControlParental.App.UI/Strings/en-US/Strings.resw] Add matching neutral-English fallback keys.
- [ ] 8.3 [P8.edit, src/ControlParental.App.UI/*.xaml] Replace user-facing literals with `{x:Bind}` resource keys.
- [ ] 8.4 [P8.edit, src/ControlParental.App.UI/OnboardingStateService.cs] Replace remaining unaccented onboarding literals.
- [ ] 8.5 [P8.delete, src/ControlParental.App.UI/ConsentDialog.cs] Remove the unused console dialog.
- [ ] 8.6 [P8.delete, src/ControlParental.Domain/ConsentStrings.cs] Remove or reduce the obsolete copy wrapper while preserving engine resources.
- [ ] 8.7 [P8.delete, src/ControlParental.App.UI/ServiceInstallStepPage.xaml] Remove the obsolete MSIX-delivered service step.
- [ ] 8.8 [P8.delete, src/ControlParental.App.UI/ServiceInstallStepPage.xaml.cs] Remove its code-behind.
- [ ] 8.9 [P8.delete, src/ControlParental.App.UI/ServiceInstallStepViewModel.cs] Remove its dead view model.
- [ ] 8.10 [P8.edit, src/ControlParental.App.UI/MainWindow.xaml] Remove the fake in-page demo overlay copy.
- [ ] 8.11 [P8.delete, src/ControlParental.App.UI/OnboardingViewModel.cs] Remove obsolete pairing/settings/index navigation methods and references.
- [ ] 8.12 [P8.test, tests/ControlParental.App.UI.Tests] Snapshot both locales and assert removed artifacts/methods have no production references.
- [ ] 8.13 [P8.verify] Measure changed lines; split #8a/#8b if above 400, then run build/tests per slice.
- [ ] 8.M [P8.merge] Land the focused slice(s) to `main` with `refactor(onboarding): localize copy and remove dead code`.

## PR #9: Consolidate IPC regression coverage (Fase 10: integration tests)

> Estimated diff: ~250 changed lines (under 400). Depends on: PRs #1-#8 merged.
> Files: `tests/ControlParental.Service.Tests/OnboardingStateServiceTests.cs`, `tests/ControlParental.App.UI.Tests/{Interop/MockNamedPipeUIChannel.cs,OnboardingViewModelStateRouteTests.cs,OnboardingViewModelTests.cs}`.

- [ ] 9.1 [P9.new, tests/ControlParental.App.UI.Tests/Interop/MockNamedPipeUIChannel.cs] Create the canonical handler-driven App.UI IPC test double.
- [ ] 9.2 [P9.test, tests/ControlParental.Service.Tests/OnboardingStateServiceTests.cs] Add temp-path atomic-kill, outbox, and concurrency regressions.
- [ ] 9.3 [P9.test, tests/ControlParental.App.UI.Tests/OnboardingViewModelStateRouteTests.cs] Add kill-between-IPC-and-render resumability and no-double-advance tests.
- [ ] 9.4 [P9.test, tests/ControlParental.App.UI.Tests/OnboardingViewModelTests.cs] Migrate the eight legacy tests away from the deleted local store and add negative ADR mocks.
- [ ] 9.5 [P9.verify] Run `dotnet build --no-restore`, `dotnet test --verbosity normal`, formatting/analyzers, and all five audit regressions.
- [ ] 9.M [P9.merge] Land PR #9 to `main` with `test(onboarding): cover IPC state regressions`.
