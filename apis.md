## 1. Resumen

| Categoría | Dirección | Transporte |
|-----------|-----------|------------|
| Pairing e identidad | agent → server | HTTPS (Edge Function) |
| Telemetría y eventos | agent → server | HTTPS (PostgREST) |
| Salud y operación | agent → server | HTTPS (RPC / Edge Function) |
| Push bidireccional | server → agent (WNS), agent → server (registro) | WNS |
| Live updates | server → agent | WebSocket (Supabase Realtime) |

Total: **10 endpoints/canales** entre los dos servicios.

---

## 2. Pairing e identidad

### `POST /functions/v1/pairing`

Emparejamiento — vincula la PC del hijo con la cuenta del padre.

- **Dirección:** agent → server
- **Auth:** ninguno (primer contacto)
- **Body (propuesto):**
  ```json
  {
    "code": "ABC123",
    "device_name": "PC-Hijo-Escritorio",
    "device_model": "Dell XPS 15",
    "os_version": "Windows 11 23H2",
    "app_version": "1.0.0",
    "platform": "WINDOWS_DESKTOP"
  }
  ```
- **Respuesta esperada:** `device_id`, `parent_id`, token de sesión, `policy_version` inicial.
- **Errores relevantes:** código inválido (404), código expirado (410).

---

## 3. Telemetría y eventos

### `POST /rest/v1/usage_logs`

Sube registros de uso de apps por el hijo (sesiones foreground).

- **Dirección:** agent → server
- **Auth:** Bearer `<device_jwt>`
- **Body:** una fila o batch de filas por sesión de app
  ```json
  {
    "app_id": "com.roblox.robloxclient",
    "app_name": "Roblox",
    "started_at": "2026-07-17T14:00:00Z",
    "ended_at": "2026-07-17T14:32:00Z",
    "foreground_seconds": 1920,
    "was_blocked": false
  }
  ```

### `POST /rest/v1/device_alerts`

Sube alertas operacionales del agente (servicio caído, intento de manipulación, etc.).

- **Dirección:** agent → server
- **Auth:** Bearer `<device_jwt>`
- **Body:**
  ```json
  {
    "alert_type": "SERVICE_DOWN | TAMPER_ATTEMPT | ENFORCEMENT_LOST | UNUSUAL_HOUR",
    "severity": "INFO | WARN | CRITICAL",
    "message": "Agent service stopped unexpectedly",
    "context": { "restart_count": 3 }
  }
  ```

### `POST /rest/v1/behavioral_events`

Sube eventos discretos de comportamiento con semántica de producto.

- **Dirección:** agent → server
- **Auth:** Bearer `<device_jwt>`
- **Body:**
  ```json
  {
    "event_type": "APP_BLOCKED | TIME_LIMIT_REACHED | CATEGORY_BLOCKED | EMERGENCY_OVERRIDE_USED | SUSPICIOUS_ACTIVITY",
    "subject": "com.roblox.robloxclient",
    "occurred_at": "2026-07-17T14:32:00Z",
    "metadata": { "daily_used_minutes": 121, "limit_minutes": 120 }
  }
  ```

### `POST /rest/v1/time_requests`

El hijo pide tiempo extra al padre.

- **Dirección:** agent → server
- **Auth:** Bearer `<device_jwt>`
- **Body:**
  ```json
  {
    "requested_minutes": 30,
    "reason": "Terminé la tarea, ¿puedo jugar 30 min más?"
  }
  ```

---

## 4. Salud y operación

### `RPC get_device_policy(device_id)`

Descarga la política de control activa (apps bloqueadas, límites, horarios, grants).

- **Dirección:** agent → server
- **Auth:** Bearer `<device_jwt>`
- **Argumento:** `device_id` (UUID)
- **Retorno esperado:** snapshot completo de política (estructura a definir; ver §6).

### `RPC heartbeat`

Envía estado de salud del agente: nivel de enforcement, offset de reloj, métricas de proceso.

- **Dirección:** agent → server
- **Auth:** Bearer `<device_jwt>`
- **Body:**
  ```json
  {
    "battery_level": 0.85,
    "is_charging": false,
    "app_in_foreground": null,
    "enforcement_level": "STANDARD | STRICT | DEGRADED",
    "clock_offset_ms": -1200,
    "policy_version": 5
  }
  ```
- **Retorno esperado:** confirmación + `policy_version` actual del servidor.

### `POST /rest/v1/integrity_reports`

Envía hash/firma del binario del agente para verificar que no fue alterado.

- **Dirección:** agent → server
- **Auth:** Bearer `<device_jwt>`
- **Body:**
  ```json
  {
    "binary_path": "C:\\Program Files\\ParentalControl\\agent.exe",
    "binary_sha256": "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
    "signature_valid": true,
    "signer": "CN=ParentalControl",
    "reported_at": "2026-07-17T15:00:00Z"
  }
  ```

---

## 5. Canales server → agent

### `WNS push registration`

Registra el canal de push para que el backend pueda notificar al agente aunque esté en background.

- **Dirección:** agent → server (registro); server → agent (entrega)
- **Auth:** Bearer `<device_jwt>` para el registro; WNS nativo para la entrega
- **Registro:**
  ```json
  POST /rest/v1/push_channels
  {
    "wns_channel_uri": "https://db5.notify.windows.com/?token=...",
    "platform": "WINDOWS_DESKTOP",
    "app_version": "1.0.0"
  }
  ```
- **Payload WNS esperado del backend:** ver §6 (a definir).

### `realtime (WebSocket)`

Se suscribe a cambios de política/grants mientras la UI está abierta.

- **Dirección:** server → agent (push)
- **Auth:** Bearer `<device_jwt>` al conectar
- **Suscripciones esperadas:**
  - `device_policy:{device_id}` — cambios de política
  - `grants:{device_id}` — nuevos grants / revocaciones
  - `commands:{device_id}` — comandos inmediatos (lock, unlock, refresh-policy)

---

## 6. Pendientes de definición conjunta

Para que el backend pueda implementar, estos puntos quedan abiertos y se resolverán en la misma revisión:

1. Shape exacta del retorno de `get_device_policy`.
2. Schema del payload WNS (tipos de mensaje, TTL, prioridad).
3. Convención de batch vs POST unitario para `usage_logs`, `behavioral_events`, `device_alerts`.
4. Headers de idempotencia (`Idempotency-Key`) y comportamiento ante reintentos.
5. Catálogo de errores uniforme (códigos estables, semántica de retry).
6. Política de retención y deduplicación offline.
7. Endpoint de unpair y baja de dispositivo.

Estos puntos se desarrollan en detalle en **`docs/windows-agent-api-contract-requirements.md`** (documento complementario).

---

## 7. Fuera de alcance (esta propuesta)

- App móvil del padre (Android) — usa contrato T15 separado, ver `supabase/README.md`.
- Panel web del padre — fuera de esta lista.
- Administración interna / herramientas de soporte.