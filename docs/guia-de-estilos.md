# SOLARIS · GAIA — guía de estilos

Guía de diseño para quien vaya a tocar las pantallas: tipo de letra, colores, piezas y distribución.
Verificada contra las hojas de estilo (`wwwroot/css`) el 02/10/2026. Si algo de aquí choca con el código, manda el código y
hay que corregir este documento.

> **CDM Auditorías Calidad** (05/10/2026): las secciones 1 a 8 son la guía de SOLARIS · GAIA tal cual. Cómo se aplica en
> esta web (una sola hoja, `site.css`), qué se le añadió y las piezas de **CDM No solución** están en la **sección 9**.

**Reglas de oro**
1. Antes de crear un estilo, usar un **token** y una **pieza** que ya existan.
2. **Nada de colores escritos a mano** en las vistas: todo color sale de una variable CSS.
3. **Sin librerías externas**: la red corporativa bloquea los CDN (cdnjs, Google Fonts). Gráficas en SVG generado en el servidor
   y JavaScript propio.
4. Todo en **español**: clases, variables, comentarios, textos.
5. **Sin emojis ni iconos «de IA»**, sin degradados de colores ni sombras de colores.
6. **No quitar funcionalidad** al rediseñar.
7. Confirmar el color con la imagen, no solo con el código.

---

## 1. Tipo de letra

| Uso | Fuente |
|---|---|
| Texto, tablas, botones | **Segoe UI Variable Text** (`--font-ui`), de reserva Segoe UI, Helvetica Neue, Arial |
| Título de página y cifras grandes | **Segoe UI Variable Display** (`--font-titulo`) |
| Logotipo «yoigo» (portada y menú) | Century Gothic (viene con Office), de reserva Arial Rounded |

- Es la letra de Windows 11: **no se carga nada** (ni `@font-face` ni Google Fonts). En Windows 10 cae a Segoe UI.
- Las define `movimiento.css`. Las hojas de cada pantalla declaran la versión sin «Variable» por si falta esa capa.
- **Display** solo en el título de la cabecera y en las cifras grandes (tarjetas de indicador, medidor, anillos).
- Cuerpo a **14 px**. Titulares con `text-wrap: balance`, párrafos con `pretty`.
- **Negrita controlada**: 600 para titulares y cifras, 500 para nombres en tablas. Nada de 700/800 por todas partes.
- Cifras tabulares (`tabular-nums`) en tablas, tarjetas, anillos y medidor.
- Cifras en cultura **es-ES**: dos decimales y `%` separado por un espacio fino («41,58 %»). En las celdas que se ordenan va el
  valor crudo en `data-valor`.

## 2. Colores

### 2.1 Por marca
La marca se marca en `<html data-marca="…">` y cambia el acento de toda la página.

| Marca | Acento | Oscuro del acento | Tema oscuro | Texto sobre el acento | Cabecera de tabla |
|---|---|---|---|---|---|
| **Yoigo · MásMóvil** | `#7b38c9` (morado) | `#5f27a3` | `#b48af0` | blanco | `#efe6fa` con tinta `#4a1f7d` |
| **Orange** | `#f16e00` (naranja) | `#cc5e00` | `#ff8a1f` | negro `#141414` | `#ffe3cc` con tinta `#7a3a00` |
| **Jazztel** | `#ffd200` (amarillo) | `#d9b300`; enlaces y texto de acento sobre blanco `#7a6200` | `#ffdd33` | negro `#141414` | `#fff4c2` con tinta `#5c4a00` |
| **Clanes y Formación** | `#f16e00` | enlaces `#cc5e00` | naranja aclarado | `#121316` | — |
| **Portada, menús y administración** | marino `#1f2a44` (hover `#2c3a5c`) | acento suave `#6b74b8` | botones `#e8eaf2` sobre `#1b2236` | — | — |
| **Acceso de SOLARIS (login)** | naranja `#f16e00` a ascua `#e2570a`, fondo `#fbf6f0` | tinta `#111a24` | noche cálida `#120d0a` | `#1b0d03` | — |

- **El texto que va encima del acento sale siempre de `--acento-texto`**: nunca se escribe a mano (blanco en morado, negro en
  naranja y en amarillo).
- **El acento no se usa como color de texto sobre blanco en Jazztel**: el amarillo no se lee. Enlaces, pestaña activa en texto
  y cifras «de acento» usan `--acento-enlace` (el oscuro del acento en cada marca: `#5f27a3`, `#cc5e00`, `#7a6200`).
- **La marca sigue al filtro** (pedido del usuario el 06-10-2026): en los informes con filtro de marca, `data-marca` sale de lo
  marcado: solo ORANGE → `orange`; solo YOIGO y/o MASMOVIL → `ygmm`; solo JAZZTEL → `jazztel`; nada o marcas de grupos
  distintos → el color por defecto del informe. Lo pone el servidor y el JS lo actualiza en las recargas parciales. **Con la
  marca cambian también las series de las gráficas** (2.2, pedido del usuario el 07-10-2026).
- Los menús y la portada van en marino y gris: el color de marca solo queda en los logotipos.

### 2.2 Series de las gráficas: una paleta por marca

Las gráficas toman `--serie-1` … `--serie-5` de la marca de la página (`[data-marca]`); nunca el naranja y el azul de
siempre. La 1 es el color de la marca; las demás, de su misma familia pero distinguibles (cambian de tono **y** de claridad).

| Marca | 1 | 2 | 3 | 4 | 5 | Oscuro (1–5) |
|---|---|---|---|---|---|---|
| **Yoigo · MásMóvil** (`ygmm`) | morado `#7b38c9` | magenta `#c2408f` | índigo `#4b55c4` | lila `#a98ae0` | gris violáceo `#6e6680` | `#b48af0`, `#ee8cc4`, `#8f97f0`, `#cdb6f5`, `#a59cb8` |
| **Orange** (`orange`, y el por defecto) | naranja `#f16e00` | tostado `#a84a00` | melocotón `#f2a65a` | teja `#d13f1f` | gris cálido `#8a7464` | `#ff8a1f`, `#e08a4d`, `#ffc48a`, `#ff7a5c`, `#b8a493` |
| **Jazztel** (`jazztel`) | amarillo `#d4a900` (oscurecido para leerse sobre blanco) | ocre `#7a6200` | carbón `#3a3a3a` | ámbar `#e08c00` | arena `#a89a6e` | `#ffd200`, `#c9b45a`, `#e6e6e6`, `#ffb347`, `#c9bf9f` |

- **Grupos de una misma cosa, mismo color** (columnas por etapa de formación): `--etapa-pre` (preconexión, tono claro de la
  marca: `color-mix(in srgb, var(--serie-1) 45%, var(--superficie))`) y `--etapa-aseg` (aseguramiento, `--serie-1`).
- **Cuando cada serie es una marca** (evolución por marca), cada una lleva el color de su marca, sea cual sea la página:
  ORANGE `#f16e00`, YOIGO `#ab3e91`, MASMOVIL `#3a3a3a` (su amarillo coincide con el de Jazztel), JAZZTEL `#d4a900`.
- Los colores con significado no cambian con la marca: bueno / malo, semáforo y sentimientos (tabla de abajo).

### 2.3 Fijos en todas las marcas
| Qué | Colores |
|---|---|
| **Bueno / malo** | verde `#228722` / rojo `#cd3c14` |
| **Semáforo pastel** (como el formato condicional de Excel) | verde `#c6efce`, ámbar `#ffeb9c`, rojo `#ffc7ce`, tinta `#141414`. En oscuro: `#2f5d3a`, `#5e5325`, `#5f2f37` con tinta `#f5f5f5` |
| **Sentimientos** | positivo `#2a9d8f`, neutro `#8d99ae`, mixto `#e9b44c`, negativo `#d1495b` |
| **Clanes: modelo ABCZ** | A `#F57E1B`, B `#00B0F0`, C `#00B050`, Z `#7030A0` |
| **Fondo / texto de la portada (claro)** | fondo `#f5f6f9`, tarjeta `#ffffff`, borde `#e4e7ee`, texto `#1b2236`, suave `#5f6780`, tenue `#9aa1b3` |
| **Fondo / texto de la portada (oscuro)** | fondo `#12121a`, tarjeta `#1c1c26`, borde `#2f2f3d`, texto `#f4f4f8` |

