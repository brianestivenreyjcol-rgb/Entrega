using System.Globalization;
using CDM_Auditorias_Calidad.Servicios.NoSolucion;
using Microsoft.AspNetCore.Html;
using OpcionDesplegable = CDM_Auditorias_Calidad.Models.OpcionFiltro;

namespace CDM_Auditorias_Calidad.Models.NoSolucion;

/// <summary>Las pestañas de CDM No solución.</summary>
public enum PestanaNoSolucion
{
    Resumen,
    Equipos,
    Motivos,
    Internet,
}

/// <summary>
/// Lo común a las cuatro pestañas de CDM No solución (el <c>PaginaCdm</c> de ranking-mvc): el
/// contexto de datos y filtros, los avisos y los enlaces que conservan los filtros.
/// </summary>
public abstract class PaginaNoSolucion
{
    public const string RutaBase = "/nosolucion";

    public required ContextoCdm Contexto { get; init; }

    /// <summary>Resultado de pulsar «Actualizar datos»: <c>en-curso</c> o <c>ya-en-curso</c>.</summary>
    public string? Actualizacion { get; init; }

    /// <summary>Por qué la vista no se pudo calcular con estos filtros.</summary>
    public string? AvisoVista { get; init; }

    public abstract PestanaNoSolucion Pestana { get; }

    public MetaPublica? Meta => Contexto.Meta;
    public FiltrosResueltos? Filtros => Contexto.Filtros;

    /// <summary>Los avisos de filtros y de la vista, sin repetir.</summary>
    public IReadOnlyList<string> Avisos
        => Contexto.Avisos.Concat(AvisoVista is null ? [] : [AvisoVista]).Distinct().ToList();

    /// <summary>Parámetros propios de la pestaña que se conservan al cambiar los filtros (nivel, orden…).</summary>
    public virtual IEnumerable<KeyValuePair<string, string>> ParametrosPropios => [];

    public static string Ruta(PestanaNoSolucion p) => p switch
    {
        PestanaNoSolucion.Equipos => RutaBase + "/equipos",
        PestanaNoSolucion.Motivos => RutaBase + "/motivos",
        PestanaNoSolucion.Internet => RutaBase + "/internet",
        _ => RutaBase,
    };

    public static string Texto(PestanaNoSolucion p) => p switch
    {
        PestanaNoSolucion.Equipos => "Equipos",
        PestanaNoSolucion.Motivos => "Motivos",
        PestanaNoSolucion.Internet => "Sin acceso a internet",
        _ => "Resumen",
    };

    private IEnumerable<KeyValuePair<string, string>> ParametrosFiltro => Filtros?.Parametros() ?? [];

    /// <summary>El enlace a una pestaña con los filtros puestos.</summary>
    public string UrlPestana(PestanaNoSolucion p) => FiltrosCdm.Url(Ruta(p), ParametrosFiltro);

    /// <summary>Esta misma página con otros parámetros propios (para ordenar, cambiar de nivel…).</summary>
    public string UrlAqui(IEnumerable<KeyValuePair<string, string>> propios)
        => FiltrosCdm.Url(Ruta(Pestana), ParametrosFiltro.Concat(propios));

    /// <summary>Esta página con el rango de un atajo de fechas y el resto de filtros igual.</summary>
    public string UrlAtajo(string atajo)
    {
        if (Filtros is null || Meta?.DiaMin is null || Meta.DiaMax is null) return Ruta(Pestana);
        var (d, h) = FiltrosCdm.RangoDeAtajo(atajo, Meta.DiaMin, Meta.DiaMax);
        var otros = Filtros.Parametros().Where(p => p.Key is not ("desde" or "hasta"));
        var fechas = new List<KeyValuePair<string, string>>();
        // El rango por defecto no se escribe: así sigue al último día con datos.
        if (d != FiltrosCdm.DesdePorDefecto(h, Meta.DiaMin)) fechas.Add(new("desde", d));
        if (h != Meta.DiaMax) fechas.Add(new("hasta", h));
        return FiltrosCdm.Url(Ruta(Pestana), fechas.Concat(otros).Concat(ParametrosPropios));
    }

