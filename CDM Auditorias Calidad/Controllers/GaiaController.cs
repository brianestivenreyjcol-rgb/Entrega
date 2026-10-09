using System.Globalization;
using CDM_Auditorias_Calidad.Models.Gaia;
using CDM_Auditorias_Calidad.Servicios.Comun;
using CDM_Auditorias_Calidad.Servicios.Gaia;
using Microsoft.AspNetCore.Mvc;

namespace CDM_Auditorias_Calidad.Controllers;

/// <summary>
/// GAIA Formación (port del PBI <c>GAIA Formación.pbip</c>): llamadas de los agentes en formación en
/// sus días de preconexión y aseguramiento, según el Excel de nómina de la carpeta compartida.
/// </summary>
/// <remarks>
/// Como en No solución, con la cabecera <c>X-Parcial: 1</c> (la manda site.js) cada pestaña
/// devuelve solo <c>#informe</c>. Cada visita mira, ya servida la página, si el Excel cambió.
/// </remarks>
[Route(PaginaGaia.RutaBase)]
public sealed class GaiaController : Controller
{
    private const string ClaveActualizacion = "GaiaActualizar";

    private readonly ServicioGaia _gaia;

    public GaiaController(ServicioGaia gaia) => _gaia = gaia;

    [HttpGet("")]
    public IActionResult Resumen([FromQuery] PeticionGaia filtros, string? kpi)
    {
        var (datos, f) = Resolver(filtros);
        if (f is null) return Pagina("Resumen", new PaginaResumenGaia { Datos = datos, Cargando = _gaia.Cargando, Error = _gaia.UltimoError });

        var agentesNomina = datos!.Agentes.Count(a => EntraEnNomina(a, f.Peticion));
        return Pagina("Resumen", new PaginaResumenGaia
        {
            Datos = datos, Filtros = f, Cargando = _gaia.Cargando, Error = _gaia.UltimoError,
            Actualizacion = TempData[ClaveActualizacion] as string,
            Total = CalculadoraGaia.Calcular(f.Llamadas),
            Kpi = CalculadoraGaia.Kpi(kpi),
            PorEtapa = CalculadoraGaia.PorEtapa(f.Llamadas),
            PorDia = CalculadoraGaia.PorDia(f.Llamadas),
            PorSemana = CalculadoraGaia.PorSemana(f.Llamadas),
            PorMarca = CalculadoraGaia.Agrupar(f.Llamadas, l => l.Marca),
            AgentesConLlamadas = f.Llamadas.Select(l => l.IdAgente).Distinct().Count(),
            AgentesEnNomina = agentesNomina,
        });
    }

    [HttpGet("ranking")]
    public IActionResult RankingEstilo([FromQuery] PeticionGaia filtros, string? agrupar, string? orden, string? dir, string? q, int motivo = 2)
    {
        var (datos, f) = Resolver(filtros);
        if (f is null) return Pagina("RankingEstilo", new PaginaRankingEstiloGaia { Datos = datos, Cargando = _gaia.Cargando, Error = _gaia.UltimoError });

        var grupo = PaginaRankingEstiloGaia.Agrupaciones.Any(a => a.Clave == agrupar) ? agrupar! : "agente";
        var clave = orden == "texto" || PaginaRankingEstiloGaia.Columna(orden) is not null ? orden! : "llamadas";
        var desc = dir != "asc";
        IEnumerable<FilaRankingGaia> filas = CalculadoraGaia.Ranking(f.Llamadas, grupo);
        if (!string.IsNullOrWhiteSpace(q))
        {
            filas = filas.Where(r => r.Texto.Contains(q.Trim(), StringComparison.CurrentCultureIgnoreCase));
        }
        if (clave == "texto")
        {
            filas = desc ? filas.OrderByDescending(r => r.Texto) : filas.OrderBy(r => r.Texto);
        }
        else
        {
            var valor = PaginaRankingEstiloGaia.Columna(clave)!.Valor;
            var conValor = filas.Where(r => valor(r.Indicadores) is not null);
            filas = (desc ? conValor.OrderByDescending(r => valor(r.Indicadores)) : conValor.OrderBy(r => valor(r.Indicadores)))
                .ThenByDescending(r => r.Indicadores.Llamadas)
                .Concat(filas.Where(r => valor(r.Indicadores) is null));
        }

        var etapas = CalculadoraGaia.PorEtapa(f.Llamadas);
        var matriz = f.Llamadas.GroupBy(l => l.IdAgente).ToDictionary(
            g => g.Key,
            g => (IReadOnlyDictionary<string, double?>)g.GroupBy(l => l.TipoConexion)
                 .ToDictionary(e => e.Key, e => CalculadoraGaia.Estilo(e).Adherencia));
        motivo = Math.Clamp(motivo, 1, 3);

        return Pagina("RankingEstilo", new PaginaRankingEstiloGaia
        {
            Datos = datos, Filtros = f, Cargando = _gaia.Cargando, Error = _gaia.UltimoError,
            Actualizacion = TempData[ClaveActualizacion] as string,
            Total = CalculadoraGaia.Calcular(f.Llamadas),
            PorEtapa = etapas,
            PorSemana = CalculadoraGaia.PorSemana(f.Llamadas),
            PorDia = CalculadoraGaia.PorDia(f.Llamadas),
            AgentesEtapa = Ordenar(CalculadoraGaia.PorAgente(f.Llamadas), i => i.Adherencia, descendente: false).ToList(),
            MatrizEtapas = matriz,
            Etapas = etapas.Select(e => e.Clave).ToList(),
            Agrupar = grupo, Filas = filas.ToList(), Orden = clave, Descendente = desc, Busqueda = q?.Trim(),
            NivelMotivo = motivo,
            Tipologico = CalculadoraGaia.Tipologico(f.Llamadas, motivo),
        });
    }

