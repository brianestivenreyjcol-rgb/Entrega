-- GAIA Formación: hasta qué día hay llamadas de cada marca en la tabla de DataOrb (la tarjeta
-- «Actualización BD» de la portada del PBI). Se lee en cada carga.
SELECT
    brand AS Marca,
    MIN(day) AS PrimeraFecha,
    MAX(day) AS UltimaFecha
FROM `mo-vendor-management-reporting.JZZBOGOTA.ALL_dataorb_to_mo_insight_all_data_flattened_GAMMA`
WHERE serviceProvider IN ('JAZZBOG', 'JAZZTEL', 'JAZZPLAT')
GROUP BY brand
ORDER BY brand
