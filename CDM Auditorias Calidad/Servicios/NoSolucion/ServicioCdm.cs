using System.Globalization;
using System.Text;
using CDM_Auditorias_Calidad.Servicios.Comun;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>
/// Lo que la pantalla CDM necesita del cubo: el cubo actual, la caché de
/// respuestas y la construcción en segundo plano. En la aplicación es
/// <see cref="OrigenNoSolucion"/>; en los tests, un cubo fijo.
/// </summary>
public interface IOrigenCdm
{
    /// <summary>El cubo y su versión. Lanza <see cref="SinDatos"/> si todavía no hay.</summary>
    (Cubo Cubo, string Version) Actual();

    /// <summary>Una vista calculada una sola vez por versión de datos.</summary>
    T Respuesta<T>(string clave, Func<Cubo, T> calcular) where T : class;

    /// <summary>Lanza la construcción del cubo en segundo plano si no hay otra en marcha.</summary>
    void AsegurarConstruccion();

    /// <summary>Renueva el cubo en segundo plano si tiene más horas de las configuradas.</summary>
    void RenovarSiViejo();

    /// <summary>Pide traer de nuevo los datos; false si ya hay una descarga en curso o acaba de terminar.</summary>
    bool PedirActualizacion();

    /// <summary>Por dónde va la descarga y el último error.</summary>
    EstadoConstruccion Estado { get; }

    /// <summary>Si ahora mismo se están trayendo los datos.</summary>
    bool Construyendo { get; }
}

/// <summary>El origen de verdad: el cubo y la construcción de <see cref="ServicioNoSolucion"/>.</summary>
public sealed class OrigenNoSolucion : IOrigenCdm
{
    private readonly ServicioNoSolucion _ns;

    public OrigenNoSolucion(ServicioNoSolucion ns) => _ns = ns;

    public (Cubo Cubo, string Version) Actual() => _ns.Cache.Actual();

    public T Respuesta<T>(string clave, Func<Cubo, T> calcular) where T : class
        => (T)_ns.Cache.Respuesta(clave, c => calcular(c)).Valor;

    public void AsegurarConstruccion() => _ns.AsegurarConstruccion();

    public void RenovarSiViejo()
    {
        try { _ns.RenovarSiViejo(_ns.Cache.Actual().Cubo); }
        catch (SinDatos) { }
    }

    public bool PedirActualizacion() => _ns.PedirActualizacion();

    public EstadoConstruccion Estado => _ns.Fuente.Estado.Copia();

    public bool Construyendo => _ns.ConstruyendoAhora || _ns.Fuente.Estado.EnCurso;
}

/// <summary>
/// Lo común a las cuatro pestañas: si hay datos, qué datos hay, los filtros
/// resueltos y las opciones de los desplegables.
/// </summary>
public sealed class ContextoCdm
{
    /// <summary>Todavía no hay cubo: se está trayendo de BigQuery.</summary>
    public bool Preparando { get; init; }

    /// <summary>El tramo por el que va la descarga, si está en marcha.</summary>
    public string? Progreso { get; init; }

    /// <summary>El último error de la descarga, si lo hubo.</summary>
    public string? ErrorConstruccion { get; init; }

    /// <summary>Si ahora mismo se están trayendo los datos (con cubo: la renovación).</summary>
    public bool Construyendo { get; init; }

    public MetaPublica? Meta { get; init; }

    /// <summary>Nulo mientras no hay datos.</summary>
    public FiltrosResueltos? Filtros { get; init; }

    public IReadOnlyList<OpcionFiltro> OpcionesServicio { get; init; } = Array.Empty<OpcionFiltro>();
    public IReadOnlyList<OpcionFiltro> OpcionesMarca { get; init; } = Array.Empty<OpcionFiltro>();
    public IReadOnlyList<OpcionFiltro> OpcionesSupervisor { get; init; } = Array.Empty<OpcionFiltro>();
    public IReadOnlyList<OpcionFiltro> OpcionesTl { get; init; } = Array.Empty<OpcionFiltro>();
    public IReadOnlyList<OpcionFiltro> OpcionesAgente { get; init; } = Array.Empty<OpcionFiltro>();

    /// <summary>Filtros que no se pudieron aplicar tal cual (una fecha mal escrita, un rango al revés…).</summary>
    public IReadOnlyList<string> Avisos { get; init; } = Array.Empty<string>();

