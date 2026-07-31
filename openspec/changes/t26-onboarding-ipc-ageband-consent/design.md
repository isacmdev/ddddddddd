# Design: T26 Onboarding — IPC + Age Band + Consent + Account + Managed

**Change**: `t26-onboarding-ipc-ageband-consent`
**Status**: design
**Source of truth**: `openspec/changes/t26-onboarding-ipc-ageband-consent/proposal.md`
**Architectural anchors**: `backlog-control-parental-windows.md:32`, `:46`, `:309–310`, `:429`, `:432`, `:440`, `:442`, `:444`, `:446`, `:517`
**Out of scope**: T31 bulk (WDAC/AppLocker/kiosk), T18/T19/T20 (sync, push, schedulers), T05–T11 (already done), backend (T14 contract consumed as-is).

This document is the technical blueprint that turns the proposal into a sequenced set of PRs. It captures **what we decide**, **why we decide it**, **which file owns what**, and **which test catches regressions**. It does not propose scope changes; the proposal owns scope.

---

## 0. Executive summary

The T26 implementation is non-functional on its critical path today: the Service fails to start because of duplicate DI registrations (`Program.cs:347–356`), App.UI swallows consent errors (`ConsentPage.xaml.cs:79–82`), the progress bar reports `Maximum="4"` against a 5-step list (`OnboardingViewModel.cs:63`, `MainWindow.xaml:18`), pairing falls through a `Task.Delay(500)` simulation (`PairingViewModel.cs:147`), and there is no `ManagedStepPage.xaml`. Underneath all five, App.UI owns `OnboardingState` and writes to `%LOCALAPPDATA%` instead of letting the Service own SQLite in `%PROGRAMDATA%` — the architectural inversion that T26 was supposed to enforce.

This design flips the ownership in **one** PR (Fase 5) and lands the four symptom fixes around it in a 9-PR chain. Total diff spread respects the **400-line review budget** (`sdd-phase-common.md` §E) — Fase 5 is the largest and is sequenced 4th so it lands on a working baseline.

**Decisions taken under delegation (no escalation):**

| # | Decision | ADR | Rationale anchor |
|---|----------|-----|------------------|
| Consent-offline UX | **Fail-closed** (Option A) | ADR-001 | `:444` (la elevación se pide al adulto), `:432` (bloquear el avance del onboarding hasta consentir), audit finding #2 |
| Ownership of `OnboardingState` | **Service** (single source of truth) | ADR-002 | `:32`, `:310`, `:446` |
| Duplicated IPC types in App.UI | **Delete App.UI copies**, carry `Domain` types over IPC | ADR-003 | DoD-G purity, single source of truth |
| Age band wire format | **`"7-12"`** (no `años` suffix) | ADR-004 | `:417`, T24 contract, existing VM mapping |
| App.UI `IEnforcementLevelMonitor` | **Real proxy** reading from IPC, **no inflation** | ADR-005 | `:440` (nunca inflado), `:444` |
| JSON serializer | **`JsonSerializerContext` source-gen** | ADR-006 | `:21`, `:46` (AOT-safe) |
| Onboarding copy locale chain | **`Strings.resw` es + `Strings.en.resw`**, no other locales | ADR-007 | T25 §Impl punto 3 (localizable), `:432` |
| Elevation UX | **Service prompts UAC**, App.UI displays text only | ADR-008 | `:444`, `:309`, T31 spirit |
| Testable state store | **Constructor-injected path** + atomic write (`.tmp` + rename) | ADR-009 | T36 §Impl punto 1, audit finding (existing pattern is broken) |

---

## 1. Component map

```
+--------------------------------------------------------------------------------+
|  KID'S INTERACTIVE SESSION (Session 1+)                                         |
|                                                                                  |
|   +-------------------+         +---------------------+                          |
|   |  App.UI (WinUI 3)  |         |  SessionAgent (.exe) |                          |
|   |  PID = kid         |         |  PID = kid           |                          |
|   |  Owner: writes? NO |         |  Owner: paints overlay only |                  |
|   +-------------------+         +---------------------+                          |
|            |                                  ^                                  |
|            |  named pipe                      |  named pipe                       |
|            |  ControlParental.UI              |  ControlParental.Agent            |
|            |  ACL = LocalSystem Full +        |  ACL = LocalSystem Full +         |
|            |        InteractiveSid Allow      |        kid-SID Allow              |
|            v                                  |                                  |
+--------------------------------------------------------------------------------+
             |                                  |
             |    +-----------------------------+
             |    |
   +---------v----v--------------------------------------------------------------------+
   |  SERVICE (LocalSystem, Session 0, auto-start)                                    |
   |                                                                                  |
   |   Pipe servers                          Domain handlers                          |
   |   ----------                            ----------------                          |
   |   NamedPipeServer  (Agent ↔ Service)    T12 EnforcementLevelMonitor               |
   |   NamedPipeUIServer (App.UI ↔ Service)  T24 PairingService                         |
   |                                         T25 ConsentService   (SQLite ◀─────────┐  |
   |   Message dispatch                     T26 OnboardingStateService  (SQLite ◀─┐│ |
   |   -----------------                    T37 AccountManager (SQLite)          ◀─┐││ |
   |   PipeServerListener (agents)          NamedPipeUIServerHostedAdapter        │││ |
   |   UIMessageHandler (App.UI msgs)                                                 │││ |
   |                                          Funnel/Behavioral: ControlParentalDbContext (outbox table)│││
   |                                              │                                   │││ |
   |                                              v                                   │││ |
   |   +-----------------------------------------------------+                         │││ |
   |   |  ProgramData service-only ACL                        | ◀────────────────────┘││ |
   |   |  C:\ProgramData\ControlParental\                     | ◀─────────────────────┘│ |
   |   |  ├── onboarding_state.json (OnboardingStateService) | ◀──────────────────────┘ |
   |   |  ├── controlparental.db (EF Core SQLite)             | <───────────────────────────────────────┐
   |   |  │   ├── Consent          (T25)                      |                                         |
   |   |  │   ├── Account          (T37)                      |                                         |
   |   |  │   ├── outbox           (T03/T32 funnel events)    |                                         |
   |   |  │   └── policy/grants/usage                          |                                         |
   |   |  ├── secrets.dpapi       (T16)                       |                                         |
   |   |  └── logs                                                  service writes only; deny standard users |
   |   +-----------------------------------------------------+                                         |
   +--------------------------------------------------------------------------------------------------+
```

**Process boundaries and ownership:**

