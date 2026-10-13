using CDM_Auditorias_Calidad.Infraestructura;
using CDM_Auditorias_Calidad.Models;

namespace CDM_Auditorias_Calidad.Servicios.Tablero;

/// <summary>
/// Calcula la pestaña «T0 y planes de acción»: por técnico de calidad / auditor, cuántas auditorías hizo, cuántas
/// tienen T0 y cuántas de esas están en <c>[Reporting].[pdc].[Reportar_T0]</c> (y allí, con plan de acción). Del
/// lado de las T0 solo se usa Reportar_T0 (pedido del usuario el 13-10-2026). Lleva los filtros de General (fecha,
/// mes, sector, super, team, auditor y cargo) más situación y plantilla. Cómo se une cada auditoría con su reporte:
/// <c>Consultas/AuditoriasT0.sql</c> y docs/contexto/t0-planes.md.
/// </summary>
public static class CalculadoraT0
{
    /// <summary>Filas de la tabla de detalle (el Excel lleva todas).</summary>
    public const int FilasDetalle = 200;

    private sealed record Dimension(string Campo, string Titulo, Func<AuditoriaT0, string?> Valor);

    private static readonly Dimension[] Dimensiones =
    [
        new("sector", "Sector", a => a.Sector),
        new("super", "Super", a => a.Super),
        new("team", "Team", a => a.Team),
        new("auditor", "Auditor", a => a.NombreAuditor),
        new("situacion", "Situación", a => TextosT0.Texto(a.Situacion)),
        new("cargo", "Cargo auditor", a => a.CargoAuditor),
        new("plantilla", "Plantilla", a => a.Plantilla),
    ];

    /// <summary>Los filtros que existen también en las auditorías de General y en Reportar_T0 (no situación ni plantilla).</summary>
    private static readonly string[] CamposComunes = ["sector", "super", "team", "auditor", "cargo"];

    /// <summary>Lo filtrado: fechas elegidas y los valores marcados de cada filtro.</summary>
    private sealed class Contexto
    {
        public required DateOnly Desde { get; init; }
        public required DateOnly Hasta { get; init; }
        public required HashSet<string> Meses { get; init; }
        public required Dictionary<string, HashSet<string>> Elegidos { get; init; }

        public bool EnFechas(DateOnly f) => f >= Desde && f <= Hasta && (Meses.Count == 0 || Meses.Contains(CalculadoraTablero.ClaveMes(f)));

        public bool Pasa(AuditoriaT0 a, string? salvo)
        {
            foreach (var d in Dimensiones)
            {
                if (d.Campo == salvo) continue;
                var set = Elegidos[d.Campo];
                if (set.Count > 0 && !set.Contains(CalculadoraTablero.Clave(d.Valor(a)))) return false;
            }
            return true;
        }

        /// <summary>Para las auditorías de General y las filas de Reportar_T0: solo los filtros que tienen.</summary>
        public bool PasaComunes(Func<string, string?> valor)
            => CamposComunes.All(c => Elegidos[c].Count == 0 || Elegidos[c].Contains(CalculadoraTablero.Clave(valor(c))));
    }

    private static Contexto CrearContexto(FiltrosTablero filtros, DateOnly calMin, DateOnly calMax)
    {
        var desde = CalculadoraTablero.Acotar(FiltrosTablero.LeerFecha(filtros.Desde) ?? calMin, calMin, calMax);
        var hasta = CalculadoraTablero.Acotar(FiltrosTablero.LeerFecha(filtros.Hasta) ?? calMax, calMin, calMax);
        if (desde > hasta) (desde, hasta) = (hasta, desde);
        return new Contexto
        {
            Desde = desde,
            Hasta = hasta,
            Meses = new HashSet<string>(filtros.Mes),
            Elegidos = Dimensiones.ToDictionary(d => d.Campo, d => new HashSet<string>(filtros.Lista(d.Campo), StringComparer.Ordinal)),
        };
    }

    private static string? Comun(Auditoria a, string campo) => campo switch
    {
        "sector" => a.Sector,
        "super" => a.Super,
        "team" => a.Team,
        "auditor" => a.NombreAuditor,
        _ => a.CargoAuditor,
    };

