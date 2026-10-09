# CDM No solución

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del
> proyecto). Las referencias a «sección N» son de cuando todo estaba en un solo fichero:
> 1–2 → `power-bi.md` · 3 y 3 bis → `decisiones.md` · 3 ter → `no-solucion.md` ·
> 4–5 → `estructura-y-ejecucion.md` · 6 → `verificacion.md` · 7 → `pendientes.md` · 8 → `bitacora.md`.

## 3 ter. CDM No solución (desde el 05-10-2026)

El usuario pidió el 05-10-2026 «implementar lo de No solución del otro proyecto» aquí. Es la
pantalla `/cdm` de ranking-mvc (en producción allí, en el 5173), traída a `/nosolucion` con el
aspecto de este proyecto. **Allí sigue existiendo**: son dos copias independientes.

**Qué mide**: la encuesta de solución de las llamadas de Call Bogotá.
`% no solución = no solucionadas ÷ (solucionadas + no solucionadas)`, sobre llamadas
**entrantes encuestadas**. Universo: `channel = Call`; YOIGO y MASMOVIL en
`serviceProviderLocation = JAZZBOG`, JAZZTEL y ORANGE en `JZZ_BOGOTA`. El equipo sale de cruzar
`primaryAgentId` con la nómina (`NominaBogota`): por `DataOrb` en YOIGO/MASMOVIL y por la
extensión `Avaya` en JAZZTEL/ORANGE (la ficha «Cruce con nómina» debe rondar el 99,9 %; por
debajo del 99 % la llave estaría mal). Más detalle de negocio, en la bóveda de ranking-mvc:
`ranking-mvc\Documentacion\Negocio\Fuentes\CDM No solución.md`.

**De dónde sale**: BigQuery por ODBC (driver Simba, **DSN de usuario `BQCOL`**, con la cuenta de
Google del usuario), tablas `mo-vendor-management-reporting.JZZBOGOTA.ALL_dataorb_to_mo_insight_all_data_flattened_GAMMA`
y `…JZZBOGOTA.NominaBogota`. Se traen **90 días** hasta ayer en dos tramos de 45 (con 90 de golpe
el driver fallaba), se agregan en un **cubo** y se guardan en disco (~60 MB):

- Desarrollo: `CDM Auditorias Calidad\App_Data\cache_nosolucion.json` (ni se versiona ni se publica).
- Producción: `publicacion\datos\cache_nosolucion.json` (fuera de `publicacion\app`, para que
  `publicar.cmd` no la borre). Lo fija `appsettings.Production.json`.
- **Ninguna petición espera a BigQuery**: sin cubo, la página enseña «Preparando los datos» y se
  vuelve a pedir sola cada 10 s (la primera descarga tarda ~1 min). Con un cubo de más de 12 h, se
  sirve el que hay y se renueva en segundo plano. «Actualizar ahora» lo fuerza (como mucho una vez
  cada 30 s; aquí no hay inicio de sesión, así que lo puede pulsar cualquiera).
- **El driver no admite dos consultas a la vez** en el mismo proceso: todas pasan por un turno
  único (`FuenteBigQuery.Turno`), con timeout y tres reintentos.
- Los últimos 5 días son **provisionales** (las encuestas llegan tarde) y un día cuya extracción
  no llega al 80 % de la mediana de su día de la semana es **parcial** (línea discontinua).
- `Datos\impedimentos_tabla.json`: tabla fija de etiquetas de impedimento → categorías (la
  «aprendida del informe del 10/09»); viaja con el código.

**Pestañas** (todas con el mismo panel de filtros: fechas con atajos —todo, 7 días, 30 días, último
mes cerrado; **por defecto, los últimos 30 días**—, Sector (cola de entrada), Supervisor, Team
leader, Agente y Marca, en cascada; supervisor, TL y agente se encadenan):

- **Resumen**: 5 indicadores (no solución con su variación frente a los mismos días previos,
  no solucionadas, encuestadas con cobertura, llamadas entrantes, cruce con nómina), evolución
  diaria del % por marca (color fijo por marca), encuestas por día, los días que peor cerraron y
  «qué queda fuera de la cifra».
- **Equipos**: ranking por % de supervisores, TL o agentes (suelo de encuestas prorrateado al
  rango), buscador, orden por columna **en el servidor** (para que el CSV salga igual que la
  tabla), desvío frente a la media y aviso si la cobertura es desigual.
- **Motivos**: áreas N2 en barras (por no solucionadas en absoluto) y las 25 tipologías N3; cada
  una se despliega con 10 llamadas de ejemplo (texto e ID con «Copiar ID») y su CSV.
- **Sin acceso a internet**: indicadores, atención frente a proceso, la rúbrica ítem a ítem,
  los impedimentos (desplegables con reparto por marca y servicio y llamadas de ejemplo) y las
  averías N4 › N5.
- **CSV**: todas las no solucionadas con los filtros (`/nosolucion/csv`, opcionalmente de una
  tipología `n3` o un impedimento `bit`), la tabla de equipos y las tipologías.

**Qué se trajo y qué se rehízo**:

- **Tal cual** (solo cambia el espacio de nombres): `Servicios/NoSolucion/` (`FuenteBigQuery`,
  `CacheNoSolucion`, `Cubo`, `Ensamblador`, `Agregados`, `FiltrosCdm`, `ServicioCdm`) y
  `Servicios/Comun/` (`FormatoPython`, `ExportacionCsv`, `Errores`), con sus pruebas
  (`NoSolucionTests.cs` contra la salida del Python de referencia en `Fixtures/nosolucion_python.json`,
  y las del servicio de pantallas de `CdmTests.cs`).
