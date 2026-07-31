# Change: t26-onboarding-ipc-ageband-consent

## Why

T26 ("Onboarding por valor + setup con progreso") was nominally delivered, but a
read-only audit of the existing code shows the implementation is non-functional
on its critical path and structurally violates the project's architectural
invariants. The Service does not start, the consent step is theatre, the
progress bar lies, the "managed" step is a dead-end, and pairing is a stub.
Without these fixes the onboarding cannot honestly fulfil its mandate: "primera
victoria antes de pasos caros · progreso real · reanudable · eventos de embudo ·
DoD-G" (`backlog-control-parental-windows.md:446`).

The audit findings motivating this change are:

1. **Build blocker** — `src/ControlParental.Service/Program.cs:347-356`
   registers `NamedPipeUIServer` and `NamedPipeUIServerHostedAdapter` twice.
   The Service cannot start, which means every other T26/T24 integration is
   theoretical.
2. **Consent is theatre** — `src/ControlParental.App.UI/App.xaml.cs:127`
   registers the in-memory `src/ControlParental.App.UI/ConsentService.cs`
   (returns `Task.CompletedTask`) instead of the real persistent
   `src/ControlParental.Service/ConsentService.cs` (SQLite via
   `ControlParentalDbContext`). `src/ControlParental.App.UI/ConsentPage.xaml.cs:79-82`
   swallows persistence errors with `catch {}` and proceeds anyway. T25
   acceptance criterion "Bloquear el avance del onboarding hasta consentir"
   (`backlog-control-parental-windows.md:432`) is violated.
3. **Progress bar lies** — `OnboardingViewModel.ProgressTotal = 4` and
   `MainWindow.xaml Maximum="4"`, but `OnboardingStateStore.CreateInitialState`
   defines 5 steps. `CalculateRealProgress` admits (in code comments) that it
   may inflate the count when `enforcementLevelMonitor == null`. T26 §Impl
   punto 2 ("nunca inflado", `backlog-control-parental-windows.md:440`) is
   violated.
4. **Managed step is a dead-end** — declared in `OnboardingState`, handled by
   `OnboardingViewModel.ExecuteStepAsync`, but
   `MainWindow.NavigateToStep("managed")` returns null. No
   `ManagedStepPage.xaml` exists in the project.
5. **Pairing is `Task.Delay(500)`** — `PairingPage.xaml.cs:22-27` constructs the
   VM with `uiChannel = null`; `MainWindow.NavigateToStep` always invokes the
   parameterless constructor; `PairingViewModel.PairWithCodeAsync` falls to
   the "simulate success for development" branch. The real `PairingService`
   (T24) is never invoked from the live code path. The whole T24 ↔ T26
   integration is theoretical.

The structural defect underneath all five is the same: the App.UI process owns
`OnboardingState` and writes it to a local file under `%LOCALAPPDATA%`, while
the architectural rule (`backlog-control-parental-windows.md:32`) is that the
Service owns SQLite in `%PROGRAMDATA%` with service ACL. This change flips
ownership so the Service becomes the single source of truth, the App.UI
reads/writes through IPC, and the five symptoms disappear.

User impact today: an adult who installs the app cannot complete pairing
against the real backend, cannot grant consent that persists, cannot trust the
progress bar, and cannot reach a "first win" — the literal objective of T26.

## What changes

- The Service starts cleanly (no duplicate registrations).
- The consent step calls the real persistent `ConsentService` via IPC and
  blocks advance until grant is persisted in SQLite.
- The progress bar reports the real state of T12 from the Service: standard
  account ✓, service active ✓, watcher emitting ✓, preventive layer ✓/✗.
  No inflation. `ProgressTotal = 5`.
- Pairing calls the real `PairingService` via IPC; HTTP 404/410/429 surface
  to the UI; no `Task.Delay` simulation.
