# Bitácora

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del
> proyecto). Las referencias a «sección N» son de cuando todo estaba en un solo fichero:
> 1–2 → `power-bi.md` · 3 y 3 bis → `decisiones.md` · 3 ter → `no-solucion.md` ·
> 4–5 → `estructura-y-ejecucion.md` · 6 → `verificacion.md` · 7 → `pendientes.md` · 8 → `bitacora.md`.

## 8. Bitácora

(Lo más reciente al final.)

- **01-10-2026** — Análisis del PBI completo (sección 2). Probada la conexión de lectura con
  la cuenta de DB_2 de ranking-mvc: lee las 4 tablas de origen y la consulta completa (~2 s).
- **01-10-2026** — Web construida sobre la plantilla MVC del usuario: portada, General,
  Formación & Calidad, filtros, 4 gráficos, Excel, recarga periódica, pruebas, scripts de
  publicación.
- **02-10-2026** — Aspecto rehecho con la guía de estilos SOLARIS · GAIA (sección 3 bis): tema
  claro/oscuro, cabecera de 72 px, pestañas, panel de 268 px con «Más filtros», tira de
  indicadores con icono, barras con la nota en pareja, tabla ranking ordenable con semáforo,
  fichas, animaciones y portada ejecutiva.
- **02-10-2026** — Proyecto subido a GitHub (repositorio privado; el usuario lo pasó de público
  a privado antes de subir).
- **02-10-2026** — Portada solo con «General». Tarjeta «Agentes auditados» sustituida por
  «Total agentes» (nómina frente a auditados; nueva consulta `Nomina.sql`, sección 2.2 bis).
- **02-10-2026** — El usuario arrancó producción en el 5180 (versión anterior; sin actualizar).
- **02-10-2026** — Filtros: los desplegables quedaban detrás de las tarjetas (arreglado con
  `z-index` en el panel pegajoso); ahora filtran al marcar y siguen abiertos; se abren hacia
  arriba si no caben; sin animaciones de entrada al filtrar. Documentación repasada entera.
- **02-10-2026** — La variación de «Total auditorías» y de la nota compara con el mismo tiempo
  justo antes (el DATEADD -1 MONTH del PBI daba +102 % con el rango completo).
- **02-10-2026** — Julio: ICEBERG con 3 meses atrás + el mes en curso (`- 3`, cambio del usuario)
  activo en desarrollo y producción; las consultas se leen de la carpeta de la aplicación en cada
  carga. WEB sin julio en origen: el usuario pedirá que traiga 4 meses y avisará. El usuario
  quitó la carpeta `Power bi\` del proyecto (subido el borrado; sigue en el historial).
- **02-10-2026** — El buscador de los desplegables filtra mientras se escribe (antes ocultaba con
  `[hidden]` pero el CSS lo anulaba).
- **02-10-2026** — Una sola dirección: el 5180 (también el perfil de Visual Studio); producción
  publicada con la versión actual y comprobada (`localhost` y la IP del equipo, julio en el
  filtro: 5.530 auditorías, todas ICEBERG; datos del 01/07 al 02/10, 20.874 auditorías).
- **05-10-2026** — **CDM No solución** traído de ranking-mvc a `/nosolucion` (sección 3 ter): motor de
  BigQuery, caché y cálculos copiados con sus pruebas; controlador, modelos y vistas rehechos con las
  piezas de este proyecto; tarjeta en la portada. Probado en una copia temporal (5190) con datos
  reales; producción (5180) sin tocar, a la espera del usuario.
- **05-10-2026** — `docs/guia-de-estilos.md` actualizada (el usuario la volvió a pasar, idéntica): las
  secciones 1–8 siguen siendo las de SOLARIS y la nueva sección 9 recoge cómo se aplica aquí, las
  variables añadidas y las piezas de Auditorías y de No solución. La misma copia quedó en su carpeta
  de Descargas.
- **05-10-2026** — Sin acceso a internet: **una causa por llamada** (atención, proceso, las dos, cliente,
  sin causa) con las reglas del proceso de soporte de YGMM, CSV de cada llamada con su causa, tarjeta
  «¿Hay llamadas repetidas?» (no hay: lo que sumaba de más eran llamadas con varios impedimentos) y
  etiquetas nuevas de DataOrb clasificadas por palabras clave. Cubo v3. 290 pruebas. Probado en el 5190
  con datos reales; sin publicar.
- **06-10-2026** — Resumen de No solución: **etiquetas de impacto** en las dos gráficas (pedido del
  usuario). Evolución por marca: último valor de cada marca a la derecha (separados para no pisarse),
  el pico del periodo y la media en la leyenda con su línea discontinua. Encuestas por día: media de
  los días completos, máximo y mínimo con su cifra y «parcial» en los días parciales. Los días
  parciales y provisionales no cuentan como pico, máximo ni mínimo (sus encuestas siguen llegando).
  Probado en el 5190; sin publicar.
- **06-10-2026** — Publicado en el 5180 (pedido del usuario): No solución con la causa de cada llamada,
  el control de repetidas y las etiquetas de impacto. Se copió la caché v3 de `App_Data` a
  `publicacion\datos` para que entrara al momento. Comprobado desde `http://10.148.223.143:5180`.