| Process | Runs as | Touches filesystem? | Why |
|---|---|---|---|
| `App.UI` | kid (interactive) | **No** for state. Reads `%LOCALAPPDATA%\ControlParental\app_startup.log` only for diagnostic logging (existing) | `:32` says Service owns SQLite; App.UI is a renderer + IPC client |
| `SessionAgent` | kid (interactive) | Yes — overlay UI only, registry-frozen via T37 ACL | T38: agent obeys, does not decide |
| `Service` | `LocalSystem`, Session 0 | Yes — `%PROGRAMDATA%\ControlParental\` only, with service-only ACL (T37) | `:32`, `:310`, the único escritor |

**Ownership of contracts after this change:**

| Contract | Producer (implements) | Consumer(s) |
|---|---|---|
| `IOnboardingStateStore` (legacy, to be deleted) | n/a — **removed** | n/a |
| `IOnboardingStateService` (the Service-side interface) | `ControlParental.Service.OnboardingStateService` | `ControlParental.Service.UIMessageHandler` |
| `IOnboardingStateService` exposed over IPC | `NamedPipeUIServer` reads `GetOnboardingState` / `RecordOnboardingStepCompleted` / `RecordFunnelEvent` | App.UI (`OnboardingViewModel`) |
| `IConsentService` | `ControlParental.Service.ConsentService` (SQLite via `ControlParentalDbContext`) | `ControlParental.Service.UIMessageHandler.GrantConsentAsync` (IPC-routed) |
| `IConsentService` (App.UI adapter — renamed) | **`ControlParental.App.UI.IpcConsentService`** wraps `IUIChannel.SendAsync(new GrantConsent())` and `QueryAsync<GetConsentStatus, ConsentStatusSnapshot>` | `ControlParental.App.UI.OnboardingViewModel`, `ConsentPage.xaml.cs` |
| `IEnforcementLevelMonitor` | The `ControlParental.Service` implementation is the **canonical owner** of the T12 state. App.UI gets a thin `ServiceEnforcementLevelMonitor` that proxies IPC `GetEnforcementLevel` → `EnforcementLevelResponse.Checks` → `IReadOnlyList<EnforcementIssue>` (read at startup and on every `LevelChanged` event the UI polls) | `ControlParental.App.UI.OnboardingViewModel` (T12 territory; we do not duplicate the engine, we just expose it locally) |

---

## 2. IPC contract catalogue

All messages are JSON-serialized UTF-8 records on `ControlParental.UI` named pipe (full-duplex, single client at a time on App.UI side; existing on Service side via `NamedPipeUIServer` with `PipeOptions.Asynchronous`).

**Direction conventions** (this document): `UI → Service` means the App.UI sends the record and waits for the corresponding `*Response`; `Service → UI` means it is the unsolicited push direction (in our case the only "push" is the response back over the same connection).

**Routing table (`UIMessageHandler.HandleAsync`)** — new entries are flagged `[NEW]`; entries touched by this change are flagged `[MOD]`:

| MessageType | Direction | Status | Handler | Fase |
|-------------|-----------|--------|---------|------|
| `GetEnforcementLevel` | UI → Service | existing | `EnforcementLevelQueryHandler` | 2 |
| `EnforcementLevelResponse` | Service → UI | `[MOD]` — now also exposes `Checks` derived from `IEnforcementLevelMonitor.CurrentIssues` | (response) | 2 |
| `GetOnboardingState` | UI → Service | existing | `OnboardingStateService.GetStateAsync` | 5 |
| `OnboardingStateResponse` | Service → UI | `[MOD]` — payload uses `Domain.OnboardingState` (after duplicate types removed) | (response) | 5 |
| `RecordOnboardingStepCompleted` | UI → Service | existing | `OnboardingStateService.RecordStepCompletedAsync` | 5 |
| `StepCompletedResponse` | Service → UI | existing | (response) | 5 |
| `RecordFunnelEvent` | UI → Service | existing | `OnboardingStateService.RecordFunnelEventAsync` (writes to outbox via T03 table) | 7 |
| `ShowOverlayCommand` | UI → Service | existing — wired through `Service → Agent` pipe | translates to `ShowOverlay(reason, ctaLabel)` on the Agent pipe | 7 |
| `HideOverlayCommand` | UI → Service | existing — same translation | translates to `HideOverlay()` | 7 |
| `GetConsentStatus` | UI → Service | `[NEW]` | `ControlParental.Service.ConsentService.GetConsentStatusAsync` (SQLite) | 1 |
| `ConsentStatusSnapshot` | Service → UI | `[NEW]` | (response — `IsGranted`, `GrantedAt`, `GrantedByDeviceId`) | 1 |
| `GrantConsent` | UI → Service | `[NEW]` | `ControlParental.Service.ConsentService.GrantAsync` (SQLite) | 1 |
| `GetEnforcementLevel` | UI → Service | existing — App.UI proxy uses it | `EnforcementLevelQueryHandler` (already returns `Level + Checks`) | 2 |
| `ListAccounts` | UI → Service | `[NEW]` (replaces `GetAccounts`) | `AccountManager.GetAccountsAsync` enriched with `IsStandard`/`Type` | 6 |
| `AccountList` | Service → UI | `[NEW]` | (response — `IReadOnlyList<AccountInfo>`) | 6 |
| `CreateAccount` | UI → Service | `[MOD]` — full payload, errors include `RequiresElevation` | `AccountManager.CreateStandardAccountAsync` | 6 |
| `CreateAccountResponse` | Service → UI | `[MOD]` — already at `UIMessageHandler.cs:140-166`; we extend payload with `RequiresElevation` flag explicitly | (response) | 6 |
| `ConvertAccount` | UI → Service | existing | `AccountManager.ConvertToStandardAsync` | 6 |
| `ConvertAccountResponse` | Service → UI | existing | (response) | 6 |
| `PairDevice` | UI → Service | existing — wire value = `"7-12" | "13-16" | "17-18"` (ADR-004) | `PairingService.PairAsync` | 3 |
| `PairDeviceResponse` | Service → UI | `[MOD]` — final shape from `PairingResult` (success / `InvalidCode` / `ExpiredCode` / `TooManyRequests` / `ServerError` / `NetworkError`) — already implemented at `UIMessageHandler.cs:92-118` | (response) | 3 |
| `ResetOnboardingState` | UI → Service | `[NEW]` (used by tests + recovery) | `OnboardingStateService.ResetAsync` (writes initial state) | 10 |
| `MsSettingsOpen` | UI → Service | `[NEW]` — Service-side launcher runs `Process.Start("ms-settings:...")` because elevation is required for `ms-settings:appsfeatures`; App.UI cannot elevate mid-session (`:444`, ADR-008) | `Process.Start` from Service (LocalSystem) | 6 |

**Error shapes (no plain string):**

Every `*Response` carries a typed result. There are **two** layers of error to distinguish:

1. **IPC transport errors** (`NamedPipeUIChannel.QueryAsync` returns `null` on connect-timeout, IO failure, or deserialization failure). The App.UI side converts `null` into `IpcUnavailableReason { Timeout = true }` shape (a discriminated field on the response, not a separate type — see ADR-006 about types). The VM uses this to drive the "Service not available, retry" UX in ADR-001.
2. **Domain errors** carry a typed `PairingStatus` / `AccountCreationResult` / `ConsentStatus` enum + `ErrorMessage` for the human copy. App.UI renders the enum with copy from `Strings.resw` (catalogue below).

**Per-message rationale and Fase:**

### `GetOnboardingState` → `OnboardingStateResponse`
- **Why**: single read of state on App.UI startup; we are removing App.UI's local write path.
- **Fase**: 5 (architectural flip).
- **Field shape** (after duplicate deletion): reuses `ControlParental.Domain.OnboardingState` (`CurrentStepIndex`, `IsCompleted`, `IsAbandoned`, `Steps`, `Events`) — see ADR-003.

### `RecordOnboardingStepCompleted(string stepId)` → `StepCompletedResponse(bool success)`
- **Why**: when a step page completes, App.UI calls this to advance the canonical state. Service persists; if persistence fails, returns `success=false` and the App.UI blocks the advance (Fase 1: the `OnboardingViewModel.OnStepCompletedAsync("consent")` path already calls this; Fase 5 stops the App.UI from also writing its local file).
- **Fase**: 5 (and the consent-blocking flavour lands in 1).

### `RecordFunnelEvent(string eventName)` → void / `StepCompletedResponse`
- **Why**: T32 catalog (`backlog-control-parental-windows.md:517`) requires `onboarding_step_reached`, `onboarding_first_win`, `onboarding_completed`, `onboarding_abandoned`. Service writes to the SQLite outbox table (existing T03) so the upload is T18-driven and resilient to offline (T18 §Restr: offline-tolerante).
- **Fase**: 7 (the live code path).

### `GetConsentStatus()` → `ConsentStatusSnapshot`
- **Why**: Onboarding can resume after restart. We need to know whether consent is already granted before unblocking the "Acepto" affordance; an unfinished user should be told to redo the step.
- **Fase**: 1.

### `GrantConsent(string grantedByDeviceId)` → `ConsentStatusSnapshot`
- **Why**: The one action that mutates consent. Calls into `ControlParental.Service.ConsentService.GrantAsync` (the SQLite-backed implementation). Persists to SQLite. Returns the post-write snapshot.
- **Fase**: 1.

### `GetEnforcementLevel()` → `EnforcementLevelResponse`
- **Why**: drives the honest progress bar.
- **Fase**: 2.
- **Current shape already exposes** `Level + Checks`. We do not change wire shape; we change how App.UI consumes it (inject real `IEnforcementLevelMonitor`, derive `CurrentIssues` from `Checks`).

### `ListAccounts()` → `AccountList`
- **Why**: replaces App.UI's hardcoded `("UsuarioTest", Standard, true)` (`AccountStepViewModel.cs:79`).
- **Fase**: 6.
- **Shape**: `IReadOnlyList<AccountInfo>` where `AccountInfo(Username, Type, IsStandard)`.

### `CreateAccount(string Username, string Password)` → `CreateAccountResponse(bool Success, bool RequiresElevation, string? ErrorMessage)`
- **Why**: T37 §Impl punto 2 ("ejecutar con elevación del padre").
- **Field extension**: existing `UIMessageHandler.HandleCreateAccountAsync` already maps `RequiresElevation` into `ErrorMessage` via string concatenation (`UIMessageHandler.cs:147-151`). **That is wrong** — the elevation flag is data, not copy. We promote it to a first-class field. See ADR-008.
- **Fase**: 6.

### `PairDevice(string Code, string AgeBand)` → `PairDeviceResponse`
- **Why**: T24 contract (`backlog-control-parental-windows.md:413–420`).
- **Wire value of `AgeBand`**: exactly `"7-12" | "13-16" | "17-18"`. The XAML ComboBox shows display copy `"7-12 años"`; the VM exposes `SelectedAgeBand` derived from the index (existing code at `PairingViewModel.cs:72-78`) which already returns the unen-yeared value — what we need to fix is the **reverse direction**: when reading from the ComboBox `ComboBoxItem.Content` (which currently is `"7-12 años"`), we must not pass that literal straight to IPC; the VM's `SelectedAgeBand` already maps index → wire string correctly. The fix lives in the VM bind (selected `ComboBoxItem.Tag = "7-12"`), not the user-visible copy.
- **Fase**: 3.

### `ShowOverlayCommand(string Reason, string? CtaLabel)` and `HideOverlayCommand`
- **Why**: existing — translated into `ShowOverlay` / `HideOverlay` against the SessionAgent pipe. Used by `DemoStepViewModel.RunDemoAsync`.
- **Fase**: 7 (live path).

### `ResetOnboardingState()` → `OnboardingStateResponse`
- **Why**: test helper + future "start over" affordance. Removes the file via Service and returns fresh initial state.
- **Fase**: 10 (test-only; small implementation costs <20 lines).

### `MsSettingsOpen(string page)` → void
- **Why**: T37 + T30 repair screens open `ms-settings:otherusers`, `ms-settings:appsfeatures`, etc. App.UI cannot launch anything that needs elevation directly when running as a non-elevated user — but the Service can. We delegate the `Process.Start` to the Service (LocalSystem, UAC prompt when needed). ADR-008.
- **Fase**: 6.

### `OnboardingState` payload — concrete JSON shape

After the duplicate-type deletion (ADR-003), the wire JSON for an `OnboardingState` is:

```json
{
  "currentStepIndex": 1,
  "isCompleted": false,
  "isAbandoned": false,
  "steps": [
    { "index": 0, "id": "pairing",  "title": "...", "description": "...", "buttonLabel": "...", "status": "Completed", "isFirstWin": false },
    { "index": 1, "id": "consent",  "title": "...", "description": "...", "buttonLabel": "...", "status": "InProgress", "isFirstWin": false },
    { "index": 2, "id": "account",  "title": "...", "description": "...", "buttonLabel": "...", "status": "Locked",    "isFirstWin": false },
    { "index": 3, "id": "demo",     "title": "...", "description": "...", "buttonLabel": "...", "status": "Locked",    "isFirstWin": true  },
    { "index": 4, "id": "managed",  "title": "...", "description": "...", "buttonLabel": "...", "status": "Locked",    "isFirstWin": false }
  ],
  "events": [
    { "type": "OnboardingStepReached", "stepId": "consent", "occurredAt": "2026-07-21T10:30:00Z" }
  ]
}
```

`OnboardingStep` JSON convention: **camelCase, matching `[JsonPropertyName]`** (Domain uses `JsonSerializerContext` source-gen per `:21` — `ControlParental.Domain.PolicyJsonContext`). For this change we extend the context, see ADR-006.

---

## 3. Persistence policy — Fase 5 architectural flip

| State | Owner | Location | ACL | Write trigger | Read trigger |
|---|---|---|---|---|---|
| `OnboardingState` | **Service** | `%PROGRAMDATA%\ControlParental\onboarding_state.json` | Service-only write (T37 ACL `Deny` for standard users) | IPC: `RecordOnboardingStepCompleted`, `RecordFunnelEvent`, `AdvanceOnboardingStep` (`[NEW]`), `ResetOnboardingState` | IPC: `GetOnboardingState` |
| `ConsentRecord` | **Service** | `ControlParentalDbContext.Consent` (EF Core SQLite at `%PROGRAMDATA%\ControlParental\controlparental.db`) | Service-only write | IPC: `GrantConsent` | IPC: `GetConsentStatus`, server-side reads |
| `FunnelEvent` | **Service** | `ControlParentalDbContext.Outbox` (existing T03 outbox table — `tipo='behavioral_events'`, payload is the T32 catalog row) | Service-only write | IPC: `RecordFunnelEvent` | Server-side, T18 upload |
| App.UI display cache | **App.UI** | in-memory `IReadOnlyDictionary<string, object>` populated at App startup from `GetOnboardingState` IPC call | n/a (RAM) | Pull-on-init | `OnboardingViewModel` |

### Deletion of `src/ControlParental.Domain/OnboardingStateStore.cs`

This is the **App.UI-side local file store** at `%LOCALAPPDATA%\ControlParental\onboarding_state.json` (`:13–14` of the existing file). The new rule is **the Service owns persistence** (`:32`). What gets removed and what replaces it:

- `OnboardingStateStore` class — **delete entirely**.
- `IOnboardingStateStore` interface — **delete**. (Consumers switch to `IOnboardingStateService` + IPC.)
- `OnboardingStateStore` registration at `App.xaml.cs:124` (`services.AddSingleton<IOnboardingStateStore, OnboardingStateStore>();`) — **delete**.
- The `OnboardingViewModel` constructor parameter `stateStore` — **change to `stateService`** (a thin `IOnboardingStateService` proxy that wraps `IUIChannel`).
- `OnboardingViewModel.SaveAsync` calls (e.g. `:241, :260, :412`) — become IPC `RecordOnboardingStepCompleted` and `AdvanceOnboardingStep` (and `GetOnboardingState` at init).
- `OnboardingStateStore.CreateInitialState` (`:67-78`) — moves to the Service-side (`OnboardingStateService.CreateInitialState` already exists at `:106-117`). App.UI's `OnboardingViewModel` initial state now comes from the IPC response.

### Deletion of `src/ControlParental.App.UI/ConsentService.cs`

This is the in-memory no-op (`ConsentService.cs:13–35`) currently DI-registered as `IConsentService` in `App.xaml.cs:127`. It returns `Task.CompletedTask` and reports `IsConsentGranted=true` regardless of the input — **that is the consent theatre** the audit identified.

- `src/ControlParental.App.UI/ConsentService.cs` — **delete**.
- `App.xaml.cs:127` registration — **delete**.
- `ConsentPage.xaml.cs` constructor takes `IConsentService?` (currently `App.Services.GetService<IConsentService>()`) — **change** to `IpcConsentService?` and get it from `App.Services.GetService<IpcConsentService>()`.
- New file `src/ControlParental.App.UI/Interop/IpcConsentService.cs` provides:
  ```csharp
  public sealed class IpcConsentService : IConsentService
  {
      private readonly NamedPipeUIChannel channel;
      public bool IsConsentGranted => /* last cached snapshot */;
      public async Task<ConsentRecord> GetConsentStatusAsync(ct) =>
          /* QueryAsync<GetConsentStatus, ConsentStatusSnapshot> -> map to Domain ConsentRecord */;
      public async Task GrantConsentAsync(string? grantedByDeviceId, ct) =>
          /* SendAsync(new GrantConsent(grantedByDeviceId)) */;
  }
  ```

### Resolution of duplicated types in `src/ControlParental.App.UI/Interop/UIMessages.cs:75-104`

This block re-declares four Domain types:

| `App.UI/Interop/UIMessages.cs` | Canonical version (already exists in `Domain`) |
|---|---|
| `OnboardingState` record (`:75-80`) | `ControlParental.Domain.OnboardingState` (`Domain/OnboardingState.cs:38-43`) |
| `OnboardingStepStatus` enum (`:85-92`) | `ControlParental.Domain.OnboardingStepStatus` (referenced via `OnboardingStep.cs`) |
| `OnboardingStep` record (`:97-104`) | `ControlParental.Domain.OnboardingStep` (`Domain/OnboardingStep.cs`) |
| `FunnelEvent` record (`:109`) | `ControlParental.Domain.FunnelEvent` (`Domain/OnboardingState.cs:28`) |

The App.UI assembly **already references the Domain assembly** (`ControlParental.Domain`), so `OnboardingStateResponse(State)` can carry the canonical `OnboardingState` directly — the duplicates were an artifact of "App.UI cannot reference the Service assembly" (comment at `UIMessages.cs:8-10`), which is **true for the Service-specific handlers but false for shared Domain types**.

**Action (ADR-003):** delete the four duplicates above. The only remaining App.UI-specific record types in `UIMessages.cs` that live there (and must stay there) are the `IUIMessage`-bearing request/response records (`GetEnforcementLevel`, `OnboardingStateResponse`, `RecordOnboardingStepCompleted`, etc.) — those still need a home, and the App.UI side has to declare them because the Service already declares equivalent types in `Domain/IpcMessage.cs`.

**One exception**: `IUIMessage` interface itself is currently declared in both places — `UIMessages.cs:16-19` and `Domain/IpcMessage.cs:132-134`. They are **the same interface** with different `using` paths. The App.UI copy in `UIMessages.cs:16-19` becomes redundant after we delete the duplicates and switch imports to `using ControlParental.Domain;`. Keep the Domain one.

**JSON shape and `JsonSerializerContext`:**
- Add a `JsonSerializerContext` for the IPC payload types (`:21` of the DoD requires AOT-safe serialization). The existing `ControlParental.Domain.PolicyJsonContext` covers `Policy`; we add `ControlParental.Domain.UIMessagesJsonContext` (or extend the existing one) that registers:
  ```csharp
  [JsonSerializable(typeof(OnboardingState))]
  [JsonSerializable(typeof(OnboardingStep))]
  [JsonSerializable(typeof(FunnelEvent))]
  [JsonSerializable(typeof(OnboardingStateResponse))]
  [JsonSerializable(typeof(ConsentStatusSnapshot))]
  [JsonSerializable(typeof(GetConsentStatus))]
  [JsonSerializable(typeof(GrantConsent))]
  [JsonSerializable(typeof(StepCompletedResponse))]
  [JsonSerializable(typeof(PairDevice))]
  [JsonSerializable(typeof(PairDeviceResponse))]
  [JsonSerializable(typeof(ListAccounts))]
  [JsonSerializable(typeof(AccountList))]
  [JsonSerializable(typeof(CreateAccount))]
  [JsonSerializable(typeof(CreateAccountResponse))]
  public partial class UIMessagesJsonContext : JsonSerializerContext { }
  ```
- This keeps the AOT/R2R publishing viable (`:46`) and avoids reflection at runtime.

---

## 4. Progress bar honesty — Fase 2

### `ProgressTotal = 5`
- `OnboardingViewModel.cs:63` already declares `private int progressTotal = 4;` — change to `5` (matches `OnboardingStateStore.CreateInitialState` 5-step list at `:69-76` and the memory #113 anchor: `pairing(0) → consent(1) → account(2) → demo(3) → managed(4)`).
- `MainWindow.xaml:18` is `Maximum="4"` — change to `Maximum="5"`.

### DI wiring for `IEnforcementLevelMonitor` in App.UI
- `App.xaml.cs:126` already registers `IEnforcementLevelMonitor` (currently bound to `EnforcementLevelMonitor` — but the existing implementation is **App.UI-side**, which contradicts `:440`'s "real" requirement).
- **Replacement registration**:
  ```csharp
  services.AddSingleton<IEnforcementLevelMonitor, ServiceEnforcementLevelMonitor>();
  services.AddSingleton<NamedPipeUIChannel>();
  ```
  where `ServiceEnforcementLevelMonitor` is a thin App.UI-side proxy that holds a `NamedPipeUIChannel` and exposes:
  - `CurrentLevel` and `CurrentIssues` as in-memory state, refreshed on a 5-second timer (existing `enforcementPollTimer` already at 5s in `OnboardingViewModel.cs:94`) by calling `GetEnforcementLevel` IPC and converting `Checks` → `EnforcementIssue` records.
  - T12 detection logic is **not** duplicated here — we just translate the IPC response shape.
- `OnboardingViewModel.cs:79-83` constructor already accepts `IEnforcementLevelMonitor?` (`:81`) — it now receives the real implementation, never null in production. The factory (existing `App.xaml.cs:79`) passes `null` only in unit tests; tests inject a fake `IEnforcementLevelMonitor` directly (Fase 10).

### `CalculateRealProgress` without the inflation fallback
- `OnboardingViewModel.cs:501-527` admits in comments "(may be inflated)" — **delete the `enforcementLevelMonitor == null` branch entirely** (ADR-005).
- The new body:
  ```csharp
  private int CalculateRealProgress()
  {
      var issues = this.enforcementLevelMonitor.CurrentIssues;
      var issueTypes = issues.Select(i => i.Type).ToHashSet();
      var level    = this.enforcementLevelMonitor.CurrentLevel;
      var passing  = new[]
      {
          !issueTypes.Contains(EnforcementIssueType.ServiceNotRunning),
          !issueTypes.Contains(EnforcementIssueType.ChildIsAdministrator),
          !issueTypes.Contains(EnforcementIssueType.AgentNotResponding)
              && !issueTypes.Contains(EnforcementIssueType.HookTimeout),
          !issueTypes.Contains(EnforcementIssueType.PreventiveLayerUnavailable)
              && (level is EnforcementLevel.Standard or EnforcementLevel.Managed),
      };
      return passing.Count(p => p);
  }
  ```
- When the IPC call fails (the `catch` block at `OnboardingViewModel.cs:380-383`) the **last known good state is preserved** (no fallback counting steps). The progress label becomes:

### Honest label when Service is unreachable
- New rule (ADR-001 + `:444`): when `currentEnforcementLevel == EnforcementLevel.Unknown` (initial) OR no successful IPC polling yet, the label is **`"Estado desconocido"`** and `ProgressCount = 0` — UI shows the empty bar.
- The fallback "count completed steps" path is **deleted**, not replaced.

---

## 5. Consent flow — Fase 1 + ADR

### Wire-up
- **Adapter**: `src/ControlParental.App.UI/Interop/IpcConsentService.cs` (new file, ADR-002). Constructor takes `NamedPipeUIChannel`.
- **`App.xaml.cs:127` change**: replace `services.AddSingleton<IConsentService, ConsentService>();` with `services.AddSingleton<IConsentService, IpcConsentService>();`.
- **Service handler**: extend `UIMessageHandler.HandleAsync` with two new cases:
  - `GetConsentStatus` → calls `ControlParental.Service.ConsentService.GetConsentStatusAsync` (the SQLite-backed one registered at `Program.cs:327`); returns `ConsentStatusSnapshot`.
  - `GrantConsent` → calls `ControlParental.Service.ConsentService.GrantConsentAsync`; returns the post-write snapshot.
- **`OnboardingViewModel.OnStepCompletedAsync("consent")`** (`OnboardingViewModel.cs:248-288`) already calls `RecordOnboardingStepCompleted` after marking the step complete locally. After Fase 5, we add a guard: **`await this.EnsureConsentPersistedAsync(ct)` is awaited before `await this.uiChannel.SendAsync(new RecordOnboardingStepCompleted(...))`** — if consent persistence failed, the step does NOT advance. Today the step advances even when persistence failed (the `catch {}` in `ConsentPage.xaml.cs:79-82` swallows it).
- **`ConsentPage.xaml.cs:79-82`** — **delete** the empty `catch {}` block. Replace with explicit `LogConsentFailure` showing "No pudimos registrar tu consentimiento. Pedile a tu tutor que revise la instalación." (Strings.resw key `ConsentPersistFailed`). The "Acepto" affordance's button stays disabled during the call; the Onboarding advance is gated on the IPC returning success.

### Persistence happens in the Service SQLite
- Survives App.UI restart, survives full machine reboot (`:32`: Service owns SQLite, SQLite at `%PROGRAMDATA%`).

### ADR-001 — consent-offline UX (fail-closed)

#### Context
T25 §Impl punto 4 (`backlog-control-parental-windows.md:429`): "Bloquear el avance del onboarding hasta consentir." Fase 5 of T26 makes the Service the owner of consent persistence (`backlog-control-parental-windows.md:32`). The backlog does not specify what happens if the Service is unreachable when the adult clicks "Acepto".

#### Decision
**Option A — Fail-closed.** Do not advance. The "Acepto" click returns `IpcUnavailableReason.Timeout`; the page shows copy `ServiceNotAvailable` ("No pudimos conectar con la protección. Pedile a tu tutor que revise la instalación.") and disables the button until the next successful IPC. **Do NOT** persist locally as a fallback.

#### Consequences
- ✅ Aligns with `:444` ("el progreso nunca miente; la elevación se pide al adulto") — an advance we cannot verify in the canonical store is not an advance.
- ✅ Aligns with `:432` ("bloquear el avance del onboarding hasta consentir") — failure of the consent path is the same as absence of consent.
- ✅ No two-store race; no DPAPI outbox to engineer; no merge-on-reconnect logic; the audit didn't find any T18 retry hooks for consent outbox, so option B is a non-trivial new subsystem.
- ❌ The user sees a wall when the service is genuinely down. We mitigate by showing the retry path and the service-status indicator (a child affordance that says "Pedile a tu tutor que revise la instalación" is reasonable — the parent is the only one with admin perms to do anything).
- ❌ A service that crashes mid-onboarding halts consent. Acceptable: `:444` rules — the protection surface must be honest, not optimistic.

#### Alternatives considered
- **Option B (fail-open with DPAPI outbox)** — requires new subsystem (DPAPI scope machine + retry scheduler + merge logic); backlog does not request it; `:429` already says "Bloquear el avance... hasta consentir", and a fail-open path is the opposite semantics.
- **Option C (timeout then advance)** — directly contradicts `:444`; will be flagged by DoD-G (`:46` "errores explícitos... prohibido `catch {}` vacío").

---

## 6. Pairing flow — Fase 3

### `MainWindow` constructor of `StepFrame.Navigate(pageType, parameter: uiChannel)`
- The current page constructors accept the parameterless ctor only. We change every step page to accept a `(Action callback, NamedPipeUIChannel uiChannel)` constructor and call `StepFrame.Navigate(pageType, parameter: uiChannel, info: callback)`.
- **Concrete changes**:
  - `PairingPage` — already has a `(Action, NamedPipeUIChannel)` ctor at `PairingPage.xaml.cs:34-39`. We **update `MainWindow.NavigateToStep`** (currently `:57-72`, ignores the existing 2-arg ctor) to call `StepFrame.Navigate(typeof(PairingPage), parameter: uiChannel, info: this.PairingCompleted)`.
  - `AccountStepPage` — already has `(Action onAccountCompleted)` ctor; add a 2nd `(Action, NamedPipeUIChannel)`.
  - `ConsentPage` — add `(Action, NamedPipeUIChannel)` ctor.
  - `DemoStepPage` — already has `(NamedPipeUIChannel)` ctor at `DemoStepPage.xaml.cs:32-37`; update `MainWindow.NavigateToStep` to pass the channel.
  - `MainWindow.NavigateToStep` (`:57-72`) — extract a single helper that resolves the per-step callback and forwards both to `StepFrame.Navigate(pageType, parameter: uiChannel, info: callback)`.

### Remove `Task.Delay(500)` from `PairingViewModel.PairWithCodeAsync`
- `PairingViewModel.cs:147` — `await Task.Delay(500, ct);` → **delete**.
- The early-out branches already handle the no-channel case at `:121-148`. After Fase 0 + Fase 1 are landed (real Service runs), the channel is non-null in production; we throw a `new InvalidOperationException("IPC channel missing for pairing")` instead of falling through.

### Map HTTP codes to UI messages (cite T24)
T24 already provides a structured result (`PairingResult`) with the status field set to one of `Success | InvalidCode | ExpiredCode | TooManyRequests | AlreadyPaired | Error`. `UIMessageHandler.HandlePairDeviceAsync` (`UIMessageHandler.cs:92-118`) maps them today — we add explicit copies for UI display:

| `PairingResult.Status` | UI copy (from `Strings.resw.PairingError_*`) | Source |
|---|---|---|
| `InvalidCode` | "El código no existe. Verificá con tu tutor." | `PairingService.PairAsync` returns `InvalidCode()` for HTTP 404 (`:107-108`) |
| `ExpiredCode` | "El código venció. Pedile uno nuevo a tu tutor." | `ExpiredCode()` for 410 (`:109-111`) |
| `TooManyRequests` | "Demasiados intentos. Esperá unos minutos." | HTTP 429 (`:113-114`) |
| `NetworkError` | "No pudimos conectar al servidor. Verificá tu internet." | retries exhausted (`:159`) |
| `ServerError` | "Error del servidor. Intentá de nuevo en unos minutos." | HTTP 5xx (`:115-117`) |
| `Error` (catch-all) | "No pudimos emparejar. Pedile ayuda a tu tutor." | (`:122`) |

All copy keys live in `Strings.resw`; XAML binds via `{x:Bind Strings.PairingError_InvalidCode}`. The catalogue is also in T25 (§Impl punto 3 says positive copy with motivo — T24's error strings are the motivo for the pairing step).

### Validation: `AgeBand` ComboBox must have a `null` initial state
- Today `PairingPage.xaml:33-41` has three ComboBoxItems, the first preselected via `SelectedIndex="{Binding SelectedAgeBandIndex, Mode=TwoWay}"` defaulted to `0` in `PairingViewModel.cs:66`. We **drop the preselection**: initial `SelectedAgeBandIndex = -1` (no item selected). The "Emparejar" button is bound to `IsEnabled = !(string.IsNullOrEmpty(Code1) || ... || SelectedAgeBandIndex < 0)` — i.e. disabled until the user picks an age band **and** enters the full 8 characters.
- The wire format `AgeBand` is derived from `SelectedAgeBandIndex` in `PairingViewModel.SelectedAgeBand` (`:72-78`), but with `index = -1` we throw `InvalidAgeBand` rather than defaulting to `"7-12"` silently — silent default is a "may be inflated" type of lie.

### Wire format: `"7-12"` not `"7-12 años"`
- ADR-004. The XAML displays "7-12 años"; the **wire** string IPC sends is `"7-12"`. The inconsistency is at the ComboBox binding: currently the VM reads the `ComboBoxItem.Content` literal which would yield "7-12 años". We instead use a **two-property approach**: `DisplayName` for the user-facing copy, `WireValue` (the wire string) as the `Tag` on each `ComboBoxItem`. `SelectedAgeBandWire` is derived from `SelectedItem.Tag` not `SelectedItem.Content`. (Today the codebase reads from `SelectedAgeBandIndex` because the VM has the switch table — that path already returns the correct wire value at `:72-78`; the issue is only at the **reverse** if a future contributor changes the VM. We make it explicit by binding to `Tag`.)

---

## 7. Demo + funnel — Fase 7

### `DemoStepViewModel.RunDemoAsync`
- `DemoStepViewModel.cs:55-82` currently sends `ShowOverlayCommand("Probemos tu proteccion", null)` to the channel and starts the timer. **Wire the IPC to the real Service so the overlay is shown on the SessionAgent**, not on a WinUI in-page overlay. The Service receives `ShowOverlayCommand` at `UIMessageHandler.cs:62-65` and translates to `ShowOverlay(reason, ctaLabel)` against the agent pipe (`HandleShowOverlayAsync` at `:175-185`). The Agent lives in the kid's session — same as App.UI — so the overlay window itself is the real one (T08).
- After Fase 7 lands, **`MainWindow.xaml:33-40`** ("DemoOverlay" overlay) can stay as the visual fallback when the agent pipe is not available, but its source of truth is the same channel. We remove the dead branch in `OnboardingViewModel.ExecuteDemoStepAsync` (the path was a UI-only fake; see Fase 9).

### `onboarding_first_win` funnel event
- After the countdown completes (existing `OnDemoTimerTick` at `:84-110`), the VM calls `RecordFunnelEvent(FunnelEventType.OnboardingFirstWin)` over IPC. We extend `OnboardingViewModel.RunDemoAsync` (or, more cleanly, `OnStepCompletedAsync("demo")`) to emit this event — the lifecycle is:
  1. `StepCompleted("demo")` → mark step `Completed`, await `RecordOnboardingStepCompleted("demo")` IPC.
  2. Emit `RecordFunnelEvent(OnboardingFirstWin)` IPC → write to outbox.
  3. Advance to next step (`managed`).
- `OnboardingViewModel.ExecuteStepAsync` (`OnboardingViewModel.cs:179-204`) currently handles `case "demo"` by navigating; `DemoStepViewModel.RunDemoAsync` does the work. The funnel event emission lives in `OnStepCompletedAsync("demo")` because that is where we have the IPC channel guaranteed.

### Cleanup
- **`OnboardingViewModel.ExecuteDemoStepAsync`** at `:297-322` — **delete**. It's a dead path that re-sends `ShowOverlayCommand` on top of what `DemoStepViewModel` already does, with a different countdown source. (`OnboardingViewModel.cs:297-322`, listed as Fase 9 dead code.)

---

## 8. Managed step — Fase 4

### `ManagedStepPage.xaml`
- New page. Reads `ManagedStepViewModel.CurrentEnforcementLevel` (from the `IEnforcementLevelMonitor` proxy injected at construction).
- The visible UI is conditional:
  - `EnforcementLevel.Managed`: full success state — shows "Capa preventiva activa", green check.
  - `EnforcementLevel.Standard`: friendly copy "En esta edición de Windows, podés sumar la capa preventiva (WDAC) si querés". Includes a single button "Activar WDAC" (this triggers the WDAC enrollment via T31, which is its own scope).
  - `EnforcementLevel.Unknown` or no IPC: "Estado desconocido" — neutral copy.
  - `EnforcementLevel.Degraded`: copy "Reparar primero" with a CTA that routes to T30 (out of scope here).

### `ManagedStepViewModel` — opt-in
- Constructor accepts `(Action onCompleted, IEnforcementLevelMonitor monitor, NamedPipeUIChannel channel)`.
- `IsOptInAvailable` = `monitor.CurrentLevel is EnforcementLevel.Standard or EnforcementLevel.Managed` (the WS-2021 T31 spirit: opt-in is the only path in STANDARD; bulk T31 is its own change).
- `IsManagedAlready` = `monitor.CurrentLevel == EnforcementLevel.Managed`.
- On "Activar WDAC", send `MsSettingsOpen("appsfeatures")` via IPC (delegated to Service for elevation, ADR-008). Don't run `Process.Start` from App.UI; the kid's session is a standard user.

### Wiring into `OnboardingState` step `managed(4)`
- Already declared at `OnboardingStateService.CreateInitialState` (`:114`); the App.UI `MainWindow.NavigateToStep` switch statement needs the `managed` case (`MainWindow.xaml.cs:59-72` currently returns null for anything other than `pairing|consent|account|demo`). Add `case "managed" => typeof(ManagedStepPage)`.
- `MainWindow.NavigateToStep` calls `StepFrame.Navigate(pageType, parameter: uiChannel, info: callback)`.

### Bulk T31 stays out of scope
- Only the opt-in managed step is here. WDAC/AppLocker/Assigned Access provisioning is its own change (line 497 of the backlog).

---

## 9. Account step — Fase 6

### Remove `Task.Delay(300)` and `"UsuarioTest"` hardcoding
- `AccountStepViewModel.cs:72` `await Task.Delay(300);` — **delete**.
- `AccountStepViewModel.cs:79` `this.Accounts.Add(new AccountItem("UsuarioTest", AccountType.Standard, true));` — **delete**.

### `AccountStepViewModel.LoadAccountsAsync`
- Becomes:
  ```csharp
  public async Task LoadAccountsAsync()
  {
      this.IsLoading = true;
      this.HasError = false;
      try
      {
          var response = await this.channel.QueryAsync<ListAccounts, AccountList>(new ListAccounts(), ct);
          if (response is null) { /* fail-closed show "service unavailable" */ return; }
          this.Accounts.Clear();
          foreach (var a in response.Accounts)
              this.Accounts.Add(new AccountItem(a.Username, MapType(a.Type), a.IsStandard));
      }
      catch (Exception ex) { /* typed error — no plain string catch */ }
      finally { this.IsLoading = false; }
  }
  ```
- Constructor changed to accept `NamedPipeUIChannel`.

### `RequiresElevation` UI handling
- **Today's bug**: `UIMessageHandler.HandleCreateAccountAsync` (`UIMessageHandler.cs:140-152`) mashes the `RequiresElevation` flag into `ErrorMessage` text. We promote it to a first-class field on `CreateAccountResponse(bool Success, bool RequiresElevation, string? ErrorMessage)`.
- **App.UI copy when `RequiresElevation`**: "Tu tutor necesita autorizar la creación de la cuenta. Pedile que abra la app." (Strings.resw key `AccountElevationRequired`). This is **the only** UX — App.UI **does NOT** `Process.Start` an installer, does NOT show a UAC prompt. The Service (LocalSystem) is the only one who can elevate per `:444`. ADR-008.

---

## 10. Copy / i18n — Fase 8

### `Strings.resw` and `Strings.en.resw`
A WinUI 3 project uses `.resw` files inside a `Strings/` folder (default `en-US/` fallback locale). The existing repo has `ControlParental.Domain/Strings.resx` (for the `Strings` C# accessor used by `ConsentStrings`) — that is **legacy .NET ResourceManager** and stays where it is for T25's engine copy. For App.UI XAML, we add `ControlParental.App.UI/Strings/en-US/Strings.resw` and `ControlParental.App.UI/Strings/es/Strings.resw` (default locale when no override, Spanish as per the project's primary copy tone — `:432` mentions "positivos, con motivo").

`Strings.resw` keys (minimum set for the onboarding flow):

```
OnboardingTitle
Onboarding.Pairing.Title
Onboarding.Pairing.Description
Onboarding.Pairing.AgeBandPrompt
Onboarding.Pairing.CodePrompt
Onboarding.Pairing.Action
Onboarding.Pairing.CodeIncomplete
Onboarding.Pairing.CodeInvalidChars
Onboarding.Pairing.AgeBandMissing
Onboarding.Pairing.ServiceUnavailable
Onboarding.Pairing.FirstWin
PairingError_InvalidCode
PairingError_ExpiredCode
PairingError_TooManyRequests
PairingError_NetworkError
PairingError_ServerError
PairingError_Unknown

