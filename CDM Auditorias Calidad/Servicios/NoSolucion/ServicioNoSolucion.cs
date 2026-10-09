using System.Globalization;
using CDM_Auditorias_Calidad.Servicios.Configuracion;
using Microsoft.Extensions.Options;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>
/// La fuente, la caché y la construcción en segundo plano de No solución.
/// </summary>
/// <remarks>
/// Traído de ranking-mvc (<c>RankingMvc/Servicios/NoSolucion</c>) el 05-10-2026; el motor es el
/// mismo, solo cambia de dónde lee la configuración (<see cref="OpcionesNoSolucion"/>).
/// Ninguna petición espera a BigQuery: sin cubo, la pantalla enseña «preparando» y el cubo se
/// construye en segundo plano. Con cubo, si tiene más de <c>NoSolucion:RefrescoHoras</c> horas
/// se trae de nuevo en segundo plano mientras se sigue sirviendo el que hay.
/// </remarks>
public sealed class ServicioNoSolucion
{
    /// <summary>Tiempo mínimo entre dos <c>POST /actualizar</c>.</summary>
    public const int MinSegundosEntreRecargas = 30;

    /// <summary>Tiempo mínimo entre dos renovaciones automáticas, por si la anterior falló.</summary>
    public static readonly TimeSpan ReintentoRenovacion = TimeSpan.FromMinutes(15);

    private readonly ILogger<ServicioNoSolucion> _log;
    private readonly object _cerrojo = new();
    private Task? _construccion;
    private DateTime _ultimaRecarga = DateTime.MinValue;
    private DateTime _ultimoIntentoRenovacion = DateTime.MinValue;

    public ServicioNoSolucion(IOptions<OpcionesNoSolucion> opciones, IWebHostEnvironment entorno,
        ILogger<ServicioNoSolucion> log, ILoggerFactory registros)
    {
        _log = log;
        var op = opciones.Value;
        // Las rutas relativas, a la carpeta de la aplicación (en desarrollo, la del proyecto).
        string Ruta(string r) => Path.IsPathRooted(r) ? r : Path.GetFullPath(Path.Combine(entorno.ContentRootPath, r));

        RefrescoHoras = op.RefrescoHoras;
        Fuente = new FuenteBigQuery(
            op.Odbc, op.Ventana, op.Tramo, op.DiasProvisionales,
            Ruta(op.RutaTablaImpedimentos), registros.CreateLogger<FuenteBigQuery>());
        Cache = new CacheNoSolucion(Fuente, Ruta(op.RutaCache), op.Ventana, log: log);
    }

    public FuenteBigQuery Fuente { get; }
    public CacheNoSolucion Cache { get; }

    /// <summary>Horas tras las que el cubo se renueva al entrar alguien.</summary>
    public double RefrescoHoras { get; }

    public bool ConstruyendoAhora
    {
        get { lock (_cerrojo) { return _construccion is { IsCompleted: false }; } }
    }

    /// <summary>Lanza la construcción del cubo en segundo plano si no hay otra en marcha.</summary>
    public void AsegurarConstruccion()
    {
        lock (_cerrojo)
        {
            if (_construccion is { IsCompleted: false }) return;
            _construccion = Task.Run(async () =>
            {
                try
                {
                    await Cache.ForzarRecargaAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "No solución: la construcción del cubo falló");
                }
            });
        }
    }

    /// <summary>Horas desde que se trajeron los datos del cubo, o nulo si no se sabe.</summary>
    public static double? EdadHoras(Cubo cubo, DateTime? ahora = null)
    {
        var generado = cubo.Meta.Generado;
        if (string.IsNullOrEmpty(generado)) return null;
        if (!DateTime.TryParse(generado, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return null;
        return ((ahora ?? DateTime.Now) - t).TotalHours;
    }

    /// <summary>
    /// Lanza la renovación en segundo plano si el cubo es más viejo que
    /// <see cref="RefrescoHoras"/>, no hay otra en marcha y no se intentó en
    /// los últimos 15 minutos.
    /// </summary>
    public void RenovarSiViejo(Cubo cubo)
    {
        var edad = EdadHoras(cubo);
        if (edad is null || edad < RefrescoHoras) return;
        lock (_cerrojo)
        {
            if (_construccion is { IsCompleted: false }) return;
            var ahora = DateTime.UtcNow;
            if (ahora - _ultimoIntentoRenovacion < ReintentoRenovacion) return;
            _ultimoIntentoRenovacion = ahora;
        }
        AsegurarConstruccion();
    }

    /// <summary>
    /// Lo que hace <c>POST /actualizar</c>. Devuelve false si ya hay una
    /// construcción en curso o acaba de terminar una.
    /// </summary>
    public bool PedirActualizacion()
    {
        lock (_cerrojo)
        {
            var enCurso = _construccion is { IsCompleted: false } || Fuente.Estado.EnCurso;
            if (enCurso || (DateTime.UtcNow - _ultimaRecarga).TotalSeconds < MinSegundosEntreRecargas)
            {
                return false;
            }
            _ultimaRecarga = DateTime.UtcNow;
        }
        AsegurarConstruccion();
        return true;
    }

    /// <summary>El cuerpo del 503 mientras se construye el cubo.</summary>
    public Dictionary<string, object?> Progreso()
    {
        var estado = Fuente.Estado;
        return new Dictionary<string, object?>
        {
            ["preparando"] = true,
            ["progreso"] = estado.Progreso,
            ["error"] = estado.UltimoError,
            ["detail"] = "Se están trayendo los datos de los últimos 3 meses. La primera vez tarda alrededor de un minuto.",
        };
    }
}