    /// <summary>Si hay datos y filtros con los que pintar las vistas.</summary>
    public bool Listo => Filtros is not null && Meta is not null;
}

/// <summary>Una vista calculada, o el aviso de por qué no se pudo con estos filtros.</summary>
public sealed record VistaCdm<T>(T? Datos, string? Aviso) where T : class;

/// <summary>Motivos con las llamadas de ejemplo de cada tipología N3.</summary>
public sealed record MotivosCdm(RespuestaMotivos Motivos, IReadOnlyDictionary<string, RespuestaMuestras> Muestras);

/// <summary>Sin acceso a internet con las llamadas de ejemplo de cada impedimento.</summary>
/// <param name="MuestrasCausa">Llamadas de ejemplo de cada causa de no solución (cubo v3; vacío si el cubo no la sabe).</param>
public sealed record InternetCdm(
    RespuestaInternet Internet, IReadOnlyDictionary<int, RespuestaMuestrasImpedimento> Muestras,
    IReadOnlyDictionary<string, RespuestaMuestras> MuestrasCausa);

/// <summary>Una fila de la tabla de equipos, con su puesto en el ranking por % y su tono de desvío.</summary>
public sealed record FilaEquipoCdm(int Puesto, FilaEquipo Fila, string Detalle, string? Tono);

/// <summary>La tabla de equipos ya buscada y ordenada como la pide la URL.</summary>
public sealed record EquiposCdm(
    RespuestaEquipos Equipos, string Nivel, string Orden, string Direccion, string Busca,
    IReadOnlyList<FilaEquipoCdm> Filas, double MaxPct);

/// <summary>Un fichero CSV para descargar.</summary>
public sealed record FicheroCsv(string Nombre, byte[] Contenido);

/// <summary>
/// La pantalla CDM No solución: resuelve los filtros de la URL contra el
/// cubo y calcula cada pestaña con <see cref="Agregados"/>, con las mismas
/// llaves de caché que la API anterior (<c>RutasNoSolucion</c>).
/// </summary>
/// <remarks>
/// Nunca espera a BigQuery: sin cubo, el contexto sale «preparando» y la
/// descarga se lanza en segundo plano.
/// </remarks>

public sealed class ServicioCdm
{
    /// <summary>Los niveles de la pestaña Equipos.</summary>
    public static readonly string[] Niveles = { "supervisor", "tl", "agente" };

    /// <summary>Las columnas por las que se puede ordenar la tabla de equipos.</summary>
    public static readonly string[] ColumnasEquipos = { "nombre", "llamadas", "encuestadas", "cobertura", "nosol", "pct", "desvio" };

    /// <summary>Largo máximo de la tipología que se admite en la URL del CSV.</summary>
    public const int MaxLargoN3 = 120;

    private static readonly StringComparer Espanol = StringComparer.Create(CultureInfo.GetCultureInfo("es-ES"), ignoreCase: false);

    private readonly IOrigenCdm _origen;

    public ServicioCdm(ServicioNoSolucion noSolucion) : this(new OrigenNoSolucion(noSolucion)) { }

    public ServicioCdm(IOrigenCdm origen) => _origen = origen;

    private static string Clave(object?[] prefijo, object?[] filtros, params object?[] sufijo)
        => FormatoPython.ReprTupla(prefijo.Concat(filtros).Concat(sufijo));

    // ------------------------------------------------------------------
    // Contexto común
    // ------------------------------------------------------------------

