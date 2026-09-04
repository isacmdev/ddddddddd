# Cierre de la auditoría funcional del MVP de Windows — Plan de implementación paralelo

> **For Hermes:** Use subagent-driven-development skill to implement this plan task-by-task.

**Goal:** Cerrar, sin omitir ningún hallazgo de la auditoría, el recorrido instalable y funcional `instalar → onboarding → cuenta protegida → enforcement → estado → pedir tiempo → grant → recuperación`, manteniendo separadas las correcciones del cliente Windows, las dependencias del backend, la validación externa y los requisitos post-MVP.

**Architecture:** La ejecución se divide en cuatro carriles Windows realmente independientes (runtime/IPC, shell/UI, overlay, instalador/release) y cuatro carriles backend/integración (identidad/policy, grants/eventos, WNS, integridad). Primero se fija una única línea base y contratos compartidos; después se trabaja en paralelo; finalmente se integra de forma secuencial y se ejecutan gates completos y un journey real sobre Windows limpio. No se abrirán worktrees por tareas que compartan `Program.cs`, `UIMessageHandler.cs`, `App.xaml.cs` o `MainWindow.xaml.cs`.

**Tech Stack:** .NET 9, C#, WinUI 3/Windows App SDK, Windows Service, SessionAgent/Win32, named pipes con ACL, EF Core/SQLite, xUnit, PowerShell, MSIX + instalador Service, Supabase/Postgres/Edge Functions, WNS.

---

## 1. Contexto y reglas de ejecución

### Línea base

La auditoría original se ejecutó sobre:

- rama `feat/sdd7-4c2c2-runtime-composition`;
- commit `8d4f7b15fae8fa564cc3327a030dd264f08143d0`;
- worktree `C:/Users/Usuario/orca/workspaces/control-parental-windows/sdd7-unit-4c2c2-runtime-composition`.

El workspace actual `trout` está en `isacmdev/trout`, commit `ad414f0bc51ceec7441f7038d2ea751d9a097f73`, y no debe asumirse automáticamente como línea base de implementación. Además, los artefactos de `t26-live-flow-closure` contienen afirmaciones contradictorias: un `verify-report.md` con FAIL y un `apply-progress.md` posterior que afirma recomposición pero no presenta un gate final completo. Por ello, la primera unidad es obligatoriamente secuencial: escoger el commit fuente más avanzado, reconciliar los cambios T26/SDD7 y generar evidencia nueva.

### Restricciones

- No implementar directamente en `main`.
- No fusionar a `main` sin petición explícita.
- No hacer commit, push ni PR sin autorización explícita aplicable a esa acción.
- Preservar cambios preexistentes y no atribuirlos al trabajo nuevo.
- No declarar `client-ready`, `live-integrated`, `backend-integrated` ni `ExternalVerified=true` sin receipts actuales.
- WNS solo señala que hay que sincronizar; nunca transporta ni autoriza la policy.
- El backend y Windows mantienen autoridades separadas: el cliente aplica la última policy local válida; el backend autentica, autoriza, versiona y deduplica.
- Un worktree solo se crea para uno de los carriles independientes definidos abajo; no uno por hallazgo.

---

## 2. Inventario completo de hallazgos y trazabilidad

Cada identificador debe aparecer en una tarea, una prueba y un gate final.

