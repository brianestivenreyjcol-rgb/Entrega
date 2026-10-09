using CDM_Auditorias_Calidad.Servicios.Tablero;

namespace CDM_Auditorias_Calidad.Models;

/// <summary>Todo lo que pinta una página del tablero.</summary>
public sealed class TableroModelo
{
    public const string TituloInforme = "Auditorías | Control y calidad";

    /// <summary>
    /// Filtros que van plegados en «Más filtros» (guía de estilos, 4): los demás se ven siempre.
    /// </summary>
    public static readonly IReadOnlySet<string> CamposEnMasFiltros = new HashSet<string> { "cargo", "base", "plantilla" };

    public required PaginaTablero Pagina { get; init; }

    /// <summary>Los filtros ya normalizados (fechas dentro del calendario, vista resuelta).</summary>
    public required FiltrosTablero Filtros { get; init; }

    public required Vista Vista { get; init; }

    /// <summary>Rango del calendario (todas las auditorías cargadas).</summary>
    public DateOnly? CalendarioDesde { get; init; }
    public DateOnly? CalendarioHasta { get; init; }

    /// <summary>Rango elegido en el filtro de fecha.</summary>
    public DateOnly? Desde { get; init; }
    public DateOnly? Hasta { get; init; }

    public IReadOnlyList<TarjetaKpi> Tarjetas { get; init; } = [];
    public IReadOnlyList<PuntoSerie> Evolucion { get; init; } = [];
    public IReadOnlyList<BarraSector> Sectores { get; init; } = [];
    public IReadOnlyList<FilaAuditor> TopAuditores { get; init; } = [];
    public IReadOnlyList<GrupoFiltro> Grupos { get; init; } = [];

    /// <summary>El contenido de «T0 y planes de acción»; null en General y Formación.</summary>
    public InformeT0? T0 { get; init; }

    /// <summary>Auditorías que cumplen todos los filtros.</summary>
    public int TotalFiltrado { get; init; }

    public double MetaCalidad { get; init; }

    public DateTime? DatosCargadosEn { get; init; }
    public int FilasCargadas { get; init; }

    /// <summary>Error de la última recarga (si hay datos, se siguen enseñando los anteriores).</summary>
    public string? ErrorDatos { get; init; }

    public bool HayDatos => DatosCargadosEn is not null;

    public bool HayFiltros =>
        Filtros.Desde is not null || Filtros.Hasta is not null ||
        FiltrosTablero.CamposMultiples.Any(c => Filtros.Lista(c).Count > 0);
}

/// <summary>
/// Una tarjeta de la medida «Tarjetas KPI».
/// </summary>
/// <param name="Icono">Nombre del icono de trazo (<see cref="Infraestructura.Iconos"/>).</param>
/// <param name="Valor">La cifra ya formateada.</param>
/// <param name="Numero">La cifra sin formato, para que site.js la haga contar al aparecer.</param>
/// <param name="EsPorcentaje">Si <see cref="Numero"/> es una fracción (0,5 = 50 %).</param>
/// <param name="Variacion">Positiva = sube (verde); negativa = baja (rojo); null = no hay con qué comparar.</param>
/// <param name="TextoVariacion">Sin la flecha: la vista pone el icono según el signo.</param>
/// <param name="Destacada">La tarjeta resaltada de la tira (la nota de calidad).</param>
/// <param name="Ayuda">Qué fechas compara (se ve al pasar el ratón).</param>
/// <param name="Progreso">Si no es null (0 a 1), se pinta una barra de cobertura bajo la cifra (Total agentes).</param>
public sealed record TarjetaKpi(
    string Titulo,
    string Icono,
    string Valor,
    double? Numero,
    bool EsPorcentaje,
    double? Variacion,
    string TextoVariacion,
    bool Destacada,
    string Ayuda,
    double? Progreso = null);

/// <summary>Un punto de los gráficos de evolución (un día, una semana o un mes).</summary>
/// <param name="Etiqueta">Texto del eje X.</param>
/// <param name="Detalle">Texto largo para el tooltip.</param>
public sealed record PuntoSerie(string Etiqueta, string Detalle, int Cantidad, double? Nota);

public sealed record BarraSector(string Sector, int Cantidad, double? Nota, bool Marcado);

public sealed record FilaAuditor(string Auditor, string Cargo, int Cantidad, double? Nota, bool Marcado);

public sealed record OpcionFiltro(string Valor, string Texto, int Cantidad, bool Marcada);

/// <param name="Campo">Nombre del parámetro en la URL.</param>
public sealed record GrupoFiltro(string Campo, string Titulo, IReadOnlyList<OpcionFiltro> Opciones)
{
    public int Marcadas => Opciones.Count(o => o.Marcada);

    /// <summary>Un desplegable con menos de dos opciones se oculta, salvo que tenga algo marcado (guía, 4).</summary>
    public bool Visible => Opciones.Count >= 2 || Marcadas > 0;

    /// <summary>Lo que se lee en el desplegable cerrado, como en el segmentador del PBI.</summary>
    public string Resumen => Marcadas switch
    {
        0 => "Todas",
        1 => Opciones.First(o => o.Marcada).Texto,
        var n => $"Varias selecciones ({n})",
    };
}
