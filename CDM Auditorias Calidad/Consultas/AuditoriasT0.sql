-- Auditorías de calidad con Tolerancia 0 (T0) y su plan de acción, para la pestaña «T0 y planes
-- de acción» del informe de Auditorías. Regla de negocio: cada auditoría con afectación T0 debe
-- tener un plan de acción cargado.
--
-- Cómo se une (lo comprobado el 08-10-2026 está en docs/contexto/t0-planes.md):
--   1. Auditoría con T0: ICEBERG, ModulosIceberg.Calidad.PlantillaCalidadUnificada con la
--      columna [Tolerancia 0] rellena (ni vacía ni «N/A»). Los formularios WEB (WhatsApp,
--      Jazztel, Orange) no tienen campo de T0.
--   2. ID de la llamada: no está en PlantillaCalidadUnificada; sale de las respuestas de la
--      plantilla (PlanillaRespuestasP + PlanillaPreguntasP: «Call ID», «ID llamada»,
--      «ID de Grabación», «ID Conversacion»…).
--   3. Alerta T0 (RecursosHumanos.Legal.Alertas_T0): primero por el ID de la llamada; si no, el
--      mismo agente y el mismo auditor con la alerta reportada entre 1 día antes y 15 después de
--      la auditoría. Si hay varias, la de la misma infracción y la más cercana en fecha.
--   4. Plan de acción: la alerta dice la acción tomada (Accion) y, si es «Plan de acción», su
--      número (ID_Accion) = RecursosHumanos.Legal.PlanAccion.IdPlanDeAccion.
--   5. Comparación: el plan con motivo «Acción de Calidad» (PlanAccionMotivos 1020) del mismo
--      agente creado entre el día de la auditoría y 15 días después, sin pasar por la alerta
--      (así dijo Calidad que se iban a cargar las T0).
-- Misma ventana que ICEBERG en Auditorias.sql: 3 meses atrás + el mes en curso.
-- La web la lee en cada carga: un cambio aquí vale con pulsar «Actualizar».

SET NOCOUNT ON;

DECLARE @Desde DATE = DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()) - 3, 0);
DECLARE @Hasta DATE = DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()) + 1, 0);

WITH Nomina AS
(
    SELECT
        CAST(NA.fecha AS DATE) AS Fecha,
        NA.leg,
        NA.sec_descrip,
        NA.Super,
        NA.Team,
        NA.Agente,
        ROW_NUMBER() OVER (PARTITION BY CAST(NA.fecha AS DATE), NA.leg ORDER BY NA.leg) AS RN
    FROM Planificacion.Nomina.NominaAntiguedad NA
    WHERE NA.fecha >= @Desde AND NA.fecha < @Hasta
),

T0 AS
(
    SELECT
        P.IdGestion,
        CAST(P.[Fecha Auditoria] AS DATE) AS Fecha,
        P.Legajo,
        P.[Legajo Auditor] AS LegajoAuditor,
        P.Auditor,
        P.[Cargo Auditor] AS CargoAuditor,
        P.[Nombre Plantilla] AS Plantilla,
        LTRIM(RTRIM(P.[Tolerancia 0])) AS Infraccion,
        P.[Nota Calidad] AS Nota
    FROM ModulosIceberg.Calidad.PlantillaCalidadUnificada P
    WHERE P.[Fecha Auditoria] >= @Desde
      AND P.[Fecha Auditoria] < @Hasta
      AND ISNULL(LTRIM(RTRIM(P.[Tolerancia 0])), '') NOT IN ('', 'N/A')
),

Llamada AS
(
    SELECT
        R.IdGestion,
        MIN(LTRIM(RTRIM(R.Respuesta))) AS ID_Llamada
    FROM ModulosIceberg.Calidad.PlanillaRespuestasP R
    INNER JOIN ModulosIceberg.Calidad.PlanillaPreguntasP Q
        ON Q.IdP = R.IdPRegunta
    WHERE R.IdGestion IN (SELECT IdGestion FROM T0)
      AND (Q.Pregunta LIKE 'Call ID%'
           OR Q.Pregunta LIKE 'ID%llamada%'
           OR Q.Pregunta LIKE 'ID de Grabaci%'
           OR Q.Pregunta LIKE 'ID%Conversaci%')
      AND LEN(LTRIM(RTRIM(R.Respuesta))) >= 6
    GROUP BY R.IdGestion
),

-- Alertas de la ventana con un margen: una T0 se puede reportar antes de la auditoría.
Alertas AS
(
    SELECT
        A.ID,
        LTRIM(RTRIM(A.ID_Llamada)) AS ID_Llamada,
        A.Legajo_AG,
        A.Auditor_Legajo,
        A.Fecha_reporte_T0,
        A.Infraccion,
        A.Gravedad,
        A.Estado,
        A.Accion,
        A.ID_Accion,
        A.Fecha_Gestionado,
        A.Nombre_Gestionador,
        A.Cargo_Gestionador
    FROM RecursosHumanos.Legal.Alertas_T0 A
    WHERE A.Fecha_reporte_T0 >= DATEADD(DAY, -60, CAST(@Desde AS DATETIME))
)