    /// <summary>El atajo de fechas que coincide con el rango elegido, o nulo.</summary>
    public string? AtajoActivo
        => Filtros is null || Meta?.DiaMin is null || Meta.DiaMax is null
            ? null : FiltrosCdm.AtajoActivo(Filtros.Desde, Filtros.Hasta, Meta.DiaMin, Meta.DiaMax);

    /// <summary>«Borrar filtros»: esta pestaña sin fechas ni filtros.</summary>
    public string UrlLimpiar => FiltrosCdm.Url(Ruta(Pestana), ParametrosPropios);

    /// <summary>El CSV de las no solucionadas con los filtros; opcionalmente, de una tipología o un impedimento.</summary>
    public string UrlCsv(string? n3 = null, int? bit = null)
    {
        var extra = new List<KeyValuePair<string, string>>();
        if (n3 is not null) extra.Add(new("n3", n3));
        if (bit is not null) extra.Add(new("bit", bit.Value.ToString(CultureInfo.InvariantCulture)));
        return FiltrosCdm.Url(RutaBase + "/csv", ParametrosFiltro.Concat(extra));
    }

    /// <summary>
    /// Los cinco desplegables del panel, en el orden del informe, con la forma de los de
    /// Auditorías (<see cref="GrupoFiltro"/>) para usar el mismo desplegable. La cifra de cada
    /// opción es su volumen de llamadas entrantes; lo elegido que ya no está va delante, para
    /// poder quitarlo.
    /// </summary>
    public IReadOnlyList<GrupoFiltro> Desplegables()
    {
        var f = Filtros;
        if (f is null) return [];

        static GrupoFiltro Grupo(string campo, string titulo, IReadOnlyList<Servicios.NoSolucion.OpcionFiltro> opciones, IReadOnlyList<string> elegidos)
        {
            var hay = new HashSet<string>(opciones.Select(o => o.Valor), StringComparer.Ordinal);
            var faltan = elegidos.Where(e => !hay.Contains(e)).Select(e => new Servicios.NoSolucion.OpcionFiltro(e, 0));
            var marcadas = new HashSet<string>(elegidos, StringComparer.Ordinal);
            var lista = faltan.Concat(opciones)
                .Select(o => new OpcionDesplegable(o.Valor, o.Valor, (int)Math.Min(int.MaxValue, o.Llamadas ?? 0), marcadas.Contains(o.Valor)))
                .ToList();
            return new GrupoFiltro(campo, titulo, lista);
        }

        return
        [
            Grupo("servicio", "Sector (cola de entrada)", Contexto.OpcionesServicio, f.Servicio),
            Grupo("supervisor", "Supervisor", Contexto.OpcionesSupervisor, f.Supervisor),
            Grupo("tl", "Team leader", Contexto.OpcionesTl, f.Tl),
            Grupo("agente", "Agente", Contexto.OpcionesAgente, f.Agente),
            Grupo("marca", "Marca", Contexto.OpcionesMarca, f.Marca),
        ];
    }

    /// <summary>Si hay días provisionales dentro del rango elegido.</summary>
    public bool HayProvisionalesEnRango
        => Meta is { DiasProvisionales.Count: > 0 } m && Filtros is { } f
           && m.DiasProvisionales.Any(d => string.CompareOrdinal(d, f.Desde) >= 0 && string.CompareOrdinal(d, f.Hasta) <= 0);

    /// <summary>
    /// Clase de color fija por marca (las series de la guía), nunca por posición: si se filtra una,
    /// las demás no cambian de color.
    /// </summary>
    public static string ClaseMarca(string marca) => marca switch
    {
        "ORANGE" => "serie-orange",
        "JAZZTEL" => "serie-jazztel",
        "YOIGO" => "serie-yoigo",
        "MASMOVIL" => "serie-masmovil",
        _ => "serie-otra",
    };
}

public sealed class PaginaResumenNoSolucion : PaginaNoSolucion
{
    public RespuestaResumen? Datos { get; init; }

