# GAIA Formación: análisis del Power BI y plan de migración

> Parte del contexto de CDM Auditorías Calidad (resumen en `CONTEXTO_IA.md`). Léelo cuando toques la
> vista GAIA Formación. Escrito el 06-10-2026, antes de empezar a programar.

## 1. Qué es

El Power BI `Power bi\GAIA Formación.pbip` (lo añadió el usuario el 06-10-2026) sigue a los agentes
nuevos durante su formación: las llamadas que hacen en sus días de **preconexión** y
**aseguramiento**, con las métricas de DataOrb (adherencia al estilo, rellamada, no solución, ventas,
motivos, «españolización» del vocabulario…). Se quiere como **vista nueva** de esta web.

## 2. De dónde salen los datos

| Tabla del PBI | Origen | Qué trae |
|---|---|---|
| `Nomina` | Excel `\\172.16.232.102\Proyecto Ranking\2.GAIA\Formación\Nomina Iniciales General.xlsx`, hoja **Oleadas** | Un agente por fila: Sector, Supervisor, Coordinador, Personal, Cargo, Legajo, `1/2/3 Preconexion`, `Aseguramiento 1…6`, Formador, Oleada, Login, `ID_Agente ` (con espacio al final), Ventas, CDA. El PBI lo despivota: una fila por agente y fecha con su «Tipo de Conexión». |
| `DatosBQ` | BigQuery (DSN `BQCOL`), `mo-vendor-management-reporting.JZZBOGOTA.ALL_dataorb_to_mo_insight_all_data_flattened_GAMMA` | Una fila por llamada, ~70 columnas de DataOrb, traducidas al español en Power Query. |
| `Encuestas` | BigQuery, `mm-cockpit-reporting.atc.REP_ALL_IVR_LLAMADAS_TIPIS` (YOIGO y MASMOVIL, colas JAZZBOG, encuesta pedida) | Se cruza con `DatosBQ` por `conversation_id + "_" + agent_step_order` = `externalConversationId`. |
| `Actualización de llamadas` | BigQuery, la misma GAMMA | Primera y última fecha por marca (la portada «Actualización BD»). |
| `Españolización` | BigQuery, `mo-customer-ops-reporting.smartops_prod.ALL_dataorb_to_mo_insight_all_data_flattened_GAMMA` | Lee la transcripción (`messages`) del agente y marca 41 palabras: 18 de España («vale», «coger», «móvil»…) y 23 de Colombia («qué pena», «celular», «de pronto»…). |

**Cómo se actualiza hoy:** `Generador.py` (en la misma carpeta compartida) lee el Excel y escribe
`Consulta.sql` con dos listas a mano: todas las fechas del Excel y todos los `ID_Agente`. Esas listas
se pegan en la consulta del PBI (`DatosBQ` y `Españolización`). `Encuestas` tiene otra lista de
fechas a mano que no se ha vuelto a pegar desde junio.

**En la web:** la web lee el Excel directamente, arma ella el filtro y consulta BigQuery. Con
**guardar el Excel** basta: no hay que tocar SQL ni ejecutar el generador.

## 3. Lo que hay que saber antes de copiar el PBI (problemas encontrados)

1. **Filtro cruzado: 8 veces más llamadas de las que tocan.** El PBI pide «cualquier fecha del Excel»
   × «cualquier agente del Excel», así que trae llamadas de un agente en días de formación de *otros*.
   Medido el 06-10-2026 con el Excel de ese día (356 agentes con ID, 97 fechas, 2.408 pares
   agente-día): **110.718** llamadas con el filtro del PBI frente a **13.461** con el filtro exacto
   (cada agente solo en sus días); 307 agentes tienen llamadas. El PBI solo recorta a lo exacto
   cuando se filtra por un campo de la nómina (la relación es por la llave `fecha + id`); sin
   filtros, los totales cuentan las 110.718. **Decisión del usuario** (sección 6).
2. **«Abruptas» siempre vale 0**: la consulta pone `0 AS interactionFlags_abruptlyEnded`. Por eso
   «% Abruptas» y «Llamadas cortadas» salen siempre a 0 y la regla «si la llamada se cortó, el
   criterio es N/A» de la adherencia nunca se aplica. En GAMMA existe el campo de verdad.