    private static string? Comun(AlertaT0 r, string campo) => campo switch
    {
        "sector" => r.Sector,
        "super" => r.Super,
        "team" => r.Team,
        "auditor" => r.NombreAuditor,
        _ => r.CargoAuditor,
    };

    public static TableroModelo Calcular(InstantaneaAuditorias? datos, FiltrosTablero filtros, string? errorDatos = null)
    {
        var pagina = PaginaTablero.T0;
        var vista = FiltrosTablero.LeerVista(filtros.Vista) ?? pagina.VistaInicial;

        if (datos is null || datos.PrimeraFecha is not { } calMin || datos.UltimaFecha is not { } calMax)
        {
            return new TableroModelo
            {
                Pagina = pagina,
                Filtros = filtros,
                Vista = vista,
                DatosCargadosEn = datos?.CargadoEn,
                FilasCargadas = datos?.Filas.Count ?? 0,
                ErrorDatos = errorDatos,
                T0 = new InformeT0 { Error = datos?.ErrorT0 },
            };
        }

        var cx = CrearContexto(filtros, calMin, calMax);
        var todas = datos.T0 ?? [];
        var conColumnas = todas.Where(a => cx.Pasa(a, null)).ToList();
        var filas = conColumnas.Where(a => cx.EnFechas(a.Fecha)).ToList();

        // Las auditorías de General y las filas de Reportar_T0 con los filtros que tienen.
        var auditorias = datos.Filas.Where(a => cx.EnFechas(a.Fecha) && cx.PasaComunes(c => Comun(a, c))).ToList();
        var reportes = (datos.ReportesT0 ?? []).Where(r => cx.EnFechas(r.Fecha) && cx.PasaComunes(c => Comun(r, c))).ToList();
        var idsDeAuditorias = todas.Where(a => a.IdReporte is not null).Select(a => a.IdReporte!.Value).ToHashSet();

        var total = filas.Count;
        var reportadas = filas.Count(a => a.Reportada);
        var conPlan = filas.Count(a => a.Situacion == SituacionT0.ReportadaConPlan);
        var diasGestion = Mediana(filas.Where(a => a.Reportada).Select(a => a.Reporte!.DiasGestion));
        var sinFiltrosPropios = filtros.Plantilla.Count == 0 && filtros.Situacion.Count == 0;

        // --- Por técnico de calidad / auditor -------------------------------------------------

        var auditoresMarcados = cx.Elegidos["auditor"];
        var auditoriasPor = auditorias.GroupBy(a => CalculadoraTablero.Clave(a.NombreAuditor)).ToDictionary(g => g.Key, g => g.ToList());
        var t0Por = filas.GroupBy(a => CalculadoraTablero.Clave(a.NombreAuditor)).ToDictionary(g => g.Key, g => g.ToList());
        var reportesPor = reportes.GroupBy(r => CalculadoraTablero.Clave(r.NombreAuditor)).ToDictionary(g => g.Key, g => g.Count());
        var porAuditor = auditoriasPor.Keys.Union(t0Por.Keys).Union(reportesPor.Keys)
            .Select(nombre =>
            {
                var suyas = auditoriasPor.GetValueOrDefault(nombre) ?? [];
                var t0 = t0Por.GetValueOrDefault(nombre) ?? [];
                var cargo = suyas.Select(a => a.CargoAuditor).Concat(t0.Select(a => a.CargoAuditor))
                    .Where(c => !string.IsNullOrWhiteSpace(c)).GroupBy(c => c).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                return new FilaAuditorT0(nombre, cargo, suyas.Count, t0.Count, t0.Count(a => a.Reportada),
                    t0.Count(a => a.Situacion == SituacionT0.ReportadaConPlan), reportesPor.GetValueOrDefault(nombre), auditoresMarcados.Contains(nombre));
            })
            .OrderByDescending(f => f.ConT0)
            .ThenByDescending(f => f.Auditorias)
            .ThenBy(f => f.Nombre, StringComparer.Create(Formato.Es, true))
            .ToList();

        // --- Tarjetas -----------------------------------------------------------------------

        var iceberg = auditorias.Count(a => string.Equals(a.Base, "ICEBERG", StringComparison.OrdinalIgnoreCase));
        var auditoresSinReportar = porAuditor.Count(f => f.SinReportar > 0);
        var tarjetas = new List<TarjetaKpi>
        {
            new("Auditorías", "portapapeles", Formato.Entero(auditorias.Count), auditorias.Count, false, null,
                $"{Formato.Entero(auditoriasPor.Count)} auditores" + (sinFiltrosPropios ? "" : " · sin los filtros de situación y plantilla"), false,
                "Todas las auditorías de las fechas elegidas (WEB e ICEBERG), como en General"),
            new("Con T0", "alerta", Formato.Entero(total), total, false, null,
                iceberg > 0 && sinFiltrosPropios ? $"{Formato.Porcentaje(Math.Min(1, (double)total / iceberg))} de las {Formato.Entero(iceberg)} de ICEBERG" : "auditorías de ICEBERG", false,
                "Auditorías de ICEBERG con la columna «Tolerancia 0» rellena (los formularios WEB no tienen ese campo)"),
            new("En Reportar_T0", "cruce", Formato.Entero(reportadas), reportadas, false, null,
                $"{Formato.Porcentaje(Fraccion(reportadas, total))} de las T0 · deberían estar todas", true,
                "Auditorías con T0 que están en [Reporting].[pdc].[Reportar_T0] (por el ID de la llamada o por agente, auditor y fecha)",
                Fraccion(reportadas, total) ?? 0),
            new("Sin reportar", "no-resuelta", Formato.Entero(total - reportadas), total - reportadas, false, null,
                $"{Formato.Entero(auditoresSinReportar)} {(auditoresSinReportar == 1 ? "auditor" : "auditores")} con alguna sin reportar", false,
                "Auditorías con T0 que no están en Reportar_T0"),
            new("Con plan de acción", "escudo", Formato.Entero(conPlan), conPlan, false, null,
                $"{Formato.Porcentaje(Fraccion(conPlan, reportadas))} de las reportadas · gestión en {TextosT0.Dias(diasGestion)} (mediana)", false,
                "Reportadas que en Reportar_T0 se gestionaron con «Plan de acción»; la mediana es el tiempo entre el reporte y la gestión",
                Fraccion(conPlan, reportadas) ?? 0),
        };

        // --- Situación, sector y plantilla ----------------------------------------------------

        var situaciones = Enum.GetValues<SituacionT0>()
            .Select(s => new FilaSituacionT0(s, filas.Count(a => a.Situacion == s), DetalleSituacion(s, filas)))
            .ToList();

        var porSector = Cumplimiento(filas, a => a.Sector, cx.Elegidos["sector"]).OrderByDescending(f => f.Total)
            .ThenBy(f => f.Nombre, StringComparer.Create(Formato.Es, true)).ToList();
        var porPlantilla = Cumplimiento(filas, a => a.Plantilla, cx.Elegidos["plantilla"]).OrderByDescending(f => f.SinReportar)
            .ThenByDescending(f => f.Total).ThenBy(f => f.Nombre, StringComparer.Create(Formato.Es, true)).ToList();

        // --- Opciones de los filtros (se filtran entre sí, como en General) --------------------

        var grupos = new List<GrupoFiltro> { CalculadoraTablero.GrupoMes(conColumnas.Select(a => a.Fecha), cx.Desde, cx.Hasta, cx.Meses) };
        foreach (var d in Dimensiones)
        {
            var cuenta = todas
                .Where(a => cx.EnFechas(a.Fecha) && cx.Pasa(a, d.Campo))
                .GroupBy(a => CalculadoraTablero.Clave(d.Valor(a)))
                .ToDictionary(g => g.Key, g => g.Count());
            grupos.Add(CalculadoraTablero.Grupo(d.Campo, d.Titulo, cuenta, cx.Elegidos[d.Campo]));
        }

        var normalizados = filtros.Copia();
        normalizados.Vista = FiltrosTablero.EscribirVista(vista);

        return new TableroModelo
        {
            Pagina = pagina,
            Filtros = normalizados,
            Vista = vista,
            CalendarioDesde = calMin,
            CalendarioHasta = calMax,
            Desde = cx.Desde,
            Hasta = cx.Hasta,
            Tarjetas = tarjetas,
            Grupos = grupos,
            TotalFiltrado = total,
            DatosCargadosEn = datos.CargadoEn,
            FilasCargadas = datos.Filas.Count,
            ErrorDatos = errorDatos,
            T0 = new InformeT0
            {
                Total = total,
                Reportadas = reportadas,
                ConPlan = conPlan,
                ReportesSinAuditoria = reportes.Count(r => !idsDeAuditorias.Contains(r.Id)),
                DiasGestion = diasGestion,
                Situaciones = situaciones,
                PorAuditor = porAuditor,
                PorSector = porSector,
                PorPlantilla = porPlantilla,
                Detalle = Ordenar(filas).Take(FilasDetalle).ToList(),
                DetalleTotal = total,
                Evolucion = CalculadoraTablero.Serie(filas, a => a.Fecha, FraccionReportadas, vista),
                Error = datos.T0 is null ? datos.ErrorT0 ?? "No se pudieron leer las T0." : null,
            },
        };
    }

