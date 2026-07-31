# Informe técnico: bloqueos del backend para Windows

## Conclusión ejecutiva

Windows puede continuar desarrollando sus funciones locales, pero la integración completa con el backend todavía no está lista para producción.

Los bloqueos confirmados son:

1. La sesión no queda asociada correctamente al dispositivo después del pairing.
2. La policy pierde reglas y su lectura no valida correctamente al dispositivo.
3. El backend no puede enviar avisos WNS.
4. La aprobación de tiempo extra puede quedar incompleta o duplicarse.
5. Algunos eventos no tienen reintentos seguros.
6. El backend no calcula un veredicto de integridad para Windows.

Los dos primeros son los más graves. Sin una identidad válida y una policy completa, las demás integraciones trabajan sobre una base insegura o incorrecta.

## Alcance de la revisión

Este informe compara:

- lo que espera el cliente Windows;
- lo que especifica el backend;
- lo que implementan actualmente sus migraciones y Edge Functions.

Repositorios revisados:

- Windows: `C:\Users\Usuario\Desktop\Proyectos\ControlParental`
- Backend: `C:\Users\Usuario\Desktop\Proyectos\ParentalControls`

No se revisó un despliegue real. Las conclusiones describen el código actual, no garantizan qué versión está instalada en staging o producción.

## Resumen de riesgos

| Área | Problema observado | Qué bloquea | Gravedad |
| --- | --- | --- | --- |
| Pairing/JWT | Windows no obtiene la identidad del dispositivo creado | Llamadas protegidas y RLS | Crítica |
| Policy | Se pierden límites y falta comprobar la propiedad | Aplicar reglas reales con seguridad | Crítica |
| WNS | El backend solo tiene infraestructura FCM | Avisos rápidos a Windows | Alta |
| Tiempo extra | Aprobación y grant se guardan por separado | Garantizar un solo grant | Alta |
| Eventos | La deduplicación no cubre todos los datos | Reintentos seguros | Alta |
| Integridad | Se guarda evidencia, pero no se evalúa | Reacción ante versiones revocadas | Media/alta |

---

## 1. La identidad queda rota después del pairing

**Evaluación: bloqueo crítico confirmado.**

### Qué debería ocurrir

Windows crea una sesión anónima, realiza el pairing y renueva el JWT. Ese token debe contener el `device_id` para que RLS sepa qué datos puede utilizar el dispositivo.

Evidencia del contrato:

- `ControlParental/backlog-control-parental-windows.md:317-326`
- `ControlParental/openspec/changes/t24-pairing-code/spec.md:11-36`
- `ParentalControls/openspec/specs/pairing-flow/spec.md:28`

### Qué ocurre actualmente

El backend consume correctamente el código, pero crea otro usuario de autenticación y guarda allí el `device_id`. Windows no recibe una sesión utilizable de ese nuevo usuario.

Evidencia de implementación:

- Consumo del código: `ParentalControls/supabase/functions/pairing/index.ts:86-98,258-292`
- Nuevo usuario: `ParentalControls/supabase/functions/pairing/index.ts:100-133`
- Metadatos: `ParentalControls/supabase/functions/pairing/index.ts:196-208`
- Respuesta: `ParentalControls/supabase/functions/pairing/index.ts:225-233`

### Por qué bloquea

Conocer el UUID no significa estar autenticado como ese dispositivo. Es como conocer el número de una habitación sin tener su tarjeta de acceso.

Sin el JWT correcto no puede demostrarse de forma segura qué dispositivo pide una policy, envía telemetría o accede mediante RLS.

### Evidencia necesaria para cerrarlo

- JWT renovado con el `device_id` correcto.
- Renovación posterior sin perder la identidad.
- Acceso permitido a los datos propios.
- Acceso rechazado a los datos de otro dispositivo.

---

## 2. La policy es incompleta y su autorización es insegura

**Evaluación: bloqueo crítico confirmado.**

### Qué debería ocurrir

El backend debe devolver la policy definida por Windows, con versión, límites, categorías, horarios y grants.

Evidencia del contrato:

- Forma de la policy: `ControlParental/backlog-control-parental-windows.md:88-121`
- Entrega y versionado: `ControlParental/backlog-control-parental-windows.md:292-295,328-339`

### Qué ocurre actualmente

El RPC fija el límite global en 120 y devuelve vacíos los límites por categoría, aunque las plantillas contienen otros valores.

También recibe un `device_id` y usa permisos elevados sin comprobar claramente que pertenezca al JWT actual.

Evidencia de implementación:

- RPC: `ParentalControls/supabase/migrations/001_initial_schema.sql:321-404`
- Datos fijos o vacíos: `ParentalControls/supabase/migrations/001_initial_schema.sql:388-400`
- Límites reales en plantillas: `ParentalControls/supabase/migrations/003_policy_templates.sql:14-27,48-76,99-126`
- Edge Function con permisos de servicio: `ParentalControls/supabase/functions/get-policy/index.ts:27-47`

