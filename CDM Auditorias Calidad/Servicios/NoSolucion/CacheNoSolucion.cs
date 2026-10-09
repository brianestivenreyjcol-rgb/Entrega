using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>No hay cubo ni caché guardada: no hay nada que servir.</summary>
public sealed class SinDatos : Exception
{
    public SinDatos(string mensaje) : base(mensaje) { }
}

/// <summary>Cómo va la descarga de la fuente. Es lo que enseña <c>/estado</c> como <c>construccion</c>.</summary>
public sealed class EstadoConstruccion
{
    [JsonPropertyName("en_curso")] public bool EnCurso { get; set; }
    [JsonPropertyName("progreso")] public string? Progreso { get; set; }
    [JsonPropertyName("ultimo_inicio")] public string? UltimoInicio { get; set; }
    [JsonPropertyName("ultimo_fin")] public string? UltimoFin { get; set; }
    [JsonPropertyName("ultimo_resultado")] public ResultadoConstruccion? UltimoResultado { get; set; }
    [JsonPropertyName("ultimo_error")] public string? UltimoError { get; set; }

    public EstadoConstruccion Copia() => new()
    {
        EnCurso = EnCurso, Progreso = Progreso, UltimoInicio = UltimoInicio, UltimoFin = UltimoFin,
        UltimoResultado = UltimoResultado, UltimoError = UltimoError,
    };
}

public sealed record ResultadoConstruccion(
    [property: JsonPropertyName("dias")] int Dias,
    [property: JsonPropertyName("tramos")] int Tramos);

/// <summary>
/// De dónde salen los datos de No solución. Hoy, BigQuery por ODBC; el
/// resto del módulo no sabe cuál es.
/// </summary>
public interface IFuenteNoSolucion
{
    /// <summary>Texto para <c>/estado</c>: qué fuente es.</summary>
    string Ruta { get; }

    /// <summary>
    /// Identifica la versión de la fuente. Para la ventana única es
    /// constante a propósito: nada invalida la caché por el paso del tiempo.
    /// </summary>
    string Firma();

    EstadoConstruccion Estado { get; }

    /// <summary>Descarga la ventana completa y ensambla el cubo. Devuelve (cubo, cuadre).</summary>
    Task<(Cubo Cubo, List<long> Cuadre)> CargarCuboAsync(int ventana, CancellationToken ct);
}

