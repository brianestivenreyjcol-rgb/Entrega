using System.Data.Odbc;
using System.Globalization;
using System.Text.Json;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>Un error de BigQuery que no es transitorio, o que agotó los reintentos.</summary>
public sealed class ErrorBigQuery : Exception
{
    public ErrorBigQuery(string mensaje) : base(mensaje) { }
}

/// <summary>
/// Fuente de No solución: descarga de BigQuery por ODBC (driver Simba, DSN
/// <c>BQCOL</c>) la ventana completa y ensambla el cubo. Hace lo mismo que
/// backend/app/nosolucion/ventana.py y bigquery.py.
/// </summary>
/// <remarks>
/// <para>
/// El driver no admite consultas simultáneas (con dos a la vez aborta y tira
/// el proceso): todas pasan por un turno único y los tramos van en serie.
/// </para>
/// <para>
/// Las consultas siempre filtran por <c>day</c> (la tabla está particionada),
/// cruzan con la nómina (por DataOrb en YOIGO y MASMOVIL, por la extensión
/// Avaya en JAZZTEL y ORANGE), y equipo y tipología salen de la misma
/// consulta para que cuadren entre sí. Solo la de no solucionadas lee
/// columnas de texto.
/// </para>
/// </remarks>
public sealed class FuenteBigQuery : IFuenteNoSolucion
{
    /// <summary>Turno único de proceso para hablar con el driver.</summary>
    private static readonly SemaphoreSlim Turno = new(1, 1);

    /// <summary>
    /// Las llamadas de Call Bogotá: YOIGO y MASMOVIL van con la ubicación
    /// <c>JAZZBOG</c>; JAZZTEL y ORANGE, con <c>JZZ_BOGOTA</c>.
    /// </summary>
    internal const string Universo =
        "channel = 'Call' AND ((brand IN ('YOIGO','MASMOVIL') AND serviceProviderLocation = 'JAZZBOG')"
        + " OR (brand IN ('JAZZTEL','ORANGE') AND serviceProviderLocation = 'JZZ_BOGOTA'))";

    /// <summary>Rúbrica, en el mismo orden que <see cref="Ensamblador.RubNom"/>.</summary>
    private static readonly string[] ColumnasRubrica =
    {
        "openingGreeting_rating", "issueDiscovery_rating", "needAssessment_rating",
        "acknowledgementStatement_rating", "objectionHandling_rating", "clearLanguage_rating",
        "closingGreeting_rating", "confirmResolution_rating", "problemSolvingStatements_rating",
    };

    /// <summary>Valor de cada respuesta de la rúbrica; cualquier otra (NA, NULL) vale -1.</summary>
    private static readonly Dictionary<string, int> ValorRubrica = new()
    {
        ["yes"] = 1, ["no"] = 0, ["notApplicable"] = 2,
    };

    /// <summary>Teléfonos que se enmascaran en el texto, el mismo patrón que el informe.</summary>
    private const string RegexTelefono =
        @"(?:\+?\s?(?:34|57)[\s.\-]?)?\b[6-9]\d{2}[\s.\-]?\d{3}[\s.\-]?\d{3}\b|\b3\d{2}[\s.\-]?\d{3}[\s.\-]?\d{4}\b|\b\d{9,}\b";

    /// <summary>Largo máximo del resumen de cada llamada.</summary>
    private const int LargoMaxTexto = 2000;

    /// <summary>Errores del driver que se reintentan.</summary>
    private static readonly string[] ErroresTransitorios =
    {
        "Failure when receiving", "timed out", "Timeout", "Couldn't connect",
        "Connection reset", "SSL", "HTTP code 5", "backendError", "rateLimitExceeded",
    };

