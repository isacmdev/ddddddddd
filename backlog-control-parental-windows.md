---
title: "Backlog ejecutable por agente IA — Control Parental para Windows (Agente + Backend)"
platform: "Windows 10/11 · .NET 9 · C# · WinUI 3 · Supabase"
self_contained: true
alcance: "Cliente AGENTE CUSTODIO únicamente. Su única responsabilidad es custodiar y obedecer las configuraciones y órdenes del tutor según las reglas de negocio. Consume las MISMAS APIs que el cliente Android (backend ya existente, compartido); NO construye el backend ni la interfaz del tutor (app del padre, fuera de alcance). El único cambio de backend que requiere es el canal de push WNS (ver T14)."
numeracion: "Se conservan los IDs del backlog original. T15 se fusionó en T14 (el backend ya existe: el agente lo consume, no lo construye) y T33 se eliminó (feature del tutor). Los huecos T15 y T33 son intencionales, no omisiones."
nota: "Cada tarea trae su contexto. Las únicas referencias externas válidas son: (a) este bloque de Reglas globales, leído una vez; (b) el código producido por tareas previas (se ejecutan en orden de §FIN)."
---

# Reglas globales (leer una vez; vinculan a TODA tarea)

## Stack obligatorio (usa exactamente esto)
| Tema | USA | NUNCA |
|---|---|---|
| Lenguaje/runtime | C# / .NET 9 | .NET Framework para lógica nueva |
| UI | WinUI 3 (Windows App SDK), MVVM, `x:Bind` | WinForms/WPF salvo interop justificado |
| DI/arranque | `Microsoft.Extensions.DependencyInjection` + Generic Host; servicio con `UseWindowsService()` | service locator, `new` disperso |
| Persistencia local | EF Core + SQLite | ADO.NET crudo sin capa |
| Secretos en disco | DPAPI (`ProtectedData`, scope máquina + entropía) o `DataProtectionProvider`; clave en TPM/CNG si existe | claro, registro sin cifrar, `appsettings` versionado |
| Concurrencia | `async`/`await` con `CancellationToken`; estado reactivo con System.Reactive o `Channel<T>` | `Thread` crudo, `.Result`/`.Wait()`, `async void` salvo handlers |
| Serialización | System.Text.Json con `JsonSerializerContext` source-gen (AOT-safe) | Newtonsoft salvo necesidad |
| Backend | `supabase-csharp` (gotrue/postgrest/realtime/functions) + `HttpClient`/`SocketsHttpHandler`; **publishable key** + JWT con claim `device_id` | `anon`/`service_role` en cliente |
| Background | Servicio de Windows (continuo) + Task Scheduler (disparadores) | tareas que el usuario mate fácilmente |
| Push | WNS raw notification (requiere identidad MSIX) + sondeo del servicio como respaldo | aplicar datos del payload del push |
| Bloqueo duro | WDAC (universal) / AppLocker (Ent/Edu) preventivo; `TerminateProcess`/Job Objects reactivo | depender solo de matar procesos |
| Bloqueo total | `LockWorkStation()` desde el agente; Assigned Access en MANAGED | matar `winlogon`/`csrss` |
| Empaquetado | MSIX (UI + identidad WNS) + MSI/EXE (servicio); todo Authenticode **EV** | binarios sin firmar |
| Paquetes | Central Package Management (`Directory.Packages.props`), última estable | versiones dispersas |
| Tests | xUnit + Moq/NSubstitute + FluentAssertions + RichardSzalay.MockHttp + SQLite in-memory + WinAppDriver/Appium | servidor HTTP real en unit tests |

## Arquitectura invariante (toda tarea la respeta)
- **Dos procesos.** **Servicio** (`LocalSystem`, Sesión 0, auto-arranque): dueño de SQLite, motor, sync, heartbeat y enforcement duro; **decide**. **Agente de sesión** (proceso en la sesión interactiva del menor, lo lanza el Servicio): ve el primer plano, pinta overlay/avisos; **obedece**. Comunicación por **IPC = named pipe con ACL** (solo `LocalSystem`↔agente del usuario objetivo) o gRPC sobre named pipe. *El Servicio NUNCA pinta UI (aislamiento de Sesión 0). El Agente NUNCA decide bloqueos.*
- **Resistencia a manipulación = modelo de cuentas**, no una API: **menor = usuario estándar**, **padre = único administrador**; instalación en `Program Files` y datos en `ProgramData` con **ACL** que niega escritura/borrado a usuarios estándar; claves de registro del servicio con ACL equivalente; servicio con recuperación del SCM. Un usuario estándar no puede detener servicios, cambiar la hora, editar `HKLM` ni desinstalar. Sin esto → best-effort (`DEGRADED`).
- **Offline-first.** El enforcement nunca depende de red; el motor opera sobre SQLite. El push (WNS) solo dice "sincroniza ahora" (señal, no dato): el Servicio hace un `GET` autenticado y no confía en el payload. Una política se aplica **solo si `version > version_local`**. Realtime solo mientras hay UI en primer plano.

## Identidad de app (`AppId` canónico, usado en todo el sistema)
Apps MSIX → **AUMID/PackageFamilyName**. Apps Win32 → `nombre.exe` + **publisher Authenticode** (fallback: hash del binario). **Nunca** la ruta cruda. El backend guarda este `AppId` en el campo `package_name`. La función `ResolveAppIdentity(processPath) → AppId` la crea T05 y la usan T06/T11.

## Compatibilidad
Probar en **Win10 22H2, Win11 23H2, Win11 24H2**; **x64 y ARM64**. Cada capacidad sensible a edición va detrás de interfaz (`IHardEnforcer`, `IForegroundWatcher`, `ISecretStore`) y se detecta en runtime: **WDAC** universal; **AppLocker/Assigned Access/Shell Launcher** solo Enterprise/Education/IoT; **BitLocker/GPO** Pro+. Nunca anunciar protección que la edición no soporta.

## Privacidad
Recolectar **solo uso por app** (`AppId` + minutos + fecha). Nunca contenido, ni títulos de ventana con datos personales, ni texto, ni mensajes, ni capturas. Retención limitada.

## DoD-G (Definition of Done universal — cada tarea la cumple además de su `Done`)
Verde y verificado antes de marcar hecho: `dotnet build` · `dotnet test` · `dotnet format --verify-no-changes` + analizadores Roslyn (`AnalysisLevel=latest-recommended`) y StyleCop como error en CI · `Nullable` enable, prohibido `!` sin comentario · capas separadas (proyecto `Domain` = .NET puro, **0** P/Invoke ni `using Windows.*`) · errores explícitos (`Result<T>`/jerarquía `sealed`, prohibido `catch {}` vacío) · inmutabilidad por defecto (`record`/`init`/`with`) · 0 secretos en el diff · 0 contenido del menor en logs/eventos · en `Release`: AOT/ReadyToRun + ofuscación IL sin romper serialización/EF/DI.

---

# Tareas

## T00 — Solución, tooling y empaquetado
**Obj:** solución .NET compilable, firmable y empaquetable; tooling listo.
**Dep:** —
**Impl:**
1. Solución con proyectos: `Domain` (`net9.0`, .NET puro: modelos, motor, tiempo), `Service` (`net9.0-windows`, worker `UseWindowsService()`), `SessionAgent` (`net9.0-windows`, proceso de sesión), `App.UI` (`net9.0-windows`, WinUI 3), y un proyecto de tests por cada uno (xUnit).
2. `Directory.Packages.props` (CPM, última estable): EF Core + `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.Extensions.Hosting(.WindowsServices)`, `System.Reactive`, `supabase-csharp`, `Microsoft.WindowsAppSDK`, `CommunityToolkit.Mvvm`, `System.Text.Json`, `StyleCop.Analyzers`, `Microsoft.Win32.TaskScheduler`, IPC (`System.IO.Pipes` o `Grpc.*`), `ZXing.Net` (T24); tests: `xunit`, `Moq`/`NSubstitute`, `FluentAssertions`, `RichardSzalay.MockHttp`, SQLite in-memory, `coverlet.collector`, `Appium.WebDriver`.
3. `Directory.Build.props`: `<Nullable>enable</Nullable>`, `LangVersion=latest`, `AnalysisLevel=latest-recommended`, `TreatWarningsAsErrors` en CI; target Windows `10.0.19041.0`.
4. Instalador (WiX o MSIX con extensión de servicio) que registra el servicio con tipo `auto`, acciones de recuperación (`sc failure`) y ACLs de §Reglas/arquitectura; proyecto MSIX para `App.UI` (identidad WNS). Firma Authenticode EV en CI de release.
5. `.editorconfig` + StyleCop verdes con el código inicial.
**Restr:** CPM (no versiones dispersas); `Domain` sin dependencias Windows; ningún `.pfx`/secreto en el repo.
**Test:** `dotnet build`/`test`/`format`/`publish -c Release` y build MSIX firmado, todos verdes; un test trivial con dependencia inyectada por DI.
**Done:** ☐ 5 proyectos compilan · ☐ CPM en última estable · ☐ debug+release(AOT/R2R) · ☐ MSIX/MSI firmable · ☐ DoD-G.