| ID | Hallazgo | Clase | Prioridad | Unidad del plan |
|---|---|---|---|---|
| W-01 | ACL del pipe App.UI → Service rechaza la UI normal/no elevada | Bug cliente | P0 | A2 |
| W-02 | Ruta `OnboardingRoute.Completed` puede dejar `PageHost.Content = null`; Status no queda compuesto | Bug cliente | P0 | B1 |
| W-03 | Botón “Pedir tiempo” es un no-op y no crea `TimeRequestEntry` | Bug cliente | P0/P1 | B2 |
| W-04 | Overlay no pinta motivo ni CTA real | Bug cliente | P0 | C1 |
| W-05 | `CtaClicked` no está conectado al flujo de solicitud | Bug cliente | P0/P1 | C2 + B2 |
| W-06 | `ShowWarning(10/5)` usa overlay bloqueante sin autocierre | Bug cliente | P1 | C3 |
| W-07 | Onboarding no persiste la cuenta Windows protegida | Bug cliente | P0 | A1 |
| W-08 | Service no activa/reconfigura `SessionManager` en caliente | Bug cliente/SDD8-T26 | P0 | A3 |
| W-09 | Instalador no provisiona ni valida `SUPABASE_URL`/`SUPABASE_ANON_KEY` | Bloqueador mixto | P0/P1 | D1 |
| W-10 | No existe reparación productiva de un toque para Service/agente/cuenta/hook | Faltante cliente | P2 | E1 |
| W-11 | MANAGED detecta capacidades, pero no aprovisiona WDAC/AppLocker/kiosk/MDM | Post-MVP salvo promesa MANAGED | P2 | E2 |
| W-12 | MSIX/Service bundle no forman un instalador confiable firmado y distribuible | Release | P1/P2 | D2 |
| W-13 | Falta matriz Win10/Win11, Home/Pro/Ent-Edu, x64/ARM64 y clean install | Validación | P1/P2 | G2 |
| W-14 | Pin de certificado debe fallar cerrado ante mismatch | Seguridad cliente | P1 | A4 |
| W-15 | WNS Channel URI/token no debe aparecer completo en logs | Privacidad/seguridad cliente | P1 | D3 |
| W-16 | Flujos de cuenta contienen sync-over-async y riesgo de deadlock/cancelación | Deuda funcional preventiva | P2 | A5 |
| B-01 | Pairing no entrega/restaura JWT definitivo con `device_id`; falta prueba RLS de dos dispositivos | Backend crítico | P0 | F1 |
| B-02 | Policy backend incompleta y autorización por dispositivo insuficiente | Backend crítico | P0 | F2 |
| B-03 | No existe registro, renovación, fan-out y limpieza WNS backend | Backend | P1 | F4 |
| B-04 | Aprobación de tiempo y creación de grant no son atómicas/idempotentes | Backend crítico | P0/P1 | F3 |
| B-05 | Alertas/eventos/solicitudes no tienen deduplicación uniforme | Backend | P1 | F3 |
| B-06 | No existe veredicto remoto de integridad Windows real y fail-safe | Backend/integridad | P1 | F5 |
| B-07 | Contradicción contractual de `child_first_name` en pairing | Decisión de contrato | P0 antes de F1 | P0.3 |
| V-01 | `live-integrated`, `backend-integrated` y `ExternalVerified=true` no están acreditados | Gate externo | Final | G3 |
| V-02 | Evidencia T26/app shell contiene reportes contradictorios y pruebas bloqueadas/stale | Integridad de evidencia | P0 | P0.1–P0.4 |
| V-03 | Build actual puede quedar bloqueado por `App.UI/Program.cs` duplicado o test seam obsoleto | Baseline | P0 | P0.2 |
| V-04 | Analyzer gate completo no era verde; no debe ocultarse con `NoWarn` global | Calidad | P1 | G1 |
| V-05 | Journey real en Windows limpio no fue demostrado | Aceptación | P0/P1 | G2 |

### Hallazgos explícitamente descartados en la auditoría y que no deben reabrirse sin nueva evidencia

- Longitud de pairing cliente/servicio: ambos usaban seis caracteres en la línea base auditada.
- Listener de `AgentLauncher`: no esperaba conexión antes de crear el proceso.
- Acumulación de uso: conservaba segundos.
- Policy sync: utilizaba `identity.DeviceId`, no `"default"`.
- Backoff: utilizaba `nextEligibleAt` y tiempo real.
- Recovery: estaba conectado a `SessionManager.RecoverAgentAsync()`.
- Argumentos del backup: sí se parseaban y llegaban a `RunBackupAsync()`.
- El pipe UI sí estaba registrado; el defecto era su ACL.

Solo se reabrirán si una prueba nueva sobre la línea base elegida falla.

---

## 3. Decisión de paralelismo

### Trabajo obligatoriamente secuencial

