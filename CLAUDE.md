# FNCESB

## Propósito
Bus de integración (ESB) de la Fundación Neumológica Colombiana. Conecta
Salesforce (CRM de citas y planes) con Servinte (HIS/historia clínica en Oracle)
y con la bodega de estadística Integra, además de servicios de digiturno,
telemedicina, consentimiento informado, espirometrías y notificación a pacientes.

## Stack técnico
- **.NET Framework 4.5–4.8** (C#), sin capa de compatibilidad .NET Core/5+.
- **Bases de datos:** Oracle (`FNEUMB` = Servinte HIS, `INTEGRA` = estadística,
  vía `Oracle.ManagedDataAccess.Core`) y SQL Server (`HEIMDALL`/`FNCStats`).
- **Servicios expuestos:** WCF (`.svc`), ASMX clásico, WebForms (`.aspx`).
- **Integración externa:** Salesforce (REST y SOAP, coexistentes), AWS S3, Google
  Calendar, SMTP, SMS (Computec).
- **Librerías clave:** `Oracle.ManagedDataAccess.Core`, `EPPlus` (Excel),
  `iTextSharp`/`itext7` (PDF), `Newtonsoft.Json`, `RestSharp`, `AWSSDK.S3`.
- **Autenticación:** sin estándar único — usuario/clave por parámetro,
  `AuthenticationTokenService.svc` para token propio, o ninguna en varios
  endpoints internos.
- **Sin ORM, sin DI, sin pruebas automatizadas** (ver `pruebas.md` centralizado).

## Documentación completa

Toda la documentación descriptiva de este repositorio vive en
`Centralización_Documentación\Proyectos\BusDatos\docs\FNCESB\`, junto con la de
`Reportes` (carpeta hermana `..\Reportes\` dentro de ese mismo `docs\`, ver
"Proyectos compartidos con la solución Reportes" abajo). No crear `docs/` en
este repositorio — agregar contenido nuevo allá y, si hace falta, enlazarlo
desde este archivo.

| Archivo (en `BusDatos\docs\FNCESB\`) | Qué contiene |
|---|---|
| `arquitectura.md` | Capas, reglas de dependencia, patrones, proyectos en desuso |
| `dominio.md` | Entidades y reglas de negocio |
| `endpoints.md` | Servicios WCF/ASMX/WebForms y puntos de entrada de cada ejecutable |
| `flujos.md` | Flujos de negocio de punta a punta |
| `decisiones.md` | Decisiones técnicas no obvias y riesgos identificados |
| `pruebas.md` | Estado de testing (no hay pruebas automatizadas hoy) |
| `historial.md` | Historial completo de requerimientos cerrados |
| `externas.md` | Integraciones externas (Salesforce, Oracle, AWS, etc.) |
| `proyectos-compartidos.md` | Proyectos que esta solución comparte con `Reportes` (quién es dueño de cuál y qué se rompe al tocarlos) |

## Mapa de carpetas
Son **32 proyectos** independientes en la raíz (sin carpeta contenedora). Los más
relevantes:

- `FNCDAC/` — acceso a datos Oracle/SQL Server (Servinte, Integra). Capa más
  pesada de la solución (`ServinteOracle.cs`, ~3.900 líneas).
- `FNCSalesforce/` — dos clientes de Salesforce, REST y SOAP.
- `FNCFacade/` — orquesta `FNCDAC` + `FNCSalesforce` para operaciones de negocio.
- `FNCEntity/` — POCOs de dominio compartidos, sin lógica.
- `FNCUtils/` — correo, AWS S3, helpers.
- `FNCCargoProgramas/` — ejecutable de carga de programas especiales (el más
  operado; ver `flujos.md` centralizado).
- `FNCServicioProgramas/` — misma lógica que el anterior, como servicio de
  Windows (duplicación, ver `decisiones.md` centralizado).
- `FNCInspiraServinte/`, `FNCWSDigiturno/`, `FNCESB/`, `ESBDigiturno/` —
  servicios WCF/ASMX expuestos (ver `endpoints.md` centralizado).
- `FNCEnviaEspiros/`, `FNCJsonProcessor/`, `FNCSincroniza/`, `FNCETL/` y demás
  ejecutables de proceso batch — uno por tarea programada/servicio.

## Proyectos compartidos con la solución `Reportes`

`FNCDAC`, `FNCEntity`, `FNCFacade` y `FNCUtils` **son de este repositorio pero
también los compila `Reportes`** (`Trazabilidad`, en producción). En sentido
inverso, `EventLog` es de `Reportes` y lo usan 19 proyectos de aquí.

Documentación única en `proyectos-compartidos.md` centralizado — no duplicarla
en `Reportes`, y tampoco copiarla de vuelta a este repositorio.

## Endpoints o puntos de entrada
Ver `endpoints.md` centralizado.

## Zonas de peligro
- **`FNCDAC/ServinteOracle.cs`** (~3.900 líneas): núcleo de creación/actualización
  de pacientes y cargos en Servinte. Tiene bloques grandes de lógica comentada
  (creación de cargo físico/RIPS) — no asumir que borrar el comentario reactiva
  esa lógica sin antes entenderla completa.
- **Cualquier `App.config`/`Web.config`**: contienen credenciales de Salesforce,
  Oracle y SQL Server en texto plano, versionadas en git. No agregar más secretos
  en texto plano; al tocar uno, considerar si corresponde moverlo a un mecanismo
  de secretos.
- **`..\..\Reportes\EventLog`**: dependencia de 19 proyectos que vive **fuera** de
  este repositorio. Un cambio ahí no queda registrado en el historial de git de
  `FNCESB`.
- **`FNCDAC`, `FNCEntity`, `FNCFacade`, `FNCUtils`**: los compila también
  `Reportes\Trazabilidad`, que está **en producción** (UROR2, `E:\www\newcargos`).
  Un cambio de firma pública rompe el build de `Reportes` sin que aparezca error
  alguno en `FNCESB.sln`. Ver `proyectos-compartidos.md` centralizado.
- **Transacciones de un solo `Commit()` al final** en `ServinteOracle` e
  `Integrador`: no hay forma de ver avance incremental en la base de datos
  mientras un proceso batch corre. No confundir "sin filas nuevas" con "no está
  avanzando" — ver `flujos.md` centralizado → "Carga de programas especiales".
- **`FNCCargoProgramas` vs `FNCServicioProgramas`**: lógica de negocio duplicada.
  Verificar cuál está activo en el servidor antes de aplicar un cambio de reglas.
- **`FNCSalesforce.SalesforceIntegrator` vs `SalesforceViaRestApi`**: confirmar
  cuál usa el proyecto concreto antes de tocar la integración con Salesforce.

## Comandos del día a día
No hay `.sln` único para toda la solución de forma consistente con build/test por
CLI estandarizado (proyectos `.NET Framework` clásicos, pensados para compilarse
desde Visual Studio). Como referencia:

```powershell
# Compilar un proyecto puntual (requiere MSBuild de Visual Studio en el PATH)
msbuild FNCCargoProgramas\FNCCargoProgramas.csproj /p:Configuration=Release

# Ejecutar un batch en el servidor, ya compilado
.\FNCCargoProgramas.exe <true|false>   # true = Famisanar

# Ver errores registrados por un proceso
Get-EventLog -LogName FNCProgramas -Newest 20 | Format-List TimeGenerated, EntryType, Message

# Ver si un proceso batch sigue con sesión activa en Oracle
# (ejecutar contra FNEUMB para fases de Servinte, contra INTEGRA para la fase final)
SELECT username, status, last_call_et, program FROM v$session WHERE username IN ('SERVINTE','INTEGRABUS','FNCSISTEMAS');
```

No hay comando de test — no existen pruebas automatizadas (ver `pruebas.md`
centralizado).

## Requerimiento en curso
(vacío)

## Últimos cambios
(máximo 3 entradas, el historial completo está en `historial.md` centralizado)

- 2026-09-24: `FNCDescargaSoportes` — tarea programada caída por .NET 4.8.1 (pasado a 4.8 con candado en el `.csproj`, no versionado), descarga de "Nota adicional TERAPEUTA" sin cita en seguimientos, corrección de `ListKeys` y recuperación de 763 notas y del 31/08 — ver `historial.md` centralizado.
- 2026-09-22: corregido error al crear consentimiento vía `WSDigiturno.asmx` (flag SUCCESS mal calculado, `smail` sin mapear, `LogError` con argumentos invertidos) — ver `historial.md` centralizado.

## INSTRUCCIONES DE MANTENIMIENTO — leer y respetar siempre

1. PUNTO DE CONTROL (al cerrar una sesión intermedia de un requerimiento):
   Cuando el usuario diga "punto de control", actualiza la sección
   "Requerimiento en curso" del CLAUDE.md con:
   - Qué se está implementando
   - Qué ya está hecho (con archivos modificados)
   - Cuál es el siguiente paso exacto
   - Contexto importante que no está en el código
   - Si se escribieron pruebas: cuáles y qué cubren

2. RETOMAR SESIÓN (al iniciar una sesión nueva):
   Cuando el usuario diga "retomar", lee la sección "Requerimiento en curso"
   y resume en 3 líneas dónde estamos y cuál es el siguiente paso.

3. CIERRE DE REQUERIMIENTO (al terminar un requerimiento completo):
   Cuando el usuario diga "cerrar requerimiento", haz esto en orden. Los pasos
   a/c/d/e son sobre archivos en
   `Centralización_Documentación\Proyectos\BusDatos\docs\FNCESB\` (no en este
   repositorio); el paso b es sobre este mismo `CLAUDE.md`:
   a. Agrega una entrada completa a `historial.md` con:
      - Fecha de hoy
      - Título corto del requerimiento
      - Qué se implementó
      - Archivos modificados
      - Decisiones tomadas que no son obvias
      - Pruebas escritas: qué clases o módulos y qué escenarios cubren
      - Si quedó algo pendiente
   b. Actualiza "Últimos cambios" en el CLAUDE.md:
      - Agrega la entrada nueva resumida en 1 línea
      - Si ya hay 3 entradas, elimina la más antigua
   c. Actualiza `pruebas.md`:
      - Agrega las clases o módulos nuevos a "Qué se prueba en este proyecto"
      - Agrega una entrada al "Historial de cobertura" con fecha,
        qué se cubrió y por qué
   d. Si el requerimiento implicó una decisión técnica no obvia,
      agrega una entrada a `decisiones.md`
   e. Si el requerimiento afectó endpoints, dominio, flujos o integraciones,
      actualiza el archivo correspondiente en ese mismo `docs\FNCESB\`
   f. Borra el contenido de "Requerimiento en curso" y déjalo como (vacío)

4. ESCRIBIR PRUEBAS (comportamiento estándar siempre):
   Cuando el usuario pida implementar o modificar lógica de negocio,
   sin que tenga que pedirlo explícitamente:
   - Propón qué pruebas unitarias corresponden a ese cambio
   - Usa las herramientas de testing estándar del stack detectado
   - Usa la estructura AAA (Arrange / Act / Assert) con comentarios
   - Nombra los tests describiendo: método o función, escenario y resultado esperado
   - Mockea solo dependencias externas, nunca módulos propios del proyecto
   - Cubre mínimo: caso feliz + caso de error más probable
