# Entrega — CDM Auditorías Calidad

Web de **calidad y formación del CDM**: los informes de Power BI pasados a ASP.NET Core MVC (.NET 8, C#,
Razor), con sus mismas medidas y filtros. Lleva **tres informes**, los de la portada:

| Informe | Dirección | Qué enseña | Datos |
|---|---|---|---|
| **General** (CDM · Calidad) | `/general`, `/formacion`, `/t0` | Pestañas General, Formación & Calidad y T0 y planes de acción: 5 indicadores, evolución de auditorías y nota, por sector, top 10 de auditores, T0 frente a su plan | SQL Server (`Consultas/Auditorias.sql`, `Nomina.sql`, `AuditoriasT0.sql`), en memoria, cada 30 min |
| **CDM No solución** (CDM · Bogotá) | `/nosolucion` | Encuesta de solución de Call Bogotá (YOIGO, MASMOVIL, JAZZTEL y ORANGE): resumen, equipos, motivos y sin acceso a internet | BigQuery por ODBC (DSN `BQCOL`), cubo de 90 días en disco, se renueva cada 12 h |
| **GAIA Formación** (GAIA · Agentes nuevos) | `/gaia` | Agentes nuevos en preconexión y aseguramiento: adherencia al estilo, rellamada, no solución, ventas y las llamadas una a una (9 pestañas) | Excel de nómina (hoja Oleadas) + BigQuery (`Consultas/Gaia*.sql`), se recarga sola al guardar el Excel |

## Para que se vea igual

Todo lo que se ve está en el repositorio y **no se carga nada de fuera** (ni CDN, ni Google Fonts, ni Bootstrap):

- `CDM Auditorias Calidad/wwwroot/css/site.css` — una sola hoja con las variables (colores por marca, tema claro y oscuro,
  un solo punto de ruptura a 760 px), las tarjetas, tablas, pestañas, filtros y gráficas.
- `CDM Auditorias Calidad/wwwroot/js/tema.js` (tema claro/oscuro, se carga en el `<head>` para que no parpadee) y
  `wwwroot/js/site.js` (filtros que se aplican al marcar, pestañas sin recargar, imprimir, presentar).
- `CDM Auditorias Calidad/wwwroot/favicon.svg` y los iconos de trazo en `Infraestructura` (`Iconos`), dibujados en SVG.
- `CDM Auditorias Calidad/Views/` — las vistas Razor; las piezas comunes (cabecera, panel de filtros, desplegables, gráficas
  de línea y columnas, leyendas) están en `Views/Shared`. Las gráficas son SVG con los textos en HTML: no hay librería de gráficas.
- Letra: **Segoe UI Variable** (la de Windows 11). En Windows 10 cae a Segoe UI; en otro sistema, a Helvetica/Arial y
  se verá algo distinto.
- `docs/guia-de-estilos.md` — la guía SOLARIS · GAIA con las reglas (colores, variables, piezas que ya existen, lo que no
  hay que hacer); la **sección 9** es lo propio de esta web. Para cambiar pantallas sin romper el estilo, la skill y el
  agente `frontend-solaris` están en `docs/compartir/` (con su `LEEME.md`).

Si se cambia algo de lo que se ve, hacerlo en esas hojas y piezas (nada de colores ni estilos sueltos en las vistas).

## Ponerlo en marcha

1. Copiar `CDM Auditorias Calidad/env.ejemplo` como `CDM Auditorias Calidad/.env` y poner las credenciales de SQL Server
   (el `.env` no se sube al repositorio).
2. Para No solución y GAIA: el driver ODBC de BigQuery (Simba) con un DSN llamado `BQCOL`; GAIA necesita además acceso a la
   carpeta compartida del Excel de nómina (ruta en `appsettings.json`, sección `Gaia`).
3. Abrir `CDM Auditorias Calidad.sln` en Visual Studio y pulsar F5, o ejecutar `dotnet run --launch-profile http` en
   `CDM Auditorias Calidad/` → `http://localhost:5180/`.
4. Pruebas: `dotnet test "CDM Auditorias Calidad.sln"`.
5. Para servirla a los compañeros: `publicar.cmd` y luego `arrancar.cmd`, en el puerto 5180
   (`http://<IP del equipo>:5180/`). Solo puede haber una en marcha a la vez.

## Documentación

**`CONTEXTO_IA.md`** es el resumen: qué es, datos, cómo se prueba y publica, decisiones y pendientes. El detalle, por
temas, está en **`docs/contexto/`** (análisis del Power BI y medidas DAX, decisiones, No solución, GAIA Formación, T0 y
planes, estructura, verificación, pendientes y bitácora).