## T37 — Cuenta estándar del menor + endurecimiento *(cimiento; ejecutar temprano)*
**Obj:** establecer y vigilar el modelo de cuentas que da la resistencia a manipulación.
**Dep:** T00
**Impl:**
1. `IPrivilegeInspector`: detectar si el usuario del menor es **estándar** o **administrador local** (pertenencia a grupos vía `WindowsIdentity`/`WindowsPrincipal` o WTS). Exponer el resultado a T12.
2. Guía/automatización (consumida por el onboarding T26): crear o convertir la cuenta del menor a **estándar**; dejar la única cuenta **administrador** al padre. Donde aplique, ejecutar con elevación del padre.
3. Endurecer instalación: ACL `Deny Write/Delete` al grupo de usuarios estándar sobre la carpeta del agente (`Program Files`), sobre `ProgramData` (DB SQLite, almacén de secretos) y sobre las claves de registro del servicio; servicio `auto` + recuperación (coordina con T10).
4. Opcional best-effort: ejecutar servicio/agente como **proceso protegido (PPL)** si la firma lo permite; documentar.
**Restr:** nunca crear puertas traseras de admin para el menor; la elevación la hace el padre.
**Test:** con menor estándar, no puede detener el servicio, cambiar la hora, escribir en la carpeta del agente ni desinstalar; con menor admin, `IPrivilegeInspector` lo reporta (alimenta DEGRADED severo en T12).
**Done:** ☐ detección de privilegio · ☐ guía/automatización a estándar · ☐ ACLs de carpeta/DB/registro · ☐ DoD-G.

## T38 — Servicio + Agente de sesión + IPC *(cimiento; prerequisito de T05/06/08/09/10/11)*
**Obj:** plomería de procesos que resuelve el aislamiento de Sesión 0.
**Dep:** T00
**Impl:**
1. **Servicio** (`UseWindowsService()`, `LocalSystem`, auto-arranque): detecta sesiones interactivas (`WTSEnumerateSessions`), obtiene el token del usuario objetivo (`WTSQueryUserToken`) y lanza el **agente** con `CreateProcessAsUser` en esa sesión; lo relanza si muere; maneja fast-user-switching, logon/logoff y lock/unlock sin huérfanos ni duplicados.
2. **IPC**: named pipe con **ACL** (solo `LocalSystem` ↔ agente del usuario objetivo) o gRPC sobre named pipe. Contrato de mensajes mínimo: `ForegroundChanged(appId)`, `ShowOverlay(reason)`, `HideOverlay`, `ShowWarning(minutes)`, `RequestStateSnapshot→StateSnapshot`, `Heartbeat`, `LockWorkstation`. **Auth mutua**: el servicio valida el SID del cliente; el agente valida que el servidor es el binario firmado del servicio.
3. Si el IPC cae: el servicio sigue con enforcement duro (terminación de procesos) aunque no haya overlay; emitir alerta (consumida por T12).
**Restr:** servicio sin UI; agente sin decisiones; ACL estricta del pipe (no abierto a todos).
**Test:** el servicio lanza/relanza el agente en la sesión del menor; el IPC transporta mensajes; multi-sesión enruta al agente correcto; un proceso no autorizado no puede hablar por el pipe.
**Done:** ☐ servicio lanza/relanza agente por sesión · ☐ IPC con ACL+auth mutua · ☐ maneja multi-sesión/lock · ☐ DoD-G.

## T01 — Modelo de dominio + (de)serialización de la política
**Obj:** tipos C# puros que representan y (de)serializan el JSON de política.
**Dep:** T00
**Contexto (contrato JSON de política — autoridad de esta tarea):**
```json
{
  "device_id": "uuid", "version": 42, "device_state": "active",
  "daily_screen_time_minutes": 240,
  "schedules": [
    {"id":"bedtime","days":["MON","TUE","WED","THU","SUN"],"from":"22:00","to":"07:00","action":"lock"},
    {"id":"homework","days":["MON","TUE","WED","THU","FRI"],"from":"16:00","to":"18:00","action":"allow_only","allow_list":["msedge"]}
  ],
  "category_limits": [{"category":"games","minutes":60}],
  "app_policies": [
    {"package_name":"whatsapp","state":"always_allowed"},
    {"package_name":"instagram","state":"limited","daily_limit_minutes":30,"category":"social",
     "allowed_windows":[{"days":["MON","TUE","WED","THU","FRI"],"from":"15:00","to":"20:00"}]},
    {"package_name":"clashroyale","state":"blocked","category":"games"}
  ],
  "category_assignments": {"instagram":"social","clashroyale":"games"},
  "grants": [
    {"id":"g1","request_id":"tr_77","scope":"device","minutes":30,
     "granted_at":"2026-05-25T20:30:00Z","expires_at":"2026-05-25T21:00:00Z","source":"extra_time"}
  ]
}
```
Semántica (vinculante): `device_state ∈ active|locked|downtime`. `app_policy.state ∈ allowed|blocked|limited|always_allowed` (`limited`⇒ `daily_limit_minutes` requerido). `allowed_windows` opcional: si presente y no vacía, la app solo se permite dentro de alguna ventana. `schedule.action ∈ lock|allow_only` (`allow_only` requiere `allow_list` no vacía). `category_assignments`: mapa `package_name→category`; app sin entrada no cuenta para límites de categoría. `grant`: ventana de pared (`granted_at→expires_at`, evaluada con hora de servidor); `scope ∈ device|<package_name>|<category>`; `source ∈ extra_time|reward|manual`; `request_id` único = idempotencia. Horas `HH:mm`; `from>to` = cruza medianoche. `package_name` = `AppId` canónico (§Reglas).
**Impl:**
1. `record Policy`, `Schedule`, `Window` (reutilizable por `schedule` y `allowed_windows`), `CategoryLimit`, `AppPolicy`, `Grant`; `enum` para los 4 conjuntos de valores anteriores.
2. `[JsonPropertyName]` snake_case exacto (incl. `allowed_windows`, `category_assignments`, `granted_at`, `request_id`); `JsonSerializerContext` source-gen.
3. Validación de invariantes: `limited`⇒`daily_limit_minutes!=null`; `allow_only`⇒`allow_list` no vacía; `expires_at>granted_at`; `HH:mm` válido.
**Restr:** proyecto `Domain`, 0 dependencias Windows.
**Test:** round-trip del JSON anterior sin pérdida; políticas inválidas rechazadas con error claro.
**Done:** ☐ deserializa el ejemplo exacto · ☐ valida invariantes · ☐ source-gen JSON · ☐ DoD-G.

