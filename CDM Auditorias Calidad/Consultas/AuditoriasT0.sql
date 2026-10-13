-- Auditorías con T0 frente a [Reporting].[pdc].[Reportar_T0], para la pestaña «T0 y planes de acción».
-- Regla de negocio: cada auditoría de calidad con T0 tiene que estar reportada en Reportar_T0 (y
-- allí, gestionada con un plan de acción). El usuario pidió el 13-10-2026 usar SOLO Reportar_T0 del
-- lado de las T0 (ya no Legal.Alertas_T0 ni Legal.PlanAccion) y verlo por técnico de calidad /
-- auditor: «tiene 10 auditorías, 5 con T0 y las 5 están en Reportar_T0».
--
-- Dos resultados (detalle en docs/contexto/t0-planes.md):
--   1. Auditorías con T0: ICEBERG, Calidad.PlantillaCalidadUnificada con [Tolerancia 0] rellena (ni
--      vacía ni «N/A»; los formularios WEB no tienen ese campo). El ID de la llamada sale de las
--      respuestas de la plantilla (PlanillaRespuestasP + PlanillaPreguntasP). IdReporte = la fila
--      de Reportar_T0 que le corresponde: primero por el ID de la llamada; si no, el mismo agente y
--      el mismo auditor con el reporte entre 1 día antes y 15 después de la auditoría.
--   2. Reportar_T0 entero (unas 8.500 filas desde el 28-02-2026): la acción tomada, el plan…
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

Reportes AS
(
    SELECT
        R.ID,
        LTRIM(RTRIM(R.ID_Llamada)) AS ID_Llamada,
        R.Legajo_AG,
        R.Auditor_Legajo,
        R.Fecha_reporte_Alerta,
        LTRIM(RTRIM(R.Infraccion)) AS Infraccion
    FROM Reporting.pdc.Reportar_T0 R
    WHERE R.Fecha_reporte_Alerta >= DATEADD(DAY, -60, CAST(@Desde AS DATETIME))
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
    T0.LegajoAuditor,
    CAST(T0.Auditor AS VARCHAR(200)) AS Nombre_Auditor,
    CAST(T0.CargoAuditor AS VARCHAR(200)) AS Cargo_Auditor,
    T0.Plantilla,
    T0.Infraccion,
    CAST(TRY_CONVERT(NUMERIC(10,4), T0.Nota) / 100 AS NUMERIC(10,4)) AS Respuesta,
    RP.ID AS IdReporte,
    RP.Cruce
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
        R.ID,
        CASE WHEN R.ID_Llamada = L.ID_Llamada THEN 'Llamada' ELSE 'Agente, auditor y fecha' END AS Cruce
    FROM Reportes R
    WHERE (L.ID_Llamada IS NOT NULL AND R.ID_Llamada = L.ID_Llamada)
       OR (R.Legajo_AG = T0.Legajo
           AND R.Auditor_Legajo = T0.LegajoAuditor
           AND CAST(R.Fecha_reporte_Alerta AS DATE) BETWEEN DATEADD(DAY, -1, T0.Fecha) AND DATEADD(DAY, 15, T0.Fecha))
    ORDER BY
        CASE WHEN R.ID_Llamada = L.ID_Llamada THEN 0 ELSE 1 END,
        CASE WHEN R.Infraccion = T0.Infraccion THEN 0 ELSE 1 END,
        ABS(DATEDIFF(DAY, T0.Fecha, R.Fecha_reporte_Alerta))
) RP;

-- Segundo resultado: Reportar_T0 entero (la web filtra por la fecha del reporte).
SELECT
    R.ID,
    R.Fecha_reporte_Alerta,
    R.Fecha_Falta,
    R.Login,
    R.Legajo_AG,
    R.Sector,
    R.Supervisor,
    R.Team,
    R.Agente,
    LTRIM(RTRIM(R.ID_Llamada)) AS ID_Llamada,
    LTRIM(RTRIM(R.Infraccion)) AS Infraccion,
    R.Gravedad,
    R.Observaciones_Reporte,
    R.Auditor_Legajo,
    R.Nombre_Auditor,
    R.Cargo_Auditor,
    R.Estado,
    R.Fecha_Gestionado,
    R.Accion,
    R.ID_Accion,
    R.Observaciones_Respuesta,
    R.Nombre_Gestionador,
    R.Cargo_Gestionador,
    R.compromiso_nocumplido,
    R.expediente,
    R.MotivoLlamada,
    R.OportunidadMejora
FROM Reporting.pdc.Reportar_T0 R
WHERE R.Fecha_reporte_Alerta IS NOT NULL;
