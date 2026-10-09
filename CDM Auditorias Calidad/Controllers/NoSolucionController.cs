using CDM_Auditorias_Calidad.Models.NoSolucion;
using CDM_Auditorias_Calidad.Servicios.Comun;
using CDM_Auditorias_Calidad.Servicios.NoSolucion;
using Microsoft.AspNetCore.Mvc;

namespace CDM_Auditorias_Calidad.Controllers;

/// <summary>
/// CDM No solución (traído de ranking-mvc, donde es <c>/cdm</c>): resumen, equipos, motivos y sin
/// acceso a internet, con los filtros del informe en la URL, y las exportaciones a CSV.
/// </summary>
/// <remarks>
/// Aquí no hay inicio de sesión, así que no hay permiso <c>ver_nosolucion</c> ni botón solo para
/// admin: «Actualizar datos» lo puede pulsar cualquiera (como mucho una vez cada 30 s). Con la
/// cabecera <c>X-Parcial: 1</c> (la manda site.js) cada pestaña devuelve solo <c>#informe</c>.
/// </remarks>
[Route(PaginaNoSolucion.RutaBase)]
public sealed class NoSolucionController : Controller
{
    /// <summary>Clave de TempData con el resultado de «Actualizar datos».</summary>
    private const string ClaveActualizacion = "NoSolucionActualizar";

    private readonly ServicioCdm _cdm;

    public NoSolucionController(ServicioCdm cdm) => _cdm = cdm;

    [HttpGet("")]
    public IActionResult Resumen([FromQuery] PeticionCdm filtros)
    {
        var ctx = _cdm.Contexto(filtros);
        var vista = ctx.Listo ? _cdm.Resumen(ctx) : null;
        return Pagina("Resumen", new PaginaResumenNoSolucion
        {
            Contexto = ctx, Datos = vista?.Datos, AvisoVista = vista?.Aviso,
            Actualizacion = TempData[ClaveActualizacion] as string,
        });
    }

    [HttpGet("equipos")]
    public IActionResult Equipos([FromQuery] PeticionCdm filtros, string? nivel, string? orden, string? dir, string? q)
    {
        var n = ServicioCdm.Nivel(nivel);
        var ctx = _cdm.Contexto(filtros);
        var vista = ctx.Listo ? _cdm.Equipos(ctx, n, orden, dir, q) : null;
        return Pagina("Equipos", new PaginaEquiposNoSolucion
        {
            Contexto = ctx, Datos = vista?.Datos, AvisoVista = vista?.Aviso, Nivel = n,
            Actualizacion = TempData[ClaveActualizacion] as string,
        });
    }

    [HttpGet("motivos")]
    public IActionResult Motivos([FromQuery] PeticionCdm filtros)
    {
        var ctx = _cdm.Contexto(filtros);
        var vista = ctx.Listo ? _cdm.Motivos(ctx) : null;
        return Pagina("Motivos", new PaginaMotivosNoSolucion
        {
            Contexto = ctx, Datos = vista?.Datos, AvisoVista = vista?.Aviso,
            Actualizacion = TempData[ClaveActualizacion] as string,
        });
    }

    [HttpGet("internet")]
    public IActionResult Internet([FromQuery] PeticionCdm filtros)
    {
        var ctx = _cdm.Contexto(filtros);
        var vista = ctx.Listo ? _cdm.Internet(ctx) : null;
        return Pagina("Internet", new PaginaInternetNoSolucion
        {
            Contexto = ctx, Datos = vista?.Datos, AvisoVista = vista?.Aviso,
            Actualizacion = TempData[ClaveActualizacion] as string,
        });
    }

    /// <summary>Todas las no solucionadas con los filtros, en CSV; opcionalmente de una tipología (<c>n3</c>) o un impedimento (<c>bit</c>).</summary>
    [HttpGet("csv")]
    public IActionResult Csv([FromQuery] PeticionCdm filtros, string? n3, int? bit)
        => Descargar(() => _cdm.CsvLlamadas(filtros, n3, bit));

    /// <summary>La tabla de equipos tal y como se ve, en CSV.</summary>
    [HttpGet("equipos/csv")]
    public IActionResult CsvEquipos([FromQuery] PeticionCdm filtros, string? nivel, string? orden, string? dir, string? q)
        => Descargar(() => _cdm.CsvEquipos(filtros, nivel, orden, dir, q));

    /// <summary>Cada no solucionada de sinAccesoInternet con su causa (atención o proceso), en CSV; opcionalmente de una causa.</summary>
    [HttpGet("internet/causas/csv")]
    public IActionResult CsvCausas([FromQuery] PeticionCdm filtros, string? causa)
        => Descargar(() => _cdm.CsvCausas(filtros, causa));

    /// <summary>Las tipologías N3 de Motivos, en CSV.</summary>
    [HttpGet("motivos/csv")]
    public IActionResult CsvTipologias([FromQuery] PeticionCdm filtros)
        => Descargar(() => _cdm.CsvTipologias(filtros));

    /// <summary>
    /// Vuelve a traer los 3 meses de BigQuery en segundo plano, sin esperar a la renovación de
    /// cada 12 horas. Como mucho una vez cada 30 s.
    /// </summary>
    [HttpPost("actualizar")]
    [ValidateAntiForgeryToken]
    public IActionResult Actualizar(string? volver)
    {
        TempData[ClaveActualizacion] = _cdm.PedirActualizacion() ? "en-curso" : "ya-en-curso";
        var destino = !string.IsNullOrEmpty(volver) && Url.IsLocalUrl(volver)
                      && volver.StartsWith(PaginaNoSolucion.RutaBase, StringComparison.OrdinalIgnoreCase)
            ? volver : PaginaNoSolucion.RutaBase;
        return LocalRedirect(destino);
    }

    /// <summary>Pinta la pestaña (entera o solo #informe) y, ya servida, renueva el cubo si es viejo.</summary>
    private IActionResult Pagina(string vista, PaginaNoSolucion modelo)
    {
        ViewData["Parcial"] = Request.Headers["X-Parcial"] == "1";
        if (!modelo.Contexto.Preparando)
        {
            Response.OnCompleted(() =>
            {
                _cdm.RenovarSiViejo();
                return Task.CompletedTask;
            });
        }
        return View(vista, modelo);
    }

    /// <summary>El fichero; sin datos todavía, a la pantalla, que enseña «preparando».</summary>
    private IActionResult Descargar(Func<FicheroCsv?> generar)
    {
        try
        {
            var fichero = generar();
            if (fichero is null) return LocalRedirect(PaginaNoSolucion.RutaBase);
            Response.Headers.CacheControl = "private, no-store";
            return File(fichero.Contenido, "text/csv; charset=utf-8", fichero.Nombre);
        }
        catch (PeticionInvalida ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
