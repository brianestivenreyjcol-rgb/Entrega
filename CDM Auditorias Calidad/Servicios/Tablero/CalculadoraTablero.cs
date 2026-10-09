using CDM_Auditorias_Calidad.Infraestructura;
using CDM_Auditorias_Calidad.Models;
using CDM_Auditorias_Calidad.Servicios.Configuracion;

namespace CDM_Auditorias_Calidad.Servicios.Tablero;

/// <summary>
/// Calcula una página del tablero a partir de las auditorías en memoria: las medidas DAX del
/// Power BI (tabla «Medidas») pasadas a C#. Ver docs/contexto/power-bi.md, sección 2.3.
/// </summary>
public static class CalculadoraTablero
{
    /// <summary>Texto de los valores vacíos en filtros, gráficos y tablas, como en Power BI.</summary>
    public const string EnBlanco = "(En blanco)";

    private sealed record Dimension(string Campo, string Titulo, Func<Auditoria, string?> Valor);

    /// <summary>Los filtros de columna de la barra izquierda (el de Mes va aparte: es del calendario).</summary>
    private static readonly Dimension[] Dimensiones =
    [
        new("sector", "Sector", a => a.Sector),
        new("super", "Super", a => a.Super),
        new("team", "Team", a => a.Team),
        new("auditor", "Auditor", a => a.NombreAuditor),
        new("cargo", "Cargo auditor", a => a.CargoAuditor),
        new("base", "Base", a => a.Base),
    ];

    public static string Clave(string? valor) => string.IsNullOrWhiteSpace(valor) ? EnBlanco : valor;

    public static string ClaveMes(DateOnly fecha) => $"{fecha.Year:D4}-{fecha.Month:D2}";

