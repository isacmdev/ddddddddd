# Activación de perfiles Windows después de Gate 0

## Estado actual

Estos perfiles fueron creados y configurados, pero deben permanecer sin tarjetas ni coordinación activa hasta que Gate 0 esté verificado:

| Perfil | Rol | Modelo |
|---|---|---|
| `windows-technical-lead` | Liderazgo técnico/producto y coordinación del DAG | `gpt-5.6-sol` |
| `windows-runtime-implementer` | APPLY de Service, SessionAgent, IPC y enforcement | `gpt-5.6-luna` |
| `windows-device-sync-implementer` | APPLY de identidad, outbox, backend, Realtime y WNS | `gpt-5.6-luna` |

Los tres usan `openai-codex`, `xhigh` y el CWD:

```text
C:/Users/Usuario/Desktop/Proyectos/control-parental-windows
```

No se modificaron el board, el cron, el dispatcher, la concurrencia ni las tarjetas existentes.

## Condiciones previas a la activación

No activar hasta verificar todas:

1. P0.R7.3 terminó con evidencia completa.
2. P0.R7 recibió review independiente Sol y PASS.
3. Se creó únicamente el commit local autorizado conforme a los criterios vigentes.
4. Gate 0 quedó aprobado.
5. No hay workers vivos en worktrees que se reutilizarán.
6. `git status`, branches y worktrees están inventariados.
7. Los carriles posteriores tienen contratos estables y ownership exclusivo.

## Verificación de los perfiles

```bash
hermes profile describe windows-technical-lead
hermes profile describe windows-runtime-implementer
hermes profile describe windows-device-sync-implementer

hermes -p windows-technical-lead config get model
hermes -p windows-technical-lead config get terminal.cwd
hermes -p windows-technical-lead config get agent.reasoning_effort
hermes -p windows-technical-lead auth list
hermes -p windows-technical-lead doctor

hermes -p windows-runtime-implementer config get model
hermes -p windows-runtime-implementer config get terminal.cwd
hermes -p windows-runtime-implementer config get agent.reasoning_effort
hermes -p windows-runtime-implementer auth list
hermes -p windows-runtime-implementer doctor

hermes -p windows-device-sync-implementer config get model
hermes -p windows-device-sync-implementer config get terminal.cwd
hermes -p windows-device-sync-implementer config get agent.reasoning_effort
hermes -p windows-device-sync-implementer auth list
hermes -p windows-device-sync-implementer doctor
```

No es necesario iniciar gateways propios para estos perfiles; el dispatcher del gateway principal puede spawnearlos.

## Transición del liderazgo

Realizarla como una operación controlada, no mientras otro coordinador esté actuando:

1. Confirmar que el cron coordinador actual no está ejecutándose.
2. Pausarlo temporalmente.
3. Configurar el orchestrator:

```bash
hermes config set kanban.orchestrator_profile windows-technical-lead
hermes config get kanban.orchestrator_profile
```

4. Actualizar o reemplazar el cron para que use `windows-technical-lead` y el board `control-parental-windows-mvp`.
5. Ejecutar una prueba sobre una tarjeta de coordinación no crítica.
6. Verificar read-back: decisión registrada, dependencia correcta, worker único y ningún cambio de código del líder.
7. Reanudar el coordinador solo después de la prueba.

El líder puede decidir arquitectura, prioridades, routing, retry, descomposición y suficiencia de handoffs. No implementa features rutinariamente ni concede PASS a su propio trabajo.

## Preparación del primer swarm

Antes del fan-out, definir por carril:

```text
base SHA exacto
branch y worktree exclusivo
perfil APPLY
perfil VERIFY
archivos permitidos
archivos prohibidos
contratos compartidos congelados
pruebas obligatorias
formato de handoff
```

Routing inicial recomendado:

```text
Runtime/Service/SessionAgent/IPC/enforcement:
  APPLY  → windows-runtime-implementer
  VERIFY → windows-runtime-enforcement + reviewer cuando el gate lo requiera

Identidad/backend/outbox/Realtime/WNS:
  APPLY  → windows-device-sync-implementer
  VERIFY → device-sync-identity + reviewer cuando el gate lo requiera

Trabajo transversal o pequeño:
  APPLY  → coder
  VERIFY → reviewer

UI infantil Windows:
  APPLY  → coder inicialmente
  VERIFY → child-facing-product-ux y reviewer según riesgo
```

Mantener inicialmente:

```text
kanban.max_in_progress_per_profile = 1
```

Aumentar `kanban.max_in_progress` solo cuando existan al menos dos carriles realmente independientes y worktrees separados. No usar ocupación máxima como objetivo.

## Loop APPLY → VERIFY

```text
technical lead estabiliza contrato y DAG
    ↓
implementador Luna trabaja en worktree exclusivo
    ↓
handoff con SHA/diff/pruebas/riesgos
    ↓
reviewer Sol inspecciona y ejecuta evidencia
    ├── CHANGES_REQUESTED → remediación por implementador → re-review
    └── APPROVED → siguiente dependencia/gate
```

Máximo dos intentos equivalentes sobre la misma causa. Después: investigar, cambiar estrategia, descomponer o mover a triage. Nunca obtener verde debilitando seguridad, omitiendo pruebas o introduciendo bypasses.

## Cambios que siguen pospuestos

Después de Gate 0, evaluar por separado convertir estos reviewers especializados a Sol/xhigh:

```text
device-sync-identity
windows-runtime-enforcement
child-facing-product-ux
```

No modificar sus modelos o SOUL mientras tengan tarjetas existentes o puedan ser despachados por el DAG actual.

## Rollback de activación

Si la prueba del líder crea routing incorrecto o decisiones duplicadas:

1. pausar el cron nuevo;
2. restaurar `kanban.orchestrator_profile` al valor anterior;
3. no archivar ni reescribir tarjetas automáticamente;
4. inspeccionar events/runs/logs;
5. corregir SOUL/routing;
6. repetir la prueba sobre una tarjeta no crítica.

Crear los perfiles no requiere rollback: mientras estén idle no afectan el board.
