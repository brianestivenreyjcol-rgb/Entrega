using CDM_Auditorias_Calidad.Servicios.Configuracion;
using Microsoft.Extensions.Options;

namespace CDM_Auditorias_Calidad.Servicios.Datos;

/// <summary>
/// Carga las auditorías al arrancar y las recarga cada <c>Auditorias:MinutosRecarga</c> minutos.
/// </summary>
public sealed class RecargaPeriodica : BackgroundService
{
    private readonly AlmacenAuditorias _almacen;
    private readonly IOptions<OpcionesAuditorias> _opciones;

    public RecargaPeriodica(AlmacenAuditorias almacen, IOptions<OpcionesAuditorias> opciones)
    {
        _almacen = almacen;
        _opciones = opciones;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Que el arranque de la web no espere a la consulta.
        await Task.Yield();
        await _almacen.RecargarAsync(ct);

        using var reloj = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _opciones.Value.MinutosRecarga)));
        try
        {
            while (await reloj.WaitForNextTickAsync(ct))
                await _almacen.RecargarAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // La aplicación se está cerrando.
        }
    }
}