### 2.3 Tema claro / oscuro
- `tema.js` pone `data-tema="claro|oscuro"` en `<html>` (automático, claro u oscuro; se guarda en el navegador).
- Todo color es una variable de `:root` redefinida bajo `[data-tema="oscuro"]`.
- Base oscura en gris muy oscuro (no negro puro). **La elevación va con bordes, no con sombras.**
- Cuando algo «no se ve» en oscuro, casi siempre es un color escrito a mano.

## 3. Variables (tokens)

Hay dos juegos con el mismo significado, porque las hojas nacieron por separado:

| Qué | Pantallas de voz (`reporte.css`) | Informes (`informe.css`) |
|---|---|---|
| Acento | `--acento`, `--acento-osc` | `--acento`, `--acento-osc` |
| Texto sobre el acento | `--acento-texto` | `--acento-texto` |
| Texto | `--texto`, `--texto-suave`, `--texto-tenue` | `--text-primary`, `--text-secondary`, `--text-muted` |
| Superficie, fondo, borde | `--superficie`, `--fondo`, `--borde` | `--surface-1`, `--page`, `--border` |
| Cabecera de tabla | `--thead`, `--thead-texto` | `--thead-bg`, `--thead-texto` |
| Tarjeta de indicador | `--tile-borde`, `--tile-realce`, `--tile-filo` | los mismos |
| Semáforo | `--mapa-verde`, `--mapa-ambar`, `--mapa-rojo` | `--rampa-0` … `--rampa-6` |
| Cabecera (cromo) | `--cab-fondo`, `--cab-texto`, `--cab-borde` | los mismos |
| Forma | `--radio` 16 px, `--radio-sm` 8 px, `--alto-cabecera` 72 px | `--alto-cabecera` 72 px |

`movimiento.css` los une con `--mov-*` (`--mov-acento`, `--mov-texto`, `--mov-superficie`, `--mov-borde`…): lo que sea común a
los dos (asistente, animaciones) usa solo esos.

## 4. Distribución de una página

**Hojas de estilo**

| Hoja | Para qué |
|---|---|
| `reporte.css` | informes de voz (Yoigo y Orange) |
| `informe.css` | WhatsApp, Siebel y Clanes |
| `portal.css` | menús de cada marca |
| `login.css` | portada de marcas, login, «Sin acceso» y administración |
| `movimiento.css` / `.js` | capa común: letra, animaciones, impresión |
| `asistente.css` | el asistente |
| `accesos.css` | login, menú del usuario, candados |

**Estructura (la misma en todos los informes)**
1. **Cabecera** pegajosa de **72 px**, con una línea de 3 px del color de marca debajo. Tres partes: el logotipo a la izquierda
   (vuelve al menú de su marca), el título grande con su subtítulo en el centro, y a la derecha el botón de tema, el menú del
   usuario y «Salir».
2. **Pestañas** (solo en los informes de voz): una pastilla por página, la activa resaltada.
3. **Cuerpo `.tablero`**: ancho de **1.120 a 1.720 px**, centrado. A la izquierda el **panel de filtros** (268 px; en Siebel
   330 px), a la derecha el contenido. Por debajo de 1.120 px hay scroll horizontal en vez de reordenar.
4. **Un solo punto de ruptura**, a **760 px** (móvil): el panel sube y todo se apila.
5. **No** usar `auto-fit` en las tiras de indicadores ni añadir más breakpoints: la composición es fija a propósito.

**Panel de filtros**: sin scroll interno, a la altura de la pantalla. Lo siempre visible (fechas, semana, super, team, agente,
marca, cola…) arriba; el resto, en «Más filtros». Un desplegable con menos de dos opciones **se oculta**. Los filtros son en
cascada: cada lista solo ofrece lo que existe con el resto de la selección.

**Portada y menús**: estilo ejecutivo, marino y grises, tarjetas grandes de marca en fila (3 en la portada, 4 en el menú de
Yoigo, 3 en el de Orange). Debajo de 1.100 px, una por fila.

## 5. Piezas que ya existen (usar antes de inventar)

| Pieza | Clase | Notas |
|---|---|---|
| Tira de indicadores | `.resumen` / `.resumen-dato` (voz), `.kpis` / `.tile` (informes) | tarjeta con radio 16 px, borde fino, filo de 3 px arriba y realce radial; la destacada es `.destacada` |
| Tarjeta KPI con icono | `.tile.kpi-m` | iconos de trazo en `Service/IconosKpi.cs` |
| Bloque | `.tarjeta` (voz), `.card` (informes) | radio 16 px |
| Pareja de columnas | `.rejilla-2` | siempre dos |
| Tabla que se ordena y colorea | `table.ranking[data-mapa]` | `th[data-sentido]` (1 más es mejor, −1 más es peor, 0 sin color) y `td[data-valor]`; los grupos con pocas llamadas pueden ir al final, **sin gris**; **la primera columna se lee entera** (sin «…»), se ajusta a su texto y queda fija a la izquierda al hacer scroll; **total al pie**: `<tfoot><tr class="total">` con «TOTAL», pegado abajo del scroll (`sticky; bottom: 0`), borde superior de 2 px, sin color; nunca una fila de total arriba |
| Gráfica por tiempo | `.crono-tarjeta` con `crono-linea principal/secundaria`, área y pastillas | SVG del servidor |
| Barras horizontales | `.hbarras > .hbarra` | `.hbarra-media` en pareja |
| Árbol desplegable | `#tablaTipologico`, `tr.padre` / `tr.hija` | `ccvbpe.js` |
| Parciales | `_GraficaLineas`, `_Dispersion`, `_EvolucionOrange`, `_Tendencia`, `_PanelFiltros`, `_ErrorDatos` | |

**Gráficas**: se generan en el servidor como SVG. Para que salga la **ficha al pasar el ratón** basta con poner un `<title>` en
cada punto, barra o banda con el formato «Etiqueta · Serie valor · …». `graficas.js` hace el resto (ficha, guía vertical,
desvanecido). Los textos SVG se emiten como `HtmlString` (Razor reserva `<text>`).

## 6. Movimiento

- Tiempos: 160 ms (clic), 220 ms (hover), 440 ms (aparición de tarjetas), 780 ms (cifras que cuentan y barras que crecen), 45 ms
  entre tarjetas hermanas. Curva `cubic-bezier(.22, .61, .36, 1)`, **sin rebote**.
- Las barras crecen con `transform: scaleX`, nunca animando el ancho.
- Foco visible con teclado (2 px del acento).
- **Con «reducir movimiento» no se anima nada** (`prefers-reduced-motion`): hay que respetarlo en todo lo nuevo.
- Hay barra de carga naranja arriba al navegar o aplicar filtros.

## 7. SOLARIS (portada y acceso)

- La plataforma se llama **SOLARIS** y dentro están los informes de GAIA. El logotipo (`_MarcaSolaris`, SVG propio) va en la
  esquina de la portada, los menús, «Sin acceso» y la administración. Los informes siguen con su cabecera propia.
- **Login**: logotipo arriba a la izquierda, el sol a la izquierda y la tarjeta de acceso a la derecha (debajo en móvil). Fondo
  blanco cálido con chispas naranjas; en oscuro, noche cálida con un resplandor detrás del sol.
- El botón «Entrar» va en degradado naranja → ascua con texto negro.

