using System.Globalization;
using System.Text;

namespace CDM_Auditorias_Calidad.Servicios.Tablero;

/// <summary>Eje X de los gráficos de evolución (parámetro de campo «Fecha_Parametro»).</summary>
public enum Vista { Dia, Semana, Mes }

/// <summary>
/// Los filtros de la barra izquierda, tal como llegan en la URL
/// (<c>?desde=2026-08-01&amp;sector=X&amp;sector=Y&amp;vista=semana</c>).
/// </summary>
/// <remarks>
/// Fechas en formato <c>yyyy-MM-dd</c> (el de <c>&lt;input type="date"&gt;</c>) y meses en
/// <c>yyyy-MM</c>. Los filtros de varias opciones son listas: vacía = todas.
/// </remarks>
public sealed class FiltrosTablero
{
    public string? Desde { get; set; }
    public string? Hasta { get; set; }
    public List<string> Mes { get; set; } = [];
    public List<string> Sector { get; set; } = [];
    public List<string> Super { get; set; } = [];
    public List<string> Team { get; set; } = [];
    public List<string> Auditor { get; set; } = [];
    public List<string> Cargo { get; set; } = [];
    public List<string> Base { get; set; } = [];

    /// <summary>Solo en «T0 y planes de acción»: plantilla de ICEBERG y situación frente al plan.</summary>
    public List<string> Plantilla { get; set; } = [];
    public List<string> Situacion { get; set; } = [];
    public string? Vista { get; set; }

    /// <summary>Nombres de los campos de varias opciones, en el orden de la barra.</summary>
    public static readonly string[] CamposMultiples = ["mes", "sector", "super", "team", "auditor", "cargo", "base", "plantilla", "situacion"];

    public List<string> Lista(string campo) => campo switch
    {
        "mes" => Mes,
        "sector" => Sector,
        "super" => Super,
        "team" => Team,
        "auditor" => Auditor,
        "cargo" => Cargo,
        "base" => Base,
        "plantilla" => Plantilla,
        "situacion" => Situacion,
        _ => throw new ArgumentOutOfRangeException(nameof(campo), campo, null),
    };

    public static DateOnly? LeerFecha(string? texto)
        => DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    public static string EscribirFecha(DateOnly fecha) => fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static Vista? LeerVista(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "dia" or "día" => Tablero.Vista.Dia,
        "semana" => Tablero.Vista.Semana,
        "mes" => Tablero.Vista.Mes,
        _ => null,
    };

    public static string EscribirVista(Vista vista) => vista switch
    {
        Tablero.Vista.Dia => "dia",
        Tablero.Vista.Semana => "semana",
        _ => "mes",
    };

    public FiltrosTablero Copia()
    {
        var c = new FiltrosTablero { Desde = Desde, Hasta = Hasta, Vista = Vista };
        foreach (var campo in CamposMultiples) c.Lista(campo).AddRange(Lista(campo));
        return c;
    }

    /// <summary>
    /// Copia con un valor añadido o quitado de un filtro (clic en una barra o en un auditor).
    /// </summary>
    public FiltrosTablero Alternar(string campo, string valor)
    {
        var c = Copia();
        var lista = c.Lista(campo);
        if (!lista.Remove(valor)) lista.Add(valor);
        return c;
    }

    /// <summary>Copia con otra vista de los gráficos.</summary>
    public FiltrosTablero ConVista(Vista vista)
    {
        var c = Copia();
        c.Vista = EscribirVista(vista);
        return c;
    }

    /// <summary>Solo las fechas y los meses: lo que se lleva al cambiar de página.</summary>
    public FiltrosTablero SoloFechas()
    {
        var c = new FiltrosTablero { Desde = Desde, Hasta = Hasta };
        c.Mes.AddRange(Mes);
        return c;
    }

    /// <summary>La parte <c>?…</c> de la URL (vacía si no hay filtros).</summary>
    public string Consulta()
    {
        var partes = new List<string>();
        void Poner(string clave, string? valor)
        {
            if (!string.IsNullOrEmpty(valor)) partes.Add(clave + "=" + Uri.EscapeDataString(valor));
        }

        Poner("desde", Desde);
        Poner("hasta", Hasta);
        foreach (var campo in CamposMultiples)
            foreach (var v in Lista(campo).Distinct())
                Poner(campo, v);
        Poner("vista", Vista);

        if (partes.Count == 0) return "";
        var sb = new StringBuilder("?");
        sb.AppendJoin('&', partes);
        return sb.ToString();
    }
}
