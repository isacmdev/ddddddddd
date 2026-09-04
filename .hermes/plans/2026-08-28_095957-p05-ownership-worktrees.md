# P0.5 Ownership y worktrees Implementation Plan

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Dejar un DAG reproducible para abrir, después de Gate 0, como máximo cuatro carriles Windows sin solapamiento de ownership ni pérdida de contratos o cambios preexistentes.

**Architecture:** El trabajo Windows se divide en cuatro worktrees hermanos derivados de una única base canónica posterior al freeze de contratos: runtime/IPC/cuenta, shell UI/solicitud de tiempo, SessionAgent/overlay y instalador/release. La integración será secuencial A → B → C → D; el backend permanece fuera de estos worktrees y se desbloquea solo con la identidad de dispositivo/JWT/RLS congelada. P0.5 no implementa features, no crea tarjetas de implementación y no crea worktrees antes de la aprobación de Gate 0.

**Tech Stack:** Git worktrees en Windows, .NET 9/C#, WinUI 3/Windows App SDK, Windows Service, SessionAgent/Win32, xUnit, PowerShell, MSIX; contratos v1 en `openspec/changes/shared-contracts-freeze/`.

---

## 1. Contexto verificado

- Repositorio: `C:\Users\Usuario\Desktop\Proyectos\control-parental-windows`.
- Rama actual: `fix/windows-runtime-foundations`.
- HEAD observado: `7b74a0a4b6430610b344cea0afa9da7493e1a084` (`chore: establish audited runtime baseline`).
- El árbol no está limpio: `docs/backend-blockers-windows-implementation.md` aparece eliminado y existen cambios no rastreados en `.codegraph/`, `AGENTS.md`, `docs/product/` y varias carpetas OpenSpec, incluido `openspec/changes/shared-contracts-freeze/`.
- CodeGraph está inicializado y actualizado: 357 archivos, 8.103 nodos, 19.925 edges.
- Contratos v1 congelados documentalmente en `openspec/changes/shared-contracts-freeze/{proposal.md,design.md,tasks.md,specs/shared-contracts/spec.md}`. La especificación exige envelope versionado, ACK durable de cuenta, estados de activación y outbox, JWT/device binding, RLS, policy monotónica, integridad backend-authoritative y WNS/Realtime signal-only.
- La matriz de aceptación del freeze es CT-01..CT-12; la implementación downstream y la aceptación live siguen pendientes.

### Decisión de precondición

No se crean worktrees en P0.5. Aunque el contrato está definido, no existe una referencia Git inmutable que contenga simultáneamente el HEAD `7b74a0a` y el freeze documental, y el árbol tiene cambios ajenos/preexistentes. Crear ahora desde HEAD perdería el freeze; crear desde el árbol sucio produciría worktrees no reproducibles. Gate 0 debe exigir primero una base canónica inequívoca (commit o artefacto de parche autorizado) sin tocar `main`, y verificarla en los cuatro carriles antes de despachar implementación.

---

## 2. Base, rutas y ramas propuestas

La base se denomina `<BASE_CANONICA_P05>` hasta que Gate 0 la fije con SHA exacto. Debe contener el baseline `7b74a0a` y los cuatro artefactos del freeze de contratos, sin absorber la eliminación de `docs/backend-blockers-windows-implementation.md` ni otros cambios no atribuibles.

| Carril | Ruta de worktree propuesta | Rama de trabajo | Alcance |
|---|---|---|---|
| A | `C:\Users\Usuario\Desktop\Proyectos\control-parental-windows-wt\runtime-ipc-account` | `work/runtime-ipc-account` | Runtime, cuenta, IPC y hardening de transporte |
| B | `C:\Users\Usuario\Desktop\Proyectos\control-parental-windows-wt\ui-status-time-request` | `work/ui-status-time-request` | Navegación shell, StatusPage y solicitud de tiempo |
| C | `C:\Users\Usuario\Desktop\Proyectos\control-parental-windows-wt\sessionagent-overlay` | `work/sessionagent-overlay` | Overlay, CTA y warnings del SessionAgent |
| D | `C:\Users\Usuario\Desktop\Proyectos\control-parental-windows-wt\installer-release` | `work/installer-release` | Configuración, WNS redaction e instalador/release |

