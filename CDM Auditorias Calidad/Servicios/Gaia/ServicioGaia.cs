using System.Text.Json;
using CDM_Auditorias_Calidad.Servicios.Configuracion;
using Microsoft.Extensions.Options;

namespace CDM_Auditorias_Calidad.Servicios.Gaia;

/// <summary>
/// Los datos de GAIA Formación: la caché en disco, la vigilancia del Excel y la recarga en segundo
/// plano. Ninguna petición espera a BigQuery: sin datos, la vista enseña «preparando».
/// </summary>
/// <remarks>
/// Se vuelve a traer todo (unos 20 s) cuando cambia la fecha de modificación del Excel de nómina,
/// cuando los datos tienen más de <see cref="OpcionesGaia.RefrescoHoras"/> horas y con «Actualizar».
/// Así basta con guardar el Excel: en unos minutos (<see cref="OpcionesGaia.MinutosRevision"/>) la
/// web lo nota sola.
/// </remarks>
public sealed class ServicioGaia
{
    /// <summary>Tiempo mínimo entre dos «Actualizar».</summary>
    public const int MinSegundosEntreRecargas = 30;

    /// <summary>Tras un fallo, no se reintenta solo hasta pasado este tiempo.</summary>
    public static readonly TimeSpan ReintentoTrasFallo = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions OpcionesJson = new() { WriteIndented = false };

    private readonly OpcionesGaia _op;
    private readonly string _rutaCache;
    private readonly string _rutaPalabras;
    private readonly FuenteGaia _fuente;
    private readonly ILogger<ServicioGaia> _log;
    private readonly object _cerrojo = new();
    private Task? _carga;
    private DateTime _ultimaPeticion = DateTime.MinValue;
    private DateTime _ultimoFallo = DateTime.MinValue;
    private bool _cacheLeida;

    public ServicioGaia(IOptions<OpcionesGaia> opciones, IWebHostEnvironment entorno, ILogger<ServicioGaia> log)
    {
        _op = opciones.Value;
        _log = log;
        string Ruta(string r) => Path.IsPathRooted(r) ? r : Path.GetFullPath(Path.Combine(entorno.ContentRootPath, r));
        _rutaCache = Ruta(_op.RutaCache);
        _rutaPalabras = Ruta(_op.RutaPalabras);
        _fuente = new FuenteGaia(_op.Odbc, Path.Combine(entorno.ContentRootPath, "Consultas"), _op.AgentesPorConsulta, log);
    }

    /// <summary>Los datos que se sirven ahora (nulo hasta la primera carga).</summary>
    public DatosGaia? Actual { get; private set; }

    /// <summary>El último error al leer el Excel o al traer de BigQuery (se borra al cargar bien).</summary>
    public string? UltimoError { get; private set; }

    public double RefrescoHoras => _op.RefrescoHoras;

    public bool Cargando
    {
        get { lock (_cerrojo) { return _carga is { IsCompleted: false }; } }
    }

    /// <summary>
    /// Lo que se hace al entrar alguien y cada pocos minutos: lee la caché la primera vez y lanza
    /// la recarga si el Excel cambió, si los datos son viejos o si no hay datos.
    /// </summary>
    public void Revisar()
    {
        LeerCacheUnaVez();
        var motivo = MotivoRecarga();
        if (motivo is null) return;
        lock (_cerrojo)
        {
            if (_carga is { IsCompleted: false }) return;
            if (DateTime.UtcNow - _ultimoFallo < ReintentoTrasFallo) return;
        }
        _log.LogInformation("GAIA: recarga ({Motivo})", motivo);
        LanzarCarga();
    }

    /// <summary>«Actualizar» de la vista. Falso si ya hay una carga en marcha o acaba de pedirse.</summary>
    public bool PedirActualizacion()
    {
        lock (_cerrojo)
        {
            if (_carga is { IsCompleted: false } || (DateTime.UtcNow - _ultimaPeticion).TotalSeconds < MinSegundosEntreRecargas)
            {
                return false;
            }
            _ultimaPeticion = DateTime.UtcNow;
            _ultimoFallo = DateTime.MinValue;
        }
        LanzarCarga();
        return true;
    }

    /// <summary>Por qué hay que volver a traer los datos, o nulo si no hace falta.</summary>
    private string? MotivoRecarga()
    {
        var datos = Actual;
        if (datos is null) return "sin datos";
        if ((DateTime.Now - datos.Generado).TotalHours >= _op.RefrescoHoras) return "datos de más de " + _op.RefrescoHoras + " h";
        try
        {
            if (File.Exists(_op.RutaExcel) && File.GetLastWriteTime(_op.RutaExcel) != datos.ExcelModificado)
            {
                return "el Excel de nómina cambió";
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning("GAIA: no se pudo mirar el Excel: {Error}", ex.Message);
        }
        return null;
    }

    private void LanzarCarga()
    {
        lock (_cerrojo)
        {
            if (_carga is { IsCompleted: false }) return;
            _carga = Task.Run(CargarAsync);
        }
    }

    private async Task CargarAsync()
    {
        try
        {
            var nomina = LectorNominaGaia.Leer(_op.RutaExcel, _op.Hoja);
            var palabras = PalabrasGaia.Leer(_rutaPalabras);
            var datos = await _fuente.TraerAsync(nomina, palabras, CancellationToken.None);
            Actual = datos;
            UltimoError = null;
            GuardarCache(datos);
            _log.LogInformation("GAIA: {Llamadas} llamadas de {Agentes} agentes ({Pares} días de formación)",
                datos.Llamadas.Count, datos.Agentes.Count, nomina.Pares);
        }
        catch (Exception ex)
        {
            UltimoError = ex.Message;
            lock (_cerrojo) { _ultimoFallo = DateTime.UtcNow; }
            _log.LogError(ex, "GAIA: la carga falló");
        }
    }

    private void LeerCacheUnaVez()
    {
        lock (_cerrojo)
        {
            if (_cacheLeida) return;
            _cacheLeida = true;
        }
        try
        {
            if (!File.Exists(_rutaCache)) return;
            using var flujo = File.OpenRead(_rutaCache);
            var datos = JsonSerializer.Deserialize<DatosGaia>(flujo, OpcionesJson);
            if (datos is { Version: DatosGaia.VersionActual }) Actual ??= datos;
        }
        catch (Exception ex)
        {
            _log.LogWarning("GAIA: la caché no se pudo leer y se vuelve a traer: {Error}", ex.Message);
        }
    }

    private void GuardarCache(DatosGaia datos)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_rutaCache)!);
            var temporal = _rutaCache + ".tmp";
            using (var flujo = File.Create(temporal))
            {
                JsonSerializer.Serialize(flujo, datos, OpcionesJson);
            }
            File.Move(temporal, _rutaCache, overwrite: true);
        }
        catch (Exception ex)
        {
            _log.LogWarning("GAIA: no se pudo guardar la caché: {Error}", ex.Message);
        }
    }
}

/// <summary>Mira cada pocos minutos si el Excel de nómina cambió (o si los datos son viejos) y recarga.</summary>
public sealed class RevisionGaia : BackgroundService
{
    private readonly ServicioGaia _gaia;
    private readonly TimeSpan _cada;

    public RevisionGaia(ServicioGaia gaia, IOptions<OpcionesGaia> opciones)
    {
        _gaia = gaia;
        _cada = TimeSpan.FromMinutes(Math.Max(1, opciones.Value.MinutosRevision));
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            _gaia.Revisar();
            try { await Task.Delay(_cada, ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