3. **«% Envío encuestas» vacío desde julio**: la lista de fechas de `Encuestas` acaba el 17-06-2026.
4. **«Españolización»** tiene sus fechas e ID pegados a mano (los mismos que `DatosBQ`) y lee
   transcripciones completas: es la consulta más pesada.
5. **Denominadores raros**: `% Transferencia` divide entre `Transferencia = 1 || Rellamada72hr = 0`
   y `% Rellamada 24h` entre `Rellamada24hr = 1 || Rellamada72hr = 0` (lo lógico sería el mismo campo
   = 0). Preguntar antes de corregir.
6. `Rellamada24hr` es una columna calculada que busca otra llamada del mismo cliente en las 24 h
   anteriores **dentro de lo descargado**: cambia si cambia el filtro (punto 1).
7. **Campos rotos** en páginas copiadas de otro informe: `Team`, `Mes`, `Semana`, `PLATAFORMA`,
   `sec_descrip`, `AntiguedadLinea`, `Color de Letra por Marca`, `FECHA_LLAMADA`, `fecha_seg`.
   No existen en el modelo: esos segmentadores no hacen nada.
8. **El Excel**: 23 filas sin `ID_Agente` (no salen en BigQuery), 8 ID repetidos, `CDA` vacío,
   `Aseguramiento 4–6` solo en parte de las filas (el generador las usa; el PBI las despivota sin
   tipo). La segunda hoja (`Hoja1`) guarda **usuarios y contraseñas**: la web solo leerá `Oleadas`
   y el contenido de `Hoja1` no se copia ni se documenta.

## 4. Medidas (cómo pasarlas)

- **Adherencia al estilo** = Saludo 10 % + Soy claro y fiable 15 % + Solucionó 25 % + Resumió 20 %
  + Confirmó solución 20 % + Despedida 10 %. Cada criterio sale de la calificación de DataOrb
  (`yes` → 1, otro → 0, N/A si la llamada se cortó). Aparte, sin peso: Escuchó (habla < 80 %, se
  pisaron < 10 veces, reconoció al cliente) y Buscó información (silencio ≤ 50 % de la duración).
  Colores de la barra: ≤ 40 % naranja, ≤ 70 % rojo, verde por encima.
- **KPIs**: % Rellamada 72 h (`enh_Redial_72h` 1 / 1+0), % No solución (`enh_Resolution_request`
  2 / 1+2), % Transferencia, % Churn (riesgo «Alto» / total), % Ofrecimientos (intento de venta /
  llamadas), reactivo/proactivo (con o sin `inboundSalesLead`), alineados/no alineados, % Ventas
  (`enh_Has_Sales_Bool`), sentimiento final positivo/negativo, TMO (media de `duration`), llamadas
  < 60 s, 60–180 s, sin contexto, % resuelto/pendiente/no se puede cumplir, % envío de encuestas.
- Los «parámetros de campo» del PBI (elegir qué KPI se ve en el eje) pasan a un desplegable.
- Las traducciones de Power Query (riesgo, sentimiento, motivos, resultado de venta…) se pasan a un
  diccionario en C#, igual que en No solución.

## 5. Páginas del PBI (17; solo «GAIA» es visible, el resto por botones)

| Página | Qué enseña | Fase |
|---|---|---|
| GAIA (portada) | Botones a cada página y «Actualización BD» por marca | 2 |
| Base | Tabla de llamadas con todos los campos y filtros | 2 |
| Ranking | Agentes con todos los KPI («Ranking general» y «Tipológico») | 2 |
| Estilo / Estilo Tabla | Adherencia y sus criterios: donas, barra, tabla por agente y fecha | 2 |
| Rendimiento | TMO, cortas, sin contexto, rellamada por día/semana | 3 |
| Cronológico / Comparativo | KPI elegido por día; agentes en dispersión | 3 |
| Ventas / Detalle Comercial / Alineamientos | Ofrecimientos, ventas, resultado, alineación | 3 |
| Motivos de Contacto / Mapa de afectación | Motivos (zoom 1-2-3), obstáculos, KPI por motivo | 3 |
| Españolización (+ Novato, Aficionado, Experto) | Uso de palabras de España y Colombia por agente | 4 |