## 8. Lo que NO hay que hacer
- Librerías de gráficas, fuentes o iconos desde un CDN.
- Colores, tamaños de letra o tipografías nuevas «porque queda bien».
- Envolver cifras de una tarjeta en `<span>` sin mirar las reglas genéricas de su contenedor (`.resumen-dato span` ya encogió
  las cifras una vez): las reglas nuevas van con `:is(...)` para ganarles.
- Unificar fórmulas entre páginas «porque deberían ser iguales»: cada informe define sus porcentajes a su manera.
- Poner `<style>` o `<script>` con lógica dentro de las vistas: las vistas solo llevan marcado.
- Olvidar el modo oscuro: probar siempre claro y oscuro, y a 375 px de ancho.

---

## 9. CDM Auditorías Calidad (esta web)

Cómo se aplica la guía en `C:\Proyectos\CDM Auditorias Calidad` (ASP.NET Core MVC, Razor). Tiene dos informes con el
mismo aspecto: **Auditorías** (General y Formación & Calidad, el Power BI «CDM Auditorías Calidad») y **CDM No solución**
(traído de ranking-mvc el 05/10/2026). Verificado contra `site.css` y `site.js` el 05/10/2026.

### 9.1 Hojas, scripts e iconos

| En SOLARIS | Aquí |
|---|---|
| `reporte.css`, `informe.css`, `portal.css`, `movimiento.css` | **una sola hoja**, `wwwroot/css/site.css`, con los nombres de variable de las pantallas de voz (`--acento`, `--texto`, `--superficie`, `--borde`, `--thead`…) |
| `graficas.js`, `movimiento.js` | `wwwroot/js/site.js`: fichas, guía vertical, tablas que se ordenan, cifras que cuentan y filtros |
| `tema.js` | igual (`wwwroot/js/tema.js`, en `<head>`); guarda la elección en `localStorage` con la clave `cdm-tema` |
| `Service/IconosKpi.cs` | `Infraestructura/Iconos.cs`: iconos de trazo y el logotipo (marino con el visto bueno naranja) |
| `_MarcaSolaris`, login, menú del usuario, «Salir» | no hay: la web no está dentro de SOLARIS ni tiene inicio de sesión. La cabecera lleva logotipo con el área, título con migas y los botones Imprimir, Presentar y Tema (9.9) |

- Marca: los dos informes van con `data-marca="orange"` (por defecto); la portada y la página de error, con
  `data-marca="portada"` (marino y grises). Se elige con `ViewData["Marca"]`.
- Cifras: `Formato` (Auditorías, notas en fracción 0–1) y `FormatoNoSolucion` (No solución, porcentajes ya en tanto por
  cien). Los dos escriben dos decimales y `%` con espacio fino que no se parte (U+202F).
- Capturas para revisar el aspecto: Edge sin ventana con `--force-prefers-reduced-motion` (si no, sale a mitad de las
  animaciones) y `--blink-settings=preferredColorScheme=1` (claro) o `=0` (oscuro).

### 9.2 Variables añadidas

| Variable | Qué es |
|---|---|
| `--serie-1` … `--serie-5` | la paleta de series **de la marca de la página** (desde el 07-10-2026, ver 9.10); antes eran fijas |
| `--atencion` / `--atencion-texto` | tramo «atención» de No solución: ámbar `#c99400` para rellenos y `#8a6500` para texto; en oscuro, `#e9b44c` los dos |
| `--serie` | color de una serie **por marca**, fijo (si se filtra una, las demás no cambian): lo ponen `.serie-orange`, `.serie-jazztel`, `.serie-masmovil` y `.serie-yoigo` con `--marca-*` (9.10) |
| `--relleno` | color de una barra **por tono**: `.tono-critico` / `.relleno-malo` (rojo), `.tono-atencion` (ámbar), `.tono-bueno` (verde), `.tono-neutro` / `.relleno-tenue` (gris), `.relleno-proceso` (azul) y `.relleno-atencion` (naranja). `.hbarra-relleno` y `.hbarra-media-relleno` lo usan si está; si no, su color de siempre |
| `--fraccion` | largo de una barra de `.hbarras.libre` (0–1) sobre el hueco que deja su cifra: la cifra nunca se sale |

En las vistas solo van, en línea, posiciones y tamaños de gráficos (`left`, `top`, `width`, `--fraccion`), nunca colores.

### 9.3 Piezas de Auditorías

| Pieza | Dónde |
|---|---|
| Tira de 5 indicadores | `.resumen` / `.resumen-dato` (`.destacada` = la nota); `.resumen-cobertura` es la barra de «Total agentes» |
| Gráficas por tiempo | `.crono-tarjeta`: columnas (`.columna`) y línea suavizada (`crono-linea principal`, `crono-area`, `.meta` discontinua al 50 %) |
| Barras por sector | `.hbarras > .hbarra` con la nota en pareja (`.hbarra-media`); pulsar una barra filtra |
| Top 10 | `table.ranking[data-mapa]` con `th[data-sentido]` y `td[data-valor]` |
| Panel y desplegables | `_PanelFiltros`, `Shared/_Desplegable` (lo usan los dos informes), «Más filtros» con contador |
| Pestañas | `.pestanas` (`.pestana`) y, a la derecha, `.pestanas-vista` con `.segmento` (Día / Semana / Mes) |
| Portada | tres tarjetas de informe como la portada de módulos del portal (9.9): **General**, **CDM No solución** y **GAIA Formación** (Formación & Calidad se abre desde las pestañas) |

### 9.4 Piezas de CDM No solución

Usa las de Auditorías (cabecera, pestañas, panel, `_Desplegable`, `.resumen`, `.tarjeta`, `.rejilla-2`, `.hbarras`,
`.crono-linea`, `.punto`, `table.ranking`, `.aviso`) y añade solo lo que no existía:

| Pieza | Clase | Notas |
|---|---|---|
| Barras sin alto fijo | `.hbarras.libre > .hbarra` | nombre con su detalle (`.sub-ns`), barra con su cifra y una nota en la tercera columna |
| Líneas por marca | `.lineas-ns` con `crono-linea` y `.punto` dentro de `.serie-*` | los tramos que tocan un día parcial, `crono-linea discontinua` |
| Pastilla de tasa | `.chip` + `.chip-critico` / `.chip-atencion` / `.chip-bueno` | la tasa y su desvío frente a la media, con el semáforo pastel |
| Filas que se despliegan | `.filas-ns > details.desplegable-ns > summary.fila-ns` | columnas con `.cols-n3`, `.cols-averia` o `.cols-impedimento`; cabecera `.fila-ns-cabecera` con los colores de `--thead`; cuerpo `.cuerpo-ns` |
| Llamadas de ejemplo | `.muestras-ns` / `.muestra-ns` | filo de 3 px del acento; «Copiar ID» con `[data-copiar]` |
| Atajos de fecha | `.atajos-ns` / `.atajo` (`.activo`) | Todo el periodo, 7 días, 30 días, último mes cerrado |
| Mientras se traen los datos | `.preparando-ns` con `.girando` y `[data-recargar-en]` | la página se vuelve a pedir sola cada 10 s |
| Textos | `.nota-ns` (explicación bajo un título), `.pie-ns` (pie con la fuente), `.estado-ns` (fecha de los datos junto a las pestañas), `.puesto-ns`, `.sub-ns` | |
| Aviso informativo | `.aviso.aviso-info` | el `.aviso` normal es ámbar; este va sobre la superficie |
| Reparto en una línea | `.veredicto-ns` con `span.relleno-*` | cuadradito del color de su `--relleno` y la cifra en negrita (la causa de la no solución) |
| Comprobaciones | `ul.comprobaciones-ns` con `FormatoNoSolucion.ChipComprobacion` | pastilla «Correcto» (verde), «Revisar» (ámbar) o «Nota» (sin color) y el texto al lado |
| Texto largo bajo un nombre | `.sub-ns.envuelve` | el `.sub-ns` normal corta con «…»; este ocupa varias líneas |
| Etiquetas de las gráficas | `.etiqueta-punto` dentro de `.serie-*` (pastilla con tinte, borde y texto de su `--serie`; `.arriba` / `.abajo`) | **cada punto y cada columna con su valor** (pedido del usuario el 06-10-2026); con dos series, la mayor encima y la menor debajo; nada de «máx.», «mín.», «pico» ni media rotulada. Sustituye a las antiguas etiquetas de impacto (`.fin-serie`, `.valor-serie`, `.meta-texto`), que ya no se usan para rotular |