1. Seleccionar la línea base y limpiar/atribuir los bloqueadores de compilación.
2. Congelar contratos IPC compartidos: selección de cuenta, activación runtime y solicitud de tiempo.
3. Integrar los carriles Windows en la rama padre elegida.
4. Ejecutar build, suites completas y journey Windows limpio.
5. Otorgar o negar los estados de readiness.

### Carriles que sí aportan paralelismo real

Después del gate P0 pueden ejecutarse simultáneamente:

- **Carril A — Runtime, cuenta e IPC:** W-01, W-07, W-08, W-14, W-16.
- **Carril B — Shell diaria y solicitud de tiempo:** W-02, W-03 y la mitad UI de W-05.
- **Carril C — Overlay y warnings:** W-04, mitad SessionAgent de W-05, W-06.
- **Carril D — Instalación, configuración y release:** W-09, W-12, W-15.
- **Carril F1/F2 — Backend identidad + policy:** B-01, B-02 y B-07; se mantienen juntos porque comparten JWT/RLS/contrato de dispositivo.
- **Carril F3 — Backend grants + eventos:** B-04 y B-05; comparten transacciones, claves idempotentes y semántica de retry.
- **Carril F4 — WNS:** B-03; independiente tras congelar identidad de dispositivo.
- **Carril F5 — Integridad:** B-06; independiente tras congelar el contrato de verdict.

### Worktrees recomendados

Máximo cuatro worktrees Windows, no ocho:

1. `runtime-ipc-account`
2. `ui-status-time-request`
3. `sessionagent-overlay`
4. `installer-release`

Los trabajos backend ocurren en el repositorio backend y deben usar su propia rama de trabajo; no requieren worktrees adicionales de Windows salvo para adaptar contratos. E1/E2 (reparación y MANAGED) se ejecutan después del MVP base, preferentemente sin worktree nuevo si el carril runtime ya terminó.

### Conflictos previstos

| Archivos/superficies | Regla |
|---|---|
| `ControlParental.Service/Program.cs`, `UIMessageHandler.cs` | Solo Carril A durante la fase paralela |
| `ControlParental.App.UI/App.xaml.cs`, `MainWindow.*` | Solo Carril B |
| `ControlParental.SessionAgent/OverlayWindow.cs`, `IOverlayManager.cs` | Solo Carril C |
| `build/installer/*`, `Build-MSIX.ps1` | Solo Carril D |
| Contratos en `ControlParental.Domain` | Se congelan en P0; cambios posteriores requieren decisión y rebase coordinado |

---

## 4. Plan por unidades

### P0.1: Elegir una línea base única y reconciliar evidencia

**Objective:** Evitar implementar sobre `trout` o sobre receipts obsoletos por error.

**Files/evidence:**
- `openspec/changes/t26-live-flow-closure/verify-report.md`
- `openspec/changes/t26-live-flow-closure/apply-progress.md`
- `openspec/changes/t23-agent-integrity/*`
- `src/ControlParental.App.UI/App.xaml.cs`
- `src/ControlParental.App.UI/MainWindow.xaml`
- `src/ControlParental.App.UI/MainWindow.xaml.cs`

**Steps:**
1. Comparar la rama padre activa, `feat/sdd7-4c2c2-runtime-composition` y cualquier rama T26 posterior.
2. Enumerar commits y cambios no integrados por capacidad, no solo por fecha.
3. Elegir el commit que contiene SDD7 más la composición T26 válida.
4. Registrar SHA, rama, worktree, `git status`, archivos no rastreados y razones de elección.
5. No crear worktrees de implementación antes de este gate.

**Acceptance:** Una sola línea base reproducible; ningún cambio del usuario perdido; discrepancia T26 explicada.

### P0.2: Restaurar baseline compilable y ejecutable

**Objective:** Resolver V-03 sin ocultar deuda ni usar binarios stale.

**Likely files:**
- `src/ControlParental.App.UI/Program.cs`
- `src/ControlParental.App.UI/ControlParental.App.UI.csproj`
- `tests/ControlParental.Service.Tests/AgentLauncherLaunchSeamTests.cs`
- `src/ControlParental.Service/AgentLauncher.cs`