    /// <summary>Todas las auditorías con T0 con los filtros, en el orden de la tabla de detalle (para el Excel).</summary>
    public static IReadOnlyList<AuditoriaT0> Detalle(InstantaneaAuditorias datos, FiltrosTablero filtros)
    {
        if (datos.T0 is not { } todas || datos.PrimeraFecha is not { } calMin || datos.UltimaFecha is not { } calMax) return [];
        var cx = CrearContexto(filtros, calMin, calMax);
        return Ordenar(todas.Where(a => cx.EnFechas(a.Fecha) && cx.Pasa(a, null))).ToList();
    }

    // ---------------------------------------------------------------------------------------

    /// <summary>Primero las sin reportar, luego las pendientes y las sin plan; de la más reciente a la más antigua.</summary>
    private static IEnumerable<AuditoriaT0> Ordenar(IEnumerable<AuditoriaT0> filas) => filas
        .OrderByDescending(a => (int)a.Situacion)
        .ThenByDescending(a => a.Fecha)
        .ThenBy(a => a.NombreAuditor, StringComparer.Ordinal)
        .ThenBy(a => a.Agente, StringComparer.Ordinal);

    private static IEnumerable<FilaCumplimientoT0> Cumplimiento(List<AuditoriaT0> filas, Func<AuditoriaT0, string?> clave, HashSet<string> marcados)
        => filas.GroupBy(a => CalculadoraTablero.Clave(clave(a)))
            .Select(g => new FilaCumplimientoT0(g.Key, g.Count(), g.Count(a => a.Reportada),
                g.Count(a => a.Situacion == SituacionT0.ReportadaConPlan), marcados.Contains(g.Key)));