    public override PestanaNoSolucion Pestana => PestanaNoSolucion.Resumen;

    /// <summary>La evolución diaria del % por marca; los días parciales van discontinuos.</summary>
    public GraficoSeries GraficoEvolucion()
    {
        var r = Datos!;
        var parciales = r.Serie.Select((x, i) => (x, i)).Where(p => p.x.Parcial).Select(p => p.i).ToHashSet();
        var series = r.Marcas.Select(m => new SerieNs(m, ClaseMarca(m),
                r.Serie.Select(x => x.Marcas.TryGetValue(m, out var d) ? d.Pct : null).ToList()))
            .ToList();
        var maximo = series.SelectMany(s => s.Valores).Where(v => v.HasValue).Select(v => v!.Value).DefaultIfEmpty(0).Max();
        return new GraficoSeries(
            Etiquetas: r.Serie.Select(x => FormatoNoSolucion.Fecha(x.Dia)).ToList(),
            Detalles: r.Serie.Select(x => FormatoNoSolucion.FechaLarga(x.Dia) + (x.Parcial ? " (parcial)" : "")).ToList(),
            Series: series,
            Discontinuos: parciales,
            Maximo: Math.Max(5, Math.Ceiling(maximo * 1.15 / 5) * 5),
            Media: r.Totales.Pct,
            Provisionales: r.Serie.Select((x, i) => (x, i)).Where(p => r.Provisionales.Contains(p.x.Dia)).Select(p => p.i).ToHashSet());
    }
}

public sealed class PaginaEquiposNoSolucion : PaginaNoSolucion
{
    public EquiposCdm? Datos { get; init; }

    /// <summary>El nivel pedido (supervisor, tl o agente), aunque la vista no se haya podido calcular.</summary>
    public required string Nivel { get; init; }

    public override PestanaNoSolucion Pestana => PestanaNoSolucion.Equipos;

    public static readonly IReadOnlyList<(string Valor, string Texto)> Niveles =
    [
        ("supervisor", "Supervisor"), ("tl", "Team leader"), ("agente", "Agente"),
    ];

    public static readonly IReadOnlyList<(string Clave, string Texto, bool Izquierda)> Columnas =
    [
        ("nombre", "Nombre", true), ("llamadas", "Llamadas", false), ("encuestadas", "Encuestadas", false),
        ("cobertura", "Cobertura", false), ("nosol", "No sol.", false), ("pct", "% no solución", false),
        ("desvio", "Desvío", false),
    ];

    public static string Plural(string nivel) => nivel switch
    {
        "tl" => "team leaders",
        "agente" => "agentes",
        _ => "supervisores",
    };

    public override IEnumerable<KeyValuePair<string, string>> ParametrosPropios
    {
        get
        {
            yield return new("nivel", Nivel);
            if (Datos is null) yield break;
            if (Datos.Orden != "pct") yield return new("orden", Datos.Orden);
            if (Datos.Direccion != (Datos.Orden == "nombre" ? "asc" : "desc")) yield return new("dir", Datos.Direccion);
            if (Datos.Busca.Length > 0) yield return new("q", Datos.Busca);
        }
    }

    /// <summary>El enlace de otro nivel: se pierde el orden y la búsqueda, que son de la tabla anterior.</summary>
    public string UrlNivel(string nivel) => UrlAqui([new KeyValuePair<string, string>("nivel", nivel)]);

    /// <summary>El enlace de la cabecera de una columna: la misma columna cambia de sentido; otra empieza en el suyo.</summary>
    public string UrlOrden(string columna)
    {
        var dir = Datos is not null && Datos.Orden == columna
            ? (Datos.Direccion == "desc" ? "asc" : "desc")
            : (columna == "nombre" ? "asc" : "desc");
        var p = new List<KeyValuePair<string, string>> { new("nivel", Nivel), new("orden", columna), new("dir", dir) };
        if (Datos is { Busca.Length: > 0 }) p.Add(new("q", Datos.Busca));
        return UrlAqui(p);
    }

