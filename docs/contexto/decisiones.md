# Decisiones de la web y estilo

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del
> proyecto). Las referencias a «sección N» son de cuando todo estaba en un solo fichero:
> 1–2 → `power-bi.md` · 3 y 3 bis → `decisiones.md` · 3 ter → `no-solucion.md` ·
> 4–5 → `estructura-y-ejecucion.md` · 6 → `verificacion.md` · 7 → `pendientes.md` · 8 → `bitacora.md`.

## 3. Decisiones de la web

- **Datos en memoria**: igual que el modo importación de Power BI. Al arrancar, cada
  `Auditorias:MinutosRecarga` minutos (30) y al pulsar «Actualizar» se ejecutan las consultas
  y se guardan todas las filas en memoria (~15.000 auditorías + ~205.000 filas de nómina,
  ~5 s en total, en segundo plano). Todos los cálculos se hacen en C# sobre esas listas (cada
  página tarda < 150 ms). Si una recarga falla, se siguen sirviendo los datos anteriores y se
  avisa en la página.
- **Sin librerías de gráficos ni Bootstrap**: gráficas dibujadas en el servidor (SVG estirado
  al plano + textos en HTML encima, alto fijo). Se quitaron `wwwroot/lib` (Bootstrap, jQuery)
  y la página Privacy de la plantilla.
- **Filtros**: formulario GET (la URL se puede compartir y el botón Atrás funciona). `site.js`
  lo envía por `fetch` con la cabecera `X-Parcial: 1`; el servidor devuelve solo el partial
  `_Informe` y se sustituye `#informe`. Sin JavaScript también funciona (enlaces normales y un
  botón «Aplicar filtros» en `<noscript>`).
  - **Se filtra al marcar** (pedido del usuario el 02-10-2026): cada casilla aplica el filtro
    al momento y el desplegable **sigue abierto** tras recargar (mismo texto de búsqueda,
    mismo desplazamiento y el foco en la casilla) para poder marcar más. Ya no hay botón
    «Aplicar»; «Quitar selección (n)» aparece cuando hay algo marcado. Las fechas filtran al
    cambiarlas. Un clic fuera o Escape cierran el desplegable.
  - Buscador si hay más de 8 opciones: **filtra mientras se escribe** (sin tildes ni
    mayúsculas, sobre el nombre de la opción), dice «Sin coincidencias para «…»» si no queda
    ninguna, e Intro no envía el formulario. Ojo: las opciones se ocultan con `[hidden]` y
    `label.opcion` es `display: flex`, así que hace falta `label.opcion[hidden] { display: none }`
    (sin esa regla el buscador no ocultaba nada; lo vio el usuario el 02-10-2026). Cada opción
    lleva su cifra de auditorías.
  - Las opciones se filtran entre sí, como los segmentadores del PBI (cada filtro muestra lo
    que queda con los demás); las marcadas se ven siempre, aunque queden a 0.
  - Valores vacíos = «(En blanco)», como en Power BI.
  - Las fechas que coinciden con el borde del calendario no se mandan en la URL, para que un
    enlace guardado siga cogiendo los datos nuevos.
  - Al cambiar de página (General ↔ Formación) se conservan fecha y mes; el resto no (en el
    PBI los segmentadores no estaban sincronizados).
  - **Los desplegables van por encima del contenido**: el panel de filtros es `position:
    sticky` (crea su propia capa de apilamiento) y las tarjetas animadas crean las suyas; sin
    `z-index` en el panel, los desplegables quedaban **detrás** de las tarjetas (lo vio el
    usuario el 02-10-2026). Se arregló con `z-index: 20` en `.panel-filtros` (por debajo de la
    cabecera, 50). En móvil el panel es `position: relative` con el mismo `z-index`.
  - Si un desplegable no cabe debajo (los de «Más filtros»), `site.js` lo abre hacia arriba
    (`.hacia-arriba`) y ajusta el alto de la lista al hueco que hay.
- **Clic para filtrar**: pulsar una barra de sector o un auditor del top 10 añade (o quita)
  ese filtro; sustituye la interacción de resaltado del PBI.
