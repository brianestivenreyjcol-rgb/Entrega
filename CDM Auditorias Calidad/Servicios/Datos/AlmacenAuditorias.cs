using CDM_Auditorias_Calidad.Models;

namespace CDM_Auditorias_Calidad.Servicios.Datos;

/// <summary>
/// Guarda en memoria la última carga de auditorías, como el modo importación de Power BI.
/// </summary>
/// <remarks>
/// La recarga la lanza <see cref="RecargaPeriodica"/> al arrancar y cada
/// <c>Auditorias:MinutosRecarga</c> minutos, o el botón «Actualizar datos». Si una recarga
/// falla se siguen sirviendo los datos anteriores y se guarda el error para enseñarlo.
/// </remarks>
public sealed class AlmacenAuditorias
{
    private readonly RepositorioAuditorias _repositorio;
    private readonly ILogger<AlmacenAuditorias> _log;
    private readonly SemaphoreSlim _cerrojo = new(1, 1);
    private volatile InstantaneaAuditorias? _actual;

    public AlmacenAuditorias(RepositorioAuditorias repositorio, ILogger<AlmacenAuditorias> log)
    {
        _repositorio = repositorio;
        _log = log;
    }

    public InstantaneaAuditorias? Actual => _actual;

    /// <summary>Mensaje de la última recarga fallida, o null si la última fue bien.</summary>
    public string? UltimoError { get; private set; }

    public DateTime? UltimoIntento { get; private set; }

    /// <summary>
    /// Los datos en memoria; si todavía no hay (primer arranque), espera a la primera carga.
    /// </summary>
    public async Task<InstantaneaAuditorias?> ObtenerAsync(CancellationToken ct)
    {
        if (_actual is { } datos) return datos;
        // Si la base no responde, no se reintenta en cada visita (cada intento puede tardar 30 s).
        if (UltimoError is not null && DateTime.Now - UltimoIntento < TimeSpan.FromMinutes(1)) return null;
        await RecargarAsync(ct);
        return _actual;
    }

    public async Task RecargarAsync(CancellationToken ct)
    {
        var pedido = DateTime.Now;
        await _cerrojo.WaitAsync(ct);
        try
        {
            // Si mientras se esperaba terminó otra carga, esa ya sirve.
            if (UltimoIntento >= pedido) return;

            UltimoIntento = DateTime.Now;
            var datos = await _repositorio.CargarAsync(ct);
            _actual = datos;
            UltimoError = null;
            _log.LogInformation("Auditorías cargadas: {Filas} filas en {Ms} ms", datos.Filas.Count, (long)datos.Duracion.TotalMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UltimoError = ex.Message;
            _log.LogError(ex, "No se pudieron cargar las auditorías");
        }
        finally
        {
            _cerrojo.Release();
        }
    }
}
