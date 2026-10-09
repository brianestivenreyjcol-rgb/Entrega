# Estructura, ejecución y publicación

> Parte del contexto de CDM Auditorías Calidad: el resumen está en `CONTEXTO_IA.md` (raíz del
> proyecto). Las referencias a «sección N» son de cuando todo estaba en un solo fichero:
> 1–2 → `power-bi.md` · 3 y 3 bis → `decisiones.md` · 3 ter → `no-solucion.md` ·
> 4–5 → `estructura-y-ejecucion.md` · 6 → `verificacion.md` · 7 → `pendientes.md` · 8 → `bitacora.md`.

## 4. Estructura del proyecto

```
C:\Proyectos\CDM Auditorias Calidad\
├─ CONTEXTO_IA.md                 ← este documento
├─ README.md                      ← presentación corta para GitHub
├─ docs\guia-de-estilos.md        ← guía de estilos SOLARIS · GAIA (referencia del aspecto)
├─ CDM Auditorias Calidad.sln     ← web + pruebas
├─ publicar.cmd / arrancar.cmd    ← publicación en este equipo (ver 5)
├─ publicacion\                   ← (sin versionar) app publicada + .env de producción
├─ capturas\                      ← (sin versionar) capturas con datos reales
│                                   (la carpeta «Power bi\» con el PBIP se quitó el 02-10-2026; en git, commit 2caa2bd)
├─ CDM Auditorias Calidad\        ← la web (ASP.NET Core MVC, .NET 8)
│  ├─ Program.cs                  ← cultura es-ES, servicios, rutas por atributo
│  ├─ appsettings.json            ← sección "Auditorias" (OpcionesAuditorias)
│  ├─ appsettings.Production.json ← RutaEnv = ..\.env (publicacion\.env)
│  ├─ .env / env.ejemplo          ← credenciales SQL (el .env no se versiona)
│  ├─ Consultas\Auditorias.sql    ← la consulta del PBI, copiada tal cual (sin ORDER BY)
│  ├─ Consultas\Nomina.sql        ← nómina por día con cargo (tarjeta «Total agentes»)
│  ├─ Datos\impedimentos_tabla.json ← No solución: etiquetas de impedimento → categorías (fija)
│  ├─ App_Data\                   ← (sin versionar) caché de No solución en desarrollo (~60 MB)
│  ├─ Controllers\
│  │  ├─ HomeController.cs        ← "/" portada (Menú) y "/error"
│  │  ├─ TableroController.cs     ← "/general", "/formacion", "/{pagina}/descargar", POST "/datos/recargar"
│  │  └─ NoSolucionController.cs  ← "/nosolucion" (+ /equipos, /motivos, /internet), los CSV y POST "/nosolucion/actualizar"
│  ├─ Models\                     ← Auditoria y RegistroNomina (filas), InstantaneaAuditorias,
│  │                                TableroModelo (+ TarjetaKpi, GrupoFiltro…), MenuModelo, CabeceraModelo
│  │  └─ NoSolucion\              ← PaginaNoSolucion (+ una por pestaña), FormatoNoSolucion
│  ├─ Infraestructura\            ← Formato (es-ES, espacio fino), Iconos (trazo + logotipo), EscalaGrafico
│  ├─ Servicios\
│  │  ├─ Configuracion\           ← LectorDotEnv, DatosConexion, OpcionesAuditorias, OpcionesNoSolucion
│  │  ├─ Datos\                   ← RepositorioAuditorias (SQL: auditorías y nómina), AlmacenAuditorias (memoria),
│  │  │                             RecargaPeriodica
│  │  ├─ Tablero\                 ← CalculadoraTablero (las medidas DAX y «Total agentes»), Periodos (fechas DAX),
│  │  │                             FiltrosTablero (URL), PaginaTablero (General / Formación)
│  │  ├─ Exportacion\             ← ExportadorExcel
│  │  ├─ NoSolucion\              ← (de ranking-mvc) FuenteBigQuery, CacheNoSolucion, Cubo, Ensamblador,
│  │  │                             Agregados, FiltrosCdm, ServicioCdm, ServicioNoSolucion; (de aquí)
│  │  │                             CausaNoSolucion (atención o proceso), ClasificadorEtiquetas
│  │  └─ Comun\                   ← (de ranking-mvc) FormatoPython, ExportacionCsv, Errores
│  ├─ Views\
│  │  ├─ Shared\_Layout, _Cabecera ← documento base (data-marca, tema.js) y cabecera común
│  │  ├─ Home\Index.cshtml        ← portada (solo «General»)
│  │  ├─ Tablero\Index            ← cabecera + _Informe
│  │  ├─ Tablero\_Informe         ← lo que se sustituye al filtrar: pestañas, panel, tira, rejillas
│  │  ├─ Tablero\_PanelFiltros, _ErrorDatos (_Desplegable está en Shared: lo usan los dos informes)
│  │  ├─ Tablero\Graficos\        ← _Columnas, _Linea, _Sectores (hbarras), _TopAuditores (ranking)
│  │  └─ NoSolucion\              ← _LayoutNoSolucion, _InformeNoSolucion (#informe), _PanelNoSolucion,
│  │                                Resumen, Equipos, Motivos, Internet, _LineasNs, _EncuestasDiarias,
│  │                                _BarrasNs (hbarras), _MuestrasNs
│  └─ wwwroot\                    ← css\site.css (variables de la guía), js\tema.js, js\site.js, favicon.svg
└─ CDM Auditorias Calidad.Tests\  ← xUnit: PeriodosTests, CalculadoraTableroTests, NoSolucionTests y
                                    CdmTests (de ranking-mvc, con Fixtures\nosolucion_python.json) y
                                    CausaNoSolucionTests (causa, repetidas, palabras clave); 290 casos
```

