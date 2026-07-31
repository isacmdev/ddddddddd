# Spec: onboarding

## Purpose

The onboarding capability introduces the first-run experience that walks an adult
through pairing the device, granting consent, configuring the child's standard
account, running an interactive protection demo, and offering the optional
managed upgrade. It enforces the project's architectural rule that the Service
owns persisted state (`backlog-control-parental-windows.md:32`) and that every
mutation crosses a named-pipe IPC boundary, so the user never sees an inflated
progress bar and never advances a step whose precondition the canonical store
has not acknowledged.

## Requirements

### Requirement: OnboardingState is owned by the Service

The Service SHALL be the single source of truth for `OnboardingState`,
persisting it under `%PROGRAMDATA%\ControlParental\onboarding_state.json` with
service-only ACL. App.UI SHALL NOT write any local copy of onboarding state
under `%LOCALAPPDATA%` or anywhere else. This implements backlog
`backlog-control-parental-windows.md:32` ("Servicio … dueño de SQLite")
and `:310` ("Persistir en ProgramData con ACL del servicio, nunca en el
perfil del menor"), and supersedes the legacy App.UI-owned
`ControlParental.Domain.OnboardingStateStore`.

#### Scenario: App.UI starts without a local state file

- **WHEN** App.UI launches and `%LOCALAPPDATA%\ControlParental\onboarding_state.json` does not exist
- **THEN** App.UI calls `GetOnboardingState` over IPC and renders the Service's response
- **AND** App.UI does NOT create a file under `%LOCALAPPDATA%` for onboarding state

#### Scenario: App.UI never persists onboarding state locally

- **WHEN** any onboarding step is advanced via `RecordOnboardingStepCompleted`, `AdvanceOnboardingStep`, `RecordFunnelEvent`, or `ResetOnboardingState`
- **THEN** no file is created or modified under `%LOCALAPPDATA%\ControlParental\` by App.UI
- **AND** the Service's persisted state reflects the change

### Requirement: State mutations go through named-pipe IPC

Every onboarding state mutation initiated by App.UI SHALL be expressed as an
IPC message to the Service and SHALL NOT touch the filesystem directly. The
Service SHALL apply the mutation, persist it, and return the authoritative
result. App.UI SHALL treat the Service response as authoritative and SHALL NOT
maintain a parallel in-memory store that survives a process restart.

#### Scenario: App.UI records a completed step via IPC

- **WHEN** App.UI calls `RecordOnboardingStepCompleted("consent")` over the `ControlParental.UI` named pipe
- **THEN** the Service persists the step completion and returns `StepCompletedResponse(success=true)`
- **AND** App.UI updates its view from the response, never from a local write

#### Scenario: App.UI queries initial state via IPC

- **WHEN** App.UI starts the onboarding flow
- **THEN** App.UI calls `GetOnboardingState` over IPC and uses the returned `OnboardingState` to drive navigation
- **AND** the Service returns the canonical state from `%PROGRAMDATA%\ControlParental\onboarding_state.json`

### Requirement: Service host registration has no duplicates

The Service `Program.cs` SHALL register `NamedPipeUIServer` and its hosted
adapter exactly once. A regression that re-registers them SHALL be caught by
`dotnet build`. This implements the audit finding that a duplicate
registration at `src/ControlParental.Service/Program.cs:347-356` prevents the
Service from starting.

#### Scenario: Service starts cleanly

- **WHEN** `dotnet build src/ControlParental.Service/ControlParental.Service.csproj` runs and the Service process starts
- **THEN** no duplicate `NamedPipeUIServer` / `NamedPipeUIServerHostedAdapter` registration is present in `Program.cs`
- **AND** the Service host builds and reaches `Started` state without a `System.InvalidOperationException`

### Requirement: Consent persists in Service SQLite

`GrantConsent` IPC SHALL persist the grant in the Service SQLite database
(`ControlParentalDbContext.Consent`) under `%PROGRAMDATA%`. The persisted grant
SHALL be readable via `GetConsentStatus` after an app restart and a full
machine reboot. App.UI SHALL consume consent via the new `IpcConsentService`
adapter that wraps the named-pipe channel. The legacy in-memory
`App.UI/ConsentService.cs` SHALL NOT be registered.

#### Scenario: Adult grants consent

- **WHEN** App.UI sends `GrantConsent(grantedByDeviceId)` over IPC
- **THEN** the Service writes a row to `ControlParentalDbContext.Consent` and returns `ConsentStatusSnapshot(IsGranted=true, GrantedAt, GrantedByDeviceId)`
- **AND** a subsequent `GetConsentStatus` from a fresh App.UI process returns `IsGranted=true`

#### Scenario: App.UI consumes consent via the IPC adapter

- **WHEN** App.UI resolves `IConsentService`
- **THEN** the resolution returns the `IpcConsentService` instance backed by `NamedPipeUIChannel`
- **AND** the in-memory no-op `App.UI/ConsentService.cs` is not registered in the DI container

### Requirement: Consent step blocks advance until persisted

The onboarding consent step SHALL NOT mark itself complete unless the Service
acknowledges the grant. An empty `catch {}` swallow of consent-persistence
errors is prohibited (DoD-G, `backlog-control-parental-windows.md:46`). On
failure, the consent page SHALL display the `Onboarding.Consent.PersistFailed`
copy and the onboarding SHALL NOT advance to the next step. This implements
backlog `backlog-control-parental-windows.md:429` ("Bloquear el avance del
onboarding hasta consentir") and `:432`.

#### Scenario: Consent persistence succeeds

- **WHEN** the adult clicks "Acepto" and the Service returns `ConsentStatusSnapshot(IsGranted=true)`
- **THEN** App.UI awaits `EnsureConsentPersistedAsync` and then calls `RecordOnboardingStepCompleted("consent")`
- **AND** the onboarding advances to the account step

#### Scenario: Consent persistence fails

- **WHEN** the adult clicks "Acepto" and the IPC call throws or returns a failure
- **THEN** App.UI surfaces the `Onboarding.Consent.PersistFailed` copy
- **AND** `RecordOnboardingStepCompleted("consent")` is NOT called
- **AND** the onboarding does NOT advance

#### Scenario: Empty catch is prohibited

- **WHEN** the consent-persistence call site is reviewed
- **THEN** there is no empty `catch {}` block at `ConsentPage.xaml.cs` (formerly lines 79–82)
- **AND** every catch arm either rethrows, returns a typed error, or surfaces user-visible copy

### Requirement: Consent offline UX is fail-closed (ADR-001)

If the Service is unreachable when the adult clicks "Acepto", the consent step
SHALL NOT advance and SHALL NOT persist the grant locally as a fallback. The
UI SHALL display `Onboarding.Consent.ServiceUnavailable` copy. No DPAPI outbox
or retry-queue is introduced for consent. This implements ADR-001 and aligns
with `backlog-control-parental-windows.md:444` ("el progreso nunca miente; la
elevación se pide al adulto") and `:432`.

#### Scenario: Service unreachable on grant attempt

- **WHEN** `GrantConsent` IPC times out or the named pipe is unavailable
- **THEN** the consent step's "Acepto" button stays disabled until the next successful IPC
- **AND** no local file, DPAPI blob, or outbox row is written
- **AND** the UI displays `Onboarding.Consent.ServiceUnavailable`

#### Scenario: No outbox fallback for consent

- **WHEN** the consent flow is reviewed
- **THEN** no DPAPI scope-machine outbox is added by App.UI for consent persistence
- **AND** the failure path returns `IpcUnavailableReason.Timeout` to drive the UX

### Requirement: Progress bar reflects real T12 state with honest total

The progress bar SHALL report `ProgressTotal = 5` matching the 5-step
onboarding order `pairing(0) → consent(1) → account(2) → demo(3) → managed(4)`
(Engram project memory #113). `ProgressCount` SHALL be derived solely from the
real `IEnforcementLevelMonitor` snapshot; no inflation branch is permitted. If
the Service has not yet responded to `GetEnforcementLevel`, the progress label
SHALL read "Estado desconocido" and `ProgressCount` SHALL be `0`. This
implements backlog `backlog-control-parental-windows.md:440` ("nunca
inflado"), `:442` ("reanudable"), and `:444` ("el progreso nunca miente").

#### Scenario: All four T12 checks pass

- **GIVEN** the Service reports `EnforcementLevel == Standard`, no `ServiceNotRunning`, no `ChildIsAdministrator`, no `AgentNotResponding`/`HookTimeout`, and no `PreventiveLayerUnavailable`
- **WHEN** App.UI evaluates `CalculateRealProgress`
- **THEN** `ProgressCount` is `4` and the label reads `"Protección 4 de 5"`

#### Scenario: One T12 check fails

- **GIVEN** the Service reports `ChildIsAdministrator`
- **WHEN** App.UI evaluates `CalculateRealProgress`
- **THEN** `ProgressCount` is `3` and the label reads `"Protección 3 de 5"`
- **AND** no fallback counts completed steps to inflate the number

#### Scenario: Service not yet reachable

- **WHEN** App.UI starts and no successful `GetEnforcementLevel` IPC response has arrived
- **THEN** the progress label reads `"Estado desconocido"`
- **AND** `ProgressCount` is `0`
- **AND** the progress bar shows an empty fill

#### Scenario: ProgressTotal is five

- **WHEN** the onboarding window is rendered
- **THEN** `MainWindow.xaml` `ProgressBar.Maximum == 5`
- **AND** `OnboardingViewModel.ProgressTotal == 5`

### Requirement: Pairing IPC is the only pairing path

Every pairing attempt SHALL invoke `PairDevice` IPC over the named pipe. App.UI
SHALL NOT use `Task.Delay`, simulated responses, or local fallbacks for
pairing. HTTP error codes from the backend SHALL be mapped to typed
`PairingResult` status values per T24 contract
(`backlog-control-parental-windows.md:413–420`) and surfaced to the UI with
dedicated `Strings.resw` copy keys. This implements the audit finding that
`PairingViewModel.PairWithCodeAsync` previously fell through a
`Task.Delay(500)` simulation at `PairingViewModel.cs:147`.

#### Scenario: PairDevice with a valid code

- **GIVEN** a non-null `NamedPipeUIChannel` and a complete 6-character code with a selected age band
- **WHEN** the user taps "Emparejar"
- **THEN** App.UI sends `PairDevice(Code, AgeBand)` over IPC
- **AND** the Service calls `PairingService.PairAsync` and returns `PairDeviceResponse(Success=true, DeviceId=...)`

#### Scenario: Backend returns HTTP 404

- **WHEN** `PairingService.PairAsync` returns `PairingResult.InvalidCode`
- **THEN** App.UI displays the `PairingError_InvalidCode` copy ("El código no existe. Verificá con tu tutor.")
- **AND** no automatic retry is attempted (T24 retry policy)

#### Scenario: Backend returns HTTP 410

- **WHEN** `PairingService.PairAsync` returns `PairingResult.ExpiredCode`
- **THEN** App.UI displays the `PairingError_ExpiredCode` copy ("El código venció. Pedile uno nuevo a tu tutor.")

#### Scenario: Backend returns HTTP 429

- **WHEN** `PairingService.PairAsync` returns `PairingResult.TooManyRequests`
- **THEN** App.UI displays the `PairingError_TooManyRequests` copy ("Demasiados intentos. Esperá unos minutos.")

#### Scenario: Backend returns HTTP 5xx or network error

- **WHEN** `PairingService.PairAsync` returns `PairingResult.ServerError` or `PairingResult.NetworkError`
- **THEN** App.UI displays the `PairingError_ServerError` or `PairingError_NetworkError` copy from `Strings.resw`

#### Scenario: Channel missing

- **WHEN** `PairingViewModel.PairWithCodeAsync` is invoked with a null `NamedPipeUIChannel`
- **THEN** the call throws `InvalidOperationException` ("IPC channel missing for pairing")
- **AND** no `Task.Delay` fallback is executed

### Requirement: Pairing validates age band and uses correct wire format

The pairing page SHALL require an age band selection before "Emparejar" is
enabled. The ComboBox SHALL bind its wire value via `Tag` (e.g. `"7-12"`,
`"13-16"`, `"17-18"`) and SHALL NOT pass a localized `Content` literal
("7-12 años") to the IPC payload. The "Emparejar" button SHALL remain
disabled while `SelectedAgeBandIndex < 0` or the code is incomplete. This
implements ADR-004, `backlog-control-parental-windows.md:417` ("Age band:
obligatorio, valores `"7-12" | "13-16" | "17-18"`"), and T25 acceptance
criteria.

#### Scenario: Age band not selected

- **WHEN** the user has typed a complete 6-character code but no age band
- **THEN** the "Emparejar" button is disabled
- **AND** if somehow invoked, App.UI surfaces `Onboarding.Pairing.AgeBandMissing` and does NOT call IPC

#### Scenario: Age band wire format

- **WHEN** the user selects "7-12 años" in the ComboBox
- **THEN** the IPC `PairDevice.AgeBand` field is the literal string `"7-12"` (no `años` suffix)
- **AND** the wire value is sourced from `ComboBoxItem.Tag`, not from `ComboBoxItem.Content`

### Requirement: Managed step is opt-in and edition-honest

`ManagedStepPage` SHALL surface the user's current `EnforcementLevel` and
SHALL offer the MANAGED upgrade (T31 spirit) only where the edition permits.
On `Standard`, the page SHALL show a "Activar WDAC" affordance that sends
`MsSettingsOpen("appsfeatures")` over IPC (delegating elevation to the
Service). On `Unknown` or `Degraded`, the page SHALL show honest copy
("No disponible en esta edición", "Reparar primero") and SHALL NOT block
the user. The managed step SHALL NEVER hard-enforce WDAC/AppLocker/kiosk
in this change (those are T31 bulk scope). This implements
`backlog-control-parental-windows.md:444` ("la elevación se pide al adulto")
and `:507` ("la oferta nunca bloquea STANDARD").

#### Scenario: Current level is Managed

- **GIVEN** the Service reports `EnforcementLevel.Managed`
- **WHEN** the managed step renders
- **THEN** the page shows the "Capa preventiva activa" success state

#### Scenario: Current level is Standard and edition permits

- **GIVEN** the Service reports `EnforcementLevel.Standard` and the edition supports WDAC
- **WHEN** the user taps "Activar WDAC"
- **THEN** App.UI sends `MsSettingsOpen("appsfeatures")` over IPC
- **AND** App.UI does NOT call `Process.Start` directly

#### Scenario: Current level is Unknown or Degraded

- **WHEN** the Service reports `EnforcementLevel.Unknown` or `EnforcementLevel.Degraded`
- **THEN** the page shows the neutral "Estado desconocido" or "Reparar primero" copy
- **AND** no blocking CTA is presented

### Requirement: Account step lists real OS accounts

`AccountStepViewModel.LoadAccountsAsync` SHALL fetch the account list via the
`ListAccounts` IPC message and SHALL NOT hardcode any placeholder
(`"UsuarioTest"` or equivalent). The hardcoded `Task.Delay(300)` and
`AccountItem("UsuarioTest", ...)` calls at `AccountStepViewModel.cs:72,79`
SHALL be removed. This implements backlog `backlog-control-parental-windows.md:439`
(third onboarding step is "crear/confirmar cuenta estándar del menor") and
T37 §Impl punto 2.

#### Scenario: LoadAccounts success

- **WHEN** App.UI calls `LoadAccountsAsync` and the Service returns `AccountList` with the OS accounts
- **THEN** the view model populates `Accounts` with `AccountItem(Username, Type, IsStandard)` entries derived from the IPC payload

#### Scenario: LoadAccounts failure or IPC unavailable

- **WHEN** `ListAccounts` IPC returns null or throws
- **THEN** the view model surfaces `Onboarding.Account.ServiceUnavailable` copy
- **AND** no hardcoded placeholder is displayed

### Requirement: Account creation requires adult elevation

When `CreateAccount` IPC returns `RequiresElevation = true`, App.UI SHALL
display the `Onboarding.Account.ElevationRequired` copy ("Tu tutor necesita
autorizar la creación de la cuenta. Pedile que abra la app.") and SHALL NOT
invoke `Process.Start` or any installer. Elevation is the responsibility of
the Service (LocalSystem), per ADR-008 and `backlog-control-parental-windows.md:444`.

#### Scenario: CreateAccount succeeds without elevation

- **WHEN** the Service returns `CreateAccountResponse(Success=true, RequiresElevation=false)`
- **THEN** App.UI updates the account list and proceeds

#### Scenario: CreateAccount requires elevation

- **WHEN** the Service returns `CreateAccountResponse(Success=false, RequiresElevation=true)`
- **THEN** App.UI surfaces `Onboarding.Account.ElevationRequired` copy
- **AND** App.UI does NOT call `Process.Start`

### Requirement: Demo emits onboarding_first_win via IPC

When the demo step completes successfully, App.UI SHALL send
`RecordFunnelEvent(FunnelEventType.OnboardingFirstWin)` over IPC so the
Service writes the event to the SQLite outbox table. This implements
`backlog-control-parental-windows.md:517` (T32 catalog includes
`onboarding_first_win`) and `:443` (T26 §Impl punto 5 funnel events).

#### Scenario: Demo countdown reaches zero without error

- **WHEN** `DemoStepViewModel.RunDemoAsync` finishes its countdown successfully
- **THEN** `RecordFunnelEvent(OnboardingFirstWin)` is sent over IPC before the next step is advanced
- **AND** the Service persists a row to the `ControlParentalDbContext.Outbox` table with `tipo='behavioral_events'`

#### Scenario: Demo is aborted before completion

- **WHEN** the user closes the demo before the countdown finishes
- **THEN** App.UI sends `RecordFunnelEvent(OnboardingAbandoned)` over IPC
- **AND** the onboarding does NOT advance to the managed step

### Requirement: Demo uses the real SessionAgent overlay

`DemoStepViewModel.RunDemoAsync` SHALL send `ShowOverlayCommand` and
`HideOverlayCommand` over IPC; the Service SHALL translate them into the
existing `ShowOverlay` / `HideOverlay` messages on the agent pipe so the
overlay is painted by the SessionAgent (T08), not by a fake in-page overlay.
This implements `backlog-control-parental-windows.md:439` ("auto-demostración
… muestra el overlay 2–3 s").

#### Scenario: Demo starts

- **WHEN** the user reaches the demo step
- **THEN** App.UI sends `ShowOverlayCommand(reason, ctaLabel)` over IPC
- **AND** the SessionAgent paints the overlay window

#### Scenario: Demo countdown ends

- **WHEN** the demo countdown reaches zero
- **THEN** App.UI sends `HideOverlayCommand` over IPC
- **AND** the SessionAgent dismisses the overlay window

### Requirement: All onboarding copy lives in Strings.resw

Every user-facing string in the onboarding flow SHALL live in
`src/ControlParental.App.UI/Strings/es/Strings.resw` (default) and
`src/ControlParental.App.UI/Strings/en-US/Strings.resw` (fallback). XAML
SHALL bind via `{x:Bind Strings.<Key>}`. The locale chain SHALL be
Spanish default → English fallback; no other locales are introduced by this
change (ADR-007). This implements `backlog-control-parental-windows.md:428`
("Sistema de copy: strings externalizados (.resw) … origen único") and
T25 §Impl punto 3.

#### Scenario: XAML binds via resource key

- **WHEN** any onboarding page renders text
- **THEN** the XAML uses `{x:Bind Strings.<Key>}` and not a hardcoded literal
- **AND** the corresponding key exists in `Strings/es/Strings.resw`

#### Scenario: English fallback resolves

- **WHEN** the system locale is `en-US` and a key is present in both `Strings.es.resw` and `Strings.en-US.resw`
- **THEN** the rendered text comes from `Strings.en-US.resw`

### Requirement: Copy is professional Spanish with correct accents (DoD-G)

All Spanish user-facing strings in the onboarding flow SHALL be neutral or
professional Spanish with proper accents (e.g. `protección`, `está`,
`Registro`). The pre-existing strings `"Probemos tu proteccion"`,
`"Veamos como funciona la proteccion"`, and `"Tu proteccion esta activa!"`
SHALL be replaced with accented versions or moved to `Strings.resw` with
correct accents. This implements `backlog-control-parental-windows.md:46`
(DoD-G quality bar).

#### Scenario: Accented strings

- **WHEN** the onboarding step titles are rendered
- **THEN** every visible string contains correctly accented Spanish words
- **AND** no `proyeccion` / `esta` / `Registr`-style truncations are present in user-visible copy

### Requirement: Dead code is removed (DoD-G cleanup)

The dead artifacts `ServiceInstallStepPage.xaml(.cs)`,
`ServiceInstallStepViewModel.cs`, `App.UI/ConsentService.cs`, and
`App.UI/ConsentDialog.cs` SHALL be deleted. The duplicated Domain type
re-declarations at `src/ControlParental.App.UI/Interop/UIMessages.cs:75-104`
SHALL be deleted in favor of `ControlParental.Domain.OnboardingState`,
`OnboardingStep`, `OnboardingStepStatus`, and `FunnelEvent`. Dead VM methods
(`OnboardingViewModel.ExecuteDemoStepAsync`, `OnboardingViewModel.OnDemoTimerTick`,
`OnboardingViewModel.NavigateToStepByIndex`, `OnboardingViewModel.OpenMsSettings`)
SHALL be removed once their replacements land. This implements DoD-G and the
cleanup backlog cited in `proposal.md:131`.

#### Scenario: No dead ServiceInstall step references

- **WHEN** the repository is searched for `ServiceInstallStep`
- **THEN** no production references remain (only historical/test references are tolerated)

#### Scenario: No duplicate IPC types in App.UI

- **WHEN** the App.UI assembly references `ControlParental.Domain`
- **THEN** the duplicates at `App.UI/Interop/UIMessages.cs:75-104` are gone
- **AND** `OnboardingStateResponse` carries the canonical `Domain.OnboardingState`

#### Scenario: Compile clean after dead-code removal

- **WHEN** `dotnet build` runs after the cleanup
- **THEN** no compilation errors are introduced by the deletions

### Requirement: Onboarding is resumable across restarts

App restart after consent (or any completed step) SHALL resume past that
step. `GetOnboardingState` SHALL return the persisted state from the Service,
and the App.UI SHALL navigate to the next pending step without re-prompting
the user for already-completed steps. This implements
`backlog-control-parental-windows.md:442` ("Onboarding reanudable, estado
persistido en el almacén del servicio").

#### Scenario: Restart after consent

- **WHEN** App.UI restarts after the adult granted consent
- **THEN** `GetOnboardingState` returns `consent=Completed`
- **AND** App.UI navigates to the account step, NOT the consent step

#### Scenario: Restart after kill mid-advance

- **WHEN** App.UI is killed after `RecordOnboardingStepCompleted` IPC was sent but before the next step was rendered
- **THEN** App.UI restart fetches the persisted state and recovers without double-advancing
- **AND** the previously persisted `InProgress` step is presented, not skipped

### Requirement: Regression tests cover the five audit bugs

Regression tests SHALL exist and pass for the five critical audit findings:

1. Service builds and starts without duplicate registrations.
2. Consent persistence blocks advance when the IPC call fails (no silent swallow).
3. `CalculateRealProgress` returns the honest count from the real
   `IEnforcementLevelMonitor` snapshot, with no `enforcementLevelMonitor == null`
   inflation branch.
4. Pairing maps HTTP 404 / 410 / 429 / 5xx to the corresponding typed
   `PairingResult` and surfaces the correct `Strings.resw` copy.
5. `OnboardingStateService.SaveStateAsync` uses atomic write (`.tmp` + rename),
   so a kill between write and move leaves the prior state intact.

`MockNamedPipeUIChannel` SHALL be the canonical test double for App.UI IPC
tests. `OnboardingStateService` SHALL accept a constructor-injected
`dataFolderPath` so tests use temp directories, not `%LOCALAPPDATA%`.

#### Scenario: Consent blocks advance on failure

- **WHEN** the mock channel returns a failure for `GrantConsent`
- **THEN** the test asserts `OnboardingViewModel.OnStepCompletedAsync("consent")` does NOT call `RecordOnboardingStepCompleted`

#### Scenario: Honest progress calculation

- **WHEN** the monitor reports three passing checks and one failure
- **THEN** `CalculateRealProgress` returns `3`, not `4`

#### Scenario: Pairing error mapping

- **WHEN** the mock channel returns `PairDeviceResponse(InvalidCode, ...)`
- **THEN** the VM sets `HasError = true` and `ErrorMessage` resolves to `PairingError_InvalidCode`

#### Scenario: Atomic write under kill

- **WHEN** a writer is killed between `WriteAllTextAsync` and `File.Move`
- **THEN** a subsequent load returns the prior intact state, not a partial write

#### Scenario: MockNamedPipeUIChannel as canonical test double

- **WHEN** App.UI tests register handlers for IPC messages
- **THEN** they use `MockNamedPipeUIChannel` (no real pipe is opened)
- **AND** tests run in temp directories, not `%LOCALAPPDATA%`