- **Descargable**: botón «Descargar Excel» (.xlsx, ClosedXML) con el detalle filtrado.
- **Tarjetas**: al pasar el ratón dicen qué fechas comparan.
- **Período anterior de «Total auditorías» y de la nota** (lo eligió el usuario el 02-10-2026,
  porque el +102 % del PBI no tenía lógica): **el mismo tiempo, justo antes**, en
  `Periodos.PeriodoAnterior`:
  - fechas que empiezan un día 1 y acaban a fin de mes, o en la última fecha con datos (mes en
    curso) → los mismos meses de antes con el mismo corte de día: septiembre → agosto entero;
    01–02/10 → 01–02/09; agosto + septiembre → junio + julio;
  - cualquier otro rango → los mismos días justo antes: 21–27/09 → 14–20/09.
  - Nunca se solapa con lo elegido. Si ese período empieza antes que los datos (con el rango
    completo, que empieza el 01/08), no hay variación: «Sin período anterior con datos», y la
    ficha dice qué período haría falta.
  - «Auditorías de la semana» y «del mes» siguen las medidas del PBI (semana hasta la fecha
    frente a la anterior; mes hasta la fecha frente al anterior), que sí tienen sentido.
- **Tarjeta «Total agentes»** (02-10-2026, en lugar de «Agentes auditados»): agentes en nómina
  frente a los que tienen auditoría.
  - Total = legajos distintos de `Consultas/Nomina.sql` en las fechas elegidas, con los
    filtros de **sector, super y team** (los de auditor, cargo de auditor y base no aplican a
    la nómina) y con cargo en `Auditorias:CargosAgente`: **Agente, Agente en Capacitacion y
    Aprendiz Sena Etapa Productiva** (lo eligió el usuario; se compara sin mayúsculas).
  - Auditados = de ese total, los que tienen al menos una auditoría con todos los filtros. Se
    pinta «1.487 auditados · 86,45 %» con una barra de cobertura (nunca pasa del 100 %).
  - Si la nómina no se puede leer, la tarjeta sale «—» con el motivo en la ficha y el resto
    del informe sigue.
- **Portada**: la tarjeta «General» (el 02-10-2026 el usuario pidió quitar la de Formación) y, desde
  el 05-10-2026, «CDM No solución»; «Formación &
  Calidad» se abre desde las pestañas del informe.
- **Top 10**: agrupa por auditor y cargo (como el PBI: un auditor con dos cargos sale en dos
  filas); los auditores con menos de 20 auditorías van al final y en gris.
- **Credenciales**: en `CDM Auditorias Calidad/.env` (no se versiona ni se publica; plantilla
  en `env.ejemplo`). Variables: `AUDITORIAS_DB_HOST`, `AUDITORIAS_DB_NAME` (REPORTING),
  `AUDITORIAS_DB_USER`, `AUDITORIAS_DB_PASSWORD`, `DB_TRUST_SERVER_CERTIFICATE=true`. Una
  variable de entorno con el mismo nombre tiene prioridad. **Se copiaron de la cuenta `DB_2`
  del `.env` de producción de ranking-mvc** (es un login personal del usuario; ver 7).
- **Sin inicio de sesión**: como el PBI, cualquiera que llegue a la URL ve los datos (hay
  nombres de agentes y auditores). Ver 7.

---

## 3 bis. Estilo: guía SOLARIS · GAIA (desde el 02-10-2026)

El usuario pidió seguir **`docs/guia-de-estilos.md`** (guía de diseño de su plataforma SOLARIS ·
GAIA; las secciones 1–8 son las suyas tal cual y la **sección 9**, añadida el 05-10-2026, recoge cómo
se aplica en esta web, las variables añadidas y las piezas de Auditorías y de No solución: es lo
primero que hay que mirar). **Antes de tocar el aspecto, léela.** Lo que se aplicó:

- **Letra**: Segoe UI Variable Text (`--font-ui`) y Display (`--font-titulo`, solo en el título
  de la cabecera y en las cifras grandes); no se carga ninguna fuente. Negrita 600 en titulares
  y cifras, 500 en nombres de tabla; nada de 700/800.
- **Cifras**: dos decimales y `%` con espacio fino que no se parte (U+202F): «50,14 %».
  `Formato.Porcentaje`, `Formato.Decimal2`, `Formato.PorcentajeEntero` (ejes).