- **06-10-2026** — Skill y agente **«frontend-solaris»** (pedido del usuario: «que solo se encargue del
  front»): instalados en `~/.claude/skills/frontend-solaris` y `~/.claude/agents/frontend-solaris.md`;
  llevan la guía de estilos, recetas de piezas, `revisar_vistas.py` (colores a mano, `<style>`/`<script>`
  en vistas, emojis, negritas) y `capturas.py` (claro y oscuro con Edge). Para compartir:
  `docs/compartir/` (`frontend-solaris.skill`, el agente y `LEEME.md`). Si cambia la guía, volver a
  copiarla a la skill y regenerar el `.skill`.
- **06-10-2026** — Análisis del PBI **GAIA Formación** (`Power bi\`, lo añadió el usuario) y plan de
  migración a una vista nueva: `docs/contexto/gaia-formacion.md`. Lo importante: el filtro del PBI
  (todas las fechas × todos los ID del Excel) trae 110.718 llamadas; el exacto (cada agente en sus
  días) 13.461. Abruptas siempre 0, encuestas con fechas fijas hasta junio. Sin código todavía.
- **06-10-2026** — **GAIA Formación, fases 1 y 2** (pedido del usuario: filtro exacto, corregir los fallos,
  `/gaia`, pantallas con el agente frontend-solaris). La web lee el Excel de nómina de la compartida
  (solo la hoja Oleadas) y trae de BigQuery las llamadas de cada agente en sus días (13.529 de 356
  agentes); se recarga sola al cambiar el Excel. Corregidos transferencia, rellamada 24 h (con el dato
  de origen) y encuestas sin fechas fijas; «abruptas» no existe en DataOrb y se quita. Consulta por
  tandas de 60 agentes (el driver se cortaba). 15 pruebas nuevas (305 en verde). Sin publicar.
- **06-10-2026** — GAIA (pedido del usuario): fuera la fila «Total del filtro» del Ranking (el tinte sigue
  comparando con el total) y el resumen de contacto de Llamadas ya no se recorta (antes, 600 caracteres:
  se cortaban 12.776 de 13.529). La caché pasa a 47 MB; las tandas de 60 agentes siguen aguantando.
- **06-10-2026** — GAIA, **fases 3 y 4** (pedido del usuario: «sigue con las demás fases»; resumen de
  resolución también entero). Datos y cálculos de Rendimiento, Evolución, Comercial, Motivos y
  Españolización; vistas con el agente frontend-solaris. Españolización: el agente de cada llamada se
  identifica por sus frases (el «hablante 1» del PBI no es fijo), palabras enteras y piso/apartamento al
  derecho; lista en `Datos/palabras_gaia.json`. Obstáculos separados por « | ». 312 pruebas. Sin publicar.
- **06-10-2026** — **Rediseño visual de toda la web al estilo del portal SOLARIS** (pedido del usuario; agente frontend-solaris, solo
  `Views/**`, `site.css`, `site.js`, `tema.js` e `Iconos.cs`): portada de módulos con el estado de los datos, cabecera con migas y botones
  (Imprimir, Presentar, Tema), pestañas en barra, panel de filtros plegable, tablas con puesto / barrita de volumen / TOTAL al pie (también en
  General y No solución), gráficas de General y No solución con cada punto en pastilla, selector Semana / Día en todas las de GAIA con
  `PorSemana`, medidor y anillos en Estilo y color por marca en No solución. Detalle en la guía, 9.9. Probado en el 5190; sin publicar.

- **07-10-2026** — La portada recibe el estado de No solución y GAIA en el modelo (`MenuModelo` +
  `EstadoInforme`, desde `HomeController`), en vez de leer los servicios desde la vista. Guía recopiada a
  la skill frontend-solaris y `.skill` regenerado. 312 pruebas; todo da 200 en el 5190. Sin publicar.
- **07-10-2026** — **Publicado en el 5180** (pedido del usuario): GAIA Formación (9 pestañas) y el rediseño al
  estilo del portal SOLARIS. Se copió `cache_gaia.json` (v2) de `App_Data` a `publicacion\datos`. Comprobado
  desde `http://10.148.223.143:5180`; el 5190 de pruebas, parado.
- **07-10-2026** — Gráficas con la paleta de la marca filtrada en toda la web (`--serie-1…5` por `[data-marca]`, claro y oscuro; las series que son una marca
  llevan el color de su marca), columnas por etapa en preconexión / aseguramiento (`--etapa-pre`, `--etapa-aseg`) en vez de por umbral y tablas compactas (una línea por
  fila, nombre con la oleada en una pastilla; Llamadas con marca y etapa en columnas propias). Detalle en la guía, 9.10. Probado en el 5190 (parado al acabar); sin publicar.

- **07-10-2026** — Españolización: la tabla de agentes lleva también la oleada al lado del nombre (`GrupoEspanolizacion.Oleada`). Guía recopiada a la skill y `.skill` regenerado.
- **07-10-2026** — GAIA: pestañas **Ranking y Estilo** (medidor, anillos, evolución con pestañas de indicador, ranking general y tipológico, etapa y matriz) y
  **Motivo de contacto** (tarjetas con histórico, burbujas, sentimiento, rellamada por rol, obstáculos, mapa de calor y clientes) como las del portal; patrón «indicador + volumen»
  compartido (también en el Resumen); fuera todas las líneas de media, meta y umbrales y el gris de las filas con pocas llamadas. Guía, 9.11. 319 pruebas; probado en el 5190; sin publicar.
- **07-10-2026** — Todas las gráficas de línea con el estilo de la de adherencia (suavizada, degradado bajo la principal, llamadas en columnas grises detrás, eje ajustado a los datos), también
  TMO, Evolución, Comercial, Españolización, General y No solución; y la primera columna de todas las tablas se lee entera y queda fija a la izquierda. Guía, 9.12. 319 pruebas; probado en el 5190; sin publicar.

- **07-10-2026** — La curva suavizada de las gráficas (`AyudasGaia.TrazosSuaves`) ya no se pasa de largo: los puntos de control se acotan al rango de cada tramo (antes podía bajar de 0 en un valle). Guía recopiada a la skill.
- **07-10-2026** — **Publicado en el 5180** (pedido del usuario): paletas por marca, tablas compactas con la primera columna entera y fija, Ranking y Estilo, Motivo de contacto y todas las gráficas de línea con el estilo de la de adherencia. Sin copiar caché (formato v2 sin cambios). Comprobado desde `http://10.148.223.143:5180`; el 5190, parado.
- **08-10-2026** — Auditorías: pestaña nueva **T0 y planes de acción** (`/t0`): auditorías ICEBERG con «Tolerancia 0» unidas a `Legal.Alertas_T0` (por ID de llamada de `PlanillaRespuestasP`, o agente + auditor + fecha) y a `Legal.PlanAccion` (`ID_Accion` = `IdPlanDeAccion`), con la comparación «Acción de Calidad» (motivo 1020). Filtros Situación y Plantilla, Excel de 30 columnas, aviso del hueco de PlanAccion (14-07 a 30-09-2026). 359 pruebas. Sin publicar. Detalle en `t0-planes.md`.
- **08-10-2026** — **Publicado en el 5180** T0 y planes de acción (pedido del usuario). Sin cachés que copiar (la consulta se lee de `publicacion\app\Consultas`). Comprobadas desde `http://10.148.223.143:5180` todas las páginas, `/t0` (mismas cifras que en el 5190) y su Excel.
- **09-10-2026** — Repositorio **`Entrega`** para el otro equipo (pedido del usuario): General (con Formación & Calidad y T0), CDM No solución y GAIA Formación, con las mismas vistas, CSS y JS que el 5180. README para la entrega (qué hace falta para que se vea igual) y `env.ejemplo`. 327 pruebas; probada en el 5190.
- **09-10-2026** — Auditorías (General y Formación & Calidad): «Evolución de auditorías» y «Nota promedio de calidad» en paralelo (`.rejilla-2`, la de columnas crece al alto de la de línea). `AyudasGaia.Rotulados` coloca cada pastilla con su ancho (la primera y la última, corridas hacia dentro) y no pone las que pisan: menos choques también en GAIA y No solución. `RotuladosTests`. 336 pruebas; medido sin choques de 1.280 px en adelante; claro, oscuro y 375 px revisados. **Publicado en el 5180.**