    /// <summary>«Estilo» está ahora dentro de «Ranking y Estilo» (07-10-2026): los enlaces viejos siguen valiendo.</summary>
    [HttpGet("estilo")]
    public IActionResult Estilo() => LocalRedirect(PaginaGaia.Ruta(PestanaGaia.RankingEstilo) + Request.QueryString);

    [HttpGet("rendimiento")]
    public IActionResult Rendimiento([FromQuery] PeticionGaia filtros)
    {
        var (datos, f) = Resolver(filtros);
        if (f is null) return Pagina("Rendimiento", new PaginaRendimientoGaia { Datos = datos, Cargando = _gaia.Cargando, Error = _gaia.UltimoError });

        return Pagina("Rendimiento", new PaginaRendimientoGaia
        {
            Datos = datos, Filtros = f, Cargando = _gaia.Cargando, Error = _gaia.UltimoError,
            Actualizacion = TempData[ClaveActualizacion] as string,
            Total = CalculadoraGaia.Calcular(f.Llamadas),
            PorDia = CalculadoraGaia.PorDia(f.Llamadas),
            PorSemana = CalculadoraGaia.PorSemana(f.Llamadas),
            PorEtapa = CalculadoraGaia.PorEtapa(f.Llamadas),
            Agentes = Ordenar(CalculadoraGaia.PorAgente(f.Llamadas), i => i.Tmo, descendente: true).ToList(),
        });
    }

    [HttpGet("evolucion")]
    public IActionResult Evolucion([FromQuery] PeticionGaia filtros, string? kpi, string? x, string? y)
    {
        var (datos, f) = Resolver(filtros);
        if (f is null) return Pagina("Evolucion", new PaginaEvolucionGaia { Datos = datos, Cargando = _gaia.Cargando, Error = _gaia.UltimoError });

        return Pagina("Evolucion", new PaginaEvolucionGaia
        {
            Datos = datos, Filtros = f, Cargando = _gaia.Cargando, Error = _gaia.UltimoError,
            Actualizacion = TempData[ClaveActualizacion] as string,
            Total = CalculadoraGaia.Calcular(f.Llamadas),
            Kpi = CalculadoraGaia.Kpi(kpi ?? PaginaEvolucionGaia.KpiPorDefecto),
            KpiX = CalculadoraGaia.Kpi(x ?? PaginaEvolucionGaia.XPorDefecto),
            KpiY = CalculadoraGaia.Kpi(y ?? PaginaEvolucionGaia.YPorDefecto),
            PorDia = CalculadoraGaia.PorDia(f.Llamadas),
            PorSemana = CalculadoraGaia.PorSemana(f.Llamadas),
            Agentes = CalculadoraGaia.PorAgente(f.Llamadas).Where(a => a.Indicadores.Llamadas >= PaginaEvolucionGaia.MinLlamadas).ToList(),
        });
    }