## T02 — Motor de reglas (precedencia determinista) — el núcleo
**Obj:** función pura `Evaluar(...) → Decision (PERMITIR | BLOQUEAR(motivo))`.
**Dep:** T01
**Contexto (algoritmo — autoridad de esta tarea). Por cada cambio de app en primer plano, evaluar en orden; la 1ª coincidencia decide:**
```
 1. ¿App del agente o sistema crítico?                            → PERMITIR  (lista blanca dura: agente, winlogon, LogonUI, csrss, consent/UAC, accesibilidad del SO, explorer-como-shell)
 2. ¿device_state == locked?                                      → BLOQUEAR  (total; ni always_allowed ni grants lo levantan)
 3. ¿app_policy.state == blocked?                                 → BLOQUEAR  (dura)
 4. ¿schedule 'allow_only' activo y app NO en allow_list?         → BLOQUEAR  (dura)
 5. ¿app tiene allowed_windows y AHORA fuera de todas?            → BLOQUEAR  (dura)
 6. ¿grant vigente que cubre el scope (device|<pkg>|<cat>)?       → PERMITIR  (levanta 7–11)
 7. ¿device_state == downtime y app NO always_allowed?            → BLOQUEAR
 8. ¿dentro de schedule 'lock' y NO always_allowed?               → BLOQUEAR
 9. ¿excedió daily_limit_minutes de la app y NO always_allowed?   → BLOQUEAR
10. ¿excedió límite de su categoría y NO always_allowed?          → BLOQUEAR
11. ¿excedió daily_screen_time global (app no exenta)?            → BLOQUEAR
12. resto                                                         → PERMITIR
```
Reglas: grant (6) cubre si `scope==device` o `==pkg` o `==category_assignments[pkg]`, vigente ⇔ `granted_at≤now_servidor<expires_at`; **solo levanta 7–11, nunca 2–5**. `always_allowed` ignora 7–11 pero **no** el paso 2. Categoría (10) = suma del uso de hoy de los pkgs con esa categoría. Cada BLOQUEAR devuelve `motivo` legible (tabla de motivos; copy real lo provee T25): p.ej. 2→"dispositivo bloqueado", 7/8→"hora de dormir", 9→"se acabó el tiempo de esta app", 10→"se acabó el tiempo de juegos", 11→"se acabó el tiempo de hoy". Determinismo: la hora y la zona entran por parámetro.
**Impl:**
1. `Evaluar(policy, appId, usage, now, zonaHoraria) → Decision`; `usage` ya trae uso por app + agregados por categoría/global (los deriva T03; el motor no recalcula categorías).
2. Implementar los 12 pasos en orden, comentando cada paso con su número.
**Restr:** proyecto `Domain`, 0 dependencias Windows; sin estado oculto ni reloj global.
**Test (suite de bordes, exhaustiva):** una prueba por paso y por conflicto — "solo Edge durante allow_only de tareas"; "2 h de juegos pero downtime"; "grant `device` levanta el global pero NO desbloquea `blocked` ni `allow_only`"; "`always_allowed` ignora downtime/límites pero no `locked`"; cruce de medianoche en schedule/window/grant; cambio de día por fecha de servidor; grant vencido al filo; cambio de zona; paso 1 nunca bloquea winlogon/explorer/agente.
**Done:** ☐ 12 pasos + combinaciones · ☐ grant solo 7–11 · ☐ bordes verdes · ☐ determinista · ☐ cada bloqueo con motivo · ☐ DoD-G.

## T04 — Servicio de tiempo confiable
**Obj:** fuente de tiempo/zona robusta para evaluar ventanas y fechar el uso.
**Dep:** T00
**Impl:**
1. `ITimeProvider` inyectable y faleable: hora monotónica (`Environment.TickCount64`/`Stopwatch.GetTimestamp`) + hora de pared (`DateTimeOffset.UtcNow`).
2. Resolver/observar zona (`TimeZoneInfo.Local`; señal en `Microsoft.Win32.SystemEvents.TimeZoneChanged`) → expone evento para T13.
3. "Fecha de servidor" inyectable (la rellena T18); fallback a hora local marcando incertidumbre.
4. Detectar saltos de reloj (inconsistencia monotónica↔pared; `SystemEvents.TimeChanged`) y distinguirlos de cambios de zona (mismo instante, distinta zona); emitir banderas para T13.
**Restr:** la lógica de tiempo vive en `Domain` (.NET puro, faleable); el observador de eventos del SO se inyecta desde el servicio.
**Test (faleando `ITimeProvider`):** cruces de medianoche, cambio de día, salto de reloj y cambio de zona detectados.
**Done:** ☐ interfaz faleable · ☐ detecta salto y cambio de zona · ☐ lógica pura · ☐ DoD-G.

## T03 — Persistencia local (EF Core + SQLite, fuente de verdad offline)
**Obj:** caché de política, uso del día y outbox de subida.
**Dep:** T00, T01
**Impl:**
1. `DbContext` + entidades: política vigente (`version`, `category_assignments`), `app_policies` (con `allowed_windows`), `grants` (`granted_at`/`expires_at`), `usage_today` clave `(AppId, server_date)`, y `outbox(tipo, payload_json, dedup_key, intentos, created_at)` para `usage_logs`/`device_alerts`/`behavioral_events`/`time_requests`/`heartbeat`.
2. Exponer política y uso como `IObservable<T>`/`IAsyncEnumerable<T>`.
3. **Guard de versión atómico:** upsert en transacción que aplica **solo si `nuevaVersion>versionLocal`** (descarta downgrades bajo carrera).
4. **Agregados para el motor:** uso por app + sumado por categoría (vía `category_assignments`) + global (excluyendo `always_allowed`). Único lugar que conoce app→categoría.
5. `usage_today` por **fecha de servidor** (T04); rollover al cambiar la fecha (no borra histórico).
6. DB en `ProgramData` con ACL del servicio (no en el perfil del menor); migraciones EF versionadas.
**Restr:** EF Core+SQLite; async + reactivo.
**Test (SQLite in-memory):** persistir/leer política; acumular uso; agregados categoría/global correctos; outbox encola/drena; downgrade de versión no sobrescribe; cambiar fecha de servidor cambia "hoy".
**Done:** ☐ guard de versión atómico · ☐ agregados correctos · ☐ `usage_today` por fecha de servidor con rollover · ☐ outbox · ☐ migraciones aplican · ☐ DB con ACL · ☐ DoD-G.

## T05 — Watcher de app en primer plano (en el agente de sesión)
**Obj:** detectar la app en primer plano y emitir su `AppId` al servicio por IPC.
**Dep:** T00, T38
**Impl:**
1. En el **agente de sesión** (con bucle de mensajes): `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, …, WINEVENT_OUTOFCONTEXT)`; complemento UI Automation `FocusChanged`.
2. Al cambiar: `GetForegroundWindow`→`GetWindowThreadProcessId`→`QueryFullProcessImageName`; implementar `ResolveAppIdentity(path)→AppId` (AUMID para MSIX; `exe`+publisher Authenticode, fallback hash, para Win32). Esta función la reusan T06/T11.
3. Filtrar ruido (shell, conmutador de tareas, overlay propio). **No** leer títulos con datos personales ni contenido.
4. Emitir `ForegroundChanged(appId)` al servicio por IPC (T38).
**Restr:** vive en el agente, nunca en el servicio (Sesión 0); sin contenido de ventana.
**Test (3 versiones de Windows):** abrir 2–3 apps (MSIX y Win32) → el servicio recibe el `AppId` correcto en orden; no se registran títulos.
**Done:** ☐ detecta cambios fiable en 3 versiones · ☐ `ResolveAppIdentity` estable · ☐ sin contenido · ☐ emite por IPC · ☐ DoD-G.

## T06 — Contador de tiempo en vivo (en el servicio)
**Obj:** acumular el tiempo de la app en primer plano; fuente primaria de tiempo.
**Dep:** T03, T04, T05, T38
**Impl:**
1. En el **servicio**, suscribir `ForegroundChanged` (IPC). Al cambiar, marcar `t0`; tick cada 5–10 s (constante nombrada) → `usage_today[AppId]+=delta` en SQLite con fecha de servidor (T04).
2. Calcular minutos restantes por scope; al cruzar 10 y 5 min, ordenar al agente `ShowWarning(minutes)` (toast que llega aun con la UI minimizada).
3. Pausar el conteo cuando no hay sesión interactiva activa del menor (sesión bloqueada, fast-user-switching, suspensión): usar eventos de sesión WTS.
4. Al iniciar, pedir backfill a T07.
**Restr:** el conteo vive en el servicio (sobrevive al logout del menor); avisos in-process (timers/`async`), **no** tareas programadas exactas para los avisos.
**Test (tick acelerado):** acumulado ±1 tick vs real; avisos 10/5 min con UI minimizada; sobrevive a muerte del agente (el servicio sigue) y pausa en sesión bloqueada.
**Done:** ☑ conteo exacto en el servicio · ☑ avisos sin UI visible · ☑ pausa en lock/switch · ☑ DoD-G (161 tests passing, 24 nuevos para T06).

