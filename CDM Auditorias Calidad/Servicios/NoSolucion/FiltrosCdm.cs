using System.Globalization;
using System.Text.RegularExpressions;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>
/// Los filtros de la pantalla CDM No solución tal y como llegan en la URL.
/// Los de varias opciones repiten la clave (<c>marca=A&amp;marca=B</c>).
/// </summary>
public sealed class PeticionCdm
{
    public string? Desde { get; set; }
    public string? Hasta { get; set; }
    public List<string> Marca { get; set; } = new();
    /// <summary>El «sector» de la pantalla: la cola de entrada.</summary>
    public List<string> Servicio { get; set; } = new();
    public List<string> Supervisor { get; set; } = new();
    public List<string> Tl { get; set; } = new();
    public List<string> Agente { get; set; } = new();
}

/// <summary>Una opción de un desplegable, con su volumen de llamadas entrantes si se conoce.</summary>
public sealed record OpcionFiltro(string Valor, long? Llamadas);

/// <summary>
/// Las tres listas de personas encadenadas (supervisor → TL → agente) y lo
/// elegido en cada una que sigue estando en su lista.
/// </summary>
public sealed record Encadenado(
    IReadOnlyList<OpcionFiltro> Supervisores,
    IReadOnlyList<OpcionFiltro> Tls,
    IReadOnlyList<OpcionFiltro> Agentes,
    IReadOnlyList<string> Supervisor,
    IReadOnlyList<string> Tl,
    IReadOnlyList<string> Agente);

/// <summary>Un atajo de fechas: su valor en la URL y su texto.</summary>
public sealed record AtajoFecha(string Valor, string Texto);

/// <summary>
/// Los filtros ya resueltos contra los datos: el rango que se aplica, las
/// listas que existen y, aparte, lo que tiene que viajar en los enlaces.
/// </summary>
public sealed record FiltrosResueltos
{
    /// <summary>Primer día que se aplica (AAAA-MM-DD).</summary>
    public required string Desde { get; init; }

    /// <summary>Último día que se aplica (AAAA-MM-DD).</summary>
    public required string Hasta { get; init; }

    /// <summary>La fecha inicial si no es la de por defecto (30 días hasta <see cref="Hasta"/>); si no, nula.</summary>
    public string? DesdePedido { get; init; }

    /// <summary>La fecha final si no es el último día con datos; si no, nula.</summary>
    public string? HastaPedido { get; init; }