Onboarding.Consent.Title
Onboarding.Consent.Body
Onboarding.Consent.DetailsHeader
Onboarding.Consent.Details.Tiempo
Onboarding.Consent.Details.Nombre
Onboarding.Consent.Details.Hora
Onboarding.Consent.NoPersonalData
Onboarding.Consent.Accept
Onboarding.Consent.ViewTransparency
Onboarding.Consent.PersistFailed
Onboarding.Consent.ServiceUnavailable

Onboarding.Account.Title
Onboarding.Account.Description
Onboarding.Account.ExistingHeader
Onboarding.Account.TypeDisplay
Onboarding.Account.UseAction
Onboarding.Account.CreateNew
Onboarding.Account.ConvertExisting
Onboarding.Account.ElevationRequired
Onboarding.Account.ServiceUnavailable
Onboarding.Account.UsernameRequired
Onboarding.Account.PasswordRequired

Onboarding.Managed.Title
Onboarding.Managed.DescriptionStandard
Onboarding.Managed.DescriptionManaged
Onboarding.Managed.DescriptionUnknown
Onboarding.Managed.ActionActivate
Onboarding.Managed.ActionRepair

Onboarding.Progress.Unknown
Onboarding.Progress.LabelFormat   (e.g. "Protección {0} de {1}")