### 9.5 Reglas aprendidas en esta web

- **El panel de filtros pegajoso necesita `z-index`** (20, por debajo de la cabecera, 50): sin él, sus desplegables quedan
  detrás de las tarjetas animadas.
- **`label.opcion[hidden] { display: none }`**: el buscador oculta opciones con `[hidden]` y el `display: flex` de la
  opción lo anulaba.
- **Filtros al marcar**: cada casilla filtra y el desplegable sigue abierto (mismo texto de búsqueda, desplazamiento y
  foco). Si no cabe debajo, se abre hacia arriba (`.hacia-arriba`).
- **Al filtrar no se repiten las animaciones de entrada**: el contenido nuevo lleva `.actualizado` y las cifras pasan del
  valor anterior al nuevo.
- **Fechas en la URL**: las que coinciden con el rango por defecto no se escriben, para que un enlace guardado siga al último
  día con datos. En Auditorías el rango por defecto es el calendario entero; en No solución, los últimos 30 días
  (`data-defecto` en cada fecha y `data-fechas-juntas` en el formulario).
- **Eje X**: la última etiqueta siempre se escribe; las intermedias, solo si no la pisan.
- **Barras con cifra al lado**: el largo sale de `--fraccion` sobre el hueco libre, no de un porcentaje fijo; si no, en
  tarjetas estrechas la cifra pisa la nota.
- **No solución**: las diferencias entre dos porcentajes se escriben con `%` y signo («+0,65 %»), porque son una resta, no una
  variación relativa. Más es peor: la variación que sube va en rojo (`.peor`) y la que baja en verde (`.mejor`).
- **Panel de No solución**: fechas con atajos, Sector, Supervisor, Team leader, Agente y Marca, todo a la vista (no hay
  «Más filtros»); supervisor, TL y agente se encadenan.


### 9.6 Piezas de GAIA Formación (`/gaia`, 06-10-2026)

Mismas cabecera, pestañas, panel, `_Desplegable`, `.resumen`, `.tarjeta`, `.rejilla-2`, `.crono-linea`, `table.ranking`,
`.filas-ns` (filas que se despliegan), `.chip`, `.aviso` y `.preparando-ns` que Auditorías y No solución. Vistas en
`Views/Gaia/` (`_LayoutGaia`, `_InformeGaia`, `_PanelGaia`, una por pestaña y tres gráficas parciales). Los formatos y las
clases de color de los umbrales están en `Views/Gaia/AyudasGaia.cs` (`@functions` no funciona en `_ViewImports`; se importa
allí con `@using static`). Lo nuevo:

| Pieza | Clase | Notas |
|---|---|---|
| Tira de 10 indicadores | `.resumen` con 10 `.resumen-dato` | dos filas de cinco; el 6.º al 10.º entran con su escalón |
| Tira de criterios | `.resumen.en-4` + `.resumen-dato.sin-icono` | cuatro por fila, sin icono, con `.resumen-cobertura` como barra y el peso en la nota |
| Selector de indicador | `.barra-kpi` con `details.multi` y `a.opcion-enlace` (`.activa`) | cada enlace es `data-parcial` y recarga con `?kpi=`; no hace falta JS nuevo |
| Columnas por etapa, semana o día | `_ColumnasGaia` (`.grafica.lineas-ns`, `.columna-serie`; con `PorUmbral`, `.columna-umbral` + `.tono-*`) | cada columna con su valor en una pastilla `.etiqueta-punto`; media discontinua con su cifra en la leyenda; con dos series, la mayor encima y la menor debajo del borde de su columna |
| Línea + volumen | `_LineasGaia` y `_ColumnasGaia` con `GraficoVolumen` (`.grafica.corta`, `.columna-volumen`) | comparten posiciones; el eje X va en el volumen; cada punto y cada columna con su valor en pastilla; con más de ~20 periodos, una de cada dos o menos (siempre la primera y la última) |
| Barra de adherencia | `.medidor` (`.medidor-pista`, `.medidor-relleno` con `--fraccion` y `.tono-*`, `.medidor-marca` en el 40 % y el 70 %) | `.adherencia-cifra` es la cifra grande |
| Celdas frente al total | `.mejor-total` / `.peor-total` | semáforo pastel al 55 %; margen de 1 punto o 10 % del total; agentes con menos de 10 llamadas, en gris (`tr.pocas`) y sin tinte |
| Fila de total | `table.ranking tfoot tr.total` (y `.fila-ns.fila-total` en las listas) | **última fila, en el `tfoot`**, con «TOTAL», pegada abajo del contenedor con scroll (`sticky; bottom: 0`), sobre la superficie, borde superior de 2 px, cifras en 600 y sin tinte; nunca una fila «Total del filtro» arriba |
| Cabecera que no ordena | `.ranking .cab-columna` | no usar `.ordenar` si no ordena: site.js lo ordenaría en el navegador |
| Matriz agente × etapa | `.tabla-matriz-gaia` | primera columna pegajosa; celdas con `mapa-rojo` / `mapa-ambar` / `mapa-verde` según los umbrales de la adherencia |
| Punto de marca | `.marca-punto` con `.serie-*` | color fijo por marca |
| Llamadas desplegables | `.filas-ns.filas-llamadas` con `.cols-llamada`, `.celda-doble`, `.recorta` | la marca y la etapa van bajo el agente para caber en el ancho mínimo; en móvil la fila hace scroll y el detalle no |
| Detalle de llamada | `.detalle-gaia` (dos columnas), `.calificaciones-gaia` (con `.chip` Sí / No / N/A), `dl.datos-gaia`, `.texto-largo`, `.detalle-gaia-ids` | los ID se copian con `[data-copiar]` |
| Paginación | `.paginacion` con `.pestanas-vista` y `.segmento` (`.desactivado`) | de 100 en 100 |
| Aviso plegable | `.aviso-plegable` | los avisos del Excel de nómina, cerrado por defecto |
| Tinte de ejemplo | `.muestra-tinte` (+ `.mejor-total`, `.peor-total`, `.mapa-*`) | muestras de color en las notas |

- Variables: no hay nuevas; todo sale de `--malo`, `--atencion`, `--bueno`, `--mapa-*`, `--serie-*` y `--superficie-2`.
- Iconos nuevos en `Iconos.cs`: `transferir`, `baja-cliente`, `etiqueta` y `carrito`.
- La portada pasa a **tres tarjetas** en fila (General, CDM No solución y GAIA Formación): `.portada` sube a 1.320 px y la
  tarjeta a 410 px; las «ventajas» se quedan en 1.040 px.
- Estado de los datos de GAIA: sin datos, `.preparando-ns` (se recarga sola cada 10 s); tras un fallo sin reintento, el error
  y «Volver a intentarlo». Panel: fechas (por defecto, todo el rango) y siete desplegables, sin «Más filtros».

### 9.7 GAIA Formación: las cinco pestañas de la segunda tanda (06-10-2026)

Rendimiento, Evolución, Comercial, Motivos y Españolización se hicieron con lo de 9.6 más lo que sigue. **Nueve pestañas** en
`.pestanas.compactas` (menos relleno y, si no caben, pasan a dos líneas). En `Views/Gaia/AyudasGaia.cs`, además de los formatos, están
los tipos de las gráficas genéricas (`GraficoGaia`, `SerieGaia`, `SelectorGaia`, `DispersionGaia`, `TramoGaia`) y los constructores
`Grafico("dia" | "semana" | "etapa", grupos, series…)` y `GraficoEspanolizacion(…)`.

