using CDM_Auditorias_Calidad.Infraestructura;
using CDM_Auditorias_Calidad.Models;

namespace CDM_Auditorias_Calidad.Servicios.Tablero;

/// <summary>
/// Calcula la pestaña «T0 y planes de acción»: las auditorías de ICEBERG con Tolerancia 0 frente a su
/// alerta T0 y su plan de acción (regla de negocio: cada auditoría con T0 debe tener un plan cargado).
/// Lleva los filtros de General (fecha, mes, sector, super, team, auditor y cargo) más plantilla y
/// situación. Cómo se une cada auditoría con su alerta y su plan: <c>Consultas/AuditoriasT0.sql</c> y
/// docs/contexto/t0-planes.md.
/// </summary>
public static class CalculadoraT0
{
    /// <summary>Filas de la tabla de detalle (el Excel lleva todas).</summary>
    public const int FilasDetalle = 200;

    /// <summary>Días seguidos sin ningún plan en <c>Legal.PlanAccion</c> a partir de los que se avisa de un hueco.</summary>
    public const int DiasHueco = 3;

    /// <summary>Días después de la auditoría en los que un hueco de PlanAccion afecta a sus planes (la alerta se gestiona después).</summary>
    private const int DiasGestion = 15;

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

    /// <summary>Lo filtrado: fechas elegidas y las filas que pasan todos los filtros (o todos menos uno).</summary>
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

    /// <param name="hoy">Para los huecos de PlanAccion (hasta ayer); null = hoy.</param>
    public static TableroModelo Calcular(InstantaneaAuditorias? datos, FiltrosTablero filtros, string? errorDatos = null, DateOnly? hoy = null)
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

        var total = filas.Count;
        var conAlerta = filas.Count(a => a.IdAlerta is not null);
        var conPlan = filas.Count(a => a.ConPlan);
        var verificados = filas.Count(a => a.Situacion == SituacionT0.PlanVerificado);
        int Contar(SituacionT0 s) => filas.Count(a => a.Situacion == s);

        // --- Tarjetas -----------------------------------------------------------------------

        var tarjetas = new List<TarjetaKpi>
        {
            new("Auditorías con T0", "alerta", Formato.Entero(total), total, false, null,
                TextoIceberg(datos, filtros, cx, total), false,
                "Auditorías de calidad de ICEBERG con la columna «Tolerancia 0» rellena, en las fechas elegidas"),
            new("Con alerta T0", "cruce", Formato.Entero(conAlerta), conAlerta, false, null,
                $"{Formato.Porcentaje(Fraccion(conAlerta, total))} de las T0", false,
                "Auditorías con su alerta en Legal.Alertas_T0 (por el ID de la llamada o, si no, por agente, auditor y fecha)",
                Fraccion(conAlerta, total) ?? 0),
            new("Con plan de acción", "portapapeles", Formato.Entero(conPlan), conPlan, false, null,
                $"{Formato.Porcentaje(Fraccion(conPlan, total))} de las T0 · la regla pide todas", true,
                "Auditorías cuya alerta T0 se gestionó con «Plan de acción»",
                Fraccion(conPlan, total) ?? 0),
            new("Sin plan de acción", "no-resuelta", Formato.Entero(total - conPlan), total - conPlan, false, null,
                $"{Formato.Entero(Contar(SituacionT0.SinAlerta))} sin alerta · {Formato.Entero(Contar(SituacionT0.AlertaPendiente))} pendientes · {Formato.Entero(Contar(SituacionT0.OtraAccion))} otra acción",
                false, "Auditorías con T0 que incumplen la regla: sin alerta, con la alerta pendiente o gestionada con otra acción"),
            new("Plan verificado", "escudo", Formato.Entero(verificados), verificados, false, null,
                $"{Formato.Porcentaje(Fraccion(verificados, conPlan))} de los planes están en PlanAccion", false,
                "Planes de las alertas cuyo número existe en Legal.PlanAccion",
                Fraccion(verificados, conPlan) ?? 0),
        };

        // --- Situación, sector, team y motivos ------------------------------------------------

        var situaciones = Enum.GetValues<SituacionT0>()
            .Select(s => new FilaSituacionT0(s, Contar(s), DetalleSituacion(s, filas)))
            .ToList();