    /// <param name="cargosAgente">
    /// Cargos de la nómina que cuentan como agente en «Total agentes»
    /// (<c>Auditorias:CargosAgente</c>); null = los de <see cref="OpcionesAuditorias"/>.
    /// </param>
    public static TableroModelo Calcular(
        InstantaneaAuditorias? datos,
        PaginaTablero pagina,
        FiltrosTablero filtros,
        double metaCalidad,
        string? errorDatos = null,
        IEnumerable<string>? cargosAgente = null)
    {
        var vista = FiltrosTablero.LeerVista(filtros.Vista) ?? pagina.VistaInicial;

        if (datos is null || datos.PrimeraFecha is not { } calMin || datos.UltimaFecha is not { } calMax)
        {
            return new TableroModelo
            {
                Pagina = pagina,
                Filtros = filtros,
                Vista = vista,
                MetaCalidad = metaCalidad,
                DatosCargadosEn = datos?.CargadoEn,
                FilasCargadas = datos?.Filas.Count ?? 0,
                ErrorDatos = errorDatos,
            };
        }

        // --- Contexto de filtro -------------------------------------------------------------

        // Filtro de página (Formación & Calidad: solo ciertos cargos de auditor).
        IReadOnlyList<Auditoria> filasPagina = pagina.Cargos is { } cargos
            ? datos.Filas.Where(a => a.CargoAuditor is { } c && cargos.Contains(c)).ToList()
            : datos.Filas;

        // Filtro de fecha (segmentador «Entre»): sin elegir, todo el calendario.
        var desde = Acotar(FiltrosTablero.LeerFecha(filtros.Desde) ?? calMin, calMin, calMax);
        var hasta = Acotar(FiltrosTablero.LeerFecha(filtros.Hasta) ?? calMax, calMin, calMax);
        if (desde > hasta) (desde, hasta) = (hasta, desde);
        var meses = new HashSet<string>(filtros.Mes);

        bool EnRango(DateOnly f) => f >= desde && f <= hasta;
        bool EnFechas(DateOnly f) => EnRango(f) && (meses.Count == 0 || meses.Contains(ClaveMes(f)));

        // Las fechas del calendario que quedan con el filtro de fecha y el de mes.
        var fechas = new List<DateOnly>();
        for (var d = desde; d <= hasta; d = d.AddDays(1))
            if (EnFechas(d)) fechas.Add(d);

        var elegidos = Dimensiones.ToDictionary(
            d => d.Campo,
            d => new HashSet<string>(filtros.Lista(d.Campo), StringComparer.Ordinal));

        bool Pasa(Auditoria a, string? salvo)
        {
            foreach (var d in Dimensiones)
            {
                if (d.Campo == salvo) continue;
                var set = elegidos[d.Campo];
                if (set.Count > 0 && !set.Contains(Clave(d.Valor(a)))) return false;
            }
            return true;
        }

        // Filas con todos los filtros de columna, sin mirar la fecha: las medidas de
        // semana, mes y período anterior ponen sus propias fechas.
        var conColumnas = filasPagina.Where(a => Pasa(a, null)).ToList();
        var contexto = conColumnas.Where(a => EnFechas(a.Fecha)).ToList();

        // --- Tarjetas KPI -------------------------------------------------------------------

        var tarjetas = Tarjetas(conColumnas, contexto, fechas, calMin, calMax);
        tarjetas.Add(TarjetaAgentes(datos, contexto, fechas, elegidos, cargosAgente ?? new OpcionesAuditorias().CargosAgente));

        // --- Gráficos -----------------------------------------------------------------------

        var evolucion = Serie(contexto, a => a.Fecha, Media, vista);

        var sectoresMarcados = elegidos["sector"];
        var sectores = contexto
            .GroupBy(a => Clave(a.Sector))
            .Select(g => new BarraSector(g.Key, g.Count(), Media(g), sectoresMarcados.Contains(g.Key)))
            .OrderByDescending(s => s.Cantidad)
            .ThenBy(s => s.Sector, StringComparer.Create(Formato.Es, true))
            .ToList();

        // Top 10 Auditores: filtro TopN 10 sobre Nombre_Auditor por Total Auditorías, y la
        // tabla agrupa por auditor y cargo.
        var auditoresMarcados = elegidos["auditor"];
        var top = contexto
            .GroupBy(a => Clave(a.NombreAuditor))
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Create(Formato.Es, true))
            .Take(10)
            .SelectMany(g => g.GroupBy(a => Clave(a.CargoAuditor))
                .Select(c => new FilaAuditor(g.Key, c.Key, c.Count(), Media(c), auditoresMarcados.Contains(g.Key))))
            .OrderByDescending(f => f.Cantidad)
            .ToList();

        // --- Opciones de los filtros (se filtran entre sí, como los segmentadores) -----------

