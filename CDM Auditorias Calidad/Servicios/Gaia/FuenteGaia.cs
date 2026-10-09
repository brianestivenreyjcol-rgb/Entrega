using System.Globalization;
using System.Text;
using CDM_Auditorias_Calidad.Servicios.NoSolucion;
using T = CDM_Auditorias_Calidad.Servicios.Gaia.TraduccionesGaia;

namespace CDM_Auditorias_Calidad.Servicios.Gaia;

/// <summary>Hasta qué día hay llamadas de una marca en DataOrb (tarjeta «Actualización BD» del PBI).</summary>
public sealed record ActualizacionMarca(string Marca, DateOnly? PrimeraFecha, DateOnly? UltimaFecha);

/// <summary>Todo lo que se trae de una vez: se guarda entero en la caché.</summary>
public sealed class DatosGaia
{
    public List<LlamadaGaia> Llamadas { get; set; } = new();
    public List<ActualizacionMarca> Actualizacion { get; set; } = new();

    /// <summary>Agentes del Excel con al menos un día (tengan llamadas o no).</summary>
    public List<AgenteGaia> Agentes { get; set; } = new();
    public List<string> AvisosExcel { get; set; } = new();

    /// <summary>Lo que no se pudo traer pero no impide enseñar el resto (p. ej. la españolización).</summary>
    public List<string> AvisosCarga { get; set; } = new();

    /// <summary>La lista de palabras con la que se buscó (los índices de cada llamada apuntan a ella).</summary>
    public PalabrasGaia Palabras { get; set; } = new();

    /// <summary>Cuándo se trajeron los datos de BigQuery.</summary>
    public DateTime Generado { get; set; }

    /// <summary>Fecha de modificación del Excel con el que se trajeron.</summary>
    public DateTime ExcelModificado { get; set; }

    /// <summary>Versión del formato de la caché: si cambia, la caché vieja no se usa.</summary>
    public int Version { get; set; } = VersionActual;

    public const int VersionActual = 2;
}

/// <summary>
/// Trae de BigQuery las llamadas de los agentes del Excel en sus días de formación
/// (<c>Consultas/GaiaLlamadas.sql</c>) y la actualización por marca (<c>GaiaActualizacion.sql</c>).
/// Usa el mismo cliente ODBC que No solución (un único turno con el driver).
/// </summary>
public sealed class FuenteGaia
{
    private readonly string _odbc;
    private readonly string _carpetaConsultas;
    private readonly ILogger? _log;
    private readonly int _agentesPorConsulta;

    public FuenteGaia(string odbc, string carpetaConsultas, int agentesPorConsulta = 60, ILogger? log = null)
    {
        _odbc = odbc;
        _carpetaConsultas = carpetaConsultas;
        _agentesPorConsulta = agentesPorConsulta;
        _log = log;
    }

    public async Task<DatosGaia> TraerAsync(NominaGaia nomina, PalabrasGaia palabras, CancellationToken ct)
    {
        var agentes = nomina.Agentes.Where(a => a.Dias.Count > 0).ToList();
        var llamadas = new List<LlamadaGaia>();
        var avisos = new List<string>();
        if (agentes.Count > 0)
        {
            // Por tandas de agentes: con todo de una vez el driver se cortaba al bajar.
            var plantilla = await LeerConsultaAsync("GaiaLlamadas.sql", ct);
            var porId = agentes.ToDictionary(a => a.Id, StringComparer.Ordinal);
            var vistos = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tanda in agentes.Chunk(Math.Max(1, _agentesPorConsulta)))
            {
                var filas = await FuenteBigQuery.ConsultarAsync(_odbc, ArmarConsulta(plantilla, tanda), _log, ct);
                llamadas.AddRange(filas.Select(f => Convertir(f, porId)).Where(l => vistos.Add(l.IdConversacion)));
            }

            // La españolización va aparte: si falla, el resto se enseña igual.
            try
            {
                await TraerEspanolizacionAsync(agentes, palabras, llamadas, ct);
            }
            catch (Exception ex) when (ex is ErrorBigQuery or IOException or InvalidOperationException)
            {
                _log?.LogWarning("GAIA: la españolización no se pudo traer: {Error}", ex.Message);
                avisos.Add("No se pudo traer la españolización (transcripciones): " + ex.Message);
            }
        }

        var actualizacion = (await FuenteBigQuery.ConsultarAsync(_odbc, await LeerConsultaAsync("GaiaActualizacion.sql", ct), _log, ct))
            .Select(f => new ActualizacionMarca(Texto(f, "Marca"), Dia(f, "PrimeraFecha"), Dia(f, "UltimaFecha")))
            .ToList();