| Pieza | Clase / parcial | Notas |
|---|---|---|
| Líneas con varias series | `_LineasGaia` (`.grafica.lineas-ns`, `.sin-eje-x`, `.etiqueta-punto`, `.eje-y.derecho`) | porcentajes, segundos (eje en minutos) o recuentos; **cada punto con su valor en pastilla**, nada de «máx.», «mín.» ni media rotulada; dos series con escalas muy distintas (una cinco veces mayor), eje izquierdo y derecho; la leyenda va arriba y centrada (`_LeyendaGraficoGaia`, con la media y sus cifras) |
| Columnas en grupo | `_ColumnasGaia` (`.columna-serie`) | por etapa, semana o día; un grupo de columnas por punto; el eje de un porcentaje no pasa de 100 %; mismas pastillas, ejes y leyenda que las líneas |
| Colores de serie por posición | `.serie-a` … `.serie-e` (`--serie-1` … `--serie-5`) y `.serie-espana` (azul) / `.serie-colombia` (naranja) | sin variables nuevas; las piezas leen `--serie` y `--relleno` |
| Dispersión de agentes | `_DispersionGaia` (`.grafica.dispersion`, `.punto-disp`, `.etiqueta-eje`, `.esquina`) | puntos HTML, tamaño = llamadas, ficha con `data-ficha`; líneas discontinuas del total en cada eje y flechas hacia lo mejor |
| Semana / Día | `_SelectorVistaGaia` (`.pestanas-vista` con `button.segmento`) y `[data-vistas]` > `[data-vista="semana"]` / `[data-vista="dia"][hidden]` | arriba a la derecha de las gráficas con muchos días (Rendimiento y Evolución, que tienen `PorSemana`); empieza en Semana (eje «2026-W37»); lo cambia site.js sin recargar |
| Selector de enlaces | `_SelectorGaia` (`.selector-campo`, `.barra-kpi`, `a.opcion-enlace`) | `?kpi=`, `?x=`, `?y=`; para pocas opciones, `.pestanas-vista` con `.segmento` (`?nivel=`) |
| Barras de un reparto | `_RepartoGaia` (`.hbarras.libre`, con `.largo` para textos de DataOrb) | «Otros» no cuenta para la escala y, si domina, va sin barra |
| Barra al 100 % apilada | `_ApiladaGaia` (`.apilada` > `.tramo.tono-*`, `.apilada-titulo`) | estado de resolución y sentimientos; leyenda con `.veredicto-ns`; el `title` se vuelve ficha (site.js: `.apilada[title]`) |
| Árbol de motivos | `_NodoMotivoGaia` (`.cols-motivo`, `.nivel-1/2/3`, `.hijos-motivo`, `.hoja`, `.sin-chevron`) | `details` anidados que se pintan a sí mismos; la hoja no se abre |
| Texto largo | `_TextoLargoGaia` (`.resumen-largo`, `.rotulo-largo`) y `.lista-obstaculos` | los resúmenes de la llamada, enteros y en párrafos con su rótulo; los obstáculos, en lista |
| Tablas de las pestañas | `.tabla-rendimiento-gaia`, `.tabla-comercial-gaia`, `.tabla-mapa-gaia`, `.tabla-pares-gaia`, `.tabla-espanolizacion-gaia` | ancho mínimo y scroll en su contenedor; `tr.pocas` en gris; `tfoot tr.total` al pie |

- Motivos: DataOrb los manda en camelCase («facturacionOrCobros», «promoDescuentoNoAplicadoOMalAplicado»); `MotivoLegible` los
  parte, pone «o» y las tildes más habituales («Facturación o cobros»). Lo que ya viene sin tilde y no está en su lista se queda así.
- El nombre de una serie en la ficha no puede llevar una cifra justo tras un espacio («Menos de 60 s»): la ficha parte por la
  primera cifra. Por eso «Cortas (<60 s)» y `NombreFicha`.
- `AvisosCarga` (lo que no se pudo traer, p. ej. la españolización) se enseña como aviso ámbar visible; los del Excel, en el aviso
  plegable.


### 9.8 GAIA Formación: pastillas, total al pie y color por marca (06-10-2026)

- **Etiquetas:** cada punto de una línea y cada columna lleva su valor en una pastilla `.etiqueta-punto` (tinte suave de `--serie`, borde fino y
  texto 600 en el tono oscuro de la serie). Si no caben todas, se rotulan las que caben sin pisarse
  (`AyudasGaia.Rotulados`, con el ancho real de cada pastilla; ver 9.15), siempre el primero y el último. Las pastillas por umbral (adherencia) toman `--malo` / `--atencion` / `--bueno`.
  La dispersión de agentes no rotula nada sobre los puntos: su leyenda lleva los totales.
- **Total al pie:** toda tabla con cifras (Ranking, Estilo, Rendimiento, Comercial, Motivos, Españolización, por marca) lleva `<tfoot><tr class="total">`
  con «TOTAL»; en las listas que no son tabla (árbol de motivos, Llamadas), una `.fila-total` al final.
- **Color por marca filtrada:** `AyudasGaia.MarcaDePagina` (solo ORANGE → `orange`; solo YOIGO y/o MASMOVIL → `ygmm`; solo JAZZTEL → `jazztel`; si no,
  `orange`) se pone en `<html data-marca>` (desde `_LayoutGaia`) y en `#informe[data-marca-pagina]`, y site.js lo copia a `<html>` en las recargas
  parciales. Variables nuevas en `site.css`: `--acento-enlace` (el acento cuando es texto: se usa en enlaces, botones secundarios, iconos de tarjeta; en
  Jazztel es `#7a6200` y no el amarillo) y los bloques `:root[data-marca="ygmm"]` / `"jazztel"` (claro y oscuro) con `--acento`, `--acento-osc`,
  `--acento-enlace`, `--acento-texto`, `--acento-suave`, `--thead`, `--thead-texto` y `--tile-realce(-fuerte)`. Desde el 07-10-2026 cambian también las series (2.2).


### 9.9 Rediseño al estilo del portal SOLARIS (06-10-2026)

Toda la web (portada, General, No solución y GAIA) se llevó al aspecto del portal (`capturas/` de la skill): cabecera con migas,
pestañas en barra, panel plegable, tarjetas con cabecera y pie, tablas con puesto, barrita de volumen y TOTAL al pie, gráficas con
cada punto en pastilla, medidor y anillos. Las clases que ya existían se conservaron; esto es lo nuevo o lo que cambió.

**Paleta y variables** (`site.css`, sección 1). Los neutros son los del portal: claro `#f4f6f9` / `#ffffff` / borde `#e2e6ec` / texto
`#121826`; oscuro, noche gris azulada `#0e1116` / tarjetas `#151a21` / borde `#262d38` / texto `#e7eaf0`. En oscuro, las series azul,
verde, rojo y lila suben un escalón de luz. Variables nuevas:

| Variable | Qué es |
|---|---|
| `--campo` | fondo de los campos del panel de filtros (`#f7f8fa`; en oscuro `#1b2129`) |
| `--cab-hover` | fondo al pasar el ratón por botones y pestañas |
| `--sombra`, `--sombra-2` | elevación neutra de las tarjetas y de la tarjeta de la portada al pasar el ratón (en oscuro, apenas una sombra) |
| `--alto-pestanas` | alto de la barra de pestañas (57 px): el panel pegajoso cuelga de cabecera + pestañas |
| `--tile-filo` | ahora es un tono suave del acento; solo la tarjeta `.destacada` lleva el acento entero |

