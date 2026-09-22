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
- **Sin ORM, sin DI, sin pruebas automatizadas** (ver [docs/pruebas.md](docs/pruebas.md)).

## Arquitectura
Ver [docs/arquitectura.md](docs/arquitectura.md)

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
  operado; ver [docs/flujos.md](docs/flujos.md)).
- `FNCServicioProgramas/` — misma lógica que el anterior, como servicio de
  Windows (duplicación, ver [docs/decisiones.md](docs/decisiones.md)).
- `FNCInspiraServinte/`, `FNCWSDigiturno/`, `FNCESB/`, `ESBDigiturno/` —
  servicios WCF/ASMX expuestos (ver [docs/endpoints.md](docs/endpoints.md)).
- `FNCEnviaEspiros/`, `FNCJsonProcessor/`, `FNCSincroniza/`, `FNCETL/` y demás
  ejecutables de proceso batch — uno por tarea programada/servicio.
- `docs/` — documentación detallada:
  - `arquitectura.md` — capas, reglas de dependencia, patrones, proyectos en desuso.
  - `dominio.md` — entidades y reglas de negocio.
  - `endpoints.md` — servicios WCF/ASMX/WebForms y puntos de entrada de cada ejecutable.
  - `flujos.md` — flujos de negocio de punta a punta.
  - `decisiones.md` — decisiones técnicas no obvias y riesgos identificados.
  - `pruebas.md` — estado de testing (no hay pruebas automatizadas hoy).
  - `historial.md` — historial completo de requerimientos cerrados.
  - `externas.md` — integraciones externas (Salesforce, Oracle, AWS, etc.).
  - `proyectos-compartidos.md` — proyectos que esta solución comparte con
    `Reportes` (quién es dueño de cuál y qué se rompe al tocarlos).

## Proyectos compartidos con la solución `Reportes`

`FNCDAC`, `FNCEntity`, `FNCFacade` y `FNCUtils` **son de este repositorio pero
también los compila `Reportes`** (`Trazabilidad`, en producción). En sentido
inverso, `EventLog` es de `Reportes` y lo usan 19 proyectos de aquí.

Documentación única en [docs/proyectos-compartidos.md](docs/proyectos-compartidos.md)
— no duplicarla en `Reportes`.

## Flujos principales
Ver [docs/flujos.md](docs/flujos.md)

## Entidades del dominio
Ver [docs/dominio.md](docs/dominio.md)

## Endpoints o puntos de entrada
Ver [docs/endpoints.md](docs/endpoints.md)

## Dependencias externas
Ver [docs/externas.md](docs/externas.md)

## Decisiones técnicas no obvias
Ver [docs/decisiones.md](docs/decisiones.md)

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
  alguno en `FNCESB.sln`. Ver
  [docs/proyectos-compartidos.md](docs/proyectos-compartidos.md).
- **Transacciones de un solo `Commit()` al final** en `ServinteOracle` e
  `Integrador`: no hay forma de ver avance incremental en la base de datos
  mientras un proceso batch corre. No confundir "sin filas nuevas" con "no está
  avanzando" — ver [docs/flujos.md](docs/flujos.md#carga-de-programas-especiales).
- **`FNCCargoProgramas` vs `FNCServicioProgramas`**: lógica de negocio duplicada.
  Verificar cuál está activo en el servidor antes de aplicar un cambio de reglas.
- **`FNCSalesforce.SalesforceIntegrator` vs `SalesforceViaRestApi`**: confirmar
  cuál usa el proyecto concreto antes de tocar la integración con Salesforce.

## Pruebas
Ver [docs/pruebas.md](docs/pruebas.md)

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

No hay comando de test — no existen pruebas automatizadas (ver
[docs/pruebas.md](docs/pruebas.md)).

## Requerimiento en curso
(vacío)

## Últimos cambios
(máximo 3 entradas, el historial completo está en docs/historial.md)

- 2026-09-22: corregido error al crear consentimiento vía `WSDigiturno.asmx` (flag SUCCESS mal calculado, `smail` sin mapear, `LogError` con argumentos invertidos) — ver [docs/historial.md](docs/historial.md).

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
   Cuando el usuario diga "cerrar requerimiento", haz esto en orden:
   a. Agrega una entrada completa a docs/historial.md con:
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
   c. Actualiza docs/pruebas.md:
      - Agrega las clases o módulos nuevos a "Qué se prueba en este proyecto"
      - Agrega una entrada al "Historial de cobertura" con fecha,
        qué se cubrió y por qué
   d. Si el requerimiento implicó una decisión técnica no obvia,
      agrega una entrada a docs/decisiones.md
   e. Si el requerimiento afectó endpoints, dominio, flujos o integraciones,
      actualiza el archivo docs/ correspondiente
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