- **Colores**: todos son variables de `:root` en `site.css`, redefinidas en
  `[data-tema="oscuro"]`. Informe = marca **Orange** (`--acento #f16e00`, texto encima negro,
  cabecera de tabla `#ffe3cc`/`#7a3a00`). Portada y error = `data-marca="portada"` (marino
  `#1f2a44` y grises; el naranja solo en el logotipo). Bueno/malo `#228722`/`#cd3c14`;
  semáforo pastel en la tabla; series de gráfica `#f16e00` y `#4170d8`.
  - La marca se pone en `<html data-marca>` desde `ViewData["Marca"]` (por defecto `orange`).
  - **Nada de colores en las vistas** (solo en `site.css`; el Excel y el favicon llevan los
    suyos porque no son vistas).
- **Tema claro/oscuro**: `wwwroot/js/tema.js` (en `<head>`, antes del CSS) pone
  `data-tema="claro|oscuro"` y `data-tema-preferido="auto|claro|oscuro"`; botón en la cabecera
  que va pasando auto → claro → oscuro; se guarda en `localStorage` (`cdm-tema`). En oscuro la
  elevación va con bordes, no con sombras.
- **Distribución**: cabecera pegajosa de 72 px con línea de 3 px del acento (logotipo que vuelve
  al menú, título y subtítulo en el centro, botón de tema); pestañas (General / Formación) y a la
  derecha «Agrupar por» Día/Semana/Mes; `.tablero` de 1.120 a 1.720 px con el panel de filtros
  de 268 px. Por debajo de 1.120 px hay scroll horizontal; **un solo punto de ruptura, 760 px**
  (todo se apila). La portada, además, pasa a una tarjeta por fila por debajo de 1.100 px (lo
  dice la guía). No usar `auto-fit` ni añadir más puntos de ruptura.
- **Panel de filtros**: sin scroll interno y pegajoso; siempre a la vista Fecha, Mes, Sector,
  Super, Team y Auditor; Cargo auditor y Base en «Más filtros» (`TableroModelo.CamposEnMasFiltros`;
  se abre solo si tiene algo marcado). Un desplegable con menos de dos opciones no se pinta
  (`GrupoFiltro.Visible`).
- **Piezas con los nombres de la guía**: `.resumen` / `.resumen-dato` (tira de indicadores con
  icono, filo de 3 px y realce radial; `.destacada` = la nota; `.resumen-cobertura` = la barra de
  «Total agentes»), `.tarjeta`, `.rejilla-2`, `.crono-tarjeta` con `crono-linea principal` y
  `crono-area`, `.hbarras > .hbarra` con `.hbarra-media` (la nota del sector, en azul, en pareja
  con la cantidad), `table.ranking[data-mapa]` con `th[data-sentido]` y `td[data-valor]`,
  parciales `_PanelFiltros` y `_ErrorDatos`.
- **Fichas al pasar el ratón**: cada periodo de las gráficas lleva una banda invisible con un
  `<title>` «Etiqueta · Serie valor · …»; `site.js` lo convierte en ficha, con guía vertical y el
  resto de periodos desvanecido. (La guía habla de `graficas.js` de SOLARIS; aquí no existe y lo
  hace `site.js`.) También tienen ficha las tarjetas de indicador y las barras de sector.
- **Movimiento**: 160/220/440/780 ms, 45 ms entre tarjetas hermanas, curva
  `cubic-bezier(.22,.61,.36,1)`; las barras crecen con `scaleX`/`scaleY`; barra de carga
  naranja arriba al aplicar filtros. Con «reducir movimiento» no se anima nada (CSS y JS).
  - Las animaciones de entrada (tarjetas que aparecen, barras que crecen, cifras que cuentan
    desde cero) **solo se ven al cargar la página**. Al filtrar, el contenido nuevo lleva la
    clase `.actualizado` (sin animaciones de entrada) y las cifras pasan del valor anterior al
    nuevo, para que marcar casillas seguidas no parpadee.
- **Sin emojis ni iconos de color**: iconos de trazo en `Infraestructura/Iconos.cs` (también el
  logotipo); se quitaron el GIF y las tarjetas decorativas de la portada.
- No se puso la marca SOLARIS (no hay logotipo ni se sabe si la web va dentro de esa
  plataforma): ver 7.