**Cabecera** (`Shared/_Cabecera.cshtml`). Logotipo con el área apilada (`.logo-texto` + `.logo-area`, en el color de la marca), título
`h1` y migas (`.migas`, `.miga-area`, `.miga-actual`) y a la derecha botones con borde (`.btn-tema`): **Imprimir** (`[data-imprimir]`,
`window.print`), **Presentar** (`[data-presentar]`: `data-presentacion` en `<html>`, sin panel ni botones, pantalla completa; se sale con
`.pres-salir`, Esc o saliendo de la pantalla completa) y **Tema** (`[data-boton-tema]`, con su texto «Tema: automático»). Cada vista
fija `ViewData["Area"]` («Auditorías», «No solución», «GAIA») y `ViewData["Migas"]` (string[]; «CDM» se pone solo); la miga actual
se cambia sin recargar con `#informe[data-miga]` (site.js). La línea de 3 px del acento ya no está bajo la cabecera sino bajo la barra
de pestañas; la cabecera lleva una línea fina. La portada no lleva título ni migas (muestra su lema).

**Pestañas** (`.pestanas`): una barra a todo el ancho, pegajosa bajo la cabecera, con la pastilla activa rellena del acento y la línea
de 3 px debajo. Va **fuera** de `.contenedor` (el ancho de 1.120–1.720 px): `#informe` la lleva y después abre `.contenedor`. A la
derecha, `.pestanas-vista` con `.segmento` (el activo, relleno del acento) o `.estado-ns`. En móvil deja de ser pegajosa.

**Panel de filtros.** Tarjeta con sombra: `Shared/_PanelCabecera` (título «FILTROS», botón `.cmp-plegar` y el raíl `.cmp-panel-rail`),
**grupos** separados por una línea (`.grupo-filtros`; `AyudasGaia.EnBloques(grupos, ["campo", …], …)` reparte los desplegables visibles
en bloques), «Más filtros» (Auditorías) y al pie `.acciones-filtros` con `.btn-panel.primario` (Descargar Excel o CSV) y `.btn-panel`
(Borrar filtros, `data-parcial`), más el estado de los datos. **Plegable** a un raíl vertical: `html[data-panel="plegado"]` (solo con
más de 760 px); el estado se guarda en `localStorage` con la clave `cdm-panel` y lo aplica `tema.js` antes de pintar. El panel sigue sin
scroll propio: un `overflow` recortaría sus desplegables.

**Tira de indicadores.** Misma `.resumen` con sombra; `.destacada` con borde y cifra en `--acento-enlace`. **Tendencia**:
`Iconos.Tendencia("sube" | "baja" | "igual")` pinta ▲ / ▼ / – (texto, no emoji) dentro de `.resumen-variacion` (`.sube`, `.baja`, `.mejor`,
`.peor`, `.neutra`). Solo donde el modelo trae la variación: General (las cuatro primeras) y el «No solución» del Resumen de CDM No solución;
en GAIA no hay variación y no se inventó.

**Tarjetas.** `.tarjeta-cabecera` es una rejilla: título (`h2`), nota (`.tarjeta-nota`, en gris debajo) y, a la derecha, lo demás (selector
Semana/Día, atajos, botón CSV); lleva una línea debajo. La **frase de lectura al pie** es `.pie-tarjeta` o, sin añadir clase, el último
`.nota-ns` de la tarjeta (línea encima y letra algo mayor). Con `.tabla-contenedor` como último hijo, la tabla llega a ras de la tarjeta.

**Tablas.** Cabecera `--thead` en versalitas con flechas de orden (las dos sin ordenar, la que toca en la columna activa; se dibujan con
`clip-path`, sin emoji). `table.ranking.con-puesto` numera las filas (1, 2, 3…) con un contador CSS que sigue al orden que se vea (Top 10 de
General, Ranking, Estilo, Rendimiento, Comercial y Españolización de GAIA). `td.barra-volumen[data-valor]` pinta la **barrita de volumen**
tras la cifra: site.js pone `--v` (0 a 1) sobre la mayor de su columna. **TOTAL al pie** en todas las tablas con cifras (`tfoot tr.total`) y
en las listas (`.fila-ns.fila-total`): se añadió a General (Top 10: el del filtro entero), a No solución (Resumen: el del rango; Equipos: la
suma de las filas; Motivos: la suma de las tipologías; Internet: el de las averías).

**Gráficas.** Las piezas de GAIA pasaron a `Views/Shared` (`_LineasGaia`, `_ColumnasGaia`, `_LeyendaGraficoGaia`, `_SelectorVistaGaia`) y
`AyudasGaia` se importa en `Views/_ViewImports.cshtml`: las usa también General (`Tablero/Graficos/_Columnas` y `_Linea` construyen un
`GraficoGaia`). `GraficoGaia` ganó `SinVolumen` (la ficha no lleva «Llamadas N») y `FichaExtra` (un texto más por periodo, p. ej.
«Nota 85,20 %»); `AyudasGaia.Ficha(g, i)` escribe el `<title>` de la banda. Cada punto y cada columna lleva su valor en una
`.etiqueta-punto`; la primera y la última del eje llevan `.al-inicio` / `.al-final` (no tapan los números del eje) y, con tres o más marcas en
un mismo día, `.centro` (apiladas con separación). General: las dos gráficas por tiempo van **en paralelo** desde el 09-10-2026 (antes, a todo el ancho; ver 9.15). No solución: `_LineasNs` y `_EncuestasDiarias` rotulan cada punto (uno de cada pocos con
tantos días, siempre el primero y el último); sin «máx.», «mín.», «pico» ni media rotulada, y se quitaron del CSS `.valor`, `.fin-serie` y
`.meta-texto`. GAIA: el selector Semana / Día (`_SelectorVistaGaia`, empieza en Semana) está ya en Resumen, Estilo, Rendimiento, Evolución,
Comercial y Españolización.

**Medidor y anillos** (GAIA → Estilo, como `capturas/ranking-estilo-orange.png`). `.medidor` es ahora un medio arco SVG: tres zonas en
pastel (`.zona-baja`, `.zona-media`, `.zona-alta`, cortadas en el 40 % y el 70 %), `.medidor-aguja`, `.medidor-eje`, los umbrales
(`.medidor-umbral`, HTML sobre el SVG) y la cifra (`.medidor-cifra`). Los ocho criterios son `.anillos > .anillo.alto|medio|bajo` (arco con el
color del umbral y pista pastel, `.anillo-cifra` dentro, `.anillo-peso` debajo, con los umbrales 40 % / 70 % de la vista). `.rejilla-estilo`
los pone lado a lado.

**Portada** (`Home/Index`). `.modulos > a.modulo`: icono de trazo (`.modulo-icono`), etiqueta (`.etiqueta`), título con rótulo (`h2 small`),
`.descripcion` y el pie con el estado de los datos (`.modulo-estado.ok` con punto verde, `.en-curso` ámbar que late, sin clase en gris) y la
flecha (`.flecha`). Al pasar el ratón sube, dibuja la barra del acento y colorea la flecha. La portada solo recibe del controlador los datos
de Auditorías; el estado de No solución y de GAIA se lee de `ServicioNoSolucion.Cache` y `ServicioGaia` (solo lectura, con `@inject`): si se
quiere quitar ese acoplamiento, el controlador tendría que pasar esas dos fechas a `MenuModelo`.

**Color por marca en No solución.** `_LayoutNoSolucion` pone `ViewData["Marca"]` (`MarcaDePagina(Model.Filtros?.Marca)`: solo ORANGE → `orange`;
solo YOIGO y/o MASMOVIL → `ygmm`; solo JAZZTEL → `jazztel`; si no, `orange`) y `#informe[data-marca-pagina]` lo repite para que site.js lo copie
a `<html>` en las recargas parciales. General no tiene filtro de marca y se queda con su color.

**Imprimir.** `@media print` deja solo la pestaña activa y su contenido, sin panel ni botones, con las tarjetas sin partir; site.js pasa la
página a tema claro mientras se imprime (`beforeprint` / `afterprint`).

**Barra de carga.** Además de al filtrar, sale al pulsar un enlace a otra página (menos las descargas) y se quita al volver (`pageshow`).