    [HttpGet("comercial")]
    public IActionResult Comercial([FromQuery] PeticionGaia filtros)
    {
        var (datos, f) = Resolver(filtros);
        if (f is null) return Pagina("Comercial", new PaginaComercialGaia { Datos = datos, Cargando = _gaia.Cargando, Error = _gaia.UltimoError });

        var conIntento = f.Llamadas.Where(l => l.IntentoVenta == true).ToList();
        return Pagina("Comercial", new PaginaComercialGaia
        {
            Datos = datos, Filtros = f, Cargando = _gaia.Cargando, Error = _gaia.UltimoError,
            Actualizacion = TempData[ClaveActualizacion] as string,
            Total = CalculadoraGaia.Calcular(f.Llamadas),
            ResultadosVenta = CalculadoraGaia.Reparto(conIntento, l => l.ResultadoVenta),
            CategoriasOferta = CalculadoraGaia.Reparto(conIntento, l => l.CategoriaOferta),
            TiposServicioVenta = CalculadoraGaia.Reparto(f.Llamadas.Where(l => l.TieneVenta == true), l => l.TipoServicioVenta),
            PorDia = CalculadoraGaia.PorDia(f.Llamadas),
            PorSemana = CalculadoraGaia.PorSemana(f.Llamadas),
            PorEtapa = CalculadoraGaia.PorEtapa(f.Llamadas),
            Agentes = Ordenar(CalculadoraGaia.PorAgente(f.Llamadas), i => i.Ofrecimientos, descendente: true).ToList(),
        });
    }

    [HttpGet("motivos")]
    public IActionResult Motivos([FromQuery] PeticionGaia filtros, string? rol)
    {
        var (datos, f) = Resolver(filtros);
        if (f is null) return Pagina("Motivos", new PaginaMotivosGaia { Datos = datos, Cargando = _gaia.Cargando, Error = _gaia.UltimoError });

        // «Histórico»: los mismos filtros sin las fechas.
        var sinFechas = new PeticionGaia
        {
            Marca = filtros.Marca, Sector = filtros.Sector, Oleada = filtros.Oleada, Formador = filtros.Formador,
            Supervisor = filtros.Supervisor, Conexion = filtros.Conexion, Agente = filtros.Agente,
        };
        rol = PaginaMotivosGaia.Roles.Any(r => r.Clave == rol) ? rol! : "agente";

        return Pagina("Motivos", new PaginaMotivosGaia
        {
            Datos = datos, Filtros = f, Cargando = _gaia.Cargando, Error = _gaia.UltimoError,
            Actualizacion = TempData[ClaveActualizacion] as string,
            Total = CalculadoraGaia.Calcular(f.Llamadas),
            Historico = CalculadoraGaia.Calcular(FiltrosGaia.Resolver(datos!.Llamadas, sinFechas).Llamadas),
            Burbujas = CalculadoraGaia.PorAgente(f.Llamadas).Where(a => a.Indicadores.Llamadas >= PaginaMotivosGaia.MinBurbuja).ToList(),
            Sentimiento = CalculadoraGaia.SentimientoInicialFinal(f.Llamadas),
            Rol = rol,
            RellamadaPorRol = CalculadoraGaia.RellamadaPorRol(f.Llamadas, rol),
            Obstaculos = CalculadoraGaia.Obstaculos(f.Llamadas, maximo: 10),
            Tema = CalculadoraGaia.MotivoPorSemana(f.Llamadas),
            Clientes = CalculadoraGaia.ClientesQueVuelven(f.Llamadas),
        });
    }