    private static string? DetalleSituacion(SituacionT0 s, List<AuditoriaT0> filas)
    {
        var de = filas.Where(a => a.Situacion == s).ToList();
        if (de.Count == 0) return null;
        return s switch
        {
            SituacionT0.ReportadaSinPlan => string.Join(" · ", de
                .GroupBy(a => TextosT0.Texto(a.Reporte!.Gestion))
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key} {Formato.Entero(g.Count())}")),
            SituacionT0.ReportadaConPlan when de.Count(a => a.Reporte!.Gestion == GestionT0.PlanSinNumero) is > 0 and var sinNumero
                => $"{Formato.Entero(sinNumero)} sin número de plan",
            SituacionT0.SinReportar when de.Count(a => a.IdLlamada is null) is > 0 and var sinLlamada
                => $"{Formato.Entero(sinLlamada)} sin ID de llamada en la plantilla",
            _ => null,
        };
    }

    private static double? Fraccion(int parte, int total) => total > 0 ? (double)parte / total : null;

    private static double? FraccionReportadas(IEnumerable<AuditoriaT0> g)
    {
        int n = 0, si = 0;
        foreach (var a in g)
        {
            n++;
            if (a.Reportada) si++;
        }
        return n > 0 ? (double)si / n : null;
    }

    private static double? Mediana(IEnumerable<double?> valores)
    {
        var v = valores.Where(x => x is not null).Select(x => x!.Value).Order().ToList();
        if (v.Count == 0) return null;
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }
}