### 9.10 Series por marca, colores por etapa y tablas compactas (07-10-2026)

**Las gráficas siguen a la marca.** `--serie-1` … `--serie-5` se definen por `[data-marca]`, en claro y en oscuro, con las paletas de 2.2: Orange (y por
defecto) naranja / tostado / melocotón / teja / gris cálido; Yoigo y MásMóvil (`ygmm`) morado / magenta / índigo / lila / gris violáceo; Jazztel amarillo
oscurecido / ocre / carbón / ámbar / arena. Las clases `.serie-a` … `.serie-e`, `.serie-espana` / `.serie-colombia`, `.columna-volumen`, los puntos de la
dispersión, las barras de reparto y la barra de carga leen esas variables y cambian solas con `?marca=`. Al haber una sola paleta por marca, las gráficas con
dos series usan la 1 y la 2 (Comercial: «Ventas» pasó de `serie-c` a `serie-b`, porque el índigo de Yoigo se parecía demasiado a su morado). Excepción: cuando
cada serie **es una marca** (evolución por marca de No solución, `.marca-punto`, el punto de la tabla de Actualización BD), `.serie-orange`, `.serie-yoigo`,
`.serie-masmovil` y `.serie-jazztel` usan `--marca-orange` `#f16e00`, `--marca-yoigo` `#ab3e91`, `--marca-masmovil` `#3a3a3a` y `--marca-jazztel` `#d4a900`
(en oscuro, `#d777bd`, `#c9c9c9` y `#ffd200` para leerse). Bueno / malo, semáforo y sentimientos no cambian.

**Columnas por etapa de formación.** Variables `--etapa-pre` (tono claro de la marca: `color-mix` del `--serie-1` con la superficie) y `--etapa-aseg` (el
`--serie-1`) y clases `.etapa-pre` / `.etapa-aseg` (fijan `--serie` y `--relleno`). `GraficoGaia.PorEtapa` (lo pone `Grafico("etapa", …)`) hace que, con **una
sola serie**, `_ColumnasGaia` ponga la clase de su grupo (`AyudasGaia.ClaseEtapa(titulo)`: «Preconexión» o «Aseguramiento») en cada columna y cada pastilla, y
`_LeyendaGraficoGaia` rotule «Preconexión» y «Aseguramiento». Lo usan Resumen («Avance por etapa»), Estilo («Adherencia por etapa», que **ya no colorea por
umbral**) y Rendimiento («TMO por etapa»). Con dos series por etapa (cortas y sin contexto, ofrecimientos y ventas, España y Colombia) se usan dos colores de
la paleta. `PorUmbral` sigue en `GraficoGaia` pero ninguna vista lo usa.

**Tablas compactas** (regla 10). Una sola línea por fila, relleno vertical de 6 px (filas de 32 px). La primera celda es `td.celda-nombre` con
`div.nombre-fila` > `span.nombre-texto` (se corta con «…») y `span.pastilla-dato[title]` (pastilla pequeña gris con el dato corto: en GAIA, la oleada, `Ana Pérez  740`).
Con `.con-puesto`, el número va dentro de `.nombre-fila` (no antes de ella). Se quitaron los subtítulos `.sub-ns` bajo el nombre: Ranking, Estilo (matriz),
Rendimiento y Comercial llevan la oleada en la pastilla y el sector en el `title`; en No solución, Equipos pasa su detalle al `title` y los días peores llevan el
día de la semana en la pastilla; Españolización no lleva oleada (su modelo de agente no la trae) y General no tenía subtítulo. **Llamadas**: la fecha y la hora van en una
línea y el agente, la marca y la etapa abreviada («Pre 1», «Aseg 3») tienen su propia columna (`.cols-llamada`, 9 columnas; en móvil la fila hace scroll con 940 px de ancho
mínimo). Las filas de las listas desplegables (`.fila-ns`) bajaron a 7 px de relleno.


### 9.11 «Ranking y Estilo», «Motivo de contacto», indicador + volumen y fuera medias y grises (07-10-2026)

**Reglas nuevas en toda la web.** (1) **Sin líneas de media, de meta ni de umbrales** en ninguna gráfica, ni como clave de la leyenda: se quitaron
`GraficoGaia.Media`, `Referencias` y `Extras` (y `ItemLeyendaGaia`), la línea de meta de General, la media de No solución (líneas y columnas) y las líneas del
total de la dispersión de Evolución; también `.meta` del CSS. Los colores por umbral siguen en el medidor, los anillos y la matriz, pero sin rotular
«40 %» ni «70 %». (2) **Ninguna fila en gris** por tener pocas llamadas: `.ranking tr.pocas` ya no tiene estilo (en General la clase solo sirve para ordenarlas al
final y la nota lo dice). El semáforo pastel de `colorearTabla` incluye ahora esas filas.

**Indicador + volumen** (`Views/Shared/_IndicadorVolumenGaia.cshtml`, modelo `AyudasGaia.IndicadorVolumenGaia(Id, Indicadores, Semanas, Dias)` con
`IndicadorGaia(Clave, Nombre, Valor, Segundos)`). Una línea suavizada (`AyudasGaia.TrazosSuaves`, Catmull-Rom) con su área en degradado
(`.degradado-ini` / `.degradado-fin`, del `--serie-1` al 32 % a transparente), las llamadas como columnas grises detrás (`.columna-gris`, 34 % del alto) con su
cifra gris (`.valor-volumen`), el eje Y del indicador ajustado al rango de los datos más un 10 % (no desde 0) y una pastilla en cada punto. Con varios indicadores
lleva pestañas en pastilla (`.pastillas-indicador` > `button.segmento[data-indicador-boton]`) y siempre el selector Semana / Día (`[data-ind-vista]`). El servidor pinta
**todos** los bloques (`.indicador-bloque[data-indicador][data-vista]`, con `hidden` los no elegidos) y site.js (`mostrarIndicador`) enseña el que toca sin recargar y
cambia el título (`[data-ind-nombre]`). Se usa en Ranking y Estilo (adherencia y sus ocho criterios) y en la evolución del Resumen de GAIA (un solo indicador, el KPI elegido).

**«Ranking y Estilo»** (`/gaia/ranking`; `/gaia/estilo` redirige; modelo `PaginaRankingEstiloGaia`, vista `Views/Gaia/RankingEstilo.cshtml`) sustituye a las
pestañas Ranking y Estilo. De arriba abajo: medidor (`.medidor`, sin umbrales rotulados, con `.medidor-pie` «N llamadas · fechas») y los ocho anillos en el orden del
portal («no puntúa» donde no hay peso); evolución con pestañas de indicador; **ranking general** (segmento Supervisor / Formador / Oleada / Agente con `UrlAgrupar`,
buscador `q` en `.buscar-tarjeta`, columnas de `PaginaRankingEstiloGaia.Columnas` con `data-sentido` para el semáforo, `CeldaRanking` para escribirlas, barrita de
volumen en «Q llamadas», «Sector» solo al agrupar por agente, TOTAL con `Model.Total`; `.tabla-ranking-estilo` hace scroll dentro de su caja); **ranking tipológico** por
motivo (segmento Motivo 1/2/3, `tr.fila-padre[data-hijos]` con «+» / «−» y `tr.hija[data-padre][hidden]`; el nivel 3 no despliega; el semáforo toma su escala de las filas principales
y se aplica también a las hijas; `MotivoDe` deja «Sin asignar» como viene); y, al final, la adherencia por etapa (preconexión / aseguramiento, sin umbrales) y la matriz agente × etapa.