        return new DatosGaia
        {
            Llamadas = llamadas.OrderBy(l => l.FechaHora).ToList(),
            Actualizacion = actualizacion,
            Agentes = agentes,
            AvisosExcel = nomina.Avisos,
            AvisosCarga = avisos,
            Palabras = palabras,
            Generado = DateTime.Now,
            ExcelModificado = nomina.ExcelModificado,
        };
    }

    /// <summary>
    /// <c>Consultas/GaiaEspanolizacion.sql</c> por tandas: marca en cada llamada si tiene transcripción
    /// y qué palabras de la lista dijo el agente.
    /// </summary>
    private async Task TraerEspanolizacionAsync(List<AgenteGaia> agentes, PalabrasGaia palabras, List<LlamadaGaia> llamadas, CancellationToken ct)
    {
        var plantilla = await LeerConsultaAsync("GaiaEspanolizacion.sql", ct);
        var expresion = palabras.ExpresionSql();
        var porConversacion = llamadas.GroupBy(l => l.IdConversacion).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var tanda in agentes.Chunk(Math.Max(1, _agentesPorConsulta)))
        {
            var sql = Sustituir(ArmarConsulta(plantilla, tanda), "{{PALABRAS}}", expresion);
            foreach (var f in await FuenteBigQuery.ConsultarAsync(_odbc, sql, _log, ct))
            {
                if (!porConversacion.TryGetValue(Texto(f, "IdConversacion"), out var l)) continue;
                l.TieneTranscripcion = true;
                l.Palabras = Logico(f, "AgenteIdentificado") == true ? PalabrasGaia.Indices(Texto(f, "Palabras")) : null;
            }
        }
    }

    /// <summary>Sustituye un marcador que tiene que estar exactamente una vez.</summary>
    public static string Sustituir(string plantilla, string marcador, string valor)
    {
        var veces = (plantilla.Length - plantilla.Replace(marcador, "", StringComparison.Ordinal).Length) / marcador.Length;
        if (veces != 1)
        {
            throw new InvalidOperationException($"La consulta de GAIA tiene que llevar el marcador {marcador} una sola vez (lleva {veces}).");
        }
        return plantilla.Replace(marcador, valor, StringComparison.Ordinal);
    }

    private async Task<string> LeerConsultaAsync(string nombre, CancellationToken ct)
        => await File.ReadAllTextAsync(Path.Combine(_carpetaConsultas, nombre), ct);

    /// <summary>
    /// Sustituye <c>{{PARES}}</c> por un <c>STRUCT(id, día)</c> por cada agente y día de formación.
    /// Los ID se escapan: vienen de un Excel que edita cualquiera.
    /// </summary>
    public static string ArmarConsulta(string plantilla, IEnumerable<AgenteGaia> agentes)
    {
        // Exactamente una vez: si estuviera también en un comentario, la lista rompería el SQL.
        var sb = new StringBuilder();
        foreach (var a in agentes)
        {
            foreach (var dia in a.Dias.Keys)
            {
                if (sb.Length > 0) sb.Append(",\n    ");
                sb.Append("STRUCT('").Append(Escapar(a.Id)).Append("' AS id_agente, DATE '")
                  .Append(dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append("' AS dia)");
            }
        }
        return Sustituir(plantilla, "{{PARES}}", sb.ToString());
    }

    /// <summary>Texto seguro dentro de comillas simples de BigQuery.</summary>
    public static string Escapar(string texto)
        => texto.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);

    /// <summary>Una fila de BigQuery como llamada, traducida y con los datos del agente.</summary>
    public static LlamadaGaia Convertir(Dictionary<string, object?> f, IReadOnlyDictionary<string, AgenteGaia> agentes)
    {
        var fecha = Dia(f, "Fecha") ?? DateOnly.MinValue;
        var idAgente = Texto(f, "IdAgente");
        agentes.TryGetValue(idAgente, out var ag);
        var fechaHora = DateTime.TryParseExact(Texto(f, "FechaHora"), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var fh) ? fh : fecha.ToDateTime(TimeOnly.MinValue);

        return new LlamadaGaia
        {
            IdConversacion = Texto(f, "IdConversacion"),
            IdExterno = Texto(f, "IdExterno"),
            Fecha = fecha,
            FechaHora = fechaHora,
            IdAgente = idAgente,
            IdCliente = Texto(f, "IdCliente"),
            Marca = Texto(f, "Marca"),

            Agente = ag?.Nombre ?? idAgente,
            Sector = ag?.Sector ?? "",
            Supervisor = ag?.Supervisor ?? "",
            Coordinador = ag?.Coordinador ?? "",
            Formador = ag?.Formador ?? "",
            Oleada = ag?.Oleada ?? "",
            TipoConexion = ag is not null && ag.Dias.TryGetValue(fecha, out var tipo) ? tipo : "",

            DuracionSegundos = Doble(f, "DuracionSegundos"),
            TiempoNoHablado = Doble(f, "TiempoNoHablado"),
            PorcentajeHabla = Doble(f, "PorcentajeHabla"),
            VecesSePisaron = Doble(f, "VecesSePisaron"),

            Contexto = T.Traducir(T.Contexto, Texto(f, "Contexto")),
            RiesgoChurn = T.Traducir(T.RiesgoChurn, Texto(f, "RiesgoChurn")),
            RazonNivel1 = T.Traducir(T.RazonNivel1, Texto(f, "RazonNivel1")),
            TipoProblema = T.Traducir(T.TipoProblema, Texto(f, "TipoProblema")),
            TipoConsulta = T.Traducir(T.TipoConsulta, Texto(f, "TipoConsulta")),
            TemaContacto = Texto(f, "TemaContacto"),
            GrupoResolucion = Texto(f, "GrupoResolucion"),
            Motivo1 = Texto(f, "Motivo1"),
            Motivo2 = Texto(f, "Motivo2"),
            Motivo3 = Texto(f, "Motivo3"),
            SentimientoInicial = T.Traducir(T.Sentimiento, Texto(f, "SentimientoInicial")),
            SentimientoFinal = T.Traducir(T.Sentimiento, Texto(f, "SentimientoFinal")),
            EstadoResolucion = T.Traducir(T.EstadoResolucion, Texto(f, "EstadoResolucion")),
            Obstaculos = Texto(f, "Obstaculos"),
            ProblemaResuelto = Logico(f, "ProblemaResuelto"),
            ResumenContacto = Texto(f, "ResumenContacto"),
            ResumenResolucion = Texto(f, "ResumenResolucion"),

            CalificacionSaludo = Texto(f, "CalificacionSaludo"),
            CalificacionSolucion = Texto(f, "CalificacionSolucion"),
            CalificacionResumen = Texto(f, "CalificacionResumen"),
            CalificacionCierre = Texto(f, "CalificacionCierre"),
            CalificacionConfirmacion = Texto(f, "CalificacionConfirmacion"),
            CalificacionLenguajeClaro = Texto(f, "CalificacionLenguajeClaro"),
            CalificacionReconocimiento = Texto(f, "CalificacionReconocimiento"),

            Transferencia = Entero(f, "Transferencia"),
            Rellamada72h = Entero(f, "Rellamada72h"),
            MinutosSiguienteLlamada = Largo(f, "MinutosSiguienteLlamada"),
            EncuestaSolucion = Entero(f, "EncuestaSolucion"),
            EncuestaEnviada = Logico(f, "EncuestaEnviada") ?? false,

            IntentoVenta = Logico(f, "IntentoVenta"),
            PosibleVentaEntrante = Logico(f, "PosibleVentaEntrante"),
            ResultadoVenta = T.Traducir(T.ResultadoVenta, Texto(f, "ResultadoVenta")),
            CategoriaOferta = T.Traducir(T.CategoriaOferta, Texto(f, "CategoriaOferta")),
            AlineacionOferta = Texto(f, "AlineacionOferta"),
            TieneVenta = Logico(f, "TieneVenta"),
            TipoServicioVenta = Texto(f, "TipoServicioVenta"),
        };
    }

    // ------------------------------------------------------------------
    // Lectura de los valores del driver (tipos según BigQuery)
    // ------------------------------------------------------------------

    private static object? Valor(Dictionary<string, object?> f, string clave)
        => f.TryGetValue(clave, out var v) && v is not null && v is not DBNull ? v : null;

    internal static string Texto(Dictionary<string, object?> f, string clave)
        => Valor(f, clave) is { } v ? (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Trim() : "";

    internal static double? Doble(Dictionary<string, object?> f, string clave)
        => Valor(f, clave) switch
        {
            null => null,
            string s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null,
            var v => Convert.ToDouble(v, CultureInfo.InvariantCulture),
        };

    internal static int? Entero(Dictionary<string, object?> f, string clave)
        => Doble(f, clave) is { } d ? (int)d : null;

    internal static long? Largo(Dictionary<string, object?> f, string clave)
        => Doble(f, clave) is { } d ? (long)d : null;

    internal static bool? Logico(Dictionary<string, object?> f, string clave)
        => Valor(f, clave) switch
        {
            null => null,
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1",
            var v => Convert.ToInt64(v, CultureInfo.InvariantCulture) != 0,
        };

    internal static DateOnly? Dia(Dictionary<string, object?> f, string clave)
        => Valor(f, clave) switch
        {
            null => null,
            DateTime dt => DateOnly.FromDateTime(dt),
            DateOnly d => d,
            var v => DateOnly.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d) ? d : null,
        };
}