    [HttpGet("espanolizacion")]
    public IActionResult Espanolizacion([FromQuery] PeticionGaia filtros, int nivel = 0)
    {
        var (datos, f) = Resolver(filtros);
        if (f is null) return Pagina("Espanolizacion", new PaginaEspanolizacionGaia { Datos = datos, Cargando = _gaia.Cargando, Error = _gaia.UltimoError });

        var palabras = datos!.Palabras;
        nivel = Math.Clamp(nivel, 0, palabras.Niveles.Count);
        var porAgente = CalculadoraGaia.EspanolizacionPor(f.Llamadas, palabras, nivel, l => l.IdAgente, g => g.First().Agente, g => g.First().Oleada);
        return Pagina("Espanolizacion", new PaginaEspanolizacionGaia
        {
            Datos = datos, Filtros = f, Cargando = _gaia.Cargando, Error = _gaia.UltimoError,
            Actualizacion = TempData[ClaveActualizacion] as string,
            Total = CalculadoraGaia.Calcular(f.Llamadas),
            Nivel = nivel,
            Niveles = palabras.Niveles,
            Resumen = CalculadoraGaia.Espanolizacion(f.Llamadas, palabras, nivel),
            Pares = CalculadoraGaia.ParesEspanolizacion(f.Llamadas, palabras, nivel),
            Palabras = CalculadoraGaia.PalabrasEspanolizacion(f.Llamadas, palabras, nivel),
            PorEtapa = CalculadoraGaia.EspanolizacionPor(f.Llamadas, palabras, nivel, l => l.TipoConexion, g => g.Key)
                .OrderBy(g => Array.IndexOf(LectorNominaGaia.ColumnasDias, g.Clave) is var i && i >= 0 ? i : 99).ToList(),
            PorDia = CalculadoraGaia.EspanolizacionPor(f.Llamadas, palabras, nivel, l => l.Fecha.ToString("yyyy-MM-dd"), g => g.First().Fecha.ToString("dd/MM"))
                .OrderBy(g => g.Clave).ToList(),
            PorSemana = CalculadoraGaia.EspanolizacionPorSemana(f.Llamadas, palabras, nivel),
            // Con base suficiente primero, de menos a más españolización; los de poca base, al final.
            Agentes = porAgente.OrderBy(a => a.Espanolizacion.Base < PaginaEspanolizacionGaia.MinBase)
                .ThenBy(a => a.Espanolizacion.Espana ?? 2).ThenByDescending(a => a.Espanolizacion.Base).ToList(),
        });
    }

    [HttpGet("llamadas")]
    public IActionResult Llamadas([FromQuery] PeticionGaia filtros, string? q, int pagina = 1)
    {
        var (datos, f) = Resolver(filtros);
        if (f is null) return Pagina("Llamadas", new PaginaLlamadasGaia { Datos = datos, Cargando = _gaia.Cargando, Error = _gaia.UltimoError });

        var filas = Buscar(f.Llamadas, q).OrderByDescending(l => l.FechaHora).ToList();
        var paginas = Math.Max(1, (filas.Count + PaginaLlamadasGaia.PorPagina - 1) / PaginaLlamadasGaia.PorPagina);
        pagina = Math.Clamp(pagina, 1, paginas);
        return Pagina("Llamadas", new PaginaLlamadasGaia
        {
            Datos = datos, Filtros = f, Cargando = _gaia.Cargando, Error = _gaia.UltimoError,
            Actualizacion = TempData[ClaveActualizacion] as string,
            Total = CalculadoraGaia.Calcular(f.Llamadas),
            Filas = filas.Skip((pagina - 1) * PaginaLlamadasGaia.PorPagina).Take(PaginaLlamadasGaia.PorPagina).ToList(),
            TotalFilas = filas.Count, Pagina = pagina, Busqueda = q?.Trim(),
        });
    }

    /// <summary>Todas las llamadas con los filtros, en CSV para Excel.</summary>
    [HttpGet("llamadas/csv")]
    public IActionResult Csv([FromQuery] PeticionGaia filtros, string? q)
    {
        var (_, f) = Resolver(filtros);
        if (f is null) return LocalRedirect(PaginaGaia.RutaBase);
        var filas = Buscar(f.Llamadas, q).OrderBy(l => l.FechaHora);
        var bytes = ExportacionCsv.Generar(filas, ColumnasCsv);
        Response.Headers.CacheControl = "private, no-store";
        return File(bytes, ExportacionCsv.TipoContenido, $"gaia_llamadas_{f.Desde:yyyyMMdd}_{f.Hasta:yyyyMMdd}.csv");
    }

    /// <summary>Vuelve a leer el Excel y a traer las llamadas de BigQuery. Como mucho una vez cada 30 s.</summary>
    [HttpPost("actualizar")]
    [ValidateAntiForgeryToken]
    public IActionResult Actualizar(string? volver)
    {
        TempData[ClaveActualizacion] = _gaia.PedirActualizacion() ? "en-curso" : "ya-en-curso";
        var destino = !string.IsNullOrEmpty(volver) && Url.IsLocalUrl(volver)
                      && volver.StartsWith(PaginaGaia.RutaBase, StringComparison.OrdinalIgnoreCase)
            ? volver : PaginaGaia.RutaBase;
        return LocalRedirect(destino);
    }

    // ------------------------------------------------------------------