    /// <summary>
    /// Qué datos hay y los filtros de la URL resueltos contra ellos. Sin cubo
    /// lanza su construcción y sale «preparando».
    /// </summary>
    public ContextoCdm Contexto(PeticionCdm peticion)
    {
        try
        {
            _origen.Actual();
        }
        catch (SinDatos)
        {
            _origen.AsegurarConstruccion();
            var estado = _origen.Estado;
            return new ContextoCdm { Preparando = true, Progreso = estado.Progreso, ErrorConstruccion = estado.UltimoError, Construyendo = true };
        }

        var meta = _origen.Respuesta(FormatoPython.ReprTupla(new object?[] { "meta" }), Agregados.MetaPublica);
        if (meta.DiaMin is null || meta.DiaMax is null)
        {
            return new ContextoCdm { Meta = meta, Construyendo = _origen.Construyendo };
        }

        var avisos = new List<string>();
        string? FechaDeLaUrl(string? texto, string cual)
        {
            if (string.IsNullOrEmpty(texto)) return null;
            if (FiltrosCdm.EsFecha(texto)) return texto;
            avisos.Add($"La fecha {cual} «{texto}» no tiene la forma AAAA-MM-DD: se usa la de por defecto.");
            return null;
        }

        var (desde, hasta) = FiltrosCdm.AcotarFechas(
            FechaDeLaUrl(peticion.Desde, "inicial"), FechaDeLaUrl(peticion.Hasta, "final"), meta.DiaMin, meta.DiaMax);

        var filtros = new FiltrosResueltos
        {
            Desde = desde,
            Hasta = hasta,
            DesdePedido = desde == FiltrosCdm.DesdePorDefecto(hasta, meta.DiaMin) ? null : desde,
            HastaPedido = hasta == meta.DiaMax ? null : hasta,
            Marca = FiltrosCdm.SoloConocidos(peticion.Marca, meta.Marcas),
            Servicio = FiltrosCdm.SoloConocidos(peticion.Servicio, meta.Servicios),
            Supervisor = FiltrosCdm.Limpiar(peticion.Supervisor),
            Tl = FiltrosCdm.Limpiar(peticion.Tl),
            Agente = FiltrosCdm.Limpiar(peticion.Agente),
        };

        // Las opciones respetan fechas, marca y servicio; las personas se encadenan sobre ellas.
        RespuestaOpciones? opciones = null;
        try
        {
            var f = filtros.ParaAgregados(conPersonas: false);
            opciones = _origen.Respuesta(Clave(new object?[] { "opciones" }, filtros.Clave(conPersonas: false)),
                cubo => Agregados.Opciones(cubo, f));
        }
        catch (FiltroInvalido ex)
        {
            avisos.Add(ex.Message);
        }

        var cadena = opciones is null
            ? FiltrosCdm.SinEncadenar(filtros.Supervisor, filtros.Tl, filtros.Agente)
            : FiltrosCdm.Encadenar(opciones.Agentes, filtros.Supervisor, filtros.Tl, filtros.Agente);
        filtros = filtros with { Supervisor = cadena.Supervisor, Tl = cadena.Tl, Agente = cadena.Agente };

        return new ContextoCdm
        {
            Meta = meta,
            Filtros = filtros,
            Construyendo = _origen.Construyendo,
            OpcionesServicio = opciones?.Servicios.Select(o => new OpcionFiltro(o.Nombre, o.Llamadas)).ToList()
                               ?? meta.Servicios.Select(n => new OpcionFiltro(n, null)).ToList(),
            OpcionesMarca = opciones?.Marcas.Select(o => new OpcionFiltro(o.Nombre, o.Llamadas)).ToList()
                            ?? meta.Marcas.Select(n => new OpcionFiltro(n, null)).ToList(),
            OpcionesSupervisor = cadena.Supervisores,
            OpcionesTl = cadena.Tls,
            OpcionesAgente = cadena.Agentes,
            Avisos = avisos,
        };
    }

    /// <summary>Después de servir una página: renueva el cubo en segundo plano si es viejo.</summary>
    public void RenovarSiViejo() => _origen.RenovarSiViejo();

    /// <summary>Lo que hace «Actualizar datos»: false si ya hay una descarga en curso o acaba de terminar.</summary>
    public bool PedirActualizacion() => _origen.PedirActualizacion();

    /// <summary>Calcula una vista con su llave de caché; un filtro que no existe en los datos vuelve como aviso.</summary>
    private VistaCdm<T> Vista<T>(ContextoCdm ctx, object?[] prefijo, Func<Cubo, Filtros, T> calcular, params object?[] sufijo)
        where T : class
    {
        var filtros = ctx.Filtros ?? throw new InvalidOperationException("La vista necesita datos: mira ContextoCdm.Listo.");
        var f = filtros.ParaAgregados();
        try
        {
            return new VistaCdm<T>(_origen.Respuesta(Clave(prefijo, filtros.Clave(), sufijo), cubo => calcular(cubo, f)), null);
        }
        catch (FiltroInvalido ex)
        {
            return new VistaCdm<T>(null, ex.Message);
        }
    }

    // ------------------------------------------------------------------
    // Las pestañas
    // ------------------------------------------------------------------

    public VistaCdm<RespuestaResumen> Resumen(ContextoCdm ctx)
        => Vista(ctx, new object?[] { "resumen" }, Agregados.Resumen);