/// <summary>
/// La caché de No solución en tres capas: el cubo en memoria, el cubo en
/// disco (<c>cache_nosolucion.json</c>, el mismo fichero que usa el backend
/// en Python, reutilizado solo si coinciden firma y ventana) y las respuestas
/// ya calculadas (LRU de 256 por versión de datos).
/// </summary>
/// <remarks>
/// Nunca descarga dentro de una petición: sin cubo lanza <see cref="SinDatos"/>
/// y la ruta responde 503 y construye en segundo plano. La descarga va fuera
/// del cerrojo, así que mientras dura se sigue sirviendo el cubo anterior; si
/// falla, se sigue con la última versión buena.
/// </remarks>
public sealed class CacheNoSolucion
{
    private static readonly JsonSerializerOptions FormatoDisco = new()
    {
        // json.dump(..., separators=(",", ":")): compacto y con acentos.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly IFuenteNoSolucion _fuente;
    private readonly string _rutaCache;
    private readonly int _ventana;
    private readonly int _maxRespuestas;
    private readonly ILogger? _log;
    private readonly object _cerrojo = new();

    private Cubo? _cubo;
    private string? _firma;
    private string? _version;
    private bool _intentadoDisco;

    // LRU de respuestas: clave -> (valor, nodo en la lista de recencia).
    private readonly Dictionary<string, (object Valor, LinkedListNode<string> Nodo)> _respuestas = new();
    private readonly LinkedList<string> _recencia = new();

    // Lo que enseña /estado.
    private string? _origen;
    private string? _cargadoEn;
    private double? _segundosCarga;
    private int _recargas;
    private int _aciertos;
    private int _fallos;
    private string? _ultimoError;
    private List<long>? _cuadre;

    public CacheNoSolucion(IFuenteNoSolucion fuente, string rutaCache, int ventana = 90, int maxRespuestas = 256, ILogger? log = null)
    {
        _fuente = fuente;
        _rutaCache = rutaCache;
        _ventana = ventana;
        _maxRespuestas = maxRespuestas;
        _log = log;
    }

    public IFuenteNoSolucion Fuente => _fuente;

    /// <summary>(cubo, versión). Lanza <see cref="SinDatos"/> si no hay cubo.</summary>
    public (Cubo Cubo, string Version) Actual()
    {
        lock (_cerrojo)
        {
            if (_cubo is null && !_intentadoDisco)
            {
                _intentadoDisco = true;
                CargarDesdeDisco();
            }
            if (_cubo is null) throw new SinDatos("Todavía no hay datos de No solución cargados.");
            return (_cubo, _version!);
        }
    }

    /// <summary>¿Hay cubo servible sin construir nada?</summary>
    public bool TieneCubo()
    {
        try { Actual(); return true; }
        catch (SinDatos) { return false; }
    }

    private void CargarDesdeDisco()
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var guardado = LeerDisco();
            var firma = _fuente.Firma();
            var usable = guardado is not null
                         && guardado.Ventana == _ventana
                         && guardado.Cubo is not null
                         && guardado.Firma == firma;
            if (!usable) return;

            Instalar(guardado!.Cubo!, guardado.Firma, guardado.Cuadre, "disco", reloj.Elapsed.TotalSeconds);
        }
        catch (Exception ex)
        {
            _ultimoError = ex.GetType().Name + ": " + ex.Message;
            _log?.LogWarning(ex, "No se pudo cargar la caché de No solución desde {Ruta}", _rutaCache);
        }
    }

    /// <summary>
    /// Relee la fuente aunque la firma no haya cambiado, y reescribe el
    /// disco. Es lo que hace <c>POST /actualizar</c> y la construcción en
    /// segundo plano.
    /// </summary>
    public async Task<string> ForzarRecargaAsync(CancellationToken ct)
    {
        var reloj = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var firma = _fuente.Firma();
            var (cubo, cuadre) = await _fuente.CargarCuboAsync(_ventana, ct);
            EscribirDisco(new FicheroCache
            {
                Firma = firma,
                Ventana = _ventana,
                Cuadre = cuadre,
                Guardado = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
                Cubo = cubo,
            });
            lock (_cerrojo)
            {
                Instalar(cubo, firma, cuadre, "fuente", reloj.Elapsed.TotalSeconds);
                return _version!;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (_cerrojo) { _ultimoError = ex.GetType().Name + ": " + ex.Message; }
            // Se sigue sirviendo la última versión buena, si la hay.
            throw;
        }
    }

    /// <summary>Llamar con el cerrojo tomado.</summary>
    private void Instalar(Cubo cubo, string? firma, List<long>? cuadre, string origen, double segundos)
    {
        _cubo = cubo;
        _firma = firma;
        _version = Version(firma, _ventana, cubo.Meta.Generado);
        _respuestas.Clear();
        _recencia.Clear();
        _origen = origen;
        _recargas++;
        _cargadoEn = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        _segundosCarga = Math.Round(segundos, 2);
        _ultimoError = null;
        _cuadre = cuadre;
    }

    /// <summary><c>sha1("firma|ventana|generado")[:12]</c>, como el original.</summary>
    public static string Version(string? firma, int ventana, string? generado)
    {
        var texto = $"{firma ?? "None"}|{ventana.ToString(CultureInfo.InvariantCulture)}|{generado ?? "None"}";
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant()[..12];
    }

    /// <summary>
    /// (valor, versión) de una vista, calculándola solo una vez por versión
    /// de datos.
    /// </summary>
    public (object Valor, string Version) Respuesta(string clave, Func<Cubo, object> calcular)
    {
        var (cubo, version) = Actual();
        var k = version + "|" + clave;

        lock (_cerrojo)
        {
            if (_respuestas.TryGetValue(k, out var entrada))
            {
                _recencia.Remove(entrada.Nodo);
                _recencia.AddLast(entrada.Nodo);
                _aciertos++;
                return (entrada.Valor, version);
            }
        }

        var valor = calcular(cubo);

        lock (_cerrojo)
        {
            // No guardar algo calculado sobre un cubo viejo.
            if (version == _version && !_respuestas.ContainsKey(k))
            {
                var nodo = _recencia.AddLast(k);
                _respuestas[k] = (valor, nodo);
                while (_respuestas.Count > _maxRespuestas && _recencia.First is not null)
                {
                    _respuestas.Remove(_recencia.First.Value);
                    _recencia.RemoveFirst();
                }
            }
            _fallos++;
        }
        return (valor, version);
    }

    /// <summary>Port de <c>resumen_estado</c>: de dónde salió el cubo, cuándo, aciertos…</summary>
    public Dictionary<string, object?> ResumenEstado()
    {
        lock (_cerrojo)
        {
            return new Dictionary<string, object?>
            {
                ["origen"] = _origen,
                ["cargado_en"] = _cargadoEn,
                ["segundos_carga"] = _segundosCarga,
                ["recargas"] = _recargas,
                ["aciertos"] = _aciertos,
                ["fallos"] = _fallos,
                ["ultimo_error"] = _ultimoError,
                ["cuadre"] = _cuadre,
                ["version"] = _version,
                ["firma"] = _firma,
                ["respuestas_en_cache"] = _respuestas.Count,
                ["ruta_fuente"] = _fuente.Ruta,
                ["ruta_cache"] = _rutaCache,
            };
        }
    }

    // ------------------------------------------------------------------
    // Disco
    // ------------------------------------------------------------------

    private sealed class FicheroCache
    {
        [JsonPropertyName("firma")] public string? Firma { get; set; }
        [JsonPropertyName("ventana")] public int Ventana { get; set; }
        [JsonPropertyName("cuadre")] public List<long>? Cuadre { get; set; }
        [JsonPropertyName("guardado")] public string? Guardado { get; set; }
        [JsonPropertyName("cubo")] public Cubo? Cubo { get; set; }
        [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    }

    private FicheroCache? LeerDisco()
    {
        if (!File.Exists(_rutaCache)) return null;
        try
        {
            using var flujo = File.OpenRead(_rutaCache);
            return JsonSerializer.Deserialize<FicheroCache>(flujo, FormatoDisco);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Escritura atómica: o el fichero viejo o el nuevo, nunca uno a medias.</summary>
    private void EscribirDisco(FicheroCache fichero)
    {
        var carpeta = Path.GetDirectoryName(_rutaCache);
        if (!string.IsNullOrEmpty(carpeta)) Directory.CreateDirectory(carpeta);

        var temporal = Path.Combine(carpeta ?? ".", Path.GetRandomFileName() + ".tmp");
        try
        {
            using (var flujo = File.Create(temporal))
            {
                JsonSerializer.Serialize(flujo, fichero, FormatoDisco);
            }
            if (File.Exists(_rutaCache)) File.Replace(temporal, _rutaCache, null);
            else File.Move(temporal, _rutaCache);
        }
        catch
        {
            try { File.Delete(temporal); } catch { }
            throw;
        }
    }
}