    /// <summary>
    /// La fila más reciente de la nómina por cada llave con la que llega el
    /// agente en <c>primaryAgentId</c>: su DataOrb (YOIGO y MASMOVIL) o su
    /// extensión Avaya (JAZZTEL y ORANGE). No se confunden: un DataOrb tiene
    /// 36 caracteres y una extensión, 6 o 7. Una extensión que la nómina tuvo
    /// asignada a dos personas se le da a la más reciente.
    /// </summary>
    internal const string NominaCte = $$"""
        nomina AS (
          SELECT clave, Super, Team, Agente,
                 ROW_NUMBER() OVER (PARTITION BY clave ORDER BY Fecha DESC) AS rn
          FROM (
            SELECT LOWER(DataOrb) AS clave, Fecha, Super, Team, Agente
            FROM `{{Ensamblador.TablaNomina}}`
            WHERE DataOrb IS NOT NULL
            UNION ALL
            SELECT LOWER(TRIM(Avaya)) AS clave, Fecha, Super, Team, Agente
            FROM `{{Ensamblador.TablaNomina}}`
            WHERE Avaya IS NOT NULL AND TRIM(Avaya) != ''
          )
        )
        """;

    /// <summary>El cruce de cada llamada con su fila de la nómina.</summary>
    internal const string CruceNomina = "LEFT JOIN nomina n ON LOWER(TRIM(u.primaryAgentId)) = n.clave AND n.rn = 1";

    private readonly string _cadenaOdbc;
    private readonly int _ventana;
    private readonly int _tramo;
    private readonly int _diasProvisionales;
    private readonly string _rutaTablaImpedimentos;
    private readonly Func<DateOnly> _hoy;
    private readonly ILogger<FuenteBigQuery>? _log;
    private readonly SemaphoreSlim _construyendo = new(1, 1);
    private readonly Lazy<(Dictionary<string, int> Tabla, Dictionary<string, int> Conjuntos)> _impedimentos;

    public FuenteBigQuery(
        string cadenaOdbc, int ventana, int tramo, int diasProvisionales,
        string rutaTablaImpedimentos, ILogger<FuenteBigQuery>? log = null, Func<DateOnly>? hoy = null)
    {
        _cadenaOdbc = cadenaOdbc;
        _ventana = ventana;
        _tramo = tramo;
        _diasProvisionales = diasProvisionales;
        _rutaTablaImpedimentos = rutaTablaImpedimentos;
        _log = log;
        _hoy = hoy ?? (() => DateOnly.FromDateTime(DateTime.Now));
        _impedimentos = new Lazy<(Dictionary<string, int>, Dictionary<string, int>)>(CargarTablaImpedimentos);
        Ruta = $"BigQuery (ventana de {ventana} días, una sola descarga)";
    }

    /// <summary>Descripción de la fuente que enseña <c>/estado</c>.</summary>
    public string Ruta { get; }

    public EstadoConstruccion Estado { get; } = new();

    /// <summary>
    /// Qué llamadas trae el cubo. Va en la firma: si cambia el universo, el
    /// cubo guardado no coincide y se reconstruye.
    /// </summary>
    private const string MarcasDelUniverso = "ygmm-jzor";

    /// <summary>
    /// Firma de la caché en disco: no depende de la fecha, así que el cubo
    /// guardado se reutiliza al reiniciar. Lleva la versión del formato y el
    /// universo: un cubo guardado con otro formato u otras marcas no coincide
    /// y se reconstruye.
    /// </summary>
    public string Firma() => $"ventana-unica-{_ventana}-v{Ensamblador.VersionCubo}-{MarcasDelUniverso}";

    private static string Ahora() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>De <c>ventana</c> días atrás hasta ayer: el día en curso está a medias.</summary>
    public (DateOnly Desde, DateOnly Hasta) RangoObjetivo()
    {
        var hasta = _hoy().AddDays(-1);
        return (hasta.AddDays(-(_ventana - 1)), hasta);
    }

