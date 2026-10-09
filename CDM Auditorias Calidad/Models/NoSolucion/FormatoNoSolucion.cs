using System.Globalization;
using CDM_Auditorias_Calidad.Servicios.NoSolucion;
using Microsoft.AspNetCore.Html;

namespace CDM_Auditorias_Calidad.Models.NoSolucion;

/// <summary>
/// Formato de cifras y fechas de CDM No solución. Es el <c>FormatoCdm</c> de ranking-mvc con
/// la regla de la guía de estilos: dos decimales y «%» separado por un espacio fino (19,60 %).
/// </summary>
/// <remarks>
/// Los porcentajes de No solución ya vienen en tanto por cien (19,6 = 19,6 %), no en fracción
/// como los de Auditorías. Las diferencias entre dos porcentajes se escriben con «%» y signo
/// (una resta, no una variación relativa): 19,6 % frente a 20,1 % es −0,50 %.
/// </remarks>
public static class FormatoNoSolucion
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");
    private static readonly string[] Meses = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];
    private const string Fino = " ";

    /// <summary>Entero con separador de miles también en los de cuatro cifras (9.621).</summary>
    public static string Int(double? n) => Math.Floor((n ?? 0) + 0.5).ToString("#,0", Es);

    /// <summary>Porcentaje (ya en tanto por cien) con dos decimales: 19,60 %. «—» si no hay dato.</summary>
    public static string Pct(double? n, int decimales = 2)
        => n is not { } v || !double.IsFinite(v) ? "—" : v.ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), Es) + Fino + "%";

    /// <summary>Diferencia entre dos porcentajes, con signo: +1,20 %.</summary>
    public static string Pp(double? n, int decimales = 2)
        => n is not { } v || !double.IsFinite(v) ? "—" : (v > 0 ? "+" : "") + v.ToString("F" + decimales.ToString(CultureInfo.InvariantCulture), Es) + Fino + "%";

    /// <summary>Día corto para los ejes: 27/09.</summary>
    public static string Fecha(string iso) => $"{iso.Substring(8, 2)}/{iso.Substring(5, 2)}";

    /// <summary>Día largo: 27 sep 2026.</summary>
    public static string FechaLarga(string? iso)
        => string.IsNullOrEmpty(iso) || iso.Length < 10 ? "—"
            : $"{iso.Substring(8, 2)} {Meses[int.Parse(iso.Substring(5, 2), CultureInfo.InvariantCulture) - 1]} {iso[..4]}";

    /// <summary>Cuándo se trajeron los datos: 28 sep, 09:31.</summary>
    public static string? FechaHora(string? iso)
    {
        if (string.IsNullOrEmpty(iso)) return null;
        if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return iso;
        return $"{d:dd} {Meses[d.Month - 1]}, {d:HH:mm}";
    }

    /// <summary>
    /// Pastilla de una comprobación: «Correcto» (verde), «Revisar» (ámbar) o, con nulo, «Nota»
    /// (sin color: es una explicación, no un control).
    /// </summary>
    public static IHtmlContent ChipComprobacion(bool? correcto) => correcto switch
    {
        true => new HtmlString("<span class=\"chip chip-bueno\">Correcto</span>"),
        false => new HtmlString("<span class=\"chip chip-atencion\">Revisar</span>"),
        null => new HtmlString("<span class=\"chip\">Nota</span>"),
    };

    /// <summary>
    /// La tasa y su desvío frente a la media en una pastilla del color de su tramo
    /// (<see cref="ServicioCdm.Tono"/>: +6 % crítico, +2,5 % atención, −2,5 % bueno), con el
    /// texto siempre visible.
    /// </summary>
    public static IHtmlContent ChipTasa(double? pct, double? desvio)
    {
        var tono = ServicioCdm.Tono(desvio);
        var html = new HtmlContentBuilder();
        html.AppendHtml($"<span class=\"chip{(tono is null ? "" : " chip-" + tono)}\">");
        html.Append(Pct(pct));
        if (desvio is not null)
        {
            html.AppendHtml("<span class=\"chip-desvio\">");
            html.Append("· " + Pp(desvio));
            html.AppendHtml("</span>");
        }
        html.AppendHtml("</span>");
        return html;
    }
}