- **Adaptado**: `ServicioNoSolucion` lee `OpcionesNoSolucion` (sección `NoSolucion` de
  appsettings; en ranking-mvc eran las variables `NOSOL_*` del `.env`) y resuelve las rutas
  respecto a la carpeta de la aplicación.
- **Rehecho con el estilo de aquí**: controlador `NoSolucionController`, modelos
  `Models/NoSolucion` y vistas `Views/NoSolucion`. Usa las piezas de Auditorías (cabecera,
  pestañas, `_Desplegable` —movido a `Views/Shared`—, tira de indicadores, tarjetas, `.hbarras`,
  `crono-linea`, `table.ranking`, filtros que se aplican al marcar con recarga parcial) y solo
  añade lo que no existía (pastillas de tasa, filas que se despliegan, llamadas de ejemplo,
  atajos de fecha, «preparando»). Todo está en la **sección 9 de `docs/guia-de-estilos.md`**.
- **Quitado** respecto a ranking-mvc: el permiso `ver_nosolucion`, el botón solo para admin y el
  recuerdo de filtros en la sesión (aquí no hay inicio de sesión; los filtros van en la URL).
- **Portada**: segunda tarjeta, «CDM No solución», junto a «General».

### ¿Atención o proceso? Y las llamadas repetidas (05-10-2026, tarde)

El usuario pidió, «en base al contexto» de su bóveda `C:\Proyectos\Soporte YGMM` (el proceso de
soporte técnico de YGMM: Schaman, escalados, envío de técnico, cierre y encuesta…), que la pestaña
**Sin acceso a internet** diga si cada no solución fue de **atención** o de **proceso**, y que se
valide si había **llamadas duplicadas** en «Qué frena el proceso».

- **Duplicadas: no hay.** Comprobado en BigQuery (05-09 → 04-10): 20.589 registros = 20.589
  `conversationId` distintos; las 1.638 no solucionadas son 1.638 llamadas distintas. 277
  conversaciones son tramos de una llamada transferida (mismo `externalConversationId` con `_1`,
  `_2`…): se cuentan aparte porque cada tramo tiene su agente y su encuesta. Lo que «inflaba» la tabla
  es que **casi todas las no solucionadas traen 2 o 3 impedimentos** y cuentan en cada fila (las filas
  suman 3.631 con 1.598 llamadas). La página lo dice ahora: tarjeta **«¿Hay llamadas repetidas?»**
  (calculada en cada carga, con el filtro de marca), y en «Qué frena» el número de llamadas distintas
  y, por fila, cuántas traen **solo ese** impedimento y con cuáles viene.
- **Una sola causa por llamada** (`Servicios/NoSolucion/CausaNoSolucion.cs`, reglas documentadas allí):
  *Proceso* (impedimento de proceso y atención correcta), *Proceso, con fallos de atención* (las dos:
  revisar si el escalado o el técnico eran evitables; en la bóveda, «Escalados de Voz»: el 90 % de los
  escalados a N2 eran errores de N1), *Atención* (sin impedimento de proceso y con cierre abrupto,
  impedimento del agente o rúbrica peor que la mediana de las solucionadas), *Cliente* y *Sin causa*.
  Son los mismos cuadrantes de antes con nombres de soporte, más la separación cliente / sin causa,
  cada uno desplegable con sus razones, 10 llamadas de ejemplo y su CSV. CSV nuevo con **cada llamada
  y su causa** y las señales que la deciden: `/nosolucion/internet/causas/csv` (`?causa=` opcional).
- **Etiquetas nuevas por palabras clave** (`ClasificadorEtiquetas.cs`): las etiquetas de impedimento
  las redacta la IA de DataOrb y salen nuevas; la tabla del 10/09 no conocía las de 596 de las 1.638
  no solucionadas y todas iban a «Otro». Ahora una etiqueta **que no está en la tabla** pasa por reglas
  de palabras clave (orden y excepciones revisados contra las etiquetas reales de 90 días: «tienda» no
  es técnico, «dependiente» no es «pendiente», la falta de permisos del agente es proceso, no atención…);
  si ninguna encaja, sigue en «Otro». La tabla manda siempre sobre las palabras.
- **Resultado** (05-09 → 04-10): proceso 904 (55,19 %), proceso con fallos de atención 494 (30,16 %),
  atención 140 (8,55 %), cliente 34, sin causa 66. En las dos causas con atención, la señal que más pesa
  es la rúbrica (85 %): ojo, la mediana de ítems fallados de las solucionadas es **1 de 7**, así que
  basta con fallar 2 para contar como «atención». Si al usuario le parece estricto, el umbral está en
  `Ensamblador` (mediana) y se puede subir.
- **Cubo v3**: tres cortes nuevos por día (`ver`, `dup`, `causas`) y la consulta de internet trae
  `conversationId` y la llamada física. La firma cambia (`…-v3-…`), así que al publicar **el cubo se
  vuelve a traer de BigQuery** (~1 min de «Preparando los datos»). Las pruebas que comparan con el
  Python de referencia dejan fuera solo lo nuevo; lo nuevo tiene sus pruebas (`CausaNoSolucionTests`).
- Ojo: la API de Storage de BigQuery dio una vez «failed to connect to all addresses» (red); la página
  reintenta sola y a la segunda trajo los datos.