    /// <summary>Un nivel válido de la pestaña Equipos (por defecto, supervisor).</summary>
    public static string Nivel(string? nivel) => Niveles.Contains(nivel) ? nivel! : "supervisor";

    /// <summary>
    /// La tabla de equipos del nivel pedido, con el buscador y el orden de la
    /// URL. El puesto es siempre el del ranking por %, aunque la tabla se
    /// ordene por otra columna.
    /// </summary>
    public VistaCdm<EquiposCdm> Equipos(ContextoCdm ctx, string? nivel, string? orden, string? direccion, string? busca)
    {
        var n = Nivel(nivel);
        var vista = Vista(ctx, new object?[] { "equipos", n }, (cubo, f) => Agregados.Equipos(cubo, n, f), (object?)null);
        if (vista.Datos is null) return new VistaCdm<EquiposCdm>(null, vista.Aviso);
        return new VistaCdm<EquiposCdm>(TablaEquipos(vista.Datos, orden, direccion, busca), null);
    }

    /// <summary>Busca y ordena la tabla de equipos como la pantalla en React.</summary>
    public static EquiposCdm TablaEquipos(RespuestaEquipos r, string? orden, string? direccion, string? busca)
    {
        var col = ColumnasEquipos.Contains(orden) ? orden! : "pct";
        var dir = direccion is "asc" or "desc" ? direccion : col == "nombre" ? "asc" : "desc";
        var q = (busca ?? "").Trim();

        var puesto = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < r.Filas.Count; i++) puesto.TryAdd(r.Filas[i].Nombre, i + 1);

        var qMin = q.ToLowerInvariant();
        IEnumerable<FilaEquipo> filas = q.Length == 0
            ? r.Filas
            : r.Filas.Where(f => $"{f.Nombre} {f.Tl} {f.Supervisor}".ToLowerInvariant().Contains(qMin, StringComparison.Ordinal));

        IOrderedEnumerable<FilaEquipo> ordenadas;
        if (col == "nombre")
        {
            ordenadas = dir == "asc" ? filas.OrderBy(f => f.Nombre, Espanol) : filas.OrderByDescending(f => f.Nombre, Espanol);
        }
        else
        {
            // Un nulo va como el menor de todos, igual que -Infinity en el navegador.
            double Valor(FilaEquipo f) => (col switch
            {
                "llamadas" => f.Llamadas,
                "encuestadas" => f.Encuestadas,
                "cobertura" => f.Cobertura,
                "nosol" => f.Nosol,
                "desvio" => f.Desvio,
                _ => f.Pct,
            }) ?? double.NegativeInfinity;
            ordenadas = dir == "asc" ? filas.OrderBy(Valor) : filas.OrderByDescending(Valor);
        }