**TDD/verification:**
1. Ejecutar `dotnet build ControlParental.sln --no-restore --verbosity minimal` y guardar salida/exit code.
2. Si existe el `Program` duplicado, decidir con evidencia si eliminar el entrypoint manual o desactivar la generación; añadir prueba/build que impida la duplicación.
3. Alinear el test seam de AgentLauncher con la API actual o retirarlo solo si se demuestra que prueba una interfaz eliminada y tiene cobertura equivalente.
4. Repetir build desde fuentes actuales, sin `--no-build`.
5. Ejecutar las cuatro suites seriales y registrar baseline.

**Acceptance:** Solución compila desde cero y todas las suites alcanzan runtime; cualquier fallo restante queda atribuido por prueba.

### P0.3: Congelar contratos compartidos

**Objective:** Permitir paralelismo sin que UI, Service, SessionAgent y backend inventen mensajes incompatibles.

**Contracts:**
- `SetProtectedAccountRequest/Response` con username/SID normalizado, idempotency key y estado de activación.
- `RuntimeActivationState` con `NotConfigured`, `Activating`, `Active`, `Degraded`, `Failed` y razón observable.
- `CreateTimeRequest` con request ID estable, minutos solicitados, origen (`StatusPage`/overlay), policy version y estado outbox.
- `IntegrityEvidence/Verdict` Windows.
- `child_first_name`: decidir si lo proporciona tutor, Windows o deja de ser requerido.

**Files likely to change:**
- `src/ControlParental.Domain/IpcMessage.cs`
- `src/ControlParental.Domain/UIMessagesJsonContext.cs`
- `src/ControlParental.Domain/TimeRequestEntry.cs`
- `apis.md`
- specs OpenSpec correspondientes.

**Tests:** serialización round-trip, compatibilidad de versión, rechazo fail-closed de payload inválido y source-generated JSON context.

**Acceptance:** Contratos aprobados y congelados antes de abrir los cuatro carriles Windows.

### P0.4: Crear worktrees/carriles y baseline por carril

**Objective:** Obtener paralelismo real sin conflictos ocultos.

**Steps:**
1. Crear solo los cuatro worktrees enumerados.
2. Asignar ownership exclusivo de archivos.
3. Ejecutar la suite focal de cada carril antes de editar.
4. Guardar command, exit code y fallos preexistentes.
5. No iniciar backend hasta cerrar B-07 y el contrato JWT/device.

---

## Carril A — Runtime, cuenta e IPC

### A1: Persistir la cuenta protegida (W-07)

**Files:**
- Modify: `src/ControlParental.App.UI/AccountStepViewModel.cs`
- Modify: `src/ControlParental.Service/UIMessageHandler.cs`
- Modify: `src/ControlParental.Service/AccountManager.cs` solo si el contrato durable requiere async/cancelación
- Tests: `tests/ControlParental.App.UI.Tests/AccountStepViewModelTests.cs`
- Tests: `tests/ControlParental.Service.Tests/UIMessageHandler*Tests.cs`

**Steps:**
1. Escribir test RED: seleccionar cuenta no puede completar onboarding hasta recibir ACK durable del Service.
2. Escribir test RED: repetir la misma selección es idempotente.
3. Escribir test RED: usuario inexistente/admin/rechazado no avanza.
4. Implementar mensaje IPC y llamada a `SetChildAccountName` después de validación.
5. Hacer que la UI muestre pendiente/error y solo invoque completion tras ACK.
6. Verificar restart y lectura durable.

**Acceptance:** La cuenta seleccionada persiste, se valida como estándar y onboarding no miente sobre éxito.

### A2: Corregir ACL App.UI → Service (W-01)

**Files:**
- Modify: `src/ControlParental.Service/Interop/NamedPipeUIServer.cs`
- Modify: composición en `src/ControlParental.Service/Program.cs`
- Tests: `tests/ControlParental.Service.Tests/NamedPipeUIServerHostedAdapterTests.cs`
- Tests: nuevos casos ACL/identity.