### Por qué bloquea

Windows no puede reconstruir reglas que el backend no envía. Un adaptador puede cambiar nombres, pero no recuperar información perdida.

Además, el backend debe comprobar tanto qué policy se solicita como si el token tiene permiso para leerla. De lo contrario, conocer el UUID de otro dispositivo podría permitir intentar acceder a su policy.

### Evidencia necesaria para cerrarlo

- Policy completa, sin sustituir ni omitir límites.
- Validación entre el `device_id` solicitado y el JWT.
- Prueba con dos dispositivos que demuestre el aislamiento.

---

## 3. Falta soporte WNS en el backend

**Evaluación: bloqueo para avisos rápidos; no bloquea polling.**

### Qué debería ocurrir

El backend debe registrar el canal WNS y enviar una señal mínima de “sincronizá ahora”. Windows obtiene después los datos reales mediante HTTPS.

Evidencia del contrato:

- `ControlParental/backlog-control-parental-windows.md:298-303,341-350`
- `ControlParental/apis.md:156-173`

### Qué ocurre actualmente

La infraestructura disponible registra tokens FCM y envía mediante Firebase, que corresponde a Android. No se encontró un flujo equivalente para WNS.

Evidencia de implementación:

- Plataformas y tokens: `ParentalControls/supabase/migrations/001_initial_schema.sql:19,121-133`
- Registro FCM: `ParentalControls/supabase/functions/register-token/index.ts:37-65`
- Envío FCM: `ParentalControls/supabase/functions/fcm-send/index.ts:52-73,92-120`

### Por qué bloquea

FCM es el timbre de Android y WNS el de Windows. Sin WNS, polling sigue funcionando, pero Windows puede enterarse tarde de un bloqueo, una nueva policy o un grant.

### Evidencia necesaria para cerrarlo

- Registro y renovación del canal WNS.
- Asociación con el dispositivo correcto.
- Envío real de la señal.
- Limpieza de canales inválidos.
- Polling como respaldo cuando el push no llega.

---

## 4. La aprobación de tiempo extra no es atómica

**Evaluación: bloqueo de consistencia confirmado.**

### Qué debería ocurrir

Una aprobación debe cambiar el estado, crear un solo grant, aumentar la versión y quedar disponible para sincronización.

Evidencia del contrato:

- `ControlParental/backlog-control-parental-windows.md:461-471`
- `ParentalControls/openspec/specs/time-request-approval/spec.md:41-58`

### Qué ocurre actualmente

La solicitud se marca como aprobada antes de crear el grant. No existe una restricción única suficiente que garantice un solo grant por solicitud. La notificación posterior usa FCM.

Evidencia de implementación:

- Aprobación y creación: `ParentalControls/supabase/functions/approve-request/index.ts:190-220`
- Relación del grant: `ParentalControls/supabase/migrations/001_initial_schema.sql:84-99`
- Notificación FCM: `ParentalControls/supabase/functions/approve-request/index.ts:233-240,268-306`

### Por qué bloquea

Un fallo intermedio puede dejar una aprobación sin grant. Dos peticiones simultáneas pueden producir grants duplicados. Windows solo ve el resultado y no puede reparar de forma segura una operación incompleta del servidor.

### Evidencia necesaria para cerrarlo

- Una solicitud produce como máximo un grant.
- Repetir la aprobación devuelve el mismo resultado.
- Ningún fallo deja una aprobación sin grant.
- Polling encuentra el grant aunque falle el push.

---

## 5. Algunos eventos no tienen reintentos seguros

**Evaluación: bloqueo limitado a los datos sin protección suficiente.**

### Qué debería ocurrir

Windows conserva eventos en una outbox y puede reenviarlos después de perder la conexión. El backend debe reconocer el mismo evento y evitar duplicarlo.

Evidencia del contrato:

- `ControlParental/backlog-control-parental-windows.md:161-173,328-339,509-520`

### Qué ocurre actualmente

Los resúmenes de uso ya tienen una combinación única de dispositivo, aplicación y fecha. Otros eventos, alertas o solicitudes no siempre tienen una clave de cliente o restricción equivalente.

Evidencia de implementación:

- Uso con clave única: `ParentalControls/supabase/migrations/001_initial_schema.sql:102-114`
- Otros eventos y alertas: `ParentalControls/supabase/migrations/004_parent_outcome_checkins.sql:53-70`
- `ParentalControls/supabase/migrations/011_telemetry_and_rls.sql:43-101`
- Solicitudes de tiempo: `ParentalControls/supabase/migrations/001_initial_schema.sql:64-75`

### Por qué bloquea