    /// <summary>La ventana partida en rangos de como mucho <c>tramo</c> días: una consulta por rango.</summary>
    public List<(string Desde, string Hasta)> Tramos()
    {
        var (desde, hasta) = RangoObjetivo();
        var salida = new List<(string, string)>();
        var ini = desde;
        while (ini <= hasta)
        {
            var fin = ini.AddDays(_tramo - 1);
            if (fin > hasta) fin = hasta;
            salida.Add((ini.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), fin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            ini = fin.AddDays(1);
        }
        return salida;
    }

    /// <summary>
    /// Lee la tabla de etiquetas de impedimento a categorías (el mismo fichero
    /// que usa el backend en Python). Sin fichero, toda etiqueta cuenta como
    /// «Otro impedimento».
    /// </summary>
    private (Dictionary<string, int> Tabla, Dictionary<string, int> Conjuntos) CargarTablaImpedimentos()
    {
        try
        {
            using var flujo = File.OpenRead(_rutaTablaImpedimentos);
            using var doc = JsonDocument.Parse(flujo);
            var raiz = doc.RootElement;
            return (Leer(raiz, "tabla"), Leer(raiz, "conjuntos"));
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            _log?.LogWarning(ex, "No se pudo leer la tabla de impedimentos {Ruta}", _rutaTablaImpedimentos);
            return (new Dictionary<string, int>(), new Dictionary<string, int>());
        }

        static Dictionary<string, int> Leer(JsonElement raiz, string clave)
        {
            var salida = new Dictionary<string, int>();
            if (raiz.TryGetProperty(clave, out var o) && o.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in o.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.Number) salida[p.Name] = p.Value.GetInt32();
                }
            }
            return salida;
        }
    }

    /// <summary>Descarga la ventana completa y devuelve (cubo, cuadre). Tarda alrededor de un minuto.</summary>
    public async Task<(Cubo Cubo, List<long> Cuadre)> CargarCuboAsync(int ventana, CancellationToken ct)
    {
        await _construyendo.WaitAsync(ct);
        Estado.EnCurso = true;
        Estado.UltimoInicio = Ahora();
        Estado.UltimoError = null;
        try
        {
            var tramos = Tramos();
            var docs = new Dictionary<string, DocCrudo>();
            for (var i = 0; i < tramos.Count; i++)
            {
                var (a, b) = tramos[i];
                Estado.Progreso = $"tramo {i + 1} de {tramos.Count} ({a} a {b})";
                foreach (var (dia, doc) in await ExtraerRangoAsync(a, b, ct)) docs[dia] = doc;
            }

            // Los días sin filas (festivo, caída) entran vacíos: cuentan para
            // la mediana de días parciales.
            var (desde, hasta) = RangoObjetivo();
            var ahora = Ahora();
            for (var d = desde; d <= hasta; d = d.AddDays(1))
            {
                var iso = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                docs.TryAdd(iso, new DocCrudo { Dia = iso, Extraido = ahora });
            }

            if (!docs.Values.Any(doc => doc.Base.Count > 0))
            {
                throw new SinDatos("BigQuery no devolvió ninguna llamada en la ventana.");
            }

            var (tabla, conjuntos) = _impedimentos.Value;
            var cubo = Ensamblador.Ensamblar(docs.Values, tabla, _diasProvisionales, conjuntos);

            Estado.UltimoResultado = new ResultadoConstruccion(docs.Count, tramos.Count);
            Estado.UltimoFin = Ahora();
            Estado.Progreso = null;
            return (cubo, cubo.Meta.Cuadre ?? new List<long>());
        }
        catch (Exception ex)
        {
            Estado.UltimoError = ex.GetType().Name + ": " + ex.Message;
            Estado.UltimoFin = Ahora();
            Estado.Progreso = null;
            throw;
        }
        finally
        {
            Estado.EnCurso = false;
            _construyendo.Release();
        }
    }

    // ------------------------------------------------------------------
    // Las consultas
    // ------------------------------------------------------------------

    /// <summary>Valida una fecha antes de ponerla como literal en el SQL.</summary>
    private static string Fecha(string x)
        => DateOnly.ParseExact(x, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Día × marca × dirección × servicio × equipo × agente × N2 × N3. De aquí salen eq y tp.</summary>
    public static string SqlBase(string desde, string hasta) => $$"""
        WITH {{NominaCte}},
        universo AS (
          SELECT day, brand, channelDirection, service, primaryAgentId,
                 interactionClassification_interactionBusinessScope AS n2,
                 interactionScopeDetail_classification AS n3,
                 enh_Resolution_request
          FROM `{{Ensamblador.TablaGamma}}`
          WHERE day BETWEEN '{{Fecha(desde)}}' AND '{{Fecha(hasta)}}' AND {{Universo}}
        )
        SELECT CAST(u.day AS STRING) AS day, u.brand,
               IFNULL(u.channelDirection, 'SIN DIRECCION') AS direccion,
               IFNULL(u.service, 'SIN SERVICIO') AS servicio,
               IFNULL(n.Super, 'SIN ASIGNAR') AS supervisor,
               IFNULL(n.Team, 'SIN ASIGNAR') AS tl,
               IFNULL(n.Agente, 'SIN ASIGNAR') AS agente,
               IFNULL(u.n2, 'SIN TIPIFICAR') AS n2,
               IFNULL(u.n3, 'SIN TIPIFICAR') AS n3,
               CAST(n.clave IS NOT NULL AS INT64) AS cruzado,
               COUNT(*) AS llamadas,
               COUNTIF(u.enh_Resolution_request = 1) AS sol,
               COUNTIF(u.enh_Resolution_request = 2) AS nosol
        FROM universo u
        {{CruceNomina}}
        GROUP BY 1, 2, 3, 4, 5, 6, 7, 8, 9, 10
        """;

    /// <summary>
    /// Una fila por llamada de sinAccesoInternet con su equipo, avería,
    /// señales, impedimentos y rúbrica. Sin textos. Desde la v3 del cubo lleva
    /// además el <c>conversationId</c> y la llamada física (el
    /// <c>externalConversationId</c> sin su sufijo <c>_N</c>, que numera los tramos
    /// de una llamada transferida), para la causa de cada no solucionada y el
    /// control de repetidas.
    /// </summary>
    public static string SqlInternet(string desde, string hasta)
    {
        var rubrica = string.Join(",\n       ", ColumnasRubrica.Select((c, i) => $"IFNULL(u.{c}, 'NA') AS r{i}"));
        return $$"""
        WITH {{NominaCte}},
        u AS (
          SELECT *
          FROM `{{Ensamblador.TablaGamma}}`
          WHERE day BETWEEN '{{Fecha(desde)}}' AND '{{Fecha(hasta)}}' AND {{Universo}}
            AND interactionScopeDetail_classification = 'sinAccesoInternet'
        )
        SELECT CAST(u.day AS STRING) AS day, u.brand,
               IFNULL(u.service, 'SIN SERVICIO') AS servicio,
               IFNULL(n.Super, 'SIN ASIGNAR') AS supervisor,
               IFNULL(n.Team, 'SIN ASIGNAR') AS tl,
               IFNULL(n.Agente, 'SIN ASIGNAR') AS agente,
               IFNULL(u.enh_TypN1_Techsupport, 'SIN TICKET TECNICO') AS n4,
               IFNULL(u.enh_TypN2_Techsupport, 'SIN TICKET TECNICO') AS n5,
               IFNULL(u.enh_Resolution_request, 0) AS resultado,
               CASE WHEN u.enh_Has_Techsupport_Bool THEN 1 ELSE 0 END AS ticket,
               CASE WHEN u.actionItemAnalysis_backOfficeEscalation IS NULL THEN -1
                    WHEN u.actionItemAnalysis_backOfficeEscalation THEN 1 ELSE 0 END AS escalado_bo,
               IFNULL(u.enh_Redial_72h, -1) AS rellamada72,
               IFNULL(u.tasks_EventCheck_rating, 'NA') AS evento,
               IFNULL(u.tasks_CompletionCheck_rating, 'NA') AS cierre,
               ARRAY_TO_STRING(u.resolution_resolutionImpediments_resolutionImpedimentsTopicGroup, '||') AS impedimentos,
               {{rubrica}},
               IFNULL(CAST(u.conversationId AS STRING), '') AS conversacion,
               IFNULL(REGEXP_REPLACE(CAST(u.externalConversationId AS STRING), r'_\d+$', ''), '') AS llamada
        FROM u
        {{CruceNomina}}
        """;
    }

    /// <summary>
    /// Todas las llamadas entrantes no solucionadas, con su id
    /// (<c>conversationId</c>), su equipo, sus etiquetas de impedimento y el
    /// resumen de la conversación con los teléfonos enmascarados.
    /// </summary>
    public static string SqlNoSolucionadas(string desde, string hasta) => $$"""
        WITH {{NominaCte}},
        u AS (
          SELECT day, brand, service, primaryAgentId, conversationId, externalConversationId,
                 IFNULL(interactionClassification_interactionBusinessScope, 'SIN TIPIFICAR') AS n2,
                 IFNULL(interactionScopeDetail_classification, 'SIN TIPIFICAR') AS n3,
                 IFNULL(enh_TypN1_Techsupport, 'SIN TICKET TECNICO') AS n4,
                 ARRAY_TO_STRING(resolution_resolutionImpediments_resolutionImpedimentsTopicGroup, '||') AS impedimentos,
                 COALESCE(NULLIF(TRIM(interactionScopeDetail_assessment), ''),
                          contactReason_contactReasonSummary) AS bruto
          FROM `{{Ensamblador.TablaGamma}}`
          WHERE day BETWEEN '{{Fecha(desde)}}' AND '{{Fecha(hasta)}}' AND {{Universo}}
            AND channelDirection = 'Inbound' AND enh_Resolution_request = 2
        )
        SELECT CAST(u.day AS STRING) AS day, u.brand,
               IFNULL(u.service, 'SIN SERVICIO') AS servicio,
               IFNULL(n.Super, 'SIN ASIGNAR') AS supervisor,
               IFNULL(n.Team, 'SIN ASIGNAR') AS tl,
               IFNULL(n.Agente, 'SIN ASIGNAR') AS agente,
               u.n2, u.n3,
               u.n4, u.impedimentos,
               IFNULL(CAST(u.conversationId AS STRING), '') AS conversacion,
               IFNULL(CAST(u.externalConversationId AS STRING), '') AS conversacion_externa,
               IFNULL(SUBSTR(REGEXP_REPLACE(REGEXP_REPLACE(u.bruto, r'[\r\n]+', ' '),
                      r'{{RegexTelefono}}', '[teléfono]'), 1, {{LargoMaxTexto}}), '') AS texto
        FROM u
        {{CruceNomina}}
        """;

    // ------------------------------------------------------------------
    // El cliente ODBC: turno único, timeout y tres reintentos
    // ------------------------------------------------------------------

    private Task<List<Dictionary<string, object?>>> ConsultarAsync(string sql, CancellationToken ct)
        => ConsultarAsync(_cadenaOdbc, sql, _log, ct);

    /// <summary>
    /// Lanza una consulta por ODBC y devuelve sus filas. Todas las consultas
    /// del proceso a BigQuery pasan por aquí (también las del CDM de
    /// Rellamada): el driver no admite dos a la vez.
    /// </summary>
    internal static async Task<List<Dictionary<string, object?>>> ConsultarAsync(
        string cadenaOdbc, string sql, ILogger? log, CancellationToken ct)
    {
        const int reintentos = 3;
        const double espera = 2.0;
        string? ultimo = null;

        for (var intento = 1; intento <= reintentos; intento++)
        {
            await Turno.WaitAsync(ct);
            try
            {
                using var cn = new OdbcConnection(cadenaOdbc) { ConnectionTimeout = 60 };
                await cn.OpenAsync(ct);
                using var cmd = cn.CreateCommand();
                cmd.CommandText = sql;
                cmd.CommandTimeout = 300;
                using var rd = await cmd.ExecuteReaderAsync(ct);

                var columnas = Enumerable.Range(0, rd.FieldCount).Select(rd.GetName).ToArray();
                var filas = new List<Dictionary<string, object?>>();
                while (await rd.ReadAsync(ct))
                {
                    var fila = new Dictionary<string, object?>(columnas.Length);
                    for (var i = 0; i < columnas.Length; i++)
                    {
                        fila[columnas[i]] = rd.IsDBNull(i) ? null : rd.GetValue(i);
                    }
                    filas.Add(fila);
                }
                return filas;
            }
            catch (OdbcException ex)
            {
                ultimo = ex.Message;
                if (!ErroresTransitorios.Any(t => ultimo.Contains(t, StringComparison.Ordinal)))
                {
                    throw new ErrorBigQuery(Limpiar(ultimo));
                }
                log?.LogWarning("BigQuery: error transitorio en el intento {Intento}: {Error}", intento, Limpiar(ultimo));
            }
            finally
            {
                Turno.Release();
            }
            await Task.Delay(TimeSpan.FromSeconds(espera * intento), ct);
        }
        throw new ErrorBigQuery($"Sin respuesta de BigQuery tras {reintentos} intentos: {Limpiar(ultimo)}");
    }

    /// <summary>El mensaje del driver sin el prefijo de la API REST, acotado a 400 caracteres.</summary>
    private static string Limpiar(string? mensaje)
    {
        mensaje ??= "";
        var i = mensaje.IndexOf("REST API:", StringComparison.Ordinal);
        var texto = (i >= 0 ? mensaje[(i + 9)..] : mensaje).Trim();
        return texto.Length > 400 ? texto[..400] : texto;
    }

    private static string Texto(Dictionary<string, object?> fila, string clave)
        => fila.TryGetValue(clave, out var v) && v is not null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";

    private static int Entero(Dictionary<string, object?> fila, string clave)
        => fila.TryGetValue(clave, out var v) && v is not null ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : 0;

    private static List<string> Etiquetas(string texto)
        => texto.Split("||", StringSplitOptions.None).Where(x => x.Length > 0).ToList();

    /// <summary>
    /// Ejecuta las tres consultas de un rango y devuelve <c>{dia: doc}</c> para
    /// cada día con datos, con las filas ordenadas como en el original.
    /// </summary>
    public async Task<Dictionary<string, DocCrudo>> ExtraerRangoAsync(string desde, string hasta, CancellationToken ct)
    {
        var @base = await ConsultarAsync(SqlBase(desde, hasta), ct);
        var internet = await ConsultarAsync(SqlInternet(desde, hasta), ct);
        var llamadas = await ConsultarAsync(SqlNoSolucionadas(desde, hasta), ct);
        return Agrupar(@base, internet, llamadas, Ahora());
    }

    /// <summary>
    /// Reparte las filas de las tres consultas por día y las ordena: base por
    /// la fila entera, internet por sus ocho primeras columnas y llamadas por
    /// (id, id_externo). El orden es estable, como <c>list.sort</c>.
    /// </summary>
    public static Dictionary<string, DocCrudo> Agrupar(
        List<Dictionary<string, object?>> @base, List<Dictionary<string, object?>> internet,
        List<Dictionary<string, object?>> llamadas, string extraido)
    {
        var docs = new Dictionary<string, DocCrudo>();
        DocCrudo Doc(string dia)
        {
            if (!docs.TryGetValue(dia, out var d)) docs[dia] = d = new DocCrudo { Dia = dia, Extraido = extraido };
            return d;
        }

        foreach (var r in @base)
        {
            Doc(Texto(r, "day")).Base.Add(new FilaBase(
                Texto(r, "brand"), Texto(r, "direccion"), Texto(r, "servicio"), Texto(r, "supervisor"),
                Texto(r, "tl"), Texto(r, "agente"), Texto(r, "n2"), Texto(r, "n3"),
                Entero(r, "cruzado"), Entero(r, "llamadas"), Entero(r, "sol"), Entero(r, "nosol")));
        }
        foreach (var r in internet)
        {
            Doc(Texto(r, "day")).Internet.Add(new FilaInternet(
                Texto(r, "brand"), Texto(r, "servicio"), Texto(r, "supervisor"), Texto(r, "tl"), Texto(r, "agente"),
                Texto(r, "n4"), Texto(r, "n5"),
                Entero(r, "resultado"), Entero(r, "ticket"), Entero(r, "escalado_bo"), Entero(r, "rellamada72"),
                Texto(r, "evento"), Texto(r, "cierre"),
                Etiquetas(Texto(r, "impedimentos")),
                Enumerable.Range(0, ColumnasRubrica.Length)
                    .Select(i => ValorRubrica.GetValueOrDefault(Texto(r, "r" + i.ToString(CultureInfo.InvariantCulture)), -1))
                    .ToList(),
                Texto(r, "conversacion"), Texto(r, "llamada")));
        }
        foreach (var r in llamadas)
        {
            Doc(Texto(r, "day")).Llamadas.Add(new FilaLlamada(
                Texto(r, "brand"), Texto(r, "servicio"), Texto(r, "supervisor"), Texto(r, "tl"), Texto(r, "agente"),
                Texto(r, "n2"), Texto(r, "n3"), Texto(r, "n4"), Etiquetas(Texto(r, "impedimentos")),
                Texto(r, "conversacion"), Texto(r, "conversacion_externa"), Texto(r, "texto")));
        }

        foreach (var d in docs.Values)
        {
            var b = d.Base.OrderBy(x => x, Comparer<FilaBase>.Create(CompararBase)).ToList();
            var i = d.Internet.OrderBy(x => x, Comparer<FilaInternet>.Create(CompararInternet)).ToList();
            var l = d.Llamadas.OrderBy(x => x.Id, Ensamblador.TextoPython)
                              .ThenBy(x => x.IdExterno, Ensamblador.TextoPython).ToList();
            d.Base.Clear(); d.Base.AddRange(b);
            d.Internet.Clear(); d.Internet.AddRange(i);
            d.Llamadas.Clear(); d.Llamadas.AddRange(l);
        }
        return docs;
    }

    private static int CompararBase(FilaBase x, FilaBase y)
    {
        var c = Ensamblador.CompararTexto(x.B, y.B); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.Dir, y.Dir); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.S, y.S); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.U, y.U); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.T, y.T); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.A, y.A); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.N2, y.N2); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.N3, y.N3); if (c != 0) return c;
        c = x.Cruzado.CompareTo(y.Cruzado); if (c != 0) return c;
        c = x.Ll.CompareTo(y.Ll); if (c != 0) return c;
        c = x.Sol.CompareTo(y.Sol); if (c != 0) return c;
        return x.Nosol.CompareTo(y.Nosol);
    }

    /// <summary>Orden de las filas de internet: (b, s, u, t, a, n4, n5, res).</summary>
    private static int CompararInternet(FilaInternet x, FilaInternet y)
    {
        var c = Ensamblador.CompararTexto(x.B, y.B); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.S, y.S); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.U, y.U); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.T, y.T); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.A, y.A); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.N4, y.N4); if (c != 0) return c;
        c = Ensamblador.CompararTexto(x.N5, y.N5); if (c != 0) return c;
        return x.Res.CompareTo(y.Res);
    }
}
