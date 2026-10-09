# Verificación hecha

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del
> proyecto). Las referencias a «sección N» son de cuando todo estaba en un solo fichero:
> 1–2 → `power-bi.md` · 3 y 3 bis → `decisiones.md` · 3 ter → `no-solucion.md` ·
> 4–5 → `estructura-y-ejecucion.md` · 6 → `verificacion.md` · 7 → `pendientes.md` · 8 → `bitacora.md`.

## 6. Verificación hecha

- **01-10-2026**: cifras de las tarjetas cuadradas con SQL directo sobre la misma consulta
  (General, sin filtros, datos 01-08 → 01-10): total 15.148 (▲106,0 % frente a 7.355 del 01-08
  al 01-09), semana 883 (28-09 → 01-10) frente a 1.017 (21-09 → 24-09) = ▼13,2 %, mes 28 frente
  a 166 = ▼83,1 %, nota 50,0 % frente a 47,8 % = ▲2,2 pp.
- Excel del sector «CO Atención Jazztel»: 1.549 filas = la barra del gráfico.
- Interacción en el navegador: clic en sector, vista Mes, Atrás, borrar filtros, «Actualizar»
  (recarga y conserva filtros). Sin errores de consola.
- Publicación probada en local (Production, puerto 5181 solo en localhost) y luego parada.
- **02-10-2026, guía de estilos**: capturas en claro y oscuro (portada, General, Formación a
  1366 px); botón de tema (auto → claro → oscuro → auto), fichas con guía vertical, tabla
  coloreada y ordenable, «Más filtros» con contador; sin desbordes a 375 px (portada, General,
  Formación) y sin errores de consola. Revisado que no hay colores escritos en las vistas, ni
  `<style>`/`<script>` en ellas, ni emojis, ni negritas de más de 600.
- **02-10-2026, «Total agentes»** cuadrado con SQL directo (General, sin filtros, 01-08 → 02-10):
  1.720 agentes en nómina, 1.487 con auditoría (86,45 %).
- **02-10-2026, filtros**: a 1440 × 900, el desplegable de Mes queda por encima de las
  tarjetas (medido con `elementFromPoint` en tres puntos que pisan el contenido) y el de Cargo
  (en «Más filtros») se abre hacia arriba dentro de la pantalla. Marcar «Septiembre» filtra al
  momento (15.340 → 7.933) con el desplegable abierto y el foco en la casilla; marcar otra da
  «Varias selecciones (2)»; «Quitar selección» vuelve a todo; Escape cierra.
- **02-10-2026, período anterior**: con `?mes=2026-09`, septiembre 7.933 frente a agosto 7.189 =
  +10,35 % y nota 51,54 % frente a 48,30 % = +3,25 pp, igual que con SQL directo; sin filtros,
  «Sin período anterior con datos».
- 30 pruebas unitarias en verde (fechas DAX, período anterior, tarjetas, «Total agentes»,
  filtros en cascada, top 10, vistas, detalle, URL).
- **05-10-2026, CDM No solución** (copia temporal en el 5190, ya parada; producción sin tocar):
  - El cubo se trajo de BigQuery desde esta web (dos tramos, ~1 min, 60 MB) y la página pasó sola de
    «Preparando los datos» al informe.
  - Últimos 30 días (05-09 → 04-10): 15,00 % de no solución, 15.612 no solucionadas de 104.060
    encuestadas, 364.793 llamadas, cruce con nómina 99,98 %. El CSV general trae 15.612 filas, igual
    que la tarjeta; el de un impedimento y los de equipos y tipologías también descargan.
  - Marcar «YOIGO» en Marca filtra al momento (sinAccesoInternet: 6.018 → 1.679 encuestadas) con el
    desplegable abierto; el buscador de Equipos y las filas desplegables funcionan.
  - Capturas en claro y oscuro de las 4 pestañas y la portada; sin desbordes a 375 px; sin errores de
    consola. El informe General de Auditorías sigue igual (se tocaron piezas compartidas).
  - «Copiar ID» no se pudo pulsar con un clic real (el panel del navegador no pintaba); usa lo mismo
    que ranking-mvc (portapapeles y, por HTTP con IP, `execCommand('copy')`).
  - 240 pruebas en verde (las de Auditorías y las de No solución traídas de ranking-mvc, que
    comparan con la salida del Python de referencia).