Reglas de creación:

1. Crear el directorio padre solo después de PASS de Gate 0.
2. Ejecutar `git worktree add <ruta> -b <rama> <BASE_CANONICA_P05>` para los cuatro carriles; nunca usar `main` como base.
3. Registrar para cada worktree `git rev-parse HEAD`, rama, `git status --short` y los archivos no rastreados.
4. Los cuatro HEAD deben ser idénticos antes de editar.
5. No crear worktree adicional para backend, E1 o E2 desde este task. Backend usa su repositorio/rama propia; E1/E2 esperan al MVP y se reevalúan sin aumentar el máximo de cuatro worktrees Windows.

---

## 3. Ownership exclusivo y superficies compartidas

### Carril A — `runtime-ipc-account`

Ownership exclusivo durante la fase paralela:

- `src/ControlParental.Service/Program.cs`
- `src/ControlParental.Service/UIMessageHandler.cs`
- `src/ControlParental.Service/AccountManager.cs`
- `src/ControlParental.Service/Interop/NamedPipeUIServer.cs`
- composición/lifecycle de `SessionManager` y cualquier `ProtectedSessionRuntimeCoordinator.cs` nuevo
- `src/ControlParental.Service/CertificatePinningValidator.cs`
- `src/ControlParental.Service/CertificatePinningPolicy.cs`
- tests Service de cuenta, composición, ACL, sesión y pinning

Hallazgos: W-01, W-07, W-08, W-14, W-16. No debe editar `MainWindow.*`, `App.xaml.cs`, `OverlayWindow.cs`, `IOverlayManager.cs` ni scripts de instalador.

Suite focal previa y por cambio:

- `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --verbosity minimal`
- pruebas dirigidas: `NamedPipeUIServerHostedAdapterTests`, `NamedPipeSecurityTests`, `UIMessageHandlerStateControlTests`, `OnboardingStateAtomicTests`, `OnboardingStateResumabilityTests`, `ServiceCompositionTests`, `SessionCoordinatorTests`, `AccountManagerTests`, `CertificatePinningPolicyTests`, `CertificatePinningValidatorTests`.

Dependencias: requiere P0.2 verde y contratos CT-01..CT-04/CT-07 congelados. Es prerequisito de B y de cualquier adaptación backend de identidad.

### Carril B — `ui-status-time-request`

Ownership exclusivo:

- `src/ControlParental.App.UI/App.xaml.cs`
- `src/ControlParental.App.UI/MainWindow.xaml`
- `src/ControlParental.App.UI/MainWindow.xaml.cs`
- `src/ControlParental.App.UI/OnboardingRouteCatalog.cs`
- `src/ControlParental.App.UI/StatusPage.xaml`
- `src/ControlParental.App.UI/StatusPage.xaml.cs`
- `src/ControlParental.App.UI/StatusViewModel.cs`
- view models y adaptadores UI específicos de onboarding/time request
- tests App.UI de rutas, estado y solicitud de tiempo

Hallazgos: W-02, W-03 y mitad UI de W-05. No debe editar `Service/Program.cs`, `Service/UIMessageHandler.cs` ni `SessionAgent/OverlayWindow.cs`.

Suite focal:

- `dotnet test tests/ControlParental.App.UI.Tests/ControlParental.App.UI.Tests.csproj --no-restore --verbosity minimal`
- pruebas dirigidas: `OnboardingViewModelStateRouteTests`, `OnboardingViewModelFunnelTests`, `OnboardingE2EStateMachineTests`, `AccountStepViewModelTests`, `ConsentFlowTests`, `IpcOnboardingStateServiceTests`, `IpcMessageContractTests`.

