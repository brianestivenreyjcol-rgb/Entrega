# CDM Auditorías Calidad — contexto para continuar el trabajo

> Lo lee la IA al empezar cada sesión: es corto a propósito. El detalle está en `docs/contexto/`,
> un fichero por tema: **léelo solo cuando vayas a tocar ese tema**. Al cambiar algo, actualiza aquí
> el estado o los pendientes si cambian, y el detalle en su fichero; lo hecho va a
> `docs/contexto/bitacora.md` (una entrada corta, lo más reciente al final).
> Idioma del usuario: **español**, en todo (lo que ve, los avisos intermedios, código y comentarios).
>
> **Repositorio `Entrega`** (09-10-2026): la versión para el otro equipo, con **tres informes**: General (con sus pestañas
> Formación & Calidad y T0 y planes de acción), CDM No solución y GAIA Formación. Las vistas, el CSS y el JS son los mismos
> que los de la web del 5180, para que se vea igual.

## Qué es y dónde está

- El Power BI «CDM Auditorías Calidad» pasado a web ASP.NET Core MVC (.NET 8, Razor): **General** y
  **Formación & Calidad**. Desde el 05-10-2026, también **CDM No solución** (`/nosolucion`), traído de
  ranking-mvc (son dos copias independientes del motor).
- Desde el 08-10-2026, **T0 y planes de acción** (`/t0`, tercera pestaña de Auditorías, **publicado en el 5180 el 08-10-2026**): cada auditoría ICEBERG con
  «Tolerancia 0» frente a su alerta T0 (`RecursosHumanos.Legal.Alertas_T0`) y su plan (`Legal.PlanAccion`; «Acción de Calidad» es el
  motivo 1020). Ver `docs/contexto/t0-planes.md`.
- Proyecto: `C:\Proyectos\CDM Auditorias Calidad\` (`CDM Auditorias Calidad.sln`: web + pruebas).
- GitHub: `brianestivenreyjcol-rgb/Entrega` (**público**, rama `main`; nunca subir `.env`, `publicacion\`, capturas ni cachés).
- Aspecto: `docs/guia-de-estilos.md` (SOLARIS · GAIA + sección 9, lo de esta web). Para el front está
  la skill y el agente **`frontend-solaris`** (copias para compartir en `docs/compartir/`); si la guía
  cambia, recopiarla en la skill y regenerar el `.skill`.

## Datos

- Auditorías: SQL Server `10.148.226.40\REPORTING`, `Consultas/Auditorias.sql` (la del PBI) y
  `Consultas/Nomina.sql`; en memoria, recarga cada 30 min; las consultas se leen en cada carga
  (editar el `.sql` + «Actualizar»). Ventana: 3 meses atrás + el mes en curso (ICEBERG con `- 3`).
  Las tablas WEB empiezan el 01/08: **el usuario avisará** cuando traigan 4 meses; no investigar.
- T0: `Consultas/AuditoriasT0.sql` (mismo `.env`, ~8 s, en la misma recarga que las auditorías; si falla, solo esa pestaña avisa).
  `Legal.PlanAccion` **no tiene planes del 14-07 al 30-09-2026**: la web avisa de los huecos. El login de SQL está en español:
  en columnas `datetime`, escribir las fechas como `'20260701'`.
- No solución: BigQuery por ODBC (DSN de usuario `BQCOL`), cubo de 90 días en disco (`App_Data` en
  desarrollo, `publicacion\datos` en producción), se renueva solo cada 12 h. Formato del cubo: **v3**.
- GAIA: el Excel de nómina `\\172.16.232.102\Proyecto Ranking\2.GAIA\Formación\Nomina Iniciales General.xlsx` (solo la hoja
  Oleadas) y las llamadas de BigQuery (mismo DSN `BQCOL`); caché `cache_gaia.json`, se recarga sola al guardar el Excel.
- Credenciales en `CDM Auditorias Calidad\.env` y `publicacion\.env` (login personal del usuario;
  nunca decirlas en el chat).

## Producción y pruebas (una sola dirección: el 5180)

- `http://10.148.223.143:5180` (compañeros) = `http://localhost:5180`: ventana «CDM Auditorias Calidad
  (5180)» que abre `arrancar.cmd` sobre `publicacion\app`. Al día desde el **09-10-2026** (lo anterior más las gráficas de General y Formación en paralelo).
- **Publicar** (solo si el usuario lo pide): cerrar esa ventana → `cmd /c "C:\Proyectos\CDM Auditorias
  Calidad\publicar.cmd"` → `arrancar.cmd` con ruta completa. Si cambia el formato del cubo de No
  solución o de la caché de GAIA (`DatosGaia.VersionActual`), copiar antes la caché de `App_Data` a
  `publicacion\datos` para no esperar a BigQuery.
- **Probar** en una copia temporal en otro puerto (p. ej. 5190: `ASPNETCORE_URLS=http://localhost:5190
  dotnet run --no-build --no-launch-profile`) y **pararla al acabar**. Parar la web antes de compilar
  (la DLL queda bloqueada). Pruebas: `dotnet test "CDM Auditorias Calidad.sln"` (336 en verde).
- Capturas: Edge sin ventana con `--force-prefers-reduced-motion` (o `scripts/capturas.py` de la skill).
  Si el usuario dice «sin capturas», verificar con texto y medidas: las imágenes gastan muchos tokens.

