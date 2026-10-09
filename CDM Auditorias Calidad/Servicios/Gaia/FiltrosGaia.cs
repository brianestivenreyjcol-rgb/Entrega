using System.Globalization;
using CDM_Auditorias_Calidad.Models;

namespace CDM_Auditorias_Calidad.Servicios.Gaia;

/// <summary>
/// Los filtros de GAIA Formación tal como llegan en la URL (los de varias opciones repiten la clave).
/// Son los segmentadores comunes de las páginas del PBI.
/// </summary>
public sealed class PeticionGaia
{
    public string? Desde { get; set; }
    public string? Hasta { get; set; }
    public List<string> Marca { get; set; } = new();
    public List<string> Sector { get; set; } = new();
    public List<string> Oleada { get; set; } = new();
    public List<string> Formador { get; set; } = new();
    public List<string> Supervisor { get; set; } = new();
    /// <summary>Tipo de conexión: «1 Preconexion» … «Aseguramiento 6».</summary>
    public List<string> Conexion { get; set; } = new();
    /// <summary>ID de agente (el texto es el nombre).</summary>
    public List<string> Agente { get; set; } = new();
}

/// <summary>Los filtros resueltos contra los datos: el rango aplicado, las llamadas que quedan y los desplegables.</summary>
public sealed class FiltrosResueltosGaia
{
    public required DateOnly Desde { get; init; }
    public required DateOnly Hasta { get; init; }
    public required DateOnly DiaMin { get; init; }
    public required DateOnly DiaMax { get; init; }
    public required List<LlamadaGaia> Llamadas { get; init; }
    public required IReadOnlyList<GrupoFiltro> Desplegables { get; init; }
    public required PeticionGaia Peticion { get; init; }

    public int Dias => Hasta.DayNumber - Desde.DayNumber + 1;

    /// <summary>Si el rango no es el de por defecto (todo lo que hay).</summary>
    public bool FechasPedidas => Desde != DiaMin || Hasta != DiaMax;

    public bool HayFiltros => FechasPedidas || Desplegables.Any(g => g.Marcadas > 0);

    /// <summary>Los parámetros de la URL de estos filtros (para enlaces entre páginas y descargas).</summary>
    public IEnumerable<KeyValuePair<string, string>> Parametros()
    {
        if (Desde != DiaMin) yield return new("desde", Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (Hasta != DiaMax) yield return new("hasta", Hasta.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        foreach (var g in Desplegables)
        {
            foreach (var o in g.Opciones.Where(o => o.Marcada)) yield return new(g.Campo, o.Valor);
        }
    }
}

/// <summary>Reglas de los filtros: rango por defecto (todo), recorte a lo que hay y desplegables en cascada.</summary>
public static class FiltrosGaia
{
    /// <summary>Un desplegable: su campo en la URL, su título y de dónde sale el valor (y el texto) de cada llamada.</summary>
    private sealed record Definicion(string Campo, string Titulo, Func<PeticionGaia, List<string>> Elegidos,
        Func<LlamadaGaia, string> Valor, Func<LlamadaGaia, string>? Texto = null, bool OrdenEtapa = false);

    private static readonly Definicion[] Definiciones =
    [
        new("marca", "Marca", p => p.Marca, l => l.Marca),
        new("sector", "Sector", p => p.Sector, l => l.Sector),
        new("oleada", "Oleada", p => p.Oleada, l => l.Oleada),
        new("conexion", "Tipo de conexión", p => p.Conexion, l => l.TipoConexion, OrdenEtapa: true),
        new("formador", "Formador", p => p.Formador, l => l.Formador),
        new("supervisor", "Supervisor", p => p.Supervisor, l => l.Supervisor),
        new("agente", "Agente", p => p.Agente, l => l.IdAgente, l => l.Agente),
    ];

    /// <summary>Las claves de filtro de la URL.</summary>
    public static readonly string[] Claves = ["desde", "hasta", .. Definiciones.Select(d => d.Campo)];

    public static FiltrosResueltosGaia Resolver(IReadOnlyList<LlamadaGaia> todas, PeticionGaia p)
    {
        var diaMin = todas.Count > 0 ? todas.Min(l => l.Fecha) : DateOnly.FromDateTime(DateTime.Today);
        var diaMax = todas.Count > 0 ? todas.Max(l => l.Fecha) : diaMin;
        var desde = Acotar(Dia(p.Desde) ?? diaMin, diaMin, diaMax);
        var hasta = Acotar(Dia(p.Hasta) ?? diaMax, diaMin, diaMax);
        if (hasta < desde) (desde, hasta) = (hasta, desde);

        var enRango = todas.Where(l => l.Fecha >= desde && l.Fecha <= hasta).ToList();
        var elegidos = Definiciones.Select(d => d.Elegidos(p).Where(v => v.Length > 0).ToHashSet(StringComparer.Ordinal)).ToArray();

        bool Pasa(LlamadaGaia l, int salvo)
        {
            for (var i = 0; i < Definiciones.Length; i++)
            {
                if (i == salvo || elegidos[i].Count == 0) continue;
                if (!elegidos[i].Contains(Definiciones[i].Valor(l))) return false;
            }
            return true;
        }

        // Cada desplegable cuenta con todos los demás filtros aplicados menos el suyo.
        var grupos = new List<GrupoFiltro>();
        for (var i = 0; i < Definiciones.Length; i++)
        {
            var d = Definiciones[i];
            var conteo = enRango.Where(l => Pasa(l, i))
                .GroupBy(d.Valor)
                .Where(g => g.Key.Length > 0)
                .ToDictionary(g => g.Key, g => (Cantidad: g.Count(), Texto: d.Texto?.Invoke(g.First()) ?? g.Key));
            // Lo marcado que ya no tiene llamadas sigue en la lista (con 0) para poder quitarlo.
            foreach (var v in elegidos[i].Where(v => !conteo.ContainsKey(v)))
            {
                var texto = d.Texto is null ? v : todas.FirstOrDefault(l => d.Valor(l) == v) is { } l ? d.Texto(l) : v;
                conteo[v] = (0, texto);
            }
            var opciones = conteo
                .Select(kv => new OpcionFiltro(kv.Key, kv.Value.Texto, kv.Value.Cantidad, elegidos[i].Contains(kv.Key)));
            opciones = d.OrdenEtapa
                ? opciones.OrderBy(o => Array.IndexOf(LectorNominaGaia.ColumnasDias, o.Valor) is var x && x >= 0 ? x : 99)
                : opciones.OrderBy(o => o.Texto, StringComparer.Create(CultureInfo.GetCultureInfo("es-ES"), true));
            grupos.Add(new GrupoFiltro(d.Campo, d.Titulo, opciones.ToList()));
        }

        return new FiltrosResueltosGaia
        {
            Desde = desde, Hasta = hasta, DiaMin = diaMin, DiaMax = diaMax,
            Llamadas = enRango.Where(l => Pasa(l, -1)).ToList(),
            Desplegables = grupos,
            Peticion = p,
        };
    }

    private static DateOnly? Dia(string? texto)
        => DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static DateOnly Acotar(DateOnly d, DateOnly min, DateOnly max) => d < min ? min : d > max ? max : d;
}