## T07 — Reconciliación de uso (ETW/WMI; no hay UsageStatsManager)
**Obj:** backfill/cross-check del uso del día cuando el contador no estuvo activo.
**Dep:** T03, T04
**Impl:**
1. `IUsageReconciler`: Windows **no** expone API pública de screen-time. Reconstruir uso desde eventos de proceso — WMI `Win32_ProcessStartTrace`/`StopTrace` (`ManagementEventWatcher`) o ETW (`TraceEvent`) — correlacionados con los periodos de foco registrados por T05/T06.
2. Al arrancar y periódicamente, conciliar con `usage_today` tomando el máximo razonable; registrar discrepancias. **Idempotente** (re-ejecutar no duplica).
3. Si faltan permisos/datos, no romper: degradación parcial (alimenta T12) y confiar en T06 como fuente primaria.
**Restr:** fuente de eventos detrás de interfaz; idempotente; sin contenido del menor.
**Test:** tras simular arranque/parada de procesos, backfill coherente; re-ejecutar no duplica; sin permisos no crashea.
**Done:** ☐ backfill desde eventos de proceso · ☐ idempotente · ☐ degrada limpio · ☐ DoD-G.

## T08 — Overlay de bloqueo (motivo + CTA) — en el agente de sesión
**Obj:** pantalla de bloqueo sin salida, con motivo y CTA "Pedir permiso".
**Dep:** T00, T38
**Impl:**
1. Ventana WinUI **topmost** a pantalla completa cubriendo **todos los monitores**, sin barra de título, `HWND_TOPMOST` reafirmado periódicamente.
2. Bloquear entrada mientras está activa: deshabilitar Alt+Tab, tecla Win, menú contextual. **Límite a documentar:** `Ctrl+Alt+Supr` (SAS) no se intercepta desde modo usuario; se mitiga por directiva en MANAGED (T31), no por la ventana.
3. Mostrar/ocultar por orden del servicio (`ShowOverlay(reason)`/`HideOverlay`, IPC). Renderizar el `motivo` recibido (lo produce el motor T02) con copy de T25.
4. CTA "Pedir permiso" → dispara el flujo de T28.
**Restr:** sin botón de salida para el menor; copy desde T25; la decisión de mostrar la toma el servicio.
**Test (3 versiones, multi-monitor):** aparece sobre apps de terceros; siempre muestra motivo; el CTA dispara el callback; documentado el límite SAS.
**Done:** ☐ aparece sobre terceros (multi-monitor) · ☐ bloquea atajos comunes · ☐ siempre muestra motivo · ☐ sin salida · ☐ CTA conectado · ☐ DoD-G.

## T09 — Bloqueo total (LockWorkStation + estado locked)
**Obj:** bloqueo total del equipo, manual o programado, en modo STANDARD.
**Dep:** T08, T38
**Impl:**
1. `LockWorkStation()` invocado **desde el agente** (sesión interactiva) por orden del servicio (IPC `LockWorkstation`); alternativa `WTSDisconnectSession` desde el servicio.
2. Exponer `BloquearAhora()` en el servicio (delega al agente).
3. Si `device_state==locked`, al desbloquear reaparece el overlay persistente: el servicio reordena `ShowOverlay` en el evento WTS de desbloqueo (coordina con T08).
**Restr:** nunca matar `winlogon`/`LogonUI`; no dejar el equipo inaccesible al padre.
**Test:** orden de bloqueo → estación bloqueada; con `locked`, el overlay vuelve al desbloquear.
**Done:** ☐ bloqueo total desde el agente · ☐ overlay persistente al desbloquear · ☐ no rompe el login · ☐ DoD-G.

## T10 — Persistencia tras reinicio y muerte de proceso (SCM)
**Obj:** reactivar el agente tras boot, actualización o muerte de procesos.
**Dep:** T06, T37, T38
**Impl:**
1. Servicio con inicio **auto** (o delayed-auto) en el SCM + acciones de recuperación (`sc failure`): reinicio con backoff en cada fallo.
2. El servicio detecta sesiones (WTS) y lanza/relanza el agente (`CreateProcessAsUser`); tarea de Task Scheduler "al iniciar sesión" como respaldo.
3. Al arrancar: reconciliar uso (T07), encolar sync inicial (T18), reverificar cimientos (T37) y delegar salud a T12.
4. Configurar arranque del servicio en **Modo seguro** (clave `SafeBoot`) para resistencia básica.
**Restr:** sin servicios/agentes duplicados; no depender de que el menor inicie nada.
**Test:** reiniciar → servicio activo + agente en la sesión + uso reconstruido; matar agente → relanza; matar servicio (admin) → el SCM lo recupera.
**Done:** ☐ auto-arranque + recuperación · ☐ agente relanzado por sesión · ☐ reconcilia uso · ☐ sin duplicados · ☐ DoD-G.

## T11 — Enforcement STANDARD integrado (rebanada E2E offline)
**Obj:** cablear T02+T03+T05+T06+T08+T09 en un bucle que funciona **sin red** con política mock en SQLite.
**Dep:** T02, T03, T05, T06, T08, T09, T37, T38
**Impl:**
1. `ForegroundChanged` (IPC) → el **servicio** evalúa con el motor (T02) sobre SQLite (T03) → decisión.
2. BLOQUEAR ⇒ ordenar `ShowOverlay(motivo)` al agente (T08) **y** aplicar bloqueo duro: `TerminateProcess`/Job Object de la app y devolver foco al escritorio; nunca tocar la lista blanca de sistema crítico (motor paso 1: agente, winlogon, csrss, **explorer**, LogonUI, consent/UAC, accesibilidad). Si `device_state==locked` ⇒ bloqueo total (T09).
3. PERMITIR ⇒ el contador (T06) marca el foreground; ocultar overlay si estaba.
4. Reevaluar cuando `usage_today` cruza un umbral.
**Restr:** todo el bucle funciona offline; la terminación jamás toca procesos de sistema crítico ni el shell.
**Test (política mock que ejerza cada regla, sin red):** bloquea `blocked` (overlay+termina proceso); respeta `always_allowed`; aplica `daily_limit` y `downtime`; nunca mata winlogon/explorer/agente.
**Done:** ☐ bloquea/permite/limita según mock · ☐ offline · ☐ termina la app correcta sin tocar sistema · ☐ reevalúa al exceder · ☐ DoD-G.

## T12 — Vigilante de salud + enforcementLevel + estados de error
**Obj:** calcular `enforcementLevel ∈ {MANAGED, STANDARD, DEGRADED}` y manejar cada estado de error.
**Dep:** T03, T05, T08, T37
**Impl:**
1. Verificar: servicio corriendo (SCM/heartbeat IPC), agente vivo, hook emitiendo, **privilegio del menor** (T37), **edición** de Windows, presencia de WDAC/AppLocker/MDM. Tarea periódica (T20) + chequeo en arranque.
2. Derivar nivel: **MANAGED** = capa preventiva (WDAC/AppLocker/MDM) + menor estándar + endurecimiento; **STANDARD** = menor estándar + servicio sano sin capa preventiva; **DEGRADED** = falta un cimiento clave.
3. Tabla de estados → respuesta (alerta + pantalla "reparar" de T30):

   | Situación | Detección | Respuesta |
   |---|---|---|
   | Servicio detenido | SCM `STOPPED` / sin heartbeat IPC | DEGRADED + alerta; SCM recupera |
   | Agente muerto | sesión activa sin agente | relanzar (T38); alertar si reincide |
   | Menor es admin | grupos (T37) | DEGRADED severo + guía a estándar |
   | Hook sin eventos | timeout de `ForegroundChanged` | DEGRADED + re-enganche |
   | Sin red prolongada | heartbeat fallido N veces | última política cacheada + alerta |
   | Reloj/zona manipulados | banderas de T04 | recalcular con hora de servidor + sospecha |