    public IReadOnlyList<string> Marca { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Servicio { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Supervisor { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Tl { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Agente { get; init; } = Array.Empty<string>();

    /// <summary>Días del rango, los dos extremos incluidos.</summary>
    public int Dias => (FiltrosCdm.Dia(Hasta) - FiltrosCdm.Dia(Desde)).Days + 1;

    /// <summary>Si hay algo que «Limpiar filtros» pueda quitar.</summary>
    public bool HayFiltros => DesdePedido is not null || HastaPedido is not null
        || Marca.Count > 0 || Servicio.Count > 0 || Supervisor.Count > 0 || Tl.Count > 0 || Agente.Count > 0;

    /// <summary>Los filtros para <see cref="Agregados"/>; sin personas, para las opciones de los desplegables.</summary>
    public Filtros ParaAgregados(bool conPersonas = true) => new(
        FiltrosCdm.DiasPorDefecto, Desde, Hasta,
        Agregados.ClaveFiltro(Marca), Agregados.ClaveFiltro(Servicio),
        conPersonas ? Agregados.ClaveFiltro(Supervisor) : null,
        conPersonas ? Agregados.ClaveFiltro(Tl) : null,
        conPersonas ? Agregados.ClaveFiltro(Agente) : null);

    /// <summary>
    /// La parte de los filtros en la llave de la caché de respuestas, en el
    /// mismo orden que la API anterior: días, fechas y las cinco listas.
    /// </summary>
    public object?[] Clave(bool conPersonas = true)
    {
        var f = ParaAgregados(conPersonas);
        return new object?[] { f.Dias, f.Desde, f.Hasta, f.Marca, f.Servicio, f.Supervisor, f.Tl, f.Agente };
    }

    /// <summary>Los parámetros de la URL de estos filtros, en el orden de la barra.</summary>
    public IEnumerable<KeyValuePair<string, string>> Parametros()
    {
        if (DesdePedido is not null) yield return new("desde", DesdePedido);
        if (HastaPedido is not null) yield return new("hasta", HastaPedido);
        foreach (var v in Servicio) yield return new("servicio", v);
        foreach (var v in Supervisor) yield return new("supervisor", v);
        foreach (var v in Tl) yield return new("tl", v);
        foreach (var v in Agente) yield return new("agente", v);
        foreach (var v in Marca) yield return new("marca", v);
    }
}

/// <summary>
/// Reglas de los filtros de CDM No solución, las mismas que la pantalla en
/// React (<c>NoSolucion.jsx</c>): rango de fechas por defecto y acotado a lo
/// que hay, atajos de fecha, listas encadenadas de personas y los enlaces.
/// </summary>
public static partial class FiltrosCdm
{
    /// <summary>Días del rango cuando no se pide ninguno.</summary>
    public const int DiasPorDefecto = 30;

    /// <summary>Las claves de filtro de la URL (lo que se recuerda y lo que «Limpiar filtros» quita).</summary>
    public static readonly string[] Claves = { "desde", "hasta", "servicio", "supervisor", "tl", "agente", "marca" };

    /// <summary>Los atajos del informe. Solo rellenan las dos fechas.</summary>
    public static readonly IReadOnlyList<AtajoFecha> Atajos = new[]
    {
        new AtajoFecha("todo", "Todo el periodo"),
        new AtajoFecha("7", "Últimos 7 días"),
        new AtajoFecha("30", "Últimos 30 días"),
        new AtajoFecha("mes", "Último mes cerrado"),
    };

    private static readonly StringComparer Espanol = StringComparer.Create(CultureInfo.GetCultureInfo("es-ES"), ignoreCase: false);

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex PatronFecha();

    /// <summary>Un día AAAA-MM-DD como fecha.</summary>
    public static DateTime Dia(string iso) => DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Iso(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Si el texto es un día AAAA-MM-DD que existe.</summary>
    public static bool EsFecha(string? texto)
        => texto is not null && PatronFecha().IsMatch(texto)
           && DateTime.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    /// <summary>El primer día de un rango de <paramref name="dias"/> días que acaba en <paramref name="iso"/>.</summary>
    public static string Restar(string iso, int dias) => Iso(Dia(iso).AddDays(-(dias - 1)));

    /// <summary>El último mes entero que hay en los datos: el de <paramref name="diaMax"/> si acaba ese día, si no el anterior.</summary>
    public static (string Desde, string Hasta) MesCerrado(string diaMax)
    {
        var d = Dia(diaMax);
        var fin = d.AddDays(1).Month != d.Month ? d : new DateTime(d.Year, d.Month, 1).AddDays(-1);
        return (Iso(new DateTime(fin.Year, fin.Month, 1)), Iso(fin));
    }

    /// <summary>El rango de un atajo, con el inicio acotado al primer día con datos.</summary>
    public static (string Desde, string Hasta) RangoDeAtajo(string atajo, string diaMin, string diaMax)
    {
        (string, string) Acotar((string Desde, string Hasta) r)
            => (string.CompareOrdinal(r.Desde, diaMin) < 0 ? diaMin : r.Desde, r.Hasta);

        return atajo switch
        {
            "todo" => (diaMin, diaMax),
            "mes" => Acotar(MesCerrado(diaMax)),
            _ when int.TryParse(atajo, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
                => Acotar((Restar(diaMax, n), diaMax)),
            _ => throw new ArgumentException("Atajo de fecha desconocido: " + atajo, nameof(atajo)),
        };
    }

    /// <summary>El atajo cuyo rango es exactamente el elegido, o nulo.</summary>
    public static string? AtajoActivo(string desde, string hasta, string diaMin, string diaMax)
        => Atajos.FirstOrDefault(a => RangoDeAtajo(a.Valor, diaMin, diaMax) == (desde, hasta))?.Valor;

    /// <summary>La fecha inicial por defecto: 30 días hasta <paramref name="hasta"/>, sin salirse de los datos.</summary>
    public static string DesdePorDefecto(string hasta, string diaMin)
    {
        var d = Restar(hasta, DiasPorDefecto);
        return string.CompareOrdinal(d, diaMin) < 0 ? diaMin : d;
    }

    /// <summary>
    /// El rango que se aplica: una fecha que falta o que se sale de los datos
    /// se cambia por la de por defecto (el último día con datos y 30 días
    /// hasta él). Si la inicial queda detrás de la final no se corrige: la
    /// pantalla lo avisa.
    /// </summary>
    public static (string Desde, string Hasta) AcotarFechas(string? desde, string? hasta, string diaMin, string diaMax)
    {
        static string Tope(string? x, string diaMin, string diaMax, string porDefecto)
            => string.IsNullOrEmpty(x) || string.CompareOrdinal(x, diaMin) < 0 || string.CompareOrdinal(x, diaMax) > 0
                ? porDefecto : x;

        var h = Tope(hasta, diaMin, diaMax, diaMax);
        var d = Tope(desde, diaMin, diaMax, DesdePorDefecto(h, diaMin));
        return (d, h);
    }

    /// <summary>Los valores pedidos que existen en <paramref name="conocidos"/>, sin vacíos ni repetidos, en el orden pedido.</summary>
    public static IReadOnlyList<string> SoloConocidos(IEnumerable<string?>? pedidos, IEnumerable<string> conocidos)
    {
        var validos = new HashSet<string>(conocidos, StringComparer.Ordinal);
        return Limpiar(pedidos).Where(validos.Contains).ToList();
    }

    /// <summary>Una lista de la URL sin vacíos ni repetidos, en el orden pedido.</summary>
    public static IReadOnlyList<string> Limpiar(IEnumerable<string?>? valores)
        => Agregados.ListaFiltro(valores).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// Suma las llamadas de los agentes por supervisor o por TL (el volumen
    /// de cada opción del desplegable), por orden alfabético.
    /// </summary>
    private static List<OpcionFiltro> Agrupar(IEnumerable<OpcionAgente> agentes, Func<OpcionAgente, string> campo)
    {
        var suma = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var a in agentes) suma[campo(a)] = suma.GetValueOrDefault(campo(a)) + a.Llamadas;
        return suma.Select(p => new OpcionFiltro(p.Key, p.Value)).OrderBy(o => o.Valor, Espanol).ToList();
    }

    /// <summary>
    /// Las tres listas encadenadas, como en el informe: elegir un supervisor
    /// deja en la de TL solo a los suyos, y elegir un TL deja en la de
    /// agentes solo a su equipo. Lo elegido que ya no está en su lista (otro
    /// supervisor, o nadie con llamadas en las fechas nuevas) se descarta.
    /// </summary>
    public static Encadenado Encadenar(
        IReadOnlyList<OpcionAgente> agentes,
        IReadOnlyList<string> supervisor, IReadOnlyList<string> tl, IReadOnlyList<string> agente)
    {
        static List<string> Presentes(IEnumerable<string> elegidos, IEnumerable<OpcionFiltro> opciones)
        {
            var hay = new HashSet<string>(opciones.Select(o => o.Valor), StringComparer.Ordinal);
            return elegidos.Where(hay.Contains).ToList();
        }

        var supervisores = Agrupar(agentes, a => a.Supervisor);
        var sup = Presentes(supervisor, supervisores);
        var deSup = sup.Count > 0 ? agentes.Where(a => sup.Contains(a.Supervisor)).ToList() : agentes.ToList();

        var tls = Agrupar(deSup, a => a.Tl);
        var t = Presentes(tl, tls);
        var deTl = t.Count > 0 ? deSup.Where(a => t.Contains(a.Tl)).ToList() : deSup;

        var ags = deTl.Select(a => new OpcionFiltro(a.Agente, a.Llamadas)).OrderBy(o => o.Valor, Espanol).ToList();
        var ag = Presentes(agente, ags);

        return new Encadenado(supervisores, tls, ags, sup, t, ag);
    }

    /// <summary>
    /// Sin opciones (los filtros no dejan calcularlas), lo elegido se queda
    /// tal cual y es lo único que ofrecen las listas, para poder quitarlo.
    /// </summary>
    public static Encadenado SinEncadenar(IReadOnlyList<string> supervisor, IReadOnlyList<string> tl, IReadOnlyList<string> agente)
    {
        static List<OpcionFiltro> Solo(IEnumerable<string> v) => v.Select(x => new OpcionFiltro(x, null)).ToList();
        return new Encadenado(Solo(supervisor), Solo(tl), Solo(agente), supervisor, tl, agente);
    }

    /// <summary>Una URL con sus parámetros, codificados.</summary>
    public static string Url(string ruta, IEnumerable<KeyValuePair<string, string>> parametros)
    {
        var consulta = Consulta(parametros);
        return consulta.Length == 0 ? ruta : ruta + "?" + consulta;
    }

    /// <summary>Parámetros como texto de consulta (<c>a=1&amp;b=2</c>), sin el «?».</summary>
    public static string Consulta(IEnumerable<KeyValuePair<string, string>> parametros)
        => string.Join("&", parametros.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));
}