Filtros comunes: Fecha, Marca, Sector, Oleada, Formador, Supervisor, Agente y Tipo de conexión
(Preconexión 1-3, Aseguramiento 1-6).

## 6. Decisiones del usuario (06-10-2026)

1. **Filtro exacto**: cada agente solo en sus días del Excel (no el cruce del PBI).
2. **Corregir los fallos** del PBI:
   - % Transferencia = Transferencia 1 / Transferencia 0 o 1.
   - % Rellamada 24 h = rellamada 72 h oficial (`enh_Redial_72h` = 1) cuya siguiente llamada llega en
     ≤ 24 h (`enh_minutos_posterior_callid` ≤ 1440), sobre la base de la de 72 h. No se usan solo los
     minutos: daban 3.653 rellamadas «de 72 h» frente a 2.365 oficiales.
   - Encuestas: sin lista de fechas fija (salen de los días del Excel), dentro de la consulta principal.
   - **Abruptas / llamadas cortadas: no se pueden corregir.** El campo `interactionFlags_abruptlyEnded`
     no existe ni en la GAMMA de JZZBOGOTA ni en la de smartops_prod (comprobado en
     INFORMATION_SCHEMA). La web no enseña esos KPI.
3. Dirección **`/gaia`**, con tarjeta en la portada.
4. Las pantallas las hace el agente `frontend-solaris` (pedido del usuario).

**Ojo con «envío de encuestas»**: la tabla del IVR solo marca como pedidas unas 400 de las ~8.100
llamadas de YOIGO y MASMOVIL, aunque 3.600 tienen respuesta en `enh_Resolution_request`. Es el
método del PBI; el número es bajo por la fuente, no por la web.

## 6 bis. Lo construido (fases 1 y 2)

- `Consultas/GaiaLlamadas.sql` (marcador `{{PARES}}` una sola vez; el resumen de contacto va entero —lo pidió el usuario—, el de resolución recortado a 600)
  y `Consultas/GaiaActualizacion.sql`. Se lanzan por tandas de 60 agentes (`Gaia:AgentesPorConsulta`):
  con todos de golpe el driver se cortaba («Failure when receiving data from the peer»).
- `Servicios/Gaia/`: `NominaGaia.cs` (lector del Excel, ClosedXML, solo la hoja Oleadas, abierto en
  modo compartido), `FuenteGaia.cs` (consulta + conversión), `TraduccionesGaia.cs`, `CalculadoraGaia.cs`
  (medidas), `FiltrosGaia.cs` (desplegables en cascada, mismo `GrupoFiltro` que el resto),
  `ServicioGaia.cs` (caché `App_Data/cache_gaia.json` — en producción `publicacion\datos` —, recarga en
  segundo plano y `RevisionGaia`, que cada 5 min mira la fecha del Excel).
- Se recarga cuando cambia la fecha de modificación del Excel, cada 12 h y con «Actualizar»; tras un
  fallo no reintenta solo en 15 min.
- `Controllers/GaiaController.cs` y `Models/Gaia/PaginaGaia.cs`: Resumen, Ranking, Estilo, Llamadas y
  CSV. Pruebas: `GaiaTests.cs` (15).
- Primera carga real (06-10-2026 10:36): 13.529 llamadas, 356 agentes, 2.408 días; caché de 40 MB;
  unos 40 s con las tandas. `talkRatio` viene en escala 0–100 (mediana 49), como supone «Escuchó».

## 6 ter. Vistas (06-10-2026, agente frontend-solaris)

- `Views/Gaia/`: `_LayoutGaia`, `_InformeGaia` (pestañas, avisos, preparando), `_PanelGaia` (filtros y pie con el estado de los
  datos), `Resumen`, `Ranking`, `Estilo`, `Llamadas` y las gráficas `_LineasGaia` y `_ColumnasGaia` (antes había tres parciales de gráficas propios; ver la tercera tanda).
  Ayudantes de presentación en `Views/Gaia/AyudasGaia.cs` (etiquetas de etapa y motivo, Sí/No/N/A, m:ss, clases de umbral).