**Steps:**
1. Test RED con UI no elevada del padre autorizado.
2. Test RED con menor y usuario ajeno denegados.
3. Test RED para administrador elevado/no elevado.
4. Resolver SID del padre de una fuente durable y explícita; no abrir a `Authenticated Users`.
5. Construir `PipeSecurity` con `LocalSystem` + SID parental exacto y denegaciones necesarias.
6. Probar reconexión, restart y rotación/ausencia de SID fail-closed.

**Acceptance:** App.UI normal conecta; menor y terceros no; no se debilita el aislamiento.

### A3: Activar/reconfigurar runtime en caliente (W-08)

**Files:**
- Modify: `src/ControlParental.Service/Program.cs`
- Modify/refactor: `SessionManager` o nuevo `ProtectedSessionRuntimeCoordinator.cs`
- Modify: `UIMessageHandler.cs`
- Tests: Service composition, session manager, onboarding activation, fast-user-switching.

**Steps:**
1. Test RED: tras `SetProtectedAccount`, el runtime pasa a `Active` sin reiniciar Service.
2. Test RED: reselección segura detiene la autoridad anterior antes de iniciar la nueva.
3. Test RED: fallo al lanzar agente deja estado `Degraded/Failed`, no `Completed` falso.
4. Extraer coordinación de lifecycle de `Program.cs` si es necesario para testearla.
5. Conectar IPC, `SessionManager`, agente, `SessionSafetyLoop`, reconciliador y locks.
6. Verificar idempotencia, restart, lock/unlock, fast-user-switching y shutdown.

**Acceptance:** Instalación nueva protege la cuenta inmediatamente y recupera el estado después de reiniciar.

### A4: TLS pinning fail-closed (W-14)

**Files:** `CertificatePinningValidator.cs`, `CertificatePinningPolicy.cs`, callback HTTP en `Program.cs`, tests existentes.

**Tests:** pin válido pasa; mismatch falla; certificado ausente falla; TLS ordinario válido sin política incorrecta no se rompe; logs no exponen secretos.

### A5: Eliminar sync-over-async en cuenta (W-16)

**Files:** `AccountManager.cs`, interfaz y callers.

**Tests:** cancelación, timeout, ausencia de deadlock bajo synchronization context y propagación de errores. Ejecutar después de A1/A3 para evitar editar el mismo contrato dos veces.

---

## Carril B — Shell diaria y tiempo adicional

### B1: Cerrar navegación post-onboarding (W-02, V-02)

**Files:**
- `src/ControlParental.App.UI/App.xaml.cs`
- `src/ControlParental.App.UI/MainWindow.xaml`
- `src/ControlParental.App.UI/MainWindow.xaml.cs`
- `src/ControlParental.App.UI/OnboardingRouteCatalog.cs`
- `src/ControlParental.App.UI/StatusPage.xaml.cs`
- tests T26, consent, route, dead-code, status.

**Steps:**
1. Test RED para `Completed → StatusPage`, `Abandoned → estado explícito` y restart ya completado.
2. Confirmar que `StatusViewModel`, `IUIPipeClient/IUIChannel` y `IRealtimeSubscriber` estén registrados en DI.
3. Componer `StatusPage` como destino diario; nunca asignar `null` silenciosamente.
4. Reconciliar la composición T26: consentimiento/transparencia in-app, estado Service-owned y `PageHost` real.
5. Ejecutar suites T26 desde build actual, no desde binarios previos.

**Acceptance:** completar o reanudar onboarding siempre produce una pantalla honesta y funcional.

### B2: Implementar solicitud de tiempo desde StatusPage (W-03)

**Files:**
- `src/ControlParental.App.UI/StatusViewModel.cs`
- `src/ControlParental.App.UI/StatusPage.xaml`
- canal IPC/Service handler del contrato P0.3
- `OutboxManager.cs`/repositorio de requests si corresponde
- tests UI, handler y outbox.

