-- GAIA Formación: llamadas de los agentes en formación, solo en SUS días de preconexión y
-- aseguramiento (filtro exacto agente + día). La web pone dentro del UNNEST de «pares» la lista que
-- sale del Excel de nómina (hoja Oleadas): STRUCT('id' AS id_agente, DATE 'aaaa-mm-dd' AS dia), ...
-- (el marcador va solo una vez, ahí). La lanza por tandas de agentes (Gaia:AgentesPorConsulta).
-- Se lee en cada carga: editar este fichero y pulsar «Actualizar» basta.
-- Las traducciones al español se hacen en la web (TraduccionesGaia.cs), no aquí.
WITH pares AS (
    SELECT * FROM UNNEST([{{PARES}}])
),
-- Encuestas enviadas (YOIGO y MASMOVIL, colas JAZZBOG). El PBI tenía aquí una lista de fechas fija
-- que acababa el 17-06-2026; ahora salen de los mismos días del Excel.
encuestas AS (
    SELECT DISTINCT CONCAT(conversation_id, '_', CAST(agent_step_order AS STRING)) AS id_externo
    FROM `mm-cockpit-reporting.atc.REP_ALL_IVR_LLAMADAS_TIPIS`
    WHERE agent_brand IN ('YOIGO', 'MASMOVIL')
      AND agent_step_order IS NOT NULL
      AND agent_queue_name LIKE '%JAZZBOG%'
      AND survey_requested = TRUE
      AND day IN (SELECT DISTINCT dia FROM pares)
)
SELECT
    BD.conversation_id AS IdConversacion,
    BD.externalConversationId AS IdExterno,
    BD.day AS Fecha,
    FORMAT_TIMESTAMP('%Y-%m-%d %H:%M:%S', BD.conversationDateTime, 'America/Bogota') AS FechaHora,
    BD.primaryAgentId AS IdAgente,
    BD.customerId AS IdCliente,
    BD.brand AS Marca,
    BD.duration AS DuracionSegundos,
    BD.nonTalkTime AS TiempoNoHablado,
    BD.talkRatio AS PorcentajeHabla,
    BD.overTalkCount AS VecesSePisaron,
    BD.context AS Contexto,
    BD.churnRiskAnalysis_churnRiskStatus AS RiesgoChurn,
    BD.commercialOfferInsights_hasSalesAttempt AS IntentoVenta,
    BD.contactReason_inboundSalesLead AS PosibleVentaEntrante,
    BD.contactReason_customerIntent AS RazonNivel1,
    BD.issueInquiryClassification_issueType AS TipoProblema,
    BD.issueInquiryClassification_inquiryType AS TipoConsulta,
    BD.customerSentimentAnalysis_initialSentiment AS SentimientoInicial,
    BD.customerSentimentAnalysis_finalSentiment AS SentimientoFinal,
    BD.openingGreeting_rating AS CalificacionSaludo,
    BD.problemSolvingStatements_rating AS CalificacionSolucion,
    BD.summaryOfInteraction_rating AS CalificacionResumen,
    BD.closingGreeting_rating AS CalificacionCierre,
    BD.confirmResolution_rating AS CalificacionConfirmacion,
    BD.clearLanguage_rating AS CalificacionLenguajeClaro,
    BD.acknowledgementStatement_rating AS CalificacionReconocimiento,
    -- Los dos resúmenes van enteros (lo pidió el usuario el 06-10-2026).
    BD.contactReason_contactReasonSummary AS ResumenContacto,
    BD.resolution_resolutionSummary AS ResumenResolucion,
    BD.resolution_resolutionIssueResolved AS ProblemaResuelto,
    BD.enh_Transfer AS Transferencia,
    BD.enh_Redial_72h AS Rellamada72h,
    -- Minutos hasta la siguiente llamada del cliente: da la rellamada en 24 h con el dato de origen
    -- (el PBI la calculaba solo con las llamadas descargadas).
    BD.enh_minutos_posterior_callid AS MinutosSiguienteLlamada,
    BD.enh_Resolution_request AS EncuestaSolucion,
    BD.interactionClassification_interactionType AS Motivo1,
    BD.interactionClassification_interactionBusinessScope AS Motivo2,
    BD.interactionScopeDetail_classification AS Motivo3,
    (SELECT el FROM UNNEST(BD.resolution_issueResolutionInsights_resolutionStatus) AS el
      WHERE el IS NOT NULL LIMIT 1) AS EstadoResolucion,
    -- Separados por « | »: las frases de DataOrb llevan comas.
    ARRAY_TO_STRING(ARRAY(SELECT el FROM UNNEST(BD.resolution_resolutionImpediments) AS el), ' | ') AS Obstaculos,
    (SELECT el FROM UNNEST(BD.commercialOfferInsights_salesOffers_salesOutcome) AS el
      WHERE el IS NOT NULL LIMIT 1) AS ResultadoVenta,
    (SELECT el FROM UNNEST(BD.commercialOfferInsights_salesOffers_offerCategory) AS el
      WHERE el IS NOT NULL LIMIT 1) AS CategoriaOferta,
    (SELECT el FROM UNNEST(BD.commercialOfferInsights_salesOffers_offerAlignment) AS el
      WHERE el IS NOT NULL LIMIT 1) AS AlineacionOferta,
    (SELECT el FROM UNNEST(BD.contactReason_contactReasonTopic) AS el
      WHERE el IS NOT NULL LIMIT 1) AS TemaContacto,
    (SELECT el FROM UNNEST(BD.resolution_issueResolutionInsights_resolutionTopicGroup) AS el
      WHERE el IS NOT NULL LIMIT 1) AS GrupoResolucion,
    BD.enh_Has_Sales_Bool AS TieneVenta,
    BD.enh_Servicetype_Sales AS TipoServicioVenta,
    e.id_externo IS NOT NULL AS EncuestaEnviada
FROM `mo-vendor-management-reporting.JZZBOGOTA.ALL_dataorb_to_mo_insight_all_data_flattened_GAMMA` BD
JOIN pares p ON BD.primaryAgentId = p.id_agente AND BD.day = p.dia
LEFT JOIN encuestas e ON e.id_externo = BD.externalConversationId