4. Encolar la alerta en la outbox (T03) **sin fingir que protege**; reportar el `enforcement` efectivo en el heartbeat (T20).
**Test:** detener servicio ⇒ DEGRADED+alerta; menor admin ⇒ DEGRADED severo; restaurar ⇒ STANDARD/MANAGED según capas.
**Done:** ☐ nivel correcto (edición+privilegio) · ☐ alerta al degradar · ☐ no oculta el estado real · ☐ DoD-G.

## T13 — Anti-tamper + anti-evasión de reloj/zona
**Obj:** defensa best-effort y anti-evasión de tiempo.
**Dep:** T04, T12, T37
**Impl:**
1. Detectar intentos de: detener el servicio, matar el agente, abrir el desinstalador, modificar ACLs/registro del agente → alerta (muchos los **bloquea** T37 si el menor es estándar; igual alertar el intento).
2. Saltos de reloj y cambios de zona (banderas T04) → recalcular ventanas con hora/fecha de servidor; marcar sospecha. Nota: un menor estándar no puede cambiar la hora; si ocurre, es señal de privilegio/manipulación.
3. Consumo clavado por fecha de servidor (T03): cambiar la hora no regala tiempo.
4. Encolar eventos en la outbox (T03) — la subida la formaliza T32. Nombres: `service_stop_attempt`, `agent_kill_detected`, `uninstall_attempt`, `clock_tamper_suspected`, `timezone_changed`, `child_is_admin_detected`.
**Restr:** eventos sin contenido del menor.
**Test:** cambiar hora/zona (admin de prueba) no resetea `usage_today` y genera señal; intentar detener el servicio alerta; menor admin se detecta.
**Done:** ☐ reloj/zona no regalan tiempo · ☐ alertas emitidas · ☐ eventos en outbox · ☐ DoD-G.

## T14 — Contrato de backend consumido + delta de push WNS
**Obj:** documentar el contrato que el agente **consume** y el único cambio de backend que Windows requiere. *Este cliente NO construye el backend.* El esquema, RLS, *Custom Access Token Hook* del claim `device_id`, `bump_policy_version`, la RPC `get_device_policy`, las plantillas de política, la retención y las Edge Functions (emparejamiento, política inicial, aprobación de tiempo extra, recompensa, heartbeat, integridad, fan-out) **ya existen** (compartidos con el cliente Android). La responsabilidad del agente es invocarlos correctamente y obedecer.
**Dep:** —
**Contrato consumido (provisto por el backend; el agente solo lo llama):**
- **Auth:** sesión anónima de Supabase Auth; el claim `device_id` lo inyecta el hook existente tras el emparejamiento. Cliente con **publishable key** (nunca `service_role`); la RLS ya restringe cada dispositivo a sus propias filas (`(auth.jwt()->>'device_id')::uuid = device_id`).
- **Pull de política:** RPC **`get_device_policy(device_id)`** → JSON con la **forma exacta de T01** (política vigente + `app_policies` con `allowed_windows` + `category_assignments` + `grants` con `expires_at>now()`). Cualquier cambio sube `device_policies.version`; el agente aplica solo si `version>local`.
- **Emparejamiento:** Edge Function que valida un `pairing_codes` (uso único + TTL) y deja `device_id` en `app_metadata` (la invoca T24).
 - **Escrituras del agente (tablas/RPC ya existentes, idempotentes por id/`dedup_key`):** tablas `usage_logs`, `device_alerts`, `behavioral_events`, `time_requests`; RPC **heartbeat** (`enforcement`, `battery_pct` null si escritorio, `clock_offset_ms`); endpoint de **registro de token de push**; endpoint de **reporte de integridad** (verifica firma/hash, lo alimenta T23). El endpoint `POST /rest/v1/integrity_reports` retorna `{ "verdict": "trust"|"revoked"|"unknown" }` en el response body, permitiendo que T23 reaccione al resultado de la verificación.
- **Solicitudes:** el agente crea `time_requests`; la **aprobación la hace el tutor en su app (fuera de alcance)**; el backend crea el `grant` y sube versión; el agente lo recibe vía sync.
**Delta REQUERIDO por Windows (dependencia a coordinar con el equipo de backend; no es código de este cliente):**
1. `device_push_tokens` acepta `channel='wns'` con `push_handle` = Channel URI de WNS (caduca ~30 días → campo `expires_at` para renovación).
2. **Fan-out:** si `channel='wns'`, el backend hace POST autenticado a **WNS** (Package SID + secret de Partner Center) una raw notification "sync ahora" (en lugar de FCM); maneja Channel URI caducado.
**Restr:** publishable key en cliente; el agente nunca asume `service_role` ni replica lógica de negocio del backend (generación de política, aprobaciones, topes de recompensa) — **solo la consume**.
**Test (contra backend de prueba):** `get_device_policy` devuelve JSON válido por T01; el agente solo ve sus filas (RLS); un `time_requests` aprobado produce un `grant` + sube versión + dispara push WNS (mock); un Channel URI caducado se detecta y renueva.
**Done:** ☐ contrato consumido ejercido contra backend de prueba · ☐ delta WNS (canal + fan-out) acordado y verificado · ☐ publishable key sin `service_role` · ☐ sin replicar lógica de negocio · ☐ DoD-G.

## T16 — Almacenamiento seguro de secretos
**Obj:** guardar el token de dispositivo y el secreto de emparejamiento cifrados.
**Dep:** T00
**Impl:**
1. `ISecretStore`: cifrar con **DPAPI** (`ProtectedData`, scope **máquina** + entropía, para que `LocalSystem` lo lea) o `DataProtectionProvider` (`LOCAL=machine`/SID). Donde haya **TPM**, proteger la clave con CNG (NCrypt + KSP del TPM).
2. Persistir en `ProgramData` con ACL del servicio (T37), nunca en el perfil del menor.
3. API `async`; excluir del roaming.
4. "Blob corrupto/clave no recuperable": regeneración controlada (forzar re-emparejamiento) sin crashear.
**Restr:** DPAPI/`DataProtectionProvider` (+TPM si existe); nada en claro ni en `appsettings` versionado.
**Test:** escribir/leer persiste cifrado y solo lo lee el servicio; inspeccionar el archivo no revela el valor; sin TPM cae al fallback DPAPI sin romper.
**Done:** ☐ lectura/escritura cifradas y async · ☐ ligado a máquina/TPM · ☐ ACL del servicio · ☐ sin roaming · ☐ DoD-G.

## T17 — Autenticación de dispositivo
**Obj:** sesión del agente contra Supabase con claim `device_id` (respeta RLS).
**Dep:** T14, T16
**Impl:**
1. Abrir **sesión anónima** con `supabase-csharp` (gotrue) → crea el `auth.users` del dispositivo. Tras emparejar (T24), `device_id` queda en `app_metadata` y el hook lo inyecta como claim. Persistir sesión (access+refresh) cifrada con T16.
2. Refresh automático; ante anomalía (veredicto de integridad negativo de T23), forzar re-emparejamiento o rotación.
3. Cliente Supabase con **publishable key** + esta sesión; todas las llamadas REST/Realtime la usan, desde el servicio.
**Restr:** publishable key (no anon/service_role).
**Test (backend de prueba):** abre sesión, el token incluye el claim `device_id`, refresca al expirar y solo accede a sus filas.
**Done:** ☐ sesión anónima persistente/refrescable · ☐ claim presente · ☐ rotación ante anomalía · ☐ respeta RLS · ☐ DoD-G.

