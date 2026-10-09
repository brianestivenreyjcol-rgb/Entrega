-- Consulta de la tabla «Auditorias» del Power BI «CDM Auditorias Calidad Prueba»
-- (Power bi/…SemanticModel/definition/tables/Auditorias.tmdl; el usuario quitó la carpeta del
-- PBI del proyecto el 02-10-2026, sigue en el historial de git). Copiada tal cual, salvo:
--   - se quitó el ORDER BY final (la web ordena en memoria);
--   - ICEBERG trae 3 meses atrás + el mes en curso («- 3»; en el PBI era «- 2»): lo cambió el
--     usuario el 02-10-2026.
-- La web la lee en cada carga: un cambio aquí vale con pulsar «Actualizar».
-- Columnas que lee la web, en este orden: Fecha, legajo, Sector, Super, Team, Agente,
-- ID_Llamada, CorreoAuditor, Nombre_Auditor, Cargo_Auditor, Respuesta, Base.

WITH NominaAntiguedad AS
(
    SELECT
        CAST(NA.fecha AS DATE) AS Fecha,
        NA.leg,
        NA.sec_descrip,
        NA.Super,
        NA.Team,
        NA.Agente,
        ROW_NUMBER() OVER
        (
            PARTITION BY 
                CAST(NA.fecha AS DATE),
                NA.leg
            ORDER BY 
                NA.leg
        ) AS RN
    FROM Planificacion.Nomina.NominaAntiguedad NA
),

Nomina AS
(
    SELECT 
        PU.fecha,
        PU.legajo,
        PU.usuario,
        NA.sec_descrip,
        NA.Super,
        NA.Team,
        NA.Agente
    FROM [Planificacion].[Nomina].[Nomina_User_Avaya] PU

    LEFT JOIN NominaAntiguedad NA
        ON PU.legajo = NA.leg
        AND CAST(PU.fecha AS DATE) = NA.Fecha
        AND NA.RN = 1

    WHERE PU.fecha IS NOT NULL
),

Auditores AS
(
    SELECT
        NA.leg,
        NA.Agente AS Nombre_Auditor,
        NA.car_descrip,
        PD.Correo,

        ROW_NUMBER() OVER
        (
            PARTITION BY 
                NA.leg,
                LOWER(LTRIM(RTRIM(PD.Correo)))
            ORDER BY 
                NA.leg
        ) AS RN

    FROM Planificacion.Nomina.NominaAntiguedad NA

    LEFT JOIN [Planificacion].[Nomina].[PD_Usuarios] PD
        ON NA.leg = PD.legajo

    WHERE PD.Correo IS NOT NULL
),

Auditorias AS
(
    SELECT   
        CAST([Fecha Auditoria] AS DATE) AS Fecha, 
        CAST([Identificador Conversacion] AS VARCHAR(100)) AS ID_Llamada, 
        CAST(Agente AS VARCHAR(200)) AS Agente, 
        Auditor, 
        Instancia, 
        Campana,

        CASE
            WHEN TRY_CONVERT(NUMERIC(10,4), REPLACE(CAST(Respuesta AS VARCHAR(50)), '%', '')) > 1
                THEN TRY_CONVERT(NUMERIC(10,4), REPLACE(CAST(Respuesta AS VARCHAR(50)), '%', '')) / 100
            ELSE TRY_CONVERT(NUMERIC(10,4), REPLACE(CAST(Respuesta AS VARCHAR(50)), '%', ''))
        END AS Respuesta,

        'WHATSAPP' AS Marca 
    FROM Reporting.WO.AuditoriasWhatsapp 

    UNION ALL
	
    SELECT   
        CAST([Fecha Auditoria] AS DATE) AS Fecha, 
        CAST([Verint Contact ID] AS VARCHAR(100)) AS ID_Llamada, 
        CAST(Agente AS VARCHAR(200)) AS Agente, 
        Auditor, 
        Instancia, 
        Campana,

        CASE
            WHEN TRY_CONVERT(NUMERIC(10,4), REPLACE(CAST(Respuesta AS VARCHAR(50)), '%', '')) > 1
                THEN TRY_CONVERT(NUMERIC(10,4), REPLACE(CAST(Respuesta AS VARCHAR(50)), '%', '')) / 100
            ELSE TRY_CONVERT(NUMERIC(10,4), REPLACE(CAST(Respuesta AS VARCHAR(50)), '%', ''))
        END AS Respuesta,

        'JAZZTEL' AS Marca 
    FROM Reporting.WO.AuditoriasJazztel 
 
    UNION ALL 
 
    SELECT   
        CAST([Fecha Auditoria] AS DATE) AS Fecha, 
        CAST([Verint Contact ID] AS VARCHAR(100)) AS ID_Llamada, 
        CAST(Agente AS VARCHAR(200)) AS Agente, 
        Auditor, 
        Instancia, 
        Campana,

        CAST(
            (
                (CASE WHEN [Saludo Estilo] IN ('SI', 'N/A') THEN 1.0 ELSE 0.0 END * 0.10) +
                (CASE WHEN [Soy Claro Fiable] IN ('SI', 'N/A') THEN 1.0 ELSE 0.0 END * 0.15) +
                (CASE WHEN [Soluciono] IN ('SI', 'N/A') THEN 1.0 ELSE 0.0 END * 0.25) +
                (CASE WHEN [Resumo] IN ('SI', 'N/A') THEN 1.0 ELSE 0.0 END * 0.20) +
                (CASE WHEN [Pregunta Solucion] IN ('SI', 'N/A') THEN 1.0 ELSE 0.0 END * 0.20) +
                (CASE WHEN [Despedida Estilo] IN ('SI', 'N/A') THEN 1.0 ELSE 0.0 END * 0.10)
            ) AS NUMERIC(10,4)
        ) AS Respuesta,

        'ORANGE' AS Marca 

    FROM Reporting.WO.AuditoriasOrange
),