## Decisiones del usuario que no hay que deshacer

- Variación de «Total auditorías» y de la nota: frente al mismo tiempo justo antes (no el DATEADD del PBI).
- General y Formación & Calidad: «Evolución de auditorías» y «Nota promedio de calidad» **en paralelo** (09-10-2026; antes a todo el
  ancho). Las pastillas que no caben sin pisarse no se rotulan (guía 9.15).
- Filtros que se aplican al marcar, con el desplegable abierto; portada con «General» y «CDM No solución».
- «Total agentes» = nómina (Agente, Agente en Capacitacion, Aprendiz Sena Etapa Productiva) vs. auditados.
- No solución, «Sin acceso a internet»: **una causa por llamada** (proceso, proceso con fallos de
  atención, atención, cliente, sin causa) con las reglas del proceso de soporte de YGMM
  (`C:\Proyectos\Soporte YGMM`); etiquetas nuevas de DataOrb por palabras clave; no hay llamadas
  repetidas (lo que sumaba de más eran llamadas con varios impedimentos).
- Gráficas: **cada punto con su valor en una pastilla** del color de su serie, sin «máx./mín./media»
  (06-10-2026, sustituye a las etiquetas de impacto). Tablas: **total al pie** (`tfoot`, «TOTAL», pegado
  abajo), nunca arriba. El color de la página **sigue a la marca filtrada**: Orange naranja, YOIGO/MASMOVIL
  morado, Jazztel `#FFD200`. Reglas en la skill frontend-solaris y en la guía (2.1, 4 y 9.4).
- **Aspecto = el del portal SOLARIS** (06-10-2026, guía 9.9): cabecera con migas, Imprimir / Presentar / Tema, pestañas en barra, panel
  de filtros plegable (`cdm-panel`), tarjetas con cabecera y pie, tablas con puesto, barrita de volumen y TOTAL al pie, gráficas con
  cada punto en pastilla (piezas en `Views/Shared`), medidor y anillos en GAIA → Estilo; el color de No solución también sigue a la marca.
  Desde el 07-10-2026 las gráficas también siguen a la marca (paleta por marca, guía 2.2 y 9.10), las columnas por etapa van en preconexión /
  aseguramiento y las tablas son compactas (una línea, oleada en pastilla).
  También: sin líneas de media/meta/umbral en las gráficas ni filas en gris por pocas llamadas; patrón «indicador + volumen» compartido; GAIA tiene
  «Ranking y Estilo» (unidas) y «Motivo de contacto» nuevas (guía 9.11).
  Gráficas de línea: un solo estilo (suavizada + degradado + llamadas grises detrás); tablas: primera columna entera y fija (guía 9.12).

## Pendientes

- Revisar con el usuario el umbral de la rúbrica para «atención» (hoy basta con fallar 2 de 7) y, de
  vez en cuando, las etiquetas de impedimento que siguen en «Otro».
- Cuenta de servicio para SQL y BigQuery (hoy van con el login y el DSN personales del usuario); en la
  torre nueva hay que recrear el DSN `BQCOL`.
- Decidir si hace falta inicio de sesión; si la web entra en SOLARIS, su logotipo y su login.
- Julio de WEB cuando avise el usuario (hasta entonces, agosto frente a julio sale inflado).
- **GAIA Formación** (`/gaia`, desde el 06-10-2026): PBI en `Power bi\GAIA Formación.pbip`; llamadas de
  BigQuery filtradas por el Excel de nómina de la compartida (filtro exacto agente + día, fallos del PBI
  corregidos; se recarga sola al guardar el Excel). Las 9 pestañas (Resumen, Ranking, Estilo,
  Rendimiento, Evolución, Comercial, Motivos, Españolización, Llamadas) hechas y **publicadas en el 5180 el 07-10-2026**. Todo en `docs/contexto/gaia-formacion.md`.
- **T0 y planes** (`/t0`, publicado el 08-10-2026): que el usuario diga si «Registro de Falta» y «Agente dado de baja» cumplen la regla (hoy no)
  y que pidan recargar `Legal.PlanAccion`.
- Detalle de todo esto: `docs/contexto/pendientes.md`.

## Dónde está el detalle (`docs/contexto/`)

| Fichero | Léelo cuando… |
|---|---|
| `gaia-formacion.md` | toques la vista GAIA Formación (análisis de su PBI, Excel de nómina, plan por fases) |
| `power-bi.md` | toques medidas, fechas DAX o la consulta de auditorías (análisis del PBI y cómo se pasó cada medida) |
| `decisiones.md` | toques el informe de Auditorías: filtros, tarjetas, top 10, Excel, estilo aplicado |
| `t0-planes.md` | toques «T0 y planes de acción»: tablas, llaves del cruce, «Acción de Calidad», hueco de PlanAccion |
| `no-solucion.md` | toques No solución: fuente, cubo, pestañas, causas atención/proceso, palabras clave |
| `estructura-y-ejecucion.md` | necesites saber qué hay en cada carpeta o cómo se ejecuta y publica con detalle |
| `verificacion.md` | quieras cuadrar cifras con lo que ya se comprobó contra SQL o BigQuery |
| `pendientes.md` | vayas a cerrar un pendiente (historia de cada uno) |
| `bitacora.md` | necesites saber qué se hizo y cuándo |