**Steps:**
1. Test RED: click crea una sola solicitud con ID estable.
2. Test RED: offline queda `Queued`; ACK la cambia a `Sent/Pending`; retry no duplica.
3. Test RED: throttle evita spam sin perder solicitud válida.
4. Implementar selección de duración permitida y copy honesto.
5. Mostrar estados `queued`, `pending`, `approved`, `denied`, `failed`, `applied` sin colapsarlos.
6. Consumir grant de versión superior y actualizar estado.

**Acceptance:** El botón deja de ser no-op y funciona offline-first hasta el límite del contrato backend.

### B3: Adaptador de origen overlay (mitad UI de W-05)

Crear un entrypoint compartido para que StatusPage y overlay produzcan la misma solicitud/idempotency semantics. No duplicar lógica de negocio en SessionAgent.

---

## Carril C — Overlay, CTA y warnings

### C1: Renderizar overlay real (W-04)

**Files:**
- `src/ControlParental.SessionAgent/OverlayWindow.cs`
- `src/ControlParental.SessionAgent/IOverlayManager.cs`
- Win32 interop y tests de overlay.

**Steps:**
1. Test seam RED para comandos de pintura: fondo, motivo, CTA, bounds y DPI.
2. Implementar GDI/Direct2D mínimo con liberación segura de handles.
3. Calcular hit target del CTA y foco/teclado accesible.
4. Manejar multi-monitor, DPI, display changes y topmost.
5. Validar que cerrar/ocultar UI no desactive la policy.

**Acceptance:** bloqueo visible explica motivo y ofrece solo acciones reales.

### C2: Conectar CTA a solicitud (W-05)

**Steps:**
1. Test RED: clic emite exactamente un comando tipado al Service.
2. Conectar `CtaClicked` al canal SessionAgent → Service; el Service crea la solicitud mediante el contrato compartido.
3. Mostrar estado pendiente sin afirmar aprobación.
4. Probar desconexión/reconexión e idempotencia.

### C3: Separar warning de bloqueo (W-06)

**Steps:**
1. Test RED: warning no cambia `isOverlayVisible` del bloqueo.
2. Implementar toast/ventana no bloqueante separada con autocierre real.
3. Cancelar/reemplazar warnings anteriores de forma segura.
4. Verificar 10 min, 5 min, click CTA y expiración.

**Acceptance:** el aviso nunca impide usar la app antes de que el límite expire.

---

## Carril D — Instalador, configuración, logs y release

### D1: Provisionar configuración (W-09)

**Files:**
- `build/installer/Install-ControlParentalService.ps1`
- `build/installer/Build-ServiceInstaller.ps1`
- `ConfigurationLoader.cs`
- `.env.example`
- tests/harness del instalador.

**Steps:**
1. Definir un mecanismo no secreto-en-línea-de-comandos para URL + publishable/anon key.
2. Crear `%ProgramData%\ControlParental\.env` con ACL restringida o usar un almacén mejor documentado.
3. Validar formato/conectividad sin imprimir la key.
4. Hacer rollback si Service no alcanza `RUNNING`/health.
5. Añadir escenarios missing/invalid/config-write-denied/reinstall/upgrade/uninstall.

**Acceptance:** instalación limpia deja Service arrancado y configurado; secreto/key no aparece en logs o receipts.

### D2: Unificar y endurecer distribución (W-12)

**Steps:**
1. Definir bootstrapper único para UI MSIX + Service/SessionAgent.
2. Versionar artefactos y checksums.
3. Firmar todos los binarios e instaladores; verificar Authenticode.
4. Implementar install/repair/upgrade/uninstall y rollback.
5. No afirmar EV/SmartScreen hasta contar con certificado y receipt real.

### D3: Redactar WNS channel en logs (W-15)

**Files:** `WnsHostedService.cs` y tests de logging.

**Acceptance:** logs contienen solo metadatos seguros o hash parcial; nunca URI/token completo.

---

## Carril E — Faltantes Windows post-MVP

### E1: Reparación de un toque (W-10)

Implementar acciones separadas y autorizadas para Service detenido, agente muerto, watcher inactivo y cuenta admin. Cada acción debe mostrar qué hará, requerir elevación solo cuando corresponda, re-verificar y anunciar recuperación únicamente después de evidencia.