## T18 — Sincronización REST (offline-first)
**Obj:** traer la política nueva y subir datos, con SQLite como verdad.
**Dep:** T03, T17
**Impl:**
1. **Pull:** llamar `get_device_policy` (JSON de T01) → aplicar solo si `version>local` con el guard de T03 → el motor reevalúa.
2. **Push:** drenar la outbox (T03) mapeando cada `tipo` a su tabla (`usage_logs`/`device_alerts`/`behavioral_events`/`time_requests`; `heartbeat` a la RPC de heartbeat del backend (T14)). Idempotencia por `dedup_key`/ids.
3. Renovar el Channel URI de WNS si caducó (coordina con T19).
4. Rellenar la "fecha de servidor" de T04 desde la respuesta.
5. **Nunca** bloquear el enforcement por red: si falla, seguir con la política cacheada.
**Restr:** `postgrest-csharp`; pruebas con `HttpClient` **mockeado** (RichardSzalay.MockHttp), no servidor real.
**Test (mock):** aplica `v+1` y descarta `v-1`; drena outbox y reintenta sin duplicar (mismo `dedup_key`); 500/timeout/JSON malformado; offline → el motor sigue con la última política.
**Done:** ☐ pull usa `get_device_policy` · ☐ versionado+idempotencia · ☐ outbox con reintentos · ☐ heartbeat enviado · ☐ offline-tolerante · ☐ DoD-G.

## T19 — Push WNS "sync ahora" + sondeo de respaldo
**Obj:** recibir el push y registrar/renovar el Channel URI; mantener sondeo de respaldo.
**Dep:** T18
**Impl:**
1. Solicitar el Channel URI (`PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync`, requiere identidad MSIX) → upsert vía el endpoint de registro de token del backend (T14); encolar en outbox si no hay red. **Renovar** antes de caducar (~30 días).
2. Al recibir una raw notification "sync ahora", el componente con identidad avisa al servicio por IPC → el servicio hace `GET` autenticado (T18). No confía en el payload.
3. **Sondeo de respaldo en el servicio:** como Windows no tiene Doze, sondear `get_device_policy` en intervalo corto (constante, ej. 30–60 s) y al arrancar. El push solo reduce latencia.
**Restr:** nunca aplicar el payload; el sondeo nunca sustituye el guard de versión.
**Test:** push de prueba → pull y aplica en segundos; sin push, el sondeo aplica en el intervalo; Channel URI caducado se renueva.
**Done:** ☑ Channel URI registrado/renovado · ☑ push dispara sync sin confiar en payload · ☑ sondeo de respaldo · ☑ DoD-G.

## T20 — Trabajo programado (timers del servicio + Task Scheduler)
**Obj:** heartbeat, subida de outbox, reconciliación, reintentos.
**Dep:** T07, T18
**Impl:**
1. Heartbeat periódico (RPC del backend, T14) con `enforcement` (T12), `battery_pct` (o null) y `clock_offset_ms` (T13).
2. Subida periódica de la outbox.
3. Reconciliación de uso (T07).
4. Backoff exponencial + chequeo de conectividad (`NetworkInformation`).
5. Implementar como **timers dentro del servicio** (continuo) + tareas de Task Scheduler (al iniciar, al logon, recuperación) como red de seguridad; encadenar con T10.
**Restr:** los trabajos viven en el servicio (no dependen del login del menor); inyección por DI.
**Test:** cada trabajo corre, reintenta con backoff, respeta conectividad; tareas de respaldo registradas; DI funciona.
**Done:** ☑ heartbeat/subida/reconciliación · ☑ reintentos con backoff · ☑ timers + tareas de respaldo · ☑ DoD-G.

## T21 — Realtime solo en primer plano (UI)
**Obj:** refrescar la UI al instante sin canal de control en background.
**Dep:** T17
**Impl:**
1. Suscribir cambios de política/grants con `realtime-csharp` **solo** mientras la UI está en primer plano (atado al ciclo de vida de la ventana).
2. Al ir a background/cerrar, cerrar el socket (el control va por push/sondeo del servicio).
3. Refrescar la pantalla de estado (T27) y el resultado de solicitudes (T28).
**Restr:** el **servicio** no abre Realtime; solo la UI. Nunca como canal de control.
**Test:** con UI en primer plano, un cambio en backend la refresca; al cerrarla, el socket se cierra.
**Done:** ☑ Realtime solo en foreground UI · ☑ cierra socket al background · ☑ refresca UI relevante · ☑ DoD-G (81.7% coverage RealtimeSubscriber, 8/8 tests passed).

## T22 — TLS 1.3 + certificate pinning
**Obj:** asegurar el canal contra el backend.
**Dep:** T17
**Impl:**
1. Forzar TLS 1.3 en `SocketsHttpHandler` (`SslClientAuthenticationOptions.EnabledSslProtocols = SslProtocols.Tls13`).
2. Pinning del dominio del backend vía `RemoteCertificateValidationCallback` contra pines conocidos; plan de rotación documentado.
3. Aplicarlo al `HttpClient` que usa `supabase-csharp`.
**Test:** pin correcto OK; pin alterado falla (MITM bloqueado).
**Done:** ☑ TLS 1.3 · ☑ pinning con rotación documentada · ☑ MITM rechazado · ☑ DoD-G (10/10 CertificatePinningValidatorTests passed, 415 Service + 8 App.UI full suite passing).

## T23 — Integridad del agente + ofuscación
**Obj:** verificar integridad y dificultar el reempaquetado/manipulación.
**Dep:** T00, T14, T17
**Impl:**
1. **Auto-verificación:** el servicio verifica su firma **Authenticode** (`WinVerifyTrust`) y el hash de sus binarios; reporta al endpoint de integridad del backend (T14) (no confiar solo en el cliente).
2. Opcional MANAGED: atestación TPM / Device Health Attestation.
 3. Reaccionar a veredicto negativo (alerta + degradación) sin romper ante falsos positivos. El veredicto se obtiene del response body del `POST /rest/v1/integrity_reports` (campo `verdict: "trust"|"revoked"|"unknown"`). Mecanismos implementados:
   - **Timeout graceful:** falla de red no causa degradación; se contabiliza para el circuit breaker.
   - **Count threshold:** se necesitan 3 veredictos `"revoked"` consecutivos antes de escalar.
   - **Hysteresis:** una vez degradado, se requieren 3 `"trust"` consecutivos para recuperar (anti-flapping).
   - **Escalation before DEGRADED:** antes de degradar, se notifica al admin via outbox y se espera 5 minutos para que pueda hacer override.
   - **Grace period startup:** durante los primeros 5 minutos después del inicio del servicio, se skippean los checks de integridad.
   - **Staged response:** escala gradualmente WARN → LIMIT → DEGRADED según cantidad de `"revoked"` consecutivos.
   - **Shadow mode:** inicia en shadow mode; loguea qué haría pero no toma acción hasta que se llame `DisableShadowMode()`.
   - **Circuit breaker:** si el backend falla 5 veces consecutivas, se abre el circuito por 15 minutos; mientras está abierto, se skippea toda verificación.
   Si `verdict == "revoked"` y se cumplen todas las condiciones (3 consecutivos, no es grace period, no es shadow mode, circuito cerrado): notificar admin, esperar 5 min, luego EnforcementLevel → DEGRADED via el pipeline existente de T12.
4. Publicación con **NativeAOT/R2R** + ofuscador IL del dominio/motor; preservar serialización source-gen, EF y la reflexión necesaria; anti-debug básico (no como única defensa).
**Test:** build firmada → veredicto válido server-side; build alterada/sin firma → detectada; la versión AOT/ofuscada sigue (de)serializando y consultando la DB.
**Done:** ☑ firma/hash verificados en servidor · ☑ reacción sin falsos positivos catastróficos · ☑ AOT/ofuscación sin romper serialización/EF · ☑ DoD-G.