Dependencias: base contractual CT-01..CT-06; consume los mensajes/estados de A sin modificar sus definiciones. Integrar después de A para comprobar composición contra el runtime real.

### Carril C — `sessionagent-overlay`

Ownership exclusivo:

- `src/ControlParental.SessionAgent/OverlayWindow.cs`
- `src/ControlParental.SessionAgent/IOverlayManager.cs`
- `src/ControlParental.SessionAgent/Program.cs` solo para wiring de overlay/CTA, con coordinación previa si aparece un conflicto
- seams Win32 de pintura, DPI, foco y warning
- tests SessionAgent de overlay, host y safety loop

Hallazgos: W-04, W-06 y mitad SessionAgent de W-05. No debe editar `Service/Program.cs`, `App.UI/MainWindow.*` ni instalador.

Suite focal:

- `dotnet test tests/ControlParental.SessionAgent.Tests/ControlParental.SessionAgent.Tests.csproj --no-restore --verbosity minimal`
- pruebas dirigidas: `OverlayManagerTests`, `SessionAgentHostTests`, `WindowsRuntimeSafetyLoopTests`, `NamedPipeClientSeamTests`.

Dependencias: contrato de solicitud de tiempo estable y adaptador UI de B definido; el CTA emite un comando tipado, pero nunca decide aprobación. Integrar después de B para evitar que los dos carriles inventen semánticas distintas.

### Carril D — `installer-release`

Ownership exclusivo:

- `build/installer/Install-ControlParentalService.ps1`
- `build/installer/Build-ServiceInstaller.ps1`
- `build/installer/Build-MSIX.ps1` y demás archivos de packaging bajo `build/installer/`
- `src/ControlParental.Service/ConfigurationLoader.cs`
- `src/ControlParental.Service/WnsHostedService.cs` únicamente para redacción de logs
- `.env.example` y harnesses de instalación/release
- tests de configuración, WNS redaction y scripts

Hallazgos: W-09, W-12, W-15. No debe editar contratos Domain, `Service/Program.cs`, `UIMessageHandler.cs` ni UI/overlay.

Suite focal:

- `dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --verbosity minimal`
- pruebas dirigidas: `WnsHostedServiceTests`, `WnsNotificationServiceTests`, `UIMessageHandlerWnsTests`, más harness PowerShell de missing/invalid/config-write-denied/reinstall/upgrade/uninstall.

Dependencias: consume configuración y WNS signal-only ya definidos; no puede afirmar firma, SmartScreen, staging o `ExternalVerified` sin evidencia actual. Se integra último entre los carriles Windows.

### Superficies compartidas congeladas

- `src/ControlParental.Domain/**` y cualquier contrato JSON: solo cambios por decisión coordinada y rebase de los cuatro carriles.
- `ControlParental.sln`, props comunes y archivos globales: no tocar durante paralelo salvo corrección de baseline aprobada.
- `Program.cs` del Service es A; `App.xaml.cs`/`MainWindow.*` es B; overlay es C; packaging es D.
- Si un requisito parece requerir una superficie de otro carril, registrar conflicto y detener la edición; no reclamar ownership implícito.

---

## 4. DAG de ejecución e integración

```text
P0.1 selección de base ─┐
P0.2 baseline compilable ─┼─> P0.3 contratos congelados ─> P0.5 ownership/worktrees
P0.3 revalidación 28/28 ─┘                                      │
                                                               v
                                                           GATE 0 PASS
                                                               │
                              ┌────────────────────────────────┼───────────────────────────────┐
                              v                                v                               v
                         Carril A                         Carril B                         Carril C
                       runtime/IPC                     UI/status/time                  overlay/agent
                              └───────────────> integración A ────────────────┘
                                                               │
                                                               v
                                                     integración B → integración C
                                                               │
                                                               v
                                                     Carril D / integración D
                                                               │
                                                               v
                                           backend F1/F2 → F3/F4/F5 → G1 → G2 → G3
```

Interpretación operativa:

1. P0.5 entrega el diseño y deja Gate 0 como único desbloqueador de creación/despacho.
2. Gate 0 verifica base, contracts, build/suites, 28 hallazgos, ownership no solapado y `main` intacta.
3. Una vez PASS, A/B/C pueden preparar cambios independientes; la integración no es paralela: A, luego B, luego C, luego D.
4. F1/F2 backend espera la decisión contractual y la identidad de dispositivo; F3 espera la semántica de request/grant; F4 y F5 consumen contratos estabilizados.
5. E1/E2 quedan fuera del DAG MVP y no bloquean la apertura de estos cuatro carriles.

---

## 5. Protocolo de conflicto y rebase

- Antes de editar, cada carril registra `git status --short`, base SHA y ownership manifest.
- Un conflicto de archivo fuera de ownership se marca `hotspot: <path>` en la tarjeta y se detiene el cambio; no se resuelve unilateralmente.
- Un cambio en `ControlParental.Domain` requiere decisión del owner de contratos, actualización de pruebas CT afectadas y rebase coordinado de A/B/C/D.
- Un conflicto en `Service/Program.cs`, `App.xaml.cs`, `MainWindow.*` u overlay se resuelve únicamente por el owner indicado en la tabla, preservando el comportamiento de los otros carriles.
- Antes de cada integración se actualiza la rama del carril desde la base/padre autorizada, se repite la suite focal y se inspecciona el diff. No se reescribe historia ajena.
- No se hace commit, push, PR ni merge a `main` sin autorización explícita. La integración se entiende como revisión/aplicación secuencial, no como autorización de publicación.

---

## 6. Gate 0 y verificación

Gate 0 debe PASS solo si se puede demostrar todo lo siguiente:

1. SHA de base única, reproducible y posterior al freeze; las cuatro ramas/worktrees, si se crean, tienen el mismo SHA.
2. `main` no fue modificada y el borrado/cambios preexistentes del árbol actual están atribuidos, no absorbidos.
3. Contratos CT-01..CT-12 coherentes con ownership; ningún carril puede cambiar el wire contract por conveniencia.
4. Matriz W-01..W-16, B-01..B-07 y V-01..V-05 completa (28/28), con hallazgos descartados no reabiertos sin evidencia.
5. Build y suites baseline ejecutados desde fuentes actuales, sin binarios stale ni `--no-build`.
6. Manifest de ownership no solapado y dependencias A → B → C → D explícitas.
7. Backend separado del repositorio Windows y bloqueado hasta identidad/JWT/RLS.
8. No existen receipts stale presentados como aceptación live, backend-integrated o ExternalVerified.

Comandos de verificación por integración:

```text
dotnet build ControlParental.sln --no-restore --no-incremental
dotnet test tests/ControlParental.Domain.Tests/ControlParental.Domain.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/ControlParental.SessionAgent.Tests/ControlParental.SessionAgent.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/ControlParental.App.UI.Tests/ControlParental.App.UI.Tests.csproj --no-restore --verbosity minimal
dotnet test tests/ControlParental.Service.Tests/ControlParental.Service.Tests.csproj --no-restore --verbosity minimal
dotnet format whitespace ControlParental.sln --no-restore --verify-no-changes
dotnet format style ControlParental.sln --no-restore --verify-no-changes
dotnet format analyzers ControlParental.sln --no-restore --verify-no-changes
```

Este documento no declara que dichos comandos hayan sido ejecutados en P0.5; son los gates que deben ejecutar Gate 0 y cada integración. La creación real de worktrees queda explícitamente pendiente de un SHA canónico y del PASS del reviewer.

---

## 7. Resultado esperado de P0.5

- Un único documento con base, cuatro rutas, ramas, ownership, suites, dependencias, secuencia y protocolo de conflictos.
- Ninguna tarjeta de implementación creada por P0.5.
- Ningún worktree creado mientras la base no sea inequívoca.
- Gate 0 (`t_a5c68ff0`) listo para verificar y, si PASS, autorizar apertura/despacho de carriles; nunca autoriza merge a `main`.