        var sectoresMarcados = cx.Elegidos["sector"];
        var porSector = filas
            .GroupBy(a => CalculadoraTablero.Clave(a.Sector))
            .Select(g => Cumplimiento(g.Key, null, g, sectoresMarcados.Contains(g.Key)))
            .OrderByDescending(f => f.Total)
            .ThenBy(f => f.Nombre, StringComparer.Create(Formato.Es, true))
            .ToList();

        // Por team: quien gestiona la alerta y carga el plan suele ser el team leader.
        var teamsMarcados = cx.Elegidos["team"];
        var porTeam = filas
            .GroupBy(a => CalculadoraTablero.Clave(a.Team))
            .Select(g => Cumplimiento(g.Key,
                g.GroupBy(a => a.Super).OrderByDescending(s => s.Count()).Select(s => s.Key).FirstOrDefault(),
                g, teamsMarcados.Contains(g.Key)))
            .OrderByDescending(f => f.SinPlan)
            .ThenByDescending(f => f.Total)
            .ThenBy(f => f.Nombre, StringComparer.Create(Formato.Es, true))
            .ToList();

        var motivos = filas
            .Where(a => a.IdPlan is not null)
            .GroupBy(a => string.IsNullOrWhiteSpace(a.MotivoPlan) ? "(Sin motivo)" : a.MotivoPlan.Trim())
            .Select(g => (Motivo: g.Key, Cantidad: g.Count()))
            .OrderByDescending(m => m.Cantidad)
            .ThenBy(m => m.Motivo, StringComparer.Create(Formato.Es, true))
            .ToList();

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
                ConAlerta = conAlerta,
                ConPlan = conPlan,
                Verificados = verificados,
                Situaciones = situaciones,
                PorSector = porSector,
                PorTeam = porTeam,
                Motivos = motivos,
                AmbosPlanes = filas.Count(a => a.ConPlan && a.IdPlanCalidad is not null),
                SoloAlerta = filas.Count(a => a.ConPlan && a.IdPlanCalidad is null),
                SoloCalidad = filas.Count(a => !a.ConPlan && a.IdPlanCalidad is not null),
                NingunPlan = filas.Count(a => !a.ConPlan && a.IdPlanCalidad is null),
                Detalle = Ordenar(filas).Take(FilasDetalle).ToList(),
                DetalleTotal = total,
                Evolucion = CalculadoraTablero.Serie(filas, a => a.Fecha, g => FraccionConPlan(g), vista),
                Huecos = Huecos(datos.PlanesPorDia, hoy ?? DateOnly.FromDateTime(DateTime.Today))
                    .Where(h => h.Hasta >= cx.Desde && h.Desde <= cx.Hasta.AddDays(DiasGestion))
                    .ToList(),
                Error = datos.T0 is null ? datos.ErrorT0 ?? "No se pudieron leer las alertas T0." : null,
            },
        };
    }

    /// <summary>Todas las auditorías T0 con los filtros, en el orden de la tabla de detalle (para el Excel).</summary>
    public static IReadOnlyList<AuditoriaT0> Detalle(InstantaneaAuditorias datos, FiltrosTablero filtros)
    {
        if (datos.T0 is not { } todas || datos.PrimeraFecha is not { } calMin || datos.UltimaFecha is not { } calMax) return [];
        var cx = CrearContexto(filtros, calMin, calMax);
        return Ordenar(todas.Where(a => cx.EnFechas(a.Fecha) && cx.Pasa(a, null))).ToList();
    }

    /// <summary>
    /// Tramos de al menos <see cref="DiasHueco"/> días seguidos sin ningún plan creado, desde el primer día con
    /// planes hasta ayer. El 08-10-2026, PlanAccion no tenía planes del 14-07 al 30-09-2026.
    /// </summary>
    public static IReadOnlyList<HuecoPlanes> Huecos(IReadOnlyDictionary<DateOnly, int>? planesPorDia, DateOnly hoy)
    {
        if (planesPorDia is null || planesPorDia.Count == 0) return [];
        var huecos = new List<HuecoPlanes>();
        DateOnly? inicio = null;
        var fin = hoy.AddDays(-1);
        for (var d = planesPorDia.Keys.Min(); d <= fin; d = d.AddDays(1))
        {
            var hay = planesPorDia.TryGetValue(d, out var n) && n > 0;
            if (!hay) inicio ??= d;
            if ((hay || d == fin) && inicio is { } i)
            {
                var ultimo = hay ? d.AddDays(-1) : d;
                if (ultimo.DayNumber - i.DayNumber + 1 >= DiasHueco) huecos.Add(new HuecoPlanes(i, ultimo));
                inicio = null;
            }
        }
        return huecos;
    }

    // ---------------------------------------------------------------------------------------

    /// <summary>Primero las que no tienen plan (sin alerta, pendientes, otra acción), de la más reciente a la más antigua.</summary>
    private static IEnumerable<AuditoriaT0> Ordenar(IEnumerable<AuditoriaT0> filas) => filas
        .OrderByDescending(a => (int)a.Situacion)
        .ThenByDescending(a => a.Fecha)
        .ThenBy(a => a.Agente, StringComparer.Ordinal);

    private static FilaCumplimientoT0 Cumplimiento(string nombre, string? sub, IEnumerable<AuditoriaT0> g, bool marcado)
    {
        int total = 0, alerta = 0, plan = 0, verificado = 0;
        foreach (var a in g)
        {
            total++;
            if (a.IdAlerta is not null) alerta++;
            if (a.ConPlan) plan++;
            if (a.Situacion == SituacionT0.PlanVerificado) verificado++;
        }
        return new FilaCumplimientoT0(nombre, sub, total, alerta, plan, verificado, marcado);
    }

    private static string? DetalleSituacion(SituacionT0 s, List<AuditoriaT0> filas)
    {
        var de = filas.Where(a => a.Situacion == s).ToList();
        if (de.Count == 0) return null;
        return s switch
        {
            SituacionT0.OtraAccion => string.Join(" · ", de
                .GroupBy(a => string.IsNullOrWhiteSpace(a.Accion) ? "Sin acción" : a.Accion.Trim())
                .OrderByDescending(g => g.Count())
                .Select(g => $"{g.Key} {Formato.Entero(g.Count())}")),
            SituacionT0.PlanSinVerificar => Unir(
                (de.Count(a => a.IdAccion is null or <= 0), "sin número de plan"),
                (de.Count(a => a.IdAccion is > 0), "con un número que no está en PlanAccion")),
            SituacionT0.SinAlerta => Unir(
                (de.Count(a => a.IdLlamada is null), "sin ID de llamada en la plantilla")),
            _ => null,
        };
    }

    private static string? Unir(params (int Cantidad, string Texto)[] partes)
    {
        var con = partes.Where(p => p.Cantidad > 0).Select(p => $"{Formato.Entero(p.Cantidad)} {p.Texto}").ToList();
        return con.Count > 0 ? string.Join(" · ", con) : null;
    }

    /// <summary>Qué parte de las auditorías ICEBERG del filtro tienen T0 (sin los filtros de plantilla y situación, que no existen en ellas).</summary>
    private static string TextoIceberg(InstantaneaAuditorias datos, FiltrosTablero filtros, Contexto cx, int total)
    {
        if (filtros.Plantilla.Count > 0 || filtros.Situacion.Count > 0) return "con los filtros elegidos";
        var columnas = new (string Campo, Func<Auditoria, string?> Valor)[]
        {
            ("sector", a => a.Sector), ("super", a => a.Super), ("team", a => a.Team),
            ("auditor", a => a.NombreAuditor), ("cargo", a => a.CargoAuditor),
        };
        var iceberg = datos.Filas.Count(a =>
            string.Equals(a.Base, "ICEBERG", StringComparison.OrdinalIgnoreCase) && cx.EnFechas(a.Fecha) &&
            columnas.All(c => cx.Elegidos[c.Campo].Count == 0 || cx.Elegidos[c.Campo].Contains(CalculadoraTablero.Clave(c.Valor(a)))));
        return iceberg > 0
            ? $"{Formato.Porcentaje(Math.Min(1, (double)total / iceberg))} de {Formato.Entero(iceberg)} auditorías ICEBERG"
            : "auditorías ICEBERG";
    }

    private static double? Fraccion(int parte, int total) => total > 0 ? (double)parte / total : null;

    private static double? FraccionConPlan(IEnumerable<AuditoriaT0> g)
    {
        int n = 0, plan = 0;
        foreach (var a in g)
        {
            n++;
            if (a.ConPlan) plan++;
        }
        return n > 0 ? (double)plan / n : null;
    }
}