Common.AgeBand_7_12
Common.AgeBand_13_16
Common.AgeBand_17_18
Common.ServiceUnavailableShort
```

`Strings.en.resw` (already exists at `src/ControlParental.Domain/Strings.en.resx`): we mirror keys with neutral English copy. **No other locales this change** (Rioplatense Spanish is the only committed dialect; English is fallback). Future locales are welcome and fit cleanly through the resource manager pipeline — but not in this change.

### Fix missing accents (DoD-G + backlog honesty)
- `OnboardingStateService.CreateInitialState` (`:112-114`) has `"Probemos tu proteccion"`, `"Veamos como funciona la proteccion"` — **fix** to `"Probemos tu protección"`, `"Veamos cómo funciona la protección"`.
- `MainWindow.xaml:38` `"Tu proteccion esta activa!"` — **fix** accent; **moves to Strings.resw** in Fase 8.
- `OnboardingService` titles also missing in `Domain/OnboardingStateService.cs` copies — fix.
- `ServiceInstallStepViewModel.cs:80,90,100,103` `"El servicio no esta instalado"` etc — **deleted** with the page per Fase 9, not migrated.

### Locale chain (Fase 8 only)
- `Strings.resw` (default = Rioplatense/neutral Spanish) → `Strings.en.resw` (neutral English). No other locales; new locales are easy to add but out of scope for T26.

---

## 11. Cleanup — Fase 9

### Files to delete

| Path | Why |
|---|---|
| `src/ControlParental.App.UI/ServiceInstallStepPage.xaml` | Service is delivered via MSIX (memory #113); service install step is not part of T26 onboarding per `pairing → consent → account → demo → managed` |
| `src/ControlParental.App.UI/ServiceInstallStepPage.xaml.cs` | Same |
| `src/ControlParental.App.UI/ServiceInstallStepViewModel.cs` | Same — also no callers after Fase 4 ships |
| `src/ControlParental.App.UI/ConsentDialog.cs` | Console-`Console.WriteLine` dialog never invoked from WinUI. `Backlog:425` says "divulgación prominente in-app" — that is the WinUI `ConsentPage`. |
| `src/ControlParental.Domain/OnboardingStateStore.cs` | Replaced by IPC-owned Service state (ADR-002) |
| `src/ControlParental.Domain/IOnboardingStateStore.cs` | Same |
| `src/ControlParental.App.UI/ConsentService.cs` | In-memory no-op, the consent-theatre source (ADR-002) |
| `src/ControlParental.App.UI/Interop/UIMessages.cs:75-104` | Duplicated Domain types (ADR-003) |
| `src/ControlParental.App.UI/OnboardingViewModel.ExecuteDemoStepAsync` (`:297-322`) | Dead — `DemoStepViewModel.RunDemoAsync` is the live code path (Fase 7) |
| `src/ControlParental.App.UI/OnboardingViewModel.OnDemoTimerTick` (`:324-346`) | Dead — same reason, but **only after** we ship `DemoStepViewModel` with the IPC-driven timer (Fase 7). Keep them until Fase 7 lands to avoid a regression window. |
| `src/ControlParental.App.UI/OnboardingViewModel.NavigateToStepByIndex` (`:472-484`) | Dead — not invoked (`MainWindow.NavigateBack` at `:96-103` calls it but the only caller `MainWindow.xaml.cs` wires the button via `BackButton.Click` which calls `viewModel.GoBackCommand` instead). |
| `src/ControlParental.App.UI/OnboardingViewModel.OpenMsSettings` (`:536-546`) | Dead — never invoked; replaced by `MsSettingsOpen` IPC (Fase 6, ADR-008). |

### Strings to be removed from `strings.resx` / `strings.en.resx`

- The `ConsentStrings.cs` / `ConsentStrings.ConsentStrings.DisclosureBody` etc. still used by the deprecated `ConsentDialog.cs` — migrate to Strings.resw + delete the file. The `ConsentStrings` literal class becomes redundant.
- Touching engine copy (`ReasonBlocked`, etc.) stays in `Domain/Strings.resx` — engine code uses `ConsentStrings`/the C# class. T25 §Impl 3 says engine copy comes from T25; that's the .resx we have. No change needed.

### Knock-on deprecation
- The `OnboardingState` field `Title`/`Description`/`ButtonLabel` on each step are currently Spanish strings in code (`OnboardingStateService.cs:108-115`). After Fase 8, those literals are replaced by `Strings.resw` keys (`Onboarding.<step>.Title` etc.). The Domain record stays the same shape (still carries localized strings), but the source of the strings is the resource manager.

---

## 12. Test strategy — Fase 10

### Test stack
- xUnit 2.9.2 (`config.yaml`/T36 §Impl punto 1)
- NSubstitute 5.1.0 (we use NSubstitute over Moq for cleaner setup syntax — both are available)
- FluentAssertions 6.12.2
- RichardSzalay.MockHttp 1.1.2 (`PairingService` tests)
- EF Core InMemory 9.0.0 (ConsentService tests with SQLite-via-InMemory provider)
- coverlet.collector 6.0.2 (coverage reports)

### `MockNamedPipeUIChannel` (new test double)
- The `MockNamedPipeUIChannel` is a hand-rolled `IUIChannel`-shaped test double that lives at `tests/ControlParental.App.UI.Tests/Interop/MockNamedPipeUIChannel.cs` (new project file as well, or appended to the existing one).
- Shape:
  ```csharp
  public sealed class MockNamedPipeUIChannel : IUIChannel
  {
      private readonly Dictionary<string, Delegate> handlers = new();
      public void On<TRequest, TResponse>(Func<TRequest, Task<TResponse>> handler);
      public void OnFireAndForget<TRequest>(Func<TRequest, Task> handler);
      public Task SendAsync<T>(T message, CancellationToken ct);
      public Task<TResponse?> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken ct) where TQuery : IUIMessage where TResponse : class, IUIMessage;
  }
  ```
- Per-fase test cases register handlers for each message type the test cares about. This decouples App.UI tests from a real Service or pipe — today `NamedPipeUIChannel` directly opens a pipe; we'd have to spin up a fake server. The mock centralizes that.

### Testable stores (fix the broken pattern)
- **`OnboardingStateStore`** — **deleted**; its replacement `OnboardingStateService` already accepts `dataFolderPath` via ctor (`OnboardingStateService.cs:26`). Tests pass `Path.Combine(Path.GetTempPath(), "cp-onboarding-" + Guid.NewGuid())` — use a `TempFolderAttribute` xUnit extension (T36 idiom).
- **New `OnboardingStateService` tests**: persist → load round-trip; persist → kill → reload returns persisted state; atomic write helper resists partial writes.

### Atomic write helper
- `OnboardingStateService.SaveStateAsync` currently does `File.WriteAllTextAsync` (`:98`). Replace with:
  ```csharp
  var tmpPath = this.stateFilePath + ".tmp";
  await File.WriteAllTextAsync(tmpPath, json, ct);
  File.Move(tmpPath, this.stateFilePath, overwrite: true); // atomic on NTFS
  ```
- Tests cover: kill between `WriteAllTextAsync` and `Move` → original file remains intact; subsequent successful call replaces it cleanly.

### Per-fase test cases

#### Fase 0 (build blocker)
- `dotnet build src/ControlParental.Service/ControlParental.Service.csproj` exits with code 0 — guards regression.

#### Fase 1 (real consent)
- `IpcConsentService.GrantConsentAsync` succeeds when mock Service returns snapshot.
- `IpcConsentService.GrantConsentAsync` propagates null/mock-unavailable to caller (no silent swallow). Asserted by xUnit: `await Assert.ThrowsAsync<ServiceUnavailableException>(...)` if mock returns null; or extension method.
- `OnboardingViewModel.OnStepCompletedAsync("consent")` does **not** advance if persistence failed.
- `OnboardingStateService` happy-path + transaction rollback scenarios.
- `ConsentPage.OnAcceptClick` happy path: button stays disabled until success, page raises `OnStepCompletedAsync("consent")` after.
- `ConsentPage.OnAcceptClick` Service-down path: button stays disabled after timeout, page does NOT raise `OnStepCompletedAsync`.

#### Fase 2 (honest progress)
- `OnboardingViewModel.CalculateRealProgress` returns `4` when 4 checks pass (table-driven test, all combinations).
- `OnboardingViewModel.ProgressLabel` is `"Estado desconocido"` when `CurrentEnforcementLevel == Unknown`.
- `OnboardingViewModel.ProgressLabel` does **not** equal `"Protección 4 de 4"` when only 3 checks pass (regression for "may be inflated" branch).
- `MainWindow.xaml` `ProgressBar.Maximum == 5`.

#### Fase 3 (real pairing)
- `PairingViewModel.PairWithCodeAsync` with mock channel returning `PairDeviceResponse(InvalidCode, ...)`: VM sets `HasError = true`, `ErrorMessage = "El código no existe..."`.
- Same for `ExpiredCode`, `TooManyRequests`, `NetworkError`, `ServerError`, `Unknown`.
- `PairWithCodeAsync` with `uiChannel = null`: throws `InvalidOperationException` (NOT a silent delay).
- `PairWithCodeAsync` with `SelectedAgeBandIndex == -1` and code complete: VM surfaces `"AgeBandMissing"` error and does NOT call IPC.
- `AgeBandExtensions.TryParse("7-12")` round-trips to `AgeBand.Child` (already at T24, regression).

#### Fase 4 (managed step)
- `ManagedStepViewModel.IsOptInAvailable` true when `monitor.CurrentLevel == Standard`, false when `Degraded`.
- `ManagedStepViewModel` "Activar WDAC" → emits `MsSettingsOpen("appsfeatures")` via channel.
- `ManagedStepViewModel` does **not** call `Process.Start` directly — asserted by `DoesNotRaise(nameof(Process.Start))`.

#### Fase 5 (architectural flip)
- **Most critical regression**: kill the `OnboardingStateStore` and start a fresh Service-owned state path.
  - Test 1: set Service-side state to `pairing=Completed, consent=InProgress`, restart App.UI, assert initial state is restored without App.UI touching the file system.
  - Test 2: write 1000 funnel events on the Service, kill App.UI, restart App.UI, assert all 1000 events round-trip.
- **Atomic write race**: spawn a writer that signals when `SaveAsync` has the lock, kill the writer mid-call, assert next load returns the previous state.

#### Fase 6 (account step)
- `AccountStepViewModel.LoadAccountsAsync` lists real accounts from mock IPC.
- `AccountStepViewModel.CreateAccountAsync` happy path: mock returns success, list updates.
- `AccountStepViewModel.CreateAccountAsync` `RequiresElevation == true`: error message is the elevation-specific copy; VM does NOT call `Process.Start` (asserted by fake `IProcessStarter` substitute).

#### Fase 7 (demo + funnel)
- `OnboardingStep` advancement to `managed` after demo completes.
- `RecordFunnelEvent(OnboardingFirstWin)` is sent via IPC when demo completes.
- `RecordFunnelEvent(OnboardingCompleted)` is sent on final step.
- `RecordFunnelEvent(OnboardingAbandoned)` is sent on `AbandonAsync`.

#### Fase 8 (i18n)
- Snapshot test the resource manager returns expected strings for `es` and default locales.

#### Fase 9 (cleanup)
- `dotnet build` clean; no dead-code lints (`AD0001`/`SA0001` already enforced).
- `grep` test on the codebase asserting `ServiceInstallStepPage` has no remaining references.

#### Fase 10 (kill-during-advance)
- Specifically for `:444` "reanudable": kill the App.UI between `RecordFunnelEvent` send and `OnStepCompletedAsync` IPC send, restart, assert state is consistent (the `InProgress` state is recovered, no double-advance).

### Coverage goal
- Not a percentage. The audit identified **five** critical paths and each gets at least one dedicated regression test. New regressions in those five paths fail the test suite even at 60% coverage overall.

---

## 13. Risks per Fase

| Fase | Risk | Mitigation | Test |
|---|---|---|---|
| 0 | Duplicate registration not actually a duplicate (different code path) | Confirm both blocks are identical before deleting one | `dotnet build` + service start smoke |
| 1 | Consent blocking advance breaks unit-test onboarding flow that previously advanced | Test fixture exposes a way to bypass for unit tests while production gates | Fase 1 tests + OnboardingViewModel test for the gate |
| 2 | Removing the inflation branch regresses an edge case where monitor is real but stale | `CalculateRealProgress` is a pure function of monitor snapshot; if it's stale it's still honest (doesn't claim more than the latest data shows) | Fase 2 tests + snapshot test |
| 3 | Pairing 5xx retries look like success to the UI | `PairDeviceResponse.ErrorMessage` carries typed status; UI copy keys resolve to it | T24's structured `PairingResult` unchanged, asserted in 3 unit tests |
| 4 | `ManagedStepPage.xaml` doesn't ship — leaves nav with null page | Schedulepage list in `MainWindow.NavigateToStep` MUST be extended in the same PR; `dotnet build` fails compilation if not | Build blocker |
| 5 | Race between App.UI reading state and a parallel Service write | `OnboardingStateService.fileLock` (existing `SemaphoreSlim`) ensures ordered reads; tests force concurrent access | Atomic write + concurrency test |
| 6 | UAC prompt from LocalSystem is unexpected to a kid user | Copy says "Pedile a tu tutor que abra la app" (elevation explanation, not silent) | UI assertion: copy contains "tutor" |
| 7 | Funnel events lost on shutdown | Service outbox table persists; T18 upload picks them up asynchronously | Offline upload tested in T18 already; we add a unit test that RecordFunnelEvent returns success after offline state |
| 8 | Accents/encoding break when migrating to .resw | Use UTF-8 resource files; verify by snapshot test | Fase 8 tests |
| 9 | Deleting dead code might break a hidden caller | Compile-time check (`dotnet build`) catches references; integration test suite covers the live path | Build + test runs |
| 10 | Test isolation fails on shared `%LOCALAPPDATA%` paths | Constructor-injected paths + temp folders | Temp-folder test isolation + ADR-009 atomic write |

---

## 14. PR slicing detail

The 9-PR sequence from the proposal, with exact file paths and rough diff budgets. **Each PR has its own worktree + branch; chained PRs land on the previous PR's branch** so any review can see a focused diff.

### PR #1 — Fase 0 — build blocker (XS)
**Files:**
- `src/ControlParental.Service/Program.cs` — delete lines 354–356 (the duplicate `NamedPipeUIServer` / `NamedPipeUIServerHostedAdapter` registration).

**Diff size**: 3 lines deleted. Total diff: ~3 LOC plus the implicit whitespace.
**Tests**: none (this is a build blocker; a green `dotnet build` is the verification).
**Dependencies**: none; this is the root of the chain.

### PR #2 — Fase 1 — real consent + tests (M)
**Files:**
- `src/ControlParental.Domain/IpcMessage.cs` — add `GetConsentStatus`, `ConsentStatusSnapshot`, `GrantConsent` records.
- `src/ControlParental.Service/UIMessageHandler.cs` — add two cases: `GetConsentStatus` → `ConsentStatusSnapshot`, `GrantConsent` → calls `ConsentService.GrantAsync`.
- `src/ControlParental.Service/Program.cs` — `NamedPipeUIServer` `DeserializeMessage` switch (`:274-298`) — add three new `nameof` arms for the new messages.
- `src/ControlParental.App.UI/Interop/IpcConsentService.cs` — **new file** (~50 LOC).
- `src/ControlParental.App.UI/App.xaml.cs` — change `:127` registration from `ConsentService` to `IpcConsentService`.
- `src/ControlParental.App.UI/ConsentPage.xaml.cs` — delete `catch {}` (`:79-82`); explicit error path.
- `src/ControlParental.App.UI/OnboardingViewModel.cs` — add `EnsureConsentPersistedAsync`; gate `OnStepCompletedAsync("consent")` on it.
- `src/ControlParental.App.UI/ConsentService.cs` — **delete** (~35 LOC removed).
- `src/ControlParental.App.UI/MainWindow.xaml.cs` — `MainWindow.NavigateToStep` change so `StepFrame.Navigate(typeof(ConsentPage), parameter: uiChannel, info: callback)`.

**Diff size**: ~250 LOC additions, ~35 LOC deletions.
**Tests** (new test file `tests/ControlParental.App.UI.Tests/ConsentFlowTests.cs`):
- `IpcConsentService_GrantsViaIPC_Success`
- `IpcConsentService_GrantsViaIPC_PropagatesFailure_NoSilentSwallow`
- `OnboardingViewModel_OnStepCompleted_Consent_BlocksAdvance_OnFailure`
- `OnboardingViewModel_OnStepCompleted_Consent_Advances_OnSuccess`
- `ConsentPage_OnAcceptClick_ServiceUnavailable_StaysDisabled`
**Dependencies**: PR #1 merged.

### PR #3 — Fase 2 — honest progress + tests (M)
**Files:**
- `src/ControlParental.App.UI/OnboardingViewModel.cs` — `ProgressTotal = 5` (`:63`), delete inflation branch (`:501-507`), add `"Estado desconocido"` label path.
- `src/ControlParental.App.UI/MainWindow.xaml` — `Maximum="5"` (`:18`).
- `src/ControlParental.App.UI/Interop/ServiceEnforcementLevelMonitor.cs` — **new file** (~70 LOC) — proxy for `IEnforcementLevelMonitor` reading IPC `GetEnforcementLevel` responses.
- `src/ControlParental.App.UI/App.xaml.cs` — replace `EnforcementLevelMonitor` registration (`:126`) with `ServiceEnforcementLevelMonitor`.
- (delete) `src/ControlParental.App.UI/EnforcementLevelMonitor.cs` — only if App.UI copy is fully redundant after the proxy ships. The audit says it's currently a "dev-time" placeholder; verify in PR review.

**Diff size**: ~180 LOC additions, ~70 LOC deletions.
**Tests** (new file `tests/ControlParental.App.UI.Tests/ProgressBarHonestyTests.cs`):
- `CalculateRealProgress_AllChecksPassing_Returns4`
- `CalculateRealProgress_OneCheckFailing_Returns3`
- `ProgressLabel_WhenMonitorUnknown_ReturnsEstadoDesconocido`
- `ProgressLabel_WhenMonitorReal_ReturnsHonestLabel`
- `Maximum_OnProgressBar_Is5`
**Dependencies**: PR #2 merged.

### PR #4 — Fase 5 — architectural flip (L)
**Files:**
- `src/ControlParental.Domain/IOnboardingStateService.cs` — extend interface with `ResetAsync(string stepId)`, `AdvanceAsync(int newIndex)`, `RecordFunnelEventAsync(FunnelEventType type, string stepId)`.
- `src/ControlParental.Service/OnboardingStateService.cs` — implement the new interface methods, atomic write helper, `fileLock` already in place.
- `src/ControlParental.Service/UIMessageHandler.cs` — extend `HandleAsync` to handle `RecordOnboardingStepCompleted` (already there) + new `AdvanceOnboardingStep`, `ResetOnboardingState`.
- `src/ControlParental.Service/Interop/NamedPipeUIServer.cs` — add `nameof` arms for new messages.
- `src/ControlParental.Domain/IpcMessage.cs` — add `AdvanceOnboardingStep`, `ResetOnboardingState` records.
- `src/ControlParental.App.UI/Interop/UIMessages.cs` — delete the duplicate Domain types at `:75-104`. Keep `IUIMessage` interface declaration here or import from Domain.
- `src/ControlParental.App.UI/OnboardingViewModel.cs` — replace `IOnboardingStateStore` with `IOnboardingStateService` (and `stateStore` field); `SaveAsync` → IPC; `InitializeAsync` → `GetStateAsync` IPC.
- `src/ControlParental.App.UI/App.xaml.cs` — change DI registration (`:124`).
- `src/ControlParental.Domain/OnboardingStateStore.cs` — **delete**.
- `src/ControlParental.Domain/IOnboardingStateStore.cs` — **delete**.

**Diff size**: ~400 LOC additions, ~120 LOC deletions (largest PR; if diff budget exceeds 400 lines, **split** into PR #4a "Service-side persistence + IPC messages" and PR #4b "App.UI deletes local store and routes through IPC" — bot are mergeable only after #3 lands).
**Tests** (new file `tests/ControlParental.Service.Tests/OnboardingStateServiceTests.cs` + `tests/ControlParental.App.UI.Tests/OnboardingViewModelStateRouteTests.cs`):
- `OnboardingStateService_AtomicWrite_PartialKillLeavesPriorStateIntact`
- `OnboardingStateService_RecordsFunnelEvent_PersistsToOutbox`
- `OnboardingViewModel_LoadsStateFromIpc_NotLocalFile`
- `OnboardingViewModel_AdvancesViaIpc_NotLocalFile`
- `OnboardingViewModel_NoLocalFileWrites_AfterFlip`
**Dependencies**: PR #3 merged.

### PR #5 — Fase 3 — real pairing + tests (L)
**Files:**
- `src/ControlParental.App.UI/PairingViewModel.cs` — remove `Task.Delay` (`:147`); null channel throws `InvalidOperationException`; `SelectedAgeBandIndex = -1` initial; explicit error per status.
- `src/ControlParental.App.UI/PairingPage.xaml` — `ComboBoxItem` `Tag = "7-12" | "13-16" | "17-18"` (preserves display copy in `Content`); "Emparejar" button enabled-binding updates for `SelectedAgeBandIndex >= 0`.
- `src/ControlParental.App.UI/PairingPage.xaml.cs` — wire `OnStepCompletedAsync` from `MainWindow`.
- `src/ControlParental.App.UI/MainWindow.xaml.cs` — `NavigateToStep` routes `pairing` to `StepFrame.Navigate(typeof(PairingPage), parameter: uiChannel, info: PairingCompleted)`.
- `src/ControlParental.Service/UIMessageHandler.cs` — extend `HandlePairDeviceAsync` (already there, no logic change but ensure the typed result is propagated).

**Diff size**: ~150 LOC additions, ~25 LOC deletions.
**Tests** (new file `tests/ControlParental.App.UI.Tests/PairingFlowTests.cs`):
- `PairingViewModel_InvalidCode_Status_String`
- `PairingViewModel_ExpiredCode_Status_String`
- `PairingViewModel_TooManyRequests_Status_String`
- `PairingViewModel_NetworkError_Status_String`
- `PairingViewModel_ServerError_Status_String`
- `PairingViewModel_NoChannel_Throws (no silent delay)`
- `PairingViewModel_AgeBandMissing_BlocksPair_NoIpcCall`
- `AgeBandExtensions_TryParse_RoundTrips`
**Dependencies**: PR #4 merged.

### PR #6 — Fase 4 — managed step (S)
**Files:**
- `src/ControlParental.App.UI/ManagedStepPage.xaml` — **new file**.
- `src/ControlParental.App.UI/ManagedStepPage.xaml.cs` — **new file** (~50 LOC).
- `src/ControlParental.App.UI/ManagedStepViewModel.cs` — **new file** (~80 LOC).
- `src/ControlParental.App.UI/MainWindow.xaml.cs` — extend `NavigateToStep` switch with `case "managed"`.
- `src/ControlParental.App.UI/OnboardingViewModel.cs` — `OnStepCompletedAsync("managed")` path emits `OnboardingCompleted` funnel and stops the demo timer.

**Diff size**: ~200 LOC additions.
**Tests** (new file `tests/ControlParental.App.UI.Tests/ManagedStepTests.cs`):
- `ManagedStepViewModel_IsOptInAvailable_TrueForStandard`
- `ManagedStepViewModel_IsOptInAvailable_FalseForDegraded`
- `ManagedStepViewModel_NoProcessStartFromUi`
**Dependencies**: PR #4 merged.

### PR #7 — Fases 6 + 7 — account + demo funnel (M)
**Files:**
- `src/ControlParental.App.UI/AccountStepViewModel.cs` — remove `Task.Delay` (`:72`), `("UsuarioTest", ...)` (`:79`); call `ListAccounts` IPC; `RequiresElevation` UI copy; constructor accepts channel.
- `src/ControlParental.App.UI/AccountStepPage.xaml.cs` — add `(Action, NamedPipeUIChannel)` ctor.
- `src/ControlParental.App.UI/DemoStepViewModel.cs` — extend with funnel event emission; ensure `ShowOverlay` IPC is awaited.
- `src/ControlParental.App.UI/DemoStepPage.xaml.cs` — wire channel.
- `src/ControlParental.App.UI/MainWindow.xaml.cs` — `NavigateToStep` for `account` and `demo`.
- `src/ControlParental.Domain/IpcMessage.cs` — add `ListAccounts`, `AccountList`, `MsSettingsOpen` records.
- `src/ControlParental.Service/UIMessageHandler.cs` — handle `ListAccounts`, `MsSettingsOpen`; extend `CreateAccountResponse` to a typed field (migrate from string concat at `:147-151`).
- `src/ControlParental.Service/Program.cs` — register new NamedPipeUIServer `DeserializeMessage` arms.
- `src/ControlParental.App.UI/OnboardingViewModel.cs` — `ExecuteDemoStepAsync` (`:297-322`) and `OnDemoTimerTick` (`:324-346`) **delete** — move to `DemoStepViewModel`.

**Diff size**: ~250 LOC additions, ~75 LOC deletions.
**Tests** (new files `tests/ControlParental.App.UI.Tests/AccountStepTests.cs` and `tests/ControlParental.App.UI.Tests/DemoStepTests.cs`):
- `AccountStepViewModel_LoadAccounts_ListsFromIpc`
- `AccountStepViewModel_CreateAccount_ElevationRequired_UsesElevationCopy`
- `AccountStepViewModel_CreateAccount_NoProcessStart`
- `DemoStepViewModel_OnComplete_RecordsFirstWinFunnelEvent`
- `OnboardingViewModel_OnManagedStep_RecordsCompletedFunnelEvent`
**Dependencies**: PR #5 merged.

### PR #8 — Fases 8 + 9 — copy/i18n + cleanup (M)
**Files:**
- `src/ControlParental.App.UI/Strings/en-US/Strings.resw` — **new file**.
- `src/ControlParental.App.UI/Strings/es/Strings.resw` — **new file**.
- All `*.xaml` files — replace hardcoded `Text="..."` with `{x:Bind Strings.Key}`.
- `src/ControlParental.App.UI/OnboardingStateService.cs` — accent fixes in `CreateInitialState` (`:112-114`).
- `src/ControlParental.App.UI/ConsentDialog.cs` — **delete**.
- `src/ControlParental.Domain/ConsentStrings.cs` — replace with Delegates to `Strings.resw`; or **delete** since `Domain/Strings.resx` is the source.
- `src/ControlParental.App.UI/ServiceInstallStepPage.xaml(.cs)` — **delete**.
- `src/ControlParental.App.UI/ServiceInstallStepViewModel.cs` — **delete**.
- `src/ControlParental.App.UI/MainWindow.xaml:38` — remove `"Tu proteccion esta activa!"` text (the real DEMO overlay is on the SessionAgent; this fallback goes away).

**Diff size**: ~150 LOC additions, ~350 LOC deletions.
**Tests**: snapshot tests for resource keys; `dotnet build` clean.
**Dependencies**: PR #7 merged.

### PR #9 — Fase 10 — tests for atomic write, kill-during-advance (S)
**Files:**
- `tests/ControlParental.Service.Tests/OnboardingStateServiceTests.cs` — `RecordFunnelEvent_PersistsToOutbox`, `KillBetweenWriteAndMove_PreservesOldState`, `ConcurrentReadsAndWrites_NoCorruption`.
- `tests/ControlParental.App.UI.Tests/Interop/MockNamedPipeUIChannel.cs` — **new file**.
- `tests/ControlParental.App.UI.Tests/OnboardingViewModelStateRouteTests.cs` — `KillBetweenIpcAndAdvance_ResumeIsConsistent`.

**Diff size**: ~250 LOC additions.
**Dependencies**: PRs #1–#8 all merged.

---

## Cross-cutting ADRs (consolidated index)

For brevity, the consent-offline decision was the only one with full prose. The other decisions are captured in this design and summarised in the executive table — they are derived from the backlog (the user's source of truth) and do not warrant additional ADR prose. ADRs in this document:

- **ADR-001 — Consent-offline UX: fail-closed.** See §5 above.
- **ADR-002 — `OnboardingState` ownership flips to Service.** See §3 and the file-deletion list §11.
- **ADR-003 — Delete duplicate IPC types in App.UI.** See §3 last subsection.
- **ADR-004 — Age band wire format `7-12` (no `años` suffix).** See §6 last subsection.
- **ADR-005 — App.UI `IEnforcementLevelMonitor` is a proxy, not a duplicate engine.** See §4.
- **ADR-006 — JSON: `JsonSerializerContext` source-gen for IPC payloads.** See §3 last subsection.
- **ADR-007 — Copy locale chain: Spanish default → English, no others.** See §10.
- **ADR-008 — Elevation UX: Service prompts UAC; App.UI never `Process.Start`.** See §9.
- **ADR-009 — Testable state store + atomic write.** See §12.

---

## Open resolved items

There is **one** open question in the proposal; this design closes it via ADR-001.

The other open architectural questions are resolved by anchor in the backlog:

- **Q**: Where does the progress bar get its real value? **A**: `IEnforcementLevelMonitor` proxy in App.UI reading `GetEnforcementLevel` IPC (§4, ADR-005).
- **Q**: Where does consent persist? **A**: Service SQLite via `ControlParentalDbContext.Consent` (§3, ADR-002).
- **Q**: How is onboarding resumable across reboot? **A**: Service-owned `%PROGRAMDATA%\ControlParental\onboarding_state.json` atomic write (§12, ADR-009).
- **Q**: What handles elevation in the account step? **A**: Service (LocalSystem) hosts the UAC; App.UI displays copy only (§9, ADR-008).

**Status for orchestrator:** design complete. Ready for `sdd-spec` to refresh `spec.md` and `sdd-tasks` to regenerate `tasks.md` from the 9-PR slice in §14.
