-- GAIA Formación, españolización: qué palabras de España y de Colombia dice el AGENTE en cada llamada.
-- La web pone la lista de agente + día del Excel en el UNNEST de «pares» y, en la última SELECT, la
-- expresión que devuelve los índices de las palabras encontradas («0,5,12,»), sacada de
-- Datos/palabras_gaia.json. Cada marcador va una sola vez, ahí. Se lanza por tandas de agentes.
--
-- Cambio frente al PBI: el PBI quitaba las líneas del hablante «1:» pensando que era el cliente, pero los
-- números de hablante no son fijos (comprobado el 06-10-2026). Aquí el agente de cada llamada es el
-- hablante que más frases de agente dice (saludo, «en qué le puedo ayudar», «mi nombre es»…); si nadie
-- las dice, la llamada queda sin agente identificado y no cuenta.
WITH pares AS (
    SELECT * FROM UNNEST([{{PARES}}])
),
lineas AS (
    SELECT
        b.conversation_id,
        REGEXP_EXTRACT(l, r'^\s*(\d+)\s*:') AS hablante,
        -- minúsculas, sin tildes y sin signos (se cambian por espacio para no pegar palabras)
        REGEXP_REPLACE(
            TRANSLATE(LOWER(REGEXP_REPLACE(l, r'^\s*\d+\s*:', '')), 'áéíóúü', 'aeiouu'),
            r'[^a-z0-9ñ\s]', ' ') AS texto
    FROM `mo-customer-ops-reporting.smartops_prod.ALL_dataorb_to_mo_insight_all_data_flattened_GAMMA` b
    JOIN pares p ON b.primaryAgentId = p.id_agente AND b.day = p.dia,
    UNNEST(SPLIT(b.messages, '\n')) l
),
frases AS (
    SELECT conversation_id, hablante,
        COUNTIF(REGEXP_CONTAINS(texto,
            r'(en que (le|te|lo|la) (puedo )?(ayudar|colaborar|servir)|mi nombre es|(le|te) atiende|(le|te) habla|gracias por (llamar|comunicar|su llamada|tu llamada)|bienvenid|algo mas en (que|lo que))'
        )) AS n
    FROM lineas
    WHERE hablante IS NOT NULL
    GROUP BY conversation_id, hablante
),
agente AS (
    SELECT conversation_id, ARRAY_AGG(hablante ORDER BY n DESC LIMIT 1)[OFFSET(0)] AS hablante
    FROM frases
    WHERE n > 0
    GROUP BY conversation_id
),
dicho AS (
    SELECT l.conversation_id, CONCAT(' ', STRING_AGG(l.texto, ' '), ' ') AS t
    FROM lineas l
    JOIN agente a ON a.conversation_id = l.conversation_id AND a.hablante = l.hablante
    GROUP BY l.conversation_id
)
SELECT
    c.conversation_id AS IdConversacion,
    a.hablante IS NOT NULL AS AgenteIdentificado,
    IF(d.t IS NULL, '', {{PALABRAS}}) AS Palabras
FROM (SELECT DISTINCT conversation_id FROM lineas) c
LEFT JOIN agente a ON a.conversation_id = c.conversation_id
LEFT JOIN dicho d ON d.conversation_id = c.conversation_id