Parámetros de la URL: `desde`, `hasta` (yyyy-MM-dd), `mes` (yyyy-MM), `sector`, `super`,
`team`, `auditor`, `cargo`, `base` (repetibles), `vista` (`dia` / `semana` / `mes`).

`OpcionesAuditorias` (appsettings → `Auditorias`): `RutaEnv`, `MinutosRecarga` (30),
`SegundosConsulta` (300), `MetaCalidad` (0,5), `CargosFormacion` (filtro de la página
Formación), `CargosAgente` (quién cuenta como agente en «Total agentes»).

`OpcionesNoSolucion` (appsettings → `NoSolucion`): `Odbc` (`DSN=BQCOL;`), `RutaCache`
(`App_Data\cache_nosolucion.json`; en producción `..\datos\cache_nosolucion.json`), `Ventana` (90),
`DiasProvisionales` (5), `Tramo` (45), `RefrescoHoras` (12), `RutaTablaImpedimentos`.

Parámetros de `/nosolucion…`: `desde`, `hasta`, `servicio`, `supervisor`, `tl`, `agente`, `marca`
(repetibles); en Equipos, además, `nivel` (`supervisor` / `tl` / `agente`), `orden`, `dir` y `q`.

---

## 5. Cómo se ejecuta

- **Una sola dirección**: todo va por el **5180** (el usuario lo pidió así el 02-10-2026).
  - **Desarrollo**: abrir `CDM Auditorias Calidad.sln` en Visual Studio y F5 (perfil `http`), o
    `dotnet run --launch-profile http` en la carpeta del proyecto → `http://localhost:5180/general`.
    Lee las credenciales de `CDM Auditorias Calidad\.env`. Como producción usa el mismo puerto,
    hay que cerrar antes la ventana «CDM Auditorias Calidad (5180)».
  - Si hace falta probar algo sin parar producción, levantar una copia temporal en otro puerto
    (`ASPNETCORE_URLS=http://localhost:<puerto>`, `dotnet run --no-build --no-launch-profile`)
    y **pararla al terminar**: el usuario no quiere varias direcciones en marcha.
  - Las vistas Razor se compilan con el proyecto: tras cambiar un `.cshtml` hay que
    recompilar y reiniciar. CSS y JS se sirven directamente (basta recargar el navegador).
  - Antes de compilar hay que parar la web en marcha que use `bin\Debug`: la DLL queda bloqueada.
- **No solución necesita el DSN de ODBC `BQCOL`** (driver Simba de BigQuery, DSN de usuario, con
  la cuenta de Google del usuario): el mismo que usa ranking-mvc. Si falta, la página se queda en
  «Preparando los datos» y enseña el error del último intento. La primera vez (sin caché) tarda
  ~1 min en traer los datos.
- **Pruebas**: `dotnet test "CDM Auditorias Calidad.sln"` (con la web parada).
- **Publicación** (la del 5180, en marcha con la versión actual desde el 02-10-2026):
  1. Cerrar la ventana «CDM Auditorias Calidad (5180)».
  2. `publicar.cmd` → compila en Release en `publicacion\app` y, si no existe, copia el `.env`
     del proyecto a `publicacion\.env`.
  3. `arrancar.cmd` → abre una ventana que sirve en `0.0.0.0:5180` →
     `http://10.148.223.143:5180` (ranking-mvc ya usa el 5173).
  - Desde una consola, lanzar los `.cmd` con la ruta completa
    (`cmd /c "C:\Proyectos\CDM Auditorias Calidad\publicar.cmd"`); con el nombre solo, en este
    entorno, cmd no los encuentra. Doble clic también vale.
- **Capturas sin ventana** (para revisar el aspecto): Edge sin cabeza,
  `msedge --headless=new --force-prefers-reduced-motion --blink-settings=preferredColorScheme=1 --window-size=1680,1000 --screenshot=salida.png http://localhost:5180/general`
  (`preferredColorScheme=0` para el tema oscuro). Sin `--force-prefers-reduced-motion` la
  captura sale a mitad de las animaciones de entrada (y con `--virtual-time-budget` se cuelga).
  No baja de ~500 px de ancho; para móvil, mejor el panel del navegador en modo móvil. Si el
  panel del navegador está oculto mide 0 px: darle tamaño (`resize_window`) antes de medir.
- **GitHub**: repositorio **público** `https://github.com/brianestivenreyjcol-rgb/Entrega` (rama `main`).
  No se suben `.env`, `publicacion\`, `capturas\`, `bin`/`obj`/`.vs`, `*.user`, `App_Data` ni cachés:
  nunca credenciales ni datos de personas.