### E2: Modo MANAGED (W-11)

Mantener fuera del MVP STANDARD salvo cambio explícito de alcance. Diseñar `IHardEnforcer` por capacidad/edición; aprovisionar WDAC/AppLocker/Assigned Access/MDM solo con laboratorio y rollback. STANDARD nunca debe quedar bloqueado por no disponer de MANAGED.

---

## Carriles backend

### F1: Pairing/JWT/RLS y `child_first_name` (B-01, B-07)

1. Resolver decisión contractual de `child_first_name`.
2. Evitar crear una identidad que Windows no pueda autenticar.
3. Emitir/restaurar JWT con `device_id` autoritativo.
4. Probar refresh y revocación.
5. Probar dos dispositivos: propios permitidos, acceso cruzado rechazado.

### F2: Policy completa y autorizada (B-02)

1. Devolver límites globales, app, categoría, horarios, downtime, grants y versión sin defaults que oculten datos.
2. Vincular `device_id` solicitado al JWT/RLS.
3. Añadir contrato de versionado monotónico y payload fixture compatible con Windows.
4. Probar policy ajena rechazada y policy antigua ignorada por cliente.

### F3: Grants y eventos idempotentes (B-04, B-05)

1. Hacer aprobación + grant + version bump atómicos.
2. Añadir unique key por request y respuesta estable para replay.
3. Definir IDs de cliente y constraints para alertas/eventos/requests.
4. Clasificar errores temporales/terminales para outbox.
5. Probar fallo en cada punto transaccional y retries concurrentes.

### F4: WNS end-to-end (B-03)

1. Registrar/renovar canal por `device_id` y `channel=wns`.
2. Guardar expiración; limpiar 404/410.
3. Enviar payload raw mínimo de señal.
4. Probar signal → pull HTTPS; datos nunca en push.
5. Probar polling cuando WNS falla.

### F5: Veredicto de integridad Windows (B-06)

1. Definir `trusted`, `revoked`, `unknown`, `verifier_unavailable` y versionado.
2. Evaluar evidencia Windows real; no reutilizar simulación Android.
3. Probar versión revocada, evidencia inválida, replay y caída del verificador.
4. Fallar de forma segura y producir receipts auditables.

---

## 5. Integración secuencial y gates

### G1: Integración de código y calidad

Orden recomendado:

1. Contratos P0.
2. Carril A runtime/IPC.
3. Carril B UI.
4. Carril C overlay.
5. Carril D instalador.
6. Adaptadores backend.

Después de cada integración:

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

No usar `NoWarn` global para conseguir verde. Si la deuda previa impide el gate, establecer baseline de diagnósticos y exigir cero diagnósticos nuevos, con plan separado para reducir la deuda.

### G2: Journey real sobre Windows limpio (W-13, V-05)

Probar al menos Win11 x64 primero como gate MVP:

1. Instalar bundle firmado/lab-signed.
2. Verificar Service `RUNNING` y recovery.
3. Abrir App.UI sin elevación.
4. Pairing y consentimiento.
5. Seleccionar/crear cuenta estándar.
6. Confirmar persistencia y activación inmediata.
7. Completar demo y llegar a StatusPage.
8. Aplicar policy cacheada, desconectar red y bloquear app.
9. Ver motivo/CTA en overlay multi-monitor.
10. Mostrar warning no bloqueante.
11. Crear solicitud desde status y overlay; comprobar dedupe.
12. Aprobar en staging; recibir grant por polling y, cuando esté disponible, WNS.
13. Reiniciar, lock/unlock y fast-user-switching; comprobar recuperación.
14. Desinstalar y verificar limpieza sin borrar datos fuera del scope acordado.

Luego ampliar matriz a Win10 22H2, Win11 23H2/24H2, Home/Pro y Ent/Edu; x64 obligatorio, ARM64 antes de prometer soporte.

### G3: Gates backend y readiness (V-01)

