-- Nómina por día, para la tarjeta «Total agentes» (agentes en nómina frente a auditados).
-- Es la misma cadena que el CTE «Nomina» de Auditorias.sql (Nomina_User_Avaya + NominaAntiguedad,
-- una fila de NominaAntiguedad por legajo y día), con el cargo para quedarse solo con los
-- agentes (Auditorias:CargosAgente en appsettings.json; el filtro se hace en la web).
-- @Desde y @Hasta: el rango de fechas de las auditorías cargadas (el calendario).
-- Columnas que lee la web, en este orden: Fecha, legajo, Sector, Super, Team, Cargo.

WITH NominaAntiguedad AS
(
    SELECT
        CAST(NA.fecha AS DATE) AS Fecha,
        NA.leg,
        NA.sec_descrip,
        NA.Super,
        NA.Team,
        NA.car_descrip,
        ROW_NUMBER() OVER
        (
            PARTITION BY
                CAST(NA.fecha AS DATE),
                NA.leg
            ORDER BY
                NA.leg
        ) AS RN
    FROM Planificacion.Nomina.NominaAntiguedad NA
    WHERE NA.fecha >= @Desde
      AND NA.fecha < DATEADD(DAY, 1, @Hasta)
)

SELECT DISTINCT
    CAST(PU.fecha AS DATE) AS Fecha,
    PU.legajo,
    NA.sec_descrip AS Sector,
    NA.Super,
    NA.Team,
    NA.car_descrip AS Cargo
FROM [Planificacion].[Nomina].[Nomina_User_Avaya] PU
INNER JOIN NominaAntiguedad NA
    ON PU.legajo = NA.leg
    AND CAST(PU.fecha AS DATE) = NA.Fecha
    AND NA.RN = 1
WHERE PU.fecha >= @Desde
  AND PU.fecha < DATEADD(DAY, 1, @Hasta);