- The `managed` step has a real page (opt-in via T31's essence) with a single
  source for opt-in.
- The Service owns `OnboardingState`; App.UI never writes the file directly.
  Two stores (`%LOCALAPPDATA%` vs `%PROGRAMDATA%`) collapse into one. The
  resumability invariant ("estado persistido en el almacén del servicio",
  `backlog-control-parental-windows.md:442`) is enforced.
- The `onboarding_first_win` funnel event is emitted from the live code path
  (T32 catalog, `backlog-control-parental-windows.md:517`).
- All user-facing copy moves from XAML hardcoded strings to `Strings.resw`;
  accents fixed; localization-ready.
- Duplicated types between Domain and `App.UI/Interop/UIMessages.cs:75-104`
  collapse; dead code (e.g., `ServiceInstallStepPage.*`) removed.
- Test coverage added for the critical paths the audit identified as untested
  (consent-blocking-advance, honest progress bar, pairing 404/410/429 paths).

## Backlog citations

- `backlog-control-parental-windows.md:32` — Regla "Arquitectura invariante":
  Servicio (`LocalSystem`, Sesión 0, auto-arranque) "**dueño de SQLite, motor,
  sync, heartbeat y enforcement duro; decide**". Agente de sesión "**obedece**.
  Comunicación por IPC = named pipe con ACL".
- `backlog-control-parental-windows.md:310` — T16 §Impl punto 2: "Persistir en
  **ProgramData con ACL del servicio** (T37), **nunca en el perfil del menor**."
- `backlog-control-parental-windows.md:429` — T25 §Impl punto 4: "Bloquear el
  avance del onboarding hasta consentir."
- `backlog-control-parental-windows.md:440` — T26 §Impl punto 2: "Barra
  'Protección N de M' que refleje el estado **real** de T12 (**nunca
  inflado**): cuenta estándar ✓, servicio activo ✓, watcher emitiendo ✓, capa
  preventiva ✓/✗."
- `backlog-control-parental-windows.md:442` — T26 §Impl punto 4: "Onboarding
  **reanudable** (estado persistido en el almacén del servicio)."
- `backlog-control-parental-windows.md:444` — T26 §Restr: "**el progreso nunca
  miente**; la elevación se pide al adulto."
- `backlog-control-parental-windows.md:446` — T26 §Done: "primera victoria
  antes de pasos caros · progreso real · reanudable · eventos de embudo ·
  DoD-G."
- `backlog-control-parental-windows.md:517` — T32 catálogo de eventos
  conductuales: incluye `onboarding_step_reached`, `onboarding_first_win`,
  `onboarding_completed`, `onboarding_abandoned`.
- `backlog-control-parental-windows.md:46` — DoD-G: "prohibido `catch {}`
  vacío"; this proposal removes the empty catch in
  `ConsentPage.xaml.cs:79-82`.
- Project memory #113 (topic `t37-t38-done`, Engram scope project):
  orchestrator-confirmed onboarding order is
  `pairing(0) → consent(1) → account(2) → demo(3) → managed(4)` (5 steps).
  Service install step was removed from onboarding (delivered via MSIX).

## Scope

### In scope

- All five critical audit findings (build blocker, consent theatre, progress
  lies, managed dead-end, pairing stub).
- Architectural flip (Fase 5): Service-as-source-of-truth for
  `OnboardingState`; App.UI reads/writes via IPC; one store in `%PROGRAMDATA%`
  with service ACL.
- Wiring `AccountStepViewModel` to real IPC and handling `RequiresElevation`.
- Wiring `DemoStepViewModel` to send real `ShowOverlay` via IPC.
- Emitting `onboarding_first_win` (and the rest of T32's onboarding catalog)
  from the live code path.
- Honest progress bar (`ProgressTotal = 5`, real `IEnforcementLevelMonitor`,
  no inflation branch).
- Consent copy / i18n: all user-facing strings to `Strings.resw`; accents
  fixed.
- Dead-code cleanup: remove `ServiceInstallStepPage.*`, dead VM methods,
  duplicated types in `App.UI/Interop/UIMessages.cs:75-104`.
- Tests for the critical paths the audit identified as untested.

### Out of scope

- T31 full MANAGED implementation (only the opt-in managed step inside
  onboarding; the bulk of T31 — WDAC/AppLocker/kiosk — is its own change).
- T18 / T19 / T20 (sync, push WNS, scheduler). Independent of T26 and already
  partly covered by other changes.
- Backend / Supabase work. T14 established the contract; this change only
  consumes it.
- T05/T06/T07/T08/T09/T10/T11 — already done (verified in audit).

## Plan

The change executes as 11 phases. The existing `tasks.md` in this folder
predates the audit and reflects an earlier scope (it still includes a
"ServiceInstallStepViewModel stub" task). After this proposal is accepted,
the design and tasks phases will replace `tasks.md` to encode these 11
phases; the proposal is the source of truth for sequencing and scope.

- **Fase 0 — Build blocker.** Remove duplicate `NamedPipeUIServer` /
  `NamedPipeUIServerHostedAdapter` registration in
  `src/ControlParental.Service/Program.cs:347-356`. Touch only the registration
  block. XS, isolated.
- **Fase 1 — Real consent (T25).** Replace
  `src/ControlParental.App.UI/App.xaml.cs:127` registration with an IPC consent
  adapter that calls the real Service `ConsentService` (SQLite via
  `ControlParentalDbContext`). Persist on grant. Block advance until grant is
  acknowledged by the Service. Replace the empty `catch {}` in
  `src/ControlParental.App.UI/ConsentPage.xaml.cs:79-82` with explicit error
  propagation. Fulfils `backlog-control-parental-windows.md:429`.
- **Fase 2 — Honest progress bar (T26).** Set `OnboardingViewModel.ProgressTotal = 5`
  and `MainWindow.xaml Maximum="5"` to match
  `OnboardingStateStore.CreateInitialState`'s 5-step list (per memory #113).
  Inject the real `IEnforcementLevelMonitor`. Remove the `enforcementLevelMonitor == null`
  inflation branch in `CalculateRealProgress`. Fulfils
  `backlog-control-parental-windows.md:440` and `:444`.
- **Fase 3 — Real pairing (T24 ↔ T26).** Wire `PairingViewModel` to receive a
  non-null `NamedPipeUIChannel` from `MainWindow.NavigateToStep`. Remove
  `Task.Delay(500)` simulation in `PairingPage.xaml.cs:22-27`. Surface HTTP
  404 / 410 / 429 from `PairingService` to the UI (per T24 contract:
  404 = inválido, 410 = expirado, 429 = rate limit). Retries with backoff
  remain inside the Service (per T24, no client-side retry on 404/410/429).
- **Fase 4 — Managed step.** Implement `ManagedStepPage.xaml` + VM as opt-in
  (T31's essence: single source for opt-in, no hard enforcement in STANDARD).
  Wire into `OnboardingState` step `managed(4)`.
- **Fase 5 — Inverted flow (architectural).** Service becomes source of truth
  for `OnboardingState`. App.UI never writes the file; it sends IPC messages
  (`GetOnboardingState`, `AdvanceOnboardingStep`, `ResetOnboardingState`) and
  receives the persisted result. Two stores collapse to one
  (`%PROGRAMDATA%\ControlParental\onboarding.db` with service ACL, per
  `backlog-control-parental-windows.md:310` and `:32`). Atomic write inside
  the Service eliminates the resumability race. This is the riskiest PR.
- **Fase 6 — Account step real (T37 ↔ T26).** Wire `AccountStepViewModel` to
  real IPC (`CreateAccount`, `ConvertAccount`, `GetAccounts`). Replace
  hardcoded `"UsuarioTest"`. Handle `RequiresElevation` (elevation is requested
  to the adult per `backlog-control-parental-windows.md:444`).
- **Fase 7 — Demo + funnel events.** Demo step sends real `ShowOverlay` via
  IPC; Service emits `onboarding_step_reached`, `onboarding_first_win`,
  `onboarding_completed`, `onboarding_abandoned` from the live code path
  (catalog per `backlog-control-parental-windows.md:517`).
- **Fase 8 — Copy / i18n (T25).** Move all XAML hardcoded copy to
  `Strings.resw`; bind. Fix accents. Provide age-band variants.
- **Fase 9 — Dead code / cleanup (DoD-G).** Remove `ServiceInstallStepPage.*`,
  dead VM methods, duplicated types in
  `App.UI/Interop/UIMessages.cs:75-104`. Project memory #113 already notes
  `ServiceInstallStepPage` is dead.
- **Fase 10 — Tests.** Add tests for the critical paths the audit identified
  as untested: consent-blocking-advance, honest progress bar, pairing
  404/410/429 paths, atomic write under killed-client. Temp-file paths in
  testable store (existing pattern is broken per audit; this fixes it).

## PR slicing

Chained PRs, stacked to `main`. If any PR exceeds the 400-line review budget
it is split before apply.

1. **PR #1** (XS, blocker) — Fase 0 only. Unblocks the Service.
2. **PR #2** (M) — Fase 1 + tests for consent-blocking-advance.
3. **PR #3** (M) — Fase 2 + tests for honest progress bar.
4. **PR #4** (L) — Fase 5 — the architectural flip (Service-as-source-of-truth).
   Goes 4th because everything else depends on it; isolated from PRs #2/#3 by
   landing those first.
5. **PR #5** (L) — Fase 3 + tests for pairing 404/410/429 paths.
6. **PR #6** (S) — Fase 4 (opt-in managed step).
7. **PR #7** (M) — Fases 6 + 7 + associated tests.
8. **PR #8** (M) — Fases 8 + 9 (copy + dead code cleanup; low regression risk).
9. **PR #9** (S) — Fase 10 — remaining tests (atomic write, edge cases).

## Open questions

**Service-unreachable UX during consent.** T25 §Impl punto 4
(`backlog-control-parental-windows.md:429`) says "block advance until consent".
This change's Fase 5 makes the Service the owner of consent persistence
(`backlog-control-parental-windows.md:32`). The backlog does not specify what
happens if the Service is unreachable when the adult clicks "Acepto".

Three options on the table; the proposal does NOT pick one because the user's
explicit instruction is to escalate exactly one question:

- A — Fail-closed: do not advance; show "Service not available, retry".
- B — Fail-open with retry outbox: persist consent in an encrypted local
  outbox (DPAPI machine scope, per `backlog-control-parental-windows.md:309`),
  retry uploading to Service, advance in the meantime.
- C — Fail-open with timeout: same as A but with explicit timeout before
  advancing locally.

This is the only open question. All other decisions follow the backlog
strictly.

## Risks

- **Architectural flip risk (Fase 5).** Changing who owns `OnboardingState`
  touches every step in T26. Mitigation: PR #4 is sequenced 4th, after
  consent and progress bar are already wired and tested in PRs #2/#3, so the
  flip lands on a working baseline and the diff stays focused.
- **IPC contract risk.** New IPC messages must match what SessionAgent and
  downstream consumers eventually need. Mitigation: keep the message set
  minimal (only `GetOnboardingState`, `AdvanceOnboardingStep`,
  `ResetOnboardingState`, plus the consent/pairing/account messages already
  defined in the existing `tasks.md`); add round-trip tests in PR #4.
- **Test isolation risk.** Tests that share `%LOCALAPPDATA%` paths will race
  (existing pattern is broken per audit). Mitigation: Fase 10 introduces
  temp-file paths in the testable store; PR #9 ships these fixes together.
- **Resumability race.** Kill between persist and advance can desync.
  Mitigation: atomic write inside the Service (single source of truth
  eliminates the steady-state race); Fase 10 covers the transition window
  with explicit kill-during-advance tests.