SELECT
    T0.Fecha,
    T0.Legajo AS legajo,
    N.sec_descrip AS Sector,
    N.Super,
    N.Team,
    N.Agente,
    T0.IdGestion,
    L.ID_Llamada,
    CAST(T0.LegajoAuditor AS VARCHAR(200)) AS CorreoAuditor,
    CAST(T0.Auditor AS VARCHAR(200)) AS Nombre_Auditor,
    CAST(T0.CargoAuditor AS VARCHAR(200)) AS Cargo_Auditor,
    T0.Plantilla,
    T0.Infraccion,
    CAST(TRY_CONVERT(NUMERIC(10,4), T0.Nota) / 100 AS NUMERIC(10,4)) AS Respuesta,

    AL.ID AS IdAlerta,
    AL.Cruce,
    AL.Fecha_reporte_T0 AS FechaReporteT0,
    AL.Gravedad,
    AL.Estado AS EstadoAlerta,
    AL.Accion,
    AL.ID_Accion,
    AL.Fecha_Gestionado AS FechaGestionado,
    AL.Nombre_Gestionador AS Gestionador,
    AL.Cargo_Gestionador AS CargoGestionador,

    PA.IdPlanDeAccion AS IdPlan,
    PA.FechaCreacion AS FechaPlan,
    PE.Estado AS EstadoPlan,
    PM.Motivo AS MotivoPlan,

    PC.IdPlanDeAccion AS IdPlanCalidad,
    PC.FechaCreacion AS FechaPlanCalidad

FROM T0

INNER JOIN Nomina N
    ON N.leg = T0.Legajo
    AND N.Fecha = T0.Fecha
    AND N.RN = 1

LEFT JOIN Llamada L
    ON L.IdGestion = T0.IdGestion

OUTER APPLY
(
    SELECT TOP 1
        A.*,
        CASE WHEN A.ID_Llamada = L.ID_Llamada THEN 'Llamada' ELSE 'Agente, auditor y fecha' END AS Cruce
    FROM Alertas A
    WHERE (L.ID_Llamada IS NOT NULL AND A.ID_Llamada = L.ID_Llamada)
       OR (A.Legajo_AG = T0.Legajo
           AND A.Auditor_Legajo = T0.LegajoAuditor
           AND CAST(A.Fecha_reporte_T0 AS DATE) BETWEEN DATEADD(DAY, -1, T0.Fecha) AND DATEADD(DAY, 15, T0.Fecha))
    ORDER BY
        CASE WHEN A.ID_Llamada = L.ID_Llamada THEN 0 ELSE 1 END,
        CASE WHEN A.Infraccion = T0.Infraccion THEN 0 ELSE 1 END,
        ABS(DATEDIFF(DAY, T0.Fecha, A.Fecha_reporte_T0))
) AL

LEFT JOIN RecursosHumanos.Legal.PlanAccion PA
    ON AL.Accion LIKE 'Plan de acci%'
    AND AL.ID_Accion > 0
    AND PA.IdPlanDeAccion = AL.ID_Accion

LEFT JOIN RecursosHumanos.Legal.PlanAccionEstados PE
    ON PE.IdEstado = PA.IdEstado

LEFT JOIN RecursosHumanos.Legal.PlanAccionMotivos PM
    ON PM.IdMotivo = PA.IdMotivo

OUTER APPLY
(
    SELECT TOP 1 P.IdPlanDeAccion, P.FechaCreacion
    FROM RecursosHumanos.Legal.PlanAccion P
    WHERE P.LegajoAgente = T0.Legajo
      AND P.IdMotivo = 1020
      AND CAST(P.FechaCreacion AS DATE) BETWEEN T0.Fecha AND DATEADD(DAY, 15, T0.Fecha)
    ORDER BY P.FechaCreacion
) PC;

-- Segundo resultado: planes creados cada día en Legal.PlanAccion desde un mes antes de la ventana.
-- La web lo usa para avisar de los huecos de la tabla (el 08-10-2026 no tenía ningún plan del
-- 14-07 al 30-09-2026): en esas fechas los planes de las alertas no se pueden comprobar.
SELECT
    CAST(P.FechaCreacion AS DATE) AS Dia,
    COUNT(*) AS Planes
FROM RecursosHumanos.Legal.PlanAccion P
WHERE P.FechaCreacion >= DATEADD(MONTH, -1, CAST(@Desde AS DATETIME))
GROUP BY CAST(P.FechaCreacion AS DATE);