**«Motivo de contacto»** (`/gaia/motivos`, `PaginaMotivosGaia`, `Views/Gaia/Motivos.cshtml`). Cuatro tarjetas (`.resumen.en-4` > `.resumen-dato.sin-icono.con-barra`) con
`.resumen-nota` («histórico X · mejor / peor / igual», más bajo es mejor en TMO, % problemas no resueltos y % rellamada; el veredicto compara el total con `Model.Historico`) y
`.resumen-barra` (tono bueno / crítico / neutro); burbujas rellamada–TMO (`_BurbujasGaia`, `.punto-disp.burbuja`, ficha con nombre y cifras, sin líneas de total); sentimiento del
cliente (`.sentimientos`: «Inicial» gris y «Final» en el color de la marca); rellamada 72 h por rol (`.roles`, `.rol-pista` con `.rol-base` verde pastel y `.rol-rellamadas` rojo, ambos
sobre el máximo de la base, y «438 de 2.537»); obstáculos (`_RepartoGaia` con `.relleno-marca`); mapa de calor «Tema de contacto» (`.tabla-calor`, `td.calor[--i]` = Valor / Maximo con el color
de la marca, columna Total y TOTAL al pie) y «Clientes que más vuelven» (`.tabla-clientes`, ordenable en el navegador, TOTAL = suma de llamadas y rellamadas). El árbol de motivos y el
mapa de afectación desaparecieron (los sustituye el tipológico); se quitaron `_ApiladaGaia`, `_NodoMotivoGaia` y su CSS (`.apilada`, `.tramo`, `.cols-motivo`…).

**Piezas pequeñas.** `Views/_ViewImports.cshtml` importa también `Servicios.Gaia` (las vistas compartidas usan `GrupoGaia`). `.barra-tarjeta` pone segmentos y buscador a la derecha de la cabecera
de una tarjeta (en móvil, debajo y a todo el ancho). Españolización lleva la oleada en la pastilla del agente.


### 9.12 Un solo estilo para las gráficas de línea y la primera columna entera (07-10-2026)

**Gráficas de línea: todas como la de adherencia.** `Shared/_LineasGaia` se rehízo con el patrón «indicador + volumen» (9.11): cada serie suavizada
(`TrazosSuaves`) con su color de la paleta de la marca y, **solo bajo la primera**, el área en degradado (`.degradado-ini` / `.degradado-fin` leen `--serie` de su `<g class="serie-…">`);
las llamadas, la base o las auditorías como columnas grises (`.columna-gris`) detrás, en el mismo plano, con su cifra gris (`.valor-volumen`); el eje Y ajustado al rango de los datos
más un 10 % (no desde 0; TMO con su formato m:ss); cuatro marcas en el eje; una pastilla en cada punto y, con escalas muy distintas, eje derecho para la segunda serie. `GraficoGaia.ConVolumen`
(por defecto sí) lo controla; ya no existe el panel de columnas de color aparte (`GraficoVolumen`, `Baja`, `.grafica.corta`, `.columna-volumen`, `.sin-eje-x` se quitaron). Lo usan Rendimiento (TMO y cortas / sin
contexto), Evolución (el KPI del selector), Comercial, Españolización y la línea de General (con las auditorías detrás). `_IndicadorVolumenGaia` (Ranking y Estilo, Resumen) sigue igual. En
No solución, `_LineasNs` (por marca: sin degradado salvo con una sola marca, y los tramos que tocan un día parcial, rectos y discontinuos) y `_EncuestasDiarias` (encuestadas en línea suavizada con las
llamadas entrantes en columnas grises detrás) usan el mismo trazo. Las gráficas de columnas por etapa (`etapa-pre` / `etapa-aseg`) y la de auditorías de General siguen siendo columnas.

**La columna principal se lee entera (regla 10).** `table.ranking` pasa a `table-layout: auto` y `.ranking col { width: auto !important }` (los `<colgroup>` son solo una pista): ninguna celda lleva
`overflow: hidden` ni `text-overflow: ellipsis` y la primera columna (`th`, `td` y el TOTAL del pie) se ajusta a su texto (`width: 1%; white-space: nowrap`) y queda **pegada a la izquierda**
(`position: sticky; left: 0`, con fondo opaco, también al pasar el ratón y en las filas hijas del tipológico) mientras las cifras hacen scroll horizontal. El puesto y la pastilla de la oleada van dentro de esa celda.
Los textos que no son de tabla y se cortaban (nombres de barras `.hbarra-nombre`, `.rol-nombre`, `.recorta` de las llamadas, `.detalle` de la portada) ahora se parten en dos líneas; en Llamadas el agente tiene
una columna de 270 px. `.recorta` ya no se usa en `td` (un `td` con `display: block` deja de ser celda).


### 9.13 Portada con tres tarjetas y meses de la barra en móvil

- **Portada**: `.modulos` en `repeat(3, …)` — General, CDM No solución y GAIA Formación en una fila (dos por fila por debajo
  de 1.100 px, una en móvil); `.portada` a 1.320 px y cada `.modulo:nth-child(n)` entra con su escalón.
- **Opciones de la barra de pestañas en móvil**: `.pestanas > .pestanas-vista` pasa a varias líneas (`flex-wrap`,
  `border-radius: 16px`) para no ensanchar la página.


### 9.14 T0 y planes de acción (`/t0`, 08-10-2026)

Tercera pestaña de Auditorías (`Views/Tablero/_ContenidoT0.cshtml`, debajo de la tira de 5 indicadores de siempre) hecha con piezas que ya
existían: `.aviso` (hueco de PlanAccion), `.crono-tarjeta` con `_LineasGaia` (% con plan, auditorías T0 en columnas grises detrás),
`.hbarras.libre` con `.sub-ns.envuelve` y los tonos `.tono-bueno` / `.tono-neutro` / `.tono-atencion` / `.tono-critico` por situación,
`table.ranking[data-mapa]` (sector y team, con `a.nombre-fila[data-parcial]` para filtrar, `td.barra-volumen` y TOTAL al pie), `.chip` con el
semáforo pastel en la situación del detalle y `.pastilla-dato` con el legajo, y `ul.lista-ns`. La barra de cobertura de las tarjetas
(`.resumen-cobertura`) lleva ahora el título de su tarjeta como `aria-label`. No hay variables ni clases nuevas.


### 9.15 Gráficas por tiempo de Auditorías en paralelo y pastillas sin pisarse (09-10-2026)

**En paralelo** (pedido del usuario): en General y en Formación & Calidad, «Evolución de auditorías» y «Nota promedio de calidad» van una al
lado de la otra en una `.rejilla-2` (`Views/Tablero/_Informe.cshtml`; en móvil se apilan). Como la de columnas mide 262 px y la de línea 320 px
(`.indicador-fig`), `.rejilla-2 > .crono-tarjeta` es una columna flexible y su `.grafica` crece (`flex: 1 0 auto`) para que las dos tarjetas
acaben igual; todo va en %, así que se estira sin deformarse. `_Columnas` y `_Linea` dicen `AnchoEstimado = 340` (el plano de media tarjeta
mide unos 316 px a 1.280 px de pantalla y 580 px a 1.920) y `MaxEtiquetasX = 7`.

**Pastillas sin pisarse** (`AyudasGaia.Rotulados`, lo usan todas las gráficas con pastillas). Antes saltaba «uno de cada N» como si todas
fueran centradas, y la primera y la última (`.al-inicio` / `.al-final`, el 84 % de la pastilla hacia dentro) pisaban a su vecina. Ahora coloca
cada pastilla con su ancho (46 px en m:ss, 42 en enteros, 58 en porcentajes) sobre un plano de `AnchoEstimado` px y la pone solo si deja 4 px
con la anterior; la última va siempre y, si pisa, se quita la anterior (nunca la primera); con más de 20 días, como mucho una de cada dos. Si
caben todas, van todas. Medido el 09-10-2026: General y Formación sin choques de 1.280 px en adelante (en Mes, los cuatro meses); al ancho
mínimo (1.120 px) y en móvil alguna se roza. En GAIA → Rendimiento y en No solución bajaron los choques (13 → 8 y 21 → 10 a 1.280 px): los
que quedan son de dos series en el mismo punto. Pruebas en `RotuladosTests`.