- `client-ready`: solo si journey local/offline Windows pasa en máquina limpia.
- `backend-integrated`: solo si staging demuestra B-01, B-02, B-04 y B-05.
- `live-integrated`: requiere además WNS/polling real y journey tutor→menor.
- `ExternalVerified=true`: requiere receipts externos actuales, hashes y alcance explícito.

Un receipt solo acredita lo que declara; no extender su alcance por inferencia.

### G4: Revisión final independiente

1. Inspeccionar diff completo y `git status` de cada carril.
2. Confirmar que cada ID W/B/V tiene código/prueba/receipt o una decisión post-MVP explícita.
3. Revisar secretos, ACL, logs, migraciones, rollback y compatibilidad.
4. Resincronizar CodeGraph si el proyecto se indexa; mientras no exista `.codegraph`, usar inspección de símbolos/archivos y documentar el fallback.
5. No fusionar a la rama padre hasta revisión independiente y autorización.
6. No fusionar a `main` sin petición explícita.

---

## 6. Riesgos y mitigaciones

| Riesgo | Mitigación |
|---|---|
| Worktrees sobre ramas distintas/obsoletas | P0 elige SHA único y exige rebase antes de editar |
| Conflictos en `Program.cs`/Domain contracts | Ownership exclusivo y contratos congelados |
| Abrir demasiado la ACL para “arreglar” IPC | Tests padre permitido/menor-tercero denegados; SID exacto |
| Doble autoridad durante cambio de cuenta | Coordinador serializado: detener anterior antes de activar nueva |
| UI muestra éxito antes de persistencia/activación | ACK Service-owned y estados pending/degraded/failed |
| Doble solicitud desde Status y overlay | ID estable, unique constraint y un solo servicio de creación |
| Warning reutiliza estado de bloqueo | Ventana/canal de estado separado |
| Secretos en instalador, CLI o receipts | Canal seguro, ACL, redacción y tests de logs |
| Backend duplica grants/eventos en retry | Transacción + unique constraints + respuestas idempotentes |
| WNS se convierte en autoridad | Payload señal-only; siempre pull HTTPS autenticado |
| Reports stale se tratan como prueba | Builds actuales sin `--no-build`, hashes y timestamps |
| Scope crece hacia MANAGED/release público | E1/E2 y matriz completa separados del MVP STANDARD |

---

## 7. Criterio de finalización

El plan solo se considera completado cuando:

- todos los IDs de la tabla de trazabilidad están cerrados o explícitamente aceptados como post-MVP;
- build y suites aplicables corren sobre fuentes actuales;
- App.UI no elevada conecta con Service sin permitir acceso al menor/terceros;
- onboarding persiste y activa la cuenta protegida sin reiniciar Service;
- la shell termina en StatusPage, no en contenido nulo;
- overlay y warnings tienen comportamiento visible y correcto;
- solicitudes desde StatusPage y overlay son offline-first e idempotentes;
- instalador provisiona configuración sin filtrar credenciales;
- staging demuestra identidad, policy, grants y dedupe;
- polling funciona aunque WNS falle, y WNS real se acredita por separado;
- un Windows limpio completa el journey y recupera tras restart;
- el diff, status, tests y receipts son revisados independientemente;
- no se ha hecho merge a `main` sin autorización.

## 8. Secuencia recomendada por calendario lógico

```text
P0 baseline + contratos
        │
        ├── A runtime/IPC/account ─────────┐
        ├── B shell/status/request ────────┤
        ├── C overlay/warnings ────────────┤
        ├── D installer/release ───────────┤
        ├── F1+F2 identity/policy ─────────┤
        ├── F3 grants/events ──────────────┤
        ├── F4 WNS ────────────────────────┤
        └── F5 integrity ──────────────────┘
                                           │
                              G1 integración secuencial
                                           │
                              G2 Windows limpio + matriz
                                           │
                              G3 readiness externo
                                           │
                              G4 revisión independiente
                                           │
                              E1/E2 post-MVP si se autoriza
```

Este DAG ahorra tiempo porque el trabajo sobre UI, SessionAgent, instalador y backend no espera a que termine el runtime, pero evita paralelizar cambios que comparten composición, contratos o autoridad de sesión.