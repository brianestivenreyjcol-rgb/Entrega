using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CDM_Auditorias_Calidad.Servicios.Gaia;

/// <summary>Una palabra o expresión que se busca en lo que dice el agente.</summary>
/// <param name="Indice">Posición en la lista completa (es lo que guarda cada llamada).</param>
/// <param name="Patron">Sin tildes y en minúsculas, como queda la transcripción al normalizarla.</param>
/// <param name="EsEspana">De España (lo que se quiere que diga) o de Colombia (lo que se quiere evitar).</param>
/// <param name="Par">Índice del par España ↔ Colombia al que pertenece.</param>
public sealed record PalabraGaia(int Indice, string Texto, string Patron, bool EsEspana, int Par);

/// <summary>Un par de España ↔ Colombia y el nivel desde el que cuenta (1 Novato, 2 Aficionado, 3 Experto; nulo, solo en «Todas»).</summary>
public sealed record ParGaia(int Indice, IReadOnlyList<PalabraGaia> Espana, IReadOnlyList<PalabraGaia> Colombia, int? Nivel)
{
    public string TextoEspana => Espana.Count == 0 ? "—" : string.Join(" / ", Espana.Select(p => p.Texto));
    public string TextoColombia => Colombia.Count == 0 ? "—" : string.Join(" / ", Colombia.Select(p => p.Texto));
}

/// <summary>
/// La lista de palabras de la españolización (<c>Datos/palabras_gaia.json</c>): los pares y los niveles
/// de las páginas «Españolización Novato/Aficionado/Experto» del PBI.
/// </summary>
public sealed class PalabrasGaia
{
    public IReadOnlyList<string> Niveles { get; init; } = [];
    public IReadOnlyList<ParGaia> Pares { get; init; } = [];
    public IReadOnlyList<PalabraGaia> Palabras { get; init; } = [];

    /// <summary>Los pares que cuentan en un nivel (se acumulan); 0 o fuera de rango, todos.</summary>
    public IEnumerable<ParGaia> ParesDelNivel(int nivel)
        => nivel <= 0 || nivel > Niveles.Count ? Pares : Pares.Where(p => p.Nivel is { } n && n <= nivel);

    public static PalabrasGaia Leer(string ruta)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
        var raiz = doc.RootElement;
        var niveles = raiz.GetProperty("niveles").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        var pares = new List<ParGaia>();
        var palabras = new List<PalabraGaia>();
        foreach (var p in raiz.GetProperty("pares").EnumerateArray())
        {
            var indicePar = pares.Count;
            List<PalabraGaia> Lista(string clave, bool espana)
                => p.GetProperty(clave).EnumerateArray().Select(e => e.GetString() ?? "").Where(t => t.Trim().Length > 0)
                    .Select(t =>
                    {
                        var palabra = new PalabraGaia(palabras.Count, t.Trim(), Normalizar(t), espana, indicePar);
                        palabras.Add(palabra);
                        return palabra;
                    }).ToList();
            var es = Lista("espana", true);
            var col = Lista("colombia", false);
            int? nivel = null;
            if (p.TryGetProperty("nivel", out var n) && n.ValueKind == JsonValueKind.String)
            {
                var i = niveles.IndexOf(n.GetString() ?? "");
                if (i >= 0) nivel = i + 1;
            }
            pares.Add(new ParGaia(indicePar, es, col, nivel));
        }
        return new PalabrasGaia { Niveles = niveles, Pares = pares, Palabras = palabras };
    }

    /// <summary>
    /// La expresión de BigQuery que devuelve los índices encontrados («0,5,12,») buscando palabras
    /// enteras en <c>d.t</c> (lo que dice el agente, normalizado).
    /// </summary>
    public string ExpresionSql()
    {
        if (Palabras.Count == 0) return "''";
        return "CONCAT(" + string.Join(", ", Palabras.Select(p =>
            $"IF(REGEXP_CONTAINS(d.t, r'\\b{EscaparRegex(p.Patron)}\\b'), '{p.Indice},', '')")) + ")";
    }

    /// <summary>Los índices de una respuesta «0,5,12,».</summary>
    public static List<int> Indices(string texto)
        => texto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(t => int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : -1)
                .Where(i => i >= 0).Distinct().Order().ToList();

    /// <summary>Minúsculas y sin tildes (la ñ se queda), como la transcripción en la consulta.</summary>
    public static string Normalizar(string texto)
    {
        var sb = new StringBuilder();
        foreach (var c in texto.Trim().ToLowerInvariant())
        {
            if (c == 'ñ') { sb.Append(c); continue; }
            foreach (var d in c.ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(d) != UnicodeCategory.NonSpacingMark) sb.Append(d);
            }
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Solo letras, números y espacios llegan al patrón (lo demás se quita).</summary>
    private static string EscaparRegex(string patron)
        => new(patron.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
}