    private (DatosGaia? Datos, FiltrosResueltosGaia? Filtros) Resolver(PeticionGaia p)
    {
        _gaia.Revisar();
        var datos = _gaia.Actual;
        return datos is null ? (null, null) : (datos, FiltrosGaia.Resolver(datos.Llamadas, p));
    }

    private IActionResult Pagina(string vista, PaginaGaia modelo)
    {
        ViewData["Parcial"] = Request.Headers["X-Parcial"] == "1";
        return View(vista, modelo);
    }

    /// <summary>Si un agente del Excel entra en los filtros de nómina (sector, oleada, formador, supervisor, agente).</summary>
    private static bool EntraEnNomina(AgenteGaia a, PeticionGaia p)
        => (p.Sector.Count == 0 || p.Sector.Contains(a.Sector))
           && (p.Oleada.Count == 0 || p.Oleada.Contains(a.Oleada))
           && (p.Formador.Count == 0 || p.Formador.Contains(a.Formador))
           && (p.Supervisor.Count == 0 || p.Supervisor.Contains(a.Supervisor))
           && (p.Agente.Count == 0 || p.Agente.Contains(a.Id));

    /// <summary>Ordena por un KPI con los vacíos siempre al final.</summary>
    private static IEnumerable<FilaAgenteGaia> Ordenar(IEnumerable<FilaAgenteGaia> filas, Func<IndicadoresGaia, double?> valor, bool descendente)
    {
        var conValor = filas.Where(r => valor(r.Indicadores) is not null);
        var ordenadas = descendente ? conValor.OrderByDescending(r => valor(r.Indicadores)) : conValor.OrderBy(r => valor(r.Indicadores));
        return ordenadas.ThenByDescending(r => r.Indicadores.Llamadas).Concat(filas.Where(r => valor(r.Indicadores) is null));
    }

    private static IEnumerable<LlamadaGaia> Buscar(IEnumerable<LlamadaGaia> ll, string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return ll;
        var t = q.Trim();
        return ll.Where(l => l.IdConversacion.Contains(t, StringComparison.OrdinalIgnoreCase)
                             || l.IdExterno.Contains(t, StringComparison.OrdinalIgnoreCase)
                             || l.IdCliente.Contains(t, StringComparison.OrdinalIgnoreCase)
                             || l.Agente.Contains(t, StringComparison.CurrentCultureIgnoreCase));
    }

    private static string Num(double? v) => v is { } d ? d.ToString("0.##", CultureInfo.GetCultureInfo("es-ES")) : "";
    private static string Si(bool? v) => v switch { true => "Sí", false => "No", _ => "" };

    private static readonly IReadOnlyList<ColumnaCsv<LlamadaGaia>> ColumnasCsv =
    [
        new("Fecha", l => l.FechaHora.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
        new("Id conversación", l => l.IdConversacion),
        new("Id externo", l => l.IdExterno),
        new("Marca", l => l.Marca),
        new("Agente", l => l.Agente),
        new("Id agente", l => l.IdAgente),
        new("Sector", l => l.Sector),
        new("Oleada", l => l.Oleada),
        new("Tipo de conexión", l => l.TipoConexion),
        new("Formador", l => l.Formador),
        new("Supervisor", l => l.Supervisor),
        new("Duración (s)", l => Num(l.DuracionSegundos)),
        new("Silencio (s)", l => Num(l.TiempoNoHablado)),
        new("Rellamada 72 h", l => l.Rellamada72h?.ToString()),
        new("Encuesta solución", l => l.EncuestaSolucion?.ToString()),
        new("Transferencia", l => l.Transferencia?.ToString()),
        new("Riesgo churn", l => l.RiesgoChurn),
        new("Motivo 1", l => l.Motivo1),
        new("Motivo 2", l => l.Motivo2),
        new("Motivo 3", l => l.Motivo3),
        new("Sentimiento inicial", l => l.SentimientoInicial),
        new("Sentimiento final", l => l.SentimientoFinal),
        new("Saludo", l => l.CalificacionSaludo),
        new("Lenguaje claro", l => l.CalificacionLenguajeClaro),
        new("Solucionó", l => l.CalificacionSolucion),
        new("Resumió", l => l.CalificacionResumen),
        new("Confirmó solución", l => l.CalificacionConfirmacion),
        new("Despedida", l => l.CalificacionCierre),
        new("Intento de venta", l => Si(l.IntentoVenta)),
        new("Venta", l => Si(l.TieneVenta)),
        new("Resumen del contacto", l => l.ResumenContacto),
    ];
}