- Piezas y clases nuevas: sección 9.6 de `docs/guia-de-estilos.md`. Tarjeta «GAIA Formación» en la portada.
- Los motivos llegan sin traducir en camelCase con «Or» (`incidenciaOrReclamación`); la vista los pasa a «Incidencia o
  reclamación». Obstáculos de DataOrb como «String», «Not applicable» o «NA» no se enseñan.
- Las filas de la pestaña Llamadas pesan unos 8 KB (resúmenes dentro): la página de 100 llamadas es de unos 870 KB.

### Vistas de la segunda tanda (06-10-2026)

- Nuevas en `Views/Gaia/`: `Rendimiento`, `Evolucion`, `Comercial`, `Motivos`, `Espanolizacion` y los parciales `_LineasGaia`,
  `_ColumnasGaia`, `_DispersionGaia`, `_SelectorGaia`, `_RepartoGaia`, `_ApiladaGaia`, `_NodoMotivoGaia`, `_TextoLargoGaia`. Piezas y
  clases: sección 9.7 de `docs/guia-de-estilos.md`.
- Llamadas: los obstáculos van en lista (sin «NA», «String»…) y los resúmenes enteros, en párrafos con su rótulo.
- Las páginas pesan: Llamadas, alrededor de 1,1 MB por 100 filas (resúmenes enteros); Ranking, Estilo, Rendimiento y Comercial,
  de 450 a 550 KB (tablas de unos 300 agentes y todos los desplegables de filtro).

## 6 quater. Fases 3 y 4: datos (06-10-2026)

- Pestañas nuevas: **Rendimiento**, **Evolución** (Cronológico + Comparativo), **Comercial** (Ventas,
  Detalle Comercial, Alineamientos), **Motivos** (Motivos de Contacto + Mapa de afectación) y
  **Españolización** (con los niveles Novato, Aficionado y Experto). Cálculos en
  `Servicios/Gaia/AnalisisGaia.cs`; modelos al final de `Models/Gaia/PaginaGaia.cs`. Pruebas:
  `GaiaAnalisisTests.cs` (312 en verde).
- Los dos resúmenes (contacto y resolución) van **enteros** (pedido del usuario); los obstáculos se
  separan por « | » (con «, » se partían frases). Repartos sin valores vacíos (NA, String, No aplica…);
  en Temas, que es texto libre, sin «Otros» (se llevaba el 89 %).
- **Españolización**: `Consultas/GaiaEspanolizacion.sql` sobre la GAMMA de smartops_prod (transcripción
  en `messages`), por tandas, con los marcadores `{{PARES}}` y `{{PALABRAS}}` una sola vez. La lista de
  palabras está en `Datos/palabras_gaia.json` (pares España ↔ Colombia y nivel; editable, se aplica al
  «Actualizar»). Los niveles salen de las tarjetas de las páginas del PBI: Novato 7 pares, Aficionado
  +3, Experto +2; el resto, solo en «Todas».
- **Correcciones frente al PBI** en españolización:
  1. El PBI quitaba las líneas del hablante «1:» como si fuera el cliente, pero los números de hablante
     no son fijos (0–4, comprobado contando frases de agente por hablante). Ahora el agente de cada
     llamada es el hablante con más frases de agente («en qué le puedo ayudar», «mi nombre es», «le
     atiende», «gracias por llamar», «bienvenid…»); sin ninguna, la llamada no cuenta.
  2. Palabras enteras (`\b`): el PBI contaba «dar» en «andar», «vale» en «equivale»…
  3. «piso» es de España y «apartamento» de Colombia (el PBI los tenía al revés).
- Cobertura (06-10-2026): 9.305 de 13.529 llamadas tienen transcripción; en 9.169 (98,5 %) se identifica
  al agente. Novato: 85,16 % de las llamadas con alguna palabra de España, 37,17 % con alguna de Colombia.
- Si la españolización falla, el resto se carga igual y la página enseña el aviso (`AvisosCarga`).
- Caché v2 (60 MB). Pendiente de valorar: cargar el detalle de Llamadas bajo demanda (1,1 MB por página
  de 100) y una tabla de traducción de los motivos (llegan en camelCase; la vista los arregla a medias).

