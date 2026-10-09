using System.Diagnostics;
using System.Globalization;
using CDM_Auditorias_Calidad.Models;
using CDM_Auditorias_Calidad.Servicios.Datos;
using CDM_Auditorias_Calidad.Servicios.Gaia;
using CDM_Auditorias_Calidad.Servicios.NoSolucion;
using Microsoft.AspNetCore.Mvc;

namespace CDM_Auditorias_Calidad.Controllers;

public sealed class HomeController : Controller
{
    private readonly AlmacenAuditorias _almacen;
    private readonly ServicioNoSolucion _noSolucion;
    private readonly ServicioGaia _gaia;

    public HomeController(AlmacenAuditorias almacen, ServicioNoSolucion noSolucion, ServicioGaia gaia)
    {
        _almacen = almacen;
        _noSolucion = noSolucion;
        _gaia = gaia;
    }

    /// <summary>La portada: las tres tarjetas de informe con el estado de sus datos.</summary>
    [HttpGet("/")]
    public IActionResult Index()
    {
        DateTime? nsCargado = _noSolucion.Cache.TieneCubo()
            && DateTime.TryParse(_noSolucion.Cache.Actual().Cubo.Meta.Generado, CultureInfo.InvariantCulture, DateTimeStyles.None, out var g)
            ? g : null;
        DateTime? gaiaCargado = _gaia.Actual is { } d && d.Generado != default ? d.Generado : null;
        return View(new MenuModelo(_almacen.Actual, _almacen.UltimoError,
            new EstadoInforme(nsCargado, _noSolucion.ConstruyendoAhora),
            new EstadoInforme(gaiaCargado, _gaia.Cargando)));
    }

    [Route("/error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
        => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