        var grupos = new List<GrupoFiltro> { GrupoMes(conColumnas.Select(a => a.Fecha), desde, hasta, meses) };
        foreach (var d in Dimensiones)
        {
            var cuenta = filasPagina
                .Where(a => EnFechas(a.Fecha) && Pasa(a, d.Campo))
                .GroupBy(a => Clave(d.Valor(a)))
                .ToDictionary(g => g.Key, g => g.Count());
            grupos.Add(Grupo(d.Campo, d.Titulo, cuenta, elegidos[d.Campo]));
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
            Desde = desde,
            Hasta = hasta,
            Tarjetas = tarjetas,
            Evolucion = evolucion,
            Sectores = sectores,
            TopAuditores = top,
            Grupos = grupos,
            TotalFiltrado = contexto.Count,
            MetaCalidad = metaCalidad,
            DatosCargadosEn = datos.CargadoEn,
            FilasCargadas = datos.Filas.Count,
            ErrorDatos = errorDatos,
        };
    }

    /// <summary>
    /// Las filas del detalle («Descargable») con los filtros de la página, de la más reciente a la más antigua.
    /// </summary>
    public static IReadOnlyList<Auditoria> Detalle(InstantaneaAuditorias datos, PaginaTablero pagina, FiltrosTablero filtros)
    {
        if (datos.PrimeraFecha is not { } calMin || datos.UltimaFecha is not { } calMax) return [];
        var desde = Acotar(FiltrosTablero.LeerFecha(filtros.Desde) ?? calMin, calMin, calMax);
        var hasta = Acotar(FiltrosTablero.LeerFecha(filtros.Hasta) ?? calMax, calMin, calMax);
        if (desde > hasta) (desde, hasta) = (hasta, desde);

        var meses = new HashSet<string>(filtros.Mes);
        var elegidos = Dimensiones.Select(d => (d, set: new HashSet<string>(filtros.Lista(d.Campo), StringComparer.Ordinal))).ToList();

        return datos.Filas
            .Where(a => pagina.Cargos is not { } cargos || (a.CargoAuditor is { } c && cargos.Contains(c)))
            .Where(a => a.Fecha >= desde && a.Fecha <= hasta && (meses.Count == 0 || meses.Contains(ClaveMes(a.Fecha))))
            .Where(a => elegidos.All(e => e.set.Count == 0 || e.set.Contains(Clave(e.d.Valor(a)))))
            .OrderByDescending(a => a.Fecha)
            .ThenBy(a => a.Sector, StringComparer.Ordinal)
            .ThenBy(a => a.Agente, StringComparer.Ordinal)
            .ToList();
    }

    // ---------------------------------------------------------------------------------------

    private static List<TarjetaKpi> Tarjetas(List<Auditoria> conColumnas, List<Auditoria> contexto, List<DateOnly> fechas, DateOnly calMin, DateOnly calMax)
    {
        int Contar(DateOnly desde, DateOnly hasta) => conColumnas.Count(a => a.Fecha >= desde && a.Fecha <= hasta);
        List<Auditoria> EnFechas(ISet<DateOnly> set) => conColumnas.Where(a => set.Contains(a.Fecha)).ToList();
        string Rango(DateOnly desde, DateOnly hasta) => desde == hasta ? Formato.Fecha(desde) : $"{Formato.Fecha(desde)} – {Formato.Fecha(hasta)}";
        string RangoDe(ICollection<DateOnly> set) => set.Count == 0 ? "sin fechas" : Rango(set.Min(), set.Max());

        // MAX(Calendario[Fecha]) del contexto: la última fecha elegida.
        DateOnly? maximo = fechas.Count > 0 ? fechas[^1] : null;

        // Período anterior de «Total auditorías» y de la nota: el mismo tiempo, justo antes
        // (Periodos.PeriodoAnterior). El PBI usaba DATEADD -1 MONTH, que con más de un mes
        // compara periodos solapados y de distinto largo (+102 % con el rango completo); el
        // usuario lo cambió el 02-10-2026. Sin datos de todo ese periodo, no hay variación.
        var periodoAnterior = Periodos.PeriodoAnterior(fechas, calMax);
        var hayAnterior = periodoAnterior is { Count: > 0 } && periodoAnterior.Min >= calMin;
        var anteriores = hayAnterior ? EnFechas(periodoAnterior!) : null;

        var total = contexto.Count;
        int? totalAnterior = anteriores?.Count;

        var nota = Media(contexto);
        var notaAnterior = anteriores is null ? null : Media(anteriores);

        string ayudaPeriodo = fechas.Count == 0
            ? "Sin fechas elegidas"
            : hayAnterior
                ? $"{RangoDe(fechas)} frente a {RangoDe(periodoAnterior!)} (el mismo tiempo, justo antes)"
                : $"{RangoDe(fechas)}: se compara con el mismo tiempo justo antes" +
                  (periodoAnterior is { Count: > 0 } p0 ? $" ({RangoDe(p0)})" : "") +
                  $", y no hay datos antes del {Formato.Fecha(calMin)}. Elige un rango más corto para ver la variación.";

        // «Auditorías del mes anterior» sí sigue la medida del PBI: DATESMTD sobre DATEADD -1 MONTH.
        var fechasMesAnterior = Periodos.DesplazarMeses(fechas, -1, calMin, calMax);

        // Semana: del lunes de la semana de la última fecha hasta ella; la anterior, 7 días antes.
        int? semana = null, semanaAnterior = null;
        string ayudaSemana = "Sin fechas elegidas";
        if (maximo is { } max)
        {
            var actual = Periodos.Intervalo(Periodos.Lunes(max), max, calMin, calMax);
            semana = actual is { } s ? Contar(s.Desde, s.Hasta) : 0;
            ayudaSemana = actual is { } s1 ? Rango(s1.Desde, s1.Hasta) : "—";

            var menos7 = Periodos.DesplazarDias(fechas, -7, calMin, calMax);
            if (menos7.Count > 0)
            {
                var m = menos7.Max;
                var previa = Periodos.Intervalo(Periodos.Lunes(m), m, calMin, calMax);
                semanaAnterior = previa is { } p ? Contar(p.Desde, p.Hasta) : 0;
                ayudaSemana += previa is { } p1 ? $" frente a {Rango(p1.Desde, p1.Hasta)}" : "";
            }
        }

        // Mes: del día 1 del mes de la última fecha hasta ella (DATESMTD); el anterior, sobre
        // las fechas desplazadas un mes.
        int? mes = null, mesAnterior = null;
        string ayudaMes = "Sin fechas elegidas";
        if (maximo is { } maxMes)
        {
            var actual = Periodos.Intervalo(Periodos.InicioDeMes(maxMes), maxMes, calMin, calMax);
            mes = actual is { } s ? Contar(s.Desde, s.Hasta) : 0;
            ayudaMes = actual is { } s1 ? Rango(s1.Desde, s1.Hasta) : "—";

            if (fechasMesAnterior.Count > 0)
            {
                var m = fechasMesAnterior.Max;
                var previo = Periodos.Intervalo(Periodos.InicioDeMes(m), m, calMin, calMax);
                mesAnterior = previo is { } p ? Contar(p.Desde, p.Hasta) : 0;
                ayudaMes += previo is { } p1 ? $" frente a {Rango(p1.Desde, p1.Hasta)}" : "";
            }
        }

        double? variacionNota = nota is { } a && notaAnterior is { } b ? (a - b) * 100 : null;

        return
        [
            new("Total auditorías", "portapapeles", Formato.Entero(total), total, false,
                Variacion(total, totalAnterior),
                hayAnterior ? TextoVariacion(Variacion(total, totalAnterior), "vs. período anterior") : SinPeriodoCompleto,
                false, ayudaPeriodo),
            new("Auditorías de la semana", "calendario", semana is { } sv ? Formato.Entero(sv) : "—", semana, false,
                Variacion(semana, semanaAnterior), TextoVariacion(Variacion(semana, semanaAnterior), "vs. semana anterior"), false, ayudaSemana),
            new("Auditorías del mes", "calendario-mes", mes is { } mv ? Formato.Entero(mv) : "—", mes, false,
                Variacion(mes, mesAnterior), TextoVariacion(Variacion(mes, mesAnterior), "vs. mes anterior"), false, ayudaMes),
            new("Nota promedio de calidad", "estrella", Formato.Porcentaje(nota), nota, true,
                variacionNota,
                variacionNota is { } vn ? $"{Formato.Decimal2(Math.Abs(vn))} pp vs. período anterior" : hayAnterior ? SinAnterior : SinPeriodoCompleto,
                true, ayudaPeriodo),
        ];
    }

    /// <summary>Filtros de columna que también tiene la nómina (los demás son del auditor o del origen).</summary>
    private static readonly string[] CamposDeNomina = ["sector", "super", "team"];

    /// <summary>
    /// «Total agentes»: los agentes en nómina (legajos distintos con alguno de los cargos de
    /// agente) en las fechas elegidas y con los filtros de sector, super y team, frente a los
    /// que de ellos tienen al menos una auditoría con todos los filtros. Pedida por el usuario el
    /// 02-10-2026 en lugar de «Agentes auditados» (que no está en el PBI con este cálculo).
    /// </summary>
    private static TarjetaKpi TarjetaAgentes(
        InstantaneaAuditorias datos,
        List<Auditoria> contexto,
        List<DateOnly> fechas,
        Dictionary<string, HashSet<string>> elegidos,
        IEnumerable<string> cargosAgente)
    {
        const string Titulo = "Total agentes";
        if (datos.Nomina is not { } nomina)
        {
            return new(Titulo, "personas", "—", null, false, null, "Nómina no disponible", false,
                datos.ErrorNomina is { } e ? "No se pudo leer la nómina: " + e : "No se pudo leer la nómina");
        }

        var cargos = new HashSet<string>(cargosAgente.Select(c => c.Trim()), StringComparer.OrdinalIgnoreCase);
        var dias = new HashSet<DateOnly>(fechas);
        string? Valor(RegistroNomina n, string campo) => campo switch { "sector" => n.Sector, "super" => n.Super, _ => n.Team };

        var universo = new HashSet<long>();
        foreach (var n in nomina)
        {
            if (!dias.Contains(n.Fecha) || n.Cargo is null || !cargos.Contains(n.Cargo)) continue;
            var pasa = true;
            foreach (var campo in CamposDeNomina)
            {
                var set = elegidos[campo];
                if (set.Count > 0 && !set.Contains(Clave(Valor(n, campo)))) { pasa = false; break; }
            }
            if (pasa) universo.Add(n.Legajo);
        }

        var auditados = contexto
            .Where(a => a.Legajo is { } l && universo.Contains(l))
            .Select(a => a.Legajo!.Value)
            .Distinct()
            .Count();

        double? cobertura = universo.Count > 0 ? (double)auditados / universo.Count : null;
        var texto = cobertura is { } c
            ? $"{Formato.Entero(auditados)} auditados · {Formato.Porcentaje(c)}"
            : "Sin agentes en nómina con estos filtros";
        var ayuda = $"Agentes en nómina ({string.Join(", ", cargosAgente)}) en las fechas elegidas y con los filtros " +
                    $"de sector, super y team: {Formato.Entero(universo.Count)}. Con al menos una auditoría: {Formato.Entero(auditados)}.";

        return new(Titulo, "personas", Formato.Entero(universo.Count), universo.Count, false, null, texto, false, ayuda, cobertura);
    }

    private const string SinAnterior = "Sin datos del período anterior";

    /// <summary>Cuando el periodo anterior empieza antes que los datos (p. ej., con el rango completo).</summary>
    private const string SinPeriodoCompleto = "Sin período anterior con datos";

    /// <summary><c>DIVIDE(actual - anterior, anterior)</c>: null si no hay anterior o es 0.</summary>
    private static double? Variacion(int? actual, int? anterior)
        => actual is { } a && anterior is { } b && b != 0 ? (a - b) / (double)b : null;

    /// <summary>El texto de la variación, sin la flecha (la vista pone el icono según el signo).</summary>
    private static string TextoVariacion(double? variacion, string sufijo)
        => variacion is { } v ? $"{Formato.Porcentaje(Math.Abs(v))} {sufijo}" : SinAnterior;

    /// <summary><c>AVERAGE(Auditorias[Respuesta])</c>: ignora las notas vacías.</summary>
    private static double? Media(IEnumerable<Auditoria> filas)
    {
        double suma = 0;
        var n = 0;
        foreach (var f in filas)
        {
            if (f.Respuesta is not { } r) continue;
            suma += r;
            n++;
        }
        return n > 0 ? suma / n : null;
    }

    internal static DateOnly Acotar(DateOnly f, DateOnly min, DateOnly max) => f < min ? min : f > max ? max : f;

    /// <summary>
    /// Un punto por día, semana o mes con el número de filas y su <paramref name="valor"/> (la nota en
    /// General; el % con plan de acción en «T0 y planes de acción»).
    /// </summary>
    internal static List<PuntoSerie> Serie<T>(IReadOnlyCollection<T> filas, Func<T, DateOnly> fecha, Func<IEnumerable<T>, double?> valor, Vista vista)
    {
        var años = filas.Select(a => fecha(a).Year).Distinct().Count();

        switch (vista)
        {
            case Vista.Dia:
                return filas
                    .GroupBy(fecha)
                    .OrderBy(g => g.Key)
                    .Select(g => new PuntoSerie(
                        Formato.FechaCorta(g.Key),
                        Formato.Mayuscula(g.Key.ToString("dddd dd/MM/yyyy", Formato.Es)),
                        g.Count(), valor(g)))
                    .ToList();

            case Vista.Semana:
                return filas
                    .GroupBy(a => (fecha(a).Year, Semana: Periodos.Semana(fecha(a))))
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Semana)
                    .Select(g =>
                    {
                        var lunes = Periodos.Lunes(g.Min(fecha));
                        var etiqueta = años > 1 ? $"{g.Key.Semana}/{g.Key.Year % 100:D2}" : g.Key.Semana.ToString(Formato.Es);
                        return new PuntoSerie(etiqueta,
                            $"Semana {g.Key.Semana} de {g.Key.Year} ({Formato.FechaCorta(lunes)} – {Formato.FechaCorta(lunes.AddDays(6))})",
                            g.Count(), valor(g));
                    })
                    .ToList();

            default:
                return filas
                    .GroupBy(a => (fecha(a).Year, fecha(a).Month))
                    .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                    .Select(g =>
                    {
                        var corto = Formato.MesCorto(g.Key.Year, g.Key.Month);
                        return new PuntoSerie(años > 1 ? $"{corto} {g.Key.Year % 100:D2}" : corto,
                            Formato.MesLargo(g.Key.Year, g.Key.Month), g.Count(), valor(g));
                    })
                    .ToList();
        }
    }

    /// <param name="fechasConColumnas">Las fechas de las filas que pasan los filtros de columna (una por fila).</param>
    internal static GrupoFiltro GrupoMes(IEnumerable<DateOnly> fechasConColumnas, DateOnly desde, DateOnly hasta, HashSet<string> meses)
    {
        // El segmentador de Mes es del calendario: lo filtra el de fecha, no los de columna.
        // La cifra de cada mes es la de auditorías con los demás filtros.
        var cuenta = fechasConColumnas
            .Where(f => f >= desde && f <= hasta)
            .GroupBy(ClaveMes)
            .ToDictionary(g => g.Key, g => g.Count());

        var opciones = new List<OpcionFiltro>();
        var claves = new SortedSet<string>(meses, StringComparer.Ordinal);
        for (var d = Periodos.InicioDeMes(desde); d <= hasta; d = d.AddMonths(1))
            claves.Add(ClaveMes(d));

        foreach (var clave in claves)
        {
            var año = int.Parse(clave[..4], Formato.Es);
            var mes = int.Parse(clave[5..], Formato.Es);
            opciones.Add(new OpcionFiltro(clave, Formato.MesLargo(año, mes), cuenta.GetValueOrDefault(clave), meses.Contains(clave)));
        }

        return new GrupoFiltro("mes", "Mes", opciones);
    }

    internal static GrupoFiltro Grupo(string campo, string titulo, Dictionary<string, int> cuenta, HashSet<string> elegidos)
    {
        // Las elegidas se ven siempre, aunque otro filtro las deje sin auditorías, para poder quitarlas.
        foreach (var e in elegidos) cuenta.TryAdd(e, 0);

        var opciones = cuenta
            .OrderBy(kv => kv.Key == EnBlanco ? 1 : 0)
            .ThenBy(kv => kv.Key, StringComparer.Create(Formato.Es, true))
            .Select(kv => new OpcionFiltro(kv.Key, kv.Key, kv.Value, elegidos.Contains(kv.Key)))
            .ToList();

        return new GrupoFiltro(campo, titulo, opciones);
    }
}