El backend puede guardar un evento y perderse la respuesta. Windows lo reenvía porque no sabe que fue recibido. Sin deduplicación del servidor pueden aparecer dos copias.

### Evidencia necesaria para cerrarlo

Para cada tipo afectado debe quedar definido:

- identificador estable;
- restricción que evita duplicados;
- respuesta para “ya estaba guardado”;
- errores temporales y definitivos.

---

## 6. Falta un veredicto de integridad para Windows

**Evaluación: bloqueo para decisiones remotas de confianza.**

### Qué debería ocurrir

Windows envía evidencia de firma, hash y versión. El backend responde, por ejemplo, `trust`, `revoked` o `unknown`.

Evidencia del contrato:

- `ControlParental/backlog-control-parental-windows.md:386-404`
- `ControlParental/openspec/changes/t23-agent-integrity/t14-gap-analysis.md:17-26,49-68,107-113`

### Qué ocurre actualmente

El backend puede guardar reportes, pero no calcula un veredicto para Windows. El verificador existente está orientado a Google Play y puede devolver un resultado simulado cuando no está configurado.

Evidencia de implementación:

- Reportes: `ParentalControls/supabase/migrations/011_telemetry_and_rls.sql:103-143`
- Verificador Android: `ParentalControls/supabase/functions/verify-integrity/index.ts:107-119`

### Por qué bloquea

Windows puede validar localmente una firma, pero no saber si el servidor revocó esa versión. Una versión puede estar bien firmada y aun así estar prohibida por un problema de seguridad.

### Evidencia necesaria para cerrarlo

- Evaluación real de evidencia de Windows.
- Respuesta documentada para cada veredicto.
- Fallo seguro si el verificador no está disponible.
- Ningún resultado simulado presentado como validación real.

---

## Decisión pendiente: `child_first_name`

El contrato de Windows no garantiza que el cliente conozca `child_first_name`:

- `ControlParental/backlog-control-parental-windows.md:406-418`
- `ControlParental/openspec/changes/t24-pairing-code/spec.md:17-26`

El backend lo exige al consumir el código, pero no lo guarda al crear ese código:

- Requisito: `ParentalControls/supabase/functions/pairing/index.ts:72-77`
- Creación: `ParentalControls/supabase/functions/create-pairing-code/index.ts:49-67`

Debe decidirse si el tutor lo guarda, Windows lo pregunta o deja de ser obligatorio. Es una contradicción contractual, no un bug atribuible automáticamente a un solo equipo.

## Diferencias que no son bloqueos

- **Heartbeat:** existe una Edge Function con `server_time`; Windows puede adaptar la ruta. Evidencia: `ParentalControls/supabase/functions/heartbeat/index.ts:52-114`.
- **Nombres de campos:** Windows puede mapearlos si no se pierde información.
- **Código de pairing:** el backend genera ocho caracteres y Windows puede aceptar la longitud oficial. Evidencia: `ParentalControls/supabase/functions/create-pairing-code/index.ts:103-111`.
- **Age bands:** pueden mapearse después de decidir cómo tratar `17-18`.
- **Polling:** permite sincronizar aunque WNS todavía no esté disponible.

## Qué puede continuar en Windows

Puede continuar el desarrollo de:

- motor local de reglas y bloqueo;
- policy local, horarios, límites y grants;
- almacenamiento offline;
- outbox, reintentos, polling y backoff;
- comunicación entre UI, servicio y Session Agent;
- instalación, permisos, recuperación y actualización;
- parte cliente de WNS;
- firma, hashes e integridad local;
- interfaces, adaptadores y tests con respuestas controladas.

Todavía no puede declararse terminada la integración real de:

- pairing y JWT del dispositivo;
- aislamiento RLS;
- descarga segura de la policy completa;
- WNS de punta a punta;
- grants atómicos;
- deduplicación de todos los eventos afectados;
- veredicto remoto de integridad.

## Prueba final necesaria

Después de corregir los bloqueos, staging debe demostrar:

1. Pairing con JWT y `device_id` correctos.
2. Aislamiento entre dos dispositivos.
3. Policy completa y rechazo de accesos ajenos.
4. Reintentos sin duplicados.
5. Una aprobación repetida con un solo grant.
6. Grant encontrado mediante polling aunque falle el push.
7. Registro y recepción real de WNS.
8. Evidencia de integridad con veredicto real.

Staging no es otro bug: es la prueba que permite afirmar que las correcciones funcionan juntas.

## Dictamen final

La integración no debería aprobarse para producción mientras sigan abiertos los problemas de identidad y policy.

Windows puede avanzar de forma segura en sus componentes locales mediante interfaces, adaptadores, polling y pruebas controladas. Sin embargo, esos avances no deben confundirse con una integración real ya validada contra el backend.
