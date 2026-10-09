# Análisis del Power BI y medidas DAX

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del
> proyecto). Las referencias a «sección N» son de cuando todo estaba en un solo fichero:
> 1–2 → `power-bi.md` · 3 y 3 bis → `decisiones.md` · 3 ter → `no-solucion.md` ·
> 4–5 → `estructura-y-ejecucion.md` · 6 → `verificacion.md` · 7 → `pendientes.md` · 8 → `bitacora.md`.

## 1. Objetivo

Pasar a web el informe de Power BI **«CDM Auditorías Calidad»** (auditorías de calidad de
llamadas/chats del call center), manteniendo sus pantallas, filtros y cálculos.

- Power BI de origen: estaba en `Power bi\` (formato PBIP: `*.SemanticModel` = modelo y
  consultas, `*.Report` = páginas y visuales, todo en texto). **El usuario quitó esa carpeta
  del proyecto el 02-10-2026**; sigue en el historial de git (commit `2caa2bd`:
  `git show 2caa2bd:"Power bi/…"`). Todo lo necesario de él está resumido en la sección 2.
- Web: `CDM Auditorias Calidad\` (lo creó el usuario con la plantilla MVC de Visual Studio).
- Mismo estilo de trabajo que su otro proyecto, `C:\Proyectos\ranking-mvc` (MVC + Razor, sin
  API JSON, acceso a datos en `Servicios/`). De allí se reutilizaron ideas (lector de `.env`,
  gráficas SVG con textos en HTML) y, el 05-10-2026, se **copió** el motor de CDM No solución
  (sección 3 ter): es una copia, no código compartido, así que un arreglo en un proyecto no
  llega solo al otro.

---

## 2. Análisis del Power BI

### 2.1 Modelo (`CDM Auditorias Calidad Prueba.SemanticModel/definition`)

| Tabla | Qué es |
|---|---|
| `Auditorias` | Importación de una consulta SQL (ver 2.2). Columnas: `Fecha`, `legajo`, `Sector`, `Super`, `Team`, `Agente`, `ID_Llamada`, `CorreoAuditor`, `Nombre_Auditor`, `Cargo_Auditor`, `Respuesta` (nota 0–1), `Base` (`WEB` / `ICEBERG`). |
| `Calendario` | Tabla calculada `CALENDAR(MIN(Auditorias[Fecha]), MAX(Auditorias[Fecha]))`, marcada como tabla de fechas. Columnas `Año`, `NumMes`, `Mes` (`"MMM"`), `Día` (= `DAY()`, día del mes), `Semana` (= `WEEKNUM(fecha, 2)`, semana que empieza en lunes), etc. |
| `Fecha_Parametro` | Parámetro de campo con 3 opciones: `Día`, `Semana`, `Mes` → cambia el eje X de los dos gráficos de evolución. |
| `Medidas` | Medidas DAX (ver 2.3). |

Relación única: `Auditorias[Fecha]` → `Calendario[Fecha]` (el calendario filtra a las
auditorías, no al revés).

### 2.2 Consulta SQL de auditorías

Servidor `10.148.226.40\REPORTING`, base `REPORTING` (usa nombres de 3 partes, cruza bases).
Copia en `CDM Auditorias Calidad/Consultas/Auditorias.sql`, igual que la del PBI salvo dos
cosas: sin el `ORDER BY` final y con la ventana de ICEBERG ampliada a `- 3` (ver más abajo).
La web la lee **en cada carga** desde la carpeta de la aplicación (en desarrollo, la del
proyecto; en producción, `publicacion\app`): un cambio en el `.sql` vale con pulsar
«Actualizar». Por partes (CTE):

- **`NominaAntiguedad`** (líneas 8–26): `Planificacion.Nomina.NominaAntiguedad`, una fila por
  legajo y día (`ROW_NUMBER` por fecha y legajo) → sector (`sec_descrip`), super, team, nombre.
- **`Nomina`** (28–46): `Planificacion.Nomina.Nomina_User_Avaya` (legajo ↔ usuario Avaya por
  día) cruzada con la anterior → la nómina con usuario Avaya, que es la que se usa para saber
  de quién es cada auditoría.
- **`Auditores`** (48–71): `NominaAntiguedad` + `Planificacion.Nomina.PD_Usuarios` (correo) →
  nombre y cargo del auditor.
- **`Auditorias`** (73–135) = **WEB**: une `Reporting.WO.AuditoriasWhatsapp`,
  `AuditoriasJazztel` y `AuditoriasOrange`.
  - WhatsApp y Jazztel: la nota viene en `Respuesta` (texto, a veces con `%`; si es > 1 se
    divide entre 100).
  - Orange: la nota se calcula con pesos: Saludo 10 %, Soy claro/fiable 15 %, Solucionó
    25 %, Resumió 20 %, Pregunta solución 20 %, Despedida 10 % (`SI` o `N/A` puntúan).
- **`WEB`** (137–188): cruza esas auditorías con `Nomina` por fecha y usuario Avaya del
  agente (**INNER JOIN**: auditorías sin agente en nómina ese día se pierden) y con
  `Auditores` por correo (normaliza `@masorange.es` → `@orange.es`).
- **`ICEBERG`** (190–222): `ModulosIceberg.Calidad.PlantillaCalidadUnificada` (nota en
  `Nota Calidad` sobre 100), con `Nomina` por legajo y fecha. Trae **3 meses atrás + el mes en
  curso** (`DATEADD(MONTH, DATEDIFF(MONTH,0,GETDATE())-3, 0)` hasta fin del mes actual): en el
  PBI era `- 2`; lo cambió el usuario el 02-10-2026 para tener julio.
- Al final: `WEB UNION ALL ICEBERG WHERE Agente IS NOT NULL`.

Medido el 01-10-2026: ~15.150 filas (WEB 5.638 desde 01-08; ICEBERG 9.510 desde 01-08), la
consulta tarda ~2 s.

### 2.2 bis Consulta de nómina (de la web, no del PBI)

`CDM Auditorias Calidad/Consultas/Nomina.sql`, para la tarjeta «Total agentes» (ver 3): es la
misma cadena que el CTE `Nomina` de arriba (`Nomina_User_Avaya` + `NominaAntiguedad` con
`RN = 1`) pero devolviendo además el **cargo** (`car_descrip`). Parámetros `@Desde`/`@Hasta` =
rango de las auditorías cargadas. Devuelve `Fecha, legajo, Sector, Super, Team, Cargo`
(~205.000 filas del 01-08 al 02-10, ~2,5 s).

Lo que se vio al explorarla (02-10-2026, desde el 01-08): unos 2.000 legajos con usuario
Avaya; por cargo, 1.555 «Agente», 259 «Agente en Capacitacion», 106 «Team Leader»,
29 «Formador», 21 «Técnico de Calidad», 18 «Aprendiz Sena Etapa Productiva»… De los auditados,
1.433 son «Agente», 121 en capacitación, 57 Team Leader, 16 aprendices SENA y 15 técnicos de
calidad. `Nomina_User_Avaya` trae también **fechas futuras** (planificadas, hasta noviembre).

### 2.3 Medidas DAX y cómo se reproducen

| Medida | DAX | En la web |
|---|---|---|
| Total Auditorías | `COUNTROWS(Auditorias)` | nº de filas filtradas |
| Nota Promedio de Calidad | `AVERAGE(Auditorias[Respuesta])` | media de las notas no nulas |
| Agentes Auditados | `DISTINCTCOUNT(Auditorias[legajo])` | **sustituida** por «Total agentes» (ver 3), a petición del usuario |
| Auditorías de la Semana | del lunes de la semana de `MAX(Calendario[Fecha])` hasta esa fecha | igual |
| Auditorías Semana Anterior | la anterior con `DATEADD(-7, DAY)` → lunes..(máx − 7) | igual |
| Auditorías del Mes | `DATESMTD` → día 1 del mes de la fecha máxima hasta ella | igual |
| Auditorías Mes Anterior | `DATESMTD` sobre `DATEADD(-1, MONTH)` | igual |
| Var % vs período anterior (total) | mismo filtro de fechas desplazado `-1 MONTH` | **cambiada**: el mismo tiempo, justo antes (ver 2.5 y 3) |
| Var pp nota | `(nota − nota período anterior) × 100` | `(nota − nota del mismo tiempo justo antes) × 100` (ver 2.5 y 3) |
| Meta de Calidad | constante `0,5` («para la línea punteada del gráfico») | línea discontinua al 50 % en el gráfico de nota |

`MAX(Calendario[Fecha])` es la última fecha del rango elegido en el filtro de fecha/mes (los
filtros de sector, auditor, etc. no la cambian, porque el calendario no se filtra desde
`Auditorias`). Como `Calendario` está marcada como tabla de fechas, las medidas de
semana/mes/período anterior **anulan el filtro de Mes** y usan solo sus propias fechas.

`DATEADD(-1, MONTH)`: cada fecha pasa al mismo día del mes anterior (si no existe, al último
día), y si el rango incluye el último día de un mes, el resultado llega hasta el último día
del mes anterior (30-09 → 31-08). El resultado se recorta al rango del calendario.

Todo esto está en `Servicios/Tablero/CalculadoraTablero.cs` y `Periodos.cs`, con pruebas.

### 2.4 Páginas del informe

1. **Menú** (portada, imagen de fondo con dos botones): «General» y «Formación y Calidad».
   ⚠️ En el PBI, el botón «Formación y Calidad» apunta a una página que no existe
   (`7f694fdf5024230e8068`).
2. **General** y 3. **Formación & Calidad**: mismo diseño (1672 × 941):
   - Barra izquierda: logo (vuelve al menú), «FILTROS» con botón de borrar filtros, filtros
     **Fecha** (rango), **Mes**, **Sector**, **Super**, **Team**, **Auditor**,
     **Cargo_Auditor**, **Base** (desplegables de selección múltiple), y la tabla
     **Descargable** (Base, Fecha, Super, Team, Agente, Sector, Auditor, Cargo, Respuesta).
   - Cabecera: «AUDITORÍAS | CONTROL Y CALIDAD» + nombre de la página, navegador de páginas
     y selector Día / Semana / Mes.
   - 5 tarjetas KPI (medida HTML `Tarjetas KPI`) con su variación (verde ▲ / rojo ▼).
   - «Evolución de Auditorías» (columnas), «Nota Promedio de Calidad» (línea suavizada),
     «Total Auditorías y Nota Promedio de Calidad por Sector» (barras), «Top 10 Auditores».
   - **Diferencias entre páginas**: Formación & Calidad tiene un filtro de página
     `Cargo_Auditor ∈ {Formador, Formador PP, Técnico de Calidad, Técnico de Calidad PP}` y
     arranca agrupando por **Mes**; General no filtra cargos y arranca por **Semana**.
   - Interacción: al pulsar un auditor del top 10 se resalta su reparto por sector.

### 2.5 Fallos o rarezas del PBI detectados (y qué hace la web)

| PBI | Web |
|---|---|
| El botón del menú «Formación y Calidad» no navega (página inexistente). | Formación & Calidad se abre desde las pestañas del informe (en la portada no tiene tarjeta, a petición del usuario). |
| La página Formación & Calidad muestra el subtítulo «General» (usa la medida `Titulo Encabezado General`). | Muestra «Formación & Calidad». |
| «Día» del eje X es `DAY()` (1–31): con varios meses junta el día 5 de julio con el 5 de agosto. | Agrupa por fecha real (dd/mm). |
| `Semana` y `Mes` no llevan año: entre años distintos se mezclan. | Se agrupa por año + semana / año + mes (la etiqueta sigue siendo el nº de semana o el mes). |
| El «período anterior» de Total y Nota es `DATEADD(-1, MONTH)` de las fechas elegidas: con más de un mes **se solapa** con ellas y es **más corto** (con todo el rango, 01/08–02/10 frente a 01/08–02/09 → +102 %, sin sentido; el usuario lo vio el 02-10-2026). | Se compara con **el mismo tiempo, justo antes** (`Periodos.PeriodoAnterior`); si no hay datos de todo ese período, «Sin período anterior con datos». |
| Si el período anterior no tiene datos, la variación de nota sale como la nota entera en pp (blank = 0). | Muestra «Sin datos del período anterior». |
| La tabla «Descargable» agrupa filas idénticas y suma su `Respuesta`. | Exporta una fila por auditoría. |
| La medida `Meta de Calidad` existe pero ningún visual la usa. | Se dibuja como línea discontinua en el gráfico de nota. |