## T24 — Emparejamiento (código / QR)
**Obj:** vincular el PC del menor con la cuenta del padre.
**Dep:** T14, T16, T17
**Impl:**
1. **Entrada de código** como vía primaria (hay teclado). Opcional: QR por webcam (`MediaCapture` + `ZXing.Net`) — **implementar en futura versión**.
2. Tras abrir/recuperar la sesión anónima (T17), llamar a la Edge Function de emparejamiento del backend y guardar la sesión con T16.
3. Estados claros: éxito, **código inválido/vencido/usado** (uso único + TTL ~10 min), con opción de pedir uno nuevo.
**Contrato backend** (`POST /functions/v1/pairing`):
- Request: `{ code, device_name, device_model, os_version, app_version, age_band }`
- Response: `{ success, device_id, parent_id, policy_version }`
- Errors: `404` = inválido, `410` = expirado, `429` = rate limit, `5xx` = retry con backoff
**Age band:** obligatorio, valores `"7-12"` | `"13-16"` | `"17-18"`.
**Retry:** 3 intentos con backoff exponencial (1s, 2s, 4s) para errores de red/servidor. No retry para 404/410/429.
**Test:** código válido empareja y persiste sesión; inválido/vencido/usado muestra el error correcto; QR por webcam funciona donde hay cámara.
**Done:** ☑ empareja por código · ☑ persiste sesión · ☑ maneja errores · ☑ DoD-G.

## T25 — Divulgación + consentimiento + transparencia + copy
**Obj:** divulgación al adulto, pantalla de transparencia para el menor y copy centralizado.
**Dep:** T00
**Impl:**
1. **Divulgación prominente in-app**: dentro de la app, en uso normal, describe los datos de uso recolectados y su uso/compartición; requiere **acción afirmativa** del adulto; separada de la política de privacidad.
2. Pantalla "qué se monitorea" siempre accesible para el menor. **No** implementar modo oculto.
3. Sistema de copy: strings externalizados (`.resw`), positivos, con motivo, con variantes por edad; **origen único** de todos los textos de cara al menor (overlay T08, avisos T06/T27, alertas T30). El motor (T02) toma de aquí los `motivo`.
4. Bloquear el avance del onboarding hasta consentir.
**Restr:** sin modo oculto; copy localizable.
**Test:** no se avanza sin consentimiento afirmativo; la transparencia es accesible.
**Done:** ☐ divulgación cumplida · ☐ transparencia accesible · ☑ copy centralizado/localizable · ☑ bloquear hasta consentir · ☑ no se avanza sin consentimiento · ☑ DoD-G.
> **Nota:** items 1+2 (divulgación + transparencia UI) se completan cuando se construya App.UI en T26/T27. Lógica de consentimiento (ConsentService, ConsentDialog consola) implementada y funcionales.

## T26 — Onboarding por valor + setup con progreso
**Obj:** setup ordenado por valor, con progreso real y una primera victoria antes de los pasos caros.
**Dep:** T08, T12, T24, T25, T37
**Impl:**
1. Orden: emparejar (T24) → divulgación/consentimiento (T25) → **crear/confirmar cuenta estándar del menor** (T37, primer cimiento) → instalar/iniciar servicio (elevación del padre) → **auto-demostración** ("Probemos tu protección": muestra el overlay 2–3 s) → luego "subir el nivel" (WDAC/AppLocker/BitLocker/kiosk = MANAGED, T31).
2. Barra "Protección N de M" que refleje el estado **real** de T12 (nunca inflado): cuenta estándar ✓, servicio activo ✓, watcher emitiendo ✓, capa preventiva ✓/✗.
3. Cada paso pendiente abre la página `ms-settings:` correcta o pide elevación, y reverifica al volver.
4. Onboarding **reanudable** (estado persistido en el almacén del servicio).
5. Emitir eventos de embudo (T32): `onboarding_step_reached`, `onboarding_first_win`, `onboarding_completed`, `onboarding_abandoned`.
**Restr:** el progreso nunca miente; la elevación se pide al adulto.
**Test (usabilidad+instrumentado):** llegar a la primera victoria; reanudar tras cerrar; el progreso coincide con T12; deep links funcionan en las 3 versiones.
**Done:** ☐ primera victoria antes de pasos caros · ☐ progreso real · ☐ reanudable · ☐ eventos de embudo · ☐ DoD-G.

## T27 — Pantalla de estado del menor (límites visibles + avisos)
**Obj:** estado diario con tiempo restante, próximo downtime, avisos y acceso a "pedir tiempo extra".
**Dep:** T06, T21
**Impl:**
1. Mostrar "Quedan X min", qué está permitido ahora y el próximo bloqueo (un solo foco visual). Datos vía IPC desde el servicio.
2. Espejar los avisos de 10/5 min (la notificación la emite el servicio→agente, T06; aquí se refleja cuando está visible).
3. Botón "Pedir tiempo extra" → flujo de T28.
4. Refresco en vivo vía Realtime foreground (T21).
5. Nada configurable por el menor; textos desde T25.
**Restr:** avisos in-process; no configurable por el menor.
**Test (contador acelerado):** el restante coincide con SQLite; avisos a 10/5 min; sin corte sorpresa.
**Done:** ☐ restante y próximos cortes visibles · ☐ avisos exactos · ☐ no configurable por el menor · ☐ DoD-G.

## T28 — Bucle "Pedir tiempo extra" (feature ancla)
**Obj:** que el menor solicite tiempo y reciba respuesta rápida; al aprobarse, el motor aplica el grant.
**Dep:** T08, T14, T18, T19, T21, T27
**Impl:**
1. Desde la pantalla de estado (T27) o el overlay (T08), crear un `time_requests` (scope, minutos, motivo opcional). Offline: encolar en outbox (T03) y sincronizar al reconectar.
2. Esperar resolución vía push WNS (T19) / Realtime (T21).
3. Al aprobarse (el tutor lo hace en su app, fuera de alcance), el backend crea el `grant(source='extra_time')` idempotente y sube versión → sync (T18) → el motor lo aplica en su **paso 6** (levanta los límites de tiempo del scope; **no** desbloquea `blocked` ni `allow_only`).
4. Mostrar resultado inmediato ("Te dieron 20 min" / "Ahora no").
5. Throttle anti-spam (local + servidor).
**Test (E2E, backend de prueba):** solicitud → aprobación → grant aplicado y acceso recuperado; tolerante a offline (se aplica al reconectar por versionado); throttle activo.
**Done:** ☐ flujo completo solicitud→grant aplicado · ☐ resultado rápido · ☐ offline-tolerante · ☐ throttle activo · ☐ DoD-G.

## T29 — Banco de tiempo / recompensas
**Obj:** mostrar y aplicar "tiempo ganado" como refuerzo positivo.
**Dep:** T18, T19, T28
**Impl:**
1. Recibir grants de recompensa (regla server-side del backend, `grant.source='reward'`).
2. Mostrar el "tiempo ganado" en la pantalla de estado (T27) con confirmación positiva.
3. Respetar topes del padre (sin saldo infinito).
**Restr:** reutilizar `grants` (sin ruta paralela).
**Test (E2E):** una recompensa server-side aplica un grant; el menor ve el saldo y la confirmación.
**Done:** ☐ recompensa vía grants · ☐ saldo visible · ☐ topes respetados · ☐ DoD-G.

## T30 — Reparación de un toque desde alertas de degradación
**Obj:** convertir cada degradación (tabla de T12) en una acción de reparación de un toque.
**Dep:** T12
**Impl:**
1. Por cada causa (servicio detenido, agente caído, menor admin, hook sin eventos): pantalla/overlay con **causa concreta + CTA "Reparar"** → reiniciar el servicio (elevación del padre), relanzar el agente, abrir `ms-settings:otherusers` para revertir el admin del menor, etc. Deep links válidos por versión.
2. Copy honesto (T25), sin urgencia falsa.
3. Anti-fatiga: agrupar y limitar alertas por incidente.
4. Al recuperarse, confirmación positiva.
5. Eventos (T32): `degraded_alert_shown`, `repair_tapped`, `protection_restored`.
**Test (3 versiones):** cada tipo abre la acción correcta; tras reparar, vuelve a STANDARD/MANAGED.
**Done:** ☐ reparación de un toque por causa · ☐ deep links válidos por versión · ☐ anti-spam · ☐ eventos · ☐ DoD-G.