        var salida = ordenadas
            .Select(f => new FilaEquipoCdm(puesto.GetValueOrDefault(f.Nombre), f, DetalleEquipo(r.Nivel, f), Tono(f.Desvio)))
            .ToList();
        var maxPct = Math.Max(1, r.Filas.Select(f => f.Pct ?? 0).DefaultIfEmpty(0).Max());
        return new EquiposCdm(r, r.Nivel, col, dir, q, salida, maxPct);
    }

    /// <summary>La línea de debajo del nombre: el equipo de cada agente, o cuántos agentes lleva.</summary>
    public static string DetalleEquipo(string nivel, FilaEquipo f) => nivel switch
    {
        "agente" => $"{f.Tl} · {f.Supervisor}",
        "tl" => $"{f.Supervisor} · {f.Agentes} agentes",
        _ => $"{f.Agentes} agentes",
    };

    /// <summary>
    /// Cómo de lejos está una tasa de la media de la ventana. Los cortes son
    /// los del informe: +6 % es crítico, +2,5 % merece atención, −2,5 % es
    /// bueno (diferencias en puntos sobre la media).
    /// </summary>
    public static string? Tono(double? desvio) => desvio switch
    {
        null => null,
        >= 6 => "critico",
        >= 2.5 => "atencion",
        <= -2.5 => "bueno",
        _ => null,
    };

    /// <summary>Motivos y, para cada tipología N3, sus diez llamadas de ejemplo con los mismos filtros.</summary>
    public VistaCdm<MotivosCdm> Motivos(ContextoCdm ctx)
    {
        var vista = Vista(ctx, new object?[] { "motivos" }, Agregados.Motivos);
        if (vista.Datos is null) return new VistaCdm<MotivosCdm>(null, vista.Aviso);

        var muestras = new Dictionary<string, RespuestaMuestras>(StringComparer.Ordinal);
        foreach (var n3 in vista.Datos.N3.Select(x => x.Nombre))
        {
            var m = Vista(ctx, new object?[] { "muestras", n3 }, (cubo, f) => Agregados.MuestrasN3(cubo, n3, f));
            if (m.Datos is not null) muestras[n3] = m.Datos;
        }
        return new VistaCdm<MotivosCdm>(new MotivosCdm(vista.Datos, muestras), null);
    }

    /// <summary>
    /// Sin acceso a internet y, para cada impedimento, sus diez llamadas de ejemplo: solo de
    /// sinAccesoInternet, como la barra, para que su total sea el mismo.
    /// </summary>
    public VistaCdm<InternetCdm> Internet(ContextoCdm ctx)
    {
        var vista = Vista(ctx, new object?[] { "internet" }, Agregados.Internet);
        if (vista.Datos is null) return new VistaCdm<InternetCdm>(null, vista.Aviso);

        var muestras = new Dictionary<int, RespuestaMuestrasImpedimento>();
        foreach (var bit in vista.Datos.Impedimentos.Select(x => x.Bit))
        {
            var m = Vista(ctx, new object?[] { "muestras-imp", Agregados.TipologiaInternet, bit },
                (cubo, f) => Agregados.MuestrasImpedimento(cubo, bit, f, Agregados.TipologiaInternet));
            if (m.Datos is not null) muestras[bit] = m.Datos;
        }
        var porCausa = new Dictionary<string, RespuestaMuestras>();
        foreach (var clave in (vista.Datos.Causas ?? new List<CausaInternet>()).Where(c => c.Nosol > 0).Select(c => c.Clave))
        {
            var m = Vista(ctx, new object?[] { "muestras-causa", clave },
                (cubo, f) => Agregados.MuestrasCausa(cubo, clave, f));
            if (m.Datos is not null) porCausa[clave] = m.Datos;
        }
        return new VistaCdm<InternetCdm>(new InternetCdm(vista.Datos, muestras, porCausa), null);
    }

    // ------------------------------------------------------------------
    // Exportar
    // ------------------------------------------------------------------

    /// <summary>
    /// Todas las no solucionadas de los filtros en CSV (el del informe),
    /// opcionalmente de una tipología o de un impedimento. Nulo si todavía no
    /// hay datos; un filtro que no existe es <see cref="PeticionInvalida"/>.
    /// </summary>
    public FicheroCsv? CsvLlamadas(PeticionCdm peticion, string? n3, int? bit)
    {
        if (n3 is { Length: > MaxLargoN3 }) throw new PeticionInvalida($"La tipología admite como mucho {MaxLargoN3} caracteres.");
        if (bit is < 0 or > 1024) throw new PeticionInvalida("El impedimento debe estar entre 0 y 1024.");

        var ctx = Contexto(peticion);
        if (ctx.Preparando) return null;
        if (!ctx.Listo) throw new PeticionInvalida("Todavía no hay días con datos.");

        var f = ctx.Filtros!.ParaAgregados();
        try
        {
            var (cubo, _) = _origen.Actual();
            var (nombre, texto) = Agregados.CsvLlamadas(cubo, f, string.IsNullOrEmpty(n3) ? null : n3, bit);
            return new FicheroCsv(nombre, Encoding.UTF8.GetBytes(texto));
        }
        catch (FiltroInvalido ex)
        {
            throw new PeticionInvalida(ex.Message);
        }
    }

    /// <summary>La tabla de equipos tal y como se ve (buscada y ordenada), en CSV para Excel.</summary>
    public FicheroCsv? CsvEquipos(PeticionCdm peticion, string? nivel, string? orden, string? direccion, string? busca)
    {
        var ctx = Contexto(peticion);
        if (ctx.Preparando) return null;
        if (!ctx.Listo) throw new PeticionInvalida("Todavía no hay días con datos.");
        var vista = Equipos(ctx, nivel, orden, direccion, busca);
        if (vista.Datos is null) throw new PeticionInvalida(vista.Aviso ?? "Filtro no válido.");

        var n = vista.Datos.Nivel;
        var cabecera = new List<string> { n switch { "tl" => "team_leader", "agente" => "agente", _ => "supervisor" } };
        if (n == "agente") cabecera.Add("team_leader");
        if (n != "supervisor") cabecera.Add("supervisor");
        if (n != "agente") cabecera.Add("agentes");
        cabecera.AddRange(new[] { "llamadas", "encuestadas", "cobertura_pct", "no_solucionadas", "pct_no_solucion", "desvio_pct" });

        var filas = vista.Datos.Filas.Select(x =>
        {
            var f = x.Fila;
            var v = new List<string?> { f.Nombre };
            if (n == "agente") v.Add(f.Tl);
            if (n != "supervisor") v.Add(f.Supervisor);
            if (n != "agente") v.Add(f.Agentes?.ToString(CultureInfo.InvariantCulture));
            v.AddRange(new[]
            {
                f.Llamadas.ToString(CultureInfo.InvariantCulture), f.Encuestadas.ToString(CultureInfo.InvariantCulture),
                Dec2(f.Cobertura), f.Nosol.ToString(CultureInfo.InvariantCulture), Dec2(f.Pct), Dec2(f.Desvio),
            });
            return v;
        });
        var plural = n switch { "tl" => "team_leaders", "agente" => "agentes", _ => "supervisores" };
        return new FicheroCsv($"nosolucion_{plural}_{RangoNombre(ctx.Filtros!)}.csv", ExportacionCsv.Bytes(cabecera, filas));
    }

    /// <summary>
    /// Cada no solucionada de sinAccesoInternet con su causa (atención, proceso, las dos, cliente o
    /// sin causa) y las señales que la deciden; opcionalmente, solo las de una causa.
    /// </summary>
    public FicheroCsv? CsvCausas(PeticionCdm peticion, string? causa)
    {
        if (causa is { Length: > 40 }) throw new PeticionInvalida("Causa no válida.");
        var ctx = Contexto(peticion);
        if (ctx.Preparando) return null;
        if (!ctx.Listo) throw new PeticionInvalida("Todavía no hay días con datos.");
        try
        {
            var (cubo, _) = _origen.Actual();
            var (nombre, texto) = Agregados.CsvCausas(cubo, ctx.Filtros!.ParaAgregados(), string.IsNullOrEmpty(causa) ? null : causa);
            return new FicheroCsv(nombre, Encoding.UTF8.GetBytes(texto));
        }
        catch (FiltroInvalido ex)
        {
            throw new PeticionInvalida(ex.Message);
        }
    }

    /// <summary>Las 25 tipologías N3 de Motivos en CSV para Excel.</summary>
    public FicheroCsv? CsvTipologias(PeticionCdm peticion)
    {
        var ctx = Contexto(peticion);
        if (ctx.Preparando) return null;
        if (!ctx.Listo) throw new PeticionInvalida("Todavía no hay días con datos.");
        var vista = Vista(ctx, new object?[] { "motivos" }, Agregados.Motivos);
        if (vista.Datos is null) throw new PeticionInvalida(vista.Aviso ?? "Filtro no válido.");

        var cabecera = new[] { "tipologia_n3", "encuestadas", "no_solucionadas", "pct_no_solucion", "desvio_pct", "peso_pct" };
        var filas = vista.Datos.N3.Select(x => new List<string?>
        {
            x.Nombre, x.Encuestadas.ToString(CultureInfo.InvariantCulture), x.Nosol.ToString(CultureInfo.InvariantCulture),
            Dec2(x.Pct), Dec2(x.Desvio), Dec2(x.Peso),
        });
        return new FicheroCsv($"nosolucion_tipologias_{RangoNombre(ctx.Filtros!)}.csv", ExportacionCsv.Bytes(cabecera, filas));
    }

    /// <summary>Sufijo con el rango de fechas, como el del CSV de llamadas: 20260829-20260927.</summary>
    public static string RangoNombre(FiltrosResueltos f) => $"{f.Desde.Replace("-", "")}-{f.Hasta.Replace("-", "")}";

    /// <summary>
    /// Porcentaje a 2 decimales y con coma, que es lo que Excel en español
    /// lee como número (en pantalla va a 1 decimal). Redondea como
    /// <c>Math.round</c> del navegador.
    /// </summary>
    public static string Dec2(double? x)
        => x is null ? "" : (Math.Floor(x.Value * 100 + 0.5) / 100).ToString(CultureInfo.InvariantCulture).Replace('.', ',');
}
