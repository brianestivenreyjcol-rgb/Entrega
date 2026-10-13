# T0 y planes de acción (`/t0`)

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del proyecto).

Tercera pestaña del informe de Auditorías (junto a General y Formación & Calidad). **Regla de negocio: cada auditoría de
calidad con afectación T0 tiene que estar reportada en `[Reporting].[pdc].[Reportar_T0]` y gestionarse con un plan de acción.**

- 08-10-2026: primera versión (publicada en el 5180 ese día): auditorías con T0 frente a `RecursosHumanos.Legal.Alertas_T0` y
  `Legal.PlanAccion` (ver «Historia» abajo).
- **13-10-2026: el usuario pidió usar SOLO `Reportar_T0`** del lado de las T0 («solo con esa información») y verlo **por técnico de
  calidad / auditor**: «Ariza Hernandez, Maria Del Pilar tiene 10 auditorías, de las cuales 5 son T0 y las 5 están en Reportar_T0».
  Esta versión **no está publicada** todavía.

## Qué se cruza (comprobado contra SQL el 13-10-2026)

| Pieza | De dónde | Notas |
|---|---|---|
| Auditorías de cada auditor | las de General (`Consultas/Auditorias.sql`, WEB e ICEBERG) | ya están en memoria; mismos filtros |
| Auditoría con T0 | `ModulosIceberg.Calidad.PlantillaCalidadUnificada`, columna `[Tolerancia 0]` rellena (ni vacía ni «N/A») | solo ICEBERG: los formularios WEB no tienen campo de T0 |
| ID de la llamada | `Calidad.PlanillaRespuestasP` + `PlanillaPreguntasP` (`IdP = IdPRegunta`), preguntas `Call ID%`, `ID%llamada%`, `ID de Grabaci%`, `ID%Conversaci%` | sale en el 96 % de las T0; `Total_Plantillas` es una vista sobre esas tablas |
| ¿Está en Reportar_T0? | `[Reporting].[pdc].[Reportar_T0]` | 1) mismo `ID_Llamada`; 2) si no, `Legajo_AG` = legajo, `Auditor_Legajo` = `[Legajo Auditor]` y `Fecha_reporte_Alerta` entre 1 día antes y 15 después. Con varias, la de la misma infracción y la más cercana |
| Gestión y plan | la misma fila de Reportar_T0: `Estado`, `Accion` («Plan de acción», «Registro de Falta», «Agente dado de baja», «Sin acción») y `ID_Accion` (número del plan; 0 si no se puso) | no se usa ninguna otra tabla de T0 ni de planes |

- `Reportar_T0`: unas 8.500 filas desde el 28-02-2026, las mismas columnas que `Alertas_T0` (y los mismos ID: es un subconjunto
  reciente). `Estado` «Pendiente» = `Accion` «Sin acción». Mediana entre el reporte y la gestión: 4 días (el 10 % pasa de 20).
- Nombre y legajo del auditor coinciden entre ICEBERG y Reportar_T0. En ~20 % de los cruces por llamada la fila de Reportar_T0 la
  subió otra persona: la T0 cuenta como reportada y se atribuye al auditor de la auditoría.
- Varias auditorías pueden caer en la misma fila de Reportar_T0 (la misma llamada auditada dos veces): cuentan las dos como reportadas.
- Muchas filas de Reportar_T0 no salen de una auditoría T0 de ICEBERG (desde julio, 2.473 de 4.255): formularios WEB, team leaders…
  Salen en la columna «Sus reportes» de cada auditor y en la nota de la tabla.

## Cifras del 13-10-2026 (01-07 a 12-10-2026)

23.265 auditorías de 156 auditores; 3.228 con T0 (19 % de las de ICEBERG); **2.154 en Reportar_T0 (66,7 %)**; 1.074 sin reportar
(76 auditores con alguna); de las reportadas, 1.812 con plan de acción (84 %), 236 sin plan (registro de falta 197, baja 39) y 106
pendientes. Por plantilla, las que menos se reportan: ESTILO YG/MM, BO'S ORANGE, WHATSAPP YGMM TÉCNICO y ATENCIÓN YGMM.

## En la web

- `Consultas/AuditoriasT0.sql`: dos resultados (las T0 de ICEBERG con `IdReporte` y `Cruce`; Reportar_T0 entero). Se carga con las
  auditorías (`RepositorioAuditorias.CargarT0Async`, que enlaza cada auditoría con su fila); si falla, solo esta pestaña avisa.
- Modelo `Models/AlertaT0.cs`: `AuditoriaT0` (con `Reporte`), `AlertaT0` (fila de Reportar_T0, `GestionT0`), `SituacionT0`
  (Reportada con plan · Reportada sin plan · Reportada pendiente · Sin reportar), `TextosT0`, `InformeT0`, `FilaAuditorT0`.
- Cálculo `Servicios/Tablero/CalculadoraT0.cs`; vista `Views/Tablero/_ContenidoT0.cshtml`; Excel `ExportadorExcel.GenerarT0` (31 columnas:
  la auditoría y su fila de Reportar_T0 con observaciones, motivo y oportunidad de mejora).
- Pantalla: 5 tarjetas (Auditorías, Con T0, En Reportar_T0, Sin reportar, Con plan de acción con la mediana de gestión); **tabla por técnico
  de calidad / auditor** (Auditorías, Con T0, % con T0, En Reportar_T0, Sin reportar, % reportadas, Con plan, Sus reportes); evolución del %
  reportadas y barras por situación; por sector y por plantilla; detalle (200 filas, primero las sin reportar).
- Filtros de General más **Situación** y **Plantilla** (en «Más filtros»). Las auditorías de General y las filas de Reportar_T0 solo
  tienen fecha, sector, super, team, auditor y cargo: con situación o plantilla marcadas, la columna «Auditorías» no cambia.

## Historia: la versión del 08-10-2026 (ya no se usa)

Unía las T0 con `RecursosHumanos.Legal.Alertas_T0` y el plan con `Legal.PlanAccion` (`ID_Accion` = `IdPlanDeAccion`), y comparaba con los
planes de motivo «Acción de Calidad» (`PlanAccionMotivos` 1020; «Tolerancia Cero» es el 1051). `Legal.PlanAccion` no tenía planes del
14-07 al 30-09-2026 y la web avisaba de ese hueco. Todo eso se quitó el 13-10-2026 a petición del usuario.

## Pendiente

- Publicar en el 5180 la versión por auditor cuando lo pida.
- Que el usuario diga si «Registro de Falta» y «Agente dado de baja» cuentan como plan (hoy no).
- Ojo al consultar a mano: el login de SQL está en español; en columnas `datetime`, las fechas como `'20260701'`.