### Vistas, tercera tanda (06-10-2026)

- Pastillas con el valor de cada punto en todas las gráficas (sin «máx.», «mín.» ni media rotulada), leyenda arriba y centrada, eje derecho si
  dos series tienen escalas muy distintas, y selector Semana / Día en Rendimiento y Evolución. Resumen, Estilo, Comercial y Españolización no tienen
  `PorSemana` en su modelo: ahí, el Día rotula uno de cada pocos puntos.
- TOTAL al pie en todas las tablas; color de la página según la marca filtrada (`data-marca`: orange, ygmm, jazztel). Sección 9.8 de `docs/guia-de-estilos.md`.
- Los parciales `_ColumnasEtapaGaia`, `_LineaDiaGaia` y `_VolumenDiaGaia` desaparecen: todo pasa por `_LineasGaia` y `_ColumnasGaia`
  (el volumen es `GraficoVolumen`).

## 7. Plan de trabajo

**Fase 0 — Decisiones y base (½ día).** Respuestas de la sección 6. Copia del Excel para pruebas en
`App_Data` (sin versionar). Anotar las cifras del PBI de un periodo para cuadrar (fase 5).

**Fase 1 — Datos (1–2 días).**
- `LectorNominaGaia` (ClosedXML, ya está en el proyecto): abre el Excel en solo lectura (o una copia
  temporal si está abierto en Excel), solo la hoja `Oleadas`, recorta espacios de los encabezados y
  devuelve los agentes y los pares (agente, día, tipo de conexión). Avisa de filas sin ID o repetidas.
- `Consultas/GaiaLlamadas.sql`, `GaiaEncuestas.sql`, `GaiaActualizacion.sql`, `GaiaEspanolizacion.sql`
  con un marcador `{{PARES}}` que la web sustituye por la lista del Excel (`UNNEST` de pares y
  `JOIN`, o el cruce si el usuario lo prefiere). Se leen en cada carga, como las de auditorías.
- Fuente BigQuery por ODBC reutilizando lo de No solución (DSN `BQCOL`).
- Caché en disco (`App_Data\cache_gaia.json` / `publicacion\datos`): se rehace **cuando cambia la
  fecha de modificación del Excel** (se comprueba cada pocos minutos), cada 12 h (la rellamada mira
  3 días hacia delante y JAZZTEL/ORANGE llegan con ~5 días de retraso) y con «Actualizar». La
  consulta tarda ~20 s; la web sirve la caché anterior mientras tanto.
- `appsettings.json`: sección `Gaia` con la ruta UNC del Excel (no `Z:`), la hoja y las horas.

**Fase 2 — Cálculos y primeras vistas (2–3 días).**
- `CalculadoraGaia` con las medidas de la sección 4 y pruebas unitarias (una por medida).
- `GaiaController` en `/gaia` con los filtros comunes (mismo panel que el resto de la web).
- Vistas: portada con KPIs y «Actualización BD», Ranking, Estilo (adherencia) y Base (tabla con
  descarga a Excel). Front con el agente `frontend-solaris` y la guía de estilos.

**Fase 3 — Resto de páginas (2–3 días).** Rendimiento, Cronológico/Comparativo, Ventas/Alineamientos,
Motivos y Mapa de afectación.

**Fase 4 — Españolización (1–2 días).** Consulta aparte (transcripciones) con su propia caché;
palabras en una lista configurable; niveles Novato/Aficionado/Experto por agente.

**Fase 5 — Cuadre y publicación (1 día).** Comparar con el PBI el mismo periodo y filtro (sabiendo
que el filtro exacto cambia los totales), probar en el 5190, documentar en `verificacion.md` y
publicar en el 5180 solo cuando el usuario lo pida.

## 8. Riesgos

- La web corre en el PC del usuario: necesita acceso a `\\172.16.232.102` y su DSN `BQCOL`
  (dependencias personales, igual que No solución).
- Si alguien cambia el nombre de la hoja o de una columna del Excel, la vista avisa y sigue con la
  última caché buena.
- Un agente sin `ID_Agente` en el Excel no sale: la vista lo listará como aviso.