WEB AS
(
    SELECT
        A.Fecha,
        N.legajo,
        N.sec_descrip AS Sector,
        N.Super,
        N.Team,
        N.Agente,

        A.ID_Llamada,
		CASE
			WHEN LOWER(LTRIM(RTRIM(A.Auditor))) LIKE '%@masorange.es'
				THEN LEFT(
					LOWER(LTRIM(RTRIM(A.Auditor))),
					CHARINDEX('@', LOWER(LTRIM(RTRIM(A.Auditor)))) - 1
				) + '@orange.es'
			ELSE LOWER(LTRIM(RTRIM(A.Auditor)))
		END AS CorreoAuditor,
        AU.Nombre_Auditor,
        AU.car_descrip AS Cargo_Auditor,

        A.Respuesta,
        'WEB' AS Base

    FROM Nomina N

    INNER JOIN Auditorias A
        ON CAST(N.Fecha AS DATE) = CAST(A.Fecha AS DATE)
        AND N.usuario = A.Agente

    LEFT JOIN Auditores AU
    ON
    CASE
        WHEN LOWER(LTRIM(RTRIM(A.Auditor))) LIKE '%@masorange.es'
            THEN LEFT(
                LOWER(LTRIM(RTRIM(A.Auditor))),
                CHARINDEX('@', LOWER(LTRIM(RTRIM(A.Auditor)))) - 1
            ) + '@orange.es'
        ELSE LOWER(LTRIM(RTRIM(A.Auditor)))
    END
    =
    CASE
        WHEN LOWER(LTRIM(RTRIM(AU.Correo))) LIKE '%@masorange.es'
            THEN LEFT(
                LOWER(LTRIM(RTRIM(AU.Correo))),
                CHARINDEX('@', LOWER(LTRIM(RTRIM(AU.Correo)))) - 1
            ) + '@orange.es'
        ELSE LOWER(LTRIM(RTRIM(AU.Correo)))
    END
    AND AU.RN = 1
),

ICEBERG AS
(
    SELECT
        CAST(IC.[Fecha Auditoria] AS DATE) AS Fecha,
        IC.[Legajo] AS legajo,

        N.sec_descrip AS Sector,
        N.Super,
        N.Team,
        N.Agente,

        CAST(NULL AS VARCHAR(100)) AS ID_Llamada,

        CAST(IC.[Legajo Auditor] AS VARCHAR(200)) AS CorreoAuditor,
        CAST(IC.[Auditor] AS VARCHAR(200)) AS Nombre_Auditor,
        CAST(IC.[Cargo Auditor] AS VARCHAR(200)) AS Cargo_Auditor,

        CAST(
            TRY_CONVERT(NUMERIC(10,4), IC.[Nota Calidad]) / 100
            AS NUMERIC(10,4)
        ) AS Respuesta,

        'ICEBERG' AS Base

    FROM [ModulosIceberg].[Calidad].[PlantillaCalidadUnificada] IC

    LEFT JOIN Nomina N
        ON CAST(IC.[Fecha Auditoria] AS DATE) = CAST(N.Fecha AS DATE)
        AND IC.[Legajo] = N.legajo

    WHERE IC.[Fecha Auditoria] >= DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()) - 3, 0)
      AND IC.[Fecha Auditoria] < DATEADD(MONTH, DATEDIFF(MONTH, 0, GETDATE()) + 1, 0)
)

SELECT
    Fecha,
    legajo,
    Sector,
    Super,
    Team,
    Agente,
    ID_Llamada,
    CorreoAuditor,
    Nombre_Auditor,
    Cargo_Auditor,
    Respuesta,
    Base

FROM WEB

UNION ALL

SELECT
    Fecha,
    legajo,
    Sector,
    Super,
    Team,
    Agente,
    ID_Llamada,
    CorreoAuditor,
    Nombre_Auditor,
    Cargo_Auditor,
    Respuesta,
    Base

FROM ICEBERG
where Agente is not null;