    /// <summary>El CSV de la tabla tal y como se ve.</summary>
    public string UrlCsvTabla
        => FiltrosCdm.Url(RutaBase + "/equipos/csv", (Filtros?.Parametros() ?? []).Concat(ParametrosPropios));
}

public sealed class PaginaMotivosNoSolucion : PaginaNoSolucion
{
    public MotivosCdm? Datos { get; init; }

    public override PestanaNoSolucion Pestana => PestanaNoSolucion.Motivos;

    public string UrlCsvTabla => FiltrosCdm.Url(RutaBase + "/motivos/csv", Filtros?.Parametros() ?? []);
}

public sealed class PaginaInternetNoSolucion : PaginaNoSolucion
{
    public InternetCdm? Datos { get; init; }

    public override PestanaNoSolucion Pestana => PestanaNoSolucion.Internet;

    /// <summary>El CSV de cada no solucionada con su causa; con <paramref name="causa"/>, solo las de esa causa.</summary>
    public string UrlCsvCausas(string? causa = null)
        => FiltrosCdm.Url(RutaBase + "/internet/causas/csv",
            (Filtros?.Parametros() ?? []).Concat(causa is null ? [] : [new KeyValuePair<string, string>("causa", causa)]));

    /// <summary>Clase de color de cada causa (atención en naranja, proceso en azul, como los impedimentos).</summary>
    public static string ClaseCausa(string clave) => clave switch
    {
        CausaNoSolucion.Proceso => "relleno-proceso",
        CausaNoSolucion.ProcesoYAtencion => "relleno-malo",
        CausaNoSolucion.Atencion => "relleno-atencion",
        _ => "relleno-tenue",
    };

    /// <summary>Clase de color de cada cuadrante atención/proceso.</summary>
    public static string ClaseCuadrante(string clave) => clave switch
    {
        "11" => "relleno-malo",
        "01" => "relleno-proceso",
        "10" => "relleno-atencion",
        _ => "relleno-tenue",
    };

    /// <summary>Clase de color de un impedimento según sea de proceso, de atención u otro.</summary>
    public static string ClaseTipo(string? tipo) => tipo switch
    {
        "proceso" => "relleno-proceso",
        "atencion" => "relleno-atencion",
        _ => "relleno-tenue",
    };

    public static string TextoTipo(string? tipo) => tipo switch
    {
        "proceso" => "impedimento de proceso",
        "atencion" => "atribuido a la atención",
        _ => "otro",
    };
}

/// <summary>Una serie del gráfico de evolución: su nombre, su clase de color y un valor por día.</summary>
public sealed record SerieNs(string Nombre, string Clase, IReadOnlyList<double?> Valores);

/// <summary>
/// Un gráfico de líneas de varias series sobre los mismos días (el % diario por marca).
/// </summary>
/// <param name="Detalles">El texto largo de cada día, para la ficha.</param>
/// <param name="Discontinuos">Días que van con línea discontinua (los parciales).</param>
/// <param name="Media">El % del rango con todas las marcas: la línea de referencia discontinua.</param>
/// <param name="Provisionales">Días cuyas encuestas siguen llegando: no se marcan como pico.</param>
public sealed record GraficoSeries(
    IReadOnlyList<string> Etiquetas,
    IReadOnlyList<string> Detalles,
    IReadOnlyList<SerieNs> Series,
    IReadOnlySet<int> Discontinuos,
    double Maximo,
    double? Media = null,
    IReadOnlySet<int>? Provisionales = null);

/// <summary>Una barra horizontal: <c>Valor</c> manda el largo; el resto es texto.</summary>
public sealed record BarraNs(string Nombre, string? Sub, double Valor, string Etiqueta, string? Nota, string Clase);

/// <summary>Una lista de barras horizontales.</summary>
public sealed record BarrasNs(IReadOnlyList<BarraNs> Barras);

/// <summary>Las llamadas de ejemplo de un desglose, con su enlace de exportación.</summary>
public sealed record MuestrasNs(
    string Titulo, IHtmlContent Explicacion, IReadOnlyList<Muestra> Muestras, long Total, string UrlCsv,
    bool VerImpedimentos, string SinTexto);