## T31 — Provisión MANAGED + enforcement reforzado + oferta re-temporizada
**Obj:** capa preventiva real y su oferta en el momento adecuado.
**Dep:** T11, T12, T37
**Impl:**
1. El **mismo motor (T02)** sirve a STANDARD y MANAGED; las acciones se degradan en STANDARD.
2. En MANAGED, aplicar la capa preventiva por edición (detrás de `IHardEnforcer`): **WDAC (App Control)** que solo permite el agente + apps autorizadas (universal); **AppLocker** en Ent/Edu; bloqueo de desinstalación = ACLs (T37) + WDAC; **Assigned Access/Shell Launcher** (kiosk) para bloqueo total donde la edición lo permita; **BitLocker + Secure Boot** y Modo seguro restringido por directiva.
3. Documentar el aprovisionamiento: inscripción **MDM/Intune** o paquete `.ppkg`, o configuración manual del PC dedicado por el padre.
4. **Oferta re-temporizada:** en STANDARD no exigir nada disruptivo; ofrecer "modo reforzado" como tarjeta honesta y guiar la provisión solo en equipo nuevo/dedicado o inscripción MDM.
5. Reportar `enforcement` al backend (`devices`).
**Restr:** capa preventiva detrás de interfaz por capacidad; degradar limpio si la edición no soporta AppLocker/kiosk; WDAC como base universal.
**Test:** en MANAGED, el menor no puede ejecutar apps fuera de la política ni desinstalar; en STANDARD no aparece exigencia bloqueante.
**Done:** ☐ hard enforcement (WDAC/AppLocker/kiosk/uninstall-block) por edición · ☐ mismo motor en ambos niveles · ☐ la oferta nunca bloquea STANDARD · ☐ nivel reportado · ☐ DoD-G.

## T32 — Instrumentación de eventos conductuales
**Obj:** emitir y subir el catálogo de eventos, con minimización de datos.
**Dep:** T03, T18, T20
**Impl:**
1. API de tracking interna que **encola** eventos (no bloquea el enforcement) en la outbox (T03).
2. Cablear eventos en: onboarding (T26), bucle de tiempo (T27/T28), recompensas (T29), reparación (T30), evasión (T13).
3. Cada evento con `device_id`, `event_version`, `client_ts` y `props` mínimos. **Nunca** contenido del menor.
4. Subida resiliente y por lotes (T18/T20).
**Catálogo:** `onboarding_step_reached`, `onboarding_first_win`, `onboarding_completed`, `onboarding_abandoned`, `protection_progress`, `permission_granted`, `managed_offered/adopted/declined`, `degraded_alert_shown`, `repair_tapped`, `protection_restored`, `time_warning_shown`, `limit_reached`, `block_overlay_shown`, `ask_permission_tapped`, `extra_time_requested`, `extra_time_resolved`, `reward_granted`, `reward_seen`, `service_stop_attempt`, `agent_kill_detected`, `uninstall_attempt`, `clock_tamper_suspected`, `timezone_changed`, `child_is_admin_detected`.
**Restr:** sin contenido del menor; esquema versionado (`event_version`).
**Test (E2E):** cada evento del catálogo se emite y llega a `behavioral_events`; resiliente a offline; sin datos de contenido.
**Done:** ☐ catálogo completo emitido y recibido · ☐ no afecta el enforcement · ☐ minimización · ☐ esquema versionado · ☐ DoD-G.

## T34 — Distribución y cumplimiento (Windows)
**Obj:** todo lo no-código que la distribución exige.
**Dep:** T05, T25, T31
**Impl:**
1. **Firma Authenticode EV** de todos los binarios e instaladores; verificar reputación SmartScreen (sin advertencia "editor desconocido").
2. Confirmar divulgación prominente (T25) y pantalla de transparencia.
3. Política de privacidad + descripción del monitoreo y datos; cumplir reglas de datos de menores de la jurisdicción.
4. Si se distribuye por **Microsoft Store**: cumplir Store Policies (familia/menores y monitoreo/restricción), clasificación por edad, sección de privacidad del listado, justificación del caso parental.
5. Documentar justificación del servicio `LocalSystem` y de WDAC/AppLocker/WFP si se usan.
6. Documentar aprovisionamiento MANAGED (MDM/Intune o `.ppkg`) + canal de distribución alterno (instalador EV fuera de la Store).
**Test:** revisión interna contra esta lista pasa antes de publicar; instalador firmado completo; SmartScreen sin advertencia.
**Done:** ☐ binarios/instaladores firmados (EV) · ☐ transparencia+privacidad publicadas · ☐ Store policies (si aplica) · ☐ aprovisionamiento documentado · ☐ DoD-G.

## T35 — (Opcional) Filtrado de red/DNS por WFP/Firewall
**Obj:** filtrar dominios/apps prohibidos a nivel de red, con sus límites declarados.
**Dep:** T11
**Impl:**
1. Filtrado por **WFP** en modo usuario (bloquear apps/destinos), o **NRPT** (`Add-DnsClientNrptRule`) para redirigir DNS a un resolutor filtrado, o **Firewall de Windows** (`INetFwPolicy2`) para bloqueo de salida por app. Callout driver solo si se necesita inspección profunda (alto coste/riesgo).
2. En MANAGED, fijar el resolutor DNS por directiva para que el menor estándar no lo cambie.
3. **Sin** recolectar tráfico sensible ni redirigirlo para monetizar.
**Test:** dominios/apps bloqueados no resuelven/conectan; el menor estándar no revierte el DNS bajo MANAGED.
**Done:** ☐ filtra DNS/app · ☐ resolutor fijado en MANAGED · ☐ no recolecta tráfico sensible · ☐ DoD-G.

## T36 — Estrategia de pruebas transversal + matriz de compatibilidad
**Obj:** cobertura completa, compatibilidad y CI.
**Dep:** se construye en paralelo; cierra al final.
**Impl:**
1. Unit (`Domain`): motor (T02), tiempo (T04), modelo (T01) — suite de bordes exhaustiva.
2. Repositorio/sync: SQLite in-memory + `HttpClient` mock para versionado/idempotencia.
3. Instrumentadas (WinAppDriver): overlay sobre app bloqueada (multi-monitor), `LockWorkStation`, re-arme tras boot, detener servicio → DEGRADED.
4. Anti-tamper: detener servicio, matar agente, cambiar hora/zona (admin de prueba), menor admin detectado.
5. Sesiones: fast-user-switching, lock/unlock, suspensión (conteo pausa/reanuda; push/sondeo despierta el sync).
6. **Matriz:** Win10 22H2, Win11 23H2, Win11 24H2; x64 y ARM64; Home/Pro y (si hay) Ent/Edu para WDAC vs AppLocker.
7. CI: `build`/`test`/`format`/analizadores por cada PR; build firmado y checklist de T34 antes de publicar.
**Test:** el pipeline corre toda la suite en las versiones/arquitecturas objetivo y reporta cobertura.
**Done:** ☐ unit+integración+instrumentadas verdes en 3 versiones · ☐ x64+ARM64 · ☐ sesiones y anti-tamper · ☐ HttpClient mock · ☐ checklist de T34 en CI · ☐ DoD-G.

---

# §FIN — Orden de ejecución
1. **T00** — solución, tooling, empaquetado.
2. **T37 → T38** — cimientos (cuentas + procesos/IPC). *Temprano pese al ID alto: T05/06/08/09/10/11 dependen de ellos.*
3. **T01 → T02 → T04 → T03** — núcleo offline (`Domain` puro + SQLite, todo testeable).
4. **T05 → T06 → T07 → T08 → T09 → T10 → T11** — enforcement STANDARD; T11 cierra una rebanada E2E offline jugable.
5. **T12 → T13** — salud y anti-tamper.
6. Paralelo: **T14** (contrato consumido + delta WNS) y **T16 → T17 → T18 → T19 → T20 → T21** (auth/sync).
7. **T22 → T23** — seguridad e integridad.
8. **T24 → T25 → T26 → T27 → T28 → T29 → T30 → T31** — UX y conducta (no dejar T28 al final).
9. **T32** (instrumentación), **T34** (distribución), **T35** (opcional), **T36** (cierre de pruebas).
