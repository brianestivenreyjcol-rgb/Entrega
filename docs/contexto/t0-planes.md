# T0 y planes de acción (`/t0`, 08-10-2026, publicado en el 5180 el mismo día)

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del proyecto).

Tercera pestaña del informe de Auditorías (junto a General y Formación & Calidad). El usuario pidió el 08-10-2026
unir las auditorías de calidad con las alertas T0 (`RecursosHumanos.Legal.Alertas_T0`), que es donde se dice qué plan
de acción se cargó, y compararlo con `RecursosHumanos.Legal.PlanAccion` y su «Acción de Calidad». **Regla de negocio:
cada auditoría de calidad con afectación T0 debe tener un plan de acción cargado.** Según el usuario, el jefe de Calidad
dijo que las T0 se iban a cargar como plan con «Acción de Calidad».

## Tablas y cómo se unen (comprobado contra SQL el 08-10-2026)

| Paso | Tabla | Llave |
|---|---|---|
| Auditoría con T0 | `ModulosIceberg.Calidad.PlantillaCalidadUnificada` | columna `[Tolerancia 0]` rellena (ni vacía ni «N/A»): trae el texto de la infracción, el mismo catálogo que `Alertas_T0.Infraccion` |
| ID de la llamada | `Calidad.PlanillaRespuestasP` + `PlanillaPreguntasP` (`IdP = IdPRegunta`) | preguntas `Call ID%`, `ID%llamada%`, `ID de Grabaci%`, `ID%Conversaci%`; sale en el 96 % de las T0. `Calidad.Total_Plantillas` es una vista sobre estas tablas (el usuario señaló las de origen) |
| Alerta T0 | `RecursosHumanos.Legal.Alertas_T0` | 1) `ID_Llamada` = ID de la llamada; 2) si no, `Legajo_AG` = legajo, `Auditor_Legajo` = `[Legajo Auditor]` y `Fecha_reporte_T0` entre 1 día antes y 15 después de la auditoría. Con varias: la de la misma infracción y la más cercana |
| Plan de acción | `RecursosHumanos.Legal.PlanAccion` | `Alertas_T0.ID_Accion` = `IdPlanDeAccion` cuando `Accion` = «Plan de acción». Con la tabla completa (enero–junio) el número existe en el 95–98 % de los casos |
| Motivo / estado | `Legal.PlanAccionMotivos`, `Legal.PlanAccionEstados` | `IdMotivo`, `IdEstado` |

- **«Acción de Calidad» no es una columna**: es el motivo **1020** de `PlanAccionMotivos` (hay también el **1051 «Tolerancia
  Cero»**). Es el motivo más usado de todos (~4.700 planes al mes), no solo para T0. En los planes de las alertas T0 de
  enero–junio: Retroalimentación 40 %, Tolerancia Cero 38 %, Acción de Calidad 16 %. El plan lo crea casi siempre el
  gestionador de la alerta (el team leader), no el auditor.
- **Comparación «Acción de Calidad»**: el primer plan 1020 del mismo agente creado entre el día de la auditoría y 15 días
  después, sin pasar por la alerta (`IdPlanCalidad`). Es un cruce por agente y fecha, no exacto: también hay planes 1020 cerca
  de auditorías sin T0 (en una muestra, el 18 %).
- Los formularios **WEB** (WhatsApp, Jazztel, Orange de `Reporting.WO`) no tienen campo de T0: no entran. 289 alertas desde
  julio cuadran por llamada con auditorías WEB.
- Otras tablas vistas: `Reporting.pdc.Reportar_T0` (mismas columnas que `Alertas_T0`, 8.396 filas), `Legal.LegajoDigital_Registro_Falta_Tipologias`.
- Accion de las alertas: Plan de acción, Registro de Falta, Agente dado de baja, Sin acción, Error QA, Cancelada. Estado:
  Gestionado, Pendiente, Solicitud de Cancelacion.

## Hueco en `Legal.PlanAccion`

**No tiene ningún plan creado del 14-07-2026 al 30-09-2026** (los ID saltan de 840.921 a 865.871). Por eso los planes de
las alertas de esas fechas salen «Plan sin verificar» (de julio a septiembre solo existe el 13 % de los números) y la
comparación con «Acción de Calidad» sale baja. La consulta devuelve un segundo resultado con los planes por día y la web
avisa de cualquier tramo de 3 días o más sin planes (`CalculadoraT0.Huecos`). **Hay que pedir que recarguen la tabla.**

## Cifras del 08-10-2026 (01-07 a 07-10-2026)

3.110 auditorías T0 (19 % de las ICEBERG); con alerta 2.098 (67 %; 1.551 por llamada); con plan según la alerta 1.769
(57 %), de ellas 231 verificadas en PlanAccion; otra acción 238 (registro de falta 197, baja 41); alerta pendiente 91;
**sin alerta 1.012 (33 %)**. Por plantilla, las que menos alerta tienen: ESTILO YG/MM, BO'S ORANGE, FIDE OUT JAZZTEL,
WHATSAPP YGMM TÉCNICO y ATENCIÓN YGMM.

## En la web

- Consulta `Consultas/AuditoriasT0.sql` (unos 8 s), cargada con las auditorías en la misma recarga (`RepositorioAuditorias.CargarT0Async`;
  si falla, solo esta pestaña sale con aviso). Modelo `Models/AuditoriaT0.cs` (`SituacionT0`, `TextosT0`, `InformeT0`),
  cálculo `Servicios/Tablero/CalculadoraT0.cs`, vista `Views/Tablero/_ContenidoT0.cshtml`, Excel `ExportadorExcel.GenerarT0` (30 columnas).
- **Situación** de cada auditoría: Plan verificado, Plan sin verificar (cumplen la regla), Otra acción, Alerta pendiente, Sin alerta T0.
- Filtros de General más **Situación** y **Plantilla** (en «Más filtros»). Tira de 5 tarjetas, evolución del % con plan (auditorías T0 detrás),
  barras por situación, cumplimiento por sector y por team, motivo de los planes, cruce alerta × «Acción de Calidad», detalle (200 filas, primero
  las que incumplen) y «Cómo se une».
- Ojo al consultar a mano: el login de SQL está en español, y en columnas `datetime` el literal `'2026-07-01'` se lee como
  7 de enero. Usar `'20260701'` (en `date` no pasa).

## Pendiente

- Que el usuario confirme si «Agente dado de baja» y «Registro de Falta» cuentan como cumplir la regla (hoy no).
- Que pidan recargar `Legal.PlanAccion` (hueco del 14-07 al 30-09-2026).
